using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronic.Endpoint.Core {
    public static class Processor {

        public const string AM335x = "AM335x";
        public const string STM32MP1 = "STM32MP1";

        public static string Name { get; internal set; } = STM32MP1;

    }
}
