using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("GHIElectronics.Endpoint.Native")]
[assembly: InternalsVisibleTo("GHIElectronics.Endpoint.Devices.Can")]
[assembly: InternalsVisibleTo("GHIElectronics.Endpoint.Devices.Storage")]
[assembly: InternalsVisibleTo("GHIElectronics.Endpoint.Devices.Dcmi")]
[assembly: InternalsVisibleTo("GHIElectronics.Endpoint.Devices.Rtc")]
[assembly: InternalsVisibleTo("GHIElectronics.Endpoint.Devices.Adc")]
[assembly: InternalsVisibleTo("GHIElectronics.Endpoint.Devices.Dac")]
[assembly: InternalsVisibleTo("GHIElectronics.Endpoint.Devices.Watchdog")]

namespace GHIElectronics.Endpoint.Core {
    public static partial class EPM815 {
        internal static void ThrowExceptionPinInUsed(int pin) =>
            throw new InvalidOperationException($"Pin {Gpio.Name(pin)} is already in use");

        internal static void ThrowExceptionPinNotInRange(int pin) =>
            throw new ArgumentOutOfRangeException(nameof(pin), pin, "Not an EPM815 pin");
    }
}
