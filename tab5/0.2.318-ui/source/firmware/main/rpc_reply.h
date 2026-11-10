#pragma once
#include "cJSON.h"
// Transfer body ownership without allocating a second tree during a bulk reply.
static inline cJSON *rpc_reply_body(cJSON *root,int *status) {
    cJSON *code=cJSON_GetObjectItemCaseSensitive(root,"status"),*body=cJSON_GetObjectItemCaseSensitive(root,"body");
    if(!cJSON_IsNumber(code)||code->valuedouble!=code->valueint||code->valueint<100||code->valueint>599||!cJSON_IsObject(body)){
        *status=-1006;return NULL;
    }
    *status=code->valueint;return cJSON_DetachItemFromObjectCaseSensitive(root,"body");
}
