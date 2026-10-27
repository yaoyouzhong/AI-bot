#include <assert.h>
#include <stdio.h>
#include "../../firmware/main/ble_keyboard_flow.h"

// Independent USB HID usage -> English US character oracle, including Caps Lock.
static char decode(uint8_t usage,uint8_t modifier,bool caps) {
    if(usage>=4&&usage<=29)return (char)((((modifier&2)!=0)^caps?'A':'a')+usage-4);
    if(usage>=0x1e&&usage<=0x27)return (modifier&2)?"!@#$%^&*()"[usage-0x1e]:"1234567890"[usage-0x1e];
    if(usage==0x2c)return ' ';
    if(usage>=0x2d&&usage<=0x38)return (modifier&2)?"_+{}|~:\"~<>?"[usage-0x2d]:"-=[]\\#;'`,./"[usage-0x2d];
    return 0;
}
static bool zero(const uint8_t *p,size_t n){for(size_t i=0;i<n;i++)if(p[i])return false;return true;}
int main(void) {
    for(unsigned caps=0;caps<2;caps++)for(unsigned c=32;c<=126;c++) {
        ble_keyboard_flow f={0};char text[1]={(char)c};
        assert(ble_keyboard_flow_start(&f,text,sizeof(text),true));
        for(unsigned i=0;i<2;i++) {
            uint8_t r[8];assert(ble_keyboard_flow_next(&f,true,r));ble_keyboard_caps_lock(r,caps);
            assert(r[0]<=2&&r[1]==0&&zero(r+3,5));
            if(i<1)assert(decode(r[2],r[0],caps)==(char)c);else assert(r[2]==0x28&&r[0]==0);
            ble_keyboard_flow_commit(&f,true);assert(f.keys[i]==0&&f.modifiers[i]==0);
            assert(ble_keyboard_flow_next(&f,true,r)&&zero(r,8));ble_keyboard_flow_commit(&f,true);
        }
        uint8_t r[8];assert(f.finished&&!f.failed&&!f.busy&&!ble_keyboard_flow_next(&f,true,r));
        assert(zero(f.keys,sizeof(f.keys))&&zero(f.modifiers,sizeof(f.modifiers)));
    }
    for(unsigned c=0;c<=255;c++)if(c<32||c>126) {
        ble_keyboard_flow f={0};char text[4]={'a',(char)c,'B','!'};
        assert(!ble_keyboard_flow_start(&f,text,4,true)&&zero(f.keys,sizeof(f.keys)));
    }
    for(unsigned stop=0;stop<10;stop++) {
        ble_keyboard_flow f={0};uint8_t r[8];assert(ble_keyboard_flow_start(&f,"Aa! ",4,true));
        for(unsigned i=0;i<stop&&f.busy;i++){assert(ble_keyboard_flow_next(&f,true,r));ble_keyboard_flow_commit(&f,true);}
        ble_keyboard_flow_cancel(&f);assert(zero(f.keys,sizeof(f.keys))&&zero(f.modifiers,sizeof(f.modifiers)));
        if(f.busy){assert(ble_keyboard_flow_next(&f,false,r)&&zero(r,8));ble_keyboard_flow_commit(&f,true);}
        assert(f.finished&&f.failed&&!f.busy&&!ble_keyboard_flow_next(&f,true,r));
    }
    ble_keyboard_flow f={0};char longest[65];memset(longest,'A',sizeof(longest));
    assert(!ble_keyboard_flow_start(&f,longest,65,true)&&!ble_keyboard_flow_start(&f,longest,0,true));
    assert(!ble_keyboard_flow_start(&f,longest,64,false)&&ble_keyboard_flow_start(&f,longest,64,true));
    unsigned reports=0;uint8_t r[8];while(ble_keyboard_flow_next(&f,true,r)){reports++;ble_keyboard_flow_commit(&f,true);}
    assert(reports==130&&f.finished&&!f.failed);
    assert(ble_keyboard_flow_display_wake(&f,true));assert(ble_keyboard_flow_next(&f,true,r)&&r[0]==2&&r[2]==0);ble_keyboard_flow_commit(&f,true);
    assert(ble_keyboard_flow_next(&f,true,r)&&zero(r,8));ble_keyboard_flow_commit(&f,true);
    puts("UNLOCK_ASCII_FLOW_OK: all 95 single-character credentials, both Caps Lock states, releases, 64-character limit, control rejection and cancellation wiping");
}
