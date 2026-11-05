namespace GHIElectronics.Endpoint.Core.Internal {
    /// <summary>
    /// One of the chip's ADC inputs, in a form both the EPM815 and EPN256
    /// tables share. <paramref name="Alternate"/> is the pin's mux value:
    /// <c>-1</c> means put the pin in analog mode (STM32 MP1); any non-negative
    /// value is the MFP peripheral function (MA35). <paramref name="HasOwnPin"/>
    /// is <c>false</c> for dedicated analog pads (STM32 ANA0 / ANA1) that are
    /// not GPIO and so skip the pin reservation.
    /// </summary>
    internal readonly record struct AnalogInput(
        int Pin, int Channel, int Alternate, bool HasOwnPin, string Name, string IioName);
}
