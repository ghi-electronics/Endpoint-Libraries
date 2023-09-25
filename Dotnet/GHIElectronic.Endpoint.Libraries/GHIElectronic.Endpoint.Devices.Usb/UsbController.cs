namespace GHIElectronic.Endpoint.Devices.Usb
{
    public enum DeviceType : byte
    {

        /// <summary>The device is not recognized.</summary>
        Unknown,

        /// <summary>USB Hub.</summary>
        Hub,

        /// <summary>Human Interface Device.</summary>
        HID,

        /// <summary>Mouse.</summary>
        Mouse,

        /// <summary>Keyboard.</summary>
        Keyboard,

        /// <summary>Joystick.</summary>
        Joystick,

        /// <summary>Mass Storage. This includes USB storage devices such as USB Thumbs drives and USB hard disks.</summary>
        MassStorage,

        /// <summary>Printer.</summary>
        [Obsolete()]
        Printer,

        /// <summary>USB to Serial device.</summary>
        SerialFTDI,

        /// <summary>USB to Serial device.</summary>
        [Obsolete()]
        SerialProlific,

        /// <summary>USB to Serial device.</summary>
        [Obsolete()]
        SerialProlific2,

        /// <summary>USB to Serial device.</summary>
        [Obsolete()]
        SerialSiLabs,

        /// <summary>USB to Serial device.</summary>
        SerialCDC,

        /// <summary>USB to Serial device.</summary>
        [Obsolete()]
        SerialSierraC885,

        /// <summary>Sierra Installer.</summary>
        [Obsolete()]
        SierraInstaller,

        /// <summary>Video device.</summary>
        //Video,

        /// <summary>Webcamera.</summary>
        //Webcam,
    }
    public class UsbController
    {

    }
}
