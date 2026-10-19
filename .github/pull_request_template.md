## What and why

<!-- What this changes, and the problem it solves. Link the issue if there is one. -->

## How it was tested

<!-- The tests added or changed, and anything run by hand. -->

## Checklist

- [ ] No real patient data anywhere in the change (code, tests, fixtures, this description).
- [ ] Nothing quotes the input: new exceptions, logs, findings and errors carry positions and categories only, and a test proves it.
- [ ] Failures fail closed; nothing that failed reads as "nothing found".
- [ ] The leak guard still uses only the deterministic detectors, and keys still come only from configuration, never from input.
- [ ] Tests cover the change; `dotnet test DeidSharp.slnx` passes.
- [ ] Docs updated where behavior changed (README, docs/, the threat model if a guarantee moved, docs/evaluation for a measurement).
- [ ] CHANGELOG.md updated under "Unreleased" if a user would notice.
