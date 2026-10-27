#include "gallery_remote.h"
#include "gallery_jpeg_cache.h"
#include "gallery_store.h"
#include "sd_storage.h"
#include "wifi_performance.h"
#include "gallery_read_pipeline.h"
#include "wifi_probe.h"
#include "wifi_status.h"
#include "esp_timer.h"
#include "device.h"
#include "rpc_link.h"
#include "ota_update.h"
#include "transport_benchmark.h"
#include "camera_preview.h"
#include "camera_gallery_buffer.h"
#include "photo_attachment.h"
#include "voice_remote.h"
#include "esp_http_client.h"
#include "esp_heap_caps.h"
#include "esp_cache.h"
#include "driver/jpeg_decode.h"
#include "freertos/idf_additions.h"
#include "mbedtls/sha256.h"
#include <stdatomic.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#define GALLERY_TEXT_ALLOC(bytes) heap_caps_calloc(1,(bytes),MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT)
#include "gallery_text_pages.h"

static atomic_bool working;
static atomic_bool prefetching,cancel_prefetch;
// Explicit, bounded acceptance sample. Never reads or replaces the gallery
// cache: the exact production receive/write worker uses an isolated root.
static atomic_int speed_phase; // idle, queued, running, complete, failed, cancelled
static unsigned speed_samples,speed_bytes,speed_total_ms,speed_receive_ms,speed_write_ms,speed_overlap;
static int speed_error;static link_kind speed_link=LINK_NONE;
static int64_t speed_deadline,speed_started;
static gallery_io_profile speed_io;
static gallery_rpc_profile speed_rpc;
static const char *speed_root=SD_STORAGE_MOUNT "/aibot/cache/gallery-speed";
static bool speed_active(void){int p=atomic_load(&speed_phase);return p==1||p==2;}
bool gallery_speed_start(void) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    bool ok=!speed_active()&&!(atomic_load(&speed_phase)==5&&atomic_load(&prefetching));
    if(ok){speed_samples=speed_bytes=speed_total_ms=speed_receive_ms=speed_write_ms=speed_overlap=0;speed_error=0;speed_link=LINK_NONE;speed_started=0;speed_deadline=esp_timer_get_time()+180000000;atomic_store(&speed_phase,1);}
    if(ok){memset(&speed_io,0,sizeof(speed_io));memset(&speed_rpc,0,sizeof(speed_rpc));}
    xSemaphoreGive(device_mutex);return ok;
}
void gallery_speed_cancel(void){if(speed_active()){int previous=atomic_exchange(&speed_phase,5);if(previous==2)atomic_store(&cancel_prefetch,true);}}
void gallery_speed_diagnostics(char *out,size_t size) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    const char *states[]={"idle","running","running","complete","failed","cancelled"};
    snprintf(out,size,"%s;gallery,%s,%u,%u,%u,%u,%u,%u,%d",states[atomic_load(&speed_phase)],speed_link==LINK_WIFI?"Wi-Fi":speed_link==LINK_USB?"USB":"none",speed_samples,speed_bytes,speed_total_ms,speed_receive_ms,speed_write_ms,speed_overlap,speed_error);
    size_t used=strlen(out);
    if(used<size)snprintf(out+used,size-used,";gallery_io,%u,%u,%u,%u,%u,%u,%u,%u;gallery_rpc,%u,%u,%u,%u,%u;gallery_rx,%u,%u,%u,%u",
        speed_io.read_bytes,speed_io.write_bytes,speed_io.read_us/1000,speed_io.write_us/1000,speed_io.sync_us/1000,speed_io.sync_calls,speed_io.bulk_bytes,speed_io.edge_bytes,
        (unsigned)(speed_rpc.encode_us/1000),(unsigned)(speed_rpc.exchange_us/1000),(unsigned)(speed_rpc.decode_us/1000),(unsigned)(speed_rpc.backpressure_us/1000),speed_rpc.calls,
        (unsigned)(speed_rpc.network_read_us/1000),(unsigned)(speed_rpc.consumer_wait_us/1000),speed_rpc.receive_ahead_calls,speed_rpc.receive_peak_bytes);
    xSemaphoreGive(device_mutex);
}
static atomic_int_fast64_t prefetch_retry;
static unsigned prefetch_cursor;
static bool start_request(int category,const char *date,int frame,bool portrait,uint8_t *spare,bool background,int purpose);
static gallery_guide *guides[2];
// Only the current complete original for each background category is kept.
// Its revision/date/work/hash must match before reusing checked text pages.
static gallery_guide *library_guides[2];
static char library_guide_revisions[2][65];
void gallery_guide_reader_release(gallery_guide *g) {
    if(!g)return;
    for(int i=0;i<32;i++){free(g->transcription_pages[i]);g->transcription_pages[i]=NULL;}
}
bool gallery_guide_reader_copy(int category,const char *date,const char *work_id,gallery_guide *out) {
    if(category<0||category>1||!date||!work_id||!out)return false;
    gallery_guide_reader_release(out);
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    gallery_guide *g=guides[category];bool ok=g&&!strcmp(g->date,date)&&!strcmp(g->work_id,work_id);
    if(ok) {
        *out=*g;memset(out->transcription_pages,0,sizeof(out->transcription_pages));
        for(int i=0;ok&&i<g->transcription_page_count&&g->transcription_page_count>1;i++)ok=gallery_text_page_set(out,i,g->transcription_pages[i]);
    }
    xSemaphoreGive(device_mutex);
    if(!ok)gallery_guide_reader_release(out);
    return ok;
}
bool gallery_guide_copy(int category,const char *date,const char *work_id,gallery_guide *out) {
    return gallery_guide_page_copy(category,date,work_id,0,out);
}
bool gallery_guide_page_copy(int category,const char *date,const char *work_id,int page,gallery_guide *out) {
    if(category<0||category>1||!date||!work_id||!work_id[0]||!out)return false;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    gallery_guide *g=guides[category];bool ok=g&&!strcmp(g->date,date)&&!strcmp(g->work_id,work_id)&&page>=0&&page<(g->transcription_page_count?g->transcription_page_count:1);
    if(ok) {
        *out=*g;memset(out->transcription_pages,0,sizeof(out->transcription_pages));
        if(g->transcription_page_count>1) {
            if(!g->transcription_pages[page])ok=false;
            else snprintf(out->transcription,sizeof(out->transcription),"%s",g->transcription_pages[page]);
        }
    }
    xSemaphoreGive(device_mutex);return ok;
}
static bool guide_text(cJSON *root,const char *key,char *out,size_t size) {
    const char *value=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(root,key));
    if(!value)value="";
    size_t n=strlen(value);if(n>=size)return false;memcpy(out,value,n+1);return true;
}
static gallery_guide *guide_read(cJSON *root,const char *date) {
    if(!cJSON_IsObject(root))return NULL;
    gallery_guide *g=heap_caps_calloc(1,sizeof(*g),MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);if(!g)return NULL;
    snprintf(g->date,sizeof(g->date),"%s",date);
    bool ok=guide_text(root,"workId",g->work_id,sizeof(g->work_id))&&g->work_id[0]&&
        guide_text(root,"title",g->title,sizeof(g->title))&&guide_text(root,"author",g->author,sizeof(g->author))&&
        guide_text(root,"period",g->period,sizeof(g->period))&&guide_text(root,"museum",g->museum,sizeof(g->museum))&&
        guide_text(root,"brief",g->brief,sizeof(g->brief))&&guide_text(root,"overview",g->overview,sizeof(g->overview))&&
        guide_text(root,"looking",g->looking,sizeof(g->looking))&&guide_text(root,"transcription",g->transcription,sizeof(g->transcription))&&
        guide_text(root,"explanation",g->explanation,sizeof(g->explanation));
    cJSON *count=cJSON_GetObjectItemCaseSensitive(root,"transcriptionPages");
    if(count) {
        ok=ok&&cJSON_IsNumber(count)&&count->valuedouble==count->valueint&&count->valueint>=1&&count->valueint<=32;
        if(ok)g->transcription_page_count=count->valueint;
        if(ok&&g->transcription_page_count>1) {
            ok=guide_text(root,"transcriptionSha256",g->transcription_sha256,sizeof(g->transcription_sha256))&&
                strlen(g->transcription_sha256)==64&&strspn(g->transcription_sha256,"0123456789abcdef")==64&&gallery_text_page_set(g,0,g->transcription);
            cJSON *pages=cJSON_GetObjectItemCaseSensitive(root,"transcriptionPageTexts");
            if(pages) {
                ok=ok&&cJSON_IsArray(pages)&&cJSON_GetArraySize(pages)==g->transcription_page_count;
                for(int i=0;ok&&i<g->transcription_page_count;i++)ok=gallery_text_page_set(g,i,cJSON_GetStringValue(cJSON_GetArrayItem(pages,i)));
                ok=ok&&gallery_text_pages_complete(g);
            }
        }
    }
    if(!ok){gallery_guide_free(g);return NULL;}return g;
}
static atomic_int pack_status[2];
static atomic_int decode_status[4];
// Fixed-size counters only: no heap traversal or render-path instrumentation.
static atomic_int diagnostic_stage,diagnostic_error,diagnostic_category,diagnostic_portrait;
static atomic_uint diagnostic_requests,diagnostic_allocations;
static atomic_int diagnostic_frame,diagnostic_reused;
static atomic_uint diagnostic_fetch_ms,diagnostic_decode_ms,diagnostic_total_ms;
static atomic_uint failed_slots;
static gallery_jpeg_cache jpeg_cache;
static char pack_revisions[2][65];
static unsigned card_hits,card_writes,card_failures;
static char stored_date[11];
static uint64_t stored_pages[2][2];
static int stored_frames[2]={1,1};
static gallery_catalog catalogs[2];
static bool catalog_loaded[2];
static unsigned sync_cursor[2],sync_category;
static atomic_bool library_enabled;
static atomic_bool storage_enabled;
void gallery_storage_enable(bool enabled) {
    bool previous=atomic_exchange(&storage_enabled,enabled);
    if(previous&&!enabled&&atomic_load(&prefetching))atomic_store(&cancel_prefetch,true);
}
void gallery_library_supported(bool enabled){bool previous=atomic_exchange(&library_enabled,enabled);if(previous&&!enabled&&atomic_load(&prefetching))atomic_store(&cancel_prefetch,true);}
void gallery_storage_status(char *out,size_t size) {
    if(!atomic_load(&storage_enabled)){snprintf(out,size,"使用设备缓存");return;}
    xSemaphoreTake(device_mutex,portMAX_DELAY);gallery_catalog p=catalogs[0],c=catalogs[1];unsigned failures=card_failures;xSemaphoreGive(device_mutex);
    if(sd_storage_state()!=1)snprintf(out,size,"等待 TF 卡就绪");
    else if(failures&&esp_timer_get_time()/1000<prefetch_retry)snprintf(out,size,"保存暂停，请检查 TF 卡");
    else if(!p.works&&!c.works)snprintf(out,size,"等待电脑已安装的图库");
    else if(p.next==p.works&&c.next==c.works)snprintf(out,size,"保存完成 · 名画 %u · 书法 %u",p.works,c.works);
    else snprintf(out,size,"正在保存 · 名画 %u/%u · 书法 %u/%u",p.next,p.works,c.next,c.works);
}
static int64_t catalog_check_after[2];
static void catalog_load(int category,const char *revision,bool force) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);bool loaded=catalog_loaded[category];xSemaphoreGive(device_mutex);
    if(!atomic_load(&storage_enabled)||(loaded&&!force)||!sd_storage_lock())return;
    gallery_catalog value={0};gallery_catalog_load(SD_STORAGE_MOUNT,category,revision,&value);
    sd_storage_unlock(gallery_store_media_error());
    xSemaphoreTake(device_mutex,portMAX_DELAY);catalogs[category]=value;catalog_loaded[category]=true;xSemaphoreGive(device_mutex);
}
static bool catalog_save(int category,const gallery_catalog *value) {
    if(!atomic_load(&storage_enabled)||!sd_storage_lock())return false;
    bool ok=gallery_catalog_save(SD_STORAGE_MOUNT,category,value);sd_storage_unlock(gallery_store_media_error());
    if(!ok){xSemaphoreTake(device_mutex,portMAX_DELAY);card_failures++;xSemaphoreGive(device_mutex);}return ok;
}
static gallery_image card_key(const gallery_image *key,const char *revision) {
    gallery_image value=*key;
    xSemaphoreTake(device_mutex,portMAX_DELAY);gallery_catalog catalog=catalogs[key->category];xSemaphoreGive(device_mutex);
    if(catalog.works&&(!*revision||!strcmp(catalog.revision,revision)))gallery_catalog_key(key->date,catalog.works,value.date);
    return value;
}
static uint8_t *store_allocate(size_t bytes,size_t *capacity) {
    const jpeg_decode_memory_alloc_cfg_t cfg={.buffer_direction=JPEG_DEC_ALLOC_INPUT_BUFFER};
    return jpeg_alloc_decoder_mem(bytes,&cfg,capacity);
}
static gallery_jpeg card_take(const gallery_image *key,const char *revision,gallery_guide **guide) {
    gallery_jpeg value={0};cJSON *raw=NULL;
    if(!atomic_load(&storage_enabled)||!sd_storage_lock())return value;
    gallery_image normalized=card_key(key,revision);
    bool ok=gallery_store_get(SD_STORAGE_MOUNT,&normalized,revision,store_allocate,&value,&raw);
    sd_storage_unlock(gallery_store_media_error());
    if(ok&&raw) {
        *guide=guide_read(raw,key->date);
        if(!*guide||strcmp((*guide)->work_id,value.key.work_id)||((*guide)->transcription_page_count>1&&!gallery_text_pages_complete(*guide))){gallery_guide_free(*guide);*guide=NULL;free(value.data);value=(gallery_jpeg){0};ok=false;}
    }
    cJSON_Delete(raw);
    if(ok)snprintf(value.key.date,sizeof(value.key.date),"%s",key->date);
    if(ok){xSemaphoreTake(device_mutex,portMAX_DELAY);card_hits++;xSemaphoreGive(device_mutex);}
    return value;
}
static bool card_put(const char *root,const gallery_jpeg *value,const char *revision,const gallery_guide *guide,bool track_day,const gallery_resume *resume,bool unchanged) {
    if(!atomic_load(&storage_enabled))return false;
    gallery_jpeg normalized=*value;normalized.key=card_key(&value->key,revision);
    gallery_guide *adjusted=NULL;
    if(guide&&strcmp(guide->date,normalized.key.date)) {
        adjusted=heap_caps_calloc(1,sizeof(*adjusted),MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);if(!adjusted)return false;
        *adjusted=*guide;snprintf(adjusted->date,sizeof(adjusted->date),"%s",normalized.key.date);guide=adjusted;
    }
    if(!sd_storage_lock()){free(adjusted);return false;}
    bool ok=unchanged&&gallery_store_refresh(root,&normalized,revision,guide);
    if(!ok)ok=resume?gallery_store_finish(root,&normalized,revision,guide,resume):gallery_store_put(root,&normalized,revision,guide);
    sd_storage_unlock(gallery_store_media_error());
    free(adjusted);
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    if(ok) {
        card_writes++;
        if(track_day) {
            if(strcmp(stored_date,value->key.date)){snprintf(stored_date,sizeof(stored_date),"%s",value->key.date);memset(stored_pages,0,sizeof(stored_pages));stored_frames[0]=stored_frames[1]=1;}
            stored_pages[value->key.category][value->key.portrait]|=UINT64_C(1)<<value->key.frame;
            stored_frames[value->key.category]=value->key.frames;
        }
    }else card_failures++;
    xSemaphoreGive(device_mutex);
    return ok;
}
static void card_resume_load(const char *root,const gallery_image *key,const char *revision,const char *sha,uint8_t *jpeg,size_t total,gallery_resume *state) {
    gallery_image normalized=card_key(key,revision);
    if(!atomic_load(&storage_enabled)||!sd_storage_lock())return;
    gallery_resume_load(root,&normalized,revision,sha,jpeg,total,state);
    sd_storage_unlock(gallery_store_media_error());
}
static bool card_resume_append(const char *root,const gallery_image *key,const char *revision,const char *sha,const uint8_t *jpeg,size_t total,size_t end,gallery_resume *state) {
    gallery_image normalized=card_key(key,revision);
    if(!atomic_load(&storage_enabled)||!sd_storage_lock())return false;
    bool ok=gallery_resume_append(root,&normalized,revision,sha,jpeg,total,end,state);
    sd_storage_unlock(gallery_store_media_error());
    if(!ok){xSemaphoreTake(device_mutex,portMAX_DELAY);card_failures++;xSemaphoreGive(device_mutex);}return ok;
}
static void card_resume_clear(const char *root,const gallery_image *key,const char *revision,const char *sha,size_t total) {
    gallery_image normalized=card_key(key,revision);
    if(!atomic_load(&storage_enabled)||!sd_storage_lock())return;
    gallery_resume_clear(root,&normalized,revision,sha,total);
    sd_storage_unlock(gallery_store_media_error());
}
void gallery_cache_reclaim(void) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);gallery_jpeg_clear(&jpeg_cache);xSemaphoreGive(device_mutex);
}
unsigned gallery_take_failures(void){return atomic_exchange(&failed_slots,0);}
void gallery_diagnostics(char *out,size_t size) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    unsigned hits=jpeg_cache.hits,misses=jpeg_cache.misses,bytes=jpeg_cache.bytes,ch=card_hits,cw=card_writes,cf=card_failures;
    gallery_catalog painting=catalogs[0],calligraphy=catalogs[1];
    xSemaphoreGive(device_mutex);
    snprintf(out,size,"%u,%d,%d,%d,%d,%d,%d,%d,%u,%u,%u,%u,%u,%u,%u,%d,%u,%u,%u,%u,%u,%u,%u",atomic_load(&diagnostic_requests),atomic_load(&working)||atomic_load(&prefetching),atomic_load(&diagnostic_category),atomic_load(&diagnostic_portrait),atomic_load(&diagnostic_stage),atomic_load(&diagnostic_error),atomic_load(&diagnostic_frame),atomic_load(&diagnostic_reused),atomic_load(&diagnostic_allocations),hits,misses,bytes,atomic_load(&diagnostic_fetch_ms),atomic_load(&diagnostic_decode_ms),atomic_load(&diagnostic_total_ms),sd_storage_state(),ch,cw,cf,painting.next,painting.works,calligraphy.next,calligraphy.works);
}
int gallery_pack_status(int category){return category>=0&&category<2?atomic_load(&pack_status[category]):0;}
int gallery_decode_status(int category,bool portrait){return category>=0&&category<2?atomic_load(&decode_status[category*2+portrait]):GALLERY_DECODE_OK;}
static gallery_image result;
typedef struct {gallery_image image;link_kind transport;bool background,cached_only;int purpose;unsigned request_id;char revision[65];} gallery_work;
static gallery_work foreground_job,prefetch_job;
#include "gallery_http_cache.h"
static gallery_http_lease library_http_idle;
bool gallery_camera_prepare(void) {
    // camera_preview_busy() already gates new gallery requests and cancels
    // in-flight downloads. Their buffers/HTTP clients belong to those workers.
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    atomic_store(&cancel_prefetch,true);
    gallery_http_lease idle=gallery_http_take_idle(&library_http_idle);
    bool ready=!atomic_load(&working)&&!atomic_load(&prefetching);
    gallery_jpeg_clear(&jpeg_cache);
    xSemaphoreGive(device_mutex);
    // SDK cleanup can block; never hold the shared device/UI mutex here.
    if(idle.client)esp_http_client_cleanup(idle.client);
    if(ready) {
        // A completed decoder may have published a result after the saver was
        // hidden. No LVGL object owns it yet, and gallery_update() will not
        // consume it on the Codex page. Drain this owner as well as the spare.
        gallery_image pending;
        if(gallery_take(&pending))gallery_release_buffer(pending.pixels);
        uint8_t *spare;while((spare=gallery_take_buffer())!=NULL)gallery_release_buffer(spare);
    }
    return ready&&camera_gallery_buffers_idle();
}
static bool foreground_busy(void) {
    voice_remote_state voice;voice_remote_get(&voice);
    return voice.busy||ota_update_busy()||transport_benchmark_busy()||camera_preview_busy()||photo_state()==1;
}
static bool download_cancelled(link_kind transport,bool background) {
    int mode=device_get_config().mode;
    return (speed_active()&&esp_timer_get_time()>speed_deadline)||foreground_busy()||(background&&atomic_load(&cancel_prefetch))||(mode>=LINK_USB&&mode<=LINK_BLE&&mode!=transport);
}
static atomic_uint transfer_received,transfer_written,transfer_write_ms,transfer_overlap;
void gallery_transfer_diagnostics(char *out,size_t size) {
    snprintf(out,size,"%u,%u,%u,%u",atomic_load(&transfer_received),atomic_load(&transfer_written),atomic_load(&transfer_write_ms),atomic_load(&transfer_overlap));
}
#include "gallery_write_pipeline.h"
typedef struct {
    gallery_write_pipeline flow;atomic_bool writing;gallery_image key;const char *revision,*sha;uint8_t *jpeg;size_t total,base;
    gallery_resume *resume;const char *root;link_kind transport;bool background,started;unsigned request_id;size_t initial;
} gallery_card_writer;
static void card_writer_task(void *argument) {
    gallery_card_writer *w=argument;
    for(;;) {
        size_t end=gallery_write_next(&w->flow,GALLERY_RESUME_BLOCK);
        if(end>w->resume->offset) {
            size_t previous=w->resume->offset;int64_t began=esp_timer_get_time();atomic_store(&w->writing,true);
            bool saved=card_resume_append(w->root,&w->key,w->revision,w->sha,w->jpeg,w->total,end,w->resume);atomic_store(&w->writing,false);
            if(!saved){atomic_store(&w->flow.failed,true);break;}
            if(atomic_load(&diagnostic_requests)==w->request_id){atomic_fetch_add(&transfer_written,end-previous);atomic_fetch_add(&transfer_write_ms,(esp_timer_get_time()-began)/1000);}
            atomic_store(&w->flow.written,w->resume->offset);
        }else if(atomic_load(&w->flow.closed))break;
        else vTaskDelay(pdMS_TO_TICKS(1));
    }
    atomic_store(&w->flow.finished,true);vTaskDeleteWithCaps(NULL);
}
static bool card_writer_progress(void *argument,size_t verified) {
    gallery_card_writer *w=argument;
    if(download_cancelled(w->transport,w->background)||verified>w->total-w->base)return false;
    size_t end=w->base+verified;
    if(atomic_load(&diagnostic_requests)==w->request_id){atomic_store(&transfer_received,end-w->initial);if(atomic_load(&w->writing))atomic_fetch_add(&transfer_overlap,1);}
    if(!w->started) {
        while(end-w->resume->offset>=GALLERY_RESUME_BLOCK)
            if(!card_resume_append(w->root,&w->key,w->revision,w->sha,w->jpeg,w->total,w->resume->offset+GALLERY_RESUME_BLOCK,w->resume))return false;
        return true;
    }
    while(end-atomic_load(&w->flow.written)>2u*GALLERY_RESUME_BLOCK&&!atomic_load(&w->flow.failed)) {
        if(download_cancelled(w->transport,w->background))return false;
        vTaskDelay(pdMS_TO_TICKS(1));
    }
    if(atomic_load(&w->flow.failed))return false;
    atomic_store(&w->flow.verified,end);return true;
}
static void card_writer_finish(gallery_card_writer *w) {
    if(!w->started)return;
    atomic_store(&w->flow.closed,true);
    while(!atomic_load(&w->flow.finished))vTaskDelay(pdMS_TO_TICKS(1));
    w->started=false;
}
static cJSON *gallery_call(link_kind transport,bool background,void *http,cJSON *request,int *status,uint8_t *out,size_t count,size_t *received) {
    // Manifest and SHA-bound ranges are immutable reads. Only a missing reply
    // may be repeated, on the same link, with a fresh authenticated nonce.
    // Keep previously validated ranges; never retry auth/content errors.
    for(unsigned attempt=0;attempt<3;attempt++) {
        *status=0;if(received)*received=0;
        if(download_cancelled(transport,background))return NULL;
        char nonce[33];
        cJSON *reply=rpc_data_call(transport,http,request,status,nonce,NULL,0,out,count,received);
        if(*status||attempt==2)return reply;
        cJSON_Delete(reply);
        if(rpc_read_link_select()!=transport)return NULL;
        vTaskDelay(pdMS_TO_TICKS(100*(attempt+1)));
    }
    return NULL;
}
bool gallery_request_ready(void){return !atomic_load(&working)&&!atomic_load(&prefetching)&&!result.pixels&&!foreground_busy();}
unsigned gallery_pack_sync(const char *painting,const char *calligraphy) {
    if(speed_active())return 0;
    const char *values[2]={painting,calligraphy};unsigned changed=0;
    for(int c=0;c<2;c++) {
        const char *value=values[c];
        if(!value||strlen(value)!=64||strspn(value,"0123456789abcdef")!=64)continue;
        if(strcmp(value,pack_revisions[c]))changed|=1u<<c;
    }
    if(!changed)return 0;
    if(atomic_load(&prefetching))atomic_store(&cancel_prefetch,true);
    if(!gallery_request_ready())return 0;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    for(int c=0;c<2;c++)if(changed&(1u<<c)) {
        gallery_jpeg_clear_category(&jpeg_cache,c);gallery_guide_free(guides[c]);guides[c]=NULL;
        gallery_guide_free(library_guides[c]);library_guides[c]=NULL;library_guide_revisions[c][0]=0;
        snprintf(pack_revisions[c],sizeof(pack_revisions[c]),"%s",values[c]);
        memset(stored_pages[c],0,sizeof(stored_pages[c]));stored_frames[c]=1;
        catalogs[c]=(gallery_catalog){0};catalog_loaded[c]=false;sync_cursor[c]=0;catalog_check_after[c]=0;
        atomic_store(&pack_status[c],0);
        atomic_store(&decode_status[c*2],GALLERY_DECODE_OK);atomic_store(&decode_status[c*2+1],GALLERY_DECODE_OK);
    }
    xSemaphoreGive(device_mutex);
    atomic_fetch_and(&failed_slots,~((changed&1?3u:0u)|(changed&2?12u:0u)));
    prefetch_retry=0;return changed;
}
bool gallery_request_available(int category,const char *date,int frame,bool portrait) {
    if(speed_active())return false;
    if(atomic_load(&prefetching))atomic_store(&cancel_prefetch,true);
    if(atomic_load(&working)||result.pixels||foreground_busy()||category<0||category>1||!date||strlen(date)!=10||frame<0||frame>=64)return false;
    gallery_image key={.category=category,.frame=frame,.portrait=portrait};snprintf(key.date,sizeof(key.date),"%s",date);
    xSemaphoreTake(device_mutex,portMAX_DELAY);bool cached=gallery_jpeg_has(&jpeg_cache,&key);xSemaphoreGive(device_mutex);
    return cached||(atomic_load(&storage_enabled)&&sd_storage_available())||(!atomic_load(&prefetching)&&rpc_read_link_select()!=LINK_NONE);
}
static void worker(void *unused) {
    gallery_work context=*(gallery_work *)unused;
    gallery_image job=context.image;link_kind transport=context.transport;bool background=context.background;
    int64_t began=esp_timer_get_time(),decode_started=0;bool wifi_bulk=false,wifi_isolated=false,validated=false,persisted=false;
    bool sample=context.purpose==3,library=context.purpose!=0;
    const char *root=sample?speed_root:SD_STORAGE_MOUNT;
    int64_t receive_started=0,receive_ended=0;
    if(library&&!atomic_load(&storage_enabled)){if(sample)atomic_store(&speed_phase,4);atomic_store(&prefetching,false);vTaskDeleteWithCaps(NULL);return;}
    // A blocked background transfer owns no RGB output. Cached foreground
    // decoding can run independently, without a second network request.
#define record(counter,value) do {if(atomic_load(&diagnostic_requests)==context.request_id)atomic_store(&(counter),(value));} while(0)
    uint8_t *jpeg=NULL,*pixels=job.pixels;size_t capacity=0,jpeg_capacity=0;int length=0,offset=0;
    gallery_resume resume={0};bool resume_active=false;gallery_card_writer writer={0};
    const jpeg_decode_memory_alloc_cfg_t input={.buffer_direction=JPEG_DEC_ALLOC_INPUT_BUFFER},output={.buffer_direction=JPEG_DEC_ALLOC_OUTPUT_BUFFER};
    cJSON *reply=NULL,*request=NULL;
    gallery_guide *guide=NULL;
    esp_http_client_handle_t http=NULL;jpeg_decoder_handle_t codec=NULL;
    gallery_http_lease http_lease={0};unsigned http_calls_started=0;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    unsigned generation=jpeg_cache.generation;gallery_jpeg cached=library?(gallery_jpeg){0}:gallery_jpeg_take(&jpeg_cache,&job);
    xSemaphoreGive(device_mutex);
    bool from_card=false;gallery_jpeg stale={0};
    catalog_load(job.category,context.revision,context.purpose==2);
    xSemaphoreTake(device_mutex,portMAX_DELAY);gallery_catalog local_catalog=catalogs[job.category];xSemaphoreGive(device_mutex);
    if(!context.revision[0]&&local_catalog.works)snprintf(context.revision,sizeof(context.revision),"%s",local_catalog.revision);
    if(context.purpose==2) {
        if(!sd_storage_lock())goto done;
        sd_storage_unlock(false);
        if(local_catalog.works){validated=true;goto done;}
        goto network;
    }
    if(!sample&&!cached.data){cached=card_take(&job,context.revision,&guide);from_card=cached.data!=NULL;}
    if(cached.data&&!from_card&&background&&atomic_load(&storage_enabled)&&cached.key.work_id[0]) {
        // RAM JPEG entries own no guide. Persist an independent complete guide,
        // or refetch its manifest instead of writing a newer image-only record
        // over the same verified image/text revision on the card.
        guide=heap_caps_calloc(1,sizeof(*guide),MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
        if(guide&&!gallery_guide_reader_copy(job.category,job.date,cached.key.work_id,guide)){gallery_guide_free(guide);guide=NULL;}
        if(!guide){stale=cached;cached=(gallery_jpeg){0};goto network;}
    }
    if(cached.data) {
        jpeg=cached.data;jpeg_capacity=cached.capacity;length=cached.size;job.frames=cached.key.frames;snprintf(job.work_id,sizeof(job.work_id),"%s",cached.key.work_id);
        if(background){if(foreground_busy()||atomic_load(&cancel_prefetch))goto done;validated=true;persisted=from_card||card_put(root,&cached,context.revision,guide,!library,NULL,false);if(guide&&!library){xSemaphoreTake(device_mutex,portMAX_DELAY);gallery_guide_free(guides[job.category]);guides[job.category]=guide;guide=NULL;xSemaphoreGive(device_mutex);}record(diagnostic_stage,10);goto done;}
        atomic_store(&pack_status[job.category],0);goto decode;
    }
    if(library&&!sample){gallery_guide *old_guide=NULL;stale=card_take(&job,"",&old_guide);gallery_guide_free(old_guide);}
network:
    if(context.cached_only||transport==LINK_NONE)goto done; // Never overlap network mailbox owners.
    request=cJSON_CreateObject();
    if(!request)goto done;
    if(transport==LINK_BLE&&device_get_config().mode==LINK_BLE) {
        int64_t until=esp_timer_get_time()+500000;
        do {
            wifi_isolated=wifi_probe_begin_gallery();
            if(wifi_isolated||!wifi_probe_pending()||foreground_busy())break;
            // The other layout can start while the previous image's release
            // is still restoring Wi-Fi. Let that worker finish, then acquire
            // our own lease; never end/cancel somebody else's pending lease.
            tab5_wifi_wake();vTaskDelay(pdMS_TO_TICKS(10));
        }while(device_get_config().mode==LINK_BLE&&esp_timer_get_time()<until);
        if(wifi_isolated) {
            tab5_wifi_wake();
            // Do not add a multi-second Wi-Fi/driver wait to a small image.
            // Cancellation/restoration is worker-owned, including a stop that
            // finishes after this short preparation budget has expired.
            while(wifi_probe_pending()&&!wifi_probe_ready()&&!foreground_busy()&&esp_timer_get_time()<until)
                vTaskDelay(pdMS_TO_TICKS(10));
            if(!wifi_probe_ready()){wifi_probe_end();tab5_wifi_wake();wifi_isolated=false;}
        }
    }
    if(transport==LINK_WIFI) {
        wifi_performance_bulk(true);wifi_bulk=true;
        device_config config=device_get_config();char url[128];
        snprintf(url,sizeof(url),"http://%s:%d/tab5/v1/rpc",config.host,config.port);
        esp_http_client_config_t options={.url=url,.timeout_ms=8000,.disable_auto_redirect=true,
            .buffer_size=GALLERY_HTTP_READ_BYTES};
        if(library&&background) {
            xSemaphoreTake(device_mutex,portMAX_DELAY);http_lease=gallery_http_take_idle(&library_http_idle);xSemaphoreGive(device_mutex);
            if(!gallery_http_valid(&http_lease,url,(uint64_t)(esp_timer_get_time()/1000))) {
                if(http_lease.client)esp_http_client_cleanup(http_lease.client);
                http_lease=(gallery_http_lease){0};
            }
            http=http_lease.client;
        }
        if(!http) {
            http=esp_http_client_init(&options);if(!http)goto done;
            http_lease=(gallery_http_lease){.client=http,.created_ms=(uint64_t)(esp_timer_get_time()/1000)};
            snprintf(http_lease.endpoint,sizeof(http_lease.endpoint),"%s",url);
        }
        http_calls_started=rpc_gallery_profile_get().calls;
    }
    if(foreground_busy())goto done;
    cJSON_AddStringToObject(request,"kind","gallery");cJSON_AddStringToObject(request,"op",context.purpose==2?"catalog":"manifest");
    cJSON_AddBoolToObject(request,"includeGuide",true);
    cJSON_AddNumberToObject(request,"guideVersion",3);
    cJSON_AddStringToObject(request,"category",job.category?"calligraphy":"painting");
    cJSON_AddStringToObject(request,"date",job.date);cJSON_AddNumberToObject(request,"frame",job.frame);
    if(job.portrait)cJSON_AddStringToObject(request,"orientation","portrait");
    gallery_guide *previous=library?library_guides[job.category]:NULL;
    bool guide_hint=previous&&context.revision[0]&&!strcmp(library_guide_revisions[job.category],context.revision)&&
        !strcmp(previous->date,job.date)&&previous->work_id[0]&&
        (previous->transcription_page_count<=1||gallery_text_pages_complete(previous));
    if(guide_hint) {
        cJSON_AddStringToObject(request,"guideRevision",context.revision);
        cJSON_AddStringToObject(request,"guideWorkId",previous->work_id);
    }
    int status=0;char sha[65]={0};
    reply=gallery_call(transport,background,http,request,&status,NULL,0,NULL);
    record(diagnostic_error,status);
    if(context.purpose==2) {
        cJSON *count=cJSON_GetObjectItemCaseSensitive(reply,"works");
        const char *revision=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(reply,"revision"));
        const char *base=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(reply,"baseDate"));
        cJSON *version=cJSON_GetObjectItemCaseSensitive(reply,"syncVersion");
        if(status!=200||!cJSON_IsNumber(version)||version->valuedouble!=1||!base||strcmp(base,"2026-01-01")||
           !cJSON_IsNumber(count)||count->valuedouble!=count->valueint||count->valueint<1||count->valueint>4096||
           !revision||strlen(revision)!=64||strspn(revision,"0123456789abcdef")!=64||
           (context.revision[0]&&strcmp(context.revision,revision)))goto done;
        gallery_catalog value={.works=count->valueint};snprintf(value.revision,sizeof(value.revision),"%s",revision);
        if(!catalog_save(job.category,&value))goto done;
        xSemaphoreTake(device_mutex,portMAX_DELAY);catalogs[job.category]=value;catalog_loaded[job.category]=true;sync_cursor[job.category]=0;xSemaphoreGive(device_mutex);
        validated=true;goto done;
    }
    const char *error=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(reply,"error"));
    if(status==404&&error&&!strcmp(error,"gallery_pack_missing"))atomic_store(&pack_status[job.category],1);
    else if(status==503&&error&&!strcmp(error,"gallery_pack_incomplete"))atomic_store(&pack_status[job.category],2);
    else if(status==200)atomic_store(&pack_status[job.category],0);
    cJSON *size=cJSON_GetObjectItemCaseSensitive(reply,"size"),*hash=cJSON_GetObjectItemCaseSensitive(reply,"sha256"),*frames=cJSON_GetObjectItemCaseSensitive(reply,"frames");
    if(status!=200||!cJSON_IsNumber(size)||size->valuedouble!=size->valueint||size->valueint<16||size->valueint>1048576||
       !cJSON_IsString(hash)||strlen(hash->valuestring)!=64||!cJSON_IsNumber(frames)||frames->valueint<1||frames->valueint>64||job.frame>=frames->valueint)goto done;
    // An older bridge must not silently return a landscape frame for portrait.
    if(job.portrait){const char *mode=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(reply,"orientation"));if(!mode||strcmp(mode,"portrait"))goto done;}
    length=size->valueint;job.frames=frames->valueint;snprintf(sha,sizeof(sha),"%s",hash->valuestring);
    cJSON *guide_version=cJSON_GetObjectItemCaseSensitive(reply,"guideVersion");
    if(cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(reply,"guideUnchanged"))) {
        const char *id=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(reply,"workId"));
        if(!guide_hint||!cJSON_IsNumber(guide_version)||guide_version->valuedouble!=3||!id||strcmp(id,previous->work_id)||
           !cJSON_IsNull(cJSON_GetObjectItemCaseSensitive(reply,"guide")))goto done;
        guide=heap_caps_calloc(1,sizeof(*guide),MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);if(!guide)goto done;
        *guide=*previous;memset(guide->transcription_pages,0,sizeof(guide->transcription_pages));
        for(int page=0;page<guide->transcription_page_count&&guide->transcription_page_count>1;page++)
            if(!gallery_text_page_set(guide,page,previous->transcription_pages[page]))goto done;
    }
    else if(cJSON_IsNumber(guide_version)&&(guide_version->valuedouble==1||guide_version->valuedouble==2||guide_version->valuedouble==3))
        guide=guide_read(cJSON_GetObjectItemCaseSensitive(reply,"guide"),job.date);
    if(cJSON_IsObject(cJSON_GetObjectItemCaseSensitive(reply,"guide"))&&!guide)goto done;
    if(guide)snprintf(job.work_id,sizeof(job.work_id),"%s",guide->work_id);
    if(guide&&guide->transcription_page_count>1&&!gallery_text_pages_complete(guide)) {
        bool reuse=previous&&!strcmp(library_guide_revisions[job.category],context.revision)&&
            !strcmp(previous->date,guide->date)&&!strcmp(previous->work_id,guide->work_id)&&
            previous->transcription_page_count==guide->transcription_page_count&&
            !strcmp(previous->transcription_sha256,guide->transcription_sha256)&&gallery_text_pages_complete(previous);
        if(reuse) {
            for(int page=1;page<guide->transcription_page_count;page++)if(!gallery_text_page_set(guide,page,previous->transcription_pages[page]))goto done;
            if(!gallery_text_pages_complete(guide))goto done;
        } else {
        cJSON_ReplaceItemInObjectCaseSensitive(request,"op",cJSON_CreateString("guidePage"));
        cJSON_AddStringToObject(request,"workId",guide->work_id);
        cJSON_AddStringToObject(request,"sha256",guide->transcription_sha256);
        for(int page=1;page<guide->transcription_page_count;page++) {
            cJSON_DeleteItemFromObjectCaseSensitive(request,"page");cJSON_AddNumberToObject(request,"page",page);
            cJSON_Delete(reply);reply=gallery_call(transport,background,http,request,&status,NULL,0,NULL);
            const char *text=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(reply,"text"));
            const char *part_sha=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(reply,"pageSha256"));
            const char *full_sha=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(reply,"sha256"));
            const char *id=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(reply,"workId"));
            cJSON *at=cJSON_GetObjectItemCaseSensitive(reply,"page"),*count=cJSON_GetObjectItemCaseSensitive(reply,"pages");
            if(status!=200||!id||strcmp(id,guide->work_id)||!full_sha||strcmp(full_sha,guide->transcription_sha256)||
               !cJSON_IsNumber(at)||at->valuedouble!=page||!cJSON_IsNumber(count)||count->valuedouble!=guide->transcription_page_count||
               !gallery_text_hash(text,part_sha)||!gallery_text_page_set(guide,page,text))goto done;
        }
        if(!gallery_text_pages_complete(guide))goto done;
        cJSON_DeleteItemFromObjectCaseSensitive(request,"workId");cJSON_DeleteItemFromObjectCaseSensitive(request,"sha256");cJSON_DeleteItemFromObjectCaseSensitive(request,"page");
        }
    }
    record(diagnostic_stage,2);
    bool reused_stale=false;
    if(stale.data&&stale.size==(size_t)length) {
        uint8_t old_hash[32];char old_sha[65];mbedtls_sha256(stale.data,stale.size,old_hash,0);
        for(int i=0;i<32;i++)sprintf(old_sha+2*i,"%02x",old_hash[i]);
        if(!strcmp(old_sha,sha)){jpeg=stale.data;jpeg_capacity=stale.capacity;stale.data=NULL;reused_stale=true;}
    }
    if(!jpeg)jpeg=jpeg_alloc_decoder_mem(length,&input,&jpeg_capacity);
    if(!jpeg){gallery_cache_reclaim();jpeg=jpeg_alloc_decoder_mem(length,&input,&jpeg_capacity);}
    if(!jpeg)goto done;
    record(diagnostic_stage,3);
    if(library&&!reused_stale&&context.revision[0]) {
        resume_active=true;if(!sample)card_resume_load(root,&job,context.revision,sha,jpeg,length,&resume);offset=(int)resume.offset;
        writer=(gallery_card_writer){.key=job,.revision=context.revision,.sha=sha,.jpeg=jpeg,.total=length,.base=offset,.resume=&resume,.transport=transport,.background=background,.request_id=context.request_id,.initial=offset,.root=root};
        gallery_write_init(&writer.flow,resume.offset);
        atomic_init(&writer.writing,false);
        atomic_store(&transfer_received,0);atomic_store(&transfer_written,0);atomic_store(&transfer_write_ms,0);atomic_store(&transfer_overlap,0);
        writer.started=true;
        if(xTaskCreateWithCaps(card_writer_task,"tab5_card_save",8192,&writer,2,NULL,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT)!=pdPASS)writer.started=false;
    }
    if(reused_stale)offset=length;
    cJSON_ReplaceItemInObjectCaseSensitive(request,"op",cJSON_CreateString("read"));cJSON_AddStringToObject(request,"sha256",sha);
    receive_started=esp_timer_get_time();
    for(;offset<length;) {
        if(download_cancelled(transport,background))goto done;
        bool stream=library&&transport==LINK_WIFI&&rpc_gallery_stream_ready();
        int count=length-offset,limit=stream?length:rpc_data_limit(transport);if(count>limit)count=limit;
        cJSON_DeleteItemFromObjectCaseSensitive(request,"offset");cJSON_AddNumberToObject(request,"offset",offset);
        cJSON_DeleteItemFromObjectCaseSensitive(request,"count");cJSON_AddNumberToObject(request,"count",count);
        size_t received=0;cJSON_Delete(reply);
        if(stream) {
            writer.base=offset;char nonce[33];
            reply=rpc_gallery_stream_call(http,request,&status,nonce,jpeg+offset,count,&received,resume_active?card_writer_progress:NULL,&writer);
        }else reply=gallery_call(transport,background,http,request,&status,jpeg+offset,count,&received);
        record(diagnostic_error,status);
        cJSON *at=cJSON_GetObjectItemCaseSensitive(reply,"offset");
        if(status!=200||received!=(size_t)count||!cJSON_IsNumber(at)||at->valuedouble!=offset){if(stream&&received<=(size_t)count)offset+=(int)received;goto done;}
        offset+=count;
        if(resume_active&&!stream){writer.base=0;if(!card_writer_progress(&writer,offset))goto done;}
        taskYIELD();
    }
    receive_ended=esp_timer_get_time();
    card_writer_finish(&writer);
    if(atomic_load(&writer.flow.failed))goto done;
    uint8_t digest[32];char actual[65];mbedtls_sha256(jpeg,length,digest,0);
    record(diagnostic_stage,4);
    for(int i=0;i<32;i++)sprintf(actual+2*i,"%02x",digest[i]);
    if(strcmp(actual,sha)){if(resume_active)card_resume_clear(root,&job,context.revision,sha,length);resume_active=false;goto done;}
    jpeg_decode_picture_info_t downloaded_info={0};
    if(jpeg_decoder_get_info(jpeg,length,&downloaded_info)!=ESP_OK||downloaded_info.width!=1280||downloaded_info.height!=720){if(resume_active)card_resume_clear(root,&job,context.revision,sha,length);resume_active=false;goto done;}
    validated=true;
    if(!download_cancelled(transport,background)) {
        gallery_jpeg saved={.key=job,.data=jpeg,.size=length,.capacity=jpeg_capacity};
        size_t pending=resume_active?length-resume.offset:0;int64_t write_began=esp_timer_get_time();
        persisted=card_put(root,&saved,context.revision,guide,!library,resume_active?&resume:NULL,reused_stale);
        if(resume_active&&persisted){atomic_fetch_add(&transfer_written,pending);atomic_fetch_add(&transfer_write_ms,(esp_timer_get_time()-write_began)/1000);}
        if(persisted&&resume_active){card_resume_clear(root,&job,context.revision,sha,length);resume_active=false;}
    }
    if(background){
        jpeg_decode_picture_info_t info={0};
        if(jpeg_decoder_get_info(jpeg,length,&info)!=ESP_OK||info.width!=1280||info.height!=720){validated=false;goto done;}
        // Leave enough PSRAM for the largest measured rotated reader layer
        // (3.25 MB), output reuse and transport overhead. Never grow RGB cache.
        if(!library&&heap_caps_get_free_size(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT)<4u*1024u*1024u){validated=false;goto done;}
        if(guide&&!library){xSemaphoreTake(device_mutex,portMAX_DELAY);gallery_guide_free(guides[job.category]);guides[job.category]=guide;guide=NULL;xSemaphoreGive(device_mutex);}
        record(diagnostic_stage,10);goto done;
    }
