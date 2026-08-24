using TasmanianDevil.Analyzer;

namespace TasmanianDevil.Recognizers.TheNetherlands;

/// <summary>
/// Recognizer for Dutch social security numbers (BSN).
/// </summary>
public sealed class NlBSNRecognizer : PatternRecognizer
{
    private static readonly IReadOnlyList<Pattern> DefaultPatterns =
    [
        new Pattern(
            "BSN (9 digits, 11 proef)",
            @"\b[1-9][0-9]{8}\b",
            0.95),
    ];

    private static readonly IReadOnlyList<string> DefaultContext =
    [
        "bsn", "burgerservicenummer", "sociaalverzekeringsnummer", "socialezekerheidsnummer",
    ];

    /// <summary>Initializes a new instance of the <see cref="NlBSNRecognizer"/> class.</summary>
    public NlBSNRecognizer(string supportedEntity = "NL_BSN", string supportedLanguage = "en")
        : base(supportedEntity, patterns: DefaultPatterns, context: DefaultContext, supportedLanguage: supportedLanguage, countryCode: "nl")
    {
    }

    /// <summary>
    /// Validates a Dutch BSN using the 11-proef.
    /// </summary>
    public override bool? ValidateResult(string patternText)
    {
        var digits = patternText.Select(c => c - '0').ToArray();

        // BSN cannot start with zero.
        if (digits[0] == 0)
        {
            return false;
        }

        int sum = 0;

        // 9 8 7 6 5 4 3 2 -1
        for (int i = 0; i < 8; i++)
        {
            sum += digits[i] * (9 - i);
        }

        sum -= digits[8];

        return sum % 11 == 0;
    }
}