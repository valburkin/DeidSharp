using System.Text;
using Hl7.Fhir.Model;
using DeidSharp.Fhir;
using DeidSharp.Fhir.References;
using static DeidSharp.Tests.Fhir.FhirTestRunner;

namespace DeidSharp.Tests.Fhir;

/// <summary>Leaks found in review, each reduced to the smallest resource that showed it.</summary>
public class FhirLeakRegressionTests
{
    private const string Note = "Patient John Smith SSN 123-45-6789 seen 2024-03-05";

    private static DocumentReference Document(string contentType, byte[] data)
    {
        var document = new DocumentReference { Id = "d" };
        document.Content.Add(new DocumentReference.ContentComponent { Attachment = new Attachment { ContentType = contentType, Data = data } });
        return document;
    }

    [Theory]
    [InlineData("text/plain; charset=utf-16")]
    [InlineData("text/plain; charset=UTF-16LE")]
    [InlineData("text/plain;charset=\"iso-8859-1\"")]
    public async Task Text_in_a_charset_other_than_utf8_is_removed_not_passed_through(string contentType)
    {
        var encoding = contentType.Contains("8859", StringComparison.Ordinal) ? Encoding.Latin1 : Encoding.Unicode;
        var result = await RunAs<DocumentReference>(Document(contentType, encoding.GetBytes(Note)));
        Assert.Empty(result.Content);
    }

    [Fact]
    public async Task Text_holding_nul_or_other_control_characters_is_removed()
    {
        var result = await RunAs<DocumentReference>(Document("text/plain", [.. "J\0o\0h\0n\0 \0S\0m\0i\0t\0h\0"u8]));
        Assert.Empty(result.Content);
    }

    [Theory]
    [InlineData("text/plain; charset=utf-8")]
    [InlineData("text/plain; charset=\"UTF-8\"")]
    [InlineData("text/markdown")]
    public async Task Utf8_text_is_cleaned_and_kept(string contentType)
    {
        var result = await RunAs<DocumentReference>(Document(contentType, Encoding.UTF8.GetBytes(Note)));
        Assert.Equal("Patient John Smith SSN [SSN] seen [DATE]", AttachmentText(result));
    }

    [Fact]
    public async Task Extensions_inside_codings_and_quantities_are_removed()
    {
        var coding = new Coding("http://loinc.org", "2160-0", "Creatinine");
        coding.Extension.Add(new Extension("http://evil/x", new FhirString("Jane Q Public 617-555-0198")));
        var quantity = new Quantity(2.1m, "mg/dL");
        quantity.Extension.Add(new Extension("http://evil/y", new FhirString("SSN 123-45-6789")));
        var observation = new Observation { Id = "o", Code = new CodeableConcept { Coding = [coding] }, Value = quantity };

        var result = await Run(observation);
        var json = FhirFixtures.ToJson(result.Resource);

        Assert.DoesNotContain("617-555-0198", json, StringComparison.Ordinal);
        Assert.DoesNotContain("123-45-6789", json, StringComparison.Ordinal);
        var cleaned = Assert.IsType<Observation>(result.Resource);
        Assert.Equal("2160-0", cleaned.Code.Coding.Single().Code);
        Assert.Equal(2.1m, Assert.IsType<Quantity>(cleaned.Value).Value);
    }

    [Fact]
    public async Task An_unknown_extension_on_a_kept_primitive_is_removed_instead_of_failing_the_run()
    {
        var patient = new Patient { Id = "p", Gender = AdministrativeGender.Female };
        patient.GenderElement!.Extension.Add(new Extension("http://evil/z", new FhirString("Jane Q Public")));

        var result = await RunAs<Patient>(patient);

        Assert.Equal(AdministrativeGender.Female, result.Gender);
        Assert.Empty(result.GenderElement!.Extension);
    }

    [Fact]
    public async Task An_allowed_extension_on_a_kept_string_is_still_cleaned()
    {
        const string translation = "http://hl7.org/fhir/StructureDefinition/translation";
        var observation = new Observation { Id = "o", Value = new FhirString("Stable") };
        var extension = new Extension { Url = translation };
        extension.Extension.Add(new Extension("content", new FhirString("Call 617-555-0198")));
        observation.Value.Extension.Add(extension);
        var options = SafeHarbor(o => o with { AllowedExtensions = new HashSet<string> { translation } });

        var result = await RunAs<Observation>(observation, options);

        var content = Assert.IsType<FhirString>(result.Value!.Extension.Single().Extension.Single().Value);
        Assert.Equal("Call [PHONE]", content.Value);
    }

    [Fact]
    public async Task Uris_that_describe_rather_than_define_are_removed()
    {
        var device = new Device { Id = "d", Url = "http://10.1.2.3/pump/jdoe" };
        var endpoint = new Endpoint { Id = "e", Address = "https://ehr.stmarys.example/patients/jane.doe" };

        Assert.Null((await RunAs<Device>(device)).Url);
        Assert.Null((await RunAs<Endpoint>(endpoint)).Address);
    }

    [Fact]
    public async Task Defining_uris_are_kept()
    {
        var questionnaire = new Questionnaire { Id = "q", Url = "http://example.org/Questionnaire/phq9", Status = PublicationStatus.Active };
        Assert.Equal("http://example.org/Questionnaire/phq9", (await RunAs<Questionnaire>(questionnaire)).Url);
    }

