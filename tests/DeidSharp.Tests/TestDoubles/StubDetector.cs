using DeidSharp.Detection;

namespace DeidSharp.Tests.TestDoubles;

// Design pattern: Test Double (Stub, Spy).
// The pattern: a stand-in for a real dependency in a test. A stub returns canned
// answers; a spy records how it was called.
// Here: a detector that returns the spans a test chose, so merging, replacement and
// composite behavior can be tested without real detection. It counts its calls, so a
// test can check that a configured detector was really used.

/// <summary>Returns fixed spans, whatever the text.</summary>
internal sealed class StubDetector(string name, params PhiSpan[] spans) : IPhiDetector
{
    // Counted atomically: a FHIR run calls one detector from several texts at once.
    private int _calls;

    public string Name { get; } = name;

    public int Calls => _calls;

    public Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult<IReadOnlyList<PhiSpan>>(spans);
    }
}
