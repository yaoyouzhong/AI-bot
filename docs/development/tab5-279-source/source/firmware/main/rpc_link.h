#pragma once
#include "link_state.h"
#include "cJSON.h"
#include <stddef.h>
#include <stdint.h>
#include <stdbool.h>
#include "gallery_profile.h"
// Bounded, authenticated application RPC over CDC or GATT. A request stays on
// its selected transport; uncertain sends are never retried on another link.
// Local admission result only: no nonce or request bytes were published.
// Callers may retry this result while their original owner/deadline still hold.
#define RPC_STATUS_UNPUBLISHED_BUSY (-1011)
void rpc_link_init(void);
void rpc_link_reset(link_kind link);
size_t rpc_link_read(link_kind link,uint8_t *out,size_t capacity);
bool rpc_usb_wait_supported(void);
size_t rpc_link_read_wait(link_kind link,uint8_t *out,size_t capacity,unsigned wait_ms);
bool rpc_link_write(link_kind link,const uint8_t *data,size_t size);
link_kind rpc_link_select(void);
link_kind rpc_read_link_select(void);
// Whole-library reads prefer a fresh Wi-Fi path in auto mode, with USB fallback.
// Foreground state, explicit transport preference and per-request binding remain.
link_kind rpc_library_link_select(void);
link_kind rpc_ota_link_select(void);
// Availability hint only; every actual range still authenticates independently.
bool rpc_ota_link_connected(link_kind link);
// Auto mode prefers bulk bandwidth; explicit single-channel modes are binding.
link_kind rpc_image_link_select(const char *session);
bool rpc_link_ready(void);
bool rpc_read_link_ready(void);
// The paired bridge advertises this capability inside encrypted state.
size_t rpc_data_limit(link_kind link);
cJSON *rpc_call(link_kind link,cJSON *request,int *status,char nonce[33]);
cJSON *rpc_http_call(void *client,cJSON *request,int *status,char nonce[33]);
cJSON *rpc_data_call(link_kind link,void *http,cJSON *request,int *status,char nonce[33],
    const uint8_t *upload,size_t upload_size,uint8_t *download,size_t capacity,size_t *received);
typedef bool (*rpc_gallery_progress)(void *context,size_t verified);
bool rpc_gallery_stream_ready(void);
cJSON *rpc_gallery_stream_call(void *http,cJSON *request,int *status,char nonce[33],
    uint8_t *download,size_t capacity,size_t *received,rpc_gallery_progress progress,void *context);
int rpc_ota_read(link_kind link,const char *sha,const char *offer,int offset,uint8_t *out,int count);
// Same authenticated immutable range over Wi-Fi RPC, used only when the
// selected bridge advertises rpcOtaResume=1 after a stream interruption.
int rpc_ota_http_read(void *http,const char *sha,const char *offer,int offset,uint8_t *out,int count);
void rpc_ota_reset_bytes(link_kind link);
unsigned rpc_ota_received_bytes(link_kind link);
int rpc_ota_probe_read(const char *sha,const char *offer,int offset,uint8_t *out,int count);
typedef struct {
    uint64_t encode_us,exchange_us,decode_us;
    unsigned calls,wire_tx,wire_rx,min_internal,min_psram,min_largest;
} rpc_profile;
void rpc_profile_reset(link_kind link);
rpc_profile rpc_profile_get(link_kind link);
void rpc_failure_diagnostics(char *out,size_t size);
