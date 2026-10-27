#pragma once
#include <stdbool.h>
#include <stdint.h>
typedef struct {
    char id[37],name[97];
    bool folder,has_icon;
    uint8_t icon[32*32*4];
} computer_launcher;
typedef struct {
    bool lock_known,locked,one_click_unlock;
    bool busy,reading,ready,volume_available,muted,can_lock,media_available,can_play_pause,can_previous,can_next,playing;
    int volume,status,count,display_state;
    int64_t updated;
    int64_t lock_updated;
    int64_t display_updated;
    unsigned generation;
    unsigned diagnostic_operation;int diagnostic_status;int64_t diagnostic_started;
    computer_launcher launchers[4];
    char icon_revision[65];
    char message[160];
} computer_remote_state;
// Lightweight foreground reservation; no state allocation or transcript copies.
bool computer_remote_busy(void);
bool computer_remote_oneclick_enabled(void);
void computer_remote_diagnostic(unsigned *operation,int *status,int64_t *started);
void computer_remote_get(computer_remote_state *out);
// Lightweight lock snapshot, independent of ordinary controller availability.
// Freshness starts at request dispatch; failed unrelated reads cannot renew it.
bool computer_remote_lock_state(bool *locked,int64_t *updated);
// UI report only, expires after 4 seconds including RPC time. Never HID evidence.
bool computer_remote_screen_state(int *display);
// Result is tied to one modal's explicit read-only request. Expired, denied,
// unsupported and unknown results complete the check without permitting Space.
bool computer_remote_cover_state(const char *request_id,bool *cover);
// -1 unknown/stale, 0 off, 1 on, 2 dim. Request freshness includes RPC time.
bool computer_remote_display_state(const char *request_id,int *display);
// No retry or transport failover after sending a side-effecting operation.
bool computer_remote_start(const char *request_id,const char *operation,int value,const char *target);
