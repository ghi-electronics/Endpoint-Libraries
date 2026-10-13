# Board test of the Endpoint Python packages, written as a customer would
# use them. Run as root on the board with the packages on the path.
# Needs nothing connected: PF13/PF14 are unconnected (PF13 follows its pull,
# which also makes pin change events), the DAC outputs are measured by the
# ADC on the same pins, PWM by sampling the pin. The watchdog never runs out.
import datetime
import glob
import mmap
import os
import sys
import threading
import time

from GHIElectronics.Endpoint.Core import (EPM815, ArgumentException, ArgumentOutOfRangeException,
                                          DeviceConnectionStatus, InvalidOperationException, IOException,
                                          NotImplementedException, NotSupportedException, ObjectDisposedException)
from GHIElectronics.Endpoint.Devices.Adc import AdcController
from GHIElectronics.Endpoint.Devices.Dac import DacController
from GHIElectronics.Endpoint.Devices.Gpio import GpioController, PinEventTypes, PinMode, PinValue
from GHIElectronics.Endpoint.Devices.I2c import I2cConnectionSettings, I2cDevice
from GHIElectronics.Endpoint.Devices.Pwm import PwmChannel
from GHIElectronics.Endpoint.Devices.Rtc import BatteryChargeMode, RtcController
from GHIElectronics.Endpoint.Devices.Spi import SpiConnectionSettings, SpiDevice, SpiMode
from GHIElectronics.Endpoint.Devices import Storage
from GHIElectronics.Endpoint.Devices.Storage import DriveInfo, DriveType, FileSystem, StorageController
from GHIElectronics.Endpoint.Devices.UsbHost import DeviceType, UsbHostController
from GHIElectronics.Endpoint.Devices.Watchdog import WatchdogController

failures = 0


def check(ok, what):
    global failures
    print(("PASS  " if ok else "FAIL  ") + what)
    if not ok:
        failures += 1


def raises(exception, action, what):
    try:
        action()
        check(False, what + ": no exception")
    except exception as e:
        check(True, f"{what}: {type(e).__name__}: {e}")
    except Exception as e:
        check(False, f"{what}: wrong exception {type(e).__name__}: {e}")


def mode(port_base, line):
    return (int(os.popen(f"devmem 0x{port_base:X}").read(), 16) >> (line * 2)) & 3


Pin = EPM815.Gpio.Pin
GPIOA = 0x50002000

print("=== Core")
check(Pin.PC0 == 32 and Pin.PZ7 == 151 and EPM815.Gpio.Name(Pin.PF13) == "PF13", "EPM815.Gpio.Pin.PC0 = 32, PZ7 = 151, Name(PF13)")
check(issubclass(ArgumentOutOfRangeException, ValueError) and issubclass(IOException, OSError),
      "errors are also the matching Python errors (ValueError, OSError)")

