#include "camera_preview.h"
#include "camera_viewport.h"
#include "bsp/m5stack_tab5.h"
#include "esp_heap_caps.h"
#include "gallery_remote.h"
#include "camera_gallery_buffer.h"
#include "freertos/idf_additions.h"
#include "esp_video_device.h"
#include "esp_video_init.h"
#include "esp_video_ioctl.h"
#include "esp_attr.h"
#include "esp_timer.h"
#include "esp_log.h"
#include "linux/videodev2.h"
#include <errno.h>
#include <fcntl.h>
#include <stdio.h>
#include <stdatomic.h>
#include <stdint.h>
#include <string.h>
#include <sys/ioctl.h>
#include <unistd.h>
#include <stdarg.h>

#define PREVIEW_WIDTH CAMERA_CAPTURE_WIDTH
#define PREVIEW_HEIGHT CAMERA_CAPTURE_HEIGHT
#define PREVIEW_BYTES (PREVIEW_WIDTH * PREVIEW_HEIGHT * 2)
#define VIEW_BYTES (CAMERA_VIEW_WIDTH * CAMERA_VIEW_HEIGHT * 2)
// One frame displayed, one waiting for the LVGL lock, one owned by CSI. With
// backup disabled the driver keeps capturing into the last unreturned frame;
// two buffers would stall while the displayed frame is retained by LVGL.
#define CAMERA_BUFFERS 3
#define CAMERA_FRAME_CAPS (MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT|MALLOC_CAP_CACHE_ALIGNED)
#define CAMERA_POOL_FIXED 1

static atomic_bool active, keep_running, capture_requested;
static atomic_int state;
static bool video_initialized, video_init_failed;
static uint16_t *still_pixels;
static unsigned still_width=1280,still_height=720;
void camera_preview_dimensions(unsigned *w,unsigned *h){*w=still_width;*h=still_height;}
// Reserve all three frames at startup, before UI/attachment/worker allocations
// can split their contiguous storage. They are never lent to the general heap
// between sessions; small retained thumbnails must not strand a frame-sized hole.
static uint8_t *frame_pool[CAMERA_BUFFERS];
// A teardown fault prevents reuse of this pool until the next real reboot.
static uint8_t *quarantined_frames[CAMERA_BUFFERS];
static bool release_failed;
static lv_obj_t *preview_canvas;
static bool (*apply_orientation)(int direction);
static atomic_int requested_direction;
void camera_preview_set_orientation_handler(bool (*apply)(int)) {apply_orientation=apply;}
void camera_preview_request_orientation(int direction) {
    if(camera_pose_valid(direction))atomic_store(&requested_direction,direction);
}
static char failure_text[144];
static atomic_int diagnostic_stage, diagnostic_error;
static atomic_uint frames;
static vprintf_like_t previous_log;
static _Atomic(TaskHandle_t) camera_log_task;
static portMUX_TYPE camera_log_lock=portMUX_INITIALIZER_UNLOCKED;
static char driver_error[160];
static unsigned gallery_loans;
uint8_t *camera_gallery_buffer_take(void) {
    uint8_t *pixels=NULL;
    portENTER_CRITICAL(&camera_log_lock);
    if(!atomic_load(&active)&&!release_failed) {
        for(unsigned i=0;i<CAMERA_BUFFERS;i++)
            if(frame_pool[i]&&frame_pool[i]!=(uint8_t *)still_pixels&&!(gallery_loans&(1u<<i))) {
                gallery_loans|=1u<<i;pixels=frame_pool[i];break;
            }
    }
    portEXIT_CRITICAL(&camera_log_lock);
    return pixels;
}
bool camera_gallery_buffer_release(uint8_t *pixels) {
    if(!pixels)return false;
    bool owned=false;
    portENTER_CRITICAL(&camera_log_lock);
    for(unsigned i=0;i<CAMERA_BUFFERS;i++)if(pixels==frame_pool[i]) {
        gallery_loans&=~(1u<<i);owned=true;break;
    }
    portEXIT_CRITICAL(&camera_log_lock);
    return owned;
}
bool camera_gallery_buffers_idle(void) {
    portENTER_CRITICAL(&camera_log_lock);
    bool idle=gallery_loans==0;
    portEXIT_CRITICAL(&camera_log_lock);
    return idle;
}
bool camera_preview_init_buffers(void) {
    if(frame_pool[CAMERA_BUFFERS-1])return true;
    for(unsigned i=0;i<CAMERA_BUFFERS;i++) {
        frame_pool[i]=heap_caps_aligned_alloc(64,PREVIEW_BYTES,CAMERA_FRAME_CAPS);
        if(!frame_pool[i]) {
            ESP_LOGE("camera_pool","startup frame=%u/%u size=%u free=%u largest=%u",i+1,CAMERA_BUFFERS,
                (unsigned)PREVIEW_BYTES,(unsigned)heap_caps_get_free_size(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT),
                (unsigned)heap_caps_get_largest_free_block(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT));
            for(unsigned j=0;j<i;j++){free(frame_pool[j]);frame_pool[j]=NULL;}
            return false;
        }
    }
    still_pixels=(uint16_t *)frame_pool[0];
    return true;
}
// Retain the FIRST driver error from this camera worker; later unwind errors
// otherwise hide whether CSI, DMA, ISP, or the sensor failed. Other task logs,
// messages, photos and credentials are never copied into this diagnostic.
static int camera_log(const char *format,va_list args) {
    if(!xPortInIsrContext()&&xTaskGetCurrentTaskHandle()==camera_log_task) {
        char line[160];va_list copy;va_copy(copy,args);
        vsnprintf(line,sizeof(line),format,copy);va_end(copy);
        if(strstr(line,"E (")&&!strstr(line,"tab5_camera:")) {
            portENTER_CRITICAL(&camera_log_lock);
            if(!driver_error[0])snprintf(driver_error,sizeof(driver_error),"%s",line);
            portEXIT_CRITICAL(&camera_log_lock);
        }
    }
    return previous_log?previous_log(format,args):0;
}
void camera_preview_driver_error_hex(char *out,unsigned size) {
    static const char digits[]="0123456789abcdef";
    if(!size)return;
    portENTER_CRITICAL(&camera_log_lock);
    unsigned i=0;while(driver_error[i]&&i<(sizeof(driver_error)-1)&&i*2+2<size) {
        unsigned char c=(unsigned char)driver_error[i];out[i*2]=digits[c>>4];out[i*2+1]=digits[c&15];i++;
    }
    out[i*2]=0;
    portEXIT_CRITICAL(&camera_log_lock);
}
static atomic_uint prepare_ms,lock_wait_ms,lock_hold_ms,capture_ms,capture_started_ms;
static unsigned elapsed_ms(int64_t start) {
    int64_t value=(esp_timer_get_time()-start)/1000;
    return (unsigned)(value>9999?9999:value);
}
static unsigned previous_stage, previous_error;
RTC_NOINIT_ATTR static struct {unsigned magic,stage,error;} saved_diagnostic;

