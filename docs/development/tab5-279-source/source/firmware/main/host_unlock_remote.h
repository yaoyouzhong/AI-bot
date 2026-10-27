#pragma once
#include "link_state.h"
#include <stdbool.h>
typedef struct {bool busy,finished,unlocked,current;int error,stage;} host_unlock_state;
// Mac host-side intent only. No PIN, HID report, HTTP or transport fallback.
link_kind host_unlock_remote_link(void);
bool host_unlock_remote_start(const char *request_id,link_kind link);
void host_unlock_remote_cancel(void);
void host_unlock_remote_get(host_unlock_state *out);
