using System.Collections.Frozen;
using System.Globalization;

namespace DeidSharp.Gazetteer;

/// <summary>
/// The name and place lists the <see cref="GazetteerDetector"/> searches for, loaded once from
/// the files embedded in this assembly (Data/SOURCES.md says where each comes from). Keys are
/// lowercase; a place is kept as the sequence of its words.
/// </summary>
public sealed class Gazetteer
{
    private static readonly Lazy<Gazetteer> Loaded = new(() => new Gazetteer(Resource));

    internal Gazetteer(Func<string, IEnumerable<string>> lines)
    {
        Surnames = lines("surnames.txt").Select(l => l.Split('\t'))
            .ToFrozenDictionary(f => f[0], f => int.Parse(f[1], CultureInfo.InvariantCulture), StringComparer.Ordinal);
        FirstNames = lines("first-names.txt").ToFrozenSet(StringComparer.Ordinal);
        CommonWords = lines("common-words.txt").ToFrozenSet(StringComparer.Ordinal);
        NotAlone = lines("not-alone.txt").ToFrozenSet(StringComparer.Ordinal);

        // By first word, longest first, so "Fall River" wins over a shorter place at the same word.
        Places = lines("places.txt").Select(PlaceWords).Where(w => w.Length > 0).Distinct(WordsComparer.Instance)
            .GroupBy(w => w[0], StringComparer.Ordinal)
            .ToFrozenDictionary(g => g.Key, g => (IReadOnlyList<string[]>)[.. g.OrderByDescending(w => w.Length)], StringComparer.Ordinal);
    }

    /// <summary>The lists that ship with DeidSharp.Gazetteer.</summary>
    public static Gazetteer Default => Loaded.Value;

    /// <summary>Surnames, with how many people in the 2010 Census had each.</summary>
    internal FrozenDictionary<string, int> Surnames { get; }

    internal FrozenSet<string> FirstNames { get; }

    /// <summary>Common English words: a name or place that is one counts only with context.</summary>
    internal FrozenSet<string> CommonWords { get; }

    /// <summary>Names and places that are also clinical terms ("foley"): they count only with context too.</summary>
    internal FrozenSet<string> NotAlone { get; }

    /// <summary>Places as their words, lowercase, by first word.</summary>
    internal FrozenDictionary<string, IReadOnlyList<string[]>> Places { get; }

    /// <summary>A place's words, lowercase, without the period of an abbreviation ("St. Louis" is "st", "louis").</summary>
    private static string[] PlaceWords(string place) =>
        [.. place.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w.TrimEnd('.'))];

    private static List<string> Resource(string file)
    {
        using var stream = typeof(Gazetteer).Assembly.GetManifestResourceStream($"DeidSharp.Gazetteer.Data.{file}")
            ?? throw new InvalidOperationException($"The gazetteer's {file} is missing from the assembly.");
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0 && !line.StartsWith('#'))
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    private sealed class WordsComparer : IEqualityComparer<string[]>
    {
        public static readonly WordsComparer Instance = new();

        public bool Equals(string[]? x, string[]? y) => x is not null && y is not null && x.SequenceEqual(y, StringComparer.Ordinal);

        public int GetHashCode(string[] obj) => string.Join(' ', obj).GetHashCode(StringComparison.Ordinal);
    }
}
