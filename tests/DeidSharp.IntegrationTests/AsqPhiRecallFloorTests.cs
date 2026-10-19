using DeidSharp.Detection;
using DeidSharp.Evaluation.Cli;
using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Scoring;
using DeidSharp.Presidio;

namespace DeidSharp.IntegrationTests;

/// <summary>
/// The NER stacks on ASQ-PHI. The floors are the numbers measured when they were set
/// (docs/evaluation/results.json), rounded down: a new Presidio image, sidecar package or
/// model that loses recall fails here. Raise a floor when a change gains recall, never
/// lower one without saying why.
/// </summary>
internal static class AsqPhiRecallFloor
{
    public static async Task<StackResult> ScoreAsync(PresidioDetector analyzer)
    {
        var corpus = new AsqPhiCorpus(AsqPhiCorpus.BundledPath);
        var stack = new NamedStack($"deterministic + {analyzer.Name}", new CompositePhiDetector([analyzer, .. DeterministicDetectors.Create()]));
        return await StackScorer.ScoreAsync(stack, corpus, corpus.Read(), EvaluationScope.SafeHarbor, cancellationToken: TestContext.Current.CancellationToken);
    }
}

public sealed class StockPresidioAsqPhiTests(StockPresidioContainer analyzer) : IClassFixture<StockPresidioContainer>
{
    [Fact]
    public async Task Stacked_with_the_deterministic_detectors_it_keeps_its_recall()
    {
        var result = await AsqPhiRecallFloor.ScoreAsync(analyzer.Detector());

        Assert.True(result.Report.Overall.Recall >= 0.80, $"recall {result.Report.Overall.Recall:0.0000}");
        Assert.True(result.Report.CharacterRecall >= 0.69, $"character recall {result.Report.CharacterRecall:0.0000}");
    }
}

public sealed class ClinicalAsqPhiTests(ClinicalAnalyzerContainer analyzer) : IClassFixture<ClinicalAnalyzerContainer>
{
    [Fact]
    public async Task Stacked_with_the_deterministic_detectors_it_keeps_its_recall()
    {
        var result = await AsqPhiRecallFloor.ScoreAsync(analyzer.Detector("clinical"));

        // Measured at 0.9973 and 0.9596. Leaving out spaCy's general NER and the URL and license
        // recognizers cost 7 of 2,976 spans here, and 2 points of recall on PhysioNet's tuning
        // half, for precision from 64% to 76% here and from 5% to 55% there (docs/evaluation).
        // One of the 8 misses is a labeling error in the corpus.
        Assert.True(result.Report.Overall.Recall >= 0.997, $"recall {result.Report.Overall.Recall:0.0000}");
        Assert.True(result.Report.CharacterRecall >= 0.959, $"character recall {result.Report.CharacterRecall:0.0000}");
    }
}
