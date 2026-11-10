#pragma once
#include <stdbool.h>
#include <stdint.h>
#define WIFI_PROFILE_LIMIT 10
#define WIFI_PROFILE_LEGACY_LIMIT 5
#define WIFI_PROFILE_VERSION 3
#define WIFI_LABEL_SIZE 49
typedef struct {char ssid[33],password[64];} wifi_profile_v1;
typedef struct {uint32_t version,count,preferred;wifi_profile_v1 entries[WIFI_PROFILE_LEGACY_LIMIT];} wifi_profile_store_v1;
typedef struct {char ssid[33],password[64],label[WIFI_LABEL_SIZE];} wifi_profile;
typedef struct {uint32_t version,count,preferred;wifi_profile entries[WIFI_PROFILE_LEGACY_LIMIT];} wifi_profile_store_v2;
typedef struct {uint32_t version,count,preferred;wifi_profile entries[WIFI_PROFILE_LIMIT];} wifi_profile_store;
bool wifi_profiles_valid(const wifi_profile_store *store);
bool wifi_profiles_migrate(wifi_profile_store *store,const wifi_profile_store_v1 *legacy);
bool wifi_profiles_migrate_v2(wifi_profile_store *store,const wifi_profile_store_v2 *legacy);
bool wifi_profiles_set_label(wifi_profile_store *store,const char *ssid,const char *label);
// Returns false instead of silently evicting a saved network when full.
bool wifi_profiles_put(wifi_profile_store *store,const char *ssid,const char *password);
bool wifi_profiles_forget(wifi_profile_store *store,const char *ssid);
// Resolve stale legacy/active credentials against the authoritative saved list.
void wifi_profiles_sync_active(const wifi_profile_store *store,char ssid[33],char password[64]);
int wifi_profiles_index(const wifi_profile_store *store,const char *ssid);
int device_wifi_names(char names[WIFI_PROFILE_LIMIT][33]);
bool device_wifi_select(int index,bool persist);
bool device_wifi_forget(const char *ssid);
void device_wifi_label(const char *ssid,char label[WIFI_LABEL_SIZE]);
int device_wifi_count(void);
