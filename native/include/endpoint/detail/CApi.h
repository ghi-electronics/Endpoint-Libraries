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
int endpoint_printer_open(const char* deviceName, endpoint_unplugged_callback callback, void* context, void** device);
int endpoint_printer_close(void* device);
int endpoint_printer_info(void* device, int* vendorId, int* productId, int* connected);
int endpoint_printer_device_id(void* device, char* text, int size);
int endpoint_printer_status(void* device, int* status);
int endpoint_printer_write(void* device, const uint8_t* data, int length, int timeoutMs, int* written);
int endpoint_printer_read(void* device, uint8_t* buffer, int size, int timeoutMs, int* length);

// Sound (SoundDevice) and MP3
int endpoint_sound_create(const char* playback, const char* recording, const char* mixer, int sampleRate, int channels,
                          int bitsPerSample, int unmute, void** device);
int endpoint_sound_dispose(void* device);
int endpoint_sound_play_file(void* device, const char* path, int mp3);
int endpoint_sound_play_buffer(void* device, const uint8_t* data, int length, int mp3);
int endpoint_sound_record_file(void* device, int seconds, const char* path);
int endpoint_sound_record_buffer(void* device, int seconds, uint8_t* buffer, int size, int* length);
int endpoint_sound_start_recording(void* device, const char* path);
int endpoint_sound_stop_recording(void* device);
int endpoint_sound_get_volume(void* device, int capture, int64_t* volume);
int endpoint_sound_set_volume(void* device, int capture, int64_t volume);
int endpoint_sound_get_mute(void* device, int capture, int* mute);
int endpoint_sound_set_mute(void* device, int capture, int mute);
int endpoint_mp3_open(const char* path, const uint8_t* data, int length, void** decoder, int* sampleRate, int* channels,
                      int64_t* samples);
int endpoint_mp3_read(void* decoder, int16_t* samples, int count, int* got);
int endpoint_mp3_close(void* decoder);

// Webcams (Camera)
typedef void (*endpoint_camera_frame_callback)(void* context, const uint8_t* jpeg, int length, int width, int height);
int endpoint_camera_open(const char* deviceName, endpoint_camera_frame_callback frame, endpoint_unplugged_callback unplugged,
                         void* context, void** camera);
int endpoint_camera_close(void* camera);
int endpoint_camera_info(void* camera, char* name, int nameSize, int* connected, int* formats);
int endpoint_camera_format(void* camera, int index, int* width, int* height, int* pixelFormat, int* maxFrameRate);
int endpoint_camera_get_resolution(void* camera, int* width, int* height, int* pixelFormat, int* frameRate);
int endpoint_camera_set_resolution(void* camera, int width, int height, int frameRate, int pixelFormat);
int endpoint_camera_get_quality(void* camera, int* quality);
int endpoint_camera_set_quality(void* camera, int quality);
int endpoint_camera_capture(void* camera, int timeoutMs, uint8_t** jpeg, int* length);
int endpoint_camera_free(uint8_t* jpeg);
int endpoint_camera_start_streaming(void* camera);
int endpoint_camera_stop_streaming(void* camera);
int endpoint_camera_get_control(void* camera, int control, int* supported, int* minimum, int* maximum, int* step,
                                int* defaultValue, int* value);
int endpoint_camera_set_control(void* camera, int control, int value);

// Network: translates a type (0=Ethernet, 1=WiFi, 2=UsbTethered) into the
// system identifier for its interface ("eth0", "wlan0", "usb0", ...).
// Fills outBuf with an empty string if no interface of that type is
// currently present. Enable reads /config/network.json fresh every time
// and applies synchronously in the caller's process.
int endpoint_network_get_interface_name(int type, char* outBuf, int outSize);
int endpoint_network_enable(int type);
int endpoint_network_disable(int type);
int endpoint_network_is_enabled(int type, int* result);
int endpoint_network_is_connected(int type, int* result);

// Persistent interface settings carried across the C boundary as a POD
// struct. Empty strings mean "not set" (null in JSON). WiFi-only fields
// (ssid/password/security) are ignored for non-WiFi types. The C++ and
// Python wrappers marshal this into their typed Settings classes.
#define ENDPOINT_NETWORK_MAX_DNS 8
#define ENDPOINT_NETWORK_IP_SIZE 64
#define ENDPOINT_NETWORK_MAC_SIZE 32
#define ENDPOINT_NETWORK_SSID_SIZE 64
#define ENDPOINT_NETWORK_PASSWORD_SIZE 128

