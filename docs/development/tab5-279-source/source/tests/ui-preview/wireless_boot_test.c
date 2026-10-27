#include <assert.h>
#include <stdbool.h>
#include <stdio.h>
#include <string.h>
typedef int esp_err_t;
enum {ESP_RST_POWERON=1,ESP_RST_EXT=2,ESP_RST_SW=3,ESP_RST_PANIC=4};
#define TAB5_WIRELESS_BOOT_TEST
#define pdMS_TO_TICKS(ms) (ms)
static int reason,hosted_result;
static bool powered,hosted_started;
static unsigned off_ms;
static char actions[32];static unsigned action_count;
static void record(char action){assert(action_count<sizeof(actions)-1);actions[action_count++]=action;actions[action_count]=0;}
static int esp_reset_reason(void){return reason;}
static void bsp_set_wifi_power_enable(bool enabled){assert(!hosted_started);powered=enabled;record(enabled?'1':'0');}
static void vTaskDelay(unsigned ticks){assert(!powered&&!hosted_started);off_ms+=ticks;record('D');}
static esp_err_t esp_hosted_init(void){assert(powered&&!hosted_started);hosted_started=true;record('H');return hosted_result;}
#include "../../firmware/main/wireless_boot.c"
static void reset(int why,int result){reason=why;hosted_result=result;powered=true;hosted_started=false;off_ms=0;action_count=0;actions[0]=0;}
int main(void){
    char diag[64];
    reset(ESP_RST_POWERON,0);assert(tab5_wireless_boot_prepare()==0);assert(!strcmp(actions,"1H")&&off_ms==0);
    tab5_wireless_boot_diagnostics(diag,sizeof(diag));assert(!strcmp(diag,"1,1,0,0"));
    reset(ESP_RST_SW,0);assert(tab5_wireless_boot_prepare()==0);assert(!strcmp(actions,"0D1H")&&off_ms==500);
    tab5_wireless_boot_diagnostics(diag,sizeof(diag));assert(!strcmp(diag,"1,3,1,0"));
    reset(ESP_RST_SW,0x101);assert(tab5_wireless_boot_prepare()==0x101);assert(!strcmp(actions,"0D1H")&&powered);
    tab5_wireless_boot_diagnostics(diag,sizeof(diag));assert(!strcmp(diag,"1,3,1,257"));
    reset(ESP_RST_EXT,0);assert(tab5_wireless_boot_prepare()==0);assert(!strcmp(actions,"1H")&&off_ms==0);
    reset(ESP_RST_PANIC,0);assert(tab5_wireless_boot_prepare()==0);assert(!strcmp(actions,"1H")&&off_ms==0);
    puts("WIRELESS_BOOT_OK cold boot; OTA warm reset drains power before SDK tasks; preserves init failures; no runtime/deinit loop");
}
