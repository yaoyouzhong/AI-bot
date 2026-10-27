#pragma once
#include <stdbool.h>
#include <stdint.h>
#include <string.h>
#define TRUSTED_HOST_LIMIT 8
#define TRUSTED_HOST_NAME 64
typedef struct { char id[33],name[TRUSTED_HOST_NAME]; uint8_t key[32]; char address[16]; int32_t port; } trusted_host;
typedef struct { uint32_t version,count,selected; trusted_host entries[TRUSTED_HOST_LIMIT]; } trusted_host_store;
static inline bool trusted_host_public_command(const char *type) {
    return type&&(!strcmp(type,"tab5_ping")||!strcmp(type,"tab5_pair")||!strcmp(type,"tab5_host_claim")||
        !strcmp(type,"tab5_ota_status")||!strcmp(type,"tab5_orientation_status")||!strcmp(type,"tab5_crash_status")||
        !strcmp(type,"tab5_hid_status")||!strcmp(type,"tab5_wifi_list"));
}
static inline bool trusted_host_id_valid(const char *id) {
    if(!id||strlen(id)!=32)return false;
    for(unsigned i=0;i<32;i++)if(!((id[i]>='0'&&id[i]<='9')||(id[i]>='a'&&id[i]<='f')))return false;
    return true;
}
static inline bool trusted_host_name_valid(const char *name) {
    if(!name||!name[0]||strlen(name)>=TRUSTED_HOST_NAME)return false;
    for(const unsigned char *p=(const unsigned char *)name;*p;p++)if(*p<32||*p==127||*p=='|')return false;
    return true;
}
static inline int trusted_host_index(const trusted_host_store *s,const char *id) {
    if(!trusted_host_id_valid(id))return -1;
    for(unsigned i=0;i<s->count;i++)if(!strcmp(s->entries[i].id,id))return (int)i;
    return -1;
}
static inline bool trusted_hosts_valid(const trusted_host_store *s) {
    if(!s||s->version!=1||s->count>TRUSTED_HOST_LIMIT||(s->count?s->selected>=s->count:s->selected!=0))return false;
    for(unsigned i=0;i<s->count;i++) {
        const trusted_host *h=&s->entries[i];
        if(!memchr(h->id,0,sizeof(h->id))||!memchr(h->name,0,sizeof(h->name))||!trusted_host_id_valid(h->id)||!trusted_host_name_valid(h->name)||
           !memchr(h->address,0,sizeof(h->address))||h->port<0||h->port>65535)return false;
        for(unsigned j=0;j<i;j++)if(!strcmp(s->entries[j].id,h->id))return false;
    }
    return true;
}
// No eviction: enrolling another computer must never erase an existing trust.
static inline bool trusted_hosts_put(trusted_host_store *s,const trusted_host *h) {
    if(!trusted_hosts_valid(s)||!trusted_host_id_valid(h->id)||!trusted_host_name_valid(h->name))return false;
    int at=trusted_host_index(s,h->id);
    if(at<0){if(s->count==TRUSTED_HOST_LIMIT)return false;at=(int)s->count++;}
    s->entries[at]=*h;return true;
}
