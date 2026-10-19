using Hl7.Fhir.Model;
using DeidSharp.Detection;
using DeidSharp.Fhir.Known;

namespace DeidSharp.Tests.Fhir.Known;

public class FhirKnownIdentifierExtractorTests
{
    private static readonly KnownIdentifiers Known = FhirKnownIdentifierExtractor.Extract(FhirFixtures.RichPatientBundle());

    private static List<string> Values(PhiCategory category) => [.. Known.Items.Where(i => i.Category == category).Select(i => i.Value)];

    [Fact]
    public void Collects_every_persons_name_including_relatives_staff_and_maiden_names()
    {
        var names = Values(PhiCategory.Name);
        Assert.Contains("Rosa María Álvarez-Núñez", names);
        Assert.Contains("Carmen Alvarez", names);
        Assert.Contains("Amelia Hart", names);
        Assert.Contains("Lucia Ferreira", names);
    }

    [Fact]
    public void Collects_contact_details_by_kind()
    {
        Assert.Contains("617-555-0198", Values(PhiCategory.Phone));
        Assert.Contains("617-555-0142", Values(PhiCategory.Phone));
        Assert.Contains("rosa.alvarez@mailbox.example", Values(PhiCategory.Email));
    }

    [Fact]
    public void Collects_address_parts()
    {
        Assert.Contains("48 Willow Creek Rd", Values(PhiCategory.StreetAddress));
        Assert.Contains("Springfield", Values(PhiCategory.Location));
        Assert.Contains("Ponce", Values(PhiCategory.Location));
        Assert.Contains("01103", Values(PhiCategory.ZipCode));
    }

    [Fact]
    public void Files_identifiers_under_the_category_their_type_or_system_implies()
    {
        Assert.Contains("4471-882-19", Values(PhiCategory.MedicalRecordNumber));
        Assert.Contains("123-45-6789", Values(PhiCategory.Ssn));
        Assert.Contains("S99923841", Values(PhiCategory.LicenseNumber));
        Assert.Contains("9999912345", Values(PhiCategory.LicenseNumber));
        Assert.Contains("org-77", Values(PhiCategory.OtherIdentifier));
    }

    [Fact]
    public void Collects_full_birth_dates_and_site_names()
    {
        Assert.Contains("1931-03-04", Values(PhiCategory.Date));
        Assert.Contains("St. Brendan Hospital", Values(PhiCategory.Organization));
        Assert.Contains("St. Brendan Hospital - Ward 4B", Values(PhiCategory.Location));
    }

    [Fact]
    public void Partial_birth_dates_are_not_collected_as_known_dates()
    {
        var known = FhirKnownIdentifierExtractor.Extract(new Patient { BirthDate = "1931-03" });
        Assert.DoesNotContain(known.Items, i => i.Category == PhiCategory.Date);
    }

    [Fact]
    public void A_name_written_only_as_text_is_collected()
    {
        var patient = new Patient();
        patient.Name.Add(new HumanName { Text = "Dr. Who" });
        Assert.Contains("Dr. Who", FhirKnownIdentifierExtractor.Extract(patient).Items.Select(i => i.Value));
    }
}
