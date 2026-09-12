using TasmanianDevil.Analyzer;

namespace TasmanianDevil.Azure;

/// <summary>
/// An <see cref="EntityRecognizer"/> that delegates detection to Azure AI Language via
/// <see cref="AzurePiiClient"/>. Purely async: <see cref="Analyze"/> returns no results, so the
/// synchronous <c>AnalyzerEngine.Analyze</c> path silently skips this recognizer; only
/// <see cref="AnalyzeAsync"/> (via <c>AnalyzerEngine.AnalyzeAsync</c>) calls out to Azure.
/// Fails open by default (see <see cref="AzurePiiOptions.FailOpen"/>): a remote failure yields no
/// results rather than throwing, so local recognizers still redact what they can.
/// </summary>
public sealed class AzurePiiRecognizer : EntityRecognizer, IDisposable
{
    private readonly AzurePiiClient _client;
    private readonly AzurePiiOptions _options;

    /// <summary>Initializes a new instance of the <see cref="AzurePiiRecognizer"/> class.</summary>
    /// <param name="client">The Azure PII client to delegate to.</param>
    /// <param name="options">Recognizer and transport configuration.</param>
    /// <param name="name">Optional display name. Defaults to the type name.</param>
    /// <param name="supportedLanguage">
    /// The analysis language this recognizer is registered for and sends to Azure. Defaults to
    /// <see cref="AzurePiiOptions.SupportedLanguage"/>; callers that select the language elsewhere
    /// (e.g. a guardrail builder resolving it from the PII options) pass it here so the recognizer's
    /// language always matches the language the analyzer runs with - otherwise the registry would
    /// filter this recognizer out on a language mismatch.
    /// </param>
    public AzurePiiRecognizer(AzurePiiClient client, AzurePiiOptions options, string? name = null, string? supportedLanguage = null)
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
        // Azure's own category filter (AzurePiiOptions.PiiCategories) is fixed per client, not
        // per-call; still guard against being asked for nothing this recognizer supports
        var requested = FilterSupported(entities);
        if (requested.Count == 0)
        {
            return [];
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_options.Timeout);

        try
        {
            var result = await _client.DetectAsync(text, SupportedLanguage, timeoutCts.Token).ConfigureAwait(false);
            return MapResults(result.Entities, requested, text);
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

    private List<RecognizerResult> MapResults(IReadOnlyList<AzurePiiEntity> entities, IReadOnlyList<string> requested, string text)
    {
        var map = _options.CategoryMap ?? AzurePiiCategoryMap.Default;
        var allowed = new HashSet<string>(requested);
        var results = new List<RecognizerResult>(entities.Count);

        foreach (var entity in entities)
        {
            // guard against a misbehaving service returning offsets outside the analyzed text, which
            // would otherwise throw when the anonymizer/context enhancer slices text[Start..End]
            if (entity.Offset < 0 || entity.Length <= 0 || entity.Offset + entity.Length > text.Length)
            {
                continue;
            }

            if (_options.ConfidenceThreshold is { } threshold && entity.ConfidenceScore < threshold)
            {
                continue;
            }

            var type = map.TryGetValue(entity.Category, out var mapped) ? mapped : entity.Category;

            // a remote service should only ever answer for what was requested; drop anything else
            // rather than trust it, since AnalyzerEngine does not post-filter recognizer output
            if (!allowed.Contains(type))
            {
                continue;
            }

            var score = Math.Clamp(entity.ConfidenceScore, EntityRecognizer.MinScore, EntityRecognizer.MaxScore);
            results.Add(new RecognizerResult(type, entity.Offset, entity.Offset + entity.Length, score));
        }

        return results;
    }
}
