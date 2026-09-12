using TasmanianDevil.Analyzer;

namespace TasmanianDevil.Recognizers.Germany;

/// <summary>
/// Recognizes the German Steueridentifikationsnummer (11-digit personal tax id) using regex plus the
/// ISO 7064 Mod 11,10 check digit and the digit-repetition structural rule.
/// </summary>
public sealed class DeTaxIdRecognizer : PatternRecognizer
{
    private static readonly IReadOnlyList<Pattern> DefaultPatterns =
    [
        new Pattern("Steueridentifikationsnummer (High)", @"\b[1-9]\d{10}\b", 0.5),
    ];

    private static readonly IReadOnlyList<string> DefaultContext =
    [
        "steueridentifikationsnummer", "steuer-id", "steuerid", "steuerliche identifikationsnummer",
        "steuerliche identifikation", "persönliche identifikationsnummer", "steuer identifikation",
        "idnr", "steuer-idnr", "steuernummer", "bzst",
    ];

    /// <summary>Initializes a new instance of the <see cref="DeTaxIdRecognizer"/> class.</summary>
    public DeTaxIdRecognizer(string supportedEntity = "DE_TAX_ID", string supportedLanguage = "en")
        : base(supportedEntity, patterns: DefaultPatterns, context: DefaultContext, supportedLanguage: supportedLanguage, countryCode: "de")
    {
    }

    /// <inheritdoc />
    public override bool? ValidateResult(string patternText)
    {
        // the leading digit is already constrained to 1-9 by the pattern
        if (patternText.Length != 11 || !patternText.All(char.IsAsciiDigit))
        {
            return false;
        }

        if (!HasValidDigitDistribution(patternText.AsSpan(0, 10)))
        {
            return false;
        }

        return Iso7064Mod1110.ComputeCheckDigit(patternText.AsSpan(0, 10)) == patternText[10] - '0';
    }

    /// <summary>
    /// Applies the IdNr uniqueness rule to the first ten digits: exactly one digit is repeated, and
    /// it appears either twice, or - for numbers issued from 2016 - three times but never in three
    /// consecutive positions. Every other digit appears at most once.
    /// </summary>
    private static bool HasValidDigitDistribution(ReadOnlySpan<char> digits)
    {
        var counts = new int[10];
        foreach (var c in digits)
        {
            counts[c - '0']++;
        }

        var repeated = -1;
        foreach (var digit in Enumerable.Range(0, 10))
        {
            switch (counts[digit])
            {
                case <= 1:
                    continue;
                case 2 or 3 when repeated < 0:
                    repeated = digit;
                    continue;
                default:
                    // a second repeated digit, or one appearing four or more times
                    return false;
            }
        }

        // exactly one digit must repeat - an all-distinct run of ten digits is not a valid IdNr
        if (repeated < 0)
        {
            return false;
        }

        if (counts[repeated] != 3)
        {
            return true;
        }

        // a digit occurring three times may not occupy three consecutive positions
        var wanted = (char)('0' + repeated);
        for (var i = 0; i + 2 < digits.Length; i++)
        {
            if (digits[i] == wanted && digits[i + 1] == wanted && digits[i + 2] == wanted)
            {
                return false;
            }
        }

        return true;
    }
}
