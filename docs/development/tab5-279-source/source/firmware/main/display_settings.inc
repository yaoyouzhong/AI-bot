// Runs only in the LVGL task. Never changes Wi-Fi, pairing or connection mode.
static char display_request[33];
static cJSON *display_reply;
static int64_t display_retry,display_until;
static uint32_t display_hash_add(uint32_t hash,int value){return (hash^(uint32_t)value)*16777619u;}
static cJSON *display_values(const ui_preferences *p) {
    device_config cfg=device_get_config();device_sound sound=device_get_sound();
    uint32_t hash=2166136261u;
    int values[]={cfg.brightness,sound.volume,sound.muted,p->selected,p->cycle,p->interval,p->saver_minutes,p->alerts};
    for(unsigned i=0;i<sizeof(values)/sizeof(values[0]);i++)hash=display_hash_add(hash,values[i]);
    for(int i=0;i<8;i++){hash=display_hash_add(hash,p->enabled[i]);hash=display_hash_add(hash,p->order[i]);}
    char revision[9];snprintf(revision,sizeof(revision),"%08lx",(unsigned long)hash);
    cJSON *out=cJSON_CreateObject();cJSON_AddStringToObject(out,"revision",revision);
    cJSON_AddNumberToObject(out,"brightness",cfg.brightness);cJSON_AddNumberToObject(out,"volume",sound.volume);cJSON_AddBoolToObject(out,"muted",sound.muted);
    cJSON_AddNumberToObject(out,"selected",p->selected);cJSON_AddBoolToObject(out,"cycle",p->cycle);cJSON_AddNumberToObject(out,"interval",p->interval);
    cJSON_AddNumberToObject(out,"saverMinutes",p->saver_minutes);cJSON_AddBoolToObject(out,"alerts",p->alerts);
    cJSON *enabled=cJSON_AddArrayToObject(out,"enabled"),*order=cJSON_AddArrayToObject(out,"order");
    for(int i=0;i<8;i++){cJSON_AddItemToArray(enabled,cJSON_CreateNumber(p->enabled[i]));cJSON_AddItemToArray(order,cJSON_CreateNumber(p->order[i]));}return out;
}
static bool display_number(cJSON *root,const char *key,int min,int max,int *value) {
    cJSON *n=cJSON_GetObjectItemCaseSensitive(root,key);if(!cJSON_IsNumber(n)||n->valuedouble!=n->valueint||n->valueint<min||n->valueint>max)return false;*value=n->valueint;return true;
}
static bool display_decode(cJSON *s,ui_preferences *p,int *brightness,int *volume,bool *muted) {
    if(!display_number(s,"brightness",10,100,brightness)||!display_number(s,"volume",0,100,volume)||!display_number(s,"selected",-1,7,&p->selected)||
       !display_number(s,"interval",10,60,&p->interval)||!display_number(s,"saverMinutes",0,60,&p->saver_minutes))return false;
    if(p->interval!=10&&p->interval!=15&&p->interval!=30&&p->interval!=60)return false;
    if(p->saver_minutes!=0&&p->saver_minutes!=1&&p->saver_minutes!=5&&p->saver_minutes!=10&&p->saver_minutes!=30&&p->saver_minutes!=60)return false;
    cJSON *m=cJSON_GetObjectItemCaseSensitive(s,"muted"),*c=cJSON_GetObjectItemCaseSensitive(s,"cycle"),*a=cJSON_GetObjectItemCaseSensitive(s,"alerts");
    if(!cJSON_IsBool(m)||!cJSON_IsBool(c)||!cJSON_IsBool(a))return false;
    *muted=cJSON_IsTrue(m);p->cycle=cJSON_IsTrue(c);p->alerts=cJSON_IsTrue(a);
    if(p->cycle&&p->selected!=-1)return false;
    cJSON *enabled=cJSON_GetObjectItemCaseSensitive(s,"enabled"),*order=cJSON_GetObjectItemCaseSensitive(s,"order");
    if(!cJSON_IsArray(enabled)||!cJSON_IsArray(order)||cJSON_GetArraySize(enabled)!=8||cJSON_GetArraySize(order)!=8)return false;
    unsigned seen=0,any=0;
    for(int i=0;i<8;i++){cJSON *e=cJSON_GetArrayItem(enabled,i),*o=cJSON_GetArrayItem(order,i);
        if(!cJSON_IsNumber(e)||e->valuedouble!=e->valueint||e->valueint<0||e->valueint>1||!cJSON_IsNumber(o)||o->valuedouble!=o->valueint||o->valueint<0||o->valueint>7)return false;
        p->enabled[i]=e->valueint;p->order[i]=o->valueint;seen|=1u<<o->valueint;any|=e->valueint;
    }return seen==255&&any;
}
static bool display_settings_tick(cJSON *frame,ui_preferences *prefs,link_kind link,int64_t now,int64_t received,bool editing) {
    cJSON *health=cJSON_GetObjectItemCaseSensitive(frame,"connectionHealth"),*command=cJSON_GetObjectItemCaseSensitive(health,"displayCommand");
    cJSON *clock=cJSON_GetObjectItemCaseSensitive(frame,"epochMilliseconds");
    const char *id=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(command,"requestId"));
    cJSON *version=cJSON_GetObjectItemCaseSensitive(command,"version"),*expires=cJSON_GetObjectItemCaseSensitive(command,"expiresAt");
    bool changed=false;
    if(link!=LINK_NONE&&now-received>=0&&now-received<6000&&cJSON_IsNumber(clock)&&cJSON_IsNumber(expires)&&cJSON_IsNumber(version)&&version->valuedouble==1&&
       expires->valuedouble>=clock->valuedouble+now-received&&expires->valuedouble<=clock->valuedouble+30000&&id&&strlen(id)==32&&strcmp(display_request,id)) {
        bool valid_id=true;for(int i=0;i<32;i++)if(!((id[i]>='0'&&id[i]<='9')||(id[i]>='a'&&id[i]<='f')))valid_id=false;
        if(!valid_id)return false;
        snprintf(display_request,sizeof(display_request),"%s",id);cJSON_Delete(display_reply);display_reply=NULL;
        cJSON *before=display_values(prefs),*desired=cJSON_GetObjectItemCaseSensitive(command,"settings");const char *error="";bool ok=true;
        if(cJSON_IsObject(desired)) {
            const char *expected=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(desired,"revision"));
            const char *revision=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(before,"revision"));
            ui_preferences candidate=*prefs;int brightness,volume;bool muted;
            if(editing){ok=false;error="device_busy";}
            else if(!expected||!revision||strcmp(expected,revision)){ok=false;error="conflict";}
            else if(!display_decode(desired,&candidate,&brightness,&volume,&muted)){ok=false;error="invalid_settings";}
            else {
                device_config old_config=device_get_config();device_sound old_sound=device_get_sound();
                ok=device_save_brightness(brightness)&&device_set_sound(volume,muted)&&ui_preferences_save(&candidate);
                if(ok){*prefs=candidate;changed=true;}
                else {device_save_brightness(old_config.brightness);device_set_sound(old_sound.volume,old_sound.muted);error="save_failed";}
            }
        }else if(!cJSON_IsNull(desired)){ok=false;error="invalid_settings";}
        cJSON_Delete(before);display_reply=cJSON_CreateObject();
        cJSON_AddStringToObject(display_reply,"kind","display-settings");cJSON_AddStringToObject(display_reply,"requestId",id);
        cJSON_AddBoolToObject(display_reply,"ok",ok);cJSON_AddStringToObject(display_reply,"error",error);cJSON_AddItemToObject(display_reply,"settings",display_values(prefs));
        display_until=now+20000;display_retry=0;
    }
    if(display_reply&&link!=LINK_NONE&&now>=display_retry&&now<display_until&&cJSON_IsNumber(clock)&&now-received>=0&&now-received<6000) {
        cJSON *copy=cJSON_Duplicate(display_reply,true),*input=cJSON_GetObjectItemCaseSensitive(frame,"pcInput");
        const char *session=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(input,"session"));
        if(copy&&session){cJSON_AddStringToObject(copy,"session",session);cJSON_AddNumberToObject(copy,"issuedAt",clock->valuedouble+now-received);if(!display_settings_report(copy,link))cJSON_Delete(copy);}else cJSON_Delete(copy);
        display_retry=now+2000;
    }
    return changed;
}
