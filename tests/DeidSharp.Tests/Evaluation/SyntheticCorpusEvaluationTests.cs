using DeidSharp.Detection;
using DeidSharp.Evaluation;

namespace DeidSharp.Tests.Evaluation;

/// <summary>
/// The deterministic stack (known identifiers + patterns) measured on the annotated
/// corpus. This is a regression floor, not a claim about real notes: the corpus was
/// written with this stack in mind. Real recall must be measured on real annotated data.
/// </summary>
public class SyntheticCorpusEvaluationTests
{
    private static readonly CompositePhiDetector Stack = new(DeterministicDetectors.Create());

    private static async Task<EvaluationReport> EvaluateAsync()
    {
        var cases = new List<(AnnotatedText, IReadOnlyList<PhiSpan>)>();
        foreach (var (document, known) in SyntheticCorpus.Notes)
        {
            cases.Add((document, await Stack.DetectAsync(new DetectionRequest(document.Text, known), TestContext.Current.CancellationToken)));
        }

        return DetectionEvaluator.Evaluate(cases);
    }

    [Fact]
    public async Task Finds_every_annotated_identifier()
    {
        var report = await EvaluateAsync();

        Assert.Equal(1.0, report.Overall.Recall);
        Assert.Equal(1.0, report.CharacterRecall);
    }

    [Fact]
    public async Task Rarely_flags_clinical_content()
    {
        var report = await EvaluateAsync();

        Assert.True(report.Overall.Precision >= 0.95, $"precision {report.Overall.Precision:0.000}");
    }

    [Fact]
    public void The_corpus_parser_strips_marks_and_records_spans()
    {
        var parsed = SyntheticCorpus.Parse("Hi {{Name|Ann}}, see {{Date|May 5}}.");
        Assert.Equal("Hi Ann, see May 5.", parsed.Text);
        Assert.Equal([new GoldSpan(3, 6, PhiCategory.Name), new GoldSpan(12, 17, PhiCategory.Date)], parsed.Gold);
    }
}
