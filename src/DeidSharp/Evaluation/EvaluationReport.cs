namespace DeidSharp.Evaluation;

/// <summary>How a detector did on an annotated corpus (see <see cref="DetectionEvaluator"/>).</summary>
/// <param name="CharacterRecall">Share of gold PHI characters covered by some prediction.</param>
/// <param name="CharacterPrecision">Share of predicted characters that are gold PHI.</param>
/// <param name="Words">Word-level counts, PHI or not, as Philter and pyclipse report them.</param>
/// <param name="NonPhiRemoved">Share of the non-whitespace characters outside gold PHI that a prediction covers: the clinical text lost.</param>
public sealed record EvaluationReport(
    DetectionMetrics Overall,
    double CharacterRecall,
    double CharacterPrecision,
    IReadOnlyDictionary<PhiCategory, CategoryRecall> PerCategory,
    WordMetrics Words,
    double NonPhiRemoved)
{
    /// <summary>
    /// Span-level counts under the strict (exact-boundary) match, categories aside, like the
    /// binary PHI-or-not evaluation of the 2014 i2b2/UTHealth challenge: a gold span is found
    /// only by a prediction with exactly its start and end, and a prediction is right only if it
    /// is exactly a gold span. A prediction that adds a title ("Dr. Hart" for "Hart") or cuts a
    /// name short is wrong here, though it removes the PHI. <see cref="DetectionEvaluator"/>
    /// always sets it; it is null only in a report constructed directly.
    /// </summary>
    public DetectionMetrics? Strict { get; init; }
}

/// <summary>Span-level counts, category-agnostic.</summary>
/// <param name="DetectedGoldSpans">Gold spans overlapped by at least one prediction (in <see cref="EvaluationReport.Strict"/>, matched exactly).</param>
/// <param name="CorrectPredictedSpans">Predictions overlapping at least one gold span (in <see cref="EvaluationReport.Strict"/>, matching one exactly).</param>
public sealed record DetectionMetrics(int GoldSpans, int DetectedGoldSpans, int PredictedSpans, int CorrectPredictedSpans)
{
    public double Recall => DetectionEvaluator.Ratio(DetectedGoldSpans, GoldSpans);

    public double Precision => DetectionEvaluator.Ratio(CorrectPredictedSpans, PredictedSpans);

    public double F1 => Recall + Precision == 0 ? 0 : 2 * Recall * Precision / (Recall + Precision);
}

/// <summary>
/// Word-level binary counts. A word is a run of letters or digits; it is PHI if any of its
/// characters is gold PHI, and predicted if a prediction covers any of its characters.
/// </summary>
/// <param name="PhiWords">Words that are gold PHI.</param>
/// <param name="PredictedWords">Words a prediction covers.</param>
/// <param name="CorrectWords">Words that are both.</param>
public sealed record WordMetrics(int PhiWords, int PredictedWords, int CorrectWords)
{
    public double Recall => DetectionEvaluator.Ratio(CorrectWords, PhiWords);

    public double Precision => DetectionEvaluator.Ratio(CorrectWords, PredictedWords);

    public double F1 => Recall + Precision == 0 ? 0 : 2 * Recall * Precision / (Recall + Precision);
}

/// <summary>Recall for the gold spans of one category.</summary>
/// <param name="DetectedWithCategory">Found by a prediction carrying the same category.</param>
public sealed record CategoryRecall(int GoldSpans, int Detected, int DetectedWithCategory)
{
    public double Recall => DetectionEvaluator.Ratio(Detected, GoldSpans);

    public double LabeledRecall => DetectionEvaluator.Ratio(DetectedWithCategory, GoldSpans);
}
