"""Chip-neutral PWM controller dispatcher. Mirrors .NET's
GHIElectronics.Endpoint.Core.PwmController: dispatches through
BoardSupport.Current, which the active Pins.* package registered at import.
"""

from GHIElectronics.Endpoint.Core._board_support import BoardSupport


class PwmController:
    """Chip-neutral entry point for PWM setup. Call once before using a
    PWM channel: it brings up the PWM channel and muxes its pin."""

    @staticmethod
    def Initialize(pin):
        """Brings up a PWM channel. The argument is a pin constant from the
        chip's namespace; only that pin is claimed."""
        BoardSupport.Current().PwmInitialize(pin)

    @staticmethod
    def UnInitialize(pin):
        """Releases the PWM pin back to free GPIO."""
        BoardSupport.Current().PwmUnInitialize(pin)

    @staticmethod
    def GetChipId(pin):
        """Kernel pwmchipN index to pass to the PWM channel constructor."""
        return BoardSupport.Current().PwmGetChipId(pin)

    @staticmethod
    def GetChannelId(pin):
        """Channel index within the PWM chip to pass to the PWM channel constructor."""
        return BoardSupport.Current().PwmGetChannelId(pin)
