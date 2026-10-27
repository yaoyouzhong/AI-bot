#undef NDEBUG
#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <setjmp.h>
#include "../../firmware/main/battery_estimate.h"

// Keep assertions enabled and fail normally without a Windows crash dialog.
#undef assert
#define assert(value) do {if(!(value)){fprintf(stderr,"BATTERY_RECOVERY_ASSERT %s:%d: %s\n",__FILE__,__LINE__,#value);exit(1);}}while(0)

// Execute the production sampler and BSP charge controller against a fake I2C
// bus, including failures between a successful write and its readback.
typedef int esp_err_t;
typedef void *i2c_master_dev_handle_t;
typedef int portMUX_TYPE;
typedef struct { int dev_addr_length,device_address,scl_speed_hz; } i2c_device_config_t;
#define ESP_OK 0
#define ESP_FAIL -1
#define portMUX_INITIALIZER_UNLOCKED 0
#define portENTER_CRITICAL(x) ((void)(x))
#define portEXIT_CRITICAL(x) ((void)(x))
#define I2C_ADDR_BIT_LEN_7 0
#define I2C_MASTER_TIMEOUT_MS 30
#define PI4IO_REG_OUT_SET 5
#define setbit(x,n) ((x)|=(1U<<(n)))
#define clrbit(x,n) ((x)&=~(1U<<(n)))
#define pdMS_TO_TICKS(x) (x)
#define pdPASS 1
static void *const i2c_dev_handle_pi4ioe2=(void *)2;
static int64_t clock_us;
static int input_mv,input_ma,monitor_fail,config_fail,exp_read_fail,exp_write_fail;
static int fail_after_disable,fail_enable,ignore_disable,disable_count,enable_count;
static uint8_t outputs;
static bool stop_sample_task;
static jmp_buf sample_task_stop;
static int64_t esp_timer_get_time(void){return clock_us;}
static int bsp_i2c_get_handle(void){return 1;}
static void vTaskDelay(unsigned ms){(void)ms;if(stop_sample_task)longjmp(sample_task_stop,1);}
static int xTaskCreate(void (*f)(void *),const char *name,int stack,void *arg,int priority,void *handle)
{(void)f;(void)name;(void)stack;(void)arg;(void)priority;(void)handle;return pdPASS;}
static int i2c_master_bus_add_device(int bus,const i2c_device_config_t *config,void **handle)
{(void)bus;(void)config;*handle=(void *)1;return ESP_OK;}
static int i2c_master_bus_rm_device(void *handle){(void)handle;return ESP_OK;}
static int i2c_master_transmit_receive(void *handle,const uint8_t *request,int n,uint8_t *data,int len,int timeout) {
    (void)timeout;assert(n==1);
    if(handle==(void *)2) {
        assert(request[0]==5&&len==1);
        if(exp_read_fail){--exp_read_fail;return ESP_FAIL;}
        data[0]=outputs;return ESP_OK;
    }
    assert(handle==(void *)1&&len==2);
    if(monitor_fail){--monitor_fail;return ESP_FAIL;}
    uint16_t raw=request[0]==0?0x4127:request[0]==2?(uint16_t)(input_mv*4/5):(uint16_t)(int16_t)(-input_ma*2);
    data[0]=(uint8_t)(raw>>8);data[1]=(uint8_t)raw;return ESP_OK;
}
static int i2c_master_transmit(void *handle,const uint8_t *data,int len,int timeout) {
    (void)timeout;
    if(handle==(void *)1) {assert(len==3&&data[0]==0);if(config_fail){--config_fail;return ESP_FAIL;}return ESP_OK;}
    assert(handle==(void *)2&&len==2&&data[0]==5);
    if(exp_write_fail){--exp_write_fail;return ESP_FAIL;}
    bool enabled=!!(data[1]&0x80);
    if(enabled&&fail_enable){--fail_enable;return ESP_FAIL;}
    if(enabled)++enable_count;else ++disable_count;
    if(enabled||!ignore_disable)outputs=data[1];
    if(!enabled&&fail_after_disable){--fail_after_disable;exp_read_fail=1;}
    return ESP_OK;
}
#include "battery_charge_under_test.inc"
#include "battery_sampler_under_test.inc"

