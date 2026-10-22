// C API for the UI subsystem — the layer the language bindings call into.
// Hides LVGL from customers: handles are opaque void* (actually lv_obj_t*),
// colors are uint32_t (0xRRGGBB), events are plain function pointers plus a
// context pointer owned by the caller.
//
// Font size → LVGL built-in font. We support the three sizes the designer
// palette exposes: 14, 20, 32. Any other size is coerced to the closest
// supported one; the designer validates on input anyway.

#include <cmath>
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

// Solid background color (opaque). 0x00RRGGBB.
ENDPOINT_API int endpoint_ui_widget_set_bg_color(void* widget, uint32_t rgb) {
    return Call([&] {
        auto* obj = AsObj(widget);
        lv_obj_set_style_bg_color(obj, ColorFromRgb(rgb), LV_PART_MAIN);
        lv_obj_set_style_bg_opa(obj, LV_OPA_COVER, LV_PART_MAIN);
        lv_obj_set_style_bg_grad_dir(obj, LV_GRAD_DIR_NONE, LV_PART_MAIN);
    });
}

// Linear gradient background from start to end color. direction: 0=vertical
// (top=start, bottom=end), 1=horizontal (left=start, right=end).
ENDPOINT_API int endpoint_ui_widget_set_bg_gradient(void* widget,
                                                    uint32_t rgb_start, uint32_t rgb_end,
                                                    int direction) {
    return Call([&] {
        auto* obj = AsObj(widget);
        lv_obj_set_style_bg_color(obj, ColorFromRgb(rgb_start), LV_PART_MAIN);
        lv_obj_set_style_bg_grad_color(obj, ColorFromRgb(rgb_end), LV_PART_MAIN);
        lv_obj_set_style_bg_grad_dir(obj, direction == 1 ? LV_GRAD_DIR_HOR : LV_GRAD_DIR_VER, LV_PART_MAIN);
        lv_obj_set_style_bg_opa(obj, LV_OPA_COVER, LV_PART_MAIN);
    });
}

// Background image from raw RGB565 pixels (little-endian).
ENDPOINT_API int endpoint_ui_widget_set_bg_image(void* widget,
                                                 const uint8_t* data, int data_len,
                                                 int width, int height) {
    return Call([&] {
        if (!data || data_len <= 0) ThrowInvalidOperation("data is null or empty");
        if (data_len != width * height * 2) ThrowInvalidOperation("data_len does not match width*height*2 (RGB565)");
        auto* obj = AsObj(widget);
        // LVGL needs an lv_image_dsc_t that outlives the widget. Store it on
        // the widget itself (user data), freeing any previous one.
        auto* prev = reinterpret_cast<lv_image_dsc_t*>(lv_obj_get_user_data(obj));
        if (prev) { delete prev; }
        auto* desc = new lv_image_dsc_t{};
        desc->header.cf = LV_COLOR_FORMAT_RGB565;
        desc->header.w = width;
        desc->header.h = height;
        desc->data_size = data_len;
        desc->data = data;
        lv_obj_set_user_data(obj, desc);
        lv_obj_set_style_bg_image_src(obj, desc, LV_PART_MAIN);
        lv_obj_set_style_bg_image_tiled(obj, true, LV_PART_MAIN);
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
        // Match WPF: a Container is a plain rectangle, not rounded or bordered.
        lv_obj_set_style_border_width(obj, 0, LV_PART_MAIN);
        lv_obj_set_style_radius(obj, 0, LV_PART_MAIN);
        *out = obj;
    });
}

// =========================================================================
// Border — a Container with styled border + corner radius + background.
// Standard WPF Border semantics: BorderThickness, BorderBrush, CornerRadius,
// Background, Padding. Children positioned absolute like Container.
// =========================================================================