decode:
    if(wifi_isolated){wifi_probe_release_gallery();tab5_wifi_wake();wifi_isolated=false;}
    decode_started=esp_timer_get_time();
    record(diagnostic_fetch_ms,cached.data&&!from_card?0:(decode_started-began)/1000);
    if(wifi_bulk){wifi_performance_bulk(false);wifi_bulk=false;}
    jpeg_decode_picture_info_t info={0};
    record(diagnostic_stage,5);
    if(jpeg_decoder_get_info(jpeg,length,&info)!=ESP_OK||info.width!=1280||info.height!=720||foreground_busy())goto done;
    // Reuse a detached, non-visible decoder buffer. Keeping two cached frames
    // and allocating a third could fail despite sufficient total free PSRAM.
    // Camera startup already reserved three full frames. When CSI is idle,
    // borrow its two non-photograph frames instead of competing for another
    // contiguous 1.8 MB allocation in the fragmented general heap.
    if(!pixels)pixels=camera_gallery_buffer_take();
    if(pixels)capacity=1280*720*2;
    else {
        atomic_fetch_add(&diagnostic_allocations,1);pixels=jpeg_alloc_decoder_mem(1280*720*2,&output,&capacity);
        if(!pixels){gallery_cache_reclaim();pixels=jpeg_alloc_decoder_mem(1280*720*2,&output,&capacity);}
    }
    record(diagnostic_stage,6);
    const jpeg_decode_engine_cfg_t engine={.timeout_ms=3000};
    const jpeg_decode_cfg_t format={.output_format=JPEG_DECODE_OUT_FORMAT_RGB565,.rgb_order=JPEG_DEC_RGB_ELEMENT_ORDER_BGR,.conv_std=JPEG_YUV_RGB_CONV_STD_BT601};
    uint32_t written=0;
    // The decoder allocator clears PSRAM through the CPU cache. Retire those
    // dirty lines before DMA owns the output, or later eviction can overwrite
    // decoded pixels (especially the tail). The driver invalidates after DMA.
    if(!pixels){record(diagnostic_error,ESP_ERR_NO_MEM);atomic_store(&decode_status[job.category*2+job.portrait],GALLERY_DECODE_NO_MEMORY);goto done;}
    record(diagnostic_stage,7);
    esp_err_t decode_error=esp_cache_msync(pixels,capacity,ESP_CACHE_MSYNC_FLAG_DIR_C2M|ESP_CACHE_MSYNC_FLAG_INVALIDATE);record(diagnostic_error,decode_error);if(decode_error!=ESP_OK){atomic_store(&decode_status[job.category*2+job.portrait],decode_error==ESP_ERR_NO_MEM?GALLERY_DECODE_NO_MEMORY:GALLERY_DECODE_FAILED);goto done;}
    record(diagnostic_stage,8);
    decode_error=jpeg_new_decoder_engine(&engine,&codec);record(diagnostic_error,decode_error);if(decode_error!=ESP_OK){atomic_store(&decode_status[job.category*2+job.portrait],decode_error==ESP_ERR_NO_MEM?GALLERY_DECODE_NO_MEMORY:GALLERY_DECODE_FAILED);goto done;}
    record(diagnostic_stage,9);
    decode_error=jpeg_decoder_process(codec,&format,jpeg,length,pixels,capacity,&written);record(diagnostic_error,decode_error);if(decode_error!=ESP_OK||written!=1280*720*2){atomic_store(&decode_status[job.category*2+job.portrait],decode_error==ESP_ERR_NO_MEM?GALLERY_DECODE_NO_MEMORY:GALLERY_DECODE_FAILED);goto done;}
    result=job;result.pixels=pixels;pixels=NULL;
    if(guide){xSemaphoreTake(device_mutex,portMAX_DELAY);gallery_guide_free(guides[job.category]);guides[job.category]=guide;guide=NULL;xSemaphoreGive(device_mutex);}
    record(diagnostic_stage,10);
