// C API for the UI subsystem — the layer the language bindings call into.
// Hides LVGL from customers: handles are opaque void* (actually lv_obj_t*),
// colors are uint32_t (0xRRGGBB), events are plain function pointers plus a
// context pointer owned by the caller.
//
// Font size → LVGL built-in font. We support the three sizes the designer
// palette exposes: 14, 20, 32. Any other size is coerced to the closest
// supported one; the designer validates on input anyway.

#include <cstdint>
#include <cstdio>
#include <cstring>
#include <new>

#include "lvgl.h"

#include "CApi.h"
#include "UI.h"
#include "Errors.h"
#include "endpoint/detail/CApi.h"

using namespace GHIElectronics::Endpoint::Internal;

namespace {

lv_obj_t* AsObj(void* handle) {
    return static_cast<lv_obj_t*>(handle);
}

lv_color_t ColorFromRgb(uint32_t rgb) {
    return lv_color_hex(rgb & 0x00FFFFFF);
}

const lv_font_t* FontForSize(int size) {
    switch (size) {
        case 32: return &lv_font_montserrat_32;
        case 20: return &lv_font_montserrat_20;
        default: return &lv_font_montserrat_14;
    }
}

// --- Event trampoline ---
// Each subscription allocates a Record on the heap. The pointer is passed as
// LVGL's user_data; the trampoline casts back and invokes cb(context). Not
// freed on unsubscribe (unsubscribe isn't exposed yet) — leaked at process
// exit, bounded by the number of subscribe calls over the UI's lifetime.
struct SubRecord {
    endpoint_ui_event_callback cb;
    void* context;
};

void EventTrampoline(lv_event_t* e) {
    auto* rec = static_cast<SubRecord*>(lv_event_get_user_data(e));
    if (rec && rec->cb) rec->cb(rec->context);
}

int Subscribe(void* widget, lv_event_code_t code,
              endpoint_ui_event_callback cb, void* context) {
    return Call([&] {
        if (!widget) ThrowInvalidOperation("widget is null");
        if (!cb) ThrowInvalidOperation("callback is null");
        auto* rec = new SubRecord{cb, context};
        lv_obj_add_event_cb(AsObj(widget), EventTrampoline, code, rec);
    });
}

}  // namespace

// =========================================================================
// Lifecycle
// =========================================================================

ENDPOINT_API int endpoint_ui_initialize(void) {
    return Call([] { UI::Initialize(); });
}

ENDPOINT_API int endpoint_ui_run(void) {
    return Call([] { UI::Run(); });
}

ENDPOINT_API int endpoint_ui_stop(void) {
    return Call([] { UI::Stop(); });
}

ENDPOINT_API int endpoint_ui_touch_attach(void) {
    return Call([] { UI::TouchAttach(); });
}

ENDPOINT_API int endpoint_ui_touch_feed(int x, int y, int pressed) {
    return Call([&] { UI::TouchFeed(x, y, pressed != 0); });
}

// =========================================================================
// Common widget operations
// =========================================================================

ENDPOINT_API int endpoint_ui_widget_destroy(void* widget) {
    return Call([&] {
        if (widget) lv_obj_delete(AsObj(widget));
    });
}

ENDPOINT_API int endpoint_ui_widget_set_pos(void* widget, int x, int y) {
    return Call([&] { lv_obj_set_pos(AsObj(widget), x, y); });
}

ENDPOINT_API int endpoint_ui_widget_set_x(void* widget, int x) {
    return Call([&] { lv_obj_set_x(AsObj(widget), x); });
}

ENDPOINT_API int endpoint_ui_widget_set_y(void* widget, int y) {
    return Call([&] { lv_obj_set_y(AsObj(widget), y); });
}

ENDPOINT_API int endpoint_ui_widget_set_width(void* widget, int width) {
    return Call([&] { lv_obj_set_width(AsObj(widget), width); });
}

ENDPOINT_API int endpoint_ui_widget_set_height(void* widget, int height) {
    return Call([&] { lv_obj_set_height(AsObj(widget), height); });
}

ENDPOINT_API int endpoint_ui_widget_get_pos(void* widget, int* x, int* y) {
    return Call([&] {
        if (x) *x = lv_obj_get_x(AsObj(widget));
        if (y) *y = lv_obj_get_y(AsObj(widget));
    });
}

