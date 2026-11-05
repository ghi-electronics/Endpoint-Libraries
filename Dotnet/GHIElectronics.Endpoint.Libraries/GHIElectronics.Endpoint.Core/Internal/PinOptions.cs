namespace GHIElectronics.Endpoint.Core.Internal {
    // Chip-neutral pin options carried in PinFunction. Each chip helper
    // (Stm32Mp1Pins, Ma35dPins, ...) translates these into its own register
    // bit values. Values match the STM32MP1 register encoding so that chip's
    // writes can pass through without conversion; other chips map by table.

    internal enum Pull { None = 0, Up = 1, Down = 2 }

    internal enum OutputType { PushPull = 0, OpenDrain = 1 }

    internal enum Speed { Low = 0, Medium = 1, High = 2, VeryHigh = 3 }
}
