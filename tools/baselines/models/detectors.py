"""The model baselines, each behind one interface: give it a text, get its spans back in
DeidSharp's categories. Every model runs in its own authors' recommended way, at its default
settings, with nothing tuned on the corpora it is measured on.

The heavy packages (torch, transformers, gliner, opf) are imported when a model is built, so
the label maps and the windowing can be tested without them.
"""

from __future__ import annotations

from collections.abc import Iterator
from dataclasses import dataclass
from typing import ClassVar, Protocol

from export import Span, word_windows


@dataclass(frozen=True)
class Weights:
    """A model's files on Hugging Face, pinned to one commit, so a rebuild runs the same model."""

    repo: str
    revision: str
    allow: tuple[str, ...] | None = None


# Design pattern: Strategy.
# The pattern: a family of interchangeable algorithms behind one interface; the caller runs
# whichever it is given without knowing which one it got.
# Here: each model is one class with the same spans(text) method, picked by name from MODELS,
# so run.py reads the export and writes the spans the same way for every model, and a new
# baseline is a new class and a row, never a branch in run.py.


class Detector(Protocol):
    """A model baseline: the spans it finds in a text, in code points, with DeidSharp's categories."""

    def spans(self, text: str) -> Iterator[Span]: ...


class StanfordDeidentifier:
    """The Stanford/Penn de-identifier (Chambon et al., JAMIA 2022; MIT): a BERT model trained
    on radiology reports and the i2b2 2006 and 2014 corpora, through transformers' pipeline,
    which reads a text longer than the model's 512 tokens in overlapping strides."""

    WEIGHTS: ClassVar = (
        Weights("StanfordAIMI/stanford-deidentifier-base", "661b9c1c717d3165512d440abc3700c386aefab6"),
    )
    LABELS: ClassVar = {
        "PATIENT": "Name",
        "HCW": "Name",
        "HOSPITAL": "Organization",
        "VENDOR": "Organization",
        "DATE": "Date",
        "ID": "OtherIdentifier",
        "PHONE": "Phone",
    }

    def __init__(self) -> None:
        from transformers import pipeline  # noqa: PLC0415 - heavy, and only in the image

        weights = self.WEIGHTS[0]
        self._pipeline = pipeline(
            "token-classification",
            model=weights.repo,
            revision=weights.revision,
            aggregation_strategy="simple",
            stride=128,
            device="cpu",
        )

    def spans(self, text: str) -> Iterator[Span]:
        if not text.strip():
            return
        for entity in self._pipeline(text):
            yield Span(int(entity["start"]), int(entity["end"]), self.LABELS[entity["entity_group"]])


class PrivacyFilter:
    """OpenAI Privacy Filter (2026; Apache 2.0) through its own package, opf, at its defaults:
    constrained Viterbi decoding, spans trimmed of whitespace, the whole text in one pass (its
    context is 128,000 tokens)."""

    WEIGHTS: ClassVar = (Weights("openai/privacy-filter", "7ffa9a043d54d1be65afb281eddf0ffbe629385b", ("original/*",)),)
    LABELS: ClassVar = {
        "private_person": "Name",
        "private_address": "StreetAddress",
        "private_email": "Email",
        "private_phone": "Phone",
        "private_url": "Url",
        "private_date": "Date",
        "account_number": "AccountNumber",
        "secret": "OtherIdentifier",
    }

    def __init__(self) -> None:
        from huggingface_hub import snapshot_download  # noqa: PLC0415
        from opf import OPF  # noqa: PLC0415

        weights = self.WEIGHTS[0]
        folder = snapshot_download(weights.repo, revision=weights.revision, allow_patterns=list(weights.allow or ()))
        self._opf = OPF(model=f"{folder}/original", device="cpu")

    def spans(self, text: str) -> Iterator[Span]:
        if not text.strip():
            return
        for span in self._opf.redact(text).detected_spans:
            # opf counts code points; a span whose text differs would mean it counted otherwise.
            if text[span.start : span.end] != span.text:
                raise ValueError("opf returned a span whose offsets don't match its text.")
            yield Span(span.start, span.end, self.LABELS[span.label])


