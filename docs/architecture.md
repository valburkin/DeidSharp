# Architecture

DeidSharp is a set of small, separately usable layers. The core assembly (`DeidSharp`) has no dependencies; FHIR support and the Presidio detector are separate assemblies that build on it.

```text
                    ┌────────────────────────────── DeidSharp.Fhir ─────────────────────────────┐
FHIR resource ───►  │ known-identifier extraction ─► rule chain (walker) ─► structural check    │ ─► de-identified copy
                    │                                   │ free text                             │
                    └───────────────────────────────────┼───────────────────────────────────────┘
                                                        ▼
note ─────────────► TextDeidentifier: detector stack ─► merge spans ─► replace ─► known-identifier check ─► text
                                       │
                        ┌──────────────┼───────────────────────┬────────────────────────┐
                        ▼              ▼                       ▼                        ▼
               KnownIdentifier   PatternPhiDetector    PresidioDetector ──HTTP──► Presidio / clinical
                  Detector        (regex + checks)      (DeidSharp.Presidio)        sidecar (Docker)
```

## Namespaces

| Namespace | Contents |
|---|---|
| `DeidSharp` | `PhiCategory` (the Safe Harbor identifier kinds), `PhiSpan` |
| `DeidSharp.Detection` | `IPhiDetector`, `IDeterministicPhiDetector`, `CompositePhiDetector`, `DeterministicDetectors` (the default deterministic stack), `SpanMerger`, `KnownIdentifiers` |
| `DeidSharp.Detection.Known` | `KnownIdentifierDetector` and the patterns it builds per identifier |
| `DeidSharp.Detection.Patterns` | `PatternPhiDetector`, the rule table `PatternLibrary`, `Checksums` (Luhn, VIN) |
| `DeidSharp.Text` | `TextDeidentifier`, `TagReplacement`, `SurrogateReplacement` (with its name and value generators), `TextFolding` |
| `DeidSharp.Dates` | `PartialDate`, `TextDateParser` |
| `DeidSharp.Structured` | `AgeGeneralizer`, `ZipCodeGeneralizer`, date strategies (`YearOnlyDateTransformer`, `ShiftingDateTransformer`) |
| `DeidSharp.Pseudonymization` | `PseudonymKey`, `HmacPseudonymizer`, `PseudonymFormats`, `DateShifter` |
| `DeidSharp.Verification` | `LeakScanner`, `LeakReport`, `ResidualPhiException` |
| `DeidSharp.Risk` | `ReidentificationRiskAssessor`, `RiskReport` |
| `DeidSharp.Evaluation` | `DetectionEvaluator`, `AnnotatedText`, `EvaluationReport` |
| `DeidSharp.Diagnostics` | `DeidSharpTelemetry`: the `DeidSharp` meter and activity source (detector durations by outcome, replacements by category, refused outputs), never carrying text |
| `DeidSharp.Fhir` (+ `.Rules`, `.References`, `.Known`, `.Verification`) | `FhirDeidentifier`, options, the rule chain, reference rewriting, identifier extraction, the FHIR output check |
| `DeidSharp.Gazetteer` | `GazetteerDetector`, `GazetteerOptions`, `Gazetteer` (the embedded lists; `Data/SOURCES.md` says where each comes from) |
| `DeidSharp.Llm` | `LlmPhiDetector`, `LlmDetectorOptions`, `LlmPrompt` (instructions and fictional examples) |
| `DeidSharp.Presidio` | `PresidioDetector`, `PresidioOptions`, `PresidioEntityMap`, DI registration |

## Free text

