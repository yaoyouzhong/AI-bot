// Included after the composer and camera callbacks; all functions run on LVGL.
#include "photo_geometry.h"
#include "photo_display_probe.h"
static lv_obj_t *photo_window,*photo_title,*photo_note,*photo_files,*photo_view,*photo_confirm,*photo_up,*photo_prev,*photo_next,*photo_close_button;
static lv_obj_t *photo_rows[PHOTO_LIST_COUNT],*composer_photos[3],*composer_thumbs[3],*composer_remove[3];
static struct {char id[37];uint16_t *pixels;unsigned width,height;} photo_cache[3];
static unsigned photo_cache_next;
static char photo_task[37],photo_id[37],photo_directory[512]="/sdcard";
static photo_listing *photo_visible_listing;
static int photo_step; // 1 prepare, 2 preview, 3 uploading, 4 list, 5 wait for still scanout
static bool photo_from_camera;
static bool photo_camera_retry_prepare;
static bool photo_camera_encode_pending;
static int photo_camera_hold_result;
static void photo_camera_begin(bool encode);
static void photo_tick(void);
static const char *photo_camera_start_error;
static void photo_view_layout(void) {
    int w=lv_display_get_horizontal_resolution(NULL),h=lv_display_get_vertical_resolution(NULL);
    lv_obj_set_size(photo_window,w,h);
    lv_obj_set_width(photo_title,w-236);lv_obj_set_x(photo_close_button,w-208);
    lv_obj_set_pos(photo_note,0,h-110);lv_obj_set_width(photo_note,w-284);
    lv_obj_set_pos(photo_confirm,w-264,h-122);
    lv_obj_set_size(photo_files,w-64,h-210);
    const lv_draw_buf_t *buf=lv_canvas_get_draw_buf(photo_view);
    if(!buf||!buf->data||!buf->header.w||!buf->header.h)return;
    unsigned fw,fh;photo_fit(buf->header.w,buf->header.h,w-64,h-180,&fw,&fh);
    unsigned sx=fw*256/buf->header.w,sy=fh*256/buf->header.h,scale=sx<sy?sx:sy;
    lv_image_set_pivot(photo_view,0,0);lv_image_set_scale(photo_view,scale);
    lv_obj_set_pos(photo_view,(w-64-(int)(buf->header.w*scale/256))/2,
        64+(h-180-(int)(buf->header.h*scale/256))/2);
}
static void photo_view_orientation(void) {
    display_orientation_apply(camera_current_pose());photo_view_layout();
}
static void photo_set_view(const uint16_t *pixels,unsigned width,unsigned height) {
    lv_canvas_set_buffer(photo_view,(void *)pixels,width,height,LV_COLOR_FORMAT_RGB565);
    photo_view_layout();
}

static void photo_camera_busy(bool busy) {
    photo_camera_processing=busy;
    lv_obj_t *controls[]={camera_close,camera_retry,camera_use,photo_confirm,photo_close_button};
    for(unsigned i=0;i<sizeof(controls)/sizeof(controls[0]);i++) {
        if(busy)lv_obj_add_state(controls[i],LV_STATE_DISABLED);
        else lv_obj_remove_state(controls[i],LV_STATE_DISABLED);
    }
    ui_text(lv_obj_get_child(camera_use,0),busy?"添加中…":"添加照片");
}
static bool photo_camera_return(bool show_window) {
    // A failed fence retains scanout ownership. Leave the frozen controls
    // disabled and retry on the next UI turn; never expose live hit targets
    // underneath a photograph which is still being scanned.
    if(!camera_viewport_still_end())return false;
    // Restore page geometry in this same LVGL turn. Successful camera uploads
    // reveal the composer directly; only retained-photo errors need the dialog.
    photo_camera_busy(false);
    screen_orientation_apply(screen_orientation.direction);
    visible(camera_layer,false);visible(photo_window,show_window);
    if(show_window)lv_obj_move_foreground(photo_window);
    if(show_window)photo_view_orientation();
    return true;
}

