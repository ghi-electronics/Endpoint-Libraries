using System.Collections;
using System.Runtime.CompilerServices;
using GHIElectronics.Endpoint.Core;

[assembly: InternalsVisibleToAttribute("GHIElectronics.Endpoint.Devices.UsbHost")]
namespace GHIElectronics.Endpoint.Devices.Camera {
    /// <summary>Pixel layout of a camera frame.</summary>
    public enum PixelFormat {
        /// <summary>16-bit RGB, 5 bits red, 6 bits green, 5 bits blue, two bytes per pixel.</summary>
        Rgb565 = 0,
        /// <summary>24-bit RGB, 8 bits per channel, three bytes per pixel.</summary>
        Rgb888 = 1,
        /// <summary>JPEG-compressed frame, size varies per frame.</summary>
        Jpeg = 2,
    }

    /// <summary>Resolution, pixel format and frame rate requested from a camera.</summary>
    public class CameraConfiguration {
        /// <summary>Frame width in pixels.</summary>
        public int Width { get; set; }
        /// <summary>Frame height in pixels.</summary>
        public int Height { get; set; }
        /// <summary>Pixel format each frame is delivered in. Defaults to <see cref="PixelFormat.Rgb565"/>.</summary>
        public PixelFormat ImageFormat { get; set; } = PixelFormat.Rgb565;

        /// <summary>Target frame rate in frames per second. Defaults to 15.</summary>
        public int FrameRate { get; set; } = 15;

        internal static string[] GetResolution() {

            var script = new Script("v4l2-ctl", "./", "--list-formats-ext");
            script.Start();
            var resolutions = new ArrayList();

            if (script.Output != null && script.Output.Length > 0) {

                var strReader = new StringReader(script.Output);
                while (true) {

                    var line = strReader.ReadLine();

                    if (line == null) {
                        break;
                    }
                    else {
                        const string CompVal = "\t\tSize: Discrete";

                        if (line.Contains(CompVal)) {
                            var start = line.IndexOf(CompVal);
                            var end = start + CompVal.Length + 1; // +1 space                         


                            line = line.Substring(start + end);
                            if (!resolutions.Contains(line))
                                resolutions.Add(line);
                        }
                    }
                }
            }
            if (resolutions.Count > 0) {
                return (string[])resolutions.ToArray(typeof(string));
            }
            else
                return null;
        }
    }

    /// <summary>Converts a camera frame between pixel formats. Only JPEG input is supported today.</summary>
    public static class ImageConverter {
        /// <summary>Decodes a JPEG frame into the requested pixel format.</summary>
        /// <param name="imageData">Raw JPEG bytes. The buffer may be longer than the actual JPEG; the end-of-image marker (<c>FF D9</c>) is located automatically.</param>
        /// <param name="width">Decoded frame width in pixels.</param>
        /// <param name="height">Decoded frame height in pixels.</param>
        /// <param name="inPixelFormat">Must be <see cref="PixelFormat.Jpeg"/>; other formats throw <see cref="ArgumentException"/>.</param>
        /// <param name="outPixelFormat">Target format: <see cref="PixelFormat.Jpeg"/> returns the trimmed JPEG, <see cref="PixelFormat.Rgb888"/> returns 3 bytes per pixel, <see cref="PixelFormat.Rgb565"/> returns 2 bytes per pixel.</param>
        /// <returns>The converted frame, or <c>null</c> if the input does not start with the JPEG SOI marker (<c>FF D8</c>) or no end-of-image marker is found.</returns>
        public static byte[] Convert(byte[] imageData, int width, int height, PixelFormat inPixelFormat, PixelFormat outPixelFormat) {
            // inBuffer is jpeg format

            if (inPixelFormat != PixelFormat.Jpeg) {
                throw new ArgumentException("Support inPixelFormat jpeg only");
            }

            if (imageData[0] == 0xFF && imageData[1] == 0xD8) {
                //var jpeg_size = imageData.Length - 1;


                //for (; jpeg_size > 2; jpeg_size--) {
                //    if (imageData[jpeg_size] == 0xD9 && imageData[jpeg_size - 1] == 0xFF)
                //        break;
                //}

                var jpeg_size = 2;

                for (; jpeg_size < imageData.Length-2; jpeg_size++) {
                    if (imageData[jpeg_size] == 0xFF && imageData[jpeg_size + 1] == 0xD9) {
                        break;
                    }
                }

                if (jpeg_size > 2) {
                    jpeg_size+=2;

                    var dataJpeg = new byte[jpeg_size];

                    Array.Copy(imageData, 0, dataJpeg, 0, dataJpeg.Length);

                    switch (outPixelFormat) {
                        case PixelFormat.Jpeg:
                            return dataJpeg;

                        case PixelFormat.Rgb888:
                        case PixelFormat.Rgb565:

                            var data888 = new byte[width * height * 3];

                            NativeUtils.JpegtoRGB888(dataJpeg, jpeg_size, data888);
                            if (outPixelFormat == PixelFormat.Rgb888) {
                                return data888;
                            }

                            var data = new byte[width * height * 2];
                            var index = 0;
                            // to 565
                            for (var i = 0; i < data888.Length; i += 3) {
                                var color = (uint)(data888[i + 2] | (data888[i + 1] << 8) | (data888[i + 0] << 16));

                                data[0 + index + 0] = (byte)(((color & 0x00001c00) >> 5) | ((color & 0x000000f8) >> 3));
                                data[0 + index + 1] = (byte)(((color & 0x00f80000) >> 16) | ((color & 0x0000e000) >> 13));

                                index += 2;
                            }

                            return data;
                    }

                }

            }
            return null;


        }
    }
}
