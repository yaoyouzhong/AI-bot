#include "ui_prefs.h"
#include "nvs.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>
#include "countdown.h"
static ui_date_countdown date_disk,date_pending;
static bool date_exists;
static int disk=-1,pending=-1;static bool fail_open,fail_set,fail_commit;
static int model_disk=-1,model_pending=-1,legacy_models=-1;
static int clock_disk=-1,clock_pending=-1;
static int orientation_disk=-1,orientation_pending=-1;
int nvs_open(const char *name,int mode,nvs_handle_t *h){assert(!strcmp(name,"aibot_tab5"));(void)mode;*h=1;pending=disk;model_pending=model_disk;clock_pending=clock_disk;orientation_pending=orientation_disk;return fail_open?-1:0;}
void nvs_close(nvs_handle_t h){(void)h;}
int nvs_get_u8(nvs_handle_t h,const char *key,uint8_t *value){(void)h;if(!strcmp(key,"orientation")){if(orientation_disk<0)return -1;*value=(uint8_t)orientation_disk;return 0;}if(!strcmp(key,"clock_style")){if(clock_disk<0)return -1;*value=(uint8_t)clock_disk;return 0;}if(!strcmp(key,"home_models")){if(legacy_models<0)return -1;*value=(uint8_t)legacy_models;return 0;}assert(!strcmp(key,"saver_style"));if(disk<0)return -1;*value=(uint8_t)disk;return 0;}
int nvs_get_u16(nvs_handle_t h,const char *key,uint16_t *value){(void)h;assert(!strcmp(key,"home_providers"));if(model_disk<0)return -1;*value=(uint16_t)model_disk;return 0;}
int nvs_set_u16(nvs_handle_t h,const char *key,uint16_t value){(void)h;assert(!strcmp(key,"home_providers"));if(fail_set)return -1;model_pending=value;return 0;}
int nvs_set_u8(nvs_handle_t h,const char *key,uint8_t value){(void)h;if(fail_set)return -1;if(!strcmp(key,"orientation"))orientation_pending=value;else if(!strcmp(key,"clock_style"))clock_pending=value;else {assert(!strcmp(key,"saver_style"));pending=value;}return 0;}
int nvs_get_blob(nvs_handle_t h,const char *key,void *data,size_t *length){(void)h;assert(!strcmp(key,"date_countdown"));if(!date_exists)return -1;assert(*length>=sizeof(date_disk));memcpy(data,&date_disk,sizeof(date_disk));*length=sizeof(date_disk);return 0;}
int nvs_set_blob(nvs_handle_t h,const char *key,const void *data,size_t length){(void)h;assert(!strcmp(key,"date_countdown")&&length==sizeof(date_disk));if(fail_set)return -1;memcpy(&date_pending,data,length);return 0;}
int nvs_commit(nvs_handle_t h){(void)h;if(fail_commit)return -1;disk=pending;model_disk=model_pending;clock_disk=clock_pending;orientation_disk=orientation_pending;if(date_pending.version){date_disk=date_pending;date_exists=true;}return 0;}
int main(void) {
    assert(ui_orientation_load()==0);
    for(int mode=0;mode<=2;mode++)assert(ui_orientation_save(mode)&&ui_orientation_load()==mode);
    fail_commit=true;assert(!ui_orientation_save(0)&&ui_orientation_load()==2);fail_commit=false;
    fail_set=true;assert(!ui_orientation_save(0)&&ui_orientation_load()==2);fail_set=false;
    fail_open=true;assert(!ui_orientation_save(0)&&ui_orientation_load()==0);fail_open=false;
    assert(!ui_orientation_save(-1)&&!ui_orientation_save(3)&&orientation_disk==2);
    orientation_disk=255;assert(ui_orientation_load()==0);
    puts("ORIENTATION_PREFS_OK default, both locks survive reload, invalid fallback, storage failures preserve previous value");
    assert(ui_clock_style_load()==0);
    for(int old=0;old<5;old++){clock_disk=old;assert(ui_clock_style_load()==old);}
    for(int style=0;style<UI_CLOCK_STYLES;style++)assert(ui_clock_style_save(style)&&ui_clock_style_load()==style);
    clock_disk=7;fail_commit=true;assert(!ui_clock_style_save(8)&&ui_clock_style_load()==7);fail_commit=false;
    fail_set=true;assert(!ui_clock_style_save(0)&&ui_clock_style_load()==7);fail_set=false;
    fail_open=true;assert(!ui_clock_style_save(0)&&ui_clock_style_load()==0);fail_open=false;
    assert(!ui_clock_style_save(UI_CLOCK_STYLES)&&!ui_clock_style_save(-1)&&clock_disk==7);
    clock_disk=255;assert(ui_clock_style_load()==0);
    puts("CLOCK_PREFS_OK five_old_IDs_retained nine_choices_restart open_set_commit_failures invalid_fallback");
    assert(ui_saver_style_load()==0);assert(ui_saver_style_save(1)&&ui_saver_style_load()==1);
    assert(!ui_saver_style_save(-1)&&!ui_saver_style_save(UI_SAVER_STYLES)&&disk==1);
    fail_commit=true;assert(!ui_saver_style_save(0)&&ui_saver_style_load()==1);fail_commit=false;
    fail_set=true;assert(!ui_saver_style_save(0)&&disk==1);fail_set=false;
    fail_open=true;assert(!ui_saver_style_save(0)&&ui_saver_style_load()==0);fail_open=false;
    disk=255;assert(ui_saver_style_load()==0);
#if !TAB5_LOCAL_ART
    disk=6;assert(ui_saver_style_load()==0&&disk==6); // Read fallback preserves local preference on disk.
    disk=7;assert(ui_saver_style_load()==0&&disk==7);
#endif
    assert(ui_saver_style_save(0)&&ui_saver_style_load()==0);
    for(int style=0;style<UI_SAVER_STYLES;style++)if(ui_saver_style_valid(style))assert(ui_saver_style_save(style)&&ui_saver_style_load()==style);
    puts("SAVER_PREFS_OK default, persistence, invalid values and open/set/commit failures; existing preference blob untouched");
    for(int old=1;old<=2;old++) {
        disk=old;int loaded=ui_saver_style_normalize(ui_saver_style_load());
        assert(loaded==5&&disk==old&&ui_saver_option_from_style(loaded)==1);
    }
    for(int option=0;option<(TAB5_LOCAL_ART?7:5);option++) {
        int style=ui_saver_style_from_option(option);
        assert(ui_saver_style_valid(style)&&ui_saver_option_from_style(style)==option);
        assert(ui_saver_style_save(style)&&ui_saver_style_load()==style);
    }
    puts("SAVER_OPTIONS_OK legacy day/night normalize without disk writes; visible options persist and roundtrip");
    assert(ui_home_models_load()==0); // Fresh users start in automatic mode.
    for(int legacy=1;legacy<8;legacy++) {
        legacy_models=legacy;unsigned expected=((legacy&1)?1u<<1:0)|((legacy&2)?1u<<5:0)|((legacy&4)?1u<<6:0);
        assert(ui_home_models_load()==expected&&model_disk==-1&&legacy_models==legacy);
    }
    assert(ui_home_models_save((1u<<0)|(1u<<3)|(1u<<9))&&ui_home_models_load()==((1u<<0)|(1u<<3)|(1u<<9)));
    assert(ui_home_models_save(1u<<10)&&ui_home_models_load()==(1u<<10));
    assert(!ui_home_models_save(15)&&!ui_home_models_save(2048));
    fail_commit=true;assert(!ui_home_models_save(0)&&ui_home_models_load()!=0);fail_commit=false;
    fail_set=true;assert(!ui_home_models_save(0)&&ui_home_models_load()!=0);fail_set=false;
    fail_open=true;assert(!ui_home_models_save(0)&&ui_home_models_load()==0);fail_open=false;
    assert(ui_home_models_save(0)&&ui_home_models_load()==0&&legacy_models==7);
    model_disk=15;assert(ui_home_models_load()==0);model_disk=2048;assert(ui_home_models_load()==0);
    puts("HOME_PROVIDER_PREFS_OK new_user_auto, seven_legacy_masks_preserved, eleven_provider_bits, max_three, auto_persists, open_set_commit_failures");
    ui_date_countdown value={0};assert(!ui_date_countdown_load(&value));
    value=(ui_date_countdown){.version=1,.year=2028,.month=2,.day=29,.title="Date"};
    assert(ui_date_countdown_save(&value));memset(&value,0,sizeof(value));assert(ui_date_countdown_load(&value)&&value.day==29&&value.year==2028);
    value.year=2027;assert(!ui_date_countdown_save(&value));value.year=2028;value.day=28;
    fail_commit=true;assert(!ui_date_countdown_save(&value));fail_commit=false;assert(ui_date_countdown_load(&value)&&value.day==29);
    fail_set=true;assert(!ui_date_countdown_save(&value));fail_set=false;
    fail_open=true;assert(!ui_date_countdown_save(&value));fail_open=false;
    strcpy(value.title,"1234567890");assert(ui_date_countdown_save(&value));
    strcpy(value.title,"12345678901");assert(!ui_date_countdown_save(&value));
    strcpy(date_disk.title,"12345678901");assert(ui_date_countdown_load(&value)&&!strcmp(value.title,"12345678901"));
    assert(!ui_date_countdown_save(&value));
    date_disk.title[0]=0;assert(!ui_date_countdown_load(&value));
    assert(countdown_civil_day(2028,3,1)-countdown_civil_day(2028,2,28)==2);
    assert(countdown_civil_day(2027,3,1)-countdown_civil_day(2027,2,28)==1);
    assert(countdown_civil_day(2027,1,1)-countdown_civil_day(2026,12,31)==1);
    assert(!countdown_date_valid(2100,2,29)&&countdown_date_valid(2000,2,29));
    countdown_timer timer;countdown_reset(&timer,86399);countdown_start(&timer,1000);
    assert(!countdown_tick(&timer,500)&&timer.remaining_ms>0); // signed monotonic arithmetic, no underflow
    assert(countdown_tick(&timer,1000+86399000)&&!countdown_tick(&timer,1000+86399001));
    countdown_reset(&timer,0);countdown_start(&timer,1000);assert(timer.phase==COUNTDOWN_READY);
    puts("COUNTDOWN_MODEL_OK civil_days leap_year century year_boundary date_NVS_restart failures invalid_data max_duration zero one_shot_expiry");
}
