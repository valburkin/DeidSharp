"""Patches known vulnerabilities in the Python locks that Dependabot cannot regenerate: the
sidecar's two images and the model baselines' image, each compiled with uv across two indexes.

Only the packages pip-audit flags are asked to move, so detection stays as measured. Each lock
records the uv command that built it; this reruns that command with --upgrade-package for each
flagged package, taking no release from the last five days (sidecar/README.md, "Dependencies").
uv may move a dependency a new release needs; the summary lists every pin that changed.
A daily workflow runs it and proposes the result as a pull request
(.github/workflows/python-locks.yml).

    python sidecar/tools/patch_locks.py [--summary <file>] [--attention <file>]

A package whose fix is too recent stays where it is, with a warning, until the fix is old
enough. One that no patch can move (no fixed release, or pinned exactly in the image's inputs)
is written to the attention file, for a person. Failing to audit or to compile fails the run:
an audit that can't run never reads as "nothing found".
"""

import argparse
import itertools
import json
import re
import shlex
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from datetime import UTC, date, datetime, timedelta
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

# Locks that move together. The GPU lock pins every package of the CPU lock at the same
# version (sidecar/tests/test_locks.py), so a package flagged in either moves in both.
LOCK_GROUPS = (
    ("sidecar/requirements.txt", "sidecar/requirements-cuda.txt"),
    ("tools/baselines/models/requirements.txt",),
)

# The files beside a lock that pin packages exactly (requirements.in, the GPU image's PyTorch).
# uv can't move those pins; a person edits the file.
PINNED_INPUTS = ("requirements.in", "torch-cuda.txt")

# Releases younger than this are left out, as Dependabot's cooldown does for everything else.
COOLDOWN = timedelta(days=5)

HEADER = re.compile(r"^#\s+(uv pip compile .+)$", re.MULTILINE)
PIN = re.compile(r"^([A-Za-z0-9_.\-]+)==(\S+)", re.MULTILINE)
# PyTorch's builds come from its own index, where pip-audit finds no advisories, so the
# release is also audited by its plain version.
TORCH = re.compile(r"^torch==([0-9.]+)", re.MULTILINE)

# The recorded command's holds and upgrades belong to the run that wrote it. A patch drops
# them: uv keeps every other pin as the lock has it, and a global --exclude-newer would
# downgrade the packages PyTorch's indexes mirror without upload dates.
DROPPED_WITH_VALUE = ("--exclude-newer", "--exclude-newer-package")
DROPPED = ("--upgrade", "-U")


@dataclass(frozen=True)
class Finding:
    """A package pip-audit flagged, by name only: advisories are public identifiers."""

    package: str
    version: str
    advisories: tuple[str, ...]
    fixable: bool


@dataclass(frozen=True)
class Row:
    """What happened to one flagged package."""

    package: str
    advisories: tuple[str, ...]
    result: str
    still_flagged: bool
    needs_a_person: bool


@dataclass(frozen=True)
class GroupResult:
    """One group of locks after a patch: its flagged packages, and every pin that changed."""

    rows: list[Row]
    changed: list[str]


def normalize(name: str) -> str:
    return re.sub(r"[-_.]+", "-", name).lower()


def compile_command(header: str, output_file: str, packages: list[str], cutoff: date) -> list[str]:
    """The lock's recorded uv command, moving only the given packages."""
    args = shlex.split(header)
    if args[:3] != ["uv", "pip", "compile"]:
        raise ValueError(f"The recorded command for {output_file} is not uv pip compile.")
    kept: list[str] = []
    skip_value = False
    for arg in args:
        if skip_value:
            skip_value = False
        elif arg in DROPPED_WITH_VALUE:
            skip_value = True
        elif arg not in DROPPED and not arg.startswith(tuple(f"{flag}=" for flag in DROPPED_WITH_VALUE)):
            kept.append(arg)
    if output_of(kept) != output_file:
        raise ValueError(f"The command recorded in {output_file} writes another file.")
    for package in sorted(packages):
        kept += ["--upgrade-package", package, "--exclude-newer-package", f"{package}={cutoff.isoformat()}"]
    return kept


