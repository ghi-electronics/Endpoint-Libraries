# GHIElectronics.Endpoint.Pins

Pin constants, bus tables, and board support for every GHI Electronics
Endpoint board (EPM815, EPDomino24, EPN256, EPN128). One wheel covers
every Endpoint board; the right board registers itself at import via
ChipDetect, so the chip-neutral controllers in
`GHIElectronics.Endpoint.Core` (`I2cController`, `SpiController`, etc.)
dispatch correctly on whatever hardware the program is running on.

```python
import GHIElectronics.Endpoint.Pins                    # registers the board
from GHIElectronics.Endpoint.Core import I2cController
from GHIElectronics.Endpoint.Pins import EPDomino24

I2cController.Initialize(EPDomino24.I2c.I2c1)
```

Raspberry Pi Zero 2 W (not an Endpoint board) lives in
`GHIElectronics.Endpoint.Drivers.RaspberryPi` instead.

Mirrors .NET's `GHIElectronics.Endpoint.Pins`.
