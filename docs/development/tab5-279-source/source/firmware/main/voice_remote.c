#include "voice_remote.h"
#include "agenda_chime.h"
#include "voice_control.h"
#include "voice_recovery.h"
#include "rpc_link.h"
#include "wifi_performance.h"
#include "transport_benchmark.h"
#include "flash_benchmark.h"
#include "ble_voice.h"
#include "voice_adpcm.h"
#include "device.h"
#include "bsp/m5stack_tab5.h"
#include "esp_codec_dev.h"
#include "esp_http_client.h"
#include "esp_timer.h"
#include "esp_random.h"
#include "freertos/queue.h"
#include "freertos/idf_additions.h"
#include "esp_heap_caps.h"
#include "gallery_remote.h"
#include "mbedtls/gcm.h"
#include "mbedtls/sha256.h"
#include "mbedtls/base64.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdatomic.h>
#include <math.h>

typedef struct {uint16_t size;int16_t samples[3200];} audio_chunk;
typedef struct {char session[40];int64_t epoch, tick;bool bluetooth,usb,force_tab5;} voice_job;
static voice_remote_state state;
static QueueHandle_t audio_queue;
static QueueHandle_t voice_jobs;
static TaskHandle_t capture_task_handle;
volatile int tab5_voice_start_error;
volatile unsigned tab5_voice_largest_internal;
volatile int tab5_voice_http_stage, tab5_voice_http_status, tab5_voice_request_ms, tab5_voice_last_seq;
volatile unsigned tab5_voice_capture_frames,tab5_voice_queue_peak;
volatile int64_t tab5_voice_capture_start_ms;
static atomic_int command;
static atomic_bool mic_stop, mic_done, mic_error;
static atomic_int mic_error_reason;
static esp_codec_dev_handle_t microphone;
static esp_codec_dev_handle_t notification_speaker;
static atomic_uint notification_requested;
static atomic_uint health_requested;
static atomic_uint preview_requested;
static atomic_uint timer_requested;
static atomic_uint agenda_requested;
extern const uint8_t reply_notice_start[] asm("_binary_reply_notice_pcm_start");
extern const uint8_t reply_notice_end[] asm("_binary_reply_notice_pcm_end");
extern const uint8_t health_notice_start[] asm("_binary_health_notice_pcm_start");
extern const uint8_t health_notice_end[] asm("_binary_health_notice_pcm_end");
void voice_remote_notify_reply(void) {
    atomic_store(&notification_requested,(unsigned)(esp_timer_get_time()/1000));
}
void voice_remote_notify_health(void) {
    unsigned now=(unsigned)(esp_timer_get_time()/1000);atomic_store(&health_requested,now?now:1u);
}
bool voice_remote_preview_sound(void) {
    voice_remote_state current;voice_remote_get(&current);
    if(!voice_jobs||current.busy||!device_sound_volume())return false;
    unsigned now=(unsigned)(esp_timer_get_time()/1000);
    atomic_store(&preview_requested,now?now:1u);
    return true;
}
void voice_remote_notify_timer(void) {
    unsigned now=(unsigned)(esp_timer_get_time()/1000);atomic_store(&timer_requested,now?now:1u);
}
void voice_remote_notify_agenda(void) {
    unsigned now=(unsigned)(esp_timer_get_time()/1000);atomic_store(&agenda_requested,now?now:1u);
}
static void reply_chime(bool spoken,bool health,bool agenda) {
    // The existing voice worker owns both playback and capture sessions. Never
    // open/change the shared I2S clock from the UI thread or during recording.
    int volume=device_sound_volume();if(!volume)return;
    if(bsp_set_speaker_enable(false)!=ESP_OK)return;
    if(!notification_speaker) {
        if(heap_caps_get_largest_free_block(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT)<16384) {
            printf("TAB5_REPLY_CHIME skipped=memory\n");return;
        }
        notification_speaker=bsp_audio_codec_speaker_init();
    }
    esp_codec_dev_sample_info_t format={.sample_rate=16000,.bits_per_sample=16,.channel=2};
    if(!notification_speaker||esp_codec_dev_open(notification_speaker,&format)!=ESP_CODEC_DEV_OK) {
        printf("TAB5_REPLY_CHIME failed=open\n");return;
    }
    int16_t pcm[640]={0};
    // Prime the codec and let the analog output settle while muted and with the
    // external amplifier off. write() queues DMA; it does not mean audio drained.
    bool previous_mute=false;
    int result=esp_codec_dev_get_out_mute(notification_speaker,&previous_mute);
    bool mute_known=result==ESP_CODEC_DEV_OK;
    if(result==ESP_CODEC_DEV_OK)result=esp_codec_dev_set_out_mute(notification_speaker,true);
    if(result==ESP_CODEC_DEV_OK)result=esp_codec_dev_set_out_vol(notification_speaker,volume);
    for(int i=0;i<4&&result==ESP_CODEC_DEV_OK;i++)result=esp_codec_dev_write(notification_speaker,pcm,sizeof(pcm));
    if(result==ESP_CODEC_DEV_OK)vTaskDelay(pdMS_TO_TICKS(20));
    if(result==ESP_CODEC_DEV_OK&&bsp_set_speaker_enable(true)!=ESP_OK)result=ESP_FAIL;
    if(result==ESP_CODEC_DEV_OK) {
        vTaskDelay(pdMS_TO_TICKS(20));
        result=esp_codec_dev_set_out_mute(notification_speaker,false);
    }
    if(result!=ESP_CODEC_DEV_OK) {
        esp_codec_dev_set_out_mute(notification_speaker,true);
        bsp_set_speaker_enable(false);
        if(mute_known)esp_codec_dev_set_out_mute(notification_speaker,previous_mute);
        esp_codec_dev_close(notification_speaker);return;
    }
    // Two soft notes with attack/release ramps and silence between/after them.
    for(int block=0;result==ESP_CODEC_DEV_OK&&!health&&block<(agenda?AGENDA_CHIME_BLOCKS:24);block++) {
        if(uxQueueMessagesWaiting(voice_jobs)>0||!device_sound_volume())break;
        int note=block<9?0:block>=12&&block<22?1:-1;
        int start=note==0?0:12,end=note==0?9:22;
        for(int i=0;i<320;i++) {
            float envelope=note<0?0.0f:block==start?(float)i/320:block==end-1?(320.0f-i)/320:1.0f;
            // Keep headroom below full scale; the previous 2800 peak was
            // already -21 dB before the user's codec-volume attenuation.
            float frequency=note==0?660.0f:880.0f;
            int16_t sample=agenda?agenda_chime_sample((unsigned)(block*320+i)):
                (int16_t)(9000*envelope*sinf(6.2831853f*frequency*(block*320+i)/16000));
            pcm[i*2]=sample;pcm[i*2+1]=sample;
        }
        result=esp_codec_dev_write(notification_speaker,pcm,sizeof(pcm));
        if(result!=ESP_CODEC_DEV_OK)break;
    }
    // Offline Codex speech or the approved synthesized cuckoo. Capture wins.
    const uint8_t *notice=health?health_notice_start:reply_notice_start;
    size_t notice_size=health?(size_t)(health_notice_end-health_notice_start):(size_t)(reply_notice_end-reply_notice_start);
    for(size_t offset=0;(spoken||health)&&result==ESP_CODEC_DEV_OK&&offset<notice_size;offset+=640) {
        if(uxQueueMessagesWaiting(voice_jobs)>0||!device_sound_volume())break;
        for(int i=0;i<320;i++) {
            size_t pos=offset+i*2;int16_t sample=0;
            if(pos+1<notice_size)sample=(int16_t)(notice[pos]|(notice[pos+1]<<8));
            pcm[i*2]=sample;pcm[i*2+1]=sample;
        }
        result=esp_codec_dev_write(notification_speaker,pcm,sizeof(pcm));
    }
    // Two silent blocks plus one complete DMA window put the actual DAC at
    // zero before muting/turning off the amplifier. Preserve all speech samples
    // and the user's volume; this is an output-lifecycle change, not attenuation.
    memset(pcm,0,sizeof(pcm));
    for(int i=0;i<2&&result==ESP_CODEC_DEV_OK;i++)result=esp_codec_dev_write(notification_speaker,pcm,sizeof(pcm));
    if(result==ESP_CODEC_DEV_OK)vTaskDelay(pdMS_TO_TICKS(20));
    esp_codec_dev_set_out_mute(notification_speaker,true);
    vTaskDelay(pdMS_TO_TICKS(20));
    bsp_set_speaker_enable(false);
    // The recording-ready tone shares this codec. Restore logical mute while
    // the amplifier is off so the next session does not inherit our soft mute.
    if(mute_known)esp_codec_dev_set_out_mute(notification_speaker,previous_mute);
    esp_codec_dev_close(notification_speaker);
    printf("TAB5_%s_CHIME result=%d\n",health?"HEALTH":agenda?"AGENDA":"REPLY",result);
}

