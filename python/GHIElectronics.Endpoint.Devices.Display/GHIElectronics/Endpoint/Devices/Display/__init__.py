"""Display on Endpoint. DisplayController is an abstract class: customers
subclass it when they wire up a non-standard panel (SPI, I2C, or anything
else) and drive the hardware themselves. For the one transport the library
ships out of the box - the SoC's parallel RGB panel into /dev/fb0 - we
provide ParallelDisplayController; GetDefault() returns one when
``/config/display.json`` names type = ``parallel``.

    from GHIElectronics.Endpoint.Devices.Display import DisplayController, PixelFormat

    display = DisplayController.GetDefault()
    if display is not None:
        display.Enable()
        display.ShowLogo()
        display.Flush(bitmap_bytes, PixelFormat.Format32bppArgb)
"""

import abc
import ctypes
import json
import os
import struct
import tempfile
from enum import IntEnum

from GHIElectronics.Endpoint.Core._native import native


_CONFIG_PATH = "/config/display.json"


class DisplayType(IntEnum):
    """Which built-in transport the display uses. ``None_`` means the board
    has no built-in display (and a custom subclass may still own a panel)."""

    None_ = 0
    Parallel = 1


class PixelFormat(IntEnum):
    """Pixel format of a buffer passed to :meth:`DisplayController.Flush`.
    Names and integer values mirror System.Drawing.Imaging.PixelFormat."""

    Format1bppIndexed  = 0x30101
    Format8bppIndexed  = 0x30803
    Format16bppRgb555  = 0x21005
    Format16bppRgb565  = 0x21006
    Format24bppRgb     = 0x21808
    Format32bppRgb     = 0x22009
    Format32bppArgb    = 0x26200A


class SignalPolarity(IntEnum):
    """Polarity of a logic-level control signal."""

    ActiveHigh = 0
    ActiveLow = 1


class ClockEdge(IntEnum):
    """Which edge of the pixel clock the panel latches data on."""

    Rising = 0
    Falling = 1


class BacklightSettings:
    """GPIO line driving the panel's backlight."""

    def __init__(self):
        self.GpioChip = None    # str like "gpiochip7" or None
        self.GpioPin = None     # int or None
        self.ActivePolarity = SignalPolarity.ActiveHigh


class ParallelLcdSettings:
    """Timings for the SoC's built-in parallel RGB panel controller."""

    def __init__(self):
        self.Width = 0
        self.Height = 0
        self.PixelClockHz = 0
        self.HFrontPorch = 0
        self.HBackPorch = 0
        self.HSync = 0
        self.VFrontPorch = 0
        self.VBackPorch = 0
        self.VSync = 0
        self.BitsPerPixel = 16
        self.DataEnablePolarity = SignalPolarity.ActiveHigh
        self.PixelClockEdge = ClockEdge.Rising


class DisplaySettings:
    """Everything in /config/display.json."""

    def __init__(self):
        self.Type = DisplayType.None_
        self.EnableAtBoot = False
        self.Backlight = None
        self.Logo = None
        self.Parallel = None


