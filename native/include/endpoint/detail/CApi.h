// libendpoint's hidden C layer, called by the classes in endpoint.h.
// Programs use those classes, not these functions. Every function returns
// 0, or the kind of error (see Core.h); endpoint_last_error() gives its message.
#pragma once

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

const char* endpoint_last_error(void);
const char* endpoint_version(void);

// GPIO
typedef void (*endpoint_gpio_callback)(void* context, int pin, int changeType);
int endpoint_gpio_create(void** controller);
int endpoint_gpio_dispose(void* controller);
int endpoint_gpio_pin_count(void* controller, int* count);
int endpoint_gpio_open_pin(void* controller, int pin, int mode, int initialValue);
int endpoint_gpio_close_pin(void* controller, int pin);
int endpoint_gpio_is_pin_open(void* controller, int pin, int* open);
int endpoint_gpio_set_pin_mode(void* controller, int pin, int mode, int initialValue);
int endpoint_gpio_get_pin_mode(void* controller, int pin, int* mode);
int endpoint_gpio_is_pin_mode_supported(void* controller, int pin, int mode, int* supported);
int endpoint_gpio_read(void* controller, int pin, int* value);
int endpoint_gpio_write(void* controller, int pin, int value);
int endpoint_gpio_wait_for_event(void* controller, int pin, int eventTypes, int timeoutMs, int* happened, int* timedOut);
int endpoint_gpio_add_callback(void* controller, int pin, int eventTypes, endpoint_gpio_callback callback, void* context,
                               int* id);
int endpoint_gpio_remove_callback(void* controller, int id);

// EPM815 setup
int endpoint_i2c_initialize(int bus, int frequency);
int endpoint_i2c_uninitialize(int bus);
int endpoint_i2c_get_frequency(int bus, int* frequency);
int endpoint_spi_initialize(int bus, int bufferSize);
int endpoint_spi_uninitialize(int bus);
int endpoint_pwm_initialize(int pin);
int endpoint_pwm_uninitialize(int pin);
int endpoint_pwm_get_chip_id(int pin, int* chip);
int endpoint_pwm_get_channel_id(int pin, int* channel);
int endpoint_serialport_initialize(const char* portName, int enableHardwareFlowControl);
int endpoint_serialport_uninitialize(const char* portName);
int endpoint_can_initialize(const char* port, int bitrate);
int endpoint_can_uninitialize(const char* port);

// I2C, SPI, PWM devices
int endpoint_i2cdevice_create(int busId, int deviceAddress, void** device);
int endpoint_i2cdevice_dispose(void* device);
int endpoint_i2cdevice_read(void* device, uint8_t* buffer, int length);
int endpoint_i2cdevice_write(void* device, const uint8_t* buffer, int length);
int endpoint_i2cdevice_write_read(void* device, const uint8_t* write, int writeLength, uint8_t* read, int readLength);
int endpoint_spidevice_create(int busId, int chipSelectLine, int mode, int dataBitLength, int clockFrequency, int dataFlow,
                              int chipSelectActiveHigh, void** device);
int endpoint_spidevice_dispose(void* device);
int endpoint_spidevice_transfer(void* device, const uint8_t* write, uint8_t* read, int length);
int endpoint_pwmchannel_create(int chip, int channel, int frequency, double dutyCycle, void** pwm);
int endpoint_pwmchannel_dispose(void* pwm);
int endpoint_pwmchannel_get_frequency(void* pwm, int* frequency);
int endpoint_pwmchannel_set_frequency(void* pwm, int frequency);
int endpoint_pwmchannel_get_duty_cycle(void* pwm, double* dutyCycle);
int endpoint_pwmchannel_set_duty_cycle(void* pwm, double dutyCycle);
int endpoint_pwmchannel_start(void* pwm);
int endpoint_pwmchannel_stop(void* pwm);

// ADC, DAC, watchdog, RTC
int endpoint_adc_pin_count(int* count);
int endpoint_adc_supports_analog_input(int pin, int* supported);
int endpoint_adc_convert_pin_to_channel(int pin, int* channel);
int endpoint_adc_voltage_reference(double* millivolts);
int endpoint_adc_open_pin(int pin, void** input);
int endpoint_adc_close_pin(void* input);
int endpoint_adc_resolution_bits(void* input, int* bits);
int endpoint_adc_read_raw(void* input, uint32_t* value);
int endpoint_adc_read_millivolts(void* input, double* millivolts);
int endpoint_dac_open_channel(int channel, void** output);
int endpoint_dac_close_channel(void* output);
int endpoint_dac_write_value(void* output, int value);
int endpoint_dac_last_written_value(void* output, int* value);
int endpoint_watchdog_create(void** watchdog);
int endpoint_watchdog_dispose(void* watchdog);
int endpoint_watchdog_max_timeout(int* milliseconds);
int endpoint_watchdog_is_enabled(void* watchdog, int* enabled);
int endpoint_watchdog_enable(void* watchdog, int timeoutMs);
int endpoint_watchdog_reset(void* watchdog);
int endpoint_watchdog_disable(void* watchdog);
int endpoint_rtc_read_time(int64_t* secondsUtc);
int endpoint_rtc_set_time(int64_t secondsUtc);
int endpoint_rtc_is_valid(int* valid);
int endpoint_rtc_set_charge_mode(int chargeMode);
int endpoint_rtc_not_implemented(void);

