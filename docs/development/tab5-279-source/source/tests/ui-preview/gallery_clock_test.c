#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <string.h>
#include <stdio.h>
#include <time.h>
#ifdef _WIN32
static struct tm *clock_gmtime_r(const time_t *t,struct tm *out){return gmtime_s(out,t)?NULL:out;}
#define gmtime_r clock_gmtime_r
#endif
#define TAB5_GALLERY_CLOCK_TEST
#define ESP_OK 0
#define pdPASS 1
#define portMUX_INITIALIZER_UNLOCKED 0
#define portENTER_CRITICAL(p) ((void)(p))
#define portEXIT_CRITICAL(p) ((void)(p))
#define pdMS_TO_TICKS(n) (n)
#define I2C_ADDR_BIT_LEN_7 7
typedef int portMUX_TYPE;
typedef void *i2c_master_dev_handle_t;
typedef struct {int dev_addr_length,device_address,scl_speed_hz;} i2c_device_config_t;
static uint8_t registers[64];
static int read_fail=-1,write_fail=-1,task_result=pdPASS,removed;
static int64_t test_ms;
static void *bsp_i2c_get_handle(void){return (void*)1;}
static int i2c_master_bus_add_device(void *bus,const i2c_device_config_t *cfg,void **out){assert(bus&&cfg->device_address==0x32&&cfg->scl_speed_hz==400000);*out=(void*)2;return 0;}
static int i2c_master_bus_rm_device(void *device){assert(device);removed++;return 0;}
static int i2c_master_transmit_receive(void *device,const uint8_t *reg,size_t count,uint8_t *out,size_t size,int timeout){assert(device&&count==1&&timeout==30&&*reg+size<=sizeof(registers));if(read_fail==*reg)return -1;memcpy(out,registers+*reg,size);return 0;}
static int i2c_master_transmit(void *device,const uint8_t *data,size_t size,int timeout){assert(device&&timeout==30&&data[0]+size-1<=sizeof(registers));if(write_fail==data[0])return -1;memcpy(registers+data[0],data+1,size-1);return 0;}
static int64_t esp_timer_get_time(void){return test_ms*1000;}
static void vTaskDelay(int ticks){(void)ticks;}
static int xTaskCreate(void(*task)(void*),const char *name,int stack,void *arg,int priority,void *handle){assert(task&&name&&stack==3072&&!arg&&priority==1&&!handle);return task_result;}
#include "../../firmware/main/gallery_clock.c"
int main(void) {
    gallery_clock_start();assert(rtc&&!removed);
    struct tm local;assert(!gallery_clock_local(0,&local));clock_poll();assert(!gallery_clock_local(0,&local));
    // Unclaimed / unset RTC cannot manufacture today's work or trust UTC as local.
    const int64_t instant=((int64_t)(countdown_civil_day(2026,10,14)-countdown_civil_day(1970,1,1))*86400+23*3600+59*60+59)*1000;
    uint8_t data[7];assert(gallery_clock_encode(instant,data));
    memcpy(registers+0x10,data,7);clock_poll();assert(!gallery_clock_local(0,&local));
    gallery_clock_sync(instant,0);clock_poll();assert(!memcmp(registers+0x20,marker,4));
    assert(registers[0x1f]==0); // Charging/backup policy is untouched.
    assert(gallery_clock_local(2000,&local)&&local.tm_year==126&&local.tm_mon==9&&local.tm_mday==15);
    // A fresh CPU boot keeps only the RTC, not the prior bridge anchor.
    wanted_valid=valid=false;test_ms=50;clock_poll();assert(gallery_clock_local(50,&local)&&local.tm_mday==14);
    int64_t decoded;assert(gallery_clock_decode(data,0,0,&decoded)&&decoded==instant);
    assert(!gallery_clock_decode(data,2,0,&decoded)&&!gallery_clock_decode(data,0,0x40,&decoded));
    uint8_t bad[7];memcpy(bad,data,7);bad[0]=0x6a;assert(!gallery_clock_decode(bad,0,0,&decoded));
    memcpy(bad,data,7);bad[3]=3;assert(!gallery_clock_decode(bad,0,0,&decoded));
    memcpy(bad,data,7);bad[4]=0x30;bad[5]=2;assert(!gallery_clock_decode(bad,0,0,&decoded));
    assert(gallery_clock_encode((int64_t)(countdown_civil_day(2024,2,29)-countdown_civil_day(1970,1,1))*86400000,data));
    assert(gallery_clock_decode(data,0,0,&decoded));
    assert(!gallery_clock_encode((int64_t)(countdown_civil_day(2100,1,1)-countdown_civil_day(1970,1,1))*86400000,data));
    // A failed date write always attempts to resume the oscillator.
    write_fail=0x10;assert(!set_clock(instant));assert(!(registers[0x1e]&0x40));write_fail=-1;
    // Bus errors preserve the already trusted monotonic clock, and may retry.
    read_fail=0x10;clock_poll();assert(gallery_clock_local(2000,&local));read_fail=-1;
    registers[0x1d]=2;wanted_valid=valid=false;clock_poll();assert(!gallery_clock_local(test_ms,&local));
    gallery_clock_sync(instant,test_ms);clock_poll();assert(!(registers[0x1d]&2)&&gallery_clock_local(test_ms,&local));
    task_result=0;gallery_clock_start();assert(!rtc&&removed==1);
    puts("GALLERY_CLOCK_OK claimed local RTC, cold CPU boot, midnight, leap day, invalid/voltage-loss/stopped RTC, malformed BCD, write failure resume, transient bus failure, task cleanup");
}
