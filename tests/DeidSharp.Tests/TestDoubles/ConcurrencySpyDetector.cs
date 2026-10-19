using DeidSharp.Detection;

namespace DeidSharp.Tests.TestDoubles;

// Design pattern: Test Double (Spy).
// The pattern: a stand-in for a real dependency in a test. A spy records how it was called.
// Here: a detector, like a sidecar on the network, that records how many calls were in
// flight at once. Calls wait at a barrier until a given number have arrived, so a test
// sees exactly how many ran together without depending on timing. A text holding the
// failure marker makes it throw at once.

/// <summary>Finds nothing, and remembers how many calls it served and the most at once.</summary>
internal sealed class ConcurrencySpyDetector(int releaseAt, string? failureMarker = null) : IPhiDetector
{
    // Long enough never to be reached when calls do run together; a run that does not
    // reach the barrier fails with a timeout instead of hanging.
    private static readonly TimeSpan BarrierTimeout = TimeSpan.FromSeconds(10);

    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _calls;
    private int _inFlight;
    private int _mostInFlight;

    public string Name => "spy";

    public int Calls => Volatile.Read(ref _calls);

    public int MostInFlight => Volatile.Read(ref _mostInFlight);

    public async Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default)
    {
        var inFlight = Interlocked.Increment(ref _inFlight);
        InterlockedMax(ref _mostInFlight, inFlight);
        var calls = Interlocked.Increment(ref _calls);
        try
        {
            if (failureMarker is not null && request.Text.Contains(failureMarker, StringComparison.Ordinal))
            {
                throw new HttpRequestException("The sidecar is down.");
            }

            if (calls >= releaseAt)
            {
                _released.TrySetResult();
            }

            await _released.Task.WaitAsync(BarrierTimeout, cancellationToken);
            return [];
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    private static void InterlockedMax(ref int target, int value)
    {
        var current = Volatile.Read(ref target);
        while (value > current)
        {
            var seen = Interlocked.CompareExchange(ref target, value, current);
            if (seen == current)
            {
                return;
            }

            current = seen;
        }
    }
}