ENDPOINT_API int endpoint_ui_border_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        auto* obj = lv_obj_create(AsObj(parent));
        lv_obj_set_scrollbar_mode(obj, LV_SCROLLBAR_MODE_OFF);
        lv_obj_remove_flag(obj, LV_OBJ_FLAG_SCROLLABLE);
        lv_obj_set_style_pad_all(obj, 0, LV_PART_MAIN);
        // Border defaults match the WPF Border: 1px dark border, no radius.
        lv_obj_set_style_border_width(obj, 1, LV_PART_MAIN);
        lv_obj_set_style_border_color(obj, lv_color_hex(0x808080), LV_PART_MAIN);
        lv_obj_set_style_radius(obj, 0, LV_PART_MAIN);
        *out = obj;
    });
}

ENDPOINT_API int endpoint_ui_border_set_thickness(void* border, int px) {
    return Call([&] { lv_obj_set_style_border_width(AsObj(border), px, LV_PART_MAIN); });
}

ENDPOINT_API int endpoint_ui_border_set_brush(void* border, uint32_t rgb) {
    return Call([&] { lv_obj_set_style_border_color(AsObj(border), ColorFromRgb(rgb), LV_PART_MAIN); });
}

ENDPOINT_API int endpoint_ui_border_set_corner_radius(void* border, int px) {
    return Call([&] { lv_obj_set_style_radius(AsObj(border), px, LV_PART_MAIN); });
}

ENDPOINT_API int endpoint_ui_border_set_padding(void* border, int px) {
    return Call([&] { lv_obj_set_style_pad_all(AsObj(border), px, LV_PART_MAIN); });
}

// =========================================================================
// StackPanel — WPF-style flex layout: children stack vertically or
// horizontally. The `spacing` is the gap between children; per-child
// margins are set via endpoint_ui_widget_set_margin on the child itself.
// orientation: 0 = Vertical (column), 1 = Horizontal (row).
// =========================================================================

ENDPOINT_API int endpoint_ui_stackpanel_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        auto* obj = lv_obj_create(AsObj(parent));
        lv_obj_set_scrollbar_mode(obj, LV_SCROLLBAR_MODE_OFF);
        lv_obj_remove_flag(obj, LV_OBJ_FLAG_SCROLLABLE);
        lv_obj_set_style_pad_all(obj, 0, LV_PART_MAIN);
        lv_obj_set_style_border_width(obj, 0, LV_PART_MAIN);
        lv_obj_set_style_bg_opa(obj, LV_OPA_TRANSP, LV_PART_MAIN);
        lv_obj_set_layout(obj, LV_LAYOUT_FLEX);
        lv_obj_set_flex_flow(obj, LV_FLEX_FLOW_COLUMN);
        lv_obj_set_flex_align(obj, LV_FLEX_ALIGN_START, LV_FLEX_ALIGN_START, LV_FLEX_ALIGN_START);
        *out = obj;
    });
}

ENDPOINT_API int endpoint_ui_stackpanel_set_orientation(void* panel, int orientation) {
    return Call([&] {
        lv_obj_set_flex_flow(AsObj(panel),
            orientation == 1 ? LV_FLEX_FLOW_ROW : LV_FLEX_FLOW_COLUMN);
    });
}

ENDPOINT_API int endpoint_ui_stackpanel_set_spacing(void* panel, int px) {
    return Call([&] {
        // In a column, children stack vertically — the inter-child gap is pad_row.
        // In a row, it's pad_column. We set both so toggling orientation keeps the
        // gap. LVGL ignores the irrelevant axis.
        lv_obj_set_style_pad_row(AsObj(panel), px, LV_PART_MAIN);
        lv_obj_set_style_pad_column(AsObj(panel), px, LV_PART_MAIN);
    });
}

