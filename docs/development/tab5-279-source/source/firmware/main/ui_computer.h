// Shared PC controller; playback belongs to the music page.
#include "computer_remote.h"
#include "ui_unlock.h"
#include "wifi_submission_test.h"
#ifdef TAB5_UI_PREVIEW
static computer_remote_state preview_computer;
bool computer_remote_oneclick_enabled(void){return preview_computer.one_click_unlock;}
bool computer_remote_lock_state(bool *locked,int64_t *updated){int64_t now=esp_timer_get_time()/1000;bool valid=preview_computer.lock_known&&now>=preview_computer.lock_updated&&now-preview_computer.lock_updated<6500;*locked=valid&&preview_computer.locked;*updated=preview_computer.lock_updated;return valid;}
bool computer_remote_screen_state(int *display){*display=preview_computer.ready&&esp_timer_get_time()/1000-preview_computer.updated<4000?preview_display:-1;return *display>=0;}
static unsigned preview_computer_commands,preview_status_reads;
static char preview_computer_op[16],preview_computer_target[37];
void computer_remote_get(computer_remote_state *out){*out=preview_computer;}
void computer_remote_diagnostic(unsigned *operation,int *status,int64_t *started){*operation=preview_computer.diagnostic_operation;*status=preview_computer.diagnostic_status;*started=preview_computer.diagnostic_started;}
bool computer_remote_start(const char *id,const char *op,int value,const char *target) {
    (void)id;if(preview_computer.busy)return false;
    strcpy(preview_computer_op,op);snprintf(preview_computer_target,sizeof(preview_computer_target),"%s",target?target:"");
    if(!strcmp(op,"status"))preview_status_reads++;
    if(!strcmp(op,"lock-screen")){strcpy(preview_cover_request,id);preview_cover_reads++;}
    else if(strcmp(op,"status"))preview_computer_commands++;
    if(!strcmp(op,"volume"))preview_computer.volume=value;
    if(!strcmp(op,"mute"))preview_computer.muted=value!=0;
    if(!strcmp(op,"status")||(!strcmp(op,"lock-screen")&&preview_cover_complete))preview_computer.lock_updated=esp_timer_get_time()/1000;
    preview_computer.updated=esp_timer_get_time()/1000;preview_computer.generation++;
    return true;
}
#endif
static lv_obj_t *computer_status,*computer_slider,*computer_volume,*computer_volume_icon,*computer_volume_slash,*computer_mute,*computer_lock,*computer_launch[4],*computer_launch_hint;
static lv_obj_t *music_controls,*music_control_buttons[3],*music_control_hint;
static lv_obj_t *computer_host;
static computer_remote_state *computer_view;
static lv_obj_t *computer_launch_icon[4],*computer_launch_symbol[4];
static lv_image_dsc_t computer_launch_images[4];
static uint8_t (*computer_launch_pixels)[32*32*4];
static int64_t computer_poll_at,computer_message_until;
static bool computer_adjusting;
static char computer_action_notice[160];
typedef struct {
    bool active,unlock;link_kind link;int mode;unsigned host,connection,generation;
    int64_t until,lock_updated;char session[40];
    int media;bool playing;uint64_t music;
} computer_click_intent;
static computer_click_intent computer_pending_click;
// Bind a delayed explicit tap to the same selected peer, session and connection.
static bool computer_click_binding(computer_click_intent *out) {
    out->link=rpc_link_select();if(out->link==LINK_NONE)return false;
    out->host=device_host_epoch();
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    out->mode=device_links.mode;snprintf(out->session,sizeof(out->session),"%s",device_links.session[out->link]);
    xSemaphoreGive(device_mutex);
    if(out->link==LINK_WIFI)out->connection=unlock_remote_connection();
    else {ble_keyboard_state keyboard;if(out->link==LINK_USB)usb_keyboard_get(&keyboard);else ble_keyboard_get(&keyboard);out->connection=keyboard.connection;}
    return out->session[0]&&out->host==device_host_epoch();
}
static void computer_notice(const char *text) {
    snprintf(computer_action_notice,sizeof(computer_action_notice),"%s",text);
    computer_message_until=esp_timer_get_time()/1000+3500;
    if(computer_status)ui_text(computer_status,text);
}

