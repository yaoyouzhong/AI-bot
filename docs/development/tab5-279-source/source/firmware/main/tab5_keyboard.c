#include "tab5_keyboard.h"
#include "bsp/m5stack_tab5.h"
#include "driver/i2c_master.h"
#include "esp_timer.h"
#include <string.h>

static i2c_master_dev_handle_t keyboard;
static int64_t next_probe;
static bool present;

static bool read_register(uint8_t reg, uint8_t *data, size_t length) {
    return keyboard && i2c_master_transmit_receive(keyboard, &reg, 1, data, length, 30) == ESP_OK;
}
static void probe(void) {
    int64_t now = esp_timer_get_time();
    if (now < next_probe) return;
    next_probe = now + 3000000;
    if (!keyboard) {
        if (bsp_ext_i2c_init() != ESP_OK) return;
        i2c_master_bus_handle_t bus = bsp_ext_i2c_get_handle();
        if (!bus || i2c_master_probe(bus, 0x6d, 30) != ESP_OK) return;
        i2c_device_config_t config = {.dev_addr_length = I2C_ADDR_BIT_LEN_7, .device_address = 0x6d,
                                       .scl_speed_hz = 100000};
        if (i2c_master_bus_add_device(bus, &config, &keyboard) != ESP_OK) return;
    }
    uint8_t mode[] = {0x10, 2};
    present = i2c_master_transmit(keyboard, mode, sizeof(mode), 30) == ESP_OK;
}
bool tab5_keyboard_present(void) { return present; }
bool tab5_keyboard_poll(char character[10], uint8_t *modifier) {
    if (!character || !modifier) return false;
    character[0] = 0; *modifier = 0;
    if (!present) probe();
    if (!present) return false;
    uint8_t count = 0, length = 0, event[10] = {0};
    if (!read_register(0x02, &count, 1)) { present = false; return false; }
    if (!count || !read_register(0x40, &length, 1) || !length) return false;
    if (length > 9 || !read_register(0x50, event, length + 1)) { present = false; return false; }
    *modifier = event[0];
    memcpy(character, event + 1, length);
    character[length] = 0;
    return true;
}
