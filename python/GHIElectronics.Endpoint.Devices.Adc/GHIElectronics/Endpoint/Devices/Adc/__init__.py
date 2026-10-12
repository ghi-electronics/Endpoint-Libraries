"""The analog inputs of Endpoint, with the same API as .NET's
System.Device.Analog and Endpoint's AdcController:

    from GHIElectronics.Endpoint.Core import EPM815
    from GHIElectronics.Endpoint.Devices.Adc import AdcController

    adc = AdcController()
    pin = adc.OpenPin(EPM815.Adc.Pin.PA0)
    print(pin.ReadVoltage().Volts, pin.ReadRaw())

Opening a pin makes it an analog input; closing it puts the pin back as it was.
"""

from GHIElectronics.Endpoint.Core._errors import NotSupportedException
from GHIElectronics.Endpoint.Core._native import native

__all__ = ["AdcController", "AnalogInputPin", "ElectricPotential"]


class ElectricPotential:
    """A voltage, as .NET's UnitsNet.ElectricPotential (Volts, Millivolts)"""

    def __init__(self, volts):
        self._volts = float(volts)

    @staticmethod
    def FromVolts(volts):
        return ElectricPotential(volts)

    @staticmethod
    def FromMillivolts(millivolts):
        return ElectricPotential(millivolts / 1000)

    @property
    def Volts(self):
        return self._volts

    @property
    def Millivolts(self):
        return self._volts * 1000

    def __eq__(self, other):
        return isinstance(other, ElectricPotential) and other._volts == self._volts

    def __lt__(self, other):
        return self._volts < other._volts

    def __repr__(self):
        return f"{self._volts:.3f} V"


class AdcController:
    """All of Endpoint's analog inputs (both converters)"""

    def __init__(self):
        millivolts = native.out_double()
        native.adc_voltage_reference(native.ref(millivolts))
        self._reference = ElectricPotential.FromMillivolts(millivolts.value)
        self._open = {}

    @property
    def PinCount(self):
        count = native.out_int()
        native.adc_pin_count(native.ref(count))
        return count.value

    @property
    def VoltageReference(self):
        """The inputs measure 0 V to this (3.3 V)"""
        return self._reference

    def SupportsAnalogInput(self, pin):
        supported = native.out_int()
        native.adc_supports_analog_input(pin, native.ref(supported))
        return bool(supported.value)

    def ConvertPinNumberToAnalogChannelNumber(self, pinNumber):
        """The pin's channel in its converter (ADC1 or ADC2)"""
        channel = native.out_int()
        native.adc_convert_pin_to_channel(pinNumber, native.ref(channel))
        return channel.value

    def OpenPin(self, pinNumber):
        pin = AnalogInputPin(self, pinNumber)
        self._open[pinNumber] = pin
        return pin

    def ClosePin(self, pin):
        """pin is an AnalogInputPin (as .NET) or its pin number"""
        pin = self._open.get(pin, pin) if isinstance(pin, int) else pin
        if isinstance(pin, AnalogInputPin):
            pin.Dispose()

    def IsPinOpen(self, pinNumber):
        return pinNumber in self._open

    def Dispose(self):
        for pin in list(self._open.values()):
            pin.Dispose()

    def __enter__(self):
        return self

    def __exit__(self, *exception):
        self.Dispose()


class AnalogInputPin:
    """An open analog input: the pin is in analog mode until it is disposed"""

    def __init__(self, controller, pinNumber):
        handle = native.out_handle()
        native.adc_open_pin(pinNumber, native.ref(handle))
        self._handle = handle
        self._controller = controller
        self.PinNumber = pinNumber

    def _h(self):
        if self._handle is None:
            raise native.disposed("AnalogInputPin")
        return self._handle

    @property
    def AdcResolutionBits(self):
        bits = native.out_int()
        native.adc_resolution_bits(self._h(), native.ref(bits))
        return bits.value

    @property
    def VoltageReference(self):
        return self._controller.VoltageReference

    @property
    def MinVoltage(self):
        return ElectricPotential(0)

    @property
    def MaxVoltage(self):
        return self._controller.VoltageReference

    def ReadRaw(self):
        """A new sample, 0 to 2**AdcResolutionBits - 1"""
        value = native.out_uint32()
        native.adc_read_raw(self._h(), native.ref(value))
        return value.value

    def ReadVoltage(self):
        """A new sample as an ElectricPotential (the driver's exact scale)"""
        millivolts = native.out_double()
        native.adc_read_millivolts(self._h(), native.ref(millivolts))
        return ElectricPotential.FromMillivolts(millivolts.value)

    def EnableAnalogValueChangedEvent(self, masterController=None, masterPin=None):
        raise NotSupportedException("Endpoint's analog inputs have no value-changed event: call ReadRaw or ReadVoltage")

    def DisableAnalogValueChangedEvent(self):
        pass

    def Dispose(self):
        """Puts the pin back as it was before it was opened"""
        if self._handle is not None:
            native.adc_close_pin(self._handle)
            self._handle = None
            self._controller._open.pop(self.PinNumber, None)

    def __enter__(self):
        return self

    def __exit__(self, *exception):
        self.Dispose()

    def __del__(self):
        try:
            self.Dispose()
        except Exception:
            pass
