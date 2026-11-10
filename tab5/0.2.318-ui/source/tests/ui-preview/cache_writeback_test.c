// The runner extracts s_c2m_ops verbatim from the installed SDK esp_cache.c.
// Only interrupt, critical-section and cache-HAL boundaries are host facades.
// This checks operation sizes/order; it does not measure hardware latency.
#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include "cache_writeback_sdkconfig.h"

_Static_assert(CONFIG_ESP_MM_CACHE_MSYNC_C2M_CHUNKED_OPS == 1,
               "Test must use the enabled firmware C2M configuration");
_Static_assert(CONFIG_ESP_MM_CACHE_MSYNC_C2M_CHUNKED_OPS_MAX_LEN == 0x20000,
               "Test expects the selected P4 128 KiB limit");

enum { FRAME_BYTES = 720 * 1280 * 2, MAX_CALLS = 32 };
static unsigned s_spinlock;
static struct {
    bool in_isr;
    unsigned depth, enters, exits, writes, interrupt_windows;
    unsigned writes_at_enter;
    uint32_t base;
    size_t requested, covered, max_critical_bytes;
    uint32_t addresses[MAX_CALLS];
    size_t lengths[MAX_CALLS];
} observed;

static bool xPortInIsrContext(void)
{
    return observed.in_isr;
}

static void esp_os_enter_critical_safe(unsigned *lock)
{
    assert(lock == &s_spinlock);
    assert(observed.depth == 0);
    observed.depth = 1;
    observed.writes_at_enter = observed.writes;
    observed.enters++;
}

static bool cache_hal_writeback_addr(uint32_t address, size_t bytes)
{
    assert(observed.depth == 1);
    assert(observed.writes < MAX_CALLS);
    assert(observed.writes == observed.writes_at_enter);
    assert(address == observed.base + observed.covered);
    assert(observed.covered <= observed.requested);
    assert(bytes <= observed.requested - observed.covered);
    observed.addresses[observed.writes] = address;
    observed.lengths[observed.writes] = bytes;
    observed.writes++;
    observed.covered += bytes;
    if (bytes > observed.max_critical_bytes) observed.max_critical_bytes = bytes;
    return true;
}

static void esp_os_exit_critical_safe(unsigned *lock)
{
    assert(lock == &s_spinlock);
    assert(observed.depth == 1);
    assert(observed.writes == observed.writes_at_enter + 1);
    observed.depth = 0;
    observed.exits++;
    // A task has restored its interrupt state at every exit. ISR context does
    // not imply an opportunity for another handler at the same priority.
    if (!observed.in_isr) observed.interrupt_windows++;
}

#include "cache_writeback_under_test.inc"

static void exercise(const char *name, size_t bytes, bool in_isr,
                     unsigned expected_calls, size_t expected_largest)
{
    memset(&observed, 0, sizeof(observed));
    observed.base = 0x48000000;
    observed.requested = bytes;
    observed.in_isr = in_isr;
    s_c2m_ops(observed.base, bytes);
    assert(observed.depth == 0);
    assert(observed.enters == expected_calls);
    assert(observed.exits == expected_calls);
    assert(observed.writes == expected_calls);
    assert(observed.covered == bytes);
    assert(observed.max_critical_bytes == expected_largest);
    assert(observed.interrupt_windows == (in_isr ? 0 : expected_calls));
    if (!in_isr) {
        for (unsigned i = 0; i < observed.writes; i++) {
            assert(observed.lengths[i] > 0);
            assert(observed.lengths[i] <= CONFIG_ESP_MM_CACHE_MSYNC_C2M_CHUNKED_OPS_MAX_LEN);
        }
    }
    printf("PASS %s bytes=%zu calls=%u max_critical_bytes=%zu interrupt_windows=%u\n",
           name, bytes, observed.writes, observed.max_critical_bytes,
           observed.interrupt_windows);
}

int main(void)
{
    exercise("task empty", 0, false, 0, 0);
    exercise("task one byte", 1, false, 1, 1);
    exercise("task below boundary", 0x1ffff, false, 1, 0x1ffff);
    exercise("task exact boundary", 0x20000, false, 1, 0x20000);
    exercise("task over boundary", 0x20001, false, 2, 0x20000);
    exercise("task two exact chunks", 0x40000, false, 2, 0x20000);
    exercise("task full RGB565 frame", FRAME_BYTES, false, 15, 0x20000);
    for (unsigned i = 0; i < 14; i++) {
        assert(observed.addresses[i] == observed.base + i * 0x20000u);
        assert(observed.lengths[i] == 0x20000);
    }
    assert(observed.addresses[14] == observed.base + 14 * 0x20000u);
    assert(observed.lengths[14] == 8192);
    exercise("ISR below boundary fallback", 128, true, 1, 128);
    exercise("ISR full frame fallback", FRAME_BYTES, true, 1, FRAME_BYTES);
    puts("CACHE_WRITEBACK_OK 9 cases; exact SDK function; task frame=14*131072+8192");
    puts("LIMIT: ISR fallback remains one full critical operation; host checks do not prove hardware underrun cause or timing.");
    return 0;
}
