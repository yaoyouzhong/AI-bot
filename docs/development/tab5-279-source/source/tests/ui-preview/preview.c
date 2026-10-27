#define TAB5_UI_PREVIEW
#undef NDEBUG
#include "../../firmware/main/link_state.h"
// The native renderer has no authenticated report transport. Exercise real
// reporting in the separate boot worker/RPC tests; never synthesize success.
static bool boot_verify_report(const char *id,const char *session,link_kind link){(void)id;(void)session;(void)link;return false;}
static bool runtime_diagnostics_report(const char *id,const char *session,link_kind link){(void)id;(void)session;(void)link;return false;}
static bool screen_preview_request(const char *stream,const char *request,const char *session,link_kind link,int width,int height,int version,const char *encoding,int64_t deadline){(void)version;(void)encoding;(void)stream;(void)request;(void)session;(void)link;(void)width;(void)height;(void)deadline;return false;}
void gallery_cache_reclaim(void){}
static bool screen_preview_busy(void){return false;}
static unsigned preview_scratch_reclaims;
static unsigned screen_preview_cache_reclaim(bool force){if(force)preview_scratch_reclaims++;return 0;}
#include "../../firmware/main/ui.c"
#include "../../firmware/main/art_orientation_filter.h"
#include <assert.h>
#if TAB5_LOCAL_ART
static void camera_gallery_released_probe(void) {
    assert(lv_obj_has_flag(gallery_view,LV_OBJ_FLAG_HIDDEN)&&!lv_image_get_src(gallery_view));
    assert(!gallery_presented.pixels);
    for(int i=0;i<4;i++)assert(!gallery_cache[i].pixels&&!gallery_descriptor[i].data);
}
#endif
LV_FONT_DECLARE(calendar_compare_regular_24);
LV_FONT_DECLARE(calendar_compare_running_26);
#include "src/core/lv_obj_event_private.h"
#include "src/core/lv_obj_scroll_private.h"
#include "src/draw/lv_draw_private.h"
#include "src/draw/lv_draw_buf_private.h"
#include "src/draw/lv_image_decoder_private.h"
#include "src/display/lv_display_private.h"
#include "src/misc/lv_area_private.h"
#ifdef _WIN32
#include <windows.h>
#endif
static lv_point_t camera_touch_point;
static lv_indev_state_t camera_touch_state;
static unsigned camera_still_flushes;
static unsigned camera_draw_tasks,camera_underlay_tasks,camera_image_tasks;
static unsigned audit_fill_pixels,audit_image_pixels,audit_argb_pixels;
static uint16_t *camera_partial_pixels;
static bool gallery_measure_flush;
static unsigned gallery_motion_flush_pixels;
static void camera_partial_flush(lv_display_t *display,const lv_area_t *area,uint8_t *pixels) {
    if(gallery_measure_flush)gallery_motion_flush_pixels+=(unsigned)lv_area_get_size(area);
    int width=area->x2-area->x1+1;
    assert(area->x1>=0&&area->x2<1280&&area->y1>=0&&area->y2<720);
    assert(!(width&1)&&!((area->y2-area->y1+1)&1));
    if(tab5_draw_cache_take_native(pixels,area)) {
        int height=lv_area_get_height(area);
        for(int y=area->y1;y<=area->y2;y++)for(int x=area->x1;x<=area->x2;x++)
            camera_partial_pixels[y*1280+x]=((uint16_t *)pixels)[(area->x2-x)*height+y-area->y1];
    } else for(int y=area->y1;y<=area->y2;y++)
        memcpy(camera_partial_pixels+y*1280+area->x1,pixels+(y-area->y1)*width*2,width*2);
    lv_display_flush_ready(display);
}
static void camera_draw_audit(lv_event_t *event) {
    lv_draw_task_t *task=lv_event_get_draw_task(event);
    lv_draw_dsc_base_t *base=lv_draw_task_get_draw_dsc(task);
    camera_draw_tasks++;
    lv_area_t clipped;
    if(lv_area_intersect(&clipped,&task->area,&task->clip_area)) {
        unsigned pixels=lv_area_get_size(&clipped);
        if(lv_draw_task_get_fill_dsc(task))audit_fill_pixels+=pixels;
        if(lv_draw_task_get_image_dsc(task))audit_image_pixels+=pixels;
        if(base->layer->color_format==LV_COLOR_FORMAT_ARGB8888)audit_argb_pixels+=pixels;
    }
    lv_obj_t *ancestor=base->obj;
    while(ancestor&&ancestor!=camera_layer)ancestor=lv_obj_get_parent(ancestor);
    if(!ancestor||base->obj==camera_layer)camera_underlay_tasks++;
    if(base->obj==camera_canvas)camera_image_tasks++;
    if(!getenv("TAB5_CAMERA_DRAW_AUDIT"))return;
    lv_area_t area;lv_draw_task_get_area(task,&area);
    printf("CAMERA_DRAW type=%d object=%s area=%ld,%ld,%ld,%ld layer_cf=%d\n",
        lv_draw_task_get_type(task),base->obj==camera_canvas?"canvas":base->obj==camera_layer?"camera":"other",
        (long)area.x1,(long)area.y1,(long)area.x2,(long)area.y2,base->layer->color_format);
    printf("  clip=%d,%d,%d,%d\n",task->clip_area.x1,task->clip_area.y1,task->clip_area.x2,task->clip_area.y2);
    lv_draw_image_dsc_t *image=lv_draw_task_get_image_dsc(task);
    if(image)printf("  image cf=%d opa=%d recolor=%d rotation=%ld scale=%ld,%ld clip_radius=%ld mask=%d\n",
        image->header.cf,image->opa,image->recolor_opa,(long)image->rotation,
        (long)image->scale_x,(long)image->scale_y,(long)image->clip_radius,image->bitmap_mask_src!=NULL);
    lv_draw_fill_dsc_t *fill=lv_draw_task_get_fill_dsc(task);
    if(fill)printf("  fill opa=%d radius=%ld gradient=%d\n",fill->opa,(long)fill->radius,fill->grad.dir);
}
static void camera_audit_tree(lv_obj_t *obj,bool enabled) {
    if(enabled){lv_obj_add_flag(obj,LV_OBJ_FLAG_SEND_DRAW_TASK_EVENTS);lv_obj_add_event_cb(obj,camera_draw_audit,LV_EVENT_DRAW_TASK_ADDED,NULL);}
    else {lv_obj_remove_flag(obj,LV_OBJ_FLAG_SEND_DRAW_TASK_EVENTS);lv_obj_remove_event_cb(obj,camera_draw_audit);}
    for(uint32_t i=0;i<lv_obj_get_child_count(obj);i++)camera_audit_tree(lv_obj_get_child(obj,i),enabled);
}
static void check_camera_occlusion(void) {
    lv_display_t *d=lv_display_get_default();
    lv_obj_invalidate(camera_canvas);lv_refr_now(d);
    uint16_t *expected=malloc(sizeof(preview_buffer));assert(expected);
    memcpy(expected,preview_buffer,sizeof(preview_buffer));
    camera_partial_pixels=calloc(1,sizeof(preview_buffer));assert(camera_partial_pixels);
    lv_display_set_flush_cb(d,camera_partial_flush);
    // Match the device's partial buffer, including the shorter final strip.
    lv_display_set_buffers(d,preview_buffer,NULL,TAB5_DRAW_BUFFER_PIXELS*2,LV_DISPLAY_RENDER_MODE_PARTIAL);
    camera_draw_tasks=camera_underlay_tasks=camera_image_tasks=0;
    camera_audit_tree(lv_screen_active(),true);
    lv_obj_invalidate(camera_canvas);lv_refr_now(d);
    camera_audit_tree(lv_screen_active(),false);
    assert(camera_underlay_tasks==0&&camera_image_tasks==(720+TAB5_DRAW_STRIP_ROWS-1)/TAB5_DRAW_STRIP_ROWS);
    printf("CAMERA_OCCLUSION_OK tasks=%u hidden_page_and_background=%u image_strips=%u\n",
        camera_draw_tasks,camera_underlay_tasks,camera_image_tasks);
    assert(!memcmp(expected,camera_partial_pixels,sizeof(preview_buffer)));
    free(expected);free(camera_partial_pixels);camera_partial_pixels=NULL;
    lv_display_set_flush_cb(d,preview_flush);
    lv_display_set_buffers(d,preview_buffer,NULL,sizeof(preview_buffer),LV_DISPLAY_RENDER_MODE_FULL);
    lv_area_t outside={-1,0,1279,719};
    lv_cover_check_info_t check={.area=&outside,.res=LV_COVER_RES_COVER};
    lv_obj_send_event(camera_canvas,LV_EVENT_COVER_CHECK,&check);
    assert(check.res==LV_COVER_RES_NOT_COVER);
    lv_area_t inside={0,0,1279,719};check.area=&inside;
    lv_obj_set_style_image_opa(camera_canvas,LV_OPA_50,0);
    check.res=LV_COVER_RES_COVER;lv_obj_send_event(camera_canvas,LV_EVENT_COVER_CHECK,&check);
    assert(check.res==LV_COVER_RES_NOT_COVER);
    lv_obj_set_style_image_opa(camera_canvas,LV_OPA_COVER,0);
}
static void camera_still_flush(lv_event_t *event) {
    (void)event;camera_still_flushes++;
}
#if TAB5_LOCAL_ART
static void capture(const char *name);
static bool gallery_original_reviewed(cJSON *original) {
    const char *status=text(original,"status");
    return !strcmp(status,"source_verified")||!strcmp(status,"reference_verified");
}
static void check_gallery_guide_catalog(int category) {
    const char *path=getenv("TAB5_GALLERY_GUIDES");if(!path)return;
    FILE *file=fopen(path,"rb");assert(file);fseek(file,0,SEEK_END);long size=ftell(file);rewind(file);
    char *json=calloc(1,(size_t)size+1);assert(json&&fread(json,1,(size_t)size,file)==(size_t)size);fclose(file);
    cJSON *root=cJSON_Parse(json);free(json);assert(root);cJSON *works=cJSON_GetObjectItemCaseSensitive(root,"works");
    const char *fields[]={"title","author","period","museum","brief","overview","looking"};
    cJSON *longest=NULL,*row=NULL;size_t longest_bytes=0;unsigned checked=0;
    cJSON_ArrayForEach(row,works) {
        const char *kind=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(row,"category"));
        if(strcmp(kind,category?"calligraphy":"painting"))continue;checked++;
        const char *title=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(row,"title"));
        if(strlen(title)>longest_bytes){longest_bytes=strlen(title);longest=row;}
        for(int i=0;i<9;i++) {
            cJSON *transcription=cJSON_GetObjectItemCaseSensitive(row,"transcription");
            if(i>=7&&!gallery_original_reviewed(transcription))continue;
            const char *text=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(i>=7?transcription:row,i>=7?(i==7?"text":"explanation"):fields[i]));
            if(!text)continue;
            for(uint32_t at=0;text[at];){uint32_t cp=lv_text_encoded_next(text,&at);lv_font_glyph_dsc_t glyph;
                if(cp!='\n'&&cp!='\r')assert(lv_font_get_glyph_dsc(&tab5_font_24,&glyph,cp,0)&&!glyph.is_placeholder);}
        }
    }
    assert(checked==(category?867:1765)&&longest);
    memset(&preview_guide,0,sizeof(preview_guide));strcpy(preview_guide.date,"2026-10-06");
    #define GUIDE_FIXTURE_FIELD(name) snprintf(preview_guide.name,sizeof(preview_guide.name),"%s",cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(longest,#name)))
    snprintf(preview_guide.work_id,sizeof(preview_guide.work_id),"%s",cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(longest,"workId")));
    GUIDE_FIXTURE_FIELD(title);GUIDE_FIXTURE_FIELD(author);GUIDE_FIXTURE_FIELD(period);GUIDE_FIXTURE_FIELD(museum);
    GUIDE_FIXTURE_FIELD(overview);GUIDE_FIXTURE_FIELD(looking);
    #undef GUIDE_FIXTURE_FIELD
    for(int i=category*2;i<category*2+2;i++)if(gallery_cache[i].pixels)strcpy(gallery_cache[i].work_id,preview_guide.work_id);
    for(int orientation=0;orientation<2;orientation++){
        gallery_orientation=orientation;gallery_show();lv_obj_send_event(gallery_details_button,LV_EVENT_CLICKED,NULL);
        assert(gallery_details_open);lv_obj_update_layout(gallery_reader);
        assert(lv_obj_get_height(gallery_reader_copy)>150&&lv_obj_get_y(gallery_reader_copy)+lv_obj_get_height(gallery_reader_copy)<lv_obj_get_height(gallery_reader)-78);
        capture(category?(orientation?"gallery-reader-long-calligraphy-portrait":"gallery-reader-long-calligraphy"):(orientation?"gallery-reader-long-painting-portrait":"gallery-reader-long-painting"));
        gallery_reader_close();
    }
    if(category) {
        cJSON *reviewed=NULL;unsigned originals=0;
        cJSON_ArrayForEach(row,works) {
            cJSON *original=item(row,"transcription");
            if(!gallery_original_reviewed(original))continue;
            assert(strlen(text(original,"text"))<sizeof(preview_guide.transcription));
            assert(strlen(text(original,"explanation"))<sizeof(preview_guide.explanation));
            originals++;
            if(!reviewed||strlen(text(original,"text"))>strlen(text(item(reviewed,"transcription"),"text")))reviewed=row;
        }
        assert(reviewed&&originals>=87);
        gallery_guide saved=preview_guide;
        #define ORIGINAL_FIXTURE_FIELD(name) snprintf(preview_guide.name,sizeof(preview_guide.name),"%s",text(reviewed,#name))
        snprintf(preview_guide.work_id,sizeof(preview_guide.work_id),"%s",text(reviewed,"workId"));
        ORIGINAL_FIXTURE_FIELD(title);ORIGINAL_FIXTURE_FIELD(author);ORIGINAL_FIXTURE_FIELD(period);ORIGINAL_FIXTURE_FIELD(museum);
        #undef ORIGINAL_FIXTURE_FIELD
        snprintf(preview_guide.transcription,sizeof(preview_guide.transcription),"%s",text(item(reviewed,"transcription"),"text"));
        snprintf(preview_guide.explanation,sizeof(preview_guide.explanation),"%s",text(item(reviewed,"transcription"),"explanation"));
        for(int i=category*2;i<category*2+2;i++)if(gallery_cache[i].pixels)strcpy(gallery_cache[i].work_id,preview_guide.work_id);
        for(int orientation=0;orientation<2;orientation++) {
            gallery_orientation=orientation;gallery_show();unsigned requests=preview_gallery_requests;
            lv_obj_send_event(gallery_details_button,LV_EVENT_CLICKED,NULL);
            lv_obj_send_event(gallery_chapters[2],LV_EVENT_CLICKED,NULL);
            assert(gallery_details_open&&gallery_reading_chapter==2&&preview_gallery_requests==requests);
            assert(strstr(lv_label_get_text(gallery_reader_body),preview_guide.transcription)&&strstr(lv_label_get_text(gallery_reader_body),"说明"));
            capture(orientation?"gallery-original-reviewed-portrait":"gallery-original-reviewed");
            lv_obj_update_layout(gallery_reader_copy);
            assert(lv_obj_get_scroll_bottom(gallery_reader_copy)>0);
            lv_obj_scroll_to_y(gallery_reader_copy,LV_COORD_MAX,LV_ANIM_OFF);lv_obj_update_layout(gallery_reader_copy);
            assert(lv_obj_get_scroll_bottom(gallery_reader_copy)==0&&strstr(lv_label_get_text(gallery_reader_body),preview_guide.transcription));
            capture(orientation?"gallery-original-end-portrait":"gallery-original-end");
            lv_obj_send_event(gallery_chapters[0],LV_EVENT_CLICKED,NULL);
            lv_obj_send_event(gallery_chapters[2],LV_EVENT_CLICKED,NULL);
            assert(lv_obj_get_scroll_y(gallery_reader_copy)==0&&preview_gallery_requests==requests);
            gallery_reader_close();
        }
        // The daily user example must render its complete eight lines as well.
        cJSON_ArrayForEach(row,works)if(!strcmp(text(row,"workId"),"colbase:tnm:TB-1261")) {
            snprintf(preview_guide.work_id,sizeof(preview_guide.work_id),"%s",text(row,"workId"));
            snprintf(preview_guide.title,sizeof(preview_guide.title),"%s",text(row,"title"));
            snprintf(preview_guide.author,sizeof(preview_guide.author),"%s",text(row,"author"));
            snprintf(preview_guide.period,sizeof(preview_guide.period),"%s",text(row,"period"));
            snprintf(preview_guide.museum,sizeof(preview_guide.museum),"%s",text(row,"museum"));
            snprintf(preview_guide.transcription,sizeof(preview_guide.transcription),"%s",text(item(row,"transcription"),"text"));
            snprintf(preview_guide.explanation,sizeof(preview_guide.explanation),"%s",text(item(row,"transcription"),"explanation"));
            for(int i=2;i<4;i++)if(gallery_cache[i].pixels)strcpy(gallery_cache[i].work_id,preview_guide.work_id);
            gallery_orientation=0;gallery_show();lv_obj_send_event(gallery_details_button,LV_EVENT_CLICKED,NULL);
            lv_obj_send_event(gallery_chapters[2],LV_EVENT_CLICKED,NULL);
            assert(strstr(lv_label_get_text(gallery_reader_body),"满市花风起")&&strstr(lv_label_get_text(gallery_reader_body),"转盼夕烟浮"));
            capture("gallery-original-qin-guan");gallery_reader_close();break;
        }
        preview_guide=saved;
        for(int i=category*2;i<category*2+2;i++)if(gallery_cache[i].pixels)strcpy(gallery_cache[i].work_id,saved.work_id);
        printf("GALLERY_ORIGINAL_OK all_%u_texts_and_glyphs, full_longest_original_to_end, complete_Qin_Guan, offline_open, portrait_and_landscape\n",originals);
    }
    cJSON_Delete(root);printf("GALLERY_GUIDE_CATALOG_OK category=%d works=%u all glyphs including verified transcriptions; longest real title and text in both layouts\n",category,checked);
}
static void check_gallery_replacements(int category,struct tm *day) {
    lv_display_t *display=lv_display_get_default();
    uint16_t *expected=malloc(sizeof(preview_buffer));assert(expected);
    camera_partial_pixels=malloc(sizeof(preview_buffer));assert(camera_partial_pixels);
    for(int update=0;update<12;update++) {
        bool portrait=update%2;int frame=(update/2)%3;
        uint16_t *pixels=malloc(sizeof(preview_buffer));assert(pixels);
        for(int i=0;i<1280*720;i++)pixels[i]=(uint16_t)(i*31+update*197);
        preview_art_orientation.orientation=portrait?-1:0;
        preview_gallery_result=(gallery_image){.pixels=(uint8_t *)pixels,.category=category,.frame=frame,.frames=3,.portrait=portrait};
        strcpy(preview_gallery_result.date,"2026-10-06");
        gallery_update(day);
        // First verify every source pixel with the intentional footer overlays
        // hidden, then verify partial rendering of the complete visible UI.
        bool date_visible=!lv_obj_has_flag(gallery_date,LV_OBJ_FLAG_HIDDEN);
        bool footer_visible=!lv_obj_has_flag(gallery_controls,LV_OBJ_FLAG_HIDDEN);
        bool previous_visible=!lv_obj_has_flag(gallery_previous,LV_OBJ_FLAG_HIDDEN),next_visible=!lv_obj_has_flag(gallery_next,LV_OBJ_FLAG_HIDDEN);
        visible(gallery_date,false);visible(gallery_controls,false);visible(gallery_previous,false);visible(gallery_next,false);lv_refr_now(display);
        assert(!memcmp(preview_buffer,pixels,sizeof(preview_buffer)));
        visible(gallery_date,date_visible);visible(gallery_controls,footer_visible);visible(gallery_previous,previous_visible);visible(gallery_next,next_visible);lv_refr_now(display);
        memcpy(expected,preview_buffer,sizeof(preview_buffer));
        memset(camera_partial_pixels,0xDA,sizeof(preview_buffer));
        lv_display_set_flush_cb(display,camera_partial_flush);
        lv_display_set_buffers(display,preview_buffer,NULL,TAB5_DRAW_BUFFER_PIXELS*2,LV_DISPLAY_RENDER_MODE_PARTIAL);
        lv_obj_invalidate(gallery_layer);lv_refr_now(display);
        assert(!memcmp(expected,camera_partial_pixels,sizeof(preview_buffer)));
        lv_display_set_flush_cb(display,preview_flush);
        lv_display_set_buffers(display,preview_buffer,NULL,sizeof(preview_buffer),LV_DISPLAY_RENDER_MODE_FULL);
        int cached=0;for(int i=0;i<4;i++)if(gallery_cache[i].pixels)cached++;assert(cached<=2);
    }
    free(expected);free(camera_partial_pixels);camera_partial_pixels=NULL;
    preview_art_orientation.orientation=0;gallery_update(day);
    printf("GALLERY_REPLACEMENT_OK category=%d 12 layouts, 3 pages, all rows, partial final strip\n",category);
}
#endif
static void check_camera_idle_redraw(void) {
    camera_tick();lv_refr_now(NULL);
    camera_still_flushes=0;
    lv_display_t *display=lv_display_get_default();
    lv_display_add_event_cb(display,camera_still_flush,LV_EVENT_FLUSH_START,NULL);
    // Run the actual camera controls repeatedly without producing a new frame.
    // Neither live waiting nor a captured still should invalidate any pixels.
    for(unsigned i=0;i<60;i++){camera_tick();lv_refr_now(display);}
    assert(camera_still_flushes==0);
    assert(lv_display_remove_event_cb_with_user_data(display,camera_still_flush,NULL)==1);
}
static void camera_touch_read(lv_indev_t *indev,lv_indev_data_t *data) {
    (void)indev;data->point=camera_touch_point;data->state=camera_touch_state;
}
#if TAB5_LOCAL_ART
static lv_point_t gallery_physical_point(int x,int y,int angle) {
    if(angle==900)return (lv_point_t){1280-y,x};
    if(angle==1800)return (lv_point_t){1280-x,720-y};
    if(angle==2700)return (lv_point_t){y,720-x};
    return (lv_point_t){x,y};
}
static void gallery_pointer_click(lv_indev_t *touch,lv_point_t point) {
    camera_touch_point=point;camera_touch_state=LV_INDEV_STATE_PRESSED;lv_indev_read(touch);
    camera_touch_state=LV_INDEV_STATE_RELEASED;lv_indev_read(touch);
}
static void gallery_motion_fixture(uint8_t *pixels,int category,bool portrait,int frame) {
    const char *root=getenv("TAB5_GALLERY_MOTION_FIXTURES");if(!root)return;
    char path[1024];snprintf(path,sizeof(path),"%s/%s-%s-%d.rgb565",root,category?"calligraphy":"painting",portrait?"portrait":"landscape",frame);
    FILE *file=fopen(path,"rb");assert(file&&fread(pixels,1,1280*720*2,file)==1280*720*2&&fgetc(file)==EOF);fclose(file);
}
static void gallery_motion_capture(const uint16_t *pixels,const char *kind,int category,int pose,int frame) {
    if(!getenv("TAB5_GALLERY_MOTION_CAPTURE"))return;
    char path[160];snprintf(path,sizeof(path),"motion-%s-%d-%d-%d.ppm",kind,category,pose,frame);
    FILE *file=fopen(path,"wb");assert(file);fprintf(file,"P6\n1280 720\n255\n");
    for(int i=0;i<1280*720;i++){
        uint16_t pixel=pixels[i];uint8_t rgb[]={(uint8_t)(((pixel>>11)&31)*255/31),(uint8_t)(((pixel>>5)&63)*255/63),(uint8_t)((pixel&31)*255/31)};
        fwrite(rgb,1,3,file);
    }fclose(file);
}
static void check_gallery_motion(struct tm *day) {
    const int paging_directions[]={0,2,1,-1};
    // Run the production reveal through the LVGL animation clock. Inspect
    // the partially drawn framebuffer before a full redraw reference, so a
    // skipped strip or incorrect inverted clip cannot hide behind a snapshot.
    preview_gallery_motion=true;
    gallery_motion_create();
    for(int kind=0;kind<2;kind++)for(int pose=0;pose<4;pose++){
        gallery_motion_finish();for(int i=0;i<4;i++)gallery_drop(i);free(gallery_take_buffer());
        int direction=paging_directions[pose];bool portrait=pose>=2;int slot=kind*2+portrait;
        saver_style=6+kind;gallery_orientation=direction;preview_art_orientation.orientation=direction;
        preview_art_orientation.ready=true;
        gallery_frame[kind]=0;gallery_manual_until=preview_now_us/1000+30000;
        gallery_return_hold=false;gallery_pending_page=(gallery_image){0};
        uint8_t *old=malloc(1280*720*2),*next=malloc(1280*720*2);assert(old&&next);
        for(int i=0;i<1280*720;i++){((uint16_t *)old)[i]=(uint16_t)(i*7+173);((uint16_t *)next)[i]=(uint16_t)(i*11+491);}
        gallery_motion_fixture(old,kind,portrait,0);gallery_motion_fixture(next,kind,portrait,1);
        gallery_cache[slot]=(gallery_image){.pixels=old,.category=kind,.frames=3,.portrait=portrait};
        strcpy(gallery_cache[slot].date,preview_guide.date);strcpy(gallery_cache[slot].work_id,preview_guide.work_id);
        gallery_descriptor[slot]=(lv_image_dsc_t){.header={.magic=LV_IMAGE_HEADER_MAGIC,.cf=LV_COLOR_FORMAT_RGB565,.w=1280,.h=720,.stride=2560},.data_size=1280*720*2,.data=old};
        gallery_show();lv_refr_now(NULL);
        gallery_motion_capture(preview_buffer,"page",kind,pose,0);
        camera_partial_pixels=malloc(sizeof(preview_buffer));assert(camera_partial_pixels);
        memcpy(camera_partial_pixels,preview_buffer,sizeof(preview_buffer));
        lv_display_t *display=lv_display_get_default();
        lv_display_set_flush_cb(display,camera_partial_flush);
        lv_display_set_buffers(display,preview_buffer,NULL,TAB5_DRAW_BUFFER_PIXELS*2,LV_DISPLAY_RENDER_MODE_PARTIAL);
        preview_gallery_result=gallery_cache[slot];preview_gallery_result.pixels=next;preview_gallery_result.frame=1;
        gallery_update(day);
        assert(gallery_motion_active&&gallery_motion_owned&&gallery_old_image.pixels==old);
        assert(gallery_cache[slot].pixels==next&&!gallery_schedule(kind,preview_guide.date,2,portrait));
        gallery_motion_flush_pixels=0;
        for(int frame=0;frame<7;frame++){
            unsigned before_damage=gallery_motion_flush_pixels;
            lv_display_set_flush_cb(display,camera_partial_flush);
            lv_display_set_buffers(display,preview_buffer,NULL,TAB5_DRAW_BUFFER_PIXELS*2,LV_DISPLAY_RENDER_MODE_PARTIAL);
            gallery_measure_flush=true;lv_tick_inc(30);lv_refr_now(display);gallery_measure_flush=false;
            printf("MOTION_DAMAGE frame=%d progress=%d pixels=%u\n",frame,gallery_motion_progress,gallery_motion_flush_pixels-before_damage);
            lv_display_set_flush_cb(display,preview_flush);
            lv_display_set_buffers(display,preview_buffer,NULL,sizeof(preview_buffer),LV_DISPLAY_RENDER_MODE_FULL);
            lv_obj_invalidate(gallery_layer);lv_refr_now(display);
            assert(!memcmp(camera_partial_pixels,preview_buffer,sizeof(preview_buffer)));
            gallery_motion_capture(camera_partial_pixels,"page",kind,pose,frame+1);
        }
        assert(gallery_motion_flush_pixels<=1280u*720u*3u/2u);
        printf("GALLERY_MOTION_DAMAGE_OK category=%d direction=%d total_pixels=%u screen_pixels=921600\n",kind,direction,gallery_motion_flush_pixels);
        assert(!gallery_motion_active&&!gallery_old_image.pixels&&gallery_take_buffer()==old);
        // Return the test-owned recycled buffer before interruption tests.
        gallery_recycle(old);free(camera_partial_pixels);camera_partial_pixels=NULL;
        gallery_motion_hold(slot,false);gallery_motion_begin();gallery_motion_frame(NULL,137);
        gallery_motion_finish();assert(!gallery_motion_active&&gallery_cache[slot].pixels==next);
        int other=kind*2+!portrait;uint8_t *alternate=gallery_take_buffer();assert(alternate==old);
        gallery_motion_fixture(alternate,kind,!portrait,1);
        gallery_cache[other]=gallery_cache[slot];gallery_cache[other].pixels=alternate;gallery_cache[other].portrait=!portrait;
        gallery_descriptor[other]=gallery_descriptor[slot];gallery_descriptor[other].data=alternate;
        gallery_motion_capture(preview_buffer,"rotation",kind,pose,0);
        preview_art_orientation.orientation=portrait?0:1;gallery_update(day);
        assert(gallery_motion_active&&!gallery_motion_owned&&gallery_old_image.pixels==next);
        if(getenv("TAB5_GALLERY_MOTION_CAPTURE")){
            for(int frame=0;frame<7;frame++){lv_tick_inc(30);lv_refr_now(display);gallery_motion_capture(preview_buffer,"rotation",kind,pose,frame+1);}
            assert(!gallery_motion_active&&gallery_cache[slot].pixels==next&&gallery_cache[other].pixels==alternate);
            gallery_motion_hold(other,false);gallery_motion_begin();
        }
        lv_obj_send_event(gallery_details_button,LV_EVENT_CLICKED,NULL);
        assert(gallery_details_open&&!gallery_motion_active&&gallery_cache[slot].pixels==next&&gallery_cache[other].pixels==alternate);
        gallery_reader_close();assert(!gallery_details_open&&gallery_frame[kind]==1);
    }
    gallery_motion_finish();preview_gallery_motion=false;
    for(int i=0;i<4;i++)gallery_drop(i);free(gallery_take_buffer());
    lv_obj_set_parent(gallery_view,gallery_layer);lv_obj_move_background(gallery_view);
    lv_obj_delete(gallery_reveal);lv_obj_delete(gallery_old_view);gallery_reveal=gallery_old_view=NULL;
}
static size_t gallery_render_reserve,gallery_render_budget,gallery_render_live,gallery_render_peak;
static lv_draw_buf_malloc_cb gallery_render_malloc_original;
static lv_draw_buf_free_cb gallery_render_free_original;
static struct {void *data;size_t size;} gallery_render_allocations[32];
static void *gallery_render_malloc(size_t size,lv_color_format_t format) {
    if(size>gallery_render_budget-gallery_render_live) {
        fprintf(stderr,"GALLERY_RENDER_BUDGET_FAILED requested=%zu live=%zu available=%zu retainedMainCache=%u nativeCache=%u\n",
            size,gallery_render_live,gallery_render_budget,tab5_cache_bytes,tab5_native_cache_bytes);
        exit(69); // Preserve the failed demand without hanging in LVGL's retry loop.
    }
    void *data=gallery_render_malloc_original(size,format);assert(data);
    unsigned i=0;while(i<32&&gallery_render_allocations[i].data)i++;assert(i<32);
    gallery_render_allocations[i].data=data;gallery_render_allocations[i].size=size;
    gallery_render_live+=size;if(gallery_render_live>gallery_render_peak)gallery_render_peak=gallery_render_live;
    return data;
}
static void gallery_render_free(void *data) {
    for(unsigned i=0;i<32;i++)if(gallery_render_allocations[i].data==data) {
        gallery_render_live-=gallery_render_allocations[i].size;gallery_render_allocations[i].data=NULL;break;
    }
    gallery_render_free_original(data);
}
static void check_gallery_render_budget(int category,int direction,bool details) {
    if(!getenv("TAB5_GALLERY_RENDER_BUDGET"))return;
    lv_display_t *display=lv_display_get_default();
    uint16_t *expected=malloc(sizeof(preview_buffer));assert(expected);memcpy(expected,preview_buffer,sizeof(preview_buffer));
    camera_partial_pixels=calloc(1,sizeof(preview_buffer));assert(camera_partial_pixels);
    // Replay the production 512-row path, with the observed sub-700 KiB
    // free-PSRAM floor while main-page caches remain resident. Releasing those
    // caches restores the same budget; unlimited desktop malloc is not evidence.
    size_t retained=(size_t)tab5_cache_bytes+tab5_native_cache_bytes;
    assert(gallery_render_reserve>=retained);
    gallery_render_budget=700u*1024u+gallery_render_reserve-retained;
    const char *limit=getenv("TAB5_GALLERY_RENDER_AVAILABLE_BYTES");
    if(limit){size_t available=strtoull(limit,NULL,10);assert(available>0);if(available<gallery_render_budget)gallery_render_budget=available;}
    const char *art_limit=getenv("TAB5_GALLERY_ARTWORK_RENDER_AVAILABLE_BYTES");
    if(art_limit&&!details){size_t available=strtoull(art_limit,NULL,10);assert(available>0);if(available<gallery_render_budget)gallery_render_budget=available;}
    gallery_render_live=gallery_render_peak=0;memset(gallery_render_allocations,0,sizeof(gallery_render_allocations));
    lv_draw_buf_handlers_t *handlers=lv_draw_buf_get_handlers();
    gallery_render_malloc_original=handlers->buf_malloc_cb;gallery_render_free_original=handlers->buf_free_cb;
    handlers->buf_malloc_cb=gallery_render_malloc;handlers->buf_free_cb=gallery_render_free;
    lv_display_set_flush_cb(display,camera_partial_flush);
    lv_display_set_buffers(display,preview_buffer,NULL,TAB5_DRAW_BUFFER_PIXELS*2,LV_DISPLAY_RENDER_MODE_PARTIAL);
    lv_obj_invalidate(details?gallery_reader:gallery_layer);lv_refr_now(display);
    assert(gallery_render_live==0&&!memcmp(expected,camera_partial_pixels,sizeof(preview_buffer)));
    handlers->buf_malloc_cb=gallery_render_malloc_original;handlers->buf_free_cb=gallery_render_free_original;
    lv_display_set_flush_cb(display,preview_flush);lv_display_set_buffers(display,preview_buffer,NULL,sizeof(preview_buffer),LV_DISPLAY_RENDER_MODE_FULL);
    free(expected);free(camera_partial_pixels);camera_partial_pixels=NULL;
    printf("GALLERY_RENDER_BUDGET_OK category=%d direction=%d view=%s peak=%zu available=%zu pixels_identical\n",category,direction,details?"reader":"artwork",gallery_render_peak,gallery_render_budget);
}
static void check_gallery_motion_budget(struct tm *day) {
    gallery_render_budget=4194304;gallery_render_live=gallery_render_peak=0;
    memset(gallery_render_allocations,0,sizeof(gallery_render_allocations));
    lv_draw_buf_handlers_t *handlers=lv_draw_buf_get_handlers();
    gallery_render_malloc_original=handlers->buf_malloc_cb;gallery_render_free_original=handlers->buf_free_cb;
    handlers->buf_malloc_cb=gallery_render_malloc;handlers->buf_free_cb=gallery_render_free;
    check_gallery_motion(day);assert(gallery_render_live==0);
    handlers->buf_malloc_cb=gallery_render_malloc_original;handlers->buf_free_cb=gallery_render_free_original;
    printf("GALLERY_MOTION_MEMORY_OK peak=%zu available=4194304 no_extra_output_frame\n",gallery_render_peak);
}
static void check_gallery_footer(int category) {
    const int directions[]={0,2,1,-1};
    // The production clock fixture has another date. Keep these already loaded
    // artwork fixtures offline while exiting/re-entering, so an allowed daily
    // refresh cannot evict the portrait fixture being used for the next pose.
    bool online=preview_gallery_ready;preview_gallery_ready=false;
    const char *footer_catalog=getenv("TAB5_GALLERY_FOOTER_GUIDES");
    const char *catalog_path=footer_catalog?footer_catalog:getenv("TAB5_GALLERY_GUIDES");
    if(catalog_path) {
        FILE *file=fopen(catalog_path,"rb");assert(file);fseek(file,0,SEEK_END);long size=ftell(file);rewind(file);
        char *json=calloc(1,(size_t)size+1);assert(json&&fread(json,1,(size_t)size,file)==(size_t)size);fclose(file);
        cJSON *catalog=cJSON_Parse(json);free(json);assert(catalog);cJSON *row=NULL,*selected=NULL;
        const char *id=category?"colbase:tnm:TB-6":"cma:1964.242";
        cJSON_ArrayForEach(row,cJSON_GetObjectItemCaseSensitive(catalog,"works"))
            if(footer_catalog?!strcmp(cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(row,"category")),category?"calligraphy":"painting"):
                !strcmp(cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(row,"workId")),id))selected=row;
        assert(selected);memset(&preview_guide,0,sizeof(preview_guide));strcpy(preview_guide.date,"2026-10-06");
        snprintf(preview_guide.work_id,sizeof(preview_guide.work_id),"%s",cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(selected,"workId")));
        #define FOOTER_FIXTURE_FIELD(name) snprintf(preview_guide.name,sizeof(preview_guide.name),"%s",cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(selected,#name)))
        FOOTER_FIXTURE_FIELD(title);FOOTER_FIXTURE_FIELD(author);FOOTER_FIXTURE_FIELD(period);FOOTER_FIXTURE_FIELD(museum);
        FOOTER_FIXTURE_FIELD(overview);FOOTER_FIXTURE_FIELD(looking);
        #undef FOOTER_FIXTURE_FIELD
        cJSON *original=cJSON_GetObjectItemCaseSensitive(selected,"transcription");
        if(gallery_original_reviewed(original)) {
            snprintf(preview_guide.transcription,sizeof(preview_guide.transcription),"%s",text(original,"text"));
            snprintf(preview_guide.explanation,sizeof(preview_guide.explanation),"%s",text(original,"explanation"));
        }
        cJSON_Delete(catalog);
        for(int i=category*2;i<category*2+2;i++)strcpy(gallery_cache[i].work_id,preview_guide.work_id);
    }
    lv_indev_t *touch=lv_indev_create();lv_indev_set_type(touch,LV_INDEV_TYPE_POINTER);
    lv_indev_set_display(touch,lv_display_get_default());lv_indev_set_read_cb(touch,camera_touch_read);
    for(int pose=0;pose<4;pose++) {
        bool portrait=pose>=2;int width=portrait?720:1280,height=portrait?1280:720;
        gallery_orientation=directions[pose];gallery_last_portrait=portrait?directions[pose]:gallery_last_portrait;
        gallery_show();lv_obj_update_layout(gallery_controls);
        // JPEG portrait fixtures include the production counter-clockwise
        // pre-rotation. The overlay must match that and the image's 180 flip.
        int angle=(lv_image_get_rotation(gallery_view)+(portrait?2700:0))%3600;
        assert(lv_obj_get_style_transform_rotation(gallery_controls,0)==angle);
        assert(!lv_obj_has_flag(gallery_controls,LV_OBJ_FLAG_HIDDEN));
        assert(lv_obj_get_height(gallery_controls)<150); // Never rotate a full canvas for a footer.
        assert(lv_obj_has_flag(gallery_footer_title,LV_OBJ_FLAG_HIDDEN)==!portrait);
        char brief[512];snprintf(brief,sizeof(brief),"%s · %s",preview_guide.title,preview_guide.author);
        lv_obj_t *captions[]={gallery_footer_title,gallery_footer_museum};
        const char *complete[]={brief,preview_guide.museum};
        for(int i=0;i<2;i++) {
            // DOT may change the rendered buffer. Its complete text must
            // survive clipping and rotation, including long source periods.
            lv_label_set_long_mode(captions[i],LV_LABEL_LONG_CLIP);
            assert(!strcmp(lv_label_get_text(captions[i]),complete[i]));
            lv_label_set_long_mode(captions[i],LV_LABEL_LONG_DOT);
        }
        assert(!strcmp(gallery_reading->period,preview_guide.period));
        assert(lv_obj_get_y(gallery_footer_museum)+lv_obj_get_height(gallery_footer_museum)<=lv_obj_get_height(gallery_controls));
        lv_refr_now(lv_display_get_default());
        for(int stable=0;stable<40;stable++) {
            gallery_show();lv_obj_update_layout(gallery_controls);
            assert(!lv_display_get_default()->inv_p); // unchanged clipped captions never repaint
        }
        if(portrait) {
            assert(lv_obj_get_y(gallery_footer_title)+lv_obj_get_height(gallery_footer_title)<=lv_obj_get_y(gallery_details_button));
        } else {
            // No second title/author/period strip over the accepted image panel.
            assert(lv_obj_has_flag(gallery_footer_title,LV_OBJ_FLAG_HIDDEN));
        }
        int museum_x=lv_obj_get_x(gallery_footer_museum),museum_y=lv_obj_get_y(gallery_footer_museum);
        int museum_w=lv_obj_get_width(gallery_footer_museum),museum_h=lv_obj_get_height(gallery_footer_museum);
        int page_x=lv_obj_get_x(gallery_page_count),page_y=lv_obj_get_y(gallery_page_count);
        int page_w=lv_obj_get_width(gallery_page_count),page_h=lv_obj_get_height(gallery_page_count);
        assert(museum_x+museum_w<=page_x||page_x+page_w<=museum_x||museum_y+museum_h<=page_y||page_y+page_h<=museum_y);
        assert(museum_x+museum_w<=(portrait?width:lv_obj_get_x(gallery_details_button)));
        char name[100];snprintf(name,sizeof(name),"gallery-footer-%s-%d",category?"calligraphy":"painting",directions[pose]);capture(name);
        check_gallery_render_budget(category,directions[pose],false);
        // Use the real input dispatcher at the visually transformed button,
        // rather than directly dispatching an event to a possibly upside-down control.
        gallery_pointer_click(touch,gallery_physical_point(width-110,height-42,angle));
        assert(saver&&gallery_details_open);
        assert(lv_obj_get_style_transform_rotation(gallery_reader,0)==angle);
        for(int chapter=0;chapter<(category?3:2);chapter++) {
            unsigned before=preview_gallery_requests;int frame=gallery_frame[category];
            gallery_pointer_click(touch,gallery_physical_point(120+160*chapter,lv_obj_get_y(gallery_chapters[chapter])+28,angle));
            assert(gallery_details_open&&gallery_reading_chapter==chapter&&preview_gallery_requests==before&&gallery_frame[category]==frame);
            for(int i=0;i<(category?3:2);i++) {
                lv_obj_t *tab=gallery_chapters[i];
                assert(lv_obj_get_style_bg_opa(tab,0)==LV_OPA_COVER);
                assert(lv_color_to_u32(lv_obj_get_style_bg_color(tab,0))!=lv_color_to_u32(lv_obj_get_style_bg_color(gallery_reader,0)));
                if(i!=chapter)assert(lv_color_to_u32(lv_obj_get_style_bg_color(tab,0))!=lv_color_to_u32(lv_obj_get_style_bg_color(gallery_chapters[chapter],0)));
                uint32_t normal=lv_color_to_u32(lv_obj_get_style_bg_color(tab,0));
                lv_obj_add_state(tab,LV_STATE_PRESSED);
                assert(lv_color_to_u32(lv_obj_get_style_bg_color(tab,0))!=normal);
                lv_obj_remove_state(tab,LV_STATE_PRESSED);
            }
            snprintf(name,sizeof(name),"gallery-tabs-%s-%d-chapter%d",category?"calligraphy":"painting",directions[pose],chapter);capture(name);
        }
        if(category) {
            unsigned requests=preview_gallery_requests;int frame=gallery_frame[category];
            gallery_pointer_click(touch,gallery_physical_point(440,lv_obj_get_y(gallery_chapters[2])+28,angle));
            assert(saver&&gallery_details_open&&gallery_reading_chapter==2);
            assert(preview_gallery_requests==requests&&gallery_frame[category]==frame);
            assert(!lv_obj_has_flag(gallery_chapters[2],LV_OBJ_FLAG_HIDDEN));
            if(!gallery_reading->transcription[0])assert(!strcmp(lv_label_get_text(gallery_reader_body),"这幅作品的原文尚未核实。"));
            snprintf(name,sizeof(name),"gallery-original-calligraphy-%d",directions[pose]);capture(name);
        }
        snprintf(name,sizeof(name),"gallery-details-%s-%d",category?"calligraphy":"painting",directions[pose]);capture(name);
        check_gallery_render_budget(category,directions[pose],true);
        gallery_pointer_click(touch,gallery_physical_point(width-138,height-50,angle));
        assert(saver&&!gallery_details_open);
        int selected=prefs.selected,brightness=saver_restore_brightness;bool cycle=prefs.cycle;
        gallery_pointer_click(touch,gallery_physical_point(width/2,height/2,angle));
        assert(!saver&&!saver_preview&&lv_obj_has_flag(saver_layer,LV_OBJ_FLAG_HIDDEN));
        assert(preview_brightness==brightness&&prefs.selected==selected&&prefs.cycle==cycle);
        saver_enter(false); // Continue with the next orientation after actual touch exit.
    }
    lv_anim_delete(touch,NULL);lv_indev_reset(touch,NULL);lv_indev_delete(touch);
    preview_gallery_ready=online;
    gallery_orientation=0;gallery_manual_until=0;gallery_show();
    printf("GALLERY_FOOTER_OK category=%d four bitmap-aligned directions, opaque themed tabs with selected and pressed states; transformed chapter/Details/Return touch; unavailable original has no reading suggestion; exact frame and display policy preserved\n",category);
}
#endif
static bool press_text(lv_obj_t *matrix,const char *text) {
    for(uint32_t i=0;i<100;i++) {
        const char *label=lv_buttonmatrix_get_button_text(matrix,i);
        if(!label)return false;
        if(!strcmp(label,text)) {
            lv_buttonmatrix_set_selected_button(matrix,i);
            lv_obj_send_event(matrix,LV_EVENT_VALUE_CHANGED,NULL);return true;
        }
    }
    return false;
}
static void type_spelling(const char *text) {
    for(;*text;text++){char key[2]={*text,0};assert(press_text(codex_keyboard,key));}
}
static void choose_word(const char *word) {
    lv_obj_t *choices=lv_obj_get_child(codex_ime_panel,2);
    for(int page_no=0;page_no<12;page_no++) {
        if(press_text(choices,word))return;
        assert(!lv_buttonmatrix_has_button_ctrl(choices,5,LV_BUTTONMATRIX_CTRL_DISABLED));
        assert(press_text(choices,">"));
    }
    assert(!"Pinyin candidate missing");
}
static void check_network_segments(const uint16_t *pixels) {
    lv_area_t bounds;lv_obj_get_coords(chart,&bounds);
    lv_chart_series_t *series[]={upload,download};
    uint16_t colors[]={lv_color_to_u16(lv_color_hex(NET_UP)),lv_color_to_u16(lv_color_hex(NET_DOWN))};
    unsigned probes=0;
    for(int s=0;s<2;s++)for(int i=1;i<NETWORK_POINTS;i++) {
        int32_t *v=lv_chart_get_y_array(chart,series[s]);
        if(v[i-1]==LV_CHART_POINT_NONE||v[i]==LV_CHART_POINT_NONE)continue;
        lv_point_t a,b;lv_chart_get_point_pos_by_id(chart,series[s],i-1,&a);
        lv_chart_get_point_pos_by_id(chart,series[s],i,&b);
        // Probe inside rising AND falling segments, away from their end caps.
        for(int q=1;q<4;q++) {
            int x=bounds.x1+a.x+(b.x-a.x)*q/4,y=bounds.y1+a.y+(b.y-a.y)*q/4;
            bool found=false;
            for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++) {
                uint16_t pixel=pixels[(y+dy)*1280+x+dx];
                if(pixel==colors[0]||pixel==colors[1])found=true;
            }
            assert(found);probes++;
        }
    }
    assert(probes>30);
}
static void check_system_chart_sampling(void) {
    cJSON *m=cJSON_Parse("{\"sampleSession\":\"chart-046\",\"sampleSequence\":3,\"samples\":[{\"upload\":0},{\"upload\":409600},{\"upload\":0}]}");
    render_chart(m);
    assert(network_raw[222].up==409600); // Preserve measured quarter-second burst.
    assert(lv_chart_get_y_array(chart,upload)[222]==409600/2);
    uint64_t before=metric_seq;render_chart(m);assert(metric_seq==before&&!network_queued);
    // Rendering a duplicate state must not overwrite current numbers with a
    // differently timed header. Both labels belong to the displayed endpoint.
    cJSON *saved_shown=shown,*fixture=cJSON_CreateObject();
    cJSON *metrics=cJSON_Duplicate(m,true);
    cJSON_AddNumberToObject(metrics,"uploadBytesPerSecond",999999);
    cJSON_AddNumberToObject(metrics,"downloadBytesPerSecond",888888);
    cJSON_AddItemToObject(fixture,"systemMetrics",metrics);shown=fixture;
    render_system();char duplicate_rate[32];rate(network_mean(223).up,duplicate_rate,sizeof(duplicate_rate));
    assert(!strcmp(lv_label_get_text(network_rates[0]),duplicate_rate));
    rate(network_mean(223).down,duplicate_rate,sizeof(duplicate_rate));
    assert(!strcmp(lv_label_get_text(network_rates[1]),duplicate_rate));
    shown=saved_shown;cJSON_Delete(fixture);
    cJSON_ReplaceItemInObject(m,"sampleSequence",cJSON_CreateNumber(6));
    render_chart(m);
    assert(!network_queued&&network_raw[222].up==409600&&network_raw[223].up==0);
    char expected_rate[32];rate(network_mean(223).up,expected_rate,sizeof(expected_rate));
    assert(!strcmp(lv_label_get_text(network_rates[0]),expected_rate));
    cJSON_ReplaceItemInObject(m,"sampleSequence",cJSON_CreateNumber(12));render_chart(m);
    assert(network_raw[218].up==LV_CHART_POINT_NONE&&network_raw[220].up==LV_CHART_POINT_NONE);
    assert(lv_chart_get_y_array(chart,upload)[220]==LV_CHART_POINT_NONE);
    cJSON_ReplaceItemInObject(m,"sampleSession",cJSON_CreateString("chart-restart"));
    cJSON_ReplaceItemInObject(m,"sampleSequence",cJSON_CreateNumber(3));render_chart(m);
    assert(network_raw[220].up==LV_CHART_POINT_NONE&&!network_queued);
    cJSON_Delete(m);
    m=cJSON_Parse("{\"sampleSession\":\"batch-046\",\"sampleSequence\":64,\"samples\":[]}");
    for(int i=0;i<64;i++)cJSON_AddItemToArray(item(m,"samples"),cJSON_Parse("{\"upload\":102400,\"download\":307200}"));
    render_chart(m);cJSON_ReplaceItemInObject(m,"sampleSequence",cJSON_CreateNumber(128));render_chart(m);
    for(int i=96;i<224;i++)assert(network_raw[i].up==102400&&lv_chart_get_y_array(chart,download)[i]==307200);
    for(int i=0;i<NETWORK_POINTS;i++)network_raw[i]=(network_point){1000000000,500000000};
    network_plot();
    assert(network_raw[0].up==1000000000&&lv_chart_get_y_array(chart,upload)[0]<1000000);
    lv_point_t high;lv_chart_get_point_pos_by_id(chart,upload,100,&high);
    assert(high.y>=0&&high.y<250); // High throughput cannot overflow LVGL's pixel multiply.
    cJSON_Delete(m);
    // A delayed batch must move numbers and graph to the same latest sample.
    network_head=0;network_queued=20;
    for(int i=0;i<20;i++)network_queue[i]=(network_point){102400,307200};
    network_flush();assert(!network_queued&&network_mean(223).up==102400);
    rate(102400,expected_rate,sizeof(expected_rate));assert(!strcmp(lv_label_get_text(network_rates[0]),expected_rate));
    rate(307200,expected_rate,sizeof(expected_rate));assert(!strcmp(lv_label_get_text(network_rates[1]),expected_rate));
    // No arriving data preserves the last measurement rather than inserting zero.
    network_flush();assert(network_mean(223).up==102400);
    for(int i=0;i<4;i++)network_enqueue((network_point){0,0});
    network_flush();assert(!network_queued&&network_mean(223).up==0);
    rate(0,expected_rate,sizeof(expected_rate));assert(!strcmp(lv_label_get_text(network_rates[0]),expected_rate));
    // A first measured point is not averaged with unknown startup history.
    for(int i=0;i<NETWORK_POINTS;i++)network_raw[i]=(network_point){LV_CHART_POINT_NONE,LV_CHART_POINT_NONE};
    network_enqueue((network_point){12345,54321});network_flush();
    assert(network_mean(223).up==12345&&network_mean(223).down==54321);
    // The graph's declared one-second mean conserves a burst's contribution.
    for(int i=0;i<NETWORK_POINTS;i++)network_raw[i]=(network_point){0,0};
    network_raw[100].up=400000;
    for(int i=100;i<104;i++)assert(network_mean(i).up==100000);
    assert(network_mean(104).up==0);
    network_raw[101]=(network_point){LV_CHART_POINT_NONE,LV_CHART_POINT_NONE};
    assert(network_mean(101).up==LV_CHART_POINT_NONE&&network_mean(102).up==0);
    puts("SYSTEM_CHART_OK quarter-second raw samples; one-second mean; aligned current numbers; immediate delayed batches; burst; stop; startup; gaps; duplicate suppression; reconnect");
}
static unsigned network_image_tasks,network_line_tasks;
static void network_draw_audit(lv_event_t *event) {
    lv_draw_task_t *task=lv_event_get_draw_task(event);
    if(lv_draw_task_get_image_dsc(task))network_image_tasks++;
    if(lv_draw_task_get_line_dsc(task))network_line_tasks++;
}
static void check_network_partial(void) {
    lv_display_t *d=lv_display_get_default();
    unsigned builds=tab5_cache_builds;for(int i=0;i<20;i++)cache_tick(NULL);assert(tab5_cache_builds==builds);
    uint16_t *expected=malloc(sizeof(preview_buffer));assert(expected);
    camera_partial_pixels=calloc(1,sizeof(preview_buffer));assert(camera_partial_pixels);
    for(int offset=0;offset<4;offset++) {
        network_scroll=offset;
        lv_display_set_flush_cb(d,preview_flush);
        lv_display_set_buffers(d,preview_buffer,NULL,sizeof(preview_buffer),LV_DISPLAY_RENDER_MODE_FULL);
        lv_obj_invalidate(lv_screen_active());lv_refr_now(d);
        memcpy(expected,preview_buffer,sizeof(preview_buffer));
        lv_display_set_flush_cb(d,camera_partial_flush);
        lv_display_set_buffers(d,preview_buffer,NULL,TAB5_DRAW_BUFFER_PIXELS*2,LV_DISPLAY_RENDER_MODE_PARTIAL);
        network_image_tasks=network_line_tasks=0;
        lv_obj_add_flag(chart,LV_OBJ_FLAG_SEND_DRAW_TASK_EVENTS);
        lv_obj_add_event_cb(chart,network_draw_audit,LV_EVENT_DRAW_TASK_ADDED,NULL);
        lv_obj_invalidate(lv_screen_active());lv_refr_now(d);
        lv_obj_remove_event_cb(chart,network_draw_audit);
        lv_obj_remove_flag(chart,LV_OBJ_FLAG_SEND_DRAW_TASK_EVENTS);
        assert(network_image_tasks>0&&network_image_tasks<=5&&network_line_tasks==0);
        assert(!memcmp(expected,camera_partial_pixels,sizeof(preview_buffer)));
        if(!offset)check_network_segments(camera_partial_pixels);
        // Repaint just the graph on an already visible frame. A stale source
        // cache or wrong offset clip must not leave strips from the old image.
        network_scroll=(offset+1)%4;lv_obj_invalidate(chart);lv_refr_now(d);
        network_scroll=offset;lv_obj_invalidate(chart);lv_refr_now(d);
        assert(!memcmp(expected,camera_partial_pixels,sizeof(preview_buffer)));
    }
    free(expected);free(camera_partial_pixels);camera_partial_pixels=NULL;network_scroll=0;
    lv_display_set_flush_cb(d,preview_flush);
    lv_display_set_buffers(d,preview_buffer,NULL,sizeof(preview_buffer),LV_DISPLAY_RENDER_MODE_FULL);
    lv_obj_invalidate(lv_screen_active());lv_refr_now(d);
    puts("NETWORK_RASTER_OK full/partial pixel equality at offsets 0..3; at most 5 image tasks; zero line tasks; rising/falling continuity");
}
static void capture(const char *name) {
    fprintf(stderr,"PREVIEW %s\n",name);
    lv_obj_update_layout(lv_screen_active());
    lv_obj_invalidate(lv_screen_active()); lv_refr_now(NULL);
    if(!strcmp(name,"computer-unlock-cover-dismissed")) {
        // Inspect the display buffer independently of lv_snapshot's offscreen
        // render, so repeated modal snapshots cannot hide a live draw problem.
        FILE *live=fopen("computer-unlock-cover-dismissed-display.ppm","wb");assert(live);
        fprintf(live,"P6\n1280 720\n255\n");
        for(unsigned i=0;i<1280*720;i++) {
            uint16_t pixel=preview_buffer[i];
            uint8_t rgb[]={(uint8_t)(((pixel>>11)&31)*255/31),(uint8_t)(((pixel>>5)&63)*255/63),(uint8_t)((pixel&31)*255/31)};
            fwrite(rgb,1,3,live);
        }
        fclose(live);
    }
    if(!strcmp(name,"page-4")) {
        for(int i=0;i<2;i++) {
            lv_area_t arc,number;lv_obj_get_coords(system_meter[i].arc,&arc);lv_obj_get_coords(system_meter[i].number,&number);
            assert(lv_area_get_width(&arc)==lv_area_get_height(&arc));
            assert(abs((arc.x1+arc.x2)-(number.x1+number.x2))<=1);
            assert(abs((arc.y1+arc.y2)-(number.y1+number.y2))<=1);
            assert(!strchr(lv_label_get_text(system_meter[i].number),'.'));
        }
        check_network_segments(preview_buffer);check_network_partial();
        FILE *live=fopen("network-display.ppm","wb");assert(live);
        fprintf(live,"P6\n1280 720\n255\n");
        for(unsigned i=0;i<1280*720;i++) {
            uint16_t pixel=preview_buffer[i];
            uint8_t rgb[]={(uint8_t)(((pixel>>11)&31)*255/31),(uint8_t)(((pixel>>5)&63)*255/63),(uint8_t)((pixel&31)*255/31)};
            fwrite(rgb,1,3,live);
        }
        fclose(live);
    }
    lv_draw_buf_t *snap=lv_snapshot_take(lv_screen_active(),LV_COLOR_FORMAT_RGB565);
    assert(snap); char path[160]; snprintf(path,sizeof(path),"%s.ppm",name);
    FILE *f=fopen(path,"wb"); assert(f); fprintf(f,"P6\n%d %d\n255\n",snap->header.w,snap->header.h);
    for(unsigned y=0;y<snap->header.h;y++) for(unsigned x=0;x<snap->header.w;x++) {
        uint16_t pixel=*(uint16_t *)(snap->data+y*snap->header.stride+x*2);
        uint8_t rgb[]={(uint8_t)(((pixel>>11)&31)*255/31),(uint8_t)(((pixel>>5)&63)*255/63),(uint8_t)((pixel&31)*255/31)}; fwrite(rgb,1,3,f);
    }
    fclose(f); lv_draw_buf_destroy(snap);
}
static unsigned cache_compare_frame(bool partial,unsigned *uncached_tasks,unsigned *cached_tasks) {
    extern bool (*tab5_lvgl_cache_ready)(const lv_obj_t *);
    extern bool (*tab5_lvgl_cache_draw)(lv_layer_t *,lv_obj_t *);
    bool (*ready)(const lv_obj_t *)=tab5_lvgl_cache_ready;
    bool (*draw)(lv_layer_t *,lv_obj_t *)=tab5_lvgl_cache_draw;
    uint16_t *expected=malloc(sizeof(preview_buffer));assert(expected);
    lv_display_t *display=lv_display_get_default();
    if(partial) {
        camera_partial_pixels=calloc(1,sizeof(preview_buffer));assert(camera_partial_pixels);
        lv_display_set_flush_cb(display,camera_partial_flush);
        lv_display_set_buffers(display,preview_buffer,NULL,TAB5_DRAW_BUFFER_PIXELS*2,LV_DISPLAY_RENDER_MODE_PARTIAL);
    }
    tab5_lvgl_cache_ready=NULL;tab5_lvgl_cache_draw=NULL;
    lv_obj_remove_event_cb(lv_tabview_get_content(tabs),page_strip_cover);
    camera_draw_tasks=0;camera_audit_tree(lv_screen_active(),true);
    lv_obj_invalidate(lv_screen_active());lv_refr_now(display);
    camera_audit_tree(lv_screen_active(),false);*uncached_tasks+=camera_draw_tasks;
    memcpy(expected,partial?camera_partial_pixels:preview_buffer,sizeof(preview_buffer));
    tab5_lvgl_cache_ready=ready;tab5_lvgl_cache_draw=draw;
    lv_obj_add_event_cb(lv_tabview_get_content(tabs),page_strip_cover,LV_EVENT_COVER_CHECK,NULL);
    unsigned before=tab5_cache_hits;
    audit_fill_pixels=audit_image_pixels=audit_argb_pixels=0;
    camera_draw_tasks=0;camera_audit_tree(lv_screen_active(),true);
    lv_obj_invalidate(lv_screen_active());lv_refr_now(display);
    camera_audit_tree(lv_screen_active(),false);*cached_tasks+=camera_draw_tasks;
    if(getenv("TAB5_SCROLL_DRAW_AUDIT"))printf("SCROLL_DRAW page=%d partial=%d fill=%u image=%u argb=%u tasks=%u\n",current_page,partial,audit_fill_pixels,audit_image_pixels,audit_argb_pixels,camera_draw_tasks);
    assert(!memcmp(expected,partial?camera_partial_pixels:preview_buffer,sizeof(preview_buffer)));
    if(partial) {
        lv_display_set_buffers(display,preview_buffer,NULL,sizeof(preview_buffer),LV_DISPLAY_RENDER_MODE_FULL);
        lv_display_set_flush_cb(display,preview_flush);free(camera_partial_pixels);camera_partial_pixels=NULL;
    }
    free(expected);return tab5_cache_hits-before;
}
static void check_scroll_damage(void) {
    lv_display_t *display=lv_display_get_default();
    camera_partial_pixels=calloc(1,sizeof(preview_buffer));assert(camera_partial_pixels);
    lv_display_set_flush_cb(display,camera_partial_flush);
    lv_display_set_buffers(display,preview_buffer,NULL,TAB5_DRAW_BUFFER_PIXELS*2,LV_DISPLAY_RENDER_MODE_PARTIAL);
    lv_obj_invalidate(lv_screen_active());lv_refr_now(display);
    lv_obj_scroll_to_y(settings_scroll,80,LV_ANIM_OFF);
    assert(tab5_draw_cache_is_ready(settings_content));
    audit_fill_pixels=audit_image_pixels=audit_argb_pixels=0;
    camera_audit_tree(lv_screen_active(),true);lv_refr_now(display);camera_audit_tree(lv_screen_active(),false);
    printf("SCROLL_DAMAGE_OK fill=%u cached_image=%u argb=%u\n",audit_fill_pixels,audit_image_pixels,audit_argb_pixels);
    // Only the scrollbar may need filling: the opaque cached body covers the
    // actual damage. A whole-window screenshot alone cannot prove this.
    assert(audit_fill_pixels<10000&&audit_image_pixels>200000&&audit_argb_pixels==0);
    uint16_t *actual=camera_partial_pixels;camera_partial_pixels=NULL;
    lv_display_set_buffers(display,preview_buffer,NULL,sizeof(preview_buffer),LV_DISPLAY_RENDER_MODE_FULL);
    lv_display_set_flush_cb(display,preview_flush);
    extern bool (*tab5_lvgl_cache_draw)(lv_layer_t *,lv_obj_t *);
    bool (*draw)(lv_layer_t *,lv_obj_t *)=tab5_lvgl_cache_draw;tab5_lvgl_cache_draw=NULL;
    lv_obj_invalidate(lv_screen_active());lv_refr_now(display);tab5_lvgl_cache_draw=draw;
    assert(!memcmp(actual,preview_buffer,sizeof(preview_buffer)));free(actual);
    lv_obj_scroll_to_y(settings_scroll,0,LV_ANIM_OFF);
}
static void check_live_metrics(void) {
    cJSON *original=shown,*snapshot=device_snapshots[0],*metrics=device_metrics[0];
    link_state links=device_links;link_kind old_link=last_link,old_metrics_link=metrics_link;
    int64_t received=last_received;uint64_t old_seen=metrics_seen,old_sequence=device_metrics_sequence[0];
    shown=cJSON_Duplicate(original,true);device_snapshots[0]=cJSON_Duplicate(original,true);
    link_state_init(&device_links);link_state_receive(&device_links,LINK_USB,"telemetry-test",1,preview_now_us/1000);
    last_link=LINK_USB;last_received=device_links.received_ms[0];
    device_metrics[0]=cJSON_Parse("{\"sampleSession\":\"telemetry-test\",\"sampleSequence\":1,\"cpuPercent\":9,\"memoryPercent\":55,\"uploadBytesPerSecond\":100,\"downloadBytesPerSecond\":200,\"samples\":[{\"upload\":100,\"download\":200}]}");
    device_metrics_sequence[0]=1;metrics_seen=0;
    page(4);tick(NULL);assert(num(item(shown,"systemMetrics"),"cpuPercent")==9&&metric_seq==1);
    assert(last_received==device_links.received_ms[0]);
    page(0);lv_obj_t *scroller=lv_tabview_get_content(tabs);lv_tabview_set_active(tabs,1,LV_ANIM_ON);
    cJSON *before=shown;
    cJSON_ReplaceItemInObject(device_metrics[0],"sampleSequence",cJSON_CreateNumber(2));
    cJSON_ReplaceItemInObject(device_metrics[0],"cpuPercent",cJSON_CreateNumber(99));device_metrics_sequence[0]=2;
    preview_now_us+=250000;link_state_receive(&device_links,LINK_USB,"telemetry-test",2,preview_now_us/1000);
    tick(NULL);assert(shown==before&&num(item(shown,"systemMetrics"),"cpuPercent")==9);
    lv_anim_delete(scroller,NULL);page(4);tick(NULL);
    assert(num(item(shown,"systemMetrics"),"cpuPercent")==99&&metric_seq==2);
    // Repeated full snapshots cannot make newer samples/chart move backward.
    preview_now_us+=250000;link_state_receive(&device_links,LINK_USB,"telemetry-test",3,preview_now_us/1000);tick(NULL);
    assert(num(item(shown,"systemMetrics"),"cpuPercent")==99&&metric_seq==2);
    cJSON_Delete(shown);cJSON_Delete(device_snapshots[0]);cJSON_Delete(device_metrics[0]);
    shown=original;device_snapshots[0]=snapshot;device_metrics[0]=metrics;device_links=links;
    last_link=old_link;last_received=received;metrics_link=old_metrics_link;metrics_seen=old_seen;device_metrics_sequence[0]=old_sequence;
    metrics_pending=false;page(0);render();
    puts("METRICS_UI_OK latest sample, no full snapshot allocation during swipe, deferred apply, no rollback from older full state");
}
static int hold_test_page(const lv_obj_t *obj){return obj==pages[0]?2:0;}
static int hold_test_strip(const lv_obj_t *obj){return lv_obj_get_parent(obj)==tab5_draw_cache_native_strip?2:0;}
static int hold_test_all(const lv_obj_t *obj){(void)obj;return 1;}
static void check_swipe_prewarm_fairness(void) {
    page(0);lv_anim_delete(lv_tabview_get_content(tabs),NULL);
    lv_obj_update_layout(lv_screen_active());tab5_draw_cache_drop();
    lv_display_trigger_activity(NULL);lv_tick_inc(1000);
    for(int p=0;p<2;p++)for(int i=0;i<12;i++)tab5_draw_cache_step(pages[primary_pages[p]]);
    assert(tab5_draw_cache_is_ready(pages[0])&&tab5_draw_cache_is_ready(pages[8]));
    lv_color_t original=lv_obj_get_style_bg_color(pages[0],0);
    // A busy visible page must not prevent the two colder destinations from
    // acquiring their first complete image after camera/OTA dropped the cache.
    for(int i=0;i<80;i++) {
        if(i%3==0)lv_obj_set_style_bg_color(pages[0],lv_color_hex(i%2?0x213244:0x324355),0);
        cache_tick(NULL);lv_tick_inc(30);
    }
    if(!tab5_draw_cache_is_ready(pages[1])||!tab5_draw_cache_is_ready(pages[11]))
        printf("CACHE_DIAG page=%d saver=%d quick=%d ota=%d voice=%d photo=%d camera_hidden=%d animations=%u idle=%u detail_hidden=%d\n",current_page,saver,quick_visible,ota_update_busy(),codex_voice_waiting,photo_flow,lv_obj_has_flag(camera_layer,LV_OBJ_FLAG_HIDDEN),(unsigned)lv_anim_count_running(),(unsigned)lv_display_get_inactive_time(NULL),lv_obj_has_flag(detail_layer,LV_OBJ_FLAG_HIDDEN));
    assert(tab5_draw_cache_is_ready(pages[1])&&tab5_draw_cache_is_ready(pages[11]));
    lv_obj_set_style_bg_color(pages[0],original,0);
    for(int i=0;i<40;i++){cache_tick(NULL);lv_tick_inc(30);}
    for(int p=0;p<4;p++)assert(tab5_draw_cache_is_ready(pages[primary_pages[p]]));
    unsigned raw=0,cached=0;assert(cache_compare_frame(true,&raw,&cached));
    tab5_draw_cache_drop();
    puts("SWIPE_PREWARM_FAIR_OK continuous visible-page updates do not starve cold destinations; latest pixels repaired");
}
static void check_native_swipe(void) {
    page(0);lv_obj_update_layout(lv_screen_active());tab5_draw_cache_drop();
    for(int p=0;p<4;p++)for(int i=0;i<12;i++)tab5_draw_cache_step(pages[primary_pages[p]]);
    assert(tab5_native_cache_bytes==tab5_cache_bytes);
    lv_display_t *d=lv_display_get_default();lv_obj_t *strip=lv_tabview_get_content(tabs);
    int (*hold)(const lv_obj_t *)=tab5_draw_cache_interaction;tab5_draw_cache_interaction=hold_test_strip;
    uint16_t *expected=malloc(sizeof(preview_buffer)),*second=malloc(1280u*512u*2u);assert(expected&&second);
    camera_partial_pixels=malloc(sizeof(preview_buffer));assert(camera_partial_pixels);
    int positions[]={0,1,83,610,1219,1501,2440,3000,3660,-40};
    const int strip_rows[]={24,64,128,256,512};
    lv_color_t original=lv_obj_get_style_bg_color(pages[0],0);lv_obj_t *overlay=NULL;
    for(int phase=0;phase<5;phase++) {
    lv_obj_scroll_to_x(strip,0,LV_ANIM_OFF);
    if(phase==1) {
        tab5_draw_cache_interaction=hold;lv_obj_set_style_bg_color(pages[0],lv_color_hex(0x113355),0);
        tab5_draw_cache_step_budget(pages[0],1280u*16u);tab5_draw_cache_interaction=hold_test_strip;
    }
    if(phase==2) {
        tab5_draw_cache_interaction=hold;for(int i=0;i<12;i++)tab5_draw_cache_step(pages[0]);
        tab5_draw_cache_interaction=hold_test_strip;
    }
    if(phase==3){overlay=shape(lv_screen_active(),30,92,1220,516,0x123456,0);lv_obj_update_layout(overlay);}
    if(phase==4) {
        lv_obj_delete(overlay);overlay=NULL;tab5_draw_cache_drop();tab5_draw_cache_native_strip=NULL;
        tab5_draw_cache_interaction=hold;
        for(int p=0;p<4;p++)for(int i=0;i<12;i++)tab5_draw_cache_step(pages[primary_pages[p]]);
        tab5_draw_cache_native_strip=strip;tab5_draw_cache_interaction=hold_test_strip;
    }
    for(int buffers=1;buffers<=2;buffers++)for(unsigned r=0;r<sizeof(strip_rows)/sizeof(strip_rows[0]);r++)for(unsigned p=0;p<sizeof(positions)/sizeof(positions[0]);p++) {
        int rows=strip_rows[r];
        lv_obj_scroll_by_raw(strip,lv_obj_get_scroll_x(strip)-positions[p],0);
        lv_display_set_flush_cb(d,preview_flush);
        lv_display_set_buffers(d,preview_buffer,NULL,sizeof(preview_buffer),LV_DISPLAY_RENDER_MODE_FULL);
        lv_obj_invalidate(lv_screen_active());lv_refr_now(d);memcpy(expected,preview_buffer,sizeof(preview_buffer));
        memcpy(camera_partial_pixels,expected,sizeof(preview_buffer));
        // Model the physical portrait panel while retaining 1280x720 logical coordinates.
        d->hor_res=720;d->ver_res=1280;d->rotation=LV_DISPLAY_ROTATION_90;
        lv_display_set_flush_cb(d,camera_partial_flush);
        lv_display_set_buffers(d,preview_buffer,buffers==2?second:NULL,1280u*rows*2u,LV_DISPLAY_RENDER_MODE_PARTIAL);
        unsigned hits=tab5_native_cache_hits;
        lv_obj_invalidate(strip);lv_refr_now(d);
        if(positions[p]>=0&&phase<3)assert(tab5_native_cache_hits>hits);else assert(tab5_native_cache_hits==hits);
        assert(!memcmp(expected,camera_partial_pixels,sizeof(preview_buffer)));
        d->hor_res=1280;d->ver_res=720;d->rotation=LV_DISPLAY_ROTATION_0;
    }
    }
    lv_display_set_flush_cb(d,preview_flush);
    lv_display_set_buffers(d,preview_buffer,NULL,sizeof(preview_buffer),LV_DISPLAY_RENDER_MODE_FULL);
    tab5_draw_cache_interaction=hold;lv_obj_scroll_to_x(strip,0,LV_ANIM_OFF);lv_obj_set_style_bg_color(pages[0],original,0);
    free(expected);free(second);free(camera_partial_pixels);camera_partial_pixels=NULL;tab5_draw_cache_drop();
    puts("NATIVE_SWIPE_OK 500 partial frames; four pages; odd offsets; 24/64/128/256/512 rows; single/double buffers; interrupted/publication/overlay/missing-cache/rebound; pixels identical");
}
static void check_native_settle_frame(void) {
    page(0);lv_anim_delete(lv_tabview_get_content(tabs),NULL);
    lv_obj_update_layout(lv_screen_active());tab5_draw_cache_drop();
    for(int p=0;p<4;p++)for(int i=0;i<12;i++)tab5_draw_cache_step(pages[primary_pages[p]]);
    assert(!gesture_active());
    lv_display_t *d=lv_display_get_default();lv_obj_t *strip=lv_tabview_get_content(tabs);
    uint16_t *expected=malloc(sizeof(preview_buffer)),*second=malloc(1280u*512u*2u);
    camera_partial_pixels=malloc(sizeof(preview_buffer));assert(expected&&second&&camera_partial_pixels);
    lv_color_t original=lv_obj_get_style_bg_color(pages[0],0);
    for(int phase=0;phase<3;phase++) {
        if(phase==1)lv_obj_set_style_bg_color(pages[0],lv_color_hex(0x123456),0);
        if(phase==2)for(int i=0;i<12;i++)tab5_draw_cache_step(pages[0]);
        for(int rows=256;rows<=512;rows*=2) {
            lv_display_set_flush_cb(d,preview_flush);
            lv_display_set_buffers(d,preview_buffer,NULL,sizeof(preview_buffer),LV_DISPLAY_RENDER_MODE_FULL);
            lv_obj_invalidate(lv_screen_active());lv_refr_now(d);
            memcpy(expected,preview_buffer,sizeof(preview_buffer));
            memcpy(camera_partial_pixels,expected,sizeof(preview_buffer));
            d->hor_res=720;d->ver_res=1280;d->rotation=LV_DISPLAY_ROTATION_90;
            lv_display_set_flush_cb(d,camera_partial_flush);
            lv_display_set_buffers(d,preview_buffer,second,1280u*rows*2u,LV_DISPLAY_RENDER_MODE_PARTIAL);
            unsigned hits=tab5_native_cache_hits;
            // LVGL removes the snap animation before drawing its endpoint.
            // Fresh snapshots remain valid there; changed content must not.
            lv_obj_invalidate(strip);lv_refr_now(d);
            unsigned expected_parts=phase==1?0:rows==512?1:2;
            assert(tab5_native_cache_hits-hits==expected_parts);
            assert(!memcmp(expected,camera_partial_pixels,sizeof(preview_buffer)));
            d->hor_res=1280;d->ver_res=720;d->rotation=LV_DISPLAY_ROTATION_0;
        }
    }
    lv_display_set_flush_cb(d,preview_flush);
    lv_display_set_buffers(d,preview_buffer,NULL,sizeof(preview_buffer),LV_DISPLAY_RENDER_MODE_FULL);
    lv_obj_set_style_bg_color(pages[0],original,0);tab5_draw_cache_drop();
    free(expected);free(second);free(camera_partial_pixels);camera_partial_pixels=NULL;
    puts("NATIVE_SETTLE_OK fresh endpoint snapshots accelerated, dirty content falls back, 512 rows use one part; pixels identical");
}
static void check_cache_publication(void) {
    page(0);lv_obj_update_layout(lv_screen_active());tab5_draw_cache_drop();
    for(int i=0;i<12;i++)tab5_draw_cache_step(pages[0]);
    assert(tab5_draw_cache_is_ready(pages[0]));
    lv_display_t *display=lv_display_get_default();
    lv_obj_invalidate(lv_screen_active());lv_refr_now(display);
    uint16_t *before=malloc(sizeof(preview_buffer));assert(before);memcpy(before,preview_buffer,sizeof(preview_buffer));
    lv_color_t original=lv_obj_get_style_bg_color(pages[0],0);
    lv_obj_set_style_bg_color(pages[0],lv_color_hex(0x113355),0);
    tab5_draw_cache_step_budget(pages[0],1280u*16u);
    assert(!tab5_draw_cache_is_ready(pages[0]));
    int (*hold)(const lv_obj_t *)=tab5_draw_cache_interaction;tab5_draw_cache_interaction=hold_test_page;
    unsigned builds=tab5_cache_builds;
    for(int i=0;i<16;i++)tab5_draw_cache_step(pages[0]);
    assert(builds==tab5_cache_builds&&tab5_draw_cache_is_ready(pages[0]));
    lv_obj_invalidate(lv_screen_active());lv_refr_now(display);
    assert(!memcmp(before,preview_buffer,sizeof(preview_buffer)));
    tab5_draw_cache_interaction=hold;
    for(int i=0;i<12;i++)tab5_draw_cache_step(pages[0]);
    assert(tab5_draw_cache_is_ready(pages[0]));
    lv_obj_invalidate(lv_screen_active());lv_refr_now(display);
    assert(memcmp(before,preview_buffer,sizeof(preview_buffer)));
    unsigned raw=0,cached=0;assert(cache_compare_frame(true,&raw,&cached));
    // Exercise the optional 24-row internal-memory path as well as 64 rows.
    camera_partial_pixels=calloc(1,sizeof(preview_buffer));assert(camera_partial_pixels);
    lv_display_set_flush_cb(display,camera_partial_flush);
    lv_display_set_buffers(display,preview_buffer,NULL,1280u*24u*2u,LV_DISPLAY_RENDER_MODE_PARTIAL);
    lv_obj_invalidate(lv_screen_active());lv_refr_now(display);
    uint16_t *actual=camera_partial_pixels;camera_partial_pixels=NULL;
    lv_display_set_flush_cb(display,preview_flush);
    lv_display_set_buffers(display,preview_buffer,NULL,sizeof(preview_buffer),LV_DISPLAY_RENDER_MODE_FULL);
    lv_obj_invalidate(lv_screen_active());lv_refr_now(display);
    assert(!memcmp(actual,preview_buffer,sizeof(preview_buffer)));free(actual);free(before);
    lv_obj_set_style_bg_color(pages[0],original,0);tab5_draw_cache_drop();
    puts("CACHE_PUBLICATION_OK interrupted rebuild preserves prior full image; release publishes latest; 24/64-row pixels agree");
}
static void check_scroll_cache(void) {
    lv_obj_t *identity_parent=lv_obj_create(lv_screen_active()),*identity_child=lv_obj_create(identity_parent);
    lv_obj_update_layout(identity_parent);
    lv_area_t original_area,transformed;lv_obj_get_coords(identity_child,&original_area);transformed=original_area;
    lv_obj_get_transformed_area(identity_child,&transformed,LV_OBJ_POINT_TRANSFORM_FLAG_RECURSIVE);
    assert(!memcmp(&original_area,&transformed,sizeof(transformed)));
    lv_obj_set_style_transform_rotation(identity_parent,100,0);transformed=original_area;
    lv_obj_get_transformed_area(identity_child,&transformed,LV_OBJ_POINT_TRANSFORM_FLAG_RECURSIVE);
    assert(memcmp(&original_area,&transformed,sizeof(transformed)));
    lv_obj_delete(identity_parent);
    puts("IDENTITY_DAMAGE_OK inclusive bounds preserved; real ancestor transforms retain generic bounds");
    page(8);codex_notice_style();lv_obj_update_layout(lv_screen_active());
    for(int i=0;i<12;i++)tab5_draw_cache_step(pages[8]);
    assert(tab5_draw_cache_is_ready(pages[8]));
    // A heartbeat with the same reply/notification status must retain the
    // prepared page, including when it is the neighbor of a swipe.
    codex_notice_style();
    assert(tab5_draw_cache_is_ready(pages[8]));
    lv_color_t original=lv_obj_get_style_bg_color(codex_reply_button,0);
    ui_bg_color(codex_reply_button,lv_color_hex(0x123456),0);
    assert(!tab5_draw_cache_is_ready(pages[8]));
    ui_bg_color(codex_reply_button,original,0);
    // Same effective color still needs an explicit override to remain stable
    // when its parent later changes; selectors retain independent values.
    lv_obj_t *parent=lv_obj_create(lv_screen_active()),*child=lv_obj_create(parent);
    lv_obj_remove_style_all(parent);lv_obj_remove_style_all(child);
    ui_text_color(parent,lv_color_hex(0x123456),0);
    ui_text_color(child,lv_obj_get_style_text_color(child,0),0);
    ui_text_color(parent,lv_color_hex(0x654321),0);
    assert(lv_color_eq(lv_obj_get_style_text_color(child,0),lv_color_hex(0x123456)));
    ui_text_color(child,lv_color_hex(0xabcdef),LV_STATE_PRESSED);
    lv_obj_add_state(child,LV_STATE_PRESSED);
    assert(lv_color_eq(lv_obj_get_style_text_color(child,0),lv_color_hex(0xabcdef)));
    lv_obj_remove_state(child,LV_STATE_PRESSED);
    assert(lv_color_eq(lv_obj_get_style_text_color(child,0),lv_color_hex(0x123456)));
    lv_obj_delete(parent);
    puts("STYLE_HEARTBEAT_OK unchanged notification retains cache; changes, inheritance and press feedback verified");
    tab5_draw_cache_drop();
    page(9);settings_select(2);lv_obj_update_layout(lv_screen_active());
    for(int i=0;i<24;i++)tab5_draw_cache_step(settings_content);
    assert(tab5_draw_cache_is_ready(settings_content));
    check_scroll_damage();
    int offsets[]={0,48,260,680,37};
    unsigned uncached_tasks=0,cached_tasks=0;
    lv_indev_t *vertical_touch=lv_indev_create();lv_indev_set_type(vertical_touch,LV_INDEV_TYPE_POINTER);
    lv_indev_set_display(vertical_touch,lv_display_get_default());lv_indev_set_read_cb(vertical_touch,camera_touch_read);
    camera_touch_point=(lv_point_t){1170,530};camera_touch_state=LV_INDEV_STATE_PRESSED;lv_indev_read(vertical_touch);
    camera_touch_point.y-=80;lv_indev_read(vertical_touch);
    camera_touch_point.y-=80;lv_indev_read(vertical_touch);
    assert(lv_indev_get_scroll_obj(vertical_touch)==settings_scroll);
    for(int i=0;i<3;i++)tab5_draw_cache_repair(settings_content);
    assert(tab5_draw_cache_is_ready(settings_content));
    assert(cache_compare_frame(true,&uncached_tasks,&cached_tasks)>0);
    tab5_draw_cache_drop();
    unsigned warm_steps=0;
    while(!tab5_draw_cache_is_ready(settings_content)&&warm_steps<32){cache_tick(NULL);warm_steps++;}
    assert(tab5_draw_cache_is_ready(settings_content));
    assert(cache_compare_frame(true,&uncached_tasks,&cached_tasks)>0);
    printf("COLD_DRAG_CACHE_OK cache becomes ready during held drag in %u bounded steps\n",warm_steps);
    camera_touch_state=LV_INDEV_STATE_RELEASED;lv_indev_read(vertical_touch);
    // LVGL 9.2's indev_delete does not cancel the momentum animation whose var
    // is this synthetic input. Real touch devices live for the app lifetime.
    lv_anim_delete(vertical_touch,NULL);lv_indev_reset(vertical_touch,NULL);lv_indev_delete(vertical_touch);
    puts("VERTICAL_TOUCH_CACHE_OK actual settings drag retains pixel-identical cache");
    for(unsigned i=0;i<sizeof(offsets)/sizeof(offsets[0]);i++) {
        lv_obj_scroll_to_y(settings_scroll,offsets[i],LV_ANIM_OFF);
        assert(tab5_draw_cache_is_ready(settings_content));
        assert(cache_compare_frame(false,&uncached_tasks,&cached_tasks)>0);
        assert(cache_compare_frame(true,&uncached_tasks,&cached_tasks)>0);
    }
    // Content changes must never reuse the former pixels.
    const char *old=lv_label_get_text(order_rows[0]);char saved[128];snprintf(saved,sizeof(saved),"%s",old);
    lv_label_set_text(order_rows[0],"缓存失效检查");assert(!tab5_draw_cache_is_ready(settings_content));
    tab5_draw_cache_drop();
    tab5_draw_cache_step(settings_content);assert(!tab5_draw_cache_is_ready(settings_content));
    lv_label_set_text(order_rows[0],saved);
    // A mutation during band preparation restarts that revision from row zero.
    for(int i=0;i<24;i++)tab5_draw_cache_step(settings_content);
    assert(tab5_draw_cache_is_ready(settings_content));
    assert(cache_compare_frame(true,&uncached_tasks,&cached_tasks)>0);
    tab5_draw_cache_drop();assert(tab5_cache_bytes==0);
    settings_select(0);page(0);lv_obj_update_layout(lv_screen_active());
    for(int i=0;i<12;i++)tab5_draw_cache_step(pages[0]);
    for(int i=0;i<12;i++)tab5_draw_cache_step(pages[8]);
    assert(tab5_draw_cache_is_ready(pages[0])&&tab5_draw_cache_is_ready(pages[8]));
    lv_obj_t *scroller=lv_tabview_get_content(tabs);
    // A repeated bridge snapshot must not discard either swipe page.
    for(int heartbeat=0;heartbeat<8;heartbeat++)render();
    printf("HEARTBEAT_PAGE_CACHE home=%d codex=%d\n",tab5_draw_cache_is_ready(pages[0]),tab5_draw_cache_is_ready(pages[8]));
    assert(tab5_draw_cache_is_ready(pages[0])&&tab5_draw_cache_is_ready(pages[8]));
    {
        lv_display_t *display=lv_display_get_default();
        camera_partial_pixels=calloc(1,sizeof(preview_buffer));assert(camera_partial_pixels);
        lv_display_set_flush_cb(display,camera_partial_flush);
        // The optional internal buffer holds only 24 full-width rows. Verify
        // clipped 1220-wide swipe damage with real INVALIDATE_AREA alignment.
        lv_display_set_buffers(display,preview_buffer,NULL,1280u*24u*2u,LV_DISPLAY_RENDER_MODE_PARTIAL);
        lv_obj_invalidate(lv_screen_active());lv_refr_now(display);
        lv_obj_scroll_by_raw(scroller,-200,0);
        audit_fill_pixels=audit_image_pixels=audit_argb_pixels=0;
        camera_audit_tree(lv_screen_active(),true);lv_refr_now(display);camera_audit_tree(lv_screen_active(),false);
        printf("HORIZONTAL_DAMAGE fill=%u cached_image=%u argb=%u\n",audit_fill_pixels,audit_image_pixels,audit_argb_pixels);
        assert(audit_fill_pixels<10000&&audit_image_pixels>=629520&&audit_argb_pixels==0);
        lv_area_t bounds;lv_obj_get_coords(scroller,&bounds);
        printf("STRIP_BOUNDS %d,%d,%d,%d scroll=%d\n",bounds.x1,bounds.y1,bounds.x2,bounds.y2,lv_obj_get_scroll_x(scroller));
        for(int i=0;i<2;i++){lv_obj_get_coords(pages[primary_pages[i]],&bounds);printf("PAGE_BOUNDS %d %d,%d,%d,%d\n",i,bounds.x1,bounds.y1,bounds.x2,bounds.y2);}
        uint16_t *actual=camera_partial_pixels;camera_partial_pixels=NULL;
        lv_display_set_buffers(display,preview_buffer,NULL,sizeof(preview_buffer),LV_DISPLAY_RENDER_MODE_FULL);
        lv_display_set_flush_cb(display,preview_flush);
        lv_obj_invalidate(lv_screen_active());lv_refr_now(display);
        assert(!memcmp(actual,preview_buffer,sizeof(preview_buffer)));free(actual);
        lv_obj_scroll_by_raw(scroller,200,0);
    }
    int positions[]={83,480,1000,610,0};
    for(unsigned i=0;i<sizeof(positions)/sizeof(positions[0]);i++) {
        // Mid-gesture positions before the tabview snaps and emits SCROLL_END.
        lv_obj_scroll_by_raw(scroller,lv_obj_get_scroll_x(scroller)-positions[i],0);
        assert(tab5_draw_cache_is_ready(pages[0])&&tab5_draw_cache_is_ready(pages[8]));
        assert(cache_compare_frame(false,&uncached_tasks,&cached_tasks)>0);
        assert(cache_compare_frame(true,&uncached_tasks,&cached_tasks)>0);
    }
    tab5_draw_cache_drop();page(8);lv_obj_update_layout(lv_screen_active());
    for(int p=0;p<3;p++)for(int i=0;i<12;i++)tab5_draw_cache_step(pages[primary_pages[p]]);
    assert(tab5_draw_cache_is_ready(pages[0])&&tab5_draw_cache_is_ready(pages[8])&&tab5_draw_cache_is_ready(pages[1]));
    assert(tab5_cache_bytes<=6u*1024u*1024u);
    tab5_draw_cache_step(pages[8]);
    for(int i=0;i<12;i++)tab5_draw_cache_step(pages[11]);
    assert(tab5_draw_cache_is_ready(pages[0])&&tab5_draw_cache_is_ready(pages[8])&&tab5_draw_cache_is_ready(pages[1])&&tab5_draw_cache_is_ready(pages[11]));
    assert(tab5_cache_bytes<=6u*1024u*1024u);
    puts("NEIGHBOR_CACHE_OK four main pages retained within 6 MiB, round trip keeps first page");
    page(0);lv_obj_update_layout(lv_screen_active());lv_refr_now(lv_display_get_default());
    uint16_t *before_trim=malloc(sizeof(preview_buffer));assert(before_trim);memcpy(before_trim,preview_buffer,sizeof(preview_buffer));
    int (*trim_interaction)(const lv_obj_t *)=tab5_draw_cache_interaction;
    tab5_draw_cache_interaction=hold_test_all;
    assert(tab5_draw_cache_trim_hidden(1)==0);
    assert(tab5_draw_cache_reclaim_staging()==0);
    tab5_draw_cache_interaction=trim_interaction;
    assert(tab5_draw_cache_reclaim_staging()>0);
    assert(tab5_draw_cache_reclaim_staging()==0);
    assert(tab5_draw_cache_is_ready(pages[0]));
    unsigned before_bytes=tab5_cache_bytes+tab5_native_cache_bytes;
    unsigned trimmed=tab5_draw_cache_trim_hidden(1);assert(trimmed>0&&tab5_cache_bytes+tab5_native_cache_bytes==before_bytes-trimmed);
    assert(tab5_draw_cache_is_ready(pages[0]));lv_obj_invalidate(lv_screen_active());lv_refr_now(lv_display_get_default());
    assert(!memcmp(before_trim,preview_buffer,sizeof(preview_buffer)));free(before_trim);
    preview_gallery_library_pending=true;lv_tick_inc(1000);
    unsigned hidden_bytes=tab5_cache_bytes+tab5_native_cache_bytes;
    for(int i=0;i<16;i++)cache_tick_work(NULL);
    assert(tab5_cache_bytes+tab5_native_cache_bytes==hidden_bytes);
    preview_gallery_library_pending=false;
    for(int p=0;p<4;p++)for(int i=0;i<12;i++)tab5_draw_cache_step(pages[primary_pages[p]]);
    assert(tab5_draw_cache_is_ready(pages[0])&&tab5_draw_cache_is_ready(pages[8])&&tab5_draw_cache_is_ready(pages[1])&&tab5_draw_cache_is_ready(pages[11]));
    puts("GALLERY_CACHE_FAIRNESS_OK hidden snapshots reclaimed; visible pixels and active gestures preserved; whole-library sync prevents hidden cache refill; normal four-page cache restored");
    page(0);lv_obj_update_layout(lv_screen_active());
    lv_obj_scroll_by_raw(scroller,-600,0);
    // LVGL starts the remaining snap animation before announcing the new tab.
    // The app callback must update navigation without jumping to its endpoint.
    lv_tabview_set_active(tabs,1,LV_ANIM_ON);
    int release_x=lv_obj_get_scroll_x(scroller);
    lv_obj_send_event(tabs,LV_EVENT_VALUE_CHANGED,NULL);
    printf("SWIPE_RELEASE_POSITION before=%d after=%d\n",release_x,lv_obj_get_scroll_x(scroller));
    assert(lv_obj_get_scroll_x(scroller)==release_x&&current_page==8);
    assert(lv_anim_get(scroller,NULL)!=NULL);
    bool was_cycling=prefs.cycle;int was_selected=prefs.selected;prefs.cycle=false;prefs.selected=0;
    unsigned saved_before=preview_preferences_saves;
    // Return toward overview; page preference writes must wait for settling.
    lv_tabview_set_active(tabs,0,LV_ANIM_ON);lv_obj_send_event(tabs,LV_EVENT_VALUE_CHANGED,NULL);
    assert(preview_preferences_saves==saved_before&&preferences_due>0);
    render_pending=true;tick(NULL);assert(render_pending&&preview_preferences_saves==saved_before);
    lv_anim_delete(scroller,NULL);page(0);
    preview_now_us+=1000000;tick(NULL);
    assert(!render_pending&&!preferences_due&&preview_preferences_saves==saved_before+1);
    puts("SWIPE_IDLE_WORK_OK render and preference commit deferred until gesture ends");
    prefs.cycle=was_cycling;prefs.selected=was_selected;display_controls_refresh();
    tab5_draw_cache_drop();page(11);lv_obj_update_layout(lv_screen_active());
    for(int i=0;i<12;i++)tab5_draw_cache_step(pages[11]);
    lv_obj_t *app=lv_obj_get_child(pages[11],0);
    lv_indev_t *touch=lv_indev_create();lv_indev_set_type(touch,LV_INDEV_TYPE_POINTER);
    lv_indev_set_display(touch,lv_display_get_default());lv_indev_set_read_cb(touch,camera_touch_read);
    camera_touch_point=(lv_point_t){94,166};camera_touch_state=LV_INDEV_STATE_PRESSED;lv_indev_read(touch);
    assert(lv_obj_has_state(app,LV_STATE_PRESSED));
    assert(!tab5_draw_cache_is_ready(pages[11]));
    unsigned repairs=0;
    while(!tab5_draw_cache_is_ready(pages[11])&&repairs<3){tab5_draw_cache_repair(pages[11]);repairs++;}
    // Press feedback is live; do not publish pressed pixels into the swipe snapshot.
    assert(!tab5_draw_cache_is_ready(pages[11]));
    cache_compare_frame(true,&uncached_tasks,&cached_tasks);
    camera_touch_point.x+=180;lv_indev_read(touch);
    camera_touch_point.x+=120;lv_indev_read(touch);
    assert(lv_indev_get_scroll_obj(touch)==scroller);
    assert(!lv_obj_has_state(app,LV_STATE_PRESSED));
    for(int i=0;i<3;i++)tab5_draw_cache_repair(pages[11]);
    assert(tab5_draw_cache_is_ready(pages[11]));
    assert(cache_compare_frame(true,&uncached_tasks,&cached_tasks)>0);
    camera_touch_state=LV_INDEV_STATE_RELEASED;lv_indev_read(touch);
    lv_anim_delete(touch,NULL);lv_indev_reset(touch,NULL);lv_indev_delete(touch);
    printf("TOUCH_CACHE_OK live press and unpressed swipe snapshot agree with direct rendering; %u blocked repair attempts\n",repairs);
    tab5_draw_cache_drop();assert(tab5_cache_bytes==0);
    lv_obj_t *temporary=lv_obj_create(lv_screen_active());lv_obj_remove_style_all(temporary);
    lv_obj_set_size(temporary,50,50);lv_obj_set_style_bg_opa(temporary,LV_OPA_COVER,0);
    tab5_draw_cache_step(temporary);assert(tab5_draw_cache_is_ready(temporary));
    lv_obj_delete(temporary);tab5_draw_cache_drop();assert(tab5_cache_bytes==0);
    temporary=lv_obj_create(lv_screen_active());lv_obj_remove_style_all(temporary);
    lv_obj_set_size(temporary,4096,4096);lv_obj_set_style_bg_opa(temporary,LV_OPA_COVER,0);
    tab5_draw_cache_step(temporary);assert(!tab5_draw_cache_is_ready(temporary)&&tab5_cache_bytes==0);
    lv_obj_delete(temporary);page(0);
    assert(cached_tasks<uncached_tasks);
    printf("SCROLL_CACHE_OK 25 full/partial frames pixel-identical, tasks %u -> %u; invalidation, deletion and budget fallback verified\n",uncached_tasks,cached_tasks);
}
static void confirm_quick_open(int index) {
    assert(quick_opening&&!strcmp(preview_desktop_target,quick_tasks[index].id));
    preview_desktop.busy=false;preview_desktop.status=200;
    strcpy(preview_desktop.task_id,quick_tasks[index].id);strcpy(preview_desktop.title,quick_tasks[index].title);
    strcpy(preview_desktop.project_id,quick_tasks[index].project);strcpy(preview_desktop.folder,quick_tasks[index].folder);
    preview_desktop.updated=quick_tasks[index].updated;strcpy(preview_desktop.message,"Codex 已在前台");quick_tick();
    assert(!quick_opening&&!quick_failed&&quick_index==index&&!strcmp(quick_opened,quick_tasks[index].id));
    assert(!strcmp(codex_selected,quick_opened));
}
static void assert_monthly_cover(const monthly_theme *theme) {
    bool has_poem=monthly_rendered_term>=0;
    char quote[96]={0};size_t used=0;
    for(int i=0;i<4;i++) {
        assert(lv_obj_has_flag(monthly_lines[i],LV_OBJ_FLAG_HIDDEN)==(!has_poem&&i>=2));
        assert(lv_obj_get_style_text_font(monthly_lines[i],0)==(has_poem?monthly_poems[monthly_rendered_term].font:&tab5_calendar_verse_24));
        if(!has_poem&&i>=2)continue;
        char line[32]={0};size_t n=0;const char *text=lv_label_get_text(monthly_lines[i]);
        for(size_t j=0;text[j];j++)if(text[j]!='\n')line[n++]=text[j];
        if(has_poem)assert(!strcmp(line,monthly_poems[monthly_rendered_term].lines[i]));
        else {memcpy(quote+used,line,n);used+=n;memcpy(quote+used,i?"。":"，",3);used+=3;}
    }
    if(!has_poem)assert(!strcmp(quote,theme->quote));
    assert(lv_obj_has_flag(monthly_term_seal,LV_OBJ_FLAG_HIDDEN)==!has_poem);
    assert(!strcmp(lv_label_get_text(monthly_poem_title),has_poem?monthly_poems[monthly_rendered_term].title:""));
    assert(!strcmp(lv_label_get_text(monthly_poem_byline),has_poem?monthly_poems[monthly_rendered_term].byline:""));
    char month[24]={0};used=0;const char *text=lv_label_get_text(monthly_name);
    for(size_t j=0;text[j];j++)if(text[j]!='\n')month[used++]=text[j];
    assert(!strcmp(month,theme->name));
    lv_obj_t *labels[]={monthly_name,monthly_lines[0],monthly_lines[1],monthly_lines[2],monthly_lines[3],monthly_poem_title,monthly_poem_byline,monthly_term_label};
    for(int i=0;i<8;i++) {
        if(lv_obj_has_flag(labels[i],LV_OBJ_FLAG_HIDDEN))continue;
        const lv_font_t *font=lv_obj_get_style_text_font(labels[i],0);
        text=lv_label_get_text(labels[i]);
        for(uint32_t at=0;text[at];) {
            uint32_t glyph=lv_text_encoded_next(text,&at);lv_font_glyph_dsc_t desc;
            if(glyph!='\n')assert(lv_font_get_glyph_dsc(font,&desc,glyph,0));
        }
        lv_obj_update_layout(labels[i]);lv_point_t size;
        lv_text_get_size(&size,text,font,0,lv_obj_get_style_text_line_space(labels[i],0),LV_COORD_MAX,LV_TEXT_FLAG_NONE);
        assert(size.x<=lv_obj_get_width(labels[i])&&size.y<=lv_obj_get_height(labels[i]));
        if(i>=1&&i<=4){assert(font->line_height>=24);assert(lv_obj_get_y(labels[i])+size.y<=365);}
        assert(lv_obj_get_y(labels[i])+size.y<=lv_obj_get_height(lv_obj_get_parent(labels[i])));
    }
}
static void check_clock_faces(void) {
    ui_preferences saved_prefs=prefs;int saved_style=clock_style;
    cJSON *saved_weather=cJSON_DetachItemFromObject(shown,"weather"),*sample_weather=cJSON_Duplicate(saved_weather,true);
    cJSON_AddItemToObject(shown,"weather",sample_weather);cJSON_DeleteItemFromObject(sample_weather,"daily");cJSON_DeleteItemFromObject(sample_weather,"utcOffsetSeconds");
    cJSON_AddNumberToObject(sample_weather,"utcOffsetSeconds",num(shown,"utcOffsetSeconds"));
    cJSON *solar_days=cJSON_AddArrayToObject(sample_weather,"daily"),*solar_day=cJSON_CreateObject();cJSON_AddItemToArray(solar_days,solar_day);
    cJSON_AddStringToObject(solar_day,"date","2026-09-23");cJSON_AddStringToObject(solar_day,"sunrise","05:48");cJSON_AddStringToObject(solar_day,"sunset","18:21");
    // Align the daylight marker with the preview's fixed 10:08:42 reading.
    clock_solar_refresh(1790129322+28800);
    char saved_time[20],saved_seconds[4],saved_header[20];
    snprintf(saved_time,sizeof(saved_time),"%s",lv_label_get_text(clock_big));
    snprintf(saved_seconds,sizeof(saved_seconds),"%s",lv_label_get_text(clock_seconds));
    snprintf(saved_header,sizeof(saved_header),"%s",lv_label_get_text(clock_label));
    struct tm saved_face=clock_face_time,sample={.tm_hour=10,.tm_min=8,.tm_sec=42};
    lv_indev_t *touch=lv_indev_create();lv_indev_set_type(touch,LV_INDEV_TYPE_POINTER);
    lv_indev_set_display(touch,lv_display_get_default());lv_indev_set_read_cb(touch,camera_touch_read);
    page(7);ui_text(clock_big,"10:08");ui_text(clock_seconds,":42");ui_text(clock_label,"10:08:42");
    analog_update(&sample,500);
    for(int i=0;i<UI_CLOCK_STYLES;i++) {
        lv_obj_update_layout(clock_controls);lv_area_t area;lv_obj_get_coords(lv_obj_get_parent(clock_name),&area);
        gallery_pointer_click(touch,(lv_point_t){(area.x1+area.x2)/2,(area.y1+area.y2)/2});
        assert(!lv_obj_has_flag(clock_picker,LV_OBJ_FLAG_HIDDEN));
        char picker_name[48];snprintf(picker_name,sizeof(picker_name),"clock-face-picker-current-%d",clock_style);capture(picker_name);
        if(!i)capture("clock-face-picker");
        gallery_pointer_click(touch,(lv_point_t){64+(i%3)*388+190,112+(i/3)*162+66});
        assert(clock_style==i&&preview_saved_clock==i&&lv_obj_has_flag(clock_picker,LV_OBJ_FLAG_HIDDEN));
        assert(!strcmp(lv_label_get_text(clock_name),clock_style_names[i]));
        analog_update(&sample,500);char name[32];snprintf(name,sizeof(name),"clock-face-%d",i);capture(name);
        lv_obj_t *labels[]={clock_big,clock_seconds,date_label,clock_lunar,clock_holiday,clock_name,clock_style_index,clock_solar_times[0],clock_solar_times[1],clock_solar_hours[0],clock_solar_hours[1],clock_solar_hours[2],clock_solar_hours[3],clock_solar_hours[4]};
        for(unsigned n=0;n<sizeof(labels)/sizeof(labels[0]);n++) {
            lv_obj_update_layout(labels[n]);lv_area_t bounds;lv_obj_get_coords(labels[n],&bounds);
            assert(bounds.x1>=30&&bounds.x2<1250&&bounds.y1>=92&&bounds.y2<608);
            const char *text=lv_label_get_text(labels[n]);const lv_font_t *font=lv_obj_get_style_text_font(labels[n],0);
            for(uint32_t at=0;text[at];) {uint32_t glyph=lv_text_encoded_next(text,&at);lv_font_glyph_dsc_t g;assert(lv_font_get_glyph_dsc(font,&g,glyph,0));}
        }
    }
    clock_picker_open(NULL);capture("clock-face-picker-current-8");clock_picker_close(NULL);
    lv_tick_inc(50);uint32_t idle_before=lv_display_get_inactive_time(NULL);page(0);
    assert(lv_display_get_inactive_time(NULL)==idle_before);page(7);
    clock_picker_open(NULL);page(0);assert(lv_obj_has_flag(clock_picker,LV_OBJ_FLAG_HIDDEN));page(7);
    // A failed save keeps both the persisted choice and active face intact.
    int kept=clock_style;clock_picker_open(NULL);preview_clock_save_fail=true;
    gallery_pointer_click(touch,(lv_point_t){642,178});
    assert(clock_style==kept&&preview_saved_clock==kept&&!lv_obj_has_flag(clock_picker,LV_OBJ_FLAG_HIDDEN));
    assert(strstr(lv_label_get_text(clock_picker_hint),"失败"));capture("clock-face-save-failure");
    preview_clock_save_fail=false;gallery_pointer_click(touch,(lv_point_t){1136,40});
    assert(lv_obj_has_flag(clock_picker,LV_OBJ_FLAG_HIDDEN));
    struct tm samples[]={{.tm_hour=0},{.tm_hour=12},{.tm_hour=10,.tm_min=8,.tm_sec=42},{.tm_hour=15,.tm_min=50,.tm_sec=10},{.tm_hour=23,.tm_min=59,.tm_sec=59}};
    for(unsigned sample=0;sample<sizeof(samples)/sizeof(samples[0]);sample++) {
        clock_select_style(0);analog_update(&samples[sample],500);
        int ordinary[3];for(int hand=0;hand<3;hand++)ordinary[hand]=lv_obj_get_style_transform_rotation(clock_hands[hand],0);
        clock_select_style(5);analog_update(&samples[sample],500);
        for(int hand=0;hand<3;hand++)assert(!lv_obj_has_flag(clock_hands[hand],LV_OBJ_FLAG_HIDDEN)&&lv_obj_get_style_transform_rotation(clock_hands[hand],0)==ordinary[hand]);
        assert(!strcmp(lv_label_get_text(clock_numbers[0]),"12")&&!strcmp(lv_label_get_text(clock_numbers[3]),"3")&&!strcmp(lv_label_get_text(clock_numbers[6]),"6")&&!strcmp(lv_label_get_text(clock_numbers[9]),"9"));
    }
    struct tm last={.tm_hour=23,.tm_min=59,.tm_sec=59};
    capture("clock-face-day-night-2359");
    clock_select_style(8);analog_update(&last,500);capture("clock-face-lcd-2359");
    lv_area_t lcd;lv_obj_get_coords(clock_face,&lcd);
    struct tm forty_two={.tm_hour=10,.tm_min=8,.tm_sec=42},forty_three=forty_two;forty_three.tm_sec=43;
    analog_update(&forty_two,0);lv_refr_now(lv_display_get_default());
    int sx=lcd.x1+333+19,sy=lcd.y1+284+30;
    uint16_t off=preview_buffer[sy*1280+sx];
    analog_update(&forty_three,0);lv_refr_now(lv_display_get_default());
    assert(preview_buffer[sy*1280+sx]!=off&&preview_buffer[sy*1280+sx]==lv_color_to_u16(lv_color_hex(0x263520)));
    capture("clock-face-lcd-seconds-43");
    clock_select_style(saved_style);prefs=saved_prefs;display_controls_refresh();
    ui_text(clock_big,saved_time);ui_text(clock_seconds,saved_seconds);ui_text(clock_label,saved_header);analog_update(&saved_face,0);clock_layout();
    cJSON_ReplaceItemInObject(shown,"weather",saved_weather);clock_solar_refresh((time_t)(num(shown,"epochUtc")+num(shown,"utcOffsetSeconds")));
    page(0);lv_indev_delete(touch);
    puts("CLOCK_FACES_UI_OK nine_native_faces pointer_picker previous_ids_preserved glyphs_bounds save_failure_rollback solar_standard_12_hour_hands lcd_2359 seconds_pixel_42_43");
}
static void check_clock_solar(void) {
    cJSON *w=cJSON_Parse("{\"city\":\"上海\",\"utcOffsetSeconds\":28800,\"daily\":[{\"date\":\"2026-09-23\",\"sunrise\":\"05:48\",\"sunset\":\"18:21\"},{\"date\":\"2026-09-24\",\"sunrise\":\"05:49\",\"sunset\":\"18:20\"}]}");
    time_t noon=1790149800+28800;clock_solar_state s=clock_solar_for(w,noon,28800);
    assert(s.mode==CLOCK_SOLAR_NORMAL&&s.start==348&&s.end==1101);
    assert(clock_solar_progress(s,348)==0&&clock_solar_progress(s,608)==345&&clock_solar_progress(s,1100)>=998&&clock_solar_progress(s,1101)<0&&clock_solar_progress(s,300)<0);
    assert(clock_solar_ring_span(s)==188&&clock_solar_ring_angle(s,720)==0);
    assert(clock_solar_ring_angle(s,608)==332&&clock_solar_ring_angle(s,0)==180&&clock_solar_ring_angle(s,360)==270&&clock_solar_ring_angle(s,1080)==90);
    s=clock_solar_for(w,noon+86400,28800);assert(s.mode==CLOCK_SOLAR_NORMAL&&s.rise==349&&s.set==1100);
    s=clock_solar_for(w,noon+86400*2,28800);assert(s.mode==CLOCK_SOLAR_DEFAULT&&s.rise==360&&s.set==1080);
    cJSON_AddBoolToObject(w,"dailyStale",true);s=clock_solar_for(w,noon,28800);assert(s.mode==CLOCK_SOLAR_NORMAL&&s.cached);
    cJSON *day=cJSON_GetArrayItem(item(w,"daily"),0);
    cJSON_ReplaceItemInObject(day,"sunrise",cJSON_CreateString("25:00"));s=clock_solar_for(w,noon,28800);assert(s.mode==CLOCK_SOLAR_DEFAULT);
    cJSON_AddNumberToObject(day,"daylightSeconds",86400);s=clock_solar_for(w,noon,28800);assert(s.mode==CLOCK_SOLAR_DAY);
    assert(clock_solar_progress(s,0)==1000);
    cJSON_SetNumberValue(item(day,"daylightSeconds"),0);s=clock_solar_for(w,noon,28800);assert(s.mode==CLOCK_SOLAR_NIGHT);
    assert(clock_solar_progress(s,720)<0);
    cJSON_ReplaceItemInObject(w,"utcOffsetSeconds",cJSON_CreateNumber(36000));
    s=clock_solar_for(w,noon,28800);assert(s.mode==CLOCK_SOLAR_NIGHT&&clock_solar_ring_span(s)==0);
    assert(clock_solar_ring_angle(s,600)==0&&clock_solar_ring_angle(s,1320)==180);
    cJSON_SetNumberValue(item(day,"daylightSeconds"),86400);s=clock_solar_for(w,noon,28800);
    assert(s.mode==CLOCK_SOLAR_DAY&&clock_solar_ring_span(s)==360&&clock_solar_ring_angle(s,600)==0);
    cJSON_ReplaceItemInObject(w,"utcOffsetSeconds",cJSON_CreateNumber(28800));
    cJSON_DeleteItemFromObject(day,"daylightSeconds");cJSON_ReplaceItemInObject(day,"sunrise",cJSON_CreateString("22:00"));cJSON_ReplaceItemInObject(day,"sunset",cJSON_CreateString("02:00"));
    s=clock_solar_for(w,noon,28800);assert(s.mode==CLOCK_SOLAR_NORMAL&&s.start==1320&&s.end==120);
    assert(clock_solar_progress(s,0)==500&&clock_solar_progress(s,720)<0);
    cJSON_ReplaceItemInObject(w,"utcOffsetSeconds",cJSON_CreateNumber(36000));
    s=clock_solar_for(w,noon,28800);assert(s.mode==CLOCK_SOLAR_NORMAL&&s.start==1200&&s.end==0);
    assert(clock_solar_ring_angle(s,1200)==150&&clock_solar_ring_angle(s,600)==0);
    // A foreign city's date follows its offset, even before the clock's midnight.
    cJSON_ReplaceItemInObject(w,"utcOffsetSeconds",cJSON_CreateNumber(-18000));
    time_t morning=1790103600+28800;s=clock_solar_for(w,morning,28800);assert(s.mode==CLOCK_SOLAR_DEFAULT);
    cJSON *saved=cJSON_DetachItemFromObject(shown,"weather");cJSON_AddItemToObject(shown,"weather",w);
    int saved_style=clock_style;page(7);clock_select_style(5);struct tm solar_time;gmtime_r(&noon,&solar_time);analog_update(&solar_time,0);clock_solar_refresh(noon+86400*3);
    assert(strstr(lv_label_get_text(clock_solar_times[0]),"默认")&&!strstr(lv_label_get_text(clock_solar_times[0]),"待更新"));
    assert(strstr(lv_label_get_text(clock_solar_times[0]),"06:00")&&strstr(lv_label_get_text(clock_solar_times[1]),"18:00"));capture("clock-face-solar-default");
    cJSON_ReplaceItemInObject(w,"utcOffsetSeconds",cJSON_CreateNumber(28800));cJSON_ReplaceItemInObject(day,"sunrise",cJSON_CreateString("05:48"));cJSON_ReplaceItemInObject(day,"sunset",cJSON_CreateString("18:21"));cJSON_DeleteItemFromObject(w,"dailyStale");
    clock_solar_refresh(noon);assert(strstr(lv_label_get_text(clock_solar_times[0]),"05:48")&&!strstr(lv_label_get_text(clock_solar_times[0]),"默认"));capture("clock-face-solar-local");
    lv_area_t dial;lv_obj_get_coords(clock_face,&dial);lv_point_t probe=clock_polar(dial.x1+214,dial.y1+214,190,280);
    uint16_t warm=preview_buffer[probe.y*1280+probe.x];assert(warm==lv_color_to_u16(lv_color_hex(0xDDB97D)));
    cJSON_ReplaceItemInObject(day,"sunrise",cJSON_CreateString("08:00"));cJSON_ReplaceItemInObject(day,"sunset",cJSON_CreateString("16:00"));
    clock_solar_refresh(noon);capture("clock-face-solar-winter");assert(preview_buffer[probe.y*1280+probe.x]!=warm&&preview_buffer[probe.y*1280+probe.x]==lv_color_to_u16(lv_color_hex(0x425C77)));
    cJSON_AddNumberToObject(day,"daylightSeconds",86400);clock_solar_refresh(noon);capture("clock-face-solar-polar-day");
    cJSON_SetNumberValue(item(day,"daylightSeconds"),0);clock_solar_refresh(noon);capture("clock-face-solar-polar-night");
    assert(strstr(lv_label_get_text(clock_solar_times[0]),"--:--")&&strstr(lv_label_get_text(clock_solar_times[0]),"极夜")&&preview_buffer[probe.y*1280+probe.x]==lv_color_to_u16(lv_color_hex(0x425C77)));
    cJSON_ReplaceItemInObject(shown,"weather",saved);clock_solar_refresh(noon);clock_select_style(saved_style);page(0);
    puts("CLOCK_SOLAR_UI_OK city_date_timezone midnight_default_0600_1800 cache invalid polar_day_night wrapped_daylight local_24_hour_ring_pixels ordinary_inner_dial");
}
#include "date_countdown_test.h"
static void check_header_clock(void) {
    lv_indev_t *touch=lv_indev_create();lv_indev_set_type(touch,LV_INDEV_TYPE_POINTER);
    lv_indev_set_display(touch,lv_display_get_default());lv_indev_set_read_cb(touch,camera_touch_read);
    const int origins[]={0,1,8,11};
    for(unsigned i=0;i<sizeof(origins)/sizeof(origins[0]);i++) {
        page(origins[i]);gallery_pointer_click(touch,(lv_point_t){1100,32});assert(current_page==7);
        gallery_pointer_click(touch,(lv_point_t){1100,32});assert(current_page==7);
        gallery_pointer_click(touch,(lv_point_t){56,40});assert(current_page==origins[i]);
    }
    page(11);page(5);page(9);assert(settings_return==5);
    gallery_pointer_click(touch,(lv_point_t){1100,32});assert(current_page==7);
    gallery_pointer_click(touch,(lv_point_t){308,40});assert(timer_open());
    gallery_pointer_click(touch,(lv_point_t){56,40});assert(!timer_open()&&current_page==9&&settings_return==5);
    gallery_pointer_click(touch,(lv_point_t){56,40});assert(current_page==5&&detail_return==11);
    gallery_pointer_click(touch,(lv_point_t){56,40});assert(current_page==11);
    page(1);gallery_pointer_click(touch,(lv_point_t){1100,32});page(11);page(7);
    gallery_pointer_click(touch,(lv_point_t){56,40});assert(current_page==11);
    lv_indev_delete(touch);page(0);
    puts("HEADER_CLOCK_OK pointer_navigation_repeat_tap_settings_return_countdown_return_no_stale_origin");
}
static void check_countdowns(void) {
    int64_t saved_now=preview_now_us;unsigned chimes=preview_timer_chimes;
    page(11);page(7);int style_before=clock_style;
    lv_indev_t *timer_touch=lv_indev_create();lv_indev_set_type(timer_touch,LV_INDEV_TYPE_POINTER);
    lv_indev_set_display(timer_touch,lv_display_get_default());lv_indev_set_read_cb(timer_touch,camera_touch_read);
    assert(!lv_obj_has_flag(clock_segments,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(workbench_title,LV_OBJ_FLAG_HIDDEN));
    capture("clock-segment-classic");
    gallery_pointer_click(timer_touch,(lv_point_t){308,40});assert(timer_open()&&clock_style==style_before);
    capture("clock-countdown-ready");
    gallery_pointer_click(timer_touch,(lv_point_t){56,40});assert(!timer_open()&&current_page==11&&clock_countdown.phase==COUNTDOWN_READY);
    page(7);gallery_pointer_click(timer_touch,(lv_point_t){308,40});assert(timer_open());
    gallery_pointer_click(timer_touch,(lv_point_t){168,40});assert(!timer_open()&&current_page==7&&clock_style==style_before);
    page(0);assert(lv_obj_has_flag(clock_segments,LV_OBJ_FLAG_HIDDEN)&&!lv_obj_has_flag(workbench_title,LV_OBJ_FLAG_HIDDEN));
    page(7);gallery_pointer_click(timer_touch,(lv_point_t){308,40});assert(timer_open());
    lv_dropdown_set_selected(timer_fields[0],23);lv_dropdown_set_selected(timer_fields[1],59);lv_dropdown_set_selected(timer_fields[2],59);timer_duration_changed(NULL);
    timer_action(NULL);capture("clock-countdown-running");
    assert(lv_obj_has_flag(timer_setup,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(timer_header,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(timer_controls,LV_OBJ_FLAG_HIDDEN));
    lv_point_t text_size;lv_text_get_size(&text_size,lv_label_get_text(timer_value),&tab5_timer_digits_192,0,0,LV_COORD_MAX,LV_TEXT_FLAG_NONE);
    assert(text_size.x<=1152&&text_size.y<=300);
    gallery_pointer_click(timer_touch,(lv_point_t){640,360});assert(!lv_obj_has_flag(timer_controls,LV_OBJ_FLAG_HIDDEN));
    preview_now_us+=6000000;clock_tick(NULL);assert(lv_obj_has_flag(timer_controls,LV_OBJ_FLAG_HIDDEN));
    gallery_pointer_click(timer_touch,(lv_point_t){640,360});capture("clock-countdown-controls");
    int64_t deadline=clock_countdown.deadline_ms;
    gallery_pointer_click(timer_touch,(lv_point_t){56,40});assert(!timer_open()&&current_page==0&&clock_countdown.phase==COUNTDOWN_RUNNING&&clock_countdown.deadline_ms==deadline);
    page(7);gallery_pointer_click(timer_touch,(lv_point_t){308,40});gallery_pointer_click(timer_touch,(lv_point_t){640,360});
    gallery_pointer_click(timer_touch,(lv_point_t){168,40});assert(!timer_open()&&current_page==7&&clock_countdown.phase==COUNTDOWN_RUNNING);
    preview_now_us+=1000000;clock_tick(NULL);assert(clock_countdown.deadline_ms==deadline);
    gallery_pointer_click(timer_touch,(lv_point_t){308,40});assert(timer_open()&&clock_countdown.phase==COUNTDOWN_RUNNING);
    assert(lv_obj_has_flag(timer_header,LV_OBJ_FLAG_HIDDEN));
    gallery_pointer_click(timer_touch,(lv_point_t){640,360});
    timer_reset_clicked(NULL);lv_dropdown_set_selected(timer_fields[0],0);lv_dropdown_set_selected(timer_fields[2],0);
    lv_dropdown_set_selected(timer_fields[1],0);timer_duration_changed(NULL);
    assert(lv_obj_has_state(timer_action_button,LV_STATE_DISABLED));
    lv_dropdown_set_selected(timer_fields[2],3);timer_duration_changed(NULL);
    lv_obj_send_event(timer_action_button,LV_EVENT_CLICKED,NULL);
    assert(clock_countdown.phase==COUNTDOWN_RUNNING);
    preview_now_us+=1250000;clock_tick(NULL);assert(!strcmp(lv_label_get_text(timer_value),"00:00:02"));
    gallery_pointer_click(timer_touch,(lv_point_t){640,360});lv_obj_update_layout(timer_layer);
    gallery_pointer_click(timer_touch,(lv_point_t){804,600});assert(clock_countdown.phase==COUNTDOWN_PAUSED);
    assert(lv_obj_has_flag(timer_setup,LV_OBJ_FLAG_HIDDEN)&&!lv_obj_has_flag(timer_controls,LV_OBJ_FLAG_HIDDEN));
    int64_t remaining=clock_countdown.remaining_ms;preview_now_us+=9000000;clock_tick(NULL);assert(clock_countdown.remaining_ms==remaining);
    capture("clock-countdown-paused");
    timer_action(NULL);timer_close(NULL);page(0);preview_now_us+=remaining*1000+500000;
    clock_tick(NULL);assert(clock_countdown.phase==COUNTDOWN_DONE&&timer_open()&&preview_timer_chimes==chimes+1);
    capture("clock-countdown-finished");clock_tick(NULL);assert(preview_timer_chimes==chimes+1);
    gallery_pointer_click(timer_touch,(lv_point_t){56,40});assert(!timer_open()&&current_page==0&&clock_countdown.phase==COUNTDOWN_DONE);
    timer_show(NULL);
    timer_reset_clicked(NULL);assert(clock_countdown.remaining_ms==3000&&clock_countdown.phase==COUNTDOWN_READY);
    assert(!lv_obj_has_flag(timer_setup,LV_OBJ_FLAG_HIDDEN));lv_indev_delete(timer_touch);
    timer_close(NULL);countdown_reset(&clock_countdown,300);lv_dropdown_set_selected(timer_fields[1],5);lv_dropdown_set_selected(timer_fields[2],0);timer_render();
    page(9);settings_select(1);int old_saver=saver_style;
    assert(lv_obj_has_flag(date_settings_row,LV_OBJ_FLAG_HIDDEN));capture("countdown-settings-hidden");
    lv_dropdown_set_selected(saver_selector,ui_saver_option_from_style(UI_SAVER_COUNTDOWN));lv_obj_send_event(saver_selector,LV_EVENT_VALUE_CHANGED,NULL);
    assert(!lv_obj_has_flag(date_settings_row,LV_OBJ_FLAG_HIDDEN)&&lv_obj_get_style_y(screen_sound_group,0)==466);
    capture("countdown-settings");
    preview_saver_save_fail=true;lv_dropdown_set_selected(saver_selector,0);lv_obj_send_event(saver_selector,LV_EVENT_VALUE_CHANGED,NULL);
    assert(!lv_obj_has_flag(date_settings_row,LV_OBJ_FLAG_HIDDEN));preview_saver_save_fail=false;
    lv_dropdown_set_selected(saver_selector,0);lv_obj_send_event(saver_selector,LV_EVENT_VALUE_CHANGED,NULL);
    assert(lv_obj_has_flag(date_settings_row,LV_OBJ_FLAG_HIDDEN)&&lv_obj_get_style_y(screen_sound_group,0)==386);
    lv_dropdown_set_selected(saver_selector,ui_saver_option_from_style(UI_SAVER_COUNTDOWN));lv_obj_send_event(saver_selector,LV_EVENT_VALUE_CHANGED,NULL);
    date_editor_show(NULL);
    lv_textarea_set_text(date_title_input,"");date_keyboard_show(NULL);
    assert(lv_obj_has_flag(date_target_fields,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(date_footer,LV_OBJ_FLAG_HIDDEN));
    const char *spelling="nihao";for(const char *p=spelling;*p;p++){char key[]={*p,0};assert(press_text(date_keyboard,key));}
    assert(!strcmp(tab5_ime_pending_for(date_ime),"nihao")&&!tab5_ime_pending()[0]);
    assert(press_text(lv_obj_get_child(date_ime,2),"你好"));assert(!strcmp(lv_textarea_get_text(date_title_input),"你好"));
    capture("date-countdown-name-input");date_keyboard_hide(NULL);
    assert(!lv_obj_has_flag(date_target_fields,LV_OBJ_FLAG_HIDDEN)&&!lv_obj_has_flag(date_footer,LV_OBJ_FLAG_HIDDEN));
    lv_textarea_set_text(date_title_input,"重要日子");
    date_edit.year=2028;date_edit.month=2;date_edit.day=30;
    date_editor_save(NULL);assert(date_editor_open()&&strstr(lv_label_get_text(date_feedback),"日期无效"));
    date_edit.day=29;preview_date_save_fail=true;date_editor_save(NULL);
    assert(date_editor_open()&&!date_countdown.version&&strstr(lv_label_get_text(date_feedback),"保存失败"));
    preview_date_save_fail=false;date_editor_save(NULL);assert(!date_editor_open()&&date_countdown.day==29);
    date_editor_show(NULL);capture("date-countdown-editor");date_editor_close(NULL);
    struct tm local={.tm_year=128,.tm_mon=1,.tm_mday=28,.tm_hour=23,.tm_min=59};
    saver_style=UI_SAVER_COUNTDOWN;saver_enter(false);date_saver_update(&local);
    assert(!strcmp(lv_label_get_text(date_saver_days),"1")&&!lv_obj_has_flag(date_saver_layer,LV_OBJ_FLAG_HIDDEN));
#if TAB5_LOCAL_ART
    assert(lv_obj_has_flag(gallery_layer,LV_OBJ_FLAG_HIDDEN));
#endif
    capture("date-countdown-tomorrow");local.tm_mday=29;local.tm_hour=0;date_saver_update(&local);
    assert(!strcmp(lv_label_get_text(date_saver_days),"")&&!strcmp(lv_label_get_text(date_saver_note),"就是今天"));capture("date-countdown-today");
    local.tm_mon=2;local.tm_mday=1;date_saver_update(&local);assert(!strcmp(lv_label_get_text(date_saver_days),"1")&&!strcmp(lv_label_get_text(date_saver_note),"已过去"));
    capture("date-countdown-past");saver_leave();saver_style=old_saver;
    ui_saver_style_save(old_saver);lv_dropdown_set_selected(saver_selector,ui_saver_option_from_style(old_saver));date_settings_refresh();
    memset(&date_countdown,0,sizeof(date_countdown));memset(&preview_date_countdown,0,sizeof(preview_date_countdown));
    preview_now_us=saved_now;preview_timer_chimes=chimes;page(0);
    check_date_countdown_editor();
    // Advance the real LVGL animation clock after enabling/disabling controls.
    // Later cache tests deliberately call cache_tick without the animation timer.
    for(int i=0;i<20;i++){lv_tick_inc(50);lv_anim_refr_now();}
    assert(lv_anim_get(date_title_input,NULL)==NULL);
    puts("COUNTDOWNS_UI_OK segmented_pointer_navigation background_timer_return digit_only_controls pause_resume_background_expiry_once reset zero_guard date_leap_midnight_past persistence_failure separate_pinyin");
}
static void check_display_modes(void) {
    ui_preferences original=prefs;int original_return=settings_return,original_style=clock_style;
    int64_t original_now=preview_now_us;int64_t original_alert=alert_until;
    page(9);settings_select(2);prefs.selected=-1;prefs.cycle=true;prefs.interval=15;prefs.alerts=false;prefs.saver_minutes=0;
    for(int i=0;i<8;i++){prefs.order[i]=i;prefs.enabled[i]=true;}
    display_controls_refresh();
    lv_indev_t *touch=lv_indev_create();lv_indev_set_type(touch,LV_INDEV_TYPE_POINTER);
    lv_indev_set_display(touch,lv_display_get_default());lv_indev_set_read_cb(touch,camera_touch_read);
    uint8_t kept_order[8],kept_enabled[8];memcpy(kept_order,prefs.order,8);memcpy(kept_enabled,prefs.enabled,8);
    const char *captures[]={"display-mode-cycle","display-mode-fixed","display-mode-auto"};
    for(int choice=0;choice<3;choice++) {
        lv_obj_update_layout(settings_scroll);lv_area_t area;lv_obj_get_coords(display_choices[choice],&area);
        gallery_pointer_click(touch,(lv_point_t){(area.x1+area.x2)/2,(area.y1+area.y2)/2});
        assert(current_page==9&&display_choice()==choice);
        for(int i=0;i<3;i++)assert(lv_obj_has_state(display_choices[i],LV_STATE_CHECKED)==(i==choice));
        assert(lv_obj_has_flag(display_cycle_options,LV_OBJ_FLAG_HIDDEN)==(choice!=0));
        assert(lv_obj_has_flag(display_fixed_options,LV_OBJ_FLAG_HIDDEN)==(choice!=1));
        assert(!memcmp(prefs.order,kept_order,8)&&!memcmp(prefs.enabled,kept_enabled,8)&&prefs.interval==15);
        capture(captures[choice]);
        if(choice==0){lv_obj_scroll_to_y(settings_scroll,500,LV_ANIM_OFF);capture("display-mode-cycle-order");lv_obj_scroll_to_y(settings_scroll,0,LV_ANIM_OFF);}
    }
    // Automatic music routing survives manual browsing and clock-face selection.
    assert(prefs.selected==-1&&!prefs.cycle);
    lv_obj_send_event(workbench_nav[0],LV_EVENT_CLICKED,NULL);assert(prefs.selected==-1&&!prefs.cycle);
    page(7);assert(clock_select_style((original_style+1)%UI_CLOCK_STYLES));assert(prefs.selected==-1&&!prefs.cycle);
    cJSON *music=item(shown,"music"),*saved_music=cJSON_Duplicate(music,true);assert(music&&saved_music);
    cJSON_ReplaceItemInObject(music,"playing",cJSON_CreateBool(true));alert_until=0;page(0);tick(NULL);assert(current_page==5&&display_choice()==2);
    cJSON_ReplaceItemInObject(music,"playing",cJSON_CreateBool(false));tick(NULL);assert(current_page==0&&display_choice()==2);
    cJSON_ReplaceItemInObject(shown,"music",saved_music);
    page(9);settings_select(2);
    lv_obj_send_event(display_choices[1],LV_EVENT_CLICKED,NULL);
    lv_dropdown_set_selected(display_fixed_page,7);lv_obj_send_event(display_fixed_page,LV_EVENT_VALUE_CHANGED,NULL);
    assert(current_page==9&&settings_return==7&&preview_saved_preferences.selected==7&&!preview_saved_preferences.cycle);
    capture("display-mode-fixed-clock");
    // A failed save never changes the selected mode or the stored target.
    preview_preferences_save_ok=false;lv_obj_send_event(display_choices[2],LV_EVENT_CLICKED,NULL);
    assert(display_choice()==1&&prefs.selected==7&&settings_return==7&&preview_saved_preferences.selected==7);
    assert(lv_obj_has_state(display_choices[1],LV_STATE_CHECKED)&&!lv_obj_has_flag(display_fixed_options,LV_OBJ_FLAG_HIDDEN));
    lv_dropdown_set_selected(display_fixed_page,3);lv_obj_send_event(display_fixed_page,LV_EVENT_VALUE_CHANGED,NULL);
    assert(prefs.selected==7&&lv_dropdown_get_selected(display_fixed_page)==7&&settings_return==7);
    capture("display-mode-save-failed");preview_preferences_save_ok=true;
    workbench_return(NULL);assert(current_page==7&&display_choice()==1);
    page(9);settings_select(2);lv_obj_send_event(display_choices[0],LV_EVENT_CLICKED,NULL);
    assert(preview_saved_preferences.selected==-1&&preview_saved_preferences.cycle);
    preview_preferences_save_ok=false;lv_dropdown_set_selected(remote_interval,3);lv_obj_send_event(remote_interval,LV_EVENT_VALUE_CHANGED,NULL);
    assert(prefs.interval==15&&lv_dropdown_get_selected(remote_interval)==1&&preview_saved_preferences.interval==15);
    lv_obj_remove_state(cycle_checks[0],LV_STATE_CHECKED);lv_obj_send_event(cycle_checks[0],LV_EVENT_VALUE_CHANGED,NULL);
    assert(prefs.enabled[0]&&lv_obj_has_state(cycle_checks[0],LV_STATE_CHECKED)&&preview_saved_preferences.enabled[0]);
    lv_obj_t *up=NULL;
    for(unsigned i=0;i<lv_obj_get_child_count(display_cycle_options);i++) {
        lv_obj_t *child=lv_obj_get_child(display_cycle_options,i);if(lv_obj_check_type(child,&lv_button_class)){up=child;break;}
    }
    assert(up);lv_obj_send_event(up,LV_EVENT_CLICKED,NULL);assert(prefs.order[0]==0&&prefs.order[1]==1&&preview_saved_preferences.order[0]==0);
    preview_preferences_save_ok=true;
    ui_preferences only=prefs;for(int i=0;i<8;i++)only.enabled[i]=i==0;assert(display_preferences_commit(&only));
    unsigned attempts=preview_preferences_saves;
    lv_obj_remove_state(cycle_checks[0],LV_STATE_CHECKED);lv_obj_send_event(cycle_checks[0],LV_EVENT_VALUE_CHANGED,NULL);
    assert(prefs.enabled[0]&&lv_obj_has_state(cycle_checks[0],LV_STATE_CHECKED)&&preview_preferences_saves==attempts);
    // Old empty configurations can still enter cycling with the first ordered page.
    prefs.cycle=false;prefs.selected=-1;for(int i=0;i<8;i++)prefs.enabled[i]=0;
    display_controls_refresh();lv_obj_send_event(display_choices[0],LV_EVENT_CLICKED,NULL);assert(prefs.cycle&&prefs.enabled[prefs.order[0]]);
    assert(clock_select_style(original_style));prefs=original;display_controls_refresh();
    preview_now_us=original_now;alert_until=original_alert;settings_return=original_return;
    page(9);settings_select(2);lv_indev_delete(touch);
    puts("DISPLAY_MODES_UI_OK three_exclusive_pointer_choices conditional_settings keep_cycle_selection_and_interval stay_in_settings fixed_on_back auto_music clock_style_keeps_mode save_failure_rollback last_page_guard legacy_empty_cycle");
}
#if TAB5_LOCAL_ART
static void check_annual_long_reader_ui(const char *path) {
    FILE *file=fopen(path,"rb");assert(file);fseek(file,0,SEEK_END);long size=ftell(file);rewind(file);
    char *json=calloc(1,(size_t)size+1);assert(json&&fread(json,1,(size_t)size,file)==(size_t)size);fclose(file);
    cJSON *rows=cJSON_Parse(json);free(json);assert(cJSON_IsArray(rows)&&cJSON_GetArraySize(rows)==7);
    lv_indev_t *touch=lv_indev_create();lv_indev_set_type(touch,LV_INDEV_TYPE_POINTER);lv_indev_set_display(touch,lv_display_get_default());lv_indev_set_read_cb(touch,camera_touch_read);
    saver=true;saver_style=7;visible(saver_layer,true);visible(gallery_layer,true);preview_gallery_ready=false;
    for(int slot=2;slot<4;slot++) {
        gallery_cache[slot]=(gallery_image){.pixels=calloc(1,1280*720*2),.category=1,.frame=0,.frames=1,.portrait=slot==3};assert(gallery_cache[slot].pixels);
        gallery_descriptor[slot]=(lv_image_dsc_t){.header={.magic=LV_IMAGE_HEADER_MAGIC,.cf=LV_COLOR_FORMAT_RGB565,.w=1280,.h=720,.stride=2560},.data_size=1280*720*2,.data=gallery_cache[slot].pixels};
    }
    const int directions[]={0,2,1,-1},angles[]={0,1800,900,2700};
    for(int index=0;index<7;index++) {
        cJSON *row=cJSON_GetArrayItem(rows,index),*guide=item(row,"guide"),*pages=item(row,"pages");memset(&preview_guide,0,sizeof(preview_guide));
        snprintf(preview_guide.date,sizeof(preview_guide.date),"%s",text(row,"date"));
        snprintf(preview_guide.work_id,sizeof(preview_guide.work_id),"%s",text(guide,"workId"));
#define ANNUAL_FIELD(name) snprintf(preview_guide.name,sizeof(preview_guide.name),"%s",text(guide,#name))
        ANNUAL_FIELD(title);ANNUAL_FIELD(author);ANNUAL_FIELD(period);ANNUAL_FIELD(museum);ANNUAL_FIELD(overview);ANNUAL_FIELD(looking);ANNUAL_FIELD(transcription);ANNUAL_FIELD(explanation);
#undef ANNUAL_FIELD
        preview_guide.transcription_page_count=cJSON_GetArraySize(pages);assert(preview_guide.transcription_page_count>1&&preview_guide.transcription_page_count<=32);
        for(int page=0;page<preview_guide.transcription_page_count;page++)preview_guide.transcription_pages[page]=(char *)text(cJSON_GetArrayItem(pages,page),"text");
        for(int slot=2;slot<4;slot++){strcpy(gallery_cache[slot].date,preview_guide.date);strcpy(gallery_cache[slot].work_id,preview_guide.work_id);}
        for(int pose=0;pose<4;pose++) {
            gallery_orientation=directions[pose];gallery_show();lv_obj_send_event(gallery_details_button,LV_EVENT_CLICKED,NULL);assert(gallery_details_open);
            for(int page=0;page<preview_guide.transcription_page_count;page++)assert(!strcmp(gallery_reading->transcription_pages[page],preview_guide.transcription_pages[page]));
            assert(lv_obj_get_style_transform_rotation(gallery_reader,0)==angles[pose]);
            lv_obj_update_layout(lv_screen_active());lv_refr_now(NULL);
            gallery_pointer_click(touch,gallery_physical_point(440,lv_obj_get_y(gallery_chapters[2])+28,angles[pose]));assert(gallery_reading_chapter==2&&gallery_reading_page==0);
            // Model pack/cache replacement while the old article stays open.
            char identity[80];strcpy(identity,preview_guide.work_id);strcpy(preview_guide.work_id,"replacement-work");
            for(int page=0;page<preview_guide.transcription_page_count;page++) {
                const char *part=preview_guide.transcription_pages[page];
                assert(gallery_reading_page==page&&!strncmp(lv_label_get_text(gallery_reader_body),part,strlen(part)));
                lv_obj_update_layout(gallery_reader);assert(lv_obj_get_height(gallery_reader_copy)>150);
                assert(lv_obj_get_x(gallery_text_next)+lv_obj_get_width(gallery_text_next)<lv_obj_get_x(gallery_reader_back));
                if((pose==0||pose==2)&&(page==0||page+1==preview_guide.transcription_page_count)) {
                    if(page)lv_obj_scroll_to_y(gallery_reader_copy,LV_COORD_MAX,LV_ANIM_OFF);
                    char name[96];snprintf(name,sizeof(name),"annual-long-%d-%s-%s",index,pose==2?"portrait":"landscape",page?"end":"start");capture(name);
                }
                if(page+1<preview_guide.transcription_page_count) {
                    gallery_pointer_click(touch,gallery_physical_point(374,lv_obj_get_y(gallery_text_next)+28,angles[pose]));
                }
            }
            assert(lv_obj_has_state(gallery_text_next,LV_STATE_DISABLED));
            gallery_pointer_click(touch,gallery_physical_point(114,lv_obj_get_y(gallery_text_previous)+28,angles[pose]));assert(gallery_reading_page==preview_guide.transcription_page_count-2);
            strcpy(preview_guide.work_id,identity);
            gallery_pointer_click(touch,gallery_physical_point((pose>=2?720:1280)-138,(pose>=2?1280:720)-50,angles[pose]));assert(!gallery_details_open&&gallery_frame[1]==0);
            for(int page=0;page<32;page++)assert(!gallery_reading->transcription_pages[page]);
        }
    }
    memset(&preview_guide,0,sizeof(preview_guide));cJSON_Delete(rows);lv_indev_delete(touch);
    puts("ANNUAL_LONG_READER_UI_OK 7 real packaged texts; all segments; four orientations; transformed touch next/previous/return; bounded nonoverlapping controls; first/end landscape/portrait captures; no hardware claim");
}
#endif
#include "ota_remote_ui_test.inc"
#include "runtime_health_test.h"
#include "agenda_ui_test.h"
static unsigned computer_preview_damage_count;
static void computer_preview_damage(lv_event_t *event){(void)event;computer_preview_damage_count++;}
static void photo_fixture_close_camera(void) {
    bool clicked=false;
    for(unsigned i=0;i<lv_obj_get_child_count(camera_layer);i++) {
        lv_obj_t *b=lv_obj_get_child(camera_layer,i),*caption=lv_obj_get_child(b,0);
        if(caption&&lv_obj_check_type(caption,&lv_label_class)&&!strcmp(lv_label_get_text(caption),"关闭")) {
            lv_obj_send_event(b,LV_EVENT_CLICKED,NULL);clicked=true;break;
        }
    }
    assert(clicked&&!photo_flow&&lv_obj_has_flag(camera_layer,LV_OBJ_FLAG_HIDDEN));
}
static void photo_feedback_present(void) {
    assert(photo_camera_processing&&photo_step==5&&!photo_camera_feedback_drawn);
    int prepares=preview_photo_prepares;unsigned uploads=preview_photo_uploads;
    photo_tick(); // Work must not start before an actual LVGL refresh completes.
    assert(photo_step==5&&preview_photo_prepares==prepares&&preview_photo_uploads==uploads);
    lv_refr_now(lv_display_get_default());
    assert(photo_camera_feedback_drawn);
    photo_tick();
}
static void check_photo_add_transition(void) {
    uint16_t *pixels=malloc(1280*720*2);assert(pixels);
    preview_photo_fixture=true;
    unsigned uploads=preview_photo_uploads,starts=preview_camera_starts;
    int prepares=preview_photo_prepares;
    const int poses[]={0,2,1,-1};
    for(unsigned i=0;i<4;i++) {
        int pose=poses[i];unsigned w=camera_pose_width(pose),h=camera_pose_height(pose);
        for(unsigned y=0;y<h;y++)for(unsigned x=0;x<w;x++)pixels[y*w+x]=(uint16_t)((x<w/2?0x4000:0x0010)|(y<h/2?0x0600:0));
        photo_flow=photo_for_codex=true;preview_camera_state=CAMERA_PREVIEW_CAPTURED;
        visible(camera_layer,true);lv_obj_move_foreground(camera_layer);
        lv_canvas_set_buffer(camera_canvas,pixels,w,h,LV_COLOR_FORMAT_RGB565);
        assert(camera_orientation_apply(pose));camera_tick();
        lv_display_rotation_t rotation=lv_display_get_rotation(NULL);
        const uint8_t *before=lv_canvas_get_draw_buf(camera_canvas)->data;
        lv_obj_send_event(camera_use,LV_EVENT_CLICKED,NULL);photo_feedback_present();prepares++;
        assert(photo_camera_processing&&photo_step==1&&preview_photo_prepares==prepares);
        for(int n=0;n<3;n++) {
            camera_tick();photo_tick();clock_tick(NULL);
            lv_obj_send_event(camera_use,LV_EVENT_CLICKED,NULL);
            lv_obj_send_event(camera_retry,LV_EVENT_CLICKED,NULL);
            lv_obj_send_event(camera_close,LV_EVENT_CLICKED,NULL);
            assert(photo_camera_processing&&preview_photo_prepares==prepares&&preview_camera_starts==starts);
            assert(lv_display_get_rotation(NULL)==rotation);
            assert(lv_canvas_get_draw_buf(camera_canvas)->data==before);
            assert(!lv_obj_has_flag(camera_layer,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(photo_window,LV_OBJ_FLAG_HIDDEN));
            assert(lv_obj_has_state(camera_use,LV_STATE_DISABLED)&&lv_obj_has_state(camera_retry,LV_STATE_DISABLED));
        }
        char name[64];snprintf(name,sizeof(name),"photo-processing-pose-%d",pose);capture(name);
        // Both success and error restore the ordinary page before it is shown.
        last_received=-100000;preview_photo_state=i%2?3:2;photo_tick();
        assert(!photo_camera_processing&&photo_step==2);
        assert(lv_display_get_horizontal_resolution(NULL)==1280&&lv_display_get_vertical_resolution(NULL)==720);
        assert(lv_obj_has_flag(camera_layer,LV_OBJ_FLAG_HIDDEN)&&!lv_obj_has_flag(photo_window,LV_OBJ_FLAG_HIDDEN));
        assert(preview_photo_uploads==uploads);
        if(i%2) {
            lv_obj_send_event(photo_confirm,LV_EVENT_CLICKED,NULL);photo_feedback_present();prepares++;
            assert(photo_camera_processing&&preview_photo_prepares==prepares);
            assert(lv_obj_has_flag(camera_layer,LV_OBJ_FLAG_HIDDEN)&&!lv_obj_has_flag(photo_window,LV_OBJ_FLAG_HIDDEN));
            preview_photo_state=2;photo_tick();assert(!photo_camera_processing&&photo_step==2);
        }
        if(i==3)capture("photo-processing-return");
        photo_finish();assert(!photo_camera_processing&&!photo_flow);
    }
    lv_image_set_src(camera_canvas,NULL);free(pixels);preview_photo_fixture=false;
    preview_photo_prepares=prepares-6;camera_preview_stop();
    puts("PHOTO_ADD_TRANSITION_OK four poses; retained still; no premature page/rotation; duplicate controls blocked; encoder retry; normal geometry before reveal; no upload or send");
}
static uint16_t *photo_stable_pixels;
static lv_area_t photo_control_areas[4];
static unsigned photo_delta_flushes,photo_delta_pixels,photo_outside_changes;
static void photo_delta_flush(lv_display_t *display,const lv_area_t *area,uint8_t *pixels) {
    unsigned width=lv_display_get_horizontal_resolution(display),height=lv_display_get_vertical_resolution(display);
    assert(area->x1>=0&&area->y1>=0&&area->x2<(int)width&&area->y2<(int)height);
    unsigned stride=lv_area_get_width(area);photo_delta_flushes++;photo_delta_pixels+=lv_area_get_size(area);
    for(int y=area->y1;y<=area->y2;y++)for(int x=area->x1;x<=area->x2;x++) {
        uint16_t color=((uint16_t *)pixels)[(y-area->y1)*stride+x-area->x1];
        bool control=false;
        for(unsigned i=0;i<4;i++){lv_area_t *a=&photo_control_areas[i];control|=x>=a->x1&&x<=a->x2&&y>=a->y1&&y<=a->y2;}
        if(!control&&color!=photo_stable_pixels[y*width+x])photo_outside_changes++;
    }
    lv_display_flush_ready(display);
}
static void photo_delta_begin(void) {
    lv_display_t *display=lv_display_get_default();lv_refr_now(display);
    photo_stable_pixels=malloc(sizeof(preview_buffer));assert(photo_stable_pixels);
    memcpy(photo_stable_pixels,preview_buffer,sizeof(preview_buffer));
    lv_obj_t *controls[]={camera_close,camera_retry,camera_use,camera_hint};
    // Reserve exactly the feedback badge area before it becomes visible.
    ui_text(camera_hint,"正在添加照片…");lv_obj_update_layout(camera_layer);
    for(unsigned i=0;i<4;i++){lv_obj_get_coords(controls[i],&photo_control_areas[i]);lv_area_increase(&photo_control_areas[i],4,4);}
    lv_display_set_flush_cb(display,photo_delta_flush);
    lv_display_set_buffers(display,preview_buffer,NULL,TAB5_DRAW_BUFFER_PIXELS*2,LV_DISPLAY_RENDER_MODE_PARTIAL);
    lv_refr_now(display);photo_delta_flushes=photo_delta_pixels=photo_outside_changes=0;
}
static void photo_delta_check(int pose,const char *stage) {
    lv_refr_now(NULL);
    printf("PHOTO_PARTIAL_STAGE pose=%d stage=%s flushes=%u pixels=%u changed_photo_pixels=%u\n",pose,stage,photo_delta_flushes,photo_delta_pixels,photo_outside_changes);
    assert(photo_outside_changes==0);
    photo_delta_flushes=photo_delta_pixels=photo_outside_changes=0;
}
static void photo_delta_end(void) {
    free(photo_stable_pixels);photo_stable_pixels=NULL;
    lv_display_t *display=lv_display_get_default();
    lv_display_set_flush_cb(display,preview_flush);
    lv_display_set_buffers(display,preview_buffer,NULL,sizeof(preview_buffer),LV_DISPLAY_RENDER_MODE_FULL);
}
static void check_photo_upload_transition(void) {
    // A completed codec is not a completed attachment. Keep the still visible
    // while the host acknowledges it, then reveal the composer only once.
    preview_photo_fixture=true;last_received=esp_timer_get_time()/1000;
    shown=cJSON_Parse("{\"pcInput\":{\"session\":\"photo-fixture\"},\"epochMilliseconds\":1700000000000}");assert(shown);
    snprintf(codex_selected,sizeof(codex_selected),"24800000-0000-4000-8000-000000000001");
    snprintf(photo_task,sizeof(photo_task),"%s",codex_selected);
    codex_draft *d=codex_draft_for(codex_selected,true);assert(d);d->loaded=true;
    unsigned uploads=preview_photo_uploads;int submits=preview_submit_count;
    uint16_t *pixels=calloc(1280*720,2);assert(pixels);
    const int poses[]={0,2,1,-1};
    for(unsigned i=0;i<4;i++) {
        for(unsigned n=0;n<1280*720;n++)pixels[n]=(uint16_t)(0x0841+(n/1280%8)*0x0100);
        photo_flow=photo_for_codex=true;preview_camera_state=CAMERA_PREVIEW_CAPTURED;
        visible(camera_layer,true);lv_obj_move_foreground(camera_layer);
        lv_canvas_set_buffer(camera_canvas,pixels,camera_pose_width(poses[i]),camera_pose_height(poses[i]),LV_COLOR_FORMAT_RGB565);
        assert(camera_orientation_apply(poses[i]));camera_tick();
        lv_display_rotation_t rotation=lv_display_get_rotation(NULL);
        photo_delta_begin();
        lv_obj_send_event(camera_use,LV_EVENT_CLICKED,NULL);photo_feedback_present();
        photo_delta_check(poses[i],"encode");
        preview_photo_state=2;preview_result_code=-1;photo_tick();uploads++;
        photo_delta_check(poses[i],"upload");
        assert(photo_step==3&&preview_photo_uploads==uploads);
        for(int n=0;n<4;n++) {
            photo_tick();camera_tick();clock_tick(NULL);
            assert(photo_camera_processing&&!lv_obj_has_flag(camera_layer,LV_OBJ_FLAG_HIDDEN));
            assert(lv_obj_has_flag(photo_window,LV_OBJ_FLAG_HIDDEN)&&lv_display_get_rotation(NULL)==rotation);
            assert(preview_photo_uploads==uploads);
        }
        photo_delta_check(poses[i],"pending");photo_delta_end();
        if(i==2)capture("photo-upload-keeps-portrait");
        if(i==1) {
            preview_result_code=500;preview_result_text="添加失败，照片已保留";photo_tick();
            assert(!photo_camera_processing&&photo_step==2&&!lv_obj_has_flag(photo_window,LV_OBJ_FLAG_HIDDEN));
            assert(preview_photo_state==2&&lv_display_get_horizontal_resolution(NULL)==1280);
            photo_tick();assert(preview_photo_uploads==uploads); // no automatic retry
            lv_obj_send_event(photo_confirm,LV_EVENT_CLICKED,NULL);photo_feedback_present();uploads++;
            assert(preview_photo_uploads==uploads&&photo_step==3);
        }
        preview_result_code=200;preview_result_text="照片已添加";photo_tick();
        assert(!photo_flow&&!photo_camera_processing&&lv_obj_has_flag(photo_window,LV_OBJ_FLAG_HIDDEN));
        assert(lv_obj_has_flag(camera_layer,LV_OBJ_FLAG_HIDDEN)&&lv_display_get_horizontal_resolution(NULL)==1280);
        assert(d->images[0][0]&&d->thumbnails[0]&&preview_submit_count==submits);
        assert(!strcmp(lv_label_get_text(codex_receipt),"已添加 1/3 张"));
        if(i==0){page(8);capture("photo-added-success");}
        lv_obj_send_event(composer_remove[0],LV_EVENT_CLICKED,NULL);assert(!d->images[0][0]);
    }
    // Complete three distinct attachments without sending; count and full-limit
    // feedback must follow the confirmed draft, not merely a completed encoder.
    for(int slot=0;slot<3;slot++) {
        photo_flow=photo_for_codex=true;preview_camera_state=CAMERA_PREVIEW_CAPTURED;
        visible(camera_layer,true);lv_obj_move_foreground(camera_layer);
        lv_canvas_set_buffer(camera_canvas,pixels,1280,720,LV_COLOR_FORMAT_RGB565);
        assert(camera_orientation_apply(0));camera_tick();last_received=esp_timer_get_time()/1000;
        photo_id[0]=0;photo_camera_use();photo_feedback_present();
        preview_photo_state=2;preview_result_code=-1;photo_tick();
        assert(photo_step==3&&!d->images[slot][0]);
        preview_result_code=200;preview_result_text="照片已添加";photo_tick();
        char expected[128];snprintf(expected,sizeof(expected),"已添加 %d/3 张",slot+1);
        assert(!photo_flow&&d->images[slot][0]&&!strcmp(lv_label_get_text(codex_receipt),expected));
    }
    unsigned starts=preview_camera_starts;photo_open(NULL);
    assert(!photo_flow&&preview_camera_starts==starts&&strstr(lv_label_get_text(codex_receipt),"最多添加 3 张"));
    assert(preview_submit_count==submits);
    for(int slot=2;slot>=0;slot--)lv_obj_send_event(composer_remove[slot],LV_EVENT_CLICKED,NULL);
    lv_image_set_src(camera_canvas,NULL);free(pixels);preview_photo_fixture=false;camera_preview_stop();
    puts("PHOTO_UPLOAD_TRANSITION_OK four poses; stable still through pending ack; one successful page return; failure retains photo; explicit retry only; no conversation send");
}
static void capture_actual_release_notes(void) {
    const char *saved_notes=preview_ota_notes;
    const char *release_notes_path=getenv("TAB5_RELEASE_NOTES_JSON");
    if(release_notes_path&&release_notes_path[0]) {
        FILE *f=fopen(release_notes_path,"rb");assert(f);
        fseek(f,0,SEEK_END);long size=ftell(f);assert(size>0&&size<=8192);rewind(f);
        char *raw=calloc(1,size+1);assert(raw);assert(fread(raw,1,size,f)==size);fclose(f);
        cJSON *release=cJSON_Parse(raw);assert(release);free(raw);
        cJSON *notes=cJSON_GetObjectItemCaseSensitive(release,"notes"),*version=cJSON_GetObjectItemCaseSensitive(release,"version");
        assert(cJSON_IsString(notes)&&cJSON_IsString(version)&&strlen(notes->valuestring)<=1024);
        preview_ota_notes=notes->valuestring;tick(NULL);visible(save_status,false);
        ui_text_fmt(ota_offer_title,"%s · 有新固件可用",version->valuestring);
        capture("ota-release-notes-actual");
        assert(lv_obj_get_width(ota_badge)==26&&lv_obj_get_height(ota_badge)==26);
        capture("proposal-upgrade-arrow");
        lv_obj_scroll_to_y(ota_notes_box,999,LV_ANIM_OFF);capture("ota-release-notes-actual-scrolled");
        preview_ota_notes=saved_notes;tick(NULL);cJSON_Delete(release);
        puts("OTA_ACTUAL_SIDECAR_PREVIEW_OK");
    }
}

static lv_obj_t *preview_network_button(lv_obj_t *parent,const char *caption) {
    for(unsigned i=0;i<lv_obj_get_child_count(parent);i++) {
        lv_obj_t *child=lv_obj_get_child(parent,i),*text=lv_obj_get_child(child,0);
        if(lv_obj_check_type(child,&lv_button_class)&&text&&lv_obj_check_type(text,&lv_label_class)&&!strcmp(lv_label_get_text(text),caption))return child;
    }
    return NULL;
}
static void preview_network_click(lv_indev_t *touch,lv_obj_t *button) {
    lv_obj_update_layout(lv_screen_active());lv_area_t area;lv_obj_get_coords(button,&area);
    gallery_pointer_click(touch,(lv_point_t){(area.x1+area.x2)/2,(area.y1+area.y2)/2});
}
static void preview_network_workflow(void) {
    page(9);settings_select(0);lv_obj_t *open_network=NULL,*settings_parent=settings_panels[0];
    for(unsigned i=0;i<lv_obj_get_child_count(settings_parent);i++) {
        lv_obj_t *candidate=lv_obj_get_child(settings_parent,i),*title=lv_obj_get_child(candidate,0);
        if(title&&lv_obj_check_type(title,&lv_label_class)&&!strcmp(lv_label_get_text(title),"管理连接")){open_network=candidate;break;}
    }
    assert(open_network);
    lv_indev_t *touch=lv_indev_create();lv_indev_set_type(touch,LV_INDEV_TYPE_POINTER);
    lv_indev_set_display(touch,lv_display_get_default());lv_indev_set_read_cb(touch,camera_touch_read);
    preview_network_click(touch,open_network);
    link_state_init(&device_links);last_link=LINK_WIFI;last_received=esp_timer_get_time()/1000;device_links.received_ms[LINK_WIFI]=last_received;
    tick(NULL);capture("network-management");
    lv_obj_t *aligned[]={host_picker,host_rename,host_switch,network_picker,network_connect,network_forget};
    lv_area_t first_picker,second_picker,first_action,second_action;
    lv_obj_get_coords(host_picker,&first_picker);lv_obj_get_coords(network_picker,&second_picker);
    lv_obj_get_coords(host_switch,&first_action);lv_obj_get_coords(network_connect,&second_action);
    assert(first_picker.y1==second_picker.y1&&first_action.y1==second_action.y1);
    for(unsigned i=0;i<6;i++) {
        lv_area_t object,parent;lv_obj_get_coords(aligned[i],&object);lv_obj_get_coords(lv_obj_get_parent(aligned[i]),&parent);
        assert(object.x1>=parent.x1&&object.y1>=parent.y1&&object.x2<=parent.x2&&object.y2<=parent.y2);
    }
    lv_obj_t *details=preview_network_button(network_window,"连接诊断");assert(details);
    preview_network_click(touch,details);assert(!lv_obj_has_flag(network_diag_window,LV_OBJ_FLAG_HIDDEN));capture("network-diagnostics");
    const char *unrelated[]={"设备配对","Codex","语音"};
    for(unsigned i=0;i<lv_obj_get_child_count(network_diag_window);i++) {
        lv_obj_t *child=lv_obj_get_child(network_diag_window,i);
        if(lv_obj_check_type(child,&lv_label_class))for(unsigned j=0;j<3;j++)assert(strcmp(lv_label_get_text(child),unrelated[j]));
    }
    lv_area_t wifi_details,reconnect_button,feedback_area,diagnostic_bounds;
    lv_obj_get_coords(network_details,&wifi_details);lv_obj_get_coords(network_retry,&reconnect_button);
    lv_obj_get_coords(network_diag_feedback,&feedback_area);lv_obj_get_coords(network_diag_window,&diagnostic_bounds);
    assert(reconnect_button.y1>wifi_details.y2&&feedback_area.x2<reconnect_button.x1&&feedback_area.y2<diagnostic_bounds.y2);
    lv_obj_t *diagnostic_fields[]={network_diag_state,network_details,network_diag_name,network_diag_host,network_diag_ip};
    for(unsigned i=0;i<sizeof(diagnostic_fields)/sizeof(diagnostic_fields[0]);i++) {
        lv_area_t field,parent;lv_obj_get_coords(diagnostic_fields[i],&field);lv_obj_get_coords(lv_obj_get_parent(diagnostic_fields[i]),&parent);
        assert(field.x1>=parent.x1&&field.y1>=parent.y1&&field.x2<=parent.x2&&field.y2<=parent.y2);
    }
    for(int state=1;state<=5;state++) {
        preview_wifi_diagnostic_case=state;network_host_render();
        char shot[64];snprintf(shot,sizeof(shot),"network-diagnostics-state-%d",state);capture(shot);
        assert(!strstr(lv_label_get_text(network_details),"鉴权通过"));
        lv_point_t measured;
        lv_text_get_size(&measured,lv_label_get_text(network_details),&tab5_font_30,0,0,538,LV_TEXT_FLAG_NONE);
        assert(measured.y<=80);
    }
    preview_wifi_diagnostic_case=0;network_host_render();
    puts("NETWORK_DIAGNOSTIC_SCOPE_OK wifi_and_bridge_only_reconnect_and_feedback_bounds");
    lv_obj_t *back=preview_network_button(network_diag_window,"返回");assert(back);
    preview_network_click(touch,back);assert(lv_obj_has_flag(network_diag_window,LV_OBJ_FLAG_HIDDEN)&&!lv_obj_has_flag(network_window,LV_OBJ_FLAG_HIDDEN));
    assert(!lv_obj_has_flag(network_window,LV_OBJ_FLAG_HIDDEN));
    assert(lv_dropdown_get_option_count(host_picker)==2);
    lv_obj_t *switch_host=host_switch;
    assert(switch_host);bool original_usb=preview_usb_connected;link_state original_host_links=device_links;int64_t original_host_time=preview_now_us;
    preview_usb_connected=false;lv_dropdown_set_selected(host_picker,1);lv_obj_send_event(switch_host,LV_EVENT_CLICKED,NULL);
    assert(preview_host_selected==1);capture("network-host-connecting");
    assert(strstr(lv_label_get_text(host_current),"MacBook"));
    assert(strstr(lv_label_get_text(host_state),"正在连接")&&!strcmp(lv_label_get_text(host_symbol),LV_SYMBOL_REFRESH));
    assert(!strstr(lv_label_get_text(network_details),"鉴权通过"));
    // Wi-Fi association and the previous computer's shown snapshot cannot claim success.
    preview_now_us+=13000000;network_host_render();
    assert(preview_host_selected==1&&strstr(lv_label_get_text(host_state),"未连接")&&!strcmp(lv_label_get_text(host_symbol),LV_SYMBOL_CLOSE));
    capture("network-host-offline");
    device_links.mode=-1;device_links.received_ms[LINK_WIFI]=esp_timer_get_time()/1000;network_host_render();
    assert(strstr(lv_label_get_text(host_state),"已连接 · Wi-Fi")&&!strcmp(lv_label_get_text(host_symbol),LV_SYMBOL_OK));
    capture("network-host-mac");
    // A separate editor supports Chinese without borrowing the Codex composer.
    char draft_before_name[2001];snprintf(draft_before_name,sizeof(draft_before_name),"%s",lv_textarea_get_text(codex_input));
    lv_obj_send_event(host_rename,LV_EVENT_CLICKED,NULL);assert(!lv_obj_has_flag(host_editor,LV_OBJ_FLAG_HIDDEN));
    lv_textarea_set_text(host_editor_input,"");
    const char *spelling="nihao";for(const char *p=spelling;*p;p++){char key[2]={*p,0};assert(press_text(host_editor_keyboard,key));}
    assert(!strcmp(tab5_ime_pending_for(host_editor_ime),"nihao")&&!tab5_ime_pending()[0]);
    assert(press_text(lv_obj_get_child(host_editor_ime,2),"你好"));assert(!strcmp(lv_textarea_get_text(host_editor_input),"你好"));
    lv_textarea_set_text(host_editor_input,"工作 MacBook");capture("network-host-rename");
    lv_area_t editor_keys,editor_bounds;lv_obj_get_coords(host_editor_keyboard,&editor_keys);lv_obj_get_coords(host_editor,&editor_bounds);
    assert(editor_keys.y1>=editor_bounds.y1&&editor_keys.y2<editor_bounds.y2&&editor_keys.x2<editor_bounds.x2);
    lv_obj_send_event(host_editor_keyboard,LV_EVENT_READY,NULL);
    assert(lv_obj_has_flag(host_editor,LV_OBJ_FLAG_HIDDEN)&&!strcmp(lv_label_get_text(host_current),"工作 MacBook"));
    assert(strstr(lv_dropdown_get_options(host_picker),"工作 MacBook"));
    assert(!strcmp(draft_before_name,lv_textarea_get_text(codex_input))&&!tab5_ime_pending()[0]);
    lv_obj_send_event(host_rename,LV_EVENT_CLICKED,NULL);assert(press_text(host_editor_keyboard,"n"));
    assert(tab5_ime_pending_for(host_editor_ime)[0]);lv_obj_send_event(host_editor_keyboard,LV_EVENT_CANCEL,NULL);
    assert(!tab5_ime_pending_for(host_editor_ime)[0]&&!strcmp(preview_host_labels[1],"工作 MacBook"));
    lv_obj_send_event(host_rename,LV_EVENT_CLICKED,NULL);lv_textarea_set_text(host_editor_input,"新备注");preview_host_save_ok=false;
    lv_obj_send_event(host_editor_keyboard,LV_EVENT_READY,NULL);
    assert(!lv_obj_has_flag(host_editor,LV_OBJ_FLAG_HIDDEN)&&!strcmp(lv_textarea_get_text(host_editor_input),"新备注")&&!strcmp(preview_host_labels[1],"工作 MacBook"));
    capture("network-host-rename-failed");preview_host_save_ok=true;
    lv_obj_send_event(host_editor_keyboard,LV_EVENT_READY,NULL);assert(!strcmp(preview_host_labels[1],"新备注"));
    lv_obj_send_event(host_rename,LV_EVENT_CLICKED,NULL);lv_textarea_set_text(host_editor_input,"");lv_obj_send_event(host_editor_keyboard,LV_EVENT_READY,NULL);
    assert(!strcmp(preview_host_labels[1],"MacBook"));
    // Editing a different trusted computer cannot select it.
    lv_dropdown_set_selected(host_picker,0);lv_obj_send_event(host_rename,LV_EVENT_CLICKED,NULL);
    lv_textarea_set_text(host_editor_input,"公司 Windows");lv_obj_send_event(host_editor_keyboard,LV_EVENT_READY,NULL);
    assert(preview_host_selected==1&&!strcmp(preview_host_labels[0],"公司 Windows"));
    assert(!strcmp(lv_label_get_text(host_current),"MacBook"));
    preview_usb_connected=true;lv_dropdown_set_selected(host_picker,0);lv_obj_send_event(switch_host,LV_EVENT_CLICKED,NULL);
    assert(preview_host_selected==1&&strstr(lv_label_get_text(host_state),"USB")&&!strcmp(lv_label_get_text(host_symbol),LV_SYMBOL_WARNING));
    capture("network-host-usb-owned");
    photo_flow=true;lv_obj_send_event(switch_host,LV_EVENT_CLICKED,NULL);assert(strstr(lv_label_get_text(host_state),"请先结束"));photo_flow=false;
    // Authenticated USB takeover follows the actual owner and refreshes the picker.
    preview_host_selected=0;device_links.received_ms[LINK_USB]=esp_timer_get_time()/1000;network_host_render();
    assert(strstr(lv_label_get_text(host_state),"USB 接管")&&!strcmp(lv_label_get_text(host_current),"公司 Windows"));
    assert(lv_dropdown_get_selected(host_picker)==0&&!strcmp(lv_label_get_text(host_symbol),LV_SYMBOL_OK));capture("network-host-usb-takeover");
    memset(preview_host_labels[0],'W',63);preview_host_labels[0][63]=0;network_host_render();capture("network-host-long-name");
    assert(lv_obj_get_height(host_current)==39&&lv_label_get_long_mode(host_current)==LV_LABEL_LONG_DOT);
    strcpy(preview_host_labels[0],"Windows 笔记本");strcpy(preview_host_labels[1],"MacBook");
    preview_host_selected=0;preview_usb_connected=original_usb;device_links=original_host_links;preview_now_us=original_host_time;
    host_display_id[0]=0;network_refresh();network_host_render();
    puts("HOST_SELECTION_UI_OK immediate named waiting, stale/wireless association cannot confirm, offline retains target, authenticated success, USB block/takeover, Chinese aliases, editor cancel/failure and Codex isolation, long-name bounds");
    assert(wifi_profiles_set_label(&preview_networks,"办公室","单位访客网"));network_refresh();
    assert(strstr(lv_dropdown_get_options(network_picker),"单位访客网 · 办公室"));
    wifi_profile_store saved_profiles=preview_networks;
    for(int i=3;i<=10;i++){char name[33];snprintf(name,sizeof(name),"synthetic-network-%d",i);assert(wifi_profiles_put(&preview_networks,name,""));}
    network_refresh();assert(network_option_count==10&&lv_dropdown_get_option_count(network_picker)==10);
    lv_dropdown_set_selected(network_picker,9);assert(lv_dropdown_get_selected(network_picker)==9);
    capture("network-ten-saved");
    preview_networks=saved_profiles;network_refresh();
    puts("NETWORK_CAPACITY_UI_OK ten_options_tenth_selectable_original_profiles_restored");
    assert(strstr(lv_label_get_text(network_details),"鉴权通过"));
    // Cancellation, failed persistence, active/last deletion and disabled empty list.
    lv_dropdown_set_selected(network_picker,1);lv_obj_send_event(network_forget,LV_EVENT_CLICKED,NULL);
    assert(strstr(lv_label_get_text(network_confirm_text),"办公室")&&strstr(lv_label_get_text(network_confirm_text),"单位访客网"));
    preview_network_click(touch,host_rename);assert(lv_obj_has_flag(host_editor,LV_OBJ_FLAG_HIDDEN)&&!lv_obj_has_flag(network_confirm,LV_OBJ_FLAG_HIDDEN));capture("network-forget-confirm");
    lv_obj_send_event(network_confirm_no,LV_EVENT_CLICKED,NULL);assert(device_wifi_count()==2);
    lv_obj_send_event(network_forget,LV_EVENT_CLICKED,NULL);preview_forget_ok=false;
    lv_obj_send_event(network_confirm_yes,LV_EVENT_CLICKED,NULL);assert(device_wifi_count()==2);
    assert(strstr(lv_label_get_text(network_feedback),"删除未完成"));preview_forget_ok=true;
    lv_dropdown_set_selected(network_picker,1);lv_obj_send_event(network_forget,LV_EVENT_CLICKED,NULL);
    lv_obj_send_event(network_confirm_yes,LV_EVENT_CLICKED,NULL);assert(device_wifi_count()==1&&!strcmp(preview_config.ssid,"家庭网络"));
    assert(wifi_profiles_put(&preview_networks,"办公室",""));network_refresh();
    lv_obj_send_event(network_forget,LV_EVENT_CLICKED,NULL);lv_obj_send_event(network_confirm_yes,LV_EVENT_CLICKED,NULL);
    assert(device_wifi_count()==1&&!strcmp(preview_config.ssid,"办公室"));
    lv_obj_send_event(network_forget,LV_EVENT_CLICKED,NULL);assert(strstr(lv_label_get_text(network_confirm_text),"最后一个"));
    lv_obj_send_event(network_confirm_yes,LV_EVENT_CLICKED,NULL);assert(device_wifi_count()==0&&!preview_config.ssid[0]);
    assert(lv_obj_has_state(network_forget,LV_STATE_DISABLED)&&lv_obj_has_state(network_connect,LV_STATE_DISABLED));
    capture("network-forget-empty");
    assert(wifi_profiles_put(&preview_networks,"家庭网络","")&&wifi_profiles_put(&preview_networks,"办公室",""));
    strcpy(preview_config.ssid,"家庭网络");network_refresh();
    codex_voice_waiting=true;lv_obj_send_event(network_forget,LV_EVENT_CLICKED,NULL);
    assert(lv_obj_has_flag(network_confirm,LV_OBJ_FLAG_HIDDEN)&&device_wifi_count()==2);codex_voice_waiting=false;
    puts("NETWORK_FORGET_UI_OK confirmation_cancel_failure_active_last_busy");
    preview_network_click(touch,details);assert(!lv_obj_has_flag(network_diag_window,LV_OBJ_FLAG_HIDDEN));
    page(0);assert(lv_obj_has_flag(network_window,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(network_diag_window,LV_OBJ_FLAG_HIDDEN));
    lv_indev_delete(touch);puts("NETWORK_LAYOUT_UI_OK aligned_groups_compact_actions_pointer_detail_navigation_original_workflows");
}

static void check_camera_open_readiness(void) {
    page(8);
    snprintf(codex_selected,sizeof(codex_selected),"00000000-0000-4000-8000-000000000111");
    codex_draft *d=codex_draft_for(codex_selected,true);assert(d);
    assert(codex_store_text(d,"camera readiness fixture",true));codex_set_text(d->text);
    for(int i=0;i<3;i++)codex_uuid(d->images[i]);
    char retained[3][37];memcpy(retained,d->images,sizeof(retained));
    unsigned starts=preview_camera_starts;int submissions=preview_submit_count;
    codex_waiting=codex_voice_waiting=codex_send_queued=photo_flow=false;
    codex_reading=codex_syncing=codex_load_waiting=true;
    photo_open(NULL);
    assert(!photo_open_pending&&!photo_flow&&preview_camera_starts==starts);
    assert(strstr(lv_label_get_text(codex_receipt),"最多添加"));
    assert(!memcmp(retained,d->images,sizeof(retained))&&!strcmp(d->text,"camera readiness fixture"));
    d->images[2][0]=0;codex_load_waiting=false;
    photo_open(NULL);photo_open(NULL);
    assert(photo_flow&&!photo_open_pending&&preview_camera_starts==starts+1);
    assert(codex_reading&&codex_syncing); // Do not abandon or replay pending work.
    assert(!memcmp(retained,d->images,2*37)&&!d->images[2][0]);
    photo_step=2;photo_upload();
    assert(photo_step==2&&strstr(lv_label_get_text(photo_note),"照片已保留"));
    codex_reading=false;preview_result_code=200;
    strcpy(codex_sync_op,"draft-save");snprintf(codex_sync_task,sizeof(codex_sync_task),"%s",d->id);
    codex_sync_revision=d->revision;codex_sync_tick(esp_timer_get_time()/1000);
    assert(photo_flow&&!codex_syncing&&!d->dirty&&d->loaded);
    assert(!memcmp(retained,d->images,2*37));
    camera_preview_stop();photo_finish();
    d->loaded=false;d->dirty=false;strcpy(codex_sync_op,"draft-load");codex_syncing=true;
    photo_open(NULL);assert(photo_open_pending&&!photo_flow&&preview_camera_starts==starts+1);
    assert(strstr(lv_label_get_text(codex_receipt),"恢复当前草稿"));
    page(0);page(8);codex_syncing=false;photo_open_tick();
    assert(!photo_open_pending&&!photo_flow&&preview_camera_starts==starts+1);
    d->loaded=true;strcpy(d->request,"00000000-0000-4000-8000-000000000222");
    photo_open(NULL);assert(!photo_flow&&preview_camera_starts==starts+1);
    d->request[0]=0;
    assert(preview_submit_count==submissions&&!memcmp(retained,d->images,2*37));
    puts("CAMERA_READY_UI_OK full_limit_immediate_local_open_during_read_save_no_duplicate_upload_waits_mailbox_completed_save_drained_draft_retained_restore_gate_navigation_cancel_pending_send_blocked");
}

#include "voice_start_focused_test.h"

#include "voice_stop_touch_test.h"
#include <time.h>
#include "voice_refresh_test.h"

int main(void) {
#ifdef _WIN32
    // A failed offline test must return failure to the runner instead of
    // opening a modal Windows error dialog on the user's desktop.
    SetErrorMode(SEM_FAILCRITICALERRORS|SEM_NOGPFAULTERRORBOX);
    _set_abort_behavior(0,_WRITE_ABORT_MSG|_CALL_REPORTFAULT);
#endif
    setvbuf(stdout,NULL,_IONBF,0);
    ui_time_state steady={0};
    assert(ui_time_value(&steady,1000000,0,0)==1000000);
    // Changing heartbeat phases cannot repeat/skip the displayed second.
    for(int i=1;i<=60;i++) {
        int delay=(i*173)%900;
        assert(ui_time_value(&steady,1000000+i*1000,i*1000+delay,i*1000+delay)==1000000+i*1000+delay);
        assert(ui_time_value(&steady,1000000+i*1000,i*1000+delay,i*1000+950)==1000000+i*1000+950);
    }
    assert(ui_time_value(&steady,0,0,120000)==1120000); // disconnected local time keeps advancing
    assert(ui_time_value(&steady,1200000,120100,120100)==1200000); // deliberate large clock correction
    lv_init(); ui_start();
    if(getenv("TAB5_VOICE_REFRESH_ONLY")){check_voice_refresh();return 0;}
    if(getenv("TAB5_VOICE_STOP_TOUCH_ONLY")){check_voice_stop_touch();return 0;}
    if(getenv("TAB5_VOICE_START_ONLY")){unsigned before=preview_scratch_reclaims;check_voice_start_pending();assert(preview_scratch_reclaims>before);puts("PREVIEW_SCRATCH_FOREGROUND_RECLAIM_OK");return 0;}
    if(getenv("TAB5_CAMERA_READY_ONLY")){unsigned before=preview_scratch_reclaims;check_camera_open_readiness();assert(preview_scratch_reclaims>before);puts("PREVIEW_SCRATCH_FOREGROUND_RECLAIM_OK");return 0;}
    if(getenv("TAB5_PHOTO_TRANSITION_ONLY")){check_photo_add_transition();check_photo_upload_transition();return 0;}
    if(getenv("TAB5_ORIENTATION_ONLY")) {
        page(9);settings_select(1);lv_obj_update_layout(settings_scroll);
        assert(lv_dropdown_get_selected(orientation_selector)==0);
        lv_area_t left,right;lv_obj_get_coords(orientation_selector,&left);lv_obj_get_coords(sound_test,&right);
        assert(left.y1==right.y1&&left.y2==right.y2&&left.x2<right.x1);
        lv_obj_get_coords(remote_saver,&left);lv_obj_get_coords(saver_selector,&right);
        assert(left.y1==right.y1&&left.y2==right.y2&&left.x2<right.x1);
        lv_indev_t *orientation_touch=lv_indev_create();lv_indev_set_type(orientation_touch,LV_INDEV_TYPE_POINTER);
        lv_indev_set_display(orientation_touch,lv_display_get_default());lv_indev_set_read_cb(orientation_touch,camera_touch_read);
        camera_touch_point=(lv_point_t){10,5};camera_touch_state=LV_INDEV_STATE_PRESSED;lv_indev_read(orientation_touch);
        for(int i=0;i<=10;i++) {
            preview_now_us+=50000;preview_art_orientation=(art_orientation_state){.ready=true,.x=-16384,.sampled_ms=preview_now_us/1000};clock_tick(NULL);
            assert(screen_orientation.direction==0);
        }
        camera_touch_state=LV_INDEV_STATE_RELEASED;lv_indev_read(orientation_touch);
        capture("orientation-auto-settings");
        for(int i=0;i<=10;i++) {
            preview_now_us+=50000;
            preview_art_orientation=(art_orientation_state){.ready=true,.x=-16384,.sampled_ms=preview_now_us/1000};
            clock_tick(NULL);
        }
        assert(screen_orientation.direction==2&&lv_display_get_rotation(NULL)==LV_DISPLAY_ROTATION_180);
        assert(current_page==9&&settings_content==settings_panels[1]);
        lv_obj_get_coords(orientation_selector,&left);
        camera_touch_point=(lv_point_t){1279-(left.x1+left.x2)/2,719-(left.y1+left.y2)/2};
        camera_touch_state=LV_INDEV_STATE_PRESSED;lv_indev_read(orientation_touch);
        camera_touch_state=LV_INDEV_STATE_RELEASED;lv_indev_read(orientation_touch);
        assert(lv_dropdown_is_open(orientation_selector));lv_dropdown_close(orientation_selector);
        lv_indev_delete(orientation_touch);
        lv_dropdown_set_selected(orientation_selector,1);lv_obj_send_event(orientation_selector,LV_EVENT_VALUE_CHANGED,NULL);
        assert(screen_orientation_mode==2&&preview_saved_orientation==2);
        capture("orientation-locked-settings");
        preview_orientation_save_fail=true;
        lv_dropdown_set_selected(orientation_selector,0);lv_obj_send_event(orientation_selector,LV_EVENT_VALUE_CHANGED,NULL);
        assert(screen_orientation_mode==2&&lv_dropdown_get_selected(orientation_selector)==1);
        preview_orientation_save_fail=false;
        int previous_saver=saver_style;saver_style=6;saver_enter(false);
        assert(lv_display_get_rotation(NULL)==LV_DISPLAY_ROTATION_0);
        saver_leave();saver_style=previous_saver;
        assert(lv_display_get_rotation(NULL)==LV_DISPLAY_ROTATION_180&&screen_orientation_mode==2);
        lv_dropdown_set_selected(orientation_selector,0);lv_obj_send_event(orientation_selector,LV_EVENT_VALUE_CHANGED,NULL);
        assert(screen_orientation_mode==0&&preview_saved_orientation==0);
        uint16_t *direction_pixels=malloc(1280*720*2);assert(direction_pixels);
        lv_canvas_set_buffer(camera_canvas,direction_pixels,1280,720,LV_COLOR_FORMAT_RGB565);
        lv_obj_remove_flag(camera_layer,LV_OBJ_FLAG_HIDDEN);
        lv_obj_move_foreground(camera_layer);
        preview_camera_state=CAMERA_PREVIEW_LIVE;preview_camera_busy=true;camera_tick();
        int poses[]={0,2,1,-1};
        for(unsigned i=0;i<4;i++) {
            int pose=poses[i];camera_geometry g=camera_geometry_for(pose);
            // Simulate turning the sensor in front of an upright test scene.
            // The red top marker must stay at the viewer's top in all images.
            for(int y=0;y<720;y++)for(int x=0;x<1280;x++) {
                int wx=x,wy=y,w=1280;
                if(pose==2){wx=1279-x;wy=719-y;}
                if(pose==1){wx=719-y;wy=x;w=720;}
                if(pose==-1){wx=y;wy=1279-x;w=720;}
                direction_pixels[y*1280+x]=(wy<120&&wx>w/2-140&&wx<w/2+140)?0xf800:(wx<w/2?0x0560:0x001f);
            }
            preview_now_us+=600000;
            preview_art_orientation=(art_orientation_state){.ready=true,.orientation=pose,
                .x=pose==0?16384:pose==2?-16384:0,.y=pose==1?16384:pose==-1?-16384:0,.sampled_ms=preview_now_us/1000};
            lv_display_rotation_t previous=lv_display_get_rotation(NULL);clock_tick(NULL);
            assert(preview_camera_direction==pose&&lv_display_get_rotation(NULL)==previous);
            assert(preview_camera_orientation_handler(pose));
            assert(lv_display_get_rotation(NULL)==(lv_display_rotation_t)((4-pose)%4));
            assert(lv_obj_get_width(camera_layer)==g.width&&lv_obj_get_height(camera_layer)==g.height);
            assert(lv_obj_get_x(camera_take)==g.shutter_x&&lv_obj_get_y(camera_take)==g.shutter_y);
            // User's edge requirement, independent of the geometry helper.
            if(pose==2)assert(lv_obj_get_x(camera_take)>1000);
            if(pose==1)assert(lv_obj_get_y(camera_take)>900);
            if(pose==-1)assert(lv_obj_get_y(camera_take)<100);
            lv_obj_t *caption=lv_obj_get_child(camera_take,2),*circle=lv_obj_get_child(camera_take,1);
            assert(lv_obj_get_y(caption)>lv_obj_get_y(circle)+lv_obj_get_height(circle));
            assert(!lv_obj_has_flag(camera_close,LV_OBJ_FLAG_HIDDEN));
            lv_obj_update_layout(camera_layer);
            assert(lv_obj_get_x(camera_close)==g.width-176&&lv_obj_get_y(camera_close)==16);
            char name[64];snprintf(name,sizeof(name),"camera-four-way-%u",i);capture(name);
            lv_indev_t *touch=lv_indev_create();lv_indev_set_type(touch,LV_INDEV_TYPE_POINTER);
            lv_indev_set_display(touch,lv_display_get_default());lv_indev_set_read_cb(touch,camera_touch_read);
            int x=g.shutter_x+94,y=g.shutter_y+128;
            camera_touch_point=pose==1?(lv_point_t){1279-y,x}:pose==-1?(lv_point_t){y,719-x}:
                pose==2?(lv_point_t){1279-x,719-y}:(lv_point_t){x,y};
            camera_touch_state=LV_INDEV_STATE_PRESSED;lv_indev_read(touch);
            assert(preview_camera_state==CAMERA_PREVIEW_CAPTURING);
            assert(!preview_camera_orientation_handler((pose+2)%4));
            camera_touch_state=LV_INDEV_STATE_RELEASED;lv_indev_read(touch);lv_indev_delete(touch);
            preview_camera_state=CAMERA_PREVIEW_LIVE;camera_tick();
        }
        for(int y=0;y<1280;y++)for(int x=0;x<720;x++)
            direction_pixels[y*720+x]=(y<120&&x>220&&x<500)?0xf800:(x<360?0x0560:0x001f);
        lv_canvas_set_buffer(camera_canvas,direction_pixels,720,1280,LV_COLOR_FORMAT_RGB565);
        preview_camera_state=CAMERA_PREVIEW_CAPTURED;preview_camera_busy=false;
        assert(preview_camera_orientation_handler(1));camera_tick();
        assert(lv_canvas_get_draw_buf(camera_canvas)->data==(uint8_t *)direction_pixels);
        assert(lv_image_get_rotation(camera_canvas)==0);capture("camera-four-way-review");
        for(int y=0;y<1280;y++)for(int x=0;x<720;x++)assert(direction_pixels[y*720+x]==
            ((y<120&&x>220&&x<500)?0xf800:(x<360?0x0560:0x001f)));
        lv_image_set_src(camera_canvas,NULL);free(direction_pixels);
        preview_camera_state=CAMERA_PREVIEW_OFF;lv_obj_add_flag(camera_layer,LV_OBJ_FLAG_HIDDEN);
        for(int i=0;i<=10;i++) {
            preview_now_us+=50000;preview_art_orientation=(art_orientation_state){.ready=true,.x=16384,.sampled_ms=preview_now_us/1000};clock_tick(NULL);
        }
        assert(screen_orientation.direction==0&&lv_display_get_rotation(NULL)==LV_DISPLAY_ROTATION_0);
        saver_style=UI_SAVER_COUNTDOWN;date_settings_refresh();lv_obj_update_layout(settings_scroll);
        assert(lv_obj_get_y(screen_sound_group)>=lv_obj_get_y(date_settings_row)+lv_obj_get_height(date_settings_row));
        capture("orientation-countdown-settings");
        puts("CAMERA_DIRECTION_UI_OK four upright scene renders, lens-opposite shutter, caption below circle, four physical touch transforms; scanout/gesture fences; normalized portrait review and return to landscape");
        puts("ORIENTATION_UI_OK actual settings, flip, lock, failed save rollback, gallery entry/exit, unlock, countdown spacing");return 0;
    }
    assert(current_page==0&&startup_home.holding&&!startup_home.frame_seen);
    capture("startup-overview");
    puts("UI_STARTUP_OVERVIEW_ACTUAL_OK production UI opens overview before saved display policy resumes");
    if(getenv("TAB5_STARTUP_ONLY"))return 0;
    if(getenv("TAB5_AGENDA_FIXTURE")){check_agenda_ui(getenv("TAB5_AGENDA_FIXTURE"));return 0;}
#if TAB5_LOCAL_ART
    if(getenv("TAB5_GALLERY_CAPTION_ONLY")) {
        const char *fixtures=getenv("TAB5_GALLERY_FIXTURES");assert(fixtures&&getenv("TAB5_GALLERY_FOOTER_GUIDES"));
        preview_gallery_ready=false;
        for(int category=0;category<2;category++) {
            saver_style=6+category;saver_enter(false);
            for(int portrait=0;portrait<2;portrait++) {
                char path[2048];snprintf(path,sizeof(path),"%s/%s%s.rgb565",fixtures,category?"calligraphy":"painting",portrait?"-portrait":"");
                FILE *file=fopen(path,"rb");assert(file);int slot=category*2+portrait;
                uint8_t *pixels=malloc(1280*720*2);assert(pixels&&fread(pixels,1,1280*720*2,file)==1280*720*2&&fgetc(file)==EOF);fclose(file);
                // These matched production works each have one image. Keep
                // review screenshots faithful; multipage controls are covered
                // by the separate loaded-gallery and legacy reader cases.
                gallery_cache[slot]=(gallery_image){.pixels=pixels,.category=category,.portrait=portrait,.frames=1};
                strcpy(gallery_cache[slot].date,"2026-10-06");strcpy(gallery_cache[slot].work_id,"caption-fixture");
                gallery_descriptor[slot]=(lv_image_dsc_t){.header={.magic=LV_IMAGE_HEADER_MAGIC,.cf=LV_COLOR_FORMAT_RGB565,.w=1280,.h=720,.stride=2560},.data_size=1280*720*2,.data=pixels};
            }
            const char *rounds_text=getenv("TAB5_GALLERY_STABILITY_ROUNDS");
            unsigned rounds=rounds_text?(unsigned)strtoul(rounds_text,NULL,10):1;
            assert(rounds>=1&&rounds<=16);
            for(unsigned round=0;round<rounds;round++)check_gallery_footer(category);
            printf("GALLERY_STABILITY_ROUNDS_OK category=%d rounds=%u\n",category,rounds);
            lv_image_set_src(gallery_view,NULL);gallery_presented=(gallery_image){0};
            for(int slot=category*2;slot<category*2+2;slot++)gallery_drop(slot);
            free(gallery_take_buffer());
        }
        puts("GALLERY_CAPTION_ONLY_OK accepted image captions, compact portrait metadata, no landscape duplicate strip; four directions, transformed controls and exact reader return");return 0;
    }
    const char *annual_readers=getenv("TAB5_GALLERY_LONG_READERS");if(annual_readers){check_annual_long_reader_ui(annual_readers);return 0;}
#endif
    const char *health_fixtures=getenv("TAB5_RUNTIME_HEALTH_FIXTURES");
    if(health_fixtures){check_runtime_health_ui(health_fixtures);return 0;}
    if(getenv("TAB5_GALLERY_MOTION_ONLY")) {
        saver=true;visible(saver_layer,true);visible(gallery_layer,true);
        strcpy(preview_guide.date,"2026-10-06");strcpy(preview_guide.work_id,"motion-fixture");
        strcpy(preview_guide.title,"书画转场回放");strcpy(preview_guide.museum,"本地绘图预览");
        struct tm day={.tm_year=126,.tm_mon=9,.tm_mday=6};
        check_gallery_motion_budget(&day);puts("GALLERY_MOTION_FOCUSED_OK");return 0;
    }
    shown=cJSON_Parse("{\"epochUtc\":1790149800,\"utcOffsetSeconds\":28800,\"calendar\":{\"date\":\"2026-09-23\",\"lunar\":\"八月十三\"},\"claude\":{\"state\":\"working\",\"tokensToday\":18420},\"codex\":{\"state\":\"idle\",\"tokensToday\":96350},\"quotas\":{\"claude\":{\"primaryPercent\":24,\"weeklyPercent\":57},\"codex\":{\"primaryPercent\":36,\"weeklyPercent\":88}},\"domesticActivity\":{\"activeProvider\":\"DeepSeek\",\"state\":\"idle\",\"tokensToday\":7230},\"weather\":{\"city\":\"上海\",\"condition\":\"多云\",\"temperature\":26.5,\"high\":29,\"low\":21,\"humidity\":68,\"pm25\":19,\"airQualityIndex\":42,\"airQualityLabel\":\"优\",\"source\":\"天气服务\"},\"systemMetrics\":{\"cpuPercent\":34,\"memoryPercent\":80,\"uploadBytesPerSecond\":162816,\"downloadBytesPerSecond\":3952640,\"sampleSession\":\"preview\",\"sampleSequence\":480},\"music\":{\"title\":\"忽然之间\",\"artist\":\"阿虾\",\"album\":\"音乐专辑\",\"playing\":true,\"durationSeconds\":228,\"elapsedSeconds\":85}}");
    cJSON *details=cJSON_AddArrayToObject(shown,"quotaDetails");
    for(int i=0;i<UI_HOME_PROVIDERS;i++) cJSON_AddItemToArray(details,cJSON_Parse(i==10?"{\"id\":\"volcengine\",\"plan\":\"待接入\"}":"{\"name\":\"Codex\",\"plan\":\"Pro\",\"primary\":36,\"weekly\":88,\"primaryReset\":\"09-23 18:00\",\"weeklyReset\":\"09-26 12:00\",\"balance\":120,\"currency\":\"CNY\",\"credits\":2,\"tokensToday\":96350}"));
    cJSON *quotes=cJSON_AddArrayToObject(cJSON_AddObjectToObject(shown,"stocks"),"quotes");
    for(int i=0;i<12;i++) cJSON_AddItemToArray(quotes,cJSON_Parse(i%2?"{\"name\":\"贵州茅台\",\"code\":\"600519\",\"price\":\"1423.50\",\"changePercent\":\"-1.26%\"}":"{\"name\":\"上证指数\",\"code\":\"000001\",\"price\":\"3280.16\",\"changePercent\":\"+0.85%\"}"));
    cJSON *samples=cJSON_AddArrayToObject(item(shown,"systemMetrics"),"samples");
    for(int i=0;i<12;i++) {
        cJSON *quote=cJSON_GetArrayItem(quotes,i);
        cJSON_AddStringToObject(quote,"symbol",i%3==0?"usAAPL":i%3==1?"hkHSI":"sh000001");
        cJSON_ReplaceItemInObject(quote,"name",cJSON_CreateString(i%3==0?"苹果":i%3==1?"恒生指数":"上证指数"));
        cJSON_ReplaceItemInObject(quote,"code",cJSON_CreateString(i%3==0?"AAPL":i%3==1?"HSI":"000001"));
    }
    for(int i=0;i<480;i++) {
        // Two minutes of synthetic traffic with sub-second packet bursts.
        // The zero/high alternation must average out; sustained hills remain.
        int second=i/4,up_phase=second%38,down_phase=second%56;
        int up=24+3*(up_phase<19?up_phase:38-up_phase);
        int down=45+7*(down_phase<28?down_phase:56-down_phase);
        cJSON *v=cJSON_CreateObject();
        cJSON_AddNumberToObject(v,"upload",(i%2?2*up:0)*1024);
        cJSON_AddNumberToObject(v,"download",(i%2?2*down:0)*1024);
        cJSON_AddItemToArray(samples,v);
    }
    const char *trace_path=getenv("TAB5_NETWORK_TRACE");
    if(trace_path) {
        FILE *f=fopen(trace_path,"rb");assert(f);fseek(f,0,SEEK_END);long size=ftell(f);rewind(f);
        char *json=calloc(1,size+1);assert(json);assert(fread(json,1,size,f)==size);fclose(f);
        cJSON *trace=cJSON_Parse(json);free(json);assert(trace);
        cJSON_DeleteItemFromObject(shown,"systemMetrics");cJSON_AddItemToObject(shown,"systemMetrics",trace);
    }
    cJSON_AddItemToObject(shown,"assetIds",cJSON_Parse("[\"00000001\",\"00000002\",\"00000003\"]"));
    cJSON_AddBoolToObject(item(shown,"music"),"hasArtwork",true);
    cJSON *weather_data=item(shown,"weather");
    cJSON_ReplaceItemInObject(weather_data,"source",cJSON_CreateString("qweather"));
    cJSON_ReplaceItemInObject(weather_data,"city",cJSON_CreateString("南京 雨花台"));
    cJSON_AddNumberToObject(weather_data,"pressureHpa",1012);
    cJSON_AddStringToObject(weather_data,"pressureKind","station");
    cJSON_AddStringToObject(weather_data,"airQualityStandard","cn-mee");
    cJSON_AddStringToObject(item(shown,"calendar"),"solarTermLabel","今日秋分");
    cJSON_AddStringToObject(item(shown,"calendar"),"holidayLabel","工作日 · 距中秋节假期 2 天");
    cJSON_AddStringToObject(item(shown,"calendar"),"nextDate","2026-09-24");
    cJSON_AddStringToObject(item(shown,"calendar"),"nextLunar","农历八月十四");
    cJSON_AddStringToObject(item(shown,"calendar"),"nextSolarTermLabel","下个节气 寒露 10-08");
    cJSON_AddStringToObject(item(shown,"calendar"),"nextHolidayLabel","工作日 · 距中秋节假期 1 天");
    cJSON_AddItemToObject(item(shown,"calendar"),"detail",cJSON_Parse("{\"holidayYear\":2026,\"holidays\":[{\"start\":215,\"end\":223,\"name\":\"春节\"},{\"start\":925,\"end\":927,\"name\":\"中秋节\"},{\"start\":1001,\"end\":1007,\"name\":\"国庆节\"}],\"workdays\":[920,1010],\"terms\":[{\"year\":2026,\"dates\":[105,120,204,218,305,320,405,420,505,521,605,621,707,723,807,823,907,923,1008,1023,1107,1122,1207,1222]}],\"birthdays\":[{\"name\":\"家人\",\"month\":8,\"day\":15,\"lunar\":true,\"leap\":false,\"remind\":7},{\"name\":\"朋友\",\"month\":10,\"day\":1,\"lunar\":false,\"leap\":false,\"remind\":3}]}"));
    cJSON *codex=cJSON_GetArrayItem(details,1);
    for(int i=5;i<=6;i++) {
        cJSON *api=cJSON_GetArrayItem(details,i);
        cJSON_AddStringToObject(api,"billingMode","payg");
        cJSON_ReplaceItemInObject(api,"balance",cJSON_CreateNumber(i==5?75.37:96.72));
        if(i==6)cJSON_AddNumberToObject(api,"cost",3.27);
    }
    cJSON_DeleteItemFromObject(codex,"primary");
    cJSON_ReplaceItemInObject(codex,"credits",cJSON_CreateNumber(3));
    cJSON_AddItemToObject(codex,"creditExpiries",cJSON_Parse("[\"2026-10-04 11:26\",\"2026-10-05 13:38\",\"2026-10-23 00:27\"]"));
    cJSON_DeleteItemFromObject(item(item(shown,"quotas"),"codex"),"primaryPercent");
    cJSON_AddBoolToObject(item(item(shown,"quotas"),"codex"),"stale",true);
    cJSON_AddBoolToObject(codex,"stale",true);
    cJSON_AddItemToObject(shown,"quotaTrend",cJSON_Parse("{\"days\":[{\"label\":\"09-17\",\"used\":null},{\"label\":\"09-18\",\"used\":6.2},{\"label\":\"09-19\",\"used\":12.1},{\"label\":\"09-20\",\"used\":0},{\"label\":\"09-21\",\"used\":18.0},{\"label\":\"09-22\",\"used\":9.0,\"partial\":true},{\"label\":\"09-23\",\"used\":4.0,\"partial\":true}]}"));
    cJSON_AddItemToObject(shown,"pcInput",cJSON_Parse("{\"session\":\"preview-session\",\"sequence\":1}"));
    cJSON_AddItemToObject(shown,"codexTasks",cJSON_Parse("{\"projects\":[{\"id\":\"aaaaaaaaaaaaaaaa\",\"name\":\"M5Stack TAB5\"},{\"id\":\"bbbbbbbbbbbbbbbb\",\"name\":\"biaoshu\"}],\"tasks\":[{\"id\":\"01a0cc69-2782-7650-9903-303578a0296e\",\"title\":\"检查 TAB5 设备连接\",\"folder\":\"M5Stack TAB5\",\"projectId\":\"aaaaaaaaaaaaaaaa\"},{\"id\":\"01a0cc69-2782-7650-9903-303578a0296f\",\"title\":\"修复标书资料导入\",\"folder\":\"biaoshu\",\"projectId\":\"bbbbbbbbbbbbbbbb\"}],\"receipts\":{}}"));
    render(); clock_music(10000,true); images_tick(150);
    if(getenv("TAB5_RELEASE_NOTES_ONLY")) {
        page(9);settings_select(3);preview_ota_available=true;tick(NULL);
        capture_actual_release_notes();return 0;
    }
    if(getenv("TAB5_NETWORK_LAYOUT_ONLY")){preview_network_workflow();return 0;}
    if(getenv("TAB5_COMPUTER_CLICK_ONLY")) {
        #include "computer_click_queue_test.h"
    }
    if(getenv("TAB5_HOST_UNLOCK_ONLY")) {
        #include "host_unlock_ui_test.h"
    }
    if(getenv("TAB5_CALENDAR_LABEL_ONLY")) {
        calendar_set_today(2026,10,24);lv_obj_send_event(home_calendar_action,LV_EVENT_CLICKED,NULL);
        calendar_year=2026;calendar_month=10;calendar_selected=cal_serial(2026,10,10);calendar_render();
        const char *note=lv_label_get_text(calendar_day_note);
        printf("CALENDAR_MAKEUP_LABEL %s\n",note);capture("calendar-makeup-october");
        if(getenv("TAB5_CALENDAR_BEFORE"))return 0;
        assert(!strcmp(note,"调休补班"));
        int slot=-1;for(int i=0;i<42;i++)if(calendar_cell_dates[i]==calendar_selected)slot=i;
        assert(slot>=0&&!strcmp(lv_label_get_text(calendar_marks[slot]),"班"));
        calendar_selected=cal_serial(2026,10,1);calendar_render();
        assert(strstr(lv_label_get_text(calendar_day_note),"国庆节 · 假期休息")&&strstr(lv_label_get_text(calendar_day_note),"朋友的生日"));capture("calendar-national-holiday");
        calendar_selected=cal_serial(2026,10,11);calendar_render();
        assert(!strcmp(lv_label_get_text(calendar_day_note),"周末"));
        calendar_selected=cal_serial(2026,10,12);calendar_render();
        assert(!strcmp(lv_label_get_text(calendar_day_note),"工作日"));
        calendar_selected=cal_serial(2026,10,8);calendar_render();
        assert(!strcmp(lv_label_get_text(calendar_day_note),"工作日\n节气 · 寒露"));capture("calendar-solar-term");
        calendar_year=2027;calendar_month=1;calendar_selected=cal_serial(2027,1,5);
        cJSON_AddItemToArray(item(calendar_detail(),"terms"),cJSON_Parse("{\"year\":2027,\"dates\":[105,120,204,219,306,321,405,420,506,521,606,621,707,723,808,823,908,923,1008,1023,1107,1122,1207,1222]}"));
        calendar_render();assert(!strcmp(lv_label_get_text(calendar_day_note),"放假安排待更新\n节气 · 小寒"));capture("calendar-next-year");
        // Synthetic overlap verifies a distinct festival remains when its day is a makeup workday.
        cJSON_AddItemToObject(calendar_detail(),"holidayYears",cJSON_Parse("[{\"year\":2027,\"holidays\":[],\"workdays\":[101]}]"));
        calendar_selected=cal_serial(2027,1,1);calendar_render();
        assert(!strcmp(lv_label_get_text(calendar_day_note),"元旦 · 调休补班"));
        puts("CALENDAR_LABEL_OK makeup_marker_holiday_birthday_weekend_workday_solar_unknown_year_festival");return 0;
    }
    check_clock_faces();check_clock_solar();
    if(getenv("TAB5_CLOCK_ONLY"))return 0;
    check_countdowns();
    check_header_clock();
    if(getenv("TAB5_COUNTDOWN_ONLY"))return 0;
    // Rest is reserved for an explicitly idle session; unknown/offline/waiting
    // must not be presented as a completed working turn.
    cJSON *pet_codex=item(shown,"codex");
    const char *pet_states[]={"working","idle","offline","unknown"};
    const char *pet_expected[]={"正在工作","休息中，等待下一项任务","未运行","状态待确认"};
    for(int i=0;i<4;i++) {
        cJSON_ReplaceItemInObject(pet_codex,"state",cJSON_CreateString(pet_states[i]));render();
        assert(!strcmp(lv_label_get_text(pet_hint[1]),pet_expected[i]));
    }
    cJSON_AddBoolToObject(pet_codex,"needsInput",true);render();
    assert(!strcmp(lv_label_get_text(pet_hint[1]),"等待你的操作"));
    cJSON_DeleteItemFromObject(pet_codex,"needsInput");
    cJSON_ReplaceItemInObject(pet_codex,"state",cJSON_CreateString("idle"));render();
    puts("PET_STATUS_UI_OK working_idle_offline_unknown_waiting");
    const int overview_quota_ids[]={1,5,6};
    for(int i=0;i<3;i++){lv_obj_send_event(home_panels[i],LV_EVENT_CLICKED,NULL);assert(current_page==1&&selected_quota==overview_quota_ids[i]&&lv_dropdown_get_selected(quota_picker)==selected_quota);}
    lv_obj_send_event(home_weather_action,LV_EVENT_CLICKED,NULL);assert(current_page==2);workbench_return(NULL);assert(current_page==0);
    calendar_set_today(2026,9,23);lv_obj_send_event(home_calendar_action,LV_EVENT_CLICKED,NULL);assert(current_page==12);
    assert(strstr(lv_label_get_text(home_holiday),"家人")&&strstr(lv_label_get_text(home_holiday),"2 天"));
    capture("calendar-september");
    cJSON *calendar_birthdays=cJSON_DetachItemFromObject(calendar_detail(),"birthdays");
    calendar_selected=cal_serial(2026,9,28);calendar_render();
    assert(!strcmp(lv_label_get_text(calendar_upcoming),"无"));
    capture("calendar-empty-birthdays");
    cJSON_AddItemToObject(calendar_detail(),"birthdays",calendar_birthdays);
    calendar_selected=calendar_today_serial;calendar_render();
    for(int i=0;i<42;i++)assert(lv_obj_has_flag(calendar_cells[i],LV_OBJ_FLAG_HIDDEN)==(i>=35));
    int selected_slot=-1;for(int i=0;i<42;i++)if(calendar_cell_dates[i]==cal_serial(2026,9,25))selected_slot=i;
    assert(selected_slot>=0);lv_obj_send_event(calendar_cells[selected_slot],LV_EVENT_CLICKED,NULL);
    assert(strstr(lv_label_get_text(calendar_lunar_label),"八月十五")&&strstr(lv_label_get_text(calendar_day_note),"家人的生日"));
    assert(!strcmp(lv_label_get_text(calendar_marks[selected_slot]),"生"));
    lv_obj_send_event(calendar_next,LV_EVENT_CLICKED,NULL);assert(calendar_year==2026&&calendar_month==10);
    capture("calendar-october");
    calendar_year=2021;calendar_month=2;calendar_selected=cal_serial(2021,2,1);calendar_render();
    for(int i=0;i<42;i++)assert(lv_obj_has_flag(calendar_cells[i],LV_OBJ_FLAG_HIDDEN)==(i>=28));
    calendar_year=2026;calendar_month=3;calendar_selected=cal_serial(2026,3,1);calendar_render();
    for(int i=0;i<42;i++)assert(!lv_obj_has_flag(calendar_cells[i],LV_OBJ_FLAG_HIDDEN));
    int kind;assert(!strcmp(calendar_holiday(2026,1010,&kind),"调休补班")&&kind==2);
    calendar_year=2027;calendar_month=1;calendar_selected=cal_serial(2027,1,1);calendar_render();
    assert(strstr(lv_label_get_text(calendar_day_note),"放假安排待更新"));
    // Synthetic future-year protocol fixture, not a real holiday forecast.
    cJSON_AddItemToObject(calendar_detail(),"holidayYears",cJSON_Parse("[{\"year\":2027,\"holidays\":[{\"start\":101,\"end\":103,\"name\":\"测试假期\"}],\"workdays\":[104]}]"));
    calendar_render();assert(strstr(lv_label_get_text(calendar_day_note),"测试假期"));
    assert(!strcmp(calendar_holiday(2027,104,&kind),"调休补班")&&kind==2);
    assert(calendar_schedule(2026)!=NULL&&calendar_schedule(2028)==NULL);
    cJSON_DeleteItemFromObject(calendar_detail(),"holidayYears");
    calendar_year=2026;calendar_month=9;calendar_selected=calendar_today_serial;calendar_render();workbench_return(NULL);assert(current_page==0);
    selected_quota=1;lv_dropdown_set_selected(quota_picker,1);render_quota();
    // Capture urgency colors using synthetic quota values, then restore the fixture.
    cJSON *quota_color_fixture=cJSON_Duplicate(item(item(shown,"quotas"),"codex"),true);
    cJSON *quota_color_detail_fixture=cJSON_Duplicate(cJSON_GetArrayItem(item(shown,"quotaDetails"),1),true);
    const int quota_color_used[]={67,80,95,95};
    const char *quota_color_names[]={"normal","warning","critical","stale"};
    for(int i=0;i<4;i++) {
        cJSON *q=item(item(shown,"quotas"),"codex");
        cJSON_DeleteItemFromObject(q,"stale");cJSON_AddBoolToObject(q,"stale",i==3);
        cJSON_DeleteItemFromObject(q,"weeklyPercent");cJSON_AddNumberToObject(q,"weeklyPercent",quota_color_used[i]);
        cJSON *detail=cJSON_GetArrayItem(item(shown,"quotaDetails"),1);
        cJSON_DeleteItemFromObject(detail,"stale");cJSON_AddBoolToObject(detail,"stale",i==3);
        cJSON_DeleteItemFromObject(detail,"weekly");cJSON_AddNumberToObject(detail,"weekly",quota_color_used[i]);
        char name[64];render();page(0);snprintf(name,sizeof(name),"quota-color-%s-overview",quota_color_names[i]);capture(name);
        render_quota();page(1);snprintf(name,sizeof(name),"quota-color-%s-detail",quota_color_names[i]);capture(name);
    }
    cJSON_ReplaceItemInObject(item(shown,"quotas"),"codex",quota_color_fixture);
    cJSON_ReplaceItemInArray(item(shown,"quotaDetails"),1,quota_color_detail_fixture);
    codex=cJSON_GetArrayItem(item(shown,"quotaDetails"),1);render();page(0);
    puts("CALENDAR_UI_OK overview routes, lunar festival, birthday countdown, month navigation, official rest/work and unknown-year state");
    assert(!strcmp(lv_label_get_text(weather_facts[3]),"1012.0"));
    cJSON *forecast_hours=cJSON_AddArrayToObject(weather_data,"hourly"),*forecast_days=cJSON_AddArrayToObject(weather_data,"daily");
    for(int i=0;i<24;i++) {
        cJSON *h=cJSON_CreateObject();cJSON_AddItemToArray(forecast_hours,h);char stamp[40];snprintf(stamp,sizeof(stamp),"2026-09-%02dT%02d:00+08:00",28+(i+16)/24,(i+16)%24);
        cJSON_AddStringToObject(h,"time",stamp);cJSON_AddStringToObject(h,"condition",i%3?"多云":"小雨");cJSON_AddNumberToObject(h,"temperature",26-i%9);
        if(i!=1)cJSON_AddNumberToObject(h,"rainProbability",i%3?0:80);
    }
    for(int i=0;i<7;i++) {
        cJSON *d=cJSON_CreateObject();cJSON_AddItemToArray(forecast_days,d);char stamp[16];snprintf(stamp,sizeof(stamp),"2026-%02d-%02d",i<3?9:10,i<3?28+i:i-2);
        cJSON_AddStringToObject(d,"date",stamp);cJSON_AddStringToObject(d,"condition",i%3?"晴":"中雨");cJSON_AddNumberToObject(d,"low",19+i);cJSON_AddNumberToObject(d,"high",25+i);
        if(i%2)cJSON_AddNumberToObject(d,"rainProbability",20);else cJSON_AddNumberToObject(d,"precipitationMm",2.4);
    }
    page(2);weather_forecast_render(weather_data);capture("weather-current-forecast-tabs");
    const char *temperature_keys[]={"low","high","temperature"};cJSON *saved_temperatures[3];
    for(int i=0;i<3;i++)saved_temperatures[i]=cJSON_Duplicate(item(weather_data,temperature_keys[i]),true);
    const double temperature_cases[][3]={{-12,-2,-7},{4,4,4},{21,29,21},{21,29,29},{21,29,30},{29,21,26},{-101,29,26},{21,101,26}};
    for(unsigned n=0;n<sizeof(temperature_cases)/sizeof(temperature_cases[0]);n++) {
        for(int i=0;i<3;i++)cJSON_ReplaceItemInObject(weather_data,temperature_keys[i],cJSON_CreateNumber(temperature_cases[n][i]));
        weather_temperature_render(weather_data);lv_obj_update_layout(weather_panels[0]);
        assert(lv_obj_has_flag(weather_range_dot,LV_OBJ_FLAG_HIDDEN)==(n>=4));
        if(n==0||n==1)assert(lv_obj_get_x(weather_range_dot)==257);
        if(n==2)assert(lv_obj_get_x(weather_range_dot)==2);
        if(n==3)assert(lv_obj_get_x(weather_range_dot)==512);
        if(n==0)capture("weather-temperature-negative");
        if(n==1)capture("weather-temperature-equal");
    }
    cJSON_ReplaceItemInObject(weather_data,"low",cJSON_CreateNull());weather_temperature_render(weather_data);
    assert(!strcmp(lv_label_get_text(weather_low),"--")&&lv_obj_has_flag(weather_range,LV_OBJ_FLAG_HIDDEN));capture("weather-temperature-unavailable");
    for(int i=0;i<3;i++)cJSON_ReplaceItemInObject(weather_data,temperature_keys[i],saved_temperatures[i]);
    weather_temperature_render(weather_data);lv_refr_now(lv_display_get_default());
    assert(!lv_display_get_default()->inv_p);weather_temperature_render(weather_data);
    assert(!lv_display_get_default()->inv_p); // Identical weather must not redraw the temperature section.
    puts("WEATHER_TEMPERATURE_OK negative, equal, endpoints, outside_range, inverted, invalid, missing, unchanged_heartbeat");
    lv_obj_send_event(weather_nav[1],LV_EVENT_CLICKED,NULL);assert(!strcmp(lv_label_get_text(hour_rain[1]),"降雨 --"));capture("weather-hourly");
    lv_area_t weather_area;lv_obj_get_coords(hour_plot,&weather_area);
    for(int i=0;i<8;i++) {
        int x=weather_area.x1+(hour_points[i][0].x+hour_points[i][1].x)/2;
        int y=weather_area.y1+(hour_points[i][0].y+hour_points[i][1].y)/2;
        bool found=false;
        for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)
            if(preview_buffer[(y+dy)*1280+x+dx]==lv_color_to_u16(lv_color_hex(BLUE)))found=true;
        assert(found);
    }
    assert(!lv_obj_has_flag(hour_rain_bars[0],LV_OBJ_FLAG_HIDDEN)&&lv_obj_get_height(hour_rain_bars[0])==36);
    assert(lv_obj_has_flag(hour_rain_bars[1],LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(hour_rain_bars[2],LV_OBJ_FLAG_HIDDEN));
    lv_obj_scroll_to_x(weather_panels[1],12*HOUR_STEP,LV_ANIM_OFF);capture("weather-hourly-later");
    int weather_scroll=lv_obj_get_scroll_x(weather_panels[1]);assert(weather_scroll>0);
    weather_forecast_render(weather_data);assert(lv_obj_get_scroll_x(weather_panels[1])==weather_scroll);
    cJSON *missing_hour=cJSON_GetArrayItem(forecast_hours,12);
    cJSON_DeleteItemFromObject(missing_hour,"temperature");weather_forecast_render(weather_data);
    assert(lv_obj_has_flag(hour_segments[11],LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(hour_segments[12],LV_OBJ_FLAG_HIDDEN));
    assert(!strcmp(lv_label_get_text(hour_temp[12]),"--"));capture("weather-hourly-missing-temperature");
    cJSON_AddNumberToObject(missing_hour,"temperature",23);
    for(int i=0;i<24;i++)cJSON_SetNumberValue(item(cJSON_GetArrayItem(forecast_hours,i),"temperature"),-3);
    weather_forecast_render(weather_data);
    assert(hour_points[0][0].y==hour_points[22][1].y);capture("weather-hourly-flat-cold");
    for(int i=0;i<24;i++)cJSON_SetNumberValue(item(cJSON_GetArrayItem(forecast_hours,i),"temperature"),26-i%9);
    weather_forecast_render(weather_data);
    puts("WEATHER_CURVE_OK horizontal_scroll_retained_missing_gap_flat_negative");
    lv_obj_send_event(weather_nav[2],LV_EVENT_CLICKED,NULL);assert(!strcmp(lv_label_get_text(day_rain[0]),"降水 2.4 mm"));capture("weather-seven-days");
    weather_forecast_render(NULL);capture("weather-forecast-unavailable");assert(!lv_obj_has_flag(forecast_empty[1],LV_OBJ_FLAG_HIDDEN));
    weather_forecast_render(weather_data);lv_obj_send_event(weather_nav[0],LV_EVENT_CLICKED,NULL);workbench_return(NULL);
    assert(!strcmp(lv_label_get_text(home_lunar),"八月十三 · 秋分"));
    cJSON *home_fixture=shown;
    shown=cJSON_Parse("{\"codexTasks\":{\"tasks\":[{\"id\":\"00000000-0000-0000-0000-000000000001\",\"title\":\"旧会话\",\"updatedAt\":1},{\"id\":\"00000000-0000-0000-0000-000000000002\",\"title\":\"最近会话\",\"updatedAt\":2}]}}");
    render_home_task();assert(!strcmp(lv_label_get_text(home_task_title),"最近会话"));
    cJSON *home_remote=item(shown,"codexTasks"),*home_working=cJSON_Duplicate(cJSON_GetArrayItem(item(home_remote,"tasks"),0),true);
    cJSON_AddStringToObject(home_working,"state","working");cJSON_AddItemToObject(home_remote,"overviewTask",home_working);
    render_home_task();assert(!strcmp(lv_label_get_text(home_task_title),"旧会话")&&!strcmp(lv_label_get_text(model_state[1]),"运行中"));
    // A completed task wins over an unrelated active task without selecting
    // the editor. Read failure/heartbeat cannot consume its notification.
    codex_notices=calloc(80,sizeof(*codex_notices));assert(codex_notices);
    snprintf(codex_notices[0].task,sizeof(codex_notices[0].task),"%s",text(cJSON_GetArrayItem(item(home_remote,"tasks"),1),"id"));
    snprintf(codex_notices[0].turn,sizeof(codex_notices[0].turn),"completed-turn");
    codex_notices[0].unread=true;codex_notices[0].arrived=1;
    char before_selection[37];snprintf(before_selection,sizeof(before_selection),"%s",codex_selected);
    render_home_task();assert(home_task_unread&&!strcmp(home_task_turn,"completed-turn"));
    assert(!strcmp(lv_label_get_text(lv_obj_get_child(home_task_action,0)),"查看完成内容"));
    assert(!strcmp(before_selection,codex_selected)&&codex_notices[0].unread);
    capture("overview-completed");
    codex_notices[0].unread=false;render_home_task();assert(!home_task_unread);
    free(codex_notices);codex_notices=NULL;
    puts("OVERVIEW_COMPLETION_OK unread completion precedes working; heartbeat preserves selection and unread; read restores working");
    cJSON_DeleteItemFromObject(home_remote,"overviewTask");cJSON_DeleteItemFromObject(home_remote,"tasks");render_home_task();
    assert(!home_task_id[0]&&!strcmp(lv_label_get_text(model_state[1]),"电脑状态"));
    cJSON_AddItemToObject(shown,"systemMetrics",cJSON_Parse("{\"cpuPercent\":34,\"memoryPercent\":80}"));render_home_task();
    assert(strstr(lv_label_get_text(home_task_title),"34%")&&strstr(lv_label_get_text(home_task_title),"80%"));
    page(0);capture("overview-no-codex");
    lv_obj_send_event(home_task_action,LV_EVENT_CLICKED,NULL);assert(current_page==4);
    cJSON_Delete(shown);shown=home_fixture;render();
    puts("OVERVIEW_SELECTION_UI_OK working_recent_empty_independent_of_list_order");
    lv_font_glyph_dsc_t title_glyph;
    assert(lv_font_get_glyph_dsc(&tab5_font_30,&title_glyph,0x2011,0)&&!title_glyph.is_placeholder);
    assert(lv_font_get_glyph_dsc(&tab5_font_24,&title_glyph,0x2011,0)&&!title_glyph.is_placeholder);
    ui_text(home_task_title,"排查 Wi\xe2\x80\x91" "Fi 未连接和弹窗");page(0);capture("overview-title-unicode-hyphen");render();
    cJSON_ReplaceItemInObject(item(shown,"calendar"),"solarTermLabel",cJSON_CreateString("下个节气 寒露 10-08"));
    clock_music(11000,true);
    assert(!strcmp(lv_label_get_text(home_lunar),"八月十三"));
    assert(!strchr(lv_label_get_text(home_weather),'\n'));
    assert(!strcmp(lv_label_get_text(home_values[1]),"75.37"));
    assert(!strcmp(lv_label_get_text(home_values[2]),"96.72"));
    assert(lv_obj_has_flag(quota_rows[0],LV_OBJ_FLAG_HIDDEN));
    assert(!lv_obj_has_flag(quota_rows[1],LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_has_flag(quota_rows[2],LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_get_child_count(credit_list)==3);
    assert(!strcmp(lv_label_get_text(lv_obj_get_child(lv_obj_get_child(credit_list,2),1)),"2026-10-23  00:27"));
    lv_obj_update_layout(lv_screen_active());
    page(1);lv_obj_update_layout(lv_screen_active());capture("quota-three-cards");
    assert(lv_obj_has_flag(quota_weekly_remaining,LV_OBJ_FLAG_HIDDEN));
    int64_t quota_now=(int64_t)num(shown,"epochUtc");
    cJSON_AddNumberToObject(codex,"weeklyResetEpoch",quota_now+5*86400+14*3600);
    cJSON_ReplaceItemInObject(codex,"weeklyReset",cJSON_CreateString("09-29 05:50"));
    render_quota();
    assert(!strcmp(lv_label_get_text(quota_rows[1]),"本周已使用"));
    assert(lv_obj_has_flag(quota_time_markers[1],LV_OBJ_FLAG_HIDDEN));
    for(int cut=0;cut<6;cut++) {
        assert(!lv_obj_has_flag(quota_bar_dividers[1][cut],LV_OBJ_FLAG_HIDDEN));
        assert(lv_obj_get_x(quota_bar_dividers[1][cut])==(cut+1)*100-1);
    }
    cJSON_AddNumberToObject(codex,"weeklyWindowSeconds",604800);render_quota();
    assert(!lv_obj_has_flag(quota_time_markers[1],LV_OBJ_FLAG_HIDDEN));
    lv_obj_update_layout(lv_screen_active());int weekly_x=lv_obj_get_x(quota_time_markers[1]);
    quota_countdown(quota_now+3600);lv_obj_update_layout(lv_screen_active());
    int hour_move=lv_obj_get_x(quota_time_markers[1])-weekly_x;
    assert(hour_move>=4&&hour_move<=5);
    quota_countdown(quota_now);lv_obj_update_layout(lv_screen_active());capture("quota-time-weekly");
    assert(!lv_obj_has_flag(quota_weekly_remaining,LV_OBJ_FLAG_HIDDEN));
    assert(!strcmp(lv_label_get_text(quota_weekly_remaining),"距重置")&&!strcmp(lv_label_get_text(quota_weekly_time),"5d 14h"));
    quota_countdown(quota_now+86400+6*3600);
    assert(!strcmp(lv_label_get_text(quota_weekly_time),"4d 8h"));
    capture("quota-reset-countdown");
    quota_countdown(quota_now+5*86400+13*3600+30*60);
    assert(!strcmp(lv_label_get_text(quota_weekly_remaining),"距重置")&&!strcmp(lv_label_get_text(quota_weekly_time),"30m"));
    quota_countdown(quota_now+5*86400+14*3600);
    assert(!strcmp(lv_label_get_text(quota_weekly_remaining),"等待重置同步"));
    assert(lv_obj_has_flag(quota_weekly_time,LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_has_flag(quota_time_markers[1],LV_OBJ_FLAG_HIDDEN));
    quota_countdown(quota_now-35*3600);
    assert(lv_obj_has_flag(quota_time_markers[1],LV_OBJ_FLAG_HIDDEN));
    cJSON_ReplaceItemInObject(codex,"weeklyWindowSeconds",cJSON_CreateNumber(0));quota_countdown(quota_now);
    assert(lv_obj_has_flag(quota_time_markers[1],LV_OBJ_FLAG_HIDDEN));
    cJSON_ReplaceItemInObject(codex,"weeklyWindowSeconds",cJSON_CreateNumber(604800));
    quota_countdown(quota_now);
    assert(!strcmp(lv_label_get_text(credit_count),"3 张"));
    cJSON_AddItemToArray(item(codex,"creditExpiries"),cJSON_CreateString("2026-11-04 11:26"));
    cJSON_ReplaceItemInObject(codex,"credits",cJSON_CreateNumber(4));render_quota();
    lv_obj_update_layout(lv_screen_active());capture("quota-four-cards");
    assert(strstr(lv_label_get_text(credit_count),"上滑"));
    lv_obj_scroll_to_y(credit_list,lv_obj_get_scroll_bottom(credit_list),LV_ANIM_OFF);
    capture("quota-four-cards-scrolled");
    cJSON_DeleteItemFromArray(item(codex,"creditExpiries"),3);
    cJSON_ReplaceItemInObject(codex,"credits",cJSON_CreateNumber(3));render();page(0);
    for(int i=0;i<4;i++) {
        lv_obj_add_state(workbench_nav[i],LV_STATE_PRESSED);
        lv_obj_remove_state(workbench_nav[i],LV_STATE_PRESSED);
    }
    lv_area_t usage_area,percent_area;
    lv_obj_get_coords(home_captions[0],&usage_area);lv_obj_get_coords(home_values[0],&percent_area);
    assert(!strcmp(lv_label_get_text(home_captions[0]),"本周已用 · 缓存"));
    assert(usage_area.y2<percent_area.y1);
    lv_area_t title_area,number_area,bar_area;
    lv_obj_get_coords(quota_rows[1],&title_area);lv_obj_get_coords(quota_values[1],&number_area);
    lv_obj_get_coords(quota_bars[1],&bar_area);
    assert(number_area.y1-title_area.y2>=20);
    assert(bar_area.y1-number_area.y2>=12);
    assert(!strcmp(lv_label_get_text(quota_plan),"PRO"));
    assert(!strcmp(lv_dropdown_get_options(codex_project_picker),"M5Stack TAB5\nbiaoshu"));
    assert(!strcmp(codex_task_ids[0],"01a0cc69-2782-7650-9903-303578a0296e"));
    assert(!codex_task_ids[1][0]);
    assert(lv_obj_get_x(codex_voice_button)>500&&lv_obj_get_x(codex_voice_button)<lv_obj_get_x(codex_send_button));
    assert(!strcmp(lv_label_get_text(trend_values[0]),"--"));
    assert(!strcmp(lv_label_get_text(trend_values[3]),"0"));
    cJSON *carry_day=cJSON_GetArrayItem(item(item(shown,"quotaTrend"),"days"),4);
    cJSON_AddNumberToObject(carry_day,"carryOver",6);render_quota();
    assert(!strcmp(lv_label_get_text(trend_values[4]),"18"));
    assert(!strcmp(lv_label_get_text(trend_title),"近7日 · 周额度使用（百分比）"));
    page(1);capture("quota-carry-over");page(0);
    cJSON_DeleteItemFromObject(carry_day,"carryOver");render_quota();
    assert(!strcmp(lv_label_get_text(trend_values[4]),"18"));
    // Even an older bridge carrying a cached five-hour value cannot show it for PRO.
    cJSON_AddNumberToObject(codex,"primary",42);render_quota();
    assert(lv_obj_has_flag(quota_rows[0],LV_OBJ_FLAG_HIDDEN));
    cJSON_ReplaceItemInObject(codex,"plan",cJSON_CreateString(" plus "));render_quota();
    assert(!lv_obj_has_flag(quota_rows[0],LV_OBJ_FLAG_HIDDEN));
    lv_obj_update_layout(lv_screen_active());
    assert(lv_obj_get_height(quota_bars[1])==6);
    quota_countdown(quota_now);
    assert(lv_obj_has_flag(quota_weekly_remaining,LV_OBJ_FLAG_HIDDEN));
    {
        cJSON *claude=cJSON_GetArrayItem(item(shown,"quotaDetails"),0);
        cJSON_AddNumberToObject(claude,"primaryResetEpoch",quota_now+150*60);
        cJSON_AddNumberToObject(claude,"primaryWindowSeconds",300*60);
        cJSON_AddNumberToObject(claude,"weeklyResetEpoch",quota_now+4*86400);
        cJSON_AddNumberToObject(claude,"weeklyWindowSeconds",7*86400);
        selected_quota=0;lv_dropdown_set_selected(quota_picker,0);render_quota();
        lv_obj_update_layout(lv_screen_active());
        assert(!lv_obj_has_flag(quota_time_markers[0],LV_OBJ_FLAG_HIDDEN));
        assert(!lv_obj_has_flag(quota_time_markers[1],LV_OBJ_FLAG_HIDDEN));
        for(int cut=0;cut<6;cut++) {
            assert(lv_obj_has_flag(quota_bar_dividers[0][cut],LV_OBJ_FLAG_HIDDEN)==(cut>=4));
            if(cut<4)assert(lv_obj_get_x(quota_bar_dividers[0][cut])==(cut+1)*140-1);
            assert(!lv_obj_has_flag(quota_bar_dividers[1][cut],LV_OBJ_FLAG_HIDDEN));
        }
        int minute_x=lv_obj_get_x(quota_time_markers[0]);
        assert(minute_x+10-lv_obj_get_x(quota_bars[0])==350);
        quota_countdown(quota_now+60);lv_obj_update_layout(lv_screen_active());
        int minute_move=lv_obj_get_x(quota_time_markers[0])-minute_x;
        assert(minute_move>=2&&minute_move<=3);
        quota_countdown(quota_now);lv_obj_update_layout(lv_screen_active());
        lv_area_t face,label_area;
        lv_obj_get_coords(quota_time_faces[0],&face);lv_obj_get_coords(quota_rows[0],&label_area);
        assert(face.y1>label_area.y2);
        page(1);capture("quota-time-claude");
        cJSON_DeleteItemFromObject(claude,"primaryWindowSeconds");quota_countdown(quota_now);
        assert(lv_obj_has_flag(quota_time_markers[0],LV_OBJ_FLAG_HIDDEN));
        assert(!lv_obj_has_flag(quota_time_markers[1],LV_OBJ_FLAG_HIDDEN));
        selected_quota=1;lv_dropdown_set_selected(quota_picker,1);
        page(0);
        puts("QUOTA_TIME_MARKER_OK weekly hour movement, Claude five-hour minute movement, independent windows, expired and unknown windows hidden, five/seven equal bar segments without an external ruler");
    }
    cJSON_ReplaceItemInObject(codex,"plan",cJSON_CreateString("PRO"));render_quota();
    assert(last_frame[0]==1 && last_frame[1]==0);
    for(int i=0;i<2;i++) {
        lv_obj_update_layout(images[i]);
        int longest=pictures[i].width>pictures[i].height?pictures[i].width:pictures[i].height;
        assert(longest*lv_image_get_scale(images[i])/256<=(i==0?280:232));
        lv_area_t child,parent;lv_obj_get_coords(images[i],&child);lv_obj_get_content_coords(lv_obj_get_parent(images[i]),&parent);
        assert(abs((child.x1+child.x2)-(parent.x1+parent.x2))<=2);
        assert(abs((child.y1+child.y2)-(parent.y1+parent.y2))<=2);
    }
    for(int i=0;i<12;i++) { page(i); clock_music(10000,true);char name[32]; snprintf(name,sizeof(name),"page-%d",i); capture(name); }
    {
        cJSON *m=item(shown,"music");char *saved_title=strdup(text(m,"title"));
        cJSON_ReplaceItemInObject(m,"title",cJSON_CreateString("Ballade Pour Adeline\n水边的阿狄丽娜"));
        page(5);last_received++;clock_music(10000,true);lv_obj_update_layout(lv_screen_active());
        assert(!strcmp(lv_label_get_text(music),"Ballade Pour Adeline"));
        assert(!strcmp(lv_label_get_text(music_alias),"水边的阿狄丽娜"));
        assert(lv_obj_get_y(music_alias)+lv_obj_get_height(music_alias)<lv_obj_get_y(music_artist));
        capture("music-bilingual-layout");
        cJSON_ReplaceItemInObject(m,"title",cJSON_CreateString("A Very Long Piano Recording with a Long Subtitle and Additional Details\n一首有中文别名的钢琴曲"));
        last_received++;clock_music(10000,true);lv_obj_update_layout(lv_screen_active());
        assert(lv_obj_get_y(music_alias)>=lv_obj_get_y(music)+lv_obj_get_height(music));
        assert(lv_obj_get_y(music_album)+lv_obj_get_height(music_album)<lv_obj_get_y(music_time));
        capture("music-long-layout");
        cJSON_ReplaceItemInObject(m,"title",cJSON_CreateString(saved_title));free(saved_title);last_received++;clock_music(10000,true);
        assert(lv_obj_has_flag(music_alias,LV_OBJ_FLAG_HIDDEN));
    }
    {
        asset_image saved=pictures[2];
        pictures[2]=(asset_image){.width=336,.height=336,.frames=1,.delays={1000},.revision=1,.crc=3};
        pictures[2].pixels=calloc(336*336,2);assert(pictures[2].pixels);
        for(int y=75;y<261;y++)for(int x=0;x<336;x++)
            ((uint16_t *)pictures[2].pixels)[y*336+x]=x<45?0xf800:x>=291?0x001f:0x07e0;
        cJSON *m=item(shown,"music");
        cJSON_AddNumberToObject(m,"artworkWidth",150);cJSON_AddNumberToObject(m,"artworkHeight",83);
        last_frame[2]=-1;page(5);images_tick(0);lv_obj_update_layout(music_cover);
        int scale=lv_image_get_scale(images[2]);
        assert(336*scale/256>=558&&336*scale/256<=560);
        assert(186*scale/256>=309&&186*scale/256<=311);
        assert(lv_obj_get_width(music_cover)==576&&lv_obj_get_x(music)==658);
        lv_area_t picture,cover;
        lv_obj_get_coords(images[2],&picture);lv_obj_get_content_coords(music_cover,&cover);
        int cx=(picture.x1+picture.x2)/2,cy=(picture.y1+picture.y2)/2;
        assert(cx-280>=cover.x1&&cx+280<=cover.x2);
        assert(cy-156>=cover.y1&&cy+156<=cover.y2);
        assert(lv_obj_get_x(music)>lv_obj_get_x(music_cover)+lv_obj_get_width(music_cover));
        capture("music-landscape-large");
        lv_image_cache_drop(&descriptors[2]);free(pictures[2].pixels);
        pictures[2]=(asset_image){.width=560,.height=315,.frames=1,.delays={1000},.revision=2,.crc=3};
        pictures[2].pixels=calloc(560*315,2);assert(pictures[2].pixels);
        for(int y=0;y<315;y++)for(int x=0;x<560;x++)((uint16_t *)pictures[2].pixels)[y*560+x]=x<75?0xf800:x>=485?0x001f:0x07e0;
        last_frame[2]=-1;images_tick(0);lv_obj_update_layout(music_cover);
        assert(lv_image_get_scale(images[2])==256);
        assert(lv_obj_get_style_bg_opa(music_cover,0)==LV_OPA_TRANSP);
        assert(lv_obj_get_style_border_width(music_cover,0)==0);
        capture("music-native-cover");
        // Restore the legacy geometry before testing its square fallback.
        lv_image_cache_drop(&descriptors[2]);free(pictures[2].pixels);
        pictures[2]=(asset_image){.width=336,.height=336,.frames=1,.delays={1000},.revision=3,.crc=3};
        pictures[2].pixels=calloc(336*336,2);assert(pictures[2].pixels);last_frame[2]=-1;
        // Change metadata without a new bitmap: layout must return to square.
        cJSON_SetNumberValue(item(m,"artworkWidth"),336);cJSON_SetNumberValue(item(m,"artworkHeight"),336);
        images_tick(0);lv_obj_update_layout(music_cover);
        assert(lv_image_get_scale(images[2])==384*256/336&&lv_obj_get_width(music_cover)==384&&lv_obj_get_x(music)==466);
        cJSON_SetNumberValue(item(m,"artworkWidth"),1e9);images_tick(0);
        assert(lv_image_get_scale(images[2])==384*256/336);
        cJSON_DeleteItemFromObject(m,"artworkWidth");cJSON_DeleteItemFromObject(m,"artworkHeight");
        lv_image_cache_drop(&descriptors[2]);free(pictures[2].pixels);pictures[2]=saved;last_frame[2]=-1;images_tick(0);
        assert(abs(saved.width*lv_image_get_scale(images[2])/256-384)<=1);
        puts("MUSIC_LANDSCAPE_OK full picture about 560x310, no text overlap, square/legacy fallback preserved");
    }
    {
        cJSON *original=cJSON_Duplicate(item(shown,"music"),true);
        cJSON_ReplaceItemInObject(shown,"music",cJSON_Parse("{\"title\":\"\",\"playing\":false,\"hasArtwork\":false}"));
        page(5);last_received++;clock_music(10000,true);images_tick(0);
        assert(!lv_obj_has_flag(music_empty,LV_OBJ_FLAG_HIDDEN));
        assert(lv_obj_has_flag(music_cover,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(images[2],LV_OBJ_FLAG_HIDDEN));
        assert(lv_obj_has_flag(music_state,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(progress,LV_OBJ_FLAG_HIDDEN));
        assert(lv_obj_has_flag(music_time,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(music_duration,LV_OBJ_FLAG_HIDDEN));
        capture("music-idle");
        cJSON_DeleteItemFromObject(shown,"music");last_received++;clock_music(10000,true);
        assert(!lv_obj_has_flag(music_empty,LV_OBJ_FLAG_HIDDEN));
        cJSON_AddItemToObject(shown,"music",cJSON_Duplicate(original,true));
        cJSON_ReplaceItemInObject(item(shown,"music"),"playing",cJSON_CreateBool(false));
        last_received++;clock_music(10000,true);images_tick(0);
        assert(lv_obj_has_flag(music_empty,LV_OBJ_FLAG_HIDDEN)&&!lv_obj_has_flag(music,LV_OBJ_FLAG_HIDDEN));
        assert(!strcmp(lv_label_get_text(music_state),"已暂停"));capture("music-paused");
        cJSON_ReplaceItemInObject(shown,"music",original);last_received++;clock_music(10000,true);images_tick(0);
        assert(lv_obj_has_flag(music_empty,LV_OBJ_FLAG_HIDDEN));
        puts("MUSIC_IDLE_OK empty_missing_track_no_stale_cover_paused_metadata_playback_recovery");
    }
    if(getenv("TAB5_CONTROL_FOCUSED")) {
        #include "computer_control_focused_test.h"
    }
    #include "computer_control_test.h"
    check_cache_publication();
    check_swipe_prewarm_fairness();
    check_native_swipe();
    check_native_settle_frame();
    check_scroll_cache();
    check_live_metrics();
    // Only four primary pages participate in horizontal swipes.
    assert(lv_obj_get_child_count(lv_tabview_get_content(tabs))==4);
    for(int i=0;i<4;i++){lv_tabview_set_active(tabs,i,LV_ANIM_OFF);lv_obj_send_event(tabs,LV_EVENT_VALUE_CHANGED,NULL);assert(current_page==primary_pages[i]);}
    page(4);assert(!lv_obj_has_flag(workbench_back,LV_OBJ_FLAG_HIDDEN));
    workbench_return(NULL);assert(current_page==11);
    page(8);page(9);workbench_return(NULL);assert(current_page==8);
    lv_obj_send_event(codex_task_rows[0],LV_EVENT_CLICKED,NULL);
    assert(!strcmp(codex_selected,"01a0cc69-2782-7650-9903-303578a0296e"));
    codex_waiting=true;lv_dropdown_set_selected(codex_project_picker,1);lv_obj_send_event(codex_project_picker,LV_EVENT_VALUE_CHANGED,NULL);
    assert(!strcmp(codex_selected,"01a0cc69-2782-7650-9903-303578a0296e"));codex_waiting=false;
    lv_textarea_set_text(codex_input,"项目一的未发送草稿");
    lv_dropdown_set_selected(codex_project_picker,1);lv_obj_send_event(codex_project_picker,LV_EVENT_VALUE_CHANGED,NULL);
    assert(!codex_selected[0]&&!strcmp(codex_task_ids[0],"01a0cc69-2782-7650-9903-303578a0296f"));
    lv_obj_send_event(codex_task_rows[0],LV_EVENT_CLICKED,NULL);
    assert(!lv_textarea_get_text(codex_input)[0]);
    lv_textarea_set_text(codex_input,"项目二的草稿");
    lv_dropdown_set_selected(codex_project_picker,0);lv_obj_send_event(codex_project_picker,LV_EVENT_VALUE_CHANGED,NULL);
    lv_obj_send_event(codex_task_rows[0],LV_EVENT_CLICKED,NULL);
    assert(!strcmp(lv_textarea_get_text(codex_input),"项目一的未发送草稿"));
    lv_textarea_set_text(codex_input,"");codex_save_draft();
    cJSON *recent_tasks=item(item(shown,"codexTasks"),"tasks");
    cJSON *recent_a=cJSON_GetArrayItem(recent_tasks,0),*recent_b=cJSON_GetArrayItem(recent_tasks,1);
    cJSON_AddNumberToObject(recent_a,"updatedAt",100);cJSON_AddNumberToObject(recent_b,"updatedAt",200);
    int default_submits=preview_submit_count;
    page(0);lv_obj_send_event(workbench_nav[1],LV_EVENT_CLICKED,NULL);
    assert(!strcmp(codex_selected,text(recent_b,"id"))&&!strcmp(codex_project_selected,"bbbbbbbbbbbbbbbb"));
    assert(!strcmp(lv_textarea_get_text(codex_input),"项目二的草稿"));capture("codex-default-recent");
    cJSON_ReplaceItemInObject(recent_a,"updatedAt",cJSON_CreateNumber(300));render_codex();
    assert(!strcmp(codex_selected,text(recent_b,"id"))); // Background changes preserve this visit.
    page(0);lv_obj_send_event(workbench_nav[1],LV_EVENT_CLICKED,NULL);
    assert(!strcmp(codex_selected,text(recent_a,"id"))&&!strcmp(codex_project_selected,"aaaaaaaaaaaaaaaa"));
    codex_waiting=true;assert(!codex_default_focus(true));codex_waiting=false;
    for(int extra=0;extra<9;extra++) {
        cJSON *task=cJSON_CreateObject();char id[37];snprintf(id,sizeof(id),"10000000-0000-4000-8000-%012d",100+extra);
        cJSON_AddStringToObject(task,"id",id);cJSON_AddStringToObject(task,"title","最近活动的会话");
        cJSON_AddStringToObject(task,"projectId","aaaaaaaaaaaaaaaa");cJSON_AddNumberToObject(task,"updatedAt",400+extra);
        cJSON_AddItemToArray(recent_tasks,task);
    }
    page(0);lv_obj_send_event(workbench_nav[1],LV_EVENT_CLICKED,NULL);
    assert(!strcmp(codex_selected,"10000000-0000-4000-8000-000000000108")&&!strcmp(codex_task_ids[7],codex_selected));
    for(int extra=0;extra<9;extra++)cJSON_DeleteItemFromArray(recent_tasks,2);
    codex_select_task(text(recent_a,"id"));
    assert(preview_submit_count==default_submits);cJSON_DeleteItemFromObject(recent_a,"updatedAt");cJSON_DeleteItemFromObject(recent_b,"updatedAt");
    puts("CODEX_DEFAULT_RECENT_OK timestamp_not_array_order, project_and_session, refresh_preserves_choice, next_visit_updates, draft_preserved, selected_beyond_eight_visible, busy_guard, no_submit");
    cJSON *responses=cJSON_AddObjectToObject(item(shown,"codexTasks"),"responses");
    cJSON_AddStringToObject(responses,codex_selected,"已检查连接。\n请继续在电脑上查看完整任务记录。");
    render_codex();
    assert(!lv_obj_has_flag(codex_reply_button,LV_OBJ_FLAG_HIDDEN));
    page(8);lv_obj_send_event(codex_reply_button,LV_EVENT_CLICKED,NULL);
    assert(!lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN));
    capture("codex-reply-open");
    lv_obj_send_event(lv_obj_get_child(codex_reply_overlay,1),LV_EVENT_CLICKED,NULL);
    assert(lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN));
    codex_reading=false;
    cJSON *remote=item(shown,"codexTasks"),*ready=cJSON_AddObjectToObject(remote,"repliesReady");
    codex_notice_tick(1000,true);assert(preview_chimes==0);
    cJSON_AddStringToObject(ready,codex_selected,"reply-one");
    codex_notice_tick(2000,true);assert(preview_chimes==1);
    assert(!lv_obj_has_flag(codex_unread_dot,LV_OBJ_FLAG_HIDDEN));
    assert(!strcmp(lv_label_get_text(codex_unread_count),"1"));
    assert(!strcmp(lv_label_get_text(lv_obj_get_child(codex_reply_button,0)),"查看新回复"));
    capture("codex-unread-badge");
    // Count unread tasks, retain other tasks when the selected one is read,
    // and cap the displayed count without losing the underlying notices.
    codex_notice saved_notices[80];memcpy(saved_notices,codex_notices,sizeof(saved_notices));
    for(int i=1;i<3;i++) {
        snprintf(codex_notices[i].task,sizeof(codex_notices[i].task),"00000000-0000-0000-0000-%012d",i);
        codex_notices[i].unread=true;
    }
    codex_notice_style();assert(!strcmp(lv_label_get_text(codex_unread_count),"3"));
    capture("proposal-codex-count-3");
    codex_notice_read();assert(!strcmp(lv_label_get_text(codex_unread_count),"2"));
    assert(!lv_obj_has_flag(codex_unread_dot,LV_OBJ_FLAG_HIDDEN));
    memcpy(codex_notices,saved_notices,sizeof(saved_notices));
    for(int i=1;i<9;i++)codex_notices[i].unread=true;
    codex_notice_style();assert(!strcmp(lv_label_get_text(codex_unread_count),"9"));
    lv_obj_update_layout(codex_unread_dot);assert(lv_obj_get_width(codex_unread_dot)==24);
    codex_notices[9].unread=true;codex_notice_style();
    assert(!strcmp(lv_label_get_text(codex_unread_count),"9+"));
    lv_obj_update_layout(codex_unread_dot);assert(lv_obj_get_width(codex_unread_dot)==32);
    capture("proposal-codex-count-9plus");
    memcpy(codex_notices,saved_notices,sizeof(saved_notices));codex_notice_style();
    puts("CODEX_UNREAD_COUNT_OK count_tasks, selected_read_retains_others, 9_plus_cap");
    codex_notice_tick(2500,true);assert(preview_chimes==1);
    cJSON *desktop_read=cJSON_AddObjectToObject(remote,"repliesRead");
    cJSON_AddStringToObject(desktop_read,codex_selected,"older-turn");
    codex_notice_tick(2501,true);assert(codex_notices[0].unread);
    cJSON_ReplaceItemInObject(desktop_read,codex_selected,cJSON_CreateString("reply-one"));
    codex_notice_tick(2502,true);
    assert(!codex_notices[0].unread&&lv_obj_has_flag(codex_unread_dot,LV_OBJ_FLAG_HIDDEN));
    assert(!strcmp(lv_label_get_text(lv_obj_get_child(codex_reply_button,0)),"查看回复"));
    capture("codex-desktop-read-count-zero");
    // Read all three tasks without including two of them in the ready catalog.
    memcpy(codex_notices,saved_notices,sizeof(saved_notices));
    for(int i=1;i<3;i++) {
        snprintf(codex_notices[i].task,sizeof(codex_notices[i].task),"00000000-0000-0000-0000-%012d",i);
        snprintf(codex_notices[i].turn,sizeof(codex_notices[i].turn),"other-reply");codex_notices[i].unread=true;
    }
    codex_notice_style();assert(!strcmp(lv_label_get_text(codex_unread_count),"3"));
    codex_notice_tick(2503,true);assert(!strcmp(lv_label_get_text(codex_unread_count),"2"));
    capture("codex-desktop-read-count-two");
    for(int i=1;i<3;i++)cJSON_AddStringToObject(desktop_read,codex_notices[i].task,"other-reply");
    codex_notice_tick(2503,true);assert(lv_obj_has_flag(codex_unread_dot,LV_OBJ_FLAG_HIDDEN));
    memcpy(codex_notices,saved_notices,sizeof(saved_notices));
    snprintf(codex_notices[0].turn,sizeof(codex_notices[0].turn),"reply-new");
    cJSON_ReplaceItemInObject(ready,codex_selected,cJSON_CreateString("reply-new"));
    codex_notice_tick(2504,true);assert(codex_notices[0].unread);
    cJSON_DeleteItemFromObject(remote,"repliesRead");codex_notice_tick(2505,true);assert(codex_notices[0].unread);
    memcpy(codex_notices,saved_notices,sizeof(saved_notices));
    cJSON_ReplaceItemInObject(ready,codex_selected,cJSON_CreateString("reply-one"));codex_notice_style();
    puts("CODEX_DESKTOP_READ_SYNC_OK exact_turn, top_count_zero, reduced_catalog, new_turn_preserved, unavailable_preserved");
    cJSON *desktop_unread=cJSON_AddObjectToObject(remote,"repliesUnread");
    cJSON_AddStringToObject(desktop_unread,codex_selected,"reply-one");
    memset(codex_notices,0,sizeof(saved_notices));codex_notices_ready=false;
    codex_notice_tick(2506,true);assert(codex_notices[0].unread&&preview_chimes==1);
    assert(tab5_codex_notice_state==65793u); // queue 1, actual label 1, visible
    codex_notice_read();codex_notice_tick(2507,true);assert(!codex_notices[0].unread);
    assert(tab5_codex_notice_state==0u);
    cJSON_DeleteItemFromObject(remote,"repliesUnread");
    memcpy(codex_notices,saved_notices,sizeof(saved_notices));codex_notice_style();
    puts("CODEX_DESKTOP_UNREAD_RESTART_OK restores_badge_without_chime, local_read_not_resurrected, actual_widget_diagnostic");
    // The overview action must request the completed turn explicitly, retain
    // the draft and keep unread until matching reply content is rendered.
    lv_textarea_set_text(codex_input,"完成提醒不覆盖草稿");
    int completion_sends=preview_submit_count;
    last_received=esp_timer_get_time()/1000;
    page(0);render_home_task();assert(home_task_unread);
    lv_obj_send_event(home_task_action,LV_EVENT_CLICKED,NULL);
    assert(current_page==8&&!lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN));
    assert(!strcmp(preview_read_turn,"reply-one")&&preview_read_page==0);
    assert(!codex_reply_follow&&home_task_unread);
    assert(!strcmp(lv_textarea_get_text(codex_input),"完成提醒不覆盖草稿"));
    assert(preview_submit_count==completion_sends);
    codex_reading=false;codex_nav_pending=false;
    cJSON *completion_view=cJSON_AddObjectToObject(remote,"replyView");
    cJSON_AddStringToObject(completion_view,"taskId",codex_selected);cJSON_AddStringToObject(completion_view,"turnId","reply-one");
    cJSON_AddNumberToObject(completion_view,"page",0);cJSON_AddNumberToObject(completion_view,"pageCount",1);
    cJSON_AddBoolToObject(completion_view,"hasText",false);
    cJSON_AddStringToObject(completion_view,"readReceipt","3c4c9f23-c67f-4fbf-b59e-59140ec508ab");
    unsigned rendered_acks=preview_read_acks;
    cJSON *saved_response=cJSON_Duplicate(item(responses,codex_selected),true);
    cJSON_ReplaceItemInObject(responses,codex_selected,cJSON_CreateString("本轮尚无公开文字。"));
    render_codex();assert(home_task_unread&&!codex_reply_follow&&preview_read_acks==rendered_acks);
    codex_reply_refresh(NULL);assert(!codex_read_cache_only&&!strcmp(preview_read_turn,"reply-one"));codex_reading=false;
    cJSON_ReplaceItemInObject(completion_view,"hasText",cJSON_CreateBool(true));
    cJSON_ReplaceItemInObject(completion_view,"turnId",cJSON_CreateString("other-turn"));
    cJSON_ReplaceItemInObject(responses,codex_selected,cJSON_CreateString("其他轮次内容"));
    render_codex();assert(home_task_unread&&preview_read_acks==rendered_acks);
    cJSON_ReplaceItemInObject(completion_view,"turnId",cJSON_CreateString("reply-one"));
    cJSON_ReplaceItemInObject(responses,codex_selected,cJSON_CreateString("已完成的真实公开答复"));
    render_codex();assert(!home_task_unread&&lv_obj_has_flag(codex_unread_dot,LV_OBJ_FLAG_HIDDEN));
    assert(preview_read_acks==rendered_acks+1);render_codex();assert(preview_read_acks==rendered_acks+1);
    codex_notice_tick(2550,true);assert(!home_task_unread&&preview_chimes==1);
    cJSON_DeleteItemFromObject(remote,"replyView");cJSON_ReplaceItemInObject(responses,codex_selected,saved_response);
    puts("OVERVIEW_READ_ACK_OK pinned completed text clears unread; placeholders and other turns do not; empty replies retry");
    lv_obj_send_event(lv_obj_get_child(codex_reply_overlay,1),LV_EVENT_CLICKED,NULL);
    codex_reading=false;codex_nav_pending=false;
    lv_textarea_set_text(codex_input,"");codex_save_draft();
    puts("OVERVIEW_COMPLETION_OPEN_OK exact turn, page zero, draft preserved, no send, unread retained");
    alert_visual_tick(2000);capture("codex-reply-notice");
    codex_notice_read();assert(lv_obj_has_flag(codex_unread_dot,LV_OBJ_FLAG_HIDDEN));
    codex_notice_tick(2600,true);assert(preview_chimes==1);
    cJSON_ReplaceItemInObject(ready,codex_selected,cJSON_CreateString("reply-two"));
    prefs.alerts=false;codex_notice_tick(3000,true);assert(preview_chimes==1);prefs.alerts=true;codex_notice_read();
    cJSON *view=cJSON_AddObjectToObject(remote,"replyView");
    cJSON_AddStringToObject(view,"taskId",codex_selected);cJSON_AddStringToObject(view,"turnId","active-turn");
    cJSON_AddNumberToObject(view,"page",1);cJSON_AddNumberToObject(view,"pageCount",3);
    cJSON_AddStringToObject(item(remote,"receipts"),codex_selected,"运行中");
    cJSON_ReplaceItemInObject(responses,codex_selected,cJSON_CreateString("进度\n正在检查当前任务的接口。\n\n操作\n读取任务记录 · 完成\n\n进度\n已经找到旧轮次状态与当前执行状态不一致的原因。"));
    codex_nav_pending=false;render_codex();assert(!strcmp(lv_label_get_text(codex_reply_status),"运行中"));
    last_received=esp_timer_get_time()/1000;
    // This scenario opens live activity; the preceding history-read scenario
    // now correctly leaves a real bookmark rather than an empty placeholder.
    codex_open_latest=true;
    lv_obj_send_event(codex_reply_button,LV_EVENT_CLICKED,NULL);assert(preview_read_page==-1);
    codex_reading=false;codex_nav_pending=false;lv_obj_send_event(codex_reply_older,LV_EVENT_CLICKED,NULL);assert(preview_read_page==0);
    codex_reading=false;lv_obj_send_event(codex_reply_latest,LV_EVENT_CLICKED,NULL);assert(preview_read_page==-1);
    codex_reading=false;codex_read_next=0;
    int before_poll=preview_reads;tick(NULL);assert(preview_reads==before_poll+1);
    // Updating the view never sends a new Codex message and preserves the draft.
    assert(!lv_textarea_get_text(codex_input)[0]);
    codex_reading=false;codex_nav_pending=false;render_codex();capture("codex-live-activity");
    // History navigation preserves the visible page until its matching heartbeat arrives.
    cJSON_AddBoolToObject(view,"hasPrevious",true);cJSON_AddBoolToObject(view,"hasNext",false);
    render_codex();int history_sends=preview_submit_count;
    lv_obj_send_event(codex_turn_older,LV_EVENT_CLICKED,NULL);
    assert(preview_read_offset==-1&&!strcmp(preview_read_turn,"active-turn")&&codex_turn_pending);
    codex_reading=false;codex_nav_pending=false;snprintf(codex_reply_expected,sizeof(codex_reply_expected),"older-turn");codex_turn_offset=0;
    render_codex();assert(codex_turn_pending&&!strcmp(codex_reply_turn,"active-turn"));
    cJSON_ReplaceItemInObject(view,"turnId",cJSON_CreateString("older-turn"));
    cJSON_ReplaceItemInObject(view,"page",cJSON_CreateNumber(0));
    cJSON_ReplaceItemInObject(view,"pageCount",cJSON_CreateNumber(2));
    cJSON_ReplaceItemInObject(view,"hasNext",cJSON_CreateTrue());
    cJSON_AddBoolToObject(view,"history",true);cJSON_AddStringToObject(view,"startedLabel","09-26 10:30");
    cJSON_ReplaceItemInObject(responses,codex_selected,cJSON_CreateString("答复\n这是上一轮的历史回复。\n\n可以翻页阅读，也可以回到最新动态。"));
    render_codex();assert(!codex_turn_pending&&!strcmp(codex_reply_selected,"older-turn"));
    assert(strstr(lv_label_get_text(codex_reply_status),"历史回复"));
    int history_idle_reads=preview_reads;
    codex_read_next=0;codex_reply_refresh(NULL);assert(!codex_reading&&preview_reads==history_idle_reads);
    codex_reading=false;capture("codex-history-reply");
    lv_obj_send_event(codex_reply_newer,LV_EVENT_CLICKED,NULL);assert(preview_read_page==1&&!strcmp(preview_read_turn,"older-turn"));
    codex_reading=false;codex_nav_pending=false;lv_obj_send_event(codex_turn_newer,LV_EVENT_CLICKED,NULL);assert(preview_read_offset==1);
    codex_reading=false;lv_obj_send_event(codex_reply_latest,LV_EVENT_CLICKED,NULL);
    assert(!preview_read_turn[0]&&preview_read_offset==0&&preview_read_page==-1&&!codex_turn_pending);
    assert(preview_submit_count==history_sends);codex_reading=false;codex_nav_pending=false;
    // An in-flight automatic poll must not eat page clicks. The most recent
    // requested destination wins; an older completed result cannot repaint it.
    codex_reply_page=0;codex_reply_total=4;codex_reply_follow=false;
    codex_reply_refresh(NULL);assert(codex_reading);
    int reads_before_click=preview_reads;
    lv_obj_remove_state(codex_reply_newer,LV_STATE_DISABLED);
    lv_obj_send_event(codex_reply_newer,LV_EVENT_CLICKED,NULL);
    lv_obj_send_event(codex_reply_newer,LV_EVENT_CLICKED,NULL);
    assert(codex_requested_page==1&&preview_reads==reads_before_click&&codex_nav_queued);
    assert(lv_obj_has_state(codex_reply_newer,LV_STATE_DISABLED));
    assert(strstr(lv_label_get_text(codex_reply_status),"正在翻页"));
    capture("codex-reply-loading");
    preview_result_code=200;codex_read_finish(100);
    assert(!codex_reading&&codex_nav_pending&&codex_read_next==0);
    codex_reply_refresh(NULL);assert(preview_read_page==1);
    // Back to latest supersedes that pending page, with immediate visual feedback.
    lv_obj_send_event(codex_reply_latest,LV_EVENT_CLICKED,NULL);
    assert(codex_requested_page==-1&&strstr(lv_label_get_text(codex_reply_status),"正在回到最新"));
    codex_read_finish(200);codex_reply_refresh(NULL);assert(preview_read_page==-1&&!preview_read_turn[0]);
    preview_read_view=cJSON_CreateObject();cJSON_AddStringToObject(preview_read_view,"taskId",codex_selected);
    cJSON_AddStringToObject(preview_read_view,"text","最新答复直接显示");cJSON_AddStringToObject(preview_read_view,"receipt","已完成");
    cJSON *direct_view=cJSON_AddObjectToObject(preview_read_view,"replyView");
    cJSON_AddStringToObject(direct_view,"taskId",codex_selected);cJSON_AddStringToObject(direct_view,"turnId","new-latest");
    cJSON_AddNumberToObject(direct_view,"page",0);cJSON_AddNumberToObject(direct_view,"pageCount",1);
    codex_read_finish(300);
    assert(!codex_nav_pending&&!strcmp(lv_label_get_text(codex_reply_text),"最新答复直接显示"));
    assert(codex_read_next==3300); // Refresh starts after the response, not the request.
    render_codex();assert(!strcmp(codex_reply_turn,"new-latest")); // stale heartbeat cannot roll back direct result
    // Cache the preceding completed page and turn it without any HTTP request.
    cJSON *cached_history=cJSON_Duplicate(codex_inline_reply,true);
    cJSON_ReplaceItemInObject(cached_history,"text",cJSON_CreateString("缓存中的历史正文"));
    cJSON *cached_view=item(cached_history,"replyView");
    cJSON_ReplaceItemInObject(cached_view,"turnId",cJSON_CreateString("cached-history"));
    cJSON_ReplaceItemInObject(cached_view,"pageCount",cJSON_CreateNumber(3));
    cJSON_AddStringToObject(cached_view,"state","completed");cJSON_AddBoolToObject(cached_view,"history",true);
    codex_cache_store(cached_history);cJSON_Delete(cached_history);
    snprintf(codex_reply_turn,sizeof(codex_reply_turn),"cached-history");codex_reply_page=1;codex_reply_total=3;
    int cache_reads=preview_reads;lv_obj_remove_state(codex_reply_older,LV_STATE_DISABLED);
    lv_obj_send_event(codex_reply_older,LV_EVENT_CLICKED,NULL);
    assert(preview_reads==cache_reads&&!strcmp(lv_label_get_text(codex_reply_text),"缓存中的历史正文")&&codex_showing_cache);
    capture("codex-reply-cached");
    // BLE history can succeed while the full-state heartbeat is delayed.
    // A pinned public page never polls, even when the full heartbeat recovers.
    int64_t saved_received=last_received;
    last_received=esp_timer_get_time()/1000-7000;
    codex_reply_refresh(NULL);
    assert(preview_reads==cache_reads&&!codex_reading);
    assert(!strcmp(lv_label_get_text(codex_reply_status),"历史回复 · 已缓存"));
    assert(!strcmp(lv_label_get_text(codex_reply_text),"缓存中的历史正文"));
    last_received=esp_timer_get_time()/1000;
    codex_reply_refresh(NULL);assert(!codex_reading&&preview_reads==cache_reads);
    assert(!strcmp(lv_label_get_text(codex_reply_status),"历史回复 · 已缓存"));
    assert(!strcmp(lv_label_get_text(codex_reply_text),"缓存中的历史正文"));
    preview_result_code=200;last_received=saved_received;cache_reads=preview_reads;
    puts("HISTORY_BACKGROUND_OK pinned history never polls; stale and recovered telemetry preserve page");
    // A just-authenticated BLE response lets the next page start even when
    // full telemetry is queued; without it retry promptly when data returns.
    last_received=esp_timer_get_time()/1000-7000;
    codex_requested_page=1;codex_reply_follow=false;preview_read_recent=true;
    int quick_reads=preview_reads;codex_navigation_start();
    assert(codex_reading&&preview_reads==quick_reads+1&&preview_read_page==1);
    codex_reading=false;preview_read_recent=false;
    codex_reply_refresh(NULL);
    assert(!codex_reading&&codex_read_next<=esp_timer_get_time()/1000+250);
    assert(strstr(lv_label_get_text(codex_reply_status),"等待状态同步"));
    last_received=saved_received;codex_requested_page=0;codex_navigation_start();
    cache_reads=preview_reads;
    assert(codex_showing_cache&&!codex_reading&&!codex_nav_pending);
    puts("HISTORY_QUICK_PAGE_OK authenticated reply bypasses stale telemetry; waiting retries within 250ms");
    // Learn real previous/next relationships from successful relative reads.
    cJSON *turn_a=cJSON_Duplicate(codex_inline_reply,true),*turn_b=cJSON_Duplicate(turn_a,true);
    cJSON *a_view=item(turn_a,"replyView"),*b_view=item(turn_b,"replyView");
    cJSON_AddBoolToObject(a_view,"hasPrevious",true);cJSON_AddBoolToObject(a_view,"hasNext",true);
    cJSON_AddBoolToObject(b_view,"hasPrevious",true);cJSON_AddBoolToObject(b_view,"hasNext",true);
    cJSON_ReplaceItemInObject(b_view,"turnId",cJSON_CreateString("previous-cached-turn"));
    cJSON_ReplaceItemInObject(turn_b,"text",cJSON_CreateString("上一轮缓存正文"));
    cJSON_Delete(codex_inline_reply);codex_inline_reply=cJSON_Duplicate(turn_a,true);render_codex();
    int turn_reads=preview_reads;
    lv_obj_send_event(codex_turn_older,LV_EVENT_CLICKED,NULL);
    assert(codex_reading&&preview_reads==turn_reads+1&&preview_read_offset==-1);
    preview_read_view=cJSON_Duplicate(turn_b,true);codex_read_finish(7000);
    assert(!strcmp(codex_reply_turn,"previous-cached-turn"));
    wifi_connected=false;last_received=esp_timer_get_time()/1000-90000;
    lv_obj_send_event(codex_turn_newer,LV_EVENT_CLICKED,NULL);
    assert(!strcmp(codex_reply_turn,"cached-history")&&preview_reads==turn_reads+1);
    lv_obj_send_event(codex_turn_older,LV_EVENT_CLICKED,NULL);
    assert(!strcmp(codex_reply_turn,"previous-cached-turn")&&preview_reads==turn_reads+1);
    // Waiting for an unknown older turn cannot lock the cached newer turn.
    lv_obj_send_event(codex_turn_older,LV_EVENT_CLICKED,NULL);
    assert(codex_nav_pending&&!lv_obj_has_state(codex_turn_newer,LV_STATE_DISABLED));
    codex_navigation_timeout(codex_nav_started+11999);assert(codex_nav_pending);
    codex_navigation_timeout(codex_nav_started+12000);assert(!codex_nav_pending&&codex_nav_timed_out);
    codex_reply_refresh(NULL);assert(preview_reads==turn_reads+1);
    lv_obj_send_event(codex_turn_newer,LV_EVENT_CLICKED,NULL);
    assert(!strcmp(codex_reply_turn,"cached-history")&&!codex_nav_timed_out&&preview_reads==turn_reads+1);
    wifi_connected=true;last_received=saved_received;
    // A late network reply may fill cache, but never replace a newer cached click.
    lv_obj_send_event(codex_turn_newer,LV_EVENT_CLICKED,NULL);assert(codex_reading);
    lv_obj_send_event(codex_turn_older,LV_EVENT_CLICKED,NULL);
    assert(!strcmp(codex_reply_turn,"previous-cached-turn")&&codex_reading);
    cJSON *late=cJSON_Duplicate(turn_a,true);
    cJSON_ReplaceItemInObject(item(late,"replyView"),"turnId",cJSON_CreateString("late-newer-turn"));
    preview_read_view=late;codex_read_finish(8000);
    assert(!strcmp(codex_reply_turn,"previous-cached-turn")&&!codex_nav_pending);
    lv_obj_send_event(codex_turn_newer,LV_EVENT_CLICKED,NULL);
    assert(!strcmp(codex_reply_turn,"cached-history"));
    char cache_task_before[37];snprintf(cache_task_before,sizeof(cache_task_before),"%s",codex_selected);
    snprintf(codex_selected,sizeof(codex_selected),"00000000-0000-0000-0000-000000000000");
    assert(codex_cache_turn_index("cached-history",-1)<0);
    snprintf(codex_selected,sizeof(codex_selected),"%s",cache_task_before);
    cJSON_Delete(turn_a);cJSON_Delete(turn_b);cache_reads=preview_reads;
    puts("HISTORY_TURN_CACHE_OK first read then offline previous/next; pending unlock; 12s bound; late response isolation; task isolation");
    // Prefetched adjacency metadata makes an unvisited turn locally reachable.
    cJSON *batch=cJSON_Duplicate(codex_inline_reply,true),*prefetched=cJSON_Duplicate(batch,true);
    cJSON_AddStringToObject(item(batch,"replyView"),"nextTurnId","prefetched-turn");
    cJSON_ReplaceItemInObject(item(prefetched,"replyView"),"turnId",cJSON_CreateString("prefetched-turn"));
    cJSON_AddStringToObject(item(prefetched,"replyView"),"previousTurnId","cached-history");
    cJSON_AddItemToArray(cJSON_AddArrayToObject(batch,"neighbors"),prefetched);
    codex_cache_reply(batch);cJSON_Delete(batch);
    assert(codex_cache_turn_index("cached-history",1)>=0&&codex_cache_turn_index("prefetched-turn",-1)>=0);
    assert(preview_reads==cache_reads);
    puts("HISTORY_PREFETCH_OK adjacent unvisited turn is cached from one batch with no extra request");
    // Refill the boundary while keeping the current page and navigation usable.
    codex_prefetch_armed=true;codex_prefetch_due=0;preview_read_recent=true;
    int64_t prefetch_now=esp_timer_get_time()/1000;last_received=prefetch_now;
    codex_prefetch_tick(prefetch_now);
    assert(codex_reading&&codex_read_cache_only&&!codex_nav_pending&&preview_read_offset==-1);
    assert(!strcmp(preview_read_turn,"previous-cached-turn"));
    char visible_turn[64];snprintf(visible_turn,sizeof(visible_turn),"%s",codex_reply_turn);
    cJSON *refill=cJSON_Duplicate(codex_inline_reply,true);
    cJSON_ReplaceItemInObject(item(refill,"replyView"),"turnId",cJSON_CreateString("refill-older"));
    preview_read_view=refill;preview_result_code=200;codex_read_finish(prefetch_now+1000);
    assert(!strcmp(codex_reply_turn,visible_turn)&&codex_cache_turn_index("previous-cached-turn",-1)>=0);
    int after_refill=preview_reads;codex_prefetch_tick(prefetch_now+2000);assert(preview_reads==after_refill);
    // Failure is silent and the same boundary is not repeatedly fetched.
    codex_prefetch_armed=true;codex_prefetch_tick(prefetch_now+2100);assert(codex_reading);
    preview_result_code=503;codex_read_finish(prefetch_now+2200);
    assert(!codex_nav_pending&&!strcmp(codex_reply_turn,visible_turn));
    assert(!strstr(lv_label_get_text(codex_reply_status),"重试"));
    after_refill=preview_reads;codex_prefetch_armed=true;codex_prefetch_tick(prefetch_now+2300);
    assert(preview_reads==after_refill&&!codex_reading);
    // A foreground click while that boundary is being fetched consumes the
    // completed batch, without waiting for a redundant second roundtrip.
    codex_prefetch_armed=true;codex_prefetch_tick(prefetch_now+33000);assert(codex_reading);
    snprintf(codex_reply_selected,sizeof(codex_reply_selected),"refill-older");
    codex_turn_offset=-1;codex_navigation_start();assert(codex_nav_queued);
    refill=cJSON_Duplicate(codex_inline_reply,true);
    cJSON_ReplaceItemInObject(item(refill,"replyView"),"turnId",cJSON_CreateString("refill-clicked"));
    preview_read_view=refill;preview_result_code=200;after_refill=preview_reads;
    codex_read_finish(prefetch_now+34000);
    assert(!strcmp(codex_reply_turn,"refill-clicked")&&!codex_reading&&!codex_nav_pending&&preview_reads==after_refill);
    // Restore the bookmark used by the remaining UI scenarios.
    snprintf(codex_reply_selected,sizeof(codex_reply_selected),"cached-history");
    codex_turn_offset=0;codex_requested_page=0;codex_navigation_start();
    preview_result_code=200;preview_read_recent=false;cache_reads=preview_reads;
    puts("HISTORY_ROLLING_OK boundary refill; silent completion and failure; no idle loop; boundary deduplication");
    // Reopening history resumes the same turn/page without a network roundtrip.
    codex_reply_close(NULL);
    assert(ui_reading_save(codex_bookmarks));memset(codex_bookmarks,0,32*sizeof(*codex_bookmarks));assert(ui_reading_load(codex_bookmarks));
    codex_reply_open(NULL);
    assert(!strcmp(codex_reply_selected,"cached-history")&&codex_reply_page==0);
    assert(preview_reads==cache_reads&&!codex_nav_pending);
    cJSON_ReplaceItemInObject(ready,codex_selected,cJSON_CreateString("new-latest"));
    unsigned chimes_before=preview_chimes;codex_notice_tick(3500,true);
    assert(preview_chimes==chimes_before+1&&!lv_obj_has_flag(codex_unread_dot,LV_OBJ_FLAG_HIDDEN));
    codex_notice_tick(3600,true);assert(preview_chimes==chimes_before+1);
    // Paragraphs, lists and fenced code remain legible without tool records.
    codex_reply_set_text("## 检查结果\n\n1. 连接正常，草稿已经保存。\n2. 历史回复会记住阅读位置。\n\n```c\nint count = 3;\nreturn count;\n```\n\n完成后可继续发送新的问题。");
    lv_obj_update_layout(codex_reply_document);
    assert(lv_obj_get_child_count(codex_reply_document)>=8);
    capture("codex-reply-reading");
    const uint32_t money_symbols[]={0x00a5,0xffe5,0x20ac,0x00a3,0x2014,0x2022};
    const lv_font_t *money_fonts[]={&tab5_font_24,&tab5_font_30};
    for(unsigned f=0;f<2;f++)for(unsigned c=0;c<6;c++) {
        lv_font_glyph_dsc_t glyph={0};
        assert(lv_font_get_glyph_dsc(money_fonts[f],&glyph,money_symbols[c],0));
        assert(glyph.box_w>0&&glyph.box_h>0);
    }
    codex_reply_set_text("## 余额：¥75.37\n\n累计已使用：¥24.62\n\n全角：￥75.37　欧元：€12.50　英镑：£8.00\n\n• 金额与原文一致 — 不替换为方框");
    capture("codex-reply-currency");
    puts("REPLY_CURRENCY_OK both fonts contain halfwidth/fullwidth yen, euro, pound, em dash and bullet");
    // Latest returns instantly from cache too, then revalidates asynchronously.
    lv_obj_send_event(codex_reply_latest,LV_EVENT_CLICKED,NULL);
    assert(!strcmp(lv_label_get_text(codex_reply_text),"最新答复直接显示")&&codex_showing_cache&&codex_reading);
    codex_reading=false;codex_nav_pending=false;codex_reply_selected[0]=0;
    cJSON_Delete(codex_inline_reply);codex_inline_reply=NULL;codex_nav_queued=false;
    codex_reply_refresh(NULL);preview_result_code=429;codex_read_finish(400);assert(codex_read_next==600);
    preview_result_code=-1;codex_reading=false;codex_nav_pending=false;
    printf("CODEX_NAVIGATION_OK queued_clicks_latest_wins_direct_reply_stale_heartbeat_rate_retry_cached_history_instant_latest\n");
    lv_obj_send_event(lv_obj_get_child(codex_reply_overlay,1),LV_EVENT_CLICKED,NULL);
    codex_apply_key("a",0);codex_apply_key("enter",0);codex_apply_key("backspace",0);codex_apply_key("A",0);
    codex_apply_key("x",1);codex_apply_key("x",4);
    assert(!strcmp(lv_textarea_get_text(codex_input),"aA"));
    codex_waiting=true;codex_apply_key("x",0);assert(!strcmp(lv_textarea_get_text(codex_input),"aA"));codex_waiting=false;
    lv_textarea_set_text(codex_input,"");
    last_received=esp_timer_get_time()/1000;
    lv_obj_send_event(codex_voice_button,LV_EVENT_CLICKED,NULL);
    assert(tab5_ui_voice_check==7&&!strcmp(lv_label_get_text(codex_voice_hint),"语音任务未启动，请稍后重试"));
    wifi_connected=false;lv_obj_send_event(codex_voice_button,LV_EVENT_CLICKED,NULL);
    assert(tab5_ui_voice_check==5&&!strcmp(lv_label_get_text(codex_voice_hint),"请连接电脑语音服务"));
    preview_usb_connected=true;device_set_mode(LINK_USB);
    lv_obj_send_event(codex_voice_button,LV_EVENT_CLICKED,NULL);
    assert(tab5_ui_voice_check==7); // USB reaches the voice service stub instead of being blocked by the UI.
    preview_usb_connected=false;device_set_mode(-1);wifi_connected=true;
    page(8);capture("codex-task-selected");
    lv_obj_send_event(codex_input,LV_EVENT_FOCUSED,NULL);
    lv_obj_update_layout(lv_screen_active());capture("codex-keyboard-open");
    assert(!lv_obj_has_flag(codex_keyboard,LV_OBJ_FLAG_HIDDEN));
    assert(!lv_obj_has_flag(codex_ime_panel,LV_OBJ_FLAG_HIDDEN));
    // Use the actual keyboard event chain; past previews never enabled the built-in IME.
    tab5_pinyin_candidate candidates[TAB5_PINYIN_CANDIDATES];
    assert(tab5_pinyin_lookup("nihao",candidates,TAB5_PINYIN_CANDIDATES)>0);
    assert(!strcmp(candidates[0].text,"你好")&&candidates[0].consumed==5);
    assert(tab5_pinyin_lookup("xi'an",candidates,TAB5_PINYIN_CANDIDATES)>0);
    assert(!strcmp(candidates[0].text,"西安")&&candidates[0].consumed==5);
    assert(tab5_pinyin_lookup("ni?hao",candidates,TAB5_PINYIN_CANDIDATES)==0);
    lv_textarea_set_text(codex_input,"");type_spelling("nihao");
    assert(!strcmp(tab5_ime_pending(),"nihao")&&!lv_textarea_get_text(codex_input)[0]);
    capture("codex-pinyin-nihao");choose_word("你好");
    assert(!strcmp(lv_textarea_get_text(codex_input),"你好")&&!tab5_ime_pending()[0]);
    // A single-character choice consumes only its syllable, preserving hao.
    lv_textarea_set_text(codex_input,"");type_spelling("nihao");choose_word("你");
    assert(!strcmp(lv_textarea_get_text(codex_input),"你")&&!strcmp(tab5_ime_pending(),"hao"));
    choose_word("好");assert(!strcmp(lv_textarea_get_text(codex_input),"你好"));
    type_spelling("shijie");assert(press_text(codex_keyboard," "));
    assert(!strcmp(lv_textarea_get_text(codex_input),"你好世界"));
    type_spelling("nihaoq");choose_word("你好");assert(!strcmp(tab5_ime_pending(),"q"));
    assert(press_text(codex_keyboard,LV_SYMBOL_BACKSPACE));assert(!tab5_ime_pending()[0]);
    assert(!strcmp(lv_textarea_get_text(codex_input),"你好世界你好"));
    type_spelling("nihao");assert(press_text(codex_keyboard,LV_SYMBOL_BACKSPACE));
    assert(!strcmp(tab5_ime_pending(),"niha"));type_spelling("o");choose_word("你好");
    capture("codex-pinyin-committed");
    // Switching to English preserves pending spelling and bypasses conversion.
    type_spelling("test");lv_obj_send_event(lv_obj_get_child(codex_ime_panel,0),LV_EVENT_CLICKED,NULL);
    type_spelling("hello");assert(strstr(lv_textarea_get_text(codex_input),"testhello")&&!tab5_ime_pending()[0]);
    lv_obj_send_event(lv_obj_get_child(codex_ime_panel,0),LV_EVENT_CLICKED,NULL);
    lv_textarea_set_text(codex_input,"前后");lv_textarea_set_cursor_pos(codex_input,1);
    type_spelling("nihao");choose_word("你好");assert(!strcmp(lv_textarea_get_text(codex_input),"前你好后"));
    // Pending spelling is saved with its own task, never carried to another draft.
    lv_textarea_set_text(codex_input,"");type_spelling("nihao");
    lv_dropdown_set_selected(codex_project_picker,1);lv_obj_send_event(codex_project_picker,LV_EVENT_VALUE_CHANGED,NULL);
    lv_obj_send_event(codex_task_rows[0],LV_EVENT_CLICKED,NULL);
    assert(!tab5_ime_pending()[0]&&!strcmp(lv_textarea_get_text(codex_input),"项目二的草稿"));
    lv_dropdown_set_selected(codex_project_picker,0);lv_obj_send_event(codex_project_picker,LV_EVENT_VALUE_CHANGED,NULL);
    lv_obj_send_event(codex_task_rows[0],LV_EVENT_CLICKED,NULL);
    assert(!strcmp(lv_textarea_get_text(codex_input),"nihao"));
    lv_textarea_set_text(codex_input,"");
    lv_area_t keyboard_area,page_area;
    lv_obj_get_coords(codex_keyboard,&keyboard_area);
    lv_obj_get_coords(lv_obj_get_parent(codex_keyboard),&page_area);
    assert(keyboard_area.y1<page_area.y1+300&&keyboard_area.y2<=page_area.y2);
    lv_obj_send_event(codex_keyboard,LV_EVENT_CANCEL,NULL);
    assert(lv_obj_has_flag(codex_ime_panel,LV_OBJ_FLAG_HIDDEN));
    lv_obj_send_event(codex_input,LV_EVENT_CLICKED,NULL);
    assert(!lv_obj_has_flag(codex_keyboard,LV_OBJ_FLAG_HIDDEN));
    lv_obj_send_event(codex_keyboard,LV_EVENT_CANCEL,NULL);
    lv_obj_remove_flag(camera_layer,LV_OBJ_FLAG_HIDDEN);camera_tick();capture("camera-overlay-unavailable");
    lv_obj_add_flag(camera_layer,LV_OBJ_FLAG_HIDDEN);
    // Exercise the real microphone click: the anchor must survive focus/page changes,
    // repeated full transcripts, Unicode, cancellation and a second take.
    {
        const struct {const char *base;uint32_t cursor;const char *expected;} cases[]={
            {"前后",0,"语音前后"},{"前后",1,"前语音后"},{"前后",2,"前后语音"},
            {"",0,"语音"},{"A中\n🙂尾",4,"A中\n🙂语音尾"}
        };
        preview_voice_start_enabled=true;
        for(unsigned i=0;i<sizeof(cases)/sizeof(cases[0]);i++) {
            lv_textarea_set_text(codex_input,cases[i].base);
            lv_textarea_set_cursor_pos(codex_input,cases[i].cursor);
            last_received=esp_timer_get_time()/1000;
            lv_obj_send_event(codex_voice_button,LV_EVENT_CLICKED,NULL);
            assert(codex_voice_waiting&&codex_voice_cursor==cases[i].cursor);
            strcpy(preview_voice.source,"tab5");strcpy(preview_voice.phase,"recording");preview_voice.ready=true;
            strcpy(preview_voice.text,"临时");codex_voice_tick();
            lv_obj_send_event(codex_input,LV_EVENT_DEFOCUSED,NULL);
            lv_textarea_set_cursor_pos(codex_input,0);page(0);
            strcpy(preview_voice.text,"语音");codex_voice_tick();codex_voice_tick();
            assert(!strcmp(lv_textarea_get_text(codex_input),cases[i].expected));
            assert(lv_textarea_get_cursor_pos(codex_input)==cases[i].cursor+2);
            preview_voice.busy=false;strcpy(preview_voice.phase,"review");codex_voice_tick();page(8);
            assert(!codex_voice_waiting&&preview_submit_count==0);
        }
        // The next take starts after the previous insertion, not at the draft end.
        last_received=esp_timer_get_time()/1000;
        lv_obj_send_event(codex_voice_button,LV_EVENT_CLICKED,NULL);
        assert(codex_voice_waiting&&codex_voice_cursor==6);
        strcpy(preview_voice.text,"继续");preview_voice.busy=false;strcpy(preview_voice.phase,"review");codex_voice_tick();
        assert(!strcmp(lv_textarea_get_text(codex_input),"A中\n🙂语音继续尾"));
        assert(lv_textarea_get_cursor_pos(codex_input)==8);
        // Cancel restores both draft and the original middle cursor.
        lv_textarea_set_text(codex_input,"前后");lv_textarea_set_cursor_pos(codex_input,1);
        last_received=esp_timer_get_time()/1000;lv_obj_send_event(codex_voice_button,LV_EVENT_CLICKED,NULL);
        assert(codex_voice_waiting);strcpy(preview_voice.text,"取消内容");codex_voice_tick();
        preview_voice.busy=false;strcpy(preview_voice.phase,"cancelled");codex_voice_tick();
        assert(!strcmp(lv_textarea_get_text(codex_input),"前后")&&lv_textarea_get_cursor_pos(codex_input)==1);
        // Empty/error results preserve the draft and cursor, without sending.
        const char *empty_phases[]={"review","error"};
        for(unsigned i=0;i<2;i++) {
            last_received=esp_timer_get_time()/1000;lv_obj_send_event(codex_voice_button,LV_EVENT_CLICKED,NULL);
            assert(codex_voice_waiting);preview_voice.busy=false;strcpy(preview_voice.phase,empty_phases[i]);codex_voice_tick();
            assert(!strcmp(lv_textarea_get_text(codex_input),"前后")&&lv_textarea_get_cursor_pos(codex_input)==1);
        }
        // Reconnect keeps the original insertion anchor while controls are locked.
        last_received=esp_timer_get_time()/1000;lv_obj_send_event(codex_voice_button,LV_EVENT_CLICKED,NULL);
        assert(codex_voice_waiting);strcpy(preview_voice.phase,"recovering");
        strcpy(preview_voice.message,"连接中断，收音已停止；重连后取回文字");codex_voice_tick();
        assert(lv_obj_has_state(codex_input,LV_STATE_DISABLED)&&lv_obj_has_state(codex_voice_button,LV_STATE_DISABLED));
        assert(!lv_obj_has_state(codex_voice_cancel,LV_STATE_DISABLED));
        assert(strstr(lv_label_get_text(codex_voice_hint),"重连后取回"));capture("codex-voice-recovering");
        page(0);lv_textarea_set_cursor_pos(codex_input,0);
        strcpy(preview_voice.text,"恢复");strcpy(preview_voice.phase,"review");preview_voice.busy=false;
        strcpy(preview_voice.message,"已取回识别文字，本段可能不完整，请检查后补录");
        codex_voice_tick();codex_voice_tick();page(8);
        assert(!strcmp(lv_textarea_get_text(codex_input),"前恢复后")&&lv_textarea_get_cursor_pos(codex_input)==3);
        assert(!codex_voice_waiting&&preview_submit_count==0);capture("codex-voice-recovered");
        lv_textarea_set_text(codex_input,"前后");lv_textarea_set_cursor_pos(codex_input,1);
        last_received=esp_timer_get_time()/1000;lv_obj_send_event(codex_voice_button,LV_EVENT_CLICKED,NULL);
        strcpy(preview_voice.phase,"recovering");strcpy(preview_voice.text,"临时");codex_voice_tick();
        lv_obj_send_event(codex_voice_cancel,LV_EVENT_CLICKED,NULL);assert(preview_voice_command==2);
        preview_voice.busy=false;strcpy(preview_voice.phase,"cancelled");codex_voice_tick();
        assert(!strcmp(lv_textarea_get_text(codex_input),"前后")&&lv_textarea_get_cursor_pos(codex_input)==1);
        puts("VOICE_RECOVERY_UI_OK controls_cancel_original_cursor_no_duplicate_no_send");
        char near_limit[1998];memset(near_limit,'a',1997);near_limit[1997]=0;
        lv_textarea_set_text(codex_input,near_limit);lv_textarea_set_cursor_pos(codex_input,1);
        last_received=esp_timer_get_time()/1000;lv_obj_send_event(codex_voice_button,LV_EVENT_CLICKED,NULL);
        assert(codex_voice_waiting);strcpy(preview_voice.text,"中");codex_voice_tick();
        assert(strlen(lv_textarea_get_text(codex_input))==2000&&lv_textarea_get_cursor_pos(codex_input)==2);
        strcpy(preview_voice.text,"中文");codex_voice_tick();
        assert(strlen(lv_textarea_get_text(codex_input))==2000&&strstr(lv_label_get_text(codex_voice_hint),"内容过长"));
        preview_voice.busy=false;strcpy(preview_voice.phase,"cancelled");codex_voice_tick();
        assert(!strcmp(lv_textarea_get_text(codex_input),near_limit)&&lv_textarea_get_cursor_pos(codex_input)==1);
        assert(preview_submit_count==0);preview_voice_start_enabled=false;memset(&preview_voice,0,sizeof(preview_voice));
        puts("VOICE_CURSOR_OK start_middle_end_unicode_revisions_focus_navigation_next_take_cancel_empty_error_limit_no_send");
    }
    // Voice drafts stay local until the user explicitly sends. Existing text survives cancel.
    codex_voice_waiting=true;strcpy(codex_voice_base,"请继续：");ui_text(codex_receipt,"");
    codex_voice_cursor=4;codex_voice_insert_byte=strlen(codex_voice_base);
    strcpy(preview_voice.task_id,codex_selected);preview_voice.busy=true;
    strcpy(preview_voice.source,"dji");strcpy(preview_voice.phase,"recording");
    preview_voice.ready=false;codex_voice_started=0;codex_voice_tick();
    assert(!codex_voice_started&&strstr(lv_label_get_text(codex_voice_hint),"正在准备"));
    assert(lv_obj_has_state(codex_voice_button,LV_STATE_DISABLED));capture("codex-voice-preparing");
    preview_voice.ready=true;
    strcpy(preview_voice.message,"大疆收音中");strcpy(preview_voice.text,"检查异常处理");
    codex_voice_tick();
    assert(!strcmp(lv_textarea_get_text(codex_input),"请继续：检查异常处理"));
    assert(!lv_obj_has_state(codex_voice_switch,LV_STATE_DISABLED));
    codex_apply_key("x",0);assert(!strcmp(lv_textarea_get_text(codex_input),"请继续：检查异常处理"));
    assert(!codex_waiting);capture("codex-voice-dji");
    unsigned commands_before=preview_voice_commands;
    char voice_task[37];snprintf(voice_task,sizeof(voice_task),"%s",codex_selected);
    lv_obj_send_event(codex_input,LV_EVENT_DEFOCUSED,NULL);
    lv_obj_send_event(pages[8],LV_EVENT_CLICKED,NULL);
    codex_voice_tick();
    assert(codex_voice_waiting&&preview_voice_commands==commands_before);
    page(0);codex_voice_tick();
    assert(current_page==0&&codex_voice_waiting&&preview_voice_commands==commands_before);
    // The task list, project picker, notifications and home shortcut may not
    // retarget the take while its original editor is offscreen.
    render_codex();codex_notice_open(NULL);
    lv_obj_send_event(codex_task_rows[1],LV_EVENT_CLICKED,NULL);
    lv_obj_send_event(codex_project_picker,LV_EVENT_VALUE_CHANGED,NULL);
    assert(!strcmp(voice_task,codex_selected)&&preview_submit_count==0);
    page(8);codex_voice_tick();
    assert(codex_voice_waiting&&preview_voice_commands==commands_before);
    assert(!strcmp(lv_textarea_get_text(codex_input),"请继续：检查异常处理"));
    lv_obj_send_event(codex_voice_button,LV_EVENT_CLICKED,NULL);
    assert(preview_voice_commands==commands_before+1&&preview_voice_command==1);
    lv_obj_send_event(codex_voice_cancel,LV_EVENT_CLICKED,NULL);
    assert(preview_voice_commands==commands_before+2&&preview_voice_command==2);

    assert(strstr(lv_label_get_text(codex_voice_hint),"00:00"));
    assert(strstr(lv_label_get_text(codex_voice_hint),"可以说话了"));
    strcpy(preview_voice.phase,"recognizing");codex_voice_tick();
    assert(lv_obj_has_state(codex_voice_button,LV_STATE_DISABLED));
    assert(strstr(lv_label_get_text(codex_voice_hint),"正在识别"));capture("codex-voice-recognizing");
    // This rendering fixture explicitly resumes a ready capture; stop/cancel
    // now clear readiness synchronously in the production-shaped facade.
    preview_voice.ready=true;strcpy(preview_voice.phase,"recording");codex_voice_tick();
    assert(!lv_obj_has_state(codex_voice_button,LV_STATE_DISABLED));
    strcpy(preview_voice.source,"tab5");strcpy(preview_voice.message,"已切换 TAB5，请继续说话");codex_voice_tick();
    assert(lv_obj_has_state(codex_voice_switch,LV_STATE_DISABLED));capture("codex-voice-tab5");
    preview_voice.busy=false;strcpy(preview_voice.phase,"cancelled");codex_voice_tick();
    assert(!codex_voice_waiting&&!strcmp(lv_textarea_get_text(codex_input),"请继续："));
    codex_voice_waiting=true;strcpy(preview_voice.phase,"review");strcpy(preview_voice.message,"文字已返回，请检查后发送");
    page(0);codex_voice_tick();assert(!codex_voice_waiting&&!codex_waiting);
    assert(!strcmp(voice_task,codex_selected)&&preview_submit_count==0);page(8);
    assert(!strcmp(lv_textarea_get_text(codex_input),"请继续：检查异常处理"));capture("codex-voice-review");
    codex_reading=true;lv_obj_send_event(codex_send_button,LV_EVENT_CLICKED,NULL);
    assert(codex_send_queued&&preview_submit_count==0);
    codex_reading=false;preview_submit_enabled=true;preview_result_code=-1;
    last_received=esp_timer_get_time()/1000;
    tick(NULL); // queued click proceeds as soon as the read finishes
    assert(codex_waiting&&preview_submit_count==1&&!strcmp(lv_label_get_text(lv_obj_get_child(codex_send_button,0)),"发送中…"));
    codex_send(NULL);assert(preview_submit_count==1);capture("codex-sending");
    lv_obj_send_event(codex_reply_button,LV_EVENT_CLICKED,NULL);
    assert(!lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN)&&strstr(lv_label_get_text(codex_reply_status),"正在发送"));
    capture("codex-view-during-send");
    lv_obj_send_event(lv_obj_get_child(codex_reply_overlay,1),LV_EVENT_CLICKED,NULL);
    assert(lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN)&&codex_waiting);
    preview_result_code=503;preview_result_text="会话恢复失败，文字已保留";tick(NULL);render_codex();
    assert(!codex_waiting&&strstr(lv_label_get_text(codex_receipt),"会话恢复失败"));
    assert(!strcmp(lv_textarea_get_text(codex_input),"请继续：检查异常处理"));
    tick(NULL);render_codex();assert(strstr(lv_label_get_text(codex_receipt),"会话恢复失败"));capture("codex-send-failed");
    codex_reading=false;preview_result_code=-1;lv_obj_send_event(codex_send_button,LV_EVENT_CLICKED,NULL);
    preview_result_code=202;preview_result_text="已发送，可查看会话动态";tick(NULL);render_codex();
    assert(!lv_textarea_get_text(codex_input)[0]&&strstr(lv_label_get_text(codex_receipt),"已发送"));
    cJSON_ReplaceItemInObject(item(remote,"receipts"),codex_selected,cJSON_CreateString("已交给 Codex"));render_codex();
    assert(!codex_feedback_pinned&&!strcmp(lv_label_get_text(codex_receipt),"已交给 Codex"));
    // Draft/recovery behavior: loss of ack is queried with the original ID.
    codex_reading=false;codex_syncing=false;codex_load_waiting=false;codex_read_next=999999;
    lv_textarea_set_text(codex_input,"保留草稿");
    assert(codex_save_draft()&&!strcmp(preview_checkpoint.text,"保留草稿"));
    preview_checkpoint_ok=false;preview_result_code=-1;
    int before=preview_submit_count;codex_send(NULL);
    assert(!codex_waiting&&preview_submit_count==before); // cannot send without durable request ID
    preview_checkpoint_ok=true;codex_send(NULL);assert(codex_waiting);
    char request[37];snprintf(request,sizeof(request),"%s",preview_checkpoint.request);
    assert(strlen(request)==36);
    preview_result_code=504;preview_result_text="未确认送达";tick(NULL);
    assert(!codex_waiting&&!strcmp(lv_textarea_get_text(codex_input),"保留草稿"));
    codex_send(NULL);assert(codex_waiting&&!strcmp(preview_operation,"receipt")&&!strcmp(request,preview_request_id));
    assert(preview_submit_count==before+1); // query is not another send
    preview_result_code=202;tick(NULL);assert(!lv_textarea_get_text(codex_input)[0]);
    // Active turns expose append + stop; stop leaves the user's next draft intact.
    cJSON *control=cJSON_AddObjectToObject(remote,"control");
    cJSON_AddStringToObject(control,"taskId",codex_selected);cJSON_AddStringToObject(control,"turnId","00000000-0000-4000-8000-000000000123");
    cJSON_AddBoolToObject(control,"running",true);render_codex();
    assert(!strcmp(lv_label_get_text(lv_obj_get_child(codex_send_button,0)),"追加"));
    assert(!lv_obj_has_flag(codex_interrupt_button,LV_OBJ_FLAG_HIDDEN));
    codex_reading=false;preview_result_code=-1;lv_textarea_set_text(codex_input,"下一条草稿");codex_interrupt(NULL);
    assert(codex_waiting&&!strcmp(preview_operation,"interrupt"));
    preview_result_code=202;tick(NULL);assert(!strcmp(lv_textarea_get_text(codex_input),"下一条草稿"));
    codex_reading=false;preview_result_code=-1;codex_send(NULL);assert(!strcmp(preview_operation,"steer"));
    preview_result_code=202;tick(NULL);assert(!lv_textarea_get_text(codex_input)[0]);
    codex_reading=false;lv_textarea_set_text(codex_input,"重启后恢复");assert(codex_save_draft());
    codex_draft *saved=codex_draft_for(codex_selected,false);assert(saved);free(saved->text);memset(saved,0,sizeof(*saved));
    codex_selected[0]=0;codex_set_text("");codex_restore_draft();
    assert(!strcmp(lv_textarea_get_text(codex_input),"重启后恢复"));
    render_codex();capture("codex-draft-recovered");
    visible(codex_reply_overlay,true);render_codex();capture("codex-activity-controls");visible(codex_reply_overlay,false);
    const bool all_links[3]={true,true,true},wifi_only[3]={false,true,false},none_links[3]={false,false,false};
    links_render(all_links,all_links,false);battery_render((battery_status){true,true,100,8200,200});capture("header-usb-charging");
    assert(!strcmp(lv_label_get_text(battery_label),"100")&&!lv_obj_has_flag(battery_charge,LV_OBJ_FLAG_HIDDEN));
    links_render(wifi_only,wifi_only,false);battery_render((battery_status){true,false,15,6840,-200});capture("header-wifi-low-battery");
    links_render(all_links,wifi_only,false);battery_render((battery_status){0});capture("header-link-waiting");
    assert(link_visual_state[LINK_USB]==1&&link_visual_state[LINK_WIFI]==2&&link_visual_state[LINK_BLE]==1);
    assert(lv_color_to_u32(lv_obj_get_style_text_color(link_symbols[LINK_BLE],0))==lv_color_to_u32(lv_color_hex(0xE6B65C)));
    const bool usb_only[3]={true,false,false},without_ble[3]={true,true,false};
    cJSON *standby_frame=cJSON_Parse("{\"connectionHealth\":{\"bleStandby\":true}}");assert(standby_frame);
    assert(link_standby_report(standby_frame,LINK_USB,9000,10000));
    assert(!link_standby_report(standby_frame,LINK_USB,2000,10000));
    assert(!link_standby_report(standby_frame,LINK_USB,11000,10000));
    assert(!link_standby_report(standby_frame,LINK_WIFI,9000,10000));
    assert(!link_standby_report(NULL,LINK_USB,9000,10000));
    links_render(all_links,usb_only,true);assert(link_visual_state[LINK_BLE]==3);
    capture("header-ble-standby");
    links_render(all_links,wifi_only,true);assert(link_visual_state[LINK_BLE]==1);
    links_render(without_ble,usb_only,true);assert(link_visual_state[LINK_BLE]==0);
    links_render(all_links,all_links,true);assert(link_visual_state[LINK_BLE]==2);
    cJSON_Delete(standby_frame);
    puts("BLE_STANDBY_DISPLAY_OK fresh authenticated USB report, physical BLE required; stale/disconnected/active states preserved; no routing refresh");
    assert(!strcmp(lv_label_get_text(battery_label),"--")&&lv_obj_has_flag(battery_charge,LV_OBJ_FLAG_HIDDEN));
    battery_render((battery_status){true,true,-1,8350,1000});
    assert(!strcmp(lv_label_get_text(battery_label),"--"));
    assert(!lv_obj_has_flag(battery_charge,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(battery_fill,LV_OBJ_FLAG_HIDDEN));
    links_render(none_links,all_links,false);assert(link_visual_state[LINK_BLE]==0);battery_render(preview_battery);
    cJSON *before_offline_power=shown;shown=NULL;preview_now_us+=2000000;
    battery_status before_power=preview_battery;preview_battery=(battery_status){true,false,55,7500,-100};
    clock_tick(NULL);assert(!strcmp(lv_label_get_text(battery_label),"55"));
    shown=before_offline_power;preview_battery=before_power;battery_render(preview_battery);
    // Delayed host draft must never replace text typed since the load began.
    for(int i=0;i<80;i++)codex_drafts[i].dirty=false;
    saved=codex_draft_for(codex_selected,false);codex_store_text(saved,"",false);saved->loaded=false;codex_set_text("");
    codex_reading=false;codex_syncing=false;codex_sync_next=0;preview_result_code=-1;
    codex_sync_tick(20000);assert(codex_syncing&&!strcmp(preview_operation,"draft-load"));
    cJSON *draft_view=cJSON_AddObjectToObject(remote,"draftView");
    cJSON_AddStringToObject(draft_view,"taskId",codex_selected);cJSON_AddStringToObject(draft_view,"requestId",codex_sync_request);cJSON_AddStringToObject(draft_view,"text","旧草稿");
    lv_textarea_set_text(codex_input,"刚输入的新内容");preview_result_code=200;codex_sync_tick(20100);
    assert(!strcmp(lv_textarea_get_text(codex_input),"刚输入的新内容")&&saved->dirty);
    // Save completion only clears the exact revision that was sent.
    preview_result_code=-1;codex_sync_tick(25000);assert(codex_syncing&&!strcmp(preview_operation,"draft-save"));
    lv_textarea_set_text(codex_input,"保存期间继续输入");preview_result_code=200;codex_sync_tick(25100);assert(saved->dirty);
    codex_syncing=false;codex_load_waiting=false;
    char selected_before[37];snprintf(selected_before,sizeof(selected_before),"%s",codex_selected);
    snprintf(codex_selected,sizeof(codex_selected),"00000000-0000-4000-8000-000000000999");codex_load_draft();
    assert(codex_save_draft());codex_draft *unloaded=codex_draft_for(codex_selected,false);
    assert(!unloaded->loaded&&!unloaded->dirty); // visiting then leaving cannot erase a host draft
    snprintf(codex_selected,sizeof(codex_selected),"%s",selected_before);codex_load_draft();
    preview_submit_enabled=false;preview_result_code=0;preview_result_text="";
    for(int i=0;i<12;i++)assert(lv_obj_get_parent(stock_rows[i])==market_columns[i%3]);
    page(3);lv_obj_update_layout(lv_screen_active());
    assert(!strcmp(lv_label_get_text(market_titles[0]),"美股"));
    assert(!strcmp(lv_label_get_text(market_titles[1]),"港股"));
    assert(lv_obj_get_scroll_bottom(market_columns[0])>0);
    assert(!lv_obj_has_flag(market_scroll_hint,LV_OBJ_FLAG_HIDDEN));
    capture("markets-scroll");
    lv_obj_t *options=NULL,*settings_parent=settings_panels[2];
    for(unsigned i=0;i<lv_obj_get_child_count(settings_parent);i++) {
        lv_obj_t *candidate=lv_obj_get_child(settings_parent,i),*title=lv_obj_get_child(candidate,0);
        if(title&&lv_obj_check_type(title,&lv_label_class)&&!strcmp(lv_label_get_text(title),"总览显示的模型")){options=candidate;break;}
    }
    assert(options&&lv_obj_get_child_count(options)==14);
    preview_network_workflow();
    page(9);
    for(int i=0;i<SETTINGS_CATEGORIES;i++) {
        lv_obj_send_event(settings_categories[i],LV_EVENT_CLICKED,NULL);
        assert(settings_content==settings_panels[i]);
        assert(lv_obj_get_scroll_y(settings_scroll)==0);
        char output[80];snprintf(output,sizeof(output),"settings-category-%d",i);capture(output);
    }
    settings_select(0);
    for(int i=0;i<SETTINGS_CATEGORIES;i++)ui_text_font(lv_obj_get_child(settings_categories[i],0),&tab5_font_24,0);
    capture("settings-compact-font24");
    for(int i=0;i<SETTINGS_CATEGORIES;i++)ui_text_font(lv_obj_get_child(settings_categories[i],0),&tab5_font_30,0);
    capture("settings-compact-font30");
    settings_select(4);capture("settings-device-info");
    assert(lv_obj_has_flag(save_status,LV_OBJ_FLAG_HIDDEN));
    lv_obj_scroll_to_y(settings_scroll,999,LV_ANIM_OFF);capture("settings-device-info-details");
    assert(lv_obj_get_scroll_y(settings_scroll)>0);settings_select(0);
    puts("DEVICE_INFO_UI_OK fifth category, official capability rows and scrollable details");
    // A plugged-in USB link without fresh authenticated data is waiting,
    // and must not keep a green ready check after the bridge stops.
    const bool setup_connected[]={true,true,true},setup_fresh[]={true,true,true};
    setup_connections(true,setup_connected,setup_fresh,true);
    assert(!strcmp(lv_label_get_text(setup_values[1]),"已就绪"));
    assert(!lv_obj_has_flag(setup_checks[1],LV_OBJ_FLAG_HIDDEN));
    setup_connections(true,setup_connected,none_links,true);
    assert(!strcmp(lv_label_get_text(setup_values[1]),"等待电脑"));
    assert(lv_obj_has_flag(setup_checks[1],LV_OBJ_FLAG_HIDDEN));
    assert(lv_color_eq(lv_obj_get_style_text_color(setup_values[1],0),lv_color_hex(0xE6B65C)));
    setup_connections(false,none_links,none_links,false);
    for(int i=0;i<4;i++)assert(lv_obj_has_flag(setup_checks[i],LV_OBJ_FLAG_HIDDEN));
    assert(!strcmp(lv_label_get_text(setup_values[1]),"未连接"));
    setup_connections(true,setup_connected,setup_fresh,true);settings_select(0);
    capture("settings-connection-status");
    puts("CONNECTION_STATUS_UI_OK USB ready_waiting_disconnected and positive checks");
    settings_select(1);
    int previous_brightness=preview_config.brightness;
    lv_slider_set_value(remote_brightness,62,LV_ANIM_OFF);
    lv_obj_send_event(remote_brightness,LV_EVENT_VALUE_CHANGED,NULL);
    assert(!strcmp(lv_label_get_text(brightness_value),"62%")&&preview_brightness==62&&preview_config.brightness==previous_brightness);
    lv_obj_send_event(remote_brightness,LV_EVENT_RELEASED,NULL);
    assert(preview_config.brightness==62);
    capture("settings-brightness-value");
    lv_slider_set_value(remote_brightness,previous_brightness,LV_ANIM_OFF);
    lv_obj_send_event(remote_brightness,LV_EVENT_RELEASED,NULL);
    assert(preview_sound.volume==35&&!preview_sound.muted);
    lv_slider_set_value(sound_slider,68,LV_ANIM_OFF);
    lv_obj_send_event(sound_slider,LV_EVENT_VALUE_CHANGED,NULL);
    assert(preview_sound.volume==35&&!strcmp(lv_label_get_text(sound_value),"68%"));
    lv_obj_send_event(sound_slider,LV_EVENT_RELEASED,NULL);
    assert(preview_sound.volume==68);
    lv_obj_add_state(sound_mute,LV_STATE_CHECKED);lv_obj_send_event(sound_mute,LV_EVENT_VALUE_CHANGED,NULL);
    assert(preview_sound.muted&&preview_sound.volume==68&&lv_obj_has_state(sound_test,LV_STATE_DISABLED));
    assert(!strcmp(lv_label_get_text(sound_header_icon),LV_SYMBOL_MUTE));
    capture("settings-sound-muted");
    lv_obj_remove_state(sound_mute,LV_STATE_CHECKED);lv_obj_send_event(sound_mute,LV_EVENT_VALUE_CHANGED,NULL);
    assert(device_sound_volume()==68&&!lv_obj_has_state(sound_test,LV_STATE_DISABLED));
    assert(!strcmp(lv_label_get_text(sound_header_icon),LV_SYMBOL_VOLUME_MAX));
    page(0);lv_obj_send_event(sound_header_button,LV_EVENT_CLICKED,NULL);
    assert(current_page==9&&settings_content==settings_panels[1]);
    preview_voice.busy=true;lv_obj_send_event(sound_test,LV_EVENT_CLICKED,NULL);
    assert(strstr(lv_label_get_text(sound_hint),"忙碌"));preview_voice.busy=false;
    lv_obj_send_event(sound_test,LV_EVENT_CLICKED,NULL);assert(!lv_label_get_text(sound_hint)[0]);
    capture("settings-sound");
    preview_sound_save_ok=false;lv_slider_set_value(sound_slider,90,LV_ANIM_OFF);
    lv_obj_send_event(sound_slider,LV_EVENT_RELEASED,NULL);
    assert(preview_sound.volume==68&&lv_slider_get_value(sound_slider)==68&&strstr(lv_label_get_text(sound_hint),"失败"));
    preview_sound_save_ok=true;lv_slider_set_value(sound_slider,0,LV_ANIM_OFF);
    lv_obj_send_event(sound_slider,LV_EVENT_RELEASED,NULL);
    assert(!preview_sound.muted&&!device_sound_volume()&&lv_obj_has_state(sound_test,LV_STATE_DISABLED));
    lv_slider_set_value(sound_slider,35,LV_ANIM_OFF);lv_obj_send_event(sound_slider,LV_EVENT_RELEASED,NULL);
    puts("SOUND_UI_OK release_save_mute_restore_zero_busy_save_failure");
    // Enabling rotation clears a fixed page; ordinary browsing must not persist
    // a fixed page while rotation is enabled. Settings/tasks remain protected.
    ui_preferences cycle_saved=prefs;int64_t clock_saved=preview_now_us;
    prefs.selected=3;prefs.cycle=false;prefs.alerts=false;prefs.saver_minutes=0;
    prefs.interval=10;alert_until=0;
    for(int i=0;i<8;i++){prefs.order[i]=i;prefs.enabled[i]=i<2;}
    display_controls_refresh();lv_obj_send_event(display_choices[0],LV_EVENT_CLICKED,NULL);
    assert(prefs.cycle&&prefs.selected==-1&&display_choice()==0);
    page(0);preview_now_us+=11000000;tick(NULL);assert(current_page==1);
    lv_tabview_set_active(tabs,0,LV_ANIM_OFF);lv_obj_send_event(tabs,LV_EVENT_VALUE_CHANGED,NULL);
    assert(current_page==0&&prefs.selected==-1&&prefs.cycle);
    preview_now_us+=11000000;tick(NULL);assert(current_page==1);
    page(9);preview_now_us+=11000000;tick(NULL);assert(current_page==9);
    lv_obj_send_event(display_choices[1],LV_EVENT_CLICKED,NULL);
    lv_dropdown_set_selected(display_fixed_page,3);lv_obj_send_event(display_fixed_page,LV_EVENT_VALUE_CHANGED,NULL);
    assert(prefs.selected==3&&!prefs.cycle&&display_choice()==1&&current_page==9);
    workbench_return(NULL);assert(current_page==3);
    preview_now_us+=11000000;tick(NULL);assert(current_page==3);
    prefs=cycle_saved;display_controls_refresh();preview_now_us=clock_saved;page(9);
    puts("CYCLE_UI_OK fixed_mode_enable_swipe_resume_settings_pause_explicit_fixed_mode");
    check_display_modes();
    save();
    int64_t feedback_started=preview_now_us/1000;
    settings_feedback_tick(feedback_started+2999);
    assert(!lv_obj_has_flag(save_status,LV_OBJ_FLAG_HIDDEN));
    settings_feedback_tick(feedback_started+3000);
    assert(lv_obj_has_flag(save_status,LV_OBJ_FLAG_HIDDEN));
    settings_feedback("保存失败，请重试",true);
    settings_feedback_tick(feedback_started+60000);
    assert(!lv_obj_has_flag(save_status,LV_OBJ_FLAG_HIDDEN)&&strstr(lv_label_get_text(save_status),"失败"));
    save();settings_feedback_tick(feedback_started+3000);
    assert(lv_obj_has_flag(save_status,LV_OBJ_FLAG_HIDDEN));
    lv_obj_update_layout(lv_screen_active());lv_area_t feedback_bounds;lv_obj_get_coords(save_status,&feedback_bounds);
    for(int i=0;i<SETTINGS_CATEGORIES;i++){lv_area_t b;lv_obj_get_coords(settings_categories[i],&b);assert(feedback_bounds.y1>b.y2);}
    assert(lv_obj_get_y(save_status)+lv_obj_get_height(save_status)<=lv_obj_get_height(pages[9]));
    const char *feedback_examples[]={"已保存到 TAB5","保存失败，请重试","至少保留一页","最多选三个模型","总览模型已保存","已恢复自动选择"};
    for(unsigned i=0;i<sizeof(feedback_examples)/sizeof(feedback_examples[0]);i++){settings_feedback(feedback_examples[i],i==1);lv_obj_update_layout(lv_screen_active());lv_point_t fit;lv_text_get_size(&fit,feedback_examples[i],lv_obj_get_style_text_font(save_status,0),0,0,LV_COORD_MAX,LV_TEXT_FLAG_NONE);assert(fit.x<=lv_obj_get_width(save_status)&&fit.y<=lv_obj_get_height(save_status));}
    settings_feedback("已保存到 TAB5",false);capture("settings-feedback-clear-of-menu");visible(save_status,false);
    puts("SETTINGS_FEEDBACK_LAYOUT_OK six menu entries remain readable; bounded one-line notices below navigation and inside page");
    puts("SETTINGS_FEEDBACK_OK success_expires_failure_persists_success_clears_error");
    // Reordering carries the enabled state with the page, not its old row.
    settings_select(2);lv_obj_send_event(display_choices[0],LV_EVENT_CLICKED,NULL);int first=prefs.order[0],second=prefs.order[1];
    prefs.enabled[first]=false;prefs.enabled[second]=true;
    lv_obj_t *up=NULL;
    for(unsigned i=0;i<lv_obj_get_child_count(display_cycle_options);i++) {
        lv_obj_t *child=lv_obj_get_child(display_cycle_options,i);
        if(lv_obj_check_type(child,&lv_button_class)){up=child;break;}
    }
    assert(up);lv_obj_send_event(up,LV_EVENT_CLICKED,NULL);
    assert(prefs.order[0]==second&&prefs.order[1]==first);
    assert(lv_obj_has_state(cycle_checks[0],LV_STATE_CHECKED)&&!lv_obj_has_state(cycle_checks[1],LV_STATE_CHECKED));
    lv_obj_add_state(cycle_checks[1],LV_STATE_CHECKED);lv_obj_send_event(cycle_checks[1],LV_EVENT_VALUE_CHANGED,NULL);
    assert(prefs.enabled[first]);
    lv_obj_scroll_to_y(settings_scroll,999,LV_ANIM_OFF);capture("settings-order-list");
    settings_select(0);assert(lv_obj_get_scroll_y(settings_scroll)==0);
    // OTA never starts during voice work, nor if the local draft checkpoint fails.
    settings_select(3);preview_ota_available=true;tick(NULL);capture("ota-update-available");
    int expanded_offer_height=lv_obj_get_height(ota_offer_card),expanded_button_y=lv_obj_get_y(ota_button);
    bool saved_usb=preview_usb_connected,saved_wifi=wifi_connected,saved_ble=ble_connected;
    preview_usb_connected=false;wifi_connected=false;ble_connected=true;tick(NULL);
    assert(!lv_obj_has_state(ota_button,LV_STATE_DISABLED)&&!strcmp(lv_label_get_text(lv_obj_get_child(ota_button,0)),"通过蓝牙升级"));
    capture("ota-update-ble");
    ble_connected=false;tick(NULL);
    assert(lv_obj_has_state(ota_button,LV_STATE_DISABLED)&&strstr(lv_label_get_text(ota_hint),"Wi-Fi、USB 或蓝牙"));
    ota_action(NULL);assert(!preview_ota_busy);
    wifi_connected=true;tick(NULL);assert(!lv_obj_has_state(ota_button,LV_STATE_DISABLED)&&strstr(lv_label_get_text(lv_obj_get_child(ota_button,0)),"Wi-Fi"));
    preview_usb_connected=true;tick(NULL);assert(strstr(lv_label_get_text(lv_obj_get_child(ota_button,0)),"Wi-Fi"));
    wifi_connected=false;tick(NULL);assert(strstr(lv_label_get_text(lv_obj_get_child(ota_button,0)),"USB"));
    preview_usb_connected=saved_usb;wifi_connected=saved_wifi;ble_connected=saved_ble;tick(NULL);
    assert(!lv_obj_has_flag(ota_badge,LV_OBJ_FLAG_HIDDEN)&&!lv_obj_has_state(ota_button,LV_STATE_DISABLED));
    assert(!strcmp(lv_label_get_text(ota_offer_title),"0.2.30-ui · 有新固件可用"));
    const char *saved_notes=preview_ota_notes;
    capture_actual_release_notes();

    preview_ota_notes="## 滑动优化\n- 优化四个主页面拖动时的画面连续性\n## 升级说明\n- 按分类展示更新内容，精简备注";
    tick(NULL);capture("ota-update-057-notes");
    preview_ota_notes="## 页面滑动\n- 优化四个主页面之间的左右滑动\n## Codex 额度\n- 剩余超过 25% 绿色，10% 至 25% 黄色，低于 10% 红色\n- 缓存数据显示灰色";
    tick(NULL);capture("ota-update-056-notes");
    preview_ota_notes="升级页首行显示新版本号\n新增更新说明，较长内容可滚动查看\n无新固件时精简提示文字\n保留升级稳定性修复与传输优化\n识别后的内容仍需在 TAB5 检查后手动发送";
    tick(NULL);capture("ota-update-long-notes");
    lv_obj_scroll_to_y(ota_notes_box,999,LV_ANIM_OFF);capture("ota-update-notes-scrolled");
    preview_ota_notes="## 新增功能\n- 声音设置：音量、静音、试听，自动保存\n- 天气：24 小时与未来 7 天预报\n\n## 界面优化\n1. 电池图标内显示数字，闪电表示充电\n2. USB、Wi-Fi、蓝牙采用独立状态图标\n---\n较长说明会自动换行，后续条目可以连续滚动查看；正文保持清晰的段落间距。";
    tick(NULL);capture("ota-update-markdown");
    lv_obj_t *heading=lv_obj_get_child(lv_obj_get_child(ota_notes,0),0);
    assert(!strcmp(lv_label_get_text(heading),"新增功能")&&lv_obj_get_style_text_font(heading,0)==&tab5_font_30);
    lv_obj_t *list=lv_obj_get_child(ota_notes,1);
    assert(!strcmp(lv_label_get_text(lv_obj_get_child(list,0)),"·"));
    lv_obj_scroll_to_y(ota_notes_box,999,LV_ANIM_OFF);lv_obj_update_layout(ota_notes_box);
    int note_scroll=lv_obj_get_scroll_y(ota_notes_box);assert(note_scroll>0);
    tick(NULL);lv_obj_update_layout(ota_notes_box);
    assert(lv_obj_get_child(lv_obj_get_child(ota_notes,0),0)==heading&&lv_obj_get_scroll_y(ota_notes_box)==note_scroll);
    capture("ota-update-markdown-scrolled");
    puts("OTA_NOTES_LAYOUT_OK headings_lists_wrapping_scroll_retained");

    preview_ota_notes="";tick(NULL);capture("ota-update-no-notes");
    preview_ota_notes=saved_notes;tick(NULL);
    codex_voice_waiting=true;ota_action(NULL);assert(!preview_ota_busy);
    codex_voice_waiting=false;codex_waiting=codex_reading=codex_syncing=codex_load_waiting=codex_send_queued=photo_flow=false;
    for(int i=0;i<80;i++){codex_drafts[i].request[0]=0;codex_drafts[i].dirty=false;}
    snprintf(codex_selected,sizeof(codex_selected),"%s",selected_before);
    lv_textarea_set_text(codex_input,"升级前需要保留的新草稿");
    preview_checkpoint_ok=false;ota_action(NULL);assert(!preview_ota_busy);preview_checkpoint_ok=true;
    ota_action(NULL);assert(preview_ota_busy&&!lv_obj_has_flag(ota_layer,LV_OBJ_FLAG_HIDDEN));
    assert(!preview_ota_preparation_presented);
    capture("ota-preparation");assert(preview_ota_preparation_presented);
    int syncs_before_ota=preview_sync_count;codex_sync_next=0;
    tick(NULL);assert(preview_sync_count==syncs_before_ota);
    assert(lv_bar_get_value(ota_version_track)==35&&!strcmp(lv_label_get_text(ota_progress),"正在写入固件 · 写入 35%"));
    capture("ota-progress");
    preview_ota_transfer_status="已接收镜像 6707 / 6707 KB · 传输 3515 KB";tick(NULL);capture("ota-compressed-progress");
    assert(strstr(lv_label_get_text(ota_progress),"镜像 6707 / 6707 KB")&&strstr(lv_label_get_text(ota_progress),"传输 3515 KB"));
    preview_ota_transfer_status="正在写入固件";tick(NULL);
    lv_area_t ota_bar_area,version_area;lv_obj_get_coords(ota_version_track,&ota_bar_area);lv_obj_get_coords(ota_version,&version_area);
    assert(abs((ota_bar_area.x1+ota_bar_area.x2)-(version_area.x1+version_area.x2))<=2);
    assert(lv_obj_get_style_text_align(ota_version,0)==LV_TEXT_ALIGN_CENTER);
    preview_ota_verifying=true;tick(NULL);
    assert(lv_bar_get_value(ota_version_track)==99&&!preview_ota_presented);
    assert(!strcmp(lv_label_get_text(ota_title),"正在校验固件"));
    assert(!strcmp(lv_label_get_text(ota_progress),"正在校验固件 · 0.8 秒"));
    assert(!strstr(lv_label_get_text(ota_progress),"99%"));
    assert(!strcmp(lv_label_get_text(ota_note),"下载与写入完成，校验通过后自动重启"));
    capture("ota-verification");
    preview_ota_completed=true;tick(NULL);
    assert(!preview_ota_presented&&!strcmp(lv_label_get_text(ota_title),"升级完成"));
    assert(!strcmp(lv_label_get_text(ota_version),"版本 0.2.24-ui"));
    assert(lv_bar_get_value(ota_version_track)==100&&!strcmp(lv_label_get_text(ota_progress),"3 秒后自动重启"));
    assert(!lv_obj_has_flag(ota_success_icon,LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_has_flag(ota_note,LV_OBJ_FLAG_HIDDEN));
    capture("ota-complete");assert(preview_ota_presented&&ota_completion_rendered);
    preview_ota_completed=false;preview_ota_presented=false;preview_ota_verifying=false;
    preview_ota_busy=false;preview_ota_available=false;preview_ota_idle="电脑已停止提供固件";tick(NULL);
    assert(lv_obj_has_flag(ota_layer,LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_has_flag(ota_badge,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_state(ota_button,LV_STATE_DISABLED));
    capture("ota-update-unavailable");
    assert(lv_obj_get_height(ota_offer_card)<expanded_offer_height&&lv_obj_get_y(ota_button)<expanded_button_y);
    assert(lv_obj_has_flag(ota_button,LV_OBJ_FLAG_HIDDEN));
    assert(!lv_obj_has_flag(ota_empty_hint,LV_OBJ_FLAG_HIDDEN));
    preview_ota_idle="已是最新版本";tick(NULL);
    assert(!strcmp(lv_label_get_text(ota_offer_title),"当前固件已安装"));
    assert(!lv_label_get_text(ota_hint)[0]&&lv_obj_has_flag(ota_notes_box,LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_has_flag(ota_badge,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_state(ota_button,LV_STATE_DISABLED));
    capture("ota-current-version");
    preview_ota_available=true;tick(NULL);lv_obj_update_layout(ota_offer_card);
    assert(lv_obj_get_height(ota_offer_card)==expanded_offer_height&&lv_obj_get_y(ota_button)==expanded_button_y);
    assert(!lv_obj_has_flag(ota_notes_box,LV_OBJ_FLAG_HIDDEN));
    assert(!lv_obj_has_flag(ota_button,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(ota_empty_hint,LV_OBJ_FLAG_HIDDEN));
    preview_ota_available=false;tick(NULL);settings_select(0);
    puts("OTA_COMPACT_LAYOUT_OK unavailable_latest_shrink_offer_restores");
    lv_obj_send_event(flash_entry,LV_EVENT_CLICKED,NULL);tick(NULL);
    assert(!lv_obj_has_flag(flash_layer,LV_OBJ_FLAG_HIDDEN)&&!preview_flash_starts);
    assert(strstr(lv_label_get_text(flash_details),"不包含 USB"));capture("flash-benchmark-idle");
    bool previous_waiting=codex_waiting;codex_waiting=true;
    lv_obj_send_event(flash_start,LV_EVENT_CLICKED,NULL);tick(NULL);
    assert(!preview_flash_starts&&strstr(lv_label_get_text(flash_details),"请先结束"));codex_waiting=previous_waiting;
    lv_obj_send_event(flash_start,LV_EVENT_CLICKED,NULL);tick(NULL);
    assert(preview_flash_busy&&preview_flash_starts==1&&lv_obj_has_state(flash_start,LV_STATE_DISABLED));
    lv_obj_send_event(flash_start,LV_EVENT_CLICKED,NULL);lv_obj_send_event(flash_close,LV_EVENT_CLICKED,NULL);
    assert(preview_flash_starts==1&&!lv_obj_has_flag(flash_layer,LV_OBJ_FLAG_HIDDEN));
    lv_obj_send_event(flash_cancel,LV_EVENT_CLICKED,NULL);tick(NULL);
    assert(preview_flash_busy&&strstr(lv_label_get_text(flash_details),"先恢复"));
    preview_flash_busy=false;preview_flash_status="测试完成 · Flash 123456 · 样本 128 KiB\n8 KiB：700 KiB/s · 写入 182.8 ms · 擦除 150.0 ms\n16 KiB：710 KiB/s · 写入 180.2 ms · 擦除 149.8 ms\n48 KiB：715 KiB/s · 写入 179.0 ms · 擦除 150.1 ms\n64 KiB：内部缓冲不足，跳过\n测试区已恢复，备用固件校验通过";
    tick(NULL);assert(!lv_obj_has_state(flash_close,LV_STATE_DISABLED)&&lv_obj_has_flag(flash_cancel,LV_OBJ_FLAG_HIDDEN));
    capture("flash-benchmark-result-fixture");
    lv_obj_send_event(flash_close,LV_EVENT_CLICKED,NULL);assert(lv_obj_has_flag(flash_layer,LV_OBJ_FLAG_HIDDEN));
    puts("FLASH_BENCHMARK_UI_OK manual_start busy_guard cancel_wait_restore return; result_capture_is_fixture");
    for(int i=5;i<=6;i++) {
        lv_obj_t *box=home_model_checks[i];lv_obj_remove_state(box,LV_STATE_CHECKED);lv_obj_send_event(box,LV_EVENT_VALUE_CHANGED,NULL);
    }
    assert(home_models==(1u<<1)&&preview_saved_models==(1u<<1));
    page(0);capture("home-codex-only");assert(lv_obj_get_width(home_panels[0])==760);
    lv_obj_t *codex_box=home_model_checks[1];
    preview_models_save_fail=true;lv_obj_remove_state(codex_box,LV_STATE_CHECKED);lv_obj_send_event(codex_box,LV_EVENT_VALUE_CHANGED,NULL);
    assert(home_models==(1u<<1)&&lv_obj_has_state(codex_box,LV_STATE_CHECKED));preview_models_save_fail=false;
    for(int i=5;i<=6;i++) {
        lv_obj_t *box=home_model_checks[i];lv_obj_add_state(box,LV_STATE_CHECKED);lv_obj_send_event(box,LV_EVENT_VALUE_CHANGED,NULL);
    }
    const unsigned personal_models=(1u<<1)|(1u<<5)|(1u<<6);
    assert(home_models==personal_models&&preview_saved_models==personal_models);
    lv_obj_add_state(home_model_checks[0],LV_STATE_CHECKED);lv_obj_send_event(home_model_checks[0],LV_EVENT_VALUE_CHANGED,NULL);
    assert(home_models==personal_models&&!lv_obj_has_state(home_model_checks[0],LV_STATE_CHECKED));
    // All eleven providers route to their own detail, including index 2 (Alibaba).
    for(int id=0;id<UI_HOME_PROVIDERS;id++) {
        home_models=1u<<id;overview_domestic();
        lv_obj_send_event(home_panels[0],LV_EVENT_CLICKED,NULL);assert(current_page==1&&selected_quota==id);
        assert(lv_obj_get_width(home_panels[0])==760);
        assert(!lv_obj_has_flag(home_brand_marks[0],LV_OBJ_FLAG_HIDDEN)&&lv_image_get_src(home_brand_marks[0])==home_provider_logos[id]);
        assert(home_provider_logos[id]->header.w==36&&home_provider_logos[id]->header.h==36&&
            home_provider_logos[id]->header.cf==LV_COLOR_FORMAT_ARGB8888&&home_provider_logos[id]->header.stride==144);
        assert(lv_color_eq(lv_obj_get_style_text_color(home_brand_labels[0],0),lv_color_hex(home_provider_colors[id])));
        for(int earlier=0;earlier<id;earlier++)assert(home_provider_colors[id]!=home_provider_colors[earlier]);
        page(0);char provider_capture[64];snprintf(provider_capture,sizeof(provider_capture),"overview-provider-%d",id);capture(provider_capture);
    }
    for(int group=0;group<4;group++) {
        home_models=(1u<<home_provider_order[group*3])|(1u<<home_provider_order[group*3+1])|(1u<<home_provider_order[(group*3+2)%UI_HOME_PROVIDERS]);
        overview_domestic();page(0);char grouped_capture[64];snprintf(grouped_capture,sizeof(grouped_capture),"overview-brand-group-%d",group);capture(grouped_capture);
    }
    cJSON *ark_fixture=cJSON_Duplicate(cJSON_GetArrayItem(details,10),true);
    cJSON_ReplaceItemInArray(details,10,cJSON_CreateObject());home_models=1u<<10;overview_domestic();
    assert(!strcmp(lv_label_get_text(home_captions[0]),"额度采集待接入")&&lv_obj_has_flag(home_bars[0],LV_OBJ_FLAG_HIDDEN));
    page(1);selected_quota=10;render_quota();capture("quota-ark-pending");
    assert(!strcmp(lv_label_get_text(quota_empty),"火山方舟额度采集待接入"));
    page(0);capture("overview-ark-pending");
    cJSON_DeleteItemFromArray(details,10);overview_domestic();assert(home_provider_ids[0]==10&&!strcmp(lv_label_get_text(home_captions[0]),"额度采集待接入"));
    cJSON_AddItemToArray(details,ark_fixture);
    home_models=(1u<<0)|(1u<<3);overview_domestic();page(0);capture("overview-claude-kimi");
    assert(lv_obj_get_width(home_panels[0])==372&&lv_obj_get_width(home_panels[1])==372);
    // New users see only available account data. Empty data is a single prompt,
    // zero balance is available, and stale successful balances stay visible.
    cJSON *home_provider_fixture=cJSON_Duplicate(details,true);
    for(int id=0;id<UI_HOME_PROVIDERS;id++)cJSON_ReplaceItemInArray(details,id,cJSON_CreateObject());
    home_models=0;overview_domestic();page(0);capture("overview-no-models");
    assert(home_provider_ids[0]==-1&&lv_obj_get_width(home_panels[0])==760);
    cJSON *api_fixture=cJSON_GetArrayItem(details,5);cJSON_AddStringToObject(api_fixture,"billingMode","payg");
    cJSON_AddNumberToObject(api_fixture,"balance",0);cJSON_AddStringToObject(api_fixture,"currency","CNY");cJSON_AddBoolToObject(api_fixture,"stale",true);
    overview_domestic();assert(home_provider_ids[0]==5&&!strcmp(lv_label_get_text(home_values[0]),"0.00"));
    cJSON_AddNumberToObject(cJSON_GetArrayItem(details,0),"weekly",24);overview_domestic();
    assert(home_provider_ids[0]==0&&home_provider_ids[1]==5);page(0);capture("overview-auto-two");
    cJSON_ReplaceItemInObject(shown,"quotaDetails",home_provider_fixture);details=item(shown,"quotaDetails");codex=cJSON_GetArrayItem(details,1);
    home_models=personal_models;overview_domestic();
    lv_obj_add_state(home_auto_check,LV_STATE_CHECKED);lv_obj_send_event(home_auto_check,LV_EVENT_VALUE_CHANGED,NULL);
    assert(home_models==0&&preview_saved_models==0&&!lv_obj_has_state(home_model_checks[1],LV_STATE_CHECKED));
    page(9);settings_select(2);
    lv_obj_scroll_to_y(settings_scroll,lv_obj_get_y(display_home_options),LV_ANIM_OFF);
    lv_obj_update_layout(settings_scroll);capture("settings-overview-providers");
    lv_indev_t *model_touch=lv_indev_create();lv_indev_set_type(model_touch,LV_INDEV_TYPE_POINTER);
    lv_indev_set_display(model_touch,lv_display_get_default());lv_indev_set_read_cb(model_touch,camera_touch_read);
    for(int id=0;id<UI_HOME_PROVIDERS;id++) {
        lv_obj_t *option=home_model_checks[id],*logo=lv_obj_get_child(option,0);lv_area_t area;
        assert(lv_image_get_src(logo)==(id==1?&brand_openai_option_image:home_provider_logos[id])&&!lv_obj_has_flag(logo,LV_OBJ_FLAG_CLICKABLE));
        for(int large=0;large<2;large++) {
            lv_point_t size;lv_text_get_size(&size,home_provider_names[id],large?&tab5_font_30:&tab5_font_24,0,0,LV_COORD_MAX,LV_TEXT_FLAG_NONE);
            assert(size.x+106<=lv_obj_get_width(option));
        }
        lv_obj_update_layout(option);lv_obj_get_coords(logo,&area);
        lv_area_t option_area;lv_obj_get_coords(option,&option_area);
        int marker_height=lv_font_get_line_height(lv_obj_get_style_text_font(option,LV_PART_MAIN))+
            lv_obj_get_style_pad_top(option,LV_PART_INDICATOR)+lv_obj_get_style_pad_bottom(option,LV_PART_INDICATOR);
        int marker_center_twice=2*(option_area.y1+lv_obj_get_style_pad_top(option,LV_PART_MAIN)+lv_obj_get_style_border_width(option,LV_PART_MAIN))+marker_height-1;
        assert(abs(area.y1+area.y2-marker_center_twice)<=1);
        int marker_right=option_area.x1+lv_obj_get_style_pad_left(option,LV_PART_MAIN)+lv_obj_get_style_border_width(option,LV_PART_MAIN)+
            lv_font_get_line_height(lv_obj_get_style_text_font(option,LV_PART_MAIN))+
            lv_obj_get_style_pad_left(option,LV_PART_INDICATOR)+lv_obj_get_style_pad_right(option,LV_PART_INDICATOR)-1;
        assert(area.x1-marker_right>=16);
        lv_point_t center={(area.x1+area.x2)/2,(area.y1+area.y2)/2};
        gallery_pointer_click(model_touch,center);assert(home_models==(1u<<id)&&preview_saved_models==(1u<<id));
        gallery_pointer_click(model_touch,center);assert(home_models==0&&preview_saved_models==0);
    }
    lv_anim_delete(model_touch,NULL);lv_indev_reset(model_touch,NULL);lv_indev_delete(model_touch);
    puts("OVERVIEW_OPTION_LOGOS_OK eleven_matching_logos, checkbox_and_logo_centers_within_half_pixel, both_font_sizes_fit, real_logo_touch_selects_and_deselects");
    home_models=personal_models;overview_domestic();
    puts("OVERVIEW_PROVIDER_UI_OK eleven_provider_routes, auto_available_only, zero_and_stale_balance, one_two_three_equal_columns, max_three, failed_save_rollback, no_codex_system_fallback");
    assert(!strcmp(lv_label_get_text(network_ticks[4]),"0"));
    selected_quota=2;lv_dropdown_set_selected(quota_picker,2);
    cJSON *domestic=cJSON_GetArrayItem(details,2);
    cJSON_AddNumberToObject(domestic,"planPercent",64);
    cJSON_AddStringToObject(domestic,"planReset","10-01 00:00");
    render_quota();page(1);capture("quota-three-windows");
    for(int i=0;i<3;i++)for(int cut=0;cut<6;cut++) {
        int parts=i==1?7:5;
        assert(lv_obj_has_flag(quota_bar_dividers[i][cut],LV_OBJ_FLAG_HIDDEN)==(cut>=parts-1));
        if(cut<parts-1)assert(lv_obj_get_x(quota_bar_dividers[i][cut])==(cut+1)*700/parts-1);
    }
    assert(lv_obj_has_flag(quota_time_markers[2],LV_OBJ_FLAG_HIDDEN));
    // Package percentages are divided consistently even when the provider has
    // no reliable package duration. Switching providers must not retain cuts
    // from a previously visible short or weekly window.
    for(int id=2;id<UI_HOME_PROVIDERS;id++) {
        cJSON *saved=cJSON_Duplicate(cJSON_GetArrayItem(details,id),true);
        cJSON *package=cJSON_CreateObject();
        cJSON_AddStringToObject(package,"plan","Token Plan");
        cJSON_AddNumberToObject(package,"planPercent",64);
        cJSON_ReplaceItemInArray(details,id,package);selected_quota=id;lv_dropdown_set_selected(quota_picker,id);render_quota();
        assert(!lv_obj_has_flag(quota_bars[2],LV_OBJ_FLAG_HIDDEN));
        assert(lv_obj_has_flag(quota_time_markers[2],LV_OBJ_FLAG_HIDDEN));
        for(int cut=0;cut<6;cut++) {
            assert(lv_obj_has_flag(quota_bar_dividers[2][cut],LV_OBJ_FLAG_HIDDEN)==(cut>=4));
            if(cut<4)assert(lv_obj_get_x(quota_bar_dividers[2][cut])==(cut+1)*140-1);
            assert(lv_obj_has_flag(quota_bar_dividers[0][cut],LV_OBJ_FLAG_HIDDEN));
            assert(lv_obj_has_flag(quota_bar_dividers[1][cut],LV_OBJ_FLAG_HIDDEN));
        }
        if(id==4)capture("quota-token-plan-segments");
        cJSON_DeleteItemFromObject(package,"planPercent");render_quota();
        for(int cut=0;cut<6;cut++)assert(lv_obj_has_flag(quota_bar_dividers[2][cut],LV_OBJ_FLAG_HIDDEN));
        cJSON_ReplaceItemInArray(details,id,saved);
    }
    selected_quota=2;lv_dropdown_set_selected(quota_picker,2);domestic=cJSON_GetArrayItem(details,2);render_quota();
    puts("QUOTA_PACKAGE_SEGMENTS_OK nine domestic provider routes, five equal percentage segments, three independent bars, unknown durations hidden, missing quota clears cuts");
    {
        cJSON *saved=cJSON_Duplicate(domestic,true);
        cJSON_AddNumberToObject(domestic,"primaryWindowSeconds",7*3600);
        cJSON_AddNumberToObject(domestic,"primaryResetEpoch",quota_now+7*1800);
        cJSON_AddNumberToObject(domestic,"weeklyWindowSeconds",7*86400);
        cJSON_AddNumberToObject(domestic,"weeklyResetEpoch",quota_now+4*86400);
        cJSON_AddNumberToObject(domestic,"planWindowSeconds",28*86400);
        cJSON_AddNumberToObject(domestic,"planResetEpoch",quota_now+14*86400);
        render_quota();lv_obj_update_layout(lv_screen_active());
        for(int i=0;i<3;i++)assert(!lv_obj_has_flag(quota_time_markers[i],LV_OBJ_FLAG_HIDDEN));
        assert(!strcmp(lv_label_get_text(quota_rows[0]),"7 小时已使用"));
        for(int cut=0;cut<6;cut++)assert(!lv_obj_has_flag(quota_bar_dividers[0][cut],LV_OBJ_FLAG_HIDDEN));
        int short_x=lv_obj_get_x(quota_time_markers[0]);
        int package_x=lv_obj_get_x(quota_time_markers[2]);
        quota_countdown(quota_now+60);lv_obj_update_layout(lv_screen_active());
        assert(lv_obj_get_x(quota_time_markers[0])-short_x>=1&&lv_obj_get_x(quota_time_markers[0])-short_x<=2);
        quota_countdown(quota_now+3600);lv_obj_update_layout(lv_screen_active());
        assert(lv_obj_get_x(quota_time_markers[2])-package_x>=1&&lv_obj_get_x(quota_time_markers[2])-package_x<=2);
        cJSON_ReplaceItemInObject(domestic,"planWindowSeconds",cJSON_CreateNumber(31*86400));
        quota_countdown(quota_now);lv_obj_update_layout(lv_screen_active());
        assert(lv_obj_get_x(quota_time_markers[2])>package_x+30);
        cJSON_AddBoolToObject(domestic,"stale",true);render_quota();
        assert(!lv_obj_has_flag(quota_time_markers[2],LV_OBJ_FLAG_HIDDEN));
        capture("quota-time-domestic-periods");
        cJSON_DeleteItemFromObject(domestic,"planWindowSeconds");quota_countdown(quota_now);
        assert(lv_obj_has_flag(quota_time_markers[2],LV_OBJ_FLAG_HIDDEN));
        assert(!lv_obj_has_flag(quota_time_markers[0],LV_OBJ_FLAG_HIDDEN));
        cJSON_ReplaceItemInObject(domestic,"primaryResetEpoch",cJSON_CreateNumber(quota_now));quota_countdown(quota_now);
        assert(lv_obj_has_flag(quota_time_markers[0],LV_OBJ_FLAG_HIDDEN));
        assert(!lv_obj_has_flag(quota_time_markers[1],LV_OBJ_FLAG_HIDDEN));
        cJSON_ReplaceItemInArray(details,2,saved);domestic=cJSON_GetArrayItem(details,2);render_quota();
        puts("QUOTA_DOMESTIC_TIME_OK explicit seven-hour minute movement, independent weekly and 28/31-day package progress, cached current windows, unknown and expired markers hidden");
    }
    lv_area_t reset_bounds,reset_parent;
    lv_obj_get_coords(quota_resets,&reset_bounds);
    lv_obj_get_content_coords(lv_obj_get_parent(quota_resets),&reset_parent);
    assert(reset_bounds.y2<=reset_parent.y2);
    cJSON_AddStringToObject(domestic,"billingMode","payg");
    lv_dropdown_set_selected(lv_obj_get_child(lv_obj_get_parent(quota_plan),0),6);
    cJSON_ReplaceItemInObject(domestic,"balance",cJSON_CreateNumber(96.72));
    cJSON_AddNumberToObject(domestic,"cost",3.27);
    render_quota();capture("quota-payg");
    assert(!lv_obj_has_flag(payg_panels[0],LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_has_flag(quota_panels[0],LV_OBJ_FLAG_HIDDEN));
    assert(!strcmp(lv_label_get_text(payg_values[0]),"96.72"));
    assert(!strcmp(lv_label_get_text(payg_values[1]),"3.27"));
    assert(!strcmp(lv_label_get_text(quota_plan),"API PAYG"));
    for(int i=0;i<3;i++)for(int cut=0;cut<6;cut++)assert(lv_obj_has_flag(quota_bar_dividers[i][cut],LV_OBJ_FLAG_HIDDEN));
    cJSON_ReplaceItemInObject(domestic,"balance",cJSON_CreateNumber(0));
    cJSON_DeleteItemFromObject(domestic,"cost");render_quota();
    assert(!strcmp(lv_label_get_text(payg_values[0]),"0.00"));
    assert(!strcmp(lv_label_get_text(payg_values[1]),"--"));
    selected_quota=4;
    cJSON *coding=cJSON_GetArrayItem(details,4);
    cJSON_ReplaceItemInObject(coding,"plan",cJSON_CreateString("Coding Plan"));
    cJSON_AddStringToObject(coding,"planExpiry","2026-10-23");
    render_quota();capture("quota-coding-plan");
    assert(!strcmp(lv_label_get_text(quota_plan),"Coding Plan"));
    assert(strstr(lv_label_get_text(quota_resets),"到期日 · 2026-10-23"));
    selected_quota=1;render_quota();
    lv_dropdown_set_selected(lv_obj_get_child(lv_obj_get_parent(quota_plan),0),1);
    assert(lv_obj_has_flag(payg_panels[0],LV_OBJ_FLAG_HIDDEN));
    assert(!lv_obj_has_flag(quota_plan,LV_OBJ_FLAG_HIDDEN));
    page(7);int clock_browse_selected=prefs.selected;bool clock_browse_cycle=prefs.cycle;
    for(int i=1;i<=UI_CLOCK_STYLES;i++) {
        lv_obj_send_event(lv_obj_get_parent(clock_big),LV_EVENT_CLICKED,NULL);
        assert(clock_style==i%UI_CLOCK_STYLES && preview_saved_clock==i%UI_CLOCK_STYLES);
        assert(!saver&&!offline_saver&&prefs.selected==clock_browse_selected&&prefs.cycle==clock_browse_cycle);
        clock_music(10000,true);
        if(i<UI_CLOCK_STYLES) {char name[32];snprintf(name,sizeof(name),"clock-style-%d",i);capture(name);}
    }
    assert(!saver_timed_out(299999)&&saver_timed_out(300000));
    prefs.saver_minutes=0;assert(!saver_timed_out(3600000));prefs.saver_minutes=5;
    preview_now_us=120000000;offline_since=60000;
    lv_display_trigger_activity(NULL);lv_tick_inc(30001);tick(NULL);
    assert(!saver); // More than 30s disconnected must not bypass five minutes.
    prefs.saver_minutes=0;lv_tick_inc(3600000);tick(NULL);assert(!saver);
    prefs.saver_minutes=5;lv_display_trigger_activity(NULL);lv_tick_inc(300000);tick(NULL);assert(saver);
    saver_leave();preview_now_us=10000000;offline_since=-1;
    puts("SCREENSAVER_IDLE_OK offline honors configured timeout and disabled setting; touch/PC wake regression covered");
    assert(!strstr(lv_dropdown_get_options(saver_selector),"自动昼夜")&&!strstr(lv_dropdown_get_options(saver_selector),"暗夜"));
    assert(ui_saver_option_from_style(1)==1&&ui_saver_option_from_style(2)==1&&ui_saver_style_from_option(1)==5);
    assert(saver_year_days(2024)==366&&saver_year_days(2026)==365&&saver_year_days(2100)==365&&saver_year_days(2000)==366);
    unsigned saver_prefs_before=preview_preferences_saves;
    lv_dropdown_set_selected(saver_selector,1);lv_obj_send_event(saver_selector,LV_EVENT_VALUE_CHANGED,NULL);
    assert(saver_style==5&&ui_saver_style_load()==5&&preview_preferences_saves==saver_prefs_before);
    preview_saver_save_fail=true;
    lv_dropdown_set_selected(saver_selector,0);lv_obj_send_event(saver_selector,LV_EVENT_VALUE_CHANGED,NULL);
    assert(saver_style==5&&lv_dropdown_get_selected(saver_selector)==1&&strstr(lv_label_get_text(saver_style_status),"失败"));
    preview_saver_save_fail=false;
    lv_dropdown_set_selected(saver_selector,0);lv_obj_send_event(saver_selector,LV_EVENT_VALUE_CHANGED,NULL);
    lv_dropdown_set_selected(saver_selector,1);lv_obj_send_event(saver_selector,LV_EVENT_VALUE_CHANGED,NULL);
    page(9);settings_select(1);capture("settings-saver-type");
#if TAB5_LOCAL_ART
    settings_select(SETTINGS_STORAGE);
    assert(!lv_obj_has_flag(tf_storage_row,LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_get_parent(tf_storage_row)==settings_panels[SETTINGS_STORAGE]);
    assert(!tf_cache_enabled&&!ui_tf_cache_load()&&lv_dropdown_get_selected(tf_cache_selector)==0);
    assert(!preview_gallery_storage_enabled&&strstr(lv_label_get_text(tf_storage_value),"设备缓存"));
    capture("settings-storage-default");
    preview_tf_cache_save_fail=true;
    lv_dropdown_set_selected(tf_cache_selector,1);lv_obj_send_event(tf_cache_selector,LV_EVENT_VALUE_CHANGED,NULL);
    assert(!tf_cache_enabled&&!preview_gallery_storage_enabled&&lv_dropdown_get_selected(tf_cache_selector)==0);
    assert(strstr(lv_label_get_text(tf_cache_feedback),"失败"));preview_tf_cache_save_fail=false;
    lv_dropdown_set_selected(tf_cache_selector,1);lv_obj_send_event(tf_cache_selector,LV_EVENT_VALUE_CHANGED,NULL);
    assert(tf_cache_enabled&&ui_tf_cache_load()&&preview_gallery_storage_enabled&&lv_dropdown_get_selected(tf_cache_selector)==1);
    lv_dropdown_set_selected(saver_selector,ui_saver_option_from_style(6));lv_obj_send_event(saver_selector,LV_EVENT_VALUE_CHANGED,NULL);tick(NULL);
    lv_obj_update_layout(lv_screen_active());
    assert(!lv_obj_has_flag(tf_storage_row,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(date_settings_row,LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_get_style_y(screen_sound_group,0)==386&&strstr(lv_label_get_text(tf_storage_value),"名画 123/366"));
    capture("settings-tf-library");
    lv_dropdown_set_selected(saver_selector,1);lv_obj_send_event(saver_selector,LV_EVENT_VALUE_CHANGED,NULL);
    assert(!lv_obj_has_flag(tf_storage_row,LV_OBJ_FLAG_HIDDEN)&&lv_obj_get_style_y(screen_sound_group,0)==386);
    for(int style=0;style<=UI_SAVER_COUNTDOWN;style++) {
        lv_dropdown_set_selected(saver_selector,ui_saver_option_from_style(style));lv_obj_send_event(saver_selector,LV_EVENT_VALUE_CHANGED,NULL);
        lv_obj_update_layout(lv_screen_active());
        assert(!lv_obj_has_flag(tf_storage_row,LV_OBJ_FLAG_HIDDEN));
        assert(lv_obj_get_parent(tf_storage_row)==settings_panels[SETTINGS_STORAGE]&&lv_obj_get_y(tf_storage_row)==68);
    }
    lv_dropdown_set_selected(saver_selector,1);lv_obj_send_event(saver_selector,LV_EVENT_VALUE_CHANGED,NULL);
    cJSON *tf_previous_shown=shown;shown=NULL;
    preview_tf_state=0;tick(NULL);assert(!strcmp(lv_label_get_text(tf_card_value),"正在检测 TF 卡"));
    preview_tf_state=-1;tick(NULL);assert(strstr(lv_label_get_text(tf_card_value),"未检测"));
    assert(strstr(lv_label_get_text(tf_storage_value),"使用设备缓存")&&tf_cache_enabled);
    capture("settings-tf-unavailable");
    preview_tf_state=1;tick(NULL);assert(!strcmp(lv_label_get_text(tf_card_value),"已就绪"));
    assert(strstr(lv_label_get_text(tf_storage_value),"名画 123/366"));capture("settings-tf-independent");shown=tf_previous_shown;
    puts("TF_SETTINGS_OK independent storage section, all screensavers/offline detection, default device cache, TF opt-in saved, failed save rolls back, unavailable TF falls back");
    lv_dropdown_set_selected(tf_cache_selector,0);lv_obj_send_event(tf_cache_selector,LV_EVENT_VALUE_CHANGED,NULL);
    assert(!tf_cache_enabled&&!preview_gallery_storage_enabled&&strstr(lv_label_get_text(tf_storage_value),"设备缓存"));
    settings_select(1);
#endif
    int saver_page_before=current_page;
    lv_obj_send_event(saver_preview_button,LV_EVENT_CLICKED,NULL);
    assert(saver&&saver_preview&&preview_brightness==80);
    tick(NULL);assert(saver); // Recent touch must not close a full-screen preview immediately.
    struct tm annual_sample={.tm_year=126,.tm_mon=9,.tm_mday=5,.tm_yday=277,.tm_hour=12,.tm_min=34};
    saver_update(&annual_sample);capture("screensaver-annual-dots");
    assert(annual_days==365&&annual_day==278&&!strcmp(lv_label_get_text(annual_percent),"75%"));
    assert(!strcmp(lv_label_get_text(annual_passed),"已过 277 天")&&!strcmp(lv_label_get_text(annual_remaining),"今天之后，还剩 87 天"));
    annual_sample=(struct tm){.tm_year=124,.tm_mon=1,.tm_mday=29,.tm_yday=59};saver_update(&annual_sample);
    assert(annual_days==366&&annual_day==60&&!strcmp(lv_label_get_text(annual_remaining),"今天之后，还剩 306 天"));
    annual_sample=(struct tm){.tm_year=124,.tm_mon=11,.tm_mday=31,.tm_yday=365};saver_update(&annual_sample);
    assert(!strcmp(lv_label_get_text(annual_remaining),"今天之后，还剩 0 天"));capture("screensaver-annual-year-end");
    annual_sample=(struct tm){.tm_year=125,.tm_mon=0,.tm_mday=1,.tm_yday=0};saver_update(&annual_sample);
    assert(annual_days==365&&annual_day==1&&!strcmp(lv_label_get_text(annual_percent),"0%"));capture("screensaver-annual-new-year");
    assert(preview_preferences_saves==saver_prefs_before); // Time refresh never writes preferences.
    lv_obj_send_event(saver_layer,LV_EVENT_CLICKED,NULL);
    assert(!saver&&!saver_preview&&current_page==saver_page_before&&preview_brightness==80);
    lv_dropdown_set_selected(saver_selector,0);lv_obj_send_event(saver_selector,LV_EVENT_VALUE_CHANGED,NULL);
    assert(saver_style==0&&ui_saver_style_load()==0);
    puts("ANNUAL_SAVER_OK 365/366, century rules, today excluded from elapsed/remaining, year rollover, persistent selection, failed save rollback, preview exit and brightness restore");
    // New styles use the production UI and existing calendar payload, with
    // interactions checked independently from the generated screenshots.
    for(int style=3;style<=3;style++) {
        lv_dropdown_set_selected(saver_selector,ui_saver_option_from_style(style));lv_obj_send_event(saver_selector,LV_EVENT_VALUE_CHANGED,NULL);
        saver_local_time=(struct tm){.tm_year=126,.tm_mon=9,.tm_mday=6,.tm_yday=278,.tm_min=1};saver_has_time=true;
        lv_obj_send_event(saver_preview_button,LV_EVENT_CLICKED,NULL);
        assert(saver&&saver_preview&&preview_brightness==80&&ui_saver_style_load()==style);
        assert(lv_color_to_u32(lv_obj_get_style_bg_color(saver_layer,0))==lv_color_to_u32(lv_color_hex(0)));
        assert(!strcmp(lv_label_get_text(annual_percent),"76%"));
        capture(style==2?"screensaver-annual-night":"screensaver-annual-warm");
        lv_obj_send_event(saver_layer,LV_EVENT_CLICKED,NULL);assert(!saver&&preview_brightness==80);
        saver_enter(false);assert(!saver_preview&&preview_brightness==12);saver_leave();
    }
    lv_dropdown_set_selected(saver_selector,ui_saver_option_from_style(5));lv_obj_send_event(saver_selector,LV_EVENT_VALUE_CHANGED,NULL);
    saver_local_time=(struct tm){.tm_year=126,.tm_mon=9,.tm_mday=6,.tm_yday=278,.tm_hour=6,.tm_min=59};
    lv_obj_send_event(saver_preview_button,LV_EVENT_CLICKED,NULL);
    assert(saver&&saver_preview&&annual_palette==2&&preview_brightness==80);
    struct tm auto_sample=saver_local_time;
    auto_sample.tm_hour=7;auto_sample.tm_min=0;saver_update(&auto_sample);
    assert(annual_palette==1&&saver&&saver_preview&&ui_saver_style_load()==5);
    assert(lv_color_to_u32(lv_obj_get_style_bg_color(saver_layer,0))==lv_color_to_u32(lv_color_hex(0xF1F3F6)));
    assert(!strcmp(lv_label_get_text(annual_percent),"76%"));capture("screensaver-annual-auto-day");
    auto_sample.tm_hour=18;auto_sample.tm_min=59;saver_update(&auto_sample);assert(annual_palette==1);
    auto_sample.tm_hour=19;auto_sample.tm_min=0;saver_update(&auto_sample);
    assert(annual_palette==2&&saver&&saver_preview&&preview_brightness==80&&ui_saver_style_load()==5);
    assert(lv_color_to_u32(lv_obj_get_style_bg_color(saver_layer,0))==lv_color_to_u32(lv_color_hex(0)));
    capture("screensaver-annual-auto-night");
    auto_sample.tm_hour=23;auto_sample.tm_min=59;saver_update(&auto_sample);assert(annual_palette==2);
    auto_sample.tm_hour=0;auto_sample.tm_min=0;saver_update(&auto_sample);assert(annual_palette==2);
    saver_leave();saver_local_time=auto_sample;saver_enter(false);assert(annual_palette==2&&preview_brightness==12);
    auto_sample.tm_hour=7;saver_update(&auto_sample);assert(saver&&!saver_preview&&annual_palette==1&&preview_brightness==12);
    saver_leave();assert(preview_brightness==80&&preview_preferences_saves==saver_prefs_before);
    // Legacy day/night IDs now select automatic mode; warm gray stays manual.
    for(int style=1;style<=3;style++) {
        lv_dropdown_set_selected(saver_selector,ui_saver_option_from_style(style));lv_obj_send_event(saver_selector,LV_EVENT_VALUE_CHANGED,NULL);
        assert(saver_style==(style==3?3:5));
        saver_enter(false);auto_sample.tm_hour=20;saver_update(&auto_sample);assert(annual_palette==(style==3?3:2));
        auto_sample.tm_hour=10;saver_update(&auto_sample);assert(annual_palette==(style==3?3:1));saver_leave();
    }
    puts("ANNUAL_AUTO_SAVER_OK local 07:00/19:00 boundaries, midnight, active preview/idle, legacy day/night map to auto, warm gray retained");
    cJSON *monthly_before=cJSON_DetachItemFromObject(shown,"calendar");
    cJSON_AddItemToObject(shown,"calendar",cJSON_Parse("{\"detail\":{\"holidayYear\":2026,\"holidays\":[{\"start\":101,\"end\":103,\"name\":\"元旦\"},{\"start\":215,\"end\":223,\"name\":\"春节\"},{\"start\":404,\"end\":406,\"name\":\"清明节\"},{\"start\":501,\"end\":505,\"name\":\"劳动节\"},{\"start\":619,\"end\":621,\"name\":\"端午节\"},{\"start\":925,\"end\":927,\"name\":\"中秋节\"},{\"start\":1001,\"end\":1007,\"name\":\"国庆节\"}],\"workdays\":[104,214,228,509,920,1010],\"terms\":[{\"year\":2026,\"dates\":[105,120,204,218,305,320,405,420,505,521,605,621,707,723,807,823,907,923,1008,1023,1107,1122,1207,1222]}]}}"));
    lv_dropdown_set_selected(saver_selector,ui_saver_option_from_style(4));lv_obj_send_event(saver_selector,LV_EVENT_VALUE_CHANGED,NULL);
    lv_obj_send_event(saver_preview_button,LV_EVENT_CLICKED,NULL);
    assert(saver&&saver_preview&&!lv_obj_has_flag(monthly_nav,LV_OBJ_FLAG_HIDDEN));
    for(int m=1;m<=12;m++) {
        lv_obj_send_event(monthly_buttons[m-1],LV_EVENT_CLICKED,NULL);
        assert(saver&&saver_preview&&monthly_rendered_month==m&&preview_brightness==80);
        assert_monthly_cover(&monthly_themes[m-1]);
        assert(lv_image_get_src(monthly_flower)==&monthly_flower_images[m-1]);
        lv_image_decoder_dsc_t flower_decode;
        assert(lv_image_decoder_open(&flower_decode,&monthly_flower_images[m-1],NULL)==LV_RESULT_OK);
        assert(flower_decode.decoded&&flower_decode.decoded->header.w==280&&flower_decode.decoded->header.h==252);
        lv_image_decoder_close(&flower_decode);
        for(int prior=0;prior<m-1;prior++)assert(strcmp(monthly_themes[m-1].quote,monthly_themes[prior].quote));
        assert(lv_obj_has_flag(monthly_status,LV_OBJ_FLAG_HIDDEN));
        int count=0;for(int i=0;i<42;i++)if(!lv_obj_has_flag(monthly_cells[i],LV_OBJ_FLAG_HIDDEN))count++;
        assert(count==cal_days(2026,m));
        char shot[48];snprintf(shot,sizeof(shot),"screensaver-calendar-%02d-preview",m);capture(shot);
    }
    lv_obj_send_event(monthly_buttons[9],LV_EVENT_CLICKED,NULL);
    assert(lv_obj_has_flag(monthly_status,LV_OBJ_FLAG_HIDDEN));assert_monthly_cover(&monthly_themes[9]);
    int october_start=cal_serial(2026,10,1)%7;
    assert(!strcmp(lv_label_get_text(monthly_marks[october_start+9]),"班"));
    assert(!strcmp(lv_label_get_text(monthly_notes[october_start+7]),"寒露"));
    lv_obj_send_event(saver_layer,LV_EVENT_CLICKED,NULL);assert(!saver&&preview_brightness==80);
    saver_enter(false);assert(!saver_preview&&preview_brightness==12&&lv_obj_has_flag(monthly_nav,LV_OBJ_FLAG_HIDDEN));
    assert(monthly_rendered_month==10&&monthly_rendered_term==17&&!lv_obj_has_flag(monthly_lines[0],LV_OBJ_FLAG_HIDDEN));capture("screensaver-calendar-current");
    // Month controls cannot mutate a normal screensaver, even if an old event arrives.
    lv_obj_send_event(monthly_buttons[0],LV_EVENT_CLICKED,NULL);assert(saver&&monthly_rendered_month==10);
    struct tm monthly_sample={.tm_year=126,.tm_mon=10,.tm_mday=1,.tm_yday=304};saver_update(&monthly_sample);assert(monthly_rendered_month==11);assert_monthly_cover(&monthly_themes[10]);
    monthly_sample=(struct tm){.tm_year=127,.tm_mon=0,.tm_mday=1};saver_update(&monthly_sample);
    assert(monthly_rendered_year==2027&&monthly_rendered_month==1&&!lv_obj_has_flag(monthly_status,LV_OBJ_FLAG_HIDDEN)&&strstr(lv_label_get_text(monthly_status),"待更新"));assert_monthly_cover(&monthly_themes[0]);
    monthly_sample=(struct tm){.tm_year=124,.tm_mon=1,.tm_mday=29,.tm_yday=59};saver_update(&monthly_sample);
    int leap_cells=0;for(int i=0;i<42;i++)if(!lv_obj_has_flag(monthly_cells[i],LV_OBJ_FLAG_HIDDEN))leap_cells++;assert(leap_cells==29);
    assert(monthly_rendered_term==-1);assert_monthly_cover(&monthly_themes[1]);
    // Each poem survives both the dated transition and the following days,
    // with exactly the sourced text, complete glyphs, and no clipped columns.
    const int term_dates[]={105,120,204,218,305,320,405,420,505,521,605,621,707,723,807,823,907,923,1008,1023,1107,1122,1207,1222};
    assert(monthly_term_at(2026,1,1)==23); // Previous year's winter solstice.
    unsigned poem_fonts_seen=0;
    for(int i=0;i<24;i++) {
        int serial=cal_serial(2026,term_dates[i]/100,term_dates[i]%100),y,m,d;
        cal_gregorian(serial-1,&y,&m,&d);assert(monthly_term_at(y,m,d)==(i+23)%24);
        for(int after=0;after<2;after++) {
            cal_gregorian(serial+after,&y,&m,&d);
            monthly_sample=(struct tm){.tm_year=y-1900,.tm_mon=m-1,.tm_mday=d};saver_update(&monthly_sample);
            assert(monthly_rendered_term==i);assert_monthly_cover(&monthly_themes[m-1]);
            const lv_font_t *face=lv_obj_get_style_text_font(monthly_lines[0],0);
            poem_fonts_seen|=face==&tab5_calendar_verse_24?1:face==&tab5_calendar_brush_24?2:face==&tab5_calendar_running_28?4:0;
        }
        char shot[64];snprintf(shot,sizeof(shot),"screensaver-poem-%02d",i+1);capture(shot);
        if(i==18) {
            // Same verse, paper, ink and 33px baseline pitch: compare actual
            // LVGL font rasterization rather than scaling a screenshot.
            const lv_font_t *faces[]={&calendar_compare_regular_24,&calendar_compare_running_26,&tab5_calendar_running_28};
            const char *names[]={"calendar-compare-regular-24","calendar-compare-running-26","calendar-compare-running-28"};
            for(int variant=0;variant<3;variant++) {
                assert(faces[variant]->line_height<=33);
                for(int col=0;col<4;col++) {
                    ui_text_font(monthly_lines[col],faces[variant],0);
                    lv_obj_set_style_text_line_space(monthly_lines[col],33-faces[variant]->line_height,0);
                    const char *verse=lv_label_get_text(monthly_lines[col]);
                    for(uint32_t at=0;verse[at];) {
                        uint32_t glyph=lv_text_encoded_next(verse,&at);lv_font_glyph_dsc_t desc;
                        if(glyph!='\n')assert(lv_font_get_glyph_dsc(faces[variant],&desc,glyph,0));
                    }
                    lv_obj_update_layout(monthly_lines[col]);lv_point_t size;
                    lv_text_get_size(&size,verse,faces[variant],0,33-faces[variant]->line_height,LV_COORD_MAX,LV_TEXT_FLAG_NONE);
                    assert(size.x<=lv_obj_get_width(monthly_lines[col]));
                    assert(lv_obj_get_y(monthly_lines[col])+size.y<=365);
                }
                capture(names[variant]);
            }
            for(int col=0;col<4;col++) {
                ui_text_font(monthly_lines[col],monthly_poems[i].font,0);
                lv_obj_set_style_text_line_space(monthly_lines[col],33-monthly_poems[i].font->line_height,0);
            }
            puts("CALENDAR_FONT_COMPARISON_OK same_poem_paper_ink_and_pitch, real_24_26_28_glyphs, no_clipping");
        }
    }
    assert(poem_fonts_seen==7);
    cJSON *term_table=cJSON_GetArrayItem(item(calendar_detail(),"terms"),0);
    cJSON *bad_date=cJSON_GetArrayItem(item(term_table,"dates"),0);
    cJSON_SetNumberValue(bad_date,0);assert(monthly_term_at(2026,10,6)==-1);
    cJSON_SetNumberValue(bad_date,105.5);assert(monthly_term_at(2026,10,6)==-1);
    cJSON_SetNumberValue(bad_date,105);
    assert(monthly_term_at(2027,1,1)==-1);
    puts("SEASONAL_POEMS_OK 24 full poems, term eve/day/following day, January rollover, missing/malformed tables, glyph coverage and text bounds");
    // New computer input wakes the new style and restores the user's brightness.
    cJSON *night_pc=cJSON_Parse("{\"pcInput\":{\"session\":\"saver-test\",\"sequence\":1,\"lastInputTickMs\":1000}}");pc_input_update(night_pc);
    unsigned wakes_before=tab5_pc_wakes;
    cJSON_SetNumberValue(item(item(night_pc,"pcInput"),"sequence"),2);
    cJSON_SetNumberValue(item(item(night_pc,"pcInput"),"lastInputTickMs"),2000);pc_input_update(night_pc);assert(!saver&&preview_brightness==80&&tab5_pc_wakes==wakes_before+1);cJSON_Delete(night_pc);
    tab5_pc_wakes=wakes_before; // Keep this isolated scenario out of the legacy counter assertions below.
    cJSON_DeleteItemFromObject(shown,"calendar");if(monthly_before)cJSON_AddItemToObject(shown,"calendar",monthly_before);
    lv_dropdown_set_selected(saver_selector,0);lv_obj_send_event(saver_selector,LV_EVENT_VALUE_CHANGED,NULL);
    assert(preview_preferences_saves==saver_prefs_before);
    puts("MONTHLY_NIGHT_SAVER_OK twelve months, lunar/terms/official days, normal versus preview controls, month/year rollover, leap day, missing data, brightness and PC wake");
    // Gallery success is atomic; an offline or failed update retains the exact
    // previous frame and its date. Independent categories must never cross over.
#if TAB5_LOCAL_ART
    // No bridge snapshot after a cold boot: a trusted local RTC must still
    // schedule a TF request; an unset RTC explains the date, not missing images.
    cJSON *cold_saved_snapshot=shown;shown=NULL;int cold_saved_style=saver_style;
    bool cold_saved_saver=saver,old_download=preview_gallery_download;
    saver_style=6;saver=true;preview_gallery_download=false;
    preview_gallery_clock_valid=false;unsigned cold_requests=preview_gallery_requests;
    clock_music(preview_now_us/1000,false);assert(preview_gallery_requests==cold_requests);
    assert(strstr(lv_label_get_text(gallery_hint),"校准日期"));
    preview_gallery_clock_valid=true;
    preview_gallery_clock_time=(struct tm){.tm_year=126,.tm_mon=9,.tm_mday=14};
    for(int category=0;category<2;category++) {
        saver_style=6+category;gallery_retry[gallery_slot(category)]=0;
        unsigned before=preview_gallery_requests;clock_music(preview_now_us/1000,false);
        assert(preview_gallery_requests>before);
    }
    preview_gallery_clock_valid=false;saver=cold_saved_saver;saver_style=cold_saved_style;
    preview_gallery_download=old_download;shown=cold_saved_snapshot;
    const char *gallery_fixtures=getenv("TAB5_GALLERY_FIXTURES");
    if(getenv("TAB5_GALLERY_RENDER_BUDGET")) {
        page(0);tab5_draw_cache_drop();lv_obj_update_layout(lv_screen_active());
        for(int p=0;p<4;p++)for(int i=0;i<12;i++)tab5_draw_cache_step(pages[primary_pages[p]]);
        gallery_render_reserve=(size_t)tab5_cache_bytes+tab5_native_cache_bytes;
        assert(gallery_render_reserve>4u*1024u*1024u);
        printf("GALLERY_RENDER_RESIDENT_CACHE bytes=%zu\n",gallery_render_reserve);
    }
    int64_t gallery_clock_before=preview_now_us;
    struct tm gallery_day={.tm_year=126,.tm_mon=9,.tm_mday=6,.tm_yday=278};
    for(int kind=0;kind<2;kind++) {
        saver_style=6+kind;saver_enter(false);gallery_update(&gallery_day);
        preview_gallery_pack_status[kind]=1;clock_tick(NULL);
        assert(strstr(lv_label_get_text(gallery_hint),kind?"书法图库":"名画图库"));
        capture(kind?"gallery-calligraphy-pack-missing":"gallery-painting-pack-missing");
        preview_gallery_pack_status[kind]=2;clock_tick(NULL);
        assert(strstr(lv_label_get_text(gallery_hint),"重新导入"));
        preview_gallery_pack_status[kind]=0;
        int decode_slot=gallery_slot(kind);
        preview_gallery_decode_status[decode_slot]=GALLERY_DECODE_NO_MEMORY;gallery_hint_update(kind);
        assert(strstr(lv_label_get_text(gallery_hint),"解码内存不足"));
        assert(!strstr(lv_label_get_text(gallery_hint),"同步"));
        preview_gallery_decode_status[decode_slot]=GALLERY_DECODE_FAILED;gallery_hint_update(kind);
        assert(strstr(lv_label_get_text(gallery_hint),"解码失败"));
        preview_gallery_decode_status[decode_slot]=GALLERY_DECODE_OK;
        preview_gallery_decode_status[decode_slot^1]=GALLERY_DECODE_NO_MEMORY;gallery_hint_update(kind);
        assert(!strstr(lv_label_get_text(gallery_hint),"解码"));
        preview_gallery_decode_status[decode_slot^1]=GALLERY_DECODE_OK;gallery_show();
        int failed_slot=gallery_slot(kind);unsigned requests_before_failure=preview_gallery_requests;
        const int delays[]={2000,4000,8000,15000,15000};
        for(int failure=0;failure<5;failure++){
            gallery_retry[failed_slot]=preview_now_us/1000+60000;
            preview_gallery_failures=1u<<failed_slot;gallery_update(&gallery_day);
            assert(gallery_retry[failed_slot]==preview_now_us/1000+delays[failure]);
            assert(preview_gallery_requests==requests_before_failure); // No immediate retry storm.
        }
        gallery_failures[failed_slot]=0;gallery_retry[failed_slot]=0;

        assert(!lv_obj_has_flag(gallery_hint,LV_OBJ_FLAG_HIDDEN));
        if(gallery_fixtures) {
            char fixture[1024];snprintf(fixture,sizeof(fixture),"%s/%s.rgb565",gallery_fixtures,kind?"calligraphy":"painting");
            FILE *file=fopen(fixture,"rb");assert(file);uint8_t *pixels=malloc(1280*720*2);assert(pixels);
            assert(fread(pixels,1,1280*720*2,file)==1280*720*2);assert(fgetc(file)==EOF);fclose(file);
            preview_gallery_result=(gallery_image){.pixels=pixels,.category=kind,.frames=2};strcpy(preview_gallery_result.date,"2026-10-06");
            unsigned before_prefetch=preview_gallery_requests;
            gallery_update(&gallery_day);assert(gallery_cache[kind*2].pixels==pixels&&lv_obj_has_flag(gallery_hint,LV_OBJ_FLAG_HIDDEN));
            assert(preview_gallery_requests==before_prefetch+1&&preview_gallery_requested_portrait&&preview_gallery_requested_frame==0);
            const lv_font_t *date_font=lv_obj_get_style_text_font(gallery_date,LV_PART_MAIN);
            for(const char *ch="0123456789-";*ch;ch++) {
                lv_font_glyph_dsc_t glyph;
                assert(lv_font_get_glyph_dsc(date_font,&glyph,(uint32_t)*ch,0)&&!glyph.is_placeholder);
            }
            capture(kind?"screensaver-daily-calligraphy":"screensaver-daily-painting");
            memset(&preview_guide,0,sizeof(preview_guide));strcpy(preview_guide.date,"2026-10-06");strcpy(preview_guide.work_id,"guide-fixture");
            strcpy(preview_guide.title,kind?"书法作品 · 导读":"绘画作品 · 导读");strcpy(preview_guide.author,"作者");strcpy(preview_guide.period,"馆方年代");strcpy(preview_guide.museum,"馆藏机构");
            strcpy(preview_guide.overview,"简介依据馆方资料整理。作品背景与欣赏提示分开展示。");strcpy(preview_guide.looking,"留意作品的线条、留白与构图，保持完整分页。");
            if(kind){strcpy(preview_guide.transcription,"已核实正文：書、畫、雲、龍。");strcpy(preview_guide.explanation,"释文与现代解读分开。");}
            strcpy(gallery_cache[kind*2].work_id,preview_guide.work_id);gallery_show();
            assert(!lv_obj_has_flag(gallery_details_button,LV_OBJ_FLAG_HIDDEN));
            unsigned reader_requests=preview_gallery_requests;int reader_frame=gallery_frame[kind];
            lv_obj_send_event(gallery_details_button,LV_EVENT_CLICKED,NULL);assert(saver&&gallery_details_open);
            assert(lv_obj_has_flag(gallery_chapters[2],LV_OBJ_FLAG_HIDDEN)==!kind);
            lv_obj_send_event(gallery_chapters[1],LV_EVENT_CLICKED,NULL);assert(!strcmp(lv_label_get_text(gallery_reader_body),preview_guide.looking));
            lv_display_trigger_activity(NULL);clock_tick(NULL);assert(saver&&gallery_details_open);
            preview_art_orientation.ready=true;preview_art_orientation.orientation=1;gallery_update(&gallery_day);
            assert(gallery_orientation==1&&preview_gallery_requests==reader_requests&&gallery_frame[kind]==reader_frame);
            assert(lv_obj_get_style_transform_rotation(gallery_reader,0)==900);
            const int reading_directions[]={-1,2,0},reading_angles[]={2700,1800,0};
            for(int pose=0;pose<3;pose++) {
                preview_art_orientation.orientation=reading_directions[pose];gallery_update(&gallery_day);
                assert(gallery_orientation==reading_directions[pose]&&lv_obj_get_style_transform_rotation(gallery_reader,0)==reading_angles[pose]);
                assert(gallery_details_open&&preview_gallery_requests==reader_requests&&gallery_frame[kind]==reader_frame);
            }
            preview_gallery_result=(gallery_image){.pixels=calloc(1,1280*720*2),.category=kind,.frame=(reader_frame+1)%2,.frames=2};assert(preview_gallery_result.pixels);
            strcpy(preview_gallery_result.date,"2026-10-06");
            capture(kind?"gallery-reader-calligraphy":"gallery-reader-painting");
            if(kind){lv_obj_send_event(gallery_chapters[2],LV_EVENT_CLICKED,NULL);assert(strstr(lv_label_get_text(gallery_reader_body),"書"));capture("gallery-reader-transcription");}
            lv_obj_send_event(lv_obj_get_child(gallery_reader,lv_obj_get_child_count(gallery_reader)-1),LV_EVENT_CLICKED,NULL);
            assert(saver&&!gallery_details_open&&gallery_frame[kind]==reader_frame&&!strcmp(gallery_cache[kind*2].date,"2026-10-06"));
            gallery_update(&gallery_day);assert(gallery_frame[kind]==reader_frame&&gallery_cache[kind*2].pixels==pixels&&!preview_gallery_result.pixels);
            free(gallery_take_buffer());
            gallery_orientation=1;gallery_show();lv_obj_send_event(gallery_details_button,LV_EVENT_CLICKED,NULL);
            assert(gallery_details_open&&lv_obj_get_width(gallery_reader)==720);capture(kind?"gallery-reader-calligraphy-portrait":"gallery-reader-painting-portrait");
            gallery_reader_close();check_gallery_guide_catalog(kind);gallery_manual_until=0;gallery_orientation=0;preview_art_orientation.orientation=0;gallery_show();
            strcpy(preview_guide.work_id,"another-work");gallery_show();assert(lv_obj_has_flag(gallery_details_button,LV_OBJ_FLAG_HIDDEN));
            puts("GALLERY_READER_OK identity binding, verified-only transcription, touch activity retained, pinned date/pages, four live orientations, exact return, portrait layout");
            preview_now_us+=61000000;gallery_day.tm_mday=7;
            unsigned requests=preview_gallery_requests;gallery_update(&gallery_day);assert(preview_gallery_requests==requests+1);
            gallery_update(&gallery_day);assert(preview_gallery_requests==requests+1); // Retry is bounded.
            assert(gallery_cache[kind*2].pixels==pixels&&!strcmp(lv_label_get_text(gallery_date),"2026-10-06"));
            gallery_day.tm_mday=6;
            snprintf(fixture,sizeof(fixture),"%s/%s-portrait.rgb565",gallery_fixtures,kind?"calligraphy":"painting");
            file=fopen(fixture,"rb");assert(file);uint8_t *portrait_pixels=malloc(1280*720*2);assert(portrait_pixels);
            assert(fread(portrait_pixels,1,1280*720*2,file)==1280*720*2);assert(fgetc(file)==EOF);fclose(file);
            preview_art_orientation=(art_orientation_state){.ready=true,.orientation=1};
            gallery_update(&gallery_day); // No portrait reply yet: retain successful landscape.
            assert(!lv_obj_has_flag(gallery_view,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(gallery_hint,LV_OBJ_FLAG_HIDDEN));
            preview_gallery_result=(gallery_image){.pixels=portrait_pixels,.category=kind,.frames=2,.portrait=true};strcpy(preview_gallery_result.date,"2026-10-06");
            gallery_update(&gallery_day);
            assert(gallery_cache[kind*2+1].pixels==portrait_pixels&&lv_obj_has_flag(gallery_date,LV_OBJ_FLAG_HIDDEN));
            assert(lv_image_get_rotation(gallery_view)==1800);
            capture(kind?"screensaver-calligraphy-portrait":"screensaver-painting-portrait");
            preview_art_orientation.orientation=-1;gallery_update(&gallery_day);
            assert(lv_image_get_rotation(gallery_view)==0);
            capture(kind?"screensaver-calligraphy-portrait-reverse":"screensaver-painting-portrait-reverse");
            // Test cached rotations before the separate 30-second pagination
            // deadline; an expired page is allowed to reuse the hidden layout.
            gallery_displayed[kind*2]=preview_now_us/1000;
            preview_art_orientation.orientation=0;gallery_update(&gallery_day);
            assert(lv_image_get_rotation(gallery_view)==0&&lv_obj_has_flag(gallery_date,LV_OBJ_FLAG_HIDDEN));
            assert(gallery_cache[kind*2].pixels==pixels);
            capture(kind?"screensaver-calligraphy-landscape-restored":"screensaver-painting-landscape-restored");
            gallery_displayed[kind*2]=preview_now_us/1000;
            unsigned before_invert=preview_gallery_requests;
            preview_art_orientation.orientation=2;gallery_update(&gallery_day);
            assert(gallery_slot(kind)==kind*2&&gallery_cache[kind*2].pixels==pixels);
            assert(lv_image_get_rotation(gallery_view)==1800&&lv_obj_get_style_transform_rotation(gallery_date,0)==1800);
            assert(preview_gallery_requests==before_invert); // Reuse the exact landscape frame.
            capture(kind?"screensaver-calligraphy-landscape-inverted":"screensaver-painting-landscape-inverted");
            preview_art_orientation.orientation=0;gallery_update(&gallery_day);
            assert(lv_image_get_rotation(gallery_view)==0&&lv_obj_get_style_transform_rotation(gallery_date,0)==0);
            capture(kind?"screensaver-calligraphy-landscape-upright":"screensaver-painting-landscape-upright");
            strcpy(preview_guide.work_id,gallery_cache[kind*2].work_id);
            strcpy(gallery_cache[kind*2+1].work_id,preview_guide.work_id);
            check_gallery_footer(kind);
            // Cover the real sensor-to-layout chain, not only injected direction IDs.
            // The first vector is the user's fixed normal-landscape calibration.
            const int poses[][5]={{16197,-181,-1899,0,0},{-16197,181,-1899,2,1800},
                {100,5600,-15400,1,1800},{100,-4400,-15780,-1,0}};
            art_orientation_filter calibrated={.candidate=3};int64_t sample_ms=100;
            for(unsigned pose=0;pose<sizeof(poses)/sizeof(poses[0]);pose++){
                for(int sample=0;sample<6;sample++,sample_ms+=100)
                    art_orientation_sample(&calibrated,poses[pose][0],poses[pose][1],poses[pose][2],sample_ms);
                assert(calibrated.orientation==poses[pose][3]);
                preview_art_orientation.orientation=calibrated.orientation;gallery_update(&gallery_day);
                assert(gallery_slot(kind)==kind*2+(pose>=2));
                assert(lv_image_get_rotation(gallery_view)==poses[pose][4]);
                assert(gallery_frame[kind]==0);
            }
            preview_art_orientation.orientation=0;gallery_update(&gallery_day);
            // A stale orientation cache must not restart the work at page zero.
            // A queued background tab snap may finish after automatic saver entry.
            // Keep its visible layer and state aligned, then use the production
            // clock timer (rather than directly calling gallery_update).
            assert(saver&&!saver_preview);
            lv_obj_send_event(tabs,LV_EVENT_VALUE_CHANGED,NULL);
            assert(saver&&!lv_obj_has_flag(saver_layer,LV_OBJ_FLAG_HIDDEN));
            // The clock fixture has a different date. Exercise automatic
            // rotation with both cached layouts while offline; online daily
            // refresh is allowed to reuse a stale, non-visible layout.
            preview_gallery_ready=false;
            for(unsigned pose=0;pose<sizeof(poses)/sizeof(poses[0]);pose++) {
                preview_art_orientation.orientation=poses[pose][3];clock_tick(NULL);
                assert(saver&&!saver_preview&&gallery_slot(kind)==kind*2+(pose>=2));
                assert(lv_image_get_rotation(gallery_view)==poses[pose][4]);
            }
            preview_art_orientation.orientation=0;gallery_update(&gallery_day);
            preview_gallery_ready=true;
            puts("AUTO_GALLERY_ROTATION_OK late background tab event, normal saver, production clock timer, four directions");
            gallery_frame[kind]=1;gallery_cache[kind*2+1].frame=1;
            preview_art_orientation.orientation=1;gallery_update(&gallery_day);
            preview_art_orientation.orientation=0;gallery_update(&gallery_day);
            assert(preview_gallery_requested_frame==1&&!preview_gallery_requested_portrait);
            assert(!lv_obj_has_flag(gallery_view,LV_OBJ_FLAG_HIDDEN));
            gallery_frame[kind]=0;gallery_cache[kind*2+1].frame=0;gallery_show();
            int cached=0;for(int i=0;i<4;i++)if(gallery_cache[i].pixels)cached++;assert(cached<=2);
            check_gallery_replacements(kind,&gallery_day);
            // A text-only edition expires warm bitmap identities, preserving
            // the visible image and every cache key of the other category.
            gallery_image before_refresh[4];memcpy(before_refresh,gallery_cache,sizeof(gallery_cache));
            int before_frames[2]={gallery_frame[0],gallery_frame[1]};
            preview_gallery_pack_changes=1u<<kind;gallery_sync_packs(NULL);
            for(int i=0;i<4;i++) {
                assert(gallery_cache[i].pixels==before_refresh[i].pixels);
                if(i/2==kind)assert(!gallery_cache[i].date[0]&&!gallery_retry[i]);
                else assert(!strcmp(gallery_cache[i].date,before_refresh[i].date));
            }
            assert(gallery_frame[kind]==0&&gallery_frame[!kind]==before_frames[!kind]);
            memcpy(gallery_cache,before_refresh,sizeof(gallery_cache));
            gallery_frame[0]=before_frames[0];gallery_frame[1]=before_frames[1];
            puts("GALLERY_UI_PACK_REFRESH_OK affected warm identities expire; bitmap retained; other category unchanged");
        }
        lv_obj_send_event(saver_layer,LV_EVENT_CLICKED,NULL);assert(!saver&&preview_brightness==80);
    }
    preview_now_us=gallery_clock_before;saver_style=0;
    // Under memory pressure a new download reuses an unused bitmap, including
    // another category's cache, while the actual fallback stays displayable.
    for(int i=0;i<4;i++)gallery_drop(i);
    saver_style=6;gallery_orientation=1;
    uint8_t *visible_pixels=calloc(1,1280*720*2),*unused_pixels=calloc(1,1280*720*2);
    assert(visible_pixels&&unused_pixels);
    gallery_cache[0]=(gallery_image){.pixels=visible_pixels};
    gallery_cache[3]=(gallery_image){.pixels=unused_pixels,.category=1,.portrait=true};
    gallery_retry[3]=preview_now_us/1000+30000;
    gallery_descriptor[0]=(lv_image_dsc_t){.header={.magic=LV_IMAGE_HEADER_MAGIC,.cf=LV_COLOR_FORMAT_RGB565,.w=1280,.h=720,.stride=2560},.data_size=1280*720*2,.data=visible_pixels};
    gallery_show();assert(lv_image_get_src(gallery_view)==&gallery_descriptor[0]);
    preview_gallery_ready=false;
    assert(!gallery_schedule(0,"2026-10-06",0,true));
    assert(gallery_cache[0].pixels==visible_pixels&&gallery_cache[3].pixels==unused_pixels);
    preview_gallery_ready=true;
    assert(!gallery_schedule(0,"2026-10-06",0,true)); // Stub models rejected/failed worker.
    assert(preview_gallery_spare==unused_pixels&&!gallery_cache[3].pixels);
    assert(gallery_retry[3]==0); // Revisit must not inherit the evicted layout's cooldown.
    assert(gallery_cache[0].pixels==visible_pixels&&lv_image_get_src(gallery_view)==&gallery_descriptor[0]);
    gallery_show();assert(!lv_obj_has_flag(gallery_view,LV_OBJ_FLAG_HIDDEN));
    for(int i=0;i<4;i++)gallery_drop(i);
    gallery_show();saver_style=0;gallery_orientation=0;
    // Actual scheduler: forbid a third output allocation across page/category changes.
    free(gallery_take_buffer());
    memset(gallery_retry,0,sizeof(gallery_retry));memset(gallery_failures,0,sizeof(gallery_failures));
    gallery_frame[0]=gallery_frame[1]=0;
    preview_gallery_download=true;preview_gallery_allocations=0;
    for(int kind=0;kind<2;kind++){
        saver_style=6+kind;preview_art_orientation.orientation=0;gallery_orientation=0;gallery_show();
        int previous_frame=-1;
        for(int turn=0;turn<12;turn++){
            preview_now_us+=31000000;
            if(turn==4)preview_gallery_fail_download=true;
            for(int tick=0;tick<8;tick++){gallery_update(&gallery_day);preview_now_us+=1000000;}
            if(previous_frame>=0)assert(gallery_frame[kind]==(previous_frame+1)%3);
            previous_frame=gallery_frame[kind];
            const int directions[]={0,1,-1,2};
            for(int pose=0;pose<4;pose++){
                preview_art_orientation.orientation=directions[pose];gallery_update(&gallery_day);
                int slot=gallery_slot(kind);
                assert(gallery_cache[slot].pixels&&gallery_cache[slot].frame==gallery_frame[kind]);
                assert(lv_image_get_src(gallery_view)==&gallery_descriptor[slot]);
                assert(gallery_cache[slot].pixels[1280*720*2-1]==gallery_frame[kind]*4+gallery_cache[slot].portrait+1);
            }
            assert(preview_gallery_allocations==2);
        }
    }
    preview_gallery_download=false;
    for(int i=0;i<4;i++)gallery_drop(i);
    free(gallery_take_buffer());gallery_show();saver_style=0;gallery_orientation=0;
    // Manual paging and reader return must retain the selected page while
    // still fetching its missing orientation immediately, in both directions.
    for(int kind=0;kind<2;kind++)for(int returning=0;returning<2;returning++)for(int portrait=0;portrait<2;portrait++){
        for(int i=0;i<4;i++)gallery_drop(i);free(gallery_take_buffer());
        memset(gallery_retry,0,sizeof(gallery_retry));
        saver_style=6+kind;gallery_orientation=portrait?1:0;gallery_frame[kind]=1;
        int held_slot=kind*2+portrait;uint8_t *held_pixels=calloc(1,1280*720*2);assert(held_pixels);
        gallery_cache[held_slot]=(gallery_image){.pixels=held_pixels,.category=kind,.frame=1,.frames=3,.portrait=portrait};
        strcpy(gallery_cache[held_slot].date,"2026-10-06");
        gallery_descriptor[held_slot]=(lv_image_dsc_t){.header={.magic=LV_IMAGE_HEADER_MAGIC,.cf=LV_COLOR_FORMAT_RGB565,.w=1280,.h=720,.stride=2560},.data_size=1280*720*2,.data=held_pixels};
        gallery_show();gallery_manual_until=preview_now_us/1000+30000;gallery_return_hold=returning;
        gallery_reading_category=kind;gallery_reading_frame=1;strcpy(gallery_reading->date,"2026-10-06");
        preview_gallery_download=true;preview_gallery_allocations=1;preview_art_orientation.ready=true;
        preview_art_orientation.orientation=portrait?0:-1;
        unsigned before_orientation=preview_gallery_requests;gallery_update(&gallery_day);
        assert(preview_gallery_requests==before_orientation+1&&preview_gallery_requested_frame==1&&preview_gallery_requested_portrait==!portrait);
        gallery_update(&gallery_day);
        int target=kind*2+!portrait;
        assert(gallery_shown_slot(kind)==target&&gallery_cache[target].frame==1&&!strcmp(gallery_cache[target].date,"2026-10-06"));
        assert(gallery_frame[kind]==1&&preview_gallery_allocations==2&&preview_gallery_requests==before_orientation+1);
        // With the requested page visible, proactively warm a missing reverse
        // layout during the same hold. The actual clock timer then rotates
        // without starting any download or changing the selected page/date.
        gallery_drop(held_slot);unsigned before_warm=preview_gallery_requests;
        gallery_update(&gallery_day);
        assert(preview_gallery_requests==before_warm+1&&preview_gallery_requested_frame==1&&preview_gallery_requested_portrait==portrait);
        gallery_update(&gallery_day);
        preview_art_orientation.orientation=portrait?1:0;
        bool was_saver=saver;saver=true;clock_tick(NULL);saver=was_saver;
        assert(gallery_shown_slot(kind)==held_slot&&preview_gallery_requests==before_warm+1&&gallery_frame[kind]==1&&preview_gallery_allocations==2);
        preview_gallery_ready=false;unsigned before_page=preview_gallery_requests;
        lv_obj_send_event(gallery_next,LV_EVENT_CLICKED,NULL);
        assert(gallery_pending_page.date[0]&&gallery_pending_page.frame==2&&preview_gallery_requests==before_page);
        preview_gallery_ready=true;gallery_update(&gallery_day);
        assert(gallery_pending_page.date[0]&&preview_gallery_requested_frame==2);
        gallery_update(&gallery_day);assert(!gallery_pending_page.date[0]&&gallery_frame[kind]==2&&preview_gallery_allocations==2);
        gallery_update(&gallery_day);assert(!preview_gallery_result.pixels&&gallery_frame[kind]==2);
        gallery_manual_until=0;gallery_return_hold=false;preview_gallery_download=false;
    }
    for(int i=0;i<4;i++)gallery_drop(i);free(gallery_take_buffer());
    gallery_show();saver_style=0;gallery_orientation=0;preview_art_orientation.orientation=0;
    puts("GALLERY_MANUAL_ORIENTATION_OK eight missing-layout transitions during page and reader-return holds, same date/page, two output buffers");
    puts("GALLERY_ROTATION_PREFETCH_OK eight manual-hold reverse layouts warmed before rotation; clock timer applies cached layout with zero new requests");
    puts("GALLERY_PREFETCH_INPUT_OK eight busy-worker page taps retained and applied when idle without a third output buffer");
    // Physical footer hit testing, including both inverted orientations.
    const int paging_directions[]={0,2,1,-1};
    lv_indev_t *paging_touch=lv_indev_create();lv_indev_set_type(paging_touch,LV_INDEV_TYPE_POINTER);
    lv_indev_set_display(paging_touch,lv_display_get_default());lv_indev_set_read_cb(paging_touch,camera_touch_read);
    bool paging_saver=saver;saver=true;visible(saver_layer,true);visible(gallery_layer,true);
    strcpy(preview_guide.date,"2026-10-06");strcpy(preview_guide.work_id,"paging-fixture");
    for(int legacy=0;legacy<2;legacy++)for(int kind=0;kind<2;kind++)for(int pose=0;pose<4;pose++){
        for(int i=0;i<4;i++)gallery_drop(i);free(gallery_take_buffer());
        memset(gallery_retry,0,sizeof(gallery_retry));gallery_pending_page=(gallery_image){0};
        strcpy(preview_guide.work_id,legacy?"":"paging-fixture");
        int direction=paging_directions[pose];bool portrait=pose>=2;
        saver_style=6+kind;gallery_orientation=direction;gallery_last_portrait=portrait?direction:gallery_last_portrait;
        preview_art_orientation.orientation=direction;gallery_frame[kind]=1;gallery_manual_until=preview_now_us/1000+30000;
        int slot=kind*2+portrait;uint8_t *pixels=calloc(1,1280*720*2);assert(pixels);
        const char *legacy_fixtures=getenv("TAB5_GALLERY_LEGACY_FIXTURES");
        if(legacy&&legacy_fixtures) {
            char path[1024];snprintf(path,sizeof(path),"%s/%s%s.rgb565",legacy_fixtures,kind?"calligraphy":"painting",portrait?"-portrait":"");
            FILE *file=fopen(path,"rb");assert(file&&fread(pixels,1,1280*720*2,file)==1280*720*2&&fgetc(file)==EOF);fclose(file);
        }
        gallery_cache[slot]=(gallery_image){.pixels=pixels,.category=kind,.frame=1,.frames=3,.portrait=portrait};
        strcpy(gallery_cache[slot].date,preview_guide.date);strcpy(gallery_cache[slot].work_id,preview_guide.work_id);
        gallery_descriptor[slot]=(lv_image_dsc_t){.header={.magic=LV_IMAGE_HEADER_MAGIC,.cf=LV_COLOR_FORMAT_RGB565,.w=1280,.h=720,.stride=2560},.data_size=1280*720*2,.data=pixels};
        preview_gallery_allocations=1;preview_gallery_download=true;preview_gallery_ready=false;
        gallery_show();lv_obj_update_layout(gallery_controls);
        assert(!lv_obj_has_flag(gallery_next,LV_OBJ_FLAG_HIDDEN)&&!lv_obj_has_flag(gallery_previous,LV_OBJ_FLAG_HIDDEN));
        assert(lv_obj_has_flag(gallery_controls,LV_OBJ_FLAG_HIDDEN)==!!legacy);
        assert(lv_obj_has_flag(gallery_details_button,LV_OBJ_FLAG_HIDDEN)==!!legacy);
        gallery_cache[slot].frames=1;gallery_show();
        assert(lv_obj_has_flag(gallery_previous,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(gallery_next,LV_OBJ_FLAG_HIDDEN));
        assert(lv_obj_has_flag(gallery_details_button,LV_OBJ_FLAG_HIDDEN)==!!legacy);
        gallery_cache[slot].frames=3;gallery_show();
        int angle=lv_obj_get_style_transform_rotation(gallery_controls,0),width=portrait?720:1280,height=portrait?1280:720;
        bool footer_navigation=!legacy;
        int previous_x=footer_navigation?width/2-88:44,next_x=footer_navigation?width/2+88:width-44;
        int navigation_y=footer_navigation?height-45:(height-(portrait?138:98))/2;
        if(footer_navigation) {
            // Touch targets stay inside the existing metadata rail; the artwork
            // pixels must not change when the two navigation arrows are shown.
            assert(lv_obj_get_width(gallery_previous)==80&&lv_obj_get_height(gallery_previous)==72);
            assert(lv_obj_get_width(gallery_next)==80&&lv_obj_get_height(gallery_next)==72);
            assert(width/2-128>=lv_obj_get_x(gallery_footer_museum)+lv_obj_get_width(gallery_footer_museum));
            assert(width/2+128<=lv_obj_get_x(gallery_details_button));
            visible(gallery_previous,false);visible(gallery_next,false);lv_refr_now(lv_display_get_default());
            uint16_t *without=malloc(sizeof(preview_buffer));assert(without);memcpy(without,preview_buffer,sizeof(preview_buffer));
            visible(gallery_previous,true);visible(gallery_next,true);lv_refr_now(lv_display_get_default());
            for(int y=0;y<height-(portrait?138:98);y++)for(int x=0;x<width;x++) {
                lv_point_t point=gallery_physical_point(x,y,angle);
                if(point.x>=0&&point.x<1280&&point.y>=0&&point.y<720)
                    assert(without[point.y*1280+point.x]==preview_buffer[point.y*1280+point.x]);
            }
            free(without);
        }
        if(legacy) {
            assert(lv_obj_has_flag(gallery_page_count,LV_OBJ_FLAG_HIDDEN));
            visible(gallery_previous,false);visible(gallery_next,false);lv_refr_now(lv_display_get_default());
            uint16_t *without=malloc(sizeof(preview_buffer));assert(without);memcpy(without,preview_buffer,sizeof(preview_buffer));
            visible(gallery_previous,true);visible(gallery_next,true);lv_refr_now(lv_display_get_default());
            // Every pixel of the baked title/author/licence must survive. A
            // click-only test previously missed controls covering that text.
            for(int y=height-(portrait?138:98);y<height;y++)for(int x=0;x<width;x++) {
                lv_point_t point=gallery_physical_point(x,y,angle);
                if(point.x>=0&&point.x<1280&&point.y>=0&&point.y<720)
                    assert(without[point.y*1280+point.x]==preview_buffer[point.y*1280+point.x]);
            }
            free(without);
            char name[96];snprintf(name,sizeof(name),"gallery-legacy-%s-%d",kind?"calligraphy":"painting",direction);capture(name);
            check_gallery_render_budget(kind,direction,false);
        }
        // Hit the expanded invisible corner, outside the former 56x56 target.
        gallery_pointer_click(paging_touch,gallery_physical_point(next_x+(footer_navigation?34:0),navigation_y+(footer_navigation?30:0),angle));
        assert(saver&&gallery_pending_page.frame==2&&gallery_pending_page.date[0]);
        preview_gallery_ready=true;gallery_update(&gallery_day);gallery_update(&gallery_day);
        assert(gallery_frame[kind]==2&&preview_gallery_allocations==2);
        // Drain reverse-layout warming, then tap Previous through the dispatcher.
        gallery_update(&gallery_day);gallery_update(&gallery_day);lv_obj_update_layout(gallery_controls);
        gallery_pointer_click(paging_touch,gallery_physical_point(previous_x-(footer_navigation?34:0),navigation_y-(footer_navigation?30:0),angle));
        gallery_update(&gallery_day);gallery_update(&gallery_day);
        assert(saver&&gallery_frame[kind]==1&&preview_gallery_allocations==2);
        gallery_update(&gallery_day);gallery_update(&gallery_day);
        // An unchanged image (including a reverse-layout warmup) must not
        // manufacture a new full-screen render.
        lv_refr_now(lv_display_get_default());camera_still_flushes=0;
        lv_display_add_event_cb(lv_display_get_default(),camera_still_flush,LV_EVENT_FLUSH_START,NULL);
        gallery_show();lv_refr_now(lv_display_get_default());
        assert(camera_still_flushes==0);
        assert(lv_display_remove_event_cb_with_user_data(lv_display_get_default(),camera_still_flush,NULL)==1);
        preview_gallery_ready=false;
        gallery_pointer_click(paging_touch,gallery_physical_point(next_x,navigation_y,angle));
        gallery_pointer_click(paging_touch,gallery_physical_point(next_x,navigation_y,angle));
        assert(gallery_pending_page.date[0]&&gallery_pending_page.frame==0);
        preview_gallery_ready=true;gallery_update(&gallery_day);gallery_update(&gallery_day);
        assert(gallery_frame[kind]==0);
        gallery_update(&gallery_day);gallery_update(&gallery_day);
        // The first tap has submitted a result, but the screen still shows
        // page zero when the second tap arrives. Both steps must count.
        gallery_pointer_click(paging_touch,gallery_physical_point(next_x,navigation_y,angle));
        gallery_pointer_click(paging_touch,gallery_physical_point(next_x,navigation_y,angle));
        assert(gallery_pending_page.date[0]&&gallery_pending_page.frame==2);
        gallery_update(&gallery_day);gallery_update(&gallery_day);
        assert(gallery_frame[kind]==2);
        gallery_update(&gallery_day);gallery_update(&gallery_day);
        preview_gallery_download=false;
    }
    strcpy(preview_guide.work_id,"paging-fixture"); // Restore the reader fixture after legacy image-only cases.
    puts("GALLERY_LEGACY_PAGING_OK guide-free Next/Previous and busy-worker intent, both categories/four directions; optional details hidden, original caption pixels unchanged");
    check_gallery_motion_budget(&gallery_day);
    lv_indev_reset(paging_touch,NULL);lv_indev_delete(paging_touch);saver=paging_saver;
    for(int i=0;i<4;i++)gallery_drop(i);free(gallery_take_buffer());
    saver_style=6;preview_gallery_ready=false;gallery_hint_update(0);
    assert(strstr(lv_label_get_text(gallery_hint),"正在同步"));preview_gallery_ready=true;
    saver_style=6;saver=false;unsigned before_prefetch=preview_gallery_prefetch_calls;
    clock_tick(NULL);assert(preview_gallery_prefetch_calls>before_prefetch&&strlen(preview_gallery_prefetch_date)==10);
    saver=paging_saver;saver_style=0;gallery_orientation=0;gallery_manual_until=0;gallery_show();
    puts("GALLERY_POINTER_PAGING_OK sixteen transformed Next/Previous taps, two categories/four directions, blocked worker retained, two RGB buffers");
    puts("GALLERY_REFRESH_IDLE_OK eight unchanged-image presentations cause zero LCD flushes");
    puts("GALLERY_POINTER_BURST_OK 32 transformed rapid taps accumulate across background and foreground waits with two RGB buffers");
    puts("GALLERY_TRANSITION_OK opaque 180 ms band reveal, eight directions/categories, partial/full pixels equal, old buffer retired only on completion, interruption keeps current frame");
    puts("GALLERY_FIRST_LOAD_OK explicit synchronization hint and day-prefetch before saver entry through production clock timer");
    puts("GALLERY_PAGE_CYCLE_OK 24 page advances, 96 orientations, both categories, failed-download retries, exactly two output allocations");
    puts("GALLERY_BUFFER_OWNERSHIP_OK reuse non-visible frame, protect actual fallback, busy request leaves caches untouched, failed request retains visible image");
    puts("GALLERY_UI_OK independent categories, atomic replacement, failed next-day retention, bounded retry, normal touch wake");
    puts("GALLERY_PORTRAIT_UI_OK both directions, unchanged landscape restored, two-frame memory bound");
#else
    assert(UI_SAVER_STYLES==9&&lv_dropdown_get_option_count(saver_selector)==5);
    assert(!strstr(lv_dropdown_get_options(saver_selector),"每日名画")&&!strstr(lv_dropdown_get_options(saver_selector),"每日书法"));
    puts("PUBLIC_GALLERY_EXCLUDED_OK five saver options including date countdown, no painting or calligraphy entry");
#endif
    page(3);int saved_style=clock_style;saver_enter(false);clock_music(12000,true);capture("screensaver");
    assert(saver&&preview_brightness==12&&!lv_obj_has_flag(saver_layer,LV_OBJ_FLAG_HIDDEN));
    struct tm classic_sample={.tm_year=126,.tm_mon=9,.tm_mday=17,.tm_wday=6,.tm_hour=20,.tm_min=48,.tm_sec=36};
    saver_update(&classic_sample);lv_obj_update_layout(saver_layer);
    assert(!strcmp(lv_label_get_text(saver_time),"20:48")&&!strcmp(lv_label_get_text(classic_seconds),"36"));
    assert(strstr(lv_label_get_text(saver_date),"2026 / 10 / 17")&&strstr(lv_label_get_text(saver_date),"周六"));
    lv_area_t time_bounds,seconds_bounds;lv_obj_get_coords(saver_time,&time_bounds);lv_obj_get_coords(classic_seconds,&seconds_bounds);
    assert(time_bounds.x2<seconds_bounds.x1&&seconds_bounds.x2<1280&&time_bounds.y2<454);
    capture("screensaver-classic-refined");
    capture("screensaver-classic-night");
    unsigned classic_writes=preview_preferences_saves;int classic_brightness=preview_brightness;
    const int classic_boundaries[][3]={{6,59,0},{7,0,1},{18,59,1},{19,0,0},{0,0,0}};
    for(int i=0;i<5;i++) {
        classic_sample.tm_hour=classic_boundaries[i][0];classic_sample.tm_min=classic_boundaries[i][1];
        saver_update(&classic_sample);lv_obj_update_layout(saver_layer);
        lv_color_t background=lv_obj_get_style_bg_color(saver_layer,0);
        lv_color_t expected=lv_color_hex(classic_boundaries[i][2]?0xF1F3F6:0x000000);
        assert(lv_color_eq(background,expected));
        assert(preview_brightness==classic_brightness&&preview_preferences_saves==classic_writes);
    }
    classic_sample.tm_hour=8;classic_sample.tm_min=24;saver_update(&classic_sample);capture("screensaver-classic-day");
    puts("CLASSIC_SAVER_DAY_NIGHT_OK 07:00/19:00 live transitions, midnight, brightness and preferences unchanged");
    const int classic_times[][3]={{0,0,0},{11,11,11},{23,59,59}};
    for(int i=0;i<3;i++) {
        classic_sample.tm_hour=classic_times[i][0];classic_sample.tm_min=classic_times[i][1];classic_sample.tm_sec=classic_times[i][2];
        saver_update(&classic_sample);lv_obj_update_layout(saver_layer);
        lv_obj_get_coords(saver_time,&time_bounds);lv_obj_get_coords(classic_seconds,&seconds_bounds);
        assert(time_bounds.x1>0&&time_bounds.x2<seconds_bounds.x1&&seconds_bounds.x2<1280);
        assert(seconds_bounds.x1-time_bounds.x2==33);
    }
    puts("CLASSIC_SAVER_LAYOUT_OK grouped centered time and seconds, date/weekday, midnight and end-of-day bounds");
    classic_sample.tm_hour=8;classic_sample.tm_min=24;classic_sample.tm_sec=0;
    saver_update(&classic_sample);lv_obj_update_layout(saver_layer);
    lv_area_t stable_time,stable_seconds;
    lv_obj_get_coords(saver_time,&stable_time);lv_obj_get_coords(classic_seconds,&stable_seconds);
    for(int second=0;second<60;second++) {
        classic_sample.tm_sec=second;saver_update(&classic_sample);lv_obj_update_layout(saver_layer);
        lv_obj_get_coords(saver_time,&time_bounds);lv_obj_get_coords(classic_seconds,&seconds_bounds);
        assert(time_bounds.x1==stable_time.x1&&time_bounds.x2==stable_time.x2&&time_bounds.y1==stable_time.y1&&time_bounds.y2==stable_time.y2);
        assert(seconds_bounds.x1==stable_seconds.x1&&seconds_bounds.x2==stable_seconds.x2&&seconds_bounds.y1==stable_seconds.y1&&seconds_bounds.y2==stable_seconds.y2);
        lv_point_t text_size;lv_text_get_size(&text_size,lv_label_get_text(classic_seconds),lv_obj_get_style_text_font(classic_seconds,0),lv_obj_get_style_text_letter_space(classic_seconds,0),0,LV_COORD_MAX,LV_TEXT_FLAG_NONE);
        assert(text_size.x<=lv_obj_get_content_width(classic_seconds));
        if(second==0)capture("screensaver-classic-stable-00");
        if(second==11)capture("screensaver-classic-stable-11");
        if(second==59)capture("screensaver-classic-stable-59");
    }
    classic_sample.tm_min=25;classic_sample.tm_sec=0;saver_update(&classic_sample);lv_obj_update_layout(saver_layer);
    lv_obj_get_coords(saver_time,&time_bounds);lv_obj_get_coords(classic_seconds,&seconds_bounds);
    assert(time_bounds.y1!=stable_time.y1&&seconds_bounds.y1-stable_seconds.y1==time_bounds.y1-stable_time.y1);
    puts("CLASSIC_SAVER_SECONDS_STABLE_OK all 60 seconds keep time/seconds bounds fixed, no clipping, minute drift retained");
    lv_obj_update_layout(saver_time);int old_x=lv_obj_get_x(saver_time),old_y=lv_obj_get_y(saver_time);
    clock_music(72000,true);
    lv_obj_update_layout(saver_time);assert(lv_obj_get_x(saver_time)!=old_x||lv_obj_get_y(saver_time)!=old_y);
    lv_obj_send_event(saver_layer,LV_EVENT_CLICKED,NULL);
    assert(!saver&&preview_brightness==80&&current_page==3&&clock_style==saved_style);
    assert(lv_obj_has_flag(saver_layer,LV_OBJ_FLAG_HIDDEN));
    cJSON *pc=cJSON_Parse("{\"pcInput\":{\"session\":\"pc-a\",\"sequence\":1,\"lastInputTickMs\":1000}}");
    pc_input_update(pc);saver_enter(false);
    cJSON *pi=item(pc,"pcInput");
    cJSON_SetNumberValue(item(pi,"sequence"),2);pc_input_update(pc);
    assert(saver&&tab5_pc_wakes==0); // idle heartbeat
    cJSON_SetNumberValue(item(pi,"lastInputTickMs"),2000);
    cJSON_SetNumberValue(item(pi,"sequence"),3);pc_input_update(pc);
    assert(!saver&&tab5_pc_wakes==1&&preview_brightness==80&&current_page==3);
    saver_enter(false);pc_input_update(pc);assert(saver); // repeated packet
    cJSON_SetNumberValue(item(pi,"sequence"),2);
    cJSON_SetNumberValue(item(pi,"lastInputTickMs"),1000);pc_input_update(pc);assert(saver); // older link
    cJSON_SetNumberValue(item(pi,"sequence"),4);
    cJSON_SetNumberValue(item(pi,"lastInputTickMs"),2000);pc_input_update(pc);assert(saver);
    cJSON_SetNumberValue(item(pi,"sequence"),5);
    cJSON_SetNumberValue(item(pi,"lastInputTickMs"),3000);pc_input_update(pc);assert(!saver&&tab5_pc_wakes==2);
    saver_enter(false);
    cJSON_ReplaceItemInObject(pi,"session",cJSON_CreateString("pc-restart"));
    cJSON_SetNumberValue(item(pi,"sequence"),1);pc_input_update(pc);assert(saver); // process restart is not input
    cJSON_DeleteItemFromObject(pi,"lastInputTickMs");pc_input_update(pc);assert(saver); // failed/old collector
    cJSON_Delete(pc);saver_leave();
    clock_music(86410000,false);assert(strstr(lv_label_get_text(clock_lunar),"寒露"));
    assert(!strstr(lv_label_get_text(clock_lunar),"今日秋分"));
    assert(!strcmp(lv_label_get_text(home_lunar),"农历八月十四"));
    cJSON_AddBoolToObject(item(shown,"music"),"timelineAvailable",false);
    page(5);clock_music(10000,true);
    assert(!strcmp(lv_label_get_text(music_time),"--:--"));
    assert(!strcmp(lv_label_get_text(music_duration),"03:48"));
    assert(lv_obj_has_flag(progress,LV_OBJ_FLAG_HIDDEN));
    capture("known-duration-unknown-position");
    const char *weather_cases[]={"未知","晴","多云","小雨","小雪"};
    for(int kind=0;kind<5;kind++){
        weather_icon_update(weather_cases[kind]);
        assert(weather_icon_type==kind);
        assert(lv_obj_get_child_count(home_weather_icon)==0);
    }
    weather_icon_update("多云");
    page(8);alert_started=1000;alert_until=11000;alert_attention=true;alert_phase=-1;
    ui_text(banner,"Codex：等待你的操作");alert_visual_tick(1000);capture("notification-attention");
    assert(!lv_obj_has_flag(alert_dot,LV_OBJ_FLAG_HIDDEN));
    alert_visual_tick(1500);assert(lv_obj_get_style_bg_opa(alert_dot,0)==LV_OPA_30);
    assert(!strcmp(lv_label_get_text(banner),"Codex：等待你的操作"));
    alert_visual_tick(4000);assert(lv_obj_get_style_bg_opa(alert_dot,0)==LV_OPA_COVER);
    alert_visual_tick(11000);assert(lv_obj_has_flag(alert_dot,LV_OBJ_FLAG_HIDDEN));
    assert(!strlen(lv_label_get_text(banner)));
    cJSON *long_catalog=item(shown,"codexTasks");
    cJSON_AddItemToArray(item(long_catalog,"projects"),cJSON_Parse("{\"id\":\"cccccccccccccccc\",\"name\":\"boss-resume-filter\"}"));
    cJSON_AddItemToArray(item(long_catalog,"tasks"),cJSON_Parse("{\"id\":\"01a0cc69-2782-7650-9903-303578a02970\",\"title\":\"分析页面设计优化方案\",\"projectId\":\"cccccccccccccccc\"}"));
    render_codex();lv_dropdown_set_selected(codex_project_picker,2);
    lv_obj_send_event(codex_project_picker,LV_EVENT_VALUE_CHANGED,NULL);capture("codex-project-long-name");
    // A photo draft shares the same composer but must leave text, mic and send usable.
    snprintf(codex_selected,sizeof(codex_selected),"%s","01a0cc69-2782-7650-9903-303578a02970");
    codex_draft *photo_draft=codex_draft_for(codex_selected,true);assert(photo_draft);
    for(int i=0;i<3;i++) {
        codex_uuid(photo_draft->images[i]);photo_draft->thumbnails[i]=codex_alloc(96*54*2);assert(photo_draft->thumbnails[i]);
        for(int y=0;y<54;y++)for(int x=0;x<96;x++)photo_draft->thumbnails[i][y*96+x]=((x/12+y/12+i)%2)?0x3c57:0xceed;
    }
    codex_set_text("请分析这几张照片里的问题。");codex_keyboard_done(NULL);codex_images_refresh();capture("codex-photo-draft");
    lv_obj_update_layout(codex_input);assert(lv_obj_get_height(codex_input)==170);
    photo_draft->request[0]=0;codex_waiting=codex_voice_waiting=photo_flow=false;
    lv_obj_send_event(composer_remove[1],LV_EVENT_CLICKED,NULL);
    assert(photo_draft->images[0][0]&&photo_draft->images[1][0]&&!photo_draft->images[2][0]);
    assert(!strcmp(lv_textarea_get_text(codex_input),"请分析这几张照片里的问题。"));
    strcpy(photo_draft->request,"00000000-0000-4000-8000-000000000001");
    lv_obj_send_event(composer_remove[0],LV_EVENT_CLICKED,NULL);assert(photo_draft->images[1][0]);
    photo_draft->request[0]=0;
    // Local camera opens during background reads; it does not consume the mailbox.
    page(8);codex_reading=true;codex_syncing=codex_load_waiting=codex_send_queued=false;
    unsigned camera_starts=preview_camera_starts;int camera_reads=preview_reads;
    lv_obj_update_layout(lv_screen_active());
    for(int i=0;i<24;i++)tab5_draw_cache_step(pages[8]);
    assert(tab5_cache_bytes>0);
    ui_text(codex_voice_hint,"文字已返回，请检查后发送");
    photo_open(NULL);photo_open(NULL);
    assert(!photo_open_pending&&photo_flow&&preview_camera_starts==camera_starts+1);
    assert(!lv_label_get_text(codex_voice_hint)[0]);
    render_codex();capture("codex-photo-open-during-read");
    codex_reply_refresh(NULL);assert(preview_reads==camera_reads);
    codex_reading=false;
    assert(!preview_camera_cache_bytes&&!tab5_cache_bytes&&!tab5_native_cache_bytes);
    assert(photo_draft->images[0][0]&&photo_draft->images[1][0]);
    assert(!strcmp(photo_task,codex_selected));
    camera_preview_stop();visible(camera_layer,false);photo_finish();
    // Complete the real attachment UI branch with a successful transport stub.
    // Voice review must disappear; a background task update must keep this receipt.
    int feedback_submit_count=preview_submit_count;
    ui_text(codex_voice_hint,"文字已返回，请检查后发送");
    photo_flow=true;photo_step=3;codex_uuid(photo_id);
    preview_result_code=200;preview_result_text="照片已添加";
    photo_tick();render_codex();
    assert(photo_draft->images[2][0]&&!photo_flow&&!lv_label_get_text(codex_voice_hint)[0]);
    assert(strstr(lv_label_get_text(codex_receipt),"已添加 3/3 张"));
    assert(!strcmp(lv_textarea_get_text(codex_input),"请分析这几张照片里的问题。"));
    assert(preview_submit_count==feedback_submit_count);
    capture("codex-photo-added-feedback");
    ui_text(codex_voice_hint,"照片处理完成后即可录音");
    assert(!lv_label_get_text(codex_receipt)[0]);
    ui_text(codex_receipt,"正在发送到 Codex…");
    assert(!lv_label_get_text(codex_voice_hint)[0]);
    lv_obj_send_event(composer_remove[2],LV_EVENT_CLICKED,NULL);
    assert(!strcmp(lv_label_get_text(codex_receipt),"已移除照片"));
    preview_result_code=0;preview_result_text="";
    puts("COMPOSER_FEEDBACK_OK voice_photo_prepare_add_background_refresh_send_exclusive");
    // A successful add followed by a fresh camera failure must never leave
    // the previous upload success in the composer, even after closing it.
    ui_text(codex_receipt,"照片已添加，Wi-Fi 0.6秒，可与文字一起发送");
    photo_open(NULL);assert(photo_flow);
    assert(!strcmp(lv_label_get_text(codex_receipt),"正在打开摄像头…"));
    preview_camera_state=CAMERA_PREVIEW_ERROR;camera_tick();
    assert(!strcmp(lv_label_get_text(codex_receipt),camera_preview_error()));
    // Exercise the actual close button so the error survives camera teardown.
    for(unsigned i=0;i<lv_obj_get_child_count(camera_layer);i++) {
        lv_obj_t *b=lv_obj_get_child(camera_layer,i),*caption=lv_obj_get_child(b,0);
        if(caption&&lv_obj_check_type(caption,&lv_label_class)&&!strcmp(lv_label_get_text(caption),"关闭")) {
            lv_obj_send_event(b,LV_EVENT_CLICKED,NULL);break;
        }
    }
    assert(!photo_flow&&!strcmp(lv_label_get_text(codex_receipt),camera_preview_error()));
    assert(preview_submit_count==feedback_submit_count);
    camera_starts++;
    puts("PHOTO_RETRY_FEEDBACK_OK previous_success_replaced_error_retained_no_send");
    // Rebuild page snapshots AND decoded artwork between photos. Hidden RGB
    // images must detach before camera allocation, while draft images survive.
#if TAB5_LOCAL_ART
    gallery_pending_page=(gallery_image){0};
    gallery_image leftover_gallery;
    if(gallery_take(&leftover_gallery))gallery_recycle(leftover_gallery.pixels);
#endif
    for(int repeat=0;repeat<3;repeat++) {
        lv_obj_update_layout(lv_screen_active());
        for(int i=0;i<24;i++)tab5_draw_cache_step(pages[8]);
#if TAB5_LOCAL_ART
        for(int i=0;i<2;i++) {
            gallery_drop(i);
            uint8_t *pixels=calloc(1,1280*720*2);assert(pixels);
            gallery_cache[i]=(gallery_image){.pixels=pixels,.category=0,.frame=1,.frames=3,.portrait=i==1};
            strcpy(gallery_cache[i].date,"2026-10-22");
            gallery_descriptor[i]=(lv_image_dsc_t){.header={.magic=LV_IMAGE_HEADER_MAGIC,
                .cf=LV_COLOR_FORMAT_RGB565,.w=1280,.h=720,.stride=2560},
                .data_size=1280*720*2,.data=pixels};
        }
        lv_image_set_src(gallery_view,&gallery_descriptor[0]);visible(gallery_view,true);
        gallery_presented=gallery_cache[0];
        preview_camera_start_observer=camera_gallery_released_probe;
#endif
        assert(tab5_cache_bytes>0);photo_open(NULL);
        preview_camera_start_observer=NULL;
        assert(photo_flow&&!preview_camera_cache_bytes&&!tab5_cache_bytes&&!tab5_native_cache_bytes);
#if TAB5_LOCAL_ART
        assert(gallery_camera_reload&&!gallery_pending_page.pixels&&gallery_pending_page.frame==1&&
            !strcmp(gallery_pending_page.date,"2026-10-22"));
        uint8_t *spare=gallery_take_buffer();assert(spare);free(spare);
#endif
        assert(photo_draft->images[0][0]&&photo_draft->images[1][0]);
        camera_preview_stop();visible(camera_layer,false);photo_finish();
    }
#if TAB5_LOCAL_ART
    int saved_gallery_style=saver_style,saved_gallery_orientation=gallery_orientation;
    art_orientation_state saved_camera_art=preview_art_orientation;
    bool saved_gallery_download=preview_gallery_download;
    saver_style=6;gallery_orientation=0;gallery_return_hold=false;
    preview_art_orientation.ready=true;preview_art_orientation.orientation=0;
    preview_gallery_ready=true;preview_gallery_download=true;preview_gallery_allocations=0;
    gallery_pending_until=0; // The camera may outlive the former retry window.
    struct tm camera_gallery_day={.tm_year=126,.tm_mon=9,.tm_mday=22};
    gallery_update(&camera_gallery_day);
    assert(!gallery_camera_reload&&gallery_pending_until>esp_timer_get_time()/1000&&
        preview_gallery_requested_frame==1&&preview_gallery_result.pixels);
    gallery_update(&camera_gallery_day);
    assert(gallery_cache[0].pixels&&gallery_cache[0].frame==1&&!gallery_pending_page.date[0]&&
        !lv_obj_has_flag(gallery_view,LV_OBJ_FLAG_HIDDEN));
    saver_style=saved_gallery_style;gallery_orientation=saved_gallery_orientation;
    preview_art_orientation=saved_camera_art;
    preview_gallery_download=saved_gallery_download;
    puts("CAMERA_GALLERY_RECLAIM_OK three real LVGL cycles; two decoded frames detached before camera start; attachments retained");
    puts("CAMERA_GALLERY_RETURN_OK expired old window restarts; same work/page requested and displayed after capture");
#endif
    camera_starts+=3;
    // Only the current unloaded draft needs restoration before opening.
    photo_draft->loaded=photo_draft->dirty=false;
    snprintf(codex_sync_op,sizeof(codex_sync_op),"draft-load");
    snprintf(codex_sync_task,sizeof(codex_sync_task),"%s",codex_selected);
    codex_syncing=true;photo_open(NULL);assert(photo_open_pending);
    page(0);page(8);codex_syncing=false;photo_open_tick();
    assert(!photo_open_pending&&!photo_flow&&preview_camera_starts==camera_starts+1);
    codex_syncing=true;photo_open(NULL);assert(photo_open_pending);
    codex_syncing=false;strcpy(photo_draft->request,"00000000-0000-4000-8000-000000000001");
    photo_open_tick();assert(!photo_open_pending&&!photo_flow);
    photo_open(NULL);assert(!photo_open_pending&&!photo_flow);
    photo_draft->request[0]=0;photo_draft->loaded=true;
    puts("CAMERA_OPEN_QUEUE_OK local_read_does_not_wait_initial_draft_restore_navigation_cancel_unconfirmed_send_blocked");
    lv_obj_remove_flag(photo_window,LV_OBJ_FLAG_HIDDEN);visible(photo_files,true);visible(photo_view,false);
    ui_text(photo_title,"存储卡 · JPG");ui_text(photo_note,"/sdcard/DCIM · 第 1 页");
    button(photo_files,"IMG_20260926_154012.jpg",0,0,1118,photo_choose_file,0);capture("photo-card-picker");visible(photo_window,false);
    // Exercise the visible take -> review -> retake flow with a simulated frame.
    // Taking a photo must never attach or send it without the separate confirm.
    uint16_t *camera_frame=malloc(1280*720*2);assert(camera_frame);
    uint16_t *camera_view=malloc(CAMERA_VIEW_WIDTH*CAMERA_VIEW_HEIGHT*2);assert(camera_view);
    for(int y=0;y<720;y++)for(int x=0;x<1280;x++)
        camera_frame[y*1280+x]=(uint16_t)((((x*31/1280)&31)<<11)|(((y*63/720)&63)<<5)|12);
    camera_frame_view(camera_view,(const uint8_t *)camera_frame,1280*2);
    assert(CAMERA_VIEW_WIDTH==1280&&CAMERA_VIEW_HEIGHT==720);
    assert(!memcmp(camera_view,camera_frame,1280*720*2));
    // A padded camera row must also preserve every pixel without copying padding.
    size_t camera_stride=1280*2+32;
    uint8_t *padded_camera=malloc(camera_stride*720);assert(padded_camera);
    memset(padded_camera,0xa5,camera_stride*720);
    for(unsigned y=0;y<720;y++)memcpy(padded_camera+y*camera_stride,camera_frame+y*1280,1280*2);
    camera_frame_view(camera_view,padded_camera,camera_stride);
    assert(!memcmp(camera_view,camera_frame,1280*720*2));free(padded_camera);
    lv_canvas_set_buffer(camera_canvas,camera_view,CAMERA_VIEW_WIDTH,CAMERA_VIEW_HEIGHT,LV_COLOR_FORMAT_RGB565);
    photo_for_codex=photo_flow=true;visible(camera_layer,true);lv_obj_move_foreground(camera_layer);
    int sends_before_camera=preview_submit_count;
    preview_camera_state=CAMERA_PREVIEW_STARTING;camera_tick();
    lv_obj_send_event(camera_take,LV_EVENT_PRESSED,NULL);
    assert(preview_camera_state==CAMERA_PREVIEW_STARTING);
    preview_camera_state=CAMERA_PREVIEW_LIVE;camera_tick();capture("camera-live-left");
    check_camera_occlusion();
    check_camera_idle_redraw();
    assert(lv_obj_has_flag(camera_hint,LV_OBJ_FLAG_HIDDEN));
    assert(!strcmp(lv_label_get_text(camera_hint),""));
    assert(lv_obj_get_x(camera_take)==0);
    assert(lv_obj_get_x(camera_canvas)==0&&lv_obj_get_y(camera_canvas)==0);
    assert(lv_obj_get_width(camera_canvas)==1280&&lv_obj_get_height(camera_canvas)==720);
    assert(lv_obj_get_style_bg_opa(camera_take,LV_PART_MAIN)==LV_OPA_TRANSP);
    for(unsigned i=0;i<lv_obj_get_child_count(camera_layer);i++) {
        lv_obj_t *control=lv_obj_get_child(camera_layer,i);
        for(unsigned j=0;j<lv_obj_get_child_count(control);j++) {
            lv_obj_t *child=lv_obj_get_child(control,j);
            if(lv_obj_check_type(child,&lv_label_class))assert(strcmp(lv_label_get_text(child),"存储卡"));
        }
    }
    // Hit-test through the real LVGL object tree, including decorative children.
    const lv_point_t targets[]={{4,360},{94,336},{180,360}};
    for(unsigned i=0;i<3;i++) {
        lv_point_t point=targets[i];assert(lv_indev_search_obj(lv_screen_active(),&point)==camera_take);
    }
    lv_indev_t *touch=lv_indev_create();lv_indev_set_type(touch,LV_INDEV_TYPE_POINTER);
    lv_indev_set_display(touch,lv_display_get_default());lv_indev_set_read_cb(touch,camera_touch_read);
    camera_touch_point=(lv_point_t){94,336};camera_touch_state=LV_INDEV_STATE_PRESSED;lv_indev_read(touch);
    assert(preview_camera_state==CAMERA_PREVIEW_CAPTURING); // accepted before release
    camera_tick();assert(lv_obj_has_state(camera_take,LV_STATE_DISABLED));
    assert(lv_obj_get_style_bg_opa(camera_take,LV_PART_MAIN)==LV_OPA_TRANSP);
    assert(!lv_obj_has_flag(camera_hint,LV_OBJ_FLAG_HIDDEN));
    assert(!strcmp(lv_label_get_text(camera_hint),"正在拍摄…"));
    camera_touch_point.x=220;camera_touch_state=LV_INDEV_STATE_RELEASED;lv_indev_read(touch);
    lv_obj_send_event(camera_take,LV_EVENT_PRESSED,NULL);camera_tick();
    assert(preview_camera_state==CAMERA_PREVIEW_CAPTURING);
    preview_camera_state=CAMERA_PREVIEW_CAPTURED;camera_tick();lv_tick_inc(250);lv_timer_handler();capture("camera-review");
    check_camera_occlusion();
    check_camera_idle_redraw();
    assert(preview_camera_state==CAMERA_PREVIEW_CAPTURED);
    assert(!lv_obj_has_flag(camera_use,LV_OBJ_FLAG_HIDDEN));
    assert(!lv_obj_has_flag(camera_retry,LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_has_flag(camera_take,LV_OBJ_FLAG_HIDDEN));
    assert(!lv_obj_has_flag(camera_canvas,LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_get_x(camera_canvas)==0&&lv_obj_get_y(camera_canvas)==0);
    assert(lv_obj_get_width(camera_canvas)==1280&&lv_obj_get_height(camera_canvas)==720);
    assert(!memcmp(lv_canvas_get_draw_buf(camera_canvas)->data,camera_frame,1280*720*2));
    lv_obj_send_event(camera_take,LV_EVENT_PRESSED,NULL);assert(preview_camera_state==CAMERA_PREVIEW_CAPTURED);
    lv_obj_send_event(camera_retry,LV_EVENT_CLICKED,NULL);camera_tick();
    assert(preview_camera_state==CAMERA_PREVIEW_STARTING);
    assert(lv_obj_has_flag(camera_use,LV_OBJ_FLAG_HIDDEN));
    memset(camera_frame,0x55,1280*720*2);
    camera_frame_view(camera_view,(const uint8_t *)camera_frame,1280*2);
    preview_camera_state=CAMERA_PREVIEW_LIVE;camera_tick();
    lv_obj_send_event(camera_take,LV_EVENT_PRESSED,NULL);camera_tick();
    assert(preview_camera_state==CAMERA_PREVIEW_CAPTURING);
    preview_camera_state=CAMERA_PREVIEW_CAPTURED;camera_tick();
    const uint16_t *review_pixels=(const uint16_t *)lv_canvas_get_draw_buf(camera_canvas)->data;
    assert(!lv_obj_has_flag(camera_canvas,LV_OBJ_FLAG_HIDDEN));
    assert(review_pixels[0]==0x5555&&review_pixels[1280*720-1]==0x5555);
    assert(lv_canvas_get_draw_buf(camera_canvas)->header.w==CAMERA_VIEW_WIDTH);
    assert(preview_submit_count==sends_before_camera);
    // Encoder failure offers a retry of the retained still, not a new capture.
    lv_obj_send_event(camera_use,LV_EVENT_CLICKED,NULL);photo_feedback_present();
    assert(preview_photo_prepares==1&&photo_step==1);
    assert(!lv_obj_has_flag(camera_layer,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(photo_window,LV_OBJ_FLAG_HIDDEN));
    preview_photo_state=3;photo_tick();
    assert(!lv_obj_has_flag(photo_confirm,LV_OBJ_FLAG_HIDDEN));
    assert(!strcmp(lv_label_get_text(lv_obj_get_child(photo_confirm,0)),"重试"));
    lv_obj_send_event(photo_confirm,LV_EVENT_CLICKED,NULL);photo_feedback_present();
    assert(preview_photo_prepares==2&&photo_step==1&&preview_photo_state==1);
    assert(preview_camera_state==CAMERA_PREVIEW_CAPTURED);
    assert(preview_submit_count==sends_before_camera);
    preview_photo_state=0;photo_finish();
    lv_indev_delete(touch);free(camera_frame);
    camera_preview_stop();visible(camera_layer,false);photo_for_codex=photo_flow=false;

    // Real three-attachment UI lifecycle with a simulated codec/network only.
    // Fill, reject a fourth, remove middle/last/first, reopen, and add back.
    // No turn is submitted and unrelated pre-existing fixture attachments survive.
    {
        codex_draft *d=codex_draft_for(codex_selected,false);assert(d==photo_draft);
        char saved_images[3][37];uint16_t *saved_thumbnails[3];
        memcpy(saved_images,d->images,sizeof(saved_images));memcpy(saved_thumbnails,d->thumbnails,sizeof(saved_thumbnails));
        memset(d->images,0,sizeof(d->images));memset(d->thumbnails,0,sizeof(d->thumbnails));
        codex_images_refresh();
        int submitted=preview_submit_count;unsigned uploads=preview_photo_uploads;
        char saved_text[2001];snprintf(saved_text,sizeof(saved_text),"%s",lv_textarea_get_text(codex_input));
        preview_photo_fixture=true;
        for(unsigned round=0;round<4;round++) {
            for(unsigned slot=0;slot<3;slot++)if(!d->images[slot][0]) {
                codex_waiting=codex_voice_waiting=codex_reading=codex_syncing=codex_load_waiting=codex_send_queued=false;
                last_received=esp_timer_get_time()/1000;
                photo_open(NULL);assert(photo_flow&&photo_for_codex);
                preview_camera_state=CAMERA_PREVIEW_CAPTURED;photo_camera_use();photo_feedback_present();assert(photo_step==1);
                preview_photo_width=slot==1?360:640;preview_photo_height=slot==1?640:360;
                for(unsigned n=0;n<PHOTO_PREVIEW_W*PHOTO_PREVIEW_H;n++)preview_photo_pixels[n]=(uint16_t)(0x2100+uploads);
                preview_photo_state=2;preview_result_code=-1;
                photo_tick();uploads++;assert(photo_step==3&&preview_photo_uploads==uploads);
                assert(photo_camera_processing&&!lv_obj_has_flag(camera_layer,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(photo_window,LV_OBJ_FLAG_HIDDEN));
                char added[37];snprintf(added,sizeof(added),"%s",preview_photo_upload_id);
                photo_tick();assert(photo_step==3&&preview_photo_uploads==uploads);
                preview_result_code=200;preview_result_text="照片已添加";
                photo_tick();assert(!photo_flow&&!strcmp(d->images[slot],added)&&d->thumbnails[slot]);
                assert(d->thumbnails[slot][27*96+48]==preview_photo_pixels[0]);
                assert(!strcmp(lv_textarea_get_text(codex_input),saved_text)&&preview_submit_count==submitted);
            }
            unsigned starts=preview_camera_starts;
            char full[3][37];memcpy(full,d->images,sizeof(full));
            photo_open(NULL);assert(!photo_flow&&preview_camera_starts==starts&&!memcmp(full,d->images,sizeof(full)));
            if(round==0)capture("codex-three-photo-lifecycle-full");
            // Reopen all retained attachments, including after middle/last/first
            // removal. Four physical poses use the whole available image area.
            for(unsigned slot=0;slot<3;slot++)for(unsigned p=0;p<(round==0?4:1);p++) {
                static const int poses[]={0,2,1,-1};
                preview_art_orientation=(art_orientation_state){.ready=true,.orientation=poses[p],.sampled_ms=preview_now_us/1000};
                camera_pose=poses[p];lv_obj_send_event(composer_photos[slot],LV_EVENT_CLICKED,NULL);
                assert(photo_flow&&!lv_obj_has_flag(photo_window,LV_OBJ_FLAG_HIDDEN));
                lv_obj_update_layout(lv_screen_active());
                const lv_draw_buf_t *buf=lv_canvas_get_draw_buf(photo_view);assert(buf&&buf->data);
                unsigned scale=lv_image_get_scale_x(photo_view),sw=buf->header.w*scale/256,sh=buf->header.h*scale/256;
                int dw=lv_display_get_horizontal_resolution(NULL),dh=lv_display_get_vertical_resolution(NULL);
                assert(sw<=(unsigned)(dw-64)&&sh<=(unsigned)(dh-180));
                assert(sw+4>=(unsigned)(dw-64)||sh+4>=(unsigned)(dh-180));
                if(buf->header.w>buf->header.h)assert(scale>256); // Old fixed-size landscape preview must fail this.
                if(round==0&&slot<2){char name[96];snprintf(name,sizeof(name),"photo-attached-%s-pose%d",slot?"portrait":"landscape",poses[p]);capture(name);}
                lv_obj_send_event(photo_close_button,LV_EVENT_CLICKED,NULL);assert(!photo_flow);
            }
            camera_pose=0;preview_art_orientation=(art_orientation_state){.ready=true,.orientation=0,.sampled_ms=preview_now_us/1000};
            if(round==3)break;
            static const unsigned remove_at[3]={1,2,0};
            unsigned remove=remove_at[round];uint16_t *thumbnails[3];
            memcpy(thumbnails,d->thumbnails,sizeof(thumbnails));
            lv_obj_send_event(composer_remove[remove],LV_EVENT_CLICKED,NULL);
            for(unsigned i=0,j=0;i<3;i++)if(i!=remove) {
                assert(!strcmp(d->images[j],full[i])&&d->thumbnails[j]==thumbnails[i]);j++;
            }
            assert(!d->images[2][0]&&!d->thumbnails[2]);
            char remaining[3][37];memcpy(remaining,d->images,sizeof(remaining));
            for(unsigned repeat=0;repeat<3;repeat++) {
                codex_waiting=codex_voice_waiting=codex_reading=codex_syncing=codex_load_waiting=codex_send_queued=false;
                photo_open(NULL);assert(photo_flow);
                photo_fixture_close_camera();
                assert(!memcmp(remaining,d->images,sizeof(remaining)));
            }
        }
        assert(preview_photo_uploads==uploads&&preview_submit_count==submitted);
        for(unsigned i=0;i<3;i++)lv_obj_send_event(composer_remove[0],LV_EVENT_CLICKED,NULL);
        assert(!d->images[0][0]&&!d->images[1][0]&&!d->images[2][0]);
        photo_open(NULL);assert(photo_flow);photo_fixture_close_camera();
        memcpy(d->images,saved_images,sizeof(saved_images));memcpy(d->thumbnails,saved_thumbnails,sizeof(saved_thumbnails));
        preview_photo_fixture=false;preview_photo_state=0;preview_result_code=0;preview_result_text="";
        preview_photo_width=640;preview_photo_height=360;
        codex_images_refresh();
        puts("PHOTO_THREE_LIFECYCLE_OK six uploads with pending ack, four full drafts, fourth blocked, middle/last/first removal/readd, nine reopens, remove-all/reopen, text and unrelated attachments retained, no turn sent");
    }

    // Real LVGL events: recent selection is stable during an utterance, and
    // only explicit stop + complete recognition stages a draft; no path submits a turn.
    cJSON_Delete(shown);
    shown=cJSON_Parse("{\"epochMilliseconds\":1791040000000,\"pcInput\":{\"session\":\"quick-test-session\"},\"codexTasks\":{\"projects\":[{\"id\":\"1234567890abcdef\",\"name\":\"AI-bot\"}],\"tasks\":[{\"id\":\"10000000-0000-4000-8000-000000000001\",\"title\":\"检查设备连接\",\"folder\":\"AI-bot\",\"projectId\":\"1234567890abcdef\",\"updatedAt\":10},{\"id\":\"10000000-0000-4000-8000-000000000002\",\"title\":\"TAB5 两个快捷按钮\",\"folder\":\"AI-bot\",\"projectId\":\"1234567890abcdef\",\"updatedAt\":30},{\"id\":\"10000000-0000-4000-8000-000000000003\",\"title\":\"完善语音输入\",\"folder\":\"AI-bot\",\"projectId\":\"1234567890abcdef\",\"updatedAt\":20}]}}");
    codex_waiting=codex_voice_waiting=codex_send_queued=codex_reading=codex_syncing=codex_load_waiting=false;
    codex_selected[0]=0;preview_submit_enabled=preview_voice_start_enabled=preview_usb_connected=true;
    last_received=esp_timer_get_time()/1000;preview_config.paired=true;
    for(int extra=4;extra<=6;extra++) {
        cJSON *task=cJSON_CreateObject();char id[37];snprintf(id,sizeof(id),"10000000-0000-4000-8000-%012d",extra);
        cJSON_AddStringToObject(task,"id",id);cJSON_AddStringToObject(task,"title","其他最近会话");
        cJSON_AddStringToObject(task,"folder","AI-bot");cJSON_AddStringToObject(task,"projectId","1234567890abcdef");
        cJSON_AddNumberToObject(task,"updatedAt",9-extra);cJSON_AddItemToArray(item(item(shown,"codexTasks"),"tasks"),task);
    }
    // The permanent shortcut occupies its own footer slot on every normal
    // page, outside content and all four navigation hit targets.
    lv_obj_update_layout(lv_screen_active());
    lv_area_t entry_bounds;lv_obj_get_coords(quick_entry,&entry_bounds);
    assert(entry_bounds.x1>=1080&&entry_bounds.y1>=628&&entry_bounds.x2<1280&&entry_bounds.y2<720);
    for(int n=0;n<4;n++){lv_area_t nav_bounds;lv_obj_get_coords(workbench_nav[n],&nav_bounds);assert(nav_bounds.x2<entry_bounds.x1);}
    for(int n=0;n<13;n++)if(n!=10) {
        page(n);assert(!lv_obj_has_flag(quick_entry,LV_OBJ_FLAG_HIDDEN));
        lv_obj_send_event(quick_entry,LV_EVENT_CLICKED,NULL);assert(quick_visible&&current_page==8);
        quick_close(NULL);assert(!quick_visible&&current_page==n);
    }
    // A shortcut round trip must preserve the parent's own back destination.
    for(int origin=0;origin<4;origin++) {
        page(primary_pages[origin]);page(9);
        lv_obj_send_event(quick_entry,LV_EVENT_CLICKED,NULL);
        lv_obj_send_event(quick_close_button,LV_EVENT_CLICKED,NULL);assert(current_page==9&&primary_index==origin);
        lv_obj_send_event(workbench_back,LV_EVENT_CLICKED,NULL);assert(current_page==primary_pages[origin]);
    }
    page(0);page(7);page(9);
    lv_obj_send_event(quick_entry,LV_EVENT_CLICKED,NULL);
    lv_obj_send_event(quick_close_button,LV_EVENT_CLICKED,NULL);assert(current_page==9);
    lv_obj_send_event(workbench_back,LV_EVENT_CLICKED,NULL);assert(current_page==7);
    lv_obj_send_event(workbench_back,LV_EVENT_CLICKED,NULL);assert(current_page==0);
    page(0);page(7);
    lv_obj_send_event(quick_entry,LV_EVENT_CLICKED,NULL);
    lv_obj_send_event(quick_close_button,LV_EVENT_CLICKED,NULL);assert(current_page==7);
    lv_obj_send_event(workbench_back,LV_EVENT_CLICKED,NULL);assert(current_page==0);
    puts("QUICK_RETURN_CONTEXT_OK primary_settings nested_settings overview_detail");
#if TAB5_LOCAL_ART
    lv_indev_t *return_touch=lv_indev_create();lv_indev_set_type(return_touch,LV_INDEV_TYPE_POINTER);
    lv_indev_set_display(return_touch,lv_display_get_default());lv_indev_set_read_cb(return_touch,camera_touch_read);
    page(11);page(4);page(9);settings_select(0);capture("navigation-return-settings");
    gallery_pointer_click(return_touch,(lv_point_t){1222,666});assert(quick_visible&&current_page==8);
    capture("navigation-return-codex");
    gallery_pointer_click(return_touch,(lv_point_t){56,40});assert(!quick_visible&&current_page==9);
    gallery_pointer_click(return_touch,(lv_point_t){56,40});assert(current_page==4);
    gallery_pointer_click(return_touch,(lv_point_t){56,40});assert(current_page==11);
    lv_indev_delete(return_touch);
    puts("QUICK_RETURN_POINTER_OK app_detail_settings_shortcut_and_back");
#endif
    page(0);capture("codex-direct-home");page(7);capture("codex-direct-clock");page(8);capture("codex-direct-workbench");
    lv_obj_send_event(quick_entry,LV_EVENT_CLICKED,NULL);assert(quick_count==5&&quick_tasks[0].updated==30);
    lv_obj_update_layout(quick_layer);
    assert(lv_obj_get_height(quick_close_button)==56&&lv_obj_get_width(quick_close_button)==52);
    assert(!strcmp(lv_label_get_text(lv_obj_get_child(quick_close_button,0)),LV_SYMBOL_LEFT));
    assert(lv_obj_get_x(quick_close_button)==30&&lv_obj_get_y(quick_close_button)==12);
    assert(lv_obj_get_x(quick_close_button)==lv_obj_get_x(workbench_back)&&
           lv_obj_get_y(quick_close_button)==lv_obj_get_y(workbench_back)&&
           lv_obj_get_width(quick_close_button)==lv_obj_get_width(workbench_back)&&
           lv_obj_get_height(quick_close_button)==lv_obj_get_height(workbench_back));
    const lv_image_dsc_t *key_materials[]={&quick_collar_image,&quick_graphite_cap_image,&quick_red_cap_image,&quick_blue_cap_image,
                                         &brand_codex_key_image,&brand_doubao_image};
    for(unsigned asset=0;asset<sizeof(key_materials)/sizeof(*key_materials);asset++) {
        lv_image_decoder_dsc_t decoded={0};
        assert(lv_image_decoder_open(&decoded,key_materials[asset],NULL)==LV_RESULT_OK);
        assert(decoded.decoded&&decoded.decoded->header.w==key_materials[asset]->header.w&&
               decoded.decoded->header.h==key_materials[asset]->header.h);
        lv_image_decoder_close(&decoded);
    }
    puts("QUICK_KEY_ASSETS_OK six compressed materials/icons decoded by actual LVGL");
    assert(lv_obj_has_flag(quick_feedback_panel,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(quick_text_box,LV_OBJ_FLAG_HIDDEN));
    puts("QUICK_FEEDBACK_EMPTY_OK first entry has no empty result panel");
    capture("quick-console");
    lv_obj_t *stable_controls[]={quick_close_button,quick_next};
    uint16_t stable_controls_pixels[52*56+52*48];int stable_at=0;
    for(int n=0;n<2;n++) {
        lv_area_t area;lv_obj_get_coords(stable_controls[n],&area);
        assert(stable_at+(area.x2-area.x1+1)*(area.y2-area.y1+1)<=sizeof(stable_controls_pixels)/sizeof(stable_controls_pixels[0]));
        for(int y=area.y1;y<=area.y2;y++){memcpy(stable_controls_pixels+stable_at,preview_buffer+y*1280+area.x1,(area.x2-area.x1+1)*2);stable_at+=area.x2-area.x1+1;}
    }
    quick_opening=true;quick_refresh();lv_refr_now(NULL);stable_at=0;
    for(int n=0;n<2;n++) {
        lv_area_t area;lv_obj_get_coords(stable_controls[n],&area);
        assert(!lv_obj_has_flag(stable_controls[n],LV_OBJ_FLAG_CLICKABLE));
        for(int y=area.y1;y<=area.y2;y++){assert(!memcmp(stable_controls_pixels+stable_at,preview_buffer+y*1280+area.x1,(area.x2-area.x1+1)*2));stable_at+=area.x2-area.x1+1;}
    }
    quick_close(NULL);assert(quick_visible);
    lv_obj_send_event(quick_next,LV_EVENT_CLICKED,NULL);assert(quick_index==0);
    quick_opening=false;quick_refresh();lv_refr_now(NULL);
    assert(lv_obj_has_flag(quick_close_button,LV_OBJ_FLAG_CLICKABLE)&&lv_obj_has_flag(quick_next,LV_OBJ_FLAG_CLICKABLE));
    puts("QUICK_NAV_BUSY_UI_OK close_next_pixels_unchanged and callbacks_locked");
    uint16_t stable_deck[70*210];
    int deck_y=lv_obj_get_y(quick_open_button)+24;
    for(int y=0;y<210;y++)memcpy(stable_deck+y*70,preview_buffer+(deck_y+y)*1280+140,70*2);
    lv_state_t key_states[]={LV_STATE_PRESSED,LV_STATE_DISABLED,LV_STATE_PRESSED|LV_STATE_DISABLED};
    for(int state=0;state<3;state++) {
        lv_obj_add_state(quick_open_button,key_states[state]);lv_refr_now(NULL);
        for(int y=0;y<210;y++)assert(!memcmp(stable_deck+y*70,preview_buffer+(deck_y+y)*1280+140,70*2));
        lv_obj_remove_state(quick_open_button,key_states[state]);lv_refr_now(NULL);
    }
    assert(lv_obj_get_height(quick_text_box)>=96&&lv_obj_get_width(quick_text_box)>=1160);
    lv_obj_t *quick_keys[]={quick_open_button,quick_voice_button};
    for(int key=0;key<2;key++) {
        lv_area_t key_bounds,collar_bounds;lv_obj_get_coords(quick_keys[key],&key_bounds);
        lv_obj_get_coords(lv_obj_get_child(quick_keys[key],1),&collar_bounds);
        assert(lv_obj_has_flag(lv_obj_get_child(quick_keys[key],0),LV_OBJ_FLAG_HIDDEN));
        assert(collar_bounds.y2<=key_bounds.y2&&collar_bounds.y2<lv_obj_get_y(quick_text_box));
    }
    lv_obj_send_event(quick_open_button,LV_EVENT_PRESSED,NULL);
    assert(lv_anim_get(quick_key_faces[0],quick_cap_depth)!=NULL);
    for(int frame=0;frame<6;frame++) {char name[64];snprintf(name,sizeof(name),"quick-key-motion-%02d",frame);lv_tick_inc(16);lv_timer_handler();capture(name);}
    assert(lv_obj_get_style_translate_y(quick_key_faces[0],0)==8);
    capture("quick-console-pressed");lv_obj_send_event(quick_open_button,LV_EVENT_RELEASED,NULL);
    for(int frame=6;frame<15;frame++) {char name[64];snprintf(name,sizeof(name),"quick-key-motion-%02d",frame);lv_tick_inc(20);lv_timer_handler();capture(name);}
    assert(lv_obj_get_style_translate_y(quick_key_faces[0],0)==0);
    lv_obj_send_event(quick_open_button,LV_EVENT_PRESSED,NULL);lv_tick_inc(90);lv_timer_handler();
    lv_obj_send_event(quick_open_button,LV_EVENT_PRESS_LOST,NULL);lv_tick_inc(180);lv_timer_handler();
    assert(lv_obj_get_style_translate_y(quick_key_faces[0],0)==0&&!quick_opening);
    // Opening and arrows all dispatch the displayed target; arrows commit
    // selection only after confirmation, and never reorder this visit's list.
    lv_obj_send_event(quick_open_button,LV_EVENT_CLICKED,NULL);
    confirm_quick_open(0);
    char first_target[37];strcpy(first_target,quick_tasks[0].id);
    for(int n=1;n<5;n++) {
        int requests=preview_desktop_requests;
        lv_obj_send_event(quick_next,LV_EVENT_CLICKED,NULL);
        assert(quick_opening&&quick_index==n-1&&!strcmp(preview_desktop_target,quick_tasks[n].id));
        lv_obj_send_event(quick_next,LV_EVENT_CLICKED,NULL);
        lv_obj_send_event(quick_open_button,LV_EVENT_CLICKED,NULL);
        assert(preview_desktop_requests==requests+1);
        confirm_quick_open(n);
        assert(!strcmp(quick_tasks[0].id,first_target));
    }
    int boundary_requests=preview_desktop_requests;
    lv_obj_send_event(quick_next,LV_EVENT_CLICKED,NULL);
    assert(quick_index==4&&!quick_opening&&preview_desktop_requests==boundary_requests);
    for(int n=3;n>=1;n--) {
        lv_obj_send_event(quick_previous,LV_EVENT_CLICKED,NULL);confirm_quick_open(n);
    }
    // A newer catalog entry cannot override the deliberate older selection.
    cJSON *new_latest=cJSON_Parse("{\"id\":\"10000000-0000-4000-8000-000000000007\",\"title\":\"刚更新的电脑会话\",\"folder\":\"AI-bot\",\"projectId\":\"1234567890abcdef\",\"updatedAt\":99}");
    cJSON_AddItemToArray(item(item(shown,"codexTasks"),"tasks"),new_latest);
    lv_obj_send_event(quick_open_button,LV_EVENT_CLICKED,NULL);confirm_quick_open(1);
    assert(strcmp(quick_opened,text(new_latest,"id"))&&quick_count==5&&!strcmp(quick_tasks[0].id,first_target));
    capture("quick-console-selected-desktop");
    // Failure or a wrong-target success does not move the panel selection or
    // authorize voice input in a desktop conversation that was not confirmed.
    lv_obj_send_event(quick_next,LV_EVENT_CLICKED,NULL);assert(quick_opening&&quick_index==1);
    preview_desktop.busy=false;preview_desktop.status=409;
    strcpy(preview_desktop.message,"电脑桌面不可操作，请解锁后再点 Codex");quick_tick();
    assert(quick_failed&&quick_index==1&&!quick_opened[0]);
    assert(lv_obj_has_flag(quick_cancel_button,LV_OBJ_FLAG_HIDDEN));
    assert(!lv_obj_has_state(quick_open_button,LV_STATE_DISABLED));
    for(uint32_t at=0;preview_desktop.message[at];) {
        uint32_t cp=lv_text_encoded_next(preview_desktop.message,&at);lv_font_glyph_dsc_t glyph;
        assert(lv_font_get_glyph_dsc(&tab5_font_24,&glyph,cp,0)&&!glyph.is_placeholder);
    }
    capture("quick-console-desktop-locked");
    lv_obj_send_event(quick_voice_button,LV_EVENT_CLICKED,NULL);assert(!quick_recording&&!quick_filling);
    lv_obj_send_event(quick_next,LV_EVENT_CLICKED,NULL);
    preview_desktop.busy=false;preview_desktop.status=200;strcpy(preview_desktop.task_id,first_target);quick_tick();
    assert(quick_failed&&quick_index==1&&!quick_opened[0]&&strstr(lv_label_get_text(quick_status),"未确认电脑会话同步"));
    assert(lv_obj_has_flag(quick_cancel_button,LV_OBJ_FLAG_HIDDEN));
    lv_obj_send_event(quick_open_button,LV_EVENT_CLICKED,NULL);confirm_quick_open(1);
    preview_desktop_available=false;
    lv_obj_send_event(quick_next,LV_EVENT_CLICKED,NULL);
    assert(!quick_opening&&quick_index==1&&!strcmp(quick_opened,quick_tasks[1].id));
    preview_desktop_available=true;
    assert(preview_desktop_submits==0&&preview_desktop_stages==0&&preview_desktop_clears==0);
    // Reopening the panel rebuilds recency and defaults to the new latest.
    quick_close(NULL);quick_show(NULL);
    assert(quick_index==0&&quick_count==5&&!strcmp(quick_tasks[0].id,text(new_latest,"id")));
    lv_obj_send_event(quick_open_button,LV_EVENT_CLICKED,NULL);confirm_quick_open(0);
    boundary_requests=preview_desktop_requests;
    lv_obj_send_event(quick_previous,LV_EVENT_CLICKED,NULL);
    assert(!quick_opening&&preview_desktop_requests==boundary_requests);
    assert(lv_obj_has_flag(quick_clear_button,LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_get_width(quick_status)==952);
    lv_obj_send_event(quick_clear_button,LV_EVENT_LONG_PRESSED,NULL);
    assert(!quick_clearing&&preview_desktop_clears==0);
    capture("quick-console-empty-clear-hidden");
    puts("QUICK_DESKTOP_SELECTION_UI_OK explicit_target_open, arrows_dispatch_desktop, busy_no_duplicate, boundaries, stable_order, failed_or_wrong_target_blocks_voice, new_visit_refreshes_latest");
    // Mac/Wi-Fi symptom: desktop navigation is confirmed before the slower
    // task catalog/draft requests catch up. Voice belongs to that confirmed
    // desktop target, not a hidden sidebar row or background draft worker.
    {
        cJSON *quick_remote=item(shown,"codexTasks");
        cJSON *old_catalog=cJSON_DetachItemFromObjectCaseSensitive(quick_remote,"tasks");
        cJSON_AddArrayToObject(quick_remote,"tasks");
        bool old_usb=preview_usb_connected,old_wifi=wifi_connected;
        preview_usb_connected=false;wifi_connected=true;
        int stages=preview_desktop_stages,submits=preview_desktop_submits;
        preview_result_code=-1;codex_reading=codex_syncing=codex_load_waiting=true;
        for(int round=0;round<4;round++) {
            lv_obj_send_event(round%2?quick_previous:quick_next,LV_EVENT_CLICKED,NULL);
            confirm_quick_open(round%2?0:1);
            render_codex();assert(!strcmp(codex_selected,quick_opened));
            lv_obj_add_state(codex_input,LV_STATE_DISABLED);
            last_received=esp_timer_get_time()/1000;
            codex_reading=codex_syncing=codex_load_waiting=true;
            lv_obj_send_event(quick_voice_button,LV_EVENT_CLICKED,NULL);
            assert(codex_voice_start_pending&&!quick_recording&&!codex_voice_waiting);
            assert(codex_reading&&codex_syncing&&codex_load_waiting); // no forged completion/cancellation
            // Current firmware queues exactly one start behind an owned read.
            // Simulate that read completing, then exercise the actual pending tick.
            codex_reading=codex_syncing=codex_load_waiting=false;
            codex_voice_pending_tick(esp_timer_get_time()/1000);
            assert(!codex_voice_start_pending&&quick_recording&&codex_voice_waiting&&!strcmp(preview_voice.task_id,quick_opened));
            lv_obj_send_event(quick_cancel_button,LV_EVENT_CLICKED,NULL);
            preview_voice.busy=false;strcpy(preview_voice.phase,"cancelled");codex_voice_tick();quick_tick();
            assert(!quick_recording&&!codex_voice_waiting&&!quick_filling&&!quick_sending);
        }
        codex_reading=codex_syncing=codex_load_waiting=false;lv_obj_remove_state(codex_input,LV_STATE_DISABLED);
        codex_draft *pending=codex_draft_for(codex_selected,true);char pending_before[37];strcpy(pending_before,pending->request);
        strcpy(pending->request,"30000000-0000-4000-8000-000000000001");
        lv_obj_send_event(quick_voice_button,LV_EVENT_CLICKED,NULL);
        assert(!quick_recording&&strstr(lv_label_get_text(quick_status),"待确认消息"));strcpy(pending->request,pending_before);
        int syncs=preview_sync_count;bool loaded_before=pending->loaded;pending->loaded=false;codex_sync_next=0;
        codex_sync_tick(esp_timer_get_time()/1000);assert(preview_sync_count==syncs&&!codex_syncing);pending->loaded=loaded_before;
        assert(preview_desktop_stages==stages&&preview_desktop_submits==submits);
        cJSON_DeleteItemFromObjectCaseSensitive(quick_remote,"tasks");cJSON_AddItemToObject(quick_remote,"tasks",old_catalog);
        preview_usb_connected=old_usb;wifi_connected=old_wifi;preview_result_code=0;
        quick_failed=false;quick_reset_idle();capture("quick-console-wireless-selection-recovered");
        puts("QUICK_WIRELESS_SYNC_OK four back-and-forth foreground confirmations with missing catalog, pending reads/draft sync and disabled hidden input; correct-target voice/cancel succeeds; pending sends remain guarded; no automatic stage/submit or forged worker completion");
    }
    lv_obj_send_event(quick_next,LV_EVENT_CLICKED,NULL);confirm_quick_open(1);
    int voiced_index=quick_index;
    codex_syncing=codex_load_waiting=false;
    codex_draft *quick_draft=codex_draft_for(codex_selected,true);assert(quick_draft);
    const char *local_draft="TAB5 本地草稿，必须保持原样";
    quick_draft->loaded=true;codex_set_text(local_draft);
    lv_obj_send_event(quick_voice_button,LV_EVENT_CLICKED,NULL);assert(quick_recording);
    strcpy(preview_voice.phase,"recording");strcpy(preview_voice.source,"dji");preview_voice.ready=true;
    strcpy(preview_voice.message,"可以说话了");
    strcpy(preview_voice.source_name,"麦克风 (Wireless Mic Rx)");quick_tick();
    assert(!strcmp(lv_label_get_text(quick_status),"可以说话了 · 麦克风 (Wireless Mic Rx)"));
    strcpy(preview_voice.source,"tab5");strcpy(preview_voice.source_name,"TAB5 内置麦克风");quick_tick();
    assert(strstr(lv_label_get_text(quick_status),"TAB5 内置麦克风")&&!strstr(lv_label_get_text(quick_status),"Wireless Mic Rx"));
    strcpy(preview_voice.source,"dji");strcpy(preview_voice.source_name,"麦克风 (Wireless Mic Rx)");quick_tick();
    assert(!lv_obj_has_flag(quick_recording_icon,LV_OBJ_FLAG_HIDDEN));
    assert(lv_color_eq(lv_obj_get_style_text_color(quick_status,0),lv_color_hex(0x54D69A)));
    strcpy(preview_voice.text,"这是一段较长的识别预览。补录内容只追加到电脑上的 Codex 输入框，保持未发送。TAB5 底部可以换行和滚动查看，原来的本地草稿不能被这次快捷语音改写。\n第二段录音同样保留原文，回填成功后再由我检查并按发送。");
    codex_voice_tick();quick_tick();assert(!strcmp(lv_textarea_get_text(codex_input),local_draft));
    capture("quick-console-recording");
    lv_obj_send_event(quick_next,LV_EVENT_CLICKED,NULL);assert(quick_index==voiced_index);
    int quick_before=preview_submit_count;
    lv_obj_send_event(quick_voice_button,LV_EVENT_CLICKED,NULL);assert(quick_stop_insert&&preview_voice_command==1);
    // A stop tap must repaint without waiting for the next host response/tick.
    assert(!lv_obj_has_flag(quick_work_spinner,LV_OBJ_FLAG_HIDDEN));
    assert(lv_anim_get(quick_work_spinner,NULL)!=NULL);
    assert(lv_obj_has_flag(quick_stop_icon,LV_OBJ_FLAG_HIDDEN));
    assert(!strcmp(lv_label_get_text(quick_status),"正在结束收音，识别完成后填入输入框…"));
    preview_voice_command=0;
    lv_obj_send_event(quick_voice_button,LV_EVENT_CLICKED,NULL);assert(preview_voice_command==0);
    capture("quick-console-stopping");
    // The host still reports recording until queued audio and stop arrive.
    quick_tick();quick_tick();
    assert(strstr(lv_label_get_text(quick_status),"正在结束收音"));
    assert(lv_obj_has_flag(quick_recording_icon,LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_has_state(quick_voice_button,LV_STATE_DISABLED));
    strcpy(preview_voice.phase,"draining");preview_voice.ready=false;quick_tick();
    assert(strstr(lv_label_get_text(quick_status),"正在结束收音"));
    strcpy(preview_voice.phase,"recognizing");strcpy(preview_voice.message,"正在识别…");quick_tick();
    assert(!strcmp(lv_label_get_text(quick_status),"正在识别…"));
    assert(!lv_obj_has_flag(quick_work_spinner,LV_OBJ_FLAG_HIDDEN));
    strcpy(preview_voice.text,"请检查这个项目的最新改动。");strcpy(preview_voice.phase,"review");
    preview_voice.busy=false;preview_voice.can_insert=true;
    codex_voice_tick();quick_tick();
    assert(preview_submit_count==quick_before&&quick_filling&&preview_desktop_stages==1);
    assert(!lv_obj_has_flag(quick_work_spinner,LV_OBJ_FLAG_HIDDEN));
    quick_tick();assert(preview_submit_count==quick_before&&preview_desktop_stages==1);
    assert(!strcmp(preview_desktop_text,preview_voice.text));
    assert(voiced_index>0&&!strcmp(preview_desktop_target,quick_tasks[voiced_index].id));
    preview_desktop.busy=false;preview_desktop.status=200;
    strcpy(preview_desktop.task_id,quick_tasks[voiced_index].id);strcpy(preview_desktop.message,"检查草稿，可继续语音补充；确认后点蓝色箭头发送");
    preview_desktop.appended=true; // An existing manually typed draft is still the first successful voice take.
    quick_tick();assert(!quick_filling&&!strcmp(lv_textarea_get_text(codex_input),local_draft));
    assert(lv_obj_has_flag(quick_recording_icon,LV_OBJ_FLAG_HIDDEN));
    assert(quick_ready&&preview_desktop_submits==0&&!lv_obj_has_flag(quick_send_icon,LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_has_flag(quick_work_spinner,LV_OBJ_FLAG_HIDDEN));
    assert(lv_anim_get(quick_work_spinner,NULL)==NULL);
    assert(!strcmp(lv_label_get_text(quick_status),"检查草稿，可继续语音补充；确认后点蓝色箭头发送"));
    assert(!strcmp(lv_label_get_text(lv_obj_get_child(quick_cancel_button,0)),"继续语音"));
    lv_obj_update_layout(quick_layer);
    const char *review_hint=lv_label_get_text(quick_status);
    int review_width=lv_text_get_width(review_hint,strlen(review_hint),lv_obj_get_style_text_font(quick_status,0),lv_obj_get_style_text_letter_space(quick_status,0));
    assert(review_width<=lv_obj_get_width(quick_status));
    assert(!strcmp(lv_label_get_text(lv_obj_get_child(quick_voice_button,0)),"发送"));
    assert(!lv_obj_has_flag(quick_record_icon,LV_OBJ_FLAG_HIDDEN));
    capture("quick-console-draft");
    char reopened_stage_request[37];strcpy(reopened_stage_request,quick_stage_request);
    lv_obj_send_event(quick_open_button,LV_EVENT_CLICKED,NULL);confirm_quick_open(voiced_index);
    assert(quick_ready&&quick_staged_once&&!strcmp(quick_stage_request,reopened_stage_request));
    assert(!lv_obj_has_flag(quick_clear_button,LV_OBJ_FLAG_HIDDEN)&&preview_desktop_submits==0);
    char staged_request[37];strcpy(staged_request,quick_stage_request);
    lv_obj_send_event(quick_voice_button,LV_EVENT_CLICKED,NULL);
    assert(quick_sending&&preview_desktop_submits==1&&!strcmp(preview_desktop_draft_request,staged_request));
    assert(!strcmp(preview_desktop_target,quick_tasks[voiced_index].id));
    lv_obj_send_event(quick_voice_button,LV_EVENT_CLICKED,NULL);assert(preview_desktop_submits==1);
    preview_desktop.busy=false;preview_desktop.status=409;strcpy(preview_desktop.message,"未确认发送，请检查电脑");
    quick_tick();assert(!quick_sending&&!quick_ready&&quick_failed);
    assert(!lv_obj_has_flag(quick_cancel_button,LV_OBJ_FLAG_HIDDEN));
    lv_obj_send_event(quick_cancel_button,LV_EVENT_CLICKED,NULL);
    assert(!quick_failed&&!quick_ready&&!quick_stage_request[0]);
    assert(!strcmp(lv_label_get_text(lv_obj_get_child(quick_voice_button,0)),"豆包"));
    // The same utterance recovered after a disconnect remains a draft.
    quick_filling=codex_waiting=codex_send_queued=false;quick_draft->request[0]=0;codex_set_text(local_draft);
    lv_obj_remove_state(codex_input,LV_STATE_DISABLED);quick_refresh();
    lv_obj_send_event(quick_voice_button,LV_EVENT_CLICKED,NULL);assert(quick_recording);
    preview_voice.ready=true;strcpy(preview_voice.phase,"recording");quick_tick();
    lv_obj_send_event(quick_voice_button,LV_EVENT_CLICKED,NULL);
    strcpy(preview_voice.text,"断线后恢复的部分文字");strcpy(preview_voice.phase,"review");
    preview_voice.busy=false;preview_voice.can_insert=false;
    codex_voice_tick();quick_tick();assert(preview_submit_count==quick_before&&!quick_filling&&preview_desktop_stages==1);
    assert(strstr(lv_label_get_text(quick_status),"文字已保留")&&!strcmp(lv_textarea_get_text(codex_input),local_draft));
    assert(quick_failed&&!quick_ready&&preview_desktop_submits==1);
    lv_obj_send_event(quick_cancel_button,LV_EVENT_CLICKED,NULL);
    assert(!strcmp(lv_textarea_get_text(codex_input),local_draft)&&!quick_failed&&!quick_ready);
    lv_obj_send_event(quick_voice_button,LV_EVENT_CLICKED,NULL);assert(quick_recording);
    preview_voice.ready=true;strcpy(preview_voice.phase,"recording");quick_tick();
    lv_obj_send_event(quick_cancel_button,LV_EVENT_CLICKED,NULL);
    assert(quick_resetting&&!quick_stop_insert&&preview_voice_command==2);
    strcpy(preview_voice.message,"可以说话了");quick_tick();
    assert(!strcmp(lv_label_get_text(quick_status),"正在取消语音…"));
    assert(lv_obj_has_flag(quick_recording_icon,LV_OBJ_FLAG_HIDDEN));
    strcpy(preview_voice.phase,"cancelled");preview_voice.busy=false;preview_voice.text[0]=0;
    codex_voice_tick();quick_tick();assert(!quick_recording&&!quick_resetting&&!quick_ready);
    assert(lv_obj_has_flag(quick_work_spinner,LV_OBJ_FLAG_HIDDEN)&&lv_anim_get(quick_work_spinner,NULL)==NULL);
    assert(!strcmp(lv_textarea_get_text(codex_input),local_draft));
    assert(preview_desktop_submits==1&&preview_desktop_stages==1);
    // A confirmed explicit submission returns the same key to initial state.
    quick_ready=true;strcpy(quick_stage_request,staged_request);quick_refresh();
    lv_obj_send_event(quick_voice_button,LV_EVENT_CLICKED,NULL);assert(quick_sending&&preview_desktop_submits==2);
    preview_desktop.busy=false;preview_desktop.status=200;strcpy(preview_desktop.task_id,quick_opened);
    quick_tick();assert(!quick_sending&&!quick_ready&&!quick_failed);
    assert(!quick_staged_once);
    assert(lv_obj_has_flag(quick_clear_button,LV_OBJ_FLAG_HIDDEN));
    assert(!strcmp(lv_label_get_text(lv_obj_get_child(quick_voice_button,0)),"豆包"));
    // Reset must preserve local user edits and attachments.
    quick_failed=true;strcpy(quick_saved_voice,"旧识别文字");codex_set_text("用户修改后的草稿");
    quick_cancel(NULL);assert(!strcmp(lv_textarea_get_text(codex_input),"用户修改后的草稿"));
    // Hints count only successful voice takes and require proof of actual append.
    quick_staged_once=false;preview_desktop.busy=false;preview_desktop.status=409;
    quick_filling=true;quick_tick();assert(quick_failed&&!quick_staged_once);
    assert(!lv_obj_has_flag(quick_cancel_button,LV_OBJ_FLAG_HIDDEN));
    assert(!strcmp(lv_label_get_text(lv_obj_get_child(quick_cancel_button,0)),"重试录音"));
    preview_desktop.status=200;
    strcpy(preview_desktop.task_id,quick_opened);strcpy(preview_desktop.message,"检查草稿，可继续语音补充；确认后点蓝色箭头发送");
    preview_desktop.appended=false;quick_filling=true;quick_tick();
    assert(quick_staged_once&&!strcmp(lv_label_get_text(quick_status),preview_desktop.message));
    quick_reset_idle();assert(quick_staged_once);
    preview_desktop.status=409;quick_filling=true;quick_tick();assert(quick_failed&&quick_staged_once);
    preview_desktop.status=200;preview_desktop.appended=true;quick_filling=true;quick_tick();
    assert(!strcmp(lv_label_get_text(quick_status),"已追加，可继续语音补充；检查后点蓝色箭头发送"));
    const char *append_hint=lv_label_get_text(quick_status);
    lv_obj_update_layout(quick_layer);
    int append_width=lv_text_get_width(append_hint,strlen(append_hint),lv_obj_get_style_text_font(quick_status,0),lv_obj_get_style_text_letter_space(quick_status,0));
    assert(append_width<=lv_obj_get_width(quick_status));
    printf("QUICK_VOICE_HINT_SINGLE_LINE_OK review=%d append=%d available=%d\n",review_width,append_width,(int)lv_obj_get_width(quick_status));
    capture("quick-console-append-hint");
    preview_desktop.appended=false;quick_filling=true;quick_tick();
    assert(!strcmp(lv_label_get_text(quick_status),preview_desktop.message));
    capture("quick-console-clear-control");
    assert(!lv_obj_has_flag(quick_clear_button,LV_OBJ_FLAG_HIDDEN));
    assert(!strcmp(lv_label_get_text(lv_obj_get_child(quick_clear_button,0)),"长按清空"));
    assert(lv_obj_get_width(quick_status)==744);
    assert(lv_obj_get_width(quick_clear_button)==lv_obj_get_width(quick_cancel_button));
    lv_obj_update_layout(quick_layer);
    lv_area_t clear_label_bounds,clear_button_bounds;
    lv_obj_get_coords(lv_obj_get_child(quick_clear_button,0),&clear_label_bounds);
    lv_obj_get_coords(quick_clear_button,&clear_button_bounds);
    assert(clear_label_bounds.x2<clear_button_bounds.x2&&clear_label_bounds.y2<=clear_button_bounds.y2);
    lv_obj_send_event(quick_clear_button,LV_EVENT_CLICKED,NULL);assert(preview_desktop_clears==0);
    lv_obj_send_event(quick_clear_button,LV_EVENT_LONG_PRESSED,NULL);
    assert(quick_clearing&&preview_desktop_clears==1&&!strcmp(preview_desktop_target,quick_opened));
    lv_obj_send_event(quick_clear_button,LV_EVENT_LONG_PRESSED,NULL);assert(preview_desktop_clears==1);
    quick_close(NULL);assert(quick_visible);
    preview_desktop.busy=false;preview_desktop.status=200;strcpy(preview_desktop.task_id,quick_opened);
    strcpy(preview_desktop.message,"输入框已清空，可重新录音");quick_tick();
    assert(!quick_clearing&&!quick_ready&&!quick_failed&&!quick_stage_request[0]&&!quick_staged_once);
    assert(!strcmp(lv_textarea_get_text(codex_input),"用户修改后的草稿"));
    assert(!strcmp(lv_label_get_text(lv_obj_get_child(quick_voice_button,0)),"豆包"));
    capture("quick-console-cleared");
    assert(lv_obj_has_flag(quick_clear_button,LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_get_width(quick_status)==952);
    lv_obj_send_event(quick_clear_button,LV_EVENT_LONG_PRESSED,NULL);assert(!quick_clearing&&preview_desktop_clears==1);
    // A new verified take restores the control; failed clearing keeps it
    // available because the existing desktop draft was not confirmed empty.
    strcpy(preview_desktop.message,"检查草稿，可继续语音补充；确认后点蓝色箭头发送");
    quick_filling=true;quick_tick();assert(quick_staged_once&&!lv_obj_has_flag(quick_clear_button,LV_OBJ_FLAG_HIDDEN));
    quick_reset_idle();assert(!lv_obj_has_flag(quick_clear_button,LV_OBJ_FLAG_HIDDEN));
    lv_obj_send_event(quick_clear_button,LV_EVENT_LONG_PRESSED,NULL);assert(quick_clearing&&preview_desktop_clears==2);
    preview_desktop.busy=false;preview_desktop.status=409;strcpy(preview_desktop.message,"未确认清空，请检查电脑当前会话输入框");
    quick_tick();assert(quick_failed&&!quick_ready&&!quick_clearing);
    assert(!lv_obj_has_flag(quick_clear_button,LV_OBJ_FLAG_HIDDEN));
    assert(!strcmp(lv_textarea_get_text(codex_input),"用户修改后的草稿"));
    quick_reset_idle();
    puts("QUICK_DRAFT_HINT_CLEAR_UI_OK first_review, repeated_actual_append, empty_draft_review, failed_take_not_counted, long_press_clear_only, one_clear_while_busy, local_draft_preserved, clear_restores_voice");
    // Switching after staging cannot carry an old target's send entry into
    // the new conversation. Saving the old local draft remains independent.
    quick_ready=quick_staged_once=true;strcpy(quick_stage_request,staged_request);quick_refresh();
    int submissions_before_switch=preview_desktop_submits;
    lv_obj_send_event(quick_next,LV_EVENT_CLICKED,NULL);confirm_quick_open(voiced_index+1);
    assert(!quick_ready&&!quick_staged_once&&!quick_stage_request[0]&&preview_desktop_submits==submissions_before_switch);
    assert(!strcmp(quick_draft->text,"用户修改后的草稿"));
    puts("QUICK_SELECTED_VOICE_UI_OK older_target_stage_and_explicit_send, same_target_reopen_preserves_ticket, switching_drops_old_send_entry_without_submission_or_draft_loss");
    // A desktop send/manual clear may leave the device's old send state behind.
    quick_ready=quick_staged_once=true;strcpy(quick_stage_request,staged_request);quick_refresh();
    int before_empty_submits=preview_desktop_submits,before_empty_clears=preview_desktop_clears;
    lv_obj_send_event(quick_open_button,LV_EVENT_CLICKED,NULL);
    preview_desktop.draft_empty=true;confirm_quick_open(quick_index);
    assert(!quick_ready&&!quick_staged_once&&!quick_stage_request[0]&&!quick_failed);
    assert(lv_obj_has_flag(quick_send_icon,LV_OBJ_FLAG_HIDDEN)&&!lv_obj_has_flag(quick_doubao_icon,LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_has_flag(quick_clear_button,LV_OBJ_FLAG_HIDDEN));
    assert(!strcmp(lv_label_get_text(quick_status),"输入框为空，点豆包开始录音"));
    assert(preview_desktop_submits==before_empty_submits&&preview_desktop_clears==before_empty_clears);
    assert(!strcmp(quick_draft->text,"用户修改后的草稿"));
    lv_obj_send_event(quick_voice_button,LV_EVENT_CLICKED,NULL);
    assert(quick_recording&&!quick_sending&&preview_desktop_submits==before_empty_submits);
    lv_obj_send_event(quick_cancel_button,LV_EVENT_CLICKED,NULL);
    preview_voice.busy=false;strcpy(preview_voice.phase,"cancelled");codex_voice_tick();quick_tick();
    assert(!quick_recording&&!quick_resetting);
    puts("QUICK_EMPTY_DRAFT_REOPEN_OK empty_restores_voice, next_click_records_without_send, local_draft_preserved, unknown_keeps_ticket");
    codex_set_text("");quick_saved_voice[0]=0;
    quick_close(NULL);assert(!quick_visible);
    puts("QUICK_CONSOLE_UI_OK mechanical_press_release_animation, press_lost_restores_without_action, short_labels, permanent_footer_entry_all_pages, latest_five, desktop_synced_selection, same_target_reopen_keeps_staged_draft, voice_target_lock, explicit_stop_draft_once, explicit_third_click_send, no_double_send, error_reset, rerecord, cancel_no_insert, user_edits_preserved");

    cJSON_Delete(shown); shown=cJSON_CreateObject(); render(); clock_music(10000,false);
    page(1);capture("empty-quota");page(5);capture("unknown-duration");
    assert(lv_obj_has_flag(progress,LV_OBJ_FLAG_HIDDEN));
    assert(!lv_obj_has_flag(quota_empty,LV_OBJ_FLAG_HIDDEN));
    check_system_chart_sampling();
    check_remote_ota_ui();
    puts("LVGL_PREVIEW_OK twelve pages, Codex selection and missing data states rendered");
    return 0;
}

