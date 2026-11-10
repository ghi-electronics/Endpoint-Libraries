# GHI Electronics Endpoint

.NET libraries for the [GHI Electronics Endpoint](https://www.ghielectronics.com/) Endpoint boards. Same API as the Python and C/C++ companions; standard System.Device.* contracts underneath.

A fresh Endpoint project needs only `GHIElectronics.Endpoint.Core`:

```csharp
using System.Device.Gpio;
using GHIElectronics.Endpoint.Core;

using var gpio = new GpioController(new EPDomino24.Gpio.Driver());
gpio.OpenPin(EPDomino24.Led, PinMode.Output);
gpio.Write(EPDomino24.Led, PinValue.High);
```

Add the parts you need:

```
dotnet add package GHIElectronics.Endpoint.Devices.Display
dotnet add package GHIElectronics.Endpoint.Devices.Touch
dotnet add package GHIElectronics.Endpoint.UI
```

Device drivers (ILI9341, SH110x, FT5xx6, ...) live at [ghi-electronics/endpoint-drivers](https://github.com/ghi-electronics/endpoint-drivers).

Source + issues: <https://github.com/ghi-electronics/Endpoint-Libraries>
