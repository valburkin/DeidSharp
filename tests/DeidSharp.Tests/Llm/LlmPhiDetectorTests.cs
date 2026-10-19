using DeidSharp.Detection;
using DeidSharp.Llm;
using DeidSharp.Tests.TestDoubles;
using Microsoft.Extensions.AI;

namespace DeidSharp.Tests.Llm;

public class LlmPhiDetectorTests
{
    // Design pattern: Test Double (Stub and Spy).
    // The pattern: a stand-in for a real dependency in a test. A stub returns canned answers;
    // a spy records how it was called.
    // Here: a chat model that answers each text with what the test chose, and keeps the
    // messages and options it was sent.
    private sealed class ScriptedChat(Func<string, string> answer, Func<string, bool>? cutOff = null) : IChatClient
    {
        public List<(List<ChatMessage> Messages, ChatOptions? Options)> Calls { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var list = messages.ToList();
            Calls.Add((list, options));
            var text = list[^1].Text;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer(text)))
            {
                FinishReason = cutOff?.Invoke(text) == true ? ChatFinishReason.Length : ChatFinishReason.Stop,
            });
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private static async Task<string> Found(string text, string answer, LlmDetectorOptions? options = null)
    {
        var detector = new LlmPhiDetector(new ScriptedChat(_ => answer), options);
        var spans = await detector.DetectAsync(new DetectionRequest(text), TestContext.Current.CancellationToken);
        return string.Join("|", spans.OrderBy(s => s.Start).Select(s => $"{text[s.Start..s.End]}:{s.Category}"));
    }

    [Fact]
    public async Task Finds_every_whole_word_mention_of_what_the_model_lists()
    {
        const string text = "Dr. Hart saw her on 3/14. Hart called Hartford. HART agreed.";

        var found = await Found(text, """{"phi": [{"text": "Hart", "type": "name"}, {"text": "3/14", "type": "date"}]}""");

        Assert.Equal("Hart:Name|3/14:Date|Hart:Name", found);
    }

    [Fact]
    public async Task Falls_back_to_ignoring_case_and_drops_what_the_text_does_not_hold()
    {
        const string text = "Called ROSA MENDES at home.";

        var found = await Found(text, """{"phi": [{"text": "Rosa Mendes", "type": "name"}, {"text": "Ellen Hart", "type": "name"}, {"text": "x", "type": "name"}]}""");

        Assert.Equal("ROSA MENDES:Name", found);
    }

    [Fact]
    public async Task Reads_json_wrapped_in_a_code_fence_and_maps_unknown_types_to_other_identifiers()
    {
        const string text = "Badge 44-AX9 on file.";

        var found = await Found(text, "```json\n{\"phi\": [{\"text\": \"44-AX9\", \"type\": \"badge\"}]}\n```");

        Assert.Equal("44-AX9:OtherIdentifier", found);
    }

    [Theory]
    [InlineData("Sorry, the patient Rosa Mendes is not something I can list.")]
    [InlineData("{\"phi\": [{\"text\": \"Rosa Mendes\", \"type\": ")]
    [InlineData("{\"names\": [\"Rosa Mendes\"]}")]
    public async Task Fails_closed_without_quoting_an_answer_it_cannot_read(string answer)
    {
        var detector = new LlmPhiDetector(new ScriptedChat(_ => answer));

        var e = await Assert.ThrowsAsync<PhiDetectionException>(() => detector.DetectAsync(new DetectionRequest("Rosa Mendes called."), TestContext.Current.CancellationToken));

        Assert.DoesNotContain("Rosa", e.Message, StringComparison.Ordinal);
        Assert.Null(e.InnerException);
    }

    [Fact]
    public async Task Sends_a_long_text_in_overlapping_pieces_and_places_spans_in_the_whole_text()
    {
        var text = string.Concat(Enumerable.Repeat("Seen by Hart today. ", 60)) + "Signed Mendes.";
        var chat = new ScriptedChat(chunk => chunk.Contains("Mendes", StringComparison.Ordinal)
            ? """{"phi": [{"text": "Mendes", "type": "name"}]}"""
            : """{"phi": []}""");
        var detector = new LlmPhiDetector(chat, new LlmDetectorOptions { ChunkLength = 500, ChunkOverlap = 50 });

        var spans = await detector.DetectAsync(new DetectionRequest(text), TestContext.Current.CancellationToken);

        Assert.True(chat.Calls.Count > 2);
        Assert.All(chat.Calls, c => Assert.True(c.Messages[^1].Text.Length <= 500));
        var span = Assert.Single(spans);
        Assert.Equal("Mendes", text[span.Start..span.End]);
    }

    [Fact]
    public async Task Asks_again_in_halves_when_an_answer_is_cut_off()
    {
        var text = string.Concat(Enumerable.Repeat("Seen by Hart. ", 100));
        var chat = new ScriptedChat(
            _ => """{"phi": [{"text": "Hart", "type": "name"}]}""",
            chunk => chunk.Length > 600);
        var detector = new LlmPhiDetector(chat, new LlmDetectorOptions { ChunkLength = 1_400, ChunkOverlap = 100 });

        var spans = await detector.DetectAsync(new DetectionRequest(text), TestContext.Current.CancellationToken);

        Assert.Equal(100, spans.Select(s => s.Start).Distinct().Count());
        Assert.Contains(chat.Calls, c => c.Messages[^1].Text.Length <= 600);
    }

    [Fact]
    public async Task Fails_closed_saying_so_when_even_a_small_piece_is_cut_off()
    {
        var chat = new ScriptedChat(_ => "{\"phi\": [{\"text\": \"Ha", _ => true);
        var detector = new LlmPhiDetector(chat);

        var e = await Assert.ThrowsAsync<PhiDetectionException>(() => detector.DetectAsync(
            new DetectionRequest(string.Concat(Enumerable.Repeat("Seen by Hart. ", 100))), TestContext.Current.CancellationToken));

        Assert.Contains("cut off at the output limit", e.Message, StringComparison.Ordinal);
        Assert.All(chat.Calls, c => Assert.True(c.Messages[^1].Text.Length >= 100));
    }

    [Fact]
    public async Task Sends_the_instructions_and_examples_before_the_text_and_applies_the_provider_settings()
    {
        var chat = new ScriptedChat(_ => """{"phi": []}""");
        var prompt = LlmPrompt.Default with { Examples = [new LlmExample("Seen by Dr. Okafor.", [new LlmFinding("Okafor", "name")])] };
        var detector = new LlmPhiDetector(chat, new LlmDetectorOptions { Prompt = prompt, ConfigureRequest = o => o.Seed = 7 });

        await detector.DetectAsync(new DetectionRequest("No identifiers here."), TestContext.Current.CancellationToken);

        var (messages, options) = Assert.Single(chat.Calls);
        Assert.Equal([ChatRole.System, ChatRole.User, ChatRole.Assistant, ChatRole.User], messages.Select(m => m.Role));
        Assert.Contains("\"Okafor\"", messages[2].Text, StringComparison.Ordinal);
        Assert.Equal("No identifiers here.", messages[3].Text);
        Assert.Equal(0, options?.Temperature);
        Assert.Equal(7, options?.Seed);
    }

    [Fact]
    public async Task Drops_what_safe_harbor_keeps_unless_told_not_to()
    {
        const string text = "72 yo and 94 yo. Seen 2019 at 0800, today at 8pm, again 3/14 and on the 21st, back in March. Bed 12, MRN 4471902, badge A12.";
        const string answer = """
            {"phi": [{"text": "72 yo", "type": "age"}, {"text": "94 yo", "type": "age"}, {"text": "2019", "type": "date"},
                     {"text": "0800", "type": "date"}, {"text": "today", "type": "date"}, {"text": "8pm", "type": "date"},
                     {"text": "3/14", "type": "date"}, {"text": "21st", "type": "date"}, {"text": "March", "type": "date"},
                     {"text": "12", "type": "id"}, {"text": "4471902", "type": "id"}, {"text": "A12", "type": "other"}]}
            """;

        Assert.Equal("94 yo:Age|3/14:Date|21st:Date|March:Date|4471902:OtherIdentifier|A12:OtherIdentifier", await Found(text, answer));
        Assert.Equal(12, (await Found(text, answer, new LlmDetectorOptions { SafeHarborOnly = false })).Split('|').Length);
    }

    [Fact]
    public void Shipped_examples_list_only_text_their_note_holds()
    {
        // An example that lists something its note doesn't hold would teach the model to make PHI up.
        foreach (var prompt in new[] { LlmPrompt.WithExamples, LlmPrompt.WithNeutralExamples, LlmPrompt.Focused, LlmPrompt.WithPoolExamples })
        {
            Assert.NotEmpty(prompt.Examples);
            Assert.All(prompt.Examples.SelectMany(e => e.Phi.Select(p => (e.Text, p.Text))), pair =>
                Assert.Contains(pair.Item2, pair.Item1, StringComparison.Ordinal));
        }
        Assert.Empty(LlmPrompt.Default.Examples);
    }

    [Fact]
    public async Task Shows_retrieved_examples_after_the_prompts_own_and_before_the_text()
    {
        var chat = new ScriptedChat(_ => """{"phi": []}""");
        var pool = new FixedExampleRetriever([new LlmExample("Daughter Rosa called.", [new LlmFinding("Rosa", "name")]), new LlmExample("Vent settings PS 12/5.", [])]);
        var detector = new LlmPhiDetector(chat, new LlmDetectorOptions { Retriever = pool, RetrievedExamples = 1 });

        await detector.DetectAsync(new DetectionRequest("Son called this evening."), TestContext.Current.CancellationToken);

        var (messages, _) = Assert.Single(chat.Calls);
        Assert.Equal([ChatRole.System, ChatRole.User, ChatRole.Assistant, ChatRole.User], messages.Select(m => m.Role));
        Assert.Equal("Daughter Rosa called.", messages[1].Text);
        Assert.Equal("Son called this evening.", messages[3].Text);
    }

    [Fact]
    public void Refuses_options_out_of_range()
    {
        var chat = new ScriptedChat(_ => "");

        Assert.Throws<ArgumentOutOfRangeException>(() => new LlmPhiDetector(chat, new LlmDetectorOptions { ChunkLength = 100 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LlmPhiDetector(chat, new LlmDetectorOptions { ChunkOverlap = 1_500 }));
        Assert.Throws<ArgumentException>(() => new LlmPhiDetector(chat, new LlmDetectorOptions { Name = " " }));
    }
}