// Per-child margin (WPF Thickness). Works on any widget, in any parent.
ENDPOINT_API int endpoint_ui_widget_set_margin(void* widget, int left, int top, int right, int bottom) {
    return Call([&] {
        auto* obj = AsObj(widget);
        lv_obj_set_style_margin_left(obj, left, LV_PART_MAIN);
        lv_obj_set_style_margin_top(obj, top, LV_PART_MAIN);
        lv_obj_set_style_margin_right(obj, right, LV_PART_MAIN);
        lv_obj_set_style_margin_bottom(obj, bottom, LV_PART_MAIN);
    });
}

// =========================================================================
// Grid — WPF-style Grid with ColumnDefinitions + RowDefinitions. Column /
// Row sizes are passed as parallel int arrays, where negative values mean
// "star" (fractional) and positive mean "pixel-fixed". Example: for
// ColumnDefinitions="40,*,2*" pass [40, -1, -2]. The arrays are owned by
// LVGL for the widget's lifetime; we copy them into widget storage.
// =========================================================================

namespace {
struct GridDsc {
    int32_t* cols;
    int32_t* rows;
};
void FreeGridDsc(lv_event_t* e) {
    auto* dsc = static_cast<GridDsc*>(lv_event_get_user_data(e));
    if (!dsc) return;
    delete[] dsc->cols;
    delete[] dsc->rows;
    delete dsc;
}
int32_t SizeToLv(int v) {
    // Positive = fixed px; negative = fractional (star). Our wire format
    // puts "*" as -1, "2*" as -2, etc. LVGL uses LV_GRID_FR(n) which is a
    // large negative sentinel internally — the lv_grid_fr helper handles it.
    return v < 0 ? LV_GRID_FR(-v) : (int32_t)v;
}
}

ENDPOINT_API int endpoint_ui_grid_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        auto* obj = lv_obj_create(AsObj(parent));
        lv_obj_set_scrollbar_mode(obj, LV_SCROLLBAR_MODE_OFF);
        lv_obj_remove_flag(obj, LV_OBJ_FLAG_SCROLLABLE);
        lv_obj_set_style_pad_all(obj, 0, LV_PART_MAIN);
        lv_obj_set_style_border_width(obj, 0, LV_PART_MAIN);
        lv_obj_set_style_bg_opa(obj, LV_OPA_TRANSP, LV_PART_MAIN);
        lv_obj_set_layout(obj, LV_LAYOUT_GRID);
        *out = obj;
    });
}

ENDPOINT_API int endpoint_ui_grid_set_definitions(void* panel,
                                                  const int* cols, int col_count,
                                                  const int* rows, int row_count) {
    return Call([&] {
        if (col_count <= 0 || row_count <= 0) ThrowInvalidOperation("Grid needs at least 1 row and 1 column");
        auto* obj = AsObj(panel);
        auto* prev = static_cast<GridDsc*>(lv_obj_get_user_data(obj));
        if (prev) {
            lv_obj_remove_event_cb_with_user_data(obj, FreeGridDsc, prev);
            delete[] prev->cols; delete[] prev->rows; delete prev;
        }
        auto* dsc = new GridDsc{};
        dsc->cols = new int32_t[col_count + 1];
        dsc->rows = new int32_t[row_count + 1];
        for (int i = 0; i < col_count; i++) dsc->cols[i] = SizeToLv(cols[i]);
        dsc->cols[col_count] = LV_GRID_TEMPLATE_LAST;
        for (int i = 0; i < row_count; i++) dsc->rows[i] = SizeToLv(rows[i]);
        dsc->rows[row_count] = LV_GRID_TEMPLATE_LAST;
        lv_obj_set_grid_dsc_array(obj, dsc->cols, dsc->rows);
        lv_obj_set_user_data(obj, dsc);
        lv_obj_add_event_cb(obj, FreeGridDsc, LV_EVENT_DELETE, dsc);
    });
}

