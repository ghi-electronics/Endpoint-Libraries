# Board test of libendpoint's I2C, SPI, PWM, serial port and CAN setup through
# its hidden C layer. Run as root: python3 buses_test.py /path/libendpoint.so
# PWM on PA0 is checked by sampling the pin's input register (IDR), which
# follows the pin in alternate-function mode too.
import ctypes
import glob
import os
import sys

lib = ctypes.CDLL(sys.argv[1] if len(sys.argv) > 1 else "./libendpoint.so")
lib.endpoint_last_error.restype = ctypes.c_char_p
lib.endpoint_pwmchannel_create.argtypes = [ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.c_double,
                                           ctypes.POINTER(ctypes.c_void_p)]
lib.endpoint_pwmchannel_set_duty_cycle.argtypes = [ctypes.c_void_p, ctypes.c_double]

I2C5, SPI4 = 1, 1
PA0, PB1, PD0, PD1 = 0, 17, 48, 49
ARGUMENT, ARGUMENT_OUT_OF_RANGE, INVALID_OPERATION, IO = 1, 2, 3, 4

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


def devmem(address):
    return int(os.popen(f"devmem 0x{address:X}").read(), 16)


def mode(port_base, line):
    return (devmem(port_base) >> (line * 2)) & 3


print("=== I2C")
fails(ARGUMENT_OUT_OF_RANGE, lib.endpoint_i2c_initialize(I2C5, 2_000_000), "2 MHz")
call(lib.endpoint_i2c_initialize(I2C5, 400_000))
frequency = ctypes.c_int()
call(lib.endpoint_i2c_get_frequency(I2C5, ctypes.byref(frequency)))
check(frequency.value == 400_000, f"I2C5 at {frequency.value} Hz")
# No I2C device answers on this board: a NACK ("No such device or address") shows the
# controller really sent the address on the wire (unconnected pins would time out instead)
absent = ctypes.c_void_p()
call(lib.endpoint_i2cdevice_create(I2C5, 0x50, ctypes.byref(absent)))
fails(IO, lib.endpoint_i2cdevice_write(absent, (ctypes.c_uint8 * 1)(0), 1), "write to an absent device (0x50)")
call(lib.endpoint_i2cdevice_dispose(absent))
fails(ARGUMENT_OUT_OF_RANGE, lib.endpoint_i2cdevice_create(I2C5, 0x80, ctypes.byref(ctypes.c_void_p())), "address 0x80")
call(lib.endpoint_i2c_uninitialize(I2C5))

print("=== SPI")
call(lib.endpoint_spi_initialize(SPI4, 4096))
spi = ctypes.c_void_p()
call(lib.endpoint_spidevice_create(SPI4, 0, 3, 8, 1_000_000, 1, 0, ctypes.byref(spi)))
write, read = (ctypes.c_uint8 * 4)(1, 2, 3, 4), (ctypes.c_uint8 * 4)()
call(lib.endpoint_spidevice_transfer(spi, write, read, 4))
check(True, f"SPI4 full duplex in mode 3, LSB first, 1 MHz (read {list(read)}: nothing is connected)")
call(lib.endpoint_spidevice_transfer(spi, None, read, 4))
check(True, "read only (sends zeros)")
big = (ctypes.c_uint8 * 5000)()
fails(ARGUMENT, lib.endpoint_spidevice_transfer(spi, big, None, 5000), "5000 bytes with a 4096-byte driver buffer")
call(lib.endpoint_spidevice_dispose(spi))
fails(INVALID_OPERATION, lib.endpoint_spidevice_create(SPI4, 3, 0, 8, 1_000_000, 0, 0, ctypes.byref(ctypes.c_void_p())),
      "chip select line 3 (no /dev/spidev1.3)")
call(lib.endpoint_spi_uninitialize(SPI4))

print("=== PWM on PA0, measured by sampling the pin")
import mmap
memory = os.open("/dev/mem", os.O_RDONLY | os.O_SYNC)
gpioa = mmap.mmap(memory, 4096, mmap.MAP_SHARED, mmap.PROT_READ, offset=0x50002000)


def high_fraction(samples=200000):
    high = 0
    for _ in range(samples):
        high += gpioa[0x10] & 1  # IDR bit 0 = PA0
    return high / samples


