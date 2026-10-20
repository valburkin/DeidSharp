using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DeidSharp.Presidio;

namespace DeidSharp.IntegrationTests;

// Design pattern: Test Fixture (shared).
// The pattern: expensive setup is done once and shared by the tests that need it,
// instead of once per test.
// Here: one analyzer container per image for the whole test run (an xUnit assembly
// fixture, AssemblyFixtures.cs). Starting one takes seconds (stock Presidio) to a minute
// (the clinical model), so every test class that needs it shares the one container.

// Design pattern: Template Method.
// The pattern: a base class fixes the steps of an algorithm and lets subclasses fill
// in particular steps, without changing the overall order.
// Here: InitializeAsync always checks the switches, starts the container and waits
// for /health; each subclass only supplies its Image and, optionally, an ExtraSwitch.

/// <summary>
/// Starts a Presidio-compatible analyzer container for the test run, when integration
/// tests are switched on (<c>DEIDSHARP_INTEGRATION=1</c>) and the image's own switch,
/// if any, is on too.
/// </summary>
public abstract class AnalyzerContainer : IAsyncLifetime
{
    private const ushort Port = 3000;

    // The clinical model takes up to a minute to load on a CPU, longer on a busy CI runner.
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(5);

    private IContainer? _container;

    protected abstract string Image { get; }

    /// <summary>Extra environment variable that must be "1" (null when none is needed).</summary>
    protected virtual string? ExtraSwitch => null;

    private string? SkipReason { get; set; }

    private Uri? Endpoint { get; set; }

    public async ValueTask InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("DEIDSHARP_INTEGRATION") != "1")
        {
            SkipReason = "Set DEIDSHARP_INTEGRATION=1 to run tests against analyzer containers.";
            return;
        }

        if (ExtraSwitch is { } name && Environment.GetEnvironmentVariable(name) != "1")
        {
            SkipReason = $"Set {name}=1 (after building the image, see docs/testing.md) to run these tests.";
            return;
        }

        _container = new ContainerBuilder(Image)
            .WithPortBinding(Port, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().AddCustomWaitStrategy(new AnalyzerHealthProbe(Port), o => o.WithTimeout(StartTimeout)))
            .Build();
        await _container.StartAsync();
        Endpoint = new UriBuilder("http", _container.Hostname, _container.GetMappedPublicPort(Port)).Uri;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>A detector for this container; skips the calling test when the container is off.</summary>
    public PresidioDetector Detector(string name = "presidio")
    {
        Assert.SkipWhen(SkipReason is not null, SkipReason ?? string.Empty);
        return new PresidioDetector(new HttpClient(), new PresidioOptions { Endpoint = Endpoint!, Name = name, Timeout = TimeSpan.FromMinutes(2) });
    }
}
