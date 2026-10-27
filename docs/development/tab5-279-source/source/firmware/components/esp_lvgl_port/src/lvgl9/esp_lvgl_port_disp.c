#include "esp_memory_utils.h"
/*
 * SPDX-FileCopyrightText: 2024-2025 Espressif Systems (Shanghai) CO LTD
 *
 * SPDX-License-Identifier: Apache-2.0
 */

#include <string.h>
#include "sdkconfig.h"
#include "esp_log.h"
#include "esp_timer.h"
#include "esp_attr.h"
#include "tab5_rotate_rgb565.h"
#include "tab5_screen_preview_pixels.h"
#include "esp_cache.h"
#include "tab5_display_direct_state.h"
#include "esp_err.h"
#include "esp_check.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "freertos/semphr.h"
#include "esp_heap_caps.h"
#include "esp_idf_version.h"
#include "esp_lcd_panel_io.h"
#include "esp_lcd_panel_ops.h"
#include "esp_lvgl_port.h"
#include "esp_lvgl_port_priv.h"
#if CONFIG_IDF_TARGET_ESP32S3 && ESP_IDF_VERSION >= ESP_IDF_VERSION_VAL(5, 0, 0)
#include "esp_lcd_panel_rgb.h"
#endif

#if (CONFIG_IDF_TARGET_ESP32P4 && ESP_IDF_VERSION >= ESP_IDF_VERSION_VAL(5, 3, 0))
#include "esp_lcd_mipi_dsi.h"
#endif

#if (ESP_IDF_VERSION < ESP_IDF_VERSION_VAL(4, 4, 4)) || (ESP_IDF_VERSION == ESP_IDF_VERSION_VAL(5, 0, 0))
#define LVGL_PORT_HANDLE_FLUSH_READY 0
#else
#define LVGL_PORT_HANDLE_FLUSH_READY 1
#endif

#ifndef CONFIG_LV_DRAW_BUF_ALIGN
#define CONFIG_LV_DRAW_BUF_ALIGN 1
#endif

static const char* TAG = "LVGL";
// Numeric diagnostics only. Updated by the single LVGL display task.
volatile unsigned tab5_display_rotate_us, tab5_display_rotate_peak_us;
volatile unsigned tab5_display_flush_count, tab5_display_flush_errors;
volatile unsigned tab5_display_wait_us, tab5_display_wait_peak_us;
volatile unsigned tab5_display_render_us, tab5_display_frame_flush_us, tab5_display_frame_rotate_us;
// Keep large redraw samples until the next large redraw, so tiny clock or
// status updates cannot overwrite the motion measurements between USB polls.
volatile unsigned tab5_display_large_frames,tab5_display_large_us,tab5_display_large_peak_us;
volatile unsigned tab5_display_large_total_ms,tab5_display_large_pixels,tab5_display_large_period_us;
volatile unsigned tab5_display_large_flush_us,tab5_display_large_rotate_us;
volatile unsigned tab5_display_large_copy_us,tab5_display_large_wait_us,tab5_display_large_yield_us;
volatile unsigned tab5_display_large_parts,tab5_display_large_native_parts;
extern volatile unsigned tab5_native_copy_total_us,tab5_native_cache_hits;

/*******************************************************************************
 * Types definitions
 *******************************************************************************/

typedef struct {
    lvgl_port_disp_type_t disp_type;       /* Display type */
    esp_lcd_panel_io_handle_t io_handle;   /* LCD panel IO handle */
    esp_lcd_panel_handle_t panel_handle;   /* LCD panel handle */
    esp_lcd_panel_handle_t control_handle; /* LCD panel control handle */
    lvgl_port_rotation_cfg_t rotation;     /* Default values of the screen rotation */
    lv_color_t* draw_buffs[3];             /* Display draw buffers */
    uint8_t* oled_buffer;
    lv_display_t* disp_drv; /* LVGL display driver */
    lv_display_rotation_t current_rotation;
    SemaphoreHandle_t trans_sem; /* Idle transfer mutex */
    SemaphoreHandle_t camera_refresh_sem;
    portMUX_TYPE direct_lock;
    tab5_display_direct_state_t direct;
    uint16_t *camera_fbs[2];
    uint32_t native_width, native_height;
    SemaphoreHandle_t preview_gate;
    int64_t yield_at_us, render_start_us;
    unsigned frame_flush_us, frame_rotate_us, frame_pixels;
    unsigned frame_wait_us,frame_yield_us,frame_parts,frame_copy_start,frame_native_start;
    int64_t previous_large_frame_us;
    struct {
        unsigned int monochrome : 1;   /* True, if display is monochrome and using 1bit for 1px */
        unsigned int swap_bytes : 1;   /* Swap bytes in RGB656 (16-bit) before send to LCD driver */
        unsigned int full_refresh : 1; /* Always make the whole screen redrawn */
        unsigned int direct_mode : 1;  /* Use screen-sized buffers and draw to absolute coordinates */
        unsigned int sw_rotate : 1;    /* Use software rotation (slower) or PPA if available */
    } flags;
} lvgl_port_display_ctx_t;

/*******************************************************************************
 * Function definitions
 *******************************************************************************/
static lv_display_t* lvgl_port_add_disp_priv(const lvgl_port_display_cfg_t* disp_cfg,
                                             const lvgl_port_disp_priv_cfg_t* priv_cfg);
#if LVGL_PORT_HANDLE_FLUSH_READY
static bool lvgl_port_flush_io_ready_callback(esp_lcd_panel_io_handle_t panel_io, esp_lcd_panel_io_event_data_t* edata,
                                              void* user_ctx);
#if CONFIG_IDF_TARGET_ESP32S3 && ESP_IDF_VERSION >= ESP_IDF_VERSION_VAL(5, 0, 0)
static bool lvgl_port_flush_rgb_vsync_ready_callback(esp_lcd_panel_handle_t panel_io,
                                                     const esp_lcd_rgb_panel_event_data_t* edata, void* user_ctx);
#endif
#if (CONFIG_IDF_TARGET_ESP32P4 && ESP_IDF_VERSION >= ESP_IDF_VERSION_VAL(5, 3, 0))
static bool lvgl_port_flush_dpi_panel_ready_callback(esp_lcd_panel_handle_t panel_io,
                                                     esp_lcd_dpi_panel_event_data_t* edata, void* user_ctx);
static bool lvgl_port_flush_dpi_vsync_ready_callback(esp_lcd_panel_handle_t panel_io,
                                                     esp_lcd_dpi_panel_event_data_t* edata, void* user_ctx);
static void lvgl_port_flush_wait_callback(lv_display_t* drv);
static bool lvgl_port_camera_refresh_callback(esp_lcd_panel_handle_t panel_io,
                                             esp_lcd_dpi_panel_event_data_t* edata, void* user_ctx);
#endif
#endif
static void lvgl_port_flush_callback(lv_display_t* drv, const lv_area_t* area, uint8_t* color_map);
static void lvgl_port_disp_size_update_callback(lv_event_t* e);
static void lvgl_port_disp_rotation_update(lvgl_port_display_ctx_t* disp_ctx);
static void lvgl_port_display_invalidate_callback(lv_event_t* e);
static void lvgl_port_render_timing_callback(lv_event_t* e);
extern bool tab5_draw_cache_take_native(const void *,const lv_area_t *) __attribute__((weak));

volatile unsigned tab5_rotate_choice,tab5_rotate_baseline_us,tab5_rotate_candidate_us;
static uint16_t *tab5_rotate_workspace;
static unsigned tab5_rotate_variants[4];
static void tab5_rotate_ui(const void *src,void *dst,int width,int height,
                          size_t src_stride,size_t dst_stride,bool clockwise)
{
    // Both paths are CPU-only. Stage the transpose in bounded internal RAM
    // so reads/writes of external RAM become contiguous row copies.
    // Compare each draw-memory/damage shape before LCD submission.
    // Small and odd strips retain the generic safe path.
    bool full_strip=width>=1000&&height>=16&&
        !(((uintptr_t)src|(uintptr_t)dst|src_stride|dst_stride)&3);
    unsigned shape=(width==1280?0:1)+(esp_ptr_internal(src)?2:0);
    unsigned choice=tab5_rotate_variants[shape];
    if(!choice&&full_strip) {
        if(!tab5_rotate_workspace)tab5_rotate_workspace=heap_caps_malloc(8192,MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT);
    }
    if(!choice&&full_strip) {
        unsigned best[4]={UINT32_MAX,UINT32_MAX,UINT32_MAX,UINT32_MAX};
        for(unsigned round=0;round<4;round++)for(unsigned step=0;step<4;step++) {
            unsigned variant=(round+step)%4;
            if(variant==1&&!tab5_rotate_workspace)continue;
            int64_t start=esp_timer_get_time();
            if(variant==3)tab5_rotate_rgb565_output_rows(src,dst,width,height,src_stride,dst_stride,clockwise);
            else if(variant==2)tab5_rotate_rgb565_tiles(src,dst,width,height,src_stride,dst_stride,clockwise,true);
            else if(variant==1)tab5_rotate_rgb565_staged(src,dst,width,height,src_stride,dst_stride,clockwise,tab5_rotate_workspace);
            else tab5_rotate_rgb565(src,dst,width,height,src_stride,dst_stride,clockwise);
            unsigned elapsed=(unsigned)(esp_timer_get_time()-start);
            if(round&&elapsed<best[variant])best[variant]=elapsed;
        }
        unsigned candidate=best[2]<best[1]?2:1;
        if(best[3]<best[candidate])candidate=3;
        tab5_rotate_baseline_us=best[0];tab5_rotate_candidate_us=best[candidate];
        choice=(uint64_t)best[candidate]*10<(uint64_t)best[0]*9?candidate+1:1;
        tab5_rotate_variants[shape]=choice;
        // Retain the bounded 8 KiB workspace for the other damage shapes.

    }
    tab5_rotate_choice=choice;
    if(choice==4)tab5_rotate_rgb565_output_rows(src,dst,width,height,src_stride,dst_stride,clockwise);
    else if(choice==2&&width*height>=4096)
        tab5_rotate_rgb565_staged(src,dst,width,height,src_stride,dst_stride,clockwise,tab5_rotate_workspace);
    else if(choice==3)
        tab5_rotate_rgb565_tiles(src,dst,width,height,src_stride,dst_stride,clockwise,true);
    else tab5_rotate_rgb565(src,dst,width,height,src_stride,dst_stride,clockwise);
}

