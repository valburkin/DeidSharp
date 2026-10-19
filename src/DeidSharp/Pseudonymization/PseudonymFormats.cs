using System.Buffers.Binary;

namespace DeidSharp.Pseudonymization;

/// <summary>Shapes a pseudonym digest into the form a field needs.</summary>
public static class PseudonymFormats
{
    /// <summary>Lowercase hex, <paramref name="length"/> characters (1–64).</summary>
    public static string Hex(byte[] digest, int length = 32)
    {
        ArgumentNullException.ThrowIfNull(digest);
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, digest.Length * 2);
        return Convert.ToHexStringLower(digest)[..length];
    }

    /// <summary>
    /// A version-4-shaped UUID built from the digest. Valid wherever an id or a
    /// <c>urn:uuid:</c> reference is expected (FHIR resource ids, bundle full URLs).
    /// </summary>
    public static Guid Uuid(byte[] digest)
    {
        ArgumentNullException.ThrowIfNull(digest);
        ArgumentOutOfRangeException.ThrowIfLessThan(digest.Length, 16, nameof(digest));
        Span<byte> bytes = stackalloc byte[16];
        digest.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }

    /// <summary>A number in [0, <paramref name="exclusiveMax"/>), near-uniform for any max well below 2^63.</summary>
    public static long Index(byte[] digest, long exclusiveMax, int offset = 0)
    {
        ArgumentNullException.ThrowIfNull(digest);
        ArgumentOutOfRangeException.ThrowIfLessThan(exclusiveMax, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, digest.Length - 8);
        var raw = BinaryPrimitives.ReadUInt64BigEndian(digest.AsSpan(offset, 8)) & long.MaxValue;
        return (long)(raw % (ulong)exclusiveMax);
    }
}
