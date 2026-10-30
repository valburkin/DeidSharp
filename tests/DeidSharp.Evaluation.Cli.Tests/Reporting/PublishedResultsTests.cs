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
        Assert.Throws<UsageException>(() => ResultsPage.Update("# Evaluation\n", new PublishedResults([])));
    }

    [Fact]
    public void A_directory_without_the_results_markers_is_refused_before_the_run()
    {
        using var temp = new TempDirectory();
        temp.File(ResultsPublisher.PageFile, "# Evaluation\n");

        Assert.Throws<UsageException>(() => ResultsPublisher.EnsurePublishable(temp.Path));
    }

    [Fact]
    public void A_lone_glance_marker_in_the_repository_readme_is_refused_before_the_run()
    {
        using var temp = new TempDirectory();
        temp.File(Path.Combine("docs", "evaluation", ResultsPublisher.PageFile), $"# Evaluation\n\n{ResultsPage.Begin}\n{ResultsPage.End}\n");
        temp.File(ResultsPublisher.PageFile, $"# DeidSharp\n\n{ResultsGlance.Begin}\n");

        var error = Assert.Throws<UsageException>(() => ResultsPublisher.EnsurePublishable(Path.Combine(temp.Path, "docs", "evaluation")));

        Assert.Contains("restore them from Git", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Publishing_says_when_the_repository_readme_has_no_glance_markers()
    {
        using var temp = new TempDirectory();
        temp.File(Path.Combine("docs", "evaluation", ResultsPublisher.PageFile), $"# Evaluation\n\n{ResultsPage.Begin}\n{ResultsPage.End}\n");
        temp.File(ResultsPublisher.PageFile, "# DeidSharp\n");

        var notice = ResultsPublisher.Publish(Path.Combine(temp.Path, "docs", "evaluation"), Result("physionet-deid", "safe-harbor", 0.9712));

        Assert.Equal("The repository README has no results-at-a-glance markers, so its table was left as it is.", notice);
        Assert.Equal("# DeidSharp\n", File.ReadAllText(Path.Combine(temp.Path, ResultsPublisher.PageFile)));
    }

    [Fact]
    public void Publishing_writes_all_three_files()
    {
        using var temp = new TempDirectory();
        temp.File(ResultsPublisher.PageFile, $"# Evaluation\n\n{ResultsPage.Begin}\n{ResultsPage.End}\n");

        // A directory other than docs/evaluation has no repository README to tell about.
        Assert.Null(ResultsPublisher.Publish(temp.Path, Result("physionet-deid", "safe-harbor", 0.9712)));

        var results = ResultsPublisher.Load(temp.Path);
        Assert.Equal("physionet-deid", Assert.Single(results.Corpora).Corpus);
        Assert.Contains("97.1% · deterministic + clinical", File.ReadAllText(Path.Combine(temp.Path, ResultsPublisher.BadgeFile)), StringComparison.Ordinal);
        Assert.Contains("97.12%", File.ReadAllText(Path.Combine(temp.Path, ResultsPublisher.PageFile)), StringComparison.Ordinal);
    }

    [Fact]
    public void The_language_models_are_published_pinned_and_left_out_when_absent()
    {
        using var temp = new TempDirectory();
        var models = new LanguageModels("0.40.11", [new ModelPin("gemma4:26b-a4b", "001e5dafc3c7a8f2b6d4e1c0f9b8a7d6e5f4c3b2a1908f7e6d5c4b3a29180706")]);
        var measured = Result("physionet-deid", "safe-harbor", 0.97) with { LanguageModels = models };
        var json = ResultsPublisher.ResultsJsonText(new PublishedResults([measured]));
        temp.File(ResultsPublisher.ResultsFile, json);

        Assert.Contains("· LLM gemma4:26b-a4b (`001e5dafc3c7`) on Ollama 0.40.11", ResultsPage.Render(new PublishedResults([measured])), StringComparison.Ordinal);
        Assert.Contains("\"digest\": \"001e5dafc3c7a8f2", json, StringComparison.Ordinal);
        Assert.Equal(models, ResultsPublisher.Load(temp.Path).Corpora[0].LanguageModels, LanguageModelsComparer.Instance);
        Assert.DoesNotContain("\"languageModels\"", ResultsPublisher.ResultsJsonText(new PublishedResults([Result("asq-phi", "safe-harbor", 0.97)])), StringComparison.Ordinal);
    }

    [Fact]
    public void Language_models_are_the_same_only_with_the_same_server_and_weights()
    {
        var models = new LanguageModels("0.40.11", [new ModelPin("gemma4:26b-a4b", "001e"), new ModelPin("qwen3.5:9b", "5667")]);

        Assert.True(models.SameAs(new LanguageModels("0.40.11", [new ModelPin("gemma4:26b-a4b", "001e"), new ModelPin("qwen3.5:9b", "5667")])));
        Assert.False(models.SameAs(models with { Ollama = "0.40.12" }));
        Assert.False(models.SameAs(new LanguageModels("0.40.11", [new ModelPin("gemma4:26b-a4b", "9999"), new ModelPin("qwen3.5:9b", "5667")])));
    }

    [Fact]
    public void Results_are_not_published_when_the_server_or_the_weights_changed_during_the_run()
    {
        var before = new LanguageModels("0.40.11", [new ModelPin("gemma4:26b-a4b", "001e")]);

        before.EnsureUnchanged(before with { });
        Assert.Throws<UsageException>(() => before.EnsureUnchanged(before with { Ollama = "0.41.0" }));
        Assert.Throws<UsageException>(() => before.EnsureUnchanged(null));
    }

    [Fact]
    public void A_run_that_publishes_must_measure_the_headline_stacks()
    {
        string[] withoutModel = ["deterministic", HeadlineStacks.WithoutLlm];

        Assert.Empty(HeadlineStacks.MissingFrom(withoutModel, withLlm: false));
        Assert.Equal([HeadlineStacks.WithoutLlm], HeadlineStacks.MissingFrom(["deterministic"], withLlm: false));
        Assert.Equal(2, HeadlineStacks.MissingFrom(withoutModel, withLlm: true).Count);
        Assert.Empty(HeadlineStacks.MissingFrom([.. withoutModel, HeadlineStacks.RecallFirst, "deterministic without gazetteer + llm gemma4:26b-a4b (all-examples)"], withLlm: true));
    }

    [Fact]
    public void A_run_without_a_model_must_not_drop_the_published_stacks_with_one()
    {
        var published = Result("physionet-deid", "safe-harbor", 0.88) is var result
            ? result with { Stacks = [.. result.Stacks, result.Stacks[1] with { Stack = HeadlineStacks.RecallFirst }] }
            : null;

        Assert.Equal([HeadlineStacks.RecallFirst], HeadlineStacks.MissingFrom(["deterministic", HeadlineStacks.WithoutLlm], withLlm: false, published));
    }

    [Fact]
    public void A_broken_results_json_is_refused_before_the_run()
    {
        using var temp = new TempDirectory();
        temp.File(ResultsPublisher.PageFile, $"# Evaluation\n\n{ResultsPage.Begin}\n{ResultsPage.End}\n");
        temp.File(ResultsPublisher.ResultsFile, "{ \"corpora\": [");

        var error = Assert.Throws<UsageException>(() => ResultsPublisher.EnsurePublishable(temp.Path));

        Assert.Contains("restore it from Git", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_a_docs_evaluation_folder_reaches_the_repository_readme_and_a_lone_marker_is_refused()
    {
        using var temp = new TempDirectory();

        Assert.Null(ResultsPublisher.GlancePage(temp.Path));
        Assert.Equal(Path.Combine(temp.Path, "README.md"), ResultsPublisher.GlancePage(Path.Combine(temp.Path, "docs", "evaluation")));
        Assert.Equal(Path.Combine(temp.Path, "README.md"), ResultsPublisher.GlancePage(Path.Combine(temp.Path, "Docs", "Evaluation")));
        Assert.False(ResultsGlance.HasSection("# DeidSharp\n"));
        Assert.Throws<UsageException>(() => ResultsGlance.HasSection($"# DeidSharp\n\n{ResultsGlance.Begin}\n"));
        Assert.Throws<UsageException>(() => ResultsGlance.HasSection($"{ResultsGlance.End}\n{ResultsGlance.Begin}\n"));
    }

    [Fact]
    public void The_badge_shows_the_recall_first_stack_beside_the_one_without_a_model_once_both_are_published()
    {
        var measured = Result("physionet-deid", "safe-harbor", 0.883);
        measured = measured with { Stacks = [.. measured.Stacks, new StackSummary(HeadlineStacks.RecallFirst, 0.98889, 0.99, 0.54, 10, [])] };

        var badge = ShieldsBadge.From(new PublishedResults([measured]));

        Assert.Equal("98.8% with a local LLM · 88.3% without · v0.1.0", badge.Message);
    }

    [Fact]
    public void The_glance_shows_recall_and_precision_of_each_headline_stack_per_corpus_and_what_was_not_measured()
    {
        var nursing = Result("physionet-deid", "safe-harbor", 0.883);
        nursing = nursing with { Stacks = [.. nursing.Stacks, new StackSummary(HeadlineStacks.RecallFirst, 0.9888, 0.99, 0.5431, 10, [])] };
        var asq = Result("asq-phi", "safe-harbor", 0.9973, split: "all");

        var glance = ResultsGlance.Render(new PublishedResults([nursing, asq, Result("physionet-deid", "all", 0.5)]));

        Assert.StartsWith(ResultsGlance.Begin, glance, StringComparison.Ordinal);
        Assert.EndsWith(ResultsGlance.End, glance, StringComparison.Ordinal);
        Assert.Contains("| Recall / precision | Nursing notes | Discharge summaries | ASQ-PHI |", glance, StringComparison.Ordinal);
        Assert.Contains("| Recall first: deterministic + clinical + LLM | 98.88% / 54.31% | not measured | not measured |", glance, StringComparison.Ordinal);
        Assert.Contains("| Without an LLM: deterministic + clinical | 88.30% / 70.00% | not measured | 99.73% / 70.00% |", glance, StringComparison.Ordinal);
        Assert.Contains("measured with DeidSharp 0.1.0 (`0123456`).", glance, StringComparison.Ordinal);
    }

    [Fact]
    public void Publishing_also_updates_the_repository_readme_where_it_has_the_glance_markers()
    {
        using var temp = new TempDirectory();
        temp.File(Path.Combine("docs", "evaluation", ResultsPublisher.PageFile), $"# Evaluation\n\n{ResultsPage.Begin}\n{ResultsPage.End}\n");
        temp.File(ResultsPublisher.PageFile, $"# DeidSharp\n\n{ResultsGlance.Begin}\nstale\n{ResultsGlance.End}\n\nAfter.\n");
        var evaluation = Path.Combine(temp.Path, "docs", "evaluation");

        Assert.Null(ResultsPublisher.Publish(evaluation, Result("physionet-deid", "safe-harbor", 0.9712)));

        var readme = File.ReadAllText(Path.Combine(temp.Path, ResultsPublisher.PageFile));
        Assert.Equal(Path.Combine(temp.Path, ResultsPublisher.PageFile), ResultsPublisher.GlancePage(evaluation));
        Assert.Contains("| Without an LLM: deterministic + clinical | 97.12% / 70.00% |", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("stale", readme, StringComparison.Ordinal);
        Assert.EndsWith($"{ResultsGlance.End}\n\nAfter.\n", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void The_committed_readme_glance_agrees_with_results_json()
    {
        var readme = Normalized(RepositoryFiles.Path(ResultsPublisher.PageFile));
        Assert.True(ResultsGlance.HasSection(readme), "The README's results-at-a-glance markers are gone; restore them from Git.");

        // Hand edits would drift from the measurement; rerun the tool (--render) instead.
        Assert.Equal(ResultsGlance.Render(ResultsPublisher.Load(RepositoryFiles.Path("docs", "evaluation"))), ResultsGlance.Current(readme));
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

    // Records compare lists by reference; this compares the pins.
    private sealed class LanguageModelsComparer : IEqualityComparer<LanguageModels?>
    {
        public static LanguageModelsComparer Instance { get; } = new();

        public bool Equals(LanguageModels? x, LanguageModels? y) => x is null ? y is null : y is not null && x.SameAs(y);

        public int GetHashCode(LanguageModels? obj) => obj?.Ollama.GetHashCode(StringComparison.Ordinal) ?? 0;
    }
}
