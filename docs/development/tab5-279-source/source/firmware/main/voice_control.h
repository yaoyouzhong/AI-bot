#pragma once
#include <stdatomic.h>
#include <stdbool.h>
#include <stdint.h>

/* Status mode is -1 auto, 0 USB, 1 Wi-Fi, 2 BLE. Unsupported fixed modes
 * must fail visibly rather than silently submitting over another transport. */
static inline int voice_transport_select(bool associated,int64_t received,int64_t now,bool ble_ready,int mode) {
    bool wifi_ready=associated&&received>=0&&now>=received&&now-received<8000;
    if(mode==0)return -1;
    if(mode==2)return ble_ready?2:-1;
    if(wifi_ready)return 1;
    return mode!=1&&ble_ready?2:-1;
}
static inline int voice_transport_select_with_usb(bool associated,int64_t received,int64_t now,bool ble_ready,int mode,bool usb_ready) {
    if(mode==0)return usb_ready?0:-1;
    if(mode==-1&&usb_ready)return 0;
    int selected=voice_transport_select(associated,received,now,ble_ready,mode);
    return selected;
}

/* Both endpoints must confirm capture. The first TAB5 audio acknowledgement
 * proves its microphone opened and data reached the bridge. Silence is valid. */
static inline bool voice_capture_ready(bool recording,bool host_ready,bool tab5_source,
                                      bool audio_acknowledged,bool stopping,bool failed) {
    return recording&&host_ready&&(!tab5_source||audio_acknowledged)&&!stopping&&!failed;
}

/* Cancel wins over stop, and stop wins over a source switch. Keep commands
 * queued during the start handshake so an early tap cannot disappear. */
static inline int voice_command_priority(int command) {
    return command==2?3:command==1?2:command==3?1:0;
}
static inline void voice_command_post(atomic_int *pending,int command) {
    int previous=atomic_load(pending);
    while(voice_command_priority(command)>voice_command_priority(previous)) {
        if(atomic_compare_exchange_weak(pending,&previous,command))return;
    }
}
static inline int voice_command_take(atomic_int *pending,bool starting) {
    int command=atomic_load(pending);
    for(;;) {
        if(starting&&command!=2)return 0;
        if(atomic_compare_exchange_weak(pending,&command,0))return command;
    }
}
