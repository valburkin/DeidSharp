using DeidSharp.Detection;
using DeidSharp.Evaluation;
using DeidSharp.Tests.TestDoubles;

namespace DeidSharp.Tests.Evaluation;

public class DetectionEvaluatorTests
{
    private const string Text = "Jane Doe seen 2024-03-05 by Dr Hart";

    // "Jane Doe" [0,8), "2024-03-05" [14,24), "Hart" [31,35)
    private static readonly AnnotatedText Document = new(Text,
    [
        new GoldSpan(0, 8, PhiCategory.Name),
        new GoldSpan(14, 24, PhiCategory.Date),
        new GoldSpan(31, 35, PhiCategory.Name),
    ]);

    private static PhiSpan P(int start, int end, PhiCategory category) => new(start, end, category, 1, "p");

    [Fact]
    public void Perfect_predictions_score_one_everywhere()
    {
        var report = DetectionEvaluator.Evaluate([(Document, [P(0, 8, PhiCategory.Name), P(14, 24, PhiCategory.Date), P(31, 35, PhiCategory.Name)])]);

        Assert.Equal(1, report.Overall.Recall);
        Assert.Equal(1, report.Overall.Precision);
        Assert.Equal(1, report.Overall.F1);
        Assert.Equal(1, report.CharacterRecall);
        Assert.Equal(1, report.CharacterPrecision);
        Assert.Equal(1, report.Strict!.F1);
    }

    [Fact]
    public void The_strict_match_needs_exactly_the_gold_span()
    {
        // "Jane Doe" exactly, the date one character short, and "Hart" with its title.
        var report = DetectionEvaluator.Evaluate([(Document, [P(0, 8, PhiCategory.Name), P(14, 23, PhiCategory.Date), P(28, 35, PhiCategory.Name)])]);

        Assert.Equal(1, report.Overall.Recall);
        Assert.Equal(new DetectionMetrics(3, 1, 3, 1), report.Strict);
        Assert.Equal(1.0 / 3, report.Strict!.Recall, 6);
        Assert.Equal(1.0 / 3, report.Strict.Precision, 6);
    }

    [Fact]
    public void The_strict_match_ignores_the_category_and_counts_a_repeated_prediction_once_per_gold_span()
    {
        var report = DetectionEvaluator.Evaluate([(Document, [P(0, 8, PhiCategory.Organization), P(0, 8, PhiCategory.Name)])]);

        Assert.Equal(new DetectionMetrics(3, 1, 2, 2), report.Strict);
    }

    [Fact]
    public void One_prediction_over_two_gold_spans_matches_neither_strictly()
    {
        var report = DetectionEvaluator.Evaluate([(Document, [P(0, 24, PhiCategory.Name)])]);

        Assert.Equal(new DetectionMetrics(3, 2, 1, 1), report.Overall);
        Assert.Equal(new DetectionMetrics(3, 0, 1, 0), report.Strict);
    }

    [Fact]
    public void A_missed_span_lowers_recall_and_shows_in_its_category()
    {
        var report = DetectionEvaluator.Evaluate([(Document, [P(0, 8, PhiCategory.Name), P(14, 24, PhiCategory.Date)])]);

        Assert.Equal(2.0 / 3, report.Overall.Recall, 6);
        Assert.Equal(0.5, report.PerCategory[PhiCategory.Name].Recall);
        Assert.Equal(1, report.PerCategory[PhiCategory.Date].Recall);
    }

    [Fact]
    public void A_partial_hit_counts_as_found_but_character_recall_shows_the_gap()
    {
        var report = DetectionEvaluator.Evaluate([(Document, [P(0, 4, PhiCategory.Name), P(14, 24, PhiCategory.Date), P(31, 35, PhiCategory.Name)])]);

        Assert.Equal(1, report.Overall.Recall);
        Assert.Equal((4.0 + 10 + 4) / (8 + 10 + 4), report.CharacterRecall, 6);
    }

    [Fact]
    public void A_false_positive_lowers_precision_not_recall()
    {
        var report = DetectionEvaluator.Evaluate([(Document, [P(0, 8, PhiCategory.Name), P(14, 24, PhiCategory.Date), P(31, 35, PhiCategory.Name), P(9, 13, PhiCategory.Name)])]);

        Assert.Equal(1, report.Overall.Recall);
        Assert.Equal(0.75, report.Overall.Precision);
        Assert.True(report.CharacterPrecision < 1);
    }

    [Fact]
    public void Labeled_recall_needs_the_right_category()
    {
        var report = DetectionEvaluator.Evaluate([(Document, [P(0, 8, PhiCategory.Organization), P(14, 24, PhiCategory.Date), P(31, 35, PhiCategory.Name)])]);

        Assert.Equal(1, report.PerCategory[PhiCategory.Name].Recall);
        Assert.Equal(0.5, report.PerCategory[PhiCategory.Name].LabeledRecall);
    }

