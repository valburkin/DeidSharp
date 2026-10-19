using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Hl7.Fhir.Model;
using DeidSharp.Detection;
using DeidSharp.Diagnostics;
using DeidSharp.Fhir;
using DeidSharp.Tests.TestDoubles;
using DeidSharp.Text;
using DeidSharp.Verification;

namespace DeidSharp.Tests.Diagnostics;

/// <summary>
/// DeidSharp's metrics and traces: each detector run is timed and traced, failures and
/// refused outputs are counted, and nothing recorded carries the text. Listeners see the
/// whole process, so each test looks only at what its own uniquely named detector did.
/// </summary>
public sealed class DeidSharpTelemetryTests : IDisposable
{
    private const string Secret = "Rosa Alvarez";

    private readonly ConcurrentBag<(string Instrument, KeyValuePair<string, object?>[] Tags)> _measurements = [];
    private readonly ConcurrentBag<Activity> _activities = [];
    private readonly MeterListener _meters = new();
    private readonly ActivityListener _traces;

    public DeidSharpTelemetryTests()
    {
        _meters.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == DeidSharpTelemetry.Name)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _meters.SetMeasurementEventCallback<double>((instrument, _, tags, _) => _measurements.Add((instrument.Name, [.. tags])));
        _meters.SetMeasurementEventCallback<long>((instrument, _, tags, _) => _measurements.Add((instrument.Name, [.. tags])));
        _meters.Start();

        _traces = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DeidSharpTelemetry.Name,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _activities.Add,
        };
        ActivitySource.AddActivityListener(_traces);
    }

    [Fact]
    public async Task Each_detector_run_is_timed_and_traced_without_its_text()
    {
        var detector = new StubDetector("telemetry-probe", new PhiSpan(0, 12, PhiCategory.Name, 1.0, "telemetry-probe"));

        await new TextDeidentifier(detector, new TagReplacement()).DeidentifyAsync($"{Secret} called.", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(_measurements, m => m.Instrument == "deidsharp.detector.duration"
            && HasTag(m.Tags, "deidsharp.detector", "telemetry-probe") && HasTag(m.Tags, "deidsharp.outcome", "ok"));
        var activity = Assert.Single(_activities, a => Equals(a.GetTagItem("deidsharp.detector"), "telemetry-probe"));
        Assert.Equal(1, activity.GetTagItem("deidsharp.spans"));
        Assert.DoesNotContain(activity.TagObjects, tag => Quotes(tag.Value));
        Assert.DoesNotContain(_measurements.SelectMany(m => m.Tags), tag => Quotes(tag.Value));
    }

    [Fact]
    public async Task A_composite_is_traced_through_its_detectors_once_each()
    {
        var stack = new CompositePhiDetector([new StubDetector("telemetry-first"), new StubDetector("telemetry-second")]);

        await new TextDeidentifier(stack, new TagReplacement()).DeidentifyAsync("x", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(_activities, a => Equals(a.GetTagItem("deidsharp.detector"), "telemetry-first"));
        Assert.Single(_activities, a => Equals(a.GetTagItem("deidsharp.detector"), "telemetry-second"));
        Assert.DoesNotContain(_activities, a => a.GetTagItem("deidsharp.detector") is string name && name.Contains('+', StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_failing_detector_is_recorded_as_failed_and_its_message_is_not()
    {
        var detector = new FailingDetector("telemetry-failing", new InvalidOperationException($"could not read {Secret}"));

        await Assert.ThrowsAsync<PhiDetectionException>(() =>
            new TextDeidentifier(detector, new TagReplacement()).DeidentifyAsync("x", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains(_measurements, m => m.Instrument == "deidsharp.detector.duration"
            && HasTag(m.Tags, "deidsharp.detector", "telemetry-failing") && HasTag(m.Tags, "deidsharp.outcome", "failed"));
        var activity = Assert.Single(_activities, a => Equals(a.GetTagItem("deidsharp.detector"), "telemetry-failing"));
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Null(activity.StatusDescription);
    }

    [Fact]
    public async Task A_refused_output_is_counted_and_replaces_nothing()
    {
        var detector = new StubDetector("telemetry-blind", new PhiSpan(0, 4, PhiCategory.Organization, 1.0, "telemetry-blind"));
        var known = KnownIdentifiers.Empty.AddName(Secret);

        await Assert.ThrowsAsync<ResidualPhiException>(() =>
            new TextDeidentifier(detector, new TagReplacement()).DeidentifyAsync($"Clinic: {Secret} called.", new TextContext("p", known), TestContext.Current.CancellationToken));

        Assert.Contains(_measurements, m => m.Instrument == "deidsharp.residual_phi.refusals" && HasTag(m.Tags, "deidsharp.stage", "text"));
        Assert.DoesNotContain(_measurements, m => m.Instrument == "deidsharp.text.replacements" && HasTag(m.Tags, "deidsharp.category", "Organization"));
    }

    [Fact]
    public async Task A_fhir_run_is_traced_by_resource_type_and_mode_only()
    {
        var patient = new Patient { Id = "telemetry-patient" };
        patient.Name.Add(new HumanName { Family = "Alvarez", Given = ["Rosa"] });

        await new FhirDeidentifier().DeidentifyAsync(patient, TestContext.Current.CancellationToken);

        // Other tests de-identify patients at the same time: every such span must hold up.
        var spans = _activities.Where(a => a.OperationName == "deidsharp.fhir.deidentify" && Equals(a.GetTagItem("deidsharp.fhir.resource_type"), "Patient")).ToList();
        Assert.NotEmpty(spans);
        Assert.All(spans, span =>
        {
            Assert.Contains(span.GetTagItem("deidsharp.fhir.mode") as string, Enum.GetNames<FhirDeidentificationMode>());
            Assert.DoesNotContain(span.TagObjects, tag => Quotes(tag.Value) || tag.Value?.ToString()?.Contains("telemetry-patient", StringComparison.Ordinal) == true);
        });
    }

    public void Dispose()
    {
        _meters.Dispose();
        _traces.Dispose();
    }

    private static bool HasTag(KeyValuePair<string, object?>[] tags, string key, string value) =>
        tags.Any(t => t.Key == key && Equals(t.Value, value));

    private static bool Quotes(object? value) => value?.ToString()?.Contains("Alvarez", StringComparison.Ordinal) == true;
}
