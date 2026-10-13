/* A link-only stand-in for the board's libhidapi-libusb.so.0 (hidapi 0.15.0): the
   names of its 27 functions, made by update_from_firmware.py. Programs
   link against it and use the firmware's library on the board. */

void get_usb_code_for_current_locale(void) {}
void hid_close(void) {}
void hid_enumerate(void) {}
void hid_error(void) {}
void hid_exit(void) {}
void hid_free_enumeration(void) {}
void hid_get_device_info(void) {}
void hid_get_feature_report(void) {}
void hid_get_indexed_string(void) {}
void hid_get_input_report(void) {}
void hid_get_manufacturer_string(void) {}
void hid_get_product_string(void) {}
void hid_get_report_descriptor(void) {}
void hid_get_serial_number_string(void) {}
void hid_init(void) {}
void hid_libusb_wrap_sys_device(void) {}
void hid_open(void) {}
void hid_open_path(void) {}
void hid_read(void) {}
void hid_read_error(void) {}
void hid_read_timeout(void) {}
void hid_send_feature_report(void) {}
void hid_send_output_report(void) {}
void hid_set_nonblocking(void) {}
void hid_version(void) {}
void hid_version_str(void) {}
void hid_write(void) {}
