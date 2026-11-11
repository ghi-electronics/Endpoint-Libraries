"""Copies freshly-built wheels from dist/ into publish/<version>/ and writes an
index.html so pip can resolve them via --find-links at that URL. Temporary
bridge until every GHI Python package is on PyPI.

    python publish_wheels.py

Reads the version from ../Dotnet/GHIElectronics.Endpoint.Libraries/Endpoint.Libraries.props
so one source of truth drives .NET, Python and the publish folder name.

The publish/ folder is committed to git. Customers' extensions point --find-links
at the raw.githubusercontent.com URL for publish/<version>/index.html and pip
downloads each wheel from the raw URLs listed in that HTML. When all packages
finally live on PyPI, the extension stops passing --find-links and this script
and the publish/ folder can be deleted.
"""

import pathlib
import re
import shutil
import sys

here = pathlib.Path(__file__).resolve().parent
dist = here / "dist"
props = here.parent / "Dotnet" / "GHIElectronics.Endpoint.Libraries" / "Endpoint.Libraries.props"

match = re.search(r"<EndpointLibrariesVersion>([^<]+)</EndpointLibrariesVersion>", props.read_text(encoding="utf-8"))
if not match:
    sys.exit(f"EndpointLibrariesVersion missing in {props}")
version = match.group(1).strip()

wheels = sorted(dist.glob("*.whl"))
if not wheels:
    sys.exit(f"No wheels in {dist}. Run build_wheels.py first.")

target = here / "publish" / version
if target.exists():
    shutil.rmtree(target)
target.mkdir(parents=True)

for wheel in wheels:
    shutil.copy2(wheel, target / wheel.name)

# pip's --find-links accepts a URL that returns an HTML page with <a href=...>
# links to each wheel. Keep the links relative so the same index.html works
# from any URL (raw.githubusercontent.com, GitHub Pages, a local webserver, ...)
links = "\n".join(f'    <a href="{w.name}">{w.name}</a><br>' for w in wheels)
index = f"""<!DOCTYPE html>
<html><head><title>GHIElectronics.Endpoint {version} wheels</title></head>
<body>
<h1>GHIElectronics.Endpoint Python wheels, {version}</h1>
<p>Served as a temporary pip --find-links source while PyPI publishing
catches up. Once every package is on PyPI, this folder goes away.</p>
{links}
</body></html>
"""
(target / "index.html").write_text(index, encoding="utf-8")

# publish/latest.txt names the current version. endpoint-cli gitpip fetches
# this file to discover which publish/<version>/ folder to use, so adding
# new wheels never requires a new extension build. Writes the SAME version
# that was just published; always points at this release.
(here / "publish" / "latest.txt").write_text(version + "\n", encoding="utf-8")

print(f"Published {len(wheels)} wheels + index.html to {target}")
print(f"Updated publish/latest.txt -> {version}")
for wheel in wheels:
    print(f"  {wheel.name}")
