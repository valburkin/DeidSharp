namespace DeidSharp.Evaluation.Cli.Scoring;

/// <summary>
/// 95% confidence intervals by a percentile bootstrap over split groups (patients): each
/// resample draws as many groups as there are, with replacement, and sums their documents'
/// tallies. A patient's notes share names and dates, so resampling notes one by one would
/// understate the uncertainty. The draws come from a fixed seed, so a rerun gives the same
/// intervals, and every stack is measured on the same draws.
/// </summary>
internal sealed class Bootstrap
{
    public const int DefaultResamples = 1000;

    // Any fixed number: what matters is that published intervals can be reproduced.
    private const int Seed = 20_261_018;

    private readonly int[][] _draws;

    /// <param name="groups">How many groups (patients) the documents fall into.</param>
    public Bootstrap(int groups, int resamples = DefaultResamples)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(groups, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(resamples, 1);

        // A seeded generator on purpose: the draws must repeat, not be unpredictable.
        var random = new Random(Seed);
        _draws = [.. Enumerable.Range(0, resamples).Select(_ => Enumerable.Range(0, groups).Select(_ => random.Next(groups)).ToArray())];
    }

    public ConfidenceIntervals Intervals(StackResult result)
    {
        var reports = Reports(result);
        return new ConfidenceIntervals(
            Interval(reports, r => r.Overall.Recall),
            Interval(reports, r => r.CharacterRecall),
            Interval(reports, r => r.Overall.Precision),
            Interval(reports, r => r.Words.F1),
            Interval(reports, r => r.NonPhiRemoved));
    }

    /// <summary>
    /// How <paramref name="variant"/> differs from <paramref name="baseline"/>, with intervals from the
    /// same resamples for both (paired): a change is real when its interval excludes zero, a
    /// stricter and fairer test than comparing two separate intervals.
    /// </summary>
    public Gain Gain(StackResult baseline, StackResult variant)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(variant);
        var pairs = Reports(baseline).Zip(Reports(variant)).ToList();
        return new Gain(Of(r => r.Overall.Recall), Of(r => r.Overall.Precision), Of(r => r.Words.F1));

        Change Of(Func<EvaluationReport, double> metric) =>
            new(metric(variant.Report) - metric(baseline.Report), Interval([.. pairs.Select(p => metric(p.Second) - metric(p.First))]));
    }

    private List<EvaluationReport> Reports(StackResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        // Groups in order of first appearance: the same order for every stack of a run.
        var groups = result.Documents.GroupBy(d => d.Group, StringComparer.Ordinal)
            .Select(g => EvaluationTally.Sum(g.Select(d => d.Tally))).ToList();
        return groups.Count == _draws[0].Length
            ? [.. _draws.Select(draw => EvaluationTally.Sum(draw.Select(i => groups[i])).ToReport())]
            : throw new InvalidOperationException("The bootstrap was planned for another number of groups.");
    }

    private static Interval Interval(List<EvaluationReport> reports, Func<EvaluationReport, double> metric) => Interval([.. reports.Select(metric)]);

    private static Interval Interval(List<double> samples)
    {
        var values = samples.Order().ToList();
        return new Interval(values[(int)Math.Floor(0.025 * values.Count)], values[(int)Math.Ceiling(0.975 * values.Count) - 1]);
    }
}

/// <summary>95% confidence intervals for a stack's headline numbers.</summary>
internal sealed record ConfidenceIntervals(Interval Recall, Interval CharacterRecall, Interval Precision, Interval WordF1, Interval NonPhiRemoved);

/// <summary>A variant's change from the stack it wraps, in recall, precision and word F1.</summary>
internal sealed record Gain(Change Recall, Change Precision, Change WordF1);

/// <summary>How a stack differs from another tool's predictions (the stack minus the tool), with paired intervals.</summary>
internal sealed record Comparison(string Tool, string Stack, Gain Gain);

/// <summary>A measured change (variant minus baseline) and its paired 95% confidence interval.</summary>
internal sealed record Change(double Value, Interval Interval);

/// <summary>A confidence interval <c>[Low, High]</c>.</summary>
internal sealed record Interval(double Low, double High);
