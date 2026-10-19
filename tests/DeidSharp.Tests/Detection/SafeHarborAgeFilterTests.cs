using DeidSharp.Detection;
using DeidSharp.Tests.TestDoubles;

namespace DeidSharp.Tests.Detection;

public class SafeHarborAgeFilterTests
{
    [Theory]
    [InlineData("75 yo", true)]
    [InlineData("75", true)]
    [InlineData("89-year-old", true)]
    [InlineData("90", false)]
    [InlineData("94 yo", false)]
    [InlineData("75-92", false)]
    [InlineData("elderly", false)]
    [InlineData(null, false)]
    public void An_age_is_under_90_only_with_one_number_below_90(string? text, bool under90) =>
        Assert.Equal(under90, SafeHarborAges.IsUnder90(text));

    [Fact]
    public async Task Leaves_out_ages_under_90_and_keeps_every_other_span()
    {
        const string text = "75 yo and 94 yo, seen by Hart.";
        var stack = new StubDetector(
            "ner",
            new PhiSpan(0, 5, PhiCategory.Age, 0.9, "ner"),
            new PhiSpan(10, 15, PhiCategory.Age, 0.9, "ner"),
            new PhiSpan(25, 29, PhiCategory.Name, 0.9, "ner"),
            new PhiSpan(0, 2, PhiCategory.OtherIdentifier, 0.9, "ner"));

        var spans = await new SafeHarborAgeFilter(stack).DetectAsync(new DetectionRequest(text), TestContext.Current.CancellationToken);

        Assert.Equal(["94 yo", "Hart", "75"], spans.Select(s => text[s.Start..s.End]));
    }

    [Fact]
    public async Task Fails_closed_when_the_stack_fails()
    {
        var filter = new SafeHarborAgeFilter(new FailingDetector("ner"));

        await Assert.ThrowsAsync<PhiDetectionException>(() => filter.DetectAsync(new DetectionRequest("75 yo"), TestContext.Current.CancellationToken));
        Assert.Equal("ner", filter.Name);
    }
}