ENDPOINT_API int endpoint_ui_widget_set_size(void* widget, int width, int height) {
    return Call([&] { lv_obj_set_size(AsObj(widget), width, height); });
}

ENDPOINT_API int endpoint_ui_widget_get_size(void* widget, int* width, int* height) {
    return Call([&] {
        if (width)  *width  = lv_obj_get_width(AsObj(widget));
        if (height) *height = lv_obj_get_height(AsObj(widget));
    });
}

ENDPOINT_API int endpoint_ui_widget_set_visible(void* widget, int visible) {
    return Call([&] {
        if (visible) lv_obj_remove_flag(AsObj(widget), LV_OBJ_FLAG_HIDDEN);
        else         lv_obj_add_flag(AsObj(widget), LV_OBJ_FLAG_HIDDEN);
    });
}

ENDPOINT_API int endpoint_ui_widget_get_visible(void* widget, int* visible) {
    return Call([&] {
        if (visible) *visible = !lv_obj_has_flag(AsObj(widget), LV_OBJ_FLAG_HIDDEN);
    });
}

// =========================================================================
// Events
// =========================================================================

ENDPOINT_API int endpoint_ui_subscribe_clicked(void* widget,
                                               endpoint_ui_event_callback cb, void* context) {
    return Subscribe(widget, LV_EVENT_CLICKED, cb, context);
}

ENDPOINT_API int endpoint_ui_subscribe_value_changed(void* widget,
                                                     endpoint_ui_event_callback cb, void* context) {
    return Subscribe(widget, LV_EVENT_VALUE_CHANGED, cb, context);
}

// =========================================================================
// Screen
// =========================================================================

ENDPOINT_API int endpoint_ui_screen_create(void** out) {
    return Call([&] {
        if (!out) ThrowInvalidOperation("out is null");
        *out = lv_obj_create(nullptr);
    });
}

ENDPOINT_API int endpoint_ui_screen_load(void* screen) {
    return Call([&] { lv_screen_load(AsObj(screen)); });
}

ENDPOINT_API int endpoint_ui_screen_active(void** out) {
    return Call([&] {
        if (!out) ThrowInvalidOperation("out is null");
        *out = lv_screen_active();
    });
}

// =========================================================================
// Container
// =========================================================================

ENDPOINT_API int endpoint_ui_container_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        auto* obj = lv_obj_create(AsObj(parent));
        lv_obj_set_scrollbar_mode(obj, LV_SCROLLBAR_MODE_OFF);
        lv_obj_remove_flag(obj, LV_OBJ_FLAG_SCROLLABLE);
        lv_obj_set_style_pad_all(obj, 0, LV_PART_MAIN);
        *out = obj;
    });
}

// =========================================================================
// Button
// =========================================================================

namespace {
// A Button is an lv_button_create() object with a child lv_label for its
// text. We stash the label pointer in the button's user_data so text/font
// operations find it quickly without walking children each call. The label
// is created lazily (not at button_create time) because adding a child to
// a freshly-created button before the caller sets its size would trigger
// LVGL's content-size layout and lock the button at 0x0.
lv_obj_t* ButtonLabel(lv_obj_t* btn) {
    auto* label = static_cast<lv_obj_t*>(lv_obj_get_user_data(btn));
    if (!label) {
        label = lv_label_create(btn);
        lv_obj_center(label);
        lv_obj_set_user_data(btn, label);
    }
    return label;
}
}

ENDPOINT_API int endpoint_ui_button_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        auto* btn = lv_button_create(AsObj(parent));
        // Default LVGL button has a drop shadow that leaves a visible gap at
        // the bottom. Our customers expect a flat button; opt out of it.
        lv_obj_set_style_shadow_width(btn, 0, LV_PART_MAIN);
        *out = btn;
    });
}

ENDPOINT_API int endpoint_ui_button_set_text(void* button, const char* text) {
    return Call([&] {
        lv_label_set_text(ButtonLabel(AsObj(button)), text ? text : "");
    });
}

ENDPOINT_API int endpoint_ui_button_set_bg_color(void* button, uint32_t rgb) {
    return Call([&] {
        lv_obj_set_style_bg_color(AsObj(button), ColorFromRgb(rgb), LV_PART_MAIN);
    });
}

ENDPOINT_API int endpoint_ui_button_set_text_color(void* button, uint32_t rgb) {
    return Call([&] {
        lv_obj_set_style_text_color(ButtonLabel(AsObj(button)), ColorFromRgb(rgb), LV_PART_MAIN);
    });
}

