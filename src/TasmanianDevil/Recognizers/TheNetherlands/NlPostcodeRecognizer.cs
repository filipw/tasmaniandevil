using TasmanianDevil.Analyzer;

namespace TasmanianDevil.Recognizers.TheNetherlands;

/// <summary>
/// Recognizes Dutch postal codes (postcode): four digits followed by two letters, e.g. <c>1234 AB</c>.
/// </summary>
/// <remarks>
/// The shape carries no checksum and collides with ordinary prose and part numbers ("3000 BC",
/// "Model 5500 XT"), so the base score is deliberately below the default threshold and a context word
/// (<c>postcode</c>, <c>adres</c>, <c>straat</c>, ...) is required to surface a match.
/// </remarks>
public sealed class NlPostcodeRecognizer : PatternRecognizer
{
    private static readonly IReadOnlyList<Pattern> DefaultPatterns =
    [
        // the combinations SA, SD and SS are not issued
        new Pattern("Postcode (4 digits + 2 letters, context required)", @"\b[1-9][0-9]{3}\s?(?!SA|SD|SS)[A-Z]{2}\b", 0.1),
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
