using Hl7.Fhir.Model;
using DeidSharp.Fhir.References;
using DeidSharp.Pseudonymization;
using DeidSharp.Tests.TestDoubles;

namespace DeidSharp.Tests.Fhir.References;

public class ReferenceRewriterTests
{
    private static readonly ReferenceRewriter Rewriter = new(new HmacPseudonymizer(TestKeys.Fixed));

    private static string Id(string id) => Rewriter.PseudonymizeId(id);

    [Fact]
    public void Every_way_of_naming_a_resource_maps_to_the_same_pseudonym()
    {
        var pseudonym = Id("123");
        Assert.Equal($"Patient/{pseudonym}", Rewriter.Rewrite("Patient/123"));
        Assert.Equal($"urn:uuid:{pseudonym}", Rewriter.Rewrite("urn:uuid:123"));
        Assert.Equal($"{ReferenceRewriter.PlaceholderBase}Patient/{pseudonym}", Rewriter.Rewrite("https://ehr.example/fhir/Patient/123/_history/7"));
        Assert.Equal($"Patient/{pseudonym}", Rewriter.Rewrite("  Patient/123  "));
    }

    [Fact]
    public void The_source_servers_address_and_path_never_survive()
    {
        var rewritten = Rewriter.Rewrite("https://ehr.stmarys.example/patients/jane.doe.1950/Patient/123");
        Assert.Equal($"{ReferenceRewriter.PlaceholderBase}Patient/{Id("123")}", rewritten);
        Assert.Equal(rewritten, Rewriter.Rewrite("http://10.1.2.3:8080/fhir/Patient/123"));
    }

    [Fact]
    public void Pseudonyms_are_uuids_and_valid_fhir_ids()
    {
        Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", Id("anything"));
        Assert.NotEqual(Id("1"), Id("2"));
    }

    [Theory]
    [InlineData("#med1", "#med1")]
    [InlineData("Patient", "Patient")]
    public void Local_references_and_bare_types_are_kept(string reference, string expected) => Assert.Equal(expected, Rewriter.Rewrite(reference));

    [Fact]
    public void An_oid_becomes_a_uuid_pseudonym() =>
        Assert.StartsWith("urn:uuid:", Rewriter.Rewrite("urn:oid:1.2.3.4"), StringComparison.Ordinal);

    [Fact]
    public void An_unresolvable_conditional_reference_points_at_a_pseudonym_of_its_query()
    {
        var rewritten = Rewriter.Rewrite("Practitioner?identifier=http://hl7.org/fhir/sid/us-npi|9999912345");
        Assert.StartsWith("Practitioner/", rewritten, StringComparison.Ordinal);
        Assert.DoesNotContain("9999912345", rewritten, StringComparison.Ordinal);
        Assert.Equal(rewritten, Rewriter.Rewrite("Practitioner?identifier=http://hl7.org/fhir/sid/us-npi|9999912345"));
    }

    [Theory]
    [InlineData("Practitioner?identifier=http://npi|42")]
    [InlineData("Practitioner?identifier=http://npi%7C42")]
    [InlineData("Practitioner?identifier=42")]
    public void A_conditional_reference_resolves_to_the_bundle_entry_carrying_that_identifier(string reference)
    {
        var bundle = new Bundle();
        var practitioner = new Practitioner { Id = "pr-1" };
        practitioner.Identifier.Add(new Identifier("http://npi", "42"));
        bundle.Entry.Add(new Bundle.EntryComponent { FullUrl = "urn:uuid:pr-1", Resource = practitioner });
        var rewriter = new ReferenceRewriter(new HmacPseudonymizer(TestKeys.Fixed), bundle);

        Assert.Equal($"urn:uuid:{Id("pr-1")}", rewriter.Rewrite(reference));
    }

    [Fact]
    public void Without_a_full_url_the_entry_is_named_by_type_and_id()
    {
        var bundle = new Bundle();
        var organization = new Organization { Id = "org-9" };
        organization.Identifier.Add(new Identifier("http://org", "X1"));
        bundle.Entry.Add(new Bundle.EntryComponent { Resource = organization });

        Assert.Equal($"Organization/{Id("org-9")}", new ReferenceRewriter(new HmacPseudonymizer(TestKeys.Fixed), bundle).Rewrite("Organization?identifier=http://org|X1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a reference")]
    [InlineData("patient/123")]
    [InlineData("Patient/has spaces")]
    public void Anything_else_cannot_be_kept(string? reference) => Assert.Null(Rewriter.Rewrite(reference));

    [Fact]
    public void Rejects_a_blank_id() => Assert.Throws<ArgumentException>(() => Rewriter.PseudonymizeId(" "));
}
