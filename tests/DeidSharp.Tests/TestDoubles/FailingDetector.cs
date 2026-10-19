using DeidSharp.Detection;

namespace DeidSharp.Tests.TestDoubles;

// Design pattern: Test Double (Stub).
// The pattern: a stand-in for a real dependency in a test. A stub returns canned answers.
// Here: its canned answer is a failure (a sidecar that is down, a detector that returns
// null), to prove de-identification fails closed instead of continuing without it.

/// <summary>Throws the given exception, or returns null when none is given.</summary>
internal sealed class FailingDetector(string name, Exception? exception = null) : IPhiDetector
{
    public string Name { get; } = name;

    public Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default) =>
        exception is null ? Task.FromResult<IReadOnlyList<PhiSpan>>(null!) : Task.FromException<IReadOnlyList<PhiSpan>>(exception);
}
