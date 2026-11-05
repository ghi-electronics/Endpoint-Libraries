namespace GHIElectronics.Endpoint.Core {
    public static partial class EPM815 {
        /// <summary>
        /// The watchdog bundled with EPM815. Use with the
        /// <c>GHIElectronics.Endpoint.Devices.Watchdog</c> package (same API as TinyCLR).
        /// </summary>
        /// <example>
        /// <code>
        /// var watchdog = WatchdogController.FromName(EPM815.Watchdog.Id);
        /// watchdog.Enable(TimeSpan.FromSeconds(10));
        /// while (true) {
        ///     DoWork();
        ///     watchdog.Reset();
        /// }
        /// </code>
        /// </example>
        public static class Watchdog {
            /// <summary>The watchdog's name, for <c>WatchdogController.FromName</c>.</summary>
            public const string Id = "EPM815.Watchdog";

            /// <summary>Base address of the watchdog block used by applications.</summary>
            internal const ulong Address = 0x5A002000;
        }
    }
}
