#ifndef TAB5_TRANSPORT_BENCH_TEST
#include "transport_benchmark.h"
#include "flash_benchmark.h"
#include "device.h"
#include "rpc_link.h"
#include "wifi_performance.h"
#include "wifi_probe.h"
#include "codex_remote.h"
#include "voice_remote.h"
#include "ota_update.h"
#include "esp_http_client.h"
#include "esp_heap_caps.h"
#include "esp_timer.h"
#include "freertos/idf_additions.h"
#include "mbedtls/base64.h"
#include "mbedtls/sha256.h"
#endif
#include <stdatomic.h>
#include <stdio.h>
#include <string.h>
#include <stdlib.h>

static atomic_bool active,stopping;
static char diagnostic[3600]="idle";
bool transport_benchmark_busy(void){return atomic_load(&active);}
static void report(const char *value) {xSemaphoreTake(device_mutex,portMAX_DELAY);snprintf(diagnostic,sizeof(diagnostic),"%s",value);xSemaphoreGive(device_mutex);}
void transport_benchmark_diagnostics(char *out,size_t size) {xSemaphoreTake(device_mutex,portMAX_DELAY);snprintf(out,size,"%s",diagnostic);xSemaphoreGive(device_mutex);}
void transport_benchmark_cancel(void){atomic_store(&stopping,true);}
// This path deliberately has no OTA/partition handle. Each decoded range is
// hashed and then reused; the final digest binds the complete selected image.
static void firmware_probe(uint8_t *bytes,char *progress,bool isolate) {
    char sha[65]={0},offer[33]={0};int total=0;link_state links;
    xSemaphoreTake(device_mutex,portMAX_DELAY);links=device_links;
    cJSON *data=device_snapshots[LINK_BLE],*ota=cJSON_GetObjectItemCaseSensitive(data,"ota");
    cJSON *hash=cJSON_GetObjectItemCaseSensitive(ota,"sha256"),*id=cJSON_GetObjectItemCaseSensitive(ota,"offerId"),*size=cJSON_GetObjectItemCaseSensitive(ota,"size");
    cJSON *cap=cJSON_GetObjectItemCaseSensitive(data,"rpcOtaZlib");
    bool supported=cJSON_IsNumber(cap)&&cap->valuedouble==1;
    if(cJSON_IsString(hash)&&strlen(hash->valuestring)==64&&strspn(hash->valuestring,"0123456789abcdef")==64&&
       cJSON_IsString(id)&&strlen(id->valuestring)==32&&strspn(id->valuestring,"0123456789abcdef")==32&&
       cJSON_IsNumber(size)&&size->valuedouble>=1024&&size->valuedouble<=0x6e0000&&size->valuedouble==size->valueint) {
        memcpy(sha,hash->valuestring,65);memcpy(offer,id->valuestring,33);total=size->valueint;
    }
    xSemaphoreGive(device_mutex);
    int64_t began=esp_timer_get_time()/1000;
    if(!supported||rpc_data_limit(LINK_BLE)!=49152){snprintf(progress,3600,"failed:firmware_capability");return;}
    if(!total){snprintf(progress,3600,"failed:firmware_offer");return;}
    if(links.mode!=LINK_BLE||!tab5_usb_connected()||links.received_ms[LINK_BLE]<0||began<links.received_ms[LINK_BLE]||began-links.received_ms[LINK_BLE]>=8000){snprintf(progress,3600,"unavailable");return;}
    rpc_profile_reset(LINK_BLE);
    mbedtls_sha256_context context;mbedtls_sha256_init(&context);
    int error=mbedtls_sha256_starts(&context,0)?-5:0,transferred=0;
    while(!error&&transferred<total&&!atomic_load(&stopping)) {
        if(esp_timer_get_time()/1000-began>=110000){error=-2;break;}
        if(isolate&&!wifi_probe_ready()){error=-4;break;}
        xSemaphoreTake(device_mutex,portMAX_DELAY);
        bool same=device_links.mode==LINK_BLE&&!strcmp(device_links.session[LINK_BLE],links.session[LINK_BLE]);
        xSemaphoreGive(device_mutex);
        if(!same||!tab5_usb_connected()){error=-3;break;}
        int want=total-transferred;if(want>49152)want=49152;
        int got=rpc_ota_probe_read(sha,offer,transferred,bytes,want);
        if(got<=0||got>want){error=got<0?got:-1;break;}
        if(mbedtls_sha256_update(&context,bytes,got)){error=-5;break;}
        transferred+=got;
        snprintf(progress,3600,"running;BLE,down,1,%d/%d",transferred,total);report(progress);
    }
    uint8_t digest[32];char hex[65]={0};
    if(!error&&transferred==total&&!atomic_load(&stopping)) {
        if(mbedtls_sha256_finish(&context,digest))error=-5;
        else {for(int i=0;i<32;i++)snprintf(hex+i*2,3,"%02x",digest[i]);if(strcmp(hex,sha))error=-6;}
    }
    mbedtls_sha256_free(&context);
    unsigned ms=(unsigned)(esp_timer_get_time()/1000-began);rpc_profile p=rpc_profile_get(LINK_BLE);
    // Check the final range too: cancellation/session loss must not look like
    // a successful image just because it happened during the last RPC.
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    bool same=device_links.mode==LINK_BLE&&!strcmp(device_links.session[LINK_BLE],links.session[LINK_BLE]);xSemaphoreGive(device_mutex);
    if(!same||!tab5_usb_connected())error=-3;
    if(isolate&&!wifi_probe_ready())error=-4;
    if(ms>=110000)error=-2;
    snprintf(progress,3600,"%s;BLE,down,1,%d,%u,%d,%u,%u,%u,%u,%u,%u,%u,%u,%u;image,%s,%d;",
        atomic_load(&stopping)?"cancelled":error?"failed":"complete",transferred,ms,error,p.calls,(unsigned)(p.encode_us/1000),
        (unsigned)(p.exchange_us/1000),(unsigned)(p.decode_us/1000),p.wire_tx,p.wire_rx,p.min_internal,p.min_psram,p.min_largest,sha,total);
}
static void run(void *arg) {
    bool ota_probe=arg!=NULL,firmware=(uintptr_t)arg>=3,isolate=(uintptr_t)arg==2||(uintptr_t)arg==4,leased=false;size_t result_size=0;bool failed=false;const char *early_result=NULL;device_config cfg=device_get_config();
    char *results=heap_caps_calloc(1,3300,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    char *progress=heap_caps_malloc(3600,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    uint8_t *bytes=heap_caps_malloc(49152,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    if(!bytes||!results||!progress){early_result="failed:memory";goto done;}
    if(isolate) {
        if(cfg.mode!=LINK_BLE||!tab5_usb_connected()){early_result="failed:isolation_mode";goto done;}
        leased=firmware?wifi_probe_begin_firmware():wifi_probe_begin();
        if(!leased){early_result="failed:isolation_busy";goto done;}
        report("running;wifi_pause");int64_t prepare_until=esp_timer_get_time()/1000+6000;
        while(!atomic_load(&stopping)&&wifi_probe_pending()&&!wifi_probe_ready()&&esp_timer_get_time()/1000<prepare_until)
            vTaskDelay(pdMS_TO_TICKS(50));
        if(!wifi_probe_ready()||atomic_load(&stopping)){early_result=atomic_load(&stopping)?"cancelled;":"failed:isolation_prepare";goto done;}
    }
    if(ota_probe||cfg.mode==LINK_BLE)ble_probe_capture(true);
    if(firmware){firmware_probe(bytes,progress,isolate);goto done;}
    for(int channel=0;channel<3&&!atomic_load(&stopping);channel++) {
        link_state links;xSemaphoreTake(device_mutex,portMAX_DELAY);links=device_links;xSemaphoreGive(device_mutex);
        int64_t now=esp_timer_get_time()/1000;
        // Explicit BLE-only preflight, never silently measure USB or Wi-Fi.
        if(ota_probe&&(channel!=LINK_BLE||links.mode!=LINK_BLE))continue;
        if((links.mode!=-1&&links.mode!=channel)||links.received_ms[channel]<0||now<links.received_ms[channel]||now-links.received_ms[channel]>=8000)continue;
        esp_http_client_handle_t http=NULL;
        if(channel==LINK_WIFI)wifi_performance_bulk(true);
        if(channel==LINK_WIFI) {
            char url[96];snprintf(url,sizeof(url),"http://%s:%d/tab5/v1/rpc",cfg.host,cfg.port);
            esp_http_client_config_t options={.url=url,.method=HTTP_METHOD_POST,.timeout_ms=12000,.disable_auto_redirect=true,.buffer_size=4096};
            http=esp_http_client_init(&options);if(!http){wifi_performance_bulk(false);failed=true;break;}
        }
        size_t chunk=rpc_data_limit(channel);
        int64_t channel_deadline=now+(ota_probe?20000:300000);
        for(int repeat=0;repeat<3&&!atomic_load(&stopping);repeat++)for(int direction=ota_probe?1:0;direction<2&&!atomic_load(&stopping);direction++) {
            rpc_profile_reset(channel);
            unsigned transferred=0;int error=0;int64_t began=esp_timer_get_time()/1000;
            for(unsigned offset=0;offset<262144&&!atomic_load(&stopping);offset+=(unsigned)chunk) {
                size_t count=262144-offset<chunk?262144-offset:chunk;
                if(esp_timer_get_time()/1000>=channel_deadline){error=-2;break;}
                if(isolate&&!wifi_probe_ready()){error=-4;break;}
                xSemaphoreTake(device_mutex,portMAX_DELAY);int mode=device_links.mode;bool same=!strcmp(device_links.session[channel],links.session[channel]);xSemaphoreGive(device_mutex);
                if(!same||(mode!=-1&&mode!=channel)||(ota_probe&&mode!=LINK_BLE)){error=-3;break;}
                for(unsigned i=0;i<count;i++)bytes[i]=(uint8_t)((offset+i)*31+17);
                uint8_t hash[32];char expected_hash[65];mbedtls_sha256(bytes,count,hash,0);
                for(int i=0;i<32;i++)snprintf(expected_hash+i*2,3,"%02x",hash[i]);
                cJSON *request=cJSON_CreateObject();cJSON_AddStringToObject(request,"kind","benchmark");
                if(ota_probe)cJSON_AddBoolToObject(request,"otaProbe",true);
                cJSON_AddStringToObject(request,"session",links.session[channel]);
                cJSON_AddNumberToObject(request,"offset",offset);cJSON_AddNumberToObject(request,"count",count);
                int status=0;size_t received=0;char nonce[33];
                cJSON *reply=rpc_data_call(channel,http,request,&status,nonce,direction==0?bytes:NULL,direction==0?count:0,
                    direction==1?bytes:NULL,chunk,&received);cJSON_Delete(request);
                cJSON *sha=cJSON_GetObjectItemCaseSensitive(reply,"sha256"),*at=cJSON_GetObjectItemCaseSensitive(reply,"offset");
                bool ok=status==200&&cJSON_IsString(sha)&&!strcmp(sha->valuestring,expected_hash)&&cJSON_IsNumber(at)&&at->valuedouble==offset;
                if(ok&&direction==1){ok=received==count;for(unsigned i=0;ok&&i<received;i++)if(bytes[i]!=(uint8_t)((offset+i)*31+17))ok=false;}
                cJSON_Delete(reply);if(!ok){error=status?status:-1;break;}transferred+=(unsigned)count;
                snprintf(progress,3600,"running;%s%s,%s,%d,%u/262144",results,link_name(channel),direction?"down":"up",repeat+1,transferred);report(progress);
            }
            unsigned ms=(unsigned)(esp_timer_get_time()/1000-began);
            rpc_profile profile=rpc_profile_get(channel);
            int n=snprintf(results+result_size,3300-result_size,"%s,%s,%d,%u,%u,%d,%u,%u,%u,%u,%u,%u,%u,%u,%u;",link_name(channel),direction?"down":"up",repeat+1,transferred,ms,error,profile.calls,(unsigned)(profile.encode_us/1000),(unsigned)(profile.exchange_us/1000),(unsigned)(profile.decode_us/1000),profile.wire_tx,profile.wire_rx,profile.min_internal,profile.min_psram,profile.min_largest);
            if(n>0&&(size_t)n<3300-result_size)result_size+=n;
            if(error){failed=true;goto channel_done;}
        }
channel_done:
        if(http)esp_http_client_cleanup(http);
        if(channel==LINK_WIFI)wifi_performance_bulk(false);
    }
    snprintf(progress,3600,"%s;%s",atomic_load(&stopping)?"cancelled":failed?"failed":result_size?"complete":"unavailable",results);
done:
    if(ota_probe||cfg.mode==LINK_BLE)ble_probe_capture(false);
    if(leased) {
        // Do not expose a final success while the temporary Wi-Fi stop is
        // still held. The worker also restores on mode/USB loss or expiry.
        report("running;wifi_restore");wifi_probe_end();
        int64_t restore_until=esp_timer_get_time()/1000+6000;
        while(wifi_probe_pending()&&esp_timer_get_time()/1000<restore_until)vTaskDelay(pdMS_TO_TICKS(50));
        if(wifi_probe_pending())early_result="failed:isolation_restore";
    }
    report(early_result?early_result:progress);
    free(results);free(progress);free(bytes);atomic_store(&active,false);vTaskDeleteWithCaps(NULL);
}
static bool start(unsigned mode) {
    if(atomic_exchange(&active,true))return false;
    voice_remote_state voice;voice_remote_get(&voice);
    if(codex_remote_busy()||voice.busy||ota_update_busy()||flash_benchmark_busy()||wifi_probe_pending()){atomic_store(&active,false);report("failed:busy");return false;}
    atomic_store(&stopping,false);report("running;starting");
    if(xTaskCreateWithCaps(run,"tab5_benchmark",8192,(void*)(uintptr_t)mode,3,NULL,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT)!=pdPASS){atomic_store(&active,false);report("failed:task_memory");return false;}
    return true;
}
bool transport_benchmark_start(void){return start(false);}
bool transport_benchmark_start_ble_ota_probe(void){return start(true);}
bool transport_benchmark_start_ble_isolated_probe(void){return start(2);}
bool transport_benchmark_start_firmware_probe(bool isolate){return start(isolate?4:3);}