// Attached-property setter for a child inside a Grid.
ENDPOINT_API int endpoint_ui_grid_set_cell(void* child, int column, int column_span,
                                           int row, int row_span) {
    return Call([&] {
        lv_obj_set_grid_cell(AsObj(child),
            LV_GRID_ALIGN_STRETCH, column, column_span > 0 ? column_span : 1,
            LV_GRID_ALIGN_STRETCH, row, row_span > 0 ? row_span : 1);
    });
}

// =========================================================================
// DockPanel — WPF dock layout. The library has no LVGL-native dock; the
// higher-level language layer computes each child's position from the
// dock side + remaining area, then calls lv_obj_set_pos/size. We expose
// the raw container; the caller lays children out before (or after) adding
// them.
// =========================================================================

ENDPOINT_API int endpoint_ui_dockpanel_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        auto* obj = lv_obj_create(AsObj(parent));
        lv_obj_set_scrollbar_mode(obj, LV_SCROLLBAR_MODE_OFF);
        lv_obj_remove_flag(obj, LV_OBJ_FLAG_SCROLLABLE);
        lv_obj_set_style_pad_all(obj, 0, LV_PART_MAIN);
        lv_obj_set_style_border_width(obj, 0, LV_PART_MAIN);
        lv_obj_set_style_bg_opa(obj, LV_OPA_TRANSP, LV_PART_MAIN);
        *out = obj;
    });
}

// =========================================================================
// Menu / MenuItem — a horizontal bar of top-level buttons; each top item
// is backed by an lv_dropdown so clicks open its sub-items. Non-leaf
// MenuItems are dropdowns; leaf ones are plain buttons (handled by the
// Menu widget composition in the three-language layer).
// =========================================================================

ENDPOINT_API int endpoint_ui_menu_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        auto* obj = lv_obj_create(AsObj(parent));
        lv_obj_set_scrollbar_mode(obj, LV_SCROLLBAR_MODE_OFF);
        lv_obj_remove_flag(obj, LV_OBJ_FLAG_SCROLLABLE);
        lv_obj_set_style_pad_all(obj, 0, LV_PART_MAIN);
        lv_obj_set_style_border_width(obj, 0, LV_PART_MAIN);
        // Flat menu bar: no rounded corners, matches TinyCLR's edge-to-edge strip.
        lv_obj_set_style_radius(obj, 0, LV_PART_MAIN);
        lv_obj_set_style_bg_opa(obj, LV_OPA_COVER, LV_PART_MAIN);
        lv_obj_set_style_bg_color(obj, lv_color_hex(0xF5F5F7), LV_PART_MAIN);
        lv_obj_set_layout(obj, LV_LAYOUT_FLEX);
        lv_obj_set_flex_flow(obj, LV_FLEX_FLOW_ROW);
        lv_obj_set_flex_align(obj, LV_FLEX_ALIGN_START, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER);
        *out = obj;
    });
}

ENDPOINT_API int endpoint_ui_menu_set_background(void* menu, uint32_t rgb) {
    return Call([&] { lv_obj_set_style_bg_color(AsObj(menu), ColorFromRgb(rgb), LV_PART_MAIN); });
}

// A leaf menu item: a button with the given text, parented to the Menu.
// Returns the button handle; subscribe its Clicked event to run commands.
ENDPOINT_API int endpoint_ui_menuitem_create(void* parent, const char* header, int item_width, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        auto* btn = lv_button_create(AsObj(parent));
        lv_obj_set_size(btn, item_width > 0 ? item_width : LV_SIZE_CONTENT, LV_SIZE_CONTENT);
        lv_obj_set_style_bg_opa(btn, LV_OPA_TRANSP, LV_PART_MAIN);
        lv_obj_set_style_shadow_width(btn, 0, LV_PART_MAIN);
        lv_obj_set_style_pad_hor(btn, 10, LV_PART_MAIN);
        lv_obj_set_style_pad_ver(btn, 4, LV_PART_MAIN);
        auto* label = lv_label_create(btn);
        lv_label_set_text(label, header ? header : "");
        lv_obj_set_user_data(btn, label);
        *out = btn;
    });
}

