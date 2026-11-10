#include "runtime_diagnostics.h"
#include "device.h"
#include "assets.h"
#include "camera_preview.h"
#include "camera_viewport.h"
#include "photo_attachment.h"
#include "display_diagnostics.h"
#include "codex_remote.h"
#include "ota_update.h"
#include "art_orientation.h"
#include "gallery_remote.h"
#include "wifi_status.h"
#include "wifi_probe.h"
#include "wireless_boot.h"
#include "rpc_link.h"
#include "esp_heap_caps.h"
#include "esp_app_desc.h"
#include "esp_system.h"
#include "esp_timer.h"
#include "esp_http_client.h"
#include <stdatomic.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

extern volatile unsigned tab5_display_rotate_us,tab5_display_rotate_peak_us,tab5_display_flush_count,tab5_display_flush_errors;
extern volatile unsigned tab5_codex_notice_state,tab5_pc_wakes,tab5_backlight_changes,tab5_lcd_underruns;
extern volatile int tab5_ui_page,tab5_saver_active,tab5_backlight_percent;
extern volatile int tab5_ui_data_age_ms,tab5_ui_task_selected,tab5_ui_session_ready,tab5_ui_voice_check,tab5_ui_voice_age_ms;
extern volatile int tab5_voice_http_stage,tab5_voice_http_status,tab5_voice_request_ms,tab5_voice_last_seq;
extern unsigned tab5_ui_age_ms(void);

cJSON *runtime_diagnostics_snapshot(void) {
    cJSON *out=cJSON_CreateObject();if(!out)return NULL;
    device_config cfg=device_get_config();char host[33],name[TRUSTED_HOST_NAME];device_host_identity(host,name);
    int64_t now=esp_timer_get_time()/1000;
    cJSON_AddNumberToObject(out,"diagnosticVersion",1);
    cJSON_AddStringToObject(out,"deviceId",cfg.id);cJSON_AddStringToObject(out,"hostId",host);
    cJSON_AddStringToObject(out,"firmware",esp_app_get_description()->version);
    cJSON_AddNumberToObject(out,"uptimeMs",now);cJSON_AddNumberToObject(out,"sampleAgeMs",0);cJSON_AddNumberToObject(out,"resetReason",esp_reset_reason());
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    cJSON_AddBoolToObject(out,"wifiConnected",wifi_connected);cJSON_AddBoolToObject(out,"bleConnected",ble_connected);
    cJSON_AddNumberToObject(out,"bleConnectCount",ble_connect_count);cJSON_AddNumberToObject(out,"bleDisconnectReason",ble_disconnect_reason);
    cJSON_AddNumberToObject(out,"assetsReady",assets_ready());cJSON_AddNumberToObject(out,"assetsCached",assets_cached());
    cJSON *ages=cJSON_AddArrayToObject(out,"ageMs");
    if(ages)for(int i=0;i<3;i++)cJSON_AddItemToArray(ages,cJSON_CreateNumber(device_links.received_ms[i]<0?-1:now-device_links.received_ms[i]));
    xSemaphoreGive(device_mutex);
#define number(key,value) cJSON_AddNumberToObject(out,key,value)
    number("uiPage",tab5_ui_page);number("saverActive",tab5_saver_active);number("pcWakeCount",tab5_pc_wakes);
    number("uiAgeMs",tab5_ui_age_ms());number("uiDataAgeMs",tab5_ui_data_age_ms);
    number("taskSelected",tab5_ui_task_selected);number("inputSessionReady",tab5_ui_session_ready);number("codexNotice",tab5_codex_notice_state);
    number("voiceCheck",tab5_ui_voice_check);number("voiceAgeMs",tab5_ui_voice_age_ms);
    number("voiceStage",tab5_voice_http_stage);number("voiceHttp",tab5_voice_http_status);number("voiceRequestMs",tab5_voice_request_ms);number("voiceSeq",tab5_voice_last_seq);
    number("backlightChanges",tab5_backlight_changes);number("backlightPercent",tab5_backlight_percent);
    number("lcdUnderruns",tab5_lcd_underruns);number("flushCount",tab5_display_flush_count);number("flushErrors",tab5_display_flush_errors);
    number("rotateUs",tab5_display_rotate_us);number("rotatePeakUs",tab5_display_rotate_peak_us);
    number("internalFree",heap_caps_get_free_size(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT));
    number("internalLargest",camera_viewport_diagnostic_largest(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT));
    number("psramFree",heap_caps_get_free_size(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT));
#undef number
    // One reusable buffer bounds stack usage. Exporters are shared with USB.
    char value[1024];
#define diagnostic(key,formatter) do{value[0]=0;formatter(value,sizeof(value));cJSON_AddStringToObject(out,key,value);}while(0)
    diagnostic("cameraDiag",camera_preview_diagnostics);diagnostic("displayDiag",display_diagnostics_format);
    diagnostic("photoDiag",photo_diagnostics);diagnostic("photoUploadDiag",codex_remote_upload_diagnostics);
    diagnostic("cameraLogHex",camera_preview_driver_error_hex);diagnostic("otaDiag",ota_update_diagnostics);
    diagnostic("artOrientation",art_orientation_diagnostics);diagnostic("rpcDiag",rpc_failure_diagnostics);
    diagnostic("wifiDiag",tab5_wifi_numeric_diagnostics);diagnostic("wifiRecoveryDiag",tab5_wifi_recovery_diagnostics);
    diagnostic("wifiHttpDiag",tab5_wifi_http_diagnostics);diagnostic("wifiPathDiag",tab5_wifi_path_diagnostics);
    diagnostic("wirelessBootDiag",tab5_wireless_boot_diagnostics);diagnostic("wifiIsolation",wifi_probe_diagnostics);
#if TAB5_LOCAL_ART
    diagnostic("galleryDiag",gallery_diagnostics);diagnostic("galleryTransferDiag",gallery_transfer_diagnostics);
#endif
#undef diagnostic
    return out;
}

