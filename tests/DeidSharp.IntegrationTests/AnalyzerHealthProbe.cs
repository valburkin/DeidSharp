using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

namespace DeidSharp.IntegrationTests;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: Testcontainers asks each readiness check "ready yet?" until it says yes; this one
// asks the analyzer's /health with a short timeout, so a request that hangs is retried.

/// <summary>
/// Ready once the analyzer answers <c>GET /health</c> with a success status. A request that
/// times out or is refused counts as "not yet": Presidio's server accepts connections
/// before its worker has loaded the NER model, and a request sent then can hang. The
/// built-in HTTP check waits 100 seconds on such a request and then fails the fixture.
/// </summary>
internal sealed class AnalyzerHealthProbe(ushort port) : IWaitUntil
{
    // One client for every probe of the run; a hung request gives up after 5 seconds.
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public async Task<bool> UntilAsync(IContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);
        var health = new UriBuilder("http", container.Hostname, container.GetMappedPublicPort(port), "health").Uri;
        try
        {
            using var response = await Http.GetAsync(health);
            return response.IsSuccessStatusCode;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }
}
