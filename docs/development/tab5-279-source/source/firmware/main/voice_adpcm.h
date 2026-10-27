#pragma once
#include <stdint.h>
#include <stddef.h>
// Independent IMA ADPCM blocks: count LE16, predictor LE16, index, reserved,
// then low nibble first. Reset each 200 ms block; no cross-packet decoder state.
static size_t voice_adpcm_encode(const int16_t *pcm,unsigned count,uint8_t *out) {
    static const int steps[89]={7,8,9,10,11,12,13,14,16,17,19,21,23,25,28,31,34,37,41,45,50,55,60,66,73,80,88,97,107,118,130,143,157,173,190,209,230,253,279,307,337,371,408,449,494,544,598,658,724,796,876,963,1060,1166,1282,1411,1552,1707,1878,2066,2272,2499,2749,3024,3327,3660,4026,4428,4871,5358,5894,6484,7132,7845,8630,9493,10442,11487,12635,13899,15289,16818,18500,20350,22385,24623,27086,29794,32767};
    static const int adjust[8]={-1,-1,-1,-1,2,4,6,8};
    if(!count||count>3200)return 0;
    int predictor=pcm[0],index=0;
    out[0]=count;out[1]=count>>8;out[2]=predictor;out[3]=predictor>>8;out[4]=out[5]=0;
    for(unsigned i=1;i<count;i++) {
        int diff=pcm[i]-predictor,code=0,step=steps[index],delta=step>>3;
        if(diff<0){code=8;diff=-diff;}
        if(diff>=step){code|=4;diff-=step;delta+=step;}
        if(diff>=step/2){code|=2;diff-=step/2;delta+=step/2;}
        if(diff>=step/4){code|=1;delta+=step/4;}
        predictor+=(code&8)?-delta:delta;
        if(predictor>32767)predictor=32767;
        if(predictor<-32768)predictor=-32768;
        index+=adjust[code&7];if(index<0)index=0;if(index>88)index=88;
        unsigned byte=6+(i-1)/2;
        if(i&1)out[byte]=code;else out[byte]|=code<<4;
    }
    return 6+count/2;
}