typedef struct {char id[33],session[33];link_kind link;unsigned epoch;} runtime_job;
static atomic_bool sending;
static void runtime_send(void *argument) {
    runtime_job *job=argument;cJSON *report=NULL,*snapshot=NULL,*reply=NULL;char nonce[33];int status=0;
    if(job->epoch!=device_host_epoch()||device_host_changing())goto done;
    report=cJSON_CreateObject();snapshot=runtime_diagnostics_snapshot();
    if(!report||!snapshot)goto done;
    cJSON_AddStringToObject(report,"kind","runtime-diagnostics");cJSON_AddStringToObject(report,"requestId",job->id);
    cJSON_AddStringToObject(report,"session",job->session);cJSON_AddItemToObject(report,"snapshot",snapshot);snapshot=NULL;
    if(job->epoch!=device_host_epoch()||device_host_changing())goto done;
    if(job->link==LINK_BLE) {
        int64_t began=esp_timer_get_time()/1000;
        for(unsigned attempt=0;attempt<12;attempt++) {
            if(job->epoch!=device_host_epoch()||device_host_changing()||esp_timer_get_time()/1000-began>=5000)break;
            int64_t sample_age=esp_timer_get_time()/1000-began;
            cJSON_SetNumberValue(cJSON_GetObjectItemCaseSensitive(cJSON_GetObjectItemCaseSensitive(report,"snapshot"),"sampleAgeMs"),sample_age);
            reply=rpc_call(job->link,report,&status,nonce);
            if(reply&&status==200)break;
            cJSON_Delete(reply);reply=NULL;
            if(status!=0&&status!=503)break;
            vTaskDelay(pdMS_TO_TICKS(200));
        }
    }
    else {
        device_config cfg=device_get_config();char url[112];
        snprintf(url,sizeof(url),"http://%s:%d/tab5/v1/rpc",cfg.host,cfg.port);
        esp_http_client_config_t options={.url=url,.timeout_ms=6000,.disable_auto_redirect=true};
        esp_http_client_handle_t client=esp_http_client_init(&options);
        if(client){if(job->epoch==device_host_epoch())reply=rpc_http_call(client,report,&status,nonce);esp_http_client_cleanup(client);}
    }
done:
    cJSON_Delete(snapshot);cJSON_Delete(report);cJSON_Delete(reply);free(job);atomic_store(&sending,false);vTaskDelete(NULL);
}
bool runtime_diagnostics_report(const char *id,const char *session,link_kind link) {
    if(!id||strlen(id)!=32||!session||strlen(session)!=32||(link!=LINK_WIFI&&link!=LINK_BLE)||device_host_changing())return false;
    bool expected=false;if(!atomic_compare_exchange_strong(&sending,&expected,true))return false;
    runtime_job *job=malloc(sizeof(*job));if(!job){atomic_store(&sending,false);return false;}
    snprintf(job->id,sizeof(job->id),"%s",id);snprintf(job->session,sizeof(job->session),"%s",session);
    job->link=link;job->epoch=device_host_epoch();
    if(xTaskCreate(runtime_send,"runtime_diag",8192,job,2,NULL)!=pdPASS){free(job);atomic_store(&sending,false);return false;}
    return true;
}
