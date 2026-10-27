#include "ota_update.h"
#include "flash_benchmark.h"
#include "device.h"
#include "freertos/queue.h"
#include "freertos/idf_additions.h"
#include "rpc_link.h"
#include "wifi_performance.h"
#include "crash_record.h"
#include "esp_http_client.h"
#include "esp_ota_ops.h"
#include "esp_app_desc.h"
#include "esp_partition.h"
#include "esp_system.h"
#include "esp_heap_caps.h"
#include "esp_attr.h"
#include "gallery_remote.h"
#include "esp_timer.h"
#include "bsp/m5stack_tab5.h"
#include "mbedtls/sha256.h"
#include <stdatomic.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <strings.h>
#include "miniz.h"
#include "ota_stream.h"
#include "ota_resume.h"

typedef struct { char sha[65], version[32], offer_id[33], notes[1025]; int size; } ota_offer;
// The single OTA worker always has internal staging, even below the dynamic
// allocation reserve. Never pass PSRAM to SPI1's 32-byte copy/write fallback.
// Static DRAM avoids taking more heap or growing the flash worker's stack.
DRAM_ATTR static uint8_t flash_fallback[4096];
static atomic_bool busy;
static atomic_bool offer_withdrawn;
static char active_sha[65];
static double pending_offer_epoch,active_offer_epoch;
static atomic_bool completed, completion_presented;
static atomic_bool preparation_presented, preparing;
static atomic_bool verifying;
static atomic_uint prepare_started_ms, prepare_ms, buffer_slots;
static atomic_uint verify_started_ms;
static atomic_int download_bytes, written_bytes, image_bytes;
static atomic_int transfer_bytes;
static atomic_uint lcd_underruns_before;
static atomic_int progress_percent;
static ota_offer pending_offer;
static char last_offer[33], message[96]="未开始";
static char target_version[32];
bool ota_update_busy(void){return atomic_load(&busy);}
bool ota_update_completed(void){return atomic_load(&completed);}
bool ota_update_verifying(void){return atomic_load(&verifying)&&!ota_update_completed();}
int ota_update_progress(void){return atomic_load(&progress_percent);}
void ota_update_completion_presented(void) {
    if(atomic_load(&completed))atomic_store(&completion_presented,true);
}
void ota_update_preparation_presented(void) {
    if(ota_update_busy()&&!ota_update_completed())atomic_store(&preparation_presented,true);
}
void ota_update_diagnostics(char *out,size_t size) {
    if(!out||!size)return;
    extern volatile unsigned tab5_lcd_underruns;
    unsigned elapsed=atomic_load(&prepare_ms);
    if(atomic_load(&preparing))elapsed=(unsigned)(esp_timer_get_time()/1000)-atomic_load(&prepare_started_ms);
    snprintf(out,size,"%u,%u,%u,%d",elapsed,atomic_load(&buffer_slots),
        tab5_lcd_underruns-atomic_load(&lcd_underruns_before),ota_update_busy());
}
bool ota_update_available(void) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);bool available=pending_offer.size>0;xSemaphoreGive(device_mutex);
    return available&&!ota_update_busy();
}

bool ota_update_offer_matches(const char *offer_id,const char *sha) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    bool match=offer_id&&sha&&pending_offer.size>0&&!strcmp(pending_offer.offer_id,offer_id)&&!strcmp(pending_offer.sha,sha);
    xSemaphoreGive(device_mutex);return match;
}

