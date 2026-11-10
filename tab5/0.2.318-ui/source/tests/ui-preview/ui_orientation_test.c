#undef NDEBUG
#include "../../firmware/main/ui_orientation.h"
#include "lvgl.h"
#include <assert.h>
#include <stdio.h>

static int sample(ui_orientation_filter *f,int x,int y,int z,int64_t ms,bool blocked,int mode) {
    return ui_orientation_sample(f,(art_orientation_state){.ready=true,.x=x,.y=y,.z=z,.sampled_ms=ms},ms,blocked,mode);
}
static lv_point_t raw;
static lv_indev_state_t state;
static unsigned clicks;
static void read_touch(lv_indev_t *i,lv_indev_data_t *d){(void)i;d->point=raw;d->state=state;}
static void click(lv_event_t *e){(void)e;clicks++;}
static void touch_test(void) {
    lv_init();lv_display_t *d=lv_display_create(720,1280);
    lv_indev_t *input=lv_indev_create();lv_indev_set_type(input,LV_INDEV_TYPE_POINTER);
    lv_indev_set_display(input,d);lv_indev_set_read_cb(input,read_touch);
    const lv_point_t points[]={{0,0},{1279,0},{0,719},{1279,719},{640,360},{1230,42}};
    for(int r=LV_DISPLAY_ROTATION_90;r<=LV_DISPLAY_ROTATION_270;r+=2) {
        lv_display_set_rotation(d,(lv_display_rotation_t)r);
        assert(lv_display_get_horizontal_resolution(d)==1280&&lv_display_get_vertical_resolution(d)==720);
        for(unsigned n=0;n<sizeof(points)/sizeof(points[0]);n++) {
            lv_point_t p=points[n];
            // Raw physical panel coordinates for the desired logical point.
            raw=r==LV_DISPLAY_ROTATION_90?(lv_point_t){p.y,1279-p.x}:(lv_point_t){719-p.y,p.x};
            lv_obj_t *button=lv_obj_create(lv_screen_active());lv_obj_remove_style_all(button);
            lv_obj_set_pos(button,p.x>20?p.x-20:0,p.y>20?p.y-20:0);lv_obj_set_size(button,41,41);
            lv_obj_add_event_cb(button,click,LV_EVENT_CLICKED,NULL);lv_obj_update_layout(button);
            state=LV_INDEV_STATE_PRESSED;lv_indev_read(input);
            lv_point_t got;lv_indev_get_point(input,&got);assert(got.x==p.x&&got.y==p.y);
            state=LV_INDEV_STATE_RELEASED;lv_indev_read(input);assert(clicks==n+1+(r==LV_DISPLAY_ROTATION_270?6:0));
            lv_obj_delete(button);
        }
    }
    lv_indev_delete(input);lv_display_delete(d);
    puts("LANDSCAPE_TOUCH_OK real LVGL 90/270: corners, center, top-right button coordinates and clicks");
}
int main(void) {
    ui_orientation_filter f={.candidate=3};
    for(int64_t t=100;t<600;t+=50)assert(sample(&f,-16384,0,0,t,false,0)==0);
    assert(sample(&f,-16384,0,0,600,false,0)==2);
    for(int64_t t=650;t<=2000;t+=50)assert(sample(&f,0,16384,0,t,false,0)==2); // portrait
    for(int64_t t=2050;t<=3000;t+=50)assert(sample(&f,0,0,16384,t,false,0)==2); // flat
    for(int64_t t=3050;t<=4000;t+=50)assert(sample(&f,30000,0,0,t,false,0)==2); // acceleration
    for(int64_t t=4050;t<=4600;t+=50)assert(sample(&f,16384,0,0,t,true,0)==2); // touch/camera
    for(int64_t t=4650;t<5150;t+=50)assert(sample(&f,16384,0,0,t,false,0)==2);
    assert(sample(&f,16384,0,0,5150,false,0)==0);
    for(int64_t t=5200;t<6000;t+=50)assert(sample(&f,(t/50)%2?16384:-16384,0,0,t,false,0)==0);
    assert(sample(&f,-16384,0,0,6000,false,0)==0);
    assert(sample(&f,-16384,0,0,6700,false,0)==0); // missing samples reset hold
    art_orientation_state a={.ready=true,.x=-16384,.sampled_ms=6700};
    assert(ui_orientation_sample(&f,a,8000,false,0)==0); // stale data
    a.ready=false;assert(ui_orientation_sample(&f,a,8000,false,0)==0);
    assert(sample(&f,16384,0,0,8050,false,2)==2); // lock ignores sensor
    assert(sample(&f,-16384,0,0,8100,false,1)==0);
    for(int64_t t=8150;t<8650;t+=50)assert(sample(&f,-16384,0,0,t,false,0)==0);
    assert(sample(&f,-16384,0,0,8650,false,0)==2);
    puts("LANDSCAPE_FILTER_OK stable 500ms, portrait/flat/movement ignored, touch hold, gaps, stale IMU, lock/unlock");
    touch_test();return 0;
}
