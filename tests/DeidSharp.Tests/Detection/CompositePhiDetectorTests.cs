using DeidSharp.Detection;
using DeidSharp.Tests.TestDoubles;

namespace DeidSharp.Tests.Detection;

public class CompositePhiDetectorTests
{
    private static readonly DetectionRequest Request = new("Call John Smith at 555-0100");

    [Fact]
    public void Needs_at_least_one_detector() =>
        Assert.Throws<ArgumentException>(() => new CompositePhiDetector([]));

    [Fact]
    public void Rejects_a_null_detector() =>
        Assert.Throws<ArgumentException>(() => new CompositePhiDetector([new StubDetector("a"), null!]));

    [Fact]
    public void Rejects_two_detectors_with_the_same_name_so_span_sources_stay_unambiguous() =>
        Assert.Throws<ArgumentException>(() => new CompositePhiDetector([new StubDetector("a"), new StubDetector("a")]));

    [Fact]
    public void Its_name_lists_the_stack() =>
        Assert.Equal("a+b", new CompositePhiDetector([new StubDetector("a"), new StubDetector("b")]).Name);

    [Fact]
    public async Task Runs_every_detector_and_merges_their_spans()
    {
        var a = new StubDetector("a", new PhiSpan(5, 9, PhiCategory.Name, 0.6, "a"));
        var b = new StubDetector("b", new PhiSpan(5, 15, PhiCategory.Name, 0.9, "b"), new PhiSpan(19, 27, PhiCategory.Phone, 0.8, "b"));

        var spans = await new CompositePhiDetector([a, b]).DetectAsync(Request, TestContext.Current.CancellationToken);

        Assert.Equal(1, a.Calls);
        Assert.Equal(1, b.Calls);
        Assert.Equal([(5, 15), (19, 27)], spans.Select(s => (s.Start, s.End)));
        Assert.Equal(["a", "b"], spans[0].Sources);
    }

    [Fact]
    public async Task A_failing_detector_fails_the_whole_detection_and_names_the_culprit()
    {
        var composite = new CompositePhiDetector([new StubDetector("ok"), new FailingDetector("sidecar", new HttpRequestException("refused"))]);

        var ex = await Assert.ThrowsAsync<PhiDetectionException>(() => composite.DetectAsync(Request, TestContext.Current.CancellationToken));

        Assert.Contains("sidecar", ex.Message, StringComparison.Ordinal);
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task A_detector_returning_null_is_a_failure_not_an_empty_result()
    {
        var composite = new CompositePhiDetector([new FailingDetector("broken")]);
        await Assert.ThrowsAsync<PhiDetectionException>(() => composite.DetectAsync(Request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_span_outside_the_text_is_a_failure()
    {
        var composite = new CompositePhiDetector([new StubDetector("bad", new PhiSpan(0, 999, PhiCategory.Name, 1, "bad"))]);
        await Assert.ThrowsAsync<PhiDetectionException>(() => composite.DetectAsync(Request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_detection_exception_passes_through_unwrapped()
    {
        var original = new PhiDetectionException("inner");
        var composite = new CompositePhiDetector([new FailingDetector("x", original)]);

        var ex = await Assert.ThrowsAsync<PhiDetectionException>(() => composite.DetectAsync(Request, TestContext.Current.CancellationToken));

        Assert.Same(original, ex);
    }

    [Fact]
    public async Task Cancellation_by_the_caller_is_reported_as_cancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var composite = new CompositePhiDetector([new FailingDetector("slow", new OperationCanceledException(cts.Token))]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => composite.DetectAsync(Request, cts.Token));
    }
}
