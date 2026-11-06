"""The EPN128 module (Nuvoton MA35D0, Chili Pro form factor): its pin
names and the pin setup the other libraries use. Mirrors _epn256 so sample
code reads the same shape across MA35 boards; MA35D0 and MA35D1 share the
same pinctrl layout, the chips differ in how many pins are bonded out."""

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


class EPN128:
    """The EPN128 (Nuvoton MA35D0, Chili Pro) module"""

    class Gpio:
        """GPIO pins, for GHIElectronics.Endpoint.Devices.Gpio's GpioController"""

        class Pin:
            """The EPN128's pins: port * 16 + line, ports A to N"""
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
    # the .NET GHIElectronics.Endpoint.Core.EPN128.* classes: Initialize loads
    # the kernel driver (or binds a platform device via sysfs), the kernel's
    # pinctrl framework claims the pins at probe time, and UnInitialize
    # reverses it so unused pins return to free GPIO.

    class I2c:
        """MA35D0 I2C buses on Chili Pro. Initialize modprobes i2c-ma35d1
        (same driver as MA35D1); pinctrl claims the bus's pins at probe.
        Only I2C4 and I2C5 are bonded out. Mirrors .NET's EPN128.I2c."""
        I2c4 = 4  # PC0 (SDA), PC1 (SCL); /dev/i2c-4
        I2c5 = 5  # PC4 (SDA), PC5 (SCL); /dev/i2c-5

        DefaultFrequency = 400_000
        """The speed when none is given: 400 kHz (fast mode)"""
        MinFrequency = 10_000
        """The slowest supported bus speed: 10 kHz"""
        MaxFrequency = 1_000_000
        """1 MHz (fast mode plus)"""

        _module_name = "i2c-ma35d1"
        _gate = threading.Lock()
        _open_count = {}
        _module_loaded = False

        @staticmethod
        def _validate(bus):
            if bus not in (4, 5):
                raise ValueError(f"The I2C buses on EPN128 are I2c4 and I2c5, got {bus}")

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
            cls._set_frequency(bus, frequency)

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

        @classmethod
        def GetFrequency(cls, bus):
            """The bus speed in use, in Hz."""
            cls._validate(bus)
            path = cls._speed_file(bus)
            if os.path.exists(path):
                try:
                    with open(path) as f:
                        return int(f.read().strip())
                except OSError:
                    pass
            return cls.DefaultFrequency

        @classmethod
        def _set_frequency(cls, bus, frequency):
            path = cls._speed_file(bus)
            if os.path.exists(path):
                try:
                    with open(path, "w") as f:
                        f.write(str(frequency))
                except OSError:
                    pass

        @staticmethod
        def _speed_file(bus):
            # MMIO addresses from arch/arm64/.../ma35d0.dtsi.
            addresses = {4: 0x40840000, 5: 0x40850000}
            return f"/sys/bus/platform/devices/{addresses[bus]:x}.i2c/clock_frequency"

    class Can:
        """MA35D0 CAN interfaces on Chili Pro (Bosch M_CAN). Initialize
        modprobes m_can_platform and brings the SocketCAN interface up at
        the given bitrate. All four CAN controllers are bonded out.
        Mirrors .NET's EPN128.Can."""
        Can0 = "can0"  # PC2 (RXD), PC3 (TXD)
        Can1 = "can1"  # PC6 (RXD), PC7 (TXD)
        Can2 = "can2"  # PA15 (RXD), PG1 (TXD)
        Can3 = "can3"  # PG8 (RXD), PG9 (TXD)

        MinBitrate = 10_000
        """The slowest supported bitrate: 10 kbit/s"""
        MaxBitrate = 1_000_000
        """The fastest supported bitrate: 1 Mbit/s (classic CAN)"""

        _module_name = "m_can_platform"
        _gate = threading.Lock()
        _open_count = {}
        _module_loaded = False

        @staticmethod
        def _validate(iface):
            if iface not in ("can0", "can1", "can2", "can3"):
                raise ValueError(f"Must be EPN128.Can.Can0 / Can1 / Can2 / Can3, got {iface}")

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
            if iface not in ("can0", "can1", "can2", "can3"):
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
        """MA35D0 user UARTs on Chili Pro (the console UART is reserved for
        the kernel and not exposed). Unlike EPN256.SerialPort, EPN128's
        UARTs use the kernel's standard UART binding — no sysfs bind or
        modprobe needed; pinctrl claims the TX/RX pins at Initialize.
        Mirrors .NET's EPN128.SerialPort."""
        Uart4 = "/dev/ttyS4"    # PI10 (RXD), PI11 (TXD), PI8 (CTS), PI9 (RTS)
        Uart5 = "/dev/ttyS5"    # PG6 (RXD), PG7 (TXD); no flow-control pins bonded
        Uart7 = "/dev/ttyS7"    # PL14 (RXD), PG0 (TXD); no flow-control pins bonded
        Uart9 = "/dev/ttyS9"    # PG2 (RXD), PG3 (TXD); no flow-control pins bonded
        Uart13 = "/dev/ttyS13"  # PK4 (RXD), PG10 (TXD); no flow-control pins bonded

        _ports = ("/dev/ttyS4", "/dev/ttyS5", "/dev/ttyS7", "/dev/ttyS9", "/dev/ttyS13")

        @classmethod
        def Initialize(cls, device, enableHardwareFlowControl=False):
            if device not in cls._ports:
                raise ValueError(
                    f"Must be one of EPN128.SerialPort.Uart4 / Uart5 / Uart7 / Uart9 / Uart13, got {device}")
            if not os.path.exists(device):
                suffix = device[len("/dev/ttyS"):]
                raise OSError(
                    f"{device} does not exist: this firmware is missing uart{suffix}.")

        @classmethod
        def UnInitialize(cls, device):
            # No-op: EPN128 UARTs use the kernel's standard UART binding,
            # so there is nothing to unbind on close.
            pass

    class Adc:
        """MA35D0 ADC on Chili Pro via Linux IIO. 4-channel 10-bit ADC
        (compatible nuvoton,ma35d1-adc); only CH2 and CH3 are bonded out.
        No modprobe needed (controller is bundled). Mirrors .NET's EPN128.Adc."""

        class Pin:
            """The pins with an analog input (ADC0 channel is in the comment)."""
            PB10 = 26  # ADC0 channel 2
            PB11 = 27  # ADC0 channel 3

        IioName = "40420000.adc"
        """IIO name of the MA35D0 ADC. The ma35_adc driver sets
        indio_dev->name = dev_name(pdev), i.e. the DT node name."""

        _iio_root = "/sys/bus/iio/devices"

        _channels = {26: 2, 27: 3}  # PB10 -> CH2, PB11 -> CH3

        @classmethod
        def ReadRaw(cls, pin):
            """Raw ADC code (0..1023 for the 10-bit MA35D0 ADC)."""
            channel = cls._channel(pin)
            iio_dir = cls._resolve_iio_dir()
            path = f"{iio_dir}/in_voltage{channel}_raw"
            if not os.path.exists(path):
                raise OSError(f"ADC channel {channel} not available ({path} missing).")
            with open(path) as f:
                return int(f.read().strip())

        @classmethod
        def ReadMillivolts(cls, pin):
            """Raw * (scale, if present in IIO) + offset, in mV. Fallback: 3.3 V / 1024."""
            channel = cls._channel(pin)
            iio_dir = cls._resolve_iio_dir()
            raw = cls.ReadRaw(pin)
            scale = (cls._try_read_float(f"{iio_dir}/in_voltage_scale")
                     or cls._try_read_float(f"{iio_dir}/in_voltage{channel}_scale")
                     or (3300.0 / 1024.0))
            offset = cls._try_read_float(f"{iio_dir}/in_voltage_offset") or 0.0
            return int(round((raw + offset) * scale))

        @classmethod
        def _channel(cls, pin):
            if pin not in cls._channels:
                raise ValueError(f"Must be EPN128.Adc.Pin.PB10 or PB11, got {pin}")
            return cls._channels[pin]

        @classmethod
        def _resolve_iio_dir(cls):
            if not os.path.isdir(cls._iio_root):
                raise OSError("IIO subsystem not available: CONFIG_IIO is not enabled.")
            for entry in os.listdir(cls._iio_root):
                name_path = os.path.join(cls._iio_root, entry, "name")
                try:
                    with open(name_path) as f:
                        name = f.read().strip()
                except OSError:
                    continue
                if name.lower() == cls.IioName.lower():
                    return os.path.join(cls._iio_root, entry)
            raise OSError(f"IIO device {cls.IioName} not found under {cls._iio_root}.")

        @staticmethod
        def _try_read_float(path):
            try:
                with open(path) as f:
                    return float(f.read().strip())
            except OSError:
                return None

    class Watchdog:
        """The watchdog, for GHIElectronics.Endpoint.Devices.Watchdog
        (same API as EPN256.Watchdog and EPM815.Watchdog)."""

        Id = "EPN128.Watchdog"
        """The watchdog's name, for WatchdogController.FromName"""
