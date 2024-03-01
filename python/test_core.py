import sys


sys.path.append( '/root/.epdata/python/GHIElectronics.Endpoint.Core/GHIElectronics/Endpoint/Core/EPM815' )
sys.path.append( '/root/.epdata/python/GHIElectronics.Endpoint.Core' )

#import GHIElectronics

# import GHIElectronics.Endpoint.Core.EPM815.Register as Register
import Register
import Spi
import Gpio

#import GHIElectronics.Endpoint.Core.EPM815.I2c as I2c
#import GHIElectronics.Endpoint.Core.EPM815.Spi as Spi
#import GHIElectronics.Endpoint.Core.EPM815.Gpio as Gpio

af = Gpio.Alternate.AF1



Spi.Initialize(1)


Register.Write(0x50000A28, 2)
reg = Register.Read(0x50000A28)



#from GHIElectronics import EPM815 as EPM815

#from GHIElectronics.Endpoint.Core import Register as Register



print("hello ", str(af))

a = 3 + 3

