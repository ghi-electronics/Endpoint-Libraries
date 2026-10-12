// The analog outputs of Endpoint, with the same API as TinyCLR (and the .NET
// GHIElectronics.Endpoint.Devices.Dac):
//
//   using namespace GHIElectronics::Endpoint::Core;
//   using namespace GHIElectronics::Endpoint::Devices::Dac;
//
//   auto dac = DacController::FromName(EPM815::Dac::Id);
//   auto output = dac.OpenChannel(EPM815::Dac::PA5);
//   output.WriteValue(0.5);    // a double is a part of the range: half of 3.3 V
//   output.WriteValue(4095);   // an int is a raw value: 3.3 V
//
// Opening a channel makes its pin an analog output; closing it switches the
// output off and puts the pin back as it was.
#pragma once

#include <string>

#include "detail/Handle.h"

namespace GHIElectronics::Endpoint::Devices::Dac {

class DacChannel;

class DacController {
public:
    static DacController GetDefault() { return DacController(); }

    static DacController FromName(const std::string& name) {
        if (name != Core::EPM815::Dac::Id) {
            throw Core::ArgumentException("There is no DAC named \"" + name + "\": use EPM815.Dac.Id");
        }
        return DacController();
    }

    int ChannelCount() const { return 2; }      // channel numbers start at 0
    int ResolutionInBits() const { return 12; }
    int MinValue() const { return 0; }          // 0 V
    int MaxValue() const { return 4095; }       // the reference voltage (3.3 V)

    // EPM815::Dac::PA4 or EPM815::Dac::PA5
    DacChannel OpenChannel(int channelNumber);

    // Nothing to release: each channel is released when it is disposed
    void Dispose() {}
};

class DacChannel {
public:
    DacChannel(int channelNumber) : channelNumber_(channelNumber) {
        void* handle = nullptr;
        Core::detail::Check(endpoint_dac_open_channel(channelNumber, &handle));
        handle_ = Handle(handle);
    }

    int ChannelNumber() const { return channelNumber_; }
    DacController Controller() const { return DacController(); }

    int LastWrittenValue() const {
        int value = 0;
        Core::detail::Check(endpoint_dac_last_written_value(h(), &value));
        return value;
    }

    // A raw value from MinValue (0 V) to MaxValue (3.3 V)
    void WriteValue(int value) { Core::detail::Check(endpoint_dac_write_value(h(), value)); }

    // A part of the range: 0.0 is 0 V, 1.0 is 3.3 V
    void WriteValue(double ratio) { WriteValue(static_cast<int>(ratio * 4095)); }

    // Switches the output off and puts the pin back as it was
    void Dispose() { handle_.Reset(); }

private:
    using Handle = Core::detail::Handle<endpoint_dac_close_channel>;

    void* h() const { return handle_.Get("DacChannel"); }

    int channelNumber_;
    Handle handle_;
};

inline DacChannel DacController::OpenChannel(int channelNumber) { return DacChannel(channelNumber); }

}  // namespace GHIElectronics::Endpoint::Devices::Dac
