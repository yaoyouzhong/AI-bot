#include "ui_prefs.h"
#include "nvs.h"
#include <string.h>
#include <ctype.h>
#include <stdlib.h>
typedef struct {uint32_t version;ui_reading_bookmark items[32];} reading_store;
static bool identifier(const char *s,size_t capacity) {
    size_t n=0;while(n<capacity&&s[n]){if(!isalnum((unsigned char)s[n])&&s[n]!='-')return false;n++;}
    return n>0&&n<capacity;
}
static bool valid(const ui_reading_bookmark *values) {
    for(int i=0;i<32;i++) {
        const ui_reading_bookmark *p=&values[i];
        if(!p->task[0])continue;
        if(!identifier(p->task,sizeof(p->task))||!identifier(p->turn,sizeof(p->turn))||p->page<0||p->page>100000||p->y<0||p->y>1000000)return false;
    }
    return true;
}
bool ui_reading_load(ui_reading_bookmark values[32]) {
    memset(values,0,32*sizeof(*values));nvs_handle_t h;
    if(nvs_open("aibot_tab5",NVS_READONLY,&h)!=ESP_OK)return false;
    reading_store *candidate=calloc(1,sizeof(*candidate));if(!candidate){nvs_close(h);return false;}
    size_t length=sizeof(*candidate);esp_err_t r=nvs_get_blob(h,"reading",candidate,&length);nvs_close(h);
    bool ok=r==ESP_OK&&length==sizeof(*candidate)&&candidate->version==1&&valid(candidate->items);
    if(ok)memcpy(values,candidate->items,sizeof(candidate->items));
    free(candidate);return ok;
}
bool ui_reading_save(const ui_reading_bookmark values[32]) {
    if(!values||!valid(values))return false;
    nvs_handle_t h;
    if(nvs_open("aibot_tab5",NVS_READWRITE,&h)!=ESP_OK)return false;
    reading_store *data=calloc(1,sizeof(*data));if(!data){nvs_close(h);return false;}
    data->version=1;memcpy(data->items,values,sizeof(data->items));
    esp_err_t r=nvs_set_blob(h,"reading",data,sizeof(*data));free(data);if(r==ESP_OK)r=nvs_commit(h);nvs_close(h);return r==ESP_OK;
}
