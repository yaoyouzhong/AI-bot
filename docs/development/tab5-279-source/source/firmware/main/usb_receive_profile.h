#pragma once
#include <stdatomic.h>
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#ifdef ESP_PLATFORM
#include "soc/soc.h"
#endif

// Bounded numeric receive timing only; no payload, scheduler or endpoint writes.
static atomic_uint usb_rx_values[8],usb_rx_notified;
static atomic_bool usb_rx_active;
static inline void usb_rx_max(unsigned index,unsigned value) {
    unsigned old=atomic_load(&usb_rx_values[index]);
    while(value>old&&!atomic_compare_exchange_weak(&usb_rx_values[index],&old,value)){}
}
static inline void usb_rx_reset(void) {
    for(unsigned i=0;i<8;i++)atomic_store(&usb_rx_values[i],0);
    atomic_store(&usb_rx_notified,0);atomic_store(&usb_rx_active,false);
}
static inline void usb_rx_enable(bool enabled) {
    atomic_store(&usb_rx_active,enabled);atomic_store(&usb_rx_notified,0);
}
static inline void usb_rx_notify(unsigned now) {
    if(atomic_load(&usb_rx_active)){atomic_store(&usb_rx_notified,now);atomic_fetch_add(&usb_rx_values[5],1);}
}
static inline void usb_rx_span(unsigned bytes,unsigned read_us,unsigned copy_us,unsigned now) {
    atomic_fetch_add(&usb_rx_values[0],bytes);atomic_fetch_add(&usb_rx_values[1],1);
    usb_rx_max(2,bytes);atomic_fetch_add(&usb_rx_values[3],read_us);atomic_fetch_add(&usb_rx_values[4],copy_us);
    unsigned notified=atomic_exchange(&usb_rx_notified,0);
    // A callback can publish after the caller sampled now. Reject that future
    // sample; genuine timer wrap still has a small unsigned elapsed value.
    unsigned elapsed=now-notified;
    if(notified&&elapsed<0x80000000u)usb_rx_max(6,elapsed);
#ifdef ESP_PLATFORM
    // P4 OTG-FS base0x50040000, OUT endpoint2 transfer-size register+0xb50.
    // Observe the hardware counter after FIFO drain/rearm; never modify it.
    usb_rx_max(7,(REG_READ(0x50040b50u)>>19)&0x3ffu);
#endif
}
static inline void usb_rx_format(char *out,size_t size) {
    unsigned hardware=0,ahb=0,fifo=0;
#ifdef ESP_PLATFORM
    hardware=REG_READ(0x50040048u);ahb=REG_READ(0x50040008u);fifo=REG_READ(0x50040024u);
#endif
    snprintf(out,size,";usb_rx,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u",
        atomic_load(&usb_rx_values[0]),atomic_load(&usb_rx_values[1]),atomic_load(&usb_rx_values[2]),
        atomic_load(&usb_rx_values[3])/1000,atomic_load(&usb_rx_values[4])/1000,atomic_load(&usb_rx_values[5]),
        atomic_load(&usb_rx_values[6]),atomic_load(&usb_rx_values[7]),hardware,ahb,fifo);
}
