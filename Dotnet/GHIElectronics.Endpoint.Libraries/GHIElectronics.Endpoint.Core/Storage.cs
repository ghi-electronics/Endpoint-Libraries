namespace GHIElectronics.Endpoint.Core {
    public static partial class EPM815 {
        /// <summary>
        /// SD cards and USB drives on EPM815. Use with the
        /// <c>GHIElectronics.Endpoint.Devices.Storage</c> package.
        /// </summary>
        /// <example>
        /// <code>
        /// var usb = StorageController.FromName(EPM815.StorageController.UsbHostMassStorage);
        /// </code>
        /// </example>
        public static class StorageController {
            /// <summary>The SD card slot (mounted on <c>/media/sd/</c>).</summary>
            public const string SdCard = "EPM815.StorageController.SdCard";

            /// <summary>The USB drives (mounted on <c>/media/usb0/</c>, <c>usb1</c>, ...; the first free number).</summary>
            public const string UsbHostMassStorage = "EPM815.StorageController.UsbHostMassStorage";
        }
    }

    public static partial class EPN256 {
        /// <summary>
        /// SD cards and USB drives on EPN256. Use with the
        /// <c>GHIElectronics.Endpoint.Devices.Storage</c> package.
        /// </summary>
        /// <example>
        /// <code>
        /// var usb = StorageController.FromName(EPN256.StorageController.UsbHostMassStorage);
        /// </code>
        /// </example>
        public static class StorageController {
            /// <summary>The SD card slot (mounted on <c>/media/sd/</c>).</summary>
            public const string SdCard = "EPN256.StorageController.SdCard";

            /// <summary>The USB drives (mounted on <c>/media/usb0/</c>, <c>usb1</c>, ...; the first free number).</summary>
            public const string UsbHostMassStorage = "EPN256.StorageController.UsbHostMassStorage";
        }
    }
}
