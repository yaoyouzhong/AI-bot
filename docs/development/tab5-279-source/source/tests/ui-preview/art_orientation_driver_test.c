#include <assert.h>
#include <setjmp.h>
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#include <string.h>
#include "art_orientation.h"
#include "art_orientation_filter.h"
#include "bmi270_maximum_fifo.h"

typedef int esp_err_t;
typedef int portMUX_TYPE;
typedef void *i2c_master_dev_handle_t;
typedef struct {int dev_addr_length,device_address,scl_speed_hz;} i2c_device_config_t;
#define portMUX_INITIALIZER_UNLOCKED 0
#define portENTER_CRITICAL(x) ((void)(x))
#define portEXIT_CRITICAL(x) ((void)(x))
#define ESP_OK 0
#define ESP_ERR_INVALID_SIZE 260
#define ESP_ERR_NO_MEM 257
#define I2C_ADDR_BIT_LEN_7 0
#define pdPASS 1
#define pdMS_TO_TICKS(x) (x)
static int64_t clock_us,reset_until;
static unsigned reads_failed,sample_reads,writes,resets;
static bool wrong_id,lose_samples;
static uint8_t registers[256];
static jmp_buf finished;
static int64_t esp_timer_get_time(void){return clock_us;}
static void esp_rom_delay_us(uint32_t us){clock_us+=us;}
static void vTaskDelay(unsigned ticks){
    // Worst tick phase: a requested 2 ms sleep returns after 1.001 ms.
    clock_us+=(int64_t)ticks*1000-999;
    if(sample_reads>=18||clock_us>65000000)longjmp(finished,1);
}
static esp_err_t i2c_master_transmit_receive(void *handle,const uint8_t *reg,size_t rn,uint8_t *out,size_t n,int timeout){
    (void)handle;assert(rn==1&&timeout==100);
    if(reads_failed){reads_failed--;return 263;}
    // A sensor still in soft reset rejects communication.
    if(clock_us<reset_until)return 263;
    if(*reg==BMI2_STATUS_ADDR){
        sample_reads++;
        if(lose_samples&&sample_reads>=3&&sample_reads<=12)return 263;
    }
    memcpy(out,registers+*reg,n);
    if(*reg==BMI2_CHIP_ID_ADDR)out[0]=wrong_id?0:0x24;
    if(*reg==BMI2_INTERNAL_STATUS_ADDR)out[0]=1;
    if(*reg==BMI2_STATUS_ADDR){memset(out,0,n);out[0]=0x80;out[BMI2_ACC_X_LSB_ADDR-BMI2_STATUS_ADDR+3]=0x40;}
    return ESP_OK;
}
static esp_err_t i2c_master_transmit(void *handle,const uint8_t *data,size_t n,int timeout){
    (void)handle;assert(timeout==100&&n<=33);writes++;
    if(data[0]==BMI2_CMD_REG_ADDR&&data[1]==BMI2_SOFT_RESET_CMD){reset_until=clock_us+2000;resets++;}
    if(data[0]!=BMI2_INIT_DATA_ADDR)memcpy(registers+data[0],data+1,n-1);
    return ESP_OK;
}
static void *bsp_i2c_get_handle(void){return (void *)1;}
static esp_err_t i2c_master_bus_add_device(void *bus,const i2c_device_config_t *cfg,void **out){
    (void)bus;assert(cfg->device_address==0x68);*out=(void *)2;return ESP_OK;
}
static esp_err_t i2c_master_bus_rm_device(void *dev){(void)dev;return ESP_OK;}
static int xTaskCreate(void (*fn)(void *),const char *name,unsigned size,void *arg,unsigned priority,void *task){
    (void)fn;(void)name;(void)size;(void)arg;(void)priority;(void)task;return pdPASS;
}
#include "art_orientation_driver_under_test.inc"

static void reset_test(void){
    latest=(art_orientation_state){0};clock_us=reset_until=0;reads_failed=sample_reads=writes=resets=0;
    wrong_id=lose_samples=false;memset(registers,0,sizeof(registers));registers[BMI2_ACC_CONF_ADDR]=0xa8;
    art_orientation_start();
}
int main(void){
    reset_test();reads_failed=3;
    if(!setjmp(finished))sample_task(NULL);
    fprintf(stderr,"startup ready=%d error=%d attempts=%d orientation=%d stage=%d samples=%u resets=%u\n",latest.ready,latest.error,latest.attempts,latest.orientation,latest.stage,sample_reads,resets);
    assert(latest.ready&&latest.error==0&&latest.attempts==4&&latest.orientation==1);
    assert(latest.chip_id==0x24&&latest.stage==6&&resets==1);
    assert(clock_us>32000000); // Backoff after third failed initialization.
    clock_us+=2100000;assert(!art_orientation_get().ready);
    reset_test();wrong_id=true;
    if(!setjmp(finished))sample_task(NULL);
    assert(!latest.ready&&latest.error==BMI2_E_DEV_NOT_FOUND&&writes==0);
    reset_test();lose_samples=true;
    if(!setjmp(finished))sample_task(NULL);
    assert(latest.ready&&latest.error==0&&latest.attempts==2&&resets==2);
    puts("ART_ORIENTATION_DRIVER_OK startup recovery, minimum reset delay, chip identity, runtime recovery, stale sample");
    return 0;
}
