#include "ui_automation.h"
#include <math.h>
#ifndef TAB5_UI_PREVIEW
static portMUX_TYPE automation_mux=portMUX_INITIALIZER_UNLOCKED;
#define AUTOMATION_LOCK() taskENTER_CRITICAL(&automation_mux)
#define AUTOMATION_UNLOCK() taskEXIT_CRITICAL(&automation_mux)
#else
#define AUTOMATION_LOCK() ((void)0)
#define AUTOMATION_UNLOCK() ((void)0)
#endif
static char automation_report[96]="1,,0,0,0,0",automation_id[33];
static char automation_recent[64][33];static unsigned automation_recent_next;
static lv_indev_t *automation_input;
static bool automation_pressed;
static uint32_t automation_start,automation_ttl,automation_duration;
static int automation_width,automation_height,automation_rotation,automation_x,automation_y,automation_tx,automation_ty;
static unsigned automation_host;
void tab5_ui_automation_status(char *out,unsigned size){
    AUTOMATION_LOCK();snprintf(out,size,"%s",automation_report);AUTOMATION_UNLOCK();
}
static void automation_result(int status){
    AUTOMATION_LOCK();
    snprintf(automation_report,sizeof(automation_report),"1,%s,%d,%d,%d,%d",automation_id,status,
        (int)lv_display_get_horizontal_resolution(NULL),(int)lv_display_get_vertical_resolution(NULL),(int)lv_display_get_rotation(NULL));
    AUTOMATION_UNLOCK();
}
static bool automation_physical_touch(void){
    for(lv_indev_t *i=lv_indev_get_next(NULL);i;i=lv_indev_get_next(i))
        if(i!=automation_input&&lv_indev_get_type(i)==LV_INDEV_TYPE_POINTER&&lv_indev_get_state(i)==LV_INDEV_STATE_PRESSED)return true;
    return false;
}
// Invert the exact LVGL pointer rotation, using logical screenshot coordinates.
static lv_point_t automation_raw(int x,int y,int w,int h,int rotation){
    if(rotation==1)return (lv_point_t){y,w-x-1};
    if(rotation==2)return (lv_point_t){w-x-1,h-y-1};
    if(rotation==3)return (lv_point_t){h-y-1,x};
    return (lv_point_t){x,y};
}
static void automation_read(lv_indev_t *input,lv_indev_data_t *data){
    uint32_t elapsed=lv_tick_elaps(automation_start);
    bool invalid=automation_host!=device_host_epoch()||elapsed>automation_ttl||ota_update_busy()||photo_flow||automation_physical_touch()||
        automation_width!=lv_display_get_horizontal_resolution(NULL)||automation_height!=lv_display_get_vertical_resolution(NULL)||automation_rotation!=(int)lv_display_get_rotation(NULL);
    if(automation_pressed&&invalid){
        automation_pressed=false;lv_indev_reset(input,NULL);lv_indev_wait_release(input);automation_result(4);
    }
    uint32_t progress=elapsed<automation_duration?elapsed:automation_duration;
    int x=automation_x+(automation_tx-automation_x)*(int)progress/(int)automation_duration;
    int y=automation_y+(automation_ty-automation_y)*(int)progress/(int)automation_duration;
    data->point=automation_raw(x,y,automation_width,automation_height,automation_rotation);
    data->state=automation_pressed&&elapsed<automation_duration?LV_INDEV_STATE_PRESSED:LV_INDEV_STATE_RELEASED;
    if(automation_pressed&&elapsed>=automation_duration){automation_pressed=false;automation_result(2);}
}
static bool automation_hex(const char *s){if(!s||strlen(s)!=32)return false;for(unsigned i=0;i<32;i++)if(!((s[i]>='0'&&s[i]<='9')||(s[i]>='a'&&s[i]<='f')||(s[i]>='A'&&s[i]<='F')))return false;return true;}
static bool automation_integer(cJSON *o,const char *key,int low,int high,int *out){
    cJSON *v=item(o,key);if(!cJSON_IsNumber(v)||!isfinite(v->valuedouble)||v->valuedouble<low||v->valuedouble>high||v->valuedouble!=v->valueint)return false;
    *out=v->valueint;return true;
}
static void ui_automation_tick(cJSON *frame,link_kind link,int64_t now,int64_t received){
    cJSON *command=item(item(frame,"connectionHealth"),"uiAutomationCommand");
    const char *id=text(command,"requestId"),*session=text(item(frame,"pcInput"),"session");
    cJSON *clock=item(frame,"epochMilliseconds"),*expires=item(command,"expiresAt");int version;
    if(link!=LINK_USB||strcmp(text(command,"transport"),"USB")||!automation_hex(id)||!automation_hex(session)||
       !automation_integer(command,"version",1,1,&version)||now<received||now-received>=5000||
       !cJSON_IsNumber(clock)||!isfinite(clock->valuedouble)||clock->valuedouble<=0||
       !cJSON_IsNumber(expires)||!isfinite(expires->valuedouble)||expires->valuedouble>9007199254740991.0||
       expires->valuedouble<clock->valuedouble+now-received||expires->valuedouble>clock->valuedouble+5000)return;
    for(unsigned i=0;i<64;i++)if(!strcmp(id,automation_recent[i]))return;
    if(automation_pressed)return;
    snprintf(automation_id,sizeof(automation_id),"%s",id);
    snprintf(automation_recent[automation_recent_next++%64],33,"%s",id);
    if(!strcmp(text(command,"op"),"probe")){automation_result(2);return;}
    int w,h,r,x,y,tx,ty,duration;bool swipe=!strcmp(text(command,"op"),"swipe");
    if((!swipe&&strcmp(text(command,"op"),"tap"))||ota_update_busy()||photo_flow||automation_physical_touch()||
       !automation_integer(command,"width",1,1280,&w)||!automation_integer(command,"height",1,1280,&h)||
       !automation_integer(command,"rotation",0,3,&r)||w!=lv_display_get_horizontal_resolution(NULL)||h!=lv_display_get_vertical_resolution(NULL)||r!=(int)lv_display_get_rotation(NULL)||
       !automation_integer(command,"x",0,w-1,&x)||!automation_integer(command,"y",0,h-1,&y)||
       !automation_integer(command,"toX",0,w-1,&tx)||!automation_integer(command,"toY",0,h-1,&ty)||
       !automation_integer(command,"durationMs",swipe?100:80,swipe?1200:80,&duration)||(!swipe&&(x!=tx||y!=ty))){automation_result(3);return;}
    if(!automation_input){
        automation_input=lv_indev_create();
        if(!automation_input){automation_result(3);return;}
        lv_indev_set_type(automation_input,LV_INDEV_TYPE_POINTER);lv_indev_set_read_cb(automation_input,automation_read);
        lv_timer_set_period(lv_indev_get_read_timer(automation_input),16);
    }
    automation_width=w;automation_height=h;automation_rotation=r;automation_x=x;automation_y=y;automation_tx=tx;automation_ty=ty;
    automation_duration=(uint32_t)duration;automation_ttl=(uint32_t)(expires->valuedouble-clock->valuedouble-(now-received));
    automation_host=device_host_epoch();automation_start=lv_tick_get();automation_pressed=true;automation_result(1);
}