void camera_preview_init_diagnostics(void) {
    previous_log=esp_log_set_vprintf(camera_log);
    if(saved_diagnostic.magic==0x43414d35) {
        previous_stage=saved_diagnostic.stage;previous_error=saved_diagnostic.error;
    }
    saved_diagnostic.magic=0;saved_diagnostic.stage=saved_diagnostic.error=0;
}
static void mark(int stage,int error) {
    atomic_store(&diagnostic_stage,stage);atomic_store(&diagnostic_error,error);
    saved_diagnostic.stage=stage;saved_diagnostic.error=error;saved_diagnostic.magic=0x43414d35;
}
const char *camera_preview_error(void) {return failure_text;}
void camera_preview_diagnostics(char *out,unsigned size) {
    unsigned presented,compose,present,mode;camera_viewport_diagnostics(&presented,&compose,&present,&mode);
    snprintf(out,size,"%d,%d,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u",atomic_load(&diagnostic_stage),atomic_load(&diagnostic_error),
        (unsigned)heap_caps_get_free_size(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT),
        camera_viewport_diagnostic_largest(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT),
        atomic_load(&frames),previous_stage,previous_error,atomic_load(&prepare_ms),
        atomic_load(&lock_wait_ms),atomic_load(&lock_hold_ms),atomic_load(&capture_ms),presented,compose,present,mode);
}

