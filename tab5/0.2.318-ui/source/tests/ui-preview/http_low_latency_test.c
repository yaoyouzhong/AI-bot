#undef NDEBUG
#include <assert.h>
#include <stdio.h>
#include <string.h>
typedef int esp_err_t;
typedef struct {int value;} esp_tls_cfg_t;
typedef void *esp_tls_error_handle_t;
#define ESP_OK 0
#define IPPROTO_TCP 6
#define TCP_NODELAY 1
static int connect_result,option_result,calls,warnings;
#define ESP_LOGW(tag,msg) (++warnings)
static int setsockopt(int fd,int level,int option,const void *value,unsigned size){
    assert(fd==7&&level==IPPROTO_TCP&&option==TCP_NODELAY&&size==sizeof(int)&&*(const int*)value==1);
    calls++;return option_result;
}
#include "http_low_latency_under_test.inc"
esp_err_t __real_esp_tls_plain_tcp_connect(const char *host,int hostlen,int port,const esp_tls_cfg_t *cfg,esp_tls_error_handle_t error,int *fd){
    assert(!strcmp(host,"bridge")&&hostlen==6&&port==18765&&cfg->value==19&&error==NULL);*fd=connect_result?-1:7;return connect_result;
}
int main(void){esp_tls_cfg_t cfg={19};int fd=-1;
    assert(__wrap_esp_tls_plain_tcp_connect("bridge",6,18765,&cfg,NULL,&fd)==0&&fd==7&&calls==1&&!warnings);
    connect_result=-1;assert(__wrap_esp_tls_plain_tcp_connect("bridge",6,18765,&cfg,NULL,&fd)==-1&&fd==-1&&calls==1);
    connect_result=0;option_result=-1;assert(__wrap_esp_tls_plain_tcp_connect("bridge",6,18765,&cfg,NULL,&fd)==0&&fd==7&&calls==2&&warnings==1);
    puts("HTTP_LOW_LATENCY_OK new connected socket only; connection errors preserved; unsupported option retains connection");
}
