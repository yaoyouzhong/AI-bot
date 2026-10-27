#include "ota_remote.h"
#include "device.h"
#include "rpc_link.h"
#include "esp_http_client.h"
#include <stdatomic.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef struct {cJSON *report;link_kind link;char id[33];} ota_remote_job;
static atomic_bool sending,result_ready;
static char result_id[33];
static bool result_commit,result_confirmed;
static void ota_remote_send(void *arg) {
    ota_remote_job *job=arg;char nonce[33];int status=0;cJSON *reply=NULL;
    if(job->link!=LINK_WIFI)reply=rpc_call(job->link,job->report,&status,nonce);
    else {
        device_config cfg=device_get_config();char url[112];
        snprintf(url,sizeof(url),"http://%s:%d/tab5/v1/rpc",cfg.host,cfg.port);
        esp_http_client_config_t options={.url=url,.timeout_ms=6000,.disable_auto_redirect=true};
        esp_http_client_handle_t client=esp_http_client_init(&options);
        if(client){reply=rpc_http_call(client,job->report,&status,nonce);esp_http_client_cleanup(client);}
    }
    snprintf(result_id,sizeof(result_id),"%s",job->id);
    result_commit=status==200&&cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(reply,"commit"));
    result_confirmed=result_commit||(status==200&&cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(reply,"accepted")))||status==409;
    cJSON_Delete(reply);cJSON_Delete(job->report);free(job);
    atomic_store_explicit(&result_ready,true,memory_order_release);
    atomic_store(&sending,false);vTaskDelete(NULL);
}
bool ota_remote_report(cJSON *report,link_kind link) {
    const char *id=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(report,"requestId"));
    if(!id||strlen(id)!=32||link<LINK_USB||link>LINK_BLE||atomic_load(&result_ready))return false;
    bool expected=false;if(!atomic_compare_exchange_strong(&sending,&expected,true))return false;
    ota_remote_job *job=malloc(sizeof(*job));if(!job){atomic_store(&sending,false);return false;}
    *job=(ota_remote_job){.report=report,.link=link};snprintf(job->id,sizeof(job->id),"%s",id);
    if(xTaskCreate(ota_remote_send,"ota_control",8192,job,3,NULL)!=pdPASS){free(job);atomic_store(&sending,false);return false;}
    return true;
}
bool ota_remote_result(char request[33],bool *commit,bool *confirmed) {
    if(!atomic_load_explicit(&result_ready,memory_order_acquire))return false;
    snprintf(request,33,"%s",result_id);*commit=result_commit;*confirmed=result_confirmed;
    atomic_store(&result_ready,false);return true;
}

#include "boot_verify_worker.inc"
