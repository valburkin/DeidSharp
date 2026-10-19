namespace DeidSharp.Detection;

// Design pattern: Composite.
// The pattern: a group of objects is used through the same interface as a single
// object, so callers don't care whether they hold one or many.
// Here: several detectors stacked together are themselves an IPhiDetector. They run
// in parallel and their spans are unioned (recall first: a span any detector reports
// is redacted), then merged so overlapping findings become one replacement.

/// <summary>
/// Runs several detectors on the same text and merges what they find. If any detector
/// fails, the whole detection fails (<see cref="PhiDetectionException"/>): a stack with a
/// missing layer is not the stack that was configured.
/// </summary>
public sealed class CompositePhiDetector : IPhiDetector
{
    public CompositePhiDetector(IEnumerable<IPhiDetector> detectors)
    {
        ArgumentNullException.ThrowIfNull(detectors);
        Detectors = [.. detectors];
        if (Detectors.Count == 0)
        {
            throw new ArgumentException("A composite needs at least one detector.", nameof(detectors));
        }

        // Callers that don't use nullable annotations can still pass a null.
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (Detectors.Any(d => d is null))
        {
            throw new ArgumentException("A composite cannot hold a null detector.", nameof(detectors));
        }

        UniqueNames.Ensure(Detectors.Select(d => d.Name), "Detector", nameof(detectors));
    }

    public string Name => string.Join("+", Detectors.Select(d => d.Name));

    public IReadOnlyList<IPhiDetector> Detectors { get; }

    public async Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var results = await Task.WhenAll(Detectors.Select(d => DetectorGuard.DetectAsync(d, request, cancellationToken)))
            .ConfigureAwait(false);
        return SpanMerger.Merge(request.Text, results.SelectMany(r => r));
    }
}
