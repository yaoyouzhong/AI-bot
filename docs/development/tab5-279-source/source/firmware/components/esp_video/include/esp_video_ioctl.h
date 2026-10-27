/*
 * SPDX-FileCopyrightText: 2024 Espressif Systems (Shanghai) CO LTD
 *
 * SPDX-License-Identifier: ESPRESSIF MIT
 */

#pragma once

#include "linux/videodev2.h"
#include "esp_cam_sensor_types.h"

#ifdef __cplusplus
extern "C" {
#endif

#define VIDIOC_S_SENSOR_FMT _IOWR('V', BASE_VIDIOC_PRIVATE + 1, esp_cam_sensor_format_t)
#define VIDIOC_G_SENSOR_FMT _IOWR('V', BASE_VIDIOC_PRIVATE + 2, esp_cam_sensor_format_t)

// TAB5 extension: a stalled sensor must not make closing the camera wait forever.
typedef struct {
    struct v4l2_buffer buffer;
    uint32_t timeout_ms;
} esp_video_dqbuf_timeout_t;
#define VIDIOC_DQBUF_TIMEOUT _IOWR('V', BASE_VIDIOC_PRIVATE + 3, esp_video_dqbuf_timeout_t)

#ifdef __cplusplus
}
#endif
