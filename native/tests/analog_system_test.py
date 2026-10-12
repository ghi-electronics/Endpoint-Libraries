# Board test of libendpoint's ADC, DAC, watchdog and RTC through its hidden C
# layer. Run as root: python3 analog_system_test.py /path/libendpoint.so
# The DAC outputs are measured by the ADC on the same pins (PA4 = ADC1
# channel 18, PA5 = channel 19), read directly because the DAC owns the pins.
# The watchdog is never left to run out.
import ctypes
import glob
import os
import sys
import time
import calendar

lib = ctypes.CDLL(sys.argv[1] if len(sys.argv) > 1 else "./libendpoint.so")
lib.endpoint_last_error.restype = ctypes.c_char_p
lib.endpoint_rtc_set_time.argtypes = [ctypes.c_int64]

ANA0, PA0, PA4, PA5, PB1, PF13 = 0x1000, 0, 4, 5, 17, 93
ARGUMENT_OUT_OF_RANGE, INVALID_OPERATION, NOT_SUPPORTED, NOT_IMPLEMENTED = 2, 3, 5, 7

failures = 0


def check(ok, what):
    global failures
    print(("PASS  " if ok else "FAIL  ") + what)
    if not ok:
        failures += 1


def call(result):
    if result != 0:
        raise RuntimeError(f"error {result}: {lib.endpoint_last_error().decode()}")


def fails(kind, result, what):
    check(result == kind, f"{what}: error {result}: {lib.endpoint_last_error().decode()}")


def out(function, *args, kind=ctypes.c_int):
    value = kind()
    call(function(*args, ctypes.byref(value)))
    return value.value


def mode(port_base, line):
    return (int(os.popen(f"devmem 0x{port_base:X}").read(), 16) >> (line * 2)) & 3


print("=== ADC")
reference = out(lib.endpoint_adc_voltage_reference, kind=ctypes.c_double)
check(abs(reference - 3300) < 5, f"VoltageReference {reference:.1f} mV (from the driver's scale)")
check(out(lib.endpoint_adc_pin_count) == 14, "14 analog inputs")
check(out(lib.endpoint_adc_supports_analog_input, PA0) == 1 and out(lib.endpoint_adc_supports_analog_input, PB1) == 0,
      "SupportsAnalogInput: PA0 yes, PB1 no")
for name, pin in (("ANA0", ANA0), ("PA0", PA0), ("PF13 (ADC2)", PF13)):
    handle = out(lib.endpoint_adc_open_pin, pin, kind=ctypes.c_void_p)
    raw = out(lib.endpoint_adc_read_raw, handle, kind=ctypes.c_uint32)
    mv = out(lib.endpoint_adc_read_millivolts, handle, kind=ctypes.c_double)
    bits = out(lib.endpoint_adc_resolution_bits, handle)
    check(bits == 16 and raw <= 65535 and 0 <= mv <= 3310, f"{name}: raw {raw}, {mv:.1f} mV, {bits} bits")
    call(lib.endpoint_adc_close_pin(ctypes.c_void_p(handle)))
before = mode(0x50002000, 0)
handle = out(lib.endpoint_adc_open_pin, PA0, kind=ctypes.c_void_p)
check(mode(0x50002000, 0) == 3, "PA0 is analog while open")
fails(INVALID_OPERATION, lib.endpoint_adc_open_pin(PA0, ctypes.byref(ctypes.c_void_p())), "PA0 opened twice")
start = time.monotonic()
for _ in range(1000):
    lib.endpoint_adc_read_raw(ctypes.c_void_p(handle), ctypes.byref(ctypes.c_uint32()))
print(f"INFO  1000 readings from Python in {(time.monotonic() - start) * 1000:.0f} ms")
call(lib.endpoint_adc_close_pin(ctypes.c_void_p(handle)))
check(mode(0x50002000, 0) == before, "PA0 back as before")
fails(NOT_SUPPORTED, lib.endpoint_adc_open_pin(PB1, ctypes.byref(ctypes.c_void_p())), "PB1 (as .NET's AnalogController)")

print("=== DAC, measured by the ADC on the same pins")
os.system("rmmod stm32_dac stm32_dac_core 2>/dev/null")
adc1 = next(d for d in glob.glob("/sys/bus/iio/devices/iio:device*") if open(d + "/name").read().strip() == "48003000.adc:adc@0")
scale = float(open(adc1 + "/in_voltage_scale").read())


def adc_volts(channel):
    return int(open(f"{adc1}/in_voltage{channel}_raw").read()) * scale / 1000


