// PWM for Endpoint, with the same API as .NET's System.Device.Pwm:
//
//   using namespace GHIElectronics::Endpoint::Core;
//   using namespace GHIElectronics::Endpoint::Devices::Pwm;
//
//   EPM815::Pwm::Initialize(EPM815::Pwm::Pin::PA8);
//   auto pwm = PwmChannel::Create(EPM815::Pwm::GetChipId(EPM815::Pwm::Pin::PA8),
//                                 EPM815::Pwm::GetChannelId(EPM815::Pwm::Pin::PA8), 1000, 0.5);
//   pwm.Start();
//
// .NET's properties are a getter and a setter of the same name:
// pwm.Frequency() and pwm.Frequency(2000).
#pragma once

#include "detail/Handle.h"

namespace GHIElectronics::Endpoint::Devices::Pwm {

// A PWM output (call EPM815::Pwm::Initialize for the pin first)
class PwmChannel {
public:
    // dutyCyclePercentage is 0.0 to 1.0, as in .NET
    static PwmChannel Create(int chip, int channel, int frequency = 400, double dutyCyclePercentage = 0.5) {
        return PwmChannel(chip, channel, frequency, dutyCyclePercentage);
    }

    PwmChannel(int chip, int channel, int frequency = 400, double dutyCyclePercentage = 0.5) {
        void* handle = nullptr;
        Core::detail::Check(endpoint_pwmchannel_create(chip, channel, frequency, dutyCyclePercentage, &handle));
        handle_ = Handle(handle);
    }

    // Hz
    int Frequency() const {
        int frequency = 0;
        Core::detail::Check(endpoint_pwmchannel_get_frequency(h(), &frequency));
        return frequency;
    }
    void Frequency(int frequency) { Core::detail::Check(endpoint_pwmchannel_set_frequency(h(), frequency)); }

    // 0.0 to 1.0
    double DutyCycle() const {
        double dutyCycle = 0;
        Core::detail::Check(endpoint_pwmchannel_get_duty_cycle(h(), &dutyCycle));
        return dutyCycle;
    }
    void DutyCycle(double dutyCycle) { Core::detail::Check(endpoint_pwmchannel_set_duty_cycle(h(), dutyCycle)); }

    void Start() { Core::detail::Check(endpoint_pwmchannel_start(h())); }
    void Stop() { Core::detail::Check(endpoint_pwmchannel_stop(h())); }

    // Stops the output and gives the channel back
    void Dispose() { handle_.Reset(); }

private:
    using Handle = Core::detail::Handle<endpoint_pwmchannel_dispose>;

    void* h() const { return handle_.Get("PwmChannel"); }

    Handle handle_;
};

}  // namespace GHIElectronics::Endpoint::Devices::Pwm
