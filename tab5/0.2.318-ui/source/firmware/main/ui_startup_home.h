#pragma once
#include <stdbool.h>
#include <stdint.h>

// A transient landing page; never edits the saved display preferences.
typedef struct {bool holding,frame_seen;} ui_startup_home;
static inline ui_startup_home ui_startup_home_begin(void) {
    return (ui_startup_home){.holding=true};
}
static inline void ui_startup_home_snapshot(ui_startup_home *s,int page,int64_t now,int64_t *last_change) {
    if(s->frame_seen)return;
    s->frame_seen=true;
    if(s->holding&&page==0)*last_change=now;
}
static inline bool ui_startup_home_tick(ui_startup_home *s,int page,int selected,int interval,int64_t now,int64_t last_change,int *resume_fixed) {
    *resume_fixed=-1;
    if(!s->holding)return false;
    if(page!=0){s->holding=false;return false;} // Explicit navigation wins.
    if(now>=last_change&&now-last_change<(int64_t)interval*1000)return true;
    s->holding=false;*resume_fixed=selected;return false;
}
