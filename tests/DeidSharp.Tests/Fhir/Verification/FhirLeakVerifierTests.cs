using Hl7.Fhir.Model;
using DeidSharp.Detection;
using DeidSharp.Fhir;
using DeidSharp.Fhir.Verification;

namespace DeidSharp.Tests.Fhir.Verification;

public class FhirLeakVerifierTests
{
    private static readonly FhirDeidentificationOptions SafeHarbor = new();
    private static readonly FhirDeidentificationOptions Pseudonymized = new() { Mode = FhirDeidentificationMode.Pseudonymized };

    private static List<string> Checks(Resource resource, FhirDeidentificationOptions? options = null, KnownIdentifiers? known = null) =>
        [.. FhirLeakVerifier.Verify(resource, known ?? KnownIdentifiers.Empty, options ?? SafeHarbor).Findings.SelectMany(f => f.Sources)];

    [Fact]
    public void A_clean_resource_passes() =>
        Assert.Empty(Checks(new Patient { Id = "x", Gender = AdministrativeGender.Female, BirthDate = "1960" }));

    [Fact]
    public void Flags_names_and_contact_points()
    {
        var patient = new Patient();
        patient.Name.Add(new HumanName { Family = "Doe" });
        patient.Telecom.Add(new ContactPoint(ContactPoint.ContactPointSystem.Phone, null, "555"));

        Assert.Equal(["structure:name", "structure:contact-point"], Checks(patient));
    }

    [Fact]
    public void Flags_an_address_that_was_not_cut_down()
    {
        var patient = new Patient();
        patient.Address.Add(new Address { City = "Springfield" });
        patient.Address.Add(new Address { PostalCode = "02115" });
        patient.Address.Add(new Address { PostalCode = "021", State = "MA" });

        Assert.Equal(["structure:address", "structure:address"], Checks(patient));
    }

    [Fact]
    public void Flags_identifiers_instants_and_fine_dates_only_under_safe_harbor()
    {
        var observation = new Observation { Effective = new FhirDateTime("2024-03-05"), Issued = DateTimeOffset.UtcNow };
        observation.Identifier.Add(new Identifier("s", "v"));

        Assert.Equal(3, Checks(observation).Count);
        Assert.Empty(Checks(observation, Pseudonymized));
    }

    [Fact]
    public void Flags_unallowed_extensions_but_not_sub_extensions_of_allowed_ones()
    {
        var patient = new Patient();
        var race = new Extension { Url = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-race" };
        race.Extension.Add(new Extension("ombCategory", new Coding("s", "c")));
        patient.Extension.Add(race);
        patient.Extension.Add(new Extension("http://example.org/secret", new FhirString("x")));

        Assert.Equal(["structure:extension"], Checks(patient));
    }

    [Fact]
    public void Flags_attachments_that_are_not_inline_text()
    {
        var document = new DocumentReference();
        document.Content.Add(new DocumentReference.ContentComponent { Attachment = new Attachment { ContentType = "application/pdf", Data = [1] } });
        document.Content.Add(new DocumentReference.ContentComponent { Attachment = new Attachment { ContentType = "text/plain", Url = "http://x" } });

        Assert.Equal(["structure:attachment", "structure:attachment"], Checks(document));
    }

    [Fact]
    public void Flags_a_known_identifier_in_a_string_or_a_text_attachment()
    {
        var known = KnownIdentifiers.Empty.AddName("Rosa Alvarez");
        var document = new DocumentReference { Description = "for Alvarez" };
        document.Content.Add(new DocumentReference.ContentComponent
        {
            Attachment = new Attachment { ContentType = "text/plain", Data = [.. "Rosa was seen"u8] },
        });

        Assert.Equal(["known", "known"], Checks(document, known: known));
    }

    [Fact]
    public void Does_not_scan_coded_values_or_element_ids()
    {
        var known = KnownIdentifiers.Empty.AddName("Hodgkin Smith");
        var condition = new Condition { Code = new CodeableConcept { Coding = [new Coding("s", "c", "Hodgkin lymphoma")] } };
        condition.Code.ElementId = "Hodgkin";

        Assert.Empty(Checks(condition, known: known));
    }
}
