#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>

#define IRAM_ATTR
static char code[64];
static char *_instruction_reserved_start = code;
static char *_instruction_reserved_end = code + sizeof(code);
static unsigned sdk_calls;
// Model the pinned SDK's rejection of PSRAM text and its existing ranges.
static bool __real_esp_ptr_executable(const void *p)
{
    sdk_calls++;
    return p == (const void *)(uintptr_t)0x4ff0751e;
}
#include "executable_guard_under_test.inc"

int main(void)
{
    assert(!__real_esp_ptr_executable(code + 32));
    sdk_calls = 0;
    assert(__wrap_esp_ptr_executable(code) == !!CONFIG_SPIRAM_FETCH_INSTRUCTIONS);
    assert(__wrap_esp_ptr_executable(code + 32) == !!CONFIG_SPIRAM_FETCH_INSTRUCTIONS);
    assert(__wrap_esp_ptr_executable(code + 63) == !!CONFIG_SPIRAM_FETCH_INSTRUCTIONS);
    assert(sdk_calls == (CONFIG_SPIRAM_FETCH_INSTRUCTIONS ? 0 : 3));
    // Exclude end, before start, unrelated PSRAM/data, and NULL. These must
    // retain SDK classification instead of expanding to the entire PSRAM.
    unsigned before = sdk_calls;
    assert(!__wrap_esp_ptr_executable(code + sizeof(code)));
    assert(!__wrap_esp_ptr_executable((void *)((uintptr_t)code - 1)));
    assert(!__wrap_esp_ptr_executable((void *)(uintptr_t)0x48200000));
    assert(!__wrap_esp_ptr_executable(NULL));
    assert(__wrap_esp_ptr_executable((void *)(uintptr_t)0x4ff0751e));
    assert(sdk_calls == before + 5);
    printf("EXECUTABLE_GUARD_OK XIP=%d text-boundaries and SDK delegation\n", CONFIG_SPIRAM_FETCH_INSTRUCTIONS);
    return 0;
}
