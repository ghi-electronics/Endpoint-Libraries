"""Builds the Endpoint Python packages into dist/ (one wheel each).

    python build_wheels.py

libendpoint must be built first for BOTH architectures (v0.5.x ships both
32-bit EPM815 and 64-bit EPN256/EPN128 support in the same wheel):

    cd ../native
    cmake --preset endpoint-arm   && cmake --build --preset endpoint-arm
    cmake --preset endpoint-arm64 && cmake --build --preset endpoint-arm64

The two libendpoint.so files are copied into the Core package as
libendpoint-arm.so and libendpoint-arm64.so. _native.py picks the right
one at import time via platform.machine(). If only one arch is built,
that one still ships (and the other board family is unsupported in this
particular wheel build).
"""

import pathlib
import shutil
import subprocess
import sys

here = pathlib.Path(__file__).resolve().parent
native_build = here.parent / "native" / "build"
core_native_dir = here / "GHIElectronics.Endpoint.Core" / "GHIElectronics" / "Endpoint" / "Core"
dist = here / "dist"

# Where the two preset outputs land (see native/CMakePresets.json).
arch_sources = {
    "arm":   native_build / "linux-arm"   / "libendpoint.so",
    "arm64": native_build / "linux-arm64" / "libendpoint.so",
}

# Clean any old arch-specific copies so we never ship a stale one alongside
# a fresh build of the other arch.
for stale in core_native_dir.glob("libendpoint*.so"):
    stale.unlink()

copied = []
for arch, source in arch_sources.items():
    if source.exists():
        target = core_native_dir / f"libendpoint-{arch}.so"
        shutil.copy2(source, target)
        copied.append(arch)
    else:
        print(f"note: skipping {arch} (no {source})")

if not copied:
    sys.exit(f"Neither {arch_sources['arm']} nor {arch_sources['arm64']} exist. "
             f"Build libendpoint first (cmake --preset endpoint-arm[-64]).")

print(f"Bundled libendpoint for: {', '.join(copied)}")

shutil.rmtree(dist, ignore_errors=True)

for package in sorted(here.glob("GHIElectronics.Endpoint.*")):
    print("Building", package.name)
    subprocess.run([sys.executable, "-m", "pip", "wheel", "--no-deps", "--quiet", "--wheel-dir", str(dist), str(package)],
                   check=True)
    for leftover in ("build", f"{package.name}.egg-info"):
        shutil.rmtree(package / leftover, ignore_errors=True)

print()
for wheel in sorted(dist.glob("*.whl")):
    print(f"{wheel.name}  ({wheel.stat().st_size // 1024} KB)")
