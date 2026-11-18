"""BoardSupport registry + Pinmux shell-out helper.

Mirrors .NET's GHIElectronics.Endpoint.Core.BoardSupport / Internal.Pinmux
so Python user code uses the same chip-neutral shape: Core's controllers
(I2cController, SpiController, ...) dispatch through BoardSupport.Current,
which is set by whichever Pins.* package the user imported.

Each Pins.* package registers its board support on import:

    # inside GHIElectronics.Endpoint.Pins.EPM815/__init__.py
    from GHIElectronics.Endpoint.Core import BoardSupport
    BoardSupport.Register(_EPM815BoardSupport())

So user code just does `import GHIElectronics.Endpoint.Pins.EPDomino24` and
the registration is automatic.

Pinmux shell-out: calls /usr/sbin/endpoint-pinmux on the board. The binary
ships in the Buildroot image (package endpoint-pinmux); .NET, Python, and
C/C++ all shell out to the same binary with the same packed-uint32 function
format (see Endpoint-Firmware/pinmux/src/pinmux.h for each chip's packing).
"""

import subprocess


class BoardSupport:
    """Core's view of the currently-active board. Pins.* packages fill it in
    on import; user code never touches this class directly."""

    _current = None

    @staticmethod
    def Current():
        """Returns the registered board support object. Throws if no Pins.*
        package is imported yet."""
        if BoardSupport._current is None:
            raise RuntimeError(
                "No GHIElectronics.Endpoint.Pins.* package is imported. "
                "Add `import GHIElectronics.Endpoint.Pins.EPDomino24` "
                "(or .EPM815 / .EPN256 / .EPN128) to your program."
            )
        return BoardSupport._current

    @staticmethod
    def IsRegistered():
        """True if a Pins.* package has registered its board."""
        return BoardSupport._current is not None

    @staticmethod
    def Register(board):
        """Called by a Pins.* package on import. Same-name registrations are
        idempotent (so re-imports work); a different Name throws."""
        if board is None:
            raise ValueError("board is None")
        if BoardSupport._current is None or BoardSupport._current.Name == board.Name:
            BoardSupport._current = board
            return
        raise RuntimeError(
            f"Board '{BoardSupport._current.Name}' is already registered; "
            f"cannot also register '{board.Name}'. A program must import "
            "only one Pins.* board package."
        )


class Pinmux:
    """Fork+exec wrapper around /usr/sbin/endpoint-pinmux.

    The binary writes SoC pinmux registers via /dev/mem. One binary per chip
    ships in the Buildroot image; the Pins.* packages call these methods
    from their Save / Restore / SetFunction. Keeps chip-specific register
    knowledge in one place (the C binary) so .NET, Python, and C/C++ don't
    each re-implement it.

    `function` is a chip-specific packed uint32 — see Endpoint-Firmware/
    pinmux/src/pinmux.h for each chip's layout. Example (STM32MP1):
    [Mode:2][Type:1][Speed:2][Pull:2][AF:4].
    """

    _PATH = "/usr/sbin/endpoint-pinmux"

    @staticmethod
    def Set(pin, function):
        """Set the pin's function. Throws if the binary exits non-zero."""
        Pinmux._run("set", str(int(pin)), f"0x{int(function):X}")

    @staticmethod
    def Get(pin):
        """Read the pin's current function (packed uint32, chip-specific layout)."""
        out = Pinmux._run_capture("get", str(int(pin)))
        # Output: "pin=<n> function=0x<hex> level=<0|1>\n"
        for token in out.split():
            if token.startswith("function="):
                value = token[len("function="):]
                return int(value, 0)  # auto-detects 0x prefix
        raise RuntimeError(
            f"endpoint-pinmux get {pin}: unexpected output '{out.rstrip()}'"
        )

    @staticmethod
    def _run(*args):
        try:
            subprocess.run(
                [Pinmux._PATH, *args],
                check=True,
                capture_output=True,
                text=True,
            )
        except subprocess.CalledProcessError as e:
            raise RuntimeError(
                f"{Pinmux._PATH} {' '.join(args)} failed "
                f"(exit {e.returncode}): {e.stderr.rstrip()}"
            ) from None

    @staticmethod
    def _run_capture(*args):
        try:
            result = subprocess.run(
                [Pinmux._PATH, *args],
                check=True,
                capture_output=True,
                text=True,
            )
        except subprocess.CalledProcessError as e:
            raise RuntimeError(
                f"{Pinmux._PATH} {' '.join(args)} failed "
                f"(exit {e.returncode}): {e.stderr.rstrip()}"
            ) from None
        return result.stdout
