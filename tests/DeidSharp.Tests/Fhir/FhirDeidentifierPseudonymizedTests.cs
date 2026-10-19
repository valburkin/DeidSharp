using System.Globalization;
using Hl7.Fhir.Model;
using DeidSharp.Fhir;
using DeidSharp.Tests.TestDoubles;
using static DeidSharp.Tests.Fhir.FhirTestRunner;

namespace DeidSharp.Tests.Fhir;

public class FhirDeidentifierPseudonymizedTests
{
    private static async Task<Bundle> RichBundle(FhirDeidentificationOptions? options = null) =>
        await RunAs<Bundle>(FhirFixtures.RichPatientBundle(), options ?? Pseudonymized());

    [Fact]
    public void Needs_a_key() =>
        Assert.Throws<ArgumentException>(() => new FhirDeidentifier(new FhirDeidentificationOptions { Mode = FhirDeidentificationMode.Pseudonymized }));

    [Fact]
    public async Task Identifiers_keep_their_system_and_type_with_a_pseudonymous_value()
    {
        var patient = Entry<Patient>(await RichBundle());
        var mrn = patient.Identifier.Single(i => i.Type?.Coding.FirstOrDefault()?.Code == "MR");

        Assert.Equal("http://hospital.smarthealthit.org", mrn.System);
        Assert.Matches("^[0-9a-f]{32}$", mrn.Value);
        Assert.Equal(4, patient.Identifier.Count);
    }

    [Fact]
    public async Task The_same_key_gives_the_same_pseudonyms_across_runs_and_another_key_does_not()
    {
        var first = Entry<Patient>(await RichBundle());
        var second = Entry<Patient>(await RichBundle());
        var otherKey = Entry<Patient>(await RichBundle(Pseudonymized(o => o with { Key = TestKeys.Other })));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Identifier.Select(i => i.Value), second.Identifier.Select(i => i.Value));
        Assert.NotEqual(first.Id, otherKey.Id);
        Assert.NotEqual(first.Identifier[1].Value, otherKey.Identifier[1].Value);
    }

    [Fact]
    public async Task The_same_identifier_value_under_different_systems_gets_different_pseudonyms()
    {
        var patient = new Patient { Id = "p" };
        patient.Identifier.Add(new Identifier("http://a", "123456"));
        patient.Identifier.Add(new Identifier("http://b", "123456"));

        var result = await RunAs<Patient>(patient, Pseudonymized());

        Assert.NotEqual(result.Identifier[0].Value, result.Identifier[1].Value);
    }

    [Fact]
    public async Task Dates_move_by_one_offset_per_patient_so_intervals_survive()
    {
        var bundle = await RichBundle();
        var period = Entry<Encounter>(bundle).Period!;
        var start = DateTimeOffset.Parse(period.Start!, CultureInfo.InvariantCulture);
        var end = DateTimeOffset.Parse(period.End!, CultureInfo.InvariantCulture);
        var observed = DateTimeOffset.Parse(Assert.IsType<FhirDateTime>(Entry<Observation>(bundle).Effective).Value!, CultureInfo.InvariantCulture);

        Assert.NotEqual("2024-03-02T08:15:00-05:00", period.Start);
        Assert.Equal(TimeSpan.Parse("7.02:45:00", CultureInfo.InvariantCulture), end - start);
        Assert.Equal(TimeSpan.Parse("23:15:00", CultureInfo.InvariantCulture), observed - start);
        Assert.Equal(new TimeSpan(8, 15, 0), start.TimeOfDay);
        Assert.Equal(TimeSpan.FromHours(-5), start.Offset);
    }

    [Fact]
    public async Task Instants_are_shifted_rather_than_removed()
    {
        var observation = Entry<Observation>(await RichBundle());
        Assert.NotNull(observation.Issued);
        Assert.NotEqual(new DateTimeOffset(2024, 3, 3, 9, 0, 0, TimeSpan.FromHours(-5)), observation.Issued);
    }

    [Fact]
    public async Task Partial_dates_keep_their_precision()
    {
        var patient = await RunAs<Patient>(new Patient { Id = "p", BirthDate = "1970-04" }, Pseudonymized());
        Assert.Matches(@"^\d{4}-\d{2}$", patient.BirthDate);
    }

    [Fact]
    public async Task The_age_cut_off_still_applies()
    {
        Assert.Null(Entry<Patient>(await RichBundle()).BirthDate);
        var younger = await RunAs<Patient>(new Patient { Id = "p", BirthDate = "1970-04-12" }, Pseudonymized());
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", younger.BirthDate);
        Assert.NotEqual("1970-04-12", younger.BirthDate);
    }

    [Fact]
    public async Task Dates_in_notes_shift_with_the_structured_dates()
    {
        var bundle = await RichBundle(Pseudonymized(o => o with { FreeTextStyle = FreeTextStyle.Surrogates }));
        var admitted = Entry<Encounter>(bundle).Period!.Start![..10];
        var note = AttachmentText(Entry<DocumentReference>(bundle));

        Assert.Contains("admitted to", note, StringComparison.Ordinal);
        Assert.Contains($"on {admitted}.", note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Surrogate_names_stay_consistent_within_a_patient()
    {
        var bundle = await RichBundle(Pseudonymized(o => o with { FreeTextStyle = FreeTextStyle.Surrogates }));
        var note = AttachmentText(Entry<DocumentReference>(bundle));
        var lines = note.Split('\n');

        // "Discharge summary for <First> <Middle> <Last> (MRN…" and "Mrs. <Last> is a…"
        var fullName = lines[0]["Discharge summary for ".Length..lines[0].IndexOf(" (", StringComparison.Ordinal)];
        var surname = fullName.Split(' ')[^1];
        Assert.StartsWith($"Mrs. {surname} is a", lines[1], StringComparison.Ordinal);
        Assert.DoesNotContain("[", note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Each_patient_in_a_bundle_gets_their_own_shift()
    {
        var bundle = new Bundle { Type = Bundle.BundleType.Collection };
        foreach (var id in new[] { "a", "b", "c", "d" })
        {
            bundle.Entry.Add(new Bundle.EntryComponent { FullUrl = $"urn:uuid:{id}", Resource = new Patient { Id = id } });
            bundle.Entry.Add(new Bundle.EntryComponent
            {
                FullUrl = $"urn:uuid:obs-{id}",
                Resource = new Observation { Id = $"obs-{id}", Subject = new ResourceReference($"urn:uuid:{id}"), Effective = new FhirDateTime("2024-01-15") },
            });
        }

        var result = await RunAs<Bundle>(bundle, Pseudonymized());
        var dates = result.Entry.Select(e => e.Resource).OfType<Observation>().Select(o => Assert.IsType<FhirDateTime>(o.Effective).Value).ToList();

        Assert.True(dates.Distinct().Count() > 1, string.Join(", ", dates));
    }

    [Fact]
    public async Task Leak_check_passes_on_the_rich_bundle_with_surrogates()
    {
        var result = await Run(FhirFixtures.RichPatientBundle(), Pseudonymized(o => o with { FreeTextStyle = FreeTextStyle.Surrogates }));
        Assert.True(result.Verification.IsClean);
        var json = FhirFixtures.ToJson(result.Resource);
        foreach (var secret in new[] { "Rosa", "Alvarez", "Hart", "4471-882-19", "617-555-0198", "Willow" })
        {
            Assert.DoesNotContain(secret, json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
