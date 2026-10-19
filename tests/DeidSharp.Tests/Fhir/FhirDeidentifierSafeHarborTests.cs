using System.Text;
using Hl7.Fhir.Model;
using DeidSharp.Fhir;
using static DeidSharp.Tests.Fhir.FhirTestRunner;

namespace DeidSharp.Tests.Fhir;

public class FhirDeidentifierSafeHarborTests
{
    private static async Task<Bundle> RichBundle() => await RunAs<Bundle>(FhirFixtures.RichPatientBundle());

    private static string AttachmentText(Bundle bundle) => string.Concat(bundle.Entry
        .Select(e => e.Resource).OfType<DocumentReference>()
        .SelectMany(d => d.Content).Select(c => c.Attachment.Data is { } d ? Encoding.UTF8.GetString(d) : ""));

    [Fact]
    public async Task No_identifying_string_from_the_rich_bundle_survives()
    {
        var result = await Run(FhirFixtures.RichPatientBundle());
        var bundle = Assert.IsType<Bundle>(result.Resource);

        var json = FhirFixtures.ToJson(bundle);
        var attachments = AttachmentText(bundle);
        foreach (var secret in FhirFixtures.RichBundleSecrets)
        {
            Assert.DoesNotContain(secret, json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(secret, attachments, StringComparison.OrdinalIgnoreCase);
        }

        Assert.True(result.Verification.IsClean);
    }

    [Fact]
    public async Task The_input_is_not_modified()
    {
        var bundle = FhirFixtures.RichPatientBundle();
        var before = FhirFixtures.ToJson(bundle);

        await Run(bundle);

        Assert.Equal(before, FhirFixtures.ToJson(bundle));
    }

    [Fact]
    public async Task Names_contact_points_signatures_and_narrative_are_gone_everywhere()
    {
        var bundle = await RichBundle();
        Assert.DoesNotContain(All(bundle), n => n is HumanName or ContactPoint or Signature or Narrative);
    }

    [Fact]
    public async Task Every_date_keeps_only_its_year_and_no_instant_remains()
    {
        var bundle = await RichBundle();
        var nodes = All(bundle).ToList();

        Assert.DoesNotContain(nodes, n => n is Instant);
        Assert.All(nodes.OfType<Date>(), d => Assert.Matches("^\\d{4}$", d.Value));
        Assert.All(nodes.OfType<FhirDateTime>(), d => Assert.Matches("^\\d{4}$", d.Value));
        Assert.Equal("2024", Entry<Encounter>(bundle).Period!.Start);
    }

    [Fact]
    public async Task A_patient_over_89_loses_the_birth_date_entirely()
    {
        var patient = Entry<Patient>(await RichBundle());
        Assert.Null(patient.BirthDate);
    }

    [Fact]
    public async Task A_younger_patient_keeps_the_birth_year()
    {
        var patient = await RunAs<Patient>(new Patient { Id = "p", BirthDate = "1960-05-03" });
        Assert.Equal("1960", patient.BirthDate);
    }

    [Theory]
    [InlineData("1936-10-16", "1936")]
    [InlineData("1936", null)]
    [InlineData("1936-10", null)]
    [InlineData("1936-10-15", null)]
    public async Task The_age_cut_off_errs_toward_removal_for_partial_birth_dates(string birthDate, string? expected)
    {
        // As of 2026-10-15: born 1936-10-16 is 89 (kept as a year); a partial date that
        // could mean 90 is removed.
        var patient = await RunAs<Patient>(new Patient { Id = "p", BirthDate = birthDate });
        Assert.Equal(expected, patient.BirthDate);
    }

    [Fact]
    public async Task Age_at_death_decides_the_cut_off_for_a_deceased_patient()
    {
        var died89 = await RunAs<Patient>(new Patient { Id = "p", BirthDate = "1900-06-01", Deceased = new FhirDateTime("1990-01-01") });
        var died91 = await RunAs<Patient>(new Patient { Id = "p", BirthDate = "1900-06-01", Deceased = new FhirDateTime("1991-12-01") });

        Assert.Equal("1900", died89.BirthDate);
        Assert.Null(died91.BirthDate);
        Assert.Equal("1991", Assert.IsType<FhirDateTime>(died91.Deceased).Value);
    }

    [Fact]
    public async Task Identifiers_are_removed()
    {
        var bundle = await RichBundle();
        Assert.DoesNotContain(All(bundle), n => n is Identifier);
    }

    [Fact]
    public async Task Addresses_keep_only_state_country_and_three_digit_zip()
    {
        var address = Assert.Single(Entry<Patient>(await RichBundle()).Address);

        Assert.Empty(address.Line);
        Assert.Null(address.City);
        Assert.Equal("MA", address.State);
        Assert.Equal("US", address.Country);
        Assert.Equal("011", address.PostalCode);
        Assert.Empty(address.Extension);
    }

    [Fact]
    public async Task Only_allowed_extensions_survive_and_their_contents_are_kept()
    {
        var patient = Entry<Patient>(await RichBundle());

        Assert.Equal(
            ["http://hl7.org/fhir/us/core/StructureDefinition/us-core-race", "http://hl7.org/fhir/us/core/StructureDefinition/us-core-birthsex"],
            patient.Extension.Select(e => e.Url));
        Assert.Equal(["ombCategory", "text"], patient.Extension[0].Extension.Select(e => e.Url));
    }

    [Fact]
    public async Task Unallowed_modifier_extensions_are_removed_and_counted_for_review()
    {
        var result = await Run(FhirFixtures.RichPatientBundle());
        Assert.Empty(Entry<MedicationRequest>(Assert.IsType<Bundle>(result.Resource)).ModifierExtension);
        Assert.Equal(1, result.Statistics.ModifierExtensionsRemoved);
    }

    [Fact]
    public async Task Every_reference_still_resolves_inside_the_bundle()
    {
        var bundle = await RichBundle();
        var targets = bundle.Entry
            .SelectMany(e => new[] { e.FullUrl, $"{e.Resource!.TypeName}/{e.Resource.Id}" })
            .ToHashSet(StringComparer.Ordinal);

        var references = All(bundle).OfType<ResourceReference>().Where(r => r.Reference is not null).ToList();

        Assert.NotEmpty(references);
        Assert.All(references, r => Assert.True(r.Reference!.StartsWith('#') || targets.Contains(r.Reference), $"dangling {r.Reference}"));
    }

    [Fact]
    public async Task Conditional_references_resolve_to_the_bundle_entry_with_that_identifier()
    {
        var bundle = await RichBundle();
        var encounter = Entry<Encounter>(bundle);

        Assert.Equal(bundle.Entry.Single(e => e.Resource is Practitioner).FullUrl, encounter.Participant[0].Individual!.Reference);
        Assert.Equal(bundle.Entry.Single(e => e.Resource is Organization).FullUrl, encounter.ServiceProvider!.Reference);
    }

    [Fact]
    public async Task Reference_displays_are_dropped_but_a_display_only_reference_keeps_its_cleaned_text()
    {
        var bundle = await RichBundle();
        Assert.All(All(bundle).OfType<ResourceReference>().Where(r => r.Reference is not null), r => Assert.Null(r.Display));
        Assert.Equal("Medicare", Entry<Coverage>(bundle).Payor.Single().Display);
    }

    [Fact]
    public async Task Contained_resources_keep_their_local_ids_and_references()
    {
        var request = Entry<MedicationRequest>(await RichBundle());
        Assert.Equal("med1", request.Contained.Single().Id);
        Assert.Equal("#med1", Assert.IsType<ResourceReference>(request.Medication).Reference);
    }

    [Fact]
    public async Task Text_attachments_are_de_identified_and_other_attachments_removed()
    {
        var document = Entry<DocumentReference>(await RichBundle());
        var attachment = Assert.Single(document.Content).Attachment;

        var text = Encoding.UTF8.GetString(attachment.Data!);
        Assert.StartsWith("Discharge summary for [NAME] (MRN: [MRN]), DOB [DATE].", text, StringComparison.Ordinal);
        Assert.Contains("a [AGE]-year-old woman admitted to [ORGANIZATION] on [DATE].", text, StringComparison.Ordinal);
        Assert.Null(attachment.Size);
        Assert.Null(attachment.Hash);
        Assert.Null(attachment.Title);
        Assert.Null(attachment.Url);
    }

    [Fact]
    public async Task Free_text_fields_are_de_identified_in_place()
    {
        var bundle = await RichBundle();
        Assert.Equal("[NAME] reports fatigue; daughter [NAME] helps with meds.", Entry<Condition>(bundle).Note.Single().Text);
        Assert.Equal("Drawn by RN at [ADDRESS] home visit; call [PHONE] with results.", Entry<Observation>(bundle).Note.Single().Text);
        Assert.Null(Entry<Condition>(bundle).Note.Single().Author);
        Assert.Equal("Discharge summary, [NAME] [NAME]", Entry<DocumentReference>(bundle).Description);
    }

    [Fact]
    public async Task Clinical_content_is_left_intact()
    {
        var bundle = await RichBundle();
        var condition = Entry<Condition>(bundle);
        var observation = Entry<Observation>(bundle);

        Assert.Equal("433144002", condition.Code!.Coding.Single().Code);
        Assert.Equal("Chronic kidney disease stage 3 (disorder)", condition.Code.Coding.Single().Display);
        Assert.Equal("Chronic kidney disease stage 3", condition.Code.Text);
        Assert.Equal(2.1m, Assert.IsType<Quantity>(observation.Value).Value);
        Assert.Equal("mg/dL", Assert.IsType<Quantity>(observation.Value).Unit);
        Assert.Equal("Take 1 tablet by mouth daily", Entry<MedicationRequest>(bundle).DosageInstruction.Single().Text);
        Assert.Equal(AdministrativeGender.Female, Entry<Patient>(bundle).Gender);
    }

    [Fact]
    public async Task An_onset_age_over_89_becomes_at_least_90()
    {
        var age = Assert.IsType<Age>(Entry<Condition>(await RichBundle()).Onset);
        Assert.Equal(90m, age.Value);
        Assert.Equal(Quantity.QuantityComparator.GreaterOrEqual, age.Comparator);
    }

    [Fact]
    public async Task Identifying_elements_of_sites_devices_and_coverage_are_removed()
    {
        var bundle = await RichBundle();
        var location = Entry<Location>(bundle);
        var device = Entry<Device>(bundle);

        Assert.Null(location.Name);
        Assert.Null(location.Position);
        Assert.Null(Entry<Organization>(bundle).Name);
        Assert.Null(device.SerialNumber);
        Assert.Null(device.LotNumber);
        Assert.Empty(device.UdiCarrier);
        Assert.Null(Entry<Coverage>(bundle).SubscriberId);
        Assert.Empty(Entry<Patient>(bundle).Contact);
    }

    [Fact]
    public async Task Transaction_bookkeeping_that_carries_identifiers_is_removed()
    {
        var bundle = await RichBundle();

        Assert.Empty(bundle.Link);
        Assert.Null(bundle.Meta);
        Assert.Null(bundle.Timestamp);
        Assert.All(bundle.Entry, e => Assert.Null(e.Request!.IfNoneExist));
        Assert.All(bundle.Entry, e => Assert.Equal(e.Resource!.TypeName, e.Request!.Url));
    }

    [Fact]
    public async Task The_output_is_valid_fhir_json()
    {
        var json = await new FhirDeidentifier(SafeHarbor()).DeidentifyJsonAsync(FhirFixtures.RichPatientBundleJson(), TestContext.Current.CancellationToken);
        var parsed = FhirFixtures.FromJson<Bundle>(json);
        Assert.Equal(11, parsed.Entry.Count);
    }

    [Fact]
    public async Task With_a_fixed_key_the_output_is_reproducible()
    {
        var a = FhirFixtures.ToJson((await Run(FhirFixtures.RichPatientBundle())).Resource);
        var b = FhirFixtures.ToJson((await Run(FhirFixtures.RichPatientBundle())).Resource);
        Assert.Equal(a, b);
    }

    [Fact]
    public async Task Without_a_key_every_run_uses_fresh_pseudonyms()
    {
        var options = SafeHarbor(o => o with { Key = null });
        var a = Entry<Patient>(await RunAs<Bundle>(FhirFixtures.RichPatientBundle(), options));
        var b = Entry<Patient>(await RunAs<Bundle>(FhirFixtures.RichPatientBundle(), options));
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public async Task Statistics_describe_the_run()
    {
        var stats = (await Run(FhirFixtures.RichPatientBundle())).Statistics;
        Assert.True(stats.ElementsRemoved > 10);
        Assert.True(stats.ElementsReplaced > 10);
        Assert.True(stats.IdsPseudonymized > 10);
        Assert.True(stats.FreeTextValues > 5);
        Assert.True(stats.FreeTextFindings > 10);
    }

    [Fact]
    public async Task A_single_resource_works_without_a_bundle()
    {
        var patient = new Patient { Id = "p1", BirthDate = "1980-02-29" };
        patient.Name.Add(new HumanName { Family = "Doe", Given = ["Jane"] });
        patient.Identifier.Add(new Identifier("http://mrn", "MRN-1234"));

        var result = await RunAs<Patient>(patient);

        Assert.Empty(result.Name);
        Assert.Empty(result.Identifier);
        Assert.Equal("1980", result.BirthDate);
        Assert.NotEqual("p1", result.Id);
    }

    [Fact]
    public async Task Cancellation_stops_the_run()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new FhirDeidentifier(SafeHarbor()).DeidentifyAsync(FhirFixtures.RichPatientBundle(), cts.Token));
    }
}