1. **Detect.** An `IPhiDetector` returns spans: `[Start, End)` in UTF-16 code units, a category, a score in [0, 1] and the detector's name. Spans never carry the text they cover, so they can be logged.
2. **Stack.** `CompositePhiDetector` runs several detectors in parallel and unions their spans (recall first: a span any detector reports is replaced). If any detector fails, the whole detection fails with `PhiDetectionException`. A stack with a missing layer is not the stack that was configured, so it never silently continues.
3. **Merge.** `SpanMerger` widens spans to whole characters (never half a surrogate pair, never without a trailing combining accent), then merges overlapping spans. A merged span keeps the category of its most confident part and every source. Touching spans stay separate.
4. **Replace.** An `IReplacementStrategy` writes the replacement. `TagReplacement` writes `[NAME]`, `[DATE]` and so on. `SurrogateReplacement` writes realistic fakes ("hiding in plain sight"): deterministic per key and subject, consistent across a name's parts, never equal to one of the subject's own identifiers, and drawn from reserved fictional ranges where they exist (555-01xx, example.org, 192.0.2.x, SSN area 000). Dates are rewritten by the configured date strategy (year only, or shifted).
5. **Verify.** By default `TextDeidentifier` searches the output for the subject's known identifiers and throws `ResidualPhiException` if one survived, for example because a stack without the known-identifier detector missed a bare surname.

Matching works on a folded copy of the text (`TextFolding`). Accented Latin letters become their base letters and typographic punctuation becomes ASCII, with **exactly one character for one character**, so an index in the folded copy is the same index in the original. The table is written out rather than computed with `string.Normalize`, which globalization-invariant mode does not fully support.

Known identifiers are searched for with one regular expression each, but a text is searched only for the identifiers that can appear in it. Each identifier has **anchors**, literals of which every match contains at least one: a word of a name, the rarest-looking word of a phrase, the characters of a phone number or record number with separators dropped, the year of a date as a whole number. One pass over the text finds every anchor (`AnchorIndex`), and only the searches whose anchors are present run. A bundle's thousands of identifiers then cost one scan per text plus the few searches whose anchors appear in it, rather than thousands of searches per text. The anchors are compared at least as loosely as the searches (case classes are read from the regex engine itself, not from the host's casing tables), so skipping a search never loses a match; a property test checks this against running every search.

## Structured data and FHIR

`FhirDeidentifier` works on a deep copy of the input:

1. **Extract known identifiers** from every structured field (names, contact points, addresses, identifiers, birth and death dates, organization and location names, maiden names) so free text can be searched for them.
2. **Resolve subjects up front.** `SubjectResolver` decides which patient each resource belongs to, before any reference is rewritten. Each patient gets one date shift and consistent surrogates.
3. **Walk** every element (`FhirElementWalker`) and offer it to the rule chain (Chain of Responsibility). The first rule that does not pass decides: keep, descend, remove or replace. An element no rule claims is descended into if it is complex and kept if it is a primitive; a kept primitive's own extensions are still offered to the rules. A complex element left empty after its children were processed is removed. Free text (strings, markdown, inline text attachments, display-only references) is queued rather than de-identified on the spot. A string or markdown value keeps its place and its element id, and its extensions go through the rules like any other; only its value is rewritten later. A rule that reads a string during the walk sees the original text.
4. **De-identify the queued free text**, up to `MaxFreeTextConcurrency` values at a time (default 8), and write each result back in the order queued. A bundle's notes then wait on a detector service a few at a time, not one after another. Any failure fails the run.
5. **Verify** the result (`FhirLeakVerifier`): structural checks plus a known-identifier scan of every string and text attachment. By default any finding throws `ResidualPhiException`.

The built-in rules, in order (`DefaultFhirRules.All`):