ENDPOINT_API int endpoint_ui_button_set_font_size(void* button, int size) {
    return Call([&] {
        lv_obj_set_style_text_font(ButtonLabel(AsObj(button)), FontForSize(size), LV_PART_MAIN);
    });
}

// =========================================================================
// Label
// =========================================================================

ENDPOINT_API int endpoint_ui_label_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        *out = lv_label_create(AsObj(parent));
    });
}

ENDPOINT_API int endpoint_ui_label_set_text(void* label, const char* text) {
    return Call([&] { lv_label_set_text(AsObj(label), text ? text : ""); });
}

ENDPOINT_API int endpoint_ui_label_set_color(void* label, uint32_t rgb) {
    return Call([&] { lv_obj_set_style_text_color(AsObj(label), ColorFromRgb(rgb), LV_PART_MAIN); });
}

ENDPOINT_API int endpoint_ui_label_set_font_size(void* label, int size) {
    return Call([&] { lv_obj_set_style_text_font(AsObj(label), FontForSize(size), LV_PART_MAIN); });
}

// =========================================================================
// Image
// =========================================================================

namespace {
// Caller-provided pixels become part of a per-widget lv_image_dsc_t allocated
// on the heap. We stash it in user_data so subsequent SetSource calls can
// free the old one before replacing. Caller still owns the pixel buffer.
}

ENDPOINT_API int endpoint_ui_image_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        *out = lv_image_create(AsObj(parent));
    });
}

ENDPOINT_API int endpoint_ui_image_set_source(void* image, const uint8_t* rgb565Pixels,
                                              int byteCount, int width, int height) {
    return Call([&] {
        if (!image || !rgb565Pixels) ThrowInvalidOperation("image or pixels is null");
        auto* img = AsObj(image);
        auto* old = static_cast<lv_image_dsc_t*>(lv_obj_get_user_data(img));
        auto* dsc = new lv_image_dsc_t{};
        dsc->header.magic = LV_IMAGE_HEADER_MAGIC;
        dsc->header.cf = LV_COLOR_FORMAT_RGB565;
        dsc->header.flags = 0;
        dsc->header.w = width;
        dsc->header.h = height;
        dsc->header.stride = width * 2;
        dsc->data_size = (uint32_t)byteCount;
        dsc->data = rgb565Pixels;
        lv_image_set_src(img, dsc);
        lv_obj_set_user_data(img, dsc);
        delete old;
    });
}

// =========================================================================
// TextBox
// =========================================================================

ENDPOINT_API int endpoint_ui_textbox_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        auto* ta = lv_textarea_create(AsObj(parent));
        lv_textarea_set_one_line(ta, true);
        *out = ta;
    });
}

ENDPOINT_API int endpoint_ui_textbox_set_text(void* textbox, const char* text) {
    return Call([&] { lv_textarea_set_text(AsObj(textbox), text ? text : ""); });
}

ENDPOINT_API int endpoint_ui_textbox_get_text(void* textbox, char* outBuf, int outSize) {
    return Call([&] {
        if (!outBuf || outSize <= 0) ThrowInvalidOperation("outBuf is null or size is invalid");
        const char* text = lv_textarea_get_text(AsObj(textbox));
        std::snprintf(outBuf, outSize, "%s", text ? text : "");
    });
}

ENDPOINT_API int endpoint_ui_textbox_set_placeholder(void* textbox, const char* placeholder) {
    return Call([&] { lv_textarea_set_placeholder_text(AsObj(textbox), placeholder ? placeholder : ""); });
}

// =========================================================================
// Slider
// =========================================================================

ENDPOINT_API int endpoint_ui_slider_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        *out = lv_slider_create(AsObj(parent));
    });
}

ENDPOINT_API int endpoint_ui_slider_set_range(void* slider, int min, int max) {
    return Call([&] { lv_slider_set_range(AsObj(slider), min, max); });
}

ENDPOINT_API int endpoint_ui_slider_set_value(void* slider, int value) {
    return Call([&] { lv_slider_set_value(AsObj(slider), value, LV_ANIM_OFF); });
}

ENDPOINT_API int endpoint_ui_slider_get_value(void* slider, int* value) {
    return Call([&] {
        if (value) *value = (int)lv_slider_get_value(AsObj(slider));
    });
}

