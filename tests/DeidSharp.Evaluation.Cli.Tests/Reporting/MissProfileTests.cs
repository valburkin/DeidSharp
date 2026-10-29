using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Reporting;
using DeidSharp.Evaluation.Cli.Scoring;
using DeidSharp.Evaluation.Cli.Tests.TestDoubles;

namespace DeidSharp.Evaluation.Cli.Tests.Reporting;

/// <summary>Counts a label's misses by feature, on a fictional note whose spans each have known features.</summary>
public class MissProfileTests
{
    private const string Text = "Dr. Ellen Hart saw Rose by the rose bed. Hart called LANE and Lane. Okafor met Dr. Janet Laneway in room 42.";

    private static readonly CorpusDocument Note = new(
        "note 1",
        Text,
        [Span("Ellen Hart", "Staff"), Span("Rose", "Name"), Span("Hart", "Name", from: 20), Span("LANE", "Name"), Span("Lane", "Name"), Span("bed", "Ward"), Span("Okafor", "Name"), Span("Janet Laneway", "Staff"), Span("42", "Name")]);

    private sealed class Fictional : ICorpus
    {
        public string Id => "fictional";

        public string Title => "Fictional";

        public LabelMap Labels { get; } = new(
        [
            new LabelRule("Staff", PhiCategory.Name, SafeHarbor: true),
            new LabelRule("Name", PhiCategory.Name, SafeHarbor: true),
            new LabelRule("Ward", PhiCategory.Location, SafeHarbor: false),
        ]);

        public IReadOnlyList<CorpusDocument> Read() => [Note];
    }

    private static LabeledSpan Span(string text, string label, int from = 0)
    {
        var start = Text.IndexOf(text, from, StringComparison.Ordinal);
        return new LabeledSpan(start, start + text.Length, label);
    }

    // The stack finds the clinician's full name and "LANE": it misses "Rose", the second "Hart", "Lane"
    // "Okafor" and "42". "Okafor" is the one name that isn't an ordinary word, "Laneway" holds "Lane"
    // only as part of a word, and "42" has no letters, so it has no case.
    private static async Task<string> ProfileAsync(string label, EvaluationScope scope = EvaluationScope.SafeHarbor)
    {
        var corpus = new Fictional();
        var result = await StackScorer.ScoreAsync(
            new NamedStack("scripted", new ScriptedDetector(("Ellen Hart", PhiCategory.Name), ("LANE", PhiCategory.Name))), corpus, [Note], scope, cancellationToken: TestContext.Current.CancellationToken);
        await using var output = new StringWriter();
        MissProfile.Write(output, label, corpus, scope, [Note], [result]);
        return output.ToString();
    }

    private static string Row(string profile, string feature) =>
        profile.Split('\n').Single(l => l.StartsWith(feature + "  ", StringComparison.Ordinal))[feature.Length..].Trim();

    [Theory]
    [InlineData("every span", "5 / 6 (83.33%)")]
    [InlineData("one word", "5 / 6 (83.33%)")]
    [InlineData("several words", "–")]
    [InlineData("an ordinary English word", "3 / 4 (75.00%)")]
    [InlineData("a lowercase word elsewhere in the note", "1 / 1 (100.00%)")]
    [InlineData("a word in another label's span", "1 / 1 (100.00%)")]
    [InlineData("in capitals", "0 / 1 (0.00%)")]
    [InlineData("capitalized", "4 / 4 (100.00%)")]
    [InlineData("lowercase", "–")]
    [InlineData("no other gold span with its text", "4 / 4 (100.00%)")]
    [InlineData("another with its text, found", "1 / 1 (100.00%)")]
    [InlineData("others with its text, none found", "0 / 1 (0.00%)")]
    public async Task Counts_the_misses_among_the_spans_with_each_feature(string feature, string cell)
    {
        var profile = await ProfileAsync("Name");

        Assert.Equal(cell, Row(profile, feature));
    }

    [Fact]
    public async Task Prints_counts_and_never_the_spans_text()
    {
        var profile = await ProfileAsync("Name");

        Assert.Contains("Misses of Name by feature", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("Rose", profile, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Hart", profile, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Lane", profile, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Okafor", profile, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Says_so_for_a_label_outside_the_scope()
    {
        var profile = await ProfileAsync("Ward");

        Assert.Contains("No Ward span counts in this scope", profile, StringComparison.Ordinal);
        Assert.Equal("1 / 1 (100.00%)", Row(await ProfileAsync("Ward", EvaluationScope.All), "every span"));
    }

    [Fact]
    public void Refuses_a_label_the_corpus_lacks_and_names_the_ones_it_has()
    {
        var e = Assert.Throws<UsageException>(() => MissProfile.EnsureLabel("Hospital", [Note]));

        Assert.Equal("--miss-profile takes one of this corpus's labels: Name, Staff, Ward.", e.Message);
    }
}
