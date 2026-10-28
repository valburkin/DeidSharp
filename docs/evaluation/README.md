# Evaluation

How well DeidSharp's detector stacks find PHI in annotated text, measured with `tools/DeidSharp.Evaluation.Cli`. What the measurements taught us, and the decisions they led to, are in [findings](findings.md). The numbers below are generated from [results.json](results.json); the README's badge comes from [badge.json](badge.json). [REPRODUCING.md](../../REPRODUCING.md) gives the environment and the commands to rerun any of them.

The badge shows the recall of the deterministic detectors with the clinical sidecar, the recommended stack without a language model, on the held-out half of PhysioNet's notes, rounded down.

## Results at a glance

PhysioNet's reporting half: 1,307 ICU nursing notes, 889 PHI spans, that no choice was tuned on. Measured at `v1.0.0`; each stack with a language model ran three times with identical results ([findings](findings.md), F37 and F38).

| Stack | Recall (95% CI) | Missed | Character recall | Precision | Other text removed |
|---|---:|---:|---:|---:|---:|
| Recall first: deterministic + clinical + LLM | 98.88% (97.91–99.58) | 10 | 99.00% | 54.31% | 0.57% |
| Balanced: the same + second opinion | 98.20% (96.86–99.24) | 16 | 98.84% | 70.80% | 0.26% |
| Balanced, one call: deterministic + clinical places + LLM | 97.41% (95.79–98.64) | 23 | 98.21% | 72.15% | 0.24% |
| Precision first: deterministic + LLM | 96.40% (94.83–97.84) | 32 | 97.51% | 83.26% | 0.15% |
| The LLM alone | 96.40% (94.83–97.84) | 32 | 97.51% | 88.91% | 0.09% |
| No LLM: deterministic + clinical | 88.30% (84.71–91.11) | 104 | 86.38% | 42.97% | 0.85% |
| deid, with the lists built for this corpus | 97.30% (95.73–98.61) | 24 | 97.73% | 72.57% | 0.18% |
| deid without its corpus aids | 96.06% (94.16–97.57) | 35 | 96.41% | 72.39% | 0.17% |
| Philter | 91.45% (88.99–93.71) | 76 | 88.00% | 6.71% | 7.81% |

The LLM is `gemma4:26b-a4b` through Ollama, with `LlmPrompt.WithPoolExamples`. "Deterministic" is known identifiers and patterns, plus the gazetteer in the stack without an LLM only (D3). Paired with deid on the same resamples:

- **No configuration finds significantly more PHI than deid with every list built for this corpus.** The recall-first stack misses 10 spans against deid's 24, but its paired interval (+1.57 points, 0.00 to +3.27) starts at zero.
- **Against deid without its corpus aids**, the recall-first and balanced stacks find more (+2.81 and +2.14 points, both intervals above zero), and the balanced stack ties its precision.
- **The precision-first stack and the LLM alone tie deid's recall** at 10.7 and 16.3 points more precision.
- **Without a language model**, DeidSharp trails deid by 9 points of recall.

On ASQ-PHI (1,051 synthetic clinical queries, a second check with no held-out half), every stack with the clinical model or the LLM removes 99.6% to 99.7% of the PHI, at 95% to 99.6% precision (F39).

On MIMIC-IV-Note (494 held-out discharge summaries from another decade, with surrogate PHI), measured once under a protocol fixed beforehand (D4), every stack with the LLM finds more PHI than deid in any configuration (93.3% to 95.3% against 91.5% to 92.1%), at deid's precision or better, though much of that lead is the generator's made-up hospitals and cities, which deid's lists rarely match. Philter touches more spans (99.4%) at 23 to 27 points less precision, so F37's lead over Philter does not carry over to this corpus (F41).

