namespace DeidSharp.Detection;

// Design pattern: Decorator.
// The pattern: a wrapper with the same interface as the object it wraps, adding behavior
// before or after passing each call through, so callers can't tell the two apart.
// Here: it is an IPhiDetector around a detector stack; what it adds is dropping the ages
// Safe Harbor keeps, whichever detector reported them.

/// <summary>
/// Leaves out the ages under 90 a detector stack reports (<see cref="SafeHarborAges"/>): Safe
/// Harbor removes ages over 89 only, and NER models report every age, so a stack without this
/// removes clinical text Safe Harbor keeps. Every other span passes through.
/// </summary>
public sealed class SafeHarborAgeFilter(IPhiDetector inner) : IPhiDetector
{
    private readonly IPhiDetector _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public string Name => _inner.Name;

    public async Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var spans = await DetectorGuard.DetectAsync(_inner, request, cancellationToken).ConfigureAwait(false);
        return [.. spans.Where(s => s.Category != PhiCategory.Age || !SafeHarborAges.IsUnder90(request.Text[s.Start..s.End]))];
    }
}
