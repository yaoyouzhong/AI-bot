#include "agenda.h"
#include "agenda_storage.h"
#ifdef TAB5_AGENDA_TEST
#include "agenda_runtime_stubs.h"
#else
#include "device.h"
#include "rpc_link.h"
#include "gallery_clock.h"
#include "gallery_clock_codec.h"
#include "esp_http_client.h"
#include "esp_timer.h"
#include "freertos/queue.h"
#include "nvs.h"
#include "esp_random.h"
#include "esp_heap_caps.h"
#endif
#include <stdio.h>
#include <string.h>
#include <stdlib.h>

typedef struct {char id[33],edit[33];int64_t occurrence,until;bool complete,save,remove;uint32_t request;agenda_item definition;} command;
static SemaphoreHandle_t guard;
static QueueHandle_t commands;
static char wanted[33],notice_title[97],notice_id[33];
static int64_t notice_occurrence,last_sync;
static bool notice_pending;
static agenda_workday_calendar saved_workdays;
static uint32_t command_sequence;
// One atomic snapshot includes active items and durable deletion receipts.
// Reuse the existing key to avoid keeping two large live blobs in 24 KiB NVS.
// Read the original exact-size document; upgrade only on a successful commit.
typedef struct {uint32_t version;agenda_document document;agenda_deletions deleted;} agenda_local;
// CPU-only snapshots belong in PSRAM. Keep internal RAM available for camera
// driver startup; never fall back to internal allocation for these documents.
typedef struct {agenda_state state;agenda_document candidate;agenda_deletions deleted;agenda_local local_buffer;} agenda_memory;
static agenda_memory *memory;
static bool workday_cache_store(const agenda_workday_calendar *value) {
    nvs_handle_t h;if(nvs_open("aibot_tab5",NVS_READWRITE,&h)!=ESP_OK)return false;
    esp_err_t result=nvs_set_blob(h,"agenda_days_v1",value,sizeof(*value));if(result==ESP_OK)result=nvs_commit(h);nvs_close(h);
    if(result!=ESP_OK)return false;
    saved_workdays=*value;agenda_workdays_set(value);return true;
}
static void workday_cache_load(void) {
    nvs_handle_t h;if(nvs_open("aibot_tab5",NVS_READONLY,&h)!=ESP_OK)return;
    agenda_workday_calendar value={0};size_t size=sizeof(value);esp_err_t result=nvs_get_blob(h,"agenda_days_v1",&value,&size);nvs_close(h);
    if(result==ESP_OK&&size==sizeof(value)&&agenda_workdays_valid(&value)){saved_workdays=value;agenda_workdays_set(&value);}
}
static bool persist_local(const agenda_document *v,const agenda_deletions *removals) {
    nvs_handle_t h;if(nvs_open("aibot_tab5",NVS_READWRITE,&h)!=ESP_OK)return false;
    size_t size=agenda_storage_encode(v,removals,(uint8_t*)&memory->local_buffer,AGENDA_STORAGE_MAX);
    esp_err_t result=size?nvs_set_blob(h,"agenda_v3",&memory->local_buffer,size):ESP_ERR_INVALID_ARG;if(result==ESP_OK)result=nvs_commit(h);nvs_close(h);return result==ESP_OK;
}
static bool persist(const agenda_document *v){return persist_local(v,&memory->deleted);}
static void message(const char *text){xSemaphoreTake(guard,portMAX_DELAY);snprintf(memory->state.message,sizeof(memory->state.message),"%s",text);memory->state.generation++;xSemaphoreGive(guard);}
static void publish(const agenda_document *v,const char *text) {
    char description[160];xSemaphoreTake(guard,portMAX_DELAY);snprintf(description,sizeof(description),"%s",text);
    memory->state.document=*v;memory->state.ready=true;memory->state.pending=memory->deleted.count!=0;for(unsigned i=0;i<v->count;i++)memory->state.pending|=v->items[i].dirty!=0;
    snprintf(memory->state.message,sizeof(memory->state.message),"%s",description);memory->state.generation++;xSemaphoreGive(guard);
}
void agenda_get(agenda_state *out) {
    if(!guard){memset(out,0,sizeof(*out));strcpy(out->message,"日程服务未启动");return;}
    xSemaphoreTake(guard,portMAX_DELAY);*out=memory->state;xSemaphoreGive(guard);
}
void agenda_offer(const char *revision) {
    if(!guard||!revision||strlen(revision)!=32)return;
    xSemaphoreTake(guard,portMAX_DELAY);snprintf(wanted,sizeof(wanted),"%s",revision);xSemaphoreGive(guard);
}
uint32_t agenda_action(const char *id,const char *edit,int64_t occurrence,bool complete,int64_t until) {
    if(!commands||!id||!edit||strlen(id)!=32||strlen(edit)!=32)return false;
    command c={.occurrence=occurrence,.until=until,.complete=complete};strcpy(c.id,id);strcpy(c.edit,edit);
    xSemaphoreTake(guard,portMAX_DELAY);c.request=++command_sequence;if(!c.request)c.request=++command_sequence;xSemaphoreGive(guard);
    if(xQueueSend(commands,&c,0)!=pdPASS)return false;
    message("正在保存事项…");return c.request;
}
uint32_t agenda_save(const agenda_item *definition,const char *expected_edit) {
    if(!commands||!definition||!expected_edit||strlen(expected_edit)>32)return 0;
    xSemaphoreTake(guard,portMAX_DELAY);bool ready=memory->state.ready;xSemaphoreGive(guard);if(!ready)return 0;
    command c={.save=true,.definition=*definition};snprintf(c.edit,sizeof(c.edit),"%s",expected_edit);
    xSemaphoreTake(guard,portMAX_DELAY);c.request=++command_sequence;if(!c.request)c.request=++command_sequence;xSemaphoreGive(guard);
    if(xQueueSend(commands,&c,0)!=pdPASS)return 0;
    message("正在保存事项…");return c.request;
}
uint32_t agenda_remove(const char *id,const char *edit) {
    if(!commands||!id||!edit||strlen(id)!=32||strlen(edit)!=32)return 0;
    command c={.remove=true};strcpy(c.id,id);strcpy(c.edit,edit);
    xSemaphoreTake(guard,portMAX_DELAY);c.request=++command_sequence;if(!c.request)c.request=++command_sequence;xSemaphoreGive(guard);
    if(xQueueSend(commands,&c,0)!=pdPASS)return 0;
    message("正在删除事项…");return c.request;
}
static void action_finish(const command *c,bool success,const char *text) {
    xSemaphoreTake(guard,portMAX_DELAY);memory->state.completed_request=c->request;memory->state.last_success=success;snprintf(memory->state.message,sizeof(memory->state.message),"%s",text);memory->state.generation++;xSemaphoreGive(guard);
}
bool agenda_alert(int64_t now,char title[97],char id[33],int64_t *occurrence) {
    (void)now;if(!guard)return false;
    xSemaphoreTake(guard,portMAX_DELAY);bool result=notice_pending;
    if(result){strcpy(title,notice_title);strcpy(id,notice_id);*occurrence=notice_occurrence;notice_pending=false;}
    xSemaphoreGive(guard);return result;
}
static void action_run(const command *c) {
    memory->candidate=memory->state.document;
    for(unsigned i=0;i<memory->candidate.count;i++) {
        agenda_item *item=&memory->candidate.items[i];if(strcmp(c->id,item->id)||strcmp(c->edit,item->edit))continue;
        if(!c->complete&&c->until>=agenda_ends_at(item,c->occurrence)){action_finish(c,false,"延后提醒不能达到或晚于结束时间");return;}
        if(!agenda_apply(item,c->occurrence,c->complete,c->until)){action_finish(c,false,"事项已变化，请重新选择");return;}
        if(!persist(&memory->candidate)){action_finish(c,false,"保存失败，请检查设备存储后重试");return;}
        const char *text=c->complete?"已完成本次，连接后同步电脑":"已保存，10 分钟后提醒";publish(&memory->candidate,text);action_finish(c,true,text);return;
    }
    action_finish(c,false,"事项已修改或移除，请重新选择");
}
static void random_id(char out[33]) {uint8_t bytes[16];esp_fill_random(bytes,sizeof(bytes));for(unsigned i=0;i<16;i++)snprintf(out+i*2,3,"%02x",bytes[i]);}
static int64_t current_civil_time(void) {
    struct tm local;if(!gallery_clock_local(esp_timer_get_time()/1000,&local))return 0;
    return (int64_t)(countdown_civil_day(local.tm_year+1900,local.tm_mon+1,local.tm_mday)-countdown_civil_day(1970,1,1))*86400+local.tm_hour*3600+local.tm_min*60+local.tm_sec;
}
static void save_run(const command *c) {
    memory->candidate=memory->state.document;unsigned index=memory->candidate.count;agenda_item value=c->definition;
    if(value.id[0])for(unsigned n=0;n<memory->candidate.count;n++)if(!strcmp(value.id,memory->candidate.items[n].id)){index=n;break;}
    const agenda_item *old=index<memory->candidate.count?&memory->candidate.items[index]:NULL;
    if((value.id[0]&&!old)||(old&&strcmp(old->edit,c->edit))||(!old&&c->edit[0])){action_finish(c,false,"事项已变化，请重新打开编辑");return;}
    agenda_deletions next=memory->deleted;bool reclaimed=false;
    if(!old&&memory->candidate.count==AGENDA_LIMIT){
        int victim=agenda_oldest_expired(&memory->candidate,current_civil_time());
        if(victim<0){action_finish(c,false,"已满 16 条，没有可清理的过期事项，请先删除");return;}
        if(next.count==AGENDA_LIMIT){action_finish(c,false,"待同步删除已满，请连接电脑同步后再新增");return;}
        // Persist eviction and creation in one transaction, with the same
        // durable receipt used by manual deletion to prevent resurrection.
        const agenda_item *removed=&memory->candidate.items[victim];agenda_deletion *receipt=&next.items[next.count++];
        strcpy(receipt->id,removed->id);strcpy(receipt->edit,removed->edit);strcpy(receipt->base_edit,removed->base_edit);
        index=(unsigned)victim;reclaimed=true;
    }
    if(!old)random_id(value.id);
    snprintf(value.base_edit,sizeof(value.base_edit),"%s",old?(old->dirty&4?old->base_edit:old->edit):"");random_id(value.edit);
    value.done=value.snooze_occurrence=value.snooze_until=value.alerted=0;value.dirty=4;
    memory->candidate.items[index]=value;if(!old&&!reclaimed)memory->candidate.count++;
    if(!agenda_document_valid(&memory->candidate)||(value.due&&!value.end)){action_finish(c,false,"请检查名称及开始、结束时间");return;}
    if(!old&&agenda_expired(&value,current_civil_time())){action_finish(c,false,"结束时间已过，请选择未来时间");return;}
    if(!persist_local(&memory->candidate,&next)){action_finish(c,false,"保存失败，原事项和输入内容已保留，请重试");return;}
    memory->deleted=next;const char *text=reclaimed?"已保存，并清理 1 条最早过期事项":"已保存到本机，连接后同步电脑";
    publish(&memory->candidate,text);xSemaphoreTake(guard,portMAX_DELAY);strcpy(memory->state.saved_id,value.id);xSemaphoreGive(guard);action_finish(c,true,text);
}
static cJSON *agenda_rpc(link_kind link,cJSON *request,int *status,char nonce[33]) {
    if(link!=LINK_WIFI)return rpc_call(link,request,status,nonce);
    device_config cfg=device_get_config();char url[96];snprintf(url,sizeof(url),"http://%s:%d/tab5/v1/rpc",cfg.host,cfg.port);
    esp_http_client_config_t opts={.url=url,.method=HTTP_METHOD_POST,.timeout_ms=5000,.disable_auto_redirect=true,.buffer_size=4096};
    esp_http_client_handle_t http=esp_http_client_init(&opts);if(!http)return NULL;
    cJSON *reply=rpc_http_call(http,request,status,nonce);esp_http_client_cleanup(http);return reply;
}
static void remove_run(const command *c) {
    unsigned index=memory->state.document.count;for(unsigned n=0;n<memory->state.document.count;n++)if(!strcmp(memory->state.document.items[n].id,c->id)&&!strcmp(memory->state.document.items[n].edit,c->edit)){index=n;break;}
    if(index==memory->state.document.count){action_finish(c,false,"事项已变化，请重新选择后删除");return;}
    if(memory->deleted.count==AGENDA_LIMIT){action_finish(c,false,"待同步删除已满，请连接电脑同步后重试");return;}
    agenda_deletions next=memory->deleted;agenda_deletion *removal=&next.items[next.count++];const agenda_item *item=&memory->state.document.items[index];
    strcpy(removal->id,item->id);strcpy(removal->edit,item->edit);strcpy(removal->base_edit,item->base_edit);
    memory->candidate=memory->state.document;memmove(&memory->candidate.items[index],&memory->candidate.items[index+1],(memory->candidate.count-index-1)*sizeof(memory->candidate.items[0]));memset(&memory->candidate.items[--memory->candidate.count],0,sizeof(memory->candidate.items[0]));
    if(!persist_local(&memory->candidate,&next)){action_finish(c,false,"删除保存失败，原事项仍保留，请重试");return;}
    memory->deleted=next;if(notice_pending&&!strcmp(notice_id,c->id))notice_pending=false;
    publish(&memory->candidate,"已从 TAB5 删除，连接后同步电脑");action_finish(c,true,"已从 TAB5 删除，连接后同步电脑");
}
static void sync_run(link_kind link) {
    cJSON *request=cJSON_CreateObject(),*actions=cJSON_AddArrayToObject(request,"actions"),*edits=cJSON_AddArrayToObject(request,"edits");
    cJSON_AddStringToObject(request,"kind","agenda");cJSON_AddStringToObject(request,"op","sync");
    cJSON_AddNumberToObject(request,"workdaySupport",1);
    cJSON_AddNumberToObject(request,"retentionSupport",2);
    cJSON *removals=cJSON_AddArrayToObject(request,"deletions");
    for(unsigned n=0;n<memory->deleted.count;n++) {
        const agenda_deletion *d=&memory->deleted.items[n];cJSON *o=cJSON_CreateObject();cJSON_AddItemToArray(removals,o);
        cJSON_AddStringToObject(o,"id",d->id);cJSON_AddStringToObject(o,"edit",d->edit);cJSON_AddStringToObject(o,"baseEdit",d->base_edit);
    }
    for(unsigned n=0;n<memory->state.document.count;n++) {
        const agenda_item *i=&memory->state.document.items[n];if(i->dirty&4){cJSON *change=cJSON_CreateObject();cJSON_AddItemToArray(edits,change);cJSON_AddStringToObject(change,"baseEdit",i->base_edit);cJSON_AddItemToObject(change,"item",agenda_item_json(i));}
        unsigned dirty=i->dirty&3;if(!dirty)continue;
        if(dirty==2&&i->done>0) {
            cJSON *completed=cJSON_CreateObject();cJSON_AddItemToArray(actions,completed);cJSON_AddStringToObject(completed,"id",i->id);cJSON_AddStringToObject(completed,"edit",i->edit);
            cJSON_AddStringToObject(completed,"op","complete");cJSON_AddNumberToObject(completed,"occurrence",i->done);
        }
        cJSON *a=cJSON_CreateObject();cJSON_AddItemToArray(actions,a);cJSON_AddStringToObject(a,"id",i->id);cJSON_AddStringToObject(a,"edit",i->edit);
        cJSON_AddStringToObject(a,"op",dirty==1?"complete":"snooze");cJSON_AddNumberToObject(a,"occurrence",dirty==1?i->done:i->snooze_occurrence);cJSON_AddNumberToObject(a,"until",dirty==2?i->snooze_until:0);
    }
    xSemaphoreTake(guard,portMAX_DELAY);memory->state.busy=true;xSemaphoreGive(guard);
    int status=0;char nonce[33];cJSON *reply=agenda_rpc(link,request,&status,nonce);
    const cJSON *edit_support=cJSON_GetObjectItemCaseSensitive(reply,"editSupport");
    const cJSON *retention=cJSON_GetObjectItemCaseSensitive(reply,"retentionSupport");
    if(status==200&&(!cJSON_IsNumber(retention)||retention->valueint!=2)){
        message("本机事项已保留，请更新电脑桥接后同步");cJSON_Delete(reply);cJSON_Delete(request);
        xSemaphoreTake(guard,portMAX_DELAY);memory->state.busy=false;xSemaphoreGive(guard);return;
    }
    bool schedule_ok=true;
    const cJSON *years=cJSON_GetObjectItemCaseSensitive(reply,"workdayYears");
    if(status==200&&years) {
        agenda_workday_calendar value={0};schedule_ok=agenda_workdays_parse(years,&value);
        if(schedule_ok&&value.count&&memcmp(&value,&saved_workdays,sizeof(value)))schedule_ok=workday_cache_store(&value);
    }
    // Calendar refresh is independent from the acknowledged item snapshot.
    // A failed annual cache update keeps its last good data; unknown workday
    // years already pause their own reminders instead of blocking all edits.
    bool valid=status==200&&reply&&(!cJSON_GetArraySize(edits)||(cJSON_IsNumber(edit_support)&&edit_support->valueint==1))&&agenda_parse(reply,&memory->candidate);
    const cJSON *delete_support=cJSON_GetObjectItemCaseSensitive(reply,"deleteSupport"),*receipts=cJSON_GetObjectItemCaseSensitive(reply,"deleted");
    for(unsigned n=0;valid&&n<memory->deleted.count;n++) {
        const agenda_deletion *d=&memory->deleted.items[n];bool acknowledged=false;const cJSON *receipt=NULL;
        cJSON_ArrayForEach(receipt,receipts) {
            const cJSON *id=cJSON_GetObjectItemCaseSensitive(receipt,"id"),*edit=cJSON_GetObjectItemCaseSensitive(receipt,"edit");
            if(cJSON_IsString(id)&&cJSON_IsString(edit)&&!strcmp(id->valuestring,d->id)&&!strcmp(edit->valuestring,d->edit))acknowledged=true;
        }
        valid=cJSON_IsNumber(delete_support)&&delete_support->valueint==1&&acknowledged;
        for(unsigned j=0;valid&&j<memory->candidate.count;j++)if(!strcmp(memory->candidate.items[j].id,d->id)||(!strcmp(memory->candidate.items[j].id,d->edit)&&!strcmp(memory->candidate.items[j].edit,d->edit)))valid=false;
    }
    for(unsigned n=0;valid&&n<memory->state.document.count;n++) {
        const agenda_item *old=&memory->state.document.items[n];if(!(old->dirty&4))continue;bool acknowledged=false;
        for(unsigned j=0;j<memory->candidate.count;j++){const agenda_item *i=&memory->candidate.items[j];acknowledged|=(!strcmp(old->id,i->id)||!strcmp(old->edit,i->id))&&!strcmp(old->edit,i->edit)&&!strcmp(old->title,i->title)&&old->due==i->due&&old->end==i->end&&old->repeat==i->repeat&&old->lead==i->lead;}
        valid=acknowledged;
    }
    if(valid) {
        // Only this worker mutates state. Commands arriving during RPC stay in
        // the queue and will be validated against the newly persisted edition.
        for(unsigned n=0;n<memory->candidate.count;n++)for(unsigned j=0;j<memory->state.document.count;j++) {
            const agenda_item *old=&memory->state.document.items[j];agenda_item *i=&memory->candidate.items[n];
            if((!strcmp(old->id,i->id)||((old->dirty&4)&&!strcmp(old->edit,i->id)))&&!strcmp(old->edit,i->edit))i->alerted=old->alerted;
        }
        bool changed=memory->deleted.count||memcmp(&memory->candidate,&memory->state.document,sizeof(memory->candidate))!=0;agenda_deletions empty={0};
        if(!changed||persist_local(&memory->candidate,&empty)){memory->deleted=empty;publish(&memory->candidate,cJSON_GetArraySize(cJSON_GetObjectItemCaseSensitive(reply,"forks"))?"两端均有修改，已保留修改副本":cJSON_GetArraySize(cJSON_GetObjectItemCaseSensitive(reply,"conflicts"))?"事项已在电脑修改，旧操作未应用":!schedule_ok?"事项已同步，工作日历更新失败，将自动重试":"已同步，可离线提醒");}
        else message("同步未保存，保留原事项；请检查设备存储");
    }else message(status==409?"电脑列表已满或有冲突，本机操作已保存":status==400||(memory->deleted.count&&(!cJSON_IsNumber(delete_support)||delete_support->valueint!=1))?"本机操作已保存，请更新电脑桥接后同步":"本机操作已保存，连接后自动同步");
    cJSON_Delete(reply);cJSON_Delete(request);
    xSemaphoreTake(guard,portMAX_DELAY);memory->state.busy=false;xSemaphoreGive(guard);
}
static void alert_run(void) {
    struct tm local;int64_t ms=esp_timer_get_time()/1000;if(!gallery_clock_local(ms,&local))return;
    int64_t now=(int64_t)(countdown_civil_day(local.tm_year+1900,local.tm_mon+1,local.tm_mday)-countdown_civil_day(1970,1,1))*86400+local.tm_hour*3600+local.tm_min*60+local.tm_sec;
    if(now<=0)return;
    for(unsigned n=0;n<memory->state.document.count;n++) {
        const agenda_item *i=&memory->state.document.items[n];agenda_view v=agenda_view_at(i,now);
        if(!i->due||v.complete||v.calendar_pending)continue;
        // A long-running occurrence may overlap the next occurrence's lead reminder.
        if(i->alerted>=v.alert_at&&i->repeat)v=agenda_view_at(i,agenda_ends_at(i,v.occurrence));
        if(v.complete||v.calendar_pending||v.alert_at>now||i->alerted>=v.alert_at)continue;
        xSemaphoreTake(guard,portMAX_DELAY);bool occupied=notice_pending;xSemaphoreGive(guard);if(occupied)return;
        memory->candidate=memory->state.document;memory->candidate.items[n].alerted=v.alert_at;
        if(!persist(&memory->candidate)){message("提醒状态保存失败，请检查设备存储");return;}
        publish(&memory->candidate,memory->state.message);
        xSemaphoreTake(guard,portMAX_DELAY);strcpy(notice_title,i->title);strcpy(notice_id,i->id);notice_occurrence=v.occurrence;notice_pending=true;xSemaphoreGive(guard);return;
    }
}
static bool load_document(void) {
    nvs_handle_t h;bool loaded=false,compact=false;esp_err_t result=nvs_open("aibot_tab5",NVS_READONLY,&h);
    memset(&memory->deleted,0,sizeof(memory->deleted));
    if(result==ESP_OK){
        size_t size=sizeof(memory->local_buffer);result=nvs_get_blob(h,"agenda_v3",&memory->local_buffer,&size);
        if(result==ESP_OK) {
            const uint8_t *bytes=(const uint8_t*)&memory->local_buffer;
            if(size>=4&&!memcmp(bytes,"AGL",3)){loaded=agenda_storage_decode(bytes,size,&memory->candidate,&memory->deleted);compact=loaded;}
            else if(size==sizeof(memory->candidate)){memcpy(&memory->candidate,&memory->local_buffer,size);loaded=agenda_document_valid(&memory->candidate);}
            else {
                loaded=size==sizeof(memory->local_buffer)&&memory->local_buffer.version==1&&agenda_document_valid(&memory->local_buffer.document)&&memory->local_buffer.deleted.count<=AGENDA_LIMIT;
                for(unsigned n=0;loaded&&n<memory->local_buffer.deleted.count;n++) {
                    const agenda_deletion *d=&memory->local_buffer.deleted.items[n];loaded=d->id[32]==0&&d->edit[32]==0&&d->base_edit[32]==0;
                    if(loaded)loaded=strlen(d->id)==32&&strspn(d->id,"0123456789abcdef")==32&&strlen(d->edit)==32&&strspn(d->edit,"0123456789abcdef")==32&&(!d->base_edit[0]||(strlen(d->base_edit)==32&&strspn(d->base_edit,"0123456789abcdef")==32));
                    for(unsigned j=0;loaded&&j<n;j++)loaded=strcmp(d->id,memory->local_buffer.deleted.items[j].id)!=0;
                    for(unsigned j=0;loaded&&j<memory->local_buffer.document.count;j++)loaded=strcmp(d->id,memory->local_buffer.document.items[j].id)!=0;
                }
                if(loaded){memory->candidate=memory->local_buffer.document;memory->deleted=memory->local_buffer.deleted;}
            }
        }
        nvs_close(h);
    }
    if(loaded) {
        // Reclaim the old fixed-size snapshot at boot, before unrelated settings
        // need NVS headroom. A failed migration still exposes the intact old data.
        bool stored=compact||persist_local(&memory->candidate,&memory->deleted);
        publish(&memory->candidate,stored?"已载入，可离线提醒":"已载入，存储整理失败，原数据已保留");
    }
    else if(result!=ESP_ERR_NVS_NOT_FOUND){message("日程存储无法读取，原数据已保留");return false;}
    else {memset(&memory->candidate,0,sizeof(memory->candidate));memory->candidate.version=1;memset(memory->candidate.revision,'0',32);publish(&memory->candidate,"点新建添加事项");}
    return true;
}
static void worker(void *arg) {
    (void)arg;workday_cache_load();if(!load_document()){vTaskDelete(NULL);return;}
    for(;;) {
        command c;while(xQueueReceive(commands,&c,0)==pdPASS){if(c.save)save_run(&c);else if(c.remove)remove_run(&c);else action_run(&c);}
        alert_run();int64_t now=esp_timer_get_time()/1000;link_kind link=rpc_link_select();
        xSemaphoreTake(guard,portMAX_DELAY);bool changed=wanted[0]&&strcmp(wanted,memory->state.document.revision);bool pending=memory->state.pending;xSemaphoreGive(guard);
        if(link!=LINK_NONE&&now-last_sync>=5000&&(changed||pending||now-last_sync>=60000)) {
            last_sync=now;sync_run(link);
        }
        vTaskDelay(pdMS_TO_TICKS(200));
    }
}
void agenda_start(void) {
    if(guard)return;
    memory=heap_caps_calloc(1,sizeof(*memory),MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    if(!memory)return;
    SemaphoreHandle_t mutex=xSemaphoreCreateMutex();
    QueueHandle_t queue=mutex?xQueueCreate(8,sizeof(command)):0;
    if(!queue){if(mutex)vSemaphoreDelete(mutex);free(memory);memory=NULL;return;}
    commands=queue;guard=mutex;
    if(xTaskCreate(worker,"agenda",8192,NULL,2,NULL)!=pdPASS){
        vQueueDelete(commands);commands=0;message("日程服务启动失败");
    }
}
