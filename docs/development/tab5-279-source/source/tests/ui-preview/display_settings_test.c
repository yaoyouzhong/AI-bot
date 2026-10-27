#include <assert.h>
#include <stdio.h>
#include <string.h>
#include "cJSON.h"
#include "ui_prefs.h"
#include "link_state.h"
typedef struct {int brightness;} device_config;
typedef struct {int volume;bool muted;} device_sound;
static device_config config={75};static device_sound sound={35,false};static int saves,reports;static bool save_ok=true;
static device_config device_get_config(void){return config;}
static device_sound device_get_sound(void){return sound;}
static bool device_save_brightness(int n){config.brightness=n;return true;}
static bool device_set_sound(int n,bool muted){sound=(device_sound){n,muted};return true;}
bool ui_preferences_save(const ui_preferences *p){(void)p;saves++;return save_ok;}
static cJSON *report;
static bool display_settings_report(cJSON *r,link_kind link){assert(link!=LINK_NONE);reports++;cJSON_Delete(report);report=r;return true;}
#include "display_settings.inc"
static cJSON *frame(const char *id,cJSON *settings,int expiry) {
    cJSON *root=cJSON_CreateObject();cJSON_AddNumberToObject(root,"epochMilliseconds",100000);
    cJSON *input=cJSON_AddObjectToObject(root,"pcInput");cJSON_AddStringToObject(input,"session","test-session");
    cJSON *health=cJSON_AddObjectToObject(root,"connectionHealth"),*cmd=cJSON_AddObjectToObject(health,"displayCommand");
    cJSON_AddNumberToObject(cmd,"version",1);cJSON_AddStringToObject(cmd,"requestId",id);cJSON_AddNumberToObject(cmd,"expiresAt",expiry);cJSON_AddItemToObject(cmd,"settings",settings?settings:cJSON_CreateNull());return root;
}
int main(void) {
    ui_preferences prefs={.version=1,.selected=-1,.interval=15,.saver_minutes=5,.cycle=true,.alerts=true,.enabled={1,1,1,1,1,1,1,0},.order={0,1,2,3,4,5,6,7}};
    cJSON *read=frame("00000000000000000000000000000001",NULL,120000);
    assert(!display_settings_tick(read,&prefs,LINK_WIFI,1000,1000,false));assert(saves==0&&reports==1&&cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(report,"ok")));cJSON_Delete(read);
    cJSON *values=display_values(&prefs);cJSON_SetNumberValue(cJSON_GetObjectItemCaseSensitive(values,"brightness"),90);
    cJSON *apply=frame("00000000000000000000000000000002",values,120000);
    assert(display_settings_tick(apply,&prefs,LINK_USB,1000,1000,false));assert(config.brightness==90&&saves==1);
    assert(!display_settings_tick(apply,&prefs,LINK_BLE,1100,1000,false));assert(saves==1);cJSON_Delete(apply);
    values=display_values(&prefs);config.brightness=65;
    apply=frame("00000000000000000000000000000003",values,120000);
    assert(!display_settings_tick(apply,&prefs,LINK_WIFI,1000,1000,false));assert(!strcmp(cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(report,"error")),"conflict")&&saves==1);cJSON_Delete(apply);
    values=display_values(&prefs);cJSON_SetNumberValue(cJSON_GetArrayItem(cJSON_GetObjectItemCaseSensitive(values,"order"),1),0);
    apply=frame("00000000000000000000000000000004",values,120000);
    assert(!display_settings_tick(apply,&prefs,LINK_USB,1000,1000,false));assert(saves==1);cJSON_Delete(apply);
    values=display_values(&prefs);apply=frame("00000000000000000000000000000005",values,99999);
    assert(!display_settings_tick(apply,&prefs,LINK_WIFI,1000,1000,false));assert(saves==1);cJSON_Delete(apply);
    save_ok=false;values=display_values(&prefs);cJSON_SetNumberValue(cJSON_GetObjectItemCaseSensitive(values,"brightness"),80);apply=frame("00000000000000000000000000000006",values,120000);
    assert(!display_settings_tick(apply,&prefs,LINK_USB,1000,1000,false));assert(config.brightness==65&&!cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(report,"ok")));cJSON_Delete(apply);
    cJSON_Delete(report);cJSON_Delete(display_reply);
    puts("DISPLAY_SETTINGS_DEVICE_OK read-only, save/readback, duplicate, conflict, invalid pages, expired command, persistence failure");return 0;
}
