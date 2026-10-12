"""Builds the Endpoint Python packages into dist/ (one wheel each).

    python build_wheels.py

libendpoint must be built first (native/: cmake --build --preset endpoint):
its libendpoint.so goes into the Core wheel.
"""

import pathlib
import shutil
import subprocess
import sys

here = pathlib.Path(__file__).resolve().parent
library = here.parent / "native" / "build" / "libendpoint.so"
dist = here / "dist"

if not library.exists():
    sys.exit(f"{library} is missing: build native/ first (cmake --preset endpoint, cmake --build --preset endpoint)")

shutil.copy2(library, here / "GHIElectronics.Endpoint.Core" / "GHIElectronics" / "Endpoint" / "Core" / "libendpoint.so")
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