// A non-leaf menu item: an lv_dropdown whose entries are its sub-items'
// headers (newline-joined). We put the header text as options[0] so the
// closed dropdown shows the header. Sub-items start at index 1; callers map
// (Selected - 1) back to their sub-item array when wiring ValueChanged.
//
// Deliberately NOT using lv_dropdown_set_text — it stores the pointer
// without copying, which bites hard when the heap recycles the buffer we
// pass (two dropdowns can end up aliasing the same memory and show the
// same text). lv_dropdown_set_options takes its own copy.
ENDPOINT_API int endpoint_ui_menuitem_create_dropdown(void* parent, const char* header,
                                                     const char* newline_joined_subitems,
                                                     int item_width, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        auto* dd = lv_dropdown_create(AsObj(parent));
        if (item_width > 0) lv_obj_set_width(dd, item_width);
        lv_obj_set_style_bg_opa(dd, LV_OPA_TRANSP, LV_PART_MAIN);

        const char* h = header ? header : "";
        const char* j = newline_joined_subitems ? newline_joined_subitems : "";
        size_t hlen = strlen(h);
        size_t jlen = strlen(j);
        auto* combined = static_cast<char*>(malloc(hlen + jlen + 2));
        memcpy(combined, h, hlen);
        combined[hlen] = '\n';
        memcpy(combined + hlen + 1, j, jlen);
        combined[hlen + jlen + 1] = '\0';
        lv_dropdown_set_options(dd, combined);
        free(combined);
        lv_dropdown_set_selected(dd, 0);
        // TinyCLR Menu-style: no down-chevron glyph and no border — top-level
        // item is just tappable text that reveals a floating list on tap.
        lv_dropdown_set_symbol(dd, NULL);
        lv_obj_set_style_border_width(dd, 0, LV_PART_MAIN);
        *out = dd;
    });
}

// =========================================================================
// Calendar
// =========================================================================

ENDPOINT_API int endpoint_ui_calendar_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        *out = lv_calendar_create(AsObj(parent));
    });
}

ENDPOINT_API int endpoint_ui_calendar_set_showed_date(void* cal, int year, int month) {
    return Call([&] { lv_calendar_set_showed_date(AsObj(cal), year, month); });
}

ENDPOINT_API int endpoint_ui_calendar_set_today(void* cal, int year, int month, int day) {
    return Call([&] { lv_calendar_set_today_date(AsObj(cal), year, month, day); });
}

// =========================================================================
// DataGrid (lv_table). Simple grid of text cells. Users fill cells in code.
// =========================================================================

ENDPOINT_API int endpoint_ui_datagrid_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        *out = lv_table_create(AsObj(parent));
    });
}

ENDPOINT_API int endpoint_ui_datagrid_set_size(void* grid, int cols, int rows) {
    return Call([&] {
        lv_table_set_column_count(AsObj(grid), cols);
        lv_table_set_row_count(AsObj(grid), rows);
    });
}

ENDPOINT_API int endpoint_ui_datagrid_set_cell(void* grid, int row, int col, const char* text) {
    return Call([&] { lv_table_set_cell_value(AsObj(grid), row, col, text ? text : ""); });
}

ENDPOINT_API int endpoint_ui_datagrid_set_column_width(void* grid, int col, int px) {
    return Call([&] { lv_table_set_column_width(AsObj(grid), col, px); });
}

// =========================================================================
// TreeView — faked with lv_list, items indented by depth.
// =========================================================================

ENDPOINT_API int endpoint_ui_treeview_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        *out = lv_list_create(AsObj(parent));
    });
}

