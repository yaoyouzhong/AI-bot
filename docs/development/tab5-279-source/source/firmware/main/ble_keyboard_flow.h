#pragma once
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
#include <string.h>
#include "unlock_ascii.h"

// Transport-independent, one-shot keyboard transaction. Key usages are secret
// too: erase them on consumption, cancel and failure; never persist or log them.
typedef struct { uint8_t keys[65],modifiers[65]; size_t count,index; bool busy,release,finished,failed; } ble_keyboard_flow;
static inline void ble_keyboard_wipe(void *p,size_t n){volatile uint8_t *b=p;while(n--)*b++=0;}
static inline bool ble_keyboard_flow_wake(ble_keyboard_flow *f,bool ready) {
    if(!ready||f->busy)return false;
    ble_keyboard_wipe(f,sizeof(*f));f->keys[0]=0x2c;f->count=1;f->busy=true;return true;
}
static inline bool ble_keyboard_flow_display_wake(ble_keyboard_flow *f,bool ready) {
    if(!ready||f->busy)return false;
    ble_keyboard_wipe(f,sizeof(*f));f->keys[0]=0xe1;f->count=1;f->busy=true;return true;
}
static inline bool ble_keyboard_flow_start(ble_keyboard_flow *f,const char *pin,size_t n,bool ready) {
    if(!ready||f->busy||!pin||n<1||n>64)return false;
    for(size_t i=0;i<n;i++)if((unsigned char)pin[i]<0x20||(unsigned char)pin[i]>0x7e)return false;
    ble_keyboard_wipe(f,sizeof(*f));
    for(size_t i=0;i<n;i++)unlock_ascii_key((unsigned char)pin[i],&f->keys[i],&f->modifiers[i]);
    f->keys[n]=0x28;f->count=n+1;f->busy=true;return true;
}
static inline void ble_keyboard_flow_cancel(ble_keyboard_flow *f) {
    bool release=f->release;ble_keyboard_wipe(f,sizeof(*f));
    f->release=release;f->busy=release;f->finished=!release;f->failed=true;
}
// Returns one report. Caller reports success/failure with commit. Never retry a
// digit: an uncertain send aborts the transaction, with a best-effort key-up.
static inline bool ble_keyboard_flow_next(ble_keyboard_flow *f,bool ready,uint8_t report[8]) {
    memset(report,0,8);
    if(!f->busy)return false;
    if(!ready)ble_keyboard_flow_cancel(f);
    if(!f->busy)return false;
    if(!f->release) {
        if(f->keys[f->index]==0xe1)report[0]=0x02; // Left Shift only; no key-array character.
        else {report[2]=f->keys[f->index];report[0]=f->modifiers[f->index];}
    }
    return true;
}
static inline void ble_keyboard_flow_commit(ble_keyboard_flow *f,bool sent) {
    if(!sent){ble_keyboard_flow_cancel(f);return;}
    if(f->release){f->release=false;if(f->failed||f->index==f->count){f->busy=false;f->finished=true;}}
    else {f->modifiers[f->index]=0;f->keys[f->index++]=0;f->release=true;}
}
static inline void ble_keyboard_caps_lock(uint8_t report[8],bool caps) {
    if(caps&&report[2]>=4&&report[2]<=0x1d)report[0]^=2;
}
