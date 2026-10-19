using DeidSharp.Detection;
using DeidSharp.Detection.Known;

namespace DeidSharp.Tests.Detection.Known;

public class KnownIdentifierDetectorTests
{
    private static readonly KnownIdentifierDetector Detector = new();

    private static List<string> Found(string text, KnownIdentifiers known) =>
        [.. SpanMerger.Merge(text, Detector.Detect(new DetectionRequest(text, known))).Select(s => text[s.Start..s.End])];

    private static KnownIdentifiers Name(string name) => KnownIdentifiers.Empty.AddName(name);

    [Theory]
    [InlineData("Rosa Maria Alvarez-Nunez", "Seen: Rosa Maria Alvarez-Nunez today", "Rosa Maria Alvarez-Nunez")]
    [InlineData("Rosa Maria Alvarez-Nunez", "rosa called", "rosa")]
    [InlineData("Rosa Maria Alvarez-Nunez", "Mrs. ALVAREZ-NUNEZ is stable", "ALVAREZ-NUNEZ")]
    [InlineData("Rosa Maria Alvarez-Nunez", "Mrs. Alvarez is stable", "Alvarez")]
    [InlineData("Rosa Maria Alvarez-Nunez", "per Nunez family", "Nunez")]
    [InlineData("Seán O'Brien", "Mr O’Brien declined", "O’Brien")]
    [InlineData("Jane Smith", "Smith's daughter", "Smith")]
    [InlineData("Jane Smith", "Smith, Jane", "Smith")]
    [InlineData("Li Wu", "Wu reports pain", "Wu")]
    public void Finds_a_name_in_full_or_by_any_part(string name, string text, string expected) =>
        Assert.Contains(expected, Found(text, Name(name)));

    [Theory]
    [InlineData("Minh Do", "Mr. Do reports pain", "Do")]
    [InlineData("Minh Do", "Minh Do reports pain", "Minh Do")]
    [InlineData("Anna van Dijk", "Dr van Dijk", "van")]
    public void Surnames_that_are_everyday_words_are_found_after_a_title_or_in_the_full_name(string name, string text, string expected) =>
        Assert.Contains(expected, Found(text, Name(name)));

    [Theory]
    [InlineData("Minh Do", "Do not resuscitate; do as advised")]
    [InlineData("Anna van Dijk", "arrived by van")]
    public void Surnames_that_are_everyday_words_are_not_found_alone(string name, string text) =>
        Assert.DoesNotContain(Found(text, Name(name)), f => f.Equals("Do", StringComparison.OrdinalIgnoreCase) || f.Equals("van", StringComparison.OrdinalIgnoreCase));

    [Theory]
    [InlineData("José Álvarez", "Jose Alvarez was seen")]
    [InlineData("Jose Alvarez", "JOSÉ ÁLVAREZ was seen")]
    [InlineData("José Álvarez", "Álvarez was seen")]
    public void Ignores_accents_on_either_side(string name, string text) => Assert.NotEmpty(Found(text, Name(name)));

    [Fact]
    public void A_match_in_decomposed_text_covers_the_trailing_accent()
    {
        const string text = "José was seen";
        var span = Assert.Single(SpanMerger.Merge(text, Detector.Detect(new DetectionRequest(text, Name("José")), cancellationToken: TestContext.Current.CancellationToken)));
        Assert.Equal("José", text[span.Start..span.End]);
    }

    [Fact]
    public void Offsets_point_into_the_original_text_not_the_folded_copy()
    {
        const string text = "Ich sah Müller und Strauß.";
        var found = Found(text, KnownIdentifiers.Empty.AddName("Muller").AddName("Straus"));
        Assert.Equal(["Müller", "Strauß"], found);
    }

    [Theory]
    [InlineData("Ann Lee", "Annual review in Leeds")]
    [InlineData("Ann Lee", "annotated")]
    [InlineData("Dr. Mark Li", "Dr. visit")]
    [InlineData("J. Smith", "Item J was checked")]
    public void Does_not_match_inside_words_or_on_titles_and_initials(string name, string text) =>
        Assert.Empty(Found(text, Name(name)));

    [Theory]
    [InlineData("1931-03-04")]
    [InlineData("03/04/1931")]
    [InlineData("3/4/1931")]
    [InlineData("3/4/31")]
    [InlineData("04.03.1931")]
    [InlineData("4-3-1931")]
    [InlineData("March 4, 1931")]
    [InlineData("Mar 4 1931")]
    [InlineData("mar. 4th, 1931")]
    [InlineData("4 March 1931")]
    [InlineData("4th of March, 1931")]
    [InlineData("19310304")]
    [InlineData("1931/3/4")]
    [InlineData("04-Mar-1931")]
    [InlineData("04MAR1931")]
    [InlineData("4-March-31")]
    [InlineData("1931-Mar-04")]
    [InlineData("Mar-04-1931")]
    public void Finds_a_known_date_in_any_common_format(string written)
    {
        var known = KnownIdentifiers.Empty.AddDate(new DateOnly(1931, 3, 4));
        Assert.Equal([written], Found($"DOB {written}.", known));
    }

    [Theory]
    [InlineData("born 1931")]
    [InlineData("03/04/1932")]
    [InlineData("03/05/1931")]
    [InlineData("119310304")]
    [InlineData("1931-03-041")]
    public void Does_not_mistake_other_dates_or_a_bare_year_for_the_known_date(string text) =>
        Assert.Empty(Found(text, KnownIdentifiers.Empty.AddDate(new DateOnly(1931, 3, 4))));