// Serial port and CAN (C++ only)
int endpoint_serial_open(const char* portName, int baudRate, int parity, int dataBits, int stopBits, int handshake,
                         void** port);
int endpoint_serial_close(void* port);
int endpoint_serial_set_baud_rate(void* port, int baudRate);
int endpoint_serial_read(void* port, uint8_t* buffer, int count, int timeoutMs, int* read);
int endpoint_serial_write(void* port, const uint8_t* buffer, int count, int timeoutMs);
int endpoint_serial_bytes_to_read(void* port, int* count);
int endpoint_serial_discard_in(void* port);
int endpoint_serial_discard_out(void* port);
int endpoint_canraw_open(const char* networkInterface, void** can);
int endpoint_canraw_close(void* can);
int endpoint_canraw_write_frame(void* can, uint32_t id, const uint8_t* data, int length);
int endpoint_canraw_read_frame(void* can, uint32_t* id, uint8_t* data, int capacity, int* length, int* ok);
int endpoint_canraw_filter(void* can, uint32_t id);

// SD cards and USB drives
typedef void (*endpoint_storage_callback)(void* context, intptr_t hdc, int status, const char* volumeLabel);
int endpoint_storage_create(const char* name, endpoint_storage_callback callback, void* context, void** controller);
int endpoint_storage_dispose(void* controller);
int endpoint_storage_enable(void* controller);
int endpoint_storage_disable(void* controller);
int endpoint_storage_mount(intptr_t hdc, char* name, int nameSize);
int endpoint_storage_flush(intptr_t hdc);
int endpoint_storage_unmount(intptr_t hdc);
int endpoint_driveinfo_get(const char* path, char* format, int formatSize, int* driveType, int* isReady,
                           int64_t* totalSize, int64_t* totalFreeSpace, int64_t* availableFreeSpace);

// USB host: devices connected and disconnected
typedef void (*endpoint_usbhost_callback)(void* context, int status, uint32_t id, int type, int vendorId,
                                          int productId, const char* deviceName, const char* manufacturer,
                                          const char* product, const char* serialNumber);
int endpoint_usbhost_create(endpoint_usbhost_callback callback, void* context, void** controller);
int endpoint_usbhost_dispose(void* controller);
int endpoint_usbhost_enable(void* controller);
int endpoint_usbhost_disable(void* controller);

// USB keyboards, mice and joysticks
typedef void (*endpoint_input_callback)(void* context, int event, int a, int b, int c, int d, int e);
int endpoint_input_open(const char* deviceName, int type, endpoint_input_callback callback, void* context,
                        void** device);
int endpoint_input_close(void* device);
int endpoint_input_info(void* device, int* vendorId, int* productId, int* connected, int* capabilities);
int endpoint_input_key_state(void* device, int key, int* state);
int endpoint_input_mouse_state(void* device, int* x, int* y, int* wheel, int* buttons);
int endpoint_input_joystick_state(void* device, int cursor, int* x, int* y, int* hat, uint32_t* buttons);
int endpoint_input_set_cursor_bounds(void* device, int cursor, int minX, int maxX, int minY, int maxY);
int endpoint_input_set_cursor_position(void* device, int x, int y);
int endpoint_input_set_scale(void* device, double scale);

// A program's own USB devices (for .NET's RawDevice and HidDevice)
typedef void (*endpoint_unplugged_callback)(void* context);
int endpoint_usbraw_open(const char* deviceName, endpoint_unplugged_callback callback, void* context, void** device);
int endpoint_usbraw_close(void* device);
int endpoint_usbraw_info(void* device, int* vendorId, int* productId, int* connected);
int endpoint_usbraw_control(void* device, int requestType, int request, int value, int index, uint8_t* data,
                            int length, int timeoutMs, int* transferred);
int endpoint_usbraw_transfer(void* device, int endpoint, uint8_t* data, int length, int timeoutMs, int* transferred);
int endpoint_hid_open(const char* deviceName, endpoint_unplugged_callback callback, void* context, void** device);
int endpoint_hid_close(void* device);
int endpoint_hid_info(void* device, int* vendorId, int* productId, int* connected, char* name, int nameSize);
int endpoint_hid_report_descriptor(void* device, uint8_t* buffer, int size, int* length);
int endpoint_hid_write(void* device, const uint8_t* data, int length, int* written);
int endpoint_hid_read(void* device, uint8_t* buffer, int size, int timeoutMs, int* length);
int endpoint_hid_get_feature(void* device, uint8_t* buffer, int size, int* length);
int endpoint_hid_send_feature(void* device, const uint8_t* data, int length);

#ifdef __cplusplus
}
#endif
