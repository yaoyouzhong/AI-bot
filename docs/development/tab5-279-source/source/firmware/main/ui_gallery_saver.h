#include "gallery_remote.h"
#include "art_orientation.h"
#ifdef TAB5_UI_PREVIEW
#include <assert.h>
static gallery_image preview_gallery_result;
static unsigned preview_gallery_requests;
static int preview_gallery_requested_frame;
static bool preview_gallery_requested_portrait;
static int preview_gallery_pack_status[2];
static int preview_gallery_decode_status[4];
int gallery_decode_status(int category,bool portrait){return category>=0&&category<2?preview_gallery_decode_status[category*2+portrait]:GALLERY_DECODE_OK;}
static unsigned preview_gallery_failures;
unsigned gallery_take_failures(void){unsigned mask=preview_gallery_failures;preview_gallery_failures=0;return mask;}
int gallery_pack_status(int category){return preview_gallery_pack_status[category];}
static bool preview_gallery_ready=true;
static unsigned preview_gallery_prefetch_calls;
static char preview_gallery_prefetch_date[11];
void gallery_prefetch_day(const char *date){preview_gallery_prefetch_calls++;snprintf(preview_gallery_prefetch_date,sizeof(preview_gallery_prefetch_date),"%s",date);}
void gallery_sync_library(void){}
static bool preview_gallery_library_pending;
bool gallery_library_pending(void){return preview_gallery_library_pending;}
void gallery_library_supported(bool enabled){(void)enabled;}
static bool preview_gallery_storage_enabled;
void gallery_storage_enable(bool enabled){preview_gallery_storage_enabled=enabled;}
void gallery_storage_status(char *out,size_t size){snprintf(out,size,"%s",preview_gallery_storage_enabled?"正在保存 · 名画 123/366 · 书法 88/366":"使用设备缓存");}
static gallery_guide preview_guide;
static unsigned preview_gallery_pack_changes;
unsigned gallery_pack_sync(const char *painting,const char *calligraphy){
    (void)painting;(void)calligraphy;unsigned changed=preview_gallery_pack_changes;preview_gallery_pack_changes=0;return changed;
}
bool gallery_guide_copy(int category,const char *date,const char *work_id,gallery_guide *out){
    (void)category;if(!preview_guide.work_id[0]||strcmp(preview_guide.date,date)||strcmp(preview_guide.work_id,work_id))return false;*out=preview_guide;memset(out->transcription_pages,0,sizeof(out->transcription_pages));return true;
}
bool gallery_guide_page_copy(int category,const char *date,const char *work_id,int page,gallery_guide *out){
    if(page<0||page>=(preview_guide.transcription_page_count?preview_guide.transcription_page_count:1)||!gallery_guide_copy(category,date,work_id,out))return false;
    if(preview_guide.transcription_page_count>1) {
        if(!preview_guide.transcription_pages[page])return false;
        snprintf(out->transcription,sizeof(out->transcription),"%s",preview_guide.transcription_pages[page]);
    }
    memset(out->transcription_pages,0,sizeof(out->transcription_pages));return true;
}
void gallery_guide_reader_release(gallery_guide *g){if(g)for(int i=0;i<32;i++){free(g->transcription_pages[i]);g->transcription_pages[i]=NULL;}}
bool gallery_guide_reader_copy(int category,const char *date,const char *work_id,gallery_guide *out){
    gallery_guide_reader_release(out);
    if(!gallery_guide_copy(category,date,work_id,out))return false;
    memset(out->transcription_pages,0,sizeof(out->transcription_pages));
    for(int i=0;i<preview_guide.transcription_page_count&&preview_guide.transcription_page_count>1;i++) {
        const char *part=preview_guide.transcription_pages[i];
        if(!part){gallery_guide_reader_release(out);return false;}
        out->transcription_pages[i]=malloc(strlen(part)+1);
        if(!out->transcription_pages[i]){gallery_guide_reader_release(out);return false;}
        strcpy(out->transcription_pages[i],part);
    }
    return true;
}
static uint8_t *preview_gallery_spare;
static bool preview_gallery_download,preview_gallery_fail_download;
static unsigned preview_gallery_allocations;
bool gallery_request_ready(void){return preview_gallery_ready&&!preview_gallery_result.pixels;}
bool gallery_request_available(int category,const char *date,int frame,bool portrait){(void)category;(void)date;(void)frame;(void)portrait;return gallery_request_ready();}
bool gallery_request(int category,const char *date,int frame,bool portrait,uint8_t *spare){
    preview_gallery_requested_frame=frame;preview_gallery_requested_portrait=portrait;preview_gallery_requests++;
    if(!spare)spare=gallery_take_buffer();preview_gallery_spare=spare;
    if(!preview_gallery_download){gallery_recycle(spare);return false;}
    if(!spare){
        if(preview_gallery_allocations>=2)return false;
        spare=calloc(1,1280*720*2);assert(spare);preview_gallery_allocations++;
    }
    if(preview_gallery_fail_download){
        gallery_recycle(spare);preview_gallery_failures|=1u<<(category*2+portrait);
        preview_gallery_fail_download=false;return true;
    }
    memset(spare,frame*4+portrait+1,1280*720*2);
    preview_gallery_result=(gallery_image){.pixels=spare,.category=category,.frame=frame,.frames=3,.portrait=portrait};
    if(!strcmp(preview_guide.date,date))snprintf(preview_gallery_result.work_id,sizeof(preview_gallery_result.work_id),"%s",preview_guide.work_id);
    snprintf(preview_gallery_result.date,sizeof(preview_gallery_result.date),"%s",date);return true;
}
bool gallery_take(gallery_image *out){if(!preview_gallery_result.pixels)return false;*out=preview_gallery_result;preview_gallery_result.pixels=NULL;return true;}
#endif
static lv_obj_t *gallery_layer,*gallery_view,*gallery_hint,*gallery_date;
static gallery_image gallery_cache[4];
static lv_image_dsc_t gallery_descriptor[4];
static gallery_image gallery_presented;
static int64_t gallery_retry[4],gallery_displayed[4];
static unsigned gallery_failures[4];
static int gallery_orientation,gallery_last_portrait=1,gallery_frame[2];
static bool gallery_portrait(void){return gallery_orientation==1||gallery_orientation==-1;}
static gallery_image gallery_pending_page;
static int64_t gallery_pending_until;
static bool gallery_camera_reload;
static int gallery_slot(int category){return category*2+gallery_portrait();}
static void gallery_label_direction(lv_obj_t *label,int x,int y,bool inverted) {
    int angle=inverted?1800:0,px=inverted?1279-x:x,py=inverted?719-y:y;
    if(lv_obj_get_style_transform_rotation(label,0)==angle&&lv_obj_get_x(label)==px&&lv_obj_get_y(label)==py)return;
    lv_obj_set_style_transform_pivot_x(label,0,0);lv_obj_set_style_transform_pivot_y(label,0,0);
    lv_obj_set_style_transform_rotation(label,angle,0);
    lv_obj_set_pos(label,px,py);
}
static void gallery_drop(int slot) {
    lv_image_cache_drop(&gallery_descriptor[slot]);gallery_recycle(gallery_cache[slot].pixels);
    gallery_cache[slot]=(gallery_image){0};gallery_descriptor[slot]=(lv_image_dsc_t){0};
    gallery_retry[slot]=0;
}
#include "ui_gallery_transition.h"
static bool gallery_schedule(int category,const char *date,int frame,bool portrait) {
    if(gallery_motion_active)return false;
    if(!gallery_request_available(category,date,frame,portrait))return false;
    // Transfer ownership of an unused bitmap, never the LVGL image currently
    // displayed (which may be the fallback orientation). This bounds output
    // memory to two frames including the worker, without heap probing.
    const void *shown=lv_obj_has_flag(gallery_view,LV_OBJ_FLAG_HIDDEN)?NULL:lv_image_get_src(gallery_view);
    int spare=-1;
    for(int i=0;i<4;i++)if(gallery_cache[i].pixels&&shown!=&gallery_descriptor[i]&&
        (spare<0||gallery_displayed[i]<gallery_displayed[spare]))spare=i;
    uint8_t *pixels=NULL;
    if(spare>=0){
        pixels=gallery_cache[spare].pixels;lv_image_cache_drop(&gallery_descriptor[spare]);
        gallery_cache[spare]=(gallery_image){0};gallery_descriptor[spare]=(lv_image_dsc_t){0};
        gallery_retry[spare]=0;
    }
    return gallery_request(category,date,frame,portrait,pixels);
}
#include "ui_gallery_reader.h"
static void gallery_sync_packs(cJSON *packs) {
    unsigned changed=gallery_pack_sync(cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(packs,"painting")),
        cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(packs,"calligraphy")));
    if(!changed)return;
    gallery_motion_finish();
    if(gallery_details_open&&(changed&(1u<<gallery_reading_category)))gallery_reader_close();
    for(int c=0;c<2;c++)if(changed&(1u<<c)) {
        // Keep the last successful bitmap visible until its replacement arrives.
        // Clearing its date makes it ineligible for warm-cache reuse.
        for(int i=c*2;i<c*2+2;i++){gallery_cache[i].date[0]=0;gallery_retry[i]=0;gallery_failures[i]=0;}
        gallery_frame[c]=0;
        if(gallery_pending_page.date[0]&&gallery_pending_page.category==c)gallery_pending_page=(gallery_image){0};
    }
    gallery_manual_until=0;gallery_return_hold=false;
}
static void gallery_hint_update(int category) {
    int pack=gallery_pack_status(category);
    int decode=gallery_decode_status(category,gallery_portrait());
    ui_text(gallery_hint,pack==1?(category?"请在电脑端安装书法图库":"请在电脑端安装名画图库"):pack==2?"图库文件不完整，请在电脑端重新导入":decode==GALLERY_DECODE_NO_MEMORY?"图片解码内存不足，稍后自动重试":decode==GALLERY_DECODE_FAILED?"图片解码失败，稍后自动重试":!gallery_request_ready()?"正在同步当天作品…":"等待电脑同步图片");
}
static void gallery_show(void) {
    int category=saver_style-6;if(category<0||category>1)return;
    int slot=gallery_slot(category);
    // Offline or older bridges keep the last successful work visible while
    // its other orientation is unavailable, rather than replacing it with blank.
    int other=category*2+!gallery_portrait();
    if(!gallery_cache[slot].pixels||(gallery_cache[slot].frame!=gallery_frame[category]&&gallery_cache[other].pixels&&gallery_cache[other].frame==gallery_frame[category]))slot=other;
    bool ready=gallery_cache[slot].pixels!=NULL;
    // Hold the old layout steady until the requested bitmap is available.
    // Otherwise one turn can repaint an intermediate inverted fallback first.
    if(ready&&gallery_cache[slot].portrait!=gallery_portrait()&&
       lv_image_get_src(gallery_view)==&gallery_descriptor[slot]&&gallery_presented.pixels==gallery_cache[slot].pixels)return;
    if(!ready)gallery_hint_update(category);
    bool portrait=ready&&gallery_cache[slot].portrait;
    int direction=portrait?(gallery_portrait()?gallery_orientation:gallery_last_portrait):gallery_orientation;
    bool inverted=portrait?direction>0:direction==2;
    ui_bg_color(gallery_layer,lv_color_hex(category?0xE7E0D1:0x111516),0);
    tint(gallery_hint,category?0x5C5447:0xBCBDB6);tint(gallery_date,category?0x776E60:0x929A96);
    visible(gallery_hint,!ready);visible(gallery_view,ready);visible(gallery_date,false);
    if(!ready)gallery_label_direction(gallery_hint,0,336,gallery_orientation==2);
    gallery_label_direction(gallery_date,1100,8,!portrait&&inverted);
    if(ready){
        // LVGL invalidates even when the descriptor pointer is unchanged.
        // A reverse-layout warmup must leave the currently displayed frame
        // untouched; detect replacements even when a slot's descriptor is reused.
        gallery_image *image=&gallery_cache[slot];
        lv_display_t *display=lv_obj_get_display(gallery_layer);
        bool moving=false;
        if(lv_image_get_src(gallery_view)!=&gallery_descriptor[slot]||
           gallery_presented.pixels!=image->pixels||gallery_presented.frame!=image->frame||
           strcmp(gallery_presented.date,image->date)||strcmp(gallery_presented.work_id,image->work_id)){
            moving=gallery_old_image.pixels!=NULL;
            if(gallery_motion_enabled())for(int old=0;old<4;old++)if(old!=slot&&gallery_cache[old].pixels&&lv_image_get_src(gallery_view)==&gallery_descriptor[old])moving=true;
            if(moving)lv_display_enable_invalidation(display,false);
            if(gallery_motion_enabled()&&!gallery_old_image.pixels){
                for(int old=0;old<4;old++)if(old!=slot&&lv_image_get_src(gallery_view)==&gallery_descriptor[old])gallery_motion_hold(old,false);
            }
            lv_image_set_src(gallery_view,&gallery_descriptor[slot]);gallery_presented=*image;
        }
        // Verified on the mounted TAB5: positive raw Y needs the 180-degree
        // counterpart of the pre-rotated portrait bitmap.
        lv_image_set_pivot(gallery_view,640,360);lv_image_set_rotation(gallery_view,inverted?1800:0);
        if(!gallery_motion_active)lv_obj_set_pos(gallery_view,inverted?-1:0,inverted?-1:0);
        ui_text(gallery_date,gallery_cache[slot].date);
        if(gallery_old_image.pixels&&!gallery_motion_active)gallery_motion_begin();
        if(moving)lv_display_enable_invalidation(display,true);
    }else gallery_presented=(gallery_image){0};
    gallery_controls_update(category,slot,ready);
}
static void gallery_update(const struct tm *local) {
    int category=saver_style-6;if(category<0||category>1)return;
    // Reading pins the work, frame and date, while the device's orientation
    // continues to drive the reader layout and its transformed touch targets.
    art_orientation_state orientation=art_orientation_get();
    if(orientation.ready&&orientation.orientation!=gallery_orientation){
        gallery_motion_finish();gallery_motion_step=1;
        gallery_orientation=orientation.orientation;if(gallery_portrait())gallery_last_portrait=gallery_orientation;
        gallery_retry[gallery_slot(category)]=0;
        if(gallery_details_open)gallery_reader_layout();else gallery_show();
    }
    if(gallery_details_open){gallery_prefetch_day(gallery_reading->date);return;}
    if(lv_obj_has_flag(gallery_view,LV_OBJ_FLAG_HIDDEN))gallery_hint_update(category);
    int64_t now=esp_timer_get_time()/1000;gallery_image incoming;
    // Camera reclaim keeps only the requested work/page metadata. Start its
    // normal retry window when the gallery becomes visible again, not while
    // the user may spend minutes taking photos.
    if(gallery_camera_reload){gallery_camera_reload=false;gallery_pending_until=now+60000;}
    // A started request used to reserve its slot for 60 seconds even when it
    // had already failed. Retry completed failures with bounded backoff while
    // preserving the visible bitmap; never launch concurrent downloads.
    unsigned failed=gallery_take_failures();
    for(int i=0;i<4;i++)if(failed&(1u<<i)){
        if(gallery_failures[i]<4)gallery_failures[i]++;
        int delay=1000<<gallery_failures[i];if(delay>15000)delay=15000;
        gallery_retry[i]=now+delay;
    }
    if(gallery_take(&incoming)) {
        gallery_motion_finish();
        bool retained=gallery_return_hold&&now<gallery_manual_until;
        if(retained&&(incoming.category!=gallery_reading_category||incoming.frame!=gallery_reading_frame||strcmp(incoming.date,gallery_reading->date))){
            gallery_recycle(incoming.pixels);return; // Late auto page cannot replace the work being returned to.
        }
        int slot=incoming.category*2+incoming.portrait;
        if(incoming.category>=0&&incoming.category<2) {
            bool transitioning=gallery_motion_enabled()&&gallery_cache[slot].pixels&&lv_image_get_src(gallery_view)==&gallery_descriptor[slot];
            lv_display_t *display=lv_obj_get_display(gallery_layer);
            if(transitioning)lv_display_enable_invalidation(display,false);
            gallery_failures[slot]=0;
            if(lv_image_get_src(gallery_view)==&gallery_descriptor[slot])gallery_motion_hold(slot,true);
            gallery_drop(slot);gallery_cache[slot]=incoming;
            gallery_descriptor[slot]=(lv_image_dsc_t){.header={.magic=LV_IMAGE_HEADER_MAGIC,.cf=LV_COLOR_FORMAT_RGB565,.w=1280,.h=720,.stride=2560},.data_size=1280*720*2,.data=incoming.pixels};
            gallery_frame[incoming.category]=incoming.frame;
            if(gallery_pending_page.date[0]&&gallery_pending_page.category==incoming.category&&
               gallery_pending_page.frame==incoming.frame&&!strcmp(gallery_pending_page.date,incoming.date))
                gallery_pending_page=(gallery_image){0};
            gallery_displayed[slot]=now;gallery_retry[slot]=now+30000;
            // Keep at most two RGB565 frames: fast orientation changes without
            // growing the former two-category cache or camera/voice memory peaks.
            int count=0,oldest=-1,shown=gallery_slot(category);
            for(int i=0;i<4;i++)if(gallery_cache[i].pixels){count++;if(i!=slot&&i!=shown&&(oldest<0||gallery_displayed[i]<gallery_displayed[oldest]))oldest=i;}
            if(count>2&&oldest>=0)gallery_drop(oldest);
            gallery_show();
            if(transitioning){lv_obj_update_layout(gallery_layer);lv_display_enable_invalidation(display,true);lv_obj_invalidate(gallery_controls);}
        }else gallery_release_buffer(incoming.pixels);
    }
    if(gallery_motion_active){gallery_prefetch_day(gallery_presented.date);return;}
    if(gallery_pending_page.date[0]){
        if(gallery_pending_page.category!=category||now>=gallery_pending_until)gallery_pending_page=(gallery_image){0};
        else {
            if(gallery_schedule(category,gallery_pending_page.date,gallery_pending_page.frame,gallery_portrait())){
                // Keep the requested page until its result arrives. Rapid
                // taps advance this target while the old page remains visible.
                gallery_manual_until=now+30000;gallery_return_hold=false;
            }
            return;
        }
    }
    char date[11];strftime(date,sizeof(date),"%Y-%m-%d",local);
    int slot=gallery_slot(category);gallery_image *cached=&gallery_cache[slot];
    bool today=cached->pixels&&!strcmp(cached->date,date);
    bool same_frame=today&&cached->frame==gallery_frame[category];
    if(now<gallery_manual_until){
        // A manual page/reader return pins the work and page, not its layout.
        // Load that same displayed page in the requested orientation during
        // the hold instead of leaving its fallback bitmap for 30 seconds.
        int shown=gallery_shown_slot(category);
        if(shown>=0){
            gallery_image *held=&gallery_cache[shown];
            bool matching=cached->pixels&&cached->frame==held->frame&&!strcmp(cached->date,held->date);
            if(!matching&&now>=gallery_retry[slot])
                gallery_retry[slot]=now+(gallery_schedule(category,held->date,held->frame,gallery_portrait())?60000:1000);
            else if(matching){
                // Prepare the other layout before the next rotation, including
                // manual paging and reader return. The hold only pauses paging.
                int other=category*2+!gallery_portrait();gallery_image *alternate=&gallery_cache[other];
                if((!alternate->pixels||alternate->frame!=held->frame||strcmp(alternate->date,held->date))&&now>=gallery_retry[other])
                    gallery_retry[other]=now+(gallery_schedule(category,held->date,held->frame,!gallery_portrait())?60000:1000);
            }
        }
        gallery_prefetch_day(date);return;
    }
    if(same_frame&&(cached->frames==1||now-gallery_displayed[slot]<30000)){
        // Warm the matching layout while viewing the current page. Rotation
        // then needs no download; foreground work still takes priority.
        int other=category*2+!gallery_portrait();gallery_image *alternate=&gallery_cache[other];
        if((!alternate->pixels||strcmp(alternate->date,date)||alternate->frame!=cached->frame)&&now>=gallery_retry[other])
            gallery_retry[other]=now+(gallery_schedule(category,date,cached->frame,!gallery_portrait())?60000:1000);
        gallery_prefetch_day(date);return;
    }
    if(now<gallery_retry[slot])return;
    int other=category*2+!gallery_portrait();
    bool other_today=gallery_cache[other].pixels&&!strcmp(gallery_cache[other].date,date);
    int frame=same_frame?(cached->frame+1)%cached->frames:other_today?gallery_frame[category]:0;
    gallery_retry[slot]=now+(gallery_schedule(category,date,frame,gallery_portrait())?60000:1000);
}
static UI_INIT_SMALL void gallery_create(void) {
    gallery_layer=lv_obj_create(saver_layer);lv_obj_remove_style_all(gallery_layer);lv_obj_set_size(gallery_layer,1280,720);
    lv_obj_remove_flag(gallery_layer,LV_OBJ_FLAG_SCROLLABLE|LV_OBJ_FLAG_CLICKABLE);lv_obj_set_style_bg_opa(gallery_layer,LV_OPA_COVER,0);
    gallery_view=lv_image_create(gallery_layer);lv_obj_remove_flag(gallery_view,LV_OBJ_FLAG_CLICKABLE);
    gallery_motion_create();
    gallery_hint=label(gallery_layer,"等待电脑同步图片",0,336,1280,false);lv_obj_set_style_text_align(gallery_hint,LV_TEXT_ALIGN_CENTER,0);
    gallery_date=label(gallery_layer,"",1100,8,160,false);lv_obj_set_style_text_align(gallery_date,LV_TEXT_ALIGN_RIGHT,0);
    ui_text_font(gallery_date,&lv_font_montserrat_24,0);
    clock_touch_target(gallery_layer);gallery_reader_create();visible(gallery_layer,false);visible(gallery_view,false);visible(gallery_date,false);
}
