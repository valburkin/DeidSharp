"""The CPU and GPU locks must detect the same: every package the CPU image installs, the GPU
image installs at the same version. Only PyTorch's build differs (and the CUDA libraries
it brings), so a lock regenerated for one but not the other fails here."""

import re
from pathlib import Path

SIDECAR = Path(__file__).resolve().parents[1]
PIN = re.compile(r"^([A-Za-z0-9_.\-]+)==(\S+)", re.MULTILINE)


def pins(name: str) -> dict[str, str]:
    return dict(PIN.findall((SIDECAR / name).read_text(encoding="utf-8")))


def without_local_version(version: str) -> str:
    return version.split("+", 1)[0]


def test_the_gpu_lock_pins_every_cpu_package_at_the_same_version():
    cpu, cuda = pins("requirements.txt"), pins("requirements-cuda.txt")

    differing = {
        name: (version, cuda.get(name))
        for name, version in cpu.items()
        if name != "torch" and cuda.get(name) != version
    }
    assert differing == {}


def test_both_locks_pin_the_same_pytorch_release_in_their_own_build():
    cpu, cuda = pins("requirements.txt")["torch"], pins("requirements-cuda.txt")["torch"]

    assert without_local_version(cpu) == without_local_version(cuda)
    assert cpu.endswith("+cpu")
    assert "+cu" in cuda


def test_the_gpu_override_is_the_torch_the_gpu_lock_holds():
    override = re.search(r"^torch==(\S+)", (SIDECAR / "torch-cuda.txt").read_text(encoding="utf-8"), re.MULTILINE)

    assert override is not None
    assert override.group(1) == pins("requirements-cuda.txt")["torch"]