fails(ARGUMENT, lib.endpoint_pwm_initialize(PB1), "PB1 has no PWM")
call(lib.endpoint_pwm_initialize(PA0))
check(mode(0x50002000, 0) == 2, "PA0 in alternate mode (TIM5)")
chip, channel = ctypes.c_int(), ctypes.c_int()
call(lib.endpoint_pwm_get_chip_id(PA0, ctypes.byref(chip)))
call(lib.endpoint_pwm_get_channel_id(PA0, ctypes.byref(channel)))
check(channel.value == 0, f"PA0 is pwmchip{chip.value} channel {channel.value}")
pwm = ctypes.c_void_p()
call(lib.endpoint_pwmchannel_create(chip.value, channel.value, 1000, 0.25, ctypes.byref(pwm)))
folder = f"/sys/class/pwm/pwmchip{chip.value}/pwm{channel.value}"
check(open(folder + "/period").read().strip() == "1000000" and open(folder + "/duty_cycle").read().strip() == "250000",
      "1 kHz, 25%: period 1000000 ns, duty 250000 ns")
call(lib.endpoint_pwmchannel_start(pwm))
fraction = high_fraction()
check(abs(fraction - 0.25) < 0.05, f"running: PA0 is high {fraction:.1%} of the time (25%)")
call(lib.endpoint_pwmchannel_set_duty_cycle(pwm, 0.75))
fraction = high_fraction()
check(abs(fraction - 0.75) < 0.05, f"duty 75%: PA0 is high {fraction:.1%} of the time")
call(lib.endpoint_pwmchannel_set_frequency(pwm, 200))
check(open(folder + "/period").read().strip() == "5000000" and open(folder + "/duty_cycle").read().strip() == "3750000",
      "200 Hz keeps 75%: period 5000000, duty 3750000")
call(lib.endpoint_pwmchannel_set_frequency(pwm, 20000))
check(open(folder + "/period").read().strip() == "50000", "20 kHz (a shorter period: the duty is written first)")
fails(ARGUMENT_OUT_OF_RANGE, lib.endpoint_pwmchannel_set_duty_cycle(pwm, 1.5), "duty 1.5")
call(lib.endpoint_pwmchannel_stop(pwm))
fraction = high_fraction(20000)
check(fraction in (0.0, 1.0), f"stopped: PA0 holds still (high {fraction:.0%})")
call(lib.endpoint_pwmchannel_dispose(pwm))
check(not os.path.exists(folder), "disposed: the channel is given back (unexported)")
call(lib.endpoint_pwm_uninitialize(PA0))
check(mode(0x50002000, 0) != 2, "PA0 given back")

print("=== Serial port and CAN setup")
d0, d1 = mode(0x50005000, 0), mode(0x50005000, 1)
call(lib.endpoint_serialport_initialize(b"/dev/ttySTM3", 0))
check(mode(0x50005000, 0) == 2 and mode(0x50005000, 1) == 2, "UART4: PD1 (TX) and PD0 (RX) connected")
fails(INVALID_OPERATION, lib.endpoint_serialport_initialize(b"/dev/ttySTM3", 0) or lib.endpoint_can_initialize(b"can0", 500000),
      "CAN1 shares PD0/PD1 with UART4")
fails(ARGUMENT, lib.endpoint_serialport_initialize(b"/dev/ttySTM3", 1), "UART4 has no flow control pins")
call(lib.endpoint_serialport_uninitialize(b"/dev/ttySTM3"))
check(mode(0x50005000, 0) == d0 and mode(0x50005000, 1) == d1, "UART4 given back")
call(lib.endpoint_can_initialize(b"can0", 500000))
check("state UP" in os.popen("ip link show can0").read(), "CAN1 up at 500 kbit/s")
call(lib.endpoint_can_uninitialize(b"can0"))
check("state DOWN" in os.popen("ip link show can0").read(), "CAN1 stopped")
fails(ARGUMENT, lib.endpoint_can_initialize(b"can7", 500000), "can7")

print()
print("ALL PASSED" if failures == 0 else f"{failures} FAILED")
sys.exit(1 if failures else 0)
