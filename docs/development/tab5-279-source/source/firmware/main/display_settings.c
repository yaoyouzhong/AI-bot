#include "display_settings.h"
#include "device.h"
#include "rpc_link.h"
#include "esp_http_client.h"
#include "esp_random.h"
#include "mbedtls/gcm.h"
#include "mbedtls/sha256.h"
#include <stdatomic.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
static atomic_bool sending;
typedef struct {cJSON *report;link_kind link;} display_job;
typedef struct {uint8_t bytes[1024];size_t size;bool overflow;} display_response;
static esp_err_t response(esp_http_client_event_t *event) {
    display_response *r=event->user_data;
    if(event->event_id==HTTP_EVENT_ON_DATA&&event->data_len>0){if((size_t)event->data_len>sizeof(r->bytes)-r->size){r->overflow=true;return ESP_FAIL;}memcpy(r->bytes+r->size,event->data,event->data_len);r->size+=event->data_len;}return ESP_OK;
}
static void send_report(void *arg) {
    display_job *job=arg;char nonce[33];int status=0;
    if(job->link!=LINK_WIFI){cJSON *reply=rpc_call(job->link,job->report,&status,nonce);cJSON_Delete(reply);}
    else {
        device_config cfg=device_get_config();char *clear=cJSON_PrintUnformatted(job->report);
        if(clear&&cfg.paired&&cfg.host[0]) {
            size_t length=strlen(clear);uint8_t *packet=length<2048?malloc(length+28):NULL;
            if(packet) {
                device_nonce(nonce);esp_fill_random(packet,12);mbedtls_gcm_context ctx;mbedtls_gcm_init(&ctx);
                int err=mbedtls_gcm_setkey(&ctx,MBEDTLS_CIPHER_ID_AES,cfg.key,256);
                if(!err)err=mbedtls_gcm_crypt_and_tag(&ctx,MBEDTLS_GCM_ENCRYPT,length,packet,12,(uint8_t *)nonce,32,(uint8_t *)clear,packet+28,16,packet+12);
                mbedtls_gcm_free(&ctx);
                if(!err) {
                    uint8_t hash[32];char digest[65],auth[180],proof[65],url[112];mbedtls_sha256(packet,length+28,hash,0);
                    for(int i=0;i<32;i++)sprintf(digest+i*2,"%02x",hash[i]);
                    snprintf(auth,sizeof(auth),"POST|/tab5/v1/rpc|%s|%s|%s",cfg.id,nonce,digest);device_proof(auth,proof);
                    snprintf(url,sizeof(url),"http://%s:%d/tab5/v1/rpc",cfg.host,cfg.port);
                    display_response received={0};esp_http_client_config_t options={.url=url,.timeout_ms=6000,.disable_auto_redirect=true,.event_handler=response,.user_data=&received};
                    esp_http_client_handle_t client=esp_http_client_init(&options);
                    if(client){esp_http_client_set_method(client,HTTP_METHOD_POST);esp_http_client_set_header(client,"Content-Type","application/octet-stream");esp_http_client_set_header(client,"X-AIBot-Device",cfg.id);esp_http_client_set_header(client,"X-AIBot-Nonce",nonce);esp_http_client_set_header(client,"X-AIBot-Proof",proof);esp_http_client_set_post_field(client,(char *)packet,length+28);
                        if(esp_http_client_perform(client)==ESP_OK&&!received.overflow&&esp_http_client_get_status_code(client)==200){char *plain=device_decrypt(received.bytes,received.size,nonce);free(plain);}esp_http_client_cleanup(client);}
                }free(packet);
            }
        }free(clear);
    }
    cJSON_Delete(job->report);free(job);atomic_store(&sending,false);vTaskDelete(NULL);
}
bool display_settings_report(cJSON *report,link_kind link) {
    bool expected=false;if(!atomic_compare_exchange_strong(&sending,&expected,true))return false;
    display_job *job=malloc(sizeof(*job));if(!job){atomic_store(&sending,false);return false;}*job=(display_job){report,link};
    if(xTaskCreate(send_report,"display_report",8192,job,3,NULL)!=pdPASS){free(job);atomic_store(&sending,false);return false;}return true;
}
