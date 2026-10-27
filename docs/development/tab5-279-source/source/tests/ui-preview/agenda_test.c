#undef NDEBUG
#include "agenda_core.h"
#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
static char *read_file(const char *path){FILE *f=fopen(path,"rb");assert(f);fseek(f,0,SEEK_END);long n=ftell(f);rewind(f);char *text=calloc(n+1,1);assert(text&&fread(text,1,n,f)==(size_t)n);fclose(f);return text;}
int main(int argc,char **argv) {
    assert(argc==2);char *text=read_file(argv[1]);cJSON *cases=cJSON_Parse(text);assert(cJSON_IsArray(cases));free(text);
    unsigned count=0;agenda_document d;
    const cJSON *test=NULL;cJSON_ArrayForEach(test,cases) {
        assert(agenda_parse(test,&d)&&d.count==1);int64_t now=cJSON_GetObjectItemCaseSensitive(test,"now")->valuedouble;
        agenda_view v=agenda_view_at(&d.items[0],now);
        assert(v.occurrence==(int64_t)cJSON_GetObjectItemCaseSensitive(test,"occurrence")->valuedouble);
        if(v.calendar_pending)assert(v.alert_at==INT64_MAX);
        else assert(v.alert_at==(int64_t)cJSON_GetObjectItemCaseSensitive(test,"alertAt")->valuedouble);
        assert(v.complete==cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(test,"complete")));count++;
        assert(v.calendar_pending==cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(test,"calendarPending")));
        if(cJSON_HasObjectItem(test,"expired"))assert(agenda_expired(&d.items[0],now)==cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(test,"expired")));
        assert(agenda_ends_at(&d.items[0],v.occurrence)==(int64_t)cJSON_GetObjectItemCaseSensitive(test,"endsAt")->valuedouble);
        agenda_document restored=d;assert(agenda_document_valid(&restored)&&!memcmp(&d,&restored,sizeof(d)));
        agenda_item *i=&d.items[0];if(i->due&&!v.complete&&!v.calendar_pending) {
            int64_t until=now+600;bool allowed=until>=v.occurrence-i->lead*60&&until<agenda_ends_at(i,v.occurrence);
            assert(agenda_apply(i,v.occurrence,false,until)==allowed);if(allowed)assert(i->dirty==2);
            assert(agenda_apply(i,v.occurrence,true,0));assert(i->dirty==1&&i->done>=v.occurrence&&i->snooze_until==0);
        }
    }
    cJSON *bad=cJSON_Duplicate(cJSON_GetArrayItem(cases,0),true);cJSON_ReplaceItemInObjectCaseSensitive(bad,"items",cJSON_CreateString("invalid"));assert(!agenda_parse(bad,&d));cJSON_Delete(bad);
    assert(!agenda_parse(cJSON_GetArrayItem(cases,0),NULL));
    cJSON_Delete(cases);printf("AGENDA_CORE_OK %u cross-language recurrence/lead/overnight/automatic-end fixtures; bounded validation and actions\n",count);return 0;
}
