using DeidSharp.Detection;

namespace DeidSharp.Evaluation.Cli.Tests;

public class MemoizingDetectorTests
{
    // Design pattern: Test Double (Spy).
    // The pattern: a stand-in for a real dependency in a test. A spy records how it was called.
    // Here: a detector that counts its calls, so a test sees whether the cache answered instead.
    private sealed class CountingDetector : IPhiDetector
    {
        public int Calls { get; private set; }

        public string Name => "counting";

        public Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<PhiSpan>>([new PhiSpan(0, 1, PhiCategory.Name, 1.0, Name)]);
        }
    }

    [Fact]
    public async Task Asks_the_detector_once_per_text()
    {
        var inner = new CountingDetector();
        var memoized = new MemoizingDetector(inner);
        var ct = TestContext.Current.CancellationToken;

        var first = await memoized.DetectAsync(new DetectionRequest("Dr. Hart"), ct);
        var again = await memoized.DetectAsync(new DetectionRequest("Dr. Hart"), ct);
        await memoized.DetectAsync(new DetectionRequest("Dr. Okafor"), ct);

        Assert.Same(first, again);
        Assert.Equal(2, inner.Calls);
        Assert.Equal("counting", memoized.Name);
    }

    [Fact]
    public async Task Does_not_cache_a_request_with_known_identifiers()
    {
        var inner = new CountingDetector();
        var memoized = new MemoizingDetector(inner);
        var known = KnownIdentifiers.Empty.AddName("Hart");
        var ct = TestContext.Current.CancellationToken;

        await memoized.DetectAsync(new DetectionRequest("Dr. Hart", known), ct);
        await memoized.DetectAsync(new DetectionRequest("Dr. Hart", known), ct);

        Assert.Equal(2, inner.Calls);
    }
}
