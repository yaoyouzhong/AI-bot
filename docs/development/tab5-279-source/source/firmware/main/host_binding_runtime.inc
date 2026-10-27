// Included by device.c. Existing config ABI/key is retained for rollback;
// this separately committed blob is authoritative after the first migration.
#include "trusted_hosts.h"
#include "host_labels.h"
#include "rpc_link.h"
#include <stdatomic.h>
static trusted_host_store trusted_hosts={.version=1};
static bool trusted_hosts_readable=true;
static host_label_store host_labels={.version=1};
static bool host_labels_readable=true;
static void host_labels_init(void) {
    host_label_store saved;size_t size=sizeof(saved);
    esp_err_t result=nvs_get_blob(settings,"host_labels1",&saved,&size);
    host_labels=(host_label_store){.version=1};
    host_labels_readable=result==ESP_ERR_NVS_NOT_FOUND||
        (result==ESP_OK&&size==sizeof(saved)&&host_labels_valid(&saved));
    if(result==ESP_OK&&host_labels_readable)host_labels=saved;
}
static const char *host_name_locked(unsigned index) {
    const trusted_host *host=&trusted_hosts.entries[index];
    int at=host_labels_readable?host_label_index(&host_labels,host->id):-1;
    return at>=0?host_labels.entries[at].name:host->name;
}
static atomic_uint host_epoch=1;
static atomic_bool host_switching;
static atomic_flag host_transition=ATOMIC_FLAG_INIT;
unsigned device_host_epoch(void);
bool device_host_changing(void);
static char usb_host_nonce[33];
static unsigned usb_host_nonce_epoch,usb_host_owner_epoch;
static int usb_host_owner=-1;
static int64_t usb_host_seen=-1;
extern unsigned usb_keyboard_epoch(void);
extern void usb_keyboard_cancel(void);
extern void ble_keyboard_cancel(void);
extern void ble_host_changed(void);
extern void unlock_remote_cancel(void);
static void host_key_id(const uint8_t key[32],char id[33]) {
    uint8_t digest[32];static const char domain[]="AIBOT_TAB5_HOST_V1";
    if(mbedtls_md_hmac(mbedtls_md_info_from_type(MBEDTLS_MD_SHA256),key,32,(const uint8_t *)domain,sizeof(domain)-1,digest)){id[0]=0;return;}
    for(unsigned i=0;i<16;i++)snprintf(id+i*2,3,"%02x",digest[i]);
    memset(digest,0,sizeof(digest));
}
static bool host_save(const trusted_host_store *next) {
    return trusted_hosts_valid(next)&&nvs_set_blob(settings,"trusted_hosts1",next,sizeof(*next))==ESP_OK&&nvs_commit(settings)==ESP_OK;
}
#include "host_ble_runtime.inc"
static void host_recover_legacy_pairing(void) {
    // A power loss can commit the new store before the legacy config. The
    // committed trust is authoritative; initialize only an absent legacy pair.
    if(trusted_hosts_readable&&trusted_hosts.count&&!config.paired){trusted_host *first=&trusted_hosts.entries[0];memcpy(config.key,first->key,32);strcpy(config.host,first->address);config.port=first->port;config.paired=true;}
}
static void host_init(void) {
    trusted_host_store saved;size_t size=sizeof(saved);
    esp_err_t r=nvs_get_blob(settings,"trusted_hosts1",&saved,&size);
    if(r==ESP_OK&&size==sizeof(saved)&&trusted_hosts_valid(&saved))trusted_hosts=saved;
    else if(r==ESP_ERR_NVS_NOT_FOUND) {
        if(config.paired) {
            trusted_host old={0};host_key_id(config.key,old.id);strcpy(old.name,"已配对电脑");
            if(!trusted_host_id_valid(old.id)){trusted_hosts_readable=false;return;}
            memcpy(old.key,config.key,32);strcpy(old.address,config.host);old.port=config.port;
            trusted_hosts_put(&trusted_hosts,&old);
        }
        trusted_hosts_readable=host_save(&trusted_hosts);
    } else trusted_hosts_readable=false;
    host_recover_legacy_pairing();
    host_labels_init();
    host_ble_init();
}
static bool host_usb_owned_locked(void) {
    int64_t now=esp_timer_get_time()/1000;
    return trusted_hosts_readable&&usb_host_owner>=0&&(unsigned)usb_host_owner==trusted_hosts.selected&&
        usb_host_owner_epoch==usb_keyboard_epoch()&&tab5_usb_connected()&&usb_host_seen>=0&&now>=usb_host_seen&&now-usb_host_seen<8000;
}
bool device_host_usb_owned(void) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);bool ok=!atomic_load(&host_switching)&&host_usb_owned_locked();xSemaphoreGive(device_mutex);return ok;
}
unsigned device_host_epoch(void){return atomic_load(&host_epoch);}
bool device_host_changing(void){return atomic_load(&host_switching);}
// A late discovery reply cannot replace the newly selected computer's endpoint.
void device_discovered_host(const char *host,int port,unsigned epoch) {
    if(!private_host(host)||strlen(host)>=sizeof(config.host)||port<=0||port>65535)return;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    if(epoch==device_host_epoch()&&!device_host_changing()&&trusted_hosts_readable&&trusted_hosts.count){
        trusted_host *h=&trusted_hosts.entries[trusted_hosts.selected];strcpy(h->address,host);h->port=port;
    }
    xSemaphoreGive(device_mutex);
}
void device_host_usb_touch(void) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    if(usb_host_owner>=0&&(unsigned)usb_host_owner==trusted_hosts.selected&&usb_host_owner_epoch==usb_keyboard_epoch()&&tab5_usb_connected())usb_host_seen=esp_timer_get_time()/1000;
    xSemaphoreGive(device_mutex);
}
static void host_invalidate_locked(void) {
    atomic_fetch_add(&host_epoch,1);usb_host_owner=-1;usb_host_seen=-1;
    int mode=device_links.mode;link_state_init(&device_links);device_links.mode=mode;
    for(int i=0;i<3;i++){cJSON_Delete(device_snapshots[i]);device_snapshots[i]=NULL;cJSON_Delete(device_metrics[i]);device_metrics[i]=NULL;device_metrics_sequence[i]=0;}
}
static void host_quiesce_io(void) {
    // Block new input, then drain each keyboard's admission/queueing mutex
    // before publishing a different owner. Never hold device_mutex here.
    unlock_remote_cancel();usb_keyboard_cancel();ble_keyboard_cancel();
}
static void host_invalidate_io(void) {
    rpc_link_reset(LINK_USB);rpc_link_reset(LINK_BLE);ble_host_changed();
}
int device_host_names(char names[TRUSTED_HOST_LIMIT][TRUSTED_HOST_NAME],int *selected) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);int count=trusted_hosts_readable?(int)trusted_hosts.count:0;
    for(int i=0;i<count;i++)strcpy(names[i],host_name_locked(i));
    if(selected)*selected=count?(int)trusted_hosts.selected:-1;
    xSemaphoreGive(device_mutex);return count;
}
void device_host_status(char id[33],char name[TRUSTED_HOST_NAME],link_kind *link) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);id[0]=name[0]=0;
    if(trusted_hosts_readable&&trusted_hosts.count){strcpy(id,trusted_hosts.entries[trusted_hosts.selected].id);strcpy(name,host_name_locked(trusted_hosts.selected));}
    if(link)*link=id[0]&&!device_host_changing()?link_state_active(&device_links,esp_timer_get_time()/1000):LINK_NONE;
    xSemaphoreGive(device_mutex);
}
void device_host_identity(char id[33],char name[TRUSTED_HOST_NAME]) {device_host_status(id,name,NULL);}
bool device_host_label(int index,char id[33],char name[TRUSTED_HOST_NAME]) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);id[0]=name[0]=0;
    bool ok=trusted_hosts_readable&&index>=0&&(unsigned)index<trusted_hosts.count;
    if(ok){strcpy(id,trusted_hosts.entries[index].id);strcpy(name,host_name_locked(index));}
    xSemaphoreGive(device_mutex);return ok;
}
bool device_host_rename(const char *id,const char *name) {
    if(!trusted_host_id_valid(id)||!name||strlen(name)>=TRUSTED_HOST_NAME)return false;
    char clean[TRUSTED_HOST_NAME];while(*name==' ')name++;
    strcpy(clean,name);size_t length=strlen(clean);while(length&&clean[length-1]==' ')clean[--length]=0;
    if(clean[0]&&!trusted_host_name_valid(clean))return false;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    bool ok=trusted_hosts_readable&&host_labels_readable&&trusted_host_index(&trusted_hosts,id)>=0&&!ota_update_busy();
    if(ok) {
        host_label_store next=host_labels;int at=host_label_index(&next,id);
        if(!clean[0]) {
            if(at<0){xSemaphoreGive(device_mutex);return true;}
            memmove(&next.entries[at],&next.entries[at+1],(next.count-at-1)*sizeof(next.entries[0]));
            memset(&next.entries[--next.count],0,sizeof(next.entries[0]));
        }else {
            if(at<0){if(next.count==TRUSTED_HOST_LIMIT){xSemaphoreGive(device_mutex);return false;}at=(int)next.count++;}
            strcpy(next.entries[at].id,id);strcpy(next.entries[at].name,clean);
        }
        if(memcmp(&next,&host_labels,sizeof(next))) {
            ok=host_labels_valid(&next)&&nvs_set_blob(settings,"host_labels1",&next,sizeof(next))==ESP_OK&&nvs_commit(settings)==ESP_OK;
            if(ok)host_labels=next;
        }
    }
    xSemaphoreGive(device_mutex);return ok;
}
bool device_host_select(int index) {
    if(atomic_flag_test_and_set(&host_transition))return false;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    bool ok=trusted_hosts_readable&&index>=0&&(unsigned)index<trusted_hosts.count;
    bool changed=ok&&(unsigned)index!=trusted_hosts.selected;
    if(changed&&(host_usb_owned_locked()||ota_update_busy()))ok=false;
    if(ok&&changed){atomic_store(&host_switching,true);xSemaphoreGive(device_mutex);host_quiesce_io();xSemaphoreTake(device_mutex,portMAX_DELAY);
        trusted_host_store next=trusted_hosts;next.selected=index;ok=!host_usb_owned_locked()&&host_save(&next);if(ok){trusted_hosts=next;host_invalidate_locked();}}
    xSemaphoreGive(device_mutex);if(ok&&changed)host_invalidate_io();atomic_store(&host_switching,false);atomic_flag_clear(&host_transition);return ok;
}
void device_host_challenge(char nonce[33]) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);unsigned epoch=usb_keyboard_epoch();
    if(!usb_host_nonce[0]||epoch!=usb_host_nonce_epoch){device_nonce(usb_host_nonce);usb_host_nonce_epoch=epoch;}
    strcpy(nonce,usb_host_nonce);xSemaphoreGive(device_mutex);
}
// One USB challenge, one immutable endpoint/name, one key-bound identity.
cJSON *device_host_claim(cJSON *root) {
    if(!tab5_usb_connected())return NULL;
    const char *id=str(root,"hostId"),*nonce=str(root,"nonce"),*proof=str(root,"proof"),*name=str(root,"name"),*address=str(root,"host");
    const char *device=str(root,"deviceId");cJSON *port=cJSON_GetObjectItemCaseSensitive(root,"port");
    cJSON *version=cJSON_GetObjectItemCaseSensitive(root,"version");
    if(!device||strcmp(device,config.id)||!cJSON_IsNumber(version)||version->valuedouble!=1||!trusted_host_id_valid(id)||!nonce||strlen(nonce)!=32||!proof||strlen(proof)!=64||!trusted_host_name_valid(name)||
       !address||(*address&&!private_host(address))||strlen(address)>15||!cJSON_IsNumber(port)||port->valuedouble!=port->valueint||port->valueint<0||port->valueint>65535)return NULL;
    if(atomic_flag_test_and_set(&host_transition))return NULL;
    char message[256],expected[65];uint8_t digest[32]={0};bool changed=false,ok=false;
    xSemaphoreTake(device_mutex,portMAX_DELAY);int at=trusted_hosts_readable?trusted_host_index(&trusted_hosts,id):-1;
    if(at>=0&&tab5_usb_connected()&&usb_host_nonce_epoch==usb_keyboard_epoch()&&!strcmp(nonce,usb_host_nonce)) {
        snprintf(message,sizeof(message),"TAB5_HOST_V1|%s|%s|%s|%s|%d|%s",config.id,id,nonce,address,port->valueint,name);
        int signed_ok=mbedtls_md_hmac(mbedtls_md_info_from_type(MBEDTLS_MD_SHA256),trusted_hosts.entries[at].key,32,(uint8_t *)message,strlen(message),digest);
        for(int i=0;i<32;i++)snprintf(expected+i*2,3,"%02x",digest[i]);
        unsigned diff=0;for(int i=0;i<64;i++)diff|=(unsigned char)expected[i]^(unsigned char)proof[i];
        if(!diff&&!signed_ok) {
            if((unsigned)at!=trusted_hosts.selected&&ota_update_busy()) {
                xSemaphoreGive(device_mutex);atomic_flag_clear(&host_transition);
                cJSON *busy=cJSON_CreateObject();cJSON_AddStringToObject(busy,"type","tab5_host_claimed");cJSON_AddStringToObject(busy,"error","device_busy");return busy;
            }
            if((unsigned)at!=trusted_hosts.selected){atomic_store(&host_switching,true);xSemaphoreGive(device_mutex);host_quiesce_io();xSemaphoreTake(device_mutex,portMAX_DELAY);}
            trusted_host_store next=trusted_hosts;next.selected=at;trusted_host *h=&next.entries[at];strcpy(h->name,name);strcpy(h->address,address);h->port=port->valueint;
            ok=tab5_usb_connected()&&usb_host_nonce_epoch==usb_keyboard_epoch()&&!strcmp(nonce,usb_host_nonce)&&
                (!memcmp(&next,&trusted_hosts,sizeof(next))||host_save(&next));
            if(ok){changed=(unsigned)at!=trusted_hosts.selected;trusted_hosts=next;if(changed)host_invalidate_locked();usb_host_owner=at;usb_host_owner_epoch=usb_keyboard_epoch();usb_host_seen=esp_timer_get_time()/1000;}
        }
    }
    if(ok){snprintf(message,sizeof(message),"TAB5_HOST_ACK_V1|%s|%s|%s",config.id,id,nonce);
        ok=mbedtls_md_hmac(mbedtls_md_info_from_type(MBEDTLS_MD_SHA256),trusted_hosts.entries[at].key,32,(uint8_t *)message,strlen(message),digest)==0;
        for(int i=0;i<32;i++){snprintf(expected+i*2,3,"%02x",digest[i]);}device_nonce(usb_host_nonce);}
    xSemaphoreGive(device_mutex);if(changed)host_invalidate_io();atomic_store(&host_switching,false);atomic_flag_clear(&host_transition);
    if(!ok)return NULL;
    cJSON *reply=cJSON_CreateObject();cJSON_AddNumberToObject(reply,"version",1);cJSON_AddStringToObject(reply,"type","tab5_host_claimed");cJSON_AddStringToObject(reply,"deviceId",device);cJSON_AddStringToObject(reply,"hostId",id);cJSON_AddStringToObject(reply,"proof",expected);return reply;
}
