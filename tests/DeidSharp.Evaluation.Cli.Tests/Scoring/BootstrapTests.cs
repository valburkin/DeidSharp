using DeidSharp.Evaluation.Cli.Scoring;

namespace DeidSharp.Evaluation.Cli.Tests.Scoring;

public class BootstrapTests
{
    private static readonly AnnotatedText Note = new("Seen by Hart", [new GoldSpan(8, 12, PhiCategory.Name)]);

    private static DocumentTally Tally(string patient, bool found) =>
        new(patient, DetectionEvaluator.Tally(Note, found ? [new PhiSpan(8, 12, PhiCategory.Name, 1, "s")] : []));

    private static StackResult Result(IReadOnlyList<DocumentTally> documents) =>
        new("s", EvaluationTally.Sum(documents.Select(d => d.Tally)).ToReport(), [], [], [], documents);

    // One note per patient; the stack finds the names of the first `found` patients.
    private static StackResult Result(int patients, int found) =>
        Result([.. Enumerable.Range(0, patients).Select(p => Tally($"patient {p}", p < found))]);

    [Fact]
    public void Brackets_the_measured_recall_and_repeats_exactly()
    {
        var result = Result(patients: 80, found: 60);

        var first = new Bootstrap(80).Intervals(result);
        var again = new Bootstrap(80).Intervals(result);

        Assert.Equal(first, again);
        Assert.InRange(0.75, first.Recall.Low, first.Recall.High);
        Assert.InRange(first.Recall.High - first.Recall.Low, 0.05, 0.4);
    }

    [Fact]
    public void Collapses_to_the_measurement_when_nothing_varies()
    {
        var intervals = new Bootstrap(10).Intervals(Result(patients: 10, found: 10));

        Assert.Equal(new Interval(1, 1), intervals.Recall);
        Assert.Equal(new Interval(0, 0), intervals.NonPhiRemoved);
    }

    [Fact]
    public void Resamples_patients_not_notes()
    {
        // A patient's notes move together: with a single patient there is nothing to vary.
        var result = Result([Tally("patient 1", found: true), Tally("patient 1", found: false)]);

        Assert.Equal(new Interval(0.5, 0.5), new Bootstrap(1).Intervals(result).Recall);
    }

    [Fact]
    public void Measures_a_variant_against_its_stack_on_the_same_resamples()
    {
        var bootstrap = new Bootstrap(80);
        var baseline = Result(patients: 80, found: 60);

        var same = bootstrap.Gain(baseline, Result(patients: 80, found: 60));
        var better = bootstrap.Gain(baseline, Result(patients: 80, found: 70));

        Assert.Equal(new Change(0, new Interval(0, 0)), same.Recall);
        Assert.Equal(0.125, better.Recall.Value, 6);
        Assert.True(better.Recall.Interval.Low > 0, "ten more patients found: no resample leaves out all ten");
    }

    [Fact]
    public void Refuses_a_result_with_another_number_of_patients() =>
        Assert.Throws<InvalidOperationException>(() => new Bootstrap(5).Intervals(Result(patients: 6, found: 3)));
}