camera_preview_state camera_preview_get_state(void) { return (camera_preview_state)atomic_load(&state); }
bool camera_preview_busy(void) {return atomic_load(&active);}
const uint8_t *camera_preview_acquire(void) {
    if(atomic_exchange(&active,true))return NULL;
    if(atomic_load(&state)!=CAMERA_PREVIEW_CAPTURED||!still_pixels) {
        atomic_store(&active,false);return NULL;
    }
    return (const uint8_t *)still_pixels;
}
void camera_preview_release(const uint8_t *pixels) {
    if(pixels&&(const uint8_t *)still_pixels==pixels&&atomic_load(&state)==CAMERA_PREVIEW_CAPTURED)
        atomic_store(&active,false);
}
bool camera_preview_capture(void) {
    int expected=CAMERA_PREVIEW_LIVE;
    if(!atomic_compare_exchange_strong(&state,&expected,CAMERA_PREVIEW_CAPTURING))return false;
    atomic_store(&capture_started_ms,(unsigned)(esp_timer_get_time()/1000));
    atomic_store(&capture_requested,true);
    return true;
}
void camera_preview_stop(void) {atomic_store(&keep_running,false);}

static bool camera_keep_frame(void *ctx) {(void)ctx;return atomic_load(&keep_running);}

static bool camera_wait_for_startup_memory(void) {
    // Boot/network jobs can temporarily own the headroom needed for the first
    // sensor registration. Wait on this worker, without blocking LVGL or
    // retrying partially completed driver initialization.
    int64_t deadline=esp_timer_get_time()+4000000;
    do {
        if(!atomic_load(&keep_running))return false;
        if(heap_caps_get_free_size(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT)>=49152&&
           heap_caps_get_largest_free_block(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT)>=16384)return true;
        vTaskDelay(pdMS_TO_TICKS(20));
    }while(esp_timer_get_time()<deadline);
    return false;
}

static bool camera_wait_for_gallery(void) {
    int64_t deadline=esp_timer_get_time()+4000000;
    do {
        if(!atomic_load(&keep_running))return false;
        if(gallery_camera_prepare())return true;
        vTaskDelay(pdMS_TO_TICKS(20));
    }while(esp_timer_get_time()<deadline);
    return false;
}

