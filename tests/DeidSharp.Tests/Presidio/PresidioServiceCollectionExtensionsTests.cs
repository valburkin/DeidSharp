using System.Net;
using Microsoft.Extensions.DependencyInjection;
using DeidSharp.Detection;
using DeidSharp.Presidio;
using DeidSharp.Tests.TestDoubles;

namespace DeidSharp.Tests.Presidio;

/// <summary>
/// Registering Presidio detectors: one as a typed client, or several by name, each with its
/// own endpoint, options and HTTP client, so the stock image and the clinical sidecar stack.
/// </summary>
public class PresidioServiceCollectionExtensionsTests
{
    [Fact]
    public async Task Two_named_detectors_each_call_their_own_service_under_their_own_name()
    {
        var stock = new FakeHttpHandler(HttpStatusCode.OK, """[{"entity_type": "PERSON", "start": 0, "end": 4, "score": 0.9}]""");
        var clinical = new FakeHttpHandler(HttpStatusCode.OK, """[{"entity_type": "PERSON", "start": 5, "end": 10, "score": 0.9}]""");
        var services = new ServiceCollection();
        services.AddPresidioDetector("presidio", o => o.Endpoint = new Uri("http://stock.test/"))
            .ConfigurePrimaryHttpMessageHandler(() => stock);
        services.AddPresidioDetector("clinical", o => o.Endpoint = new Uri("http://clinical.test/"))
            .ConfigurePrimaryHttpMessageHandler(() => clinical);
        await using var provider = services.BuildServiceProvider();

        var stack = new CompositePhiDetector([
            provider.GetRequiredKeyedService<PresidioDetector>("presidio"),
            provider.GetRequiredKeyedService<PresidioDetector>("clinical"),
        ]);
        var spans = await stack.DetectAsync(new DetectionRequest("Rosa Okafo called"), TestContext.Current.CancellationToken);

        Assert.Equal(new Uri("http://stock.test/analyze"), Assert.Single(stock.Requests).Uri);
        Assert.Equal(new Uri("http://clinical.test/analyze"), Assert.Single(clinical.Requests).Uri);
        Assert.Equal([["presidio"], ["clinical"]], spans.Select(s => s.Sources));
    }

    [Fact]
    public async Task A_named_detector_can_still_take_its_own_source_name()
    {
        var services = new ServiceCollection();
        services.AddPresidioDetector("clinical", o =>
        {
            o.Endpoint = new Uri("http://clinical.test/");
            o.Name = "presidio-clinical";
        });
        await using var provider = services.BuildServiceProvider();

        Assert.Equal("presidio-clinical", provider.GetRequiredKeyedService<PresidioDetector>("clinical").Name);
    }

    [Fact]
    public async Task A_named_detector_with_bad_options_is_refused_when_resolved()
    {
        var services = new ServiceCollection();
        services.AddPresidioDetector("clinical", o => o.Endpoint = new Uri("relative/", UriKind.Relative));
        await using var provider = services.BuildServiceProvider();

        Assert.Throws<ArgumentException>(() => provider.GetRequiredKeyedService<PresidioDetector>("clinical"));
    }

    [Fact]
    public void A_second_detector_under_the_same_name_is_refused()
    {
        var services = new ServiceCollection();
        services.AddPresidioDetector("presidio", o => o.Endpoint = new Uri("http://stock.test/"));

        Assert.Throws<ArgumentException>(() => services.AddPresidioDetector("presidio", o => o.Endpoint = new Uri("http://other.test/")));
    }
}
