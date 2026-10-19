using DeidSharp.Detection;

namespace DeidSharp.Evaluation;

/// <summary>
/// Measures a detector (or a whole stack) against annotated text, the way
/// de-identification research reports it. Recall is what matters most: a missed span
/// is leaked PHI, while a false positive only costs some clinical text.
///
/// A gold span counts as found if any predicted span overlaps it, whatever category
/// the prediction carries ("binary" or "hiding" recall: was it removed at all?).
/// Character recall says how much of the PHI text was covered; a partly covered name can
/// still leak. Per-category numbers also report labeled recall (found with the right
/// category), which matters for surrogates. Word-level counts (PHI or not, as Philter and
/// pyclipse report them) and the share of other text removed make the numbers comparable
/// with published ones.
/// </summary>
public static class DetectionEvaluator
{
    public static async Task<EvaluationReport> EvaluateAsync(
        IPhiDetector detector, IEnumerable<AnnotatedText> corpus, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(corpus);
        var cases = new List<(AnnotatedText, IReadOnlyList<PhiSpan>)>();
        foreach (var document in corpus)
        {
            var spans = await detector.DetectAsync(new DetectionRequest(document.Text), cancellationToken).ConfigureAwait(false);
            cases.Add((document, spans));
        }

        return Evaluate(cases);
    }

    public static EvaluationReport Evaluate(IEnumerable<(AnnotatedText Document, IReadOnlyList<PhiSpan> Predicted)> cases)
    {
        ArgumentNullException.ThrowIfNull(cases);
        return EvaluationTally.Sum(cases.Select(c => Tally(c.Document, c.Predicted))).ToReport();
    }

    /// <summary>One document's counts. Tallies add up, so <see cref="EvaluationTally.Sum"/> of a corpus's documents gives its report.</summary>
    public static EvaluationTally Tally(AnnotatedText document, IReadOnlyList<PhiSpan> predicted)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(predicted);
        SpanMerger.EnsureWithin(document.Text, predicted, "evaluated detector");

        var detected = 0;
        var perCategory = new Dictionary<PhiCategory, CategoryRecall>();
        foreach (var g in document.Gold)
        {
            var overlapping = predicted.Where(p => Overlap(g, p)).ToList();
            var found = overlapping.Count > 0;
            detected += found ? 1 : 0;

            var entry = perCategory.GetValueOrDefault(g.Category) ?? new CategoryRecall(0, 0, 0);
            perCategory[g.Category] = new CategoryRecall(
                entry.GoldSpans + 1,
                entry.Detected + (found ? 1 : 0),
                entry.DetectedWithCategory + (overlapping.Any(p => p.Category == g.Category) ? 1 : 0));
        }

        var correct = predicted.Count(p => document.Gold.Any(g => Overlap(g, p)));
        var spans = new DetectionMetrics(document.Gold.Count, detected, predicted.Count, correct);

        var text = document.Text;
        var goldMask = Mask(text.Length, document.Gold.Select(g => (g.Start, g.End)));
        var predictedMask = Mask(text.Length, predicted.Select(p => (p.Start, p.End)));
        long goldChars = 0, predictedChars = 0, overlapChars = 0, nonPhi = 0, removedNonPhi = 0;

        for (var i = 0; i < text.Length; i++)
        {
            goldChars += goldMask[i] ? 1 : 0;
            predictedChars += predictedMask[i] ? 1 : 0;
            overlapChars += goldMask[i] && predictedMask[i] ? 1 : 0;
            if (goldMask[i] || char.IsWhiteSpace(text[i]))
            {
                continue;
            }

            nonPhi++;
            removedNonPhi += predictedMask[i] ? 1 : 0;
        }

        return new EvaluationTally(
            spans,
            new EvaluationTally.Characters(goldChars, predictedChars, overlapChars, nonPhi, removedNonPhi),
            Words(text, goldMask, predictedMask),
            perCategory);
    }

    // A word is a run of letters or digits: PHI if any of its characters is, predicted if any is covered.
    private static WordMetrics Words(string text, bool[] goldMask, bool[] predictedMask)
    {
        int phi = 0, predicted = 0, correct = 0;
        for (var start = 0; start < text.Length; start++)
        {
            if (!char.IsLetterOrDigit(text[start]))
            {
                continue;
            }

            var end = start;
            bool isPhi = false, isPredicted = false;
            for (; end < text.Length && char.IsLetterOrDigit(text[end]); end++)
            {
                isPhi |= goldMask[end];
                isPredicted |= predictedMask[end];
            }

            phi += isPhi ? 1 : 0;
            predicted += isPredicted ? 1 : 0;
            correct += isPhi && isPredicted ? 1 : 0;
            start = end;
        }

        return new WordMetrics(phi, predicted, correct);
    }

    private static bool Overlap(GoldSpan gold, PhiSpan predicted) => predicted.Start < gold.End && gold.Start < predicted.End;

    private static bool[] Mask(int length, IEnumerable<(int Start, int End)> spans)
    {
        var mask = new bool[length];
        foreach (var (start, end) in spans)
        {
            Array.Fill(mask, true, start, end - start);
        }

        return mask;
    }

    // Nothing to find counts as everything found; nothing predicted counts as nothing wrong.
    internal static double Ratio(long numerator, long denominator) => denominator == 0 ? 1.0 : (double)numerator / denominator;
}