static void camera_task(void *arg) {
    (void)arg;
    camera_log_task=xTaskGetCurrentTaskHandle();
    int fd=-1, kind=V4L2_BUF_TYPE_VIDEO_CAPTURE;
    bool streaming=false, captured=false, direct=false, buffers_requested=false;
    uint8_t *mapped[CAMERA_BUFFERS]={0};
    int displayed=-1;
    int applied_direction=3;
    size_t stride=PREVIEW_WIDTH*2;
    int step=1, error=ENOMEM;
    const char *reason="相机内存不足，请稍后重试";
    // Clearing the JPEG cache alone does not release an in-flight gallery
    // download, its TCP buffers, or the retained idle HTTP connection. Drain
    // those owners on this worker before judging heap headroom or allocating
    // the three CSI frames. LVGL remains responsive throughout the wait.
    mark(12,0);
    if(!camera_wait_for_gallery()) {
        if(!atomic_load(&keep_running))goto finish;
        step=12;error=EBUSY;reason="图库仍在结束，请稍后重试";goto failed;
    }
    mark(step,0);
    // Startup owns the three full-size allocations. Never try to recreate them
    // among attachment thumbnails or short-lived worker stacks during reopen.
    if(!frame_pool[CAMERA_BUFFERS-1])goto failed;
    if(!video_initialized) {
        if(video_init_failed){step=3;error=EIO;reason="相机初始化失败，请重启设备后重试";goto failed;}
        // Cold registration retains sensor/video/ISP resources for this boot.
        // Do not charge that headroom again on every reopen: a healthy existing
        // pipeline can have less free memory precisely because it owns them.
        // Reopens use the reserved frames; actual REQBUFS/STREAMON allocation
        // failures still follow their checked driver cleanup/error paths below.
        if(!camera_wait_for_startup_memory()) {
            if(!atomic_load(&keep_running))goto finish;
            goto failed;
        }
        step=2;mark(step,0);reason="摄像头时钟启动失败";
        error=bsp_cam_osc_init();if(error!=ESP_OK)goto failed;
        vTaskDelay(pdMS_TO_TICKS(10));
        esp_video_init_csi_config_t csi={
            .sccb_config={.init_sccb=false,.i2c_handle=bsp_i2c_get_handle(),.freq=400000},
            .reset_pin=-1,.pwdn_pin=-1
        };
        esp_video_init_config_t config={.csi=&csi,.dvp=NULL,.jpeg=NULL};
        step=3;mark(step,0);reason="相机初始化失败，请重启设备后重试";
        error=esp_video_init(&config);
        // esp_video 0.7 has no full deinit API for a partly registered pipeline.
        // Do not register duplicate devices on every tap after init failed.
        if(error!=ESP_OK){video_init_failed=true;goto failed;}
        video_initialized=true;
    }
    step=4;mark(step,0);reason="相机打开失败，请重新打开";
    fd=open(ESP_VIDEO_MIPI_CSI_DEVICE_NAME,O_RDONLY);
    if(fd<0){error=errno;goto failed;}
    struct v4l2_format format={0};
    format.type=kind;
    step=5;mark(step,0);reason="相机画面格式暂不支持";
    if(ioctl(fd,VIDIOC_G_FMT,&format)!=0){error=errno;goto failed;}
    format.fmt.pix.pixelformat=V4L2_PIX_FMT_RGB565;
    if(ioctl(fd,VIDIOC_S_FMT,&format)!=0||ioctl(fd,VIDIOC_G_FMT,&format)!=0){error=errno;goto failed;}
    if(format.fmt.pix.width!=PREVIEW_WIDTH||format.fmt.pix.height!=PREVIEW_HEIGHT||
        format.fmt.pix.pixelformat!=V4L2_PIX_FMT_RGB565){error=EINVAL;goto failed;}
    // esp_video 0.7.0 reports width/height but does not populate bytesperline.
    stride=format.fmt.pix.bytesperline?format.fmt.pix.bytesperline:PREVIEW_WIDTH*2;
    // lv_canvas_set_buffer uses a tightly packed RGB565 stride. Reject a future
    // incompatible driver format instead of interpreting padding as pixels.
    if(stride!=PREVIEW_WIDTH*2){error=EINVAL;goto failed;}
    struct v4l2_requestbuffers req={0};
    req.count=CAMERA_BUFFERS;req.type=kind;req.memory=V4L2_MEMORY_USERPTR;
    step=6;mark(step,0);reason="相机缓冲区不足，请重新打开";
    memcpy(mapped,frame_pool,sizeof(mapped));
    buffers_requested=true;
    if(ioctl(fd,VIDIOC_REQBUFS,&req)!=0){error=errno;goto failed;}
    if(req.count<CAMERA_BUFFERS){error=ENOMEM;goto failed;}
    for(unsigned i=0;i<CAMERA_BUFFERS;i++) {
        struct v4l2_buffer buffer={0};
        buffer.type=kind;buffer.memory=V4L2_MEMORY_USERPTR;buffer.index=i;
        if(ioctl(fd,VIDIOC_QUERYBUF,&buffer)!=0){error=errno;goto failed;}
        if(buffer.length!=PREVIEW_BYTES){error=EINVAL;goto failed;}
        buffer.m.userptr=(uintptr_t)mapped[i];
        if(ioctl(fd,VIDIOC_QBUF,&buffer)!=0){error=errno;goto failed;}
    }
    step=7;mark(step,0);reason="相机预览启动失败，请重新打开";
    if(ioctl(fd,VIDIOC_STREAMON,&kind)!=0){error=errno;goto failed;}
    streaming=true;
    bsp_display_lock(0);
    int direction=atomic_load(&requested_direction);
    if(apply_orientation&&apply_orientation(direction))applied_direction=direction;
    int viewport=camera_viewport_begin(preview_canvas);
    direct=viewport!=0;
    bsp_display_unlock();
    if(viewport<0){error=EIO;reason="相机显示同步失败，请重新打开";goto failed;}
    int64_t last_frame=esp_timer_get_time();
    while(atomic_load(&keep_running)) {
        step=8;mark(step,0);reason="未收到相机画面，请重新打开";
        esp_video_dqbuf_timeout_t request={.timeout_ms=250};
        struct v4l2_buffer *received=&request.buffer;
        received->type=kind;received->memory=V4L2_MEMORY_USERPTR;
        if(ioctl(fd,VIDIOC_DQBUF_TIMEOUT,&request)!=0) {
            error=errno;
            if(error==ETIMEDOUT&&esp_timer_get_time()-last_frame<3000000)continue;
            if(!atomic_load(&keep_running))break;
            goto failed;
        }
        struct v4l2_buffer buffer=request.buffer;
        buffer.type=kind;buffer.memory=V4L2_MEMORY_USERPTR;
        if(buffer.index>=CAMERA_BUFFERS||
           buffer.bytesused<stride*PREVIEW_HEIGHT){error=EINVAL;goto failed;}
        last_frame=esp_timer_get_time();
        if(!atomic_load(&keep_running))break;
        direction=atomic_load(&requested_direction);
        if(apply_orientation&&direction!=applied_direction) {
            // CSI still owns its queued frames. Release scanout before changing
            // display/touch coordinates, then rebuild only the small overlays.
            // A pending shutter request remains pending through this fence.
            bsp_display_lock(0);
            bool released=!direct||camera_viewport_end();
            if(released) {
                direct=false;
                if(apply_orientation(direction))applied_direction=direction;
                viewport=camera_viewport_begin(preview_canvas);
                direct=viewport!=0;
            }
            bsp_display_unlock();
            if(!released||viewport<0){error=EIO;reason="相机显示同步失败，请重新打开";goto failed;}
        }
        bool take=atomic_exchange(&capture_requested,false);
        // DQBUF transfers ownership to us. Keep this frame out of the driver
        // queue until it has been replaced under the same mutex used to draw.
        atomic_store(&prepare_ms,0);
        int previous=displayed;
        if(direct) {
            // Only our worker writes the inactive LCD buffer. CSI ownership is
            // retained through composition and the two-refresh presentation.
            if(!camera_viewport_frame((uint16_t *)mapped[buffer.index],camera_keep_frame,NULL)) {
                if(!atomic_load(&keep_running))break;
                error=EIO;reason="相机显示同步失败，请重新打开";goto failed;
            }
            displayed=(int)buffer.index;
        } else {
            int64_t lock_start=esp_timer_get_time();
            bsp_display_lock(0);
            atomic_store(&lock_wait_ms,elapsed_ms(lock_start));
            int64_t held_start=esp_timer_get_time();
            lv_canvas_set_buffer(preview_canvas,mapped[buffer.index],CAMERA_VIEW_WIDTH,CAMERA_VIEW_HEIGHT,LV_COLOR_FORMAT_RGB565);
            displayed=(int)buffer.index;
            atomic_store(&lock_hold_ms,elapsed_ms(held_start));
            bsp_display_unlock();
        }
        if(previous>=0) {
            struct v4l2_buffer released={.type=kind,.memory=V4L2_MEMORY_USERPTR,.index=(unsigned)previous,
                .length=PREVIEW_BYTES,.m.userptr=(uintptr_t)mapped[previous]};
            if(ioctl(fd,VIDIOC_QBUF,&released)!=0){error=errno;goto failed;}
        }
        atomic_fetch_add(&frames,1);
        int starting=CAMERA_PREVIEW_STARTING;
        atomic_compare_exchange_strong(&state,&starting,CAMERA_PREVIEW_LIVE);
        if(take){captured=true;break;}
        // Yield to touch/network tasks; sensor delivery and rendering provide
        // pacing. Do not add another fixed 50 ms after every frame.
        vTaskDelay(1);
    }
    goto finish;
failed:
    mark(step,error);
    snprintf(failure_text,sizeof(failure_text),"%s (%d/%d)",reason,step,error);
    ESP_LOGE("tab5_camera","stage=%d error=%d internal=%u largest=%u",step,error,
        (unsigned)heap_caps_get_free_size(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT),
        (unsigned)heap_caps_get_largest_free_block(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT));
    atomic_store(&state,CAMERA_PREVIEW_ERROR);
finish:
    // USERPTR storage belongs to this task. Stop DMA before transferring the
    // last displayed frame to the immutable still owner. Rotation reuses a
    // released pool frame; JPEG's lease continues to block reopening.
    if(streaming&&ioctl(fd,VIDIOC_STREAMOFF,&kind)!=0) {
        release_failed=true;captured=false;mark(11,errno);
        snprintf(failure_text,sizeof(failure_text),"相机关闭失败，请重启设备 (11/%d)",errno);
        atomic_store(&state,CAMERA_PREVIEW_ERROR);
    }
    if(displayed>=0&&!release_failed) {
        still_pixels=(uint16_t *)mapped[displayed];
        bsp_display_lock(0);
        lv_canvas_set_buffer(preview_canvas,still_pixels,CAMERA_VIEW_WIDTH,CAMERA_VIEW_HEIGHT,LV_COLOR_FORMAT_RGB565);
        bsp_display_unlock();
    }
    if(direct) {
        bsp_display_lock(0);
        bool restored=camera_viewport_end();
        bsp_display_unlock();
        if(!restored) {
            captured=false;mark(10,EIO);
            snprintf(failure_text,sizeof(failure_text),"显示同步未恢复，请重启设备 (10/%d)",EIO);
            atomic_store(&state,CAMERA_PREVIEW_ERROR);
        }
    }
    // ISP retains an open reference. Explicitly release its stopped metadata
    // pool. Our three USERPTR frames remain reserved for the next session.
    if(buffers_requested&&!release_failed) {
        struct v4l2_requestbuffers release={.count=0,.type=kind,.memory=V4L2_MEMORY_USERPTR};
        if(ioctl(fd,VIDIOC_REQBUFS,&release)!=0) {
            release_failed=true;
            captured=false;mark(11,errno);
            snprintf(failure_text,sizeof(failure_text),"相机缓冲区释放失败，请重启设备 (11/%d)",errno);
            atomic_store(&state,CAMERA_PREVIEW_ERROR);
        }
    }
    if(release_failed) {
        // Preserve explicit ownership of quarantined allocations and detach
        // the canvas. Neither retry nor a JPEG lease may reuse active storage.
        memcpy(quarantined_frames,mapped,sizeof(mapped));
        bsp_display_lock(0);lv_obj_add_flag(preview_canvas,LV_OBJ_FLAG_HIDDEN);bsp_display_unlock();
    }else {
        if(fd>=0)close(fd);
    }
    if(captured) {
        int pose=camera_pose_valid(applied_direction)?applied_direction:0;
        still_width=camera_pose_width(pose);still_height=camera_pose_height(pose);
        if(pose) {
            uint16_t *upright=(uint16_t *)mapped[(displayed+1)%CAMERA_BUFFERS];
            for(unsigned y=0;y<still_height;y++) {
                for(unsigned x=0;x<still_width;x++)upright[(size_t)y*still_width+x]=still_pixels[camera_pose_source(pose,x,y)];
                if((y&31)==31)vTaskDelay(1);
            }
            still_pixels=upright;
        }
        bsp_display_lock(0);
        lv_canvas_set_buffer(preview_canvas,still_pixels,still_width,still_height,LV_COLOR_FORMAT_RGB565);
        atomic_store(&state,CAMERA_PREVIEW_CAPTURED);
        if(apply_orientation)apply_orientation(pose);
        bsp_display_unlock();
        unsigned elapsed=(unsigned)(esp_timer_get_time()/1000)-atomic_load(&capture_started_ms);
        atomic_store(&capture_ms,elapsed>9999?9999:elapsed);
        atomic_store(&state,CAMERA_PREVIEW_CAPTURED);
    }
    if(atomic_load(&state)!=CAMERA_PREVIEW_CAPTURED&&atomic_load(&state)!=CAMERA_PREVIEW_ERROR)
        atomic_store(&state,CAMERA_PREVIEW_OFF);
    if(atomic_load(&state)!=CAMERA_PREVIEW_ERROR)mark(9,0);
    camera_log_task=NULL;
    atomic_store(&active,false);
    vTaskDeleteWithCaps(NULL);
}

