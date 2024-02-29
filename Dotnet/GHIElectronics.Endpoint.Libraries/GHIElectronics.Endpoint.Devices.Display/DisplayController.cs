namespace GHIElectronics.Endpoint.Devices.Display
{
    public class DisplayConfiguration {
        public int Width { get; set; }
        public int Height { get; set; }
    }
    /**<example>
        <code>

    using System.Device.Gpio;
    using System.Device.Gpio.Drivers;
    using GHIElectronics.Endpoint.Core;
    using GHIElectronics.Endpoint.Devices.Display;
    using SkiaSharp;
    using EndpointDisplayTest.Properties;

    var backlightPort = EPM815.Gpio.Pin.PD14 /16;
    var backlightPin = EPM815.Gpio.Pin.PD14 % 16;

    var gpioDriver = new LibGpiodDriver((int)backlightPort);
    var gpioController = new GpioController(PinNumberingScheme.Logical, gpioDriver);
    gpioController.OpenPin(backlightPin, PinMode.Output);
    gpioController.Write(backlightPin, PinValue.High); 
    var screenWidth = 480;
    var screenHeight = 272;
    
    SKBitmap bitmap = new SKBitmap(screenWidth, screenHeight, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
    bitmap.Erase(SKColors.Transparent);

    var configuration = new FBDisplay.Configuration(){
        Clock = 10000,
        Width = 480,
        Hsync_start = 480 + 2,
        Hsync_end = 480 + 2 + 41,
        Htotal = 480 + 2 + 41 + 2,
        Height = 272,
        Vsync_start = 272 + 2,
        Vsync_end = 272 + 2 + 10,
        Vtotal = 272 + 2 + 10 + 2,

    };
    var fbDisplay = new FBDisplay(configuration);
    var displayController = new DisplayController(fbDisplay);
    var imageWidth = 100;
    var imageHeight = 67;

    while (true){
        //Initialize Screen
        using (var screen = new SKCanvas(bitmap)){
            //Create Black Screen 
            screen.DrawColor(SKColors.Black);
            screen.Clear(SKColors.Black); //same thing but also erases anything else on the canvas first

            // Draw Logo from Resources
            var logo = Resources.logo;
            var info = new SKImageInfo(imageWidth, imageHeight);
            var sk_img = SKBitmap.Decode(logo, info);
            screen.DrawBitmap(sk_img, 0, 200);

            // Draw circle
            using (SKPaint circle = new SKPaint()){
                circle.Color = SKColors.Blue;
                circle.IsAntialias = true;
                circle.StrokeWidth = 15;
                circle.Style = SKPaintStyle.Stroke;
                screen.DrawCircle(410, 220, 30, circle); //arguments are x position, y position, radius, and paint
            }

            // Draw Oval
            using (SKPaint oval = new SKPaint()){
                oval.Style = SKPaintStyle.Stroke;
                oval.Color = SKColors.Blue;
                oval.StrokeWidth = 10;
                screen.DrawOval(300, 20, 60, 10, oval);
                oval.Style = SKPaintStyle.Fill;
                oval.Color = SKColors.SkyBlue;
                screen.DrawOval(300, 20, 60, 10, oval);
            }

            // Draw Line
            float[] intervals = [10, 20, 10, 20, 5, 40,];//sets the dash intervals
            using (SKPaint line = new SKPaint()){
                line.Color = SKColors.Red;
                line.IsAntialias = true;
                line.StrokeWidth = 20;
                line.Style = SKPaintStyle.Stroke;

                //Rounds the ends of the line
                line.StrokeCap = SKStrokeCap.Round;

                //Creates dashes in line based on intervals array
                line.PathEffect = SKPathEffect.CreateDash(intervals, 25);

                // Create linear gradient from upper-left to lower-right
                line.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(screenWidth, screenHeight),
                new SKColor[] { SKColors.Red, SKColors.Blue },
                new float[] { 0, 1 },
                SKShaderTileMode.Repeat);
                screen.DrawLine(0, 0, 400, 200, line);
            }

            //Using SkiaTypeface
            byte[] fontfile = Resources.OldeEnglish;
            Stream stream = new MemoryStream(fontfile);

            using (SKPaint textPaint = new SKPaint())
            using (SKTypeface tf = SKTypeface.FromStream(stream)){
                textPaint.Color = SKColors.White;
                textPaint.IsAntialias = true;
                textPaint.StrokeWidth = 2;
                textPaint.Style = SKPaintStyle.Stroke;

                //SKFont Text - 
                SKFont font = new SKFont();
                font.Size = 40;
                font.ScaleX = 2;
                font.Typeface = tf;
                SKTextBlob textBlob = SKTextBlob.Create("D", font);
                screen.DrawText(textBlob, 10, 150, textPaint);
            }

            // Draw Basic text
            using (SKPaint text = new SKPaint()){
                text.Color = SKColors.Yellow;
                text.IsAntialias = true;
                text.StrokeWidth = 2;
                text.Style = SKPaintStyle.Stroke;
                screen.DrawText("Hello World", 20, 20, text);
            }

            // Draw text
            using (SKPaint text = new SKPaint()){
                text.Color = SKColors.Yellow;
                text.IsAntialias = true;
                text.StrokeWidth = 2;
                text.Style = SKPaintStyle.Stroke;

                //SKFont Text - 
                SKFont font = new SKFont();
                font.Size = 22;
                font.ScaleX = 2;
                SKTextBlob textBlob = SKTextBlob.Create("I am Yellow", font);
                screen.DrawText(textBlob, 50, 100, text);
            }

            // Character Outlines
            using (SKPaint textPaint = new SKPaint()){
            // Set Style for the character outlines
            textPaint.Style = SKPaintStyle.Stroke;

            // Set TextSize 100x100
            textPaint.TextSize = Math.Min(100, 100);

            // Measure the text
            SKRect textBounds = new SKRect();
            textPaint.MeasureText("@", ref textBounds);

            // Coordinates to center text on screen
            float xText = screenWidth / 2 - textBounds.MidX;
            float yText = screenHeight / 2 - textBounds.MidY;

            // Get the path for the character outlines
            using (SKPath textPath = textPaint.GetTextPath("@", xText, yText)){
                // Create a new path for the outlines of the path
                using (SKPath outlinePath = new SKPath()){
                    // Convert the path to the outlines of the stroked path
                    textPaint.StrokeWidth = 1;
                    textPaint.GetFillPath(textPath, outlinePath);

                    // Stroke that new path
                    using (SKPaint outlinePaint = new SKPaint()){
                        outlinePaint.Style = SKPaintStyle.Stroke;
                        outlinePaint.StrokeWidth = 1;
                        outlinePaint.Color = SKColors.Red;
                        screen.DrawPath(outlinePath, outlinePaint);
                    }
                }
            }

            //Text along a path
            const string text = "SKIASHARP library ENDPOINT uses ";

            using (SKPath circularPath = new SKPath()){
                float radius = 0.35f * Math.Min(screenWidth, screenHeight);
                circularPath.AddCircle(screenWidth / 2, screenHeight / 2, radius);

                using (SKPaint textPaint2 = new SKPaint()){
                    textPaint2.TextSize = 100;
                    float textWidth = textPaint2.MeasureText(text);
                    textPaint.TextSize *= 2 * 3.14f * radius / textWidth;
                    textPaint.Color = SKColors.Green;

                    screen.DrawTextOnPath(text, circularPath, 0, 0, textPaint);
                }
            }

            // Draw Italic text
            using (SKPaint italicText = new SKPaint()){
                SKFontStyle fontStyle = new SKFontStyle();
                italicText.Color = SKColors.Yellow;
                italicText.IsAntialias = true;
                italicText.StrokeWidth = 2;
                italicText.Style = SKPaintStyle.Stroke;
                SKFont font2 = new SKFont(SKTypeface.Default, 12, 1, 0);
                font2.Size = 22;
                SKTextBlob textBlob = SKTextBlob.Create("ItalicFonts", font2);
                screen.DrawText(textBlob, 200, 200, italicText);
            }

            // Flush to screen
            var data = bitmap.Copy(SKColorType.Rgb565).Bytes;
            displayController.Flush(data);
            Thread.Sleep(1);
        }
      }
    }
    </code>
    </example>*/

    public class DisplayController
    {
        private IDisplayProvider iDisplay;
        public DisplayController(IDisplayProvider display) => this.iDisplay = display;

        public DisplayConfiguration Configuration => this.iDisplay.Configuration;

        public void Flush(byte[] data) => this.Flush(data, 0, data.Length);
        public void Flush(byte[] data, int offset, int length) => this.iDisplay.Flush(data, offset, length);

        public void Flush(byte[] data, int offset, int length, int width, int height) => this.iDisplay.Flush(data, offset, length, width, height);


    }
}