class Gliner:
    """GLiNER multi-PII (urchade/gliner_multi_pii-v1, Apache 2.0), the GLiNER model MedDeID
    compared, at its default threshold of 0.5. GLiNER finds the entity types it is asked for:
    LABELS asks for Safe Harbor's identifiers as plain words, chosen from the list before any
    measurement and not tuned. It reads at most 384 words, so a text is read in overlapping
    windows of its own words."""

    WEIGHTS: ClassVar = (
        Weights("urchade/gliner_multi_pii-v1", "1fcf13e85f4eef5394e1fcd406cf2ca9ea82351d"),
        # The encoder the model was trained from, whose tokenizer and configuration it loads.
        Weights(
            "microsoft/mdeberta-v3-base",
            "a0484667b22365f84929a935b5e50a51f71f159d",
            ("config.json", "spm.model", "tokenizer_config.json", "pytorch_model.bin"),
        ),
    )
    LABELS: ClassVar = {
        "person": "Name",
        "address": "StreetAddress",
        "city": "Location",
        "zip code": "ZipCode",
        "organization": "Organization",
        "date": "Date",
        "age": "Age",
        "phone number": "Phone",
        "fax number": "Fax",
        "email": "Email",
        "social security number": "Ssn",
        "medical record number": "MedicalRecordNumber",
        "health insurance number": "HealthPlanNumber",
        "account number": "AccountNumber",
        "license number": "LicenseNumber",
        "license plate number": "VehicleIdentifier",
        "url": "Url",
        "ip address": "IpAddress",
    }
    # GLiNER's own word splitter; windows well under its 384 words leave room for the labels
    # in the encoder's 512 tokens.
    WORD: ClassVar = r"\w+(?:[-_]\w+)*|\S"
    WINDOW: ClassVar = 160
    OVERLAP: ClassVar = 40
    THRESHOLD: ClassVar = 0.5

    def __init__(self) -> None:
        from gliner import GLiNER  # noqa: PLC0415

        weights = self.WEIGHTS[0]
        self._model = GLiNER.from_pretrained(weights.repo, revision=weights.revision, map_location="cpu")

    def spans(self, text: str) -> Iterator[Span]:
        labels = list(self.LABELS)
        for start, end in word_windows(text, self.WINDOW, self.OVERLAP, self.WORD):
            for entity in self._model.predict_entities(text[start:end], labels, threshold=self.THRESHOLD):
                yield Span(start + int(entity["start"]), start + int(entity["end"]), self.LABELS[entity["label"]])


MODELS: dict[str, type[StanfordDeidentifier | PrivacyFilter | Gliner]] = {
    "stanford-deid": StanfordDeidentifier,
    "privacy-filter": PrivacyFilter,
    "gliner-pii": Gliner,
}


def download() -> None:
    """Fetches every model's pinned files into the Hugging Face cache, at the image's build.

    A package that loads a model by name alone (GLiNER loads its encoder's tokenizer so) asks
    for its main branch; pointing main at the pinned commit in the cache keeps that offline
    load on the same files.
    """
    from pathlib import Path  # noqa: PLC0415

    from huggingface_hub import constants, snapshot_download  # noqa: PLC0415

    for model in MODELS.values():
        for weights in model.WEIGHTS:
            snapshot_download(
                weights.repo, revision=weights.revision, allow_patterns=list(weights.allow) if weights.allow else None
            )
            main = Path(constants.HF_HUB_CACHE) / f"models--{weights.repo.replace('/', '--')}" / "refs" / "main"
            main.parent.mkdir(parents=True, exist_ok=True)
            main.write_text(weights.revision, encoding="ascii")


def warm_up() -> None:
    """Builds every model and runs it on a made-up sentence, at the image's build, with Hugging
    Face set offline: a model that would need the network at run time fails the build instead."""
    for model in MODELS.values():
        list(model().spans("Dr. Ellen Hart saw the patient on 03/04/2024 at Lakeside Hospital."))