bool camera_preview_start(lv_obj_t *canvas) {
    if(!canvas||atomic_exchange(&active,true))return false;
    if(release_failed){atomic_store(&active,false);return false;}
    // Called on LVGL: hide the previous still before DMA can reuse it. The
    // first newly dequeued frame will replace the canvas source under its lock.
    lv_obj_add_flag(canvas,LV_OBJ_FLAG_HIDDEN);
    gallery_cache_reclaim();
    portENTER_CRITICAL(&camera_log_lock);driver_error[0]=0;portEXIT_CRITICAL(&camera_log_lock);
    preview_canvas=canvas;
    failure_text[0]=0;atomic_store(&frames,0);
    atomic_store(&prepare_ms,0);atomic_store(&lock_wait_ms,0);atomic_store(&lock_hold_ms,0);atomic_store(&capture_ms,0);
    atomic_store(&capture_requested,false);
    atomic_store(&keep_running,true);
    atomic_store(&state,CAMERA_PREVIEW_STARTING);
    if(xTaskCreatePinnedToCoreWithCaps(camera_task,"tab5_camera",8192,NULL,4,NULL,0,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT)!=pdPASS) {
        mark(1,ENOMEM);snprintf(failure_text,sizeof(failure_text),"相机内存不足，请稍后重试 (1/%d)",ENOMEM);
        atomic_store(&state,CAMERA_PREVIEW_ERROR);
        atomic_store(&active,false);
        return false;
    }
    return true;
}
