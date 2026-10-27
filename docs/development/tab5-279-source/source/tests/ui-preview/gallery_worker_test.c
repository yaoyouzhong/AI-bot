#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdatomic.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>
#include <bcrypt.h>
#include "cJSON.h"
#include "gallery_jpeg_cache.h"
#include "gallery_store.h"
#include "gallery_profile.h"
#include "gallery_read_pipeline.h"
void rpc_gallery_profile_reset(void){}
gallery_rpc_profile rpc_gallery_profile_get(void){return (gallery_rpc_profile){0};}
#include "sd_storage.h"
enum {LINK_AUTO=-2,LINK_NONE=-1,LINK_USB,LINK_WIFI,LINK_BLE};
typedef int link_kind;
enum {ESP_OK=0,ESP_ERR_NO_MEM=257,pdPASS=1,portMAX_DELAY=-1,MALLOC_CAP_SPIRAM=1,MALLOC_CAP_8BIT=2,
 JPEG_DEC_ALLOC_INPUT_BUFFER=0,JPEG_DEC_ALLOC_OUTPUT_BUFFER=1,
 JPEG_DECODE_OUT_FORMAT_RGB565=0,JPEG_RGB=0,JPEG_YUV_RGB_CONV_STD_BT601=0,JPEG_DEC_RGB_ELEMENT_ORDER_BGR=0,
 ESP_CACHE_MSYNC_FLAG_DIR_C2M=1,ESP_CACHE_MSYNC_FLAG_INVALIDATE=2};
