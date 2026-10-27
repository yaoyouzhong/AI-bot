#include "host_ble_peers.h"
static host_ble_peers host_peers={.version=1};
static bool host_peers_readable=true;
static void host_ble_init(void) {
    host_ble_peers saved;size_t size=sizeof(saved);
    int r=nvs_get_blob(settings,"host_ble_v1",&saved,&size);
    if(r==ESP_OK&&size==sizeof(saved)&&host_ble_peers_valid(&saved))host_peers=saved;
    else if(r!=ESP_ERR_NVS_NOT_FOUND)host_peers_readable=false;
}
// -1 unavailable/corrupt; 0 selected computer not yet learned; 1 known peer.
int device_host_ble_target(uint8_t *type,uint8_t address[6]) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);int result=-1;
    if(!device_host_changing()&&host_peers_readable&&trusted_hosts_readable&&trusted_hosts.count) {
        int at=host_ble_peer_index(&host_peers,trusted_hosts.entries[trusted_hosts.selected].id);result=at<0?0:1;
        if(at>=0){*type=host_peers.entries[at].type;memcpy(address,host_peers.entries[at].address,6);}
    }
    xSemaphoreGive(device_mutex);return result;
}
static bool host_ble_possible_locked(uint8_t type,const uint8_t address[6]) {
    if(device_host_changing()||!host_peers_readable||!trusted_hosts_readable||!trusted_hosts.count||!host_ble_address_valid(type,address))return false;
    // An unresolved RPA must prove the selected application key; never persist it.
    if(!host_ble_address_stable(type,address))return true;
    const char *selected=trusted_hosts.entries[trusted_hosts.selected].id;
    int at=host_ble_peer_index(&host_peers,selected);
    if(at>=0)return host_peers.entries[at].type==type&&!memcmp(host_peers.entries[at].address,address,6);
    for(unsigned i=0;i<host_peers.count;i++)if(host_peers.entries[i].type==type&&!memcmp(host_peers.entries[i].address,address,6))return false;
    return true;
}
bool device_host_ble_possible(uint8_t type,const uint8_t address[6]) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);bool ok=host_ble_possible_locked(type,address);xSemaphoreGive(device_mutex);return ok;
}
// Only a valid encrypted frame on this BLE connection may learn a peer.
bool device_host_ble_bind(unsigned epoch,uint8_t type,const uint8_t address[6]) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);bool ok=epoch==device_host_epoch()&&host_ble_possible_locked(type,address);
    if(ok&&host_ble_address_stable(type,address)) {
        const char *id=trusted_hosts.entries[trusted_hosts.selected].id;
        if(host_ble_peer_index(&host_peers,id)<0) {
            host_ble_peers next=host_peers;
            if(next.count>=TRUSTED_HOST_LIMIT)ok=false;
            else {host_ble_peer *p=&next.entries[next.count++];strcpy(p->id,id);p->type=type;memcpy(p->address,address,6);
                ok=host_ble_peers_valid(&next)&&nvs_set_blob(settings,"host_ble_v1",&next,sizeof(next))==ESP_OK&&nvs_commit(settings)==ESP_OK;
                if(ok)host_peers=next;}
        }
    }
    xSemaphoreGive(device_mutex);return ok;
}
