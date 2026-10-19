using DeidSharp.Evaluation.Cli.Corpora;

namespace DeidSharp.Evaluation.Cli.Scoring;

/// <summary>
/// Runs one stack over a corpus and scores it with <see cref="DetectionEvaluator"/>, keeping
/// each document's tally (for confidence intervals), the misses and the recall by corpus
/// label as well.
/// </summary>
internal static class StackScorer
{
    public static async Task<StackResult> ScoreAsync(
        IScoredStack stack,
        ICorpus corpus,
        IReadOnlyList<CorpusDocument> documents,
        EvaluationScope scope,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stack);
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(documents);

        var tallies = new List<DocumentTally>(documents.Count);
        var labels = new Dictionary<string, (int Gold, int Detected)>(StringComparer.Ordinal);
        var misses = new List<Miss>();
        var falsePositives = new List<FalsePositive>();
        foreach (var document in documents)
        {
            var predicted = await stack.PredictAsync(document, cancellationToken);
            var annotated = corpus.Labels.ToAnnotated(document, scope);
            tallies.Add(new DocumentTally(document.SplitGroup, DetectionEvaluator.Tally(annotated, predicted)));

            // The evaluator's rule both ways: a gold span is found, and a prediction correct,
            // if the two overlap at all.
            falsePositives.AddRange(predicted.Where(p => !annotated.Gold.Any(g => Overlap(g.Start, g.End, p))).Select(p => new FalsePositive(document, p)));
            foreach (var span in document.Spans.Where(s => corpus.Labels.Counts(s.Label, scope)))
            {
                var found = predicted.Any(p => Overlap(span.Start, span.End, p));
                var (gold, detected) = labels.GetValueOrDefault(span.Label);
                labels[span.Label] = (gold + 1, detected + (found ? 1 : 0));
                if (!found)
                {
                    misses.Add(new Miss(document, span));
                }
            }

            progress?.Report(tallies.Count);
        }

        return new StackResult(
            stack.Name,
            EvaluationTally.Sum(tallies.Select(t => t.Tally)).ToReport(),
            [.. labels.OrderByDescending(kv => kv.Value.Gold).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => new LabelRecall(kv.Key, kv.Value.Gold, kv.Value.Detected))],
            misses,
            falsePositives,
            tallies,
            Baseline: stack.Baseline);
    }

    private static bool Overlap(int start, int end, PhiSpan predicted) => predicted.Start < end && start < predicted.End;
}
