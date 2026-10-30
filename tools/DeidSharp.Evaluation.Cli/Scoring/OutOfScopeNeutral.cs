using DeidSharp.Evaluation.Cli.Corpora;

namespace DeidSharp.Evaluation.Cli.Scoring;

// Design pattern: Decorator.
// The pattern: a wrapper with the same interface as the object it wraps, adding behavior
// before or after passing each call through, so callers can't tell the two apart.
// Here: it is an IScoredStack around an IScoredStack, a DeidSharp stack or another tool's
// file alike; what it adds is leaving out the predictions that touch only gold the scope
// doesn't count (a bare year, an age under 90), so tagging them counts neither for a tool
// nor against it.

/// <summary>
/// Leaves out each prediction that overlaps a gold span outside the scope and none inside it.
/// Recall and character recall don't change; precision, the word measures and the non-PHI
/// removed no longer count those predictions. A sensitivity check, never for publishing.
/// </summary>
internal sealed class OutOfScopeNeutral(IScoredStack inner, LabelMap labels, EvaluationScope scope) : IScoredStack
{
    public string Name => inner.Name;

    public string? Baseline => inner.Baseline;

    public async Task<IReadOnlyList<PhiSpan>> PredictAsync(CorpusDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        var predicted = await inner.PredictAsync(document, cancellationToken);
        var outside = document.Spans.Where(s => !labels.Counts(s.Label, scope)).ToList();
        if (outside.Count == 0)
        {
            return predicted;
        }

        var inside = document.Spans.Where(s => labels.Counts(s.Label, scope)).ToList();
        return [.. predicted.Where(p => inside.Any(g => Overlap(g, p)) || !outside.Any(g => Overlap(g, p)))];
    }

    private static bool Overlap(LabeledSpan gold, PhiSpan predicted) => predicted.Start < gold.End && gold.Start < predicted.End;
}
