#pragma once
#include "lvgl.h"

// Returns false without touching the destination for unsupported transforms.
bool tab5_draw_orthogonal(const lv_area_t *area,const void *source,int32_t width,int32_t height,
                          int32_t stride,const lv_draw_image_dsc_t *dsc,lv_color_format_t format,void *destination);
extern volatile unsigned tab5_orthogonal_calls,tab5_orthogonal_pixels,tab5_orthogonal_us,tab5_orthogonal_peak_us;
