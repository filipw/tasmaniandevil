using TasmanianDevil.Analyzer;

namespace TasmanianDevil.Recognizers.Sweden;

/// <summary>
/// Recognizes Swedish personal identity numbers (personnummer) and coordination numbers (samordningsnummer): a birth date
/// <c>YYMMDD</c> or <c>YYYYMMDD</c>, a four-digit serial whose last digit is a Luhn check digit, written with
/// <c>-</c> (or <c>+</c> from the year someone turns 100) or without a separator.
/// </summary>
/// <remarks>
/// <para>Validation drops a match whose check digit or date fails: month 01-12, day 01-31, or 61-91 for a samordningsnummer
/// (day + 60).</para>
/// <para>Written with its separator, a valid number is distinctive enough to promote to full confidence. A bare run of ten
/// or twelve digits is not: the Luhn check passes for one in ten numbers and phone numbers such as <c>0701234567</c>
/// look like dates, so a passing bare number abstains and keeps its low base score, and a context word
/// (<c>personnummer</c>, <c>pnr</c>, <c>född</c>, ...) is needed to clear the threshold, as for <c>NL_BSN</c>.</para>
/// <para>Organisation numbers have a third digit of 2 or more (no month can), so they never validate here; see
/// <see cref="SeOrganisationsnummerRecognizer"/>.</para>
/// </remarks>
public sealed class SePersonnummerRecognizer : PatternRecognizer
{
    private static readonly IReadOnlyList<Pattern> DefaultPatterns =
    [
        new Pattern("Personnummer (with separator)", @"(?<![\d+-])(?:(?:19|20)\d{6}|\d{6})[-+]\d{4}(?![\d-])", 0.5),
        new Pattern("Personnummer (12 digits, context required)", @"(?<![\d+-])(?:19|20)\d{10}(?![\d-])", 0.1),
        new Pattern("Personnummer (10 digits, context required)", @"(?<![\d+-])\d{10}(?![\d-])", 0.05),
    ];

    private static readonly IReadOnlyList<string> DefaultContext =
    [
        "personnummer", "personnr", "pnr", "samordningsnummer", "samordningsnr", "född", "födelsenummer",
        "personal identity number",
    ];

    /// <summary>Initializes a new instance of the <see cref="SePersonnummerRecognizer"/> class.</summary>
    public SePersonnummerRecognizer(string supportedEntity = "SE_PERSONNUMMER", string supportedLanguage = "en")
        : base(supportedEntity, patterns: DefaultPatterns, context: DefaultContext, supportedLanguage: supportedLanguage, countryCode: "se")
    {
    }

    /// <summary>
    /// Rejects a value whose date or Luhn check digit fails; promotes a valid number written with its separator, and
    /// abstains (returns <c>null</c>) for a valid bare number, which still needs context.
    /// </summary>
    public override bool? ValidateResult(string patternText)
    {
        var digits = SwedishIdNumber.LastTenDigits(patternText, out var hasSeparator);
        if (digits is null || !IsDate(digits) || !SwedishIdNumber.Luhn(digits))
        {
            return false;
        }

        return hasSeparator ? true : null;
    }

    // YYMMDD: month 01-12, day 01-31, or 61-91 for a samordningsnummer
    private static bool IsDate(string digits)
    {
        var month = ((digits[2] - '0') * 10) + (digits[3] - '0');
        var day = ((digits[4] - '0') * 10) + (digits[5] - '0');
        if (day > 60)
        {
            day -= 60;
        }

        return month is >= 1 and <= 12 && day is >= 1 and <= 31;
    }
}
