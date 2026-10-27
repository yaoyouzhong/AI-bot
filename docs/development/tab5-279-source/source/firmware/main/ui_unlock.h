// Existing lock button. PIN stays in volatile device memory until the single
// confirmed submission; no credential is part of the read-only power RPC.
#include "ble_keyboard.h"
#include "computer_remote.h"
#ifdef TAB5_UI_PREVIEW
atomic_uint tab5_unlock_diagnostic;
atomic_uint tab5_unlock_failure[3];
atomic_uint tab5_unlock_history[2+UNLOCK_HISTORY_COUNT*UNLOCK_HISTORY_WORDS];
atomic_uint tab5_unlock_history_received;
static ble_keyboard_state preview_keyboard={.connected=true,.secure=true,.subscribed=true,.bridge_verified=true,.ready=true,.connection=1};
static unsigned preview_unlock_submissions;
static unsigned preview_cover_reads,preview_wake_submissions,preview_display_wakes;
static bool preview_cover,preview_cover_complete=true,preview_wake_accept=true;
static int preview_display=1;
static char preview_cover_request[37];
bool computer_remote_cover_state(const char *id,bool *cover){*cover=preview_cover;return preview_cover_complete&&!strcmp(id,preview_cover_request);}
bool computer_remote_display_state(const char *id,int *display){*display=preview_display;return preview_cover_complete&&!strcmp(id,preview_cover_request);}
bool ble_keyboard_wake(const char *id,unsigned connection){bool cover=false;if(!preview_wake_accept||!preview_keyboard.ready||connection!=preview_keyboard.connection||!computer_remote_cover_state(id,&cover)||!cover)return false;preview_wake_submissions++;preview_keyboard.busy=true;preview_keyboard.finished=false;preview_keyboard.failed=false;return true;}
bool ble_keyboard_display_wake(const char *id,unsigned connection){int display=-1;if(!preview_wake_accept||!preview_keyboard.ready||connection!=preview_keyboard.connection||!computer_remote_display_state(id,&display)||display!=0)return false;preview_display_wakes++;preview_keyboard.busy=true;preview_keyboard.finished=false;preview_keyboard.failed=false;return true;}
void ble_keyboard_get(ble_keyboard_state *out){*out=preview_keyboard;}
void ble_keyboard_cancel(void){preview_keyboard.busy=false;}
void ble_keyboard_pair_confirm(bool accept){preview_keyboard.pairing=false;preview_keyboard.secure=accept;}
bool ble_keyboard_start(const char *pin,size_t length,unsigned connection,const char *id){(void)pin;int display=-1;if(!preview_keyboard.ready||connection!=preview_keyboard.connection||length<1||length>64||!computer_remote_display_state(id,&display)||display<1)return false;preview_unlock_submissions++;preview_keyboard.busy=true;preview_keyboard.finished=false;preview_keyboard.failed=false;return true;}
#endif
#include "unlock_keyboard.h"
#include "oneclick_remote.h"
#ifdef TAB5_UI_PREVIEW
static bool preview_usb_available;
bool usb_keyboard_selected(void){return preview_usb_available;}
static int preview_oneclick_state,preview_oneclick_result=2;static unsigned preview_oneclick_requests;
static int64_t preview_oneclick_until;
void usb_keyboard_get(ble_keyboard_state *s){*s=preview_keyboard;if(!preview_usb_available)memset(s,0,sizeof(*s));}
bool usb_keyboard_start(const char *p,size_t n,unsigned c,const char *id){return preview_usb_available&&ble_keyboard_start(p,n,c,id);}
bool usb_keyboard_wake(const char *id,unsigned c,bool display){return preview_usb_available&&(display?ble_keyboard_display_wake(id,c):ble_keyboard_wake(id,c));}
void usb_keyboard_cancel(void){ble_keyboard_cancel();}
void oneclick_cancel(void){preview_oneclick_state=0;}
bool oneclick_start(const char *id,link_kind link,bool extended){(void)id;(void)link;(void)extended;preview_oneclick_requests++;preview_oneclick_state=preview_oneclick_result;preview_oneclick_until=esp_timer_get_time()/1000+6000;return true;}
int oneclick_take(char *p,size_t capacity,size_t *n){*n=0;if(preview_oneclick_state==1&&esp_timer_get_time()/1000>=preview_oneclick_until)preview_oneclick_state=-8;if(preview_oneclick_state!=2)return preview_oneclick_state;if(capacity<4)return -1;memcpy(p,"1234",4);*n=4;preview_oneclick_state=0;return 2;}
#endif
#include "unlock_remote.h"
#ifdef TAB5_UI_PREVIEW
static bool preview_wifi_only;static unsigned preview_wifi_connection=17,preview_wifi_requests,preview_wifi_cancels;
static unlock_remote_state preview_wifi_unlock;
bool unlock_remote_wifi_only(void){return preview_wifi_only;}
unsigned unlock_remote_connection(void){return preview_wifi_connection;}
void unlock_remote_get(unlock_remote_state *out){*out=preview_wifi_unlock;}
void unlock_remote_cancel(void){if(preview_wifi_unlock.busy)preview_wifi_cancels++;preview_wifi_unlock.busy=false;}
bool unlock_remote_start(const char *id,unsigned connection){if(!id||!preview_wifi_only||connection!=preview_wifi_connection||preview_wifi_unlock.busy)return false;preview_wifi_requests++;preview_wifi_unlock=(unlock_remote_state){.busy=true};return true;}
#endif
#include "host_unlock_remote.h"
#ifdef TAB5_UI_PREVIEW
static link_kind preview_host_link=LINK_NONE;
static host_unlock_state preview_host_unlock;
static unsigned preview_host_requests,preview_host_cancels;
link_kind host_unlock_remote_link(void){return preview_host_link;}
bool host_unlock_remote_start(const char *id,link_kind link){if(!id||link!=preview_host_link||preview_host_unlock.busy)return false;preview_host_requests++;preview_host_unlock=(host_unlock_state){.busy=true,.current=true};return true;}
void host_unlock_remote_cancel(void){if(preview_host_unlock.busy)preview_host_cancels++;preview_host_unlock.busy=false;}
void host_unlock_remote_get(host_unlock_state *out){*out=preview_host_unlock;}
#endif
static bool unlock_wifi,unlock_host;
static link_kind unlock_host_link;
static bool unlock_auto,unlock_auto_requested,unlock_auto_consumed,unlock_auto_failed;
static lv_obj_t *unlock_overlay,*unlock_mask,*unlock_hint,*unlock_confirm,*unlock_close_button,*unlock_keys[12];
static lv_obj_t *unlock_panel,*unlock_title,*unlock_status_face,*unlock_status_symbol,*unlock_spinner;
static lv_obj_t *unlock_text_keys,*unlock_mode_button;
static unsigned unlock_text_mode;
static const char *unlock_lower_map[]={"q","w","e","r","t","y","u","i","o","p","\n","a","s","d","f","g","h","j","k","l","\n","z","x","c","v","b","n","m",LV_SYMBOL_BACKSPACE,"\n","Space","Clear",""};
static const char *unlock_upper_map[]={"Q","W","E","R","T","Y","U","I","O","P","\n","A","S","D","F","G","H","J","K","L","\n","Z","X","C","V","B","N","M",LV_SYMBOL_BACKSPACE,"\n","Space","Clear",""};
static const char *unlock_symbol_map[]={"!","@","#","$","%","^","&","*","(",")","\n","-","_","=","+","[","]","{","}","\\","|","\n",";",":","'","\"",",",".","<",">","/","?","\n","`","~","Space","Clear",LV_SYMBOL_BACKSPACE,""};
static bool unlock_spinning;
static char unlock_pin[64];static size_t unlock_length;
static int64_t unlock_deadline,unlock_submitted_at;static bool unlock_sent,unlock_pairing_mode;
static int64_t unlock_success_until;
static unsigned unlock_connection;
static char unlock_cover_request[37];
// Bounded preparation, once on open and again on explicit confirmation:
// read -> optional Shift -> confirmed display On -> optional cover Space -> read.
// One extra Shift is allowed only after completion, settling and a new Off
// observation. Space and PIN stay single-shot; uncertainty never replays keys.
static int unlock_preparing; // 1=read pending, 2=key in flight, 3=settle, 4=wait to read
static int64_t unlock_prepare_until,unlock_prepare_deadline;
static bool unlock_observing,unlock_ready_cover;
static int64_t unlock_ready_since,unlock_ready_at;
static int unlock_ready_display;static unsigned unlock_ready_samples;
static bool unlock_woke,unlock_prepared,unlock_prepare_failed,unlock_submit_pending;
static bool unlock_wake_budget;
static int unlock_failure; // 1=budget expired, 2=accepted key stopped, 3=peer/state changed
static unsigned unlock_display_attempts;static bool unlock_display_inflight,unlock_cover_attempted;
static unsigned unlock_attempt;
static bool unlock_failure_recorded;
static bool unlock_initial_pending;
static bool unlock_history_recorded;static int64_t unlock_started,unlock_input_at;
static void unlock_record_history(unsigned outcome,unsigned reason) {
    if(unlock_history_recorded||unlock_pairing_mode)return;
    int64_t now=esp_timer_get_time()/1000,updated=0,rpc_started=0;
    bool locked=false,fresh=computer_remote_lock_state(&locked,&updated);
    ble_keyboard_state s;unlock_keyboard_get(&s);unsigned op=0;int status=0;
    computer_remote_diagnostic(&op,&status,&rpc_started);
    host_unlock_state host={0};if(unlock_host)host_unlock_remote_get(&host);
    unsigned missing=unlock_host?(!host.current?1u:0u):unlock_wifi?(!unlock_remote_wifi_only()?1u:0u)|(unlock_remote_connection()!=unlock_connection?32u:0u):
        (!s.connected?1u:0u)|(!s.secure?2u:0u)|(!s.subscribed?4u:0u)|(!s.bridge_verified?8u:0u)|
        (!s.ready?16u:0u)|(s.connection!=unlock_connection?32u:0u)|(s.busy?256u:0u);
    missing|=(!fresh?64u:0u)|(!locked?128u:0u);
    unlock_remote_state wifi={0};if(unlock_wifi)unlock_remote_get(&wifi);
    unsigned phase=unlock_host?(host.stage?5u:1u):unlock_wifi?(wifi.stage?5u:1u):(unsigned)(unlock_preparing&7);
    unsigned flags=(unlock_wifi?2u:unlock_using_usb?0u:1u)|(outcome<<2)|((reason&31u)<<4)|
        (phase<<9)|(unlock_auto?1u<<12:0u)|(!unlock_wifi&&!unlock_host&&unlock_sent?1u<<13:0u)|
        (fresh?1u<<14:0u)|(locked?1u<<15:0u)|(missing<<16);
    unsigned words[UNLOCK_HISTORY_WORDS]={0,flags,(unsigned)now,(unsigned)(now-unlock_started),
        updated>0&&now>=updated?(unsigned)(now-updated):UINT32_MAX,
        unlock_input_at?(unsigned)(unlock_input_at-unlock_started):UINT32_MAX,
        unlock_input_at?(unsigned)(now-unlock_input_at):UINT32_MAX,
        (op<<16)|(unsigned)(status>=0&&status<=599?status:0),
        rpc_started>0&&now>=rpc_started?(unsigned)(now-rpc_started):UINT32_MAX};
    unlock_history_append(words);unlock_history_recorded=true;
}
static void unlock_record_failure(unsigned reason) {
    unlock_record_history(2,reason);
    if(unlock_failure_recorded||unlock_pairing_mode||unlock_wifi||unlock_host)return;
    ble_keyboard_state s;unlock_keyboard_get(&s);bool locked=false;int64_t updated=0;
    bool fresh=computer_remote_lock_state(&locked,&updated);
    unsigned missing=(!s.connected?1u:0u)|(!s.secure?2u:0u)|(!s.subscribed?4u:0u)|
        (!s.bridge_verified?8u:0u)|(!s.ready?16u:0u)|(s.connection!=unlock_connection?32u:0u)|
        (!fresh?64u:0u)|(!locked?128u:0u)|(s.busy?256u:0u);
    unsigned packed=(reason&31u)|(missing<<5)|((unsigned)(unlock_preparing&7)<<14)|
        (unlock_using_usb?1u<<17:0u)|(unlock_auto?1u<<18:0u)|(unlock_sent?1u<<19:0u)|((unlock_attempt&4095u)<<20);
    unlock_failure_record(packed,(unsigned)(esp_timer_get_time()/1000));unlock_failure_recorded=true;
}
static void unlock_clear(void){volatile char *p=unlock_pin;for(size_t i=0;i<sizeof(unlock_pin);i++)p[i]=0;unlock_length=0;}
static void unlock_close(void){if(unlock_overlay)unlock_record_history(3,0);unlock_success_until=0;oneclick_cancel();atomic_store(&tab5_unlock_diagnostic,0u);if(unlock_pairing_mode)ble_keyboard_pair_confirm(false);if(unlock_host)host_unlock_remote_cancel();else if(unlock_wifi)unlock_remote_cancel();else unlock_keyboard_cancel();unlock_clear();unlock_preparing=0;unlock_submit_pending=false;unlock_cover_request[0]=0;if(unlock_overlay)lv_obj_delete(unlock_overlay);unlock_overlay=NULL;}
static void unlock_enable(lv_obj_t *object,bool enabled){if(enabled)lv_obj_remove_state(object,LV_STATE_DISABLED);else lv_obj_add_state(object,LV_STATE_DISABLED);}
static void unlock_show_mask(void){char mask[21];size_t n=unlock_length<20?unlock_length:20;memset(mask,'*',n);mask[n]=0;ui_text(unlock_mask,n?mask:"PIN");}
static void unlock_spin(bool active){
    visible(unlock_spinner,active);
    if(active&&!unlock_spinning)lv_spinner_set_anim_params(unlock_spinner,900,240);
    else if(!active&&unlock_spinning)lv_anim_delete(unlock_spinner,NULL);
    unlock_spinning=active;
}
static const char *unlock_transport_name(void){return unlock_wifi?"Wi-Fi":unlock_using_usb?"USB":"蓝牙";}
static void unlock_status(const char *message,bool error){
    char text[224];
    if(unlock_auto){
        if(!strcmp(message,"正在准备，请稍候"))snprintf(text,sizeof(text),"正在准备 %s 解锁",unlock_transport_name());
        else if(!strcmp(message,"正在解锁，请稍候"))snprintf(text,sizeof(text),"正在通过 %s 解锁",unlock_transport_name());
        else snprintf(text,sizeof(text),"%s %s",unlock_transport_name(),message);
        message=text;
    }
    ui_text(unlock_hint,message);
    if(!unlock_auto)return;
    unlock_spin(!error);visible(unlock_status_face,error);
    if(error){ui_bg_color(unlock_status_face,lv_color_hex(AMBER),0);tint(unlock_status_symbol,AMBER);ui_text(unlock_status_symbol,"!");}
    ui_text(lv_obj_get_child(unlock_close_button,0),error?"关闭":"取消");
}
static void unlock_status_layout(bool success){
    lv_obj_set_size(unlock_panel,520,304);lv_obj_center(unlock_panel);
    visible(unlock_title,false);visible(unlock_mask,false);visible(unlock_confirm,false);
    visible(unlock_text_keys,false);visible(unlock_mode_button,false);
    for(int i=0;i<12;i++)visible(unlock_keys[i],false);
    lv_obj_set_pos(unlock_hint,24,success?166:128);lv_obj_set_size(unlock_hint,472,success?44:72);
    ui_text_font(unlock_hint,success?&tab5_font_30:&tab5_font_24,0);
    lv_obj_set_style_text_align(unlock_hint,LV_TEXT_ALIGN_CENTER,0);tint(unlock_hint,INK);
    lv_obj_set_pos(unlock_status_face,224,success?66:36);
    lv_obj_set_pos(unlock_close_button,180,222);lv_obj_set_size(unlock_close_button,160,56);
    lv_obj_center(lv_obj_get_child(unlock_close_button,0));visible(unlock_close_button,!success);
}
static void unlock_success(int64_t now){
    unlock_record_history(1,0);
    oneclick_cancel();if(unlock_host)host_unlock_remote_cancel();else if(unlock_wifi)unlock_remote_cancel();else unlock_keyboard_cancel();unlock_clear();unlock_show_mask();
    unlock_preparing=0;unlock_submit_pending=false;unlock_prepared=false;
    unlock_enable(unlock_confirm,false);for(int i=0;i<12;i++)unlock_enable(unlock_keys[i],false);
    unlock_status_layout(true);unlock_spin(false);visible(unlock_status_face,true);
    ui_bg_color(unlock_status_face,lv_color_hex(GREEN),0);tint(unlock_status_symbol,GREEN);ui_text(unlock_status_symbol,LV_SYMBOL_OK);
    ui_text(unlock_hint,"电脑已解锁");
    unlock_success_until=now+500; // UI refresh timer; never block the input/display task.
}
static void unlock_fail(int reason){unlock_record_failure((unsigned)reason);oneclick_cancel();if(unlock_host)host_unlock_remote_cancel();else if(unlock_wifi)unlock_remote_cancel();else unlock_keyboard_cancel();unlock_clear();unlock_show_mask();unlock_preparing=0;unlock_submit_pending=false;unlock_prepared=false;unlock_prepare_failed=true;unlock_failure=reason;if(unlock_auto)unlock_auto_failed=true;}
static void unlock_wait(int64_t now){unlock_preparing=4;unlock_prepare_until=now+200;}
static void unlock_observation_reset(void){unlock_ready_samples=0;unlock_ready_since=unlock_ready_at=0;}
// Only called once per completed request-bound reply, never by an idle UI tick.
// Two distinct replies must span 500ms; a missing/changed state or long gap
// starts a new observation. A completed wake retains its existing settle gate.
static bool unlock_observed_ready(int64_t now,int display,bool cover){
    if(!unlock_observing||unlock_woke)return true;
    if(!unlock_ready_samples||now<unlock_ready_at||now-unlock_ready_at>1000||
       display!=unlock_ready_display||cover!=unlock_ready_cover){
        unlock_ready_since=now;unlock_ready_samples=0;
    }
    unlock_ready_at=now;unlock_ready_display=display;unlock_ready_cover=cover;unlock_ready_samples++;
    return unlock_ready_samples>=2&&now-unlock_ready_since>=500;
}
static void unlock_probe(int64_t now) {
    codex_uuid(unlock_cover_request);
    bool locked=false;int64_t updated=0;bool fresh=computer_remote_lock_state(&locked,&updated);
    // A run of screen probes must not starve ordinary authenticated lock state.
    // Never insert a status retry after a credential has been consumed.
    if(!unlock_submit_pending&&!unlock_auto_requested&&(!fresh||now-updated>=3000)) {
        computer_remote_start(unlock_cover_request,"status",0,NULL);unlock_preparing=4;unlock_prepare_until=now+200;return;
    }
    if(computer_remote_start(unlock_cover_request,"lock-screen",0,NULL)){unlock_preparing=1;unlock_prepare_until=now+2000;}
    else {unlock_preparing=4;unlock_prepare_until=now+100;}
}
static void unlock_prepare(int64_t now,bool opening) {
    unlock_prepared=false;unlock_prepare_failed=false;unlock_display_attempts=0;unlock_display_inflight=false;unlock_cover_attempted=false;
    unlock_woke=false;unlock_failure=0;unlock_wake_budget=opening;
    unlock_prepare_deadline=now+(opening?12000:4000);unlock_observing=opening;unlock_observation_reset();unlock_probe(now);
}
static void unlock_open_mode(bool pairing);
static void unlock_refresh(void) {
    ble_keyboard_state state,pair_state;ble_keyboard_get(&pair_state);unlock_keyboard_get(&state);
    if(pair_state.pairing&&(!unlock_overlay||!unlock_pairing_mode)){if(unlock_overlay)unlock_close();unlock_open_mode(true);return;}
    if(!unlock_overlay){atomic_store(&tab5_unlock_diagnostic,0u);return;}
    int64_t now=esp_timer_get_time()/1000;
    if(!unlock_pairing_mode&&current_page!=14){unlock_close();return;}
    if(unlock_success_until){if(now>=unlock_success_until)unlock_close();return;}
    if(now>=unlock_deadline){unlock_record_failure(10);unlock_close();return;}
    if(unlock_pairing_mode) {
        if(!state.pairing){unlock_close();return;}
        char code[16];snprintf(code,sizeof(code),"%06lu",(unsigned long)state.comparison);ui_text(unlock_mask,state.repair_pairing?"PAIR":code);return;
    }
    if(unlock_host){
        host_unlock_state host;host_unlock_remote_get(&host);
        bool locked=false;int64_t updated=0;bool fresh=computer_remote_lock_state(&locked,&updated);
        if(!host.current){host_unlock_remote_cancel();unlock_auto_failed=true;}
        if(!unlock_auto_failed&&host.finished&&host.unlocked&&fresh&&!locked&&updated>unlock_submitted_at){unlock_success(now);return;}
        bool failed=unlock_auto_failed||(host.finished&&host.error!=0)||(now-unlock_submitted_at>=40000);
        if(failed){host_unlock_remote_cancel();unlock_auto_failed=true;unlock_record_failure(!host.current?3:host.error==4?19:20);}
        unlock_status(failed?(host.error==4?"请在 Mac 开启 USB／蓝牙解锁":"解锁未完成\n请关闭面板后重试"):
            host.stage==0&&host.busy?"正在准备，请稍候":"正在解锁，请稍候",failed);
        return;
    }
    if(unlock_wifi){
        unlock_remote_state wifi;unlock_remote_get(&wifi);
        bool locked=false;int64_t updated=0;bool fresh=computer_remote_lock_state(&locked,&updated);
        bool same=unlock_remote_wifi_only()&&unlock_remote_connection()==unlock_connection;
        if(!same){unlock_remote_cancel();unlock_auto_failed=true;}
        if(!unlock_auto_failed&&wifi.finished&&wifi.unlocked&&fresh&&!locked&&updated>unlock_submitted_at){unlock_success(now);return;}
        bool failed=unlock_auto_failed||(wifi.finished&&wifi.error!=0)||(wifi.finished&&now-unlock_submitted_at>=40000);
        if(failed)unlock_record_failure(!same?3:wifi.error==3?12:wifi.error==4?19:wifi.error==5?14:wifi.error==6?9:20);
        unlock_status(failed?(wifi.error==3?"请在电脑设置 Windows PIN":wifi.error==4?"请在电脑开启 Wi-Fi 解锁":wifi.error==5?"电脑屏幕尚未就绪\n请关闭面板后重试":"解锁未完成\n请关闭面板后重试"):
            wifi.stage==0&&wifi.busy?"正在准备，请稍候":"正在解锁，请稍候",failed);
        return;
    }
    bool locked=false;int64_t updated=0;bool fresh=computer_remote_lock_state(&locked,&updated);
    bool ready=state.ready&&state.connection==unlock_connection&&fresh&&locked;
    bool peer=state.connected&&state.secure&&state.connection==unlock_connection;
    if(!ready||state.busy)unlock_observation_reset();
    // Only the read-only opening phase can recover. Never extend its budget,
    // rebind a changed connection, replay an accepted wake, or retry a PIN grant.
    bool opening_wait=unlock_preparing&&now<unlock_prepare_deadline&&state.connection==unlock_connection&&
        !unlock_sent&&!unlock_submit_pending&&!unlock_auto_requested&&!unlock_woke&&
        !unlock_display_attempts&&!unlock_cover_attempted;
    // State flags only: no PIN, length, keycode, pairing code, account or text.
    atomic_store(&tab5_unlock_diagnostic,(fresh?1u<<11:0u)|(locked?1u<<12:0u)|
        (state.connection==unlock_connection?1u<<13:0u)|(1u<<14)|(unlock_using_usb?1u<<22:0u)|(unlock_auto?1u<<10:0u)|(unlock_auto_failed?1u<<31:0u)|
        (unlock_prepared?1u<<15:0u)|(unlock_preparing?1u<<16:0u)|
        (unlock_submit_pending?1u<<17:0u)|(unlock_sent?1u<<18:0u)|
        (unlock_prepare_failed?1u<<19:0u)|((unsigned)(unlock_preparing&7)<<28));
    int reported=-1;computer_remote_screen_state(&reported);
    if(unlock_preparing&&now>=unlock_prepare_deadline)unlock_fail(1);
    else if(unlock_preparing&&((!peer&&(!opening_wait||!unlock_initial_pending))||(fresh&&!locked)))unlock_fail(3);
    if(peer&&state.ready&&state.subscribed&&state.bridge_verified&&fresh)unlock_initial_pending=false;
    // Read-only work may wait through transient HID suspension/cache freshness.
    // Once a key was accepted, uncertainty still aborts instead of replaying it.
    if(unlock_preparing==2&&!ready)unlock_fail(2);
    if(unlock_preparing==1) {
        bool cover=false;int display=-1;
        if(computer_remote_display_state(unlock_cover_request,&display)) {
            computer_remote_cover_state(unlock_cover_request,&cover);
            if(!ready||state.busy||display<0){unlock_observation_reset();if(unlock_submit_pending)unlock_fail(4);else unlock_wait(now);}
            // Fresh, consistent samples replace the unconditional six-second
            // opening pause. Final PIN admission still obtains a NEW reply.
            else if(display>0&&!unlock_observed_ready(now,display,cover))unlock_wait(now);
            else if(display==0) {
                unlock_observation_reset();
                if(!unlock_wake_budget){unlock_wake_budget=true;unlock_prepare_deadline=now+12000;}
                if(unlock_display_attempts>=2)unlock_wait(now);
                else {
                    if(unlock_keyboard_display_wake(unlock_cover_request,unlock_connection)){unlock_display_attempts++;unlock_display_inflight=true;unlock_preparing=2;}
                    else unlock_wait(now); // Not accepted: obtain new evidence before another admission attempt.
                }
            }else if(cover) {
                if(!unlock_wake_budget){unlock_wake_budget=true;unlock_prepare_deadline=now+12000;}
                if(unlock_cover_attempted)unlock_wait(now);
                else {
                    if(unlock_keyboard_wake(unlock_cover_request,unlock_connection)){unlock_cover_attempted=true;unlock_display_inflight=false;unlock_preparing=2;}
                    else unlock_wait(now);
                }
            }else {
                unlock_preparing=0;unlock_prepared=true;
                if(unlock_submit_pending) {
                    bool started=unlock_keyboard_start(unlock_pin,unlock_length,unlock_connection,unlock_cover_request);
                    unlock_submit_pending=false;unlock_clear();unlock_show_mask();
                    if(started){unlock_sent=true;unlock_submitted_at=unlock_input_at=now;}else unlock_fail(3);
                }
            }
        }else if(now>=unlock_prepare_until){unlock_observation_reset();if(unlock_submit_pending)unlock_fail(4);else unlock_wait(now);}
    }else if(unlock_preparing==2&&!state.busy) {
        if(state.finished&&!state.failed){
            unlock_woke=true;unlock_preparing=3;
            // Allow Windows to react before asking for new evidence. No cached
            // pre-settle reply can authorize the second Shift.
            unlock_prepare_until=now+(unlock_display_inflight?1500:350);unlock_display_inflight=false;
        }
        else unlock_fail(2);
    }else if((unlock_preparing==3||unlock_preparing==4)&&now>=unlock_prepare_until)unlock_probe(now);
    bool grant_waiting=unlock_auto&&unlock_auto_requested&&!unlock_auto_consumed&&!unlock_sent&&!unlock_submit_pending;
    if(unlock_auto&&!unlock_auto_failed&&!unlock_sent&&!unlock_submit_pending) {
        // The single in-flight grant has its own six-second timeout. A status
        // gap may wait for that same result; never request it again. Consuming
        // it still starts NEW request-bound lock/display preparation before HID.
        if((!peer||(fresh&&!locked)||(!fresh&&!grant_waiting))&&!opening_wait){unlock_record_failure(3);oneclick_cancel();unlock_auto_failed=true;unlock_clear();}
        else if(unlock_prepared&&!unlock_preparing&&!unlock_auto_requested) {
            unlock_auto_requested=true;
            if(!oneclick_start(unlock_cover_request,unlock_keyboard_link(),num(item(shown,"connectionHealth"),"unlockCredentialVersion")==2)){unlock_record_failure(6);unlock_auto_failed=true;}
        }else if(unlock_auto_requested&&!unlock_auto_consumed) {
            int result=oneclick_take(unlock_pin,sizeof(unlock_pin),&unlock_length);
            if(result==2){unlock_auto_consumed=true;unlock_submit_pending=true;unlock_prepare(now,false);}
            else if(result<0){unlock_failure=result<=-2&&result>=-8?10-result:7;unlock_record_failure((unsigned)unlock_failure);unlock_auto_failed=true;unlock_clear();}
        }
    }
    // A key may have just started during this refresh; re-read busy before enabling controls.
    unlock_keyboard_get(&state);
    bool editable=!unlock_auto&&!unlock_sent&&!unlock_preparing&&!state.busy&&ready&&unlock_prepared&&reported>0;
    unlock_enable(unlock_confirm,editable&&unlock_length>=1);
    for(int i=0;i<12;i++)unlock_enable(unlock_keys[i],editable);
    unlock_enable(unlock_text_keys,editable);unlock_enable(unlock_mode_button,editable);
    if(!ready&&!unlock_preparing){
        unlock_clear();unlock_show_mask();if(unlock_host)host_unlock_remote_cancel();else if(unlock_wifi)unlock_remote_cancel();else unlock_keyboard_cancel();unlock_submit_pending=false;
        // A transient HID/status gap gates every local tap, but must not erase
        // completed preparation forever on the same secure, locked peer.
        // Explicit confirmation always obtains NEW display evidence before HID.
        if(unlock_prepared&&(!peer||(fresh&&!locked))){unlock_prepared=false;unlock_prepare_failed=true;unlock_failure=3;}
    }
    if(unlock_sent) {
        bool confirmed=peer&&state.bridge_verified&&fresh&&!locked&&updated>unlock_submitted_at;
        if(confirmed){unlock_success(now);return;}
        if(state.failed)unlock_record_failure(8);
        else if(!state.busy&&now-unlock_submitted_at>=10000)unlock_record_failure(9);
        const char *message=state.failed?"解锁未完成\n请关闭面板后重试":
            state.busy||now-unlock_submitted_at<10000?"正在解锁，请稍候":"未确认解锁\n请关闭面板后重试";
        unlock_status(message,state.failed||(!state.busy&&now-unlock_submitted_at>=10000));
        if(!unlock_auto)ui_text(lv_obj_get_child(unlock_close_button,0),"关闭");
    }else if(!state.connected||!state.secure||!state.subscribed||!state.bridge_verified||!state.ready||state.connection!=unlock_connection||!fresh) {
        if((opening_wait&&unlock_preparing)||(grant_waiting&&!unlock_auto_failed))unlock_status("正在准备，请稍候",false);
        else {unlock_record_failure(5);unlock_status("电脑连接未就绪\n请关闭面板后重试",true);}
    }
    else if(!locked){unlock_success(now);return;}
    else if(unlock_auto_failed)unlock_status(unlock_failure==12?"请在电脑设置 Windows PIN":
        unlock_failure==13?"无法读取 PIN\n请在电脑重新保存":
        unlock_failure==14?"电脑屏幕尚未就绪\n请关闭面板后重试":
        "一键解锁未完成\n请关闭面板后重试",true);
    else if(unlock_preparing)unlock_status(unlock_submit_pending?"正在解锁，请稍候":"正在准备，请稍候",false);
    else if(unlock_prepare_failed)unlock_status("未能准备好电脑\n请关闭面板后重试",true);
    else if(unlock_auto&&!unlock_sent&&unlock_prepared)unlock_status("正在解锁，请稍候",false);
    else if(reported<=0)unlock_status(reported==0?"电脑屏幕尚未就绪\n请关闭面板后重试":"暂时无法确认电脑状态\n请关闭面板后重试",true);
    else unlock_status("请输入 PIN 或密码（不保存）",false);
}
static void unlock_input_key(int key) {
    if(!unlock_overlay||unlock_success_until||unlock_auto||unlock_sent||unlock_pairing_mode||unlock_preparing||!unlock_prepared)return;
    ble_keyboard_state state;unlock_keyboard_get(&state);bool locked=false;int64_t updated=0;
    if(!state.ready||state.connection!=unlock_connection||!computer_remote_lock_state(&locked,&updated)||!locked){unlock_refresh();return;}
    if(key>=0x20&&key<=0x7e&&unlock_length<sizeof(unlock_pin))unlock_pin[unlock_length++]=(char)key;
    else if(key==-1&&unlock_length)unlock_pin[--unlock_length]=0;
    else if(key==-2)unlock_clear();
    unlock_deadline=esp_timer_get_time()/1000+60000;unlock_show_mask();unlock_refresh();
}
static void unlock_key(lv_event_t *event) {
    int key=(int)(intptr_t)lv_event_get_user_data(event);unlock_input_key(key>=0&&key<=9?'0'+key:key);
}
static void unlock_text_key(lv_event_t *event) {
    uint32_t selected=lv_buttonmatrix_get_selected_button(unlock_text_keys);
    if(selected==LV_BUTTONMATRIX_BUTTON_NONE)return;
    const char *text=lv_buttonmatrix_get_button_text(unlock_text_keys,selected);if(!text)return;
    unlock_input_key(!strcmp(text,LV_SYMBOL_BACKSPACE)?-1:!strcmp(text,"Clear")?-2:!strcmp(text,"Space")?' ':(unsigned char)text[0]);
}
static void unlock_mode_change(lv_event_t *event) {
    (void)event;if(unlock_auto||unlock_pairing_mode||unlock_sent||unlock_preparing||!unlock_prepared)return;
    unlock_text_mode=(unlock_text_mode+1)%4;
    const char *next[]={"abc","ABC","#+=","123"};
    ui_text(lv_obj_get_child(unlock_mode_button,0),next[unlock_text_mode]);
    for(int i=0;i<12;i++)visible(unlock_keys[i],unlock_text_mode==0);
    visible(unlock_text_keys,unlock_text_mode!=0);
    if(unlock_text_mode)lv_buttonmatrix_set_map(unlock_text_keys,unlock_text_mode==1?unlock_lower_map:unlock_text_mode==2?unlock_upper_map:unlock_symbol_map);
    unlock_deadline=esp_timer_get_time()/1000+60000;
}
static void unlock_action(lv_event_t *event) {
    if((intptr_t)lv_event_get_user_data(event)==0){unlock_close();return;}
    if(unlock_pairing_mode){ble_keyboard_pair_confirm(true);unlock_pairing_mode=false;unlock_close();return;}
    if(unlock_success_until||unlock_auto||unlock_sent||unlock_preparing||!unlock_prepared||unlock_length<1)return;
    unlock_submit_pending=true;unlock_prepare(esp_timer_get_time()/1000,false);unlock_refresh();
}

