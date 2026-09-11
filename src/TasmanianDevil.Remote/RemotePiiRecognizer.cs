using TasmanianDevil.Analyzer;

namespace TasmanianDevil.Remote;

/// <summary>
/// An <see cref="EntityRecognizer"/> that delegates detection to an out-of-process PII detector via
/// <see cref="IPiiDetectionClient"/>. Purely async: <see cref="Analyze"/> returns no results, so the
/// synchronous <c>AnalyzerEngine.Analyze</c> path silently skips this recognizer; only
/// <see cref="AnalyzeAsync"/> (via <c>AnalyzerEngine.AnalyzeAsync</c>) calls out to the remote service.
/// Fails open by default (see <see cref="RemotePiiOptions.FailOpen"/>): a remote failure yields no
/// results rather than throwing, so local recognizers still redact what they can.
/// </summary>
public sealed class RemotePiiRecognizer : EntityRecognizer, IDisposable
{
    private readonly IPiiDetectionClient _client;
    private readonly RemotePiiOptions _options;

    /// <summary>Initializes a new instance of the <see cref="RemotePiiRecognizer"/> class.</summary>
    /// <param name="client">The remote detection client to delegate to.</param>
    /// <param name="options">Recognizer and transport configuration.</param>
    /// <param name="name">Optional display name. Defaults to the type name.</param>
    /// <param name="supportedLanguage">
    /// The analysis language this recognizer is registered for and reports to the remote service.
    /// Defaults to <see cref="RemotePiiOptions.SupportedLanguage"/>; callers that select the language
    /// elsewhere (e.g. a guardrail builder resolving it from the PII options) pass it here so the
    /// recognizer's language always matches the language the analyzer runs with - otherwise the
    /// registry would filter this recognizer out on a language mismatch.
    /// </param>
    public RemotePiiRecognizer(IPiiDetectionClient client, RemotePiiOptions options, string? name = null, string? supportedLanguage = null)
        : base(options.SupportedEntities, name: name, supportedLanguage: supportedLanguage ?? options.SupportedLanguage)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        _client = client;
        _options = options;
    }

    /// <inheritdoc />
    public override bool RequiresAsync => true;

    /// <summary>
    /// Disposes the underlying client when it implements <see cref="IDisposable"/>. The single-argument
    /// client constructors create and own an <see cref="HttpClient"/>, so without this its handler and
    /// connection pool would leak; a client built from a caller-supplied
    /// <see cref="HttpClient"/> leaves that instance alone.
    /// </summary>
    public void Dispose() => (_client as IDisposable)?.Dispose();

    /// <inheritdoc />
    public override IReadOnlyList<RecognizerResult> Analyze(string text, IReadOnlyList<string> entities) => [];

    /// <inheritdoc />
    public override async ValueTask<IReadOnlyList<RecognizerResult>> AnalyzeAsync(
        string text, IReadOnlyList<string> entities, CancellationToken ct = default)
    {
        var requested = FilterSupported(entities);
        if (requested.Count == 0)
        {
            return [];
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_options.Timeout);

        try
        {
            var remoteEntities = await _client.DetectAsync(text, SupportedLanguage, requested, timeoutCts.Token).ConfigureAwait(false);
            return MapResults(remoteEntities, text, requested);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // the caller's own token was canceled, not our internal timeout - always propagate
            throw;
        }
        catch (Exception ex) when (_options.FailOpen)
        {
            _options.OnError?.Invoke(ex);
            return [];
        }
    }

    private List<string> FilterSupported(IReadOnlyList<string> entities) =>
        entities.Count == 0
            ? [.. SupportedEntities]
            : [.. entities.Where(SupportedEntities.Contains)];

    private List<RecognizerResult> MapResults(IReadOnlyList<RemotePiiEntity> entities, string text, IReadOnlyList<string> requested)
    {
        var allowed = new HashSet<string>(requested);
        var results = new List<RecognizerResult>(entities.Count);
        foreach (var entity in entities)
        {
            // guard against a misbehaving remote service returning offsets outside the analyzed text
            if (entity.Start < 0 || entity.End > text.Length || entity.Start >= entity.End)
            {
                continue;
            }

            var type = _options.CategoryMap is not null && _options.CategoryMap.TryGetValue(entity.Type, out var mapped)
                ? mapped
                : entity.Type;

            // a remote service should only ever answer for what was requested; drop anything else
            // rather than trust it, since AnalyzerEngine does not post-filter recognizer output
            if (!allowed.Contains(type))
            {
                continue;
            }

            var score = Math.Clamp(entity.Score, EntityRecognizer.MinScore, EntityRecognizer.MaxScore);
            results.Add(new RecognizerResult(type, entity.Start, entity.End, score));
        }

        return results;
    }
}
