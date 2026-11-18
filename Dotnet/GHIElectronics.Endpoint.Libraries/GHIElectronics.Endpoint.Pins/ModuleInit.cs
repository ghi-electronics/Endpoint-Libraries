using System.Runtime.CompilerServices;
using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Pins {
    /// <summary>
    /// Runs at assembly load. Reads <see cref="ChipDetect.Current"/> and
    /// registers the matching board's <see cref="IBoardSupport"/> with Core.
    ///
    /// One Endpoint.Pins nupkg carries every board (EPM815 / EPDomino24 /
    /// EPN256 / EPN128); the runtime picks the right one from the device
    /// tree. User programs just reference the single package and get the
    /// right behavior on whichever chip they're running on.
    ///
    /// On an unknown chip (dev host, test machine) no board is registered;
    /// the first call to any chip-neutral controller throws a clear error.
    /// </summary>
    internal static class ModuleInit {
        [ModuleInitializer]
        internal static void Init() {
            switch (ChipDetect.Current) {
                case Chip.Epm815: BoardSupport.Register(new EPM815BoardSupport()); break;
                case Chip.Epn256: BoardSupport.Register(new EPN256BoardSupport()); break;
                case Chip.Epn128: BoardSupport.Register(new EPN128BoardSupport()); break;
                // Chip.Unknown: do not register. BoardSupport.Current throws
                // on first access, giving a clear "no board detected" error.
            }
        }
    }
}
