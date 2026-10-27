#include <stdbool.h>
#include <stdint.h>
#include "sdkconfig.h"
#include "esp_attr.h"

extern bool __real_esp_ptr_executable(const void *p);
#if CONFIG_IDF_TARGET_ESP32P4 && CONFIG_SPIRAM_FETCH_INSTRUCTIONS
extern char _instruction_reserved_start[], _instruction_reserved_end[];
#endif

// IDF 5.4.2 excludes the P4 PSRAM instruction segment from its heap-address
// check. HTTP/pthread TLS cleanup callbacks live there with PSRAM XIP enabled
// and otherwise abort on task deletion (Espressif esp-idf issue #15997).
// Add only the linked instruction interval; preserve every other SDK check.
bool IRAM_ATTR __wrap_esp_ptr_executable(const void *p)
{
#if CONFIG_IDF_TARGET_ESP32P4 && CONFIG_SPIRAM_FETCH_INSTRUCTIONS
    uintptr_t address = (uintptr_t)p;
    if (address >= (uintptr_t)_instruction_reserved_start &&
        address < (uintptr_t)_instruction_reserved_end) return true;
#endif
    return __real_esp_ptr_executable(p);
}