static void codex_images_refresh(void) {
    if(!composer_photos[0])return;
    codex_draft *d=codex_draft_for(codex_selected,false);bool any=d&&d->images[0][0];
    for(int i=0;i<3;i++) {
        visible(composer_photos[i],d&&d->images[i][0]);
        if(d&&d->thumbnails[i]){lv_canvas_set_buffer(composer_thumbs[i],d->thumbnails[i],96,54,LV_COLOR_FORMAT_RGB565);visible(composer_thumbs[i],true);}
        else visible(composer_thumbs[i],false);
    }
    if(codex_keyboard&&lv_obj_has_flag(codex_keyboard,LV_OBJ_FLAG_HIDDEN))lv_obj_set_height(codex_input,any?170:250);
}
static void photo_finish(void) {
    if(photo_camera_processing){if(!photo_camera_return(false))return;}
    else photo_camera_busy(false);
    lv_obj_add_flag(photo_window,LV_OBJ_FLAG_HIDDEN);lv_obj_add_flag(camera_layer,LV_OBJ_FLAG_HIDDEN);
    photo_flow=photo_for_codex=false;photo_step=0;photo_discard();codex_images_refresh();
    screen_orientation_apply(screen_orientation.direction);
    codex_sync_next=esp_timer_get_time()/1000+1200;codex_read_next=codex_sync_next;
}
static void photo_upload(void) {
    size_t size=0;const uint8_t *data=photo_data(&size);
    int64_t age=esp_timer_get_time()/1000-last_received;
    const char *session=text(item(shown,"pcInput"),"session");
    if(!data||age<0||age>6000||!strcmp(session,"--")){ui_text(photo_note,"电脑连接未就绪，照片已保留");return;}
    if(!photo_id[0])codex_uuid(photo_id);
    if(!codex_remote_upload(photo_task,session,(int64_t)num(shown,"epochMilliseconds")+age,photo_id,data,size)){ui_text(photo_note,"电脑正在同步，请稍后点添加");return;}
    photo_step=3;ui_text(photo_note,"正在添加照片…");lv_obj_add_state(photo_confirm,LV_STATE_DISABLED);lv_obj_add_state(photo_close_button,LV_STATE_DISABLED);
    if(photo_camera_processing)ui_text(lv_obj_get_child(camera_use,0),"添加中…");
}
static void photo_load_directory(const char *path,int page) {
    if(!photo_visible_listing)photo_visible_listing=codex_alloc(sizeof(*photo_visible_listing));
    if(!photo_visible_listing){ui_text(photo_note,"目录内存不足，请稍后重试");return;}
    if(!photo_list_directory(path,page)){ui_text(photo_note,"目录尚未读取完成，请稍后重试");return;}
    photo_step=4;snprintf(photo_directory,sizeof(photo_directory),"%s",path);
    visible(photo_view,false);visible(photo_confirm,false);visible(photo_files,true);
    visible(photo_up,true);visible(photo_prev,true);visible(photo_next,true);
    lv_obj_clean(photo_files);memset(photo_rows,0,sizeof(photo_rows));
    ui_text(photo_note,"正在读取存储卡…");
}
static void photo_choose_file(lv_event_t *e) {
    int index=(int)(intptr_t)lv_event_get_user_data(e);if(!photo_visible_listing||index<0||index>=photo_visible_listing->count||photo_state()==1)return;
    photo_entry *entry=&photo_visible_listing->entries[index];char path[768];
    snprintf(path,sizeof(path),"%s/%s",photo_directory,entry->name);
    if(entry->folder){photo_load_directory(path,0);return;}
    if(photo_prepare_file(path)) {
        photo_from_camera=false;photo_step=1;photo_id[0]=0;
        visible(photo_files,false);visible(photo_up,false);visible(photo_prev,false);visible(photo_next,false);
        ui_text(photo_title,"图片预览");ui_text(photo_note,"正在读取图片…");
    }else ui_text(photo_note,"无法打开图片，请重试");
}
static void photo_window_action(lv_event_t *e) {
    int action=(int)(intptr_t)lv_event_get_user_data(e);
    if(photo_step==3||photo_step==5||photo_state()==1)return;
    if(action==0){photo_finish();return;}
    if(action==1){
        if(photo_from_camera)photo_camera_begin(photo_state()==3||photo_camera_retry_prepare);
        else photo_upload();
        return;
    }
    if(action==2){char path[512];snprintf(path,sizeof(path),"%s",photo_directory);char *slash=strrchr(path,'/');if(slash&&slash>path)*slash=0;photo_load_directory(path,0);}
    if(action==3&&photo_visible_listing&&photo_visible_listing->page>0)photo_load_directory(photo_directory,photo_visible_listing->page-1);
    if(action==4&&photo_visible_listing&&photo_visible_listing->more)photo_load_directory(photo_directory,photo_visible_listing->page+1);
}
static void photo_camera_begin(bool encode) {
    if(photo_camera_processing||!photo_for_codex)return;
    photo_step=5;photo_from_camera=true;photo_camera_start_error=NULL;photo_camera_encode_pending=encode;
    if(encode)photo_id[0]=0;
    photo_display_probe_mark(PHOTO_DISPLAY_PROBE_CLICK);
    photo_camera_hold_result=camera_viewport_still_begin(camera_canvas);
    photo_camera_busy(true);
    // A retry stays on its error page; do not rotate back into the camera
    // while the new encoding task is already running.
    visible(photo_files,false);visible(photo_up,false);visible(photo_prev,false);visible(photo_next,false);
    if(encode)visible(photo_view,false);
    visible(photo_confirm,!encode);
    ui_text(photo_title,"添加照片");ui_text(photo_note,"正在处理照片…");
    // Still inside the click's LVGL turn: fence pending DMA and take scanout
    // before any of the changed controls can trigger an ordinary partial draw.
    photo_tick();
}
static void photo_camera_use(void){photo_camera_begin(true);}
static void photo_open(lv_event_t *e) {
    (void)e;codex_draft *d=codex_draft_for(codex_selected,true);
    if(photo_flow||photo_camera_processing)return;
    codex_feedback_pinned=true;codex_submit_accepted=false;
    if(!d){ui_text(codex_receipt,"请先选择会话");return;}
    if(codex_waiting||codex_voice_waiting||codex_send_queued||d->request[0]){photo_open_pending=false;ui_text(codex_receipt,"请先完成发送或录音，再打开相机");return;}
    if(codex_reading||codex_syncing||codex_load_waiting) {
        photo_open_pending=true;snprintf(photo_open_task,sizeof(photo_open_task),"%s",codex_selected);
        ui_text(codex_receipt,"正在准备相机，请稍候…");return;
    }
    photo_open_pending=false;
    if(d->images[2][0]){ui_text(codex_receipt,"最多添加 3 张照片");return;}
    if(!codex_save_draft())return;
    ui_text(codex_receipt,"正在打开摄像头…");
    codex_keyboard_done(NULL);
    snprintf(photo_task,sizeof(photo_task),"%s",codex_selected);photo_flow=photo_for_codex=true;photo_id[0]=0;
    // Match the application camera/retake entry; retain draft attachments.
    camera_release_caches();
    camera_preview_request_orientation(camera_current_pose());
    lv_obj_remove_flag(camera_layer,LV_OBJ_FLAG_HIDDEN);lv_obj_move_foreground(camera_layer);
    if(!camera_preview_start(camera_canvas)) {
        ui_text(camera_hint,"摄像头暂不可用，请稍后重试");
        ui_text(codex_receipt,"摄像头暂不可用，请稍后重试");
    }
}
static void photo_open_tick(void) {
    if(!photo_open_pending)return;
    // A queued click belongs to this task and page only. It must not open a
    // camera unexpectedly after navigation, sleep, a send, or another workflow.
    codex_draft *d=codex_draft_for(codex_selected,false);
    if(current_page!=8||saver||strcmp(photo_open_task,codex_selected)||!d||
       photo_flow||codex_waiting||codex_voice_waiting||codex_send_queued||d->request[0]||ota_update_busy()) {
        photo_open_pending=false;return;
    }
    if(!codex_reading&&!codex_syncing&&!codex_load_waiting)photo_open(NULL);
}
static void photo_remove(lv_event_t *e) {
    int index=(int)(intptr_t)lv_event_get_user_data(e);codex_draft *d=codex_draft_for(codex_selected,false);
    if(!d||d->request[0]||codex_waiting||codex_voice_waiting||photo_flow)return;
    free(d->thumbnails[index]);
    for(int i=index;i<2;i++){memcpy(d->images[i],d->images[i+1],37);d->thumbnails[i]=d->thumbnails[i+1];}
    d->images[2][0]=0;d->thumbnails[2]=NULL;d->dirty=d->loaded=true;d->revision++;
    codex_local_dirty=true;codex_checkpoint_write();codex_images_refresh();codex_sync_next=esp_timer_get_time()/1000+1200;
    codex_feedback_pinned=true;ui_text(codex_receipt,"已移除照片");
}
static void photo_attached_open(lv_event_t *e) {
    int index=(int)(intptr_t)lv_event_get_user_data(e);codex_draft *d=codex_draft_for(codex_selected,false);
    if(!d||!d->images[index][0]||codex_waiting||codex_voice_waiting||photo_flow)return;
    const uint16_t *pixels=NULL;unsigned width=0,height=0;
    for(int i=0;i<3;i++)if(!strcmp(photo_cache[i].id,d->images[index])){pixels=photo_cache[i].pixels;width=photo_cache[i].width;height=photo_cache[i].height;}
    if(!pixels){ui_text(codex_receipt,"照片附件已保留，本机预览已释放");return;}
    photo_flow=true;photo_step=2;photo_from_camera=false;
    photo_set_view(pixels,width,height);photo_view_orientation();
    visible(photo_files,false);visible(photo_up,false);visible(photo_prev,false);visible(photo_next,false);visible(photo_confirm,false);visible(photo_view,true);
    ui_text(photo_title,"照片附件");ui_text(photo_note,"");visible(photo_window,true);lv_obj_move_foreground(photo_window);
}
static void photo_tick(void) {
    if(!photo_flow)return;
    lv_display_trigger_activity(NULL);
    if(photo_camera_processing&&photo_step==2&&!photo_camera_return(true))return;
    if(photo_step==5) {
        if(!photo_camera_start_error) {
                int held=photo_camera_hold_result;
                if(held==1) {
                    if(!photo_camera_encode_pending) {
                        photo_step=2;photo_upload();
                        if(photo_step!=3)photo_camera_return(true);
                        return;
                    }
                    if(photo_prepare_camera()){photo_camera_retry_prepare=false;photo_step=1;return;}
                }
                photo_camera_start_error=held==1?"无法启动照片处理，请重试":"照片显示同步未恢复，请重试";
        }
        if(!photo_camera_return(true))return;
        photo_camera_retry_prepare=photo_camera_encode_pending;photo_step=2;
        ui_text(photo_note,photo_camera_start_error);
        ui_text(lv_obj_get_child(photo_confirm,0),"重试");visible(photo_confirm,true);
        lv_obj_remove_state(photo_confirm,LV_STATE_DISABLED);return;
    }
    if(photo_step==4) {
        const photo_listing *value=photo_get_listing();if(!value||value->state!=2||photo_rows[0])return;
        *photo_visible_listing=*value;
        for(int i=0;i<value->count;i++) {
            char title[280];snprintf(title,sizeof(title),"%s%s",value->entries[i].folder?"[目录] ":"",value->entries[i].name);
            photo_rows[i]=button(photo_files,title,0,i*66,1118,photo_choose_file,i);lv_obj_set_height(photo_rows[i],58);
        }
        if(!value->count)photo_rows[0]=muted(photo_files,"此目录没有 JPG 图片或文件夹",20,30,1000);
        ui_text_fmt(photo_note,value->error[0]?"%s":"%s · 第 %d 页",value->error[0]?value->error:photo_directory,value->page+1);
    }else if(photo_step==1) {
        if(photo_state()==1)return;
        if(photo_camera_processing) {
            if((photo_state()!=2&&photo_state()!=3)||gesture_active())return;
        }
        if(photo_state()==3){
            if(photo_camera_processing&&!photo_camera_return(true))return;
            ui_text(photo_note,photo_error());photo_step=2;
            if(photo_from_camera){
                ui_text(lv_obj_get_child(photo_confirm,0),"重试");
                visible(photo_confirm,true);lv_obj_remove_state(photo_confirm,LV_STATE_DISABLED);
            }
            return;
        }
        if(photo_state()!=2)return;
        unsigned width,height;photo_preview_dimensions(&width,&height);
        photo_set_view(photo_preview(),width,height);
        ui_text(lv_obj_get_child(photo_confirm,0),"添加附件");
        visible(photo_view,true);visible(photo_confirm,true);lv_obj_remove_state(photo_confirm,LV_STATE_DISABLED);
        photo_step=2;ui_text(photo_note,"确认后添加到当前消息");
        if(photo_from_camera)photo_upload();
        if(photo_camera_processing&&photo_step!=3)photo_camera_return(true);
    }else if(photo_step==3) {
        char status[96];int code=codex_remote_status(photo_task,status,sizeof(status));if(code==-1){if(status[0])ui_text(photo_note,status);return;}
        if(photo_camera_processing&&gesture_active())return;
        lv_obj_remove_state(photo_confirm,LV_STATE_DISABLED);lv_obj_remove_state(photo_close_button,LV_STATE_DISABLED);
        if(code!=200){if(photo_camera_processing&&!photo_camera_return(true))return;photo_step=2;ui_text(photo_note,status);return;}
        codex_draft *d=codex_draft_for(photo_task,true);int slot=0;while(d&&slot<3&&d->images[slot][0])slot++;
        if(!d||slot==3){if(photo_camera_processing&&!photo_camera_return(true))return;photo_step=2;ui_text(photo_note,"附件空间不足，照片已保留");return;}
        // Release the still fence before committing attachment ownership. The
        // refresh timer resumes after this LVGL turn, when the composer is ready.
        if(photo_camera_processing&&!camera_viewport_still_end())return;
        snprintf(d->images[slot],37,"%s",photo_id);d->thumbnails[slot]=codex_alloc(96*54*2);
        unsigned width,height;photo_preview_dimensions(&width,&height);
        const uint16_t *pixels=photo_preview();
        if(d->thumbnails[slot]&&pixels&&width&&height) {
            unsigned tw,th;photo_fit(width,height,96,54,&tw,&th);
            memset(d->thumbnails[slot],0,96*54*2);
            for(unsigned y=0;y<th;y++)for(unsigned x=0;x<tw;x++)
                d->thumbnails[slot][(y+(54-th)/2)*96+x+(96-tw)/2]=pixels[(y*height/th)*width+x*width/tw];
        }
        unsigned cache=photo_cache_next++%3;
        // Reuse a removed attachment's entry before evicting a photograph
        // that is still present in this three-photo draft.
        for(unsigned i=0;i<3;i++) {
            bool retained=false;for(unsigned j=0;j<3;j++)if(!strcmp(photo_cache[i].id,d->images[j]))retained=true;
            if(!retained||!photo_cache[i].pixels){cache=i;break;}
        }
        // The hidden viewer already references this completed preview. Move its
        // ownership instead of allocating and copying another 460,800 bytes
        // while the LCD is still scanning the captured photograph.
        uint16_t *retained=photo_take_preview();
        if(retained){free(photo_cache[cache].pixels);photo_cache[cache].pixels=retained;photo_cache[cache].width=width;photo_cache[cache].height=height;snprintf(photo_cache[cache].id,37,"%s",photo_id);}
        d->dirty=d->loaded=true;d->revision++;codex_local_dirty=true;codex_checkpoint_write();photo_finish();
        codex_feedback_pinned=true;codex_submit_accepted=false;
        ui_text(codex_receipt,status);
    }
}
static UI_INIT_SMALL void photo_ui_create(lv_obj_t *screen,lv_obj_t *composer) {
    lv_obj_t *trigger=button(composer,"",450,332,56,photo_open,0);lv_obj_set_size(trigger,56,56);lv_obj_clean(trigger);lv_obj_set_style_pad_all(trigger,0,0);
    codex_camera_button=trigger;
    lv_obj_t *body=shape(trigger,11,19,34,24,INK,5);ui_bg_opa(body,LV_OPA_TRANSP,0);lv_obj_set_style_border_width(body,2,0);ui_border_color(body,lv_color_hex(INK),0);
    shape(trigger,21,14,14,7,INK,2);lv_obj_t *lens=shape(trigger,23,24,12,12,INK,6);ui_bg_opa(lens,LV_OPA_TRANSP,0);lv_obj_set_style_border_width(lens,2,0);ui_border_color(lens,lv_color_hex(INK),0);
    for(int i=0;i<3;i++) {
        composer_photos[i]=button(composer,"",i*150,240,142,photo_attached_open,i);lv_obj_set_height(composer_photos[i],64);lv_obj_clean(composer_photos[i]);lv_obj_set_style_pad_all(composer_photos[i],0,0);
        label(composer_photos[i],"照片",8,20,80,false);composer_thumbs[i]=lv_canvas_create(composer_photos[i]);lv_obj_set_pos(composer_thumbs[i],5,5);
        composer_remove[i]=button(composer_photos[i],"×",102,0,40,photo_remove,i);lv_obj_set_height(composer_remove[i],64);ui_bg_opa(composer_remove[i],LV_OPA_TRANSP,0);lv_obj_center(lv_obj_get_child(composer_remove[i],0));visible(composer_photos[i],false);
    }
    photo_window=card(screen,0,0,1280,720);lv_obj_set_style_pad_all(photo_window,32,0);
    photo_title=label(photo_window,"图片",0,2,550,false);
    photo_close_button=button(photo_window,"返回",1072,-8,140,photo_window_action,0);
    photo_up=button(photo_window,"上一级",590,-8,140,photo_window_action,2);
    photo_prev=button(photo_window,"上一页",748,-8,140,photo_window_action,3);
    photo_next=button(photo_window,"下一页",906,-8,140,photo_window_action,4);
    photo_files=lv_obj_create(photo_window);lv_obj_set_pos(photo_files,0,76);lv_obj_set_size(photo_files,1216,510);ui_bg_opa(photo_files,LV_OPA_TRANSP,0);lv_obj_set_style_border_width(photo_files,0,0);
    photo_view=lv_canvas_create(photo_window);lv_obj_set_pos(photo_view,288,112);visible(photo_view,false);
    photo_note=muted(photo_window,"",0,610,940);lv_obj_set_height(photo_note,40);lv_label_set_long_mode(photo_note,LV_LABEL_LONG_DOT);
    photo_confirm=button(photo_window,"添加附件",1016,598,200,photo_window_action,1);
    visible(photo_window,false);visible(photo_confirm,false);
}
