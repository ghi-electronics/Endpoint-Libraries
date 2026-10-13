using System.Runtime.InteropServices;

namespace GHIElectronics.Endpoint.Core.Internal {
    /// <summary>
    /// libendpoint, the board's library that .NET, Python and C++ share for the
    /// features with real logic (storage, USB host). It comes in this package
    /// (runtimes/linux-arm/native). Every function returns 0, or the kind of
    /// error: <see cref="Check"/> throws the matching .NET exception.
    /// </summary>
    internal static unsafe partial class Libendpoint {
        private const string Library = "libendpoint";

        [LibraryImport(Library, EntryPoint = "endpoint_last_error")]
        private static partial byte* LastError();

        public static void Check(int result) {
            if (result == 0) {
                return;
            }
            var message = Marshal.PtrToStringUTF8((nint)LastError()) ?? "";
            throw result switch {
                1 => new ArgumentException(message),
                2 => new ArgumentOutOfRangeException(null, message),
                3 => new InvalidOperationException(message),
                5 => new NotSupportedException(message),
                6 => new ObjectDisposedException(null, message),
                7 => new NotImplementedException(message),
                8 => new TimeoutException(message),
                _ => new IOException(message),
            };
        }

        // SD cards and USB drives: the callback (context, hdc, status, volume label) runs on the library's storage thread

        [LibraryImport(Library, EntryPoint = "endpoint_storage_create", StringMarshalling = StringMarshalling.Utf8)]
        public static partial int StorageCreate(string name, delegate* unmanaged<nint, nint, int, byte*, void> callback,
                                                nint context, out nint controller);

        [LibraryImport(Library, EntryPoint = "endpoint_storage_dispose")]
        public static partial int StorageDispose(nint controller);

        [LibraryImport(Library, EntryPoint = "endpoint_storage_enable")]
        public static partial int StorageEnable(nint controller);

        [LibraryImport(Library, EntryPoint = "endpoint_storage_disable")]
        public static partial int StorageDisable(nint controller);

        [LibraryImport(Library, EntryPoint = "endpoint_storage_mount")]
        public static partial int StorageMount(nint hdc, byte* name, int nameSize);

        [LibraryImport(Library, EntryPoint = "endpoint_storage_flush")]
        public static partial int StorageFlush(nint hdc);

        [LibraryImport(Library, EntryPoint = "endpoint_storage_unmount")]
        public static partial int StorageUnmount(nint hdc);

        // USB host: the callback (context, status, device id, type, vendor id, product id, device name,
        // manufacturer, product, serial number) runs on the library's USB thread

        [LibraryImport(Library, EntryPoint = "endpoint_usbhost_create")]
        public static partial int UsbHostCreate(delegate* unmanaged<nint, int, uint, int, int, int, byte*, byte*, byte*, byte*, void> callback,
                                                nint context, out nint controller);

        [LibraryImport(Library, EntryPoint = "endpoint_usbhost_dispose")]
        public static partial int UsbHostDispose(nint controller);

        [LibraryImport(Library, EntryPoint = "endpoint_usbhost_enable")]
        public static partial int UsbHostEnable(nint controller);

        [LibraryImport(Library, EntryPoint = "endpoint_usbhost_disable")]
        public static partial int UsbHostDisable(nint controller);

        // USB keyboards, mice and joysticks: the callback (context, event, a, b, c, d, e) runs on the device's own thread

        [LibraryImport(Library, EntryPoint = "endpoint_input_open", StringMarshalling = StringMarshalling.Utf8)]
        public static partial int InputOpen(string deviceName, int type, delegate* unmanaged<nint, int, int, int, int, int, int, void> callback,
                                            nint context, out nint device);

        [LibraryImport(Library, EntryPoint = "endpoint_input_close")]
        public static partial int InputClose(nint device);

        [LibraryImport(Library, EntryPoint = "endpoint_input_info")]
        public static partial int InputInfo(nint device, out int vendorId, out int productId, out int connected, out int capabilities);

        [LibraryImport(Library, EntryPoint = "endpoint_input_key_state")]
        public static partial int InputKeyState(nint device, int key, out int state);

        [LibraryImport(Library, EntryPoint = "endpoint_input_mouse_state")]
        public static partial int InputMouseState(nint device, out int x, out int y, out int wheel, out int buttons);

        [LibraryImport(Library, EntryPoint = "endpoint_input_joystick_state")]
        public static partial int InputJoystickState(nint device, int cursor, out int x, out int y, out int hat, out uint buttons);

        [LibraryImport(Library, EntryPoint = "endpoint_input_set_cursor_bounds")]
        public static partial int InputSetCursorBounds(nint device, int cursor, int minX, int maxX, int minY, int maxY);

        [LibraryImport(Library, EntryPoint = "endpoint_input_set_cursor_position")]
        public static partial int InputSetCursorPosition(nint device, int x, int y);

        [LibraryImport(Library, EntryPoint = "endpoint_input_set_scale")]
        public static partial int InputSetScale(nint device, double scale);
    }
}
