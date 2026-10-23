// Execute the actual camera task against an ownership-checking V4L2 mock and
// real LVGL canvas. This tests buffer lifetime, not sensor/interrupt throughput.
#undef NDEBUG
#include "camera_preview.h"
#include <assert.h>
#include <errno.h>
#include <stdarg.h>
#include <stdatomic.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#ifdef _WIN32
#include <malloc.h>
#include <windows.h>
#endif

#define RTC_NOINIT_ATTR
typedef int (*vprintf_like_t)(const char *,va_list);
typedef void *TaskHandle_t;
typedef int portMUX_TYPE;
#define portMUX_INITIALIZER_UNLOCKED 0
#define portENTER_CRITICAL(p) ((void)(p))
#define portEXIT_CRITICAL(p) ((void)(p))
static TaskHandle_t mock_task=(void *)1;
static bool mock_isr;
static bool xPortInIsrContext(void){return mock_isr;}
static TaskHandle_t xTaskGetCurrentTaskHandle(void){return mock_task;}
static char forwarded_log[256];
static int log_sink(const char *format,va_list args){return vsnprintf(forwarded_log,sizeof(forwarded_log),format,args);}
static vprintf_like_t installed_log=log_sink;
static vprintf_like_t esp_log_set_vprintf(vprintf_like_t cb){vprintf_like_t old=installed_log;installed_log=cb;return old;}
static void test_log(const char *format,...){va_list args;va_start(args,format);installed_log(format,args);va_end(args);}
#define ESP_OK 0
#define ESP_LOGE(...) ((void)0)
#define MALLOC_CAP_INTERNAL 1
#define MALLOC_CAP_8BIT 2
#define MALLOC_CAP_SPIRAM 4
#define MALLOC_CAP_CACHE_ALIGNED 8
#define O_RDONLY 0
#define PROT_READ 1
#define PROT_WRITE 2
#define MAP_SHARED 1
#define pdPASS 1
#define pdMS_TO_TICKS(x) (x)
#define ESP_VIDEO_MIPI_CSI_DEVICE_NAME "mock-camera"
#define V4L2_BUF_TYPE_VIDEO_CAPTURE 1
#define V4L2_MEMORY_MMAP 1
#define V4L2_MEMORY_USERPTR 2
#define V4L2_PIX_FMT_RGB565 565
enum {VIDIOC_G_FMT=1,VIDIOC_S_FMT,VIDIOC_REQBUFS,VIDIOC_QUERYBUF,VIDIOC_QBUF,
      VIDIOC_STREAMON,VIDIOC_STREAMOFF,VIDIOC_DQBUF_TIMEOUT};
