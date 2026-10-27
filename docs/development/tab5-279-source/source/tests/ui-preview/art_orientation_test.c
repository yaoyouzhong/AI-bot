#include <assert.h>
#include <stdio.h>
#include "../../firmware/main/art_orientation_filter.h"
int main(void){
    art_orientation_filter s={.candidate=3};int64_t t=100;
    const int poses[][4]={{16197,-181,-1899,0},{0,16384,0,1},{0,-16384,0,-1},{-16384,0,0,2},{16384,0,0,0},{100,5600,-15400,1},{100,-4400,-15780,-1}};
    for(int pose=0;pose<7;pose++){
        int previous=s.orientation;
        art_orientation_sample(&s,0,0,16384,t);t+=50;
        for(int i=0;i<3;i++,t+=50)assert(art_orientation_sample(&s,poses[pose][0],poses[pose][1],poses[pose][2],t)==previous);
        assert(art_orientation_sample(&s,poses[pose][0],poses[pose][1],poses[pose][2],t)==poses[pose][3]);t+=50;
        // Brief wrong-axis observations, flat, diagonal and acceleration cannot
        // change orientation or contribute time to the next stable candidate.
        for(int i=0;i<30;i++,t+=50){
            int type=i%5;
            int x=type==0?0:type==1?11000:type==2?28000:type==3?1900:poses[(pose+1)%7][0];
            int y=type==0?0:type==1?11000:type==2?28000:type==3?2800:poses[(pose+1)%7][1];
            int z=type==0?16384:type==3?-16000:type==4?poses[(pose+1)%7][2]:0;
            assert(art_orientation_sample(&s,x,y,z,t)==poses[pose][3]);
        }
    }
    // A long sampling gap restarts the stability interval.
    art_orientation_sample(&s,16384,0,0,t);t+=50;
    art_orientation_sample(&s,16384,0,0,t);t+=1000;
    assert(art_orientation_sample(&s,16384,0,0,t)==-1);t+=50;
    for(int i=0;i<2;i++,t+=50)assert(art_orientation_sample(&s,16384,0,0,t)==-1);
    assert(art_orientation_sample(&s,16384,0,0,t)==0);
    puts("ART_ORIENTATION_OK deliberate four-direction/shallow-stand turns within 150 ms from first sample; 50 ms cadence; brief/flat/diagonal/shake/stale rejection");
}
