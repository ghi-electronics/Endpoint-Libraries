using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GHIElectronics.Endpoint.Core.STM32MP1;

namespace GHIElectronics.Endpoint.Core {
    public static partial class Configuration {


        
        internal static void ThrowExceptionPinInUsed(int pin) {
            throw new ArgumentException($"Pin {pin} is already in used."); ;
        }

    }
}
