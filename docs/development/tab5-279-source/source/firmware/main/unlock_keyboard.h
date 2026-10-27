#pragma once
#include "ble_keyboard.h"
#include "link_state.h"
void usb_keyboard_get(ble_keyboard_state *out);
bool usb_keyboard_selected(void);
bool usb_keyboard_start(const char *pin,size_t n,unsigned connection,const char *id);
bool usb_keyboard_wake(const char *id,unsigned connection,bool display);
void usb_keyboard_cancel(void);
// Selection is latched for this modal. Never fail over after any key was sent.
static bool unlock_using_usb;
static inline void unlock_keyboard_select(bool pairing) {
    unlock_using_usb=!pairing&&usb_keyboard_selected();
}
static inline link_kind unlock_keyboard_link(void){return unlock_using_usb?LINK_USB:LINK_BLE;}
static inline void unlock_keyboard_get(ble_keyboard_state *s){if(unlock_using_usb)usb_keyboard_get(s);else ble_keyboard_get(s);}
static inline void unlock_keyboard_cancel(void){if(unlock_using_usb)usb_keyboard_cancel();else ble_keyboard_cancel();}
static inline bool unlock_keyboard_start(const char *p,size_t n,unsigned c,const char *id){return unlock_using_usb?usb_keyboard_start(p,n,c,id):ble_keyboard_start(p,n,c,id);}
static inline bool unlock_keyboard_wake(const char *id,unsigned c){return unlock_using_usb?usb_keyboard_wake(id,c,false):ble_keyboard_wake(id,c);}
static inline bool unlock_keyboard_display_wake(const char *id,unsigned c){return unlock_using_usb?usb_keyboard_wake(id,c,true):ble_keyboard_display_wake(id,c);}
