#include "link_state.h"
#include <string.h>
void link_state_init(link_state *s) { memset(s, 0, sizeof(*s)); s->mode = -1; for(int i=0;i<3;i++) s->received_ms[i]=-1; }
bool link_state_receive(link_state *s, link_kind link, const char *session, uint64_t seq, int64_t now) {
    if(link<0 || link>2 || !session || !*session || strlen(session)>=40 || seq==0 || now<0) return false;
    if(!strcmp(s->session[link],session) && seq<=s->sequence[link]) return false;
    strcpy(s->session[link],session); s->sequence[link]=seq; s->received_ms[link]=now; return true;
}
void link_state_disconnect(link_state *s,link_kind link) {
    if(link>=LINK_USB&&link<=LINK_BLE)s->received_ms[link]=-1;
}
link_kind link_state_active(const link_state *s, int64_t now) {
    for(int i=0;i<3;i++) if((s->mode==-1 || s->mode==i) && s->received_ms[i]>=0 && now>=s->received_ms[i] && now-s->received_ms[i]<8000) return (link_kind)i;
    return LINK_NONE;
}
const char *link_name(link_kind link) { return link==LINK_USB?"USB":link==LINK_WIFI?"Wi-Fi":link==LINK_BLE?"BLE":"Offline"; }
bool link_state_read_fresh(const link_state *s,link_kind link,int64_t now,int64_t confirmed_ms,const char *confirmed_session) {
    if(link<LINK_USB||link>LINK_BLE||(s->mode!=-1&&s->mode!=link)||s->received_ms[link]<0||now<s->received_ms[link])return false;
    int64_t age=now-s->received_ms[link];
    if(age<8000)return true;
    // A recently authenticated RPC proves the paired bridge is still serving
    // this session even while a BLE full-state frame waits behind page data.
    // Never treat an unauthenticated mailbox poll as a session heartbeat.
    return link!=LINK_WIFI&&age<=90000&&confirmed_ms>0&&now>=confirmed_ms&&now-confirmed_ms<8000&&
        confirmed_session&&s->session[link][0]&&!strcmp(s->session[link],confirmed_session);
}
link_kind link_state_ota(const link_state *s,int64_t now,bool wifi_ready,bool usb_rpc_ready) {
    const link_kind order[]={LINK_WIFI,LINK_USB};
    for(unsigned i=0;i<2;i++) {
        link_kind link=order[i];bool ready=link==LINK_WIFI?wifi_ready:usb_rpc_ready;
        if((s->mode==-1||s->mode==link)&&ready&&s->received_ms[link]>=0&&now>=s->received_ms[link]&&now-s->received_ms[link]<8000)return link;
    }
    return LINK_NONE;
}
link_kind link_state_image(const link_state *s,int64_t now,const char *session,bool wifi_ready,bool usb_rpc_ready,bool ble_rpc_ready) {
    if(!session||!*session)return LINK_NONE;
    const link_kind order[]={LINK_WIFI,LINK_USB,LINK_BLE};
    for(unsigned i=0;i<3;i++) {
        link_kind link=order[i];bool ready=link==LINK_WIFI?wifi_ready:link==LINK_USB?usb_rpc_ready:ble_rpc_ready;
        if((s->mode==-1||s->mode==link)&&ready&&s->received_ms[link]>=0&&now>=s->received_ms[link]&&now-s->received_ms[link]<8000&&
           !strcmp(s->session[link],session))return link;
    }
    return LINK_NONE;
}
link_kind link_state_ota_bulk(const link_state *s,int64_t now,bool wifi_ready,bool usb_rpc_ready,bool ble_rpc_ready) {
    link_kind fast=link_state_ota(s,now,wifi_ready,usb_rpc_ready);
    if(fast!=LINK_NONE)return fast;
    return (s->mode==-1||s->mode==LINK_BLE)&&ble_rpc_ready&&s->received_ms[LINK_BLE]>=0&&now>=s->received_ms[LINK_BLE]&&now-s->received_ms[LINK_BLE]<8000?LINK_BLE:LINK_NONE;
}