    [Theory]
    [InlineData("617-555-0198")]
    [InlineData("(617) 555-0198")]
    [InlineData("617.555.0198")]
    [InlineData("6175550198")]
    [InlineData("617 555 0198")]
    [InlineData("+1 617 555 0198")]
    [InlineData("16175550198")]
    [InlineData("1-617-555-0198")]
    public void Finds_a_known_phone_with_any_punctuation(string written)
    {
        var found = Found($"call {written} now", KnownIdentifiers.Empty.AddPhone("617-555-0198"));
        Assert.EndsWith("0198", Assert.Single(found), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("617-555-01989")]
    [InlineData("2617-555-0198")]
    public void Does_not_match_a_phone_inside_a_longer_number(string text) =>
        Assert.Empty(Found(text, KnownIdentifiers.Empty.AddPhone("617-555-0198")));

    [Theory]
    [InlineData("MRN 4471-882-19")]
    [InlineData("MRN 447188219")]
    [InlineData("MRN 4471 882 19")]
    [InlineData("MRN 4471.882.19")]
    public void Finds_a_known_record_number_with_any_separators(string text) =>
        Assert.Single(Found(text, KnownIdentifiers.Empty.AddIdentifier("4471-882-19", PhiCategory.MedicalRecordNumber)));

    [Fact]
    public void Matches_letter_identifiers_case_insensitively() =>
        Assert.Single(Found("licence s99923841 on file", KnownIdentifiers.Empty.AddIdentifier("S99923841", PhiCategory.LicenseNumber)));

    [Fact]
    public void Skips_identifiers_too_short_to_search_for_safely() =>
        Assert.Empty(Found("MRN 123, room 123", KnownIdentifiers.Empty.AddIdentifier("123", PhiCategory.MedicalRecordNumber)));

    [Fact]
    public void The_minimum_identifier_length_is_configurable()
    {
        var detector = new KnownIdentifierDetector(new KnownIdentifierOptions { MinimumIdentifierLength = 3 });
        var spans = detector.Detect(new DetectionRequest("MRN 123", KnownIdentifiers.Empty.AddIdentifier("123")), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(spans);
    }

    [Fact]
    public void Rejects_nonsensical_options()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new KnownIdentifierDetector(new KnownIdentifierOptions { MinimumIdentifierLength = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new KnownIdentifierDetector(new KnownIdentifierOptions { MinimumNameTokenLength = 0 }));
    }

    [Theory]
    [InlineData("rosa.alvarez@mailbox.example", "write to ROSA.ALVAREZ@MAILBOX.EXAMPLE today", 1)]
    [InlineData("rosa.alvarez@mailbox.example", "xrosa.alvarez@mailbox.example", 0)]
    [InlineData("rosa.alvarez@mailbox.example", "rosa.alvarez@mailbox.examples", 0)]
    public void Finds_a_known_email_only_as_a_whole_address(string email, string text, int expected) =>
        Assert.Equal(expected, Found(text, KnownIdentifiers.Empty.AddEmail(email)).Count);

    [Theory]
    [InlineData("48 Willow Creek Road")]
    [InlineData("48 willow creek rd.")]
    [InlineData("48 Willow Creek Rd, Apt 3")]
    public void Finds_a_known_street_line_with_suffix_abbreviated_or_spelled_out(string text) =>
        Assert.NotEmpty(Found(text, KnownIdentifiers.Empty.AddStreetAddress("48 Willow Creek Rd")));

    [Theory]
    [InlineData("admitted to Saint Brendan Hospital")]
    [InlineData("admitted to St Brendan Hospital")]
    [InlineData("admitted to St. Brendan Hospital")]
    public void Finds_a_known_organization_with_its_usual_abbreviations(string text) =>
        Assert.Single(Found(text, KnownIdentifiers.Empty.AddOrganization("St. Brendan Hospital")));

    [Theory]
    [InlineData("zip 01103", 1)]
    [InlineData("zip 011030", 0)]
    public void Finds_a_known_zip_code_only_as_a_whole_number(string text, int expected) =>
        Assert.Equal(expected, Found(text, KnownIdentifiers.Empty.Add(PhiCategory.ZipCode, "01103")).Count);

    [Fact]
    public void Values_with_regex_metacharacters_are_matched_literally()
    {
        var known = KnownIdentifiers.Empty.AddOrganization("C++ (Main) Clinic").AddIdentifier("A.B*C+D?E");
        Assert.Single(Found("seen at C++ (Main) Clinic", known));
        Assert.Empty(Found("seen at Cxx Main Clinic", known));
    }

    [Fact]
    public void Nothing_known_or_no_text_finds_nothing()
    {
        Assert.Empty(Detector.Detect(new DetectionRequest("Jane Smith", KnownIdentifiers.Empty), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(Detector.Detect(new DetectionRequest("", Name("Jane Smith")), cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Ages_are_not_searched_for_as_known_values() =>
        Assert.Empty(Found("92 mg", KnownIdentifiers.Empty.Add(PhiCategory.Age, "92")));

    [Fact]
    public void Every_hit_is_certain_and_attributed()
    {
        var span = Assert.Single(Detector.Detect(new DetectionRequest("Jane", Name("Jane Smith")), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1.0, span.Score);
        Assert.Equal(["known"], span.Sources);
        Assert.Equal(PhiCategory.Name, span.Category);
    }

    [Fact]
    public async Task The_async_form_gives_the_same_answer()
    {
        var request = new DetectionRequest("Jane Smith", Name("Jane Smith"));
        Assert.Equal(Detector.Detect(request, cancellationToken: TestContext.Current.CancellationToken), await Detector.DetectAsync(request, TestContext.Current.CancellationToken));
    }
}
