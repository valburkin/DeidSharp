using DeidSharp.Detection;
using DeidSharp.Gazetteer;

namespace DeidSharp.Tests.Gazetteer;

public class GazetteerDetectorTests
{
    // A small gazetteer, so each rule is tested on its own. Every name and text here is fictional.
    private static readonly DeidSharp.Gazetteer.Gazetteer Lists = new(file => file switch
    {
        "surnames.txt" => ["hart\t50000", "brown\t500000", "foley\t51865", "quist\t150", "oduya\t20000"],
        "first-names.txt" => ["ellen", "mark", "will", "hope", "rosa"],
        "common-words.txt" => ["will", "mark", "brown", "hope", "fall", "river"],
        "not-alone.txt" => ["foley"],
        "places.txt" => ["Fall River", "Easton", "Hope", "St. Louis"],
        _ => [],
    });

    private static string Found(string text, GazetteerOptions? options = null)
    {
        var spans = new GazetteerDetector(Lists, options).Detect(new DetectionRequest(text), TestContext.Current.CancellationToken);
        return string.Join("|", spans.Select(s => $"{text[s.Start..s.End]}:{s.Category}"));
    }

    [Theory]
    [InlineData("Seen by Ellen Hart today.", "Ellen Hart:Name")]
    [InlineData("Called Oduya back.", "Oduya:Name")]
    [InlineData("Hart's daughter called.", "Hart:Name")]
    public void Finds_a_name_that_is_no_common_word_on_its_own(string text, string expected) =>
        Assert.Equal(expected, Found(text));

    [Theory]
    [InlineData("Will continue to monitor. Mark the site.", "")]
    [InlineData("Seen by Dr. Will.", "Will:Name")]
    [InlineData("Her daughter Hope is visiting.", "Hope:Name")]
    [InlineData("His daughter is Hope.", "Hope:Name")]
    [InlineData("Mark Brown visited.", "Mark Brown:Name")]
    [InlineData("Rosa Brown visited.", "Rosa Brown:Name")]
    public void Takes_a_name_that_is_a_common_word_only_with_context(string text, string expected) =>
        Assert.Equal(expected, Found(text));

    [Theory]
    [InlineData("Foley catheter draining well.", "")]
    [InlineData("Dr. Foley aware.", "Foley:Name")]
    public void Takes_a_clinical_term_only_with_context(string text, string expected) =>
        Assert.Equal(expected, Found(text));

    [Theory]
    [InlineData("Quist noted.", "")]
    [InlineData("Paged Dr. Quist.", "Quist:Name")]
    public void Takes_a_rare_surname_only_with_context(string text, string expected) =>
        Assert.Equal(expected, Found(text));

    [Fact]
    public void Looks_up_capitals_by_default_and_lowercase_only_when_asked()
    {
        Assert.Equal("ELLEN HART:Name", Found("SEEN BY ELLEN HART"));
        Assert.Equal("", Found("seen by ellen hart"));
        Assert.Equal("ellen hart:Name", Found("seen by ellen hart", new GazetteerOptions { Lowercase = true }));
        Assert.Equal("", Found("SEEN BY ELLEN HART", new GazetteerOptions { Capitals = false }));
    }

    [Theory]
    [InlineData("Transferred from Fall River today.", "Fall River:Location")]
    [InlineData("Lives in St. Louis.", "St. Louis:Location")]
    [InlineData("Moved to Easton last year.", "Easton:Location")]
    [InlineData("Lives in Hope with her son.", "Hope:Location")]
    [InlineData("From Hope, AR.", "Hope:Location")]
    [InlineData("Hope is high. Fall\nRiver.", "")]
    public void Finds_places_and_takes_a_common_word_place_only_with_context(string text, string expected) =>
        Assert.Equal(expected, Found(text));

    [Fact]
    public void Joins_neighbors_only_on_one_line()
    {
        Assert.Equal("Ellen:Name|Hart:Name", Found("Ellen\nHart"));
    }

    [Fact]
    public void Reports_its_source_and_refuses_a_negative_count()
    {
        var span = Assert.Single(new GazetteerDetector(Lists).Detect(new DetectionRequest("Ellen"), TestContext.Current.CancellationToken));

        Assert.Equal(["gazetteer:name"], span.Sources);
        Assert.Throws<ArgumentOutOfRangeException>(() => new GazetteerDetector(Lists, new GazetteerOptions { MinimumSurnameCount = -1 }));
    }

    [Fact]
    public void Ships_its_lists()
    {
        var spans = new GazetteerDetector().Detect(new DetectionRequest("Visited by Nguyen from Boston."), TestContext.Current.CancellationToken);

        Assert.Contains(spans, s => s.Category == PhiCategory.Location);
        Assert.Contains(spans, s => s.Category == PhiCategory.Name);
    }
}
