#include <assert.h>
#include <stdio.h>
#include "music_timeline_reference.h"

static aibot_music_position sample(aibot_music_clock *clock, uint64_t seq,
        double position, bool playing, int64_t received, int64_t now) {
    return aibot_music_value(clock,1,10,seq,position,100,playing,true,true,received,now);
}
static void lifecycle(void) {
    aibot_music_clock clock={0};
    assert(sample(&clock,1,0,true,0,500).milliseconds==500); /* zero start */
    assert(sample(&clock,2,2.1,true,2000,2500).milliseconds==2500);
    assert(sample(&clock,3,2.4,false,2600,9000).milliseconds==2400); /* pause */
    assert(sample(&clock,4,2.4,true,9000,9500).milliseconds==2900); /* resume */
    assert(sample(&clock,5,40,true,10000,10000).milliseconds==40000); /* seek forward */
    assert(sample(&clock,6,5,true,11000,11000).milliseconds==5000); /* real backwards seek */
    assert(sample(&clock,6,80,true,11500,11500).milliseconds==5500); /* duplicate */
    assert(sample(&clock,4,2,true,11700,11700).milliseconds==5700); /* out of order */
    assert(aibot_music_value(&clock,1,11,7,0,10,true,true,true,12000,12500).milliseconds==500); /* new track */
    assert(aibot_music_value(&clock,2,11,1,0,10,true,true,true,13000,13500).milliseconds==500); /* new bridge */
    assert(!aibot_music_value(&clock,2,11,2,1,10,true,false,true,14000,14500).available);
    assert(!aibot_music_value(&clock,2,11,1,0,10,true,true,true,14500,14500).available); /* old data cannot restore an unknown clock */
    assert(aibot_music_value(&clock,2,11,3,2,10,true,true,true,15000,15500).milliseconds==2500);
    assert(!aibot_music_value(&clock,2,11,4,3,10,true,true,false,16000,20000).available); /* lost connection */
    assert(!aibot_music_value(&clock,2,11,4,3,10,true,true,true,20000,20500).available); /* fresh duplicate does not establish recovery */
    assert(aibot_music_value(&clock,2,11,5,8,10,true,true,true,21000,22000).milliseconds==9000);
    assert(aibot_music_value(&clock,2,11,5,8,10,true,true,true,21000,26000).milliseconds==10000); /* end cap */
    assert(!aibot_music_value(&clock,2,11,6,NAN,10,true,true,true,27000,27000).available);
    assert(!aibot_music_value(&clock,2,11,6,0,INFINITY,true,true,true,27000,27000).available);
    assert(!aibot_music_value(&clock,2,11,6,0,10,true,true,true,28000,27000).available);
    assert(aibot_music_key("a","bc","")!=aibot_music_key("ab","c",""));
}
static void replay(bool legacy, bool jitter) {
    const int delays[]={60,180,90,140},offsets[]={0,120,-80,90};
    int index=-1,previous=-1,changes=0,backwards=0,skips=0;
    int64_t changed=0,shortest=100000,longest=0;
    aibot_music_clock clock={0};
    for(int64_t now=0;now<=30000;now+=50) {
        while(index+1<16&&now>=(index+1)*2000+delays[(index+1)%4])index++;
        if(index<0)continue;
        int64_t received=index*2000+delays[index%4];
        double seconds=(10350+index*2000+(jitter?offsets[index%4]:0))/1000.0;
        int shown=legacy?(int)seconds+(int)((now-received)/1000):
            (int)(sample(&clock,(uint64_t)index+1,seconds,true,received,now).milliseconds/1000);
        if(previous>=0&&shown!=previous) {
            backwards+=shown<previous;skips+=shown>previous+1;
            if(changes>0) {
                int64_t dwell=now-changed;
                if(dwell<shortest)shortest=dwell;
                if(dwell>longest)longest=dwell;
            }
            changes++;changed=now;
        }
        previous=shown;
    }
    assert(!backwards&&!skips);
    if(legacy)assert(shortest<1000&&longest>1000);
    else assert(shortest==1000&&longest==1000);
    printf("%s jitter=%d dwell_ms=%lld..%lld backwards=%d skips=%d\n",
        legacy?"published_expression":"reference_candidate",jitter,
        (long long)shortest,(long long)longest,backwards,skips);
}
int main(void) {
    lifecycle();replay(true,false);replay(false,false);replay(true,true);replay(false,true);
    puts("MUSIC_REFERENCE_OK lifecycle/jitter/duplicates/seeks/stale/end/invalid; no firmware built or device accessed");
}
