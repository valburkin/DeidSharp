using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using DeidSharp.Detection;
using DeidSharp.Presidio;
using DeidSharp.Tests.TestDoubles;

namespace DeidSharp.Tests.Presidio;

public class PresidioDetectorTests
{
    private static (PresidioDetector Detector, FakeHttpHandler Handler) Create(string body, HttpStatusCode status = HttpStatusCode.OK, Action<PresidioOptions>? configure = null)
    {
        var handler = new FakeHttpHandler(status, body);
        var options = new PresidioOptions { Endpoint = new Uri("http://presidio.test/") };
        configure?.Invoke(options);
        return (new PresidioDetector(new HttpClient(handler), options), handler);
    }

    private static Task<IReadOnlyList<PhiSpan>> Detect(PresidioDetector detector, string text) =>
        detector.DetectAsync(new DetectionRequest(text), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Sends_the_text_language_threshold_and_entities_to_analyze()
    {
        var (detector, handler) = Create("[]", configure: o =>
        {
            o.Language = "es";
            o.ScoreThreshold = 0.5;
            o.Entities = ["PERSON", "PHONE_NUMBER"];
        });

        await Detect(detector, "Hola Juan");

        var (method, uri, body) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("http://presidio.test/analyze", uri!.ToString());
        using var json = JsonDocument.Parse(body!);
        Assert.Equal("Hola Juan", json.RootElement.GetProperty("text").GetString());
        Assert.Equal("es", json.RootElement.GetProperty("language").GetString());
        Assert.Equal(0.5, json.RootElement.GetProperty("score_threshold").GetDouble());
        Assert.Equal(["PERSON", "PHONE_NUMBER"], json.RootElement.GetProperty("entities").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Leaves_entities_out_when_none_are_configured()
    {
        var (detector, handler) = Create("[]");
        await Detect(detector, "x");
        using var json = JsonDocument.Parse(handler.Requests.Single().Body!);
        Assert.False(json.RootElement.TryGetProperty("entities", out _));
    }

    [Fact]
    public async Task Maps_presidio_entities_to_phi_categories()
    {
        var (detector, _) = Create("""
            [{"entity_type":"PERSON","start":0,"end":10,"score":0.85},
             {"entity_type":"PHONE_NUMBER","start":18,"end":30,"score":0.4},
             {"entity_type":"DATE_TIME","start":34,"end":47,"score":0.85}]
            """);

        var spans = await Detect(detector, "John Smith called 617-555-0123 on March 5, 2024.");

        Assert.Equal([PhiCategory.Name, PhiCategory.Phone, PhiCategory.Date], spans.Select(s => s.Category));
        Assert.All(spans, s => Assert.Equal(["presidio"], s.Sources));
        Assert.Equal(0.4, spans[1].Score);
    }

    [Fact]
    public async Task Records_the_recognizer_in_the_source_only_when_asked()
    {
        const string body = """
            [{"entity_type":"PERSON","start":0,"end":4,"score":0.9,"recognition_metadata":{"recognizer_name":"ClinicalNerRecognizer"}},
             {"entity_type":"DATE_TIME","start":8,"end":12,"score":0.9,"analysis_explanation":{"recognizer":"SpacyRecognizer"}},
             {"entity_type":"PERSON","start":13,"end":16,"score":0.9}]
            """;
        var (quiet, quietHandler) = Create(body);
        var (reporting, reportingHandler) = Create(body, configure: o => o.ReportRecognizers = true);

        var plain = await Detect(quiet, "John on 2024 Ann");
        var named = await Detect(reporting, "John on 2024 Ann");

        Assert.All(plain, s => Assert.Equal(["presidio"], s.Sources));
        Assert.Equal([["presidio:ClinicalNerRecognizer"], ["presidio:SpacyRecognizer"], ["presidio"]], named.Select(s => s.Sources));
        using var quietRequest = JsonDocument.Parse(quietHandler.Requests.Single().Body!);
        using var reportingRequest = JsonDocument.Parse(reportingHandler.Requests.Single().Body!);
        Assert.False(quietRequest.RootElement.TryGetProperty("return_decision_process", out _));
        Assert.True(reportingRequest.RootElement.GetProperty("return_decision_process").GetBoolean());
    }

    [Theory]
    [InlineData("Called John Smith")]
    [InlineData("")]
    [InlineData("SpacyRecognizer\n")]
    public async Task Leaves_out_a_recognizer_name_that_is_not_a_plain_identifier(string name)
    {
        var body = JsonSerializer.Serialize(new[] { new Dictionary<string, object>
        {
            ["entity_type"] = "PERSON", ["start"] = 0, ["end"] = 4, ["score"] = 0.9,
            ["recognition_metadata"] = new Dictionary<string, string> { ["recognizer_name"] = name },
        } });
        var (detector, _) = Create(body, configure: o => o.ReportRecognizers = true);

        var span = Assert.Single(await Detect(detector, "John"));

        Assert.Equal(["presidio"], span.Sources);
    }

    [Fact]
    public async Task Converts_python_code_point_offsets_to_dotnet_string_indices()
    {
        // Real Presidio output for this text: PERSON at code points [2, 12).
        const string text = "\U0001F600 John Smith called";
        var (detector, _) = Create("""[{"entity_type":"PERSON","start":2,"end":12,"score":0.85}]""");

        var span = Assert.Single(await Detect(detector, text));

        Assert.Equal("John Smith", text[span.Start..span.End]);
    }

    [Fact]
    public async Task A_span_covering_part_of_a_word_is_widened_to_the_whole_word()
    {
        const string text = "seen by Okafor today";
        var (detector, _) = Create("""[{"entity_type":"PERSON","start":8,"end":11,"score":0.9}]""");

        var span = Assert.Single(await Detect(detector, text));

        Assert.Equal("Okafor", text[span.Start..span.End]);
    }

    [Fact]
    public async Task An_identifier_covering_part_of_a_token_is_widened_to_the_whole_token()
    {
        const string text = "Email (rosa.alvarez@mailbox.example).";
        var (detector, _) = Create("""[{"entity_type":"EMAIL_ADDRESS","start":7,"end":11,"score":0.9}]""");

        var span = Assert.Single(await Detect(detector, text));

        Assert.Equal("rosa.alvarez@mailbox.example", text[span.Start..span.End]);
    }

    [Fact]
    public async Task Ignores_entities_that_are_not_identifiers()
    {
        var (detector, _) = Create("""[{"entity_type":"NRP","start":0,"end":5,"score":0.9},{"entity_type":"crypto","start":6,"end":9,"score":0.9}]""");
        Assert.Empty(await Detect(detector, "Irish abc"));
    }

    [Fact]
    public async Task An_unknown_entity_is_still_redacted_as_an_identifier()
    {
        var (detector, _) = Create("""[{"entity_type":"SOMETHING_NEW","start":0,"end":5,"score":0.9}]""");
        Assert.Equal(PhiCategory.OtherIdentifier, Assert.Single(await Detect(detector, "abcde")).Category);
    }

    [Fact]
    public async Task The_entity_map_can_be_overridden()
    {
        var (detector, _) = Create("""[{"entity_type":"HOSPITAL","start":0,"end":5,"score":0.9}]""",
            configure: o => o.EntityMap["HOSPITAL"] = PhiCategory.Organization);
        Assert.Equal(PhiCategory.Organization, Assert.Single(await Detect(detector, "abcde")).Category);
    }

    [Fact]
    public async Task Scores_outside_zero_to_one_are_clamped()
    {
        var (detector, _) = Create("""[{"entity_type":"PERSON","start":0,"end":1,"score":1.7}]""");
        Assert.Equal(1.0, Assert.Single(await Detect(detector, "a")).Score);
    }

    [Theory]
    [InlineData("""[{"entity_type":"PERSON","start":0,"end":99,"score":0.9}]""")]
    [InlineData("""[{"entity_type":"PERSON","start":3,"end":3,"score":0.9}]""")]
    [InlineData("""[{"entity_type":"PERSON","start":-1,"end":2,"score":0.9}]""")]
    [InlineData("""[{"start":0,"end":2,"score":0.9}]""")]
    [InlineData("null")]
    [InlineData("{\"error\":\"x\"}")]
    [InlineData("not json")]
    public async Task Unusable_answers_are_failures_not_empty_results(string body)
    {
        var (detector, _) = Create(body);
        await Assert.ThrowsAsync<PhiDetectionException>(() => Detect(detector, "abcde"));
    }

    [Fact]
    public async Task An_http_error_is_a_failure_and_never_echoes_the_response()
    {
        var (detector, _) = Create("""{"error":"text was: John Smith"}""", HttpStatusCode.InternalServerError);

        var ex = await Assert.ThrowsAsync<PhiDetectionException>(() => Detect(detector, "John Smith"));

        Assert.Contains("500", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("John", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unreachable_service_is_a_failure()
    {
        var handler = new FakeHttpHandler(_ => throw new HttpRequestException("connection refused"));
        var detector = new PresidioDetector(new HttpClient(handler), new PresidioOptions());
        await Assert.ThrowsAsync<PhiDetectionException>(() => Detect(detector, "x"));
    }

    [Fact]
    public async Task A_slow_service_times_out_as_a_failure()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, "[]");
        var detector = new PresidioDetector(new HttpClient(new DelayingHandler(handler)), new PresidioOptions { Timeout = TimeSpan.FromMilliseconds(50) });

        await Assert.ThrowsAsync<PhiDetectionException>(() => Detect(detector, "x"));
    }

    [Fact]
    public async Task Cancellation_by_the_caller_is_reported_as_cancellation()
    {
        var (detector, _) = Create("[]");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => detector.DetectAsync(new DetectionRequest("x"), cts.Token));
    }

    [Fact]
    public async Task Empty_text_needs_no_call()
    {
        var (detector, handler) = Create("[]");
        Assert.Empty(await Detect(detector, ""));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Reports_health()
    {
        var (healthy, _) = Create("Presidio Analyzer service is up");
        var (down, _) = Create("", HttpStatusCode.ServiceUnavailable);
        var unreachable = new PresidioDetector(new HttpClient(new FakeHttpHandler(_ => throw new HttpRequestException())), new PresidioOptions());

        Assert.True(await healthy.IsHealthyAsync(TestContext.Current.CancellationToken));
        Assert.False(await down.IsHealthyAsync(TestContext.Current.CancellationToken));
        Assert.False(await unreachable.IsHealthyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_failure_of_a_handler_in_the_pipeline_is_a_detection_failure()
    {
        // As a resilience handler fails: an open circuit breaker, a full rate limiter.
        var detector = new PresidioDetector(new HttpClient(new FakeHttpHandler(_ => throw new InvalidOperationException("breaker open"))), new PresidioOptions());

        var error = await Assert.ThrowsAsync<PhiDetectionException>(() => Detect(detector, "Rosa Alvarez"));

        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    [Fact]
    public async Task A_timeout_or_any_other_failure_is_unhealthy_not_an_exception()
    {
        var timedOut = new PresidioDetector(new HttpClient(new FakeHttpHandler(_ => throw new TaskCanceledException())), new PresidioOptions());
        var broken = new PresidioDetector(new HttpClient(new FakeHttpHandler(_ => throw new InvalidOperationException())), new PresidioOptions());

        Assert.False(await timedOut.IsHealthyAsync(TestContext.Current.CancellationToken));
        Assert.False(await broken.IsHealthyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_callers_own_cancellation_is_not_read_as_unhealthy()
    {
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();
        var (healthy, _) = Create("Presidio Analyzer service is up");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => healthy.IsHealthyAsync(canceled.Token));
    }

    [Theory]
    [InlineData("relative/")]
    [InlineData(null)]
    public void Rejects_an_endpoint_that_is_not_absolute(string? endpoint) =>
        Assert.Throws<ArgumentException>(() => new PresidioDetector(new HttpClient(), new PresidioOptions { Endpoint = endpoint is null ? null! : new Uri(endpoint, UriKind.Relative) }));

    [Fact]
    public void Rejects_other_nonsensical_options()
    {
        Assert.Throws<ArgumentException>(() => new PresidioDetector(new HttpClient(), new PresidioOptions { Name = " " }));
        Assert.Throws<ArgumentException>(() => new PresidioDetector(new HttpClient(), new PresidioOptions { ScoreThreshold = 2 }));
        Assert.Throws<ArgumentException>(() => new PresidioDetector(new HttpClient(), new PresidioOptions { Timeout = TimeSpan.Zero }));
    }

    [Fact]
    public void Registers_as_a_typed_http_client()
    {
        var services = new ServiceCollection();
        services.AddPresidioDetector(o =>
        {
            o.Endpoint = new Uri("http://sidecar:3000/");
            o.Name = "presidio-clinical";
        });

        using var provider = services.BuildServiceProvider();
        var detector = provider.GetRequiredService<PresidioDetector>();

        Assert.Equal("presidio-clinical", detector.Name);
    }

    [Fact]
    public void Invalid_registered_options_fail_when_resolved()
    {
        var services = new ServiceCollection();
        services.AddPresidioDetector(o => o.ScoreThreshold = 5);
        using var provider = services.BuildServiceProvider();
        Assert.ThrowsAny<Exception>(provider.GetRequiredService<PresidioDetector>);
    }

    // Design pattern: Test Double (Fake).
    // The pattern: a stand-in for a real dependency in a test. A fake works but takes a shortcut.
    // Here: waits before passing the request on, like a busy service, so the
    // detector's own timeout fires first. The wait is far longer than any timeout under
    // test: on a loaded machine both timers can fire late, and a short wait could end first.
    // It still ends, so a timeout that never fires fails the test instead of hanging it.
    private sealed class DelayingHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