static void unlock_open_mode(bool pairing) {
    if(unlock_overlay)return;
    unlock_keyboard_select(pairing);
    bool wifi_ready=!pairing&&unlock_remote_wifi_only()&&num(item(shown,"connectionHealth"),"wifiUnlockVersion")==1;
    unlock_host_link=pairing?LINK_NONE:host_unlock_remote_link();
    // Auto mode retains USB > Wi-Fi > BLE; explicit BLE makes wifi_ready false.
    if(unlock_host_link==LINK_BLE&&wifi_ready)unlock_host_link=LINK_NONE;
    unlock_host=unlock_host_link!=LINK_NONE;if(unlock_host)unlock_using_usb=unlock_host_link==LINK_USB;
    unlock_wifi=!unlock_host&&wifi_ready;oneclick_cancel();unlock_auto=unlock_host||unlock_wifi||(!pairing&&computer_remote_oneclick_enabled());unlock_auto_requested=unlock_auto_consumed=unlock_auto_failed=false;
    unlock_attempt++;unlock_failure_recorded=false;unlock_history_recorded=false;unlock_started=esp_timer_get_time()/1000;unlock_input_at=0;
    ble_keyboard_state state;unlock_keyboard_get(&state);unlock_connection=unlock_wifi?unlock_remote_connection():state.connection;
    bool locked=false;int64_t updated=0;
    unlock_initial_pending=!state.connected||!state.secure||!state.subscribed||!state.bridge_verified||!state.ready||!computer_remote_lock_state(&locked,&updated);
    unlock_clear();unlock_success_until=0;unlock_sent=false;unlock_pairing_mode=pairing;unlock_deadline=esp_timer_get_time()/1000+(pairing?30000:60000);
    unlock_preparing=0;unlock_woke=false;unlock_cover_request[0]=0;
    unlock_prepared=false;unlock_prepare_failed=false;unlock_submit_pending=false;
    if(unlock_host){
        char id[37];codex_uuid(id);unlock_submitted_at=esp_timer_get_time()/1000;unlock_sent=true;
        if(!host_unlock_remote_start(id,unlock_host_link))unlock_auto_failed=true;
    }else if(unlock_wifi){
        char id[37];codex_uuid(id);unlock_submitted_at=esp_timer_get_time()/1000;unlock_sent=true;
        if(!unlock_remote_start(id,unlock_connection))unlock_auto_failed=true;
    }else if(!pairing)unlock_prepare(esp_timer_get_time()/1000,true);
    unlock_overlay=lv_obj_create(lv_screen_active());lv_obj_remove_style_all(unlock_overlay);
    lv_obj_set_size(unlock_overlay,LV_PCT(100),LV_PCT(100));lv_obj_remove_flag(unlock_overlay,LV_OBJ_FLAG_SCROLLABLE);
    ui_bg_color(unlock_overlay,lv_color_black(),0);ui_bg_opa(unlock_overlay,LV_OPA_80,0);
    lv_obj_t *panel=card(unlock_overlay,0,0,700,unlock_auto?270:pairing?360:640);lv_obj_center(panel);lv_obj_set_style_pad_all(panel,0,0);
    char title_text[64];snprintf(title_text,sizeof(title_text),"%s 解锁电脑",unlock_transport_name());
    lv_obj_t *title=label(panel,pairing?(state.repair_pairing?"重新配对":"蓝牙配对"):title_text,32,22,636,false);ui_text_font(title,&tab5_font_30,0);
    unlock_panel=panel;unlock_title=title;
    unlock_hint=muted(panel,pairing?(state.repair_pairing?"电脑请求重新配对，确认将清除此蓝牙配对\n随后请在电脑删除设备并重新添加":"请确认电脑与此处显示的配对码相同\n这是蓝牙配对码，不是 Windows PIN"):"",32,72,636);
    lv_obj_set_height(unlock_hint,66);
    unlock_mask=label(panel,"PIN",32,148,636,false);ui_text_font(unlock_mask,&lv_font_montserrat_40,0);
    lv_obj_set_style_text_align(unlock_mask,LV_TEXT_ALIGN_CENTER,0);
    lv_obj_set_width(unlock_mask,510);
    unlock_text_mode=0;
    unlock_mode_button=button(panel,"abc",564,148,104,unlock_mode_change,0);lv_obj_set_height(unlock_mode_button,52);lv_obj_center(lv_obj_get_child(unlock_mode_button,0));
    visible(unlock_mode_button,!pairing&&!unlock_auto);
    unlock_text_keys=lv_buttonmatrix_create(panel);lv_obj_set_pos(unlock_text_keys,32,216);lv_obj_set_size(unlock_text_keys,636,292);
    ui_bg_opa(unlock_text_keys,LV_OPA_TRANSP,0);lv_obj_set_style_border_width(unlock_text_keys,0,0);
    lv_obj_set_style_pad_all(unlock_text_keys,0,0);lv_obj_set_style_pad_row(unlock_text_keys,10,0);lv_obj_set_style_pad_column(unlock_text_keys,10,0);
    ui_bg_color(unlock_text_keys,lv_color_hex(0x314144),LV_PART_ITEMS);ui_bg_opa(unlock_text_keys,LV_OPA_COVER,LV_PART_ITEMS);
    ui_text_color(unlock_text_keys,lv_color_hex(INK),LV_PART_ITEMS);lv_obj_set_style_radius(unlock_text_keys,12,LV_PART_ITEMS);
    lv_obj_set_style_border_width(unlock_text_keys,0,LV_PART_ITEMS);lv_obj_set_style_shadow_width(unlock_text_keys,0,LV_PART_ITEMS);
    ui_bg_color(unlock_text_keys,lv_color_hex(BLUE),LV_PART_ITEMS|LV_STATE_PRESSED);ui_text_color(unlock_text_keys,lv_color_hex(UI_BACKGROUND),LV_PART_ITEMS|LV_STATE_PRESSED);
    lv_obj_set_style_text_font(unlock_text_keys,&lv_font_montserrat_24,LV_PART_ITEMS);
    lv_obj_add_event_cb(unlock_text_keys,unlock_text_key,LV_EVENT_VALUE_CHANGED,NULL);visible(unlock_text_keys,false);
    const char *captions[]={"1","2","3","4","5","6","7","8","9","清空","0",LV_SYMBOL_BACKSPACE};
    const int values[]={1,2,3,4,5,6,7,8,9,-2,0,-1};
    for(int i=0;i<12;i++) {
        unlock_keys[i]=button(panel,captions[i],32+(i%3)*218,216+(i/3)*76,200,unlock_key,values[i]);
        lv_obj_set_height(unlock_keys[i],64);lv_obj_center(lv_obj_get_child(unlock_keys[i],0));
        if(i!=9)ui_text_font(lv_obj_get_child(unlock_keys[i],0),&lv_font_montserrat_40,0);
        if(pairing||unlock_auto)lv_obj_add_flag(unlock_keys[i],LV_OBJ_FLAG_HIDDEN);
    }
    unlock_close_button=button(panel,"取消",32,unlock_auto?174:pairing?260:542,200,unlock_action,0);lv_obj_set_height(unlock_close_button,64);lv_obj_center(lv_obj_get_child(unlock_close_button,0));
    unlock_confirm=button(panel,pairing?(state.repair_pairing?"确认重新配对":"确认相同"):"确认解锁",250,unlock_auto?174:pairing?260:542,418,unlock_action,1);lv_obj_set_height(unlock_confirm,64);lv_obj_center(lv_obj_get_child(unlock_confirm,0));
    ui_bg_color(unlock_confirm,lv_color_hex(BLUE),0);ui_text_color(lv_obj_get_child(unlock_confirm,0),lv_color_hex(UI_BACKGROUND),0);
    unlock_status_face=shape(panel,224,36,72,72,GREEN,LV_RADIUS_CIRCLE);ui_bg_opa(unlock_status_face,LV_OPA_10,0);
    unlock_status_symbol=label(unlock_status_face,LV_SYMBOL_OK,0,0,60,false);ui_text_font(unlock_status_symbol,&lv_font_montserrat_40,0);
    lv_obj_set_style_text_align(unlock_status_symbol,LV_TEXT_ALIGN_CENTER,0);lv_obj_center(unlock_status_symbol);visible(unlock_status_face,false);
    unlock_spinner=lv_spinner_create(panel);lv_obj_set_pos(unlock_spinner,224,36);lv_obj_set_size(unlock_spinner,72,72);
    lv_obj_set_style_arc_width(unlock_spinner,5,LV_PART_MAIN);lv_obj_set_style_arc_width(unlock_spinner,5,LV_PART_INDICATOR);
    lv_obj_set_style_arc_color(unlock_spinner,lv_color_hex(BLUE),LV_PART_MAIN);lv_obj_set_style_arc_color(unlock_spinner,lv_color_hex(BLUE),LV_PART_INDICATOR);
    lv_obj_set_style_arc_opa(unlock_spinner,LV_OPA_20,LV_PART_MAIN);lv_obj_remove_flag(unlock_spinner,LV_OBJ_FLAG_CLICKABLE);
    lv_anim_delete(unlock_spinner,NULL);unlock_spinning=false;visible(unlock_spinner,false);
    if(unlock_auto)unlock_status_layout(false);
    unlock_refresh();
}
static void unlock_open(void){unlock_open_mode(false);}
