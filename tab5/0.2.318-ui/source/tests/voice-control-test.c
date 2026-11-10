#include <assert.h>
#include <stdio.h>
#include "voice_control.h"

int main(void) {
    assert(voice_transport_select(true,100,7999,true,-1)==1);
    assert(voice_transport_select(true,100,8100,true,-1)==2);
    assert(voice_transport_select(true,-1,100,true,-1)==2);
    assert(voice_transport_select(true,101,100,true,-1)==2);
    assert(voice_transport_select(false,100,101,false,-1)==-1);
    assert(voice_transport_select(true,100,101,true,2)==2);
    assert(voice_transport_select(true,100,101,false,2)==-1);
    assert(voice_transport_select(false,100,101,true,1)==-1);
    assert(voice_transport_select(true,100,101,true,0)==-1);
    assert(voice_transport_select(false,100,101,true,0)==-1);
    assert(voice_transport_select_with_usb(true,100,101,true,0,true)==0);
    assert(voice_transport_select_with_usb(true,100,101,true,0,false)==-1);
    assert(voice_transport_select_with_usb(true,100,101,true,-1,true)==0);
    assert(voice_transport_select_with_usb(true,100,101,true,-1,false)==1);
    assert(voice_transport_select_with_usb(false,100,101,true,-1,true)==0);
    assert(voice_transport_select_with_usb(false,100,101,true,1,true)==-1);
    assert(voice_transport_select_with_usb(true,100,101,false,2,true)==-1);
    atomic_int pending=0;
    assert(!voice_capture_ready(true,false,false,false,false,false));
    assert(!voice_capture_ready(true,true,true,false,false,false));
    assert(voice_capture_ready(true,true,true,true,false,false));
    assert(voice_capture_ready(true,true,false,false,false,false));
    assert(!voice_capture_ready(true,true,true,true,true,false));
    assert(!voice_capture_ready(true,true,true,true,false,true));
    assert(!voice_capture_ready(false,true,true,true,false,false));
    voice_command_post(&pending,1);
    assert(voice_command_take(&pending,true)==0);
    assert(voice_command_take(&pending,false)==1);
    voice_command_post(&pending,3);
    voice_command_post(&pending,2);
    voice_command_post(&pending,1);
    voice_command_post(&pending,3);
    assert(voice_command_take(&pending,true)==2);
    assert(voice_command_take(&pending,false)==0);
    voice_command_post(&pending,1);
    voice_command_post(&pending,3);
    assert(voice_command_take(&pending,false)==1);
    voice_command_post(&pending,3);
    assert(voice_command_take(&pending,true)==0);
    assert(voice_command_take(&pending,false)==3);
    puts("VOICE_CONTROL_TEST_OK early stop, early cancel, cancel priority, delayed switch");
    return 0;
}
