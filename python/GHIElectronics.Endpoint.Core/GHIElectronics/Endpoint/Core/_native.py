"""libendpoint, the board's hardware library that this package carries.
The other Endpoint packages call it through `native`; programs do not."""

import ctypes
import os

from ._errors import ObjectDisposedException, _exception

_library = ctypes.CDLL(os.path.join(os.path.dirname(os.path.abspath(__file__)), "libendpoint.so"))
_library.endpoint_last_error.restype = ctypes.c_char_p
_library.endpoint_version.restype = ctypes.c_char_p

_int, _double, _int64 = ctypes.c_int, ctypes.c_double, ctypes.c_int64
_handle, _text, _buffer = ctypes.c_void_p, ctypes.c_char_p, ctypes.c_void_p
_out_handle = ctypes.POINTER(ctypes.c_void_p)
_out_int = ctypes.POINTER(ctypes.c_int)
_out_double = ctypes.POINTER(ctypes.c_double)
_out_uint32 = ctypes.POINTER(ctypes.c_uint32)
_out_int64 = ctypes.POINTER(ctypes.c_int64)

# Called on libendpoint's own thread: (context, pin, change type)
GpioCallback = ctypes.CFUNCTYPE(None, ctypes.c_void_p, ctypes.c_int, ctypes.c_int)
# Called on libendpoint's storage thread: (context, drive handle, status, volume label)
StorageCallback = ctypes.CFUNCTYPE(None, ctypes.c_void_p, ctypes.c_ssize_t, ctypes.c_int, ctypes.c_char_p)
_hdc = ctypes.c_ssize_t
# Called on libendpoint's USB thread: (context, status, device id, type, vendor id, product id,
# device name, manufacturer, product, serial number)
UsbHostCallback = ctypes.CFUNCTYPE(None, ctypes.c_void_p, ctypes.c_int, ctypes.c_uint32, ctypes.c_int, ctypes.c_int,
                                   ctypes.c_int, ctypes.c_char_p, ctypes.c_char_p, ctypes.c_char_p, ctypes.c_char_p)

