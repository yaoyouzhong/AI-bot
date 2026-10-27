#pragma once
#include "cJSON.h"
#include <stdbool.h>
#include <string.h>

// Bound retained allocations without serializing the tree. cJSON's floating
// number printer calls newlib sprintf and overflowed the 4 KiB NimBLE host
// stack on the real 0.2.65 device. This iterative walk uses a fixed small stack.
static bool telemetry_tree_bounded(const cJSON *root) {
    if(!root)return false;
    const cJSON *parents[17],*node=root;unsigned depth=0,nodes=0;size_t strings=0;
    for(;;) {
        if(++nodes>8192)return false;
        if(node->string)strings+=strlen(node->string)+1;
        if(node->valuestring)strings+=strlen(node->valuestring)+1;
        if(strings>65536)return false;
        if(node->child){if(depth==16)return false;parents[depth++]=node;node=node->child;continue;}
        while(node!=root&&!node->next)node=parents[--depth];
        if(node==root)return true;
        node=node->next;
    }
}
// Build a private replacement; allocation/validation failure must leave the
// current snapshot and its authenticated freshness untouched.
static cJSON *telemetry_delta_apply(const cJSON *baseline,const cJSON *changes) {
    if(!cJSON_IsObject(baseline)||!cJSON_IsArray(changes)||cJSON_GetArraySize(changes)>256||
       !telemetry_tree_bounded(baseline)||!telemetry_tree_bounded(changes))return NULL;
    cJSON *result=cJSON_Duplicate(baseline,true);if(!result)return NULL;
    const cJSON *op;
    cJSON_ArrayForEach(op,changes) {
        const cJSON *path=cJSON_GetObjectItemCaseSensitive(op,"path");
        const cJSON *value=cJSON_GetObjectItemCaseSensitive(op,"value");
        const cJSON *remove=cJSON_GetObjectItemCaseSensitive(op,"remove");
        int depth=cJSON_GetArraySize(path);
        if(!cJSON_IsObject(op)||!cJSON_IsArray(path)||depth<1||depth>16||
            (remove?(!cJSON_IsTrue(remove)||value!=NULL):value==NULL))goto fail;
        cJSON *parent=result;
        for(int i=0;i<depth;i++) {
            const cJSON *part=cJSON_GetArrayItem(path,i);
            if(!cJSON_IsString(part)||!part->valuestring[0]||strlen(part->valuestring)>128||!cJSON_IsObject(parent))goto fail;
            const char *key=part->valuestring;
            cJSON *old=cJSON_GetObjectItemCaseSensitive(parent,key);
            if(i+1<depth){if(!cJSON_IsObject(old))goto fail;parent=old;continue;}
            if(remove){if(!old)goto fail;cJSON_DeleteItemFromObjectCaseSensitive(parent,key);}
            else {
                cJSON *copy=cJSON_Duplicate(value,true);if(!copy)goto fail;
                bool ok=old?cJSON_ReplaceItemInObjectCaseSensitive(parent,key,copy):cJSON_AddItemToObject(parent,key,copy);
                if(!ok){cJSON_Delete(copy);goto fail;}
            }
        }
    }
    if(!cJSON_IsObject(cJSON_GetObjectItemCaseSensitive(result,"codex"))||!cJSON_IsObject(cJSON_GetObjectItemCaseSensitive(result,"claude")))goto fail;
    if(!telemetry_tree_bounded(result))goto fail;
    return result;
fail:
    cJSON_Delete(result);return NULL;
}
