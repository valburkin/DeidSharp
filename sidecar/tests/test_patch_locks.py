"""Tests for tools/patch_locks.py, without running uv or pip-audit: python -m pytest sidecar/tests."""

import json
import subprocess
import sys
from datetime import date
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))

import patch_locks
from patch_locks import (
    HEADER,
    LOCK_GROUPS,
    ROOT,
    Finding,
    changed_pins,
    compile_command,
    group_pins,
    outcomes,
    output_of,
    parse_audit,
    pinned_by_hand,
    run_pip_audit,
)

CUTOFF = date(2026, 11, 11)


def test_a_patch_moves_only_the_flagged_packages_and_drops_the_recorded_holds():
    header = (
        "uv pip compile requirements.in --upgrade --generate-hashes --exclude-newer 2026-10-12 "
        "--exclude-newer-package certifi=2026-10-23 --index-url https://pypi.org/simple --output-file requirements.txt"
    )

    command = compile_command(header, "requirements.txt", ["urllib3", "certifi"], CUTOFF)

    assert command == [
        "uv",
        "pip",
        "compile",
        "requirements.in",
        "--generate-hashes",
        "--index-url",
        "https://pypi.org/simple",
        "--output-file",
        "requirements.txt",
        "--upgrade-package",
        "certifi",
        "--exclude-newer-package",
        "certifi=2026-11-11",
        "--upgrade-package",
        "urllib3",
        "--exclude-newer-package",
        "urllib3=2026-11-11",
    ]


def test_a_patch_drops_holds_written_with_an_equals_sign_and_the_short_upgrade_flag():
    header = "uv pip compile requirements.in -U --exclude-newer=2026-10-12 --output-file requirements.txt"

    command = compile_command(header, "requirements.txt", ["x"], CUTOFF)

    assert "--exclude-newer=2026-10-12" not in command
    assert "-U" not in command


@pytest.mark.parametrize(
    "args",
    [["-o", "requirements.txt"], ["--output-file", "requirements.txt"], ["--output-file=requirements.txt"]],
)
def test_the_output_file_is_read_in_every_spelling(args):
    assert output_of(["uv", "pip", "compile", "requirements.in", *args]) == "requirements.txt"


def test_a_command_that_writes_another_file_is_refused():
    header = "uv pip compile requirements.in --output-file requirements-cuda.txt"

    with pytest.raises(ValueError, match="writes another file"):
        compile_command(header, "requirements.txt", ["x"], CUTOFF)


def test_a_command_that_is_not_uv_pip_compile_is_refused():
    with pytest.raises(ValueError, match="not uv pip compile"):
        compile_command("pip-compile requirements.in --output-file requirements.txt", "requirements.txt", [], CUTOFF)


def test_every_lock_records_a_command_that_rebuilds_it():
    for group in LOCK_GROUPS:
        for path in group:
            lock = ROOT / path
            header = HEADER.search(lock.read_text(encoding="utf-8"))

            assert header is not None, path
            command = compile_command(header.group(1), lock.name, ["x"], CUTOFF)
            assert "--generate-hashes" in command, path
            assert "--index-url" in command, path
            assert "--extra-index-url" in command, path
            assert ("--override" in command) == path.endswith("-cuda.txt"), path


def test_the_audit_report_gives_each_flagged_package_and_whether_any_advisory_has_a_fix():
    report = json.dumps(
        {
            "dependencies": [
                {
                    "name": "OpenTelemetry_API",
                    "version": "1.45.1",
                    "vulns": [{"id": "CVE-1", "fix_versions": ["1.45.2"]}],
                },
                {
                    "name": "torch",
                    "version": "2.14.1",
                    "vulns": [{"id": "CVE-2", "fix_versions": ["2.15.0"]}, {"id": "CVE-3", "fix_versions": []}],
                },
                {"name": "anyio", "version": "4.15.1", "vulns": []},
                {"name": "blis", "version": "1.3.3", "vulns": [{"id": "CVE-4", "fix_versions": []}]},
                {"name": "en-core-web-sm", "skip_reason": "URL requirements cannot be pinned"},
            ],
            "fixes": [],
        }
    )

    findings = parse_audit(report)

    assert [(f.package, f.version, f.advisories, f.fixable) for f in findings] == [
        ("opentelemetry-api", "1.45.1", ("CVE-1",), True),
        ("torch", "2.14.1", ("CVE-2", "CVE-3"), True),
        ("blis", "1.3.3", ("CVE-4",), False),
    ]


