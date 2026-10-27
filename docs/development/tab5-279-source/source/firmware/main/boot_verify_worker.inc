#include "esp_app_desc.h"
#include "esp_ota_ops.h"
#include "esp_timer.h"
extern unsigned tab5_ui_age_ms(void);
extern volatile unsigned tab5_display_flush_count,tab5_display_flush_errors;
typedef struct {char id[33],session[33];link_kind link;} boot_verify_job;
static atomic_bool boot_verify_sending;
static void boot_verify_send(void *argument) {
    boot_verify_job *job=argument;
    const esp_partition_t *running=esp_ota_get_running_partition();
    esp_ota_img_states_t state=ESP_OTA_IMG_UNDEFINED;
    esp_err_t error=running?esp_ota_get_state_partition(running,&state):ESP_ERR_NOT_FOUND;
    const esp_app_desc_t *description=esp_app_get_description();
    char hash[65],nonce[33];for(int i=0;i<32;i++)snprintf(hash+i*2,3,"%02x",description->app_elf_sha256[i]);
    device_config cfg=device_get_config();
    cJSON *report=cJSON_CreateObject(),*hello=cJSON_CreateObject(),*diagnostic=cJSON_CreateObject(),*reply=NULL;
    if(report&&hello&&diagnostic) {
        cJSON_AddStringToObject(report,"kind","boot-verify");cJSON_AddStringToObject(report,"requestId",job->id);
        cJSON_AddStringToObject(report,"session",job->session);
        cJSON_AddStringToObject(hello,"type","tab5_hello");cJSON_AddNumberToObject(hello,"version",1);
        cJSON_AddStringToObject(hello,"deviceId",cfg.id);cJSON_AddStringToObject(hello,"firmware",description->version);
        cJSON_AddNumberToObject(hello,"uptimeMs",esp_timer_get_time()/1000);
        cJSON_AddNumberToObject(hello,"uiAgeMs",tab5_ui_age_ms());
        cJSON_AddNumberToObject(hello,"flushCount",tab5_display_flush_count);cJSON_AddNumberToObject(hello,"flushErrors",tab5_display_flush_errors);
        cJSON_AddStringToObject(diagnostic,"type","tab5_ota_diagnostic");cJSON_AddStringToObject(diagnostic,"firmware",description->version);
        cJSON_AddStringToObject(diagnostic,"partition",running?running->label:"none");cJSON_AddNumberToObject(diagnostic,"address",running?running->address:0);
        cJSON_AddNumberToObject(diagnostic,"state",state);cJSON_AddNumberToObject(diagnostic,"stateError",error);cJSON_AddStringToObject(diagnostic,"elfSha256",hash);
        cJSON_AddItemToObject(report,"hello",hello);hello=NULL;cJSON_AddItemToObject(report,"diagnostic",diagnostic);diagnostic=NULL;
        int status=0;
        if(job->link==LINK_BLE) {
            int64_t began=esp_timer_get_time()/1000;
            for(unsigned attempt=0;attempt<16;attempt++) {
                if(attempt&&esp_timer_get_time()/1000-began>=6000)break;
                cJSON *sample=cJSON_GetObjectItemCaseSensitive(report,"hello");
                // The cJSON macro casts its operand before division; pass an integer value.
                int64_t uptime_ms=esp_timer_get_time()/1000;
                cJSON_SetNumberValue(cJSON_GetObjectItemCaseSensitive(sample,"uptimeMs"),uptime_ms);
                cJSON_SetNumberValue(cJSON_GetObjectItemCaseSensitive(sample,"uiAgeMs"),tab5_ui_age_ms());
                cJSON_SetNumberValue(cJSON_GetObjectItemCaseSensitive(sample,"flushCount"),tab5_display_flush_count);
                cJSON_SetNumberValue(cJSON_GetObjectItemCaseSensitive(sample,"flushErrors"),tab5_display_flush_errors);
                reply=rpc_call(LINK_BLE,report,&status,nonce);
                if(reply&&status==200)break;
                cJSON_Delete(reply);reply=NULL;
                if((status!=0&&status!=503)||attempt==15||esp_timer_get_time()/1000-began>=6000)break;
                vTaskDelay(pdMS_TO_TICKS(250));
            }
        }
        else if(job->link==LINK_WIFI) {
            char url[112];snprintf(url,sizeof(url),"http://%s:%d/tab5/v1/rpc",cfg.host,cfg.port);
            esp_http_client_config_t options={.url=url,.timeout_ms=6000,.disable_auto_redirect=true};
            esp_http_client_handle_t client=esp_http_client_init(&options);
            if(client){reply=rpc_http_call(client,report,&status,nonce);esp_http_client_cleanup(client);}
        }
    }
    cJSON_Delete(reply);cJSON_Delete(report);cJSON_Delete(hello);cJSON_Delete(diagnostic);free(job);
    atomic_store(&boot_verify_sending,false);vTaskDelete(NULL);
}
bool boot_verify_report(const char *id,const char *session,link_kind link) {
    if(!id||strlen(id)!=32||!session||strlen(session)!=32||(link!=LINK_WIFI&&link!=LINK_BLE))return false;
    bool expected=false;if(!atomic_compare_exchange_strong(&boot_verify_sending,&expected,true))return false;
    boot_verify_job *job=malloc(sizeof(*job));
    if(!job){atomic_store(&boot_verify_sending,false);return false;}
    snprintf(job->id,sizeof(job->id),"%s",id);snprintf(job->session,sizeof(job->session),"%s",session);job->link=link;
    if(xTaskCreate(boot_verify_send,"boot_verify",8192,job,3,NULL)!=pdPASS){free(job);atomic_store(&boot_verify_sending,false);return false;}
    return true;
}
