using System.Runtime.InteropServices;
using GHIElectronics.Endpoint.Core.Internal;
using Iot.Device.Rtc;

namespace GHIElectronics.Endpoint.Devices.Rtc {
    /// <summary>How the RTC's backup battery or supercapacitor (VBAT) is charged (as in TinyCLR)</summary>
    public enum BatteryChargeMode {
        /// <summary>No charging</summary>
        None = 0,
        /// <summary>Charging through 1.5 kΩ</summary>
        Fast = 1,
        /// <summary>Charging through 5 kΩ</summary>
        Slow = 2,
    }

    /// <summary>
    /// The chip's real-time clock, for .NET IoT's RtcBase (Iot.Device.Rtc).
    /// It counts UTC: at boot, the system time is set from it.
    /// <code>
    /// using var rtc = new RtcController();
    /// if (!rtc.IsValid) {
    ///     rtc.DateTime = DateTimeOffset.UtcNow;   // set it once; a backup battery keeps it
    /// }
    /// Console.WriteLine(rtc.DateTime);
    /// </code>
    /// <see cref="IsValid"/> and <see cref="SetChargeMode"/> are the same as TinyCLR's.
    /// </summary>
    public class RtcController : RtcBase {
        // Linux's RTC calls (linux/rtc.h)
        private const nuint ReadTimeRequest = 0x80247009;  // RTC_RD_TIME
        private const nuint SetTimeRequest = 0x4024700A;   // RTC_SET_TIME

        private const ulong RtcAddress = 0x5C004000;

        // PWR_CR3: VBE switches VBAT charging on, VBRS picks 1.5 kΩ instead of 5 kΩ
        private const ulong PwrCr3 = 0x5000100C;
        private const uint ChargeEnable = 1u << 8, ChargeFast = 1u << 9;

        // struct rtc_time
        [StructLayout(LayoutKind.Sequential)]
        private struct RtcTime {
            public int Second, Minute, Hour, Day, Month, Year, WeekDay, YearDay, DaylightSaving;
        }

        private readonly string device;
        private bool disposed;

        /// <summary>The chip's RTC</summary>
        public RtcController() {
            var number = EndpointDevices.Find("rtc", "rtc", RtcAddress);
            if (number < 0) {
                throw new InvalidOperationException("The RTC is not enabled in this firmware: update the board's firmware");
            }
            this.device = $"/dev/rtc{number}";
            this.LocalTimeZone = TimeZoneInfo.Utc;
        }

        /// <summary>
        /// Whether the RTC has been set since it last lost power. Without
        /// power it starts again at 2000-01-01.
        /// </summary>
        public bool IsValid => this.Read().Year > 100;  // years since 1900

        /// <summary>Charges the backup battery or supercapacitor on VBAT, or stops charging</summary>
        public void SetChargeMode(BatteryChargeMode chargeMode) {
            var bits = chargeMode switch {
                BatteryChargeMode.None => 0u,
                BatteryChargeMode.Fast => ChargeEnable | ChargeFast,
                BatteryChargeMode.Slow => ChargeEnable,
                _ => throw new ArgumentOutOfRangeException(nameof(chargeMode), chargeMode, null),
            };
            PhysicalMemory.Modify(PwrCr3, ChargeEnable | ChargeFast, bits);
        }

        // TODO: backup memory (TinyCLR API) in the chip's TAMP backup registers
        // (32 x 4 bytes). Check first which registers the boot loader uses.

        /// <summary>Not implemented yet: the size of the backup memory in bytes</summary>
        public uint BackupMemorySize => throw NotImplemented();

        /// <summary>Not implemented yet</summary>
        public void WriteBackupMemory(byte[] sourceData) => throw NotImplemented();

        /// <summary>Not implemented yet</summary>
        public void WriteBackupMemory(byte[] sourceData, uint destinationOffset) => throw NotImplemented();

        /// <summary>Not implemented yet</summary>
        public void WriteBackupMemory(byte[] sourceData, uint sourceOffset, uint destinationOffset, int count) => throw NotImplemented();

        /// <summary>Not implemented yet</summary>
        public int ReadBackupMemory(byte[] destinationData) => throw NotImplemented();

        /// <summary>Not implemented yet</summary>
        public int ReadBackupMemory(byte[] destinationData, uint sourceOffset) => throw NotImplemented();

        /// <summary>Not implemented yet</summary>
        public int ReadBackupMemory(byte[] destinationData, uint destinationOffset, uint sourceOffset, int count) => throw NotImplemented();

        // TODO: Calibrate (TinyCLR API) with the RTC's smooth calibration (RTC_CALR)

        /// <summary>Not implemented yet</summary>
        public void Calibrate(int pulse) => throw NotImplemented();

        private static NotImplementedException NotImplemented() => new("Not implemented");

        /// <inheritdoc/>
        protected override DateTime ReadTime() {
            var t = this.Read();
            return new DateTime(t.Year + 1900, t.Month + 1, t.Day, t.Hour, t.Minute, t.Second);
        }

        /// <inheritdoc/>
        protected override unsafe void SetTime(DateTime time) {
            if (time.Kind == DateTimeKind.Local) {
                time = time.ToUniversalTime();
            }
            if (time.Year is < 2000 or > 2099) {
                throw new ArgumentOutOfRangeException(nameof(time), time, "The RTC counts from 2000 to 2099");
            }

            var t = new RtcTime {
                Second = time.Second, Minute = time.Minute, Hour = time.Hour,
                Day = time.Day, Month = time.Month - 1, Year = time.Year - 1900,
                WeekDay = (int)time.DayOfWeek, YearDay = time.DayOfYear - 1,
            };
            this.Call(SetTimeRequest, (nint)(&t), "set");
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing) {
            this.disposed = true;
            base.Dispose(disposing);
        }

        private unsafe RtcTime Read() {
            RtcTime t;
            this.Call(ReadTimeRequest, (nint)(&t), "read");
            return t;
        }

        // Linux lets one program at a time open the RTC: open it only for
        // each call, and wait a little if another program has it
        private void Call(nuint request, nint argument, string what) {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            for (var attempt = 0; ; attempt++) {
                var fd = Libc.Open(this.device, Libc.O_RDONLY | Libc.O_CLOEXEC);
                if (fd >= 0) {
                    try {
                        if (Libc.Ioctl(fd, request, argument) < 0) {
                            throw Libc.Error($"Cannot {what} the RTC");
                        }
                        return;
                    }
                    finally {
                        Libc.Close(fd);
                    }
                }
                if (Marshal.GetLastPInvokeError() != Libc.EBUSY || attempt == 100) {
                    throw Libc.Error($"Cannot open {this.device}");
                }
                Thread.Sleep(10);
            }
        }
    }
}
