#undef NDEBUG
#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <stdbool.h>
#include <windows.h>
#include <wincrypt.h>
#include "cJSON.h"
#include "link_state.h"
#include "trusted_hosts.h"
#define TAB5_MAX_FRAME 65536
#define MALLOC_CAP_SPIRAM 0
#define MALLOC_CAP_8BIT 0
#define portMAX_DELAY 0
#define xSemaphoreTake(a,b) ((void)0)
#define xSemaphoreGive(a) ((void)0)
#define TINFL_FLAG_PARSE_ZLIB_HEADER 1
#define TINFL_FLAG_USING_NON_WRAPPING_OUTPUT_BUF 4
#define TINFL_STATUS_DONE 0
typedef int tinfl_status;
typedef struct {unsigned state;uint8_t workspace[11000];} tinfl_decompressor;
#define tinfl_init(p) ((p)->state=0)
static void *workspace;
static bool fail_workspace;
static void *test_heap_malloc(size_t size,unsigned caps) {
    (void)caps;
    if(size==sizeof(tinfl_decompressor)) {
        if(fail_workspace)return NULL;
        assert(!workspace);workspace=malloc(size);return workspace;
    }
    return malloc(size);
}
static void test_free(void *p){if(p==workspace)workspace=NULL;free(p);}
#define heap_caps_malloc test_heap_malloc
#define free test_free
static struct {bool paired;char id[13];} config={true,"001122334455"};
static link_state device_links;
static cJSON *device_snapshots[3],*device_metrics[3];
static uint64_t device_metrics_sequence[3];
static unsigned offers;
static trusted_host_store trusted_hosts={.version=1,.count=1};
static bool trusted_hosts_readable=true,usb_owner_verified=true;
static unsigned device_host_epoch(void){return 1;}
static bool device_host_usb_owned(void){return usb_owner_verified;}
static void device_host_usb_touch(void){}
static int64_t clock_ms=1000;
static int64_t esp_timer_get_time(void){return clock_ms*1000;}
static bool assets_receive(cJSON *value){(void)value;return true;}
static void ota_update_consider(const char *json){(void)json;offers++;}
static const char *str(cJSON *o,const char *key){cJSON *v=cJSON_GetObjectItemCaseSensitive(o,key);return cJSON_IsString(v)?v->valuestring:NULL;}
static int mbedtls_base64_decode(unsigned char *out,size_t capacity,size_t *used,const unsigned char *input,size_t length) {
    DWORD size=(DWORD)capacity;
    if(!CryptStringToBinaryA((const char*)input,(DWORD)length,CRYPT_STRING_BASE64|CRYPT_STRING_STRICT,out,&size,NULL,NULL))return -1;
    *used=size;return 0;
}
// Host zlib validates the wire format; the firmware links ESP32-P4 ROM miniz.
static tinfl_status tinfl_decompress(tinfl_decompressor *state,const uint8_t *input,size_t *length,uint8_t *start,uint8_t *out,size_t *capacity,unsigned flags) {
    assert(state==workspace&&start==out&&flags==(TINFL_FLAG_PARSE_ZLIB_HEADER|TINFL_FLAG_USING_NON_WRAPPING_OUTPUT_BUF));
    typedef int (__cdecl *uncompress_fn)(void*,unsigned long*,const void*,unsigned long*);
    static uncompress_fn inflate;
    if(!inflate){HMODULE dll=LoadLibraryA(getenv("TAB5_TEST_ZLIB")?getenv("TAB5_TEST_ZLIB"):"C:\\Program Files\\Git\\ucrt64\\bin\\zlib1.dll");assert(dll);inflate=(uncompress_fn)GetProcAddress(dll,"uncompress2");assert(inflate);}
    unsigned long size=(unsigned long)*capacity,used=(unsigned long)*length;
    int status=inflate(out,&size,input,&used);*capacity=size;*length=used;
    return status==0?TINFL_STATUS_DONE:-1;
}
#include "telemetry_receive_under_test.inc"
static char *read_file(const char *directory,const char *name) {
    char path[1024];snprintf(path,sizeof(path),"%s/%s",directory,name);FILE *f=fopen(path,"rb");assert(f);
    fseek(f,0,SEEK_END);long size=ftell(f);rewind(f);char *data=calloc(size+1,1);assert(data);assert(fread(data,1,size,f)==(size_t)size);fclose(f);return data;
}
int main(int argc,char **argv) {
    assert(argc==2||argc==3);link_state_init(&device_links);
    char *full=read_file(argv[1],"full.json"),*packed=read_file(argv[1],"packed.json"),*one=read_file(argv[1],"metrics1.json"),*two=read_file(argv[1],"metrics2.json");
    device_reject_reason reason;
    cJSON *owner_frame=cJSON_Parse(full);const char *owner_id=str(cJSON_GetObjectItemCaseSensitive(cJSON_GetObjectItemCaseSensitive(owner_frame,"data"),"connectionHealth"),"hostId");
    if(owner_id){assert(strlen(owner_id)==32);strcpy(trusted_hosts.entries[0].id,owner_id);}cJSON_Delete(owner_frame);
    usb_owner_verified=false;assert(!device_receive_checked(full,LINK_USB,NULL,&reason));usb_owner_verified=true;
    assert(!device_receive_for_host(full,LINK_WIFI,NULL,0));
    owner_frame=cJSON_Parse(full);cJSON *owner_data=cJSON_GetObjectItemCaseSensitive(owner_frame,"data"),*owner_health=cJSON_GetObjectItemCaseSensitive(owner_data,"connectionHealth");
    if(!owner_health)owner_health=cJSON_AddObjectToObject(owner_data,"connectionHealth");
    cJSON_DeleteItemFromObjectCaseSensitive(owner_health,"hostId");cJSON_AddStringToObject(owner_health,"hostId","ffffffffffffffffffffffffffffffff");
    char *foreign=cJSON_PrintUnformatted(owner_frame);trusted_hosts.count=2;assert(!device_receive_checked(foreign,LINK_WIFI,NULL,&reason));trusted_hosts.count=1;free(foreign);cJSON_Delete(owner_frame);
    fail_workspace=true;assert(!device_receive_checked(packed,LINK_USB,NULL,&reason)&&reason==DEVICE_REJECT_PACKED&&!workspace);fail_workspace=false;
    assert(device_receive_checked(full,LINK_USB,NULL,&reason)&&reason==DEVICE_REJECT_NONE);
    // This recovery case established USB state. Isolate the following
    // first-connection test, which must still reject pre-session metrics.
    cJSON_Delete(device_snapshots[LINK_USB]);device_snapshots[LINK_USB]=NULL;link_state_init(&device_links);
    assert(!device_receive_checked("invalid",LINK_USB,NULL,&reason)&&reason==DEVICE_REJECT_JSON);
    assert(!device_receive_checked("{}",LINK_USB,NULL,&reason)&&reason==DEVICE_REJECT_ENVELOPE);
    char *delayed=read_file(argv[1],"metrics-delayed.json");
    for(int link=0;link<3;link++) {
        uint64_t ack=0;assert(!device_receive(one,link,&ack));
        assert(device_receive_checked(packed,link,&ack,&reason)&&reason==DEVICE_REJECT_NONE&&!workspace);assert(device_receive(full,link,&ack));
        int64_t received=device_links.received_ms[link];uint64_t full_sequence=device_links.sequence[link];unsigned before=offers;
        clock_ms+=250;assert(device_receive(one,link,&ack));clock_ms+=250;assert(device_receive(two,link,&ack));
        assert(cJSON_GetObjectItemCaseSensitive(device_metrics[link],"sampleSequence")->valueint==2);
        assert(device_links.received_ms[link]==received&&device_links.sequence[link]==full_sequence&&offers==before);
        char burst[512];snprintf(burst,sizeof(burst),"{\"version\":1,\"type\":\"tab5_resources\",\"deviceId\":\"001122334455\",\"session\":\"%s\",\"sequence\":999,\"resources\":[{}]}",device_links.session[link]);
        assert(device_receive(burst,link,&ack)&&ack==999);
        assert(device_links.received_ms[link]==received&&device_links.sequence[link]==full_sequence&&offers==before);
        cJSON *malformed=cJSON_Parse(burst);cJSON_ReplaceItemInObject(malformed,"session",cJSON_CreateString("wrong"));char *badburst=cJSON_PrintUnformatted(malformed);
        assert(!device_receive(badburst,link,&ack));free(badburst);cJSON_Delete(malformed);
        assert(!device_receive(one,link,&ack));assert(device_receive(two,link,&ack));
        assert(device_receive(delayed,link,&ack)&&!workspace);
        assert(cJSON_GetArraySize(cJSON_GetObjectItemCaseSensitive(device_metrics[link],"samples"))==64);
        assert(cJSON_GetObjectItemCaseSensitive(device_metrics[link],"sampleSequence")->valueint==64);
        assert(device_links.received_ms[link]==received&&device_links.sequence[link]==full_sequence&&offers==before);
        cJSON *wrong=cJSON_Parse(two);cJSON_ReplaceItemInObject(wrong,"session",cJSON_CreateString("different-session"));char *bad=cJSON_PrintUnformatted(wrong);
        assert(!device_receive(bad,link,&ack));free(bad);cJSON_Delete(wrong);
        config.paired=false;assert(!device_receive(two,link,&ack));config.paired=true;
    }
    assert(!device_receive("{\"type\":\"tab5_packed\",\"size\":65537,\"payload\":\"eA==\"}",LINK_USB,NULL));
    assert(!device_receive("{\"type\":\"tab5_packed\",\"size\":100,\"payload\":\"invalid!\"}",LINK_USB,NULL));
    assert(!device_receive(full,LINK_NONE,NULL));
    {
        cJSON *edit=cJSON_Parse(full);uint64_t ack=0;
        cJSON_ReplaceItemInObject(edit,"deviceId",cJSON_CreateString("wrong"));char *bad=cJSON_PrintUnformatted(edit);
        assert(!device_receive_checked(bad,LINK_USB,&ack,&reason)&&reason==DEVICE_REJECT_IDENTITY);free(bad);
        cJSON_ReplaceItemInObject(edit,"deviceId",cJSON_CreateString("001122334455"));
        cJSON_ReplaceItemInObject(edit,"sequence",cJSON_CreateNumber(0));bad=cJSON_PrintUnformatted(edit);
        assert(!device_receive_checked(bad,LINK_USB,&ack,&reason)&&reason==DEVICE_REJECT_ENVELOPE);free(bad);
        cJSON_ReplaceItemInObject(edit,"sequence",cJSON_CreateNumber(device_links.sequence[LINK_USB]+1));bad=cJSON_PrintUnformatted(edit);
        assert(device_receive_checked(bad,LINK_USB,&ack,&reason)&&reason==DEVICE_REJECT_NONE);free(bad);
        cJSON_ReplaceItemInObject(edit,"sequence",cJSON_CreateNumber(device_links.sequence[LINK_USB]-1));bad=cJSON_PrintUnformatted(edit);
        assert(!device_receive_checked(bad,LINK_USB,&ack,&reason)&&reason==DEVICE_REJECT_SEQUENCE);free(bad);cJSON_Delete(edit);
        puts("USB_STATUS_REJECT_REASON_OK parse, packed allocation, envelope, identity, sequence and successful same-state raw reception");
    }
    {
        char *base=read_file(argv[1],"delta-baseline.json"),*update=read_file(argv[1],"delta-update.json"),*expected=read_file(argv[1],"delta-expected.json");
        cJSON *target=cJSON_Parse(expected),*edit=cJSON_Parse(update);uint64_t ack=0;
        assert(!device_receive(update,LINK_BLE,&ack));
        assert(device_receive(base,LINK_BLE,&ack));clock_ms+=100;
        assert(device_receive(update,LINK_BLE,&ack)&&ack==1001);
        assert(cJSON_Compare(device_snapshots[LINK_BLE],cJSON_GetObjectItemCaseSensitive(target,"data"),true));
        int64_t fresh=device_links.received_ms[LINK_BLE];clock_ms+=100;
        assert(device_receive(update,LINK_BLE,&ack)&&device_links.received_ms[LINK_BLE]==fresh);
        cJSON_ReplaceItemInObject(edit,"sequence",cJSON_CreateNumber(1003));
        char *bad=cJSON_PrintUnformatted(edit);assert(!device_receive(bad,LINK_BLE,&ack));free(bad);
        cJSON_ReplaceItemInObject(edit,"baseSequence",cJSON_CreateNumber(1001));
        cJSON_ReplaceItemInObject(edit,"session",cJSON_CreateString("wrong"));
        bad=cJSON_PrintUnformatted(edit);assert(!device_receive(bad,LINK_BLE,&ack));free(bad);
        cJSON_ReplaceItemInObject(edit,"session",cJSON_CreateString("delta-test"));
        cJSON_ReplaceItemInObject(edit,"changes",cJSON_Parse("[{\"path\":[\"codex\"],\"remove\":true}]"));
        bad=cJSON_PrintUnformatted(edit);assert(!device_receive(bad,LINK_BLE,&ack));free(bad);
        assert(device_links.received_ms[LINK_BLE]==fresh&&cJSON_Compare(device_snapshots[LINK_BLE],cJSON_GetObjectItemCaseSensitive(target,"data"),true));
        link_state_disconnect(&device_links,LINK_BLE);assert(!device_receive(update,LINK_BLE,&ack));
        cJSON_Delete(edit);cJSON_Delete(target);free(base);free(update);free(expected);
        puts("TELEMETRY_DELTA_OK production receiver; exact null/remove/arrays; duplicate has no freshness; wrong baseline/session/disconnected rejected atomically");
    }
    if(argc>2) {
        FILE *file=fopen(argv[2],"rb");assert(file);fseek(file,0,SEEK_END);long n=ftell(file);rewind(file);
        char *wire=calloc(n+1,1);assert(wire&&fread(wire,1,n,file)==(size_t)n);fclose(file);
        cJSON *envelope=cJSON_Parse(wire);assert(envelope);free(wire);
        char *clear=device_unpack_json(envelope,24576);assert(clear&&!workspace);
        cJSON *page=cJSON_Parse(clear);assert(page&&strstr(str(page,"text"),"上一轮回复"));
        assert(cJSON_GetArraySize(cJSON_GetObjectItemCaseSensitive(page,"neighbors"))>0);
        free(clear);cJSON_Delete(page);
        fail_workspace=true;assert(!device_unpack_json(envelope,24576));fail_workspace=false;
        cJSON_ReplaceItemInObject(envelope,"size",cJSON_CreateNumber(24577));assert(!device_unpack_json(envelope,24576));
        cJSON_Delete(envelope);puts("PACKED_PAGE_OK bridge page roundtrip, prefetched neighbors, bounded output and heap allocation failure");
    }
    {
        char *wide=read_file(argv[1],"status64-full.json"),*compressed=read_file(argv[1],"status64-packed.json");
        char *oversized=read_file(argv[1],"status64-over.json"),*target=read_file(argv[1],"status48-full.json");
        assert(strlen(wide)==65508&&strlen(target)==49124&&strlen(oversized)==65509);
        for(int link=0;link<3;link++) {
            uint64_t ack=0;clock_ms+=10;
            assert(device_receive(target,link,&ack)&&ack==999);clock_ms+=10;
            assert(device_receive_checked(compressed,link,&ack,&reason)&&ack==1000&&reason==DEVICE_REJECT_NONE&&!workspace);
            cJSON *padding=cJSON_GetObjectItemCaseSensitive(device_snapshots[link],"padding");
            assert(cJSON_IsString(padding)&&strlen(padding->valuestring)>64000&&telemetry_tree_bounded(device_snapshots[link]));
            int64_t received=device_links.received_ms[link];clock_ms+=10;
            assert(device_receive(wide,link,&ack)&&device_links.received_ms[link]==received);
            assert(!device_receive_checked(oversized,link,&ack,&reason)&&reason==DEVICE_REJECT_ENVELOPE&&device_links.sequence[link]==1000&&device_links.received_ms[link]==received);
        }
        free(wide);free(compressed);free(oversized);free(target);
        puts("STATUS_64K_RECEIVE_OK actual bridge 48KiB target and exact 64KiB expanded/encrypted boundary across all three links; overflow leaves cache/freshness intact");
    }
    free(full);free(packed);free(one);free(two);
    for(int i=0;i<3;i++){cJSON_Delete(device_snapshots[i]);cJSON_Delete(device_metrics[i]);}
    puts("TELEMETRY_RECEIVE_OK USB/Wi-Fi/BLE compressed full state, metrics ordering/session isolation, no heartbeat or OTA mutation, bounds");
}
