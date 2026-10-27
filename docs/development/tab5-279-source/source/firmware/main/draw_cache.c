#include "draw_cache.h"
#include "src/core/lv_refr_private.h"
#include "src/core/lv_obj_draw_private.h"
#include "src/display/lv_display_private.h"
#include "src/draw/lv_draw_private.h"
#include "src/misc/lv_area_private.h"
#include <string.h>
#include <stdlib.h>
#include "../components/esp_lvgl_port/src/lvgl9/tab5_rotate_rgb565.h"
#ifdef ESP_PLATFORM
#include "esp_heap_caps.h"
#include "esp_timer.h"
#endif

// Retain all four main pages so rapid round trips do not evict a swipe target.
// Use explicit PSRAM allocation
// on the device so the optional cache cannot consume internal DMA memory.
#define CACHE_LIMIT (6u*1024u*1024u)
#define CACHE_ROWS 64
#define CACHE_SLOTS 4
typedef struct {
    lv_obj_t *obj;
    lv_draw_buf_t *pixels;
    lv_draw_buf_t storage;
    void *allocation;
    unsigned revision,captured_revision,stamp;
    int row;
    lv_area_t dirty,region;
    bool valid,has_image,has_dirty;
    uint8_t *native;
    bool native_valid,native_building;
} cache_entry;
static cache_entry entries[CACHE_SLOTS];
static unsigned stamp;
static bool capturing;
int (*tab5_draw_cache_interaction)(const lv_obj_t *);
static lv_draw_buf_t staging;
static void *staging_allocation;
static unsigned staging_size;
static cache_entry *staging_owner;
static uint8_t *staging_native;
lv_obj_t *tab5_draw_cache_native_strip;
static const void *native_pixels;
static lv_area_t native_area;
volatile unsigned tab5_native_cache_hits,tab5_native_cache_bytes;
volatile unsigned tab5_native_copy_total_us;
volatile unsigned tab5_cache_hits,tab5_cache_builds,tab5_cache_bytes;
volatile unsigned tab5_cache_snapshot_count,tab5_native_snapshot_count,tab5_cache_restart_count;
extern bool (*tab5_lvgl_cache_ready)(const lv_obj_t *);
extern bool (*tab5_lvgl_cache_draw)(lv_layer_t *,lv_obj_t *);
extern void (*tab5_lvgl_cache_dirty)(const lv_obj_t *,const lv_area_t *);

