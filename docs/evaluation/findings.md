# Findings

The chronological log of every measurement behind DeidSharp's detectors, newest last, including the ideas that didn't work. Each entry gives the evidence (numbers, how they were measured, the commit) and what it led to. The results of record are F46, D6's run of record, and F36 to F41 before it; the earlier entries show how the configurations they measure were reached. Entries are not rewritten: a finding that turns out wrong gets a new entry that says so.

Counts and rates only. Nothing here quotes a note. **The nursing notes** are PhysioNet's Deidentified Medical Text and **the discharge summaries** the `mimic-iv-note` corpus built from MIMIC-IV-Note ([corpora](README.md#corpora)). Findings are made on the tuning halves of the nursing notes and the discharge summaries (`--split tune`) and on ASQ-PHI ([how the numbers are counted](README.md#how-the-numbers-are-counted)). ASQ-PHI has no held-out half, so its numbers are a second check, not a held-out result. The nursing notes' reporting half was measured four times while the work went on (F13, F23, F30, F35), each time for configurations already fixed on the tuning half, and once more at the release (F37); the discharge summaries' reporting half once (F41); both once more, with ASQ-PHI, in the run of record (F46), after a first pass on ASQ-PHI and the nursing notes that it superseded (D6's amendment). "Clinical" is the clinical sidecar; "deterministic" is DeidSharp's own detectors. The work went in three stages: the sidecar and the deterministic detectors (F1 to F14), a local language model as a detector (F15 to F23), and its precision (F24 to F35).

