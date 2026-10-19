"""Tests for recognizers.py. No model or Presidio needed: python -m pytest sidecar/tests."""

import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from recognizers import LEFT_OUT_RECOGNIZERS, left_out


def test_leaves_out_all_three_by_default():
    assert left_out(None) == sorted(LEFT_OUT_RECOGNIZERS)
    assert left_out("") == sorted(LEFT_OUT_RECOGNIZERS)


def test_keeps_the_named_ones():
    assert left_out(" SpacyRecognizer , UrlRecognizer") == ["UsLicenseRecognizer"]


def test_refuses_a_name_it_does_not_leave_out():
    with pytest.raises(RuntimeError, match="DEIDSHARP_KEEP_RECOGNIZERS may only name"):
        left_out("PhoneRecognizer")
