using DeidSharp.Detection;
using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Scoring;
using DeidSharp.Gazetteer;

namespace DeidSharp.Evaluation.Cli.Tests;

/// <summary>
/// The deterministic detectors on ASQ-PHI, with every build. The floors are the numbers
/// measured when they were set (docs/evaluation/results.json), rounded down: a change that
/// loses recall fails here. Raise a floor when a change gains recall, never lower one without
/// saying why. Names and places need NER, which is why recall is far from 1 here; the
/// integration tests hold the Presidio stacks to their own floors.
/// </summary>
public class AsqPhiRecallFloorTests
{
    [Fact]
    public async Task The_deterministic_stack_keeps_its_recall_and_precision()
    {
        var corpus = new AsqPhiCorpus(AsqPhiCorpus.BundledPath);
        var stack = new NamedStack("deterministic", new CompositePhiDetector(DeterministicDetectors.Create()));

        var result = await StackScorer.ScoreAsync(stack, corpus, corpus.Read(), EvaluationScope.SafeHarbor, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Report.Overall.Recall >= 0.475, $"recall {result.Report.Overall.Recall:0.0000}");
        Assert.True(result.Report.CharacterRecall >= 0.450, $"character recall {result.Report.CharacterRecall:0.0000}");
        Assert.True(result.Report.Overall.Precision >= 0.997, $"precision {result.Report.Overall.Precision:0.0000}");
    }

    // The deterministic part of the recommended stack, as the evaluation tool runs it.
    [Fact]
    public async Task With_the_gazetteer_it_keeps_its_recall_and_precision()
    {
        var corpus = new AsqPhiCorpus(AsqPhiCorpus.BundledPath);
        var stack = new NamedStack("deterministic", new CompositePhiDetector([.. DeterministicDetectors.Create(), new GazetteerDetector()]));
        var result = await StackScorer.ScoreAsync(stack, corpus, corpus.Read(), EvaluationScope.SafeHarbor, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Report.Overall.Recall >= 0.866, $"recall {result.Report.Overall.Recall:0.0000}");
        Assert.True(result.Report.CharacterRecall >= 0.706, $"character recall {result.Report.CharacterRecall:0.0000}");
        Assert.True(result.Report.Overall.Precision >= 0.976, $"precision {result.Report.Overall.Precision:0.0000}");
    }
}
