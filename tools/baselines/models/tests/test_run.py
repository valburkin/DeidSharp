"""Tests for run.py on made-up text, with a stub in place of a model."""

import sys
from collections.abc import Iterator
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import run
from export import Document, Span


# Design pattern: Test Double (Stub).
# The pattern: a stand-in for a real dependency that returns canned answers.
# Here: a model that fails the way a third-party package can, with the text in its message.
class FailingModel:
    def spans(self, text: str) -> Iterator[Span]:
        raise ValueError(f"could not tag {text!r}")


def test_a_failing_model_stops_the_run_naming_the_document_and_the_error_type_never_the_text(monkeypatch):
    monkeypatch.setitem(run.MODELS, "stanford-deid", FailingModel)
    note = Document("00042", "note 42", "Seen by Dr. Hart.")

    with pytest.raises(SystemExit) as stop:
        list(run.spans("stanford-deid", [note]))

    assert str(stop.value) == "stanford-deid failed on document 00042 with ValueError."
    assert "Hart" not in str(stop.value)


@pytest.mark.parametrize("arguments", [[], ["unknown-model", "in", "out.tsv"], ["stanford-deid", "in"]])
def test_refuses_an_unknown_model_or_missing_paths_with_usage(arguments, capsys):
    assert run.main(arguments) == 2
    assert "Usage:" in capsys.readouterr().err