def test_a_report_that_does_not_parse_stops_the_run():
    with pytest.raises(json.JSONDecodeError):
        parse_audit("Traceback (most recent call last):")


class FakeAudit:
    """Test Double (Stub): stands in for pip-audit's process with a fixed result."""

    def __init__(self, returncode: int, stdout: str):
        self.result = subprocess.CompletedProcess([], returncode, stdout=stdout, stderr="boom")

    def __call__(self, *args, **kwargs):
        return self.result


REPORT_WITH_A_FINDING = json.dumps(
    {"dependencies": [{"name": "x", "version": "1", "vulns": [{"id": "CVE-1", "fix_versions": ["2"]}]}]}
)


@pytest.mark.parametrize(
    ("returncode", "stdout"),
    [
        (2, REPORT_WITH_A_FINDING),
        (0, ""),
        (1, json.dumps({"dependencies": [{"name": "x", "version": "1", "vulns": []}]})),
    ],
)
def test_an_audit_that_did_not_run_fails_rather_than_reading_as_clean(monkeypatch, returncode, stdout):
    monkeypatch.setattr(patch_locks.subprocess, "run", FakeAudit(returncode, stdout))

    with pytest.raises(RuntimeError, match="pip-audit failed"):
        run_pip_audit(Path("requirements.txt"))


def test_an_audit_that_finds_a_vulnerability_returns_it(monkeypatch):
    monkeypatch.setattr(patch_locks.subprocess, "run", FakeAudit(1, REPORT_WITH_A_FINDING))

    assert [finding.package for finding in run_pip_audit(Path("requirements.txt"))] == ["x"]


def finding(name: str, version: str = "1.0", fixable: bool = True) -> Finding:
    return Finding(name, version, (f"CVE-{name}",), fixable)


def test_each_flagged_package_gets_what_happened_to_it():
    flagged = {name: finding(name) for name in ("fixed", "partly", "waiting", "pinned")} | {
        "unfixable": finding("unfixable", fixable=False)
    }
    still = {name: finding(name) for name in ("partly", "waiting", "pinned", "new")} | {
        "unfixable": finding("unfixable", fixable=False)
    }
    before = {"fixed": "1.0", "partly": "1.0", "waiting": "1.0", "pinned": "1.0", "unfixable": "1.0"}
    after = before | {"fixed": "1.1", "partly": "1.1", "new": "0.1"}

    waiting = "not moved yet: its fix is under five days old, or a dependency holds it back"

    rows = {row.package: row for row in outcomes(flagged, still, {"pinned"}, before, after)}

    assert {name: (row.result, row.still_flagged, row.needs_a_person) for name, row in rows.items()} == {
        "fixed": ("1.0 → 1.1", False, False),
        "partly": ("1.0 → 1.1, still flagged", True, False),
        "waiting": (waiting, True, False),
        "pinned": ("pinned exactly in the image's inputs: update it by hand", True, True),
        "unfixable": ("no fixed release yet", True, True),
        "new": (f"newly flagged after the patch; {waiting}", True, False),
    }


def test_every_changed_pin_is_listed_dependencies_included():
    before = {"a": "1", "b": "1", "gone": "1"}
    after = {"a": "2", "b": "1", "new": "1"}

    assert changed_pins(before, after) == ["a 1 → 2", "gone 1 → (removed)", "new (added) → 1"]


def test_a_group_keeps_each_package_once_and_the_gpu_lock_adds_its_own(tmp_path):
    cpu, cuda = tmp_path / "requirements.txt", tmp_path / "requirements-cuda.txt"
    cpu.write_text("torch==2.14.1+cpu \\n    --hash=sha256:x\nanyio==4.15.1\n", encoding="utf-8")
    cuda.write_text("torch==2.14.1+cu130\nanyio==4.15.1\nnvidia-cudnn-cu13==9.0\n", encoding="utf-8")

    assert group_pins([cpu, cuda]) == {"torch": "2.14.1+cpu", "anyio": "4.15.1", "nvidia-cudnn-cu13": "9.0"}


def test_packages_pinned_exactly_in_the_inputs_are_left_to_a_person(tmp_path):
    (tmp_path / "requirements.in").write_text("# The packages.\nSpaCy==3.8.7\nnumpy\n", encoding="utf-8")
    (tmp_path / "torch-cuda.txt").write_text("torch==2.14.1+cu130\n", encoding="utf-8")

    assert pinned_by_hand(tmp_path) == {"spacy", "torch"}