class DisplayController(abc.ABC):
    """A display on the board. Abstract: subclass for custom SPI/I2C panels,
    use :meth:`GetDefault` for the built-in parallel RGB transport."""

    @property
    @abc.abstractmethod
    def Width(self):
        """Visible width of the panel in pixels."""

    @property
    @abc.abstractmethod
    def Height(self):
        """Visible height of the panel in pixels."""

    @property
    @abc.abstractmethod
    def IsEnabled(self):
        """True once the panel driver / init path has run and the panel is ready to receive pixels."""

    @abc.abstractmethod
    def Enable(self):
        """Load the panel driver / run the init sequence. Throws on failure."""

    @abc.abstractmethod
    def Disable(self):
        """Reverse of Enable. Safe to call when nothing is enabled."""

    def Flush(self, pixels, format_=None, x=None, y=None, width=None, height=None):
        """Push a frame. ``pixels`` is bytes / bytearray / memoryview.
        ``format_`` defaults to :attr:`PixelFormat.Format16bppRgb565`. If
        x/y/width/height are omitted, the full panel is updated."""
        if format_ is None:
            format_ = PixelFormat.Format16bppRgb565
        if x is None: x = 0
        if y is None: y = 0
        if width is None: width = self.Width
        if height is None: height = self.Height
        self._Flush(bytes(pixels), format_, x, y, width, height)

    @abc.abstractmethod
    def _Flush(self, pixels, format_, x, y, width, height):
        """Subclasses implement the full-form Flush."""

    def ShowLogo(self):
        """Read the ``logo`` filename from /config/display.json, decode the
        24-bit BMP, and Flush it. No-op if the file is missing."""
        settings = DisplayController.GetSettings()
        if not settings.Logo:
            return
        path = os.path.join("/config", settings.Logo)
        result = _read_24bit_bmp(path)
        if result is None:
            return
        pixels, w, h = result
        self.Flush(pixels, PixelFormat.Format24bppRgb, 0, 0,
                   min(w, self.Width), min(h, self.Height))

    @abc.abstractmethod
    def Dispose(self):
        """Release the panel's resources (/dev/fb0 handle, etc.)."""

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.Dispose()

    @staticmethod
    def GetSettings():
        """Reads the built-in display's settings from /config/display.json."""
        config = _load_config()
        s = DisplaySettings()
        s.Type = _parse_type(config.get("type"))
        s.EnableAtBoot = bool(config.get("enableAtBoot", False))
        s.Logo = config.get("logo") or None
        s.Backlight = _read_backlight(config.get("backlight"))
        s.Parallel = _read_parallel(config.get("parallel"))
        return s

    @staticmethod
    def SetSettings(settings):
        """Writes the settings to /config/display.json. Does NOT apply them -
        call :meth:`Enable` for that."""
        if settings is None:
            raise ValueError("settings must not be None")
        config = _load_config()
        config["version"] = 1
        config["enableAtBoot"] = bool(settings.EnableAtBoot)
        config["type"] = _type_to_string(settings.Type)
        config["logo"] = settings.Logo
        config["backlight"] = _write_backlight(settings.Backlight)
        config["parallel"] = _write_parallel(settings.Parallel)
        # Drop keys from older firmware schemas so the file stays tidy.
        for stale in ("nativeLcd", "spi", "i2c", "touch"):
            config.pop(stale, None)
        _save_config(config)

    @staticmethod
    def GetDefault():
        """The built-in display controller for this board, or None when the
        type in /config/display.json is None."""
        settings = DisplayController.GetSettings()
        if settings.Type != DisplayType.Parallel:
            return None
        return ParallelDisplayController()


