#pragma once
#include "link_state.h"
#include <stdbool.h>
#include <stdint.h>
bool screen_preview_request(const char *stream,const char *request,const char *session,
                            link_kind link,int width,int height,int version,const char *encoding,int64_t deadline_ms);

// Read-only worker state; optional UI preheating yields while preview owns memory.
bool screen_preview_busy(void);

// Numeric worker completion/stack/allocation observations; no pixels or text.
void screen_preview_diagnostics(char *out,unsigned size);

// Reclaim only idle scratch; an active worker retains ownership.
// force is used for foreground voice/camera/OTA; otherwise expire after5s idle.
unsigned screen_preview_cache_reclaim(bool force);
