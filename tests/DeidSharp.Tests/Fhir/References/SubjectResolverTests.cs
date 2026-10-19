using Hl7.Fhir.Model;
using DeidSharp.Fhir.References;

namespace DeidSharp.Tests.Fhir.References;

public class SubjectResolverTests
{
    private static Bundle TwoPatients()
    {
        var bundle = new Bundle();
        bundle.Entry.Add(new Bundle.EntryComponent { FullUrl = "urn:uuid:a", Resource = new Patient { Id = "a" } });
        bundle.Entry.Add(new Bundle.EntryComponent { FullUrl = "https://ehr.example/fhir/Patient/b", Resource = new Patient { Id = "b" } });
        return bundle;
    }

    [Fact]
    public void A_patient_is_its_own_subject()
    {
        var bundle = TwoPatients();
        var resolver = new SubjectResolver(bundle);
        Assert.Equal("a", resolver.SubjectOf(bundle.Entry[0].Resource!));
        Assert.Equal("b", resolver.SubjectOf(bundle.Entry[1].Resource!));
    }

    [Theory]
    [InlineData("urn:uuid:a", "a")]
    [InlineData("Patient/a", "a")]
    [InlineData("Patient/b", "b")]
    [InlineData("https://ehr.example/fhir/Patient/b", "b")]
    [InlineData("https://other.example/fhir/Patient/a", "a")]
    [InlineData("Patient/z", "z")]
    [InlineData("https://ehr.example/fhir/Patient/z/_history/3", "z")]
    public void A_resource_belongs_to_the_patient_its_subject_points_at(string reference, string expected) =>
        Assert.Equal(expected, new SubjectResolver(TwoPatients()).SubjectOf(new Observation { Subject = new ResourceReference(reference) }));

    [Fact]
    public void Patient_and_beneficiary_references_count_too()
    {
        var resolver = new SubjectResolver(TwoPatients());
        Assert.Equal("b", resolver.SubjectOf(new Device { Patient = new ResourceReference("Patient/b") }));
        Assert.Equal("a", resolver.SubjectOf(new Coverage { Beneficiary = new ResourceReference("urn:uuid:a") }));
    }

    [Fact]
    public void A_resource_with_no_patient_reference_belongs_to_the_only_patient_or_has_a_key_of_its_own()
    {
        var single = new Bundle();
        single.Entry.Add(new Bundle.EntryComponent { FullUrl = "urn:uuid:only", Resource = new Patient { Id = "only" } });
        var resolver = new SubjectResolver(TwoPatients());

        Assert.Equal("only", new SubjectResolver(single).SubjectOf(new Organization()));
        var first = resolver.SubjectOf(new Organization { Id = "o1" });
        var second = resolver.SubjectOf(new Organization { Id = "o2" });
        Assert.StartsWith("unattributed:", first, StringComparison.Ordinal);
        Assert.NotEqual(first, second);
        Assert.NotEqual(resolver.SubjectOf(new Organization()), resolver.SubjectOf(new Organization()));
    }

    [Fact]
    public void A_patient_gets_the_same_key_inside_or_outside_the_bundle()
    {
        var inside = new Bundle();
        inside.Entry.Add(new Bundle.EntryComponent { FullUrl = "urn:uuid:p1", Resource = new Patient { Id = "p1" } });
        var observation = new Observation { Subject = new ResourceReference("Patient/p1") };

        Assert.Equal(new SubjectResolver(inside).SubjectOf(observation), new SubjectResolver(new Bundle()).SubjectOf(observation));
    }

    [Fact]
    public void A_patient_without_an_id_is_keyed_by_its_full_url()
    {
        var bundle = new Bundle();
        bundle.Entry.Add(new Bundle.EntryComponent { FullUrl = "urn:uuid:abc", Resource = new Patient() });
        var observation = new Observation { Subject = new ResourceReference("urn:uuid:abc") };
        bundle.Entry.Add(new Bundle.EntryComponent { Resource = observation });

        Assert.Equal("abc", new SubjectResolver(bundle).SubjectOf(observation));
    }

    [Fact]
    public void A_contained_resource_belongs_to_its_containers_patient()
    {
        var bundle = TwoPatients();
        var medication = new Medication { Id = "m" };
        var request = new MedicationRequest { Subject = new ResourceReference("Patient/b") };
        request.Contained.Add(medication);
        bundle.Entry.Add(new Bundle.EntryComponent { Resource = request });

        Assert.Equal("b", new SubjectResolver(bundle).SubjectOf(medication));
    }

    [Fact]
    public void Subjects_are_resolved_before_references_are_rewritten()
    {
        var bundle = TwoPatients();
        var observation = new Observation { Subject = new ResourceReference("Patient/b") };
        bundle.Entry.Add(new Bundle.EntryComponent { Resource = observation });
        var resolver = new SubjectResolver(bundle);

        observation.Subject = new ResourceReference("Patient/pseudonym");

        Assert.Equal("b", resolver.SubjectOf(observation));
    }

    [Fact]
    public void A_lone_patient_resource_is_its_own_subject()
    {
        var patient = new Patient { Id = "solo" };
        Assert.Equal("solo", new SubjectResolver(patient).SubjectOf(patient));
    }
}
