// Production ownership state, exercised against the IDF 5.4.2 ordering:
// ISR samples cur_fb_index -> starts that link list -> refresh callback.
// draw_bitmap writes cur_fb_index -> synchronous color callback.
// This models those external events, not a second ownership implementation.
#undef NDEBUG
#include "../../firmware/components/esp_lvgl_port/include/tab5_display_direct_state.h"
#include <assert.h>
#include <stdio.h>

static void refresh(tab5_display_direct_state_t *s)
{
    tab5_direct_refresh(s);
}

static tab5_display_direct_state_t started(void)
{
    tab5_display_direct_state_t s = {0};
    assert(tab5_direct_enter(&s));
    assert(tab5_direct_prepare(&s, 0));
    assert(!tab5_direct_color_done(&s));
    tab5_direct_arm(&s);
    refresh(&s); assert(!tab5_direct_complete(&s));
    refresh(&s); assert(tab5_direct_complete(&s));
    assert(tab5_direct_back(&s) == 1);
    return s;
}

static void ordinary_dma_ownership(void)
{
    tab5_display_direct_state_t s = {0};
    assert(!tab5_direct_color_done(&s));
    assert(tab5_direct_normal_submit(&s));
    assert(!tab5_direct_normal_submit(&s));
    // Begin times out before DMA completion: no state is discarded, and the
    // original LVGL wait still consumes the eventual completion exactly once.
    assert(!tab5_direct_enter(&s));
    assert(s.normal_dma == TAB5_DIRECT_DMA_WAITING);
    assert(!tab5_direct_normal_consume(&s));
    assert(tab5_direct_color_done(&s));
    assert(!tab5_direct_color_done(&s));
    assert(!tab5_direct_enter(&s));
    assert(tab5_direct_normal_consume(&s));
    assert(!tab5_direct_normal_consume(&s));
    assert(tab5_direct_enter(&s));
    assert(!tab5_direct_normal_submit(&s));
    assert(!tab5_direct_color_done(&s));
}

static void selection_interleavings(void)
{
    // All boundaries between ISR index read, selection write, callback and arm.
    // case 0: an old callback finishes before selection;
    // case 1: ISR sampled old, callback after selection but before arm;
    // case 2: ISR sampled old, callback after selection AND arm (critical race);
    // case 3: ISR samples new and callback before arm;
    // case 4: ISR samples new and callback after arm.
    for (unsigned order = 0; order < 5; order++) {
        tab5_display_direct_state_t s = started();
        unsigned selected = 0, scanning = 0, sampled = 0;
        assert(tab5_direct_prepare(&s, 1));
        assert(tab5_direct_back(&s) < 0);
        assert(!tab5_direct_normal_submit(&s));
        if (order <= 2) sampled = selected;
        if (order == 0) { scanning = sampled; refresh(&s); }
        selected = 1; // IDF draw_bitmap's cur_fb_index assignment
        assert(!tab5_direct_color_done(&s)); // synchronous, must not feed LVGL
        if (order >= 3) sampled = selected;
        if (order == 1 || order == 3) { scanning = sampled; refresh(&s); }
        tab5_direct_arm(&s); // post-return sequence snapshot
        assert(!tab5_direct_complete(&s));
        if (order == 2 || order == 4) { scanning = sampled; refresh(&s); }
        else { scanning = selected; refresh(&s); }
        assert(!tab5_direct_complete(&s));
        assert(tab5_direct_back(&s) < 0);
        scanning = selected; refresh(&s);
        assert(tab5_direct_complete(&s));
        assert(tab5_direct_back(&s) == 0);
        assert((unsigned)tab5_direct_back(&s) != scanning);
        assert(!tab5_direct_normal_submit(&s));
    }
}

