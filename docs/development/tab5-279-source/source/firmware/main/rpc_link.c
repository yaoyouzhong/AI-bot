#include "rpc_link.h"
#include "device.h"
#include "esp_timer.h"
#include "esp_heap_caps.h"
#include "esp_random.h"
#include "esp_http_client.h"
#include "mbedtls/gcm.h"
#include "mbedtls/sha256.h"
#include "photo_transfer_crypto.h"
#include "photo_display_probe.h"
#include "camera_viewport.h"
#include "mbedtls/base64.h"
#include <stdlib.h>
#include <string.h>
#include <stdio.h>
#include "rpc_mailbox.inc"
#include "rpc_binary.h"
#include "rpc_reply.h"
#include "gallery_stream.h"
#include "gallery_read_pipeline.h"
#include "miniz.h"
#include "ota_zlib.h"
#include "wifi_probe.h"
#include "wifi_status.h"
#include <stdatomic.h>
static atomic_int rpc_failure_stage[3],rpc_failure_error[3];
static atomic_uint rpc_failures[3],rpc_failure_bytes[3];
static void rpc_failure(link_kind link,int stage,int error,size_t bytes) {
    atomic_store(&rpc_failure_stage[link],stage);atomic_store(&rpc_failure_error[link],error);
    atomic_store(&rpc_failure_bytes[link],(unsigned)bytes);atomic_fetch_add(&rpc_failures[link],1);
}
void rpc_failure_diagnostics(char *out,size_t size) {
    snprintf(out,size,"%u,%d,%d,%u;%u,%d,%d,%u;%u,%d,%d,%u",
        atomic_load(&rpc_failures[0]),atomic_load(&rpc_failure_stage[0]),atomic_load(&rpc_failure_error[0]),atomic_load(&rpc_failure_bytes[0]),
        atomic_load(&rpc_failures[1]),atomic_load(&rpc_failure_stage[1]),atomic_load(&rpc_failure_error[1]),atomic_load(&rpc_failure_bytes[1]),
        atomic_load(&rpc_failures[2]),atomic_load(&rpc_failure_stage[2]),atomic_load(&rpc_failure_error[2]),atomic_load(&rpc_failure_bytes[2]));
}
static rpc_profile profiles[3];
// Gallery calls are serialized by its worker; snapshot only between samples.
static rpc_profile gallery_profiles;
static uint64_t gallery_backpressure_us;
static uint64_t gallery_network_read_us,gallery_consumer_wait_us;
static unsigned gallery_receive_ahead_calls,gallery_receive_peak_bytes;
void rpc_gallery_profile_reset(void){memset(&gallery_profiles,0,sizeof(gallery_profiles));gallery_backpressure_us=gallery_network_read_us=gallery_consumer_wait_us=0;gallery_receive_ahead_calls=gallery_receive_peak_bytes=0;}
gallery_rpc_profile rpc_gallery_profile_get(void){return (gallery_rpc_profile){gallery_profiles.encode_us,gallery_profiles.exchange_us,gallery_profiles.decode_us,gallery_backpressure_us,gallery_profiles.calls,gallery_network_read_us,gallery_consumer_wait_us,gallery_receive_ahead_calls,gallery_receive_peak_bytes};}
static void profile_memory(rpc_profile *p) {
    // A skipped sample must not turn an unknown largest block into a zero minimum.
    if(camera_viewport_still_active())return;
    unsigned internal=heap_caps_get_free_size(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT);
    unsigned psram=heap_caps_get_free_size(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    if(internal<p->min_internal)p->min_internal=internal;
    if(psram<p->min_psram)p->min_psram=psram;
    // Keep min_largest unknown; per-chunk diagnostics must not walk the heap.
}
void rpc_profile_reset(link_kind link){if(link<0||link>2)return;profiles[link]=(rpc_profile){.min_internal=UINT32_MAX,.min_psram=UINT32_MAX,.min_largest=UINT32_MAX};}
rpc_profile rpc_profile_get(link_kind link){return link>=0&&link<3?profiles[link]:(rpc_profile){0};}
static SemaphoreHandle_t rpc_lock;
static SemaphoreHandle_t rpc_ready_usb;
static rpc_mailbox boxes[2];
static SemaphoreHandle_t rpc_done[2];
static SemaphoreHandle_t completion(link_kind link){return link==LINK_USB?rpc_done[0]:link==LINK_BLE?rpc_done[1]:NULL;}
static rpc_mailbox *box(link_kind link){return link==LINK_USB?&boxes[0]:link==LINK_BLE?&boxes[1]:NULL;}
void rpc_link_init(void) {
    if(rpc_lock)return;
    rpc_lock=xSemaphoreCreateMutex();
    rpc_ready_usb=xSemaphoreCreateBinary();
    for(int i=0;i<2;i++) {
        boxes[i].out=heap_caps_malloc(RPC_REQUEST_MAX,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
        boxes[i].in=heap_caps_malloc(RPC_RESPONSE_MAX,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    }
}
void rpc_link_reset(link_kind link) {
    rpc_mailbox *m=box(link);if(!rpc_lock||!m)return;
    xSemaphoreTake(rpc_lock,portMAX_DELAY);m->generation++;m->active=0;m->peer_seen=0;m->confirmed_ms=0;m->confirmed_session[0]=0;m->complete=false;
    if(completion(link))xSemaphoreGive(completion(link));
    xSemaphoreGive(rpc_lock);
    if(link==LINK_USB&&rpc_ready_usb)xSemaphoreGive(rpc_ready_usb);
}
size_t rpc_link_read(link_kind link,uint8_t *out,size_t capacity) {
    rpc_mailbox *m=box(link);if(!rpc_lock||!m||!m->out||!m->in||!device_get_config().paired)return 0;
    xSemaphoreTake(rpc_lock,portMAX_DELAY);m->peer_seen=esp_timer_get_time()/1000;
    size_t n=rpc_read(m,out,capacity);xSemaphoreGive(rpc_lock);return n;
}
bool rpc_usb_wait_supported(void){return rpc_lock&&rpc_ready_usb&&boxes[0].out&&boxes[0].in;}
size_t rpc_link_read_wait(link_kind link,uint8_t *out,size_t capacity,unsigned wait_ms) {
    if(link!=LINK_USB||wait_ms!=20||!rpc_usb_wait_supported())return rpc_link_read(link,out,capacity);
    if(!device_get_config().paired||!tab5_usb_connected())return 0;
    xSemaphoreTake(rpc_lock,portMAX_DELAY);uint32_t generation=boxes[0].generation;xSemaphoreGive(rpc_lock);
    int64_t deadline=esp_timer_get_time()/1000+wait_ms;
    for(;;) {
        xSemaphoreTake(rpc_lock,portMAX_DELAY);
        bool same=boxes[0].generation==generation&&tab5_usb_connected();
        bool pending=boxes[0].active&&!boxes[0].complete&&boxes[0].offset<boxes[0].size;
        xSemaphoreGive(rpc_lock);
        if(!same)return 0;
        if(pending)return rpc_link_read(link,out,capacity);
        int64_t remaining=deadline-esp_timer_get_time()/1000;
        if(remaining<=0)return rpc_link_read(link,out,capacity);
        // Empty/stale notifications never turn into an active polling loop.
        // A new request or reset wakes this bounded wait immediately.
        unsigned ticks=pdMS_TO_TICKS((unsigned)remaining);if(!ticks)ticks=1;
        xSemaphoreTake(rpc_ready_usb,ticks);
    }
}
bool rpc_link_write(link_kind link,const uint8_t *data,size_t size) {
    rpc_mailbox *m=box(link);if(!rpc_lock||!m||!m->in||!device_get_config().paired)return false;
    xSemaphoreTake(rpc_lock,portMAX_DELAY);bool ok=link==LINK_BLE?rpc_write_with_replay(m,data,size,true):rpc_write(m,data,size);
    if(ok){
        m->peer_seen=esp_timer_get_time()/1000;
        if(m->complete&&completion(link))xSemaphoreGive(completion(link));
    }
    xSemaphoreGive(rpc_lock);return ok;
}
static link_kind select_link(bool read_only,bool bulk_wifi) {
    int64_t now=esp_timer_get_time()/1000;link_state links;
    xSemaphoreTake(device_mutex,portMAX_DELAY);links=device_links;xSemaphoreGive(device_mutex);
    const link_kind normal[]={LINK_USB,LINK_WIFI,LINK_BLE},bulk[]={LINK_WIFI,LINK_USB,LINK_BLE};
    const link_kind *order=bulk_wifi?bulk:normal;
    for(int index=0;index<3;index++) {
        int i=order[index];
        if(i==LINK_USB&&!tab5_usb_connected())continue;
        if(i==LINK_BLE&&(!ble_connected||!ble_host_authorized()))continue;
        if((links.mode!=-1&&links.mode!=i)||links.received_ms[i]<0||now<links.received_ms[i])continue;
        bool fresh=now-links.received_ms[i]<8000;
        if(i==LINK_WIFI){if(fresh&&wifi_connected)return LINK_WIFI;continue;}
        if(!rpc_lock)continue;
        xSemaphoreTake(rpc_lock,portMAX_DELAY);rpc_mailbox *m=box(i);
        if(read_only)fresh=link_state_read_fresh(&links,i,now,m->confirmed_ms,m->confirmed_session);
        bool ready=fresh&&m->out&&m->in&&m->peer_seen&&now>=m->peer_seen&&now-m->peer_seen<8000;xSemaphoreGive(rpc_lock);
        if(ready)return i;
    }
    return LINK_NONE;
}
link_kind rpc_link_select(void){return select_link(false,false);}
link_kind rpc_read_link_select(void){return select_link(true,false);}
link_kind rpc_library_link_select(void){return select_link(true,true);}
// Wi-Fi streaming is faster than the current USB mailbox protocol. Do not
// change daily mode or switch transports halfway through an installation.
link_kind rpc_ota_link_select(void) {
    int64_t now=esp_timer_get_time()/1000;link_state links;bool usb_ready=false,ble_ready=false;
    xSemaphoreTake(device_mutex,portMAX_DELAY);links=device_links;xSemaphoreGive(device_mutex);
    if(rpc_lock) {
        xSemaphoreTake(rpc_lock,portMAX_DELAY);rpc_mailbox *m=box(LINK_USB);
        usb_ready=tab5_usb_connected()&&m->out&&m->in&&m->peer_seen&&now>=m->peer_seen&&now-m->peer_seen<8000;
        m=box(LINK_BLE);ble_ready=ble_connected&&ble_host_authorized()&&m->out&&m->in&&m->peer_seen&&now>=m->peer_seen&&now-m->peer_seen<8000;
        xSemaphoreGive(rpc_lock);
    }
    return link_state_ota_bulk(&links,now,wifi_connected,usb_ready,ble_ready);
}
bool rpc_link_ready(void){return rpc_link_select()!=LINK_NONE;}
link_kind rpc_image_link_select(const char *session) {
    int64_t now=esp_timer_get_time()/1000;link_state links;bool ready[2]={false,false};
    xSemaphoreTake(device_mutex,portMAX_DELAY);links=device_links;xSemaphoreGive(device_mutex);
    if(rpc_lock) {
        xSemaphoreTake(rpc_lock,portMAX_DELAY);
        for(int i=0;i<2;i++){rpc_mailbox *m=&boxes[i];ready[i]=m->out&&m->in&&m->peer_seen&&now>=m->peer_seen&&now-m->peer_seen<8000;}
        xSemaphoreGive(rpc_lock);
    }
    return link_state_image(&links,now,session,wifi_connected,ready[0]&&tab5_usb_connected(),ready[1]&&ble_connected&&ble_host_authorized());
}
bool rpc_read_link_ready(void){return rpc_read_link_select()!=LINK_NONE;}
static uint8_t *exchange(link_kind link,const char *nonce,const char *proof,const uint8_t *data,size_t size,size_t *reply_size,uint32_t generation,unsigned timeout_ms,unsigned admission_ms,unsigned host_generation,int *status) {
    *reply_size=0;rpc_mailbox *m=box(link);
    if(!rpc_lock||!m||!m->out||!m->in||size+96>RPC_REQUEST_MAX)return NULL;
    int64_t began=esp_timer_get_time()/1000,deadline=began+timeout_ms;
    // A credential read can briefly follow a status/control RPC on the same
    // mailbox. Wait only for admission, before publishing any nonce or bytes.
    // This never retries a credential request or an HID key sequence.
    for(;;) {
        xSemaphoreTake(rpc_lock,portMAX_DELAY);
        int64_t now=esp_timer_get_time()/1000;
        bool current=m->generation==generation&&host_generation==device_host_epoch()&&!device_host_changing()&&
            (link!=LINK_USB||tab5_usb_connected())&&
            (link!=LINK_BLE||(ble_connected&&ble_host_authorized()));
        if(!current||now>=deadline){xSemaphoreGive(rpc_lock);return NULL;}
        if(!m->active)break;
        xSemaphoreGive(rpc_lock);
        if(now-began>=admission_ms){*status=RPC_STATUS_UNPUBLISHED_BUSY;return NULL;}
        vTaskDelay(pdMS_TO_TICKS(10));
    }
    // Own each wakeup until detachment; reconnect cannot steal it. IDF's
    // FreeRTOS allocator keeps the object in internal RAM, even when the
    // calling task uses a PSRAM stack (which cannot host this control object).
    SemaphoreHandle_t done=xSemaphoreCreateBinary();
    rpc_done[link==LINK_USB?0:1]=done;
    memcpy(m->out,nonce,32);memcpy(m->out+32,proof,64);memcpy(m->out+96,data,size);
    m->size=size+96;m->offset=m->offered=m->received=m->expected=0;m->complete=false;
    if(!++m->serial)++m->serial;
    uint32_t serial=m->active=m->serial;xSemaphoreGive(rpc_lock);
    if(link==LINK_USB&&rpc_ready_usb)xSemaphoreGive(rpc_ready_usb);
    uint8_t *reply=NULL;
    while(esp_timer_get_time()/1000<deadline) {
        xSemaphoreTake(rpc_lock,portMAX_DELAY);
        if(m->active!=serial||host_generation!=device_host_epoch()||device_host_changing()||
           (link==LINK_BLE&&(!ble_connected||!ble_host_authorized()))){xSemaphoreGive(rpc_lock);break;}
        if(m->complete) {
            reply=heap_caps_malloc(m->expected,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
            if(reply){memcpy(reply,m->in,m->expected);*reply_size=m->expected;}
            xSemaphoreGive(rpc_lock);break;
        }
        xSemaphoreGive(rpc_lock);
        // Complete/reset wake the owning request immediately. Keep the original
        // bounded deadline and a low-memory fallback, never poll faster in a loop.
        int64_t remaining=deadline-esp_timer_get_time()/1000;
        if(remaining<=0)break;
        if(done){if(xSemaphoreTake(done,pdMS_TO_TICKS(remaining))!=pdTRUE)break;}
        else vTaskDelay(pdMS_TO_TICKS(10));
    }
    xSemaphoreTake(rpc_lock,portMAX_DELAY);
    if(m->active==serial)m->active=0;
    if(completion(link)==done)rpc_done[link==LINK_USB?0:1]=NULL;
    xSemaphoreGive(rpc_lock);
    if(done)vSemaphoreDelete(done);
    return reply;
}
static uint8_t *http_exchange(esp_http_client_handle_t client,const device_config *cfg,const char *nonce,const char *proof,const uint8_t *packet,size_t size,size_t *reply_size,int *status) {
    *reply_size=0;*status=0;
    // Every authenticated RPC uses POST, including gallery callers whose HTTP
    // client starts with the SDK's default GET method.
    if(esp_http_client_set_method(client,HTTP_METHOD_POST)!=ESP_OK){esp_http_client_close(client);return NULL;}
    esp_http_client_set_header(client,"X-AIBot-Device",cfg->id);esp_http_client_set_header(client,"X-AIBot-Nonce",nonce);esp_http_client_set_header(client,"X-AIBot-Proof",proof);
    if(esp_http_client_open(client,size)!=ESP_OK){esp_http_client_close(client);return NULL;}
    size_t sent=0;
    while(sent<size){int n=esp_http_client_write(client,(char *)packet+sent,size-sent);if(n<=0){esp_http_client_close(client);return NULL;}sent+=n;}
    int64_t length=esp_http_client_fetch_headers(client);
    int code=esp_http_client_get_status_code(client);
    if(code!=200){*status=code;esp_http_client_close(client);return NULL;}
    if(length<28||length>RPC_RESPONSE_MAX){esp_http_client_close(client);return NULL;}
    uint8_t *reply=heap_caps_malloc(length,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);if(!reply){esp_http_client_close(client);return NULL;}
    size_t used=0;
    while(used<(size_t)length){int n=esp_http_client_read(client,(char *)reply+used,length-used);if(n<=0){free(reply);esp_http_client_close(client);return NULL;}used+=n;}
    *reply_size=used;return reply;
}
typedef struct {esp_http_client_handle_t http;rpc_gallery_progress progress;void *context;uint64_t decode_us,progress_us;
    gallery_read_pipeline ahead;size_t wire;bool ahead_active;uint64_t read_us,wait_us;unsigned peak;} gallery_http_reader;
static void gallery_http_receive_task(void *context) {
    gallery_http_reader *reader=context;size_t remaining=reader->wire;
    while(remaining&&!atomic_load(&reader->ahead.stop)) {
        uint8_t *out;size_t size=gallery_read_write_span(&reader->ahead,&out);
        if(!size){vTaskDelay(pdMS_TO_TICKS(1));continue;}
        if(size>remaining)size=remaining;
        int64_t began=esp_timer_get_time();int n=esp_http_client_read(reader->http,(char *)out,size);
        reader->read_us+=esp_timer_get_time()-began;
        if(n<=0||(size_t)n>size){atomic_store(&reader->ahead.failed,true);break;}
        remaining-=n;gallery_read_publish(&reader->ahead,(size_t)n);
        size_t lead=reader->wire-remaining-atomic_load(&reader->ahead.consumed);
        if(lead>reader->peak)reader->peak=(unsigned)lead;
    }
    // Final access to this stack-owned reader. Its owner waits before closing
    // HTTP/freeing the buffer; no task is force-deleted on errors.
    atomic_store_explicit(&reader->ahead.finished,true,memory_order_release);
    vTaskDelete(NULL);
}
static int gallery_http_read(void *context,uint8_t *out,size_t size) {
    gallery_http_reader *reader=context;
    if(!reader->ahead_active)return esp_http_client_read(reader->http,(char *)out,size);
    int64_t waiting=0;
    for(;;) {
        int n=gallery_read_poll(&reader->ahead,out,size);
        if(n!=-2){if(waiting)reader->wait_us+=esp_timer_get_time()-waiting;return n;}
        if(!waiting)waiting=esp_timer_get_time();
        vTaskDelay(pdMS_TO_TICKS(1));
    }
}
static char *gallery_http_decrypt(void *context,const uint8_t *packet,size_t size,const char *nonce,int *error) {
    gallery_http_reader *reader=context;int64_t began=esp_timer_get_time();
    char *plain=device_decrypt_checked(packet,size,nonce,RPC_RESPONSE_MAX,error);
    reader->decode_us+=esp_timer_get_time()-began;return plain;
}
static bool gallery_http_progress(void *context,size_t verified) {
    gallery_http_reader *reader=context;int64_t began=esp_timer_get_time();
    bool ok=!reader->progress||reader->progress(reader->context,verified);
    reader->progress_us+=esp_timer_get_time()-began;return ok;
}
static cJSON *http_gallery_exchange(esp_http_client_handle_t http,const device_config *cfg,const char *nonce,const char *proof,
    const uint8_t *packet,size_t size,cJSON *request,uint8_t *out,size_t capacity,size_t *received,int *status,
    rpc_gallery_progress progress,void *context) {
    *status=0;*received=0;
    if(esp_http_client_set_method(http,HTTP_METHOD_POST)!=ESP_OK)return NULL;
    esp_http_client_set_header(http,"X-AIBot-Device",cfg->id);esp_http_client_set_header(http,"X-AIBot-Nonce",nonce);esp_http_client_set_header(http,"X-AIBot-Proof",proof);
    if(esp_http_client_open(http,size)!=ESP_OK){esp_http_client_close(http);return NULL;}
    for(size_t at=0;at<size;){int n=esp_http_client_write(http,(char *)packet+at,size-at);if(n<=0){esp_http_client_close(http);return NULL;}at+=n;}
    int64_t length=esp_http_client_fetch_headers(http);int code=esp_http_client_get_status_code(http);
    if(code!=200||length<28||length>GALLERY_STREAM_IMAGE_MAX+32u*(4+4096+8+28)+16){*status=code==200?-1004:code;esp_http_client_close(http);return NULL;}
    cJSON *offset=cJSON_GetObjectItemCaseSensitive(request,"offset"),*count=cJSON_GetObjectItemCaseSensitive(request,"count");
    if(!cJSON_IsNumber(offset)||!cJSON_IsNumber(count)||offset->valuedouble!=offset->valueint||count->valuedouble!=count->valueint||offset->valueint<0||count->valueint<1){esp_http_client_close(http);return NULL;}
    gallery_http_reader reader={.http=http,.progress=progress,.context=context};
    uint8_t *ahead=heap_caps_malloc(GALLERY_HTTP_AHEAD_BYTES,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    if(ahead) {
        gallery_read_init(&reader.ahead,ahead,GALLERY_HTTP_AHEAD_BYTES);reader.wire=(size_t)length;
        reader.ahead_active=xTaskCreate(gallery_http_receive_task,"gallery_http_rx",4096,&reader,4,NULL)==pdPASS;
        if(!reader.ahead_active){free(ahead);ahead=NULL;}
    }
    cJSON *result=gallery_stream_receive(gallery_http_read,gallery_http_decrypt,gallery_http_progress,&reader,length,nonce,
        out,capacity,offset->valueint,count->valueint,status,received);
    if(reader.ahead_active) {
        atomic_store(&reader.ahead.stop,true);
        // The SDK handle is never cleaned up concurrently. A stalled socket
        // retains the existing eight-second I/O timeout.
        while(!atomic_load_explicit(&reader.ahead.finished,memory_order_acquire))vTaskDelay(pdMS_TO_TICKS(1));
        gallery_network_read_us+=reader.read_us;gallery_consumer_wait_us+=reader.wait_us;gallery_receive_ahead_calls++;
        if(reader.peak>gallery_receive_peak_bytes)gallery_receive_peak_bytes=reader.peak;
        free(ahead);
    }
    gallery_profiles.decode_us+=reader.decode_us;gallery_backpressure_us+=reader.progress_us;
    if(!result)esp_http_client_close(http);
    return result;
}
bool rpc_gallery_stream_ready(void) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    cJSON *health=cJSON_GetObjectItemCaseSensitive(device_snapshots[LINK_WIFI],"connectionHealth");
    cJSON *version=cJSON_GetObjectItemCaseSensitive(health,"galleryStream");
    bool ok=cJSON_IsNumber(version)&&version->valuedouble==1;
    xSemaphoreGive(device_mutex);return ok;
}
// Admission waits before publishing a nonce; it never replays a request.
// Local unlock leases share the mailbox with already-running status reads.
static unsigned rpc_exchange_admission(const char *kind) {
    if(kind&&!strcmp(kind,"voice"))return 2000;
    if(kind&&!strcmp(kind,"screen-preview"))return 100;
    if(kind&&!strcmp(kind,"computer-control"))return 1000;
    return kind&&(!strcmp(kind,"unlock-pin")||!strcmp(kind,"host-unlock"))?1000:0;
}
static unsigned rpc_exchange_timeout(const char *kind,bool ota_probe) {
    if(kind&&!strcmp(kind,"screen-preview"))return 3000;
    if(ota_probe)return 5000;
    if(kind&&!strcmp(kind,"host-unlock"))return 2000;
    if(kind&&(!strcmp(kind,"boot-verify")||!strcmp(kind,"runtime-diagnostics")))return 2000;
    if(kind&&!strcmp(kind,"gallery"))return 8000;
    if(kind&&(!strcmp(kind,"ota")||!strcmp(kind,"ota_probe")))return 10000;
    return 75000;
}
// Size optimization is limited to RPC envelope construction/dispatch.
// Crypto, transport and display/codec hot loops keep their existing flags.
#pragma GCC push_options
#pragma GCC optimize ("Os")
static cJSON *rpc_call_impl(link_kind link,cJSON *request,int *status,char nonce[33],esp_http_client_handle_t http,
    const uint8_t *upload,size_t upload_size,uint8_t *download,size_t capacity,size_t *received,rpc_gallery_progress progress,void *progress_context) {
    if(received)*received=0;
    if((upload&&(!upload_size||upload_size>RPC_BULK_DATA||cJSON_HasObjectItem(request,"data")))||(download&&!received)){*status=0;return NULL;}
    unsigned host_generation=device_host_epoch();
    *status=0;device_config cfg=device_get_config();if(!cfg.paired)return NULL;
    if(link==LINK_USB&&!device_host_usb_owned())return NULL;
    if(link==LINK_BLE&&(!ble_connected||!ble_host_authorized()||device_host_changing()))return NULL;
    if(link!=LINK_USB&&link!=LINK_BLE&&!(link==LINK_WIFI&&http&&wifi_connected))return NULL;
    const char *kind=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(request,"kind"));
    // RAM benchmark owns these counters; normal user traffic is excluded.
    rpc_profile *profile=kind&&!strcmp(kind,"gallery")?&gallery_profiles:kind&&(!strcmp(kind,"benchmark")||!strcmp(kind,"ota_probe"))?&profiles[link]:NULL;
    int64_t profile_start=esp_timer_get_time();
    if(profile)profile_memory(profile);
    if(!rpc_lock||(link==LINK_USB&&!tab5_usb_connected()))return NULL;
    extern unsigned usb_keyboard_epoch(void);
    extern void usb_keyboard_authenticated(unsigned epoch,unsigned host_generation);
    unsigned usb_epoch=link==LINK_USB?usb_keyboard_epoch():0;
    xSemaphoreTake(rpc_lock,portMAX_DELAY);uint32_t generation=box(link)?box(link)->generation:0;xSemaphoreGive(rpc_lock);
    // Use the paired bridge's latest clock/session, including during a long
    // image upload. Never extend an old request's authorization window.
    char session[40];double epoch=0;int64_t age,received_at;bool binary=false,bulk=false;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    snprintf(session,sizeof(session),"%s",device_links.session[link]);
    cJSON *cap=cJSON_GetObjectItemCaseSensitive(device_snapshots[link],"rpcBinary");binary=cJSON_IsNumber(cap)&&cap->valuedouble==1;
    cap=cJSON_GetObjectItemCaseSensitive(device_snapshots[link],"rpcBulk");bulk=binary&&cJSON_IsNumber(cap)&&cap->valuedouble==1;
    cJSON *clock=cJSON_GetObjectItemCaseSensitive(device_snapshots[link],"epochMilliseconds");
    if(cJSON_IsNumber(clock))epoch=clock->valuedouble;
    received_at=device_links.received_ms[link];
    age=esp_timer_get_time()/1000-received_at;
    xSemaphoreGive(device_mutex);
    if(!session[0]||epoch<=0||received_at<0||age<0||age>90000)return NULL;
    if(upload_size>(bulk?RPC_BULK_DATA:8192))return NULL;
    cJSON *old_session=cJSON_GetObjectItemCaseSensitive(request,"session");
    if(cJSON_IsString(old_session)&&strcmp(old_session->valuestring,session))return NULL;
    cJSON_DeleteItemFromObjectCaseSensitive(request,"session");cJSON_AddStringToObject(request,"session",session);
    cJSON_DeleteItemFromObjectCaseSensitive(request,"issuedAt");cJSON_AddNumberToObject(request,"issuedAt",epoch+age);
    cJSON_DeleteItemFromObjectCaseSensitive(request,"binaryReply");
    if(binary)cJSON_AddBoolToObject(request,"binaryReply",true);
    cJSON_DeleteItemFromObjectCaseSensitive(request,"bulkReply");
    if(bulk)cJSON_AddBoolToObject(request,"bulkReply",true);
    device_nonce(nonce);size_t n=0;char *clear=NULL;
    if(upload&&binary) {clear=(char*)rpc_binary_pack_bulk(request,upload,upload_size,&n,bulk);if(!clear)return NULL;}
    else {
        if(upload) {
            size_t encoded_size=4*((upload_size+2)/3)+1,written=0;
            unsigned char *encoded=malloc(encoded_size);if(!encoded)return NULL;
            int result=mbedtls_base64_encode(encoded,encoded_size,&written,upload,upload_size);
            if(result){free(encoded);return NULL;}encoded[written]=0;
            cJSON *added=cJSON_AddStringToObject(request,"data",(char*)encoded);free(encoded);if(!added)return NULL;
        }
        clear=binary?(char*)rpc_binary_encode(request,&n):NULL;
        if(!clear){clear=cJSON_PrintUnformatted(request);if(clear)n=strlen(clear);}
        if(upload)cJSON_DeleteItemFromObjectCaseSensitive(request,"data");
        if(!clear)return NULL;
    }
    uint8_t *packet=n+124<=RPC_REQUEST_MAX?heap_caps_malloc(n+28,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT):NULL;
    if(!packet){free(clear);return NULL;}
    bool photo_upload=kind&&!strcmp(kind,"image-chunk");
    esp_fill_random(packet,12);uint8_t hash[32];int err;
    if(photo_upload) {
        err=photo_tx_encrypt(cfg.key,packet,(uint8_t *)nonce,(uint8_t *)clear,n,packet+28,packet+12);
        if(!err)err=photo_tx_sha256(packet,n+28,hash);
    }else if(kind&&!strcmp(kind,"screen-preview")) {
        err=rpc_payload_encrypt(cfg.key,packet,(uint8_t *)nonce,(uint8_t *)clear,n,packet+28,packet+12);
        if(!err)err=rpc_payload_sha256(packet,n+28,hash);
    }else {
        mbedtls_gcm_context ctx;mbedtls_gcm_init(&ctx);
        err=mbedtls_gcm_setkey(&ctx,MBEDTLS_CIPHER_ID_AES,cfg.key,256);
        if(!err)err=mbedtls_gcm_crypt_and_tag(&ctx,MBEDTLS_GCM_ENCRYPT,n,packet,12,(uint8_t *)nonce,32,(uint8_t *)clear,packet+28,16,packet+12);
        mbedtls_gcm_free(&ctx);if(!err)mbedtls_sha256(packet,n+28,hash,0);
    }
    free(clear);if(err){free(packet);if(photo_upload)*status=-1010;return NULL;}
    char hex[65],auth[180],proof[65];
    for(int i=0;i<32;i++)sprintf(hex+i*2,"%02x",hash[i]);
    snprintf(auth,sizeof(auth),"POST|/tab5/v1/rpc|%s|%s|%s",cfg.id,nonce,hex);device_proof(auth,proof);
    int64_t exchange_start=esp_timer_get_time();
    if(profile){profile->encode_us+=exchange_start-profile_start;profile->wire_tx+=n+28;profile_memory(profile);}
    bool ota_probe=kind&&!strcmp(kind,"benchmark")&&cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(request,"otaProbe"));
    if(http&&link==LINK_WIFI&&kind&&!strcmp(kind,"gallery")&&download&&
       cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(request,"streamReply"))) {
        uint64_t before_decode=gallery_profiles.decode_us,before_progress=gallery_backpressure_us;
        cJSON *result=http_gallery_exchange(http,&cfg,nonce,proof,packet,n+28,request,download,capacity,received,status,progress,progress_context);
        gallery_profiles.exchange_us+=esp_timer_get_time()-exchange_start-(gallery_profiles.decode_us-before_decode)-(gallery_backpressure_us-before_progress);
        gallery_profiles.calls++;
        free(packet);return result;
    }
    unsigned admission_ms=rpc_exchange_admission(kind);
    // Photo ranges are immutable and idempotent; bound a stalled BLE chunk
    // separately from general commands so connection recovery can run promptly.
    unsigned timeout_ms=link==LINK_BLE&&kind&&!strcmp(kind,"image-chunk")?15000:rpc_exchange_timeout(kind,ota_probe);
    if(photo_upload){photo_tx_phase(PHOTO_TX_EXCHANGE);photo_display_probe_mark(PHOTO_DISPLAY_PROBE_NETWORK);}
    size_t reply_size=0;uint8_t *reply=http?http_exchange(http,&cfg,nonce,proof,packet,n+28,&reply_size,status):exchange(link,nonce,proof,packet,n+28,&reply_size,generation,timeout_ms,admission_ms,host_generation,status);
    int64_t decode_start=esp_timer_get_time();
    if(profile){profile->exchange_us+=decode_start-exchange_start;profile->wire_rx+=reply_size;profile_memory(profile);}
    free(packet);
    int decode_error=0;
    char *plain=reply?device_decrypt_checked(reply,reply_size,nonce,bulk?RPC_RESPONSE_MAX:TAB5_LEGACY_FRAME,&decode_error):NULL;
    if(!reply)rpc_failure(link,1,*status,reply_size);
    else if(!plain){*status=decode_error;rpc_failure(link,2,decode_error,reply_size);}
    free(reply);
    bool decoded=plain!=NULL;cJSON *root=plain?rpc_binary_unpack(plain,reply_size-28,download,capacity,received):NULL;free(plain);
    if(decoded&&!root){*status=-1004;rpc_failure(link,3,*status,reply_size);}
    cJSON *result=NULL;
    if(root){result=rpc_reply_body(root,status);if(!result)rpc_failure(link,4,*status,reply_size);}
    if(result&&box(link)) {
        xSemaphoreTake(rpc_lock,portMAX_DELAY);bool same=box(link)->generation==generation;xSemaphoreGive(rpc_lock);
        if(!same){cJSON_Delete(result);result=NULL;*status=-1005;if(received)*received=0;rpc_failure(link,5,*status,reply_size);}
    }
    if(result&&host_generation!=device_host_epoch()){cJSON_Delete(result);result=NULL;*status=-1005;}
    if(!result&&received)*received=0;
    if(result&&*status==200&&link!=LINK_WIFI) {
        xSemaphoreTake(rpc_lock,portMAX_DELAY);rpc_mailbox *m=box(link);
        // Disconnect resets peer_seen; do not revive a mailbox after its reset.
        bool confirmed=m->peer_seen&&m->generation==generation;
        if(confirmed){m->confirmed_ms=esp_timer_get_time()/1000;snprintf(m->confirmed_session,sizeof(m->confirmed_session),"%s",session);}
        xSemaphoreGive(rpc_lock);
        // Plain CDC telemetry is not proof of the paired bridge. Only a
        // decrypted, nonce-bound reply can enable HID on this USB generation.
        if(confirmed&&link==LINK_USB){device_host_usb_touch();usb_keyboard_authenticated(usb_epoch,host_generation);}
    }
    if(profile){profile_memory(profile);profile->decode_us+=esp_timer_get_time()-decode_start;profile->calls++;}
    cJSON_Delete(root);return result;
}
#pragma GCC pop_options
cJSON *rpc_call(link_kind link,cJSON *request,int *status,char nonce[33]) {return rpc_call_impl(link,request,status,nonce,NULL,NULL,0,NULL,0,NULL,NULL,NULL);}
cJSON *rpc_http_call(void *client,cJSON *request,int *status,char nonce[33]) {return rpc_call_impl(LINK_WIFI,request,status,nonce,client,NULL,0,NULL,0,NULL,NULL,NULL);}
#include "rpc_bulk_radio.inc"
cJSON *rpc_data_call(link_kind link,void *http,cJSON *request,int *status,char nonce[33],
    const uint8_t *upload,size_t upload_size,uint8_t *download,size_t capacity,size_t *received) {
    bool radio_owner=rpc_bulk_radio_begin(link,request,upload_size,capacity);
    cJSON *result=rpc_call_impl(link,request,status,nonce,http,upload,upload_size,download,capacity,received,NULL,NULL);
    rpc_bulk_radio_end(radio_owner,result&&*status==200);
    return result;
}
cJSON *rpc_gallery_stream_call(void *http,cJSON *request,int *status,char nonce[33],uint8_t *download,size_t capacity,size_t *received,rpc_gallery_progress progress,void *context) {
    cJSON *op=cJSON_GetObjectItemCaseSensitive(request,"op"),*kind=cJSON_GetObjectItemCaseSensitive(request,"kind");
    if(!http||!rpc_gallery_stream_ready()||!cJSON_IsString(kind)||strcmp(kind->valuestring,"gallery")||!cJSON_IsString(op)||strcmp(op->valuestring,"read")){*status=0;*received=0;return NULL;}
    if(!cJSON_AddBoolToObject(request,"streamReply",true)){*status=0;*received=0;return NULL;}
    cJSON *result=rpc_call_impl(LINK_WIFI,request,status,nonce,http,NULL,0,download,capacity,received,progress,context);
    cJSON_DeleteItemFromObjectCaseSensitive(request,"streamReply");return result;
}
static unsigned ota_payload_bytes[3];
void rpc_ota_reset_bytes(link_kind link) {if(link>=LINK_USB&&link<=LINK_BLE)ota_payload_bytes[link]=0;}
unsigned rpc_ota_received_bytes(link_kind link) {return link>=LINK_USB&&link<=LINK_BLE?ota_payload_bytes[link]:0;}
static int rpc_ota_range(link_kind link,void *http,const char *sha,const char *offer,int offset,uint8_t *out,int count,bool probe) {
    static int maximum[3]={16384,8192,8192};
    static bool rejected_bulk[3];
    if(link!=LINK_USB&&link!=LINK_BLE&&!(link==LINK_WIFI&&http))return -1;
    int limit=!rejected_bulk[link]&&rpc_data_limit(link)>8192?(int)RPC_BULK_DATA:maximum[link];
    if(count>limit)count=limit;
    cJSON *request=cJSON_CreateObject();cJSON_AddStringToObject(request,"kind",probe?"ota_probe":"ota");
    cJSON_AddStringToObject(request,"sha256",sha);cJSON_AddStringToObject(request,"offerId",offer);
    cJSON_AddNumberToObject(request,"offset",offset);cJSON_AddNumberToObject(request,"count",count);
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    cJSON *cap=cJSON_GetObjectItemCaseSensitive(device_snapshots[link],"rpcOtaZlib");
    bool zlib_supported=cJSON_IsNumber(cap)&&cap->valuedouble==1;
    xSemaphoreGive(device_mutex);
    bool compressed=limit==(int)RPC_BULK_DATA&&zlib_supported;
    uint8_t *packed=compressed?heap_caps_malloc(count,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT):NULL;
    if(compressed&&!packed){cJSON_Delete(request);return -1;}
    if(compressed)cJSON_AddStringToObject(request,"acceptEncoding","zlib");
    int status=0;size_t size=0;char nonce[33];cJSON *reply=NULL;
    // Only this immutable image range can be repeated. A new nonce is used
    // each time; commands, draft submission and voice RPCs are never retried.
    for(unsigned attempt=0;attempt<(probe?3u:1u);attempt++) {
        reply=rpc_data_call(link,http,request,&status,nonce,NULL,0,packed?packed:out,count,&size);
        if(!probe&&size<=(size_t)count)ota_payload_bytes[link]+=(unsigned)size;
        if(status||!probe||attempt==2||(link==LINK_USB&&!tab5_usb_connected())||(link==LINK_BLE&&!ble_connected))break;
        printf("TAB5_OTA_RANGE_RETRY link=%d offset=%d count=%d attempt=%u\n",link,offset,count,attempt+1);
        cJSON_Delete(reply);reply=NULL;vTaskDelay(pdMS_TO_TICKS(100*(attempt+1)));
    }
    cJSON_Delete(request);
    cJSON *error=cJSON_GetObjectItemCaseSensitive(reply,"error");
    if(count>8192&&status==400&&cJSON_IsString(error)&&!strcmp(error->valuestring,"invalid_range")) {
        // An older paired bridge still accepts the original bounded range.
        maximum[link]=8192;rejected_bulk[link]=true;cJSON_Delete(reply);free(packed);return rpc_ota_range(link,http,sha,offer,offset,out,8192,probe);
    }
    bool ok=status==200&&ota_range_decode(reply,packed?packed:out,size,out,count,offset,compressed);
    free(packed);
    if(!ok)printf("TAB5_OTA_RANGE_FAILED link=%d offset=%d count=%d status=%d bytes=%u\n",link,offset,count,status,(unsigned)size);
    cJSON_Delete(reply);return ok?count:status==0?-2:status==200?-3:status<0?status:-status;
}
int rpc_ota_read(link_kind link,const char *sha,const char *offer,int offset,uint8_t *out,int count) {
    return rpc_ota_range(link,NULL,sha,offer,offset,out,count,false);
}
int rpc_ota_http_read(void *http,const char *sha,const char *offer,int offset,uint8_t *out,int count) {
    return rpc_ota_range(LINK_WIFI,http,sha,offer,offset,out,count,false);
}
int rpc_ota_probe_read(const char *sha,const char *offer,int offset,uint8_t *out,int count) {
    return rpc_ota_range(LINK_BLE,NULL,sha,offer,offset,out,count,true);
}

size_t rpc_data_limit(link_kind link) {
    if(link<0||link>2)return 8192;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    cJSON *cap=cJSON_GetObjectItemCaseSensitive(device_snapshots[link],"rpcBulk");
    cJSON *binary=cJSON_GetObjectItemCaseSensitive(device_snapshots[link],"rpcBinary");
    bool bulk=cJSON_IsNumber(cap)&&cap->valuedouble==1&&cJSON_IsNumber(binary)&&binary->valuedouble==1;
    xSemaphoreGive(device_mutex);return bulk?RPC_BULK_DATA:8192;
}

bool rpc_ota_link_connected(link_kind link) {
    if(link==LINK_USB)return tab5_usb_connected()&&device_host_usb_owned();
    if(link==LINK_BLE)return ble_connected&&ble_host_authorized()&&!device_host_changing();
    return link==LINK_WIFI&&wifi_connected;
}
