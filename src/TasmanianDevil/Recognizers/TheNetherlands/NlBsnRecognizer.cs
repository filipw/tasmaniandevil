using TasmanianDevil.Analyzer;

namespace TasmanianDevil.Recognizers.TheNetherlands;

/// <summary>
/// Recognizes Dutch social security numbers (BSN, burgerservicenummer) using regex plus the 11-proef
/// checksum.
/// </summary>
/// <remarks>
/// A BSN is an unadorned nine-digit number, and the 11-proef is a mod-11 check that roughly one in
/// eleven arbitrary nine-digit numbers passes - so the checksum alone is far too weak to promote a
/// match to full confidence, which would redact ordinary invoice and order numbers. Validation is
/// therefore used only to <em>reject</em>: a failing checksum drops the match, while a passing one
/// abstains and leaves the low base score in place, so a context word
/// (<c>bsn</c>, <c>burgerservicenummer</c>, ...) is needed to clear the detection threshold. This
/// mirrors how <c>US_SSN</c> treats a bare nine-digit run.
/// </remarks>
public sealed class NlBsnRecognizer : PatternRecognizer
{
    private static readonly IReadOnlyList<Pattern> DefaultPatterns =
    [
        new Pattern("BSN (9 digits, 11-proef, context required)", @"\b[1-9][0-9]{8}\b", 0.05),
    ];

    private static readonly IReadOnlyList<string> DefaultContext =
    [
        "bsn", "burgerservicenummer", "sofinummer", "sociaalverzekeringsnummer",
        "socialezekerheidsnummer", "rijksregisternummer", "belastingdienst",
    ];

    /// <summary>Initializes a new instance of the <see cref="NlBsnRecognizer"/> class.</summary>
    public NlBsnRecognizer(string supportedEntity = "NL_BSN", string supportedLanguage = "en")
        : base(supportedEntity, patterns: DefaultPatterns, context: DefaultContext, supportedLanguage: supportedLanguage, countryCode: "nl")
    {
    }

    /// <summary>
    /// Rejects a value whose 11-proef checksum fails; abstains (returns <c>null</c>) when it passes,
    /// so the match keeps its low base score and still requires supporting context.
    /// </summary>
    public override bool? ValidateResult(string patternText)
    {
        // guard before indexing: ValidateResult is public, so it must not throw on short input
        if (patternText.Length != 9 || !patternText.All(char.IsAsciiDigit))
        {
            return false;
        }

        var digits = patternText.Select(c => c - '0').ToArray();

        // a BSN cannot start with zero
        if (digits[0] == 0)
        {
            return false;
        }

        // 11-proef: weights 9 8 7 6 5 4 3 2 over the first eight digits, then minus the ninth
        var sum = 0;
        for (var i = 0; i < 8; i++)
        {
            sum += digits[i] * (9 - i);
        }

        sum -= digits[8];

        // abstain rather than promote: a passing mod-11 check is not evidence enough on its own
        return sum % 11 == 0 ? null : false;
    }
}
