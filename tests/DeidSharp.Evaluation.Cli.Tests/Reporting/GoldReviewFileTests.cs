using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Reporting;

namespace DeidSharp.Evaluation.Cli.Tests.Reporting;

/// <summary>The gold review file: every gold span in its context with the rule that typed it, and never inside a repository.</summary>
public class GoldReviewFileTests
{
    private static readonly CorpusDocument Note = new(
        "note 7",
        "Seen by Dr. Hart on 3/4/2015.",
        [new LabeledSpan(12, 16, "StaffName", "doctor title"), new LabeledSpan(20, 28, "Date", "date preposition")]);

    [Fact]
    public void Writes_each_gold_span_with_its_label_and_rule_in_context()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "gold.txt");

        GoldReviewFile.Write(path, [Note]);

        Assert.Equal(
            "## note 7: 2 gold spans\n\n" +
            "StaffName\tdoctor title\tSeen by Dr. [[Hart]] on 3/4/2015.\n" +
            "Date\tdate preposition\tSeen by Dr. Hart on [[3/4/2015]].\n\n",
            File.ReadAllText(path));
    }

    [Fact]
    public void Counts_spans_by_rule_most_first_and_skips_annotated_spans()
    {
        var other = new CorpusDocument("note 8", "Dr. Ash", [new LabeledSpan(4, 7, "StaffName", "doctor title"), new LabeledSpan(0, 2, "HCPName")]);

        Assert.Equal([("doctor title", 2), ("date preposition", 1)], GoldReviewFile.CountByRule([Note, other]));
    }

    [Fact]
    public void Refuses_to_write_inside_a_git_working_tree()
    {
        var path = RepositoryFiles.Path("tests", "gold.txt");

        Assert.Throws<UsageException>(() => GoldReviewFile.Write(path, [Note]));
        Assert.False(File.Exists(path));
    }
}
