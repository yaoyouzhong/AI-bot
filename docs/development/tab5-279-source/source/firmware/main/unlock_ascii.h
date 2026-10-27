#pragma once
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
#include <string.h>

// USB/BLE HID uses the host's English (US) layout. Never approximate or
// truncate unsupported characters into a different credential.
static inline bool unlock_ascii_key(unsigned char c,uint8_t *usage,uint8_t *modifier) {
    *usage=*modifier=0;
    if(c>='a'&&c<='z'){*usage=(uint8_t)(4+c-'a');return true;}
    if(c>='A'&&c<='Z'){*usage=(uint8_t)(4+c-'A');*modifier=2;return true;}
    if(c>='1'&&c<='9'){*usage=(uint8_t)(0x1e + c - '1');return true;}
    if(c=='0'){*usage=0x27;return true;}
    if(c==' '){*usage=0x2c;return true;}
    const char *shifted_digits="!@#$%^&*()";
    const char *p=strchr(shifted_digits,c);
    if(c&&p){*usage=(uint8_t)(0x1e + (p - shifted_digits));*modifier=2;return true;}
    const char *plain="`-=[]\\;',./",*shifted="~_+{}|:\"<>?";
    static const uint8_t codes[]={0x35,0x2d,0x2e,0x2f,0x30,0x31,0x33,0x34,0x36,0x37,0x38};
    p=strchr(plain,c);
    if(c&&p){*usage=codes[p-plain];return true;}
    p=strchr(shifted,c);
    if(c&&p){*usage=codes[p-shifted];*modifier=2;return true;}
    return false;
}
