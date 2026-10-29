using System.Text.Json;
using DeidSharp.Detection;
using DeidSharp.Evaluation.Cli.Tests.TestDoubles;

namespace DeidSharp.Evaluation.Cli.Tests;

public class DetectorStacksTests
{
    // The second opinion's requests carry the candidates' format: an object of "not_phi" numbers.
    private static bool IsSecondOpinion(JsonElement request) => request.GetRawText().Contains("not_phi", StringComparison.Ordinal);

    [Fact]
    public async Task Gives_the_detector_its_prompts_window_and_the_second_opinion_one_for_the_whole_note()
    {
        using var ollama = new FakeOllamaServer(r => IsSecondOpinion(r) ? """{"not_phi": []}""" : """{"phi": [{"text": "Hart", "type": "name"}]}""");
        using var stacks = new DetectorStacks(
            null, null, new HashSet<string>(StringComparer.Ordinal), llmModels: ["fictional-model"], llmEndpoint: ollama.Endpoint,
            llmPrompts: ["all-examples"], gazetteer: [false], secondOpinions: ["self"]);
        var stack = stacks.Stacks.Single(s => s.Name.EndsWith("second opinion with self-check", StringComparison.Ordinal));

        IReadOnlyList<PhiSpan> spans;
        try
        {
            spans = await stack.Detector.DetectAsync(new DetectionRequest("Hart called back.", KnownIdentifiers.Empty), TestContext.Current.CancellationToken);
        }
        finally
        {
            // What the server failed on, rather than the detector's error that followed.
            Assert.Empty(ollama.Errors);
        }

        Assert.Equal("Hart", "Hart called back."[spans.Single().Start..spans.Single().End]);
        Assert.Contains(ollama.Requests, r => !IsSecondOpinion(r));
        Assert.All(ollama.Requests.Where(r => !IsSecondOpinion(r)), r => Assert.Equal(8192, r.GetProperty("options").GetProperty("num_ctx").GetInt32()));
        Assert.Equal(16384, Assert.Single(ollama.Requests, IsSecondOpinion).GetProperty("options").GetProperty("num_ctx").GetInt32());
        Assert.All(ollama.Requests, r => Assert.False(r.GetProperty("think").GetBoolean()));
    }

    [Fact]
    public void Measures_each_variant_on_the_stacks_with_an_llm_layer_too()
    {
        using var stacks = new DetectorStacks(
            null, null, new HashSet<string>(StringComparer.Ordinal), variants: ["repeated-names"], llmModels: ["fictional-model"],
            llmEndpoint: new Uri("http://127.0.0.1:11434/"), gazetteer: [false]);

        Assert.Equal(
            [
                ("deterministic without gazetteer", null),
                ("deterministic without gazetteer + llm fictional-model", "deterministic without gazetteer"),
                ("deterministic without gazetteer + llm fictional-model + repeated names", "deterministic without gazetteer + llm fictional-model"),
                ("deterministic without gazetteer + repeated names", "deterministic without gazetteer"),
            ],
            stacks.Stacks.Select(s => (s.Name, s.Baseline)));
    }
}
