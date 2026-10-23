#include <assert.h>
#pragma once
#include <string.h>
#define gmtime_r(input,output) gmtime_s(output,input)
#include "lvgl.h"
// ui.c maps the portrait hardware to this landscape preview exactly once.
// FULL render buffers need a new stride when changing between aspect ratios;
// the production port uses PARTIAL rendering and reshapes every strip.
static uint16_t preview_buffer[1280*720];
static void preview_display_rotation(lv_display_t *d,lv_display_rotation_t r) {
    lv_display_set_rotation(d,r);
    lv_display_set_buffers(d,preview_buffer,NULL,sizeof(preview_buffer),LV_DISPLAY_RENDER_MODE_FULL);
}
#define lv_display_set_rotation(display,rotation) preview_display_rotation(display,rotation)
#include "photo_display_probe.h"
void photo_display_probe_mark(unsigned stage){(void)stage;}
#include "art_orientation.h"
static art_orientation_state preview_art_orientation;
art_orientation_state art_orientation_get(void){return preview_art_orientation;}
#include "link_state.h"
#include "ui_prefs.h"
#include "assets.h"
#define portMAX_DELAY 0
#define xSemaphoreTake(a,b) (1)
#define xSemaphoreGive(a) ((void)0)
#define BSP_LCD_H_RES 720
#define ESP_LVGL_PORT_INIT_CONFIG() 0
typedef struct { char id[13]; bool paired; char host[16],ssid[33],password[64]; int port,mode,brightness; } device_config;
typedef struct { int lvgl_port_cfg,buffer_size; bool double_buffer; struct { bool buff_dma,buff_spiram,sw_rotate; } flags; } bsp_display_cfg_t;
static link_state device_links;
static cJSON *device_snapshots[3];
static cJSON *device_metrics[3];
static uint64_t device_metrics_sequence[3];
static bool wifi_connected=true,ble_available=true;
static int64_t preview_now_us=10000000;
static int64_t esp_timer_get_time(void) { return preview_now_us; }
static device_config preview_config={.id="preview",.paired=true,.host="127.0.0.1",.ssid="家庭网络",.port=18765,.mode=-1,.brightness=80};
static device_config device_get_config(void) { return preview_config; }
static void device_set_mode(int mode) { (void)mode; }
static void device_set_brightness(int n) { if(n>=10&&n<=100)preview_config.brightness=n; }
static bool device_save_brightness(int n){preview_config.brightness=n;return true;}
static bool display_settings_report(cJSON *report,link_kind link){(void)link;cJSON_Delete(report);return true;}
typedef struct {int volume;bool muted;} device_sound;
static device_sound preview_sound={35,false};
static bool preview_sound_save_ok=true;
static device_sound device_get_sound(void){return preview_sound;}
static bool device_set_sound(int volume,bool muted){if(!preview_sound_save_ok)return false;preview_sound=(device_sound){volume,muted};return true;}
static int device_sound_volume(void){return preview_sound.muted?0:preview_sound.volume;}
static int preview_brightness;
#define ESP_OK 0
static int bsp_display_brightness_set(int n) { preview_brightness=n; return ESP_OK; }
static void bsp_reset_tp(void) {}
static void bsp_display_lock(int n) { (void)n; }
static void bsp_display_unlock(void) {}
void crash_record_checkpoint(unsigned channel,unsigned stage,unsigned detail) {(void)channel;(void)stage;(void)detail;}
static void preview_flush(lv_display_t *d,const lv_area_t *a,uint8_t *pixels) { (void)a;(void)pixels;lv_display_flush_ready(d); }
static lv_display_t *bsp_display_start_with_config(bsp_display_cfg_t *cfg) { (void)cfg; lv_display_t *d=lv_display_create(1280,720); lv_display_set_color_format(d,LV_COLOR_FORMAT_RGB565); lv_display_set_buffers(d,preview_buffer,NULL,sizeof(preview_buffer),LV_DISPLAY_RENDER_MODE_FULL); lv_display_set_flush_cb(d,preview_flush); return d; }
ui_preferences ui_preferences_load(void) { ui_preferences p={.version=1,.selected=-1,.interval=15,.saver_minutes=5,.cycle=true,.alerts=true}; for(int i=0;i<8;i++) p.enabled[i]=1,p.order[i]=i; return p; }
static unsigned preview_preferences_saves;
static bool preview_preferences_save_ok=true;
static ui_preferences preview_saved_preferences;
bool ui_preferences_save(const ui_preferences *p) {preview_preferences_saves++;if(!preview_preferences_save_ok)return false;preview_saved_preferences=*p;return true;}
static ui_reading_bookmark preview_reading[32];
bool ui_reading_load(ui_reading_bookmark values[32]){memcpy(values,preview_reading,sizeof(preview_reading));return true;}
bool ui_reading_save(const ui_reading_bookmark values[32]){memcpy(preview_reading,values,sizeof(preview_reading));return true;}
int ui_clock_style_load(void) { return 0; }
static int preview_saved_orientation;
static bool preview_orientation_save_fail;
int ui_orientation_load(void){return preview_saved_orientation;}
bool ui_orientation_save(int mode){if(preview_orientation_save_fail)return false;preview_saved_orientation=mode;return true;}
static int preview_saved_saver;
static bool preview_saved_tf_cache,preview_tf_cache_save_fail;
bool ui_tf_cache_load(void){return preview_saved_tf_cache;}
bool ui_tf_cache_save(bool enabled){if(preview_tf_cache_save_fail)return false;preview_saved_tf_cache=enabled;return true;}
static bool preview_saver_save_fail;
int ui_saver_style_load(void) {return preview_saved_saver;}
bool ui_saver_style_save(int style) {if(preview_saver_save_fail||!ui_saver_style_valid(style))return false;preview_saved_saver=style;return true;}
static ui_date_countdown preview_date_countdown;
static bool preview_date_save_fail;
bool ui_date_countdown_load(ui_date_countdown *v){*v=preview_date_countdown;return v->version==1;}
bool ui_date_countdown_save(const ui_date_countdown *v){if(preview_date_save_fail)return false;preview_date_countdown=*v;return true;}
unsigned ui_home_models_load(void) {return (1u<<1)|(1u<<5)|(1u<<6);}
static unsigned preview_saved_models;
static bool preview_models_save_fail;
bool ui_home_models_save(unsigned mask) {unsigned count=0;for(int i=0;i<UI_HOME_PROVIDERS;i++)count+=!!(mask&(1u<<i));if(preview_models_save_fail||mask>=(1u<<UI_HOME_PROVIDERS)||count>3)return false;preview_saved_models=mask;return true;}
static int preview_saved_clock=-1;
static bool preview_clock_save_fail;
bool ui_clock_style_save(int style) {if(preview_clock_save_fail)return false;preview_saved_clock=style;return style>=0&&style<UI_CLOCK_STYLES;}
bool assets_copy(int slot,uint32_t revision,asset_image *out) {
    if(revision) return false;
    int w=slot==0?64:slot==1?120:112,h=slot==0?96:slot==1?120:112;
    *out=(asset_image){.width=w,.height=h,.frames=2,.delays={100,100},.revision=1,.crc=slot+1};
    out->pixels=lv_malloc((size_t)w*h*4);
    for(int f=0;f<2;f++) for(int y=0;y<h;y++) for(int x=0;x<w;x++) {
        // Geometric test assets, not user artwork: exposes centering, clipping and aspect ratio.
        bool inside=x>w/5&&x<w*4/5&&y>h/5&&y<h*4/5;
        ((uint16_t *)out->pixels)[f*w*h+y*w+x]=inside?(slot==0?0xFDA0:slot==1?0x05BF:0xA83F):0x0000;
    }
    return true;
}
unsigned assets_ready(void) { return 7; }
int assets_progress(int slot) { (void)slot;return 0; }

