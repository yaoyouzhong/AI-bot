#include "agenda_core.h"
#include <stdatomic.h>
#include <string.h>
#include <time.h>
#include <math.h>
static bool identity(const char *v){if(!v||strlen(v)!=32)return false;for(int i=0;i<32;i++)if(!((v[i]>='0'&&v[i]<='9')||(v[i]>='a'&&v[i]<='f')))return false;return true;}
static int weekday(int64_t civil){return (int)((civil/86400+4)%7);}
#include "agenda_workdays.inc"
bool agenda_occurrence_valid(const agenda_item *i,int64_t occurrence) {
    if(!i->due)return occurrence==1;
    if(occurrence<i->due||(occurrence-i->due)%86400)return false;
    int day=weekday(occurrence);
    return i->repeat==0?occurrence==i->due:i->repeat==1?true:i->repeat==2?day!=0&&day!=6:i->repeat==3?(occurrence-i->due)%(7*86400)==0:i->repeat==4&&agenda_workday_at(occurrence)==1;
}
int64_t agenda_ends_at(const agenda_item *i,int64_t occurrence){return occurrence+(i->end?i->end-i->due:0);}
bool agenda_expired(const agenda_item *i,int64_t now){return now>0&&i->due>0&&!i->repeat&&now>=agenda_ends_at(i,i->due);}
int agenda_oldest_expired(const agenda_document *document,int64_t now) {
    int oldest=-1;
    for(unsigned n=0;n<document->count;n++) {
        const agenda_item *item=&document->items[n];if(!agenda_expired(item,now))continue;
        int64_t end=agenda_ends_at(item,item->due);
        if(oldest<0||end<agenda_ends_at(&document->items[oldest],document->items[oldest].due)||
           (end==agenda_ends_at(&document->items[oldest],document->items[oldest].due)&&strcmp(item->id,document->items[oldest].id)<0))oldest=(int)n;
    }
    return oldest;
}
agenda_view agenda_view_at(const agenda_item *i,int64_t now) {
    if(!i->due)return (agenda_view){1,0,i->done>=1,false};
    int64_t occurrence=i->due;
    if(i->repeat) {
        int64_t end=agenda_ends_at(i,i->due);occurrence=i->due+(now>=end?(now-end)/86400+1:0)*86400;
        if(i->done>=occurrence)occurrence=i->done+86400;
        bool found=false;
        for(int n=0;n<370;n++,occurrence+=86400) {
            if(i->repeat==4&&agenda_workday_at(occurrence)<0)return (agenda_view){i->due,INT64_MAX,false,true};
            if(agenda_occurrence_valid(i,occurrence)&&occurrence>i->done){found=true;break;}
        }
        if(!found)return (agenda_view){i->due,INT64_MAX,false,true};
    }
    if(i->repeat&&i->snooze_occurrence>i->done&&i->snooze_until>0&&agenda_ends_at(i,i->snooze_occurrence)>now)occurrence=i->snooze_occurrence;
    return (agenda_view){occurrence,i->snooze_occurrence==occurrence&&i->snooze_until>0?i->snooze_until:occurrence-i->lead*60,!i->repeat&&(i->done>=occurrence||now>=agenda_ends_at(i,occurrence)),false};
}
bool agenda_document_valid(const agenda_document *v) {
    if(!v||v->version!=1||v->count>AGENDA_LIMIT||!memchr(v->revision,0,sizeof(v->revision))||!identity(v->revision))return false;
    for(unsigned n=0;n<v->count;n++) {
        const agenda_item *i=&v->items[n];
        if(!memchr(i->id,0,sizeof(i->id))||!memchr(i->edit,0,sizeof(i->edit))||!memchr(i->title,0,sizeof(i->title))||!memchr(i->base_edit,0,sizeof(i->base_edit))||!identity(i->id)||!identity(i->edit)||!i->title[0]||i->repeat>4||i->lead>60||i->dirty>7||(i->base_edit[0]&&!identity(i->base_edit)))return false;
        unsigned characters=0;bool nonspace=false;for(const unsigned char *t=(const unsigned char*)i->title;*t;t++){if(*t<32||*t==127)return false;if((*t&0xc0)!=0x80)characters++;nonspace|=*t!=' ';}if(characters>32||!nonspace)return false;
        if((i->due&&(i->due<946684800LL||i->due>4102444799LL))||(!i->due&&(i->repeat||i->lead)))return false;
        if(i->end&&(!i->due||i->end<=i->due||i->end-i->due>86400||i->end>4102531199LL))return false;
        int64_t stored[]={i->done,i->snooze_occurrence};for(unsigned j=0;j<2;j++)if(stored[j]) {
            if(i->repeat==4){if(stored[j]<i->due||(stored[j]-i->due)%86400)return false;}
            else if(!agenda_occurrence_valid(i,stored[j]))return false;
        }
        if(i->snooze_until<0||i->snooze_until>4105123200LL)return false;
        for(unsigned j=0;j<n;j++)if(!strcmp(i->id,v->items[j].id))return false;
    }
    return true;
}
static bool str(const cJSON *o,const char *key,char *out,size_t capacity){const cJSON *v=cJSON_GetObjectItemCaseSensitive(o,key);if(!cJSON_IsString(v)||strlen(v->valuestring)>=capacity)return false;strcpy(out,v->valuestring);return true;}
static bool number(const cJSON *o,const char *key,int64_t *out){const cJSON *v=cJSON_GetObjectItemCaseSensitive(o,key);if(!cJSON_IsNumber(v)||!isfinite(v->valuedouble)||v->valuedouble<0||v->valuedouble>4105123200LL||floor(v->valuedouble)!=v->valuedouble)return false;*out=(int64_t)v->valuedouble;return true;}
bool agenda_parse(const cJSON *root,agenda_document *out) {
    if(!out||!cJSON_IsObject(root))return false;
    memset(out,0,sizeof(*out));int64_t version=0;
    const cJSON *items=cJSON_GetObjectItemCaseSensitive(root,"items");
    if(!number(root,"version",&version)||version!=1||!str(root,"revision",out->revision,sizeof(out->revision))||!cJSON_IsArray(items)||cJSON_GetArraySize(items)>AGENDA_LIMIT)return false;
    out->version=1;out->count=cJSON_GetArraySize(items);
    for(unsigned n=0;n<out->count;n++) {
        agenda_item *i=&out->items[n];const cJSON *o=cJSON_GetArrayItem(items,n);int64_t repeat,lead;
        if(!str(o,"id",i->id,sizeof(i->id))||!str(o,"edit",i->edit,sizeof(i->edit))||!str(o,"title",i->title,sizeof(i->title))||!number(o,"due",&i->due)||!number(o,"done",&i->done)||!number(o,"snoozeOccurrence",&i->snooze_occurrence)||!number(o,"snoozeUntil",&i->snooze_until)||!number(o,"repeat",&repeat)||!number(o,"lead",&lead)||repeat>4||lead>60)return false;
        if(cJSON_HasObjectItem(o,"end")&&!number(o,"end",&i->end))return false;
        i->repeat=repeat;i->lead=lead;
    }
    return agenda_document_valid(out);
}
bool agenda_apply(agenda_item *i,int64_t occurrence,bool complete,int64_t until) {
    if(!agenda_occurrence_valid(i,occurrence))return false;
    if(complete&&occurrence<=i->done)return true;
    if(complete){if(occurrence>i->done)i->done=occurrence;if(i->snooze_occurrence<=occurrence)i->snooze_occurrence=i->snooze_until=0;}
    else {if(!i->due||i->done>=occurrence||until<occurrence-i->lead*60||until>=agenda_ends_at(i,occurrence))return false;i->snooze_occurrence=occurrence;i->snooze_until=until;}
    i->dirty=(i->dirty&4)|(complete&&i->snooze_occurrence<=i->done?1:2);return true;
}
cJSON *agenda_item_json(const agenda_item *i) {
    cJSON *o=cJSON_CreateObject();if(!o)return NULL;
    cJSON_AddStringToObject(o,"id",i->id);cJSON_AddStringToObject(o,"edit",i->edit);cJSON_AddStringToObject(o,"title",i->title);
    cJSON_AddNumberToObject(o,"due",i->due);cJSON_AddNumberToObject(o,"end",i->end);cJSON_AddNumberToObject(o,"repeat",i->repeat);cJSON_AddNumberToObject(o,"lead",i->lead);
    cJSON_AddNumberToObject(o,"done",i->done);cJSON_AddNumberToObject(o,"snoozeOccurrence",i->snooze_occurrence);cJSON_AddNumberToObject(o,"snoozeUntil",i->snooze_until);return o;
}
