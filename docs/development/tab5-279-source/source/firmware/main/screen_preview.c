#include "screen_preview.h"
#include "screen_preview_codec.h"
#include "tab5_jpeg.h"
#include "device.h"
#include "rpc_link.h"
#include "photo_attachment.h"
#include "camera_preview.h"
#include "camera_gallery_buffer.h"
#include "gallery_remote.h"
#include "photo_transfer_crypto.h"
#include "voice_remote.h"
#include "computer_remote.h"
#include "ota_update.h"
#include "esp_lvgl_port.h"
#include "esp_lvgl_port_disp.h"
#include "bsp/esp-bsp.h"
#include "esp_heap_caps.h"
#include "esp_http_client.h"
#include "esp_timer.h"
#include "freertos/idf_additions.h"
#include <stdatomic.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

typedef struct {
    char stream[33],request[33],session[33];
    unsigned host_epoch;
    link_kind link;
    int width,height;
    bool extended,jpeg;
    int64_t deadline,progress_ms;
    void *http;
    bool cancelled;
    voice_remote_state voice; // Worker-owned PSRAM; never nest two transcript buffers on its stack.
} preview_job;
static atomic_bool running;
// One lease-scoped scratch buffer. Reserve before a slow begin RPC and reuse
// it across frames so foreground rendering cannot fragment the next capture.
static uint16_t *spare_pixels;
static int64_t spare_until;
// Gallery and camera already reserve complete, aligned RGB565 frames. Borrow
// only detached/idle owners; never free a reserved camera frame to the heap.
static void release_pixels(uint16_t *pixels){gallery_release_buffer((uint8_t *)pixels);}
static uint16_t *take_hd_pixels(void) {
    uint8_t *pixels=gallery_take_buffer();
    if(!pixels)pixels=camera_gallery_buffer_take();
    return pixels?(uint16_t *)pixels:heap_caps_malloc(1280u*720u*2u,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
}
unsigned screen_preview_cache_reclaim(bool force) {
    bool expected=false;
    if(!atomic_compare_exchange_strong(&running,&expected,true))return 0;
    unsigned released=0;
    if(spare_pixels&&(force||esp_timer_get_time()/1000>=spare_until)) {
        release_pixels(spare_pixels);spare_pixels=NULL;released=1280u*720u*2u;
    }
    atomic_store(&running,false);return released;
}
static atomic_uint minimum_stack,finished_jobs,last_error;
void screen_preview_diagnostics(char *out,unsigned size) {
    snprintf(out,size,"sp,1,%u,%u,%u,%u",atomic_load(&running),atomic_load(&finished_jobs),
        atomic_load(&minimum_stack),atomic_load(&last_error));
}
bool screen_preview_busy(void){return atomic_load(&running);}
// Frame identity only. Scratch pixels remain private, lease bounded and never published without a fresh full capture.
static char previous_stream[33],previous_sha[65];
static uint8_t previous_pixels[32];
static int previous_width,previous_height;
static bool current(const preview_job *job) {
    return !job->cancelled&&job->host_epoch==device_host_epoch()&&!device_host_changing()&&
        esp_timer_get_time()/1000<job->deadline&&rpc_ota_link_connected(job->link);
}
static cJSON *message(const preview_job *job,const char *op) {
    cJSON *out=cJSON_CreateObject();if(!out)return NULL;
    cJSON_AddStringToObject(out,"kind","screen-preview");cJSON_AddStringToObject(out,"op",op);
    cJSON_AddStringToObject(out,"streamId",job->stream);cJSON_AddStringToObject(out,"requestId",job->request);
    cJSON_AddStringToObject(out,"session",job->session);return out;
}
static cJSON *call(preview_job *job,void *http,cJSON *request,const uint8_t *bytes,size_t size,int *status) {
    char nonce[33];if(!request||!current(job)){*status=0;return NULL;}
    for(;;) {
        if(!current(job)){*status=0;return NULL;}
        // A status read or explicit control reserves the foreground before its
        // task reaches the mailbox. Pause only unpublished preview work; let an
        // already-published chunk finish, then resume under the same deadline.
        if(!computer_remote_busy()) {
            cJSON *reply=rpc_data_call(job->link,http,request,status,nonce,bytes,size,NULL,0,NULL);
            if(*status!=RPC_STATUS_UNPUBLISHED_BUSY)return reply;
            // No bytes were published. An uncertain chunk is never replayed.
            cJSON_Delete(reply);
        }
        voice_remote_get(&job->voice);
        if(ota_update_busy()||job->voice.busy||photo_state()==1||camera_preview_busy()){
            job->cancelled=true;*status=0;return NULL;
        }
        vTaskDelay(pdMS_TO_TICKS(20));
    }
}
// Repeating the existing idempotent begin keeps an authorized request alive
// while encoding; no pixel data, extension or unbounded lease is introduced.
static bool announce(preview_job *job) {
    cJSON *request=message(job,"begin");int status=0;
    if(request&&job->extended) {
        cJSON_AddNumberToObject(request,"width",job->width);cJSON_AddNumberToObject(request,"height",job->height);
        cJSON_AddStringToObject(request,"encoding",job->jpeg?"jpeg":"rgb565-rle1");
    }
    cJSON *reply=call(job,job->http,request,NULL,0,&status);
    bool ok=status==200&&cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(reply,"accepted"));
    cJSON_Delete(request);cJSON_Delete(reply);
    job->progress_ms=esp_timer_get_time()/1000;
    if(!ok)job->cancelled=true;
    return ok;
}
static void encode_yield(void *context) {
    preview_job *job=context;vTaskDelay(1);
    if(!current(job))return;
    voice_remote_get(&job->voice);
    if(ota_update_busy()||job->voice.busy||photo_state()==1||camera_preview_busy()){job->cancelled=true;return;}
    if(esp_timer_get_time()/1000-job->progress_ms>=1000)announce(job);
}
static bool hash_yield(void *context) {encode_yield(context);return current(context);}
static void worker(void *argument) {
    preview_job *job=argument;uint16_t *pixels=spare_pixels;spare_pixels=NULL;uint8_t *encoded=NULL;
    if(!job->jpeg){release_pixels(pixels);pixels=NULL;}
    esp_http_client_handle_t http=NULL;cJSON *request=NULL,*reply=NULL;int status=0;unsigned detail=0;const char *error=NULL;
    if(!current(job))goto done;
    voice_remote_get(&job->voice);
    if(ota_update_busy()||job->voice.busy||photo_state()==1||camera_preview_busy()){error="busy";goto done;}
    if(job->jpeg&&!pixels)pixels=take_hd_pixels();
    if(job->jpeg&&bsp_display_lock(30)) {
        if(lv_display_get_horizontal_resolution(lv_display_get_default())<lv_display_get_vertical_resolution(lv_display_get_default())) {
            job->width=720;job->height=1280;
        } else {job->width=1280;job->height=720;}
        bsp_display_unlock();
    }
    if(job->link==LINK_WIFI) {
        device_config cfg=device_get_config();char url[112];snprintf(url,sizeof(url),"http://%s:%d/tab5/v1/rpc",cfg.host,cfg.port);
        esp_http_client_config_t options={.url=url,.timeout_ms=3000,.disable_auto_redirect=true,.keep_alive_enable=true};
        http=esp_http_client_init(&options);if(!http)goto done;
    }
    job->http=http;
    if(!announce(job)||!current(job))goto done;
    voice_remote_get(&job->voice);
    if(ota_update_busy()||job->voice.busy||photo_state()==1||camera_preview_busy()){error="busy";goto done;}
    size_t count=(size_t)job->width*job->height,capacity=job->jpeg?131072:count*2+(count/32767+1)*2+16;
    if(!job->jpeg)pixels=heap_caps_malloc(count*2,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    if(!pixels){error="memory";detail=1;goto done;}
    bool captured=false;
    for(unsigned attempt=0;attempt<8&&current(job);attempt++) {
        if(bsp_display_lock(30)) {
            captured=lvgl_port_screen_preview(lv_display_get_default(),pixels,job->width,job->height);
            bsp_display_unlock();
        }
        if(captured)break;
        vTaskDelay(pdMS_TO_TICKS(20));
    }
    if(!captured){error="capture";goto done;}
    if(!current(job))goto done;
    uint8_t pixel_hash[32];
    if(preview_payload_sha256((const uint8_t *)pixels,count*2,pixel_hash,hash_yield,job)){error="encoding";goto done;}
    if(job->extended&&!strcmp(previous_stream,job->stream)&&previous_width==job->width&&previous_height==job->height&&
       !memcmp(pixel_hash,previous_pixels,sizeof(pixel_hash))) {
        request=message(job,"unchanged");
        if(request) {
            cJSON_AddNumberToObject(request,"width",job->width);cJSON_AddNumberToObject(request,"height",job->height);
            cJSON_AddStringToObject(request,"encoding",job->jpeg?"jpeg":"rgb565-rle1");cJSON_AddStringToObject(request,"sha256",previous_sha);
        }
        reply=call(job,http,request,NULL,0,&status);
        if(status!=200||!cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(reply,"accepted")))previous_stream[0]=0;
        goto done;
    }
    // Unchanged frames need only their pixel hash, not an output buffer.
    size_t size=0;
    for(;;) {
        if(!current(job))goto done;
        encoded=heap_caps_malloc(capacity,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
        if(!encoded){error="memory";detail=2;goto done;}
        if(job->jpeg) {
            uint32_t written=0;
            tab5_jpeg_result result=tab5_jpeg_encode(pixels,count*2,job->width,job->height,encoded,capacity,&written,encode_yield,job);
            if(result==TAB5_JPEG_OK)size=written;
            else if(result==TAB5_JPEG_MEMORY){error="memory";detail=3;goto done;}
            else if(result==TAB5_JPEG_FULL&&capacity<524288&&current(job)) {
                free(encoded);encoded=NULL;capacity*=2;continue;
            }
        } else size=preview_pack(pixels,count,encoded,capacity);
        break;
    }
    if(!job->jpeg){release_pixels(pixels);pixels=NULL;}
    if(!current(job))goto done;
    if(!size||size>524288){error="encoding";goto done;}
    uint8_t digest[32];char sha[65];
    if(preview_payload_sha256(encoded,size,digest,hash_yield,job)){error="encoding";goto done;}
    for(unsigned i=0;i<32;i++)snprintf(sha+i*2,3,"%02x",digest[i]);
    for(size_t offset=0;offset<size&&current(job);) {
        voice_remote_get(&job->voice);if(ota_update_busy()||job->voice.busy||photo_state()==1||camera_preview_busy())break;
        size_t n=size-offset;if(n>4096)n=4096;
        if(job->link==LINK_BLE&&n>1024)n=1024;
        size_t limit=rpc_data_limit(job->link);if(n>limit)n=limit;if(!n)break;
        request=message(job,"frame");if(!request)break;
        cJSON_AddNumberToObject(request,"width",job->width);cJSON_AddNumberToObject(request,"height",job->height);
        cJSON_AddStringToObject(request,"encoding",job->jpeg?"jpeg":"rgb565-rle1");cJSON_AddNumberToObject(request,"size",size);
        cJSON_AddStringToObject(request,"sha256",sha);cJSON_AddNumberToObject(request,"offset",offset);
        reply=call(job,http,request,encoded+offset,n,&status);
        cJSON *next=cJSON_GetObjectItemCaseSensitive(reply,"nextOffset");
        bool accepted=status==200&&cJSON_IsNumber(next)&&next->valuedouble==(double)(offset+n);
        bool complete=cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(reply,"complete"));
        cJSON_Delete(request);request=NULL;cJSON_Delete(reply);reply=NULL;
        if(!accepted)break;
        if(offset+n==size&&complete&&job->extended) {
            snprintf(previous_stream,sizeof(previous_stream),"%s",job->stream);snprintf(previous_sha,sizeof(previous_sha),"%s",sha);
            memcpy(previous_pixels,pixel_hash,sizeof(pixel_hash));previous_width=job->width;previous_height=job->height;
        }
        offset+=n;vTaskDelay(1);
    }
done:
    if(error&&current(job)) {
        cJSON_Delete(request);cJSON_Delete(reply);request=message(job,"error");
        if(request)cJSON_AddStringToObject(request,"error",error);
        reply=call(job,http,request,NULL,0,&status);
    }
    voice_remote_get(&job->voice);
    if(pixels&&job->jpeg&&!error&&status==200&&current(job)&&!ota_update_busy()&&!job->voice.busy&&photo_state()!=1&&!camera_preview_busy()) {
        spare_pixels=pixels;pixels=NULL;spare_until=esp_timer_get_time()/1000+5000;
    }
    cJSON_Delete(request);cJSON_Delete(reply);release_pixels(pixels);free(encoded);
    if(http)esp_http_client_cleanup(http);
    unsigned stack=uxTaskGetStackHighWaterMark(NULL),old=atomic_load(&minimum_stack);
    if(!old||stack<old)atomic_store(&minimum_stack,stack);
    atomic_store(&last_error,detail);atomic_fetch_add(&finished_jobs,1);
    free(job);atomic_store(&running,false);vTaskDeleteWithCaps(NULL);
}
bool screen_preview_request(const char *stream,const char *request,const char *session,
                            link_kind link,int width,int height,int version,const char *encoding,int64_t deadline_ms) {
    bool jpeg=encoding&&!strcmp(encoding,"jpeg"),rle=encoding&&!strcmp(encoding,"rgb565-rle1");
    bool legacy_size=(width==640&&height==360)||(width==320&&height==180);
    bool hd_size=(width==1280&&height==720)||(width==720&&height==1280);
    if(!stream||strlen(stream)!=32||!request||strlen(request)!=32||!session||strlen(session)!=32||
       (link!=LINK_USB&&link!=LINK_WIFI&&link!=LINK_BLE)||
       !((version==1&&rle&&legacy_size)||(version==2&&((rle&&legacy_size)||(jpeg&&hd_size))))||device_host_changing())return false;
    bool expected=false;if(!atomic_compare_exchange_strong(&running,&expected,true))return false;
    preview_job *job=heap_caps_calloc(1,sizeof(*job),MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    if(!job){atomic_store(&running,false);return false;}
    snprintf(job->stream,sizeof(job->stream),"%s",stream);snprintf(job->request,sizeof(job->request),"%s",request);
    snprintf(job->session,sizeof(job->session),"%s",session);job->link=link;job->host_epoch=device_host_epoch();
    job->width=width;job->height=height;job->deadline=deadline_ms;
    job->extended=version==2;job->jpeg=jpeg;
    if(xTaskCreateWithCaps(worker,"screen_preview",16384,job,1,NULL,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT)!=pdPASS) {
        free(job);atomic_store(&running,false);return false;
    }
    return true;
}
