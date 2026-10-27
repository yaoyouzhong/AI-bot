#include "art_orientation.h"
#include "art_orientation_filter.h"
#include "bmi270_maximum_fifo.h"
#include "bsp/m5stack_tab5.h"
#include "driver/i2c_master.h"
#include "esp_timer.h"
#include "esp_rom_sys.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include <string.h>
#include <stdio.h>

static i2c_master_dev_handle_t sensor;
static portMUX_TYPE guard=portMUX_INITIALIZER_UNLOCKED;
static art_orientation_state latest;
static BMI2_INTF_RETURN_TYPE transfer_result(esp_err_t error,uint8_t reg) {
    if(error!=ESP_OK){
        portENTER_CRITICAL(&guard);latest.i2c_error=error;latest.reg=reg;portEXIT_CRITICAL(&guard);
    }
    return error==ESP_OK?BMI2_OK:BMI2_E_COM_FAIL;
}
static BMI2_INTF_RETURN_TYPE read_sensor(uint8_t reg,uint8_t *data,uint32_t size,void *ctx) {
    (void)ctx;return transfer_result(i2c_master_transmit_receive(sensor,&reg,1,data,size,100),reg);
}
static BMI2_INTF_RETURN_TYPE write_sensor(uint8_t reg,const uint8_t *data,uint32_t size,void *ctx) {
    (void)ctx;uint8_t buffer[33];if(size>32)return transfer_result(ESP_ERR_INVALID_SIZE,reg);
    buffer[0]=reg;memcpy(buffer+1,data,size);
    return transfer_result(i2c_master_transmit(sensor,buffer,size+1,100),reg);
}
static void delay_sensor(uint32_t us,void *ctx) {
    (void)ctx;
    // RTOS delays can end one tick early. Bosch reset/config delays are minima.
    int64_t deadline=esp_timer_get_time()+us;
    if(us>=2000)vTaskDelay(pdMS_TO_TICKS(us/1000));
    int64_t remaining=deadline-esp_timer_get_time();
    if(remaining>0)esp_rom_delay_us((uint32_t)remaining);
}
static void set_stage(int stage) {
    portENTER_CRITICAL(&guard);latest.stage=stage;portEXIT_CRITICAL(&guard);
}
static int initialize_sensor(struct bmi2_dev *dev) {
    *dev=(struct bmi2_dev){.intf=BMI2_I2C_INTF,.read=read_sensor,.write=write_sensor,.delay_us=delay_sensor,.read_write_len=30};
    portENTER_CRITICAL(&guard);
    latest.ready=false;latest.i2c_error=0;latest.reg=0;latest.chip_id=0;latest.attempts++;latest.stage=1;
    portEXIT_CRITICAL(&guard);
    uint8_t id=0;
    int error=read_sensor(BMI2_CHIP_ID_ADDR,&id,1,NULL);
    portENTER_CRITICAL(&guard);latest.chip_id=id;portEXIT_CRITICAL(&guard);
    if(!error&&id!=BMI270_MAXIMUM_FIFO_CHIP_ID)error=BMI2_E_DEV_NOT_FOUND;
    if(!error){set_stage(2);error=bmi270_maximum_fifo_init(dev);}
    struct bmi2_sens_config config={.type=BMI2_ACCEL};
    if(!error){set_stage(3);error=bmi2_get_sensor_config(&config,1,dev);}
    if(!error){
        config.cfg.acc.odr=BMI2_ACC_ODR_50HZ;config.cfg.acc.range=BMI2_ACC_RANGE_2G;
        config.cfg.acc.bwp=BMI2_ACC_NORMAL_AVG4;config.cfg.acc.filter_perf=BMI2_PERF_OPT_MODE;
        set_stage(4);error=bmi2_set_sensor_config(&config,1,dev);
    }
    uint8_t accel=BMI2_ACCEL;
    if(!error){set_stage(5);error=bmi2_sensor_enable(&accel,1,dev);}
    portENTER_CRITICAL(&guard);latest.error=error;portEXIT_CRITICAL(&guard);
    return error;
}
static void sample_task(void *unused) {
    (void)unused;art_orientation_filter filter={.candidate=3};struct bmi2_dev dev;
    bool initialized=false;unsigned failures=0;
    // Start after display/expander startup has settled; never block the UI.
    vTaskDelay(pdMS_TO_TICKS(250));
    for(;;){
        if(!initialized){
            int error=initialize_sensor(&dev);
            if(error){vTaskDelay(pdMS_TO_TICKS(art_orientation_get().attempts<3?1000:30000));continue;}
            initialized=true;failures=0;filter.candidate=3;filter.last=0;
        }
        vTaskDelay(pdMS_TO_TICKS(50));struct bmi2_sens_data data={0};
        set_stage(6);int error=bmi2_get_sensor_data(&data,&dev);
        if(!error){
            int64_t now=esp_timer_get_time()/1000;failures=0;
            int orientation=art_orientation_sample(&filter,data.acc.x,data.acc.y,data.acc.z,now);
            portENTER_CRITICAL(&guard);
            latest.x=data.acc.x;latest.y=data.acc.y;latest.z=data.acc.z;latest.sampled_ms=now;
            latest.orientation=orientation;latest.ready=true;latest.error=0;latest.i2c_error=0;
            portEXIT_CRITICAL(&guard);
        } else {
            portENTER_CRITICAL(&guard);latest.error=error;latest.ready=false;portEXIT_CRITICAL(&guard);
            if(++failures>=10){initialized=false;vTaskDelay(pdMS_TO_TICKS(1000));}
        }
    }
}
void art_orientation_start(void) {
    i2c_device_config_t config={.dev_addr_length=I2C_ADDR_BIT_LEN_7,.device_address=0x68,.scl_speed_hz=400000};
    esp_err_t error=i2c_master_bus_add_device(bsp_i2c_get_handle(),&config,&sensor);
    if(error!=ESP_OK){transfer_result(error,0);return;}
    if(xTaskCreate(sample_task,"art_orientation",4096,NULL,1,NULL)!=pdPASS){
        i2c_master_bus_rm_device(sensor);sensor=NULL;transfer_result(ESP_ERR_NO_MEM,0);
    }
}
art_orientation_state art_orientation_get(void) {
    portENTER_CRITICAL(&guard);art_orientation_state state=latest;portEXIT_CRITICAL(&guard);
    if(!state.sampled_ms||esp_timer_get_time()/1000-state.sampled_ms>2000)state.ready=false;
    return state;
}
void art_orientation_diagnostics(char *out,size_t size) {
    art_orientation_state s=art_orientation_get();snprintf(out,size,"%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",s.ready,s.orientation,s.x,s.y,s.z,s.error,s.stage,s.i2c_error,s.reg,s.chip_id,s.attempts);
}