static battery_sampler reset(void) {
    clock_us=0;input_mv=8400;input_ma=1000;monitor=(void *)1;
    monitor_fail=config_fail=exp_read_fail=exp_write_fail=0;
    fail_after_disable=fail_enable=ignore_disable=disable_count=enable_count=0;
    outputs=0xb9;latest=(battery_status){0};sampled_at=0;
    return (battery_sampler){0};
}
static void tick(battery_sampler *s,int mv,int ma) {
    input_mv=mv;input_ma=ma;clock_us+=2000000;sample_once(s,clock_us);
}
static void short_tick(battery_sampler *s,int mv,int ma) {
    input_mv=mv;input_ma=ma;clock_us+=250000;sample_once(s,clock_us);
}
static int continuity_checks(void) {
#define CHECK_POWER(value,message) do {if(!(value)){printf("BATTERY_CONTINUITY_FAILED: %s\n",message);return 1;}}while(0)
    battery_sampler s=reset();tick(&s,7400,-300);tick(&s,8400,1000);
    int trusted=latest.percent;
    tick(&s,0,0);
    CHECK_POWER(battery_status_get().valid&&battery_status_get().percent==trusted,"one invalid conversion erased a trustworthy percentage");
    CHECK_POWER(s.retry_ms==250,"the next battery conversion should be scheduled within 250ms");
    short_tick(&s,8400,1000);
    CHECK_POWER(latest.valid&&latest.percent==trusted&&disable_count==0,"healthy recovery must not restart one-minute charge calibration");
    CHECK_POWER(s.retry_ms==2000,"healthy sampling should return to its normal low-rate interval");

    tick(&s,9500,0);short_tick(&s,9500,0);
    CHECK_POWER(!latest.valid&&!s.estimate.initialized,"confirmed invalid battery voltage must discard the old estimate");
    short_tick(&s,8400,1000);
    CHECK_POWER(latest.valid&&latest.percent==-1,"a replacement battery must not inherit the former battery percentage");

    s=reset();tick(&s,7400,-300);tick(&s,8400,1000);trusted=latest.percent;
    monitor_fail=1;tick(&s,8400,1000);
    CHECK_POWER(battery_status_get().valid&&battery_status_get().percent==trusted,"brief I2C loss should retain the last fresh percentage");
    short_tick(&s,8400,1000);
    CHECK_POWER(latest.valid&&latest.percent==trusted&&disable_count==0,"I2C recovery must preserve its trusted anchor");
    clock_us+=11000000;
    CHECK_POWER(!battery_status_get().valid,"stale data must remain unknown after ten seconds");
    short_tick(&s,8400,1000);
    CHECK_POWER(latest.valid&&latest.percent==-1,"long missing sampling must not reuse an unverified battery anchor");

    s=reset();tick(&s,7400,-300);
    for(int i=0;i<5;i++) {
        monitor_fail=1;short_tick(&s,7400,-300);
        CHECK_POWER(s.retry_ms==(i<4?250:2000),"persistent I2C failure must have a bounded fast retry burst");
    }
    short_tick(&s,7400,-300);
    CHECK_POWER(latest.valid&&latest.percent==50&&s.retry_ms==2000,"bus recovery should resume normal sampling immediately");

    s=reset();clock_us=1000;input_mv=7400;input_ma=-300;stop_sample_task=true;
    if(!setjmp(sample_task_stop))sample_task(NULL);
    stop_sample_task=false;
    CHECK_POWER(latest.valid&&latest.percent==50&&sampled_at==1000,"first sample must run before the initial task delay");
    puts("BATTERY_CONTINUITY_OK single_fault_no_blank retry_ms=250 bounded_burst=4 startup_delay_ms=0 confirmed_absence stale_boundary no_false_charge_anchor");
    return 0;
#undef CHECK_POWER
}
int main(void) {
    if(continuity_checks())return 1;
    battery_sampler s=reset();
    tick(&s,8400,1000);
    assert(battery_status_get().percent==-1&&s.paused&&!(outputs&0x80));
    // USB remains connected: no discharge current is required for automatic recovery.
    for(int i=0;i<29;i++){tick(&s,7400,0);assert(latest.percent==-1);}
    tick(&s,7400,0);
    assert(latest.percent==50&&!s.paused&&(outputs&0x80)&&enable_count==1);
    for(int i=0;i<100;i++)tick(&s,8400,1000);
    assert(latest.percent>=50&&latest.percent<54&&disable_count==1);
    assert((outputs&0x7f)==0x39); // No other expander pins changed.

    s=reset();tick(&s,7400,-300);tick(&s,8400,1000);
    assert(latest.percent==50&&disable_count==0); // Keep an existing trustworthy estimate.
    s=reset();tick(&s,8400,1000);
    for(int i=0;i<30;i++)tick(&s,7400,-300);
    assert(latest.percent==50&&!s.paused&&(outputs&0x80));

    s=reset();config_fail=1;tick(&s,8400,1000);
    assert(!s.configured&&!s.paused);tick(&s,8400,1000);assert(s.paused);
    monitor_fail=1;tick(&s,7400,0);
    assert(!s.paused&&(outputs&0x80));
    for(int i=0;i<6;i++){monitor_fail=1;tick(&s,7400,0);}
    assert(!battery_status_get().valid);
    tick(&s,8400,1000);assert(battery_status_get().valid&&latest.percent==-1);
    while(disable_count==1)tick(&s,8400,1000); // Retry after the cooldown.
    for(int i=0;i<30;i++)tick(&s,7400,0);
    assert(latest.percent==50&&!s.paused);

    s=reset();fail_after_disable=1;fail_enable=2;tick(&s,8400,1000);
    assert(s.restore_pending&&!(outputs&0x80));
    tick(&s,7400,0);assert(s.restore_pending);
    tick(&s,7400,0);assert(!s.restore_pending&&(outputs&0x80)&&latest.percent==-1);
    s=reset();ignore_disable=1;tick(&s,8400,1000);
    assert(!s.paused&&(outputs&0x80)&&latest.percent==-1); // Readback catches ignored writes.
    s=reset();exp_read_fail=1;tick(&s,8400,1000);
    assert(disable_count==0&&(outputs&0x7f)==0x39);
    s=reset();exp_write_fail=1;tick(&s,8400,1000);
    assert(!s.paused&&(outputs&0x80)&&latest.percent==-1);

    s=reset();tick(&s,8400,1000);
    for(int i=0;i<45;i++)tick(&s,8400,1000); // Current never falls: bounded pause, no invented level.
    assert(!s.paused&&(outputs&0x80)&&latest.percent==-1);
    s=reset();tick(&s,8400,1000);
    for(int i=0;i<44;i++)tick(&s,i%2?7600:7400,0);
    assert(latest.percent==-1);tick(&s,7400,0);assert(!s.paused&&(outputs&0x80));
    s=reset();tick(&s,8400,1000);tick(&s,0,0);
    assert(latest.percent==-1&&!s.paused&&(outputs&0x80));
    short_tick(&s,0,0);assert(!latest.valid&&!s.estimate.initialized);
    s=reset();tick(&s,6700,1000);assert(!s.paused&&disable_count==0);
    s=reset();tick(&s,8400,1000);
    for(int i=0;i<20;i++)tick(&s,7400,0);
    clock_us+=11000000;tick(&s,7400,0);
    for(int i=0;i<20;i++)tick(&s,7400,0);
    assert(latest.percent==-1&&!s.paused&&(outputs&0x80)); // Missing time is not rested sampling.
    puts("BATTERY_RECOVERY_OK usb_connected retained_estimate retry failures restore timeout no_false_charge_anchor");
}
