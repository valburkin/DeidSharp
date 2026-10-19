using Hl7.Fhir.Model;
using DeidSharp.Detection;
using DeidSharp.Fhir;
using DeidSharp.Fhir.Rules;
using DeidSharp.Tests.TestDoubles;
using static DeidSharp.Tests.Fhir.FhirTestRunner;

namespace DeidSharp.Tests.Fhir;

public class FhirDeidentifierOptionsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(151)]
    public void Rejects_a_nonsensical_top_code_age(int age) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new FhirDeidentifier(SafeHarbor(o => o with { TopCodeAge = age })));

    [Theory]
    [InlineData(0)]
    [InlineData(3651)]
    public void Rejects_a_nonsensical_date_shift(int days) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new FhirDeidentifier(SafeHarbor(o => o with { MaxDateShiftDays = days })));

    [Fact]
    public void Rejects_missing_collections() =>
        Assert.Throws<ArgumentNullException>(() => new FhirDeidentifier(SafeHarbor(o => o with { AllowedExtensions = null! })));

    [Fact]
    public async Task The_top_code_age_is_configurable()
    {
        var patient = await RunAs<Patient>(new Patient { Id = "p", BirthDate = "1950-01-01" }, SafeHarbor(o => o with { TopCodeAge = 70 }));
        Assert.Null(patient.BirthDate);
    }

    [Fact]
    public async Task More_extensions_can_be_allowed()
    {
        var patient = new Patient { Id = "p" };
        patient.Extension.Add(new Extension("http://example.org/ok", new FhirBoolean(true)));
        patient.Extension.Add(new Extension("http://example.org/other", new FhirBoolean(true)));

        var result = await RunAs<Patient>(patient, SafeHarbor(o => o with { AllowedExtensions = new HashSet<string> { "http://example.org/ok" } }));

        Assert.Equal("http://example.org/ok", Assert.Single(result.Extension).Url);
    }

    [Fact]
    public async Task Rules_see_a_readable_path_for_each_element()
    {
        var paths = new List<string>();
        var recorder = new KeepElementRule(c =>
        {
            paths.Add(c.Path);
            return false;
        });
        var patient = new Patient { Id = "p" };
        patient.Name.Add(new HumanName { Family = "Doe" });

        await Run(patient, SafeHarbor(o => o with { AdditionalRules = [recorder] }));

        Assert.Contains("Patient.id", paths);
        Assert.Contains("Patient.name[0]", paths);
    }

    [Fact]
    public async Task Additional_rules_run_before_the_built_in_ones()
    {
        // Built in, an organization's name is removed; this rule keeps it. The output
        // check still knows the name, so keeping it means turning the check off.
        var keepOrgName = new KeepElementRule(c => c is { Parent: Organization, ElementName: "name" });
        var organization = new Organization { Id = "o", Name = "Public Health Lab" };

        await Assert.ThrowsAsync<DeidSharp.Verification.ResidualPhiException>(() => Run(organization, SafeHarbor(o => o with { AdditionalRules = [keepOrgName] })));
        var result = await RunAs<Organization>(organization, SafeHarbor(o => o with { AdditionalRules = [keepOrgName], VerifyOutput = false }));
        Assert.Equal("Public Health Lab", result.Name);
    }

    [Fact]
    public async Task A_custom_free_text_detector_is_used()
    {
        var detector = new StubDetector("custom", new PhiSpan(0, 4, PhiCategory.Name, 1, "custom"));
        var observation = new Observation { Id = "o" };
        observation.Note.Add(new Annotation { Text = "Abcd is fine" });

        var result = await RunAs<Observation>(observation, SafeHarbor(o => o with { FreeTextDetector = detector }));

        Assert.Equal("[NAME] is fine", result.Note.Single().Text);
        Assert.Equal(1, detector.Calls);
    }

    [Fact]
    public async Task A_failing_free_text_detector_fails_the_run()
    {
        var observation = new Observation { Id = "o" };
        observation.Note.Add(new Annotation { Text = "text" });
        var options = SafeHarbor(o => o with { FreeTextDetector = new CompositePhiDetector([new FailingDetector("down", new HttpRequestException())]) });

        await Assert.ThrowsAsync<PhiDetectionException>(() => Run(observation, options));
    }

    [Fact]
    public async Task The_reference_date_defaults_to_today_from_the_time_provider()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var options = new FhirDeidentificationOptions { Key = TestKeys.Fixed, TimeProvider = clock };

        // Born 1940-06-01 is 89 on 2030-01-01: the year is kept.
        var patient = await new FhirDeidentifier(options).DeidentifyAsync(new Patient { Id = "p", BirthDate = "1940-06-01" }, TestContext.Current.CancellationToken);

        Assert.Equal("1940", Assert.IsType<Patient>(patient.Resource).BirthDate);
    }

    [Fact]
    public async Task Turning_verification_off_reports_but_does_not_throw()
    {
        // A rule that deliberately keeps names: verification must notice.
        var keepNames = new KeepElementRule(c => c.Value is HumanName);
        var patient = new Patient { Id = "p" };
        patient.Name.Add(new HumanName { Family = "Doe" });

        await Assert.ThrowsAsync<DeidSharp.Verification.ResidualPhiException>(() => Run(patient, SafeHarbor(o => o with { AdditionalRules = [keepNames] })));
        var result = await Run(patient, SafeHarbor(o => o with { AdditionalRules = [keepNames], VerifyOutput = false }));
        Assert.False(result.Verification.IsClean);
    }

    [Fact]
    public async Task Json_input_must_be_present()
    {
        var deidentifier = new FhirDeidentifier(SafeHarbor());
        await Assert.ThrowsAsync<ArgumentException>(() => deidentifier.DeidentifyJsonAsync(" ", TestContext.Current.CancellationToken));
    }

    // Design pattern: Test Double (Stub).
    // The pattern: a stand-in for a real dependency in a test. A stub returns canned answers.
    // Here: a rule whose canned answer is "keep", for elements the test picks.
    private sealed class KeepElementRule(Func<FhirElementContext, bool> applies) : IFhirElementRule
    {
        public ValueTask<ElementAction> ApplyAsync(FhirElementContext context) =>
            ValueTask.FromResult(applies(context) ? ElementAction.Keep : ElementAction.Pass);
    }

    // Design pattern: Test Double (Stub).
    // The pattern: a stand-in for a real dependency in a test. A stub returns canned answers.
    // Here: a clock that always reads the same instant.
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
