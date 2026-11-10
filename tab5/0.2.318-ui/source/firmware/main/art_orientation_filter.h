#pragma once
#include <stdint.h>
#include <stdlib.h>

typedef struct {int orientation,candidate;int64_t since,last;} art_orientation_filter;
// Mounted-device calibration: normal landscape is raw X ~= +1g.
// Raw Y portrait signs retain the two directions verified on .128.
// Ambiguous, flat or accelerated samples never cause an orientation switch.
static inline int art_orientation_sample(art_orientation_filter *s,int x,int y,int z,int64_t now) {
    int ax=abs(x),ay=abs(y),next=3;
    int64_t magnitude=(int64_t)x*x+(int64_t)y*y+(int64_t)z*z;
    if(magnitude>=131533373 && magnitude<=453655921) { // 0.7g .. 1.3g, +/-2g sensor range
        // Use the in-plane gravity projection. Comparing against Z rejected
        // ordinary stand angles near 35 degrees; keep only a near-flat floor.
        if(ax>=4000&&ax*2>ay*3)next=x>0?0:2;
        else if(ay>=4000&&ay*2>ax*3)next=y>0?1:-1;
    }
    if(next==3){s->candidate=3;s->since=now;}
    else {
        if(next!=s->candidate||!s->last||now-s->last>300||now<s->last){s->candidate=next;s->since=now;}
        // Four consecutive 50 ms samples confirm a deliberate turn, while
        // transient diagonal/flat/accelerated poses still restart the hold.
        if(now-s->since>=150)s->orientation=next;
    }
    s->last=now;return s->orientation;
}
