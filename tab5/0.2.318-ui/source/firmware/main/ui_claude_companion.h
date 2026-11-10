// Official original Clawd frames; task meanings and playback are AI-bot's.
#include "claude_companion.h"
#define CLAUDE_PET_AVAILABLE claude_companion_asset_available()
typedef struct {
    lv_obj_t *root,*image;
    int x,y,width,height,last_frame,last_scale,last_lift;
} claude_pet_view;
static claude_pet_view home_claude,saver_claude;
static uint8_t *claude_pixels;
static lv_image_dsc_t claude_descriptor;
static unsigned claude_rendered_frame=UINT32_MAX;
static codex_companion_state claude_mood=CODEX_COMPANION_UNKNOWN;
static int64_t claude_mood_started;
static int home_companion_provider=1;
static bool companion_busy(cJSON *model) {return yes(model,"needsInput")||!strcmp(text(model,"state"),"working");}
static double companion_age(cJSON *model) {cJSON *v=item(model,"ageSeconds");return cJSON_IsNumber(v)&&v->valuedouble>=0&&v->valuedouble<1e30?v->valuedouble:1e30;}
// Domestic model records currently come from Claude Code. Keep the mascot
// bound to the supported client, including when it uses a custom provider.
static int companion_provider_resolve(cJSON *snapshot,unsigned preferred) {
    cJSON *codex=item(snapshot,"codex"),*claude=item(snapshot,"claude"),*domestic=item(snapshot,"domesticActivity");
    unsigned clients=preferred&3u;
    // An explicit single client is authoritative even while it is idle.
    if(clients==1u)return 0;
    if(clients==2u)return 1;
    bool codex_wait=yes(codex,"needsInput"),claude_wait=yes(claude,"needsInput")||yes(domestic,"needsInput");
    if(codex_wait!=claude_wait)return codex_wait?1:0;
    bool codex_busy=companion_busy(codex),claude_busy=companion_busy(claude)||companion_busy(domestic);
    if(codex_busy!=claude_busy)return codex_busy?1:0;
    cJSON *usage=item(item(domestic,"providers"),text(domestic,"activeProvider"));
    cJSON *last=item(usage,"lastActivity"),*epoch=item(snapshot,"epochUtc");
    double local_age=cJSON_IsNumber(last)&&cJSON_IsNumber(epoch)&&last->valuedouble>0&&last->valuedouble<=epoch->valuedouble
        ?epoch->valuedouble-last->valuedouble:1e30;
    double claude_age=LV_MIN(companion_age(claude),local_age),codex_age=companion_age(codex);
    // Compare the actual overview sessions where their timestamps are available.
    cJSON *claude_last=item(item(snapshot,"claudeSession"),"lastActivity");
    cJSON *codex_last=item(item(item(snapshot,"codexTasks"),"overviewTask"),"updatedAt");
    if(cJSON_IsNumber(epoch)&&cJSON_IsNumber(claude_last)&&claude_last->valuedouble>0&&claude_last->valuedouble<=epoch->valuedouble)
        claude_age=epoch->valuedouble-claude_last->valuedouble;
    if(cJSON_IsNumber(epoch)&&cJSON_IsNumber(codex_last)&&codex_last->valuedouble>0&&codex_last->valuedouble<=epoch->valuedouble)
        codex_age=epoch->valuedouble-codex_last->valuedouble;
    // Unknown or equally recent observations do not switch the display.
    if(codex_age!=claude_age)return codex_age<claude_age?1:0;
    return home_companion_provider;
}
static codex_companion_state companion_model_mood(cJSON *model,bool fresh) {
    if(!fresh||!cJSON_IsObject(model))return CODEX_COMPANION_UNKNOWN;
    if(yes(model,"needsInput"))return CODEX_COMPANION_WAITING;
    const char *value=text(model,"state");
    if(!strcmp(value,"working"))return CODEX_COMPANION_WORKING;
    if(yes(model,"completionActive"))return CODEX_COMPANION_DONE;
    if(!strcmp(value,"idle"))return CODEX_COMPANION_IDLE;
    if(!strcmp(value,"offline"))return CODEX_COMPANION_STOPPED;
    if(!strcmp(value,"blocked")||!strcmp(value,"error"))return CODEX_COMPANION_BLOCKED;
    return CODEX_COMPANION_UNKNOWN;
}
static const char *claude_companion_label(codex_companion_state value) {
    return value==CODEX_COMPANION_STOPPED?"暂无活动":codex_companion_label(value);
}
static codex_companion_state claude_client_mood(cJSON *snapshot,bool fresh) {
    cJSON *claude=item(snapshot,"claude"),*domestic=item(snapshot,"domesticActivity");
    if(!fresh)return CODEX_COMPANION_UNKNOWN;
    if(yes(claude,"needsInput")||yes(domestic,"needsInput"))return CODEX_COMPANION_WAITING;
    if(companion_busy(domestic))return companion_model_mood(domestic,true);
    codex_companion_state state=companion_model_mood(claude,true);
    if(state==CODEX_COMPANION_STOPPED&&!strcmp(text(domestic,"state"),"idle"))return CODEX_COMPANION_IDLE;
    return state;
}
static void claude_mood_update(codex_companion_state value,int64_t now) {
    if(value!=claude_mood){claude_mood=value;claude_mood_started=now;}
}
static void claude_pet_tick(claude_pet_view *view,codex_companion_state value,int64_t now,bool show);
static lv_obj_t *claude_pet_create(claude_pet_view *view,lv_obj_t *parent,int x,int y,int width,int height) {
    if(!CLAUDE_PET_AVAILABLE)return NULL;
    *view=(claude_pet_view){.x=x,.y=y,.width=width,.height=height,.last_frame=-1,.last_scale=-1};
    view->root=lv_obj_create(parent);lv_obj_remove_style_all(view->root);
    lv_obj_set_pos(view->root,x,y);lv_obj_set_size(view->root,width,height);
    lv_obj_remove_flag(view->root,LV_OBJ_FLAG_CLICKABLE|LV_OBJ_FLAG_SCROLLABLE);
    view->image=lv_image_create(view->root);
    lv_image_set_pivot(view->image,0,0);lv_image_set_antialias(view->image,false);
    lv_obj_remove_flag(view->image,LV_OBJ_FLAG_CLICKABLE|LV_OBJ_FLAG_SCROLLABLE);
    visible(view->root,false);return view->root;
}
static void claude_pet_tick(claude_pet_view *view,codex_companion_state value,int64_t now,bool show) {
    if(!view->root)return;
    visible(view->root,show);if(!show)return;
    if(!claude_pixels){claude_pixels=lv_malloc(CLAUDE_COMPANION_FRAME_BYTES);if(!claude_pixels)return;}
    claude_companion_frame_info info=claude_companion_frame(value,now-claude_mood_started,view==&saver_claude);
    if(info.frame!=claude_rendered_frame) {
        unsigned w,h;
        if(!claude_companion_decode(info.frame,claude_pixels,CLAUDE_COMPANION_FRAME_BYTES,&w,&h))return;
        lv_image_cache_drop(&claude_descriptor);
        claude_descriptor=(lv_image_dsc_t){.header={.magic=LV_IMAGE_HEADER_MAGIC,.cf=LV_COLOR_FORMAT_RGB565A8,
            .w=w,.h=h,.stride=w*2},.data_size=w*h*3,.data=claude_pixels};
        if(home_claude.image)lv_image_set_src(home_claude.image,&claude_descriptor);
        if(saver_claude.image)lv_image_set_src(saver_claude.image,&claude_descriptor);
        claude_rendered_frame=info.frame;
    }
    int scale=view==&home_claude?(int)info.scale_q8:(int)info.saver_scale_q8;
    int lift=view==&saver_claude?(int)info.saver_lift:0;
    if(view->last_frame!=(int)info.frame||view->last_scale!=scale||view->last_lift!=lift) {
        // Match the Codex overview actor center. Fixed clip anchors preserve
        // the original motion; transparent canvas size does not set alignment.
        int anchor=view==&home_claude?152:view->width/2;
        int center=view==&home_claude?134:322;
        int top=center-view->y-((int)info.anchor_y2*scale+256)/512-lift;
        lv_obj_set_pos(view->image,anchor-((int)info.anchor_x2*scale+256)/512,top);
        if(view->last_scale!=scale)lv_image_set_scale(view->image,scale);
        view->last_frame=(int)info.frame;view->last_scale=scale;view->last_lift=lift;
    }
}
