#define TAB5_FLASH_BENCH_TEST
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stddef.h>
#include <stdlib.h>
#include <string.h>
#include <stdio.h>
#include "cJSON.h"
enum {ESP_OK=0,ESP_FAIL=-1,ESP_ERR_INVALID_STATE=259,ESP_ERR_INVALID_SIZE=260,ESP_ERR_NO_MEM=257,ESP_ERR_NOT_FOUND=261,
      ESP_PARTITION_TYPE_APP=0,ESP_OTA_IMG_VALID=2,ESP_IMAGE_VERIFY=0,
      MALLOC_CAP_SPIRAM=1,MALLOC_CAP_INTERNAL=2,MALLOC_CAP_8BIT=4,portMAX_DELAY=0,pdPASS=1};
typedef struct {unsigned type,address,size;bool encrypted;} esp_partition_t;
typedef int esp_ota_img_states_t;
typedef struct {unsigned offset,size;} esp_partition_pos_t;
typedef struct {unsigned image_len;uint8_t image_digest[32];} esp_image_metadata_t;
typedef struct {unsigned page_size;} fake_driver;
typedef struct {fake_driver *chip_drv;} fake_flash;
typedef struct {bool busy;} voice_remote_state;
static fake_driver driver={256};static fake_flash chip={&driver},*esp_flash_default_chip=&chip;
static esp_partition_t running={0,0x20000,0x6e0000,false},spare={0,0x700000,0x6e0000,false};
static bool missing_running,missing_spare,pending_boot,invalid_image,overlap,invalid_state;
static bool ota_busy,transport_busy,voice_busy,codex_busy,camera_busy,task_fail,save_fail,internal_fail;
static unsigned free_bytes,erase_calls,write_calls,read_calls,image_calls,deleted,cancel_at,write_fail_at,erase_fail_at,read_fail_at;
static bool corrupt_read,restore_fail,task_pending,digest_changed,external_stack,measure_read_fail;
static int64_t fake_us;
static uint8_t tail[320*1024],protected_image[64];
static void (*worker)(void*);static void *device_mutex;
static int xSemaphoreTake(void *m,int delay){(void)m;(void)delay;return 1;}
static void xSemaphoreGive(void *m){(void)m;}
static bool ota_update_busy(void){return ota_busy;}
static bool transport_benchmark_busy(void){return transport_busy;}
static bool codex_remote_busy(void){return codex_busy;}
static bool camera_preview_busy(void){return camera_busy;}
static void voice_remote_get(voice_remote_state *s){s->busy=voice_busy;}
static const esp_partition_t *esp_ota_get_running_partition(void){return missing_running?NULL:&running;}
static const esp_partition_t *esp_ota_get_next_update_partition(void *p){(void)p;return missing_spare?NULL:&spare;}
static const esp_partition_t *esp_ota_get_boot_partition(void){return pending_boot?&spare:&running;}
static int esp_ota_get_state_partition(const esp_partition_t *p,int *state){assert(p==&running);*state=invalid_state?1:2;return ESP_OK;}
static int esp_image_verify(int mode,const esp_partition_pos_t *p,esp_image_metadata_t *out){
    assert(mode==ESP_IMAGE_VERIFY&&p->offset==spare.address&&p->size==spare.size);image_calls++;
    for(unsigned i=0;i<sizeof(protected_image);i++)assert(protected_image[i]==(uint8_t)(i*11));
    fake_us+=900000;out->image_len=overlap?spare.size-65536:6852688;
    memset(out->image_digest,digest_changed&&image_calls>1?43:42,32);return invalid_image?ESP_FAIL:ESP_OK;
}
static int esp_flash_read_id(fake_flash *p,uint32_t *id){assert(p==&chip);*id=0x123456;return ESP_OK;}
static void *heap_caps_malloc(unsigned size,unsigned caps){
    if((caps&MALLOC_CAP_SPIRAM)&&save_fail)return NULL;
    if((caps&MALLOC_CAP_INTERNAL)&&internal_fail)return NULL;
    return malloc(size);
}
static unsigned heap_caps_get_free_size(unsigned caps){assert(caps==(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT));return free_bytes;}
static bool esp_ptr_in_dram(const void *p){assert(p);return !external_stack;}
static int64_t esp_timer_get_time(void){return fake_us;}
static void vTaskDelay(int ticks){fake_us+=ticks*1000;}
#define pdMS_TO_TICKS(ms) (ms)
static int xTaskCreateWithCaps(void (*entry)(void*),const char *name,unsigned stack,void *arg,unsigned priority,void *handle,unsigned caps){
    (void)name;(void)arg;(void)priority;(void)handle;assert(stack>=8192&&caps==(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT));
    if(task_fail)return 0;worker=entry;task_pending=true;return pdPASS;
}
static void vTaskDeleteWithCaps(void *p){assert(!p);deleted++;}
static void access_range(const esp_partition_t *p,unsigned offset,unsigned count){
    assert(p==&spare&&p->address!=running.address);assert(offset>=p->size-sizeof(tail));
    assert(offset<=p->size&&count<=p->size-offset);
}
static int esp_partition_erase_range(const esp_partition_t *p,unsigned offset,unsigned count);
static int esp_partition_read(const esp_partition_t *p,unsigned offset,void *out,unsigned count);
static int esp_partition_write(const esp_partition_t *p,unsigned offset,const void *bytes,unsigned count);
#include "../../firmware/main/flash_benchmark.c"
static int esp_partition_erase_range(const esp_partition_t *p,unsigned offset,unsigned count){
    access_range(p,offset,count);assert(offset%4096==0&&count%4096==0);erase_calls++;fake_us+=5000;
    memset(tail+offset-(p->size-sizeof(tail)),255,count/2); // A failing erase may have changed bytes.
    if(erase_calls==erase_fail_at||(restore_fail&&result.phase==PHASE_RESTORE))return ESP_FAIL;
    memset(tail+offset-(p->size-sizeof(tail)),255,count);return ESP_OK;
}
static int esp_partition_read(const esp_partition_t *p,unsigned offset,void *out,unsigned count){
    access_range(p,offset,count);read_calls++;fake_us+=4000;
    if(read_calls==read_fail_at||(measure_read_fail&&result.phase==PHASE_MEASURE))return ESP_FAIL;
    memcpy(out,tail+offset-(p->size-sizeof(tail)),count);
    if(corrupt_read&&result.phase==PHASE_MEASURE)((uint8_t*)out)[0]^=1;
    return ESP_OK;
}
static int esp_partition_write(const esp_partition_t *p,unsigned offset,const void *bytes,unsigned count){
    access_range(p,offset,count);assert(flash_benchmark_busy());write_calls++;fake_us+=count/16+100;
    memcpy(tail+offset-(p->size-sizeof(tail)),bytes,count);
    if(write_calls==cancel_at)flash_benchmark_cancel();
    return write_calls==write_fail_at?ESP_FAIL:ESP_OK;
}
static void reset(void){
    assert(!flash_benchmark_busy());memset(&result,0,sizeof(result));atomic_store(&stopping,false);
    memset(tail,255,sizeof(tail));for(unsigned i=0;i<sizeof(protected_image);i++)protected_image[i]=(uint8_t)(i*11);
    running=(esp_partition_t){0,0x20000,0x6e0000,false};spare=(esp_partition_t){0,0x700000,0x6e0000,false};
    missing_running=missing_spare=pending_boot=invalid_image=overlap=invalid_state=false;
    ota_busy=transport_busy=voice_busy=codex_busy=camera_busy=task_fail=save_fail=internal_fail=false;
    corrupt_read=restore_fail=task_pending=digest_changed=external_stack=measure_read_fail=false;free_bytes=256*1024;fake_us=0;
    erase_calls=write_calls=read_calls=image_calls=deleted=cancel_at=write_fail_at=erase_fail_at=read_fail_at=0;
}
static void execute(void){assert(task_pending);task_pending=false;worker(NULL);assert(!flash_benchmark_busy()&&deleted==1);}
static void safe_failure(void){assert(flash_benchmark_start());execute();assert(result.state==BENCH_FAILED&&!erase_calls&&!write_calls&&!result.modified);}
static void restored(void){assert(result.restored&&blank(tail+result.offset-(spare.size-sizeof(tail)),SAMPLE_BYTES)&&image_calls>=2);}
int main(void){
    reset();char json[1280],status[900];flash_benchmark_json(json,sizeof(json));assert(!strcmp(json,"null"));
    bool *busy_flags[]={&ota_busy,&transport_busy,&voice_busy,&codex_busy,&camera_busy};
    for(unsigned i=0;i<5;i++){reset();*busy_flags[i]=true;assert(!flash_benchmark_start()&&!task_pending&&!flash_benchmark_busy());}
    reset();task_fail=true;assert(!flash_benchmark_start()&&!flash_benchmark_busy()&&!erase_calls);
    reset();external_stack=true;safe_failure();assert(!read_calls&&!image_calls);
    reset();assert(flash_benchmark_start());assert(!flash_benchmark_start());execute();
    assert(result.state==BENCH_COMPLETE&&erase_calls==13);restored();
    for(unsigned i=0;i<4;i++){unsigned calls=(SAMPLE_BYTES+result.rows[i].chunk-1)/result.rows[i].chunk;
        assert(result.rows[i].completed==3&&!result.rows[i].skipped);
        for(unsigned j=0;j<3;j++)assert(result.rows[i].erase_us[j]==5000&&result.rows[i].write_us[j]==SAMPLE_BYTES/16+calls*100);
    }
    flash_benchmark_status(status,sizeof(status));assert(strstr(status,"测试完成")&&strstr(status,"校验通过"));
    flash_benchmark_json(json,sizeof(json));cJSON *parsed=cJSON_Parse(json);assert(parsed);assert(cJSON_GetArraySize(cJSON_GetObjectItem(parsed,"rows"))==4);cJSON_Delete(parsed);
    reset();missing_running=true;safe_failure();reset();missing_spare=true;safe_failure();
    reset();pending_boot=true;safe_failure();reset();invalid_state=true;safe_failure();
    reset();spare.address=running.address;safe_failure();reset();spare.type=1;safe_failure();
    reset();spare.encrypted=true;safe_failure();reset();spare.size=131072;safe_failure();
    reset();spare.size--;safe_failure();reset();invalid_image=true;safe_failure();reset();overlap=true;safe_failure();
    reset();memset(tail,17,sizeof(tail));safe_failure();assert(result.error==ESP_ERR_NOT_FOUND&&result.scanned==4&&tail[99]==17);
    flash_benchmark_status(status,sizeof(status));assert(strstr(status,"连续空白区")&&strstr(status,"0x105"));
    reset();tail[sizeof(tail)-1]=17;assert(flash_benchmark_start());execute();
    assert(result.state==BENCH_COMPLETE&&result.scanned==2&&result.offset==spare.size-SAMPLE_BYTES-65536&&tail[sizeof(tail)-1]==17);restored();
    reset();memset(tail+SAMPLE_BYTES,17,sizeof(tail)-SAMPLE_BYTES);assert(flash_benchmark_start());execute();
    assert(result.state==BENCH_COMPLETE&&result.scanned==4&&result.offset==0x690000&&tail[sizeof(tail)-1]==17);restored();
    reset();read_fail_at=1;safe_failure();assert(result.error==ESP_FAIL);
    flash_benchmark_status(status,sizeof(status));assert(strstr(status,"读取测试区失败"));
    reset();save_fail=true;safe_failure();
    reset();internal_fail=true;assert(flash_benchmark_start());execute();
    assert(result.state==BENCH_COMPLETE&&result.rows[0].chunk==4096&&result.rows[0].completed==3);
    for(unsigned i=1;i<4;i++)assert(result.rows[i].skipped&&!result.rows[i].completed);
    restored();
    reset();free_bytes=38603;assert(flash_benchmark_start());execute();
    assert(result.state==BENCH_COMPLETE&&result.rows[0].completed==3&&write_calls==96);
    for(unsigned i=1;i<4;i++)assert(result.rows[i].skipped&&!result.rows[i].completed);
    restored();
    reset();free_bytes=160*1024-1;assert(flash_benchmark_start());execute();
    assert(result.state==BENCH_COMPLETE&&result.rows[2].completed==3&&result.rows[3].skipped);restored();
    reset();write_fail_at=2;assert(flash_benchmark_start());execute();assert(result.state==BENCH_FAILED);restored();
    reset();erase_fail_at=1;assert(flash_benchmark_start());execute();assert(result.state==BENCH_FAILED);restored();
    reset();corrupt_read=true;assert(flash_benchmark_start());execute();assert(result.state==BENCH_FAILED);restored();
    reset();measure_read_fail=true;assert(flash_benchmark_start());execute();assert(result.state==BENCH_FAILED);restored();
    reset();cancel_at=2;assert(flash_benchmark_start());execute();assert(result.state==BENCH_CANCELLED);restored();
    reset();assert(flash_benchmark_start());flash_benchmark_cancel();execute();
    assert(result.state==BENCH_CANCELLED&&!erase_calls&&!result.modified);
    reset();cancel_at=2;restore_fail=true;assert(flash_benchmark_start());execute();
    assert(result.state==BENCH_FAILED&&!result.restored&&erase_calls==4); // Three bounded restore attempts.
    reset();digest_changed=true;assert(flash_benchmark_start());execute();
    assert(result.state==BENCH_FAILED&&!result.restored&&erase_calls==15&&image_calls==4);
    for(unsigned i=0;i<4;i++)for(unsigned j=0;j<3;j++)result.rows[i].erase_us[j]=result.rows[i].write_us[j]=UINT32_MAX;
    flash_benchmark_json(json,sizeof(json));parsed=cJSON_Parse(json);assert(parsed&&strlen(json)<sizeof(json));cJSON_Delete(parsed);
    flash_benchmark_json(json,16);assert(!strcmp(json,"null"));
    puts("FLASH_BENCHMARK_OK timing_separated internal_4KB_at_38KB_or_allocation_failure guards_partial_failure_cancel_restore_json");return 0;
}