| Rule | Handles |
|---|---|
| `RemovedElementsRule` | A table of identifying elements: photos, patient contacts, device serial/lot/UDI, coverage subscriber id, organization and location names, coordinates, note authors written as names, binary data, `Meta.source`, bundle links and conditional headers |
| `RemovedTypesRule` | `HumanName`, `ContactPoint`, `Signature`, `Narrative`, wherever they appear |
| `ExtensionRule` | Keeps allow-listed extensions (US Core race, ethnicity, birth sex by default) and their sub-extensions; removes the rest and counts removed modifier extensions |
| `ResourceIdRule` | Resource ids, `Bundle.entry.fullUrl` and `request.url` become pseudonyms; contained ids and element ids are local and kept |
| `ReferenceRule` | References point at the target's pseudonym, with any server part replaced by a placeholder; displays are dropped; a display-only reference keeps its de-identified display; conditional references resolve to the bundle entry carrying that identifier |
| `IdentifierRule` | Removed (Safe Harbor) or keyed pseudonym scoped by system (pseudonymized) |
| `UriRule` | Keeps URIs that define (extension urls, profiles, a definitional resource's url); removes URIs that describe (`Device.url`, `Endpoint.address`) |
| `AddressRule` | Cut to state, country and three-digit ZIP |
| `AttachmentRule` | Inline UTF-8 `text/*` without control characters is de-identified; anything else is removed (with its `DocumentReference.content`) |
| `AgeRule` | `Age` values (any unit) and other quantities in years at or over the top-code age become "≥ 90 years" |
| `BirthDateRule` | Removes the birth date of anyone at or over the top-code age (at death, if recorded) |
| `DateRule` | Year only (Safe Harbor) or shifted by the patient's offset (pseudonymized); unreadable dates removed; instants removed under Safe Harbor |
| `CodedValueRule` | `Coding` and `Quantity` kept as clean copies without extensions: vocabulary, not free text |
| `FreeTextRule` | Every remaining string and markdown value goes through free-text de-identification |

**Extending it.** Add rules with `FhirDeidentificationOptions.AdditionalRules`; they run before the built-in ones. Add detectors by stacking them into `FreeTextDetector`. Allow more extensions with `AllowedExtensions`. None of these require changes to DeidSharp itself.

## Pseudonymization

`HmacPseudonymizer` computes HMAC-SHA256 over `domain ␟ value` with a secret `PseudonymKey` (at least 256 bits). A keyed hash cannot be reversed by hashing every possible MRN, and domains keep identical values in different roles unlinkable. `PseudonymFormats` shapes a digest into hex, a version-4-shaped UUID (valid as a FHIR id and in `urn:uuid:`), or an index.

`DateShifter` derives each subject's offset from the key: never zero, within ±`MaxDays`, the same for every date of that subject. `ShiftingDateTransformer` shifts partial dates from their midpoint and keeps their precision.

Under Safe Harbor, when no key is given, each run uses a fresh random key, so a run's pseudonyms link nothing outside it.

## Design principles

- **Fail closed.** A detector that cannot run throws; it never returns "no PHI". Unknown extensions, non-text attachments and unreadable dates are removed, not kept. An unknown Presidio entity type is still redacted.
- **Recall first.** Spans from every detector are unioned, and the default score threshold is 0. A redacted word costs less than a leaked name.
- **Never log a value.** Spans, findings, reports and exception messages carry positions and categories only. HTTP errors from a sidecar are reported by status code, never by body.
- **Deterministic guard.** The leak guard uses only in-process deterministic detectors, so the check after a model never shares that model's failure modes.
- **Immutable inputs.** `KnownIdentifiers` is immutable and safe to share across threads; `FhirDeidentifier` never modifies its input.
- **One job per class.** Facades (`TextDeidentifier`, `FhirDeidentifier`) only sequence steps; building a FHIR run's services is `FhirSessionFactory`'s job, FHIR JSON parsing is `FhirJson`'s, and each options record validates its own values. Variation is added as a row in a table (`PatternLibrary`, `RemovedElementsRule`, the `FhirLeakVerifier` checks, surrogate formats) or as a new strategy or rule, never as a new branch in existing code.
- **Library hygiene.** Every `await` in `src/` uses `ConfigureAwait(false)` (CA2007 fails the build otherwise), so a caller that blocks on a task cannot deadlock. The Presidio wire format uses source-generated JSON serialization: no reflection, safe to trim.
