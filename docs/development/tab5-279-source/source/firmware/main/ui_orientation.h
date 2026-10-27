#pragma once
#include "art_orientation.h"
#include <stdlib.h>

// Only horizontal poses can change the ordinary UI. A fresh sequence of
// physical samples (not repeated timer reads) must remain stable for 500 ms.
typedef struct { int direction,candidate; int64_t since,last; } ui_orientation_filter;
static inline int ui_orientation_sample(ui_orientation_filter *s,art_orientation_state a,
                                        int64_t now,bool blocked,int mode) {
    if(mode) {s->direction=mode==2?2:0;s->candidate=3;s->last=0;return s->direction;}
    if(blocked||!a.ready||a.sampled_ms<=0||now<a.sampled_ms||now-a.sampled_ms>200) {
        s->candidate=3;s->last=0;return s->direction;
    }
    if(a.sampled_ms==s->last)return s->direction;
    int64_t x=a.x,y=a.y,z=a.z,magnitude=x*x+y*y+z*z;
    int next=3;
    if(magnitude>=131533373&&magnitude<=453655921&&llabs(x)>=4000&&llabs(x)*2>llabs(y)*3)
        next=x>0?0:2;
    if(next==3){s->candidate=3;s->last=a.sampled_ms;return s->direction;}
    if(next!=s->candidate||!s->last||a.sampled_ms-s->last>150||a.sampled_ms<s->last) {
        s->candidate=next;s->since=a.sampled_ms;
    }
    if(a.sampled_ms-s->since>=500)s->direction=next;
    s->last=a.sampled_ms;return s->direction;
}
