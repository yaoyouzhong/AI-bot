#pragma once
#include "link_state.h"
#include "cJSON.h"
#include "device_receive_reason.h"
#include "trusted_hosts.h"
#include "freertos/FreeRTOS.h"
#include "freertos/semphr.h"
#define TAB5_MAX_FRAME 65536
#define TAB5_LEGACY_FRAME 32768
typedef struct { char id[13]; uint8_t key[32]; bool paired; char host[16]; int port; char ssid[33]; char password[64]; int mode; int brightness; } device_config;
extern SemaphoreHandle_t device_mutex;
extern link_state device_links;
extern cJSON *device_snapshots[3];
extern cJSON *device_metrics[3];
extern uint64_t device_metrics_sequence[3];
void device_init(void);
unsigned device_host_epoch(void);
bool device_host_changing(void);
int device_host_ble_target(uint8_t *type,uint8_t address[6]);
bool device_host_ble_possible(uint8_t type,const uint8_t address[6]);
bool device_host_ble_bind(unsigned epoch,uint8_t type,const uint8_t address[6]);
bool ble_host_authorized(void);
bool device_host_usb_owned(void);
void device_host_usb_touch(void);
int device_host_names(char names[TRUSTED_HOST_LIMIT][TRUSTED_HOST_NAME],int *selected);
void device_host_identity(char id[33],char name[TRUSTED_HOST_NAME]);
void device_host_status(char id[33],char name[TRUSTED_HOST_NAME],link_kind *link);
bool device_host_label(int index,char id[33],char name[TRUSTED_HOST_NAME]);
bool device_host_rename(const char *id,const char *name);
bool device_host_select(int index);
void device_host_challenge(char nonce[33]);
cJSON *device_host_claim(cJSON *root);
device_config device_get_config(void);
bool device_receive(const char *json, link_kind link, uint64_t *sequence);
bool device_receive_for_host(const char *json,link_kind link,uint64_t *sequence,unsigned host_generation);
bool device_receive_checked(const char *json,link_kind link,uint64_t *sequence,device_reject_reason *reason);
char *device_unpack_json(cJSON *root,unsigned maximum);
bool device_command(cJSON *root, const char **reply);
cJSON *device_wifi_public_list(void);
bool tab5_ui_wifi_forget(const char *ssid);
void device_set_mode(int mode);
void device_set_brightness(int brightness);
bool device_save_brightness(int brightness);
typedef struct { int volume; bool muted; } device_sound;
device_sound device_get_sound(void);
bool device_set_sound(int volume, bool muted);
int device_sound_volume(void);
void device_nonce(char output[33]);
void device_proof(const char *message, char output[65]);
char *device_decrypt(const uint8_t *packet, size_t size, const char *challenge);
char *device_decrypt_bounded(const uint8_t *packet,size_t size,const char *challenge,size_t maximum);
char *device_decrypt_checked(const uint8_t *packet,size_t size,const char *challenge,size_t maximum,int *error);
bool tab5_usb_connected(void);
void usb_start(void);
void tab5_wifi_start(void);
void ble_start(void);
void ble_probe_capture(bool active);
void ble_probe_diagnostics(char *out,size_t size);
void ui_start(void);
extern volatile bool wifi_connected;
extern volatile bool ble_available;
extern volatile bool ble_connected;
extern volatile int ble_disconnect_reason;
extern volatile unsigned ble_connect_count;
