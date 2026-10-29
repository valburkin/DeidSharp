namespace DeidSharp.Evaluation;

/// <summary>
/// The counts behind an <see cref="EvaluationReport"/>, for one document or many. Tallies add
/// up: a corpus's tally is the sum of its documents', so a caller can resample documents (a
/// bootstrap for confidence intervals) without running detection again. Get one document's
/// from <see cref="DetectionEvaluator.Tally"/>.
/// </summary>
public sealed class EvaluationTally
{
    private readonly IReadOnlyDictionary<PhiCategory, CategoryRecall> _perCategory;

    internal EvaluationTally(DetectionMetrics spans, DetectionMetrics strict, Characters characters, WordMetrics words, IReadOnlyDictionary<PhiCategory, CategoryRecall> perCategory)
    {
        Spans = spans;
        Strict = strict;
        Chars = characters;
        Words = words;
        _perCategory = perCategory;
    }

    public static EvaluationTally Empty { get; } = new(new DetectionMetrics(0, 0, 0, 0), new DetectionMetrics(0, 0, 0, 0), default, new WordMetrics(0, 0, 0), new Dictionary<PhiCategory, CategoryRecall>());

    private DetectionMetrics Spans { get; }

    private DetectionMetrics Strict { get; }

    private Characters Chars { get; }

    private WordMetrics Words { get; }

    public static EvaluationTally Sum(IEnumerable<EvaluationTally> tallies)
    {
        ArgumentNullException.ThrowIfNull(tallies);
        int gold = 0, detected = 0, predicted = 0, correct = 0, strictDetected = 0, strictCorrect = 0, phiWords = 0, predictedWords = 0, correctWords = 0;
        var characters = default(Characters);
        var perCategory = new Dictionary<PhiCategory, CategoryRecall>();
        foreach (var tally in tallies)
        {
            ArgumentNullException.ThrowIfNull(tally);
            gold += tally.Spans.GoldSpans;
            detected += tally.Spans.DetectedGoldSpans;
            predicted += tally.Spans.PredictedSpans;
            correct += tally.Spans.CorrectPredictedSpans;
            strictDetected += tally.Strict.DetectedGoldSpans;
            strictCorrect += tally.Strict.CorrectPredictedSpans;
            characters += tally.Chars;
            phiWords += tally.Words.PhiWords;
            predictedWords += tally.Words.PredictedWords;
            correctWords += tally.Words.CorrectWords;
            foreach (var (category, recall) in tally._perCategory)
            {
                var sum = perCategory.GetValueOrDefault(category) ?? new CategoryRecall(0, 0, 0);
                perCategory[category] = new CategoryRecall(
                    sum.GoldSpans + recall.GoldSpans, sum.Detected + recall.Detected, sum.DetectedWithCategory + recall.DetectedWithCategory);
            }
        }

        return new EvaluationTally(
            new DetectionMetrics(gold, detected, predicted, correct),
            new DetectionMetrics(gold, strictDetected, predicted, strictCorrect),
            characters, new WordMetrics(phiWords, predictedWords, correctWords), perCategory);
    }

    public EvaluationTally Add(EvaluationTally other) => Sum([this, other]);

    public EvaluationReport ToReport() => new(
        Spans,
        DetectionEvaluator.Ratio(Chars.Overlap, Chars.Gold),
        DetectionEvaluator.Ratio(Chars.Overlap, Chars.Predicted),
        _perCategory,
        Words,
        Chars.NonPhi == 0 ? 0 : (double)Chars.RemovedNonPhi / Chars.NonPhi)
    {
        Strict = Strict,
    };

    /// <summary>Character counts: gold, predicted, both, and the non-whitespace characters outside gold (and how many of those were removed).</summary>
    internal readonly record struct Characters(long Gold, long Predicted, long Overlap, long NonPhi, long RemovedNonPhi)
    {
        public static Characters operator +(Characters a, Characters b) =>
            new(a.Gold + b.Gold, a.Predicted + b.Predicted, a.Overlap + b.Overlap, a.NonPhi + b.NonPhi, a.RemovedNonPhi + b.RemovedNonPhi);
    }
}
