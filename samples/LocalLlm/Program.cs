// De-identifies a note with a local language model (Ollama) plus the deterministic detectors:
// the precision-first stack of docs/guide.md.
//
//   ollama pull gemma4:26b-a4b                      # 18 GB; a GPU with 16 GB runs it at ~0.4 s per note
//   dotnet run --project samples/LocalLlm
//   dotnet run --project samples/LocalLlm -- gemma4:12b
//
// The note goes to the model, so the model must run where the note may go: here, this machine.
using DeidSharp.Detection;
using DeidSharp.Llm;
using DeidSharp.Text;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

var model = args.FirstOrDefault() ?? "gemma4:26b-a4b";
using var ollama = new OllamaApiClient(new Uri("http://localhost:11434/"), model);
if (!await ollama.IsRunningAsync())
{
    Console.Error.WriteLine($"Ollama isn't answering on localhost:11434. Start it, then: ollama pull {model}");
    return 1;
}

var llm = new LlmPhiDetector(ollama, new LlmDetectorOptions
{
    // Forty fictional worked examples: the most precise prompt measured. About 5,000 tokens.
    Prompt = LlmPrompt.WithPoolExamples,

    // Ollama's default window is 4,096 tokens and cuts a longer prompt from the start,
    // instructions included, without an error; and a reasoning model's thinking makes a note
    // take a minute instead of a second.
    ConfigureRequest = o => o.RawRepresentationFactory = _ => new ChatRequest
    {
        Think = false,
        Options = new RequestOptions { NumCtx = 8192 },
    },
});

// The model with the deterministic detectors, and ages under 90 left in, as Safe Harbor allows.
var stack = new SafeHarborAgeFilter(new CompositePhiDetector([llm, .. DeterministicDetectors.Create()]));
var deidentifier = new TextDeidentifier(stack, new TagReplacement());

const string note = "PT TRANSFERRED FROM MERCY GENERAL ON 11/2 S/P FALL AT HOME IN EASTBROOK. 94 YO. HCP IS NIECE DOLORES, PHONE 555-388-1902. SEEN BY DR. OKONKWO.";
TextDeidentificationResult result;
try
{
    result = await deidentifier.DeidentifyAsync(note, TextContext.None);
}
catch (PhiDetectionException)
{
    // A detector failed, so nothing is returned: here, most often a model that isn't pulled.
    Console.Error.WriteLine($"The model didn't answer. Is it pulled? ollama pull {model}");
    return 1;
}

Console.WriteLine(result.Text);
foreach (var finding in result.Findings)
{
    Console.WriteLine($"  {finding.Category} at [{finding.InputStart}, {finding.InputEnd}) by {string.Join("+", finding.Sources)}");
}

return 0;
