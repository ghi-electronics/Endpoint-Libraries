namespace GHIElectronics.Endpoint.Core {
    public static partial class EPM815 {
        /// <summary>
        /// SD cards and USB drives, for the GHIElectronics.Endpoint.Devices.Storage package:
        /// <code>
        /// var usb = StorageController.FromName(EPM815.StorageController.UsbHostMassStorage);
        /// </code>
        /// </summary>
        public static class StorageController {
            /// <summary>The SD card slot (mounted on /media/sd/)</summary>
            public const string SdCard = "EPM815.StorageController.SdCard";

            /// <summary>The USB drives (mounted on /media/usb0/, usb1, ...: the first free number)</summary>
            public const string UsbHostMassStorage = "EPM815.StorageController.UsbHostMassStorage";
        }
    }

    public static partial class EPN256 {
        /// <summary>
        /// SD cards and USB drives, for the GHIElectronics.Endpoint.Devices.Storage package:
        /// <code>
        /// var usb = StorageController.FromName(EPN256.StorageController.UsbHostMassStorage);
        /// </code>
        /// </summary>
        public static class StorageController {
            /// <summary>The SD card slot (mounted on /media/sd/)</summary>
            public const string SdCard = "EPN256.StorageController.SdCard";

            /// <summary>The USB drives (mounted on /media/usb0/, usb1, ...: the first free number)</summary>
            public const string UsbHostMassStorage = "EPN256.StorageController.UsbHostMassStorage";
        }
    }
}
