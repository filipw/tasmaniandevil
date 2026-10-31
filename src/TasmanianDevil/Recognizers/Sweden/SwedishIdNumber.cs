namespace TasmanianDevil.Recognizers.Sweden;

/// <summary>
/// The checks shared by Swedish personal identity numbers (personnummer, samordningsnummer) and organisation numbers
/// (organisationsnummer): both end in ten digits whose last is a Luhn check digit over the other nine.
/// </summary>
internal static class SwedishIdNumber
{
    /// <summary>
    /// The ten digits a match ends in, or <c>null</c> when <paramref name="text"/> is not 10 or 12 digits with at most one
    /// <c>-</c> or <c>+</c> before the last four.
    /// </summary>
    public static string? LastTenDigits(string text, out bool hasSeparator)
    {
        hasSeparator = text.Length >= 5 && text[^5] is '-' or '+';
        var digits = hasSeparator ? string.Concat(text.AsSpan(0, text.Length - 5), text.AsSpan(text.Length - 4)) : text;
        if (digits.Length is not (10 or 12) || !digits.All(char.IsAsciiDigit))
        {
            return null;
        }

        return digits[^10..];
    }

    /// <summary>The Luhn check over ten digits (weights 2, 1, 2, 1, ... from the first).</summary>
    public static bool Luhn(string tenDigits)
    {
        var sum = 0;
        for (var i = 0; i < tenDigits.Length; i++)
        {
            var d = (tenDigits[i] - '0') * (i % 2 == 0 ? 2 : 1);
            sum += (d / 10) + (d % 10);
        }

        return sum % 10 == 0;
    }
}