static bool eligible(const lv_obj_t *obj) {
    return obj&&!lv_obj_has_flag(obj,LV_OBJ_FLAG_HIDDEN)&&
        lv_obj_get_ext_draw_size(obj)==0&&
        lv_obj_get_style_bg_opa(obj,0)==LV_OPA_COVER&&
        lv_obj_get_style_opa(obj,0)==LV_OPA_COVER&&
        lv_obj_get_style_radius(obj,0)==0&&
        lv_obj_get_style_transform_rotation(obj,0)==0&&
        lv_obj_get_style_transform_scale_x(obj,0)==LV_SCALE_NONE&&
        lv_obj_get_style_transform_scale_y(obj,0)==LV_SCALE_NONE;
}
static cache_entry *find(const lv_obj_t *obj) {
    for(unsigned i=0;i<CACHE_SLOTS;i++)if(entries[i].obj==obj)return &entries[i];
    return NULL;
}
bool tab5_draw_cache_has_image(const lv_obj_t *obj) {
    cache_entry *e=find(obj);
    return !capturing&&e&&e->has_image&&eligible(obj)&&
        e->pixels->header.w==lv_obj_get_width(obj)&&e->pixels->header.h==lv_obj_get_height(obj);
}
bool tab5_draw_cache_is_ready(const lv_obj_t *obj) {
    cache_entry *e=find(obj);
    return tab5_draw_cache_has_image(obj)&&((e->valid&&e->revision==e->captured_revision)||
        (tab5_draw_cache_interaction&&tab5_draw_cache_interaction(obj)==2));
}
static void snapshot_counts(void) {
    unsigned images=0,native=0;
    for(unsigned i=0;i<CACHE_SLOTS;i++) {
        images+=entries[i].has_image;
        native+=entries[i].has_image&&entries[i].native_valid;
    }
    tab5_cache_snapshot_count=images;tab5_native_snapshot_count=native;
}
static void invalidate(const lv_obj_t *obj,const lv_area_t *area) {
    // Position changes of an ancestor only translate a cached subtree.
    // A content/style/scroll change on it or a descendant invalidates at once.
    for(const lv_obj_t *parent=obj;parent;parent=lv_obj_get_parent(parent)) {
        cache_entry *e=find(parent);
        if(e){
            lv_area_t bounds,dirty;lv_obj_get_coords(parent,&bounds);
            if(!lv_area_intersect(&dirty,&bounds,area))continue;
            lv_area_move(&dirty,-bounds.x1,-bounds.y1);
            if(e->has_dirty)lv_area_join(&e->dirty,&e->dirty,&dirty);else e->dirty=dirty;
            e->has_dirty=true;e->valid=false;e->revision++;
        }
    }
}
static bool draw(lv_layer_t *layer,lv_obj_t *obj) {
    if(!tab5_draw_cache_is_ready(obj))return false;
    cache_entry *e=find(obj);
    lv_draw_image_dsc_t image;lv_draw_image_dsc_init(&image);
    image.base.obj=obj;image.base.part=LV_PART_MAIN;
    image.src=e->pixels;image.opa=LV_OPA_COVER;
    lv_area_t area;lv_obj_get_coords(obj,&area);
    lv_draw_image(layer,&image,&area);
    e->stamp=++stamp;tab5_cache_hits++;
    return true;
}
static void deleted(lv_event_t *event) {
    cache_entry *e=find(lv_event_get_target(event));
    // Retain pixels until the next UI tick, after outstanding draw tasks end.
    if(e){e->obj=NULL;e->valid=false;}
}
static void free_pixels(cache_entry *e) {
    if(staging_owner==e)staging_owner=NULL;
    if(!e->pixels)return;
    if(e->native){tab5_native_cache_bytes-=e->pixels->data_size;free(e->native);e->native=NULL;}
    e->native_valid=e->native_building=false;
    tab5_cache_bytes-=e->pixels->data_size;
    lv_image_cache_drop(e->pixels);
    free(e->allocation);e->allocation=NULL;e->pixels=NULL;
    e->has_image=e->has_dirty=false;e->row=0;
    snapshot_counts();
}
static void release(cache_entry *e) {
    if(e->obj)lv_obj_remove_event_cb(e->obj,deleted);
    free_pixels(e);
    memset(e,0,sizeof(*e));
}
void tab5_draw_cache_drop(void) {
    for(unsigned i=0;i<CACHE_SLOTS;i++)release(&entries[i]);
    free(staging_allocation);staging_allocation=NULL;staging_size=0;staging_owner=NULL;
    free(staging_native);staging_native=NULL;native_pixels=NULL;
}
unsigned tab5_draw_cache_trim_hidden(unsigned bytes) {
    if(!bytes||capturing)return 0;
    for(unsigned i=0;i<CACHE_SLOTS;i++)if(entries[i].obj&&tab5_draw_cache_interaction&&tab5_draw_cache_interaction(entries[i].obj))return 0;
    lv_display_t *display=lv_display_get_default();
    lv_area_t viewport={0,0,lv_display_get_horizontal_resolution(display)-1,lv_display_get_vertical_resolution(display)-1};
    unsigned released=0;
    while(released<bytes) {
        cache_entry *oldest=NULL;
        for(unsigned i=0;i<CACHE_SLOTS;i++) {
            cache_entry *entry=&entries[i];lv_area_t bounds,intersection;
            if(!entry->pixels)continue;
            if(entry->obj&&lv_obj_is_visible(entry->obj)) {
                lv_obj_get_coords(entry->obj,&bounds);
                if(lv_area_intersect(&intersection,&bounds,&viewport))continue;
            }
            if(!oldest||entry->stamp<oldest->stamp)oldest=entry;
        }
        if(!oldest)break;
        unsigned before=tab5_cache_bytes+tab5_native_cache_bytes;release(oldest);
        released+=before-tab5_cache_bytes-tab5_native_cache_bytes;
    }
    return released;
}
// Called only by the UI task between render passes. Staging is unpublished;
// discard an incomplete repair, but keep every published/visible snapshot.
unsigned tab5_draw_cache_reclaim_staging(void) {
    if(capturing)return 0;
    for(unsigned i=0;i<CACHE_SLOTS;i++)
        if(entries[i].obj&&tab5_draw_cache_interaction&&tab5_draw_cache_interaction(entries[i].obj))return 0;
    unsigned released=(staging_allocation?staging_size:0)+(staging_native?staging_size:0);
    if(staging_owner){staging_owner->row=0;staging_owner->native_building=false;}
    staging_owner=NULL;
    lv_image_cache_drop(&staging);
    free(staging_allocation);free(staging_native);
    staging_allocation=NULL;staging_native=NULL;staging_size=0;
    memset(&staging,0,sizeof(staging));
    return released;
}
void tab5_draw_cache_init(void) {
    tab5_lvgl_cache_ready=tab5_draw_cache_is_ready;
    tab5_lvgl_cache_draw=draw;
    tab5_lvgl_cache_dirty=invalidate;
}
void tab5_draw_cache_repair(lv_obj_t *obj) {
    cache_entry *e=find(obj);
    if(e&&e->has_image&&!e->valid)tab5_draw_cache_step(obj);
}
void tab5_draw_cache_step(lv_obj_t *obj) {
    tab5_draw_cache_step_budget(obj,1280u*CACHE_ROWS);
}
static uint8_t *native_allocate(unsigned bytes) {
#ifdef ESP_PLATFORM
    // Optional acceleration must leave room for camera, audio and networking.
    if(heap_caps_get_free_size(MALLOC_CAP_SPIRAM)<bytes+6u*1024u*1024u)return NULL;
    return heap_caps_aligned_alloc(128,bytes,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
#else
    return malloc(bytes);
#endif
}
void tab5_draw_cache_step_budget(lv_obj_t *obj,unsigned pixels) {
    if(!pixels||(tab5_draw_cache_interaction&&tab5_draw_cache_interaction(obj)))return;
    if(!eligible(obj)||capturing)return;
    lv_obj_update_layout(obj);
    unsigned w=lv_obj_get_width(obj),h=lv_obj_get_height(obj);
    if(!w||!h||(uint64_t)w*h*2>CACHE_LIMIT)return;
    cache_entry *e=find(obj);
    if(!e) {
        e=&entries[0];
        for(unsigned i=1;i<CACHE_SLOTS;i++)
            if(e->obj&&(!entries[i].obj||entries[i].stamp<e->stamp))e=&entries[i];
        release(e);e->obj=obj;lv_obj_add_event_cb(obj,deleted,LV_EVENT_DELETE,NULL);
    }
    e->stamp=++stamp;
    if(tab5_draw_cache_is_ready(obj))return;
    if(e->pixels&&(e->pixels->header.w!=w||e->pixels->header.h!=h)) {
        free_pixels(e);
    }
    if(!e->pixels) {
        uint32_t stride=lv_draw_buf_width_to_stride(w,LV_COLOR_FORMAT_RGB565);
        uint32_t bytes=(stride*h+127u)&~127u;
        while(bytes>CACHE_LIMIT-tab5_cache_bytes) {
            cache_entry *oldest=NULL;
            for(unsigned i=0;i<CACHE_SLOTS;i++)
                if(&entries[i]!=e&&entries[i].pixels&&(!oldest||entries[i].stamp<oldest->stamp))oldest=&entries[i];
            if(!oldest)return;
            release(oldest);
        }
#ifdef ESP_PLATFORM
        e->allocation=heap_caps_aligned_alloc(128,bytes,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
        void *pixels=e->allocation;
#else
        e->allocation=malloc(bytes+127u);
        void *pixels=(void *)(((uintptr_t)e->allocation+127u)&~(uintptr_t)127u);
#endif
        if(!e->allocation)return; // Original rendering remains available.
        if(lv_draw_buf_init(&e->storage,w,h,LV_COLOR_FORMAT_RGB565,stride,pixels,bytes)!=LV_RESULT_OK) {
            free(e->allocation);e->allocation=NULL;return;
        }
        e->pixels=&e->storage;
        tab5_cache_bytes+=e->pixels->data_size;e->row=0;
    }
    // Keep the published image immutable. Incomplete idle repairs must never
    // displace a complete page while the user drags between tabs.
    if(staging_size!=e->pixels->data_size) {
        free(staging_native);staging_native=NULL;
        free(staging_allocation);staging_allocation=NULL;staging_size=0;staging_owner=NULL;
#ifdef ESP_PLATFORM
        staging_allocation=heap_caps_aligned_alloc(128,e->pixels->data_size,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
        void *memory=staging_allocation;
#else
        staging_allocation=malloc(e->pixels->data_size+127u);
        void *memory=(void *)(((uintptr_t)staging_allocation+127u)&~(uintptr_t)127u);
#endif
        if(!staging_allocation)return;
        staging_size=e->pixels->data_size;
        lv_draw_buf_init(&staging,w,h,LV_COLOR_FORMAT_RGB565,e->pixels->header.stride,memory,staging_size);
    }
    if(staging_owner!=e){
        void *memory=staging.data;
        if(lv_draw_buf_init(&staging,w,h,LV_COLOR_FORMAT_RGB565,e->pixels->header.stride,memory,staging_size)!=LV_RESULT_OK)return;
        staging_owner=e;e->row=0;
    }
    if(e->revision!=e->captured_revision){
        if(e->row)tab5_cache_restart_count++;
        e->row=0;e->captured_revision=e->revision;
    }
    if(e->row==0) {
        bool wanted=tab5_draw_cache_native_strip&&lv_obj_get_parent(obj)==tab5_draw_cache_native_strip&&!(w&1)&&!(h&1);
        if(wanted&&!e->native){e->native=native_allocate(e->pixels->data_size);if(e->native)tab5_native_cache_bytes+=e->pixels->data_size;}
        if(wanted&&e->native&&!staging_native)staging_native=native_allocate(staging_size);
        e->native_building=wanted&&e->native&&staging_native;
        if(e->native_building&&e->native_valid)memcpy(staging_native,e->native,e->pixels->data_size);
        if(e->has_image)memcpy(staging.data,e->pixels->data,e->pixels->data_size);
        e->region=e->has_image&&e->has_dirty?e->dirty:(lv_area_t){0,0,(int)w-1,(int)h-1};
        if(e->native_building&&!e->native_valid)e->region=(lv_area_t){0,0,(int)w-1,(int)h-1};
        lv_area_t bounds={0,0,(int)w-1,(int)h-1};
        if(!lv_area_intersect(&e->region,&e->region,&bounds))return;
    }
    // A full image uses 64-row bands. Dirty repairs have the same maximum
    // pixel budget, allowing a narrow button/label to finish in fewer ticks.
    // Never expose any incomplete revision.
    unsigned rows=pixels/(unsigned)lv_area_get_width(&e->region);
    if(!e->has_image&&rows>CACHE_ROWS)rows=CACHE_ROWS;
    if(rows<1)rows=1;
    lv_area_t area;lv_obj_get_coords(obj,&area);
    lv_layer_t layer;memset(&layer,0,sizeof(layer));
    layer.draw_buf=&staging;layer.buf_area=area;layer.color_format=LV_COLOR_FORMAT_RGB565;
    layer._clip_area=e->region;lv_area_move(&layer._clip_area,area.x1,area.y1);
    layer._clip_area.y1+=e->row;
    layer._clip_area.y2=LV_MIN(layer._clip_area.y2,layer._clip_area.y1+(int)rows-1);
    layer.phy_clip_area=layer._clip_area;
    lv_display_t *display=lv_obj_get_display(obj),*previous=lv_refr_get_disp_refreshing();
    lv_layer_t *old_layer=display->layer_head;display->layer_head=&layer;
    lv_refr_set_disp_refreshing(display);capturing=true;
    lv_obj_redraw(&layer,obj);
    while(layer.draw_task_head){lv_draw_dispatch_wait_for_request();lv_draw_dispatch();}
    capturing=false;display->layer_head=old_layer;lv_refr_set_disp_refreshing(previous);
    if(e->revision!=e->captured_revision){e->row=0;return;}
    if(e->native_building) {
        lv_area_t part=layer._clip_area;lv_area_move(&part,-area.x1,-area.y1);
        const void *src=staging.data+part.y1*staging.header.stride+part.x1*2;
        void *dst=staging_native+(w-1-part.x2)*h*2+part.y1*2;
        tab5_rotate_rgb565(src,dst,lv_area_get_width(&part),lv_area_get_height(&part),staging.header.stride,h*2,false);
    }
    e->row+=(int)rows;
    if(e->row>=lv_area_get_height(&e->region)) {
        lv_image_cache_drop(e->pixels);lv_image_cache_drop(&staging);
        lv_draw_buf_t previous=e->storage;e->storage=staging;staging=previous;
        void *allocation=e->allocation;e->allocation=staging_allocation;staging_allocation=allocation;
        staging_owner=NULL;
        if(e->native_building){uint8_t *old=e->native;e->native=staging_native;staging_native=old;}
        e->native_valid=e->native_building;
        e->valid=e->has_image=true;e->has_dirty=false;tab5_cache_builds++;
        snapshot_counts();
    }
}

bool tab5_draw_cache_take_native(const void *pixels,const lv_area_t *area) {
    bool match=pixels==native_pixels&&lv_area_is_equal(area,&native_area);
    if(match)native_pixels=NULL;
    return match;
}

bool tab5_draw_cache_native_render(lv_layer_t *layer) {
    native_pixels=NULL;
    lv_display_t *display=lv_refr_get_disp_refreshing();
    lv_obj_t *strip=tab5_draw_cache_native_strip;
    if(!strip||!display||display->render_mode!=LV_DISPLAY_RENDER_MODE_PARTIAL||
       layer->color_format!=LV_COLOR_FORMAT_RGB565||layer->draw_task_head||
       lv_display_get_rotation(display)!=LV_DISPLAY_ROTATION_90||display->offset_x||display->offset_y||
       lv_obj_get_child_count(lv_display_get_layer_top(display))||lv_obj_get_child_count(lv_display_get_layer_sys(display)))return false;
    const lv_area_t *area=&layer->buf_area;lv_area_t bounds;lv_obj_get_content_coords(strip,&bounds);
    if(!lv_area_is_equal(area,&layer->_clip_area)||!lv_area_is_in(area,&bounds,0))return false;
    int width=lv_area_get_width(area),height=lv_area_get_height(area);
    if((width&1)||(height&1)||(size_t)width*height*2>layer->draw_buf->data_size)return false;
    // Validate the whole area before touching the output; elastic gaps, missing
    // snapshots and non-main-page overlays fall back without partial writes.
    int covered=area->x1;
    cache_entry *parts[CACHE_SLOTS];lv_area_t positions[CACHE_SLOTS];unsigned count=0;
    for(uint32_t i=0;i<lv_obj_get_child_count(strip)&&covered<=area->x2;i++) {
        lv_obj_t *obj=lv_obj_get_child(strip,i);lv_area_t pos;lv_obj_get_coords(obj,&pos);
        if(pos.x2<covered||pos.x1>area->x2)continue;
        cache_entry *e=find(obj);
        // A fresh snapshot is also safe after the snap animation is removed.
        // is_ready permits stale pixels only during a real horizontal gesture;
        // changed content and press feedback otherwise use normal rendering.
        if(pos.x1>covered||pos.y1>area->y1||pos.y2<area->y2||!e||!e->native_valid||
           !tab5_draw_cache_is_ready(obj))return false;
        if(count==CACHE_SLOTS)return false;
        parts[count]=e;positions[count++]=pos;
        covered=pos.x2+1;
    }
    if(covered<=area->x2)return false;
#ifdef ESP_PLATFORM
    int64_t copy_start=esp_timer_get_time();
#endif
    for(unsigned i=0;i<count;i++) {
        lv_area_t pos=positions[i],part;
        if(!lv_area_intersect(&part,area,&pos))continue;
        cache_entry *e=parts[i];int h=lv_area_get_height(&pos),w=lv_area_get_width(&pos);
        // Walk both buffers forward in native orientation; reversed short
        // copies defeated sequential locality on the PSRAM path.
        const uint8_t *src=e->native+(w-1-(part.x2-pos.x1))*h*2+(area->y1-pos.y1)*2;
        uint8_t *dst=layer->draw_buf->data+(area->x2-part.x2)*height*2;
        if(height==h)memcpy(dst,src,(size_t)lv_area_get_width(&part)*height*2);
        else for(int x=part.x2;x>=part.x1;x--,src+=h*2,dst+=height*2) {
            memcpy(dst,src,(size_t)height*2);
        }
    }
#ifdef ESP_PLATFORM
    tab5_native_copy_total_us+=(unsigned)(esp_timer_get_time()-copy_start);
#endif
    native_pixels=layer->draw_buf->data;native_area=*area;tab5_native_cache_hits++;
    return true;
}
