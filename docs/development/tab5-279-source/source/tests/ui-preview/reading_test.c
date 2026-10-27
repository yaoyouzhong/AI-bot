#include "ui_prefs.h"
#include "nvs.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>
static unsigned char disk[8192],pending[8192];static size_t saved_length,pending_length;static bool fail;
int nvs_open(const char *name,int mode,nvs_handle_t *h){(void)name;(void)mode;*h=1;return 0;}
void nvs_close(nvs_handle_t h){(void)h;}
int nvs_get_blob(nvs_handle_t h,const char *key,void *data,size_t *length){(void)h;(void)key;if(!saved_length||*length<saved_length)return -1;memcpy(data,disk,saved_length);*length=saved_length;return 0;}
int nvs_set_blob(nvs_handle_t h,const char *key,const void *data,size_t length){(void)h;(void)key;assert(length<=sizeof(pending));memcpy(pending,data,length);pending_length=length;return 0;}
int nvs_commit(nvs_handle_t h){(void)h;if(fail)return -1;memcpy(disk,pending,pending_length);saved_length=pending_length;return 0;}
int main(void){
    ui_reading_bookmark before[32]={0},after[32];assert(!ui_reading_load(after)&&!after[0].task[0]);
    strcpy(before[0].task,"00112233-4455-6677-8899-aabbccddeeff");strcpy(before[0].turn,"ffeeddcc-bbaa-9988-7766-554433221100");before[0].page=12;before[0].y=384;before[0].stamp=9;
    assert(ui_reading_save(before));memset(after,0,sizeof(after));assert(ui_reading_load(after)&&after[0].page==12&&after[0].y==384&&!after[0].follow&&!strcmp(before[0].turn,after[0].turn));
    fail=true;before[0].page=15;assert(!ui_reading_save(before));assert(ui_reading_load(after)&&after[0].page==12);fail=false;
    before[0].page=-1;assert(!ui_reading_save(before));before[0].page=0;memset(before[0].turn,'x',sizeof(before[0].turn));assert(!ui_reading_save(before));
    disk[0]=99;assert(!ui_reading_load(after)&&!after[0].task[0]);puts("READING_PERSISTENCE_OK reload, positions, commit failure, malformed data");return 0;
}