    [Fact]
    public void Nothing_to_find_and_nothing_predicted_is_perfect_not_undefined()
    {
        var report = DetectionEvaluator.Evaluate([(new AnnotatedText("BP 120/80", []), [])]);
        Assert.Equal(1, report.Overall.Recall);
        Assert.Equal(1, report.Overall.Precision);
        Assert.Equal(1, report.CharacterRecall);
        Assert.Empty(report.PerCategory);
    }

    [Fact]
    public void Words_count_as_phi_and_predicted_if_any_of_their_characters_is()
    {
        // Words: Jane Doe seen 2024 03 05 by Dr Hart; PHI: Jane Doe 2024 03 05 Hart.
        var report = DetectionEvaluator.Evaluate([(Document, [P(0, 2, PhiCategory.Name), P(9, 13, PhiCategory.Name)])]);

        Assert.Equal(new WordMetrics(PhiWords: 6, PredictedWords: 2, CorrectWords: 1), report.Words);
        Assert.Equal(1.0 / 6, report.Words.Recall, 6);
        Assert.Equal(0.5, report.Words.Precision);
    }

    [Fact]
    public void Non_phi_removed_is_the_share_of_other_non_whitespace_text_covered()
    {
        // Outside gold, "seen", "by" and "Dr" hold 8 non-whitespace characters; "seen" is removed.
        var report = DetectionEvaluator.Evaluate([(Document, [P(0, 8, PhiCategory.Name), P(9, 13, PhiCategory.Name)])]);

        Assert.Equal(0.5, report.NonPhiRemoved);
        Assert.Equal(0, DetectionEvaluator.Evaluate([(Document, [])]).NonPhiRemoved);
    }

    [Fact]
    public void Tallies_add_up_to_the_corpus_report()
    {
        var second = new AnnotatedText("Call Ann at 555-0100", [new GoldSpan(5, 8, PhiCategory.Name), new GoldSpan(12, 20, PhiCategory.Phone)]);
        List<PhiSpan> first = [P(0, 8, PhiCategory.Name), P(9, 13, PhiCategory.Name)];
        List<PhiSpan> other = [P(12, 20, PhiCategory.Phone)];

        var whole = DetectionEvaluator.Evaluate([(Document, first), (second, other)]);
        var summed = DetectionEvaluator.Tally(Document, first).Add(DetectionEvaluator.Tally(second, other)).ToReport();

        Assert.Equal(whole.Overall, summed.Overall);
        Assert.Equal(new DetectionMetrics(5, 2, 3, 2), whole.Strict);
        Assert.Equal(whole.Strict, summed.Strict);
        Assert.Equal(whole.Words, summed.Words);
        Assert.Equal(whole.CharacterRecall, summed.CharacterRecall);
        Assert.Equal(whole.NonPhiRemoved, summed.NonPhiRemoved);
        Assert.Equal(whole.PerCategory[PhiCategory.Name], summed.PerCategory[PhiCategory.Name]);
        Assert.Equal(whole.Overall, EvaluationTally.Empty.Add(DetectionEvaluator.Tally(Document, first)).Add(DetectionEvaluator.Tally(second, other)).ToReport().Overall);
    }

    [Fact]
    public void F1_is_zero_when_nothing_is_right()
    {
        var report = DetectionEvaluator.Evaluate([(Document, [P(9, 13, PhiCategory.Name)])]);
        Assert.Equal(0, report.Overall.F1);
    }

    [Fact]
    public void A_prediction_outside_the_text_is_rejected() =>
        Assert.Throws<PhiDetectionException>(() => DetectionEvaluator.Evaluate([(Document, [P(0, 99, PhiCategory.Name)])]));

    [Fact]
    public void Gold_spans_must_fit_their_text()
    {
        Assert.Throws<ArgumentException>(() => new AnnotatedText("abc", [new GoldSpan(0, 4, PhiCategory.Name)]));
        Assert.Throws<ArgumentException>(() => new AnnotatedText("abc", [new GoldSpan(2, 2, PhiCategory.Name)]));
        Assert.Throws<ArgumentException>(() => new AnnotatedText("abc", [new GoldSpan(-1, 2, PhiCategory.Name)]));
    }

    [Fact]
    public async Task Runs_a_detector_over_a_corpus()
    {
        var detector = new StubDetector("stub", P(0, 8, PhiCategory.Name));
        var report = await DetectionEvaluator.EvaluateAsync(detector, [Document, Document], TestContext.Current.CancellationToken);

        Assert.Equal(6, report.Overall.GoldSpans);
        Assert.Equal(2, report.Overall.DetectedGoldSpans);
        Assert.Equal(2, detector.Calls);
    }
}
