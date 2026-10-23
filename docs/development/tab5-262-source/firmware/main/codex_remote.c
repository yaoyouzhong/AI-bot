#include "codex_remote.h"
#include "device.h"
#include "rpc_link.h"
#include "wifi_performance.h"
#include "transport_benchmark.h"
#include "esp_http_client.h"
#include "esp_random.h"
#include "esp_timer.h"
#include "freertos/idf_additions.h"
#include "esp_heap_caps.h"
#include "mbedtls/gcm.h"
#include "mbedtls/sha256.h"
#include "photo_transfer_crypto.h"
#include "photo_display_probe.h"
#include "mbedtls/base64.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

typedef struct { char task_id[37], message[2001], session[40],op[16],request_id[37],expected_turn[37],images[3][37]; link_kind transport; int64_t issued_at; bool read_only; int page,turn_offset; uint8_t *image;size_t image_size; unsigned host_generation;int mode; } codex_job;
#include "photo_upload_retry.h"
static char result[96];
static char result_task[37];
static int result_code;
static char upload_diagnostic[96];
static unsigned upload_failures,upload_retries,upload_first_offset,upload_first_ms,upload_first_phase;
static int upload_first_status;
void codex_remote_upload_diagnostics(char *out,unsigned size){
    xSemaphoreTake(device_mutex,portMAX_DELAY);int used=snprintf(out,size,"%s",upload_diagnostic);xSemaphoreGive(device_mutex);
    if(used>=0&&(unsigned)used+1<size){out[used++]=';';photo_tx_diagnostics(out+used,size-(unsigned)used);}
}
static void upload_mark(codex_job *job,int64_t started,int status){
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    snprintf(upload_diagnostic,sizeof(upload_diagnostic),"%s,%u,%u,%d;f,%u,%u,%d,%u,%u;r,%u",link_name(job->transport),
        (unsigned)job->image_size,(unsigned)((esp_timer_get_time()-started)/1000),status,upload_failures,
        upload_first_offset,upload_first_status,upload_first_ms,upload_first_phase,upload_retries);
    xSemaphoreGive(device_mutex);
}
static void upload_failure(size_t offset,int status,int64_t started,unsigned phase){
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    if(!upload_failures){upload_first_offset=(unsigned)offset;upload_first_status=status;
        upload_first_ms=(unsigned)((esp_timer_get_time()-started)/1000);upload_first_phase=phase;}
    upload_failures++;xSemaphoreGive(device_mutex);
}
static char result_turn[64];
static cJSON *result_view;
cJSON *codex_remote_take_read_view(void){xSemaphoreTake(device_mutex,portMAX_DELAY);cJSON *view=result_view;result_view=NULL;xSemaphoreGive(device_mutex);return view;}
void codex_remote_read_result(char *out,unsigned size){xSemaphoreTake(device_mutex,portMAX_DELAY);snprintf(out,size,"%s",result_turn);xSemaphoreGive(device_mutex);}
static bool busy;
typedef struct {char *body;size_t capacity,length;bool overflow;} codex_http_reply;
static esp_err_t collect_reply(esp_http_client_event_t *event) {
    codex_http_reply *reply=event->user_data;
    if(event->event_id==HTTP_EVENT_ON_DATA&&reply&&event->data_len>0) {
        size_t count=(size_t)event->data_len;
        if(!reply->body||count>reply->capacity-1-reply->length){reply->overflow=true;return ESP_FAIL;}
        memcpy(reply->body+reply->length,event->data,count);reply->length+=count;reply->body[reply->length]=0;
    }
    return ESP_OK;
}