def output_of(args: list[str]) -> str | None:
    for flag, value in itertools.pairwise(args):
        if flag in ("--output-file", "-o"):
            return value
    return next((arg.split("=", 1)[1] for arg in args if arg.startswith("--output-file=")), None)


def parse_audit(report: str) -> list[Finding]:
    """pip-audit's JSON report: the packages with at least one advisory. A package moves if any of
    its advisories has a fixed release; one that is left still fails the audit afterwards."""
    findings = []
    for dependency in json.loads(report)["dependencies"]:
        vulns = dependency.get("vulns") or []
        if vulns:
            findings.append(
                Finding(
                    package=normalize(dependency["name"]),
                    version=dependency["version"],
                    advisories=tuple(vuln["id"] for vuln in vulns),
                    fixable=any(vuln.get("fix_versions") for vuln in vulns),
                )
            )
    return findings


def run_pip_audit(requirement: Path, *options: str) -> list[Finding]:
    result = subprocess.run(
        [
            *(sys.executable, "-m", "pip_audit", "--requirement", str(requirement), *options),
            *("--disable-pip", "--progress-spinner", "off", "--format", "json"),
        ],
        capture_output=True,
        text=True,
        check=False,
    )
    # pip-audit exits with 1 when it finds a vulnerability. Any other exit code, an empty
    # report, or a 1 without a finding stops the run.
    if result.returncode not in (0, 1) or not result.stdout.strip():
        raise RuntimeError(f"pip-audit failed on {requirement.name}, exit code {result.returncode}:\n{result.stderr}")
    findings = parse_audit(result.stdout)
    if result.returncode == 1 and not findings:
        raise RuntimeError(f"pip-audit failed on {requirement.name}:\n{result.stderr}")
    return findings


def audit(lock: Path) -> list[Finding]:
    findings = run_pip_audit(lock, "--require-hashes")
    torch = TORCH.search(lock.read_text(encoding="utf-8"))
    if torch:
        with tempfile.TemporaryDirectory() as folder:
            plain = Path(folder) / "torch.txt"
            plain.write_text(f"torch=={torch.group(1)}\n", encoding="utf-8")
            findings += run_pip_audit(plain, "--no-deps")
    return findings


def audit_group(locks: list[Path]) -> dict[str, Finding]:
    return {finding.package: finding for lock in locks for finding in audit(lock)}


def pins(text: str) -> dict[str, str]:
    return {normalize(name): version for name, version in PIN.findall(text)}


def group_pins(locks: list[Path]) -> dict[str, str]:
    """Every package the group's locks pin. The GPU lock adds the CUDA libraries; a package in
    both is pinned at the same version, PyTorch's build aside, and the first lock's pin is kept."""
    merged: dict[str, str] = {}
    for lock in locks:
        for name, version in pins(lock.read_text(encoding="utf-8")).items():
            merged.setdefault(name, version)
    return merged


def pinned_by_hand(folder: Path) -> set[str]:
    return {
        name
        for input_file in PINNED_INPUTS
        if (folder / input_file).exists()
        for name in pins((folder / input_file).read_text(encoding="utf-8"))
    }


def outcomes(
    flagged: dict[str, Finding],
    still: dict[str, Finding],
    by_hand: set[str],
    before: dict[str, str],
    after: dict[str, str],
) -> list[Row]:
    """What happened to each package flagged before or after the patch."""
    rows = []
    for name in sorted(flagged.keys() | still.keys()):
        finding = still.get(name) or flagged[name]
        moved = (
            f"{before[name]} → {after.get(name, 'removed')}"
            if name in before and before[name] != after.get(name)
            else ""
        )
        newly = "" if name in flagged else "newly flagged after the patch; "
        if name not in still:
            result, needs_a_person = moved or "no longer flagged", False
        elif name in by_hand:
            result, needs_a_person = f"{newly}pinned exactly in the image's inputs: update it by hand", True
        elif not finding.fixable:
            result, needs_a_person = f"{newly}no fixed release yet", True
        elif moved:
            result, needs_a_person = f"{newly}{moved}, still flagged", False
        else:
            result, needs_a_person = (
                f"{newly}not moved yet: its fix is under five days old, or a dependency holds it back",
                False,
            )
        rows.append(Row(name, finding.advisories, result, name in still, needs_a_person))
    return rows


