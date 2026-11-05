namespace GHIElectronics.Endpoint.Core {
    public static partial class EPN256 {
        /// <summary>
        /// The watchdog bundled with EPN256. Use with the
        /// <c>GHIElectronics.Endpoint.Devices.Watchdog</c> package (same API as TinyCLR
        /// and <see cref="EPM815.Watchdog"/>).
        /// </summary>
        /// <example>
        /// <code>
        /// var watchdog = WatchdogController.FromName(EPN256.Watchdog.Id);
        /// watchdog.Enable(TimeSpan.FromSeconds(10));
        /// while (true) {
        ///     DoWork();
        ///     watchdog.Reset();
        /// }
        /// </code>
        /// </example>
        public static class Watchdog {
            /// <summary>The watchdog's name, for <c>WatchdogController.FromName</c>.</summary>
            public const string Id = "EPN256.Watchdog";

            /// <summary>MMIO base of the MA35D1 WDT1 block.</summary>
            internal const ulong Address = 0x40440000;
        }
    }
}
