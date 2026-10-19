using DeidSharp.Evaluation.Cli.Corpora;

namespace DeidSharp.Evaluation.Cli.Tests.Corpora;

public class CorpusSplitTests
{
    private static readonly string[] Patients = [.. Enumerable.Range(1, 400).Select(p => $"patient {p}")];

    [Fact]
    public void Every_group_falls_in_exactly_one_half()
    {
        Assert.All(Patients, p => Assert.NotEqual(CorpusSplits.Includes(CorpusSplit.Tune, p), CorpusSplits.Includes(CorpusSplit.Report, p)));
        Assert.All(Patients, p => Assert.True(CorpusSplits.Includes(CorpusSplit.All, p)));
    }

    [Fact]
    public void The_halves_are_roughly_even()
    {
        var tune = Patients.Count(p => CorpusSplits.Includes(CorpusSplit.Tune, p));

        Assert.InRange(tune, 160, 240);
    }

    [Fact]
    public void The_assignment_never_changes()
    {
        // Pinned (and checked against Python's hashlib): if these move, notes that were tuned
        // on would leak into the reported half.
        Assert.Equal(
            "tune,tune,tune,tune,report,tune,report,tune,report,report",
            string.Join(",", Patients.Take(10).Select(p => CorpusSplits.Includes(CorpusSplit.Tune, p) ? "tune" : "report")));
    }

    [Fact]
    public void A_document_without_a_group_is_its_own_group()
    {
        Assert.Equal("query 7", new CorpusDocument("query 7", "text", []).SplitGroup);
        Assert.Equal("patient 3", new CorpusDocument("patient 3, note 2", "text", [], "patient 3").SplitGroup);
    }
}
