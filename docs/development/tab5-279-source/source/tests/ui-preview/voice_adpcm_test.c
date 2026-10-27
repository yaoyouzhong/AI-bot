#undef NDEBUG
#include <assert.h>
#include <stdio.h>
#include <math.h>
#include "../../firmware/main/voice_adpcm.h"
int main(int argc,char **argv) {
    int16_t pcm[3200];uint8_t data[1606];
    for(int i=0;i<3200;i++)pcm[i]=0;
    assert(voice_adpcm_encode(pcm,3200,data)==1606);
    for(int i=2;i<1606;i++)assert(data[i]==0);
    assert(voice_adpcm_encode(pcm,0,data)==0&&voice_adpcm_encode(pcm,3201,data)==0);
    for(unsigned count=1;count<=3200;count++) {
        for(unsigned j=0;j<sizeof(data);j++)data[j]=0xa5;
        size_t bytes=voice_adpcm_encode(pcm,count,data);
        assert(bytes==6+count/2&&((unsigned)data[0]+((unsigned)data[1]<<8))==count);
        for(unsigned j=2;j<bytes;j++)assert(data[j]==0);
        for(unsigned j=(unsigned)bytes;j<sizeof(data);j++)assert(data[j]==0xa5);
    }
    for(int i=0;i<3200;i++)pcm[i]=(int16_t)(10000*sin(6.283185307*440*i/16000));
    size_t size=voice_adpcm_encode(pcm,3200,data);assert(size==1606);
    if(argc==2){FILE *f=fopen(argv[1],"wb");assert(f);assert(fwrite(data,1,size,f)==size);fclose(f);}
    puts("VOICE_ADPCM_ENCODER_OK bounded 3200 samples / 1606 bytes, silence and invalid lengths");return 0;
}
