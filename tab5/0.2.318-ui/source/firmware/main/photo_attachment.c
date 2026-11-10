#include "photo_attachment.h"
#include "photo_geometry.h"
#include "photo_display_probe.h"
#include "camera_preview.h"
#include "tab5_jpeg.h"
#include "sd_storage.h"
#include "driver/jpeg_decode.h"
#include "esp_cache.h"
#include "esp_heap_caps.h"
#include "gallery_remote.h"
#include "freertos/idf_additions.h"
#include <dirent.h>
#include <stdatomic.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <strings.h>
#include <sys/stat.h>
#include <errno.h>

static atomic_int phase;
static atomic_bool working;
static uint8_t *data;
static size_t data_size;
static uint16_t *preview;
static unsigned preview_width,preview_height;
static char error[96],job_path[512];
static photo_listing *listing;
static int job_kind,job_page;
static atomic_int codec_stage,codec_error;
static atomic_uint codec_internal,codec_largest,codec_psram,codec_written;
static void codec_mark(int stage,int code) {
    atomic_store(&codec_internal,heap_caps_get_free_size(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT));
    // Largest-block diagnostics walk the heap with interrupts masked. Keep the
    // legacy fields, but 0 now means unknown; do not scan during photo work.
    atomic_store(&codec_largest,0);atomic_store(&codec_psram,0);
    atomic_store(&codec_error,code);atomic_store(&codec_stage,stage);
}
void photo_diagnostics(char *out,unsigned size) {
    snprintf(out,size,"%d,%d,%u,%u,%u,%u",atomic_load(&codec_stage),atomic_load(&codec_error),
        atomic_load(&codec_internal),atomic_load(&codec_largest),atomic_load(&codec_psram),atomic_load(&codec_written));
    unsigned used=(unsigned)strlen(out);
    if(used+1<size){out[used++]=';';photo_display_probe_format(out+used,size-used);}
}
static bool safe_path(const char *p) {return p&&(!strcmp(p,"/sdcard")||!strncmp(p,"/sdcard/",8))&&!strstr(p,"/../")&&!strstr(p,"/./")&&!strstr(p,"\\")&&strlen(p)<sizeof(job_path)&&strcmp(p+strlen(p)-(strlen(p)>=3?3:0),"/..");}
static bool image_name(const char *name) {const char *dot=strrchr(name,'.');return dot&&(!strcasecmp(dot,".jpg")||!strcasecmp(dot,".jpeg"));}
static bool card_io_error(void){return errno==EIO||errno==ENODEV||errno==ENXIO||errno==ETIMEDOUT;}
static void fail(const char *message) {snprintf(error,sizeof(error),"%s",message);atomic_store(&phase,3);}
static void make_preview(const uint16_t *pixels,unsigned width,unsigned height,unsigned stride) {
    unsigned w,h;bool portrait=height>width;
    photo_fit(width,height,portrait?PHOTO_PREVIEW_H:PHOTO_PREVIEW_W,
              portrait?PHOTO_PREVIEW_W:PHOTO_PREVIEW_H,&w,&h);
    if(!w||!h)return;
    for(unsigned y=0;y<h;y++) {
        for(unsigned x=0;x<w;x++)preview[y*w+x]=pixels[(y*height/h)*stride+x*width/w];
        if((y&3)==3)vTaskDelay(1);
    }
    preview_width=w;preview_height=h;
}
static void *photo_output_alloc(size_t requested,size_t *capacity) {
    // Software encoding writes CPU-owned output: no DMA cache invalidation,
    // aligned giant clear, or extra RGB888 frame is required.
    void *out=heap_caps_malloc(requested,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    if(out)*capacity=requested;
    return out;
}
static void photo_encode_yield(void *context){(void)context;vTaskDelay(1);}
static bool prepare_camera(void) {
    size_t output_capacity=0;
    uint32_t written=0;int step=1;esp_err_t result=ESP_ERR_NO_MEM;
    const char *reason="照片内存不足，可稍后重试";
    atomic_store(&codec_written,0);codec_mark(step,0);
    step=2;codec_mark(step,0);reason="相机仍在结束，可稍后重试";result=ESP_ERR_INVALID_STATE;
    // The lease keeps the displayed full-resolution still immutable until
    // encoding finishes. The LCD continues its ordinary refresh throughout.
    const uint8_t *pixels=camera_preview_acquire();
    if(!pixels)goto finished;
    unsigned width,height;camera_preview_dimensions(&width,&height);
    photo_display_probe_mark(PHOTO_DISPLAY_PROBE_PREVIEW);
    make_preview((const uint16_t *)pixels,width,height,width);
    step=3;codec_mark(step,0);reason="照片输出内存不足，可重试";result=ESP_ERR_NO_MEM;
    for(size_t capacity=PHOTO_MAX_BYTES;capacity>=256*1024;capacity/=2) {
        data=photo_output_alloc(capacity,&output_capacity);
        if(data)break;
    }
    if(!data)goto finished;
    step=5;codec_mark(step,0);reason="照片编码失败，可重试";
    photo_display_probe_mark(PHOTO_DISPLAY_PROBE_ENCODE);
    tab5_jpeg_result encoded=tab5_jpeg_encode((const uint16_t *)pixels,1280*720*2,
        width,height,data,output_capacity,&written,photo_encode_yield,NULL);
    result=encoded==TAB5_JPEG_OK?ESP_OK:encoded==TAB5_JPEG_MEMORY?ESP_ERR_NO_MEM:
        encoded==TAB5_JPEG_FULL?ESP_ERR_INVALID_SIZE:ESP_ERR_INVALID_STATE;
    atomic_store(&codec_written,written);if(result!=ESP_OK)goto finished;
    step=6;codec_mark(step,0);reason="照片编码结果无效，可重试";
    if(!written||written>output_capacity||written>PHOTO_MAX_BYTES){result=ESP_ERR_INVALID_SIZE;goto finished;}
finished:
    codec_mark(result==ESP_OK?7:step,result);
    camera_preview_release(pixels);
    if(result!=ESP_OK) {
        free(data);data=NULL;data_size=0;
        snprintf(error,sizeof(error),"%s (%d/%d)",reason,step,result);
        atomic_store(&phase,3);return false;
    }
    uint8_t *compact=heap_caps_realloc(data,written,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    if(compact)data=compact;
    data_size=written;return true;
}
static bool prepare_file(void) {
    if(!sd_storage_lock()){fail("未找到可读取的存储卡");return false;}
    struct stat st;
    int stat_error=stat(job_path,&st);
    if(stat_error||!S_ISREG(st.st_mode)||st.st_size<16||st.st_size>PHOTO_MAX_BYTES){sd_storage_unlock(stat_error&&card_io_error());fail("请选择不超过 2 MB 的 JPG 图片");return false;}
    size_t capacity=0;const jpeg_decode_memory_alloc_cfg_t in_cfg={.buffer_direction=JPEG_DEC_ALLOC_INPUT_BUFFER},out_cfg={.buffer_direction=JPEG_DEC_ALLOC_OUTPUT_BUFFER};
    data=jpeg_alloc_decoder_mem(st.st_size,&in_cfg,&capacity);if(!data){sd_storage_unlock(false);fail("图片内存不足");return false;}
    FILE *file=fopen(job_path,"rb");if(!file){sd_storage_unlock(card_io_error());fail("无法打开图片");return false;}
    data_size=fread(data,1,st.st_size,file);bool media_error=ferror(file)&&card_io_error();if(fclose(file))media_error|=card_io_error();sd_storage_unlock(media_error);
    jpeg_decode_picture_info_t info={0};
    if(data_size!=st.st_size||jpeg_decoder_get_info(data,data_size,&info)!=ESP_OK||!info.width||!info.height||info.width>4096||info.height>4096||(uint64_t)info.width*info.height>4194304){fail("图片损坏或尺寸过大");return false;}
    unsigned stride=(info.width+15)&~15u,height=(info.height+15)&~15u;
    uint8_t *pixels=jpeg_alloc_decoder_mem(stride*height*2,&out_cfg,&capacity);
    jpeg_decoder_handle_t codec=NULL;const jpeg_decode_engine_cfg_t engine={.timeout_ms=3000};
    const jpeg_decode_cfg_t cfg={.output_format=JPEG_DECODE_OUT_FORMAT_RGB565,.rgb_order=JPEG_DEC_RGB_ELEMENT_ORDER_BGR,.conv_std=JPEG_YUV_RGB_CONV_STD_BT601};
    uint32_t written=0;
    // Match the gallery path: allocator cache lines must not overwrite DMA output.
    bool ok=pixels&&esp_cache_msync(pixels,capacity,ESP_CACHE_MSYNC_FLAG_DIR_C2M|ESP_CACHE_MSYNC_FLAG_INVALIDATE)==ESP_OK&&
        jpeg_new_decoder_engine(&engine,&codec)==ESP_OK&&jpeg_decoder_process(codec,&cfg,data,data_size,pixels,capacity,&written)==ESP_OK;
    if(ok)make_preview((uint16_t *)pixels,info.width,info.height,stride);
    if(codec)jpeg_del_decoder_engine(codec);
    free(pixels);
    if(!ok)fail("图片无法预览，请选择普通 JPG");
    return ok;
}
static void list_files(void) {
    memset(listing,0,sizeof(*listing));listing->page=job_page;snprintf(listing->directory,sizeof(listing->directory),"%s",job_path);
    if(!sd_storage_lock()){snprintf(listing->error,sizeof(listing->error),"未找到存储卡；请插入 FAT32 卡");return;}
    DIR *dir=opendir(job_path);if(!dir){sd_storage_unlock(card_io_error());snprintf(listing->error,sizeof(listing->error),"无法读取此目录");return;}
    struct dirent *entry;int seen=0;bool media_error=false;
    for(;;) {
        errno=0;entry=readdir(dir);if(!entry){media_error|=card_io_error();break;}
        if(entry->d_name[0]=='.')continue;
        char path[768];snprintf(path,sizeof(path),"%s/%s",job_path,entry->d_name);struct stat st;
        if(strlen(path)>=sizeof(job_path))continue;
        if(stat(path,&st)){media_error|=card_io_error();continue;}
        bool folder=S_ISDIR(st.st_mode);if(!folder&&(!S_ISREG(st.st_mode)||!image_name(entry->d_name)))continue;
        if(seen++<job_page*PHOTO_LIST_COUNT)continue;
        if(listing->count==PHOTO_LIST_COUNT){listing->more=true;break;}
        photo_entry *item=&listing->entries[listing->count++];item->folder=folder;snprintf(item->name,sizeof(item->name),"%s",entry->d_name);
    }
    if(closedir(dir))media_error|=card_io_error();
    sd_storage_unlock(media_error);
}
static void worker(void *arg) {
    (void)arg;
    if(job_kind==2){list_files();listing->state=2;}
    else {
        free(data);data=NULL;data_size=0;
        if(!preview)preview=heap_caps_malloc(PHOTO_PREVIEW_W*PHOTO_PREVIEW_H*2,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
        if(!preview)fail("图片预览内存不足");
        else if(job_kind==0?prepare_camera():prepare_file())atomic_store(&phase,2);
    }
    atomic_store(&working,false);vTaskDeleteWithCaps(NULL);
}
static bool start(int kind,const char *path,int page) {
    if((path&&!safe_path(path))||page<0||page>500||atomic_exchange(&working,true))return false;
    gallery_cache_reclaim();
    if(kind==2&&!listing) {
        listing=heap_caps_calloc(1,sizeof(*listing),MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
        if(!listing){fail("目录内存不足，请稍后重试");atomic_store(&working,false);return false;}
    }
    job_kind=kind;job_page=page;snprintf(job_path,sizeof(job_path),"%s",path?path:"");error[0]=0;
    if(kind==2)listing->state=1;else atomic_store(&phase,1);
    if(xTaskCreateWithCaps(worker,"tab5_photo",8192,NULL,3,NULL,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT)!=pdPASS){atomic_store(&working,false);fail("无法启动图片处理");return false;}return true;
}
bool photo_prepare_camera(void){return start(0,NULL,0);}
bool photo_prepare_file(const char *path){return start(1,path,0);}
bool photo_list_directory(const char *path,int page){return start(2,path,page);}
const photo_listing *photo_get_listing(void){return atomic_load(&working)?NULL:listing;}
int photo_state(void){return atomic_load(&working)?1:atomic_load(&phase);}
const char *photo_error(void){return error;}
const uint8_t *photo_data(size_t *size){if(photo_state()!=2)return NULL;*size=data_size;return data;}
const uint16_t *photo_preview(void){return photo_state()==2?preview:NULL;}
uint16_t *photo_take_preview(void){
    if(atomic_load(&working)||atomic_load(&phase)!=2)return NULL;
    uint16_t *pixels=preview;preview=NULL;return pixels;
}
void photo_preview_dimensions(unsigned *width,unsigned *height){*width=preview_width;*height=preview_height;}
void photo_discard(void){if(atomic_load(&working))return;free(data);data=NULL;data_size=0;atomic_store(&phase,0);}