// Prepare TX before opening RX, with the same 64 bits/frame as the four-channel
// microphone (4 x 16). At readiness we only write; no clocks or GPIO change
// during capture. Close it after the capture worker has stopped.
static bool ready_speaker_open(void) {
    if(bsp_set_speaker_enable(false)!=ESP_OK)return false;
    if(!notification_speaker) {
        if(heap_caps_get_largest_free_block(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT)<16384)return false;
        notification_speaker=bsp_audio_codec_speaker_init();
    }
    esp_codec_dev_sample_info_t format={.sample_rate=16000,.bits_per_sample=32,.channel=2};
    bool opened=notification_speaker&&esp_codec_dev_open(notification_speaker,&format)==ESP_CODEC_DEV_OK;
    if(opened)esp_codec_dev_set_out_vol(notification_speaker,device_sound_volume());
    return opened;
}
static void ready_chime(void) {
    int volume=device_sound_volume();if(!volume)return;
    esp_codec_dev_set_out_vol(notification_speaker,volume);
    int32_t pcm[320]={0};int result=ESP_CODEC_DEV_OK;
    for(int i=0;i<4&&result==ESP_CODEC_DEV_OK;i++)result=esp_codec_dev_write(notification_speaker,pcm,sizeof(pcm));
    if(atomic_load(&mic_stop)||result!=ESP_CODEC_DEV_OK||bsp_set_speaker_enable(true)!=ESP_OK) {
        bsp_set_speaker_enable(false);return;
    }
    // 120 ms soft tone, then 20 ms silence. Keep capturing throughout: no lost
    // first syllable if the user starts speaking as soon as the cue appears.
    for(int block=0;block<14&&!atomic_load(&mic_stop);block++) {
        for(int i=0;i<160;i++) {
            float envelope=block>=12?0.0f:block==0?(float)i/160:block==11?(160.0f-i)/160:1.0f;
            int32_t sample=(int32_t)(100000000*envelope*sinf(6.2831853f*880.0f*(block*160+i)/16000));
            pcm[i*2]=sample;pcm[i*2+1]=sample;
        }
        result=esp_codec_dev_write(notification_speaker,pcm,sizeof(pcm));
        if(result!=ESP_CODEC_DEV_OK)break;
    }
    bsp_set_speaker_enable(false);
    printf("TAB5_VOICE_READY_CHIME result=%d\n",result);
}

