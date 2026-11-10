#include "gallery_clock.h"
#include "gallery_clock_codec.h"
#ifndef TAB5_GALLERY_CLOCK_TEST
#include "bsp/m5stack_tab5.h"
#include "driver/i2c_master.h"
#include "esp_timer.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#endif
#include <string.h>

static i2c_master_dev_handle_t rtc;
static portMUX_TYPE guard=portMUX_INITIALIZER_UNLOCKED;
static int64_t wanted,wanted_at,epoch,epoch_at;
static bool wanted_valid,valid;
// The RTC belongs to this local-time convention only after calibration. Do
// not interpret a different firmware's UTC RTC as local civil time.
static const uint8_t marker[4]={'A','I','L',1};
static bool read_regs(uint8_t reg,uint8_t *data,size_t size){return i2c_master_transmit_receive(rtc,&reg,1,data,size,30)==ESP_OK;}
static bool write_regs(uint8_t reg,const uint8_t *data,size_t size) {
    uint8_t packet[17];if(size>16)return false;
    packet[0]=reg;memcpy(packet+1,data,size);return i2c_master_transmit(rtc,packet,size+1,30)==ESP_OK;
}
static bool read_clock(int64_t *value) {
    uint8_t owned[4],data[7],state[2];
    return read_regs(0x20,owned,4)&&!memcmp(owned,marker,4)&&read_regs(0x1d,state,2)&&
        read_regs(0x10,data,7)&&gallery_clock_decode(data,state[0],state[1],value);
}
static bool set_clock(int64_t value) {
    uint8_t data[7],state[2];if(!gallery_clock_encode(value,data)||!read_regs(0x1d,state,2))return false;
    uint8_t stopped=(state[1]&0x3f)|0x40,running=state[1]&0x3f;
    if(!write_regs(0x1e,&stopped,1))return false;
    bool ok=write_regs(0x10,data,7);
    // Restore ticking on every exit, including a failed calendar write.
    bool resumed=write_regs(0x1e,&running,1);
    if(!ok||!resumed)return false;
    uint8_t flags=state[0]&~2u;
    return write_regs(0x1d,&flags,1)&&write_regs(0x20,marker,4);
}
void gallery_clock_sync(int64_t local_ms,int64_t now_ms) {
    uint8_t data[7];if(!gallery_clock_encode(local_ms,data))return;
    portENTER_CRITICAL(&guard);wanted=local_ms;wanted_at=now_ms;wanted_valid=true;
    epoch=local_ms;epoch_at=now_ms;valid=true;portEXIT_CRITICAL(&guard);
}
bool gallery_clock_local(int64_t now_ms,struct tm *out) {
    portENTER_CRITICAL(&guard);bool ready=valid&&now_ms>=epoch_at;
    int64_t current=epoch+now_ms-epoch_at;portEXIT_CRITICAL(&guard);
    time_t second=(time_t)(current/1000);
    return out&&ready&&gmtime_r(&second,out)&&out->tm_year>=100&&out->tm_year<=199;
}
static void clock_poll(void) {
        int64_t now=esp_timer_get_time()/1000,device_time=0;
        bool device_valid=read_clock(&device_time);
        portENTER_CRITICAL(&guard);bool sync=wanted_valid&&now>=wanted_at&&now-wanted_at<8000;int64_t desired=wanted+now-wanted_at;portEXIT_CRITICAL(&guard);
        // Reads/writes are bounded. Keep the last valid monotonic anchor on
        // transient bus errors; reject unset/stopped/voltage-loss RTC data.
        if(sync&&(!device_valid||desired-device_time>2000||device_time-desired>2000)) {
            if(set_clock(desired))device_valid=read_clock(&device_time);
        }
        if(device_valid&&!sync){portENTER_CRITICAL(&guard);epoch=device_time;epoch_at=now;valid=true;portEXIT_CRITICAL(&guard);}
}
static void clock_task(void *unused) {
    (void)unused;vTaskDelay(pdMS_TO_TICKS(300));
    for(;;){clock_poll();vTaskDelay(pdMS_TO_TICKS(1000));}
}
void gallery_clock_start(void) {
    i2c_device_config_t config={.dev_addr_length=I2C_ADDR_BIT_LEN_7,.device_address=0x32,.scl_speed_hz=400000};
    if(i2c_master_bus_add_device(bsp_i2c_get_handle(),&config,&rtc)!=ESP_OK)return;
    if(xTaskCreate(clock_task,"gallery_clock",3072,NULL,1,NULL)!=pdPASS){i2c_master_bus_rm_device(rtc);rtc=NULL;}
}
