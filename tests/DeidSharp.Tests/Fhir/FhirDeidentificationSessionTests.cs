using System.Globalization;
using Hl7.Fhir.Model;
using DeidSharp.Detection;
using DeidSharp.Fhir;
using DeidSharp.Fhir.Rules;
using DeidSharp.Tests.TestDoubles;
using DeidSharp.Verification;
using static DeidSharp.Tests.Fhir.FhirTestRunner;

namespace DeidSharp.Tests.Fhir;

/// <summary>
/// Free text is queued while a resource is walked and de-identified together at the end:
/// a bundle's notes wait on a detector service a few at a time, never one after another,
/// the result is the same whatever the limit, and any failure fails the run.
/// </summary>
public class FhirDeidentificationSessionTests
{
    private const string BirthSex = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-birthsex";

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task Free_text_runs_concurrently_up_to_the_limit(int limit)
    {
        var spy = new ConcurrencySpyDetector(releaseAt: limit);

        var result = await Run(Notes(24), SafeHarbor(o => o with { FreeTextDetector = spy, MaxFreeTextConcurrency = limit }));

        Assert.Equal(limit, spy.MostInFlight);
        Assert.Equal(24, spy.Calls);
        Assert.Equal(24, result.Statistics.FreeTextValues);
    }

    [Theory]
    [InlineData(FreeTextStyle.Tags)]
    [InlineData(FreeTextStyle.Surrogates)]
    public async Task The_output_does_not_depend_on_the_limit(FreeTextStyle style)
    {
        var one = await Run(FhirFixtures.RichPatientBundle(), Pseudonymized(o => o with { FreeTextStyle = style, MaxFreeTextConcurrency = 1 }));
        var many = await Run(FhirFixtures.RichPatientBundle(), Pseudonymized(o => o with { FreeTextStyle = style, MaxFreeTextConcurrency = 16 }));

        Assert.Equal(FhirFixtures.ToJson(one.Resource), FhirFixtures.ToJson(many.Resource));
        Assert.Equivalent(one.Statistics, many.Statistics);
    }

    [Fact]
    public async Task A_failing_text_fails_the_run_and_stops_the_texts_not_yet_started()
    {
        // The walker visits a list from its end, so the last note is the first one queued.
        var bundle = Notes(24);
        Note(bundle, 23).Text = "FAIL here";
        var spy = new ConcurrencySpyDetector(releaseAt: 1000, failureMarker: "FAIL");

        var error = await Assert.ThrowsAsync<PhiDetectionException>(() =>
            Run(bundle, SafeHarbor(o => o with { FreeTextDetector = spy, MaxFreeTextConcurrency = 4 })));

        Assert.EndsWith("failed with HttpRequestException.", error.Message, StringComparison.Ordinal);
        Assert.True(spy.Calls < 24, "Texts queued after the failure still ran.");
    }

    [Fact]
    public async Task Known_identifiers_left_in_a_queued_text_fail_the_run()
    {
        var bundle = Notes(3);
        Patient(bundle).Name.Add(new HumanName { Family = "Alvarez", Given = ["Rosa"] });
        Note(bundle, 1).Text = "Rosa Alvarez called.";

        // A detector that finds nothing: the text's own check must catch the name.
        await Assert.ThrowsAsync<ResidualPhiException>(() =>
            Run(bundle, SafeHarbor(o => o with { FreeTextDetector = new StubDetector("nothing") })));
    }

