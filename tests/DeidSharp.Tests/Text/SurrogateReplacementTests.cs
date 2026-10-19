using System.Text.RegularExpressions;
using DeidSharp.Detection;
using DeidSharp.Pseudonymization;
using DeidSharp.Structured;
using DeidSharp.Tests.TestDoubles;
using DeidSharp.Text;

namespace DeidSharp.Tests.Text;

public class SurrogateReplacementTests
{
    private static readonly HmacPseudonymizer Pseudonymizer = new(TestKeys.Fixed);

    private static readonly SurrogateReplacement SafeHarbor = new(Pseudonymizer, YearOnlyDateTransformer.Instance);

    private static readonly SurrogateReplacement Shifting = new(Pseudonymizer, new ShiftingDateTransformer(new DateShifter(Pseudonymizer)));

    private static string Replace(PhiCategory category, string original, TextContext? context = null, SurrogateReplacement? strategy = null) =>
        (strategy ?? SafeHarbor).Replace(new PhiSpan(0, original.Length, category, 1, "t"), original, context ?? new TextContext("patient-1"));

    [Theory]
    [InlineData(PhiCategory.Name, "Rosa Alvarez")]
    [InlineData(PhiCategory.MedicalRecordNumber, "4471-882-19")]
    [InlineData(PhiCategory.Phone, "617-555-0198")]
    [InlineData(PhiCategory.StreetAddress, "48 Willow Creek Rd")]
    public void The_same_original_always_gets_the_same_surrogate(PhiCategory category, string original) =>
        Assert.Equal(Replace(category, original), Replace(category, original));

    [Fact]
    public void Two_writings_of_the_same_identifier_get_the_same_surrogate()
    {
        // Case and spacing don't matter (the output's case follows the input's style),
        // nor do accents or an identifier's punctuation.
        Assert.Equal(Replace(PhiCategory.Name, "Rosa Alvarez").ToUpperInvariant(), Replace(PhiCategory.Name, "ROSA  ALVAREZ"));
        Assert.Equal(Replace(PhiCategory.Name, "José"), Replace(PhiCategory.Name, "Jose"));
        Assert.Equal(Replace(PhiCategory.Phone, "617-555-0198"), Replace(PhiCategory.Phone, "(617) 555 0198"));
    }

    [Fact]
    public void Surrogates_depend_on_the_key_and_the_subject()
    {
        var otherKey = new SurrogateReplacement(new HmacPseudonymizer(TestKeys.Other), YearOnlyDateTransformer.Instance);
        var originals = Enumerable.Range(0, 20).Select(i => $"Person{i} Surname{i}").ToList();

        Assert.NotEqual(originals.Select(o => Replace(PhiCategory.Name, o)), originals.Select(o => Replace(PhiCategory.Name, o, strategy: otherKey)));
        Assert.NotEqual(
            originals.Select(o => Replace(PhiCategory.Name, o, new TextContext("a"))),
            originals.Select(o => Replace(PhiCategory.Name, o, new TextContext("b"))));
    }

    [Fact]
    public void Each_part_of_a_name_maps_consistently_so_one_person_stays_one_person()
    {
        var context = new TextContext("p", KnownIdentifiers.Empty.AddName("Rosa Maria Alvarez-Nunez"));
        var full = Replace(PhiCategory.Name, "Rosa Maria Alvarez-Nunez", context).Split(' ');
        Assert.Equal(3, full.Length);
        Assert.Equal(full[2], Replace(PhiCategory.Name, "Alvarez-Nunez", context));
        Assert.Equal(full[0], Replace(PhiCategory.Name, "Rosa", context));
    }

    [Fact]
    public void Initials_stay_initials_and_capitals_stay_capitals()
    {
        var initial = Replace(PhiCategory.Name, "J. Smith");
        Assert.Matches("^[A-Z]\\. [A-Z][a-z]+$", initial);
        var upper = Replace(PhiCategory.Name, "JOHN SMITH");
        Assert.Equal(upper.ToUpperInvariant(), upper);
    }

    [Fact]
    public void A_surrogate_never_reuses_one_of_the_subjects_own_identifiers()
    {
        // Make the surrogate that would be chosen a known name of the subject: the
        // replacement must then pick something else.
        var plain = Replace(PhiCategory.Name, "Original Person", new TextContext("p"));
        var known = KnownIdentifiers.Empty.AddName(plain).AddName("Original Person");

        var avoided = Replace(PhiCategory.Name, "Original Person", new TextContext("p", known));

        Assert.NotEqual(plain, avoided);
        foreach (var part in plain.Split(' '))
        {
            Assert.DoesNotMatch($@"\b{Regex.Escape(part)}\b", avoided);
        }
    }

    [Theory]
    [InlineData("4471-882-19")]
    [InlineData("AB-12cd/9")]
    [InlineData("S99923841")]
    [InlineData("1")]
    public void Record_numbers_keep_their_shape_but_not_their_value(string original)
    {
        var fake = Replace(PhiCategory.MedicalRecordNumber, original);
        Assert.NotEqual(original, fake);
        Assert.Equal(original.Length, fake.Length);
        for (var i = 0; i < original.Length; i++)
        {
            Assert.Equal(char.IsAsciiDigit(original[i]), char.IsAsciiDigit(fake[i]));
            Assert.Equal(char.IsAsciiLetterUpper(original[i]), char.IsAsciiLetterUpper(fake[i]));
            Assert.Equal(char.IsAsciiLetterLower(original[i]), char.IsAsciiLetterLower(fake[i]));
            if (!char.IsAsciiLetterOrDigit(original[i]))
            {
                Assert.Equal(original[i], fake[i]);
            }
        }
    }

