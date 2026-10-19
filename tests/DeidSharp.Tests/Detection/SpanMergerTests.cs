using DeidSharp.Detection;

namespace DeidSharp.Tests.Detection;

public class SpanMergerTests
{
    private const string Text = "0123456789abcdefghij";

    private static PhiSpan Span(int start, int end, PhiCategory category = PhiCategory.Name, double score = 0.5, string source = "s") =>
        new(start, end, category, score, source);

    [Fact]
    public void No_spans_gives_no_spans() => Assert.Empty(SpanMerger.Merge(Text, []));

    [Fact]
    public void Disjoint_spans_come_back_sorted_and_unchanged()
    {
        var merged = SpanMerger.Merge(Text, [Span(10, 12), Span(0, 2), Span(5, 7)]);
        Assert.Equal([(0, 2), (5, 7), (10, 12)], merged.Select(s => (s.Start, s.End)));
    }

    [Fact]
    public void Overlapping_spans_become_one_covering_both()
    {
        var merged = Assert.Single(SpanMerger.Merge(Text, [Span(0, 5), Span(3, 9)]));
        Assert.Equal((0, 9), (merged.Start, merged.End));
    }

    [Fact]
    public void A_nested_span_is_absorbed()
    {
        var merged = Assert.Single(SpanMerger.Merge(Text, [Span(0, 10), Span(3, 4)]));
        Assert.Equal((0, 10), (merged.Start, merged.End));
    }

    [Fact]
    public void A_chain_of_overlaps_merges_even_when_its_ends_do_not_overlap()
    {
        var merged = Assert.Single(SpanMerger.Merge(Text, [Span(0, 4), Span(3, 8), Span(7, 12)]));
        Assert.Equal((0, 12), (merged.Start, merged.End));
    }

    [Fact]
    public void Touching_spans_stay_separate() => Assert.Equal(2, SpanMerger.Merge(Text, [Span(0, 4), Span(4, 8)]).Count);

    [Fact]
    public void The_same_span_from_two_detectors_keeps_both_sources()
    {
        var merged = Assert.Single(SpanMerger.Merge(Text, [Span(2, 6, source: "known"), Span(2, 6, source: "presidio")]));
        Assert.Equal(["known", "presidio"], merged.Sources);
    }

    [Fact]
    public void The_most_confident_part_decides_the_category_and_score()
    {
        var merged = Assert.Single(SpanMerger.Merge(Text, [Span(0, 8, PhiCategory.Phone, 0.85), Span(0, 8, PhiCategory.Fax, 0.95)]));
        Assert.Equal(PhiCategory.Fax, merged.Category);
        Assert.Equal(0.95, merged.Score);
    }

    [Fact]
    public void On_equal_scores_the_longer_part_decides_the_category()
    {
        var merged = Assert.Single(SpanMerger.Merge(Text, [Span(0, 3, PhiCategory.Name, 0.8), Span(1, 9, PhiCategory.Organization, 0.8)]));
        Assert.Equal(PhiCategory.Organization, merged.Category);
    }

    [Fact]
    public void On_equal_score_and_length_the_lower_category_wins_so_merging_is_deterministic()
    {
        var a = SpanMerger.Merge(Text, [Span(0, 4, PhiCategory.Date, 0.8), Span(0, 4, PhiCategory.Name, 0.8)]);
        var b = SpanMerger.Merge(Text, [Span(0, 4, PhiCategory.Name, 0.8), Span(0, 4, PhiCategory.Date, 0.8)]);
        Assert.Equal(PhiCategory.Name, Assert.Single(a).Category);
        Assert.Equal(PhiCategory.Name, Assert.Single(b).Category);
    }

    [Fact]
    public void A_span_starting_inside_a_surrogate_pair_widens_to_the_whole_character()
    {
        const string text = "a\U0001F600b";
        var merged = Assert.Single(SpanMerger.Merge(text, [Span(2, 4)]));
        Assert.Equal((1, 4), (merged.Start, merged.End));
    }

    [Fact]
    public void A_span_ending_inside_a_surrogate_pair_widens_to_the_whole_character()
    {
        const string text = "a\U0001F600b";
        var merged = Assert.Single(SpanMerger.Merge(text, [Span(0, 2)]));
        Assert.Equal((0, 3), (merged.Start, merged.End));
    }

    [Fact]
    public void A_span_widens_over_the_combining_accents_of_its_last_letter()
    {
        const string text = "José came";
        var merged = Assert.Single(SpanMerger.Merge(text, [Span(0, 4)]));
        Assert.Equal((0, 5), (merged.Start, merged.End));
    }

    [Fact]
    public void A_span_outside_the_text_is_rejected() =>
        Assert.Throws<PhiDetectionException>(() => SpanMerger.Merge("short", [Span(2, 9)]));

    [Fact]
    public void EnsureWithin_rejects_null_spans_and_spans_past_the_end()
    {
        Assert.Throws<PhiDetectionException>(() => SpanMerger.EnsureWithin("abc", [null], "d"));
        Assert.Throws<PhiDetectionException>(() => SpanMerger.EnsureWithin("abc", [Span(0, 4)], "d"));
        SpanMerger.EnsureWithin("abc", [Span(0, 3)], "d");
    }
}
