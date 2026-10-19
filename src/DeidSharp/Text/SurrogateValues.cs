using System.Collections.Frozen;
using System.Globalization;
using DeidSharp.Pseudonymization;

namespace DeidSharp.Text;

// Design pattern: Table-driven method.
// The pattern: behavior that varies case by case is written as rows of data that one
// piece of code reads, instead of as one branch per case.
// Here: each category with a realistic fake format is one row (category → generator).
// A new format is a new row; a category without a row keeps the original's shape.

/// <summary>
/// Realistic fakes for <see cref="SurrogateReplacement"/>, drawn from a pseudonym digest
/// (names have their own generator, <see cref="SurrogateNames"/>). Fakes use reserved
/// ranges where they exist: 555-01xx phones, example.org, 192.0.2.x, SSN area 000.
/// </summary>
internal static class SurrogateValues
{
    private static readonly FrozenDictionary<PhiCategory, Func<byte[], string>> Generators = new Dictionary<PhiCategory, Func<byte[], string>>
    {
        [PhiCategory.StreetAddress] = digest => string.Create(CultureInfo.InvariantCulture,
            $"{PseudonymFormats.Index(digest, 9899, 8) + 100} {Pick(SurrogateData.Streets, digest, 0)} {Pick(SurrogateData.StreetSuffixes, digest, 16)}"),
        [PhiCategory.Location] = digest => Pick(SurrogateData.Cities, digest, 0),
        [PhiCategory.Organization] = digest => Pick(SurrogateData.Organizations, digest, 0),
        [PhiCategory.Phone] = Phone,
        [PhiCategory.Fax] = Phone,
        [PhiCategory.Email] = digest => string.Create(CultureInfo.InvariantCulture, $"user{PseudonymFormats.Index(digest, 10000):D4}@example.org"),
        [PhiCategory.Url] = digest => "https://example.org/" + PseudonymFormats.Hex(digest, 8),
        [PhiCategory.IpAddress] = digest => string.Create(CultureInfo.InvariantCulture, $"192.0.2.{PseudonymFormats.Index(digest, 254) + 1}"),
        [PhiCategory.Ssn] = digest => string.Create(CultureInfo.InvariantCulture,
            $"000-{PseudonymFormats.Index(digest, 100):D2}-{PseudonymFormats.Index(digest, 10000, 8):D4}"),
    }.ToFrozenDictionary();

    /// <summary>A fake of the category's own format, or of the original's shape when the category has none.</summary>
    public static string Generate(PhiCategory category, string original, byte[] digest) =>
        Generators.TryGetValue(category, out var generate) ? generate(digest) : SameShape(original, digest);

    /// <summary>One value from the list, chosen by the digest bytes at <paramref name="offset"/>.</summary>
    public static string Pick(string[] values, byte[] digest, int offset) => values[PseudonymFormats.Index(digest, values.Length, offset)];

    private static string Phone(byte[] digest) =>
        string.Create(CultureInfo.InvariantCulture, $"(555) 555-01{PseudonymFormats.Index(digest, 100):D2}");

    // Keeps the identifier's shape (digits stay digits, letters stay letters in the
    // same case, separators stay put) so downstream parsers and readers see a plausible
    // value of the same format.
    private static string SameShape(string original, byte[] digest)
    {
        var fake = string.Concat(original.Select((c, i) => Reshape(c, digest[i % digest.Length])));
        if (fake != original)
        {
            return fake;
        }

        // The fake came out identical to the original: move its first digit or letter on by one.
        var chars = fake.ToCharArray();
        var first = Array.FindIndex(chars, char.IsAsciiLetterOrDigit);
        if (first < 0)
        {
            return fake;
        }

        chars[first] = Next(chars[first]);
        return new string(chars);
    }

    private static char Reshape(char c, byte b) => c switch
    {
        >= '0' and <= '9' => (char)('0' + b % 10),
        >= 'A' and <= 'Z' => (char)('A' + b % 26),
        >= 'a' and <= 'z' => (char)('a' + b % 26),
        _ => c,
    };

    // The next digit or letter, wrapping around: '9' → '0', 'z' → 'a'.
    private static char Next(char c) => c switch
    {
        >= '0' and <= '9' => (char)('0' + (c - '0' + 1) % 10),
        >= 'A' and <= 'Z' => (char)('A' + (c - 'A' + 1) % 26),
        _ => (char)('a' + (c - 'a' + 1) % 26),
    };
}
