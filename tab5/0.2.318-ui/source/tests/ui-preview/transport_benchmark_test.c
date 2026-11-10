#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>
#include <stdio.h>
#include "cJSON.h"
#include <windows.h>
#include <bcrypt.h>
#define TAB5_TRANSPORT_BENCH_TEST
enum {LINK_USB,LINK_WIFI,LINK_BLE};
enum {MALLOC_CAP_SPIRAM=1,MALLOC_CAP_8BIT=2,pdPASS=1,portMAX_DELAY=0};
typedef int link_kind;
typedef struct {char host[32];int port,mode;} device_config;
typedef struct {int mode;int64_t received_ms[3];char session[3][40];} link_state;
typedef struct {bool busy;} voice_remote_state;
typedef struct {unsigned calls,min_internal,min_psram,min_largest,wire_tx,wire_rx;uint64_t encode_us,exchange_us,decode_us;} rpc_profile;
typedef void *esp_http_client_handle_t;
typedef struct {const char *url;int method,timeout_ms;bool disable_auto_redirect;int buffer_size;} esp_http_client_config_t;
#define HTTP_METHOD_POST 1
static link_state device_links;
static cJSON *device_snapshots[3];
static uint8_t *firmware_bytes;static int firmware_size;
static char firmware_sha[65];
static int device_mutex,calls,fail_call,cancel_call,change_call,cost_ms;
static bool ordinary;
static int64_t clock_ms;
static int64_t esp_timer_get_time(void){return clock_ms*1000;}
static device_config device_get_config(void){return (device_config){.mode=device_links.mode};}
static bool usb_connected=true,tab5_wifi_initialized=true,wifi_running=true;
static int wifi_stop_error,wifi_start_errors,wifi_stops,wifi_starts;
static bool tab5_usb_connected(void){return usb_connected;}
#define TAB5_WIFI_PROBE_TEST
#define ESP_OK 0
static int esp_wifi_stop(void){wifi_stops++;if(wifi_stop_error)return wifi_stop_error;wifi_running=false;return 0;}
static int esp_wifi_start(void){wifi_starts++;if(wifi_start_errors){wifi_start_errors--;return 257;}wifi_running=true;return 0;}
#include "../../firmware/main/wifi_probe.c"
static void ble_probe_capture(bool value){(void)value;}
#define pdMS_TO_TICKS(ms) (ms)
static void vTaskDelay(int ms){clock_ms+=ms;bool reconnect;wifi_probe_service(device_links.mode==LINK_BLE&&usb_connected,&reconnect);}
static void xSemaphoreTake(int m,int t){(void)m;(void)t;}
static void xSemaphoreGive(int m){(void)m;}
static void *heap_caps_calloc(size_t n,size_t s,int c){(void)c;return calloc(n,s);}
static void *heap_caps_malloc(size_t s,int c){(void)c;return malloc(s);}
static bool codex_remote_busy(void){return false;}
static bool ota_update_busy(void){return false;}
static bool flash_benchmark_busy(void){return false;}
static void voice_remote_get(voice_remote_state *v){v->busy=false;}
static void wifi_performance_bulk(bool b){(void)b;assert(ordinary);}
static void *esp_http_client_init(const esp_http_client_config_t *c){(void)c;assert(0);return NULL;}
static void esp_http_client_cleanup(void *p){(void)p;}
static void rpc_profile_reset(int c){assert(c==LINK_BLE);}
static rpc_profile rpc_profile_get(int c){assert(c==LINK_BLE);return (rpc_profile){0};}
static size_t rpc_data_limit(int c){assert(c==LINK_BLE);return 49152;}
static const char *link_name(int c){return c==LINK_BLE?"BLE":"unexpected";}
static int mbedtls_sha256(const uint8_t *b,size_t n,uint8_t out[32],int x){(void)b;(void)n;(void)x;memset(out,0,32);return 0;}
typedef struct {BCRYPT_ALG_HANDLE alg;BCRYPT_HASH_HANDLE hash;} mbedtls_sha256_context;
static void mbedtls_sha256_init(mbedtls_sha256_context *c){memset(c,0,sizeof(*c));}
static int mbedtls_sha256_starts(mbedtls_sha256_context *c,int mode){assert(!mode);assert(BCryptOpenAlgorithmProvider(&c->alg,BCRYPT_SHA256_ALGORITHM,NULL,0)==0);return BCryptCreateHash(c->alg,&c->hash,NULL,0,NULL,0,0);}
static int mbedtls_sha256_update(mbedtls_sha256_context *c,const uint8_t *b,size_t n){return BCryptHashData(c->hash,(PUCHAR)b,(ULONG)n,0);}
static int mbedtls_sha256_finish(mbedtls_sha256_context *c,uint8_t out[32]){return BCryptFinishHash(c->hash,out,32,0);}
static void mbedtls_sha256_free(mbedtls_sha256_context *c){BCryptDestroyHash(c->hash);BCryptCloseAlgorithmProvider(c->alg,0);}
static void vTaskDeleteWithCaps(void *p){(void)p;}
static int xTaskCreateWithCaps(void (*fn)(void*),const char *n,unsigned s,void *a,int p,void *h,int caps){(void)n;(void)s;(void)p;(void)h;(void)caps;fn(a);return pdPASS;}
void transport_benchmark_cancel(void);
static int rpc_ota_probe_read(const char *sha,const char *offer,int offset,uint8_t *out,int count) {
    assert(!strcmp(sha,firmware_sha)&&!strcmp(offer,"00112233445566778899aabbccddeeff"));
    assert(offset>=0&&count>0&&count<=49152&&offset+count<=firmware_size);
    calls++;clock_ms+=cost_ms;memcpy(out,firmware_bytes+offset,count);
    if(calls==fail_call)out[0]^=1;
    if(calls==cancel_call)transport_benchmark_cancel();
    if(calls==change_call)strcpy(device_links.session[LINK_BLE],"changed");
    return count;
}
static cJSON *rpc_data_call(int channel,void *http,cJSON *request,int *status,char nonce[33],const uint8_t *upload,size_t size,uint8_t *download,size_t capacity,size_t *received) {
    (void)nonce;assert(channel==LINK_BLE&&!http);
    unsigned offset=(unsigned)cJSON_GetObjectItemCaseSensitive(request,"offset")->valueint;
    unsigned count=(unsigned)cJSON_GetObjectItemCaseSensitive(request,"count")->valueint;
    assert(count<=capacity&&(!upload||ordinary)&&(!size||ordinary));
    assert(cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(request,"otaProbe"))==!ordinary);
    calls++;clock_ms+=cost_ms;*status=200;*received=download?count:0;
    if(download)for(unsigned i=0;i<count;i++)download[i]=(uint8_t)((offset+i)*31+17);
    if(calls==fail_call&&download)download[0]^=1;
    if(calls==cancel_call)transport_benchmark_cancel();
    if(calls==change_call)strcpy(device_links.session[LINK_BLE],"changed");
    cJSON *r=cJSON_CreateObject();cJSON_AddNumberToObject(r,"offset",offset);cJSON_AddStringToObject(r,"sha256","0000000000000000000000000000000000000000000000000000000000000000");return r;
}
#include "../../firmware/main/transport_benchmark.c"
static void reset(void){device_links=(link_state){.mode=LINK_BLE,.received_ms={1000,1000,1000}};strcpy(device_links.session[LINK_BLE],"session");clock_ms=1000;calls=fail_call=cancel_call=change_call=0;cost_ms=100;ordinary=false;
    atomic_store(&probe_stage,PROBE_IDLE);atomic_store(&requested,false);usb_connected=tab5_wifi_initialized=wifi_running=true;wifi_stop_error=wifi_start_errors=wifi_stops=wifi_starts=0;
    cJSON_Delete(device_snapshots[LINK_BLE]);device_snapshots[LINK_BLE]=cJSON_CreateObject();
    cJSON_AddNumberToObject(device_snapshots[LINK_BLE],"rpcOtaZlib",1);
    cJSON *ota=cJSON_AddObjectToObject(device_snapshots[LINK_BLE],"ota");cJSON_AddStringToObject(ota,"sha256",firmware_sha);
    cJSON_AddStringToObject(ota,"offerId","00112233445566778899aabbccddeeff");cJSON_AddNumberToObject(ota,"size",firmware_size);
}
int main(int argc,char **argv){
    assert(argc==2);FILE *image=fopen(argv[1],"rb");assert(image);fseek(image,0,SEEK_END);firmware_size=(int)ftell(image);rewind(image);
    assert(firmware_size>=1024&&firmware_size<=0x6e0000);firmware_bytes=malloc(firmware_size);assert(firmware_bytes);
    assert(fread(firmware_bytes,1,firmware_size,image)==(size_t)firmware_size);fclose(image);
    mbedtls_sha256_context hash;uint8_t digest[32];mbedtls_sha256_init(&hash);assert(!mbedtls_sha256_starts(&hash,0));
    assert(!mbedtls_sha256_update(&hash,firmware_bytes,firmware_size));assert(!mbedtls_sha256_finish(&hash,digest));mbedtls_sha256_free(&hash);
    for(int i=0;i<32;i++)snprintf(firmware_sha+i*2,3,"%02x",digest[i]);
    reset();assert(transport_benchmark_start_ble_ota_probe());assert(calls==18&&!strncmp(diagnostic,"complete;",9)&&!strstr(diagnostic,",up,")&&!transport_benchmark_busy());
    for(int mode=-1;mode<LINK_BLE;mode++){reset();device_links.mode=mode;assert(transport_benchmark_start_ble_ota_probe());assert(!calls&&!strncmp(diagnostic,"unavailable",11));}
    reset();cost_ms=2000;assert(transport_benchmark_start_ble_ota_probe());assert(calls==10&&clock_ms==21000&&!strncmp(diagnostic,"failed;",7));
    reset();fail_call=1;assert(transport_benchmark_start_ble_ota_probe());assert(calls==1&&!strncmp(diagnostic,"failed;",7));
    reset();cancel_call=1;assert(transport_benchmark_start_ble_ota_probe());assert(calls==1&&!strncmp(diagnostic,"cancelled;",10));
    reset();change_call=1;assert(transport_benchmark_start_ble_ota_probe());assert(calls==1&&!strncmp(diagnostic,"failed;",7));
    reset();ordinary=true;assert(transport_benchmark_start());assert(calls==36&&strstr(diagnostic,",up,")&&!strncmp(diagnostic,"complete;",9));
    reset();assert(transport_benchmark_start_ble_isolated_probe());assert(calls==18&&!strncmp(diagnostic,"complete;",9)&&wifi_stops==1&&wifi_starts==1&&wifi_running&&!wifi_probe_pending());
    reset();fail_call=1;assert(transport_benchmark_start_ble_isolated_probe());assert(calls==1&&!strncmp(diagnostic,"failed;",7)&&wifi_running&&wifi_starts==1);
    reset();cancel_call=1;assert(transport_benchmark_start_ble_isolated_probe());assert(calls==1&&!strncmp(diagnostic,"cancelled;",10)&&wifi_running&&wifi_starts==1);
    reset();wifi_stop_error=259;assert(transport_benchmark_start_ble_isolated_probe());assert(!calls&&!strcmp(diagnostic,"failed:isolation_prepare")&&wifi_running);
    reset();usb_connected=false;assert(transport_benchmark_start_ble_isolated_probe());assert(!calls&&!strcmp(diagnostic,"failed:isolation_mode")&&!wifi_stops);
    reset();wifi_start_errors=10;assert(transport_benchmark_start_ble_isolated_probe());assert(calls==18&&!strcmp(diagnostic,"failed:isolation_restore")&&!wifi_running&&wifi_probe_pending());
    assert(!transport_benchmark_start());wifi_start_errors=0;vTaskDelay(1000);assert(wifi_running&&!wifi_probe_pending());
    int ranges=(firmware_size+49151)/49152;
    reset();assert(transport_benchmark_start_firmware_probe(true));assert(calls==ranges&&!strncmp(diagnostic,"complete;",9)&&strstr(diagnostic,firmware_sha)&&wifi_running&&wifi_starts==1&&!wifi_probe_pending());
    reset();assert(transport_benchmark_start_firmware_probe(false));assert(calls==ranges&&!strncmp(diagnostic,"complete;",9)&&!wifi_stops);
    reset();cost_ms=1000;assert(transport_benchmark_start_firmware_probe(true));assert(calls==110&&!strncmp(diagnostic,"failed;",7)&&wifi_running);
    reset();fail_call=ranges;assert(transport_benchmark_start_firmware_probe(true));assert(calls==ranges&&!strncmp(diagnostic,"failed;",7)&&strstr(diagnostic,",-6,")&&wifi_running);
    reset();cancel_call=ranges;assert(transport_benchmark_start_firmware_probe(true));assert(calls==ranges&&!strncmp(diagnostic,"cancelled;",10)&&wifi_running);
    reset();change_call=ranges;assert(transport_benchmark_start_firmware_probe(true));assert(calls==ranges&&!strncmp(diagnostic,"failed;",7)&&strstr(diagnostic,",-3,")&&wifi_running);
    reset();cJSON_DeleteItemFromObject(device_snapshots[LINK_BLE],"rpcOtaZlib");assert(transport_benchmark_start_firmware_probe(true));assert(!calls&&!strcmp(diagnostic,"failed:firmware_capability")&&wifi_running);
    reset();cJSON_DeleteItemFromObject(device_snapshots[LINK_BLE],"ota");assert(transport_benchmark_start_firmware_probe(true));assert(!calls&&!strcmp(diagnostic,"failed:firmware_offer")&&wifi_running);
    reset();wifi_start_errors=10;assert(transport_benchmark_start_firmware_probe(true));assert(!strcmp(diagnostic,"failed:isolation_restore")&&wifi_probe_pending());wifi_start_errors=0;vTaskDelay(1000);assert(wifi_running);
    free(firmware_bytes);cJSON_Delete(device_snapshots[LINK_BLE]);
    puts("FIRMWARE_RAM_PROBE_OK full_image_real_SHA256_last_range_corrupt_cancel_session_timeout_capability_offer_restore");
    puts("BLE_OTA_PROBE_OK three_download_rounds_only fixed_BLE_no_fallback deadline corrupt_bytes_cancel_session legacy_unchanged");return 0;
}
