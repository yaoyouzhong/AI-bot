#include <assert.h>
#include <stdatomic.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include "../../firmware/main/link_state.h"
#include "../../firmware/main/wifi_status.h"
#define WIFI_LABEL_SIZE 49
#define portMAX_DELAY 0
#define xSemaphoreTake(a,b) ((void)0)
#define xSemaphoreGive(a) ((void)0)
typedef struct {bool paired;char ssid[33];} device_config;
static device_config config={true,"Home"};
static device_config device_get_config(void){return config;}
static void device_wifi_label(const char *ssid,char *label){(void)ssid;strcpy(label,"家庭网络");}
static char local_ip[16]="192.168.1.20";
static link_state device_links;
static bool wifi_connected=true;
static atomic_int disconnect_reason,http_status=200;
static atomic_bool status_pending;
static int64_t now_ms=20000,last_valid_status=19900;
static int64_t esp_timer_get_time(void){return now_ms*1000;}
#include "wifi_host_status_under_test.inc"
int main(void) {
    char text[320];link_state_init(&device_links);
    tab5_wifi_diagnostics(text,sizeof(text));assert(!strstr(text,"鉴权通过")); // previous host HTTP still fresh
    device_links.received_ms[LINK_WIFI]=now_ms-100;tab5_wifi_diagnostics(text,sizeof(text));assert(strstr(text,"鉴权通过"));
    link_state_init(&device_links);tab5_wifi_diagnostics(text,sizeof(text));assert(!strstr(text,"鉴权通过"));
    device_links.received_ms[LINK_WIFI]=now_ms-8000;tab5_wifi_diagnostics(text,sizeof(text));assert(!strstr(text,"鉴权通过"));
    device_links.received_ms[LINK_WIFI]=now_ms+1;tab5_wifi_diagnostics(text,sizeof(text));assert(!strstr(text,"鉴权通过"));
    config.paired=false;tab5_wifi_diagnostics(text,sizeof(text));assert(strstr(text,"请先通过 USB 配对"));config.paired=true;
    wifi_connected=false;tab5_wifi_diagnostics(text,sizeof(text));assert(strstr(text,"等待 Wi-Fi"));
    tab5_wifi_status status;tab5_wifi_status_read(&status);assert(!status.connected&&!status.authenticated);
    wifi_connected=true;device_links.received_ms[LINK_WIFI]=now_ms;tab5_wifi_status_read(&status);assert(status.connected&&status.authenticated);
    assert(!strcmp(status.address,"192.168.1.20")&&!strcmp(status.network,"家庭网络 · Home"));
    now_ms+=8000;atomic_store(&status_pending,true);tab5_wifi_status_read(&status);
    assert(status.connected&&!status.authenticated&&strstr(status.bridge,"正在等待电脑响应"));
    atomic_store(&status_pending,false);atomic_store(&http_status,401);tab5_wifi_status_read(&status);assert(strstr(status.bridge,"配对不匹配"));
    atomic_store(&http_status,-1);tab5_wifi_status_read(&status);assert(strstr(status.bridge,"未收到有效响应"));
    atomic_store(&http_status,0);tab5_wifi_status_read(&status);assert(strstr(status.bridge,"暂不可达"));
    atomic_store(&http_status,503);tab5_wifi_status_read(&status);assert(strstr(status.bridge,"响应异常"));
    wifi_connected=false;atomic_store(&disconnect_reason,202);tab5_wifi_status_read(&status);assert(strstr(status.state,"认证失败"));
    atomic_store(&disconnect_reason,201);tab5_wifi_status_read(&status);assert(strstr(status.state,"未找到"));
    strcpy(config.ssid,"line1\nline2\rline3");tab5_wifi_status_read(&status);assert(!strchr(status.network,'\n')&&!strchr(status.network,'\r'));
    config.ssid[0]=0;tab5_wifi_status_read(&status);assert(!status.connected&&strstr(status.state,"添加 Wi-Fi"));
    puts("WIFI_HOST_STATUS_OK old-host HTTP cannot confirm new selection; current authenticated heartbeat, invalidation, expiry/future, unpaired and offline states");return 0;
}
