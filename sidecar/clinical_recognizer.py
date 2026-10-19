"""
A Presidio recognizer backed by a clinical token-classification model
(obi/deid_roberta_i2b2 by default). The model runs without Hugging Face's aggregation;
bilou.py groups its BILOU-tagged tokens into entities, and splits long texts into
windows counted in the model's own tokens so none is cut at the model's limit.
"""

from __future__ import annotations

from typing import TYPE_CHECKING, Any

from presidio_analyzer import EntityRecognizer, RecognizerResult

from bilou import LABEL_TO_ENTITY, Token, dedupe, group_tokens, label_type, token_windows

if TYPE_CHECKING:
    from presidio_analyzer.nlp_engine import NlpArtifacts


class ClinicalNerRecognizer(EntityRecognizer):
    """Runs a BILOU token-classification model and reports its entities to Presidio."""

    def __init__(
        self, model_name: str, revision: str | None = None, supported_language: str = "en", device: str = "cpu"
    ) -> None:
        self.model_name = model_name
        self.revision = revision
        self.device = device
        self.pipeline: Any = None
        super().__init__(
            supported_entities=sorted(set(LABEL_TO_ENTITY.values())),
            supported_language=supported_language,
            name="ClinicalNerRecognizer",
        )

    def load(self) -> None:
        import torch  # noqa: PLC0415
        from transformers import pipeline  # noqa: PLC0415

        if self.device == "cuda" and not torch.cuda.is_available():
            msg = "DEIDSHARP_NER_DEVICE is cuda, but no CUDA GPU is visible: give the container a GPU (driver 580+)."
            raise RuntimeError(msg)

        self.pipeline = pipeline(
            "token-classification",
            model=self.model_name,
            revision=self.revision,
            aggregation_strategy="none",
            device=self.device,
        )

    def analyze(
        self, text: str, entities: list[str], nlp_artifacts: NlpArtifacts | None = None
    ) -> list[RecognizerResult]:
        tokens: list[Token] = []
        encoded = self.pipeline.tokenizer(text, add_special_tokens=False, return_offsets_mapping=True)
        for start, end in token_windows([tuple(o) for o in encoded["offset_mapping"]]):
            offset, chunk = start, text[start:end]
            for raw in self.pipeline(chunk):
                entity = LABEL_TO_ENTITY.get(label_type(raw["entity"]))
                if entity is None or raw.get("start") is None:
                    continue
                tokens.append(Token(entity, offset + raw["start"], offset + raw["end"], float(raw["score"])))

        wanted = set(entities) if entities else set(self.supported_entities)
        return [
            RecognizerResult(
                entity_type=group.entity,
                start=group.start,
                end=group.end,
                score=group.score,
                recognition_metadata={
                    RecognizerResult.RECOGNIZER_NAME_KEY: self.name,
                    RecognizerResult.RECOGNIZER_IDENTIFIER_KEY: self.id,
                },
            )
            for group in group_tokens(text, dedupe(tokens))
            if group.entity in wanted
        ]
