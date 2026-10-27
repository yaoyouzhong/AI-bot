#undef NDEBUG
#include <assert.h>
#include <stdint.h>
#include <stdbool.h>
#include <stdatomic.h>
#include <stdio.h>
#include <string.h>
#define ESP_OK 0
#define WIFI_IF_STA 0
#define WIFI_REASON_NO_AP_FOUND 201
#define WIFI_ALL_CHANNEL_SCAN 1
#define WIFI_CONNECT_AP_BY_SIGNAL 0
typedef struct {char ssid[33],password[65];} device_config;
typedef struct {struct {uint8_t ssid[32],password[64],channel;bool bssid_set;int scan_method,sort_method;} sta;} wifi_config_t;
typedef struct {uint8_t *ssid;bool show_hidden;} wifi_scan_config_t;
typedef struct {uint8_t ssid[33];int8_t rssi;uint8_t primary;} wifi_ap_record_t;
static bool tab5_wifi_initialized=true,wifi_connected;
static atomic_int disconnect_reason;
static wifi_config_t applied;
static int set_rc,connect_rc,scan_rc,get_rc,scan_calls,connect_calls;
static int stop_rc,start_rc,stop_calls,start_calls;
static int record_count=2,records_rc;
static int64_t now=1000;
static int64_t esp_timer_get_time(void){return now*1000;}
static int esp_wifi_disconnect(void){return 0;}
static int esp_wifi_stop(void){stop_calls++;return stop_rc;}
static int esp_wifi_start(void){start_calls++;return start_rc;}
static int esp_wifi_set_config(int iface,const wifi_config_t *config){assert(iface==0);if(!set_rc)applied=*config;return set_rc;}
static int esp_wifi_connect(void){connect_calls++;return connect_rc;}
static int esp_wifi_get_config(int iface,wifi_config_t *config){assert(iface==0);*config=applied;return get_rc;}
static int esp_wifi_scan_start(const wifi_scan_config_t *scan,bool block){assert(block&&scan->show_hidden&&!strcmp((char *)scan->ssid,"synthetic-ap"));scan_calls++;return scan_rc;}
static int esp_wifi_scan_get_ap_records(uint16_t *count,wifi_ap_record_t *records){assert(*count==4);*count=(uint16_t)record_count;memset(records,0,2*sizeof(*records));for(int i=0;i<record_count;i++){strcpy((char *)records[i].ssid,"synthetic-ap");records[i].rssi=-60+i*15;records[i].primary=1+i*8;}return records_rc;}
#include "../../firmware/main/wifi_connection_diagnostics.inc"
int main(void) {
    device_config cfg={.ssid="synthetic-ap",.password="synthetic-only-pass"},before=cfg;
    assert(wifi_apply_configuration(&cfg)&&connect_calls==1&&!memcmp(&before,&cfg,sizeof(cfg)));
    assert(!memcmp(applied.sta.password,cfg.password,strlen(cfg.password))&&atomic_load(&wifi_ssid_matches)==1);
    assert(applied.sta.scan_method==WIFI_ALL_CHANNEL_SCAN&&applied.sta.sort_method==WIFI_CONNECT_AP_BY_SIGNAL);
    // An in-flight association is not interrupted by the old ten-second retry.
    now+=10000;wifi_retry_connection(&cfg);assert(connect_calls==1&&!scan_calls);
    assert(!wifi_advance_profile(&cfg,15,2));
    // Association succeeded, DHCP gets its own deadline, independent of loop count.
    wifi_connection_phase(2);now+=59000;
    assert(wifi_connection_waiting()&&!wifi_advance_profile(&cfg,100,2));
    now+=1001;assert(!wifi_connection_waiting());
    wifi_connected=true;atomic_store(&disconnect_reason,201);wifi_retry_connection(&cfg);
    assert(!scan_calls&&connect_calls==1);
    wifi_connected=false;wifi_connection_phase(0);atomic_store(&disconnect_reason,202);
    assert(wifi_advance_profile(&cfg,5,2));wifi_retry_connection(&cfg);assert(!scan_calls&&connect_calls==2);
    // Multiple saved profiles must scan too: a visible selected AP is retained.
    wifi_connection_phase(0);atomic_store(&disconnect_reason,201);
    assert(!wifi_advance_profile(&cfg,15,2));
    assert(scan_calls==1&&atomic_load(&wifi_scan_matches)==2&&atomic_load(&wifi_scan_rssi)==-45&&atomic_load(&wifi_scan_channel)==9);
    wifi_retry_connection(&cfg);assert(connect_calls==3);
    now+=10000;wifi_retry_connection(&cfg);assert(scan_calls==1&&connect_calls==3);
    now+=20000;wifi_connection_phase(0);scan_rc=123;
    assert(!wifi_advance_profile(&cfg,15,2));
    assert(scan_calls==2&&atomic_load(&wifi_scan_rc)==123&&atomic_load(&wifi_scan_matches)==-1);
    // Driver failure is not evidence that an SSID is absent.
    now+=30000;scan_rc=0;records_rc=234;
    assert(!wifi_advance_profile(&cfg,15,2)&&atomic_load(&wifi_scan_rc)==234);
    now+=30000;records_rc=0;record_count=0;
    assert(wifi_advance_profile(&cfg,15,2)&&atomic_load(&wifi_scan_matches)==0);
    assert(!wifi_advance_profile(&cfg,15,1));
    connect_rc=321;wifi_request_connection(&cfg);
    assert(!wifi_connection_waiting()&&atomic_load(&wifi_connect_rc)==321);
    set_rc=456;assert(!wifi_apply_configuration(&cfg)&&atomic_load(&wifi_config_rc)==456);
    get_rc=789;wifi_observe_configuration(&cfg);assert(atomic_load(&wifi_get_config_rc)==789&&atomic_load(&wifi_ssid_matches)==-1);
    char report[192];tab5_wifi_numeric_diagnostics(report,sizeof(report));
    assert(!strstr(report,cfg.ssid)&&!strstr(report,cfg.password));
    unsigned separators=0;for(const char *p=report;*p;p++){if(*p==',')separators++;else assert((*p>='0'&&*p<='9')||*p=='-');}
    assert(separators==15&&report[0]=='1'&&!memcmp(&before,&cfg,sizeof(cfg)));
    // A successful connect API with no subsequent events must not retry forever.
    set_rc=get_rc=connect_rc=0;atomic_store(&wifi_timeout_phase,0);wifi_connection_phase(0);
    assert(wifi_apply_configuration(&cfg));int requests=connect_calls;
    now+=29999;assert(!wifi_recover_connection(&cfg,false)&&!stop_calls&&connect_calls==requests);
    now++;assert(wifi_recover_connection(&cfg,false)&&stop_calls==1&&start_calls==1&&connect_calls==requests+1);
    assert(atomic_load(&wifi_timeouts)>=1&&atomic_load(&wifi_restarts)==1&&wifi_connection_waiting());
    // A second silent stall waits for the 120-second cooldown, without cycling profiles.
    now+=30000;assert(wifi_recover_connection(&cfg,false)&&stop_calls==1);
    now+=89999;assert(wifi_recover_connection(&cfg,false)&&stop_calls==1);
    now++;start_rc=123;assert(wifi_recover_connection(&cfg,false)&&stop_calls==2&&start_calls==2&&wifi_restart_pending);
    now+=1999;assert(wifi_recover_connection(&cfg,false)&&start_calls==2);
    now++;start_rc=0;assert(wifi_recover_connection(&cfg,false)&&stop_calls==2&&start_calls==3&&!wifi_restart_pending);
    // Healthy connections never enter automatic recovery, even with a stale timeout.
    wifi_connected=true;now+=200000;assert(!wifi_recover_connection(&cfg,false)&&stop_calls==2);
    wifi_connected=false;wifi_connection_phase(2);now+=59999;assert(!wifi_recover_connection(&cfg,false));
    now++;assert(wifi_recover_connection(&cfg,false)&&stop_calls==3);
    // Manual reconnect gets the same driver restart, without the automatic cooldown.
    assert(wifi_recover_connection(&cfg,true)&&stop_calls==4);
    now+=120000;wifi_connection_phase(1);now+=30000;stop_rc=456;
    assert(wifi_recover_connection(&cfg,false)&&stop_calls==5&&start_calls==5);
    now+=9999;assert(wifi_recover_connection(&cfg,false)&&stop_calls==5);
    now++;stop_rc=0;assert(wifi_recover_connection(&cfg,false)&&stop_calls==6&&start_calls==6);
    wifi_connected=true;assert(!wifi_recover_connection(&cfg,false));wifi_connected=false;
    wifi_connection_phase(0);atomic_store(&disconnect_reason,201);atomic_store(&wifi_scan_rc,0);atomic_store(&wifi_scan_matches,0);
    assert(!wifi_recover_connection(&cfg,false));now+=119999;assert(!wifi_recover_connection(&cfg,false));
    now++;assert(wifi_recover_connection(&cfg,false)&&stop_calls==7);
    assert(!memcmp(&before,&cfg,sizeof(cfg))&&!memcmp(applied.sta.password,cfg.password,strlen(cfg.password)));
    tab5_wifi_recovery_diagnostics(report,sizeof(report));separators=0;
    for(const char *p=report;*p;p++){if(*p==',')separators++;else assert((*p>='0'&&*p<='9')||*p=='-');}assert(separators==7);
    puts("WIFI_CONNECTION_DIAGNOSTICS_OK association and DHCP deadlines, multi-profile targeted scan, absent-only no-AP rotation, scan errors retained, driver errors retained, credentials preserved and absent from numeric output");
    puts("WIFI_RECOVERY_OK silent association/DHCP stalls, 120-second cooldown, manual restart, stop/start failures, bounded retry, healthy-link protection and unchanged credentials");
}