/*******************************************************************************
 * Public API functions
 *******************************************************************************/

lv_display_t* lvgl_port_add_disp(const lvgl_port_display_cfg_t* disp_cfg)
{
    lvgl_port_lock(0);
    lv_disp_t* disp = lvgl_port_add_disp_priv(disp_cfg, NULL);

    if (disp != NULL) {
        lvgl_port_display_ctx_t* disp_ctx = (lvgl_port_display_ctx_t*)lv_display_get_driver_data(disp);
        /* Set display type */
        disp_ctx->disp_type = LVGL_PORT_DISP_TYPE_OTHER;

        assert(disp_cfg->io_handle != NULL);

#if LVGL_PORT_HANDLE_FLUSH_READY
        const esp_lcd_panel_io_callbacks_t cbs = {
            .on_color_trans_done = lvgl_port_flush_io_ready_callback,
        };
        /* Register done callback */
        esp_lcd_panel_io_register_event_callbacks(disp_ctx->io_handle, &cbs, disp);
#endif

        /* Apply rotation from initial display configuration */
        lvgl_port_disp_rotation_update(disp_ctx);
    }
    lvgl_port_unlock();

    return disp;
}

lv_display_t* lvgl_port_add_disp_dsi(const lvgl_port_display_cfg_t* disp_cfg,
                                     const lvgl_port_display_dsi_cfg_t* dsi_cfg)
{
    assert(dsi_cfg != NULL);
    const lvgl_port_disp_priv_cfg_t priv_cfg = {
        .avoid_tearing = dsi_cfg->flags.avoid_tearing,
    };
    lvgl_port_lock(0);
    lv_disp_t* disp = lvgl_port_add_disp_priv(disp_cfg, &priv_cfg);

    if (disp != NULL) {
        lvgl_port_display_ctx_t* disp_ctx = (lvgl_port_display_ctx_t*)lv_display_get_driver_data(disp);
        /* Set display type */
        disp_ctx->disp_type = LVGL_PORT_DISP_TYPE_DSI;

#if (CONFIG_IDF_TARGET_ESP32P4 && ESP_IDF_VERSION >= ESP_IDF_VERSION_VAL(5, 3, 0))
        esp_lcd_dpi_panel_event_callbacks_t cbs = {0};
        if (dsi_cfg->flags.avoid_tearing) {
            cbs.on_refresh_done = lvgl_port_flush_dpi_vsync_ready_callback;
        } else {
            cbs.on_color_trans_done = lvgl_port_flush_dpi_panel_ready_callback;
            if (!disp_ctx->flags.direct_mode && !disp_ctx->flags.full_refresh) {
                disp_ctx->trans_sem = xSemaphoreCreateBinary();
                disp_ctx->camera_refresh_sem = xSemaphoreCreateBinary();
                if (!disp_ctx->trans_sem || !disp_ctx->camera_refresh_sem) {
                    ESP_LOGE(TAG, "Failed to create LCD transfer semaphore");
                    lvgl_port_remove_disp(disp);
                    lvgl_port_unlock();
                    return NULL;
                }
                // LVGL's default wait spins on disp->flushing. Block instead,
                // leaving CPU time for capture, communication and idle tasks.
                lv_display_set_flush_wait_cb(disp, lvgl_port_flush_wait_callback);
                cbs.on_refresh_done = lvgl_port_camera_refresh_callback;
            }
        }
        /* Register done callback */
        // The scanout interrupt also runs while flash cache is disabled. Pass
        // its internal-RAM context directly; looking it up through LVGL would
        // execute flash code and dereference a potentially PSRAM display.
        esp_err_t callback_result = esp_lcd_dpi_panel_register_event_callbacks(disp_ctx->panel_handle, &cbs, disp_ctx);
        if (callback_result != ESP_OK) {
            ESP_LOGE(TAG, "Failed to register DSI callbacks: %d", callback_result);
            lvgl_port_remove_disp(disp);
            lvgl_port_unlock();
            return NULL;
        }

        /* Apply rotation from initial display configuration */
        lvgl_port_disp_rotation_update(disp_ctx);
#else
        ESP_RETURN_ON_FALSE(false, NULL, TAG, "MIPI-DSI is supported only on ESP32P4 and from IDF 5.3!");
#endif
    }
    lvgl_port_unlock();

    return disp;
}

lv_display_t* lvgl_port_add_disp_rgb(const lvgl_port_display_cfg_t* disp_cfg,
                                     const lvgl_port_display_rgb_cfg_t* rgb_cfg)
{
    lvgl_port_lock(0);
    assert(rgb_cfg != NULL);
    const lvgl_port_disp_priv_cfg_t priv_cfg = {
        .avoid_tearing = rgb_cfg->flags.avoid_tearing,
    };
    lv_disp_t* disp = lvgl_port_add_disp_priv(disp_cfg, &priv_cfg);

    if (disp != NULL) {
        lvgl_port_display_ctx_t* disp_ctx = (lvgl_port_display_ctx_t*)lv_display_get_driver_data(disp);
        /* Set display type */
        disp_ctx->disp_type = LVGL_PORT_DISP_TYPE_RGB;

#if (CONFIG_IDF_TARGET_ESP32S3 && ESP_IDF_VERSION >= ESP_IDF_VERSION_VAL(5, 0, 0))
        /* Register done callback */
        const esp_lcd_rgb_panel_event_callbacks_t vsync_cbs = {
            .on_vsync = lvgl_port_flush_rgb_vsync_ready_callback,
        };

        const esp_lcd_rgb_panel_event_callbacks_t bb_cbs = {
#if ESP_IDF_VERSION >= ESP_IDF_VERSION_VAL(5, 1, 2)
            .on_bounce_frame_finish = lvgl_port_flush_rgb_vsync_ready_callback,
#endif
        };

        if (rgb_cfg->flags.bb_mode && (ESP_IDF_VERSION >= ESP_IDF_VERSION_VAL(5, 1, 2))) {
            ESP_ERROR_CHECK(
                esp_lcd_rgb_panel_register_event_callbacks(disp_ctx->panel_handle, &bb_cbs, disp_ctx->disp_drv));
        } else {
            ESP_ERROR_CHECK(
                esp_lcd_rgb_panel_register_event_callbacks(disp_ctx->panel_handle, &vsync_cbs, disp_ctx->disp_drv));
        }
#else
        ESP_RETURN_ON_FALSE(false, NULL, TAG, "RGB is supported only on ESP32S3 and from IDF 5.0!");
#endif

        /* Apply rotation from initial display configuration */
        lvgl_port_disp_rotation_update(disp_ctx);
    }
    lvgl_port_unlock();

    return disp;
}

esp_err_t lvgl_port_remove_disp(lv_display_t* disp)
{
    assert(disp);
    lvgl_port_display_ctx_t* disp_ctx = (lvgl_port_display_ctx_t*)lv_display_get_driver_data(disp);

    lvgl_port_lock(0);
    lv_disp_remove(disp);
    lvgl_port_unlock();

    if (disp_ctx->preview_gate) vSemaphoreDelete(disp_ctx->preview_gate);
    if (disp_ctx->draw_buffs[0]) {
        free(disp_ctx->draw_buffs[0]);
    }

    if (disp_ctx->draw_buffs[1]) {
        free(disp_ctx->draw_buffs[1]);
    }

    if (disp_ctx->draw_buffs[2]) {
        free(disp_ctx->draw_buffs[2]);
    }

    if (disp_ctx->oled_buffer) {
        free(disp_ctx->oled_buffer);
    }

    if (disp_ctx->trans_sem) {
        vSemaphoreDelete(disp_ctx->trans_sem);
    }
    if (disp_ctx->camera_refresh_sem) {
        vSemaphoreDelete(disp_ctx->camera_refresh_sem);
    }

    free(disp_ctx);

    return ESP_OK;
}

