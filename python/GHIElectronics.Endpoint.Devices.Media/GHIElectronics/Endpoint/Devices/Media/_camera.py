"""Webcams: any USB Video Class camera. Pictures come as JPEG files, whether
the camera sends MJPEG or raw YUYV pictures."""

import ctypes
import os
from enum import IntEnum

from GHIElectronics.Endpoint.Core._event import Event
from GHIElectronics.Endpoint.Core._native import CameraFrameCallback, UnpluggedCallback, native


class CameraPixelFormat(IntEnum):
    """What a camera sends (pictures always come out as JPEG)"""

    Mjpeg = 0
    """Motion JPEG: small and fast, the way most webcams send large pictures"""
    Yuyv = 1
    """Raw YUYV pictures: every webcam has them; the library makes the JPEG"""


class CameraFormat:
    """A picture size a camera has, what it sends for it, and its highest frame rate"""

    def __init__(self, width, height, pixelFormat, maxFrameRate):
        self.Width = width
        self.Height = height
        self.PixelFormat = pixelFormat
        self.MaxFrameRate = maxFrameRate

    def __repr__(self):
        return f"{self.Width}x{self.Height} {self.PixelFormat.name} {self.MaxFrameRate} fps"


class CameraControl(IntEnum):
    """A camera's picture controls (each camera has some of them)"""

    Brightness = 0
    Contrast = 1
    Saturation = 2
    Hue = 3
    Gamma = 4
    Gain = 5
    Sharpness = 6
    BacklightCompensation = 7
    AutoWhiteBalance = 8
    """1: on, 0: off (then WhiteBalanceTemperature counts)"""
    WhiteBalanceTemperature = 9
    AutoExposure = 10
    """1: on, 0: off (then Exposure counts)"""
    Exposure = 11
    PowerLineFrequency = 12
    """0: off, 1: 50 Hz, 2: 60 Hz (against flicker from lights)"""
    AutoFocus = 13
    """1: on, 0: off (then Focus counts)"""
    Focus = 14
    Zoom = 15
    Pan = 16
    Tilt = 17


class CameraControlValue:
    """A camera control's range and value, in the camera's own units"""

    def __init__(self, control, minimum, maximum, step, defaultValue, value):
        self.Control = control
        self.Minimum = minimum
        self.Maximum = maximum
        self.Step = step
        self.DefaultValue = defaultValue
        self.Value = value

    def __repr__(self):
        return f"{self.Control.name} {self.Value} ({self.Minimum} to {self.Maximum}, default {self.DefaultValue})"


class FrameReceivedEventArgs:
    """A camera's frame while streaming: a JPEG (bytes)"""

    def __init__(self, jpeg, width, height):
        self.Jpeg = jpeg
        self.Width = width
        self.Height = height


class _EventProperty:
    def __init__(self, attribute):
        self.attribute = attribute

    def __get__(self, target, owner=None):
        return self if target is None else getattr(target, self.attribute)

    def __set__(self, target, value):
        if value is not getattr(target, self.attribute, None):
            raise TypeError("Use += and -= to add and remove an event handler")


