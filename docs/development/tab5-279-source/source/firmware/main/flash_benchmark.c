#ifndef TAB5_FLASH_BENCH_TEST
#include "flash_benchmark.h"
#include "device.h"
#include "ota_update.h"
#include "transport_benchmark.h"
#include "voice_remote.h"
#include "codex_remote.h"
#include "camera_preview.h"
#include "esp_ota_ops.h"
#include "esp_image_format.h"
#include "esp_flash.h"
#include "spi_flash_chip_driver.h"
#include "esp_heap_caps.h"
#include "esp_timer.h"
#include "esp_memory_utils.h"
#include "freertos/idf_additions.h"
#endif
#include <stdbool.h>
#include <stdint.h>
#include <stdatomic.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

enum { SAMPLE_BYTES=128*1024, ROUNDS=3, VARIANTS=4, INTERNAL_RESERVE=96*1024 };
enum { BENCH_IDLE,BENCH_RUNNING,BENCH_COMPLETE,BENCH_FAILED,BENCH_CANCELLED };
enum { PHASE_CHECK,PHASE_BACKUP,PHASE_MEASURE,PHASE_RESTORE };
typedef struct {unsigned chunk,completed,skipped,erase_us[ROUNDS],write_us[ROUNDS];} flash_row;
typedef struct {
    unsigned state,phase,variant,round,address,offset,jedec,page_bytes,restored,modified,scanned;
    int error;
    flash_row rows[VARIANTS];
} flash_result;
static atomic_bool active,stopping;
static flash_result result;
bool flash_benchmark_busy(void){return atomic_load(&active);}
void flash_benchmark_cancel(void){if(flash_benchmark_busy())atomic_store(&stopping,true);}
static void publish(const flash_result *value) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);result=*value;xSemaphoreGive(device_mutex);
}
static flash_result snapshot(void) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);flash_result value=result;xSemaphoreGive(device_mutex);return value;
}
static unsigned median(const unsigned values[ROUNDS]) {
    unsigned a=values[0],b=values[1],c=values[2];
    if(a>b){unsigned t=a;a=b;b=t;}if(b>c){unsigned t=b;b=c;c=t;}return a>b?a:b;
}
void flash_benchmark_status(char *out,size_t size) {
    if(!out||!size)return;
    flash_result r=snapshot();
    if(r.state==BENCH_IDLE){snprintf(out,size,"测量擦除及纯写入速度，不包含 USB 或 Wi-Fi 传输。\n仅测试备用分区末尾空白区，完成后恢复并核验。");return;}
    if(r.state==BENCH_RUNNING) {
        snprintf(out,size,"%s%s",r.phase==PHASE_CHECK?"正在核验备用固件与测试范围…":
            r.phase==PHASE_BACKUP?"正在查找并备份空白测试区…":r.phase==PHASE_RESTORE?"正在恢复测试区并核验备用固件…":"正在测量…",
            atomic_load(&stopping)?"\n已请求停止，将先恢复测试区。":"");
        if(r.phase==PHASE_MEASURE&&!atomic_load(&stopping))snprintf(out,size,"正在测量 %u KiB 写入块 · 第 %u / 3 轮\n样本 128 KiB · 擦除、写入分别计时",r.rows[r.variant].chunk/1024,r.round+1);
        return;
    }
    size_t used=(size_t)snprintf(out,size,"%s · Flash %06X · 样本 128 KiB\n",
        r.state==BENCH_COMPLETE?"测试完成":r.state==BENCH_CANCELLED?"已停止":"测试未完成",r.jedec);
    if(r.state==BENCH_FAILED&&used<size) {
        const char *reason=r.error==ESP_ERR_NOT_FOUND&&r.phase==PHASE_BACKUP?"镜像之后没有足够的连续空白区":
            r.phase==PHASE_BACKUP?"读取测试区失败":r.error==ESP_ERR_NO_MEM?"可用内部内存不足":
            r.phase==PHASE_CHECK?"启动状态、镜像或范围检查未通过":"擦写或恢复校验失败";
        int n=snprintf(out+used,size-used,"%s · 错误 0x%X\n",reason,(unsigned)r.error);
        if(n>0)used+=(size_t)n;
    }
    for(unsigned i=0;i<VARIANTS&&used<size;i++) {
        const flash_row *row=&r.rows[i];unsigned write=median(row->write_us),erase=median(row->erase_us);
        int n=row->completed==ROUNDS&&write?snprintf(out+used,size-used,
            "%u KiB：%u KiB/s · 写入 %u.%u ms · 擦除 %u.%u ms\n",row->chunk/1024,
            (unsigned)((uint64_t)SAMPLE_BYTES*1000000/write/1024),write/1000,write/100%10,erase/1000,erase/100%10):
            snprintf(out+used,size-used,"%u KiB：%s\n",row->chunk/1024,row->skipped?"内部缓冲不足，跳过":"未完成三轮");
        if(n>0)used+=(size_t)n;
    }
    if(used<size)snprintf(out+used,size-used,"%s%s",r.restored?"测试区已恢复，备用固件校验通过":!r.modified?"未写入测试区":"请查看电脑诊断：测试区恢复未确认",
        r.state==BENCH_COMPLETE?"":"；不修改当前启动固件");
}
void flash_benchmark_json(char *out,size_t size) {
    if(!out||!size)return;
    flash_result r=snapshot();if(r.state==BENCH_IDLE){snprintf(out,size,"null");return;}
    size_t used=(size_t)snprintf(out,size,"{\"state\":%u,\"sampleBytes\":%u,\"partitionAddress\":%u,\"offset\":%u,"
        "\"jedecId\":%u,\"pageBytes\":%u,\"error\":%d,\"restored\":%u,\"modified\":%u,\"phase\":%u,\"scanned\":%u,\"rows\":[",
        r.state,SAMPLE_BYTES,r.address,r.offset,r.jedec,r.page_bytes,r.error,r.restored,r.modified,r.phase,r.scanned);
    for(unsigned i=0;i<VARIANTS&&used<size;i++) {
        const flash_row *p=&r.rows[i];int n=snprintf(out+used,size-used,
            "%s{\"chunkBytes\":%u,\"completed\":%u,\"skipped\":%u,\"eraseUs\":[%u,%u,%u],\"writeUs\":[%u,%u,%u]}",
            i?",":"",p->chunk,p->completed,p->skipped,p->erase_us[0],p->erase_us[1],p->erase_us[2],p->write_us[0],p->write_us[1],p->write_us[2]);
        if(n>0)used+=(size_t)n;
    }
    if(used+3<=size)snprintf(out+used,size-used,"]}");else snprintf(out,size,"null");
}
static uint8_t pattern(unsigned variant,unsigned round,unsigned at) {
    return (uint8_t)((at*31)^(at>>8)^(variant*47+round*19+17));
}
static bool blank(const uint8_t *bytes,size_t size) {
    for(size_t i=0;i<size;i++)if(bytes[i]!=255)return false;
    return true;
}
static void run(void *unused) {
    (void)unused;flash_result r=snapshot();
    uint8_t *saved=NULL,*buffer=NULL,check[4096];bool touched=false;
    // Image verification and Flash driver/ROM paths require an internal task
    // stack. PSRAM XIP does not make an external task stack equivalent.
    if(!esp_ptr_in_dram(&r)){r.error=ESP_ERR_INVALID_STATE;goto done;}
    const esp_partition_t *running=esp_ota_get_running_partition(),*next=esp_ota_get_next_update_partition(NULL);
    esp_image_metadata_t before={0},after={0};esp_ota_img_states_t state;
    if(!running||!next||next->type!=ESP_PARTITION_TYPE_APP||next->address==running->address||next->encrypted||
       esp_ota_get_boot_partition()!=running||esp_ota_get_state_partition(running,&state)!=ESP_OK||state!=ESP_OTA_IMG_VALID||
       next->size<SAMPLE_BYTES+4096||next->size%65536) {r.error=ESP_ERR_INVALID_STATE;goto done;}
    r.address=next->address;r.offset=next->size-SAMPLE_BYTES;
    esp_partition_pos_t position={.offset=next->address,.size=next->size};
    if(esp_image_verify(ESP_IMAGE_VERIFY,&position,&before)!=ESP_OK||before.image_len>r.offset-4096) {
        r.error=ESP_ERR_INVALID_SIZE;goto done;
    }
    if(!esp_flash_default_chip||!esp_flash_default_chip->chip_drv){r.error=ESP_ERR_INVALID_STATE;goto done;}
    uint32_t jedec=0;
    if(esp_flash_read_id(esp_flash_default_chip,&jedec)!=ESP_OK){r.error=ESP_FAIL;goto done;}
    r.jedec=(unsigned)jedec;
    r.page_bytes=esp_flash_default_chip->chip_drv->page_size;
    saved=heap_caps_malloc(SAMPLE_BYTES,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    if(!saved){r.error=ESP_ERR_NO_MEM;goto done;}
    r.phase=PHASE_BACKUP;
    // Old images can leave bytes after the current image. Never erase them to
    // make room: search only beyond the verified image plus its 4 KiB guard.
    unsigned first=(before.image_len+4096+65535)&~65535u;
    bool found=false;
    for(unsigned offset=r.offset;;offset-=65536) {
        r.offset=offset;r.scanned++;publish(&r);
        bool all_blank=true;
        for(unsigned at=0;at<SAMPLE_BYTES;at+=sizeof(check)) {
            if(atomic_load(&stopping))goto done;
            // Read via internal memory to avoid an SDK temporary allocation or
            // direct PSRAM destination; preserve the original read error.
            r.error=esp_partition_read(next,offset+at,check,sizeof(check));
            if(r.error!=ESP_OK)goto done;
            if(!blank(check,sizeof(check))){all_blank=false;break;}
            memcpy(saved+at,check,sizeof(check));
        }
        if(all_blank){found=true;break;}
        if(offset==first)break;
    }
    if(!found){r.error=ESP_ERR_NOT_FOUND;goto done;}
    for(unsigned variant=0;variant<VARIANTS&&!atomic_load(&stopping);variant++) {
        unsigned chunk=r.rows[variant].chunk;r.variant=variant;r.phase=PHASE_MEASURE;
        // Reuse the already reserved internal readback stack buffer for the
        // OTA fallback-size measurement. Larger variants retain their reserve.
        if(chunk==sizeof(check))buffer=check;
        else if(heap_caps_get_free_size(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT)<chunk+INTERNAL_RESERVE||
           !(buffer=heap_caps_malloc(chunk,MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT))) {
            r.rows[variant].skipped=1;publish(&r);continue;
        }
        for(unsigned round=0;round<ROUNDS&&!atomic_load(&stopping);round++) {
            r.round=round;r.modified=1;publish(&r);touched=true;
            int64_t began=esp_timer_get_time();
            r.error=esp_partition_erase_range(next,r.offset,SAMPLE_BYTES);
            r.rows[variant].erase_us[round]=(unsigned)(esp_timer_get_time()-began);
            if(r.error!=ESP_OK)goto restore;
            for(unsigned at=0;at<SAMPLE_BYTES;) {
                if(atomic_load(&stopping))goto restore;
                unsigned count=SAMPLE_BYTES-at;if(count>chunk)count=chunk;
                for(unsigned i=0;i<count;i++)buffer[i]=pattern(variant,round,at+i);
                began=esp_timer_get_time();r.error=esp_partition_write(next,r.offset+at,buffer,count);
                r.rows[variant].write_us[round]+=(unsigned)(esp_timer_get_time()-began);
                if(r.error!=ESP_OK)goto restore;
                at+=count;
            }
            // Pattern generation, readback and yields are outside write timings.
            for(unsigned at=0;at<SAMPLE_BYTES;) {
                unsigned count=SAMPLE_BYTES-at;if(count>chunk)count=chunk;
                r.error=esp_partition_read(next,r.offset+at,buffer,count);if(r.error!=ESP_OK)goto restore;
                for(unsigned i=0;i<count;i++)if(buffer[i]!=pattern(variant,round,at+i)){r.error=ESP_FAIL;goto restore;}
                at+=count;
            }
            r.rows[variant].completed++;publish(&r);vTaskDelay(pdMS_TO_TICKS(20));
        }
        if(buffer!=check)free(buffer);
        buffer=NULL;
    }
restore:
    if(touched) {
        r.phase=PHASE_RESTORE;publish(&r);
        // The saved region was verified all-FF before any erase. Even power loss
        // cannot corrupt either image: the measured range is outside its extent.
        for(unsigned attempt=0;attempt<3&&!r.restored;attempt++) {
            if(esp_partition_erase_range(next,r.offset,SAMPLE_BYTES)!=ESP_OK)continue;
            bool same=true;
            for(unsigned at=0;at<SAMPLE_BYTES&&same;at+=4096) {
                same=esp_partition_read(next,r.offset+at,check,sizeof(check))==ESP_OK&&
                    !memcmp(check,saved+at,sizeof(check));
            }
            if(same&&esp_image_verify(ESP_IMAGE_VERIFY,&position,&after)==ESP_OK&&
               after.image_len==before.image_len&&!memcmp(after.image_digest,before.image_digest,32)&&
               esp_ota_get_running_partition()==running&&esp_ota_get_boot_partition()==running)r.restored=1;
        }
        if(!r.restored)r.error=ESP_FAIL;
    }
done:
    if(buffer!=check)free(buffer);
    free(saved);
    unsigned completed=0;for(unsigned i=0;i<VARIANTS;i++)completed+=r.rows[i].completed;
    if(!completed&&!atomic_load(&stopping)&&!r.error)r.error=ESP_ERR_NO_MEM;
    r.state=r.error||(!touched&&!atomic_load(&stopping))||(!completed&&!atomic_load(&stopping))?BENCH_FAILED:
        atomic_load(&stopping)?BENCH_CANCELLED:BENCH_COMPLETE;
    publish(&r);atomic_store(&active,false);vTaskDeleteWithCaps(NULL);
}
bool flash_benchmark_start(void) {
    if(atomic_exchange(&active,true))return false;
    voice_remote_state voice;voice_remote_get(&voice);
    if(ota_update_busy()||transport_benchmark_busy()||voice.busy||codex_remote_busy()||camera_preview_busy()) {
        atomic_store(&active,false);return false;
    }
    const unsigned chunks[VARIANTS]={4096,16384,49152,65536};
    flash_result initial={.state=BENCH_RUNNING};for(unsigned i=0;i<VARIANTS;i++)initial.rows[i].chunk=chunks[i];
    publish(&initial);atomic_store(&stopping,false);
    if(xTaskCreateWithCaps(run,"tab5_flash_bench",12288,NULL,3,NULL,MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT)!=pdPASS) {
        initial.state=BENCH_FAILED;initial.error=ESP_ERR_NO_MEM;publish(&initial);atomic_store(&active,false);return false;
    }
    return true;
}
