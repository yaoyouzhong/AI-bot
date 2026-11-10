#pragma once
#include "cJSON.h"
#include <string.h>

// Only a typed, target-bound no-action receipt can prove that nothing was sent.
// Old bridges, lost replies and malformed receipts keep the unknown outcome.
static const char *desktop_submit_feedback(int status,const cJSON *reply,const char *task_id) {
    const cJSON *target=cJSON_GetObjectItemCaseSensitive(reply,"taskId");
    if(status!=409||!task_id||strlen(task_id)!=36||!cJSON_IsString(target)||strcmp(target->valuestring,task_id)||
       !cJSON_IsFalse(cJSON_GetObjectItemCaseSensitive(reply,"submitted"))||
       !cJSON_IsFalse(cJSON_GetObjectItemCaseSensitive(reply,"attempted")))
        return "未确认发送，请检查电脑；不要重复发送，可重新录音";
    const cJSON *value=cJSON_GetObjectItemCaseSensitive(reply,"error");
    const char *error=cJSON_IsString(value)?value->valuestring:"";
    if(!strcmp(error,"conversation_target_or_text_changed"))
        return "未发送，会话或输入内容已变化，请检查后重新录音";
    if(!strcmp(error,"question_target_or_text_changed"))
        return "未发送，题目或回答已变化，请检查后重新录音";
    if(!strcmp(error,"staged_target_or_text_changed")||!strcmp(error,"composer_changed"))
        return "未发送，请核对当前输入内容后重新录音";
    return "未发送，电脑尚未就绪，请检查后重新录音";
}