static int8_t computer_launch_layout[4]={-1,-1,-1,-1};
static int computer_music_layout_width=-1,computer_music_layout_x=-1;
static bool computer_supported(void){return num(item(shown,"connectionHealth"),"computerControlVersion")==1;}
static bool computer_connected(void){return rpc_link_ready()&&esp_timer_get_time()/1000-last_received<8000;}
static bool computer_ready(void){return computer_view&&computer_supported()&&computer_connected()&&computer_view->ready&&esp_timer_get_time()/1000-computer_view->updated<6500;}
static bool computer_usable(void){return computer_ready()&&!computer_view->busy;}
static const char *computer_media_target(int action) {
    return action==20?"previous":action==21?"play-pause":action==22?"next":NULL;
}
static bool computer_media_allowed(int action) {
    return computer_view->media_available&&(action==20?computer_view->can_previous:
        action==21?computer_view->can_play_pause:action==22&&computer_view->can_next);
}
// A local UI fingerprint cancels a waiting tap when displayed track metadata changes.
// This is not an authentication or protocol identity.
static uint64_t computer_music_key(void) {
    uint64_t key=UINT64_C(14695981039346656037);cJSON *music=item(shown,"music");
    const char *fields[]={"title","artist","album"};
    for(unsigned i=0;i<3;i++) {
        const unsigned char *p=(const unsigned char *)text(music,fields[i]);
        do{key^=*p;key*=UINT64_C(1099511628211);}while(*p++);
    }
    return key;
}
static void computer_enable(lv_obj_t *object,bool enabled){if(enabled)lv_obj_remove_state(object,LV_STATE_DISABLED);else lv_obj_add_state(object,LV_STATE_DISABLED);}
static void computer_action(const char *op,int value,const char *target) {
    if(!computer_usable())return;
    char id[37];codex_uuid(id);
    if(computer_remote_start(id,op,value,target)){
        int64_t now=esp_timer_get_time()/1000;bool locking=!strcmp(op,"lock");
        computer_action_notice[0]=0;computer_poll_at=locking?now-2001:now;computer_message_until=locking?0:now+3500;
    }
    else {ui_text(computer_status,"操作未开始，请稍后重试");computer_message_until=esp_timer_get_time()/1000+3500;}
}
static void computer_lock_now(void) {
    if(computer_view->lock_known&&computer_view->locked) {
        if(host_unlock_remote_link()==LINK_NONE&&unlock_remote_wifi_only()&&num(item(shown,"connectionHealth"),"wifiUnlockVersion")!=1)computer_notice("请先在电脑设置 Wi-Fi 一键解锁");
        else unlock_open();
    } else if(computer_view->can_lock&&!computer_view->locked)computer_action("lock",0,NULL);
}
static void computer_pending_refresh(int64_t now) {
    if(!computer_pending_click.active)return;
    computer_click_intent current={0},pending=computer_pending_click;
    bool same=current_page==(pending.media?5:14)&&now>=pending.until-3000&&now<pending.until&&computer_connected()&&computer_click_binding(&current)&&
        current.host==pending.host&&current.link==pending.link&&current.mode==pending.mode&&
        current.connection==pending.connection&&!strcmp(current.session,pending.session);
    if(!same){computer_pending_click.active=false;computer_notice("连接或页面已变化，操作未发送");return;}
    if(computer_view->busy)return;
    computer_pending_click.active=false; // Consume before any effect; never retry.
    if(pending.media) {
        if(!computer_ready()||computer_view->generation==pending.generation||!computer_media_allowed(pending.media)||
           computer_view->playing!=pending.playing||computer_music_key()!=pending.music) {
            computer_notice("播放状态已变化，操作未发送");return;
        }
        computer_action("media",0,computer_media_target(pending.media));return;
    }
    if(!computer_ready()||!computer_view->lock_known||computer_view->locked!=pending.unlock||
       computer_view->generation==pending.generation||computer_view->lock_updated<=pending.lock_updated){
        computer_notice("电脑状态未确认或已变化，操作未发送");return;
    }
    computer_action_notice[0]=0;computer_message_until=0;computer_lock_now();
}
static void computer_click(lv_event_t *event) {
    if(!computer_view)return;
    int action=(int)(intptr_t)lv_event_get_user_data(event);
    if(action>=20&&action<=22) {
        if(current_page!=5)return;
        computer_remote_get(computer_view);
        if(!computer_ready()||!computer_media_allowed(action)||computer_pending_click.active)return;
        if(!computer_view->busy){computer_action("media",0,computer_media_target(action));return;}
        computer_click_intent pending={0};
        if(computer_view->reading&&computer_click_binding(&pending)) {
            pending.active=true;pending.media=action;pending.playing=computer_view->playing;
            pending.music=computer_music_key();pending.generation=computer_view->generation;
            pending.until=esp_timer_get_time()/1000+3000;computer_pending_click=pending;
        }else computer_notice("操作正在进行，请稍候");
        return;
    }
    if(action==0&&computer_view->volume_available)computer_action("mute",!computer_view->muted,NULL);
    else if(action==1) {
        // The last UI frame may have started a read after copying its snapshot.
        computer_remote_get(computer_view);if(!computer_ready())return;
        if(computer_pending_click.active||unlock_overlay)return;
        if(!computer_view->busy){computer_lock_now();return;}
        computer_click_intent pending={0};
        if(computer_view->reading&&computer_view->lock_known&&computer_click_binding(&pending)){
            pending.active=true;pending.unlock=computer_view->locked;pending.generation=computer_view->generation;
            pending.lock_updated=computer_view->lock_updated;pending.until=esp_timer_get_time()/1000+3000;
            computer_pending_click=pending;
        }else computer_notice("操作正在进行，请稍候");
    }
    else if(action>=10&&action<10+computer_view->count)computer_action("launch",0,computer_view->launchers[action-10].id);
}
static void computer_volume_display(int value) {
    char text[20];
    bool muted=computer_view->volume_available&&computer_view->muted;
    snprintf(text,sizeof(text),computer_view->volume_available?"%d%%":"--",value);
    ui_text(computer_volume,muted?LV_SYMBOL_MUTE:text);
    visible(computer_volume_slash,muted);
}
static void computer_volume_changed(lv_event_t *event) {
    if(!computer_view)return;
    lv_event_code_t code=lv_event_get_code(event);
    if(code==LV_EVENT_PRESSED)computer_adjusting=true;
    if(code==LV_EVENT_VALUE_CHANGED)computer_volume_display(lv_slider_get_value(computer_slider));
    if(code==LV_EVENT_RELEASED){bool submit=computer_adjusting&&current_page==14;computer_adjusting=false;if(submit&&computer_view->volume_available)computer_action("volume",lv_slider_get_value(computer_slider),NULL);}
    if(code==LV_EVENT_PRESS_LOST)computer_adjusting=false;
}
static void computer_refresh(void) {
    if(computer_host){char id[33],name[TRUSTED_HOST_NAME];device_host_identity(id,name);ui_text_fmt(computer_host,"当前电脑：%s",name[0]?name:"尚未配对");}
    wifi_submission_test_poll(item(shown,"connectionHealth"));
    unlock_refresh();
    if(!computer_status||!computer_view||!computer_launch_pixels)return;
    computer_remote_get(computer_view);int64_t now=esp_timer_get_time()/1000;
    if(computer_pending_click.active){computer_pending_refresh(now);computer_remote_get(computer_view);}
    // Disabled controls do not receive LVGL RELEASED. Cancel abandoned drags
    // without input, including when navigating away from this page.
    if(computer_adjusting&&(current_page!=14||!gesture_active()))computer_adjusting=false;
    bool usable=computer_ready();
    computer_volume_display(computer_adjusting?lv_slider_get_value(computer_slider):computer_view->volume);
    if(!computer_adjusting)lv_slider_set_value(computer_slider,computer_view->volume,LV_ANIM_OFF);
    computer_enable(computer_slider,usable&&computer_view->volume_available);computer_enable(computer_mute,usable&&computer_view->volume_available);
    ui_text(computer_volume_icon,computer_view->muted?LV_SYMBOL_MUTE:LV_SYMBOL_VOLUME_MAX);
    tint(computer_volume_icon,usable&&computer_view->volume_available?BLUE:MUTED);
    bool locked=computer_view->lock_known&&computer_view->locked;
    bool can_unlock=locked;
    computer_enable(computer_lock,usable&&(!computer_view->busy||computer_view->reading)&&(can_unlock||(computer_view->can_lock&&!locked)));
    ui_text(lv_obj_get_child(computer_lock,0),locked?(can_unlock?"解锁":"已锁屏"):"锁屏");
    uint32_t lock_color=usable&&(computer_view->can_lock||computer_view->locked)?BLUE:MUTED;
    ui_border_color(lv_obj_get_child(computer_lock,1),lv_color_hex(lock_color),0);
    ui_bg_color(lv_obj_get_child(computer_lock,2),lv_color_hex(lock_color),0);
    ui_text(lv_obj_get_child(computer_mute,0),computer_view->muted?"取消静音":"静音");
    const char *status=!computer_connected()?"连接电脑后即可使用":!computer_supported()?"请更新 Windows 桥接以使用电脑控制":computer_view->ready&&!usable?"正在读取电脑状态…":!computer_view->ready&&computer_view->busy?"正在读取电脑状态…":computer_view->message[0]?computer_view->message:computer_view->ready?"电脑已连接":"正在读取电脑状态…";
    if(computer_pending_click.active&&!computer_pending_click.media)status=computer_pending_click.unlock?"正在读取状态，随后解锁…":"正在读取状态，随后锁屏…";
    else if(computer_view->busy&&!computer_view->reading)status="操作正在进行，请稍候";
    else if(now<computer_message_until&&computer_action_notice[0])status=computer_action_notice;
    char lock_status[224];
    if(usable&&computer_view->lock_known){snprintf(lock_status,sizeof(lock_status),"%s · %s",status,computer_view->locked?"已锁屏":"未锁屏");status=lock_status;}
    ui_text(computer_status,status);
    for(int i=0;i<4;i++) {
        ui_text(lv_obj_get_child(computer_launch[i],0),i<computer_view->count?computer_view->launchers[i].name:"未设置");
        computer_enable(computer_launch[i],usable&&i<computer_view->count);
        bool configured=i<computer_view->count;computer_launcher *launcher=&computer_view->launchers[i];
        lv_obj_t *caption=lv_obj_get_child(computer_launch[i],0);
        if(computer_launch_layout[i]!=(int)configured) {
            computer_launch_layout[i]=configured;
            lv_obj_set_width(caption,configured?192:246);lv_obj_set_style_text_align(caption,configured?LV_TEXT_ALIGN_LEFT:LV_TEXT_ALIGN_CENTER,0);
            lv_obj_align(caption,configured?LV_ALIGN_LEFT_MID:LV_ALIGN_CENTER,configured?64:0,0);
        }
        visible(computer_launch_icon[i],configured&&launcher->has_icon);
        visible(computer_launch_symbol[i],configured&&!launcher->has_icon);
        ui_text(computer_launch_symbol[i],launcher->folder?LV_SYMBOL_DIRECTORY:LV_SYMBOL_FILE);
        if(configured&&launcher->has_icon&&(!computer_launch_images[i].data||memcmp(computer_launch_pixels[i],launcher->icon,sizeof(launcher->icon)))) {
            lv_image_cache_drop(&computer_launch_images[i]);memcpy(computer_launch_pixels[i],launcher->icon,sizeof(launcher->icon));
            computer_launch_images[i]=(lv_image_dsc_t){.header={.magic=LV_IMAGE_HEADER_MAGIC,.cf=LV_COLOR_FORMAT_ARGB8888,.w=32,.h=32,.stride=128},.data_size=4096,.data=computer_launch_pixels[i]};
            lv_image_set_src(computer_launch_icon[i],&computer_launch_images[i]);
        }
    }
    ui_text(computer_launch_hint,computer_view->count?"点击打开电脑上的软件或文件夹":"在电脑的设备菜单中打开“电脑控制”，添加常用入口");
    if(music_controls) {
        bool track=music_has_track();visible(music_controls,track);visible(music_control_hint,track);
        const bool capabilities[]={computer_view->can_previous,computer_view->can_play_pause,computer_view->can_next};
        for(int i=0;i<3;i++)computer_enable(music_control_buttons[i],usable&&computer_view->media_available&&capabilities[i]&&
            (!computer_view->busy||computer_view->reading)&&!computer_pending_click.active);
        ui_text(lv_obj_get_child(music_control_buttons[1],0),computer_view->playing?LV_SYMBOL_PAUSE:LV_SYMBOL_PLAY);
        const char *hint=!computer_connected()?"连接电脑后可控制播放":!computer_supported()?"更新 Windows 桥接以控制播放":!computer_view->ready?"正在读取播放状态":!computer_view->media_available?"当前播放器不支持控制":"";
        if(computer_pending_click.active&&computer_pending_click.media)hint="正在读取播放状态，随后执行…";
        else if(computer_view->busy&&!computer_view->reading)hint="正在处理播放操作…";
        else if(now<computer_message_until&&computer_action_notice[0])hint=computer_action_notice;
        ui_text(music_control_hint,hint);
        int x=lv_obj_get_x(music),width=lv_obj_get_width(music);
        if(computer_music_layout_width!=width||computer_music_layout_x!=x) {
            computer_music_layout_width=width;computer_music_layout_x=x;
            lv_obj_set_pos(music_controls,x,420);lv_obj_set_width(music_controls,width);
            lv_obj_set_pos(music_control_hint,x,488);lv_obj_set_width(music_control_hint,width);
            for(int i=0;i<3;i++){lv_obj_set_x(music_control_buttons[i],i*(width+12)/3);lv_obj_set_width(music_control_buttons[i],(width-24)/3);lv_obj_center(lv_obj_get_child(music_control_buttons[i],0));}
        }
    }
    bool confirming_unlock=unlock_overlay&&unlock_sent&&!unlock_success_until&&now-unlock_submitted_at<10000;
    int poll_interval=confirming_unlock?500:2000;
    // Status polls never overwrite a held slider or submit a command. Keeping
    // them running prevents a stale drag from permanently disabling controls.
    host_unlock_state local_unlock;host_unlock_remote_get(&local_unlock);
    // The active host-side unlock renews a short lease on this same mailbox.
    // Resume status polling after completion for independent desktop confirmation.
    if(!computer_pending_click.active&&!local_unlock.busy&&(current_page==14||current_page==5||unlock_history_pending())&&computer_connected()&&computer_supported()&&!computer_view->busy&&(confirming_unlock||now>=computer_message_until)&&now-computer_poll_at>=poll_interval) {
        char id[37];codex_uuid(id);computer_poll_at=now;computer_remote_start(id,"status",0,NULL);
    }
}
static void computer_mute_slash_draw(lv_event_t *event) {
    lv_area_t area;lv_obj_get_coords(lv_event_get_target_obj(event),&area);
    lv_layer_t *layer=lv_event_get_layer(event);
    // Two filled triangles keep the slash crisp in both device and native renderers.
    for(int pass=0;pass<2;pass++) {
        int edge=pass?2:6;
        lv_point_precise_t points[]={{area.x1,area.y1+36-edge},{area.x1+edge,area.y1+36},
            {area.x1+36,area.y1+edge},{area.x1+36-edge,area.y1}};
        lv_draw_triangle_dsc_t d;lv_draw_triangle_dsc_init(&d);d.bg_color=lv_color_hex(pass?BLUE:SURFACE);d.bg_opa=LV_OPA_COVER;
        d.p[0]=points[0];d.p[1]=points[1];d.p[2]=points[2];lv_draw_triangle(layer,&d);
        d.p[1]=points[2];d.p[2]=points[3];lv_draw_triangle(layer,&d);
    }
}
static void computer_create(lv_obj_t *parent) {
    // Icon state/cache belongs in PSRAM; keep internal RAM for radio/task stacks.
    computer_view=codex_alloc(sizeof(*computer_view));
    computer_launch_pixels=codex_alloc(4*sizeof(*computer_launch_pixels));
    if(!computer_view||!computer_launch_pixels){muted(parent,"内存不足，请重启设备后重试",0,0,1220);return;}
    computer_status=muted(parent,"连接电脑后即可使用",0,0,690);lv_label_set_long_mode(computer_status,LV_LABEL_LONG_DOT);
    computer_host=muted(parent,"当前电脑：尚未配对",720,0,500);lv_label_set_long_mode(computer_host,LV_LABEL_LONG_DOT);lv_obj_set_style_text_align(computer_host,LV_TEXT_ALIGN_RIGHT,0);
    lv_obj_t *audio=card(parent,0,48,934,200);lv_obj_set_style_pad_all(audio,0,0);
    label(audio,"电脑音量",28,26,300,false);
    // Match the visible left edge: the heading's first glyph has 3 px of bearing.
    computer_volume_icon=label(audio,LV_SYMBOL_VOLUME_MAX,31,108,48,false);ui_text_font(computer_volume_icon,&lv_font_montserrat_40,0);
    computer_volume=label(audio,"--",738,24,168,false);ui_text_font(computer_volume,&lv_font_montserrat_40,0);tint(computer_volume,BLUE);lv_obj_set_style_text_align(computer_volume,LV_TEXT_ALIGN_CENTER,0);
    computer_volume_slash=lv_obj_create(computer_volume);lv_obj_remove_style_all(computer_volume_slash);
    lv_obj_set_size(computer_volume_slash,36,36);lv_obj_center(computer_volume_slash);
    lv_obj_remove_flag(computer_volume_slash,LV_OBJ_FLAG_SCROLLABLE|LV_OBJ_FLAG_CLICKABLE);
    lv_obj_add_event_cb(computer_volume_slash,computer_mute_slash_draw,LV_EVENT_DRAW_MAIN,NULL);
    // Leave room for the zero-value knob as well as the enlarged speaker glyph.
    computer_slider=lv_slider_create(audio);lv_obj_set_pos(computer_slider,118,124);lv_obj_set_size(computer_slider,580,16);lv_slider_set_range(computer_slider,0,100);
    lv_obj_align_to(computer_volume_icon,computer_slider,LV_ALIGN_OUT_LEFT_MID,-39,0);
    ui_bg_color(computer_slider,lv_color_hex(TRACK),LV_PART_MAIN);ui_bg_color(computer_slider,lv_color_hex(BLUE),LV_PART_INDICATOR);ui_bg_color(computer_slider,lv_color_hex(INK),LV_PART_KNOB);
    lv_obj_add_event_cb(computer_slider,computer_volume_changed,LV_EVENT_ALL,NULL);
    computer_mute=button(audio,"静音",738,96,168,computer_click,0);lv_obj_set_height(computer_mute,72);lv_obj_center(lv_obj_get_child(computer_mute,0));
    computer_lock=button(parent,"锁屏",958,48,262,computer_click,1);lv_obj_set_height(computer_lock,200);
    lv_obj_set_style_radius(computer_lock,22,0);ui_bg_color(computer_lock,lv_color_hex(SURFACE),0);
    lv_obj_t *lock_caption=lv_obj_get_child(computer_lock,0);ui_text_font(lock_caption,&tab5_font_30,0);lv_obj_align(lock_caption,LV_ALIGN_BOTTOM_MID,0,-30);
    lv_obj_t *shackle=shape(computer_lock,113,44,36,46,BLUE,18);ui_bg_opa(shackle,LV_OPA_TRANSP,0);
    lv_obj_set_style_border_width(shackle,4,0);ui_border_color(shackle,lv_color_hex(BLUE),0);
    shape(computer_lock,104,72,54,44,BLUE,8);shape(computer_lock,127,85,8,8,SURFACE,LV_RADIUS_CIRCLE);shape(computer_lock,129,90,4,12,SURFACE,2);
    lv_obj_t *launch=card(parent,0,272,1220,244);label(launch,"快捷启动",24,18,1100,false);
    computer_launch_hint=muted(launch,"",24,62,1172);
    for(int i=0;i<4;i++){computer_launch[i]=button(launch,"未设置",24+i*298,122,278,computer_click,10+i);lv_obj_set_height(computer_launch[i],88);lv_obj_t *caption=lv_obj_get_child(computer_launch[i],0);lv_obj_set_width(caption,246);lv_label_set_long_mode(caption,LV_LABEL_LONG_DOT);lv_obj_set_style_text_align(caption,LV_TEXT_ALIGN_CENTER,0);lv_obj_center(caption);
        computer_launch_icon[i]=lv_image_create(computer_launch[i]);lv_obj_align(computer_launch_icon[i],LV_ALIGN_LEFT_MID,20,0);
        computer_launch_symbol[i]=label(computer_launch[i],LV_SYMBOL_FILE,20,28,32,false);ui_text_font(computer_launch_symbol[i],&lv_font_montserrat_24,0);tint(computer_launch_symbol[i],BLUE);
    }
    computer_refresh();
}
static void computer_music_create(lv_obj_t *parent) {
    music_controls=lv_obj_create(parent);lv_obj_remove_style_all(music_controls);lv_obj_set_size(music_controls,728,64);lv_obj_remove_flag(music_controls,LV_OBJ_FLAG_SCROLLABLE);
    const char *symbols[]={LV_SYMBOL_PREV,LV_SYMBOL_PLAY,LV_SYMBOL_NEXT};
    for(int i=0;i<3;i++){music_control_buttons[i]=button(music_controls,symbols[i],i*246,0,234,computer_click,20+i);lv_obj_set_height(music_control_buttons[i],64);ui_text_font(lv_obj_get_child(music_control_buttons[i],0),&lv_font_montserrat_24,0);lv_obj_center(lv_obj_get_child(music_control_buttons[i],0));}
    music_control_hint=muted(parent,"",466,488,728);computer_refresh();
}
