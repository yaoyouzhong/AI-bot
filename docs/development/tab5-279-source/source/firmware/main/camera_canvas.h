#pragma once
#include "lvgl.h"
#include "camera_frame.h"
#include "camera_geometry.h"

typedef struct {
    lv_obj_t *row_view,*column_view;
    lv_image_dsc_t row,column;
    int pose;
    uint16_t row_pixels[1280],column_pixels[1280*2];
} camera_canvas_edges;

static inline void camera_canvas_edge_event(lv_event_t *event) {
    camera_canvas_edges *edges=lv_event_get_user_data(event);
    if(lv_event_get_code(event)==LV_EVENT_DELETE) {
        lv_obj_delete(edges->row_view);lv_obj_delete(edges->column_view);
        lv_obj_set_user_data(lv_event_get_current_target(event),NULL);lv_free(edges);return;
    }
    lv_obj_t *canvas=lv_event_get_current_target(event);
    if(!lv_image_get_rotation(canvas))return;
    lv_draw_buf_t *image=lv_canvas_get_draw_buf(canvas);
    if(!image||!image->data)return;
    const uint16_t *pixels=(const uint16_t *)image->data;
    unsigned w=camera_pose_width(edges->pose),h=camera_pose_height(edges->pose);
    for(unsigned x=0;x<w;x++)edges->row_pixels[x]=pixels[camera_pose_source(edges->pose,x,h-1)];
    for(unsigned y=0;y<h;y++)edges->column_pixels[y*2]=pixels[camera_pose_source(edges->pose,w-1,y)];
    lv_image_cache_drop(&edges->row);lv_image_cache_drop(&edges->column);
}

static inline void camera_canvas_extent(lv_event_t *event) {
    // Discrete rotated edges reach one pixel beyond LVGL's continuous bounds.
    // Preserve that edge instead of clipping the review's last row/column.
    int32_t *size=lv_event_get_param(event);
    if(lv_image_get_rotation(lv_event_get_current_target(event)))(*size)++;
}
static inline void camera_canvas_init(lv_obj_t *canvas) {
    lv_image_set_pivot(canvas,CAMERA_VIEW_WIDTH/2,CAMERA_VIEW_HEIGHT/2);
    lv_obj_add_event_cb(canvas,camera_canvas_extent,LV_EVENT_REFR_EXT_DRAW_SIZE,NULL);
    // LVGL's continuous rotation bounds omit the final discrete pixel row and
    // column. Retain only those two strips (7,680 bytes), never another frame.
    camera_canvas_edges *edges=lv_malloc_zeroed(sizeof(*edges));LV_ASSERT_MALLOC(edges);
    if(!edges)return;
    edges->row=(lv_image_dsc_t){.header={.magic=LV_IMAGE_HEADER_MAGIC,.cf=LV_COLOR_FORMAT_RGB565,
        .w=CAMERA_VIEW_WIDTH,.h=1,.stride=CAMERA_VIEW_WIDTH*2},
        .data_size=sizeof(edges->row_pixels),.data=(const uint8_t *)edges->row_pixels};
    edges->column=(lv_image_dsc_t){.header={.magic=LV_IMAGE_HEADER_MAGIC,.cf=LV_COLOR_FORMAT_RGB565,
        .w=1,.h=CAMERA_VIEW_HEIGHT,.stride=4},
        .data_size=sizeof(edges->column_pixels),.data=(const uint8_t *)edges->column_pixels};
    edges->row_view=lv_image_create(canvas);lv_image_set_src(edges->row_view,&edges->row);
    lv_obj_set_pos(edges->row_view,1,CAMERA_VIEW_HEIGHT);
    edges->column_view=lv_image_create(canvas);lv_image_set_src(edges->column_view,&edges->column);
    lv_obj_set_pos(edges->column_view,CAMERA_VIEW_WIDTH,1);
    lv_obj_add_flag(canvas,LV_OBJ_FLAG_OVERFLOW_VISIBLE);
    lv_obj_add_flag(edges->row_view,LV_OBJ_FLAG_HIDDEN);lv_obj_add_flag(edges->column_view,LV_OBJ_FLAG_HIDDEN);
    lv_obj_set_user_data(canvas,edges);
    lv_obj_add_event_cb(canvas,camera_canvas_edge_event,LV_EVENT_DRAW_MAIN,edges);
    lv_obj_add_event_cb(canvas,camera_canvas_edge_event,LV_EVENT_DELETE,edges);
}
static inline void camera_canvas_direction(lv_obj_t *canvas,int pose) {
    lv_obj_set_size(canvas,CAMERA_VIEW_WIDTH,CAMERA_VIEW_HEIGHT);
    int angle=pose<0?2700:pose*900;
    int px=pose==1?-281:pose==-1?-280:pose==2?-1:0;
    int py=pose==1?280:pose==-1?279:pose==2?-1:0;
    lv_image_set_scale(canvas,256);lv_image_set_pivot(canvas,640,360);
    lv_image_set_rotation(canvas,angle);lv_obj_set_pos(canvas,px,py);
    camera_canvas_edges *edges=lv_obj_get_user_data(canvas);
    if(edges) {
        edges->pose=pose;
        unsigned w=camera_pose_width(pose),h=camera_pose_height(pose);
        if(edges->row.header.w!=w||edges->column.header.h!=h) {
            edges->row.header.w=w;edges->row.header.stride=w*2;edges->column.header.h=h;
            lv_image_cache_drop(&edges->row);lv_image_cache_drop(&edges->column);
            lv_image_set_src(edges->row_view,&edges->row);lv_image_set_src(edges->column_view,&edges->column);
        }
        lv_obj_set_pos(edges->row_view,-px,(int)h-1-py);
        lv_obj_set_pos(edges->column_view,(int)w-1-px,-py);
        lv_obj_t *views[]={edges->row_view,edges->column_view};
        for(unsigned i=0;i<2;i++)if(lv_obj_has_flag(views[i],LV_OBJ_FLAG_HIDDEN)==(pose!=0)) {
            if(pose)lv_obj_remove_flag(views[i],LV_OBJ_FLAG_HIDDEN);
            else lv_obj_add_flag(views[i],LV_OBJ_FLAG_HIDDEN);
        }
    }
}
static inline void camera_canvas_review(lv_obj_t *canvas) {
    camera_canvas_edges *edges=lv_obj_get_user_data(canvas);
    if(lv_image_get_rotation(canvas)||(edges&&edges->pose))camera_canvas_direction(canvas,0);
    lv_draw_buf_t *image=lv_canvas_get_draw_buf(canvas);if(!image||!image->data)return;
    lv_obj_set_size(canvas,image->header.w,image->header.h);
    int w=lv_display_get_horizontal_resolution(NULL),h=lv_display_get_vertical_resolution(NULL);
    int scale=LV_MIN(w*256/(int)image->header.w,h*256/(int)image->header.h);
    lv_image_set_pivot(canvas,0,0);lv_image_set_scale(canvas,scale);
    lv_obj_set_pos(canvas,(w-(int)image->header.w*scale/256)/2,(h-(int)image->header.h*scale/256)/2);
}
