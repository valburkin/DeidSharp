using System.Text.Json;
using Hl7.Fhir.Model;
using DeidSharp.Detection;
using DeidSharp.Fhir;
using DeidSharp.Risk;
using DeidSharp.Text;
using DeidSharp.Verification;

namespace DeidSharp.Tests;

/// <summary>The examples in README.md, run as written, so the documentation cannot drift from the code.</summary>
public class ReadmeExamplesTests
{
    [Fact]
    public async Task De_identifying_a_note()
    {
        var detector = new CompositePhiDetector(DeterministicDetectors.Create());
        var deidentifier = new TextDeidentifier(detector, new TagReplacement());
        var known = KnownIdentifiers.Empty.AddName("Rosa Alvarez").AddDate(new DateOnly(1931, 3, 4));

        var result = await deidentifier.DeidentifyAsync(
            "Rosa Alvarez (DOB 03/04/1931) called from 617-555-0142.",
            new TextContext(subjectId: "patient-1", known),
            TestContext.Current.CancellationToken);

        Assert.Equal("[NAME] (DOB [DATE]) called from [PHONE].", result.Text);
    }

    [Fact]
    public async Task De_identifying_a_fhir_bundle()
    {
        var deidentifier = new FhirDeidentifier(new FhirDeidentificationOptions());

        var result = await deidentifier.DeidentifyAsync(Fhir.FhirFixtures.RichPatientBundle(), TestContext.Current.CancellationToken);
        var json = await deidentifier.DeidentifyJsonAsync(Fhir.FhirFixtures.RichPatientBundleJson(), TestContext.Current.CancellationToken);

        Assert.IsType<Bundle>(result.Resource);
        Assert.DoesNotContain("Alvarez", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Checking_output_produced_elsewhere()
    {
        using var summary = JsonDocument.Parse("""{"conditions":["CKD, SSN 123-45-6789"]}""");

        var report = LeakScanner.CreateDefault().ScanJson(summary.RootElement, KnownIdentifiers.Empty, new LeakScanOptions
        {
            PathPropertyNames = new HashSet<string>(["conditions", "plan"]),
        }, TestContext.Current.CancellationToken);

        Assert.False(report.IsClean);
        Assert.Equal("$.conditions[0]: possible Ssn at [9, 20) (pattern:ssn+pattern:ssn-labeled)", Assert.Single(report.Describe()));
    }

    [Fact]
    public void Assessing_re_identification_risk()
    {
        IReadOnlyDictionary<string, string?>[] rows =
        [
            new Dictionary<string, string?> { ["ageBand"] = "60s", ["sex"] = "F", ["zip3"] = "021", ["diagnosis"] = "CKD" },
            new Dictionary<string, string?> { ["ageBand"] = "60s", ["sex"] = "F", ["zip3"] = "021", ["diagnosis"] = "HTN" },
        ];

        var report = ReidentificationRiskAssessor.Assess(rows, ["ageBand", "sex", "zip3"], sensitiveAttribute: "diagnosis");

        Assert.True(report.IsKAnonymous(2));
        Assert.True(report.IsLDiverse(2));
    }
}
