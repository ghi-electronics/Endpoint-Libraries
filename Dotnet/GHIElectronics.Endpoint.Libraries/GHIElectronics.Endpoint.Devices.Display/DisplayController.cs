#nullable enable
// System.Drawing.Imaging.PixelFormat lives in System.Drawing.Common and is
// annotated as Windows-only, but we only read the enum values (ints) - we
// never touch the GDI+ Bitmap types that actually need Windows. Silence the
// lint for the whole file.
#pragma warning disable CA1416
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Devices.Display {
    using PixelFormat = System.Drawing.Imaging.PixelFormat;

    /// <summary>
    /// A display on the board. This class is abstract: the library ships one
    /// concrete subclass, <see cref="ParallelDisplayController"/>, for the
    /// SoC's parallel RGB panel into <c>/dev/fb0</c>. Customers wiring any
    /// other kind of panel (SPI, I2C, custom) subclass this directly and
    /// drive the hardware through the Core Spi/I2c/Gpio APIs. The abstract
    /// contract is: open the panel on <see cref="Enable"/>, close it on
    /// <see cref="Disable"/>, push pixels on <see cref="Flush(ReadOnlySpan{byte})"/>.
    ///
    /// <code>
    /// using var display = DisplayController.GetDefault();
    /// if (display != null) {
    ///     display.Enable();
    ///     display.ShowLogo();                              // optional
    ///     display.Flush(bitmapBytes, PixelFormat.Format32bppArgb);
    /// }
    /// </code>
    /// </summary>
    public abstract class DisplayController : IDisposable {
        private const string ConfigPath = "/config/display.json";

        /// <summary>Visible width of the panel in pixels.</summary>
        public abstract int Width { get; }

        /// <summary>Visible height of the panel in pixels.</summary>
        public abstract int Height { get; }

        /// <summary>True once the panel driver / init path has run and the panel is ready to receive pixels.</summary>
        public abstract bool IsEnabled { get; }

        /// <summary>
        /// Loads the panel driver / runs the init sequence. Throws if the
        /// apply fails.
        /// </summary>
        public abstract void Enable();

        /// <summary>Reverse of <see cref="Enable"/>. Safe to call when nothing is enabled.</summary>
        public abstract void Disable();

        /// <summary>
        /// Push a full-screen frame. Pixel format defaults to
        /// <see cref="PixelFormat.Format16bppRgb565"/>.
        /// </summary>
        public void Flush(ReadOnlySpan<byte> pixels) =>
            Flush(pixels, PixelFormat.Format16bppRgb565);

        /// <summary>Push a full-screen frame in the given format.</summary>
        public void Flush(ReadOnlySpan<byte> pixels, PixelFormat format) =>
            Flush(pixels, format, 0, 0, Width, Height);

        /// <summary>Push a region of the panel. The subclass handles any pixel-format conversion.</summary>
        public abstract void Flush(ReadOnlySpan<byte> pixels, PixelFormat format,
                                    int x, int y, int width, int height);

        /// <summary>
        /// Reads the <c>logo</c> filename from <c>/config/display.json</c>,
        /// decodes the 24-bit BMP file from <c>/config</c>, and calls
        /// <see cref="Flush(ReadOnlySpan{byte},PixelFormat,int,int,int,int)"/>
        /// with it. No-op if the file is missing or not a 24-bit uncompressed BMP.
        /// Virtual so a subclass can replace the behaviour (e.g. pre-convert
        /// to the panel's native format once instead of on every call).
        /// </summary>
        public virtual void ShowLogo() {
            string? name = GetSettings().Logo;
            if (string.IsNullOrEmpty(name)) return;
            if (!BmpReader.TryRead24bppBgr(Path.Combine("/config", name),
                                           out byte[] pixels, out int w, out int h)) return;
            Flush(pixels, PixelFormat.Format24bppRgb, 0, 0, Math.Min(w, Width), Math.Min(h, Height));
        }

        public abstract void Dispose();

        /// <summary>
        /// Reads the built-in display's settings from <c>/config/display.json</c>.
        /// Changes to the returned object do nothing until passed back to
        /// <see cref="SetSettings"/>.
        /// </summary>
        public static DisplaySettings GetSettings() {
            var config = LoadConfig();
            return new DisplaySettings {
                Type = ParseType(config["type"]?.GetValue<string>()),
                EnableAtBoot = config["enableAtBoot"]?.GetValue<bool>() ?? false,
                Logo = config["logo"]?.GetValue<string>(),
                Backlight = ReadBacklight(config["backlight"]?.AsObject()),
                Parallel = ReadParallel(config["parallel"]?.AsObject()),
            };
        }

        /// <summary>
        /// Writes the settings to <c>/config/display.json</c> (persisted
        /// across reboots). Does NOT apply them to the live panel - call
        /// <see cref="Enable"/> for that.
        /// </summary>
        public static void SetSettings(DisplaySettings settings) {
            ArgumentNullException.ThrowIfNull(settings);

            var config = LoadConfig();
            config["version"] = 1;
            config["enableAtBoot"] = settings.EnableAtBoot;
            config["type"] = TypeToString(settings.Type);
            config["logo"] = settings.Logo;
            config["backlight"] = WriteBacklight(settings.Backlight);
            config["parallel"] = WriteParallel(settings.Parallel);

            // Drop keys from older firmware schemas so the file stays tidy.
            config.Remove("nativeLcd");
            config.Remove("spi");
            config.Remove("i2c");
            config.Remove("touch");

            SaveConfig(config);
        }

        /// <summary>
        /// The built-in display controller for this board, or null if
        /// <c>/config/display.json</c> says the type is <see cref="DisplayType.None"/>.
        /// Customers using a custom (SPI/I2C/other) panel instantiate their
        /// own subclass of <see cref="DisplayController"/> directly instead.
        /// </summary>
        public static DisplayController? GetDefault() {
            var s = GetSettings();
            return s.Type == DisplayType.Parallel ? new ParallelDisplayController() : null;
        }

        // ------- helpers -------

        private static JsonObject LoadConfig() {
            if (!File.Exists(ConfigPath)) {
                return new JsonObject { ["version"] = 1 };
            }
            using var stream = File.OpenRead(ConfigPath);
            var node = JsonNode.Parse(stream);
            return node?.AsObject() ?? new JsonObject { ["version"] = 1 };
        }

        private static void SaveConfig(JsonObject config) {
            var tmp = ConfigPath + ".tmp";
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(tmp, config.ToJsonString(options));
            File.Move(tmp, ConfigPath, overwrite: true);
        }

        private static DisplayType ParseType(string? s) => s?.ToLowerInvariant() switch {
            "parallel" => DisplayType.Parallel,
            _ => DisplayType.None,
        };

        private static string TypeToString(DisplayType t) => t switch {
            DisplayType.Parallel => "parallel",
            _ => "none",
        };

        private static BacklightSettings? ReadBacklight(JsonObject? o) {
            if (o == null) return null;
            var chip = o["gpioChip"]?.GetValue<string>();
            var pin = o["gpioPin"]?.GetValue<int?>();
            if (chip == null && pin == null) return null;
            return new BacklightSettings {
                GpioChip = chip,
                GpioPin = pin,
                ActivePolarity = ParsePolarity(o["activePolarity"]?.GetValue<string>()),
            };
        }

        private static JsonObject WriteBacklight(BacklightSettings? b) =>
            new() {
                ["gpioChip"] = b?.GpioChip,
                ["gpioPin"] = b?.GpioPin,
                ["activePolarity"] = PolarityToString(b?.ActivePolarity ?? SignalPolarity.ActiveHigh),
            };

        private static SignalPolarity ParsePolarity(string? s) =>
            s?.ToLowerInvariant() == "activelow" ? SignalPolarity.ActiveLow : SignalPolarity.ActiveHigh;

        private static string PolarityToString(SignalPolarity p) =>
            p == SignalPolarity.ActiveLow ? "activeLow" : "activeHigh";

        private static ParallelLcdSettings? ReadParallel(JsonObject? o) {
            if (o == null) return null;
            return new ParallelLcdSettings {
                Width = o["width"]?.GetValue<int>() ?? 0,
                Height = o["height"]?.GetValue<int>() ?? 0,
                PixelClockHz = o["pixelClockHz"]?.GetValue<int>() ?? 0,
                HFrontPorch = o["hFrontPorch"]?.GetValue<int>() ?? 0,
                HBackPorch = o["hBackPorch"]?.GetValue<int>() ?? 0,
                HSync = o["hSync"]?.GetValue<int>() ?? 0,
                VFrontPorch = o["vFrontPorch"]?.GetValue<int>() ?? 0,
                VBackPorch = o["vBackPorch"]?.GetValue<int>() ?? 0,
                VSync = o["vSync"]?.GetValue<int>() ?? 0,
                BitsPerPixel = o["bitsPerPixel"]?.GetValue<int>() ?? 16,
                DataEnablePolarity = (o["deActiveLow"]?.GetValue<bool>() ?? false)
                    ? SignalPolarity.ActiveLow : SignalPolarity.ActiveHigh,
                PixelClockEdge = (o["pixelClockFalling"]?.GetValue<bool>() ?? false)
                    ? ClockEdge.Falling : ClockEdge.Rising,
            };
        }

        private static JsonObject WriteParallel(ParallelLcdSettings? p) {
            p ??= new ParallelLcdSettings();
            return new JsonObject {
                ["width"] = p.Width,
                ["height"] = p.Height,
                ["pixelClockHz"] = p.PixelClockHz,
                ["hFrontPorch"] = p.HFrontPorch,
                ["hBackPorch"] = p.HBackPorch,
                ["hSync"] = p.HSync,
                ["vFrontPorch"] = p.VFrontPorch,
                ["vBackPorch"] = p.VBackPorch,
                ["vSync"] = p.VSync,
                ["bitsPerPixel"] = p.BitsPerPixel,
                ["deActiveLow"] = p.DataEnablePolarity == SignalPolarity.ActiveLow,
                ["pixelClockFalling"] = p.PixelClockEdge == ClockEdge.Falling,
            };
        }
    }

    /// <summary>
    /// The built-in display driven by the SoC's parallel RGB LCD controller
    /// (DRM "DPI" / parallel-RGB). Opens <c>/dev/fb0</c> inside
    /// <see cref="Enable"/>, respects the real framebuffer stride, converts
    /// source pixel formats to the panel's bit depth, and blits with
    /// <see cref="Flush(System.ReadOnlySpan{byte},System.Drawing.Imaging.PixelFormat,int,int,int,int)"/>.
    /// </summary>
    public sealed class ParallelDisplayController : DisplayController {
        private FileStream? framebuffer;
        private int stride;
        private int targetBpp = 16;
        private int width;
        private int height;

        public override int Width => this.width == 0
            ? GetSettings().Parallel?.Width ?? 0 : this.width;
        public override int Height => this.height == 0
            ? GetSettings().Parallel?.Height ?? 0 : this.height;

        public override bool IsEnabled {
            get {
                Libendpoint.Check(Libendpoint.DisplayIsEnabled(out int r));
                return r != 0;
            }
        }

        public override void Enable() {
            Libendpoint.Check(Libendpoint.DisplayEnable());
            var p = GetSettings().Parallel ?? new ParallelLcdSettings();
            this.width = p.Width;
            this.height = p.Height;
            this.targetBpp = p.BitsPerPixel == 0 ? 16 : p.BitsPerPixel;
            this.framebuffer = new FileStream("/dev/fb0", FileMode.Open,
                                              FileAccess.Write, FileShare.ReadWrite);
            // MA35D1 DCUltra pins fb0 at 32 bpp BGRX8888 regardless of what
            // display.json's BitsPerPixel says; trust the kernel.
            try {
                var b = File.ReadAllText("/sys/class/graphics/fb0/bits_per_pixel").Trim();
                if (int.TryParse(b, out int parsedBpp) && parsedBpp > 0) this.targetBpp = parsedBpp;
            } catch { /* keep the configured value */ }
            this.stride = this.width * (this.targetBpp / 8);
            try {
                var s = File.ReadAllText("/sys/class/graphics/fb0/stride").Trim();
                if (int.TryParse(s, out int parsed) && parsed > 0) this.stride = parsed;
            } catch { /* keep the fallback */ }
        }

        public override void Disable() {
            this.framebuffer?.Dispose();
            this.framebuffer = null;
            Libendpoint.Check(Libendpoint.DisplayDisable());
        }

        public override void Flush(ReadOnlySpan<byte> pixels, PixelFormat format,
                                    int x, int y, int width, int height) {
            if (this.framebuffer == null) {
                throw new InvalidOperationException("Flush called before Enable");
            }
            if (x < 0 || y < 0 || width <= 0 || height <= 0
                || x + width > this.width || y + height > this.height) {
                throw new ArgumentException("Flush region outside the panel");
            }
            int targetBytesPerPixel = this.targetBpp / 8;
            int sourceStride = format switch {
                PixelFormat.Format16bppRgb565 => width * 2,
                PixelFormat.Format24bppRgb    => width * 3,
                PixelFormat.Format32bppRgb or PixelFormat.Format32bppArgb => width * 4,
                _ => throw new NotSupportedException($"Unsupported source pixel format: {format}"),
            };
            if (pixels.Length < sourceStride * height) {
                throw new ArgumentException("Pixel buffer is smaller than the region");
            }

            Span<byte> row = stackalloc byte[0];
            byte[]? rowRented = null;
            int rowBytes = width * targetBytesPerPixel;
            if (rowBytes <= 8192) {
                row = stackalloc byte[rowBytes];
            } else {
                rowRented = System.Buffers.ArrayPool<byte>.Shared.Rent(rowBytes);
                row = rowRented.AsSpan(0, rowBytes);
            }
            try {
                for (int r = 0; r < height; r++) {
                    var src = pixels.Slice(r * sourceStride, sourceStride);
                    ConvertRow(src, row, format, this.targetBpp, width);
                    this.framebuffer.Seek((y + r) * this.stride + x * targetBytesPerPixel,
                                          SeekOrigin.Begin);
                    this.framebuffer.Write(row);
                }
                this.framebuffer.Flush();
            } finally {
                if (rowRented != null) System.Buffers.ArrayPool<byte>.Shared.Return(rowRented);
            }
        }

        public override void Dispose() {
            try { Disable(); } catch { /* best effort */ }
        }

        private static void ConvertRow(ReadOnlySpan<byte> src, Span<byte> dst,
                                       PixelFormat format, int targetBpp, int pixels) {
            if (format == PixelFormat.Format16bppRgb565 && targetBpp == 16) {
                src.Slice(0, pixels * 2).CopyTo(dst);
                return;
            }
            if (targetBpp == 16) {
                int srcBpp = format switch {
                    PixelFormat.Format24bppRgb => 3,
                    PixelFormat.Format32bppRgb or PixelFormat.Format32bppArgb => 4,
                    _ => throw new NotSupportedException($"Cannot convert {format} to RGB565"),
                };
                for (int p = 0; p < pixels; p++) {
                    byte b = src[p * srcBpp + 0];
                    byte g = src[p * srcBpp + 1];
                    byte re = src[p * srcBpp + 2];
                    ushort v = (ushort)(((re >> 3) << 11) | ((g >> 2) << 5) | (b >> 3));
                    dst[p * 2 + 0] = (byte)(v & 0xFF);
                    dst[p * 2 + 1] = (byte)(v >> 8);
                }
                return;
            }
            if (targetBpp == 24) {
                if (format == PixelFormat.Format24bppRgb) {
                    src.Slice(0, pixels * 3).CopyTo(dst);
                    return;
                }
                if (format is PixelFormat.Format32bppRgb or PixelFormat.Format32bppArgb) {
                    for (int p = 0; p < pixels; p++) {
                        dst[p * 3 + 0] = src[p * 4 + 0];
                        dst[p * 3 + 1] = src[p * 4 + 1];
                        dst[p * 3 + 2] = src[p * 4 + 2];
                    }
                    return;
                }
            }
            if (targetBpp == 32) {
                // BGRX8888 target (MA35D1 DCUltra default). Source bytes are
                // {B, G, R} / {B, G, R, A} little-endian; destination is
                // {B, G, R, X}. Unpack RGB565 by replicating the top bits down.
                if (format == PixelFormat.Format16bppRgb565) {
                    for (int p = 0; p < pixels; p++) {
                        byte lo = src[p * 2 + 0], hi = src[p * 2 + 1];
                        int r5 = (hi >> 3) & 0x1F;
                        int g6 = ((hi & 0x07) << 3) | ((lo >> 5) & 0x07);
                        int b5 = lo & 0x1F;
                        dst[p * 4 + 0] = (byte)((b5 << 3) | (b5 >> 2));
                        dst[p * 4 + 1] = (byte)((g6 << 2) | (g6 >> 4));
                        dst[p * 4 + 2] = (byte)((r5 << 3) | (r5 >> 2));
                        dst[p * 4 + 3] = 0;
                    }
                    return;
                }
                if (format == PixelFormat.Format24bppRgb) {
                    for (int p = 0; p < pixels; p++) {
                        dst[p * 4 + 0] = src[p * 3 + 0];
                        dst[p * 4 + 1] = src[p * 3 + 1];
                        dst[p * 4 + 2] = src[p * 3 + 2];
                        dst[p * 4 + 3] = 0;
                    }
                    return;
                }
                if (format is PixelFormat.Format32bppRgb or PixelFormat.Format32bppArgb) {
                    src.Slice(0, pixels * 4).CopyTo(dst);
                    return;
                }
            }
            throw new NotSupportedException($"Cannot convert {format} to {targetBpp}bpp");
        }
    }

    /// <summary>Minimal 24-bit uncompressed BMP reader for ShowLogo.</summary>
    internal static class BmpReader {
        public static bool TryRead24bppBgr(string path, out byte[] pixels, out int width, out int height) {
            pixels = Array.Empty<byte>();
            width = height = 0;
            if (!File.Exists(path)) return false;
            using var f = File.OpenRead(path);
            Span<byte> header = stackalloc byte[54];
            if (f.Read(header) != 54) return false;
            if (header[0] != (byte)'B' || header[1] != (byte)'M') return false;
            uint dataOffset = BitConverter.ToUInt32(header[10..14]);
            int w = BitConverter.ToInt32(header[18..22]);
            int h = BitConverter.ToInt32(header[22..26]);
            ushort bpp = BitConverter.ToUInt16(header[28..30]);
            uint compression = BitConverter.ToUInt32(header[30..34]);
            if (bpp != 24 || compression != 0 || w <= 0) return false;

            bool flipY = h > 0;
            int absHeight = flipY ? h : -h;
            int rowBytes = w * 3;
            int rowPad = (4 - (rowBytes & 3)) & 3;

            f.Seek(dataOffset, SeekOrigin.Begin);
            pixels = new byte[rowBytes * absHeight];
            Span<byte> pad = stackalloc byte[4];
            for (int r = 0; r < absHeight; r++) {
                int destRow = flipY ? absHeight - 1 - r : r;
                if (f.Read(pixels, destRow * rowBytes, rowBytes) != rowBytes) return false;
                if (rowPad > 0) f.Read(pad.Slice(0, rowPad));
            }
            width = w;
            height = absHeight;
            return true;
        }
    }
}
