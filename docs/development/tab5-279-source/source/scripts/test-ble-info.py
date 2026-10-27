"""Compile the production GATT identity callback and bound its wire reply.

Use --before to retain replies from the frozen oversized callback. Compile the
generated C with a host C11 compiler, run it, and compare paired JSON lines.
Authentication hashes/randomness are simulated; real credentials are never read.
"""
import argparse
import re
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--before', type=Path, help='Optional frozen oversized callback for historical comparison')
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
root = Path(__file__).resolve().parents[1]
source = (root / 'firmware/main/ble_link.c').read_text(encoding='utf-8')
before = (a.before / 'ble_link.c').read_text(encoding='utf-8') if a.before else source
device = (root / 'firmware/main/device.c').read_text(encoding='utf-8')

def function(text, name):
    match = re.search(r'(?:static )?(?:int|void) ' + name + r'\(', text)
    start = match.start()
    opening = text.index('{', start)
    depth, end = 1, opening + 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end]

header = r'''
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <limits.h>
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
typedef struct { char id[13]; uint8_t key[32]; bool paired; char host[16]; int port; char ssid[33]; char password[64]; int mode; int brightness; } device_config;
typedef struct {char version[32];} app_desc;
typedef struct {unsigned size;char data[1100];} test_mbuf;
struct ble_gatt_access_ctxt {int op;test_mbuf *om;};
enum {BLE_ATT_ERR_INSUFFICIENT_AUTHEN=5,BLE_ATT_ERR_UNLIKELY=14,BLE_ATT_ERR_INSUFFICIENT_RES=17,BLE_GATT_ACCESS_OP_READ_CHR=0};
#define portMAX_DELAY 0
#define MBEDTLS_MD_SHA256 0
static device_config config={.id="012345abcdef",.paired=true};
static app_desc app={.version="0.2.82-ui"};
static void *device_mutex;
static char challenge[33]="0123456789abcdef0123456789abcdef";
static uint64_t acknowledged;
static unsigned mailbox_submitted,mailbox_alloc_failed,mailbox_submit_failed,mailbox_min_free;
static unsigned rpc_notify_yields,rpc_notify_aborts;
static int mailbox_last_rc;
static int mutex_depth,append_fail;
static bool test_maximum;
static device_config device_get_config(void){return config;}
static int xSemaphoreTake(void *m,int t){(void)m;(void)t;mutex_depth++;return 1;}
static int xSemaphoreGive(void *m){(void)m;mutex_depth--;return 1;}
static void assets_ids(char *out){static const char ids[]="01234567,89abcdef,01234567";_Static_assert(sizeof(ids)==27,"asset boundary");memcpy(out,ids,sizeof(ids));}
static const app_desc *esp_app_get_description(void){return &app;}
static void ble_performance_json(char *out,size_t size){
    if(!test_maximum){snprintf(out,size,"\"link\":[7500,0,2,2,251,251,0,0,0,0]");return;}
    snprintf(out,size,"\"link\":[%u,%u,%u,%u,%u,%u,%u,%d,%d,%d]",UINT_MAX,UINT_MAX,UINT_MAX,UINT_MAX,UINT_MAX,UINT_MAX,UINT_MAX,INT_MIN,INT_MIN,INT_MIN);
}
static int os_mbuf_append(test_mbuf *out,const void *data,size_t size){
    if(append_fail)return -1;
    if(size>=sizeof(out->data))abort();
    memcpy(out->data,data,size);out->data[size]=0;out->size=(unsigned)size;return 0;
}
static const void *mbedtls_md_info_from_type(int t){(void)t;return NULL;}
static int mbedtls_md_hmac(const void *info,const uint8_t *key,size_t key_size,const uint8_t *message,size_t length,uint8_t *hash){
    (void)info;(void)key;(void)key_size;(void)message;
    for(unsigned i=0;i<32;i++)hash[i]=(uint8_t)(length+i*17);return 0;
}
static void esp_fill_random(uint8_t *bytes,size_t size){for(size_t i=0;i<size;i++)bytes[i]=(uint8_t)(i*19);}
'''
test = r'''
int main(void){
    char encoded[515],expected[513];uint8_t values[256];
    for(unsigned i=0;i<256;i++)values[i]=(uint8_t)i;
    memset(encoded,'!',sizeof(encoded));hex_encode(values,sizeof(values),encoded+1);
    for(unsigned i=0;i<256;i++)snprintf(expected+i*2,sizeof(expected)-i*2,"%02x",values[i]);
    assert(!strcmp(encoded+1,expected)&&encoded[0]=='!'&&encoded[514]=='!');
    char nonce[33],proof[65];device_nonce(nonce);assert(strlen(nonce)==32);
    device_proof("test identity proof",proof);assert(strlen(proof)==64);
    test_mbuf old={0},now={0};struct ble_gatt_access_ctxt ctx={0,&old};
    for(unsigned i=0;i<2;i++){
        test_maximum=i!=0;
        acknowledged=i?UINT64_MAX:0;
        mailbox_submitted=mailbox_alloc_failed=mailbox_submit_failed=mailbox_min_free=i?UINT_MAX:0;
        rpc_notify_yields=rpc_notify_aborts=i?UINT_MAX:0;mailbox_last_rc=i?INT_MIN:0;
        if(i){memset(app.version,'F',sizeof(app.version)-1);app.version[sizeof(app.version)-1]=0;assert(strlen(app.version)==31);}
        ctx.om=&old;assert(telemetry_info_access_before(0,0,&ctx,NULL)==0);
        ctx.om=&now;assert(telemetry_info_access(0,0,&ctx,NULL)==0);
        assert((HAS_OVERSIZED_BASELINE?old.size>512:old.size<=512)&&now.size<=512&&mutex_depth==0);
        assert(strstr(now.data,"\"rpcWriteWindow\":64")&&strstr(now.data,"\"mailboxWindow\":8")&&strstr(now.data,"\"rpcNotifyWindow\":32"));
        assert(!strcmp(config.id,"012345abcdef"));
        printf("{\"before\":%s,\"after\":%s}\n",old.data,now.data);
    }
    config.paired=false;now.size=0;assert(telemetry_info_access(0,0,&ctx,NULL)==BLE_ATT_ERR_INSUFFICIENT_AUTHEN&&now.size==0);
    config.paired=true;ctx.op=1;assert(telemetry_info_access(0,0,&ctx,NULL)==BLE_ATT_ERR_UNLIKELY&&now.size==0);
    ctx.op=0;append_fail=1;assert(telemetry_info_access(0,0,&ctx,NULL)==BLE_ATT_ERR_INSUFFICIENT_RES&&mutex_depth==0);
    fprintf(stderr,"BLE_INFO_OK maximum-width ATT value <=512 bytes, repeated reads, all-byte hex, nonce/proof lengths, auth/read/mbuf errors\n");
}
'''
functions = '\n'.join(function(device, name) for name in ['hex_encode', 'device_nonce', 'device_proof'])
functions += '\n' + function(before, 'telemetry_info_access').replace('telemetry_info_access(', 'telemetry_info_access_before(', 1)
functions += '\n' + function(source, 'telemetry_info_access')
a.output.mkdir(parents=True, exist_ok=True)
(a.output / 'ble_info_test.c').write_text(f'#define HAS_OVERSIZED_BASELINE {int(a.before is not None)}\n' + header + functions + test, encoding='utf-8')
print(a.output / 'ble_info_test.c')
