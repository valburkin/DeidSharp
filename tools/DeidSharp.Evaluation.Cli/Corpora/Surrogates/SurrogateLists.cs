using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DeidSharp.Evaluation.Cli.Corpora.Surrogates;

/// <summary>
/// The public-domain lists surrogates are drawn from (<c>data/surrogates/SOURCES.md</c>):
/// first names by sex and surnames, each drawn in proportion to how many people carry it,
/// and US places.
/// </summary>
internal sealed class SurrogateLists
{
    private readonly WeightedList _female;
    private readonly WeightedList _male;
    private readonly WeightedList _surnames;
    private readonly string[] _places;

    private SurrogateLists(WeightedList female, WeightedList male, WeightedList surnames, string[] places)
    {
        (_female, _male, _surnames, _places) = (female, male, surnames, places);
    }

    // SHA-256 of each list's lines joined by line feeds, so a checkout's line endings don't
    // matter. A list that changes changes the corpus: update these with GeneratorVersion.
    private static readonly (string File, string Sha256)[] BundledHashes =
    [
        ("first-names.tsv", "cbb8b8e9a70e2c30f38079763824b6a6f4559abe93fe49aea74787b4a73293fe"),
        ("surnames.txt", "800b79ba15f01c0933d2b1ef631c183da18dd2e429bb8f21e3a4d358a5f92475"),
        ("places.txt", "2c82401cb8dd91f969f9cbb9f7aed13e382465686154c4cc7b536d3768aedc95"),
    ];

    /// <summary>The copies that ship with this tool.</summary>
    private static string BundledDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "data", "surrogates");

    /// <summary>
    /// The lists that ship with this tool, refused if any differs from the copy the current
    /// generator version was built with (surnames and places are the gazetteer's, which can be regenerated).
    /// </summary>
    public static SurrogateLists LoadBundled()
    {
        foreach (var (file, expected) in BundledHashes)
        {
            if (!ContentHash(Path.Combine(BundledDirectory, file)).Equals(expected, StringComparison.Ordinal))
            {
                throw new CorpusFormatException($"The surrogate list {file} differs from the one this generator version was built with; a new list needs a new generator version.");
            }
        }

        return Load(BundledDirectory);
    }

    /// <summary>SHA-256 in lowercase hex of a list's lines joined by line feeds.</summary>
    public static string ContentHash(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', File.ReadLines(path, Encoding.UTF8)))));

    public static SurrogateLists Load(string directory)
    {
        var female = new List<(string, double)>();
        var male = new List<(string, double)>();
        foreach (var (fields, line) in Rows(Path.Combine(directory, "first-names.tsv")))
        {
            var list = fields switch
            {
                [_, "F", _] => female,
                [_, "M", _] => male,
                _ => throw Malformed("first-names.tsv", line),
            };
            list.Add((fields[0], Weight(fields[2], "first-names.tsv", line)));
        }

        var surnames = Rows(Path.Combine(directory, "surnames.txt"))
            .Select(r => r.Fields is [var name, var count] ? (TitleCase(name), Weight(count, "surnames.txt", r.Line)) : throw Malformed("surnames.txt", r.Line))
            .ToList();
        var places = Rows(Path.Combine(directory, "places.txt")).Select(r => r.Fields[0]).ToArray();
        return places.Length == 0
            ? throw new CorpusFormatException("places.txt is empty.")
            : new SurrogateLists(new WeightedList(female), new WeightedList(male), new WeightedList(surnames), places);
    }

    /// <summary>A first name for a woman (<c>'F'</c>) or a man (<c>'M'</c>).</summary>
    public string FirstName(SeededRandom random, char sex) => (sex == 'F' ? _female : _male).Pick(random);

    public string Surname(SeededRandom random) => _surnames.Pick(random);

    public string Place(SeededRandom random) => random.Pick(_places);

    private static IEnumerable<(string[] Fields, int Line)> Rows(string path)
    {
        var line = 0;
        foreach (var text in File.ReadLines(path, Encoding.UTF8))
        {
            line++;
            if (text.Length > 0 && text[0] != '#')
            {
                yield return (text.Split('\t'), line);
            }
        }
    }

    private static double Weight(string value, string file, int line) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var weight) && weight > 0 ? weight : throw Malformed(file, line);

    private static CorpusFormatException Malformed(string file, int line) => new($"{file} line {line} is not in the expected form.");

    // The Census lists surnames in one case, without apostrophes; clinical notes write them
    // capitalized, and "Mc" names with a second capital ("McDonald").
    private static string TitleCase(string name) => name switch
    {
        "" => name,
        ['m', 'c', _, ..] => "Mc" + char.ToUpperInvariant(name[2]) + name[3..],
        _ => char.ToUpperInvariant(name[0]) + name[1..],
    };

    /// <summary>Items drawn in proportion to their weights.</summary>
    private sealed class WeightedList
    {
        private readonly string[] _items;
        private readonly double[] _cumulative;

        public WeightedList(IReadOnlyList<(string Item, double Weight)> entries)
        {
            if (entries.Count == 0)
            {
                throw new CorpusFormatException("A surrogate list is empty.");
            }

            _items = [.. entries.Select(e => e.Item)];
            _cumulative = new double[entries.Count];
            var total = 0.0;
            for (var i = 0; i < entries.Count; i++)
            {
                _cumulative[i] = total += entries[i].Weight;
            }
        }

        public string Pick(SeededRandom random)
        {
            var target = random.NextDouble() * _cumulative[^1];
            var index = Array.BinarySearch(_cumulative, target);
            return _items[Math.Min(index < 0 ? ~index : index + 1, _items.Length - 1)];
        }
    }
}
