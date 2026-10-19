namespace DeidSharp;

/// <summary>
/// A stretch of text a detector believes is PHI: <c>[Start, End)</c> in UTF-16 code units,
/// the category, a confidence in [0, 1], and which detector(s) found it. A span never
/// carries the text it covers, so spans can be logged without leaking what they point at.
/// </summary>
public sealed record PhiSpan
{
    public PhiSpan(int start, int end, PhiCategory category, double score, string source)
        : this(start, end, category, score, [source])
    {
    }

    public PhiSpan(int start, int end, PhiCategory category, double score, IReadOnlyList<string> sources)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        if (end <= start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), end, "A span must end after it starts.");
        }

        if (double.IsNaN(score) || score is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(score), score, "A score must be between 0 and 1.");
        }

        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count == 0 || sources.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("A span needs at least one named source.", nameof(sources));
        }

        Start = start;
        End = end;
        Category = category;
        Score = score;
        Sources = sources;
    }

    public int Start { get; }

    /// <summary>The first code unit after the span (exclusive).</summary>
    public int End { get; }

    public PhiCategory Category { get; }

    public double Score { get; }

    public IReadOnlyList<string> Sources { get; }

    public int Length => End - Start;

    public bool Overlaps(PhiSpan other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Start < other.End && other.Start < End;
    }

    // Value equality, including the sources' contents (a record would compare the list by reference).
    public bool Equals(PhiSpan? other) =>
        other is not null && Start == other.Start && End == other.End && Category == other.Category
        && Score.Equals(other.Score) && Sources.SequenceEqual(other.Sources, StringComparer.Ordinal);

    public override int GetHashCode() => HashCode.Combine(Start, End, Category, Score, Sources.Count);

    public override string ToString() => $"{Category} [{Start}, {End}) {Score:0.00} by {string.Join("+", Sources)}";
}
