#pragma once
// Content-free USB status rejection codes. Zero also means an older peer
// supplied no reason. Never include payload, identity or session text.
typedef enum {
    DEVICE_REJECT_NONE=0,
    DEVICE_REJECT_JSON=1,
    DEVICE_REJECT_PACKED=2,
    DEVICE_REJECT_ENVELOPE=3,
    DEVICE_REJECT_IDENTITY=4,
    DEVICE_REJECT_SEQUENCE=5
} device_reject_reason;
