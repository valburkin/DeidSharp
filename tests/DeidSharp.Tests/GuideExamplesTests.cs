using System.Text.Json;
using System.Text.RegularExpressions;
using DeidSharp.Dates;
using DeidSharp.Detection;
using DeidSharp.Detection.Known;
using DeidSharp.Detection.Patterns;
using DeidSharp.Evaluation;
using DeidSharp.Fhir;
using DeidSharp.Fhir.Rules;
using DeidSharp.Gazetteer;
using DeidSharp.Llm;
using DeidSharp.Presidio;
using DeidSharp.Pseudonymization;
using DeidSharp.Risk;
using DeidSharp.Structured;
using DeidSharp.Tests.TestDoubles;
using DeidSharp.Text;
using DeidSharp.Verification;
using Hl7.Fhir.Model;
using Microsoft.Extensions.DependencyInjection;

namespace DeidSharp.Tests;

/// <summary>
/// The examples in docs/guide.md, run as written (with test doubles where the guide uses a
/// sidecar or a language model), so the guide cannot drift from the code. The comment above
/// each test names the guide section it belongs to.
/// </summary>
public partial class GuideExamplesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly KnownIdentifiers Rosa = KnownIdentifiers.Empty
        .AddName("Rosa Alvarez")
        .AddDate(new DateOnly(1961, 3, 4))
        .AddIdentifier("MRN-48213", PhiCategory.MedicalRecordNumber);

    // "De-identifying free text: the smallest example"
    [Fact]
    public async Task Free_text_with_tags()
    {
        var deidentifier = new TextDeidentifier(
            new CompositePhiDetector(DeterministicDetectors.Create()),
            new TagReplacement());

        var result = await deidentifier.DeidentifyAsync(
            "Rosa Alvarez (MRN-48213) was seen on 03/04/2024. Call 617-555-0142.",
            new TextContext(subjectId: "patient-1", Rosa),
            Ct);

        Assert.Equal("[NAME] ([MRN]) was seen on [DATE]. Call [PHONE].", result.Text);
    }

    // "De-identifying free text: what the result holds"
    [Fact]
    public async Task Findings_carry_positions_and_categories_only()
    {
        var deidentifier = new TextDeidentifier(new CompositePhiDetector(DeterministicDetectors.Create()), new TagReplacement());
        var result = await deidentifier.DeidentifyAsync("Seen by Dr. Hart today.", TextContext.None, Ct);

        var finding = Assert.Single(result.Findings);
        var line = $"{finding.Category} at [{finding.InputStart}, {finding.InputEnd}) by {string.Join("+", finding.Sources)}";

        Assert.Equal("Seen by Dr. [NAME] today.", result.Text);
        Assert.Equal("Name at [12, 16) by pattern:name-after-title", line);
    }

    // "Tags or surrogates: realistic surrogates"
    [Fact]
    public async Task Free_text_with_surrogates()
    {
        var pseudonymizer = new HmacPseudonymizer(TestKeys.Fixed);   // the guide: PseudonymKey.FromBase64(...)
        var deidentifier = new TextDeidentifier(
            new CompositePhiDetector(DeterministicDetectors.Create()),
            new SurrogateReplacement(pseudonymizer, YearOnlyDateTransformer.Instance));

        var context = new TextContext("patient-1", Rosa);
        var first = await deidentifier.DeidentifyAsync("Rosa Alvarez, born 03/04/1961, called from 617-555-0142.", context, Ct);
        var again = await deidentifier.DeidentifyAsync("Rosa Alvarez, born 03/04/1961, called from 617-555-0142.", context, Ct);

        Assert.DoesNotContain("Alvarez", first.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("617-555-0142", first.Text, StringComparison.Ordinal);
        Assert.Contains("1961", first.Text, StringComparison.Ordinal);
        Assert.Equal(first.Text, again.Text);
    }

    // "Tags or surrogates: dates in surrogates"
    [Fact]
    public async Task Free_text_with_shifted_dates()
    {
        var pseudonymizer = new HmacPseudonymizer(TestKeys.Fixed);
        var shifter = new DateShifter(pseudonymizer, maxDays: 365);
        var deidentifier = new TextDeidentifier(
            new CompositePhiDetector(DeterministicDetectors.Create()),
            new SurrogateReplacement(pseudonymizer, new ShiftingDateTransformer(shifter)));

        var result = await deidentifier.DeidentifyAsync("Admitted 2024-03-05, discharged 2024-03-09.", new TextContext("patient-1"), Ct);

        var shifted = IsoDate().Matches(result.Text).Select(m => DateOnly.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(2, shifted.Count);
        Assert.Equal(4, shifted[1].DayNumber - shifted[0].DayNumber);
        Assert.NotEqual(new DateOnly(2024, 3, 5), shifted[0]);
    }

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}", RegexOptions.CultureInvariant)]
    private static partial Regex IsoDate();

    // "Known identifiers: building them from your record"
    [Fact]
    public void Known_identifiers_from_a_record()
    {
        var known = KnownIdentifiers.Empty
            .AddName("Rosa M. Alvarez")
            .AddName("Luis Alvarez")                       // a relative
            .AddDate(new DateOnly(1961, 3, 4))
            .AddPhone("(617) 555-0142")
            .AddEmail("rosa.alvarez@example.org")
            .AddStreetAddress("12 Oak Street Apt 4")
            .AddLocation("Fairhaven")
            .AddOrganization("Harborview Clinic")
            .AddIdentifier("MRN-48213", PhiCategory.MedicalRecordNumber)
            .AddIdentifier("ZQ-4471029", PhiCategory.HealthPlanNumber);

        Assert.Equal(10, known.Count);
        Assert.Equal(11, known.Merge(KnownIdentifiers.Empty.AddName("Dr. Imani Coles")).Count);
    }

    // "Detectors: your own patterns" (the guide calls the generated regex LocalMrn too)
    [GeneratedRegex(@"\bH\d{7}\b", RegexOptions.CultureInvariant)]
    private static partial Regex LocalMrn();

    [Fact]
    public async Task Own_pattern_rules()
    {
        var localMrn = new PatternRule("local-mrn", PhiCategory.MedicalRecordNumber, LocalMrn(), Score: 0.9);
        var patterns = new PatternPhiDetector([.. PatternLibrary.Default, localMrn]);
        var stack = new CompositePhiDetector([new KnownIdentifierDetector(), patterns]);

        var spans = await stack.DetectAsync(new DetectionRequest("Chart H1234567 reviewed."), Ct);

        Assert.Equal(PhiCategory.MedicalRecordNumber, Assert.Single(spans).Category);
    }

    // "Detectors: writing your own detector"
    private sealed partial class BadgeNumberDetector : IDeterministicPhiDetector
    {
        public string Name => "badge";

        public IReadOnlyList<PhiSpan> Detect(DetectionRequest request, CancellationToken cancellationToken = default) =>
            [.. Badge().Matches(request.Text).Select(m => new PhiSpan(m.Index, m.Index + m.Length, PhiCategory.OtherIdentifier, 1.0, Name))];

        public Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Detect(request, cancellationToken));

        [GeneratedRegex(@"\bBADGE-\d{5}\b", RegexOptions.CultureInvariant)]
        private static partial Regex Badge();
    }

    [Fact]
    public async Task Own_detector()
    {
        var stack = new CompositePhiDetector([new BadgeNumberDetector(), .. DeterministicDetectors.Create()]);
        var deidentifier = new TextDeidentifier(stack, new TagReplacement());

        var result = await deidentifier.DeidentifyAsync("Escorted by BADGE-40211.", TextContext.None, Ct);

        Assert.Equal("Escorted by [ID].", result.Text);
    }

    // "Detectors: the gazetteer"
    [Fact]
    public async Task Gazetteer_in_a_stack()
    {
        var stack = new CompositePhiDetector([new GazetteerDetector(), .. DeterministicDetectors.Create()]);
        var deidentifier = new TextDeidentifier(stack, new TagReplacement());

        var result = await deidentifier.DeidentifyAsync("Pt's daughter Hope visited from Fairhaven.", TextContext.None, Ct);

        Assert.Equal("Pt's daughter [NAME] visited from [LOCATION].", result.Text);
    }

    // "A local language model: the recommended stacks" (the guide builds one OllamaSharp client
    // for the model and its second opinion; here each gets a stub with the answer it expects)
    [Fact]
    public async Task Language_model_stacks()
    {
        IPhiDetector clinical = new StubDetector("clinical", new PhiSpan(4, 8, PhiCategory.Name, 0.9, "clinical"));
        using var chat = new CannedChatClient("""{"phi": [{"text": "Hart", "type": "name"}, {"text": "3/14", "type": "date"}]}""");
        using var judge = new CannedChatClient("""{"not_phi": []}""");
        Action<Microsoft.Extensions.AI.ChatOptions> ollamaSettings = _ => { };

        var llm = new LlmPhiDetector(chat, new LlmDetectorOptions { Prompt = LlmPrompt.WithPoolExamples, ConfigureRequest = ollamaSettings });
        var known = new KnownIdentifierDetector();
        var patterns = new PatternPhiDetector();

        var recallFirst = new SafeHarborAgeFilter(new CompositePhiDetector([clinical, llm, known, patterns]));
        var balanced = new SafeHarborAgeFilter(new LlmSecondOpinion(
            new CompositePhiDetector([clinical, llm, known, patterns]), judge, new LlmSecondOpinionOptions { ConfigureRequest = ollamaSettings }));
        var places = new CategoryFilter(clinical, [PhiCategory.Location, PhiCategory.Organization]);
        var balancedOneCall = new SafeHarborAgeFilter(new CompositePhiDetector([places, llm, known, patterns]));
        var precisionFirst = new SafeHarborAgeFilter(new CompositePhiDetector([llm, known, patterns]));

        foreach (var stack in new IPhiDetector[] { recallFirst, balanced, balancedOneCall, precisionFirst })
        {
            var result = await new TextDeidentifier(stack, new TagReplacement()).DeidentifyAsync("Dr. Hart called on 3/14.", TextContext.None, Ct);
            Assert.Equal("Dr. [NAME] called on [DATE].", result.Text);
        }
    }

    // "De-identifying FHIR: Safe Harbor"
    [Fact]
    public async Task Fhir_safe_harbor()
    {
        var deidentifier = new FhirDeidentifier(new FhirDeidentificationOptions());

        var (resource, stats, verification) = await deidentifier.DeidentifyAsync(Fhir.FhirFixtures.RichPatientBundle(), Ct);

        Assert.IsType<Bundle>(resource);
        Assert.True(verification.IsClean);
        Assert.True(stats is { ElementsRemoved: > 0, IdsPseudonymized: > 0 });
    }

    // "De-identifying FHIR: pseudonymized, linkable across exports"
    [Fact]
    public async Task Fhir_pseudonymized()
    {
        var options = new FhirDeidentificationOptions
        {
            Mode = FhirDeidentificationMode.Pseudonymized,
            Key = TestKeys.Fixed,                          // the guide: PseudonymKey.FromBase64(...)
            FreeTextStyle = FreeTextStyle.Surrogates,
            MaxDateShiftDays = 365,
        };
        var deidentifier = new FhirDeidentifier(options);

        var first = await deidentifier.DeidentifyJsonAsync(Fhir.FhirFixtures.RichPatientBundleJson(), Ct);
        var second = await deidentifier.DeidentifyJsonAsync(Fhir.FhirFixtures.RichPatientBundleJson(), Ct);

        Assert.Equal(first, second);
    }

    // "De-identifying FHIR: your own rules"
    private sealed class DropMaritalStatus : IFhirElementRule
    {
        public ValueTask<ElementAction> ApplyAsync(FhirElementContext context) =>
            ValueTask.FromResult(context is { Parent: Patient, ElementName: "maritalStatus" } ? ElementAction.Remove : ElementAction.Pass);
    }

    [Fact]
    public async Task Fhir_own_rule()
    {
        var patient = new Patient { Id = "p1", MaritalStatus = new CodeableConcept("http://terminology.hl7.org/CodeSystem/v3-MaritalStatus", "M") };
        var deidentifier = new FhirDeidentifier(new FhirDeidentificationOptions { AdditionalRules = [new DropMaritalStatus()] });

        var result = await deidentifier.DeidentifyAsync(patient, Ct);

        Assert.Null(((Patient)result.Resource).MaritalStatus);
    }

    // "De-identifying FHIR: invalid input"
    [Fact]
    public async Task Fhir_invalid_json_is_refused_without_quoting_it()
    {
        var deidentifier = new FhirDeidentifier();

        var e = await Assert.ThrowsAsync<FhirInputException>(() => deidentifier.DeidentifyJsonAsync("""{"resourceType": "Patient", "birthDate": "Rosa"}""", Ct));

        Assert.DoesNotContain("Rosa", e.Message, StringComparison.Ordinal);
    }

    // "Pseudonyms and keys"
    [Fact]
    public void Pseudonyms_and_date_shifts()
    {
        var key = PseudonymKey.FromBase64(Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(PseudonymKey.MinimumLength)));
        var pseudonymizer = new HmacPseudonymizer(key);

        var id = PseudonymFormats.Uuid(pseudonymizer.Digest("Patient.id", "48213"));
        var again = PseudonymFormats.Uuid(pseudonymizer.Digest("Patient.id", "48213"));
        var shifter = new DateShifter(pseudonymizer, maxDays: 365);
        var offset = shifter.OffsetDays("patient-1");

        Assert.Equal(id, again);
        Assert.InRange(Math.Abs(offset), 1, 365);
        Assert.Equal(new DateOnly(2024, 3, 5).AddDays(offset), shifter.Shift(new DateOnly(2024, 3, 5), "patient-1"));
        Assert.Throws<ArgumentException>(() => PseudonymKey.FromBytes(new byte[16]));
    }

    // "Structured fields outside FHIR"
    [Fact]
    public void Structured_fields()
    {
        Assert.Equal("90+", AgeGeneralizer.SafeHarbor.Generalize(93).Label);
        Assert.Equal("60s", AgeGeneralizer.SafeHarbor.Generalize(64).Label);
        Assert.Equal("021", ZipCodeGeneralizer.SafeHarbor.Generalize("02115-1234"));
        Assert.Equal("000", ZipCodeGeneralizer.SafeHarbor.Generalize("03601"));
        Assert.Null(ZipCodeGeneralizer.SafeHarbor.Generalize("SW1A 1AA"));

        Assert.True(TextDateParser.TryParse("March 5th, 2024", out var date));
        Assert.Equal("2024", YearOnlyDateTransformer.Instance.Transform(date, "patient-1").ToString());
    }

    // "Checking output: text, values and JSON"
    [Fact]
    public void Checking_json_from_a_model()
    {
        using var summary = JsonDocument.Parse("""{"assessment": "CKD stage 3", "Rosa Alvarez": "follow up in 2 weeks"}""");
        var options = new LeakScanOptions { ScanPropertyNames = true, PathPropertyNames = new HashSet<string>(["assessment", "plan"]) };

        var report = LeakScanner.CreateDefault().ScanJson(summary.RootElement, Rosa, options, Ct);

        Assert.False(report.IsClean);
        Assert.Equal("$.<property 1>(name): possible Name at [0, 12) (known)", Assert.Single(report.Describe()));
    }

    // "Errors and failing closed"
    [Fact]
    public async Task A_failing_detector_stops_the_run()
    {
        var deidentifier = new TextDeidentifier(new CompositePhiDetector([new FailingDetector("clinical"), .. DeterministicDetectors.Create()]), new TagReplacement());

        await Assert.ThrowsAsync<PhiDetectionException>(() => deidentifier.DeidentifyAsync("Seen by Dr. Hart.", TextContext.None, Ct));
    }

    // "Errors and failing closed"
    [Fact]
    public async Task A_known_identifier_left_in_the_output_is_refused()
    {
        // A stack without the known-identifier detector misses the name; the output check finds it.
        var deidentifier = new TextDeidentifier(new StubDetector("none"), new TagReplacement());

        var e = await Assert.ThrowsAsync<ResidualPhiException>(() => deidentifier.DeidentifyAsync("Rosa Alvarez called.", new TextContext("patient-1", Rosa), Ct));

        Assert.False(e.Report.IsClean);
        Assert.DoesNotContain("Rosa", e.Message, StringComparison.Ordinal);
    }

    // "Using DeidSharp in a service: registering the stack"
    [Fact]
    public void Registering_a_stack_with_dependency_injection()
    {
        var services = new ServiceCollection();
        services.AddPresidioDetector("clinical", o =>
        {
            o.Endpoint = new Uri("http://localhost:5003/");
            o.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<IPhiDetector>(sp => new CompositePhiDetector([
            sp.GetRequiredKeyedService<PresidioDetector>("clinical"),
            new GazetteerDetector(),
            .. DeterministicDetectors.Create(),
        ]));
        services.AddSingleton(sp => new TextDeidentifier(sp.GetRequiredService<IPhiDetector>(), new TagReplacement()));

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<TextDeidentifier>());
    }

    // "Measuring recall on your own data"
    [Fact]
    public async Task Measuring_a_stack()
    {
        AnnotatedText[] sample =
        [
            new("Seen by Dr. Hart on 03/04/2024.", [new GoldSpan(12, 16, PhiCategory.Name), new GoldSpan(20, 30, PhiCategory.Date)]),
            new("Daughter Carmen called.", [new GoldSpan(9, 15, PhiCategory.Name)]),
        ];

        var report = await DetectionEvaluator.EvaluateAsync(new CompositePhiDetector(DeterministicDetectors.Create()), sample, Ct);

        Assert.Equal(1.0, report.Overall.Recall);
        Assert.Equal(1.0, report.PerCategory[PhiCategory.Name].Recall);
    }

    // "De-identifying free text: options" and "Tags or surrogates: tags"
    [Fact]
    public async Task Options_and_tag_format()
    {
        var stack = new CompositePhiDetector(DeterministicDetectors.Create());
        var deidentifier = new TextDeidentifier(stack, new TagReplacement("<{0}>"), new TextDeidentifierOptions
        {
            MinimumScore = 0,
            VerifyOutput = true,
        });

        var result = await deidentifier.DeidentifyAsync("Seen by Dr. Hart today.", TextContext.None, Ct);

        Assert.Equal("Seen by Dr. <NAME> today.", result.Text);
    }

    // "De-identifying FHIR: free text inside FHIR", "extensions" and "ages and the reference date"
    [Fact]
    public async Task Fhir_free_text_extensions_and_reference_date()
    {
        var stack = new CompositePhiDetector([new GazetteerDetector(), .. DeterministicDetectors.Create()]);
        var options = new FhirDeidentificationOptions
        {
            FreeTextDetector = stack,
            FreeTextStyle = FreeTextStyle.Tags,
            MaxFreeTextConcurrency = 8,
            AllowedExtensions = new HashSet<string>(FhirDeidentificationOptions.DefaultAllowedExtensions)
            {
                "http://example.org/fhir/StructureDefinition/trial-arm",
            },
            ReferenceDate = new DateOnly(2026, 1, 1),
        };

        var result = await new FhirDeidentifier(options).DeidentifyAsync(Fhir.FhirFixtures.RichPatientBundle(), Ct);

        Assert.True(result.Verification.IsClean);
        Assert.True(result.Statistics.FreeTextFindings > 0);
    }

    // "Checking output: text, values and JSON"
    [Fact]
    public void Checking_text_and_values()
    {
        var scanner = LeakScanner.CreateDefault();

        var report = scanner.Scan("Summary: Rosa Alvarez, CKD stage 3.", Rosa, cancellationToken: Ct);
        var fields = scanner.Scan([new ScannedValue("note.assessment", "CKD stage 3"), new ScannedValue("note.plan", "Call 617-555-0142")], Rosa, cancellationToken: Ct);

        Assert.Equal("$: possible Name at [9, 21) (known)", Assert.Single(report.Describe()));
        Assert.Equal("note.plan: possible Phone at [5, 17) (pattern:phone)", Assert.Single(fields.Describe()));
    }

    // "Structured fields outside FHIR" and "Pseudonyms and keys"
    [Fact]
    public void Age_bands_and_short_pseudonyms()
    {
        var pseudonymizer = new HmacPseudonymizer(TestKeys.Fixed);

        Assert.Equal("60-64", new AgeGeneralizer(bandWidth: 5).Generalize(64).Label);
        Assert.Equal(16, PseudonymFormats.Hex(pseudonymizer.Digest("MRN", "48213"), length: 16).Length);
    }

    // "Re-identification risk"
    [Fact]
    public void Assessing_risk()
    {
        IReadOnlyDictionary<string, string?>[] rows =
        [
            new Dictionary<string, string?> { ["ageBand"] = "60s", ["sex"] = "F", ["zip3"] = "021", ["diagnosis"] = "CKD" },
            new Dictionary<string, string?> { ["ageBand"] = "60s", ["sex"] = "F", ["zip3"] = "021", ["diagnosis"] = "HTN" },
            new Dictionary<string, string?> { ["ageBand"] = "70s", ["sex"] = "M", ["zip3"] = "021", ["diagnosis"] = "CKD" },
        ];

        var report = ReidentificationRiskAssessor.Assess(rows, quasiIdentifiers: ["ageBand", "sex", "zip3"], sensitiveAttribute: "diagnosis");

        Assert.False(report.IsKAnonymous(2));
        Assert.Equal(1, report.UniqueRecords);
        Assert.Equal(1.0, report.MaximumProsecutorRisk);
    }
}
