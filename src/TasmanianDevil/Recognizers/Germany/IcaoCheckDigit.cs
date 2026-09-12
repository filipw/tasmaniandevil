namespace TasmanianDevil.Recognizers.Germany;

/// <summary>
/// Machine readable travel document check digit (weights 7-3-1 repeating, letters mapped A=10..Z=35),
/// used by German ID card and passport document numbers.
/// </summary>
internal static class IcaoCheckDigit
{
    private static readonly int[] Weights = [7, 3, 1];

    /// <summary>
    /// Validates the trailing check digit of <paramref name="text"/> over its leading characters.
    /// Returns <c>false</c> if any character is outside the allowed set.
    /// </summary>
    public static bool Validate(string text)
    {
        // the trailing character is compared as a digit; anything else is not a check digit
        if (text.Length < 2 || !char.IsAsciiDigit(text[^1]))
        {
            return false;
        }

        var total = 0;
        for (var i = 0; i < text.Length - 1; i++)
        {
            var c = text[i];
            int value;
            if (c is >= '0' and <= '9')
            {
                value = c - '0';
            }
            else if (c is >= 'A' and <= 'Z')
            {
                value = c - 'A' + 10;
            }
            else
            {
                return false;
            }

            total += value * Weights[i % 3];
        }

        return total % 10 == text[^1] - '0';
    }
}