print("=== GPIO")
with GpioController() as gpio:
    check(gpio.PinCount == 152, "PinCount 152")
    out = gpio.OpenPin(Pin.PF14, PinMode.Output, PinValue.High)
    check(gpio.Read(Pin.PF14) == PinValue.High, "PF14 opened as an output, High")
    gpio.Write(Pin.PF14, PinValue.Low)
    check(out.Read() == PinValue.Low, "Write Low; GpioPin.Read gives Low")
    out.Toggle()
    check(gpio.Read(Pin.PF14) == PinValue.High, "Toggle gives High")
    gpio.OpenPin(Pin.PF13, PinMode.InputPullUp)
    time.sleep(0.01)
    check(gpio.Read(Pin.PF13) == PinValue.High, "PF13 with pull-up reads High")
    gpio.SetPinMode(Pin.PF13, PinMode.InputPullDown)
    time.sleep(0.01)
    check(gpio.Read(Pin.PF13) == PinValue.Low and gpio.GetPinMode(Pin.PF13) == PinMode.InputPullDown,
          "InputPullDown reads Low")
    raises(InvalidOperationException, lambda: gpio.Write(Pin.PF13, PinValue.High), "Write to an input")
    raises(ArgumentOutOfRangeException, lambda: gpio.OpenPin(200), "pin 200")

    changes = []
    def on_change(sender, e):
        changes.append((e.PinNumber, e.ChangeType))
    gpio.RegisterCallbackForPinValueChangedEvent(Pin.PF13, PinEventTypes.Rising | PinEventTypes.Falling, on_change)
    gpio.SetPinMode(Pin.PF13, PinMode.InputPullUp)
    time.sleep(0.1)
    gpio.SetPinMode(Pin.PF13, PinMode.InputPullDown)
    time.sleep(0.1)
    check(changes[:2] == [(Pin.PF13, PinEventTypes.Rising), (Pin.PF13, PinEventTypes.Falling)],
          f"callback: {changes}")
    gpio.UnregisterCallbackForPinValueChangedEvent(Pin.PF13, on_change)
    threading.Timer(0.2, lambda: gpio.SetPinMode(Pin.PF13, PinMode.InputPullUp)).start()
    result = gpio.WaitForEvent(Pin.PF13, PinEventTypes.Rising, datetime.timedelta(seconds=2))
    check(result.EventTypes == PinEventTypes.Rising and not result.TimedOut, f"WaitForEvent: {result}")
    result = gpio.WaitForEvent(Pin.PF13, PinEventTypes.Falling, datetime.timedelta(milliseconds=300))
    check(result.TimedOut, f"WaitForEvent times out: {result}")

    print("=== ADC")
    adc = AdcController()
    check(abs(adc.VoltageReference.Volts - 3.3) < 0.01 and adc.PinCount == 14, f"VoltageReference {adc.VoltageReference}, 14 inputs")
    raises(InvalidOperationException, lambda: adc.OpenPin(Pin.PF13), "PF13 while GPIO has it")
gpio_free = GpioController()
gpio_free.Dispose()
adc = AdcController()
with adc.OpenPin(EPM815.Adc.Pin.PA0) as pa0:
    volts = pa0.ReadVoltage()
    check(pa0.AdcResolutionBits == 16 and 0 <= volts.Volts <= 3.31 and pa0.ReadRaw() <= 65535,
          f"PA0: {volts}, raw {pa0.ReadRaw()}, {volts.Millivolts:.1f} mV")
    check(mode(GPIOA, 0) == 3 and adc.IsPinOpen(EPM815.Adc.Pin.PA0), "PA0 analog while open")
check(not adc.IsPinOpen(EPM815.Adc.Pin.PA0), "closed")
with adc.OpenPin(EPM815.Adc.Pin.ANA0) as ana0:
    check(0 <= ana0.ReadVoltage().Volts <= 3.31, f"ANA0: {ana0.ReadVoltage()}")
raises(NotSupportedException, lambda: adc.OpenPin(Pin.PB1), "PB1")

print("=== DAC, measured by the ADC on the same pins")
adc1 = next(d for d in glob.glob("/sys/bus/iio/devices/iio:device*") if open(d + "/name").read().strip() == "48003000.adc:adc@0")
scale = float(open(adc1 + "/in_voltage_scale").read())
measure = lambda channel: int(open(f"{adc1}/in_voltage{channel}_raw").read()) * scale / 1000
raises(ArgumentException, lambda: DacController.FromName("DAC9"), "an unknown DAC name")
dac = DacController.FromName(EPM815.Dac.Id)
with dac.OpenChannel(EPM815.Dac.PA4) as pa4, dac.OpenChannel(EPM815.Dac.PA5) as pa5:
    pa4.WriteValue(0.25)      # a float: part of the range
    pa5.WriteValue(3072)      # an int: a raw value
    time.sleep(0.01)
    check(abs(measure(18) - 0.825) < 0.1 and abs(measure(19) - 2.475) < 0.1,
          f"PA4 = 0.25 measures {measure(18):.3f} V, PA5 = 3072 measures {measure(19):.3f} V")
    check(pa4.LastWrittenValue == 1023 and pa5.LastWrittenValue == 3072, "LastWrittenValue 1023 and 3072")
    raises(ArgumentOutOfRangeException, lambda: pa4.WriteValue(5000), "5000")

