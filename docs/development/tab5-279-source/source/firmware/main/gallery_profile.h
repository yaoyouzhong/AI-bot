#pragma once
#include <stdint.h>
typedef struct {
    uint64_t encode_us,exchange_us,decode_us,backpressure_us;
    unsigned calls;
    uint64_t network_read_us,consumer_wait_us;
    unsigned receive_ahead_calls,receive_peak_bytes;
} gallery_rpc_profile;
void rpc_gallery_profile_reset(void);
gallery_rpc_profile rpc_gallery_profile_get(void);
