#pragma once
#include "link_state.h"
#include <stddef.h>
#include <stdbool.h>
bool oneclick_start(const char *id,link_kind link,bool extended);
// 0 idle, 1 pending, 2 consumed; negative values are fixed non-secret causes:
// -1 local buffer, -2 configuration, -3 store, -4 screen, -5 consumed request,
// -6 transport/server, -7 invalid envelope, -8 deadline. Caller owns/wipes PIN.
int oneclick_take(char *pin,size_t capacity,size_t *length);
void oneclick_cancel(void);
