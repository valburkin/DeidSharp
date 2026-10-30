"""Tests for export.py and the label maps in detectors.py, on made-up text. No model, torch or
network needed: python -m pytest tools/baselines/models/tests."""

import re
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from detectors import MODELS, Gliner
from export import CATEGORIES, Document, Span, Utf16Offsets, read_documents, word_windows, write_predictions


def export_folder(tmp_path: Path, documents: dict[str, tuple[str, str]]) -> Path:
    (tmp_path / "text").mkdir()
    index = "# key\tdocument\n" + "".join(f"{key}\t{document_id}\n" for key, (document_id, _) in documents.items())
    (tmp_path / "documents.tsv").write_text(index, encoding="utf-8")
    for key, (_, text) in documents.items():
        # The export writes a byte order mark for Philter.
        (tmp_path / "text" / f"{key}.txt").write_text(text, encoding="utf-8-sig")
    return tmp_path


def test_reads_each_document_with_its_id_and_without_the_byte_order_mark(tmp_path):
    export = export_folder(tmp_path, {"00001": ("note 1", "Seen by Dr. Hart."), "00002": ("note 2", "")})

    assert list(read_documents(export)) == [
        Document("00001", "note 1", "Seen by Dr. Hart."),
        Document("00002", "note 2", ""),
    ]


def test_keeps_carriage_returns_so_offsets_match_the_tools_text(tmp_path):
    # The corpus readers keep "\r", so the text read here must too, or every later span shifts.
    (tmp_path / "text").mkdir()
    (tmp_path / "documents.tsv").write_text("00001\tnote 1\n", encoding="utf-8")
    (tmp_path / "text" / "00001.txt").write_bytes("\ufeffSeen\r\nby\rDr. Hart.".encode())

    assert next(read_documents(tmp_path)).text == "Seen\r\nby\rDr. Hart."


def test_counts_offsets_in_utf16_code_units():
    # One character outside the Basic Multilingual Plane counts twice in UTF-16.
    text = "a\U0001f600b Hart"
    offsets = Utf16Offsets(text)

    assert [offsets(i) for i in range(len(text) + 1)] == [0, 1, 3, 4, 5, 6, 7, 8, 9]
    with pytest.raises(ValueError, match="outside"):
        offsets(len(text) + 1)


def test_writes_spans_in_the_evaluation_tools_format_once_each(tmp_path):
    note = Document("00001", "note 1", "\U0001f600 Dr. Hart, 03/04/2024")
    path = tmp_path / "spans.tsv"

    written = write_predictions(
        path, "made-up", [(note, Span(6, 10, "Name")), (note, Span(6, 10, "Name")), (note, Span(12, 22, "Date"))]
    )

    assert written == 2
    assert path.read_text(encoding="utf-8").splitlines()[1:] == ["note 1\t7\t11\tName", "note 1\t13\t23\tDate"]


@pytest.mark.parametrize(
    ("span", "message"),
    [(Span(0, 99, "Name"), "outside"), (Span(3, 3, "Name"), "outside"), (Span(0, 4, "PERSON"), "category")],
)
def test_refuses_a_span_outside_its_text_or_without_a_deidsharp_category(tmp_path, span, message):
    note = Document("00001", "note 1", "Seen by Dr. Hart.")

    with pytest.raises(ValueError, match=message) as error:
        write_predictions(tmp_path / "spans.tsv", "made-up", [(note, span)])

    # Errors name the document by its key, never by its text.
    assert "Hart" not in str(error.value)


def test_windows_cover_every_word_and_overlap():
    text = " ".join(f"w{i}" for i in range(10))

    windows = list(word_windows(text, words=4, overlap=1))

    assert [text[s:e].split() for s, e in windows] == [
        ["w0", "w1", "w2", "w3"],
        ["w3", "w4", "w5", "w6"],
        ["w6", "w7", "w8", "w9"],
    ]
    assert list(word_windows("", 4, 1)) == []
    with pytest.raises(ValueError, match="overlap"):
        list(word_windows(text, words=4, overlap=4))


def test_gliner_windows_count_its_own_words_and_stay_under_its_limit():
    # GLiNER splits punctuation into words of their own: a lab line has many.
    text = "Na-140 K:4.1 Cl/98 (HCO3) 24, " * 200
    limit = 384

    windows = list(word_windows(text, Gliner.WINDOW, Gliner.OVERLAP, Gliner.WORD))

    assert all(len(re.findall(Gliner.WORD, text[s:e])) <= Gliner.WINDOW < limit for s, e in windows)
    assert windows[0][0] == 0
    assert windows[-1][1] == len(text.rstrip())


@pytest.mark.parametrize("model", list(MODELS.values()))
def test_every_label_maps_to_a_deidsharp_category_and_weights_are_pinned(model):
    assert set(model.LABELS.values()) <= CATEGORIES
    for weights in model.WEIGHTS:
        assert len(weights.revision) == 40 and all(c in "0123456789abcdef" for c in weights.revision)
