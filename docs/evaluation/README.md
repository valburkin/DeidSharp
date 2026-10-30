# Evaluation

How well DeidSharp's detector stacks find PHI in annotated text, measured with `tools/DeidSharp.Evaluation.Cli`. What the measurements taught us, and the decisions they led to, are in [findings](findings.md). The numbers below are generated from [results.json](results.json); the README's badge comes from [badge.json](badge.json). [REPRODUCING.md](../../REPRODUCING.md) gives the environment and the commands to rerun any of them. Here, **the nursing notes** are PhysioNet's Deidentified Medical Text and **the discharge summaries** the `mimic-iv-note` corpus built from MIMIC-IV-Note ([corpora](#corpora)).

The badge shows recall on the held-out half of the nursing notes, rounded down: the recall-first stack with a local language model beside the recommended stack without one (the deterministic detectors with the clinical sidecar), or the latter alone while no stack with a model is published.

## Results at a glance

The reporting half of the nursing notes: 1,307 ICU notes, 889 PHI spans, that no choice was tuned on. Measured at version 1.1.0 under a protocol fixed beforehand (D6), with every tool scored by the same scorer in one run per corpus; it superseded a first pass whose model baselines ran on a CPU build ([findings](findings.md), D6's amendment and F46).

| Stack | Recall (95% CI) | Missed | Character recall | Precision | Other text removed |
|---|---:|---:|---:|---:|---:|
| Recall first: deterministic + clinical + LLM | 98.88% (97.91–99.58) | 10 | 99.00% | 54.31% | 0.57% |
| Balanced: the same + second opinion | 98.20% (96.86–99.24) | 16 | 98.84% | 70.53% | 0.26% |
| Balanced, one call: deterministic + clinical places + LLM | 97.53% (95.95–98.74) | 22 | 98.29% | 72.03% | 0.24% |
| Precision first: deterministic + LLM | 96.51% (94.92–97.93) | 31 | 97.59% | 83.18% | 0.15% |
| No LLM: deterministic + clinical | 88.30% (84.71–91.11) | 104 | 86.38% | 43.02% | 0.85% |
| deid, with the lists built for this corpus | 97.30% (95.73–98.61) | 24 | 97.73% | 72.57% | 0.18% |
| deid without its corpus aids | 96.06% (94.16–97.57) | 35 | 96.41% | 72.39% | 0.17% |
| Stanford/Penn de-identifier | 96.06% (94.56–97.27) | 35 | 96.29% | 63.56% | 0.33% |
| Philter | 91.45% (88.99–93.71) | 76 | 88.00% | 6.71% | 7.81% |

The LLM is `gemma4:26b-a4b` through Ollama, with `LlmPrompt.WithPoolExamples`. "Deterministic" is known identifiers and patterns, plus the gazetteer in the stack without an LLM only (D3). The Stanford/Penn de-identifier is the strongest of the three published models compared (with OpenAI Privacy Filter and GLiNER multi-PII, F44). Paired on the same resamples:

- **Against Stanford/Penn**, as stated beforehand: the recall-first stack finds more (+2.81 points, +1.38 to +4.37) at 9.3 points less precision, and the precision-first stack's recall is within the 2-point margin stated beforehand (+0.45, −1.45 to +2.54), at 19.6 points more precision.
- **No configuration finds significantly more PHI than deid with every list built for this corpus.** The recall-first stack misses 10 spans against deid's 24, but its paired interval (+1.57 points, 0.00 to +3.27) starts at zero. Against deid without its corpus aids, the recall-first and balanced stacks find more (+2.81 and +2.14).
- **The precision-first stack's recall doesn't differ significantly from deid's** (−0.79, −2.80 to +1.27), at 10.6 points more precision.
- **Without a language model**, DeidSharp trails deid by 9 points of recall.

On the discharge summaries' reporting half (494 MIMIC-IV-Note discharge summaries from another decade, with surrogate PHI), every recommended stack with the LLM finds 94.0% to 95.3% of the PHI, more than deid in any configuration (91.5% to 92.1%) at its precision or better, and 20.2 to 21.5 points more than Stanford/Penn (73.8%), nearly all of it on the untyped names, which it mostly misses (on the typed labels both find 99% or more). Much of the lead over deid is the generator's made-up hospitals and cities, which deid's lists rarely match: without them it shrinks to about 1.5 points for the recall-first stack and 0.2 for the precision-first one. Philter touches more spans (99.4%) at 23 to 27 points less precision (F41, F46).

On ASQ-PHI (1,051 synthetic clinical queries, a second check with no held-out half), every recommended stack removes 99.6% to 99.7% of the PHI, at 95% to 99.6% precision, as does Stanford/Penn (99.7% at 97.7%) (F39, F46).

The generated tables under [Results](#results) hold every stack and tool of that run, the stacks with a language model pinned by the model's digest and Ollama's version as the sidecars are by their images.

## Tuning and reporting halves

Changing a detector after reading its misses fits it to the notes that were read, rules as much as models. So the nursing notes and the discharge summaries are each split by patient into two halves: `--split tune` is the half to read misses and false positives on and change detectors against, and `--split report` is the half published numbers come from. Nobody reads the report half's review files. A patient's notes all fall on one side, chosen by a hash of the patient's number that never changes. `--split all` reads both halves, for comparisons with earlier runs.

These are measurements on particular corpora, not a guarantee for your data. Free-text detection is never perfect: measure on a sample of your own notes before relying on any stack ([testing](../testing.md#measuring-recall-on-your-own-data)).

## Corpora

| Corpus | What it is | Access | When it runs |
|---|---|---|---|
| [PhysioNet Deidentified Medical Text](https://physionet.org/content/deidentifiedmedicaltext/1.0/) (`physionet-deid`) | 2,434 ICU nursing notes with their PHI replaced by realistic surrogates; gold spans from the open [deid package](https://physionet.org/content/deid/) | Credentialed PhysioNet account and the dataset's data use agreement; never in this repository | By a maintainer, before each release |
| [MIMIC-IV-Note](https://physionet.org/content/mimic-iv-note/2.2/) (`mimic-iv-note`) | 1,000 discharge summaries (one each from 1,000 patients, a fixed hashed sample), whose `___` placeholders are filled with made-up surrogates that become the gold spans | Credentialed PhysioNet account and the dataset's data use agreement; never in this repository | By a maintainer, for published results |
| [ASQ-PHI](https://github.com/JamesWeatherhead/asq-phi) (`asq-phi`) | 1,051 synthetic clinical search queries, 832 with PHI and 219 hard negatives without | MIT; a copy ships in `tools/DeidSharp.Evaluation.Cli/data/asq-phi` | Every CI build (the deterministic stack's floor); published before each release |

When you publish results on the nursing notes, cite the dataset, the deid paper (its gold standard and baseline), PhysioNet, and MIMIC-III, which the dataset's page lists as its parent project, as PhysioNet asks:

- Douglass M, Long B, Moody G, Szolovits P, Lehman L, Mark R, Clifford GD. Deidentified Medical Text (version 1.0). PhysioNet; 2007. https://doi.org/10.13026/jc2a-ca12
- Neamatullah I, Douglass M, Lehman LH, Reisner A, Villarroel M, Long WJ, Szolovits P, Moody GB, Mark RG, Clifford GD. Automated de-identification of free-text medical records. BMC Medical Informatics and Decision Making. 2008;8:32. https://doi.org/10.1186/1472-6947-8-32 (the deid software, version 1.1: https://doi.org/10.13026/C20M3F)
- Pollard T, Moody BE, Lehman L, Gow B, Fernandes C, Xie C, Johnson A, Mark RG, Heldt T. PhysioNet as a global platform for biomedical research. Nature Health. 2026. https://doi.org/10.1038/s44360-026-00096-z
- Johnson A, Pollard T, Mark R. MIMIC-III Clinical Database (version 1.4). PhysioNet; 2016. https://doi.org/10.13026/C2XW26
- Johnson AEW, Pollard TJ, Shen L, Lehman LH, Feng M, Ghassemi M, Moody B, Szolovits P, Celi LA, Mark RG. MIMIC-III, a freely accessible critical care database. Scientific Data. 2016;3:160035. https://doi.org/10.1038/sdata.2016.35

### MIMIC-IV-Note with surrogates

MIMIC-IV-Note's de-identification replaced each piece of PHI with `___` and kept no annotation of what it was. The `mimic-iv-note` corpus turns those placeholders back into PHI, as "hiding in plain sight" corpora are built (Carrell et al. 2013, in the [findings' references](findings.md#references)): a table of context rules (`Corpora/Surrogates/PlaceholderRules.cs`) decides what each placeholder stood for ("Dr. ___" a clinician's surname, "___ 07:15AM" a date, "Followup Instructions: ___" a section of appointments), and a made-up identity per note fills it, so the patient keeps one name and age and the dates fall around one admission. Each surrogate becomes a gold span. A placeholder no rule recognizes gets a first name or a surname: a maintainer's reading of a sample of the tuning half found nothing else among them. Names are drawn from the Census lists in proportion to how many people carry them ([sources](../../tools/DeidSharp.Evaluation.Cli/data/surrogates/SOURCES.md)); since the gazetteer is built from the same lists, its recall on this corpus is optimistic.

The sample takes the 1,000 patients with the smallest SHA-256 of `deidsharp-mimic-patient:` and their number, and each one's note with the smallest SHA-256 of `deidsharp-mimic-note:` and its id; the surrogates come from a generator seeded by the note's id. The file is checked against the release's SHA-256 first, and the surrogate lists against the hashes the generator was built with, so anyone with access rebuilds the same corpus. The corpus title carries the generator's version; a test pins the generator's output on a fictional note, so a change to the rules, the lists or the drawing fails it until the version changes. Ages under 90 are gold spans outside the Safe Harbor scope, as in the other corpora. The rules and lists were developed on the tuning half only.

Cite the dataset and PhysioNet (above), as PhysioNet asks, and MIMIC-IV, the database the notes belong to:

- Johnson A, Pollard T, Horng S, Celi LA, Mark R. MIMIC-IV-Note: Deidentified free-text clinical notes (version 2.2). PhysioNet; 2023. https://doi.org/10.13026/1n74-ne17
- Johnson AEW, Bulgarelli L, Shen L, et al. MIMIC-IV, a freely accessible electronic health record dataset. Scientific Data. 2023;10:1. https://doi.org/10.1038/s41597-022-01899-x

The clinical sidecar's model (`obi/deid_roberta_i2b2`) was trained on the i2b2 2014 corpus, so no corpus here is one it has seen.

## Results

<!-- results:begin (generated by DeidSharp.Evaluation.Cli from results.json; do not edit) -->

### ASQ-PHI (synthetic clinical queries)

1,051 documents · 2,976 gold spans · scope `safe-harbor` · split `all` · DeidSharp 1.1.0 (`2059384`) · 2026-10-30 · LLM gemma4:26b-a4b (`001e5dafc3c7`) on Ollama 0.40.11 · clinical sidecar on CPU without spaCy NER, URL and license recognizers (f828f91086f5); model baselines from deidsharp/baseline-models:1 (1c5fa7724ddb) on an NVIDIA RTX 5080

| Stack | Recall (95% CI) | Character recall | Precision | Word F1 | Non-PHI removed | Missed |
|---|---:|---:|---:|---:|---:|---:|
| deterministic | 86.62% (85.46–87.79) | 70.69% | 97.68% | 83.20% | 0.41% | 398 |
| deterministic + llm gemma4:26b-a4b (all-examples) | 99.63% (99.39–99.84) | 97.68% | 97.89% | 98.30% | 0.47% | 11 |
| deterministic + llm gemma4:26b-a4b (all-examples) + second opinion | 99.63% (99.39–99.84) | 97.68% | 99.08% | 98.58% | 0.29% | 11 |
| deterministic + clinical | 99.73% (99.53–99.91) | 96.38% | 95.29% | 97.69% | 0.87% | 8 |
| deterministic + clinical + llm gemma4:26b-a4b (all-examples) | 99.73% (99.53–99.91) | 97.86% | 95.19% | 97.74% | 0.87% | 8 |
| deterministic + clinical + llm gemma4:26b-a4b (all-examples) + second opinion | 99.66% (99.45–99.87) | 97.83% | 98.69% | 98.49% | 0.37% | 10 |
| deterministic + clinical places | 94.22% (93.38–94.98) | 91.20% | 97.55% | 93.33% | 0.50% | 172 |
| deterministic + clinical places + llm gemma4:26b-a4b (all-examples) | 99.66% (99.45–99.87) | 97.79% | 97.58% | 98.26% | 0.52% | 10 |
| deterministic + clinical places + llm gemma4:26b-a4b (all-examples) + second opinion | 99.66% (99.45–99.87) | 97.79% | 98.72% | 98.52% | 0.34% | 10 |
| deterministic without gazetteer | 47.58% (46.61–48.55) | 45.07% | 99.71% | 65.73% | 0.05% | 1,560 |
| deterministic without gazetteer + llm gemma4:26b-a4b (all-examples) | 99.63% (99.39–99.84) | 97.68% | 99.57% | 98.68% | 0.14% | 11 |
| deterministic without gazetteer + llm gemma4:26b-a4b (all-examples) + second opinion | 99.63% (99.39–99.84) | 97.68% | 99.57% | 98.68% | 0.14% | 11 |
| deterministic without gazetteer + clinical | 99.73% (99.53–99.91) | 95.95% | 96.81% | 98.03% | 0.57% | 8 |
| deterministic without gazetteer + clinical + llm gemma4:26b-a4b (all-examples) | 99.73% (99.53–99.91) | 97.86% | 96.64% | 98.08% | 0.57% | 8 |
| deterministic without gazetteer + clinical + llm gemma4:26b-a4b (all-examples) + second opinion | 99.66% (99.45–99.87) | 97.83% | 99.11% | 98.58% | 0.25% | 10 |
| deterministic without gazetteer + clinical places | 74.29% (73.44–75.13) | 78.81% | 98.96% | 87.07% | 0.21% | 765 |
| deterministic without gazetteer + clinical places + llm gemma4:26b-a4b (all-examples) | 99.66% (99.45–99.87) | 97.79% | 99.11% | 98.60% | 0.22% | 10 |
| deterministic without gazetteer + clinical places + llm gemma4:26b-a4b (all-examples) + second opinion | 99.66% (99.45–99.87) | 97.79% | 99.11% | 98.60% | 0.22% | 10 |
| deid | 85.85% (84.82–86.99) | 68.44% | 94.68% | 80.58% | 1.52% | 421 |
| philter | 96.74% (96.12–97.35) | 70.67% | 93.55% | 89.19% | 2.18% | 97 |
| stanford-deid | 99.66% (99.44–99.87) | 98.22% | 97.70% | 98.32% | 0.56% | 10 |
| privacy-filter | 70.19% (68.95–71.40) | 64.57% | 96.65% | 80.20% | 1.60% | 887 |
| gliner-pii | 99.22% (98.89–99.53) | 99.00% | 70.73% | 81.42% | 13.26% | 23 |

<details><summary>Recall by corpus label</summary>

| Label | Gold spans | deterministic | deterministic + llm gemma4:26b-a4b (all-examples) | deterministic + llm gemma4:26b-a4b (all-examples) + second opinion | deterministic + clinical | deterministic + clinical + llm gemma4:26b-a4b (all-examples) | deterministic + clinical + llm gemma4:26b-a4b (all-examples) + second opinion | deterministic + clinical places | deterministic + clinical places + llm gemma4:26b-a4b (all-examples) | deterministic + clinical places + llm gemma4:26b-a4b (all-examples) + second opinion | deterministic without gazetteer | deterministic without gazetteer + llm gemma4:26b-a4b (all-examples) | deterministic without gazetteer + llm gemma4:26b-a4b (all-examples) + second opinion | deterministic without gazetteer + clinical | deterministic without gazetteer + clinical + llm gemma4:26b-a4b (all-examples) | deterministic without gazetteer + clinical + llm gemma4:26b-a4b (all-examples) + second opinion | deterministic without gazetteer + clinical places | deterministic without gazetteer + clinical places + llm gemma4:26b-a4b (all-examples) | deterministic without gazetteer + clinical places + llm gemma4:26b-a4b (all-examples) + second opinion | deid | philter | stanford-deid | privacy-filter | gliner-pii |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| GEOGRAPHIC_LOCATION | 829 | 73.46% | 99.87% | 99.87% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 4.94% | 99.87% | 99.87% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 82.38% | 90.83% | 99.75% | 13.63% | 98.91% |
| NAME | 814 | 87.83% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 87.96% | 100.00% | 100.00% | 14.86% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 15.11% | 100.00% | 100.00% | 99.14% | 99.63% | 100.00% | 94.96% | 100.00% |
| DATE | 806 | 98.51% | 98.88% | 98.88% | 99.13% | 99.13% | 98.88% | 98.51% | 98.88% | 98.88% | 98.51% | 98.88% | 98.88% | 99.13% | 99.13% | 98.88% | 98.51% | 98.88% | 98.88% | 98.63% | 98.88% | 99.13% | 95.53% | 99.87% |
| MEDICAL_RECORD_NUMBER | 305 | 90.49% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 92.13% | 100.00% | 100.00% | 90.49% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 92.13% | 100.00% | 100.00% | 47.21% | 97.70% | 100.00% | 75.08% | 98.68% |
| HEALTH_PLAN_BENEFICIARY_NUMBER | 91 | 65.93% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 65.93% | 100.00% | 100.00% | 65.93% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 65.93% | 100.00% | 100.00% | 13.18% | 98.90% | 100.00% | 89.01% | 94.50% |
| PHONE_NUMBER | 45 | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 97.77% | 100.00% |
| SOCIAL_SECURITY_NUMBER | 33 | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 96.96% | 100.00% |
| EMAIL_ADDRESS | 31 | 96.77% | 96.77% | 96.77% | 96.77% | 96.77% | 96.77% | 96.77% | 96.77% | 96.77% | 96.77% | 96.77% | 96.77% | 96.77% | 96.77% | 96.77% | 96.77% | 96.77% | 96.77% | 96.77% | 96.77% | 96.77% | 96.77% | 100.00% |
| UNIQUE_IDENTIFIER | 14 | 57.14% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 57.14% | 100.00% | 100.00% | 57.14% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 57.14% | 100.00% | 100.00% | 21.42% | 100.00% | 100.00% | 71.42% | 71.42% |
| ACCOUNT_NUMBER | 4 | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 25.00% | 100.00% | 100.00% | 100.00% | 100.00% |
| FAX_NUMBER | 2 | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 50.00% | 100.00% |
| CERTIFICATE_LICENSE_NUMBER | 1 | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 0.00% | 100.00% | 100.00% | 100.00% | 100.00% |
| IP_ADDRESS | 1 | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 0.00% | 100.00% | 100.00% | 100.00% | 100.00% |

</details>

### PhysioNet Deidentified Medical Text 1.0

1,307 documents · 889 gold spans · scope `safe-harbor` · split `report` · DeidSharp 1.1.0 (`2059384`) · 2026-10-30 · LLM gemma4:26b-a4b (`001e5dafc3c7`) on Ollama 0.40.11 · clinical sidecar on CPU without spaCy NER, URL and license recognizers (f828f91086f5); model baselines from deidsharp/baseline-models:1 (1c5fa7724ddb) on an NVIDIA RTX 5080

| Stack | Recall (95% CI) | Character recall | Precision | Word F1 | Non-PHI removed | Missed |
|---|---:|---:|---:|---:|---:|---:|
| deterministic | 42.06% (37.23–46.29) | 48.40% | 43.77% | 36.98% | 0.41% | 515 |
| deterministic + llm gemma4:26b-a4b (all-examples) | 96.96% (95.42–98.24) | 98.03% | 59.22% | 69.20% | 0.49% | 27 |
| deterministic + llm gemma4:26b-a4b (all-examples) + second opinion | 96.85% (95.23–98.11) | 97.93% | 74.05% | 81.24% | 0.24% | 28 |
| deterministic + clinical | 88.30% (84.70–91.12) | 86.37% | 43.01% | 51.15% | 0.85% | 104 |
| deterministic + clinical + llm gemma4:26b-a4b (all-examples) | 98.87% (97.90–99.59) | 98.99% | 43.18% | 53.54% | 0.91% | 10 |
| deterministic + clinical + llm gemma4:26b-a4b (all-examples) + second opinion | 98.20% (96.86–99.24) | 98.83% | 62.79% | 75.54% | 0.38% | 16 |
| deterministic + clinical places | 50.50% (45.50–55.09) | 56.14% | 40.36% | 39.30% | 0.52% | 440 |
| deterministic + clinical places + llm gemma4:26b-a4b (all-examples) | 97.63% (96.10–98.84) | 98.39% | 53.49% | 66.44% | 0.58% | 21 |
| deterministic + clinical places + llm gemma4:26b-a4b (all-examples) + second opinion | 97.52% (95.94–98.75) | 98.29% | 65.15% | 77.27% | 0.33% | 22 |
| deterministic without gazetteer | 24.40% (20.53–28.39) | 29.96% | 80.48% | 36.49% | 0.07% | 672 |
| deterministic without gazetteer + llm gemma4:26b-a4b (all-examples) | 96.51% (94.91–97.93) | 97.59% | 83.18% | 86.01% | 0.15% | 31 |
| deterministic without gazetteer + llm gemma4:26b-a4b (all-examples) + second opinion | 96.51% (94.91–97.93) | 97.59% | 83.18% | 86.01% | 0.15% | 31 |
| deterministic without gazetteer + clinical | 86.16% (82.43–89.06) | 84.19% | 54.97% | 60.12% | 0.52% | 123 |
| deterministic without gazetteer + clinical + llm gemma4:26b-a4b (all-examples) | 98.87% (97.90–99.59) | 98.99% | 54.31% | 62.91% | 0.57% | 10 |
| deterministic without gazetteer + clinical + llm gemma4:26b-a4b (all-examples) + second opinion | 98.20% (96.86–99.24) | 98.83% | 70.52% | 80.77% | 0.27% | 16 |
| deterministic without gazetteer + clinical places | 39.48% (34.42–44.52) | 44.95% | 60.44% | 44.50% | 0.18% | 538 |
| deterministic without gazetteer + clinical places + llm gemma4:26b-a4b (all-examples) | 97.52% (95.94–98.75) | 98.29% | 72.03% | 81.74% | 0.24% | 22 |
| deterministic without gazetteer + clinical places + llm gemma4:26b-a4b (all-examples) + second opinion | 97.52% (95.94–98.75) | 98.29% | 72.03% | 81.74% | 0.24% | 22 |
| deid | 97.30% (95.73–98.62) | 97.73% | 72.57% | 82.26% | 0.18% | 24 |
| deid-generic | 96.06% (94.15–97.57) | 96.40% | 72.38% | 82.74% | 0.18% | 35 |
| deid-nohosp | 84.70% (80.11–88.88) | 83.16% | 70.37% | 78.33% | 0.17% | 136 |
| philter | 91.45% (88.98–93.71) | 88.00% | 6.70% | 12.50% | 7.82% | 76 |
| stanford-deid | 96.06% (94.56–97.27) | 96.28% | 63.56% | 73.89% | 0.33% | 35 |
| privacy-filter | 32.84% (26.16–38.80) | 36.34% | 59.15% | 34.09% | 0.19% | 597 |
| gliner-pii | 76.82% (71.35–81.32) | 76.60% | 11.07% | 22.02% | 2.49% | 206 |

<details><summary>Recall by corpus label</summary>

| Label | Gold spans | deterministic | deterministic + llm gemma4:26b-a4b (all-examples) | deterministic + llm gemma4:26b-a4b (all-examples) + second opinion | deterministic + clinical | deterministic + clinical + llm gemma4:26b-a4b (all-examples) | deterministic + clinical + llm gemma4:26b-a4b (all-examples) + second opinion | deterministic + clinical places | deterministic + clinical places + llm gemma4:26b-a4b (all-examples) | deterministic + clinical places + llm gemma4:26b-a4b (all-examples) + second opinion | deterministic without gazetteer | deterministic without gazetteer + llm gemma4:26b-a4b (all-examples) | deterministic without gazetteer + llm gemma4:26b-a4b (all-examples) + second opinion | deterministic without gazetteer + clinical | deterministic without gazetteer + clinical + llm gemma4:26b-a4b (all-examples) | deterministic without gazetteer + clinical + llm gemma4:26b-a4b (all-examples) + second opinion | deterministic without gazetteer + clinical places | deterministic without gazetteer + clinical places + llm gemma4:26b-a4b (all-examples) | deterministic without gazetteer + clinical places + llm gemma4:26b-a4b (all-examples) + second opinion | deid | deid-generic | deid-nohosp | philter | stanford-deid | privacy-filter | gliner-pii |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| HCPName | 292 | 60.61% | 97.26% | 97.26% | 89.04% | 98.63% | 97.26% | 61.98% | 97.26% | 97.26% | 41.43% | 97.26% | 97.26% | 85.95% | 98.63% | 97.26% | 43.15% | 97.26% | 97.26% | 100.00% | 97.94% | 97.94% | 96.91% | 97.26% | 53.08% | 90.41% |
| Date | 254 | 12.59% | 98.81% | 98.81% | 91.73% | 99.60% | 98.81% | 12.59% | 98.81% | 98.81% | 12.59% | 98.81% | 98.81% | 91.73% | 99.60% | 98.81% | 12.59% | 98.81% | 98.81% | 94.88% | 94.88% | 94.88% | 95.66% | 96.06% | 9.84% | 65.35% |
| Location | 177 | 36.72% | 94.91% | 94.91% | 76.83% | 98.30% | 98.30% | 76.83% | 98.30% | 98.30% | 1.69% | 93.22% | 93.22% | 74.57% | 98.30% | 98.30% | 74.57% | 98.30% | 98.30% | 96.61% | 94.35% | 37.28% | 74.01% | 95.48% | 5.08% | 63.27% |
| RelativeProxyName | 92 | 54.34% | 94.56% | 93.47% | 90.21% | 98.91% | 98.91% | 54.34% | 94.56% | 93.47% | 17.39% | 93.47% | 93.47% | 83.69% | 98.91% | 98.91% | 17.39% | 93.47% | 93.47% | 95.65% | 95.65% | 95.65% | 93.47% | 91.30% | 58.69% | 91.30% |
| PTName | 37 | 78.37% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 78.37% | 100.00% | 100.00% | 64.86% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 64.86% | 100.00% | 100.00% | 100.00% | 97.29% | 97.29% | 100.00% | 100.00% | 91.89% | 100.00% |
| Phone | 35 | 60.00% | 97.14% | 97.14% | 100.00% | 100.00% | 100.00% | 60.00% | 97.14% | 97.14% | 60.00% | 97.14% | 97.14% | 100.00% | 100.00% | 100.00% | 60.00% | 97.14% | 97.14% | 100.00% | 100.00% | 100.00% | 91.42% | 97.14% | 42.85% | 54.28% |
| Other | 1 | 0.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 0.00% | 100.00% | 100.00% | 0.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 0.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 0.00% | 0.00% |
| PTNameInitial | 1 | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 100.00% | 0.00% | 100.00% |

</details>

### MIMIC-IV-Note 2.2 discharge summaries with surrogate PHI (generator 3)

494 documents · 29,023 gold spans · scope `safe-harbor` · split `report` · DeidSharp 1.1.0 (`2059384`) · 2026-10-30 · LLM gemma4:26b-a4b (`001e5dafc3c7`) on Ollama 0.40.11 · clinical sidecar on CPU without spaCy NER, URL and license recognizers (f828f91086f5); model baselines from deidsharp/baseline-models:1 (1c5fa7724ddb) on an NVIDIA RTX 5080

| Stack | Recall (95% CI) | Character recall | Precision | Word F1 | Non-PHI removed | Missed |
|---|---:|---:|---:|---:|---:|---:|
| deterministic | 87.07% (86.53–87.62) | 82.48% | 98.19% | 91.08% | 0.24% | 3,752 |
| deterministic + llm gemma4:26b-a4b (all-examples) | 97.47% (97.26–97.68) | 98.26% | 97.93% | 91.91% | 0.80% | 733 |
| deterministic + llm gemma4:26b-a4b (all-examples) + second opinion | 96.08% (95.82–96.34) | 97.38% | 98.89% | 91.83% | 0.76% | 1,136 |
| deterministic + clinical | 91.57% (91.16–91.99) | 94.21% | 94.75% | 93.94% | 0.42% | 2,446 |
| deterministic + clinical + llm gemma4:26b-a4b (all-examples) | 97.81% (97.62–98.01) | 98.67% | 94.74% | 90.59% | 0.96% | 634 |
| deterministic + clinical + llm gemma4:26b-a4b (all-examples) + second opinion | 96.71% (96.46–96.96) | 97.97% | 96.26% | 90.92% | 0.89% | 952 |
| deterministic + clinical places | 88.26% (87.76–88.79) | 92.09% | 95.58% | 93.58% | 0.34% | 3,407 |
| deterministic + clinical places + llm gemma4:26b-a4b (all-examples) | 97.58% (97.37–97.78) | 98.51% | 95.59% | 91.31% | 0.89% | 702 |
| deterministic + clinical places + llm gemma4:26b-a4b (all-examples) + second opinion | 96.29% (96.02–96.54) | 97.70% | 96.48% | 91.26% | 0.85% | 1,074 |
| deterministic without gazetteer | 48.89% (47.75–50.00) | 53.20% | 99.56% | 77.93% | 0.06% | 14,831 |
| deterministic without gazetteer + llm gemma4:26b-a4b (all-examples) | 93.99% (93.62–94.38) | 96.00% | 99.28% | 92.29% | 0.62% | 1,742 |
| deterministic without gazetteer + llm gemma4:26b-a4b (all-examples) + second opinion | 93.99% (93.62–94.38) | 96.00% | 99.28% | 92.29% | 0.62% | 1,742 |
| deterministic without gazetteer + clinical | 71.63% (70.68–72.56) | 80.75% | 95.18% | 89.63% | 0.24% | 8,232 |
| deterministic without gazetteer + clinical + llm gemma4:26b-a4b (all-examples) | 95.27% (94.95–95.60) | 97.08% | 95.93% | 91.19% | 0.79% | 1,371 |
| deterministic without gazetteer + clinical + llm gemma4:26b-a4b (all-examples) + second opinion | 94.91% (94.58–95.24) | 96.85% | 96.70% | 91.52% | 0.75% | 1,476 |
| deterministic without gazetteer + clinical places | 60.40% (59.23–61.49) | 72.44% | 95.77% | 86.21% | 0.16% | 11,493 |
| deterministic without gazetteer + clinical places + llm gemma4:26b-a4b (all-examples) | 94.54% (94.18–94.91) | 96.61% | 96.81% | 91.81% | 0.71% | 1,582 |
| deterministic without gazetteer + clinical places + llm gemma4:26b-a4b (all-examples) + second opinion | 94.54% (94.18–94.91) | 96.61% | 96.81% | 91.81% | 0.71% | 1,582 |
| deid | 92.07% (91.66–92.47) | 84.32% | 95.78% | 92.73% | 0.28% | 2,299 |
| deid-generic | 91.73% (91.29–92.13) | 84.12% | 95.80% | 92.64% | 0.28% | 2,399 |
| deid-nohosp | 91.49% (91.07–91.91) | 83.10% | 95.77% | 92.28% | 0.28% | 2,468 |
| philter | 99.41% (99.30–99.52) | 83.34% | 72.45% | 82.28% | 2.45% | 171 |
| stanford-deid | 73.78% (72.81–74.75) | 82.10% | 98.32% | 89.90% | 0.25% | 7,608 |
| privacy-filter | 28.80% (27.71–29.85) | 29.77% | 96.36% | 44.54% | 0.24% | 20,664 |
| gliner-pii | 61.70% (60.82–62.55) | 66.53% | 56.71% | 60.06% | 2.53% | 11,115 |

<details><summary>Recall by corpus label</summary>

| Label | Gold spans | deterministic | deterministic + llm gemma4:26b-a4b (all-examples) | deterministic + llm gemma4:26b-a4b (all-examples) + second opinion | deterministic + clinical | deterministic + clinical + llm gemma4:26b-a4b (all-examples) | deterministic + clinical + llm gemma4:26b-a4b (all-examples) + second opinion | deterministic + clinical places | deterministic + clinical places + llm gemma4:26b-a4b (all-examples) | deterministic + clinical places + llm gemma4:26b-a4b (all-examples) + second opinion | deterministic without gazetteer | deterministic without gazetteer + llm gemma4:26b-a4b (all-examples) | deterministic without gazetteer + llm gemma4:26b-a4b (all-examples) + second opinion | deterministic without gazetteer + clinical | deterministic without gazetteer + clinical + llm gemma4:26b-a4b (all-examples) | deterministic without gazetteer + clinical + llm gemma4:26b-a4b (all-examples) + second opinion | deterministic without gazetteer + clinical places | deterministic without gazetteer + clinical places + llm gemma4:26b-a4b (all-examples) | deterministic without gazetteer + clinical places + llm gemma4:26b-a4b (all-examples) + second opinion | deid | deid-generic | deid-nohosp | philter | stanford-deid | privacy-filter | gliner-pii |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| UntypedName | 12,632 | 73.29% | 94.29% | 91.10% | 80.69% | 95.02% | 92.54% | 75.90% | 94.53% | 91.59% | 1.32% | 86.58% | 86.58% | 35.05% | 89.19% | 88.42% | 13.32% | 87.60% | 87.60% | 86.21% | 85.44% | 85.44% | 99.18% | 40.72% | 13.67% | 59.61% |
| Date | 9,115 | 96.10% | 99.91% | 99.91% | 99.96% | 99.97% | 99.93% | 96.10% | 99.91% | 99.91% | 96.10% | 99.91% | 99.91% | 99.96% | 99.97% | 99.91% | 96.10% | 99.91% | 99.91% | 99.93% | 99.93% | 99.93% | 99.92% | 99.97% | 27.13% | 42.32% |
| StaffName | 2,049 | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 93.16% | 99.90% | 99.90% | 100.00% | 100.00% | 99.95% | 93.21% | 99.90% | 99.90% | 99.95% | 99.95% | 99.95% | 100.00% | 99.95% | 71.64% | 99.75% |
| Hospital | 1,300 | 98.92% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 0.00% | 97.92% | 97.92% | 99.92% | 100.00% | 100.00% | 99.92% | 100.00% | 100.00% | 73.46% | 73.46% | 68.46% | 98.00% | 99.92% | 0.07% | 99.84% |
| Phone | 1,166 | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 84.21% | 99.39% |
| PatientName | 1,149 | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 99.65% | 100.00% | 100.00% | 99.91% | 100.00% | 100.00% | 99.65% | 100.00% | 100.00% | 99.12% | 99.04% | 99.04% | 100.00% | 99.30% | 91.73% | 90.86% |
| City | 556 | 98.92% | 99.46% | 99.46% | 99.28% | 99.46% | 99.46% | 99.28% | 99.46% | 99.46% | 1.43% | 98.38% | 98.38% | 95.86% | 99.28% | 99.28% | 95.32% | 99.28% | 99.28% | 66.54% | 66.36% | 65.64% | 96.76% | 92.80% | 1.61% | 74.82% |
| RelativeName | 538 | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 97.21% | 99.81% | 99.81% | 100.00% | 100.00% | 99.81% | 97.21% | 99.81% | 99.81% | 98.88% | 98.88% | 98.88% | 99.25% | 92.56% | 64.31% | 98.51% |
| RecordNumber | 489 | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 100.00% | 61.14% | 1.63% |
| Age | 29 | 86.20% | 96.55% | 96.55% | 100.00% | 100.00% | 96.55% | 86.20% | 96.55% | 96.55% | 86.20% | 96.55% | 96.55% | 100.00% | 100.00% | 100.00% | 86.20% | 96.55% | 96.55% | 86.20% | 86.20% | 86.20% | 55.17% | 0.00% | 0.00% | 72.41% |

</details>

<!-- results:end -->

## How the numbers are counted

- **Recall**: the share of gold spans that some prediction overlaps, whatever category it gives them. A missed span is leaked PHI, so this is the number that matters.
- **Character recall**: the share of gold characters covered. A partly covered name still leaks its uncovered part.
- **Precision**: the share of predicted spans that overlap a gold span. A false positive costs clinical text, not privacy.
- **Word F1**, with word recall and precision on the console: the units Philter and pyclipse publish. A word is a run of letters or digits; it is PHI if any of its characters is gold, and predicted if a prediction covers any of its characters. Label-agnostic.
- **Strict recall, precision and F1**, on the console and in `results.json`: the exact-boundary ("strict") span match, label-agnostic, like the binary PHI-or-not evaluation of the 2014 i2b2/UTHealth challenge, whose headline score also wants the right type. A gold span counts as found only if a prediction has exactly its start and end, and a prediction counts as right only if it is exactly a gold span. A prediction that takes in a title ("Dr. Hart" for "Hart") or cuts a name short removes the PHI but fails here, so compare strict numbers with published i2b2 results, not as a privacy measure.
- **Non-PHI removed**: the share of the non-whitespace characters outside gold PHI that a prediction covers, the clinical text lost (MedDeID reports the same rate). A year on its own counts as non-PHI in the `safe-harbor` scope, so removing it counts here.
- **95% CI**: a percentile bootstrap, 1,000 resamples of the patients (or, in a corpus without patients, the documents), from a fixed seed so a rerun repeats it. A patient's notes share names and dates, so they are resampled together. Every stack of a run is measured on the same resamples. The console shows intervals for every headline number; the table, for recall. Rates are rounded so they never flatter: down where higher is better, up for the text removed, and intervals outward.
- **Scope `safe-harbor`**: the labels DeidSharp removes under Safe Harbor. A year on its own (the nursing notes' `DateYear`) is left out, because Safe Harbor keeps it. Staff names stay in, because DeidSharp removes every name.
- **Gold outside the scope**: a prediction that touches only such gold (a bare year, an age under 90) counts as a false positive, since the text it removes isn't PHI under the scope. `--out-of-scope neutral`, a sensitivity check that is never published, leaves those predictions out instead, so they count neither for nor against a tool (F47); a prediction that runs on past such gold into other text is left out whole.
- **Recall by corpus label**: the same recall for each of the corpus's own labels, so patient, relative and staff names can be told apart.
- **No known identifiers.** Detection gets the text alone, because the corpora come without the structured record that known identifiers would come from. In a FHIR bundle, the patient's own names, dates and identifiers are also matched, so these numbers are a floor for that use.
- **Overlap counts as found.** This is lenient next to the strict span matching some papers report, so compare published numbers with care.

Known issue in ASQ-PHI: one tag marks the word "email" as an email address (query 815). Every stack misses it.

## Why .NET

Most de-identification research is written in Python; DeidSharp is not, for three reasons.

- **It began as a library for .NET systems**, which Python tools don't serve in-process, and it is still used that way: the measured stacks are the ones an application runs.
- **The models run on their usual stack.** The clinical model is Python (Presidio and transformers) in its sidecar, and the language model runs in Ollama. C# does the orchestration, the rules, the filters and the scoring, so the language doesn't change what a model finds.
- **Other tools are scored by the same code.** The evaluation tool reads any tool's spans from a file (`--predictions`), so a tool in another language is measured on the same notes, split and scorer; PhysioNet's deid (Perl) and Philter (Python) are scored that way.

Reproducing the results needs the .NET SDK and Docker, not knowledge of C#.

## Running it

```bash
docker compose up -d presidio clinical      # optional: the NER stacks (stock Presidio on 5002, clinical on 5003)
# or, with an NVIDIA GPU, the clinical model many times faster: docker compose --profile gpu up -d clinical-gpu (5005)

# ASQ-PHI (the bundled copy)
dotnet run --project tools/DeidSharp.Evaluation.Cli -c Release -- --corpus asq-phi \
  --presidio http://127.0.0.1:5002/ --clinical http://127.0.0.1:5003/

# The nursing notes: id.text from the dataset, id-phi.phrase from the deid package
dotnet run --project tools/DeidSharp.Evaluation.Cli -c Release -- --corpus physionet-deid \
  --text /data/physionet/id.text --gold /data/physionet/deid/id-phi.phrase \
  --presidio http://127.0.0.1:5002/ --clinical http://127.0.0.1:5003/ \
  --misses ~/deidsharp-misses.txt --false-positives ~/deidsharp-false-positives.txt

# The discharge summaries: note/discharge.csv.gz from MIMIC-IV-Note, unmodified
dotnet run --project tools/DeidSharp.Evaluation.Cli -c Release -- --corpus mimic-iv-note \
  --notes /data/mimic-iv-note/2.2/note/discharge.csv.gz --clinical http://127.0.0.1:5003/
```

The deterministic stack (known identifiers, patterns and the gazetteer, the deterministic part of the recommended stack) always runs; each `--presidio` or `--clinical` adds a stack, and both add a stack with all three. Each note goes to each sidecar once: the stack with both reuses the answers. The CPU and GPU builds of the clinical sidecar can differ on a borderline token, so publish from one and keep to it. `--scope all` counts every label. The console shows counts and rates only, including the false positives counted by the category the stack gave them and by the recognizer that reported them ("clinical:SpacyRecognizer").

`--predictions deid=id.phi,philter=philter-spans.tsv` also scores other tools, each from a file of its spans, so every tool is measured by the same scorer on the same notes and split. A `.phi` file is read as deid writes it (below). For any other tool, run it in its own environment and convert its output to one span per line, tab-separated: the document's id in the corpus (`patient 3, note 2`, `query 815`), the start and end (UTF-16 code units, end exclusive, in the text the corpus reader builds; for the nursing notes that is the deid software's own offsets), and optionally a DeidSharp category (`Name`). The file holds positions only, never text. Score each tool in its default or recommended configuration, and say which.

`--compare-with deid,philter` also compares every stack with those prediction files on the same resamples: the console shows each stack's change from the tool in recall, precision and word F1, with a paired 95% interval. Two stacks' own intervals can overlap while the paired one excludes zero, so this is the test to quote when one stack is said to beat another.

`--export <folder>` writes the corpus (or one `--split` of it) for other tools to read, and measures nothing: one UTF-8 text file per document under `text/`, every document as a deid record in `deid.text`, and `documents.tsv`, which gives each file's key and its document's id. The folder holds corpus text, so the tool refuses one inside a Git working tree. [`tools/baselines/to-predictions.cs`](../../tools/baselines/to-predictions.cs) turns deid's and Philter's output on an export into the TSV above.

`--gold-review <file>` writes every gold span with its label in its context, and for `mimic-iv-note` the rule that chose it, so a person can check a generated corpus; the console shows how many spans each rule typed. It measures nothing, holds corpus text, and is refused inside a Git working tree.

### Comparing with deid

PhysioNet's [deid software](https://physionet.org/content/deid/1.1/) (Perl; GNU GPL version 2 or later on PhysioNet's page, the GNU *Library* GPL version 2 or later in `deid.pl`'s header) is the nursing notes' own baseline: the notes were its development set. Nothing of deid is in this repository; you run it yourself, and the tool reads the positions it writes. Scored here over all notes and every label (`--scope all`), its output matches the figures in its paper: recall 96.68% (0.967) with the same 59 spans missed, and precision 74.83% against the paper's 0.749. That is an independent check of this scorer. Those figures come from deid's default configuration, which uses lists from this corpus (below), so they are not comparable with DeidSharp's results above.

1. Download deid 1.1 from PhysioNet, and copy `id.text` into a folder outside any Git working tree.
2. From deid's folder, run `perl deid.pl <folder>/id deid-output.config` (a few minutes).
3. Score it with `--predictions deid=<folder>/id.phi`.

deid's default configuration uses help from this corpus: each record's (surrogate) patient names, lists of the doctors and local places that appear in these notes, and a rule for one of the hospital's wards. For a run with the text alone, as DeidSharp's stacks get it, set `PID to patient name mapping`, `Doctor names`, `LocalPlaces names` and `GS filter` to `n` in a copy of the configuration. Its hospital list (`Hospital names`) also fits this corpus: it alone matches more than half of the gold locations, so a run without any help from the corpus sets that to `n` too ([findings](findings.md), F10). Report which configuration you ran.

deid also writes `id.res` and `id.info`, which hold note text: treat them like the review files above. Only `id.phi` (positions) is read here.

On ASQ-PHI, deid reads an export instead: `--corpus asq-phi --export <folder>`, then `perl deid.pl <folder>/deid deid-output.config`, then `dotnet run tools/baselines/to-predictions.cs -- deid <folder> <folder>/deid.phi deid-asq.tsv`, scored with `--predictions deid=deid-asq.tsv`. deid counts bytes, and the converter maps them to the characters the tool counts.

### Comparing with Philter

[Philter](https://github.com/BCHSI/philter-ucsf) (philter-ucsf 1.0.3, Python, BSD 3-Clause; Norgeot et al. 2020) is the other open rule-based baseline. Nothing of it is in this repository either: [`tools/baselines/philter`](../../tools/baselines/philter) builds an image that installs it from PyPI, pinned by hash, on the Python 3.10 its regexes need, and runs its default configuration (`philter_delta.json`) with no network.

1. Export the corpus: `--corpus <corpus> <its files> --export <folder>`, outside any Git working tree.
2. `docker build -t deidsharp/philter:1.0.3 tools/baselines/philter`, then `docker run --rm --network none -v <folder>/text:/data/in:ro -v <output>:/data/out deidsharp/philter:1.0.3`. Philter is plain Python on one core (rules and a POS tagger, nothing a GPU runs), and slow on long notes: a few minutes for ASQ-PHI, more than an hour for the nursing notes. It handles each file on its own, so splitting the `text` folder into batches, one container each, all writing to the same output folder, gives the same output in a fraction of the time.
3. `dotnet run tools/baselines/to-predictions.cs -- philter <folder> <output> philter.tsv`, scored with `--predictions philter=philter.tsv`.

Philter removes every letter and digit its whitelists don't keep, so its spans are the runs of characters it replaced with `*`, one per word; compare it on word F1 and character recall more than on span precision. Scored this way on ASQ-PHI, its character recall is within a quarter of a point of what MedDeID published ([findings](findings.md), F36). Its output folder holds note text, like deid's `.res`.

### Comparing with model baselines

Three open models are the learned baselines, each from its published weights at its default settings, with nothing tuned on these corpora. The Stanford/Penn model runs as a Hugging Face token-classification pipeline, its tokens grouped into spans with the `simple` aggregation (our choice; its model card names none), not through the script in its authors' repository, which can add hospital and vendor lists:

| Model | What it is | License | Its labels, as DeidSharp's categories |
|---|---|---|---|
| [Stanford/Penn de-identifier](https://huggingface.co/StanfordAIMI/stanford-deidentifier-base) (`stanford-deid`) | A BERT model trained on radiology reports and the i2b2 2006 and 2014 corpora (Chambon et al., JAMIA 2022) | MIT | patients and clinicians as names, hospitals and vendors as organizations, dates, IDs, phones |
| [OpenAI Privacy Filter](https://huggingface.co/openai/privacy-filter) (`privacy-filter`) | A general PII model from 2026, through its own package `opf` (constrained decoding, the whole text in one pass; its mixture of experts on PyTorch's own operations, not opf's Triton kernels, which need a C compiler at run time) | Apache 2.0 | people, addresses, emails, phones, URLs, dates, account numbers, secrets |
| [GLiNER multi-PII](https://huggingface.co/urchade/gliner_multi_pii-v1) (`gliner-pii`) | A GLiNER model, the one MedDeID compared, at its threshold of 0.5, in overlapping windows of 160 of its words (it reads at most 384) | Apache 2.0 | the Safe Harbor identifiers it is asked for in plain words (`detectors.py` lists them, chosen before any measurement) |

None of them is in this repository: [`tools/baselines/models`](../../tools/baselines/models) builds one image that installs their packages, pinned by hash, and downloads each model's weights at a pinned commit while it builds, so the container runs without a network.

1. Export the corpus: `--corpus <corpus> <its files> --export <folder>`, outside any Git working tree.
2. `docker build -t deidsharp/baseline-models:1 -f tools/baselines/models/Dockerfile tools/baselines/models`, then for each model `docker run --rm --gpus all --network none -v <folder>:/data/in:ro -v <output>:/data/out deidsharp/baseline-models:1 <model> /data/in /data/out/<model>.tsv`. The image runs on an NVIDIA GPU (CUDA 13.0: driver 580 or later, and Docker's GPU support, the NVIDIA Container Toolkit or Docker Desktop on WSL 2), as an unprivileged user; on Linux, where a mounted folder keeps its owner, add `--user "$(id -u):$(id -g)"` so it can write to `<output>`.
3. Score them with `--predictions stanford-deid=<output>/stanford-deid.tsv,privacy-filter=<output>/privacy-filter.tsv,gliner-pii=<output>/gliner-pii.tsv`, and pair every stack with them with `--compare-with`.

Their spans files hold positions and categories only, never text. Their labels are mapped to DeidSharp's categories for the false-positive counts; recall doesn't depend on the category.

`--variants repeated-names` also measures each stack with a change (`StackVariants` lists them), the stacks with an LLM layer included (but not their second opinions), each variant after the stack it wraps, so one run compares the two with the same resamples: the console shows each variant's change from its stack in recall, precision and word F1, with a paired 95% interval. A change is real when its interval excludes zero. `repeated-names` wraps a stack in `RepeatedNameDetector`, which looks for the other mentions of every name the stack found. Like `--drop-sources`, it can't be combined with `--publish`.

`--llm qwen3.5:9b,gemma4:12b` also measures each stack with an LLM layer (`LlmPhiDetector`) on a local Ollama server (`--llm-endpoint`, default `http://127.0.0.1:11434/`; the tool refuses one that isn't on this machine, since the notes are sent to it), each right after its stack with its paired gain. Each model reads each note once, with thinking turned off: reasoning first makes a note take a minute instead of a second. `--llm-prompts instructions,examples` measures each model with each prompt (`LlmPrompts` lists them; `examples` adds four short fictional notes and their answers; `focused-rules` and `retrieved` are experiments, which can't be published). `--ablation on` also measures each Presidio service, each model, and each service with each model, without the deterministic layer, each paired with the deterministic layer plus the same layers, so the change shows what the deterministic detectors add. `--clinical-places on` also measures the clinical sidecar keeping only its locations and organizations, from the same answers. `--llm-second-opinion on,self` also measures each LLM stack with the model's second opinion on the other detectors' spans (`on`), or on its own spans as well (`self`, an experiment, which can't be published). With `--publish`, the results name Ollama's version and each model's digest, read before the run and again after it; if either changed in between (a pull, an update), the tool publishes nothing.

`--drop-sources clinical:SpacyRecognizer,clinical:UrlRecognizer` leaves out the spans that only those recognizers reported, so you can see what each one adds to recall and costs in precision before changing a sidecar. It measures a stack nobody runs, so it can't be combined with `--publish`.

`--note` publishes one line with the results, for what the tool can't see: which images ran, and on CPU or GPU. `--render docs/evaluation` rewrites the table and the badge from `results.json` without measuring, after a change to how they are shown.

`--misses` writes each missed span, and `--false-positives` each predicted span that is not gold PHI (with the detectors that reported it), with 40 characters of context on each side, for you to read and decide what to change. The tool refuses either path inside a Git working tree. Under PhysioNet's data use agreement, keep those files on your machine: don't commit them, share them, or paste them into an online service or an AI assistant.

`--miss-profile UntypedName` counts, for each stack, the misses of one corpus label among the spans with each feature: one word or several, an ordinary English word, casing, a word in another label's span, and whether another gold span has the same text and the stack found it. A span can have several features, so the rows overlap. It reads the text but prints counts only, so you can see where a stack's misses fall without reading them ([findings](findings.md), F42).

## Before a release

Published results keep the version and the commit they were measured at, and a release ships with them until a change could alter what the measured stacks find (D5 in [findings](findings.md)). What counts: the detectors' code, the clinical sidecar and its dependencies, the stock Presidio image, the language-model detector's dependencies, and the evaluation tool that runs and scores the stacks. A release that changes only packaging, docs, or dependencies the measured stacks don't run (the FHIR package's) measures nothing.

1. Set the release's version in `Directory.Build.props`.
2. Compare the release with each measurement in `results.json`: `git diff <its commit> HEAD -- src sidecar tools/DeidSharp.Evaluation.Cli docker-compose.yml ':!*.md' ':!sidecar/tests'`. If nothing changed, skip to step 5. If a change could alter what a stack finds, measure that corpus again (step 3). Otherwise, or if a finding has already counted the change's effect and the published numbers err on the conservative side, add a line for the release under Releases in [findings](findings.md), starting `- **v<version>**`, that names the results kept and says what changed and why their numbers stand; CI refuses the tag without it.
3. To measure again: run the nursing notes with `--split report`, ASQ-PHI with `--split all` and the discharge summaries with `--split report`, each in the configuration of record with the baselines, `--publish docs/evaluation` and a `--note` naming the sidecar build (CPU or GPU), from a clean checkout of `main`'s tip ([REPRODUCING.md](../../REPRODUCING.md#the-run-of-record) has the commands); nothing else lands on `main` until step 4. This rewrites `results.json`, the table above, `badge.json` and, where it has the markers, the repository README's results at a glance (the tool says when it has none). Publish the comparisons as the next finding whatever they show; no detector choice is made from a reporting half. Read the misses and the false positives of a `--split tune` run, never the report half's; turn a pattern worth fixing into a fictional regression test, and never commit corpus text.
4. Commit `docs/evaluation` on top of the measured commit as a commit of its own, changing no code, so the measured commit stays in the history.
5. Tag the release commit (after step 3, the commit of step 4) `v<version>` and push the tag. CI refuses the tag if `results.json` lacks the measurement on the nursing notes or ASQ-PHI, or if any measurement in it comes from a later version or measured code that changed since, without a release line in findings. Once every check passes, CI's publish job packs the five libraries with their symbols and pushes them to nuget.org by Trusted Publishing, with no stored API key (a policy for this repository and `ci.yml` on nuget.org, the `NUGET_USER` repository variable, and the `nuget` environment, which can require an approval). nuget.org can't delete a version, only unlist it.
