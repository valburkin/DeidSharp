using DeidSharp.Detection;
using DeidSharp.Gazetteer;
using DeidSharp.Llm;
using DeidSharp.Presidio;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace DeidSharp.Evaluation.Cli;

// Design pattern: Factory.
// The pattern: one object decides how another is built, so callers ask for what they
// want instead of assembling it.
// Here: the run asks for "the stacks to compare" and gets them named and wired: the
// deterministic detectors (known identifiers, patterns, the gazetteer) alone, then with each Presidio service given, then with all.
// Each service is memoized, so the stack with all of them costs no extra model calls.

/// <summary>The detector stacks one run compares. Owns the HTTP clients of its Presidio detectors.</summary>
internal sealed class DetectorStacks : IDisposable
{
    // A long note on the CPU sidecar can take well over the library's 30-second default, and
    // one timeout stops the whole run (fail closed), possibly an hour in.
    private static readonly TimeSpan NoteTimeout = TimeSpan.FromMinutes(5);

    private readonly List<HttpClient> _clients = [];
    private readonly List<PresidioDetector> _services = [];
    private readonly List<IPhiDetector> _memoized = [];
    private readonly List<(string Model, OllamaApiClient Client)> _ollama = [];

    /// <param name="droppedSources">Recognizers whose spans the services' answers leave out, if any.</param>
    /// <param name="variants">Changes each stack is also measured with (<see cref="StackVariants"/>), if any.</param>
    /// <param name="llmModels">Ollama models each stack is also measured with, as an LLM layer, if any.</param>
    /// <param name="llmEndpoint">The Ollama server, on this machine.</param>
    /// <param name="llmPrompts">The prompts each model is measured with (<see cref="LlmPrompts"/>); default the instructions alone.</param>
    /// <param name="gazetteer">Whether the deterministic layer has the gazetteer, one family of stacks for each; default with it.</param>
    /// <param name="clinicalPlaces">Also measure the clinical sidecar keeping only its locations and organizations.</param>
    /// <param name="ablation">Also measure each service and model without the deterministic layer.</param>
    /// <param name="secondOpinions">Also measure each LLM stack with these second opinions of the same model (<see cref="SecondOpinions"/>), if any.</param>
    public DetectorStacks(
        Uri? presidio,
        Uri? clinical,
        IReadOnlySet<string> droppedSources,
        IReadOnlyList<string>? variants = null,
        IReadOnlyList<string>? llmModels = null,
        Uri? llmEndpoint = null,
        IReadOnlyList<string>? llmPrompts = null,
        IReadOnlyList<bool>? gazetteer = null,
        IReadOnlyList<string>? secondOpinions = null,
        bool ablation = false,
        bool clinicalPlaces = false)
    {
        ArgumentNullException.ThrowIfNull(droppedSources);

        foreach (var (name, endpoint) in new[] { ("presidio", presidio), ("clinical", clinical) })
        {
            if (endpoint is null)
            {
                continue;
            }

            var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            _clients.Add(client);
            var service = new PresidioDetector(client, new PresidioOptions { Endpoint = endpoint, Name = name, Timeout = NoteTimeout, ReportRecognizers = true });
            var memoized = new MemoizingDetector(droppedSources.Count == 0 ? service : new SourceFilteringDetector(service, droppedSources));
            _services.Add(service);
            _memoized.Add(memoized);
        }

        // An experiment: the clinical model's places alone (its HOSP label maps to organizations),
        // the findings it adds next to an LLM (F33). It reads the same memoized answers.
        if (clinicalPlaces && _memoized.FirstOrDefault(s => s.Name == "clinical") is { } clinicalService)
        {
            _memoized.Add(new CategoryFilter(clinicalService, [PhiCategory.Location, PhiCategory.Organization], "clinical places"));
        }

        // The deterministic layer with the gazetteer (the recommended one), and without it if asked.
        foreach (var withGazetteer in gazetteer is { Count: > 0 } ? gazetteer : [true])
        {
            var label = withGazetteer ? "deterministic" : "deterministic without gazetteer";
            IPhiDetector[] Layer() => withGazetteer ? Deterministic() : [.. DeterministicDetectors.Create()];
            Stacks.Add(new NamedStack(label, new CompositePhiDetector(Layer())));
            Stacks.AddRange(_memoized.Select(service => new NamedStack($"{label} + {service.Name}", new CompositePhiDetector([service, .. Layer()]))));
            if (_services.Count > 1)
            {
                var all = _memoized.Take(_services.Count).ToList();
                Stacks.Add(new NamedStack($"{label} + " + string.Join(" + ", all.Select(s => s.Name)), new CompositePhiDetector([.. all, .. Layer()])));
            }
        }

        // Each model is asked about each note once, whichever stacks it joins.
        var prompts = llmPrompts is { Count: > 0 } ? llmPrompts : [LlmPrompts.DefaultId];
        var llms = (llmModels ?? []).SelectMany(model => prompts.Select(prompt => (
            Label: prompt == LlmPrompts.DefaultId ? model : $"{model} ({prompt})",
            Llm: Llm(model, prompt, llmEndpoint ?? new Uri("http://127.0.0.1:11434/"))))).ToList();

        // Safe Harbor keeps ages under 90, so every stack leaves them out, as the recommended stack does.
        Stacks = [.. Stacks.Select(s => s with { Detector = new SafeHarborAgeFilter(s.Detector) })];

        // Each variant and LLM layer right after the stack it changes, so they read side by side.
        Stacks = [.. Stacks.SelectMany(stack => llms
            .SelectMany(l =>
            {
                var withLlm = new NamedStack($"{stack.Name} + llm {l.Label}", new CompositePhiDetector([stack.Detector, l.Llm.Detector]), stack.Name);
                return (secondOpinions ?? []).Select(SecondOpinions.Get).Select(row => new NamedStack(
                        $"{withLlm.Name} + {row.Label}",
                        new LlmSecondOpinion(withLlm.Detector, l.Llm.Client, new LlmSecondOpinionOptions { ConfigureRequest = NoThinking, MayVeto = row.MayVeto }),
                        withLlm.Name))
                    .Prepend(withLlm);
            })
            .Concat((variants ?? []).Select(v => StackVariants.Apply(v, stack)))
            .Prepend(stack))];

        // An ablation: each service, each model, and each service with each model, without the
        // deterministic layer, each paired with the first deterministic layer plus the same layers,
        // so the change shows what the deterministic detectors add. Answers are memoized, so this
        // costs no calls. There is always a deterministic layer, so Stacks[0] exists.
        if (!ablation)
        {
            return;
        }

        var first = Stacks[0].Name;
        var services = _memoized.Select(s => (s.Name, Detector: s)).ToList();
        var models = llms.Select(l => (Name: $"llm {l.Label}", Detector: (IPhiDetector)l.Llm.Detector)).ToList();
        var pairs = services.SelectMany(s => models.Select(m => (Name: $"{s.Name} + {m.Name}", Detector: (IPhiDetector)new CompositePhiDetector([s.Detector, m.Detector]))));
        Stacks.AddRange(services.Concat(models).Concat(pairs).Select(layer =>
            new NamedStack($"{layer.Name}, no deterministic layer", new SafeHarborAgeFilter(layer.Detector), $"{first} + {layer.Name}")));
    }

