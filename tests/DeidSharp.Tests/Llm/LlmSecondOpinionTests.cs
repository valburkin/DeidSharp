using DeidSharp.Detection;
using DeidSharp.Llm;
using DeidSharp.Tests.TestDoubles;
using Microsoft.Extensions.AI;

namespace DeidSharp.Tests.Llm;

public class LlmSecondOpinionTests
{
    // Design pattern: Test Double (Stub and Spy).
    // The pattern: a stand-in for a real dependency in a test. A stub returns canned answers;
    // a spy records how it was called.
    // Here: a chat model that gives the verdict the test chose and keeps what it was asked.
    private sealed class Verdicts(string answer) : IChatClient
    {
        public List<string> Questions { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Questions.Add(messages.Last().Text);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private const string Note = "Foley draining. Seen by Hart. MRN 4471902. Called Rosa.";

    private static PhiSpan Span(string text, PhiCategory category, string source)
    {
        var at = Note.IndexOf(text, StringComparison.Ordinal);
        return new PhiSpan(at, at + text.Length, category, 0.8, source);
    }

    private static readonly PhiSpan Foley = Span("Foley", PhiCategory.Name, "gazetteer:name");
    private static readonly PhiSpan Hart = Span("Hart", PhiCategory.Name, "clinical:ClinicalNerRecognizer");
    private static readonly PhiSpan Mrn = Span("4471902", PhiCategory.MedicalRecordNumber, "pattern:mrn-labeled");
    private static readonly PhiSpan Rosa = Span("Rosa", PhiCategory.Name, "llm");

    [Fact]
    public async Task Drops_only_the_candidates_the_model_calls_not_phi()
    {
        var model = new Verdicts("""{"not_phi": [1]}""");
        var opinion = new LlmSecondOpinion(new StubDetector("stack", Foley, Hart, Mrn, Rosa), model);

        var spans = await opinion.DetectAsync(new DetectionRequest(Note), TestContext.Current.CancellationToken);

        Assert.Equal([Hart, Mrn, Rosa], spans);
        var question = Assert.Single(model.Questions);
        Assert.Contains("1. \"Foley\"", question, StringComparison.Ordinal);
        Assert.Contains("2. \"Hart\"", question, StringComparison.Ordinal);
        Assert.DoesNotContain("4471902\" (", question, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Rosa\" (", question, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Asks_nothing_when_only_trusted_detectors_found_spans_and_ignores_numbers_out_of_range()
    {
        var model = new Verdicts("""{"not_phi": [7, 0]}""");

        var trusted = await new LlmSecondOpinion(new StubDetector("stack", Mrn, Rosa), model).DetectAsync(new DetectionRequest(Note), TestContext.Current.CancellationToken);
        var kept = await new LlmSecondOpinion(new StubDetector("stack", Hart), model).DetectAsync(new DetectionRequest(Note), TestContext.Current.CancellationToken);

        Assert.Equal([Mrn, Rosa], trusted);
        Assert.Equal([Hart], kept);
        Assert.Single(model.Questions);
    }

    [Fact]
    public async Task Fails_closed_without_quoting_an_answer_it_cannot_read()
    {
        var opinion = new LlmSecondOpinion(new StubDetector("stack", Hart), new Verdicts("Hart is a name."));

        var e = await Assert.ThrowsAsync<PhiDetectionException>(() => opinion.DetectAsync(new DetectionRequest(Note), TestContext.Current.CancellationToken));

        Assert.DoesNotContain("Hart", e.Message, StringComparison.Ordinal);
    }
}
