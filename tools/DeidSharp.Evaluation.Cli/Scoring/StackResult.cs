using DeidSharp.Evaluation.Cli.Corpora;

namespace DeidSharp.Evaluation.Cli.Scoring;

/// <summary>How one detector stack did on one corpus.</summary>
/// <param name="Labels">Recall by the corpus's own labels, finer than DeidSharp's categories (staff and patient names apart).</param>
/// <param name="Misses">The gold spans no detector in the stack overlapped.</param>
/// <param name="FalsePositives">The predicted spans that overlap no gold span in the run's scope.</param>
/// <param name="Documents">Each document's counts with its split group, for resampling.</param>
/// <param name="Intervals">95% confidence intervals, once a <see cref="Bootstrap"/> has computed them.</param>
/// <param name="Baseline">The stack this one is a variant of, if any.</param>
/// <param name="Gain">The change from <paramref name="Baseline"/>, with paired intervals, once a <see cref="Bootstrap"/> has computed it.</param>
internal sealed record StackResult(
    string Stack,
    EvaluationReport Report,
    IReadOnlyList<LabelRecall> Labels,
    IReadOnlyList<Miss> Misses,
    IReadOnlyList<FalsePositive> FalsePositives,
    IReadOnlyList<DocumentTally> Documents,
    ConfidenceIntervals? Intervals = null,
    string? Baseline = null,
    Gain? Gain = null)
{
    /// <summary>False positives counted by the category the stack gave them, most frequent first.</summary>
    public IReadOnlyList<(PhiCategory Category, int Count)> FalsePositivesByCategory =>
        [.. FalsePositives.GroupBy(f => f.Span.Category).Select(g => (g.Key, g.Count())).OrderByDescending(c => c.Item2).ThenBy(c => c.Key)];

    /// <summary>
    /// False positives counted by the detector (and recognizer) that reported them, most
    /// frequent first. A span several detectors agreed on counts once for each.
    /// </summary>
    public IReadOnlyList<(string Source, int Count)> FalsePositivesBySource =>
        [.. FalsePositives.SelectMany(f => f.Span.Sources).GroupBy(s => s, StringComparer.Ordinal)
            .Select(g => (g.Key, g.Count())).OrderByDescending(c => c.Item2).ThenBy(c => c.Key, StringComparer.Ordinal)];
}

/// <summary>Recall for the gold spans of one corpus label.</summary>
internal sealed record LabelRecall(string Label, int GoldSpans, int Detected)
{
    public double Recall => GoldSpans == 0 ? 1.0 : (double)Detected / GoldSpans;
}

/// <summary>A gold span the stack missed, with its document, for a person to read.</summary>
internal sealed record Miss(CorpusDocument Document, LabeledSpan Span);

/// <summary>A predicted span that is not gold PHI, with its document, for a person to read.</summary>
internal sealed record FalsePositive(CorpusDocument Document, PhiSpan Span);

/// <summary>One document's counts, with the group (the patient) that a resample keeps or drops as one.</summary>
internal sealed record DocumentTally(string Group, EvaluationTally Tally);
