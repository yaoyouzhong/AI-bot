// Exercise the production host store/claim/selection implementation. Only NVS,
// RTOS and physical I/O are fakes; Windows BCrypt supplies real HMAC-SHA256.
#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdatomic.h>
#include <windows.h>
#include <bcrypt.h>
#include "cJSON.h"
#include "../../firmware/main/trusted_hosts.h"
#include "../../firmware/main/host_labels.h"
#include "../../firmware/main/host_ble_peers.h"
#include "../../firmware/main/link_state.h"
#define portMAX_DELAY 0
#define ESP_OK 0
#define ESP_ERR_NVS_NOT_FOUND 1
#define MBEDTLS_MD_SHA256 1
typedef int esp_err_t;
static int device_mutex,settings;
static link_state device_links;
static cJSON *device_snapshots[3],*device_metrics[3];
static uint64_t device_metrics_sequence[3];
static struct {char id[13];uint8_t key[32];bool paired;char host[16];int port;} config;
static trusted_host_store persisted;
static host_ble_peers persisted_peers;
static host_label_store persisted_labels;
static int label_storage_result=ESP_ERR_NVS_NOT_FOUND;
static int peer_storage_result=ESP_ERR_NVS_NOT_FOUND;
static int storage_result=ESP_ERR_NVS_NOT_FOUND,commits,cancelled,ble_dropped;
static bool save_failed,plugged=true;
static bool upgrading;
static bool ota_update_busy(void){return upgrading;}
static unsigned usb_epoch=1,nonce_counter;
static int64_t milliseconds=1000;
static void xSemaphoreTake(int lock,int wait){(void)lock;(void)wait;}
static void xSemaphoreGive(int lock){(void)lock;}
static int64_t esp_timer_get_time(void){return milliseconds*1000;}
unsigned usb_keyboard_epoch(void){return usb_epoch;}
static bool tab5_usb_connected(void){return plugged;}
void usb_keyboard_cancel(void){cancelled++;}
void ble_keyboard_cancel(void){cancelled++;}
void unlock_remote_cancel(void){cancelled++;}
void ble_host_changed(void){ble_dropped++;}
void rpc_link_reset(link_kind link){assert(link==LINK_USB||link==LINK_BLE);}
static int nvs_get_blob(int handle,const char *key,void *out,size_t *size){(void)handle;if(!strcmp(key,"host_labels1")){if(label_storage_result==ESP_OK){assert(*size==sizeof(persisted_labels));memcpy(out,&persisted_labels,*size);}return label_storage_result;}if(!strcmp(key,"host_ble_v1")){if(peer_storage_result==ESP_OK){assert(*size==sizeof(persisted_peers));memcpy(out,&persisted_peers,*size);}return peer_storage_result;}assert(!strcmp(key,"trusted_hosts1"));if(storage_result==ESP_OK){assert(*size==sizeof(persisted));memcpy(out,&persisted,*size);}return storage_result;}
static int nvs_set_blob(int handle,const char *key,const void *value,size_t size){(void)handle;if(save_failed)return 2;if(!strcmp(key,"host_labels1")){assert(size==sizeof(persisted_labels));memcpy(&persisted_labels,value,size);label_storage_result=ESP_OK;return 0;}if(!strcmp(key,"host_ble_v1")){assert(size==sizeof(persisted_peers));memcpy(&persisted_peers,value,size);peer_storage_result=ESP_OK;return 0;}assert(!strcmp(key,"trusted_hosts1")&&size==sizeof(persisted));memcpy(&persisted,value,size);storage_result=ESP_OK;return 0;}
static int nvs_commit(int handle){(void)handle;commits++;return 0;}
static const char *str(cJSON *root,const char *key){return cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(root,key));}
static bool private_host(const char *value){unsigned a,b,c,d;char tail;return value&&sscanf(value,"%u.%u.%u.%u%c",&a,&b,&c,&d,&tail)==4&&b<256&&c<256&&d<256&&(a==10||(a==192&&b==168)||(a==172&&b>=16&&b<=31));}
static void device_nonce(char out[33]){snprintf(out,33,"%032x",++nonce_counter);}
static const void *mbedtls_md_info_from_type(int type){assert(type==MBEDTLS_MD_SHA256);return (void *)1;}
static int mbedtls_md_hmac(const void *info,const uint8_t *key,size_t key_size,const uint8_t *input,size_t count,uint8_t out[32]) {
    (void)info;BCRYPT_ALG_HANDLE alg;BCRYPT_HASH_HANDLE hash;
    assert(BCryptOpenAlgorithmProvider(&alg,BCRYPT_SHA256_ALGORITHM,NULL,BCRYPT_ALG_HANDLE_HMAC_FLAG)==0);
    assert(BCryptCreateHash(alg,&hash,NULL,0,(PUCHAR)key,(ULONG)key_size,0)==0);
    assert(BCryptHashData(hash,(PUCHAR)input,(ULONG)count,0)==0);
    assert(BCryptFinishHash(hash,out,32,0)==0);BCryptDestroyHash(hash);BCryptCloseAlgorithmProvider(alg,0);return 0;
}
#include "../../firmware/main/host_binding_runtime.inc"
static cJSON *claim_for(int index,const char *nonce,const char *address) {
    trusted_host *host=&trusted_hosts.entries[index];char message[256],proof[65];uint8_t digest[32];
    snprintf(message,sizeof(message),"TAB5_HOST_V1|%s|%s|%s|%s|18765|%s",config.id,host->id,nonce,address,host->name);
    mbedtls_md_hmac(NULL,host->key,32,(uint8_t *)message,strlen(message),digest);
    for(int i=0;i<32;i++)snprintf(proof+i*2,3,"%02x",digest[i]);
    cJSON *root=cJSON_CreateObject();cJSON_AddNumberToObject(root,"version",1);cJSON_AddStringToObject(root,"deviceId",config.id);cJSON_AddStringToObject(root,"hostId",host->id);cJSON_AddStringToObject(root,"name",host->name);cJSON_AddStringToObject(root,"nonce",nonce);cJSON_AddStringToObject(root,"host",address);cJSON_AddNumberToObject(root,"port",18765);cJSON_AddStringToObject(root,"proof",proof);return root;
}
int main(void) {
    assert(trusted_host_public_command("tab5_pair")&&trusted_host_public_command("tab5_ping")&&trusted_host_public_command("tab5_ota_status"));
    assert(!trusted_host_public_command("tab5_wifi")&&!trusted_host_public_command("tab5_wifi_forget")&&!trusted_host_public_command("tab5_hid_toggle")&&!trusted_host_public_command("tab5_benchmark")&&!trusted_host_public_command("tab5_status")&&!trusted_host_public_command("tab5_rpc"));
    strcpy(config.id,"e8f60ae2ec56");memset(config.key,0x11,32);config.paired=true;strcpy(config.host,"192.168.1.20");config.port=18765;link_state_init(&device_links);
    host_init();assert(trusted_hosts_readable&&trusted_hosts.count==1&&commits==1);
    char win_id[33];host_key_id(config.key,win_id);assert(!strcmp(win_id,trusted_hosts.entries[0].id));
    uint8_t type=99,address[6]={0},win_peer[6]={1,2,3,4,5,6},mac_peer[6]={6,5,4,3,2,1};
    assert(device_host_ble_target(&type,address)==0);
    assert(!device_host_ble_bind(device_host_epoch()-1,0,win_peer));
    assert(device_host_ble_bind(device_host_epoch(),0,win_peer));
    assert(device_host_ble_target(&type,address)==1&&type==0&&!memcmp(address,win_peer,6));
    assert(!device_host_ble_possible(0,mac_peer));
    trusted_host mac={0};memset(mac.key,0x22,32);host_key_id(mac.key,mac.id);strcpy(mac.name,"MacBook");strcpy(mac.address,"192.168.1.21");mac.port=18765;
    assert(trusted_hosts_put(&trusted_hosts,&mac)&&host_save(&trusted_hosts));assert(trusted_hosts.count==2&&trusted_hosts.selected==0);
    assert(!memcmp(config.key,trusted_hosts.entries[0].key,32));
    char nonce[33];device_host_challenge(nonce);cJSON *root=claim_for(1,nonce,mac.address);
    cJSON_SetValuestring(cJSON_GetObjectItemCaseSensitive(root,"host"),"192.168.1.99");assert(!device_host_claim(root));cJSON_Delete(root);
    root=claim_for(1,nonce,mac.address);cJSON_SetValuestring(cJSON_GetObjectItemCaseSensitive(root,"hostId"),"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");assert(!device_host_claim(root));cJSON_Delete(root);
    root=claim_for(1,nonce,mac.address);device_snapshots[0]=cJSON_CreateObject();device_metrics[0]=cJSON_CreateObject();device_links.received_ms[0]=1000;
    unsigned generation=device_host_epoch();cJSON *reply=device_host_claim(root);assert(reply);cJSON_Delete(reply);
    assert(trusted_hosts.selected==1&&device_host_epoch()!=generation&&device_host_usb_owned());
    device_discovered_host("192.168.1.99",18765,generation);assert(!strcmp(trusted_hosts.entries[1].address,mac.address));
    device_discovered_host(mac.address,18765,device_host_epoch());assert(trusted_hosts.selected==1&&!strcmp(trusted_hosts.entries[1].address,mac.address));
    assert(!device_host_ble_possible(0,win_peer)&&device_host_ble_target(&type,address)==0);
    assert(device_host_ble_bind(device_host_epoch(),0,mac_peer));
    assert(!device_snapshots[0]&&!device_metrics[0]&&device_links.received_ms[0]<0&&cancelled==3&&ble_dropped==1);
    assert(!device_host_claim(root));cJSON_Delete(root);assert(!device_host_select(0));
    plugged=false;usb_epoch++;assert(!device_host_usb_owned());assert(device_host_select(0));assert(trusted_hosts.selected==0);
    plugged=true;device_host_challenge(nonce);root=claim_for(0,nonce,"192.168.1.20");usb_epoch++;assert(!device_host_claim(root));cJSON_Delete(root);
    device_host_challenge(nonce);root=claim_for(1,nonce,mac.address);upgrading=true;reply=device_host_claim(root);assert(reply&&trusted_hosts.selected==0&&!strcmp(str(reply,"error"),"device_busy"));cJSON_Delete(reply);cJSON_Delete(root);assert(!device_host_select(1));upgrading=false;
    device_host_challenge(nonce);root=claim_for(0,nonce,"192.168.1.20");reply=device_host_claim(root);assert(reply);cJSON_Delete(reply);cJSON_Delete(root);
    milliseconds+=8001;assert(!device_host_usb_owned());assert(device_host_select(1));
    plugged=false;assert(device_host_select(0));plugged=true;device_host_challenge(nonce);root=claim_for(1,nonce,mac.address);save_failed=true;assert(!device_host_claim(root));assert(trusted_hosts.selected==0);save_failed=false;cJSON_Delete(root);
    for(unsigned i=2;i<TRUSTED_HOST_LIMIT;i++){trusted_host h={0};memset(h.key,(int)i+1,32);host_key_id(h.key,h.id);strcpy(h.name,"Trusted computer");assert(trusted_hosts_put(&trusted_hosts,&h));}
    trusted_host extra={0};memset(extra.key,99,32);host_key_id(extra.key,extra.id);strcpy(extra.name,"Extra");trusted_host_store before=trusted_hosts;assert(!trusted_hosts_put(&trusted_hosts,&extra));assert(!memcmp(&before,&trusted_hosts,sizeof(before)));
    persisted=trusted_hosts;config.paired=false;memset(config.key,0,32);storage_result=ESP_OK;host_init();assert(config.paired&&!memcmp(config.key,trusted_hosts.entries[0].key,32)); // interrupted legacy mirror
    assert(device_host_ble_target(&type,address)==1&&!memcmp(address,win_peer,6));
    assert(host_ble_peer_index(&host_peers,trusted_hosts.entries[1].id)>=0);
    plugged=false;assert(device_host_select(2));uint8_t extra_peer[6]={9,8,7,6,5,4};save_failed=true;
    assert(!device_host_ble_bind(device_host_epoch(),0,extra_peer)&&device_host_ble_target(&type,address)==0);save_failed=false;
    host_ble_peers saved_peers=persisted_peers;persisted_peers.version=99;host_ble_init();assert(!host_peers_readable&&device_host_ble_target(&type,address)==-1);persisted_peers=saved_peers;
    // Aliases never change trust, keys, selected host, generations or USB proofs.
    before=trusted_hosts;generation=device_host_epoch();host_ble_peers peers_before=host_peers;
    assert(device_host_rename(win_id,"  办公室电脑  "));
    assert(!memcmp(&before,&trusted_hosts,sizeof(before))&&device_host_epoch()==generation&&!memcmp(&peers_before,&host_peers,sizeof(peers_before)));
    char label_id[33],label_name[TRUSTED_HOST_NAME],labels[TRUSTED_HOST_LIMIT][TRUSTED_HOST_NAME];int selected;
    assert(device_host_label(0,label_id,label_name)&&!strcmp(label_id,win_id)&&!strcmp(label_name,"办公室电脑"));
    device_host_names(labels,&selected);assert(!strcmp(labels[0],label_name)&&selected==(int)trusted_hosts.selected);
    assert(!device_host_rename("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","Unknown"));
    assert(!device_host_rename(win_id,"bad\nname")&&!device_host_rename(win_id,"bad|name"));
    char long_name[65];memset(long_name,'x',64);long_name[64]=0;assert(!device_host_rename(win_id,long_name));
    save_failed=true;assert(!device_host_rename(win_id,"Not saved"));save_failed=false;
    device_host_label(0,label_id,label_name);assert(!strcmp(label_name,"办公室电脑"));
    // Normal USB claims update the original machine name, not the user's alias.
    strcpy(trusted_hosts.entries[0].name,"Windows Laptop");assert(host_save(&trusted_hosts));
    plugged=true;device_host_challenge(nonce);root=claim_for(0,nonce,"192.168.1.20");reply=device_host_claim(root);assert(reply);cJSON_Delete(reply);cJSON_Delete(root);
    device_host_identity(label_id,label_name);assert(!strcmp(label_name,"办公室电脑")&&device_host_usb_owned());
    host_init();device_host_identity(label_id,label_name);assert(!strcmp(label_name,"办公室电脑"));
    assert(device_host_rename(win_id,""));device_host_identity(label_id,label_name);assert(!strcmp(label_name,"Windows Laptop"));
    for(unsigned i=0;i<TRUSTED_HOST_LIMIT;i++){char alias[32];snprintf(alias,sizeof(alias),"Computer %u",i+1);assert(device_host_rename(trusted_hosts.entries[i].id,alias));}
    assert(host_labels.count==TRUSTED_HOST_LIMIT&&host_labels_valid(&host_labels));
    host_label_store labels_before=persisted_labels;persisted_labels.version=99;host_labels_init();
    assert(!host_labels_readable&&!device_host_rename(win_id,"Do not overwrite corrupt labels"));
    device_host_identity(label_id,label_name);assert(!strcmp(label_name,"Windows Laptop")&&trusted_hosts_readable);
    persisted_labels=labels_before;host_labels_init();assert(host_labels_readable);
    puts("HOST_LABELS_OK Chinese aliases, trim, rename unselected host, independent trust/keys/bonds, USB claim and reboot retention, restore original, failure retention, capacity and corruption isolation");
    persisted.version=99;trusted_hosts_readable=true;host_init();assert(!trusted_hosts_readable); // no stale legacy resurrection
    assert(!trusted_host_name_valid("bad|name")&&!trusted_host_name_valid("bad\nname"));
    printf("TRUSTED_HOSTS_TEST_OK migration; independent_keys; USB_takeover; BLE_peer_isolation; reboot_retention; stale_epoch_rejected; NVS_failure; corrupt_store_closed; identity=%s\n",win_id);
    return 0;
}
