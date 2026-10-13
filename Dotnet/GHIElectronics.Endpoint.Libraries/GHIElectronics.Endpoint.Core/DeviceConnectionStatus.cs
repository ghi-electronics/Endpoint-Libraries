namespace GHIElectronics.Endpoint.Core {
    /// <summary>A device connected or disconnected (StorageController, UsbHostController)</summary>
    public enum DeviceConnectionStatus {
        Disconnected = 0,
        Connected = 1,

        /// <summary>A drive with no file system the board reads (format it as FAT32 or exFAT)</summary>
        Bad = 2,
    }
}