_FUNCTIONS = {
    "gpio_create": [_out_handle],
    "gpio_dispose": [_handle],
    "gpio_pin_count": [_handle, _out_int],
    "gpio_open_pin": [_handle, _int, _int, _int],
    "gpio_close_pin": [_handle, _int],
    "gpio_is_pin_open": [_handle, _int, _out_int],
    "gpio_set_pin_mode": [_handle, _int, _int, _int],
    "gpio_get_pin_mode": [_handle, _int, _out_int],
    "gpio_is_pin_mode_supported": [_handle, _int, _int, _out_int],
    "gpio_read": [_handle, _int, _out_int],
    "gpio_write": [_handle, _int, _int],
    "gpio_wait_for_event": [_handle, _int, _int, _int, _out_int, _out_int],
    "gpio_add_callback": [_handle, _int, _int, GpioCallback, ctypes.c_void_p, _out_int],
    "gpio_remove_callback": [_handle, _int],
    "i2c_initialize": [_int, _int],
    "i2c_uninitialize": [_int],
    "i2c_get_frequency": [_int, _out_int],
    "spi_initialize": [_int, _int],
    "spi_uninitialize": [_int],
    "pwm_initialize": [_int],
    "pwm_uninitialize": [_int],
    "pwm_get_chip_id": [_int, _out_int],
    "pwm_get_channel_id": [_int, _out_int],
    "serialport_initialize": [_text, _int],
    "serialport_uninitialize": [_text],
    "can_initialize": [_text, _int],
    "can_uninitialize": [_text],
    "i2cdevice_create": [_int, _int, _out_handle],
    "i2cdevice_dispose": [_handle],
    "i2cdevice_read": [_handle, _buffer, _int],
    "i2cdevice_write": [_handle, _buffer, _int],
    "i2cdevice_write_read": [_handle, _buffer, _int, _buffer, _int],
    "spidevice_create": [_int, _int, _int, _int, _int, _int, _int, _out_handle],
    "spidevice_dispose": [_handle],
    "spidevice_transfer": [_handle, _buffer, _buffer, _int],
    "pwmchannel_create": [_int, _int, _int, _double, _out_handle],
    "pwmchannel_dispose": [_handle],
    "pwmchannel_get_frequency": [_handle, _out_int],
    "pwmchannel_set_frequency": [_handle, _int],
    "pwmchannel_get_duty_cycle": [_handle, _out_double],
    "pwmchannel_set_duty_cycle": [_handle, _double],
    "pwmchannel_start": [_handle],
    "pwmchannel_stop": [_handle],
    "adc_pin_count": [_out_int],
    "adc_supports_analog_input": [_int, _out_int],
    "adc_convert_pin_to_channel": [_int, _out_int],
    "adc_voltage_reference": [_out_double],
    "adc_open_pin": [_int, _out_handle],
    "adc_close_pin": [_handle],
    "adc_resolution_bits": [_handle, _out_int],
    "adc_read_raw": [_handle, _out_uint32],
    "adc_read_millivolts": [_handle, _out_double],
    "dac_open_channel": [_int, _out_handle],
    "dac_close_channel": [_handle],
    "dac_write_value": [_handle, _int],
    "dac_last_written_value": [_handle, _out_int],
    "watchdog_create": [_out_handle],
    "watchdog_dispose": [_handle],
    "watchdog_max_timeout": [_out_int],
    "watchdog_is_enabled": [_handle, _out_int],
    "watchdog_enable": [_handle, _int],
    "watchdog_reset": [_handle],
    "watchdog_disable": [_handle],
    "rtc_read_time": [_out_int64],
    "rtc_set_time": [_int64],
    "rtc_is_valid": [_out_int],
    "rtc_set_charge_mode": [_int],
    "rtc_not_implemented": [],
    "storage_create": [_text, StorageCallback, ctypes.c_void_p, _out_handle],
    "storage_dispose": [_handle],
    "storage_enable": [_handle],
    "storage_disable": [_handle],
    "storage_mount": [_hdc, _buffer, _int],
    "storage_flush": [_hdc],
    "storage_unmount": [_hdc],
    "driveinfo_get": [_text, _buffer, _int, _out_int, _out_int, _out_int64, _out_int64, _out_int64],
    "usbhost_create": [UsbHostCallback, ctypes.c_void_p, _out_handle],
    "usbhost_dispose": [_handle],
    "usbhost_enable": [_handle],
    "usbhost_disable": [_handle],
}


class _Native:
    """Each libendpoint function, checked: an error becomes its exception"""

    def __init__(self):
        for name, argtypes in _FUNCTIONS.items():
            function = getattr(_library, "endpoint_" + name)
            function.argtypes = argtypes
            function.restype = ctypes.c_int
            setattr(self, name, self._checked(function))

    @staticmethod
    def _checked(function):
        def call(*args):
            result = function(*args)
            if result != 0:
                raise _exception(result, _library.endpoint_last_error().decode())
        return call

    @staticmethod
    def version():
        return _library.endpoint_version().decode()

    # Values returned through pointers
    @staticmethod
    def out_int():
        return ctypes.c_int()

    @staticmethod
    def out_double():
        return ctypes.c_double()

    @staticmethod
    def out_handle():
        return ctypes.c_void_p()

    @staticmethod
    def out_uint32():
        return ctypes.c_uint32()

    @staticmethod
    def out_int64():
        return ctypes.c_int64()

    @staticmethod
    def ref(value):
        return ctypes.byref(value)

    @staticmethod
    def readable(buffer):
        """A pointer to bytes, bytearray or memoryview, and its length"""
        data = bytes(buffer)
        return ctypes.c_char_p(data), len(data), data

    @staticmethod
    def writable(buffer):
        """A pointer to a bytearray (or writable memoryview) to fill, and its length"""
        if isinstance(buffer, (bytes, str)) or (isinstance(buffer, memoryview) and buffer.readonly):
            raise TypeError("A buffer to read into must be writable: use a bytearray")
        view = memoryview(buffer).cast("B")
        return (ctypes.c_char * len(view)).from_buffer(view), len(view)

    @staticmethod
    def disposed(name):
        return ObjectDisposedException(f"The {name} is disposed")


native = _Native()
