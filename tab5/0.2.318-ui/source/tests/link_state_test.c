#include "link_state.h"
#include <assert.h>
#include <stdio.h>
int main(void) {
    link_state s;link_state_init(&s);
    assert(link_state_active(&s,0)==LINK_NONE);
    assert(link_state_receive(&s,LINK_BLE,"bridge-a",1,100));
    assert(link_state_active(&s,100)==LINK_BLE);
    assert(link_state_receive(&s,LINK_WIFI,"bridge-a",1,101));
    assert(link_state_receive(&s,LINK_USB,"bridge-a",1,102));
    assert(link_state_active(&s,102)==LINK_USB);
    assert(!link_state_receive(&s,LINK_USB,"bridge-a",1,5000));
    assert(!link_state_receive(&s,LINK_USB,"bridge-a",0,5000));
    assert(link_state_receive(&s,LINK_WIFI,"bridge-a",2,8000));
    assert(link_state_active(&s,8102)==LINK_WIFI);
    s.mode=LINK_USB;assert(link_state_active(&s,8102)==LINK_NONE);
    s.mode=LINK_BLE;assert(link_state_active(&s,8102)==LINK_NONE);
    assert(link_state_receive(&s,LINK_BLE,"bridge-a",3,8103));
    assert(link_state_active(&s,8103)==LINK_BLE);
    s.mode=-1;assert(link_state_active(&s,8103)==LINK_WIFI);
    assert(link_state_receive(&s,LINK_USB,"bridge-b",1,8104));
    assert(link_state_active(&s,8104)==LINK_USB);
    assert(link_state_active(&s,16104)==LINK_NONE);
    assert(!link_state_receive(&s,LINK_NONE,"bridge-b",2,17000));
    link_state_init(&s);s.mode=LINK_BLE;
    assert(link_state_receive(&s,LINK_USB,"bridge",1,100));
    assert(link_state_receive(&s,LINK_WIFI,"bridge",1,101));
    assert(link_state_receive(&s,LINK_BLE,"bridge",1,102));
    assert(link_state_ota(&s,102,true,true)==LINK_NONE&&s.mode==LINK_BLE);
    s.mode=LINK_USB;assert(link_state_ota(&s,102,true,true)==LINK_USB);
    s.mode=LINK_WIFI;assert(link_state_ota(&s,102,false,true)==LINK_NONE);
    s.mode=-1;assert(link_state_ota(&s,102,true,true)==LINK_WIFI);
    assert(link_state_ota(&s,102,false,true)==LINK_USB);
    assert(link_state_ota(&s,102,false,false)==LINK_NONE);
    assert(link_state_ota(&s,8101,true,true)==LINK_NONE);
    assert(link_state_ota(&s,99,true,true)==LINK_NONE);
    assert(link_state_receive(&s,LINK_USB,"bridge",2,8200));
    assert(link_state_ota(&s,8200,true,true)==LINK_USB);
    s.mode=LINK_BLE;
    assert(link_state_active(&s,10000)==LINK_NONE);
    assert(link_state_read_fresh(&s,LINK_BLE,10000,9999,"bridge"));
    assert(!link_state_read_fresh(&s,LINK_BLE,10000,0,"bridge"));
    assert(!link_state_read_fresh(&s,LINK_BLE,10000,9999,"other-session"));
    assert(!link_state_read_fresh(&s,LINK_BLE,18000,10000,"bridge"));
    assert(!link_state_read_fresh(&s,LINK_BLE,10000,10001,"bridge"));
    assert(!link_state_read_fresh(&s,LINK_BLE,90103,90102,"bridge"));
    assert(!link_state_read_fresh(&s,LINK_WIFI,10000,9999,"bridge"));
    s.mode=LINK_USB;assert(!link_state_read_fresh(&s,LINK_BLE,10000,9999,"bridge"));
    link_state_init(&s);assert(link_state_receive(&s,LINK_USB,"bridge",7,100));
    assert(link_state_receive(&s,LINK_WIFI,"bridge",7,100));
    link_state_disconnect(&s,LINK_USB);assert(link_state_active(&s,100)==LINK_WIFI);
    assert(!link_state_receive(&s,LINK_USB,"bridge",7,101));
    assert(!link_state_read_fresh(&s,LINK_USB,101,101,"bridge"));
    s.mode=LINK_USB;assert(link_state_active(&s,101)==LINK_NONE);
    assert(link_state_receive(&s,LINK_USB,"bridge",8,102));assert(link_state_active(&s,102)==LINK_USB);
    link_state_disconnect(&s,LINK_NONE);assert(link_state_active(&s,102)==LINK_USB);
    puts("TAB5_LINK_STATE_OK daily/OTA fixed mode binding; read-only authenticated same-session freshness bounded");
    link_state_init(&s);
    for(int i=0;i<3;i++)assert(link_state_receive(&s,i,"same-session",1,100));
    assert(link_state_image(&s,101,"same-session",true,true,true)==LINK_WIFI);
    assert(link_state_image(&s,101,"same-session",false,true,true)==LINK_USB);
    assert(link_state_image(&s,101,"same-session",false,false,true)==LINK_BLE);
    for(int mode=0;mode<3;mode++){
        s.mode=mode;assert(link_state_image(&s,101,"same-session",true,true,true)==mode);
        assert(link_state_image(&s,101,"same-session",mode!=LINK_WIFI,mode!=LINK_USB,mode!=LINK_BLE)==LINK_NONE);
        assert(s.mode==mode);
    }
    s.mode=-1;
    assert(link_state_image(&s,8100,"same-session",true,true,true)==LINK_NONE);
    assert(link_state_image(&s,99,"same-session",true,true,true)==LINK_NONE);
    assert(link_state_image(&s,101,"other-session",true,true,true)==LINK_NONE);
    assert(link_state_receive(&s,LINK_WIFI,"other-session",2,102));
    assert(link_state_image(&s,102,"same-session",true,true,true)==LINK_USB);
    puts("PHOTO_ROUTE_OK auto bandwidth priority; every fixed mode binding; no stale/cross-session path");
    link_state_init(&s);for(int i=0;i<3;i++)assert(link_state_receive(&s,i,"ota-session",1,100));
    assert(link_state_ota_bulk(&s,101,true,true,true)==LINK_WIFI);
    assert(link_state_ota_bulk(&s,101,false,true,true)==LINK_USB);
    assert(link_state_ota_bulk(&s,101,false,false,true)==LINK_BLE);
    for(int mode=0;mode<3;mode++) {
        s.mode=mode;assert(link_state_ota_bulk(&s,101,true,true,true)==mode);
        assert(link_state_ota_bulk(&s,101,mode!=LINK_WIFI,mode!=LINK_USB,mode!=LINK_BLE)==LINK_NONE);
    }
    s.mode=-1;assert(link_state_ota_bulk(&s,8101,true,true,true)==LINK_NONE);
    puts("OTA_BULK_ROUTE_OK auto bandwidth order; fixed BLE/USB/WiFi; unavailable paths rejected");
}
