// Included by ui.c: reuse its persisted drafts, voice owner and send receipts.
#include "desktop_remote.h"
#include "mechanical_keycaps.h"
#ifdef TAB5_UI_PREVIEW
static desktop_remote_state preview_desktop;
static char preview_desktop_target[37];
static char preview_desktop_text[2001];
static int preview_desktop_stages;
static int preview_desktop_submits;
static int preview_desktop_clears;
static int preview_desktop_requests;
static bool preview_desktop_available=true;
static char preview_desktop_draft_request[37];
bool desktop_remote_open(const char *request,const char *target) {
    preview_desktop_requests++;if(!preview_desktop_available)return false;
    (void)request;memset(&preview_desktop,0,sizeof(preview_desktop));preview_desktop.busy=true;
    snprintf(preview_desktop_target,sizeof(preview_desktop_target),"%s",target);return true;
}
bool desktop_remote_latest(const char *request){return desktop_remote_open(request,"");}
void desktop_remote_get(desktop_remote_state *out){*out=preview_desktop;}
bool desktop_remote_stage(const char *request,const char *target,const char *text) {
    preview_desktop_stages++;snprintf(preview_desktop_text,sizeof(preview_desktop_text),"%s",text);
    return desktop_remote_open(request,target);
}
bool desktop_remote_submit(const char *request,const char *target,const char *draft_request) {
    preview_desktop_submits++;snprintf(preview_desktop_draft_request,sizeof(preview_desktop_draft_request),"%s",draft_request);
    return desktop_remote_open(request,target);
}
bool desktop_remote_clear(const char *request,const char *target) {
    preview_desktop_clears++;return desktop_remote_open(request,target);
}
#endif
typedef struct {char id[37],project[17],title[241],folder[121];double updated;} quick_task;
static quick_task quick_tasks[5];
static int quick_count,quick_index,quick_return;
static int quick_settings_return,quick_detail_return,quick_primary_index;
static int quick_open_index=-1;
static lv_obj_t *quick_entry,*quick_layer,*quick_title,*quick_project,*quick_position,*quick_status,*quick_text;
static lv_obj_t *quick_previous,*quick_next,*quick_open_button,*quick_voice_button,*quick_close_button,*quick_cancel_button;
static lv_obj_t *quick_clear_button;
static lv_obj_t *quick_doubao_icon,*quick_stop_icon,*quick_send_icon,*quick_work_spinner;
static lv_obj_t *quick_record_icon,*quick_cancel_icon,*quick_text_box;
static lv_obj_t *quick_recording_icon,*quick_recording_dot,*quick_feedback_panel;
static lv_obj_t *quick_key_faces[2];
static bool quick_opening,quick_recording,quick_stop_insert,quick_filling,quick_sending,quick_ready,quick_failed,quick_resetting,quick_clearing;
static bool quick_staged_once;
static char quick_opened[37],quick_session[40],quick_stage_request[37],quick_saved_voice[2001];
static unsigned quick_generation;
static bool quick_busy(void){return codex_voice_start_pending||quick_opening||quick_recording||quick_filling||quick_sending||quick_clearing||codex_voice_waiting||codex_waiting||codex_send_queued;}
static bool quick_has_clearable_draft(void){return quick_opened[0]&&quick_staged_once&&!quick_recording;}
static void quick_recording_hint(bool recording) {
    visible(quick_recording_icon,recording);
    ui_text_color(quick_status,lv_color_hex(recording||quick_ready?0x54D69A:quick_failed?0xEFA98F:INK),0);
    int x=recording?96:60,right=quick_has_clearable_draft()?804:1012,width=right-x;
    if(lv_obj_get_x(quick_status)!=x)lv_obj_set_x(quick_status,x);
    if(lv_obj_get_width(quick_status)!=width)lv_obj_set_width(quick_status,width);
    if(recording)ui_bg_opa(quick_recording_dot,(esp_timer_get_time()/500000)%2?LV_OPA_COVER:LV_OPA_50,0);
}
static void quick_enable(lv_obj_t *object,bool enabled) {
    if(enabled)lv_obj_remove_state(object,LV_STATE_DISABLED);else lv_obj_add_state(object,LV_STATE_DISABLED);
}
static void quick_lock(lv_obj_t *object,bool locked) {
    // A temporary operation lock must not repaint unrelated navigation.
    if(locked)lv_obj_remove_flag(object,LV_OBJ_FLAG_CLICKABLE);
    else lv_obj_add_flag(object,LV_OBJ_FLAG_CLICKABLE);
}
static void quick_voice_visual(bool processing) {
    bool was_processing=!lv_obj_has_flag(quick_work_spinner,LV_OBJ_FLAG_HIDDEN);
    if(processing&&!was_processing)lv_spinner_set_anim_params(quick_work_spinner,900,240);
    else if(!processing&&was_processing)lv_anim_delete(quick_work_spinner,NULL);
    visible(quick_work_spinner,processing);
    visible(quick_doubao_icon,!processing&&!quick_recording&&!quick_ready&&!quick_sending);
    visible(quick_stop_icon,!processing&&quick_recording);
    visible(quick_send_icon,quick_ready||quick_sending);
}
static bool quick_bind(void) {
    if(!quick_count)return false;
    quick_task *task=&quick_tasks[quick_index];
    if(!strcmp(codex_selected,task->id))return true;
    snprintf(codex_project_selected,sizeof(codex_project_selected),"%s",task->project);
    codex_restore_project=codex_default_pending=false;
    // open-recent already authenticated this exact target. Do not wait for a
    // slower Wi-Fi/BLE catalog to create a hidden sidebar row for it first.
    codex_select_task(task->id);
    return !strcmp(codex_selected,task->id);
}
static void quick_feedback_refresh(void) {
    bool has_text=lv_label_get_text(quick_text)[0]!=0;
    bool active=has_text||lv_label_get_text(quick_status)[0]||quick_recording||quick_ready||quick_failed||quick_has_clearable_draft();
    visible(quick_feedback_panel,active);visible(quick_text_box,has_text);
    int height=has_text?172:64;
    if(lv_obj_get_height(quick_feedback_panel)!=height)lv_obj_set_height(quick_feedback_panel,height);
}
static void quick_refresh(void) {
    quick_feedback_refresh();
    if(!quick_recording)quick_recording_hint(false);
    bool busy=quick_busy();
    visible(quick_clear_button,quick_has_clearable_draft());
    quick_enable(quick_clear_button,!busy);
    quick_enable(quick_previous,quick_index>0);quick_enable(quick_next,quick_index+1<quick_count);
    quick_lock(quick_previous,busy);quick_lock(quick_next,busy);quick_lock(quick_close_button,busy);
    quick_enable(quick_open_button,!busy&&quick_count>0);
    if(!quick_recording)quick_enable(quick_voice_button,!busy&&quick_count>0);
    ui_text(lv_obj_get_child(quick_voice_button,0),quick_ready||quick_sending?"发送":"豆包");
    const lv_image_dsc_t *voice_material=quick_ready||quick_sending?&quick_blue_cap_image:&quick_red_cap_image;
    lv_obj_t *voice_surface=lv_obj_get_child(quick_key_faces[1],0);
    if(lv_image_get_src(voice_surface)!=voice_material)lv_image_set_src(voice_surface,voice_material);
    // Navigation failure has no confirmed voice target. Retry with the Codex
    // key instead of offering a recording reset that cannot repair navigation.
    visible(quick_cancel_button,codex_voice_start_pending||quick_recording||quick_ready||(quick_failed&&quick_opened[0]));
    ui_text(lv_obj_get_child(quick_cancel_button,0),codex_voice_start_pending?"取消准备":quick_recording?"取消录音":quick_staged_once?"继续语音":"重试录音");
    visible(quick_record_icon,!quick_recording);
    visible(quick_cancel_icon,quick_recording);
    lv_obj_align(lv_obj_get_child(quick_cancel_button,0),LV_ALIGN_LEFT_MID,60,0);
    quick_enable(quick_cancel_button,!quick_opening&&!quick_filling&&!quick_sending&&!quick_clearing&&!quick_resetting);
    quick_voice_visual(quick_stop_insert||quick_resetting||quick_filling);
    ui_text_fmt(quick_position,"%d / %d",quick_count?quick_index+1:0,quick_count);
    ui_text(quick_project,quick_count?quick_tasks[quick_index].folder:"最近会话");
    ui_text(quick_title,quick_count?quick_tasks[quick_index].title:"暂无会话，请先连接电脑 Codex");
}
static void quick_cap_depth(void *object,int32_t depth) {
    lv_obj_t *face=object;lv_obj_set_style_translate_y(face,depth,0);
    lv_obj_set_style_transform_scale(face,256-depth/2,0);
    lv_obj_set_style_shadow_ofs_y(face,10-depth,0);
}
static void quick_key_motion(lv_event_t *event) {
    lv_obj_t *face=lv_event_get_user_data(event);
    bool down=lv_event_get_code(event)==LV_EVENT_PRESSED;
    lv_anim_delete(face,quick_cap_depth);
    lv_anim_t motion;lv_anim_init(&motion);lv_anim_set_var(&motion,face);
    lv_anim_set_exec_cb(&motion,quick_cap_depth);
    lv_anim_set_values(&motion,lv_obj_get_style_translate_y(face,0),down?8:0);
    lv_anim_set_duration(&motion,down?65:140);
    lv_anim_set_path_cb(&motion,down?lv_anim_path_ease_out:lv_anim_path_overshoot);lv_anim_start(&motion);
}
static lv_obj_t *quick_keycap(lv_obj_t *key,const lv_image_dsc_t *material) {
    lv_obj_set_height(key,276);lv_obj_set_style_pad_all(key,0,0);
    lv_obj_set_style_border_width(key,0,0);lv_obj_set_style_shadow_width(key,0,0);
    ui_bg_opa(key,LV_OPA_TRANSP,0);ui_bg_opa(key,LV_OPA_TRANSP,LV_STATE_PRESSED);
    // The shared button helper paints disabled controls. Mechanical keys lock
    // during RPC/recognition, but their transparent hit area must never flash.
    ui_bg_opa(key,LV_OPA_TRANSP,LV_STATE_DISABLED);
    ui_bg_opa(key,LV_OPA_TRANSP,LV_STATE_DISABLED|LV_STATE_PRESSED);
    lv_obj_t *rim=lv_image_create(key);lv_image_set_src(rim,&quick_collar_image);
    lv_obj_set_pos(rim,114,0);lv_obj_remove_flag(rim,LV_OBJ_FLAG_CLICKABLE);
    lv_obj_t *face=lv_obj_create(key);lv_obj_remove_style_all(face);
    lv_obj_set_pos(face,140,14);lv_obj_set_size(face,200,200);
    lv_obj_add_flag(face,LV_OBJ_FLAG_OVERFLOW_VISIBLE);
    lv_obj_remove_flag(face,LV_OBJ_FLAG_CLICKABLE|LV_OBJ_FLAG_SCROLLABLE);
    lv_obj_t *surface=lv_image_create(face);lv_image_set_src(surface,material);
    lv_obj_set_pos(surface,0,0);lv_obj_remove_flag(surface,LV_OBJ_FLAG_CLICKABLE);
    lv_obj_add_event_cb(key,quick_key_motion,LV_EVENT_PRESSED,face);
    lv_obj_add_event_cb(key,quick_key_motion,LV_EVENT_RELEASED,face);
    lv_obj_add_event_cb(key,quick_key_motion,LV_EVENT_PRESS_LOST,face);
    // Logos and the send arrow identify the keys; the space below belongs to
    // the multi-line transcript instead of repeating the product names.
    visible(lv_obj_get_child(key,0),false);return face;
}
static void quick_forget(void) {
    quick_ready=quick_failed=quick_resetting=false;quick_stage_request[0]=quick_saved_voice[0]=0;
}
static void quick_reset_idle(void) {
    // Desktop takes only own their preview; neither local nor PC drafts belong
    // to this reset. The next take appends to the current desktop draft.
    quick_forget();ui_text(quick_text,"");
    ui_text(quick_status,quick_staged_once?"点豆包继续语音，文字将追加到电脑草稿":"点豆包开始录音");quick_refresh();
}
static void quick_open_task(int index) {
    if(quick_busy()||index<0||index>=quick_count)return;
    char request[37];codex_uuid(request);
    if(!desktop_remote_open(request,quick_tasks[index].id)){ui_text(quick_status,"连接暂不可用，请稍后重试");return;}
    // Keep the current selection until the desktop confirms this exact target.
    // Preserve staged controls unless the host confirms the composer is empty.
    quick_open_index=index;quick_opening=true;
    ui_text(quick_status,index==quick_index?"正在启动或打开 Codex…":"正在切换 Codex 会话…");quick_refresh();
}
static void quick_switch(lv_event_t *e) {
    quick_open_task(quick_index+(int)(intptr_t)lv_event_get_user_data(e));
}
static void quick_open(lv_event_t *e) {
    (void)e;quick_open_task(quick_index);
}
static void quick_voice_started(void) {
    voice_remote_state *voice=&ui_voice_work->quick_start;voice_remote_get(voice);quick_generation=voice->generation;
    snprintf(quick_session,sizeof(quick_session),"%s",text(item(shown,"pcInput"),"session"));
    quick_forget();quick_recording=true;quick_stop_insert=false;ui_text(quick_text,"");quick_refresh();
}
static void quick_voice_pending_result(const char *message) {if(quick_visible){ui_text(quick_status,message);quick_refresh();}}
static void quick_voice(lv_event_t *e) {
    if(quick_recording) {
        voice_remote_state *voice=&ui_voice_work->quick_stop;voice_remote_get(voice);
        if(voice->busy&&!quick_stop_insert&&!quick_resetting&&
            (!strcmp(voice->phase,"recording")||!strcmp(voice->phase,"preparing"))) {
            quick_stop_insert=true;voice_remote_command(1);quick_enable(quick_voice_button,false);
            quick_recording_hint(false);quick_voice_visual(true);
            ui_text(quick_status,"正在结束收音，识别完成后填入输入框…");
        }
        return;
    }
    if(quick_busy()||!quick_count)return;
    if(quick_ready) {
        char request[37];codex_uuid(request);
        if(!desktop_remote_submit(request,quick_opened,quick_stage_request)) {
            ui_text(quick_status,"连接暂不可用，未触发发送；可重新录音");return;
        }
        quick_sending=true;quick_ready=false;
        ui_text(quick_status,"正在向语音回填的会话发送回车…");quick_refresh();return;
    }
    if(strcmp(quick_opened,quick_tasks[quick_index].id)){ui_text(quick_status,"请先点击 Codex，确认目标会话");return;}
    if(!quick_bind()){quick_failed=true;ui_text(quick_status,"本机草稿未保存，请先返回任务页处理");quick_refresh();return;}
    codex_voice_action(e);
    if(codex_voice_start_pending){ui_text(quick_status,codex_voice_start_note);quick_refresh();return;}
    if(!codex_voice_waiting){quick_failed=true;ui_text(quick_status,lv_label_get_text(codex_voice_hint));quick_refresh();return;}
    quick_voice_started();
}
static void quick_cancel(lv_event_t *e) {
    (void)e;
    if(codex_voice_start_pending){codex_voice_request(2);quick_refresh();return;}
    if(!quick_recording){if(!quick_busy())quick_reset_idle();return;}
    if(quick_resetting)return;
    voice_remote_state *voice=&ui_voice_work->quick_cancel;voice_remote_get(voice);
    snprintf(quick_saved_voice,sizeof(quick_saved_voice),"%s",voice->text);
    quick_stop_insert=false;quick_resetting=true;voice_remote_command(2);
    quick_voice_visual(true);
    quick_enable(quick_cancel_button,false);
    quick_enable(quick_voice_button,false);ui_text(quick_status,"正在取消语音…");
}
static void quick_clear(lv_event_t *e) {
    if(lv_event_get_code(e)!=LV_EVENT_LONG_PRESSED||quick_busy()||!quick_has_clearable_draft())return;
    char request[37];codex_uuid(request);
    if(!desktop_remote_clear(request,quick_opened)){ui_text(quick_status,"连接暂不可用，未触发清空");return;}
    quick_clearing=true;ui_text(quick_status,"正在清空输入框…");quick_refresh();
}
static void quick_close(lv_event_t *e) {
    (void)e;if(quick_busy())return;quick_visible=false;visible(quick_layer,false);
    primary_index=quick_primary_index;
    page(quick_return);
    settings_return=quick_settings_return;detail_return=quick_detail_return;
}
static void quick_show(lv_event_t *e) {
    (void)e;if(codex_voice_waiting||codex_waiting||codex_send_queued||photo_flow||camera_preview_busy()||ota_update_busy())return;
    if(!quick_visible) {
        quick_return=current_page;quick_primary_index=primary_index;
        quick_settings_return=settings_return;quick_detail_return=detail_return;
    }
    quick_count=quick_index=0;quick_open_index=-1;quick_opened[0]=0;quick_forget();quick_staged_once=false;memset(quick_tasks,0,sizeof(quick_tasks));
    cJSON *task=NULL;
    cJSON_ArrayForEach(task,item(item(shown,"codexTasks"),"tasks")) {
        if(strlen(text(task,"id"))!=36)continue;
        int at=quick_count;double updated=num(task,"updatedAt");
        for(int i=0;i<quick_count;i++)if(updated>quick_tasks[i].updated){at=i;break;}
        if(at>=5)continue;
        if(quick_count<5)quick_count++;
        for(int i=quick_count-1;i>at;i--)quick_tasks[i]=quick_tasks[i-1];
        quick_task *entry=&quick_tasks[at];entry->updated=updated;
        snprintf(entry->id,sizeof(entry->id),"%s",text(task,"id"));snprintf(entry->project,sizeof(entry->project),"%s",text(task,"projectId"));
        snprintf(entry->title,sizeof(entry->title),"%s",text(task,"title"));snprintf(entry->folder,sizeof(entry->folder),"%s",text(task,"folder"));
    }
    page(8);quick_visible=true;visible(quick_layer,true);lv_obj_move_foreground(quick_layer);tab5_draw_cache_drop();
    ui_text(quick_status,"");ui_text(quick_text,"");quick_refresh();
}
static void quick_tick(void) {
    if(!quick_visible)return;
    if(codex_voice_start_pending){ui_text(quick_status,codex_voice_start_note);quick_refresh();return;}
    if(quick_opening) {
        desktop_remote_state result;desktop_remote_get(&result);
        if(!result.busy) {
            quick_opening=false;
            bool confirmed=result.status==200&&quick_open_index>=0&&quick_open_index<quick_count&&
                !strcmp(result.task_id,quick_tasks[quick_open_index].id);
            if(confirmed) {
                if(strcmp(quick_opened,result.task_id)||result.draft_empty) {
                    quick_forget();quick_staged_once=false;ui_text(quick_text,"");
                }
                // Update metadata in place. Reordering on each successful open
                // would change what Previous/Next means during this visit.
                quick_index=quick_open_index;
                quick_task *selected=&quick_tasks[quick_index];
                snprintf(selected->project,sizeof(selected->project),"%s",result.project_id);
                snprintf(selected->title,sizeof(selected->title),"%s",result.title);
                snprintf(selected->folder,sizeof(selected->folder),"%s",result.folder);selected->updated=result.updated;
                snprintf(quick_opened,sizeof(quick_opened),"%s",result.task_id);
                quick_bind();
            }else {
                // An uncertain switch may already have moved the desktop.
                // Require explicit navigation confirmation before voice/input.
                quick_opened[0]=0;quick_forget();quick_staged_once=false;
            }
            quick_open_index=-1;quick_failed=!confirmed;
            ui_text(quick_status,confirmed&&result.draft_empty?"输入框为空，点豆包开始录音":
                !confirmed&&result.status==200?"未确认电脑会话同步，请重试 Codex":result.message);quick_refresh();
        }
    }
    if(quick_recording) {
        voice_remote_state *voice=&ui_voice_work->quick_render;voice_remote_get(voice);
        if(voice->generation!=quick_generation||strcmp(voice->task_id,quick_tasks[quick_index].id)) {
            quick_recording=quick_stop_insert=false;quick_failed=true;quick_resetting=false;
            ui_text(quick_status,"语音会话已变化，未发送；可重新录音");quick_refresh();return;
        }
        ui_text(quick_text,voice->text);
        bool recording=voice->busy&&voice->ready&&!strcmp(voice->phase,"recording")&&!quick_stop_insert&&!quick_resetting;
        // Local stop/cancel intent wins while older host replies are in flight.
        if(voice->busy&&quick_resetting)ui_text(quick_status,"正在取消语音…");
        else if(voice->busy&&quick_stop_insert&&(!strcmp(voice->phase,"recording")||!strcmp(voice->phase,"draining")))
            ui_text(quick_status,"正在结束收音，识别完成后填入输入框…");
        else if(recording&&voice->source_name[0])ui_text_fmt(quick_status,"%s · %s",voice->message,voice->source_name);
        else ui_text(quick_status,voice->message);
        quick_recording_hint(recording);
        quick_feedback_refresh();
        quick_voice_visual(quick_stop_insert||quick_resetting||!strcmp(voice->phase,"recognizing")||!strcmp(voice->phase,"draining"));
        ui_text(lv_obj_get_child(quick_voice_button,0),"豆包");
        quick_enable(quick_voice_button,voice->busy&&!quick_stop_insert&&!quick_resetting&&
            (!strcmp(voice->phase,"recording")||!strcmp(voice->phase,"preparing")));
        if(!voice->busy) {
            bool insert=quick_stop_insert&&voice->can_insert&&!strcmp(voice->phase,"review")&&voice->text[0]&&
                !strcmp(codex_selected,voice->task_id)&&!strcmp(quick_session,text(item(shown,"pcInput"),"session"));
            quick_recording=quick_stop_insert=false;
            if(quick_resetting){quick_reset_idle();return;}
            snprintf(quick_saved_voice,sizeof(quick_saved_voice),"%s",voice->text);
            if(insert) {
                char request[37];codex_uuid(request);
                quick_filling=desktop_remote_stage(request,voice->task_id,voice->text);
                snprintf(quick_stage_request,sizeof(quick_stage_request),"%s",quick_filling?request:"");
                quick_failed=!quick_filling;
                ui_text(quick_status,quick_filling?"正在填入 Codex 输入框…":"连接暂不可用，识别文字已保留");
            }
            else {quick_failed=true;if(voice->text[0])ui_text(quick_status,"识别文字已保留在预览，可重新录音");}
            quick_refresh();
        }
    }
    if(quick_filling) {
        desktop_remote_state result;desktop_remote_get(&result);
        if(!result.busy) {
            quick_filling=false;
            const char *message=result.message;
            if(result.status==200&&!strcmp(result.task_id,quick_tasks[quick_index].id)) {
                ui_text(quick_text,"");
                quick_ready=true;quick_failed=false;
                if(quick_staged_once&&result.appended)message="已追加，可继续语音补充；检查后点蓝色箭头发送";
                quick_staged_once=true;
            }
            else {quick_failed=true;quick_stage_request[0]=0;}
            ui_text(quick_status,message);quick_refresh();
        }
    }
    if(quick_sending) {
        desktop_remote_state result;desktop_remote_get(&result);
        if(!result.busy) {
            quick_sending=false;quick_ready=false;quick_stage_request[0]=0;
            quick_failed=result.status!=200||strcmp(result.task_id,quick_opened);
            if(!quick_failed)quick_staged_once=false;
            ui_text(quick_status,result.message);quick_refresh();
        }
    }
    if(quick_clearing) {
        desktop_remote_state result;desktop_remote_get(&result);
        if(!result.busy) {
            quick_clearing=false;quick_ready=false;quick_stage_request[0]=0;
            quick_failed=result.status!=200||strcmp(result.task_id,quick_opened);
            if(!quick_failed){quick_forget();quick_staged_once=false;ui_text(quick_text,"");}
            ui_text(quick_status,result.message);quick_refresh();
        }
    }
}
static void quick_broom(lv_obj_t *parent) {
    lv_obj_t *icon=lv_image_create(parent);lv_image_set_src(icon,&quick_broom_image);
    lv_obj_set_pos(icon,28,12);lv_obj_remove_flag(icon,LV_OBJ_FLAG_CLICKABLE);
}
static lv_obj_t *quick_microphone(lv_obj_t *parent,int x,int y) {
    lv_obj_t *icon=lv_obj_create(parent);lv_obj_remove_style_all(icon);
    lv_obj_set_pos(icon,x,y);lv_obj_set_size(icon,24,24);
    lv_obj_remove_flag(icon,LV_OBJ_FLAG_CLICKABLE|LV_OBJ_FLAG_SCROLLABLE);
    const uint32_t green=0x54D69A;
    shape(icon,9,1,6,13,green,3);
    lv_obj_t *arc=lv_arc_create(icon);lv_obj_remove_style_all(arc);
    lv_obj_set_pos(arc,3,2);lv_obj_set_size(arc,18,18);
    lv_arc_set_bg_angles(arc,0,180);lv_arc_set_angles(arc,0,0);
    lv_obj_set_style_arc_color(arc,lv_color_hex(green),LV_PART_MAIN);
    lv_obj_set_style_arc_width(arc,2,LV_PART_MAIN);lv_obj_set_style_arc_opa(arc,LV_OPA_COVER,LV_PART_MAIN);
    lv_obj_remove_flag(arc,LV_OBJ_FLAG_CLICKABLE);
    shape(icon,11,18,2,5,green,1);shape(icon,7,22,10,2,green,1);
    return icon;
}
static UI_INIT_SMALL void quick_create(lv_obj_t *screen) {
    quick_entry=workbench_icon_button(screen,638,quick_show,0);
    ui_bg_opa(quick_entry,LV_OPA_TRANSP,0);lv_obj_set_style_border_width(quick_entry,0,0);
    ui_bg_color(quick_entry,lv_color_hex(0x304A55),LV_STATE_PRESSED);
    ui_bg_opa(quick_entry,LV_OPA_COVER,LV_STATE_PRESSED);
    lv_obj_delete(lv_obj_get_child(quick_entry,0));
    lv_obj_t *shortcut_icon=lv_image_create(quick_entry);lv_image_set_src(shortcut_icon,&brand_codex_image);
    lv_obj_remove_flag(shortcut_icon,LV_OBJ_FLAG_CLICKABLE);lv_obj_center(shortcut_icon);
    quick_layer=lv_obj_create(screen);lv_obj_remove_style_all(quick_layer);lv_obj_set_size(quick_layer,1280,720);
    ui_bg_color(quick_layer,lv_color_hex(0x171C23),0);ui_bg_opa(quick_layer,LV_OPA_COVER,0);
    lv_obj_remove_flag(quick_layer,LV_OBJ_FLAG_SCROLLABLE);lv_obj_add_flag(quick_layer,LV_OBJ_FLAG_CLICKABLE);
    ui_text_font(quick_layer,&tab5_font_24,0);ui_text_color(quick_layer,lv_color_hex(INK),0);
    quick_close_button=workbench_back_button(quick_layer,quick_close);
    lv_obj_t *quick_heading=label(quick_layer,"Codex 直达",94,21,260,false);
    ui_text_font(quick_heading,&tab5_font_30,0);
    lv_obj_set_style_text_align(quick_heading,LV_TEXT_ALIGN_LEFT,0);
    shape(quick_layer,40,86,1200,78,0x232B34,18);
    quick_project=muted(quick_layer,"",64,110,172);lv_obj_set_height(quick_project,32);lv_label_set_long_mode(quick_project,LV_LABEL_LONG_DOT);
    shape(quick_layer,250,106,1,38,0x3C4855,0);
    quick_title=label(quick_layer,"",274,108,738,false);ui_text_font(quick_title,&tab5_font_24,0);
    lv_obj_set_height(quick_title,36);lv_label_set_long_mode(quick_title,LV_LABEL_LONG_DOT);
    quick_previous=button(quick_layer,LV_SYMBOL_LEFT,1036,100,52,quick_switch,-1);
    quick_next=button(quick_layer,LV_SYMBOL_RIGHT,1176,100,52,quick_switch,1);
    ui_text_font(lv_obj_get_child(quick_previous,0),&lv_font_montserrat_24,0);
    ui_text_font(lv_obj_get_child(quick_next,0),&lv_font_montserrat_24,0);
    quick_position=muted(quick_layer,"",1096,110,72);lv_obj_set_style_text_align(quick_position,LV_TEXT_ALIGN_CENTER,0);
    quick_open_button=button(quick_layer,"Codex",120,246,480,quick_open,0);
    quick_voice_button=button(quick_layer,"豆包",680,246,480,quick_voice,0);
    quick_key_faces[0]=quick_keycap(quick_open_button,&quick_graphite_cap_image);
    quick_key_faces[1]=quick_keycap(quick_voice_button,&quick_red_cap_image);
    lv_obj_t *codex_icon=lv_image_create(quick_key_faces[0]);lv_image_set_src(codex_icon,&brand_codex_key_image);
    lv_obj_align(codex_icon,LV_ALIGN_CENTER,0,-1);
    lv_obj_remove_flag(codex_icon,LV_OBJ_FLAG_CLICKABLE);
    // Official Doubao IME avatar, preserving its circle and original alpha.
    quick_doubao_icon=lv_image_create(quick_key_faces[1]);lv_image_set_src(quick_doubao_icon,&brand_doubao_image);
    lv_obj_align(quick_doubao_icon,LV_ALIGN_CENTER,0,-1);lv_obj_remove_flag(quick_doubao_icon,LV_OBJ_FLAG_CLICKABLE);
    quick_stop_icon=shape(quick_key_faces[1],0,0,32,32,INK,6);lv_obj_align(quick_stop_icon,LV_ALIGN_CENTER,0,-1);
    quick_work_spinner=lv_spinner_create(quick_key_faces[1]);
    lv_obj_set_size(quick_work_spinner,56,56);lv_obj_align(quick_work_spinner,LV_ALIGN_CENTER,0,-1);
    lv_anim_delete(quick_work_spinner,NULL); // Hidden controls must not keep the UI animating.
    lv_obj_set_style_arc_width(quick_work_spinner,5,LV_PART_MAIN);
    lv_obj_set_style_arc_width(quick_work_spinner,5,LV_PART_INDICATOR);
    lv_obj_set_style_arc_color(quick_work_spinner,lv_color_hex(INK),LV_PART_INDICATOR);
    lv_obj_set_style_arc_opa(quick_work_spinner,LV_OPA_20,LV_PART_MAIN);
    lv_obj_remove_flag(quick_work_spinner,LV_OBJ_FLAG_CLICKABLE);visible(quick_work_spinner,false);
    quick_send_icon=lv_obj_create(quick_key_faces[1]);lv_obj_remove_style_all(quick_send_icon);
    lv_obj_set_size(quick_send_icon,80,80);lv_obj_remove_flag(quick_send_icon,LV_OBJ_FLAG_SCROLLABLE);
    // The centered 10px white arrow is baked into the blue material, sharing
    // its exact press transform and eliminating font baselines/clipped lines.
    lv_obj_align(quick_send_icon,LV_ALIGN_CENTER,0,-1);lv_obj_remove_flag(quick_send_icon,LV_OBJ_FLAG_CLICKABLE);
    visible(quick_stop_icon,false);visible(quick_send_icon,false);
    quick_feedback_panel=shape(quick_layer,40,536,1200,172,0x202832,18);
    quick_status=label(quick_layer,"",60,551,952,false);lv_obj_set_height(quick_status,36);lv_label_set_long_mode(quick_status,LV_LABEL_LONG_SCROLL_CIRCULAR);
    quick_recording_icon=quick_microphone(quick_layer,60,554);
    quick_recording_dot=shape(quick_recording_icon,20,0,4,4,0x54D69A,LV_RADIUS_CIRCLE);
    visible(quick_recording_icon,false);
    quick_text_box=lv_obj_create(quick_layer);lv_obj_remove_style_all(quick_text_box);
    lv_obj_set_pos(quick_text_box,60,596);lv_obj_set_size(quick_text_box,1160,96);
    lv_obj_set_scroll_dir(quick_text_box,LV_DIR_VER);lv_obj_set_scrollbar_mode(quick_text_box,LV_SCROLLBAR_MODE_AUTO);
    quick_text=label(quick_text_box,"",0,0,1160,false);lv_label_set_long_mode(quick_text,LV_LABEL_LONG_WRAP);
    quick_clear_button=button(quick_layer,"长按清空",828,542,184,quick_clear,0);
    ui_bg_opa(quick_clear_button,LV_OPA_TRANSP,0);
    ui_bg_opa(quick_clear_button,LV_OPA_TRANSP,LV_STATE_DISABLED);
    lv_obj_set_style_border_width(quick_clear_button,1,0);
    lv_obj_set_style_border_color(quick_clear_button,lv_color_hex(0x43525C),0);
    ui_text_color(quick_clear_button,lv_color_hex(0xA9BCC2),0);
    ui_bg_color(quick_clear_button,lv_color_hex(0x2D3A44),LV_STATE_PRESSED);
    ui_bg_opa(quick_clear_button,LV_OPA_COVER,LV_STATE_PRESSED);
    ui_text_color(quick_clear_button,lv_color_hex(INK),LV_STATE_PRESSED);
    lv_obj_add_event_cb(quick_clear_button,quick_clear,LV_EVENT_LONG_PRESSED,0);
    lv_obj_align(lv_obj_get_child(quick_clear_button,0),LV_ALIGN_LEFT_MID,60,0);quick_broom(quick_clear_button);
    quick_cancel_button=button(quick_layer,"取消录音",1036,542,184,quick_cancel,0);
    quick_record_icon=quick_microphone(quick_cancel_button,28,12);
    quick_cancel_icon=shape(quick_cancel_button,28,12,24,24,INK,LV_RADIUS_CIRCLE);
    ui_bg_opa(quick_cancel_icon,LV_OPA_TRANSP,0);
    lv_obj_set_style_border_width(quick_cancel_icon,2,0);
    lv_obj_set_style_border_color(quick_cancel_icon,lv_color_hex(INK),0);
    lv_obj_t *cancel_cross=label(quick_cancel_icon,LV_SYMBOL_CLOSE,0,0,LV_SIZE_CONTENT,false);
    ui_text_font(cancel_cross,&lv_font_montserrat_14,0);lv_obj_center(cancel_cross);
    visible(quick_clear_button,false);visible(quick_cancel_button,false);visible(quick_layer,false);
}
