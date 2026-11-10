#pragma once
#include "trusted_hosts.h"
typedef struct {char id[33],name[TRUSTED_HOST_NAME];} host_label;
typedef struct {uint32_t version,count;host_label entries[TRUSTED_HOST_LIMIT];} host_label_store;
static inline int host_label_index(const host_label_store *s,const char *id) {
    for(unsigned i=0;i<s->count;i++)if(!strcmp(s->entries[i].id,id))return (int)i;
    return -1;
}
static inline bool host_labels_valid(const host_label_store *s) {
    if(s->version!=1||s->count>TRUSTED_HOST_LIMIT)return false;
    for(unsigned i=0;i<s->count;i++) {
        const host_label *h=&s->entries[i];
        if(!memchr(h->id,0,sizeof(h->id))||!memchr(h->name,0,sizeof(h->name))||
           !trusted_host_id_valid(h->id)||!trusted_host_name_valid(h->name))return false;
        for(unsigned j=0;j<i;j++)if(!strcmp(s->entries[j].id,h->id))return false;
    }
    return true;
}
