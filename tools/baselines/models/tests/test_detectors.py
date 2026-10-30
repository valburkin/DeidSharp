"""Tests for where detectors.py runs the models, with stubs in place of them."""

import sys
from collections.abc import Iterator
from pathlib import Path
from typing import ClassVar

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import detectors
import run
from export import Document, Span


# Design pattern: Test Double (Spy).
# The pattern: a stand-in for a real dependency that records how it was called.
# Here: a model that records the device it was built for, and finds nothing.
class DeviceSpy:
    devices: ClassVar[list[str]] = []

    def __init__(self, device: str = detectors.GPU) -> None:
        DeviceSpy.devices.append(device)

    def spans(self, text: str) -> Iterator[Span]:
        return iter(())


def test_the_build_warms_every_model_up_on_the_cpu(monkeypatch):
    DeviceSpy.devices = []
    monkeypatch.setattr(detectors, "MODELS", dict.fromkeys(detectors.MODELS, DeviceSpy))

    detectors.warm_up()

    assert DeviceSpy.devices == ["cpu"] * len(detectors.MODELS)


def test_a_run_builds_its_model_for_the_gpu(monkeypatch):
    DeviceSpy.devices = []
    monkeypatch.setitem(run.MODELS, "stanford-deid", DeviceSpy)

    list(run.spans("stanford-deid", [Document("00001", "note 1", "Seen by Dr. Hart.")]))

    assert DeviceSpy.devices == ["cuda"]
