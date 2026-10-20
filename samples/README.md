# Samples

Small programs that use DeidSharp the way an application does. CI builds them with every change, so they always match the current API. Each one references the libraries in this repository; in your own project, install the packages instead (`dotnet add package DeidSharp`).

| Sample | What it shows | Needs | Run |
|---|---|---|---|
| [QuickStart](QuickStart/Program.cs) | Free text with tags, then surrogates; logging findings safely; checking text produced elsewhere | Nothing | `dotnet run --project samples/QuickStart` |
| [FhirBundle](FhirBundle/Program.cs) | A FHIR R4 bundle under Safe Harbor, or pseudonymized with a key from an environment variable; the run's statistics | Nothing | `dotnet run --project samples/FhirBundle` |
| [LocalLlm](LocalLlm/Program.cs) | A local language model through Ollama, in the precision-first stack | Ollama with `gemma4:26b-a4b` (a 16 GB GPU) | `dotnet run --project samples/LocalLlm` |

All data in them is fictional. The [guide](../docs/guide.md) explains every step.
