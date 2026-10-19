"""
DeidSharp clinical analyzer sidecar.

Presidio's AnalyzerEngine with its built-in pattern recognizers (less the ones in
recognizers.py), plus a transformer NER model trained on clinical notes (default
obi/deid_roberta_i2b2, trained on the i2b2 2014 de-identification corpus; see
clinical_recognizer.py). It speaks
Presidio's REST contract (POST /analyze, GET /health), so DeidSharp's PresidioDetector
talks to this image and to the stock mcr.microsoft.com/presidio-analyzer image the
same way.

The model is baked into the image at build time and the container runs offline: text
sent here never leaves it. Offsets in responses are Python code points (DeidSharp
converts them to UTF-16).
"""

import os
from collections.abc import AsyncGenerator
from contextlib import asynccontextmanager
from dataclasses import dataclass
from typing import TYPE_CHECKING

from fastapi import FastAPI, HTTPException
from fastapi.responses import PlainTextResponse
from pydantic import BaseModel, Field

if TYPE_CHECKING:
    from presidio_analyzer import AnalyzerEngine

NLP_CONFIG = os.getenv("DEIDSHARP_NLP_CONFIG", "/app/nlp-config.yaml")
NER_MODEL = os.getenv("DEIDSHARP_NER_MODEL", "obi/deid_roberta_i2b2")
# The commit the image downloaded (Dockerfile, NER_REVISION); the model loads from the
# offline cache at this commit. Unset outside the image: the model's latest commit.
NER_REVISION = os.getenv("DEIDSHARP_NER_REVISION")
# Where the model runs: "cpu", or "cuda" for an NVIDIA GPU (the image's DEVICE build arg).
# Checked at startup: a GPU image without a GPU refuses to start rather than run on the CPU.
NER_DEVICE = os.getenv("DEIDSHARP_NER_DEVICE", "cpu")
# Recognizers in LEFT_OUT_RECOGNIZERS to keep after all (recognizers.py), comma-separated.
KEEP_RECOGNIZERS = os.getenv("DEIDSHARP_KEEP_RECOGNIZERS")
DEVICES = ("cpu", "cuda")
LANGUAGES = ["en"]


@dataclass
class _State:
    engine: "AnalyzerEngine | None" = None


_state = _State()


@asynccontextmanager
async def lifespan(_: FastAPI) -> AsyncGenerator[None]:
    # Imported here so importing this module (for linting, tests) never loads torch.
    from presidio_analyzer import AnalyzerEngine, RecognizerRegistry  # noqa: PLC0415
    from presidio_analyzer.nlp_engine import NlpEngineProvider  # noqa: PLC0415

    from clinical_recognizer import ClinicalNerRecognizer  # noqa: PLC0415
    from recognizers import left_out  # noqa: PLC0415

    if NER_DEVICE not in DEVICES:
        msg = f"DEIDSHARP_NER_DEVICE must be one of {', '.join(DEVICES)}."
        raise RuntimeError(msg)

    registry = RecognizerRegistry(supported_languages=LANGUAGES)
    registry.load_predefined_recognizers(languages=LANGUAGES)
    for name in left_out(KEEP_RECOGNIZERS):
        registry.remove_recognizer(name)
    registry.add_recognizer(ClinicalNerRecognizer(NER_MODEL, NER_REVISION, device=NER_DEVICE))
    nlp_engine = NlpEngineProvider(conf_file=NLP_CONFIG).create_engine()
    _state.engine = AnalyzerEngine(registry=registry, nlp_engine=nlp_engine, supported_languages=LANGUAGES)
    yield


app = FastAPI(title="DeidSharp Clinical Analyzer", lifespan=lifespan)


class AnalyzeRequest(BaseModel):
    text: str
    language: str = "en"
    score_threshold: float | None = Field(default=None, ge=0.0, le=1.0)
    entities: list[str] | None = None


class AnalyzeResult(BaseModel):
    entity_type: str
    start: int
    end: int
    score: float
    # Which recognizer found it (the model or a pattern), as stock Presidio names it.
    recognition_metadata: dict[str, str] | None = None


@app.get("/health", response_class=PlainTextResponse)
def health() -> str:
    if _state.engine is None:
        raise HTTPException(status_code=503, detail="model not loaded")
    return "DeidSharp clinical analyzer is up"


@app.post("/analyze")
def analyze(request: AnalyzeRequest) -> list[AnalyzeResult]:
    if _state.engine is None:
        raise HTTPException(status_code=503, detail="model not loaded")
    if request.language not in LANGUAGES:
        # Never echo the request: it holds patient text.
        raise HTTPException(status_code=400, detail="unsupported language")

    results = _state.engine.analyze(
        text=request.text,
        language=request.language,
        entities=request.entities or None,
        score_threshold=request.score_threshold,
    )
    return [
        AnalyzeResult(
            entity_type=r.entity_type,
            start=r.start,
            end=r.end,
            score=r.score,
            recognition_metadata=_recognizer(r.recognition_metadata),
        )
        for r in results
    ]


def _recognizer(metadata: dict | None) -> dict[str, str] | None:
    # Only the recognizer's name: the rest of the metadata is Presidio's internals.
    name = (metadata or {}).get("recognizer_name")
    return {"recognizer_name": name} if isinstance(name, str) else None
