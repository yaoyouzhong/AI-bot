#include "codex_companion.h"
#include <stdio.h>
#include <string.h>

static const cJSON *field(const cJSON *value, const char *name) { return cJSON_GetObjectItemCaseSensitive(value,name); }
static const char *string(const cJSON *value, const char *name) { const cJSON *v=field(value,name); return cJSON_IsString(v)?v->valuestring:""; }

codex_companion_value codex_companion_resolve(const cJSON *snapshot, const cJSON *task, bool fresh, bool unread, const char *unread_turn) {
    codex_companion_value result={.state=CODEX_COMPANION_UNKNOWN};
    snprintf(result.task,sizeof(result.task),"%s",string(task,"id"));
    snprintf(result.turn,sizeof(result.turn),"%s",string(task,"turnId"));
    if(!fresh||!cJSON_IsObject(snapshot))return result;
    const cJSON *model=field(snapshot,"codex");
    if(!strcmp(string(model,"state"),"offline")){result.state=CODEX_COMPANION_STOPPED;return result;}
    const char *actual=string(task,"companionState");
    bool observed=cJSON_IsString(field(task,"companionState"));
    if(!observed)actual=string(task,"state");
    const char *waiting=string(task,"waitingKind");
    bool same_task=result.task[0]&&strcmp(result.task,"--");
    if(!strcmp(actual,"waiting")) {
        result.state=!strcmp(waiting,"answer")?CODEX_COMPANION_QUESTION:!strcmp(waiting,"confirm")?CODEX_COMPANION_APPROVAL:CODEX_COMPANION_WAITING;
    } else if(!strcmp(actual,"working"))result.state=CODEX_COMPANION_WORKING;
    else if(!strcmp(actual,"failed")||!strcmp(actual,"blocked")||(!strcmp(actual,"idle")&&!strcmp(string(task,"outcome"),"failed")))result.state=CODEX_COMPANION_BLOCKED;
    else if(!strcmp(actual,"unknown")||(same_task&&!actual[0]))result.state=CODEX_COMPANION_UNKNOWN;
    else if(unread){result.state=CODEX_COMPANION_DONE;snprintf(result.turn,sizeof(result.turn),"%s",unread_turn?unread_turn:"");}
    else if(!strcmp(actual,"idle"))result.state=CODEX_COMPANION_IDLE;
    else if(!same_task&&!strcmp(string(model,"state"),"idle"))result.state=CODEX_COMPANION_IDLE;
    return result;
}

const char *codex_companion_label(codex_companion_state state) {
    static const char *labels[]={"待命","工作中","等你确认","等你回答","等你处理","已完成","任务受阻","Codex 未运行","状态未知"};
    return (unsigned)state<sizeof(labels)/sizeof(labels[0])?labels[state]:labels[CODEX_COMPANION_UNKNOWN];
}

bool codex_companion_select(codex_companion_animation *animation, codex_companion_value value, int64_t now) {
    if(animation->initialized&&animation->value.state==value.state&&!strcmp(animation->value.task,value.task)&&!strcmp(animation->value.turn,value.turn))return false;
    animation->value=value;animation->started_at=now;animation->initialized=true;return true;
}

unsigned codex_companion_frame(const codex_companion_animation *animation, int64_t now) {
    int64_t age=animation->initialized&&now>=animation->started_at?now-animation->started_at:0;
    switch(animation->value.state) {
    case CODEX_COMPANION_IDLE: {
        static const unsigned durations[]={1680,660,660,840,840,1920};unsigned tick=(unsigned)(age%6600);
        for(unsigned i=0;i<6;i++){if(tick<durations[i])return i;tick-=durations[i];}return 0;
    }
    case CODEX_COMPANION_WORKING: {unsigned t=(unsigned)(age%820);return 6+(t/120<5?t/120:5);}
    case CODEX_COMPANION_APPROVAL:case CODEX_COMPANION_QUESTION:case CODEX_COMPANION_WAITING: {
        unsigned t=(unsigned)(age%7000);return t>=700?14:12+(t/140<3?t/140:3);
    }
    case CODEX_COMPANION_DONE: {if(age>=2060)return 17;unsigned t=(unsigned)(age%1030);return 16+(t/150<5?t/150:5);}
    case CODEX_COMPANION_BLOCKED: return age>=660?25:22+(unsigned)(age/140<3?age/140:3);
    case CODEX_COMPANION_STOPPED:return 1;
    case CODEX_COMPANION_UNKNOWN:default:return 26;
    }
}

unsigned codex_companion_saver_frame(const codex_companion_animation *animation,int64_t now) {
    codex_companion_animation playback=*animation;
    if(animation->value.state==CODEX_COMPANION_DONE&&animation->initialized&&now>=animation->started_at)
        playback.started_at=now-(now-animation->started_at)%1030;
    return codex_companion_frame(&playback,now);
}

#ifdef TAB5_CODEX_PET_ASSET
#include "codex_pet_data.inc"
#endif

bool codex_companion_asset_available(void) {
#ifdef TAB5_CODEX_PET_ASSET
    return sizeof(codex_pet_frames)/sizeof(codex_pet_frames[0])==27;
#else
    return false;
#endif
}

bool codex_companion_unpack(const uint8_t *data,size_t size,const uint16_t palette[256],const uint8_t alpha[256],uint8_t *output,size_t pixels) {
    if(!data||!palette||!alpha||!output||!pixels||pixels>CODEX_COMPANION_PIXELS)return false;
    size_t cursor=0,used=0;
    // Validate the whole frame before changing the currently displayed buffer.
    while(cursor<size) {
        uint8_t tag=data[cursor++];size_t count=(tag&127u)+1u,bytes=tag&128u?1u:count;
        if(count>pixels-used||bytes>size-cursor)return false;
        used+=count;cursor+=bytes;
    }
    if(used!=pixels)return false;
    cursor=0;used=0;
    while(cursor<size) {
        uint8_t tag=data[cursor++];size_t count=(tag&127u)+1u;
        for(size_t n=0;n<count;n++) {
            unsigned index=data[cursor+(tag&128u?0:n)];uint16_t color=palette[index];
            output[used*2]=(uint8_t)color;output[used*2+1]=(uint8_t)(color>>8);output[pixels*2+used]=alpha[index];used++;
        }
        cursor+=tag&128u?1u:count;
    }
    return true;
}

bool codex_companion_decode(unsigned frame,uint8_t *output,size_t capacity) {
    if(capacity<CODEX_COMPANION_FRAME_BYTES)return false;
#ifdef TAB5_CODEX_PET_ASSET
    if(frame>=sizeof(codex_pet_frames)/sizeof(codex_pet_frames[0]))return false;
    size_t offset=codex_pet_frames[frame].offset,size=codex_pet_frames[frame].size;
    if(offset>sizeof(codex_pet_stream)||size>sizeof(codex_pet_stream)-offset)return false;
    return codex_companion_unpack(codex_pet_stream+offset,size,codex_pet_palette,codex_pet_alpha,output,CODEX_COMPANION_PIXELS);
#else
    (void)frame;(void)output;return false;
#endif
}
