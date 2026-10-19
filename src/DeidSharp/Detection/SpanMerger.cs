using DeidSharp.Text;

namespace DeidSharp.Detection;

/// <summary>
/// Turns the raw, possibly overlapping spans from one or more detectors into the
/// disjoint, ordered spans a replacement can be applied to.
/// </summary>
public static class SpanMerger
{
    /// <summary>
    /// Snaps each span to whole characters, then merges every group of overlapping spans
    /// into one span covering the group. The merged span keeps the category of its most
    /// confident part (then the longest, then the lowest category), the highest score and
    /// every source (in ordinal order, so the result does not depend on detector order).
    /// Touching spans (one ends where the next starts) stay separate.
    /// </summary>
    public static IReadOnlyList<PhiSpan> Merge(string text, IEnumerable<PhiSpan> spans)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(spans);

        var ordered = spans
            .Select(s => SnapToCharacters(text, s))
            .OrderBy(s => s.Start)
            .ThenByDescending(s => s.End)
            .ToList();

        var merged = new List<PhiSpan>();
        var group = new List<PhiSpan>();
        var groupEnd = -1;
        foreach (var span in ordered)
        {
            if (group.Count > 0 && span.Start >= groupEnd)
            {
                merged.Add(Combine(group));
                group.Clear();
            }

            group.Add(span);
            groupEnd = Math.Max(groupEnd, span.End);
        }

        if (group.Count > 0)
        {
            merged.Add(Combine(group));
        }

        return merged;
    }

    /// <summary>Throws if any span is null or points outside the text it was found in.</summary>
    public static void EnsureWithin(string text, IEnumerable<PhiSpan?> spans, string detectorName)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(spans);
        foreach (var span in spans)
        {
            if (span is null)
            {
                throw new PhiDetectionException($"Detector '{detectorName}' returned a null span.");
            }

            if (span.End > text.Length)
            {
                throw new PhiDetectionException(
                    $"Detector '{detectorName}' returned a span [{span.Start}, {span.End}) outside text of length {text.Length}.");
            }
        }
    }

    /// <summary>
    /// Widens a span so it never cuts a character in half: not between the two halves of
    /// a surrogate pair (an emoji, a rare CJK character), and not before the combining
    /// marks (accents) that belong to its last letter. Replacing half a character would
    /// leave a broken or still-readable remnant.
    /// </summary>
    private static PhiSpan SnapToCharacters(string text, PhiSpan span)
    {
        if (span.End > text.Length)
        {
            throw new PhiDetectionException($"Span [{span.Start}, {span.End}) is outside text of length {text.Length}.");
        }

        var start = span.Start;
        var end = span.End;

        if (start > 0 && char.IsLowSurrogate(text[start]) && char.IsHighSurrogate(text[start - 1]))
        {
            start--;
        }

        if (end < text.Length && char.IsLowSurrogate(text[end]) && char.IsHighSurrogate(text[end - 1]))
        {
            end++;
        }

        while (end < text.Length && TextFolding.IsCombiningMark(text[end]))
        {
            end++;
        }

        return start == span.Start && end == span.End
            ? span
            : new PhiSpan(start, end, span.Category, span.Score, span.Sources);
    }

    private static PhiSpan Combine(List<PhiSpan> group)
    {
        if (group.Count == 1)
        {
            return group[0];
        }

        var lead = group
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.Length)
            .ThenBy(s => s.Category)
            .First();

        var sources = group.SelectMany(s => s.Sources).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        return new PhiSpan(group.Min(s => s.Start), group.Max(s => s.End), lead.Category, lead.Score, sources);
    }
}
