"""Reads a corpus the evaluation tool wrote with --export and writes a baseline's spans in the
tool's predictions format, one span per line: document<TAB>start<TAB>end<TAB>category.

Offsets come out as the evaluation tool counts them: UTF-16 code units, end exclusive, in the
text as the corpus reader built it (the exported file without its byte order mark). Python
counts code points, which differ only after a character outside the Basic Multilingual Plane.

The export holds corpus text, so nothing here prints or quotes it: messages name files, keys
and counts only.
"""

from __future__ import annotations

import bisect
import re
from collections.abc import Iterable, Iterator
from dataclasses import dataclass
from pathlib import Path

INDEX_FILE = "documents.tsv"
TEXT_FOLDER = "text"

# The names of DeidSharp's PhiCategory, which the predictions file's fourth column takes.
CATEGORIES = frozenset(
    {
        "Name",
        "StreetAddress",
        "Location",
        "ZipCode",
        "Date",
        "Age",
        "Phone",
        "Fax",
        "Email",
        "Ssn",
        "MedicalRecordNumber",
        "HealthPlanNumber",
        "AccountNumber",
        "LicenseNumber",
        "VehicleIdentifier",
        "DeviceIdentifier",
        "Url",
        "IpAddress",
        "Biometric",
        "Photo",
        "OtherIdentifier",
        "Organization",
    }
)


@dataclass(frozen=True)
class Document:
    """One exported document: its key in the export, its id in the corpus, and its text."""

    key: str
    id: str
    text: str


@dataclass(frozen=True)
class Span:
    """A span a model found, in code points of the document's text, with DeidSharp's category."""

    start: int
    end: int
    category: str


def read_documents(export: Path) -> Iterator[Document]:
    """The exported documents in the export's order."""
    index = export / INDEX_FILE
    for number, line in enumerate(index.read_text(encoding="utf-8").splitlines(), start=1):
        if not line or line.startswith("#"):
            continue
        fields = line.split("\t")
        if len(fields) != 2:  # noqa: PLR2004 - key and id
            raise ValueError(f"{index}: line {number} is not key<TAB>document.")
        key, document_id = fields
        # Bytes, decoded: reading as text would turn CRLF and a lone CR into LF and shift
        # every later offset; utf-8-sig drops the byte order mark the export writes for Philter.
        text = (export / TEXT_FOLDER / f"{key}.txt").read_bytes().decode("utf-8-sig")
        yield Document(key, document_id, text)


class Utf16Offsets:
    """Turns code-point offsets into UTF-16 code units, for one text."""

    def __init__(self, text: str) -> None:
        # The code-point positions after which one more code unit is counted.
        self._astral = [i for i, c in enumerate(text) if ord(c) > 0xFFFF]  # noqa: PLR2004 - the last BMP code point
        self._length = len(text)

    def __call__(self, offset: int) -> int:
        if not 0 <= offset <= self._length:
            raise ValueError(f"offset {offset} is outside a text of {self._length} code points.")
        return offset + bisect.bisect_left(self._astral, offset)


def word_windows(text: str, words: int, overlap: int, word: str = r"\S+") -> Iterator[tuple[int, int]]:
    """Code-point ranges of at most `words` words each (a word being what the `word` pattern
    matches), the next starting `overlap` words before the last one ends, so an entity cut at
    one window's edge is whole in the next."""
    if not 0 <= overlap < words:
        raise ValueError("overlap must be at least 0 and less than words.")
    bounds = [(m.start(), m.end()) for m in re.finditer(word, text)]
    first = 0
    while first < len(bounds):
        last = min(first + words, len(bounds))
        yield bounds[first][0], bounds[last - 1][1]
        if last == len(bounds):
            return
        first = last - overlap


def write_predictions(path: Path, tool: str, spans: Iterable[tuple[Document, Span]]) -> int:
    """Writes the spans as the evaluation tool reads them, and returns how many it wrote.

    Each span is checked against its text and DeidSharp's categories, so a broken model run
    fails here rather than scoring as wrong spans. Repeats of a span (from overlapping
    windows) are written once.
    """
    lines = [f"# {tool} spans, written by tools/baselines/models\n"]
    seen = set()
    offsets_by_key: dict[str, Utf16Offsets] = {}
    for document, span in spans:
        if span.category not in CATEGORIES:
            raise ValueError(f"document {document.key}: {span.category!r} is not a DeidSharp category.")
        if not 0 <= span.start < span.end <= len(document.text):
            raise ValueError(f"document {document.key}: a span ({span.start}, {span.end}) is outside the text.")
        if document.key not in offsets_by_key:
            offsets_by_key[document.key] = Utf16Offsets(document.text)
        offsets = offsets_by_key[document.key]
        line = f"{document.id}\t{offsets(span.start)}\t{offsets(span.end)}\t{span.category}\n"
        if line not in seen:
            seen.add(line)
            lines.append(line)
    path.write_text("".join(lines), encoding="utf-8", newline="\n")
    return len(lines) - 1
