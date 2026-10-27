#include "assets.h"
#include "battery_status.h"
#include "crash_record.h"
#include <stdio.h>
#include <string.h>
#ifdef TAB5_UI_PREVIEW
static battery_status preview_battery={true,false,76,7816,-380};
static battery_status battery_status_get_preview(void){return preview_battery;}
#define battery_status_get battery_status_get_preview
static bool preview_ota_available,preview_ota_busy,preview_ota_completed,preview_ota_presented,preview_ota_preparation_presented,preview_ota_verifying;
static bool ota_update_available(void){return preview_ota_available;}
static bool ota_update_busy(void){return preview_ota_busy;}
static bool ota_update_completed(void){return preview_ota_completed;}
static bool ota_update_verifying(void){return preview_ota_verifying&&!preview_ota_completed;}
static int ota_update_progress(void){return preview_ota_completed?100:preview_ota_verifying?99:35;}
static void ota_update_completion_presented(void){preview_ota_presented=true;}
static void ota_update_preparation_presented(void){preview_ota_preparation_presented=true;}
static void ota_update_target_version(char *out,size_t size){snprintf(out,size,"0.2.24-ui");}
static const char *preview_ota_notes="修复升级中途蓝屏重启\n优化固件传输与写入速度\n升级页新增版本更新说明";
static const char *preview_ota_idle="等待电脑提供固件";
static const char *preview_ota_transfer_status="正在写入固件";
static void ota_update_offer_details(char *version,size_t vs,char *notes,size_t ns){snprintf(version,vs,"%s",preview_ota_available?"0.2.30-ui":"");snprintf(notes,ns,"%s",preview_ota_available?preview_ota_notes:"");}
static unsigned preview_ota_starts;
static bool ota_update_offer_matches(const char *offer,const char *sha){return preview_ota_available&&!strcmp(offer,"11111111111111111111111111111111")&&!strcmp(sha,"2222222222222222222222222222222222222222222222222222222222222222");}
static bool ota_update_request(void){if(!preview_ota_available)return false;preview_ota_starts++;preview_ota_busy=true;return true;}
static bool ota_update_request_checked(const char *offer,const char *sha){return ((!offer&&!sha)||ota_update_offer_matches(offer,sha))&&ota_update_request();}
static void ota_update_status(char *out,size_t size){snprintf(out,size,"%s",preview_ota_completed?"3 秒后自动重启":preview_ota_verifying?"正在校验固件 · 0.8 秒":preview_ota_busy?preview_ota_transfer_status:preview_ota_available?"新固件 0.2.24-ui":preview_ota_idle);}
static bool preview_flash_busy;static unsigned preview_flash_starts;
static const char *preview_flash_status="测量擦除及纯写入速度，不包含 USB 或 Wi-Fi 传输。\n仅测试备用分区末尾空白区，完成后恢复并核验。";
static bool flash_benchmark_busy(void){return preview_flash_busy;}
static bool flash_benchmark_start(void){if(preview_flash_busy||preview_ota_busy)return false;preview_flash_busy=true;preview_flash_starts++;return true;}
static void flash_benchmark_cancel(void){preview_flash_status="已请求停止，将先恢复测试区。";}
static void flash_benchmark_status(char *out,size_t size){snprintf(out,size,"%s",preview_flash_status);}
#include "ui_preview_stubs.h"
static bool rpc_link_ready(void);
static bool rpc_read_link_ready(void);
#else
#include "bsp/m5stack_tab5.h"
#include "device.h"
#include "ble_voice.h"
#include "rpc_link.h"
#include "display_settings.h"
#include "esp_timer.h"
#include "esp_heap_caps.h"
#include "esp_random.h"
#include "esp_app_desc.h"
#include "flash_benchmark.h"
#endif
#ifndef TAB5_UI_PREVIEW
#include "ota_remote.h"
#include "runtime_diagnostics.h"
#include "screen_preview.h"
#endif
#include "ui_prefs.h"
#include "trusted_hosts.h"
#include "ui_orientation.h"
static ui_orientation_filter screen_orientation={.candidate=3};
static int screen_orientation_mode;
static lv_obj_t *orientation_selector;
#include "ui_startup_home.h"
#include "ui_time.h"
#include "gallery_clock.h"
#include "agenda.h"
#ifdef TAB5_UI_PREVIEW
static agenda_state preview_agenda;
static bool preview_agenda_save_fail;
void agenda_get(agenda_state *out){*out=preview_agenda;}
void agenda_offer(const char *revision){(void)revision;}
uint32_t agenda_action(const char *id,const char *edit,int64_t occurrence,bool complete,int64_t until) {
    if(preview_agenda_save_fail){preview_agenda.completed_request++;preview_agenda.last_success=false;strcpy(preview_agenda.message,"保存失败，请检查设备存储后重试");return preview_agenda.completed_request;}
    for(unsigned i=0;i<preview_agenda.document.count;i++){agenda_item *item=&preview_agenda.document.items[i];if(!strcmp(id,item->id)&&!strcmp(edit,item->edit)&&agenda_apply(item,occurrence,complete,until)){preview_agenda.generation++;preview_agenda.completed_request++;preview_agenda.last_success=true;strcpy(preview_agenda.message,complete?"已完成本次，连接后同步电脑":"已保存，10 分钟后提醒");return preview_agenda.completed_request;}}return 0;
}
static bool preview_agenda_notice;
uint32_t agenda_save(const agenda_item *definition,const char *expected_edit) {
    uint32_t request=++preview_agenda.completed_request;preview_agenda.last_success=false;
    if(preview_agenda_save_fail){strcpy(preview_agenda.message,"保存失败，输入内容已保留，请重试");return request;}
    agenda_document next=preview_agenda.document;unsigned index=next.count;for(unsigned n=0;n<next.count;n++)if(!strcmp(next.items[n].id,definition->id)){index=n;break;}
    if(index==next.count&&next.count==AGENDA_LIMIT){strcpy(preview_agenda.message,"最多保存 16 项");return request;}
    if(index<next.count&&strcmp(next.items[index].edit,expected_edit)){strcpy(preview_agenda.message,"事项已变化，请重新打开编辑");return request;}
    agenda_item value=*definition;if(!value.id[0])snprintf(value.id,sizeof(value.id),"%032x",request+200);snprintf(value.edit,sizeof(value.edit),"%032x",request+1000);value.dirty=4;
    next.items[index]=value;if(index==next.count)next.count++;
    if(!agenda_document_valid(&next)){strcpy(preview_agenda.message,"请检查名称及时间");return request;}
    preview_agenda.document=next;strcpy(preview_agenda.saved_id,value.id);preview_agenda.pending=true;preview_agenda.last_success=true;strcpy(preview_agenda.message,"已保存到本机");return request;
}
bool agenda_alert(int64_t now,char title[97],char id[33],int64_t *occurrence){(void)now;if(!preview_agenda_notice)return false;preview_agenda_notice=false;strcpy(title,preview_agenda.document.items[0].title);strcpy(id,preview_agenda.document.items[0].id);*occurrence=preview_agenda.document.items[0].due;return true;}
uint32_t agenda_remove(const char *id,const char *edit) {
    uint32_t request=++preview_agenda.completed_request;preview_agenda.last_success=false;
    if(preview_agenda_save_fail){strcpy(preview_agenda.message,"删除保存失败，原事项仍保留，请重试");return request;}
    for(unsigned n=0;n<preview_agenda.document.count;n++)if(!strcmp(preview_agenda.document.items[n].id,id)&&!strcmp(preview_agenda.document.items[n].edit,edit)) {
        agenda_document *d=&preview_agenda.document;memmove(&d->items[n],&d->items[n+1],(d->count-n-1)*sizeof(d->items[0]));memset(&d->items[--d->count],0,sizeof(d->items[0]));
        preview_agenda.pending=true;preview_agenda.last_success=true;strcpy(preview_agenda.message,"已从 TAB5 删除，连接后同步电脑");return request;
    }
    strcpy(preview_agenda.message,"事项已变化，请重新选择后删除");return request;
}
static bool preview_gallery_clock_valid;
static struct tm preview_gallery_clock_time;
void gallery_clock_sync(int64_t local_ms,int64_t now_ms){(void)local_ms;(void)now_ms;}
bool gallery_clock_local(int64_t now_ms,struct tm *out){(void)now_ms;if(!preview_gallery_clock_valid)return false;*out=preview_gallery_clock_time;return true;}
#endif
#include "reply_document.h"
#include "draw_cache.h"
#include "src/misc/lv_text_private.h"
#include "wifi_profiles.h"
#include "wifi_status.h"
#include "voice_remote.h"
#ifndef TAB5_UI_PREVIEW
extern volatile int tab5_voice_start_error;
#endif
#include "camera_preview.h"
#ifndef TAB5_UI_PREVIEW
#include "camera_viewport.h"
#endif
#include "photo_attachment.h"
#ifdef TAB5_UI_PREVIEW
static int preview_tf_state=1;
static int sd_storage_state(void){return preview_tf_state;}
#else
#include "sd_storage.h"
#endif
#ifndef TAB5_UI_PREVIEW
#include "ota_update.h"
#endif
#include <stdio.h>
#include <string.h>
// A 1220x516 home viewport fits in one 1280x512 PSRAM buffer. Its rotated
// snapshots can then be copied contiguously instead of walking every column
// twice. Keep the same double-buffer ownership and RGB565 alignment.
#define TAB5_DRAW_STRIP_ROWS 512
#define TAB5_DRAW_BUFFER_PIXELS (1280 * TAB5_DRAW_STRIP_ROWS)
#ifndef TAB5_UI_PREVIEW
#include "codex_remote.h"
#include "tab5_keyboard.h"
#else
static voice_remote_state preview_voice;
static unsigned preview_chimes;
static unsigned preview_health_chimes;
static unsigned preview_timer_chimes;
static unsigned preview_agenda_chimes;
void voice_remote_notify_reply(void) {preview_chimes++;}
void voice_remote_notify_health(void) {preview_health_chimes++;}
void voice_remote_notify_timer(void) {preview_timer_chimes++;}
void voice_remote_notify_agenda(void) {preview_agenda_chimes++;}
bool voice_remote_preview_sound(void) {return !preview_voice.busy&&device_sound_volume()>0;}
static bool preview_voice_start_enabled;
static unsigned preview_voice_starts;
bool voice_remote_start(const char *id,const char *session,int64_t epoch) {
    (void)session;(void)epoch;
    if(!preview_voice_start_enabled)return false;
    preview_voice_starts++;
    memset(&preview_voice,0,sizeof(preview_voice));
    snprintf(preview_voice.task_id,sizeof(preview_voice.task_id),"%s",id);
    strcpy(preview_voice.phase,"preparing");preview_voice.busy=true;return true;
}
static unsigned preview_voice_commands;
bool voice_remote_start_tab5(const char *id,const char *session,int64_t epoch){return voice_remote_start(id,session,epoch);}
static int preview_voice_command;
void voice_remote_command(int cmd) {
    preview_voice_commands++;preview_voice_command=cmd;
    if((cmd==1||cmd==2)&&preview_voice.busy) {
        preview_voice.ready=false;preview_voice.level=0;preview_voice.silent=false;
        strcpy(preview_voice.phase,"draining");strcpy(preview_voice.message,cmd==2?"正在取消录音…":"正在结束收音…");
    }
}
void voice_remote_get(voice_remote_state *out) { *out=preview_voice; }
static bool preview_submit_enabled;
static wifi_profile_store preview_networks={.version=WIFI_PROFILE_VERSION,.count=2,.entries={{.ssid="家庭网络"},{.ssid="办公室"}}};
static bool preview_forget_ok=true,preview_usb_connected;
static int preview_host_selected;
static char preview_host_labels[2][TRUSTED_HOST_NAME]={"Windows 笔记本","MacBook"};
static bool preview_host_save_ok=true;
int device_host_names(char names[TRUSTED_HOST_LIMIT][TRUSTED_HOST_NAME],int *selected){memcpy(names,preview_host_labels,sizeof(preview_host_labels));*selected=preview_host_selected;return 2;}
bool device_host_label(int index,char id[33],char name[TRUSTED_HOST_NAME]){if(index<0||index>1)return false;strcpy(id,index?"22222222222222222222222222222222":"11111111111111111111111111111111");strcpy(name,preview_host_labels[index]);return true;}
void device_host_identity(char id[33],char name[TRUSTED_HOST_NAME]){device_host_label(preview_host_selected,id,name);}
void device_host_status(char id[33],char name[TRUSTED_HOST_NAME],link_kind *link){device_host_identity(id,name);*link=link_state_active(&device_links,esp_timer_get_time()/1000);}
bool device_host_rename(const char *id,const char *name){int index=!strcmp(id,"11111111111111111111111111111111")?0:!strcmp(id,"22222222222222222222222222222222")?1:-1;if(index<0||!preview_host_save_ok||(*name&&!trusted_host_name_valid(name)))return false;snprintf(preview_host_labels[index],TRUSTED_HOST_NAME,"%s",*name?name:index?"MacBook":"Windows 笔记本");return true;}
bool device_host_usb_owned(void){return preview_usb_connected;}
bool device_host_select(int index){if(index<0||index>1||(preview_usb_connected&&index!=preview_host_selected))return false;if(index!=preview_host_selected){int mode=device_links.mode;link_state_init(&device_links);device_links.mode=mode;}preview_host_selected=index;return true;}

int device_wifi_names(char names[WIFI_PROFILE_LIMIT][33]) {for(unsigned i=0;i<preview_networks.count;i++)strcpy(names[i],preview_networks.entries[i].ssid);return preview_networks.count;}
int device_wifi_count(void){return preview_networks.count;}
void device_wifi_label(const char *ssid,char label[WIFI_LABEL_SIZE]){int at=wifi_profiles_index(&preview_networks,ssid);snprintf(label,WIFI_LABEL_SIZE,"%s",at<0?"":preview_networks.entries[at].label);}
bool device_wifi_select(int index,bool persist){if(index<0||index>=(int)preview_networks.count)return false;if(persist)preview_networks.preferred=index;strcpy(preview_config.ssid,preview_networks.entries[index].ssid);return true;}
bool device_wifi_forget(const char *ssid){if(!preview_forget_ok||!wifi_profiles_forget(&preview_networks,ssid))return false;wifi_profiles_sync_active(&preview_networks,preview_config.ssid,preview_config.password);return true;}
static int preview_wifi_diagnostic_case;
void tab5_wifi_status_read(tab5_wifi_status *out) {
    int64_t now=esp_timer_get_time()/1000,received=device_links.received_ms[LINK_WIFI];
    out->connected=preview_wifi_diagnostic_case!=1&&preview_wifi_diagnostic_case!=2;
    out->authenticated=out->connected&&received>=0&&now>=received&&now-received<8000&&!preview_wifi_diagnostic_case;
    snprintf(out->network,sizeof(out->network),"%s",preview_wifi_diagnostic_case==1?"尚未配置":preview_wifi_diagnostic_case==5?"工作室与家庭的无线网络备注 · Long-WiFi-Network-0123456789ABCDEF":"家庭网络");
    snprintf(out->address,sizeof(out->address),"%s",out->connected?"192.168.1.20":"尚未分配");
    out->state=preview_wifi_diagnostic_case==1?"请在电脑添加 Wi-Fi":preview_wifi_diagnostic_case==2?"认证失败，请检查密码":"已连接";
    out->bridge=!out->connected?"等待 Wi-Fi":out->authenticated?"正常 · 鉴权通过":preview_wifi_diagnostic_case==3?"正在等待电脑响应":preview_wifi_diagnostic_case==4?"暂不可达，正在寻找电脑":"正在连接所选电脑";
}
void tab5_wifi_diagnostics(char *out,size_t size){tab5_wifi_status status;tab5_wifi_status_read(&status);snprintf(out,size,"Wi-Fi  %s\n%s\nIP  %s\n电脑桥接  %s",status.network,status.state,status.address,status.bridge);}
void tab5_wifi_reconnect(void){}
static int preview_submit_count,preview_result_code;
static const char *preview_result_text="";
static bool codex_remote_submit(const char *id, const char *message, const char *session, int64_t issued_at) { (void)id; (void)message; (void)session; (void)issued_at; if(preview_submit_enabled)preview_submit_count++;return preview_submit_enabled; }
static int codex_remote_status(const char *id, char *out, unsigned size) { (void)id; snprintf(out, size, "%s", preview_result_text); return preview_result_code; }
static int preview_reads,preview_read_page;
static bool codex_remote_read_page(const char *id,const char *session,int64_t epoch,int page) {(void)id;(void)session;(void)epoch;preview_reads++;preview_read_page=page;return true;}
static char preview_read_turn[64];static int preview_read_offset;
static bool codex_remote_read_turn(const char *id,const char *session,int64_t epoch,int page,const char *turn,int offset) {snprintf(preview_read_turn,sizeof(preview_read_turn),"%s",turn);preview_read_offset=offset;return codex_remote_read_page(id,session,epoch,page);}
static cJSON *preview_read_view;
static cJSON *codex_remote_take_read_view(void){cJSON *v=preview_read_view;preview_read_view=NULL;return v;}
static const char *preview_result_turn="";
static void codex_remote_read_result(char *out,unsigned size){snprintf(out,size,"%s",preview_result_turn);}
static char preview_operation[16],preview_request_id[37];
static int preview_sync_count;
static bool codex_remote_request(const char *op,const char *id,const char *message,const char *session,int64_t epoch,const char *request,const char *turn) {
    (void)turn;snprintf(preview_operation,sizeof(preview_operation),"%s",op);snprintf(preview_request_id,sizeof(preview_request_id),"%s",request);
    if(!strcmp(op,"send")||!strcmp(op,"steer")||!strcmp(op,"interrupt"))return codex_remote_submit(id,message,session,epoch);
    preview_sync_count++;return preview_submit_enabled;
}
static bool codex_remote_with_images(const char *op,const char *id,const char *message,const char *session,int64_t epoch,const char *request,const char *turn,const char images[3][37]){(void)images;return codex_remote_request(op,id,message,session,epoch,request,turn);}
static bool preview_photo_fixture;
static unsigned preview_photo_uploads;
static char preview_photo_upload_id[37];
static uint16_t preview_photo_pixels[PHOTO_PREVIEW_W*PHOTO_PREVIEW_H];
static unsigned preview_photo_width=PHOTO_PREVIEW_W,preview_photo_height=PHOTO_PREVIEW_H;
static const uint8_t preview_photo_jpeg[8]={0xff,0xd8,1,2,3,4,0xff,0xd9};
static bool codex_remote_upload(const char *id,const char *session,int64_t epoch,const char *image_id,const uint8_t *data,size_t size){
    (void)id;(void)session;(void)epoch;
    if(!preview_photo_fixture||data!=preview_photo_jpeg||size!=sizeof(preview_photo_jpeg))return false;
    snprintf(preview_photo_upload_id,sizeof(preview_photo_upload_id),"%s",image_id);preview_photo_uploads++;return true;
}
static int preview_photo_state,preview_photo_prepares;
bool photo_prepare_camera(void){preview_photo_prepares++;preview_photo_state=1;return true;}
bool photo_prepare_file(const char *path){(void)path;return false;}
bool photo_list_directory(const char *path,int page){(void)path;(void)page;return false;}
const photo_listing *photo_get_listing(void){return NULL;}
int photo_state(void){return preview_photo_state;}
const char *photo_error(void){return "预览";}
const uint8_t *photo_data(size_t *size){*size=preview_photo_fixture?sizeof(preview_photo_jpeg):0;return preview_photo_fixture?preview_photo_jpeg:NULL;}
const uint16_t *photo_preview(void){return preview_photo_fixture&&preview_photo_state==2?preview_photo_pixels:NULL;}
uint16_t *photo_take_preview(void){
    if(!photo_preview())return NULL;
    uint16_t *pixels=malloc(sizeof(preview_photo_pixels));
    if(pixels)memcpy(pixels,preview_photo_pixels,sizeof(preview_photo_pixels));
    return pixels;
}
void photo_preview_dimensions(unsigned *width,unsigned *height){*width=preview_photo_width;*height=preview_photo_height;}
void photo_discard(void){if(preview_photo_fixture)preview_photo_state=0;}
static bool tab5_keyboard_present(void) { return false; }
static bool tab5_keyboard_poll(char out[10], uint8_t *modifier) { (void)out; (void)modifier; return false; }
static camera_preview_state preview_camera_state=CAMERA_PREVIEW_ERROR;
static unsigned preview_camera_starts;
static unsigned preview_camera_cache_bytes;
static void (*preview_camera_start_observer)(void);
static bool preview_camera_busy;
static bool (*preview_camera_orientation_handler)(int);
static int preview_camera_direction;
void camera_preview_set_orientation_handler(bool (*apply)(int)){preview_camera_orientation_handler=apply;}
void camera_preview_request_orientation(int direction){preview_camera_direction=direction;}
bool camera_preview_start(lv_obj_t *canvas) { lv_obj_add_flag(canvas,LV_OBJ_FLAG_HIDDEN);if(preview_camera_start_observer)preview_camera_start_observer();preview_camera_cache_bytes=tab5_cache_bytes+tab5_native_cache_bytes;preview_camera_starts++;preview_camera_state=CAMERA_PREVIEW_STARTING; return true; }
bool camera_preview_capture(void) { if(preview_camera_state!=CAMERA_PREVIEW_LIVE)return false;preview_camera_state=CAMERA_PREVIEW_CAPTURING;return true; }
void camera_preview_stop(void) { preview_camera_state=CAMERA_PREVIEW_OFF; }
camera_preview_state camera_preview_get_state(void) { return preview_camera_state; }
bool camera_preview_busy(void){return preview_camera_busy;}
const char *camera_preview_error(void){return "相机初始化失败，请重启设备后重试 (3/257)";}
#endif

#include "lvgl.h"
#include "src/core/lv_obj_event_private.h"
#include "camera_canvas.h"
#include "src/misc/lv_area_private.h"
#include "tab5_ime.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <stdatomic.h>
#include <stdarg.h>
#include <math.h>
#include "ui_style.h"
#if defined(__GNUC__) && !defined(__clang__)
#define UI_INIT_SMALL __attribute__((optimize("Os")))
#else
#define UI_INIT_SMALL
#endif
// Snapshot copies and text assembly are UI task data, not DMA/ISR state.
// Keep distinct slots (callbacks may nest) but place the storage in PSRAM.
typedef struct {
    voice_remote_state codex_tick,camera,agenda_action,agenda_tick;
    voice_remote_state date_active,date_action,date_tick;
    voice_remote_state quick_stop,quick_start,quick_cancel,quick_render;
    char codex_joined[2001],date_joined[2097];
} ui_voice_workspace;
static ui_voice_workspace *ui_voice_work;
static lv_obj_t *codex_receipt,*codex_voice_hint;
// Avoid invalidating unchanged labels on every transport heartbeat.
static void ui_text(lv_obj_t *object,const char *value) {
    // Composer feedback shares one footer. A new operation replaces the
    // previous message, including voice review left behind before a photo.
    if(value[0]) {
        lv_obj_t *previous=object==codex_receipt?codex_voice_hint:object==codex_voice_hint?codex_receipt:NULL;
        if(previous&&lv_label_get_text(previous)[0])lv_label_set_text(previous,"");
    }
    if(strcmp(lv_label_get_text(object),value))lv_label_set_text(object,value);
}
static void ui_text_fmt(lv_obj_t *object,const char *format,...) {
    char local[512];va_list args,copy;va_start(args,format);va_copy(copy,args);
    int size=vsnprintf(local,sizeof(local),format,args);va_end(args);
    if(size>=0&&(size_t)size<sizeof(local))ui_text(object,local);
    else if(size>=0) {
        char *full=malloc((size_t)size+1);
        if(full){vsnprintf(full,(size_t)size+1,format,copy);ui_text(object,full);free(full);}
    }
    va_end(copy);
}
volatile unsigned tab5_backlight_changes;
volatile int tab5_backlight_percent;
static void set_backlight(int value) {
    if(bsp_display_brightness_set(value)==ESP_OK){tab5_backlight_percent=value;tab5_backlight_changes++;}
}
static void first_frame_light(lv_timer_t *timer) {
    set_backlight((int)(intptr_t)lv_timer_get_user_data(timer));
}
static void first_frame_ready(lv_event_t *event) {
    lv_display_t *display=lv_event_get_target(event);
    lv_display_remove_event_cb_with_user_data(display,first_frame_ready,NULL);
    int brightness=device_get_config().brightness;
    // The final partial flush can still be in flight at REFR_READY. Allow it
    // and a full panel scan to finish before restoring the saved brightness.
    lv_timer_t *timer=lv_timer_create(first_frame_light,100,(void *)(intptr_t)brightness);
    if(timer)lv_timer_set_repeat_count(timer,1);
    else set_backlight(brightness);
}
// Sample the heartbeat before the clock: another core may publish a newer
// heartbeat during the timer read, which must not wrap a healthy age to UINT_MAX.
static atomic_uint ui_heartbeat_ms;
unsigned tab5_ui_age_ms(void) {unsigned heartbeat=atomic_load(&ui_heartbeat_ms);return (unsigned)(esp_timer_get_time()/1000)-heartbeat;}
LV_FONT_DECLARE(tab5_font_24);
LV_FONT_DECLARE(tab5_countdown_24);
LV_FONT_DECLARE(tab5_font_30);
LV_FONT_DECLARE(tab5_digits_72);
LV_FONT_DECLARE(tab5_digits_96);
static lv_obj_t *workbench_nav[4],*workbench_title,*workbench_back,*settings_button;
static lv_obj_t *clock_segments;
static lv_obj_t *pages[15],*detail_layer,*settings_content,*settings_scroll,*quota_picker;
static bool health_open;
static lv_obj_t *health_header_entry,*health_segments,*health_system_entry;
static void health_segments_refresh(void);
static void health_leave_system(void);
#define SETTINGS_CATEGORIES 6
#define SETTINGS_STORAGE 5
static lv_obj_t *settings_panels[SETTINGS_CATEGORIES],*settings_categories[SETTINGS_CATEGORIES],*cycle_checks[8],*device_info_link;
static lv_obj_t *remote_brightness,*brightness_value,*remote_interval,*remote_saver,*remote_alerts;
static lv_obj_t *sound_slider,*sound_value,*sound_mute,*sound_hint,*sound_test;
static const int primary_pages[]={0,8,1,11};
static int primary_index,settings_return,detail_return=11;
static void workbench_go(lv_event_t *e);
static void overview_open(lv_event_t *e);
static void calendar_render(void);
static void clock_picker_close(lv_event_t *e);
static void workbench_return(lv_event_t *e);
static int clock_return_page=-1,clock_return_detail,clock_return_settings;
static void visible(lv_obj_t *o,bool show);
static void display_controls_refresh(void);
static const char *names[] = {"总览", "模型额度", "天气", "行情", "系统", "音乐", "桌宠", "时钟", "Codex 任务", "设置", "应用", "应用","日历","日程","控制"};
static lv_obj_t *tabs, *clock_label, *banner, *weather, *stocks, *music, *progress, *clock_big,
    *date_label, *setup_values[4], *setup_checks[4], *save_status, *order_rows[8], *pet_labels[2];