print("=== PWM on PA0, measured by sampling the pin")
memory = os.open("/dev/mem", os.O_RDONLY | os.O_SYNC)
gpioa = mmap.mmap(memory, 4096, mmap.MAP_SHARED, mmap.PROT_READ, offset=GPIOA)
high = lambda samples=200000: sum(gpioa[0x10] & 1 for _ in range(samples)) / samples
EPM815.Pwm.Initialize(EPM815.Pwm.Pin.PA0)
with PwmChannel.Create(EPM815.Pwm.GetChipId(EPM815.Pwm.Pin.PA0), EPM815.Pwm.GetChannelId(EPM815.Pwm.Pin.PA0), 1000, 0.25) as pwm:
    pwm.Start()
    fraction = high()
    check(abs(fraction - 0.25) < 0.05, f"1 kHz at 25%: high {fraction:.1%} of the time")
    pwm.DutyCycle = 0.75
    fraction = high()
    check(abs(fraction - 0.75) < 0.05 and pwm.DutyCycle == 0.75, f"DutyCycle = 0.75: high {fraction:.1%}")
    pwm.Frequency = 2000
    check(pwm.Frequency == 2000, "Frequency = 2000")
    pwm.Stop()
EPM815.Pwm.UnInitialize(EPM815.Pwm.Pin.PA0)
check(mode(GPIOA, 0) != 2, "PA0 given back")

print("=== SPI and I2C")
EPM815.Spi.Initialize(EPM815.Spi.Spi4)
settings = SpiConnectionSettings(EPM815.Spi.Spi4)  # chip select -1, as .NET's default
settings.Mode = SpiMode.Mode3
settings.ClockFrequency = 1_000_000
with SpiDevice.Create(settings) as spi:
    received = bytearray(4)
    spi.TransferFullDuplex(bytes([1, 2, 3, 4]), received)
    check(len(received) == 4, f"SPI4 full duplex (read {list(received)}: nothing connected)")
    raises(ArgumentException, lambda: spi.TransferFullDuplex(bytes(4), bytearray(3)), "different lengths")
    raises(TypeError, lambda: spi.Read(bytes(4)), "reading into bytes (not writable)")
EPM815.Spi.UnInitialize(EPM815.Spi.Spi4)
EPM815.I2c.Initialize(EPM815.I2c.I2c1)
check(EPM815.I2c.GetFrequency(EPM815.I2c.I2c1) == 400_000, "I2C1 at 400 kHz")
with I2cDevice.Create(I2cConnectionSettings(EPM815.I2c.I2c1, 0x50)) as device:
    raises(IOException, lambda: device.WriteByte(0), "no device at 0x50")
EPM815.I2c.UnInitialize(EPM815.I2c.I2c1)

print("=== Serial port and CAN setup")
EPM815.SerialPort.Initialize(EPM815.SerialPort.Uart4)
raises(InvalidOperationException, lambda: EPM815.Can.Initialize(EPM815.Can.Can1, 500_000), "CAN1 while UART4 has PD0/PD1")
EPM815.SerialPort.UnInitialize(EPM815.SerialPort.Uart4)
EPM815.Can.Initialize(EPM815.Can.Can1, 500_000)
check("state UP" in os.popen("ip link show can0").read(), "CAN1 up at 500 kbit/s")
EPM815.Can.UnInitialize(EPM815.Can.Can1)

print("=== Watchdog (never left to run out)")
raises(ArgumentException, lambda: WatchdogController.FromName("WDT9"), "an unknown watchdog name")
watchdog = WatchdogController.FromName(EPM815.Watchdog.Id)
check(watchdog.MaxTimeout == datetime.timedelta(seconds=131) and not watchdog.IsEnabled, f"MaxTimeout {watchdog.MaxTimeout}")
watchdog.Enable(datetime.timedelta(seconds=3))
for _ in range(4):
    time.sleep(1)
    watchdog.Reset()
check(watchdog.IsEnabled, "enabled 3 s, reset every second for 4 s")
watchdog.Disable()
check(not watchdog.IsEnabled, "disabled")
watchdog.Dispose()

print("=== RTC")
rtc = RtcController()
now = datetime.datetime.now(datetime.timezone.utc)
rtc.DateTime = now
check(abs((rtc.DateTime - now).total_seconds()) < 2 and rtc.DateTime.tzinfo is not None, f"DateTime {rtc.DateTime}")
rtc.RtcDateTime = datetime.datetime(2030, 6, 15, 12, 34, 56)
check(rtc.RtcDateTime.replace(microsecond=0) in (datetime.datetime(2030, 6, 15, 12, 34, 56), datetime.datetime(2030, 6, 15, 12, 34, 57)),
      f"RtcDateTime {rtc.RtcDateTime}")
