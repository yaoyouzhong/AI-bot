#pragma once
#include <stdbool.h>
#include <stdint.h>

// One boot/one immutable offer only. The writer and its SHA state stay alive;
// no partition is erased again, and failed reads never advance the checkpoint.
typedef struct {
    unsigned retries, consecutive;
    int64_t started_ms, gap_ms;
    bool in_gap;
} ota_resume;
static bool ota_resume_transient(int result) {
    return result==-2 || result==-1005 || result==-408 || result==-429 ||
        result==-500 || result==-502 || result==-503 || result==-504;
}
static bool ota_resume_retry(ota_resume *s,int result,int64_t now,unsigned *delay_ms) {
    if(!s || !delay_ms || !ota_resume_transient(result) || now<s->started_ms ||
       now-s->started_ms>=15*60*1000 || s->retries>=32)return false;
    if(!s->in_gap){s->gap_ms=now;s->in_gap=true;s->consecutive=0;}
    if(now<s->gap_ms || now-s->gap_ms>=90000 || s->consecutive>=8)return false;
    *delay_ms=250u << (s->consecutive<4?s->consecutive:4);
    s->consecutive++;s->retries++;return true;
}
static void ota_resume_progress(ota_resume *s){s->in_gap=false;s->consecutive=0;}

// Waiting for a physically absent link does not spend all request attempts
// before Windows can finish rediscovery, but the same time budgets still apply.
static bool ota_resume_wait_budget(const ota_resume *s,int64_t now) {
    return s&&s->in_gap&&now>=s->gap_ms&&now>=s->started_ms&&
        now-s->gap_ms<90000&&now-s->started_ms<15*60*1000;
}
