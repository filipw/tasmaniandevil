using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace TasmanianDevil.Anonymizer.Operators;

/// <summary>
/// Authenticated AES-GCM encryption helper, base64url-encoded.
/// <para>
/// <b>Wire format.</b> <c>base64url(version ‖ nonce ‖ tag ‖ ciphertext)</c> - a one-byte version
/// marker, a 12-byte random nonce, the 16-byte authentication tag, then the ciphertext. The version
/// byte is bound in as additional authenticated data, so a tampered or downgraded marker fails
/// authentication rather than silently changing how the payload is read.
/// </para>
/// <para>
/// <b>Security properties.</b> GCM is authenticated encryption: tampering with a stored token is
/// detected and surfaces as a <see cref="CryptographicException"/> instead of yielding altered
/// plaintext. A fresh nonce is drawn per call, so encrypting the same value twice produces different
/// ciphertexts - do not rely on ciphertext equality to link records, use the <c>hash</c> operator for
/// that.
/// </para>
/// <para>
/// <b>Key material.</b> The key is used verbatim - no key derivation is applied. A key passed as a
/// <see cref="string"/> is its raw UTF-8 bytes, so a 16-character passphrase becomes an AES-128 key
/// with only as much entropy as the passphrase itself. Supply a key from
/// <see cref="RandomNumberGenerator"/> (or derive one with PBKDF2/HKDF) rather than a
/// human-chosen string.
/// </para>
/// <para>
/// <b>Migrating from 0.2.1 and earlier</b>, which used unauthenticated AES-CBC: ciphertexts produced
/// by those versions are not readable by <see cref="Decrypt"/>. Read them once with
/// <see cref="DecryptLegacyCbc"/> and re-encrypt with <see cref="Encrypt"/>.
/// </para>
/// </summary>
public static class AesCipher
{
    /// <summary>Current wire format: AES-GCM with a 12-byte nonce and a 16-byte tag.</summary>
    private const byte VersionAesGcm = 0x01;

    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int LegacyCbcIvSize = 16;

    /// <summary>
    /// Encrypts <paramref name="text"/> with <paramref name="key"/>, returning
    /// <c>base64url(version ‖ nonce ‖ tag ‖ ciphertext)</c>.
    /// </summary>
    public static string Encrypt(byte[] key, string text)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(text);

        var plaintext = Encoding.UTF8.GetBytes(text);

        var combined = new byte[1 + NonceSize + TagSize + plaintext.Length];
        combined[0] = VersionAesGcm;

        var nonce = combined.AsSpan(1, NonceSize);
        RandomNumberGenerator.Fill(nonce);

        var tag = combined.AsSpan(1 + NonceSize, TagSize);
        var ciphertext = combined.AsSpan(1 + NonceSize + TagSize);

        using var aesGcm = new AesGcm(key, TagSize);

        // bind the version marker so a downgrade attempt fails authentication
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, associatedData: combined.AsSpan(0, 1));

        return Base64Url.EncodeToString(combined);
    }

    /// <summary>
    /// Decrypts a value produced by <see cref="Encrypt"/>.
    /// </summary>
    /// <exception cref="CryptographicException">
    /// The key is wrong, or the value was tampered with. For a value written by 0.2.1 or earlier, use
    /// <see cref="DecryptLegacyCbc"/>.
    /// </exception>
    /// <exception cref="FormatException">The value is not valid base64url.</exception>
    public static string Decrypt(byte[] key, string text)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(text);

        var combined = Base64Url.DecodeFromChars(text);
        if (combined.Length < 1 + NonceSize + TagSize || combined[0] != VersionAesGcm)
        {
            throw new CryptographicException(
                "Unrecognized ciphertext format. Values written by TasmanianDevil 0.2.1 or earlier use " +
                $"AES-CBC; read them with {nameof(DecryptLegacyCbc)} and re-encrypt with {nameof(Encrypt)}.");
        }

        var nonce = combined.AsSpan(1, NonceSize);
        var tag = combined.AsSpan(1 + NonceSize, TagSize);
        var ciphertext = combined.AsSpan(1 + NonceSize + TagSize);
        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, associatedData: combined.AsSpan(0, 1));

        return Encoding.UTF8.GetString(plaintext);
    }

    /// <summary>
    /// Decrypts a value written by TasmanianDevil 0.2.1 or earlier, which used unauthenticated
    /// AES-CBC with a prepended IV. Provided only to migrate stored data: the plaintext it returns is
    /// <b>not authenticated</b>, so a tampered value decrypts to altered text without error. Re-encrypt
    /// with <see cref="Encrypt"/> and stop using this method.
    /// </summary>
    public static string DecryptLegacyCbc(byte[] key, string text)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(text);

        var combined = Base64Url.DecodeFromChars(text);
        if (combined.Length <= LegacyCbcIvSize)
        {
            throw new CryptographicException("Value is too short to contain a legacy AES-CBC IV and ciphertext.");
        }

        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        return Encoding.UTF8.GetString(
            aes.DecryptCbc(combined.AsSpan(LegacyCbcIvSize), combined.AsSpan(0, LegacyCbcIvSize)));
    }

    /// <summary>Returns <c>true</c> if the key length is a valid AES key size (128/192/256 bits).</summary>
    public static bool IsValidKeySize(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.Length is 16 or 24 or 32;
    }
}