static void set_result(const char *value, int code) {
    xSemaphoreTake(device_mutex, portMAX_DELAY);
    snprintf(result, sizeof(result), "%s", value);
    result_code = code;
    xSemaphoreGive(device_mutex);
}
int codex_remote_status(const char *task_id, char *out, unsigned size) {
    if (!out || !size) return 0;
    xSemaphoreTake(device_mutex, portMAX_DELAY);
    snprintf(out, size, "%s", task_id && !strcmp(task_id, result_task) ? result : "");
    int code = task_id && !strcmp(task_id, result_task) ? result_code : 0;
    xSemaphoreGive(device_mutex);
    return code;
}
static cJSON *rpc_job(codex_job *job,cJSON *json,int *status,char nonce[33]) {
    if(!job->image) {cJSON_AddStringToObject(json,"kind","codex");return rpc_call(job->transport,json,status,nonce);}
    uint8_t hash[32];char sha[65];if(photo_tx_sha256(job->image,job->image_size,hash)){*status=-1010;return NULL;}
    for(int i=0;i<32;i++)sprintf(sha+i*2,"%02x",hash[i]);
    cJSON_AddStringToObject(json,"kind","image-chunk");cJSON_AddStringToObject(json,"sha256",sha);
    cJSON_AddNumberToObject(json,"size",job->image_size);
    cJSON *reply=NULL;size_t chunk_limit=rpc_data_limit(job->transport);unsigned retries=0;
    int64_t began=esp_timer_get_time();
    for(size_t at=0;at<job->image_size;) {
        size_t count=job->image_size-at;if(count>chunk_limit)count=chunk_limit;
        cJSON_DeleteItemFromObjectCaseSensitive(json,"offset");cJSON_AddNumberToObject(json,"offset",at);
        for(;;) {
            cJSON_Delete(reply);reply=NULL;
            if(job->transport==LINK_BLE&&!photo_ble_current(job->host_generation,job->mode,job->session)){
                *status=-1005;upload_failure(at,*status,began,1);break;
            }
            reply=rpc_data_call(job->transport,NULL,json,status,nonce,job->image+at,count,NULL,0,NULL);
            if(*status==200)break;
            cJSON *range_error=cJSON_GetObjectItemCaseSensitive(reply,"error");
            if(*status==400&&count>6144&&cJSON_IsString(range_error)&&!strcmp(range_error->valuestring,"invalid_range"))break;
            upload_failure(at,*status,began,2);
            // Windows validates duplicate prefixes and final image IDs. Reuse
            // this exact range; rpc_data_call creates a fresh nonce each time.
            // Authentication, validation and ownership errors are final.
            if(job->transport!=LINK_BLE||*status!=0||retries>=PHOTO_BLE_RETRIES||!photo_ble_retry_supported()||
               !photo_ble_wait(job->host_generation,job->mode,job->session))break;
            retries++;xSemaphoreTake(device_mutex,portMAX_DELAY);upload_retries++;xSemaphoreGive(device_mutex);
        }
        cJSON *error=cJSON_GetObjectItemCaseSensitive(reply,"error");
        if(*status==400&&count>6144&&cJSON_IsString(error)&&!strcmp(error->valuestring,"invalid_range")){chunk_limit=6144;continue;}
        if(*status!=200)break;
        at+=count;
        char progress[96];snprintf(progress,sizeof(progress),"正在通过 %s 添加照片… %u%%",link_name(job->transport),(unsigned)(at*100/job->image_size));set_result(progress,-1);
        if(at<job->image_size) {
            cJSON *next=cJSON_GetObjectItemCaseSensitive(reply,"offset");
            if(!cJSON_IsNumber(next)||next->valuedouble!=(double)at){*status=502;upload_failure(at,*status,began,4);break;}
        }
    }
    return reply;
}
static void send_job(void *arg) {
    codex_job *job = arg;
    bool image_upload=!strcmp(job->op,"image-upload");
    if(image_upload){photo_tx_phase(PHOTO_TX_PACK);photo_display_probe_mark(PHOTO_DISPLAY_PROBE_PACK);}
    bool wifi_bulk=job->image&&job->transport==LINK_WIFI;
    if(wifi_bulk)wifi_performance_bulk(true);
    int64_t upload_started=esp_timer_get_time();
    if(job->image){upload_mark(job,upload_started,-1);char progress[96];snprintf(progress,sizeof(progress),"正在通过 %s 添加照片… %u KB",link_name(job->transport),(unsigned)((job->image_size+1023)/1024));set_result(progress,-1);}
    device_config cfg = device_get_config();
    char nonce[33], proof[65], digest[65], auth[155], url[112];
    device_nonce(nonce);
    cJSON *json = cJSON_CreateObject();
    cJSON_AddStringToObject(json, "session", job->session);
    cJSON_AddStringToObject(json, "taskId", job->task_id);
    cJSON_AddStringToObject(json, "message", job->message);
    cJSON_AddStringToObject(json,"op",job->op);
    cJSON_AddStringToObject(json,"requestId",job->request_id);
    cJSON_AddStringToObject(json,"expectedTurn",job->expected_turn);
    cJSON *images=cJSON_AddArrayToObject(json,"images");
    for(int i=0;i<3;i++)if(job->images[i][0])cJSON_AddItemToArray(images,cJSON_CreateString(job->images[i]));
    bool binary_image=false;
    if(job->image&&job->transport==LINK_WIFI) {
        xSemaphoreTake(device_mutex,portMAX_DELAY);cJSON *cap=cJSON_GetObjectItemCaseSensitive(device_snapshots[LINK_WIFI],"imageBinary");
        binary_image=cJSON_IsNumber(cap)&&cap->valuedouble==1;xSemaphoreGive(device_mutex);
    }
    if(job->image&&job->transport==LINK_WIFI&&!binary_image) {
        size_t encoded_size=4*((job->image_size+2)/3)+1,written=0;
        unsigned char *encoded=heap_caps_malloc(encoded_size,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
        if(encoded&&mbedtls_base64_encode(encoded,encoded_size,&written,job->image,job->image_size)==0){encoded[written]=0;cJSON_AddStringToObject(json,"image",(char *)encoded);}
        free(encoded);free(job->image);job->image=NULL;
    }
    if(!strcmp(job->op,"read")){cJSON_AddBoolToObject(json,"inlineReply",true);cJSON_AddNumberToObject(json,"page",job->page);cJSON_AddStringToObject(json,"turnId",job->expected_turn);cJSON_AddNumberToObject(json,"turnOffset",job->turn_offset);}
    if(!strcmp(job->op,"read")&&job->transport!=LINK_WIFI)cJSON_AddNumberToObject(json,"rpcReplyVersion",1);
    if(!strcmp(job->op,"read")&&job->transport==LINK_BLE) {
        cJSON_AddBoolToObject(json,"packedReply",true);
    }
    cJSON_AddNumberToObject(json, "issuedAt", (double)job->issued_at);
    int rpc_status=0;
    cJSON *rpc_reply=job->transport!=LINK_WIFI?rpc_job(job,json,&rpc_status,nonce):NULL;
    char *clear = job->transport==LINK_WIFI?cJSON_PrintUnformatted(json):NULL;
    cJSON_Delete(json);
    size_t len = clear ? strlen(clear) : 0;
    size_t metadata_length=len;
    if(binary_image)len=metadata_length>=2&&metadata_length<=4096?8+metadata_length+job->image_size:0;
    uint8_t *packet = len && len < (image_upload?3*1024*1024:8000) ? heap_caps_malloc(len + 28,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT) : NULL;
    int status = rpc_status;
    size_t reply_capacity=!strcmp(job->op,"read")?24576:384;
    codex_http_reply response={.body=heap_caps_calloc(1,reply_capacity,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT),.capacity=reply_capacity};
    if (packet && cfg.paired && cfg.host[0] && cfg.port > 0) {
        if(binary_image) {
            memcpy(packet+28,"T5I1",4);for(unsigned i=0;i<4;i++)packet[32+i]=(uint32_t)metadata_length>>(i*8);
            memcpy(packet+36,clear,metadata_length);photo_tx_copy(packet+36+metadata_length,job->image,job->image_size);
            free(job->image);job->image=NULL;
        }
        esp_fill_random(packet, 12);
        uint8_t hash[32];int err;
        if(image_upload) {
            err=photo_tx_encrypt(cfg.key,packet,(const uint8_t *)nonce,
                binary_image?packet+28:(const uint8_t *)clear,len,packet+28,packet+12);
            if(!err)err=photo_tx_sha256(packet,len+28,hash);
        }else {
            mbedtls_gcm_context ctx;mbedtls_gcm_init(&ctx);
            err=mbedtls_gcm_setkey(&ctx,MBEDTLS_CIPHER_ID_AES,cfg.key,256);
            if(!err)err=mbedtls_gcm_crypt_and_tag(&ctx,MBEDTLS_GCM_ENCRYPT,len,packet,12,
                (const uint8_t *)nonce,32,(const uint8_t *)clear,packet+28,16,packet+12);
            mbedtls_gcm_free(&ctx);if(!err)mbedtls_sha256(packet,len+28,hash,0);
        }
        if(image_upload&&err)status=-1010;
        if (!err) {
            for (int i = 0; i < 32; i++) sprintf(digest + i * 2, "%02x", hash[i]);
            snprintf(auth, sizeof(auth), "POST|%s|%s|%s", cfg.id, nonce, digest);
            device_proof(auth, proof);
            snprintf(url, sizeof(url), "http://%s:%d/tab5/v1/codex/%s", cfg.host, cfg.port,image_upload?"image":job->read_only?"read":"turn");
            esp_http_client_config_t options = {.url = url, .timeout_ms = job->read_only?35000:60000, .disable_auto_redirect = true,
                .event_handler=collect_reply,.user_data=&response};
            esp_http_client_handle_t client = esp_http_client_init(&options);
            if (client) {
                esp_http_client_set_method(client, HTTP_METHOD_POST);
                esp_http_client_set_header(client, "Content-Type", "application/octet-stream");
                esp_http_client_set_header(client, "X-AIBot-Device", cfg.id);
                esp_http_client_set_header(client, "X-AIBot-Nonce", nonce);
                esp_http_client_set_header(client, "X-AIBot-Proof", proof);
                esp_http_client_set_post_field(client, (const char *)packet, len + 28);
                if(image_upload){photo_tx_phase(PHOTO_TX_EXCHANGE);photo_display_probe_mark(PHOTO_DISPLAY_PROBE_NETWORK);}
                if (esp_http_client_perform(client) == ESP_OK) status = esp_http_client_get_status_code(client);
                esp_http_client_cleanup(client);
            }
        }
    }
    cJSON *reply=job->transport!=LINK_WIFI?rpc_reply:response.body&&!response.overflow?cJSON_Parse(response.body):NULL;
    if(!strcmp(job->op,"read")&&status==200) {
        cJSON *encoded=cJSON_GetObjectItemCaseSensitive(reply,"encrypted");
        if(cJSON_IsString(encoded)) {
            size_t size=0,capacity=strlen(encoded->valuestring)*3/4+1;
            unsigned char *sealed=heap_caps_malloc(capacity,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
            unsigned char *plain=heap_caps_malloc(capacity+1,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
            cJSON *decoded=NULL;
            if(sealed&&plain&&mbedtls_base64_decode(sealed,capacity,&size,(unsigned char *)encoded->valuestring,strlen(encoded->valuestring))==0&&size>=28) {
                mbedtls_gcm_context ctx;mbedtls_gcm_init(&ctx);
                int err=mbedtls_gcm_setkey(&ctx,MBEDTLS_CIPHER_ID_AES,cfg.key,256);
                if(!err)err=mbedtls_gcm_auth_decrypt(&ctx,size-28,sealed,12,(const unsigned char *)nonce,32,sealed+12,16,sealed+28,plain);
                mbedtls_gcm_free(&ctx);
                if(!err){plain[size-28]=0;decoded=cJSON_Parse((char *)plain);}
            }
            free(sealed);free(plain);
            cJSON *task=cJSON_GetObjectItemCaseSensitive(decoded,"taskId"),*view=cJSON_GetObjectItemCaseSensitive(decoded,"replyView"),*body=cJSON_GetObjectItemCaseSensitive(decoded,"text");
            if(cJSON_IsString(task)&&!strcmp(task->valuestring,job->task_id)&&cJSON_IsObject(view)&&cJSON_IsString(body)) {
                cJSON_Delete(reply);reply=decoded;
                xSemaphoreTake(device_mutex,portMAX_DELAY);cJSON_Delete(result_view);result_view=cJSON_Duplicate(reply,true);xSemaphoreGive(device_mutex);
            }else{cJSON_Delete(decoded);status=502;}
        }else if(job->transport!=LINK_WIFI) {
            // rpc_call already authenticated/decrypted the complete response.
            cJSON *packed=cJSON_GetObjectItemCaseSensitive(reply,"packedReply");
            if(cJSON_IsTrue(packed)) {
                char *clear=device_unpack_json(reply,24576);
                cJSON *decoded=clear?cJSON_Parse(clear):NULL;free(clear);
                cJSON_Delete(reply);reply=decoded;
            }
            cJSON *format=cJSON_GetObjectItemCaseSensitive(reply,"rpcReplyVersion");
            cJSON *task=cJSON_GetObjectItemCaseSensitive(reply,"taskId"),*view=cJSON_GetObjectItemCaseSensitive(reply,"replyView"),*body=cJSON_GetObjectItemCaseSensitive(reply,"text");
            if(cJSON_IsNumber(format)&&format->valueint==1&&cJSON_IsString(task)&&!strcmp(task->valuestring,job->task_id)&&cJSON_IsObject(view)&&cJSON_IsString(body)) {
                xSemaphoreTake(device_mutex,portMAX_DELAY);cJSON_Delete(result_view);result_view=cJSON_Duplicate(reply,true);xSemaphoreGive(device_mutex);
            }else status=502;
        }else if(!reply)status=502;
    }
    cJSON *error=cJSON_GetObjectItemCaseSensitive(reply,"error");
    const char *reason=cJSON_IsString(error)?error->valuestring:"";
    if(!strcmp(job->op,"read")){cJSON *turn=cJSON_GetObjectItemCaseSensitive(reply,"turnId");xSemaphoreTake(device_mutex,portMAX_DELAY);snprintf(result_turn,sizeof(result_turn),"%s",cJSON_IsString(turn)?turn->valuestring:"");xSemaphoreGive(device_mutex);}
    if(image_upload){photo_tx_finish(status);upload_mark(job,upload_started,status);char message[96];if(status==200)snprintf(message,sizeof(message),"照片已添加（%s，%.1f 秒）",link_name(job->transport),(esp_timer_get_time()-upload_started)/1000000.0);else snprintf(message,sizeof(message),"图片未上传，请保留预览并重试");set_result(message,status);}
    else if(!strcmp(job->op,"draft-load")||!strcmp(job->op,"draft-save"))set_result(status==200?"草稿已同步":"草稿暂未同步",status);
    else if(!strcmp(job->op,"read"))set_result(status==200?"回复已刷新":status==404?"任务已变化，请重新选择":"回复暂时无法刷新，请稍后重试",status);
    else set_result(status == 202 ? "已发送，可查看会话动态" : status == 404 ? "任务已变化，请刷新后重选" :
        status == 400 ? "状态已变化，请等待电脑数据刷新" : status == 429 ? "发送太快，请稍后再试" :
        status == 409 ? (!strcmp(reason,"task_busy_or_state_unknown")?"任务正在运行，请刷新后追加":!strcmp(reason,"turn_changed")?"当前会话已变化，请刷新动态":!strcmp(reason,"task_waiting_for_input")?"请先在电脑处理待确认事项":"请求已处理，请先查看动态") : status == 401 ? "设备认证失败，请检查配对" :
        status == 503 ? (!strcmp(reason,"desktop_load_timeout")?"Codex 会话加载超时，草稿已保留":!strcmp(reason,"desktop_load_failed")?"无法打开 Codex，草稿已保留":!strcmp(reason,"desktop_owner_unavailable")?"请在电脑 Codex 打开该会话":!strcmp(reason,"desktop_version_unsupported")?"Codex 版本不兼容，请更新桥接":!strcmp(reason,"turn_start_failed")?"Codex 拒绝启动，文字已保留":"Codex 连接失败，文字已保留") : "发送结果不确定，请先在电脑端查看", status);
    cJSON_Delete(reply);
    free(response.body);
    if(wifi_bulk)wifi_performance_bulk(false);
    free(packet); free(clear); free(job->image); free(job);
    xSemaphoreTake(device_mutex, portMAX_DELAY); busy = false; xSemaphoreGive(device_mutex);
    vTaskDeleteWithCaps(NULL);
}
bool codex_remote_busy(void){xSemaphoreTake(device_mutex,portMAX_DELAY);bool value=busy;xSemaphoreGive(device_mutex);return value;}
static bool start_job(const char *op,const char *task_id,const char *message,const char *session,int64_t issued_at,int page,const char *request_id,const char *expected_turn,const char images[3][37],const uint8_t *image,size_t image_size,int turn_offset) {
    unsigned host_generation=device_host_epoch();int mode=device_get_config().mode;
    link_kind transport=image?rpc_image_link_select(session):!strcmp(op,"read")?rpc_read_link_select():rpc_link_select();
    bool read_only=!strcmp(op,"read")||!strcmp(op,"draft-load")||!strcmp(op,"receipt");
    if (!task_id || strlen(task_id) != 36 || !message || ((!strcmp(op,"send")||!strcmp(op,"steer"))&&!*message&&(!images||!images[0][0])) || strlen(message) > 2000 || image_size>2*1024*1024 ||
        !session || strlen(session) >= 40 || transport==LINK_NONE||host_generation!=device_host_epoch()||device_host_changing()) return false;
    xSemaphoreTake(device_mutex, portMAX_DELAY);
    if (busy||transport_benchmark_busy()) { xSemaphoreGive(device_mutex); return false; }
    busy = true; snprintf(result, sizeof(result), "%s",read_only?"正在读取回复…":"正在发送...");
    result_code = -1;
    cJSON_Delete(result_view);result_view=NULL;
    snprintf(result_task, sizeof(result_task), "%s", task_id);
    xSemaphoreGive(device_mutex);
    if(image&&image_size)photo_tx_begin();
    codex_job *job = calloc(1, sizeof(*job));
    if (job) {
        job->transport=transport;
        job->host_generation=host_generation;job->mode=mode;
        if(images)memcpy(job->images,images,sizeof(job->images));
        if(image&&image_size){job->image=heap_caps_malloc(image_size,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);if(job->image){photo_tx_copy(job->image,image,image_size);job->image_size=image_size;}}
        snprintf(job->task_id, sizeof(job->task_id), "%s", task_id);
        snprintf(job->message, sizeof(job->message), "%s", message);
        snprintf(job->session, sizeof(job->session), "%s", session);
        job->issued_at = issued_at;
        job->read_only=read_only;
        job->page=page;job->turn_offset=turn_offset;
        snprintf(job->op,sizeof(job->op),"%s",op);
        snprintf(job->request_id,sizeof(job->request_id),"%s",request_id?request_id:"");
        snprintf(job->expected_turn,sizeof(job->expected_turn),"%s",expected_turn?expected_turn:"");
    }
    if (!job || (image&&!job->image) || xTaskCreateWithCaps(send_job, "codex_request", 8192, job, 4, NULL,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT) != pdPASS) {
        if(job)free(job->image);
        free(job); xSemaphoreTake(device_mutex, portMAX_DELAY); busy = false; xSemaphoreGive(device_mutex);
        if(image)photo_tx_finish(0);
        set_result("无法启动发送任务", 0); return false;
    }
    return true;
}
bool codex_remote_submit(const char *id,const char *message,const char *session,int64_t issued_at) {return start_job("send",id,message,session,issued_at,-1,NULL,NULL,NULL,NULL,0,0);}
bool codex_remote_read(const char *id,const char *session,int64_t issued_at) {return start_job("read",id,"",session,issued_at,-1,NULL,NULL,NULL,NULL,0,0);}
bool codex_remote_read_page(const char *id,const char *session,int64_t issued_at,int page) {return start_job("read",id,"",session,issued_at,page,NULL,NULL,NULL,NULL,0,0);}
bool codex_remote_request(const char *op,const char *id,const char *message,const char *session,int64_t epoch,const char *request_id,const char *expected_turn) {return start_job(op,id,message,session,epoch,-1,request_id,expected_turn,NULL,NULL,0,0);}
bool codex_remote_with_images(const char *op,const char *id,const char *message,const char *session,int64_t epoch,const char *request_id,const char *expected_turn,const char images[3][37]) {return start_job(op,id,message,session,epoch,-1,request_id,expected_turn,images,NULL,0,0);}
bool codex_remote_upload(const char *id,const char *session,int64_t epoch,const char *image_id,const uint8_t *data,size_t size) {return start_job("image-upload",id,"",session,epoch,-1,image_id,NULL,NULL,data,size,0);}

bool codex_remote_read_turn(const char *id,const char *session,int64_t epoch,int page,const char *turn,int offset) {return start_job("read",id,"",session,epoch,page,NULL,turn,NULL,NULL,0,offset);}
