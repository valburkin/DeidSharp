using DeidSharp.Detection;
using DeidSharp.Evaluation.Cli.Tests.TestDoubles;

namespace DeidSharp.Evaluation.Cli.Tests;

public class RepeatedNameDetectorTests
{
    private static Task<IReadOnlyList<PhiSpan>> DetectAsync(string text, params (string Word, PhiCategory Category)[] found) =>
        new RepeatedNameDetector(new ScriptedDetector(found)).DetectAsync(new DetectionRequest(text), TestContext.Current.CancellationToken);

    private static string Covered(string text, IEnumerable<PhiSpan> spans) =>
        string.Join("|", spans.OrderBy(s => s.Start).Select(s => text[s.Start..s.End]));

    [Fact]
    public async Task Finds_the_other_mentions_of_a_name_by_any_part()
    {
        const string text = "Seen by Dr. Ellen Hart today. Paged hart at noon; Ellen agreed.";

        var spans = await DetectAsync(text, ("Ellen Hart", PhiCategory.Name));

        Assert.Equal("Ellen Hart|hart|Ellen", Covered(text, spans));
        Assert.All(spans.Skip(1), s => Assert.Equal([RepeatedNameDetector.Source], s.Sources));
    }

    [Fact]
    public async Task Leaves_other_categories_and_partial_words_alone()
    {
        const string text = "Moved from Hartford to Boston. Hartley visited.";

        var spans = await DetectAsync(text, ("Boston", PhiCategory.Location), ("Hartley", PhiCategory.Name));

        Assert.Equal("Boston|Hartley", Covered(text, spans));
    }

    [Fact]
    public async Task Takes_an_ambiguous_name_part_alone_only_after_a_title()
    {
        const string text = "Called Anh Do about it. Do not disturb. Mr. Do called back.";

        var spans = await DetectAsync(text, ("Anh Do", PhiCategory.Name));

        Assert.Equal("Anh Do|Do", Covered(text, spans));
        Assert.Equal(text.LastIndexOf("Do", StringComparison.Ordinal), spans[1].Start);
    }

    [Fact]
    public async Task Returns_the_stack_s_spans_when_it_found_no_name()
    {
        const string text = "Moved to Boston.";

        var spans = await DetectAsync(text, ("Boston", PhiCategory.Location));

        Assert.Equal("Boston", Covered(text, spans));
        Assert.Equal(["scripted"], Assert.Single(spans).Sources);
    }

    [Fact]
    public async Task Fails_closed_when_the_stack_fails()
    {
        var detector = new RepeatedNameDetector(new FailingDetector("ner"));

        await Assert.ThrowsAsync<PhiDetectionException>(() => detector.DetectAsync(new DetectionRequest("Seen by Dr. Hart."), TestContext.Current.CancellationToken));
        Assert.Equal("ner", detector.Name);
    }
}
