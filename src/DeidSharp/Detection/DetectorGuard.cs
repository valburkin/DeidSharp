using System.Diagnostics;
using DeidSharp.Diagnostics;

namespace DeidSharp.Detection;

/// <summary>
/// Runs one detector and holds it to the <see cref="IPhiDetector"/> contract: any failure
/// becomes a <see cref="PhiDetectionException"/> naming the detector, a null result is a
/// failure (never "no PHI found"), and every span must lie inside the text. Each run of a
/// detector is timed and traced (<see cref="DeidSharpTelemetry"/>); a composite is not,
/// since each of its detectors already is.
/// </summary>
internal static class DetectorGuard
{
    public static async Task<IReadOnlyList<PhiSpan>> DetectAsync(
        IPhiDetector detector, DetectionRequest request, CancellationToken cancellationToken)
    {
        if (detector is CompositePhiDetector)
        {
            return await RunAsync(detector, request, cancellationToken).ConfigureAwait(false);
        }

        using var activity = DeidSharpTelemetry.Source.CreateActivity("deidsharp.detect", ActivityKind.Internal)?.Start();
        activity?.SetTag("deidsharp.detector", detector.Name);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var spans = await RunAsync(detector, request, cancellationToken).ConfigureAwait(false);
            DeidSharpTelemetry.RecordDetection(detector.Name, Stopwatch.GetElapsedTime(started), failed: false);
            activity?.SetTag("deidsharp.spans", spans.Count);
            return spans;
        }
        catch (PhiDetectionException)
        {
            DeidSharpTelemetry.RecordDetection(detector.Name, Stopwatch.GetElapsedTime(started), failed: true);

            // The status says only that it failed: the exception's message is not recorded.
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
    }

    private static async Task<IReadOnlyList<PhiSpan>> RunAsync(
        IPhiDetector detector, DetectionRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<PhiSpan>? spans;
        try
        {
            spans = await detector.DetectAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (PhiDetectionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Only the type is named: a detector's exception may hold the text, in its message or
            // in a property (a regex timeout's Input) that a structured logger writes out, so it is
            // not passed on as the inner exception.
            throw new PhiDetectionException($"Detector '{detector.Name}' failed with {ex.GetType().Name}.");
        }

        if (spans is null)
        {
            throw new PhiDetectionException($"Detector '{detector.Name}' returned no result.");
        }

        SpanMerger.EnsureWithin(request.Text, spans, detector.Name);
        return spans;
    }
}
