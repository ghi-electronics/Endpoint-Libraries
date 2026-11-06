"""The chip the library is running on, read once from
/proc/device-tree/model + /proc/device-tree/compatible at module import.
Mirrors .NET's GHIElectronics.Endpoint.Core.Internal.ChipDetect.

Chip-dispatch inside the library (pin muxing, device paths) uses this.
Customer-facing APIs do not expose it - the board identity on the host
side comes from the USB interface name.

Usage:

    from GHIElectronics.Endpoint.Core._chip_detect import Current, Chip, is_ma35

    if is_ma35:
        ...
"""

import os
from enum import IntEnum


class Chip(IntEnum):
    """The detected chip family."""
    Unknown = 0
    """Unknown - fallback for dev machines without a Linux device tree."""
    Epm815 = 1
    """STM32MP153 (EPM815 board family). 32-bit ARMv7."""
    Epn256 = 2
    """Nuvoton MA35D1 (EPN256 board family, dual A35). 64-bit ARM."""
    Epn128 = 3
    """Nuvoton MA35D0 (EPN128 board family, single A35, Chili Pro). 64-bit ARM."""


def _read_dt_node_text(path):
    """Read a /proc/device-tree node file and strip the trailing NUL the
    kernel appends. Returns None if the file does not exist / is unreadable."""
    try:
        if not os.path.exists(path):
            return None
        with open(path, "rb") as f:
            raw = f.read()
        # Device-tree node files terminate with a NUL byte; trim it so
        # substring matches behave intuitively.
        return raw.rstrip(b"\x00").decode("utf-8", errors="replace")
    except OSError:
        return None


def _detect():
    try:
        # /proc/device-tree/model carries the board/SoC name verbatim
        # ("Nuvoton MA35D05K-IoT" on Chili Pro, "Nuvoton MA35D1 SoM ..." on
        # EPN256). Use it first because /proc/device-tree/compatible on
        # MA35D0 reports just "nuvoton,ma35d1" - the family string - which
        # would send MA35D0 down the MA35D1 code path.
        model = _read_dt_node_text("/proc/device-tree/model")
        if model is not None:
            # Order matters: "MA35D0" matches "MA35D05K"/"MA35D06K"; check
            # it first so it wins over a later "MA35D1" fallback that would
            # also match a D0 name by accident.
            low = model.lower()
            if "ma35d0" in low:
                return Chip.Epn128
            if "ma35d1" in low:
                return Chip.Epn256
            if "stm32mp15" in low:
                return Chip.Epm815

        # Fallback to compatible for boards whose vendor DTS does not set a
        # distinctive model string.
        compat = _read_dt_node_text("/proc/device-tree/compatible")
        if compat is not None:
            low = compat.lower()
            if "ma35d0" in low:
                return Chip.Epn128
            if "ma35d1" in low:
                return Chip.Epn256
            if "stm32mp15" in low:
                return Chip.Epm815
    except OSError:
        # No device tree (dev machine, container, test host): Unknown.
        pass
    return Chip.Unknown


# Cached at module import; do NOT re-probe per call.
Current = _detect()
"""The detected chip; evaluated once at module import."""

is_ma35 = Current in (Chip.Epn256, Chip.Epn128)
"""True for any Nuvoton MA35 chip (EPN256 or EPN128)."""

is_stm32_mp1 = Current == Chip.Epm815
"""True for the STM32MP1 family (EPM815)."""
