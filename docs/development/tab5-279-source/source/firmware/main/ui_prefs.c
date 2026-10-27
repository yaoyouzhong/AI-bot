#include "ui_prefs.h"
#include "nvs.h"
#include <string.h>
#include <stddef.h>
#include "countdown.h"
int ui_orientation_load(void) {
    nvs_handle_t h;uint8_t mode=0;
    if(nvs_open("aibot_tab5",NVS_READONLY,&h)==ESP_OK) {
        if(nvs_get_u8(h,"orientation",&mode)!=ESP_OK)mode=0;
        nvs_close(h);
    }
    return mode<=2?mode:0;
}
bool ui_orientation_save(int mode) {
    if(mode<0||mode>2)return false;
    nvs_handle_t h;if(nvs_open("aibot_tab5",NVS_READWRITE,&h)!=ESP_OK)return false;
    esp_err_t result=nvs_set_u8(h,"orientation",(uint8_t)mode);
    if(result==ESP_OK)result=nvs_commit(h);
    nvs_close(h);return result==ESP_OK;
}
static bool date_countdown_valid(const ui_date_countdown *v) {
    return v&&v->version==1&&countdown_date_valid(v->year,v->month,v->day)&&v->title[0]&&memchr(v->title,0,sizeof(v->title));
}
bool ui_date_countdown_load(ui_date_countdown *value) {
    if(!value)return false;
    memset(value,0,sizeof(*value));nvs_handle_t h;
    if(nvs_open("aibot_tab5",NVS_READONLY,&h)!=ESP_OK)return false;
    size_t length=sizeof(*value);esp_err_t r=nvs_get_blob(h,"date_countdown",value,&length);nvs_close(h);
    if(r==ESP_OK&&length==sizeof(*value)&&date_countdown_valid(value))return true;
    memset(value,0,sizeof(*value));return false;
}
bool ui_date_countdown_save(const ui_date_countdown *value) {
    if(!date_countdown_valid(value))return false;
    unsigned chars=0;for(const unsigned char *p=(const unsigned char *)value->title;*p;p++)if((*p&0xC0)!=0x80)chars++;
    if(chars>10)return false;
    nvs_handle_t h;if(nvs_open("aibot_tab5",NVS_READWRITE,&h)!=ESP_OK)return false;
    esp_err_t r=nvs_set_blob(h,"date_countdown",value,sizeof(*value));
    if(r==ESP_OK)r=nvs_commit(h);
    nvs_close(h);return r==ESP_OK;
}
bool ui_codex_checkpoint_load(ui_codex_checkpoint *value) {
    nvs_handle_t h;if(nvs_open("aibot_tab5",NVS_READONLY,&h)!=ESP_OK)return false;
    memset(value,0,sizeof(*value));size_t length=sizeof(*value);esp_err_t r=nvs_get_blob(h,"codex_draft",value,&length);nvs_close(h);
    // The previous structure ended after text, rounded to uint32 alignment.
    size_t legacy=(offsetof(ui_codex_checkpoint,images)+3)&~(size_t)3;
    if(r==ESP_OK&&length==legacy)memset(value->images,0,sizeof(value->images));
    return r==ESP_OK&&(length==sizeof(*value)||length==legacy)&&value->version==1&&value->task[36]==0&&
        value->images[0][36]==0&&value->images[1][36]==0&&value->images[2][36]==0&&
        value->request[36]==0&&value->operation[15]==0&&value->turn[36]==0&&value->text[2000]==0;
}
bool ui_codex_checkpoint_save(const ui_codex_checkpoint *value) {
    if(!value||value->version!=1)return false;
    nvs_handle_t h;if(nvs_open("aibot_tab5",NVS_READWRITE,&h)!=ESP_OK)return false;
    esp_err_t r=nvs_set_blob(h,"codex_draft",value,sizeof(*value));if(r==ESP_OK)r=nvs_commit(h);nvs_close(h);return r==ESP_OK;
}
unsigned ui_home_models_load(void) {
    uint16_t mask=0;nvs_handle_t h;
    if(nvs_open("aibot_tab5",NVS_READONLY,&h)==ESP_OK) {
        if(nvs_get_u16(h,"home_providers",&mask)!=ESP_OK) {
            uint8_t legacy=0;
            if(nvs_get_u8(h,"home_models",&legacy)==ESP_OK&&legacy>0&&legacy<8)
                mask=((legacy&1)?1u<<1:0)|((legacy&2)?1u<<5:0)|((legacy&4)?1u<<6:0);
        }
        nvs_close(h);
    }
    unsigned count=0;for(unsigned i=0;i<UI_HOME_PROVIDERS;i++)count+=!!(mask&(1u<<i));
    return mask<(1u<<UI_HOME_PROVIDERS)&&count<=3?mask:0;
}
bool ui_home_models_save(unsigned mask) {
    unsigned count=0;for(unsigned i=0;i<UI_HOME_PROVIDERS;i++)count+=!!(mask&(1u<<i));
    if(mask>=(1u<<UI_HOME_PROVIDERS)||count>3)return false;
    nvs_handle_t h;if(nvs_open("aibot_tab5",NVS_READWRITE,&h)!=ESP_OK)return false;
    esp_err_t r=nvs_set_u16(h,"home_providers",(uint16_t)mask);if(r==ESP_OK)r=nvs_commit(h);nvs_close(h);return r==ESP_OK;
}
int ui_clock_style_load(void) {
    nvs_handle_t h;
    uint8_t style = 0;
    if (nvs_open("aibot_tab5", NVS_READONLY, &h) == ESP_OK) {
        nvs_get_u8(h, "clock_style", &style);
        nvs_close(h);
    }
    return style < UI_CLOCK_STYLES ? style : 0;
}
bool ui_clock_style_save(int style) {
    if (style < 0 || style >= UI_CLOCK_STYLES)
        return false;
    nvs_handle_t h;
    if (nvs_open("aibot_tab5", NVS_READWRITE, &h) != ESP_OK)
        return false;
    esp_err_t result = nvs_set_u8(h, "clock_style", (uint8_t)style);
    if (result == ESP_OK)
        result = nvs_commit(h);
    nvs_close(h);
    return result == ESP_OK;
}
int ui_saver_style_load(void) {
    nvs_handle_t h;uint8_t style=0;
    if(nvs_open("aibot_tab5",NVS_READONLY,&h)==ESP_OK) {
        nvs_get_u8(h,"saver_style",&style);nvs_close(h);
    }
    return ui_saver_style_valid(style)?style:0;
}
bool ui_tf_cache_load(void) {
    nvs_handle_t h;uint8_t enabled=0;
    if(nvs_open("aibot_tab5",NVS_READONLY,&h)==ESP_OK) {
        if(nvs_get_u8(h,"tf_cache",&enabled)!=ESP_OK)enabled=0;
        nvs_close(h);
    }
    return enabled==1;
}
bool ui_tf_cache_save(bool enabled) {
    nvs_handle_t h;if(nvs_open("aibot_tab5",NVS_READWRITE,&h)!=ESP_OK)return false;
    esp_err_t result=nvs_set_u8(h,"tf_cache",enabled?1:0);
    if(result==ESP_OK)result=nvs_commit(h);
    nvs_close(h);return result==ESP_OK;
}
bool ui_saver_style_save(int style) {
    if(!ui_saver_style_valid(style))return false;
    nvs_handle_t h;if(nvs_open("aibot_tab5",NVS_READWRITE,&h)!=ESP_OK)return false;
    esp_err_t result=nvs_set_u8(h,"saver_style",(uint8_t)style);
    if(result==ESP_OK)result=nvs_commit(h);
    nvs_close(h);return result==ESP_OK;
}
ui_preferences ui_preferences_load(void) {
    ui_preferences p = {.version = 1,
                        .selected = -1,
                        .interval = 15,
                        .saver_minutes = 5,
                        .cycle = true,
                        .alerts = true,
                        .enabled = {1, 1, 1, 1, 1, 1, 1, 0},
                        .order = {0, 1, 2, 3, 4, 5, 6, 7}};
    nvs_handle_t h;
    if (nvs_open("aibot_tab5", NVS_READONLY, &h) != ESP_OK)
        return p;
    ui_preferences candidate;
    size_t length = sizeof(candidate);
    if (nvs_get_blob(h, "ui", &candidate, &length) == ESP_OK && length == sizeof(candidate) && candidate.version == 1 &&
        candidate.selected >= -1 && candidate.selected < 8 && candidate.interval >= 10 && candidate.interval <= 60 &&
        candidate.saver_minutes >= 0 && candidate.saver_minutes <= 60) {
        unsigned seen = 0;
        for (int i = 0; i < 8; i++)
            if (candidate.order[i] < 8)
                seen |= 1u << candidate.order[i];
        if (seen == 255)
            p = candidate;
    }
    nvs_close(h);
    return p;
}
bool ui_preferences_save(const ui_preferences *p) {
    nvs_handle_t h;
    if (nvs_open("aibot_tab5", NVS_READWRITE, &h) != ESP_OK)
        return false;
    esp_err_t r = nvs_set_blob(h, "ui", p, sizeof(*p));
    if (r == ESP_OK)
        r = nvs_commit(h);
    nvs_close(h);
    return r == ESP_OK;
}
