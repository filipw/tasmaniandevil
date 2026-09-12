using TasmanianDevil.Analyzer;

namespace TasmanianDevil.Recognizers.Germany;

/// <summary>
/// Recognizes German identity document serial numbers - Personalausweis (nPA) and Reisepass alike -
/// using the shared travel-document charset plus the ICAO check digit, and the legacy
/// <c>T + 8 digits</c> ID card format.
/// </summary>
/// <remarks>
/// German ID card and passport serials use one and the same format: nine characters drawn from the
/// same restricted charset (A, B, D, E, I, O, Q, S and U are excluded as visually ambiguous) with the
/// same ICAO 7-3-1 check digit. A valid number therefore carries no signal about which document it
/// belongs to, so this recognizer reports a single <c>DE_ID_DOCUMENT</c> entity rather than claiming
/// both <c>DE_ID_CARD</c> and <c>DE_PASSPORT</c> for the same span - which is what earlier versions
/// did, at full confidence, for every match.
/// </remarks>
public sealed class DeIdDocumentRecognizer : PatternRecognizer
{
    private static readonly IReadOnlyList<Pattern> DefaultPatterns =
    [
        new Pattern(
            "Ausweis-/Reisepassnummer (charset + check digit)",
            @"\b[CFGHJKLMNPRTVWXYZ][CFGHJKLMNPRTVWXYZ0-9]{7}[0-9]\b",
            0.4),
        new Pattern("Personalausweisnummer alt (T + 8 Ziffern)", @"\bT\d{8}\b", 0.5),
    ];

    private static readonly IReadOnlyList<string> DefaultContext =
    [
        // Personalausweis
        "personalausweis", "ausweis", "personalausweisnummer", "ausweisnummer", "ausweisdokument",
        "npa", "neuer personalausweis", "personalausweisgesetz", "pauwsg", "bundespersonalausweis",
        "identity card", "national id",

        // Reisepass
        "reisepass", "pass", "passnummer", "reisepassnummer", "passport", "passport number",
        "pass-nr", "bundesrepublik deutschland", "mrz",

        // shared
        "dokumentennummer", "seriennummer",
    ];

    /// <summary>Initializes a new instance of the <see cref="DeIdDocumentRecognizer"/> class.</summary>
    /// <param name="supportedEntity">The entity type to report. Defaults to <c>DE_ID_DOCUMENT</c>.</param>
    /// <param name="supportedLanguage">The analysis language this recognizer is registered for.</param>
    public DeIdDocumentRecognizer(string supportedEntity = "DE_ID_DOCUMENT", string supportedLanguage = "en")
        : base(supportedEntity, patterns: DefaultPatterns, context: DefaultContext, supportedLanguage: supportedLanguage, countryCode: "de")
    {
    }

    /// <inheritdoc />
    public override bool? ValidateResult(string patternText)
    {
        var text = patternText.ToUpperInvariant().Trim();
        if (text.Length != 9)
        {
            return false;
        }

        // the legacy "T + 8 digits" format predates the check digit and cannot be structurally
        // validated, so abstain and let the pattern score stand
        if (text[0] == 'T' && text[1..].All(char.IsAsciiDigit))
        {
            return null;
        }

        // the visually ambiguous letters are already excluded by the pattern's character class, so
        // only the check digit remains to be verified
        return IcaoCheckDigit.Validate(text);
    }
}