struct v4l2_format {int type;struct {struct {unsigned width,height,pixelformat,bytesperline;} pix;} fmt;};
struct v4l2_requestbuffers {unsigned count;int type,memory;};
struct v4l2_buffer {unsigned index,length,bytesused;int type,memory;union {unsigned offset; uintptr_t userptr;} m;};
typedef struct {unsigned timeout_ms;struct v4l2_buffer buffer;} esp_video_dqbuf_timeout_t;
typedef struct {struct {bool init_sccb;void *i2c_handle;unsigned freq;} sccb_config;int reset_pin,pwdn_pin;} esp_video_init_csi_config_t;
typedef struct {esp_video_init_csi_config_t *csi;void *dvp,*jpeg;} esp_video_init_config_t;
enum {NORMAL,STOP,FAIL_DQ,FAIL_QBUF,FAIL_MAP,BAD_STRIDE,FAIL_START,FAIL_REQBUFS,LOW_MEMORY,FAIL_PRESENT,FAIL_RESTORE,FAIL_ALLOC_1,FAIL_ALLOC_2,FAIL_STREAM_ON,ROTATE,FAIL_ROTATE_RELEASE,FAIL_STOP_STREAM,FAIL_RELEASE};
static bool use_direct,direct_active;
static const uint16_t *presented_source;
static unsigned direct_presented;
static int scenario;
static unsigned pool_memory,frame_live,frame_peak,frame_allocations;
static unsigned startup_fail_call;
static bool fragment_heap;
static void *frame_storage[4];
static void frame_free(void *p);
static lv_obj_t *canvas;
static void (*scheduled_task)(void *);
static uint8_t *buffers[3];
static bool queued[3],streaming_mock,locked;
static unsigned delivered,returned,unmapped,closed,frame_copies,last_pixel;
static int capture_pose_test=3;
static unsigned cold_memory_case,cold_waits;
static uint16_t sensor_pixel(unsigned x,unsigned y) {
    return (uint16_t)(((x*7+y*3)&31)<<11|((x+y*5)&63)<<5|((x*11+y)&31));
}
static int64_t mock_time;
static unsigned rotation_calls,rotation_changes;
static int last_direction;
static bool rotation_deferred,rotation_release_failed;
static bool mock_apply_direction(int direction) {
    assert(locked&&!direct_active);
    rotation_calls++;
    if(scenario==ROTATE&&direction==2&&!rotation_deferred){rotation_deferred=true;return false;}
    if(direction!=last_direction)rotation_changes++;
    last_direction=direction;return true;
}
static const size_t frame_bytes=1280*720*2;
static uint8_t *canvas_pixels(void) {
    lv_draw_buf_t *b=lv_canvas_get_draw_buf(canvas);return b?b->data:NULL;
}
static bool canvas_uses_driver(void) {
    if(lv_obj_has_flag(canvas,LV_OBJ_FLAG_HIDDEN))return false;
    for(unsigned i=0;i<3;i++)if(buffers[i]&&canvas_pixels()==buffers[i])return true;
    return false;
}
static void verify_display_is_owned(void) {
    if(lv_obj_has_flag(canvas,LV_OBJ_FLAG_HIDDEN))return;
    for(unsigned i=0;i<3;i++)if(buffers[i]&&canvas_pixels()==buffers[i])assert(!queued[i]);
}
static unsigned heap_caps_get_free_size(int caps){
    if(cold_memory_case&&cold_memory_case!=2&&cold_waits<10)return 32000;
    if(scenario==LOW_MEMORY)return 8192;
    // .243 after successful encode/upload: 48,383 free internal bytes. The
    // registered video/sensor/ISP pipeline already owns its persistent memory.
    return fragment_heap&&caps==(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT)?48383:131072;
}
static unsigned heap_caps_get_largest_free_block(int caps){
    if(cold_memory_case==2&&cold_waits<10)return 8192;
    if(scenario==LOW_MEMORY)return 8192;
    return fragment_heap&&caps==(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT)?23552:131072;
}
static unsigned camera_viewport_diagnostic_largest(unsigned caps){return heap_caps_get_largest_free_block((int)caps);}
static void *frame_allocate(size_t size) {
    assert(size==frame_bytes);
    frame_allocations++;
    // Actual .241 failure: 11,640,296 free bytes but largest block 1,835,008.
    // Attachment add/remove leaves small owners between the freed frame holes.
    if((fragment_heap&&size>1835008)||frame_live==3 ||
       (startup_fail_call&&frame_allocations==startup_fail_call) || (scenario==FAIL_ALLOC_1&&frame_allocations==1) ||
       (scenario==FAIL_ALLOC_2&&frame_allocations==2))return NULL;
#ifdef _WIN32
    void *p=_aligned_malloc(size,128);
#else
    void *p=aligned_alloc(128,size);
#endif
    assert(p);for(unsigned i=0;i<4;i++)if(!frame_storage[i]){frame_storage[i]=p;break;}
    frame_live++;if(frame_live>frame_peak)frame_peak=frame_live;return p;
}
static void frame_free(void *p) {
    if(!p)return;
    assert(!canvas || lv_obj_has_flag(canvas,LV_OBJ_FLAG_HIDDEN) || canvas_pixels()!=p);
    unsigned i;for(i=0;i<4;i++)if(frame_storage[i]==p)break;assert(i<4&&frame_live);
    frame_storage[i]=NULL;frame_live--;
#ifdef _WIN32
    _aligned_free(p);
#else
    free(p);
#endif
}
static void *heap_caps_malloc(size_t size,int caps){(void)caps;return frame_allocate(size);}
static void *heap_caps_aligned_alloc(size_t align,size_t size,int caps) {
    assert(align==64&&caps==(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT|MALLOC_CAP_CACHE_ALIGNED));
    return frame_allocate(size);
}
static int bsp_cam_osc_init(void){return ESP_OK;}
static void *bsp_i2c_get_handle(void){return NULL;}
static unsigned video_init_calls;
static int esp_video_init(const esp_video_init_config_t *c){assert(c->csi);video_init_calls++;return ESP_OK;}
static int64_t esp_timer_get_time(void){return ++mock_time;}
static void bsp_display_lock(int timeout){(void)timeout;assert(!locked);verify_display_is_owned();locked=true;}
static void bsp_display_unlock(void){assert(locked);
    if(streaming_mock&&delivered&&!use_direct)lv_obj_remove_flag(canvas,LV_OBJ_FLAG_HIDDEN);
    verify_display_is_owned();locked=false;}
