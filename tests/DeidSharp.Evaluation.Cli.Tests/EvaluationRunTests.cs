using System.IO.Compression;
using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Tests.Corpora.Surrogates;

namespace DeidSharp.Evaluation.Cli.Tests;

public class EvaluationRunTests
{
    [Fact]
    public async Task Compares_every_stack_with_another_tools_predictions_paired()
    {
        using var temp = new TempDirectory();
        var none = temp.File("none.tsv", "# a tool that finds nothing\n");
        var settings = EvaluationSettings.Parse(["--corpus", "asq-phi", "--predictions", $"none={none}", "--compare-with", "none"]);
        await using var output = new StringWriter();

        await EvaluationRun.RunAsync(settings, output, TextWriter.Null, new DateOnly(2026, 10, 19), TestContext.Current.CancellationToken);

        var report = output.ToString();
        var section = report[report.IndexOf("Change from none, in points (paired bootstrap, 95% CI)", StringComparison.Ordinal)..];
        Assert.StartsWith("deterministic", section.Split('\n')[2], StringComparison.Ordinal);
        Assert.DoesNotContain("\nnone ", section.Split("\n\n")[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Writes_a_gold_review_and_shows_only_rule_counts()
    {
        using var temp = new TempDirectory();
        var lists = SurrogateListsFixture.Write(temp);
        var notes = Path.Combine(temp.Path, "notes.csv.gz");
        await using (var gzip = new GZipStream(File.Create(notes), CompressionLevel.Fastest))
        {
            await gzip.WriteAsync("note_id,subject_id,text\n1-DS-1,1,Seen by Dr. ___ on ___.\n"u8.ToArray(), TestContext.Current.CancellationToken);
        }

        var review = Path.Combine(temp.Path, "gold.txt");
        var settings = EvaluationSettings.Parse(["--corpus", "asq-phi", "--split", "all", "--gold-review", review])
            with { Corpus = new MimicIvNoteCorpus(notes, patients: 1, expectedSha256: null, surrogatesDirectory: lists) };
        await using var output = new StringWriter();

        await EvaluationRun.RunAsync(settings, output, TextWriter.Null, new DateOnly(2026, 10, 28), TestContext.Current.CancellationToken);

        var console = output.ToString();
        Assert.Contains("doctor title", console, StringComparison.Ordinal);
        Assert.Contains("date preposition", console, StringComparison.Ordinal);
        Assert.DoesNotContain("Seen by", console, StringComparison.Ordinal);
        Assert.All(SurrogateListsFixture.Names, name => Assert.DoesNotContain(name, console, StringComparison.Ordinal));
        Assert.Contains("Seen by Dr. [[", await File.ReadAllTextAsync(review, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Exports_the_split_and_measures_nothing()
    {
        using var temp = new TempDirectory();
        var export = Path.Combine(temp.Path, "export");
        var settings = EvaluationSettings.Parse(["--corpus", "asq-phi", "--export", export]);
        await using var output = new StringWriter();

        await EvaluationRun.RunAsync(settings, output, TextWriter.Null, new DateOnly(2026, 10, 19), TestContext.Current.CancellationToken);

        Assert.Equal(1051, Directory.GetFiles(Path.Combine(export, "text")).Length);
        Assert.DoesNotContain("Recall", output.ToString(), StringComparison.Ordinal);
    }
}
