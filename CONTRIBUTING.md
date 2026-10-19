# Contributing to DeidSharp

Thank you for helping. DeidSharp de-identifies health data, so every change is held to one rule above the others: **PHI never leaks**. This guide covers how to build, what a change must keep, and how to send it.

## Before you start

- **Never use real patient data**, not in code, tests, fixtures, issues or pull requests. Make up names, dates and numbers. Test corpora here are fictional. Anything real is deleted on sight, edit history included.
- **Security problems** go through private reporting, not issues: see [SECURITY.md](SECURITY.md).
- For a larger change (a new detector, a new FHIR rule, a new corpus), open an issue first so we can agree on the approach.

## Building and testing

You need the .NET SDK named in [global.json](global.json) (10.0.401 or a later 10.0 feature release). Docker and Python 3.12 are optional, for the integration and sidecar checks.

```bash
dotnet build DeidSharp.slnx
dotnet test DeidSharp.slnx                           # do not add -nologo (see docs/testing.md)
DEIDSHARP_INTEGRATION=1 dotnet test DeidSharp.slnx   # also the stock Presidio container (Docker)
python -m pip install -r sidecar/tools/requirements.txt
ruff check sidecar tools/baselines && ruff format --check sidecar tools/baselines && python -m pytest sidecar/tests
```

CI runs all of this on every pull request ([docs/testing.md](docs/testing.md#continuous-integration)).

## What every change keeps

These are the guarantees in [docs/threat-model.md](docs/threat-model.md). A change that alters what the threat model says updates it in the same pull request.

- **Nothing quotes the input.** Exceptions, logs, findings and reports carry positions, categories and paths only. Name an invalid entry by its position (`ageBandPaths[2]`), never by its value.
- **Fail closed.** A detector that fails throws `PhiDetectionException`; leftover PHI throws `ResidualPhiException`; a null input is refused. Nothing that failed ever reads as "nothing found".
- **The leak guard stays deterministic.** It uses only the known-identifier and pattern detectors.
- **Keys stay server-side.** Pseudonym keys never come from a request.
- **Recall first.** Every span any detector reports is replaced. Raising a threshold needs evaluation results that justify the lost recall.

## Code conventions

- Analyzers and code style are build errors (`Directory.Build.props`, `.editorconfig`). Turn a rule off only in `.editorconfig`, with a reason.
- New behavior comes as a new strategy class or table row (a detector, a FHIR rule, a pattern), not as a new branch in existing code. Detectors implement `IPhiDetector` and must be safe to call concurrently.
- One main type per file, named after it; namespaces follow folders; `tests/` mirrors `src/`.
- Where code uses a design pattern, a comment above the type names it, defines it and says how it is used here (see any detector for the form).
- Comments explain why. Write American English in code, comments, docs and commit messages.
- Package versions live only in `Directory.Packages.props`. After changing one, run `dotnet restore` and commit the updated `packages.lock.json` files; CI restores in locked mode and fails on a stale lock. The sidecar's Python packages are locked the same way (`sidecar/README.md`, "Dependencies").
- The maintainer also keeps the code ReSharper-clean (`jb inspectcode`); you don't need to, but suggestions in review may come from it.

## Tests

Every change comes with tests that prove it, including that it does not leak: a test that the output or the exception message does not contain the identifier you fed in. Prefer a property test (CsCheck) where an invariant holds over many inputs. Keep the README examples working; they run as tests.

## Pull requests

- Keep a pull request to one change, with a title that says what it does ("Find ages written as words", not "Fix bug").
- Describe what changed and why, and how you tested it. Fill in the template's checklist.
- Commit messages: a short summary line in the imperative, a blank line, then the why.
- Update [CHANGELOG.md](CHANGELOG.md) under "Unreleased" for anything a user of the libraries or the evaluation tool would notice.

By contributing, you agree that your contribution is licensed under the [MIT License](LICENSE), like the rest of the project.
