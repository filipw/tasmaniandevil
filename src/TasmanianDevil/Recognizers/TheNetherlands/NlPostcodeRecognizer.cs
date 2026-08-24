using TasmanianDevil.Analyzer;

namespace TasmanianDevil.Recognizers.TheNetherlands;

/// <summary>
/// Recognizer for Dutch postal codes (Postcode).
/// </summary>
public sealed class NlPostcodeRecognizer : PatternRecognizer
{
    private static readonly IReadOnlyList<Pattern> DefaultPatterns =
    [
        new Pattern(
            "Postcode (4 digits, context required)",
            @"\b[1-9][0-9]{3}\s?(?!SA|SD|SS)[A-Z]{2}\b",
            0.95),
    ];

    private static readonly IReadOnlyList<string> DefaultContext =
    [
        "postcode", "postadres", "adres", "woonplaats", "plaats", "woonadres",
        "leveringsadres", "factuuradres", "straat", "huisnummer", "postbus",
        "provincie", "gemeente", "stad", "dorp",
    ];

    /// <summary>Initializes a new instance of the <see cref="NlPostcodeRecognizer"/> class.</summary>
    public NlPostcodeRecognizer(string supportedEntity = "NL_POSTCODE", string supportedLanguage = "en")
        : base(supportedEntity, patterns: DefaultPatterns, context: DefaultContext, supportedLanguage: supportedLanguage, countryCode: "nl")
    {
    }
}