static void timeout_and_recovery(void)
{
    for (unsigned received = 0; received < 2; received++) {
        tab5_display_direct_state_t s = started();
        assert(tab5_direct_prepare(&s, 1));
        tab5_direct_arm(&s);
        if (received) refresh(&s);
        assert(!tab5_direct_complete(&s));
        tab5_direct_fault(&s);
        // Neither a late interrupt nor a stale binary semaphore token grants
        // a writable buffer or restores ordinary DMA after the timeout.
        for (unsigned i = 0; i < 6; i++) {
            refresh(&s);
            assert(!tab5_direct_complete(&s));
            assert(tab5_direct_back(&s) < 0);
            assert(!tab5_direct_normal_submit(&s));
            assert(!tab5_direct_color_done(&s));
            assert(!tab5_direct_leave(&s));
        }
        assert(tab5_direct_prepare(&s, 0)); // end reselects canonical front
        assert(!tab5_direct_color_done(&s));
        tab5_direct_arm(&s);
        refresh(&s); assert(!tab5_direct_complete(&s));
        assert(!tab5_direct_leave(&s));
        refresh(&s); assert(tab5_direct_complete(&s));
        assert(tab5_direct_leave(&s));
        assert(tab5_direct_back(&s) < 0);
        assert(!tab5_direct_color_done(&s));
        assert(tab5_direct_normal_submit(&s));
        assert(tab5_direct_color_done(&s));
        assert(tab5_direct_normal_consume(&s));
    }
}

static void sequence_wrap_and_repeated_handoffs(void)
{
    tab5_display_direct_state_t s = started();
    s.refresh_sequence = UINT32_MAX;
    assert(tab5_direct_prepare(&s, 1));
    tab5_direct_arm(&s);
    refresh(&s); assert(s.refresh_sequence == 0);
    assert(!tab5_direct_complete(&s));
    refresh(&s); assert(tab5_direct_complete(&s));
    for (unsigned i = 0; i < 10000; i++) {
        int next = tab5_direct_back(&s);
        assert(next >= 0 && (unsigned)next != s.front);
        assert(tab5_direct_prepare(&s, (unsigned)next));
        // Arbitrarily many pre-return refreshes cannot satisfy the handoff.
        refresh(&s); refresh(&s); refresh(&s);
        tab5_direct_arm(&s);
        assert(!tab5_direct_complete(&s));
        refresh(&s); assert(!tab5_direct_complete(&s));
        refresh(&s); assert(tab5_direct_complete(&s));
        assert(s.front == (unsigned)next);
    }
}

static void static_freeze_ownership(void)
{
    tab5_display_direct_state_t s={0};
    assert(tab5_direct_normal_submit(&s));
    assert(!tab5_direct_freeze(&s));
    assert(tab5_direct_color_done(&s));
    assert(!tab5_direct_freeze(&s));
    assert(tab5_direct_normal_consume(&s));
    for(unsigned i=0;i<10000;i++) {
        unsigned before=s.refresh_sequence;
        assert(tab5_direct_freeze(&s));
        assert(s.mode==TAB5_DIRECT_FROZEN);
        assert(!tab5_direct_freeze(&s) && !tab5_direct_enter(&s));
        assert(!tab5_direct_prepare(&s,0) && !tab5_direct_prepare(&s,1));
        assert(tab5_direct_back(&s)<0 && !tab5_direct_normal_submit(&s));
        assert(!tab5_direct_color_done(&s) && !tab5_direct_leave(&s));
        refresh(&s);refresh(&s);
        assert(!tab5_direct_complete(&s) && s.refresh_sequence==before+2);
        assert(tab5_direct_unfreeze(&s) && s.mode==TAB5_DIRECT_NORMAL);
        assert(!tab5_direct_unfreeze(&s));
    }
    const tab5_direct_mode_t others[]={TAB5_DIRECT_CAMERA,TAB5_DIRECT_SWITCHING,TAB5_DIRECT_FAULT};
    for(unsigned i=0;i<3;i++){
        s.mode=others[i];assert(!tab5_direct_freeze(&s) && !tab5_direct_unfreeze(&s));
        assert(s.mode==others[i]);
    }
}

int main(void)
{
    static_freeze_ownership();
    ordinary_dma_ownership();
    selection_interleavings();
    timeout_and_recovery();
    sequence_wrap_and_repeated_handoffs();
    puts("DISPLAY_DIRECT_STATE_PASS: ISR interleavings, DMA ownership, timeout recovery, 10000 handoffs");
    return 0;
}
