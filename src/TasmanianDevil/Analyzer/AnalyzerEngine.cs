using System.Text.RegularExpressions;
using TasmanianDevil.Analyzer.Context;

namespace TasmanianDevil.Analyzer;

/// <summary>How an allow-list entry is interpreted.</summary>
public enum AllowListMatch
{
    /// <summary>Allow only results whose text exactly equals an allow-list entry.</summary>
    Exact,

    /// <summary>Allow results whose text matches any allow-list entry interpreted as a regex.</summary>
    Regex,
}

/// <summary>
/// Orchestrates PII detection: runs recognizers, enhances scores using context, removes duplicates
/// and low-scoring results, and applies allow-lists.
/// </summary>
public sealed class AnalyzerEngine
{
    private readonly RecognizerRegistry _registry;
    private readonly IContextAwareEnhancer _contextAwareEnhancer;
    private readonly double _defaultScoreThreshold;

    /// <summary>Initializes a new instance of the <see cref="AnalyzerEngine"/> class.</summary>
    public AnalyzerEngine(
        RecognizerRegistry registry,
        IContextAwareEnhancer? contextAwareEnhancer = null,
        double defaultScoreThreshold = 0)
    {
        _registry = registry;
        _contextAwareEnhancer = contextAwareEnhancer ?? new LemmaContextAwareEnhancer();
        _defaultScoreThreshold = defaultScoreThreshold;
    }

    /// <summary>The recognizer registry backing this engine.</summary>
    public RecognizerRegistry Registry => _registry;

    /// <summary>Detects PII entities in <paramref name="text"/> for the given <paramref name="language"/>.</summary>
    public IReadOnlyList<RecognizerResult> Analyze(
        string text,
        string language = "en",
        IReadOnlyList<string>? entities = null,
        double? scoreThreshold = null,
        IReadOnlyList<string>? allowList = null,
        AllowListMatch allowListMatch = AllowListMatch.Exact,
        IReadOnlyList<string>? context = null)
    {
        var recognizers = _registry.GetRecognizers(language, entities);
        var effectiveEntities = EffectiveEntities(language, entities);

        var results = new List<RecognizerResult>();
        foreach (var recognizer in recognizers)
        {
            var current = recognizer.Analyze(text, effectiveEntities);
            if (current.Count > 0)
            {
                AddRecognizerIdIfMissing(current, recognizer);
                results.AddRange(current);
            }
        }

        return PostProcess(text, results, recognizers, scoreThreshold, allowList, allowListMatch, context);
    }

    /// <summary>
    /// Asynchronously detects PII entities in <paramref name="text"/>, awaiting each recognizer in turn
    /// (sequentially). Synchronous recognizers resolve their <see cref="EntityRecognizer.AnalyzeAsync"/>
    /// call synchronously, so mixing sync and async recognizers in one registry costs nothing extra for
    /// the sync ones.
    /// </summary>
    public async ValueTask<IReadOnlyList<RecognizerResult>> AnalyzeAsync(
        string text,
        string language = "en",
        IReadOnlyList<string>? entities = null,
        double? scoreThreshold = null,
        IReadOnlyList<string>? allowList = null,
        AllowListMatch allowListMatch = AllowListMatch.Exact,
        IReadOnlyList<string>? context = null,
        CancellationToken ct = default)
    {
        var recognizers = _registry.GetRecognizers(language, entities);
        var effectiveEntities = EffectiveEntities(language, entities);

        var results = new List<RecognizerResult>();
        foreach (var recognizer in recognizers)
        {
            var current = await recognizer.AnalyzeAsync(text, effectiveEntities, ct).ConfigureAwait(false);
            if (current.Count > 0)
            {
                AddRecognizerIdIfMissing(current, recognizer);
                results.AddRange(current);
            }
        }

        return PostProcess(text, results, recognizers, scoreThreshold, allowList, allowListMatch, context);
    }

    private IReadOnlyList<string> EffectiveEntities(string language, IReadOnlyList<string>? entities) =>
        entities is null || entities.Count == 0
            ? _registry.GetSupportedEntities(language)
            : entities;

    private List<RecognizerResult> PostProcess(
        string text,
        List<RecognizerResult> results,
        IReadOnlyList<EntityRecognizer> recognizers,
        double? scoreThreshold,
        IReadOnlyList<string>? allowList,
        AllowListMatch allowListMatch,
        IReadOnlyList<string>? context)
    {
        var enhanced = _contextAwareEnhancer.EnhanceUsingContext(text, results, recognizers, context).ToList();

        var deduped = EntityRecognizer.RemoveDuplicates(enhanced);
        var thresholded = RemoveLowScores(deduped, scoreThreshold);

        if (allowList is { Count: > 0 })
        {
            thresholded = RemoveAllowList(thresholded, allowList, text, allowListMatch);
        }

        return thresholded;
    }

    private List<RecognizerResult> RemoveLowScores(List<RecognizerResult> results, double? scoreThreshold)
    {
        var threshold = scoreThreshold ?? _defaultScoreThreshold;
        return results.Where(r => r.Score >= threshold).ToList();
    }

    private static List<RecognizerResult> RemoveAllowList(
        List<RecognizerResult> results,
        IReadOnlyList<string> allowList,
        string text,
        AllowListMatch allowListMatch)
    {
        var allowed = new List<RecognizerResult>();
        var kept = new List<RecognizerResult>(results.Count);

        var isAllowed = BuildAllowPredicate(allowList, allowListMatch);

        foreach (var result in results)
        {
            if (isAllowed(text[result.Start..result.End]))
            {
                allowed.Add(result);
            }
            else
            {
                kept.Add(result);
            }
        }

        if (allowed.Count == 0)
        {
            return kept;
        }

        // an allow-listed value must be exempt as a whole: recognizers routinely detect a narrower
        // entity inside a wider one (the URL "acme.com" inside the address "support@acme.com"), and
        // matching only each span's own text would redact part of a value the caller exempted.
        return kept.Where(r => !allowed.Any(a => r.ContainedIn(a))).ToList();
    }

    private static Func<string, bool> BuildAllowPredicate(IReadOnlyList<string> allowList, AllowListMatch allowListMatch)
    {
        if (allowListMatch != AllowListMatch.Regex)
        {
            var allowed = new HashSet<string>(allowList, StringComparer.Ordinal);
            return allowed.Contains;
        }

        var regex = new Regex(
            string.Join("|", allowList),
            PatternRecognizer.DefaultRegexOptions,
            PatternRecognizer.DefaultTimeout);

        return word =>
        {
            try
            {
                return regex.IsMatch(word);
            }
            catch (RegexMatchTimeoutException)
            {
                // cannot confirm the term is exempt, so keep the detection (fail safe: redact)
                return false;
            }
        };
    }

    private static void AddRecognizerIdIfMissing(IReadOnlyList<RecognizerResult> results, EntityRecognizer recognizer)
    {
        foreach (var result in results)
        {
            result.RecognitionMetadata ??= new Dictionary<string, object>();
            if (!result.RecognitionMetadata.ContainsKey(RecognizerResult.RecognizerIdentifierKey))
            {
                result.RecognitionMetadata[RecognizerResult.RecognizerIdentifierKey] = recognizer.Id;
            }

            if (!result.RecognitionMetadata.ContainsKey(RecognizerResult.RecognizerNameKey))
            {
                result.RecognitionMetadata[RecognizerResult.RecognizerNameKey] = recognizer.Name;
            }
        }
    }
}
