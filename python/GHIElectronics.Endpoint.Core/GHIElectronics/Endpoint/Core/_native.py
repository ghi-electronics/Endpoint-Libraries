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

# Called on the input device's own thread: (context, event, a, b, c, d, e)
InputCallback = ctypes.CFUNCTYPE(None, ctypes.c_void_p, ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.c_int,
                                 ctypes.c_int, ctypes.c_int)

# Called on the touchscreen's own thread: (context, event 0/1/2, point count, ids*, xs*, ys*)
_int_p = ctypes.POINTER(ctypes.c_int)
TouchCallback = ctypes.CFUNCTYPE(None, ctypes.c_void_p, ctypes.c_int, ctypes.c_int,
                                 _int_p, _int_p, _int_p)

# Called once, on the device's own thread, when it is unplugged: (context)
UnpluggedCallback = ctypes.CFUNCTYPE(None, ctypes.c_void_p)

# A camera's frame while streaming, on its own thread: (context, jpeg, length, width, height)
CameraFrameCallback = ctypes.CFUNCTYPE(None, ctypes.c_void_p, ctypes.POINTER(ctypes.c_uint8), ctypes.c_int, ctypes.c_int,
                                       ctypes.c_int)

# UI widget event (Clicked / ValueChanged), on the UI thread: (context)
UiEventCallback = ctypes.CFUNCTYPE(None, ctypes.c_void_p)
_uint32 = ctypes.c_uint32
_bytes_ptr = ctypes.POINTER(ctypes.c_uint8)

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
    "input_open": [_text, _int, InputCallback, ctypes.c_void_p, _out_handle],
    "input_close": [_handle],
    "input_info": [_handle, _out_int, _out_int, _out_int, _out_int],
    "input_key_state": [_handle, _int, _out_int],
    "input_mouse_state": [_handle, _out_int, _out_int, _out_int, _out_int],
    "input_joystick_state": [_handle, _int, _out_int, _out_int, _out_int, _out_uint32],
    "input_set_cursor_bounds": [_handle, _int, _int, _int, _int, _int],
    "input_set_cursor_position": [_handle, _int, _int],
    "input_set_scale": [_handle, _double],
    "printer_open": [_text, UnpluggedCallback, ctypes.c_void_p, _out_handle],
    "printer_close": [_handle],
    "printer_info": [_handle, _out_int, _out_int, _out_int],
    "printer_device_id": [_handle, _buffer, _int],
    "printer_status": [_handle, _out_int],
    "printer_write": [_handle, _buffer, _int, _int, _out_int],
    "printer_read": [_handle, _buffer, _int, _int, _out_int],
    "sound_create": [_text, _text, _text, _int, _int, _int, _int, _out_handle],
    "sound_dispose": [_handle],
    "sound_play_file": [_handle, _text, _int],
    "sound_play_buffer": [_handle, _buffer, _int, _int],
    "sound_record_file": [_handle, _int, _text],
    "sound_record_buffer": [_handle, _int, _buffer, _int, _out_int],
    "sound_start_recording": [_handle, _text],
    "sound_stop_recording": [_handle],
    "sound_get_volume": [_handle, _int, _out_int64],
    "sound_set_volume": [_handle, _int, _int64],
    "sound_get_mute": [_handle, _int, _out_int],
    "sound_set_mute": [_handle, _int, _int],
    "camera_open": [_text, CameraFrameCallback, UnpluggedCallback, ctypes.c_void_p, _out_handle],
    "camera_close": [_handle],
    "camera_info": [_handle, _buffer, _int, _out_int, _out_int],
    "camera_format": [_handle, _int, _out_int, _out_int, _out_int, _out_int],
    "camera_get_resolution": [_handle, _out_int, _out_int, _out_int, _out_int],
    "camera_set_resolution": [_handle, _int, _int, _int, _int],
    "camera_get_quality": [_handle, _out_int],
    "camera_set_quality": [_handle, _int],
    "camera_capture": [_handle, _int, ctypes.POINTER(ctypes.POINTER(ctypes.c_uint8)), _out_int],
    "camera_free": [ctypes.POINTER(ctypes.c_uint8)],
    "camera_start_streaming": [_handle],
    "camera_stop_streaming": [_handle],
    "camera_get_control": [_handle, _int, _out_int, _out_int, _out_int, _out_int, _out_int, _out_int],
    "camera_set_control": [_handle, _int, _int],
    "network_get_interface_name": [_int, _buffer, _int],
    "network_enable": [_int],
    "network_disable": [_int],
    "network_is_enabled": [_int, _out_int],
    "network_is_connected": [_int, _out_int],
    "display_enable": [],
    "display_disable": [],
    "display_is_enabled": [_out_int],
    "touch_find_default": [_buffer, _int],
    "touch_open": [_text, TouchCallback, ctypes.c_void_p, _out_handle],
    "touch_close": [_handle],
    "touch_info": [_handle, _out_int, _out_int, _out_int, _out_int, _out_int],

    # UI (LVGL under the hood). Lifecycle first, then shared widget ops, then
    # per-type functions. Events pass (callback, context) — the Python side
    # keeps a reference to the callback so ctypes doesn't collect it.
    "ui_initialize": [],
    "ui_run": [],
    "ui_stop": [],
    "ui_touch_attach": [],
    "ui_touch_feed": [_int, _int, _int],
    "ui_widget_destroy": [_handle],
    "ui_widget_set_pos": [_handle, _int, _int],
    "ui_widget_set_x": [_handle, _int],
    "ui_widget_set_y": [_handle, _int],
    "ui_widget_get_pos": [_handle, _out_int, _out_int],
    "ui_widget_set_size": [_handle, _int, _int],
    "ui_widget_set_width": [_handle, _int],
    "ui_widget_set_height": [_handle, _int],
    "ui_widget_get_size": [_handle, _out_int, _out_int],
    "ui_widget_set_visible": [_handle, _int],
    "ui_widget_get_visible": [_handle, _out_int],
    "ui_subscribe_clicked": [_handle, UiEventCallback, ctypes.c_void_p],
    "ui_subscribe_value_changed": [_handle, UiEventCallback, ctypes.c_void_p],
    "ui_screen_create": [_out_handle],
    "ui_screen_load": [_handle],
    "ui_screen_active": [_out_handle],
    "ui_container_create": [_handle, _out_handle],
    "ui_button_create": [_handle, _out_handle],
    "ui_button_set_text": [_handle, _text],
    "ui_button_set_bg_color": [_handle, _uint32],
    "ui_button_set_text_color": [_handle, _uint32],
    "ui_button_set_font_size": [_handle, _int],
    "ui_label_create": [_handle, _out_handle],
    "ui_label_set_text": [_handle, _text],
    "ui_label_set_color": [_handle, _uint32],
    "ui_label_set_font_size": [_handle, _int],
    "ui_image_create": [_handle, _out_handle],
    "ui_image_set_source": [_handle, _bytes_ptr, _int, _int, _int],
    "ui_textbox_create": [_handle, _out_handle],
    "ui_textbox_set_text": [_handle, _text],
    "ui_textbox_get_text": [_handle, _buffer, _int],
    "ui_textbox_set_placeholder": [_handle, _text],
    "ui_slider_create": [_handle, _out_handle],
    "ui_slider_set_range": [_handle, _int, _int],
    "ui_slider_set_value": [_handle, _int],
    "ui_slider_get_value": [_handle, _out_int],
    "ui_checkbox_create": [_handle, _out_handle],
    "ui_checkbox_set_text": [_handle, _text],
    "ui_checkbox_set_checked": [_handle, _int],
    "ui_checkbox_get_checked": [_handle, _out_int],
    "ui_keyboard_create": [_handle, _out_handle],
    "ui_keyboard_set_textbox": [_handle, _handle],
    "ui_switch_create": [_handle, _out_handle],
    "ui_switch_set_checked": [_handle, _int],
    "ui_switch_get_checked": [_handle, _out_int],
    "ui_progressbar_create": [_handle, _out_handle],
    "ui_progressbar_set_range": [_handle, _int, _int],
    "ui_progressbar_set_value": [_handle, _int],
    "ui_progressbar_get_value": [_handle, _out_int],
    "ui_dropdown_create": [_handle, _out_handle],
    "ui_dropdown_set_options": [_handle, _text],
    "ui_dropdown_set_selected": [_handle, _int],
    "ui_dropdown_get_selected": [_handle, _out_int],
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
                raise _exception(result, _library.endpoint_last_error().decode(errors="replace"))
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

    # Objects with events stay alive until they are disposed, as in .NET: a
    # program that does not keep one in a variable still gets its events
    _alive = set()

    @staticmethod
    def keep_alive(target):
        _Native._alive.add(target)

    @staticmethod
    def let_go(target):
        _Native._alive.discard(target)


native = _Native()
