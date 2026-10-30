namespace DeidSharp.Evaluation.Cli.Reporting;

/// <summary>
/// A part of a hand-written Markdown page between two marker comments, which the tool rewrites
/// and leaves the rest of the page alone.
/// </summary>
/// <param name="Begin">The comment that opens the section.</param>
/// <param name="End">The comment that closes it.</param>
/// <param name="Lost">The error when a page should have the markers and doesn't.</param>
internal sealed record MarkedSection(string Begin, string End, string Lost)
{
    /// <summary>Whether the page has the section; a page with only one of its markers, or with them out of order, is refused.</summary>
    public bool IsIn(string page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var hasBegin = page.Contains(Begin, StringComparison.Ordinal);
        var hasEnd = page.Contains(End, StringComparison.Ordinal);
        if (!hasBegin && !hasEnd)
        {
            return false;
        }

        // A marker lost in an edit would otherwise leave the section unrendered, and unchecked, for good.
        _ = Bounds(page);
        return true;
    }

    /// <summary>The page with the section, markers included, replaced by <paramref name="section"/>.</summary>
    public string Replace(string page, string section)
    {
        ArgumentNullException.ThrowIfNull(page);
        var (begin, end) = Bounds(page);
        return string.Concat(page.AsSpan(0, begin), section, page.AsSpan(end));
    }

    /// <summary>The page's current section, markers included.</summary>
    public string Current(string page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var (begin, end) = Bounds(page);
        return page[begin..end];
    }

    private (int Begin, int End) Bounds(string page)
    {
        var begin = page.IndexOf(Begin, StringComparison.Ordinal);
        var end = begin < 0 ? -1 : page.IndexOf(End, begin, StringComparison.Ordinal);
        return end > begin ? (begin, end + End.Length) : throw new UsageException(Lost);
    }
}