raises(ArgumentOutOfRangeException, lambda: setattr(rtc, "RtcDateTime", datetime.datetime(2100, 1, 1)), "2100")
rtc.DateTime = datetime.datetime.now(datetime.timezone.utc)
check(rtc.IsValid, "IsValid")
rtc.SetChargeMode(BatteryChargeMode.Slow)
slow = int(os.popen("devmem 0x5000100C").read(), 16)
rtc.SetChargeMode(BatteryChargeMode.None_)
check((slow & 0x300) == 0x100 and (int(os.popen("devmem 0x5000100C").read(), 16) & 0x300) == 0, "charge modes Slow, None_")
raises(NotImplementedException, lambda: rtc.BackupMemorySize, "BackupMemorySize (TODO)")

print("=== Storage (with a USB drive plugged in, it is mounted and written too)")
raises(ArgumentException, lambda: StorageController.FromName("EPM815.StorageController.Floppy"), "an unknown storage controller")
raises(InvalidOperationException, lambda: FileSystem.Mount(12345), "a handle that is not a drive's")
usb = StorageController.FromName(EPM815.StorageController.UsbHostMassStorage)
connected, calls = [], []


def on_usb(sender, e):
    calls.append(e)
    if e.DeviceStatus == DeviceConnectionStatus.Connected and sender is usb:
        connected.append(e.Hdc)


def never(sender, e):
    calls.append("never")


usb.OnConnectionChangedEvent += on_usb
usb.OnConnectionChangedEvent += never
usb.OnConnectionChangedEvent -= never
raises(TypeError, lambda: setattr(usb, "OnConnectionChangedEvent", on_usb), "= instead of +=")
usb.Enable()
time.sleep(1)
check("never" not in calls and len(calls) == len(connected), f"+= and -=: {calls}")
if not connected:
    print("      (no USB drive plugged in: nothing mounted)")
for hdc in connected:
    drive = FileSystem.Mount(hdc)
    check(drive.Name.startswith("/media/usb") and drive.Name.endswith("/") and drive.IsReady,
          f"mounted on {drive.Name} ({drive.DriveFormat}, {drive.AvailableFreeSpace >> 20} MB free)")
    check(FileSystem.Mount(hdc).Name == drive.Name, "a second Mount gives the same folder")
    text = "Written by the Endpoint Python test"
    with open(os.path.join(drive, "endpoint-python-test.txt"), "w") as f:
        f.write(text)
    FileSystem.Flush(hdc)
    with open(drive.RootDirectory / "endpoint-python-test.txt") as f:
        check(f.read() == text, "written, flushed and read back (DriveInfo works as a path)")
    check(drive.TotalSize > 0 and drive.DriveType == DriveType.Fixed, "TotalSize and DriveType")
check(DriveInfo("/data").DriveFormat == "ext4" and DriveInfo("/data").IsReady, 'DriveInfo("/data"): ext4')
check(not DriveInfo("/no/such/folder").IsReady, "a folder that does not exist is not ready")
usb.Dispose()
raises(ObjectDisposedException, usb.Enable, "Enable after Dispose")
check(Storage.DeviceConnectionStatus is DeviceConnectionStatus, "Storage's DeviceConnectionStatus is Core's")

print("=== USB host (lists what is plugged in)")
host = UsbHostController.GetDefault()
things = []
host.OnConnectionChangedEvent += lambda sender, e: things.append(e)
host.Enable()
time.sleep(1)
for e in things:
    print(f"      #{e.Id} {e.Type.name:<11} {e.DeviceName:<22} {e.VendorId:04X}:{e.ProductId:04X} "
          f"{e.Manufacturer!r} {e.Product!r}")
check(things and all(e.DeviceStatus == DeviceConnectionStatus.Connected for e in things),
      f"{len(things)} USB things connected")
check(all(isinstance(e.Type, DeviceType) and e.Id > 0 for e in things), "each has a DeviceType and an Id")
nets = [e.DeviceName for e in things if e.Type == DeviceType.Network]
check(all(os.path.exists(f"/sys/class/net/{name}") for name in nets), f"network adapters are interfaces: {nets}")
host.Dispose()
raises(ObjectDisposedException, host.Enable, "Enable after Dispose")

print()
print("ALL PASSED" if failures == 0 else f"{failures} FAILED")
sys.exit(1 if failures else 0)
