#pragma once
#include <stdbool.h>
#include <stdint.h>
typedef enum { LINK_NONE = -1, LINK_USB = 0, LINK_WIFI = 1, LINK_BLE = 2 } link_kind;
typedef struct { int64_t received_ms[3]; uint64_t sequence[3]; char session[3][40]; int mode; } link_state;
void link_state_init(link_state *s);
bool link_state_receive(link_state *s, link_kind link, const char *session, uint64_t seq, int64_t now);
// Invalidate availability without erasing anti-replay session/sequence state.
void link_state_disconnect(link_state *s,link_kind link);
link_kind link_state_active(const link_state *s, int64_t now);
bool link_state_read_fresh(const link_state *s,link_kind link,int64_t now,int64_t confirmed_ms,const char *confirmed_session);
link_kind link_state_ota(const link_state *s,int64_t now,bool wifi_ready,bool usb_rpc_ready);
link_kind link_state_ota_bulk(const link_state *s,int64_t now,bool wifi_ready,bool usb_rpc_ready,bool ble_rpc_ready);
link_kind link_state_image(const link_state *s,int64_t now,const char *session,bool wifi_ready,bool usb_rpc_ready,bool ble_rpc_ready);
const char *link_name(link_kind link);
