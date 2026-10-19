using System.Text.RegularExpressions;
using DeidSharp.Detection;
using DeidSharp.Detection.Patterns;

namespace DeidSharp.Tests.Detection.Patterns;

public partial class PatternPhiDetectorTests
{
    private static readonly PatternPhiDetector Detector = new();

    private static IReadOnlyList<PhiSpan> Detect(string text) => SpanMerger.Merge(text, Detector.Detect(new DetectionRequest(text)));

    // Asserts that some span of the category covers exactly the expected substring.
    private static void AssertFinds(string text, PhiCategory category, string expected)
    {
        var spans = Detect(text);
        var hits = spans.Where(s => s.Category == category).Select(s => text[s.Start..s.End]).ToList();
        Assert.True(hits.Contains(expected), $"Expected {category} \"{expected}\" in \"{text}\"; found: {string.Join(", ", spans.Select(s => $"{s.Category} \"{text[s.Start..s.End]}\""))}");
    }

    [Theory]
    [InlineData("Email rosa.alvarez@mailbox.example today", "rosa.alvarez@mailbox.example")]
    [InlineData("<j_doe+notes@sub.hospital.org>", "j_doe+notes@sub.hospital.org")]
    [InlineData("contact: A.B@x.io.", "A.B@x.io")]
    public void Finds_emails(string text, string expected) => AssertFinds(text, PhiCategory.Email, expected);

    [Theory]
    [InlineData("see https://portal.example.org/p?id=42.", "https://portal.example.org/p?id=42")]
    [InlineData("(www.myclinic.com)", "www.myclinic.com")]
    [InlineData("HTTP://EXAMPLE.ORG/a", "HTTP://EXAMPLE.ORG/a")]
    public void Finds_urls(string text, string expected) => AssertFinds(text, PhiCategory.Url, expected);

    [Theory]
    [InlineData("from 192.168.10.255 at night", "192.168.10.255")]
    [InlineData("ip=10.0.0.1;", "10.0.0.1")]
    [InlineData("from 10.0.0.1.", "10.0.0.1")]
    public void Finds_ipv4_addresses(string text, string expected) => AssertFinds(text, PhiCategory.IpAddress, expected);

    [Theory]
    [InlineData("host 2001:db8::1 down", "2001:db8::1")]
    [InlineData("addr fe80:0:0:0:200:f8ff:fe21:67cf", "fe80:0:0:0:200:f8ff:fe21:67cf")]
    public void Finds_ipv6_addresses(string text, string expected) => AssertFinds(text, PhiCategory.IpAddress, expected);

    [Theory]
    [InlineData("pump MAC 00:1A:2B:3C:4D:5E logged", "00:1A:2B:3C:4D:5E")]
    [InlineData("mac 00-1a-2b-3c-4d-5e", "00-1a-2b-3c-4d-5e")]
    public void Finds_mac_addresses_as_device_identifiers(string text, string expected) => AssertFinds(text, PhiCategory.DeviceIdentifier, expected);

    [Theory]
    [InlineData("call 617-555-0198 now", "617-555-0198")]
    [InlineData("call (617) 555-0198 now", "(617) 555-0198")]
    [InlineData("call (617)555-0198", "(617)555-0198")]
    [InlineData("call 617.555.0198", "617.555.0198")]
    [InlineData("call 617 555 0198", "617 555 0198")]
    [InlineData("call +1 617 555 0198", "+1 617 555 0198")]
    [InlineData("call 1-617-555-0198", "1-617-555-0198")]
    [InlineData("intl +44 20 7946 0958", "+44 20 7946 0958")]
    [InlineData("ext 555-0142 please", "555-0142")]
    public void Finds_phone_numbers(string text, string expected) => AssertFinds(text, PhiCategory.Phone, expected);

    [Theory]
    [InlineData("Fax: (617) 555-0101", "(617) 555-0101")]
    [InlineData("fax # 617-555-0101", "617-555-0101")]
    public void A_labeled_fax_number_is_a_fax_not_a_phone(string text, string expected) => AssertFinds(text, PhiCategory.Fax, expected);

    [Theory]
    [InlineData("SSN 123-45-6789.", "123-45-6789")]
    [InlineData("ssn: 123456789", "123456789")]
    [InlineData("Social Security Number 123 45 6789", "123 45 6789")]
    [InlineData("social security no. is 987-65-4321", "987-65-4321")]
    [InlineData("id 123 45 6789", "123 45 6789")]
    public void Finds_social_security_numbers(string text, string expected) => AssertFinds(text, PhiCategory.Ssn, expected);

    [Theory]
    [InlineData("card 4111 1111 1111 1111 on file", "4111 1111 1111 1111")]
    [InlineData("card 4111-1111-1111-1111", "4111-1111-1111-1111")]
    [InlineData("card 5500005555555559", "5500005555555559")]
    public void Finds_payment_cards_that_pass_the_luhn_check(string text, string expected) => AssertFinds(text, PhiCategory.AccountNumber, expected);

    [Fact]
    public void Ignores_card_shaped_numbers_that_fail_the_luhn_check() =>
        Assert.DoesNotContain(Detect("card 4111 1111 1111 1112"), s => s.Category == PhiCategory.AccountNumber);

    [Theory]
    [InlineData("car VIN 1M8GDM9AXKP042788 parked", "1M8GDM9AXKP042788")]
    [InlineData("1HGCM82633A004352", "1HGCM82633A004352")]
    public void Finds_vins_that_pass_the_check_digit(string text, string expected) => AssertFinds(text, PhiCategory.VehicleIdentifier, expected);

    [Theory]
    [InlineData("MRN: 12345", PhiCategory.MedicalRecordNumber, "12345")]
    [InlineData("MRN#12345678", PhiCategory.MedicalRecordNumber, "12345678")]
    [InlineData("Medical record number is 0098-221", PhiCategory.MedicalRecordNumber, "0098-221")]
    [InlineData("med rec no. 99-1234", PhiCategory.MedicalRecordNumber, "99-1234")]
    [InlineData("chart # A44812", PhiCategory.MedicalRecordNumber, "A44812")]
    [InlineData("Member ID: XJH123456789", PhiCategory.HealthPlanNumber, "XJH123456789")]
    [InlineData("Policy # 12345-AB", PhiCategory.HealthPlanNumber, "12345-AB")]
    [InlineData("Medicare ID 1EG4-TE5-MK73", PhiCategory.HealthPlanNumber, "1EG4-TE5-MK73")]
    [InlineData("Acct 99887766", PhiCategory.AccountNumber, "99887766")]
    [InlineData("FIN: 123456", PhiCategory.AccountNumber, "123456")]
    [InlineData("encounter number 77001", PhiCategory.AccountNumber, "77001")]
    [InlineData("DEA AB1234563", PhiCategory.LicenseNumber, "AB1234563")]
    [InlineData("NPI 1234567893", PhiCategory.LicenseNumber, "1234567893")]
    [InlineData("Lic. No. 445566", PhiCategory.LicenseNumber, "445566")]
    [InlineData("driver's license D1234567", PhiCategory.LicenseNumber, "D1234567")]
    [InlineData("license plate 7ABC123", PhiCategory.VehicleIdentifier, "7ABC123")]
    [InlineData("serial number SN-44812", PhiCategory.DeviceIdentifier, "SN-44812")]
    [InlineData("S/N 99A88B", PhiCategory.DeviceIdentifier, "99A88B")]
    [InlineData("lot # 7654321D", PhiCategory.DeviceIdentifier, "7654321D")]
    [InlineData("Patient ID 778899", PhiCategory.OtherIdentifier, "778899")]
    [InlineData("passport 912803456", PhiCategory.OtherIdentifier, "912803456")]
    public void Finds_labeled_identifiers_and_keeps_the_label(string text, PhiCategory category, string expected) =>
        AssertFinds(text, category, expected);

    [Theory]
    [InlineData("ref 1234567890123 noted", "1234567890123")]
    [InlineData("#20240311998", "20240311998")]
    public void Treats_a_long_unlabeled_number_as_an_identifier(string text, string expected) =>
        AssertFinds(text, PhiCategory.OtherIdentifier, expected);

    [Theory]
    [InlineData("on 2024-03-05.", "2024-03-05")]
    [InlineData("at 2024-03-05T10:00:00Z", "2024-03-05")]
    [InlineData("on 2024/3/5", "2024/3/5")]
    [InlineData("on 3/5/2024", "3/5/2024")]
    [InlineData("seen 3/5/2024.", "3/5/2024")]
    [InlineData("stamp 20240305.", "20240305")]
    [InlineData("on 03/05/24", "03/05/24")]
    [InlineData("on 25.12.2023", "25.12.2023")]
    [InlineData("on 25-12-2023", "25-12-2023")]
    [InlineData("on March 5, 2024", "March 5, 2024")]
    [InlineData("on March 5th", "March 5th")]
    [InlineData("on Sept. 7 2021", "Sept. 7 2021")]
    [InlineData("on MAR 5", "MAR 5")]
    [InlineData("on dec 25", "dec 25")]
    [InlineData("on 5 March 2024", "5 March 2024")]
    [InlineData("on the 5th of March", "5th of March")]
    [InlineData("since March 2024", "March 2024")]
    [InlineData("since Aug, 2019", "Aug, 2019")]
    [InlineData("since 3/2024", "3/2024")]
    [InlineData("stamp 20240305", "20240305")]
    [InlineData("May 10 visit", "May 10")]
    [InlineData("DOB 05-Mar-1950.", "05-Mar-1950")]
    [InlineData("DOB 05MAR1950", "05MAR1950")]
    [InlineData("drawn 5-March-2024", "5-March-2024")]
    [InlineData("drawn 05/Mar/24", "05/Mar/24")]
    [InlineData("drawn 2024-Mar-05", "2024-Mar-05")]
    [InlineData("drawn Mar-05-2024", "Mar-05-2024")]
    public void Finds_dates_finer_than_a_year(string text, string expected) => AssertFinds(text, PhiCategory.Date, expected);

    [Theory]
    [InlineData("a 92-year-old woman", "92")]
    [InlineData("a 92 year old woman", "92")]
    [InlineData("93 yo F", "93")]
    [InlineData("97 y/o male", "97")]
    [InlineData("103 years old", "103")]
    [InlineData("99yo", "99")]
    [InlineData("91 y.o. man", "91")]
    [InlineData("aged 95", "95")]
    [InlineData("Age: 101", "101")]
    [InlineData("age=90", "90")]
    public void Finds_ages_over_89_and_redacts_only_the_number(string text, string expected) => AssertFinds(text, PhiCategory.Age, expected);

    [Theory]
    [InlineData("lives at 48 Willow Creek Road.", "48 Willow Creek Road")]
    [InlineData("221B Baker Street, London", "221B Baker Street")]
    [InlineData("12 N. Main St., Apt 4 ok", "12 N. Main St., Apt 4")]
    [InlineData("1600 Pennsylvania Avenue NW", "1600 Pennsylvania Avenue")]
    [InlineData("send to PO Box 123", "PO Box 123")]
    [InlineData("send to P.O. Box 77", "P.O. Box 77")]
    [InlineData("at 655 Bogan Mill Suite 45", "655 Bogan Mill Suite 45")]
    public void Finds_street_addresses(string text, string expected) => AssertFinds(text, PhiCategory.StreetAddress, expected);

    [Theory]
    [InlineData("Springfield, MA 01103", PhiCategory.ZipCode, "01103")]
    [InlineData("Boston MA 02115-1234", PhiCategory.ZipCode, "02115-1234")]
    [InlineData("zip code: 90210", PhiCategory.ZipCode, "90210")]
    [InlineData("postal code 30301", PhiCategory.ZipCode, "30301")]
    [InlineData("moved to Springfield, MA last year", PhiCategory.Location, "Springfield")]
    [InlineData("from Cedar Falls, IA", PhiCategory.Location, "Cedar Falls")]
    public void Finds_zip_codes_and_cities_next_to_a_state(string text, PhiCategory category, string expected) =>
        AssertFinds(text, category, expected);

    [Theory]
    [InlineData("Seen by Dr. Amelia Hart today", "Amelia Hart")]
    [InlineData("Mrs. Smith declined", "Smith")]
    [InlineData("Prof Ng reviewed", "Ng")]
    [InlineData("Patient: John Smith", "John Smith")]
    [InlineData("Attending: Jane Q. Doe", "Jane Q. Doe")]
    [InlineData("Electronically signed by: Mark Lee", "Mark Lee")]
    [InlineData("her daughter Carmen called", "Carmen")]
    [InlineData("son, Michael Brown, visited", "Michael Brown")]
    [InlineData("wife's Maria was present", "Maria")]
    [InlineData("Seen by Dr. José García today", "José García")]
    [InlineData("Mrs. Ñúñez declined", "Ñúñez")]
    [InlineData("DR. SMITH reviewed", "SMITH")]
    [InlineData("Patient: JOHN SMITH", "JOHN SMITH")]
    [InlineData("daughter Zoë called", "Zoë")]
    public void Finds_names_after_a_title_label_or_relation(string text, string expected) => AssertFinds(text, PhiCategory.Name, expected);

    [Theory]
    [InlineData("BP 120/80 mmHg, HR 88 bpm, temp 37.5 C")]
    [InlineData("pain 7/10, strength 4/5 in the left arm")]
    [InlineData("WBC 12.3, Hb 9.8 g/dL, platelets 250,000, INR 1.2")]
    [InlineData("Na 140, K 4.1, creatinine 2.1 mg/dL, eGFR 45 mL/min/1.73m2")]
    [InlineData("metformin 500 mg twice daily, 5-10 units insulin, q6h x3")]
    [InlineData("seen in 2019, diagnosed in 1998, the year 2024")]
    [InlineData("at 14:30 and again at 23:05:10")]
    [InlineData("an 89-year-old woman aged 85 in her 80s")]
    [InlineData("we will march to the clinic and may be given fluids")]
    [InlineData("Stage 3 CKD, BMI 31.2, O2 sat 94%, glucose 105")]
    [InlineData("ICD-10 N18.3, LOINC 2160-0, 12-lead ECG, 24-hour urine")]
    [InlineData("SNOMED 433144002, RxNorm 314076, NDC 0002-8215-01, CPT 99213")]
    [InlineData("Dr. visit tomorrow; patient denies pain; account of events")]
    [InlineData("pi is 3.14159265 and e is 2.71828182")]
    [InlineData("a lot of fluid; license to practice; policy changes")]
    [InlineData("1:1 sitter, 2/2 sets, 100-200 mg")]
    [InlineData("dose 250-1000 mg daily, 500-1000 units, 100-1000 mL")]
    [InlineData("Name: N/A")]
    [InlineData("Labs OK, IN progress, Seen, ME")]
    public void Leaves_ordinary_clinical_text_alone(string text) => Assert.Empty(Detect(text));

    [Fact]
    public void Leaves_times_and_clock_strings_alone_even_with_colons() =>
        Assert.DoesNotContain(Detect("12:30:45 and 1:2:3"), s => s.Category == PhiCategory.IpAddress);

    [Fact]
    public void Tags_written_by_the_de_identifier_are_not_detected_again() =>
        Assert.Empty(Detect("[NAME] was seen on [DATE] at [ADDRESS], MRN [MRN], [AGE]-year-old, phone [PHONE]"));

    [Fact]
    public void Every_rule_has_a_unique_name_and_a_sane_score()
    {
        var rules = PatternLibrary.Default;
        Assert.Equal(rules.Count, rules.Select(r => r.Name).Distinct().Count());
        Assert.All(rules, r => Assert.InRange(r.Score, 0.0, 1.0));
        Assert.All(rules, r => Assert.NotEqual(Regex.InfiniteMatchTimeout, r.Pattern.MatchTimeout));
    }

    [Fact]
    public void Custom_rules_replace_the_default_table()
    {
        var rule = new PatternRule("ticket", PhiCategory.OtherIdentifier, Ticket(), 0.9);
        var detector = new PatternPhiDetector([rule]);

        var span = Assert.Single(detector.Detect(new DetectionRequest("ref TCK-991 and SSN 123-45-6789"), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(["pattern:ticket"], span.Sources);
    }

    [Fact]
    public void Duplicate_rule_names_are_rejected()
    {
        var rule = new PatternRule("dup", PhiCategory.OtherIdentifier, LetterX(), 0.5);
        Assert.Throws<ArgumentException>(() => new PatternPhiDetector([rule, rule]));
    }

    [Fact]
    public void A_validator_can_discard_a_match()
    {
        var rule = new PatternRule("even", PhiCategory.OtherIdentifier, Digits(), 0.5, Validator: m => int.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture) % 2 == 0);
        var spans = new PatternPhiDetector([rule]).Detect(new DetectionRequest("1 2 3 4"), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, spans.Count);
    }

    [Fact]
    public void Handles_very_long_text_without_timing_out()
    {
        var text = string.Concat(Enumerable.Repeat("BP 120/80, MRN: 12345, call 617-555-0198. ", 5000));
        var spans = Detector.Detect(new DetectionRequest(text), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(spans.Count >= 10000);
    }

    [Fact]
    public void Survives_pathological_input_built_to_make_regexes_backtrack()
    {
        var text = new string('a', 20000) + "@" + new string('a', 20000) + "!" + string.Concat(Enumerable.Repeat("1 ", 10000));
        _ = Detector.Detect(new DetectionRequest(text), cancellationToken: TestContext.Current.CancellationToken);
    }

    [GeneratedRegex(@"TCK-\d+", RegexOptions.None, 1000)]
    private static partial Regex Ticket();

    [GeneratedRegex("x")]
    private static partial Regex LetterX();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();
}
