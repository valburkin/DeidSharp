using Hl7.Fhir.Model;
using static DeidSharp.Tests.Fhir.FhirTestRunner;

namespace DeidSharp.Tests.Fhir.Rules;

/// <summary>Edge cases of the individual rules, driven through the de-identifier.</summary>
public class ElementRuleTests
{
    [Theory]
    [InlineData(91, "a", true)]
    [InlineData(89, "a", false)]
    [InlineData(1100, "mo", true)]
    [InlineData(1000, "mo", false)]
    [InlineData(4800, "wk", true)]
    [InlineData(40000, "d", true)]
    [InlineData(95, "years", true)]
    public async Task Ages_are_top_coded_in_any_unit(decimal value, string code, bool topCoded)
    {
        var condition = new Condition { Id = "c", Onset = new Age { Value = value, Code = code, Unit = code } };
        var age = Assert.IsType<Age>((await RunAs<Condition>(condition)).Onset);

        Assert.Equal(topCoded ? 90m : value, age.Value);
        Assert.Equal(topCoded ? Quantity.QuantityComparator.GreaterOrEqual : null, age.Comparator);
    }

    [Fact]
    public async Task An_age_with_a_value_but_no_known_unit_is_removed()
    {
        var condition = new Condition { Id = "c", Onset = new Age { Value = 50, Unit = "fortnights" } };
        Assert.Null((await RunAs<Condition>(condition)).Onset);
    }

    [Fact]
    public async Task An_unreadable_date_is_removed()
    {
        var observation = new Observation { Id = "o", Effective = new FhirDateTime { JsonValue = "sometime in March" } };
        Assert.Null((await RunAs<Observation>(observation)).Effective);
    }

    [Fact]
    public async Task An_unreadable_birth_date_is_removed()
    {
        var patient = new Patient { Id = "p", BirthDateElement = new Date { JsonValue = "unknown" } };
        Assert.Null((await RunAs<Patient>(patient)).BirthDate);
    }

    [Fact]
    public async Task Text_attachments_of_any_text_type_are_cleaned()
    {
        var document = new DocumentReference { Id = "d" };
        document.Content.Add(new DocumentReference.ContentComponent
        {
            Attachment = new Attachment { ContentType = "text/html", Data = [.. "<p>SSN 123-45-6789</p>"u8] },
        });

        var result = await RunAs<DocumentReference>(document);

        Assert.Equal("<p>SSN [SSN]</p>", AttachmentText(result));
    }

    [Fact]
    public async Task A_text_attachment_that_is_not_valid_utf8_is_removed()
    {
        var document = new DocumentReference { Id = "d" };
        document.Content.Add(new DocumentReference.ContentComponent
        {
            Attachment = new Attachment { ContentType = "text/plain", Data = [0xC3, 0x28, 0xA0, 0xA1] },
        });

        Assert.Empty((await RunAs<DocumentReference>(document)).Content);
    }

    [Fact]
    public async Task An_attachment_outside_a_document_is_removed_when_it_is_not_text()
    {
        var patient = new Patient { Id = "p" };
        patient.Photo.Add(new Attachment { ContentType = "image/png", Data = [1, 2, 3] });
        var media = new Communication { Id = "c" };
        media.Payload.Add(new Communication.PayloadComponent { Content = new Attachment { ContentType = "application/pdf", Url = "http://x/y.pdf" } });

        Assert.Empty((await RunAs<Patient>(patient)).Photo);
        Assert.Empty((await RunAs<Communication>(media)).Payload);
    }

    [Fact]
    public async Task An_address_with_nothing_left_after_generalizing_is_removed()
    {
        var patient = new Patient { Id = "p" };
        patient.Address.Add(new Address { Line = ["1 Main St"], City = "Springfield" });
        patient.Address.Add(new Address { PostalCode = "K1A 0B1", Country = "CA" });

        var result = await RunAs<Patient>(patient);

        var address = Assert.Single(result.Address);
        Assert.Equal("CA", address.Country);
        Assert.Null(address.PostalCode);
    }

    [Fact]
    public async Task Element_ids_on_data_types_are_kept_as_local_labels()
    {
        var observation = new Observation { Id = "o", Code = new CodeableConcept { ElementId = "code-1", Text = "Glucose" } };
        Assert.Equal("code-1", (await RunAs<Observation>(observation)).Code.ElementId);
    }

    [Fact]
    public async Task A_reference_with_neither_target_nor_text_is_removed()
    {
        var observation = new Observation { Id = "o", Subject = new ResourceReference { Type = "Patient" } };
        Assert.Null((await RunAs<Observation>(observation)).Subject);
    }

    [Fact]
    public async Task A_reference_that_cannot_be_rewritten_is_removed()
    {
        var observation = new Observation { Id = "o", Subject = new ResourceReference("not a reference at all") };
        Assert.Null((await RunAs<Observation>(observation)).Subject);
    }

    [Fact]
    public async Task Coded_values_are_kept_whole_even_when_their_text_looks_like_phi()
    {
        var observation = new Observation
        {
            Id = "o",
            Code = new CodeableConcept { Coding = [new Coding("http://loinc.org", "1234-5", "Specimen 2024-03-05 per Dr. Hodgkin")] },
        };

        var coding = (await RunAs<Observation>(observation)).Code.Coding.Single();

        Assert.Equal("Specimen 2024-03-05 per Dr. Hodgkin", coding.Display);
    }

    [Fact]
    public async Task Markdown_is_de_identified_too()
    {
        var observation = new Observation { Id = "o" };
        observation.Note.Add(new Annotation { Text = "Call **617-555-0198**" });

        var note = (await RunAs<Observation>(observation)).Note.Single();

        Assert.IsType<Markdown>(note.TextElement);
        Assert.Equal("Call **[PHONE]**", note.Text);
    }
}
