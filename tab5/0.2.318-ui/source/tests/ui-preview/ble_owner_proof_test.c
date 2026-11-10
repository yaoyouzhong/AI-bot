#include <assert.h>
#include <stdint.h>
#include <stdbool.h>
#include <stdlib.h>
#include <string.h>
#include <stdio.h>
#include "cJSON.h"
#define TRUSTED_HOST_NAME 64
static struct {char id[13];} config={"e8f60ae2ec56"};
static struct {unsigned count;} trusted_hosts={1};
static bool trusted_hosts_readable=true,locked;
static int device_mutex;
#define portMAX_DELAY 1
static void xSemaphoreTake(int mutex,int delay){assert(mutex==device_mutex&&delay==portMAX_DELAY&&!locked);locked=true;}
static void xSemaphoreGive(int mutex){assert(mutex==device_mutex&&locked);locked=false;}
#include "pairing_identity_under_test.inc"
static void device_host_identity(char id[33],char name[64]){strcpy(id,"11111111111111111111111111111111");strcpy(name,"Selected computer");}
#include "../../firmware/main/ble_owner_proof.inc"
int main(void) {
    const char *valid="{\"version\":1,\"type\":\"tab5_ble_owner\",\"deviceId\":\"e8f60ae2ec56\",\"hostId\":\"11111111111111111111111111111111\",\"sequence\":42}";
    uint64_t sequence=0;assert(ble_owner_proof(valid,&sequence)&&sequence==42);
    char bounded[15];memset(bounded,'X',sizeof(bounded));
    assert(device_pairing_identity(bounded+1)&&!locked&&bounded[0]=='X'&&bounded[14]=='X'&&!strcmp(bounded+1,config.id));
    assert(device_pairing_identity(NULL)&&!locked);
    trusted_hosts_readable=false;sequence=99;
    assert(!device_pairing_identity(NULL)&&!ble_owner_proof(valid,&sequence)&&sequence==99&&!locked);
    trusted_hosts_readable=true;trusted_hosts.count=0;
    assert(!device_pairing_identity(NULL)&&!ble_owner_proof(valid,&sequence)&&sequence==99&&!locked);
    trusted_hosts.count=1;
    const char *keys[]={"type","deviceId","hostId"},*values[]={"tab5_status","other-device","22222222222222222222222222222222"};
    for(int i=0;i<3;i++){cJSON *root=cJSON_Parse(valid);cJSON_SetValuestring(cJSON_GetObjectItemCaseSensitive(root,keys[i]),values[i]);char *wire=cJSON_PrintUnformatted(root);assert(!ble_owner_proof(wire,&sequence));free(wire);cJSON_Delete(root);}
    const char *invalid[]={"0","-1","1.5","9007199254740992"};
    // Keep the exact wire integer: cJSON printing can round a large test double.
    for(unsigned i=0;i<sizeof(invalid)/sizeof(invalid[0]);i++){char wire[256];snprintf(wire,sizeof(wire),"%.*s%s}",(int)strlen(valid)-3,valid,invalid[i]);assert(!ble_owner_proof(wire,&sequence));}
    cJSON *root=cJSON_Parse(valid);cJSON_AddStringToObject(root,"hostId","22222222222222222222222222222222");char *wire=cJSON_PrintUnformatted(root);assert(!ble_owner_proof(wire,&sequence));free(wire);cJSON_Delete(root);
    assert(!ble_owner_proof("[]",&sequence)&&!ble_owner_proof("bad",&sequence));
    puts("BLE_OWNER_PROOF_TEST_OK selected host/device and integer sequence; wrong host/device/type; duplicates; malformed and unsafe sequence rejected");
}
