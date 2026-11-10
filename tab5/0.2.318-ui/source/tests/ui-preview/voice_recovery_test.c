#include "voice_recovery.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>
typedef struct {
    int64_t now,online_at,cancel_at;
    int calls,scenario,terminals;
    bool stop_seen,poll_seen;
    char phase[20],text[128],message[256];
} test_state;
static int64_t now(void *c){return ((test_state*)c)->now;}
static void wait_ms(void *c,unsigned ms){((test_state*)c)->now+=ms;}
static bool cancelled(void *c){test_state *t=c;return t->cancel_at>=0&&t->now>=t->cancel_at;}
static bool ready(void *c){test_state *t=c;return t->now>=t->online_at;}
static cJSON *request(void *c,const char *op,const char *id) {
    test_state *t=c;t->calls++;assert(!strcmp(id,"original-voice"));
    assert(!strcmp(op,"stop")||!strcmp(op,"poll"));
    if(!strcmp(op,"stop"))t->stop_seen=true;else t->poll_seen=true;
    if(t->scenario==1&&t->calls==1)return NULL; /* stop accepted, response lost */
    if(t->scenario==2)t->now=t->cancel_at; /* cancel while request is in flight */
    if(t->scenario==6)t->now+=121000; /* reply arrived after the recovery deadline */
    cJSON *r=cJSON_CreateObject();
    cJSON_AddStringToObject(r,"voiceId",t->scenario==3?"different-voice":id);
    cJSON_AddStringToObject(r,"taskId",t->scenario==4?"different-task":"original-task");
    cJSON_AddStringToObject(r,"state",t->scenario==5?"cancelled":t->calls==1?"recognizing":"review");
    cJSON_AddStringToObject(r,"text",t->scenario==5?"":"recovered text");return r;
}
static void publish(void *c,const char *phase,const char *msg,const char *text) {
    test_state *t=c;snprintf(t->phase,sizeof(t->phase),"%s",phase);snprintf(t->message,sizeof(t->message),"%s",msg);
    if(text)snprintf(t->text,sizeof(t->text),"%s",text);
    if(strcmp(phase,"recovering"))t->terminals++;
}
static void run(test_state *t) {
    voice_recovery_io io={t,now,wait_ms,cancelled,ready,request,publish};
    voice_recovery_run(&io,"original-voice","original-task");assert(t->terminals==1);
}
int main(void) {
    test_state t={.online_at=45000,.cancel_at=-1};run(&t);
    assert(t.stop_seen&&t.poll_seen&&t.calls==2&&!strcmp(t.phase,"review")&&!strcmp(t.text,"recovered text"));
    t=(test_state){.cancel_at=-1,.scenario=1};run(&t);assert(t.calls==2&&!strcmp(t.phase,"review"));
    t=(test_state){.online_at=999999,.cancel_at=1000};strcpy(t.text,"old partial");run(&t);assert(!strcmp(t.phase,"cancelled")&&!t.calls&&!t.text[0]);
    t=(test_state){.cancel_at=1000,.scenario=2};run(&t);assert(!strcmp(t.phase,"cancelled")&&!t.text[0]);
    for(int i=3;i<=6;i++) {
        t=(test_state){.cancel_at=-1,.scenario=i};strcpy(t.text,"old partial");run(&t);
        assert(!strcmp(t.phase,"error")&&!strcmp(t.text,"old partial"));
    }
    t=(test_state){.online_at=999999,.cancel_at=-1};run(&t);assert(!strcmp(t.phase,"error")&&t.now==120000&&!t.calls);
    puts("VOICE_RECOVERY_OK original take, delayed reconnect, lost stop reply, cancel race, identity isolation, empty result and bounded timeout; no audio/start/send operations");return 0;
}