typedef int esp_err_t;
typedef void *esp_http_client_handle_t;
typedef void *jpeg_decoder_handle_t;
typedef struct {const char *url;int timeout_ms;bool disable_auto_redirect;int buffer_size;} esp_http_client_config_t;
typedef struct {int buffer_direction;} jpeg_decode_memory_alloc_cfg_t;
typedef struct {int timeout_ms;} jpeg_decode_engine_cfg_t;
typedef struct {int output_format,rgb_order,conv_std;} jpeg_decode_cfg_t;
typedef struct {int width,height;} jpeg_decode_picture_info_t;
typedef struct {char host[32];int port,mode;} device_config;
typedef struct {bool busy;} voice_remote_state;
static int device_mutex;
static __declspec(thread) int locked;
static SRWLOCK device_test_lock=SRWLOCK_INIT;
static int link=LINK_USB,network_calls,wifi_users,decodes;
static bool bad_hash,http_failure,foreground;
static size_t free_psram=16u*1024u*1024u;
static size_t heap_caps_get_free_size(int caps){(void)caps;return free_psram;}
static int fail_offset=-1,fail_remaining,fail_status,first_ranges,tail_ranges,manifests;
static void (*rpc_hook)(void);
static unsigned cached_total_ms,cached_decode_ms;
static bool cancel_after_failure,mode_after_failure;
static int selected_mode=LINK_AUTO,wifi_stops,wifi_starts,wifi_stop_error;
static bool tab5_wifi_initialized=true,wifi_running=true,hold_wifi_worker,defer_wifi_restore,immediate_gallery_release;
static atomic_int_fast64_t ticks;
static int64_t esp_timer_get_time(void){return ++ticks*1000;}
static void xSemaphoreTake(int mutex,int timeout){(void)mutex;(void)timeout;assert(!locked);AcquireSRWLockExclusive(&device_test_lock);locked=1;}
static void xSemaphoreGive(int mutex){(void)mutex;assert(locked);locked=0;ReleaseSRWLockExclusive(&device_test_lock);}
static void voice_remote_get(voice_remote_state *s){s->busy=foreground;}
static bool ota_update_busy(void){return false;}
static bool transport_benchmark_busy(void){return false;}
static bool camera_preview_busy(void){return false;}
static uint8_t *camera_gallery_buffer_take(void){return NULL;}
static bool camera_gallery_buffers_idle(void){return true;}
static int photo_state(void){return 0;}
static int rpc_read_link_select(void){return link;}
static int rpc_library_link_select(void){return link;}
static device_config device_get_config(void){return (device_config){"localhost",18765,selected_mode};}
static int esp_wifi_stop(void){wifi_stops++;if(wifi_stop_error)return wifi_stop_error;wifi_running=false;return 0;}
static int esp_wifi_start(void){wifi_starts++;wifi_running=true;return 0;}
#define TAB5_WIFI_PROBE_TEST
#include "../../firmware/main/wifi_probe.c"
static void tab5_wifi_wake(void){
    if(immediate_gallery_release&&atomic_load(&probe_stage)==PROBE_QUIET)wifi_probe_end();
    if(!hold_wifi_worker&&!(defer_wifi_restore&&!atomic_load(&requested))){bool reconnect;wifi_probe_service(selected_mode==LINK_BLE&&!wifi_probe_requires_usb(),&reconnect);}
}
static void wifi_performance_bulk(bool active){wifi_users+=active?1:-1;assert(wifi_users>=0);}
static unsigned http_created,http_cleaned;
static void *esp_http_client_init(const esp_http_client_config_t *c){
    assert(c->timeout_ms==8000&&c->buffer_size==GALLERY_HTTP_READ_BYTES);
    if(http_failure)return NULL;
    unsigned *handle=malloc(sizeof(*handle));assert(handle);*handle=++http_created;return handle;
}
static bool fail_text_allocation;
static unsigned text_page_allocations;
static void *heap_caps_calloc(size_t count,size_t size,int caps){assert(caps==(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT));if(size<=32761){text_page_allocations++;if(fail_text_allocation)return NULL;}return calloc(count,size);}
static void esp_http_client_cleanup(void *p){assert(p);http_cleaned++;free(p);}
static bool esp_http_client_is_complete_data_received(void *p){assert(p);return !http_failure;}
static void vTaskDeleteWithCaps(void *p){(void)p;}
static void vTaskDelay(int ms){if(ms==1){Sleep(1);return;}ticks+=ms;if(!hold_wifi_worker){bool reconnect;wifi_probe_service(selected_mode==LINK_BLE&&!wifi_probe_requires_usb(),&reconnect);}}
#define pdMS_TO_TICKS(ms) (ms)
#define taskYIELD() ((void)0)
static bool defer_workers;
static struct {void (*fn)(void *);void *arg;} pending_workers[2];
static unsigned pending_worker_count;
typedef struct {void (*fn)(void *);void *arg;} card_thread_call;
static DWORD WINAPI card_thread_run(void *argument){card_thread_call call=*(card_thread_call *)argument;free(argument);call.fn(call.arg);return 0;}
static int xTaskCreateWithCaps(void (*fn)(void *),const char *name,int stack,void *arg,int priority,void *handle,int caps){
    (void)stack;(void)priority;(void)handle;(void)caps;
    if(!strcmp(name,"tab5_card_save")){card_thread_call *call=malloc(sizeof(*call));assert(call);*call=(card_thread_call){fn,arg};HANDLE thread=CreateThread(NULL,0,card_thread_run,call,0,NULL);assert(thread);CloseHandle(thread);return pdPASS;}
    if(defer_workers){assert(pending_worker_count<2);pending_workers[pending_worker_count].fn=fn;pending_workers[pending_worker_count++].arg=arg;}
    else fn(arg);
    return pdPASS;
}
static int mbedtls_sha256(const uint8_t *bytes,size_t size,uint8_t *out,int variant) {
    (void)variant;BCRYPT_ALG_HANDLE provider=NULL;assert(BCryptOpenAlgorithmProvider(&provider,BCRYPT_SHA256_ALGORITHM,NULL,0)==0);
    assert(BCryptHash(provider,NULL,0,(PUCHAR)bytes,(ULONG)size,out,32)==0);BCryptCloseAlgorithmProvider(provider,0);return 0;
}
#include "gallery_text_allocator_under_test.inc"
#include "gallery_text_pages.h"
#define GALLERY_STORE_TEST
#define gallery_speed_prepare file_speed_prepare
#define gallery_store_get file_store_get
#define gallery_store_put file_store_put
#define gallery_store_refresh file_store_refresh
#define gallery_store_finish file_store_finish
#define gallery_catalog_load file_catalog_load
#define gallery_catalog_save file_catalog_save
#define gallery_resume_load file_resume_load
#define gallery_resume_append file_resume_append
#define gallery_resume_clear file_resume_clear
#include "../../firmware/main/gallery_store.c"
#undef gallery_speed_prepare
#undef gallery_store_get
#undef gallery_store_put
#undef gallery_store_refresh
#undef gallery_store_finish
#undef gallery_catalog_load
#undef gallery_catalog_save
#undef gallery_resume_load
#undef gallery_resume_append
#undef gallery_resume_clear
static bool test_card,stream_enabled,slow_card;
static atomic_bool card_writing;
static int image_bytes=65536,stream_calls,stream_cut=-1,overlapping_receives;
static size_t stream_offset;
static unsigned test_card_operations;
static char test_card_root[MAX_PATH];
bool sd_storage_available(void){return test_card;}
bool sd_storage_lock(void){assert(!locked);return test_card;}
void sd_storage_unlock(bool media_error){assert(!locked&&!media_error);}
int sd_storage_state(void){return test_card?1:-1;}
static const char *mapped_root(const char *root) {
    static char sample[MAX_PATH];
    if(!strcmp(root,"/sdcard"))return test_card_root;
    assert(!strcmp(root,"/sdcard/aibot/cache/gallery-speed"));
    snprintf(sample,sizeof(sample),"%s/aibot/cache/gallery-speed",test_card_root);return sample;
}
bool gallery_speed_prepare(const char *root){assert(!locked);return file_speed_prepare(mapped_root(root));}
bool gallery_store_get(const char *root,const gallery_image *key,const char *revision,gallery_store_allocator alloc,gallery_jpeg *out,cJSON **guide){test_card_operations++;assert(!locked);return file_store_get(mapped_root(root),key,revision,alloc,out,guide);}
bool gallery_store_put(const char *root,const gallery_jpeg *value,const char *revision,const gallery_guide *guide){test_card_operations++;assert(!locked);return file_store_put(mapped_root(root),value,revision,guide);}
bool gallery_store_refresh(const char *root,const gallery_jpeg *value,const char *revision,const gallery_guide *guide){test_card_operations++;assert(!locked);return file_store_refresh(mapped_root(root),value,revision,guide);}
bool gallery_store_finish(const char *root,const gallery_jpeg *value,const char *revision,const gallery_guide *guide,const gallery_resume *state){test_card_operations++;assert(!locked);return file_store_finish(mapped_root(root),value,revision,guide,state);}
bool gallery_catalog_load(const char *root,int category,const char *revision,gallery_catalog *out){test_card_operations++;assert(!locked);return file_catalog_load(mapped_root(root),category,revision,out);}
bool gallery_catalog_save(const char *root,int category,const gallery_catalog *value){test_card_operations++;assert(!locked);return file_catalog_save(mapped_root(root),category,value);}
bool gallery_resume_load(const char *root,const gallery_image *key,const char *revision,const char *sha,uint8_t *jpeg,size_t total,gallery_resume *state){test_card_operations++;assert(!locked);return file_resume_load(mapped_root(root),key,revision,sha,jpeg,total,state);}
bool gallery_resume_append(const char *root,const gallery_image *key,const char *revision,const char *sha,const uint8_t *jpeg,size_t total,size_t end,gallery_resume *state){test_card_operations++;assert(!locked);atomic_store(&card_writing,true);if(slow_card)Sleep(20);bool ok=file_resume_append(mapped_root(root),key,revision,sha,jpeg,total,end,state);atomic_store(&card_writing,false);return ok;}
bool gallery_resume_clear(const char *root,const gallery_image *key,const char *revision,const char *sha,size_t total){test_card_operations++;assert(!locked);return file_resume_clear(mapped_root(root),key,revision,sha,total);}
static uint8_t *jpeg_alloc_decoder_mem(size_t n,const jpeg_decode_memory_alloc_cfg_t *c,size_t *capacity){(void)c;*capacity=n;return calloc(1,n);}
static int jpeg_decoder_get_info(const uint8_t *p,int n,jpeg_decode_picture_info_t *info){assert(p&&n>16);info->width=1280;info->height=720;return 0;}
static int esp_cache_msync(void *p,size_t n,int flags){assert(p&&n==1280*720*2&&flags==3);return 0;}
static int engine_error;
static int jpeg_new_decoder_engine(const jpeg_decode_engine_cfg_t *cfg,void **codec){assert(cfg->timeout_ms==3000);if(engine_error)return engine_error;*codec=(void*)1;return 0;}
static int jpeg_decoder_process(void *codec,const jpeg_decode_cfg_t *cfg,const uint8_t *jpeg,int n,uint8_t *pixels,size_t capacity,uint32_t *written){(void)codec;(void)cfg;assert(n>16);memset(pixels,jpeg[0],capacity);*written=(uint32_t)capacity;decodes++;return 0;}
static void jpeg_del_decoder_engine(void *codec){assert(codec);}
static size_t rpc_data_limit(int channel){assert(channel>=0&&channel<=2);return 49152;}
static int fixture_guide_version=2;
static bool test_catalog;
static char remote_revisions[2][65];
static unsigned range_calls;
static const char *fixture_overview="museum context";
static cJSON *annual_fixture;
static int annual_bad_page=-1,annual_cancel_page=-1,guide_page_calls;
static bool guide_references=true,bad_guide_reference;
static unsigned full_guides,referenced_guides;
static cJSON *rpc_data_call(int channel,void *http,cJSON *request,int *status,char *nonce,
 const uint8_t *upload,size_t upload_size,uint8_t *download,size_t capacity,size_t *received) {
    (void)nonce;(void)upload;(void)upload_size;assert(channel>=0&&channel<=2);assert((channel==LINK_WIFI)==(http!=NULL));network_calls++;
    if(rpc_hook){void (*hook)(void)=rpc_hook;rpc_hook=NULL;hook();}
    const char *category=cJSON_GetStringValue(cJSON_GetObjectItem(request,"category"));
    const char *orientation=cJSON_GetStringValue(cJSON_GetObjectItem(request,"orientation"));
    int category_index=!strcmp(category,"calligraphy");
    if(annual_fixture&&!strcmp(cJSON_GetStringValue(cJSON_GetObjectItem(request,"op")),"guidePage")) {
        guide_page_calls++;
        int page=cJSON_GetObjectItemCaseSensitive(request,"page")->valueint;
        cJSON *body=cJSON_Duplicate(cJSON_GetArrayItem(cJSON_GetObjectItemCaseSensitive(annual_fixture,"pages"),page),true);assert(body);
        assert(!strcmp(cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(body,"workId")),cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(request,"workId"))));
        *status=200;
        if(page==annual_bad_page)cJSON_ReplaceItemInObjectCaseSensitive(body,"pageSha256",cJSON_CreateString("0000000000000000000000000000000000000000000000000000000000000000"));
        if(page==annual_cancel_page)foreground=true;
        return body;
    }
    if(!strcmp(cJSON_GetStringValue(cJSON_GetObjectItem(request,"op")),"catalog")) {
        cJSON *reply=cJSON_CreateObject();*status=test_catalog?200:400;
        cJSON_AddNumberToObject(reply,"syncVersion",1);cJSON_AddNumberToObject(reply,"works",category_index?2:3);
        cJSON_AddStringToObject(reply,"baseDate","2026-01-01");cJSON_AddStringToObject(reply,"revision",remote_revisions[category_index]);return reply;
    }
    int work_index=0;
    if(test_catalog){char canonical[11];assert(gallery_catalog_key(cJSON_GetStringValue(cJSON_GetObjectItem(request,"date")),category_index?2:3,canonical));work_index=canonical[9]-'1';}
    int frame=cJSON_GetObjectItem(request,"frame")->valueint;
    int seed=1+category_index*20+work_index*50+frame*2+(orientation&&!strcmp(orientation,"portrait"));
    const int size=image_bytes;static uint8_t bytes[1048576];for(int i=0;i<size;i++)bytes[i]=(uint8_t)(seed+i);
    cJSON *reply=cJSON_CreateObject();*status=200;
    if(!strcmp(cJSON_GetStringValue(cJSON_GetObjectItem(request,"op")),"manifest")) {
        manifests++;
        assert(cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(request,"includeGuide")));
        assert(cJSON_GetObjectItemCaseSensitive(request,"guideVersion")->valueint==3);
        if(fail_offset==-2&&fail_remaining>0){fail_remaining--;*status=fail_status;return reply;}
        uint8_t hash[32];char hex[65];mbedtls_sha256(bytes,size,hash,0);if(bad_hash)hash[0]^=1;
        for(int i=0;i<32;i++)sprintf(hex+2*i,"%02x",hash[i]);
        cJSON_AddNumberToObject(reply,"size",size);cJSON_AddNumberToObject(reply,"frames",3);
        cJSON_AddStringToObject(reply,"sha256",hex);cJSON_AddStringToObject(reply,"orientation",orientation?orientation:"landscape");
        cJSON_AddNumberToObject(reply,"guideVersion",fixture_guide_version);
        char work_id[80];snprintf(work_id,sizeof(work_id),test_catalog?"fixture-work-%d":"fixture-work",work_index);
        cJSON *guide=cJSON_CreateObject();cJSON_AddStringToObject(guide,"workId",work_id);
        cJSON_AddStringToObject(guide,"title","source-bound guide");cJSON_AddStringToObject(guide,"overview",fixture_overview);
        cJSON_AddStringToObject(guide,"looking","editorial viewing suggestion");cJSON_AddItemToObject(reply,"guide",guide);
        if(annual_fixture){cJSON_ReplaceItemInObjectCaseSensitive(reply,"guide",cJSON_Duplicate(cJSON_GetObjectItemCaseSensitive(annual_fixture,"guide"),true));cJSON_ReplaceItemInObjectCaseSensitive(reply,"guideVersion",cJSON_CreateNumber(3));}
        const char *hint=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(request,"guideRevision"));
        const char *id=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(request,"guideWorkId"));
        const char *actual=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(cJSON_GetObjectItemCaseSensitive(reply,"guide"),"workId"));
        if(guide_references&&(annual_fixture||fixture_guide_version==3)&&hint&&id&&!strcmp(hint,remote_revisions[category_index])&&!strcmp(id,actual)) {
            cJSON_AddStringToObject(reply,"workId",bad_guide_reference?"different-work":actual);cJSON_AddBoolToObject(reply,"guideUnchanged",true);
            cJSON_ReplaceItemInObjectCaseSensitive(reply,"guide",cJSON_CreateNull());referenced_guides++;
        }else full_guides++;
    }else {
        range_calls++;
        int offset=cJSON_GetObjectItem(request,"offset")->valueint,count=cJSON_GetObjectItem(request,"count")->valueint;
        assert(download&&received&&count==(int)capacity&&offset+count<=size);
        if(offset==0)first_ranges++;else tail_ranges++;
        if(offset==fail_offset&&fail_remaining>0) {
            fail_remaining--;*status=fail_status;*received=0;
            if(cancel_after_failure)foreground=true;
            if(mode_after_failure)selected_mode=(channel+1)%3;
            return reply;
        }
        memcpy(download,bytes+offset,count);*received=count;cJSON_AddNumberToObject(reply,"offset",offset);
    }
    return reply;
}
typedef bool (*rpc_gallery_progress)(void *,size_t);
static bool rpc_gallery_stream_ready(void){return stream_enabled;}
static cJSON *rpc_gallery_stream_call(void *http,cJSON *request,int *status,char nonce[33],uint8_t *download,size_t capacity,size_t *received,rpc_gallery_progress progress,void *context){
    stream_calls++;stream_offset=cJSON_GetObjectItemCaseSensitive(request,"offset")->valueint;
    cJSON *reply=rpc_data_call(LINK_WIFI,http,request,status,nonce,NULL,0,download,capacity,received);
    if(*status!=200)return reply;
    *received=0;
    for(size_t at=0;at<capacity;){size_t size=capacity-at;if(size>49152)size=49152;at+=size;*received=at;
        if(atomic_load(&card_writing))overlapping_receives++;
        if(progress&&!progress(context,at)){cJSON_Delete(reply);*status=0;return NULL;}
        Sleep(1);
        if(stream_cut>=0&&(int)at>=stream_cut){cJSON_Delete(reply);*status=0;return NULL;}
    }
    return reply;
}
#include "gallery_worker_under_test.inc"
static unsigned memory_trim_calls;
static void tab5_draw_cache_trim_hidden(unsigned bytes){memory_trim_calls++;free_psram+=bytes;}
#include "gallery_memory_ui_under_test.inc"

