using DeidSharp.Detection;
using DeidSharp.Tests.TestDoubles;

namespace DeidSharp.Tests.Detection;

public class CategoryFilterTests
{
    [Fact]
    public async Task Keeps_only_the_spans_of_the_given_categories()
    {
        const string text = "Seen by Hart at Lakeview Rehab on 3/14.";
        var ner = new StubDetector(
            "ner",
            new PhiSpan(8, 12, PhiCategory.Name, 0.9, "ner"),
            new PhiSpan(16, 30, PhiCategory.Organization, 0.9, "ner"),
            new PhiSpan(34, 38, PhiCategory.Date, 0.9, "ner"));

        var spans = await new CategoryFilter(ner, [PhiCategory.Location, PhiCategory.Organization])
            .DetectAsync(new DetectionRequest(text), TestContext.Current.CancellationToken);

        Assert.Equal(["Lakeview Rehab"], spans.Select(s => text[s.Start..s.End]));
    }

    [Fact]
    public async Task Fails_closed_when_the_detector_fails_and_keeps_its_name_unless_given_one()
    {
        var filter = new CategoryFilter(new FailingDetector("ner"), [PhiCategory.Location]);

        await Assert.ThrowsAsync<PhiDetectionException>(() => filter.DetectAsync(new DetectionRequest("Lakeview"), TestContext.Current.CancellationToken));
        Assert.Equal("ner", filter.Name);
        Assert.Equal("ner places", new CategoryFilter(new FailingDetector("ner"), [PhiCategory.Location], "ner places").Name);
    }

    [Fact]
    public void Refuses_an_empty_category_list_or_a_blank_name()
    {
        Assert.Throws<ArgumentException>(() => new CategoryFilter(new FailingDetector("ner"), []));
        Assert.Throws<ArgumentException>(() => new CategoryFilter(new FailingDetector("ner"), [PhiCategory.Location], " "));
    }
}
