using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Reporting;
using DeidSharp.Evaluation.Cli.Scoring;
using DeidSharp.Evaluation.Cli.Tests.TestDoubles;

namespace DeidSharp.Evaluation.Cli.Tests.Reporting;

/// <summary>The strict span match on the console, on a fictional note.</summary>
public class ConsoleReportTests
{
    private const string Text = "Seen by Dr. Hart on 03/04/2024.";

    private static readonly CorpusDocument Note = new("note 1", Text, [Span("Hart", "Name"), Span("03/04/2024", "Date")]);

    private static readonly FictionalCorpus Corpus = new(
        [new LabelRule("Name", PhiCategory.Name, SafeHarbor: true), new LabelRule("Date", PhiCategory.Date, SafeHarbor: true)], Note);

    private static LabeledSpan Span(string text, string label)
    {
        var start = Text.IndexOf(text, StringComparison.Ordinal);
        return new LabeledSpan(start, start + text.Length, label);
    }

    // The stack finds the date exactly and the name with its title: both found, one of them strictly.
    internal static Task<StackResult> ScoreAsync() => StackScorer.ScoreAsync(
        new NamedStack("scripted", new ScriptedDetector(("Dr. Hart", PhiCategory.Name), ("03/04/2024", PhiCategory.Date))),
        Corpus, [Note], EvaluationScope.SafeHarbor, cancellationToken: TestContext.Current.CancellationToken);

    [Fact]
    public async Task Shows_the_strict_match_next_to_the_overlap()
    {
        var result = await ScoreAsync();
        await using var output = new StringWriter();

        ConsoleReport.Write(output, Corpus, EvaluationScope.SafeHarbor, CorpusSplit.All, 1, [result], []);

        var lines = output.ToString().Split(Environment.NewLine);
        var header = Array.FindIndex(lines, l => l.StartsWith("Strict span match", StringComparison.Ordinal));
        Assert.Equal(1, result.Report.Overall.Recall);
        Assert.Equal(["scripted", "50.00%", "50.00%", "50.00%"], lines[header + 2].Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
