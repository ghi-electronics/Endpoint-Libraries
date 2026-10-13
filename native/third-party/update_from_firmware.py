"""Takes the board's libusb and hidapi headers, licenses and function lists
from a firmware build (Endpoint-Firmware's Buildroot output), for C/C++
programs: they include the headers and link against stubs built from the
function lists (CMakeLists.txt: build/libusb-1.0.so, build/libhidapi-libusb.so).
The stubs only carry the functions' names; on the board the program uses the
firmware's own libusb and hidapi. Run it (in WSL) after the firmware changes
these libraries:

    python3 update_from_firmware.py <Endpoint-Firmware>/output
"""

import glob
import os
import shutil
import subprocess
import sys

here = os.path.dirname(os.path.abspath(__file__))
if len(sys.argv) != 2:
    sys.exit(__doc__)
output = os.path.abspath(sys.argv[1])
staging = os.path.join(output, "staging", "usr")
nm = glob.glob(os.path.join(output, "host", "bin", "*-linux-gnueabihf-nm"))[0]

LIBRARIES = [
    # (folder, library file, its SONAME, headers, license files, the Buildroot build folder)
    ("libusb", "libusb-1.0.so.0", ["libusb-1.0/libusb.h"], ["COPYING"], "libusb-1.0.*"),
    ("hidapi", "libhidapi-libusb.so.0", ["hidapi/hidapi.h", "hidapi/hidapi_libusb.h"],
     ["LICENSE.txt", "LICENSE-bsd.txt", "LICENSE-gpl3.txt", "LICENSE-orig.txt"], "hidapi-libusb-*"),
]

for folder, soname, headers, licenses, build in LIBRARIES:
    target = os.path.join(here, folder)
    shutil.rmtree(target, ignore_errors=True)
    for header in headers:
        os.makedirs(os.path.join(target, "include", os.path.dirname(header)), exist_ok=True)
        shutil.copy2(os.path.join(staging, "include", header), os.path.join(target, "include", header))
    source = sorted(glob.glob(os.path.join(output, "build", build)))[-1]
    for license in licenses:
        shutil.copy2(os.path.join(source, license), os.path.join(target, license))
    version = os.path.basename(source).rsplit("-", 1)[-1]
    library = os.path.realpath(os.path.join(staging, "lib", soname))
    names = sorted(line.split()[-1] for line in subprocess.run([nm, "-D", "--defined-only", library], check=True,
                                                               capture_output=True, text=True).stdout.splitlines()
                   if line.split()[1] == "T")
    with open(os.path.join(target, "stub.c"), "w") as stub:
        stub.write(f"/* A link-only stand-in for the board's {soname} ({folder} {version}): the\n"
                   f"   names of its {len(names)} functions, made by update_from_firmware.py. Programs\n"
                   f"   link against it and use the firmware's library on the board. */\n\n")
        for name in names:
            stub.write(f"void {name}(void) {{}}\n")
    print(f"{folder} {version}: {len(headers)} headers, {len(names)} functions")
