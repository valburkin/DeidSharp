# DeidSharp clinical analyzer: Presidio + a clinical transformer NER model.
# The model is downloaded at build time; the running container needs no network.
# What decides detection is pinned (the base image by digest, every Python package by
# hash, the model by commit), so a rebuild detects the same. Only the public suffix list
# below is fetched as it stands at build time. The image is for linux/amd64.
FROM python:3.12-slim@sha256:a6e34c598f2467ed0e9a8d349809fcd8b5c603269512df273a0bb1784edc11b1

ENV PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    HF_HOME=/opt/models \
    PYTHONUNBUFFERED=1

WORKDIR /app

# Where the model runs: cpu (the default) or cuda, for an NVIDIA GPU. Each has its own
# lock, identical but for PyTorch's build and the CUDA libraries it needs.
ARG DEVICE=cpu

# The locked packages and nothing else, from the indexes the lock names: PyTorch from
# PyTorch's own (CPU-only by default, a few GB instead of CUDA), the rest, spaCy's English
# model included, from PyPI. pip check proves the lock is complete. See README.md,
# "Dependencies" and "GPU".
COPY requirements.txt requirements-cuda.txt ./
RUN case "$DEVICE" in \
      cpu) lock=requirements.txt ;; \
      cuda) lock=requirements-cuda.txt ;; \
      *) echo "DEVICE must be cpu or cuda." >&2; exit 1 ;; \
    esac \
    && pip install --require-hashes --no-deps --requirement "$lock" \
    && pip check

# The model at a fixed commit: a new upload under the same name cannot change detection.
ARG NER_MODEL=obi/deid_roberta_i2b2
ARG NER_REVISION=78f2152eb93ddd817290ce8dbe46f1a6685e09fc
ENV DEIDSHARP_NER_MODEL=${NER_MODEL} \
    DEIDSHARP_NER_REVISION=${NER_REVISION} \
    DEIDSHARP_NER_DEVICE=${DEVICE}
RUN python -c "from transformers import AutoModelForTokenClassification, AutoTokenizer; \
AutoTokenizer.from_pretrained('${NER_MODEL}', revision='${NER_REVISION}'); \
AutoModelForTokenClassification.from_pretrained('${NER_MODEL}', revision='${NER_REVISION}')"

# Presidio's email recognizer uses tldextract, which fetches the public suffix list on
# first use. Fetch it now, into a cache the service then only reads.
ENV TLDEXTRACT_CACHE=/opt/tldextract
RUN python -c "import tldextract; tldextract.extract('example.com')"

# From here on nothing is fetched: patient text is processed fully offline.
ENV HF_HUB_OFFLINE=1 \
    TRANSFORMERS_OFFLINE=1

COPY app.py bilou.py clinical_recognizer.py recognizers.py nlp-config.yaml ./

RUN useradd --uid 1001 --no-create-home deidsharp && chown -R deidsharp /app /opt/tldextract
USER deidsharp

EXPOSE 3000
HEALTHCHECK --interval=15s --timeout=5s --start-period=120s --retries=5 \
    CMD python -c "import urllib.request; urllib.request.urlopen('http://localhost:3000/health')"
CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "3000"]
