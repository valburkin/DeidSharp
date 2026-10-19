using DeidSharp.Detection;

namespace DeidSharp.Tests.Detection;

public class KnownIdentifiersTests
{
    [Fact]
    public void Adding_returns_a_new_set_and_leaves_the_original_alone()
    {
        var empty = KnownIdentifiers.Empty;
        var one = empty.AddName("Jane Doe");

        Assert.Equal(0, empty.Count);
        Assert.Equal(1, one.Count);
        Assert.NotSame(empty, one);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_values_are_ignored(string? value) => Assert.Same(KnownIdentifiers.Empty, KnownIdentifiers.Empty.AddName(value));

    [Fact]
    public void A_duplicate_is_kept_once_and_values_are_trimmed()
    {
        var known = KnownIdentifiers.Empty.AddPhone(" 555-0100 ").AddPhone("555-0100");
        Assert.Equal(new KnownIdentifier(PhiCategory.Phone, "555-0100"), Assert.Single(known.Items));
    }

    [Fact]
    public void The_same_value_in_two_categories_is_two_identifiers() =>
        Assert.Equal(2, KnownIdentifiers.Empty.AddIdentifier("12345678", PhiCategory.MedicalRecordNumber).AddPhone("12345678").Count);

    [Fact]
    public void Dates_are_stored_in_iso_form() =>
        Assert.Equal("1931-03-04", Assert.Single(KnownIdentifiers.Empty.AddDate(new DateOnly(1931, 3, 4)).Items).Value);

    [Fact]
    public void Every_convenience_method_files_the_value_under_its_category()
    {
        var known = KnownIdentifiers.Empty
            .AddName("n").AddPhone("p").AddEmail("e").AddStreetAddress("a").AddLocation("l").AddOrganization("o")
            .AddIdentifier("i").AddIdentifier("m", PhiCategory.MedicalRecordNumber);

        Assert.Equal(
            [PhiCategory.Name, PhiCategory.Phone, PhiCategory.Email, PhiCategory.StreetAddress, PhiCategory.Location,
             PhiCategory.Organization, PhiCategory.OtherIdentifier, PhiCategory.MedicalRecordNumber],
            known.Items.Select(i => i.Category));
    }

    [Fact]
    public void Merging_unions_both_sets_without_duplicates()
    {
        var a = KnownIdentifiers.Empty.AddName("A").AddName("B");
        var b = KnownIdentifiers.Empty.AddName("B").AddName("C");

        Assert.Equal(["A", "B", "C"], a.Merge(b).Items.Select(i => i.Value));
    }
}
