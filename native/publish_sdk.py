"""Packages the C/C++ SDK for Endpoint and stages it for release:
native/publish/<version>/endpoint-sdk-<version>.zip, plus a one-line
native/publish/latest.txt pointing at the current version.

    python publish_sdk.py

The zip contains the whole 'endpoint' folder (headers, libendpoint.a arm +
arm64, libusb/hidapi link stubs, licenses), staged by copy-sdk.js into the
VS Code extension's C/C++ template. Run that first.

Customers update an existing project in one of two ways:
 1) endpoint-cli sdk install <version>   (reads native/publish/latest.txt
    to find the version and downloads the matching zip over endpoint/).
 2) Download the zip from the Endpoint Downloads page and unzip it over
    the project's endpoint/ folder manually.
"""

import pathlib
import re
import shutil
import sys
import zipfile

here = pathlib.Path(__file__).resolve().parent  # native/
props = here.parent / "Dotnet" / "GHIElectronics.Endpoint.Libraries" / "Endpoint.Libraries.props"

match = re.search(r"<EndpointLibrariesVersion>([^<]+)</EndpointLibrariesVersion>", props.read_text(encoding="utf-8"))
if not match:
    sys.exit(f"EndpointLibrariesVersion missing in {props}")
version = match.group(1).strip()

# The staged 'endpoint' folder has everything a C/C++ customer ships with: headers,
# libendpoint.a for both arm + arm64, libusb/hidapi link stubs, licenses.
template = here.parent.parent / "Endpoint-Extensions" / "vscode" / "templates" / "cpp" / "endpoint"
if not template.exists():
    sys.exit(f"{template} is missing. Run Endpoint-Extensions/vscode/scripts/copy-sdk.js first.")

target_dir = here / "publish" / version
if target_dir.exists():
    shutil.rmtree(target_dir)
target_dir.mkdir(parents=True)

zip_path = target_dir / f"endpoint-sdk-{version}.zip"
files = 0
with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
    for f in sorted(template.rglob("*")):
        if f.is_file():
            # Top level in the zip is 'endpoint/...' so extracting over a project
            # folder lands the files back in the project's endpoint/ folder.
            arcname = "endpoint/" + str(f.relative_to(template)).replace("\\", "/")
            z.write(f, arcname)
            files += 1

(here / "publish" / "latest.txt").write_text(version + "\n", encoding="utf-8")

size_mb = zip_path.stat().st_size / (1024 * 1024)
print(f"Published C/C++ SDK {version}: {zip_path.name} ({files} files, {size_mb:.1f} MB)")
print(f"Updated native/publish/latest.txt -> {version}")
