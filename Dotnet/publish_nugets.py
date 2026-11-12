"""Copies freshly-built library nupkgs from GHIElectronics.Endpoint.Libraries/output/
into Dotnet/publish/<ver>/. Mirrors the Python and native publish layouts so one
version folder per release carries all deliverables for its language.

    python publish_nugets.py

Reads EndpointLibrariesVersion from Endpoint.Libraries.props and strips any
"-preview" suffix for the folder name (0.5.2-preview -> 0.5.2).
"""

import pathlib
import re
import shutil
import sys

here = pathlib.Path(__file__).resolve().parent  # Endpoint-Libraries/Dotnet/
output = here / "GHIElectronics.Endpoint.Libraries" / "output"
props = here / "GHIElectronics.Endpoint.Libraries" / "Endpoint.Libraries.props"

match = re.search(r"<EndpointLibrariesVersion>([^<]+)</EndpointLibrariesVersion>", props.read_text(encoding="utf-8"))
if not match:
    sys.exit(f"EndpointLibrariesVersion missing in {props}")
version = match.group(1).strip()
# Strip "-preview" / "-rc.1" / etc. for the folder name.
folder = re.sub(r"[-+].*$", "", version)

nupkgs = sorted(output.glob("*.nupkg"))
if not nupkgs:
    sys.exit(f"No nupkgs in {output}. Run 'dotnet build -c Release' on the solution first.")

target = here / "publish" / folder
if target.exists():
    shutil.rmtree(target)
target.mkdir(parents=True)

for nupkg in nupkgs:
    shutil.copy2(nupkg, target / nupkg.name)

(here / "publish" / "latest.txt").write_text(folder + "\n", encoding="utf-8")

print(f"Published {len(nupkgs)} nupkgs to {target}")
print(f"Updated Dotnet/publish/latest.txt -> {folder}")
for nupkg in nupkgs:
    print(f"  {nupkg.name}")
