# Threat model

What DeidSharp protects, against whom, what it guarantees, and what it leaves to you.

## What is protected

Identifiers of patients, their relatives, household members and employers, and of care staff, in structured health data (FHIR R4) and in free text (notes, comments, text attachments, model output).

## Who and what it defends against

| Threat | How it is handled |
|---|---|
| A recipient of de-identified data recognizing someone from a direct identifier | Structured identifiers are removed or generalized by rule; free text is searched by a stack of detectors; the output is verified before it is returned. |
| A recipient linking records to an outside dataset by quasi-identifiers (age, ZIP, dates, rare conditions) | Safe Harbor generalization (year-only dates, three-digit ZIP, 90+ ages). `ReidentificationRiskAssessor` measures k-anonymity, l-diversity and prosecutor risk of what remains. Choosing and generalizing further is your decision. |
| Reversing a pseudonym by hashing candidate values | Pseudonyms are keyed HMACs; without the key there is nothing to compare against. |
| A detector failing silently (a sidecar down, a timeout, a malformed answer) | Every failure throws `PhiDetectionException`. A failing detector never reads as "no PHI found", and the whole run stops. A language model's answer that isn't the JSON asked for fails the same way, and its message never quotes the answer. |
| A model or an NER service missing an identifier the record lists | The known-identifier detector finds the record's own identifiers deterministically, and the output check runs on every result. |
| An LLM writing an identifier into its output | `LeakScanner` scans every value of the output for the record's known identifiers and the pattern library's shapes. It does not check that the output is true: whether an LLM invented or altered a fact is for the caller to check against the record. |
| PHI leaking into logs, traces or exception messages | Spans, findings, reports and exceptions carry positions and categories only. Sidecar error bodies are never read into messages. The FHIR parser's errors, which quote the input, are replaced by a count (`FhirInputException`). JSON keys outside a caller's schema are masked in finding paths (`LeakScanOptions.PathPropertyNames`). |
| PHI leaving the machine through an NER service or a language model | The clinical sidecar runs offline with its model baked in; Presidio sidecars run locally. `LlmPhiDetector` sends every text to the `IChatClient` it is given, so it is meant for a model on the same host or a private network (Ollama, vLLM); DeidSharp never supplies a cloud client, and the evaluation tool refuses an LLM endpoint that isn't on the machine. Ollama has no authentication, so its server must be reachable only from the program that uses it. Cloud de-identification services are not used by default. |

## What DeidSharp guarantees

These hold by construction and are covered by tests:

- In structured FHIR output under Safe Harbor: no `HumanName`, `ContactPoint`, `Signature`, `Narrative`, `Identifier` or `instant` remains; no date is finer than a year; addresses hold at most state, country and a three-digit ZIP; only allow-listed extensions remain, at any depth (inside codings and quantities and on primitive values too); only defining URIs remain, and references and full URLs carry no server part but the placeholder; only readable UTF-8 text attachments remain. `FhirLeakVerifier` checks all of these on every run.
- No string or markdown value in FHIR output, no text attachment, and no text returned by `TextDeidentifier` contains one of the record's known identifiers (with the default verification on), in any form the known-identifier detector recognizes.
- A detector failure stops the run.
- The input resource is never modified.

## What DeidSharp does not guarantee

- **Perfect free-text recall.** No detector stack finds every identifier. Names the record does not list rely on NER, which misses some. Recall varies with the text, so measure it on your own data.
- **Protection from quasi-identifiers.** Safe Harbor output can still single out a person when rare values combine. Measure the risk; generalize or suppress further where needed.
- **Images, audio and other binary content.** These are removed, never inspected. Faces in a photo or a voice in a recording are not detected.
- **Correct handling of identifiers written in unusual places,** such as names in `Coding.display`. See the known gaps in [safe-harbor.md](safe-harbor.md#known-gaps).
- **Certification.** DeidSharp has not been through an expert determination or an external audit.

## Using it safely

1. **Stack detectors.** For real notes, use NER (the clinical sidecar) together with the known-identifier and pattern detectors. Do not use NER alone.
2. **Pass what the record knows.** Give `TextDeidentifier` the subject's `KnownIdentifiers`. `FhirDeidentifier` extracts them itself.
3. **Keep verification on.** Treat a `ResidualPhiException` as a stop: inspect the record (by position, not by logging the value) and fix the rules or detectors.
4. **Measure recall before use.** Annotate a sample of your own notes and run `DetectionEvaluator`. Look at missed spans by category, not just the overall number.
5. **Review a sample by hand.** Have a person read a random sample of output before release, every time the data source, the detectors or DeidSharp changes.
6. **Assess re-identification risk** of datasets before release with `ReidentificationRiskAssessor`, using the quasi-identifiers a recipient could know.
7. **Guard the key.** In pseudonymized mode the `PseudonymKey` re-identifies the data. Store it in a secret store and never ship it with the data.
8. **Keep sidecars and models private.** The analyzer services and a language model behind `LlmPhiDetector` receive raw text. Run them on the same host or a private network, never exposed publicly. The bundled `docker-compose.yml` publishes every service on 127.0.0.1 only.
9. **Govern the use.** Ethics approval and a data use agreement come before real data, whatever the software does.
