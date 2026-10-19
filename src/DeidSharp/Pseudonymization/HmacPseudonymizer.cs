using System.Security.Cryptography;
using System.Text;

namespace DeidSharp.Pseudonymization;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: the default IPseudonymizer, HMAC-SHA256 with a secret key.

/// <summary>
/// Keyed pseudonyms with HMAC-SHA256. Unlike a plain hash, a keyed hash cannot be
/// reversed by hashing every possible MRN or birth date and comparing: without the key
/// there is nothing to compare against.
/// </summary>
public sealed class HmacPseudonymizer(PseudonymKey key) : IPseudonymizer
{
    // Separates domain from value so ("ab", "c") and ("a", "bc") never collide.
    private const char Separator = '\u001F';

    private readonly PseudonymKey _key = key ?? throw new ArgumentNullException(nameof(key));

    public byte[] Digest(string domain, string value)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(value);
        if (domain.Contains(Separator, StringComparison.Ordinal))
        {
            throw new ArgumentException("A domain cannot contain the unit separator character.", nameof(domain));
        }

        return HMACSHA256.HashData(_key.Bytes, Encoding.UTF8.GetBytes(domain + Separator + value));
    }
}