    [Fact]
    public async Task The_caller_can_cancel_while_queued_text_is_running()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var spy = new ConcurrencySpyDetector(releaseAt: 1000);
        var deidentifier = new FhirDeidentifier(SafeHarbor(o => o with { FreeTextDetector = spy }));
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => deidentifier.DeidentifyAsync(Notes(24), cancellation.Token));
    }

    [Fact]
    public async Task Queued_text_is_written_back_before_the_output_is_verified()
    {
        var bundle = Notes(3);
        Patient(bundle).Name.Add(new HumanName { Family = "Alvarez", Given = ["Rosa"] });
        Note(bundle, 1).Text = "Called Rosa Alvarez at 617-555-0142.";

        var result = await Run(bundle);

        Assert.Equal("Called [NAME] at [PHONE].", Note(Assert.IsType<Bundle>(result.Resource), 1).Text);
        Assert.True(result.Verification.IsClean);
    }

    [Fact]
    public async Task A_rewritten_string_keeps_its_element_id_and_allowed_extensions()
    {
        var bundle = Notes(1);
        var text = Note(bundle, 0).TextElement;
        text.Value = "Called 617-555-0142.";
        text.ElementId = "note-text";
        text.Extension.Add(new Extension(BirthSex, new Code("F")));
        text.Extension.Add(new Extension("http://example.org/unknown", new FhirString("anything")));

        var result = await Run(bundle);

        var cleaned = Note(Assert.IsType<Bundle>(result.Resource), 0).TextElement;
        Assert.Equal("Called [PHONE].", cleaned.Value);
        Assert.Equal("note-text", cleaned.ElementId);
        Assert.Equal([BirthSex], cleaned.Extension.Select(e => e.Url));
    }

    [Fact]
    public async Task A_custom_rule_can_still_de_identify_text_on_the_spot()
    {
        var bundle = Notes(1);
        Note(bundle, 0).Text = "Called 617-555-0142.";

        var result = await Run(bundle, SafeHarbor(o => o with { AdditionalRules = [new ImmediateNoteRule()] }));

        Assert.Equal("Called [PHONE].", Note(Assert.IsType<Bundle>(result.Resource), 0).Text);
    }

    [Fact]
    public async Task Text_cannot_be_queued_once_the_queue_is_running()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Run(Notes(1), SafeHarbor(o => o with { AdditionalRules = [new RequeueingRule()] })));

        Assert.Contains("cannot be queued", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(257)]
    public void Rejects_a_nonsensical_limit(int limit) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new FhirDeidentifier(SafeHarbor(o => o with { MaxFreeTextConcurrency = limit })));

    // One patient and a number of observations, each with a note.
    private static Bundle Notes(int count)
    {
        var bundle = new Bundle { Type = Bundle.BundleType.Collection };
        bundle.Entry.Add(new Bundle.EntryComponent { FullUrl = "urn:uuid:9f0a8a0e-0000-4000-8000-000000000001", Resource = new Patient { Id = "p" } });
        for (var i = 0; i < count; i++)
        {
            var observation = new Observation
            {
                Id = string.Create(CultureInfo.InvariantCulture, $"o{i}"),
                Status = ObservationStatus.Final,
                Code = new CodeableConcept("http://loinc.org", "8867-4"),
                Subject = new ResourceReference("Patient/p"),
            };
            observation.Note.Add(new Annotation { Text = string.Create(CultureInfo.InvariantCulture, $"Stable, note {i}.") });
            bundle.Entry.Add(new Bundle.EntryComponent { Resource = observation });
        }

        return bundle;
    }

    private static Patient Patient(Bundle bundle) => bundle.Entry.Select(e => e.Resource).OfType<Patient>().Single();

    private static Annotation Note(Bundle bundle, int index) =>
        bundle.Entry.Select(e => e.Resource).OfType<Observation>().ElementAt(index).Note.Single();

    // Design pattern: Test Double (Stub).
    // The pattern: a stand-in for a real dependency in a test. A stub returns canned answers.
    // Here: a custom rule that cleans an annotation's text itself, the way a rule written
    // before free text was queued would.
    private sealed class ImmediateNoteRule : IFhirElementRule
    {
        public async ValueTask<ElementAction> ApplyAsync(FhirElementContext context) =>
            context is { Parent: Annotation, Value: Markdown { Value: { } text } }
                ? ElementAction.ReplaceWith(new Markdown(await context.Session.DeidentifyTextAsync(text, context.Resource)))
                : ElementAction.Pass;
    }

    // Design pattern: Test Double (Stub).
    // The pattern: a stand-in for a real dependency in a test. A stub returns canned answers.
    // Here: a custom rule whose write-back queues more text, which the queue must refuse.
    private sealed class RequeueingRule : IFhirElementRule
    {
        public ValueTask<ElementAction> ApplyAsync(FhirElementContext context)
        {
            if (context is not { Parent: Annotation, Value: Markdown { Value: { } text } })
            {
                return ValueTask.FromResult(ElementAction.Pass);
            }

            context.Session.QueueText(text, context.Resource, _ => context.Session.QueueText(text, context.Resource, _ => { }));
            return ValueTask.FromResult(ElementAction.Keep);
        }
    }
}
