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


class UnknownBoardSupport:
    """Default board support. Every peripheral method throws a
    NotImplementedError whose message names the missing package.

    Concrete board classes (EPM815BoardSupport, EPN256BoardSupport,
    EPN128BoardSupport, Zero2WBoardSupport) inherit this and override only
    the methods their board actually implements. Methods they don't override
    keep the throwing default, which is the right answer for peripherals
    that don't exist on that board (e.g. CAN on Pi, DAC on Nuvoton).

    Also the instance registered in BoardSupport.Current before any package
    has imported — so peripheral calls on a bare install throw a helpful
    "add the right package" message instead of an AttributeError.
    """

    Name = "Unknown"

    # ---------- GPIO introspection ----------
    def GpioChipLabel(self, port): self._nope("GPIO introspection")
    def GpioChipId(self, pin):     self._nope("GPIO introspection")
    def GpioLineId(self, pin):     self._nope("GPIO introspection")
    def GpioName(self, pin):       self._nope("GPIO introspection")

    # ---------- Peripheral dispatch ----------
    def I2cInitialize(self, bus, frequency): self._nope("I2C")
    def I2cUnInitialize(self, bus):          self._nope("I2C")
    def I2cGetFrequency(self, bus):          self._nope("I2C")

    def SpiInitialize(self, bus, buffer_size): self._nope("SPI")
    def SpiUnInitialize(self, bus):            self._nope("SPI")

    def PwmInitialize(self, pin):      self._nope("PWM")
    def PwmUnInitialize(self, pin):    self._nope("PWM")
    def PwmGetChipId(self, pin):       self._nope("PWM")
    def PwmGetChannelId(self, pin):    self._nope("PWM")

    def CanInitialize(self, port, bitrate): self._nope("CAN")
    def CanUnInitialize(self, port):        self._nope("CAN")

    def SerialPortInitialize(self, port_name, enable_hw_flow_control):
        self._nope("serial port")
    def SerialPortUnInitialize(self, port_name):
        self._nope("serial port")

    def _nope(self, what):
        raise NotImplementedError(
            f"{what} is not supported on this board ('{self.Name}'). "
            "If this is an Endpoint board (EPM815 / EPDomino24 / EPN256 / EPN128), "
            "import GHIElectronics.Endpoint.Pins.<Board>. "
            "If this is a Raspberry Pi Zero 2 W, "
            "import GHIElectronics.Endpoint.Drivers.RaspberryPi."
        )


class BoardSupport:
    """Core's view of the currently-active board. Pins.* / Drivers.*
    packages fill it in on import; user code never touches this class
    directly."""

    _current = UnknownBoardSupport()

    # Tracked by a flag rather than `isinstance(_current, UnknownBoardSupport)`:
    # a board class is free to derive from UnknownBoardSupport to inherit its
    # throwing defaults (the C++ and .NET boards do), and the isinstance test
    # would then report "nothing registered" even with a real board in place.
    _registered = False

    @staticmethod
    def Current():
        """Returns the registered board support. Starts as
        UnknownBoardSupport so peripheral calls before registration throw
        a clear "add the right package" message instead of None errors."""
        return BoardSupport._current

    @staticmethod
    def IsRegistered():
        """True if a board has registered its implementation."""
        return BoardSupport._registered

    @staticmethod
    def Register(board):
        """Called on module import. Replacing UnknownBoardSupport is always
        allowed; replacing with the same-named board is a no-op; replacing
        with a different real board throws."""
        if board is None:
            raise ValueError("board is None")
        if not BoardSupport._registered or BoardSupport._current.Name == board.Name:
            BoardSupport._current = board
            BoardSupport._registered = True
            return
        raise RuntimeError(
            f"Board '{BoardSupport._current.Name}' is already registered; "
            f"cannot also register '{board.Name}'. A program must import "
            "only one board package (Pins.* for Endpoint, Drivers.RaspberryPi for Pi)."
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
    def Save(pin):
        """Stash the pin's current function so Restore can put it back.

        The binary keeps the snapshot under /run, not in this process, so a
        pin saved by one program is still restorable by another.
        """
        Pinmux._run("save", str(int(pin)))

    @staticmethod
    def Restore(pin):
        """Put the pin back to what Save captured. Best effort: a pin that
        was never saved is left alone rather than raising, so cleanup paths
        stay simple."""
        try:
            Pinmux._run("restore", str(int(pin)))
        except (RuntimeError, OSError):
            pass

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
