namespace GHIElectronics.Endpoint.Core {
    /// <summary>A device connected or disconnected (StorageController, UsbHostController)</summary>
    public enum DeviceConnectionStatus {
        /// <summary>The device has been removed.</summary>
        Disconnected = 0,
        /// <summary>The device is present and ready for use.</summary>
        Connected = 1,

        /// <summary>A drive with no file system the board reads (format it as FAT32 or exFAT)</summary>
        Bad = 2,
    }
}
