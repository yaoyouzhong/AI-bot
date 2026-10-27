#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdio.h>
typedef int SemaphoreHandle_t;
typedef int wifi_ps_type_t;
#define ESP_OK 0
#define WIFI_PS_NONE 0
#define portMAX_DELAY 0
#define pdTRUE 1
static int mode=2,gets,sets,failure;
static int xSemaphoreCreateMutex(void){return 1;}
static int xSemaphoreTake(int lock,int timeout){assert(lock==1);return 1;}
static void xSemaphoreGive(int lock){assert(lock==1);}
static int esp_wifi_get_ps(int *out){gets++;if(failure==1)return 1;*out=mode;return 0;}
static int esp_wifi_set_ps(int value){sets++;if(failure==2)return 2;mode=value;return 0;}
#include "wifi_performance_under_test.inc"
int main(void){
    wifi_performance_init();wifi_performance_bulk(true);assert(mode==0&&users==1&&changed);
    wifi_performance_bulk(true);wifi_performance_bulk(false);assert(mode==0&&users==1&&sets==1);
    wifi_performance_bulk(false);assert(mode==2&&users==0&&!changed&&sets==2);
    wifi_performance_bulk(false);assert(users==0&&sets==2);
    wifi_performance_bulk(true);failure=2;wifi_performance_bulk(false);assert(changed&&mode==0);failure=0;wifi_performance_idle();assert(!changed&&mode==2);
    failure=1;wifi_performance_bulk(true);wifi_performance_bulk(false);assert(mode==2&&!changed);failure=0;
    mode=0;wifi_performance_bulk(true);wifi_performance_bulk(false);assert(mode==0&&!changed);
    puts("WIFI_PERFORMANCE_OK nested users, exact idle restoration, failed restore retry, failed get, already disabled, release underflow");
}