void lvgl_port_flush_ready(lv_display_t* disp)
{
    assert(disp);
    lv_disp_flush_ready(disp);
}

/*******************************************************************************
 * Private functions
 *******************************************************************************/

static lv_display_t* lvgl_port_add_disp_priv(const lvgl_port_display_cfg_t* disp_cfg,
                                             const lvgl_port_disp_priv_cfg_t* priv_cfg)
{
    esp_err_t ret               = ESP_OK;
    lv_display_t* disp          = NULL;
    lv_color_t* buf1            = NULL;
    lv_color_t* buf2            = NULL;
    uint32_t buffer_size        = 0;
    SemaphoreHandle_t trans_sem = NULL;
    assert(disp_cfg != NULL);
    assert(disp_cfg->panel_handle != NULL);
    assert(disp_cfg->buffer_size > 0);
    assert(disp_cfg->hres > 0);
    assert(disp_cfg->vres > 0);

    buffer_size = disp_cfg->buffer_size;

    /* Check supported display color formats */
    ESP_RETURN_ON_FALSE(
        disp_cfg->color_format == 0 || disp_cfg->color_format == LV_COLOR_FORMAT_RGB565 ||
            disp_cfg->color_format == LV_COLOR_FORMAT_RGB888 || disp_cfg->color_format == LV_COLOR_FORMAT_XRGB8888 ||
            disp_cfg->color_format == LV_COLOR_FORMAT_ARGB8888 || disp_cfg->color_format == LV_COLOR_FORMAT_I1,
        NULL, TAG, "Not supported display color format!");

    lv_color_format_t display_color_format =
        (disp_cfg->color_format != 0 ? disp_cfg->color_format : LV_COLOR_FORMAT_RGB565);
    uint8_t color_bytes = lv_color_format_get_size(display_color_format);
    if (disp_cfg->flags.swap_bytes) {
        /* Swap bytes can be used only in RGB565 color format */
        ESP_RETURN_ON_FALSE(display_color_format == LV_COLOR_FORMAT_RGB565, NULL, TAG,
                            "Swap bytes can be used only in display color format RGB565!");
    }

    if (disp_cfg->flags.buff_dma) {
        /* DMA buffer can be used only in RGB565 color format */
        ESP_RETURN_ON_FALSE(display_color_format == LV_COLOR_FORMAT_RGB565, NULL, TAG,
                            "DMA buffer can be used only in display color format RGB565 (not aligned copy)!");
    }

    /* Display context */
    lvgl_port_display_ctx_t* disp_ctx = heap_caps_malloc(sizeof(lvgl_port_display_ctx_t),
                                                       MALLOC_CAP_INTERNAL | MALLOC_CAP_8BIT);
    ESP_GOTO_ON_FALSE(disp_ctx, ESP_ERR_NO_MEM, err, TAG, "Not enough memory for display context allocation!");
    memset(disp_ctx, 0, sizeof(lvgl_port_display_ctx_t));
    disp_ctx->direct_lock = (portMUX_TYPE)portMUX_INITIALIZER_UNLOCKED;
    disp_ctx->preview_gate = xSemaphoreCreateMutex();
    disp_ctx->native_width = disp_cfg->hres;
    disp_ctx->native_height = disp_cfg->vres;
    disp_ctx->io_handle         = disp_cfg->io_handle;
    disp_ctx->panel_handle      = disp_cfg->panel_handle;
    disp_ctx->control_handle    = disp_cfg->control_handle;
    disp_ctx->rotation.swap_xy  = disp_cfg->rotation.swap_xy;
    disp_ctx->rotation.mirror_x = disp_cfg->rotation.mirror_x;
    disp_ctx->rotation.mirror_y = disp_cfg->rotation.mirror_y;
    disp_ctx->flags.swap_bytes  = disp_cfg->flags.swap_bytes;
    disp_ctx->flags.sw_rotate   = disp_cfg->flags.sw_rotate;
    disp_ctx->current_rotation  = LV_DISPLAY_ROTATION_0;

    uint32_t buff_caps = 0;
#if SOC_PSRAM_DMA_CAPABLE == 0
    if (disp_cfg->flags.buff_dma && disp_cfg->flags.buff_spiram) {
        ESP_GOTO_ON_FALSE(false, ESP_ERR_NOT_SUPPORTED, err, TAG,
                          "Alloc DMA capable buffer in SPIRAM is not supported!");
    }
#endif
    if (disp_cfg->flags.buff_dma) {
        buff_caps |= MALLOC_CAP_DMA;
    }
    if (disp_cfg->flags.buff_spiram) {
        buff_caps |= MALLOC_CAP_SPIRAM;
    }
    if (buff_caps == 0) {
        buff_caps |= MALLOC_CAP_DEFAULT;
    }

    /* Use RGB internal buffers for avoid tearing effect */
    if (priv_cfg && priv_cfg->avoid_tearing) {
#if CONFIG_IDF_TARGET_ESP32S3 && ESP_IDF_VERSION >= ESP_IDF_VERSION_VAL(5, 0, 0)
        buffer_size = disp_cfg->hres * disp_cfg->vres;
        ESP_GOTO_ON_ERROR(esp_lcd_rgb_panel_get_frame_buffer(disp_cfg->panel_handle, 2, (void*)&buf1, (void*)&buf2),
                          err, TAG, "Get RGB buffers failed");
#elif CONFIG_IDF_TARGET_ESP32P4 && ESP_IDF_VERSION >= ESP_IDF_VERSION_VAL(5, 3, 0)
        buffer_size = disp_cfg->hres * disp_cfg->vres;
        ESP_GOTO_ON_ERROR(esp_lcd_dpi_panel_get_frame_buffer(disp_cfg->panel_handle, 2, (void*)&buf1, (void*)&buf2),
                          err, TAG, "Get RGB buffers failed");
#endif

        trans_sem = xSemaphoreCreateCounting(1, 0);
        ESP_GOTO_ON_FALSE(trans_sem, ESP_ERR_NO_MEM, err, TAG, "Failed to create transport counting Semaphore");
        disp_ctx->trans_sem = trans_sem;
    } else {
        /* alloc draw buffers used by LVGL */
        /* it's recommended to choose the size of the draw buffer(s) to be at least 1/10 screen sized */
        // P4 PPA output requires whole cache lines; allocation size is unchanged.
        size_t draw_alignment=CONFIG_LV_DRAW_BUF_ALIGN;
#if CONFIG_IDF_TARGET_ESP32P4
        if(draw_alignment<128)draw_alignment=128;
#endif
        buf1 = heap_caps_aligned_alloc(draw_alignment, buffer_size * color_bytes, buff_caps);
        ESP_GOTO_ON_FALSE(buf1, ESP_ERR_NO_MEM, err, TAG, "Not enough memory for LVGL buffer (buf1) allocation!");
        if (disp_cfg->double_buffer) {
            buf2 = heap_caps_aligned_alloc(draw_alignment, buffer_size * color_bytes, buff_caps);
            ESP_GOTO_ON_FALSE(buf2, ESP_ERR_NO_MEM, err, TAG, "Not enough memory for LVGL buffer (buf2) allocation!");
        }

        disp_ctx->draw_buffs[0] = buf1;
        disp_ctx->draw_buffs[1] = buf2;
    }

    disp = lv_display_create(disp_cfg->hres, disp_cfg->vres);

    /* Set display color format */
    lv_display_set_color_format(disp, display_color_format);

    /* Monochrome display settings */
    if (disp_cfg->monochrome) {
#if CONFIG_LV_COLOR_DEPTH_1
#error please disable LV_COLOR_DEPTH_1 for using monochromatic screen
#endif

        /* Monochrome can be used only in RGB565 color format */
        ESP_RETURN_ON_FALSE(
            display_color_format == LV_COLOR_FORMAT_RGB565 || display_color_format == LV_COLOR_FORMAT_I1, NULL, TAG,
            "Monochrome can be used only in display color format RGB565 or I1!");

        /* When using monochromatic display, there must be used full bufer! */
        ESP_GOTO_ON_FALSE((disp_cfg->hres * disp_cfg->vres == buffer_size), ESP_ERR_INVALID_ARG, err, TAG,
                          "Monochromatic display must using full buffer!");

        disp_ctx->flags.monochrome = 1;
        lv_display_set_buffers(disp, buf1, buf2, buffer_size * color_bytes, LV_DISPLAY_RENDER_MODE_FULL);

        if (display_color_format == LV_COLOR_FORMAT_I1) {
            /* OLED monochrome buffer */
            // To use LV_COLOR_FORMAT_I1, we need an extra buffer to hold the converted data
            disp_ctx->oled_buffer = heap_caps_malloc(buffer_size, buff_caps);
            ESP_GOTO_ON_FALSE(disp_ctx->oled_buffer, ESP_ERR_NO_MEM, err, TAG,
                              "Not enough memory for LVGL buffer (OLED buffer) allocation!");
        }

    } else if (disp_cfg->flags.direct_mode) {
        /* When using direct_mode, there must be used full bufer! */
        ESP_GOTO_ON_FALSE((disp_cfg->hres * disp_cfg->vres == buffer_size), ESP_ERR_INVALID_ARG, err, TAG,
                          "Direct mode must using full buffer!");

        disp_ctx->flags.direct_mode = 1;
        lv_display_set_buffers(disp, buf1, buf2, buffer_size * color_bytes, LV_DISPLAY_RENDER_MODE_DIRECT);
    } else if (disp_cfg->flags.full_refresh) {
        /* When using full_refresh, there must be used full bufer! */
        ESP_GOTO_ON_FALSE((disp_cfg->hres * disp_cfg->vres == buffer_size), ESP_ERR_INVALID_ARG, err, TAG,
                          "Full refresh must using full buffer!");

        disp_ctx->flags.full_refresh = 1;
        lv_display_set_buffers(disp, buf1, buf2, buffer_size * color_bytes, LV_DISPLAY_RENDER_MODE_FULL);
    } else {
        lv_display_set_buffers(disp, buf1, buf2, buffer_size * color_bytes, LV_DISPLAY_RENDER_MODE_PARTIAL);
        // lv_display_set_buffers(disp, buf1, buf2, buffer_size * color_bytes, LV_DISPLAY_RENDER_MODE_FULL);
    }

    lv_display_set_flush_cb(disp, lvgl_port_flush_callback);
    lv_display_add_event_cb(disp, lvgl_port_disp_size_update_callback, LV_EVENT_RESOLUTION_CHANGED, disp_ctx);
    lv_display_add_event_cb(disp, lvgl_port_display_invalidate_callback, LV_EVENT_INVALIDATE_AREA, disp_ctx);
    lv_display_add_event_cb(disp, lvgl_port_display_invalidate_callback, LV_EVENT_REFR_REQUEST, disp_ctx);
    lv_display_add_event_cb(disp, lvgl_port_render_timing_callback, LV_EVENT_RENDER_START, disp_ctx);
    lv_display_add_event_cb(disp, lvgl_port_render_timing_callback, LV_EVENT_RENDER_READY, disp_ctx);

    lv_display_set_driver_data(disp, disp_ctx);
    disp_ctx->disp_drv = disp;

    /* Use SW rotation */
    if (disp_cfg->flags.sw_rotate) {
        disp_ctx->draw_buffs[2] = heap_caps_malloc(buffer_size * color_bytes, buff_caps);
        ESP_GOTO_ON_FALSE(disp_ctx->draw_buffs[2], ESP_ERR_NO_MEM, err, TAG,
                          "Not enough memory for LVGL buffer (rotation buffer) allocation!");
    }

err:
    if (ret != ESP_OK) {
        if (disp_ctx->draw_buffs[0]) {
            free(disp_ctx->draw_buffs[0]);
        }
        if (disp_ctx->draw_buffs[1]) {
            free(disp_ctx->draw_buffs[1]);
        }
        if (disp_ctx->draw_buffs[2]) {
            free(disp_ctx->draw_buffs[2]);
        }
        if (disp_ctx->oled_buffer) {
            free(disp_ctx->oled_buffer);
        }
        if (disp_ctx) {
            if (disp_ctx->preview_gate) vSemaphoreDelete(disp_ctx->preview_gate);
            free(disp_ctx);
        }
        if (trans_sem) {
            vSemaphoreDelete(trans_sem);
        }
    }

    return disp;
}

