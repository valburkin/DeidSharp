# The model baselines for the evaluation tool (detectors.py): the Stanford/Penn de-identifier,
# OpenAI Privacy Filter and GLiNER multi-PII, each at its defaults, on CPU. What decides
# detection is pinned: the base image by digest, every package by hash (requirements.txt),
# opf's source by checksum, and each model's weights by commit. The image is for linux/amd64.
#
#   docker build -t deidsharp/baseline-models:1 -f tools/baselines/models/Dockerfile tools/baselines/models
#   docker run --rm --network none -v <export>:/data/in:ro -v <output>:/data/out deidsharp/baseline-models:1 \
#     <stanford-deid|privacy-filter|gliner-pii> /data/in /data/out/<model>.tsv
#
# The container needs no network to run; the notes never leave the machine.
FROM python:3.12-slim@sha256:a6e34c598f2467ed0e9a8d349809fcd8b5c603269512df273a0bb1784edc11b1

ENV PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    HF_HOME=/opt/models \
    TIKTOKEN_CACHE_DIR=/opt/models/tiktoken \
    PYTHONPATH=/opt/opf \
    PYTHONUNBUFFERED=1

COPY requirements.txt /tmp/requirements.txt
RUN pip install --require-hashes --no-deps --requirement /tmp/requirements.txt && pip check

# opf, OpenAI Privacy Filter's own package, is not on PyPI: its source at one commit, by
# checksum. It is pure Python, so it runs from the path; its dependencies are in the lock.
ADD --checksum=sha256:c79c613e3600bfee1357fb37528d439e703484f590552ffe9ef79a8050ccbe39 \
    https://codeload.github.com/openai/privacy-filter/tar.gz/f7f00ca7fb869683eb732c010299d901457f19c3 \
    /tmp/opf.tar.gz
RUN mkdir -p /opt/opf \
    && tar -xzf /tmp/opf.tar.gz -C /tmp \
    && mv /tmp/privacy-filter-f7f00ca7fb869683eb732c010299d901457f19c3/opf /opt/opf/opf \
    && rm -rf /tmp/opf.tar.gz /tmp/privacy-filter-f7f00ca7fb869683eb732c010299d901457f19c3

WORKDIR /app
COPY export.py detectors.py run.py ./

# The weights at their pinned commits; then each model is built and run once with Hugging
# Face offline, which also caches the tokenizer opf loads through tiktoken.
RUN python -c "import detectors; detectors.download()"
ENV HF_HUB_OFFLINE=1
RUN python -c "import detectors; detectors.warm_up()"
# Hugging Face keeps each snapshot's file list readable by its owner only; the models run as
# nobody, who must read it to load a snapshot offline.
RUN chmod -R a+rX /opt/models

USER nobody
ENTRYPOINT ["python", "/app/run.py"]