done:
    card_writer_finish(&writer);
    if(resume_active&&jpeg&&atomic_load(&storage_enabled)) {
        while((size_t)offset>resume.offset) {
            size_t end=(size_t)offset;if(end-resume.offset>GALLERY_RESUME_BLOCK)end=resume.offset+GALLERY_RESUME_BLOCK;
            if(!card_resume_append(root,&job,context.revision,sha,jpeg,length,end,&resume))break;
        }
    }
    if(library&&!sample&&persisted&&guide&&(guide->transcription_page_count<=1||gallery_text_pages_complete(guide))) {
        gallery_guide_free(library_guides[job.category]);library_guides[job.category]=guide;guide=NULL;
        snprintf(library_guide_revisions[job.category],sizeof(library_guide_revisions[job.category]),"%s",context.revision);
    }
    if(wifi_isolated){wifi_probe_end();tab5_wifi_wake();}
    if(wifi_bulk)wifi_performance_bulk(false);
    record(diagnostic_decode_ms,decode_started?(esp_timer_get_time()-decode_started)/1000:0);
    record(diagnostic_total_ms,(esp_timer_get_time()-began)/1000);
    if(background)record(diagnostic_fetch_ms,(esp_timer_get_time()-began)/1000);
    if(codec)jpeg_del_decoder_engine(codec);
    if(http) {
        unsigned after=rpc_gallery_profile_get().calls;
        unsigned calls=after-http_calls_started;
        bool keep=library&&background&&validated&&persisted&&calls<=200&&
            http_lease.requests<=200-calls&&esp_http_client_is_complete_data_received(http)&&
            !download_cancelled(transport,background)&&gallery_http_valid(&http_lease,http_lease.endpoint,(uint64_t)(esp_timer_get_time()/1000));
        if(keep) {
            http_lease.requests+=calls;
            xSemaphoreTake(device_mutex,portMAX_DELAY);
            if(gallery_http_return_idle(&library_http_idle,&http_lease))http=NULL;
            xSemaphoreGive(device_mutex);
        }
        if(http)esp_http_client_cleanup(http);
    }
    cJSON_Delete(reply);cJSON_Delete(request);
    if(from_card&&!library) {
        xSemaphoreTake(device_mutex,portMAX_DELAY);
        if(strcmp(stored_date,job.date)){snprintf(stored_date,sizeof(stored_date),"%s",job.date);memset(stored_pages,0,sizeof(stored_pages));stored_frames[0]=stored_frames[1]=1;}
        stored_pages[job.category][job.portrait]|=UINT64_C(1)<<job.frame;stored_frames[job.category]=job.frames;
        xSemaphoreGive(device_mutex);
    }
    if(library&&context.purpose==1&&validated&&persisted&&!atomic_load(&cancel_prefetch)) {
        xSemaphoreTake(device_mutex,portMAX_DELAY);gallery_catalog value=catalogs[job.category];xSemaphoreGive(device_mutex);
        unsigned next=job.frame*2u+(job.portrait?1u:0u)+1;
        if(next>=job.frames*2u) {
            value.next++;
            if(catalog_save(job.category,&value)){xSemaphoreTake(device_mutex,portMAX_DELAY);catalogs[job.category]=value;sync_cursor[job.category]=0;xSemaphoreGive(device_mutex);}
            else {validated=false;persisted=false;}
        }else {xSemaphoreTake(device_mutex,portMAX_DELAY);sync_cursor[job.category]=next;xSemaphoreGive(device_mutex);}
    }
    if(library&&!sample) {
        xSemaphoreTake(device_mutex,portMAX_DELAY);catalog_check_after[job.category]=esp_timer_get_time()/1000+(context.purpose==2?60000:0);xSemaphoreGive(device_mutex);
        if(!persisted&&context.purpose==1)validated=false;
    }
    free(stale.data);
    gallery_guide_free(guide);
    if(!library&&(background?validated:result.pixels!=NULL)&&jpeg&&!foreground_busy()&&(!background||!atomic_load(&cancel_prefetch))) {
        gallery_jpeg value={.key=job,.data=jpeg,.size=length,.capacity=jpeg_capacity};
        xSemaphoreTake(device_mutex,portMAX_DELAY);gallery_jpeg_put(&jpeg_cache,value,generation);xSemaphoreGive(device_mutex);
    } else free(jpeg);
    gallery_recycle(pixels);
    if(background){if(!validated)prefetch_retry=esp_timer_get_time()/1000+5000;}
    else if(!result.pixels)atomic_fetch_or(&failed_slots,1u<<(job.category*2+job.portrait));
    if(sample) {
        xSemaphoreTake(device_mutex,portMAX_DELAY);
        speed_bytes+=atomic_load(&transfer_received);speed_total_ms=(esp_timer_get_time()-speed_started)/1000;
        speed_receive_ms+=receive_started?((receive_ended?receive_ended:esp_timer_get_time())-receive_started)/1000:0;
        speed_write_ms+=atomic_load(&transfer_write_ms);speed_overlap+=atomic_load(&transfer_overlap);
        speed_io=gallery_store_profile_get();speed_rpc=rpc_gallery_profile_get();
        if(atomic_load(&speed_phase)!=5) {
            if(validated&&persisted){speed_samples++;atomic_store(&speed_phase,speed_samples==8?3:1);}
            else {speed_error=atomic_load(&diagnostic_error);atomic_store(&speed_phase,4);}
        }
        xSemaphoreGive(device_mutex);
    }
    if(background)atomic_store(&prefetching,false);else atomic_store(&working,false);