#if LVGL_PORT_HANDLE_FLUSH_READY
static bool lvgl_port_flush_io_ready_callback(esp_lcd_panel_io_handle_t panel_io, esp_lcd_panel_io_event_data_t* edata,
                                              void* user_ctx)
{
    lv_display_t* disp_drv = (lv_display_t*)user_ctx;
    assert(disp_drv != NULL);
    lv_disp_flush_ready(disp_drv);
    return false;
}

#if (CONFIG_IDF_TARGET_ESP32P4 && ESP_IDF_VERSION >= ESP_IDF_VERSION_VAL(5, 3, 0))
static bool IRAM_ATTR lvgl_port_camera_refresh_callback(esp_lcd_panel_handle_t panel_io,
                                             esp_lcd_dpi_panel_event_data_t* edata, void* user_ctx)
{
    lvgl_port_display_ctx_t* ctx = user_ctx;
    BaseType_t need_yield = pdFALSE;
    portENTER_CRITICAL_ISR(&ctx->direct_lock);
    bool wake = tab5_direct_refresh(&ctx->direct);
    portEXIT_CRITICAL_ISR(&ctx->direct_lock);
    if (wake) xSemaphoreGiveFromISR(ctx->camera_refresh_sem, &need_yield);
    return need_yield == pdTRUE;
}

bool lvgl_port_screen_preview(lv_display_t *disp,uint16_t *out,int width,int height) {
    if (!disp || !out || width < 1 || height < 1 || width > 1280 || height > 1280 || (size_t)width*height > 1280*720) return false;
    lvgl_port_display_ctx_t *ctx = lv_display_get_driver_data(disp);
    if (!ctx || !ctx->preview_gate || ctx->disp_type != LVGL_PORT_DISP_TYPE_DSI ||
        ctx->flags.swap_bytes || lv_display_get_color_format(disp) != LV_COLOR_FORMAT_RGB565 ||
        xSemaphoreTake(ctx->preview_gate, 0) != pdTRUE) return false;
    portENTER_CRITICAL(&ctx->direct_lock);
    bool stable = (ctx->direct.mode == TAB5_DIRECT_NORMAL || ctx->direct.mode == TAB5_DIRECT_FROZEN ||
                   ctx->direct.mode == TAB5_DIRECT_CAMERA) && ctx->direct.normal_dma != TAB5_DIRECT_DMA_WAITING;
    unsigned front = ctx->direct.mode == TAB5_DIRECT_CAMERA ? ctx->direct.front : 0;
    portEXIT_CRITICAL(&ctx->direct_lock);
    uint16_t *buffers[2] = {0};
    bool ok = stable && front < 2 && esp_lcd_dpi_panel_get_frame_buffer(ctx->panel_handle,2,
                (void **)&buffers[0],(void **)&buffers[1]) == ESP_OK && buffers[front];
    // DMA2D updates panel memory without invalidating CPU pixel cache lines.
    // M2C invalidation only: never write stale cached pixels back to scanout.
    if (ok) ok = esp_cache_msync(buffers[front],(size_t)ctx->native_width*ctx->native_height*2,
                               ESP_CACHE_MSYNC_FLAG_DIR_M2C) == ESP_OK;
    if (ok) ok = tab5_preview_sample(buffers[front],ctx->native_width,ctx->native_height,
                                   ctx->current_rotation,out,width,height);
    xSemaphoreGive(ctx->preview_gate);
    return ok;
}

static bool lvgl_port_camera_select(lvgl_port_display_ctx_t* ctx, unsigned index, uint32_t timeout_ms)
{
    int64_t began = esp_timer_get_time();
    if (ctx->preview_gate && xSemaphoreTake(ctx->preview_gate, pdMS_TO_TICKS(timeout_ms)) != pdTRUE) return false;
    bool selected = false;
    int64_t deadline = began + (int64_t)timeout_ms * 1000;
    portENTER_CRITICAL(&ctx->direct_lock);
    bool prepared = tab5_direct_prepare(&ctx->direct, index);
    portEXIT_CRITICAL(&ctx->direct_lock);
    if (!prepared) goto preview_done;
    xSemaphoreTake(ctx->camera_refresh_sem, 0);

    // IDF 5.4.2 recognizes a panel-owned framebuffer and only writes back cache
    // and selects cur_fb_index here. Its synchronous color callback is NOT a
    // scanout completion and must not release an ordinary LVGL transfer token.
    esp_err_t result = esp_lcd_panel_draw_bitmap(ctx->panel_handle, 0, 0,
                                                ctx->native_width, ctx->native_height,
                                                ctx->camera_fbs[index]);
    if (result == ESP_OK) {
        portENTER_CRITICAL(&ctx->direct_lock);
        tab5_direct_arm(&ctx->direct);
        portEXIT_CRITICAL(&ctx->direct_lock);
        for (;;) {
            portENTER_CRITICAL(&ctx->direct_lock);
            bool complete = tab5_direct_complete(&ctx->direct);
            portEXIT_CRITICAL(&ctx->direct_lock);
            if (complete) { selected = true; goto preview_done; }
            int64_t remaining = deadline - esp_timer_get_time();
            if (remaining <= 0) break;
            TickType_t ticks = pdMS_TO_TICKS((uint32_t)((remaining + 999) / 1000));
            xSemaphoreTake(ctx->camera_refresh_sem, ticks ? ticks : 1);
        }
    }
    portENTER_CRITICAL(&ctx->direct_lock);
    tab5_direct_fault(&ctx->direct);
    portEXIT_CRITICAL(&ctx->direct_lock);
    tab5_display_flush_errors++;
    ESP_LOGE(TAG, "Camera framebuffer handoff failed (%s); display writes remain suppressed",
             result == ESP_OK ? "refresh timeout" : esp_err_to_name(result));
preview_done:
    if (ctx->preview_gate) xSemaphoreGive(ctx->preview_gate);
    return selected;
}

