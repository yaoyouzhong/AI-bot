#include "desktop_remote.h"
#include "device.h"
#include "rpc_link.h"
#include "esp_http_client.h"
#include <stdio.h>
#include <string.h>
#include <stdlib.h>

typedef struct {char id[37],task_id[37],text[2001],draft_request[37];int operation;link_kind link;} desktop_job;
static desktop_remote_state state;
static const char *str(cJSON *object,const char *key) {
    cJSON *value=cJSON_GetObjectItemCaseSensitive(object,key);
    return cJSON_IsString(value)?value->valuestring:"";
}
void desktop_remote_get(desktop_remote_state *out) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);*out=state;xSemaphoreGive(device_mutex);
}
static void run(void *arg) {
    desktop_job *job=arg;int status=0;char nonce[33];
    cJSON *request=cJSON_CreateObject(),*reply=NULL;
    bool stage=job->operation==1,submit=job->operation==2,clear=job->operation==3;
    cJSON_AddStringToObject(request,"kind","desktop");cJSON_AddStringToObject(request,"op",clear?"clear-draft":submit?"submit-draft":stage?"stage-draft":"open-recent");
    if(stage)cJSON_AddStringToObject(request,"text",job->text);
    if(submit)cJSON_AddStringToObject(request,"draftRequestId",job->draft_request);
    cJSON_AddStringToObject(request,"requestId",job->id);
    if(job->task_id[0])cJSON_AddStringToObject(request,"taskId",job->task_id);
    if(job->link==LINK_WIFI) {
        device_config cfg=device_get_config();char url[96];
        snprintf(url,sizeof(url),"http://%s:%d/tab5/v1/rpc",cfg.host,cfg.port);
        esp_http_client_config_t options={.url=url,.method=HTTP_METHOD_POST,.timeout_ms=12000,.disable_auto_redirect=true,.buffer_size=4096};
        esp_http_client_handle_t client=esp_http_client_init(&options);
        if(client){reply=rpc_http_call(client,request,&status,nonce);esp_http_client_cleanup(client);}
    }else reply=rpc_call(job->link,request,&status,nonce);
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    state.status=status;
    const char *target=str(reply,"taskId");
    bool valid=status==200&&cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(reply,clear?"cleared":submit?"submitted":stage?"staged":"foreground"))&&strlen(target)==36&&
        (!job->task_id[0]||!strcmp(target,job->task_id));
    if(valid) {
        state.draft_empty=job->operation==0&&cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(reply,"draftEmpty"));
        state.appended=stage&&cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(reply,"appended"));
        snprintf(state.task_id,sizeof(state.task_id),"%s",str(reply,"taskId"));
        snprintf(state.project_id,sizeof(state.project_id),"%s",str(reply,"projectId"));
        snprintf(state.title,sizeof(state.title),"%s",str(reply,"title"));
        snprintf(state.folder,sizeof(state.folder),"%s",str(reply,"folder"));
        cJSON *updated=cJSON_GetObjectItemCaseSensitive(reply,"updatedAt");state.updated=cJSON_IsNumber(updated)?updated->valuedouble:0;
        snprintf(state.message,sizeof(state.message),"%s",clear?"输入框已清空，可重新录音":submit?"已成功发送Codex执行。":stage?"检查草稿，可继续语音补充；确认后点蓝色箭头发送":"已请求打开会话，Codex 已在前台");
    }else {
        state.status=status==200?502:status;
        const char *error=str(reply,"error");
        const char *stage_message=!strcmp(error,"composer_changed")?"电脑草稿正在变化或含引用，请检查；识别文字保留在预览":
            !strcmp(error,"composer_too_long")?"电脑草稿过长，请先整理；识别文字保留在预览":
            !strcmp(error,"composer_not_ready")?"电脑主输入框未就绪，识别文字保留在预览":
            "未确认追加，请检查电脑输入框；识别文字保留在预览";
        snprintf(state.message,sizeof(state.message),"%s",clear?"未确认清空，请检查电脑当前会话输入框":submit?"未确认发送，请检查电脑；不要重复发送，可重新录音":stage?stage_message:
            !strcmp(error,"desktop_not_interactive")?"电脑桌面不可操作，请解锁后再点 Codex":
            status==404?"没有可用的 Codex 会话":status==400?"请更新电脑桥接后重试":status==409?"未确认 Codex 前台，请在电脑检查后再点 Codex":"未确认打开结果，请检查电脑连接");
    }
    state.busy=false;xSemaphoreGive(device_mutex);
    cJSON_Delete(reply);cJSON_Delete(request);free(job);vTaskDelete(NULL);
}
static bool start(const char *id,const char *task_id,const char *text,const char *draft_request,int operation) {
    if(!id||strlen(id)!=36||(task_id&&strlen(task_id)!=36)||(text&&(!task_id||!text[0])))return false;
    if(text&&strlen(text)>2000)return false;
    if(draft_request&&(!task_id||strlen(draft_request)!=36))return false;
    if(operation==3&&!task_id)return false;
    link_kind link=rpc_link_select();if(link==LINK_NONE)return false;
    desktop_job *job=calloc(1,sizeof(*job));if(!job)return false;
    snprintf(job->id,sizeof(job->id),"%s",id);job->link=link;
    if(task_id)snprintf(job->task_id,sizeof(job->task_id),"%s",task_id);
    if(text)snprintf(job->text,sizeof(job->text),"%s",text);
    if(draft_request)snprintf(job->draft_request,sizeof(job->draft_request),"%s",draft_request);
    job->operation=operation;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    if(state.busy){xSemaphoreGive(device_mutex);free(job);return false;}
    memset(&state,0,sizeof(state));state.busy=true;state.status=-1;
    xSemaphoreGive(device_mutex);
    if(xTaskCreate(run,"desktop-open",6144,job,4,NULL)!=pdPASS) {
        xSemaphoreTake(device_mutex,portMAX_DELAY);state.busy=false;state.status=0;xSemaphoreGive(device_mutex);free(job);return false;
    }
    return true;
}
bool desktop_remote_open(const char *id,const char *task_id){return start(id,task_id,NULL,NULL,0);}
bool desktop_remote_latest(const char *id){return start(id,NULL,NULL,NULL,0);}
bool desktop_remote_stage(const char *id,const char *task_id,const char *text){return text&&text[0]&&start(id,task_id,text,NULL,1);}
bool desktop_remote_submit(const char *id,const char *task_id,const char *draft_request){return draft_request&&start(id,task_id,NULL,draft_request,2);}
bool desktop_remote_clear(const char *id,const char *task_id){return start(id,task_id,NULL,NULL,3);}
