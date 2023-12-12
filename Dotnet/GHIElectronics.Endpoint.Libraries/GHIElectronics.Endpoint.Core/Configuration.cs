using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

[assembly: InternalsVisibleToAttribute("GHIElectronics.Endpoint.Devices.Can")]
[assembly: InternalsVisibleToAttribute("GHIElectronics.Endpoint.Devices.Sdmmc")]
namespace GHIElectronics.Endpoint.Core {

    public static partial class Configuration {


        
        internal static void ThrowExceptionPinInUsed(int pin) {
            throw new ArgumentException($"Pin {pin} is already in used."); ;
        }

        internal static void ThrowExceptionPinNotInRange(int pin) {
            throw new ArgumentException($"Pin {pin} is out of cpu support range."); ;
        }

    }
}
