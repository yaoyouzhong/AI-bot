/*
 * SPDX-FileCopyrightText: 2024 Espressif Systems (Shanghai) CO LTD
 *
 * SPDX-License-Identifier: Apache-2.0
 */

/**
 * @file
 * @brief ESP LVGL port display
 */

#pragma once

#include "esp_err.h"
#include "esp_lcd_panel_io.h"
#include "esp_lcd_panel_ops.h"
#include "lvgl.h"

#if LVGL_VERSION_MAJOR == 8
#include "esp_lvgl_port_compatibility.h"
#endif

#ifdef __cplusplus
extern "C" {
#endif

/**
 * @brief Rotation configuration
 */
typedef struct {
    bool swap_xy;  /*!< LCD Screen swapped X and Y (in esp_lcd driver) */
    bool mirror_x; /*!< LCD Screen mirrored X (in esp_lcd driver) */
    bool mirror_y; /*!< LCD Screen mirrored Y (in esp_lcd driver) */
} lvgl_port_rotation_cfg_t;

/**
 * @brief Configuration display structure
 */
typedef struct {
    esp_lcd_panel_io_handle_t io_handle;   /*!< LCD panel IO handle */
    esp_lcd_panel_handle_t panel_handle;   /*!< LCD panel handle */
    esp_lcd_panel_handle_t control_handle; /*!< LCD panel control handle */

    uint32_t buffer_size; /*!< Size of the buffer for the screen in pixels */
    bool double_buffer;   /*!< True, if should be allocated two buffers */
    uint32_t trans_size;  /*!< Allocated buffer will be in SRAM to move framebuf (optional) */

    uint32_t hres; /*!< LCD display horizontal resolution */
    uint32_t vres; /*!< LCD display vertical resolution */

    bool monochrome; /*!< True, if display is monochrome and using 1bit for 1px */

    lvgl_port_rotation_cfg_t
        rotation; /*!< Default values of the screen rotation (Only HW state. Not supported for default SW rotation!) */
#if LVGL_VERSION_MAJOR >= 9
    lv_color_format_t color_format; /*!< The color format of the display */
#endif
    struct {
        unsigned int buff_dma : 1;    /*!< Allocated LVGL buffer will be DMA capable */
        unsigned int buff_spiram : 1; /*!< Allocated LVGL buffer will be in PSRAM */
        unsigned int sw_rotate : 1;   /*!< Use software rotation (slower) or PPA if available */
#if LVGL_VERSION_MAJOR >= 9
        unsigned int swap_bytes : 1; /*!< Swap bytes in RGB656 (16-bit) color format before send to LCD driver */
#endif
        unsigned int full_refresh : 1; /*!< 1: Always make the whole screen redrawn */
        unsigned int direct_mode : 1;  /*!< 1: Use screen-sized buffers and draw to absolute coordinates */
    } flags;
} lvgl_port_display_cfg_t;

/**
 * @brief Configuration RGB display structure
 */
typedef struct {
    struct {
        unsigned int bb_mode : 1; /*!< 1: Use bounce buffer mode */
        unsigned int
            avoid_tearing : 1; /*!< 1: Use internal RGB buffers as a LVGL draw buffers to avoid tearing effect, enabling
                                  this option requires over two LCD buffers and may reduce the frame rate */
    } flags;
} lvgl_port_display_rgb_cfg_t;

/**
 * @brief Configuration MIPI-DSI display structure
 */
typedef struct {
    struct {
        unsigned int
            avoid_tearing : 1; /*!< 1: Use internal MIPI-DSI buffers as a LVGL draw buffers to avoid tearing effect,
                                  enabling this option requires over two LCD buffers and may reduce the frame rate */
    } flags;
} lvgl_port_display_dsi_cfg_t;

/**
 * @brief Add I2C/SPI/I8080 display handling to LVGL
 *
 * @note Allocated memory in this function is not free in deinit. You must call lvgl_port_remove_disp for free all
 * memory!
 *
 * @param disp_cfg Display configuration structure
 * @return Pointer to LVGL display or NULL when error occurred
 */
lv_display_t *lvgl_port_add_disp(const lvgl_port_display_cfg_t *disp_cfg);

/**
 * @brief Add MIPI-DSI display handling to LVGL
 *
 * @note Allocated memory in this function is not free in deinit. You must call lvgl_port_remove_disp for free all
 * memory!
 *
 * @param disp_cfg Display configuration structure
 * @param dsi_cfg MIPI-DSI display specific configuration structure
 * @return Pointer to LVGL display or NULL when error occurred
 */
lv_display_t *lvgl_port_add_disp_dsi(const lvgl_port_display_cfg_t *disp_cfg,
                                     const lvgl_port_display_dsi_cfg_t *dsi_cfg);

/**
 * @brief Add RGB display handling to LVGL
 *
 * @note Allocated memory in this function is not free in deinit. You must call lvgl_port_remove_disp for free all
 * memory!
 *
 * @param disp_cfg Display configuration structure
 * @param rgb_cfg RGB display specific configuration structure
 * @return Pointer to LVGL display or NULL when error occurred
 */
lv_display_t *lvgl_port_add_disp_rgb(const lvgl_port_display_cfg_t *disp_cfg,
                                     const lvgl_port_display_rgb_cfg_t *rgb_cfg);

/**
 * @brief Remove display handling from LVGL
 *
 * @note Free all memory used for this display.
 *
 * @return
 *      - ESP_OK                    on success
 */
esp_err_t lvgl_port_remove_disp(lv_display_t *disp);

#if LVGL_VERSION_MAJOR >= 9
/** Caller holds the LVGL lock. Drain the last asynchronous normal DMA copy;
 * keep that lock until shared DMA2D work finishes. Scanout keeps displaying the
 * existing framebuffer. Timeout preserves pending ownership and never clears it.
 */
bool lvgl_port_display_wait_idle(lv_display_t *disp, uint32_t timeout_ms);

/** Caller holds the LVGL lock in an input/timer turn, outside draw/flush.
 * Drain any ordinary DMA and pause rendering while retaining the current
 * scanout. No framebuffer selection, pixel copy, allocation or cache sync.
 * Failure acquires nothing and leaves the previous owner/DMA state intact.
 */
bool lvgl_port_display_freeze(lv_display_t *disp, uint32_t timeout_ms);

/** Caller holds the LVGL lock. Resume pending invalidations without selecting
 * a framebuffer or forcing a whole-screen redraw. Update the destination UI
 * in this same turn. A different display owner is never released by this API.
 */
bool lvgl_port_display_unfreeze(lv_display_t *disp);

/**
 * Take exclusive ownership of two native RGB565 DPI framebuffers. Caller holds
 * the LVGL lock. This display's refresh timer is paused and ordinary flushes
 * are suppressed until camera_end succeeds; input/application timers continue.
 * Begin waits at most 300 ms for each ownership handoff. A failed framebuffer
 * handoff leaves the display suppressed; call camera_end to recover it.
 */
/* Read-only sampled snapshot. Caller holds LVGL lock; false means busy. */
bool lvgl_port_screen_preview(lv_display_t *disp,uint16_t *out,int width,int height);

bool lvgl_port_camera_begin(lv_display_t *disp);

/** Writable native-orientation buffer, or NULL after a failure / while switching.
 * One camera worker owns this lease; stop writing before present or end. The
 * pointer expires on present/end. The worker must not overlap those operations.
 */
uint16_t *lvgl_port_camera_back_buffer(lv_display_t *disp);

/** Submit the leased framebuffer without DMA2D copying; wait for two actual DPI
 * refresh completions after submission. Called by the worker outside LVGL lock.
 * On timeout no framebuffer is writable until a successful end/begin cycle.
 */
bool lvgl_port_camera_present(lv_display_t *disp, uint32_t timeout_ms);

/** Caller holds LVGL lock and has stopped the camera worker. Restore the normal
 * path only after a bounded scanout handoff; false keeps flushes suppressed and
 * all panel-owned framebuffers intact. May be retried, including after a failed
 * begin. Successful recovery invalidates the screen and resumes its refresh
 * timer for a complete LVGL redraw (including a timer LVGL paused while idle).
 */
bool lvgl_port_camera_end(lv_display_t *disp, uint32_t timeout_ms);
#endif

#ifdef __cplusplus
}
#endif