class Camera:
    """A webcam: any USB Video Class camera, opened with the DeviceName of a Webcam
    from UsbHostController ("/dev/video0"; the number can change when it is
    plugged in again). The first picture waits until the camera's automatic
    exposure has settled (about a second); while pictures are taken the camera
    keeps running, so the next ones come at once. At first the size is 640x480.

        with Camera(e.DeviceName) as camera:
            camera.SetResolution(1280, 720)
            camera.Capture("/media/usb0/photo.jpg")

            camera.FrameReceived += lambda sender, frame: send_to_browser(frame.Jpeg)
            camera.StartStreaming()

    The events come on the camera's own thread. A camera stays alive until it is disposed.
    """

    FrameReceived = _EventProperty("_frame_event")
    """A frame while streaming: handler(sender, e) with e.Jpeg (bytes), e.Width, e.Height"""
    Disconnected = _EventProperty("_disconnected_event")
    """The camera was unplugged: handler(sender, e)"""

    def __init__(self, deviceName):
        self._frame_event = Event()
        self._disconnected_event = Event()
        self._handle = None
        self._streaming = False
        self.DeviceName = str(deviceName)
        # The callbacks are kept: the library calls them
        self._frame_callback = CameraFrameCallback(self._frame)
        self._unplugged_callback = UnpluggedCallback(lambda context: self._disconnected_event(self, None))
        handle = native.out_handle()
        native.camera_open(self.DeviceName.encode(), self._frame_callback, self._unplugged_callback, None,
                           native.ref(handle))
        self._handle = handle
        native.keep_alive(self)
        name, connected, count = ctypes.create_string_buffer(256), native.out_int(), native.out_int()
        native.camera_info(handle, name, len(name), native.ref(connected), native.ref(count))
        self.Name = name.value.decode(errors="replace")
        """The camera's name ("USB 2.0 Camera: USB Camera")"""
        formats = []
        for index in range(count.value):
            width, height, pixel, rate = (native.out_int() for _ in range(4))
            native.camera_format(handle, index, native.ref(width), native.ref(height), native.ref(pixel), native.ref(rate))
            formats.append(CameraFormat(width.value, height.value, CameraPixelFormat(pixel.value), rate.value))
        self.SupportedFormats = tuple(formats)
        """The sizes the camera has, with what it sends for each"""

    def _h(self):
        if self._handle is None:
            raise native.disposed("Camera")
        return self._handle

    def _frame(self, context, jpeg, length, width, height):
        if len(self._frame_event):
            self._frame_event(self, FrameReceivedEventArgs(ctypes.string_at(jpeg, length), width, height))

    @property
    def Connected(self):
        if self._handle is None:
            return False
        connected, count = native.out_int(), native.out_int()
        native.camera_info(self._handle, None, 0, native.ref(connected), native.ref(count))
        return bool(connected.value)

    def _resolution(self):
        values = [native.out_int() for _ in range(4)]
        native.camera_get_resolution(self._h(), *[native.ref(v) for v in values])
        return [v.value for v in values]

    @property
    def Width(self):
        return self._resolution()[0]

    @property
    def Height(self):
        return self._resolution()[1]

    @property
    def PixelFormat(self):
        return CameraPixelFormat(self._resolution()[2])

    @property
    def FrameRate(self):
        return self._resolution()[3]

    @property
    def JpegQuality(self):
        """The JPEG quality (1 to 100) of pictures the library makes from raw YUYV pictures (85 at first)"""
        quality = native.out_int()
        native.camera_get_quality(self._h(), native.ref(quality))
        return quality.value

    @JpegQuality.setter
    def JpegQuality(self, value):
        native.camera_set_quality(self._h(), int(value))

    @property
    def IsStreaming(self):
        return self._streaming

    def SetResolution(self, width, height=None, frameRate=0):
        """A size the camera has: SetResolution(1280, 720) (MJPEG if the camera has it at that size,
        else YUYV), or one of SupportedFormats as it is: SetResolution(format). frameRate 0: the
        camera's highest for that size."""
        if isinstance(width, CameraFormat):
            if height is not None:
                frameRate = height
            native.camera_set_resolution(self._h(), width.Width, width.Height, int(frameRate), int(width.PixelFormat))
        else:
            native.camera_set_resolution(self._h(), int(width), int(height), int(frameRate), -1)

    def Capture(self, path=None, timeout=5000):
        """Takes a picture: a JPEG (bytes), or saved in a file when a path is given. A
        TimeoutException if the camera sends none in timeout milliseconds."""
        data, length = ctypes.POINTER(ctypes.c_uint8)(), native.out_int()
        native.camera_capture(self._h(), int(timeout), ctypes.byref(data), native.ref(length))
        try:
            jpeg = ctypes.string_at(data, length.value)
        finally:
            native.camera_free(data)
        if path is None:
            return jpeg
        with open(os.fspath(path), "wb") as file:
            file.write(jpeg)
        return None

    def StartStreaming(self):
        """Every frame comes as a JPEG in FrameReceived"""
        native.camera_start_streaming(self._h())
        self._streaming = True

    def StopStreaming(self):
        native.camera_stop_streaming(self._h())
        self._streaming = False

    def _get(self, control):
        values = [native.out_int() for _ in range(6)]
        native.camera_get_control(self._h(), int(control), *[native.ref(v) for v in values])
        supported, minimum, maximum, step, default, value = (v.value for v in values)
        return CameraControlValue(CameraControl(control), minimum, maximum, step, default, value) if supported else None

    @property
    def SupportedControls(self):
        """The controls this camera has"""
        return [control for control in CameraControl if self._get(control) is not None]

    def GetControl(self, control):
        """A control's range and value; a NotSupportedException if the camera does not have it"""
        value = self._get(control)
        if value is None:
            from GHIElectronics.Endpoint.Core import NotSupportedException
            raise NotSupportedException(f"The camera has no {CameraControl(control).name} control")
        return value

    def SetControl(self, control, value):
        """Sets a control, in the camera's own units (GetControl gives its range)"""
        native.camera_set_control(self._h(), int(control), int(value))

    def Dispose(self):
        """Stops the camera (a frame being delivered finishes first) and closes it"""
        if self._handle is not None:
            native.camera_close(self._handle)
            self._handle = None
            self._streaming = False
            native.let_go(self)

    def __enter__(self):
        return self

    def __exit__(self, *exception):
        self.Dispose()

    def __repr__(self):
        return f"Camera({self.DeviceName!r})"
