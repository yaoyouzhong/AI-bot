#include "device.h"
#include "assets.h"
#include "ota_update.h"
#include "voice_remote.h"
#include "camera_preview.h"
#include "camera_viewport.h"
#include "wifi_profiles.h"
#include "wifi_status.h"
#include "wifi_performance.h"
#include "wifi_probe.h"
#include "esp_app_desc.h"
#include <stdatomic.h>
#include "lwip/sockets.h"
#include "lwip/inet.h"
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include "esp_event.h"
#include "esp_netif.h"
#include "esp_wifi.h"
#include "esp_http_client.h"
#include "esp_log.h"
#include "esp_heap_caps.h"
#include "esp_timer.h"
#include "tinyusb.h"
extern volatile int tab5_voice_http_stage,tab5_voice_http_status,tab5_voice_request_ms,tab5_voice_last_seq;
extern volatile unsigned tab5_voice_capture_frames,tab5_voice_queue_peak;
extern volatile int64_t tab5_voice_capture_start_ms;
volatile bool wifi_connected;
volatile bool tab5_wifi_initialized;
static atomic_int disconnect_reason,http_status;
static atomic_uint status_attempts,status_valid,status_host,status_local,status_port;
static atomic_int status_open_error,status_socket_error;
static atomic_bool reconnect_requested;
static TaskHandle_t wifi_task;
void tab5_wifi_wake(void){if(wifi_task)xTaskNotifyGive(wifi_task);}
static char local_ip[16];
static int64_t last_valid_status;
#include "wifi_connection_diagnostics.inc"
#include "wifi_path_diagnostics.inc"
void tab5_wifi_http_diagnostics(char *out,size_t size) {
    snprintf(out,size,"1,%u,%d,%d,%d,%u,%u,%u,%u",atomic_load(&status_attempts),
        atomic_load(&status_open_error),atomic_load(&status_socket_error),atomic_load(&http_status),
        atomic_load(&status_valid),atomic_load(&status_host),atomic_load(&status_port),atomic_load(&status_local));
}
void tab5_wifi_reconnect(void){atomic_store(&reconnect_requested,true);}
void tab5_wifi_diagnostics(char *out,size_t size) {
    device_config cfg=device_get_config();char ip[16];int64_t received;
    // Host selection invalidates these authenticated timestamps immediately;
    // the previous host's HTTP success must not label the new target ready.
    xSemaphoreTake(device_mutex,portMAX_DELAY);strcpy(ip,local_ip);received=device_links.received_ms[LINK_WIFI];xSemaphoreGive(device_mutex);
    int reason=atomic_load(&disconnect_reason),http=atomic_load(&http_status);
    const char *state=!cfg.ssid[0]?"请在电脑添加 Wi-Fi":wifi_connected?"已连接":reason==202||reason==15?"认证失败，请检查密码":reason==201?"未找到已保存网络":"正在连接已保存网络";
    const char *bridge=!cfg.paired?"请先通过 USB 配对":!wifi_connected?"等待 Wi-Fi":
        received>=0&&esp_timer_get_time()/1000>=received&&esp_timer_get_time()/1000-received<8000?"正常 · 鉴权通过":
        http==401||http==403?"配对不匹配，请重新配对":http==0?"暂不可达，正在寻找电脑":"响应异常，正在重试";
    char label[WIFI_LABEL_SIZE];device_wifi_label(cfg.ssid,label);
    snprintf(out,size,"Wi-Fi  %s%s%s\n%s\nIP  %s\n电脑桥接  %s",label[0]?label:"",label[0]?" · ":"",cfg.ssid[0]?cfg.ssid:"尚未配置",state,ip[0]?ip:"尚未分配",bridge);
}
static void event(void *arg,esp_event_base_t base,int32_t id,void *data) {
    if(base==WIFI_EVENT&&id==WIFI_EVENT_STA_CONNECTED) {
        atomic_store(&disconnect_reason,0);wifi_connection_phase(2);
    }
    if(base==IP_EVENT&&id==IP_EVENT_STA_GOT_IP) {
        ip_event_got_ip_t *got=data;
        xSemaphoreTake(device_mutex,portMAX_DELAY);snprintf(local_ip,sizeof(local_ip),IPSTR,IP2STR(&got->ip_info.ip));xSemaphoreGive(device_mutex);
        atomic_store(&status_local,ntohl(got->ip_info.ip.addr));
        atomic_store(&disconnect_reason,0);wifi_connection_phase(0);wifi_connected=true;
    }
    if(base==WIFI_EVENT&&id==WIFI_EVENT_STA_DISCONNECTED) {
        wifi_connected=false;wifi_connection_phase(0);atomic_store(&disconnect_reason,((wifi_event_sta_disconnected_t *)data)->reason);
        xSemaphoreTake(device_mutex,portMAX_DELAY);local_ip[0]=0;last_valid_status=0;xSemaphoreGive(device_mutex);
    }
    if(base==WIFI_EVENT&&id==WIFI_EVENT_STA_STOP) {
        wifi_connected=false;wifi_connection_phase(0);
        xSemaphoreTake(device_mutex,portMAX_DELAY);local_ip[0]=0;last_valid_status=0;xSemaphoreGive(device_mutex);
    }
}
static void discover_bridge(void) {
    unsigned owner=device_host_epoch();device_config cfg=device_get_config();
    if(!cfg.paired||owner!=device_host_epoch()||device_host_changing())return;
    int sock=socket(AF_INET,SOCK_DGRAM,IPPROTO_UDP);if(sock<0)return;
    int yes=1;struct timeval timeout={.tv_sec=0,.tv_usec=300000};
    setsockopt(sock,SOL_SOCKET,SO_BROADCAST,&yes,sizeof(yes));
    setsockopt(sock,SOL_SOCKET,SO_RCVTIMEO,&timeout,sizeof(timeout));
    char nonce[33],proof[65],payload[180],packet[256];device_nonce(nonce);
    snprintf(payload,sizeof(payload),"TAB5_DISCOVER_V1|%s|%s",cfg.id,nonce);device_proof(payload,proof);
    snprintf(packet,sizeof(packet),"%s|%s",payload,proof);
    struct sockaddr_in target={.sin_family=AF_INET,.sin_port=htons(18766),.sin_addr.s_addr=INADDR_BROADCAST};
    sendto(sock,packet,strlen(packet),0,(struct sockaddr *)&target,sizeof(target));
    struct sockaddr_in source;socklen_t source_size=sizeof(source);
    int n=recvfrom(sock,packet,sizeof(packet)-1,0,(struct sockaddr *)&source,&source_size);
    if(n>0) {
        packet[n]=0;char id[13],reply_nonce[33],host[16],mac[65],tail;unsigned port;
        if(sscanf(packet,"TAB5_BRIDGE_V1|%12[0-9a-f]|%32[0-9a-f]|%15[0-9.]|%u|%64[0-9a-f]%c",id,reply_nonce,host,&port,mac,&tail)==5&&
           !strcmp(id,cfg.id)&&!strcmp(reply_nonce,nonce)&&strlen(mac)==64&&port>0&&port<65536&&inet_addr(host)==source.sin_addr.s_addr) {
            char *separator=strrchr(packet,'|');*separator=0;device_proof(packet,proof);
            unsigned difference=0;for(int i=0;i<64;i++)difference|=(unsigned char)proof[i]^(unsigned char)mac[i];
            if(!difference)device_discovered_host(host,(int)port,owner);
        }
    }
    close(sock);
}
static void poll_status(void) {
    unsigned owner=device_host_epoch();
    device_config current=device_get_config();device_config *cfg=&current;
    if(owner!=device_host_epoch()||device_host_changing())return;
    if(!cfg->paired||!cfg->host[0]||cfg->port<=0)return;
    char url[100],nonce[33],proof[65],message[100],ids[27],asset_proof[65],diag[256],camera_diag[128];device_nonce(nonce);
    snprintf(message,sizeof(message),"GET|%.12s|%s",cfg->id,nonce);device_proof(message,proof);
    xSemaphoreTake(device_mutex,portMAX_DELAY);assets_ids(ids);xSemaphoreGive(device_mutex);
    snprintf(message,sizeof(message),"ASSETS|%s|%s",nonce,ids);device_proof(message,asset_proof);
    snprintf(url,sizeof(url),"http://%.15s:%d/tab5/v1/status",cfg->host,cfg->port);
    esp_http_client_config_t options={.url=url,.timeout_ms=3000,.disable_auto_redirect=true,.buffer_size=2048};
    esp_http_client_handle_t client=esp_http_client_init(&options);if(!client)return;
    atomic_store(&http_status,0);
    esp_http_client_set_header(client,"X-AIBot-Device",cfg->id);
    esp_http_client_set_header(client,"X-AIBot-Nonce",nonce);
    esp_http_client_set_header(client,"X-AIBot-Proof",proof);
    esp_http_client_set_header(client,"X-AIBot-Assets",ids);
    esp_http_client_set_header(client,"X-AIBot-Assets-Proof",asset_proof);
    char firmware_proof[65];const char *firmware=esp_app_get_description()->version;
    snprintf(message,sizeof(message),"FIRMWARE|%s|%s",nonce,firmware);device_proof(message,firmware_proof);
    esp_http_client_set_header(client,"X-Tab5-Firmware",firmware);
    esp_http_client_set_header(client,"X-Tab5-Firmware-Proof",firmware_proof);
    // Paired status polling remains available when the USB serial port disappears.
    // Export only numeric diagnostics; never include audio, text, or credentials.
    int64_t now_ms=esp_timer_get_time()/1000;
    snprintf(diag,sizeof(diag),"%lld,%d,%d,%d,%d,%d,%d,%u,%u,%d,%u,%lld,%u",
        (long long)now_ms,tud_mounted()?1:0,
        tab5_voice_http_stage,tab5_voice_http_status,tab5_voice_request_ms,
        tab5_voice_last_seq,voice_remote_queue_depth(),
        camera_viewport_diagnostic_largest(MALLOC_CAP_DMA|MALLOC_CAP_8BIT),
        camera_viewport_diagnostic_largest(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT),
        voice_remote_mic_error(),tab5_voice_capture_frames,
        (long long)(tab5_voice_capture_start_ms?now_ms-tab5_voice_capture_start_ms:0),
        tab5_voice_queue_peak);
    camera_preview_diagnostics(camera_diag,sizeof(camera_diag));
    size_t used_diag=strlen(diag);snprintf(diag+used_diag,sizeof(diag)-used_diag,",%s",camera_diag);
    esp_http_client_set_header(client,"X-Tab5-Diag",diag);
    atomic_store(&status_host,ntohl(inet_addr(cfg->host)));atomic_store(&status_port,cfg->port);
    atomic_fetch_add(&status_attempts,1);
    esp_err_t opened=esp_http_client_open(client,0);
    atomic_store(&status_open_error,opened);atomic_store(&status_socket_error,esp_http_client_get_errno(client));
    int tls_code=0,tls_flags=0;
    atomic_store(&status_transport_error,esp_http_client_get_and_clear_last_tls_error(client,&tls_code,&tls_flags));
    atomic_store(&status_tls_code,tls_code);atomic_store(&status_tls_flags,tls_flags);
    if(opened==ESP_OK) {
        int64_t size=esp_http_client_fetch_headers(client);
        atomic_store(&http_status,esp_http_client_get_status_code(client));
        if(esp_http_client_get_status_code(client)==200&&size>=28&&size<=TAB5_MAX_FRAME) {
            uint8_t *bytes=heap_caps_malloc((size_t)size,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);int used=0;
            if(bytes) {
                while(used<size) {int n=esp_http_client_read(client,(char*)bytes+used,size-used);if(n<=0)break;used+=n;}
                if(used==size) {char *clear=device_decrypt(bytes,size,nonce);if(clear){if(device_receive_for_host(clear,LINK_WIFI,NULL,owner)) {
                    xSemaphoreTake(device_mutex,portMAX_DELAY);last_valid_status=esp_timer_get_time()/1000;xSemaphoreGive(device_mutex);
                    atomic_fetch_add(&status_valid,1);
                }free(clear);}}
                free(bytes);
            }
        }
        esp_http_client_close(client);
    }
    esp_http_client_cleanup(client);
}
static void task(void *arg) {
    ESP_ERROR_CHECK(esp_netif_init());ESP_ERROR_CHECK(esp_event_loop_create_default());station_netif=esp_netif_create_default_wifi_sta();
    ESP_ERROR_CHECK(esp_event_handler_register(WIFI_EVENT,ESP_EVENT_ANY_ID,event,NULL));
    ESP_ERROR_CHECK(esp_event_handler_register(IP_EVENT,IP_EVENT_STA_GOT_IP,event,NULL));
    wifi_init_config_t init=WIFI_INIT_CONFIG_DEFAULT();
    esp_err_t ret=esp_wifi_init(&init);
    if(ret!=ESP_OK){ESP_LOGE("tab5","Wireless initialization failed: %s",esp_err_to_name(ret));vTaskDelete(NULL);return;}
    esp_wifi_set_storage(WIFI_STORAGE_RAM);esp_wifi_set_mode(WIFI_MODE_STA);
    if(esp_wifi_start()!=ESP_OK){vTaskDelete(NULL);return;}
    wifi_performance_init();tab5_wifi_initialized=true;ble_start();
    char last_ssid[33]="",last_password[64]="";int attempts=0;int64_t next_discovery=0;
    while(true) {
        device_config cfg=device_get_config();
        bool probe_reconnect=false;
        if(wifi_probe_service(cfg.mode==LINK_BLE&&(!wifi_probe_requires_usb()||tab5_usb_connected()),&probe_reconnect)) {
            ulTaskNotifyTake(pdTRUE,pdMS_TO_TICKS(100));continue;
        }
        wifi_performance_idle();
        bool manual_reconnect=atomic_exchange(&reconnect_requested,false);
        bool reconnect=manual_reconnect||probe_reconnect;
        if(wifi_recover_connection(&cfg,manual_reconnect)) {
            attempts=0;next_discovery=0;
            // A successful recovery reapplies the current RAM configuration.
            // Keep pending start failures owned by this worker and retry later.
            if(!wifi_restart_pending&&atomic_load(&wifi_config_rc)==ESP_OK){strcpy(last_ssid,cfg.ssid);strcpy(last_password,cfg.password);}
            else last_ssid[0]=last_password[0]=0;
            ulTaskNotifyTake(pdTRUE,pdMS_TO_TICKS(2000));continue;
        }
        if(reconnect){attempts=0;next_discovery=0;}
        if(!cfg.ssid[0]) {
            if(last_ssid[0]||wifi_connected||reconnect)esp_wifi_disconnect();
            last_ssid[0]=last_password[0]=0;
        } else if(reconnect||strcmp(last_ssid,cfg.ssid)||strcmp(last_password,cfg.password)) {
            if(wifi_apply_configuration(&cfg)) {strcpy(last_ssid,cfg.ssid);strcpy(last_password,cfg.password);}
            attempts=0;
        }
        if(cfg.ssid[0]&&!wifi_connected&&++attempts%5==0&&!wifi_connection_waiting()) {
            if(wifi_advance_profile(&cfg,attempts,device_wifi_count())) {
                char names[WIFI_PROFILE_LIMIT][33];int count=device_wifi_names(names),at=0;
                for(int i=0;i<count;i++)if(!strcmp(names[i],cfg.ssid)){at=i;break;}
                device_wifi_select((at+1)%count,false);attempts=0;
            }else wifi_retry_connection(&cfg);
        }
        if(wifi_connected) {
            attempts=0;poll_status();
            wifi_path_sample(&cfg);
            int64_t now=esp_timer_get_time()/1000,received;
            xSemaphoreTake(device_mutex,portMAX_DELAY);received=last_valid_status;xSemaphoreGive(device_mutex);
            if((!received||now-received>8000)&&now>=next_discovery) {
                discover_bridge();next_discovery=now+10000;
            }
        }
        extern volatile int tab5_ui_page;
        xSemaphoreTake(device_mutex,portMAX_DELAY);bool resources_pending=assets_pending();xSemaphoreGive(device_mutex);
        ulTaskNotifyTake(pdTRUE,pdMS_TO_TICKS(wifi_connected&&(tab5_ui_page==4||resources_pending)?250:2000));
    }
}
void tab5_wifi_start(void) {xTaskCreate(task,"tab5_wireless",8192,NULL,4,&wifi_task);}