static lv_obj_t *display_choices[3],*display_fixed_page,*display_mode_hint,*display_cycle_options,*display_fixed_options,*display_home_options;
static int display_fixed_choice;
static lv_obj_t *chart, *images[3], *image_loading[3], *music_cover;
static lv_chart_series_t *upload, *download;
static asset_image pictures[3];
static lv_image_dsc_t descriptors[3];
static int last_frame[] = {-1, -1, -1};
static ui_preferences prefs;
static cJSON *shown;
static link_kind last_link = LINK_NONE;
static int64_t last_received, last_change, alert_until, offline_since = -1, pet_elapsed[2], last_tick;
static lv_obj_t *alert_dot;
static int64_t alert_started;
static bool alert_attention;
static int alert_phase=-1;
static uint64_t completion[2], metric_seq;
static char metric_session[80];
static bool waiting[2];
static int current_page, selected_quota = 1;
static ui_startup_home startup_home;
static bool changing_page, saver, offline_saver;
static lv_obj_t *codex_list, *codex_project_picker, *codex_input, *codex_keyboard, *codex_hint, *codex_physical, *codex_send_button;
static lv_obj_t *codex_reply_button,*codex_reply_overlay,*codex_reply_text,*codex_reply_status,*codex_reply_scroll;
static lv_obj_t *codex_reply_document;
static lv_obj_t *network_window,*network_details,*network_picker,*network_feedback,*network_retry;
static lv_obj_t *network_diag_window,*network_diag_feedback,*network_wifi_card,*network_wifi_state,*network_wifi_name;
static lv_obj_t *network_diag_state,*network_diag_name,*network_diag_ip,*network_diag_host;
static lv_obj_t *host_picker,*host_current,*host_state,*host_card,*host_monitor,*host_badge,*host_symbol,*host_switch,*host_rename;
static lv_obj_t *host_editor,*host_editor_input,*host_editor_hint,*host_editor_keyboard,*host_editor_ime;
static char host_display_id[33],host_notice[128],host_editor_id[33];
static int host_editor_index;
static int64_t host_connect_started;
static void host_editor_close(void);
static lv_obj_t *network_connect,*network_forget,*network_confirm,*network_confirm_text,*network_confirm_yes,*network_confirm_no;
static char network_options[WIFI_PROFILE_LIMIT][33],network_forget_ssid[33];
static int network_option_count;
static lv_obj_t *ota_button,*ota_hint,*ota_layer,*ota_progress,*ota_title,*ota_note;
static lv_obj_t *flash_entry,*flash_layer,*flash_details,*flash_start,*flash_cancel,*flash_close;
static bool flash_feedback_pinned;
static lv_obj_t *ota_success_icon,*ota_version,*ota_version_track;
static lv_obj_t *ota_offer_card,*ota_offer_title,*ota_badge,*ota_notes_box,*ota_notes;
static char ota_notes_cached[1025];
static lv_obj_t *link_buttons[3],*link_symbols[3];
static int link_visual_state[3]={-1,-1,-1};
#ifdef TAB5_UI_PREVIEW
static bool ble_connected;
static bool rpc_link_ready(void){return wifi_connected||ble_connected||preview_usb_connected;}
static link_kind preview_rpc_selection=LINK_NONE;
static link_kind rpc_link_select(void){return preview_rpc_selection!=LINK_NONE?preview_rpc_selection:preview_usb_connected?LINK_USB:LINK_NONE;}
static bool preview_read_recent;
static bool rpc_read_link_ready(void){return rpc_link_ready()&&(preview_read_recent||esp_timer_get_time()/1000-last_received<=6000);}
static link_kind rpc_ota_link_select(void){return wifi_connected?LINK_WIFI:preview_usb_connected?LINK_USB:ble_connected?LINK_BLE:LINK_NONE;}
// Preview has no live GATT peer.
static bool ble_voice_ready(void){return false;}
static bool tab5_usb_connected(void){return preview_usb_connected;}
#else
extern bool tab5_usb_connected(void);
#endif
static lv_obj_t *battery_label,*battery_outline,*battery_fill,*battery_cap,*battery_charge;
static lv_obj_t *sound_header_button,*sound_header_icon;
static bool quick_visible;
static lv_obj_t *codex_run_badge;
static bool ota_completion_render_pending,ota_completion_rendered;
static int64_t network_retry_until;
typedef ui_reading_bookmark codex_bookmark;
static codex_bookmark *codex_bookmarks;
static unsigned codex_bookmark_stamp;
static bool codex_bookmarks_dirty;
static int codex_restore_y=-1;
static bool codex_open_latest;
static void codex_bookmark_save(void);
static lv_obj_t *codex_interrupt_button;
static lv_obj_t *codex_reply_older,*codex_reply_newer,*codex_reply_latest,*codex_reply_position,*codex_unread_dot,*codex_unread_count;
static int codex_reply_page=-1,codex_reply_total=1;
static lv_obj_t *codex_turn_older,*codex_turn_newer;
static char codex_reply_selected[64],codex_reply_expected[64];
static int codex_turn_offset;
static bool codex_turn_pending;
static cJSON *codex_inline_reply;
typedef struct {cJSON *value;unsigned stamp;bool latest;} codex_cached_page;
#define CODEX_CACHE_PAGES 32
static codex_cached_page codex_reply_cache[CODEX_CACHE_PAGES];
typedef struct {char task[37],from[64],to[64];int direction;unsigned stamp;} codex_cached_edge;
static codex_cached_edge codex_turn_cache[32];
static char codex_read_from_turn[64];
static int codex_read_turn_offset;
static unsigned codex_cache_stamp;
static char codex_cache_session[40];
static bool codex_showing_cache,codex_read_cache_only;
static bool codex_prefetch_armed;
static unsigned codex_prefetch_revision;
static int64_t codex_prefetch_due,codex_prefetch_attempted_at;
static char codex_prefetch_task[37],codex_prefetch_boundary[64],codex_prefetch_session[40];
static bool codex_nav_queued,codex_nav_pending;
static unsigned codex_nav_revision,codex_read_revision;
static int codex_requested_page=-1;
static bool codex_reply_follow=true,codex_reply_jump;
static int64_t codex_read_next,codex_nav_started;
static bool codex_nav_timed_out;
static char codex_read_session[40];
static char codex_reply_turn[64],codex_alert_task[37];
typedef struct {char task[37],turn[64];bool unread,desktop_unread_seen;unsigned arrived;} codex_notice;
static unsigned codex_notice_sequence;
static codex_notice *codex_notices;
static bool codex_notices_ready;
static void codex_notice_tick(int64_t now,bool changed);
static void codex_notice_read(void);
#ifdef TAB5_UI_PREVIEW
static unsigned preview_read_acks;
static char preview_read_receipt[37];
void codex_read_acknowledge(const char *task,const char *turn,const char *receipt,const char *session) {
    (void)task;(void)turn;(void)session;
    if(strlen(receipt)==36&&strcmp(preview_read_receipt,receipt)){strcpy(preview_read_receipt,receipt);preview_read_acks++;}
}
#else
#include "codex_read_ack.h"
#endif
static void codex_notice_style(void);
static bool codex_reading;
static bool codex_result_is_read;
static char codex_read_task[37];
static lv_obj_t *codex_ime_panel;
static void codex_keyboard_done(lv_event_t *e);
static bool codex_waiting;
static bool codex_feedback_pinned,codex_submit_accepted;
static char codex_receipt_before_send[96];
static bool codex_voice_waiting;
static bool codex_voice_desktop;
static bool codex_voice_start_pending,codex_voice_start_desktop;
static char codex_voice_start_task[37],codex_voice_start_session[40],codex_voice_start_note[128];
static int codex_voice_start_page;
static int64_t codex_voice_start_at;
static void quick_voice_started(void);
static void quick_voice_pending_result(const char *message);
static int64_t codex_voice_started;
static char codex_voice_base[2001];
static uint32_t codex_voice_cursor,codex_voice_insert_byte;
static lv_obj_t *codex_task_rows[8],*codex_task_titles[8],*codex_empty;
static char codex_task_ids[8][37];
static void codex_selected_changed(lv_event_t *e);
static void codex_project_changed(lv_event_t *e);
static bool codex_default_focus(bool all_projects);
static bool codex_default_pending,codex_default_all;
static char codex_project_ids[24][17],codex_project_selected[17];
static int codex_project_count;
static lv_obj_t *codex_mic_glyph,*codex_stop_glyph;
static lv_obj_t *codex_voice_button,*codex_voice_switch,*codex_voice_cancel,*codex_voice_meter,*codex_camera_button;
static lv_obj_t *camera_layer,*camera_canvas,*camera_hint,*camera_take,*camera_retry,*camera_use,*camera_close;
static int camera_pose;
static bool photo_flow,photo_for_codex,photo_camera_processing;
static bool photo_open_pending;
static char photo_open_task[37];
static void codex_images_refresh(void);
static void photo_camera_use(void);
static void photo_finish(void);
static char codex_selected[37];
static char last_codex_status[96];
static lv_obj_t *saver_layer,*saver_time,*saver_date;
static int saver_restore_brightness;
static void saver_leave(void);
volatile unsigned tab5_pc_wakes;
volatile int tab5_saver_active;
volatile int tab5_ui_page; // Diagnostic values do not call LVGL from the USB task.
volatile int tab5_ui_data_age_ms=-1,tab5_ui_task_selected,tab5_ui_session_ready;
volatile unsigned tab5_codex_notice_state;
volatile unsigned tab5_gallery_reader_clicks,tab5_gallery_reader_guide_us,tab5_gallery_reader_layout_us,tab5_gallery_reader_paint_us;
volatile int tab5_ui_voice_check=-1,tab5_ui_voice_age_ms=-1;
static cJSON *item(cJSON *o, const char *key) { return cJSON_GetObjectItemCaseSensitive(o, key); }
static const char *text(cJSON *o, const char *key) {
    cJSON *v = item(o, key);
    return cJSON_IsString(v) ? v->valuestring : "--";
}
static double num(cJSON *o, const char *key) {
    cJSON *v = item(o, key);
    return cJSON_IsNumber(v) ? v->valuedouble : 0;
}
static bool yes(cJSON *o, const char *key) { return cJSON_IsTrue(item(o, key)); }
static void value(cJSON *o, const char *key, char *out, size_t size, const char *suffix) {
    cJSON *v = item(o, key);
    if (cJSON_IsNumber(v))
        snprintf(out, size, "%.1f%s", v->valuedouble, suffix);
    else
        snprintf(out, size, "--");
}
static void integer_value(cJSON *o, const char *key, char *out, size_t size, const char *suffix) {
    cJSON *v = item(o, key);
    if (cJSON_IsNumber(v)) snprintf(out, size, "%.0f%s", v->valuedouble, suffix);
    else snprintf(out, size, "--");
}
// Reuse creation code across pages instead of duplicating it at every call site.
// These helpers initialize widgets; pixel rendering keeps its existing optimization.
#if defined(__GNUC__) && !defined(__clang__)
#pragma GCC push_options
#pragma GCC optimize ("Os")
#endif
static __attribute__((noinline)) lv_obj_t *label(lv_obj_t *p, const char *s, int x, int y, int w, bool large) {
    lv_obj_t *o = lv_label_create(p);
    ui_text(o, s);
    lv_obj_set_pos(o, x, y);
    lv_obj_set_width(o, w);
    ui_text_font(o, large ? &lv_font_montserrat_40 : &tab5_font_24, 0);
    ui_text_color(o, lv_color_hex(INK), 0);
    return o;
}
static __attribute__((noinline)) lv_obj_t *card(lv_obj_t *p, int x, int y, int w, int h) {
    lv_obj_t *o = lv_obj_create(p);
    lv_obj_set_pos(o, x, y);
    lv_obj_set_size(o, w, h);
    ui_bg_color(o, lv_color_hex(SURFACE), 0);
    lv_obj_set_style_border_width(o, 0, 0);
    lv_obj_set_style_radius(o, UI_CARD_RADIUS, 0);
    lv_obj_set_style_pad_all(o, UI_CARD_PADDING, 0);
    lv_obj_remove_flag(o, LV_OBJ_FLAG_SCROLLABLE);
    return o;
}
static __attribute__((noinline)) lv_obj_t *button(lv_obj_t *p, const char *s, int x, int y, int w, lv_event_cb_t cb, int arg) {
    lv_obj_t *b = lv_button_create(p);
    lv_obj_remove_style_all(b);
    lv_obj_set_pos(b, x, y);
    lv_obj_set_size(b, w, 48);
    ui_bg_color(b, lv_color_hex(0x354247), 0);
    ui_bg_opa(b,LV_OPA_COVER,0);
    ui_text_color(b,lv_color_hex(0xEFF4F2),0);
    lv_obj_set_style_shadow_width(b, 0, 0);
    lv_obj_set_style_radius(b,UI_CONTROL_RADIUS,0);
    ui_bg_color(b,lv_color_hex(0x465C65),LV_STATE_PRESSED);
    ui_text_color(b,lv_color_hex(0xEFF4F2),LV_STATE_PRESSED);
    ui_bg_color(b,lv_color_hex(0x303A3E),LV_STATE_DISABLED);
    ui_text_color(b,lv_color_hex(0x829196),LV_STATE_DISABLED);
    ui_bg_opa(b,LV_OPA_COVER,LV_STATE_DISABLED);
    lv_obj_t *l = lv_label_create(b);
    ui_text(l, s);
    lv_obj_center(l);
    ui_text_font(l, &tab5_font_24, 0);
    lv_obj_add_event_cb(b, cb, LV_EVENT_CLICKED, (void *)(intptr_t)arg);
    return b;
}
#if defined(__GNUC__) && !defined(__clang__)
#pragma GCC pop_options
#endif
static lv_obj_t *workbench_back_button(lv_obj_t *parent,lv_event_cb_t callback) {
    lv_obj_t *back=button(parent,LV_SYMBOL_LEFT,30,12,52,callback,0);
    lv_obj_set_height(back,56);
    lv_obj_t *icon=lv_obj_get_child(back,0);
    ui_text_font(icon,&lv_font_montserrat_24,0);lv_obj_center(icon);
    return back;
}
static lv_obj_t *workbench_icon_button(lv_obj_t *p,int y,lv_event_cb_t cb,int arg) {
    // Header and footer shortcuts share geometry, while retaining their own backgrounds.
    const int size=56,right_margin=30;
    lv_obj_t *b=button(p,"",1280-right_margin-size,y,size,cb,arg);
    lv_obj_set_height(b,size);lv_obj_set_style_radius(b,16,0);
    return b;
}
static lv_obj_t *dropdown(lv_obj_t *p, const char *s, int x, int y, int w, int selected, lv_event_cb_t cb, int arg) {
    lv_obj_t *o = lv_dropdown_create(p);
    lv_obj_set_pos(o, x, y);
    lv_obj_set_width(o, w);
    lv_obj_set_height(o, 48);
    ui_bg_color(o, lv_color_hex(0x354247), 0);
    lv_obj_set_style_radius(o,12,0);
    lv_obj_set_style_pad_ver(o,7,0);
    lv_obj_set_style_border_width(o, 0, 0);
    ui_text_color(o, lv_color_hex(0xEFF4F2), 0);
    lv_dropdown_set_options(o, s);
    ui_text_font(o,&tab5_font_24,0);
    lv_dropdown_set_symbol(o, LV_SYMBOL_DOWN);
    ui_text_font(o, &lv_font_montserrat_24, LV_PART_INDICATOR);
    lv_dropdown_set_selected(o, selected);
    ui_text_font(lv_dropdown_get_list(o), &tab5_font_24, 0);
    if(cb)lv_obj_add_event_cb(o, cb, LV_EVENT_VALUE_CHANGED, (void *)(intptr_t)arg);
    return o;
}
static lv_obj_t *check(lv_obj_t *p, const char *s, int x, int y, bool enabled, lv_event_cb_t cb, int arg) {
    lv_obj_t *o = lv_checkbox_create(p);
    // Replace the theme indicator (including delayed transitions) so touch
    // selection is immediately legible against the dark surface.
    lv_obj_remove_style(o,NULL,LV_PART_INDICATOR|LV_STATE_ANY);
    lv_obj_set_pos(o, x, y);
    lv_checkbox_set_text(o, s);
    ui_text_color(o, lv_color_hex(0xEFF4F2), 0);
    lv_obj_set_style_pad_column(o,12,0);
    ui_bg_color(o,lv_color_hex(0x263136),LV_PART_INDICATOR);
    ui_bg_opa(o,LV_OPA_COVER,LV_PART_INDICATOR);
    lv_obj_set_style_border_width(o,2,LV_PART_INDICATOR);
    lv_obj_set_style_pad_all(o,3,LV_PART_INDICATOR);
    ui_border_color(o,lv_color_hex(0x82969D),LV_PART_INDICATOR);
    lv_obj_set_style_radius(o,6,LV_PART_INDICATOR);
    ui_text_font(o,&lv_font_montserrat_24,LV_PART_INDICATOR);
    ui_text_color(o,lv_color_hex(0x173642),LV_PART_INDICATOR|LV_STATE_CHECKED);
    ui_bg_color(o,lv_color_hex(0xB9DCEB),LV_PART_INDICATOR|LV_STATE_CHECKED);
    ui_border_color(o,lv_color_hex(0xB9DCEB),LV_PART_INDICATOR|LV_STATE_CHECKED);
    lv_obj_set_style_bg_image_src(o,LV_SYMBOL_OK,LV_PART_INDICATOR|LV_STATE_CHECKED);
    if (enabled)
        lv_obj_add_state(o, LV_STATE_CHECKED);
    lv_obj_add_event_cb(o, cb, LV_EVENT_VALUE_CHANGED, (void *)(intptr_t)arg);
    return o;
}
static int64_t preferences_due;
static int64_t settings_feedback_until;
static void settings_feedback(const char *text,bool error) {
    if(!save_status)return;
    ui_text(save_status,text);
    visible(save_status,true);
    settings_feedback_until=error?0:esp_timer_get_time()/1000+3000;
}
static void settings_feedback_tick(int64_t now) {
    if(settings_feedback_until&&now>=settings_feedback_until) {
        visible(save_status,false);
        settings_feedback_until=0;
    }
}
static lv_obj_t *ota_empty_hint,*ota_empty_icon;
static void ota_offer_layout(bool offered) {
    static int previous=-1;
    if(previous==(int)offered)return;
    previous=offered;
    lv_obj_set_height(ota_offer_card,offered?332:300);
    lv_obj_set_style_border_width(ota_offer_card,offered?2:0,0);
    ui_bg_opa(ota_offer_card,offered?LV_OPA_COVER:LV_OPA_TRANSP,0);
    lv_obj_set_pos(ota_offer_title,0,offered?0:140);
    lv_obj_set_style_text_align(ota_offer_title,offered?LV_TEXT_ALIGN_LEFT:LV_TEXT_ALIGN_CENTER,0);
    lv_obj_set_y(ota_button,offered?400:158);
    lv_obj_set_y(flash_entry,offered?400:158);
    lv_obj_set_pos(ota_hint,offered?280:24,offered?412:370);
    lv_obj_set_width(ota_hint,offered?618:850);
    lv_obj_set_style_text_align(ota_hint,offered?LV_TEXT_ALIGN_LEFT:LV_TEXT_ALIGN_CENTER,0);
    visible(ota_notes_box,offered);
    visible(ota_button,offered);
    visible(ota_empty_hint,!offered);visible(ota_empty_icon,!offered);
}
static bool render_pending;
static uint64_t metrics_seen;
static link_kind metrics_link=LINK_NONE;
static bool metrics_pending;
static bool gesture_active(void) {
    if(tabs&&lv_anim_get(lv_tabview_get_content(tabs),NULL))return true;
    for(lv_indev_t *input=lv_indev_get_next(NULL);input;input=lv_indev_get_next(input))
        if(lv_indev_get_state(input)==LV_INDEV_STATE_PRESSED||lv_indev_get_scroll_obj(input))return true;
    return false;
}
static void save(void) {
    preferences_due=0;
    bool ok = ui_preferences_save(&prefs);
    settings_feedback(ok ? "已保存到 TAB5" : "保存失败，请重试",!ok);
}
static void display_orientation_apply(int direction) {
    lv_display_t *display=lv_display_get_default();
    lv_display_rotation_t target=(lv_display_rotation_t)((1-direction+4)%4);
#ifdef TAB5_UI_PREVIEW
    target=(lv_display_rotation_t)((4-direction)%4);
#endif
    if(lv_display_get_rotation(display)==target)return;
    tab5_draw_cache_drop();
    // LVGL transforms the raw touch coordinates using this same display
    // rotation. Do not additionally mirror the touch controller.
    lv_display_set_rotation(display,target);
    for(lv_indev_t *input=lv_indev_get_next(NULL);input;input=lv_indev_get_next(input)) {
        if(lv_indev_get_display(input)==display) {
            bool pressed=lv_indev_get_state(input)==LV_INDEV_STATE_PRESSED;
            lv_indev_reset(input,NULL);
            // Idle event-mode touch devices may not report another release
            // until the next tap. Do not swallow that first post-flip tap.
            if(pressed)lv_indev_wait_release(input);
        }
    }
    lv_obj_invalidate(lv_screen_active());lv_obj_invalidate(lv_layer_top());lv_obj_invalidate(lv_layer_sys());
}
static void screen_orientation_apply(int direction) {display_orientation_apply(direction);}
static int camera_current_pose(void) {
    art_orientation_state a=art_orientation_get();
    int64_t now=esp_timer_get_time()/1000;
    if(a.ready&&a.sampled_ms>0&&now>=a.sampled_ms&&now-a.sampled_ms<=200&&camera_pose_valid(a.orientation))
        camera_pose=a.orientation;
    return camera_pose;
}
static bool camera_orientation_apply(int direction) {
    if(gesture_active()||ota_update_busy()||flash_benchmark_busy())return false;
    display_orientation_apply(direction);
    camera_geometry g=camera_geometry_for(direction);
    lv_obj_set_size(camera_layer,g.width,g.height);
    lv_obj_set_pos(camera_take,g.shutter_x,g.shutter_y);
    lv_obj_set_pos(camera_close,g.width-176,16);
    lv_obj_set_pos(camera_retry,28,g.height-92);
    lv_obj_set_pos(camera_use,g.width-256,g.height-92);
    lv_obj_set_style_max_width(camera_hint,g.width-220,0);
    if(camera_preview_get_state()==CAMERA_PREVIEW_CAPTURED)camera_canvas_review(camera_canvas);
    else camera_canvas_direction(camera_canvas,direction);
    return true;
}
static void screen_orientation_changed(lv_event_t *e) {
    int mode=lv_dropdown_get_selected(lv_event_get_target(e))?(screen_orientation.direction==2?2:1):0;
    if(!ui_orientation_save(mode)) {
        lv_dropdown_set_selected(orientation_selector,screen_orientation_mode?1:0);
        settings_feedback("保存失败，请重试",true);return;
    }
    screen_orientation_mode=mode;screen_orientation.candidate=3;screen_orientation.last=0;
    settings_feedback("已保存到 TAB5",false);
}
static void page_select(int index,bool move_strip) {
    if(current_page==7&&index!=7)clock_return_page=-1;
    if(index!=4&&health_open)health_leave_system();
    if(index!=7)clock_picker_close(NULL);
    if(index==10)index=11; // Former More entry now opens the unified app library.
    if(index!=0)startup_home.holding=false;
    if(((index>=2&&index<8)||index==12||index==13||index==14)&&current_page!=index&&current_page!=9)detail_return=current_page==0?0:11;
    if(index==9&&current_page!=9)settings_return=current_page;
    if(current_page==8&&index!=8)codex_bookmark_save();
    if(network_window&&index!=9)lv_obj_add_flag(network_window,LV_OBJ_FLAG_HIDDEN);
    if(network_diag_window&&index!=9)lv_obj_add_flag(network_diag_window,LV_OBJ_FLAG_HIDDEN);
    if(index!=9)host_editor_close();
    if(index!=8&&codex_keyboard)codex_keyboard_done(NULL);
    changing_page = true;
    int primary=-1;
    for(int i=0;i<4;i++)if(primary_pages[i]==index)primary=i;
    if(primary>=0)primary_index=primary;
    else if(index!=9)primary_index=3;
    if(move_strip)lv_tabview_set_active(tabs,primary_index,LV_ANIM_OFF);
    for(int i=2;i<=9;i++)if(i!=8)visible(pages[i],primary<0&&i==index);
    if(pages[12])visible(pages[12],index==12);
    if(pages[13])visible(pages[13],index==13);
    if(pages[14])visible(pages[14],index==14);
    visible(detail_layer,primary<0);
    // Detail pages cover the entire tab viewport. Keeping the tab tree visible
    // makes broad scroll invalidations draw both trees before the overlay.
    visible(tabs,primary>=0);
    current_page = index;
    if(index!=8)photo_open_pending=false;
    if(index==12)calendar_render();
    tab5_ui_page = index;
    last_change = esp_timer_get_time() / 1000;
    changing_page = false;
    if(workbench_title)ui_text(workbench_title,index==4&&health_open?"运行监控":names[index]);
    visible(health_header_entry,index==4);visible(health_segments,index==4);health_segments_refresh();
    lv_obj_set_x(banner,index==4?392:264);lv_obj_set_width(banner,index==4?276:404);
    lv_obj_set_x(alert_dot,index==4?404:276);
    lv_obj_set_width(workbench_title,index==4&&!health_open?72:180);
    visible(workbench_title,index!=7&&index!=4);
    if(clock_segments)visible(clock_segments,index==7);
    visible(workbench_back,primary<0);
    visible(settings_button,index!=9);
    lv_obj_set_x(workbench_title,primary<0?94:30);
    int group=primary_index;
    for(int i=0;i<4;i++)if(workbench_nav[i]) {
        ui_bg_color(workbench_nav[i],lv_color_hex(i==group?0xB9DCEB:0x2B3539),0);
        ui_text_color(workbench_nav[i],lv_color_hex(i==group?0x173642:0xB9C5C9),0);
    }
}
static void page(int index) {page_select(index,true);}
static const char *state(cJSON *o) {
    if (yes(o, "needsInput"))
        return "等待操作";
    if (yes(o, "completionActive"))
        return "任务完成";
    const char *s = text(o, "state");
    return !strcmp(s, "working") ? "工作中" : !strcmp(s, "idle") ? "空闲" : !strcmp(s, "offline") ? "离线" : "--";
}
static void rate(double n, char *out, size_t size) {
    if (n >= 1048576)
        snprintf(out, size, "%.2f MB/s", n / 1048576);
    else
        snprintf(out, size, "%.1f KB/s", n / 1024);
}
#include "ui_visuals.h"
#include "brand_logos.h"