// =========================================================================
// CheckBox
// =========================================================================

ENDPOINT_API int endpoint_ui_checkbox_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        *out = lv_checkbox_create(AsObj(parent));
    });
}

ENDPOINT_API int endpoint_ui_checkbox_set_text(void* checkbox, const char* text) {
    return Call([&] { lv_checkbox_set_text(AsObj(checkbox), text ? text : ""); });
}

ENDPOINT_API int endpoint_ui_checkbox_set_checked(void* checkbox, int checked) {
    return Call([&] {
        if (checked) lv_obj_add_state(AsObj(checkbox), LV_STATE_CHECKED);
        else         lv_obj_remove_state(AsObj(checkbox), LV_STATE_CHECKED);
    });
}

ENDPOINT_API int endpoint_ui_checkbox_get_checked(void* checkbox, int* checked) {
    return Call([&] {
        if (checked) *checked = lv_obj_has_state(AsObj(checkbox), LV_STATE_CHECKED) ? 1 : 0;
    });
}

// =========================================================================
// Keyboard — LVGL's built-in virtual keyboard. Attach to a TextBox and it
// pops rendered chars into the TextBox when keys are tapped; Enter / clear
// are routed by LVGL for free. Position/size defaults to the display's
// bottom half; the caller can override via widget_set_x/y/width/height.
// =========================================================================

namespace {
// Auto-hide on Enter (LV_EVENT_READY) and Close (LV_EVENT_CANCEL). This is
// the natural UX for text input: user taps textbox, types, hits Enter, the
// keyboard tucks away. To show again, flip Visible back on.
void KeyboardHideOnReadyOrCancel(lv_event_t* e) {
    lv_obj_add_flag(lv_event_get_target_obj(e), LV_OBJ_FLAG_HIDDEN);
}
}

ENDPOINT_API int endpoint_ui_keyboard_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        auto* kb = lv_keyboard_create(AsObj(parent));
        lv_obj_add_event_cb(kb, KeyboardHideOnReadyOrCancel, LV_EVENT_READY, nullptr);
        lv_obj_add_event_cb(kb, KeyboardHideOnReadyOrCancel, LV_EVENT_CANCEL, nullptr);
        *out = kb;
    });
}

ENDPOINT_API int endpoint_ui_keyboard_set_textbox(void* keyboard, void* textbox) {
    return Call([&] {
        if (!keyboard) ThrowInvalidOperation("keyboard is null");
        lv_keyboard_set_textarea(AsObj(keyboard), textbox ? AsObj(textbox) : nullptr);
    });
}

// =========================================================================
// Switch — binary on/off toggle. ValueChanged event fires on each flip.
// =========================================================================

ENDPOINT_API int endpoint_ui_switch_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        *out = lv_switch_create(AsObj(parent));
    });
}

ENDPOINT_API int endpoint_ui_switch_set_checked(void* sw, int checked) {
    return Call([&] {
        if (checked) lv_obj_add_state(AsObj(sw), LV_STATE_CHECKED);
        else         lv_obj_remove_state(AsObj(sw), LV_STATE_CHECKED);
    });
}

ENDPOINT_API int endpoint_ui_switch_get_checked(void* sw, int* checked) {
    return Call([&] {
        if (checked) *checked = lv_obj_has_state(AsObj(sw), LV_STATE_CHECKED) ? 1 : 0;
    });
}

// =========================================================================
// ProgressBar — horizontal bar showing a value in [min, max]. No events.
// =========================================================================

ENDPOINT_API int endpoint_ui_progressbar_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        auto* bar = lv_bar_create(AsObj(parent));
        *out = bar;
    });
}

ENDPOINT_API int endpoint_ui_progressbar_set_range(void* bar, int min, int max) {
    return Call([&] { lv_bar_set_range(AsObj(bar), min, max); });
}

ENDPOINT_API int endpoint_ui_progressbar_set_value(void* bar, int value) {
    return Call([&] { lv_bar_set_value(AsObj(bar), value, LV_ANIM_OFF); });
}

ENDPOINT_API int endpoint_ui_progressbar_get_value(void* bar, int* value) {
    return Call([&] {
        if (value) *value = (int)lv_bar_get_value(AsObj(bar));
    });
}

// =========================================================================
// Dropdown — select one item from a list. Options are newline-separated.
// Fires ValueChanged when the selection changes.
// =========================================================================