int voice_remote_queue_depth(void) {
    return audio_queue ? (int)uxQueueMessagesWaiting(audio_queue) : 0;
}
int voice_remote_mic_error(void) {
    return atomic_load(&mic_error_reason);
}

void voice_remote_get(voice_remote_state *out) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);*out=state;xSemaphoreGive(device_mutex);
}
static void message(const char *phase,const char *value) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    state.ready=false;
    snprintf(state.phase,sizeof(state.phase),"%s",phase);snprintf(state.message,sizeof(state.message),"%s",value);
    xSemaphoreGive(device_mutex);
}
void voice_remote_command(int cmd) {
    if(cmd==1||cmd==2)atomic_store(&mic_stop,true);
    voice_command_post(&command,cmd);
    if(cmd==1||cmd==2) {
        xSemaphoreTake(device_mutex,portMAX_DELAY);
        if(state.busy) {
            state.ready=false;state.level=0;state.silent=false;
            snprintf(state.phase,sizeof(state.phase),"draining");
            snprintf(state.message,sizeof(state.message),"%s",atomic_load(&command)==2?"正在取消录音…":"正在结束收音…");
        }
        xSemaphoreGive(device_mutex);
    }
}
static const char *str(cJSON *root,const char *key) {
    cJSON *v=cJSON_GetObjectItemCaseSensitive(root,key);return cJSON_IsString(v)?v->valuestring:"";
}
static cJSON *request(cJSON *root,esp_http_client_handle_t client,bool usb) {
    if(usb){int status=0;char nonce[33];cJSON_AddStringToObject(root,"kind","voice");cJSON *reply=rpc_call(LINK_USB,root,&status,nonce);if(status==200)return reply;cJSON_Delete(reply);return NULL;}
    device_config cfg=device_get_config();char nonce[33],proof[65],digest[65],auth[180];
    if(!cfg.paired||(client?(!wifi_connected||!cfg.host[0]):!ble_voice_ready()))return NULL;
    char *clear=cJSON_PrintUnformatted(root);if(!clear)return NULL;
    size_t len=strlen(clear);uint8_t *packet=len<16000?malloc(len+28):NULL;
    cJSON *reply=NULL;
    if(packet) {
        device_nonce(nonce);esp_fill_random(packet,12);
        mbedtls_gcm_context ctx;mbedtls_gcm_init(&ctx);
        int err=mbedtls_gcm_setkey(&ctx,MBEDTLS_CIPHER_ID_AES,cfg.key,256);
        if(!err)err=mbedtls_gcm_crypt_and_tag(&ctx,MBEDTLS_GCM_ENCRYPT,len,packet,12,(uint8_t*)nonce,32,(uint8_t*)clear,packet+28,16,packet+12);
        mbedtls_gcm_free(&ctx);
        if(!err) {
            uint8_t hash[32];mbedtls_sha256(packet,len+28,hash,0);
            for(int i=0;i<32;i++)sprintf(digest+i*2,"%02x",hash[i]);
            snprintf(auth,sizeof(auth),"POST|/tab5/v1/voice|%s|%s|%s",cfg.id,nonce,digest);device_proof(auth,proof);
            if(!client) {
                size_t size=0;uint8_t *body=ble_voice_exchange(nonce,proof,packet,len+28,&size);
                char *decoded=body?device_decrypt(body,size,nonce):NULL;
                if(decoded){reply=cJSON_Parse(decoded);free(decoded);}
                free(body);free(packet);free(clear);return reply;
            }
            {
                esp_http_client_set_header(client,"X-AIBot-Device",cfg.id);
                esp_http_client_set_header(client,"X-AIBot-Nonce",nonce);
                esp_http_client_set_header(client,"X-AIBot-Proof",proof);
                if(esp_http_client_open(client,len+28)==ESP_OK) {
                    tab5_voice_http_stage=2;
                    int sent=0;
                    while(sent<len+28){int n=esp_http_client_write(client,(char*)packet+sent,len+28-sent);if(n<=0)break;sent+=n;}
                    if(sent==len+28)tab5_voice_http_stage=3;
                    int64_t size=sent==len+28?esp_http_client_fetch_headers(client):-1;
                    tab5_voice_http_status=esp_http_client_get_status_code(client);
                    if(size>=0)tab5_voice_http_stage=4;
                    if(esp_http_client_get_status_code(client)==200&&size>=28&&size<=8192) {
                        uint8_t *body=malloc(size);int used=0;
                        if(body) {
                            while(used<size){int n=esp_http_client_read(client,(char*)body+used,size-used);if(n<=0)break;used+=n;}
                            if(used==size){
                                tab5_voice_http_stage=5;
                                char *decoded=device_decrypt(body,size,nonce);
                                if(decoded){reply=cJSON_Parse(decoded);free(decoded);}
                                if(reply)tab5_voice_http_stage=0;
                            }
                            free(body);
                        }
                    }
                }
            }
        }
    }
    free(packet);free(clear);return reply;
}
static void capture_session(void) {
    audio_chunk *chunk=heap_caps_malloc(sizeof(*chunk),MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    int16_t *raw=malloc(320*4*sizeof(int16_t));
    if(chunk)memset(chunk,0,sizeof(*chunk));
    if(!chunk||!raw){atomic_store(&mic_error_reason,1);atomic_store(&mic_error,true);free(chunk);free(raw);atomic_store(&mic_done,true);return;}
    printf("TAB5_MIC_HEAP dma_free=%u dma_largest=%u internal_free=%u\n",
        (unsigned)heap_caps_get_free_size(MALLOC_CAP_DMA|MALLOC_CAP_8BIT),
        (unsigned)heap_caps_get_largest_free_block(MALLOC_CAP_DMA|MALLOC_CAP_8BIT),
        (unsigned)heap_caps_get_free_size(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT));
    if(!microphone)microphone=bsp_audio_codec_microphone_init();
    esp_codec_dev_sample_info_t format={.sample_rate=16000,.bits_per_sample=16,.channel=4};
    bool opened=microphone&&esp_codec_dev_open(microphone,&format)==ESP_CODEC_DEV_OK;
    printf("TAB5_MIC_OPEN opened=%d dma_free=%u dma_largest=%u\n",opened,
        (unsigned)heap_caps_get_free_size(MALLOC_CAP_DMA|MALLOC_CAP_8BIT),
        (unsigned)heap_caps_get_largest_free_block(MALLOC_CAP_DMA|MALLOC_CAP_8BIT));
    if(!opened){atomic_store(&mic_error_reason,2);atomic_store(&mic_error,true);}
    if(opened)esp_codec_dev_set_in_gain(microphone,24.0f);
    tab5_voice_capture_start_ms=esp_timer_get_time()/1000;
    while(!atomic_load(&mic_stop)&&!atomic_load(&mic_error)) {
        if(esp_codec_dev_read(microphone,raw,320*4*sizeof(int16_t))!=ESP_CODEC_DEV_OK){atomic_store(&mic_error_reason,3);atomic_store(&mic_error,true);break;}
        tab5_voice_capture_frames++;
        // Official demo TDM order: MIC-L, AEC, MIC-R, MIC-HP. Use MIC-L.
        for(int i=0;i<320;i++)chunk->samples[chunk->size/2+i]=raw[i*4];
        chunk->size+=640;
        if(chunk->size==6400) {
            if(xQueueSend(audio_queue,chunk,0)!=pdTRUE){atomic_store(&mic_error_reason,4);atomic_store(&mic_error,true);break;}
            unsigned queued=uxQueueMessagesWaiting(audio_queue);
            if(queued>tab5_voice_queue_peak)tab5_voice_queue_peak=queued;
            chunk->size=0;
        }
    }
    if(chunk&&chunk->size&&!atomic_load(&mic_error))if(xQueueSend(audio_queue,chunk,0)!=pdTRUE){atomic_store(&mic_error_reason,4);atomic_store(&mic_error,true);}
    if(atomic_load(&mic_error))printf("TAB5_MIC_ERROR reason=%d queued=%u\n",atomic_load(&mic_error_reason),audio_queue?(unsigned)uxQueueMessagesWaiting(audio_queue):0);
    if(opened)esp_codec_dev_close(microphone);
    free(chunk);free(raw);atomic_store(&mic_done,true);
}
static void capture_task(void *unused) {
    (void)unused;
    for(;;) {
        ulTaskNotifyTake(pdTRUE,portMAX_DELAY);
        capture_session();
    }
}
typedef struct {voice_job *job;esp_http_client_handle_t client;} recovery_context;
static int64_t recovery_now(void *ctx){(void)ctx;return esp_timer_get_time()/1000;}
static void recovery_wait(void *ctx,unsigned ms){(void)ctx;vTaskDelay(pdMS_TO_TICKS(ms));}
static bool recovery_cancelled(void *ctx){(void)ctx;return voice_command_take(&command,false)==2;}
static bool recovery_ready(void *ctx) {
    recovery_context *r=ctx;
    // Stay on the original channel, even in auto mode. Fixed USB must never
    // retrieve over Wi-Fi/BLE behind the user's back.
    if(r->job->usb)return rpc_link_select()==LINK_USB;
    if(r->job->bluetooth)return ble_voice_ready();
    return wifi_connected;
}
static cJSON *recovery_request(void *ctx,const char *op,const char *voice_id) {
    recovery_context *r=ctx;voice_job *job=r->job;
    cJSON *req=cJSON_CreateObject();if(!req)return NULL;
    cJSON_AddStringToObject(req,"op",op);cJSON_AddStringToObject(req,"session",job->session);
    cJSON_AddStringToObject(req,"voiceId",voice_id);
    cJSON_AddNumberToObject(req,"issuedAt",job->epoch+esp_timer_get_time()/1000-job->tick);
    cJSON *reply=request(req,r->client,job->usb);cJSON_Delete(req);return reply;
}
static void recovery_publish(void *ctx,const char *phase,const char *value,const char *text) {
    (void)ctx;xSemaphoreTake(device_mutex,portMAX_DELAY);
    state.ready=false;state.level=0;state.silent=false;state.can_insert=false;
    snprintf(state.phase,sizeof(state.phase),"%s",phase);snprintf(state.message,sizeof(state.message),"%s",value);
    if(text)snprintf(state.text,sizeof(state.text),"%s",text);
    xSemaphoreGive(device_mutex);
}
static void run_session(voice_job *job) {
    bool wifi_bulk=!job->bluetooth&&!job->usb;
    if(wifi_bulk)wifi_performance_bulk(true);
    bsp_set_speaker_enable(false);
    char voice_id[40]="",source[8]="",phase[20]="";
    bool first=true,started_mic=false,stopping=false,failed=false;
    bool speaker_prepared=false,speaker_open=false,audio_acknowledged=false,announced=false;
    int seq=0;int64_t last_poll=0;
    audio_chunk *chunk=heap_caps_malloc(sizeof(*chunk),MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    char *base64=heap_caps_malloc(8540,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    device_config cfg=device_get_config();char url[112];
    snprintf(url,sizeof(url),"http://%s:%d/tab5/v1/voice",cfg.host,cfg.port);
    // The bridge keeps this authenticated voice connection open for the
    // session, so repeated audio chunks do not accumulate TCP TIME_WAIT PCBs.
    esp_http_client_config_t opts={.url=url,.timeout_ms=12000,.disable_auto_redirect=true,.buffer_size=2048};
    esp_http_client_handle_t client=job->bluetooth||job->usb?NULL:esp_http_client_init(&opts);
    if(client){esp_http_client_set_method(client,HTTP_METHOD_POST);esp_http_client_set_header(client,"Content-Type","application/octet-stream");}
    if(!chunk||!base64||(!job->bluetooth&&!job->usb&&!client)){message("error","语音内存不足");failed=true;}
    while(!failed) {
        int cmd=voice_command_take(&command,first);
        if(first&&cmd==2){message("cancelled","已取消");break;}
        if(cmd==1)stopping=true;
        if(esp_timer_get_time()/1000-job->tick>70000){message("error","语音已超时，请检查电脑");break;}
        if(atomic_load(&mic_error)&&cmd!=2){
            int reason=atomic_load(&mic_error_reason);
            message("error",reason==4?"音频发送缓冲已满，请重新开始":reason==3?"TAB5 麦克风读取失败":reason==2?"TAB5 麦克风启动失败":"TAB5 收音内存不足");
            break;
        }
        bool have_audio=false;
        const char *op="poll";
        if(first)op="start";
        else if(cmd==2)op="cancel";
        else if(cmd==3&&!stopping)op="tab5";
        else if(!strcmp(phase,"recording")&&!strcmp(source,"tab5")&&audio_queue&&xQueueReceive(audio_queue,chunk,pdMS_TO_TICKS(40))==pdTRUE){op="audio";have_audio=true;}
        else if(stopping&&atomic_load(&mic_done)&&(!audio_queue||uxQueueMessagesWaiting(audio_queue)==0)){op="stop";stopping=false;}
        else if(esp_timer_get_time()/1000-last_poll<400){vTaskDelay(pdMS_TO_TICKS(20));continue;}
        cJSON *req=cJSON_CreateObject();
        cJSON_AddStringToObject(req,"op",op);cJSON_AddStringToObject(req,"session",job->session);
        cJSON_AddStringToObject(req,"voiceId",voice_id);
        cJSON_AddNumberToObject(req,"issuedAt",job->epoch+esp_timer_get_time()/1000-job->tick);
        if(first){cJSON_AddStringToObject(req,"taskId",state.task_id);if(job->force_tab5)cJSON_AddStringToObject(req,"source","tab5");}
        if(have_audio) {
            size_t size=0;
            if(job->bluetooth) {
                // Dictation profile: pair-average 16 kHz input to 8 kHz before
                // ADPCM. ~4 KiB/s leaves radio airtime for status and replies.
                unsigned samples=chunk->size/4;
                for(unsigned i=0;i<samples;i++)chunk->samples[i]=((int)chunk->samples[i*2]+chunk->samples[i*2+1])/2;
                uint8_t compressed[806];size_t encoded=voice_adpcm_encode(chunk->samples,samples,compressed);
                mbedtls_base64_encode((uint8_t*)base64,8540,&size,compressed,encoded);
                cJSON_AddStringToObject(req,"codec","ima-adpcm8k");
            } else {
                // The paired bridge already accepts 16kHz IMA ADPCM. Keep the
                // sample rate while reducing Wi-Fi/USB payload by about 75%.
                uint8_t compressed[1606];size_t encoded=voice_adpcm_encode(chunk->samples,chunk->size/2,compressed);
                mbedtls_base64_encode((uint8_t*)base64,8540,&size,compressed,encoded);
                cJSON_AddStringToObject(req,"codec","ima-adpcm");
            }
            base64[size]=0;
            cJSON_AddStringToObject(req,"pcm",base64);cJSON_AddNumberToObject(req,"seq",seq++);
        }
        tab5_voice_http_stage=1;tab5_voice_http_status=0;tab5_voice_last_seq=seq;
        int64_t request_begin=esp_timer_get_time();
        cJSON *reply=request(req,client,job->usb);cJSON_Delete(req);last_poll=esp_timer_get_time()/1000;
        tab5_voice_request_ms=(int)((esp_timer_get_time()-request_begin)/1000);
        if(have_audio&&((seq%10)==0||esp_timer_get_time()-request_begin>120000))
            printf("TAB5_VOICE_AUDIO seq=%d request_ms=%lld queued=%u ok=%d\n",seq,
                (long long)((esp_timer_get_time()-request_begin)/1000),
                audio_queue?(unsigned)uxQueueMessagesWaiting(audio_queue):0,reply!=NULL);
        if(!reply){
            atomic_store(&mic_stop,true);
            while(!atomic_load(&mic_done))vTaskDelay(pdMS_TO_TICKS(20));
            if(audio_queue)xQueueReset(audio_queue);
            bsp_set_speaker_enable(false);
            if(speaker_open){esp_codec_dev_close(notification_speaker);speaker_open=false;}
            if(cmd==2){recovery_publish(NULL,"cancelled","已取消","");break;}
            if(!voice_id[0]){message("error","语音连接中断，本段未完整保存，请重新录入");break;}
            recovery_context context={job,client};
            voice_recovery_io io={&context,recovery_now,recovery_wait,recovery_cancelled,recovery_ready,recovery_request,recovery_publish};
            char task_id[37];xSemaphoreTake(device_mutex,portMAX_DELAY);
            snprintf(task_id,sizeof(task_id),"%s",state.task_id);xSemaphoreGive(device_mutex);
            voice_recovery_run(&io,voice_id,task_id);break;
        }
        snprintf(voice_id,sizeof(voice_id),"%s",str(reply,"voiceId"));
        snprintf(source,sizeof(source),"%s",str(reply,"source"));snprintf(phase,sizeof(phase),"%s",str(reply,"state"));
        if(have_audio)audio_acknowledged=true;
        xSemaphoreTake(device_mutex,portMAX_DELAY);
        // Stop/cancel publishes its local intent under this same mutex. Test it
        // here so an older reply cannot restore recording after that intent.
        bool ready=voice_capture_ready(!strcmp(phase,"recording"),cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(reply,"ready")),
            !strcmp(source,"tab5"),audio_acknowledged,atomic_load(&mic_stop),atomic_load(&mic_error));
        state.ready=ready;
        cJSON *level=cJSON_GetObjectItemCaseSensitive(reply,"level");
        state.level=ready&&cJSON_IsNumber(level)?(level->valueint<0?0:level->valueint>100?100:level->valueint):0;
        state.silent=ready&&cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(reply,"silent"));
        state.can_insert=cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(reply,"canInsert"));
        snprintf(state.source,sizeof(state.source),"%s",source);
        snprintf(state.source_name,sizeof(state.source_name),"%s",str(reply,"sourceName"));
        snprintf(state.phase,sizeof(state.phase),"%s",!strcmp(phase,"recording")&&!ready?(atomic_load(&mic_stop)?"draining":"preparing"):phase);
        // A late recording response can still carry "ready to speak" after
        // local capture stopped. Keep the message consistent with our phase.
        snprintf(state.message,sizeof(state.message),"%s",!strcmp(state.phase,"draining")&&!strcmp(phase,"recording")?
            (cmd==2||atomic_load(&command)==2?"正在取消录音…":"正在结束收音…"):str(reply,"message"));
        snprintf(state.text,sizeof(state.text),"%s",str(reply,"text"));
        xSemaphoreGive(device_mutex);cJSON_Delete(reply);first=false;
        if(strcmp(phase,"recording"))atomic_store(&mic_stop,true);
        if(!strcmp(phase,"review")||!strcmp(phase,"error")||!strcmp(phase,"cancelled"))break;
        if(strcmp(phase,"recording")&&strcmp(phase,"recognizing")&&strcmp(phase,"draining")){message("error","电脑语音状态无效");break;}
        if(!speaker_prepared&&!atomic_load(&mic_stop)) {
            speaker_prepared=true;speaker_open=ready_speaker_open();
        }
        if(ready&&!announced&&!atomic_load(&mic_stop)) {
            announced=true;
            printf("TAB5_VOICE_READY source=%s startup_ms=%lld audio_seq=%d cue=%d\n",source,
                (long long)(esp_timer_get_time()/1000-job->tick),seq,speaker_open);
            if(speaker_open)ready_chime();
        }
        if(!strcmp(source,"tab5")&&!started_mic&&!atomic_load(&mic_stop)) {
            if(!audio_queue)audio_queue=xQueueCreateWithCaps(30,sizeof(audio_chunk),MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
            if(!audio_queue){message("error","TAB5 收音缓冲区不足");break;}
            atomic_store(&mic_done,false);started_mic=true;
            xTaskNotifyGive(capture_task_handle);
        }
    }
    atomic_store(&mic_stop,true);
    while(!atomic_load(&mic_done))vTaskDelay(pdMS_TO_TICKS(20));
    bsp_set_speaker_enable(false);
    if(speaker_open)esp_codec_dev_close(notification_speaker);
    if(audio_queue)xQueueReset(audio_queue);
    if(client)esp_http_client_cleanup(client);
    if(wifi_bulk)wifi_performance_bulk(false);
    free(chunk);free(base64);
    xSemaphoreTake(device_mutex,portMAX_DELAY);state.busy=false;state.ready=false;xSemaphoreGive(device_mutex);
}
static void run_task(void *unused) {
    (void)unused;
    voice_job job;
    for(;;) {
        if(xQueueReceive(voice_jobs,&job,pdMS_TO_TICKS(250))==pdTRUE) {
            atomic_store(&preview_requested,0);run_session(&job);
        }
        else {
            unsigned requested=atomic_exchange(&notification_requested,0);
            unsigned preview=atomic_exchange(&preview_requested,0);
            if(requested&&(unsigned)(esp_timer_get_time()/1000)-requested<30000)reply_chime(true,false,false);
            else if(preview&&(unsigned)(esp_timer_get_time()/1000)-preview<2000)reply_chime(false,false,false);
            unsigned health=atomic_exchange(&health_requested,0);
            if(health&&(unsigned)(esp_timer_get_time()/1000)-health<30000)reply_chime(false,true,false);
            unsigned timer=atomic_exchange(&timer_requested,0);
            if(timer&&(unsigned)(esp_timer_get_time()/1000)-timer<30000)reply_chime(false,false,false);
            unsigned agenda=atomic_exchange(&agenda_requested,0);
            if(agenda&&(unsigned)(esp_timer_get_time()/1000)-agenda<30000)reply_chime(false,false,true);
        }
    }
}
bool voice_remote_init(void) {
    if(voice_jobs)return true;
    voice_jobs=xQueueCreate(1,sizeof(voice_job));
    if(!voice_jobs)return false;
    if(xTaskCreateWithCaps(capture_task,"tab5_mic",4096,NULL,5,&capture_task_handle,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT)!=pdPASS) {
        vQueueDelete(voice_jobs);voice_jobs=NULL;return false;
    }
    if(xTaskCreateWithCaps(run_task,"tab5_voice",8192,NULL,4,NULL,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT)!=pdPASS) {
        vTaskDeleteWithCaps(capture_task_handle);capture_task_handle=NULL;
        vQueueDelete(voice_jobs);voice_jobs=NULL;return false;
    }
    return true;
}
static bool start_session(const char *task_id,const char *session,int64_t epoch_ms,bool force_tab5) {
    tab5_voice_start_error=0;
    tab5_voice_largest_internal=heap_caps_get_largest_free_block(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT);
    if(!task_id||strlen(task_id)!=36||!session||strlen(session)>=40){tab5_voice_start_error=1;return false;}
    if(!voice_jobs){tab5_voice_start_error=4;return false;}
    bool ble_ready=ble_voice_ready();
    bool usb_ready=rpc_link_select()==LINK_USB;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    if(state.busy||transport_benchmark_busy()||flash_benchmark_busy()){tab5_voice_start_error=2;xSemaphoreGive(device_mutex);return false;}
    int transport=voice_transport_select_with_usb(wifi_connected,device_links.received_ms[LINK_WIFI],esp_timer_get_time()/1000,ble_ready,device_links.mode,usb_ready);
    if(transport<0){tab5_voice_start_error=1;xSemaphoreGive(device_mutex);return false;}
    voice_job job={.force_tab5=force_tab5};
    job.bluetooth=transport==LINK_BLE;
    job.usb=transport==LINK_USB;
    unsigned generation=state.generation+1;memset(&state,0,sizeof(state));state.generation=generation;state.busy=true;
    snprintf(state.task_id,sizeof(state.task_id),"%s",task_id);snprintf(state.phase,sizeof(state.phase),"preparing");
    snprintf(state.message,sizeof(state.message),"正在准备，请稍候…");
    snprintf(job.session,sizeof(job.session),"%s",session);job.epoch=epoch_ms;job.tick=esp_timer_get_time()/1000;
    tab5_voice_http_stage=0;tab5_voice_http_status=0;tab5_voice_request_ms=0;tab5_voice_last_seq=0;
    tab5_voice_capture_frames=0;tab5_voice_queue_peak=0;tab5_voice_capture_start_ms=0;
    atomic_store(&command,0);atomic_store(&mic_stop,false);atomic_store(&mic_done,true);atomic_store(&mic_error,false);atomic_store(&mic_error_reason,0);
    xSemaphoreGive(device_mutex);
    gallery_cache_reclaim();
    if(xQueueSend(voice_jobs,&job,0)!=pdTRUE){tab5_voice_start_error=4;xSemaphoreTake(device_mutex,portMAX_DELAY);state.busy=false;xSemaphoreGive(device_mutex);return false;}
    return true;
}
bool voice_remote_start(const char *task_id,const char *session,int64_t epoch_ms){return start_session(task_id,session,epoch_ms,false);}
bool voice_remote_start_tab5(const char *owner,const char *session,int64_t epoch_ms){return start_session(owner,session,epoch_ms,true);}
