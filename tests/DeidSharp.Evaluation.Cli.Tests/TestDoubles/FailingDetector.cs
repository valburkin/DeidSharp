using DeidSharp.Detection;

namespace DeidSharp.Evaluation.Cli.Tests.TestDoubles;

// Design pattern: Test Double (Stub).
// The pattern: a stand-in for a real dependency in a test. A stub returns canned answers.
// Here: its canned answer is null, a broken detector's answer, to prove a wrapper around a
// stack fails closed instead of reading it as "no PHI found".

/// <summary>Returns null instead of a list of spans.</summary>
internal sealed class FailingDetector(string name) : IPhiDetector
{
    public string Name { get; } = name;

    public Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PhiSpan>>(null!);
}
