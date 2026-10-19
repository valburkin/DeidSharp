using System.Buffers;

namespace DeidSharp.Detection.Known;

/// <summary>
/// Says which searches of an identifier set can match a text, before any of them runs.
/// Every search lists anchors (<see cref="KnownValuePattern"/>): literals of which each of
/// its matches contains at least one, such as a word of a name, the digits of a phone
/// number or the year of a date. A text that holds none of a search's anchors is not
/// searched. One pass over the text finds every anchor of every search at once, so a set
/// of thousands of identifiers (every patient of a large bundle) costs a scan per text
/// plus the few searches whose anchors are there, instead of thousands of searches per text.
///
/// The filter only ever skips a search that would have found nothing: anchors are looked
/// for in views of the text at least as loose as the searches. Case is compared with
/// <see cref="CaseKeys"/>, read from the regex engine, and the compact view drops the
/// separators that a search allows between the characters of a phone number or identifier.
/// </summary>
internal sealed class AnchorIndex
{
    private readonly int[] _unanchored;
    private readonly CaseKeys _caseKeys;
    private readonly LiteralView? _folded;
    private readonly LiteralView? _compact;
    private readonly Dictionary<string, int[]>.AlternateLookup<ReadOnlySpan<char>>? _digitRuns;

    public AnchorIndex(IReadOnlyList<KnownValuePattern> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);

        // Anchors are alternatives: one empty anchor would match anywhere, so its search always runs.
        _unanchored = [.. Enumerable.Range(0, patterns.Count).Where(i => patterns[i].Anchors is [] || patterns[i].Anchors.Any(string.IsNullOrEmpty))];
        _caseKeys = new CaseKeys(patterns.Where(p => p.View != AnchorView.DigitRun).SelectMany(p => p.Anchors).SelectMany(a => a));
        _folded = LiteralView.Create(Anchors(patterns, AnchorView.Folded, _caseKeys.Apply));
        _compact = LiteralView.Create(Anchors(patterns, AnchorView.Compact, _caseKeys.Apply));
        var digitRuns = Anchors(patterns, AnchorView.DigitRun, run => run);
        _digitRuns = digitRuns.Count == 0 ? null : digitRuns.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    /// <summary>
    /// The positions of the searches that can match <paramref name="folded"/>, in ascending
    /// order. Its cost follows the text and the anchors it holds, not the size of the set.
    /// </summary>
    /// <param name="folded">The text, folded with <see cref="Text.TextFolding.Fold"/>.</param>
    public int[] Candidates(string folded)
    {
        ArgumentNullException.ThrowIfNull(folded);
        var candidates = new HashSet<int>(_unanchored);
        _folded?.Mark(_caseKeys.Apply(folded), candidates);

        // Separators are dropped before keys are applied, so only the text's own separators go.
        _compact?.Mark(_caseKeys.Apply(Compact(folded)), candidates);
        if (_digitRuns is { } digitRuns)
        {
            MarkDigitRuns(folded, digitRuns, candidates);
        }

        return [.. candidates.Order()];
    }

    // Anchor (in its comparable form) → the searches it belongs to.
    private static Dictionary<string, int[]> Anchors(
        IReadOnlyList<KnownValuePattern> patterns, AnchorView view, Func<string, string> comparable) =>
        Enumerable.Range(0, patterns.Count)
            .Where(i => patterns[i].View == view)
            .SelectMany(i => patterns[i].Anchors.Where(a => a.Length > 0).Select(a => (Anchor: comparable(a), Search: i)))
            .GroupBy(x => x.Anchor, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Search).Distinct().ToArray(), StringComparer.Ordinal);

    private static string Compact(string text) =>
        string.Create(text.Count(IsKept), text, static (span, source) =>
        {
            var position = 0;
            foreach (var c in source.Where(IsKept))
            {
                span[position++] = c;
            }
        });

    private static bool IsKept(char c) => !KnownValuePatterns.IsDigitGapSeparator(c);

    // Every maximal run of ASCII digits is looked up as a whole.
    private static void MarkDigitRuns(
        string text, Dictionary<string, int[]>.AlternateLookup<ReadOnlySpan<char>> runs, HashSet<int> candidates)
    {
        var position = 0;
        while (position < text.Length)
        {
            var start = text.AsSpan(position).IndexOfAnyInRange('0', '9');
            if (start < 0)
            {
                return;
            }

            start += position;
            var length = text.AsSpan(start).IndexOfAnyExceptInRange('0', '9');
            length = length < 0 ? text.Length - start : length;
            if (runs.TryGetValue(text.AsSpan(start, length), out var searches))
            {
                candidates.UnionWith(searches);
            }

            position = start + length;
        }
    }

    // The anchors looked for anywhere in one view of the text, found in a single pass.
    private sealed class LiteralView
    {
        private readonly SearchValues<string> _anchors;
        private readonly Dictionary<string, int[]>.AlternateLookup<ReadOnlySpan<char>> _searches;
        private readonly int[] _lengths;

        private LiteralView(Dictionary<string, int[]> searches)
        {
            _anchors = SearchValues.Create([.. searches.Keys], StringComparison.Ordinal);
            _searches = searches.GetAlternateLookup<ReadOnlySpan<char>>();
            _lengths = [.. searches.Keys.Select(k => k.Length).Distinct().Order()];
        }

        public static LiteralView? Create(Dictionary<string, int[]> searches) => searches.Count == 0 ? null : new LiteralView(searches);

        // Finds the leftmost anchor, checks which anchors start there, and moves on by one
        // character, so overlapping anchors are all found.
        public void Mark(string text, HashSet<int> candidates)
        {
            var position = 0;
            while (position < text.Length)
            {
                var found = text.AsSpan(position).IndexOfAny(_anchors);
                if (found < 0)
                {
                    return;
                }

                position += found;
                foreach (var length in _lengths)
                {
                    if (position + length > text.Length)
                    {
                        break;
                    }

                    if (_searches.TryGetValue(text.AsSpan(position, length), out var searches))
                    {
                        candidates.UnionWith(searches);
                    }
                }

                position++;
            }
        }
    }
}
