// Desktop-only experiment. Device transitions stay disabled until measured
// hardware smoothness justifies enabling them; responsiveness takes priority.
#ifdef TAB5_UI_PREVIEW
// Reveal only the newly exposed band. Keep both RGB frames opaque: no
// full-screen alpha layer, temporary snapshot, or third decoder buffer.
static lv_obj_t *gallery_reveal,*gallery_old_view;
static gallery_image gallery_old_image;
static lv_image_dsc_t gallery_old_descriptor;
static const uint16_t gallery_empty_pixels[4];
static const lv_image_dsc_t gallery_empty_descriptor={.header={.magic=LV_IMAGE_HEADER_MAGIC,
    .cf=LV_COLOR_FORMAT_RGB565,.w=2,.h=2,.stride=4},.data_size=8,.data=(const uint8_t *)gallery_empty_pixels};
static bool gallery_motion_active,gallery_motion_owned,gallery_motion_vertical,gallery_motion_reverse;
static int gallery_motion_progress,gallery_motion_extent,gallery_motion_step=1;
#ifdef TAB5_UI_PREVIEW
// Legacy fixtures inspect immediate states; dedicated motion fixtures run the
// same production callbacks and LVGL clock with this enabled.
static bool preview_gallery_motion;
#endif
static bool gallery_motion_enabled(void) {
#ifdef TAB5_UI_PREVIEW
    return preview_gallery_motion;
#else
    return true;
#endif
}
static void gallery_motion_frame(void *unused,int32_t progress) {
    (void)unused;if(!gallery_motion_active)return;
    if(progress<gallery_motion_progress)progress=gallery_motion_progress;
    if(progress>gallery_motion_extent)progress=gallery_motion_extent;
    int old=gallery_motion_progress;if(progress==old)return;
    lv_display_t *display=lv_obj_get_display(gallery_layer);
    lv_display_enable_invalidation(display,false);
    int origin=gallery_motion_reverse?gallery_motion_extent-progress:0;
    lv_obj_set_pos(gallery_reveal,gallery_motion_vertical?0:origin,gallery_motion_vertical?origin:0);
    lv_obj_set_size(gallery_reveal,gallery_motion_vertical?1280:progress,gallery_motion_vertical?progress:720);
    bool inverted=lv_image_get_rotation(gallery_view)!=0;
    lv_obj_set_pos(gallery_view,(inverted?-1:0)-(gallery_motion_vertical?0:origin),
        (inverted?-1:0)-(gallery_motion_vertical?origin:0));
    lv_obj_update_layout(gallery_reveal);
    lv_display_enable_invalidation(display,true);
    int start=gallery_motion_reverse?gallery_motion_extent-progress:old;
    int end=gallery_motion_reverse?gallery_motion_extent-old-1:progress-1;
    lv_area_t band=gallery_motion_vertical?(lv_area_t){0,start,1279,end}:(lv_area_t){start,0,end,719};
    lv_obj_invalidate_area(gallery_layer,&band);gallery_motion_progress=progress;
}
static void gallery_motion_finish(void) {
    if(!gallery_reveal)return;
    if(!gallery_motion_active&&!gallery_old_image.pixels)return;
    lv_anim_delete(gallery_reveal,gallery_motion_frame);
    if(gallery_motion_active)gallery_motion_frame(NULL,gallery_motion_extent);
    gallery_motion_active=false;
    lv_display_t *display=lv_obj_get_display(gallery_layer);
    lv_display_enable_invalidation(display,false);
    visible(gallery_old_view,false);lv_image_set_src(gallery_old_view,&gallery_empty_descriptor);
    lv_image_cache_drop(&gallery_old_descriptor);
    lv_display_enable_invalidation(display,true);
    if(gallery_motion_owned)gallery_recycle(gallery_old_image.pixels);
    gallery_old_image=(gallery_image){0};gallery_motion_owned=false;
}
static void gallery_motion_done(lv_anim_t *animation) {(void)animation;gallery_motion_finish();}
static void gallery_motion_hold(int slot,bool owned) {
    if(!gallery_motion_enabled()||!gallery_cache[slot].pixels||lv_obj_has_flag(gallery_view,LV_OBJ_FLAG_HIDDEN))return;
    gallery_motion_finish();gallery_old_image=gallery_cache[slot];gallery_old_descriptor=gallery_descriptor[slot];
    lv_image_set_src(gallery_old_view,&gallery_old_descriptor);
    lv_image_set_pivot(gallery_old_view,640,360);int angle=lv_image_get_rotation(gallery_view);
    lv_image_set_rotation(gallery_old_view,angle);lv_obj_set_pos(gallery_old_view,angle?-1:0,angle?-1:0);
    gallery_motion_owned=owned;
    if(owned){gallery_cache[slot]=(gallery_image){0};gallery_descriptor[slot]=(lv_image_dsc_t){0};}
}
static void gallery_motion_begin(void) {
    if(!gallery_old_image.pixels)return;
    gallery_motion_vertical=gallery_portrait();
    gallery_motion_reverse=(gallery_orientation==2||gallery_orientation==-1)^(gallery_motion_step<0);
    gallery_motion_extent=gallery_motion_vertical?720:1280;gallery_motion_progress=0;
    gallery_motion_active=true;visible(gallery_old_view,true);
    // Zero-sized clipping starts with the previous complete image visible.
    lv_obj_set_size(gallery_reveal,gallery_motion_vertical?1280:0,gallery_motion_vertical?0:720);
    lv_anim_t animation;lv_anim_init(&animation);lv_anim_set_var(&animation,gallery_reveal);
    lv_anim_set_values(&animation,0,gallery_motion_extent);lv_anim_set_duration(&animation,180);
    lv_anim_set_exec_cb(&animation,gallery_motion_frame);lv_anim_set_path_cb(&animation,lv_anim_path_ease_out);
    lv_anim_set_completed_cb(&animation,gallery_motion_done);lv_anim_start(&animation);
    // Resolve visibility/clipping layout while the caller still suppresses
    // invalidation. A deferred layout pass would damage the whole old frame.
    lv_obj_update_layout(gallery_layer);
}
static void gallery_motion_create(void) {
    if(gallery_reveal||(!preview_gallery_motion&&!getenv("TAB5_GALLERY_MOTION_ONLY")))return;
    gallery_old_view=lv_image_create(gallery_layer);lv_obj_remove_flag(gallery_old_view,LV_OBJ_FLAG_CLICKABLE);visible(gallery_old_view,false);
    gallery_reveal=lv_obj_create(gallery_layer);lv_obj_remove_style_all(gallery_reveal);
    lv_obj_remove_flag(gallery_reveal,LV_OBJ_FLAG_CLICKABLE|LV_OBJ_FLAG_SCROLLABLE|LV_OBJ_FLAG_OVERFLOW_VISIBLE);
    lv_obj_set_size(gallery_reveal,1280,720);lv_obj_set_parent(gallery_view,gallery_reveal);
}
#else
static const bool gallery_motion_active=false;
static const gallery_image gallery_old_image;
static int gallery_motion_step;
static inline bool gallery_motion_enabled(void){return false;}
static inline void gallery_motion_finish(void){}
static inline void gallery_motion_hold(int slot,bool owned){(void)slot;(void)owned;}
static inline void gallery_motion_begin(void){}
static inline void gallery_motion_create(void){}
#endif
