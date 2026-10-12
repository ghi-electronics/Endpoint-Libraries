// The hidden C layer (see CApi.h) for the ADC, DAC, watchdog and RTC

#include <cstdint>

#include "Analog.h"
#include "CApi.h"
#include "System.h"

using namespace GHIElectronics::Endpoint::Internal;

namespace {
template <typename T>
T* Handle(void* handle, const char* name) {
    if (handle == nullptr) {
        throw EndpointError(ErrorKind::ObjectDisposed, std::string("The ") + name + " is disposed");
    }
    return static_cast<T*>(handle);
}
}  // namespace

// ---------------------------------------------------------------- ADC

ENDPOINT_API int endpoint_adc_pin_count(int* count) {
    return Call([&] { *count = AdcController::PinCount(); });
}

ENDPOINT_API int endpoint_adc_supports_analog_input(int pin, int* supported) {
    return Call([&] { *supported = AdcController::SupportsAnalogInput(pin); });
}

ENDPOINT_API int endpoint_adc_convert_pin_to_channel(int pin, int* channel) {
    return Call([&] { *channel = AdcController::ConvertPinNumberToAnalogChannelNumber(pin); });
}

ENDPOINT_API int endpoint_adc_voltage_reference(double* millivolts) {
    return Call([&] { *millivolts = AdcController::VoltageReferenceMillivolts(); });
}

ENDPOINT_API int endpoint_adc_open_pin(int pin, void** input) {
    return Call([&] { *input = new AnalogInputPinImpl(pin); });
}

ENDPOINT_API int endpoint_adc_close_pin(void* input) {
    return Call([&] { delete static_cast<AnalogInputPinImpl*>(input); });
}

ENDPOINT_API int endpoint_adc_resolution_bits(void* input, int* bits) {
    return Call([&] { *bits = Handle<AnalogInputPinImpl>(input, "AnalogInputPin")->AdcResolutionBits(); });
}

ENDPOINT_API int endpoint_adc_read_raw(void* input, uint32_t* value) {
    return Call([&] { *value = Handle<AnalogInputPinImpl>(input, "AnalogInputPin")->ReadRaw(); });
}

ENDPOINT_API int endpoint_adc_read_millivolts(void* input, double* millivolts) {
    return Call([&] { *millivolts = Handle<AnalogInputPinImpl>(input, "AnalogInputPin")->ReadMillivolts(); });
}

// ---------------------------------------------------------------- DAC

ENDPOINT_API int endpoint_dac_open_channel(int channel, void** output) {
    return Call([&] { *output = new DacChannelImpl(channel); });
}

ENDPOINT_API int endpoint_dac_close_channel(void* output) {
    return Call([&] { delete static_cast<DacChannelImpl*>(output); });
}

ENDPOINT_API int endpoint_dac_write_value(void* output, int value) {
    return Call([&] { Handle<DacChannelImpl>(output, "DacChannel")->WriteValue(value); });
}

ENDPOINT_API int endpoint_dac_last_written_value(void* output, int* value) {
    return Call([&] { *value = Handle<DacChannelImpl>(output, "DacChannel")->LastWrittenValue(); });
}

// ---------------------------------------------------------------- Watchdog

ENDPOINT_API int endpoint_watchdog_create(void** watchdog) {
    return Call([&] { *watchdog = new WatchdogImpl(); });
}

ENDPOINT_API int endpoint_watchdog_dispose(void* watchdog) {
    return Call([&] { delete static_cast<WatchdogImpl*>(watchdog); });
}

ENDPOINT_API int endpoint_watchdog_max_timeout(int* milliseconds) {
    return Call([&] { *milliseconds = WatchdogImpl::MaxTimeoutMs; });
}

ENDPOINT_API int endpoint_watchdog_is_enabled(void* watchdog, int* enabled) {
    return Call([&] { *enabled = Handle<WatchdogImpl>(watchdog, "WatchdogController")->IsEnabled(); });
}

ENDPOINT_API int endpoint_watchdog_enable(void* watchdog, int timeoutMs) {
    return Call([&] { Handle<WatchdogImpl>(watchdog, "WatchdogController")->Enable(timeoutMs); });
}

ENDPOINT_API int endpoint_watchdog_reset(void* watchdog) {
    return Call([&] { Handle<WatchdogImpl>(watchdog, "WatchdogController")->Reset(); });
}

ENDPOINT_API int endpoint_watchdog_disable(void* watchdog) {
    return Call([&] { Handle<WatchdogImpl>(watchdog, "WatchdogController")->Disable(); });
}

// ---------------------------------------------------------------- RTC

ENDPOINT_API int endpoint_rtc_read_time(int64_t* secondsUtc) {
    return Call([&] { *secondsUtc = Rtc::ReadTime(); });
}

ENDPOINT_API int endpoint_rtc_set_time(int64_t secondsUtc) {
    return Call([&] { Rtc::SetTime(secondsUtc); });
}

ENDPOINT_API int endpoint_rtc_is_valid(int* valid) {
    return Call([&] { *valid = Rtc::IsValid(); });
}

ENDPOINT_API int endpoint_rtc_set_charge_mode(int chargeMode) {
    return Call([&] { Rtc::SetChargeMode(chargeMode); });
}

// Backup memory and Calibrate (TinyCLR API): not implemented yet, as in .NET
ENDPOINT_API int endpoint_rtc_not_implemented() {
    return Call([&] { ThrowNotImplemented(); });
}
