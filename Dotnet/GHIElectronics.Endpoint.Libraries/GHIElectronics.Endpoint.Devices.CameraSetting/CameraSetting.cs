using System.Collections;
using System.Runtime.CompilerServices;
using GHIElectronics.Endpoint.Core;

[assembly: InternalsVisibleToAttribute("GHIElectronics.Endpoint.Devices.Dcmi")]
[assembly: InternalsVisibleToAttribute("GHIElectronics.Endpoint.Devices.UsbHost")]
namespace GHIElectronics.Endpoint.Devices.CameraSetting {
    public enum Format {
        Rgb565 = 0,
        Rgb888 = 1,
        Jpeg = 2,
    }

    public class CameraConfiguration {
        public int Width { get; set; }
        public int Height { get; set; }        
        public Format ImageFormat { get; set; } = Format.Rgb565;

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

    public static class ConvertImage {
        public static byte[] Convert(byte[] imageData, int width, int height, Format inPixelFormat, Format outPixelFormat) {
            // inBuffer is jpeg format

            if (inPixelFormat != Format.Jpeg) {
                throw new ArgumentException("Support inPixelFormat jpeg only");
            }

            if (imageData[0] == 0xFF && imageData[1] == 0xD8) {
                var jpeg_size = imageData.Length - 1;


                for (; jpeg_size > 2; jpeg_size--) {
                    if (imageData[jpeg_size] == 0xD9 && imageData[jpeg_size - 1] == 0xFF)
                        break;
                }

                if (jpeg_size > 2) {
                    jpeg_size++;

                    var dataJpeg = new byte[jpeg_size];

                    Array.Copy(imageData, 0, dataJpeg, 0, dataJpeg.Length);

                    switch (outPixelFormat) {
                        case Format.Jpeg:
                            return dataJpeg;

                        case Format.Rgb888:
                        case Format.Rgb565:

                            var data888 = new byte[width * height * 3];

                            NativeUtils.JpegtoRGB888(dataJpeg, jpeg_size, data888);
                            if (outPixelFormat == Format.Rgb888) {
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
