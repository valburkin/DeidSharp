# DeidSharp

[![CI](https://github.com/valburkin/DeidSharp/actions/workflows/ci.yml/badge.svg)](https://github.com/valburkin/DeidSharp/actions/workflows/ci.yml)
[![PHI recall](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2Fvalburkin%2FDeidSharp%2Fmain%2Fdocs%2Fevaluation%2Fbadge.json)](https://github.com/valburkin/DeidSharp/blob/main/docs/evaluation/README.md)

De-identification of health data for .NET: HIPAA Safe Harbor transforms, pseudonymization, stackable PHI detectors for free text, a deterministic leak guard, re-identification risk metrics and detector evaluation. FHIR R4 resources and bundles are supported directly.

> **Research-grade software.** DeidSharp has not been certified, audited or validated by an expert determination. It removes what its rules and detectors find, and free-text detection is never perfect. Before using it on real patient data, read the [threat model](https://github.com/valburkin/DeidSharp/blob/main/docs/threat-model.md), measure recall on an annotated sample of your own data ([testing](https://github.com/valburkin/DeidSharp/blob/main/docs/testing.md)), and operate under your organization's governance (ethics approval, data use agreement).

## What it does

| Layer | What happens | Where |
|---|---|---|
| Structured fields | Safe Harbor: names, contact points, identifiers removed; dates cut to the year; ages 90+ removed or top-coded; addresses cut to state and three-digit ZIP ("000" for sparse areas); unknown extensions removed | `DeidSharp.Structured`, `DeidSharp.Fhir` |
| Pseudonymization | Keyed HMAC pseudonyms for ids and identifiers; one date shift per patient that preserves intervals | `DeidSharp.Pseudonymization` |
| Free text | A stack of detectors (the record's own identifiers, a pattern library, name and place lists, Presidio, a clinical transformer, a local language model) finds PHI; spans are merged and replaced by tags or realistic surrogates. [Measured stacks](https://github.com/valburkin/DeidSharp/blob/main/docs/providers.md#with-a-local-language-model) | `DeidSharp.Detection`, `DeidSharp.Text`, `DeidSharp.Gazetteer`, `DeidSharp.Presidio`, `DeidSharp.Llm` |
| Leak guard | A deterministic scan of the output for anything identifying that remains, and structural checks on FHIR output; fails closed | `DeidSharp.Verification` |
| Risk | k-anonymity, l-diversity and prosecutor risk of a de-identified dataset | `DeidSharp.Risk` |
| Evaluation | Recall, precision, character recall, word-level F1 and the share of other text removed, for a detector stack against annotated text; [measured results](https://github.com/valburkin/DeidSharp/blob/main/docs/evaluation/README.md) on PhysioNet's nursing notes and ASQ-PHI | `DeidSharp.Evaluation`, `tools/DeidSharp.Evaluation.Cli` |

Coverage of the 18 Safe Harbor identifiers, element by element, is in [docs/safe-harbor.md](https://github.com/valburkin/DeidSharp/blob/main/docs/safe-harbor.md).

## Packages

| Project | Depends on | For |
|---|---|---|
| `DeidSharp` | nothing | Detection, text de-identification, structured transforms, pseudonymization, leak guard, risk, evaluation |
| `DeidSharp.Fhir` | `DeidSharp`, `Hl7.Fhir.R4` | De-identifying FHIR R4 resources and bundles |
| `DeidSharp.Presidio` | `DeidSharp`, `Microsoft.Extensions.Http` | A detector backed by a Presidio analyzer service (stock image or the clinical sidecar) |
| `DeidSharp.Gazetteer` | `DeidSharp` | A deterministic detector for names and places, from public-domain US Census lists |
| `DeidSharp.Llm` | `DeidSharp`, `Microsoft.Extensions.AI.Abstractions` | A detector that asks a language model through `IChatClient`, meant for a local model (Ollama, vLLM) |

The deterministic layers need no network, model or container. NER is an optional detector running in a sidecar.

## Quick start

De-identify a note:

```csharp
using DeidSharp.Detection;
using DeidSharp.Text;

// The deterministic stack: the record's known identifiers plus the pattern library.
var detector = new CompositePhiDetector(DeterministicDetectors.Create());
var deidentifier = new TextDeidentifier(detector, new TagReplacement());

// What the structured record already knows about this patient.
var known = KnownIdentifiers.Empty.AddName("Rosa Alvarez").AddDate(new DateOnly(1931, 3, 4));

var result = await deidentifier.DeidentifyAsync(
    "Rosa Alvarez (DOB 03/04/1931) called from 617-555-0142.",
    new TextContext(subjectId: "patient-1", known));

// result.Text == "[NAME] (DOB [DATE]) called from [PHONE]."
```

De-identify a FHIR bundle under Safe Harbor:

```csharp
using DeidSharp.Fhir;

var deidentifier = new FhirDeidentifier(new FhirDeidentificationOptions());
var result = await deidentifier.DeidentifyAsync(bundle);   // a de-identified copy; the input is untouched
string json = await deidentifier.DeidentifyJsonAsync(bundleJson);
```

Pseudonymize instead, so the same patient links across exports:

```csharp
var options = new FhirDeidentificationOptions
{
    Mode = FhirDeidentificationMode.Pseudonymized,
    Key = PseudonymKey.FromBase64(secretFromYourVault),
    FreeTextStyle = FreeTextStyle.Surrogates,
};
```

Add NER for names and places the record does not list:

```csharp
using DeidSharp.Gazetteer;
using DeidSharp.Presidio;

services.AddPresidioDetector(o =>
{
    o.Endpoint = new Uri("http://localhost:5003/");   // the clinical sidecar
    o.Name = "clinical";
});

var stack = new CompositePhiDetector([
    serviceProvider.GetRequiredService<PresidioDetector>(),
    new GazetteerDetector(),
    .. DeterministicDetectors.Create(),
]);
var fhir = new FhirDeidentifier(new FhirDeidentificationOptions { FreeTextDetector = stack });
```

Check output produced elsewhere, for example by an LLM:

```csharp
using DeidSharp.Verification;

var report = LeakScanner.CreateDefault().ScanJson(summaryJson.RootElement, known);
if (!report.IsClean)
{
    // Positions and categories only, safe to log: "$.conditions[0]: possible Ssn at [9, 20) (pattern:ssn)"
    logger.LogWarning("Rejected: {Findings}", string.Join("; ", report.Describe()));
}
```

## Detector sidecars

```bash
docker compose up -d presidio            # stock Presidio: spaCy NER + pattern recognizers, 127.0.0.1:5002
docker compose up -d --build clinical    # Presidio + obi/deid_roberta_i2b2, 127.0.0.1:5003
docker compose --profile gpu up -d --build clinical-gpu   # the same on an NVIDIA GPU, 127.0.0.1:5005
```

The clinical image bakes its model in at build time and runs offline. See [sidecar/README.md](https://github.com/valburkin/DeidSharp/blob/main/sidecar/README.md).

## Reproducing the evaluation

The numbers of record come from `tools/DeidSharp.Evaluation.Cli` at the `v1.0.0` tag, with PhysioNet's deid and Philter scored next to them by the same code. [REPRODUCING.md](https://github.com/valburkin/DeidSharp/blob/main/REPRODUCING.md) lists the environment, the versions and the commands; [findings](https://github.com/valburkin/DeidSharp/blob/main/docs/evaluation/findings.md) logs every measurement, those made along the way included, and what each led to. ASQ-PHI ships with the repository; PhysioNet's notes need a credentialed account and stay on your machine.

## Why .NET

Most de-identification research is written in Python; DeidSharp is not, for three reasons.

- **It began as a library for .NET systems**, which Python tools don't serve in-process, and it is still used that way: the measured stacks are the ones an application runs.
- **The models run on their usual stack.** The clinical model is Python (Presidio and transformers) in its sidecar, and the language model runs in Ollama. C# does the orchestration, the rules, the filters and the scoring, so the language doesn't change what a model finds.
- **Other tools are scored by the same code.** The evaluation tool reads any tool's spans from a file (`--predictions`), so a tool in another language is measured on the same notes, split and scorer; PhysioNet's deid (Perl) and Philter (Python) are scored that way.

Reproducing the results needs the .NET SDK and Docker, not knowledge of C#.

## Development

```bash
dotnet build DeidSharp.slnx
dotnet test DeidSharp.slnx                     # unit, property and corpus tests; integration tests skip
DEIDSHARP_INTEGRATION=1 dotnet test DeidSharp.slnx   # also runs against the stock Presidio container
python -m pip install -r sidecar/tools/requirements.txt
ruff check sidecar tools/baselines && ruff format --check sidecar tools/baselines && python -m pytest sidecar/tests
```

`dotnet test` runs on Microsoft.Testing.Platform: do not pass `-nologo`. Details in [docs/testing.md](https://github.com/valburkin/DeidSharp/blob/main/docs/testing.md).

## Documentation

- [Architecture](https://github.com/valburkin/DeidSharp/blob/main/docs/architecture.md): layers, the detection pipeline, the FHIR rule chain, extension points
- [Safe Harbor coverage](https://github.com/valburkin/DeidSharp/blob/main/docs/safe-harbor.md): how each of the 18 identifiers is handled, and the gaps
- [Threat model](https://github.com/valburkin/DeidSharp/blob/main/docs/threat-model.md): what DeidSharp guarantees, what it does not, and how to use it safely
- [Detectors](https://github.com/valburkin/DeidSharp/blob/main/docs/providers.md): the built-in detectors, Presidio, the clinical model, writing your own
- [Testing](https://github.com/valburkin/DeidSharp/blob/main/docs/testing.md): the test suites, measuring recall on your own data, CI
- [Evaluation](https://github.com/valburkin/DeidSharp/blob/main/docs/evaluation/README.md): measured recall on PhysioNet's nursing notes and ASQ-PHI, and how to rerun it

## Contributing and security

Contributions are welcome: see [CONTRIBUTING.md](https://github.com/valburkin/DeidSharp/blob/main/CONTRIBUTING.md) and the [code of conduct](https://github.com/valburkin/DeidSharp/blob/main/CODE_OF_CONDUCT.md). Report a vulnerability privately, as [SECURITY.md](https://github.com/valburkin/DeidSharp/blob/main/SECURITY.md) describes, never in a public issue. Changes are listed in [CHANGELOG.md](https://github.com/valburkin/DeidSharp/blob/main/CHANGELOG.md).

## License

[MIT](https://github.com/valburkin/DeidSharp/blob/main/LICENSE). The software is provided as is, without warranty of any kind; see the research-grade notice above.