    [Fact]
    public async Task A_server_address_and_path_in_full_urls_and_references_never_survive()
    {
        var bundle = new Bundle { Type = Bundle.BundleType.Collection };
        bundle.Entry.Add(new Bundle.EntryComponent
        {
            FullUrl = "https://ehr.stmarys.example/patients/jane.doe.1950/Patient/123",
            Resource = new Patient { Id = "123" },
        });
        bundle.Entry.Add(new Bundle.EntryComponent
        {
            FullUrl = "https://ehr.stmarys.example/patients/jane.doe.1950/Observation/9",
            Resource = new Observation { Id = "9", Subject = new ResourceReference("https://ehr.stmarys.example/patients/jane.doe.1950/Patient/123") },
        });

        var result = await Run(bundle);
        var json = FhirFixtures.ToJson(result.Resource);

        Assert.DoesNotContain("stmarys", json, StringComparison.Ordinal);
        Assert.DoesNotContain("jane", json, StringComparison.OrdinalIgnoreCase);
        var output = Assert.IsType<Bundle>(result.Resource);
        Assert.Equal(output.Entry[0].FullUrl, Assert.IsType<Observation>(output.Entry[1].Resource).Subject!.Reference);
        Assert.StartsWith(ReferenceRewriter.PlaceholderBase, output.Entry[0].FullUrl, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(95, "a", true)]
    [InlineData(95, "years", true)]
    [InlineData(89, "a", false)]
    [InlineData(95, "mg", false)]
    public async Task An_age_kept_as_a_plain_quantity_in_years_is_top_coded(decimal value, string unit, bool topCoded)
    {
        var observation = new Observation { Id = "o", Value = new Quantity { Value = value, Code = unit, Unit = unit } };

        var quantity = Assert.IsType<Quantity>((await RunAs<Observation>(observation)).Value);

        Assert.Equal(topCoded ? 90m : value, quantity.Value);
    }

    [Fact]
    public async Task Plan_numbers_kept_as_plain_strings_are_removed()
    {
        var coverage = new Coverage { Id = "c", Dependent = "03" };
        coverage.Class.Add(new Coverage.ClassComponent { Type = new CodeableConcept("http://terminology.hl7.org/CodeSystem/coverage-class", "group"), Value = "GRP-998877" });
        var claim = new Claim { Id = "cl" };
        claim.Insurance.Add(new Claim.InsuranceComponent { Sequence = 1, Focal = true, PreAuthRef = ["PA123456"] });

        var json = FhirFixtures.ToJson((await Run(coverage)).Resource) + FhirFixtures.ToJson((await Run(claim)).Resource);

        Assert.DoesNotContain("GRP-998877", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PA123456", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"03\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Georgia", "Georgia")]
    [InlineData("Al", "AL")]
    [InlineData("Virginia", "Virginia")]
    public async Task A_state_that_shares_a_word_with_the_patients_name_is_not_a_leak(string given, string state)
    {
        var patient = new Patient { Id = "p" };
        patient.Name.Add(new HumanName { Given = [given], Family = "Smith" });
        patient.Address.Add(new Address { State = state, City = "Atlanta" });

        var result = await RunAs<Patient>(patient);

        Assert.Equal(state, result.Address.Single().State);
    }

    [Fact]
    public async Task Invalid_json_is_rejected_without_quoting_the_input()
    {
        const string json = """{"resourceType":"Patient","birthDate":"John Smith 1950-13-45","gender":"SSN 123-45-6789"}""";
        var deidentifier = new FhirDeidentifier(SafeHarbor());

        var ex = await Assert.ThrowsAsync<FhirInputException>(() => deidentifier.DeidentifyJsonAsync(json, TestContext.Current.CancellationToken));

        Assert.DoesNotContain("John", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("123-45", ex.Message, StringComparison.Ordinal);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public async Task The_same_patients_dates_shift_the_same_whether_or_not_the_patient_is_in_the_export()
    {
        var withPatient = new Bundle { Type = Bundle.BundleType.Collection };
        withPatient.Entry.Add(new Bundle.EntryComponent { FullUrl = "urn:uuid:p1", Resource = new Patient { Id = "p1" } });
        withPatient.Entry.Add(new Bundle.EntryComponent { FullUrl = "urn:uuid:o", Resource = Observation() });
        var alone = new Bundle { Type = Bundle.BundleType.Collection };
        alone.Entry.Add(new Bundle.EntryComponent { FullUrl = "urn:uuid:o", Resource = Observation() });

        var first = await RunAs<Bundle>(withPatient, Pseudonymized());
        var second = await RunAs<Bundle>(alone, Pseudonymized());

        Assert.Equal(
            Assert.IsType<FhirDateTime>(Entry<Observation>(first).Effective).Value,
            Assert.IsType<FhirDateTime>(Entry<Observation>(second).Effective).Value);
        return;

        static Observation Observation() =>
            new() { Id = "o", Subject = new ResourceReference("Patient/p1"), Effective = new FhirDateTime("2024-03-05") };
    }
}
