// A paired TAB5 sends the Doubao shortcut through the keyboard interface of
// its persistent CDC + HID composite USB device. The PHY never changes modes.
#include "tinyusb.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "esp_attr.h"

unsigned tab5_hid_magic,tab5_hid_stage,tab5_hid_error;
RTC_NOINIT_ATTR static uint32_t last_hid_magic,last_hid_stage;

void tab5_hid_mark(unsigned stage) {
    tab5_hid_stage=stage;
    last_hid_magic=0x48494436;
    last_hid_stage=stage;
}
unsigned tab5_hid_previous_stage(void) {
    return last_hid_magic==0x48494436?last_hid_stage:0;
}

static bool key(uint8_t modifier,uint8_t code) {
    uint8_t keys[6]={code};
    for(int i=0;i<50&&!tud_hid_ready();i++)vTaskDelay(pdMS_TO_TICKS(10));
    return tud_hid_ready()&&tud_hid_keyboard_report(0,modifier,keys);
}
static bool release_keys(void) {
    for(int i=0;i<20;i++) {
        if(key(0,0))return true;
        vTaskDelay(pdMS_TO_TICKS(20));
    }
    return false;
}

extern bool usb_keyboard_probe_begin(void);
extern void usb_keyboard_probe_end(void);
void tab5_hid_probe(int key_mode,bool auto_escape) {
    if(!usb_keyboard_probe_begin())return;
    tab5_hid_magic=0x48494435;
    tab5_hid_mark(1);
    tab5_hid_error=0;
    for(int i=0;i<100&&!tud_mounted();i++)vTaskDelay(pdMS_TO_TICKS(50));
    if(!tud_mounted()) {
        tab5_hid_error=32;
        tab5_hid_mark(9);
        usb_keyboard_probe_end();return;
    }
    tab5_hid_mark(4);
    if(key_mode) {
        vTaskDelay(pdMS_TO_TICKS(auto_escape?5000:250));
        tab5_hid_mark(5);
        if(key_mode==2) {
            if(!key(0,HID_KEY_F24))tab5_hid_error|=1;
            vTaskDelay(pdMS_TO_TICKS(80));
        } else {
            uint8_t modifier=key_mode==3?KEYBOARD_MODIFIER_RIGHTALT:
                key_mode==4?(KEYBOARD_MODIFIER_LEFTCTRL|KEYBOARD_MODIFIER_LEFTALT):KEYBOARD_MODIFIER_LEFTALT;
            if(!key(modifier,0))tab5_hid_error|=1;
            vTaskDelay(pdMS_TO_TICKS(80));
            if(!key(modifier,HID_KEY_SPACE))tab5_hid_error|=2;
            vTaskDelay(pdMS_TO_TICKS(80));
            if(!key(modifier,0))tab5_hid_error|=4;
            vTaskDelay(pdMS_TO_TICKS(80));
        }
        if(!release_keys())tab5_hid_error|=8;
        if(auto_escape) {
            vTaskDelay(pdMS_TO_TICKS(7000));
            key(0,HID_KEY_ESCAPE);
            vTaskDelay(pdMS_TO_TICKS(80));
            release_keys();
        }
    }
    tab5_hid_mark(9);usb_keyboard_probe_end();
}