#include "ui_dashboard.h"
#include "ui_runtime_health.h"
#include "ui_calendar.h"
#include "ui_saver.h"
#include "ui_clock_countdown.h"
#include "ui_agenda.h"
#include "ui_weather.h"
static void render_quota(void) { quota_render(); }
#include "ui_markets.h"
#include "ui_ota_notes.h"
static void render_stocks(void) { markets_render(); }
// Retain measured 250 ms samples; numbers and graph share their trailing
// one-second mean. Rasterize once per received batch, not per display strip.
#define NETWORK_POINTS 224
#define NETWORK_WIDTH 769
#define NETWORK_HEIGHT 250
#define NETWORK_RASTER_WIDTH 774
typedef struct {int32_t up,down;} network_point;
static network_point network_raw[NETWORK_POINTS],network_queue[NETWORK_POINTS];
static unsigned network_queued,network_head;
static uint16_t *network_pixels;
static lv_image_dsc_t network_image;
static int network_scroll;
static int32_t network_ceiling;
static lv_obj_t *network_span;
static void network_append(network_point point) {
    memmove(network_raw,network_raw+1,(NETWORK_POINTS-1)*sizeof(*network_raw));
    network_raw[NETWORK_POINTS-1]=point;
}
static network_point network_mean(int index) {
    int64_t u=0,d=0;int count=0;
    for(int i=index;i>=0&&i>index-4;i--) {
        network_point p=network_raw[i];
        if(p.up==LV_CHART_POINT_NONE||p.down==LV_CHART_POINT_NONE)break;
        u+=p.up;d+=p.down;count++;
    }
    return count?(network_point){u/count,d/count}:(network_point){LV_CHART_POINT_NONE,LV_CHART_POINT_NONE};
}
static void network_draw(lv_event_t *event) {
    if(!network_pixels)return;
    lv_layer_t *layer=lv_event_get_layer(event);
    lv_area_t bounds,clip=layer->_clip_area;
    lv_obj_get_coords(chart,&bounds);
    lv_area_t visible_area;if(!lv_area_intersect(&visible_area,&clip,&bounds))return;
    layer->_clip_area=visible_area;
    lv_draw_image_dsc_t image;lv_draw_image_dsc_init(&image);
    image.base.obj=chart;image.base.part=LV_PART_MAIN;
    image.src=&network_image;image.opa=LV_OPA_COVER;
    bounds.x1-=network_scroll;bounds.x2=bounds.x1+NETWORK_RASTER_WIDTH-1;
    lv_draw_image(layer,&image,&bounds);
    layer->_clip_area=clip;
}
static void network_plot(void) {
    network_point current=network_mean(NETWORK_POINTS-1);
    char up_text[32],down_text[32];
    rate(current.up,up_text,sizeof(up_text));rate(current.down,down_text,sizeof(down_text));
    ui_text(network_rates[0],current.up==LV_CHART_POINT_NONE?"--":up_text);
    ui_text(network_rates[1],current.down==LV_CHART_POINT_NONE?"--":down_text);
    if(!network_pixels) {
#ifdef TAB5_UI_PREVIEW
        network_pixels=malloc(NETWORK_RASTER_WIDTH*NETWORK_HEIGHT*2);
#else
        network_pixels=heap_caps_malloc(NETWORK_RASTER_WIDTH*NETWORK_HEIGHT*2,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
#endif
        if(!network_pixels)return;
        network_image=(lv_image_dsc_t){.header={.magic=LV_IMAGE_HEADER_MAGIC,.cf=LV_COLOR_FORMAT_RGB565,
            .w=NETWORK_RASTER_WIDTH,.h=NETWORK_HEIGHT,.stride=NETWORK_RASTER_WIDTH*2},
            .data_size=NETWORK_RASTER_WIDTH*NETWORK_HEIGHT*2,.data=(uint8_t*)network_pixels};
    }
    static network_point mean[NETWORK_POINTS+1];int32_t peak=8960;unsigned valid=0;
    for(int i=0;i<=NETWORK_POINTS;i++) {
        mean[i]=i==NETWORK_POINTS?mean[NETWORK_POINTS-1]:network_mean(i);
        if(mean[i].up!=LV_CHART_POINT_NONE){peak=LV_MAX(peak,mean[i].up);if(i<NETWORK_POINTS)valid++;}
        if(mean[i].down!=LV_CHART_POINT_NONE)peak=LV_MAX(peak,mean[i].down);
    }
    int32_t desired=(int32_t)((int64_t)peak*8/7);
    // Grow immediately; release the vertical scale slowly instead of pumping
    // the entire graph whenever an old peak leaves the history window.
    network_ceiling=desired>=network_ceiling?desired:LV_MAX(desired,network_ceiling-network_ceiling/50);
    int32_t divisor=(network_ceiling+999999)/1000000;
    int32_t *up=lv_chart_get_y_array(chart,upload),*down=lv_chart_get_y_array(chart,download);
    for(int i=0;i<NETWORK_POINTS;i++) {
        up[i]=mean[i].up==LV_CHART_POINT_NONE?LV_CHART_POINT_NONE:mean[i].up/divisor;
        down[i]=mean[i].down==LV_CHART_POINT_NONE?LV_CHART_POINT_NONE:mean[i].down/divisor;
    }
    lv_chart_set_range(chart,LV_CHART_AXIS_PRIMARY_Y,0,network_ceiling/divisor);
    static int16_t yy[2][NETWORK_RASTER_WIDTH];
    for(int x=0;x<NETWORK_RASTER_WIDTH;x++) {
        int position=x*(NETWORK_POINTS-1),index=position/(NETWORK_WIDTH-1),frac=position%(NETWORK_WIDTH-1);
        for(int channel=0;channel<2;channel++) {
            int32_t a=channel?mean[index].down:mean[index].up;
            int32_t b=index<NETWORK_POINTS?(channel?mean[index+1].down:mean[index+1].up):a;
            yy[channel][x]=-1;
            if(a==LV_CHART_POINT_NONE||(frac&&b==LV_CHART_POINT_NONE))continue;
            int64_t value=a+((int64_t)b-a)*frac/(NETWORK_WIDTH-1);
            yy[channel][x]=NETWORK_HEIGHT-1-value*(NETWORK_HEIGHT-1)/network_ceiling;
        }
    }
    uint16_t bg=lv_color_to_u16(lv_color_hex(SURFACE)),fill=lv_color_to_u16(lv_color_hex(0x29443B));
    uint16_t grid=lv_color_to_u16(lv_color_hex(0x37454B));
    uint16_t colors[]={lv_color_to_u16(lv_color_hex(NET_UP)),lv_color_to_u16(lv_color_hex(NET_DOWN))};
    // Sequential row writes avoid hundreds of alpha masks, triangles and
    // scattered external-RAM writes. Fully compose before invalidating.
    static uint16_t row[NETWORK_RASTER_WIDTH];
    for(int y=0;y<NETWORK_HEIGHT;y++) {
        bool gridline=y==0||y==62||y==124||y==186||y==249;
        for(int x=0;x<NETWORK_RASTER_WIDTH;x++) {
            uint16_t color=yy[1][x]>=0&&y>yy[1][x]?fill:bg;
            if(gridline)color=grid;
            for(int channel=1;channel>=0;channel--) {
                int mid=yy[channel][x];if(mid<0)continue;
                int left=x&&yy[channel][x-1]>=0?(mid+yy[channel][x-1])/2:mid;
                int right=x+1<NETWORK_RASTER_WIDTH&&yy[channel][x+1]>=0?(mid+yy[channel][x+1])/2:mid;
                if(y>=LV_MIN(mid,LV_MIN(left,right))-1&&y<=LV_MAX(mid,LV_MAX(left,right))+1)color=colors[channel];
            }
            row[x]=color;
        }
        memcpy(network_pixels+y*NETWORK_RASTER_WIDTH,row,sizeof(row));
    }
    double unit=network_ceiling>=1048576?1048576.0:1024.0;
    ui_text(chart_scale,unit>1024?"MB/s":"KB/s");
    for(int i=0;i<5;i++) {
        char label[24];double value=network_ceiling/unit*(4-i)/4;
        snprintf(label,sizeof(label),value==0||value>=100?"%.0f":"%.1f",value);
        ui_text(network_ticks[i],label);
    }
    if(network_span)ui_text_fmt(network_span,"1 秒均值 · 已采集 %u 秒",(valid+3)/4);
    network_scroll=0;lv_obj_invalidate(chart);
}
static void network_flush(void) {
    if(!network_queued)return;
    while(network_queued) {
        network_append(network_queue[network_head]);
        network_head=(network_head+1)%NETWORK_POINTS;network_queued--;
    }
    network_plot();
}
static void network_enqueue(network_point point) {
    if(network_queued==NETWORK_POINTS) {
        // Bounded memory: consume the oldest sample before adding another.
        network_append(network_queue[network_head]);
        network_head=(network_head+1)%NETWORK_POINTS;network_queued--;
    }
    network_queue[(network_head+network_queued++)%NETWORK_POINTS]=point;
}
static void render_chart(cJSON *s) {
    const char *session=text(s,"sampleSession");
    uint64_t seq=(uint64_t)num(s,"sampleSequence");
    bool reset=strcmp(session,metric_session)!=0;
    if(reset) {
        snprintf(metric_session,sizeof(metric_session),"%s",session);metric_seq=0;
        network_queued=network_head=0;network_ceiling=0;
        for(int i=0;i<NETWORK_POINTS;i++)network_raw[i]=(network_point){LV_CHART_POINT_NONE,LV_CHART_POINT_NONE};
    }
    if(seq<=metric_seq)return;
    cJSON *samples=item(s,"samples");int count=cJSON_GetArraySize(samples);
    uint64_t delta=seq-metric_seq;
    int take=delta<(uint64_t)count?(int)delta:count;
    uint64_t missing=delta-take;if(missing>NETWORK_POINTS)missing=NETWORK_POINTS;
    while(missing--)network_enqueue((network_point){LV_CHART_POINT_NONE,LV_CHART_POINT_NONE});
    for(int i=count-take;i<count;i++) {
        cJSON *v=cJSON_GetArrayItem(samples,i);
        network_enqueue((network_point){(int32_t)LV_CLAMP(0,num(v,"upload"),1000000000),
            (int32_t)LV_CLAMP(0,num(v,"download"),1000000000)});
    }
    metric_seq=seq;
    network_flush();
}
static void render_system(void) {
    health_render();
    cJSON *m=item(shown,"systemMetrics");
    meter_update(&system_meter[0],m,"cpuPercent",BLUE);
    meter_update(&system_meter[1],m,"memoryPercent",GREEN);
    if(cJSON_GetArraySize(item(m,"samples"))>0)render_chart(m);
    else {
        char up[32],down[32];rate(num(m,"uploadBytesPerSecond"),up,sizeof(up));rate(num(m,"downloadBytesPerSecond"),down,sizeof(down));
        ui_text(network_rates[0],up);ui_text(network_rates[1],down);
    }
}
// Text lives in PSRAM. Only the latest draft (or unresolved send) is
// checkpointed locally; the host keeps the per-conversation draft library.
typedef struct {char id[37],request[37],operation[16],turn[37],images[3][37];uint16_t *thumbnails[3];char *text;bool loaded,dirty;unsigned revision,edited;} codex_draft;
static unsigned codex_edit_sequence;
static codex_draft *codex_drafts;
static ui_codex_checkpoint *codex_checkpoint,*codex_checkpoint_saved;
static bool codex_setting_text,codex_restore_project,codex_local_dirty;
static int64_t codex_edit_at,codex_sync_next;
static char codex_sync_op[16],codex_sync_task[37],codex_sync_request[37];
static unsigned codex_sync_revision;
static bool codex_syncing,codex_load_waiting,codex_send_queued,codex_querying;
static char codex_action[16];
static void *codex_alloc(size_t size) {
#ifdef TAB5_UI_PREVIEW
    return calloc(1,size);
#else
    return heap_caps_calloc(1,size,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
#endif
}
static void codex_uuid(char out[37]) {
    uint8_t b[16];
#ifdef TAB5_UI_PREVIEW
    static unsigned n;memset(b,0,sizeof(b));unsigned value=++n;memcpy(b,&value,sizeof(value));
#else
    esp_fill_random(b,sizeof(b));
#endif
    b[6]=(b[6]&15)|64;b[8]=(b[8]&63)|128;
    snprintf(out,37,"%02x%02x%02x%02x-%02x%02x-%02x%02x-%02x%02x-%02x%02x%02x%02x%02x%02x",b[0],b[1],b[2],b[3],b[4],b[5],b[6],b[7],b[8],b[9],b[10],b[11],b[12],b[13],b[14],b[15]);
}
static codex_draft *codex_draft_for(const char *id,bool create) {
    if(strlen(id)!=36)return NULL;
    if(!codex_drafts&&create)codex_drafts=codex_alloc(80*sizeof(*codex_drafts));
    if(!codex_drafts)return NULL;
    codex_draft *empty=NULL;
    for(int i=0;i<80;i++) {
        if(!strcmp(codex_drafts[i].id,id))return &codex_drafts[i];
        if(!codex_drafts[i].id[0]&&!empty)empty=&codex_drafts[i];
    }
    if(create&&empty){snprintf(empty->id,sizeof(empty->id),"%s",id);return empty;}
    return NULL;
}
static bool codex_store_text(codex_draft *d,const char *value,bool dirty) {
    if(!d||strlen(value)>2000)return false;
    if(d->text&&!strcmp(d->text,value))return true;
    char *copy=codex_alloc(strlen(value)+1);if(!copy)return false;
    strcpy(copy,value);free(d->text);d->text=copy;d->revision++;
    if(dirty){d->dirty=true;d->loaded=true;d->edited=++codex_edit_sequence;codex_local_dirty=true;}
    return true;
}
static bool codex_checkpoint_write(void) {
    if(!codex_checkpoint)codex_checkpoint=codex_alloc(sizeof(*codex_checkpoint));
    if(!codex_checkpoint_saved)codex_checkpoint_saved=codex_alloc(sizeof(*codex_checkpoint_saved));
    if(!codex_checkpoint||!codex_checkpoint_saved)return false;
    codex_draft *d=codex_draft_for(codex_selected,false);
    if(!d||!d->loaded) {
        d=NULL;
        if(codex_drafts)for(int i=0;i<80;i++)if(codex_drafts[i].loaded&&(!d||codex_drafts[i].edited>d->edited))d=&codex_drafts[i];
    }
    // An uncertain request survives even if the user visits another task.
    if(codex_drafts)for(int i=0;i<80;i++)if(codex_drafts[i].request[0]){d=&codex_drafts[i];break;}
    memset(codex_checkpoint,0,sizeof(*codex_checkpoint));codex_checkpoint->version=1;
    if(d) {
        snprintf(codex_checkpoint->task,sizeof(codex_checkpoint->task),"%s",d->id);
        snprintf(codex_checkpoint->text,sizeof(codex_checkpoint->text),"%s",d->text?d->text:"");
        snprintf(codex_checkpoint->request,sizeof(codex_checkpoint->request),"%s",d->request);
        snprintf(codex_checkpoint->operation,sizeof(codex_checkpoint->operation),"%s",d->operation);
        snprintf(codex_checkpoint->turn,sizeof(codex_checkpoint->turn),"%s",d->turn);
        memcpy(codex_checkpoint->images,d->images,sizeof(d->images));
    }
    if(memcmp(codex_checkpoint,codex_checkpoint_saved,sizeof(*codex_checkpoint))) {
        if(!ui_codex_checkpoint_save(codex_checkpoint))return false;
        memcpy(codex_checkpoint_saved,codex_checkpoint,sizeof(*codex_checkpoint));
    }
    codex_local_dirty=false;return true;
}
static bool codex_save_draft(void) {
    tab5_ime_finish();
    if(tab5_ime_pending()[0]){ui_text(codex_voice_hint,"输入已满，请先删除部分内容");return false;}
    if(!codex_selected[0]||!codex_input)return true;
    codex_draft *d=codex_draft_for(codex_selected,true);
    const char *value=lv_textarea_get_text(codex_input);
    // An empty placeholder before the host load is not an intentional deletion.
    if(d&&!d->loaded&&!d->dirty&&!*value)return true;
    if(!codex_store_text(d,value,true)||!codex_checkpoint_write()) {
        ui_text(codex_voice_hint,"草稿未能保存，请先保留当前页面");return false;
    }
    return true;
}
static void codex_draft_changed(lv_event_t *e) {
    (void)e;if(codex_setting_text||!codex_selected[0])return;
    if(!codex_store_text(codex_draft_for(codex_selected,true),lv_textarea_get_text(codex_input),true))
        ui_text(codex_voice_hint,"草稿暂存空间不足");
    codex_edit_at=esp_timer_get_time()/1000;codex_sync_next=codex_edit_at+1800;
}
static void codex_set_text(const char *value) {codex_setting_text=true;lv_textarea_set_text(codex_input,value);codex_setting_text=false;}
static void codex_load_draft(void) {
    codex_draft *d=codex_draft_for(codex_selected,true);
    codex_set_text(d&&d->text?d->text:"");
    codex_images_refresh();
    codex_sync_next=esp_timer_get_time()/1000+1200;codex_read_next=0;
    if(d&&d->request[0])lv_obj_add_state(codex_input,LV_STATE_DISABLED);else lv_obj_remove_state(codex_input,LV_STATE_DISABLED);
}
static void codex_restore_draft(void) {
    if(!codex_checkpoint)codex_checkpoint=codex_alloc(sizeof(*codex_checkpoint));
    if(!codex_checkpoint||!ui_codex_checkpoint_load(codex_checkpoint)||strlen(codex_checkpoint->task)!=36)return;
    codex_draft *d=codex_draft_for(codex_checkpoint->task,true);
    if(!codex_store_text(d,codex_checkpoint->text,true))return;
    snprintf(d->request,sizeof(d->request),"%s",codex_checkpoint->request);
    snprintf(d->operation,sizeof(d->operation),"%s",codex_checkpoint->operation);
    snprintf(d->turn,sizeof(d->turn),"%s",codex_checkpoint->turn);
    memcpy(d->images,codex_checkpoint->images,sizeof(d->images));
    snprintf(codex_selected,sizeof(codex_selected),"%s",d->id);codex_restore_project=true;codex_load_draft();
}
static bool codex_request(const char *op,const char *task,const char *value,const char *id,const char *turn) {
    if(photo_flow)return false;
    int64_t age=esp_timer_get_time()/1000-last_received;
    const char *session=text(item(shown,"pcInput"),"session");
    codex_draft *d=codex_draft_for(task,false);
    return age>=0&&age<=6000&&strcmp(session,"--")&&codex_remote_with_images(op,task,value,session,(int64_t)num(shown,"epochMilliseconds")+age,id,turn,strcmp(op,"interrupt")&&d?d->images:NULL);
}
static bool codex_running(void) {
    cJSON *control=item(item(shown,"codexTasks"),"control");
    return !strcmp(text(control,"taskId"),codex_selected)&&yes(control,"running")&&strlen(text(control,"turnId"))==36;
}
static void codex_control_style(void) {
    if(!codex_send_button)return;
    codex_draft *d=codex_draft_for(codex_selected,false);
    if(!codex_waiting&&!codex_send_queued)ui_text(lv_obj_get_child(codex_send_button,0),d&&d->request[0]?"查询送达":codex_running()?"追加":"发送");
    if(codex_interrupt_button) {
        visible(codex_interrupt_button,codex_running());
        if(codex_waiting||codex_reading||codex_syncing||(d&&d->request[0]))lv_obj_add_state(codex_interrupt_button,LV_STATE_DISABLED);
        else lv_obj_remove_state(codex_interrupt_button,LV_STATE_DISABLED);
    }
}
static void codex_sync_tick(int64_t now) {
    if(codex_voice_waiting||codex_waiting)return;
    if(!photo_flow&&codex_local_dirty&&now-codex_edit_at>=1500&&!codex_checkpoint_write()) {
        codex_edit_at=now;ui_text(codex_voice_hint,"本机草稿尚未保存");
    }
    if(codex_syncing) {
        char status[96];int code=codex_remote_status(codex_sync_task,status,sizeof(status));
        if(code==-1)return;
        codex_syncing=false;codex_sync_next=now+3000;
        codex_draft *d=codex_draft_for(codex_sync_task,false);
        if(code==200&&d&&d->revision==codex_sync_revision) {
            if(!strcmp(codex_sync_op,"draft-save"))d->dirty=false;
            else {codex_load_waiting=true;codex_sync_next=now+6000;}
        }
    }
    if(codex_load_waiting) {
        cJSON *view=item(item(shown,"codexTasks"),"draftView");
        if(!strcmp(text(view,"taskId"),codex_sync_task)&&!strcmp(text(view,"requestId"),codex_sync_request)) {
            codex_draft *d=codex_draft_for(codex_sync_task,false);
            if(d&&d->revision==codex_sync_revision&&!d->dirty) {
                if(codex_store_text(d,text(view,"text"),false)) {
                    memset(d->images,0,sizeof(d->images));cJSON *ids=item(view,"images");
                    for(int i=0;i<3;i++){cJSON *id=cJSON_GetArrayItem(ids,i);if(cJSON_IsString(id)&&strlen(id->valuestring)==36)snprintf(d->images[i],37,"%s",id->valuestring);}
                    d->loaded=true;
                    const char *pending=text(view,"pendingRequest");
                    if(strlen(pending)==36) {
                        snprintf(d->request,sizeof(d->request),"%s",pending);
                        snprintf(d->operation,sizeof(d->operation),"%s",text(view,"operation"));
                        snprintf(d->turn,sizeof(d->turn),"%s",text(view,"expectedTurn"));
                    }
                    if(!strcmp(codex_selected,d->id)) {
                        codex_load_draft();codex_local_dirty=true;
                        if(d->text&&d->text[0])ui_text(codex_voice_hint,"已恢复草稿");
                    }
                }
            }
            codex_load_waiting=false;
        }else if(now<codex_sync_next)return;else codex_load_waiting=false;
    }
    // Desktop Quick Console owns a separate composer. Finish any previous
    // request above, but do not keep launching hidden draft jobs during a visit.
    if(photo_flow||quick_visible||photo_open_pending||codex_voice_start_pending||codex_reading||codex_syncing||codex_send_queued||now<codex_sync_next)return;
    codex_draft *d=NULL;
    if(codex_drafts)for(int i=0;i<80;i++)if(codex_drafts[i].dirty&&!codex_drafts[i].request[0]){d=&codex_drafts[i];break;}
    if(!d) {d=codex_draft_for(codex_selected,true);if(!d||d->loaded||d->request[0])return;}
    snprintf(codex_sync_op,sizeof(codex_sync_op),"%s",d->dirty?"draft-save":"draft-load");
    codex_uuid(codex_sync_request);snprintf(codex_sync_task,sizeof(codex_sync_task),"%s",d->id);codex_sync_revision=d->revision;
    codex_sync_next=now+4000;
    if(codex_request(codex_sync_op,d->id,d->text?d->text:"",codex_sync_request,"")) {codex_syncing=true;codex_result_is_read=true;}
}
// Bounded pages live in PSRAM through the existing cJSON allocator.
// Cache keys include task and turn; a bridge restart invalidates the snapshot.
static void codex_reply_set_text(const char *value) {
    ui_text(codex_reply_text,value);
    reply_document_set(codex_reply_document,value);
    bool formatted=lv_obj_get_child_count(codex_reply_document)>0;
    visible(codex_reply_text,!formatted&&value[0]);visible(codex_reply_document,formatted);
}
static void codex_bookmark_save(void) {
    if(!codex_selected[0]||!codex_reply_turn[0]||!codex_reply_overlay||
       lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN)||codex_nav_pending)return;
    if(!codex_bookmarks)codex_bookmarks=codex_alloc(32*sizeof(*codex_bookmarks));
    if(!codex_bookmarks)return;
    int slot=0;unsigned oldest=UINT32_MAX;
    for(int i=0;i<32;i++) {
        if(!strcmp(codex_bookmarks[i].task,codex_selected)){slot=i;break;}
        if(codex_bookmarks[i].stamp<oldest){oldest=codex_bookmarks[i].stamp;slot=i;}
    }
    codex_bookmark *mark=&codex_bookmarks[slot];
    int scroll=lv_obj_get_scroll_y(codex_reply_scroll);if(scroll<0)scroll=0;
    bool follow=codex_reply_follow&&lv_obj_get_scroll_bottom(codex_reply_scroll)<24;
    if(!strcmp(mark->task,codex_selected)&&!strcmp(mark->turn,codex_reply_turn)&&mark->page==codex_reply_page&&mark->y==scroll&&mark->follow==follow)return;
    snprintf(mark->task,sizeof(mark->task),"%s",codex_selected);
    snprintf(mark->turn,sizeof(mark->turn),"%s",codex_reply_turn);
    mark->page=codex_reply_page;mark->y=scroll;
    mark->follow=follow;
    mark->stamp=++codex_bookmark_stamp;
    codex_bookmarks_dirty=true;
}
static void codex_cache_edge(const char *task,const char *from,const char *to,int direction);
static void codex_cache_store(cJSON *payload) {
    cJSON *v=item(payload,"replyView");
    if(!cJSON_IsObject(v)||!cJSON_IsString(item(payload,"text")))return;
    int slot=-1;unsigned oldest=UINT32_MAX;
    for(int i=0;i<CODEX_CACHE_PAGES;i++) {
        cJSON *other=item(codex_reply_cache[i].value,"replyView");
        if(codex_reply_cache[i].value&&!strcmp(text(payload,"taskId"),text(codex_reply_cache[i].value,"taskId"))&&
           !strcmp(text(v,"turnId"),text(other,"turnId"))&&num(v,"page")==num(other,"page")){slot=i;break;}
        if(codex_reply_cache[i].stamp<oldest){oldest=codex_reply_cache[i].stamp;slot=i;}
    }
    cJSON *copy=cJSON_Duplicate(payload,true);if(!copy)return;
    cJSON_DeleteItemFromObjectCaseSensitive(copy,"neighbors");
    bool latest=!cJSON_IsTrue(item(v,"history"))&&num(v,"page")==num(v,"pageCount")-1;
    if(latest)for(int i=0;i<CODEX_CACHE_PAGES;i++)if(!strcmp(text(payload,"taskId"),text(codex_reply_cache[i].value,"taskId")))codex_reply_cache[i].latest=false;
    cJSON_Delete(codex_reply_cache[slot].value);
    codex_reply_cache[slot]=(codex_cached_page){.value=copy,.stamp=++codex_cache_stamp,.latest=latest};
    codex_cache_edge(text(payload,"taskId"),text(v,"turnId"),text(v,"previousTurnId"),-1);
    codex_cache_edge(text(payload,"taskId"),text(v,"turnId"),text(v,"nextTurnId"),1);
}
static void codex_cache_reply(cJSON *payload) {
    cJSON *neighbor=NULL;cJSON_ArrayForEach(neighbor,item(payload,"neighbors"))codex_cache_store(neighbor);
    codex_cache_store(payload);
}
static int codex_cache_index(bool latest,const char *turn,int page) {
    for(int i=0;i<CODEX_CACHE_PAGES;i++) {
        codex_cached_page *entry=&codex_reply_cache[i];cJSON *v=item(entry->value,"replyView");
        if(!entry->value||strcmp(text(entry->value,"taskId"),codex_selected))continue;
        if(latest?!entry->latest:(strcmp(text(v,"turnId"),turn)||num(v,"page")!=page))continue;
        return i;
    }
    return -1;
}
static cJSON *codex_cache_copy(int index) {
    if(index<0)return NULL;
    codex_reply_cache[index].stamp=++codex_cache_stamp;
    return cJSON_Duplicate(codex_reply_cache[index].value,true);
}
static cJSON *codex_cache_find(bool latest,const char *turn,int page) {
    return codex_cache_copy(codex_cache_index(latest,turn,page));
}
static void codex_cache_edge(const char *task,const char *from,const char *to,int direction) {
    if(!from[0]||!to[0]||!strcmp(from,"--")||!strcmp(to,"--")||!strcmp(from,to)||(direction!=-1&&direction!=1))return;
    int slot=0;unsigned oldest=UINT32_MAX;
    for(int i=0;i<32;i++) {
        codex_cached_edge *edge=&codex_turn_cache[i];
        if(!strcmp(edge->task,task)&&!strcmp(edge->from,from)&&edge->direction==direction){slot=i;break;}
        if(edge->stamp<oldest){oldest=edge->stamp;slot=i;}
    }
    codex_cached_edge *edge=&codex_turn_cache[slot];
    snprintf(edge->task,sizeof(edge->task),"%s",task);snprintf(edge->from,sizeof(edge->from),"%s",from);snprintf(edge->to,sizeof(edge->to),"%s",to);
    edge->direction=direction;edge->stamp=++codex_cache_stamp;
}
static int codex_cache_turn_index(const char *from,int direction) {
    for(int i=0;i<32;i++) {
        codex_cached_edge *edge=&codex_turn_cache[i];
        if(strcmp(edge->task,codex_selected)||strcmp(edge->from,from)||edge->direction!=direction)continue;
        int best=-1;
        for(int j=0;j<CODEX_CACHE_PAGES;j++) {
            cJSON *value=codex_reply_cache[j].value,*view=item(value,"replyView");
            if(!value||strcmp(text(value,"taskId"),codex_selected)||strcmp(text(view,"turnId"),edge->to))continue;
            // Prefer page zero, otherwise reopen the most recently read cached page.
            if(num(view,"page")==0)return j;
            if(best<0||codex_reply_cache[j].stamp>codex_reply_cache[best].stamp)best=j;
        }
        return best;
    }
    return -1;
}
static bool codex_public_reply(cJSON *view,const char *body);
static void codex_cache_visible(void) {
    cJSON *view=codex_inline_reply?item(codex_inline_reply,"replyView"):item(item(shown,"codexTasks"),"replyView");
    if(strcmp(text(view,"taskId"),codex_selected)||strcmp(text(view,"turnId"),codex_reply_turn)||num(view,"page")!=codex_reply_page||!codex_public_reply(view,lv_label_get_text(codex_reply_text)))return;
    cJSON *payload=cJSON_CreateObject();if(!payload)return;
    cJSON_AddStringToObject(payload,"taskId",codex_selected);
    cJSON_AddStringToObject(payload,"text",lv_label_get_text(codex_reply_text));
    cJSON_AddStringToObject(payload,"receipt",text(item(item(shown,"codexTasks"),"receipts"),codex_selected));
    cJSON_AddItemToObject(payload,"replyView",cJSON_Duplicate(view,true));
    codex_cache_store(payload);cJSON_Delete(payload);
}
static void render_home_task(void) {
    cJSON *remote=item(shown,"codexTasks"),*recent=item(remote,"overviewTask");
    if(!cJSON_IsObject(recent)) {
        // Older bridges may reorder tasks for the reply viewer. Use timestamps,
        // never assume the first transported entry is the most recent session.
        cJSON *task=NULL;recent=NULL;
        cJSON_ArrayForEach(task,item(remote,"tasks")) {
            if(strlen(text(task,"id"))!=36)continue;
            bool working=!strcmp(text(task,"state"),"working"),was_working=recent&&!strcmp(text(recent,"state"),"working");
            if(!recent||(working&&!was_working)||(working==was_working&&num(task,"updatedAt")>num(recent,"updatedAt")))recent=task;
        }
    }
    // Unread completed turns take precedence over the default working task.
    // Reading/heartbeats never change the editor selection or its draft.
    home_task_unread=false;home_task_turn[0]=0;
    unsigned newest=0,count=0;
    if(codex_notices)for(int i=0;i<80;i++)if(codex_notices[i].unread) {
        cJSON *task=NULL;
        cJSON_ArrayForEach(task,item(remote,"tasks"))if(!strcmp(text(task,"id"),codex_notices[i].task)) {
            count++;
            if(!home_task_unread||codex_notices[i].arrived>newest) {
                recent=task;newest=codex_notices[i].arrived;home_task_unread=true;
                snprintf(home_task_turn,sizeof(home_task_turn),"%s",codex_notices[i].turn);
            }
            break;
        }
    }
    bool valid=recent&&strlen(text(recent,"id"))==36;
    visible(home_task_title,valid);
    for(int i=0;i<2;i++){visible(home_system_captions[i],!valid);visible(home_system_values[i],!valid);}
    snprintf(home_task_id,sizeof(home_task_id),"%s",valid?text(recent,"id"):"");
    snprintf(home_task_project,sizeof(home_task_project),"%s",valid?text(recent,"projectId"):"");
    if(valid)ui_text(home_task_title,text(recent,"title"));
    else {
        cJSON *metrics=item(shown,"systemMetrics");char cpu[24],memory[24];
        integer_value(metrics,"cpuPercent",cpu,sizeof(cpu),"%");integer_value(metrics,"memoryPercent",memory,sizeof(memory),"%");
        ui_text_fmt(home_task_title,"CPU  %s     内存  %s",cpu,memory);
        ui_text(home_system_values[0],cpu);ui_text(home_system_values[1],memory);
    }
    const char *folder=valid?text(recent,"folder"):"";
    if(folder[0]&&strcmp(folder,"--"))ui_text_fmt(home_task_note,"项目 · %s",folder);else ui_text(home_task_note,"");
    bool working=valid&&!strcmp(text(recent,"state"),"working");
    const char *task_state=text(recent,"state");
    if(home_task_unread)ui_text_fmt(model_state[1],"已完成 · %u 个未读",count);
    else ui_text(model_state[1],!valid?(shown?"电脑状态":"等待电脑数据"):working?"运行中":!strcmp(task_state,"waiting")?"等待操作":!strcmp(task_state,"unknown")?"状态待确认":"空闲 · 最近会话");
    ui_text(lv_obj_get_child(home_task_action,0),!valid?"查看电脑状态":home_task_unread?"查看完成内容":"继续 Codex 任务");
    tint(model_state[1],working||home_task_unread?GREEN:MUTED);ui_bg_color(model_dot[1],lv_color_hex(working||home_task_unread?GREEN:MUTED),0);
}
static bool codex_public_reply(cJSON *view,const char *body) {
    cJSON *flag=item(view,"hasText");
    if(cJSON_IsBool(flag))return cJSON_IsTrue(flag)&&body&&body[0];
    // Compatibility with bridges predating the explicit content flag.
    return body&&body[0]&&strcmp(body,"本轮尚无公开文字。")&&
        strcmp(body,"Codex 正在处理，等待新的动态…")&&strcmp(body,"暂未收到会话动态。")&&
        strcmp(body,"正在读取会话动态…");
}
static void render_codex(void) {
    render_home_task();
    // Wireless telemetry can still list the previous desktop selection. The
    // confirmed Quick Console target must not be cleared by that older catalog.
    if(quick_visible)return;
    const char *cache_session=text(item(shown,"pcInput"),"session");
    if(strcmp(cache_session,"--")&&strcmp(codex_cache_session,cache_session)) {
        for(int i=0;i<CODEX_CACHE_PAGES;i++){cJSON_Delete(codex_reply_cache[i].value);codex_reply_cache[i]=(codex_cached_page){0};}
        memset(codex_turn_cache,0,sizeof(codex_turn_cache));
        codex_nav_revision++;
        cJSON_Delete(codex_inline_reply);codex_inline_reply=NULL;codex_showing_cache=false;
        snprintf(codex_cache_session,sizeof(codex_cache_session),"%s",cache_session);
    }
    if (!codex_list || !shown || codex_waiting || codex_voice_waiting || codex_send_queued) return;
    cJSON *remote = item(shown, "codexTasks"), *tasks = item(remote, "tasks"), *projects=item(remote,"projects");
    int count = cJSON_GetArraySize(tasks);
    if(codex_default_pending&&count&&codex_default_focus(codex_default_all))return;
    if(codex_restore_project&&!count)return;
    if(codex_restore_project) {
        for(int i=0;i<count;i++)if(!strcmp(text(cJSON_GetArrayItem(tasks,i),"id"),codex_selected)) {
            snprintf(codex_project_selected,sizeof(codex_project_selected),"%s",text(cJSON_GetArrayItem(tasks,i),"projectId"));codex_restore_project=false;break;
        }
    }
    codex_control_style();
    // LVGL callbacks share one task; a 4 KB automatic buffer exhausts its stack.
    static char options[4096];options[0]=0;int selected_project=-1;
    codex_project_count=0;
    for(int i=0;i<cJSON_GetArraySize(projects)&&codex_project_count<24;i++) {
        cJSON *project=cJSON_GetArrayItem(projects,i);
        const char *id=text(project,"id"),*name=text(project,"name");
        if(strlen(id)!=16||!strcmp(name,"--"))continue;
        size_t used=strlen(options),needed=strlen(name)+(codex_project_count?1:0);
        if(used+needed>=sizeof(options))break;
        snprintf(options+used,sizeof(options)-used,"%s%s",codex_project_count?"\n":"",name);
        snprintf(codex_project_ids[codex_project_count],sizeof(codex_project_ids[0]),"%s",id);
        if(!strcmp(id,codex_project_selected))selected_project=codex_project_count;
        codex_project_count++;
    }
    if(!codex_project_count) {
        // An older bridge still supplies the global recent-task list.
        snprintf(options,sizeof(options),"%s",count?"最近任务":"暂无项目");
        codex_project_ids[0][0]=0;codex_project_count=1;selected_project=0;
    }
    if(selected_project<0)selected_project=0;
    snprintf(codex_project_selected,sizeof(codex_project_selected),"%s",codex_project_ids[selected_project]);
    if(strcmp(lv_dropdown_get_options(codex_project_picker),options))lv_dropdown_set_options(codex_project_picker,options);
    lv_dropdown_set_selected(codex_project_picker,selected_project);
    int selected=-1,visible_count=0;
    cJSON *visible_tasks[8]={0};
    for(int source=0;source<count;source++) {
        cJSON *task=cJSON_GetArrayItem(tasks,source);
        if(codex_project_selected[0]&&strcmp(text(task,"projectId"),codex_project_selected))continue;
        const char *id=text(task,"id");
        bool valid=task&&strlen(id)==36;
        if(!valid)continue;
        if(visible_count<8)visible_tasks[visible_count++]=task;
        else if(!strcmp(id,codex_selected))visible_tasks[7]=task;
    }
    for(int i=0;i<visible_count;i++) {
        cJSON *task=visible_tasks[i];const char *id=text(task,"id");
        visible(codex_task_rows[i],true);
        snprintf(codex_task_ids[i],sizeof(codex_task_ids[i]),"%s",id);
        bool active=!strcmp(id,codex_selected);if(active)selected=i;
        ui_text(codex_task_titles[i],text(task,"title"));
        tint(codex_task_titles[i],active?BLUE:INK);
        ui_bg_color(codex_task_rows[i],lv_color_hex(active?0x465C65:0x354247),0);
        ui_border_opa(codex_task_rows[i],active?LV_OPA_COVER:LV_OPA_TRANSP,0);
        ui_border_color(codex_task_rows[i],lv_color_hex(BLUE),0);
    }
    for(int i=visible_count;i<8;i++){visible(codex_task_rows[i],false);codex_task_ids[i][0]=0;}
    visible(codex_empty,visible_count==0);
    if(selected<0){if(codex_selected[0]){if(!codex_save_draft())return;codex_selected[0]=0;codex_load_draft();}ui_text(codex_hint,"输入");}
    else ui_text(codex_hint,lv_label_get_text(codex_task_titles[selected]));
    cJSON *receipt = item(item(remote, "receipts"), codex_selected);
    bool task_running=cJSON_IsString(receipt)&&!strcmp(receipt->valuestring,"运行中");
    visible(codex_run_badge,task_running);
    int hint_width=task_running?432:566;
    if(lv_obj_get_width(codex_hint)!=hint_width)lv_obj_set_width(codex_hint,hint_width);
    if (cJSON_IsString(receipt)) {
        if(codex_submit_accepted&&strcmp(receipt->valuestring,codex_receipt_before_send)) {
            codex_feedback_pinned=false;codex_submit_accepted=false;
        }
        if(!codex_feedback_pinned&&!lv_label_get_text(codex_voice_hint)[0])ui_text(codex_receipt,task_running?"":receipt->valuestring);
        ui_text(codex_reply_status,receipt->valuestring);
        tint(codex_reply_status,strstr(receipt->valuestring,"运行")?BLUE:strstr(receipt->valuestring,"完成")?GREEN:AMBER);
    }
    bool inline_matches=codex_inline_reply&&!strcmp(text(codex_inline_reply,"taskId"),codex_selected);
    cJSON *response=inline_matches?item(codex_inline_reply,"text"):item(item(remote,"responses"),codex_selected);
    if(inline_matches)ui_text(codex_reply_status,text(codex_inline_reply,"receipt"));
    bool has_response=cJSON_IsString(response)&&response->valuestring[0];
    visible(codex_reply_button,codex_selected[0]!=0);
    cJSON *view=inline_matches?item(codex_inline_reply,"replyView"):item(remote,"replyView");
    bool view_matches=!strcmp(text(view,"taskId"),codex_selected);
    if(codex_turn_pending&&codex_reply_expected[0]&&view_matches&&!strcmp(codex_reply_expected,text(view,"turnId")))codex_turn_pending=false;
    bool new_turn=view_matches&&strcmp(codex_reply_turn,text(view,"turnId"));
    if(view_matches&&!codex_turn_pending&&!codex_nav_pending) {
        codex_reply_page=(int)num(view,"page");codex_reply_total=(int)num(view,"pageCount");
        snprintf(codex_reply_turn,sizeof(codex_reply_turn),"%s",text(view,"turnId"));
    }
    if(codex_reply_selected[0]&&!codex_turn_pending&&!codex_nav_pending&&view_matches)
        snprintf(codex_reply_selected,sizeof(codex_reply_selected),"%s",text(view,"turnId"));
    if(view_matches) {
        bool previous=cJSON_IsTrue(item(view,"hasPrevious")),next=cJSON_IsTrue(item(view,"hasNext"));
        if(previous)lv_obj_remove_state(codex_turn_older,LV_STATE_DISABLED);else lv_obj_add_state(codex_turn_older,LV_STATE_DISABLED);
        if(next)lv_obj_remove_state(codex_turn_newer,LV_STATE_DISABLED);else lv_obj_add_state(codex_turn_newer,LV_STATE_DISABLED);
        if(cJSON_IsTrue(item(view,"history"))) {
            ui_text_fmt(codex_reply_status,"历史回复 · %s",text(view,"startedLabel"));
            tint(codex_reply_status,MUTED);
        }
    }
    const char *body=has_response?response->valuestring:"正在读取会话动态…";
    if(!codex_turn_pending&&!codex_nav_pending&&strcmp(lv_label_get_text(codex_reply_text),body)) {
        int y=lv_obj_get_scroll_y(codex_reply_scroll);
        bool bottom=lv_obj_get_scroll_bottom(codex_reply_scroll)<24;
        codex_reply_set_text(body);lv_obj_update_layout(codex_reply_scroll);
        if(codex_reply_follow&&(bottom||new_turn||codex_reply_jump))
            lv_obj_scroll_to_y(codex_reply_scroll,LV_COORD_MAX,LV_ANIM_OFF);
        else lv_obj_scroll_to_y(codex_reply_scroll,codex_reply_jump?0:y,LV_ANIM_OFF);
        codex_reply_jump=false;
    }
    if(!codex_nav_pending&&!codex_turn_pending&&view_matches&&codex_restore_y>=0) {
        lv_obj_update_layout(codex_reply_scroll);
        lv_obj_scroll_to_y(codex_reply_scroll,codex_restore_y,LV_ANIM_OFF);codex_restore_y=-1;
    }
    if(view_matches&&!codex_nav_pending&&!codex_turn_pending&&codex_public_reply(view,body)&&current_page==8&&
       !lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN)&&codex_notices) {
        for(int i=0;i<80;i++)if(!strcmp(codex_notices[i].task,codex_selected)&&
            !strcmp(codex_notices[i].turn,codex_reply_turn)){
                codex_notice_read();
                codex_read_acknowledge(codex_selected,codex_reply_turn,text(view,"readReceipt"),text(item(shown,"pcInput"),"session"));
                break;
            }
    }
    bool multiple=codex_reply_total>1;
    visible(codex_reply_older,multiple);visible(codex_reply_newer,multiple);
    if(multiple)ui_text_fmt(codex_reply_position,"%d / %d",codex_reply_page<0?1:codex_reply_page+1,codex_reply_total);
    else ui_text(codex_reply_position,codex_reply_selected[0]?"历史回复":"自动更新");
    lv_obj_set_x(codex_reply_position,752);
    lv_obj_set_width(codex_reply_position,100);
    visible(codex_reply_latest,!codex_reply_follow||codex_reply_selected[0]);
    if(codex_waiting)ui_text(codex_reply_status,"正在发送…");
    int button_page=codex_nav_pending&&!codex_turn_pending?codex_requested_page:codex_reply_page;
    if(button_page<=0)lv_obj_add_state(codex_reply_older,LV_STATE_DISABLED);else lv_obj_remove_state(codex_reply_older,LV_STATE_DISABLED);
    if(button_page>=codex_reply_total-1)lv_obj_add_state(codex_reply_newer,LV_STATE_DISABLED);else lv_obj_remove_state(codex_reply_newer,LV_STATE_DISABLED);
    if(codex_showing_cache)ui_text(codex_reply_status,codex_reply_follow?"缓存 · 正在更新":"历史回复 · 已缓存");
    if(codex_nav_timed_out)ui_text(codex_reply_status,"读取超时 · 可重新翻页");
    if(codex_nav_pending)ui_text(codex_reply_status,codex_reply_follow?"正在回到最新…":"正在翻页…");
    if(codex_nav_pending) {
        lv_obj_add_state(codex_reply_older,LV_STATE_DISABLED);lv_obj_add_state(codex_reply_newer,LV_STATE_DISABLED);
        lv_obj_add_state(codex_turn_older,LV_STATE_DISABLED);lv_obj_add_state(codex_turn_newer,LV_STATE_DISABLED);
        if(codex_reply_page>0&&codex_cache_index(false,codex_reply_turn,codex_reply_page-1)>=0)lv_obj_remove_state(codex_reply_older,LV_STATE_DISABLED);
        if(codex_reply_page+1<codex_reply_total&&codex_cache_index(false,codex_reply_turn,codex_reply_page+1)>=0)lv_obj_remove_state(codex_reply_newer,LV_STATE_DISABLED);
        if(codex_cache_turn_index(codex_reply_turn,-1)>=0)lv_obj_remove_state(codex_turn_older,LV_STATE_DISABLED);
        if(codex_cache_turn_index(codex_reply_turn,1)>=0)lv_obj_remove_state(codex_turn_newer,LV_STATE_DISABLED);
    }
    if(codex_nav_pending&&codex_reply_follow)lv_obj_add_state(codex_reply_latest,LV_STATE_DISABLED);
    else lv_obj_remove_state(codex_reply_latest,LV_STATE_DISABLED);
    lv_obj_t *status_labels[]={codex_receipt,codex_reply_status};
    for(int i=0;i<2;i++) {
        bool running=strstr(lv_label_get_text(status_labels[i]),"运行")!=NULL;
        ui_bg_color(status_labels[i],lv_color_hex(0x23533B),0);
        ui_bg_opa(status_labels[i],LV_OPA_TRANSP,0);
        tint(status_labels[i],running?0xA6F2BA:MUTED);
    }
    codex_notice_style();
}
static void render(void) {
    if (!shown)
        return;
    for (int i = 0; i < 2; i++) {
        cJSON *model = item(shown, i ? "codex" : "claude");
        ui_bg_color(pet_dot[i], lv_color_hex(activity_color(model)), 0);
        ui_text_fmt(pet_labels[i], "%s   %s", i ? "Codex" : "Claude", state(model));
        const char *pet_state=text(model,"state");
        ui_text(pet_hint[i],yes(model,"needsInput")?"等待你的操作":!strcmp(pet_state,"working")?"正在工作":!strcmp(pet_state,"idle")?"休息中，等待下一项任务":!strcmp(pet_state,"offline")?"未运行":"状态待确认");
    }
    cJSON *w = item(shown, "weather");
    char t[24], h[24], pm[24], aq[24];
    value(w, "temperature", t, sizeof(t), " °C");
    value(w, "humidity", h, sizeof(h), "%");
    value(w, "pm25", pm, sizeof(pm), "");
    value(w, "airQualityIndex", aq, sizeof(aq), "");
    ui_text(home_temperature,t);
    ui_text_fmt(home_weather,"%s · %s",text(w,"city"),text(w,"condition"));
    render_quota();
    overview_domestic();
    if(current_page==12)calendar_render();
    render_stocks();
    char hero_temp[24];value(w,"temperature",hero_temp,sizeof(hero_temp),"°");
    ui_text(weather_temp,hero_temp);
    ui_text(weather_condition,text(w,"condition"));
    weather_temperature_render(w);
    ui_text(weather_facts[0], h);
    ui_text(weather_facts[1], pm);
    ui_text(weather_facts[2], aq);
    char pressure[24];value(w,"pressureHpa",pressure,sizeof(pressure),"");ui_text(weather_facts[3],pressure);
    ui_text(weather_pressure_kind,!strcmp(text(w,"pressureKind"),"station")?"站点气压 · hPa":!strcmp(text(w,"pressureKind"),"sea_level")?"海平面气压 · hPa":"hPa");
    const char *standard=text(w,"airQualityStandard");
    ui_text(weather_aqi_title,!strcmp(standard,"cn-mee")?"空气质量 · 中国 AQI":!strcmp(standard,"us-epa")?"空气质量 · 美国 AQI":!strcmp(standard,"qaqi")?"空气质量 · 和风指数":"空气质量指数");
    ui_text(weather_aqi_category,text(w,"airQualityLabel"));
    lv_bar_set_value(weather_humidity, percentage(w, "humidity"), LV_ANIM_OFF);
    double aqi = num(w, "airQualityIndex");
    ui_text_color(weather_aqi_category,
                              lv_color_hex(aqi <= 50    ? GREEN
                                           : aqi <= 100 ? AMBER
                                                        : RED),
                              0);
    weather_icon_update(text(w, "condition"));
    weather_forecast_render(w);
    render_system();
    render_codex();
}
static void codex_select_task(const char *id) {
    if(codex_waiting||codex_voice_waiting||codex_send_queued)return;
    if(!id||strlen(id)!=36||!strcmp(codex_selected,id))return;
    if(!codex_save_draft())return;
    codex_bookmark_save();
    snprintf(codex_selected,sizeof(codex_selected),"%s",id);
    codex_load_draft();
    lv_obj_scroll_to_y(codex_reply_scroll,0,LV_ANIM_OFF);
    cJSON_Delete(codex_inline_reply);codex_inline_reply=NULL;
    codex_nav_revision++;codex_nav_queued=codex_nav_pending=false;
    codex_nav_timed_out=false;
    codex_reply_turn[0]=codex_reply_selected[0]=0;codex_turn_offset=0;codex_turn_pending=false;codex_reply_page=-1;codex_reply_total=1;codex_reply_follow=true;
    ui_text(codex_reply_status,"");
    last_codex_status[0]=0;
    codex_feedback_pinned=codex_submit_accepted=false;
    ui_text(codex_receipt,"");ui_text(codex_voice_hint,"");
    render_codex();
}
static void codex_selected_changed(lv_event_t *e) {
    int index=(int)(intptr_t)lv_event_get_user_data(e);
    if(index<0||index>=8||!codex_task_ids[index][0])return;
    codex_default_pending=false;
    codex_select_task(codex_task_ids[index]);
}
static bool codex_default_focus(bool all_projects) {
    if(codex_waiting||codex_voice_waiting||codex_send_queued||!codex_save_draft())return false;
    cJSON *latest=NULL,*task=NULL;
    cJSON_ArrayForEach(task,item(item(shown,"codexTasks"),"tasks")) {
        if(strlen(text(task,"id"))!=36)continue;
        if(!all_projects&&codex_project_selected[0]&&strcmp(text(task,"projectId"),codex_project_selected))continue;
        if(!latest||num(task,"updatedAt")>num(latest,"updatedAt"))latest=task;
    }
    if(!latest)return false;
    codex_default_pending=false;codex_restore_project=false;
    const char *project=text(latest,"projectId");
    snprintf(codex_project_selected,sizeof(codex_project_selected),"%s",strlen(project)==16?project:"");
    char id[37];snprintf(id,sizeof(id),"%s",text(latest,"id"));
    if(!strcmp(id,codex_selected))render_codex();else codex_select_task(id);
    return true;
}
static void codex_reply_refresh(lv_event_t *e) {
    if(codex_nav_timed_out)return;
    if(photo_open_pending||photo_flow||codex_voice_start_pending||codex_reading||codex_waiting||codex_voice_waiting||codex_syncing||codex_load_waiting||codex_send_queued||!codex_selected[0])return;
    codex_read_next=esp_timer_get_time()/1000+3000;
    int64_t age=esp_timer_get_time()/1000-last_received;
    const char *session=text(item(shown,"pcInput"),"session");
    cJSON *reply_view=codex_inline_reply?item(codex_inline_reply,"replyView"):item(item(shown,"codexTasks"),"replyView");
    bool cache_only=codex_reply_selected[0]&&!codex_nav_queued&&!codex_nav_pending&&
        codex_public_reply(reply_view,lv_label_get_text(codex_reply_text));
    // A pinned public page is locally readable. Only explicit navigation or
    // returning to live activity should request new content.
    if(cache_only){codex_read_next=esp_timer_get_time()/1000+15000;return;}
    const char *wait_reason=NULL;
    bool read_ready=rpc_read_link_ready();
    if(age<0||age>90000||!strcmp(session,"--")||(age>6000&&!read_ready))wait_reason="等待状态同步 · 自动重试";
    else if(!read_ready)wait_reason="连接暂不可用 · 自动重试";
    else if(!codex_remote_read_turn(codex_selected,session,(int64_t)num(shown,"epochMilliseconds")+age,cache_only||codex_reply_follow?-1:codex_nav_pending?codex_requested_page:codex_reply_page,cache_only?"":codex_turn_pending&&codex_reply_expected[0]?codex_reply_expected:codex_reply_selected,cache_only?0:codex_turn_offset))wait_reason="读取尚未开始 · 自动重试";
    if(wait_reason) {
        if(!cache_only)ui_text(codex_reply_status,wait_reason);
        if(!cache_only)codex_read_next=esp_timer_get_time()/1000+(codex_nav_pending?250:6000);
        return;
    }
    snprintf(codex_read_task,sizeof(codex_read_task),"%s",codex_selected);codex_reading=true;codex_result_is_read=true;
    codex_read_revision=codex_nav_revision;codex_nav_queued=false;codex_read_cache_only=cache_only;
    snprintf(codex_read_from_turn,sizeof(codex_read_from_turn),"%s",codex_reply_selected);codex_read_turn_offset=codex_turn_offset;
    snprintf(codex_read_session,sizeof(codex_read_session),"%s",session);
    if(e)ui_text(codex_reply_status,"正在读取动态…");
}
// Keep the latest requested destination while a network read is in flight.
// A late result must not replace a newer click or reset the requested page.
static void codex_navigation_start(void) {
    codex_cache_visible();
    codex_prefetch_armed=codex_turn_offset==-1;
    codex_prefetch_due=esp_timer_get_time()/1000+250;
    codex_nav_revision++;codex_nav_queued=codex_nav_pending=true;
    codex_prefetch_revision=codex_nav_revision;
    codex_nav_timed_out=false;
    codex_nav_started=esp_timer_get_time()/1000;
    codex_read_next=0;
    cJSON *cached=codex_turn_offset?codex_cache_copy(codex_cache_turn_index(codex_reply_selected,codex_turn_offset)):codex_cache_find(codex_reply_follow,codex_reply_selected,codex_requested_page);
    if(cached) {
        cJSON_Delete(codex_inline_reply);codex_inline_reply=cached;
        codex_nav_pending=false;codex_turn_pending=false;codex_turn_offset=0;codex_showing_cache=true;
        codex_requested_page=(int)num(item(cached,"replyView"),"page");
        if(!codex_reply_follow)snprintf(codex_reply_selected,sizeof(codex_reply_selected),"%s",text(item(cached,"replyView"),"turnId"));
        render_codex();
        // Explicit history navigation is cache-first. Live activity still
        // revalidates so new output is not hidden by an earlier snapshot.
        if(!codex_reply_follow) {
            codex_nav_queued=false;codex_read_next=esp_timer_get_time()/1000+15000;return;
        }
    }else {codex_showing_cache=false;render_codex();}
    codex_reply_refresh(NULL);
}
static void codex_reply_navigate(lv_event_t *e) {
    int direction=(int)(intptr_t)lv_event_get_user_data(e);
    if(codex_nav_pending&&direction&&codex_cache_index(false,codex_reply_turn,codex_reply_page+direction)<0)return;
    codex_restore_y=-1;
    int base=codex_reply_page;
    codex_reply_follow=direction==0;codex_reply_jump=true;
    codex_turn_pending=false;codex_turn_offset=0;codex_reply_expected[0]=0;
    if(!direction){codex_reply_selected[0]=0;codex_requested_page=-1;}
    else {
        snprintf(codex_reply_selected,sizeof(codex_reply_selected),"%s",codex_reply_turn);
        codex_requested_page=LV_CLAMP(0,base+direction,codex_reply_total-1);
    }
    codex_navigation_start();
}
static void codex_turn_navigate(lv_event_t *e) {
    int direction=(int)(intptr_t)lv_event_get_user_data(e);
    if(codex_waiting||!codex_reply_turn[0]||(codex_nav_pending&&codex_cache_turn_index(codex_reply_turn,direction)<0))return;
    codex_restore_y=-1;
    snprintf(codex_reply_selected,sizeof(codex_reply_selected),"%s",codex_reply_turn);
    codex_turn_offset=direction;
    codex_requested_page=0;codex_reply_follow=false;codex_reply_jump=true;codex_turn_pending=true;codex_reply_expected[0]=0;
    codex_navigation_start();
}
static void codex_reply_open_turn(lv_event_t *e,const char *turn) {
    if(codex_voice_waiting)return;
    (void)e;
    codex_keyboard_done(NULL);
    visible(codex_reply_overlay,true);
    lv_obj_move_foreground(codex_reply_overlay);
    codex_reply_selected[0]=0;codex_turn_offset=0;codex_turn_pending=false;codex_reply_follow=true;codex_reply_jump=true;
    codex_restore_y=-1;
    bool latest=codex_open_latest||codex_waiting||codex_send_queued;codex_open_latest=false;
    // The green new-reply action explicitly opens latest; ordinary reopening
    // restores a bookmark without consuming a newer unread reply.
    if(codex_notices)for(int i=0;i<80;i++)if(!strcmp(codex_notices[i].task,codex_selected)&&codex_notices[i].unread)latest=true;
    if(codex_waiting||codex_send_queued) {
        ui_text(codex_reply_status,codex_send_queued?"正在准备发送…":"正在发送…");
        codex_reply_set_text("消息正在交给电脑 Codex，确认送达后会自动更新动态。");
        codex_read_next=0;
        return;
    }
    codex_requested_page=-1;
    if(!latest&&codex_bookmarks)for(int i=0;i<32;i++)if(!strcmp(codex_bookmarks[i].task,codex_selected)) {
        codex_bookmark *mark=&codex_bookmarks[i];
        if(!mark->follow) {
            snprintf(codex_reply_selected,sizeof(codex_reply_selected),"%s",mark->turn);
            codex_requested_page=mark->page;codex_reply_follow=false;codex_restore_y=mark->y;
        }
        mark->stamp=++codex_bookmark_stamp;break;
    }
    if(turn&&turn[0]) {
        snprintf(codex_reply_selected,sizeof(codex_reply_selected),"%s",turn);
        codex_requested_page=0;codex_reply_follow=false;codex_restore_y=-1;
    }
    codex_navigation_start();
}
static void codex_reply_open(lv_event_t *e) {codex_reply_open_turn(e,NULL);}
static void codex_read_finish(int64_t now) {
    if(codex_reading) {
        char read_status[96];int read_code=codex_remote_status(codex_read_task,read_status,sizeof(read_status));
        if(read_code!=-1) {
            codex_reading=false;
            if(strcmp(codex_read_session,text(item(shown,"pcInput"),"session"))) {
                cJSON_Delete(codex_remote_take_read_view());return;
            }
            if(!strcmp(codex_read_task,codex_selected)) {
                cJSON *direct=codex_remote_take_read_view();
                if(direct) {
                    codex_cache_reply(direct);
                    if(read_code==200&&codex_read_turn_offset&&!strcmp(text(direct,"taskId"),codex_read_task)) {
                        const char *target=text(item(direct,"replyView"),"turnId");
                        codex_cache_edge(codex_read_task,codex_read_from_turn,target,codex_read_turn_offset);
                        codex_cache_edge(codex_read_task,target,codex_read_from_turn,-codex_read_turn_offset);
                    }
                }
                if(codex_read_revision!=codex_nav_revision){
                    cJSON_Delete(direct);
                    if(codex_nav_queued) {
                        codex_read_next=0;
                        int cached=codex_turn_offset?codex_cache_turn_index(codex_reply_selected,codex_turn_offset):
                            codex_cache_index(codex_reply_follow,codex_reply_selected,codex_requested_page);
                        // A click during prefetch may now be satisfied by that
                        // same batch. Do not queue a second request for it.
                        if(cached>=0&&!codex_reply_follow)codex_navigation_start();
                    }
                }
                else if(codex_read_cache_only){cJSON_Delete(direct);codex_read_next=now+15000;}
                else if(read_code==200) {
                    codex_showing_cache=false;
                    codex_nav_pending=false;
                    if(direct&&!strcmp(text(direct,"taskId"),codex_selected)) {
                        cJSON_Delete(codex_inline_reply);codex_inline_reply=direct;direct=NULL;codex_turn_pending=false;
                        if(!codex_reply_follow)snprintf(codex_reply_selected,sizeof(codex_reply_selected),"%s",text(item(codex_inline_reply,"replyView"),"turnId"));
                    }else if(codex_turn_pending)codex_remote_read_result(codex_reply_expected,sizeof(codex_reply_expected));
                    cJSON_Delete(direct);codex_turn_offset=0;render_codex();
                    // Start the refresh interval after completion. A slow BLE
                    // read must not immediately trigger another read on stale state.
                    codex_read_next=now+(codex_reply_selected[0]&&!codex_reply_follow?15000:3000);
                }else {
                    cJSON_Delete(direct);
                    ui_text(codex_reply_status,read_code==429?"正在翻页…":"连接稍慢 · 正在重试");
                    codex_read_next=now+(read_code==429?200:1500);
                }
            }
        }
    }
}
// One bounded refill per explicit older-turn navigation. Walk the cached chain
// before requesting, so the batch extends its oldest edge, not the visible page.
static void codex_prefetch_tick(int64_t now) {
    if(!codex_prefetch_armed||now<codex_prefetch_due)return;
    if(codex_prefetch_revision!=codex_nav_revision||current_page!=8||codex_reply_follow||lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN)) {
        codex_prefetch_armed=false;return;
    }
    if(codex_voice_start_pending||codex_reading||codex_nav_pending||codex_nav_queued||codex_waiting||codex_voice_waiting||
       codex_syncing||codex_load_waiting||codex_send_queued||ota_update_busy()||gesture_active())return;
    cJSON *view=item(codex_inline_reply,"replyView");
    if(strcmp(text(view,"taskId"),codex_selected)||!codex_public_reply(view,lv_label_get_text(codex_reply_text))) {
        codex_prefetch_armed=false;return;
    }
    const char *boundary=text(view,"turnId");
    for(int depth=0;depth<3;depth++) {
        int index=codex_cache_turn_index(boundary,-1);
        if(index<0)break;
        if(depth==2){codex_prefetch_armed=false;return;} // Three older turns remain.
        view=item(codex_reply_cache[index].value,"replyView");boundary=text(view,"turnId");
    }
    if(!yes(view,"hasPrevious")){codex_prefetch_armed=false;return;}
    const char *session=text(item(shown,"pcInput"),"session");
    if(!strcmp(codex_prefetch_task,codex_selected)&&!strcmp(codex_prefetch_boundary,boundary)&&
       !strcmp(codex_prefetch_session,session)&&now-codex_prefetch_attempted_at<30000) {
        codex_prefetch_armed=false;return;
    }
    int64_t age=now-last_received;
    if(age<0||age>90000||!strcmp(session,"--")||!rpc_read_link_ready())return;
    if(!codex_remote_read_turn(codex_selected,session,(int64_t)num(shown,"epochMilliseconds")+age,0,boundary,-1))return;
    snprintf(codex_read_task,sizeof(codex_read_task),"%s",codex_selected);
    snprintf(codex_read_session,sizeof(codex_read_session),"%s",session);
    snprintf(codex_read_from_turn,sizeof(codex_read_from_turn),"%s",boundary);
    snprintf(codex_prefetch_task,sizeof(codex_prefetch_task),"%s",codex_selected);
    snprintf(codex_prefetch_boundary,sizeof(codex_prefetch_boundary),"%s",boundary);
    snprintf(codex_prefetch_session,sizeof(codex_prefetch_session),"%s",session);
    codex_prefetch_attempted_at=now;codex_prefetch_armed=false;
    codex_reading=codex_result_is_read=codex_read_cache_only=true;
    codex_read_revision=codex_nav_revision;codex_read_turn_offset=-1;
}
static void codex_navigation_timeout(int64_t now) {
    if(!codex_nav_pending||now-codex_nav_started<12000)return;
    // Release navigation without cancelling the transport. A late authenticated
    // result may populate the cache, but its revision cannot replace this view.
    codex_nav_revision++;codex_nav_queued=codex_nav_pending=codex_turn_pending=false;
    codex_turn_offset=0;codex_reply_expected[0]=0;codex_nav_timed_out=true;
    codex_prefetch_armed=false;
    codex_requested_page=codex_reply_page;
    if(codex_reply_turn[0]) {
        snprintf(codex_reply_selected,sizeof(codex_reply_selected),"%s",codex_reply_turn);
        codex_reply_follow=false;
    }
    render_codex();
}
static void codex_reply_close(lv_event_t *e) {
    (void)e;
    codex_prefetch_armed=false;
    codex_bookmark_save();
    visible(codex_reply_overlay,false);
}
static void codex_project_changed(lv_event_t *e) {
    if(codex_waiting||codex_voice_waiting||codex_send_queued)return;
    int index=lv_dropdown_get_selected(lv_event_get_target(e));
    if(index<0||index>=codex_project_count)return;
    codex_default_pending=false;
    if(!codex_save_draft())return;
    codex_bookmark_save();
    snprintf(codex_project_selected,sizeof(codex_project_selected),"%s",codex_project_ids[index]);
    codex_selected[0]=0;last_codex_status[0]=0;
    codex_feedback_pinned=codex_submit_accepted=false;
    codex_load_draft();
    visible(codex_reply_overlay,false);ui_text(codex_reply_status,"");
    ui_text(codex_receipt,"");ui_text(codex_voice_hint,"");
    render_codex();
}
static void codex_input_focus(lv_event_t *e) {
    (void)e;
    if(codex_waiting||codex_voice_waiting)return;
    codex_draft *d=codex_draft_for(codex_selected,false);if(d&&d->request[0]){ui_text(codex_receipt,"请先查询上一条消息是否送达");return;}
    lv_obj_set_height(codex_input,112);
    lv_obj_remove_flag(codex_keyboard, LV_OBJ_FLAG_HIDDEN);
    lv_keyboard_set_textarea(codex_keyboard, codex_input);
    tab5_ime_show(true);
}
static void codex_keyboard_done(lv_event_t *e) {
    (void)e;
    lv_obj_set_height(codex_input,250);
    lv_obj_add_flag(codex_keyboard, LV_OBJ_FLAG_HIDDEN);
    tab5_ime_show(false);
    codex_images_refresh();
}
static void codex_begin_action(const char *operation) {
    if(codex_waiting||codex_voice_waiting)return;
    codex_draft *d=codex_draft_for(codex_selected,true);
    if(!d)return;
    bool querying=d->request[0]!=0;
    if(!querying&&!codex_save_draft())return;
    if(!querying&&strcmp(operation,"interrupt")&&(!d->text||!*d->text)&&!d->images[0][0]){ui_text(codex_receipt,"请先输入内容或添加照片");return;}
    if(!querying) {
        codex_uuid(d->request);snprintf(d->operation,sizeof(d->operation),"%s",operation);
        snprintf(d->turn,sizeof(d->turn),"%s",strcmp(operation,"send")?text(item(item(shown,"codexTasks"),"control"),"turnId"):"");
        if(!codex_checkpoint_write()){d->request[0]=0;ui_text(codex_receipt,"草稿未保存，未发送");return;}
    }
    if(!codex_request(querying?"receipt":operation,d->id,!strcmp(operation,"interrupt")?"":d->text?d->text:"",d->request,d->turn)) {
        if(!querying){d->request[0]=0;codex_checkpoint_write();}
        ui_text(codex_receipt,"请求未启动，请检查连接");return;
    }
    snprintf(codex_action,sizeof(codex_action),"%s",d->operation);codex_querying=querying;
    codex_waiting=true;codex_result_is_read=false;codex_feedback_pinned=true;codex_submit_accepted=false;
    ui_text(codex_voice_hint,"");
    snprintf(codex_receipt_before_send,sizeof(codex_receipt_before_send),"%s",text(item(item(shown,"codexTasks"),"receipts"),codex_selected));
    ui_text(codex_receipt,querying?"正在核对送达记录…":!strcmp(operation,"interrupt")?"正在请求停止…":"正在发送到 Codex…");
    ui_text(lv_obj_get_child(codex_send_button,0),querying?"查询中…":"发送中…");
    lv_obj_add_state(codex_list,LV_STATE_DISABLED);lv_obj_add_state(codex_project_picker,LV_STATE_DISABLED);
    lv_obj_add_state(codex_input,LV_STATE_DISABLED);lv_obj_add_state(codex_send_button,LV_STATE_DISABLED);
    codex_control_style();codex_keyboard_done(NULL);
}
static void codex_send(lv_event_t *e) {
    (void)e;if(codex_waiting||codex_send_queued)return;
    if(codex_voice_start_pending){ui_text(codex_voice_hint,"请先取消录音准备，再发送消息");return;}
    if(codex_voice_waiting){ui_text(codex_voice_hint,"正在结束语音，请等文字确认后发送");return;}
    if(codex_reading||codex_syncing||codex_load_waiting) {
        // One HTTP worker keeps memory bounded. Queue the explicit user click
        // behind a read/save rather than discard it or block the LVGL task.
        if(!codex_save_draft())return;
        codex_send_queued=true;ui_text(codex_receipt,"正在准备发送…");
        ui_text(lv_obj_get_child(codex_send_button,0),"发送中…");
        lv_obj_add_state(codex_input,LV_STATE_DISABLED);lv_obj_add_state(codex_send_button,LV_STATE_DISABLED);return;
    }
    codex_begin_action(codex_running()?"steer":"send");
}
static void codex_interrupt(lv_event_t *e) {
    (void)e;if(codex_reading||codex_syncing||codex_send_queued||!codex_running())return;
    codex_draft *d=codex_draft_for(codex_selected,false);if(d&&d->request[0])return;
    codex_begin_action("interrupt");
}
static void codex_apply_key(const char *key, uint8_t modifier) {
        if (codex_waiting || codex_voice_waiting || codex_send_queued || lv_obj_has_state(codex_input,LV_STATE_DISABLED) || (modifier & 5)) return;
        tab5_ime_finish();if(tab5_ime_pending()[0])return;
        if (!strcmp(key, "backspace") || !strcmp(key, "BACKSPACE") || key[0] == '\b')
            lv_textarea_delete_char(codex_input);
        else if (!strcmp(key, "enter") || !strcmp(key, "ENTER") || key[0] == '\r')
            lv_textarea_add_text(codex_input, "\n");
        else if (key[0] >= 32 && key[0] < 127 && key[1] == 0)
            lv_textarea_add_text(codex_input, key);
}
static void codex_voice_pending_clear(const char *message) {
    bool desktop=codex_voice_start_desktop;codex_voice_start_pending=false;
    lv_obj_remove_state(codex_voice_button,LV_STATE_DISABLED);
    visible(codex_voice_cancel,false);visible(codex_camera_button,true);visible(codex_send_button,true);
    if(message){ui_text(codex_voice_hint,message);if(desktop)quick_voice_pending_result(message);}
}
static void codex_voice_request(int action) {
    if(codex_voice_start_pending) {
        if(action==2)codex_voice_pending_clear("已取消录音准备");
        else ui_text(codex_voice_hint,codex_voice_start_note);
        return;
    }
    if(codex_voice_waiting){voice_remote_command(action?action:1);return;}
    if(photo_flow||camera_preview_busy()||photo_state()==1){ui_text(codex_voice_hint,"照片处理完成后即可录音");return;}
    if(action||codex_waiting||codex_send_queued)return;
    if(codex_reading||codex_syncing||codex_load_waiting) {
        if(!codex_selected[0]){ui_text(codex_voice_hint,"请先选择会话");return;}
        codex_voice_start_pending=true;codex_voice_start_desktop=quick_visible;
        codex_voice_start_page=current_page;codex_voice_start_at=esp_timer_get_time()/1000;
        snprintf(codex_voice_start_task,sizeof(codex_voice_start_task),"%s",codex_selected);
        snprintf(codex_voice_start_session,sizeof(codex_voice_start_session),"%s",text(item(shown,"pcInput"),"session"));
        snprintf(codex_voice_start_note,sizeof(codex_voice_start_note),"正在等待当前读取结束，可取消准备");
        ui_text(codex_voice_hint,codex_voice_start_note);
        if(!quick_visible){lv_obj_add_state(codex_voice_button,LV_STATE_DISABLED);visible(codex_voice_cancel,true);lv_obj_remove_state(codex_voice_cancel,LV_STATE_DISABLED);visible(codex_camera_button,false);visible(codex_send_button,false);}
        return;
    }
    if(!quick_visible&&lv_obj_has_state(codex_input,LV_STATE_DISABLED)){ui_text(codex_voice_hint,"请先确认上一条消息的送达结果");return;}
    codex_draft *voice_draft=codex_draft_for(codex_selected,false);
    if(quick_visible&&voice_draft&&voice_draft->request[0]){ui_text(codex_voice_hint,"此会话有待确认消息，请先返回任务页查询");return;}
    if(!quick_visible){tab5_ime_finish();if(tab5_ime_pending()[0])return;}
    int64_t age=esp_timer_get_time()/1000-last_received;
    const char *session=text(item(shown,"pcInput"),"session");
    const char *base=quick_visible?"":lv_textarea_get_text(codex_input);
    tab5_ui_voice_age_ms=age<0?-1:age>3600000?3600000:(int)age;
    if(!codex_selected[0]){tab5_ui_voice_check=1;ui_text(codex_voice_hint,"请先点选左侧会话");return;}
    if(age<0||age>6000){tab5_ui_voice_check=2;ui_text(codex_voice_hint,"电脑状态未刷新，请稍后重试");return;}
    if(!base||strlen(base)>2000){tab5_ui_voice_check=3;ui_text(codex_voice_hint,"输入内容过长，请先精简");return;}
    if(!strcmp(session,"--")||strlen(session)>=40){tab5_ui_voice_check=4;ui_text(codex_voice_hint,"电脑桥接会话未就绪");return;}
    bool usb_ready=rpc_link_select()==LINK_USB;
    if(!usb_ready&&!wifi_connected&&!ble_voice_ready()){tab5_ui_voice_check=5;ui_text(codex_voice_hint,"请连接电脑语音服务");return;}
    device_config cfg=device_get_config();
    if(!cfg.paired||(!usb_ready&&!ble_voice_ready()&&(!cfg.host[0]||cfg.port<=0))){tab5_ui_voice_check=6;ui_text(codex_voice_hint,"电脑语音服务地址未就绪");return;}
    tab5_draw_cache_drop();
    screen_preview_cache_reclaim(true);
    if(!voice_remote_start(codex_selected,session,(int64_t)num(shown,"epochMilliseconds")+age)) {
        tab5_ui_voice_check=7;
#ifndef TAB5_UI_PREVIEW
        ui_text(codex_voice_hint,tab5_voice_start_error==2?"上一段语音仍在结束，请稍后重试":tab5_voice_start_error==4?"语音服务未就绪，请重启 TAB5":"语音连接状态已变化，请重试");
#else
        ui_text(codex_voice_hint,"语音任务未启动，请稍后重试");
#endif
        return;
    }
    tab5_ui_voice_check=0;
    codex_voice_desktop=quick_visible;
    if(codex_voice_desktop){codex_voice_waiting=true;return;}
    codex_feedback_pinned=codex_submit_accepted=false;
    ui_text(codex_receipt,"");
    snprintf(codex_voice_base,sizeof(codex_voice_base),"%s",base);codex_voice_waiting=true;
    // Anchor this take after IME commit, before hiding/disabling the editor.
    // LVGL cursor positions count characters; draft buffers contain UTF-8 bytes.
    codex_voice_cursor=lv_textarea_get_cursor_pos(codex_input);
    codex_voice_insert_byte=lv_text_encoded_get_byte_id(codex_voice_base,codex_voice_cursor);
    codex_voice_started=0;
    ui_text(codex_voice_hint,"正在准备，请稍候…");
    lv_obj_add_state(codex_list,LV_STATE_DISABLED);lv_obj_add_state(codex_input,LV_STATE_DISABLED);
    lv_obj_add_state(codex_project_picker,LV_STATE_DISABLED);
    lv_obj_add_state(codex_send_button,LV_STATE_DISABLED);codex_keyboard_done(NULL);
}
static bool codex_voice_stopped_on_press;
static void codex_voice_tick(void);
static void codex_voice_pressed(lv_event_t *e) {
    (void)e;codex_voice_stopped_on_press=false;
    if(!codex_voice_waiting)return;
    // Ending capture is local and irreversible for this take. Accept the down
    // event so a small finger movement or a late reply cannot swallow release.
    codex_voice_stopped_on_press=true;voice_remote_command(1);codex_voice_tick();
}
static void codex_voice_action(lv_event_t *e) {
    lv_indev_t *input=lv_event_get_indev(e);
    if(lv_event_get_target(e)==codex_voice_button&&codex_voice_stopped_on_press&&input&&lv_indev_get_type(input)==LV_INDEV_TYPE_POINTER) {
        codex_voice_stopped_on_press=false;return; // Never restart on this release.
    }
    codex_voice_request((int)(intptr_t)lv_event_get_user_data(e));
}
static void codex_voice_pending_tick(int64_t now) {
    if(!codex_voice_start_pending)return;
    codex_draft *d=codex_draft_for(codex_selected,false);
    if(current_page!=codex_voice_start_page||quick_visible!=codex_voice_start_desktop||saver||
       strcmp(codex_selected,codex_voice_start_task)||strcmp(text(item(shown,"pcInput"),"session"),codex_voice_start_session)||
       codex_waiting||codex_send_queued||!d||d->request[0]||photo_flow||ota_update_busy()) {
        codex_voice_pending_clear("会话或连接已变化，录音准备已取消");return;
    }
    if(now<codex_voice_start_at||now-codex_voice_start_at>=20000){codex_voice_pending_clear("后台读取等待较久，录音尚未开始，请稍后重试");return;}
    if(codex_reading||codex_syncing||codex_load_waiting)return;
    bool desktop=codex_voice_start_desktop;codex_voice_pending_clear(NULL);codex_voice_request(0);
    if(desktop){if(codex_voice_waiting)quick_voice_started();else quick_voice_pending_result(lv_label_get_text(codex_voice_hint));}
}
static void codex_voice_set_text(const char *value,int cursor) {
    if(strcmp(lv_textarea_get_text(codex_input),value)) {
        // This programmatic insertion is already bounded to 2000 UTF-8 bytes.
        // A nonzero LVGL character limit rebuilds text one character at a time,
        // emitting a draft edit for every prefix. Apply one complete result and
        // restore the manual-input limit before the next input event can run.
        uint32_t limit=lv_textarea_get_max_length(codex_input);
        lv_textarea_set_max_length(codex_input,0);
        lv_textarea_set_text(codex_input,value);
        lv_textarea_set_max_length(codex_input,limit);
    }
    lv_textarea_set_cursor_pos(codex_input,cursor);
}
static void codex_voice_tick(void) {
    voice_remote_state *voice=&ui_voice_work->codex_tick;voice_remote_get(voice);
    if(!codex_voice_waiting)return;
    // Quick Console owns a desktop take. Never mirror it into the TAB5
    // composer, including partial results, failed delivery and cancellation.
    if(codex_voice_desktop){if(!voice->busy)codex_voice_waiting=false;return;}
    // The take belongs to its task, not to the focused widget or visible page.
    // Navigation must not cancel it; only explicit voice controls end capture.
    if(strcmp(voice->task_id,codex_selected))return;
    bool recording=voice->busy&&voice->ready&&!strcmp(voice->phase,"recording");
    if(recording) {
        if(!codex_voice_started)codex_voice_started=esp_timer_get_time()/1000;
        int seconds=(int)((esp_timer_get_time()/1000-codex_voice_started)/1000);
        ui_text_fmt(codex_voice_hint,voice->silent?"未检测到声音 · %s  %02d:%02d":"可以说话了 · %s  %02d:%02d",!strcmp(voice->source,"dji")?"大疆":"TAB5",seconds/60,seconds%60);
    } else if(voice->busy&&(!strcmp(voice->phase,"preparing")||!strcmp(voice->phase,"recording"))) {
        ui_text(codex_voice_hint,"正在准备，请稍候…");
    } else if(voice->busy&&!strcmp(voice->phase,"draining"))ui_text(codex_voice_hint,"正在结束收音…");
    else if(voice->busy&&!strcmp(voice->phase,"recognizing"))ui_text(codex_voice_hint,"正在识别，请稍候…");
    else ui_text(codex_voice_hint,voice->message);
    tint(codex_voice_hint,recording?(voice->silent?0xE4C889:0xA1E5BD):voice->busy?0xE4C889:!strcmp(voice->phase,"error")?0xEFA98F:MUTED);
    visible(codex_voice_meter,recording);
    lv_bar_set_value(codex_voice_meter,voice->level,LV_ANIM_OFF);
    ui_text_font(codex_voice_hint,recording?&tab5_font_30:&tab5_font_24,0);
    visible(codex_mic_glyph,!recording);visible(codex_stop_glyph,recording);
    ui_bg_color(codex_voice_button,lv_color_hex(recording?0x365D48:voice->busy?0x534C39:0x354247),0);
    if(voice->busy&&!recording)lv_obj_add_state(codex_voice_button,LV_STATE_DISABLED);
    else lv_obj_remove_state(codex_voice_button,LV_STATE_DISABLED);
    if(recording&&!strcmp(voice->source,"dji"))lv_obj_remove_state(codex_voice_switch,LV_STATE_DISABLED);
    else lv_obj_add_state(codex_voice_switch,LV_STATE_DISABLED);
    visible(codex_voice_switch,recording&&!strcmp(voice->source,"dji"));
    visible(codex_camera_button,!voice->busy);
    visible(codex_voice_cancel,voice->busy);
    visible(codex_send_button,!voice->busy);
    if(voice->busy)lv_obj_remove_state(codex_voice_cancel,LV_STATE_DISABLED);
    else lv_obj_add_state(codex_voice_cancel,LV_STATE_DISABLED);
    if(!strcmp(voice->phase,"cancelled")) {
        codex_voice_set_text(codex_voice_base,codex_voice_cursor);
    }
    else if(voice->text[0]) {
        size_t base=strlen(codex_voice_base),extra=strlen(voice->text);
        if(base+extra<=2000) {
            // Each reply is the full current transcript, not an append-only delta.
            char *joined=ui_voice_work->codex_joined;
            memcpy(joined,codex_voice_base,codex_voice_insert_byte);
            memcpy(joined+codex_voice_insert_byte,voice->text,extra);
            memcpy(joined+codex_voice_insert_byte+extra,codex_voice_base+codex_voice_insert_byte,base-codex_voice_insert_byte+1);
            codex_voice_set_text(joined,codex_voice_cursor+lv_text_get_encoded_length(voice->text));
        } else ui_text(codex_voice_hint,"内容过长，请在电脑草稿中整理后发送");
    }
    if(!voice->busy) {
        codex_voice_waiting=false;
        lv_obj_remove_state(codex_list,LV_STATE_DISABLED);lv_obj_remove_state(codex_input,LV_STATE_DISABLED);
        lv_obj_remove_state(codex_project_picker,LV_STATE_DISABLED);
        lv_obj_remove_state(codex_send_button,LV_STATE_DISABLED);
    }
    if(voice->busy)lv_obj_add_state(codex_reply_button,LV_STATE_DISABLED);
    else lv_obj_remove_state(codex_reply_button,LV_STATE_DISABLED);
}
static void codex_physical_input(void) {
    for (int i = 0; i < 4; i++) {
        char key[10]; uint8_t modifier;
        if (!tab5_keyboard_poll(key, &modifier)) break;
        codex_apply_key(key, modifier);
    }
    ui_text(codex_physical, tab5_keyboard_present() ? "键盘已连接" : "");
}
static bool music_has_track(void) {
    cJSON *m=item(shown,"music"),*title=item(m,"title");
    if(yes(m,"playing"))return true;
    if(!cJSON_IsString(title))return false;
    const char *value=title->valuestring;
    while(*value==' '||*value=='\t'||*value=='\r'||*value=='\n')value++;
    return *value!=0;
}
static void music_empty_refresh(void) {
    bool has_track=music_has_track();
    visible(music_empty,!has_track);
    lv_obj_t *content[]={music_cover,music_state,music,music_alias,music_artist,music_album,music_time,music_duration,progress};
    for(unsigned i=0;i<sizeof(content)/sizeof(content[0]);i++)visible(content[i],has_track);
}
#include "ui_computer.h"
static int music_artwork_layout(void) {
    cJSON *m=item(shown,"music");
    double w=num(m,"artworkWidth"),h=num(m,"artworkHeight");
    bool wide=yes(m,"hasArtwork")&&w>0&&w<=32768&&h>0&&h<=32768&&w>h*1.25;
    int cover_width=wide?576:384,text_x=wide?658:466,text_width=wide?536:728;
    lv_obj_set_width(music_cover,cover_width);
    lv_obj_t *labels[]={music_state,music,music_alias,music_artist,music_album,music_time,music_duration,progress};
    for(int i=0;i<8;i++){lv_obj_set_x(labels[i],text_x);lv_obj_set_width(labels[i],text_width);}
    lv_obj_set_width(image_loading[2],cover_width-72);lv_obj_center(image_loading[2]);
    // Legacy 336-square images contain letterboxing. JPEG images already have
    // their native aspect ratio and use the card width without another zoom.
    bool native=pictures[2].width!=pictures[2].height;
    return wide?(native?560:(int)fmin(560.0,336.0*w/h)):384;
}
static void images_tick(int64_t elapsed) {
    int music_fit=music_artwork_layout();
    for (int i = 0; i < 3; i++) {
        asset_image next;
        bool updated;
        if(!xSemaphoreTake(device_mutex, 0)) continue;
        updated = assets_copy(i, pictures[i].revision, &next);
        int received = assets_progress(i);
        xSemaphoreGive(device_mutex);
        ui_text_fmt(image_loading[i], i < 2 ? "桌宠同步中 %d%%" : "封面同步中 %d%%", received);
        if (i == 2 && !yes(item(shown, "music"), "hasArtwork"))
            ui_text(image_loading[i], "暂未获取到封面");
        if (updated) {
            lv_image_cache_drop(&descriptors[i]);
            uint8_t *old = pictures[i].pixels;
            pictures[i] = next;
            last_frame[i] = -1;
            free(old);
        }
        asset_image *p = &pictures[i];
        if(i==2&&(!music_has_track()||!yes(item(shown,"music"),"hasArtwork"))) {
            visible(images[i],false);visible(image_loading[i],music_has_track());continue;
        }
        if (!p->pixels)
            continue;
        int frame = 0;
        if (i < 2 && !strcmp(text(item(shown, i ? "codex" : "claude"), "state"), "working")) {
            pet_elapsed[i] += elapsed;
            int duration = 0;
            for (int j = 0; j < p->frames; j++)
                duration += p->delays[j];
            int position = (int)(pet_elapsed[i] % duration);
            while (frame < p->frames - 1 && position >= p->delays[frame])
                position -= p->delays[frame++];
        }
        if (last_frame[i] != frame) {
            lv_image_cache_drop(&descriptors[i]);
            descriptors[i] = (lv_image_dsc_t){.header = {.magic = LV_IMAGE_HEADER_MAGIC,
                                                         .cf = LV_COLOR_FORMAT_RGB565,
                                                         .w = p->width,
                                                         .h = p->height,
                                                         .stride = p->width * 2},
                                              .data_size = p->width * p->height * 2,
                                              .data = p->pixels + (size_t)frame * p->width * p->height * 2};
            lv_image_set_src(images[i], &descriptors[i]);
            int longest = p->width > p->height ? p->width : p->height;
            int fit = i == 0 ? 280 : i == 1 ? 232 : music_fit;
            lv_image_set_scale(images[i], (uint32_t)(fit * 256 / longest));
            lv_obj_center(images[i]);
            last_frame[i] = frame;
        }
        if(i==2) {
            int longest=p->width>p->height?p->width:p->height;
            lv_image_set_scale(images[i],(uint32_t)(music_fit*256/longest));
            lv_obj_center(images[i]);
        }
        cJSON *expected = cJSON_GetArrayItem(item(shown, "assetIds"), i);
        char id[9];
        snprintf(id, sizeof(id), "%08x", (unsigned int)p->crc);
        if ((i < 2 && !shown) || (cJSON_IsString(expected) && !strcmp(id, expected->valuestring))) {
            lv_obj_remove_flag(images[i], LV_OBJ_FLAG_HIDDEN);
            lv_obj_add_flag(image_loading[i], LV_OBJ_FLAG_HIDDEN);
        } else {
            lv_obj_add_flag(images[i], LV_OBJ_FLAG_HIDDEN);
            lv_obj_remove_flag(image_loading[i], LV_OBJ_FLAG_HIDDEN);
        }
    }
}
static void sound_header_render(void) {
    if(!sound_header_icon)return;
    int volume=device_sound_volume();
    ui_text(sound_header_icon,volume==0?LV_SYMBOL_MUTE:volume<50?LV_SYMBOL_VOLUME_MID:LV_SYMBOL_VOLUME_MAX);
    tint(sound_header_icon,volume?INK:MUTED);
}
static void battery_render(battery_status power) {
    bool known=power.valid&&power.percent>=0;
    if(known)ui_text_fmt(battery_label,"%d",power.percent);
    else ui_text(battery_label,"--");
    uint32_t battery_color=known?GREEN:MUTED;
    tint(battery_label,known?INK:MUTED);ui_border_color(battery_outline,lv_color_hex(battery_color),0);
    ui_bg_color(battery_fill,lv_color_hex(battery_color),0);ui_bg_color(battery_cap,lv_color_hex(battery_color),0);
    visible(battery_charge,power.valid&&power.charging);
    int fill_width=known?(power.percent*60+99)/100:0;
    visible(battery_fill,fill_width>0);if(fill_width>0&&lv_obj_get_width(battery_fill)!=fill_width)lv_obj_set_width(battery_fill,fill_width);
}
static void tf_storage_refresh(void) {
    if(!tf_card_value)return;
    int state=sd_storage_state();
    ui_text(tf_card_value,state==1?"已就绪":state<0?"未检测到可用 TF 卡":"正在检测 TF 卡");
    tint(tf_card_value,state==1?GREEN:MUTED);
#if TAB5_LOCAL_ART
    char storage[160];gallery_storage_status(storage,sizeof(storage));
    ui_text(tf_storage_value,!tf_cache_enabled?"使用设备缓存":state==1?storage:"TF 卡不可用，使用设备缓存");
#endif
}
static void tf_cache_changed(lv_event_t *e) {
    bool requested=lv_dropdown_get_selected(lv_event_get_target(e))==1;
    if(requested==tf_cache_enabled)return;
    lv_obj_add_state(tf_cache_selector,LV_STATE_DISABLED);ui_text(tf_cache_feedback,"正在保存…");
    bool ok=ui_tf_cache_save(requested)&&ui_tf_cache_load()==requested;
    if(ok){tf_cache_enabled=requested;
#if TAB5_LOCAL_ART
        gallery_storage_enable(requested);
#endif
    }else lv_dropdown_set_selected(tf_cache_selector,tf_cache_enabled?1:0);
    ui_text(tf_cache_feedback,ok?"已保存":"保存失败，请重试");tint(tf_cache_feedback,ok?MUTED:0xEFA98F);
    lv_obj_remove_state(tf_cache_selector,LV_STATE_DISABLED);tf_storage_refresh();
}
#if TAB5_LOCAL_ART
static void gallery_sync_with_memory(void) {
#ifndef TAB5_UI_PREVIEW
    if(gallery_library_pending()&&gallery_request_ready()) {
        unsigned free_bytes=heap_caps_get_free_size(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
        const unsigned reserve=5u*1024u*1024u;
        if(free_bytes<reserve)tab5_draw_cache_trim_hidden(reserve-free_bytes);
    }
#endif
    gallery_sync_library();
}
#endif
static void clock_music(int64_t now, bool fresh) {
    if (!shown) {
        music_empty_refresh();
#if TAB5_LOCAL_ART
        // A mounted TF library needs no PC snapshot. Only its civil date comes
        // from the calibrated local RTC; never manufacture a current date.
        if(saver&&saver_is_gallery()) {
            struct tm offline;
            if(gallery_clock_local(now,&offline)) {
                saver_local_time=offline;saver_has_time=true;
                gallery_update(&offline);gallery_sync_with_memory();
            }else ui_text(gallery_hint,"请连接电脑校准日期，TF 图库已保留");
        }
#endif
        return;
    }
    static ui_time_state timeline;
    int64_t sample_ms=cJSON_IsNumber(item(shown,"epochMilliseconds"))?(int64_t)num(shown,"epochMilliseconds"):(int64_t)num(shown,"epochUtc")*1000;
    int64_t local_ms=ui_time_value(&timeline,sample_ms,last_received,now)+(int64_t)num(shown,"utcOffsetSeconds")*1000;
    time_t timestamp=(time_t)(local_ms/1000);
    struct tm local;
    gmtime_r(&timestamp, &local);
    saver_local_time=local;saver_has_time=local.tm_year>=100;
    // Art orientation must not wait for the next whole-second clock update.
    if(fresh&&saver_has_time)gallery_clock_sync(local_ms,now);
#if TAB5_LOCAL_ART
    if(fresh){gallery_sync_packs(item(item(shown,"connectionHealth"),"galleryPacks"));gallery_library_supported(num(item(shown,"connectionHealth"),"gallerySyncVersion")==1);}
    if(saver&&saver_is_gallery()){gallery_update(&local);gallery_sync_with_memory();return;}
    if(!saver&&saver_is_gallery()&&saver_has_time){
        char gallery_day[11];strftime(gallery_day,sizeof(gallery_day),"%Y-%m-%d",&local);
        gallery_prefetch_day(gallery_day);
    }
    gallery_sync_with_memory();
#endif
    // Picker thumbnails are snapshots; do not animate an obscured clock.
    if(!saver && current_page==7 && !timer_open() && lv_obj_has_flag(clock_picker,LV_OBJ_FLAG_HIDDEN))
        analog_update(&local,(int)(local_ms%1000));
    static int64_t rendered_second = -1, rendered_received = -1;
    if (rendered_second == timestamp && rendered_received == last_received)
        return;
    rendered_second = timestamp;
    rendered_received = last_received;
    clock_solar_refresh(timestamp);
    if(!saver&&current_page==1)quota_countdown(local_ms/1000-(int64_t)num(shown,"utcOffsetSeconds"));
    char t[20], date[20];
    strftime(t, sizeof(t), "%H:%M:%S", &local);
    strftime(date, sizeof(date), "%Y-%m-%d", &local);
    if(saver) {
        saver_update(&local);
        return;
    }
    ui_text(clock_label, t);
    char hours_minutes[8],seconds[4];strftime(hours_minutes,sizeof(hours_minutes),"%H:%M",&local);strftime(seconds,sizeof(seconds),":%S",&local);
    ui_text(clock_big,hours_minutes);ui_text(clock_seconds,seconds);
    clock_time_align();
    cJSON *calendar = item(shown, "calendar");
    bool today = !strcmp(date, text(calendar, "date")), tomorrow = !strcmp(date, text(calendar, "nextDate"));
    const char *lunar = today ? text(calendar, "lunar") : tomorrow ? text(calendar, "nextLunar") : "农历待联网更新";
    const char *term = today      ? text(calendar, "solarTermLabel")
                       : tomorrow ? text(calendar, "nextSolarTermLabel")
                                  : "节气待更新";
    const char *holiday = today      ? text(calendar, "holidayLabel")
                          : tomorrow ? text(calendar, "nextHolidayLabel")
                                     : "假期信息待更新";
    const char *days[] = {"星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六"};
    ui_text_fmt(date_label,"%s  %s",date,days[local.tm_wday]);
    ui_text_fmt(clock_lunar,"%s%s%s",lunar,term[0]?"   ":"",term);ui_text(clock_holiday,holiday);
    ui_text_fmt(home_date,"%d 年 %d 月 %d 日",local.tm_year+1900,local.tm_mon+1,local.tm_mday);
    ui_text_fmt(home_day,"%02d:%02d",local.tm_hour,local.tm_min);
    ui_text(home_weekday,days[local.tm_wday]);
    const char *today_prefix="今日";
    if(today && !strncmp(term,today_prefix,strlen(today_prefix)) && term[strlen(today_prefix)])
        ui_text_fmt(home_lunar,"%s · %s",lunar,term+strlen(today_prefix));
    else
        ui_text(home_lunar,lunar);
    calendar_set_today(local.tm_year+1900,local.tm_mon+1,local.tm_mday);
    if(!calendar_has_birthday)ui_text(home_holiday,holiday);
    static bool clock_sized;
    if(!clock_sized) {clock_layout();clock_sized=true;}
    music_empty_refresh();
    if(!music_has_track())return;
    cJSON *m = item(shown, "music");
    int duration = (int)num(m, "durationSeconds");
    bool timeline_available=!cJSON_IsFalse(item(m,"timelineAvailable"));
    int pos=ui_music_position(num(m,"elapsedSeconds"),duration,
                              timeline_available && fresh && yes(m,"playing"),now-last_received);
    const char *title=text(m,"title"),*alias=strchr(title,'\n');
    // Keep the original title and its localized alias on distinct rows.
    if(alias)ui_text_fmt(music,"%.*s",(int)(alias-title),title);
    else ui_text(music,title);
    ui_text(music_alias,alias?alias+1:"");
    visible(music_alias,alias&&alias[1]);
    lv_point_t title_size;
    lv_text_get_size(&title_size,lv_label_get_text(music),lv_obj_get_style_text_font(music,0),0,4,lv_obj_get_width(music),LV_TEXT_FLAG_NONE);
    int alias_y=82+(title_size.y<100?title_size.y:100)+8;
    lv_obj_set_y(music_alias,alias_y);
    int names_bottom=alias&&alias[1]?alias_y+32:178;
    if(names_bottom<178)names_bottom=178;
    lv_obj_set_y(music_artist,names_bottom+26);
    lv_obj_set_y(music_album,names_bottom+70);
    ui_text(music_artist, text(m, "artist"));
    ui_text(music_album, text(m, "album"));
    ui_text(music_state, yes(m, "playing") ? "播放中" : "已暂停");
    tint(music_state, yes(m, "playing") ? GREEN : MUTED);
    if(duration>0 && !timeline_available) {
        ui_text(music_time,"--:--");
        ui_text_fmt(music_duration,"%02d:%02d",duration/60,duration%60);
        lv_obj_add_flag(progress,LV_OBJ_FLAG_HIDDEN);
    } else if (duration > 0) {
        ui_text_fmt(music_time,"%02d:%02d",pos/60,pos%60);
        ui_text_fmt(music_duration,"%02d:%02d",duration/60,duration%60);
        lv_obj_remove_flag(progress, LV_OBJ_FLAG_HIDDEN);
        lv_bar_set_value(progress, (int)((int64_t)pos * 100 / duration), LV_ANIM_OFF);
    } else {
        if (pos > 0)
            ui_text_fmt(music_time,"%02d:%02d",pos/60,pos%60);
        else
            ui_text(music_time,"--:--");
        ui_text(music_duration,"--:--");
        lv_obj_add_flag(progress, LV_OBJ_FLAG_HIDDEN);
    }
}
static bool saver_timed_out(uint32_t idle) {
    return prefs.saver_minutes>0 && idle>=(uint32_t)prefs.saver_minutes*60000;
}
static void saver_enter(bool offline) {
#if TAB5_LOCAL_ART
    gallery_reader_close();gallery_manual_until=0;gallery_return_hold=false;
    // Hidden main-page snapshots compete with the two art bitmaps and the
    // rotated reader's temporary layer. They can be rebuilt after exit; keep
    // that memory available before an art preview or idle saver starts.
    if(saver_is_gallery())tab5_draw_cache_drop();
#endif
    saver=true;offline_saver=offline;saver_preview=false;
    // Artwork owns its four-way object transforms, relative to normal landscape.
    if(saver_is_gallery())screen_orientation_apply(0);
    tab5_saver_active=1;
    saver_restore_brightness=device_get_config().brightness;
    set_backlight(saver_restore_brightness<12?saver_restore_brightness:12);
    ui_text(saver_time,"--:--");ui_text(classic_seconds,"--");
    ui_text(saver_date,"");
    saver_style_apply();if(saver_has_time)saver_update(&saver_local_time);
    lv_obj_remove_flag(saver_layer,LV_OBJ_FLAG_HIDDEN);
    lv_obj_move_foreground(saver_layer);
}
static void saver_leave(void) {
#if TAB5_LOCAL_ART
    gallery_motion_finish();
    gallery_reader_close();
#endif
    if(saver) {
        tab5_saver_active=0;
        lv_obj_add_flag(saver_layer,LV_OBJ_FLAG_HIDDEN);
        set_backlight(saver_restore_brightness);
        lv_display_trigger_activity(NULL);
        last_change=esp_timer_get_time()/1000;
        render();
    }
    saver=false;offline_saver=false;saver_preview=false;
    screen_orientation_apply(screen_orientation.direction);
}
static void saver_wake(lv_event_t *e) {(void)e;saver_leave();}
static void camera_cover_check(lv_event_t *e) {
    // LVGL's image cover check keeps the base object's NOT_COVER result when
    // its background is transparent, even for an opaque RGB565 canvas. Avoid
    // repainting the hidden page and a full black background behind each frame.
    lv_cover_check_info_t *info=lv_event_get_param(e);
    const lv_area_t *covered=info->area;
    lv_obj_t *canvas=lv_event_get_current_target(e);
    lv_draw_buf_t *buf=lv_canvas_get_draw_buf(canvas);
    if(info->res==LV_COVER_RES_MASKED||!covered||lv_obj_get_style_clip_corner(canvas,0)||!buf||!buf->data||buf->header.cf!=LV_COLOR_FORMAT_RGB565||
       lv_obj_get_style_image_opa(canvas,0)!=LV_OPA_COVER||lv_obj_get_style_opa(canvas,0)!=LV_OPA_COVER||
       lv_image_get_rotation(canvas)!=0||lv_image_get_scale_x(canvas)!=LV_SCALE_NONE||
       lv_image_get_scale_y(canvas)!=LV_SCALE_NONE||lv_obj_get_style_radius(canvas,0)!=0||
       lv_image_get_bitmap_map_src(canvas)!=NULL)return;
    lv_area_t area;lv_obj_get_coords(canvas,&area);
    if(buf->header.w!=lv_area_get_width(&area)||buf->header.h!=lv_area_get_height(&area))return;
    if(covered->x1>=area.x1&&covered->y1>=area.y1&&covered->x2<=area.x2&&covered->y2<=area.y2)
        info->res=LV_COVER_RES_COVER; // The public setter only permits less coverage.
}
static void camera_release_caches(void) {
    screen_preview_cache_reclaim(true);
    tab5_draw_cache_drop();
#if TAB5_LOCAL_ART
    // Hidden artwork remains owned by the UI after leaving the saver. Detach
    // its LVGL source on this thread before recycling any decoded pixels;
    // the camera worker drains the spare and pending result after workers stop.
    gallery_motion_finish();
    if(!gallery_pending_page.date[0]&&gallery_presented.pixels) {
        gallery_pending_page=gallery_presented;gallery_pending_page.pixels=NULL;
    }
    gallery_camera_reload=gallery_pending_page.date[0]!=0;
    visible(gallery_view,false);lv_image_set_src(gallery_view,NULL);
    gallery_presented=(gallery_image){0};
    for(int i=0;i<4;i++)gallery_drop(i);
#endif
}
static void camera_action(lv_event_t *e) {
    if(photo_camera_processing)return;
    int action=(int)(intptr_t)lv_event_get_user_data(e);
    if(action==0||action==-2) {
        voice_remote_state *voice=&ui_voice_work->camera;voice_remote_get(voice);
        if(voice->busy){ui_text(codex_voice_hint,"录音结束后即可拍照");return;}
        camera_release_caches();
        camera_preview_request_orientation(camera_current_pose());
        lv_obj_remove_flag(camera_layer,LV_OBJ_FLAG_HIDDEN);
        lv_obj_move_foreground(camera_layer);
        if(!camera_preview_start(camera_canvas))ui_text(camera_hint,"摄像头暂不可用，请稍后重试");
    } else if(action==4)photo_camera_use();
    else if(action==1) {
        if(!camera_preview_capture())return;
        lv_obj_add_state(camera_take,LV_STATE_DISABLED);
        ui_text(camera_hint,"正在拍摄…");
    }
    else if(action==2) {
        if(photo_for_codex)ui_text(codex_receipt,"正在打开摄像头…");
        camera_release_caches();
        camera_preview_request_orientation(camera_current_pose());
        if(!camera_preview_start(camera_canvas)) {
            ui_text(camera_hint,"摄像头尚未关闭，请稍后重试");
            if(photo_for_codex)ui_text(codex_receipt,"摄像头尚未关闭，请稍后重试");
        }
    } else {
        if(photo_for_codex)ui_text(codex_receipt,camera_preview_get_state()==CAMERA_PREVIEW_ERROR?
                                  camera_preview_error():"已取消拍照");
        camera_preview_stop();
        lv_obj_add_flag(camera_layer,LV_OBJ_FLAG_HIDDEN);
        if(photo_for_codex)photo_finish();
    }
}
static void camera_tick(void) {
    if(!camera_layer||lv_obj_has_flag(camera_layer,LV_OBJ_FLAG_HIDDEN))return;
    if(photo_camera_processing)return;
    lv_display_trigger_activity(NULL);
    camera_preview_state current=camera_preview_get_state();
    if(photo_for_codex&&current==CAMERA_PREVIEW_ERROR)
        ui_text(codex_receipt,camera_preview_error());
    if(current==CAMERA_PREVIEW_CAPTURED)camera_canvas_review(camera_canvas);
    // Review freezes the normalized photograph; controls float above it.
    visible(camera_canvas,current==CAMERA_PREVIEW_LIVE||current==CAMERA_PREVIEW_CAPTURING||current==CAMERA_PREVIEW_CAPTURED);
    ui_text(camera_hint,current==CAMERA_PREVIEW_STARTING?"正在打开摄像头…":
        current==CAMERA_PREVIEW_LIVE?"":
        current==CAMERA_PREVIEW_CAPTURING?"正在拍摄…":
        current==CAMERA_PREVIEW_CAPTURED?"已拍摄":
        current==CAMERA_PREVIEW_ERROR?camera_preview_error():"摄像头已关闭");
    visible(camera_hint,current!=CAMERA_PREVIEW_LIVE&&current!=CAMERA_PREVIEW_CAPTURED);
    if(current==CAMERA_PREVIEW_LIVE)lv_obj_remove_state(camera_take,LV_STATE_DISABLED);
    else lv_obj_add_state(camera_take,LV_STATE_DISABLED);
    visible(camera_take,current==CAMERA_PREVIEW_LIVE||current==CAMERA_PREVIEW_STARTING||current==CAMERA_PREVIEW_CAPTURING);
    if(current==CAMERA_PREVIEW_CAPTURED||current==CAMERA_PREVIEW_ERROR)lv_obj_remove_state(camera_retry,LV_STATE_DISABLED);
    else lv_obj_add_state(camera_retry,LV_STATE_DISABLED);
    visible(camera_retry,current==CAMERA_PREVIEW_CAPTURED||current==CAMERA_PREVIEW_ERROR);
    visible(camera_use,photo_for_codex&&current==CAMERA_PREVIEW_CAPTURED);
}
#include "ui_photos.h"
#include "ui_quick_console.h"
// Same status travels over all three links. Only a newer input timestamp from
// a newer sample is activity; heartbeats and delayed link fallback are not.
static void pc_input_update(cJSON *data) {
    static char session[40];
    static int64_t sequence, last_input;
    cJSON *input=item(data,"pcInput"), *stamp=item(input,"lastInputTickMs"), *seq=item(input,"sequence");
    const char *source=text(input,"session");
    if(!cJSON_IsNumber(stamp)||stamp->valuedouble<0||!cJSON_IsNumber(seq)||seq->valuedouble<=0||
       !strcmp(source,"--")||!*source||strlen(source)>=sizeof(session)) return;
    int64_t next_seq=(int64_t)seq->valuedouble,next_input=(int64_t)stamp->valuedouble;
    if(strcmp(source,session)) {
        snprintf(session,sizeof(session),"%s",source);
        sequence=next_seq;last_input=next_input;return; // establish baseline after restart
    }
    if(next_seq<=sequence) return;
    sequence=next_seq;
    if(next_input<=last_input) return;
    last_input=next_input;
    lv_display_trigger_activity(NULL);
    if(saver&&!saver_preview) {tab5_pc_wakes++;saver_leave();}
}

static void clock_tick(lv_timer_t *timer) {
    int64_t now=esp_timer_get_time()/1000;
    // Sample during camera use, but let its worker release scanout before
    // applying display/touch coordinates. Never flip mid-gesture.
    bool rotation_busy=photo_camera_processing||gesture_active()||ota_update_busy()||flash_benchmark_busy();
    int direction=ui_orientation_sample(&screen_orientation,art_orientation_get(),now,rotation_busy,screen_orientation_mode);
    bool camera_visible=camera_layer&&!lv_obj_has_flag(camera_layer,LV_OBJ_FLAG_HIDDEN);
    if(!rotation_busy&&camera_visible) {
        int pose=camera_current_pose();
        camera_preview_request_orientation(pose);
        camera_preview_state state=camera_preview_get_state();
        if(state==CAMERA_PREVIEW_CAPTURED||(!camera_preview_busy()&&state==CAMERA_PREVIEW_OFF))
            camera_orientation_apply(pose);
    } else if(!rotation_busy&&photo_flow&&photo_window&&!lv_obj_has_flag(photo_window,LV_OBJ_FLAG_HIDDEN)&&photo_step!=4)photo_view_orientation();
    else if(!rotation_busy&&(!camera_preview_busy()||camera_preview_get_state()==CAMERA_PREVIEW_CAPTURED))screen_orientation_apply(saver&&saver_is_gallery()?0:direction);
    atomic_store(&ui_heartbeat_ms,(unsigned)now);
    (void)timer;
    // Device indicators must work before the first bridge snapshot arrives.
    static int64_t power_second=-1;
    if(!saver&&power_second!=now/1000) {
        power_second=now/1000;battery_render(battery_status_get());sound_header_render();
    }
    clock_music(now,offline_since<0);
    timer_tick(now);
}
static void codex_notice_style(void) {
    unsigned unread=0;bool selected=false;
    if(codex_notices)for(int i=0;i<80;i++)if(codex_notices[i].unread) {
        unread++;if(!strcmp(codex_notices[i].task,codex_selected))selected=true;
    }
    if(codex_unread_dot) {
        int width=unread>9?32:24,x=259-width;
        if(lv_obj_get_width(codex_unread_dot)!=width)lv_obj_set_width(codex_unread_dot,width);
        if(lv_obj_get_x(codex_unread_dot)!=x)lv_obj_set_x(codex_unread_dot,x);
        if(unread>9)ui_text(codex_unread_count,"9+");
        else ui_text_fmt(codex_unread_count,"%u",unread);
        lv_obj_center(codex_unread_count);visible(codex_unread_dot,unread>0);
        const char *badge=lv_label_get_text(codex_unread_count);
        unsigned shown_count=!strcmp(badge,"9+")?10:(unsigned)atoi(badge);
        // One aligned store keeps USB diagnostics consistent with actual LVGL state.
        tab5_codex_notice_state=unread|(shown_count<<8)|(!lv_obj_has_flag(codex_unread_dot,LV_OBJ_FLAG_HIDDEN)?65536u:0);
    }
    if(codex_reply_button) {
        ui_border_color(codex_reply_button,lv_color_hex(selected?GREEN:TRACK),0);
        ui_bg_color(codex_reply_button,lv_color_hex(GREEN),0);
        ui_bg_opa(codex_reply_button,selected?LV_OPA_10:LV_OPA_TRANSP,0);
        tint(lv_obj_get_child(codex_reply_button,0),selected?GREEN:INK);
        ui_text(lv_obj_get_child(codex_reply_button,0),selected?"查看新回复":"查看回复");
    }
}
static void codex_notice_read(void) {
    if(codex_notices)for(int i=0;i<80;i++)if(!strcmp(codex_notices[i].task,codex_selected))codex_notices[i].unread=false;
    codex_notice_style();
    render_home_task();
}
static void codex_notice_open(lv_event_t *e) {
    if(health_banner_active&&esp_timer_get_time()/1000>=alert_until){health_show(e);return;}
    if(esp_timer_get_time()/1000<alert_until&&!strcmp(lv_label_get_text(banner),"生日提醒 · 点击查看日历")) {page(12);detail_return=0;return;}
    if(esp_timer_get_time()/1000>=alert_until||!codex_alert_task[0]||codex_voice_waiting||codex_waiting)return;
    cJSON *tasks=item(item(shown,"codexTasks"),"tasks"),*task=NULL;
    cJSON_ArrayForEach(task,tasks)if(!strcmp(text(task,"id"),codex_alert_task)) {
        if(!codex_save_draft())return;
        snprintf(codex_selected,sizeof(codex_selected),"%s",codex_alert_task);
        snprintf(codex_project_selected,sizeof(codex_project_selected),"%s",text(task,"projectId"));
        codex_load_draft();codex_reply_turn[0]=0;page(8);render_codex();codex_open_latest=true;codex_reply_open(e);return;
    }
}
static void codex_notice_tick(int64_t now,bool changed) {
    if(!changed)return;
    cJSON *remote=item(shown,"codexTasks"),*ready=item(remote,"repliesReady"),*read=item(remote,"repliesRead"),*unread=item(remote,"repliesUnread"),*entry=NULL;
    if(!cJSON_IsObject(ready))return;
    if(!codex_notices) {
#ifdef TAB5_UI_PREVIEW
        codex_notices=calloc(80,sizeof(*codex_notices));
#else
        codex_notices=heap_caps_calloc(80,sizeof(*codex_notices),MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
#endif
        if(!codex_notices)return;
    }
    bool announce=false;
    cJSON_ArrayForEach(entry,ready) {
        if(!cJSON_IsString(entry)||!entry->string||strlen(entry->string)!=36||strlen(entry->valuestring)>=64)continue;
        int slot=-1,empty=-1;
        for(int i=0;i<80;i++) {
            if(!strcmp(codex_notices[i].task,entry->string)){slot=i;break;}
            if(!codex_notices[i].task[0]&&empty<0)empty=i;
        }
        if(slot<0)slot=empty;
        // Completed PC tasks now share this queue. Reuse read/out-of-catalog
        // entries so a long-running device cannot exhaust all 80 slots.
        if(slot<0)for(int i=0;i<80;i++)if(!codex_notices[i].unread) {
            if(slot<0||codex_notices[i].arrived<codex_notices[slot].arrived)slot=i;
        }
        if(slot<0)continue;
        codex_notice *notice=&codex_notices[slot];
        cJSON *pending=item(unread,entry->string);
        bool desktop_unread=cJSON_IsString(pending)&&!strcmp(pending->valuestring,entry->valuestring);
        if(!strcmp(notice->turn,entry->valuestring)) {
            // Restore desktop unread after a delayed initial snapshot, but don't
            // resurrect a device-local read on every subsequent publication.
            if(desktop_unread&&!notice->desktop_unread_seen)notice->unread=true;
            if(desktop_unread)notice->desktop_unread_seen=true;
            continue;
        }
        snprintf(notice->task,sizeof(notice->task),"%s",entry->string);
        snprintf(notice->turn,sizeof(notice->turn),"%s",entry->valuestring);
        // Desktop acknowledgement applies only to this exact completed turn.
        cJSON *ack=item(read,entry->string);
        bool desktop_read=cJSON_IsString(ack)&&!strcmp(ack->valuestring,entry->valuestring);
        notice->desktop_unread_seen=desktop_unread;
        notice->unread=(codex_notices_ready||desktop_unread)&&!desktop_read;
        notice->arrived=++codex_notice_sequence;
        if(notice->unread&&codex_notices_ready) {
            announce=true;snprintf(codex_alert_task,sizeof(codex_alert_task),"%s",entry->string);
            // Arrival is unread until the matching turn has actually rendered.
        }
    }
    // Apply even when repliesReady is unchanged, and across reduced catalogs.
    // An absent/unknown map never clears local unread; an old turn cannot clear
    // a newer arrival. Reusing the same style path updates the top-right count.
    for(int i=0;i<80;i++)if(codex_notices[i].task[0]) {
        cJSON *ack=item(read,codex_notices[i].task);
        if(cJSON_IsString(ack)&&!strcmp(ack->valuestring,codex_notices[i].turn)) {
            codex_notices[i].unread=false;codex_notices[i].desktop_unread_seen=false;
        }
    }
    codex_notices_ready=true;
    if(announce&&prefs.alerts) {
        voice_remote_notify_reply();alert_until=now+10000;alert_started=now;alert_attention=false;alert_phase=-1;
        ui_text(banner,"Codex 新回复 · 查看");
    }
    codex_notice_style();
    render_home_task();
}
static void alerts(int64_t now, bool changed) {
    if (!changed)
        return;
    for (int i = 0; i < 2; i++) {
        cJSON *s = item(shown, i ? "codex" : "claude");
        uint64_t seq = (uint64_t)num(s, "completionSequence");
        bool input = yes(s, "needsInput");
        bool event = (input && !waiting[i]) || (seq != completion[i] && yes(s, "completionActive"));
        if (event && prefs.alerts) {
            codex_alert_task[0]=0;
            alert_until = now + 10000;
            alert_started=now;alert_attention=input;alert_phase=-1;
            ui_text_fmt(banner, "%s：%s", i ? "Codex" : "Claude", input ? "等待你的操作" : "任务已完成");
        }
        completion[i] = seq;
        waiting[i] = input;
    }
}
// Pulse only a small status dot three times; keep the message continuously readable.
static void alert_visual_tick(int64_t now) {
    bool active=now<alert_until;
    int phase=active?((now-alert_started<3000)?(int)((now-alert_started)/500)%2:0):-1;
    if(phase!=alert_phase) {
        alert_phase=phase;
        uint32_t color=alert_attention?AMBER:GREEN;
        tint(banner,active?color:MUTED);
        ui_bg_color(banner,lv_color_hex(color),0);
        ui_bg_opa(banner,active?LV_OPA_10:LV_OPA_TRANSP,0);
        visible(alert_dot,active);
        ui_bg_color(alert_dot,lv_color_hex(color),0);
        ui_bg_opa(alert_dot,phase==1?LV_OPA_30:LV_OPA_COVER,0);
    }
    if(!active)ui_text(banner,current_page==7&&clock_style_error?"表盘保存失败，请点击重试":"");
}
static void network_refresh(void) {
    char names[TRUSTED_HOST_LIMIT][TRUSTED_HOST_NAME],host_options[TRUSTED_HOST_LIMIT*(TRUSTED_HOST_NAME+1)]={0};int host_selected=-1;
    int host_count=device_host_names(names,&host_selected);
    for(int i=0;i<host_count;i++){if(i)strcat(host_options,"\n");strcat(host_options,names[i]);}
    lv_dropdown_set_options(host_picker,host_count?host_options:"请先通过 USB 配对电脑");
    if(host_selected>=0)lv_dropdown_set_selected(host_picker,host_selected);
    if(host_count){lv_obj_remove_state(host_switch,LV_STATE_DISABLED);lv_obj_remove_state(host_rename,LV_STATE_DISABLED);}
    else {lv_obj_add_state(host_switch,LV_STATE_DISABLED);lv_obj_add_state(host_rename,LV_STATE_DISABLED);}
    char options[WIFI_PROFILE_LIMIT*(WIFI_LABEL_SIZE+37)]={0};network_option_count=device_wifi_names(network_options);
    device_config cfg=device_get_config();int selected=0;
    for(int i=0;i<network_option_count;i++) {
        if(!strcmp(network_options[i],cfg.ssid))selected=i;
        char name[WIFI_LABEL_SIZE+37],label[WIFI_LABEL_SIZE];device_wifi_label(network_options[i],label);
        snprintf(name,sizeof(name),"%.48s%s%.32s",label,label[0]?" · ":"",network_options[i]);
        for(char *p=name;*p;p++)if(*p=='\n'||*p=='\r')*p=' ';
        if(i)strcat(options,"\n");
        strcat(options,name);
    }
    lv_dropdown_set_options(network_picker,network_option_count?options:"尚无已保存网络");
    lv_dropdown_set_selected(network_picker,selected);
    lv_obj_t *controls[]={network_picker,network_connect,network_forget};
    for(unsigned i=0;i<3;i++) {
        if(network_option_count)lv_obj_remove_state(controls[i],LV_STATE_DISABLED);
        else lv_obj_add_state(controls[i],LV_STATE_DISABLED);
    }
}
static void network_message(const char *text) {
    ui_text(network_feedback,text);ui_text(network_diag_feedback,text);
}
static void network_host_render(void) {
    char id[33],name[TRUSTED_HOST_NAME];link_kind link;
    device_host_status(id,name,&link);int64_t now=esp_timer_get_time()/1000;
    tab5_wifi_status diagnostic;tab5_wifi_status_read(&diagnostic);
    ui_text(network_details,diagnostic.bridge);tint(network_details,diagnostic.authenticated?GREEN:AMBER);
    ui_text(network_diag_state,diagnostic.state);tint(network_diag_state,diagnostic.connected?GREEN:AMBER);
    ui_text(network_diag_name,diagnostic.network);ui_text(network_diag_ip,diagnostic.address);
    ui_text(network_diag_host,name[0]?name:"尚未绑定电脑");
    device_config cfg=device_get_config();char ssid[33];snprintf(ssid,sizeof(ssid),"%s",cfg.ssid);
    for(char *p=ssid;*p;p++)if(*p=='\n'||*p=='\r')*p=' ';
    ui_text(network_wifi_state,wifi_connected?"Wi-Fi 已连接":ssid[0]?"正在连接 Wi-Fi":"尚未配置 Wi-Fi");
    ui_text(network_wifi_name,ssid[0]?ssid:"请在电脑桥接添加网络");
    tint(network_wifi_state,wifi_connected?GREEN:MUTED);
    ui_bg_color(network_wifi_card,lv_color_hex(wifi_connected?0x294137:0x27343A),0);
    if(strcmp(id,host_display_id)) {
        strcpy(host_display_id,id);host_notice[0]=0;host_connect_started=now;
        network_refresh();
    }
    bool connected=link!=LINK_NONE;
    bool waiting=id[0]&&!connected&&now>=host_connect_started&&now-host_connect_started<12000;
    const char *symbol=host_notice[0]?LV_SYMBOL_WARNING:connected?LV_SYMBOL_OK:waiting?LV_SYMBOL_REFRESH:LV_SYMBOL_CLOSE;
    uint32_t color=host_notice[0]?AMBER:connected?GREEN:waiting?AMBER:MUTED;
    ui_text(host_current,name[0]?name:"尚未绑定电脑");
    if(host_notice[0])ui_text(host_state,host_notice);
    else if(connected)ui_text_fmt(host_state,"已连接 · %s%s",link==LINK_USB?"USB":link==LINK_WIFI?"Wi-Fi":"蓝牙",device_host_usb_owned()?" 接管":"");
    else ui_text(host_state,!id[0]?"请先通过 USB 配对":waiting?"正在连接…":"未连接 · 请确认电脑桥接已运行");
    ui_text(host_symbol,symbol);lv_obj_center(host_symbol);
    tint(host_state,color);tint(host_symbol,UI_BACKGROUND);
    ui_border_color(host_monitor,lv_color_hex(color),0);ui_bg_color(host_badge,lv_color_hex(color),0);
    ui_bg_color(host_card,lv_color_hex(connected&&!host_notice[0]?0x294137:waiting||host_notice[0]?0x403B30:0x303B40),0);
}
static void host_editor_close(void) {
    if(!host_editor)return;
    tab5_ime_reset_for(host_editor_ime);tab5_ime_show_for(host_editor_ime,false);
    visible(host_editor,false);
}
static void host_editor_action(lv_event_t *e) {
    if((int)(intptr_t)lv_event_get_user_data(e)==0){host_editor_close();return;}
    tab5_ime_finish_for(host_editor_ime);
    const char *name=lv_textarea_get_text(host_editor_input);
    if(tab5_ime_pending_for(host_editor_ime)[0]||strlen(name)>=TRUSTED_HOST_NAME) {
        ui_text(host_editor_hint,"名称过长，请缩短后保存");tint(host_editor_hint,AMBER);return;
    }
    if(!device_host_rename(host_editor_id,name)) {
        ui_text(host_editor_hint,"未保存，请检查名称后重试");tint(host_editor_hint,AMBER);return;
    }
    int index=host_editor_index;host_editor_close();network_refresh();lv_dropdown_set_selected(host_picker,index);
    network_host_render();network_message("电脑备注已保存");
}
static void host_editor_ready(lv_event_t *e) {
    if(lv_event_get_code(e)==LV_EVENT_CANCEL){host_editor_close();return;}
    host_editor_action(e);
}
static void host_editor_open(void) {
    char name[TRUSTED_HOST_NAME];host_editor_index=lv_dropdown_get_selected(host_picker);
    if(!device_host_label(host_editor_index,host_editor_id,name))return;
    tab5_ime_reset_for(host_editor_ime);lv_textarea_set_text(host_editor_input,name);
    ui_text(host_editor_hint,"留空保存可恢复电脑原名");tint(host_editor_hint,MUTED);
    visible(host_editor,true);lv_obj_move_foreground(host_editor);
    lv_keyboard_set_textarea(host_editor_keyboard,host_editor_input);tab5_ime_show_for(host_editor_ime,true);
}
bool tab5_ui_wifi_forget(const char *ssid) {
#ifndef TAB5_UI_PREVIEW
    if(!bsp_display_lock(100))return false;
#endif
    bool ok=false;
    if(!codex_voice_waiting&&!codex_waiting&&!codex_send_queued&&!photo_flow&&!camera_preview_busy()&&!ota_update_busy()) {
        ok=device_wifi_forget(ssid);
        if(ok&&network_picker){network_refresh();visible(network_confirm,false);network_forget_ssid[0]=0;}
    }
#ifndef TAB5_UI_PREVIEW
    bsp_display_unlock();
#endif
    return ok;
}
static void network_action(lv_event_t *e) {
    int action=(int)(intptr_t)lv_event_get_user_data(e);
    if(action==8){host_editor_open();return;}
    if(action==7) {
        if(codex_voice_waiting||codex_waiting||codex_send_queued||photo_flow||camera_preview_busy()||ota_update_busy()){snprintf(host_notice,sizeof(host_notice),"请先结束正在进行的操作");network_host_render();return;}
        bool ok=device_host_select(lv_dropdown_get_selected(host_picker));
        if(ok){host_notice[0]=0;host_connect_started=esp_timer_get_time()/1000;network_refresh();}
        else snprintf(host_notice,sizeof(host_notice),"%s",device_host_usb_owned()?"请先断开当前电脑的 USB 再切换":"切换未保存，请重试");
        network_host_render();
        return;
    }
    if(action==9){ui_text(network_diag_feedback,"");visible(network_confirm,false);network_forget_ssid[0]=0;visible(network_diag_window,true);lv_obj_move_foreground(network_diag_window);network_host_render();return;}
    if(action==10){visible(network_diag_window,false);return;}
    if(action==2){visible(network_confirm,false);visible(network_diag_window,false);visible(network_window,false);return;}
    if(action==5){visible(network_confirm,false);network_forget_ssid[0]=0;return;}
    if(action==0) {
        network_refresh();visible(network_confirm,false);visible(network_diag_window,false);network_forget_ssid[0]=0;
        visible(network_window,true);lv_obj_move_foreground(network_window);
        network_host_render();
        network_message("新电脑需通过 USB 配对；Wi-Fi 可在电脑桥接添加。");return;
    }
    if(codex_voice_waiting||codex_waiting||codex_send_queued||photo_flow||ota_update_busy()) {
        visible(network_confirm,false);
        network_message("请先结束录音、拍照、发送或升级，再更改网络。");return;
    }
    if(action==4) {
        unsigned selected=lv_dropdown_get_selected(network_picker);if(selected>=(unsigned)network_option_count)return;
        strcpy(network_forget_ssid,network_options[selected]);
        char name[WIFI_LABEL_SIZE+37],label[WIFI_LABEL_SIZE];device_wifi_label(network_forget_ssid,label);
        snprintf(name,sizeof(name),"%.48s%s%.32s",label,label[0]?" · ":"",network_forget_ssid);
        for(char *p=name;*p;p++)if(*p=='\n'||*p=='\r')*p=' ';
        device_config cfg=device_get_config();
        ui_text_fmt(network_confirm_text,"删除“%s”？\n\n%s",name,
            device_wifi_count()==1?"这是最后一个网络，删除后 Wi-Fi 将断开。":!strcmp(cfg.ssid,network_forget_ssid)?"正在使用此网络，删除后将尝试其他已保存网络。":"删除后不再自动连接此网络。");
        visible(network_confirm,true);lv_obj_move_foreground(network_confirm);return;
    }
    if(action==6) {
        if(!network_forget_ssid[0])return;
        bool ok=device_wifi_forget(network_forget_ssid);network_forget_ssid[0]=0;
        visible(network_confirm,false);network_refresh();
        network_message(!ok?"删除未完成，请重试。":device_wifi_count()?"网络已删除。":"网络已删除；可通过电脑 USB 添加 Wi-Fi。");return;
    }
    if(esp_timer_get_time()/1000<network_retry_until)return;
    if(action==3&&!device_wifi_select(lv_dropdown_get_selected(network_picker),true)) {
        network_message("网络选择未保存，请重试。");return;
    }
    tab5_wifi_reconnect();network_retry_until=esp_timer_get_time()/1000+3000;
    lv_obj_add_state(network_retry,LV_STATE_DISABLED);
    network_message("正在连接；状态会自动更新。");
}
static void ota_completion_drawn(lv_event_t *e) {
    (void)e;
    if(ota_update_busy()&&!ota_update_completed()&&ota_layer&&!lv_obj_has_flag(ota_layer,LV_OBJ_FLAG_HIDDEN))
        ota_update_preparation_presented();
    if(ota_completion_render_pending) {
        ota_completion_render_pending=false;ota_completion_rendered=true;
        ota_update_completion_presented();
    }
}
static const char *ota_prepare(void) {
    if(ota_update_busy()||flash_benchmark_busy()){return "device_busy";}
    if(codex_voice_waiting||codex_waiting||codex_reading||codex_send_queued||codex_syncing||codex_load_waiting||photo_flow||camera_preview_busy()) {
        ui_text(ota_hint,"请先结束录音、拍照或网络操作，再升级。");return "device_busy";
    }
    if(codex_drafts)for(int i=0;i<80;i++) {
        if(codex_drafts[i].request[0]){ui_text(ota_hint,"尚有消息未确认送达，请先查询送达结果。");return "draft_pending";}
        if(codex_drafts[i].dirty&&strcmp(codex_drafts[i].id,codex_selected)) {
            ui_text(ota_hint,"其他会话的草稿正在同步，请稍后升级。");return "draft_pending";
        }
    }
    if(!codex_save_draft()){ui_text(ota_hint,"草稿保存失败，升级未开始。");return "draft_save_failed";}
    if(rpc_ota_link_select()==LINK_NONE){ui_text(ota_hint,"固件升级请连接 Wi-Fi、USB 或蓝牙。");return "no_link";}
    return NULL;
}
static const char *ota_begin(const char *offer,const char *sha) {
    const char *error=ota_prepare();if(error)return error;
    tab5_draw_cache_drop();
    screen_preview_cache_reclaim(true);
    if(ota_update_request_checked(offer,sha)) {
        ota_completion_render_pending=ota_completion_rendered=false;
        ui_text(ota_title,"正在更新 TAB5");tint(ota_title,INK);
        lv_obj_set_y(ota_title,180);lv_obj_set_y(ota_progress,350);
        lv_obj_set_y(ota_version_track,254);
        char version[32];ota_update_target_version(version,sizeof(version));
        ui_text_fmt(ota_version,"版本 %s",version);
        ui_bg_color(ota_version_track,lv_color_hex(0x426672),LV_PART_INDICATOR);
        lv_bar_set_value(ota_version_track,0,LV_ANIM_OFF);
        visible(ota_success_icon,false);visible(ota_version_track,true);
        ui_text(ota_note,"请保持供电；完成后会自动重启。");
        visible(ota_note,true);
        visible(ota_layer,true);lv_obj_move_foreground(ota_layer);
        ui_text(ota_progress,"正在准备固件更新…");
        return NULL;
    }else ui_text(ota_hint,"暂时无法开始，请重新提供固件。");
    return "start_failed";
}
static void ota_action(lv_event_t *e){(void)e;(void)ota_begin(NULL,NULL);}
#include "ota_remote.inc"
#include "boot_verify.inc"
#include "runtime_diagnostics.inc"
#include "screen_preview.inc"
static void flash_action(lv_event_t *e) {
    int action=(int)(intptr_t)lv_event_get_user_data(e);
    if(action==0){flash_feedback_pinned=false;visible(flash_layer,true);lv_obj_move_foreground(flash_layer);return;}
    if(action==2){if(!flash_benchmark_busy())visible(flash_layer,false);return;}
    if(action==3){flash_benchmark_cancel();return;}
    if(action!=1||flash_benchmark_busy())return;
    flash_feedback_pinned=true;
    if(codex_voice_waiting||codex_waiting||codex_reading||codex_send_queued||codex_syncing||codex_load_waiting||photo_flow||camera_preview_busy()) {
        ui_text(flash_details,"请先结束录音、拍照或网络操作，再测试。");return;
    }
    if(!codex_save_draft()){ui_text(flash_details,"草稿保存失败，测试未开始。");return;}
    if(!flash_benchmark_start()){ui_text(flash_details,"设备忙或内存不足，测试未开始。");return;}
    flash_feedback_pinned=false;
    lv_obj_add_state(flash_start,LV_STATE_DISABLED);lv_obj_add_state(flash_close,LV_STATE_DISABLED);
}
// Grey: disconnected; amber: physical link awaiting data; green: fresh data.
// Pale blue: authenticated BLE standby reported by a fresh USB snapshot.
// This display-only state never renews BLE data or changes transport selection.
static bool link_standby_report(cJSON *frame,link_kind source,int64_t received,int64_t now) {
    return source==LINK_USB&&received>=0&&now>=received&&now-received<8000&&
        cJSON_IsTrue(item(item(frame,"connectionHealth"),"bleStandby"));
}
static void links_render(const bool connected[3],const bool ready[3],bool ble_standby) {
    for(int i=0;i<3;i++) {
        int state=connected[i]?(ready[i]?2:i==LINK_BLE&&ble_standby&&connected[LINK_USB]&&ready[LINK_USB]?3:1):0;
        if(link_visual_state[i]==state)continue;
        link_visual_state[i]=state;
        tint(link_symbols[i],state==2?GREEN:state==3?0xA9C9D0:state==1?0xE6B65C:0x68787F);
        ui_bg_color(link_buttons[i],lv_color_hex(UI_BACKGROUND),0);
    }
}
static void setup_status(int index,const char *value,bool positive,bool waiting) {
    ui_text(setup_values[index],value);
    tint(setup_values[index],positive?GREEN:waiting?0xE6B65C:MUTED);
    visible(setup_checks[index],positive);
}
static void setup_connections(bool paired,const bool connected[3],const bool ready[3],bool bluetooth_available) {
    setup_status(0,paired?"已配对":"请在电脑连接",paired,false);
    bool usb_ready=connected[LINK_USB]&&ready[LINK_USB];
    setup_status(1,usb_ready?"已就绪":connected[LINK_USB]?"等待电脑":"未连接",usb_ready,connected[LINK_USB]&&!usb_ready);
    setup_status(2,connected[LINK_WIFI]?"已连接":"未连接",connected[LINK_WIFI],false);
    setup_status(3,connected[LINK_BLE]?(link_visual_state[LINK_BLE]==3?"已连接 · 待命":"已连接"):bluetooth_available?"已就绪":"未就绪",connected[LINK_BLE]||bluetooth_available,false);
}
static void sound_refresh(void);
#include "display_settings.inc"
static void tick_work(lv_timer_t *timer) {
    ota_remote_tick(shown,last_link,esp_timer_get_time()/1000,last_received);
    boot_verify_tick(shown,last_link,esp_timer_get_time()/1000,last_received);
    runtime_diagnostics_tick(shown,last_link,esp_timer_get_time()/1000,last_received);
    screen_preview_tick(shown,last_link,esp_timer_get_time()/1000,last_received);
    tf_storage_refresh();
    static int64_t reading_saved_at;
    int64_t reading_now=esp_timer_get_time();
    if(reading_now-reading_saved_at>=15000000&&!gesture_active()&&!ota_update_busy()&&!flash_benchmark_busy()) {
        reading_saved_at=reading_now;codex_bookmark_save();
        if(codex_bookmarks_dirty&&ui_reading_save(codex_bookmarks))codex_bookmarks_dirty=false;
    }
    (void)timer;
    int64_t now = esp_timer_get_time() / 1000;
    settings_feedback_tick(now);
    // Coalesce page selections and keep flash commits out of drag/snap frames.
    if(preferences_due&&now>=preferences_due&&!gesture_active()&&!ota_update_busy()&&!flash_benchmark_busy())save();
    if(flash_layer&&!lv_obj_has_flag(flash_layer,LV_OBJ_FLAG_HIDDEN)) {
        char status[900];if(!flash_feedback_pinned){flash_benchmark_status(status,sizeof(status));ui_text(flash_details,status);}
        bool testing=flash_benchmark_busy();
        if(testing){lv_obj_add_state(flash_start,LV_STATE_DISABLED);lv_obj_add_state(flash_close,LV_STATE_DISABLED);}
        else {lv_obj_remove_state(flash_start,LV_STATE_DISABLED);lv_obj_remove_state(flash_close,LV_STATE_DISABLED);}
        visible(flash_cancel,testing);
        if(testing)return;
    }
    if(ota_update_busy()) {
        char status[160];ota_update_status(status,sizeof(status));
        int percent=ota_update_progress();
        bool verifying_now=ota_update_verifying();
        if(ota_update_completed()||verifying_now)ui_text(ota_progress,status);
        else ui_text_fmt(ota_progress,"%s · 写入 %d%%",status,percent);
        if(verifying_now) {
            ui_text(ota_title,"正在校验固件");
            ui_text(ota_note,"下载与写入完成，校验通过后自动重启");
        }
        lv_bar_set_value(ota_version_track,percent,LV_ANIM_OFF);
        if(ota_update_completed()&&!ota_completion_rendered) {
            ui_text(ota_title,"升级完成");tint(ota_title,GREEN);
            lv_obj_set_y(ota_title,284);lv_obj_set_y(ota_progress,432);
            lv_obj_set_y(ota_version_track,350);
            ui_bg_color(ota_version_track,lv_color_hex(0x35604C),LV_PART_INDICATOR);
            char version[32];ota_update_target_version(version,sizeof(version));
            ui_text_fmt(ota_version,"版本 %s",version);
            visible(ota_success_icon,true);visible(ota_version_track,true);
            visible(ota_note,false);ota_completion_render_pending=true;
        }
        visible(ota_layer,true);lv_obj_move_foreground(ota_layer);return;
    } else if(ota_layer&&!lv_obj_has_flag(ota_layer,LV_OBJ_FLAG_HIDDEN)) {
        visible(ota_layer,false);
        char status[160];ota_update_status(status,sizeof(status));ui_text(ota_hint,status);
    }
    bool changed = false;
    if(!xSemaphoreTake(device_mutex, 0)) return;
    link_kind active = link_state_active(&device_links, now);
    if(device_info_link)ui_text(device_info_link,active==LINK_USB?"USB":active==LINK_WIFI?"Wi-Fi":active==LINK_BLE?"蓝牙":"未连接电脑");
    bool ready_links[3];
    for(int i=0;i<3;i++)ready_links[i]=device_links.received_ms[i]>=0&&
        now>=device_links.received_ms[i]&&now-device_links.received_ms[i]<8000;
    bool interacting=gesture_active();
    if (!interacting&&active != LINK_NONE && (active != last_link || device_links.received_ms[active] != last_received)) {
        cJSON *next = cJSON_Duplicate(device_snapshots[active], true);
        if (next) {
            cJSON_Delete(shown);
            shown = next;
            ui_startup_home_snapshot(&startup_home,current_page,now,&last_change);
            last_received = device_links.received_ms[active];
            last_link = active;
            changed = true;
        }
    }
    if(!interacting&&active!=LINK_NONE&&device_metrics[active]&&
       (changed||metrics_link!=active||metrics_seen!=device_metrics_sequence[active])) {
        cJSON *incoming=device_metrics[active],*previous=item(shown,"systemMetrics");
        if(strcmp(text(incoming,"sampleSession"),text(previous,"sampleSession"))||num(incoming,"sampleSequence")>num(previous,"sampleSequence")) {
            cJSON *copy=cJSON_Duplicate(incoming,true);
            if(copy&&shown){cJSON_DeleteItemFromObjectCaseSensitive(shown,"systemMetrics");cJSON_AddItemToObject(shown,"systemMetrics",copy);metrics_pending=true;}else cJSON_Delete(copy);
        }
        metrics_link=active;metrics_seen=device_metrics_sequence[active];
    }
    xSemaphoreGive(device_mutex);
    int64_t frame_age=last_received?now-last_received:-1;
    if(!interacting&&display_settings_tick(shown,&prefs,active,now,last_received,current_page==9||preferences_due!=0||clock_picker_is_open())) {
        int level=device_get_config().brightness;if(saver)saver_restore_brightness=level;else set_backlight(level);
        if(remote_brightness)lv_slider_set_value(remote_brightness,level,LV_ANIM_OFF);
        if(brightness_value)ui_text_fmt(brightness_value,"%d%%",level);
        display_controls_refresh();
        if(remote_interval)lv_dropdown_set_selected(remote_interval,prefs.interval==10?0:prefs.interval==15?1:prefs.interval==30?2:3);
        if(remote_saver)lv_dropdown_set_selected(remote_saver,prefs.saver_minutes==0?0:prefs.saver_minutes==1?1:prefs.saver_minutes==5?2:prefs.saver_minutes==10?3:prefs.saver_minutes==30?4:5);
        if(remote_alerts){if(prefs.alerts)lv_obj_add_state(remote_alerts,LV_STATE_CHECKED);else lv_obj_remove_state(remote_alerts,LV_STATE_CHECKED);}
        for(int i=0;i<8;i++){ui_text_fmt(order_rows[i],"%d. %s",i+1,names[prefs.order[i]]);if(prefs.enabled[prefs.order[i]])lv_obj_add_state(cycle_checks[i],LV_STATE_CHECKED);else lv_obj_remove_state(cycle_checks[i],LV_STATE_CHECKED);}
        sound_refresh();last_change=now;if(prefs.selected>=0)page(prefs.selected);
    }
    tab5_ui_data_age_ms=frame_age<0?-1:frame_age>3600000?3600000:(int)frame_age;
    tab5_ui_task_selected=codex_selected[0]?1:0;
    const char *input_session=text(item(shown,"pcInput"),"session");
    tab5_ui_session_ready=strcmp(input_session,"--")&&strlen(input_session)<40;
    if (active == LINK_NONE) {
        if (offline_since < 0)
            offline_since = now;
    } else
        offline_since = -1;
    bool connected_links[3]={tab5_usb_connected(),wifi_connected,ble_connected};
    bool ble_standby=link_standby_report(shown,last_link,last_received,now);
    links_render(connected_links,ready_links,ble_standby);
    if(changed)render_pending=true;
    // Keep the cached strip stable for the duration of a gesture. The newest
    // authenticated snapshot is rendered once on release; alerts still run.
    if(render_pending&&!saver&&!interacting){render();render_pending=false;metrics_pending=false;}
    else if(metrics_pending&&!saver&&!interacting&&current_page==4){render_system();metrics_pending=false;}
    // The lightweight graph timer owns sample playback and pixel scrolling.
    if(!saver&&(current_page==5||current_page==6))images_tick(last_tick ? now - last_tick : 0);
    last_tick = now;
    alerts(now, changed);
    codex_notice_tick(now,changed);
    codex_voice_tick();date_editor_voice_tick();
    quick_tick();
    if(quick_visible)quick_feedback_refresh();
    camera_tick();
    photo_tick();
    alert_visual_tick(now);
    health_notice_tick(now);
    agenda_tick(now);
    computer_refresh();
    // Polling results, refreshing hidden task controls and checkpointing drafts
    // can wait until a main-page drag/snap ends. Voice/camera safety still runs.
    if(interacting&&!lv_obj_has_flag(tabs,LV_OBJ_FLAG_HIDDEN))return;
    codex_read_finish(now);
    codex_navigation_timeout(now);
    if(!quick_visible&&codex_nav_queued&&!codex_reading&&now>=codex_read_next)codex_reply_refresh(NULL);
    codex_sync_tick(now);
    photo_open_tick();
    codex_voice_pending_tick(now);
    if(codex_send_queued&&!codex_reading&&!codex_syncing&&!codex_load_waiting) {
        codex_send_queued=false;lv_obj_remove_state(codex_send_button,LV_STATE_DISABLED);
        lv_obj_remove_state(codex_input,LV_STATE_DISABLED);codex_send(NULL);
    }
    codex_control_style();
    if(!quick_visible&&!photo_open_pending&&!photo_flow)codex_prefetch_tick(now);
    if(!quick_visible&&current_page==8&&!saver&&!photo_flow&&!codex_reading&&now>=codex_read_next) {
        if(!codex_nav_pending&&!lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN)&&lv_obj_get_scroll_bottom(codex_reply_scroll)>24)codex_reply_follow=false;
        codex_reply_refresh(NULL);
    }
    if (codex_receipt) {
        codex_physical_input();
        char status[96]; int code = codex_remote_status(codex_selected, status, sizeof(status));
        if (codex_waiting && code != -1) {
            codex_waiting = false;
            lv_obj_remove_state(codex_list, LV_STATE_DISABLED);
            lv_obj_remove_state(codex_project_picker, LV_STATE_DISABLED);
            lv_obj_remove_state(codex_input, LV_STATE_DISABLED);
            lv_obj_remove_state(codex_send_button, LV_STATE_DISABLED);
            ui_text(lv_obj_get_child(codex_send_button,0),"发送");
            codex_feedback_pinned=true;codex_submit_accepted=code==202;
            codex_draft *d=codex_draft_for(codex_selected,false);
            bool rejected=code==400||code==401||code==403||code==404||code==409||code==429||code==503;
            bool definite=code==202||(!codex_querying&&rejected)||(codex_querying&&code==503);
            if(definite&&d) {
                d->request[0]=0;
                if(code==202&&strcmp(codex_action,"interrupt")) {codex_set_text("");codex_store_text(d,"",true);d->dirty=false;memset(d->images,0,sizeof(d->images));for(int i=0;i<3;i++){free(d->thumbnails[i]);d->thumbnails[i]=NULL;}codex_images_refresh();}
                codex_local_dirty=true;codex_checkpoint_write();
            }
            if(d&&d->request[0])lv_obj_add_state(codex_input,LV_STATE_DISABLED);
            codex_control_style();
            if(code==202)snprintf(status,sizeof(status),"%s",!strcmp(codex_action,"interrupt")?"已请求停止":!strcmp(codex_action,"steer")?"已追加，可查看会话动态":"已发送，可查看会话动态");
            snprintf(last_codex_status, sizeof(last_codex_status), "%s", status);
            ui_text(codex_receipt, status[0]?status:"未确认送达，文字已保留");
            ui_text(codex_reply_status,status);
            if(code==202) {
                codex_reply_set_text(!strcmp(codex_action,"interrupt")?"已请求停止，正在更新会话状态…":"消息已发送，正在等待会话动态…");
                codex_read_next=0;
            }
        }
    }
    if (current_page == 9) {
    device_config cfg = device_get_config();
    char ota_state[160]="未开始";
    ota_update_status(ota_state,sizeof(ota_state));
    bool offered=ota_update_available();
    link_kind upgrade_link=rpc_ota_link_select();
    // This callback runs only on the LVGL task; keep notes off its small stack.
    static char offered_version[32],notes[1025];
    ota_update_offer_details(offered_version,sizeof(offered_version),notes,sizeof(notes));
    bool installed=!strcmp(ota_state,"已是最新版本");
    ui_text(ota_hint,offered||installed||!strcmp(ota_state,"电脑已停止提供固件")?"":ota_state);
    if(offered&&upgrade_link==LINK_NONE)ui_text(ota_hint,"请连接 Wi-Fi、USB 或蓝牙后升级。");
    const char *note_text=notes[0]?notes:"暂无更新说明";
    if(strcmp(ota_notes_cached,note_text)&&ota_notes_render(ota_notes,note_text)) {
        snprintf(ota_notes_cached,sizeof(ota_notes_cached),"%s",note_text);
        lv_obj_update_layout(ota_notes_box);lv_obj_scroll_to_y(ota_notes_box,0,LV_ANIM_OFF);
    }
    ota_offer_layout(offered);
    bool can_upgrade=offered&&upgrade_link!=LINK_NONE;
    if(can_upgrade)lv_obj_remove_state(ota_button,LV_STATE_DISABLED);else lv_obj_add_state(ota_button,LV_STATE_DISABLED);
    ui_text(lv_obj_get_child(ota_button,0),!offered?"立即升级":upgrade_link==LINK_USB?"通过 USB 升级":upgrade_link==LINK_WIFI?"通过 Wi-Fi 升级":upgrade_link==LINK_BLE?"通过蓝牙升级":"等待连接");
    if(offered)ui_text_fmt(ota_offer_title,"%s · 有新固件可用",offered_version);
    else ui_text(ota_offer_title,installed?"当前固件已安装":"暂无可升级固件");
    ui_text(ota_empty_hint,installed?"当前固件与电脑提供的版本一致。":"在电脑端选择固件后，版本与更新说明会显示在这里。");
    tint(ota_offer_title,offered?BLUE:MUTED);
    ui_bg_color(ota_offer_card,lv_color_hex(offered?0x243E49:0x303A3E),0);
    ui_border_color(ota_offer_card,lv_color_hex(offered?BLUE:TRACK),0);
    ui_bg_color(ota_button,lv_color_hex(can_upgrade?BLUE:TRACK),0);
    tint(lv_obj_get_child(ota_button,0),can_upgrade?0x17313D:MUTED);
    visible(ota_badge,offered);
    setup_connections(cfg.paired,connected_links,ready_links,ble_available);
    }
    if(network_window&&!lv_obj_has_flag(network_window,LV_OBJ_FLAG_HIDDEN)) {
        network_host_render();
        if(now>=network_retry_until)lv_obj_remove_state(network_retry,LV_STATE_DISABLED);
    }
    if(changed && active!=LINK_NONE) pc_input_update(shown);
    uint32_t idle = lv_display_get_inactive_time(NULL);
    if (saver && !saver_preview && ((idle < 1000 && !saver_is_gallery()) || (offline_saver && active != LINK_NONE))) {
        saver_leave();
        return;
    }
    // Connection loss is not inactivity. Both online and offline use the
    // user's timeout; disabling the saver also disables it while disconnected.
    if (!saver && !quick_visible && !timer_open() && !date_editor_open() && !agenda_editor_open() && !agenda_delete_open() && !clock_picker_is_open() && (!host_editor||lv_obj_has_flag(host_editor,LV_OBJ_FLAG_HIDDEN)) && saver_timed_out(idle)) {
        saver_enter(offline_since >= 0);
        return;
    }
    if (saver || timer_open() || date_editor_open() || agenda_editor_open() || agenda_delete_open() || clock_picker_is_open() || current_page>=8)
        return;
    int resume_fixed;
    if(ui_startup_home_tick(&startup_home,current_page,prefs.selected,prefs.interval,now,last_change,&resume_fixed))return;
    if(resume_fixed>=0){if(current_page!=resume_fixed)page(resume_fixed);return;}
    if(prefs.selected>=0)return;
    if (now < alert_until) {
        if (current_page != 0&&!codex_alert_task[0])
            page(0);
        return;
    }
    if (prefs.cycle) {
        if (now - last_change >= prefs.interval * 1000) {
            int pos = 0;
            for (int i = 0; i < 8; i++)
                if (prefs.order[i] == current_page)
                    pos = i;
            for (int i = 1; i <= 8; i++) {
                int next = prefs.order[(pos + i) % 8];
                if (prefs.enabled[next]) {
                    page(next);
                    return;
                }
            }
            last_change = now;
        }
    } else {
        int target = yes(item(shown, "music"), "playing") ? 5 : 0;
        if (current_page != target)
            page(target);
    }
}
static void tick(lv_timer_t *timer) {
    crash_record_checkpoint(RUNTIME_UI,1,current_page);
    tick_work(timer);
    crash_record_checkpoint(RUNTIME_UI,0,current_page);
}
static void mode_changed(lv_event_t *e) { device_set_mode((int)lv_dropdown_get_selected(lv_event_get_target(e)) - 1); }
static void brightness_changed(lv_event_t *e) {
    int n = lv_slider_get_value(lv_event_get_target(e));
    ui_text_fmt(brightness_value,"%d%%",n);
    set_backlight(n);
    if(lv_event_get_code(e)==LV_EVENT_RELEASED)device_set_brightness(n);
}
static void sound_refresh(void) {
    device_sound s=device_get_sound();
    lv_slider_set_value(sound_slider,s.volume,LV_ANIM_OFF);
    ui_text_fmt(sound_value,"%d%%",s.volume);
    if(s.muted)lv_obj_add_state(sound_mute,LV_STATE_CHECKED);
    else lv_obj_remove_state(sound_mute,LV_STATE_CHECKED);
    if(s.muted||!s.volume)lv_obj_add_state(sound_test,LV_STATE_DISABLED);
    else lv_obj_remove_state(sound_test,LV_STATE_DISABLED);
    sound_header_render();
}
static void sound_changed(lv_event_t *e) {
    ui_text_fmt(sound_value,"%d%%",(int)lv_slider_get_value(sound_slider));
    if(lv_event_get_target(e)==sound_slider&&lv_event_get_code(e)!=LV_EVENT_RELEASED)return;
    bool ok=device_set_sound(lv_slider_get_value(sound_slider),lv_obj_has_state(sound_mute,LV_STATE_CHECKED));
    sound_refresh();
    ui_text(sound_hint,ok?(device_sound_volume()?"已保存":"已静音"):"保存失败，请重试");
}
static void sound_preview(lv_event_t *e) {
    (void)e;
    if(!device_sound_volume()){ui_text(sound_hint,"已静音");return;}
    ui_text(sound_hint,voice_remote_preview_sound()?"":"语音忙碌，请结束后试听");
}
static int display_choice(void) {return prefs.selected>=0?1:prefs.cycle?0:2;}
static void display_controls_refresh(void) {
    if(!display_choices[0])return;
    int choice=display_choice();
    if(prefs.selected>=0)display_fixed_choice=prefs.selected;
    for(int i=0;i<3;i++) {
        bool selected=i==choice;
        if(selected)lv_obj_add_state(display_choices[i],LV_STATE_CHECKED);else lv_obj_remove_state(display_choices[i],LV_STATE_CHECKED);
        ui_bg_color(display_choices[i],lv_color_hex(selected?BLUE:UI_BACKGROUND),0);
        ui_text_color(display_choices[i],lv_color_hex(selected?0x173642:MUTED),0);
        ui_border_color(display_choices[i],lv_color_hex(selected?BLUE:TRACK),0);
    }
    const char *hints[]={"按下方勾选的页面和顺序循环展示。","退出设置后显示所选页面。","播放音乐时显示音乐，其余显示总览。"};
    ui_text(display_mode_hint,hints[choice]);
    lv_dropdown_set_selected(display_fixed_page,display_fixed_choice);
    lv_dropdown_set_selected(remote_interval,prefs.interval==10?0:prefs.interval==15?1:prefs.interval==30?2:3);
    visible(display_cycle_options,choice==0);visible(display_fixed_options,choice==1);
    for(int i=0;i<8;i++) {
        ui_text_fmt(order_rows[i],"%d. %s",i+1,names[prefs.order[i]]);
        if(prefs.enabled[prefs.order[i]])lv_obj_add_state(cycle_checks[i],LV_STATE_CHECKED);else lv_obj_remove_state(cycle_checks[i],LV_STATE_CHECKED);
    }
    int options_height=choice==0?650:choice==1?56:0;
    int home_y=192+options_height+(options_height?28:0);
    lv_obj_set_y(display_home_options,home_y);
    lv_obj_set_height(settings_panels[2],home_y+344+24);
    lv_obj_update_layout(settings_scroll);
    lv_obj_scroll_to_y(settings_scroll,lv_obj_get_scroll_y(settings_scroll),LV_ANIM_OFF);
}
static bool display_preferences_commit(const ui_preferences *next) {
    if(!ui_preferences_save(next)) {
        display_controls_refresh();settings_feedback("保存失败，请重试",true);return false;
    }
    prefs=*next;preferences_due=0;last_change=esp_timer_get_time()/1000;
    display_controls_refresh();settings_feedback("已保存到 TAB5",false);return true;
}
static void display_mode_changed(lv_event_t *e) {
    int choice=(int)(intptr_t)lv_event_get_user_data(e);
    if(choice<0||choice>2||choice==display_choice())return;
    ui_preferences next=prefs;next.cycle=choice==0;next.selected=choice==1?display_fixed_choice:-1;
    if(next.cycle) {
        bool any=false;for(int i=0;i<8;i++)any|=next.enabled[i];
        if(!any)next.enabled[next.order[0]]=true;
    }
    if(display_preferences_commit(&next)) {
        if(current_page==9)settings_return=next.selected>=0?next.selected:0;
        lv_obj_scroll_to_y(settings_scroll,0,LV_ANIM_OFF);
    }
}
static void display_fixed_changed(lv_event_t *e) {
    if(display_choice()!=1)return;
    ui_preferences next=prefs;next.selected=(int)lv_dropdown_get_selected(lv_event_get_target(e));
    if(next.selected>=0&&next.selected<8&&display_preferences_commit(&next)&&current_page==9)settings_return=next.selected;
}
static void tab_changed(lv_event_t *e) {
    (void)e;
    // A queued tab snap can finish after idle starts the saver. Its hidden
    // background event must not clear saver while leaving its layer visible.
    if (changing_page || saver)
        return;
    int slot=lv_tabview_get_tab_active(tabs);
    if(slot<0||slot>=4)return;
    // LVGL already selected the tab and started its snap animation. Do not
    // restart it with LV_ANIM_OFF from its own VALUE_CHANGED callback.
    bool default_codex=primary_pages[slot]==8&&current_page!=8;
    page_select(primary_pages[slot],false);
    if(default_codex){codex_default_pending=codex_default_all=true;render_codex();}
    last_change = esp_timer_get_time() / 1000;
    saver = false;
    if (current_page < 8 && prefs.selected>=0) {
        prefs.selected = current_page;
        display_controls_refresh();
        preferences_due=last_change+750;
    }
}
static void automatic(lv_event_t *e) {
    (void)e;
    ui_preferences next=prefs;next.selected=-1;next.cycle=true;
    bool any=false;for(int i=0;i<8;i++)any|=next.enabled[i];
    if(!any)next.enabled[next.order[0]]=true;
    if(!display_preferences_commit(&next))return;
    saver = false;
    page(0);
}
static void pref_changed(lv_event_t *e) {
    int which = (int)(intptr_t)lv_event_get_user_data(e);
    lv_obj_t *o = lv_event_get_target(e);
    int selected = (which == 2 || which == 3) ? lv_dropdown_get_selected(o) : 0;
    if(which==2||(which>=20&&which<28)) {
        ui_preferences next=prefs;
        if(which==2){int values[]={10,15,30,60};next.interval=values[selected];}
        else {
            next.enabled[next.order[which-20]]=lv_obj_has_state(o,LV_STATE_CHECKED);
            bool any=false;for(int i=0;i<8;i++)any|=next.enabled[i];
            if(!any){display_controls_refresh();settings_feedback("至少保留一页",true);return;}
        }
        display_preferences_commit(&next);return;
    }
    if (which == 1)
        prefs.alerts = lv_obj_has_state(o, LV_STATE_CHECKED);
    else if (which == 3) {
        int values[] = {0, 1, 5, 10, 30, 60};
        prefs.saver_minutes = values[selected];
    } else
        prefs.enabled[which - 10] = lv_obj_has_state(o, LV_STATE_CHECKED);
    save();
}
static void order_changed(lv_event_t *e) {
    int pos = (int)(intptr_t)lv_event_get_user_data(e);
    if (pos > 0) {
        ui_preferences next=prefs;uint8_t swap=next.order[pos];
        next.order[pos]=next.order[pos-1];next.order[pos-1]=swap;
        display_preferences_commit(&next);
    }
}
static void quota_changed(lv_event_t *e) {
    selected_quota = lv_dropdown_get_selected(lv_event_get_target(e));
    render_quota();
}
static void home_model_changed(lv_event_t *e) {
    int id=(int)(intptr_t)lv_event_get_user_data(e);lv_obj_t *target=lv_event_get_target(e);
    bool checked=lv_obj_has_state(target,LV_STATE_CHECKED);
    if(id<0&&!checked){lv_obj_add_state(target,LV_STATE_CHECKED);return;}
    unsigned next=id<0?0:checked?home_models|(1u<<id):home_models&~(1u<<id);
    unsigned count=0;for(int i=0;i<UI_HOME_PROVIDERS;i++)count+=!!(next&(1u<<i));
    if(count>3||!ui_home_models_save(next)) {
        if(checked)lv_obj_remove_state(target,LV_STATE_CHECKED);else lv_obj_add_state(target,LV_STATE_CHECKED);
        settings_feedback(count>3?"最多选三个模型":"保存失败，请重试",true);return;
    }
    home_models=next;
    for(int i=-1;i<UI_HOME_PROVIDERS;i++) {
        lv_obj_t *box=i<0?home_auto_check:home_model_checks[i];bool selected=i<0?!next:(next&(1u<<i))!=0;
        if(selected)lv_obj_add_state(box,LV_STATE_CHECKED);else lv_obj_remove_state(box,LV_STATE_CHECKED);
    }
    overview_domestic();settings_feedback(next?"总览模型已保存":"已恢复自动选择",false);
}
static void workbench_go(lv_event_t *e) {
    int target=(int)(intptr_t)lv_event_get_user_data(e);
    if(target==-1){automatic(e);return;}
    if(target==-2){camera_action(e);return;}
    if(target==-3) {
        if(!home_task_id[0]){page(4);detail_return=0;return;}
        if(codex_waiting||codex_voice_waiting||codex_send_queued||!home_task_id[0]||!codex_save_draft())return;
        char selected[37],completed_turn[64];snprintf(selected,sizeof(selected),"%s",home_task_id);
        snprintf(completed_turn,sizeof(completed_turn),"%s",home_task_unread?home_task_turn:"");
        snprintf(codex_project_selected,sizeof(codex_project_selected),"%s",home_task_project);
        codex_restore_project=false;render_codex();
        for(int i=0;i<8;i++)if(!strcmp(codex_task_ids[i],selected)) {
            lv_obj_send_event(codex_task_rows[i],LV_EVENT_CLICKED,NULL);page(8);
            if(completed_turn[0])codex_reply_open_turn(e,completed_turn);
            return;
        }
        return;
    }
    if(target>=2&&target<8)detail_return=11;
    bool default_codex=target==8&&current_page!=8;
    page(target);
    if(default_codex){codex_default_pending=codex_default_all=true;render_codex();}
    if(target<8&&prefs.selected>=0){ui_preferences next=prefs;next.selected=target;display_preferences_commit(&next);}
}
static void workbench_return(lv_event_t *e) {
    if(current_page==7&&clock_return_page>=0) {
        int target=clock_return_page,detail=clock_return_detail,settings=clock_return_settings;
        page(target);detail_return=detail;settings_return=settings;return;
    }
    (void)e;if(health_back())return;page(current_page==9?settings_return:detail_return);
}
static void header_clock_open(lv_event_t *e) {
    (void)e;if(current_page==7)return;
    clock_return_page=current_page;clock_return_detail=detail_return;clock_return_settings=settings_return;
    page(7);
}
static void overview_open(lv_event_t *e) {
    int target=(int)(intptr_t)lv_event_get_user_data(e);
    if(target==2||target==12){page(target);detail_return=0;return;}
    selected_quota=target;lv_dropdown_set_selected(quota_picker,target);render_quota();page(1);
}
static void settings_select(int selected) {
    if(selected<0||selected>=SETTINGS_CATEGORIES)return;
    for(int i=0;i<SETTINGS_CATEGORIES;i++) {
        visible(settings_panels[i],i==selected);
        ui_bg_color(settings_categories[i],lv_color_hex(i==selected?0x354A52:0x20272A),0);
        ui_text_color(settings_categories[i],lv_color_hex(i==selected?0xD2EAF2:0xA5B5B9),0);
    }
    settings_content=settings_panels[selected];
    if(selected==4)visible(save_status,false);
    lv_obj_update_layout(settings_scroll);
    lv_obj_scroll_to_y(settings_scroll,0,LV_ANIM_OFF);
}
static void settings_category_changed(lv_event_t *e) {
    settings_select((int)(intptr_t)lv_event_get_user_data(e));
}
static void connection_details(lv_event_t *e) {
    (void)e;page(9);settings_select(0);
}
static void sound_details(lv_event_t *e) {
    (void)e;page(9);settings_select(1);
}
// The strip has a transparent background, but adjacent opaque pages jointly
// cover it while swiping. Let LVGL start there instead of repainting the whole
// parent background behind those pages. Elastic gaps keep the normal fallback.
static void page_strip_cover(lv_event_t *event) {
    lv_cover_check_info_t *info=lv_event_get_param(event);
    if(info->res==LV_COVER_RES_MASKED)return;
    lv_obj_t *strip=lv_event_get_target(event);
    int covered=info->area->x1;
    for(uint32_t i=0;i<lv_obj_get_child_count(strip);i++) {
        lv_obj_t *child=lv_obj_get_child(strip,i);lv_area_t area;lv_obj_get_coords(child,&area);
        if(lv_obj_has_flag(child,LV_OBJ_FLAG_HIDDEN)||lv_obj_get_style_bg_opa(child,0)!=LV_OPA_COVER||
           lv_obj_get_style_opa(child,0)!=LV_OPA_COVER||lv_obj_get_style_radius(child,0)||
           lv_obj_get_style_transform_rotation(child,0)||lv_obj_get_style_transform_scale_x(child,0)!=LV_SCALE_NONE||
           lv_obj_get_style_transform_scale_y(child,0)!=LV_SCALE_NONE)continue;
        if(area.y1>info->area->y1||area.y2<info->area->y2||area.x1>covered||area.x2<covered)continue;
        covered=area.x2+1;if(covered>info->area->x2){info->res=LV_COVER_RES_COVER;return;}
    }
}
static int cache_hold(const lv_obj_t *obj) {
    if(!tabs||!detail_layer||!lv_obj_has_flag(detail_layer,LV_OBJ_FLAG_HIDDEN))return false;
    lv_obj_t *strip=lv_tabview_get_content(tabs);
    bool sliding=lv_anim_get(strip,NULL)!=NULL;
    for(lv_indev_t *input=lv_indev_get_next(NULL);input;input=lv_indev_get_next(input))
        if(lv_indev_get_scroll_obj(input)==strip)sliding=true;
    for(int i=0;i<4;i++)if(obj==pages[primary_pages[i]])return sliding?2:gesture_active()?1:0;
    return false;
}
static bool native_swipe_render(lv_layer_t *layer) {
    if(!detail_layer||saver||ota_update_busy()||photo_flow||
       !lv_obj_has_flag(detail_layer,LV_OBJ_FLAG_HIDDEN)||
       !lv_obj_has_flag(camera_layer,LV_OBJ_FLAG_HIDDEN)||
       !lv_obj_has_flag(codex_keyboard,LV_OBJ_FLAG_HIDDEN)||
       !lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN))return false;
    lv_obj_t *parent=lv_obj_get_parent(tabs);
    for(uint32_t i=lv_obj_get_index(tabs)+1;i<lv_obj_get_child_count(parent);i++) {
        lv_obj_t *sibling=lv_obj_get_child(parent,i);lv_area_t bounds,overlap;
        if(lv_obj_has_flag(sibling,LV_OBJ_FLAG_HIDDEN))continue;
        lv_obj_get_coords(sibling,&bounds);
        if(lv_area_intersect(&overlap,&bounds,&layer->buf_area))return false;
    }
    return tab5_draw_cache_native_render(layer);
}
static void cache_tick_work(lv_timer_t *timer) {
    (void)timer;
    if(screen_preview_cache_paused(esp_timer_get_time()/1000)||saver||quick_visible||agenda_editor_open()||agenda_delete_open()||ota_update_busy()||codex_voice_waiting||photo_flow||
       !lv_obj_has_flag(camera_layer,LV_OBJ_FLAG_HIDDEN))return;
    if(current_page==4||current_page==5||current_page==6||current_page==7)return;
    if(current_page==8&&(!lv_obj_has_flag(codex_keyboard,LV_OBJ_FLAG_HIDDEN)||
                         !lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN)))return;
    bool interacting=lv_display_get_inactive_time(NULL)<300||lv_anim_count_running();
    for(lv_indev_t *input=lv_indev_get_next(NULL);input;input=lv_indev_get_next(input))
        if(lv_indev_get_state(input)==LV_INDEV_STATE_PRESSED||lv_indev_get_scroll_obj(input))interacting=true;
    lv_obj_t *target=current_page==9?settings_content:pages[current_page];
    if(interacting) {
        // Settings retain their bounded cold vertical-scroll cache path.
        if(current_page==9)tab5_draw_cache_step_budget(target,1280u*32u);
        return; // Main-page swipes never construct snapshots.
    }
