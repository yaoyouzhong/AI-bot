// Run the pinned production ATT transmitter, including mbuf ownership.
#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#define CONFIG_IDF_TARGET_ESP32P4 1
#define CONFIG_ESP_HOSTED_ENABLED 1
struct os_mbuf;
struct os_mbuf_pkthdr {struct os_mbuf_pkthdr *next; struct os_mbuf *owner;};
struct os_mbuf {unsigned om_len; uint8_t data[8]; uint8_t *om_data; struct os_mbuf_pkthdr hdr;};
struct queue {struct os_mbuf_pkthdr *first, *last;};
struct ble_hs_conn {bool client_att_busy; struct queue att_tx_q;};
struct ble_l2cap_chan {int mtu;};
#define STAILQ_FIRST(q) ((q)->first)
#define STAILQ_REMOVE_HEAD(q, field) do { (q)->first=(q)->first->next; if(!(q)->first)(q)->last=NULL; } while(0)
#define STAILQ_INSERT_TAIL(q, h, field) do { (h)->next=NULL; if((q)->last)(q)->last->next=(h); else(q)->first=(h); (q)->last=(h); } while(0)
#define OS_MBUF_PKTHDR_TO_MBUF(h) ((h)->owner)
#define OS_MBUF_PKTHDR(m) (&(m)->hdr)
#define BLE_HS_DBG_ASSERT_EVAL(x) assert(x)
#define BLE_EATT_LOG_DEBUG(...) ((void)0)
static int logged, sent, freed, tx_rc, stats;
#define BLE_HS_LOG(level, ...) (++logged)
static bool ble_att_is_request_op(int op){return op==0x0a;}
static void ble_att_inc_tx_stat(int op){(void)op;stats++;}
static void ble_att_truncate_to_mtu(struct ble_l2cap_chan *c,struct os_mbuf *m){assert(c->mtu==517&&m->om_len==1);}
static int ble_l2cap_tx(struct ble_hs_conn *c,struct ble_l2cap_chan *ch,struct os_mbuf *m){
    (void)c;(void)ch;sent++;free(m);freed++;return tx_rc;
}
#include "ble_att_tx_under_test.inc"
static struct os_mbuf *packet(int op){
    struct os_mbuf *m=calloc(1,sizeof(*m));assert(m);m->om_len=1;m->om_data=m->data;m->data[0]=(uint8_t)op;m->hdr.owner=m;return m;
}
int main(void){
#ifdef _WIN32
    _set_error_mode(_OUT_TO_STDERR);
    _set_abort_behavior(0, _CALL_REPORTFAULT);
#endif
    struct ble_hs_conn conn={0};struct ble_l2cap_chan chan={517};
    assert(ble_att_tx_with_conn(&conn,&chan,packet(0x0a))==0&&conn.client_att_busy&&sent==1);
    // Busy requests queue without transferring ownership to L2CAP yet.
    assert(ble_att_tx_with_conn(&conn,&chan,packet(0x0a))==0&&sent==1&&conn.att_tx_q.first);
    assert(ble_att_tx_with_conn(&conn,&chan,NULL)==0&&sent==1);
    conn.client_att_busy=false;tx_rc=6;
    assert(ble_att_tx_with_conn(&conn,&chan,NULL)==6&&!conn.client_att_busy&&!conn.att_tx_q.first);
    assert(sent==2&&freed==2&&logged==1);
    tx_rc=0;assert(ble_att_tx_with_conn(&conn,&chan,packet(0x0a))==0&&conn.client_att_busy&&sent==3);
    // Notification/response failure must not clear another outstanding request.
    tx_rc=-1;assert(ble_att_tx_with_conn(&conn,&chan,packet(0x1b))==-1&&conn.client_att_busy);
    tx_rc=3;assert(ble_att_tx_with_conn(&conn,&chan,packet(0x0b))==3&&conn.client_att_busy);
    assert(sent==5&&freed==5&&stats==5&&logged==3);
    conn.client_att_busy=false;
    assert(ble_att_tx_with_conn(&conn,&chan,NULL)==0&&sent==5);
    puts("BLE_ATT_TX_FAILURE_OK exact_rc queued_request_recovery response_notify_preserve_busy consumed_once");
}
