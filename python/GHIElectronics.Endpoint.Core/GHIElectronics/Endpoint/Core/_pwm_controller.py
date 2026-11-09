"""Chip-neutral PWM controller dispatcher. Mirrors .NET's
GHIElectronics.Endpoint.Core.PwmController: dispatches to the right chip
helper based on the board detected at startup.

Usage:

    from GHIElectronics.Endpoint.Core import PwmController, EPN256
    PwmController.Initialize(EPN256.Pwm.Pin.PK5)
"""

from GHIElectronics.Endpoint.Core._chip_detect import Current, Chip
from GHIElectronics.Endpoint.Core._epm815 import EPM815
from GHIElectronics.Endpoint.Core._epn256 import EPN256
from GHIElectronics.Endpoint.Core._errors import NotSupportedException


class PwmController:
    """Chip-neutral entry point for PWM setup. Call once before using a
    PWM channel: it brings up the PWM channel and muxes its pin."""

    @staticmethod
    def Initialize(pin):
        """Brings up a PWM channel. The argument is a pin constant from the
        chip's namespace; only that pin is claimed."""
        if Current == Chip.Epn128:
            raise NotSupportedException(
                "EPN128 (MA35D0 Chili Pro) does not bond out EPWM pins.")
        if Current == Chip.Epn256:
            EPN256.Pwm.Initialize(pin)
        else:
            EPM815.Pwm.Initialize(pin)

    @staticmethod
    def UnInitialize(pin):
        """Releases the PWM pin back to free GPIO."""
        if Current == Chip.Epn128:
            return
        if Current == Chip.Epn256:
            EPN256.Pwm.UnInitialize(pin)
        else:
            EPM815.Pwm.UnInitialize(pin)

    @staticmethod
    def GetChipId(pin):
        """Kernel pwmchipN index to pass to the PWM channel constructor."""
        if Current == Chip.Epn128:
            raise NotSupportedException(
                "EPN128 (MA35D0 Chili Pro) does not bond out EPWM pins.")
        if Current == Chip.Epn256:
            return EPN256.Pwm.GetChipId(pin)
        return EPM815.Pwm.GetChipId(pin)

    @staticmethod
    def GetChannelId(pin):
        """Channel index within the PWM chip to pass to the PWM channel constructor."""
        if Current == Chip.Epn128:
            raise NotSupportedException(
                "EPN128 (MA35D0 Chili Pro) does not bond out EPWM pins.")
        if Current == Chip.Epn256:
            return EPN256.Pwm.GetChannelId(pin)
        return EPM815.Pwm.GetChannelId(pin)
