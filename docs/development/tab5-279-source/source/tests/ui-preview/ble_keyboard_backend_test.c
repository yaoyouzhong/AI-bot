#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>
#include <stdio.h>
#include "../../firmware/main/ble_keyboard.h"
#define portMAX_DELAY 0
#define BLE_HS_CONN_HANDLE_NONE 65535
#define BLE_ATT_ERR_INVALID_ATTR_VALUE_LEN 1
#define BLE_ATT_ERR_VALUE_NOT_ALLOWED 2
#define BLE_ATT_ERR_UNLIKELY 3
#define BLE_ATT_ERR_INSUFFICIENT_RES 4
#define BLE_GATT_CHR_F_READ 1
#define BLE_GATT_CHR_F_READ_ENC 2
#define BLE_GATT_CHR_F_READ_AUTHEN 4
#define BLE_GATT_CHR_F_WRITE 8
#define BLE_GATT_CHR_F_WRITE_NO_RSP 16
#define BLE_GATT_CHR_F_WRITE_ENC 32
#define BLE_GATT_CHR_F_WRITE_AUTHEN 64
#define BLE_GATT_CHR_F_NOTIFY 128
#define BLE_ATT_F_READ 1
#define BLE_GATT_SVC_TYPE_PRIMARY 1
#define BLE_GATT_ACCESS_OP_READ_DSC 1
#define BLE_GATT_ACCESS_OP_READ_CHR 2
#define BLE_GATT_ACCESS_OP_WRITE_CHR 3
#define BLE_GAP_EVENT_CONNECT 1
#define BLE_GAP_EVENT_DISCONNECT 2
#define BLE_GAP_EVENT_ENC_CHANGE 3
#define BLE_GAP_EVENT_SUBSCRIBE 4
#define BLE_GAP_EVENT_NOTIFY_TX 5
#define BLE_GAP_EVENT_PASSKEY_ACTION 6
#define BLE_GAP_EVENT_REPEAT_PAIRING 7
#define BLE_GAP_REPEAT_PAIRING_IGNORE 2
#define BLE_SM_IOACT_NUMCMP 4
#define BLE_SM_IO_CAP_DISP_YES_NO 1
#define BLE_SM_PAIR_KEY_DIST_ENC 1
#define BLE_SM_PAIR_KEY_DIST_ID 2
#define BLE_ERR_AUTH_FAIL 5
#define BLE_ERR_REM_USER_CONN_TERM 19
#define BLE_UUID16_DECLARE(n) (&(const uint16_t){n})
typedef struct {int held;} *SemaphoreHandle_t;
static SemaphoreHandle_t xSemaphoreCreateMutex(void){return calloc(1,sizeof(struct {int held;}));}
static void xSemaphoreTake(SemaphoreHandle_t m,int wait){(void)wait;assert(!m->held);m->held=1;}
static void xSemaphoreGive(SemaphoreHandle_t m){assert(m->held);m->held=0;}
struct ble_npl_event{int unused;};struct ble_npl_callout{int unused;};
static int ble_npl_callout_init(struct ble_npl_callout *c,void *q,void(*f)(struct ble_npl_event*),void *a){(void)c;(void)q;(void)f;(void)a;return 0;}
static void *nimble_port_get_dflt_eventq(void){return NULL;}
static int ble_npl_time_ms_to_ticks32(int ms){return ms;}
static void ble_npl_callout_reset(struct ble_npl_callout *c,int ms){(void)c;(void)ms;}
static int64_t clock_ms=1000,lock_updated=1000;static bool lock_fresh=true,lock_value=true;
static bool cover_value,cover_complete=true;static int64_t cover_at;static const char *cover_id="11111111-1111-4111-8111-111111111111";
bool computer_remote_cover_state(const char *id,bool *cover){*cover=cover_value&&clock_ms>=cover_at&&clock_ms-cover_at<1500;return cover_complete&&!strcmp(id,cover_id);}
static int display_value=1;static bool display_complete=true;
bool computer_remote_display_state(const char *id,int *display){*display=clock_ms>=cover_at&&clock_ms-cover_at<1500?display_value:-1;return display_complete&&!strcmp(id,cover_id);}
static int64_t esp_timer_get_time(void){return clock_ms*1000;}
struct os_mbuf {uint8_t data[80];size_t n;};
#define OS_MBUF_PKTLEN(m) ((m)->n)
static int os_mbuf_copydata(struct os_mbuf *m,int off,int n,void *p){memcpy(p,m->data+off,n);return 0;}
static int os_mbuf_append(struct os_mbuf *m,const void *p,size_t n){assert(n<=80);memcpy(m->data,p,n);m->n=n;return 0;}
static struct os_mbuf *ble_hs_mbuf_from_flat(const void *p,size_t n){struct os_mbuf *m=calloc(1,sizeof(*m));os_mbuf_append(m,p,n);return m;}
struct ble_gatt_access_ctxt{int op;struct os_mbuf *om;};
typedef int(*access_fn)(uint16_t,uint16_t,struct ble_gatt_access_ctxt*,void*);
struct ble_gatt_dsc_def{const void *uuid;int att_flags;access_fn access_cb;void *arg;};
struct ble_gatt_chr_def{const void *uuid;access_fn access_cb;void *arg;int flags;uint16_t *val_handle;struct ble_gatt_dsc_def *descriptors;};
struct ble_gatt_svc_def{int type;const void *uuid;struct ble_gatt_chr_def *characteristics;};
static int ble_gatts_count_cfg(const struct ble_gatt_svc_def *s){(void)s;return 0;}
static int ble_gatts_add_svcs(const struct ble_gatt_svc_def *s){(void)s;return 0;}
struct ble_sm_io{int action;bool numcmp_accept;};
static int pair_accepted=-1,terminated;
static int ble_sm_inject_io(uint16_t c,struct ble_sm_io *io){(void)c;pair_accepted=io->numcmp_accept;return 0;}
static int ble_gap_terminate(uint16_t c,int reason){(void)c;(void)reason;terminated++;return 0;}
struct ble_gap_conn_desc{struct {bool encrypted,authenticated,bonded;} sec_state;int peer_id_addr;};
static unsigned bond_deleted;static int ble_store_util_delete_peer(const int *addr){(void)addr;bond_deleted++;return 0;}
static struct ble_gap_conn_desc security;
static int ble_gap_conn_find(uint16_t c,struct ble_gap_conn_desc *d){(void)c;*d=security;return 0;}
struct ble_gap_event{int type;struct {int status;uint16_t conn_handle;} connect,enc_change;
 struct {uint16_t attr_handle;bool cur_notify;} subscribe;
 struct {uint16_t attr_handle;int status;} notify_tx;
 struct {uint16_t conn_handle;struct {int action;uint32_t numcmp;} params;} passkey;
};
static struct {int sm_io_cap,sm_bonding,sm_mitm,sm_sc,sm_our_key_dist,sm_their_key_dist;} ble_hs_cfg;
static unsigned reports;static uint8_t usages[160],modifiers[160];static bool fail_send,cancel_during_send;
static int ble_gatts_notify_custom(uint16_t c,uint16_t a,struct os_mbuf *m){(void)c;(void)a;assert(reports<160);modifiers[reports]=m->data[0];usages[reports++]=m->data[2];free(m);if(cancel_during_send)ble_keyboard_cancel();return fail_send?1:0;}
static unsigned test_host_epoch;
static unsigned device_host_epoch(void){return test_host_epoch;}
static bool device_host_changing(void){return false;}
#include "../../firmware/main/ble_keyboard.inc"
void ble_store_config_init(void){}
bool computer_remote_lock_state(bool *locked,int64_t *updated){*locked=lock_value;*updated=lock_updated;return lock_fresh;}
static void event(int type){struct ble_gap_event e={.type=type};e.connect.conn_handle=1;keyboard_gap_event(&e);}
static void tick(void){clock_ms+=40;lock_updated=clock_ms;keyboard_tick(NULL);}
static void ready(void){event(BLE_GAP_EVENT_CONNECT);security=(struct ble_gap_conn_desc){.sec_state={true,true,true}};event(BLE_GAP_EVENT_ENC_CHANGE);keyboard_authenticated(test_host_epoch);keyboard_report=11;keyboard_boot=12;struct ble_gap_event e={.type=BLE_GAP_EVENT_SUBSCRIBE,.subscribe={11,true}};keyboard_gap_event(&e);lock_value=lock_fresh=true;lock_updated=cover_at=clock_ms;display_value=1;display_complete=true;}
int main(void){
 assert(keyboard_init());assert(ble_hs_cfg.sm_mitm&&ble_hs_cfg.sm_bonding&&ble_hs_cfg.sm_sc);
 assert((keyboard_services[0].characteristics[4].flags&BLE_GATT_CHR_F_READ_AUTHEN)!=0);
 ready();ble_keyboard_state s;ble_keyboard_get(&s);assert(s.ready);
 test_host_epoch=1;keyboard_authenticated(0);ble_keyboard_get(&s);assert(!s.ready);keyboard_authenticated(1);ble_keyboard_get(&s);assert(s.ready);test_host_epoch=0;ready();ble_keyboard_get(&s);
 for(unsigned caps=0;caps<2;caps++) {
  ready();ble_keyboard_get(&s);reports=0;cover_value=false;cover_at=clock_ms;
  struct os_mbuf led={.n=1};led.data[0]=caps?2:0;
  struct ble_gatt_access_ctxt output={.op=BLE_GATT_ACCESS_OP_WRITE_CHR,.om=&led};
  assert(keyboard_access(1,0,&output,(void*)6)==0);
  assert(ble_keyboard_start("aA! ",4,s.connection,cover_id));for(unsigned i=0;i<10;i++)tick();
  assert(reports==10&&usages[0]==4&&modifiers[0]==(caps?2:0)&&usages[2]==4&&modifiers[2]==(caps?0:2));
  assert(usages[4]==0x1e&&modifiers[4]==2&&usages[6]==0x2c&&modifiers[6]==0&&usages[8]==0x28);
  for(unsigned i=1;i<10;i+=2)assert(usages[i]==0&&modifiers[i]==0);
 }
 ready();ble_keyboard_get(&s);reports=0;
 // Only a fresh positive, authenticated cover request can schedule one Space.
 reports=0;cover_value=false;cover_at=clock_ms;assert(!ble_keyboard_wake(cover_id,s.connection));assert(reports==0);
 cover_value=true;cover_complete=false;assert(!ble_keyboard_wake(cover_id,s.connection));cover_complete=true;
 lock_value=false;assert(!ble_keyboard_wake(cover_id,s.connection));lock_value=true;
 lock_fresh=false;assert(!ble_keyboard_wake(cover_id,s.connection));lock_fresh=true;
 keyboard_bridge_verified=false;assert(!ble_keyboard_wake(cover_id,s.connection));keyboard_authenticated(test_host_epoch);
 keyboard_secure=false;assert(!ble_keyboard_wake(cover_id,s.connection));keyboard_secure=true;
 cover_at=clock_ms-1500;assert(!ble_keyboard_wake(cover_id,s.connection));cover_at=clock_ms;
 assert(!ble_keyboard_wake(cover_id,s.connection+1));
 assert(ble_keyboard_wake(cover_id,s.connection));assert(!ble_keyboard_wake(cover_id,s.connection));
 tick();cover_value=false;tick();assert(reports==2&&usages[0]==0x2c&&usages[1]==0); // Release survives cover transition.
 cover_value=true;cover_at=clock_ms;assert(!ble_keyboard_wake(cover_id,s.connection));
 cover_id="22222222-2222-4222-8222-222222222222";assert(ble_keyboard_wake(cover_id,s.connection));
 cover_value=false;unsigned before=reports;tick();assert(reports==before); // Evidence lost before key-down: no Space.
 cover_value=true;cover_at=clock_ms;cover_id="33333333-3333-4333-8333-333333333333";
 assert(ble_keyboard_wake(cover_id,s.connection));ble_keyboard_cancel();tick();assert(reports==before);
 cover_id="44444444-4444-4444-8444-444444444444";assert(ble_keyboard_wake(cover_id,s.connection));
 event(BLE_GAP_EVENT_DISCONNECT);tick();assert(reports==before);ready();ble_keyboard_get(&s);
 for(int i=0;i<5;i++)tick();assert(reports==before); // Reconnect never replays a wake.
 cover_id="55555555-5555-4555-8555-555555555555";cover_at=clock_ms;assert(ble_keyboard_wake(cover_id,s.connection));
 clock_ms+=501;tick();assert(reports==before); // Dispatch deadline expires without input.
 cover_id="66666666-6666-4666-8666-666666666666";cover_at=clock_ms;assert(ble_keyboard_wake(cover_id,s.connection));
 fail_send=true;tick();fail_send=false;assert(reports==before+1&&terminated==1);tick();assert(reports==before+1);terminated=0;
 // Screen-off preparation has a modifier-only report and mandatory release.
 ready();ble_keyboard_get(&s);reports=0;cover_value=false;display_value=1;
 cover_id="77777777-7777-4777-8777-777777777777";cover_at=clock_ms;
 assert(!ble_keyboard_display_wake(cover_id,s.connection));
 display_value=-1;assert(!ble_keyboard_display_wake(cover_id,s.connection));
 display_value=0;display_complete=false;assert(!ble_keyboard_display_wake(cover_id,s.connection));display_complete=true;
 assert(ble_keyboard_display_wake(cover_id,s.connection));assert(!ble_keyboard_display_wake(cover_id,s.connection));
 tick();display_value=1;tick();assert(reports==2&&modifiers[0]==2&&usages[0]==0&&modifiers[1]==0&&usages[1]==0);
 cover_id="88888888-8888-4888-8888-888888888888";display_value=0;cover_at=clock_ms;
 assert(ble_keyboard_display_wake(cover_id,s.connection));cancel_during_send=true;tick();cancel_during_send=false;tick();
 assert(reports==4&&modifiers[2]==2&&modifiers[3]==0&&usages[2]==0&&usages[3]==0);
 ready();ble_keyboard_get(&s);reports=0;cover_at=clock_ms;
 cover_id="99999999-9999-4999-8999-999999999999";display_value=0;
 assert(ble_keyboard_display_wake(cover_id,s.connection));display_value=1;tick();assert(!reports);
 cover_id="aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";display_value=0;cover_at=clock_ms;
 assert(ble_keyboard_display_wake(cover_id,s.connection));clock_ms+=501;tick();assert(!reports);
 ready();ble_keyboard_get(&s);cover_id="bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";display_value=0;
 assert(ble_keyboard_display_wake(cover_id,s.connection));event(BLE_GAP_EVENT_DISCONNECT);tick();ready();ble_keyboard_get(&s);tick();assert(!reports);
 cover_id="cccccccc-cccc-4ccc-8ccc-cccccccccccc";display_value=0;cover_at=clock_ms;
 assert(ble_keyboard_display_wake(cover_id,s.connection));fail_send=true;tick();fail_send=false;assert(reports==1&&terminated==1);tick();assert(reports==1);terminated=0;
 ready();ble_keyboard_get(&s);reports=0;cover_at=clock_ms;
 display_value=0;assert(!ble_keyboard_start("4826",4,s.connection,cover_id));
 display_value=-1;assert(!ble_keyboard_start("4826",4,s.connection,cover_id));
 display_value=1;assert(ble_keyboard_start("4826",4,s.connection,cover_id));display_value=0;tick();assert(!reports);
 ready();ble_keyboard_get(&s);reports=0;
 lock_value=false;assert(!ble_keyboard_start("4826",4,s.connection,cover_id));lock_value=true;
 lock_fresh=false;assert(!ble_keyboard_start("4826",4,s.connection,cover_id));lock_fresh=true;
 keyboard_bridge_verified=false;assert(!ble_keyboard_start("4826",4,s.connection,cover_id));keyboard_authenticated(test_host_epoch);
 security.sec_state.authenticated=false;event(BLE_GAP_EVENT_ENC_CHANGE);assert(!ble_keyboard_start("4826",4,s.connection,cover_id));security.sec_state.authenticated=true;event(BLE_GAP_EVENT_ENC_CHANGE);
 assert(!ble_keyboard_start("4826",4,s.connection+1,cover_id));assert(ble_keyboard_start("4826",4,s.connection,cover_id));assert(!ble_keyboard_start("4826",4,s.connection,cover_id));
 for(int i=0;i<10;i++)tick();ble_keyboard_get(&s);assert(s.finished&&!s.failed&&!s.busy&&reports==10);assert(usages[8]==0x28&&usages[9]==0);
 ready();ble_keyboard_get(&s);reports=0;assert(ble_keyboard_start("4826",4,s.connection,cover_id));tick();lock_value=false;tick();assert(reports==2&&usages[1]==0);ble_keyboard_get(&s);assert(s.failed&&!s.busy);
 ready();ble_keyboard_get(&s);reports=0;assert(ble_keyboard_start("4826",4,s.connection,cover_id));for(int i=0;i<8;i++)tick();
 lock_updated=keyboard_last_digit_at;clock_ms+=40;keyboard_tick(NULL);assert(reports==8); // Wait for newer lock state before Enter.
 lock_value=false;tick();assert(reports==8); // Auto-submit unlocked Windows: no stray Enter.
 ready();ble_keyboard_get(&s);reports=0;assert(ble_keyboard_start("4826",4,s.connection,cover_id));cancel_during_send=true;tick();cancel_during_send=false;tick();assert(reports==2&&usages[1]==0);ble_keyboard_get(&s);assert(!s.busy&&s.failed);
 ready();ble_keyboard_get(&s);reports=0;assert(ble_keyboard_start("4826",4,s.connection,cover_id));fail_send=true;tick();fail_send=false;assert(terminated==1);tick();assert(reports==1);ble_keyboard_get(&s);assert(s.failed&&!s.busy);
 ready();ble_keyboard_get(&s);assert(ble_keyboard_start("4826",4,s.connection,cover_id));event(BLE_GAP_EVENT_DISCONNECT);ble_keyboard_get(&s);assert(!s.ready&&!s.busy);for(size_t i=0;i<sizeof(keyboard_flow.keys);i++)assert(keyboard_flow.keys[i]==0);
 ready();ble_keyboard_get(&s);reports=0;char longest[64];memset(longest,'0',sizeof(longest));assert(ble_keyboard_start(longest,sizeof(longest),s.connection,cover_id));
 for(int i=0;i<130;i++)tick();ble_keyboard_get(&s);assert(s.finished&&!s.failed&&reports==130);ble_keyboard_wipe(longest,sizeof(longest));
 ready();struct ble_gap_event p={.type=BLE_GAP_EVENT_PASSKEY_ACTION,.passkey={1,{BLE_SM_IOACT_NUMCMP,482617}}};keyboard_gap_event(&p);ble_keyboard_get(&s);assert(s.pairing&&!s.ready&&pair_accepted==-1);ble_keyboard_pair_confirm(true);ble_keyboard_get(&s);assert(!s.pairing);tick();assert(pair_accepted==1);
 keyboard_gap_event(&p);clock_ms+=30001;tick();assert(pair_accepted==0);p.type=BLE_GAP_EVENT_REPEAT_PAIRING;assert(keyboard_gap_event(&p)==BLE_GAP_REPEAT_PAIRING_IGNORE);assert(bond_deleted==0);ble_keyboard_pair_confirm(false);tick();assert(bond_deleted==0);keyboard_gap_event(&p);ble_keyboard_pair_confirm(true);tick();assert(bond_deleted==1);
 ready();ble_keyboard_get(&s);clock_ms+=8001;ble_keyboard_get(&s);assert(!s.ready&&!s.bridge_verified);assert(!ble_keyboard_start("4826",4,s.connection,cover_id));
 puts("BLE_KEYBOARD_BACKEND_OK: encrypted bonded peer, authenticated bridge, fresh lock, connection generation, cancel race, send failure, pairing consent and timeout; no hardware claimed");free(keyboard_mutex);return 0;
}

