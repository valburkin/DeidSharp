using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Reporting;
using DeidSharp.Evaluation.Cli.Scoring;

namespace DeidSharp.Evaluation.Cli.Tests.Reporting;

/// <summary>The misses and false-positives files: one span per line in its context, and never inside a repository.</summary>
public class ReviewFilesTests
{
    private static readonly CorpusDocument Note = new("note 7", "Line one.\nSeen by Dr. Hart today.", [new LabeledSpan(22, 26, "HCPName")]);

    private static readonly StackResult Result = new(
        "deterministic",
        DetectionEvaluator.Evaluate([]),
        [new LabelRecall("HCPName", 1, 0)],
        [new Miss(Note, Note.Spans[0])],
        [
            new FalsePositive(Note, new PhiSpan(0, 4, PhiCategory.Name, 0.9, "clinical")),
            new FalsePositive(Note, new PhiSpan(5, 8, PhiCategory.Age, 0.9, "clinical")),
        ],
        []);

    [Fact]
    public void Writes_each_miss_on_one_line_with_its_context()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "misses.txt");

        MissesFile.Write(path, [Result]);

        Assert.Equal(
            "## deterministic: 1 missed\n\nnote 7\t[22, 26)\tHCPName\tLine one. Seen by Dr. [[Hart]] today.\n\n",
            File.ReadAllText(path));
    }

    [Fact]
    public void Writes_each_false_positive_grouped_by_category_with_its_sources()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "false-positives.txt");

        FalsePositivesFile.Write(path, [Result]);

        Assert.Equal(
            "## deterministic: 2 false positives\n\n" +
            "note 7\t[0, 4)\tName\tclinical\t[[Line]] one. Seen by Dr. Hart today.\n" +
            "note 7\t[5, 8)\tAge\tclinical\tLine [[one]]. Seen by Dr. Hart today.\n\n",
            File.ReadAllText(path));
    }

    [Fact]
    public void Counts_false_positives_by_category()
    {
        Assert.Equal([(PhiCategory.Name, 1), (PhiCategory.Age, 1)], Result.FalsePositivesByCategory);
    }

    [Fact]
    public void Refuses_to_write_either_file_inside_a_git_working_tree()
    {
        var path = RepositoryFiles.Path("tests", "review.txt");

        Assert.Throws<UsageException>(() => MissesFile.Write(path, [Result]));
        Assert.Throws<UsageException>(() => FalsePositivesFile.Write(path, [Result]));
        Assert.False(File.Exists(path));
    }
}
