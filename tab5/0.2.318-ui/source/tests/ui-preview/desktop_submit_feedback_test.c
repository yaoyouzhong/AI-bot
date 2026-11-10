#include <assert.h>
#include <stdio.h>
#include "../../firmware/main/desktop_submit_feedback.h"

int main(void) {
    const char *id="11111111-1111-1111-1111-111111111111";
    cJSON *reply=cJSON_Parse("{\"taskId\":\"11111111-1111-1111-1111-111111111111\",\"submitted\":false,\"attempted\":false,\"error\":\"staged_target_or_text_changed\"}");
    assert(reply);
    assert(strstr(desktop_submit_feedback(409,reply,id),"未发送，请核对当前输入内容"));
    cJSON_SetValuestring(cJSON_GetObjectItemCaseSensitive(reply,"error"),"composer_changed");
    assert(strstr(desktop_submit_feedback(409,reply,id),"未发送，请核对当前输入内容"));
    cJSON_SetValuestring(cJSON_GetObjectItemCaseSensitive(reply,"error"),"conversation_target_or_text_changed");
    assert(strstr(desktop_submit_feedback(409,reply,id),"未发送，会话或输入内容已变化"));
    cJSON_SetValuestring(cJSON_GetObjectItemCaseSensitive(reply,"error"),"question_target_or_text_changed");
    assert(strstr(desktop_submit_feedback(409,reply,id),"未发送，题目或回答已变化"));
    cJSON_ReplaceItemInObjectCaseSensitive(reply,"attempted",cJSON_CreateTrue());
    assert(strstr(desktop_submit_feedback(409,reply,id),"不要重复发送"));
    cJSON_ReplaceItemInObjectCaseSensitive(reply,"attempted",cJSON_CreateFalse());
    cJSON_SetValuestring(cJSON_GetObjectItemCaseSensitive(reply,"error"),"native_submit_unavailable");
    assert(strstr(desktop_submit_feedback(409,reply,id),"未发送，电脑尚未就绪"));
    assert(strstr(desktop_submit_feedback(409,reply,"22222222-2222-2222-2222-222222222222"),"未确认发送"));
    assert(strstr(desktop_submit_feedback(200,reply,id),"未确认发送"));
    assert(strstr(desktop_submit_feedback(0,NULL,id),"未确认发送"));
    cJSON_ReplaceItemInObjectCaseSensitive(reply,"attempted",cJSON_CreateTrue());
    assert(strstr(desktop_submit_feedback(409,reply,id),"不要重复发送"));
    cJSON_ReplaceItemInObjectCaseSensitive(reply,"attempted",cJSON_CreateString("false"));
    assert(strstr(desktop_submit_feedback(409,reply,id),"未确认发送"));
    cJSON_DeleteItemFromObjectCaseSensitive(reply,"attempted");
    assert(strstr(desktop_submit_feedback(409,reply,id),"未确认发送"));
    cJSON_AddFalseToObject(reply,"attempted");
    cJSON_ReplaceItemInObjectCaseSensitive(reply,"submitted",cJSON_CreateTrue());
    assert(strstr(desktop_submit_feedback(409,reply,id),"未确认发送"));
    cJSON_DeleteItemFromObjectCaseSensitive(reply,"submitted");
    assert(strstr(desktop_submit_feedback(409,reply,id),"未确认发送"));
    cJSON_Delete(reply);
    puts("DESKTOP_SUBMIT_FEEDBACK_OK cases=14 changed_text no_action unknown malformed old_bridge wrong_target");
    return 0;
}
