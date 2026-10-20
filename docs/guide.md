# DeidSharp guide

How to use DeidSharp in a .NET application: which packages to install, which detector stack to choose, and how to de-identify free text and FHIR, pseudonymize, check output, handle errors, run it in a service, and measure it on your own data. The library examples run as tests ([`GuideExamplesTests`](../tests/DeidSharp.Tests/GuideExamplesTests.cs)), with stand-ins for the sidecar and the model. The snippets for a host (ASP.NET Core, OpenTelemetry) are sketches to adapt; the three [samples](../samples) are complete programs, built by CI.

> **Research-grade software.** DeidSharp removes what its rules and detectors find, and free-text detection is never perfect. It has not been certified, audited or validated by an expert determination. Before real patient data, read the [threat model](threat-model.md), measure recall on a sample of your own data ([Measuring recall on your own data](#measuring-recall-on-your-own-data)), and work under your organization's governance.

## Contents

1. [Concepts in five minutes](#concepts-in-five-minutes)
2. [Installing](#installing)
3. [Choosing a detector stack](#choosing-a-detector-stack)
4. [De-identifying free text](#de-identifying-free-text)
5. [Known identifiers](#known-identifiers)
6. [Tags or surrogates](#tags-or-surrogates)
7. [De-identifying FHIR](#de-identifying-fhir)
8. [Detectors](#detectors)
9. [The clinical sidecar and Presidio](#the-clinical-sidecar-and-presidio)
10. [A local language model](#a-local-language-model)
11. [Pseudonyms and keys](#pseudonyms-and-keys)
12. [Structured fields outside FHIR](#structured-fields-outside-fhir)
13. [Checking output](#checking-output)
14. [Errors and failing closed](#errors-and-failing-closed)
15. [Using DeidSharp in a service](#using-deidsharp-in-a-service)
16. [Telemetry](#telemetry)
17. [Performance and sizing](#performance-and-sizing)
18. [Measuring recall on your own data](#measuring-recall-on-your-own-data)
19. [Re-identification risk](#re-identification-risk)
20. [Before production](#before-production)
21. [Questions](#questions)
22. [Reference](#reference)

## Concepts in five minutes

- **PHI** is protected health information: the 18 kinds of identifier HIPAA's Safe Harbor method lists (names, places smaller than a state, dates finer than a year, phone numbers, record numbers…). `PhiCategory` has values for each kind (places, for one, are split into street addresses, locations and ZIP codes), plus `Organization`. [safe-harbor.md](safe-harbor.md) shows how each one is handled.
- **A detector** (`IPhiDetector`) finds PHI in text and returns **spans**: start, end, category, score and which detector found it. A span never carries the text it covers, so spans can be logged.
- **A stack** (`CompositePhiDetector`) runs several detectors on the same text. Every span any of them reports is replaced: recall comes first, because a missed identifier is leaked PHI, while a false positive only costs a word.
- **Known identifiers** are what the structured record already says about the patient (name, birth date, MRN). Notes repeat them, so looking for them directly is the most reliable detector there is.
- **A replacement strategy** turns each span into a tag (`[NAME]`) or a realistic surrogate (a fake name).
- **The output check.** After replacing, `TextDeidentifier` looks for the subject's known identifiers in the output and throws if any survived; `FhirDeidentifier` also checks the output's structure. `LeakScanner` checks text produced elsewhere with the deterministic detectors. All three fail closed: when in doubt, nothing is returned.
- **Two modes.** Safe Harbor removes identifiers and cuts dates to the year. Pseudonymization replaces identifiers with keyed pseudonyms and shifts dates per patient, so data can be linked over time; it is not Safe Harbor and belongs under a data use agreement or an expert determination.

## Installing

DeidSharp targets .NET 10. Install the packages your scenario needs:

| Package | Install | For | Depends on |
|---|---|---|---|
| `DeidSharp` | `dotnet add package DeidSharp` | Everything below starts here: free text, detectors, pseudonyms, the leak guard, risk, evaluation | Nothing |
| `DeidSharp.Fhir` | `dotnet add package DeidSharp.Fhir` | FHIR R4 resources and bundles | `Hl7.Fhir.R4` (Firely) |
| `DeidSharp.Presidio` | `dotnet add package DeidSharp.Presidio` | NER through the clinical sidecar or stock Presidio | `Microsoft.Extensions.Http`, `Microsoft.Extensions.Options` |
| `DeidSharp.Gazetteer` | `dotnet add package DeidSharp.Gazetteer` | Names and places from US Census lists, with no model | Nothing (2.6 MB of embedded lists) |
| `DeidSharp.Llm` | `dotnet add package DeidSharp.Llm` | A local language model as a detector | `Microsoft.Extensions.AI.Abstractions` |

For the language model you also need a client for your model server; the examples use OllamaSharp (`dotnet add package OllamaSharp`).

## Choosing a detector stack

What to expect, measured on real clinical notes (ICU nursing notes) that none of the stacks was tuned on. **Recall** is the share of identifiers removed: what matters for privacy. **Precision** is the share of removed spans that really were identifiers. **Other text removed** is the clinical text lost to false positives. ([How these were measured](evaluation/README.md).)

| Stack | Packages | Needs | Recall | Precision | Other text removed |
|---|---|---|---:|---:|---:|
| Deterministic: known identifiers + patterns | `DeidSharp` | Nothing | 24.4% | 80.5% | 0.06% |
| Deterministic + gazetteer | + `Gazetteer` | Nothing | 42.1% | 43.8% | 0.41% |
| No language model: deterministic + gazetteer + clinical sidecar | + `Gazetteer`, `Presidio` | Docker | 88.3% | 43.0% | 0.85% |
| **Precision first**: deterministic + language model | + `Llm` | A GPU (16 GB) | 96.4% | 83.3% | 0.15% |
| **Balanced, one call**: deterministic + the sidecar's places + language model | + `Presidio`, `Llm` | Docker and a GPU | 97.4% | 72.2% | 0.24% |
| **Balanced**: recall first + the model's second opinion | + `Presidio`, `Llm` | Docker and a GPU | 98.2% | 70.8% | 0.26% |
| **Recall first**: deterministic + clinical sidecar + language model | + `Presidio`, `Llm` | Docker and a GPU | 98.9% | 54.3% | 0.57% |

The measurements gave each stack the note's text only, with no structured record, so the known-identifier detector had nothing to look for. With the record's identifiers (or FHIR, which extracts them), expect more than these numbers, most of all from the deterministic stacks.

How to choose:

- **Only structured data, or text from your own forms?** The deterministic stack, with the record's known identifiers. Text written by people needs more.
- **Clinical notes, no GPU?** The sidecar stack (88.3%). Expect to review output by hand.
- **Clinical notes, a GPU?** One of the language-model stacks. Precision first keeps the most text; recall first leaves the fewest identifiers; the two balanced stacks sit between.
- **Whatever you choose:** these numbers come from one kind of note. Measure the stack on an annotated sample of your own data before you rely on it ([Measuring recall on your own data](#measuring-recall-on-your-own-data)).

## De-identifying free text

### The smallest example

```csharp
using DeidSharp;
using DeidSharp.Detection;
using DeidSharp.Text;

var deidentifier = new TextDeidentifier(
    new CompositePhiDetector(DeterministicDetectors.Create()),
    new TagReplacement());

var known = KnownIdentifiers.Empty
    .AddName("Rosa Alvarez")
    .AddDate(new DateOnly(1961, 3, 4))
    .AddIdentifier("MRN-48213", PhiCategory.MedicalRecordNumber);

var result = await deidentifier.DeidentifyAsync(
    "Rosa Alvarez (MRN-48213) was seen on 03/04/2024. Call 617-555-0142.",
    new TextContext(subjectId: "patient-1", known));

// result.Text == "[NAME] ([MRN]) was seen on [DATE]. Call [PHONE]."
```

`TextDeidentifier` runs the detector, merges the spans, replaces each one, and checks that none of the subject's known identifiers is left in the output. Build it once and reuse it: it holds no state between calls and is safe to call from many threads, as long as its detectors are (every built-in detector is).

### What the result holds

`result.Text` is the de-identified text. `result.Findings` lists every replacement: where it was in the input, where its replacement is in the output, its category, score and the detectors that found it. Findings hold positions and categories, never the values, so they can be logged:

```csharp
var result = await deidentifier.DeidentifyAsync("Seen by Dr. Hart today.", TextContext.None);
// result.Text == "Seen by Dr. [NAME] today."

foreach (var finding in result.Findings)
{
    logger.LogInformation("{Category} at [{Start}, {End}) by {Sources}",
        finding.Category, finding.InputStart, finding.InputEnd, string.Join("+", finding.Sources));
}
// Name at [12, 16) by pattern:name-after-title
```

Never log `result.Text` next to the input, or the input itself.

### The context: who the text is about

`TextContext` carries two things:

- **`SubjectId`**: a stable id for the patient (your internal id, a pseudonym, anything). It keys the per-patient choices: the same date shift and the same surrogates for all of one patient's notes. Use the same id for every note of a patient.
- **`Known`**: the patient's identifiers from the structured record ([Known identifiers](#known-identifiers)).

`TextContext.None` means "unknown subject, no known identifiers": allowed, but you lose the most reliable detector.

### Options

```csharp
var deidentifier = new TextDeidentifier(stack, new TagReplacement(), new TextDeidentifierOptions
{
    MinimumScore = 0,      // default: every span any detector reports is replaced
    VerifyOutput = true,   // default: throw if a known identifier survived
});
```

Raise `MinimumScore` only with evaluation results that justify the recall you lose. Turn `VerifyOutput` off only if you check the output yourself.

### Long texts

There is no length limit. The deterministic detectors scan any length; the language-model detector splits long text into overlapping pieces (2,000 characters by default, `ChunkLength`); the Presidio detector sends the whole text in one request, so very long documents depend on the analyzer's own limits and `Timeout`.

### Non-English text

The pattern library and the gazetteer are written for US English. Names in other languages are found by the known-identifier detector (accent-insensitive) and, to the extent the model knows them, by NER and the language model. Other languages haven't been tested.

## Known identifiers

### Why they matter

A note about Rosa Alvarez usually says "Rosa", "Ms. Alvarez" or "ALVAREZ". A model can miss an unusual surname; looking for the record's own values cannot. The known-identifier detector finds them case- and accent-insensitively, as whole words, in every common written form: a name in full or by any part, a date in any common format ("03/04/1961", "March 4, 1961", "4 Mar 1961"), a phone or record number with any punctuation. Every hit scores 1.0.

### Building them from your record

```csharp
var known = KnownIdentifiers.Empty
    .AddName("Rosa M. Alvarez")
    .AddName("Luis Alvarez")                       // a relative
    .AddDate(new DateOnly(1961, 3, 4))
    .AddPhone("(617) 555-0142")
    .AddEmail("rosa.alvarez@example.org")
    .AddStreetAddress("12 Oak Street Apt 4")
    .AddLocation("Fairhaven")
    .AddOrganization("Harborview Clinic")
    .AddIdentifier("MRN-48213", PhiCategory.MedicalRecordNumber)
    .AddIdentifier("ZQ-4471029", PhiCategory.HealthPlanNumber);
```

`KnownIdentifiers` is immutable: each `Add` returns a new set; `Merge` joins two. Blank values are ignored and duplicates kept once. Add everyone the record names: the patient, relatives, emergency contacts, the care team, the employer. For FHIR you don't do this yourself: `FhirDeidentifier` extracts them from the resource.

### What it skips

Identifiers with fewer than 4 letters and digits are not searched for (a 3-digit MRN would match every "123"), and name parts shorter than 2 characters are not searched for on their own. Tune both in `KnownIdentifierOptions` (`MinimumIdentifierLength`, `MinimumNameTokenLength`); short identifiers must then be caught by another layer.

## Tags or surrogates

### Tags

`TagReplacement` writes the category: `[NAME]`, `[DATE]`, `[MRN]`; the full list is in the [Reference](#phi-categories). Nothing in the output can be mistaken for real data. Change the format with `new TagReplacement("<{0}>")`.

### Realistic surrogates

`SurrogateReplacement` writes a fake of the same kind: a name for a name, an MRN-shaped number for an MRN. If a detector misses an identifier, the real one then hides among fakes ("hiding in plain sight") instead of standing out between tags.

```csharp
using DeidSharp.Pseudonymization;
using DeidSharp.Structured;

var pseudonymizer = new HmacPseudonymizer(PseudonymKey.FromBase64(secretFromYourVault));
var deidentifier = new TextDeidentifier(
    new CompositePhiDetector(DeterministicDetectors.Create()),
    new SurrogateReplacement(pseudonymizer, YearOnlyDateTransformer.Instance));
// "Rosa Alvarez, born 03/04/1961, …" → "Jessica Garrison, born 1961, …"
```

Surrogates are deterministic: the same value of the same patient under the same key always becomes the same fake, so "Mr. Lee … Lee's daughter" stays consistent. A surrogate never equals one of the patient's own known identifiers, and fakes use reserved ranges where they exist (555-01xx phones, example.org, 192.0.2.x, SSN area 000).

### Dates in surrogates

The second argument decides what a date becomes:

- `YearOnlyDateTransformer.Instance`: the year only, as Safe Harbor requires. Safe Harbor also removes the year of a date that reveals an age over 89, such as a birth year 90 or more years ago; in free text DeidSharp can't tell whose date a date is, so it keeps the year. Where that matters, drop the date yourself or use tags ([known gaps](safe-harbor.md#known-gaps)).
- `new ShiftingDateTransformer(new DateShifter(pseudonymizer, maxDays: 365))`: every date of a patient moves by the same secret number of days, so intervals survive (length of stay, days since diagnosis). This is pseudonymization, not Safe Harbor.

```csharp
var shifter = new DateShifter(pseudonymizer, maxDays: 365);
var deidentifier = new TextDeidentifier(stack, new SurrogateReplacement(pseudonymizer, new ShiftingDateTransformer(shifter)));
// "Admitted 2024-03-05, discharged 2024-03-09." → two shifted dates, still four days apart
```

## De-identifying FHIR

### Safe Harbor

```csharp
using DeidSharp.Fhir;

var deidentifier = new FhirDeidentifier(new FhirDeidentificationOptions());

FhirDeidentificationResult result = await deidentifier.DeidentifyAsync(bundle);   // a Firely Resource
string json = await deidentifier.DeidentifyJsonAsync(bundleJson);                 // or FHIR JSON in, FHIR JSON out
```

The input is never modified; the result is a de-identified copy. Names, contact points, identifiers, signatures and narrative are removed; dates are cut to the year; addresses keep state, country and the three-digit ZIP; anyone 90 or older loses their birth date; unknown extensions are removed; resource ids become pseudonyms and every reference is rewritten to match; free text (notes, comments, text attachments) goes through the free-text stack. [safe-harbor.md](safe-harbor.md) lists every element.

The [FhirBundle sample](../samples/FhirBundle) runs this on a fictional bundle.

### What the result holds

- `result.Resource`: the de-identified resource or bundle.
- `result.Statistics`: counts for logs and audit (elements removed and replaced, ids pseudonymized, free-text values and findings, modifier extensions removed). Review a non-zero `ModifierExtensionsRemoved`: a removed modifier extension can change what a resource means.
- `result.Verification`: the output check, always clean unless you turned verification off.

### Pseudonymized, linkable across exports

```csharp
var options = new FhirDeidentificationOptions
{
    Mode = FhirDeidentificationMode.Pseudonymized,
    Key = PseudonymKey.FromBase64(secretFromYourVault),
    FreeTextStyle = FreeTextStyle.Surrogates,
    MaxDateShiftDays = 365,
};
```

With the same key, the same patient gets the same pseudonyms and the same date shift in every export, so exports can be joined. Identifiers keep their system and type with a pseudonymized value; dates keep their precision and time of day, shifted. This is not Safe Harbor ([safe-harbor.md](safe-harbor.md#pseudonymized-mode-is-not-safe-harbor)). In Safe Harbor mode, leave `Key` null: each run then uses a fresh key, and its pseudonyms link nothing outside the run.

### Free text inside FHIR

By default free text goes through the deterministic stack plus the resource's own known identifiers. For clinical notes, give it a real stack:

```csharp
var options = new FhirDeidentificationOptions
{
    FreeTextDetector = stack,                 // any IPhiDetector, e.g. one of the stacks above
    FreeTextStyle = FreeTextStyle.Tags,       // or Surrogates
    MaxFreeTextConcurrency = 8,               // texts of one bundle sent to the detector at once
};
```

The texts of a bundle are collected while the resource is walked and de-identified together at the end, up to `MaxFreeTextConcurrency` at a time, so a slow detector service isn't called one note after another.

### Extensions

Every extension whose URL isn't allowed is removed: an unknown extension can hold anything (a birthplace, a mother's maiden name). The default allows US Core race, ethnicity and birth sex. To keep more:

```csharp
var options = new FhirDeidentificationOptions
{
    AllowedExtensions = new HashSet<string>(FhirDeidentificationOptions.DefaultAllowedExtensions)
    {
        "http://example.org/fhir/StructureDefinition/trial-arm",
    },
};
```

Values inside a kept extension still go through the other rules.

### Ages and the reference date

Ages are computed at `ReferenceDate`, or today (UTC, from `TimeProvider`) when it is null. Set it for reproducible output, and set `TopCodeAge` (default 90) only if your rules are stricter than Safe Harbor.

### Your own rules

A rule decides what happens to one element. Rules in `AdditionalRules` run before the built-in ones; the first rule that doesn't `Pass` wins.

```csharp
using DeidSharp.Fhir.Rules;
using Hl7.Fhir.Model;

sealed class DropMaritalStatus : IFhirElementRule
{
    public ValueTask<ElementAction> ApplyAsync(FhirElementContext context) =>
        ValueTask.FromResult(context is { Parent: Patient, ElementName: "maritalStatus" }
            ? ElementAction.Remove
            : ElementAction.Pass);
}

var deidentifier = new FhirDeidentifier(new FhirDeidentificationOptions { AdditionalRules = [new DropMaritalStatus()] });
```

An `ElementAction` can `Pass` (ask the next rule), `Keep` (keep it, don't look inside), `Descend` (keep it, apply the rules to its children), `Remove`, or `ReplaceWith(replacement)`. `context` gives the element, its parent, its name, its resource, a readable path and the run's session (mode, pseudonymizer, known identifiers). A rule never sees free text de-identified yet: that happens after the walk. The output check runs after your rules too: it refuses a name, contact point or signature left in place (and, in Safe Harbor, an identifier), and any of the record's known identifiers in text. It can't catch PHI the record doesn't list inside text your rule kept with `Keep`, so keep only what you know is safe.

### Invalid input

`DeidentifyJsonAsync` throws `FhirInputException` when the JSON isn't valid FHIR R4. Its message gives only how many problems the parser found: the parser's own messages quote the input, which may be PHI.

### What isn't handled

`Coding.display` and `Quantity.unit` are kept as vocabulary and not scanned; images and other binary attachments are removed, never inspected. The full list is in [safe-harbor.md](safe-harbor.md#known-gaps).

## Detectors

### The built-in detectors

| Detector | Package | Finds | Deterministic |
|---|---|---|---|
| `KnownIdentifierDetector` | `DeidSharp` | The record's own identifiers, in any common form | Yes |
| `PatternPhiDetector` | `DeidSharp` | Shaped identifiers: SSNs, phones, emails, URLs, IPs, MACs, payment cards (Luhn), VINs, labeled MRNs and ids, dates (numeric ones with a year), ages over 89, street addresses, ZIP codes next to a state, names after a title or label | Yes |
| `GazetteerDetector` | `DeidSharp.Gazetteer` | Names and places from US Census lists, with context rules for names that are common words | Yes |
| `PresidioDetector` | `DeidSharp.Presidio` | Whatever the analyzer finds: the clinical transformer model, or stock Presidio's spaCy NER | No (a service) |
| `LlmPhiDetector` | `DeidSharp.Llm` | Whatever the model lists, located in the text | No (a model) |

`DeterministicDetectors.Create()` gives the first two with their defaults. [providers.md](providers.md) compares them in more detail.

### Building a stack

```csharp
var stack = new CompositePhiDetector([clinical, new GazetteerDetector(), .. DeterministicDetectors.Create()]);
```

All detectors run on the same text in parallel. Overlapping spans merge into one, which keeps the category of its most confident part and lists every detector in `Sources`. If any detector fails, the whole detection fails (`PhiDetectionException`): a stack with a missing layer is not the stack you configured. Each detector needs its own `Name`.

### Wrappers

- `SafeHarborAgeFilter(stack)` leaves out ages under 90, which Safe Harbor keeps but NER models and language models report. Wrap any stack that has one of them.
- `CategoryFilter(detector, [PhiCategory.Location, PhiCategory.Organization])` keeps only some categories of one detector's spans. It lowers recall wherever another detector doesn't cover what it drops: measure before using it.
- `LlmSecondOpinion(stack, chat)` shows a language model the spans only the noisier detectors found and drops those it calls not PHI ([A local language model](#a-local-language-model)).

### The gazetteer

```csharp
using DeidSharp.Gazetteer;

var stack = new CompositePhiDetector([new GazetteerDetector(), .. DeterministicDetectors.Create()]);
// "Pt's daughter Hope visited from Fairhaven." → "Pt's daughter [NAME] visited from [LOCATION]."
```

It looks capitalized words up in Census lists; a name that is also a common word or a clinical term ("Will", "Foley") counts only with context ("Dr. Will", "daughter Hope", "in Hope"). `GazetteerOptions` tunes it: `MinimumSurnameCount` (default 10,000 people) decides which surnames count on their own, `Capitals` (default on) looks up words in capitals too, `Lowercase` (default off) lowercase words as well. Use it when there is no language model; next to one, it adds almost no recall and removes more text.

### Your own patterns

```csharp
using System.Text.RegularExpressions;
using DeidSharp.Detection.Known;
using DeidSharp.Detection.Patterns;

var localMrn = new PatternRule("local-mrn", PhiCategory.MedicalRecordNumber, LocalMrn(), Score: 0.9);
var patterns = new PatternPhiDetector([.. PatternLibrary.Default, localMrn]);
var stack = new CompositePhiDetector([new KnownIdentifierDetector(), patterns]);

// In a partial class: the regex is compiled at build time.
[GeneratedRegex(@"\bH\d{7}\b", RegexOptions.CultureInvariant)]
private static partial Regex LocalMrn();
```

A rule can name a capture group that holds the PHI (`ValueGroup`) and a `Validator` that discards a match (a check digit, say). Keep `PatternLibrary.Default` in the list unless you mean to replace it.

### Writing your own detector

Implement `IPhiDetector`, or `IDeterministicPhiDetector` if it runs in-process and always gives the same answer (only those can join the leak guard):

```csharp
sealed partial class BadgeNumberDetector : IDeterministicPhiDetector
{
    public string Name => "badge";

    public IReadOnlyList<PhiSpan> Detect(DetectionRequest request, CancellationToken cancellationToken = default) =>
        [.. Badge().Matches(request.Text).Select(m => new PhiSpan(m.Index, m.Index + m.Length, PhiCategory.OtherIdentifier, 1.0, Name))];

    public Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Detect(request, cancellationToken));

    [GeneratedRegex(@"\bBADGE-\d{5}\b", RegexOptions.CultureInvariant)]
    private static partial Regex Badge();
}
```

The rules every detector follows:

- Spans are UTF-16 indices into `request.Text`, end exclusive.
- **Throw when you can't run** (a service is down, an answer is malformed). An empty list means "no PHI found"; returning one after a failure would leak.
- Never put the text, or any part of it, in an exception message or a log.
- Be safe to call concurrently: one detector serves many texts at once.
- `request.Known` holds the record's identifiers, if you can use them.

## The clinical sidecar and Presidio

### Starting the sidecar

The clinical sidecar is Presidio with `obi/deid_roberta_i2b2`, a transformer trained on clinical notes, baked into the image and run offline. From a clone of this repository:

```bash
docker compose up -d --build clinical                       # CPU, on 127.0.0.1:5003
docker compose --profile gpu up -d --build clinical-gpu     # NVIDIA GPU, on 127.0.0.1:5005
docker compose up -d presidio                               # stock Presidio, on 127.0.0.1:5002
```

The image is about 5 GB, and the first build downloads the model. Every service listens on 127.0.0.1 only: it receives raw patient text. [sidecar/README.md](../sidecar/README.md) covers configuration, GPU builds and running it elsewhere.

### Using it

```csharp
using DeidSharp.Presidio;

using var http = new HttpClient();
var clinical = new PresidioDetector(http, new PresidioOptions
{
    Endpoint = new Uri("http://localhost:5003/"),
    Name = "clinical",
});

if (!await clinical.IsHealthyAsync()) { /* refuse to start: a missing layer lowers recall */ }
```

With dependency injection, register it as a typed HTTP client ([Using DeidSharp in a service](#using-deidsharp-in-a-service)).

### Presidio options

`ScoreThreshold` (default 0.3, low on purpose: Presidio scores an unlabeled phone number at 0.4), `Entities` (ask for only these Presidio entity types), `IgnoredEntities` (default NRP and CRYPTO, which aren't Safe Harbor identifiers), `EntityMap` (Presidio entity type to `PhiCategory`; unknown types become `OtherIdentifier`, so they are still redacted), `Timeout` (default 30 seconds, including retries; must be positive), `ReportRecognizers` (records which recognizer inside the analyzer found each span, for diagnosis). The defaults are in the [Reference](#presidiooptions).

The sidecar on a CPU takes up to half a second per note, more for long documents, so leave `Timeout` generous. If you add `Microsoft.Extensions.Http.Resilience`'s standard handler, raise its per-attempt timeout (10 seconds by default) as well, or long notes fail.

### Stock Presidio or the clinical sidecar?

The clinical sidecar, for clinical text. Stock Presidio's spaCy NER wasn't trained on clinical notes and flags so much ordinary text (about 6% precision on clinical notes) that it is useful mainly as a recall backstop. Two Presidio detectors in one stack need different names.

## A local language model

### What it does

`LlmPhiDetector` asks a language model to list the PHI in a text, in JSON, then finds each listed piece in the text as whole words. Anything the model made up or reworded isn't in the text and is dropped. Long texts go in overlapping pieces. An answer that isn't the JSON asked for fails closed. It runs at temperature 0, but a model can still answer differently on another version, so it is never part of the leak guard.

The note is sent to the model as it is. **Run the model where the note may go**: on the same machine or a private network. DeidSharp never supplies a cloud client; using one is your decision and your agreement with that provider.

### Setting up Ollama

1. Install [Ollama](https://ollama.com) on the machine that will run the model (tested with 0.40.6).
2. `ollama pull gemma4:26b-a4b`, the recommended model (18 GB, 4-bit). It runs on a 16 GB GPU at about 0.4 s per note, with part of it on the CPU. `gemma4:12b` (7.7 GB) fits smaller GPUs and finds somewhat less.
3. Keep Ollama reachable only from the program that uses it: it has no authentication.

### Using it

```csharp
using DeidSharp.Llm;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

using var ollama = new OllamaApiClient(new Uri("http://localhost:11434/"), "gemma4:26b-a4b");

// Thinking off (a note takes a second, not a minute) and a window that holds the prompt.
Action<ChatOptions> ollamaSettings = o => o.RawRepresentationFactory = _ => new ChatRequest
{
    Think = false,
    Options = new RequestOptions { NumCtx = 8192 },
};

var llm = new LlmPhiDetector(ollama, new LlmDetectorOptions
{
    Prompt = LlmPrompt.WithPoolExamples,
    ConfigureRequest = ollamaSettings,
});

var precisionFirst = new SafeHarborAgeFilter(new CompositePhiDetector([llm, .. DeterministicDetectors.Create()]));
```

Two settings matter more than any other:

- **The context window.** Ollama runs a model with a 4,096-token window unless told otherwise, and **cuts a longer prompt from the start, instructions included, without an error**. `WithPoolExamples` is about 5,000 tokens, so set `NumCtx` to at least 8,192.
- **Thinking off.** A reasoning model thinks before it answers by default: a note takes a minute and may come back empty.

The [LocalLlm sample](../samples/LocalLlm) runs this on a fictional note.

### Other model servers

`LlmPhiDetector` takes any `Microsoft.Extensions.AI` `IChatClient` whose server honors a JSON-schema response format (vLLM and LM Studio, through an OpenAI-compatible client, do). Only Ollama has been tested; measure your server and model before relying on them.

### The recommended stacks

```csharp
var llm = new LlmPhiDetector(ollama, new LlmDetectorOptions { Prompt = LlmPrompt.WithPoolExamples, ConfigureRequest = ollamaSettings });
var known = new KnownIdentifierDetector();
var patterns = new PatternPhiDetector();

var recallFirst = new SafeHarborAgeFilter(new CompositePhiDetector([clinical, llm, known, patterns]));
var balanced = new SafeHarborAgeFilter(new LlmSecondOpinion(
    new CompositePhiDetector([clinical, llm, known, patterns]), ollama, new LlmSecondOpinionOptions { ConfigureRequest = ollamaSettings }));
var places = new CategoryFilter(clinical, [PhiCategory.Location, PhiCategory.Organization]);
var balancedOneCall = new SafeHarborAgeFilter(new CompositePhiDetector([places, llm, known, patterns]));
var precisionFirst = new SafeHarborAgeFilter(new CompositePhiDetector([llm, known, patterns]));
```

`clinical` is the sidecar's `PresidioDetector` ([The clinical sidecar and Presidio](#the-clinical-sidecar-and-presidio)). The second opinion needs the same Ollama settings as the detector. What to expect from each stack is in [Choosing a detector stack](#choosing-a-detector-stack). Leave the gazetteer out next to a model.

### Prompts

| Preset | What it is | When |
|---|---|---|
| `LlmPrompt.WithPoolExamples` | Instructions plus forty fictional annotated notes, twelve with no PHI (~5,000 tokens) | The most precise, and the one to use (set it: the options' default is `Default`). Needs `NumCtx` ≥ 8,192 |
| `LlmPrompt.WithNeutralExamples` | Instructions plus four region-neutral fictional notes | A smaller window |
| `LlmPrompt.WithExamples` | An earlier version of the same four notes | Prefer `WithNeutralExamples` |
| `LlmPrompt.Focused` | A closed list of what is and isn't PHI, five examples | Fewer false positives on small models, at a cost in recall |
| `LlmPrompt.Default` | Instructions only | Not recommended: the examples add recall |

A custom prompt is `new LlmPrompt(instructions, examples)`. Examples are sent with every text, so they must be fictional.

### What the model's answer is filtered for

`SafeHarborOnly` (default on) drops what Safe Harbor doesn't ask to remove, however the model typed it: ages under 90; dates that are only a year, only a time, or words without a number or month ("today", "Monday"); identifiers of three digits or fewer (a bed, a room, a setting).

### The second opinion

`LlmSecondOpinion` shows the model each span that only the noisier detectors found and drops the ones it calls not PHI. It raises precision from about 54% to 71% for a small loss of recall (98.9% to 98.2%), at the cost of a second model call per note (about 0.1 s).

What it may veto (`MayVeto`) is decided by each span's source names: by default not a span from the known-identifier detector (`known`), a pattern (`pattern…`) or the language model (`llm…`), and never a location or an organization, which the model wrongly rejected as hospitals. So:

- **Keep the language-model detector's name starting with `llm`** (the default is `llm`). Under another name its findings become vetoable, and a model asked to judge its own answers rejects much of the real PHI.
- **Your own reliable detectors are vetoable** unless you widen `MayVeto` to exclude them.

### Retrieved examples

`LlmDetectorOptions.Retriever` takes an `IExampleRetriever` that picks extra examples for each text, for a pool of your own. DeidSharp ships none: `WithPoolExamples`, which shows every example, works better than picking a few.

## Pseudonyms and keys

### The key

A `PseudonymKey` is the secret behind every pseudonym, surrogate and date shift. Whoever holds it can recompute pseudonyms and so link records; without it they can't be reversed.

- **Create one** outside your program and store it in a secret store: `openssl rand -base64 32` (at least 32 bytes).
- **Load it** with `PseudonymKey.FromBase64(value)` from configuration or the secret store, never from a request.
- **`PseudonymKey.Generate()`** makes a fresh random key for one run; its pseudonyms link nothing outside the run. The key's bytes are never exposed, so a generated key can't be saved.
- **Rotating** a key breaks linkage: records made with the old key and the new one no longer match. Rotate only when that's what you want.

### Pseudonyms and date shifts directly

```csharp
var pseudonymizer = new HmacPseudonymizer(PseudonymKey.FromBase64(secretFromYourVault));

Guid patientId = PseudonymFormats.Uuid(pseudonymizer.Digest("Patient.id", "48213"));
string shortId = PseudonymFormats.Hex(pseudonymizer.Digest("MRN", "48213"), length: 16);

var shifter = new DateShifter(pseudonymizer, maxDays: 365);
DateOnly shifted = shifter.Shift(new DateOnly(2024, 3, 5), "patient-1");
int offset = shifter.OffsetDays("patient-1");      // never 0; the same for every date of the patient
```

The digest is HMAC-SHA256. The domain ("Patient.id", "MRN") keeps pseudonyms of the same digits in different roles unrelated. `DateShifter.Shift` also takes a `DateTimeOffset` and keeps its time of day.

## Structured fields outside FHIR

For your own tables and columns:

```csharp
using DeidSharp.Dates;
using DeidSharp.Structured;

AgeGeneralizer.SafeHarbor.Generalize(93).Label;          // "90+"
AgeGeneralizer.SafeHarbor.Generalize(64).Label;          // "60s"
new AgeGeneralizer(bandWidth: 5).Generalize(64).Label;   // "60-64"

ZipCodeGeneralizer.SafeHarbor.Generalize("02115-1234");  // "021"
ZipCodeGeneralizer.SafeHarbor.Generalize("03601");       // "000": a sparse three-digit area
ZipCodeGeneralizer.SafeHarbor.Generalize("SW1A 1AA");    // null: not a US ZIP, drop it

TextDateParser.TryParse("March 5th, 2024", out var date);                  // 2024-03-05
YearOnlyDateTransformer.Instance.Transform(date, "patient-1").ToString();   // "2024"
```

The sparse ZIP list is HHS's from the 2000 Census; pass a current one to `new ZipCodeGeneralizer(prefixes)`. `PartialDate` holds a date known to year, month or day. `TextDateParser` reads dates as clinical text writes them, month-first (US), and never parses a date without a year.

## Checking output

### Why

Text that DeidSharp didn't write itself (a language model's summary, a report template filled by another system) can carry identifiers back in. `LeakScanner` checks it with the deterministic detectors only, so the check never shares a model's failure modes.

### Text, values and JSON

```csharp
using DeidSharp.Verification;

var scanner = LeakScanner.CreateDefault();

LeakReport report = scanner.Scan(summaryText, known);
LeakReport fields = scanner.Scan([new ScannedValue("note.assessment", assessment), new ScannedValue("note.plan", plan)], known);
LeakReport json = scanner.ScanJson(document.RootElement, known, new LeakScanOptions
{
    ScanPropertyNames = true,                                           // a model can put a name in a key
    PathPropertyNames = new HashSet<string>(["assessment", "plan"]),    // your schema's names
});

if (!report.IsClean)
{
    logger.LogWarning("Rejected: {Findings}", string.Join("; ", report.Describe()));
    // "$: possible Name at [9, 21) (known)"
}
```

Findings carry paths, positions and categories, never values. A JSON key outside `PathPropertyNames` is written as `<property N>` (a name found in the key itself as `<property N>(name)`), because a key written by a model can itself be PHI. Leave `PathPropertyNames` null only for JSON whose keys you control. `IgnoredCategories` skips categories in fields that legitimately hold them (code-system URLs, say); `MinimumScore` filters by score.

### Checking against the record

The scanner finds the record's known identifiers and shaped identifiers. It doesn't check that a model's summary is true: whether a fact was invented or altered is for you to check against the record.

## Errors and failing closed

DeidSharp prefers no output to a leaky one. Four exceptions matter:

| Exception | Thrown when | What to do |
|---|---|---|
| `PhiDetectionException` | A detector failed: a sidecar is down or timed out, a model's answer wasn't valid | Retry later, or stop. Never fall back to a smaller stack silently: that is a lower-recall stack nobody chose |
| `ResidualPhiException` | The output still holds a known identifier (text) or fails the structural check (FHIR) | Don't release that output. `e.Report` says where, by position. Usually a detector or rule gap: fix it, don't turn verification off |
| `FhirInputException` | The input isn't valid FHIR JSON | Reject the input. The message has only a count of problems |
| `ArgumentException` (and `ArgumentOutOfRangeException`) | An option is out of range: a key too short, a chunk length below 500 | Fix the configuration. Options never fall back to a default silently |

Their messages never quote the input, so log the exception's type and message. **Don't log the exception object itself**, or an inner exception: `PhiDetectionException` wraps the detector's original exception, and some exceptions carry the text they were working on as a property (a regex timeout does), which structured loggers write out. An example is under [An endpoint](#an-endpoint).

A canceled `CancellationToken` throws `OperationCanceledException`, as usual.

## Using DeidSharp in a service

### Registering the stack

```csharp
using DeidSharp.Presidio;

builder.Services.AddPresidioDetector("clinical", o =>
{
    o.Endpoint = new Uri(builder.Configuration["Deid:ClinicalEndpoint"]!);
    o.Timeout = TimeSpan.FromSeconds(30);
})
.UseSocketsHttpHandler((handler, _) => handler.PooledConnectionLifetime = TimeSpan.FromMinutes(5))
.SetHandlerLifetime(Timeout.InfiniteTimeSpan);

builder.Services.AddSingleton<IPhiDetector>(sp => new CompositePhiDetector([
    sp.GetRequiredKeyedService<PresidioDetector>("clinical"),
    new GazetteerDetector(),
    .. DeterministicDetectors.Create(),
]));
builder.Services.AddSingleton(sp => new TextDeidentifier(sp.GetRequiredService<IPhiDetector>(), new TagReplacement()));
```

The stack and the de-identifier are singletons: they're stateless and thread-safe. A singleton stack holds its Presidio detector's `HttpClient` for the application's lifetime, so let the connections recycle themselves (`PooledConnectionLifetime`) instead of relying on the handler's rotation. `AddPresidioDetector(configure)` without a name registers one typed `PresidioDetector`; with a name, each is a keyed service with its own options and client. A resilience handler is optional; see [Presidio options](#presidio-options) for its timeout.

### An endpoint

```csharp
app.MapPost("/deidentify", async (DeidRequest request, TextDeidentifier deidentifier, ILogger<Program> logger, CancellationToken ct) =>
{
    try
    {
        var result = await deidentifier.DeidentifyAsync(request.Text, new TextContext(request.SubjectId), ct);
        return Results.Ok(new { result.Text });
    }
    catch (PhiDetectionException e)
    {
        // A layer is missing: refuse rather than return less protected text. Log the type, not the object.
        logger.LogError("De-identification failed: {Error}", e.Message);
        return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "De-identification is unavailable.");
    }
    catch (ResidualPhiException e)
    {
        logger.LogError("Refused output: {Findings}", string.Join("; ", e.Report.Describe()));
        return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "The output still held possible PHI.");
    }
});

record DeidRequest(string Text, string SubjectId);
```

This endpoint gets no known identifiers, so `TextDeidentifier` has nothing to check the output against: the detectors are all that stand between the input and the response. If the caller has the patient's record, pass its identifiers in `TextContext`, or check the output yourself with `LeakScanner`.

Things a service must do that the library can't:

- **Never log request or response bodies**, and turn off any body logging or tracing middleware for these routes.
- **Keep keys server-side**: from configuration or a secret store, never from a request.
- **Limit request size** and authenticate callers: the endpoint turns PHI into less-protected text.
- **Check the sidecar and the model at startup** and expose a health check, so a missing layer stops the service instead of lowering recall.

### Health checks

```csharp
sealed class ClinicalSidecarHealthCheck([FromKeyedServices("clinical")] PresidioDetector clinical) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await clinical.IsHealthyAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The clinical sidecar isn't answering.");
}

builder.Services.AddHealthChecks().AddCheck<ClinicalSidecarHealthCheck>("clinical");
```

For Ollama, a short request to the model at startup proves it's loaded.

## Telemetry

DeidSharp emits metrics and traces through .NET's own `Meter` and `ActivitySource`, both named `DeidSharp` (`DeidSharpTelemetry.Name`). They carry detector names, categories, outcomes and counts, never text, values or paths.

| Instrument | Kind | Tags |
|---|---|---|
| `deidsharp.detector.duration` | Histogram (seconds) | `deidsharp.detector`, `deidsharp.outcome` (`ok`, `failed`) |
| `deidsharp.text.replacements` | Counter | `deidsharp.category` |
| `deidsharp.residual_phi.refusals` | Counter | `deidsharp.stage` (`text`, `fhir`) |

Each detection is a trace span `deidsharp.detect` with tags `deidsharp.detector` and `deidsharp.spans`, and each FHIR run a span `deidsharp.fhir.deidentify` with `deidsharp.fhir.resource_type`, `deidsharp.fhir.mode` and `deidsharp.fhir.free_text_values`. With OpenTelemetry:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter(DeidSharpTelemetry.Name))
    .WithTracing(t => t.AddSource(DeidSharpTelemetry.Name));
```

`deidsharp.residual_phi.refusals` above zero means outputs were refused: look at the logged findings. A rising `failed` outcome on a detector means a sidecar or model is in trouble.

## Performance and sizing

Time per clinical note of a few hundred words:

| Layer | Time per note | Hardware |
|---|---:|---|
| Known identifiers, patterns, gazetteer | Milliseconds | Any |
| Clinical sidecar, CPU | 0.3–0.5 s | A desktop CPU |
| Clinical sidecar, GPU | Several times faster than CPU | NVIDIA GPU |
| `gemma4:26b-a4b` through Ollama | 0.4 s | 16 GB GPU (RTX 5080), a fifth of the model on the CPU |
| Second opinion | 0.1 s | The same GPU |

- **Run layers in parallel.** A stack already runs its detectors in parallel; for many notes, call `DeidentifyAsync` concurrently (FHIR does this for a bundle's texts, `MaxFreeTextConcurrency`).
- **Reuse instances.** Build stacks, de-identifiers and HTTP clients once.
- **Batch jobs** are bound by the slowest layer: a GPU sidecar and a GPU model share one card, so size the GPU for both or put them on separate machines.
- **Model size.** Larger models find more PHI; smaller ones fit smaller GPUs. Ollama's model memory plus the context window must fit the card, or part of the model runs on the CPU and slows down.

## Measuring recall on your own data

The expected numbers above come from one kind of clinical note. Before you rely on a stack, measure it on your own data.

1. **Sample** a few hundred texts at random from the data you will process, across note types and sources.
2. **Annotate** them: two people mark every PHI span independently, then reconcile. Annotation is PHI work: do it under your governance, on approved machines.
3. **Run** your stack:

```csharp
using DeidSharp.Evaluation;

AnnotatedText[] sample =
[
    new("Seen by Dr. Hart on 03/04/2024.", [new GoldSpan(12, 16, PhiCategory.Name), new GoldSpan(20, 30, PhiCategory.Date)]),
    new("Daughter Carmen called.", [new GoldSpan(9, 15, PhiCategory.Name)]),
];

EvaluationReport report = await DetectionEvaluator.EvaluateAsync(stack, sample);

Console.WriteLine($"recall {report.Overall.Recall:P1}, character recall {report.CharacterRecall:P1}, precision {report.Overall.Precision:P1}");
foreach (var (category, recall) in report.PerCategory)
{
    Console.WriteLine($"{category}: {recall.Recall:P1} of {recall.GoldSpans}");
}
```

`DetectionEvaluator` gives each detector the text only, as `AnnotatedText` carries no record, so the known-identifier detector finds nothing there: the numbers are a floor for use with the record's identifiers.

4. **Read the misses by category**, not just the total: one missed category (staff names, say) can matter more than the overall number. Character recall shows partly covered names; `NonPhiRemoved` is the clinical text you lose.
5. **Fix and re-measure** on a different sample than the one you read, or you'll fit your fixes to it.

## Re-identification risk

Safe Harbor output can still single someone out when rare values combine (a rare diagnosis, a small ZIP area, a birth year). Measure what remains before you release a dataset:

```csharp
using DeidSharp.Risk;

var report = ReidentificationRiskAssessor.Assess(rows, quasiIdentifiers: ["ageBand", "sex", "zip3"], sensitiveAttribute: "diagnosis");

report.IsKAnonymous(5);            // every combination shared by at least 5 records?
report.IsLDiverse(2);              // at least 2 distinct diagnoses in every group?
report.UniqueRecords;              // records alone in their group
report.MaximumProsecutorRisk;      // 1 / smallest group
```

Rows are field name → value. The assessor measures; it doesn't generalize the data for you. Generalize further (wider age bands, fewer ZIP digits) or suppress records, then measure again.

## Before production

- [ ] Read the [threat model](threat-model.md), and know what DeidSharp doesn't guarantee.
- [ ] Choose a stack, and **measure its recall on an annotated sample of your own data**.
- [ ] Pass every known identifier the record holds (`TextContext.Known`), or let `FhirDeidentifier` extract them.
- [ ] Keep `VerifyOutput` on, and treat `ResidualPhiException` as a stop.
- [ ] Load pseudonym keys from a secret store; never ship them with the data.
- [ ] Run the sidecar and the model on the same host or a private network, never exposed publicly.
- [ ] Never log inputs or outputs; log findings (positions and categories) only.
- [ ] Fail closed when a layer is missing: health-check the sidecar and the model at startup.
- [ ] Have a person review a random sample of output, every time the data source, the stack or DeidSharp changes.
- [ ] Assess re-identification risk of datasets before release.
- [ ] Work under ethics approval and a data use agreement.

## Questions

### Is DeidSharp HIPAA compliant?

No software is "HIPAA compliant" by itself. DeidSharp implements Safe Harbor's transforms for structured data and removes the PHI its detectors find in text. Safe Harbor also requires that you have no actual knowledge that what remains could identify someone, which is a judgment about your data. It has not been through an expert determination or an external audit.

### Does it send data anywhere?

The core, FHIR and gazetteer packages run in-process and send nothing. `PresidioDetector` sends text to the analyzer at the endpoint you configure; `LlmPhiDetector` sends it to the `IChatClient` you give it. Telemetry carries no text. Run both services where the data may go.

### Can I use a cloud model (OpenAI, Azure, Anthropic)?

Technically yes, through any `IChatClient` that supports a JSON-schema response format. Whether you may send PHI to that provider is a legal question (a business associate agreement, data retention) that DeidSharp can't answer. It has only been tested with local models.

### How accurate is it?

On real clinical notes, 96.4% to 98.9% of identifiers removed with a local language model, and 88.3% with the clinical sidecar and no model ([Choosing a detector stack](#choosing-a-detector-stack)). Your data will differ: measure it. How these numbers were produced is in [docs/evaluation](evaluation/README.md).

### Why did a drug or a clinical term become [NAME]?

A detector thought it was a name: often the gazetteer ("Foley" is also a surname) or stock Presidio's spaCy. DeidSharp replaces every span any detector reports, so false positives cost text, not privacy. Remove the noisy detector, or measure a `CategoryFilter`, rather than raising `MinimumScore`.

### Why was "3/14" kept?

The pattern library doesn't treat a numeric month and day without a year as a date, because that's how pain scores and ventilator settings are written ("PS 12/5"). Written-out dates without a year ("MAR 5", "5th of March") are found. The clinical model and the language model also find numeric dates in context.

### Why is a year kept?

Safe Harbor allows the year of a date, so `YearOnlyDateTransformer` and the FHIR rules keep it, and the pattern library doesn't flag a bare year. The exception is ages over 89: for anyone 90 or older, even the birth year goes. FHIR applies that to the record's birth date; in free text DeidSharp can't tell whose date a date is, so a birth year 90 or more years ago is kept unless a detector reports the age ([known gaps](safe-harbor.md#known-gaps)).

### Can I get the original values back?

Not from tags. Pseudonyms are keyed one-way hashes: you can't reverse them, but whoever holds the key can recompute one from a known value and so link records. If you need re-identification, keep your own mapping table under the same protections as the source data.

### Why did I get ResidualPhiException with surrogates?

The output still contained one of the subject's known identifiers. The usual causes: the stack has no `KnownIdentifierDetector` (the check always looks for the known identifiers, the stack may not), the detector runs with stricter `KnownIdentifierOptions` than the check, or a custom replacement strategy returned the original text. `e.Report.Describe()` gives the position and category.

### Is it thread-safe?

Yes. Detectors, stacks, `TextDeidentifier`, `FhirDeidentifier`, `LeakScanner` and the generalizers hold no per-call state; build them once and share them. Your own detectors must be safe to call concurrently too.

### Which .NET versions?

.NET 10. The core has no package dependencies.

### Does it handle PDFs, images or audio?

No. FHIR attachments that aren't UTF-8 text are removed, never inspected. Faces in images and voices in recordings aren't detected.

### How do I report a missed identifier?

As an ordinary issue with a fictional example, using the "Missed identifier" template. Never paste real patient data, even privately. A way to leak data that the threat model says can't leak is a vulnerability: report it privately ([SECURITY.md](../SECURITY.md)).

## Reference

### PHI categories

| `PhiCategory` | Safe Harbor | Tag |
|---|---|---|
| `Name` | A | `[NAME]` |
| `StreetAddress` | B | `[ADDRESS]` |
| `Location` | B | `[LOCATION]` |
| `ZipCode` | B | `[ZIP]` |
| `Date` | C | `[DATE]` |
| `Age` (over 89) | C | `[AGE]` |
| `Phone` | D | `[PHONE]` |
| `Fax` | E | `[FAX]` |
| `Email` | F | `[EMAIL]` |
| `Ssn` | G | `[SSN]` |
| `MedicalRecordNumber` | H | `[MRN]` |
| `HealthPlanNumber` | I | `[HEALTH_PLAN_ID]` |
| `AccountNumber` | J | `[ACCOUNT]` |
| `LicenseNumber` | K | `[LICENSE]` |
| `VehicleIdentifier` | L | `[VEHICLE_ID]` |
| `DeviceIdentifier` | M | `[DEVICE_ID]` |
| `Url` | N | `[URL]` |
| `IpAddress` | O | `[IP_ADDRESS]` |
| `Biometric` | P | `[BIOMETRIC]` |
| `Photo` | Q | `[PHOTO]` |
| `OtherIdentifier` | R | `[ID]` |
| `Organization` | — | `[ORGANIZATION]` |

### TextDeidentifierOptions

| Option | Default | Valid | Meaning |
|---|---|---|---|
| `MinimumScore` | 0 | 0–1 | Spans scoring below this are left in the text |
| `VerifyOutput` | true | | Throw if a known identifier survived |

### KnownIdentifierOptions

| Option | Default | Valid | Meaning |
|---|---|---|---|
| `MinimumIdentifierLength` | 4 | ≥ 1 | Identifiers with fewer letters and digits aren't searched for |
| `MinimumNameTokenLength` | 2 | ≥ 1 | Shorter name parts aren't searched for on their own |
| `MatchTimeout` | 2 s | | The longest one search may run before it fails |

### FhirDeidentificationOptions

| Option | Default | Valid | Meaning |
|---|---|---|---|
| `Mode` | `SafeHarbor` | `SafeHarbor`, `Pseudonymized` | Which standard the run follows |
| `Key` | null | ≥ 32 bytes; required when pseudonymized | The pseudonym key; null in Safe Harbor uses a fresh one per run |
| `FreeTextDetector` | null | | The free-text stack; null is the deterministic one |
| `FreeTextStyle` | `Tags` | `Tags`, `Surrogates` | What found PHI in free text becomes |
| `AllowedExtensions` | US Core race, ethnicity, birth sex | | Extension URLs kept; all others removed |
| `MaxFreeTextConcurrency` | 8 | 1–256 | Free-text values de-identified at once |
| `ReferenceDate` | null (today, UTC) | | The date ages are computed at |
| `TimeProvider` | `TimeProvider.System` | | Where "today" comes from |
| `TopCodeAge` | 90 | 1–150 | From this age, the birth date is removed |
| `MaxDateShiftDays` | 365 | 1–3650 | The largest date shift when pseudonymized |
| `ZipCodes` | `ZipCodeGeneralizer.SafeHarbor` | | How ZIP codes are cut down |
| `VerifyOutput` | true | | Check the output and throw on any leftover |
| `AdditionalRules` | none | | Rules that run before the built-in ones |

### GazetteerOptions

| Option | Default | Valid | Meaning |
|---|---|---|---|
| `MinimumSurnameCount` | 10,000 | ≥ 0 | A surname counts on its own only if this many people had it (2010 Census) |
| `Capitals` | true | | Also look up words in capitals |
| `Lowercase` | false | | Also look up lowercase words |

### PresidioOptions

| Option | Default | Valid | Meaning |
|---|---|---|---|
| `Endpoint` | `http://localhost:5002/` | | The analyzer's base address |
| `Name` | `presidio` | | The detector's name in span sources |
| `Language` | `en` | | Passed to the analyzer |
| `ScoreThreshold` | 0.3 | 0–1 | The analyzer drops findings below this |
| `Entities` | all | | Ask for only these entity types |
| `IgnoredEntities` | NRP, CRYPTO | | Entity types to drop |
| `EntityMap` | built in | | Overrides for entity type → category |
| `ReportRecognizers` | false | | Record the analyzer's recognizer in each span's source |
| `Timeout` | 30 s | positive | How long one detection may wait, retries included |

### LlmDetectorOptions

| Option | Default | Valid | Meaning |
|---|---|---|---|
| `Name` | `llm` | not blank | The detector's name in span sources |
| `Prompt` | `LlmPrompt.Default` | | What the model is told (use `WithPoolExamples`) |
| `Retriever` | null | | Picks extra examples per text |
| `RetrievedExamples` | 4 | 1–20 | Examples the retriever adds |
| `ChunkLength` | 2,000 | 500–100,000 | Characters sent per call |
| `ChunkOverlap` | 200 | 0–half the chunk | Characters consecutive pieces share |
| `MaxOutputTokens` | 2,048 | ≥ 64 | The most the model may write per piece |
| `SafeHarborOnly` | true | | Drop what Safe Harbor keeps from the answer |
| `ConfigureRequest` | null | | Provider settings per request (Ollama: thinking, window) |

### LlmSecondOpinionOptions

| Option | Default | Valid | Meaning |
|---|---|---|---|
| `MayVeto` | spans from no known-identifier, pattern or LLM detector (by source name: `known`, `pattern…`, `llm…`), never a location or organization | | Which spans the model may drop |
| `MaxCandidatesPerCall` | 40 | 1–200 | Candidates shown per call |
| `ConfigureRequest` | null | | As for the detector |

### LeakScanOptions

| Option | Default | Meaning |
|---|---|---|
| `IgnoredCategories` | none | Categories not to report |
| `MinimumScore` | 0 | Findings below this aren't reported |
| `ScanPropertyNames` | false | Also scan JSON keys |
| `PathPropertyNames` | null (every name written as is) | The JSON keys that may appear in paths; others become `<property N>` |