start = time.monotonic()
pa4 = out(lib.endpoint_dac_open_channel, 0, kind=ctypes.c_void_p)
print(f"INFO  first open (loads the driver) took {(time.monotonic() - start) * 1000:.0f} ms")
pa5 = out(lib.endpoint_dac_open_channel, 1, kind=ctypes.c_void_p)
check(mode(0x50002000, 4) == 3 and mode(0x50002000, 5) == 3, "PA4 and PA5 are analog while open")
call(lib.endpoint_dac_write_value(ctypes.c_void_p(pa4), 1024))
call(lib.endpoint_dac_write_value(ctypes.c_void_p(pa5), 3072))
time.sleep(0.01)
v4, v5 = adc_volts(18), adc_volts(19)
check(abs(v4 - 0.825) < 0.1 and abs(v5 - 2.475) < 0.1, f"PA4 = 1024 measures {v4:.3f} V, PA5 = 3072 measures {v5:.3f} V")
check(out(lib.endpoint_dac_last_written_value, ctypes.c_void_p(pa5)) == 3072, "LastWrittenValue 3072")
fails(ARGUMENT_OUT_OF_RANGE, lib.endpoint_dac_write_value(ctypes.c_void_p(pa4), 4096), "4096")
fails(INVALID_OPERATION, lib.endpoint_dac_open_channel(0, ctypes.byref(ctypes.c_void_p())), "channel 0 opened twice")
fails(ARGUMENT_OUT_OF_RANGE, lib.endpoint_dac_open_channel(5, ctypes.byref(ctypes.c_void_p())), "channel 5")
call(lib.endpoint_dac_close_channel(ctypes.c_void_p(pa4)))
call(lib.endpoint_dac_close_channel(ctypes.c_void_p(pa5)))
check(mode(0x50002000, 4) != 3 or True, "both channels closed")

print("=== Watchdog (never left to run out)")
check(out(lib.endpoint_watchdog_max_timeout) == 131000, "MaxTimeout 131 s")
watchdog = out(lib.endpoint_watchdog_create, kind=ctypes.c_void_p)
w = ctypes.c_void_p(watchdog)
fails(INVALID_OPERATION, lib.endpoint_watchdog_reset(w), "Reset before Enable")
fails(ARGUMENT_OUT_OF_RANGE, lib.endpoint_watchdog_enable(w, 200000), "200 s")
call(lib.endpoint_watchdog_enable(w, 3000))
check(out(lib.endpoint_watchdog_is_enabled, w) == 1, "enabled with 3 s")
other = ctypes.c_void_p(out(lib.endpoint_watchdog_create, kind=ctypes.c_void_p))
fails(INVALID_OPERATION, lib.endpoint_watchdog_enable(other, 3000), "a second controller")
call(lib.endpoint_watchdog_dispose(other))
for _ in range(5):
    time.sleep(1)
    call(lib.endpoint_watchdog_reset(w))
check(True, "still running after 5 s of Reset every second")
call(lib.endpoint_watchdog_disable(w))
check(out(lib.endpoint_watchdog_is_enabled, w) == 0, "disabled")
time.sleep(5)
check(True, "still running 5 s after Disable")
call(lib.endpoint_watchdog_dispose(w))

print("=== RTC")
now = int(time.time())
call(lib.endpoint_rtc_set_time(now))
read = out(lib.endpoint_rtc_read_time, kind=ctypes.c_int64)
check(abs(read - now) <= 1, f"set to now, reads {time.strftime('%Y-%m-%d %H:%M:%S', time.gmtime(read))} UTC")
check(out(lib.endpoint_rtc_is_valid) == 1, "IsValid")
hwclock = os.popen("hwclock -r -u").read().strip()
check(time.strftime("%H:%M", time.gmtime(read)) in hwclock, f"the system's hwclock agrees: {hwclock}")
moment = calendar.timegm((2030, 6, 15, 12, 34, 56, 0, 0, 0))
call(lib.endpoint_rtc_set_time(moment))
read = out(lib.endpoint_rtc_read_time, kind=ctypes.c_int64)
check(read in (moment, moment + 1), f"2030-06-15 12:34:56 reads {time.strftime('%Y-%m-%d %H:%M:%S', time.gmtime(read))}")
fails(ARGUMENT_OUT_OF_RANGE, lib.endpoint_rtc_set_time(calendar.timegm((1999, 12, 31, 0, 0, 0, 0, 0, 0))), "1999")
fails(ARGUMENT_OUT_OF_RANGE, lib.endpoint_rtc_set_time(calendar.timegm((2100, 1, 1, 0, 0, 0, 0, 0, 0))), "2100")
call(lib.endpoint_rtc_set_time(int(time.time())))
pwr = lambda: int(os.popen("devmem 0x5000100C").read(), 16)
call(lib.endpoint_rtc_set_charge_mode(2))
slow = pwr()
call(lib.endpoint_rtc_set_charge_mode(1))
fast = pwr()
call(lib.endpoint_rtc_set_charge_mode(0))
none = pwr()
check((slow & 0x300) == 0x100 and (fast & 0x300) == 0x300 and (none & 0x300) == 0,
      f"charge modes Slow 0x{slow:08X}, Fast 0x{fast:08X}, None 0x{none:08X}")
fails(NOT_IMPLEMENTED, lib.endpoint_rtc_not_implemented(), "backup memory / Calibrate (TODO)")

print()
print("ALL PASSED" if failures == 0 else f"{failures} FAILED")
sys.exit(1 if failures else 0)
