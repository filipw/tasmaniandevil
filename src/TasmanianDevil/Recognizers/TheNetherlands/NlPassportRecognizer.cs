using TasmanianDevil.Analyzer;

namespace TasmanianDevil.Recognizers.TheNetherlands;

/// <summary>
/// Recognizes Dutch passport and identity card numbers (paspoort / identiteitskaart): nine characters,
/// two leading letters then six alphanumerics and a trailing digit, drawn from a charset that excludes
/// the letter O.
/// </summary>
/// <remarks>
/// Dutch travel document numbers carry no publicly documented check digit, so this is a shape-only
/// pattern that also matches many ordinary alphanumeric identifiers. The base score is therefore
/// below the default threshold and a context word is required.
/// </remarks>
public sealed class NlPassportRecognizer : PatternRecognizer
{
    private static readonly IReadOnlyList<Pattern> DefaultPatterns =
    [
        new Pattern("Passport (9 chars, no checksum, context required)", @"\b[A-NP-Z]{2}[A-NP-Z0-9]{6}[0-9]\b", 0.2),
    ];

    private static readonly IReadOnlyList<string> DefaultContext =
    [
        "passport", "paspoort", "identiteitsbewijs", "identiteitskaart", "reisdocument",
        "documentnummer", "paspoortnummer",
    ];

    /// <summary>Initializes a new instance of the <see cref="NlPassportRecognizer"/> class.</summary>
    public NlPassportRecognizer(string supportedEntity = "NL_PASSPORT", string supportedLanguage = "en")
        : base(supportedEntity, patterns: DefaultPatterns, context: DefaultContext, supportedLanguage: supportedLanguage, countryCode: "nl")
    {
    }
}
