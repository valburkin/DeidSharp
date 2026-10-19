namespace DeidSharp.Tests;

public class PhiSpanTests
{
    [Theory]
    [InlineData(-1, 3)]
    [InlineData(3, 3)]
    [InlineData(5, 2)]
    public void Rejects_an_empty_or_negative_range(int start, int end) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PhiSpan(start, end, PhiCategory.Name, 0.5, "test"));

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void Rejects_a_score_outside_zero_to_one(double score) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PhiSpan(0, 1, PhiCategory.Name, score, "test"));

    [Fact]
    public void Rejects_missing_or_blank_sources()
    {
        Assert.Throws<ArgumentException>(() => new PhiSpan(0, 1, PhiCategory.Name, 1, sources: []));
        Assert.Throws<ArgumentException>(() => new PhiSpan(0, 1, PhiCategory.Name, 1, " "));
        Assert.Throws<ArgumentNullException>(() => new PhiSpan(0, 1, PhiCategory.Name, 1, sources: null!));
    }

    [Theory]
    [InlineData(0, 5, 4, 8, true)]
    [InlineData(0, 5, 5, 8, false)]
    [InlineData(0, 10, 2, 3, true)]
    [InlineData(2, 3, 0, 10, true)]
    [InlineData(0, 5, 0, 5, true)]
    [InlineData(6, 9, 0, 5, false)]
    public void Overlap_means_sharing_at_least_one_character(int s1, int e1, int s2, int e2, bool expected)
    {
        var a = new PhiSpan(s1, e1, PhiCategory.Name, 1, "a");
        var b = new PhiSpan(s2, e2, PhiCategory.Date, 1, "b");
        Assert.Equal(expected, a.Overlaps(b));
        Assert.Equal(expected, b.Overlaps(a));
    }

    [Fact]
    public void Length_is_end_minus_start() => Assert.Equal(4, new PhiSpan(3, 7, PhiCategory.Ssn, 1, "x").Length);
}
