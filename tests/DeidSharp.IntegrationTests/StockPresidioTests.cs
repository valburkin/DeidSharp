using DeidSharp.Detection;
using DeidSharp.Detection.Known;
using DeidSharp.Detection.Patterns;
using DeidSharp.Text;

namespace DeidSharp.IntegrationTests;

public sealed class StockPresidioTests(StockPresidioContainer analyzer) : IClassFixture<StockPresidioContainer>
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reports_healthy() => Assert.True(await analyzer.Detector().IsHealthyAsync(Ct));

    [Fact]
    public async Task Finds_a_name_a_phone_number_and_a_date()
    {
        const string text = "John Smith called 617-555-0123 on March 5, 2024.";
        var spans = await analyzer.Detector().DetectAsync(new DetectionRequest(text), Ct);

        Assert.Contains(spans, s => s.Category == PhiCategory.Name && text[s.Start..s.End] == "John Smith");
        Assert.Contains(spans, s => s.Category == PhiCategory.Phone && text[s.Start..s.End] == "617-555-0123");
        Assert.Contains(spans, s => s.Category == PhiCategory.Date && text[s.Start..s.End].Contains("March 5", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Offsets_after_an_emoji_point_at_the_right_characters()
    {
        const string text = "\U0001F600 John Smith called back";
        var result = await new TextDeidentifier(analyzer.Detector(), new TagReplacement()).DeidentifyAsync(text, cancellationToken: Ct);

        Assert.Equal("\U0001F600 [NAME] called back", result.Text);
    }

    [Fact]
    public async Task Stacked_with_the_deterministic_detectors_it_cleans_a_note_end_to_end()
    {
        const string note = "Pt Gertrude Okafor, MRN: 88231907, seen 2024-03-05 by Dr. Bjornsson. Neighbor Tom Riley called (413) 555-0177.";
        var stack = new CompositePhiDetector([analyzer.Detector(), new KnownIdentifierDetector(), new PatternPhiDetector()]);

        var result = await new TextDeidentifier(stack, new TagReplacement()).DeidentifyAsync(note, cancellationToken: Ct);

        foreach (var secret in new[] { "Gertrude", "Okafor", "88231907", "2024-03-05", "Bjornsson", "Riley", "555-0177" })
        {
            Assert.DoesNotContain(secret, result.Text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Long_notes_are_handled()
    {
        var note = string.Concat(Enumerable.Repeat("Patient John Smith reports chest pain; BP 140/90. ", 400));
        var spans = await analyzer.Detector().DetectAsync(new DetectionRequest(note), Ct);
        Assert.True(spans.Count(s => s.Category == PhiCategory.Name) >= 300);
    }
}
