#pragma once
#include "trusted_hosts.h"
// Separate from trusted_hosts1: retain its ABI and all existing pairings.
typedef struct {char id[33];uint8_t type,address[6];} host_ble_peer;
typedef struct {uint32_t version,count;host_ble_peer entries[TRUSTED_HOST_LIMIT];} host_ble_peers;
static inline bool host_ble_address_valid(uint8_t type,const uint8_t address[6]) {
    unsigned any=0,not_ff=0;for(int i=0;i<6;i++){any|=address[i];not_ff|=address[i]^255;}
    return type<=1&&any&&not_ff;
}
static inline bool host_ble_address_stable(uint8_t type,const uint8_t address[6]) {
    return host_ble_address_valid(type,address)&&(type==0||(address[5]&0xc0)==0xc0);
}
static inline bool host_ble_peers_valid(const host_ble_peers *s) {
    if(s->version!=1||s->count>TRUSTED_HOST_LIMIT)return false;
    for(unsigned i=0;i<s->count;i++) {
        const host_ble_peer *p=&s->entries[i];
        if(!memchr(p->id,0,sizeof(p->id))||!trusted_host_id_valid(p->id)||!host_ble_address_stable(p->type,p->address))return false;
        for(unsigned j=0;j<i;j++)if(!strcmp(p->id,s->entries[j].id)||
            (p->type==s->entries[j].type&&!memcmp(p->address,s->entries[j].address,6)))return false;
    }
    return true;
}
static inline int host_ble_peer_index(const host_ble_peers *s,const char *id) {
    for(unsigned i=0;i<s->count;i++)if(!strcmp(s->entries[i].id,id))return (int)i;
    return -1;
}
