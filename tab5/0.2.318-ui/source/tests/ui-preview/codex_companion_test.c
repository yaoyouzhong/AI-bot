#undef NDEBUG
#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "codex_companion.h"

static codex_companion_value resolve(const char *json,bool fresh,bool unread) {
    cJSON *root=cJSON_Parse(json);assert(root);
    codex_companion_value value=codex_companion_resolve(root,cJSON_GetObjectItemCaseSensitive(root,"task"),fresh,unread,"unread-turn");
    cJSON_Delete(root);return value;
}
int main(void) {
    const char *working="{\"codex\":{\"state\":\"working\"},\"task\":{\"id\":\"task-a\",\"turnId\":\"turn-a\",\"state\":\"idle\",\"companionState\":\"working\"}}";
    assert(resolve(working,true,false).state==CODEX_COMPANION_WORKING);
    assert(resolve(working,true,true).state==CODEX_COMPANION_WORKING); // Old unread reply is not a completed current turn.
    assert(resolve(working,false,true).state==CODEX_COMPANION_UNKNOWN);
    assert(resolve("{\"codex\":{\"state\":\"offline\"},\"task\":{\"state\":\"working\"}}",true,false).state==CODEX_COMPANION_STOPPED);
    assert(resolve("{\"codex\":{\"state\":\"idle\"},\"task\":{\"id\":\"task-a\",\"state\":\"idle\"}}",true,false).state==CODEX_COMPANION_IDLE);
    assert(resolve("{\"task\":{\"id\":\"task-a\",\"state\":\"working\",\"companionState\":\"unknown\",\"outcome\":\"failed\"}}",true,true).state==CODEX_COMPANION_UNKNOWN);
    assert(resolve("{\"task\":{\"id\":\"task-a\",\"companionState\":\"idle\",\"outcome\":\"failed\"}}",true,false).state==CODEX_COMPANION_BLOCKED);
    assert(resolve("{\"task\":{\"id\":\"task-a\",\"companionState\":\"waiting\",\"waitingKind\":\"answer\"}}",true,false).state==CODEX_COMPANION_QUESTION);
    assert(resolve("{\"task\":{\"id\":\"task-a\",\"companionState\":\"waiting\",\"waitingKind\":\"confirm\"}}",true,false).state==CODEX_COMPANION_APPROVAL);
    assert(resolve("{\"task\":{\"id\":\"task-a\",\"state\":\"waiting\"}}",true,false).state==CODEX_COMPANION_WAITING);
    assert(resolve("{\"codex\":{\"needsInput\":true},\"task\":{\"id\":\"other-task\",\"state\":\"working\"}}",true,false).state==CODEX_COMPANION_WORKING);
    assert(resolve("{\"task\":{\"id\":\"task-a\",\"companionState\":\"idle\"}}",true,true).state==CODEX_COMPANION_DONE);
    assert(resolve("{}",true,false).state==CODEX_COMPANION_UNKNOWN);
    codex_companion_animation animation={0};codex_companion_value value=resolve(working,true,false);
    assert(codex_companion_select(&animation,value,1000));
    assert(codex_companion_frame(&animation,1000)==6&&codex_companion_frame(&animation,1120)==7);
    assert(!codex_companion_select(&animation,value,1500)&&animation.started_at==1000);
    value.state=CODEX_COMPANION_DONE;strcpy(value.turn,"completed-a");
    assert(codex_companion_select(&animation,value,2000));
    assert(codex_companion_frame(&animation,5000)==17);
    assert(codex_companion_saver_frame(&animation,5000)==21&&animation.started_at==2000);
    assert(codex_companion_frame(&animation,5000)==17);
    assert(!codex_companion_select(&animation,value,6000));
    strcpy(value.turn,"completed-b");assert(codex_companion_select(&animation,value,6000));
    assert(codex_companion_frame(&animation,6000)==16);
    value.state=CODEX_COMPANION_STOPPED;codex_companion_select(&animation,value,7000);assert(codex_companion_frame(&animation,9000)==1);
    value.state=CODEX_COMPANION_UNKNOWN;codex_companion_select(&animation,value,9000);assert(codex_companion_frame(&animation,9000)==26);
    uint16_t palette[256]={0};uint8_t alpha[256]={0};palette[1]=0x1234;alpha[1]=200;
    uint8_t output[9];memset(output,77,sizeof(output));
    const uint8_t repeated[]={0x82,1},truncated[]={1,1},overflow[]={0x83,1};
    assert(!codex_companion_unpack(truncated,sizeof(truncated),palette,alpha,output,3));
    assert(!codex_companion_unpack(overflow,sizeof(overflow),palette,alpha,output,3));
    for(unsigned i=0;i<sizeof(output);i++)assert(output[i]==77); // No partial corrupted frame replaces visible data.
    assert(codex_companion_unpack(repeated,sizeof(repeated),palette,alpha,output,3));
    assert(output[0]==0x34&&output[1]==0x12&&output[6]==200&&output[8]==200);
    if(codex_companion_asset_available()) {
        uint8_t *pixels=malloc(CODEX_COMPANION_FRAME_BYTES),*closed=malloc(CODEX_COMPANION_FRAME_BYTES);assert(pixels&&closed);
        for(unsigned i=0;i<27;i++)assert(codex_companion_decode(i,pixels,CODEX_COMPANION_FRAME_BYTES));
        assert(!codex_companion_decode(27,pixels,CODEX_COMPANION_FRAME_BYTES));
        assert(!codex_companion_decode(0,pixels,CODEX_COMPANION_FRAME_BYTES-1));
        assert(codex_companion_decode(1,closed,CODEX_COMPANION_FRAME_BYTES)&&codex_companion_decode(26,pixels,CODEX_COMPANION_FRAME_BYTES));
        assert(memcmp(pixels,closed,CODEX_COMPANION_FRAME_BYTES));free(pixels);free(closed);
    }
    puts("CODEX_COMPANION_OK precise_wait_kinds, lifecycle_not_silence, source_failure, matching_task, one_shot_completion, shared_timeline, bounded_decode");
    return 0;
}