class ParallelDisplayController(DisplayController):
    """The built-in parallel RGB panel driven by the SoC's LCD controller.
    Opens /dev/fb0 inside :meth:`Enable`, respects the real framebuffer
    stride, converts source pixel formats to the panel's bit depth."""

    def __init__(self):
        self._fb = None
        self._stride = 0
        self._target_bpp = 16
        self._width = 0
        self._height = 0

    @property
    def Width(self):
        if self._width:
            return self._width
        p = DisplayController.GetSettings().Parallel
        return p.Width if p else 0

    @property
    def Height(self):
        if self._height:
            return self._height
        p = DisplayController.GetSettings().Parallel
        return p.Height if p else 0

    @property
    def IsEnabled(self):
        result = ctypes.c_int()
        native.display_is_enabled(native.ref(result))
        return result.value != 0

    def Enable(self):
        native.display_enable()
        p = DisplayController.GetSettings().Parallel or ParallelLcdSettings()
        self._width = p.Width
        self._height = p.Height
        self._target_bpp = p.BitsPerPixel or 16
        self._fb = open("/dev/fb0", "r+b", buffering=0)
        self._stride = self._width * (self._target_bpp // 8)
        try:
            with open("/sys/class/graphics/fb0/stride", "r") as sf:
                parsed = int(sf.read().strip())
                if parsed > 0:
                    self._stride = parsed
        except (OSError, ValueError):
            pass

    def Disable(self):
        if self._fb is not None:
            self._fb.close()
            self._fb = None
        native.display_disable()

    def _Flush(self, pixels, format_, x, y, width, height):
        if self._fb is None:
            raise RuntimeError("Flush called before Enable")
        if (x < 0 or y < 0 or width <= 0 or height <= 0
            or x + width > self._width or y + height > self._height):
            raise ValueError("Flush region outside the panel")

        source_bpp = {
            PixelFormat.Format16bppRgb565: 2,
            PixelFormat.Format24bppRgb: 3,
            PixelFormat.Format32bppRgb: 4,
            PixelFormat.Format32bppArgb: 4,
        }.get(format_)
        if source_bpp is None:
            raise NotImplementedError(f"Unsupported source format: {format_}")
        source_stride = width * source_bpp
        if len(pixels) < source_stride * height:
            raise ValueError("Pixel buffer is smaller than the region")

        target_bytes_per_pixel = self._target_bpp // 8
        row_bytes = width * target_bytes_per_pixel
        row = bytearray(row_bytes)
        for r in range(height):
            src = pixels[r * source_stride : r * source_stride + source_stride]
            _convert_row(src, row, format_, self._target_bpp, width)
            self._fb.seek((y + r) * self._stride + x * target_bytes_per_pixel)
            self._fb.write(row)

    def Dispose(self):
        try:
            self.Disable()
        except Exception:
            pass


# ---------------------------------------------------------------------------

def _load_config():
    if not os.path.exists(_CONFIG_PATH):
        return {"version": 1}
    try:
        with open(_CONFIG_PATH, "r") as f:
            data = json.load(f)
            return data if isinstance(data, dict) else {"version": 1}
    except (OSError, json.JSONDecodeError):
        return {"version": 1}


def _save_config(config):
    directory = os.path.dirname(_CONFIG_PATH) or "."
    fd, tmp = tempfile.mkstemp(prefix=".display", suffix=".json.tmp", dir=directory)
    try:
        with os.fdopen(fd, "w") as f:
            json.dump(config, f, indent=2)
        os.replace(tmp, _CONFIG_PATH)
    except Exception:
        try: os.unlink(tmp)
        except OSError: pass
        raise


def _parse_type(value):
    if value is None:
        return DisplayType.None_
    return DisplayType.Parallel if str(value).lower() == "parallel" else DisplayType.None_


def _type_to_string(t):
    return "parallel" if t == DisplayType.Parallel else "none"


def _read_backlight(o):
    if not isinstance(o, dict):
        return None
    chip = o.get("gpioChip")
    pin = o.get("gpioPin")
    if chip is None and pin is None:
        return None
    b = BacklightSettings()
    b.GpioChip = chip
    b.GpioPin = pin
    b.ActivePolarity = (SignalPolarity.ActiveLow
                        if str(o.get("activePolarity", "")).lower() == "activelow"
                        else SignalPolarity.ActiveHigh)
    return b


def _write_backlight(b):
    polarity = b.ActivePolarity if b else SignalPolarity.ActiveHigh
    return {
        "gpioChip": b.GpioChip if b else None,
        "gpioPin": b.GpioPin if b else None,
        "activePolarity": "activeLow" if polarity == SignalPolarity.ActiveLow else "activeHigh",
    }


def _read_parallel(o):
    if not isinstance(o, dict):
        return None
    p = ParallelLcdSettings()
    p.Width = int(o.get("width", 0) or 0)
    p.Height = int(o.get("height", 0) or 0)
    p.PixelClockHz = int(o.get("pixelClockHz", 0) or 0)
    p.HFrontPorch = int(o.get("hFrontPorch", 0) or 0)
    p.HBackPorch = int(o.get("hBackPorch", 0) or 0)
    p.HSync = int(o.get("hSync", 0) or 0)
    p.VFrontPorch = int(o.get("vFrontPorch", 0) or 0)
    p.VBackPorch = int(o.get("vBackPorch", 0) or 0)
    p.VSync = int(o.get("vSync", 0) or 0)
    p.BitsPerPixel = int(o.get("bitsPerPixel", 16) or 16)
    p.DataEnablePolarity = (SignalPolarity.ActiveLow
                            if bool(o.get("deActiveLow", False))
                            else SignalPolarity.ActiveHigh)
    p.PixelClockEdge = (ClockEdge.Falling
                        if bool(o.get("pixelClockFalling", False))
                        else ClockEdge.Rising)
    return p


def _write_parallel(p):
    if p is None:
        p = ParallelLcdSettings()
    return {
        "width": p.Width,
        "height": p.Height,
        "pixelClockHz": p.PixelClockHz,
        "hFrontPorch": p.HFrontPorch,
        "hBackPorch": p.HBackPorch,
        "hSync": p.HSync,
        "vFrontPorch": p.VFrontPorch,
        "vBackPorch": p.VBackPorch,
        "vSync": p.VSync,
        "bitsPerPixel": p.BitsPerPixel,
        "deActiveLow": p.DataEnablePolarity == SignalPolarity.ActiveLow,
        "pixelClockFalling": p.PixelClockEdge == ClockEdge.Falling,
    }


def _convert_row(src, dst, format_, target_bpp, pixels):
    if format_ == PixelFormat.Format16bppRgb565 and target_bpp == 16:
        dst[: pixels * 2] = src[: pixels * 2]
        return
    if target_bpp == 16:
        src_bpp = 3 if format_ == PixelFormat.Format24bppRgb else 4
        for p in range(pixels):
            b = src[p * src_bpp + 0]
            g = src[p * src_bpp + 1]
            r = src[p * src_bpp + 2]
            v = ((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3)
            dst[p * 2 + 0] = v & 0xFF
            dst[p * 2 + 1] = (v >> 8) & 0xFF
        return
    if target_bpp == 24:
        if format_ == PixelFormat.Format24bppRgb:
            dst[: pixels * 3] = src[: pixels * 3]
            return
        if format_ in (PixelFormat.Format32bppRgb, PixelFormat.Format32bppArgb):
            for p in range(pixels):
                dst[p * 3 + 0] = src[p * 4 + 0]
                dst[p * 3 + 1] = src[p * 4 + 1]
                dst[p * 3 + 2] = src[p * 4 + 2]
            return
    raise NotImplementedError(f"Cannot convert {format_} to {target_bpp}bpp")


def _read_24bit_bmp(path):
    """Minimal 24-bit uncompressed BMP reader. Returns (pixels, width, height)
    with pixels in top-down 24bppRgb (B,G,R) order, or None on failure."""
    try:
        with open(path, "rb") as f:
            header = f.read(54)
            if len(header) != 54 or header[0:2] != b"BM":
                return None
            data_offset = struct.unpack_from("<I", header, 10)[0]
            w = struct.unpack_from("<i", header, 18)[0]
            h = struct.unpack_from("<i", header, 22)[0]
            bpp = struct.unpack_from("<H", header, 28)[0]
            compression = struct.unpack_from("<I", header, 30)[0]
            if bpp != 24 or compression != 0 or w <= 0:
                return None
            flip_y = h > 0
            abs_h = h if flip_y else -h
            row_bytes = w * 3
            row_pad = (4 - (row_bytes & 3)) & 3

            f.seek(data_offset)
            pixels = bytearray(row_bytes * abs_h)
            for r in range(abs_h):
                dest_row = (abs_h - 1 - r) if flip_y else r
                data = f.read(row_bytes)
                if len(data) != row_bytes:
                    return None
                pixels[dest_row * row_bytes : dest_row * row_bytes + row_bytes] = data
                if row_pad:
                    f.read(row_pad)
            return pixels, w, abs_h
    except OSError:
        return None
