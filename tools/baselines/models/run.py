"""Runs one model baseline over a corpus the evaluation tool wrote with --export, and writes its
spans for --predictions:

    python run.py <model> <export folder> <spans.tsv>

Models: stanford-deid, privacy-filter, gliner-pii (detectors.py). In the image, the weights are
already on disk and the container has no network: the notes never leave the machine. The
output holds positions and categories only; nothing printed quotes the notes.
"""

from __future__ import annotations

import sys
import time
from collections.abc import Iterator
from pathlib import Path

from detectors import MODELS
from export import Document, Span, read_documents, write_predictions

PROGRESS_EVERY = 250


def spans(model: str, documents: list[Document]) -> Iterator[tuple[Document, Span]]:
    detector = MODELS[model]()
    started = time.monotonic()
    for number, document in enumerate(documents, start=1):
        try:
            found = list(detector.spans(document.text))
        except Exception as error:
            # A third-party message may quote the text, so only the error's type is named.
            raise SystemExit(f"{model} failed on document {document.key} with {type(error).__name__}.") from None
        for span in found:
            yield document, span
        if number % PROGRESS_EVERY == 0 or number == len(documents):
            print(f"  {number:,} / {len(documents):,} in {time.monotonic() - started:,.0f} s", file=sys.stderr)


def main(arguments: list[str]) -> int:
    if len(arguments) != 3 or arguments[0] not in MODELS:  # noqa: PLR2004 - model, export, output
        print(f"Usage: python run.py {'|'.join(MODELS)} <export folder> <spans.tsv>", file=sys.stderr)
        return 2
    model, export, output = arguments[0], Path(arguments[1]), Path(arguments[2])
    documents = list(read_documents(export))
    written = write_predictions(output, model, spans(model, documents))
    print(f"Wrote {written:,} {model} spans for {len(documents):,} documents.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