The commits named before F36 belong to the development history from before this repository was published ([REPRODUCING.md](../../REPRODUCING.md#commits)). The code, the prompts and the fictional example notes were written with an AI coding assistant (Claude Code, following [CLAUDE.md](../../CLAUDE.md)). It never saw the text of the nursing notes or the discharge summaries: it ran the evaluation tool, whose output is counts and rates, and the maintainer described misses with made-up examples.

## F1. The clinical stack's low precision came from three of Presidio's recognizers, not the clinical model

*2026-10-17, the nursing notes' tuning half (1,127 notes, 844 gold spans), commit 9a9161a.*

The clinical stack found 89.7% of the PHI at 4.9% precision. Of about 12,900 false positives, spaCy's general-purpose NER (`en_core_web_sm`, which Presidio runs inside the sidecar) reported 11,747, `UsLicenseRecognizer` 797 and `UrlRecognizer` 488. The clinical model (`obi/deid_roberta_i2b2`) reported 433. DeidSharp's own patterns reported 30.

**Meaning:** precision was a configuration problem in the sidecar, fixable without touching the model.

## F2. Those recognizers bought about 2 points of recall for 22 times as much lost text

*2026-10-17 and 2026-10-18, tuning half and ASQ-PHI, commits 00bff4b (`--drop-sources`) and 4d28a81 (metrics).*

| Clinical stack | Recall (95% CI) | Precision | Word F1 | Non-PHI text removed |
|---|---:|---:|---:|---:|
| With all of Presidio's recognizers | 89.7% (86.4–92.5) | 4.9% | 8.6% | 11.0% |
| Without spaCy's NER, URL and license | 87.4% (83.8–90.4) | 55.8% | 62.5% | 0.50% |

On ASQ-PHI, the same change cost 0.5 points of recall (99.97% to 99.50%) and raised precision from 64% to 76%. Trials of spaCy limited to some labels (in a throwaway sidecar with a mounted configuration) placed the trade-off: people and places only gave 88.5% recall at 14.7% precision; adding organizations brought back 7 locations, likely hospital names, for about 5,900 false positives.

**Meaning:** the spans only spaCy found (mostly locations and staff names) might be recovered by cheaper means: finding a name's other mentions (F7) or a gazetteer (F9). Applied in F6.

## F3. The scorer reproduces deid's published results

*2026-10-18, all 2,434 notes, every label (`--scope all`), commit 508c8da.*

PhysioNet's deid 1.1, run as shipped (`deid-output.config`) and scored by our tool from its `.phi` output: recall 96.68% with 59 spans missed, precision 74.83%. The paper (Neamatullah et al. 2008, Table 4) reports 0.967, 59 missed and 0.749, and each label matches too (clinician names 99.49% against 0.995, locations 97.28% against 0.973, dates 94.61% against 0.946).

**Meaning:** our counting agrees with the corpus authors'. Comparisons with other tools through `--predictions` can be trusted to the extent their own conversion is right.

## F4. On its home corpus deid is well ahead, even without its corpus-specific help

*2026-10-18, tuning half, safe-harbor scope, commit 508c8da.*

| Stack | Recall (95% CI) | Precision | Word F1 | Non-PHI text removed |
|---|---:|---:|---:|---:|
| deid, default configuration | 97.2% (95.9–98.3) | 74.5% | 82.4% | 0.17% |
| deid without its corpus aids | 95.3% (93.4–96.8) | 74.2% | 82.7% | 0.16% |
| DeidSharp + clinical, after F6 | 87.7% (84.1–90.5) | 55.0% | 62.2% | 0.51% |

deid's default configuration uses help from this corpus: each record's (surrogate) patient names, lists of the doctors and local places in these notes, and a rule for one of the hospital's wards. Turning those four off ("without its corpus aids") still leaves rules developed on these very notes, which were deid's development set.

**Meaning:** about 8 points of recall separate us from deid on these notes. Its edge is dictionaries and repeated names, which F7 and F9 test.

## F5. Outside its home corpus, deid falls well behind

*2026-10-18, ASQ-PHI (1,051 queries, 2,976 gold spans), commit 84c84d2.*

| Stack | Recall (95% CI) | Character recall | Precision | Word F1 |
|---|---:|---:|---:|---:|
| DeidSharp + clinical, after F6 | 99.73% (99.53–99.90) | 95.96% | 76.3% | 92.6% |
| deid, default configuration | 85.85% (84.82–86.98) | 68.45% | 94.7% | 80.6% |

deid found 47% of the medical record numbers, 13% of the health plan numbers and 82% of the locations; its corpus aids made no difference here. This agrees with published results on i2b2's discharge summaries (deid's recall 69.8% in Norgeot et al. 2020; F1 0.757 in pyclipse, Moore et al. 2023).

**Meaning:** a rule system tuned to one hospital's notes doesn't carry to other text; DeidSharp's stack does. deid stays a baseline, not a component of DeidSharp.

## F6. The sidecar without spaCy's NER, URL and license recognizers

*2026-10-18, commit 84c84d2.*

Applied F2 in the sidecar (`LEFT_OUT_RECOGNIZERS` in `sidecar/app.py`). Measured with the rebuilt image: tuning half 87.68% recall (95% CI 84.06–90.52) at 55.01% precision; ASQ-PHI 99.73% at 76.34%. The clinical ASQ-PHI floor in the integration tests moved to 0.997 recall and 0.959 character recall.

The tuning half's recall is 0.24 points higher than `--drop-sources` predicted (87.44%): removing a recognizer inside Presidio also changes how its remaining results are merged, which filtering spans afterwards can't reproduce exactly. Treat `--drop-sources` as a close estimate, and measure the rebuilt sidecar before publishing.

## F7. Finding a name's other mentions adds almost nothing

*2026-10-18, tuning half and ASQ-PHI, `--variants repeated-names`.*

`RepeatedNameDetector` searches a note for every other mention of the names the stack found (by any part, whole words, as known names). On the tuning half it found 1 more of 844 spans (recall 87.68% to 87.80%, intervals 84.06–90.52 and 84.22–90.62) and cost 2.5 points of precision (55.01% to 52.53%). On ASQ-PHI it changed nothing.

**Meaning:** the clinical model already finds a name's repeat mentions; what it misses are names it finds nowhere in the note. deid's lead over us (F4) comes from somewhere else: without its corpus aids it still loads large generic dictionaries of first names, surnames and places, and its lead is largest on locations (93.7% against 75.3%) and staff names (96.7% against 90.0%). That points to a gazetteer of names and places from public-domain sources (F9), not more propagation. `RepeatedNameDetector` stays as an opt-in, untried on longer notes such as discharge summaries. (Later moved from the library to the evaluation tool, since no recommended stack uses it.)

## F8. Half the gold names and places in the nursing notes are written in capitals

*2026-10-18, tuning half, a counts-only diagnostic of the gold spans (no text printed).*

| Gold, tuning half | Locations (190) | Name words (402) |
|---|---:|---:|
| Written in capitals | 90 | 142 |
| Written in lowercase | 33 | 92 |
| In title case | 67 | 152 |
| Exactly a Census 2024 place | 42 | |
| In the Census name lists | | 325 |
| One word | 178 | |
| With a hospital word ("hospital", "medical", "center"...) | 9 | |

**Meaning:** a dictionary that only looks up capitalized words ("Hart") misses half of these. The locations are mostly one word, and few are towns in the Census list: F10 shows what they are.

## F9. The gazetteer: about 3 points of recall for 13 of precision on the nursing notes, neutral on ASQ-PHI

*2026-10-18, tuning half and ASQ-PHI, `--variants gazetteer`, `DeidSharp.Gazetteer` (US Census 2010 surnames, 1990 first names, 2024 places; 12dicts 2of12 for common words).*

Options compared on the tuning half, each added to the clinical stack (87.68% recall at 55.01% precision):

| Gazetteer | Recall | Precision | Word F1 | Non-PHI text removed |
|---|---:|---:|---:|---:|
| Title case only, every surname | 88.39% | 48.40% | 59.0% | 0.61% |
| Also capitals, every surname | 90.64% | 26.31% | 38.6% | 1.64% |
| Also capitals, surnames of 1,000+ people on their own | 90.64% | 35.77% | 47.6% | 1.08% |
| **Also capitals, surnames of 10,000+ people on their own (the default)** | **90.52% (87.31–93.15)** | **42.33%** | **51.8%** | **0.91%** |
| Any case, surnames of 10,000+ | 92.42% | 23.94% | 27.8% | 2.47% |

With the default, staff names go from 89.7% to 93.4%, relatives from 94.0% to 97.6%, patients from 88.2% to 94.1% and locations from 75.3% to 80.0%. On ASQ-PHI, recall stays at 99.73%, character recall goes from 95.96% to 96.38% and precision from 76.3% to 75.0%. Treating inflected forms as common words too (12dicts' 2of12inf, under WordNet's license) changed little (precision 43.8%, recall 90.40%), so the public-domain base list stays.

A hand-written list of 81 clinical abbreviations, drugs and eponyms that are also Census names ("Abd", "Foley", "Fowler", "Trach") makes them count only with context; it was built by checking a clinical vocabulary against the lists, not from the notes.

**Meaning:** shipped as an opt-in package; whether DeidSharp's recommended stack includes it is a recall-for-text trade for each deployment. The surrogate names and towns in the nursing notes were drawn from public lists, so the gain on real notes may be smaller.

## F10. deid's lead on locations came from its hospital list, which fits this corpus

*2026-10-18, tuning half. This corrects the reading of F4.*

deid's `stripped_hospitals.txt` (137 entries) alone matches 111 of the 190 gold locations (counts only). F4's "without its corpus aids" run still had it on (`Hospital names = y`). With it off as well:

| Stack | Recall (95% CI) | Precision | Locations | Staff names |
|---|---:|---:|---:|---:|
| deid without its corpus aids (F4) | 95.3% (93.4–96.8) | 74.2% | 93.7% | 96.7% |
| deid without those and its hospital list | 82.5% (78.3–85.9) | 71.6% | 36.8% | 96.7% |
| DeidSharp + clinical (F6) | 87.7% (84.1–90.5) | 55.0% | 75.3% | 89.7% |
| DeidSharp + clinical + gazetteer (F9) | 90.5% (87.3–93.2) | 42.3% | 80.0% | 93.4% |

**Meaning:** with every list built for this corpus turned off, DeidSharp finds more of the PHI than deid; deid keeps the better precision and staff names. deid's hospital list is the kind of local knowledge a deployment has (its hospitals, wards and staff): DeidSharp doesn't take such site lists; none was tried.

## F11. A national hospital list doesn't match this corpus's hospitals

*2026-10-18, tuning half, counts only.*

CMS's Hospital General Information list (5,419 US hospitals, public domain), stripped of generic words ("Medical Center", "Hospital") the way deid's list is: only 10 of the 190 gold locations equal a stripped name. Matching their first words reaches 46, but 1,149 stripped names are one word, many of them everyday words.

**Meaning:** the hospitals in these notes are local names and abbreviations, not national ones, so a national list isn't worth its false positives. Local lists supplied by a deployment (F10) are the way to cover them.

## F12. What the best stack still misses has no common shape

*2026-10-18, tuning half, read by the maintainer in the misses file (never shared); counts from the tool.*

The clinical stack with the gazetteer misses 80 of 844 spans: about 38 locations, 20 staff names, 17 dates, and a few relatives and patients. Phones are all found. Reading them, the maintainer saw house staff and doctor names, names next to a relation to the patient, dates such as "3/14" without a year, and names whose role the text doesn't make clear, with no recurring pattern among them.

**Meaning:** what is left depends on context more than on shape, which is what an LLM layer can add (F15 onward). The one clear shape, month and day without a year, is left to that layer for now: a rule for it collides with ventilator settings ("PS 12/5") and fractions ("1/2 NS"), and pattern rules also feed the leak guard, where a noisy rule would refuse clean output.

## F13. The sidecar change on the reporting half: precision tenfold, recall 3 points lower

*2026-10-18, **the nursing notes' reporting half** (1,307 notes, 889 gold spans) and ASQ-PHI, published to results.json. The first of four looks at the reporting half during the work.*

| Stack | Recall (95% CI) | Precision | Word F1 | Non-PHI text removed |
|---|---:|---:|---:|---:|
| Clinical, before F6 (published 2026-10-17) | 89.53% | 4.49% | | |
| **Clinical, after F6** | **86.16% (82.43–89.06)** | **52.47%** | 59.2% | 0.53% |
| Presidio + clinical, after F6 | 89.31% (86.08–91.79) | 8.29% | 15.1% | 5.41% |

On ASQ-PHI the clinical stack has 99.73% recall at 76.34% precision. The gazetteer is opt-in, so the published stacks leave it out; on the tuning half it added 2.84 points (F9).

**Meaning:** the reporting half agrees with the tuning half (F6: 87.68% at 55.01%), so the tuning didn't overfit. The badge shows the lower recall; the precision behind it is now usable.

## F14. Without a model, the gazetteer nearly doubles what the deterministic detectors find on ASQ-PHI

*2026-10-18, ASQ-PHI (all 1,051 queries) and the nursing notes' tuning half.*

| Deterministic detectors | ASQ-PHI recall | ASQ-PHI precision | Nursing notes (tuning) recall | Nursing notes (tuning) precision |
|---|---:|---:|---:|---:|
| Known identifiers and patterns | 47.58% | 99.72% | 25.71% | 87.12% |
| With the gazetteer | 86.63% | 97.68% | 44.19% | 43.41% |

**Meaning:** a deployment without a sidecar gains the most from the gazetteer, at little cost on ASQ-PHI's queries and a larger one on the nursing notes. CI now holds both versions to recall floors on ASQ-PHI.

## F15. A local LLM matches the clinical model on ASQ-PHI

*2026-10-18, ASQ-PHI, `--llm qwen3.5:9b` (instructions only, thinking off) added to the deterministic stack (known identifiers, patterns, gazetteer), Ollama on an RTX 5080.*

| Stack | Recall (95% CI) | Character recall | Precision | Word F1 |
|---|---:|---:|---:|---:|
| Deterministic | 86.63% (85.47–87.79) | 70.70% | 97.68% | 83.2% |
| Deterministic + qwen3.5:9b | 99.50% (99.24–99.73) | 97.62% | 83.35% | 93.0% |
| Deterministic + clinical sidecar | 99.73% (99.53–99.90) | 96.38% | 74.95% | 92.3% |

The LLM's recall gain is +12.87 points (+11.71 to +13.99, paired). The run took 10 minutes for 1,051 queries. Its character recall, 97.62%, sits between the published GLiNER (96.32) and MedDeID (98.90) figures on the same measure (M3).

## F16. On the nursing notes, the LLM layer passes the bar: 98.3% recall, at a precision cost

*2026-10-18, tuning half, `--llm qwen3.5:9b --llm-prompts instructions,examples`. "Examples" adds four short fictional notes and their answers to the prompt (`LlmPrompt.WithExamples`), written by hand for the kinds of misses in F12, never taken from the corpus.*

| Stack | Recall (95% CI) | Missed | Precision | Word F1 | Non-PHI text removed |
|---|---:|---:|---:|---:|---:|
| Clinical + gazetteer (the best without an LLM) | 90.52% (87.31–93.15) | 80 | 42.33% | 51.8% | 0.91% |
| + qwen3.5:9b, instructions | 96.45% (94.23–98.10) | 30 | 21.71% | 31.8% | 2.02% |
| **+ qwen3.5:9b, with examples** | **98.34% (97.17–99.32)** | **14** | **21.41%** | **32.3%** | **2.10%** |
| Deterministic + qwen3.5:9b with examples (no clinical sidecar) | 94.43% (91.89–96.51) | 47 | 23.25% | 34.7% | 1.77% |

Paired recall gains over the clinical stack: +5.92 points (+4.06 to +8.20) with the instructions, +7.82 (+5.27 to +10.77) with the examples, so both pass D1. With the examples, staff names reach 98.7%, locations 96.8%, dates 99.1%, relatives and patients 100%. The examples mostly helped locations (93.2% to 96.8% on the clinical stack; 81.1% to 92.1% without it).

The cost is about 1,900 false positives from the model: ~980 typed as other identifiers, ~580 dates, ~420 names, ~270 locations and ~195 ages. Ages under 90 and years alone are not PHI under Safe Harbor, and the prompt says so; F18 filters them out of the model's answer by rule.

**Meaning:** the clinical stack with a 9B local model and the examples misses 14 of 844 spans, fewer than deid with all its corpus aids (24, F4). Without the clinical sidecar, the LLM stack still beats the clinical stack (94.4% against 90.5%), so a deployment can choose one model instead of two. Speed: about 0.4 seconds per note on an RTX 5080.

## F17. gemma4:12b is the best of the three local models

*2026-10-18, tuning half, `--llm gemma4:12b,gemma4:e4b --llm-prompts examples` (compare F16 for qwen3.5:9b, same prompt). All three fit an RTX 5080 (16 GB) next to the clinical sidecar.*

| LLM with examples, added to | Recall (95% CI) | Missed | Precision | Word F1 | Non-PHI text removed |
|---|---:|---:|---:|---:|---:|
| clinical stack: qwen3.5:9b | 98.34% (97.17–99.32) | 14 | 21.41% | 32.3% | 2.10% |
| clinical stack: **gemma4:12b** | **98.70% (97.97–99.29)** | **11** | **28.99%** | **42.6%** | **1.39%** |
| clinical stack: gemma4:e4b | 96.68% (95.04–98.04) | 28 | 29.99% | 42.3% | 1.43% |
| deterministic stack: gemma4:12b | 93.60% (91.75–95.29) | 54 | 33.72% | 48.7% | 1.03% |
| deterministic stack: gemma4:e4b | 88.74% (85.64–91.69) | 95 | 34.56% | 46.7% | 1.06% |

Paired recall gains over the clinical stack: gemma4:12b +8.18 points (+5.53 to +11.20), gemma4:e4b +6.16 (+3.96 to +8.93). gemma4:12b's false positives are mostly dates (about 880); qwen3.5:9b's are spread over identifiers, dates and ages.

**Meaning:** gemma4:12b gives the most recall and the best precision of the three; without the clinical sidecar it still finds more than the clinical stack does, at about the same text cost. The small gemma4:e4b passes D1 too, for a GPU with less memory.

## F18. Rule filters on the model's answer recover most of the precision

*2026-10-18, tuning half, gemma4:12b with examples, `LlmDetectorOptions.SafeHarborOnly` (default on). The filters drop what Safe Harbor keeps, whatever type the model gave it: an age under 90; as a date, a year or a time on its own ("2019", "0800", "8pm") or words with no number or month ("today", "Monday"); an identifier of three digits or fewer.*

| Stack | Recall (95% CI) | Missed | Precision | Word F1 | Non-PHI text removed |
|---|---:|---:|---:|---:|---:|
| Clinical + gazetteer (no LLM) | 90.52% (87.31–93.15) | 80 | 42.33% | 51.8% | 0.91% |
| Clinical + gemma4:12b, unfiltered (F17) | 98.70% (97.97–99.29) | 11 | 28.99% | 42.6% | 1.39% |
| **Clinical + gemma4:12b, filtered** | **98.93% (98.23–99.51)** | **9** | **38.72%** | **49.7%** | **1.07%** |
| Deterministic + gemma4:12b, unfiltered (F17) | 93.60% (91.75–95.29) | 54 | 33.72% | 48.7% | 1.03% |
| **Deterministic + gemma4:12b, filtered** | **93.48% (91.50–95.26)** | **55** | **50.25%** | **59.5%** | **0.69%** |

Model dates fell from about 880 false positives to about 260. A first version of the filter also dropped day numbers ("21" for "the 21st"), costing 2 spans without the clinical sidecar; the final one keeps any number of one or two digits. The paired gain over the clinical stack is +8.41 points of recall (+5.85 to +11.45) for −3.61 points of precision (−4.68 to −2.65).

**Meaning:** with the filters, a deterministic stack and one local model beat the clinical stack on every measure here: more recall, better precision, less text removed. Added to the clinical stack, the model leaves 9 of 844 spans. The recall differences between the filtered and unfiltered runs are within the model's run-to-run variation (M8).

## F19. A stricter prompt buys precision with recall; telling a small model what is *not* PHI costs recall

*2026-10-18, tuning half, gemma4:12b, one paired run with three prompts. "Focused" (`LlmPrompt.Focused`) was written in a separate review of the prompt by an AI assistant: a closed list of what is PHI, a "never PHI" list (units such as MICU and OSH, durations, vent settings, "2/2"), "list each spelling once", and five region-neutral fictional notes. "Focused-rules" is an ablation: the focused instructions with the first examples.*

| gemma4:12b prompt | + clinical: recall (95% CI) | missed | precision | deterministic + LLM: recall (95% CI) | missed | precision |
|---|---:|---:|---:|---:|---:|---:|
| Examples (F16) | 98.70% (97.97–99.29) | 11 | 38.65% | 93.36% (91.40–95.14) | 56 | 50.18% |
| Focused | 97.39% (96.19–98.34) | 22 | 42.35% | 91.11% (88.99–93.20) | 75 | 57.97% |
| Focused rules, first examples | 95.85% (94.12–97.38) | 35 | 41.98% | 88.15% (85.55–90.82) | 100 | 57.07% |

The focused prompt cut the model's date and identifier false positives (model-typed ids from about 120 to about 34, dates from about 260 to about 170 with the clinical stack) but lost locations and dates: 93.2% against 96.3% of locations with the clinical stack, 74.2% against 77.9% without. The ablation, with the same examples as the first prompt, lost the most, so the loss comes from the instructions, not the examples.

**Meaning:** with small models, a list of what not to report makes them skip real PHI that resembles it (a hospital or unit written like "OSH" or "rehab"). Under recall first, the examples prompt stays the default and the prompt for H1. Focused stays as a measured preset for precision-first use. The review's other point stands: the first examples carry the corpus's region (a Massachusetts town, area code and language); F22 measures a region-neutral copy.

## F20. H1, Gemma family: a bigger model finds more and flags less

*2026-10-18, tuning half, one paired run: gemma4:e4b, gemma4:12b and gemma4:26b-a4b (mixture of experts, 4B active, 17 GB) with the examples prompt and filters. The clinical layer came from the CPU sidecar, so the 26B model had the GPU; its answers are the same as the GPU sidecar's.*

| gemma4 | Deterministic + LLM: recall (95% CI) | missed | precision | non-PHI removed | + clinical: recall (95% CI) | missed | precision |
|---|---:|---:|---:|---:|---:|---:|---:|
| e4b | 88.63% (85.53–91.59) | 96 | 39.96% | 0.94% | 96.68% (95.04–98.04) | 28 | 32.87% |
| 12b | 93.36% (91.40–95.14) | 56 | 50.18% | 0.69% | 98.70% (97.97–99.29) | 11 | 38.65% |
| **26b-a4b** | **96.80% (95.43–98.07)** | **27** | **52.62%** | **0.65%** | **99.41% (98.76–99.87)** | **5** | **39.42%** |

Recall and precision both rise with size. Without the clinical sidecar, the 12B and 26B intervals don't overlap. With it, the 26B model found every location (190 of 190) and every staff name. Its false identifiers fell to 27 (from 454 for e4b and 99 for 12b), though it listed somewhat more names and places that aren't gold. The whole run took 25 minutes for 1,127 notes, three models and the CPU clinical sidecar.

**Meaning (H1):** within one family, size improves de-identification on both recall and precision, and the step from 12B to 26B is still large without the clinical model. A deterministic stack with one 26B local model (96.8%) comes close to deid with every list built for this corpus (97.2%, F4), on notes deid was developed on.

## F21. H1, Qwen family: total size buys precision, not recall; the best model is gemma4:26b-a4b

*2026-10-18, tuning half, one paired run: qwen3.5:9b (dense), qwen3.5:35b-a3b (mixture of experts, 3B active, 22 GB) and gemma4:26b-a4b, examples prompt and filters, CPU clinical sidecar. Speed is the model's own time per note on an RTX 5080 (16 GB), the first stack that asks it.*

| Model | Deterministic + LLM: recall (95% CI) | missed | precision | + clinical: recall (95% CI) | missed | precision | Time per note |
|---|---:|---:|---:|---:|---:|---:|---:|
| qwen3.5:9b | 93.84% (90.47–96.49) | 52 | 28.52% | 97.75% (96.33–98.88) | 19 | 24.84% | 0.34 s |
| qwen3.5:35b-a3b | 91.59% (88.76–94.04) | 71 | 45.72% | 97.63% (95.87–98.89) | 20 | 36.04% | 0.45 s |
| gemma4:26b-a4b | 96.80% (95.43–98.07) | 27 | 52.62% | 99.41% (98.76–99.87) | 5 | 39.42% | 0.43 s |

The CPU clinical sidecar took 0.26 s per note. qwen3.5:9b found 4 more spans in this run than in F16 with the same prompt, within the run-to-run variation (M8).

**Meaning (H1):** within Gemma, size improves both recall and precision (F20). Within Qwen, the 35B mixture of experts, which uses only 3B parameters per token, gained precision (28.5% to 45.7% without the clinical model) but not recall. So "bigger" helps through the model's active capacity and family, not its total size alone. gemma4:26b-a4b is the best of the five on every measure, at about the 9B model's speed: it is the model in every later finding.

## F22. Region-neutral examples do as well; on ASQ-PHI one local model beats the clinical stack

*2026-10-18, gemma4:26b-a4b, one paired run per corpus: the examples prompt against `LlmPrompt.WithNeutralExamples`, the same examples with nothing pointing to one region (a town, an area code, a language) and the age copied with its unit ("94 yo").*

| gemma4:26b-a4b | Tuning half: + clinical recall | missed | precision | deterministic + LLM recall | missed | precision |
|---|---:|---:|---:|---:|---:|---:|
| Examples | 99.41% | 5 | 39.42% | 96.80% | 27 | 52.62% |
| Neutral examples | 99.29% | 6 | 38.90% | 96.68% | 28 | 51.67% |

| ASQ-PHI | Recall (95% CI) | Character recall | Precision | Word F1 | Non-PHI text removed |
|---|---:|---:|---:|---:|---:|
| Deterministic + clinical | 99.73% (99.53–99.90) | 96.38% | 74.95% | 92.30% | 2.86% |
| Deterministic + gemma4:26b-a4b, examples | 99.63% (99.40–99.83) | 97.65% | 97.89% | 98.23% | 0.45% |
| Deterministic + gemma4:26b-a4b, neutral examples | 99.66% (99.46–99.86) | 97.69% | 97.86% | 98.24% | 0.46% |

The two prompts differ by one span on each corpus, within noise (M8).

**Meaning:** the neutral examples are the prompt from here on; nothing in them points to the corpus's region. On ASQ-PHI, one local model with the deterministic detectors matches the clinical stack's recall at 98% precision against 75%. Most of the clinical stack's false positives there are ages (878), which ASQ-PHI, like Safe Harbor, doesn't count under 90; the clinical model has no such filter. F24 applies the LLM's age rule to every detector.

## F23. The LLM layer on the reporting half: 99.6% recall, ahead of deid with all its corpus aids

*2026-10-18, **the nursing notes' reporting half** (1,307 notes, 889 gold spans), the second look at that half. gemma4:26b-a4b with the neutral examples and filters (F22), CPU clinical sidecar; deid's runs are the ones in F4 and F10, scored on the same half.*

| Stack | Recall (95% CI) | Missed | Precision | Word F1 | Non-PHI text removed |
|---|---:|---:|---:|---:|---:|
| DeidSharp: deterministic (known identifiers, patterns, gazetteer) | 42.07% (37.24–46.28) | 515 | 43.78% | 37.0% | 0.41% |
| DeidSharp: deterministic + clinical | 88.30% (84.71–91.11) | 104 | 41.50% | 50.5% | 0.86% |
| **DeidSharp: deterministic + gemma4:26b-a4b** | **97.86% (96.36–99.01)** | **19** | **52.28%** | **65.1%** | **0.57%** |
| **DeidSharp: deterministic + clinical + gemma4:26b-a4b** | **99.55% (98.89–100.00)** | **4** | **38.43%** | **50.7%** | **1.00%** |
| deid, default configuration (all corpus aids) | 97.30% (95.73–98.61) | 24 | 72.57% | 82.3% | 0.18% |
| deid without its corpus aids (F4) | 96.06% (94.16–97.57) | 35 | 72.39% | 82.7% | 0.17% |
| deid without those and its hospital list (F10) | 84.70% (80.11–88.87) | 136 | 70.37% | 78.3% | 0.17% |

The LLM's paired gain over the clinical stack is +11.25 points of recall (+8.49 to +14.78), larger than on the tuning half (+8.89, F20), so the tuning didn't overfit. With the clinical stack and the model, dates, locations, relatives, patients and phones are at 100% and staff names at 98.97%. Speed: the model took 0.40 s per note and the CPU clinical sidecar 0.25 s.

**Meaning:** on held-out notes from deid's own home corpus, DeidSharp with a 26B local model misses 4 of 889 spans, against deid's 24 with every list built for this corpus, and the intervals don't overlap. Without the clinical sidecar, one local model still matches deid's recall. deid keeps the better precision (about 73% against 38–52%), and removes less text (0.18% against 0.57–1.00%). The LLM layer's bar (D1) is passed on the reporting half too.

## F24. With an LLM in the stack, the gazetteer costs far more precision than it adds; the age rule works

*2026-10-18, tuning half, gemma4:26b-a4b with the neutral examples, one paired run with `--gazetteer with,without`. Every stack now has `SafeHarborAgeFilter`.*

| Stack | Recall (95% CI) | Missed | Precision | Word F1 | Non-PHI text removed |
|---|---:|---:|---:|---:|---:|
| Deterministic + clinical (with gazetteer) | 90.52% (87.31–93.15) | 80 | 43.92% | 52.6% | 0.89% |
| Deterministic + LLM, with gazetteer | 96.68% (95.39–97.79) | 28 | 51.67% | 62.3% | 0.66% |
| **Deterministic + LLM, without gazetteer** | **96.33% (95.11–97.53)** | **31** | **71.66%** | **78.1%** | **0.27%** |
| Clinical + LLM, with gazetteer | 99.29% (98.50–99.86) | 6 | 40.16% | 51.4% | 1.04% |
| **Clinical + LLM, without gazetteer** | **99.05% (98.12–99.76)** | **8** | **50.80%** | **61.3%** | **0.64%** |

The age filter cut the clinical stack's age false positives from 60 to 1 and raised its precision from 42.33% (F9) to 43.92%, with recall unchanged; the clinical stack without the LLM is deterministic, so that difference is the filter alone. With the LLM, the gazetteer adds 2 or 3 spans, within run-to-run noise (M8), and about 400 false positives (300 names, 97 places).

**Meaning:** the gazetteer was added to recover names and places a model missed (F9); an LLM finds them anyway. With the LLM and without the gazetteer, the deterministic stack reaches 96.3% recall at 71.7% precision, close to deid's 97.2% at 74.5% (F4), with no list built for this corpus. This led to D3: the gazetteer only where there is no LLM layer.

## F25. The LLM's second opinion on the clinical model: +18.7 points of precision for 10 spans

*2026-10-18, tuning half, stacks without the gazetteer (F24), gemma4:26b-a4b with the neutral examples, `--llm-second-opinion on`. `LlmSecondOpinion` shows the model each span that no known-identifier, pattern or LLM detector reported, in its context, and drops the ones it names as not PHI.*

| Stack | Recall (95% CI) | Missed | Precision | Word F1 | Non-PHI text removed |
|---|---:|---:|---:|---:|---:|
| Deterministic + LLM | 96.33% (95.11–97.53) | 31 | 71.66% | 78.1% | 0.27% |
| Clinical + LLM | 99.05% (98.12–99.76) | 8 | 50.80% | 61.3% | 0.64% |
| **Clinical + LLM + second opinion** | **97.87% (96.82–98.85)** | **18** | **69.45%** | **76.8%** | **0.31%** |

Paired change from the second opinion: recall −1.18 points (−1.98 to −0.39), precision +18.66 (+16.06 to +20.90). The clinical model's false positives fell from 395 to 66 and its phone recognizer's from 64 to 1; organizations from 129 to 12. The lost spans were mostly locations (99.47% to 95.26%) and dates (98.25% to 97.37%). The second opinion took 0.12 s per note. On the deterministic + LLM stack it changed nothing: all its spans come from patterns or the LLM, which it doesn't judge.

**Meaning:** the model's reading of context removes most of the clinical model's mistakes, as expected of an LLM; it is wrong mainly on locations, where it rejected real hospital or unit names the clinical model alone had found, and saved few false positives (85 to 75). F26 bars it from places.

## F26. A second opinion barred from locations and organizations keeps recall and gains 12 points of precision

*2026-10-18, tuning half, stacks without the gazetteer, gemma4:26b-a4b with the neutral examples. Two refinements of F25's second opinion, each in its own run.*

| Clinical + LLM, second opinion | Recall (95% CI) | Missed | Precision | Word F1 | Non-PHI text removed | Locations |
|---|---:|---:|---:|---:|---:|---:|
| None (F24) | 99.05% (98.12–99.76) | 8 | 50.80% | 61.3% | 0.64% | 99.47% |
| On every category (F25) | 97.87% (96.82–98.85) | 18 | 69.45% | 76.8% | 0.31% | 95.26% |
| Not on locations | 97.63% (96.53–98.66) | 20 | 68.86% | 76.4% | 0.32% | 94.21% |
| **Not on locations or organizations** | **98.82% (97.90–99.52)** | **10** | **62.69%** | **73.9%** | **0.39%** | **99.47%** |

Barring locations alone recovered nothing: the clinical model types hospital names as organizations (its HOSP label), and those were the real places the model had rejected. Barring both, the paired change is −0.24 points of recall (−0.57 to 0.00; 2 dates) for +11.90 of precision (+9.77 to +13.88). The clinical model's false positives fall from 395 to 183, its phone recognizer's from 64 to 1, dates from 212 to 97 and names from 125 to 71; organizations stay at 129.

**Meaning:** the model's context is reliable for rejecting a name, a date or a phone number, but not for rejecting a place. With the bar on both, the second opinion is nearly free in recall. The two precision-first stacks are now the deterministic detectors with the LLM (96.3% at 71.7%) and the clinical stack with the LLM and its second opinion (98.8% at 62.7%). This is the new default `MayVeto`.

## F27. H2: examples retrieved per note add precision, not recall, at a cost in speed

*2026-10-18, gemma4:26b-a4b, stacks without the gazetteer, one paired run per corpus: four fixed neutral examples (F22) against four examples retrieved for each piece of text from `LlmExamplePool.Default` (forty fictional annotated notes, twelve with no PHI) by `TfIdfExampleRetriever`, with the same instructions.*

| Tuning half | Recall (95% CI) | Missed | Precision (95% CI) | Word F1 | Model time per note |
|---|---:|---:|---:|---:|---:|
| Deterministic + LLM, fixed examples | 96.33% (95.11–97.53) | 31 | 71.66% (67.68–75.86) | 78.1% | 0.42 s |
| **Deterministic + LLM, retrieved examples** | **96.33% (94.65–97.75)** | **31** | **76.91% (72.75–81.42)** | **80.2%** | **0.65 s** |
| Clinical + LLM, fixed examples | 99.05% (98.12–99.76) | 8 | 50.80% (47.38–54.59) | 61.3% | |
| Clinical + LLM, retrieved examples | 99.17% (98.39–99.77) | 7 | 53.30% (49.54–57.66) | 62.6% | |

The model's own false positives fell from 255 to 184 (names 70 to 30, identifiers 38 to 19), with recall unchanged and every location found with the clinical stack. On ASQ-PHI the two prompts tie, both at the ceiling: the deterministic + LLM stack reaches 99.63% recall at 99.64% precision. The age filter (F24) also shows there: the clinical stack's precision on ASQ-PHI rose from 74.95% (F22) to 96.82%, since most of its false positives were ages under 90.

**Meaning (H2):** retrieving examples similar to the note helps the model tell what isn't PHI (5 points of precision without the clinical model) but finds no more PHI. It costs about 50% more time per note, because examples that change for each piece of text defeat the server's prompt cache. F28 adds a third arm: all forty examples in every prompt.

## F28. H2, third arm: all forty examples in every prompt give the best precision, at the speed of four

*2026-10-18, tuning half, gemma4:26b-a4b, stacks without the gazetteer, one paired run with three prompts: four fixed neutral examples, four retrieved for each piece of text, and the whole pool (forty fictional notes, about 5,000 tokens) as fixed examples, with the model's window raised to 8,192 tokens (M10; no prompt was truncated).*

| Deterministic + gemma4:26b-a4b | Recall (95% CI) | Missed | Precision (95% CI) | Word F1 | Non-PHI text removed | Model time per note |
|---|---:|---:|---:|---:|---:|---:|
| Four fixed examples | 96.21% (94.94–97.46) | 32 | 74.09% (70.25–77.84) | 79.2% | 0.25% | 0.42 s |
| Four retrieved examples | 96.33% (94.72–97.74) | 31 | 77.00% (72.57–81.37) | 80.1% | 0.23% | 0.72 s |
| **All forty examples** | **95.73% (93.87–97.30)** | **36** | **84.19% (80.66–87.45)** | **84.1%** | **0.18%** | **0.40 s** |

With the clinical stack: 98.93% at 51.91% (fixed), 99.17% at 53.34% (retrieved), 98.70% at 56.24% (all forty). The fixed and retrieved arms repeat F27 within a span or two (M8).

**Meaning (H2):** for precision, showing the model every example beats picking a few similar ones: precision rises 10 points over four fixed examples, and the text removed falls to deid's level (0.18%, F4). It costs no time, because a fixed prompt prefix stays in the server's cache while retrieved examples change for each piece of text. Recall dips by 4 or 5 spans, within the intervals but worth watching. Without any list built for the corpus, this stack has better precision than deid with all its aids (84.2% against 74.5%) at about the recall of deid without them (95.3%, F4).

## F29. All forty examples with the second opinion: 98.5% recall at 71.2% precision

*2026-10-18, tuning half, gemma4:26b-a4b with all forty examples (8,192-token window for the detector; the second opinion on Ollama's default of 4,096, D4's second amendment), stacks without the gazetteer, the second opinion barred from locations and organizations (F26). No request in the server's current log was truncated (M10).*

| Stack | Recall (95% CI) | Missed | Precision (95% CI) | Word F1 | Non-PHI text removed |
|---|---:|---:|---:|---:|---:|
| Deterministic + LLM | 95.73% (93.87–97.30) | 36 | 84.19% (80.66–87.45) | 84.1% | 0.18% |
| Clinical + LLM | 98.70% (97.44–99.61) | 11 | 56.24% (52.30–60.63) | 64.7% | 0.56% |
| **Clinical + LLM + second opinion** | **98.46% (97.30–99.37)** | **13** | **71.16% (67.79–74.68)** | **78.9%** | **0.31%** |

The second opinion's paired change on the clinical stack is −0.24 points of recall (−0.57 to 0.00; 2 spans) for +14.92 of precision (+12.44 to +17.26), as in F26 with the neutral examples (+11.90). On the deterministic stack it changes nothing, as it should: every span there comes from known identifiers, patterns or the LLM, which it may not veto.

**Meaning:** the three tuning-half configurations are now (1) recall first: clinical + LLM, 98.7% at 56.2%; (2) balanced: the same with the second opinion, 98.5% at 71.2%, with recall still above deid's with all its aids (97.2%, F4) and precision near it (74.5%); (3) precision first: deterministic + LLM, 95.7% at 84.2%, no clinical sidecar needed. All three use all forty examples. F30 measures them on the reporting half.

## F30. The precision work on the reporting half: deid's precision with more recall, or more precision at deid's recall

*2026-10-18, **the nursing notes' reporting half** (1,307 notes, 889 gold spans), the third look at that half. The F29 configurations: gemma4:26b-a4b with all forty examples (8,192-token window for the detector, as in F29), no gazetteer (D3), the age rule (F24), the second opinion barred from locations and organizations (F26). No request was truncated (M10). deid's rows are F23's, on the same half.*

| Stack | Recall (95% CI) | Missed | Precision (95% CI) | Word F1 | Non-PHI text removed |
|---|---:|---:|---:|---:|---:|
| Recall first: deterministic + clinical + LLM | 98.88% (97.91–99.58) | 10 | 54.31% (49.32–59.68) | 62.7% | 0.57% |
| **Balanced: the same + second opinion** | **98.20% (96.86–99.24)** | **16** | **70.80% (67.17–74.88)** | **80.5%** | **0.26%** |
| **Precision first: deterministic + LLM** | **96.40% (94.83–97.84)** | **32** | **83.26% (79.51–86.54)** | **85.5%** | **0.15%** |
| F23's best: deterministic with gazetteer + clinical + LLM, four examples | 99.55% (98.89–100.00) | 4 | 38.43% | 50.7% | 1.00% |
| deid, default configuration (all corpus aids) | 97.30% (95.73–98.61) | 24 | 72.57% | 82.3% | 0.18% |
| deid without its corpus aids (F4) | 96.06% (94.16–97.57) | 35 | 72.39% | 82.7% | 0.17% |

The tuning-half numbers hold: each configuration is within a point of recall and two of precision of F29. The second opinion's paired change is −0.67 points of recall (−1.51 to 0.00; 6 spans) for +16.48 of precision (+13.89 to +18.98). The model took 0.38 s per note and the second opinion 0.11 s.

**Meaning:** F24 to F29 took the recall-first stack from 38.4% to 54.3% precision for 6 spans, and gives two more choices. The balanced stack misses 16 spans against deid's 24 with every list built for this corpus, at about deid's precision and removing a little more text (0.26% against 0.18%). The precision-first stack needs no clinical sidecar: it finds as much as deid without its corpus aids, at 11 points more precision, and removes less text than deid. No configuration beats deid on both at once. The precision-first stack's gain in precision is clear, and its recall ties deid's. The balanced stack's lead in recall is within the intervals, and its precision ties. All this comes without any list built for the corpus.

## F31. A second opinion on the model's own findings throws away a quarter of the PHI

*2026-10-19, tuning half, gemma4:26b-a4b with all forty examples, stacks without the gazetteer. `--llm-second-opinion on,self`: the second opinion as in F29, and a self-check that may also veto the LLM's own spans (still never a known identifier's, a pattern's, a location or an organization).*

| Stack | Recall (95% CI) | Missed | Precision | Word F1 |
|---|---:|---:|---:|---:|
| Deterministic + LLM | 95.73% (93.87–97.30) | 36 | 84.19% | 84.1% |
| Deterministic + LLM, self-check | 71.09% (65.77–76.43) | 244 | 87.52% | 70.6% |
| Clinical + LLM + second opinion (F29) | 98.46% (97.30–99.37) | 13 | 71.16% | 78.9% |
| Clinical + LLM, self-check | 80.69% (76.63–84.32) | 163 | 70.28% | 70.4% |

On the deterministic stack the self-check's paired change is −24.64 points of recall (−29.49 to −19.78) for +3.34 of precision. It rejected about 30% of the staff names and half the dates the model had found as a detector; locations, which it may not veto, are unchanged. On the clinical stack it loses 18 points of recall and gains nothing over the second opinion without it.

**Meaning:** asked to list PHI, the model finds it; asked to judge the same text as a candidate, it calls much of it not PHI. Judging is not a check on finding: the two tasks fail differently, and the judge's errors fall on real PHI. Rejected: the second opinion keeps its bar on the LLM's own spans (the default `MayVeto`). The option stays in the evaluation tool as a recorded negative result.

## F32. Next to the model, the pattern detectors add nothing measurable

*2026-10-19, gemma4:26b-a4b with all forty examples, no gazetteer, the age rule. The model alone against the pattern detectors plus the model (the known-identifier detector has nothing to find in either corpus: neither gives a record's identifiers).*

| Stack | Recall (95% CI) | Missed | Precision (95% CI) | Word F1 | Non-PHI text removed |
|---|---:|---:|---:|---:|---:|
| Tuning half: patterns + LLM | 95.73% (93.87–97.30) | 36 | 84.19% (80.66–87.45) | 84.1% | 0.18% |
| **Tuning half: LLM alone** | **95.62% (93.78–97.19)** | **37** | **87.50% (83.95–90.60)** | **88.0%** | **0.11%** |
| ASQ-PHI: patterns + LLM | 99.63% (99.40–99.83) | 11 | 99.57% | 98.7% | 0.14% |
| **ASQ-PHI: LLM alone** | **99.63% (99.40–99.83)** | **11** | **99.57%** | **98.7%** | **0.13%** |

On the nursing notes, dropping the patterns loses one date (−0.12 points of recall, −0.39 to 0.00) and gains 3.31 points of precision (+1.71 to +5.11). On ASQ-PHI, whose queries hold 305 record numbers, 91 health plan numbers, 45 phones and 33 social security numbers, the two stacks find exactly the same spans: the model found every formatted identifier the patterns did.

**Meaning:** one local model finds as much PHI as the model with the pattern library, on both corpora. It also reaches deid's recall without its corpus aids (95.3%, F4) at 13 points more precision, with no list or pattern at all. The patterns stay in the recommended stacks anyway, for three reasons this measurement doesn't cover. They are deterministic: they don't vary between runs (M8) or between model versions. They are the leak guard's detectors, which must not share a model's failure modes. And they find the record's own identifiers, which neither corpus supplies. Their cost in precision on the nursing notes is 30 false positives: 13 numeric dates, 9 names after a title, 7 local phone numbers and an IPv6 address.

## F33. Ablation of the recall-first stack: the model finds most PHI, the clinical model adds the places

*2026-10-19, tuning half, gemma4:26b-a4b with all forty examples, no gazetteer, the age rule, `--ablation on`. Every combination of the three layers that matter: patterns, the clinical model and the LLM. Each layer was asked once per note, and every stack scored the same answers.*

| Layers | Recall (95% CI) | Missed | Precision | Word F1 | Locations | Staff names | Dates |
|---|---:|---:|---:|---:|---:|---:|---:|
| Patterns | 25.71% (20.00–32.95) | 627 | 87.12% | 36.4% | 1.05% | 43.52% | 12.72% |
| Clinical | 86.97% (83.36–89.88) | 110 | 58.33% | 65.0% | 75.26% | 89.37% | 91.67% |
| LLM | 95.62% (93.78–97.19) | 37 | 87.64% | 87.9% | 87.89% | 98.34% | 97.37% |
| Patterns + clinical | 87.68% (84.06–90.52) | 104 | 57.79% | 63.4% | 75.26% | 89.70% | 92.54% |
| Patterns + LLM | 95.73% (93.87–97.30) | 36 | 84.19% | 84.0% | 87.89% | 98.34% | 97.81% |
| Clinical + LLM | 98.70% (97.44–99.61) | 11 | 57.08% | 66.3% | 97.89% | 99.00% | 98.68% |
| **Patterns + clinical + LLM** | **98.70% (97.44–99.61)** | **11** | **56.24%** | **64.6%** | **97.89%** | **99.00%** | **98.68%** |

Leaving one layer out of the full stack:

- **Without the LLM**, recall falls 11.02 points (+8.31 to +14.29 is its paired gain).
- **Without the clinical model**, recall falls 3 points (25 more spans missed, 19 of them locations), and precision rises 28 points.
- **Without the patterns**, recall is identical and precision is 0.84 points higher (+0.46 to +1.25).

**Meaning:** the LLM is the main detector. The clinical model's contribution is the places: the model alone misses 23 of 190 places, and the clinical model, trained with i2b2's hospital label, finds 19 of those. Its other spans cost far more precision than they add. The two models fail differently, so their union beats either one (98.7% against 95.6% and 87.0%), as Zhou et al. found with the same clinical model. The patterns add nothing measurable on these notes (F32). That leaves a question F34 answers: would the clinical model's locations alone, joined with the LLM, keep most of the recall at the LLM's precision?

## F34. The clinical model's places alone with the LLM: the second opinion's balance without a second call

*2026-10-19, tuning half, gemma4:26b-a4b with all forty examples, no gazetteer, the age rule. `--clinical-places on` keeps only the clinical model's locations and organizations (its hospital label maps to organizations), from the same answers as the full model.*

| Stack | Recall (95% CI) | Missed | Precision (95% CI) | Word F1 | Locations |
|---|---:|---:|---:|---:|---:|
| LLM alone (F33) | 95.62% (93.78–97.19) | 37 | 87.50% (83.95–90.60) | 88.0% | 87.89% |
| **Clinical places + LLM** | **97.87% (96.53–98.94)** | **18** | **74.69% (71.74–77.63)** | **83.3%** | **97.89%** |
| Patterns + clinical places + LLM | 97.99% (96.65–99.02) | 17 | 72.63% (69.30–75.85) | 79.9% | 97.89% |
| Clinical + LLM + second opinion (F29) | 98.46% (97.30–99.37) | 13 | 71.16% (67.79–74.68) | 78.9% | 97.89% |
| Clinical + LLM (F33) | 98.70% (97.44–99.61) | 11 | 57.08% (53.19–61.39) | 66.3% | 97.89% |

The places recover every location the full clinical model adds (19 spans). The 7 spans they give up against the full model are 3 dates, 2 staff names, a phone and an other identifier. Of the clinical model's 420 false positives, 162 remain, nearly all organizations (140): the price of keeping its hospital label.

**Meaning:** a fourth choice between the recall-first and precision-first stacks. It has the second opinion's balance (97.9% at 74.7% against 98.5% at 71.2%, within each other's intervals) without the second model call (0.11 s per note) and without a model's judgment between the detectors: the filter is a fixed rule. F35 measures it on the reporting half. Dropping the clinical model's organizations that aren't hospitals would need a rule that tells them apart; none was tried.

## F35. The ablation and the clinical model's places on the reporting half

*2026-10-19, **the nursing notes' reporting half** (1,307 notes, 889 gold spans), the fourth look at that half, for F32–F34. gemma4:26b-a4b with all forty examples, no gazetteer, the age rule, `--clinical-places on --ablation on`. deid's rows are F23's, on the same half.*

| Stack | Recall (95% CI) | Missed | Precision (95% CI) | Word F1 | Non-PHI text removed |
|---|---:|---:|---:|---:|---:|
| Patterns + clinical + LLM (recall first, F30) | 98.88% (97.91–99.58) | 10 | 54.31% (49.32–59.68) | 62.7% | 0.57% |
| Clinical + LLM | 98.88% (97.91–99.58) | 10 | 55.70% (50.61–61.06) | 64.4% | 0.52% |
| Patterns + clinical + LLM + second opinion (balanced, F30) | 98.20% (96.86–99.24) | 16 | 70.80% (67.17–74.88) | 80.5% | 0.26% |
| **Patterns + clinical places + LLM** | **97.41% (95.79–98.64)** | **23** | **72.15% (68.34–76.07)** | **81.3%** | **0.24%** |
| Clinical places + LLM | 97.41% (95.79–98.64) | 23 | 75.41% (71.71–79.04) | 85.1% | 0.18% |
| Patterns + LLM (precision first, F30) | 96.40% (94.83–97.84) | 32 | 83.26% (79.51–86.54) | 85.5% | 0.15% |
| **LLM alone** | **96.40% (94.83–97.84)** | **32** | **88.91% (85.51–91.59)** | **89.8%** | **0.09%** |
| Clinical alone | 86.16% (82.43–89.06) | 123 | 56.06% (50.75–61.55) | 61.9% | 0.47% |
| deid, default configuration (all corpus aids) | 97.30% (95.73–98.61) | 24 | 72.57% | 82.3% | 0.18% |
| deid without its corpus aids (F4) | 96.06% (94.16–97.57) | 35 | 72.39% | 82.7% | 0.17% |

The tuning half's results hold:

- **The patterns** again add no span next to the model. Without them, precision is 5.66 points higher alone (+3.61 to +7.61) and 1.38 higher with the clinical model (F32).
- **The clinical model's places** recover 9 of the model's 12 missed locations. Against the full clinical model they give up 13 spans: 5 relatives' names, 4 staff names, 3 dates and a phone (F34).
- **The second opinion** keeps 7 more spans than the places, at 1.4 points less precision. Every interval overlaps.

**Meaning:** on held-out notes from deid's home corpus, with no list built for the corpus:

- **The LLM alone** finds as much as deid without its aids, at 16.5 points more precision, and removes half as much text that isn't PHI.
- **The clinical model's places with the LLM** match deid with all its aids, in recall and in precision.
- **The recall-first stack** misses 10 spans where deid misses 24.

The patterns and the clinical model are worth their precision only for what this corpus doesn't measure (F32) or for recall above the model's own.

## F36. The baselines, scored by this tool, reproduce what was published about them

*2026-10-19, commit `ee4f226` (v1.0.0), ASQ-PHI and the nursing notes' reporting half, CPU clinical sidecar. deid 1.1 and Philter (philter-ucsf 1.0.3) in their default configurations, run as [the evaluation README](README.md#comparing-with-deid) describes (`--export`, `tools/baselines`).*

| Check | Measured here | Published or earlier |
|---|---:|---:|
| deid on the nursing notes' reporting half, default configuration: recall, precision | 97.30%, 72.57% | 97.30%, 72.57% (F23) |
| deid on ASQ-PHI, read from an `--export`: recall, character recall, precision | 85.85%, 68.45%, 94.69% | 85.85%, 68.45%, 94.69% (F5, converted outside this repository) |
| Philter on ASQ-PHI: character recall (95% CI) | 70.68% (69.86–71.61) | 70.89% (MedDeID, Hellemans et al.) |

Philter handles each note on its own: run in 12 batches, its output on ASQ-PHI is identical, file for file, to one run over all the queries, so the nursing notes were run in batches (one run over all of them still hadn't finished after an hour). On the nursing notes' reporting half it finds 91.45% of the PHI (88.99–93.71) at 6.71% precision and removes 7.81% of the text that isn't PHI: it removes every word its whitelists don't keep. On ASQ-PHI's short queries it finds 96.74% at 93.55%.

**Meaning:** both baselines are measured as their authors run them, and the conversions that put their spans on this tool's offsets are checked against published or independent numbers. Philter is the recall-first baseline (6.75 points more recall than deid with none of the lists built for this corpus, paired 95% CI +3.37 to +10.36, at a tenth of its precision on the nursing notes), deid the precision-first one.

## F37. The results of record on the nursing notes' reporting half

*2026-10-19, **the nursing notes' reporting half** (1,307 notes, 889 gold spans), commit `ee4f226` (v1.0.0), the last look at that half. CPU clinical sidecar; gemma4:26b-a4b with all forty examples (8,192-token window for the detector; corrected 2026-10-28: the second opinion ran on Ollama's default of 4,096, which these notes fit, D4's second amendment); no gazetteer next to the model (D3); the age rule on every stack; deid 1.1 and Philter as in F36. Each language-model stack ran three times with identical results (F38). The commands are in [REPRODUCING.md](../../REPRODUCING.md#every-stack-and-tool-paired).*

| Stack | Recall (95% CI) | Missed | Character recall | Precision (95% CI) | Word F1 | Non-PHI text removed |
|---|---:|---:|---:|---:|---:|---:|
| Recall first: deterministic + clinical + LLM | 98.88% (97.91–99.58) | 10 | 99.00% | 54.31% (49.32–59.68) | 62.72% | 0.57% |
| Balanced: the same + second opinion | 98.20% (96.86–99.24) | 16 | 98.84% | 70.80% (67.17–74.88) | 80.53% | 0.26% |
| Balanced, one call: deterministic + clinical places + LLM | 97.41% (95.79–98.64) | 23 | 98.21% | 72.15% (68.34–76.07) | 81.33% | 0.24% |
| Precision first: deterministic + LLM | 96.40% (94.83–97.84) | 32 | 97.51% | 83.26% (79.51–86.54) | 85.53% | 0.15% |
| The LLM alone | 96.40% (94.83–97.84) | 32 | 97.51% | 88.91% (85.51–91.59) | 89.77% | 0.09% |
| Without an LLM: deterministic (with gazetteer) + clinical | 88.30% (84.71–91.11) | 104 | 86.38% | 42.97% (38.46–47.64) | 51.15% | 0.85% |
| deid, default configuration (all corpus aids) | 97.30% (95.73–98.61) | 24 | 97.73% | 72.57% (67.77–76.77) | 82.27% | 0.18% |
| deid without its corpus aids | 96.06% (94.16–97.57) | 35 | 96.41% | 72.39% (67.40–76.72) | 82.74% | 0.17% |
| deid without those and its hospital list | 84.70% (80.11–88.87) | 136 | 83.17% | 70.37% (65.07–75.06) | 78.34% | 0.17% |
| Philter, default configuration | 91.45% (88.99–93.71) | 76 | 88.00% | 6.71% (5.77–7.63) | 12.51% | 7.81% |

Each DeidSharp stack against deid, paired (the change in points, with its 95% interval; `--compare-with`):

| Stack | vs deid default: recall | precision | vs deid without corpus aids: recall | precision |
|---|---:|---:|---:|---:|
| Recall first | +1.57 (0.00 to +3.27) | −18.26 (−23.52 to −12.55) | +2.81 (+1.04 to +4.80) | −18.08 (−23.43 to −12.26) |
| Balanced | +0.90 (−0.67 to +2.54) | −1.77 (−6.05 to +2.96) | +2.14 (+0.33 to +4.04) | −1.59 (−5.95 to +3.30) |
| Balanced, one call | +0.11 (−1.77 to +2.00) | −0.43 (−4.44 to +4.11) | +1.35 (−0.39 to +3.40) | −0.24 (−4.38 to +4.41) |
| Precision first | −0.90 (−2.93 to +1.16) | +10.68 (+7.02 to +14.82) | +0.34 (−1.62 to +2.50) | +10.87 (+7.23 to +15.15) |
| The LLM alone | −0.90 (−2.93 to +1.16) | +16.34 (+12.32 to +20.90) | +0.34 (−1.62 to +2.50) | +16.52 (+12.46 to +21.21) |
| Without an LLM | −9.00 (−12.80 to −5.90) | −29.61 (−34.39 to −24.21) | −7.76 (−11.38 to −4.74) | −29.42 (−34.35 to −23.90) |

Every stack with the LLM finds more than Philter (+4.95 to +7.42 points of recall, every interval above zero) at many times its precision. The F30 and F35 numbers repeat exactly at the release commit.

**Meaning:** on held-out notes from deid's home corpus, with no list built for the corpus:

- **No configuration finds significantly more PHI than deid with every list built for this corpus.** The recall-first stack's lead (+1.57, 10 spans missed against 24) has a paired interval that starts at zero; the others tie.
- **Against deid without its corpus aids** (it keeps its hospital list, F10), the recall-first and balanced stacks find more (+2.81 and +2.14, both intervals above zero), and the balanced stack ties its precision.
- **The precision-first stack and the LLM alone tie deid's recall** at 10.7 and 16.3 points more precision, paired, and remove less of the other text (0.15% and 0.09% against deid's 0.18%).
- **Without a language model**, the recommended stack trails deid by 9 points of recall and ties deid without any of its corpus lists (+3.60, −2.31 to +9.47).
- Character recall tells the same story: 97.5% to 99.0% for the stacks with the LLM, 97.7% for deid, so the counting by overlap doesn't flatter them.

What these numbers don't cover: they come from one corpus, one hospital's ICU nursing notes from about 2007, with surrogate PHI; the prompt's fictional examples were written after reading the tuning half's misses (F12, F16), so they fit this kind of note; and the gold standard has errors of its own, so some "false positives" may be PHI it missed (Palacios et al.).

## F38. The language model's answers repeat exactly from run to run

*2026-10-19, commit `ee4f226`, the F37 and F39 runs, each made three times in a row.*

All three runs on each corpus gave the same spans for every stack, the same counts and the same intervals, and the same as F30, F32 and F35 at their earlier commits. The two-span variation of M8 (gemma4:12b, 2026-10-18) did not recur with gemma4:26b-a4b at temperature 0.

The model took 0.37 to 0.39 s per note on the nursing notes in each run, the second opinion 0.11 s and the CPU clinical sidecar 0.29 to 0.50 s; on ASQ-PHI's queries the model took 0.51 s per query. The 17.9 GB model doesn't fit the 16 GB card, so Ollama keeps a fifth of it on the CPU; in a trial run before these, made while less GPU memory was free, it took 1.01 s per note, with the same answers.

**Meaning:** in this setup a rerun repeats the results of record; a different model upload, Ollama version or GPU may not, so record them with any rerun (REPRODUCING.md). The speed depends on how much of the model fits on the GPU.

## F39. The results of record on ASQ-PHI

*2026-10-19, ASQ-PHI (1,051 queries, 2,976 gold spans), commit `ee4f226` (v1.0.0), the same configurations as F37, three identical runs. ASQ-PHI has no held-out half and was consulted throughout the work, so these are a second check, not a held-out result.*

| Stack | Recall (95% CI) | Missed | Character recall | Precision | Word F1 | Non-PHI text removed |
|---|---:|---:|---:|---:|---:|---:|
| Recall first: deterministic + clinical + LLM | 99.73% (99.53–99.90) | 8 | 97.87% | 96.65% | 98.08% | 0.57% |
| Balanced: the same + second opinion | 99.66% (99.46–99.86) | 10 | 97.84% | 99.12% | 98.58% | 0.24% |
| Balanced, one call: deterministic + clinical places + LLM | 99.66% (99.46–99.86) | 10 | 97.80% | 99.12% | 98.61% | 0.22% |
| Precision first: deterministic + LLM | 99.63% (99.40–99.83) | 11 | 97.68% | 99.57% | 98.67% | 0.14% |
| The LLM alone | 99.63% (99.40–99.83) | 11 | 97.65% | 99.57% | 98.70% | 0.13% |
| Without an LLM: deterministic (with gazetteer) + clinical | 99.73% (99.53–99.90) | 8 | 96.38% | 95.30% | 97.69% | 0.86% |
| deid, default configuration | 85.85% (84.82–86.98) | 421 | 68.45% | 94.69% | 80.59% | 1.52% |
| Philter, default configuration | 96.74% (96.13–97.34) | 97 | 70.68% | 93.55% | 89.19% | 2.18% |

Paired, the LLM alone finds 13.78 points more than deid (+12.65 to +14.80) and 2.89 more than Philter (+2.28 to +3.47), at 4.89 and 6.02 points more precision.

**Meaning:** every DeidSharp stack with the clinical model or the LLM is at the ceiling on these short queries: 8 to 11 of 2,976 spans missed, one of them a labeling error (the word "email" tagged as an address). Character recall, the measure MedDeID publishes, is 97.7% to 97.9% with the LLM, below MedDeID's 98.9% and above GLiNER's 96.3% (M3). Two cautions: ASQ-PHI's queries were written by GPT-4o and the corpus is public, so a language model may have seen it or its style; and the age rule matters here, since ASQ-PHI, like Safe Harbor, doesn't tag ages under 90.

## F40. The MIMIC-IV-Note surrogate corpus, developed on its tuning half

*2026-10-28, **the discharge summaries' tuning half** (506 discharge summaries, one per patient), surrogate generator 3, the code of D4's run (its tree hashes are in F41); `--gold-review <file> --split tune` for the counts, no detector options for the smoke test. Before D4's run, the reporting half (494 notes) had not been reviewed, measured or written out: the tool builds the whole sample in memory on each run and keeps only the half asked for.*

MIMIC-IV-Note replaced each piece of PHI with `___` and kept no record of what it was, so the corpus types each placeholder by a context rule and fills it with a made-up surrogate that becomes the gold span ([evaluation README](README.md#mimic-iv-note-with-surrogates)). The rules were written from the notes' known structure and from how clinical prose writes PHI; on the tuning half the AI assistant saw only how many spans each rule typed (`--gold-review`'s console counts) and counts of the placeholders' shapes, never the text.

- **Rules.** 30,915 gold spans in 506 notes, about 61 per note. 28 specific rules typed 59% of them: timestamps on result lines 5,334, the follow-up section 2,976 (one placeholder each, filled with one to three appointments), dates after a preposition 1,376, the social history section 1,137, date fields 1,018, and 23 rules with fewer each. The other 41% (12,753) matched no rule.
- **The unmatched placeholders are names.** The maintainer read a random sample of 100 of them in context: every one stood for a first name or a surname, some of them clinicians'. They are filled with a first name or a surname in equal shares, half from the note's clinicians, and labeled as names.
- **Audit.** The maintainer checked every gold span of the first 30 notes of the tuning half's review file, 1,851 spans, for whether the surrogate's kind fits its context: none was wrong. That was generator 1.
- **Generator 2.** An independent code review found that rules ignoring case let a short token in capitals match a lowercase word ("DO" and "do", "US" and "us"), so a placeholder before "do not" was typed a clinician's name; those tokens now match in capitals only. "Mc" surnames now carry their second capital. A placeholder typed differently also changes the draws after it, so some counts moved by more than the retyped spans.
- **Generator 3.** The final review, before D4's run, found two more rule faults. "___, M.D." never matched the credential rule, since the rule wanted a letter after the last dot; on the tuning half no placeholder depended on it, so no count moved. "Seen by ___" and the like matched the date preposition rule, so a clinician's name was filled with a date: a new rule, placed after every rule that reads the word after the placeholder ("by ___ Hospital"), types 15 placeholders as clinicians that generator 2 filled with dates; "followed by" stays a date, since it as often means "and then". Titles now match in their usual case or in capitals, but not "MS" (also multiple sclerosis) or "miss" (a verb): 12 placeholders generator 2 typed as a patient's surname now match no rule and become names. Generator 3 also draws from a new seed, so the follow-up and social history sections, which draw how many people they name, changed their span counts. The counts above are generator 3's.
- **Smoke test.** The deterministic stack (known identifiers, patterns, gazetteer), Safe Harbor scope, 30,054 gold spans: recall 87.27% (86.75–87.78), precision 98.46% (98.28–98.63). By label: clinicians' names 99.91%, patients' names 100%, hospitals 98.97%, untyped names 73.10%, dates 96.28%. Generator 2 gave 86.96% and 98.45%.

**Meaning:** the corpus is fit for D4's run. Its names and places are drawn from the same Census lists as the gazetteer, which explains the deterministic layer's near-perfect recall on names in context; a real note's names are not all in those lists. That flatters only the stack with the gazetteer, the one without a language model (D3), so D4 reports that stack with and without it.

## F41. The discharge summaries' reporting half: the language model finds more than deid at its precision; Philter finds more spans

*2026-10-28, **the discharge summaries' reporting half** (494 discharge summaries, one per patient, 29,023 gold spans), surrogate generator 3, D4 as amended, run once. Code: Git tree hashes `src` `e65eae9de191de77f38233dd8c6357e812cbf884`, `tools` `d1ebd3f2c240ae39691aa790aac44f02b84c74c9`, `sidecar` `0a3cad73a19cd81355e9a0505b2d11e54063a38e`; the first two steps (the published table, the baselines) ran with `tools` at `af4a5f5f4b53b0fa421b484c41da39109f15b850`, which differs only in the second opinion's window, a layer neither step has. CPU clinical sidecar; gemma4:26b-a4b (Ollama 0.40.11, model `001e5dafc3c7`, an RTX 5080 with 16 GB) with all forty examples and an 8,192-token window, the second opinion 16,384 (D4's second amendment); no gazetteer next to the model (D3); the age rule on every stack; deid 1.1 and Philter as in F36. The commands are in [REPRODUCING.md](../../REPRODUCING.md#mimic-iv-note). Added 2026-10-29: commit `7ada415` holds exactly these trees, and `results.json` names it (D5).*

| Stack | Recall (95% CI) | Missed | Character recall | Precision (95% CI) | Word F1 | Non-PHI text removed |
|---|---:|---:|---:|---:|---:|---:|
| Recall first: deterministic + clinical + LLM | 95.28% (94.95–95.59) | 1,371 | 97.08% | 95.94% (95.61–96.27) | 91.20% | 0.78% |
| Balanced: the same + second opinion | 94.91% (94.59–95.23) | 1,476 | 96.86% | 96.70% (96.39–97.02) | 91.53% | 0.74% |
| Balanced, one call: deterministic + clinical places + LLM | 94.55% (94.18–94.91) | 1,582 | 96.61% | 96.82% (96.51–97.12) | 91.82% | 0.70% |
| Precision first: deterministic + LLM | 94.00% (93.63–94.38) | 1,742 | 96.00% | 99.29% (99.17–99.41) | 92.29% | 0.61% |
| The LLM alone | 93.29% (92.84–93.72) | 1,948 | 95.37% | 99.55% (99.45–99.65) | 92.30% | 0.56% |
| Without an LLM: deterministic (with gazetteer) + clinical | 91.57% (91.17–91.98) | 2,446 | 94.21% | 94.76% (94.40–95.13) | 93.95% | 0.42% |
| The same without the gazetteer | 71.64% (70.69–72.56) | 8,232 | 80.75% | 95.18% (94.76–95.59) | 89.64% | 0.23% |
| deid, default configuration | 92.08% (91.67–92.47) | 2,299 | 84.32% | 95.78% (95.43–96.13) | 92.74% | 0.28% |
| deid without its corpus aids | 91.73% (91.30–92.12) | 2,399 | 84.13% | 95.81% (95.45–96.14) | 92.65% | 0.28% |
| deid without those and its hospital list | 91.50% (91.08–91.90) | 2,468 | 83.11% | 95.77% (95.41–96.11) | 92.29% | 0.28% |
| Philter, default configuration | 99.41% (99.31–99.52) | 171 | 83.34% | 72.45% (71.84–73.10) | 82.29% | 2.45% |

Each DeidSharp stack paired with the baselines (the change in points, with its 95% interval; `--compare-with`):

| Stack | vs deid default: recall | precision | vs deid without corpus aids: recall | precision | vs Philter: recall | precision |
|---|---:|---:|---:|---:|---:|---:|
| Recall first | +3.20 (+2.74 to +3.68) | +0.15 (−0.35 to +0.66) | +3.54 (+3.08 to +4.04) | +0.13 (−0.37 to +0.64) | −4.13 (−4.45 to −3.82) | +23.49 (+22.71 to +24.18) |
| Balanced | +2.84 (+2.36 to +3.32) | +0.92 (+0.43 to +1.41) | +3.18 (+2.70 to +3.69) | +0.90 (+0.41 to +1.40) | −4.50 (−4.84 to −4.16) | +24.25 (+23.48 to +24.95) |
| Balanced, one call | +2.47 (+1.98 to +2.98) | +1.03 (+0.54 to +1.52) | +2.82 (+2.32 to +3.34) | +1.01 (+0.52 to +1.51) | −4.86 (−5.21 to −4.50) | +24.36 (+23.60 to +25.06) |
| Precision first | +1.92 (+1.40 to +2.45) | +3.50 (+3.16 to +3.87) | +2.26 (+1.74 to +2.80) | +3.48 (+3.13 to +3.83) | −5.41 (−5.78 to −5.03) | +26.84 (+26.17 to +27.45) |
| The LLM alone | +1.21 (+0.70 to +1.79) | +3.76 (+3.42 to +4.13) | +1.55 (+1.04 to +2.14) | +3.74 (+3.40 to +4.10) | −6.12 (−6.57 to −5.67) | +27.10 (+26.45 to +27.72) |
| Without an LLM | −0.51 (−1.02 to +0.03) | −1.03 (−1.53 to −0.49) | −0.16 (−0.67 to +0.37) | −1.05 (−1.55 to −0.51) | −7.84 (−8.27 to −7.43) | +22.30 (+21.55 to +23.00) |
| Without an LLM or the gazetteer | −20.44 (−21.46 to −19.52) | −0.60 (−1.16 to −0.02) | −20.10 (−21.07 to −19.17) | −0.63 (−1.16 to −0.04) | −27.77 (−28.73 to −26.86) | +22.73 (+21.92 to +23.44) |

**Against D4's criteria:**

- **Met: the precision-first stack and the LLM alone beat deid without its corpus aids** on recall (+2.26 and +1.55, both intervals above zero) with more precision (+3.48 and +3.74).
- **Not met: every stack with the language model finds fewer spans than Philter**, by 4.13 to 6.12 points, every interval below zero. F37's "every stack with the LLM finds more than Philter" does not hold on this corpus.

What the counts show beyond the criteria:

- **Every stack with the language model finds more than deid in every configuration**, the LLM alone included, at the same precision or more; every paired recall interval is above zero, against deid without its hospital list too (in the console's `--compare-with`, not in the table above); on the nursing notes, deid with its corpus lists tied them.
- **Much of that lead is the generator's made-up places.** The generator names a hospital after a random US place or a saint and draws cities from every US place, so deid's lists of real hospitals and places rarely match them, and it finds only what its other rules catch: 73.46% of the hospitals and 66.55% of the cities, against 97.9% to 100% and 98.4% to 99.3% for the stacks with the model. Those places cost deid about 530 spans, 1.8 points of recall: more than the LLM alone's lead over deid's default configuration (+1.21) and about the precision-first stack's (+1.92); the recall-first and balanced stacks lead by more (+3.20 and +2.84). Real notes name real hospitals, which deid's list finds: it adds 11.4 points of recall on the nursing notes (F37) and 0.23 here.
- **Untyped names are where every tool but Philter loses**: 12,632 spans, 44% of the gold, of which the stacks with the model find 86.3% to 89.2% and deid 85.4% to 86.2%. On every typed label, the four stacks with both the model and the deterministic layer find 96.6% to 100%.
- **Philter's recall comes at its usual cost.** It removes every word its whitelists don't keep, so it touches 99.4% of the gold spans; but it covers 83.3% of their characters, against 95.4% to 97.1% for the stacks with the model, and removes 2.45% of the other text, 3.1 to 4.4 times as much as they do (0.56% to 0.78%). A span counts as found when any part of it is removed, for every tool. Per word, the stacks with the model find 95.9% to 97.5% of the PHI words and Philter 95.2%; D4's criterion is span recall, so that doesn't change the verdict. Philter's recall depends on the corpus: 91.45% on the nursing notes (F36), 99.41% here.
- **Without a language model**, the stack ties deid's recall (−0.51, −1.02 to +0.03), where on the nursing notes it trailed by 9 points; but its gazetteer adds 19.9 points here (71.64% without it), against about 3 on the nursing notes (F9), because the corpus draws its names from the gazetteer's lists (F40). On real notes it would fall between the two.
- **The second opinion matters less here**: −0.36 points of recall (−0.45 to −0.28) for +0.77 of precision (+0.65 to +0.89), against +16.5 points of precision on the nursing notes' reporting half. The clinical model's spans are mostly right in these notes, so there is little to drop.
- **Precision is far higher than on the nursing notes** (94.8% to 99.6% against 43% to 89%) for every tool but Philter: a discharge summary holds 59 gold spans on average and a nursing note 0.7, so the same false alarms weigh far less.

How the run went: the third step's first attempt stopped on the second opinion's window (D4's second amendment). In the rerun, the model took 7.0 s per note (the summaries average 10,600 characters, twelve times the nursing notes), the CPU clinical sidecar 3.2 to 8.2 s across the three steps and the second opinion 0.8 s. Ollama's log counted no cut prompt in the rerun. The language-model stacks ran once, not three times as in F38. The numbers here are the console's; the published table ([results](README.md#results)) rounds rates down and intervals outward, so a few of its numbers differ by 0.01.

The clinical sidecar's fix (D4's first amendment): when its model tagged a token made only of spaces, as in the long runs of spaces of form-like headers, the sidecar returned it as a span of whitespace, or as an empty one; DeidSharp's detector refuses an empty span (fail closed), while a whitespace one passes and counts as a false alarm. The sidecar now drops spans of whitespace alone, which hide nothing. Counted with the sidecar from before the fix: on this half, 5 empty and 43 whitespace-only spans in 44 of the 494 notes; on the 2,434 nursing notes, no empty span and 9 whitespace-only ones in 9 notes; on ASQ-PHI's queries, none. So recall in F37 and F39 stands, F39 is unchanged, and across both halves of the nursing notes the fix removes at most 9 false alarms, which could raise the precision of F37's stacks with the clinical model by a fraction of a point, well inside its intervals.

**Meaning:** on a second corpus, a decade later and a different kind of note, the language model's lead over deid holds: every configuration finds more PHI than deid in any of its configurations, at its precision or better. But much of the lead comes from made-up hospitals and cities that deid's lists rarely match; on real notes, the precision-first stack and the LLM alone might only tie deid, as they did on the nursing notes. The lead over Philter does not hold: on these notes Philter touches more spans, at 23 to 27 points less precision, with less of the PHI's characters and several times the other text removed. What these numbers don't cover: one hospital's notes; PHI made up by a generator from public lists, whose names flatter the gazetteer, whose places deid's lists rarely match, and whose formats may be more regular than real PHI; untyped placeholders labeled as names after a sample of 100 on the tuning half; and one run of the language-model stacks.

## F42. The missed untyped names are mostly lone mentions; name propagation finds more, on a corpus whose construction rewards it

*2026-10-28, **the discharge summaries' tuning half** (506 discharge summaries, 30,054 gold spans, 12,795 of them untyped names), surrogate generator 3, Safe Harbor scope; `--split tune --clinical <sidecar> --llm gemma4:26b-a4b --llm-prompts all-examples --gazetteer with,without --variants repeated-names --miss-profile UntypedName`. Run twice, the second time with `--variants` wrapping the stacks with the language model too; every number the two runs share repeated exactly. Code of the second run: Git tree hashes `src` `e65eae9de191de77f38233dd8c6357e812cbf884` and `sidecar` `0a3cad73a19cd81355e9a0505b2d11e54063a38e`, as in F41, and `tools` `29201179e2cad1cde7dc67a90e8c1c77c149dd9d`; the commit that carries this finding has `tools` `bea5155cfc346555e0573819ef318481038fb250`, which reads the word list when the run starts instead of after scoring, so a missing file stops the run at once, and changes comments; neither changes a count. CPU clinical sidecar; the model as in F41, with an 8,192-token window; Ollama's log counted no cut prompt. A paired change can differ from the difference of its two rates by 0.01, from rounding.*

Untyped names are where every tool but Philter loses on these notes (F41). `--miss-profile` counts, for each stack, how many of one label's gold spans it missed among those with each feature, from the gold spans and the note's text; the console shows counts only. A span can have several features, so the rows overlap. For D4's recall-first stack (deterministic without the gazetteer, the clinical model and the LLM) and its precision-first stack (the same without the clinical model):

| The untyped name | Spans | Recall first: missed | Precision first: missed |
|---|---:|---:|---:|
| is an ordinary English word (in the gazetteer's word list) | 1,663 | 349 (20.99%) | 404 (24.29%) |
| is any other name | 11,132 | 1,071 (9.62%) | 1,336 (12.00%) |
| is a word in a gold span of another label in the note (a clinician's full name, say) | 2,934 | 300 (10.22%) | 410 (13.97%) |
| has no other gold span with its text in the note | 8,113 | 1,000 (12.33%) | 1,196 (14.74%) |
| has another, and the stack found one | 4,446 and 4,386 | 335 (7.53%) | 439 (10.01%) |
| has others, and the stack found none | 236 and 296 | 85 (36.02%) | 105 (35.47%) |
| every untyped name | 12,795 | 1,420 (11.10%) | 1,740 (13.60%) |

- **Most misses are lone mentions.** Seven in ten have no other gold span with the same text, though such names are missed at about the average rate. Ordinary English words are missed twice as often (21% against 9.6%), but they are a quarter of the misses. When the stack found none of a name's other mentions, it missed this one too in 36% of cases, against 7.5% when it found one.
- **Name propagation finds most of the repeated ones.** `RepeatedNameDetector` adds every other mention of any word of a name the stack found (F7). Wrapped around the stacks with the model (the change in points, with its paired 95% interval):

  | Stack | Recall | Precision | Word F1 | Untyped names found |
  |---|---:|---:|---:|---:|
  | Recall first | 95.25% → 96.72%: +1.48 (+1.32 to +1.63) | 96.30% → 91.09%: −5.21 (−6.36 to −4.13) | −0.85 (−1.14 to −0.58) | 88.90% → 92.36% |
  | Precision first | 94.00% → 95.97%: +1.97 (+1.78 to +2.16) | 99.33% → 96.97%: −2.35 (−3.16 to −1.71) | 0.00 (−0.19 to +0.15) | 86.40% → 90.99% |

  Of the recall-first stack's missed untyped names it recovers 443 (1,420 to 977): those that are a word of another label's span fall from 300 to 8, and those with another gold span of the same text from 420 to 70 (335 + 85 to 25 + 45). The false alarms it reported, 1,861 on the recall-first stack and 772 on the precision-first one (each counted once for every detector that reported it), cost the precision. With the gazetteer, propagation costs 21 to 22 points of precision on every stack, most likely because it repeats the gazetteer's false names across the note.
- **The corpus's construction rewards it.** Half of the untyped names are drawn from the note's clinicians (F40), and the generator reuses a clinician the note already names more often than it makes up a new one. So many untyped names repeat a name written elsewhere in full, which the stacks with the model find (99.77% or more of clinicians' names): 2,934 (23%) are a word of another label's span, and propagation cuts their misses from 300 to 8, two-thirds of the 443 it recovers. On the nursing notes, real notes with under one gold span each, propagation found one more span of 844 (F7), with a stack without the language model. How often a real discharge summary names a clinician bare, away from their full name, decides its value, and this corpus can't measure it: the sample of 100 behind F40 said only that some untyped placeholders were clinicians'.
- **What is left after propagation**: 977 untyped names on the recall-first stack, 907 of them with no other gold span of the same text and a quarter of them ordinary words (245). The gazetteer next to the model (not a recommended stack, D3) cuts the deterministic, clinical and LLM stack's misses, without propagation, to 672, 329 of them ordinary words, which the gazetteer leaves out by design; the corpus draws its names from the gazetteer's own lists (F40), so that flatters it.
- **What the profile can't tell here.** The generator writes every untyped name as one capitalized word, so the rows for several words, capitals and lowercase are empty or full; only 32 untyped names also appear as a lowercase word elsewhere in their note. F40's sample of 100 found every untyped placeholder to be a name; by the rule of three, up to 3% of them, about 380 (the 95% upper bound), could stand for something else, and those would be names that replaced something else. The credential rule, which types 45 placeholders on this half as clinicians' names, also reads a city before Maryland's or Pennsylvania's abbreviation ("___, MD") as one, so at most 45 gold spans, 0.15%, may hold a clinician's name where a city stood; fixing it would change the corpus F40 and F41 used. The 12,795 untyped names are F40's 12,753 placeholders that matched no rule and the 42 the eponym rule ("___ disease") also labels untyped names.

**Meaning:** the stacks' remaining misses of untyped names are mostly a name's only mention in its note, a quarter of them ordinary words. Whether their sentences give any cue was not measured; no context rule typed them, but that holds for the untyped names the stacks find too. Name propagation adds 1.5 to 2 points of recall to the stacks with the model at 2.4 to 5.2 points of precision, but on a corpus that often repeats the clinicians' names the stacks find; on the short nursing notes it found one span in 844 (F7), which says little about discharge summaries. So the recommended stacks don't change, and propagation stays an experiment in the evaluation tool: its value depends on how real notes repeat names, which needs real gold to measure. Whether a reader would see the lone misses as names is open; reading a sample of them locally (`--misses`) would tell.

## F43. The strict span match: far below the overlap with the gazetteer, close to it without

*2026-10-29, **the nursing notes' tuning half**, **the discharge summaries' tuning half** and **ASQ-PHI** (whole; it has no tuning half), Safe Harbor scope; the deterministic stacks with and without the gazetteer (`--gazetteer with,without`), no sidecar and no model. Code: the commit that carries this finding, which adds the strict match to the scorer and changes no detector.*

The 2014 i2b2/UTHealth challenge's headline score counts an entity as found only when a prediction has exactly its start and end and the right type; its "relaxed" match lets the end differ by up to two characters, and it also reports a binary PHI-or-not evaluation that needs the exact boundaries and ignores the type. DeidSharp's headline recall counts a gold span as found when any prediction overlaps it, since what matters for privacy is whether the PHI was removed. The scorer now also reports the exact-boundary match, categories aside, like that binary evaluation (`EvaluationReport.Strict`):

| Corpus | Stack | Recall | Strict recall | Precision | Strict precision |
|---|---|---:|---:|---:|---:|
| Nursing notes, tuning half | deterministic | 44.19% | 22.87% | 43.41% | 25.70% |
| | without the gazetteer | 25.71% | 15.52% | 87.12% | 56.22% |
| Discharge summaries, tuning half | deterministic | 87.27% | 74.34% | 98.46% | 84.12% |
| | without the gazetteer | 49.48% | 46.66% | 99.64% | 94.60% |
| ASQ-PHI | deterministic | 86.63% | 55.17% | 97.68% | 61.38% |
| | without the gazetteer | 47.58% | 42.61% | 99.72% | 89.11% |

- **With the gazetteer, much of what is found has other boundaries than the gold**: 48% of the gold spans the deterministic stack finds on the nursing notes, 36% on ASQ-PHI and 15% on the discharge summaries, so the strict match costs 21, 31 and 13 points of recall. A prediction that takes in a title or a neighboring word, covers less of the PHI, or covers two gold spans at once (the stack merges overlapping spans into one) removes the PHI and fails the strict match.
- **Without the gazetteer the gap is far smaller**: 40%, 10% and 6% of the found spans, 10, 5 and 3 points of recall. Most of the gap is the gazetteer's spans, and under the strict match its gain nearly disappears on the nursing notes and ASQ-PHI: strict F1 24.20% with it and 24.33% without, 58.11% and 57.65%. Why its boundaries differ, the counts don't say.
- **The discharge summaries' gap is the smallest partly by construction.** The corpus's gold names are surrogates drawn from the same Census lists as the gazetteer's (F40), so their boundaries line up with the gazetteer's more than an annotator's would; its strict numbers flatter the gazetteer, as its recall does.

**Meaning:** strict numbers measure agreement with an annotation convention as much as detection, and here mostly the gazetteer's; a comparison with published strict results holds only on the same corpus and conventions. Runs from now on report both, while the published results predate the strict match. The overlap stays the headline, as the measure of what was removed.

## F44. The model baselines: the Stanford/Penn de-identifier is the strong one, and out of its domain it misses the untyped names

*2026-10-30, **the nursing notes' tuning half**, **the discharge summaries' tuning half** and **ASQ-PHI**, Safe Harbor scope; each model run on an `--export` of the corpus (of its tuning half) by `deidsharp/baseline-models:1` (image `sha256:1c5fa7724ddb`, built from `tools/baselines/models` as the commit that carries this finding has it; an NVIDIA RTX 5080, no network), then scored with `--predictions` next to the deterministic stacks (with and without the gazetteer on the tuning halves, with it on ASQ-PHI). First measured on 2026-10-29 and 30 with the image's earlier CPU build: Stanford/Penn's and GLiNER's numbers repeat exactly on the GPU, and Privacy Filter's move by hundredths (1, 0 and 10 more gold spans missed on ASQ-PHI, the nursing notes and the discharge summaries). No export here holds a carriage return (counted), which the image keeps (it reads the files as bytes). The models, their pinned weights and settings are in [the evaluation README](README.md#comparing-with-model-baselines); nothing was tuned on any corpus.*

| Corpus | Model | Recall (95% CI) | Missed | Character recall | Precision | Word F1 | Strict F1 | Non-PHI removed |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| Nursing notes, tuning half (844 spans) | Stanford/Penn | 94.79% (92.30–96.78) | 44 | 94.09% | 63.00% | 71.14% | 34.60% | 0.37% |
| | OpenAI Privacy Filter | 29.03% (23.90–34.00) | 599 | 30.44% | 52.03% | 31.91% | 9.53% | 0.22% |
| | GLiNER multi-PII | 73.46% (68.79–78.34) | 224 | 74.88% | 12.35% | 22.11% | 6.57% | 2.47% |
| Discharge summaries, tuning half (30,054 spans) | Stanford/Penn | 74.93% (74.08–75.76) | 7,534 | 83.03% | 98.63% | 90.28% | 69.46% | 0.25% |
| | OpenAI Privacy Filter | 29.74% (28.74–30.74) | 21,117 | 30.13% | 96.53% | 45.29% | 34.76% | 0.24% |
| | GLiNER multi-PII | 62.64% (61.74–63.52) | 11,228 | 67.42% | 58.71% | 61.02% | 43.38% | 2.46% |
| ASQ-PHI (2,976 spans) | Stanford/Penn | 99.66% (99.45–99.87) | 10 | 98.23% | 97.71% | 98.32% | 86.70% | 0.56% |
| | OpenAI Privacy Filter | 70.19% (68.95–71.39) | 887 | 64.57% | 96.66% | 80.21% | 58.91% | 1.60% |
| | GLiNER multi-PII | 99.23% (98.90–99.52) | 23 | 99.00% | 70.74% | 81.43% | 72.74% | 13.26% |

For scale, on the same halves: the deterministic stack without the gazetteer and gemma4:26b-a4b with all forty examples (the precision-first stack) found 95.85% at 83.81% precision on the nursing notes, 94.00% at 99.33% on the discharge summaries and 99.63% at 99.57% on ASQ-PHI (F45).

- **On ASQ-PHI the numbers come close to what MedDeID published.** MedDeID reports label-agnostic character recall (M3): 63.48 for OpenAI Privacy Filter and 96.32 for GLiNER multi-PII, against 64.57 and 99.00 here. By its paper and code, MedDeID ran its comparators differently: it merged and extended their spans with its own post-processing before scoring, decoded Privacy Filter token by token instead of with the constrained decoding its package ships (used here), and asked GLiNER for 23 labels of its own in longer windows. So the numbers are close, not equal, and GLiNER's higher recall here comes with 13% of the other text removed.
- **The Stanford/Penn de-identifier is the strongest model baseline everywhere**, close to the stacks with a language model on the nursing notes (94.79% against 95.85%) and on ASQ-PHI (99.66% against 99.63%), at less precision on the nursing notes (63.00% against 83.81%). On the discharge summaries it finds 74.93%: of the untyped names, 43% of the gold spans (12,795 of 30,054), it finds 42.37%, against 86.40% and 88.09% for the precision-first stack with Gemma, with the forty examples and zero-shot (F45); it finds 99.91% to 100% of the staff names, phones, record numbers and hospitals, and 99.95% of the dates. It has no label for ages (0 of 65 here) or for emails and URLs, which is its label set, not a fault. It was trained on i2b2's corpora and radiology reports, so the nursing notes and discharge summaries are out of its domain.
- **OpenAI Privacy Filter finds little clinical PHI.** On the nursing notes it found 11.84% of the dates and 1.58% of the locations: its labels have no hospital or organization, and its model card warns that medical text is out of its domain. On the discharge summaries it found 27.83% of the dates and 15.26% of the untyped names, but 90.67% of the patients' names and 84.30% of the phones. It is a general PII filter, measured here as one.
- **GLiNER multi-PII finds names and the queries' PHI but over-removes**, and on the discharge summaries it finds 42.74% of the dates and 1.78% of the record numbers. The labels it was asked for are Safe Harbor's, in plain words, chosen before any run; another wording might do better, and tuning it on these halves would fit it to them.
- **Runtime.** On the GPU, loading included, the Stanford/Penn model took 26 s on the nursing notes' tuning half and 57 s on the discharge summaries'; GLiNER 75 s and 4 minutes; Privacy Filter, which reads a note whole, 4 and 17 minutes.

**Meaning:** the model baselines run from their published weights at their defaults, and on ASQ-PHI come close to what MedDeID measured, so they can stand in the run of record (D6). The Stanford/Penn de-identifier is the comparison that matters: an open, local, published clinical model that comes close to the stacks with a language model on short notes, and falls behind on the discharge summaries' untyped names, where the language model's reading of context helps most. D6 names it beforehand.

## F45. A model of another family gains as much recall, and the examples buy precision, not recall

*2026-10-29, **the nursing notes' tuning half**, **the discharge summaries' tuning half** and **ASQ-PHI**, Safe Harbor scope; `--llm gemma4:26b-a4b,ministral-3:14b --llm-prompts instructions,all-examples --gazetteer without`, no sidecar (the CPU was running F44's baselines), so each stack is the precision-first one with its model and prompt. ministral-3:14b is Mistral's dense 14B instruct model of December 2025 (Ollama digest `4760c35aeb9d`, Q4_K_M, Apache 2.0), all on the GPU with an 8,192-token window; gemma4:26b-a4b as in F41, Ollama 0.40.11. Code: the evaluation tool of the commit that carries F43. A first attempt stopped on its second stack when Ollama could no longer load a model on the GPU (fail closed); after Ollama was restarted, the run was repeated in full.*

This answers two questions about F37's result: whether it depends on Gemma, and whether the forty examples, written after reading the nursing notes' tuning half, fit the prompt to that corpus. "Zero-shot" is the instructions alone (`LlmPrompt.Default`).

| Corpus | Model, prompt | Recall (95% CI) | Missed | Precision | Strict F1 | Non-PHI removed |
|---|---|---:|---:|---:|---:|---:|
| Nursing notes, tuning half | gemma4:26b-a4b, forty examples | 95.85% (94.02–97.62) | 35 | 83.81% | 55.50% | 0.18% |
| | gemma4:26b-a4b, zero-shot | 95.85% (94.15–97.39) | 35 | 76.87% | 52.12% | 0.23% |
| | ministral-3:14b, forty examples | 93.36% (91.01–95.43) | 56 | 38.18% | 30.09% | 1.00% |
| | ministral-3:14b, zero-shot | 96.56% (94.54–98.24) | 29 | 12.86% | 10.48% | 3.55% |
| Discharge summaries, tuning half | gemma4:26b-a4b, forty examples | 94.00% (93.65–94.37) | 1,804 | 99.33% | 80.44% | 0.67% |
| | gemma4:26b-a4b, zero-shot | 94.83% (94.54–95.15) | 1,555 | 98.85% | 79.02% | 0.75% |
| | ministral-3:14b, forty examples | 91.74% (91.26–92.16) | 2,482 | 94.15% | 78.22% | 0.75% |
| | ministral-3:14b, zero-shot | 93.17% (92.83–93.50) | 2,054 | 90.58% | 77.19% | 0.91% |
| ASQ-PHI | gemma4:26b-a4b, forty examples | 99.63% (99.40–99.83) | 11 | 99.57% | 91.78% | 0.14% |
| | gemma4:26b-a4b, zero-shot | 99.66% (99.46–99.86) | 10 | 99.57% | 92.12% | 0.14% |
| | ministral-3:14b, forty examples | 99.19% (98.86–99.52) | 24 | 99.67% | 92.84% | 0.14% |
| | ministral-3:14b, zero-shot | 99.40% (99.12–99.66) | 18 | 99.57% | 93.36% | 0.28% |

- **The recall gain doesn't depend on Gemma.** Over the deterministic stack alone, ministral-3:14b adds +70.85 points of recall (+62.95 to +77.07) zero-shot on the nursing notes against Gemma's +70.14, and +43.68 (+42.60 to +44.69) on the discharge summaries against Gemma's +45.35, all paired. Its precision is what differs: zero-shot on the nursing notes it reported about 2,500 false identifiers and 1,150 false names, and its precision is 12.86% against Gemma's 76.87%. The examples raise it to 38.18% and cost 3 points of recall. On ASQ-PHI's short queries the two models are within a few spans of each other.
- **The examples buy precision, not recall.** With Gemma, the zero-shot prompt finds as many spans as the forty examples on the nursing notes (35 missed with each) and ASQ-PHI (10 against 11), at 7 points less precision on the nursing notes. On the discharge summaries it finds more: 94.83% against 94.00%, intervals that don't overlap (the tool pairs each prompt with the stack it extends, not the two prompts with each other); the examples cost 1.7 points of the untyped names (86.40% against 88.09%), and they buy 0.5 points of precision.
- **Size and family together.** ministral-3:14b is a dense 14B model, in the class of gemma4:12b rather than the 26B mixture of experts; with the forty examples it found 93.36% on the nursing notes, close to the 93.48% of gemma4:12b with the first examples and the rule filters (F18), next to the deterministic layer of the time. A third family at the 26B model's size was not measured.

**Meaning:** the configuration of record doesn't owe its recall to Gemma or to the examples written from the nursing notes: another family finds about as much, and a prompt with no examples finds as much or more, on both corpora. What the examples and the model's family decide is precision. Nothing here changes a stack: the zero-shot prompt's extra recall on the discharge summaries is under a point, at a cost in precision, and was measured on one corpus's tuning half.

## F46. The run of record: D6's four statements hold, and F37, F39 and F41 repeat

*2026-10-30, **the nursing notes' reporting half** (1,307 notes, 889 gold spans), **the discharge summaries' reporting half** (494 summaries, surrogate generator 3, 29,023 gold spans) and **ASQ-PHI** (1,051 queries, 2,976 gold spans), Safe Harbor scope; D6 as amended, each corpus run once, on commit `2059384` (version 1.1.0; Git tree hashes `src` `3bce79ed15e2e164003e0a53fd75bdd19ee7b2e8`, `tools` `f78ae9d473e22460c2cc45d506c1b28cd113ed9f`, `sidecar` `7a64cc0add19a7ad6751032bb9cb3b4180bed153`). gemma4:26b-a4b (digest `001e5dafc3c7`) on Ollama 0.40.11; the CPU clinical sidecar (image `f828f91086f5`); the model baselines from `deidsharp/baseline-models:1` (image `sha256:1c5fa7724ddb`, F44) on an NVIDIA RTX 5080, run on `--export`s of the reporting halves and of ASQ-PHI. `--clinical-places on --gazetteer with,without --llm gemma4:26b-a4b --llm-prompts all-examples --llm-second-opinion on`, every baseline in `--predictions` and `--compare-with`, `--publish docs/evaluation`; 1,000 resamples by patient (by query on ASQ-PHI). The [published results](README.md#results) are this run's, with rates rounded so they never flatter; this finding quotes the console, rounded to the nearest, so a rate can differ from the table by 0.01.*

"Deterministic" in the stacks with the LLM is without the gazetteer (D3); the stack without one keeps it.

| Corpus | Stack | Recall (95% CI) | Missed | Character recall | Precision (95% CI) | Word F1 | Strict F1 | Non-PHI removed |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| Nursing notes, reporting half | Recall first: deterministic + clinical + LLM | 98.88% (97.91–99.58) | 10 | 99.00% | 54.31% (49.45–59.60) | 62.92% | 46.70% | 0.57% |
| | Balanced: the same + second opinion | 98.20% (96.86–99.24) | 16 | 98.84% | 70.53% (66.77–74.42) | 80.78% | 53.88% | 0.26% |
| | Balanced, one call: deterministic + clinical places + LLM | 97.53% (95.95–98.74) | 22 | 98.29% | 72.03% (68.33–75.80) | 81.75% | 55.03% | 0.24% |
| | Precision first: deterministic + LLM | 96.51% (94.92–97.93) | 31 | 97.59% | 83.18% (79.52–86.49) | 86.02% | 59.33% | 0.15% |
| | Without an LLM: deterministic + clinical | 88.30% (84.71–91.11) | 104 | 86.38% | 43.02% (38.53–47.64) | 51.15% | 40.29% | 0.85% |
| | deid, default configuration | 97.30% (95.73–98.61) | 24 | 97.73% | 72.57% (67.77–76.77) | 82.27% | 68.57% | 0.18% |
| | deid without its corpus aids | 96.06% (94.16–97.57) | 35 | 96.41% | 72.39% (67.40–76.72) | 82.74% | 71.26% | 0.17% |
| | deid without those and its hospital list | 84.70% (80.11–88.87) | 136 | 83.17% | 70.37% (65.07–75.06) | 78.34% | 68.62% | 0.17% |
| | Philter | 91.45% (88.99–93.71) | 76 | 88.00% | 6.71% (5.77–7.63) | 12.51% | 6.18% | 7.81% |
| | Stanford/Penn | 96.06% (94.56–97.27) | 35 | 96.29% | 63.56% (59.66–67.48) | 73.90% | 35.90% | 0.33% |
| | OpenAI Privacy Filter | 32.85% (26.16–38.80) | 597 | 36.35% | 59.16% (52.06–66.39) | 34.09% | 9.13% | 0.18% |
| | GLiNER multi-PII | 76.83% (71.35–81.31) | 206 | 76.61% | 11.08% (9.73–12.67) | 22.03% | 7.56% | 2.48% |
| Discharge summaries, reporting half | Recall first | 95.28% (94.95–95.59) | 1,371 | 97.08% | 95.94% (95.61–96.27) | 91.20% | 75.77% | 0.78% |
| | Balanced | 94.91% (94.59–95.23) | 1,476 | 96.86% | 96.70% (96.39–97.02) | 91.53% | 75.93% | 0.74% |
| | Balanced, one call | 94.55% (94.18–94.91) | 1,582 | 96.61% | 96.82% (96.51–97.12) | 91.82% | 79.12% | 0.70% |
| | Precision first | 94.00% (93.63–94.38) | 1,742 | 96.00% | 99.29% (99.17–99.41) | 92.29% | 81.64% | 0.61% |
| | Without an LLM | 91.57% (91.17–91.98) | 2,446 | 94.21% | 94.76% (94.40–95.13) | 93.95% | 78.87% | 0.42% |
| | deid, default configuration | 92.08% (91.67–92.47) | 2,299 | 84.32% | 95.78% (95.43–96.13) | 92.74% | 75.55% | 0.28% |
| | deid without its corpus aids | 91.73% (91.30–92.12) | 2,399 | 84.13% | 95.81% (95.45–96.14) | 92.65% | 75.36% | 0.28% |
| | deid without those and its hospital list | 91.50% (91.08–91.90) | 2,468 | 83.11% | 95.77% (95.41–96.11) | 92.29% | 75.66% | 0.28% |
| | Philter | 99.41% (99.31–99.52) | 171 | 83.34% | 72.45% (71.84–73.10) | 82.29% | 29.29% | 2.45% |
| | Stanford/Penn | 73.79% (72.82–74.74) | 7,608 | 82.11% | 98.33% (98.10–98.56) | 89.90% | 68.78% | 0.25% |
| | OpenAI Privacy Filter | 28.80% (27.71–29.85) | 20,664 | 29.78% | 96.36% (95.89–96.88) | 44.55% | 34.14% | 0.24% |
| | GLiNER multi-PII | 61.70% (60.83–62.54) | 11,115 | 66.53% | 56.72% (55.47–58.14) | 60.06% | 42.24% | 2.53% |
| ASQ-PHI | Recall first | 99.73% (99.53–99.90) | 8 | 97.87% | 96.65% (95.92–97.28) | 98.08% | 90.59% | 0.57% |
| | Balanced | 99.66% (99.46–99.86) | 10 | 97.83% | 99.12% (98.73–99.43) | 98.58% | 91.79% | 0.24% |
| | Balanced, one call | 99.66% (99.46–99.86) | 10 | 97.79% | 99.12% (98.73–99.43) | 98.61% | 91.79% | 0.22% |
| | Precision first | 99.63% (99.40–99.83) | 11 | 97.68% | 99.57% (99.33–99.78) | 98.68% | 91.78% | 0.14% |
| | Without an LLM | 99.73% (99.53–99.90) | 8 | 96.38% | 95.30% (94.45–96.08) | 97.69% | 73.56% | 0.86% |
| | deid, default configuration | 85.85% (84.82–86.98) | 421 | 68.45% | 94.69% (93.76–95.50) | 80.59% | 33.50% | 1.52% |
| | Philter | 96.74% (96.13–97.34) | 97 | 70.68% | 93.55% (92.80–94.30) | 89.19% | 3.47% | 2.18% |
| | Stanford/Penn | 99.66% (99.45–99.87) | 10 | 98.23% | 97.71% (97.15–98.20) | 98.32% | 86.70% | 0.56% |
| | OpenAI Privacy Filter | 70.19% (68.95–71.39) | 887 | 64.57% | 96.66% (95.83–97.42) | 80.21% | 58.91% | 1.60% |
| | GLiNER multi-PII | 99.23% (98.90–99.52) | 23 | 99.00% | 70.74% (69.41–72.02) | 81.43% | 72.74% | 13.26% |

**D6's statements**, each paired with the Stanford/Penn de-identifier (the change in points, with its 95% interval; `--compare-with`):

| Reporting half | Statement | Paired change | Held |
|---|---|---:|---|
| Nursing notes | (1) The recall-first stack finds more | recall +2.81 (+1.38 to +4.37) | Yes |
| | (2) The precision-first stack loses at most 2 points of recall | recall +0.45 (−1.45 to +2.54) | Yes |
| | and has more precision | precision +19.62 (+14.40 to +24.14) | Yes |
| Discharge summaries | (1) The recall-first stack finds more | recall +21.49 (+20.53 to +22.44) | Yes |
| | (2) The precision-first stack loses at most 2 points of recall | recall +20.21 (+19.23 to +21.20) | Yes |
| | and has more precision | precision +0.96 (+0.73 to +1.19) | Yes |

All four hold. The recall-first stack pays for its recall in precision, which D6 stated nothing about: 9.25 points under Stanford/Penn's on the nursing notes (−12.58 to −5.45) and 2.39 on the discharge summaries (−2.81 to −1.98). The first pass (D6's amendment) gave the same intervals on the nursing notes to the hundredth, as it should: Stanford/Penn's scores are the same on the CPU and the GPU (F44), and the model's answers repeat from run to run (F38). So no statement reads differently in the run of record.

- **The reproduction check.** On the discharge summaries every stack and baseline repeats F41 exactly, column for column. On ASQ-PHI, F39's stacks, deid and Philter repeat its recall, missed spans and precision exactly; two character recalls and one word F1 move by 0.01, so the model's spans moved slightly. On the nursing notes the recall-first and balanced stacks, the stack without an LLM, deid and Philter repeat F37's recall exactly; the one-call and precision-first stacks each find one span more (22 and 31 missed, against 23 and 32), and the stacks' precision moves by −0.27 to +0.05 points. Within "a few spans", as D6 expected. F37 ran on Ollama 0.40.6, and the precision-first stack, which has no clinical model, can only have changed through the model's answers (M8). For the stacks with the clinical model the counts can't tell the model's answers from the sidecar's whitespace fix (D5); the fix drops false alarms, so it would raise precision, and the balanced stacks' small drops (0.27 and 0.12 points) more likely come from the model. The scorer's word measures haven't changed since F37. F37's, F39's and F41's claims read as they did: against deid with its corpus lists on the nursing notes, the recall-first stack's lead (+1.57, 0.00 to +3.27) still starts at zero, and the precision-first stack's recall still doesn't differ from it significantly (−0.79, −2.80 to +1.27), at 10.6 points more precision (+6.79 to +14.87).
- **Stanford/Penn behaves as on the tuning halves (F44).** On the nursing notes it ties deid's recall (−1.24, −3.22 to +0.66) at 9 points less precision. On the discharge summaries it finds 73.79%, and 40.73% of the untyped names (12,632 spans, 44% of the gold), against 89.19% for the recall-first stack and 86.59% for the precision-first one; it has the highest precision of the baselines there (98.33%), 0.96 points under the precision-first stack's. On ASQ-PHI, where nothing was stated beforehand, no stack with the LLM differs from it significantly on recall (the recall-first stack's +0.07, 0.00 to +0.17), and the precision-first stack has 1.86 points more precision (+1.39 to +2.39).
- **On the discharge summaries, D6's margins are the untyped names'.** Without them (16,391 typed gold spans), recall is 99.26% for Stanford/Penn, 99.96% for the recall-first stack and 99.71% for the precision-first one; without the generated hospitals and cities (27,167 spans), the two stacks lead deid by about 1.5 and 0.2 points (94.96% and 93.71% against 93.49%). Both computed from the published recall per label, so unpaired and without intervals. The untyped names are one-word names drawn from the Census lists wherever no context rule fired (F40), which a model reading context finds and a model trained on typed i2b2 labels mostly doesn't.
- **OpenAI Privacy Filter and GLiNER multi-PII trail the four stacks with the LLM** on both reporting halves, by 19.7 to 66.5 points of recall, every interval above zero; GLiNER's recall on ASQ-PHI (99.23%) comes with 13% of the other text removed.
- **The strict match keeps F43's pattern.** On the nursing notes deid, whose annotation conventions the gold standard follows, has the highest strict F1 (68.57% to 71.26%, against 46.70% to 59.33% for the stacks with the LLM and 35.90% for Stanford/Penn); on the discharge summaries the precision-first stack has it (81.64%), and on ASQ-PHI the stacks with the LLM (90.59% to 91.79%, Stanford/Penn 86.70%). The overlap stays the headline.
- **Runtime.** 24 minutes for ASQ-PHI, 24 for the nursing notes' reporting half and 2 hours 14 minutes for the discharge summaries', where the model took 7.5 s per summary.

**Meaning:** under a protocol fixed before the run, D6's two stacks with a language model meet its statements against the strongest published model baseline on both reporting halves: the recall-first stack finds more PHI, at less precision (9.3 and 2.4 points), and the precision-first stack's recall is within D6's 2-point margin of it on the nursing notes and 20 points above it on the discharge summaries, with more precision on both. On the discharge summaries the margins are the untyped names'; on their typed labels Stanford/Penn and both stacks find 99% or more. The recall-first stack's margin on the nursing notes is small (2.8 points), and on the discharge summaries most of the gap is the untyped names, where it was weak on the tuning half too. The earlier results of record repeat, within a span on the nursing notes. Out of the scope of these statements are real notes from another hospital (both reporting halves are MIMIC, one with surrogates whose names come from the gazetteer's lists, F40) and models other than gemma4:26b-a4b (F45 measured one more family on the tuning halves).

## Methodological notes

- **M1. deid's input format.** deid ignores text on the line that carries `||||END_OF_RECORD`; the marker needs a line of its own. A first run on ASQ-PHI with the marker after the text found nothing at all.
- **M2. A transformer trained on i2b2 is weak on nursing notes.** In pyclipse, the Transformer-DeID RoBERTa model found 0.386 of the names in these notes, against 0.969 for PyDeID and deid; merging RoBERTa with PyDeID reached 0.983 recall at F1 0.310.
- **M3. Where our numbers sit on ASQ-PHI.** MedDeID (Hellemans et al., arXiv 2609.10049) reports label-agnostic character recall, the same measure as ours: MedDeID 98.90, GLiNER multilingual PII 96.32, OBI RoBERTa alone 95.45, OpenMed multilingual 75.36, Philter 70.89, OpenAI Privacy Filter 63.48. DeidSharp + clinical: 95.96 after F6 (96.52 before).
- **M4. i2b2 and n2c2 are closed to new registrations**, so results on them can't be produced here; most published numbers are on i2b2 2014.
- **M5. Questions about the gold spans are answered with counts.** A scratch script reads the gold file and prints only counts (how many are in capitals, in a list, one word), so the shape of what is missed can be studied without anyone reading the notes. Only the tuning half is counted.
- **M6. Turn a reasoning model's thinking off.** `qwen3.5:9b` thinks before answering by default in Ollama: a short fictional note took 75 seconds, and came back empty; with thinking off, 0.7 seconds. `LlmDetectorOptions.ConfigureRequest` passes Ollama's switch.
- **M7. Fail-closed stops a run on a cut-off answer.** The first tuning-half run with the LLM stopped at one note whose answer wasn't valid JSON: with a schema enforced, that means the answer hit the output limit. The detector now asks again in two halves when an answer is cut off, down to 200 characters, and still fails closed below that, saying the answer was cut off.
- **M8. A local model varies a little between runs, even at temperature 0.** gemma4:12b with the same prompt on the same notes found 2 more spans in one run than in another (GPU arithmetic and batching aren't bit-for-bit repeatable). Compare LLM variants within one run where possible (the paired gains), and treat a difference of a few spans between runs as noise.
- **M9. A source-generated case-insensitive regex matched nothing in one file.** `\d|(?:jan|feb|mar|...)` with `IgnoreCase` failed on "March" when generated in `LlmPhiDetector`, while the same pattern worked at runtime and when generated in a separate program; the month check is now plain code. Test a filter's rules with mixed case before trusting it.
- **M10. Check the model's context window.** Ollama runs a model with a 4,096-token window unless told otherwise (gemma4:26b-a4b supports 262,144), and cuts a longer prompt from the start, which would drop the instructions. Its server log marks each request `truncated = 0` or `= 1`: of 35,647 requests on 2026-10-18, one was truncated, the thinking-mode speed test of M6, on a fictional note; no evaluation request was. Raise the window (`num_ctx`) for long prompts, and count those markers, never reading the log itself, which with debug logging on can hold the notes.

## Research questions

- **H1. Does a bigger model de-identify better?** (2026-10-18.) Within one family, so the family doesn't confound size: gemma4:e4b, gemma4:12b (F17) and gemma4:26b-a4b (mixture of experts, 4B active), with qwen3.5:9b against qwen3.5:35b-a3b (3B active) as a second family. Measured on the tuning half with the same prompt and filters, both stacks (with and without the clinical sidecar), with paired gains and a few-span noise floor (M8); confirmed on ASQ-PHI and, once, on the reporting half. Speed per note is reported with each, since size costs time.
- **H1b. Does quantization matter?** Dropped on 2026-10-18 before any run: all models here are the default 4-bit (Q4_K_M), and the question of a stronger model is answered by size (H1). A deployment with more GPU memory should measure its own choice.
- **H2. Do retrieved examples beat fixed ones?** (2026-10-18.) Examples picked for each note by similarity from a fictional pool, run with the best model from H1. Answered by F27 and F28: retrieved examples add precision but not recall, and all forty examples in every prompt add more.

## Decisions

- **D1. The LLM layer's success bar** (2026-10-18). It must raise recall by at least 3 points over the best stack without an LLM, on the tuning half, with the paired 95% interval of that gain above zero (`--variants` reports it). The earlier proposal, deid's 95.3%, was dropped: that run still used deid's hospital list (F10). For scale, the gazetteer's gain on the clinical stack is +2.84 points (+1.61 to +4.42).

- **D2. The gazetteer is part of the recommended stack** (2026-10-18). Leaving out spaCy's NER cost 3.4 points of recall on the reporting half (F13); the gazetteer buys back more than that on the tuning half (F9) at a twelfth of the text spaCy cost (0.4% against 11%), so rewinding was rejected. The evaluation tool's "deterministic" layer is now known identifiers, patterns and the gazetteer, so published results measure the recommended stack from the next publication on. Where recall must win at any cost, the sidecar can keep the left-out recognizers (`DEIDSHARP_KEEP_RECOGNIZERS`). The leak guard keeps to known identifiers and patterns.

- **D3. No gazetteer next to an LLM layer** (2026-10-18, approved by the maintainer). With an LLM, the gazetteer adds 2 or 3 spans in 844 and costs 10 to 20 points of precision (F24); without one, it adds about 3 points of recall (F9), so a stack without an LLM layer keeps it.

- **D4. The protocol for the discharge summaries' reporting half** (2026-10-28, approved by the maintainer before any measurement on that half). The results of record (F37) come from one corpus whose reporting half was measured five times. D4 tests them once on a second corpus, from another decade and another kind of note:
  - **Data.** `--corpus mimic-iv-note`, surrogate generator 3 (amended from 2, see below), the reporting half (494 discharge summaries, one per patient), Safe Harbor scope. Its results are looked at once, for this run; the generator, the rules and every detector setting were fixed on the tuning half (F40) or before it.
  - **Stacks.** The F37 configurations unchanged: gemma4:26b-a4b with all forty examples (8,192-token window), the CPU clinical sidecar, no gazetteer next to the model (D3), the age rule on every stack. The stack without a language model is measured with and without the gazetteer (`--gazetteer with,without`), since the corpus draws its names from the gazetteer's lists (F40).
  - **Baselines.** deid 1.1 in the three configurations of F37 and Philter (philter-ucsf 1.0.3) in its default, each run on an `--export` of the reporting half as [REPRODUCING.md](../../REPRODUCING.md) describes. On this corpus, deid's corpus aids were built for another hospital's notes, so none of its configurations has lists made for it. *(Corrected 2026-10-28, after the run: both corpora come from Beth Israel Deaconess, the nursing notes through MIMIC-II. What leaves deid's lists without a match is the generator, which makes up its hospitals and draws its cities from every US place; F41 gives what that does to the comparison.)*
  - **Comparisons.** Every stack paired with deid's default configuration, deid without its corpus aids, and Philter (`--compare-with`), 1,000 resamples by patient.
  - **What would confirm F37:** the precision-first stack and the LLM alone tie or beat deid without its corpus aids on recall (paired interval includes or exceeds zero) with more precision (paired interval above zero); every stack with the language model finds more than Philter (paired interval above zero).
  - **Code.** The run is made once, on the code after an independent review, identified by the Git tree hashes of `src/`, `tools/` and `sidecar/` (`git rev-parse HEAD:src`), which a squashed commit keeps. A run that fails for a technical reason (a sidecar that stops) is repeated with the same settings and the failure is reported. The results are published as the next finding whatever they show; no setting is changed after seeing them. *(Added 2026-10-28: on the development branch, D4 was committed before any run on the reporting half; the branch was squashed into one public commit with the results, so that order is stated here rather than shown by Git history.)*
  - **Amendment** (2026-10-28, before any result). Two attempts at the run stopped before any result was read. The first stopped itself: the clinical sidecar returned a span of whitespace alone, which DeidSharp's detector refused as malformed, so the sidecar now drops such spans (F41). *(Corrected 2026-10-28: the refused span was an empty one, cut from a token of spaces; a whitespace-only span passes and counts as a false alarm. The fix drops both.)* The second was stopped six minutes in, on its first stack, and its output deleted unread, when the final review found the rule faults generator 3 fixes (F40); measuring with faults known, then fixing them, would publish numbers the final code can't rebuild. So the run uses generator 3, and its clinical sidecar is the F37 configuration with that fix and with the security updates of three of its packages (certifi, filelock, setuptools) since F37. Nothing else changes.
  - **Second amendment** (2026-10-28, after the results of the first two steps, which have no language model, had been read, and before any result with one). The run's third step, the language-model stacks, stopped itself on the second opinion: an answer had no verdicts. The evaluation tool gave the second opinion Ollama's default window of 4,096 tokens, as it had in F37, whose short notes fit it (so F37's 8,192-token window held for the detector only); the second opinion sends the whole note, so Ollama cut 65 long discharge summaries from the start, instructions included (its log's warnings, counted). The tool prints its tables only at the end, so no result of that step was seen. The second opinion now gets a 16,384-token window, a deliberate change from F37's configuration that fits the longest note in the sample (35,739 characters); the detector keeps 8,192, since it sends a note in chunks, and it had no prompt cut. The change touches only the second opinion, which the first two steps don't have, so only the third step is rerun. It was reviewed independently after the rerun, not before. The nursing notes and ASQ-PHI's queries are short, and Ollama's logs since 2026-10-19 show no cut prompt, so F37 and F39 are unaffected.

- **D5. A release keeps the results its changes don't affect** (2026-10-29, approved by the maintainer). A published measurement keeps the version and commit it was made at and stays the result of record until a change could alter what the measured stacks find: the detectors' code, the clinical sidecar and its dependencies, the stock Presidio image, the language-model detector's dependencies, or the evaluation tool that runs and scores the stacks. A release that changes only packaging, docs, build settings, or dependencies the measured stacks don't run ships with the published results. Before D5, CI refused a release tag unless every measurement carried the tag's version, which would have measured again the reporting halves that F37 and D4 look at once, for the same numbers. The discharge summaries' measurement, made before its commit existed, now names `7ada415`, which holds exactly the trees F41 gives.
  - **When the numbers could move**, the release measures every corpus whose numbers could move again, the discharge summaries' reporting half included, and publishes each measurement as the next finding whatever it shows. No detector choice is made from it; a new choice still needs the tuning half or new data.
  - **When the measured code changed but the numbers can't move**, or a finding has already counted the change's effect and the published numbers err on the conservative side, the release keeps the results and says why in a line under [Releases](#releases). CI refuses the tag without that line, so a kept result is a recorded judgment, never an oversight.
  - **Since `ee4f226`**, where F37 and F39 were measured, the measured code changed in five ways: the package icon in each project file and the FHIR package's dependency, which no measured stack runs; certifi, filelock and setuptools in the sidecar's locks, which handle certificates, cache locks and installation, not the model's code; Microsoft.Extensions.AI.Abstractions 10.10.1 to 10.11.0, the interfaces the language-model detector calls through, with the model, the prompts and the requests unchanged; the second opinion's window in the evaluation tool, 4,096 to 16,384 tokens, which the nursing notes and ASQ-PHI's queries already fit (D4's second amendment); and the clinical sidecar's whitespace fix (D4's first amendment), which F41 counted at no span on ASQ-PHI and at most 9 false alarms across the nursing notes, recall unchanged, so their published precision is, if anything, slightly low. The evaluation tool's other changes add a corpus and options and leave the scoring as it was.
  - **Since `7ada415`**, where F41 was measured, the evaluation tool and the scorer changed: the miss profile and variants on the stacks with a language model (F42), and the strict match (F43), which add reports, stacks and a measure and leave the published ones as they were.

- **D6. The protocol for the run of record** (2026-10-30, fixed before any measurement on a reporting half since F37 and F41, and committed before any run on one). The paper's tables come from one run per corpus, fixed here. It adds the model baselines (F44) and the strict match (F43) to the configurations of F37 and F41, and changes no detector.
  - **Why another look.** F37 was to be the last look at the nursing notes' reporting half, and D4 measured the discharge summaries' once. D6 looks at both again for one reason: a paper needs every tool scored by one scorer, on the same halves, with the strict match, in one run, and the model baselines came after F37 and F41. No detector or setting is chosen from this look; F37's and F41's results stay the record of their own claims, and this run's differences from them are reported (the reproduction check below).
  - **Data.** The nursing notes' reporting half (1,307 notes, 889 gold spans), the discharge summaries' reporting half (494 summaries, surrogate generator 3, 29,023 gold spans) and ASQ-PHI (1,051 queries, 2,976 gold spans), Safe Harbor scope. No setting was chosen from either reporting half since F37 and F41.
  - **Stacks.** The configurations of F37 and F41 unchanged: gemma4:26b-a4b with all forty examples (8,192-token window, the second opinion 16,384), the CPU clinical sidecar, no gazetteer next to the model (D3), the age rule on every stack, the second opinion as in F37 (`on`) and the clinical model's places (`--clinical-places`). The deterministic layer is measured with and without the gazetteer (`--gazetteer with,without`), so the recommended stack without a model is in the same run; the stacks with the model and the gazetteer are measured too, as a side effect, and D3 still recommends against them. Left out: stock Presidio, no recommended stack, whose numbers stand in F37, F39 and F41; F37's ablation rows (the LLM alone, each layer without the deterministic one), which can't be published and which F37 reports; F45's alternatives (zero-shot, ministral-3:14b).
  - **Baselines.** deid 1.1 in the three configurations of F37 and F41 and Philter (philter-ucsf 1.0.3), from their span files of F37, F39 and F41 (both tools are deterministic, and the exports they ran on are unchanged); the Stanford/Penn de-identifier, OpenAI Privacy Filter and GLiNER multi-PII from `deidsharp/baseline-models:1` with the settings of F44, run on `--export`s of the reporting halves and of ASQ-PHI.
  - **Measures.** Span recall (overlap) with its 95% interval, character recall, precision, word F1, non-PHI text removed and the strict match, per corpus; every stack paired with every baseline (`--compare-with`), 1,000 resamples by patient (by query on ASQ-PHI). Each corpus's run publishes its results (`--publish`), the stacks with the model included, pinned by the model's digest and Ollama's version.
  - **Stated beforehand.** The statements name the Stanford/Penn de-identifier, the model baseline with the highest span recall on both tuning halves and on ASQ-PHI (F44). On each reporting half: (1) the recall-first stack (deterministic without the gazetteer, the clinical model and the LLM) finds more PHI than it (paired recall interval above zero); (2) the precision-first stack (the same without the clinical model) loses at most 2 points of recall to it (the paired recall interval's lower bound above −2 points) and has more precision (paired precision interval above zero). That is four statements, each read from its own 95% interval with no adjustment for multiplicity, and each reported as held or not. On ASQ-PHI, where every tool is near the ceiling, nothing is stated beforehand. F37's and F41's statements about deid and Philter are not tested again; their comparisons are reported.
  - **Reproduction check.** The stacks of F37 and F41 run again on code whose only change to what they find is the clinical sidecar's whitespace fix, which F41 already ran with (D5). F37 and F39 ran on Ollama 0.40.6 and F41 on 0.40.11; this run uses 0.40.11 and the same model digest. Recall is expected to repeat F41's exactly, and F37's and F39's within a few spans, since another server version can change a model's answers (M8); precision moves with the whitespace fix too. Any difference is reported, with its cause where it can be found.
  - **Code.** One commit after an independent review, identified by its hash and the Git tree hashes of `src/`, `tools/` and `sidecar/`, at version 1.1.0; the baseline image by its digest. It holds one library change that changes no span: a detector's exception is named by its type in `PhiDetectionException`'s message, no longer passed on as its inner exception. The gazetteer's fault found since (a capitalized relation word at a sentence start read as part of the name after it, "Daughter Hope") is not fixed in it, since a fix changes what the stacks with the gazetteer find. Each corpus runs once; a run that fails for a technical reason is repeated with the same settings and the failure reported. The results are published as the next finding whatever they show; no setting is changed after seeing them.
  - **Amendment** (2026-10-30, after a first pass on ASQ-PHI and the nursing notes' reporting half, before the discharge summaries). A deviation from "each corpus runs once", for runtime, not a failure. The first pass ran the model baselines on the image's earlier CPU build, where Privacy Filter needs 13 hours per discharge half. The image now runs on a GPU, where Stanford/Penn's and GLiNER's scores are identical at two decimals and Privacy Filter's spans differ by a few per corpus (F44), so F44 was measured again on it and every corpus is run again on the commit that holds it: each corpus's results come from one image and one commit. The first pass's results were read (counts and rates; on the nursing notes both statements held); they are superseded, and F46 says whether any statement reads differently in the run of record. Nothing else changed, and no result of the first pass decided anything.

## Releases

What each release kept of the published results, when the measured code had changed since (D5). A release with no line here measured again or changed nothing the results depend on.

- **v1.1.0** keeps the run of record (F46), measured at `2059384`. Since then `tools/` changed only in how `--publish` guards the published results (a run must measure the headline stacks already published) and reads `results.json` (a broken file is refused before the run); no detector, corpus, scorer or report changed, and `src/` and `sidecar/` are as measured.

## Not tried

- **Site lists** (F10): letting a deployment add its own hospitals, wards and staff names.
- **Telling hospitals from other organizations** in the clinical model's spans (F34).

## References

The works these findings rely on or compare with. Zhou et al. is the closest prior work. The citations PhysioNet asks for, for its dataset, the deid software and PhysioNet itself, are in the [evaluation README](README.md#corpora).

- Zhou Z, Zhang R, Jiao Z, Baird G, Cetintemel U. Improving Clinical Note De-identification via Post-NER Verification and Candidate Expansion. IEEE/ACM CHASE 2026, pp. 422-427. https://doi.org/10.1109/CHASE69719.2026.00072
- Palacios D, Neeley MB, Otto AA, Dhamodharan S, Woodhouse JP, Lin C, Zobeck M, Liu Z, Jeong HH. Institution-Specific LLM Prompting Recovers PHI That De-identification Systems and Their Gold Standards Both Miss. arXiv:2608.17051. 2026.
- Wiest IC, et al. Deidentifying Medical Documents with Local, Privacy-Preserving Large Language Models: The LLM-Anonymizer. NEJM AI. 2025;2(4):AIdbp2400537.
- Neamatullah I, et al. Automated de-identification of free-text medical records. BMC Med Inform Decis Mak. 2008;8:32. https://doi.org/10.1186/1472-6947-8-32
- Norgeot B, et al. Protected Health Information filter (Philter): accurately and securely de-identifying free-text clinical notes. npj Digit Med. 2020;3:57. https://doi.org/10.1038/s41746-020-0258-y (the software: philter-ucsf 1.0.3, https://github.com/BCHSI/philter-ucsf)
- Moore C, Ranisau J, Nelson W, Petch J, Johnson A. Pyclipse, a library for deidentification of free-text clinical notes. arXiv:2311.02748. 2023.
- Hellemans S, Stroobants T, Scheurwegs E, Meysman P, Jorens PG, Laukens K. MedDeID enables locally governed clinical-text de-identification from real or synthetic training data. arXiv:2609.10049. 2026.
- Carrell D, Malin B, Aberdeen J, et al. Hiding in plain sight: use of realistic surrogates to reduce exposure of protected health information in clinical text. JAMIA. 2013;20(2):342–348. https://doi.org/10.1136/amiajnl-2012-001034
- Chambon PJ, Wu C, Steinkamp JM, Adleberg J, Cook TS, Langlotz CP. Automated deidentification of radiology reports combining transformer and "hide in plain sight" rule-based methods. JAMIA. 2023;30(2):318–328. https://doi.org/10.1093/jamia/ocac219 (the Stanford/Penn de-identifier)
- Zaratiana U, Tomeh N, Holat P, Charnois T. GLiNER: Generalist Model for Named Entity Recognition using Bidirectional Transformer. NAACL 2024, pp. 5364–5376. https://doi.org/10.18653/v1/2024.naacl-long.300
- Johnson AEW, Bulgarelli L, Pollard TJ. Deidentification of free-text medical records using pre-trained bidirectional transformers. ACM CHIL 2020, pp. 214–221. https://doi.org/10.1145/3368555.3384455 (how MIMIC-IV-Note's `___` placeholders were placed)
- Weatherhead J, Golovko G, McCaffrey P. ASQ-PHI: An adversarial synthetic data benchmark for clinical de-identification and search utility. Data in Brief. 2026. https://doi.org/10.1016/j.dib.2026.112586 (the data: https://github.com/JamesWeatherhead/asq-phi)
