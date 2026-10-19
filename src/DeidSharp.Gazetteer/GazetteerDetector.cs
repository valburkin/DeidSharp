using System.Collections.Frozen;
using System.Text.RegularExpressions;
using DeidSharp.Detection;

namespace DeidSharp.Gazetteer;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: one IPhiDetector, the one that looks words up in lists of names and places. It
// stacks with the pattern, known-identifier and NER detectors like any other.

/// <summary>
/// Finds names and places by looking capitalized words up in public-domain lists
/// (<see cref="Gazetteer"/>): US Census surnames, first names and places. A word that is also
/// a common English word or a clinical term ("Will", "Brown", "Foley") counts only with
/// context: after a title or a relation ("Dr. Will", "daughter Hope"), next to another name
/// ("Mark Brown"), or, for a place, after "in", "from" or "to", or before a state ("Hope, AR").
/// </summary>
/// <remarks>
/// Only words written with a capital and at least one lowercase letter are looked up, so an
/// abbreviation in capitals ("PT", "ABD") is never a name. Deterministic: the same text always
/// gives the same spans. It is not part of the leak guard, which stays with the known-identifier
/// and pattern detectors.
/// </remarks>
public sealed partial class GazetteerDetector : IDeterministicPhiDetector
{
    private static readonly FrozenSet<string> Titles = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase, "dr", "doctor", "mr", "mrs", "ms", "miss", "mx", "prof", "professor", "nurse");

    private static readonly FrozenSet<string> Relations = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "daughter", "son", "wife", "husband", "spouse", "partner", "mother", "mom", "father", "dad", "sister", "brother",
        "friend", "neighbor", "nephew", "niece", "grandson", "granddaughter", "grandmother", "grandfather", "aunt", "uncle",
        "cousin", "girlfriend", "boyfriend", "fiance", "fiancee", "stepson", "stepdaughter", "guardian", "proxy");

    private static readonly FrozenSet<string> PlacePrepositions = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase, "in", "from", "to", "at", "near", "outside");

    private static readonly FrozenSet<string> States = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "alabama", "alaska", "arizona", "arkansas", "california", "colorado", "connecticut", "delaware", "florida", "georgia",
        "hawaii", "idaho", "illinois", "indiana", "iowa", "kansas", "kentucky", "louisiana", "maine", "maryland",
        "massachusetts", "michigan", "minnesota", "mississippi", "missouri", "montana", "nebraska", "nevada", "ohio",
        "oklahoma", "oregon", "pennsylvania", "tennessee", "texas", "utah", "vermont", "virginia", "washington",
        "wisconsin", "wyoming", "al", "ak", "az", "ar", "ca", "co", "ct", "de", "fl", "ga", "hi", "id", "il", "in", "ia",
        "ks", "ky", "la", "me", "md", "ma", "mi", "mn", "ms", "mo", "mt", "ne", "nv", "nh", "nj", "nm", "ny", "nc", "nd",
        "oh", "ok", "or", "pa", "ri", "sc", "sd", "tn", "tx", "ut", "vt", "va", "wa", "wv", "wi", "wy", "dc");

    private readonly Gazetteer _gazetteer;
    private readonly GazetteerOptions _options;

    /// <param name="gazetteer">The lists to search; null for <see cref="Gazetteer.Default"/>.</param>
    public GazetteerDetector(Gazetteer? gazetteer = null, GazetteerOptions? options = null)
    {
        _gazetteer = gazetteer ?? Gazetteer.Default;
        _options = options ?? GazetteerOptions.Default;
        _options.Validate();
    }

    public string Name => "gazetteer";

    public IReadOnlyList<PhiSpan> Detect(DetectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var text = request.Text;
        var words = Word().Matches(text).Select(m => new Token(m.Index, m.Length, m.Value, _options)).ToList();
        var found = new PhiCategory?[words.Count];

        Places(text, words, found);
        cancellationToken.ThrowIfCancellationRequested();
        Names(text, words, found);

        // Neighboring words of one kind become one span ("Ellen Hart", "Fall River").
        var spans = new List<PhiSpan>();
        for (var i = 0; i < words.Count; i++)
        {
            if (found[i] is not { } category)
            {
                continue;
            }

            var last = i;
            while (last + 1 < words.Count && found[last + 1] == category && Adjacent(text, words[last], words[last + 1]))
            {
                last++;
            }

            var source = category == PhiCategory.Name ? "name" : "place";
            spans.Add(new PhiSpan(words[i].Start, words[last].Start + words[last].Key.Length, category, 0.8, $"{Name}:{source}"));
            i = last;
        }

        return spans;
    }

    public Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Detect(request, cancellationToken));

    private void Places(string text, List<Token> words, PhiCategory?[] found)
    {
        for (var i = 0; i < words.Count; i++)
        {
            if (!words[i].Capitalized || !_gazetteer.Places.TryGetValue(words[i].Key, out var places))
            {
                continue;
            }

            var match = places.FirstOrDefault(p => Matches(text, words, i, p));
            if (match is null)
            {
                continue;
            }

            // One word that is also a common word, a clinical term or a name needs place context.
            if (match.Length == 1 && (Ambiguous(words[i].Key) || IsName(words[i].Key)) && !PlaceContext(text, words, i))
            {
                continue;
            }

            for (var j = i; j < i + match.Length; j++)
            {
                found[j] = PhiCategory.Location;
            }

            i += match.Length - 1;
        }
    }

    private void Names(string text, List<Token> words, PhiCategory?[] found)
    {
        // A name that is no common word or clinical term counts on its own.
        for (var i = 0; i < words.Count; i++)
        {
            if (Candidate(i) && IsName(words[i].Key) && !Ambiguous(words[i].Key))
            {
                found[i] = PhiCategory.Name;
            }
        }

        // A first name followed by a surname is a name, even if both are common words ("Mark Brown").
        for (var i = 0; i + 1 < words.Count; i++)
        {
            if (Candidate(i) && Candidate(i + 1) && Adjacent(text, words[i], words[i + 1])
                && _gazetteer.FirstNames.Contains(words[i].Key) && _gazetteer.Surnames.ContainsKey(words[i + 1].Key))
            {
                found[i] = found[i + 1] = PhiCategory.Name;
            }
        }

        // The rest need context: a title or relation before it, or a name next to it.
        bool changed;
        do
        {
            changed = false;
            for (var i = 0; i < words.Count; i++)
            {
                if (!Candidate(i) || (!NameContext(words, i) && !NextToName(text, words, found, i)))
                {
                    continue;
                }

                found[i] = PhiCategory.Name;
                changed = true;
            }
        }
        while (changed);

        return;

        bool Candidate(int i) => found[i] is null && words[i].Capitalized && (IsName(words[i].Key) || Rare(words[i].Key));
    }

    private bool IsName(string key) =>
        _gazetteer.FirstNames.Contains(key) || (_gazetteer.Surnames.TryGetValue(key, out var count) && count >= _options.MinimumSurnameCount);

    // A rarer surname still counts with context.
    private bool Rare(string key) => _gazetteer.Surnames.ContainsKey(key);

    private bool Ambiguous(string key) => _gazetteer.CommonWords.Contains(key) || _gazetteer.NotAlone.Contains(key);

    private static bool NameContext(List<Token> words, int i) =>
        (i >= 1 && Titles.Contains(words[i - 1].Value))
        || (i >= 1 && Relations.Contains(words[i - 1].Value))
        || (i >= 2 && Relations.Contains(words[i - 2].Value) && words[i - 1].Value is "is" or "named");

    private static bool NextToName(string text, List<Token> words, PhiCategory?[] found, int i) =>
        (i >= 1 && found[i - 1] == PhiCategory.Name && Adjacent(text, words[i - 1], words[i]))
        || (i + 1 < words.Count && found[i + 1] == PhiCategory.Name && Adjacent(text, words[i], words[i + 1]));

    private static bool PlaceContext(string text, List<Token> words, int i) =>
        (i >= 1 && PlacePrepositions.Contains(words[i - 1].Value))
        || (i + 1 < words.Count && States.Contains(words[i + 1].Value)
            && Between(text, words[i], words[i + 1]).Trim() is "," or "" && words[i + 1].Value.Any(char.IsUpper));

    // A place's words, in order, separated by spaces (or an abbreviation's period and a space).
    private static bool Matches(string text, List<Token> words, int i, string[] place)
    {
        if (i + place.Length > words.Count)
        {
            return false;
        }

        for (var j = 0; j < place.Length; j++)
        {
            var word = words[i + j];
            if (word.Key != place[j] || (j > 0 && (!Adjacent(text, words[i + j - 1], word) || !(word.Capitalized || word.Value.Length <= 3))))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Adjacent(string text, Token left, Token right) => Gap().IsMatch(Between(text, left, right));

    private static string Between(string text, Token left, Token right) => text[left.End..right.Start];

    // Letters, with inner apostrophes and hyphens ("O'Brien", "Winston-Salem").
    [GeneratedRegex(@"\p{L}+(?:['’\-]\p{L}+)*", RegexOptions.CultureInvariant)]
    private static partial Regex Word();

    // One or a few spaces, after an optional abbreviation's period: never a line break or other punctuation.
    [GeneratedRegex(@"\A\.?[ \t]{1,3}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Gap();

    /// <summary>A word of the text, its lowercase lookup key, and whether it is looked up: written "Hart", or as the options allow.</summary>
    private sealed class Token(int start, int length, string value, GazetteerOptions options)
    {
        public int Start => start;

        public int End => start + length;

        public string Value => value;

        // Without a possessive "'s", which stays out of the span too: "Hart's" is found as "Hart".
        public string Key { get; } = Possessive(value).ToLowerInvariant();

        public bool Capitalized { get; } =
            (char.IsUpper(value[0]) && value.Skip(1).Any(char.IsLower))
            || (options.Capitals && value.Length >= 3 && !value.Any(char.IsLower))
            || (options.Lowercase && !value.Any(char.IsUpper));

        private static string Possessive(string value) =>
            value.EndsWith("'s", StringComparison.Ordinal) || value.EndsWith("’s", StringComparison.Ordinal) ? value[..^2] : value;
    }
}