// An unchanged review image only needs its renderer paused. In particular,
// do not route through camera_begin/end: both select fb0 via draw_bitmap,
// which performs a full-frame cache writeback even when no pixel changed.
bool lvgl_port_display_freeze(lv_display_t* disp, uint32_t timeout_ms)
{
    if (!disp) return false;
    lvgl_port_display_ctx_t* ctx = lv_display_get_driver_data(disp);
    if (!ctx || ctx->disp_type != LVGL_PORT_DISP_TYPE_DSI ||
        ctx->flags.direct_mode || ctx->flags.full_refresh) return false;
    if (!lvgl_port_display_wait_idle(disp, timeout_ms)) return false;
    portENTER_CRITICAL(&ctx->direct_lock);
    bool acquired = tab5_direct_freeze(&ctx->direct);
    portEXIT_CRITICAL(&ctx->direct_lock);
    if (!acquired) return false;
    lv_timer_t *refresh_timer = lv_display_get_refr_timer(disp);
    if (refresh_timer) lv_timer_pause(refresh_timer);
    return true;
}

bool lvgl_port_display_unfreeze(lv_display_t* disp)
{
    if (!disp) return false;
    lvgl_port_display_ctx_t* ctx = lv_display_get_driver_data(disp);
    if (!ctx || ctx->disp_type != LVGL_PORT_DISP_TYPE_DSI) return false;
    portENTER_CRITICAL(&ctx->direct_lock);
    bool normal = ctx->direct.mode == TAB5_DIRECT_NORMAL;
    bool released = tab5_direct_unfreeze(&ctx->direct);
    portEXIT_CRITICAL(&ctx->direct_lock);
    if (normal) return true;
    if (!released) return false;
    // Invalidation areas accumulated during the hold remain pending. The
    // caller's destination changes join them before the next LVGL timer turn.
    lv_timer_t *refresh_timer = lv_display_get_refr_timer(disp);
    if (refresh_timer) lv_timer_resume(refresh_timer);
    return true;
}

bool lvgl_port_display_wait_idle(lv_display_t* disp, uint32_t timeout_ms)
{
    if (!disp) return false;
    lvgl_port_display_ctx_t* ctx = lv_display_get_driver_data(disp);
    if (!ctx || ctx->disp_type != LVGL_PORT_DISP_TYPE_DSI || !ctx->trans_sem) return false;
    portENTER_CRITICAL(&ctx->direct_lock);
    bool normal = ctx->direct.mode == TAB5_DIRECT_NORMAL;
    bool pending = ctx->direct.normal_dma != TAB5_DIRECT_DMA_NONE;
    portEXIT_CRITICAL(&ctx->direct_lock);
    if (!normal) return false;
    if (pending) {
        if (xSemaphoreTake(ctx->trans_sem, pdMS_TO_TICKS(timeout_ms)) != pdTRUE) return false;
        portENTER_CRITICAL(&ctx->direct_lock);
        bool consumed = tab5_direct_normal_consume(&ctx->direct);
        portEXIT_CRITICAL(&ctx->direct_lock);
        if (!consumed) return false;
        lv_disp_flush_ready(disp);
    }
    return true;
}

bool lvgl_port_camera_begin(lv_display_t* disp)
{
    if (!disp) return false;
    lvgl_port_display_ctx_t* ctx = lv_display_get_driver_data(disp);
    if (!ctx || ctx->disp_type != LVGL_PORT_DISP_TYPE_DSI || !ctx->camera_refresh_sem ||
        ctx->flags.direct_mode || ctx->flags.full_refresh || ctx->flags.swap_bytes ||
        lv_display_get_color_format(disp) != LV_COLOR_FORMAT_RGB565) return false;

    portENTER_CRITICAL(&ctx->direct_lock);
    bool normal = ctx->direct.mode == TAB5_DIRECT_NORMAL;
    bool pending = ctx->direct.normal_dma != TAB5_DIRECT_DMA_NONE;
    portEXIT_CRITICAL(&ctx->direct_lock);
    if (!normal) return false;
    if (pending) {
        // The LVGL lock prevents a new flush. Do not drop either the pending
        // ownership or LVGL flushing flag when this bounded wait fails.
        if (xSemaphoreTake(ctx->trans_sem, pdMS_TO_TICKS(300)) != pdTRUE) return false;
        portENTER_CRITICAL(&ctx->direct_lock);
        bool consumed = tab5_direct_normal_consume(&ctx->direct);
        portEXIT_CRITICAL(&ctx->direct_lock);
        if (!consumed) return false;
        lv_disp_flush_ready(disp);
    }
    if (esp_lcd_dpi_panel_get_frame_buffer(ctx->panel_handle, 2,
                                          (void**)&ctx->camera_fbs[0], (void**)&ctx->camera_fbs[1]) != ESP_OK ||
        !ctx->camera_fbs[0] || !ctx->camera_fbs[1] || ctx->camera_fbs[0] == ctx->camera_fbs[1]) return false;
    portENTER_CRITICAL(&ctx->direct_lock);
    bool entered = tab5_direct_enter(&ctx->direct);
    portEXIT_CRITICAL(&ctx->direct_lock);
    if (!entered) return false;
    // Pause this display's rendering only. Input and application timers remain
    // active. The invalidation callback also keeps LVGL's automatic refresh
    // requests from resuming it during CAMERA/SWITCHING/FAULT.
    lv_timer_t *refresh_timer = lv_display_get_refr_timer(disp);
    if (refresh_timer) lv_timer_pause(refresh_timer);
    // Selecting fb0, then confirming two refreshes, establishes the initial
    // front without accessing IDF's private cur_fb_index field.
    return lvgl_port_camera_select(ctx, 0, 300);
}

uint16_t* lvgl_port_camera_back_buffer(lv_display_t* disp)
{
    if (!disp) return NULL;
    lvgl_port_display_ctx_t* ctx = lv_display_get_driver_data(disp);
    if (!ctx) return NULL;
    portENTER_CRITICAL(&ctx->direct_lock);
    int index = tab5_direct_back(&ctx->direct);
    portEXIT_CRITICAL(&ctx->direct_lock);
    return index >= 0 ? ctx->camera_fbs[index] : NULL;
}

bool lvgl_port_camera_present(lv_display_t* disp, uint32_t timeout_ms)
{
    if (!disp) return false;
    lvgl_port_display_ctx_t* ctx = lv_display_get_driver_data(disp);
    if (!ctx || !ctx->camera_refresh_sem) return false;
    portENTER_CRITICAL(&ctx->direct_lock);
    int index = tab5_direct_back(&ctx->direct);
    portEXIT_CRITICAL(&ctx->direct_lock);
    return index >= 0 && lvgl_port_camera_select(ctx, (unsigned)index, timeout_ms);
}

bool lvgl_port_camera_end(lv_display_t* disp, uint32_t timeout_ms)
{
    if (!disp) return false;
    lvgl_port_display_ctx_t* ctx = lv_display_get_driver_data(disp);
    if (!ctx || !ctx->camera_refresh_sem) return false;
    portENTER_CRITICAL(&ctx->direct_lock);
    bool normal = ctx->direct.mode == TAB5_DIRECT_NORMAL;
    bool copy_front = ctx->direct.mode == TAB5_DIRECT_CAMERA && ctx->direct.front == 1;
    portEXIT_CRITICAL(&ctx->direct_lock);
    if (normal) return true;
    // A cancelled composition may have left fb0 partially written. In CAMERA
    // the two-refresh fence proves fb0 is no longer scanned when front is fb1.
    // Preserve the complete displayed frame before restoring canonical fb0.
    if (copy_front) {
        memcpy(ctx->camera_fbs[0], ctx->camera_fbs[1],
               (size_t)ctx->native_width * ctx->native_height * sizeof(uint16_t));
    }
    // The worker has stopped. After a timeout both buffers may be in use by
    // scanout, so do not copy into either; select the existing fb0 contents and
    // re-establish its ownership before allowing any ordinary DMA writes.
    if (!lvgl_port_camera_select(ctx, 0, timeout_ms)) return false;
    portENTER_CRITICAL(&ctx->direct_lock);
    bool restored = tab5_direct_leave(&ctx->direct);
    portEXIT_CRITICAL(&ctx->direct_lock);
    if (restored) {
        lv_obj_invalidate(lv_display_get_screen_active(disp));
        lv_obj_invalidate(lv_display_get_layer_top(disp));
        lv_obj_invalidate(lv_display_get_layer_sys(disp));
        // LVGL itself pauses an idle display's refresh timer. Always resume to
        // render the complete invalidation above, including the captured view.
        lv_timer_t *refresh_timer = lv_display_get_refr_timer(disp);
        if (refresh_timer) lv_timer_resume(refresh_timer);
    }
    return restored;
}