typedef struct {
    int is_dhcp_enabled;
    char address[ENDPOINT_NETWORK_IP_SIZE];
    char subnet_mask[ENDPOINT_NETWORK_IP_SIZE];
    char gateway[ENDPOINT_NETWORK_IP_SIZE];
    char dns[ENDPOINT_NETWORK_MAX_DNS][ENDPOINT_NETWORK_IP_SIZE];
    int dns_count;
    char mac[ENDPOINT_NETWORK_MAC_SIZE];
    // WiFi only; ignored for other types.
    char ssid[ENDPOINT_NETWORK_SSID_SIZE];
    char password[ENDPOINT_NETWORK_PASSWORD_SIZE];
    int security;  // 0=None, 1=Wpa, 2=Wpa2, 3=Wpa3
} endpoint_network_settings_t;

int endpoint_network_get_interface_settings(int type, endpoint_network_settings_t* out);
int endpoint_network_set_interface_settings(int type, const endpoint_network_settings_t* in);

// Display: the built-in Parallel panel connected to the SoC's LCD controller.
// Programs talk to it through the DisplayController abstract class in each
// language; this C layer only drives the one concrete transport (parallel
// RGB panel via /dev/fb0). SPI/I2C panels are the customer's own code - they
// subclass DisplayController and own the chip-specific init / pixel path.
//
// Enable reads /config/display.json, loads the panel driver (panel-simple)
// and raises the backlight. Disable lowers backlight and unloads.
// IsConnected is true when a /dev/fbN exists.
int endpoint_display_enable(void);
int endpoint_display_disable(void);
int endpoint_display_is_enabled(int* result);

#define ENDPOINT_DISPLAY_LOGO_SIZE 128
#define ENDPOINT_DISPLAY_NAME_SIZE 32

typedef struct {
    int enable_at_boot;
    int type;  // 0=None, 1=Parallel. Custom panels use None here and
               // instantiate their own DisplayController subclass.

    // Backlight (nullable: has_backlight=0 means "no backlight is controlled
    // by the display controller - either there is no backlight or the user's
    // own code drives it").
    int has_backlight;
    char backlight_gpio_chip[ENDPOINT_DISPLAY_NAME_SIZE];
    int backlight_gpio_pin;
    int backlight_active_polarity;  // 0=ActiveHigh (ON when GPIO is high),
                                    // 1=ActiveLow (ON when GPIO is low).

    // Logo filename on the config partition (empty = none).
    char logo[ENDPOINT_DISPLAY_LOGO_SIZE];

    // Parallel (RGB parallel panel; honored when type == 1).
    int parallel_width;
    int parallel_height;
    int parallel_pixel_clock_hz;
    int parallel_h_front_porch;
    int parallel_h_back_porch;
    int parallel_h_sync;
    int parallel_v_front_porch;
    int parallel_v_back_porch;
    int parallel_v_sync;
    int parallel_bits_per_pixel;
    // DE (Data Enable) polarity:
    //   0 = ActiveHigh (DE high while valid pixel data is on the bus)
    //   1 = ActiveLow  (DE low while valid pixel data is on the bus)
    int parallel_de_polarity;
    // Edge of the pixel clock on which the panel latches data:
    //   0 = Rising  (default; the SoC drives data on the falling edge, panel
    //                samples on the rising edge)
    //   1 = Falling (inverted; SoC drives on rising, panel samples on falling)
    int parallel_pixel_clock_edge;
} endpoint_display_settings_t;

int endpoint_display_get_settings(endpoint_display_settings_t* out);
int endpoint_display_set_settings(const endpoint_display_settings_t* in);

// Touchscreens (built-in via display.json or USB via UsbHostController).
// The callback runs on the device's own thread:
//   (context, eventType 0=Pressed/1=Released/2=Moved, pointCount, ids[], xs[], ys[])
// id arrays are valid for the duration of the callback only.
typedef void (*endpoint_touch_callback)(void* context, int eventType, int pointCount,
                                        const int* ids, const int* xs, const int* ys);
int endpoint_touch_find_default(char* outBuf, int outSize);
int endpoint_touch_open(const char* deviceName, endpoint_touch_callback callback,
                        void* context, void** device);
int endpoint_touch_close(void* device);
int endpoint_touch_info(void* device, int* vendorId, int* productId, int* connected,
                        int* width, int* height);

