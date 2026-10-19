using System.Collections.Concurrent;
using DeidSharp.Detection;

namespace DeidSharp.Evaluation.Cli;

// Design pattern: Decorator.
// The pattern: a wrapper with the same interface as the object it wraps, adding behavior
// before or after passing each call through, so callers can't tell the two apart.
// Here: it is an IPhiDetector around an IPhiDetector, and stacks into a composite like any
// other detector; what it adds is the cache described below.

// Design pattern: Memoization (a cache keyed by the input).
// The pattern: the result of an expensive computation is stored against its input,
// so the same input never pays for it twice.
// Here: a run scores several stacks that share a Presidio service ("+ presidio",
// "+ presidio + clinical"). Each note goes to each service once; the later stacks reuse
// its answer instead of waiting on the model again.

/// <summary>Remembers another detector's spans for each text it has seen. For one run's corpus, not for a long-lived service.</summary>
internal sealed class MemoizingDetector(IPhiDetector inner) : IPhiDetector
{
    private readonly ConcurrentDictionary<string, Lazy<Task<IReadOnlyList<PhiSpan>>>> _answers = new(StringComparer.Ordinal);

    public string Name => inner.Name;

    // Spans depend on known identifiers too; the scorer never passes any, so the text is the whole key.
    public Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Known.Count == 0
            ? _answers.GetOrAdd(request.Text, text => new Lazy<Task<IReadOnlyList<PhiSpan>>>(() => inner.DetectAsync(new DetectionRequest(text), cancellationToken))).Value
            : inner.DetectAsync(request, cancellationToken);
    }
}
