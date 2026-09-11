using TasmanianDevil.Analyzer;

namespace TasmanianDevil.Recognizers.Uk;

/// <summary>
/// Recognizes UK National Insurance Numbers (NINO) using regex over the valid prefix-letter rules.
/// </summary>
public sealed class UkNinoRecognizer : PatternRecognizer
{
    private static readonly IReadOnlyList<Pattern> DefaultPatterns =
    [
        new Pattern(
            "NINO (medium)",
            @"\b(?!bg|gb|nk|kn|nt|tn|zz)([a-ceghj-pr-tw-z][a-ceghj-npr-tw-z]) ?([0-9]{2}) ?([0-9]{2}) ?([0-9]{2}) ?([a-d])\b",
            0.5),
    ];

    private static readonly IReadOnlyList<string> DefaultContext = ["national insurance", "ni number", "nino"];

    /// <summary>Initializes a new instance of the <see cref="UkNinoRecognizer"/> class.</summary>
    public UkNinoRecognizer(string supportedEntity = "UK_NINO", string supportedLanguage = "en")
        : base(supportedEntity, patterns: DefaultPatterns, context: DefaultContext, supportedLanguage: supportedLanguage, countryCode: "uk")
    {
    }
}
