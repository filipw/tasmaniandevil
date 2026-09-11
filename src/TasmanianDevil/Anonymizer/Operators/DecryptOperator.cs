using System.Security.Cryptography;

namespace TasmanianDevil.Anonymizer.Operators;

/// <summary>Reverses <see cref="EncryptOperator"/>, restoring the original PII text.</summary>
public sealed class DecryptOperator : IOperator
{
    /// <inheritdoc />
    public string Name => "decrypt";

    /// <inheritdoc />
    public OperatorType Type => OperatorType.Deanonymize;

    /// <inheritdoc />
    public string Operate(string text, IReadOnlyDictionary<string, object> parameters)
    {
        try
        {
            return AesCipher.Decrypt(EncryptOperator.GetKey(parameters), text);
        }
        catch (CryptographicException ex)
        {
            // AES-GCM authenticates, so this covers a wrong key, a tampered value, and a ciphertext
            // written in the pre-0.3 AES-CBC format alike; surface it rather than returning garbage.
            throw new InvalidOperationException(
                "Decryption failed: the key is incorrect, the value was tampered with, or it was " +
                "encrypted by TasmanianDevil 0.2.1 or earlier (see AesCipher.DecryptLegacyCbc).", ex);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException)
        {
            // FormatException: not base64url. ArgumentOutOfRangeException: truncated payload.
            throw new InvalidOperationException(
                "Decryption failed: the value is not a valid base64url ciphertext.", ex);
        }
    }

    /// <inheritdoc />
    public void Validate(IReadOnlyDictionary<string, object> parameters) =>
        new EncryptOperator().Validate(parameters);
}
