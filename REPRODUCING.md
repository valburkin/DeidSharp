# Reproducing the evaluation

How to rerun the measurements of record, [findings](docs/evaluation/findings.md) F36 onward and the [results table](docs/evaluation/README.md#results), from the `v1.0.0` tag on your own machine; findings F40 onward (the discharge summaries, and F43's strict match) need later code (see [Commits](#commits)). Each finding names its date, corpus, half and settings in the italic line under its heading; this page gives the environment and the commands those settings plug into.

The published table (`results.json`) holds every stack and tool of the last run that `--publish` accepted for each corpus. Until D6's run of record it holds the measurements of F36 to F41 without a language model; D6's runs add the recommended stacks with one (the model pinned by its digest and Ollama's version) and the other tools compared with them. Experiments are never published; their numbers, and those of the stacks with a language model before D6, are the console output logged in the findings.

## What you need

| Component | Version | Notes |
|---|---|---|
| .NET SDK | as pinned in `global.json` (10.0.401, later feature bands allowed) | Every NuGet package is locked (`packages.lock.json`) |
| Docker | any recent | For the sidecars and Philter |
| Clinical sidecar | built from `sidecar/` | `obi/deid_roberta_i2b2` at a fixed commit, packages hash-locked. The measurements of record used the CPU build (`clinical`, port 5003), so the GPU kept the language model. The GPU build (`clinical-gpu`) can differ on a borderline token, so compare like with like |
| Stock Presidio | the digest pinned in `docker-compose.yml` (2.2.362) | Only for the stacks with `--presidio` |
| Ollama | 0.40.6 for F37 to F39, 0.40.11 from F41 on (D6's run included) | Only for stacks with a language model; it must run on the same machine (the tool refuses any other). `--publish` records the version and the model's digest in `results.json` |
| Language model | `gemma4:26b-a4b`, Q4_K_M, digest `001e5dafc3c77684c2307ebc6ab8e336e10c9b18eca52acf547d72fc83c3ca8c` | Temperature 0 and thinking off (set by the tool); the `all-examples` prompt raises the context window to 8,192 tokens. Measured on an NVIDIA RTX 5080 (16 GB) |
| deid | 1.1, from PhysioNet | A baseline; you download and run it yourself ([evaluation](docs/evaluation/README.md#comparing-with-deid)) |
| Philter | philter-ucsf 1.0.3 | A baseline; `tools/baselines/philter` builds its image ([evaluation](docs/evaluation/README.md#comparing-with-philter)) |
| Model baselines | `tools/baselines/models` (`deidsharp/baseline-models:1`) | The Stanford/Penn de-identifier, OpenAI Privacy Filter and GLiNER multi-PII, weights pinned by commit, packages by hash; an NVIDIA GPU (CUDA 13.0: Turing or later, driver 580 or later), no network ([evaluation](docs/evaluation/README.md#comparing-with-model-baselines)). F45 also needs `ollama pull ministral-3:14b` (digest `4760c35aeb9d…`) |

## Corpora

- **ASQ-PHI** ships in `tools/DeidSharp.Evaluation.Cli/data/asq-phi` (MIT) and needs no flags beyond `--corpus asq-phi`.
- **PhysioNet's Deidentified Medical Text 1.0** (the nursing notes) needs a credentialed PhysioNet account and its data use agreement. Download `id.text` from the dataset and `id-phi.phrase` from the deid package, and keep both outside any Git working tree. Never commit them, share them, or paste their text into an online service or AI assistant.
- **MIMIC-IV-Note 2.2** (the discharge summaries) needs the same account and the dataset's own data use agreement. Download `note/discharge.csv.gz` and keep it as it is: the tool checks its SHA-256 against the release's, then builds the corpus from it on each run (`--corpus mimic-iv-note --notes <discharge.csv.gz>`).

The notes split by patient into a tuning half (`--split tune`) and a reporting half (`--split report`), by the lowest bit of the first byte of the SHA-256 of `deidsharp-split:patient ` followed by the patient's number (`deidsharp-split:patient 10`), 0 for tuning. Confidence intervals are a percentile bootstrap over patients (documents in ASQ-PHI), 1,000 resamples from the fixed seed in `Scoring/Bootstrap.cs`, so a rerun repeats them exactly.

## Commands

In what follows, `<id.text>` and `<id-phi.phrase>` are your copies of the nursing notes' files, and `<work>` is a folder outside any Git working tree for the other tools' input and output, which hold corpus text.

Start the sidecars and pull the model:

```bash
docker compose up -d --build presidio clinical   # 127.0.0.1:5002 and 127.0.0.1:5003
ollama pull gemma4:26b-a4b                       # only for --llm
```

### The published table

```bash
dotnet run --project tools/DeidSharp.Evaluation.Cli -c Release -- --corpus physionet-deid \
  --text <id.text> --gold <id-phi.phrase> --split report \
  --presidio http://127.0.0.1:5002/ --clinical http://127.0.0.1:5003/

dotnet run --project tools/DeidSharp.Evaluation.Cli -c Release -- --corpus asq-phi \
  --presidio http://127.0.0.1:5002/ --clinical http://127.0.0.1:5003/
```

### The other tools' spans

deid on the nursing notes reads `id.text` as it comes, once per configuration ([evaluation](docs/evaluation/README.md#comparing-with-deid) says which settings each one turns off). Copy `id.text` into `<work>/deid-default`, `<work>/deid-generic` and `<work>/deid-nohosp`, and from deid's folder:

```bash
perl deid.pl <work>/deid-default/id deid-output.config
perl deid.pl <work>/deid-generic/id deid-generic.config      # without the corpus aids
perl deid.pl <work>/deid-nohosp/id deid-generic-nohosp.config  # without those and the hospital list
```

ASQ-PHI and Philter read an export:

```bash
dotnet run --project tools/DeidSharp.Evaluation.Cli -c Release -- --corpus asq-phi --export <work>/asq
dotnet run --project tools/DeidSharp.Evaluation.Cli -c Release -- --corpus physionet-deid \
  --text <id.text> --gold <id-phi.phrase> --export <work>/physionet

perl deid.pl <work>/asq/deid deid-output.config     # from deid's folder
dotnet run tools/baselines/to-predictions.cs -- deid <work>/asq <work>/asq/deid.phi <work>/deid-asq.tsv

docker build -t deidsharp/philter:1.0.3 tools/baselines/philter
docker run --rm --network none -v <work>/asq/text:/data/in:ro -v <work>/philter-asq:/data/out deidsharp/philter:1.0.3
docker run --rm --network none -v <work>/physionet/text:/data/in:ro -v <work>/philter-physionet:/data/out deidsharp/philter:1.0.3   # over an hour on one core; batches in parallel give the same output
dotnet run tools/baselines/to-predictions.cs -- philter <work>/asq <work>/philter-asq <work>/philter-asq.tsv
dotnet run tools/baselines/to-predictions.cs -- philter <work>/physionet <work>/philter-physionet <work>/philter-physionet.tsv
```

### Every stack and tool, paired

The reporting half, with the language-model stacks of F30 and F35 and the four baselines, each stack compared with each baseline. The findings of record ran this three times, for the language model's run-to-run variation:

```bash
dotnet run --project tools/DeidSharp.Evaluation.Cli -c Release -- --corpus physionet-deid \
  --text <id.text> --gold <id-phi.phrase> --split report --clinical http://127.0.0.1:5003/ \
  --llm gemma4:26b-a4b --llm-prompts all-examples --gazetteer without \
  --llm-second-opinion on --clinical-places on --ablation on \
  --predictions deid=<work>/deid-default/id.phi,deid-generic=<work>/deid-generic/id.phi,deid-nohosp=<work>/deid-nohosp/id.phi,philter=<work>/philter-physionet.tsv \
  --compare-with deid,deid-generic,deid-nohosp,philter
```

The published table's stacks against the baselines (no language model, so one run):

```bash
dotnet run --project tools/DeidSharp.Evaluation.Cli -c Release -- --corpus physionet-deid \
  --text <id.text> --gold <id-phi.phrase> --split report \
  --presidio http://127.0.0.1:5002/ --clinical http://127.0.0.1:5003/ \
  --predictions deid=<work>/deid-default/id.phi,deid-generic=<work>/deid-generic/id.phi,deid-nohosp=<work>/deid-nohosp/id.phi,philter=<work>/philter-physionet.tsv \
  --compare-with deid,deid-generic,deid-nohosp,philter
```

ASQ-PHI takes the same flags without `--text`, `--gold` and `--split`, with `--predictions deid=<work>/deid-asq.tsv,philter=<work>/philter-asq.tsv --compare-with deid,philter`.

### MIMIC-IV-Note

D4's run (F41), with `<notes>` your unmodified `note/discharge.csv.gz`. The other tools read an export of the reporting half; deid reads its `deid.text` once per configuration:

```bash
dotnet run --project tools/DeidSharp.Evaluation.Cli -c Release -- --corpus mimic-iv-note --notes <notes> \
  --split report --export <work>/mimic

for c in default generic nohosp; do mkdir -p <work>/mimic-deid-$c && cp <work>/mimic/deid.text <work>/mimic-deid-$c/; done
perl deid.pl <work>/mimic-deid-default/deid deid-output.config          # from deid's folder
perl deid.pl <work>/mimic-deid-generic/deid deid-generic.config
perl deid.pl <work>/mimic-deid-nohosp/deid deid-generic-nohosp.config
for c in default generic nohosp; do
  dotnet run tools/baselines/to-predictions.cs -- deid <work>/mimic <work>/mimic-deid-$c/deid.phi <work>/mimic-deid-$c.tsv
done

docker run --rm --network none -v <work>/mimic/text:/data/in:ro -v <work>/philter-mimic:/data/out deidsharp/philter:1.0.3
dotnet run tools/baselines/to-predictions.cs -- philter <work>/mimic <work>/philter-mimic <work>/philter-mimic.tsv
```

Then the published table's stacks (add `--publish docs/evaluation` to publish them), the same with the baselines, and the language-model stacks with them:

```bash
dotnet run --project tools/DeidSharp.Evaluation.Cli -c Release -- --corpus mimic-iv-note --notes <notes> \
  --split report --presidio http://127.0.0.1:5002/ --clinical http://127.0.0.1:5003/

dotnet run --project tools/DeidSharp.Evaluation.Cli -c Release -- --corpus mimic-iv-note --notes <notes> \
  --split report --presidio http://127.0.0.1:5002/ --clinical http://127.0.0.1:5003/ --gazetteer with,without \
  --predictions deid=<work>/mimic-deid-default.tsv,deid-generic=<work>/mimic-deid-generic.tsv,deid-nohosp=<work>/mimic-deid-nohosp.tsv,philter=<work>/philter-mimic.tsv \
  --compare-with deid,deid-generic,deid-nohosp,philter

dotnet run --project tools/DeidSharp.Evaluation.Cli -c Release -- --corpus mimic-iv-note --notes <notes> \
  --split report --clinical http://127.0.0.1:5003/ \
  --llm gemma4:26b-a4b --llm-prompts all-examples --gazetteer without \
  --llm-second-opinion on --clinical-places on --ablation on \
  --predictions deid=<work>/mimic-deid-default.tsv,deid-generic=<work>/mimic-deid-generic.tsv,deid-nohosp=<work>/mimic-deid-nohosp.tsv,philter=<work>/philter-mimic.tsv \
  --compare-with deid,deid-generic,deid-nohosp,philter
```

### The run of record

D6's run, once per corpus, publishing each. The model baselines read an export of the reporting half (ASQ-PHI's, of all of it), as [the evaluation README](docs/evaluation/README.md#comparing-with-model-baselines) describes; deid and Philter's files are those above:

```bash
dotnet run --project tools/DeidSharp.Evaluation.Cli -c Release -- --corpus physionet-deid \
  --text <id.text> --gold <id-phi.phrase> --split report --export <work>/physionet-report
for m in stanford-deid privacy-filter gliner-pii; do
  docker run --rm --gpus all --network none -v <work>/physionet-report:/data/in:ro -v <work>/models-physionet:/data/out \
    deidsharp/baseline-models:1 $m /data/in /data/out/$m.tsv
done

RECORD=(--clinical http://127.0.0.1:5003/ --clinical-places on --gazetteer with,without \
  --llm gemma4:26b-a4b --llm-prompts all-examples --llm-second-opinion on \
  --publish docs/evaluation --note "<the sidecar build, CPU or GPU, and the baseline image's digest>")
TOOLS=deid,deid-generic,deid-nohosp,philter,stanford-deid,privacy-filter,gliner-pii

dotnet run --project tools/DeidSharp.Evaluation.Cli -c Release -- --corpus physionet-deid \
  --text <id.text> --gold <id-phi.phrase> --split report "${RECORD[@]}" \
  --predictions deid=<work>/deid-default/id.phi,deid-generic=<work>/deid-generic/id.phi,deid-nohosp=<work>/deid-nohosp/id.phi,philter=<work>/philter-physionet.tsv,stanford-deid=<work>/models-physionet/stanford-deid.tsv,privacy-filter=<work>/models-physionet/privacy-filter.tsv,gliner-pii=<work>/models-physionet/gliner-pii.tsv \
  --compare-with $TOOLS
```

The discharge summaries take `--corpus mimic-iv-note --notes <notes> --split report` with the files of [MIMIC-IV-Note](#mimic-iv-note) and the models' spans on `<work>/mimic`; ASQ-PHI takes `--corpus asq-phi` with `deid-asq.tsv`, `philter-asq.tsv` and the models' spans on `<work>/asq`.

F40's counts come from `--split tune --gold-review <work>/gold-review.txt`, which prints how many spans each rule typed, and its smoke test from `--split tune` with no detector options. F42's profile comes from `--split tune --clinical http://127.0.0.1:5003/ --llm gemma4:26b-a4b --llm-prompts all-examples --gazetteer with,without --variants repeated-names --miss-profile UntypedName`. F43's numbers come from `--split tune --gazetteer with,without` on the nursing notes and the discharge summaries, and `--gazetteer with,without` on ASQ-PHI. F44's come from the three models run on an export of each tuning half (`--split tune --export <folder>`) and of ASQ-PHI, scored with `--gazetteer with,without --predictions stanford-deid=…,privacy-filter=…,gliner-pii=…` and `--split tune`. F45's come from `--llm gemma4:26b-a4b,ministral-3:14b --llm-prompts instructions,all-examples --gazetteer without`, with `--split tune` on the two real corpora.

For an earlier finding, start from these commands and set the flags its settings line names: `--split tune` for the tuning half, `--llm` with the models it compares, `--llm-prompts` with its prompts, `--variants repeated-names` for F7, `--llm-second-opinion self` for F31. The console prints counts and rates only, never note text.

## Commits

F1 to F35 name the commit each was measured at. Those commits belong to the development history from before this repository was published, so they aren't in it; every finding's settings line still gives what a rerun needs. F36 onward reran the configurations that matter at `ee4f226`, the commit the published table names. `v1.0.0` is that commit plus the packaging of the libraries for NuGet (README links, symbol packages, a CI pack step) and no change to code that runs, as `git diff ee4f226 v1.0.0` shows, so a rerun from the tag gives the same numbers. F40 and F41, on the discharge summaries, were measured on code that came after the tag, identified by the Git tree hashes of `src/`, `tools/` and `sidecar/` that F41 gives: any commit where `git rev-parse <commit>:src`, `:tools` and `:sidecar` print those hashes runs the same code. F42 ran on a `tools` tree that no commit holds; the commit that carries F42 differs from it only in when the word list is read and in comments, so rerun F42 from that commit.

## What to expect

Stacks without a language model are deterministic on one sidecar build and repeat to the span. Stacks with one vary by a few spans between runs (M8, F38), well inside the intervals. A different Ollama version or a newer upload under the same model tag can move them further; record both when you report a rerun (`--publish` records them in `results.json`).
