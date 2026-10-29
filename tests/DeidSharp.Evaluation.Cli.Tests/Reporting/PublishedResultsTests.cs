using DeidSharp.Evaluation.Cli.Reporting;

namespace DeidSharp.Evaluation.Cli.Tests.Reporting;

/// <summary>results.json, the README table and badge.json: built from one another, and kept in step.</summary>
public class PublishedResultsTests
{
    private static CorpusResult Result(string corpus, string scope, double recall, string version = "0.1.0", string split = "report") => new(
        corpus,
        corpus.ToUpperInvariant(),
        scope,
        version,
        "0123456789abcdef",
        new DateOnly(2026, 10, 17),
        2434,
        1733,
        [
            new StackSummary("deterministic", 0.4, 0.45, 0.9, 1040, [new LabelSummary("HCPName", 593, 0.12)]),
            new StackSummary("deterministic + clinical", recall, 0.96, 0.7, 50, [new LabelSummary("HCPName", 593, 0.98)]),
        ],
        split);

    [Fact]
    public void A_measurement_on_another_split_is_kept_beside_this_one()
    {
        var results = new PublishedResults([Result("physionet-deid", "safe-harbor", 0.9, split: "tune")])
            .With(Result("physionet-deid", "safe-harbor", 0.88, split: "report"));

        Assert.Equal(["tune", "report"], results.Corpora.Select(c => c.Split));
    }

    [Fact]
    public void The_badge_ignores_measurements_outside_the_held_out_half()
    {
        var badge = ShieldsBadge.From(new PublishedResults([Result("physionet-deid", "safe-harbor", 0.97, split: "all")]));

        Assert.Equal("not measured yet", badge.Message);
    }

    [Fact]
    public void A_new_measurement_replaces_its_corpus_and_scope_and_keeps_the_others()
    {
        var results = new PublishedResults([Result("asq-phi", "safe-harbor", 0.99), Result("physionet-deid", "safe-harbor", 0.9)])
            .With(Result("physionet-deid", "safe-harbor", 0.95))
            .With(Result("physionet-deid", "all", 0.93));

        Assert.Equal(["asq-phi/safe-harbor", "physionet-deid/safe-harbor", "physionet-deid/all"], results.Corpora.Select(c => $"{c.Corpus}/{c.Scope}"));
        Assert.Equal(0.95, results.Corpora[1].Stacks[1].Recall);
    }

    [Fact]
    public void The_badge_names_the_clinical_stack_and_its_physionet_recall_in_the_safe_harbor_scope()
    {
        var badge = ShieldsBadge.From(new PublishedResults([Result("asq-phi", "safe-harbor", 0.9997), Result("physionet-deid", "safe-harbor", 0.9719)]));

        Assert.Equal("97.1% · deterministic + clinical · v0.1.0", badge.Message);
        Assert.Equal("blue", badge.Color);
    }

