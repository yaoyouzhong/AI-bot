/* Original, host-tested reference for a future TAB5 firmware change.
 * Not linked into the Mac app or any firmware build. See the follow-up doc.
 * Existing v1 fields suffice; sequence is scoped to the bridge session. */
#ifndef AIBOT_MUSIC_TIMELINE_REFERENCE_H
#define AIBOT_MUSIC_TIMELINE_REFERENCE_H
#include <stdbool.h>
#include <stdint.h>
#include <math.h>

typedef struct {
    uint64_t session, track, sequence;
    int64_t position, anchor, received, duration;
    bool seen, valid, playing;
} aibot_music_clock;
typedef struct { int64_t milliseconds; bool available; } aibot_music_position;

static inline uint64_t aibot_music_key(const char *a, const char *b, const char *c) {
    uint64_t hash=UINT64_C(14695981039346656037);
    const char *parts[]={a,b,c};
    for(int i=0;i<3;i++) {
        for(const unsigned char *p=(const unsigned char *)parts[i];p&&*p;p++)
            hash=(hash^*p)*UINT64_C(1099511628211);
        hash=(hash^0xff)*UINT64_C(1099511628211);
    }
    return hash;
}
static inline int64_t aibot_music_project(const aibot_music_clock *clock, int64_t now) {
    int64_t value=clock->position;
    if(clock->playing&&now>clock->anchor)value+=now-clock->anchor;
    if(clock->duration>0&&value>clock->duration)value=clock->duration;
    return value;
}
static inline aibot_music_position aibot_music_value(aibot_music_clock *clock,
        uint64_t session, uint64_t track, uint64_t sequence, double seconds,
        double duration, bool playing, bool available, bool fresh,
        int64_t received, int64_t now) {
    /* Bounds make conversion and elapsed arithmetic safe, including malformed
     * host values. Thirty-one years is far beyond a useful media duration. */
    if(!isfinite(seconds)||!isfinite(duration)||seconds<0||duration<0||
       seconds>1e9||duration>1e9||received<0||now<received||now-received>86400000) {
        clock->valid=false;return (aibot_music_position){0,false};
    }
    int64_t position=(int64_t)llround(seconds*1000),length=(int64_t)llround(duration*1000);
    if(length>0&&position>length)position=length;
    if(!available||!fresh) {clock->valid=false;return (aibot_music_position){position,false};}
    bool same_source=clock->seen&&clock->session==session;
    bool duplicate=same_source&&sequence>0&&sequence<=clock->sequence;
    if(!duplicate||!clock->valid) {
        bool continuous=clock->valid&&same_source&&clock->track==track&&clock->playing&&playing&&received>=clock->received;
        int64_t expected=continuous?aibot_music_project(clock,received):position;
        int64_t error=position-expected;
        /* Keep phase under bounded jitter. A seek within 350 ms is inherently
         * indistinguishable from jitter in v1; larger seeks reset immediately. */
        if(!continuous||error>350||error< -350) {clock->position=position;clock->anchor=received;}
        clock->session=session;clock->track=track;clock->sequence=sequence;
        clock->duration=length;clock->playing=playing;clock->received=received;
        clock->seen=true;clock->valid=true;
    }
    return (aibot_music_position){aibot_music_project(clock,now),true};
}
#endif