    public List<NamedStack> Stacks { get; } = [];

    // Reasoning first makes a note take a minute, not a second.
    private static void NoThinking(ChatOptions options) => options.RawRepresentationFactory = _ => new ChatRequest { Think = false };

    // A local model as a detector, with thinking off.
    private (MemoizingDetector Detector, IChatClient Client) Llm(string model, string prompt, Uri endpoint)
    {
        var http = new HttpClient { BaseAddress = endpoint, Timeout = NoteTimeout };
        _clients.Add(http);
        var ollama = new OllamaApiClient(http, model);
        var row = LlmPrompts.Get(prompt);
        _ollama.Add((model, ollama));
        return (new MemoizingDetector(new LlmPhiDetector(ollama, new LlmDetectorOptions
        {
            Name = prompt == LlmPrompts.DefaultId ? $"llm:{model}" : $"llm:{model}:{prompt}",
            Prompt = row.Prompt,
            Retriever = row.Retriever,
            ConfigureRequest = row.ContextWindow is { } window
                ? o => o.RawRepresentationFactory = _ => new ChatRequest { Think = false, Options = new RequestOptions { NumCtx = window } }
                : NoThinking,
        })), ollama);
    }

    // The recommended deterministic layer: known identifiers, patterns and the gazetteer.
    private static IPhiDetector[] Deterministic() => [.. DeterministicDetectors.Create(), new GazetteerDetector()];

    /// <summary>Fails before scoring starts if a Presidio service or an LLM doesn't answer, rather than an hour in.</summary>
    public async Task EnsureReachableAsync(CancellationToken cancellationToken)
    {
        foreach (var (model, ollama) in _ollama)
        {
            if (!await ollama.IsRunningAsync(cancellationToken) || (await ollama.ListLocalModelsAsync(cancellationToken)).All(m => m.Name != model))
            {
                throw new UsageException($"Ollama doesn't answer, or hasn't pulled {model}. Run: ollama pull {model}");
            }
        }

        foreach (var service in _services)
        {
            if (!await service.IsHealthyAsync(cancellationToken))
            {
                throw new UsageException($"The {service.Name} analyzer doesn't answer its /health endpoint. Is the container running?");
            }
        }
    }

    public void Dispose()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }
    }
}
