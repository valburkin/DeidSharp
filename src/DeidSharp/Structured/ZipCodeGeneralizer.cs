using System.Collections.Frozen;

namespace DeidSharp.Structured;

/// <summary>
/// Safe Harbor ZIP handling: keep only the first three digits, and replace them with
/// "000" where the three-digit area holds 20,000 people or fewer.
///
/// The default list of sparse areas is the one HHS published from the 2000 Census.
/// Safe Harbor expects the current census; pass an updated list when one is available.
/// </summary>
public sealed class ZipCodeGeneralizer
{
    /// <summary>Three-digit ZIP areas with 20,000 or fewer people (2000 Census, per HHS guidance).</summary>
    public static IReadOnlyCollection<string> Census2000SparsePrefixes { get; } =
    [
        "036", "059", "063", "102", "203", "556", "692", "790", "821", "823", "830", "831", "878", "879", "884", "890", "893",
    ];

    public static ZipCodeGeneralizer SafeHarbor { get; } = new();

    private readonly FrozenSet<string> _sparsePrefixes;

    public ZipCodeGeneralizer(IEnumerable<string>? sparsePrefixes = null)
    {
        var prefixes = (sparsePrefixes ?? Census2000SparsePrefixes).ToList();

        // "p is null" catches callers that don't use nullable annotations.
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (prefixes.Any(p => p is null || p.Length != 3 || !p.All(char.IsAsciiDigit)))
        {
            throw new ArgumentException("Sparse prefixes must be three-digit strings.", nameof(sparsePrefixes));
        }

        _sparsePrefixes = prefixes.ToFrozenSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// The three-digit area for a US ZIP or ZIP+4 ("02115", "02115-1234", "021151234"),
    /// "000" for a sparse area, or null when the value is not a US ZIP code (callers
    /// should then drop it).
    /// </summary>
    public string? Generalize(string? zip)
    {
        if (string.IsNullOrWhiteSpace(zip))
        {
            return null;
        }

        var digits = zip.Trim();
        if (digits.Length == 10 && digits[5] == '-')
        {
            digits = string.Concat(digits.AsSpan(0, 5), digits.AsSpan(6));
        }

        if ((digits.Length != 5 && digits.Length != 9) || !digits.All(char.IsAsciiDigit))
        {
            return null;
        }

        var prefix = digits[..3];
        return _sparsePrefixes.Contains(prefix) ? "000" : prefix;
    }
}
