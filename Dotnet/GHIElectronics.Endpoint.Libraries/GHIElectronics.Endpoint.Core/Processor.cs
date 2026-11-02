using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronics.Endpoint.Core {
    /// <summary>
    /// Identifies the processor the firmware is running on. Libraries that need
    /// board-specific behaviour read <see cref="Name"/> and compare against the
    /// processor-id constants exposed here.
    /// </summary>
    public static class Processor {

        /// <summary>Processor id used by the EPM815 module.</summary>
        public const string STM32MP1 = "STM32MP1";

        /// <summary>The current processor's id. Set once at startup; defaults to <see cref="STM32MP1"/>.</summary>
        public static string Name { get; internal set; } = STM32MP1;

    }
}
