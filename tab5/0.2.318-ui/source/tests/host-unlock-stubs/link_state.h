#pragma once
#include <stdint.h>
typedef enum {LINK_NONE=-1,LINK_USB=0,LINK_WIFI=1,LINK_BLE=2} link_kind;
typedef struct {int mode;char session[3][40];int64_t received_ms[3];} link_state;
