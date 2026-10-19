using DeidSharp.Detection;

namespace DeidSharp.Evaluation.Cli;

// Design pattern: Decorator.
// The pattern: a wrapper with the same interface as the object it wraps, adding behavior
// before or after passing each call through, so callers can't tell the two apart.
// Here: it is an IPhiDetector around an IPhiDetector; what it adds is dropping the spans
// that only the named recognizers reported, so a run can measure what a sidecar would
// find without them before anyone changes the sidecar.

/// <summary>Leaves out the spans whose every source is one of the dropped ones. For experiments, never for publishing.</summary>
internal sealed class SourceFilteringDetector(IPhiDetector inner, IReadOnlySet<string> dropped) : IPhiDetector
{
    public string Name => inner.Name;

    public async Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default)
    {
        var spans = await inner.DetectAsync(request, cancellationToken);
        return [.. spans.Where(s => !s.Sources.All(dropped.Contains))];
    }
}
