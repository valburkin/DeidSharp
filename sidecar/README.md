# DeidSharp clinical analyzer

A Presidio analyzer service with a clinical NER model. It speaks Presidio's REST API, so `DeidSharp.Presidio.PresidioDetector` uses it exactly like the stock Presidio image.

- **Model:** `obi/deid_roberta_i2b2`, a RoBERTa model trained on the i2b2 2014 de-identification corpus. It finds patients, staff, hospitals, places, ages, dates, phones, emails and ids.
- **Recognizers:** Presidio's built-in pattern recognizers, less three that flagged far more clinical text than PHI in evaluation (spaCy's general NER, URLs and US driver's licenses; `recognizers.py`). Where recall must win at any cost to the text, `DEIDSHARP_KEEP_RECOGNIZERS=SpacyRecognizer,UrlRecognizer,UsLicenseRecognizer` keeps any of them: together they cost 11% of the non-PHI text on PhysioNet's nursing notes for about 2 points of recall (docs/evaluation/findings.md, F2).
- **Offline:** the model, and the public suffix list Presidio's email recognizer needs, are downloaded when the image is built. The running container sets `HF_HUB_OFFLINE=1` and needs no network, so text sent to it stays on the host.
- **Port:** 3000 inside the container (`docker compose` maps it to 5003 on 127.0.0.1 only: the service receives raw patient text).

```bash
docker build -t localhost/deidsharp/clinical-analyzer:local .
docker run --rm -p 127.0.0.1:5003:3000 localhost/deidsharp/clinical-analyzer:local
curl -s localhost:5003/health
curl -s -X POST localhost:5003/analyze -H 'Content-Type: application/json' \
  -d '{"text": "Seen with her daughter Ngozi Okafor at Mercy General.", "language": "en"}'
```

## GPU

The model runs on the CPU by default. With an NVIDIA GPU, build with `--build-arg DEVICE=cuda` for the same code and model on CUDA PyTorch: many times faster on long notes, and the image is several GB larger for CUDA's libraries.

```bash
docker compose --profile gpu up -d --build clinical-gpu    # 127.0.0.1:5005
# or
docker build --build-arg DEVICE=cuda -t localhost/deidsharp/clinical-analyzer:cuda .
docker run --rm --gpus all -p 127.0.0.1:5005:3000 localhost/deidsharp/clinical-analyzer:cuda
```

- **Needs** NVIDIA driver 580 or later on the host (CUDA 13.0) and Docker's GPU support: Docker Desktop on WSL2, or the NVIDIA Container Toolkit on Linux. The build covers Turing cards up to Blackwell (RTX 50xx). An older driver shows as "no CUDA GPU is visible".
- **Fails closed:** a CUDA image that sees no GPU refuses to start, rather than run on the CPU unnoticed.
- **Detects the same, nearly.** GPU arithmetic rounds differently from the CPU's, so a token at the edge of a decision can come out the other way. Measure recall on the image you deploy, and say which one produced published numbers.

## Why the model's tokens are grouped here

The model tags tokens with BILOU prefixes (B-, I-, L-, U-). Hugging Face's built-in aggregation, which Presidio's transformers engine relies on, only understands B- and I-. It silently drops entities whose last token is L- or that are a single U- token: in testing, "Okafor", "Northampton" and ages disappeared. So the model runs as a custom Presidio recognizer without aggregation (`clinical_recognizer.py`), and `bilou.py` groups the tokens, widens entities to whole words, and splits long texts into overlapping windows counted in the model's own tokens (400 per window, 64 shared), so no window is cut at the model's 512-token limit. `bilou.py` has no dependencies and is tested with `python -m pytest tests`.

## Using another model

Build with `--build-arg NER_MODEL=<huggingface id> --build-arg NER_REVISION=<commit>`; the revision pins the model, so a new upload under the same name never changes detection unseen. The model must be a token classifier whose labels, after any position prefix, appear in `bilou.LABEL_TO_ENTITY`; extend that map for other label sets. Measure recall again after any model change (see `../docs/testing.md`).

## Dependencies

The image installs exactly what the lock lists, each package checked against its hash:

| File | Holds |
|---|---|
| `requirements.in` | The runtime packages, edited by hand: CPU-only PyTorch from PyTorch's own index, everything else from PyPI |
| `requirements.txt` | The lock: every package and its dependencies, with hashes and the indexes to fetch them from, for Linux x86_64 and Python 3.12 |
| `torch-cuda.txt` | The GPU image's override: PyTorch's CUDA build in place of the CPU one |
| `requirements-cuda.txt` | The GPU image's lock: the same packages at the same versions, with CUDA PyTorch and the CUDA libraries it needs (`tests/test_locks.py` keeps the two in step) |

The image is built for linux/amd64 (the lock holds x86_64 wheels; the compose file sets the platform, so Apple Silicon builds it under emulation). Dependabot does not update this lock; CI audits it for known vulnerabilities on every run and weekly, and a finding is the signal to regenerate it. After editing `requirements.in`, regenerate the lock with [uv](https://docs.astral.sh/uv/), leaving out releases from the last five days, then rebuild and run the clinical integration tests (`../docs/testing.md`):

```bash
cd sidecar
uv pip compile requirements.in --upgrade --generate-hashes --emit-index-url \
  --python-version 3.12 --python-platform x86_64-unknown-linux-gnu \
  --index-url https://pypi.org/simple --extra-index-url https://download.pytorch.org/whl/cpu \
  --index-strategy unsafe-best-match --exclude-newer "$(date -u -d '5 days ago' +%F)" \
  --output-file requirements.txt
```

Then regenerate the GPU lock with the same cutoff date, so both resolve to the same versions:

```bash
uv pip compile requirements.in --override torch-cuda.txt --generate-hashes --emit-index-url \
  --python-version 3.12 --python-platform x86_64-unknown-linux-gnu \
  --index-url https://pypi.org/simple --extra-index-url https://download.pytorch.org/whl/cu130 \
  --index-strategy unsafe-best-match --exclude-newer <the date used above> \
  --output-file requirements-cuda.txt
```

When PyTorch moves to a new release, change `torch-cuda.txt` along with `requirements.in`.

To fix an audit finding without moving anything else, so detection stays as measured, add `--upgrade-package <name>` for each flagged package to both commands (replacing `--upgrade` in the first), and give the five-day cutoff as `--exclude-newer-package <name>=<date>` for each of them in place of `--exclude-newer`. Combined with `--upgrade-package`, a global `--exclude-newer` downgrades packages that PyTorch's indexes mirror without upload dates (sympy, mpmath, typing-extensions) and what depends on them, so the cutoff goes on the flagged packages only.

## Checking the code

`ruff.toml` pins the lint rules and `tools/requirements.txt` the tools; neither is in the image.

```bash
python -m pip install -r sidecar/tools/requirements.txt
ruff check sidecar && ruff format --check sidecar && python -m pytest sidecar/tests
```

## Files

| File | Purpose |
|---|---|
| `app.py` | FastAPI service: `/analyze`, `/health` |
| `clinical_recognizer.py` | Presidio recognizer that runs the model |
| `bilou.py` | Token grouping, word widening and token windows, dependency-free |
| `nlp-config.yaml` | Presidio's spaCy engine (tokenization and context words; its general NER is left out) |
| `requirements.txt`, `requirements-cuda.txt` | The hash-locked Python dependencies of the CPU and GPU images (see "Dependencies") |
| `tests/` | pytest for `bilou.py`, and that the two locks pin the same packages |