#if TAB5_LOCAL_ART
    if(gallery_library_pending()){tab5_draw_cache_step(target);return;}
#endif
    if(current_page!=9&&lv_obj_has_flag(detail_layer,LV_OBJ_FLAG_HIDDEN)&&
       tab5_draw_cache_has_image(target)) {
        // Repeated updates of an already captured page must not monopolize
        // the single staging image. Prepare cold destinations before repairing
        // hot ones; visible widgets still render their latest data normally.
        int order[]={primary_index,primary_index+1,primary_index-1,0,1,2,3};
        for(unsigned i=0;i<sizeof(order)/sizeof(order[0]);i++) {
            if(order[i]<0||order[i]>=4)continue;
            lv_obj_t *candidate=pages[primary_pages[order[i]]];
            if(!tab5_draw_cache_has_image(candidate)){tab5_draw_cache_step(candidate);return;}
        }
    }
    if(!tab5_draw_cache_is_ready(target)){tab5_draw_cache_step(target);return;}
    tab5_draw_cache_step(target); // Keep the visible page most recent during preheating.
    if(current_page==9)return;
    if(!lv_obj_has_flag(detail_layer,LV_OBJ_FLAG_HIDDEN))return;
    int neighbors[]={primary_index+1,primary_index-1};
    for(unsigned i=0;i<2;i++)if(neighbors[i]>=0&&neighbors[i]<4) {
        lv_obj_t *neighbor=pages[primary_pages[neighbors[i]]];
        if(!tab5_draw_cache_is_ready(neighbor)){tab5_draw_cache_step(neighbor);return;}
    }
    for(int i=0;i<4;i++)if(!tab5_draw_cache_is_ready(pages[primary_pages[i]])) {
        tab5_draw_cache_step(pages[primary_pages[i]]);break;
    }
}
static void cache_tick(lv_timer_t *timer) {
    crash_record_checkpoint(RUNTIME_CACHE,1,current_page);
    cache_tick_work(timer);
    crash_record_checkpoint(RUNTIME_CACHE,0,current_page);
}
static void runtime_display_event(lv_event_t *event) {
    unsigned channel,stage;
    switch(lv_event_get_code(event)) {
        case LV_EVENT_REFR_START:channel=RUNTIME_REFRESH;stage=1;break;
        case LV_EVENT_RENDER_START:channel=RUNTIME_REFRESH;stage=2;break;
        case LV_EVENT_RENDER_READY:channel=RUNTIME_REFRESH;stage=3;break;
        case LV_EVENT_REFR_READY:channel=RUNTIME_REFRESH;stage=0;break;
        case LV_EVENT_FLUSH_START:channel=RUNTIME_FLUSH;stage=1;break;
        case LV_EVENT_FLUSH_FINISH:channel=RUNTIME_FLUSH;stage=2;break;
        case LV_EVENT_FLUSH_WAIT_START:channel=RUNTIME_FLUSH;stage=3;break;
        case LV_EVENT_FLUSH_WAIT_FINISH:channel=RUNTIME_FLUSH;stage=0;break;
        default:return;
    }
    crash_record_checkpoint(channel,stage,current_page);
}
static void app_icon(lv_obj_t *button_obj,int kind) {
    const uint32_t colors[]={AMBER,BLUE,GREEN,0xD5BCE5,0xB9D6D2,0xE2B5A7};
    const uint32_t backgrounds[]={0x403A2F,0x2B414A,0x2C4036,0x3D3348,0x2B403F,0x493A36};
    uint32_t c=colors[kind];
    lv_obj_t *icon=shape(button_obj,0,0,80,80,backgrounds[kind],24);
    lv_obj_align(icon,LV_ALIGN_LEFT_MID,24,0);
    lv_obj_remove_flag(icon,LV_OBJ_FLAG_CLICKABLE);
    if(kind==0) {
        shape(icon,26,26,28,28,c,LV_RADIUS_CIRCLE);
        shape(icon,38,12,4,9,c,2);shape(icon,38,59,4,9,c,2);
        shape(icon,12,38,9,4,c,2);shape(icon,59,38,9,4,c,2);
    }else if(kind==1) {
        lv_obj_t *monitor=shape(icon,15,20,50,34,c,5);
        ui_bg_opa(monitor,LV_OPA_TRANSP,0);lv_obj_set_style_border_width(monitor,3,0);
        ui_border_color(monitor,lv_color_hex(c),0);
        shape(icon,38,55,4,8,c,1);shape(icon,26,63,28,3,c,1);
    }else if(kind==2) {
        shape(icon,20,41,8,19,c,3);shape(icon,36,31,8,29,c,3);shape(icon,52,20,8,40,c,3);
    }else if(kind==3) {
        lv_obj_t *note=lv_label_create(icon);lv_label_set_text(note,LV_SYMBOL_AUDIO);
        ui_text_font(note,&lv_font_montserrat_40,0);tint(note,c);lv_obj_center(note);
    }else if(kind==4) {
        lv_obj_t *face=shape(icon,16,16,48,48,c,LV_RADIUS_CIRCLE);
        ui_bg_opa(face,LV_OPA_TRANSP,0);lv_obj_set_style_border_width(face,3,0);
        ui_border_color(face,lv_color_hex(c),0);
        shape(icon,38,26,4,16,c,2);shape(icon,40,38,13,4,c,2);
    }else {
        shape(icon,28,40,28,22,c,11);
        shape(icon,17,30,11,15,c,LV_RADIUS_CIRCLE);shape(icon,30,20,11,16,c,LV_RADIUS_CIRCLE);
        shape(icon,45,20,11,16,c,LV_RADIUS_CIRCLE);shape(icon,58,30,11,15,c,LV_RADIUS_CIRCLE);
    }
}
volatile unsigned tab5_ui_internal_buffer_bytes,tab5_ui_internal_free;
#ifndef TAB5_UI_PREVIEW
static void *internal_draw_buffer;
static void ui_internal_buffer_try(lv_timer_t *timer) {
    // Wait for Wi-Fi/BLE startup so a rendering optimization cannot consume
    // their initialization reserve. Allocation failure keeps the original path.
    if(!tab5_wifi_initialized||gesture_active()||ota_update_busy())return;
    const unsigned bytes=1280u*24u*2u;
    tab5_ui_internal_free=heap_caps_get_free_size(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT);
    if(tab5_ui_internal_free>=bytes+160u*1024u)
        internal_draw_buffer=heap_caps_aligned_alloc(128,bytes,MALLOC_CAP_INTERNAL|MALLOC_CAP_DMA|MALLOC_CAP_8BIT);
    if(internal_draw_buffer) {
        lv_display_t *display=lv_display_get_default();
        // The port still owns its PSRAM buffers and rotation destination.
        // The active raster strip now stays in fast internal RAM.
        lv_display_set_buffers(display,internal_draw_buffer,NULL,bytes,LV_DISPLAY_RENDER_MODE_PARTIAL);
        tab5_ui_internal_buffer_bytes=bytes;lv_obj_invalidate(lv_screen_active());
    }
    lv_timer_delete(timer);
}
#endif
static void ui_damage_align(lv_event_t *event) {
    // LVGL also uses INVALIDATE_AREA to choose each partial-buffer height.
    // 1220-wide pages otherwise produce 67-row strips, forcing the quarter-
    // turn RGB565 routine onto its scalar path for the entire strip.
    lv_area_t *area=lv_event_get_param(event);
    area->x1&=~1;area->y1&=~1;area->x2|=1;area->y2|=1;
}
#if defined(__GNUC__) && !defined(__clang__)
__attribute__((optimize("Os")))
#endif
void ui_start(void) {
    if(!ui_voice_work)ui_voice_work=codex_alloc(sizeof(*ui_voice_work));
    LV_ASSERT_MALLOC(ui_voice_work);
    tab5_draw_cache_init();tab5_draw_cache_interaction=cache_hold;
#ifndef TAB5_UI_PREVIEW
    extern void tab5_fast_copy_init(void);tab5_fast_copy_init();
#endif
    prefs = ui_preferences_load();
    screen_orientation_mode=ui_orientation_load();
    screen_orientation=(ui_orientation_filter){.direction=screen_orientation_mode==2?2:0,.candidate=3};
    if(!codex_bookmarks) {
        codex_bookmarks=codex_alloc(32*sizeof(*codex_bookmarks));
        if(codex_bookmarks&&ui_reading_load(codex_bookmarks))for(int i=0;i<32;i++)if(codex_bookmarks[i].stamp>codex_bookmark_stamp)codex_bookmark_stamp=codex_bookmarks[i].stamp;
    }
    clock_style = ui_clock_style_load();
    saver_style = ui_saver_style_normalize(ui_saver_style_load());
    tf_cache_enabled=ui_tf_cache_load();
#if TAB5_LOCAL_ART
    gallery_storage_enable(tf_cache_enabled);
#endif
    home_models=ui_home_models_load();
    if(prefs.cycle)prefs.selected=-1;
    bsp_reset_tp();
    bsp_display_cfg_t cfg = {.lvgl_port_cfg = ESP_LVGL_PORT_INIT_CONFIG(),
                             .buffer_size = TAB5_DRAW_BUFFER_PIXELS,
                             .double_buffer = true,
                             .flags = {.buff_dma = true, .buff_spiram = true, .sw_rotate = true}};
#ifndef TAB5_UI_PREVIEW
    cfg.lvgl_port_cfg.task_stack = 10240;
    // Keep image rendering separate from CSI capture/preparation on core 0.
    // TinyUSB remains a higher-priority task on core 1 and can preempt rendering.
    cfg.lvgl_port_cfg.task_affinity = 1;
#endif
    lv_display_t *display = bsp_display_start_with_config(&cfg);
    bsp_display_lock(0);
    lv_display_add_event_cb(display,runtime_display_event,LV_EVENT_ALL,NULL);
    lv_display_set_rotation(display, LV_DISPLAY_ROTATION_90);
    screen_orientation_apply(screen_orientation.direction);
    lv_display_add_event_cb(display,ui_damage_align,LV_EVENT_INVALIDATE_AREA,NULL);
    lv_obj_t *screen = lv_screen_active();
    // Pages own their scrolling; the screen must stay fixed while the camera
    // changes aspect ratio over the hidden landscape pages.
    lv_obj_remove_flag(screen,LV_OBJ_FLAG_SCROLLABLE);
    ui_bg_color(screen, lv_color_hex(UI_BACKGROUND), 0);
    workbench_title=label(screen,"总览",30,21,180,false);
    ui_text_font(workbench_title,&tab5_font_30,0);
    health_segments=shape(screen,96,12,284,56,SURFACE,16);
    health_system_entry=button(health_segments,"性能",4,4,136,health_close,0);
    health_header_entry=button(health_segments,"诊断",144,4,136,health_show,0);
    health_segments_refresh();visible(health_segments,false);visible(health_header_entry,false);
    const char *link_icons[]={LV_SYMBOL_USB,LV_SYMBOL_WIFI,LV_SYMBOL_BLUETOOTH};
    for(int i=0;i<3;i++) {
        link_buttons[i]=button(screen,"",690+i*50,12,46,connection_details,0);
        lv_obj_set_height(link_buttons[i],48);
        link_symbols[i]=lv_obj_get_child(link_buttons[i],0);ui_text(link_symbols[i],link_icons[i]);
        ui_text_font(link_symbols[i],&lv_font_montserrat_24,0);lv_obj_center(link_symbols[i]);
        link_visual_state[i]=-1;
    }
    const bool no_links[3]={false,false,false};links_render(no_links,no_links,false);
    clock_label=label(screen,"--:--:--",1034,20,144,false);
    lv_obj_add_flag(clock_label,LV_OBJ_FLAG_CLICKABLE);
    lv_obj_set_ext_click_area(clock_label,12);
    lv_obj_add_event_cb(clock_label,header_clock_open,LV_EVENT_CLICKED,NULL);
    battery_outline=shape(screen,866,19,68,34,TRACK,6);
    ui_bg_opa(battery_outline,LV_OPA_TRANSP,0);lv_obj_set_style_border_width(battery_outline,2,0);
    battery_cap=shape(screen,935,29,4,14,MUTED,2);
    battery_fill=shape(screen,870,23,60,26,GREEN,3);ui_bg_opa(battery_fill,LV_OPA_50,0);visible(battery_fill,false);
    battery_label=label(screen,"--",870,21,60,false);lv_obj_set_height(battery_label,30);
    lv_obj_set_style_text_align(battery_label,LV_TEXT_ALIGN_CENTER,0);
    lv_label_set_long_mode(battery_label,LV_LABEL_LONG_CLIP);
    battery_charge=label(screen,LV_SYMBOL_CHARGE,947,24,24,false);
    ui_text_font(battery_charge,&lv_font_montserrat_24,0);tint(battery_charge,GREEN);visible(battery_charge,false);
    sound_header_button=button(screen,"",980,12,44,sound_details,0);
    ui_bg_color(sound_header_button,lv_color_hex(UI_BACKGROUND),0);
    sound_header_icon=lv_obj_get_child(sound_header_button,0);
    ui_text_font(sound_header_icon,&lv_font_montserrat_24,0);sound_header_render();
    banner=muted(screen,"",264,14,404);
    lv_obj_set_style_pad_left(banner,32,0);lv_obj_set_style_pad_top(banner,8,0);
    lv_obj_set_style_radius(banner,10,0);
    alert_dot=shape(screen,276,32,8,8,AMBER,LV_RADIUS_CIRCLE);visible(alert_dot,false);
    lv_obj_add_flag(banner,LV_OBJ_FLAG_CLICKABLE);
    lv_obj_add_event_cb(banner,codex_notice_open,LV_EVENT_CLICKED,NULL);
    lv_obj_set_height(banner,48);lv_label_set_long_mode(banner,LV_LABEL_LONG_DOT);
    health_banner_icon=label(screen,LV_SYMBOL_WARNING,262,25,24,false);
    ui_text_font(health_banner_icon,&lv_font_montserrat_24,0);visible(health_banner_icon,false);
    tabs=lv_tabview_create(screen);
    tab5_draw_cache_native_strip=lv_tabview_get_content(tabs);
    extern bool (*tab5_lvgl_native_render)(lv_layer_t *);
    tab5_lvgl_native_render=native_swipe_render;
    lv_obj_set_pos(tabs,30,92);lv_obj_set_size(tabs,1220,516);
    lv_tabview_set_tab_bar_size(tabs,0);
    ui_text_font(tabs,&tab5_font_24,0);
    ui_bg_color(tabs,lv_color_hex(UI_BACKGROUND),0);
    ui_bg_opa(tabs,LV_OPA_TRANSP,0);
    lv_obj_t *page_strip=lv_tabview_get_content(tabs);
    ui_bg_opa(page_strip,LV_OPA_TRANSP,0);
    lv_obj_add_event_cb(page_strip,page_strip_cover,LV_EVENT_COVER_CHECK,NULL);
    ui_text_color(tabs,lv_color_hex(INK),0);
    for(int i=0;i<4;i++) {
        lv_obj_t *p=lv_tabview_add_tab(tabs,names[primary_pages[i]]);pages[primary_pages[i]]=p;
        lv_obj_set_style_pad_all(p,0,0);lv_obj_set_style_radius(p,0,0);
        ui_bg_color(p,lv_color_hex(UI_BACKGROUND),0);ui_bg_opa(p,LV_OPA_COVER,0);
        lv_obj_remove_flag(p,LV_OBJ_FLAG_SCROLLABLE);
    }
    pages[10]=pages[11];
    detail_layer=lv_obj_create(screen);lv_obj_remove_style_all(detail_layer);
    lv_obj_set_pos(detail_layer,30,92);lv_obj_set_size(detail_layer,1220,516);
    ui_text_font(detail_layer,&tab5_font_24,0);
    ui_text_color(detail_layer,lv_color_hex(INK),0);
    lv_obj_remove_flag(detail_layer,LV_OBJ_FLAG_SCROLLABLE);
    for(int i=2;i<=9;i++)if(i!=8) {
        pages[i]=lv_obj_create(detail_layer);lv_obj_remove_style_all(pages[i]);
        lv_obj_set_size(pages[i],1220,516);
        ui_bg_color(pages[i],lv_color_hex(UI_BACKGROUND),0);
        ui_bg_opa(pages[i],LV_OPA_COVER,0);
        lv_obj_remove_flag(pages[i],LV_OBJ_FLAG_SCROLLABLE);
        visible(pages[i],false);
    }
    visible(detail_layer,false);
    pages[12]=lv_obj_create(detail_layer);lv_obj_remove_style_all(pages[12]);
    lv_obj_set_size(pages[12],1220,516);ui_bg_color(pages[12],lv_color_hex(UI_BACKGROUND),0);ui_bg_opa(pages[12],LV_OPA_COVER,0);
    lv_obj_remove_flag(pages[12],LV_OBJ_FLAG_SCROLLABLE);visible(pages[12],false);calendar_create(pages[12]);
    pages[13]=lv_obj_create(detail_layer);lv_obj_remove_style_all(pages[13]);lv_obj_set_size(pages[13],1220,516);
    ui_bg_color(pages[13],lv_color_hex(UI_BACKGROUND),0);ui_bg_opa(pages[13],LV_OPA_COVER,0);
    lv_obj_remove_flag(pages[13],LV_OBJ_FLAG_SCROLLABLE);visible(pages[13],false);agenda_create(pages[13],screen);
    pages[14]=lv_obj_create(detail_layer);lv_obj_remove_style_all(pages[14]);lv_obj_set_size(pages[14],1220,516);
    ui_bg_color(pages[14],lv_color_hex(UI_BACKGROUND),0);ui_bg_opa(pages[14],LV_OPA_COVER,0);
    lv_obj_remove_flag(pages[14],LV_OBJ_FLAG_SCROLLABLE);visible(pages[14],false);computer_create(pages[14]);
    lv_obj_add_flag(lv_tabview_get_tab_bar(tabs),LV_OBJ_FLAG_HIDDEN);
    const char *nav_names[]={"总览","Codex","额度","应用"};
    for(int i=0;i<4;i++) {
        workbench_nav[i]=button(screen,nav_names[i],30+i*288,628,271,workbench_go,primary_pages[i]);
        lv_obj_set_height(workbench_nav[i],76);lv_obj_set_style_radius(workbench_nav[i],16,0);
        lv_obj_center(lv_obj_get_child(workbench_nav[i],0));
    }
    codex_unread_dot=shape(workbench_nav[1],235,10,24,24,NOTICE_GREEN,LV_RADIUS_CIRCLE);visible(codex_unread_dot,false);
    codex_unread_count=lv_label_create(codex_unread_dot);
    ui_text_font(codex_unread_count,&lv_font_montserrat_14,0);tint(codex_unread_count,0xFFFFFF);
    lv_label_set_text(codex_unread_count,"0");lv_obj_center(codex_unread_count);
    workbench_back=workbench_back_button(screen,workbench_return);visible(workbench_back,false);
    settings_button=workbench_icon_button(screen,12,workbench_go,9);
    lv_obj_t *settings_icon=lv_obj_get_child(settings_button,0);lv_label_set_text(settings_icon,LV_SYMBOL_SETTINGS);
    ui_text_font(settings_icon,&lv_font_montserrat_32,0);lv_obj_center(settings_icon);
    const char *app_names[]={"日程","天气","系统","行情","音乐","时钟","桌宠","控制"};
    const int app_pages[]={13,2,4,3,5,7,6,14};
    const char *app_hints[]={"待办与提醒","实况与预报","电脑运行状态","关注的市场行情","播放与切歌","表盘与倒计时","AI 任务状态","音量与快捷启动"};
    for(int i=0;i<8;i++) {
        lv_obj_t *b=button(pages[11],app_names[i],(i%3)*414,(i/3)*176,390,workbench_go,app_pages[i]);
        lv_obj_set_height(b,156);lv_obj_set_style_radius(b,22,0);lv_obj_set_style_pad_all(b,0,0);
        ui_bg_color(b,lv_color_hex(SURFACE),0);
        lv_obj_t *caption=lv_obj_get_child(b,0);ui_text_font(caption,&tab5_font_30,0);
        lv_obj_align(caption,LV_ALIGN_LEFT_MID,128,-20);
        lv_obj_t *hint=muted(b,app_hints[i],128,91,240);
        if(i==7){lv_obj_t *icon=label(b,LV_SYMBOL_SETTINGS,24,50,80,false);ui_text_font(icon,&lv_font_montserrat_40,0);tint(icon,BLUE);}
        else if(i)app_icon(b,i-1);
        else {lv_obj_t *icon=label(b,LV_SYMBOL_LIST,24,50,80,false);ui_text_font(icon,&lv_font_montserrat_40,0);tint(icon,BLUE);}
        lv_obj_t *arrow=lv_label_create(b);lv_label_set_text(arrow,LV_SYMBOL_RIGHT);
        ui_text_font(arrow,&lv_font_montserrat_24,0);tint(arrow,0x82969D);
        lv_obj_align(arrow,LV_ALIGN_RIGHT_MID,-24,0);
    }
    lv_obj_add_event_cb(tabs, tab_changed, LV_EVENT_VALUE_CHANGED, NULL);
    overview_create(pages[0]);
    overview_layout();
    quota_picker=dropdown(pages[1], "Claude\nCodex\n百炼\nKimi\nMiniMax\nDeepSeek\n智谱\n阶跃\n百度\n小米\n火山方舟",0,0,280,selected_quota,quota_changed,0);
    quota_create(pages[1]);
    weather_create(pages[2]);
    markets_create(pages[3]);
    for (int i = 0; i < 2; i++) {
        lv_obj_t *c = card(pages[4], 0, i * 268, 294, 244);
        system_meter[i] = meter(c, i ? "内存占用" : "CPU 占用", 0, -2, i ? GREEN : BLUE);
        lv_obj_add_flag(system_meter[i].caption, LV_OBJ_FLAG_HIDDEN);
        lv_obj_t *n = card(pages[4], 318 + i * 463, 0, 439, 120);
        tint(label(n, i ? "下载" : "上传", 0, 0, 390, false), i ? NET_DOWN : NET_UP);
        network_rates[i] = label(n, "--", 0, 42, 401, true);
        tint(network_rates[i], i ? NET_DOWN : NET_UP);
    }
    lv_obj_t *graph = card(pages[4], 318, 144, 902, 368);
    muted(graph, "网络速率", 0, 0, 230);
    shape(graph,280,13,24,3,NET_UP,2);tint(label(graph,"上传",314,0,90,false),NET_UP);
    shape(graph,430,13,24,3,NET_DOWN,2);tint(label(graph,"下载",464,0,90,false),NET_DOWN);
    chart_scale = muted(graph, "KB/s", 520, 0, 333);
    lv_obj_set_style_text_align(chart_scale, LV_TEXT_ALIGN_RIGHT, 0);
    chart = lv_chart_create(graph);
    lv_obj_set_pos(chart, 88, 56);
    lv_obj_set_size(chart, 769, 250);
    lv_obj_set_style_pad_all(chart,0,0);
    ui_bg_opa(chart, LV_OPA_TRANSP, 0);
    lv_obj_set_style_border_width(chart, 0, 0);
    lv_obj_set_style_line_color(chart, lv_color_hex(TRACK), 0);
    lv_obj_set_style_line_width(chart, 1, 0);
    lv_obj_set_style_line_opa(chart,LV_OPA_40,0);
    lv_chart_set_div_line_count(chart, 0, 0);
    for(int i=0;i<5;i++) {
        network_ticks[i]=label(graph,"--",0,44+i*249/4,74,false);
        ui_text_font(network_ticks[i],&lv_font_montserrat_24,0);
        lv_obj_set_style_text_align(network_ticks[i],LV_TEXT_ALIGN_RIGHT,0);
        tint(network_ticks[i],MUTED);
    }
    lv_chart_set_type(chart, LV_CHART_TYPE_LINE);
    lv_chart_set_point_count(chart, NETWORK_POINTS);
    lv_obj_set_style_size(chart, 0, 0, LV_PART_INDICATOR);
    lv_obj_set_style_line_width(chart, 3, LV_PART_ITEMS);
    lv_obj_set_style_line_rounded(chart,true,LV_PART_ITEMS);
    lv_obj_set_style_line_opa(chart, LV_OPA_COVER, LV_PART_ITEMS);
    lv_obj_add_event_cb(chart,network_draw,LV_EVENT_DRAW_MAIN_END,NULL);
    upload = lv_chart_add_series(chart, lv_color_hex(NET_UP), LV_CHART_AXIS_PRIMARY_Y);
    download = lv_chart_add_series(chart, lv_color_hex(NET_DOWN), LV_CHART_AXIS_PRIMARY_Y);
    lv_chart_set_all_value(chart, upload, LV_CHART_POINT_NONE);
    lv_chart_set_all_value(chart, download, LV_CHART_POINT_NONE);
    lv_chart_hide_series(chart,upload,true);lv_chart_hide_series(chart,download,true);
    network_span=muted(graph,"1 秒均值 · 等待采样",88,312,490);
    health_create(pages[4]);
    lv_obj_t *cover = music_cover = card(pages[5], 26, 64, 384, 384);
    lv_obj_set_style_pad_all(cover,0,0);
    lv_obj_set_style_radius(cover,0,0);
    lv_obj_set_style_bg_opa(cover,LV_OPA_TRANSP,0);
    lv_obj_set_style_border_width(cover,0,0);
    ui_bg_color(cover, lv_color_hex(SURFACE), 0);
    images[2] = lv_image_create(cover);
    lv_image_set_scale(images[2], 768);
    lv_obj_center(images[2]);
    image_loading[2] = muted(cover, "等待音乐封面", 45, 211, 310);
    lv_obj_set_style_text_align(image_loading[2], LV_TEXT_ALIGN_CENTER, 0);
    music_state = label(pages[5], "等待音乐", 466, 36, 728, false);
    tint(music_state, GREEN);
    static lv_font_t music_title_font;
    music_title_font=lv_font_montserrat_40;
    music_title_font.fallback=&tab5_font_30;
    music = label(pages[5], "在电脑播放音乐", 466, 82, 728, false);
    ui_text_font(music,&music_title_font,0);
    lv_obj_set_style_text_line_space(music,4,0);
    lv_obj_set_height(music,100);
    lv_label_set_long_mode(music, LV_LABEL_LONG_DOT);
    music_alias=muted(pages[5],"",466,190,728);
    lv_obj_set_height(music_alias,32);lv_label_set_long_mode(music_alias,LV_LABEL_LONG_DOT);
    music_artist = label(pages[5], "--", 466, 238, 728, false);
    lv_obj_set_height(music_artist,40);lv_label_set_long_mode(music_artist,LV_LABEL_LONG_DOT);
    music_album = muted(pages[5], "--", 466, 282, 728);
    lv_obj_set_height(music_album,64);
    lv_label_set_long_mode(music_album, LV_LABEL_LONG_DOT);
    music_time = muted(pages[5], "--:--", 466, 354, 728);
    music_duration=muted(pages[5],"--:--",466,354,728);
    lv_obj_set_style_text_align(music_duration,LV_TEXT_ALIGN_RIGHT,0);
    progress = vbar(pages[5],466,400,728,BLUE);
    lv_obj_set_height(progress,8);
    music_empty=shape(pages[5],0,0,1220,516,UI_BACKGROUND,0);
    lv_obj_t *music_empty_icon=shape(music_empty,558,100,104,104,SURFACE,LV_RADIUS_CIRCLE);
    lv_obj_t *music_empty_note=label(music_empty_icon,LV_SYMBOL_AUDIO,0,0,104,true);
    tint(music_empty_note,BLUE);lv_obj_set_style_text_align(music_empty_note,LV_TEXT_ALIGN_CENTER,0);lv_obj_center(music_empty_note);
    lv_obj_t *music_empty_title=label(music_empty,"暂无音乐播放",110,232,1000,false);
    ui_text_font(music_empty_title,&tab5_font_30,0);lv_obj_set_style_text_align(music_empty_title,LV_TEXT_ALIGN_CENTER,0);
    lv_obj_t *music_empty_hint=muted(music_empty,"在电脑播放音乐，歌曲信息会显示在这里",110,292,1000);
    lv_obj_set_style_text_align(music_empty_hint,LV_TEXT_ALIGN_CENTER,0);
    music_empty_refresh();
    computer_music_create(pages[5]);

    for (int i = 0; i < 2; i++) {
        lv_obj_t *c = card(pages[6], i * 622, 0, 598, 512);
        pet_dot[i] = shape(c, 0, 12, 12, 12, MUTED, LV_RADIUS_CIRCLE);
        pet_labels[i] = label(c, i ? "Codex" : "Claude", 28, 4, 516, false);
        lv_obj_t *stage = shape(c, 101, 65, 360, 326, 0x070B13, 22);
        images[i] = lv_image_create(stage);
        lv_image_set_scale(images[i], 640);
        lv_obj_center(images[i]);
        image_loading[i] = muted(stage, "桌宠同步中", 12, 138, 336);
        lv_obj_set_style_text_align(image_loading[i], LV_TEXT_ALIGN_CENTER, 0);
        pet_hint[i] = muted(c, "状态待确认", 6, 423, 550);
        lv_obj_set_style_text_align(pet_hint[i], LV_TEXT_ALIGN_CENTER, 0);
    }
    analog_create(pages[7]);
    clock_big = label(pages[7], "--:--", 566, 114, 470, true);
    lv_obj_set_width(clock_big, LV_SIZE_CONTENT);
    ui_text_font(clock_big,&tab5_digits_72,0);
    lv_obj_set_style_transform_pivot_x(clock_big, 0, 0);
    lv_obj_set_style_transform_pivot_y(clock_big, 0, 0);
    clock_seconds=muted(pages[7],":--",958,175,72);ui_text_font(clock_seconds,&lv_font_montserrat_24,0);
    date_label=label(pages[7],"等待时间",570,256,612,false);
    clock_lunar=muted(pages[7],"",570,315,612);clock_holiday=muted(pages[7],"",570,357,612);
    lv_label_set_long_mode(clock_lunar,LV_LABEL_LONG_DOT);lv_label_set_long_mode(clock_holiday,LV_LABEL_LONG_DOT);
    clock_rule=shape(pages[7],570,230,574,1,TRACK,0);
    clock_touch_target(pages[7]);
    clock_controls_create(pages[7]);clock_layout();
    lv_obj_add_flag(pages[7],LV_OBJ_FLAG_CLICKABLE);
    lv_obj_add_event_cb(pages[7],clock_style_clicked,LV_EVENT_CLICKED,NULL);
    lv_obj_t *tasks_page = pages[8];
    lv_obj_t *task_left = card(tasks_page, 0, 0, 390, 512);
    lv_obj_set_style_pad_all(task_left,24,0);
    label(task_left, "项目", 0, 10, 72, false);
    codex_project_picker=dropdown(task_left,"暂无项目",80,0,262,0,codex_project_changed,0);
    ui_text_font(codex_project_picker,&tab5_font_24,0);
    codex_list=lv_obj_create(task_left);lv_obj_remove_style_all(codex_list);
    lv_obj_set_pos(codex_list,0,76);lv_obj_set_size(codex_list,342,388);
    lv_obj_set_scroll_dir(codex_list,LV_DIR_VER);
    ui_bg_color(codex_list,lv_color_hex(MUTED),LV_PART_SCROLLBAR);
    lv_obj_set_style_width(codex_list,3,LV_PART_SCROLLBAR);
    lv_obj_set_scrollbar_mode(codex_list,LV_SCROLLBAR_MODE_ON);
    codex_empty=muted(codex_list,"该项目暂无会话",10,12,320);
    for(int i=0;i<8;i++) {
        lv_obj_t *row=codex_task_rows[i]=button(codex_list,"",0,i*88,332,codex_selected_changed,i);
        lv_obj_clean(row);lv_obj_set_height(row,80);lv_obj_set_style_pad_all(row,10,0);
        lv_obj_set_style_border_width(row,2,0);lv_obj_set_style_border_opa(row,LV_OPA_TRANSP,0);
        codex_task_titles[i]=label(row,"",0,8,308,false);
        lv_obj_set_height(codex_task_titles[i],60);lv_label_set_long_mode(codex_task_titles[i],LV_LABEL_LONG_DOT);
        visible(row,false);
    }
    lv_obj_t *task_right = card(tasks_page, 414, 0, 806, 512);
    lv_obj_set_style_pad_all(task_right,24,0);
    codex_hint=label(task_right,"输入",0,0,566,false);
    codex_run_badge=label(task_right,"运行中",446,-2,126,false);
    tint(codex_run_badge,0xA6F2BA);lv_obj_set_height(codex_run_badge,36);
    ui_bg_color(codex_run_badge,lv_color_hex(0x23533B),0);ui_bg_opa(codex_run_badge,LV_OPA_COVER,0);
    lv_obj_set_style_radius(codex_run_badge,8,0);lv_obj_set_style_text_align(codex_run_badge,LV_TEXT_ALIGN_CENTER,0);
    lv_obj_set_style_pad_top(codex_run_badge,3,0);visible(codex_run_badge,false);
    lv_obj_set_height(codex_hint,32);lv_label_set_long_mode(codex_hint,LV_LABEL_LONG_DOT);
    codex_voice_button=button(task_right,"",526,332,56,codex_voice_action,0);
    lv_obj_set_height(codex_voice_button,56);lv_obj_set_style_pad_all(codex_voice_button,0,0);
    lv_obj_set_style_radius(codex_voice_button,LV_RADIUS_CIRCLE,0);
    voice_button_glyphs(codex_voice_button,&codex_mic_glyph,&codex_stop_glyph);
    lv_obj_set_ext_click_area(codex_voice_button,8);
    lv_obj_add_event_cb(codex_voice_button,codex_voice_pressed,LV_EVENT_PRESSED,NULL);
    codex_voice_switch=button(task_right,"改用 TAB5",274,332,158,codex_voice_action,3);
    lv_obj_set_height(codex_voice_switch,56);lv_obj_center(lv_obj_get_child(codex_voice_switch,0));
    lv_obj_add_state(codex_voice_switch,LV_STATE_DISABLED);
    codex_voice_cancel=button(task_right,"取消",598,332,160,codex_voice_action,2);
    lv_obj_add_state(codex_voice_cancel,LV_STATE_DISABLED);
    visible(codex_voice_switch,false);visible(codex_voice_cancel,false);
    codex_voice_hint=muted(task_right,"",0,400,570);
    lv_obj_set_height(codex_voice_hint,64);lv_label_set_long_mode(codex_voice_hint,LV_LABEL_LONG_DOT);
    codex_voice_meter=lv_bar_create(task_right);lv_obj_set_pos(codex_voice_meter,600,416);lv_obj_set_size(codex_voice_meter,158,8);
    ui_bg_color(codex_voice_meter,lv_color_hex(TRACK),LV_PART_MAIN);
    ui_bg_color(codex_voice_meter,lv_color_hex(0xA1E5BD),LV_PART_INDICATOR);visible(codex_voice_meter,false);

    codex_input = lv_textarea_create(task_right);
    lv_obj_add_event_cb(codex_input,codex_draft_changed,LV_EVENT_VALUE_CHANGED,NULL);
    lv_obj_set_pos(codex_input, 0, 56); lv_obj_set_size(codex_input, 758, 250);
    ui_bg_color(codex_input,lv_color_hex(0x354247),0);
    ui_text_color(codex_input,lv_color_hex(INK),0);
    ui_border_color(codex_input,lv_color_hex(TRACK),0);
    lv_obj_set_style_border_width(codex_input,1,0);
    ui_border_color(codex_input,lv_color_hex(BLUE),LV_STATE_FOCUSED);
    lv_obj_set_style_radius(codex_input,12,0);
    lv_obj_set_style_pad_all(codex_input,16,0);
    ui_text_font(codex_input,&tab5_font_30,0);
    lv_textarea_set_placeholder_text(codex_input, "输入内容…");
    lv_textarea_set_max_length(codex_input, 2000);
    lv_obj_add_event_cb(codex_input, codex_input_focus, LV_EVENT_FOCUSED, NULL);
    lv_obj_add_event_cb(codex_input, codex_input_focus, LV_EVENT_CLICKED, NULL);
    codex_send_button = button(task_right, "发送", 598, 332, 160, codex_send, 0);
    lv_obj_set_height(codex_send_button,56);lv_obj_center(lv_obj_get_child(codex_send_button,0));
    codex_receipt = label(task_right, "", 0, 400, 570, false);
    lv_obj_set_height(codex_receipt,64);lv_label_set_long_mode(codex_receipt,LV_LABEL_LONG_DOT);
    codex_reply_button=button(task_right,"查看回复",598,-8,160,codex_reply_open,0);
    lv_obj_set_height(codex_reply_button,48);lv_obj_center(lv_obj_get_child(codex_reply_button,0));
    ui_bg_opa(codex_reply_button,LV_OPA_TRANSP,0);
    lv_obj_set_style_border_width(codex_reply_button,1,0);
    ui_border_color(codex_reply_button,lv_color_hex(TRACK),0);
    visible(codex_reply_button,false);
    codex_physical=muted(task_right,"",0,344,240);
    ui_bg_color(codex_send_button,lv_color_hex(BLUE),0);
    ui_text_color(codex_send_button,lv_color_hex(0x173642),0);
    codex_keyboard = lv_keyboard_create(tasks_page);
    lv_obj_set_align(codex_keyboard,LV_ALIGN_TOP_LEFT);
    lv_obj_set_pos(codex_keyboard, 0, 250); lv_obj_set_size(codex_keyboard, 1220, 266);
    ui_bg_color(codex_keyboard,lv_color_hex(UI_BACKGROUND),0);
    lv_obj_set_style_border_width(codex_keyboard,0,0);
    lv_obj_set_style_pad_all(codex_keyboard,8,0);
    lv_obj_set_style_pad_row(codex_keyboard,8,0);
    lv_obj_set_style_pad_column(codex_keyboard,8,0);
    ui_bg_color(codex_keyboard,lv_color_hex(0x354247),LV_PART_ITEMS);
    ui_text_color(codex_keyboard,lv_color_hex(INK),LV_PART_ITEMS);
    lv_obj_set_style_radius(codex_keyboard,8,LV_PART_ITEMS);
    lv_obj_set_style_border_width(codex_keyboard,0,LV_PART_ITEMS);
    ui_bg_color(codex_keyboard,lv_color_hex(0x40525A),LV_PART_ITEMS|LV_STATE_CHECKED);
    ui_text_color(codex_keyboard,lv_color_hex(INK),LV_PART_ITEMS|LV_STATE_CHECKED);
    ui_bg_color(codex_keyboard,lv_color_hex(BLUE),LV_PART_ITEMS|LV_STATE_PRESSED);
    ui_text_color(codex_keyboard,lv_color_hex(0x173642),LV_PART_ITEMS|LV_STATE_PRESSED);
    lv_obj_add_event_cb(codex_keyboard, codex_keyboard_done, LV_EVENT_READY, NULL);
    lv_obj_add_event_cb(codex_keyboard, codex_keyboard_done, LV_EVENT_CANCEL, NULL);
    codex_ime_panel=tab5_ime_create(tasks_page,codex_keyboard,codex_input,&tab5_font_24);
    lv_obj_add_flag(codex_keyboard, LV_OBJ_FLAG_HIDDEN);
    codex_reply_overlay=card(tasks_page,0,0,1220,512);
    ui_bg_color(codex_reply_overlay,lv_color_hex(UI_BACKGROUND),0);
    int reply_header_y=(48-lv_font_get_line_height(&tab5_font_24))/2;
    label(codex_reply_overlay,"会话动态",0,reply_header_y,180,false);
    button(codex_reply_overlay,"返回",1030,0,150,codex_reply_close,0);
    codex_reply_latest=button(codex_reply_overlay,"回到最新",1004,420,176,codex_reply_navigate,0);
    codex_turn_older=button(codex_reply_overlay,"上一轮",0,420,140,codex_turn_navigate,-1);
    codex_turn_newer=button(codex_reply_overlay,"下一轮",156,420,140,codex_turn_navigate,1);
    codex_reply_older=button(codex_reply_overlay,"上一页",600,420,130,codex_reply_navigate,-1);
    codex_reply_newer=button(codex_reply_overlay,"下一页",866,420,130,codex_reply_navigate,1);
    codex_reply_status=muted(codex_reply_overlay,"",194,reply_header_y,626);
    codex_interrupt_button=button(codex_reply_overlay,"停止任务",844,0,160,codex_interrupt,0);
    ui_bg_opa(codex_interrupt_button,LV_OPA_TRANSP,0);tint(lv_obj_get_child(codex_interrupt_button,0),AMBER);
    // Background reads briefly disable actions; keep the header visually stable.
    ui_bg_opa(codex_interrupt_button,LV_OPA_TRANSP,LV_STATE_DISABLED);
    visible(codex_interrupt_button,false);
    codex_reply_position=muted(codex_reply_overlay,"",752,431,100);
    lv_label_set_long_mode(codex_reply_position,LV_LABEL_LONG_CLIP);
    lv_obj_set_width(codex_reply_position,220);
    lv_obj_t *reply_buttons[]={codex_reply_latest,codex_reply_older,codex_reply_newer,codex_turn_older,codex_turn_newer};
    for(int i=0;i<5;i++) {
        lv_obj_set_height(reply_buttons[i],48);
        ui_bg_opa(reply_buttons[i],LV_OPA_TRANSP,0);
        lv_obj_set_style_border_width(reply_buttons[i],1,0);
        ui_border_color(reply_buttons[i],lv_color_hex(TRACK),0);
        lv_obj_center(lv_obj_get_child(reply_buttons[i],0));
    }
    lv_label_set_long_mode(codex_reply_status,LV_LABEL_LONG_DOT);
    lv_obj_t *reply_scroll=lv_obj_create(codex_reply_overlay);codex_reply_scroll=reply_scroll;
    lv_obj_set_pos(reply_scroll,0,66);lv_obj_set_size(reply_scroll,1180,336);
    ui_bg_color(reply_scroll,lv_color_hex(0x2B3539),0);
    lv_obj_set_style_border_width(reply_scroll,0,0);
    lv_obj_set_style_radius(reply_scroll,12,0);
    lv_obj_set_style_pad_all(reply_scroll,16,0);
    lv_obj_set_scroll_dir(reply_scroll,LV_DIR_VER);
    lv_obj_set_scrollbar_mode(reply_scroll,LV_SCROLLBAR_MODE_AUTO);
    codex_reply_text=label(reply_scroll,"",0,0,1120,false);
    visible(codex_reply_text,false);
    codex_reply_document=lv_obj_create(reply_scroll);
    lv_obj_remove_style_all(codex_reply_document);
    lv_obj_set_width(codex_reply_document,1120);lv_obj_set_height(codex_reply_document,LV_SIZE_CONTENT);
    lv_obj_set_flex_flow(codex_reply_document,LV_FLEX_FLOW_COLUMN);
    lv_obj_set_style_pad_row(codex_reply_document,8,0);
    lv_obj_remove_flag(codex_reply_document,LV_OBJ_FLAG_SCROLLABLE);
    visible(codex_reply_overlay,false);
    const char *setting_names[]={"网络连接","屏幕声音","内容轮播","固件升级","设备信息","存储管理"};
    const char *setting_icons[]={LV_SYMBOL_WIFI,LV_SYMBOL_EYE_OPEN,LV_SYMBOL_LIST,LV_SYMBOL_DOWNLOAD,LV_SYMBOL_SETTINGS,LV_SYMBOL_DRIVE};
    const int setting_slots[]={0,1,2,4,5,3};
    for(int i=0;i<SETTINGS_CATEGORIES;i++) {
        int slot=setting_slots[i];
        lv_obj_t *b=button(pages[9],setting_names[i],0,16+slot*76,220,settings_category_changed,i);
        settings_categories[i]=b;lv_obj_set_height(b,64);lv_obj_set_style_pad_all(b,16,0);
        lv_obj_t *caption=lv_obj_get_child(b,0);ui_text_font(caption,&tab5_font_30,0);
        lv_obj_align(caption,LV_ALIGN_LEFT_MID,42,0);
        lv_obj_t *symbol=lv_label_create(b);lv_label_set_text(symbol,setting_icons[i]);
        ui_text_font(symbol,&lv_font_montserrat_24,0);lv_obj_align(symbol,LV_ALIGN_LEFT_MID,0,0);
        if(i==3){ota_badge=notification_upgrade_arrow(b);visible(ota_badge,false);}
    }
    save_status=muted(pages[9],"",12,474,208);
    lv_obj_set_height(save_status,32);
    visible(save_status,false);
    lv_obj_t *settings_frame=card(pages[9],244,0,976,516);
    lv_obj_set_style_pad_all(settings_frame,0,0);
    // Keep the rounded frame stationary. Only the rectangular inner viewport
    // scrolls: its cached content can fully cover a dirty strip, with no parent
    // fills or corner blending on each gesture frame.
    settings_scroll=lv_obj_create(settings_frame);lv_obj_remove_style_all(settings_scroll);
    // Center existing columns; even RGB565 bounds keep dirty strips opaque.
    lv_obj_set_pos(settings_scroll,40,24);lv_obj_set_size(settings_scroll,898,468);
    ui_bg_color(settings_scroll,lv_color_hex(SURFACE),0);ui_bg_opa(settings_scroll,LV_OPA_COVER,0);
    ui_bg_opa(settings_scroll,LV_OPA_60,LV_PART_SCROLLBAR);
    ui_bg_color(settings_scroll,lv_color_hex(MUTED),LV_PART_SCROLLBAR);
    lv_obj_set_style_width(settings_scroll,4,LV_PART_SCROLLBAR);
    lv_obj_add_flag(settings_scroll,LV_OBJ_FLAG_SCROLLABLE);
    lv_obj_remove_flag(settings_scroll,LV_OBJ_FLAG_SCROLL_CHAIN);
    lv_obj_set_scroll_dir(settings_scroll,LV_DIR_VER);
    lv_obj_set_scrollbar_mode(settings_scroll,LV_SCROLLBAR_MODE_AUTO);
    // Side insets keep the content clear of the 22 px corners.
    // Avoid an intermediate ARGB layer during every scroll frame.
    for(int i=0;i<SETTINGS_CATEGORIES;i++) {
        lv_obj_t *p=lv_obj_create(settings_scroll);settings_panels[i]=p;
        lv_obj_remove_style_all(p);lv_obj_set_size(p,898,i==2?1200:i==1?550:468);
        ui_bg_color(p,lv_color_hex(SURFACE),0);ui_bg_opa(p,LV_OPA_COVER,0);
        lv_obj_remove_flag(p,LV_OBJ_FLAG_SCROLLABLE);
        lv_obj_t *title=label(p,setting_names[i],0,0,898,false);ui_text_font(title,&tab5_font_30,0);
    }
    device_config config=device_get_config();
    lv_obj_t *connect=settings_panels[0],*screen_options=settings_panels[1],*content=settings_panels[2],*update=settings_panels[3];
    // Status, preference and diagnostics have separate rows. Values use real
    // columns instead of space-padded text, and actions share a right edge.
    const char *connection_names[]={"设备配对","USB","Wi-Fi","蓝牙"};
    for(int i=0;i<4;i++) {
        muted(connect,connection_names[i],i*224,72,192);
        lv_obj_t *status_row=lv_obj_create(connect);lv_obj_remove_style_all(status_row);
        lv_obj_set_pos(status_row,i*224,108);lv_obj_set_size(status_row,192,40);
        lv_obj_remove_flag(status_row,LV_OBJ_FLAG_SCROLLABLE|LV_OBJ_FLAG_CLICKABLE);
        lv_obj_set_flex_flow(status_row,LV_FLEX_FLOW_ROW);
        lv_obj_set_flex_align(status_row,LV_FLEX_ALIGN_START,LV_FLEX_ALIGN_CENTER,LV_FLEX_ALIGN_CENTER);
        lv_obj_set_style_pad_column(status_row,10,0);
        // Text shares the heading's left edge. A trailing check participates in
        // the same row layout instead of shifting only the status text right.
        setup_values[i]=label(status_row,"正在初始化",0,0,LV_SIZE_CONTENT,false);
        setup_checks[i]=label(status_row,LV_SYMBOL_OK,0,0,24,false);
        ui_text_font(setup_checks[i],&lv_font_montserrat_24,0);tint(setup_checks[i],GREEN);
        visible(setup_checks[i],false);
        if(i<3)shape(connect,i*224+208,76,1,68,0x364349,0);
    }
    shape(connect,0,176,898,1,0x435056,0);
    label(connect,"连接方式",0,217,282,false);
    dropdown(connect,"自动选择\n仅 USB\n仅 Wi-Fi\n仅蓝牙",598,206,284,config.mode+1,mode_changed,0);
    muted(connect,"自动选择时优先使用 USB，其次 Wi-Fi、蓝牙",0,266,882);
    shape(connect,0,320,898,1,0x435056,0);
    label(connect,"电脑与网络",0,357,500,false);
    lv_obj_t *diagnostics=button(connect,"管理连接",598,344,284,network_action,0);
    lv_obj_set_height(diagnostics,56);lv_obj_center(lv_obj_get_child(diagnostics,0));
    muted(connect,"切换已配对电脑、管理 Wi-Fi 与查看连接状态",0,416,882);
    // Two equal columns: all headings, field edges and row baselines align.
    muted(screen_options,"屏幕亮度",0,68,286);
    brightness_value=label(screen_options,"",310,68,120,false);
    lv_obj_set_style_text_align(brightness_value,LV_TEXT_ALIGN_RIGHT,0);
    ui_text_fmt(brightness_value,"%d%%",config.brightness);
    lv_obj_t *brightness=remote_brightness=lv_slider_create(screen_options);
    lv_obj_set_pos(brightness,10,116);lv_obj_set_size(brightness,410,16);
    lv_obj_set_ext_click_area(brightness,20);
    ui_bg_color(brightness,lv_color_hex(TRACK),LV_PART_MAIN);
    ui_bg_color(brightness,lv_color_hex(BLUE),LV_PART_INDICATOR);
    ui_bg_color(brightness,lv_color_hex(BLUE),LV_PART_KNOB);
    lv_slider_set_range(brightness,10,100);lv_slider_set_value(brightness,config.brightness,LV_ANIM_OFF);
    lv_obj_add_event_cb(brightness,brightness_changed,LV_EVENT_RELEASED,NULL);
    lv_obj_add_event_cb(brightness,brightness_changed,LV_EVENT_VALUE_CHANGED,NULL);
    muted(screen_options,"屏幕方向",0,164,430);
    orientation_selector=dropdown(screen_options,"自动翻转\n锁定当前方向",0,202,430,screen_orientation_mode?1:0,screen_orientation_changed,0);
    muted(screen_options,"屏保等待",0,278,430);
    int saver_index=prefs.saver_minutes==0?0:prefs.saver_minutes==1?1:prefs.saver_minutes==5?2:
                    prefs.saver_minutes==10?3:prefs.saver_minutes==30?4:5;
    remote_saver=dropdown(screen_options,"关闭\n1 分钟\n5 分钟\n10 分钟\n30 分钟\n60 分钟",0,316,430,saver_index,pref_changed,3);
    saver_style_status=muted(screen_options,"屏保类型",452,278,430);
    saver_selector=dropdown(screen_options,"经典时钟\n年度点阵\n年度点阵 · 暖灰\n十二月历"
#if TAB5_LOCAL_ART
        "\n每日名画\n每日书法"
#endif
        "\n日期倒计时"
        ,452,316,270,ui_saver_option_from_style(saver_style),saver_style_changed,0);
    saver_preview_button=button(screen_options,"预览屏保",738,316,144,saver_preview_clicked,0);
    lv_obj_set_height(saver_preview_button,48);lv_obj_center(lv_obj_get_child(saver_preview_button,0));
    muted(screen_options,"提示音量",452,68,286);
    sound_value=label(screen_options,"",762,68,120,false);
    lv_obj_set_style_text_align(sound_value,LV_TEXT_ALIGN_RIGHT,0);
    sound_slider=lv_slider_create(screen_options);
    lv_obj_set_pos(sound_slider,462,116);lv_obj_set_size(sound_slider,410,16);
    ui_bg_color(sound_slider,lv_color_hex(TRACK),LV_PART_MAIN);
    ui_bg_color(sound_slider,lv_color_hex(BLUE),LV_PART_INDICATOR);
    ui_bg_color(sound_slider,lv_color_hex(BLUE),LV_PART_KNOB);
    lv_obj_set_ext_click_area(sound_slider,20);
    lv_slider_set_range(sound_slider,0,100);
    lv_obj_add_event_cb(sound_slider,sound_changed,LV_EVENT_VALUE_CHANGED,NULL);
    lv_obj_add_event_cb(sound_slider,sound_changed,LV_EVENT_RELEASED,NULL);
    muted(screen_options,"声音控制",452,164,430);
    sound_mute=check(screen_options,"提示静音",678,210,false,sound_changed,0);
    lv_obj_align(sound_mute,LV_ALIGN_TOP_RIGHT,-16,210);
    sound_test=button(screen_options,"试听",452,202,204,sound_preview,0);
    lv_obj_set_height(sound_test,48);lv_obj_center(lv_obj_get_child(sound_test,0));
    screen_sound_group=shape(screen_options,0,386,898,70,SURFACE,0);
    shape(screen_sound_group,0,0,898,1,0x435056,0);
    remote_alerts=check(screen_sound_group,"任务状态提醒",0,28,prefs.alerts,pref_changed,1);
    sound_hint=muted(screen_sound_group,"修改后自动保存",470,32,412);
    lv_obj_set_style_text_align(sound_hint,LV_TEXT_ALIGN_RIGHT,0);
    sound_refresh();
    date_settings_row=shape(screen_options,0,382,898,72,SURFACE,0);
    muted(date_settings_row,"日期倒计时",0,14,286);
    button(date_settings_row,"设置名称和日期",310,0,572,date_editor_show,0);
    lv_obj_t *storage=settings_panels[SETTINGS_STORAGE];
    tf_storage_row=shape(storage,0,68,898,64,SURFACE,0);
    muted(tf_storage_row,"TF 卡",0,14,286);
    tf_card_value=label(tf_storage_row,"正在检测 TF 卡",310,14,572,false);
    shape(storage,0,142,898,1,0x435056,0);
    muted(storage,"缓存位置",0,177,286);
    tf_cache_selector=dropdown(storage,"设备缓存\nTF 卡",310,166,572,tf_cache_enabled?1:0,tf_cache_changed,0);
    tf_cache_feedback=muted(storage,"",310,230,572);
    muted(storage,"设备缓存：按需获取，重启后重新同步。",0,284,898);
    muted(storage,"TF 卡：保存整套图库，完成后可离线查看。",0,326,898);
#if TAB5_LOCAL_ART
    shape(storage,0,378,898,1,0x435056,0);
    muted(storage,"图库保存",0,408,230);
    tf_storage_value=muted(storage,"使用设备缓存",250,408,648);
#endif
    tf_storage_refresh();
    date_settings_refresh();
    const char *display_labels[]={"内容轮播","固定页面","自动切换"};
    for(int i=0;i<3;i++) {
        display_choices[i]=button(content,display_labels[i],i*300,64,282,display_mode_changed,i);
        lv_obj_set_height(display_choices[i],60);lv_obj_set_style_border_width(display_choices[i],1,0);
        lv_obj_center(lv_obj_get_child(display_choices[i],0));
    }
    display_mode_hint=muted(content,"",0,138,898);
    display_fixed_options=shape(content,0,192,898,56,SURFACE,0);
    muted(display_fixed_options,"显示页面",0,12,260);
    display_fixed_choice=prefs.selected>=0?prefs.selected:0;
    display_fixed_page=dropdown(display_fixed_options,"总览\n模型额度\n天气\n行情\n电脑状态\n音乐\n桌宠\n时钟",598,0,284,
                                display_fixed_choice,display_fixed_changed,0);
    display_cycle_options=shape(content,0,192,898,650,SURFACE,0);
    muted(display_cycle_options,"切换间隔",0,12,260);
    int interval=prefs.interval==10?0:prefs.interval==15?1:prefs.interval==30?2:3;
    remote_interval=dropdown(display_cycle_options,"10 秒\n15 秒\n30 秒\n60 秒",598,0,284,interval,pref_changed,2);
    shape(display_cycle_options,0,68,898,1,0x435056,0);
    label(display_cycle_options,"轮播页面与顺序",0,84,898,false);
    for(int i=0;i<8;i++) {
        int y=132+i*64;
        cycle_checks[i]=check(display_cycle_options,"",0,y+8,prefs.enabled[prefs.order[i]],pref_changed,20+i);
        lv_obj_set_size(cycle_checks[i],54,48);
        order_rows[i]=label(display_cycle_options,"",64,y+8,628,false);
        ui_text_fmt(order_rows[i],"%d. %s",i+1,names[prefs.order[i]]);
        if(i)button(display_cycle_options,"上移",762,y,136,order_changed,i);
        if(i<7)shape(display_cycle_options,64,y+56,834,1,0x39464B,0);
    }
    lv_obj_t *home_options=lv_obj_create(content);display_home_options=home_options;lv_obj_remove_style_all(home_options);
    lv_obj_set_size(home_options,898,344);lv_obj_remove_flag(home_options,LV_OBJ_FLAG_SCROLLABLE);
    label(home_options,"总览显示的模型",0,0,898,false);
    home_auto_check=check(home_options,"自动选择",0,52,!home_models,home_model_changed,-1);
    muted(home_options,"最多三个 · 按已有额度自动选择",300,56,598);
    for(int i=0;i<UI_HOME_PROVIDERS;i++) {
        int id=home_provider_order[i];
        home_model_checks[id]=check(home_options,home_provider_names[id],(i%3)*300,112+(i/3)*56,
            (home_models&(1u<<id))!=0,home_model_changed,id);
        lv_obj_t *option=home_model_checks[id];
        lv_obj_set_size(option,288,48);lv_obj_set_style_pad_column(option,64,0);
        lv_obj_t *logo=lv_image_create(option);lv_image_set_src(logo,id==1?&brand_openai_option_image:home_provider_logos[id]);
        int marker_height=lv_font_get_line_height(lv_obj_get_style_text_font(option,LV_PART_MAIN))+
            lv_obj_get_style_pad_top(option,LV_PART_INDICATOR)+lv_obj_get_style_pad_bottom(option,LV_PART_INDICATOR);
        // LVGL draws the checkbox and its text at the top of the hit area,
        // rather than vertically centering them in the explicit 48 px height.
        lv_obj_align(logo,LV_ALIGN_TOP_LEFT,52,(marker_height-36)/2);
        // The image belongs to the checkbox hit area, so tapping either the
        // brand mark or its name follows the same max-three/save/rollback path.
        lv_obj_remove_flag(logo,LV_OBJ_FLAG_CLICKABLE|LV_OBJ_FLAG_SCROLLABLE);
    }
    display_controls_refresh();
    muted(update,"当前版本",564,7,150);
#ifdef TAB5_UI_PREVIEW
    const char *installed_version=getenv("TAB5_PREVIEW_INSTALLED_VERSION");
    if(!installed_version||!installed_version[0])installed_version="0.2.21-ui";
#else
    const char *installed_version=esp_app_get_description()->version;
#endif
    lv_obj_t *installed=label(update,installed_version,720,7,178,false);tint(installed,MUTED);
    ota_offer_card=card(update,0,56,898,332);
    lv_obj_set_style_border_width(ota_offer_card,2,0);
    ota_offer_title=label(ota_offer_card,"暂无新固件",0,0,850,false);ui_text_font(ota_offer_title,&tab5_font_30,0);
    ota_empty_icon=shape(ota_offer_card,389,38,72,72,TRACK,LV_RADIUS_CIRCLE);
    lv_obj_t *empty_symbol=label(ota_empty_icon,LV_SYMBOL_DOWNLOAD,0,0,LV_SIZE_CONTENT,false);
    ui_text_font(empty_symbol,&lv_font_montserrat_24,0);tint(empty_symbol,MUTED);lv_obj_center(empty_symbol);
    ota_empty_hint=muted(ota_offer_card,"在电脑端选择固件后，版本与更新说明会显示在这里。",0,194,850);
    lv_obj_set_style_text_align(ota_empty_hint,LV_TEXT_ALIGN_CENTER,0);
    ota_hint=muted(update,"等待电脑提供固件",280,412,618);lv_obj_set_height(ota_hint,56);
    ota_notes_box=lv_obj_create(ota_offer_card);lv_obj_set_pos(ota_notes_box,0,56);lv_obj_set_size(ota_notes_box,850,232);
    lv_obj_set_style_bg_opa(ota_notes_box,LV_OPA_TRANSP,0);lv_obj_set_style_border_width(ota_notes_box,0,0);
    lv_obj_set_style_pad_all(ota_notes_box,0,0);lv_obj_set_scroll_dir(ota_notes_box,LV_DIR_VER);
    lv_obj_set_scrollbar_mode(ota_notes_box,LV_SCROLLBAR_MODE_AUTO);
    ota_notes=lv_obj_create(ota_notes_box);lv_obj_remove_style_all(ota_notes);
    lv_obj_set_width(ota_notes,824);lv_obj_set_height(ota_notes,LV_SIZE_CONTENT);
    lv_obj_set_flex_flow(ota_notes,LV_FLEX_FLOW_COLUMN);lv_obj_set_style_pad_row(ota_notes,8,0);
    lv_obj_remove_flag(ota_notes,LV_OBJ_FLAG_SCROLLABLE);
    ota_button=button(update,"立即升级",0,400,254,ota_action,0);lv_obj_set_height(ota_button,64);
    flash_entry=button(update,"存储测速",280,400,180,flash_action,0);lv_obj_set_height(flash_entry,64);
    // Retain the internal diagnostic workflow, without a normal upgrade-page entry.
    visible(flash_entry,false);
    lv_obj_add_state(ota_button,LV_STATE_DISABLED);
    ota_offer_layout(false);
    // Hardware capabilities are product specifications, not enabled application features.
    // Source: https://docs.m5stack.com/en/core/Tab5 (C145/K145).
    lv_obj_t *info=settings_panels[4];
    lv_obj_set_height(info,1064);
    label(info,"M5Stack TAB5",0,56,600,false);
    muted(info,"硬件参数 · M5Stack 官方规格",0,100,880);
    const char *info_names[]={"当前固件","设备标识","数据通道","主控","无线模块","存储","屏幕","摄像头","音频","USB","扩展","传感器与时钟","供电"};
    device_config info_config=device_get_config();
    const char *info_values[]={installed_version,info_config.id,"未连接电脑","ESP32-P4 · RISC-V 双核","ESP32-C6-MINI-1U · 2.4 GHz Wi-Fi 6",
        "16 MB Flash · 32 MB Octal PSRAM","5 英寸 IPS 触屏 · 1280 × 720 · MIPI-DSI",
        "SC2356 · 200 万像素 · 1600 × 1200 · MIPI-CSI","ES8388 / ES7210 · 双麦克风 · 1 W 扬声器 · 3.5 mm",
        "Type-A Host · Type-C USB 2.0 OTG","microSD · RS485 · HY2.0-4P · M5-Bus · GPIO_EXT",
        "BMI270 六轴 · RX8130CE RTC","USB-C / 外部供电 · 支持 NP-F550 可拆卸电池"};
    for(unsigned i=0;i<sizeof(info_names)/sizeof(info_names[0]);i++) {
        int y=156+(int)i*64;
        muted(info,info_names[i],0,y,168);
        lv_obj_t *value=label(info,info_values[i],182,y,700,false);
        if(i==2)device_info_link=value;
        shape(info,0,y+48,880,1,0x364349,0);
    }
    muted(info,"硬件规格不代表功能已启用；电池容量依实际安装电池。",0,1008,882);
    settings_select(0);
    // The global shortcut shares the footer; full-screen workflows cover it.
    quick_create(screen);
    ota_layer=card(screen,0,0,1280,720);
    ota_title=label(ota_layer,"正在更新 TAB5",170,180,900,false);
    ui_text_font(ota_title,&tab5_font_30,0);
    ota_progress=label(ota_layer,"",170,346,900,false);
    ota_note=muted(ota_layer,"请保持供电；完成后会自动重启。",170,448,900);
    ota_success_icon=shape(ota_layer,568,148,104,104,0x334B41,LV_RADIUS_CIRCLE);
    ui_border_color(ota_success_icon,lv_color_hex(GREEN),0);
    lv_obj_set_style_border_width(ota_success_icon,2,0);
    lv_obj_t *done_check=label(ota_success_icon,LV_SYMBOL_OK,0,0,60,true);
    lv_obj_set_style_text_align(done_check,LV_TEXT_ALIGN_CENTER,0);tint(done_check,GREEN);
    lv_obj_center(done_check);
    ota_version_track=lv_bar_create(ota_layer);
    lv_obj_set_pos(ota_version_track,370,254);lv_obj_set_size(ota_version_track,500,64);
    ui_bg_color(ota_version_track,lv_color_hex(0x354247),0);
    ui_bg_opa(ota_version_track,LV_OPA_COVER,0);
    lv_obj_set_style_radius(ota_version_track,16,0);
    lv_obj_set_style_radius(ota_version_track,16,LV_PART_INDICATOR);
    ui_bg_color(ota_version_track,lv_color_hex(0x426672),LV_PART_INDICATOR);
    ui_bg_opa(ota_version_track,LV_OPA_COVER,LV_PART_INDICATOR);
    lv_obj_set_style_pad_all(ota_version_track,0,0);
    lv_obj_set_style_border_width(ota_version_track,0,0);
    ota_version=label(ota_version_track,"",0,0,452,false);tint(ota_version,INK);
    lv_obj_set_style_text_align(ota_version,LV_TEXT_ALIGN_CENTER,0);lv_obj_center(ota_version);
    visible(ota_success_icon,false);visible(ota_version_track,false);
    lv_obj_set_style_text_align(ota_title,LV_TEXT_ALIGN_CENTER,0);
    lv_obj_set_style_text_align(ota_progress,LV_TEXT_ALIGN_CENTER,0);
    lv_obj_set_style_text_align(ota_note,LV_TEXT_ALIGN_CENTER,0);
    lv_display_add_event_cb(display,ota_completion_drawn,LV_EVENT_REFR_READY,NULL);
    visible(ota_layer,false);
    network_window=card(screen,30,70,1220,650);
    label(network_window,"电脑与网络",0,0,600,false);
    button(network_window,"返回",1030,0,150,network_action,2);
    muted(network_window,"切换已配对电脑，管理保存的 Wi-Fi。",0,42,960);
    lv_obj_t *computer_group=shape(network_window,0,84,578,342,0x303B40,12);
    lv_obj_t *wifi_group=shape(network_window,602,84,578,342,0x303B40,12);
    label(computer_group,"连接电脑",20,14,538,false);
    host_card=shape(computer_group,20,54,538,88,0x27343A,10);
    host_current=label(host_card,"尚未绑定电脑",80,8,438,false);ui_text_font(host_current,&tab5_font_30,0);
    lv_obj_set_height(host_current,39);lv_label_set_long_mode(host_current,LV_LABEL_LONG_DOT);
    host_state=label(host_card,"正在检查…",80,47,438,false);lv_obj_set_height(host_state,31);lv_label_set_long_mode(host_state,LV_LABEL_LONG_DOT);
    host_monitor=shape(host_card,18,19,40,30,0x27343A,5);ui_bg_opa(host_monitor,LV_OPA_TRANSP,0);lv_obj_set_style_border_width(host_monitor,2,0);
    shape(host_card,35,49,6,8,MUTED,1);shape(host_card,26,57,24,2,MUTED,1);
    host_badge=shape(host_card,43,42,26,26,MUTED,LV_RADIUS_CIRCLE);
    host_symbol=label(host_badge,LV_SYMBOL_CLOSE,0,0,24,false);ui_text_font(host_symbol,&lv_font_montserrat_24,0);lv_obj_set_style_text_align(host_symbol,LV_TEXT_ALIGN_CENTER,0);lv_obj_center(host_symbol);
    muted(computer_group,"已配对电脑",20,154,538);
    host_picker=dropdown(computer_group,"请先通过 USB 配对电脑",20,190,538,0,NULL,0);lv_obj_set_height(host_picker,54);
    host_rename=button(computer_group,"修改备注",20,266,152,network_action,8);lv_obj_set_height(host_rename,54);
    host_switch=button(computer_group,"切换电脑",338,266,220,network_action,7);lv_obj_set_height(host_switch,54);
    ui_bg_color(host_switch,lv_color_hex(BLUE),0);tint(lv_obj_get_child(host_switch,0),0x173642);
    label(wifi_group,"Wi-Fi 网络",20,14,538,false);
    network_wifi_card=shape(wifi_group,20,54,538,88,0x27343A,10);
    lv_obj_t *wifi_symbol=label(network_wifi_card,LV_SYMBOL_WIFI,22,24,42,false);ui_text_font(wifi_symbol,&lv_font_montserrat_24,0);tint(wifi_symbol,MUTED);
    network_wifi_state=label(network_wifi_card,"正在检查…",80,8,438,false);ui_text_font(network_wifi_state,&tab5_font_30,0);lv_obj_set_height(network_wifi_state,39);
    network_wifi_name=label(network_wifi_card,"",80,47,438,false);lv_obj_set_height(network_wifi_name,31);lv_label_set_long_mode(network_wifi_name,LV_LABEL_LONG_DOT);
    muted(wifi_group,"已保存网络",20,154,538);
    network_picker=dropdown(wifi_group,"正在读取",20,190,538,0,NULL,0);lv_obj_set_height(network_picker,54);
    network_connect=button(wifi_group,"连接网络",338,266,220,network_action,3);lv_obj_set_height(network_connect,54);
    ui_bg_color(network_connect,lv_color_hex(BLUE),0);tint(lv_obj_get_child(network_connect,0),0x173642);
    network_forget=button(wifi_group,"删除网络",20,266,152,network_action,4);lv_obj_set_height(network_forget,54);
    button(network_window,"连接诊断",0,450,220,network_action,9);
    muted(network_window,"查看 Wi-Fi 连接与电脑桥接通信状态",244,464,936);
    network_feedback=muted(network_window,"",0,574,1180);
    network_confirm=shape(network_window,-UI_CARD_PADDING,-UI_CARD_PADDING,1220,650,0x000000,0);ui_bg_opa(network_confirm,LV_OPA_60,0);lv_obj_add_flag(network_confirm,LV_OBJ_FLAG_CLICKABLE);
    lv_obj_t *network_confirm_card=card(network_confirm,260,120,700,320);
    label(network_confirm_card,"删除网络",0,0,660,false);
    network_confirm_text=label(network_confirm_card,"",0,50,660,false);lv_obj_set_height(network_confirm_text,144);lv_label_set_long_mode(network_confirm_text,LV_LABEL_LONG_WRAP);
    network_confirm_no=button(network_confirm_card,"取消",308,214,168,network_action,5);lv_obj_set_height(network_confirm_no,64);
    network_confirm_yes=button(network_confirm_card,"删除",492,214,168,network_action,6);lv_obj_set_height(network_confirm_yes,64);ui_bg_color(network_confirm_yes,lv_color_hex(RED),0);tint(lv_obj_get_child(network_confirm_yes,0),0x34242A);
    visible(network_confirm,false);visible(network_window,false);
    network_diag_window=card(screen,30,70,1220,650);
    label(network_diag_window,"连接诊断",0,0,600,false);
    button(network_diag_window,"返回",1030,0,150,network_action,10);
    muted(network_diag_window,"仅检查 Wi-Fi 连接与电脑桥接通信",0,52,980);
    lv_obj_t *diag_wifi=shape(network_diag_window,0,104,578,340,0x303B40,12);
    lv_obj_t *diag_bridge=shape(network_diag_window,602,104,578,340,0x303B40,12);
    label(diag_wifi,"Wi-Fi 网络",20,18,538,false);
    label(diag_bridge,"电脑桥接",20,18,538,false);
    network_diag_state=label(diag_wifi,"正在检查…",20,68,538,false);
    network_details=label(diag_bridge,"正在检查…",20,68,538,false);
    lv_obj_t *diagnostic_status[]={network_diag_state,network_details};
    for(unsigned i=0;i<2;i++) {
        ui_text_font(diagnostic_status[i],&tab5_font_30,0);lv_obj_set_height(diagnostic_status[i],80);
        lv_label_set_long_mode(diagnostic_status[i],LV_LABEL_LONG_WRAP);
    }
    shape(diag_wifi,20,162,538,1,TRACK,0);shape(diag_bridge,20,162,538,1,TRACK,0);
    network_diag_name=label(diag_wifi,"",20,184,538,false);
    network_diag_host=label(diag_bridge,"",20,184,538,false);
    lv_obj_set_height(network_diag_name,64);lv_label_set_long_mode(network_diag_name,LV_LABEL_LONG_DOT);
    lv_obj_set_height(network_diag_host,64);lv_label_set_long_mode(network_diag_host,LV_LABEL_LONG_DOT);
    muted(diag_wifi,"IP 地址",20,282,120);network_diag_ip=label(diag_wifi,"",148,282,410,false);
    muted(diag_bridge,"USB、蓝牙状态见“网络连接”",20,282,538);
    network_retry=button(network_diag_window,"重新连接 Wi-Fi",926,492,254,network_action,1);lv_obj_set_height(network_retry,60);
    network_diag_feedback=muted(network_diag_window,"",0,492,892);
    lv_obj_set_height(network_diag_feedback,100);lv_label_set_long_mode(network_diag_feedback,LV_LABEL_LONG_WRAP);
    visible(network_diag_window,false);
    host_editor=card(screen,30,70,1220,650);
    label(host_editor,"修改电脑备注",0,0,650,false);
    lv_obj_t *host_save=button(host_editor,"保存",900,0,126,host_editor_action,1);
    ui_bg_color(host_save,lv_color_hex(BLUE),0);tint(lv_obj_get_child(host_save,0),0x173642);
    button(host_editor,"取消",1050,0,130,host_editor_action,0);
    host_editor_input=lv_textarea_create(host_editor);lv_obj_set_pos(host_editor_input,0,68);lv_obj_set_size(host_editor_input,1180,70);
    lv_textarea_set_one_line(host_editor_input,true);lv_textarea_set_max_length(host_editor_input,63);
    ui_text_font(host_editor_input,&tab5_font_30,0);ui_bg_color(host_editor_input,lv_color_hex(0x354247),0);ui_text_color(host_editor_input,lv_color_hex(INK),0);
    host_editor_hint=muted(host_editor,"留空保存可恢复电脑原名",0,148,1180);
    host_editor_keyboard=lv_keyboard_create(host_editor);lv_obj_set_align(host_editor_keyboard,LV_ALIGN_TOP_LEFT);lv_obj_set_pos(host_editor_keyboard,0,318);lv_obj_set_size(host_editor_keyboard,1180,286);
    ui_bg_color(host_editor_keyboard,lv_color_hex(UI_BACKGROUND),0);lv_obj_set_style_border_width(host_editor_keyboard,0,0);
    lv_obj_set_style_pad_all(host_editor_keyboard,8,0);lv_obj_set_style_pad_row(host_editor_keyboard,8,0);lv_obj_set_style_pad_column(host_editor_keyboard,8,0);
    ui_bg_color(host_editor_keyboard,lv_color_hex(0x354247),LV_PART_ITEMS);ui_text_color(host_editor_keyboard,lv_color_hex(INK),LV_PART_ITEMS);
    lv_obj_set_style_radius(host_editor_keyboard,8,LV_PART_ITEMS);lv_obj_set_style_border_width(host_editor_keyboard,0,LV_PART_ITEMS);
    ui_bg_color(host_editor_keyboard,lv_color_hex(0x40525A),LV_PART_ITEMS|LV_STATE_CHECKED);ui_text_color(host_editor_keyboard,lv_color_hex(INK),LV_PART_ITEMS|LV_STATE_CHECKED);
    ui_bg_color(host_editor_keyboard,lv_color_hex(BLUE),LV_PART_ITEMS|LV_STATE_PRESSED);ui_text_color(host_editor_keyboard,lv_color_hex(0x173642),LV_PART_ITEMS|LV_STATE_PRESSED);
    lv_obj_add_event_cb(host_editor_keyboard,host_editor_ready,LV_EVENT_READY,(void *)(intptr_t)1);
    lv_obj_add_event_cb(host_editor_keyboard,host_editor_ready,LV_EVENT_CANCEL,NULL);
    host_editor_ime=tab5_ime_create(host_editor,host_editor_keyboard,host_editor_input,&tab5_font_24);
    lv_obj_set_y(host_editor_ime,214);lv_obj_set_width(host_editor_ime,1180);lv_obj_set_width(lv_obj_get_child(host_editor_ime,2),914);
    ui_bg_opa(lv_obj_get_child(host_editor_ime,2),LV_OPA_TRANSP,LV_PART_ITEMS|LV_STATE_DISABLED);
    visible(host_editor,false);
    flash_layer=card(screen,0,0,1280,720);
    lv_obj_t *flash_title=label(flash_layer,"存储测速 · 小样本对比",60,42,850,false);
    ui_text_font(flash_title,&tab5_font_30,0);
    flash_close=button(flash_layer,"返回",1040,28,180,flash_action,2);
    flash_details=label(flash_layer,"",60,150,1160,false);lv_label_set_long_mode(flash_details,LV_LABEL_LONG_WRAP);
    lv_obj_set_style_text_line_space(flash_details,12,0);
    muted(flash_layer,"128 KiB 用于比较分块；持续写入上限还需对照整包升级。",60,530,1160);
    flash_start=button(flash_layer,"开始测试",60,600,254,flash_action,1);lv_obj_set_height(flash_start,64);
    flash_cancel=button(flash_layer,"停止测试",342,600,254,flash_action,3);lv_obj_set_height(flash_cancel,64);
    visible(flash_cancel,false);visible(flash_layer,false);
    startup_home=ui_startup_home_begin();page(0);
    saver_layer=shape(screen,0,0,1280,720,0x000000,0);
    lv_obj_add_flag(saver_layer,LV_OBJ_FLAG_CLICKABLE);
    lv_obj_add_event_cb(saver_layer,saver_wake,LV_EVENT_CLICKED,NULL);
    saver_time=label(saver_layer,"--:--",210,224,740,true);
    ui_text_font(saver_time,&tab5_timer_digits_192,0);tint(saver_time,0xCCD2DA);
    lv_obj_set_style_text_letter_space(saver_time,4,0);
    lv_obj_set_width(saver_time,LV_SIZE_CONTENT);
    classic_seconds=label(saver_layer,"--",952,321,140,false);
    ui_text_font(classic_seconds,&tab5_digits_72,0);tint(classic_seconds,0x667382);
    // Keep the centered clock group stable as proportional seconds digits change.
    int seconds_width=0;lv_point_t seconds_size;
    for(int second=-1;second<60;second++) {
        char digits[4];if(second<0)snprintf(digits,sizeof(digits),"--");else snprintf(digits,sizeof(digits),"%02d",second);
        lv_text_get_size(&seconds_size,digits,&tab5_digits_72,lv_obj_get_style_text_letter_space(classic_seconds,0),0,LV_COORD_MAX,LV_TEXT_FLAG_NONE);
        if(seconds_size.x>seconds_width)seconds_width=seconds_size.x;
    }
    lv_obj_set_width(classic_seconds,seconds_width);
    lv_obj_set_style_text_align(classic_seconds,LV_TEXT_ALIGN_CENTER,0);
    classic_rule=shape(saver_layer,420,454,440,1,0x28313A,0);
    saver_date=label(saver_layer,"等待校时",180,494,920,false);tint(saver_date,0x85919E);
    ui_text_font(saver_date,&tab5_font_30,0);lv_obj_set_style_text_align(saver_date,LV_TEXT_ALIGN_CENTER,0);
    annual_create();monthly_create();date_saver_create();
#if TAB5_LOCAL_ART
    gallery_create();
#endif
    lv_obj_add_flag(saver_layer,LV_OBJ_FLAG_HIDDEN);
    camera_layer=lv_obj_create(screen);
    lv_obj_set_pos(camera_layer,0,0);lv_obj_set_size(camera_layer,1280,720);
    lv_obj_set_style_pad_all(camera_layer,0,0);
    lv_obj_set_style_border_width(camera_layer,0,0);
    ui_bg_color(camera_layer,lv_color_black(),0);
    lv_obj_remove_flag(camera_layer,LV_OBJ_FLAG_SCROLLABLE);
    camera_canvas=lv_canvas_create(camera_layer);
    lv_obj_set_pos(camera_canvas,0,0);lv_obj_set_size(camera_canvas,CAMERA_VIEW_WIDTH,CAMERA_VIEW_HEIGHT);
    camera_canvas_init(camera_canvas);
    lv_obj_add_event_cb(camera_canvas,camera_cover_check,LV_EVENT_COVER_CHECK,NULL);
    camera_hint=label(camera_layer,"摄像头启动中",24,20,1020,false);
    lv_obj_set_width(camera_hint,LV_SIZE_CONTENT);lv_obj_set_style_max_width(camera_hint,1020,0);
    lv_obj_set_style_pad_hor(camera_hint,16,0);lv_obj_set_style_pad_ver(camera_hint,10,0);
    lv_obj_set_style_radius(camera_hint,14,0);ui_bg_color(camera_hint,lv_color_hex(0x14212A),0);
    ui_bg_opa(camera_hint,LV_OPA_60,0);
    camera_close=button(camera_layer,"关闭",1104,16,148,camera_action,3);lv_obj_set_height(camera_close,56);
    ui_text_color(camera_close,lv_color_hex(INK),0);
    ui_bg_opa(camera_close,LV_OPA_60,0);
    // A tall edge target can be found by touch while the screen faces the subject.
    // Keep confirm and close outside it, so a second tap cannot submit a photo.
    camera_take=button(camera_layer,"",0,208,188,camera_action,1);
    // Blind taps can drift before release: accept the press once, then keep the
    // shutter disabled until capture finishes. Release cannot trigger a retake.
    lv_obj_remove_event_cb(camera_take,camera_action);
    lv_obj_add_event_cb(camera_take,camera_action,LV_EVENT_PRESSED,(void *)(intptr_t)1);
    lv_obj_set_size(camera_take,188,304);lv_obj_clean(camera_take);lv_obj_set_style_pad_all(camera_take,0,0);
    // The large hit target is invisible; only the shutter and caption cover the image.
    ui_bg_opa(camera_take,LV_OPA_TRANSP,0);
    ui_bg_opa(camera_take,LV_OPA_TRANSP,LV_STATE_PRESSED);
    ui_bg_opa(camera_take,LV_OPA_TRANSP,LV_STATE_DISABLED);
    lv_obj_set_style_border_width(camera_take,0,0);
    lv_obj_set_style_outline_width(camera_take,0,0);
    lv_obj_set_style_opa(camera_take,LV_OPA_40,LV_STATE_DISABLED);
    lv_obj_t *shutter_ring=shape(camera_take,30,64,128,128,INK,LV_RADIUS_CIRCLE);
    ui_bg_opa(shutter_ring,LV_OPA_TRANSP,0);
    ui_border_color(shutter_ring,lv_color_hex(INK),0);lv_obj_set_style_border_width(shutter_ring,4,0);
    shape(camera_take,42,76,104,104,INK,LV_RADIUS_CIRCLE);
    lv_obj_t *shutter_caption=label(camera_take,"拍摄",20,218,148,false);lv_obj_set_style_text_align(shutter_caption,LV_TEXT_ALIGN_CENTER,0);
    camera_retry=button(camera_layer,"重拍",28,628,180,camera_action,2);lv_obj_set_height(camera_retry,64);
    ui_bg_opa(camera_retry,LV_OPA_60,0);
    lv_obj_add_state(camera_take,LV_STATE_DISABLED);
    lv_obj_add_state(camera_retry,LV_STATE_DISABLED);
    lv_obj_add_flag(camera_layer,LV_OBJ_FLAG_HIDDEN);
    camera_use=button(camera_layer,"添加照片",1024,628,224,camera_action,4);lv_obj_set_height(camera_use,64);visible(camera_use,false);
    ui_bg_color(camera_use,lv_color_hex(BLUE),0);ui_text_color(camera_use,lv_color_hex(0x14212A),0);
    // This LVGL version dereferences a present transition property even if its
    // value is NULL. An empty descriptor disables it without a null access.
    static const lv_style_prop_t no_camera_transition_props[]={0};
    static const lv_style_transition_dsc_t no_camera_transition={.props=no_camera_transition_props};
    lv_obj_t *camera_controls[]={camera_close,camera_take,camera_retry,camera_use};
    for(unsigned i=0;i<sizeof(camera_controls)/sizeof(camera_controls[0]);i++)
        lv_obj_set_style_transition(camera_controls[i],&no_camera_transition,0);
    // Keep the exit control above both live and captured-photo overlays.
    lv_obj_move_foreground(camera_close);
#ifndef TAB5_UI_PREVIEW
    camera_viewport_set_controls(camera_take,camera_close);
#endif
    camera_preview_set_orientation_handler(camera_orientation_apply);
    photo_ui_create(screen,task_right);
    codex_restore_draft();
    date_editor_create(screen);
    // Create after Codex's composer so the legacy default IME remains Codex-owned.
    agenda_editor_create(screen);
    clock_segments=clock_segments_create(screen,0);visible(clock_segments,current_page==7);
    clock_countdown_create(screen);clock_picker_create(screen);
    lv_timer_create(clock_tick, 50, NULL);
    lv_timer_create(tick, 100, NULL);
    lv_timer_create(cache_tick,30,NULL);
#ifndef TAB5_UI_PREVIEW
    lv_timer_create(ui_internal_buffer_try,15000,NULL);
#endif
    // Keep the bootloader/panel initialization invisible until the first frame.
    lv_display_add_event_cb(display,first_frame_ready,LV_EVENT_REFR_READY,NULL);
    bsp_display_unlock();
}

