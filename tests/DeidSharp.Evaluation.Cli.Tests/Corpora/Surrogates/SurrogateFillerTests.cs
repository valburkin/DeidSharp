using System.Security.Cryptography;
using System.Text;
using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Corpora.Surrogates;

namespace DeidSharp.Evaluation.Cli.Tests.Corpora.Surrogates;

/// <summary>Filling a made-up note's placeholders: every one replaced, every piece of PHI a gold span on its own text.</summary>
public sealed class SurrogateFillerTests : IDisposable
{
    private const string DrawnLine = "Signature: ______________\n";

    // SHA-256 of the fixed note filled by generator 3 with the shipped lists.
    private const string GoldenSha256 = "e8d6394badc4bfd58664df466f039cb5295f7c1aac25de1aef1f80459f5768b1";

    // Reaches every kind of placeholder (Covers_every_kind_of_placeholder), so the golden
    // test below pins every renderer.
    private const string Note =
        "Name:  ___                 Unit No:   ___\n" +
        "Admission Date:  ___              Discharge Date:   ___\n" +
        "Date of Birth:  ___\n" +
        "Sex:   F\n" +
        "Attending: ___\n" +
        "History of Present Illness:\n___ with a history of CHF seen by Dr. ___ on ___.\n" +
        "Ms. ___ is a ___ year old woman seen by ___ and ___, M.D. on call.\n" +
        "She lives with her daughter ___ (phone: ___) and was transferred to ___ near ___ Hospital.\n" +
        "Labs ___ 10:30 showed ___ disease, per ___.\n" +
        "Social History:\n___\n" +
        DrawnLine +
        "Followup Instructions:\n___\n";

    private readonly TempDirectory _files = new();
    private readonly SurrogateLists _lists;

    public SurrogateFillerTests() => _lists = SurrogateLists.Load(SurrogateListsFixture.Write(_files));

    public void Dispose() => _files.Dispose();

    private (string Text, IReadOnlyList<LabeledSpan> Spans) Fill(string key) =>
        SurrogateFiller.Fill(Note, new NoteSurrogates(_lists, SeededRandom.For("test", key), SurrogateFiller.Sex(Note)));

    [Fact]
    public void Replaces_every_placeholder_and_leaves_a_drawn_line_alone()
    {
        var (text, _) = Fill("note 1");

        Assert.Contains(DrawnLine, text, StringComparison.Ordinal);
        Assert.DoesNotContain(SurrogateFiller.Placeholder, text.Replace(DrawnLine, "", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void Puts_each_name_span_on_names_the_lists_hold_in_order()
    {
        var (text, spans) = Fill("note 1");
        var names = spans.Where(s => s.Label is SurrogateLabels.PatientName or SurrogateLabels.StaffName or SurrogateLabels.RelativeName);

        Assert.All(spans, s => Assert.NotNull(s.TypedBy));
        Assert.All(names, s => Assert.All(text[s.Start..s.End].Split([' ', ','], StringSplitOptions.RemoveEmptyEntries), part => Assert.Contains(part, SurrogateListsFixture.Names)));
        Assert.Equal(spans.OrderBy(s => s.Start), spans);
    }

    [Fact]
    public void Gives_the_patient_one_surname_and_a_first_name_that_fits_the_sex_field()
    {
        var (text, spans) = Fill("note 1");
        var surname = Text(text, spans.Single(s => s.TypedBy == "patient title"));
        var full = Text(text, spans.Single(s => s.TypedBy == "name field"));

        Assert.Contains(surname, full, StringComparison.Ordinal);
        Assert.Contains(full.Replace(surname, "", StringComparison.Ordinal).Trim(' ', ','), SurrogateListsFixture.FemaleNames);
    }

    [Fact]
    public void Draws_the_same_surrogates_for_the_same_key_and_others_for_another()
    {
        var (text, spans) = Fill("note 1");
        var (again, spansAgain) = Fill("note 1");

        Assert.Equal(text, again);
        Assert.Equal(spans, spansAgain);
        Assert.NotEqual(text, Fill("note 2").Text);
    }

    [Fact]
    public void Labels_the_follow_up_with_a_clinician_a_phone_and_a_date()
    {
        var (_, spans) = Fill("note 1");

        Assert.Contains(spans, s => s is { TypedBy: "history opening", Label: SurrogateLabels.AgeUnder90 or SurrogateLabels.Age });
        Assert.Equal(
            [SurrogateLabels.StaffName, SurrogateLabels.Phone, SurrogateLabels.Date],
            spans.Where(s => s.TypedBy == "follow-up section").Take(3).Select(s => s.Label));
    }

    [Fact]
    public void Fills_a_fixed_note_exactly_as_this_generator_version_does()
    {
        // Pins the generator's output with the shipped lists: any change to the rules, the
        // lists or the drawing changes the corpus, and must come with a new GeneratorVersion.
        var lists = SurrogateLists.LoadBundled();
        var (text, spans) = SurrogateFiller.Fill(Note, new NoteSurrogates(lists, SeededRandom.For("golden", "note 1"), SurrogateFiller.Sex(Note)));
        var output = text + string.Concat(spans.Select(s => $"\n{s.Start} {s.End} {s.Label} {s.TypedBy}"));

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(output)));

        Assert.True(hash == GoldenSha256, $"The generator's output changed: bump MimicIvNoteCorpus.GeneratorVersion, then set {nameof(GoldenSha256)} to {hash}.");
        Assert.Equal(3, MimicIvNoteCorpus.GeneratorVersion);
    }

    [Fact]
    public void Covers_every_kind_of_placeholder()
    {
        var (_, spans) = Fill("note 1");
        var kinds = spans.Select(s => PlaceholderRules.All.Single(r => r.Name == s.TypedBy).Kind);

        Assert.Equal(NoteSurrogates.Kinds.Order(), kinds.Distinct().Order());
    }

    [Theory]
    [InlineData("Sex:   M\n", 'M')]
    [InlineData("Sex: f\n", 'F')]
    [InlineData("No field here.", null)]
    public void Reads_the_sex_field(string note, char? sex)
    {
        Assert.Equal(sex, SurrogateFiller.Sex(note));
    }

    private static string Text(string text, LabeledSpan span) => text[span.Start..span.End];
}
