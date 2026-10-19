using DeidSharp.Detection;
using DeidSharp.Detection.Patterns;
using DeidSharp.Pseudonymization;
using DeidSharp.Structured;
using DeidSharp.Tests.TestDoubles;
using DeidSharp.Text;
using DeidSharp.Verification;

namespace DeidSharp.Tests.Text;

public class TextDeidentifierTests
{
    private static readonly CompositePhiDetector Deterministic = new(DeterministicDetectors.Create());

    private static TextDeidentifier Tags(IPhiDetector? detector = null, TextDeidentifierOptions? options = null) =>
        new(detector ?? Deterministic, new TagReplacement(), options);

    private static Task<TextDeidentificationResult> Run(TextDeidentifier deidentifier, string text, TextContext? context = null) =>
        deidentifier.DeidentifyAsync(text, context, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Empty_text_comes_back_empty()
    {
        var result = await Run(Tags(), "");
        Assert.Equal("", result.Text);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public async Task Text_without_phi_comes_back_unchanged()
    {
        const string text = "BP 120/80, HR 88. Creatinine 2.1 mg/dL. Continue lisinopril 10 mg daily.";
        var result = await Run(Tags(), text);
        Assert.Equal(text, result.Text);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public async Task Replaces_every_span_and_records_where_each_replacement_went()
    {
        const string text = "Jane Doe (MRN: 12345678) was seen on March 5, 2024.";
        var context = new TextContext("p", KnownIdentifiers.Empty.AddName("Jane Doe"));

        var result = await Run(Tags(), text, context);

        Assert.Equal("[NAME] (MRN: [MRN]) was seen on [DATE].", result.Text);
        Assert.All(result.Findings, f => Assert.Equal('[', result.Text[f.OutputStart]));
        Assert.All(result.Findings, f => Assert.Equal(']', result.Text[f.OutputEnd - 1]));
        Assert.Equal(["Jane Doe", "12345678", "March 5, 2024"], result.Findings.Select(f => text[f.InputStart..f.InputEnd]));
        Assert.Equal([PhiCategory.Name, PhiCategory.MedicalRecordNumber, PhiCategory.Date], result.Findings.Select(f => f.Category));
        Assert.All(result.Findings, f => Assert.InRange(f.Score, 0.5, 1.0));
    }

    [Fact]
    public async Task Overlapping_findings_from_different_detectors_become_one_replacement()
    {
        var a = new StubDetector("a", new PhiSpan(0, 4, PhiCategory.Name, 0.7, "a"));
        var b = new StubDetector("b", new PhiSpan(2, 7, PhiCategory.Name, 0.9, "b"));

        var result = await Run(Tags(new CompositePhiDetector([a, b])), "John Q. Public");

        Assert.Equal("[NAME] Public", result.Text);
        Assert.Equal(["a", "b"], Assert.Single(result.Findings).Sources);
    }

    [Fact]
    public async Task A_bare_detector_that_returns_overlaps_is_merged_too()
    {
        var detector = new StubDetector("x", new PhiSpan(0, 4, PhiCategory.Name, 1, "x"), new PhiSpan(2, 6, PhiCategory.Name, 1, "x"));
        var result = await Run(Tags(detector), "abcdefgh");
        Assert.Equal("[NAME]gh", result.Text);
    }

    [Fact]
    public async Task Spans_below_the_minimum_score_are_left_in_place()
    {
        var detector = new StubDetector("x", new PhiSpan(0, 3, PhiCategory.Name, 0.2, "x"), new PhiSpan(4, 7, PhiCategory.Name, 0.9, "x"));
        var result = await Run(Tags(detector, new TextDeidentifierOptions { MinimumScore = 0.5 }), "Ann Bob");
        Assert.Equal("Ann [NAME]", result.Text);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void Rejects_a_minimum_score_outside_zero_to_one(double score) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Tags(options: new TextDeidentifierOptions { MinimumScore = score }));

    [Fact]
    public async Task A_failing_detector_stops_de_identification()
    {
        var deidentifier = Tags(new CompositePhiDetector([new FailingDetector("presidio", new HttpRequestException())]));
        await Assert.ThrowsAsync<PhiDetectionException>(() => Run(deidentifier, "Jane Doe"));
    }

    [Fact]
    public async Task A_lone_failing_detector_also_stops_with_a_detection_error()
    {
        var deidentifier = Tags(new FailingDetector("presidio", new HttpRequestException()));
        var ex = await Assert.ThrowsAsync<PhiDetectionException>(() => Run(deidentifier, "Jane Doe"));
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task A_detector_returning_null_or_out_of_range_spans_stops_de_identification()
    {
        await Assert.ThrowsAsync<PhiDetectionException>(() => Run(Tags(new FailingDetector("null")), "Jane"));
        await Assert.ThrowsAsync<PhiDetectionException>(() => Run(Tags(new StubDetector("far", new PhiSpan(0, 50, PhiCategory.Name, 1, "far"))), "Jane"));
    }

    [Fact]
    public async Task A_known_identifier_left_in_the_output_is_caught_by_the_output_check()
    {
        // A stack without the known-identifier detector misses the bare surname; the
        // output check still knows it and refuses the result.
        var deidentifier = Tags(new PatternPhiDetector());
        var context = new TextContext("p", KnownIdentifiers.Empty.AddName("Rosa Alvarez"));

        var ex = await Assert.ThrowsAsync<ResidualPhiException>(() => Run(deidentifier, "Alvarez reports fatigue", context));

        Assert.DoesNotContain("Alvarez", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(PhiCategory.Name, Assert.Single(ex.Report.Findings).Category);
    }

    [Fact]
    public async Task The_output_check_can_be_turned_off()
    {
        var deidentifier = Tags(new PatternPhiDetector(), new TextDeidentifierOptions { VerifyOutput = false });
        var context = new TextContext("p", KnownIdentifiers.Empty.AddName("Rosa Alvarez"));
        Assert.Equal("Alvarez reports fatigue", (await Run(deidentifier, "Alvarez reports fatigue", context)).Text);
    }

    [Fact]
    public async Task Replacing_next_to_an_emoji_never_splits_a_character()
    {
        const string text = "\U0001F600Jo\U0001F600";
        var detector = new StubDetector("x", new PhiSpan(2, 4, PhiCategory.Name, 1, "x"), new PhiSpan(5, 6, PhiCategory.Name, 1, "x"));

        var result = await Run(Tags(detector), text);

        Assert.Equal("\U0001F600[NAME][NAME]", result.Text);
        Assert.False(Utf16.HasLoneSurrogate(result.Text));
    }

    [Fact]
    public async Task A_realistic_note_loses_every_known_and_patterned_identifier()
    {
        const string note = """
            Discharge summary for Rosa Maria Alvarez-Nunez (MRN: 4471-882-19), DOB 03/04/1931.
            Mrs. Alvarez-Nunez is a 93-year-old woman admitted to St. Brendan Hospital on March 2, 2024.
            Her daughter Carmen called from (617) 555-0142; email rosa.alvarez@mailbox.example.
            Home: 48 Willow Creek Road, Springfield, MA 01103. SSN 123-45-6789.
            Seen by Dr. Amelia Hart. Follow up with Dr Hart in 2 weeks. BP 128/76, creatinine 2.1.
            """;
        var known = KnownIdentifiers.Empty
            .AddName("Rosa María Álvarez-Núñez").AddName("Carmen Alvarez").AddName("Amelia Hart")
            .AddIdentifier("4471-882-19", PhiCategory.MedicalRecordNumber).AddDate(new DateOnly(1931, 3, 4))
            .AddOrganization("St. Brendan Hospital").AddStreetAddress("48 Willow Creek Rd").AddLocation("Springfield");

        var result = await Run(Tags(), note, new TextContext("p", known));

        foreach (var secret in new[] { "Rosa", "Alvarez", "Nunez", "4471", "1931", "93", "Brendan", "March 2", "Carmen", "555-0142",
                     "rosa.alvarez", "Willow", "Springfield", "01103", "123-45-6789", "Amelia", "Hart" })
        {
            Assert.DoesNotContain(secret, result.Text, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("BP 128/76, creatinine 2.1", result.Text, StringComparison.Ordinal);
        Assert.Contains(", MA [ZIP]", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Running_it_twice_changes_nothing_more()
    {
        const string text = "Jane Doe, SSN 123-45-6789, seen 2024-03-05 at 48 Willow Creek Road.";
        var context = new TextContext("p", KnownIdentifiers.Empty.AddName("Jane Doe"));

        var once = await Run(Tags(), text, context);
        var twice = await Run(Tags(), once.Text, context);

        Assert.Equal(once.Text, twice.Text);
        Assert.Empty(twice.Findings);
    }

    [Fact]
    public async Task Surrogates_read_as_text_and_pass_the_output_check()
    {
        var pseudonymizer = new HmacPseudonymizer(TestKeys.Fixed);
        var deidentifier = new TextDeidentifier(Deterministic, new SurrogateReplacement(pseudonymizer, YearOnlyDateTransformer.Instance));
        var context = new TextContext("p", KnownIdentifiers.Empty.AddName("Jane Doe").AddPhone("617-555-0198"));

        var result = await Run(deidentifier, "Jane Doe called from 617-555-0198 on 2024-03-05.", context);

        Assert.DoesNotContain("[", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Jane", result.Text, StringComparison.Ordinal);
        Assert.Contains("(555) 555-01", result.Text, StringComparison.Ordinal);
        Assert.EndsWith("on 2024.", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_missing_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new TextDeidentifier(null!, new TagReplacement()));
        Assert.Throws<ArgumentNullException>(() => new TextDeidentifier(Deterministic, null!));
    }

    [Fact]
    public async Task A_canceled_call_stops_instead_of_returning_text()
    {
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();
        var deidentifier = new TextDeidentifier(Deterministic, new TagReplacement());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            deidentifier.DeidentifyAsync("Rosa Alvarez called.", new TextContext("p", KnownIdentifiers.Empty.AddName("Rosa Alvarez")), canceled.Token));
    }
}
