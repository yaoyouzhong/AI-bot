#include "cJSON.h"
#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
// The withdrawn receiver fails this test: applying an update must not call
// the recursive JSON serializer/newlib floating-point printf on the BLE stack.
static char *forbidden_print(const cJSON *item){(void)item;assert(!"Delta reserialized on BLE receive stack");return NULL;}
#define cJSON_PrintUnformatted forbidden_print
#include "../../firmware/main/telemetry_delta.h"
#undef cJSON_PrintUnformatted
int main(void) {
    cJSON *base=cJSON_Parse("{\"codex\":{},\"claude\":{},\"series\":[]}"),*series=cJSON_GetObjectItemCaseSensitive(base,"series");
    for(int i=0;i<1500;i++)cJSON_AddItemToArray(series,cJSON_CreateNumber(i*0.123456789));
    cJSON *changes=cJSON_Parse("[{\"path\":[\"codex\",\"fraction\"],\"value\":0.1234567890123456}]");
    cJSON *next=telemetry_delta_apply(base,changes);assert(next&&cJSON_GetArraySize(cJSON_GetObjectItemCaseSensitive(next,"series"))==1500);
    assert(cJSON_GetObjectItemCaseSensitive(cJSON_GetObjectItemCaseSensitive(next,"codex"),"fraction")->valuedouble==0.1234567890123456);
    assert(!cJSON_GetObjectItemCaseSensitive(cJSON_GetObjectItemCaseSensitive(base,"codex"),"fraction"));cJSON_Delete(next);
    char *large=malloc(65538);memset(large,'x',65537);large[64000]=0;
    cJSON *op=cJSON_GetArrayItem(changes,0);cJSON_ReplaceItemInObjectCaseSensitive(op,"value",cJSON_CreateString(large));
    next=telemetry_delta_apply(base,changes);assert(next&&strlen(cJSON_GetObjectItemCaseSensitive(cJSON_GetObjectItemCaseSensitive(next,"codex"),"fraction")->valuestring)==64000);cJSON_Delete(next);
    large[64000]='x';large[65537]=0;cJSON_ReplaceItemInObjectCaseSensitive(op,"value",cJSON_CreateString(large));free(large);
    assert(!telemetry_delta_apply(base,changes));assert(!cJSON_GetObjectItemCaseSensitive(cJSON_GetObjectItemCaseSensitive(base,"codex"),"fraction"));
    cJSON *deep=cJSON_CreateArray(),*cursor=deep;for(int i=0;i<17;i++){cJSON *child=cJSON_CreateArray();cJSON_AddItemToArray(cursor,child);cursor=child;}
    assert(!telemetry_tree_bounded(deep));cJSON_Delete(deep);
    for(int i=1500;i<8192;i++)cJSON_AddItemToArray(series,cJSON_CreateNumber(i));
    assert(!telemetry_tree_bounded(base));cJSON_Delete(base);cJSON_Delete(changes);
    puts("TELEMETRY_DELTA_STACK_OK fractional catalog exact; no JSON printing; depth/node/string budgets; rejection preserves baseline");return 0;
}