static bool IRAM_ATTR lvgl_port_flush_dpi_panel_ready_callback(esp_lcd_panel_handle_t panel_io,
                                                     esp_lcd_dpi_panel_event_data_t* edata, void* user_ctx)
{
    lvgl_port_display_ctx_t* disp_ctx = user_ctx;
    if (disp_ctx->trans_sem && !disp_ctx->flags.direct_mode && !disp_ctx->flags.full_refresh) {
        portENTER_CRITICAL_SAFE(&disp_ctx->direct_lock);
        bool ordinary_done = tab5_direct_color_done(&disp_ctx->direct);
        portEXIT_CRITICAL_SAFE(&disp_ctx->direct_lock);
        if (!ordinary_done) return false;
        // Exactly one completion is consumed by flush_wait_cb. Do not also
        // clear LVGL's flushing flag here: an early completion would then
        // skip the wait, leaving a stale token for the next DMA transfer.
        if (xPortInIsrContext()) {
            BaseType_t need_yield = pdFALSE;
            xSemaphoreGiveFromISR(disp_ctx->trans_sem, &need_yield);
            return need_yield == pdTRUE;
        }
        // The DPI driver can also complete synchronously for CPU/no-copy paths.
        xSemaphoreGive(disp_ctx->trans_sem);
        return false;
    }
    // This fallback is used only by full/direct rendering. Color completion
    // is a DMA2D interrupt (not the IRAM DSI scanout interrupt), or task context.
    lv_disp_flush_ready(disp_ctx->disp_drv);
    return false;
}

static void lvgl_port_flush_wait_callback(lv_display_t* drv)
{
    lvgl_port_display_ctx_t* disp_ctx = (lvgl_port_display_ctx_t*)lv_display_get_driver_data(drv);
    int64_t start = esp_timer_get_time();
    while (xSemaphoreTake(disp_ctx->trans_sem, pdMS_TO_TICKS(1000)) != pdTRUE) {
        tab5_display_flush_errors++;
        ESP_LOGE(TAG, "LCD transfer completion timed out");
        // Keep the DMA source owned until its real completion. Returning on a
        // timeout would let LVGL overwrite memory the display may still read.
    }
    portENTER_CRITICAL(&disp_ctx->direct_lock);
    bool consumed = tab5_direct_normal_consume(&disp_ctx->direct);
    portEXIT_CRITICAL(&disp_ctx->direct_lock);
    assert(consumed);
    tab5_display_wait_us = (unsigned)(esp_timer_get_time() - start);
    disp_ctx->frame_wait_us += tab5_display_wait_us;
    if (tab5_display_wait_us > tab5_display_wait_peak_us)
        tab5_display_wait_peak_us = tab5_display_wait_us;
    // DMA often finishes while LVGL draws the other buffer, so taking the
    // semaphore alone may never block. Bound continuous rendering to 8 ms
    // between strip boundaries, even during repeated full-screen capture.
    int64_t now = esp_timer_get_time();
    if (now - disp_ctx->yield_at_us >= 8000) {
        vTaskDelay(1);
        disp_ctx->yield_at_us = esp_timer_get_time();
        disp_ctx->frame_yield_us += (unsigned)(disp_ctx->yield_at_us-now);
    }
}

static bool IRAM_ATTR lvgl_port_flush_dpi_vsync_ready_callback(esp_lcd_panel_handle_t panel_io,
                                                     esp_lcd_dpi_panel_event_data_t* edata, void* user_ctx)
{
    BaseType_t need_yield = pdFALSE;

    lvgl_port_display_ctx_t* disp_ctx = user_ctx;

    if (disp_ctx->trans_sem) {
        xSemaphoreGiveFromISR(disp_ctx->trans_sem, &need_yield);
    }

    return (need_yield == pdTRUE);
}
#else
bool lvgl_port_display_freeze(lv_display_t* disp, uint32_t timeout_ms) { return false; }
bool lvgl_port_display_unfreeze(lv_display_t* disp) { return false; }
bool lvgl_port_camera_begin(lv_display_t* disp) { return false; }
uint16_t* lvgl_port_camera_back_buffer(lv_display_t* disp) { return NULL; }
bool lvgl_port_camera_present(lv_display_t* disp, uint32_t timeout_ms) { return false; }
bool lvgl_port_camera_end(lv_display_t* disp, uint32_t timeout_ms) { return false; }
#endif

#if CONFIG_IDF_TARGET_ESP32S3 && ESP_IDF_VERSION >= ESP_IDF_VERSION_VAL(5, 0, 0)
static bool lvgl_port_flush_rgb_vsync_ready_callback(esp_lcd_panel_handle_t panel_io,
                                                     const esp_lcd_rgb_panel_event_data_t* edata, void* user_ctx)
{
    BaseType_t need_yield = pdFALSE;

    lv_display_t* disp_drv = (lv_display_t*)user_ctx;
    assert(disp_drv != NULL);
    lvgl_port_display_ctx_t* disp_ctx = (lvgl_port_display_ctx_t*)lv_display_get_driver_data(disp_drv);
    assert(disp_ctx != NULL);

    if (disp_ctx->trans_sem) {
        xSemaphoreGiveFromISR(disp_ctx->trans_sem, &need_yield);
    }

    return (need_yield == pdTRUE);
}
#endif
#endif

static void _lvgl_port_transform_monochrome(lv_display_t* display, const lv_area_t* area, uint8_t** color_map)
{
    assert(color_map);
    assert(*color_map);
    uint8_t* src                      = *color_map;
    lv_color16_t* color               = (lv_color16_t*)*color_map;
    lvgl_port_display_ctx_t* disp_ctx = (lvgl_port_display_ctx_t*)lv_display_get_driver_data(display);
    uint16_t hor_res                  = lv_display_get_physical_horizontal_resolution(display);
    uint16_t ver_res                  = lv_display_get_physical_vertical_resolution(display);
    uint16_t res                      = hor_res;
    bool swap_xy                      = (lv_display_get_rotation(display) == LV_DISPLAY_ROTATION_90 ||
                    lv_display_get_rotation(display) == LV_DISPLAY_ROTATION_270);

    int x1 = area->x1;
    int x2 = area->x2;
    int y1 = area->y1;
    int y2 = area->y2;

    lv_color_format_t color_format = lv_display_get_color_format(display);
    if (color_format == LV_COLOR_FORMAT_I1) {
        // This is necessary because LVGL reserves 2 x 4 bytes in the buffer, as these are assumed to be used as a
        // palette. Skip the palette here More information about the monochrome, please refer to
        // https://docs.lvgl.io/9.2/porting/display.html#monochrome-displays
        src += 8;
        /*Use oled_buffer as output */
        *color_map = disp_ctx->oled_buffer;
    }

    int out_x, out_y;
    for (int y = y1; y <= y2; y++) {
        for (int x = x1; x <= x2; x++) {
            bool chroma_color = 0;
            if (color_format == LV_COLOR_FORMAT_I1) {
                chroma_color = (src[(hor_res >> 3) * y + (x >> 3)] & 1 << (7 - x % 8));
            } else {
                chroma_color = (color[hor_res * y + x].blue > 16);
            }

            if (swap_xy) {
                out_x = y;
                out_y = x;
                res   = ver_res;
            } else {
                out_x = x;
                out_y = y;
                res   = hor_res;
            }

            /* Write to the buffer as required for the display.
             * It writes only 1-bit for monochrome displays mapped vertically.*/
            uint8_t* outbuf = NULL;
            outbuf          = *color_map + res * (out_y >> 3) + (out_x);
            if (chroma_color) {
                (*outbuf) &= ~(1 << (out_y % 8));
            } else {
                (*outbuf) |= (1 << (out_y % 8));
            }
        }
    }
}