ENDPOINT_API int endpoint_ui_dropdown_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        *out = lv_dropdown_create(AsObj(parent));
    });
}

ENDPOINT_API int endpoint_ui_dropdown_set_options(void* dd, const char* nlSeparated) {
    return Call([&] {
        lv_dropdown_set_options(AsObj(dd), nlSeparated ? nlSeparated : "");
    });
}

ENDPOINT_API int endpoint_ui_dropdown_set_selected(void* dd, int index) {
    return Call([&] {
        lv_dropdown_set_selected(AsObj(dd), (uint16_t)index);
    });
}

ENDPOINT_API int endpoint_ui_dropdown_get_selected(void* dd, int* index) {
    return Call([&] {
        if (index) *index = (int)lv_dropdown_get_selected(AsObj(dd));
    });
}

// =========================================================================
// RadioButton — styled lv_checkbox with a circular indicator. Same event
// surface as CheckBox (ValueChanged). Grouping is the caller's job: wire
// up each radio's event and clear the others when one is set.
// =========================================================================

ENDPOINT_API int endpoint_ui_radiobutton_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        auto* rb = lv_checkbox_create(AsObj(parent));
        // Make the indicator a circle instead of a square.
        lv_obj_set_style_radius(rb, LV_RADIUS_CIRCLE, LV_PART_INDICATOR);
        *out = rb;
    });
}

ENDPOINT_API int endpoint_ui_radiobutton_set_text(void* rb, const char* text) {
    return Call([&] { lv_checkbox_set_text(AsObj(rb), text ? text : ""); });
}

ENDPOINT_API int endpoint_ui_radiobutton_set_checked(void* rb, int checked) {
    return Call([&] {
        if (checked) lv_obj_add_state(AsObj(rb), LV_STATE_CHECKED);
        else         lv_obj_remove_state(AsObj(rb), LV_STATE_CHECKED);
    });
}

ENDPOINT_API int endpoint_ui_radiobutton_get_checked(void* rb, int* checked) {
    return Call([&] {
        if (checked) *checked = lv_obj_has_state(AsObj(rb), LV_STATE_CHECKED) ? 1 : 0;
    });
}

// =========================================================================
// MessageBox — modal dialog with title, body, and up to two footer buttons.
// Shared the Clicked event via the primary/secondary button handles the
// caller receives from AddButton.
// =========================================================================

ENDPOINT_API int endpoint_ui_messagebox_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        *out = lv_msgbox_create(AsObj(parent));
    });
}

ENDPOINT_API int endpoint_ui_messagebox_set_title(void* mb, const char* title) {
    return Call([&] { lv_msgbox_add_title(AsObj(mb), title ? title : ""); });
}

ENDPOINT_API int endpoint_ui_messagebox_set_body(void* mb, const char* body) {
    return Call([&] { lv_msgbox_add_text(AsObj(mb), body ? body : ""); });
}

// Adds a footer button; returns its handle so the caller can wire a Clicked
// callback via endpoint_ui_subscribe_clicked. Call Hide() in the handler to
// dismiss the dialog.
ENDPOINT_API int endpoint_ui_messagebox_add_button(void* mb, const char* text, void** outButton) {
    return Call([&] {
        if (!outButton) ThrowInvalidOperation("outButton is null");
        *outButton = lv_msgbox_add_footer_button(AsObj(mb), text ? text : "");
    });
}

ENDPOINT_API int endpoint_ui_messagebox_hide(void* mb) {
    return Call([&] { lv_msgbox_close(AsObj(mb)); });
}

// =========================================================================
// ListBox — vertical scrollable list of text items.
// =========================================================================

ENDPOINT_API int endpoint_ui_listbox_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        *out = lv_list_create(AsObj(parent));
    });
}

// Adds a text-only item; returns its handle so the caller can wire per-item
// Clicked callbacks via endpoint_ui_subscribe_clicked.
ENDPOINT_API int endpoint_ui_listbox_add_item(void* lb, const char* text, void** outItem) {
    return Call([&] {
        if (!outItem) ThrowInvalidOperation("outItem is null");
        *outItem = lv_list_add_button(AsObj(lb), nullptr, text ? text : "");
    });
}

ENDPOINT_API int endpoint_ui_listbox_clear(void* lb) {
    return Call([&] { lv_obj_clean(AsObj(lb)); });
}
