using System.Security.Cryptography;

namespace DeidSharp.Pseudonymization;

// Design pattern: Static Factory Method.
// The pattern: static methods that build an object, named for how it is built, in
// place of public constructors.
// Here: Generate, FromBytes and FromBase64 say where the key comes from (fresh random,
// raw bytes, a secret store), and the private constructor makes every path check the
// key's length.

/// <summary>
/// The secret behind every pseudonym and date shift. Whoever holds the key can
/// recompute (and so link) pseudonyms; whoever lacks it cannot reverse them. Keep a key
/// for longitudinal studies that must link runs; use <see cref="Generate"/> for a
/// one-off export whose pseudonyms should link nothing outside it.
///
/// The key bytes are never exposed through <see cref="object.ToString"/> or a public property.
/// </summary>
public sealed class PseudonymKey
{
    public const int MinimumLength = 32;

    private PseudonymKey(byte[] bytes)
    {
        if (bytes.Length < MinimumLength)
        {
            throw new ArgumentException($"A pseudonym key needs at least {MinimumLength} bytes (256 bits).", nameof(bytes));
        }

        Bytes = bytes;
    }

    /// <summary>A fresh random key: pseudonyms made with it cannot be linked to anything made with another key.</summary>
    public static PseudonymKey Generate() => new(RandomNumberGenerator.GetBytes(MinimumLength));

    public static PseudonymKey FromBytes(ReadOnlySpan<byte> bytes) => new([.. bytes]);

    /// <summary>Reads a key from base64, e.g. from a secret store or environment variable.</summary>
    public static PseudonymKey FromBase64(string base64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64);
        try
        {
            return new PseudonymKey(Convert.FromBase64String(base64.Trim()));
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("The pseudonym key is not valid base64.", nameof(base64), ex);
        }
    }

    internal byte[] Bytes { get; }

    public override string ToString() => "PseudonymKey(***)";
}
