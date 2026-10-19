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
