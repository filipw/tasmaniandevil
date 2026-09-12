using System.Security.Cryptography;
using System.Text;

namespace TasmanianDevil.Anonymizer.Operators;

/// <summary>
/// Replaces a PII span with a salted SHA-256/512 hash.
/// <para>
/// When no <c>salt</c> parameter is supplied, a single random salt is generated once per
/// <see cref="HashOperator"/> instance. That keeps hashing <em>referentially consistent</em> within
/// one engine - the same input always yields the same digest, which is the property that makes
/// <c>hash</c> useful for linking records - while remaining unguessable. It is deliberately not
/// stable across instances or processes: pass an explicit <c>salt</c> (at least 16 bytes) whenever
/// digests must match across runs or machines.
/// </para>
/// </summary>
public sealed class HashOperator : IOperator
{
    private const string Sha256Type = "sha256";
    private const string Sha512Type = "sha512";

    private const int MinSaltBytes = 16;

    // generated once per instance, not per span: a per-span salt would make every occurrence of the
    // same value hash differently, which silently destroys the point of hashing over replace.
    private readonly byte[] _instanceSalt = RandomNumberGenerator.GetBytes(32);

    /// <inheritdoc />
    public string Name => "hash";

    /// <inheritdoc />
    public OperatorType Type => OperatorType.Anonymize;

    /// <inheritdoc />
    public string Operate(string text, IReadOnlyDictionary<string, object> parameters)
    {
        var hashType = GetHashTypeOrDefault(parameters);
        var salt = GetSalt(parameters);

        var textBytes = Encoding.UTF8.GetBytes(text);
        var salted = new byte[textBytes.Length + salt.Length];
        Buffer.BlockCopy(textBytes, 0, salted, 0, textBytes.Length);
        Buffer.BlockCopy(salt, 0, salted, textBytes.Length, salt.Length);

        var digest = hashType == Sha512Type ? SHA512.HashData(salted) : SHA256.HashData(salted);
        return Convert.ToHexStringLower(digest);
    }

    /// <inheritdoc />
    public void Validate(IReadOnlyDictionary<string, object> parameters)
    {
        var hashType = GetHashTypeOrDefault(parameters);
        if (hashType is not (Sha256Type or Sha512Type))
        {
            throw new ArgumentException($"Invalid parameter: '{OperatorParams.HashType}' must be '{Sha256Type}' or '{Sha512Type}'.");
        }

        if (parameters.TryGetValue(OperatorParams.Salt, out var saltValue))
        {
            if (NormalizeSalt(saltValue).Length < MinSaltBytes)
            {
                throw new ArgumentException(
                    $"Salt must be at least {MinSaltBytes} bytes (128 bits), or omitted to auto-generate one per operator instance.");
            }
        }
    }

    private static string GetHashTypeOrDefault(IReadOnlyDictionary<string, object> parameters) =>
        OperatorParams.Get<string>(parameters, OperatorParams.HashType, Sha256Type)!;

    private byte[] GetSalt(IReadOnlyDictionary<string, object> parameters) =>
        parameters.TryGetValue(OperatorParams.Salt, out var saltValue)
            ? NormalizeSalt(saltValue)
            : _instanceSalt;

    private static byte[] NormalizeSalt(object saltValue) => saltValue switch
    {
        byte[] bytes => bytes,
        string s => Encoding.UTF8.GetBytes(s),
        _ => throw new ArgumentException($"Invalid parameter: '{OperatorParams.Salt}' must be a string or byte array."),
    };
}
