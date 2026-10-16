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

        // A program's own USB devices (RawDevice, HidDevice): the callback (context) runs once, on the
        // device's own thread, when it is unplugged

        [LibraryImport(Library, EntryPoint = "endpoint_usbraw_open", StringMarshalling = StringMarshalling.Utf8)]
        public static partial int UsbRawOpen(string deviceName, delegate* unmanaged<nint, void> unplugged, nint context, out nint device);

        [LibraryImport(Library, EntryPoint = "endpoint_usbraw_close")]
        public static partial int UsbRawClose(nint device);

        [LibraryImport(Library, EntryPoint = "endpoint_usbraw_info")]
        public static partial int UsbRawInfo(nint device, out int vendorId, out int productId, out int connected);

        [LibraryImport(Library, EntryPoint = "endpoint_usbraw_control")]
        public static partial int UsbRawControl(nint device, int requestType, int request, int value, int index, byte* data, int length,
                                                int timeoutMs, out int transferred);

        [LibraryImport(Library, EntryPoint = "endpoint_usbraw_transfer")]
        public static partial int UsbRawTransfer(nint device, int endpoint, byte* data, int length, int timeoutMs, out int transferred);

        [LibraryImport(Library, EntryPoint = "endpoint_hid_open", StringMarshalling = StringMarshalling.Utf8)]
        public static partial int HidOpen(string deviceName, delegate* unmanaged<nint, void> unplugged, nint context, out nint device);

        [LibraryImport(Library, EntryPoint = "endpoint_hid_close")]
        public static partial int HidClose(nint device);

        [LibraryImport(Library, EntryPoint = "endpoint_hid_info")]
        public static partial int HidInfo(nint device, out int vendorId, out int productId, out int connected, byte* name, int nameSize);

        [LibraryImport(Library, EntryPoint = "endpoint_hid_report_descriptor")]
        public static partial int HidReportDescriptor(nint device, byte* buffer, int size, out int length);

        [LibraryImport(Library, EntryPoint = "endpoint_hid_write")]
        public static partial int HidWrite(nint device, byte* data, int length, out int written);

        [LibraryImport(Library, EntryPoint = "endpoint_hid_read")]
        public static partial int HidRead(nint device, byte* buffer, int size, int timeoutMs, out int length);

        [LibraryImport(Library, EntryPoint = "endpoint_hid_get_feature")]
        public static partial int HidGetFeature(nint device, byte* buffer, int size, out int length);

        [LibraryImport(Library, EntryPoint = "endpoint_hid_send_feature")]
        public static partial int HidSendFeature(nint device, byte* data, int length);

        [LibraryImport(Library, EntryPoint = "endpoint_printer_open", StringMarshalling = StringMarshalling.Utf8)]
        public static partial int PrinterOpen(string deviceName, delegate* unmanaged<nint, void> unplugged, nint context, out nint device);

        [LibraryImport(Library, EntryPoint = "endpoint_printer_close")]
        public static partial int PrinterClose(nint device);

        [LibraryImport(Library, EntryPoint = "endpoint_printer_info")]
        public static partial int PrinterInfo(nint device, out int vendorId, out int productId, out int connected);

        [LibraryImport(Library, EntryPoint = "endpoint_printer_device_id")]
        public static partial int PrinterDeviceId(nint device, byte* text, int size);

        [LibraryImport(Library, EntryPoint = "endpoint_printer_status")]
        public static partial int PrinterStatus(nint device, out int status);

        [LibraryImport(Library, EntryPoint = "endpoint_printer_write")]
        public static partial int PrinterWrite(nint device, byte* data, int length, int timeoutMs, out int written);

        [LibraryImport(Library, EntryPoint = "endpoint_printer_read")]
        public static partial int PrinterRead(nint device, byte* buffer, int size, int timeoutMs, out int length);

        // MP3 decoding, for PlayMp3 (GHIElectronics.Endpoint.Devices.Media)

        [LibraryImport(Library, EntryPoint = "endpoint_mp3_open", StringMarshalling = StringMarshalling.Utf8)]
        public static partial int Mp3Open(string? path, byte* data, int length, out nint decoder, out int sampleRate, out int channels, out long samples);

        [LibraryImport(Library, EntryPoint = "endpoint_mp3_read")]
        public static partial int Mp3Read(nint decoder, short* samples, int count, out int got);

        [LibraryImport(Library, EntryPoint = "endpoint_mp3_close")]
        public static partial int Mp3Close(nint decoder);

        // Webcams (Camera, GHIElectronics.Endpoint.Devices.Media): frame (context, jpeg, length, width, height) and
        // unplugged (context) run on the camera's own threads

        [LibraryImport(Library, EntryPoint = "endpoint_camera_open", StringMarshalling = StringMarshalling.Utf8)]
        public static partial int CameraOpen(string deviceName, delegate* unmanaged<nint, byte*, int, int, int, void> frame,
                                             delegate* unmanaged<nint, void> unplugged, nint context, out nint camera);

        [LibraryImport(Library, EntryPoint = "endpoint_camera_close")]
        public static partial int CameraClose(nint camera);

        [LibraryImport(Library, EntryPoint = "endpoint_camera_info")]
        public static partial int CameraInfo(nint camera, byte* name, int nameSize, out int connected, out int formats);

        [LibraryImport(Library, EntryPoint = "endpoint_camera_format")]
        public static partial int CameraFormat(nint camera, int index, out int width, out int height, out int pixelFormat, out int maxFrameRate);

        [LibraryImport(Library, EntryPoint = "endpoint_camera_get_resolution")]
        public static partial int CameraGetResolution(nint camera, out int width, out int height, out int pixelFormat, out int frameRate);

        [LibraryImport(Library, EntryPoint = "endpoint_camera_set_resolution")]
        public static partial int CameraSetResolution(nint camera, int width, int height, int frameRate, int pixelFormat);

        [LibraryImport(Library, EntryPoint = "endpoint_camera_get_quality")]
        public static partial int CameraGetQuality(nint camera, out int quality);

        [LibraryImport(Library, EntryPoint = "endpoint_camera_set_quality")]
        public static partial int CameraSetQuality(nint camera, int quality);

        [LibraryImport(Library, EntryPoint = "endpoint_camera_capture")]
        public static partial int CameraCapture(nint camera, int timeoutMs, out byte* jpeg, out int length);

        [LibraryImport(Library, EntryPoint = "endpoint_camera_free")]
        public static partial int CameraFree(byte* jpeg);

        [LibraryImport(Library, EntryPoint = "endpoint_camera_start_streaming")]
        public static partial int CameraStartStreaming(nint camera);

        [LibraryImport(Library, EntryPoint = "endpoint_camera_stop_streaming")]
        public static partial int CameraStopStreaming(nint camera);

        [LibraryImport(Library, EntryPoint = "endpoint_camera_get_control")]
        public static partial int CameraGetControl(nint camera, int control, out int supported, out int minimum, out int maximum,
                                                   out int step, out int defaultValue, out int value);

        [LibraryImport(Library, EntryPoint = "endpoint_camera_set_control")]
        public static partial int CameraSetControl(nint camera, int control, int value);

        // NetworkController. type is 0=Ethernet, 1=WiFi, 2=PcShared.
        // Enable reads /config/network.json and applies synchronously in the
        // caller's process: ip link up, udhcpc, wpa_supplicant, etc. Errors
        // come back through the normal return code / endpoint_last_error.

        [LibraryImport(Library, EntryPoint = "endpoint_network_get_interface_name")]
        public static partial int NetworkGetInterfaceName(int type, byte* buffer, int size);

        [LibraryImport(Library, EntryPoint = "endpoint_network_enable")]
        public static partial int NetworkEnable(int type);

        [LibraryImport(Library, EntryPoint = "endpoint_network_disable")]
        public static partial int NetworkDisable(int type);

        [LibraryImport(Library, EntryPoint = "endpoint_network_is_enabled")]
        public static partial int NetworkIsEnabled(int type, out int result);

        [LibraryImport(Library, EntryPoint = "endpoint_network_is_connected")]
        public static partial int NetworkIsConnected(int type, out int result);

        // DisplayController. One display per board, chosen by "type" in
        // /config/display.json (none, nativeLcd, spi, i2c). Enable reads the
        // JSON and applies synchronously in the caller's process (modprobe
        // panel-simple + gpioset backlight for nativeLcd).

        [LibraryImport(Library, EntryPoint = "endpoint_display_enable")]
        public static partial int DisplayEnable();

        [LibraryImport(Library, EntryPoint = "endpoint_display_disable")]
        public static partial int DisplayDisable();

        [LibraryImport(Library, EntryPoint = "endpoint_display_is_enabled")]
        public static partial int DisplayIsEnabled(out int result);

        // Touchscreens. Callback runs on the device's own thread:
        //   (context, eventType 0=Pressed/1=Released/2=Moved, pointCount, ids, xs, ys)

        [LibraryImport(Library, EntryPoint = "endpoint_touch_find_default")]
        public static partial int TouchFindDefault(byte* buffer, int size);

        [LibraryImport(Library, EntryPoint = "endpoint_touch_open", StringMarshalling = StringMarshalling.Utf8)]
        public static partial int TouchOpen(string deviceName,
                                            delegate* unmanaged<nint, int, int, int*, int*, int*, void> callback,
                                            nint context, out nint device);

        [LibraryImport(Library, EntryPoint = "endpoint_touch_close")]
        public static partial int TouchClose(nint device);

        [LibraryImport(Library, EntryPoint = "endpoint_touch_info")]
        public static partial int TouchInfo(nint device, out int vendorId, out int productId,
                                            out int connected, out int width, out int height);
    }
}