static ui_codex_checkpoint preview_checkpoint;
static bool preview_checkpoint_ok=true,preview_checkpoint_present;
static unsigned preview_checkpoint_writes;
bool ui_codex_checkpoint_load(ui_codex_checkpoint *value) {if(!preview_checkpoint_present)return false;*value=preview_checkpoint;return true;}
bool ui_codex_checkpoint_save(const ui_codex_checkpoint *value) {if(!preview_checkpoint_ok)return false;preview_checkpoint=*value;preview_checkpoint_present=true;preview_checkpoint_writes++;return true;}

static bool preview_remote_result_ready,preview_remote_commit=true,preview_remote_confirmed=true;
static char preview_remote_result_id[33],preview_remote_stage[16];
static unsigned preview_remote_reports;
static bool ota_remote_report(cJSON *report,link_kind link) {
    assert(link!=LINK_NONE);preview_remote_reports++;
    snprintf(preview_remote_result_id,33,"%s",cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(report,"requestId")));
    snprintf(preview_remote_stage,16,"%s",cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(report,"stage")));
    preview_remote_result_ready=true;cJSON_Delete(report);return true;
}
static bool ota_remote_result(char id[33],bool *commit,bool *confirmed) {
    if(!preview_remote_result_ready)return false;preview_remote_result_ready=false;
    snprintf(id,33,"%s",preview_remote_result_id);
    *commit=preview_remote_commit&&!strcmp(preview_remote_stage,"ready");*confirmed=preview_remote_confirmed;return true;
}
