#pragma once
#include <stdint.h>
#include <stdbool.h>
#include <math.h>
#include <limits.h>

typedef struct { int64_t epoch_ms, monotonic_ms, received_ms; bool initialized; } ui_time_state;
static int64_t ui_time_value(ui_time_state *s, int64_t sample_ms, int64_t received_ms, int64_t now_ms) {
    int64_t current=s->epoch_ms+now_ms-s->monotonic_ms;
    if(sample_ms>0 && (!s->initialized || s->received_ms!=received_ms)) {
        int64_t candidate=sample_ms+now_ms-received_ms;
        // Heartbeats have transport jitter. Keep a steady local phase instead of
        // restarting the fractional second on every USB/Wi-Fi/BLE response.
        if(!s->initialized || candidate-current>2000 || current-candidate>2000) {
            s->epoch_ms=candidate;s->monotonic_ms=now_ms;s->initialized=true;current=candidate;
        }
        s->received_ms=received_ms;
    }
    return current;
}

// Keep the fractional position until after receiver-side elapsed time is added.
static int ui_music_position(double sample, int duration, bool advance, int64_t age_ms) {
    double position=isfinite(sample)?sample:0;
    if(advance && age_ms>0 && (duration>0 || position>0))position+=(double)age_ms/1000.0;
    if(position<=0)return 0;
    if(duration>0 && position>duration)position=duration;
    return position>=INT_MAX?INT_MAX:(int)position;
}
