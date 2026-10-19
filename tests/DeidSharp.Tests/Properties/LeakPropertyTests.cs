using System.Globalization;
using CsCheck;
using DeidSharp.Detection;
using DeidSharp.Detection.Known;
using DeidSharp.Pseudonymization;
using DeidSharp.Structured;
using DeidSharp.Tests.TestDoubles;
using DeidSharp.Tests.Text;
using DeidSharp.Text;

namespace DeidSharp.Tests.Properties;

/// <summary>
/// Invariants checked over thousands of generated inputs instead of a few examples.
/// CsCheck shrinks any failure to the smallest input that still breaks the invariant.
/// </summary>
public class LeakPropertyTests
{
    private static readonly CompositePhiDetector Deterministic = new(DeterministicDetectors.Create());

    private static readonly string[] Templates =
    [
        "Patient {0} was seen today.",
        "{0}",
        "Per {0}: BP 120/80, HR 72.",
        "Called {0}; no answer. Pain 4/10.",
        "({0})",
        "Follow-up with {0} in 2 weeks, creatinine 1.4.",
        "{0}'s chart reviewed.",
        "Notes: {0}, {0} again.",
    ];

    private static readonly Gen<string> Word = Gen.Char['a', 'z'].Array[2, 9].Select(c => char.ToUpperInvariant(c[0]) + new string(c, 1, c.Length - 1));

    private static readonly Gen<string> Separator = Gen.OneOfConst("-", " ", ".", "");

    // Identifiers as they are recorded, with the category they are known under.
    private static readonly Gen<(PhiCategory Category, string Value)> Identifier = Gen.OneOf(
        Word.Select(Word, (a, b) => (PhiCategory.Name, $"{a} {b}")),
        Gen.Int[100, 999].Select(Gen.Int[100, 999], Gen.Int[1000, 9999], Separator, (a, b, c, s) => (PhiCategory.Phone, $"{a}{s}{b}{s}{c}")),
        Gen.Long[1000, 99_999_999].Select(v => (PhiCategory.MedicalRecordNumber, v.ToString(CultureInfo.InvariantCulture))),
        Gen.Int[1, 899].Select(Gen.Int[1, 99], Gen.Int[1, 9999], (a, b, c) => (PhiCategory.Ssn, $"{a:D3}-{b:D2}-{c:D4}")),
        Word.Select(Word, (a, b) => (PhiCategory.Email, $"{a.ToLowerInvariant()}.{b.ToLowerInvariant()}@example.net")),
        Gen.Int[1, 9999].Select(Word, Gen.OneOfConst("Street", "Road", "Lane", "Avenue"), (n, w, s) => (PhiCategory.StreetAddress, $"{n} {w} {s}")));

