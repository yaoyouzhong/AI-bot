// Reuse the existing independent CNG verifier and cross-language vectors.
#define main existing_stream_vector_main
#include "gallery_stream_test.c"
#undef main
#include "gallery_read_pipeline.h"
#include "gallery_profile.h"
#define ESP_OK 0
#define HTTP_METHOD_POST 1
#define MALLOC_CAP_SPIRAM 1
#define MALLOC_CAP_8BIT 2
#define RPC_RESPONSE_MAX 65535
#define pdPASS 1
#define pdMS_TO_TICKS(x) (x)
typedef bool (*rpc_gallery_progress)(void *,size_t);
typedef struct {char id[20];} device_config;
typedef struct {uint8_t *wire;size_t length,at,cut,reads;int code,closed;} fake_http;
typedef fake_http *esp_http_client_handle_t;
static HANDLE receive_task;
static int fail_alloc,fail_task,cancel_progress;
static struct {uint64_t decode_us;} gallery_profiles;
static uint64_t gallery_backpressure_us;
static uint64_t gallery_network_read_us,gallery_consumer_wait_us;
static unsigned gallery_receive_ahead_calls,gallery_receive_peak_bytes;
static char *device_decrypt_checked(const uint8_t *packet,size_t size,const char *nonce,size_t maximum,int *error){assert(maximum==65535);return decrypt_wire(NULL,packet,size,nonce,error);}
static int64_t esp_timer_get_time(void){LARGE_INTEGER f,n;QueryPerformanceFrequency(&f);QueryPerformanceCounter(&n);return n.QuadPart*1000000/f.QuadPart;}
static int esp_http_client_set_method(fake_http *h,int m){assert(m==HTTP_METHOD_POST);return 0;}
static void esp_http_client_set_header(fake_http *h,const char *k,const char *v){assert(k&&v);}
static int esp_http_client_open(fake_http *h,size_t n){assert(n==1);h->at=h->reads=h->closed=0;return 0;}
static int esp_http_client_write(fake_http *h,const char *b,size_t n){return (int)n;}
static int64_t esp_http_client_fetch_headers(fake_http *h){return (int64_t)h->length;}
static int esp_http_client_get_status_code(fake_http *h){return h->code;}
static void esp_http_client_close(fake_http *h){h->closed++;}
static int esp_http_client_read(fake_http *h,char *out,size_t n){
    assert(n>0&&n<=65535);h->reads++;
    if(h->at>=h->cut)return 0;
    if(n>h->cut-h->at)n=h->cut-h->at;
    if(n>4093)n=4093;
    memcpy(out,h->wire+h->at,n);h->at+=n;return (int)n;
}
static void *heap_caps_malloc(size_t n,int flags){assert(n==GALLERY_HTTP_AHEAD_BYTES&&flags==3);return fail_alloc?NULL:malloc(n);}
static void vTaskDelay(unsigned ms){Sleep(ms);}
static void vTaskDelete(void *p){assert(!p);ExitThread(0);}
typedef struct {void (*fn)(void *);void *arg;} task_call;
static DWORD WINAPI task_run(void *p){task_call c=*(task_call*)p;free(p);c.fn(c.arg);return 0;}
static int xTaskCreate(void (*fn)(void *),const char *name,unsigned stack,void *arg,unsigned priority,void *out){
    assert(stack==4096&&priority==4&&!out&&!receive_task);
    if(fail_task)return 0;
    task_call *c=malloc(sizeof(*c));assert(c);*c=(task_call){fn,arg};
    receive_task=CreateThread(NULL,0,task_run,c,0,NULL);assert(receive_task);return pdPASS;
}
#include "gallery_http_pipeline_under_test.inc"
static bool checkpoint(void *ctx,size_t verified){size_t *previous=ctx;assert(verified>*previous);*previous=verified;Sleep(2);return !cancel_progress;}
int main(int argc,char **argv){
    assert(argc==2);char path[1024],nonce[33];memset(nonce,'a',32);nonce[32]=0;
    snprintf(path,sizeof(path),"%s/gallery-stream-0-1048576.bin",argv[1]);
    FILE *file=fopen(path,"rb");assert(file&&!fseek(file,0,SEEK_END));size_t bytes=ftell(file);rewind(file);
    uint8_t *wire=malloc(bytes),*out=malloc(1048576);assert(wire&&out&&fread(wire,1,bytes,file)==bytes);fclose(file);
    device_config cfg={"001122334455"};uint8_t packet=1;unsigned cases=0;
    for(int mode=0;mode<7;mode++){
        fake_http http={.wire=wire,.length=bytes,.cut=bytes,.code=200};
        fail_alloc=mode==1;fail_task=mode==2;cancel_progress=mode==4;
        if(mode==3)http.cut=100000;if(mode==5)http.code=409;
        if(mode==6)wire[bytes-1]^=1;
        cJSON *request=cJSON_CreateObject();cJSON_AddNumberToObject(request,"offset",0);cJSON_AddNumberToObject(request,"count",1048576);
        size_t received=0,verified=0;int status=0;
        cJSON *body=http_gallery_exchange(&http,&cfg,nonce,"proof",&packet,1,request,out,1048576,&received,&status,checkpoint,&verified);
        if(receive_task){assert(WaitForSingleObject(receive_task,5000)==WAIT_OBJECT_0);CloseHandle(receive_task);receive_task=NULL;}
        if(mode<3){assert(body&&status==200&&received==1048576&&!http.closed&&http.at==bytes);for(size_t i=0;i<received;i++)assert(out[i]==(uint8_t)(i*31+17));}
        else {assert(!body&&http.closed==1);if(mode==5)assert(status==409&&!received&&!verified);else assert(received==verified&&received<1048576);}
        if(mode==6)wire[bytes-1]^=1;
        cJSON_Delete(body);cJSON_Delete(request);cases++;
    }
    free(out);free(wire);printf("GALLERY_HTTP_PIPELINE_OK %u production HTTP/task tests; receive/decrypt/checkpoint; allocation/task fallback; disconnect/cancel/409/corrupt AEAD safe teardown\n",cases);return 0;
}
