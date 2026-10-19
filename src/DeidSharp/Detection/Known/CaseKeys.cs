using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace DeidSharp.Detection.Known;

/// <summary>
/// Maps characters onto one representative per case class, for comparing anchors the way
/// a case-insensitive, culture-invariant regular expression compares letters: "K", "k" and
/// the Kelvin sign all get the same key. The classes are read from the regex engine itself,
/// never from <see cref="char.ToUpperInvariant"/>, whose tables change with the host's
/// globalization mode and ICU version: a key that disagreed with the search would skip a
/// search that matches, and leave an identifier in place.
///
/// Only the characters it is built from (the anchors') and the characters that match them
/// get a key other than themselves. That is enough: a text character that no anchor
/// character matches can never be part of an anchor match.
/// </summary>
internal sealed class CaseKeys
{
    private const RegexOptions SearchOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // Every UTF-16 code unit once, in order, so a match's index is the character it matched.
    private static readonly Lazy<string> EveryCharacter = new(() =>
        string.Create(char.MaxValue + 1, 0, static (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = (char)i;
            }
        }));

    // Design pattern: Memoization (a cache keyed by the input).
    // The pattern: the result of an expensive computation is stored against its input,
    // so the same input never pays for it twice.
    // Here: a character's case class costs one scan of every character; it never changes
    // while the process runs, so each is computed once for every set.
    private static readonly ConcurrentDictionary<char, char[]> Classes = new();

    private readonly char[] _ascii;
    private readonly FrozenDictionary<char, char> _other;

    /// <param name="characters">The characters of every anchor.</param>
    public CaseKeys(IEnumerable<char> characters)
    {
        ArgumentNullException.ThrowIfNull(characters);

        // Union-find over the classes, so the keys stay consistent even where classes overlap.
        var parent = new Dictionary<char, char>();
        foreach (var character in characters.Distinct())
        {
            foreach (var member in ClassOf(character))
            {
                Union(parent, character, member);
            }
        }

        _ascii = [.. Enumerable.Range(0, 128).Select(c => (char)c)];
        var other = new Dictionary<char, char>();
        foreach (var character in parent.Keys)
        {
            var key = Find(parent, character);
            if (character < 128)
            {
                _ascii[character] = key;
            }
            else if (key != character)
            {
                other[character] = key;
            }
        }

        _other = other.ToFrozenDictionary();
    }

    public char Of(char c) => c < 128 ? _ascii[c] : _other.GetValueOrDefault(c, c);

    /// <summary>The text with every character replaced by its key; the length is kept.</summary>
    public string Apply(string text) =>
        string.Create(text.Length, (Keys: this, Text: text), static (span, state) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = state.Keys.Of(state.Text[i]);
            }
        });

    // Every character a one-character case-insensitive search for c matches, c included.
    private static char[] ClassOf(char c) =>
        Classes.GetOrAdd(c, static c =>
            [.. new Regex(Regex.Escape(c.ToString()), SearchOptions).Matches(EveryCharacter.Value).Select(m => m.Value[0])]);

    private static char Find(Dictionary<char, char> parent, char c)
    {
        while (parent.TryGetValue(c, out var next) && next != c)
        {
            c = next;
        }

        return c;
    }

    // The smaller character becomes the root, so a class's key does not depend on the
    // order its members were seen in.
    private static void Union(Dictionary<char, char> parent, char a, char b)
    {
        parent.TryAdd(a, a);
        parent.TryAdd(b, b);
        var rootA = Find(parent, a);
        var rootB = Find(parent, b);
        if (rootA == rootB)
        {
            return;
        }

        var (low, high) = rootA < rootB ? (rootA, rootB) : (rootB, rootA);
        parent[high] = low;
    }
}