#undef record
    vTaskDeleteWithCaps(NULL);
}
static bool start_request(int category,const char *date,int frame,bool portrait,uint8_t *spare,bool background,int purpose) {
    gallery_work *context=background?&prefetch_job:&foreground_job;
    context->transport=purpose==3?speed_link:purpose?rpc_library_link_select():rpc_read_link_select();context->background=background;
    context->purpose=purpose;
    if(!background)atomic_store(&decode_status[category*2+portrait],GALLERY_DECODE_OK);
    snprintf(context->revision,sizeof(context->revision),"%s",pack_revisions[category]);
    context->cached_only=!background&&atomic_load(&prefetching);
    if(!background&&!spare)spare=gallery_take_buffer();
    atomic_store(&diagnostic_frame,frame);atomic_store(&diagnostic_reused,spare!=NULL);
    context->image=(gallery_image){.pixels=spare,.category=category,.frame=frame,.portrait=portrait};snprintf(context->image.date,sizeof(context->image.date),"%s",date);
    context->request_id=atomic_fetch_add(&diagnostic_requests,1)+1;atomic_store(&diagnostic_category,category);atomic_store(&diagnostic_portrait,portrait);atomic_store(&diagnostic_stage,1);atomic_store(&diagnostic_error,0);
    atomic_store(&diagnostic_fetch_ms,0);atomic_store(&diagnostic_decode_ms,0);atomic_store(&diagnostic_total_ms,0);
    atomic_store(&transfer_received,0);atomic_store(&transfer_written,0);atomic_store(&transfer_write_ms,0);atomic_store(&transfer_overlap,0);
    if(background){atomic_store(&cancel_prefetch,false);atomic_store(&prefetching,true);}
    else atomic_store(&working,true);
    if(xTaskCreateWithCaps(worker,"tab5_gallery",8192,context,2,NULL,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT)!=pdPASS){
        gallery_recycle(spare);context->image.pixels=NULL;
        if(background)atomic_store(&prefetching,false);else atomic_store(&working,false);
        return false;
    }
    return true;
}
bool gallery_request(int category,const char *date,int frame,bool portrait,uint8_t *spare) {
    if(!gallery_request_available(category,date,frame,portrait)){gallery_recycle(spare);return false;}
    return start_request(category,date,frame,portrait,spare,false,0);
}
bool gallery_library_pending(void) {
    if(!atomic_load(&storage_enabled)||!atomic_load(&library_enabled)||!sd_storage_available())return false;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    bool pending=speed_active()||!catalogs[0].works||catalogs[0].next<catalogs[0].works||!catalogs[1].works||catalogs[1].next<catalogs[1].works;
    xSemaphoreGive(device_mutex);return pending;
}
void gallery_sync_library(void) {
    int link=rpc_library_link_select();
    if(speed_active()) {
        if(atomic_load(&speed_phase)==2)return;
        if(esp_timer_get_time()>speed_deadline){speed_error=-1;atomic_store(&speed_phase,4);return;}
        if(!gallery_request_ready())return;
        speed_error=!atomic_load(&storage_enabled)||!sd_storage_available()?-2:
            (link!=LINK_USB&&link!=LINK_WIFI)?-3:heap_caps_get_free_size(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT)<5u*1024u*1024u?-4:0;
        if(speed_error){atomic_store(&speed_phase,4);return;}
        if(speed_link==LINK_NONE)speed_link=link;
        if(link!=speed_link){atomic_store(&speed_phase,4);return;}
        int category=speed_samples%2;unsigned index=speed_samples/4;
        gallery_catalog catalog; xSemaphoreTake(device_mutex,portMAX_DELAY);catalog=catalogs[category];xSemaphoreGive(device_mutex);
        if(!catalog.works||!pack_revisions[category][0]||!sd_storage_lock()){atomic_store(&speed_phase,4);return;}
        bool ready=gallery_speed_prepare(SD_STORAGE_MOUNT);sd_storage_unlock(gallery_store_media_error());
        char date[11];if(!ready||!gallery_catalog_date(index%catalog.works,date)){atomic_store(&speed_phase,4);return;}
        if(!speed_started){gallery_store_profile_reset();rpc_gallery_profile_reset();speed_started=esp_timer_get_time();}
        atomic_store(&speed_phase,2);
        if(!start_request(category,date,0,(speed_samples/2)%2,NULL,true,3))atomic_store(&speed_phase,4);
        return;
    }
    if(!atomic_load(&storage_enabled)||!atomic_load(&library_enabled)||(link!=LINK_USB&&link!=LINK_WIFI)||!gallery_request_ready()||foreground_busy()||!sd_storage_available()||
       esp_timer_get_time()/1000<prefetch_retry||heap_caps_get_free_size(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT)<5u*1024u*1024u)return;
    for(unsigned step=0;step<2;step++) {
        int category=(sync_category+step)%2;char date[11];
        xSemaphoreTake(device_mutex,portMAX_DELAY);gallery_catalog value=catalogs[category];unsigned cursor=sync_cursor[category];int64_t after=catalog_check_after[category];xSemaphoreGive(device_mutex);
        bool probe=!value.works||value.next==value.works;
        if(probe&&esp_timer_get_time()/1000<after)continue;
        if(!gallery_catalog_date(value.next<value.works?value.next:0,date))continue;
        if(start_request(category,date,probe?0:cursor/2,probe?false:cursor%2,NULL,true,probe?2:1)){sync_category=category+1;return;}
    }
}
void gallery_prefetch_day(const char *date) {
    if(speed_active()||!date||strlen(date)!=10||!gallery_request_ready()||foreground_busy()||rpc_read_link_select()==LINK_NONE||
       esp_timer_get_time()/1000<prefetch_retry||heap_caps_get_free_size(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT)<5u*1024u*1024u)return;
    // Discover page counts using page zero, then fill both layouts of every
    // page in both categories. Round-robin failures cannot starve the other work.
    gallery_image missing={0};bool found=false;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    if(jpeg_cache.date[0]&&strcmp(date,jpeg_cache.date)<0){xSemaphoreGive(device_mutex);return;}
    int frames[2]={1,1};
    if(atomic_load(&storage_enabled)&&!strcmp(stored_date,date)){frames[0]=stored_frames[0];frames[1]=stored_frames[1];}
    for(int i=0;i<GALLERY_JPEG_CACHE_SLOTS;i++)if(jpeg_cache.entries[i].data&&!strcmp(jpeg_cache.entries[i].key.date,date))
        frames[jpeg_cache.entries[i].key.category]=jpeg_cache.entries[i].key.frames;
    unsigned total=2u*(frames[0]+frames[1]);
    for(unsigned step=0;step<total;step++){
        unsigned index=(prefetch_cursor+step)%total;
        int category=index>=2u*frames[0],relative=index-(category?2*frames[0]:0);
        gallery_image key={.category=category,.frame=relative/2,.portrait=relative%2};snprintf(key.date,sizeof(key.date),"%s",date);
        bool stored=atomic_load(&storage_enabled)&&sd_storage_available()&&!strcmp(stored_date,date)&&(stored_pages[category][key.portrait]&(UINT64_C(1)<<key.frame));
        if(!stored&&!gallery_jpeg_has(&jpeg_cache,&key)){missing=key;prefetch_cursor=index+1;found=true;break;}
    }
    xSemaphoreGive(device_mutex);
    if(found)start_request(missing.category,date,missing.frame,missing.portrait,NULL,true,0);
}
bool gallery_take(gallery_image *out) {
    if(atomic_load(&working)||!result.pixels)return false;
    *out=result;result.pixels=NULL;return true;
}