// Add one row with indentation (depth * 20 px of left margin).
ENDPOINT_API int endpoint_ui_treeview_add_node(void* tree, int depth, int is_expanded,
                                               int has_children, const char* header, void** out_node) {
    return Call([&] {
        auto* list = AsObj(tree);
        // Compose a prefix: nothing / '-' / '+' / '•' depending on state.
        char buf[256];
        const char* glyph = has_children ? (is_expanded ? "- " : "+ ") : "  ";
        snprintf(buf, sizeof(buf), "%s%s", glyph, header ? header : "");
        auto* btn = lv_list_add_button(list, NULL, buf);
        lv_obj_set_style_pad_left(btn, depth * 20, LV_PART_MAIN);
        if (out_node) *out_node = btn;
    });
}

ENDPOINT_API int endpoint_ui_treeview_clear(void* tree) {
    return Call([&] { lv_obj_clean(AsObj(tree)); });
}

// =========================================================================
// Chart (lv_chart) — line chart with a single series. Simple TinyCLR-style
// API: create, set range, set point count, write points.
// =========================================================================

namespace {
struct ChartState {
    lv_chart_series_t* series;
};
void FreeChartState(lv_event_t* e) {
    auto* s = static_cast<ChartState*>(lv_event_get_user_data(e));
    if (s) delete s;
}
}

ENDPOINT_API int endpoint_ui_chart_create(void* parent, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        auto* chart = lv_chart_create(AsObj(parent));
        lv_chart_set_type(chart, LV_CHART_TYPE_LINE);
        lv_chart_set_update_mode(chart, LV_CHART_UPDATE_MODE_SHIFT);
        auto* st = new ChartState{};
        st->series = lv_chart_add_series(chart, lv_color_hex(0x1E88E5), LV_CHART_AXIS_PRIMARY_Y);
        lv_obj_set_user_data(chart, st);
        lv_obj_add_event_cb(chart, FreeChartState, LV_EVENT_DELETE, st);
        *out = chart;
    });
}

// mode: 0 = line, 1 = rectangle (bar), 2 = area (line with fill).
ENDPOINT_API int endpoint_ui_chart_set_mode(void* chart, int mode) {
    return Call([&] {
        auto* obj = AsObj(chart);
        auto* st = static_cast<ChartState*>(lv_obj_get_user_data(obj));
        switch (mode) {
            case 1:  // bar
                lv_chart_set_type(obj, LV_CHART_TYPE_BAR);
                break;
            case 2:  // area
                lv_chart_set_type(obj, LV_CHART_TYPE_LINE);
                if (st && st->series) {
                    // Fill under the line gives the "area" look.
                    lv_obj_set_style_bg_opa(obj, LV_OPA_30, LV_PART_ITEMS);
                }
                break;
            default:
                lv_chart_set_type(obj, LV_CHART_TYPE_LINE);
                break;
        }
    });
}

ENDPOINT_API int endpoint_ui_chart_set_colors(void* chart, uint32_t point_color,
                                              uint32_t area_color, uint32_t label_color) {
    return Call([&] {
        auto* obj = AsObj(chart);
        auto* st = static_cast<ChartState*>(lv_obj_get_user_data(obj));
        if (st && st->series) lv_chart_set_series_color(obj, st->series, ColorFromRgb(point_color));
        lv_obj_set_style_bg_color(obj, ColorFromRgb(area_color), LV_PART_ITEMS);
        lv_obj_set_style_text_color(obj, ColorFromRgb(label_color), LV_PART_MAIN);
        lv_obj_invalidate(obj);
    });
}

ENDPOINT_API int endpoint_ui_chart_set_division(void* chart, int y_divisions) {
    return Call([&] { lv_chart_set_div_line_count(AsObj(chart), y_divisions, 5); });
}

ENDPOINT_API int endpoint_ui_chart_set_points(void* chart, const int* values, int count) {
    return Call([&] {
        auto* obj = AsObj(chart);
        auto* st = static_cast<ChartState*>(lv_obj_get_user_data(obj));
        if (!st || !st->series) ThrowInvalidOperation("Chart series missing");
        if (count <= 0) return;
        lv_chart_set_point_count(obj, (uint32_t)count);
        for (int i = 0; i < count; i++) {
            lv_chart_set_value_by_id(obj, st->series, i, (int32_t)values[i]);
        }
        lv_chart_refresh(obj);
    });
}

