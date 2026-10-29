"""The EPN256 module's pins (Nuvoton MA35D1, dual Cortex-A35, 256 MB DDR3L),
the same as .NET's GHIElectronics.Endpoint.Core.EPN256. Mirrors _epm815 so
sample code reads the same shape regardless of the chip."""

import os
import subprocess
import threading
import time


def _name(pin):
    if 0 <= pin <= 223:
        port = pin // 16
        return f"P{chr(ord('A') + port)}{pin % 16}"
    return f"#{pin}"


def _run(path, *args):
    """Run a program and raise if it exits non-zero."""
    result = subprocess.run([path, *args], capture_output=True, text=True)
    if result.returncode != 0:
        raise OSError(f"{path} {' '.join(args)} failed: {result.stderr.strip()}")


def _try_run(path, *args):
    """Run a program and ignore the exit code (for best-effort cleanup)."""
    try:
        subprocess.run([path, *args], capture_output=True, text=True)
    except OSError:
        pass


def _wait_path(path, is_dir=False, timeout_s=1.0, poll_s=0.02):
    """Spin until a /dev or /sys path appears, or raise after timeout."""
    deadline = time.time() + timeout_s
    check = os.path.isdir if is_dir else os.path.exists
    while not check(path) and time.time() < deadline:
        time.sleep(poll_s)
    return check(path)


