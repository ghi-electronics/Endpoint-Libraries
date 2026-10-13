# A virtual HID device for the board tests of HidDevice (the kernel's uhid:
# modprobe uhid), like a program's own USB HID device: vendor-defined 8-byte
# input and output reports and a 4-byte feature report, without report IDs.
# The device answers: an output report comes back as an input report with
# each byte + 1; a feature report that is set is given back when it is read
# (0xAA 0xBB 0xCC 0xDD at first). Run as root on the board, at the same time
# as the test:
#
#     python3 virtual_hid.py [seconds] & <the test>
#
# It is "endpoint test hid" (1234:5678), unplugged after the given time (default 8 s).
import os
import struct
import subprocess
import sys
import threading
import time

UHID_DESTROY, UHID_START, UHID_OPEN, UHID_OUTPUT = 1, 2, 4, 6
UHID_GET_REPORT, UHID_GET_REPORT_REPLY, UHID_CREATE2, UHID_INPUT2 = 9, 10, 11, 12
UHID_SET_REPORT, UHID_SET_REPORT_REPLY = 13, 14
DATA_MAX = 4096
EVENT_SIZE = 4 + 128 + 64 + 64 + 2 + 2 + 4 * 4 + 4096  # struct uhid_event (its largest request: create2)

# Vendor page 0xFF00: 8 bytes in, 8 bytes out, 4 bytes feature
REPORT_DESCRIPTOR = bytes([
    0x06, 0x00, 0xFF, 0x09, 0x01, 0xA1, 0x01,
    0x15, 0x00, 0x26, 0xFF, 0x00, 0x75, 0x08,
    0x95, 0x08, 0x09, 0x01, 0x81, 0x02,
    0x95, 0x08, 0x09, 0x02, 0x91, 0x02,
    0x95, 0x04, 0x09, 0x03, 0xB1, 0x02,
    0xC0,
])


class VirtualHid:
    def __init__(self, name="endpoint test hid"):
        subprocess.run(["modprobe", "uhid"], check=False)
        self.fd = os.open("/dev/uhid", os.O_RDWR)
        self.feature = bytes([0xAA, 0xBB, 0xCC, 0xDD])
        self.outputs = []
        create = struct.pack("<I128s64s64sHHIIII", UHID_CREATE2, name.encode(), b"endpoint-test", b"",
                             len(REPORT_DESCRIPTOR), 0x03, 0x1234, 0x5678, 1, 0) + REPORT_DESCRIPTOR
        os.write(self.fd, create.ljust(EVENT_SIZE, b"\0"))
        self.running = True
        self.thread = threading.Thread(target=self._answer, daemon=True)
        self.thread.start()

    def _send(self, data):
        os.write(self.fd, data.ljust(EVENT_SIZE, b"\0"))

    def _answer(self):
        while self.running:
            try:
                event = os.read(self.fd, EVENT_SIZE)
            except OSError:
                return
            kind = struct.unpack_from("<I", event)[0]
            if kind == UHID_OUTPUT:
                data = event[4:4 + DATA_MAX]
                size = struct.unpack_from("<H", event, 4 + DATA_MAX)[0]
                report = data[:size]
                self.outputs.append(report)
                # Without report IDs the report's bytes follow the 0 byte
                payload = report[1:] if len(report) == 9 else report
                reply = bytes((b + 1) & 0xFF for b in payload)
                self._send(struct.pack("<IH", UHID_INPUT2, len(reply)) + reply)
            elif kind == UHID_GET_REPORT:
                request_id = struct.unpack_from("<I", event, 4)[0]
                data = bytes([0]) + self.feature  # the report ID (none: 0), then the report
                self._send(struct.pack("<IIHH", UHID_GET_REPORT_REPLY, request_id, 0, len(data)) + data)
            elif kind == UHID_SET_REPORT:
                request_id, number, rtype, size = struct.unpack_from("<IBBH", event, 4)
                data = event[4 + 8:4 + 8 + size]
                self.feature = data[1:] if len(data) > 4 else data
                self._send(struct.pack("<IIH", UHID_SET_REPORT_REPLY, request_id, 0))

    def destroy(self):
        self.running = False
        os.write(self.fd, struct.pack("<I", UHID_DESTROY).ljust(EVENT_SIZE, b"\0"))
        os.close(self.fd)


if __name__ == "__main__":
    seconds = float(sys.argv[1]) if len(sys.argv) > 1 else 8
    device = VirtualHid()
    print("virtual HID device: endpoint test hid", flush=True)
    time.sleep(seconds)
    device.destroy()
    print("virtual HID device unplugged; output reports:", [r.hex() for r in device.outputs], flush=True)
