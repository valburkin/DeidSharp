using System.Globalization;
using System.Text.RegularExpressions;
using CsCheck;
using DeidSharp.Detection;
using DeidSharp.Detection.Known;
using DeidSharp.Text;

namespace DeidSharp.Tests.Detection.Known;

/// <summary>
/// The anchor index may only skip searches that would have found nothing. These tests
/// compare the detector with every search run on every text, and check that the views
/// the anchors are compared in are at least as loose as the searches.
/// </summary>
public partial class AnchorIndexTests
{
    private const RegexOptions SearchOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // Text fragments: identifiers written in many ways; words that collide with them;
    // characters a case-insensitive search treats as the same letter though they differ
    // (the Kelvin sign and "k", the cases of the digraph U+01C4, Georgian Mkhedruli and
    // Mtavruli, U+0264 and U+A7CB); a near miss it keeps apart (the micro sign and Greek
    // mu); and digits from another script next to dates.
    private static readonly string[] Fragments =
    [
        "Kelly", "\u212Aelly", "KELLY", "kel", "Smith", "SMITH", "smiths", "Ann", "Anna", "Ánna", "\u01C5ana", "\u01C4ANA", "dzana",
        "\u00B5ark", "\u03BCARK", "\u10D0\u10DC\u10D0", "\u1C90\u1C9C\u1C90", "\u0264ana", "\uA7CBANA", "Street", "St", "St.", "Saint", "Main", "Oak", "Brendan", "Hospital",
        "742", "1742", "Evergreen", "Terrace", "Ter", "Do", "Mr.", "Dr", "van", "Jose", "José", "rosa@example.org",
        "ROSA@EXAMPLE.ORG", "March", "Mar", "Sept", "3", "03", "4", "31", "1931", "19310304", "193103045", "March 4, 1931",
        "4 Mar 31", "04MAR1931", "1931-Mar-04", "3/4/1931", "03.04.31", "\u0663", "\u0661\u0669\u0663\u0661", "617", "555", "0142", "6175550142",
        "+1 617.555.0142", "1-617-555-0142", "(617)555-0142", "617 555  0142", "MRN", "A1B2", "a1-b2", "A 1 B 2", "(", ")", "-",
        "/", ".", ",", " ", "\t", "\u00A0", "\u2009",
    ];

    private static readonly Gen<string> Text =
        Gen.Int[0, Fragments.Length - 1].Select(i => Fragments[i]).Array[0, 14]
            .Select(Gen.OneOfConst(" ", "", ", ", "-"), (fragments, separator) => string.Join(separator, fragments));

    private static readonly Gen<(PhiCategory Category, string Value)> Identifier = Gen.OneOf(
        Gen.OneOfConst("Kelly Smith", "Ann van Do", "\u01C5ana Oak", "\u00B5ark", "José Smith-Main", "Mr. St Kelly", "\u10D0\u10DC\u10D0 Smith", "\u0264ana Smith")
            .Select(v => (PhiCategory.Name, v)),
        Gen.OneOfConst("617-555-0142", "+1 (617) 555 0142").Select(v => (PhiCategory.Phone, v)),
        Gen.OneOfConst("A1B2", "MRN-1931", "a1 b2").Select(v => (PhiCategory.MedicalRecordNumber, v)),
        Gen.OneOfConst("1931-03-04", "1931-03-31", "1931-09-04", "not a date").Select(v => (PhiCategory.Date, v)),
        Gen.OneOfConst("3 Main Street", "Saint Oak", "742 Evergreen Terrace").Select(v => (PhiCategory.StreetAddress, v)),
        Gen.OneOfConst("Main", "Saint Kelly").Select(v => (PhiCategory.Location, v)),
        Gen.OneOfConst("St. Brendan Hospital").Select(v => (PhiCategory.Organization, v)),
        Gen.OneOfConst("rosa@example.org").Select(v => (PhiCategory.Email, v)));

    [Fact]
    public void Skipping_searches_never_changes_what_is_found()
    {
        var detector = new KnownIdentifierDetector();
        Identifier.Array[1, 6].Select(Text).Sample((ids, text) =>
        {
            var known = ids.Aggregate(KnownIdentifiers.Empty, (set, id) => set.Add(id.Category, id.Value));
            var found = detector.Detect(new DetectionRequest(text, known)).Select(s => (s.Start, s.End, s.Category));
            return found.SequenceEqual(EverySearch(text, known));
        }, iter: 10_000);
    }