    [Fact]
    public void The_badge_rounds_down_so_a_miss_never_shows_as_100_percent()
    {
        var badge = ShieldsBadge.From(new PublishedResults([Result("physionet-deid", "safe-harbor", 0.9996)]));

        Assert.StartsWith("99.9%", badge.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(57, 100, 0.57)]
    [InlineData(2975, 2976, 0.9996)]
    [InlineData(1, 3, 0.3333)]
    public void Published_rates_are_rounded_down_to_four_decimals(int detected, int gold, double expected)
    {
        Assert.Equal(expected, StackSummary.Round((double)detected / gold));
    }

    [Fact]
    public void A_note_is_published_with_the_results_and_left_out_when_absent()
    {
        var noted = Result("physionet-deid", "safe-harbor", 0.97) with { Note = "clinical sidecar on CUDA" };

        Assert.Contains("· clinical sidecar on CUDA", ResultsPage.Render(new PublishedResults([noted])), StringComparison.Ordinal);
        Assert.Contains("\"note\": \"clinical sidecar on CUDA\"", ResultsPublisher.ResultsJsonText(new PublishedResults([noted])), StringComparison.Ordinal);
        Assert.DoesNotContain("\"note\"", ResultsPublisher.ResultsJsonText(new PublishedResults([Result("asq-phi", "safe-harbor", 0.97)])), StringComparison.Ordinal);
    }

    [Fact]
    public void The_badge_says_so_when_physionet_was_not_measured()
    {
        var badge = ShieldsBadge.From(new PublishedResults([Result("asq-phi", "safe-harbor", 0.9997)]));

        Assert.Equal("not measured yet", badge.Message);
        Assert.Equal("lightgrey", badge.Color);
    }

    [Fact]
    public void The_page_section_is_replaced_and_the_rest_of_the_page_kept()
    {
        const string page = $"# Evaluation\n\nBefore.\n\n{ResultsPage.Begin}\nstale table\n{ResultsPage.End}\n\nAfter.\n";

        var updated = ResultsPage.Update(page, new PublishedResults([Result("physionet-deid", "safe-harbor", 0.9712)]));

        Assert.StartsWith("# Evaluation\n\nBefore.\n\n", updated, StringComparison.Ordinal);
        Assert.EndsWith("\n\nAfter.\n", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("stale table", updated, StringComparison.Ordinal);
        Assert.Contains("| deterministic + clinical | 97.12% | 96.00% | 70.00% | – | – | 50 |", updated, StringComparison.Ordinal);
        Assert.Contains("| HCPName | 593 | 12.00% | 98.00% |", updated, StringComparison.Ordinal);
        Assert.Contains("DeidSharp 0.1.0 (`0123456`)", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Word_metrics_text_removed_and_intervals_are_published_rounded_so_they_never_flatter()
    {
        var stack = new StackSummary("deterministic + clinical", 0.9, 0.88, 0.5, 9, [])
        {
            Words = new RatesSummary(0.91, 0.6, 0.7232),
            NonPhiRemoved = StackSummary.RoundUp(0.01234),
            Intervals = new IntervalsSummary(new RangeSummary(0.87, 0.93), new RangeSummary(0.85, 0.9), new RangeSummary(0.4, 0.6), new RangeSummary(0.7, 0.75), new RangeSummary(0.01, 0.015)),
        };
        var results = new PublishedResults([Result("physionet-deid", "safe-harbor", 0.9) with { Stacks = [stack] }]);

        Assert.Contains("| deterministic + clinical | 90.00% (87.00–93.00) | 88.00% | 50.00% | 72.32% | 1.24% | 9 |", ResultsPage.Render(results), StringComparison.Ordinal);
        Assert.Contains("\"nonPhiRemoved\": 0.0124", ResultsPublisher.ResultsJsonText(results), StringComparison.Ordinal);
        Assert.DoesNotContain("\"intervals\"", ResultsPublisher.ResultsJsonText(new PublishedResults([Result("asq-phi", "safe-harbor", 0.97)])), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_strict_match_is_published_rounded_down_and_left_out_when_absent()
    {
        var stack = StackSummary.From(await ConsoleReportTests.ScoreAsync());
        var json = ResultsPublisher.ResultsJsonText(new PublishedResults([Result("physionet-deid", "safe-harbor", 0.9) with { Stacks = [stack] }]));

        Assert.Equal(new RatesSummary(0.5, 0.5, 0.5), stack.Strict);
        Assert.Contains("\"strict\": {", json, StringComparison.Ordinal);
        Assert.Equal(0.6666, StackSummary.Round(2.0 / 3));
        Assert.DoesNotContain("\"strict\"", ResultsPublisher.ResultsJsonText(new PublishedResults([Result("asq-phi", "safe-harbor", 0.97)])), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0.01231, 0.0124)]
    [InlineData(0.57, 0.57)]
    public void The_text_removed_is_rounded_up(double value, double expected) => Assert.Equal(expected, StackSummary.RoundUp(value));

    [Fact]
    public void A_page_without_markers_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() => ResultsPage.Update("# Evaluation\n", new PublishedResults([])));
    }

    [Fact]
    public void A_directory_without_the_results_markers_is_refused_before_the_run()
    {
        using var temp = new TempDirectory();
        temp.File(ResultsPublisher.PageFile, "# Evaluation\n");

        Assert.Throws<UsageException>(() => ResultsPublisher.EnsurePublishable(temp.Path));
    }

    [Fact]
    public void Publishing_writes_all_three_files()
    {
        using var temp = new TempDirectory();
        temp.File(ResultsPublisher.PageFile, $"# Evaluation\n\n{ResultsPage.Begin}\n{ResultsPage.End}\n");

        ResultsPublisher.Publish(temp.Path, Result("physionet-deid", "safe-harbor", 0.9712));

        var results = ResultsPublisher.Load(temp.Path);
        Assert.Equal("physionet-deid", Assert.Single(results.Corpora).Corpus);
        Assert.Contains("97.1% · deterministic + clinical", File.ReadAllText(Path.Combine(temp.Path, ResultsPublisher.BadgeFile)), StringComparison.Ordinal);
        Assert.Contains("97.12%", File.ReadAllText(Path.Combine(temp.Path, ResultsPublisher.PageFile)), StringComparison.Ordinal);
    }

    [Fact]
    public void The_committed_files_agree_with_results_json()
    {
        var directory = RepositoryFiles.Path("docs", "evaluation");
        var results = ResultsPublisher.Load(directory);

        // Hand edits to the table or the badge would drift from the measurement; rerun the tool instead.
        Assert.Equal(ResultsPublisher.BadgeJsonText(results), Normalized(Path.Combine(directory, ResultsPublisher.BadgeFile)));
        Assert.Equal(ResultsPage.Render(results), ResultsPage.Current(Normalized(Path.Combine(directory, ResultsPublisher.PageFile))));
        Assert.Equal(ResultsPublisher.ResultsJsonText(results), Normalized(Path.Combine(directory, ResultsPublisher.ResultsFile)));
    }

    // A Windows checkout may turn the files' "\n" into "\r\n".
    private static string Normalized(string path) => File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
}