// ---- UI (LVGL under the hood) ----
// Lifecycle. Initialize once after the display is enabled; Run blocks until
// Stop is called (safe from a signal handler).
int endpoint_ui_initialize(void);
int endpoint_ui_run(void);
int endpoint_ui_stop(void);

// Touch input bridge. Call touch_attach once after Initialize to register
// an LVGL pointer input device. Then call touch_feed(x, y, pressed) from
// your touch chip's event handler — LVGL's event loop will pick it up on
// the next tick and dispatch Clicked / ValueChanged accordingly.
int endpoint_ui_touch_attach(void);
int endpoint_ui_touch_feed(int x, int y, int pressed);

// Common widget operations. `widget` is a handle returned by one of the
// *_create functions (internally an lv_obj_t*). Destroy frees the widget
// and all its children.
int endpoint_ui_widget_destroy(void* widget);
int endpoint_ui_widget_set_pos(void* widget, int x, int y);
int endpoint_ui_widget_set_x(void* widget, int x);
int endpoint_ui_widget_set_y(void* widget, int y);
int endpoint_ui_widget_get_pos(void* widget, int* x, int* y);
int endpoint_ui_widget_set_size(void* widget, int width, int height);
int endpoint_ui_widget_set_width(void* widget, int width);
int endpoint_ui_widget_set_height(void* widget, int height);
int endpoint_ui_widget_get_size(void* widget, int* width, int* height);
int endpoint_ui_widget_set_visible(void* widget, int visible);
int endpoint_ui_widget_get_visible(void* widget, int* visible);

// Event subscription. The callback runs on the UI thread (the one calling
// endpoint_ui_run). `context` is passed back untouched. Not currently
// unsubscribeable; subscriptions live until the widget is destroyed.
typedef void (*endpoint_ui_event_callback)(void* context);
int endpoint_ui_subscribe_clicked(void* widget, endpoint_ui_event_callback cb, void* context);
int endpoint_ui_subscribe_value_changed(void* widget, endpoint_ui_event_callback cb, void* context);

// Screen: a top-level container with no parent. Multiple screens can exist;
// only one is active at a time (endpoint_ui_screen_load makes it active).
int endpoint_ui_screen_create(void** out);
int endpoint_ui_screen_load(void* screen);
int endpoint_ui_screen_active(void** out);

// Container: groups children; positions them relative to itself.
int endpoint_ui_container_create(void* parent, void** out);

// Button: Clicked event via endpoint_ui_subscribe_clicked.
int endpoint_ui_button_create(void* parent, void** out);
int endpoint_ui_button_set_text(void* button, const char* text);
int endpoint_ui_button_set_bg_color(void* button, uint32_t rgb);
int endpoint_ui_button_set_text_color(void* button, uint32_t rgb);
int endpoint_ui_button_set_font_size(void* button, int size);

// Label.
int endpoint_ui_label_create(void* parent, void** out);
int endpoint_ui_label_set_text(void* label, const char* text);
int endpoint_ui_label_set_color(void* label, uint32_t rgb);
int endpoint_ui_label_set_font_size(void* label, int size);

// Image: raw RGB565 pixels (little-endian). The caller keeps the pixel
// buffer alive for the lifetime of the widget; libendpoint does not copy.
int endpoint_ui_image_create(void* parent, void** out);
int endpoint_ui_image_set_source(void* image, const uint8_t* rgb565Pixels,
                                 int byteCount, int width, int height);

// TextBox: single-line text input. ValueChanged event via
// endpoint_ui_subscribe_value_changed.
int endpoint_ui_textbox_create(void* parent, void** out);
int endpoint_ui_textbox_set_text(void* textbox, const char* text);
int endpoint_ui_textbox_get_text(void* textbox, char* outBuf, int outSize);
int endpoint_ui_textbox_set_placeholder(void* textbox, const char* placeholder);

// Slider.
int endpoint_ui_slider_create(void* parent, void** out);
int endpoint_ui_slider_set_range(void* slider, int min, int max);
int endpoint_ui_slider_set_value(void* slider, int value);
int endpoint_ui_slider_get_value(void* slider, int* value);

// CheckBox.
int endpoint_ui_checkbox_create(void* parent, void** out);
int endpoint_ui_checkbox_set_text(void* checkbox, const char* text);
int endpoint_ui_checkbox_set_checked(void* checkbox, int checked);
int endpoint_ui_checkbox_get_checked(void* checkbox, int* checked);

#ifdef __cplusplus
}
#endif
