#pragma once
#include <stdint.h>
#include <math.h>

// Original two-note agenda cue: 240 ms D5, 80 ms gap, 320 ms G5,
// then 40 ms silence. Output is 16 kHz signed PCM, before user volume.
#define AGENDA_CHIME_BLOCKS 34
static inline int16_t agenda_chime_sample(unsigned index) {
    unsigned start, length;float frequency;
    if(index<3840){start=0;length=3840;frequency=587.33f;}
    else if(index>=5120&&index<10240){start=5120;length=5120;frequency=783.99f;}
    else return 0;
    unsigned position=index-start;float envelope;
    if(position<320){float t=(float)position/320;envelope=t*t*(3-2*t);}
    else {float tail=(float)(length-1-position)/(length-1-320);envelope=tail*tail*(3-2*tail);}
    float phase=6.2831853f*frequency*position/16000;
    // A quiet second partial adds warmth; retain headroom without changing
    // codec volume, mute, capture ownership or other notification sounds.
    return (int16_t)(8333.0f*envelope*(sinf(phase)+0.08f*sinf(2*phase)));
}