static int xTaskCreatePinnedToCoreWithCaps(void (*task)(void *),const char *name,unsigned stack,void *arg,
                                         unsigned priority,void *handle,int core,unsigned caps) {
    (void)name;(void)stack;(void)arg;(void)priority;(void)handle;(void)caps;assert(core==0);
    if(scenario==FAIL_START)return 0;scheduled_task=task;return pdPASS;
}
static void vTaskDeleteWithCaps(void *task){(void)task;assert(!locked&&(!streaming_mock||scenario==FAIL_STOP_STREAM));}
static void vTaskDelay(unsigned ticks) {
    mock_time+=(int64_t)ticks*1000;
    if(!streaming_mock&&cold_memory_case) {
        cold_waits++;assert(!locked);
        if(cold_memory_case==3&&cold_waits==3)camera_preview_stop();
    }
    if(!streaming_mock)return;
    if(scenario==ROTATE||scenario==FAIL_ROTATE_RELEASE) {
        if(delivered==3)camera_preview_request_orientation(2);
        if(delivered==6)camera_preview_request_orientation(0);
    }
    if(scenario==STOP&&delivered==4)camera_preview_stop();
    else if(delivered==8){assert(camera_preview_capture());assert(!camera_preview_capture());}
}
static int mock_open(const char *name,int flags){(void)flags;assert(!strcmp(name,ESP_VIDEO_MIPI_CSI_DEVICE_NAME));return 10;}
static int mock_ioctl(int fd,int request,void *arg) {
    assert(fd==10);
    switch(request) {
    case VIDIOC_G_FMT:{struct v4l2_format *f=arg;f->fmt.pix.width=1280;f->fmt.pix.height=720;
        f->fmt.pix.pixelformat=V4L2_PIX_FMT_RGB565;f->fmt.pix.bytesperline=scenario==BAD_STRIDE?2564:0;return 0;}
    case VIDIOC_S_FMT:return 0;
    case VIDIOC_REQBUFS:{struct v4l2_requestbuffers *r=arg;
        if(r->count==0) {
            assert(!streaming_mock&&!direct_active);
            if(scenario==FAIL_RELEASE){errno=EIO;return -1;}
            for(unsigned i=0;i<3;i++){
                if(pool_memory==V4L2_MEMORY_MMAP)frame_free(buffers[i]);
                buffers[i]=NULL;queued[i]=false;
            }
            return 0;
        }
        assert(r->count==3);pool_memory=r->memory;
        if(scenario==FAIL_REQBUFS){errno=ENOMEM;return -1;}
        if(pool_memory==V4L2_MEMORY_MMAP)for(unsigned i=0;i<3;i++) {
            buffers[i]=frame_allocate(frame_bytes);
            if(!buffers[i]){errno=ENOMEM;return -1;}
        }
        else assert(pool_memory==V4L2_MEMORY_USERPTR);
        return 0;}
    case VIDIOC_QUERYBUF:{struct v4l2_buffer *b=arg;assert(b->index<3);b->length=(unsigned)frame_bytes;b->m.offset=b->index;return 0;}
    case VIDIOC_QBUF:{struct v4l2_buffer *b=arg;unsigned i=b->index;assert(i<3&&!queued[i]);
        if(b->memory==V4L2_MEMORY_USERPTR) {
            assert(b->length==frame_bytes&&b->m.userptr&&b->m.userptr%64==0);
            if(buffers[i])assert(buffers[i]==(void *)(uintptr_t)b->m.userptr);
            buffers[i]=(void *)(uintptr_t)b->m.userptr;
            if(scenario==FAIL_MAP&&!delivered&&i==1){errno=ENOMEM;return -1;}
        }
        assert(lv_obj_has_flag(canvas,LV_OBJ_FLAG_HIDDEN)||canvas_pixels()!=buffers[i]);
        if(scenario==FAIL_QBUF&&delivered==4){errno=EIO;return -1;}
        queued[i]=true;returned++;return 0;}
    case VIDIOC_STREAMON:
        if(scenario==FAIL_STREAM_ON){errno=ENOMEM;return -1;}
        streaming_mock=true;return 0;
    case VIDIOC_STREAMOFF:
        if(scenario==FAIL_STOP_STREAM){errno=EIO;return -1;}
        streaming_mock=false;for(unsigned i=0;i<3;i++)queued[i]=false;return 0;
    case VIDIOC_DQBUF_TIMEOUT:{
        assert(streaming_mock&&!locked);verify_display_is_owned();
        if(scenario==FAIL_DQ&&delivered==4){errno=EIO;return -1;}
        // With the real driver's backup disabled, a completed buffer cannot
        // be delivered until another driver-owned buffer takes over capture.
        unsigned driver_owned=0;for(unsigned i=0;i<3;i++)if(queued[i])driver_owned++;
        assert(driver_owned>=2);
        unsigned chosen=3;
        for(unsigned offset=0;offset<3;offset++){unsigned i=(delivered+offset)%3;if(queued[i]){chosen=i;break;}}
        assert(chosen<3&&canvas_pixels()!=buffers[chosen]);queued[chosen]=false;
        delivered++;last_pixel=delivered;memset(buffers[chosen],(int)last_pixel,frame_bytes);
        if(capture_pose_test!=3)for(unsigned y=0;y<720;y++)for(unsigned x=0;x<1280;x++)
            ((uint16_t *)buffers[chosen])[y*1280+x]=sensor_pixel(x,y);
        assert(frame_copies==0); // No full-frame copy anywhere in the live loop.
        esp_video_dqbuf_timeout_t *r=arg;r->buffer.index=chosen;r->buffer.bytesused=(unsigned)frame_bytes;return 0;
    }
    default:assert(!"Unexpected camera ioctl");return -1;
    }
}
static void *mock_mmap(void *address,size_t length,int prot,int flags,int fd,unsigned offset) {
    (void)address;(void)prot;(void)flags;assert(fd==10&&length==frame_bytes&&offset<3);
    if(scenario==FAIL_MAP&&offset==1)return NULL;return buffers[offset];
}
static int mock_munmap(void *address,size_t length) {
    assert(!streaming_mock&&canvas_pixels()!=address&&length==frame_bytes);unmapped++;
    memset(address,0xdd,length);return 0;
}
static int mock_close(int fd) {
    assert(fd==10&&!streaming_mock);closed++;
    // The real ISP retains a reference. close must not be relied on to free
    // its pool: every successful/failing capture must already have released it.
    for(unsigned i=0;i<3;i++)assert(!buffers[i]);return 0;
}
static void observe_frame_copy(uint16_t *out,const uint8_t *source,size_t stride) {
    assert(!locked);
    assert(use_direct?(const uint8_t *)presented_source==source:canvas_pixels()==source);frame_copies++;
    camera_frame_view(out,source,stride);
}
static int camera_viewport_begin(lv_obj_t *c) {
    assert(locked&&c==canvas&&!direct_active);direct_active=use_direct;return use_direct?1:0;
}
static bool camera_viewport_frame(const uint16_t *source,bool (*keep)(void *),void *ctx) {
    assert(direct_active&&!locked&&!canvas_uses_driver());assert(keep(ctx));
    bool owned=false;
    for(unsigned i=0;i<3;i++)if(source==(uint16_t *)buffers[i]){owned=true;assert(!queued[i]);}
    assert(owned);
    if(scenario==FAIL_PRESENT&&delivered==4)return false;
    presented_source=source;direct_presented++;return true;
}
static bool camera_viewport_end(void) {
    assert(locked&&direct_active&&!canvas_uses_driver());
    if(scenario==FAIL_ROTATE_RELEASE&&delivered==4&&!rotation_release_failed){rotation_release_failed=true;return false;}
    direct_active=false;return scenario!=FAIL_RESTORE;
}
static void camera_viewport_diagnostics(unsigned *a,unsigned *b,unsigned *c,unsigned *d) {
    *a=direct_presented;*b=*c=0;*d=direct_active;
}
#define open mock_open
#define ioctl mock_ioctl
#define mmap mock_mmap
#define munmap mock_munmap
#define close mock_close
#define camera_frame_view observe_frame_copy
#include "../../firmware/main/gallery_remote.h"
#define free frame_free
#include "camera_preview_under_test.inc"
#undef free
#undef camera_frame_view
#define TAB5_GALLERY_CAMERA_POOL_TEST
#include "../../firmware/main/gallery_buffer.c"