class EPN256:
    """The EPN256 (Nuvoton MA35D1) module"""

    class Gpio:
        """GPIO pins, for GHIElectronics.Endpoint.Devices.Gpio's GpioController"""

        class Pin:
            """The EPN256's pins: port * 16 + line, ports A to N"""
            PA0 = 0
            PA1 = 1
            PA2 = 2
            PA3 = 3
            PA4 = 4
            PA5 = 5
            PA6 = 6
            PA7 = 7
            PA8 = 8
            PA9 = 9
            PA10 = 10
            PA11 = 11
            PA12 = 12
            PA13 = 13
            PA14 = 14
            PA15 = 15
            PB0 = 16
            PB1 = 17
            PB2 = 18
            PB3 = 19
            PB4 = 20
            PB5 = 21
            PB6 = 22
            PB7 = 23
            PB8 = 24
            PB9 = 25
            PB10 = 26
            PB11 = 27
            PB12 = 28
            PB13 = 29
            PB14 = 30
            PB15 = 31
            PC0 = 32
            PC1 = 33
            PC2 = 34
            PC3 = 35
            PC4 = 36
            PC5 = 37
            PC6 = 38
            PC7 = 39
            PC8 = 40
            PC9 = 41
            PC10 = 42
            PC11 = 43
            PC12 = 44
            PC13 = 45
            PC14 = 46
            PC15 = 47
            PD0 = 48
            PD1 = 49
            PD2 = 50
            PD3 = 51
            PD4 = 52
            PD5 = 53
            PD6 = 54
            PD7 = 55
            PD8 = 56
            PD9 = 57
            PD10 = 58
            PD11 = 59
            PD12 = 60
            PD13 = 61
            PD14 = 62
            PD15 = 63
            PE0 = 64
            PE1 = 65
            PE2 = 66
            PE3 = 67
            PE4 = 68
            PE5 = 69
            PE6 = 70
            PE7 = 71
            PE8 = 72
            PE9 = 73
            PE10 = 74
            PE11 = 75
            PE12 = 76
            PE13 = 77
            PE14 = 78
            PE15 = 79
            PF0 = 80
            PF1 = 81
            PF2 = 82
            PF3 = 83
            PF4 = 84
            PF5 = 85
            PF6 = 86
            PF7 = 87
            PF8 = 88
            PF9 = 89
            PF10 = 90
            PF11 = 91
            PF12 = 92
            PF13 = 93
            PF14 = 94
            PF15 = 95
            PG0 = 96
            PG1 = 97
            PG2 = 98
            PG3 = 99
            PG4 = 100
            PG5 = 101
            PG6 = 102
            PG7 = 103
            PG8 = 104
            PG9 = 105
            PG10 = 106
            PG11 = 107
            PG12 = 108
            PG13 = 109
            PG14 = 110
            PG15 = 111
            PH0 = 112
            PH1 = 113
            PH2 = 114
            PH3 = 115
            PH4 = 116
            PH5 = 117
            PH6 = 118
            PH7 = 119
            PH8 = 120
            PH9 = 121
            PH10 = 122
            PH11 = 123
            PH12 = 124
            PH13 = 125
            PH14 = 126
            PH15 = 127
            PI0 = 128
            PI1 = 129
            PI2 = 130
            PI3 = 131
            PI4 = 132
            PI5 = 133
            PI6 = 134
            PI7 = 135
            PI8 = 136
            PI9 = 137
            PI10 = 138
            PI11 = 139
            PI12 = 140
            PI13 = 141
            PI14 = 142
            PI15 = 143
            PJ0 = 144
            PJ1 = 145
            PJ2 = 146
            PJ3 = 147
            PJ4 = 148
            PJ5 = 149
            PJ6 = 150
            PJ7 = 151
            PJ8 = 152
            PJ9 = 153
            PJ10 = 154
            PJ11 = 155
            PJ12 = 156
            PJ13 = 157
            PJ14 = 158
            PJ15 = 159
            PK0 = 160
            PK1 = 161
            PK2 = 162
            PK3 = 163
            PK4 = 164
            PK5 = 165
            PK6 = 166
            PK7 = 167
            PK8 = 168
            PK9 = 169
            PK10 = 170
            PK11 = 171
            PK12 = 172
            PK13 = 173
            PK14 = 174
            PK15 = 175
            PL0 = 176
            PL1 = 177
            PL2 = 178
            PL3 = 179
            PL4 = 180
            PL5 = 181
            PL6 = 182
            PL7 = 183
            PL8 = 184
            PL9 = 185
            PL10 = 186
            PL11 = 187
            PL12 = 188
            PL13 = 189
            PL14 = 190
            PL15 = 191
            PM0 = 192
            PM1 = 193
            PM2 = 194
            PM3 = 195
            PM4 = 196
            PM5 = 197
            PM6 = 198
            PM7 = 199
            PM8 = 200
            PM9 = 201
            PM10 = 202
            PM11 = 203
            PM12 = 204
            PM13 = 205
            PM14 = 206
            PM15 = 207
            PN0 = 208
            PN1 = 209
            PN2 = 210
            PN3 = 211
            PN4 = 212
            PN5 = 213
            PN6 = 214
            PN7 = 215
            PN8 = 216
            PN9 = 217
            PN10 = 218
            PN11 = 219
            PN12 = 220
            PN13 = 221
            PN14 = 222
            PN15 = 223
            NONE = -1

    # Pay-for-use peripheral initializers. Same pattern + same side-effects as
    # the .NET GHIElectronics.Endpoint.Core.EPN256.* classes: Initialize loads
    # the kernel driver (or binds a platform device via sysfs), the kernel's
    # pinctrl framework claims the pins at probe time, and UnInitialize
    # reverses it so unused pins return to free GPIO.

    class Spi:
        """MA35D1 SPI controllers. Initialize modprobes spi-ma35d1-spi + spidev,
        which triggers probe + pinctrl mux so /dev/spidev<N>.0 appears for
        userspace. Mirrors .NET's EPN256.Spi.

            EPN256.Spi.Initialize(EPN256.Spi.Spi0)
        """
        Spi0 = 0  # PB8 (SS1), PB9 (CLK), PB10 (MOSI), PB11 (MISO)
        Spi1 = 1  # PC8 (SS0), PC9 (CLK), PC10 (MOSI), PC11 (MISO)
        Spi2 = 2  # PG8 (SS0), PG9 (CLK), PG10 (MOSI), PK4 (MISO)
        Spi3 = 3  # PL8 (SS0), PL9 (CLK), PL4 (MOSI), PL5 (MISO)

        _controller_module = "spi-ma35d1-spi"
        _spidev_module = "spidev"
        _gate = threading.Lock()
        _open_count = {}
        _module_loaded = False

        @staticmethod
        def _validate(bus):
            if not (0 <= bus <= 3):
                raise ValueError(f"Must be EPN256.Spi.Spi0 / Spi1 / Spi2 / Spi3, got {bus}")

        @classmethod
        def Initialize(cls, bus):
            cls._validate(bus)
            with cls._gate:
                if not cls._module_loaded:
                    _run("/sbin/modprobe", cls._controller_module)
                    _run("/sbin/modprobe", cls._spidev_module)
                    cls._module_loaded = True
                cls._open_count[bus] = cls._open_count.get(bus, 0) + 1
            dev = f"/dev/spidev{bus}.0"
            if not _wait_path(dev, timeout_s=1.0):
                raise OSError(
                    f"spi-ma35d1 loaded but {dev} did not appear within 1 s. "
                    "Have you added a pinctrl_spi group to the device tree for your board?")

        @classmethod
        def UnInitialize(cls, bus):
            cls._validate(bus)
            with cls._gate:
                n = cls._open_count.get(bus, 0)
                if n <= 0:
                    return
                if n == 1:
                    cls._open_count.pop(bus, None)
                else:
                    cls._open_count[bus] = n - 1
                if not cls._open_count and cls._module_loaded:
                    _try_run("/sbin/rmmod", cls._spidev_module)
                    _try_run("/sbin/rmmod", cls._controller_module)
                    cls._module_loaded = False

    class I2c:
        """MA35D1 I2C buses. Initialize modprobes i2c-ma35d1; pinctrl claims the
        bus's pins at probe. Mirrors .NET's EPN256.I2c."""
        I2c1 = 1
        I2c2 = 2
        I2c3 = 3
        I2c4 = 4
        I2c5 = 5  # Default on EPN256 reference touch panel

        _module_name = "i2c-ma35d1"
        _gate = threading.Lock()
        _open_count = {}
        _module_loaded = False

        @staticmethod
        def _validate(bus):
            if not (1 <= bus <= 5):
                raise ValueError(f"Must be EPN256.I2c.I2c1..I2c5, got {bus}")

        @classmethod
        def Initialize(cls, bus, frequency=400_000):
            cls._validate(bus)
            if not (10_000 <= frequency <= 1_000_000):
                raise ValueError(f"I2C speeds are 10 kHz to 1 MHz, got {frequency}")
            with cls._gate:
                if not cls._module_loaded:
                    _run("/sbin/modprobe", cls._module_name)
                    cls._module_loaded = True
                cls._open_count[bus] = cls._open_count.get(bus, 0) + 1
            dev = f"/dev/i2c-{bus}"
            if not _wait_path(dev, timeout_s=1.0):
                raise OSError(f"i2c-ma35d1 loaded but {dev} did not appear within 1 s.")

        @classmethod
        def UnInitialize(cls, bus):
            cls._validate(bus)
            with cls._gate:
                n = cls._open_count.get(bus, 0)
                if n <= 0:
                    return
                if n == 1:
                    cls._open_count.pop(bus, None)
                else:
                    cls._open_count[bus] = n - 1
                if not cls._open_count and cls._module_loaded:
                    _try_run("/sbin/rmmod", cls._module_name)
                    cls._module_loaded = False

    class Pwm:
        """MA35D1 Enhanced PWM. Initialize modprobes pwm-ma35d1 and resolves
        the Linux pwmchipN that corresponds to the requested EPWM instance.
        Mirrors .NET's EPN256.Pwm."""
        Epwm0 = 0
        Epwm1 = 1  # Used by display backlight on EPN256
        Epwm2 = 2

        _module_name = "pwm-ma35d1"
        _platform_devices = ["40580000.epwm0", "40590000.epwm1", "405a0000.epwm2"]
        _gate = threading.Lock()
        _open_count = {}
        _module_loaded = False

        @staticmethod
        def _validate(controller):
            if not (0 <= controller <= 2):
                raise ValueError(f"Must be EPN256.Pwm.Epwm0 / Epwm1 / Epwm2, got {controller}")

        @classmethod
        def Initialize(cls, controller):
            cls._validate(controller)
            with cls._gate:
                if not cls._module_loaded:
                    _run("/sbin/modprobe", cls._module_name)
                    cls._module_loaded = True
                cls._open_count[controller] = cls._open_count.get(controller, 0) + 1
            for _ in range(50):
                if cls._try_get_chip_id(controller) is not None:
                    return
                time.sleep(0.02)
            raise OSError(
                f"pwm-ma35d1 loaded but no pwmchip matched {cls._platform_devices[controller]} within 1 s.")

        @classmethod
        def UnInitialize(cls, controller):
            cls._validate(controller)
            with cls._gate:
                n = cls._open_count.get(controller, 0)
                if n <= 0:
                    return
                if n == 1:
                    cls._open_count.pop(controller, None)
                else:
                    cls._open_count[controller] = n - 1
                if not cls._open_count and cls._module_loaded:
                    _try_run("/sbin/rmmod", cls._module_name)
                    cls._module_loaded = False

        @classmethod
        def GetChipId(cls, controller):
            """The Linux pwmchip number for this EPWM instance. Call after Initialize."""
            cls._validate(controller)
            chip = cls._try_get_chip_id(controller)
            if chip is None:
                raise OSError(f"EPN256.Pwm.Initialize({controller}) must be called first.")
            return chip

        @classmethod
        def _try_get_chip_id(cls, controller):
            root = "/sys/class/pwm"
            if not os.path.isdir(root):
                return None
            expected = cls._platform_devices[controller]
            for entry in sorted(os.listdir(root)):
                of_name_path = os.path.join(root, entry, "device", "of_node", "name")
                try:
                    with open(of_name_path) as f:
                        name = f.read().strip(" \t\r\n\x00")
                except OSError:
                    continue
                if expected.lower().endswith("." + name.lower()):
                    if entry.startswith("pwmchip"):
                        try:
                            return int(entry[len("pwmchip"):])
                        except ValueError:
                            pass
            return None

    class Can:
        """MA35D1 CAN interfaces (Bosch M_CAN). Initialize modprobes
        m_can_platform and brings the SocketCAN interface up at the given
        bitrate. Mirrors .NET's EPN256.Can."""
        Can0 = "can0"
        Can1 = "can1"

        _module_name = "m_can_platform"
        _gate = threading.Lock()
        _open_count = {}
        _module_loaded = False

        @staticmethod
        def _validate(iface):
            if iface not in ("can0", "can1"):
                raise ValueError(f"Must be EPN256.Can.Can0 or Can1, got {iface}")

        @classmethod
        def Initialize(cls, iface, bitrate=500_000):
            cls._validate(iface)
            if not (10_000 <= bitrate <= 1_000_000):
                raise ValueError(f"CAN bitrate must be 10 kbit/s to 1 Mbit/s, got {bitrate}")
            with cls._gate:
                if not cls._module_loaded:
                    _run("/sbin/modprobe", cls._module_name)
                    cls._module_loaded = True
                cls._open_count[iface] = cls._open_count.get(iface, 0) + 1
            if not _wait_path(f"/sys/class/net/{iface}", is_dir=True, timeout_s=1.0):
                raise OSError(f"m_can_platform loaded but {iface} did not appear within 1 s.")
            _run("/sbin/ip", "link", "set", iface, "down")
            _run("/sbin/ip", "link", "set", iface, "type", "can", "bitrate", str(bitrate))
            _run("/sbin/ip", "link", "set", iface, "up")

        @classmethod
        def UnInitialize(cls, iface):
            if iface not in ("can0", "can1"):
                return
            with cls._gate:
                n = cls._open_count.get(iface, 0)
                if n <= 0:
                    return
                if n == 1:
                    cls._open_count.pop(iface, None)
                    _try_run("/sbin/ip", "link", "set", iface, "down")
                else:
                    cls._open_count[iface] = n - 1
                if not cls._open_count and cls._module_loaded:
                    _try_run("/sbin/rmmod", cls._module_name)
                    cls._module_loaded = False

    class SerialPort:
        """MA35D1 user UARTs (UART0 is the console, not exposed). Initialize
        sysfs-binds the UART's platform device to the ma35d1-uart driver, which
        re-applies pinctrl and claims the TX/RX pins. Mirrors .NET's
        EPN256.SerialPort."""
        Uart11 = "/dev/ttyS11"
        Uart12 = "/dev/ttyS12"
        Uart14 = "/dev/ttyS14"

        _driver_path = "/sys/bus/platform/drivers/ma35d1-uart"
        _device_name = {
            "/dev/ttyS11": "407b0000.serial",
            "/dev/ttyS12": "407c0000.serial",
            "/dev/ttyS14": "407e0000.serial",
        }

        _gate = threading.Lock()
        _open_count = {}

        @classmethod
        def Initialize(cls, device):
            platform = cls._device_name.get(device)
            if platform is None:
                raise ValueError(f"Must be EPN256.SerialPort.Uart11 / Uart12 / Uart14, got {device}")
            with cls._gate:
                n = cls._open_count.get(device, 0)
                if n == 0 and not os.path.exists(f"{cls._driver_path}/{platform}"):
                    cls._write_sysfs(f"{cls._driver_path}/bind", platform)
                cls._open_count[device] = n + 1
            if not _wait_path(device, timeout_s=1.0):
                raise OSError(f"Bound {platform} but {device} did not appear within 1 s.")

        @classmethod
        def UnInitialize(cls, device):
            platform = cls._device_name.get(device)
            if platform is None:
                return
            with cls._gate:
                n = cls._open_count.get(device, 0)
                if n <= 0:
                    return
                if n == 1:
                    cls._open_count.pop(device, None)
                    try:
                        cls._write_sysfs(f"{cls._driver_path}/unbind", platform)
                    except OSError:
                        pass
                else:
                    cls._open_count[device] = n - 1

        @staticmethod
        def _write_sysfs(path, value):
            # Use the shell for the single-write semantics sysfs bind requires.
            result = subprocess.run(["/bin/sh", "-c", f"echo {value} > {path}"],
                                    capture_output=True, text=True)
            if result.returncode != 0:
                raise OSError(f"Writing '{value}' to {path} failed: {result.stderr.strip()}")

    class StorageController:
        """Storage controller name strings, for
        GHIElectronics.Endpoint.Devices.Storage.StorageController.FromName.
        Mirrors .NET's EPN256.StorageController:

            usb = StorageController.FromName(EPN256.StorageController.UsbHostMassStorage)
        """
        SdCard = "EPN256.StorageController.SdCard"
        UsbHostMassStorage = "EPN256.StorageController.UsbHostMassStorage"

    class Adc:
        """MA35D1 Enhanced ADC via Linux IIO. No modprobe needed (controller
        is bundled). Mirrors .NET's EPN256.Adc."""
        Eadc0 = "eadc0"

        _iio_root = "/sys/bus/iio/devices"

        @classmethod
        def ReadRaw(cls, bank, channel):
            """Raw ADC code (0..4095 for the 12-bit eadc0)."""
            iio_dir = cls._resolve_iio_dir(bank)
            path = f"{iio_dir}/in_voltage{channel}_raw"
            if not os.path.exists(path):
                raise OSError(f"ADC channel {channel} not available on {bank} ({path} missing).")
            with open(path) as f:
                return int(f.read().strip())

        @classmethod
        def ReadMillivolts(cls, bank, channel):
            """Raw * (scale, if present in IIO) + offset, in mV. Fallback: 3.3 V / 4096."""
            iio_dir = cls._resolve_iio_dir(bank)
            raw = cls.ReadRaw(bank, channel)
            scale = (cls._try_read_float(f"{iio_dir}/in_voltage_scale")
                     or cls._try_read_float(f"{iio_dir}/in_voltage{channel}_scale")
                     or (3300.0 / 4096.0))
            offset = cls._try_read_float(f"{iio_dir}/in_voltage_offset") or 0.0
            return int(round((raw + offset) * scale))

        @classmethod
        def _resolve_iio_dir(cls, bank):
            if bank != cls.Eadc0:
                raise ValueError(f"Only EPN256.Adc.Eadc0 is supported on this chip, got {bank}")
            if not os.path.isdir(cls._iio_root):
                raise OSError("IIO subsystem not available: CONFIG_IIO is not enabled.")
            for entry in os.listdir(cls._iio_root):
                name_path = os.path.join(cls._iio_root, entry, "name")
                try:
                    with open(name_path) as f:
                        name = f.read().strip()
                except OSError:
                    continue
                if name.lower() in ("40430000.adc", "ma35d1-eadc"):
                    return os.path.join(cls._iio_root, entry)
            raise OSError(f"IIO device for {bank} not found under {cls._iio_root}.")

        @staticmethod
        def _try_read_float(path):
            try:
                with open(path) as f:
                    return float(f.read().strip())
            except OSError:
                return None
