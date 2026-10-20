# Detectors

A detector finds PHI in text and returns spans. Detectors are stackable: `CompositePhiDetector` runs several in parallel and replaces every span any of them reports. Each one covers a different weakness of the others.

| Detector | Finds | Strength | Weakness | Needs |
|---|---|---|---|---|
| `KnownIdentifierDetector` | The record's own identifiers: names (in full or by any part), dates in any common format, phones and record numbers with any punctuation, addresses, places, organizations | Deterministic; certain (score 1.0); finds unusual names and unlabeled numbers that generic models miss | Only finds what the record lists; identifiers shorter than 4 characters are skipped | Nothing |
| `PatternPhiDetector` | Shaped identifiers: SSNs, phones, emails, URLs, IPs, MACs, payment cards (Luhn), VINs (check digit), labeled MRNs and other ids, dates, ages over 89, street addresses, ZIPs, names after titles and labels | Deterministic; high precision on formatted identifiers | Cannot find names or places in plain prose | Nothing |
| `GazetteerDetector` (`DeidSharp.Gazetteer`) | Names and places by public-domain lists: US Census surnames, first names and places. A name or place that is also a common word or clinical term ("Will", "Foley") counts only with context ("Dr. Will", "daughter Hope", "Mark Brown", "in Hope") | Deterministic; finds names in capitals and names a model misses | Costs precision: clinical words that are also names; US lists only; never lowercase words by default | Nothing |
| `LlmPhiDetector` (`DeidSharp.Llm`) | Whatever the model lists: it is asked for every kind of PHI, and each piece it copies from the text is located there (anything it made up is dropped) | Context: names and places no list or pattern knows | Not deterministic; slow without a GPU (about 0.4 s per note for a 26B model on a 16 GB GPU, once loaded); the text goes to the model, so keep it local | An `IChatClient` (for example OllamaSharp's `OllamaApiClient`) |
| `PresidioDetector` → stock image | Presidio's recognizers plus spaCy NER (persons, places, organizations, dates) | General-purpose NER | Not trained on clinical notes | Docker (`mcr.microsoft.com/presidio-analyzer`) |
| `PresidioDetector` → clinical sidecar | Presidio's pattern recognizers (without spaCy's general NER, URLs and US driver's licenses, which flagged far more clinical text than PHI) and `obi/deid_roberta_i2b2`, a RoBERTa model trained on the i2b2 2014 de-identification corpus (patients, staff, hospitals, places, ages, dates, ids) | Clinical-text NER, runs offline | Large image (about 5 GB); slow on CPU, so a GPU build is available (`sidecar/README.md`, "GPU") | Docker, built from `sidecar/` |

Without a language model, the recommended stack for real clinical text is clinical NER, known identifiers, patterns and the gazetteer; [the guide](guide.md#choosing-a-detector-stack) compares it with the stacks that add a local language model. The gazetteer trades some text for recall: on clinical notes it raised recall by about 3 points and lowered precision by about 13. Leave it out where keeping every word matters more. With an LLM layer (`LlmPhiDetector`), leave the gazetteer out: the model finds the names and places it was added for, and the gazetteer then costs 10 to 20 points of precision for almost no recall.

```csharp
var stack = new CompositePhiDetector([clinical, new GazetteerDetector(), new KnownIdentifierDetector(), new PatternPhiDetector()]);
```

### With a local language model

Four stacks with an LLM layer were measured on clinical notes none of them was tuned on ([how](evaluation/README.md)). Each uses `gemma4:26b-a4b` with `LlmPrompt.WithPoolExamples`, no gazetteer, and `SafeHarborAgeFilter`. Pick by what costs you more, a missed identifier or a removed word:

| Stack | Recall | Precision | Text that isn't PHI removed | Needs |
|---|---:|---:|---:|---|
| Recall first: clinical NER, known identifiers, patterns, LLM | 98.9% | 54.3% | 0.57% | Clinical sidecar and a GPU for the model |
| Balanced: the same, with `LlmSecondOpinion` around it | 98.2% | 70.8% | 0.26% | The same |
| Balanced, one model call: the clinical sidecar's places only (`CategoryFilter`), known identifiers, patterns, LLM | 97.4% | 72.2% | 0.24% | The same |
| Precision first: known identifiers, patterns, LLM | 96.4% | 83.3% | 0.15% | A GPU for the model |

Keep the patterns and the known-identifier detector in every stack, even next to a model: they don't vary between runs or model versions, and they find the record's own identifiers.

The code for each stack, with the Ollama settings that matter (the context window, thinking off, and the same settings for the second opinion), is in [the guide](guide.md#the-recommended-stacks), with what the second opinion may veto. In short: the clinical model labels hospitals as organizations, which is why the one-call stack keeps its locations and organizations.

## Stacking rules

- **Union.** Every span from every detector is kept, then overlapping spans are merged. A merged span keeps the category of its most confident part and lists every detector that found it in `Sources`.
- **Fail closed.** If any detector throws, returns null, or returns a span outside the text, the whole detection fails with `PhiDetectionException`.
- **Unique names.** Each detector in a stack needs its own `Name` (it is the span's source). To stack two Presidio services, register each under its own name (below); that name becomes its `Name` unless you set one.

## Presidio

`PresidioDetector` speaks Presidio's REST API (`POST /analyze`, `GET /health`). It converts Presidio's offsets, which count Python code points, to .NET string indices, which count UTF-16 units: after an emoji the two differ, and using Presidio's numbers directly would redact the wrong characters. It also widens every span to whole words, since a model can tag only part of one.

| Option | Default | Meaning |
|---|---|---|
| `Endpoint` | `http://localhost:5002/` | The analyzer's base address |
| `Name` | `presidio` | The source name on its spans |
| `Language` | `en` | Passed to the analyzer |
| `ScoreThreshold` | `0.3` | Findings below this are dropped by the analyzer. Low on purpose: Presidio scores an unlabeled phone number at 0.4 |
| `Entities` | all | Restrict to these Presidio entity types |
| `IgnoredEntities` | `NRP`, `CRYPTO` | Types to drop. NRP (nationality, religion, politics) is sensitive but not a Safe Harbor identifier |
| `EntityMap` | built in | Overrides for entity type → `PhiCategory`. Unknown types become `OtherIdentifier`, so they are still redacted |
| `Timeout` | 30 s | How long one detection may wait, including retries in the HTTP pipeline; a timeout is a failure |
| `ReportRecognizers` | `false` | Records which recognizer inside the analyzer found each span in its source (`clinical:SpacyRecognizer`). Stock Presidio says so only when asked for its decision process, a little slower; the clinical sidecar always does. Names that aren't plain identifiers are left out |

Register one with dependency injection as a typed HTTP client:

```csharp
services.AddPresidioDetector(o => o.Endpoint = new Uri("http://clinical:3000/"))
        .AddStandardResilienceHandler();   // optional, Microsoft.Extensions.Http.Resilience; raise its 10 s attempt timeout for a CPU sidecar

var presidio = serviceProvider.GetRequiredService<PresidioDetector>();
```

To stack several Presidio services, register each under a name. Each gets its own options and HTTP client, and is resolved as a keyed service:

```csharp
services.AddPresidioDetector("presidio", o => o.Endpoint = new Uri("http://presidio:3000/"));
services.AddPresidioDetector("clinical", o => o.Endpoint = new Uri("http://clinical:3000/"))
        .AddStandardResilienceHandler();

var stack = new CompositePhiDetector([
    serviceProvider.GetRequiredKeyedService<PresidioDetector>("presidio"),
    serviceProvider.GetRequiredKeyedService<PresidioDetector>("clinical"),
    new GazetteerDetector(),
    .. DeterministicDetectors.Create(),
]);
```

A stack held for the application's lifetime holds its detectors' HTTP clients too, and the factory never recycles a client's handler while it is held. Let the connections recycle themselves instead, so DNS changes are picked up:

```csharp
services.AddPresidioDetector("clinical", o => o.Endpoint = new Uri("http://clinical:3000/"))
        .UseSocketsHttpHandler((handler, _) => handler.PooledConnectionLifetime = TimeSpan.FromMinutes(5))
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
```

Presidio also has extras for cloud services (for example Azure Health Data Services de-identification). Those send text to a cloud provider. DeidSharp does not use them by default; if you add one, it is your decision and your agreement with that provider.

## Writing a detector

Implement `IPhiDetector` (or `IDeterministicPhiDetector` if it runs in-process and gives the same answer every time; only those can join the leak guard):

```csharp
public sealed class MyLlmDetector(ILlmClient llm) : IPhiDetector
{
    public string Name => "llm";

    public async Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken ct = default)
    {
        var found = await llm.FindPhiAsync(request.Text, ct);   // throw on any failure, never return []
        return [.. found.Select(f => new PhiSpan(f.Start, f.End, f.Category, f.Confidence, Name))];
    }
}
```

Rules for a detector:

- Return spans as UTF-16 indices into `request.Text`, with `End` exclusive.
- Throw when you cannot run. An empty list means "no PHI found".
- Never put the text, or parts of it, into exception messages or logs.
- Be safe to call concurrently: one detector serves the notes of a bundle, and the requests of a service, at the same time.
- `request.Known` holds the record's identifiers if you can use them.

## Measuring a detector

```csharp
var report = await DetectionEvaluator.EvaluateAsync(stack, annotatedCorpus);
Console.WriteLine($"recall {report.Overall.Recall:P1}, character recall {report.CharacterRecall:P1}, precision {report.Overall.Precision:P1}");
foreach (var (category, recall) in report.PerCategory)
{
    Console.WriteLine($"{category}: {recall.Recall:P1} found, {recall.LabeledRecall:P1} with the right category");
}
```

A gold span counts as found if any prediction overlaps it. Character recall shows how much of the PHI text was covered, since a half-covered name still leaks. See [testing](testing.md) for building an annotated sample.
