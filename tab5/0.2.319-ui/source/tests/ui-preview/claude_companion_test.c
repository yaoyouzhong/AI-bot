#include "claude_companion.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>

int main(int argc,char **argv) {
    unsigned frame_count=claude_companion_frame_count();
    assert(claude_companion_asset_available()&&(frame_count==378||frame_count==384));
    bool headphones=frame_count==384;
    unsigned work=claude_companion_frame(CODEX_COMPANION_WORKING,0,false).frame;
    assert(claude_companion_frame(CODEX_COMPANION_WORKING,1000,false).frame!=work);
    if(headphones) {
        assert(work==378&&claude_companion_frame(CODEX_COMPANION_WORKING,720,false).frame==work);
        for(unsigned i=0;i<6;i++) {
            claude_companion_frame_info first=claude_companion_frame(CODEX_COMPANION_WORKING,i*120,false);
            claude_companion_frame_info last=claude_companion_frame(CODEX_COMPANION_WORKING,i*120+119,true);
            assert(first.frame==work+i&&last.frame==first.frame&&first.width==106&&first.height==67);
        }
    } else assert(claude_companion_frame(CODEX_COMPANION_WORKING,7000,false).frame==work);
    assert(claude_companion_frame(CODEX_COMPANION_DONE,7000,false).frame==claude_companion_frame(CODEX_COMPANION_DONE,70000,false).frame);
    assert(claude_companion_frame(CODEX_COMPANION_STOPPED,0,false).frame==claude_companion_frame(CODEX_COMPANION_STOPPED,70000,false).frame);
    assert(claude_companion_frame(CODEX_COMPANION_WAITING,3400,false).frame!=claude_companion_frame(CODEX_COMPANION_UNKNOWN,3400,false).frame);
    assert(claude_companion_frame(CODEX_COMPANION_DONE,7000,true).frame!=claude_companion_frame(CODEX_COMPANION_DONE,7000,false).frame);
    claude_companion_frame_info relaxed=claude_companion_frame(CODEX_COMPANION_IDLE,0,false),inhale=claude_companion_frame(CODEX_COMPANION_IDLE,1800,false),loop=claude_companion_frame(CODEX_COMPANION_IDLE,3600,false);
    assert(relaxed.frame==0&&inhale.frame==relaxed.frame&&loop.frame==relaxed.frame);
    assert(inhale.scale_q8>relaxed.scale_q8&&inhale.scale_q8-relaxed.scale_q8<=16&&loop.scale_q8==relaxed.scale_q8);
    assert(inhale.saver_scale_q8-relaxed.saver_scale_q8==128&&loop.saver_scale_q8==relaxed.saver_scale_q8);
    assert(relaxed.saver_lift==0&&inhale.saver_lift==12&&loop.saver_lift==0);
    for(unsigned t=0;t<3600;t+=150) {
        claude_companion_frame_info first=claude_companion_frame(CODEX_COMPANION_IDLE,t,true);
        claude_companion_frame_info last=claude_companion_frame(CODEX_COMPANION_IDLE,t+149,true);
        assert(first.frame==relaxed.frame&&last.saver_scale_q8==first.saver_scale_q8&&last.saver_lift==first.saver_lift);
    }
    FILE *dump=argc>1?fopen(argv[1],"wb"):NULL;
    if(argc>1)assert(dump);
    unsigned char buffer[CLAUDE_COMPANION_FRAME_BYTES];
    for(unsigned frame=0;frame<claude_companion_frame_count();frame++) {
        unsigned width=0,height=0;memset(buffer,0xA5,sizeof(buffer));
        assert(!claude_companion_decode(frame,buffer,1,&width,&height)&&buffer[0]==0xA5&&width==0&&height==0);
        assert(claude_companion_decode(frame,buffer,sizeof(buffer),&width,&height));
        assert(width&&height&&width*height*3<=sizeof(buffer)&&width%2==0);
        if(dump){fwrite(&width,4,1,dump);fwrite(&height,4,1,dump);fwrite(buffer,1,width*height*3,dump);}
    }
    assert(!claude_companion_decode(frame_count,buffer,sizeof(buffer),NULL,NULL));
    const codex_companion_state states[]={CODEX_COMPANION_WORKING,CODEX_COMPANION_IDLE,CODEX_COMPANION_WAITING,CODEX_COMPANION_DONE,CODEX_COMPANION_UNKNOWN,CODEX_COMPANION_STOPPED};
    const unsigned counts[]={headphones?6u:28u,44,101,73,131,1};
    unsigned aligned=0;
    for(unsigned group=0;group<6;group++)for(unsigned i=0;i<counts[group];i++) {
        unsigned at=headphones&&group==0?i*120:(i*1000+11)/12;
        claude_companion_frame_info f=claude_companion_frame(states[group],at,true);
        unsigned w,h;assert(claude_companion_decode(f.frame,buffer,sizeof(buffer),&w,&h));
        int left=152-(int)(f.anchor_x2*f.scale_q8+256)/512;
        int top=130-(int)(f.anchor_y2*f.scale_q8+256)/512;
        for(unsigned y=0;y<h;y++)for(unsigned x=0;x<w;x++)if(buffer[w*h*2+y*w+x]) {
            assert(left+(int)(x*f.scale_q8/256)>=0&&top+(int)(y*f.scale_q8/256)>=0);
            assert(left+(int)(((x+1)*f.scale_q8+255)/256)<=284);
            assert(top+(int)(((y+1)*f.scale_q8+255)/256)<=215);
        }
        aligned++;
    }
    assert(aligned==(headphones?356u:378u));
    if(dump)fclose(dump);
    puts(headphones?"CLAUDE_COMPANION_OK all_384_frames, six_frame_work_only, 120ms_loop, bounds, completion_hold, static_rest":"CLAUDE_COMPANION_OK all_378_frames, bounds, continuous_work, completion_hold, static_rest");
    return 0;
}