// =========================================================================
// Gauge — composed of lv_scale (dial) + lv_line (needle). Owned storage
// holds the needle's two points and refreshes them when Value changes.
// =========================================================================

namespace {
struct GaugeState {
    lv_obj_t* scale;
    lv_obj_t* needle;
    int min_value;
    int max_value;
    int sweep_angle;
    float needle_center_y;  // 0..1
    float needle_length;    // 0..1
    int width;
    int height;
    lv_point_precise_t needle_points[2];
};
void FreeGaugeState(lv_event_t* e) {
    auto* s = static_cast<GaugeState*>(lv_event_get_user_data(e));
    if (s) delete s;
}

void UpdateNeedle(lv_obj_t* gauge_obj, GaugeState* st, int value) {
    if (!st) return;
    int cx = st->width / 2;
    int cy = (int)(st->height * st->needle_center_y);
    float length = st->needle_length * (float)(st->width < st->height ? st->width : st->height) / 2.0f;
    // Map value to angle. SweepAngle is the total sweep; the dial is centred
    // vertically (needle points up at mid-value), rotated clockwise from the
    // left edge of the sweep.
    float range = (float)(st->max_value - st->min_value);
    if (range <= 0) range = 1;
    float t = ((float)(value - st->min_value)) / range;
    if (t < 0) t = 0; if (t > 1) t = 1;
    float start = -(float)st->sweep_angle / 2.0f - 90.0f;  // left edge, degrees
    float angle_deg = start + t * (float)st->sweep_angle;
    float angle_rad = angle_deg * 3.14159265f / 180.0f;
    int tx = cx + (int)(length * cosf(angle_rad));
    int ty = cy + (int)(length * sinf(angle_rad));
    st->needle_points[0].x = cx;
    st->needle_points[0].y = cy;
    st->needle_points[1].x = tx;
    st->needle_points[1].y = ty;
    lv_line_set_points(st->needle, st->needle_points, 2);
    (void)gauge_obj;
}
}

ENDPOINT_API int endpoint_ui_gauge_create(void* parent, int width, int height, void** out) {
    return Call([&] {
        if (!parent || !out) ThrowInvalidOperation("parent or out is null");
        auto* holder = lv_obj_create(AsObj(parent));
        lv_obj_set_size(holder, width, height);
        lv_obj_set_scrollbar_mode(holder, LV_SCROLLBAR_MODE_OFF);
        lv_obj_remove_flag(holder, LV_OBJ_FLAG_SCROLLABLE);
        lv_obj_set_style_pad_all(holder, 0, LV_PART_MAIN);
        lv_obj_set_style_border_width(holder, 0, LV_PART_MAIN);
        lv_obj_set_style_bg_opa(holder, LV_OPA_TRANSP, LV_PART_MAIN);

        auto* scale = lv_scale_create(holder);
        lv_obj_set_size(scale, width, height);
        lv_scale_set_mode(scale, LV_SCALE_MODE_ROUND_INNER);
        lv_scale_set_total_tick_count(scale, 11);
        lv_scale_set_major_tick_every(scale, 2);
        lv_scale_set_label_show(scale, true);
        lv_scale_set_range(scale, 0, 100);
        lv_scale_set_angle_range(scale, 270);
        lv_scale_set_rotation(scale, 135);

        auto* needle = lv_line_create(holder);
        lv_obj_set_style_line_color(needle, lv_color_hex(0xD32F2F), LV_PART_MAIN);
        lv_obj_set_style_line_width(needle, 3, LV_PART_MAIN);
        lv_obj_set_style_line_rounded(needle, true, LV_PART_MAIN);

        auto* st = new GaugeState{};
        st->scale = scale;
        st->needle = needle;
        st->min_value = 0;
        st->max_value = 100;
        st->sweep_angle = 270;
        st->needle_center_y = 0.5f;
        st->needle_length = 0.7f;
        st->width = width;
        st->height = height;
        UpdateNeedle(holder, st, 50);
        lv_obj_set_user_data(holder, st);
        lv_obj_add_event_cb(holder, FreeGaugeState, LV_EVENT_DELETE, st);
        *out = holder;
    });
}

