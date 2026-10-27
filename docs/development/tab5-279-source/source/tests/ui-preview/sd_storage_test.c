#undef NDEBUG
#include <assert.h>
#include <windows.h>
#include <stdatomic.h>
#include <stdint.h>
#include <string.h>
#include <stdio.h>
#include "sd_storage.h"
typedef HANDLE SemaphoreHandle_t;
typedef void *TaskHandle_t;
enum {pdTRUE=1,ESP_OK=0};
#define pdMS_TO_TICKS(ms) (ms)
static atomic_int_fast64_t now;
static int mounts,unmounts,checks,monitor_starts;
static bool mount_failure,card_removed;
static void vTaskDelay(int ticks){(void)ticks;}
static int xTaskCreate(void (*worker)(void *),const char *name,int stack,void *arg,int priority,TaskHandle_t *handle) {
    assert(worker&&!strcmp(name,"tf_status")&&stack==4096&&!arg&&priority==1);
    monitor_starts++;*handle=(void *)1;return 1;
}
static HANDLE xSemaphoreCreateMutex(void){return CreateMutex(NULL,FALSE,NULL);}
static int xSemaphoreTake(HANDLE handle,int timeout){return WaitForSingleObject(handle,timeout)==WAIT_OBJECT_0;}
static void xSemaphoreGive(HANDLE handle){assert(ReleaseMutex(handle));}
static int64_t esp_timer_get_time(void){return atomic_load(&now)*1000;}
static int bsp_sdcard_init(char *path,size_t files){assert(!strcmp(path,"/sdcard")&&files==6);mounts++;Sleep(20);return mount_failure?-1:0;}
static int bsp_sdcard_deinit(char *path){assert(!strcmp(path,"/sdcard"));unmounts++;return 0;}
static int bsp_sdcard_check(void){checks++;return card_removed?-1:0;}
#define SD_STORAGE_TEST
#include "../../firmware/main/sd_storage.c"
static atomic_int users,max_users;
static DWORD WINAPI read_user(void *arg) {
    (void)arg;assert(sd_storage_lock());int n=atomic_fetch_add(&users,1)+1;
    if(n>atomic_load(&max_users))atomic_store(&max_users,n);
    Sleep(20);atomic_fetch_sub(&users,1);sd_storage_unlock(false);return 0;
}
int main(void) {
    sd_storage_init();assert(sd_storage_state()==0);sd_storage_start();sd_storage_start();assert(monitor_starts==1);
    mount_failure=true;assert(sd_storage_available());sd_storage_poll();
    assert(mounts==1&&sd_storage_state()==-1&&!sd_storage_available());
    assert(!sd_storage_lock()&&mounts==1);atomic_store(&now,30001);mount_failure=false;
    HANDLE threads[2]={CreateThread(NULL,0,read_user,NULL,0,NULL),CreateThread(NULL,0,read_user,NULL,0,NULL)};
    assert(threads[0]&&threads[1]&&WaitForMultipleObjects(2,threads,TRUE,5000)==WAIT_OBJECT_0);
    CloseHandle(threads[0]);CloseHandle(threads[1]);
    assert(mounts==2&&atomic_load(&max_users)==1&&sd_storage_state()==1);
    assert(sd_storage_lock());sd_storage_unlock(true);assert(unmounts==1&&!sd_storage_available());
    atomic_store(&now,60002);assert(sd_storage_lock());sd_storage_unlock(false);assert(mounts==3&&sd_storage_state()==1);
    // Standalone detection does not require gallery selection or any bridge.
    sd_storage_poll();assert(checks==1&&sd_storage_state()==1&&mounts==3);
    card_removed=true;sd_storage_poll();assert(checks==2&&sd_storage_state()==-1&&unmounts==2);
    sd_storage_poll();assert(checks==2&&mounts==3);
    card_removed=false;atomic_store(&now,90003);sd_storage_poll();assert(mounts==4&&checks==3&&sd_storage_state()==1);
    puts("SD_STORAGE_OK independent background detection; shared lock; no card cooldown; removal unmounts; reinsertion retries; no formatting");
    CloseHandle(mutex);return 0;
}
