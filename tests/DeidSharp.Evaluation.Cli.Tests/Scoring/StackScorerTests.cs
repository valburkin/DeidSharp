using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Scoring;
using DeidSharp.Evaluation.Cli.Tests.TestDoubles;

namespace DeidSharp.Evaluation.Cli.Tests.Scoring;

public class StackScorerTests
{
    private static readonly CorpusDocument Note = new(
        "note 1",
        "Dr. Hart saw Rosa in 2019.",
        [new LabeledSpan(4, 8, "Staff"), new LabeledSpan(13, 17, "Patient"), new LabeledSpan(21, 25, "Year")]);

    private static readonly FictionalCorpus Corpus = new(
        [
            new LabelRule("Staff", PhiCategory.Name, SafeHarbor: true),
            new LabelRule("Patient", PhiCategory.Name, SafeHarbor: true),
            new LabelRule("Year", PhiCategory.Date, SafeHarbor: false),
        ],
        Note);

    private static Task<StackResult> ScoreAsync(EvaluationScope scope, params (string, PhiCategory)[] words) =>
        StackScorer.ScoreAsync(new NamedStack("scripted", new ScriptedDetector(words)), Corpus, [Note], scope, cancellationToken: TestContext.Current.CancellationToken);

    [Fact]
    public async Task Counts_recall_by_corpus_label_and_keeps_the_misses()
    {
        var result = await ScoreAsync(EvaluationScope.SafeHarbor, ("Hart", PhiCategory.Name));

        Assert.Equal(0.5, result.Report.Overall.Recall);
        Assert.Equal([new LabelRecall("Patient", 1, 0), new LabelRecall("Staff", 1, 1)], result.Labels);
        Assert.Equal(new LabeledSpan(13, 17, "Patient"), Assert.Single(result.Misses).Span);
    }

    [Fact]
    public async Task Counts_labels_outside_the_scope_only_when_asked()
    {
        var safeHarbor = await ScoreAsync(EvaluationScope.SafeHarbor, ("Hart", PhiCategory.Name), ("Rosa", PhiCategory.Name));
        var all = await ScoreAsync(EvaluationScope.All, ("Hart", PhiCategory.Name), ("Rosa", PhiCategory.Name));

        Assert.Equal(1.0, safeHarbor.Report.Overall.Recall);
        Assert.Equal(2.0 / 3, all.Report.Overall.Recall, precision: 10);
        Assert.Equal("Year", Assert.Single(all.Misses).Span.Label);
    }

    [Fact]
    public async Task Keeps_the_predictions_that_overlap_no_gold_span_in_scope()
    {
        // "saw" is no PHI at all; "2019" is gold, but outside the Safe Harbor scope.
        var result = await ScoreAsync(EvaluationScope.SafeHarbor, ("Hart", PhiCategory.Name), ("saw", PhiCategory.Name), ("2019", PhiCategory.Date));

        Assert.Equal(["saw", "2019"], result.FalsePositives.Select(f => Note.Text[f.Span.Start..f.Span.End]));
        Assert.Equal(1.0 / 3, result.Report.Overall.Precision, precision: 10);
    }

    [Fact]
    public async Task A_partly_covered_span_counts_as_found_as_the_evaluator_counts_it()
    {
        var result = await ScoreAsync(EvaluationScope.SafeHarbor, ("Har", PhiCategory.Name), ("Ros", PhiCategory.Name));

        Assert.Equal(1.0, result.Report.Overall.Recall);
        Assert.Empty(result.Misses);
        Assert.True(result.Report.CharacterRecall < 1.0);
    }
}
