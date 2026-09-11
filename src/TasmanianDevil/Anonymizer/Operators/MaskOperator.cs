namespace TasmanianDevil.Anonymizer.Operators;

/// <summary>
/// Masks part of a PII span with a fixed character, from the start or the end.
/// </summary>
public sealed class MaskOperator : IOperator
{
    /// <inheritdoc />
    public string Name => "mask";

    /// <inheritdoc />
    public OperatorType Type => OperatorType.Anonymize;

    /// <inheritdoc />
    public string Operate(string text, IReadOnlyDictionary<string, object> parameters)
    {
        var maskingChar = OperatorParams.Get<string>(parameters, OperatorParams.MaskingChar, "*")!;
        var charsToMask = GetCharsToMask(parameters);
        var fromEnd = OperatorParams.Get<bool>(parameters, OperatorParams.FromEnd);

        var effective = Math.Min(text.Length, charsToMask > 0 ? charsToMask : 0);

        if (!fromEnd)
        {
            // never cut between the halves of a surrogate pair - that would emit a lone surrogate
            var cut = SnapForward(text, effective);
            return string.Concat(Enumerable.Repeat(maskingChar, cut)) + text[cut..];
        }

        var fromIndex = SnapBack(text, text.Length - effective);
        return text[..fromIndex] + string.Concat(Enumerable.Repeat(maskingChar, text.Length - fromIndex));
    }

    /// <inheritdoc />
    public void Validate(IReadOnlyDictionary<string, object> parameters)
    {
        var maskingChar = OperatorParams.Get<string>(parameters, OperatorParams.MaskingChar);
        if (maskingChar is null)
        {
            throw new ArgumentException($"Invalid parameter: '{OperatorParams.MaskingChar}' is required.");
        }

        if (maskingChar.Length != 1)
        {
            throw new ArgumentException($"Invalid input, '{OperatorParams.MaskingChar}' must be a single character.");
        }

        if (!parameters.ContainsKey(OperatorParams.CharsToMask) || parameters[OperatorParams.CharsToMask] is not int charsToMask)
        {
            throw new ArgumentException($"Invalid parameter: '{OperatorParams.CharsToMask}' must be an integer.");
        }

        // a non-positive count would mask nothing and silently return the PII in the clear
        if (charsToMask <= 0)
        {
            throw new ArgumentException($"Invalid input, '{OperatorParams.CharsToMask}' must be greater than zero.");
        }

        if (!parameters.ContainsKey(OperatorParams.FromEnd) || parameters[OperatorParams.FromEnd] is not bool)
        {
            throw new ArgumentException($"Invalid parameter: '{OperatorParams.FromEnd}' must be a boolean.");
        }
    }

    private static int GetCharsToMask(IReadOnlyDictionary<string, object> parameters) =>
        parameters.TryGetValue(OperatorParams.CharsToMask, out var value) && value is int i ? i : 0;

    // moves the boundary right off a low surrogate, so a masked prefix never ends mid-character
    private static int SnapForward(string text, int index) =>
        index > 0 && index < text.Length && char.IsLowSurrogate(text[index]) ? index + 1 : index;

    // moves the boundary left off a low surrogate, so a kept prefix never ends mid-character
    private static int SnapBack(string text, int index) =>
        index > 0 && index < text.Length && char.IsLowSurrogate(text[index]) ? index - 1 : index;
}