def changed_pins(before: dict[str, str], after: dict[str, str]) -> list[str]:
    """Every pin that moved, the flagged packages' dependencies included."""
    return [
        f"{name} {before.get(name, '(added)')} → {after.get(name, '(removed)')}"
        for name in sorted(before.keys() | after.keys())
        if before.get(name) != after.get(name)
    ]


def patch(lock: Path, packages: list[str], cutoff: date) -> None:
    header = HEADER.search(lock.read_text(encoding="utf-8"))
    if header is None:
        raise ValueError(f"{lock.name} does not record the command that built it.")
    subprocess.run(compile_command(header.group(1), lock.name, packages, cutoff), cwd=lock.parent, check=True)


def patch_group(group: tuple[str, ...], cutoff: date) -> GroupResult:
    locks = [ROOT / path for path in group]
    flagged = audit_group(locks)
    if not flagged:
        return GroupResult([], [])
    by_hand = pinned_by_hand(locks[0].parent)
    movable = sorted(name for name, finding in flagged.items() if finding.fixable and name not in by_hand)
    originals = {lock: lock.read_bytes() for lock in locks}
    before = group_pins(locks)
    if movable:
        for lock in locks:
            patch(lock, movable, cutoff)
    # uv writes the day's cutoff into the header, so a lock whose pins didn't move goes back to
    # what it was: otherwise every day would propose a change to the header alone.
    for lock, original in originals.items():
        if pins(lock.read_text(encoding="utf-8")) == pins(original.decode("utf-8")):
            lock.write_bytes(original)
    after = group_pins(locks)
    rows = outcomes(flagged, audit_group(locks), by_hand, before, after)
    for row in rows:
        if row.still_flagged:
            print(f"::warning::{group[0]}: {row.package} still has known vulnerabilities ({row.result}).")
    return GroupResult(rows, changed_pins(before, after))


def table(rows: list[tuple[str, Row]]) -> str:
    lines = ["| Lock | Package | Advisories | Result |", "|---|---|---|---|"]
    lines += [f"| `{lock}` | {row.package} | {', '.join(row.advisories)} | {row.result} |" for lock, row in rows]
    return "\n".join(lines) + "\n"


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n", 1)[0])
    parser.add_argument("--summary", type=Path, help="write what was flagged and what moved, in Markdown")
    parser.add_argument("--attention", type=Path, help="write the flagged packages no patch can move, in Markdown")
    arguments = parser.parse_args(argv)

    cutoff = datetime.now(UTC).date() - COOLDOWN
    rows: list[tuple[str, Row]] = []
    changed: list[str] = []
    failed = False
    for group in LOCK_GROUPS:
        # A group that can't be audited or compiled fails the run, once the others had their turn.
        try:
            result = patch_group(group, cutoff)
        except (OSError, RuntimeError, ValueError, subprocess.CalledProcessError) as error:
            print(f"::error::{group[0]}: {error}")
            failed = True
            continue
        rows += [(group[0], row) for row in result.rows]
        changed += [f"`{group[0]}`: {pin}" for pin in result.changed]

    if arguments.summary is not None:
        summary = table(rows) if rows else ""
        if changed:
            summary += "\nPins that changed:\n\n" + "".join(f"- {line}\n" for line in changed)
        arguments.summary.write_text(summary, encoding="utf-8")
    if arguments.attention is not None:
        stuck = [(lock, row) for lock, row in rows if row.needs_a_person]
        arguments.attention.write_text(table(stuck) if stuck else "", encoding="utf-8")
    print(f"{len(rows)} vulnerable package(s) found." if rows else "No known vulnerability in the Python locks.")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
