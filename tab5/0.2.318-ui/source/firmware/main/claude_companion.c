#include "claude_companion.h"
#if __has_include("claude_pet_data.inc")
#include "claude_pet_data.inc"
#define CLAUDE_HAS_ART 1
#else
#define CLAUDE_HAS_ART 0
#endif

bool claude_companion_asset_available(void) { return CLAUDE_HAS_ART; }
unsigned claude_companion_frame_count(void) {
#if CLAUDE_HAS_ART
    return sizeof(claude_pet_frames)/sizeof(claude_pet_frames[0]);
#else
    return 0;
#endif
}
claude_companion_frame_info claude_companion_frame(codex_companion_state state,int64_t elapsed,bool repeat_completion) {
    claude_companion_frame_info result={0};
#if CLAUDE_HAS_ART
    unsigned group=4; // Unknown: search/exploration, using the original space clip.
    if(state==CODEX_COMPANION_WORKING)group=0;
    else if(state==CODEX_COMPANION_IDLE)group=0;
    else if(state==CODEX_COMPANION_WAITING||state==CODEX_COMPANION_APPROVAL||state==CODEX_COMPANION_QUESTION||state==CODEX_COMPANION_BLOCKED)group=2;
    else if(state==CODEX_COMPANION_DONE)group=3;
    else if(state==CODEX_COMPANION_STOPPED)group=5;
    uint64_t tick=(uint64_t)(elapsed>0?elapsed:0)*12u/1000u;
    unsigned count=claude_pet_groups[group].count;
    unsigned local=state==CODEX_COMPANION_DONE&&!repeat_completion?(tick<count?(unsigned)tick:count-1):(unsigned)(tick%count);
    if(state==CODEX_COMPANION_IDLE)local=0; // Original unadorned Clawd; motion comes only from the slow breath.
    unsigned frame=claude_pet_groups[group].first+local;
    result=(claude_companion_frame_info){frame,claude_pet_frames[frame].width,claude_pet_frames[frame].height,
        claude_pet_groups[group].anchor_x2,group==0?1472u:group==3?1408u:1344u,
        claude_pet_groups[group].anchor_y2,group==0?15u:(group==3||group==5)?14u:12u,0,0};
    result.saver_scale_q8=result.saver_scale*256u;
    if(state==CODEX_COMPANION_IDLE) {
        unsigned phase=(unsigned)((uint64_t)(elapsed>0?elapsed:0)%3600u);
        unsigned breath=phase<=1800u?phase:3600u-phase;
        result.scale_q8=1472u+16u*breath/1800u;
        // Quantize the visible idle motion to avoid redrawing the large sprite
        // for tiny scale changes on each UI tick.
        static const uint8_t pulse[]={0,1,2,4,6,8,10,12,14,15,16,16,16,15,14,12,10,8,6,4,2,1,0,0};
        unsigned amount=pulse[phase/150u];
        result.saver_scale_q8+=8u*amount;
        result.saver_lift=(3u*amount+2u)/4u;
    }
#else
    (void)state;(void)elapsed;(void)repeat_completion;
#endif
    return result;
}
bool claude_companion_decode(unsigned frame,uint8_t *output,size_t capacity,unsigned *width,unsigned *height) {
#if CLAUDE_HAS_ART
    if(frame>=claude_companion_frame_count())return false;
    unsigned w=claude_pet_frames[frame].width,h=claude_pet_frames[frame].height;
    size_t offset=claude_pet_frames[frame].offset,size=claude_pet_frames[frame].size;
    if(capacity<w*h*3u||offset>sizeof(claude_pet_stream)||size>sizeof(claude_pet_stream)-offset)return false;
    if(!codex_companion_unpack(claude_pet_stream+offset,size,claude_pet_palette,claude_pet_alpha,output,w*h))return false;
    if(width)*width=w;
    if(height)*height=h;
    return true;
#else
    (void)frame;(void)output;(void)capacity;(void)width;(void)height;return false;
#endif
}
