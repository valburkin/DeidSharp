using System.Text.Json.Serialization;
using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Scoring;

namespace DeidSharp.Evaluation.Cli.Reporting;

/// <summary>
/// docs/evaluation/results.json: the latest measurement per corpus, scope and split. Counts and
/// rates only, so it is safe to commit whatever the corpus's license.
/// </summary>
internal sealed record PublishedResults(IReadOnlyList<CorpusResult> Corpora)
{
    /// <summary>These results with <paramref name="result"/> in place of the entry for its corpus, scope and split, or added after the others.</summary>
    public PublishedResults With(CorpusResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var corpora = Corpora.ToList();
        var index = corpora.FindIndex(c => c.Corpus == result.Corpus && c.Scope == result.Scope && c.Split == result.Split);
        if (index >= 0)
        {
            corpora[index] = result;
        }
        else
        {
            corpora.Add(result);
        }

        return new PublishedResults(corpora);
    }
}

/// <summary>One corpus measured in one scope and split by one build of DeidSharp.</summary>
/// <param name="Split">Which half of the corpus: all, tune or report (<see cref="CorpusSplit"/>).</param>
/// <param name="Commit">The Git commit the build came from, or empty if unknown.</param>
/// <param name="Note">What the numbers came from that the tool can't see (the sidecar images, CPU or GPU), if given.</param>
internal sealed record CorpusResult(
    string Corpus,
    string Title,
    string Scope,
    string Version,
    string Commit,
    DateOnly Date,
    int Documents,
    int GoldSpans,
    IReadOnlyList<StackSummary> Stacks,
    string Split = "all",
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Note = null)
{
    public static CorpusResult From(
        ICorpus corpus, EvaluationScope scope, CorpusSplit split, int documents, IReadOnlyList<StackResult> results, BuildVersion build, DateOnly date, string? note = null)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(build);
        return new CorpusResult(
            corpus.Id,
            corpus.Title,
            EvaluationScopes.Id(scope),
            build.Version,
            build.Commit,
            date,
            documents,
            results.Count > 0 ? results[0].Report.Overall.GoldSpans : 0,
            [.. results.Select(StackSummary.From)],
            CorpusSplits.Id(split),
            note);
    }
}

/// <summary>
/// One stack's numbers, rounded to four decimals so that a published rate never flatters: down
/// for the rates where higher is better, up for the text removed, and intervals outward.
/// </summary>
/// <param name="Words">Word-level recall, precision and F1; absent in results measured before the tool reported them.</param>
/// <param name="NonPhiRemoved">The share of non-PHI text removed; absent in older results.</param>
/// <param name="Intervals">95% bootstrap confidence intervals; absent in older results.</param>
internal sealed record StackSummary(
    string Stack,
    double Recall,
    double CharacterRecall,
    double Precision,
    int Missed,
    IReadOnlyList<LabelSummary> Labels,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WordSummary? Words = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? NonPhiRemoved = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IntervalsSummary? Intervals = null)
{
    public static StackSummary From(StackResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var words = result.Report.Words;
        return new StackSummary(
            result.Stack,
            Round(result.Report.Overall.Recall),
            Round(result.Report.CharacterRecall),
            Round(result.Report.Overall.Precision),
            result.Misses.Count,
            [.. result.Labels.Select(l => new LabelSummary(l.Label, l.GoldSpans, Round(l.Recall)))],
            new WordSummary(Round(words.Recall), Round(words.Precision), Round(words.F1)),
            RoundUp(result.Report.NonPhiRemoved),
            result.Intervals is { } i
                ? new IntervalsSummary(Outward(i.Recall), Outward(i.CharacterRecall), Outward(i.Precision), Outward(i.WordF1), Outward(i.NonPhiRemoved))
                : null);
    }

    // In decimal: a double such as 57/100 is stored just below 0.57, and would round down to 0.5699.
    internal static double Round(double value) => (double)(Math.Floor((decimal)value * 10_000m) / 10_000m);

    internal static double RoundUp(double value) => (double)(Math.Ceiling((decimal)value * 10_000m) / 10_000m);

    private static RangeSummary Outward(Interval interval) => new(Round(interval.Low), RoundUp(interval.High));
}

/// <summary>Word-level recall, precision and F1 (PHI or not), as Philter and pyclipse report them.</summary>
internal sealed record WordSummary(double Recall, double Precision, double F1);

/// <summary>95% confidence intervals for a stack's headline numbers.</summary>
internal sealed record IntervalsSummary(RangeSummary Recall, RangeSummary CharacterRecall, RangeSummary Precision, RangeSummary WordF1, RangeSummary NonPhiRemoved);

/// <summary>A confidence interval <c>[Low, High]</c>.</summary>
internal sealed record RangeSummary(double Low, double High);

/// <summary>Recall for one of the corpus's own labels.</summary>
internal sealed record LabelSummary(string Label, int GoldSpans, double Recall);
