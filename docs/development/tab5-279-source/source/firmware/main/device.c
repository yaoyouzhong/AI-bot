#include "device.h"
#include "assets.h"
#include "wifi_profiles.h"
#include <string.h>
#include <stdio.h>
#include <stdlib.h>
#include "nvs.h"
#include "esp_mac.h"
#include "esp_random.h"
#include "esp_timer.h"
#include "esp_heap_caps.h"
#include "mbedtls/base64.h"
#include "mbedtls/md.h"
#include "mbedtls/gcm.h"
#include "miniz.h"
#include "ota_update.h"

SemaphoreHandle_t device_mutex;
link_state device_links;
cJSON *device_snapshots[3];
cJSON *device_metrics[3];
uint64_t device_metrics_sequence[3];
static device_config config;
static device_sound sound={35,false};
static wifi_profile_store networks={.version=WIFI_PROFILE_VERSION};
static nvs_handle_t settings;
static const char *str(cJSON *o,const char *key) {const cJSON *v=cJSON_GetObjectItemCaseSensitive(o,key);return cJSON_IsString(v)?v->valuestring:NULL;}
static bool private_host(const char *h) {
    unsigned a,b,c,d;char tail;
    return h && sscanf(h,"%u.%u.%u.%u%c",&a,&b,&c,&d,&tail)==4 && b<256 && c<256 && d<256 && (a==10 || (a==192&&b==168)||(a==172&&b>=16&&b<=31));
}
static bool save(void) {
    return nvs_set_blob(settings,"config",&config,sizeof(config)) == ESP_OK && nvs_commit(settings) == ESP_OK;
}
#include "host_binding_runtime.inc"
static void *json_alloc(size_t size) {
    // Three status snapshots and the LVGL copy contain many small JSON nodes.
    // They never run in an ISR and must not consume the camera/USB DMA heap.
    return heap_caps_malloc(size,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
}
void device_init(void) {
    cJSON_Hooks hooks={.malloc_fn=json_alloc,.free_fn=free};
    cJSON_InitHooks(&hooks); // Install before USB, Wi-Fi, BLE or UI tasks start.
    device_mutex=xSemaphoreCreateMutex();link_state_init(&device_links);
    config.mode=-1;config.brightness=75;
    ESP_ERROR_CHECK(nvs_open("aibot_tab5",NVS_READWRITE,&settings));
    // Separate key preserves the pairing/config blob layout across upgrades.
    uint16_t sound_value=35;
    if(nvs_get_u16(settings,"sound_v1",&sound_value)==ESP_OK &&
       !(sound_value&~0x17f) && (sound_value&0x7f)<=100)
        sound=(device_sound){sound_value&0x7f,(sound_value&0x100)!=0};
    size_t size=sizeof(config);device_config saved;
    if(nvs_get_blob(settings,"config",&saved,&size)==ESP_OK&&size==sizeof(config)) config=saved;
    uint8_t mac[6];ESP_ERROR_CHECK(esp_efuse_mac_get_default(mac));
    snprintf(config.id,sizeof(config.id),"%02x%02x%02x%02x%02x%02x",mac[0],mac[1],mac[2],mac[3],mac[4],mac[5]);
    config.ssid[32]=0;config.password[63]=0;config.host[15]=0;
    // Separate versioned blob: never change the existing pairing/config ABI.
    size=sizeof(networks);wifi_profile_store stored_networks;
    esp_err_t profiles_result=nvs_get_blob(settings,"wifi_profiles3",&stored_networks,&size);
    if(profiles_result==ESP_OK&&
       size==sizeof(stored_networks)&&wifi_profiles_valid(&stored_networks)) {
        networks=stored_networks;
        // Once migrated, the saved list is authoritative, including an empty list.
        // Never resurrect a forgotten SSID from the legacy config blob.
        wifi_profiles_sync_active(&networks,config.ssid,config.password);
    } else if(profiles_result==ESP_ERR_NVS_NOT_FOUND) {
        // Keep both five-entry blobs intact for rollback. Migrate only when the
        // new key is absent; never resurrect deleted networks from an old key.
        wifi_profile_store_v2 legacy_v2;size=sizeof(legacy_v2);
        if(nvs_get_blob(settings,"wifi_profiles2",&legacy_v2,&size)==ESP_OK&&size==sizeof(legacy_v2)&&wifi_profiles_migrate_v2(&networks,&legacy_v2)) {
            wifi_profiles_sync_active(&networks,config.ssid,config.password);
        } else {
        wifi_profile_store_v1 legacy;size=sizeof(legacy);
        if(nvs_get_blob(settings,"wifi_profiles",&legacy,&size)==ESP_OK&&size==sizeof(legacy)&&wifi_profiles_migrate(&networks,&legacy))
            wifi_profiles_sync_active(&networks,config.ssid,config.password);
        else if(config.ssid[0])wifi_profiles_put(&networks,config.ssid,config.password);
        }
        ESP_ERROR_CHECK(nvs_set_blob(settings,"wifi_profiles3",&networks,sizeof(networks)));
        ESP_ERROR_CHECK(nvs_commit(settings));
    } else {
        // A corrupt current store must not silently restore stale credentials.
        config.ssid[0]=config.password[0]=0;
    }
    if(config.mode < -1 || config.mode>2) config.mode=-1;
    if(config.brightness<10 || config.brightness>100) config.brightness=75;
    device_links.mode=config.mode;
    host_init();
}
device_config device_get_config(void) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);device_config copy=config;
    copy.paired=trusted_hosts_readable&&trusted_hosts.count>0;
    if(copy.paired){trusted_host *h=&trusted_hosts.entries[trusted_hosts.selected];memcpy(copy.key,h->key,32);strcpy(copy.host,h->address);copy.port=h->port;}
    xSemaphoreGive(device_mutex);return copy;
}
device_sound device_get_sound(void) {xSemaphoreTake(device_mutex,portMAX_DELAY);device_sound copy=sound;xSemaphoreGive(device_mutex);return copy;}
int device_sound_volume(void) {device_sound s=device_get_sound();return s.muted?0:s.volume;}
bool device_set_sound(int volume,bool muted) {
    if(volume<0||volume>100)return false;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    bool ok=true;
    if(sound.volume!=volume||sound.muted!=muted) {
        ok=nvs_set_u16(settings,"sound_v1",(uint16_t)(volume|(muted?0x100:0)))==ESP_OK && nvs_commit(settings)==ESP_OK;
        if(ok)sound=(device_sound){volume,muted};
    }
    xSemaphoreGive(device_mutex);return ok;
}
int device_wifi_count(void) {xSemaphoreTake(device_mutex,portMAX_DELAY);int n=networks.count;xSemaphoreGive(device_mutex);return n;}
int device_wifi_names(char names[WIFI_PROFILE_LIMIT][33]) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);int count=networks.count;
    for(int i=0;i<count;i++)strcpy(names[i],networks.entries[i].ssid);
    xSemaphoreGive(device_mutex);return count;
}
void device_wifi_label(const char *ssid,char label[WIFI_LABEL_SIZE]) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);int at=wifi_profiles_index(&networks,ssid);
    snprintf(label,WIFI_LABEL_SIZE,"%s",at<0?"":networks.entries[at].label);xSemaphoreGive(device_mutex);
}
bool device_wifi_select(int index,bool persist) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    bool ok=index>=0&&index<(int)networks.count;
    if(ok) {
        device_config previous=config;unsigned preferred=networks.preferred;
        strcpy(config.ssid,networks.entries[index].ssid);strcpy(config.password,networks.entries[index].password);
        if(persist) {
            networks.preferred=index;
            ok=nvs_set_blob(settings,"wifi_profiles3",&networks,sizeof(networks))==ESP_OK&&save();
            if(!ok){config=previous;networks.preferred=preferred;}
        }
    }
    xSemaphoreGive(device_mutex);return ok;
}
bool device_wifi_forget(const char *ssid) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    wifi_profile_store updated=networks;
    bool ok=wifi_profiles_forget(&updated,ssid);
    // Commit one authoritative blob. A stale legacy config is reconciled at boot,
    // so interruption between two independent NVS keys cannot restore this SSID.
    if(ok)ok=nvs_set_blob(settings,"wifi_profiles3",&updated,sizeof(updated))==ESP_OK&&nvs_commit(settings)==ESP_OK;
    if(ok){networks=updated;wifi_profiles_sync_active(&networks,config.ssid,config.password);}
    xSemaphoreGive(device_mutex);return ok;
}
cJSON *device_wifi_public_list(void) {
    cJSON *list=cJSON_CreateArray();if(!list)return NULL;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    for(unsigned i=0;i<networks.count;i++) {
        cJSON *row=cJSON_CreateObject();
        if(!row||!cJSON_AddStringToObject(row,"ssid",networks.entries[i].ssid)||
           !cJSON_AddStringToObject(row,"label",networks.entries[i].label)||
           !cJSON_AddBoolToObject(row,"connected",wifi_connected&&!strcmp(config.ssid,networks.entries[i].ssid))||
           !cJSON_AddBoolToObject(row,"selected",!strcmp(config.ssid,networks.entries[i].ssid))) {
            cJSON_Delete(row);cJSON_Delete(list);list=NULL;break;
        }
        cJSON_AddItemToArray(list,row);
    }
    xSemaphoreGive(device_mutex);return list;
}
void device_set_mode(int mode) {if(mode<-1||mode>2)return;xSemaphoreTake(device_mutex,portMAX_DELAY);config.mode=mode;device_links.mode=mode;save();xSemaphoreGive(device_mutex);}
bool device_save_brightness(int value) {if(value<10||value>100)return false;xSemaphoreTake(device_mutex,portMAX_DELAY);int before=config.brightness;config.brightness=value;bool ok=save();if(!ok)config.brightness=before;xSemaphoreGive(device_mutex);return ok;}
void device_set_brightness(int value) {(void)device_save_brightness(value);}
char *device_unpack_json(cJSON *root,unsigned maximum) {
    cJSON *size=cJSON_GetObjectItemCaseSensitive(root,"size");const char *payload=str(root,"payload");
    if(!cJSON_IsNumber(size)||size->valueint<=0||(unsigned)size->valueint>maximum||size->valuedouble!=size->valueint||!payload||strlen(payload)>maximum)return NULL;
    size_t capacity=strlen(payload)*3/4+1,used=0;
    uint8_t *packed=heap_caps_malloc(capacity,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    char *clear=heap_caps_malloc(size->valueint+1,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    // The ROM convenience wrapper needs ~11 KiB stack; use PSRAM workspace.
    tinfl_decompressor *inflater=heap_caps_malloc(sizeof(*inflater),MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    char *result=NULL;
    if(packed&&clear&&inflater&&mbedtls_base64_decode(packed,capacity,&used,(const uint8_t*)payload,strlen(payload))==0) {
        tinfl_init(inflater);size_t input_size=used,output_size=size->valueint;
        tinfl_status status=tinfl_decompress(inflater,packed,&input_size,(uint8_t*)clear,(uint8_t*)clear,&output_size,
            TINFL_FLAG_PARSE_ZLIB_HEADER|TINFL_FLAG_USING_NON_WRAPPING_OUTPUT_BUF);
        if(status==TINFL_STATUS_DONE&&input_size==used&&output_size==(size_t)size->valueint) {
            clear[size->valueint]=0;
            if(strlen(clear)==(size_t)size->valueint){result=clear;clear=NULL;}
        }
    }
    free(inflater);free(packed);free(clear);return result;
}
#include "telemetry_delta.h"
#include "device_receive_reason.h"
static bool receive_frame(const char *json,link_kind link,uint64_t *sequence,bool allow_packed,device_reject_reason *reason,unsigned host_generation) {
    *reason=DEVICE_REJECT_ENVELOPE;
    if(!json||strlen(json)>TAB5_MAX_FRAME-28)return false;
    if(link<LINK_USB||link>LINK_BLE)return false;
    cJSON *root=cJSON_Parse(json);if(!root){*reason=DEVICE_REJECT_JSON;return false;}
    bool ok=false; const char *type=str(root,"type"),*id=str(root,"deviceId"),*session=str(root,"session");
    // USB plaintext belongs only to the bridge that proved its key on this
    // exact CDC generation. Radio traffic is decrypted with the selected key.
    if(link==LINK_USB&&!device_host_usb_owned()){cJSON_Delete(root);return false;}
    cJSON *version=cJSON_GetObjectItemCaseSensitive(root,"version"),*seq=cJSON_GetObjectItemCaseSensitive(root,"sequence"),*data=cJSON_GetObjectItemCaseSensitive(root,"data");
    if(allow_packed&&type&&!strcmp(type,"tab5_packed")) {
        char *clear=device_unpack_json(root,TAB5_MAX_FRAME-28);
        *reason=DEVICE_REJECT_PACKED;
        if(clear)ok=receive_frame(clear,link,sequence,false,reason,host_generation);
        free(clear);
        cJSON_Delete(root);return ok;
    }
    if(type&&!strcmp(type,"tab5_metrics")&&id&&session&&cJSON_IsNumber(version)&&version->valueint==1&&
       cJSON_IsNumber(seq)&&seq->valuedouble>0&&seq->valuedouble<=9007199254740991.0) {
        cJSON *metrics=cJSON_GetObjectItemCaseSensitive(root,"systemMetrics");
        xSemaphoreTake(device_mutex,portMAX_DELAY);if(host_generation!=device_host_epoch()){xSemaphoreGive(device_mutex);cJSON_Delete(root);*reason=DEVICE_REJECT_IDENTITY;return false;}
        if(config.paired&&!strcmp(config.id,id)&&device_snapshots[link]&&!strcmp(device_links.session[link],session)&&
           cJSON_IsObject(metrics)&&cJSON_IsNumber(cJSON_GetObjectItemCaseSensitive(metrics,"sampleSequence"))) {
            uint64_t incoming=(uint64_t)seq->valuedouble;
            if(incoming>device_metrics_sequence[link]) {
                cJSON_Delete(device_metrics[link]);device_metrics[link]=cJSON_DetachItemViaPointer(root,metrics);
                device_metrics_sequence[link]=incoming;
            }
            if(incoming==device_metrics_sequence[link]){if(sequence)*sequence=incoming;ok=true;}
        }
        // Metrics never renew task/session freshness or resurrect a dead link.
        xSemaphoreGive(device_mutex);cJSON_Delete(root);return ok;
    }
    if(type&&!strcmp(type,"tab5_resources")&&id&&session&&cJSON_IsNumber(version)&&version->valueint==1&&
       cJSON_IsNumber(seq)&&seq->valuedouble>0&&seq->valuedouble<=9007199254740991.0&&seq->valuedouble==(uint64_t)seq->valuedouble) {
        cJSON *resources=cJSON_GetObjectItemCaseSensitive(root,"resources");
        int count=cJSON_GetArraySize(resources);
        xSemaphoreTake(device_mutex,portMAX_DELAY);if(host_generation!=device_host_epoch()){xSemaphoreGive(device_mutex);cJSON_Delete(root);*reason=DEVICE_REJECT_IDENTITY;return false;}
        if(config.paired&&!strcmp(config.id,id)&&device_snapshots[link]&&!strcmp(device_links.session[link],session)&&
           cJSON_IsArray(resources)&&count>0&&count<=8) {
            ok=true;
            for(int i=0;i<count;i++)if(!assets_receive(cJSON_GetArrayItem(resources,i)))ok=false;
            if(ok&&sequence)*sequence=(uint64_t)seq->valuedouble;
        }
        // Image fragments do not extend task/session freshness.
        xSemaphoreGive(device_mutex);cJSON_Delete(root);return ok;
    }
    if(type&&!strcmp(type,"tab5_delta")) {
        cJSON *base=cJSON_GetObjectItemCaseSensitive(root,"baseSequence");
        if(id&&session&&cJSON_IsNumber(version)&&version->valuedouble==1&&cJSON_IsNumber(seq)&&
           seq->valuedouble>0&&seq->valuedouble<=9007199254740991.0&&seq->valuedouble==(uint64_t)seq->valuedouble&&
           cJSON_IsNumber(base)&&base->valuedouble>0&&base->valuedouble<seq->valuedouble&&base->valuedouble==(uint64_t)base->valuedouble) {
            xSemaphoreTake(device_mutex,portMAX_DELAY);if(host_generation!=device_host_epoch()){xSemaphoreGive(device_mutex);cJSON_Delete(root);*reason=DEVICE_REJECT_IDENTITY;return false;}
            if(config.paired&&!strcmp(config.id,id)&&device_snapshots[link]&&!strcmp(device_links.session[link],session)&&device_links.received_ms[link]>=0) {
                if(device_links.sequence[link]==(uint64_t)seq->valuedouble){ok=true;if(sequence)*sequence=(uint64_t)seq->valuedouble;}
                else if(device_links.sequence[link]==(uint64_t)base->valuedouble) {
                    cJSON *next=telemetry_delta_apply(device_snapshots[link],cJSON_GetObjectItemCaseSensitive(root,"changes"));
                    if(next&&link_state_receive(&device_links,link,session,(uint64_t)seq->valuedouble,esp_timer_get_time()/1000)) {
                        cJSON_Delete(device_snapshots[link]);device_snapshots[link]=next;next=NULL;
                        assets_receive(cJSON_GetObjectItemCaseSensitive(device_snapshots[link],"resource"));
                        if(sequence)*sequence=(uint64_t)seq->valuedouble;
                        ok=true;
                    }
                    cJSON_Delete(next);
                }
            }
            xSemaphoreGive(device_mutex);
        }
        // Valid USB state deltas renew the same host lease as full status.
        // Resource/metrics packets deliberately retain their existing behavior.
        if(ok&&link==LINK_USB)device_host_usb_touch();
        cJSON_Delete(root);return ok;
    }
    if(type&&strcmp(type,"tab5_status")==0&&id&&cJSON_IsNumber(version)&&version->valueint==1&&cJSON_IsNumber(seq)&&seq->valuedouble>0&&seq->valuedouble<=9007199254740991.0&&cJSON_IsObject(data)&&cJSON_IsObject(cJSON_GetObjectItemCaseSensitive(data,"codex"))&&cJSON_IsObject(cJSON_GetObjectItemCaseSensitive(data,"claude"))) {
        xSemaphoreTake(device_mutex,portMAX_DELAY);if(host_generation!=device_host_epoch()){xSemaphoreGive(device_mutex);cJSON_Delete(root);*reason=DEVICE_REJECT_IDENTITY;return false;}
        const char *host_id=str(cJSON_GetObjectItemCaseSensitive(data,"connectionHealth"),"hostId");
        bool bound=trusted_hosts_readable&&trusted_hosts.count&&
            (!host_id||!strcmp(host_id,trusted_hosts.entries[trusted_hosts.selected].id));
        if(!bound){xSemaphoreGive(device_mutex);cJSON_Delete(root);return false;}
        bool same_session=session&&!strcmp(device_links.session[link],session);
        *reason=!config.paired||strcmp(config.id,id)?DEVICE_REJECT_IDENTITY:DEVICE_REJECT_SEQUENCE;
        if(config.paired&&strcmp(config.id,id)==0&&link_state_receive(&device_links,link,session,(uint64_t)seq->valuedouble,esp_timer_get_time()/1000)) {
            if(!same_session){cJSON_Delete(device_metrics[link]);device_metrics[link]=NULL;device_metrics_sequence[link]=0;}
            assets_receive(cJSON_GetObjectItemCaseSensitive(data,"resource"));
            cJSON_Delete(device_snapshots[link]);device_snapshots[link]=cJSON_DetachItemViaPointer(root,data);
            if(sequence) *sequence=(uint64_t)seq->valuedouble;
            ok=true;
        } else if(config.paired&&strcmp(config.id,id)==0&&session&&link>=0&&link<3&&
            strcmp(device_links.session[link],session)==0&&device_links.sequence[link]==(uint64_t)seq->valuedouble) {
            // Retransmission is acknowledged but cannot extend data freshness.
            if(sequence) *sequence=(uint64_t)seq->valuedouble;
            ok=true;
        }
        xSemaphoreGive(device_mutex);
    }
    cJSON_Delete(root);
    if(ok&&link==LINK_USB)device_host_usb_touch();
    if(ok)ota_update_consider(json);
    return ok;
}
bool device_receive_checked(const char *json,link_kind link,uint64_t *sequence,device_reject_reason *reason) {
    device_reject_reason result;bool ok=receive_frame(json,link,sequence,true,&result,device_host_epoch());
    if(reason)*reason=ok?DEVICE_REJECT_NONE:result;
    return ok;
}
bool device_receive(const char *json,link_kind link,uint64_t *sequence) {return device_receive_checked(json,link,sequence,NULL);}
bool device_receive_for_host(const char *json,link_kind link,uint64_t *sequence,unsigned host_generation){device_reject_reason reason;return receive_frame(json,link,sequence,true,&reason,host_generation); }
// Configuration is accepted only by the USB task, never over radio.
bool device_command(cJSON *root,const char **reply) {
    const char *type=str(root,"type");if(!type)return false;
    cJSON *ver=cJSON_GetObjectItemCaseSensitive(root,"version");if(!cJSON_IsNumber(ver)||ver->valueint!=1)return false;
    xSemaphoreTake(device_mutex,portMAX_DELAY);bool ok=false;
    device_config previous=config;
    wifi_profile_store previous_networks=networks;
    bool wifi_changed=false;
    const char *id=str(root,"deviceId");
    if(strcmp(type,"tab5_resource")==0) {
        // Resource fragments live in RAM; never write NVS for a transfer.
        bool accepted=config.paired&&id&&!strcmp(id,config.id)&&assets_receive(cJSON_GetObjectItemCaseSensitive(root,"resource"));
        xSemaphoreGive(device_mutex);
        if(accepted)*reply="tab5_resource_ack";
        return accepted;
    }
    if(strcmp(type,"tab5_pair")==0&&id&&strcmp(id,config.id)==0) {
        const char *key=str(root,"key");uint8_t decoded[33];size_t n=0;
        if(key&&mbedtls_base64_decode(decoded,sizeof(decoded),&n,(const uint8_t*)key,strlen(key))==0&&n==32) {
            trusted_host incoming={0};host_key_id(decoded,incoming.id);memcpy(incoming.key,decoded,32);
            const char *claimed=str(root,"hostId"),*name=str(root,"name");
            if((!claimed||!strcmp(claimed,incoming.id))&&(!name||trusted_host_name_valid(name))&&trusted_hosts_readable) {
                snprintf(incoming.name,sizeof(incoming.name),"%s",name?name:"已配对电脑");
                const char *address=str(root,"host");cJSON *port=cJSON_GetObjectItemCaseSensitive(root,"port");
                if(address&&private_host(address)&&strlen(address)<16&&cJSON_IsNumber(port)&&port->valuedouble==port->valueint&&port->valueint>0&&port->valueint<=65535){strcpy(incoming.address,address);incoming.port=port->valueint;}
                trusted_host_store next=trusted_hosts;
                ok=trusted_hosts_put(&next,&incoming)&&host_save(&next);
                if(ok){trusted_hosts=next;if(!config.paired)memcpy(config.key,decoded,32);config.paired=true;*reply="tab5_paired";}
            }
            memset(decoded,0,sizeof(decoded));
        }
    } else if(strcmp(type,"tab5_wifi")==0&&config.paired&&id&&strcmp(id,config.id)==0) {
        const char *ssid=str(root,"ssid"),*password=str(root,"password");
        const char *label=str(root,"label");
        if(wifi_profiles_put(&networks,ssid,password)&&(!label||wifi_profiles_set_label(&networks,ssid,label))) {
            strcpy(config.ssid,ssid);strcpy(config.password,password);*reply="tab5_wifi_saved";ok=true;wifi_changed=true;
        }
    } else if(strcmp(type,"tab5_wifi_label")==0&&config.paired&&id&&strcmp(id,config.id)==0) {
        if(wifi_profiles_set_label(&networks,str(root,"ssid"),str(root,"label"))){*reply="tab5_wifi_label_saved";ok=true;wifi_changed=true;}
    }
    // Unbound legacy tab5_host cannot redirect a selected computer.
    if(ok&&wifi_changed&&nvs_set_blob(settings,"wifi_profiles3",&networks,sizeof(networks))!=ESP_OK)ok=false;
    if(ok&&!save())ok=false;
    if(!ok){config=previous;networks=previous_networks;}
    host_recover_legacy_pairing();
    xSemaphoreGive(device_mutex);
    return ok;
}
static void hex_encode(const uint8_t *bytes,size_t size,char *output) {
    static const char digits[]="0123456789abcdef";
    for(size_t i=0;i<size;i++){output[i*2]=digits[bytes[i]>>4];output[i*2+1]=digits[bytes[i]&15];}
    output[size*2]=0;
}
void device_nonce(char output[33]) {uint8_t bytes[16];esp_fill_random(bytes,16);hex_encode(bytes,sizeof(bytes),output);}
void device_proof(const char *message,char output[65]) {
    device_config cfg=device_get_config();uint8_t hash[32];
    mbedtls_md_hmac(mbedtls_md_info_from_type(MBEDTLS_MD_SHA256),cfg.key,32,(const uint8_t*)message,strlen(message),hash);
    hex_encode(hash,sizeof(hash),output);
}
char *device_decrypt(const uint8_t *packet,size_t size,const char *challenge) {
    return device_decrypt_bounded(packet,size,challenge,TAB5_MAX_FRAME);
}
char *device_decrypt_bounded(const uint8_t *packet,size_t size,const char *challenge,size_t maximum) {
    int error;return device_decrypt_checked(packet,size,challenge,maximum,&error);
}
char *device_decrypt_checked(const uint8_t *packet,size_t size,const char *challenge,size_t maximum,int *error) {
    *error=-1001;if(!packet||!challenge||maximum>TAB5_MAX_FRAME||size<28||size>maximum||strlen(challenge)!=32)return NULL;
    device_config cfg=device_get_config();*error=-1002;if(!cfg.paired)return NULL;
    // Bulk cleartext belongs in PSRAM; keep internal RAM available for CDC,
    // cryptographic control structures and concurrent small status messages.
    *error=-1003;char *clear=heap_caps_calloc(1,size-28+1,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);if(!clear)return NULL;
    mbedtls_gcm_context ctx;mbedtls_gcm_init(&ctx);
    int ret=mbedtls_gcm_setkey(&ctx,MBEDTLS_CIPHER_ID_AES,cfg.key,256);
    if(!ret)ret=mbedtls_gcm_auth_decrypt(&ctx,size-28,packet,12,(const uint8_t*)challenge,32,packet+12,16,packet+28,(uint8_t*)clear);
    mbedtls_gcm_free(&ctx);*error=ret;if(ret){free(clear);return NULL;}return clear;
}