The generated tables under [Results](#results) hold only the stacks without a language model: those are the ones the tool publishes, because a local model can't be pinned the way the sidecars are. The language-model numbers are the console output logged in the findings.

## Tuning and reporting halves

Changing a detector after reading its misses fits it to the notes that were read, rules as much as models. So PhysioNet's notes are split by patient into two halves: `--split tune` is the half to read misses and false positives on and change detectors against, and `--split report` is the half published numbers come from. Nobody reads the report half's review files. A patient's notes all fall on one side, chosen by a hash of the patient's number that never changes. `--split all` reads both halves, for comparisons with earlier runs.

These are measurements on particular corpora, not a guarantee for your data. Free-text detection is never perfect: measure on a sample of your own notes before relying on any stack ([testing](../testing.md#measuring-recall-on-your-own-data)).

## Corpora

| Corpus | What it is | Access | When it runs |
|---|---|---|---|
| [PhysioNet Deidentified Medical Text](https://physionet.org/content/deidentifiedmedicaltext/1.0/) (`physionet-deid`) | 2,434 ICU nursing notes with their PHI replaced by realistic surrogates; gold spans from the open [deid package](https://physionet.org/content/deid/) | Credentialed PhysioNet account and the dataset's data use agreement; never in this repository | By a maintainer, before each release |
| [MIMIC-IV-Note](https://physionet.org/content/mimic-iv-note/2.2/) (`mimic-iv-note`) | 1,000 discharge summaries (one each from 1,000 patients, a fixed hashed sample), whose `___` placeholders are filled with made-up surrogates that become the gold spans | Credentialed PhysioNet account and the dataset's data use agreement; never in this repository | By a maintainer, for published results |
| [ASQ-PHI](https://github.com/JamesWeatherhead/asq-phi) (`asq-phi`) | 1,051 synthetic clinical search queries, 832 with PHI and 219 hard negatives without | MIT; a copy ships in `tools/DeidSharp.Evaluation.Cli/data/asq-phi` | Every CI build (the deterministic stack's floor); published before each release |

When you publish results on PhysioNet's notes, cite the dataset, the deid paper (its gold standard and baseline), PhysioNet, and MIMIC-III, which the dataset's page lists as its parent project, as PhysioNet asks:

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

1,051 documents · 2,976 gold spans · scope `safe-harbor` · split `all` · DeidSharp 1.0.0 (`ee4f226`) · 2026-10-19 · clinical sidecar on CPU without spaCy NER, URL and license recognizers; stock Presidio 2.2.362

| Stack | Recall (95% CI) | Character recall | Precision | Word F1 | Non-PHI removed | Missed |
|---|---:|---:|---:|---:|---:|---:|
| deterministic | 86.62% (85.46–87.79) | 70.69% | 97.68% | 83.20% | 0.41% | 398 |
| deterministic + presidio | 92.27% (91.37–93.18) | 78.00% | 73.45% | 76.12% | 10.02% | 230 |
| deterministic + clinical | 99.73% (99.53–99.91) | 96.38% | 95.29% | 97.69% | 0.87% | 8 |
| deterministic + presidio + clinical | 99.96% (99.89–100.00) | 96.71% | 91.77% | 96.51% | 1.84% | 1 |

<details><summary>Recall by corpus label</summary>

| Label | Gold spans | deterministic | deterministic + presidio | deterministic + clinical | deterministic + presidio + clinical |
|---|---:|---:|---:|---:|---:|
| GEOGRAPHIC_LOCATION | 829 | 73.46% | 78.89% | 100.00% | 100.00% |
| NAME | 814 | 87.83% | 100.00% | 100.00% | 100.00% |
| DATE | 806 | 98.51% | 99.87% | 99.13% | 100.00% |
| MEDICAL_RECORD_NUMBER | 305 | 90.49% | 92.13% | 100.00% | 100.00% |
| HEALTH_PLAN_BENEFICIARY_NUMBER | 91 | 65.93% | 73.62% | 100.00% | 100.00% |
| PHONE_NUMBER | 45 | 100.00% | 100.00% | 100.00% | 100.00% |
| SOCIAL_SECURITY_NUMBER | 33 | 100.00% | 100.00% | 100.00% | 100.00% |
| EMAIL_ADDRESS | 31 | 96.77% | 96.77% | 96.77% | 96.77% |
| UNIQUE_IDENTIFIER | 14 | 57.14% | 64.28% | 100.00% | 100.00% |
| ACCOUNT_NUMBER | 4 | 100.00% | 100.00% | 100.00% | 100.00% |
| FAX_NUMBER | 2 | 100.00% | 100.00% | 100.00% | 100.00% |
| CERTIFICATE_LICENSE_NUMBER | 1 | 100.00% | 100.00% | 100.00% | 100.00% |
| IP_ADDRESS | 1 | 100.00% | 100.00% | 100.00% | 100.00% |

</details>

### PhysioNet Deidentified Medical Text 1.0

1,307 documents · 889 gold spans · scope `safe-harbor` · split `report` · DeidSharp 1.0.0 (`ee4f226`) · 2026-10-19 · clinical sidecar on CPU without spaCy NER, URL and license recognizers; stock Presidio 2.2.362

| Stack | Recall (95% CI) | Character recall | Precision | Word F1 | Non-PHI removed | Missed |
|---|---:|---:|---:|---:|---:|---:|
| deterministic | 42.06% (37.23–46.29) | 48.40% | 43.77% | 36.98% | 0.41% | 515 |
| deterministic + presidio | 66.36% (60.99–71.11) | 69.68% | 6.16% | 10.79% | 5.54% | 299 |
| deterministic + clinical | 88.30% (84.70–91.12) | 86.37% | 42.96% | 51.15% | 0.85% | 104 |
| deterministic + presidio + clinical | 90.32% (87.09–93.00) | 88.02% | 8.11% | 14.66% | 5.70% | 86 |

<details><summary>Recall by corpus label</summary>

| Label | Gold spans | deterministic | deterministic + presidio | deterministic + clinical | deterministic + presidio + clinical |
|---|---:|---:|---:|---:|---:|
| HCPName | 292 | 60.61% | 86.30% | 89.04% | 93.49% |
| Date | 254 | 12.59% | 45.27% | 91.73% | 92.51% |
| Location | 177 | 36.72% | 47.45% | 76.83% | 77.96% |
| RelativeProxyName | 92 | 54.34% | 80.43% | 90.21% | 91.30% |
| PTName | 37 | 78.37% | 100.00% | 100.00% | 100.00% |
| Phone | 35 | 60.00% | 80.00% | 100.00% | 100.00% |
| Other | 1 | 0.00% | 0.00% | 100.00% | 100.00% |
| PTNameInitial | 1 | 0.00% | 0.00% | 0.00% | 0.00% |

</details>

### MIMIC-IV-Note 2.2 discharge summaries with surrogate PHI (generator 3)

494 documents · 29,023 gold spans · scope `safe-harbor` · split `report` · DeidSharp 1.0.0 · 2026-10-28 · clinical sidecar on CPU without spaCy NER, URL and license recognizers; stock Presidio 2.2.362; the code is identified by the tree hashes in findings F41

| Stack | Recall (95% CI) | Character recall | Precision | Word F1 | Non-PHI removed | Missed |
|---|---:|---:|---:|---:|---:|---:|
| deterministic | 87.07% (86.53–87.62) | 82.48% | 98.19% | 91.08% | 0.24% | 3,752 |
| deterministic + presidio | 92.95% (92.61–93.31) | 87.81% | 65.10% | 71.57% | 3.31% | 2,045 |
| deterministic + clinical | 91.57% (91.16–91.99) | 94.21% | 94.75% | 93.94% | 0.42% | 2,446 |
| deterministic + presidio + clinical | 95.05% (94.75–95.33) | 96.53% | 64.27% | 74.06% | 3.40% | 1,436 |

<details><summary>Recall by corpus label</summary>

| Label | Gold spans | deterministic | deterministic + presidio | deterministic + clinical | deterministic + presidio + clinical |
|---|---:|---:|---:|---:|---:|
| UntypedName | 12,632 | 73.29% | 85.76% | 80.69% | 88.67% |
| Date | 9,115 | 96.10% | 97.55% | 99.96% | 99.97% |
| StaffName | 2,049 | 100.00% | 100.00% | 100.00% | 100.00% |
| Hospital | 1,300 | 98.92% | 98.92% | 100.00% | 100.00% |
| Phone | 1,166 | 100.00% | 100.00% | 100.00% | 100.00% |
| PatientName | 1,149 | 100.00% | 100.00% | 100.00% | 100.00% |
| City | 556 | 98.92% | 98.92% | 99.28% | 99.28% |
| RelativeName | 538 | 100.00% | 100.00% | 100.00% | 100.00% |
| RecordNumber | 489 | 100.00% | 100.00% | 100.00% | 100.00% |
| Age | 29 | 86.20% | 86.20% | 100.00% | 100.00% |

</details>

<!-- results:end -->

## How the numbers are counted

- **Recall**: the share of gold spans that some prediction overlaps, whatever category it gives them. A missed span is leaked PHI, so this is the number that matters.
- **Character recall**: the share of gold characters covered. A partly covered name still leaks its uncovered part.
- **Precision**: the share of predicted spans that overlap a gold span. A false positive costs clinical text, not privacy.
- **Word F1**, with word recall and precision on the console: the units Philter and pyclipse publish. A word is a run of letters or digits; it is PHI if any of its characters is gold, and predicted if a prediction covers any of its characters. Label-agnostic.
- **Non-PHI removed**: the share of the non-whitespace characters outside gold PHI that a prediction covers, the clinical text lost (MedDeID reports the same rate). A year on its own counts as non-PHI in the `safe-harbor` scope, so removing it counts here.
- **95% CI**: a percentile bootstrap, 1,000 resamples of the patients (or, in a corpus without patients, the documents), from a fixed seed so a rerun repeats it. A patient's notes share names and dates, so they are resampled together. Every stack of a run is measured on the same resamples. The console shows intervals for every headline number; the table, for recall. Rates are rounded so they never flatter: down where higher is better, up for the text removed, and intervals outward.
- **Scope `safe-harbor`**: the labels DeidSharp removes under Safe Harbor. A year on its own (PhysioNet's `DateYear`) is left out, because Safe Harbor keeps it. Staff names stay in, because DeidSharp removes every name.
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

# PhysioNet: id.text from the dataset, id-phi.phrase from the deid package
dotnet run --project tools/DeidSharp.Evaluation.Cli -c Release -- --corpus physionet-deid \
  --text /data/physionet/id.text --gold /data/physionet/deid/id-phi.phrase \
  --presidio http://127.0.0.1:5002/ --clinical http://127.0.0.1:5003/ \
  --misses ~/deidsharp-misses.txt --false-positives ~/deidsharp-false-positives.txt

# MIMIC-IV-Note: note/discharge.csv.gz from the dataset, unmodified
dotnet run --project tools/DeidSharp.Evaluation.Cli -c Release -- --corpus mimic-iv-note \
  --notes /data/mimic-iv-note/2.2/note/discharge.csv.gz --clinical http://127.0.0.1:5003/
```

The deterministic stack (known identifiers, patterns and the gazetteer, the deterministic part of the recommended stack) always runs; each `--presidio` or `--clinical` adds a stack, and both add a stack with all three. Each note goes to each sidecar once: the stack with both reuses the answers. The CPU and GPU builds of the clinical sidecar can differ on a borderline token, so publish from one and keep to it. `--scope all` counts every label. The console shows counts and rates only, including the false positives counted by the category the stack gave them and by the recognizer that reported them ("clinical:SpacyRecognizer").

`--predictions deid=id.phi,philter=philter-spans.tsv` also scores other tools, each from a file of its spans, so every tool is measured by the same scorer on the same notes and split. A `.phi` file is read as deid writes it (below). For any other tool, run it in its own environment and convert its output to one span per line, tab-separated: the document's id in the corpus (`patient 3, note 2`, `query 815`), the start and end (UTF-16 code units, end exclusive, in the text the corpus reader builds; for PhysioNet that is the deid software's own offsets), and optionally a DeidSharp category (`Name`). The file holds positions only, never text. Score each tool in its default or recommended configuration, and say which.

`--compare-with deid,philter` also compares every stack with those prediction files on the same resamples: the console shows each stack's change from the tool in recall, precision and word F1, with a paired 95% interval. Two stacks' own intervals can overlap while the paired one excludes zero, so this is the test to quote when one stack is said to beat another.

`--export <folder>` writes the corpus (or one `--split` of it) for other tools to read, and measures nothing: one UTF-8 text file per document under `text/`, every document as a deid record in `deid.text`, and `documents.tsv`, which gives each file's key and its document's id. The folder holds corpus text, so the tool refuses one inside a Git working tree. [`tools/baselines/to-predictions.cs`](../../tools/baselines/to-predictions.cs) turns deid's and Philter's output on an export into the TSV above.

`--gold-review <file>` writes every gold span with its label in its context, and for `mimic-iv-note` the rule that chose it, so a person can check a generated corpus; the console shows how many spans each rule typed. It measures nothing, holds corpus text, and is refused inside a Git working tree.

### Comparing with deid

PhysioNet's [deid software](https://physionet.org/content/deid/1.1/) (Perl; GNU GPL version 2 or later on PhysioNet's page, the GNU *Library* GPL version 2 or later in `deid.pl`'s header) is the corpus's own baseline: the notes were its development set. Nothing of deid is in this repository; you run it yourself, and the tool reads the positions it writes. Scored here over all notes and every label (`--scope all`), its output matches the figures in its paper: recall 96.68% (0.967) with the same 59 spans missed, and precision 74.83% against the paper's 0.749. That is an independent check of this scorer. Those figures come from deid's default configuration, which uses lists from this corpus (below), so they are not comparable with DeidSharp's results above.

1. Download deid 1.1 from PhysioNet, and copy `id.text` into a folder outside any Git working tree.
2. From deid's folder, run `perl deid.pl <folder>/id deid-output.config` (a few minutes).
3. Score it with `--predictions deid=<folder>/id.phi`.

deid's default configuration uses help from this corpus: each record's (surrogate) patient names, lists of the doctors and local places that appear in these notes, and a rule for one of the hospital's wards. For a run with the text alone, as DeidSharp's stacks get it, set `PID to patient name mapping`, `Doctor names`, `LocalPlaces names` and `GS filter` to `n` in a copy of the configuration. Its hospital list (`Hospital names`) also fits this corpus: it alone matches more than half of the gold locations, so a run without any help from the corpus sets that to `n` too ([findings](findings.md), F10). Report which configuration you ran.

deid also writes `id.res` and `id.info`, which hold note text: treat them like the review files above. Only `id.phi` (positions) is read here.

On ASQ-PHI, deid reads an export instead: `--corpus asq-phi --export <folder>`, then `perl deid.pl <folder>/deid deid-output.config`, then `dotnet run tools/baselines/to-predictions.cs -- deid <folder> <folder>/deid.phi deid-asq.tsv`, scored with `--predictions deid=deid-asq.tsv`. deid counts bytes, and the converter maps them to the characters the tool counts.

### Comparing with Philter

[Philter](https://github.com/BCHSI/philter-ucsf) (philter-ucsf 1.0.3, Python, BSD 3-Clause; Norgeot et al. 2020) is the other open rule-based baseline. Nothing of it is in this repository either: [`tools/baselines/philter`](../../tools/baselines/philter) builds an image that installs it from PyPI, pinned by hash, on the Python 3.10 its regexes need, and runs its default configuration (`philter_delta.json`) with no network.

1. Export the corpus: `--corpus <corpus> <its files> --export <folder>`, outside any Git working tree.
2. `docker build -t deidsharp/philter:1.0.3 tools/baselines/philter`, then `docker run --rm --network none -v <folder>/text:/data/in:ro -v <output>:/data/out deidsharp/philter:1.0.3`. Philter is plain Python on one core (rules and a POS tagger, nothing a GPU runs), and slow on long notes: a few minutes for ASQ-PHI, more than an hour for PhysioNet's notes. It handles each file on its own, so splitting the `text` folder into batches, one container each, all writing to the same output folder, gives the same output in a fraction of the time.
3. `dotnet run tools/baselines/to-predictions.cs -- philter <folder> <output> philter.tsv`, scored with `--predictions philter=philter.tsv`.

Philter removes every letter and digit its whitelists don't keep, so its spans are the runs of characters it replaced with `*`, one per word; compare it on word F1 and character recall more than on span precision. Scored this way on ASQ-PHI, its character recall is within a quarter of a point of what MedDeID published ([findings](findings.md), F36). Its output folder holds note text, like deid's `.res`.

`--variants repeated-names` also measures each stack with a change (`StackVariants` lists them), each variant right after its stack, so one run compares the two with the same resamples: the console shows each variant's change from its stack in recall, precision and word F1, with a paired 95% interval. A change is real when its interval excludes zero. `repeated-names` wraps a stack in `RepeatedNameDetector`, which looks for the other mentions of every name the stack found. Like `--drop-sources`, it can't be combined with `--publish`.

`--llm qwen3.5:9b,gemma4:12b` also measures each stack with an LLM layer (`LlmPhiDetector`) on a local Ollama server (`--llm-endpoint`, default `http://127.0.0.1:11434/`; the tool refuses one that isn't on this machine, since the notes are sent to it), each right after its stack with its paired gain. Each model reads each note once, with thinking turned off: reasoning first makes a note take a minute instead of a second. `--llm-prompts instructions,examples` measures each model with each prompt (`LlmPrompts` lists them; `examples` adds four short fictional notes and their answers). `--ablation on` also measures each Presidio service, each model, and each service with each model, without the deterministic layer, each paired with the deterministic layer plus the same layers, so the change shows what the deterministic detectors add. `--clinical-places on` also measures the clinical sidecar keeping only its locations and organizations, from the same answers. `--llm-second-opinion on,self` also measures each LLM stack with the model's second opinion on the other detectors' spans (`on`), or on its own spans as well (`self`). Like `--variants`, it can't be published.

`--drop-sources clinical:SpacyRecognizer,clinical:UrlRecognizer` leaves out the spans that only those recognizers reported, so you can see what each one adds to recall and costs in precision before changing a sidecar. It measures a stack nobody runs, so it can't be combined with `--publish`.

`--note` publishes one line with the results, for what the tool can't see: which images ran, and on CPU or GPU. `--render docs/evaluation` rewrites the table and the badge from `results.json` without measuring, after a change to how they are shown.

`--misses` writes each missed span, and `--false-positives` each predicted span that is not gold PHI (with the detectors that reported it), with 40 characters of context on each side, for you to read and decide what to change. The tool refuses either path inside a Git working tree. Under PhysioNet's data use agreement, keep those files on your machine: don't commit them, share them, or paste them into an online service or an AI assistant.

## Before a release

1. Set the release's version in `Directory.Build.props`.
2. Run PhysioNet with `--split report` and ASQ-PHI with `--split all`, both with both sidecars, `--publish docs/evaluation` and a `--note` naming the sidecar build (CPU or GPU), from a clean checkout of the commit to be tagged. This rewrites `results.json`, the table above and `badge.json`. Measure the language-model stacks and the baselines at the same commit, and log them in [findings](findings.md) ([REPRODUCING.md](../../REPRODUCING.md) has the commands).
3. Read the misses and the false positives of a `--split tune` run, never the report half's. Turn a pattern worth fixing into a fictional regression test; never commit corpus text.
4. Commit `docs/evaluation` on top of the measured commit, changing no code, and tag that commit `v<version>`. CI refuses the tag if `results.json` holds a measurement from another version.
5. Push the tag. Once every check passes, CI's publish job packs the five libraries with their symbols and pushes them to nuget.org by Trusted Publishing, with no stored API key (a policy for this repository and `ci.yml` on nuget.org, the `NUGET_USER` repository variable, and the `nuget` environment, which can require an approval). nuget.org can't delete a version, only unlist it.
