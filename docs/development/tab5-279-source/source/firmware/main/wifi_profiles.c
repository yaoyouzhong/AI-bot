#include "wifi_profiles.h"
#include <string.h>
bool wifi_profiles_valid(const wifi_profile_store *s) {
    if(s->version!=WIFI_PROFILE_VERSION||s->count>WIFI_PROFILE_LIMIT||(s->count&&s->preferred>=s->count))return false;
    for(unsigned i=0;i<s->count;i++) {
        if(!memchr(s->entries[i].ssid,0,33)||!s->entries[i].ssid[0]||!memchr(s->entries[i].password,0,64)||!memchr(s->entries[i].label,0,WIFI_LABEL_SIZE))return false;
        for(unsigned j=0;j<i;j++)if(!strcmp(s->entries[i].ssid,s->entries[j].ssid))return false;
    }
    return true;
}
bool wifi_profiles_migrate(wifi_profile_store *s,const wifi_profile_store_v1 *old) {
    if(old->version!=1||old->count>WIFI_PROFILE_LEGACY_LIMIT||(old->count&&old->preferred>=old->count))return false;
    wifi_profile_store next={.version=WIFI_PROFILE_VERSION};
    for(unsigned i=0;i<old->count;i++) {
        if(!memchr(old->entries[i].ssid,0,33)||!memchr(old->entries[i].password,0,64)||
           wifi_profiles_index(&next,old->entries[i].ssid)>=0||!wifi_profiles_put(&next,old->entries[i].ssid,old->entries[i].password))return false;
    }
    next.preferred=old->count?old->preferred:0;*s=next;return true;
}
bool wifi_profiles_migrate_v2(wifi_profile_store *s,const wifi_profile_store_v2 *old) {
    if(old->version!=2||old->count>WIFI_PROFILE_LEGACY_LIMIT||(old->count&&old->preferred>=old->count))return false;
    wifi_profile_store next={.version=WIFI_PROFILE_VERSION,.count=old->count,.preferred=old->count?old->preferred:0};
    memcpy(next.entries,old->entries,sizeof(old->entries));
    if(!wifi_profiles_valid(&next))return false;
    *s=next;return true;
}
bool wifi_profiles_set_label(wifi_profile_store *s,const char *ssid,const char *label) {
    if(!ssid||!label||strlen(label)>=WIFI_LABEL_SIZE||!wifi_profiles_valid(s))return false;
    for(const unsigned char *p=(const unsigned char *)label;*p;p++)if(*p<32||*p==127)return false;
    int at=wifi_profiles_index(s,ssid);if(at<0)return false;
    memset(s->entries[at].label,0,WIFI_LABEL_SIZE);strcpy(s->entries[at].label,label);return true;
}
int wifi_profiles_index(const wifi_profile_store *s,const char *ssid) {
    for(unsigned i=0;i<s->count;i++)if(!strcmp(s->entries[i].ssid,ssid))return (int)i;
    return -1;
}
bool wifi_profiles_put(wifi_profile_store *s,const char *ssid,const char *password) {
    if(!ssid||!*ssid||strlen(ssid)>32||!password||strlen(password)>63||!wifi_profiles_valid(s))return false;
    int i=wifi_profiles_index(s,ssid);
    if(i<0){if(s->count==WIFI_PROFILE_LIMIT)return false;i=(int)s->count++;}
    strcpy(s->entries[i].ssid,ssid);strcpy(s->entries[i].password,password);s->preferred=(unsigned)i;
    return true;
}
bool wifi_profiles_forget(wifi_profile_store *s,const char *ssid) {
    if(!ssid||!wifi_profiles_valid(s))return false;
    int at=wifi_profiles_index(s,ssid);if(at<0)return false;
    memmove(&s->entries[at],&s->entries[at+1],(s->count-(unsigned)at-1)*sizeof(s->entries[0]));
    memset(&s->entries[--s->count],0,sizeof(s->entries[0]));
    if(!s->count)s->preferred=0;
    else if(s->preferred>(unsigned)at)s->preferred--;
    else if(s->preferred>=s->count)s->preferred=s->count-1;
    return true;
}
void wifi_profiles_sync_active(const wifi_profile_store *s,char ssid[33],char password[64]) {
    int at=wifi_profiles_index(s,ssid);
    if(at<0&&s->count)at=(int)s->preferred;
    memset(ssid,0,33);memset(password,0,64);
    if(at>=0){strcpy(ssid,s->entries[at].ssid);strcpy(password,s->entries[at].password);}
}
