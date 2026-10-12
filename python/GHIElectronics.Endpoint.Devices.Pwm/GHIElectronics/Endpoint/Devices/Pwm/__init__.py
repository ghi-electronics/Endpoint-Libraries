"""PWM for Endpoint, with the same API as .NET's System.Device.Pwm:

    from GHIElectronics.Endpoint.Core import EPM815
    from GHIElectronics.Endpoint.Devices.Pwm import PwmChannel

    EPM815.Pwm.Initialize(EPM815.Pwm.Pin.PA8)
    pwm = PwmChannel.Create(EPM815.Pwm.GetChipId(EPM815.Pwm.Pin.PA8),
                            EPM815.Pwm.GetChannelId(EPM815.Pwm.Pin.PA8), 1000, 0.5)
    pwm.Start()
"""

from GHIElectronics.Endpoint.Core._native import native

__all__ = ["PwmChannel"]


class PwmChannel:
    """A PWM output (call EPM815.Pwm.Initialize for the pin first)"""

    def __init__(self, chip, channel, frequency=400, dutyCyclePercentage=0.5):
        handle = native.out_handle()
        native.pwmchannel_create(chip, channel, frequency, float(dutyCyclePercentage), native.ref(handle))
        self._handle = handle

    @staticmethod
    def Create(chip, channel, frequency=400, dutyCyclePercentage=0.5):
        """dutyCyclePercentage is 0.0 to 1.0, as in .NET"""
        return PwmChannel(chip, channel, frequency, dutyCyclePercentage)

    def _h(self):
        if self._handle is None:
            raise native.disposed("PwmChannel")
        return self._handle

    @property
    def Frequency(self):
        """Hz"""
        value = native.out_int()
        native.pwmchannel_get_frequency(self._h(), native.ref(value))
        return value.value

    @Frequency.setter
    def Frequency(self, frequency):
        native.pwmchannel_set_frequency(self._h(), frequency)

    @property
    def DutyCycle(self):
        """0.0 to 1.0"""
        value = native.out_double()
        native.pwmchannel_get_duty_cycle(self._h(), native.ref(value))
        return value.value

    @DutyCycle.setter
    def DutyCycle(self, dutyCycle):
        native.pwmchannel_set_duty_cycle(self._h(), float(dutyCycle))

    def Start(self):
        native.pwmchannel_start(self._h())

    def Stop(self):
        native.pwmchannel_stop(self._h())

    def Dispose(self):
        """Stops the output and gives the channel back"""
        if self._handle is not None:
            native.pwmchannel_dispose(self._handle)
            self._handle = None

    def __enter__(self):
        return self

    def __exit__(self, *exception):
        self.Dispose()

    def __del__(self):
        try:
            self.Dispose()
        except Exception:
            pass