void lvgl_port_rotate_area(lv_display_t* disp, lv_area_t* area)
{
    lv_display_rotation_t rotation = lv_display_get_rotation(disp);

    int32_t w = lv_area_get_width(area);
    int32_t h = lv_area_get_height(area);

    int32_t hres = lv_display_get_horizontal_resolution(disp);
    int32_t vres = lv_display_get_vertical_resolution(disp);
    if (rotation == LV_DISPLAY_ROTATION_90 || rotation == LV_DISPLAY_ROTATION_270) {
        vres = lv_display_get_horizontal_resolution(disp);
        hres = lv_display_get_vertical_resolution(disp);
    }

    switch (rotation) {
        case LV_DISPLAY_ROTATION_0:
            return;
        case LV_DISPLAY_ROTATION_90:
            area->y2 = vres - area->x1 - 1;
            area->x1 = area->y1;
            area->x2 = area->x1 + h - 1;
            area->y1 = area->y2 - w + 1;
            break;
        case LV_DISPLAY_ROTATION_180:
            area->y2 = vres - area->y1 - 1;
            area->y1 = area->y2 - h + 1;
            area->x2 = hres - area->x1 - 1;
            area->x1 = area->x2 - w + 1;
            break;
        case LV_DISPLAY_ROTATION_270:
            area->x1 = hres - area->y2 - 1;
            area->y2 = area->x2;
            area->x2 = area->x1 + h - 1;
            area->y1 = area->y2 - w + 1;
            break;
    }
}

extern void tab5_draw_timing(bool begin) __attribute__((weak));
static void lvgl_port_render_timing_callback(lv_event_t* e)
{
    lvgl_port_display_ctx_t* ctx = lv_event_get_user_data(e);
    if (lv_event_get_code(e) == LV_EVENT_RENDER_START) {
        ctx->render_start_us = esp_timer_get_time();
        ctx->frame_flush_us = ctx->frame_rotate_us = 0;
        ctx->frame_pixels = 0;
        ctx->frame_wait_us=ctx->frame_yield_us=ctx->frame_parts=0;
        ctx->frame_copy_start=tab5_native_copy_total_us;
        ctx->frame_native_start=tab5_native_cache_hits;
        if (tab5_draw_timing) tab5_draw_timing(true);
    } else if (ctx->render_start_us) {
        tab5_display_render_us = (unsigned)(esp_timer_get_time() - ctx->render_start_us);
        tab5_display_frame_flush_us = ctx->frame_flush_us;
        tab5_display_frame_rotate_us = ctx->frame_rotate_us;
        if(ctx->frame_pixels >= 200000) {
            int64_t now = esp_timer_get_time();
            unsigned period = (unsigned)(now - ctx->previous_large_frame_us);
            tab5_display_large_period_us = ctx->previous_large_frame_us && period < 500000 ? period : 0;
            ctx->previous_large_frame_us = now;
            tab5_display_large_us = tab5_display_render_us;
            if(tab5_display_large_us > tab5_display_large_peak_us)
                tab5_display_large_peak_us = tab5_display_large_us;
            tab5_display_large_total_ms += tab5_display_large_us / 1000;
            tab5_display_large_pixels = ctx->frame_pixels;
            tab5_display_large_flush_us = ctx->frame_flush_us;
            tab5_display_large_rotate_us = ctx->frame_rotate_us;
            tab5_display_large_copy_us=tab5_native_copy_total_us-ctx->frame_copy_start;
            tab5_display_large_wait_us=ctx->frame_wait_us;
            tab5_display_large_yield_us=ctx->frame_yield_us;
            tab5_display_large_parts=ctx->frame_parts;
            tab5_display_large_native_parts=tab5_native_cache_hits-ctx->frame_native_start;
            tab5_display_large_frames++;
        }
        if (tab5_draw_timing) tab5_draw_timing(false);
    }
}

static void lvgl_port_flush_callback(lv_display_t* drv, const lv_area_t* area, uint8_t* color_map)
{
    assert(drv != NULL);
    assert(area != NULL);
    assert(color_map != NULL);
    lvgl_port_display_ctx_t* disp_ctx = (lvgl_port_display_ctx_t*)lv_display_get_driver_data(drv);
    assert(disp_ctx != NULL);
    int64_t flush_start = esp_timer_get_time();

    portENTER_CRITICAL(&disp_ctx->direct_lock);
    bool suppressed = disp_ctx->direct.mode != TAB5_DIRECT_NORMAL;
    portEXIT_CRITICAL(&disp_ctx->direct_lock);
    if (suppressed) {
        // Camera owns scanout. LVGL may still update controls and process touch,
        // but must not rotate/copy pixels or manufacture a DMA completion token.
        lv_disp_flush_ready(drv);
        return;
    }

    int offsetx1 = area->x1;
    int offsetx2 = area->x2;
    int offsety1 = area->y1;
    int offsety2 = area->y2;
    disp_ctx->frame_pixels += (unsigned)lv_area_get_size(area);
    disp_ctx->frame_parts++;

    // printf("%d %d %d %d\n", offsetx1, offsetx2, offsety1, offsety2);

    /* SW rotation enabled */
    if (disp_ctx->flags.sw_rotate && disp_ctx->current_rotation==LV_DISPLAY_ROTATION_90 &&
        tab5_draw_cache_take_native && tab5_draw_cache_take_native(color_map,area)) {
        // The optional swipe compositor already wrote this owned LVGL buffer
        // in panel orientation. Keep normal submit/wait/retirement unchanged.
        tab5_display_rotate_us=0;
        lvgl_port_rotate_area(drv,(lv_area_t *)area);
        offsetx1=area->x1;offsetx2=area->x2;offsety1=area->y1;offsety2=area->y2;
    } else if (disp_ctx->flags.sw_rotate && (disp_ctx->current_rotation > LV_DISPLAY_ROTATION_0)) {
        /* SW rotation */
        if (disp_ctx->draw_buffs[2]) {
            int32_t ww           = lv_area_get_width(area);
            int32_t hh           = lv_area_get_height(area);
            lv_color_format_t cf = lv_display_get_color_format(drv);
            uint32_t w_stride    = lv_draw_buf_width_to_stride(ww, cf);
            uint32_t h_stride    = lv_draw_buf_width_to_stride(hh, cf);
            // TAB5: PPA_TRANS_MODE_BLOCKING was observed stuck waiting for its
            // completion semaphore while USB/Wi-Fi/BLE remained healthy. Use
            // LVGL's synchronous CPU rotation so display progress has no PPA
            // interrupt dependency. The destination is compact, with swapped
            // dimensions only for quarter turns.
            uint32_t dst_stride = disp_ctx->current_rotation == LV_DISPLAY_ROTATION_180 ? w_stride : h_stride;
            int64_t rotate_start = esp_timer_get_time();
            if (cf == LV_COLOR_FORMAT_RGB565 && disp_ctx->current_rotation != LV_DISPLAY_ROTATION_180) {
                tab5_rotate_ui(color_map, disp_ctx->draw_buffs[2], ww, hh, w_stride, dst_stride,
                                  disp_ctx->current_rotation == LV_DISPLAY_ROTATION_270);
            } else {
                lv_draw_sw_rotate(color_map, disp_ctx->draw_buffs[2], ww, hh,
                                  w_stride, dst_stride, disp_ctx->current_rotation, cf);
            }
            tab5_display_rotate_us = (unsigned)(esp_timer_get_time() - rotate_start);
            disp_ctx->frame_rotate_us += tab5_display_rotate_us;
            if (tab5_display_rotate_us > tab5_display_rotate_peak_us)
                tab5_display_rotate_peak_us = tab5_display_rotate_us;
            color_map = (uint8_t*)disp_ctx->draw_buffs[2];
            lvgl_port_rotate_area(drv, (lv_area_t*)area);
            offsetx1 = area->x1;
            offsetx2 = area->x2;
            offsety1 = area->y1;
            offsety2 = area->y2;
        }
    }

    if (disp_ctx->flags.swap_bytes) {
        size_t len = lv_area_get_size(area);
        lv_draw_sw_rgb565_swap(color_map, len);
    }

    /* Transfer data in buffer for monochromatic screen */
    if (disp_ctx->flags.monochrome) {
        _lvgl_port_transform_monochrome(drv, area, &color_map);
    }

    if ((disp_ctx->disp_type == LVGL_PORT_DISP_TYPE_RGB || disp_ctx->disp_type == LVGL_PORT_DISP_TYPE_DSI) &&
        (disp_ctx->flags.direct_mode || disp_ctx->flags.full_refresh)) {
        if (lv_disp_flush_is_last(drv)) {
            /* If the interface is I80 or SPI, this step cannot be used for drawing. */
            esp_lcd_panel_draw_bitmap(disp_ctx->panel_handle, 0, 0, lv_disp_get_hor_res(drv), lv_disp_get_ver_res(drv),
                                      color_map);
            /* Waiting for the last frame buffer to complete transmission */
            // xSemaphoreTake(disp_ctx->trans_sem, 0);
            // xSemaphoreTake(disp_ctx->trans_sem, portMAX_DELAY);
        }
    } else {
        bool tracked = disp_ctx->disp_type == LVGL_PORT_DISP_TYPE_DSI && disp_ctx->camera_refresh_sem;
        if (tracked) {
            portENTER_CRITICAL(&disp_ctx->direct_lock);
            bool submitted = tab5_direct_normal_submit(&disp_ctx->direct);
            portEXIT_CRITICAL(&disp_ctx->direct_lock);
            assert(submitted);
        }
        esp_err_t draw_result = esp_lcd_panel_draw_bitmap(disp_ctx->panel_handle, offsetx1, offsety1,
                                                        offsetx2 + 1, offsety2 + 1, color_map);
        tab5_display_flush_count++;
        if (draw_result != ESP_OK) {
            if (tracked) {
                portENTER_CRITICAL(&disp_ctx->direct_lock);
                disp_ctx->direct.normal_dma = TAB5_DIRECT_DMA_NONE;
                portEXIT_CRITICAL(&disp_ctx->direct_lock);
            }
            tab5_display_flush_errors++;
            ESP_LOGE(TAG, "LCD draw failed: %s", esp_err_to_name(draw_result));
            // The driver rejected this transfer; no completion is forthcoming.
            lv_disp_flush_ready(drv);
        }
    }

    if (disp_ctx->disp_type == LVGL_PORT_DISP_TYPE_RGB ||
        (disp_ctx->disp_type == LVGL_PORT_DISP_TYPE_DSI &&
         (disp_ctx->flags.direct_mode || disp_ctx->flags.full_refresh))) {
        lv_disp_flush_ready(drv);
    }
    disp_ctx->frame_flush_us += (unsigned)(esp_timer_get_time() - flush_start);
}

