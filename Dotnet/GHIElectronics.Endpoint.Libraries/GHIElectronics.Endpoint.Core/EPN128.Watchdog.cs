namespace GHIElectronics.Endpoint.Core {
    public static partial class EPN128 {
        /// <summary>
        /// The watchdog bundled with EPN128 (MA35D0 Chili Pro). Use with the
        /// <c>GHIElectronics.Endpoint.Devices.Watchdog</c> package (same API
        /// as <see cref="EPN256.Watchdog"/> and <see cref="EPM815.Watchdog"/>).
        /// </summary>
        /// <example>
        /// <code>
        /// var watchdog = WatchdogController.FromName(EPN128.Watchdog.Id);
        /// watchdog.Enable(TimeSpan.FromSeconds(10));
        /// </code>
        /// </example>
        public static class Watchdog {
            /// <summary>The watchdog's name, for <c>WatchdogController.FromName</c>.</summary>
            public const string Id = "EPN128.Watchdog";

            /// <summary>MMIO base of the MA35D0 WDT1 block (same layout as MA35D1).</summary>
            internal const ulong Address = 0x40440000;
        }
    }
}
