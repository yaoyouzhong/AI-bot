#include "wifi_submission_test.h"
#include "unlock_remote.h"
#include "device.h"
#include "rpc_link.h"
#include "esp_http_client.h"
#include "esp_random.h"
#include "mbedtls/base64.h"
#include "mbedtls/gcm.h"
#include <stdatomic.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

// No UI input and no credentials: the only clear payload is a public fixture.
static atomic_bool busy;
static char last_run[33];
typedef struct {char run[33];} test_job;
static void wipe(void *v,size_t n){volatile unsigned char *p=v;while(n--)*p++=0;}
static const char *text(cJSON *o,const char *n){const char *s=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(o,n));return s?s:"";}
static cJSON *call(esp_http_client_handle_t client,const char *run,const char *name,const char *op,const char *offer,const char *sealed,bool passed) {
    if(!unlock_remote_wifi_only())return NULL;
    cJSON *r=cJSON_CreateObject();if(!r)return NULL;
    cJSON_AddStringToObject(r,"kind","wifi-submit-test");cJSON_AddStringToObject(r,"run",run);cJSON_AddStringToObject(r,"case",name);cJSON_AddStringToObject(r,"op",op);
    if(offer)cJSON_AddStringToObject(r,"offer",offer);
    if(sealed)cJSON_AddStringToObject(r,"sealed",sealed);
    if(!strcmp(op,"finish"))cJSON_AddBoolToObject(r,"passed",passed);
    int status=0;char nonce[33];cJSON *reply=rpc_http_call(client,r,&status,nonce);cJSON_Delete(r);
    if(status!=200){cJSON_Delete(reply);return NULL;}return reply;
}
static bool check(cJSON *reply,const char *error){return reply&&(!error?cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(reply,"ok")):!strcmp(text(reply,"error"),error));}
static bool run_case(esp_http_client_handle_t client,const char *run,const char *name) {
    uint8_t key[32]={0},packet[32]={0};unsigned char encoded[45]={0};char offer[33]={0};size_t n=0;bool ok=false;
    cJSON *reply=call(client,run,name,"begin",NULL,NULL,false);
    if(!check(reply,NULL)||strlen(text(reply,"offer"))!=32||strlen(text(reply,"key"))!=44)goto done;
    memcpy(offer,text(reply,"offer"),32);
    if(mbedtls_base64_decode(key,sizeof(key),&n,(const unsigned char *)text(reply,"key"),44)||n!=32)goto done;
    cJSON *field=cJSON_GetObjectItemCaseSensitive(reply,"key");wipe(field->valuestring,strlen(field->valuestring));cJSON_Delete(reply);reply=NULL;
    esp_fill_random(packet,12);mbedtls_gcm_context g;mbedtls_gcm_init(&g);
    int rc=mbedtls_gcm_setkey(&g,MBEDTLS_CIPHER_ID_AES,key,256);
    if(!rc)rc=mbedtls_gcm_crypt_and_tag(&g,MBEDTLS_GCM_ENCRYPT,4,packet,12,(const uint8_t *)offer,32,(const uint8_t *)"4826",packet+28,16,packet+12);
    mbedtls_gcm_free(&g);wipe(key,sizeof(key));if(rc)goto done;
    if(!strcmp(name,"tamper"))packet[31]^=1;
    if(mbedtls_base64_encode(encoded,sizeof(encoded),&n,packet,sizeof(packet)))goto done;
    encoded[n]=0;
    if(!strcmp(name,"cancel")){reply=call(client,run,name,"cancel",offer,NULL,false);if(!check(reply,NULL))goto done;cJSON_Delete(reply);reply=NULL;}
    if(!strcmp(name,"expiry"))for(int i=0;i<32;i++){vTaskDelay(pdMS_TO_TICKS(500));if(!unlock_remote_wifi_only())goto done;}
    reply=call(client,run,name,"submit",offer,(const char *)encoded,false);
    const char *expected=!strcmp(name,"tamper")?"packet_rejected":!strcmp(name,"cancel")?"cancelled":!strcmp(name,"expiry")?"offer_expired":NULL;
    ok=check(reply,expected);
    if(ok&&!strcmp(name,"accept")) {
        ok=cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(reply,"testReceived"))&&cJSON_IsFalse(cJSON_GetObjectItemCaseSensitive(reply,"unlocked"));
        cJSON_Delete(reply);reply=call(client,run,name,"submit",offer,(const char *)encoded,false);ok=ok&&check(reply,"attempt_finished");
    }
done:
    if(reply){cJSON *k=cJSON_GetObjectItemCaseSensitive(reply,"key");if(cJSON_IsString(k))wipe(k->valuestring,strlen(k->valuestring));}cJSON_Delete(reply);
    wipe(key,sizeof(key));wipe(packet,sizeof(packet));wipe(encoded,sizeof(encoded));return ok;
}
static void worker(void *arg) {
    test_job *job=arg;device_config cfg=device_get_config();char url[96];snprintf(url,sizeof(url),"http://%s:%d/tab5/v1/rpc",cfg.host,cfg.port);
    esp_http_client_config_t options={.url=url,.method=HTTP_METHOD_POST,.timeout_ms=10000,.disable_auto_redirect=true,.buffer_size=2048};
    esp_http_client_handle_t client=esp_http_client_init(&options);
    if(client){bool passed=true;const char *names[]={"accept","tamper","cancel","expiry"};for(int i=0;i<4&&passed;i++)passed=run_case(client,job->run,names[i]);cJSON *result=call(client,job->run,"","finish",NULL,NULL,passed);cJSON_Delete(result);esp_http_client_cleanup(client);}
    wipe(&cfg,sizeof(cfg));wipe(job,sizeof(*job));free(job);atomic_store(&busy,false);vTaskDelete(NULL);
}
void wifi_submission_test_poll(cJSON *health) {
    cJSON *command=cJSON_GetObjectItemCaseSensitive(health,"wifiSubmissionTest");const char *run=text(command,"run");
    if(!cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(command,"syntheticOnly"))||strlen(run)!=32||!strcmp(run,last_run)||atomic_load(&busy)||!unlock_remote_wifi_only())return;
    test_job *job=calloc(1,sizeof(*job));if(!job)return;memcpy(job->run,run,32);memcpy(last_run,run,32);atomic_store(&busy,true);
    if(xTaskCreate(worker,"wifi-submit-test",6144,job,3,NULL)!=pdPASS){atomic_store(&busy,false);free(job);}
}