// static void lvgl_port_flush_callback(lv_display_t *drv, const lv_area_t *area, uint8_t *color_map)
// {
//     assert(drv != NULL);
//     assert(area != NULL);
//     assert(color_map != NULL);
//     lvgl_port_display_ctx_t *disp_ctx = (lvgl_port_display_ctx_t *)lv_display_get_driver_data(drv);
//     assert(disp_ctx != NULL);

//     int offsetx1 = area->x1;
//     int offsetx2 = area->x2;
//     int offsety1 = area->y1;
//     int offsety2 = area->y2;

//     /* SW rotation enabled */
//     if (disp_ctx->flags.sw_rotate && (disp_ctx->current_rotation > LV_DISPLAY_ROTATION_0)) {
//         /* SW rotation */
//         if (disp_ctx->draw_buffs[2]) {
//             int32_t ww = lv_area_get_width(area);
//             int32_t hh = lv_area_get_height(area);
//             lv_color_format_t cf = lv_display_get_color_format(drv);
//             uint32_t w_stride = lv_draw_buf_width_to_stride(ww, cf);
//             uint32_t h_stride = lv_draw_buf_width_to_stride(hh, cf);
//             if (disp_ctx->current_rotation == LV_DISPLAY_ROTATION_180) {
//                 lv_draw_sw_rotate(color_map, disp_ctx->draw_buffs[2], hh, ww, h_stride, h_stride,
//                 LV_DISPLAY_ROTATION_180, cf);
//             } else if (disp_ctx->current_rotation == LV_DISPLAY_ROTATION_90) {
//                 lv_draw_sw_rotate(color_map, disp_ctx->draw_buffs[2], ww, hh, w_stride, h_stride,
//                 LV_DISPLAY_ROTATION_90, cf);
//             } else if (disp_ctx->current_rotation == LV_DISPLAY_ROTATION_270) {
//                 lv_draw_sw_rotate(color_map, disp_ctx->draw_buffs[2], ww, hh, w_stride, h_stride,
//                 LV_DISPLAY_ROTATION_270, cf);
//             }
//             color_map = (uint8_t *)disp_ctx->draw_buffs[2];
//             lvgl_port_rotate_area(drv, (lv_area_t *)area);
//             offsetx1 = area->x1;
//             offsetx2 = area->x2;
//             offsety1 = area->y1;
//             offsety2 = area->y2;
//         }
//     }

//     if (disp_ctx->flags.swap_bytes) {
//         size_t len = lv_area_get_size(area);
//         lv_draw_sw_rgb565_swap(color_map, len);
//     }

//     /* Transfer data in buffer for monochromatic screen */
//     if (disp_ctx->flags.monochrome) {
//         _lvgl_port_transform_monochrome(drv, area, &color_map);
//     }

//     if ((disp_ctx->disp_type == LVGL_PORT_DISP_TYPE_RGB || disp_ctx->disp_type == LVGL_PORT_DISP_TYPE_DSI) &&
//     (disp_ctx->flags.direct_mode || disp_ctx->flags.full_refresh)) {
//         if (lv_disp_flush_is_last(drv)) {
//             /* If the interface is I80 or SPI, this step cannot be used for drawing. */
//             esp_lcd_panel_draw_bitmap(disp_ctx->panel_handle, 0, 0, lv_disp_get_hor_res(drv),
//             lv_disp_get_ver_res(drv), color_map);
//             /* Waiting for the last frame buffer to complete transmission */
//             xSemaphoreTake(disp_ctx->trans_sem, 0);
//             xSemaphoreTake(disp_ctx->trans_sem, portMAX_DELAY);
//         }
//     } else {
//         esp_lcd_panel_draw_bitmap(disp_ctx->panel_handle, offsetx1, offsety1, offsetx2 + 1, offsety2 + 1, color_map);
//     }

//     if (disp_ctx->disp_type == LVGL_PORT_DISP_TYPE_RGB || (disp_ctx->disp_type == LVGL_PORT_DISP_TYPE_DSI &&
//     (disp_ctx->flags.direct_mode || disp_ctx->flags.full_refresh))) {
//         lv_disp_flush_ready(drv);
//     }
// }

static void lvgl_port_disp_rotation_update(lvgl_port_display_ctx_t* disp_ctx)
{
    assert(disp_ctx != NULL);

    disp_ctx->current_rotation = lv_display_get_rotation(disp_ctx->disp_drv);
    if (disp_ctx->flags.sw_rotate) {
        return;
    }

    esp_lcd_panel_handle_t control_handle =
        (disp_ctx->control_handle ? disp_ctx->control_handle : disp_ctx->panel_handle);
    /* Solve rotation screen and touch */
    switch (lv_display_get_rotation(disp_ctx->disp_drv)) {
        case LV_DISPLAY_ROTATION_0:
            /* Rotate LCD display */
            esp_lcd_panel_swap_xy(control_handle, disp_ctx->rotation.swap_xy);
            esp_lcd_panel_mirror(control_handle, disp_ctx->rotation.mirror_x, disp_ctx->rotation.mirror_y);
            break;
        case LV_DISPLAY_ROTATION_90:
            /* Rotate LCD display */
            esp_lcd_panel_swap_xy(control_handle, !disp_ctx->rotation.swap_xy);
            if (disp_ctx->rotation.swap_xy) {
                esp_lcd_panel_mirror(control_handle, !disp_ctx->rotation.mirror_x, disp_ctx->rotation.mirror_y);
            } else {
                esp_lcd_panel_mirror(control_handle, disp_ctx->rotation.mirror_x, !disp_ctx->rotation.mirror_y);
            }
            break;
        case LV_DISPLAY_ROTATION_180:
            /* Rotate LCD display */
            esp_lcd_panel_swap_xy(control_handle, disp_ctx->rotation.swap_xy);
            esp_lcd_panel_mirror(control_handle, !disp_ctx->rotation.mirror_x, !disp_ctx->rotation.mirror_y);
            break;
        case LV_DISPLAY_ROTATION_270:
            /* Rotate LCD display */
            esp_lcd_panel_swap_xy(control_handle, !disp_ctx->rotation.swap_xy);
            if (disp_ctx->rotation.swap_xy) {
                esp_lcd_panel_mirror(control_handle, disp_ctx->rotation.mirror_x, !disp_ctx->rotation.mirror_y);
            } else {
                esp_lcd_panel_mirror(control_handle, !disp_ctx->rotation.mirror_x, disp_ctx->rotation.mirror_y);
            }
            break;
    }

    /* Wake LVGL task, if needed */
    lvgl_port_task_wake(LVGL_PORT_EVENT_DISPLAY, disp_ctx->disp_drv);
}

static void lvgl_port_disp_size_update_callback(lv_event_t* e)
{
    assert(e);
    lvgl_port_display_ctx_t* disp_ctx = (lvgl_port_display_ctx_t*)lv_event_get_user_data(e);
    lvgl_port_disp_rotation_update(disp_ctx);
}

static void lvgl_port_display_invalidate_callback(lv_event_t* e)
{
    lvgl_port_display_ctx_t* ctx = lv_event_get_user_data(e);
    portENTER_CRITICAL(&ctx->direct_lock);
    bool suppressed = ctx->direct.mode != TAB5_DIRECT_NORMAL;
    portEXIT_CRITICAL(&ctx->direct_lock);
    if (suppressed) {
        // LVGL's built-in display callback was registered before this port's
        // callback and resumes the timer on REFR_REQUEST. Pause it again here,
        // before the timer handler can draw a background LVGL frame.
        lv_timer_t *refresh_timer = lv_display_get_refr_timer(ctx->disp_drv);
        if (refresh_timer) lv_timer_pause(refresh_timer);
        return;
    }
    /* Wake LVGL task, if needed */
    lvgl_port_task_wake(LVGL_PORT_EVENT_DISPLAY, NULL);
}
