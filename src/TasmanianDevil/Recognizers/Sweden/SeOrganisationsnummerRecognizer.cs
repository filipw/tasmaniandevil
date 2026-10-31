using TasmanianDevil.Analyzer;

namespace TasmanianDevil.Recognizers.Sweden;

/// <summary>
/// Recognizes Swedish organisation numbers (organisationsnummer, also the VAT and tax number of a company): ten digits
/// <c>NNNNNN-NNNN</c>, optionally prefixed <c>16</c>, whose third digit is 2 or more and whose last is a Luhn check digit.
/// </summary>
/// <remarks>
/// <para>Validation drops a match whose third digit or check digit fails. The third digit keeps it apart from
/// personnummer, whose third digit starts the month.</para>
/// <para>Written with its hyphen, a valid number is promoted to full confidence; a bare run of ten digits abstains and keeps
/// its low base score, so a context word (<c>organisationsnummer</c>, <c>orgnr</c>, ...) is needed.</para>
/// </remarks>
public sealed class SeOrganisationsnummerRecognizer : PatternRecognizer
{
    private static readonly IReadOnlyList<Pattern> DefaultPatterns =
    [
        new Pattern("Organisationsnummer (with hyphen)", @"(?<![\d+-])(?:16)?\d{6}-\d{4}(?![\d-])", 0.5),
        new Pattern("Organisationsnummer (10 digits, context required)", @"(?<![\d+-])(?:16)?\d{10}(?![\d-])", 0.05),
    ];

    private static readonly IReadOnlyList<string> DefaultContext =
    [
        "organisationsnummer", "organisationsnr", "orgnr", "org.nr", "org nr", "momsregistreringsnummer", "bolagsverket",
        "organisation number",
    ];

    /// <summary>Initializes a new instance of the <see cref="SeOrganisationsnummerRecognizer"/> class.</summary>
    public SeOrganisationsnummerRecognizer(string supportedEntity = "SE_ORGANISATIONSNUMMER", string supportedLanguage = "en")
        : base(supportedEntity, patterns: DefaultPatterns, context: DefaultContext, supportedLanguage: supportedLanguage, countryCode: "se")
    {
    }

    /// <summary>
    /// Rejects a value whose third digit is below 2 or whose Luhn check digit fails; promotes a valid hyphenated number, and
    /// abstains (returns <c>null</c>) for a valid bare number, which still needs context.
    /// </summary>
    public override bool? ValidateResult(string patternText)
    {
        var digits = SwedishIdNumber.LastTenDigits(patternText, out var hasSeparator);
        if (digits is null || digits[2] < '2' || !SwedishIdNumber.Luhn(digits) || patternText.Contains('+'))
        {
            return false;
        }

        // a 12-digit match must carry the 16 prefix, not a century
        if (patternText.Replace("-", string.Empty, StringComparison.Ordinal).Length == 12 && !patternText.StartsWith("16", StringComparison.Ordinal))
        {
            return false;
        }

        return hasSeparator ? true : null;
    }
}
