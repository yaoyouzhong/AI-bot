#pragma once
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
typedef struct {
    bool connected,secure,subscribed,bridge_verified,ready,busy,finished,failed,pairing,repair_pairing;
    uint32_t comparison;
    unsigned connection;
} ble_keyboard_state;
void ble_keyboard_get(ble_keyboard_state *out);
bool ble_keyboard_start(const char *pin,size_t length,unsigned connection,const char *request_id);
bool ble_keyboard_wake(const char *request_id,unsigned connection);
bool ble_keyboard_display_wake(const char *request_id,unsigned connection);
void ble_keyboard_cancel(void);
void ble_keyboard_pair_confirm(bool accept);

// Read-only troubleshooting bits; never consulted to authorize a key report.
#include <stdatomic.h>
#include "unlock_history.h"
extern atomic_uint tab5_unlock_diagnostic;
// Last terminal failure, retained across modal close/success until reboot.
// One UI writer; bounded seqlock readers. No credentials or key events.
extern atomic_uint tab5_unlock_failure[3];
static inline void unlock_failure_record(unsigned packed,unsigned uptime_ms) {
    atomic_fetch_add(&tab5_unlock_failure[0],1u);
    atomic_store(&tab5_unlock_failure[1],packed);atomic_store(&tab5_unlock_failure[2],uptime_ms);
    atomic_fetch_add(&tab5_unlock_failure[0],1u);
}
static inline bool unlock_failure_snapshot(unsigned *packed,unsigned *uptime_ms) {
    for(int i=0;i<2;i++) {
        unsigned before=atomic_load(&tab5_unlock_failure[0]);if(before&1u)continue;
        unsigned p=atomic_load(&tab5_unlock_failure[1]),t=atomic_load(&tab5_unlock_failure[2]);
        if(before==atomic_load(&tab5_unlock_failure[0])){*packed=p;*uptime_ms=t;return true;}
    }
    *packed=*uptime_ms=0;return false;
}
unsigned ble_keyboard_diagnostic(void);
