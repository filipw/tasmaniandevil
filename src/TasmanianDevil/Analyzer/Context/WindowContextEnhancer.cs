using System.Text.RegularExpressions;

namespace TasmanianDevil.Analyzer.Context;

/// <summary>
/// A surface-form context-aware enhancer. It tokenizes the text on word boundaries and,
/// for each result, checks a window of preceding/following tokens against the recognizer's context
/// words (case-insensitive substring match), boosting the score when a context word is found.
/// This is a lemmatization-free approximation of a lemma-context-aware enhancer.
/// </summary>
public sealed partial class WindowContextEnhancer : IContextAwareEnhancer
{
    private readonly double _contextSimilarityFactor;
    private readonly double _minScoreWithContextSimilarity;
    private readonly int _contextPrefixCount;
    private readonly int _contextSuffixCount;

    /// <summary>Initializes a new instance of the <see cref="WindowContextEnhancer"/> class.</summary>
    public WindowContextEnhancer(
        double contextSimilarityFactor = 0.35,
        double minScoreWithContextSimilarity = 0.4,
        int contextPrefixCount = 5,
        int contextSuffixCount = 0)
    {
        _contextSimilarityFactor = contextSimilarityFactor;
        _minScoreWithContextSimilarity = minScoreWithContextSimilarity;
        _contextPrefixCount = contextPrefixCount;
        _contextSuffixCount = contextSuffixCount;
    }

    /// <inheritdoc />
    public IReadOnlyList<RecognizerResult> EnhanceUsingContext(
        string text,
        IReadOnlyList<RecognizerResult> rawResults,
        IReadOnlyList<EntityRecognizer> recognizers,
        IReadOnlyList<string>? externalContext = null)
    {
        // the same recognizer instance may appear twice in a registry; tolerate the duplicate Id
        var recognizerById = new Dictionary<string, EntityRecognizer>(StringComparer.Ordinal);
        foreach (var recognizer in recognizers)
        {
            recognizerById[recognizer.Id] = recognizer;
        }

        if (rawResults.Count == 0 || !recognizers.Any(r => r.Context is { Count: > 0 }))
        {
            return rawResults;
        }

        var external = externalContext?.Select(w => w.ToLowerInvariant()).ToList() ?? [];

        var tokens = Tokenize(text);

        foreach (var result in rawResults)
        {
            if (result.RecognitionMetadata is null ||
                !result.RecognitionMetadata.TryGetValue(RecognizerResult.RecognizerIdentifierKey, out var idObj) ||
                idObj is not string id ||
                !recognizerById.TryGetValue(id, out var recognizer))
            {
                continue;
            }

            if (recognizer.Context is null || recognizer.Context.Count == 0)
            {
                continue;
            }

            if (result.RecognitionMetadata.TryGetValue(RecognizerResult.IsScoreEnhancedByContextKey, out var flag) && flag is true)
            {
                continue;
            }

            var surrounding = ExtractSurroundingWords(tokens, result.Start);
            surrounding.AddRange(external);

            if (FindSupportiveWord(surrounding, recognizer.Context))
            {
                result.Score += _contextSimilarityFactor;
                result.Score = Math.Max(result.Score, _minScoreWithContextSimilarity);
                result.Score = Math.Min(result.Score, EntityRecognizer.MaxScore);
                result.RecognitionMetadata[RecognizerResult.IsScoreEnhancedByContextKey] = true;
                result.AnalysisExplanation?.SetImprovedScore(result.Score);
            }
        }

        return rawResults;
    }

    private List<string> ExtractSurroundingWords(List<(string Word, int Start, int End)> tokens, int matchStart)
    {
        if (tokens.Count == 0)
        {
            return [];
        }

        // find the token whose span covers matchStart, or the first token after it
        var index = -1;
        for (var i = 0; i < tokens.Count; i++)
        {
            if (matchStart < tokens[i].End)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            index = tokens.Count - 1;
        }

        var words = new List<string>();

        var from = Math.Max(0, index - _contextPrefixCount);
        var to = Math.Min(tokens.Count - 1, index + _contextSuffixCount);

        for (var i = from; i <= to; i++)
        {
            words.Add(tokens[i].Word.ToLowerInvariant());
        }

        // multi-word context entries cannot equal a single token, so offer adjacent n-grams too
        for (var i = from; i <= to; i++)
        {
            var phrase = tokens[i].Word.ToLowerInvariant();
            for (var n = 1; n < 4 && i + n <= to; n++)
            {
                phrase = $"{phrase} {tokens[i + n].Word.ToLowerInvariant()}";
                words.Add(phrase);
            }
        }

        return words;
    }

    private static bool FindSupportiveWord(IReadOnlyList<string> surroundingWords, IReadOnlyList<string> recognizerContext)
    {
        foreach (var contextWord in recognizerContext)
        {
            var lowered = contextWord.ToLowerInvariant();
            foreach (var word in surroundingWords)
            {
                if (word.Contains(lowered, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static List<(string Word, int Start, int End)> Tokenize(string text)
    {
        var tokens = new List<(string, int, int)>();
        foreach (Match m in WordRegex().Matches(text))
        {
            tokens.Add((m.Value, m.Index, m.Index + m.Length));
        }

        return tokens;
    }

    [GeneratedRegex(@"\w+")]
    private static partial Regex WordRegex();
}
