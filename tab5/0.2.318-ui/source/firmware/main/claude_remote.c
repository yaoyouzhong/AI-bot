#include "claude_remote.h"
#include "device.h"
#include "rpc_link.h"
#include "esp_http_client.h"
#include "esp_heap_caps.h"
#include <stdlib.h>
#include <string.h>
#include <stdio.h>
#include <ctype.h>

typedef struct {char binding[33],revision[65];int page;unsigned epoch,host;link_kind link;} claude_read_job;
static claude_remote_state *state;
static unsigned epoch;
static bool hex(const char *s,unsigned n){if(!s||strlen(s)!=n)return false;for(unsigned i=0;i<n;i++)if(!isxdigit((unsigned char)s[i]))return false;return true;}
static const char *text(cJSON *o,const char *key){cJSON *v=cJSON_GetObjectItemCaseSensitive(o,key);return cJSON_IsString(v)?v->valuestring:"";}
void claude_remote_get(claude_remote_state *out){xSemaphoreTake(device_mutex,portMAX_DELAY);if(state)*out=*state;else memset(out,0,sizeof(*out));xSemaphoreGive(device_mutex);}
void claude_remote_cancel(void){xSemaphoreTake(device_mutex,portMAX_DELAY);epoch++;if(state){unsigned g=state->generation+1;memset(state,0,sizeof(*state));state->generation=g;}xSemaphoreGive(device_mutex);}
static void run(void *arg){
    claude_read_job *job=arg;int status=0;char nonce[33];cJSON *request=cJSON_CreateObject(),*reply=NULL;
    if(request){
        cJSON_AddStringToObject(request,"kind","claude");cJSON_AddStringToObject(request,"op","read");
        cJSON_AddStringToObject(request,"binding",job->binding);cJSON_AddStringToObject(request,"revision",job->revision);cJSON_AddNumberToObject(request,"page",job->page);
        if(job->link==LINK_WIFI){
            device_config cfg=device_get_config();char url[96];snprintf(url,sizeof(url),"http://%s:%d/tab5/v1/rpc",cfg.host,cfg.port);
            esp_http_client_config_t options={.url=url,.method=HTTP_METHOD_POST,.timeout_ms=10000,.disable_auto_redirect=true,.buffer_size=2048};
            esp_http_client_handle_t client=esp_http_client_init(&options);
            if(client){reply=rpc_http_call(client,request,&status,nonce);esp_http_client_cleanup(client);}
        }else reply=rpc_call(job->link,request,&status,nonce);
    }
    cJSON *version=cJSON_GetObjectItemCaseSensitive(reply,"version"),*page=cJSON_GetObjectItemCaseSensitive(reply,"page"),*total=cJSON_GetObjectItemCaseSensitive(reply,"total");
    bool valid=status==200&&cJSON_IsNumber(version)&&version->valueint==1&&
        !strcmp(text(reply,"binding"),job->binding)&&hex(text(reply,"revision"),64)&&
        cJSON_IsNumber(page)&&page->valuedouble==page->valueint&&cJSON_IsNumber(total)&&total->valuedouble==total->valueint&&
        total->valueint>0&&total->valueint<=4096&&page->valueint>=0&&page->valueint<total->valueint&&
        (job->page<0||page->valueint==job->page)&&cJSON_IsString(cJSON_GetObjectItemCaseSensitive(reply,"title"))&&
        cJSON_IsString(cJSON_GetObjectItemCaseSensitive(reply,"text"))&&strlen(text(reply,"title"))<=512&&strlen(text(reply,"text"))<=1600;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    if(state&&job->epoch==epoch&&job->host==device_host_epoch()){
        state->busy=false;state->status=valid?200:status==200?502:status;state->generation++;
        if(valid){
            strcpy(state->binding,job->binding);strcpy(state->revision,text(reply,"revision"));strcpy(state->title,text(reply,"title"));strcpy(state->text,text(reply,"text"));
            state->page=page->valueint;state->total=total->valueint;state->has_earlier=cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(reply,"hasEarlier"));state->error[0]=0;
        }else{
            snprintf(state->error,sizeof(state->error),"%s",text(reply,"error"));
            if(status==403){state->text[0]=0;state->revision[0]=0;state->total=0;}
        }
    }
    xSemaphoreGive(device_mutex);cJSON_Delete(reply);cJSON_Delete(request);free(job);vTaskDelete(NULL);
}
bool claude_remote_start(const char *binding,const char *revision,int page){
    if(!hex(binding,32)||(revision&&revision[0]&&!hex(revision,64))||page< -1||page>4095)return false;
    link_kind link=rpc_read_link_select();if(link==LINK_NONE)return false;
    claude_read_job *job=calloc(1,sizeof(*job));if(!job)return false;
    strcpy(job->binding,binding);if(revision)strcpy(job->revision,revision);job->page=page;job->link=link;job->host=device_host_epoch();
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    if(!state)state=heap_caps_calloc(1,sizeof(*state),MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    if(!state||state->busy){xSemaphoreGive(device_mutex);free(job);return false;}
    if(strcmp(state->binding,binding)){memset(state,0,sizeof(*state));strcpy(state->binding,binding);}
    job->epoch=epoch;state->busy=true;state->generation++;xSemaphoreGive(device_mutex);
    if(xTaskCreate(run,"claude-read",6144,job,4,NULL)!=pdPASS){xSemaphoreTake(device_mutex,portMAX_DELAY);state->busy=false;state->status=503;state->generation++;xSemaphoreGive(device_mutex);free(job);return false;}
    return true;
}
