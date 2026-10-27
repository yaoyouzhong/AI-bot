#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stddef.h>
#include <string.h>
#define TAB5_WIFI_PROBE_TEST
#define ESP_OK 0
static bool tab5_wifi_initialized=true,wifi_running=true,cancel_in_stop;
static int stop_error,start_errors,stops,starts;
static int64_t now_ms=1000;
static int64_t esp_timer_get_time(void){return now_ms*1000;}
void wifi_probe_end(void);
static int esp_wifi_stop(void){stops++;if(cancel_in_stop)wifi_probe_end();if(stop_error)return stop_error;wifi_running=false;return 0;}
static int esp_wifi_start(void){starts++;if(start_errors){start_errors--;return 257;}wifi_running=true;return 0;}
#include "../../firmware/main/wifi_probe.c"
static bool reconnect;
static void reset(void){
    atomic_store(&probe_stage,PROBE_IDLE);atomic_store(&requested,false);atomic_store(&owner_active,false);
    tab5_wifi_initialized=wifi_running=true;cancel_in_stop=false;stop_error=start_errors=stops=starts=0;now_ms=1000;
}
int main(void){
    reset();assert(!wifi_probe_service(true,&reconnect)&&!reconnect&&!stops);
    tab5_wifi_initialized=false;assert(!wifi_probe_begin());tab5_wifi_initialized=true;
    assert(wifi_probe_begin()&&!wifi_probe_begin()&&!wifi_probe_ready()&&wifi_probe_requires_usb());
    assert(wifi_probe_service(true,&reconnect)&&wifi_probe_ready()&&!wifi_running&&stops==1);
    now_ms+=20000;assert(wifi_probe_service(true,&reconnect)&&stops==1);
    wifi_probe_end();assert(!wifi_probe_ready());assert(!wifi_probe_service(true,&reconnect)&&reconnect&&wifi_running&&!wifi_probe_pending());
    assert(atomic_load(&restored)&&atomic_load(&held_ms)==20000&&starts==1);
    reset();assert(wifi_probe_begin());wifi_probe_end();assert(!wifi_probe_service(true,&reconnect)&&!stops&&!starts&&!wifi_probe_pending());
    reset();stop_error=259;assert(wifi_probe_begin());assert(!wifi_probe_service(true,&reconnect)&&wifi_running&&!wifi_probe_pending()&&atomic_load(&stop_rc)==259);
    reset();cancel_in_stop=true;assert(wifi_probe_begin());assert(!wifi_probe_service(true,&reconnect)&&reconnect&&stops==1&&starts==1&&!wifi_probe_ready());
    reset();assert(wifi_probe_begin());assert(wifi_probe_service(true,&reconnect));
    assert(!wifi_probe_service(false,&reconnect)&&reconnect&&wifi_running&&!wifi_probe_pending());
    reset();assert(wifi_probe_begin());assert(wifi_probe_service(true,&reconnect));now_ms+=35000;
    assert(!wifi_probe_service(true,&reconnect)&&reconnect&&atomic_load(&expired)&&wifi_running);
    reset();assert(wifi_probe_begin());assert(wifi_probe_service(true,&reconnect));wifi_probe_end();start_errors=2;
    assert(wifi_probe_service(true,&reconnect)&&!reconnect&&!wifi_running&&!wifi_probe_ready()&&!wifi_probe_begin());
    assert(wifi_probe_service(true,&reconnect)&&starts==1);now_ms+=1000;
    assert(wifi_probe_service(true,&reconnect)&&starts==2);now_ms+=1000;
    assert(!wifi_probe_service(true,&reconnect)&&reconnect&&wifi_running&&starts==3&&!wifi_probe_pending());
    char diag[96];wifi_probe_diagnostics(diag,sizeof(diag));assert(!strncmp(diag,"1,0,0,0,",8));
    reset();assert(wifi_probe_begin_firmware());assert(wifi_probe_service(true,&reconnect));now_ms+=35000;
    assert(wifi_probe_service(true,&reconnect)&&wifi_probe_ready());now_ms+=90000;
    assert(!wifi_probe_service(true,&reconnect)&&reconnect&&atomic_load(&expired)&&wifi_running);
    reset();assert(wifi_probe_begin_gallery()&&!wifi_probe_requires_usb());
    assert(!wifi_probe_begin()&&!wifi_probe_begin_firmware()&&!wifi_probe_begin_gallery());
    assert(wifi_probe_service(true,&reconnect)&&wifi_probe_ready());
    // Leaving explicit BLE-only mode restores Wi-Fi, even without gallery end.
    assert(!wifi_probe_service(false,&reconnect)&&reconnect&&wifi_running);
    assert(!wifi_probe_begin());wifi_probe_end();
    assert(wifi_probe_begin_gallery());assert(wifi_probe_service(true,&reconnect));now_ms+=35000;
    assert(!wifi_probe_service(true,&reconnect)&&reconnect&&atomic_load(&expired)&&wifi_running);
    assert(!wifi_probe_begin_firmware());wifi_probe_end();
    assert(wifi_probe_begin_firmware()&&wifi_probe_requires_usb());wifi_probe_end();
    assert(!wifi_probe_service(true,&reconnect));
    reset();assert(wifi_probe_begin_gallery());assert(wifi_probe_service(true,&reconnect));
    now_ms+=1000;wifi_probe_release_gallery();
    assert(atomic_load(&probe_stage)==PROBE_QUIET&&!atomic_load(&owner_active)&&!wifi_running);
    assert(wifi_probe_service(true,&reconnect)&&!starts&&!wifi_probe_begin());
    now_ms+=100;assert(wifi_probe_begin_gallery()&&wifi_probe_ready()&&stops==1);
    now_ms+=1000;wifi_probe_release_gallery();now_ms+=599;assert(wifi_probe_service(true,&reconnect)&&!starts);
    now_ms++;assert(!wifi_probe_service(true,&reconnect)&&reconnect&&wifi_running&&starts==1);
    reset();assert(wifi_probe_begin_gallery());assert(wifi_probe_service(true,&reconnect));
    now_ms+=34900;wifi_probe_release_gallery();assert(wifi_probe_begin_gallery());
    now_ms+=100;assert(!wifi_probe_service(true,&reconnect)&&reconnect&&atomic_load(&expired)&&wifi_running);
    assert(!wifi_probe_begin());wifi_probe_end(); // Reuse never extends the hard deadline.
    reset();assert(wifi_probe_begin_gallery());assert(wifi_probe_service(true,&reconnect));
    wifi_probe_release_gallery();assert(!wifi_probe_service(false,&reconnect)&&reconnect&&wifi_running);
    reset();atomic_store(&probe_stage,PROBE_ARMING);assert(wifi_probe_service(true,&reconnect)&&!stops&&!starts);
    // A long real transfer renews only at completed authenticated ranges.
    // Keep one radio stop across64 ranges (over two minutes), not just a
    // single packet/short probe. Restore once on completion/failure.
    reset();assert(wifi_probe_begin_rpc_bulk());assert(wifi_probe_service(true,&reconnect));
    for(int range=0;range<64;range++) {
        now_ms+=2000;assert(wifi_probe_service(true,&reconnect)&&wifi_probe_ready());
        wifi_probe_release_rpc_bulk(true);assert(!atomic_load(&owner_active));
        now_ms+=100;assert(wifi_probe_service(true,&reconnect)&&!starts);
        assert(!wifi_probe_begin_gallery()); // Cannot inherit another policy.
        assert(wifi_probe_begin_rpc_bulk()&&wifi_probe_ready()&&stops==1);
    }
    assert(now_ms>125000&&!atomic_load(&expired));
    wifi_probe_release_rpc_bulk(false);
    assert(!wifi_probe_service(true,&reconnect)&&reconnect&&wifi_running&&starts==1);
    reset();assert(wifi_probe_begin_rpc_bulk());assert(wifi_probe_service(true,&reconnect));
    now_ms+=10000;wifi_probe_release_rpc_bulk(true);now_ms+=600;
    assert(!wifi_probe_service(true,&reconnect)&&reconnect&&starts==1); // Idle600ms.
    reset();assert(wifi_probe_begin_rpc_bulk());assert(wifi_probe_service(true,&reconnect));
    now_ms+=34999;assert(!wifi_probe_begin_rpc_bulk()); // No renewal by polling.
    assert(wifi_probe_service(true,&reconnect)&&wifi_probe_ready());now_ms++;
    assert(!wifi_probe_service(true,&reconnect)&&reconnect&&atomic_load(&expired));
    wifi_probe_release_rpc_bulk(true);assert(wifi_running&&!wifi_probe_pending());
    reset();assert(wifi_probe_begin_rpc_bulk());assert(wifi_probe_service(true,&reconnect));
    now_ms+=35000;wifi_probe_release_rpc_bulk(true); // Late success cannot revive.
    assert(!wifi_probe_service(true,&reconnect)&&reconnect&&atomic_load(&expired));
    reset();assert(wifi_probe_begin_rpc_bulk());assert(wifi_probe_service(true,&reconnect));
    wifi_probe_release_rpc_bulk(true);
    assert(!wifi_probe_service(false,&reconnect)&&reconnect&&wifi_running);
    reset();assert(wifi_probe_begin_rpc_bulk());assert(wifi_probe_service(true,&reconnect));
    wifi_probe_release_rpc_bulk(false);start_errors=1;
    assert(wifi_probe_service(true,&reconnect)&&!wifi_running);now_ms+=1000;
    assert(!wifi_probe_service(true,&reconnect)&&wifi_running&&starts==2);
    reset();cancel_in_stop=true;assert(wifi_probe_begin_rpc_bulk());
    assert(!wifi_probe_service(true,&reconnect)&&reconnect&&wifi_running);
    puts("WIFI_PROBE_OK diagnostic_USB_guard_and_gallery_lease_stop_start_cancel_in_driver_expiry_mode_loss_failure_retry_no_config_mutation");return 0;
}
