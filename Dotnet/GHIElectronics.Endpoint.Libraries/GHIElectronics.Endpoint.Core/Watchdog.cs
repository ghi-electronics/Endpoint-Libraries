namespace GHIElectronics.Endpoint.Core {
    public static partial class EPM815 {
        /// <summary>
        /// The watchdog, for the GHIElectronics.Endpoint.Devices.Watchdog
        /// package (the same API as TinyCLR):
        /// <code>
        /// var watchdog = WatchdogController.FromName(EPM815.Watchdog.Id);
        /// watchdog.Enable(TimeSpan.FromSeconds(10));
        /// while (true) { DoWork(); watchdog.Reset(); }
        /// </code>
        /// </summary>
        public static class Watchdog {
            /// <summary>The watchdog's name, for WatchdogController.FromName</summary>
            public const string Id = "EPM815.Watchdog";

            /// <summary>IWDG2, the watchdog Linux gives to programs</summary>
            internal const ulong Address = 0x5A002000;
        }
    }
}
