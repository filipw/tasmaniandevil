using TasmanianDevil.Analyzer;

namespace TasmanianDevil.Recognizers.TheNetherlands;

/// <summary>
/// Recognizer for Dutch passports (Paspoort).
/// </summary>
public sealed class NlPassportRecognizer : PatternRecognizer
{
    private static readonly IReadOnlyList<Pattern> DefaultPatterns =
    [
        new Pattern(
            "Passport (9 digits, context required)",
            @"\b[A-NP-Z]{2}[A-NP-Z0-9]{6}[0-9]\b",
            0.80),
    ];

    private static readonly IReadOnlyList<string> DefaultContext =
    [
        "passport", "paspoort", "identiteitsbewijs", "identiteitskaart",
    ];

    /// <summary>Initializes a new instance of the <see cref="NlPassportRecognizer"/> class.</summary>
    public NlPassportRecognizer(string supportedEntity = "NL_PASSPORT", string supportedLanguage = "en")
        : base(supportedEntity, patterns: DefaultPatterns, context: DefaultContext, supportedLanguage: supportedLanguage, countryCode: "nl")
    {
    }
}