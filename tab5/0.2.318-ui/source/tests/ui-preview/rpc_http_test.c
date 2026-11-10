#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#define ESP_OK 0
#define RPC_RESPONSE_MAX 65535
#define MALLOC_CAP_SPIRAM 1
#define MALLOC_CAP_8BIT 2
#define HTTP_METHOD_GET 0
#define HTTP_METHOD_POST 1
typedef struct {char id[20];} device_config;
typedef void *esp_http_client_handle_t;
static int fault,closed,headers,status=200,fragment;
static int method=HTTP_METHOD_GET;
static int64_t length;
static size_t read_at,write_at;
static uint8_t source[RPC_RESPONSE_MAX],input[12192];
static int esp_http_client_set_header(void *c,const char *name,const char *value){(void)c;assert(name&&value);headers++;return 0;}
static int esp_http_client_set_method(void *c,int value){(void)c;method=value;return fault==5?-1:ESP_OK;}
static int esp_http_client_open(void *c,size_t n){(void)c;assert(n==sizeof(input));read_at=write_at=0;return fault==1?-1:0;}
static void esp_http_client_close(void *c){(void)c;closed++;}
static int esp_http_client_write(void *c,const char *data,size_t n){(void)c;if(fault==2)return -1;if(n>(size_t)fragment)n=fragment;assert(!memcmp(data,input+write_at,n));write_at+=n;return (int)n;}
static int64_t esp_http_client_fetch_headers(void *c){(void)c;assert(write_at==sizeof(input));return length;}
// Match the bridge router: GET /tab5/v1/rpc never reaches the RPC handler.
static int esp_http_client_get_status_code(void *c){(void)c;return method==HTTP_METHOD_POST?status:404;}
static void *heap_caps_malloc(size_t n,int flags){assert(flags==3);return fault==4?NULL:malloc(n);}
static int esp_http_client_read(void *c,char *out,size_t n){(void)c;if(fault==3&&read_at>=512)return 0;if(n>(size_t)fragment)n=fragment;memcpy(out,source+read_at,n);read_at+=n;return (int)n;}
#include "rpc_http_under_test.inc"
int main(void){
    device_config cfg={"001122334455"};for(size_t i=0;i<sizeof(source);i++)source[i]=(uint8_t)(i*31+17);memcpy(input,source,sizeof(input));
    int response_status=99;
    for(fragment=1;fragment<=1024;fragment*=4)for(int repeat=0;repeat<2;repeat++){
        method=HTTP_METHOD_GET;length=sizeof(source);closed=headers=0;size_t n=0;uint8_t *reply=http_exchange(NULL,&cfg,"nonce","proof",input,sizeof(input),&n,&response_status);
        assert(reply&&n==sizeof(source)&&!memcmp(reply,source,n)&&headers==3&&!closed&&response_status==0);free(reply);
    }
    for(fault=1;fault<=5;fault++){size_t n=99;closed=0;assert(!http_exchange(NULL,&cfg,"nonce","proof",input,sizeof(input),&n,&response_status)&&n==0&&closed==1&&response_status==0);}
    fault=0;int64_t invalid[]={-1,0,27,RPC_RESPONSE_MAX+1};for(unsigned i=0;i<4;i++){length=invalid[i];size_t n=99;closed=0;assert(!http_exchange(NULL,&cfg,"nonce","proof",input,sizeof(input),&n,&response_status)&&n==0&&closed==1&&response_status==0);}
    int errors[]={401,403,404,503};length=RPC_RESPONSE_MAX;
    for(unsigned i=0;i<4;i++){status=errors[i];size_t n=99;closed=0;assert(!http_exchange(NULL,&cfg,"nonce","proof",input,sizeof(input),&n,&response_status)&&n==0&&closed==1&&response_status==status);}
    puts("RPC_HTTP_STATUS_OK authentication and server errors remain distinct from missing replies");
    puts("RPC_HTTP_OK production POST overrides default GET; exact persistent fragmented I/O; method/open/write/truncation/allocation/status/length failures close connection");return 0;
}
