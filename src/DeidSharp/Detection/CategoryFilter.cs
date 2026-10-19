namespace DeidSharp.Detection;

// Design pattern: Decorator.
// The pattern: a wrapper with the same interface as the object it wraps, adding behavior
// before or after passing each call through, so callers can't tell the two apart.
// Here: it is an IPhiDetector around one detector; what it adds is keeping only the spans of
// some categories, so a stack can take one detector's strength (a clinical model's places)
// without the rest of its findings.

/// <summary>
/// Keeps only the spans of the given categories that a detector reports. Next to a language
/// model, a clinical NER model's places added nearly all of its recall, and its other spans
/// mostly cost precision (docs/evaluation/findings.md, F34). Filtering a detector lowers recall
/// wherever another detector doesn't cover what it leaves out: measure before using it.
/// </summary>
public sealed class CategoryFilter : IPhiDetector
{
    private readonly IPhiDetector _inner;
    private readonly HashSet<PhiCategory> _kept;

    /// <param name="inner">The detector whose spans are filtered.</param>
    /// <param name="kept">The categories to keep; at least one.</param>
    /// <param name="name">The filtered detector's name; default the inner detector's.</param>
    public CategoryFilter(IPhiDetector inner, IEnumerable<PhiCategory> kept, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(kept);
        _inner = inner;
        _kept = [.. kept];
        if (_kept.Count == 0)
        {
            throw new ArgumentException("Keep at least one category.", nameof(kept));
        }

        if (name is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
        }

        Name = name ?? inner.Name;
    }

    public string Name { get; }

    public async Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var spans = await DetectorGuard.DetectAsync(_inner, request, cancellationToken).ConfigureAwait(false);
        return [.. spans.Where(s => _kept.Contains(s.Category))];
    }
}