ENDPOINT_API int endpoint_ui_gauge_set_range(void* gauge, int min, int max) {
    return Call([&] {
        auto* st = static_cast<GaugeState*>(lv_obj_get_user_data(AsObj(gauge)));
        if (!st) ThrowInvalidOperation("Gauge state missing");
        st->min_value = min;
        st->max_value = max;
        lv_scale_set_range(st->scale, min, max);
    });
}

ENDPOINT_API int endpoint_ui_gauge_set_value(void* gauge, int value) {
    return Call([&] {
        auto* holder = AsObj(gauge);
        auto* st = static_cast<GaugeState*>(lv_obj_get_user_data(holder));
        if (!st) ThrowInvalidOperation("Gauge state missing");
        UpdateNeedle(holder, st, value);
    });
}

ENDPOINT_API int endpoint_ui_gauge_set_sweep(void* gauge, int sweep_angle) {
    return Call([&] {
        auto* holder = AsObj(gauge);
        auto* st = static_cast<GaugeState*>(lv_obj_get_user_data(holder));
        if (!st) ThrowInvalidOperation("Gauge state missing");
        st->sweep_angle = sweep_angle;
        lv_scale_set_angle_range(st->scale, sweep_angle);
        // Rotation points the scale's start; for a sweep centred on "up" the
        // start is 270° minus half the sweep (LVGL rotation 0 = right).
        lv_scale_set_rotation(st->scale, 270 - sweep_angle / 2);
    });
}

ENDPOINT_API int endpoint_ui_gauge_set_needle_shape(void* gauge, int center_y_1000, int length_1000) {
    // Pass fractions as thousandths (so caller doesn't marshal floats).
    return Call([&] {
        auto* holder = AsObj(gauge);
        auto* st = static_cast<GaugeState*>(lv_obj_get_user_data(holder));
        if (!st) ThrowInvalidOperation("Gauge state missing");
        st->needle_center_y = (float)center_y_1000 / 1000.0f;
        st->needle_length   = (float)length_1000   / 1000.0f;
    });
}

ENDPOINT_API int endpoint_ui_gauge_show_scale(void* gauge, int visible) {
    return Call([&] {
        auto* st = static_cast<GaugeState*>(lv_obj_get_user_data(AsObj(gauge)));
        if (!st) ThrowInvalidOperation("Gauge state missing");
        if (visible) lv_obj_remove_flag(st->scale, LV_OBJ_FLAG_HIDDEN);
        else         lv_obj_add_flag(st->scale, LV_OBJ_FLAG_HIDDEN);
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
        // Zero padding so a child's (X, Y) is relative to the button's own
        // top-left — otherwise the theme's ~5px pad pushes icons off-centre.
        lv_obj_set_style_pad_all(btn, 0, LV_PART_MAIN);
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

// align: 0 = left, 1 = center, 2 = right. Combines with the widget's Width
// so text can center/right-align inside a wider label (WPF TextAlignment).
ENDPOINT_API int endpoint_ui_label_set_text_align(void* label, int align) {
    return Call([&] {
        lv_text_align_t a = align == 1 ? LV_TEXT_ALIGN_CENTER
                          : align == 2 ? LV_TEXT_ALIGN_RIGHT
                          : LV_TEXT_ALIGN_LEFT;
        lv_obj_set_style_text_align(AsObj(label), a, LV_PART_MAIN);
    });
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