static void report(const char *value) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    snprintf(message,sizeof(message),"%s",value);
    xSemaphoreGive(device_mutex);
}
void ota_update_status(char *out,size_t size) {
    if(!out||!size)return;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    if(ota_update_verifying()) {
        unsigned elapsed=(unsigned)(esp_timer_get_time()/1000)-atomic_load(&verify_started_ms);
        snprintf(out,size,"正在校验固件 · %u.%u 秒",elapsed/1000,elapsed/100%10);
    } else if(atomic_load(&preparing))snprintf(out,size,"正在准备备用分区 · 已接收镜像 %d KB · %u 秒",
        (atomic_load(&download_bytes)+1023)/1024,
        ((unsigned)(esp_timer_get_time()/1000)-atomic_load(&prepare_started_ms))/1000);
    else if(ota_update_busy()&&!ota_update_completed()&&atomic_load(&image_bytes)>0&&
            atomic_load(&download_bytes)==atomic_load(&image_bytes))
        snprintf(out,size,"下载完成 · 正在写入 %d / %d KB",(atomic_load(&written_bytes)+1023)/1024,
            (atomic_load(&image_bytes)+1023)/1024);
    else snprintf(out,size,"%s",message);
    int transferred=atomic_load(&transfer_bytes),decoded=atomic_load(&download_bytes);
    if(ota_update_busy()&&transferred>0&&transferred<decoded) {
        size_t used=strlen(out);
        snprintf(out+used,size-used," · 传输 %d KB",(transferred+1023)/1024);
    }
    xSemaphoreGive(device_mutex);
}
void ota_update_target_version(char *out,size_t size) {
    if(!out||!size)return;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    snprintf(out,size,"%s",target_version);
    xSemaphoreGive(device_mutex);
}
void ota_update_offer_details(char *version,size_t version_size,char *notes,size_t notes_size) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    if(version&&version_size)snprintf(version,version_size,"%s",pending_offer.size?pending_offer.version:"");
    if(notes&&notes_size)snprintf(notes,notes_size,"%s",pending_offer.size?pending_offer.notes:"");
    xSemaphoreGive(device_mutex);
}
static bool valid_hex(const char *value,size_t length) {
    if(!value||strlen(value)!=length)return false;
    for(size_t i=0;i<length;i++)if(!((value[i]>='0'&&value[i]<='9')||(value[i]>='a'&&value[i]<='f')))return false;
    return true;
}
static bool same_running_image(const esp_partition_t *running,const ota_offer *offer) {
    // Different embedded versions cannot have identical image hashes.
    if(strcmp(esp_app_get_description()->version,offer->version))return false;
    if(offer->size>(int)running->size)return false;
    mbedtls_sha256_context sha;mbedtls_sha256_init(&sha);mbedtls_sha256_starts(&sha,0);
    uint8_t chunk[1024];int at=0;
    while(at<offer->size) {
        int n=offer->size-at;if(n>(int)sizeof(chunk))n=sizeof(chunk);
        if(esp_partition_read(running,at,chunk,n)!=ESP_OK){mbedtls_sha256_free(&sha);return false;}
        mbedtls_sha256_update(&sha,chunk,n);at+=n;
        if(at%65536==0)vTaskDelay(1);
    }
    uint8_t digest[32];mbedtls_sha256_finish(&sha,digest);mbedtls_sha256_free(&sha);
    char hex[65];for(int i=0;i<32;i++)sprintf(hex+i*2,"%02x",digest[i]);
    return !strcmp(hex,offer->sha);
}
// Match one authenticated bulk range instead of splitting every 64 KiB block
// into a 48 KiB range and a second short RPC. Older peers still supply smaller
// ranges until the same owned block is filled.
enum { OTA_BLOCK_BYTES=49152, OTA_BUFFER_MAX=(8*1024*1024+OTA_BLOCK_BYTES-1)/OTA_BLOCK_BYTES };
// A bounded PSRAM pool absorbs reception while the SDK prepares the partition.
// The producer alone touches the transport. A block is never recycled until
// esp_ota_write returns, and all exits join the producer before closing I/O.
typedef struct {int slot,count,offset;} ota_block;
typedef struct {
    uint8_t *data[OTA_BUFFER_MAX];int slots;QueueHandle_t free_blocks,ready_blocks;SemaphoreHandle_t done;
    atomic_bool stop;bool started;
    esp_http_client_handle_t client;link_kind transport;const ota_offer *offer;
    int encoding;int64_t wire_remaining;
    unsigned host_epoch;int mode;
    bool http_lost,http_ranges,http_resume_supported;
    esp_http_client_handle_t range_client;
    ota_resume recovery;
    int64_t read_us,free_wait_us;
} ota_reader;
static esp_err_t ota_http_event(esp_http_client_event_t *event) {
    ota_reader *reader=event->user_data;
    if(event->event_id==HTTP_EVENT_ON_HEADER&&!strcasecmp(event->header_key,"X-AIBot-OTA-Encoding"))
        reader->encoding=reader->encoding==0&&!strcmp(event->header_value,"aibot-zlib-blocks-v1")?1:-1;
    return ESP_OK;
}
static int ota_http_read(void *context,uint8_t *out,int count) {
    ota_reader *reader=context;
    if(atomic_load(&reader->stop)||atomic_load(&offer_withdrawn))return -1;
    int n=esp_http_client_read(reader->client,(char*)out,count);
    if(n>0&&n<=count)atomic_fetch_add(&transfer_bytes,n);
    if(n<=0)reader->http_lost=true;
    return n;
}
static bool ota_queue(QueueHandle_t queue,void *value,bool send,ota_reader *reader) {
    while(!atomic_load(&reader->stop)) {
        if((send?xQueueSend(queue,value,pdMS_TO_TICKS(20)):xQueueReceive(queue,value,pdMS_TO_TICKS(20)))==pdTRUE)return true;
    }
    return false;
}
static bool ota_reader_owned(ota_reader *r) {
    return !atomic_load(&r->stop)&&!atomic_load(&offer_withdrawn)&&
        r->host_epoch==device_host_epoch()&&!device_host_changing()&&
        device_get_config().paired&&device_get_config().mode==r->mode;
}
static bool ota_reader_retry(ota_reader *r,int result,int offset) {
    unsigned delay;
    if(!ota_reader_owned(r)||!ota_resume_retry(&r->recovery,result,esp_timer_get_time()/1000,&delay))return false;
    char status[96];snprintf(status,sizeof(status),"连接中断，等待重连 · 已接收 %d KB · 重试 %u",
        (offset+1023)/1024,r->recovery.retries);report(status);
    printf("TAB5_OTA_RESUME link=%d offset=%d retry=%u error=%d\n",
        r->transport,offset,r->recovery.retries,result);
    for(unsigned elapsed=0;elapsed<delay;elapsed+=50) {
        if(!ota_reader_owned(r))return false;
        vTaskDelay(pdMS_TO_TICKS(50));
    }
    while(ota_reader_owned(r)&&!rpc_ota_link_connected(r->transport)) {
        if(!ota_resume_wait_budget(&r->recovery,esp_timer_get_time()/1000))return false;
        vTaskDelay(pdMS_TO_TICKS(100));
    }
    return ota_reader_owned(r)&&ota_resume_wait_budget(&r->recovery,esp_timer_get_time()/1000)&&
        rpc_ota_link_connected(r->transport);
}
static int ota_reader_range(ota_reader *r,int offset,uint8_t *out,int count) {
    if(r->transport!=LINK_WIFI)return rpc_ota_read(r->transport,r->offer->sha,r->offer->offer_id,offset,out,count);
    if(!r->range_client) {
        device_config cfg=device_get_config();char url[160];
        snprintf(url,sizeof(url),"http://%s:%d/tab5/v1/rpc",cfg.host,cfg.port);
        esp_http_client_config_t options={.url=url,.timeout_ms=10000,.disable_auto_redirect=true,.buffer_size=4096};
        r->range_client=esp_http_client_init(&options);
        if(!r->range_client)return -1;
    }
    int got=rpc_ota_http_read(r->range_client,r->offer->sha,r->offer->offer_id,offset,out,count);
    esp_http_client_close(r->range_client);return got;
}
static void ota_receive(void *arg) {
    ota_reader *reader=arg;int offset=0;
    while(offset<reader->offer->size&&!atomic_load(&reader->stop)) {
        int64_t free_started=esp_timer_get_time();
        int slot;bool have_slot=ota_queue(reader->free_blocks,&slot,false,reader);
        reader->free_wait_us+=esp_timer_get_time()-free_started;
        if(!have_slot)break;
        int want=reader->offer->size-offset;if(want>OTA_BLOCK_BYTES)want=OTA_BLOCK_BYTES;
        int filled=0,failure=-1;int64_t began=esp_timer_get_time();
        while(filled<want&&!atomic_load(&reader->stop)) {
            if(!ota_reader_owned(reader)){failure=-409;break;}
            bool stream=reader->transport==LINK_WIFI&&!reader->http_ranges;
            int got=stream?(reader->encoding==1?
                ota_stream_block(ota_http_read,reader,reader->data[slot],want,offset,&reader->wire_remaining):
                ota_http_read(reader,reader->data[slot]+filled,want-filled)):
                ota_reader_range(reader,offset+filled,reader->data[slot]+filled,want-filled);
            if(reader->transport!=LINK_WIFI)atomic_store(&transfer_bytes,(int)rpc_ota_received_bytes(reader->transport));
            if(stream&&reader->encoding==1&&offset+want==reader->offer->size&&reader->wire_remaining!=0)got=-1;
            if(!ota_reader_owned(reader)){failure=-409;break;}
            if(got<=0||got>want-filled) {
                int result=got<0?got:-1;
                if(stream&&!(reader->http_lost&&reader->http_resume_supported))result=-1;
                if(stream&&reader->http_lost&&reader->http_resume_supported) {
                    // A partial compressed block is not a checkpoint. Discard
                    // only this slot's incomplete data and re-read that block.
                    esp_http_client_close(reader->client);
                    reader->http_ranges=true;filled=0;result=-2;
                }
                if(ota_reader_retry(reader,result,offset+filled))continue;
                failure=result;break;
            }
            filled+=got;ota_resume_progress(&reader->recovery);
            atomic_store(&download_bytes,offset+filled);
            char progress[96];snprintf(progress,sizeof(progress),"已接收镜像 %d / %d KB",(offset+filled+1023)/1024,(reader->offer->size+1023)/1024);
            report(progress);
        }
        reader->read_us+=esp_timer_get_time()-began;
        ota_block block={slot,filled==want?filled:failure,offset};
        if(!ota_queue(reader->ready_blocks,&block,true,reader)||filled!=want)break;
        offset+=filled;vTaskDelay(pdMS_TO_TICKS(1));
    }
    xSemaphoreGive(reader->done);vTaskDeleteWithCaps(NULL);
}
static ota_reader *ota_reader_create(int image_size) {
    ota_reader *r=calloc(1,sizeof(*r));if(!r)return NULL;
    int desired=(image_size+OTA_BLOCK_BYTES-1)/OTA_BLOCK_BYTES;
    if(desired<2)desired=2;
    if(desired>OTA_BUFFER_MAX)desired=OTA_BUFFER_MAX;
    for(int i=0;i<desired;i++) {
        // Keep 4 MiB for UI/transport, but retain the original two-block fallback.
        if(i>=2&&heap_caps_get_free_size(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT)<OTA_BLOCK_BYTES+4*1024*1024)break;
        r->data[i]=heap_caps_malloc(OTA_BLOCK_BYTES,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
        if(!r->data[i])break;
        r->slots++;
    }
    r->free_blocks=xQueueCreate(r->slots?r->slots:2,sizeof(int));
    r->ready_blocks=xQueueCreate(r->slots?r->slots:2,sizeof(ota_block));r->done=xSemaphoreCreateBinary();
    if(r->slots<2||!r->free_blocks||!r->ready_blocks||!r->done) {
        for(int i=0;i<r->slots;i++)free(r->data[i]);
        if(r->free_blocks)vQueueDelete(r->free_blocks);
        if(r->ready_blocks)vQueueDelete(r->ready_blocks);
        if(r->done)vSemaphoreDelete(r->done);
        free(r);return NULL;
    }
    atomic_store(&buffer_slots,r->slots);
    for(int i=0;i<r->slots;i++)xQueueSend(r->free_blocks,&i,0);
    return r;
}
static void ota_reader_join(ota_reader *r) {
    if(!r||!r->started)return;
    atomic_store(&r->stop,true);xSemaphoreTake(r->done,portMAX_DELAY);r->started=false;
}
static void ota_reader_free(ota_reader *r) {
    if(!r)return;
    ota_reader_join(r);
    if(r->range_client){esp_http_client_close(r->range_client);esp_http_client_cleanup(r->range_client);}
    for(int i=0;i<r->slots;i++)free(r->data[i]);
    vQueueDelete(r->free_blocks);vQueueDelete(r->ready_blocks);vSemaphoreDelete(r->done);free(r);
}
static void install(void *arg) {
    ota_offer *offer=arg;
    int64_t install_started=esp_timer_get_time(),verify_started=0;
    ota_phase_timing phases={.transport=LINK_NONE};
    ota_trace_begin(offer->size);
    device_config cfg=device_get_config();
    const esp_partition_t *next=esp_ota_get_next_update_partition(NULL);
    const esp_partition_t *running=esp_ota_get_running_partition();
    esp_ota_handle_t handle=0;
    esp_http_client_handle_t client=NULL;
    bool opened=false,writing=false,already_current=false;
    link_kind transport=rpc_ota_link_select();
    phases.transport=transport;
    bool wifi_bulk=transport==LINK_WIFI;
    if(wifi_bulk)wifi_performance_bulk(true);
    int received=0;esp_err_t error=ESP_OK;
    uint8_t *chunk=NULL,*flash_buffer=NULL;size_t flash_capacity=0;ota_reader *reader=NULL;
    // Known image size lets the SDK erase aligned flash blocks independently
    // of the internal staging capacity. The final partial block stays bounded.
    int64_t transfer_started=0,read_us=0,write_us=0,prepare_us=0,ready_wait_us=0;
    char failure[96]="固件更新失败，请检查连接后重新发起";
    if(transport==LINK_NONE) {
        snprintf(failure,sizeof(failure),"所选连接尚未就绪，请连接电脑");goto finish;
    }
    if(!cfg.paired||transport==LINK_NONE||(transport==LINK_WIFI&&(!cfg.host[0]||cfg.port<=0))||!next||!running||
        next->address==running->address||offer->size>(int)next->size) {
        snprintf(failure,sizeof(failure),"当前分区或配对状态不支持升级");goto finish;
    }
    if(same_running_image(running,offer)){already_current=true;report("当前已是这份固件");goto finish;}
    // Reception stays in PSRAM; flash writes always use internal staging.
    reader=ota_reader_create(offer->size);
    if(!reader){snprintf(failure,sizeof(failure),"升级缓冲不足，请稍后重试");goto finish;}
    reader->host_epoch=device_host_epoch();reader->mode=cfg.mode;
    reader->recovery.started_ms=esp_timer_get_time()/1000;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    cJSON *resume_cap=cJSON_GetObjectItemCaseSensitive(device_snapshots[LINK_WIFI],"rpcOtaResume");
    reader->http_resume_supported=cJSON_IsNumber(resume_cap)&&resume_cap->valuedouble==1;
    xSemaphoreGive(device_mutex);
    // IDF 5.4's SPI1 driver copies external RAM input through a 32-byte stack
    // buffer, acquiring/releasing the flash bus for every piece. An internal
    // staging block uses the driver's normal 8 KiB writes instead. Fragmented
    // internal RAM can reject 48 KiB while still accommodating 16/8 KiB.
    // Retain at least 96 KiB for SDIO/USB; the SDK still owns erase and verify.
    const size_t flash_sizes[]={OTA_BLOCK_BYTES,16384,8192};
    for(unsigned i=0;i<sizeof(flash_sizes)/sizeof(flash_sizes[0]);i++) {
        size_t n=flash_sizes[i];
        if(heap_caps_get_free_size(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT)<n+96*1024)continue;
        flash_buffer=heap_caps_malloc(n,MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT);
        if(flash_buffer){flash_capacity=n;break;}
    }
    if(!flash_buffer){flash_buffer=flash_fallback;flash_capacity=sizeof(flash_fallback);}
    printf("TAB5_OTA_FLASH_BUFFER bytes=%u internalFree=%u\n",(unsigned)flash_capacity,
        (unsigned)heap_caps_get_free_size(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT));
    if(transport==LINK_WIFI) {
    char nonce[33],proof[65],proof_input[155],url[190];device_nonce(nonce);
    snprintf(proof_input,sizeof(proof_input),"GET|/tab5/v1/ota/%s|%s|%s",offer->sha,cfg.id,nonce);
    device_proof(proof_input,proof);
    snprintf(url,sizeof(url),"http://%s:%d/tab5/v1/ota/%s",cfg.host,cfg.port,offer->sha);
    esp_http_client_config_t options={.url=url,.timeout_ms=10000,.disable_auto_redirect=true,.buffer_size=4096,.event_handler=ota_http_event,.user_data=reader};
    ota_trace_update(OTA_TRACE_CONNECT,0,0);
    client=esp_http_client_init(&options);if(!client)goto finish;
    esp_http_client_set_header(client,"X-AIBot-Device",cfg.id);
    esp_http_client_set_header(client,"X-AIBot-Nonce",nonce);
    esp_http_client_set_header(client,"X-AIBot-Proof",proof);
    esp_http_client_set_header(client,"X-AIBot-OTA-Flow","tcp-v2");
    esp_http_client_set_header(client,"X-AIBot-OTA-Encoding","aibot-zlib-blocks-v1");
    if(esp_http_client_open(client,0)!=ESP_OK) {
        if(!reader->http_resume_supported)goto finish;
        esp_http_client_close(client);reader->http_ranges=true;
    }else {
    opened=true;
    int64_t length=esp_http_client_fetch_headers(client);
    int64_t wire_max=offer->size+(int64_t)((offer->size+49151)/49152)*16;
    int code=esp_http_client_get_status_code(client);
    if(reader->http_resume_supported&&(length<0||code==0||code==408||code==429||code>=500)) {
        esp_http_client_close(client);reader->http_ranges=true;opened=false;
    }else if(code!=200||reader->encoding<0||
        (reader->encoding==0?length!=offer->size:length<17||length>wire_max)) {
        snprintf(failure,sizeof(failure),"电脑未提供匹配的固件，请重新选择");goto finish;
    }
    reader->wire_remaining=length;
    }
    }
    // Authenticate/read a complete first block before touching the backup.
    // This also establishes the bridge's OTA lease before any long erase.
    // PSRAM XIP keeps reception and the rendered screen alive during flash I/O.
    transfer_started=esp_timer_get_time();
    report("等待接收固件 · 0 KB");
    reader->client=client;reader->transport=transport;reader->offer=offer;
    rpc_ota_reset_bytes(transport);
    // Reception does not write flash; keep its stack out of scarce internal RAM.
    if(xTaskCreateWithCaps(ota_receive,"tab5_ota_rx",8192,reader,4,NULL,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT)!=pdPASS) {
        error=ESP_ERR_NO_MEM;
        printf("TAB5_OTA_RX_START_FAILED internalLargest=%u psramLargest=%u\n",
            (unsigned)heap_caps_get_largest_free_block(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT),
            (unsigned)heap_caps_get_largest_free_block(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT));
        snprintf(failure,sizeof(failure),"无法启动固件接收，请重试");goto finish;
    }
    reader->started=true;
    ota_block block;
    ota_trace_update(OTA_TRACE_READ,0,0);
    int64_t ready_started=esp_timer_get_time();
    bool have_block=ota_queue(reader->ready_blocks,&block,false,reader);
    ready_wait_us+=esp_timer_get_time()-ready_started;
    if(!have_block||block.count<=0||block.offset!=0) {
        error=ESP_FAIL;snprintf(failure,sizeof(failure),"固件下载中断，原版本保持可用");goto finish;
    }
    // The UI starts this worker before it shows the overlay. Wait for a real
    // refresh and one panel scan before starting any flash operation.
    for(unsigned waited=0;waited<3000&&!atomic_load(&preparation_presented);waited+=50)
        vTaskDelay(pdMS_TO_TICKS(50));
    if(!atomic_load(&preparation_presented)) {
        error=ESP_FAIL;snprintf(failure,sizeof(failure),"升级画面未就绪，请重试");goto finish;
    }
    vTaskDelay(pdMS_TO_TICKS(100));
    report("正在准备备用固件分区");ota_trace_update(OTA_TRACE_BEGIN,0,0);
    int64_t erase_started=esp_timer_get_time();
    atomic_store(&prepare_started_ms,(unsigned)(erase_started/1000));atomic_store(&preparing,true);
    error=esp_ota_begin(next,offer->size,&handle);
    prepare_us=write_us=esp_timer_get_time()-erase_started;
    phases.prepare_ms=(unsigned)(write_us/1000);
    atomic_store(&prepare_ms,(unsigned)(write_us/1000));atomic_store(&preparing,false);
    if(error!=ESP_OK) {
        if(handle)esp_ota_abort(handle);
        snprintf(failure,sizeof(failure),"无法写入备用固件分区");goto finish;
    }
    writing=true;
    char reception[96];snprintf(reception,sizeof(reception),"已接收镜像 %d / %d KB",
        (atomic_load(&download_bytes)+1023)/1024,(offer->size+1023)/1024);report(reception);
    mbedtls_sha256_context sha;mbedtls_sha256_init(&sha);mbedtls_sha256_starts(&sha,0);
    while(received<offer->size) {
        ota_trace_update(OTA_TRACE_READ,received,0);
        if(received) {
            ready_started=esp_timer_get_time();have_block=ota_queue(reader->ready_blocks,&block,false,reader);
            ready_wait_us+=esp_timer_get_time()-ready_started;
            if(!have_block){error=ESP_FAIL;break;}
        }
        if(!ota_reader_owned(reader)){error=ESP_FAIL;break;}
        if(block.count<=0||block.offset!=received){error=block.count<0?block.count:ESP_FAIL;break;}
        chunk=reader->data[block.slot];int filled=block.count;
        mbedtls_sha256_update(&sha,chunk,filled);
        ota_trace_update(OTA_TRACE_WRITE,received,0);
        int64_t began=esp_timer_get_time();
        for(int at=0;at<filled;) {
            size_t n=filled-at;if(n>flash_capacity)n=flash_capacity;
            memcpy(flash_buffer,chunk+at,n);error=esp_ota_write(handle,flash_buffer,n);
            if(error!=ESP_OK)break;
            at+=n;
        }
        write_us+=esp_timer_get_time()-began;
        if(error!=ESP_OK)break;
        received+=filled;atomic_store(&written_bytes,received);
        // Reception can finish seconds before flash writes. Advance only after
        // successful writes, retaining 100% for verified boot-partition selection.
        atomic_store(&progress_percent,(int)((int64_t)received*99/offer->size));
        xQueueSend(reader->free_blocks,&block.slot,0);
        vTaskDelay(pdMS_TO_TICKS(1));
    }
    ota_reader_join(reader);read_us=reader->read_us;
    phases.write_ms=(unsigned)((write_us-prepare_us)/1000);
    phases.ready_wait_ms=(unsigned)(ready_wait_us/1000);
    phases.free_wait_ms=(unsigned)(reader->free_wait_us/1000);
    ota_trace_timing((unsigned)((esp_timer_get_time()-transfer_started)/1000),(unsigned)(read_us/1000),(unsigned)(write_us/1000));
    ota_trace_update(OTA_TRACE_VERIFY,received,error);
    printf("TAB5_OTA_TIMING bytes=%d elapsedMs=%lld readMs=%lld writeMs=%lld\n",received,
        (long long)((esp_timer_get_time()-transfer_started)/1000),(long long)(read_us/1000),(long long)(write_us/1000));
    if(received==offer->size) {
        atomic_store(&verify_started_ms,(unsigned)(esp_timer_get_time()/1000));
        atomic_store(&verifying,true);
        report("正在校验固件");
    }
    uint8_t digest[32];mbedtls_sha256_finish(&sha,digest);mbedtls_sha256_free(&sha);
    if(received!=offer->size||atomic_load(&offer_withdrawn)||reader->host_epoch!=device_host_epoch()||device_host_changing()||device_get_config().mode!=reader->mode){snprintf(failure,sizeof(failure),"固件下载中断，原版本保持可用");goto finish;}
    char actual[65];for(int i=0;i<32;i++)sprintf(actual+i*2,"%02x",digest[i]);
    if(strcmp(actual,offer->sha)){snprintf(failure,sizeof(failure),"固件校验失败，原版本保持可用");goto finish;}
    verify_started=esp_timer_get_time();
    ota_trace_update(OTA_TRACE_IMAGE,received,0);
    error=esp_ota_end(handle);
    if(error!=ESP_OK){writing=false;snprintf(failure,sizeof(failure),"固件格式校验失败，原版本保持可用");goto finish;}
    writing=false;
    esp_app_desc_t description;
    error=esp_ota_get_partition_description(next,&description);
    if(error!=ESP_OK||strcmp(description.project_name,"aibot_tab5")||strcmp(description.version,offer->version)) {
        snprintf(failure,sizeof(failure),"固件身份或启动检查失败，原版本保持可用");goto finish;
    }
    ota_trace_update(OTA_TRACE_BOOT,received,0);
    error=esp_ota_set_boot_partition(next);
    if(error!=ESP_OK){snprintf(failure,sizeof(failure),"启动分区设置失败，原版本保持可用");goto finish;}
    ota_trace_update(OTA_TRACE_DONE,received,0);
    phases.verify_ms=(unsigned)((esp_timer_get_time()-verify_started)/1000);
    phases.install_ms=(unsigned)((esp_timer_get_time()-install_started)/1000);
    ota_trace_phases(&phases);
    printf("TAB5_OTA_PHASES transport=%d prepareMs=%u writeMs=%u freeWaitMs=%u readyWaitMs=%u verifyMs=%u installMs=%u\n",
        phases.transport,phases.prepare_ms,phases.write_ms,phases.free_wait_ms,phases.ready_wait_ms,phases.verify_ms,phases.install_ms);
    if(client){esp_http_client_close(client);esp_http_client_cleanup(client);client=NULL;opened=false;}
    ota_reader_free(reader);reader=NULL;
    if(flash_buffer!=flash_fallback)free(flash_buffer);
    flash_buffer=NULL;
    if(wifi_bulk)wifi_performance_bulk(false);
    free(offer);
    report("3 秒后自动重启");
    atomic_store(&progress_percent,100);
    atomic_store(&completed,true);
    atomic_store(&verifying,false);
    // Start the readable dwell only after the UI renders the success state.
    // Never render LVGL from this worker's small stack or race its display lock.
    for(unsigned waited=0;waited<5000&&!atomic_load(&completion_presented);waited+=50)
        vTaskDelay(pdMS_TO_TICKS(50));
    if(!atomic_load(&completion_presented)) {
        report("升级完成，请手动重启 TAB5");vTaskDelete(NULL);return;
    }
    vTaskDelay(pdMS_TO_TICKS(3000));
    report("正在重启…");vTaskDelay(pdMS_TO_TICKS(500));
    // Hide the panel while CPU reset stops DSI scanout. The new app lights it
    // only after its first rendered frame; saved brightness is not changed.
    esp_err_t backlight=bsp_display_backlight_off();
    if(backlight!=ESP_OK)printf("TAB5_OTA_BACKLIGHT error=%d\n",backlight);
    vTaskDelay(pdMS_TO_TICKS(50));esp_restart();
    return;
finish:
    atomic_store(&preparing,false);
    atomic_store(&verifying,false);
    ota_reader_join(reader);
    phases.ready_wait_ms=(unsigned)(ready_wait_us/1000);
    if(reader)phases.free_wait_ms=(unsigned)(reader->free_wait_us/1000);
    if(verify_started)phases.verify_ms=(unsigned)((esp_timer_get_time()-verify_started)/1000);
    phases.install_ms=(unsigned)((esp_timer_get_time()-install_started)/1000);
    ota_trace_phases(&phases);
    ota_trace_update(already_current?OTA_TRACE_DONE:OTA_TRACE_FAILED,received,error);
    if(writing)esp_ota_abort(handle);
    if(client){if(opened)esp_http_client_close(client);esp_http_client_cleanup(client);}
    ota_reader_free(reader);reader=NULL;
    if(flash_buffer!=flash_fallback)free(flash_buffer);
    if(wifi_bulk)wifi_performance_bulk(false);
    if(!already_current)report(failure);
    free(offer);atomic_store(&busy,false);vTaskDelete(NULL);
}
void ota_update_consider(const char *authenticated_frame) {
    cJSON *root=cJSON_Parse(authenticated_frame);
    if(!root)return;
    cJSON *data=cJSON_GetObjectItemCaseSensitive(root,"data");
    cJSON *ota=cJSON_GetObjectItemCaseSensitive(data,"ota");
    cJSON *clock=cJSON_GetObjectItemCaseSensitive(data,"epochMilliseconds");
    double frame_epoch=cJSON_IsNumber(clock)?clock->valuedouble:0;
    if(!cJSON_IsObject(ota)) {
        if(atomic_load(&busy)) {
            xSemaphoreTake(device_mutex,portMAX_DELAY);
            bool withdrawn=frame_epoch>active_offer_epoch&&active_offer_epoch>0;
            xSemaphoreGive(device_mutex);
            if(withdrawn)atomic_store(&offer_withdrawn,true);
        }
        if(!atomic_load(&busy)) {
            xSemaphoreTake(device_mutex,portMAX_DELAY);
            if(pending_offer.size)snprintf(message,sizeof(message),"电脑已停止提供固件");
            last_offer[0]=0;pending_offer.size=0;xSemaphoreGive(device_mutex);
        }
        cJSON_Delete(root);return;
    }
    cJSON *sha=cJSON_GetObjectItemCaseSensitive(ota,"sha256");
    cJSON *size=cJSON_GetObjectItemCaseSensitive(ota,"size");
    cJSON *version=cJSON_GetObjectItemCaseSensitive(ota,"version");
    cJSON *offer_id=cJSON_GetObjectItemCaseSensitive(ota,"offerId");
    cJSON *notes=cJSON_GetObjectItemCaseSensitive(ota,"notes");
    if(!cJSON_IsString(sha)||!valid_hex(sha->valuestring,64)||!cJSON_IsNumber(size)||
        size->valuedouble<1024||size->valuedouble>0x6e0000||size->valuedouble!=(double)size->valueint||
        !cJSON_IsString(version)||!version->valuestring||
        strlen(version->valuestring)==0||strlen(version->valuestring)>=32||
        !cJSON_IsString(offer_id)||!valid_hex(offer_id->valuestring,32)) {cJSON_Delete(root);return;}
    if(atomic_load(&busy)) {
        xSemaphoreTake(device_mutex,portMAX_DELAY);
        bool same=!strcmp(last_offer,offer_id->valuestring)&&!strcmp(active_sha,sha->valuestring);
        bool changed=!same&&frame_epoch>active_offer_epoch&&active_offer_epoch>0;
        if(same&&frame_epoch>active_offer_epoch)active_offer_epoch=frame_epoch;
        xSemaphoreGive(device_mutex);
        if(changed)atomic_store(&offer_withdrawn,true);
        cJSON_Delete(root);return;
    }
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    // Offers survive on the PC after the device reboots into the new image.
    // Suppress the installed version before enabling the upgrade button.
    const esp_app_desc_t *running=esp_app_get_description();
    if(!strcmp(running->version,version->valuestring)) {
        pending_offer.size=0;
        snprintf(message,sizeof(message),"已是最新版本");
        xSemaphoreGive(device_mutex);cJSON_Delete(root);return;
    }
    bool seen=!strcmp(last_offer,offer_id->valuestring);
    if(!seen&&frame_epoch>pending_offer_epoch)pending_offer_epoch=frame_epoch;
    if(!seen) {
        snprintf(pending_offer.sha,sizeof(pending_offer.sha),"%s",sha->valuestring);
        snprintf(pending_offer.version,sizeof(pending_offer.version),"%s",version->valuestring);
        snprintf(pending_offer.offer_id,sizeof(pending_offer.offer_id),"%s",offer_id->valuestring);
        pending_offer.size=size->valueint;
        snprintf(pending_offer.notes,sizeof(pending_offer.notes),"%s",
            cJSON_IsString(notes)&&notes->valuestring&&strlen(notes->valuestring)<sizeof(pending_offer.notes)?notes->valuestring:"");
        snprintf(message,sizeof(message),"新固件 %s · 可从电脑或此处开始升级",pending_offer.version);
    }
    xSemaphoreGive(device_mutex);
    cJSON_Delete(root);
}
// Called by the UI only after persisting the draft and checking active work.
bool ota_update_request_checked(const char *offer_id,const char *sha) {
    extern bool transport_benchmark_busy(void);
    if(atomic_exchange(&busy,true))return false;
    if(transport_benchmark_busy()||flash_benchmark_busy()){atomic_store(&busy,false);return false;}
    gallery_cache_reclaim();
    atomic_store(&offer_withdrawn,false);
    atomic_store(&completed,false);atomic_store(&completion_presented,false);
    atomic_store(&preparation_presented,false);atomic_store(&preparing,false);
    atomic_store(&verifying,false);atomic_store(&verify_started_ms,0);
    atomic_store(&prepare_ms,0);atomic_store(&buffer_slots,0);atomic_store(&download_bytes,0);
    atomic_store(&written_bytes,0);atomic_store(&image_bytes,0);
    atomic_store(&transfer_bytes,0);
    extern volatile unsigned tab5_lcd_underruns;atomic_store(&lcd_underruns_before,tab5_lcd_underruns);
    atomic_store(&progress_percent,0);
    ota_offer *offer=calloc(1,sizeof(*offer));
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    bool available=pending_offer.size>0&&((!offer_id&&!sha)||(offer_id&&sha&&!strcmp(pending_offer.offer_id,offer_id)&&!strcmp(pending_offer.sha,sha)));
    if(offer&&available){*offer=pending_offer;pending_offer.size=0;snprintf(last_offer,sizeof(last_offer),"%s",offer->offer_id);snprintf(target_version,sizeof(target_version),"%s",offer->version);snprintf(active_sha,sizeof(active_sha),"%s",offer->sha);active_offer_epoch=pending_offer_epoch;}
    xSemaphoreGive(device_mutex);
    if(!offer||!available){free(offer);atomic_store(&busy,false);return false;}
    atomic_store(&image_bytes,offer->size);
    report("正在准备固件更新");
    if(!offer||xTaskCreate(install,"tab5_ota",8192,offer,4,NULL)!=pdPASS){
        free(offer);report("无法启动更新任务");
        xSemaphoreTake(device_mutex,portMAX_DELAY);last_offer[0]=0;xSemaphoreGive(device_mutex);
        atomic_store(&busy,false);return false;
    }
    return true;
}

bool ota_update_request(void){return ota_update_request_checked(NULL,NULL);}