    [Fact]
    public void Contact_details_come_from_reserved_fictional_ranges()
    {
        Assert.Matches(@"^\(555\) 555-01\d\d$", Replace(PhiCategory.Phone, "617-555-0198"));
        Assert.Matches(@"^\(555\) 555-01\d\d$", Replace(PhiCategory.Fax, "617-555-0101"));
        Assert.Matches(@"^user\d{4}@example\.org$", Replace(PhiCategory.Email, "rosa@x.org"));
        Assert.Matches(@"^https://example\.org/[0-9a-f]{8}$", Replace(PhiCategory.Url, "https://me.example"));
        Assert.Matches(@"^192\.0\.2\.\d{1,3}$", Replace(PhiCategory.IpAddress, "10.0.0.1"));
        Assert.Matches(@"^000-\d\d-\d{4}$", Replace(PhiCategory.Ssn, "123-45-6789"));
    }

    [Fact]
    public void Places_and_organizations_get_invented_ones()
    {
        Assert.Matches(@"^\d+ [A-Z][a-z]+ [A-Z][a-z]+$", Replace(PhiCategory.StreetAddress, "48 Willow Creek Rd"));
        Assert.NotEqual("Springfield", Replace(PhiCategory.Location, "Springfield"));
        Assert.NotEqual("St. Brendan Hospital", Replace(PhiCategory.Organization, "St. Brendan Hospital"));
    }

    [Fact]
    public void Dates_keep_only_the_year_under_safe_harbor_and_shift_when_pseudonymized()
    {
        Assert.Equal("2024", Replace(PhiCategory.Date, "March 5, 2024"));
        var shifted = Replace(PhiCategory.Date, "March 5, 2024", strategy: Shifting);
        Assert.Matches(@"^\d{4}-\d\d-\d\d$", shifted);
        Assert.NotEqual("2024-03-05", shifted);
        Assert.Matches(@"^\d{4}-\d\d$", Replace(PhiCategory.Date, "March 2024", strategy: Shifting));
    }

    [Fact]
    public void A_date_without_a_year_becomes_a_tag() => Assert.Equal("[DATE]", Replace(PhiCategory.Date, "March 5"));

    [Fact]
    public void Ages_over_89_become_the_open_band() => Assert.Equal("90+", Replace(PhiCategory.Age, "93"));

    [Fact]
    public void Younger_ages_are_clinical_content_and_stay() => Assert.Equal("45", Replace(PhiCategory.Age, "45"));

    [Fact]
    public void An_age_that_is_not_a_plain_number_becomes_a_tag() => Assert.Equal("[AGE]", Replace(PhiCategory.Age, "45-year-old"));

    [Fact]
    public void The_top_code_age_comes_from_the_age_generalizer()
    {
        var strategy = new SurrogateReplacement(Pseudonymizer, YearOnlyDateTransformer.Instance, ages: new AgeGeneralizer(topCodeAge: 80));
        Assert.Equal("80+", Replace(PhiCategory.Age, "85", strategy: strategy));
        Assert.Equal("79", Replace(PhiCategory.Age, "79", strategy: strategy));
    }

    [Fact]
    public void A_name_never_maps_to_itself_in_whole_or_in_part()
    {
        // Every first and last name the surrogate lists can produce: none may survive.
        foreach (var first in new[] { "James", "Mary", "Mark", "Lisa", "Brian" })
        {
            foreach (var last in new[] { "Bennett", "Pierce", "Nolan", "Finch" })
            {
                var fake = Replace(PhiCategory.Name, $"{first} {last}");
                Assert.DoesNotContain(first, fake, StringComparison.Ordinal);
                Assert.DoesNotContain(last, fake, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void A_place_or_organization_never_maps_to_itself()
    {
        foreach (var city in new[] { "Riverton", "Lakeview", "Fairhaven", "Brookfield", "Easton" })
        {
            Assert.NotEqual(city, Replace(PhiCategory.Location, city));
        }

        Assert.NotEqual("Northfield Clinic", Replace(PhiCategory.Organization, "Northfield Clinic"));
    }

    [Theory]
    [InlineData("02115", "021XX")]
    [InlineData("03601-1234", "000XX")]
    [InlineData("K1A 0B1", "[ZIP]")]
    public void Zip_codes_keep_the_safe_harbor_prefix(string zip, string expected) => Assert.Equal(expected, Replace(PhiCategory.ZipCode, zip));

    [Theory]
    [InlineData(PhiCategory.Biometric, "[BIOMETRIC]")]
    [InlineData(PhiCategory.Photo, "[PHOTO]")]
    public void Kinds_with_no_sensible_fake_become_tags(PhiCategory category, string expected) => Assert.Equal(expected, Replace(category, "x"));

    [Fact]
    public void Rejects_missing_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new SurrogateReplacement(null!, YearOnlyDateTransformer.Instance));
        Assert.Throws<ArgumentNullException>(() => new SurrogateReplacement(Pseudonymizer, null!));
    }
}