    private static readonly Gen<DateOnly> Date = Gen.Int[1900, 2030].Select(Gen.Int[1, 12], Gen.Int[1, 28], (y, m, d) => new DateOnly(y, m, d));

    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd", "M/d/yyyy", "MM/dd/yyyy", "MMMM d, yyyy", "d MMMM yyyy", "MMM d yyyy", "yyyyMMdd", "dd.MM.yyyy",
        "dd-MMM-yyyy", "ddMMMyyyy", "yyyy-MMM-dd", "MMM-dd-yyyy", "d-MMMM-yyyy", "dd-MMM-yy",
    ];

    [Fact]
    public void No_known_identifier_survives_de_identification()
    {
        var deidentifier = new TextDeidentifier(Deterministic, new TagReplacement());
        Identifier.Select(Gen.Int[0, Templates.Length - 1]).Sample((id, template) =>
        {
            var known = KnownIdentifiers.Empty.Add(id.Category, id.Value);
            var text = string.Format(CultureInfo.InvariantCulture, Templates[template], id.Value);

            var result = deidentifier.DeidentifyAsync(text, new TextContext("p", known)).GetAwaiter().GetResult();

            return !result.Text.Contains(id.Value, StringComparison.OrdinalIgnoreCase)
                && new KnownIdentifierDetector().Detect(new DetectionRequest(result.Text, known)).Count == 0;
        }, iter: 3000);
    }

    [Fact]
    public void A_known_date_never_survives_in_any_common_format()
    {
        var deidentifier = new TextDeidentifier(Deterministic, new TagReplacement());
        Date.Select(Gen.Int[0, DateFormats.Length - 1]).Sample((date, format) =>
        {
            var written = date.ToString(DateFormats[format], CultureInfo.InvariantCulture);
            var known = KnownIdentifiers.Empty.AddDate(date);
            var result = deidentifier.DeidentifyAsync($"DOB {written}, seen today.", new TextContext("p", known)).GetAwaiter().GetResult();
            return !result.Text.Contains(written, StringComparison.OrdinalIgnoreCase);
        }, iter: 2000);
    }

    [Fact]
    public void Patterned_identifiers_are_removed_even_when_nothing_is_known()
    {
        var deidentifier = new TextDeidentifier(Deterministic, new TagReplacement());
        var patterned = Gen.OneOf(
            Gen.Int[1, 899].Select(Gen.Int[1, 99], Gen.Int[1, 9999], (a, b, c) => $"{a:D3}-{b:D2}-{c:D4}"),
            Gen.Int[200, 999].Select(Gen.Int[200, 999], Gen.Int[0, 9999], (a, b, c) => $"({a}) {b}-{c:D4}"),
            Word.Select(Word, (a, b) => $"{a}.{b}@example.org".ToLowerInvariant()),
            Date.Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            Gen.Long[10_000, 99_999_999].Select(n => $"MRN: {n}"));
        patterned.Sample(value =>
        {
            var result = deidentifier.DeidentifyAsync($"Note: {value} recorded.").GetAwaiter().GetResult();
            var core = value.StartsWith("MRN: ", StringComparison.Ordinal) ? value[5..] : value;
            return !result.Text.Contains(core, StringComparison.OrdinalIgnoreCase);
        }, iter: 3000);
    }

    [Fact]
    public void Tag_de_identification_is_idempotent()
    {
        var deidentifier = new TextDeidentifier(Deterministic, new TagReplacement());
        Identifier.Select(Gen.Int[0, Templates.Length - 1]).Sample((id, template) =>
        {
            var context = new TextContext("p", KnownIdentifiers.Empty.Add(id.Category, id.Value));
            var text = string.Format(CultureInfo.InvariantCulture, Templates[template], id.Value);
            var once = deidentifier.DeidentifyAsync(text, context).GetAwaiter().GetResult().Text;
            var twice = deidentifier.DeidentifyAsync(once, context).GetAwaiter().GetResult().Text;
            return once == twice;
        }, iter: 1000);
    }

    [Fact]
    public void Folding_never_changes_the_length_of_any_string() =>
        Gen.String.Sample(s => TextFolding.Fold(s).Length == s.Length, iter: 5000);

    [Fact]
    public void Merged_spans_are_ordered_disjoint_and_cover_every_input_span()
    {
        const string text = "abcdefghijklmnopqrstuvwxyz0123456789abcdefghijklmnopqrstuvwxyz";
        var span = Gen.Int[0, text.Length - 1].Select(Gen.Int[1, 10], Gen.Enum<PhiCategory>(), Gen.Double[0, 1],
            (start, length, category, score) => new PhiSpan(start, Math.Min(text.Length, start + length), category, score, "g"));
        span.List[0, 30].Sample(spans =>
        {
            var merged = SpanMerger.Merge(text, spans);
            var ordered = merged.Zip(merged.Skip(1)).All(p => p.First.End <= p.Second.Start);
            var covered = spans.All(s => merged.Any(m => m.Start <= s.Start && s.End <= m.End));
            return ordered && covered;
        }, iter: 3000);
    }

    [Fact]
    public void Replacement_never_leaves_a_broken_surrogate_pair()
    {
        var unicode = Gen.OneOfConst("a", "é", "\U0001F600", "中", "é", " ", "\U0001F468‍\U0001F469").Array[1, 20].Select(string.Concat);
        unicode.Select(Gen.Int[0, 100], Gen.Int[1, 6]).Sample((text, startSeed, length) =>
        {
            var start = startSeed % text.Length;
            var end = Math.Min(text.Length, start + length);
            var detector = new StubDetector("s", new PhiSpan(start, end, PhiCategory.Name, 1, "s"));
            var result = new TextDeidentifier(detector, new TagReplacement()).DeidentifyAsync(text).GetAwaiter().GetResult().Text;
            return !Utf16.HasLoneSurrogate(result);
        }, iter: 3000);
    }

    [Fact]
    public void Surrogates_never_equal_the_original()
    {
        var replacement = new SurrogateReplacement(new HmacPseudonymizer(TestKeys.Fixed), YearOnlyDateTransformer.Instance);
        Identifier.Sample(id =>
        {
            var fake = replacement.Replace(new PhiSpan(0, id.Value.Length, id.Category, 1, "g"), id.Value, new TextContext("p"));
            return !string.Equals(fake, id.Value, StringComparison.OrdinalIgnoreCase);
        }, iter: 3000);
    }

    [Fact]
    public void Every_age_falls_in_a_valid_band_that_contains_it()
    {
        var generalizer = AgeGeneralizer.SafeHarbor;
        Gen.Int[0, 130].Sample(age =>
        {
            var band = generalizer.Generalize(age);
            return generalizer.IsBandLabel(band.Label) && age >= band.Lower && (band.Upper is null || age <= band.Upper);
        });
    }

    [Fact]
    public void Date_shifts_are_never_zero_and_never_exceed_the_maximum()
    {
        var shifter = new DateShifter(new HmacPseudonymizer(TestKeys.Fixed), maxDays: 90);
        Gen.String.Sample(subject =>
        {
            var offset = shifter.OffsetDays(subject);
            return offset != 0 && Math.Abs(offset) <= 90;
        }, iter: 5000);
    }
}