static bool apply_capture_pose(int pose){assert(locked&&!direct_active);return pose==capture_pose_test;}
int main(void) {
#ifdef _WIN32
    SetErrorMode(SEM_FAILCRITICALERRORS|SEM_NOGPFAULTERRORBOX);
    _set_error_mode(_OUT_TO_STDERR);_set_abort_behavior(0,_WRITE_ABORT_MSG|_CALL_REPORTFAULT);
#endif
    camera_preview_init_diagnostics();camera_log_task=mock_task;
    test_log("I (10) csi: starting %d",1);assert(!driver_error[0]);
    mock_task=(void *)2;test_log("E (11) other: excluded");assert(!driver_error[0]);mock_task=(void *)1;
    mock_isr=true;test_log("E (12) ISR: excluded");assert(!driver_error[0]);mock_isr=false;
    test_log("E (13) tab5_camera: summary excluded");assert(!driver_error[0]);
    test_log("\033[0;31mE (14) csi: resource %d unavailable\033[0m",19);
    assert(strstr(driver_error,"resource 19 unavailable"));
    char first[160];snprintf(first,sizeof(first),"%s",driver_error);
    test_log("E (15) video: outer error");assert(!strcmp(driver_error,first)&&strstr(forwarded_log,"outer error"));
    char hex[320];camera_preview_driver_error_hex(hex,sizeof(hex));assert(strlen(hex)==strlen(first)*2);
    for(unsigned i=0;i<strlen(first);i++){unsigned byte;assert(sscanf(hex+i*2,"%2x",&byte)==1&&byte==(unsigned char)first[i]);}
    char bounded[4]={'X','X','X','Z'};camera_preview_driver_error_hex(bounded,3);assert(bounded[2]==0&&bounded[3]=='Z');
    camera_log_task=NULL;
    puts("CAMERA_LOG_OK first_driver_error_task_isolation_forwarding_bounded_hex");
#ifdef CAMERA_POOL_FIXED
    for(unsigned fault=1;fault<=3;fault++) {
        startup_fail_call=fault;frame_allocations=0;
        assert(!camera_preview_init_buffers()&&!frame_live);
        for(unsigned i=0;i<3;i++)assert(!frame_pool[i]);
    }
    startup_fail_call=0;frame_allocations=0;
    assert(camera_preview_init_buffers()&&frame_live==3);
    uint8_t *gallery_a=camera_gallery_buffer_take(),*gallery_b=camera_gallery_buffer_take();
    assert(gallery_a&&gallery_b&&gallery_a!=gallery_b&&!camera_gallery_buffer_take());
    assert(gallery_a!=(uint8_t *)still_pixels&&gallery_b!=(uint8_t *)still_pixels);
    assert(!camera_gallery_buffers_idle());
    memset(gallery_a,0xA5,1280*720*2);memset(gallery_b,0x5A,1280*720*2);
    assert(camera_gallery_buffer_release(gallery_a)&&camera_gallery_buffer_release(gallery_b));
    assert(camera_gallery_buffers_idle()&&!camera_gallery_buffer_release((uint8_t *)1));
    atomic_store(&active,true);assert(!camera_gallery_buffer_take());atomic_store(&active,false);
    release_failed=true;assert(!camera_gallery_buffer_take());release_failed=false;
    puts("CAMERA_GALLERY_POOL_OK two idle aligned frames, photograph excluded, active/quarantined denial, return fence");
    memset(still_pixels,0xCC,1280*720*2);
    for(unsigned pass=0;pass<64;pass++) {
        uint8_t *a=camera_gallery_buffer_take(),*b=camera_gallery_buffer_take();
        assert(a&&b&&a!=b);
        memset(a,0xA5,1280*720*2);memset(b,0x5A,1280*720*2);
        gallery_recycle(a);gallery_recycle(b);
        assert(!camera_gallery_buffers_idle());
        uint8_t *returned=camera_gallery_buffer_take();assert(returned==a);
        assert(camera_gallery_buffer_release(returned));
        gallery_release_buffer(gallery_take_buffer());
        assert(camera_gallery_buffers_idle()&&frame_live==3);
        for(unsigned byte=0;byte<1280*720*2;byte++)assert(((uint8_t *)still_pixels)[byte]==0xCC);
    }
    puts("CAMERA_GALLERY_HANDOFF_OK 64 real pool/spare recycle cycles; photograph intact, exactly three original frames");
    unsigned allocations=frame_allocations;
    assert(camera_preview_init_buffers()&&frame_allocations==allocations);
    const unsigned retained_frames=3;
#else
    const unsigned retained_frames=1;
#endif
    lv_init();lv_display_create(1280,720);
    for(unsigned pipeline=0;pipeline<2;pipeline++) {
    use_direct=pipeline!=0;
    for(int mode=NORMAL;mode<=FAIL_RELEASE;mode++) {
        if(!use_direct&&(mode==FAIL_PRESENT||mode==FAIL_RESTORE||mode==FAIL_ROTATE_RELEASE))continue;
#ifdef CAMERA_POOL_FIXED
        if(mode==FAIL_ALLOC_1||mode==FAIL_ALLOC_2)continue; // Tested at startup.
#endif
        // Reopen 64 times per display path; retain only one frame between runs.
        for(unsigned run=0;run<(mode==NORMAL?64u:1u);run++) {
            scenario=mode;frame_allocations=0;delivered=returned=unmapped=closed=frame_copies=last_pixel=direct_presented=0;
            fragment_heap=(mode==NORMAL&&run>0)||(mode!=NORMAL&&video_initialized);
            // Cold initialization still needs its original safety headroom.
            // A failed cold attempt must not enter the partially undoable init.
            if(mode==LOW_MEMORY)video_initialized=false;
            unsigned initializations=video_init_calls;
            rotation_calls=rotation_changes=0;last_direction=0;rotation_deferred=rotation_release_failed=false;
            camera_preview_set_orientation_handler(mode==ROTATE||mode==FAIL_ROTATE_RELEASE?mock_apply_direction:NULL);
            camera_preview_request_orientation(0);
            direct_active=false;presented_source=NULL;
            canvas=lv_canvas_create(lv_screen_active());scheduled_task=NULL;
            if(mode==FAIL_START){assert(!camera_preview_start(canvas)&&!camera_preview_busy());lv_obj_delete(canvas);continue;}
            assert(camera_preview_start(canvas)&&scheduled_task);assert(!camera_preview_start(canvas));
            scheduled_task(NULL);assert(!camera_preview_busy());
            if(mode==NORMAL||mode==ROTATE) {
                if(camera_preview_get_state()!=CAMERA_PREVIEW_CAPTURED) {
                    fprintf(stderr,"CAMERA_FRAME_BUDGET_FAILURE: three-frame limit; attempts=%u live=%u stage=%d error=%d\n",
                        frame_allocations,frame_live,atomic_load(&diagnostic_stage),atomic_load(&diagnostic_error));
                    fflush(stderr);
                }
                assert(camera_preview_get_state()==CAMERA_PREVIEW_CAPTURED&&delivered==9);
                if(mode==ROTATE)assert(rotation_calls==5&&rotation_changes==2&&last_direction==0&&rotation_deferred);
                if(run>0)assert(video_init_calls==initializations);
                assert(frame_copies==0&&unmapped==0&&closed==1&&frame_live==retained_frames&&frame_peak==3);
#ifdef CAMERA_POOL_FIXED
                assert(!frame_allocations); // No new full frame, even with attachments pinning holes.
#endif
                const uint8_t *copy=camera_preview_acquire();assert(copy&&camera_preview_busy());
                assert(!camera_preview_acquire()&&!camera_preview_start(canvas));
                // The displayed USERPTR frame survives pool release; preview
                // and attachment must still contain that exact captured frame.
                for(size_t i=0;i<frame_bytes;i++)assert(copy[i]==last_pixel&&canvas_pixels()[i]==last_pixel);
                camera_preview_stop();assert(camera_preview_busy());
                camera_preview_release(copy);assert(!camera_preview_busy());
                assert(camera_preview_acquire()==copy);camera_preview_release(copy);
            } else if(mode==STOP) {
                assert(camera_preview_get_state()==CAMERA_PREVIEW_OFF&&delivered==4&&frame_copies==0);
            } else {
                assert(camera_preview_get_state()==CAMERA_PREVIEW_ERROR);
                if(mode==LOW_MEMORY)assert(atomic_load(&diagnostic_stage)==1&&
                    atomic_load(&diagnostic_error)==ENOMEM&&video_init_calls==initializations);
                if(mode==FAIL_REQBUFS||mode==FAIL_STREAM_ON)assert(atomic_load(&diagnostic_error)==ENOMEM&&
                    atomic_load(&diagnostic_stage)==(mode==FAIL_REQBUFS?6:7)&&closed==1);
                if(mode==FAIL_ROTATE_RELEASE)assert(rotation_release_failed&&last_direction==0&&
                    rotation_calls==1&&closed==1&&atomic_load(&diagnostic_error)==EIO);
                assert(frame_copies==0);
            }
            if(mode!=NORMAL&&mode!=ROTATE)assert(!camera_preview_acquire()&&!camera_preview_busy());
            lv_obj_delete(canvas);canvas=NULL;assert(!locked);
#ifdef CAMERA_FRAME_CAPS
            if(mode==FAIL_STOP_STREAM||mode==FAIL_RELEASE) {
                assert(release_failed&&frame_live==3&&!closed);
                for(unsigned i=0;i<3;i++)assert(quarantined_frames[i]);
                lv_obj_t *retry=lv_canvas_create(lv_screen_active());
                assert(!camera_preview_start(retry));lv_obj_delete(retry);
                assert(!camera_preview_acquire()&&!camera_preview_busy());
                // Simulated hardware reset; production waits for real reboot.
                streaming_mock=false;
                for(unsigned i=0;i<3;i++){frame_free(quarantined_frames[i]);quarantined_frames[i]=NULL;buffers[i]=NULL;queued[i]=false;}
                still_pixels=NULL;release_failed=false;
#ifdef CAMERA_POOL_FIXED
                memset(frame_pool,0,sizeof(frame_pool));fragment_heap=false;
                assert(camera_preview_init_buffers()&&frame_live==3);
#endif
            }else
#endif
                assert(!streaming_mock&&frame_live==retained_frames);
        }
    }
    }
    for(cold_memory_case=1;cold_memory_case<=3;cold_memory_case++) {
        scenario=NORMAL;use_direct=true;fragment_heap=false;video_initialized=false;
        cold_waits=0;delivered=returned=closed=frame_copies=0;direct_active=false;
        unsigned before_init=video_init_calls;
        camera_preview_set_orientation_handler(NULL);camera_preview_request_orientation(0);
        canvas=lv_canvas_create(lv_screen_active());
        assert(camera_preview_start(canvas));scheduled_task(NULL);
        if(cold_memory_case==3) {
            assert(camera_preview_get_state()==CAMERA_PREVIEW_OFF&&cold_waits==3&&video_init_calls==before_init&&!delivered);
        }else {
            assert(camera_preview_get_state()==CAMERA_PREVIEW_CAPTURED&&cold_waits==11&&video_init_calls==before_init+1);
            const uint8_t *p=camera_preview_acquire();assert(p);camera_preview_release(p);
        }
        assert(!camera_preview_busy()&&!locked);lv_obj_delete(canvas);
    }
    cold_memory_case=0;
    puts("CAMERA_COLD_START_OK: transient free/largest-block pressure recovers on first open; cancellation responsive; persistent low-memory gate still rejects without initializing");
    int all_poses[]={0,2,1,-1};
    for(unsigned p=0;p<4;p++) {
        capture_pose_test=all_poses[p];scenario=NORMAL;use_direct=true;
        delivered=returned=unmapped=closed=frame_copies=last_pixel=direct_presented=0;
        frame_allocations=0;direct_active=false;presented_source=NULL;
        camera_preview_set_orientation_handler(apply_capture_pose);
        camera_preview_request_orientation(capture_pose_test);
        canvas=lv_canvas_create(lv_screen_active());scheduled_task=NULL;
        assert(camera_preview_start(canvas));scheduled_task(NULL);
        assert(camera_preview_get_state()==CAMERA_PREVIEW_CAPTURED&&!frame_allocations&&frame_live==3);
        unsigned w,h;camera_preview_dimensions(&w,&h);
        assert(w==(p<2?1280:720)&&h==(p<2?720:1280));
        const uint8_t *leased=camera_preview_acquire();assert(leased);
        // Independent source-coordinate oracle for all 921,600 saved pixels.
        // Deliberately do not call the production geometry/index helper.
        for(unsigned y=0;y<h;y++)for(unsigned x=0;x<w;x++) {
            unsigned sx=x,sy=y;
            if(capture_pose_test==2){sx=1279-x;sy=719-y;}
            if(capture_pose_test==1){sx=y;sy=719-x;}
            if(capture_pose_test==-1){sx=1279-y;sy=x;}
            assert(((const uint16_t *)leased)[y*w+x]==sensor_pixel(sx,sy));
        }
        assert(!camera_preview_start(canvas));camera_preview_release(leased);lv_obj_delete(canvas);
    }
#ifdef CAMERA_POOL_FIXED
    for(unsigned i=0;i<3;i++)frame_free(frame_pool[i]);
#else
    frame_free(still_pixels);
#endif
    assert(!frame_live);
    puts("CAMERA_OWNERSHIP_OK: 128 low-memory captures/reopens; live flip fences; four asymmetric saved frames match independent pixel oracle with no new frame allocation; immutable JPEG lease and teardown faults safe");
    return 0;
}