    [Theory]
    [InlineData(PhiCategory.Name, "Kelly Smith", "\u212AELLY was seen")]
    [InlineData(PhiCategory.Name, "\u10D0\u10DC\u10D0 Smith", "\u1C90\u1C9C\u1C90 was seen")]
    [InlineData(PhiCategory.Name, "\u0264ana Smith", "Seen: \uA7CBANA today.")]
    [InlineData(PhiCategory.Phone, "617-555-0142", "call (617) 555 - 0142")]
    [InlineData(PhiCategory.MedicalRecordNumber, "MRN-44-821", "mrn 44821")]
    [InlineData(PhiCategory.Date, "1931-03-04", "DOB 3/4/31")]
    [InlineData(PhiCategory.StreetAddress, "742 Evergreen Terrace", "lives at 742 Evergreen Ter.")]
    public void Finds_identifiers_whose_anchor_is_written_differently(PhiCategory category, string value, string text)
    {
        var known = KnownIdentifiers.Empty.Add(category, value);

        Assert.NotEmpty(new KnownIdentifierDetector().Detect(new DetectionRequest(text, known), cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Case_keys_join_every_character_a_search_treats_as_the_same_letter()
    {
        // The scripts names are written in, with their compatibility and supplement blocks:
        // Latin, IPA, Greek, Cyrillic, Armenian, Georgian, Cherokee, Glagolitic, Coptic,
        // the Latin and Cyrillic extensions, and full-width forms.
        (int First, int Last)[] blocks =
        [
            (0x20, 0x58F), (0x10A0, 0x10FF), (0x13A0, 0x13FF), (0x1C80, 0x1CBF), (0x1D00, 0x1FFF), (0x2100, 0x218F),
            (0x2C00, 0x2DFF), (0xA640, 0xA7FF), (0xAB30, 0xABBF), (0xFF00, 0xFFEF),
        ];
        var letters = blocks.SelectMany(b => Enumerable.Range(b.First, b.Last - b.First + 1)).Select(c => (char)c).ToList();
        var keys = new CaseKeys(letters);
        var everyCharacter = new string([.. Enumerable.Range(0, char.MaxValue + 1).Select(c => (char)c)]);

        foreach (var letter in letters)
        {
            var same = new Regex(Regex.Escape(letter.ToString()), SearchOptions).Matches(everyCharacter);
            Assert.All(same, m => Assert.True(
                keys.Of(m.Value[0]) == keys.Of(letter),
                string.Create(CultureInfo.InvariantCulture, $"U+{(int)letter:X4} matches U+{(int)m.Value[0]:X4} but their case keys differ.")));
        }
    }

    [Fact]
    public void Case_keys_leave_characters_outside_the_anchors_alone()
    {
        var keys = new CaseKeys("ab");

        Assert.Equal("AABBKkz", keys.Apply("AaBbKkz"));
    }

    [Fact]
    public void The_compact_view_drops_exactly_the_separators_a_search_allows()
    {
        for (var c = 0; c <= char.MaxValue; c++)
        {
            Assert.Equal(DigitGap().IsMatch(((char)c).ToString()), KnownValuePatterns.IsDigitGapSeparator((char)c));
        }
    }

    [Fact]
    public void An_empty_anchor_keeps_its_search_running_everywhere()
    {
        var index = new AnchorIndex([new KnownValuePattern("x", AnchorView.Folded, ["", "zzz"]), new KnownValuePattern("y", AnchorView.Folded, ["qqq"])]);

        Assert.Equal([0], index.Candidates("nothing here"));
    }

    [Fact]
    public void A_large_set_is_searched_only_for_what_a_text_holds()
    {
        var known = Enumerable.Range(0, 5000).Aggregate(KnownIdentifiers.Empty, (set, i) => set
            .AddName(string.Create(CultureInfo.InvariantCulture, $"Given{i} Family{i}"))
            .AddIdentifier(string.Create(CultureInfo.InvariantCulture, $"MRN{i:000000}"))
            .AddDate(new DateOnly(1900 + i % 120, 1 + i % 12, 1 + i % 28)));
        const string text = "Given42 Family42 (MRN000042) seen; BP 120/80, follow up in 2 weeks.";
        var patterns = known.Items.Select(item => KnownValuePatterns.Build(item, KnownIdentifierOptions.Default)!).ToList();

        var found = new KnownIdentifierDetector().Detect(new DetectionRequest(text, known), cancellationToken: TestContext.Current.CancellationToken);
        var searched = new AnchorIndex(patterns).Candidates(TextFolding.Fold(text));

        Assert.Equal(["Given42 Family42", "MRN000042"], SpanMerger.Merge(text, found).Select(s => text[s.Start..s.End]));

        // The name, the MRN and the dates whose year matches a number in the text ("42",
        // "80"), out of 15,000 searches.
        Assert.InRange(searched.Length, 2, 150);
    }

    // The separators KnownValuePatterns allows between the characters of an identifier.
    [GeneratedRegex(@"[\s.\-/()]", RegexOptions.CultureInvariant)]
    private static partial Regex DigitGap();

    // What the detector finds without the index: every search run on the text, in order.
    private static IEnumerable<(int Start, int End, PhiCategory Category)> EverySearch(string text, KnownIdentifiers known)
    {
        var folded = TextFolding.Fold(text);
        foreach (var item in known.Items)
        {
            if (KnownValuePatterns.Build(item, KnownIdentifierOptions.Default) is not { } pattern)
            {
                continue;
            }

            foreach (Match match in new Regex(pattern.Pattern, SearchOptions).Matches(folded))
            {
                if (match.Length > 0)
                {
                    yield return (match.Index, match.Index + match.Length, item.Category);
                }
            }
        }
    }
}
