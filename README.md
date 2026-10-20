# DeidSharp

[![CI](https://github.com/valburkin/DeidSharp/actions/workflows/ci.yml/badge.svg)](https://github.com/valburkin/DeidSharp/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/DeidSharp.svg)](https://www.nuget.org/packages/DeidSharp)
[![PHI recall](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2Fvalburkin%2FDeidSharp%2Fmain%2Fdocs%2Fevaluation%2Fbadge.json)](https://github.com/valburkin/DeidSharp/blob/main/docs/evaluation/README.md)

De-identification of health data for .NET. DeidSharp removes the identifiers from free text and FHIR R4 resources under HIPAA's Safe Harbor method, or replaces them with keyed pseudonyms. It stacks detectors (the record's own identifiers, patterns, name lists, a clinical NER model, a local language model), checks its own output before returning it, and fails closed when anything goes wrong.

> **Research-grade software.** DeidSharp has not been certified, audited or validated by an expert determination. It removes what its rules and detectors find, and free-text detection is never perfect. Before using it on real patient data, read the [threat model](https://github.com/valburkin/DeidSharp/blob/main/docs/threat-model.md), measure recall on an annotated sample of your own data, and operate under your organization's governance (ethics approval, data use agreement).

## Install

```bash
dotnet add package DeidSharp            # free text, detectors, pseudonyms, the leak guard (no dependencies)
dotnet add package DeidSharp.Fhir       # FHIR R4 resources and bundles
dotnet add package DeidSharp.Presidio   # the clinical NER sidecar, or stock Presidio
dotnet add package DeidSharp.Gazetteer  # names and places from US Census lists, no model needed
dotnet add package DeidSharp.Llm        # a local language model as a detector
```

DeidSharp targets .NET 10.

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

Check output produced elsewhere, for example by an LLM, before it leaves:

```csharp
using DeidSharp.Verification;

var report = LeakScanner.CreateDefault().ScanJson(summaryJson.RootElement, known, new LeakScanOptions
{
    PathPropertyNames = new HashSet<string>(["conditions", "plan"]),   // your schema's keys; others are masked in paths
});
if (!report.IsClean)
{
    // Paths, positions and categories only, safe to log:
    // "$.conditions[0]: possible Ssn at [9, 20) (pattern:ssn+pattern:ssn-labeled)"
    logger.LogWarning("Rejected: {Findings}", string.Join("; ", report.Describe()));
}
```

Runnable versions are in [samples](https://github.com/valburkin/DeidSharp/tree/main/samples): `dotnet run --project samples/QuickStart`.

## Choosing a detector stack

The deterministic stack above finds the record's own identifiers and shaped ones (phones, dates, record numbers). Clinical notes also name people and places the record doesn't list, which takes NER or a language model. What to expect on real clinical notes:

| Stack | Needs | Identifiers removed | Precision |
|---|---|---:|---:|
| Deterministic (known identifiers, patterns) + gazetteer | Nothing | 42% | 44% |
| Deterministic + gazetteer + clinical NER sidecar | Docker | 88% | 43% |
| Deterministic + local language model (precision first) | A 16 GB GPU | 96% | 83% |
| Deterministic + clinical sidecar + language model (recall first) | Docker and a GPU | 99% | 54% |

These were measured on the note text alone; with the record's own identifiers, expect more. Two balanced stacks sit between the last two. The [guide](https://github.com/valburkin/DeidSharp/blob/main/docs/guide.md#choosing-a-detector-stack) explains each one, with its code; [how these were measured](https://github.com/valburkin/DeidSharp/blob/main/docs/evaluation/README.md). Measure the stack you choose on your own data.

## What it does

| Layer | What happens |
|---|---|
| Structured fields | Safe Harbor: names, contact points, identifiers removed; dates cut to the year; ages 90+ removed or top-coded; addresses cut to state and three-digit ZIP ("000" for sparse areas); unknown extensions removed |
| Pseudonymization | Keyed HMAC pseudonyms for ids and identifiers; one date shift per patient that preserves intervals |
| Free text | A stack of detectors finds PHI; spans are merged and replaced by tags or realistic surrogates |
| Leak guard | A deterministic scan of the output for anything identifying that remains, and structural checks on FHIR output; fails closed |
| Risk | k-anonymity, l-diversity and prosecutor risk of a de-identified dataset |
| Evaluation | Recall, precision and character recall of a detector stack on your own annotated text |

How each of the 18 Safe Harbor identifiers is handled is in [docs/safe-harbor.md](https://github.com/valburkin/DeidSharp/blob/main/docs/safe-harbor.md).

## Documentation

- **[Guide](https://github.com/valburkin/DeidSharp/blob/main/docs/guide.md): start here.** Installing, choosing a stack, free text, FHIR, detectors, the sidecar, a local language model, pseudonyms, checking output, errors, services, telemetry, performance, measuring on your data, and a reference of every option.
- [Threat model](https://github.com/valburkin/DeidSharp/blob/main/docs/threat-model.md): what DeidSharp guarantees, what it does not, and how to use it safely
- [Safe Harbor coverage](https://github.com/valburkin/DeidSharp/blob/main/docs/safe-harbor.md): how each of the 18 identifiers is handled, and the gaps
- [Detectors](https://github.com/valburkin/DeidSharp/blob/main/docs/providers.md): the built-in detectors compared, Presidio in depth
- [Architecture](https://github.com/valburkin/DeidSharp/blob/main/docs/architecture.md): layers, the detection pipeline, the FHIR rule chain, extension points
- [Clinical sidecar](https://github.com/valburkin/DeidSharp/blob/main/sidecar/README.md): building and running the NER service
- [Changelog](https://github.com/valburkin/DeidSharp/blob/main/CHANGELOG.md)

## Research

DeidSharp's detectors were measured on real clinical notes, and every measurement is logged. [docs/evaluation](https://github.com/valburkin/DeidSharp/blob/main/docs/evaluation/README.md) has the results and [REPRODUCING.md](https://github.com/valburkin/DeidSharp/blob/main/REPRODUCING.md) how to rerun each one.

## Contributing and security

Contributions are welcome: see [CONTRIBUTING.md](https://github.com/valburkin/DeidSharp/blob/main/CONTRIBUTING.md), with the build and test commands, and the [code of conduct](https://github.com/valburkin/DeidSharp/blob/main/CODE_OF_CONDUCT.md). Report a vulnerability privately, as [SECURITY.md](https://github.com/valburkin/DeidSharp/blob/main/SECURITY.md) describes, never in a public issue.

## License

[MIT](https://github.com/valburkin/DeidSharp/blob/main/LICENSE). The software is provided as is, without warranty of any kind; see the research-grade notice above.
