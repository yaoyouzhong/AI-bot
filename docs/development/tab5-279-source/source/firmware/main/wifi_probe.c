#ifndef TAB5_WIFI_PROBE_TEST
#include "wifi_probe.h"
#include "wifi_status.h"
#include "esp_wifi.h"
#include "esp_timer.h"
#endif
#include <stdatomic.h>
#include <stdint.h>
#include <stdio.h>

enum {PROBE_IDLE,PROBE_ARMING,PROBE_REQUESTED,PROBE_STOPPED,PROBE_RESTORING,PROBE_QUIET};
static atomic_int probe_stage,stop_rc,start_rc;
static atomic_bool requested,expired,restored;
static atomic_bool requires_usb;
static atomic_bool rpc_bulk_scope;
// Expiry restores the radio but does not transfer cancellation ownership to a
// new job. The old caller must release before another lease can start.
static atomic_bool owner_active;
static atomic_uint held_ms;
// RPC success can renew the inactivity deadline while the worker observes it.
// Other jobs retain their original absolute deadline. Driver calls stay owned
// by the Wi-Fi worker; callers never revive a restoring or expired lease.
static atomic_int_fast64_t deadline_ms,quiet_until_ms;
static int64_t stopped_ms,retry_ms;
static bool begin(unsigned duration,bool usb,bool rpc_bulk) {
    if(!tab5_wifi_initialized)return false;
    // Keep the original hard deadline: a stream of new pictures must not
    // indefinitely extend a radio pause. QUIET owns an already stopped radio.
    if(!usb) {
        int held=PROBE_QUIET;
        if(atomic_compare_exchange_strong(&probe_stage,&held,PROBE_ARMING)) {
            if(esp_timer_get_time()/1000<atomic_load(&deadline_ms)&&atomic_load(&requested)&&
               rpc_bulk==atomic_load(&rpc_bulk_scope)) {
                atomic_store(&owner_active,true);atomic_store(&probe_stage,PROBE_STOPPED);return true;
            }
            atomic_store(&probe_stage,PROBE_QUIET);return false;
        }
    }
    int expected=PROBE_IDLE;
    if(!atomic_compare_exchange_strong(&probe_stage,&expected,PROBE_ARMING))return false;
    if(atomic_load(&owner_active)){atomic_store(&probe_stage,PROBE_IDLE);return false;}
    atomic_store(&owner_active,true);
    atomic_store(&deadline_ms,esp_timer_get_time()/1000+duration);stopped_ms=retry_ms=0;
    atomic_store(&stop_rc,-1);atomic_store(&start_rc,-1);atomic_store(&held_ms,0);
    atomic_store(&expired,false);atomic_store(&restored,false);atomic_store(&requested,true);
    atomic_store(&requires_usb,usb);
    atomic_store(&rpc_bulk_scope,rpc_bulk);
    atomic_store(&probe_stage,PROBE_REQUESTED);return true;
}
bool wifi_probe_begin(void){return begin(35000,true,false);}
bool wifi_probe_begin_firmware(void){return begin(125000,true,false);}
bool wifi_probe_begin_gallery(void){return begin(35000,false,false);}
bool wifi_probe_begin_rpc_bulk(void){return begin(35000,false,true);}
bool wifi_probe_requires_usb(void){return atomic_load(&requires_usb);}
void wifi_probe_end(void){atomic_store(&requested,false);atomic_store(&owner_active,false);}
void wifi_probe_release_gallery(void) {
    int expected=PROBE_STOPPED;
    atomic_store(&quiet_until_ms,esp_timer_get_time()/1000+600);
    if(!atomic_load(&requires_usb)&&atomic_load(&requested)&&
       atomic_compare_exchange_strong(&probe_stage,&expected,PROBE_QUIET)) {
        atomic_store(&owner_active,false);return;
    }
    wifi_probe_end();
}
void wifi_probe_release_rpc_bulk(bool succeeded) {
    int expected=PROBE_STOPPED;
    if(succeeded&&atomic_load(&rpc_bulk_scope)&&atomic_load(&requested)&&
       atomic_compare_exchange_strong(&probe_stage,&expected,PROBE_ARMING)) {
        int64_t now=esp_timer_get_time()/1000;
        if(atomic_load(&requested)&&now<atomic_load(&deadline_ms)) {
            // Renew only after a complete authenticated RPC succeeded. A
            // stuck request, failed response or idle gap cannot keep Wi-Fi off.
            atomic_store(&deadline_ms,now+35000);
            atomic_store(&quiet_until_ms,now+600);
            atomic_store(&owner_active,false);
            atomic_store(&probe_stage,PROBE_QUIET);return;
        }
        wifi_probe_end();atomic_store(&probe_stage,PROBE_STOPPED);return;
    }
    wifi_probe_end();
}
bool wifi_probe_ready(void){return atomic_load(&requested)&&atomic_load(&probe_stage)==PROBE_STOPPED;}
bool wifi_probe_pending(void){return atomic_load(&probe_stage)!=PROBE_IDLE;}
bool wifi_probe_service(bool allowed,bool *reconnect) {
    *reconnect=false;int stage=atomic_load(&probe_stage);
    if(stage==PROBE_IDLE)return false;
    if(stage==PROBE_ARMING)return true;
    int64_t now=esp_timer_get_time()/1000;
    if(now>=atomic_load(&deadline_ms)){atomic_store(&expired,true);atomic_store(&requested,false);}
    if(!allowed)atomic_store(&requested,false);
    if(stage==PROBE_QUIET) {
        if(atomic_load(&requested)&&now<atomic_load(&quiet_until_ms))return true;
        int expected=PROBE_QUIET;
        // A successor may have reclaimed the pause. Only the winner of this
        // transition may restart the driver; never restore under a new owner.
        if(!atomic_compare_exchange_strong(&probe_stage,&expected,PROBE_RESTORING))return true;
        atomic_store(&requested,false);stage=PROBE_RESTORING;
        atomic_store(&held_ms,(unsigned)(now-stopped_ms));
    }
    if(stage==PROBE_REQUESTED) {
        if(!atomic_load(&requested)){atomic_store(&probe_stage,PROBE_IDLE);return false;}
        int rc=esp_wifi_stop();atomic_store(&stop_rc,rc);
        if(rc!=ESP_OK){atomic_store(&requested,false);atomic_store(&probe_stage,PROBE_IDLE);return false;}
        stopped_ms=esp_timer_get_time()/1000;
        // Cancellation can arrive during the driver call; never publish READY
        // unless the request is still live. Restoration remains worker-owned.
        stage=PROBE_STOPPED;atomic_store(&probe_stage,stage);
    }
    if(stage==PROBE_STOPPED) {
        now=esp_timer_get_time()/1000;atomic_store(&held_ms,(unsigned)(now-stopped_ms));
        if(now>=atomic_load(&deadline_ms)){atomic_store(&expired,true);atomic_store(&requested,false);}
        if(atomic_load(&requested))return true;
        int expected=PROBE_STOPPED;
        if(!atomic_compare_exchange_strong(&probe_stage,&expected,PROBE_RESTORING))return true;
        stage=PROBE_RESTORING;
    }
    if(stage==PROBE_RESTORING) {
        if(now<retry_ms)return true;
        int rc=esp_wifi_start();atomic_store(&start_rc,rc);
        if(rc!=ESP_OK){retry_ms=esp_timer_get_time()/1000+1000;return true;}
        atomic_store(&restored,true);*reconnect=true;
        atomic_store(&probe_stage,PROBE_IDLE);return false;
    }
    return false;
}
void wifi_probe_diagnostics(char *out,size_t size) {
    snprintf(out,size,"1,%d,%d,%d,%u,%d,%d",atomic_load(&probe_stage),atomic_load(&stop_rc),
        atomic_load(&start_rc),atomic_load(&held_ms),atomic_load(&restored)?1:0,atomic_load(&expired)?1:0);
}
