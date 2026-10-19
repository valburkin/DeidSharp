using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DeidSharp.Diagnostics;

/// <summary>
/// The metrics and traces DeidSharp emits, through the .NET runtime's own
/// <see cref="Meter"/> and <see cref="ActivitySource"/>, so the core needs no package: any
/// listener (OpenTelemetry, dotnet-counters) picks them up by the name <see cref="Name"/>.
/// They carry detector names, categories, outcomes and counts only, never text, values
/// or paths: telemetry leaves the process, and an input may be PHI.
/// </summary>
public static class DeidSharpTelemetry
{
    /// <summary>The name of DeidSharp's meter and activity source.</summary>
    public const string Name = "DeidSharp";

    // Bucket boundaries in seconds: a rule-based detector takes milliseconds, a
    // transformer on a CPU up to tens of seconds.
    private static readonly double[] DurationBuckets = [0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60];

    private static readonly Meter Meter = new(Name, Version);

    private static readonly Histogram<double> DetectorDuration = Meter.CreateHistogram(
        "deidsharp.detector.duration",
        "s",
        "How long one detector took on one text, by outcome (ok or failed).",
        advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = DurationBuckets });

    private static readonly Counter<long> Replacements = Meter.CreateCounter<long>(
        "deidsharp.text.replacements", "{span}", "Spans of free text replaced in outputs that passed their check, by category.");

    private static readonly Counter<long> ResidualRefusals = Meter.CreateCounter<long>(
        "deidsharp.residual_phi.refusals", "{output}", "Outputs refused because they still held possible PHI, by stage.");

    /// <summary>
    /// The activity source DeidSharp's spans start from. A package or a pipeline built on
    /// DeidSharp may start its own spans here too, under the same rule: no text in them.
    /// </summary>
    public static ActivitySource Source { get; } = new(Name, Version);

    private static string? Version => typeof(DeidSharpTelemetry).Assembly.GetName().Version?.ToString();

    /// <summary>
    /// Counts an output refused because it still held possible PHI.
    /// </summary>
    /// <param name="stage">What refused it, e.g. "text" or "fhir": a fixed name, never a value.</param>
    public static void RecordResidualRefusal(string stage) =>
        ResidualRefusals.Add(1, new KeyValuePair<string, object?>("deidsharp.stage", stage));

    internal static void RecordDetection(string detector, TimeSpan duration, bool failed) =>
        DetectorDuration.Record(
            duration.TotalSeconds,
            new KeyValuePair<string, object?>("deidsharp.detector", detector),
            new KeyValuePair<string, object?>("deidsharp.outcome", failed ? "failed" : "ok"));

    internal static void RecordReplacement(PhiCategory category) =>
        Replacements.Add(1, new KeyValuePair<string, object?>("deidsharp.category", category.ToString()));
}
