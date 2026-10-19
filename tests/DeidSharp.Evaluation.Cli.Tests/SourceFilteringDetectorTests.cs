using DeidSharp.Detection;

namespace DeidSharp.Evaluation.Cli.Tests;

public class SourceFilteringDetectorTests
{
    // Design pattern: Test Double (Stub).
    // The pattern: a stand-in for a real dependency in a test. A stub returns canned answers.
    // Here: a detector that returns the spans a test gives it, with the sources it chose.
    private sealed class FixedDetector(params PhiSpan[] spans) : IPhiDetector
    {
        public string Name => "clinical";

        public Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PhiSpan>>(spans);
    }

    [Fact]
    public async Task Drops_only_the_spans_that_dropped_sources_alone_reported()
    {
        var spacy = new PhiSpan(0, 3, PhiCategory.Name, 0.85, "clinical:SpacyRecognizer");
        var model = new PhiSpan(4, 8, PhiCategory.Name, 0.99, "clinical:ClinicalNerRecognizer");
        var both = new PhiSpan(9, 12, PhiCategory.Name, 0.99, ["clinical:ClinicalNerRecognizer", "clinical:SpacyRecognizer"]);
        var filtered = new SourceFilteringDetector(new FixedDetector(spacy, model, both), new HashSet<string>(["clinical:SpacyRecognizer"], StringComparer.Ordinal));

        var spans = await filtered.DetectAsync(new DetectionRequest("Ann met Bob at 9"), TestContext.Current.CancellationToken);

        Assert.Equal([model, both], spans);
        Assert.Equal("clinical", filtered.Name);
    }
}