static void cached_during_rpc(void) {
    int before=network_calls;
    assert(gallery_request(0,"2026-10-07",0,false,NULL));
    gallery_image image;assert(gallery_take(&image)&&image.frame==0&&image.category==0&&!image.portrait);
    gallery_recycle(image.pixels);assert(network_calls==before&&atomic_load(&prefetching));
    cached_total_ms=atomic_load(&diagnostic_total_ms);cached_decode_ms=atomic_load(&diagnostic_decode_ms);
    ticks+=8000; // The background transport remains blocked after local display.
}
static void display(int category,int frame,bool portrait) {
    assert(gallery_request(category,"2026-10-07",frame,portrait,NULL));
    gallery_image image;assert(gallery_take(&image));
    assert(image.frames==3&&image.frame==frame&&image.portrait==portrait&&image.category==category);
    assert(image.pixels[0]==1+category*20+frame*2+portrait&&image.pixels[1280*720*2-1]==image.pixels[0]);
    gallery_guide guide;assert(!strcmp(image.work_id,"fixture-work")&&gallery_guide_copy(category,image.date,image.work_id,&guide));
    assert(!strcmp(guide.overview,"museum context")&&!gallery_guide_copy(category,"2026-10-08",image.work_id,&guide));
    assert(!gallery_guide_copy(category,image.date,"another-work",&guide));
    gallery_recycle(image.pixels);assert(wifi_users==0&&!locked&&(wifi_running||defer_wifi_restore||atomic_load(&probe_stage)==PROBE_QUIET));
}
static void annual_long_readers(const char *path) {
    FILE *file=fopen(path,"rb");assert(file);assert(!fseek(file,0,SEEK_END));long length=ftell(file);assert(length>0&&length<1048576);rewind(file);
    char *json=malloc(length+1);assert(json&&fread(json,1,length,file)==length);json[length]=0;fclose(file);
    cJSON *rows=cJSON_Parse(json);free(json);assert(cJSON_IsArray(rows)&&cJSON_GetArraySize(rows)==7);
    assert(GetTempPathA(MAX_PATH,test_card_root));size_t at=strlen(test_card_root);snprintf(test_card_root+at,MAX_PATH-at,"aibot-annual-readers-%lu",GetCurrentProcessId());assert(!_mkdir(test_card_root));
    test_card=true;gallery_storage_enable(true);
    for(int channel=LINK_USB;channel<=LINK_BLE;channel++)for(int index=0;index<7;index++) {
        if(index==0) {
            char revision[65];memset(revision,'d'+channel,64);revision[64]=0;
            // Force a fresh verified transfer through each transport facade,
            // rather than letting later channels reuse the previous TF set.
            assert(gallery_pack_sync(NULL,revision)==2);
        }
        annual_fixture=cJSON_GetArrayItem(rows,index);const char *date=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(annual_fixture,"date"));
        const char *full=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(annual_fixture,"fullText"));
        link=channel;gallery_cache_reclaim();
        assert(gallery_request(1,date,0,false,NULL));gallery_image image;assert(gallery_take(&image));gallery_recycle(image.pixels);
        gallery_guide g;assert(gallery_guide_copy(1,date,image.work_id,&g));
        assert(g.transcription_page_count>1&&g.transcription_page_count<=32&&!g.transcription_pages[0]);
        char rebuilt[32761]={0};size_t bytes=0;
        for(int page=0;page<g.transcription_page_count;page++) {
            assert(gallery_guide_page_copy(1,date,image.work_id,page,&g));size_t n=strlen(g.transcription);assert(n<=4095&&bytes+n<sizeof(rebuilt));memcpy(rebuilt+bytes,g.transcription,n);bytes+=n;
        }
        assert(!strcmp(full,rebuilt)&&gallery_text_hash(rebuilt,g.transcription_sha256));
        assert(!gallery_guide_page_copy(1,date,image.work_id,g.transcription_page_count,&g));
        assert(!gallery_guide_page_copy(1,date,"another-work",0,&g));
        // Drop decoded/JPEG caches and require the complete saved TF page set.
        gallery_cache_reclaim();link=LINK_NONE;int before=network_calls;
        assert(gallery_request(1,date,0,false,NULL)&&gallery_take(&image));gallery_recycle(image.pixels);
        assert(network_calls==before&&gallery_guide_copy(1,date,image.work_id,&g));
        bytes=0;rebuilt[0]=0;
        for(int page=0;page<g.transcription_page_count;page++){assert(gallery_guide_page_copy(1,date,image.work_id,page,&g));size_t n=strlen(g.transcription);memcpy(rebuilt+bytes,g.transcription,n);bytes+=n;rebuilt[bytes]=0;}
        assert(!strcmp(full,rebuilt)&&gallery_text_hash(rebuilt,g.transcription_sha256));
        // A failed or cancelled replacement never exposes a partial new text.
        gallery_storage_enable(false);gallery_cache_reclaim();link=channel;
        annual_bad_page=1;assert(gallery_request(1,"2027-12-31",0,false,NULL));assert(!gallery_take(&image));annual_bad_page=-1;
        assert(gallery_guide_copy(1,date,g.work_id,&g)&&gallery_text_hash(full,g.transcription_sha256));
        annual_cancel_page=1;assert(gallery_request(1,"2027-12-31",0,false,NULL));assert(!gallery_take(&image));annual_cancel_page=-1;foreground=false;
        assert(gallery_guide_copy(1,date,g.work_id,&g)&&gallery_text_hash(full,g.transcription_sha256));gallery_storage_enable(true);
        gallery_guide frozen={0};assert(gallery_guide_reader_copy(1,date,g.work_id,&frozen)&&gallery_text_pages_complete(&frozen));
        // Simulated PSRAM exhaustion must retain the cached original and
        // release every partially owned snapshot page without heap fallback.
        gallery_guide unavailable={0};fail_text_allocation=true;
        assert(!gallery_guide_reader_copy(1,date,g.work_id,&unavailable));
        for(int page=0;page<32;page++)assert(!unavailable.transcription_pages[page]);
        fail_text_allocation=false;assert(gallery_guide_copy(1,date,g.work_id,&g));
        // An in-flight new-date/new-work decode can finish after the old
        // displayed work's reader opens. Its independent snapshot survives.
        gallery_storage_enable(false);gallery_cache_reclaim();defer_workers=true;
        assert(gallery_request(1,"2027-12-31",0,false,NULL)&&pending_worker_count==1);
        annual_fixture=cJSON_GetArrayItem(rows,(index+1)%7);defer_workers=false;
        pending_workers[0].fn(pending_workers[0].arg);pending_worker_count=0;
        assert(gallery_take(&image));gallery_recycle(image.pixels);
        assert(!gallery_guide_copy(1,date,g.work_id,&g)&&gallery_text_pages_complete(&frozen));
        annual_fixture=cJSON_GetArrayItem(rows,index);gallery_storage_enable(true);
        char changed[65];snprintf(changed,sizeof(changed),"%064x",100+channel*7+index);assert(gallery_pack_sync(NULL,changed)==2);
        assert(!gallery_guide_copy(1,date,g.work_id,&g)&&gallery_text_pages_complete(&frozen));
        bytes=0;rebuilt[0]=0;
        for(int page=0;page<frozen.transcription_page_count;page++){size_t n=strlen(frozen.transcription_pages[page]);memcpy(rebuilt+bytes,frozen.transcription_pages[page],n);bytes+=n;rebuilt[bytes]=0;}
        assert(!strcmp(full,rebuilt));gallery_guide_reader_release(&frozen);for(int page=0;page<32;page++)assert(!frozen.transcription_pages[page]);
    }
    // Background frame/orientation changes reuse one checked complete original,
    // while an authenticated pack change invalidates the bounded text cache.
    annual_fixture=cJSON_GetArrayItem(rows,0);const char *library_date=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(annual_fixture,"date"));
    int parts=cJSON_GetArraySize(cJSON_GetObjectItemCaseSensitive(annual_fixture,"pages")),calls=guide_page_calls;
    char library_revision[65];memset(library_revision,'5',64);library_revision[64]=0;
    strcpy(remote_revisions[1],library_revision);unsigned ref_before=referenced_guides;
    link=LINK_USB;assert(gallery_pack_sync(NULL,library_revision)==2);
    assert(start_request(1,library_date,0,false,NULL,true,1));assert(guide_page_calls==calls+parts-1);
    assert(start_request(1,library_date,0,true,NULL,true,1));assert(guide_page_calls==calls+parts-1);
    assert(start_request(1,library_date,1,false,NULL,true,1));assert(guide_page_calls==calls+parts-1);
    assert(referenced_guides==ref_before+2);
    library_revision[0]='6';assert(gallery_pack_sync(NULL,library_revision)==2);
    strcpy(remote_revisions[1],library_revision);
    assert(start_request(1,library_date,1,true,NULL,true,1));assert(guide_page_calls==calls+2*(parts-1));
    puts("GALLERY_GUIDE_REUSE_OK one complete authenticated long original per work/revision; subsequent frame/layout changes avoid repeated text RPC; new revision refetches");
    annual_fixture=NULL;cJSON_Delete(rows);test_card=false;gallery_storage_enable(false);assert(text_page_allocations>0);
    puts("NATIVE_ANNUAL_LONG_READERS_OK works=7 link_facades=3; production worker, per-page copies, exact complete SHA-256, TF offline reload, rejected corrupted/cancelled replacement retains previous guide, frozen reader survives in-flight next-work completion and pack replacement, releases pages; no hardware claim");
}
int main(int argc,char **argv) {
    if(argc==2){annual_long_readers(argv[1]);return 0;}
    char painting_revision[65],calligraphy_revision[65];
    memset(painting_revision,'a',64);painting_revision[64]=0;
    memset(calligraphy_revision,'b',64);calligraphy_revision[64]=0;
    assert(gallery_pack_sync(painting_revision,calligraphy_revision)==3);
    display(0,0,false);display(1,0,false);
    engine_error=ESP_ERR_NO_MEM;
    assert(gallery_request(1,"2026-10-07",0,false,NULL));
    gallery_image failed_decode;assert(!gallery_take(&failed_decode));
    assert(gallery_decode_status(1,false)==GALLERY_DECODE_NO_MEMORY);
    assert(gallery_decode_status(0,false)==GALLERY_DECODE_OK&&gallery_decode_status(1,true)==GALLERY_DECODE_OK);
    engine_error=258;
    assert(gallery_request(1,"2026-10-07",0,false,NULL));assert(!gallery_take(&failed_decode));
    assert(gallery_decode_status(1,false)==GALLERY_DECODE_FAILED);
    engine_error=0;display(1,0,false);
    assert(gallery_decode_status(1,false)==GALLERY_DECODE_OK);
    gallery_take_failures();
    int revision_calls=network_calls;
    assert(!gallery_pack_sync(painting_revision,calligraphy_revision)&&!gallery_pack_sync(NULL,"invalid"));
    painting_revision[0]='c';foreground=true;
    assert(!gallery_pack_sync(painting_revision,calligraphy_revision));foreground=false;
    assert(gallery_pack_sync(painting_revision,calligraphy_revision)==1);
    gallery_guide retained;
    assert(!gallery_guide_copy(0,"2026-10-07","fixture-work",&retained));
    assert(gallery_guide_copy(1,"2026-10-07","fixture-work",&retained));
    link=LINK_NONE;assert(!gallery_request_available(0,"2026-10-07",0,false));display(1,0,false);
    assert(network_calls==revision_calls);link=LINK_USB;display(0,0,false);
    assert(network_calls==revision_calls+3);
    // A pending fill owns its buffers until completion; revision application
    // is deferred, then clears stale text/JPEGs before any new request.
    defer_workers=true;gallery_prefetch_day("2026-10-07");assert(pending_worker_count==1);
    calligraphy_revision[0]='d';assert(!gallery_pack_sync(painting_revision,calligraphy_revision));
    assert(atomic_load(&cancel_prefetch));pending_workers[0].fn(pending_workers[0].arg);
    pending_worker_count=0;defer_workers=false;
    assert(gallery_pack_sync(painting_revision,calligraphy_revision)==2);
    assert(!gallery_guide_copy(1,"2026-10-07","fixture-work",&retained));
    assert(gallery_guide_copy(0,"2026-10-07","fixture-work",&retained));
    gallery_cache_reclaim();
    puts("GALLERY_PACK_REFRESH_OK text-only revision invalidates affected JPEG/guide; other category stays offline; active fill cancellation deferred; absent/invalid/unchanged digests preserve caches");
    cJSON *malformed=cJSON_Parse("{\"workId\":\"bound-work\",\"overview\":\"context\"}");
    gallery_guide *checked=guide_read(malformed,"2026-10-07");assert(checked&&!strcmp(checked->work_id,"bound-work"));free(checked);
    char oversized[640+1];memset(oversized,'x',640);oversized[640]=0;
    cJSON_ReplaceItemInObject(malformed,"overview",cJSON_CreateString(oversized));assert(!guide_read(malformed,"2026-10-07"));
    cJSON_ReplaceItemInObject(malformed,"overview",cJSON_CreateString("context"));
    char long_original[8193];
    for(int i=0;i<8190;i+=3)memcpy(long_original+i,"字",3);long_original[8190]='x';long_original[8191]=0;
    cJSON_AddStringToObject(malformed,"transcription",long_original);
    checked=guide_read(malformed,"2026-10-07");assert(checked&&strlen(checked->transcription)==8191&&!strcmp(checked->transcription,long_original));free(checked);
    long_original[8191]='x';long_original[8192]=0;
    cJSON_ReplaceItemInObject(malformed,"transcription",cJSON_CreateString(long_original));assert(!guide_read(malformed,"2026-10-07"));cJSON_Delete(malformed);
    assert(!guide_read(NULL,"2026-10-07"));puts("GALLERY_GUIDE_BOUNDS_OK 8191-byte UTF-8 original intact; 8192 rejected without truncation; missing guide safe");
    link=LINK_USB;fixture_guide_version=1;display(0,0,false);gallery_cache_reclaim();
    fixture_guide_version=2;display(0,0,false);gallery_cache_reclaim();
    puts("GALLERY_GUIDE_VERSION_OK v3 requested; both v1 and v2 bridge replies accepted");
    for(int channel=LINK_USB;channel<=LINK_BLE;channel++) {
        link=channel;gallery_cache_reclaim();first_ranges=tail_ranges=0;
        fail_offset=49152;fail_remaining=1;fail_status=0;display(0,0,false);
        assert(first_ranges==1&&tail_ranges==2&&!fail_remaining); // No restart of the first 48 KiB.
        for(int scenario=0;scenario<4;scenario++) {
            gallery_cache_reclaim();first_ranges=tail_ranges=0;fail_remaining=8;
            fail_status=scenario==1?401:0;cancel_after_failure=scenario==2;mode_after_failure=scenario==3;
            assert(gallery_request(0,"2026-10-07",0,false,NULL));gallery_image failed;
            assert(!gallery_take(&failed));
            assert(first_ranges==1&&tail_ranges==(scenario==0?3:1)&&!locked&&wifi_users==0);
            foreground=false;selected_mode=LINK_AUTO;cancel_after_failure=mode_after_failure=false;
            link=LINK_NONE;assert(!gallery_request_available(0,"2026-10-07",0,false));link=channel;
        }
    }
    for(int channel=LINK_USB;channel<=LINK_BLE;channel++) {
        link=channel;gallery_cache_reclaim();manifests=first_ranges=tail_ranges=0;
        fail_offset=-2;fail_remaining=1;fail_status=0;display(0,0,false);
        assert(manifests==2&&first_ranges==1&&tail_ranges==1);
        for(int scenario=0;scenario<2;scenario++) {
            gallery_cache_reclaim();manifests=first_ranges=tail_ranges=0;
            fail_remaining=8;fail_status=scenario?401:0;
            assert(gallery_request(0,"2026-10-07",0,false,NULL));gallery_image failed;
            assert(!gallery_take(&failed)&&manifests==(scenario?1:3)&&!first_ranges&&!tail_ranges&&!locked&&wifi_users==0);
            link=LINK_NONE;assert(!gallery_request_available(0,"2026-10-07",0,false));link=channel;
        }
    }
    fail_offset=-1;fail_remaining=fail_status=0;
    puts("GALLERY_RANGE_RECOVERY_OK three links; manifest recovery; tail retry retains prefix; bounded failures; auth rejection; foreground/mode cancellation; no partial cache");
    for(int channel=LINK_USB;channel<=LINK_BLE;channel++) {
        gallery_cache_reclaim();link=channel;int before=network_calls;
        // The UI's first synchronization warms both layouts of the current page.
        for(int c=0;c<2;c++)for(int f=0;f<3;f++)for(int p=0;p<2;p++)display(c,f,p);
        assert(network_calls==before+36);
        link=LINK_NONE;before=network_calls;
        for(int repeat=0;repeat<2;repeat++)for(int c=0;c<2;c++)for(int f=0;f<3;f++)for(int p=0;p<2;p++)display(c,f,p);
        assert(network_calls==before);assert(!gallery_request_available(0,"2026-10-08",0,false));
    }
    // A cached foreground page must finish even while an unrelated day-fill
    // request is blocked in transport. Model task scheduling rather than
    // executing every worker synchronously at creation time.
    gallery_cache_reclaim();link=LINK_USB;display(0,0,false);
    int overlap_calls=network_calls,overlap_decodes=decodes;
    defer_workers=true;gallery_prefetch_day("2026-10-07");assert(pending_worker_count==1);
    assert(gallery_request(0,"2026-10-07",0,false,NULL));
    assert(pending_worker_count==2);
    pending_workers[1].fn(pending_workers[1].arg);
    gallery_image overlap;assert(gallery_take(&overlap)&&overlap.frame==0&&!overlap.portrait);
    gallery_recycle(overlap.pixels);
    assert(network_calls==overlap_calls&&decodes==overlap_decodes+1);
    pending_workers[0].fn(pending_workers[0].arg);
    assert(network_calls==overlap_calls&&!gallery_take(&overlap));
    pending_worker_count=0;defer_workers=false;ticks+=6000; // Cancelled fills retain their production retry cooldown.
    puts("GALLERY_CACHED_PRIORITY_OK cached decode and display complete before blocked prefetch, zero RPC, cancelled background cannot replace result");
    for(int channel=LINK_USB;channel<=LINK_BLE;channel++){
        gallery_cache_reclaim();link=channel;display(0,0,false);ticks+=6000;
        int before=network_calls,decoded=decodes;rpc_hook=cached_during_rpc;
        gallery_prefetch_day("2026-10-07");
        assert(!rpc_hook&&network_calls==before+1&&decodes==decoded+1&&gallery_request_ready());
        assert(atomic_load(&diagnostic_total_ms)==cached_total_ms&&atomic_load(&diagnostic_decode_ms)==cached_decode_ms);
    }
    ticks+=6000;
    puts("GALLERY_ACTIVE_RPC_PRIORITY_OK all three blocked transport calls allow local display before reply; no overlapping network call or background RGB");
    gallery_cache_reclaim();link=LINK_USB;int daily_before=network_calls,decoded_before=decodes;
    // Automatic synchronization discovers both works and every page/layout,
    // without rendering or allocating an extra output bitmap.
    for(int step=0;step<20;step++)gallery_prefetch_day("2026-10-07");
    assert(network_calls==daily_before+36&&decodes==decoded_before);
    gallery_image background_result;assert(!gallery_take(&background_result));
    link=LINK_NONE;daily_before=network_calls;
    for(int c=0;c<2;c++)for(int f=0;f<3;f++)for(int p=0;p<2;p++)display(c,f,p);
    assert(network_calls==daily_before);
    link=LINK_USB;free_psram=4u*1024u*1024u;
    gallery_prefetch_day("2026-10-08");assert(network_calls==daily_before);
    free_psram=16u*1024u*1024u;
    bad_hash=true;gallery_prefetch_day("2026-10-08");bad_hash=false;
    assert(gallery_request_available(0,"2026-10-07",0,false)); // Failed new day retains yesterday.
    ticks+=6000;
    for(int step=0;step<20;step++)gallery_prefetch_day("2026-10-08");
    gallery_image old_day={.category=0,.frame=0,.portrait=false};strcpy(old_day.date,"2026-10-07");
    assert(!gallery_jpeg_has(&jpeg_cache,&old_day));
    link=LINK_NONE;assert(!gallery_request_available(0,"2026-10-07",0,false));
    assert(gallery_request_available(1,"2026-10-08",2,true));
    puts("GALLERY_DAY_PREFETCH_OK both works/all pages/layouts; JPEG-only background; offline zero RPC; low-memory pause; failed rollover retention; one validated day");
    gallery_cache_reclaim();assert(!gallery_request_available(0,"2026-10-07",0,false));
    link=LINK_USB;bad_hash=true;assert(gallery_request(0,"2026-10-07",0,false,NULL));
    gallery_image image;assert(!gallery_take(&image));link=LINK_NONE;
    assert(!gallery_request_available(0,"2026-10-07",0,false));bad_hash=false;
    link=LINK_WIFI;http_failure=true;assert(gallery_request(0,"2026-10-07",0,false,NULL));
    assert(!gallery_take(&image)&&wifi_users==0);
    assert(wifi_stops==0); // Auto-mode BLE fallback and other links keep Wi-Fi.
    link=LINK_BLE;selected_mode=LINK_BLE;http_failure=false;gallery_cache_reclaim();
    display(0,0,false);assert(wifi_stops==1&&!wifi_starts&&atomic_load(&probe_stage)==PROBE_QUIET);
    vTaskDelay(601);assert(wifi_running&&wifi_starts==1&&!wifi_probe_pending());
    display(0,0,false);assert(wifi_stops==1); // Cached images never stop Wi-Fi.
    bad_hash=true;assert(gallery_request(0,"2026-10-07",1,false,NULL));
    assert(!gallery_take(&image)&&wifi_stops==2&&wifi_starts==2&&wifi_running&&!wifi_probe_pending());bad_hash=false;
    wifi_stop_error=259;display(0,1,false);assert(wifi_stops==3&&wifi_starts==2&&!wifi_probe_pending());wifi_stop_error=0;
    hold_wifi_worker=true;int before=wifi_stops;display(0,2,false);
    assert(!wifi_probe_ready()&&wifi_stops==before); // 500 ms budget falls back to coexistence.
    hold_wifi_worker=false;tab5_wifi_wake();assert(!wifi_probe_pending()&&wifi_stops==before);
    tab5_wifi_initialized=false;display(1,0,false);assert(wifi_stops==before);tab5_wifi_initialized=true;
    // Real .148 failure: portrait prefetch starts before the previous restore
    // has run. Both consecutive images must get their own pause, not only one.
    gallery_cache_reclaim();defer_wifi_restore=true;immediate_gallery_release=true;before=wifi_stops;
    display(0,0,false);assert(wifi_probe_pending()&&!atomic_load(&requested)&&wifi_stops==before+1);
    display(0,0,true);assert(wifi_probe_pending()&&!atomic_load(&requested)&&wifi_stops==before+2);
    defer_wifi_restore=false;immediate_gallery_release=false;tab5_wifi_wake();assert(wifi_running&&!wifi_probe_pending());
    gallery_cache_reclaim();before=wifi_stops;int started_before=wifi_starts;
    display(0,0,false);display(0,0,true);
    assert(wifi_stops==before+1&&wifi_starts==started_before&&atomic_load(&probe_stage)==PROBE_QUIET);
    vTaskDelay(601);assert(wifi_running&&wifi_starts==started_before+1&&!wifi_probe_pending());
    gallery_cache_reclaim();free(gallery_take_buffer());
    char temporary[MAX_PATH];GetTempPathA(sizeof(temporary),temporary);snprintf(test_card_root,sizeof(test_card_root),"%saibot-tf-worker-%lu",temporary,GetCurrentProcessId());assert(!_mkdir(test_card_root));
    test_card=true;link=LINK_USB;selected_mode=LINK_AUTO;
    assert(!atomic_load(&storage_enabled));unsigned operations_before=test_card_operations;
    display(0,0,false);assert(test_card_operations==operations_before&&card_writes==0);
    gallery_library_supported(true);assert(!gallery_library_pending());int default_calls=network_calls;gallery_sync_library();assert(network_calls==default_calls);
    gallery_cache_reclaim();gallery_storage_enable(true);
    display(0,0,false);display(0,0,true);display(1,0,false);
    assert(card_writes==3);
    // Turning TF off also bypasses already saved records without deleting them.
    gallery_cache_reclaim();gallery_storage_enable(false);operations_before=test_card_operations;
    display(0,0,false);assert(test_card_operations==operations_before&&card_hits==0&&card_writes==3);
    gallery_storage_enable(true);
    puts("GALLERY_TF_OPT_IN_OK default/disabled with mounted card uses original cache and bridge; no card record I/O or library sync; existing TF data preserved");
    int persisted_calls=network_calls;gallery_cache_reclaim();
    free(guides[0]);free(guides[1]);guides[0]=guides[1]=NULL;memset(pack_revisions,0,sizeof(pack_revisions)); // reboot state
    link=LINK_NONE;display(0,0,false);display(0,0,true);display(1,0,false);
    assert(network_calls==persisted_calls&&card_hits==3);
    // Authenticated new text/image revision excludes stale TF files.
    assert(gallery_pack_sync(painting_revision,calligraphy_revision)==3);gallery_cache_reclaim();
    painting_revision[0]='e';assert(gallery_pack_sync(painting_revision,calligraphy_revision)==1);
    assert(gallery_request(0,"2026-10-07",0,false,NULL));assert(!gallery_take(&image));assert(network_calls==persisted_calls);
    link=LINK_USB;display(0,0,false);assert(network_calls==persisted_calls+3);
    test_card=false;gallery_cache_reclaim();display(0,0,true);assert(network_calls==persisted_calls+6);
    gallery_cache_reclaim();free(gallery_take_buffer());
    puts("GALLERY_TF_WORKER_OK verified downloads persist; reboot restores both orientations and reading text with zero RPC; new revision rejects stale card data; absent card falls back to bridge");
    // A RAM JPEG promoted after TF opt-in must retain the displayed work's
    // complete text, even though the JPEG cache itself owns no guide pointer.
    test_card=true;link=LINK_USB;gallery_storage_enable(false);gallery_cache_reclaim();
    assert(gallery_request(0,"2029-01-01",0,false,NULL)&&gallery_take(&image));gallery_recycle(image.pixels);
    gallery_storage_enable(true);int cached_calls=network_calls;
    assert(start_request(0,"2029-01-01",0,false,NULL,true,0));assert(network_calls==cached_calls);
    gallery_cache_reclaim();gallery_guide_free(guides[0]);guides[0]=NULL;link=LINK_NONE;
    assert(gallery_request(0,"2029-01-01",0,false,NULL)&&gallery_take(&image));
    gallery_guide promoted;assert(gallery_guide_copy(0,image.date,image.work_id,&promoted)&&!strcmp(promoted.overview,"museum context"));gallery_recycle(image.pixels);
    puts("GALLERY_CACHED_GUIDE_PERSIST_OK TF opt-in promotion preserves complete guide; cold offline reload retains reading without RPC");
    test_card=true;link=LINK_USB;selected_mode=LINK_AUTO;
    // Lose the final range, abandon every RAM-owned transfer state, then use
    // another link. The already authenticated/checksummed prefix stays on TF.
    fail_offset=49152;fail_remaining=3;fail_status=0;int first_before=first_ranges,tail_before=tail_ranges;
    assert(start_request(0,"2030-01-01",1,true,NULL,true,1));
    assert(first_ranges==first_before+1&&tail_ranges==tail_before+3);
    memset(&prefetch_job,0,sizeof(prefetch_job));prefetch_retry=0;fail_offset=-1;link=LINK_WIFI;
    assert(start_request(0,"2030-01-01",1,true,NULL,true,1));
    assert(first_ranges==first_before+1&&tail_ranges==tail_before+4);
    gallery_cache_reclaim();link=LINK_NONE;assert(gallery_request(0,"2030-01-01",1,true,NULL)&&gallery_take(&image));gallery_recycle(image.pixels);
    puts("GALLERY_RANGE_RESUME_OK complete-file failure retains checked prefix; cold worker/reconnect via another link downloads only missing range; final offline image verified");
    test_card=true;test_catalog=true;fixture_guide_version=3;link=LINK_USB;ticks+=60000;prefetch_retry=0;
    memset(remote_revisions[0],'7',64);memset(remote_revisions[1],'8',64);
    assert(gallery_pack_sync(remote_revisions[0],remote_revisions[1])==3);gallery_library_supported(true);
    assert(gallery_library_pending());
    unsigned ranges_before=range_calls,full_before=full_guides,ref_before=referenced_guides;int library_decodes_before=decodes;
    for(int i=0;i<18;i++)gallery_sync_library();
    assert(catalogs[0].next>=1&&catalogs[1].next>=1&&catalogs[0].next<3);
    // A cold restart resumes completed works and reuses saved partial pages.
    memset(catalogs,0,sizeof(catalogs));memset(catalog_loaded,0,sizeof(catalog_loaded));memset(sync_cursor,0,sizeof(sync_cursor));memset(catalog_check_after,0,sizeof(catalog_check_after));
    for(int i=0;i<50;i++)gallery_sync_library();
    assert(catalogs[0].next==3&&catalogs[1].next==2&&range_calls-ranges_before==60&&decodes==library_decodes_before);
    assert(full_guides-full_before==5&&referenced_guides-ref_before==25);
    assert(!gallery_library_pending());
    int idle_calls=network_calls;unsigned idle_writes=card_writes;
    assert(gallery_pack_sync(remote_revisions[0],remote_revisions[1])==0);
    for(int i=0;i<20;i++)gallery_sync_library();
    assert(network_calls==idle_calls&&card_writes==idle_writes);
    puts("GALLERY_UNCHANGED_LIBRARY_OK same authenticated digests and completed cursors cause zero network requests and zero writes");
    char storage[160];gallery_storage_status(storage,sizeof(storage));assert(strstr(storage,"保存完成"));
    // Whole collection, not just today's work: both layouts of all pages are
    // available after another reboot, including a different calendar year.
    gallery_cache_reclaim();memset(pack_revisions,0,sizeof(pack_revisions));memset(catalogs,0,sizeof(catalogs));memset(catalog_loaded,0,sizeof(catalog_loaded));
    link=LINK_NONE;int calls_before=network_calls;
    for(int c=0;c<2;c++)for(int w=0;w<(c?2:3);w++)for(int page=0;page<3;page++)for(int portrait=0;portrait<2;portrait++) {
        char date[11];assert(gallery_catalog_date(w,date));assert(gallery_request(c,date,page,portrait,NULL));assert(gallery_take(&image));
        assert(image.pixels[0]==1+c*20+w*50+page*2+portrait);gallery_guide g;assert(gallery_guide_copy(c,image.date,image.work_id,&g)&&!strcmp(g.overview,"museum context"));gallery_recycle(image.pixels);
    }
    assert(gallery_request(1,"2028-02-29",2,true,NULL)&&gallery_take(&image));assert(image.pixels[0]==76&&!strcmp(image.date,"2028-02-29"));gallery_recycle(image.pixels);assert(network_calls==calls_before);
    // Text-only category update reuses identical JPEG bytes, fetches no ranges,
    // and leaves the other complete collection alone.
    assert(gallery_pack_sync(remote_revisions[0],remote_revisions[1])==3);
    link=LINK_WIFI;remote_revisions[0][0]='9';fixture_overview="updated complete reading text";
    assert(gallery_pack_sync(remote_revisions[0],remote_revisions[1])==1);ranges_before=range_calls;
    full_before=full_guides;ref_before=referenced_guides;gallery_store_profile_reset();
    for(int i=0;i<50;i++)gallery_sync_library();
    assert(catalogs[0].next==3&&catalogs[1].next==2&&range_calls==ranges_before);
    gallery_io_profile delta=gallery_store_profile_get();
    assert(full_guides-full_before==3&&referenced_guides-ref_before==15&&delta.write_bytes<32768);
    printf("GALLERY_INCREMENTAL_WORKER_OK 18 unchanged JPEGs: zero image ranges, %u TF write bytes; guides=3 full+15 references; both layouts offline with updated text\n",delta.write_bytes);
    gallery_cache_reclaim();link=LINK_NONE;assert(gallery_request(0,"2026-01-03",2,true,NULL)&&gallery_take(&image));
    gallery_guide updated;assert(gallery_guide_copy(0,image.date,image.work_id,&updated)&&!strcmp(updated.overview,fixture_overview));gallery_recycle(image.pixels);
    // A lying reference must not publish another work's text. An old bridge
    // that ignores the hints still returns a full guide and completes normally.
    link=LINK_WIFI;memset(remote_revisions[0],'a',64);assert(gallery_pack_sync(remote_revisions[0],NULL)==1);
    assert(start_request(0,"2035-01-01",0,false,NULL,true,1));
    unsigned writes_before=card_writes;ranges_before=range_calls;
    bad_guide_reference=true;assert(start_request(0,"2035-01-01",0,true,NULL,true,1));bad_guide_reference=false;
    assert(card_writes==writes_before&&range_calls==ranges_before);
    guide_references=false;full_before=full_guides;
    assert(start_request(0,"2035-01-01",0,true,NULL,true,1)&&card_writes==writes_before+1&&full_guides==full_before+1);
    guide_references=true;
    puts("GALLERY_GUIDE_REFERENCE_WORKER_OK wrong-work reference rejected before image reads/writes; old bridge full-guide fallback retained");
    // Real production card-writer task on a separate OS thread: slow the file
    // writer deliberately and prove incoming blocks overlap it, with bounded
    // producer lead. No physical-card throughput is inferred from this test.
    image_bytes=1048576;stream_enabled=true;slow_card=true;test_catalog=false;link=LINK_WIFI;
    memset(pack_revisions[0],'4',64);pack_revisions[0][64]=0;
    catalogs[0]=(gallery_catalog){.works=366};strcpy(catalogs[0].revision,pack_revisions[0]);catalog_loaded[0]=true;
    int stream_before=stream_calls;
    assert(start_request(0,"2026-03-17",0,false,NULL,true,1));
    assert(stream_calls==stream_before+1&&overlapping_receives>0&&stream_offset==0);
    assert(atomic_load(&transfer_received)==1048576&&atomic_load(&transfer_written)==1048576&&atomic_load(&transfer_overlap)>0);
    stream_cut=245760;assert(start_request(0,"2026-03-17",1,false,NULL,true,1));
    memset(&prefetch_job,0,sizeof(prefetch_job));stream_cut=-1;
    assert(start_request(0,"2026-03-17",1,false,NULL,true,1)&&stream_offset==245760);
    gallery_cache_reclaim();link=LINK_NONE;
    assert(gallery_request(0,"2026-03-17",1,false,NULL)&&gallery_take(&image));gallery_recycle(image.pixels);
    assert(!atomic_load(&card_writing));
    puts("GALLERY_RECEIVE_WRITE_PARALLEL_OK 1MiB continuous receive overlaps actual file-writer thread; bounded backpressure; interrupted verified prefix245760 resumes without reread; final offline JPEG/guide");
    // Explicit samples use the actual production worker and a separate store.
    link=LINK_WIFI;gallery_catalog speed_before[2]={catalogs[0],catalogs[1]};
    gallery_http_lease idle_http=gallery_http_take_idle(&library_http_idle);
    if(idle_http.client)esp_http_client_cleanup(idle_http.client);
    unsigned http_before=http_created;
    unsigned ranges=range_calls;assert(gallery_speed_start());assert(!gallery_speed_start());
    // Completed libraries must still request the existing UI memory preparation
    // while a diagnostic sample is queued. Normal idle stays non-pending.
    catalogs[0].next=catalogs[0].works;catalogs[1].next=catalogs[1].works;
    speed_before[0]=catalogs[0];speed_before[1]=catalogs[1];
    assert(gallery_library_pending());free_psram=4u*1024u*1024u;
    unsigned trim_before=memory_trim_calls;gallery_sync_with_memory();
    assert(memory_trim_calls==trim_before+1&&free_psram==5u*1024u*1024u&&speed_samples==1);
    for(int i=1;i<8;i++)gallery_sync_with_memory();
    free_psram=16u*1024u*1024u;
    char speed[256];gallery_speed_diagnostics(speed,sizeof(speed));
    assert(atomic_load(&speed_phase)==3&&speed_samples==8&&speed_bytes==8u*1048576&&range_calls>ranges);
    assert(http_created==http_before+1); // Actual worker reuses one exclusive SDK handle for all eight files.
    puts("GALLERY_HTTP_WORKER_REUSE_OK 8 actual production-worker samples one SDK client; authenticated reads and file checkpoints unchanged");
    printf("GALLERY_SPEED_PROFILE bytes=%u write=%u bulk=%u sync=%u\n",speed_bytes,speed_io.write_bytes,speed_io.bulk_bytes,speed_io.sync_calls);fflush(stdout);
    // First use has no old image slots to read. Bulk bytes can be slightly
    // below payload bytes because partial-sector edges are counted separately.
    assert(speed_io.write_bytes>=speed_bytes&&speed_io.bulk_bytes>speed_bytes*99u/100u&&speed_io.sync_calls>0);
    assert(speed_io.bulk_bytes+speed_io.edge_bytes==speed_io.read_bytes+speed_io.write_bytes);
    assert(strstr(speed,";gallery_io,")&&strstr(speed,";gallery_rpc,"));
    assert(!memcmp(speed_before,catalogs,sizeof(speed_before))&&speed_overlap>0);
    assert(gallery_speed_start());assert(!speed_io.write_bytes&&!speed_rpc.calls);for(int i=0;i<8;i++)gallery_sync_library();
    assert(speed_samples==8&&speed_bytes==8u*1048576); // never a cache hit
    assert(!memcmp(speed_before,catalogs,sizeof(speed_before)));
    assert(gallery_speed_start());gallery_speed_cancel();assert(atomic_load(&speed_phase)==5);
    puts("GALLERY_SPEED_ISOLATION_OK eight worker samples, repeated network reads, unchanged installed cursors, overlap and cancellation");
    image_bytes=65536;stream_enabled=slow_card=false;
    test_card=false;link=LINK_USB;calls_before=network_calls;gallery_sync_library();assert(network_calls==calls_before);
    test_card=true;link=LINK_BLE;gallery_sync_library();assert(network_calls==calls_before);
    gallery_library_supported(false);link=LINK_USB;gallery_sync_library();assert(network_calls==calls_before);
    atomic_store(&prefetching,true);atomic_store(&cancel_prefetch,false);
    gallery_library_supported(false);assert(!atomic_load(&cancel_prefetch));atomic_store(&prefetching,false);
    puts("GALLERY_LIBRARY_SYNC_OK entire two collections/all pages/layouts saved without RGB; cold resume with no duplicate transfers; all works offline; cross-year reuse; text-only update zero image ranges; BLE/absent-card/old-bridge pause");
    printf("GALLERY_WORKER_CACHE_OK %d decodes; 3 transports populate both layouts; 72 offline reopens with zero RPC; date/hash/failure rejection; Wi-Fi lease released\n",decodes);
    idle_http=gallery_http_take_idle(&library_http_idle);if(idle_http.client)esp_http_client_cleanup(idle_http.client);
    assert(http_created==http_cleaned);
    return 0;
}
