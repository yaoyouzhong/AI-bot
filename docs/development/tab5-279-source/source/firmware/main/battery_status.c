#include "battery_status.h"
#include "battery_estimate.h"
#include "bsp/m5stack_tab5.h"
#include "driver/i2c_master.h"
#include "esp_timer.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

static portMUX_TYPE guard=portMUX_INITIALIZER_UNLOCKED;
static battery_status latest;
static int64_t sampled_at;
static i2c_master_dev_handle_t monitor;

static bool read_register(uint8_t reg,uint16_t *out) {
    uint8_t data[2];
    if(i2c_master_transmit_receive(monitor,&reg,1,data,2,30)!=ESP_OK)return false;
    *out=((uint16_t)data[0]<<8)|data[1];return true;
}
typedef struct {
    battery_estimate estimate;
    int filtered_mv, stable_mv;
    int64_t previous_at, pause_at, retry_at;
    unsigned quiet_ms, stable_ms, retry_ms, retry_failures;
    bool configured, paused, restore_pending, invalid_voltage;
} battery_sampler;

static void retry_soon(battery_sampler *s) {
    // Recover a missed conversion promptly without polling a failed bus forever.
    if(s->retry_failures<5)s->retry_failures++;
    s->retry_ms=s->retry_failures<=4?250:2000;
}

static bool configure_monitor(void) {
    uint16_t configuration;
    // Preserve reserved bits; 16 samples, 1.1 ms bus/shunt, continuous mode.
    if(read_register(0,&configuration)) {
        uint16_t value=(configuration&0x7000)|0x0527;
        uint8_t data[3]={0,(uint8_t)(value>>8),(uint8_t)value};
        return i2c_master_transmit(monitor,data,3,30)==ESP_OK;
    }
    return false;
}
static void restore_charge(battery_sampler *s,int64_t now) {
    // A failed write may still have reached the expander. Keep retrying enable
    // before any further sampling; never leave a failed calibration in charge-off.
    s->restore_pending=true;
    if(bsp_set_charge_en(true)!=ESP_OK)return;
    s->paused=false;s->restore_pending=false;s->retry_at=now+300000000;
    s->quiet_ms=0;s->stable_ms=0;
}
static void sample_once(battery_sampler *s,int64_t now) {
    s->retry_ms=2000;
    if(sampled_at&&now-sampled_at>10000000&&s->estimate.initialized) {
        // Missing readings cannot prove that the same battery is still fitted.
        if(s->paused)restore_charge(s,now);
        // Keep initialization history: a zero-current sample just after charge
        // restoration is not a new, rested cold-start voltage anchor.
        s->estimate=(battery_estimate){.initialized=true,.charging=s->estimate.charging};
        s->filtered_mv=0;s->previous_at=0;
        s->quiet_ms=0;s->stable_ms=0;s->invalid_voltage=false;
    }
    if(s->restore_pending || (s->paused && now-s->pause_at>=90000000)) {
        restore_charge(s,now);
        if(s->restore_pending){retry_soon(s);return;}
    }
    if(!s->configured) {
        s->configured=configure_monitor();
        if(!s->configured){retry_soon(s);return;}
    }
    uint16_t bus,shunt;
    if(!read_register(2,&bus)||!read_register(1,&shunt)) {
        if(s->paused)restore_charge(s,now);
        s->configured=false;s->previous_at=0;s->quiet_ms=0;s->stable_ms=0;s->invalid_voltage=false;
        s->estimate.discharge_ms=0;s->estimate.full_ms=0;
        retry_soon(s);
        return;
    }
    int mv=(int)bus*5/4;
    // 2.5 uV/LSB / 5 mOhm = 0.5 mA/LSB; charge is negative
    // on the TAB5 shunt. Reading shunt directly needs no calibration write.
    int ma=-(int16_t)shunt/2;
    bool valid=mv>=5000&&mv<=9000;
    int percent=-1;
    if(valid) {
        s->retry_failures=0;s->invalid_voltage=false;
        s->filtered_mv=s->filtered_mv?(s->filtered_mv*3+mv)/4:mv;
        unsigned elapsed=s->previous_at?(unsigned)((now-s->previous_at)/1000):0;
        percent=battery_estimate_sample(&s->estimate,s->filtered_mv,ma,elapsed);
        s->previous_at=now;
        if(s->paused) {
            if(!elapsed||elapsed>10000||ma>5) {
                s->quiet_ms=0;s->stable_ms=0;s->stable_mv=mv;
            } else {
                s->quiet_ms+=elapsed;
                if(mv<s->stable_mv-40||mv>s->stable_mv+40) {
                    s->stable_mv=mv;s->stable_ms=0;
                } else s->stable_ms+=elapsed;
            }
            // USB can power the system with essentially zero battery current.
            // Only a verified charge-off interval may anchor at zero current.
            if(!s->estimate.anchored&&s->quiet_ms>=60000&&s->stable_ms>=20000) {
                s->estimate.percent=(float)battery_percent_from_mv(s->filtered_mv);
                s->estimate.anchored=true;percent=(int)s->estimate.percent;
            }
            if(s->estimate.anchored)restore_charge(s,now);
        } else if(!s->estimate.anchored&&s->estimate.charging&&mv>=6800&&now>=s->retry_at) {
            // CHG_EN only controls the charger; it does not switch USB/system
            // power. Pause once to obtain a rested estimate without unplugging.
            s->paused=true;s->pause_at=now;s->quiet_ms=0;s->stable_ms=0;s->stable_mv=mv;
            if(bsp_set_charge_en(false)!=ESP_OK)restore_charge(s,now);
        }
    } else {
        if(s->paused)restore_charge(s,now);
        s->previous_at=0;s->quiet_ms=0;s->stable_ms=0;
        s->estimate.discharge_ms=0;s->estimate.full_ms=0;
        retry_soon(s);
        // One bad conversion must not erase a recent, trusted charge anchor.
        // Confirm absence/out-of-range voltage on the next read before clearing.
        if(!s->invalid_voltage){s->invalid_voltage=true;return;}
        s->filtered_mv=0;s->previous_at=0;s->estimate=(battery_estimate){0};
    }
    battery_status next={valid,valid&&s->estimate.charging,percent,mv,ma};
    portENTER_CRITICAL(&guard);latest=next;sampled_at=now;portEXIT_CRITICAL(&guard);
}
static void sample_task(void *unused) {
    (void)unused;
    battery_sampler sampler={0};
    for(;;) {
        sample_once(&sampler,esp_timer_get_time());
        vTaskDelay(pdMS_TO_TICKS(sampler.retry_ms));
    }
}
void battery_status_start(void) {
    i2c_device_config_t config={.dev_addr_length=I2C_ADDR_BIT_LEN_7,.device_address=0x41,.scl_speed_hz=400000};
    if(i2c_master_bus_add_device(bsp_i2c_get_handle(),&config,&monitor)!=ESP_OK)return;
    if(xTaskCreate(sample_task,"battery",3072,NULL,1,NULL)!=pdPASS){i2c_master_bus_rm_device(monitor);monitor=NULL;}
}
battery_status battery_status_get(void) {
    portENTER_CRITICAL(&guard);battery_status result=latest;int64_t at=sampled_at;portEXIT_CRITICAL(&guard);
    if(!at||esp_timer_get_time()-at>10000000)result.valid=false;
    return result;
}
