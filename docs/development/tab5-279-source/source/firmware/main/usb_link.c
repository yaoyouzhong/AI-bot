#include "device.h"
#include "rpc_link.h"
#include "esp_heap_caps.h"
#include "transport_benchmark.h"
#include "flash_benchmark.h"
#include "wifi_performance.h"
#include "wifi_probe.h"
#include "wifi_status.h"
#include "codex_remote.h"
#include "mbedtls/base64.h"
#include "assets.h"
#include "camera_preview.h"
#include "photo_attachment.h"
#include "display_diagnostics.h"
#include "crash_record.h"
#include "ota_update.h"
#include "art_orientation.h"
#include "gallery_remote.h"
extern volatile unsigned tab5_display_rotate_us,tab5_display_rotate_peak_us,tab5_display_flush_count,tab5_display_flush_errors;
extern volatile int tab5_ui_page,tab5_saver_active;
extern volatile unsigned tab5_codex_notice_state;
extern volatile int tab5_ui_data_age_ms,tab5_ui_task_selected,tab5_ui_session_ready,tab5_ui_voice_check,tab5_ui_voice_age_ms;
extern volatile unsigned tab5_pc_wakes,tab5_backlight_changes,tab5_lcd_underruns;
extern volatile int tab5_backlight_percent;
extern volatile int tab5_voice_http_stage,tab5_voice_http_status,tab5_voice_request_ms,tab5_voice_last_seq;
extern unsigned tab5_ui_age_ms(void);
#include "esp_system.h"
#include "esp_app_desc.h"
#include "esp_ota_ops.h"
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <stdatomic.h>
#include "tinyusb.h"
#include "tinyusb_default_config.h"
#include "tinyusb_cdc_acm.h"
#include "usb_task_policy.h"
#include "usb_receive_profile.h"
#include "usb_bulk_receive.h"
#include "wireless_boot.h"
#include "soc/lp_system_struct.h"
#include "esp_timer.h"

enum {ITF_CDC,ITF_CDC_DATA,ITF_HID,ITF_COUNT};
static atomic_uint cdc_epoch;
#include "usb_keyboard.inc"
static TaskHandle_t cdc_task;
static void usb_keyboard_bus_event(tinyusb_event_t *event,void *arg){
    (void)arg;
    // Never take application locks in TinyUSB callbacks. Invalidate even a
    // detach/attach shorter than the worker poll interval.
    if(event->id==TINYUSB_EVENT_ATTACHED||event->id==TINYUSB_EVENT_DETACHED)atomic_fetch_add(&cdc_epoch,1);
}

static void usb_cdc_rx(int itf,cdcacm_event_t *event) {
    (void)event;
    // A pending bulk DMA can retain an old prefix across DTR close. The new
    // bridge sends a separate short newline before discovery; discard that
    // stale completion before it can wake the parser, then rearm legacy64.
    if(itf==TINYUSB_CDC_ACM_0&&usb_bulk_receive_discard()){tud_cdc_read_flush();return;}
    // TinyUSB task callback, not ISR: wake the lower-priority receiver on
    // the same core. Never parse or acquire application locks here.
    if(itf==TINYUSB_CDC_ACM_0&&cdc_task){usb_rx_notify((unsigned)esp_timer_get_time());xTaskNotifyGive(cdc_task);}
}
// Only the CDC receiver owns this value. A reply belongs to the generation
// in which its command was read, even if close/open happens before dispatch.
static unsigned cdc_receive_epoch;
static atomic_uint cdc_rx_bytes,cdc_json_lines,cdc_overflows,cdc_binary_expired,cdc_tx_incomplete;
static void usb_cdc_line_state(int itf,cdcacm_event_t *event) {
    // Closing the desktop serial port lowers DTR without unplugging USB.
    // Remember the edge even when close/open completes between task polls.
    if(itf==TINYUSB_CDC_ACM_0&&!event->line_state_changed_data.dtr) {
        usb_bulk_receive_close();
        atomic_fetch_add(&cdc_epoch,1);
        // This callback precedes the host's next open. A delayed receiver-side
        // flush can instead discard the first command of that new connection.
        // TinyUSB's stream FIFOs serialize their clear operations internally;
        // no application mutex or parser work is allowed in this callback.
        tud_cdc_read_flush();tud_cdc_write_clear();
    }
    if(cdc_task)xTaskNotifyGive(cdc_task);
}
bool tab5_usb_connected(void) {
    // TAB5 remains powered by its battery after unplugging. Without a VBUS
    // monitor the controller can retain its configuration while the bus is
    // suspended, so mounted alone is not evidence of a live USB connection.
    // A configured bus with DTR lowered has no open desktop serial session.
    return tud_ready()&&tud_cdc_connected();
}
static const uint8_t hid_report[]={TUD_HID_REPORT_DESC_KEYBOARD()};
static const uint8_t usb_configuration[]={
    TUD_CONFIG_DESCRIPTOR(1,ITF_COUNT,0,TUD_CONFIG_DESC_LEN+TUD_CDC_DESC_LEN+TUD_HID_DESC_LEN,0,100),
    TUD_CDC_DESCRIPTOR(ITF_CDC,4,0x81,8,0x02,0x82,64),
    TUD_HID_DESCRIPTOR(ITF_HID,5,HID_ITF_PROTOCOL_KEYBOARD,sizeof(hid_report),0x83,16,10)
};
static const tusb_desc_device_t usb_device={
    .bLength=sizeof(tusb_desc_device_t),.bDescriptorType=TUSB_DESC_DEVICE,
    .bcdUSB=0x0200,.bDeviceClass=TUSB_CLASS_MISC,
    .bDeviceSubClass=MISC_SUBCLASS_COMMON,.bDeviceProtocol=MISC_PROTOCOL_IAD,
    .bMaxPacketSize0=64,.idVendor=0x303A,.idProduct=0x4005,
    .bcdDevice=0x0100,.iManufacturer=1,.iProduct=2,.iSerialNumber=3,.bNumConfigurations=1
};
static char usb_serial[32];
static const char *usb_strings[]={
    (const char[]){0x09,0x04},"TAB5 development","TAB5 Control",usb_serial,"TAB5 CDC","TAB5 Keyboard"
};
static void usb_prepare_restart(void) {
    // esp_restart() resets the CPUs, not every USB peripheral. Make the host
    // observe a real disconnect before the bootloader/new app takes over.
    // Keep the driver alive until reset; deleting it while CDC is polling
    // would race the normal USB task.
    tud_disconnect();
    vTaskDelay(pdMS_TO_TICKS(200));
}
uint8_t const *tud_hid_descriptor_report_cb(uint8_t instance) {
    (void)instance;return hid_report;
}
uint16_t tud_hid_get_report_cb(uint8_t instance,uint8_t report_id,hid_report_type_t type,uint8_t *buffer,uint16_t reqlen) {
    (void)instance;(void)report_id;(void)type;(void)buffer;(void)reqlen;return 0;
}
void tud_hid_set_report_cb(uint8_t instance,uint8_t report_id,hid_report_type_t type,uint8_t const *buffer,uint16_t bufsize) {
    if(instance==0&&report_id==0&&type==HID_REPORT_TYPE_OUTPUT&&buffer&&bufsize==1)atomic_store(&uk_leds,buffer[0]);
}
static bool usb_write(const char *data,size_t size) {
    // Flash operations can briefly stall CDC. A large binary frame must either
    // finish within the bridge's four-second bound or poison the stream.
    unsigned epoch=cdc_receive_epoch;
    size_t sent=0;int64_t deadline=esp_timer_get_time()+3000000;
    while(sent<size&&tab5_usb_connected()&&atomic_load(&cdc_epoch)==epoch&&esp_timer_get_time()<deadline) {
        size_t n=tinyusb_cdcacm_write_queue(TINYUSB_CDC_ACM_0,(const uint8_t*)data+sent,size-sent);
        sent+=n;
        tinyusb_cdcacm_write_flush(TINYUSB_CDC_ACM_0,pdMS_TO_TICKS(20));
        if(!n)vTaskDelay(pdMS_TO_TICKS(1));
    }
    if(sent<size){atomic_fetch_add(&cdc_tx_incomplete,1);printf("TAB5_USB_TX_INCOMPLETE sent=%u total=%u\n",(unsigned)sent,(unsigned)size);}
    return sent==size;
}
static void write_ota_diagnostic(void) {
    const esp_partition_t *running=esp_ota_get_running_partition();
    esp_ota_img_states_t state=ESP_OTA_IMG_UNDEFINED;
    esp_err_t status=running?esp_ota_get_state_partition(running,&state):ESP_ERR_NOT_FOUND;
    char hash[65],out[448];
    const esp_app_desc_t *description=esp_app_get_description();
    for(int i=0;i<32;i++)snprintf(hash+i*2,3,"%02x",description->app_elf_sha256[i]);
    extern volatile unsigned tab5_glyph_hits,tab5_glyph_misses,tab5_fast_fills,tab5_fast_masks;
    int n=snprintf(out,sizeof(out),"@AIBOT {\"type\":\"tab5_ota_diagnostic\",\"firmware\":\"%s\",\"partition\":\"%s\",\"address\":%lu,\"state\":%d,\"stateError\":%d,\"elfSha256\":\"%s\",\"glyphHits\":%u,\"glyphMisses\":%u,\"fastFills\":%u,\"fastMasks\":%u}\n",
        esp_app_get_description()->version,running?running->label:"none",running?(unsigned long)running->address:0,
        (int)state,(int)status,hash,tab5_glyph_hits,tab5_glyph_misses,tab5_fast_fills,tab5_fast_masks);
    if(n>0&&(size_t)n<sizeof(out))usb_write(out,n);
}
static void usb_rpc(cJSON *root) {
    device_config cfg=device_get_config();
    const char *id=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(root,"deviceId"));
    if(!cfg.paired||!id||strcmp(id,cfg.id))return;
    // Heap allocation avoids overflowing the CDC task's 8 KiB stack.
    uint8_t *bytes=malloc(16393);unsigned char *encoded=malloc(22000);size_t n=0,written=0;
    if(!bytes||!encoded){free(bytes);free(encoded);return;}
    const char *payload=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(root,"data"));
    bool ok;
    if(payload)ok=strlen(payload)<22000&&mbedtls_base64_decode(bytes,16393,&n,(uint8_t *)payload,strlen(payload))==0&&rpc_link_write(LINK_USB,bytes,n);
    else {
        cJSON *limit=cJSON_GetObjectItemCaseSensitive(root,"chunk");
        // Old bridges do not announce their read size; keep their 2 KiB frames.
        unsigned capacity=cJSON_IsNumber(limit)&&limit->valuedouble==16384?16392:2056;
        n=rpc_link_read(LINK_USB,bytes,capacity);ok=n>0;
    }
    cJSON *reply=cJSON_CreateObject();cJSON_AddStringToObject(reply,"type","tab5_rpc");
    cJSON_AddStringToObject(reply,"deviceId",cfg.id);cJSON_AddBoolToObject(reply,"ok",ok);
    cJSON *tag=cJSON_GetObjectItemCaseSensitive(root,"tag");if(cJSON_IsString(tag))cJSON_AddStringToObject(reply,"tag",tag->valuestring);
    if(!payload&&ok&&mbedtls_base64_encode(encoded,22000,&written,bytes,n)==0){encoded[written]=0;cJSON_AddStringToObject(reply,"data",(char *)encoded);}
    char *json=cJSON_PrintUnformatted(reply);
    if(json){usb_write("@AIBOT ",7);usb_write(json,strlen(json));usb_write("\n",1);free(json);}
    cJSON_Delete(reply);free(bytes);free(encoded);
}
#include "usb_binary.h"
static void usb_binary_reply(usb_binary *b,bool reading,unsigned wait_ms) {
    unsigned n=reading?rpc_link_read_wait(LINK_USB,b->data,b->chunk+8,wait_ms):0;
    bool ok=reading?n>=8:rpc_link_write(LINK_USB,b->data,b->want);
    uint8_t header[16];usb_binary_header(header,b->tag,n,ok);
    bool sent=usb_write((char *)header,sizeof(header));
    if(sent&&n)sent=usb_write((char *)b->data,n);
    if(!sent)b->failed=true; // Never parse a new command into a truncated frame.
    b->used=b->want=0;usb_rx_enable(false);usb_bulk_receive_select(false);
}
static void usb_binary_start(usb_binary *b,cJSON *root) {
    device_config cfg=device_get_config();unsigned count,chunk,wait_ms;uint32_t tag;
    if(!cfg.paired||!usb_binary_command(root,cfg.id,&tag,&count,&chunk))return;
    if(!usb_binary_wait(root,count,&wait_ms))return;
    cJSON *batch=cJSON_GetObjectItemCaseSensitive(root,"rxBatch");
    if(batch&&(!cJSON_IsNumber(batch)||(batch->valuedouble!=0&&batch->valuedouble!=USB_BULK_RECEIVE_BYTES)))return;
    if(!b->data)b->data=heap_caps_malloc(USB_BINARY_MAX,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    if(!b->data){b->failed=true;uint8_t header[16];usb_binary_header(header,tag,0,false);usb_write((char *)header,16);return;}
    b->used=0;b->want=count;b->chunk=chunk;b->tag=tag;b->deadline=esp_timer_get_time()/1000+2500;
    usb_rx_enable(count!=0);
    usb_bulk_receive_select(count&&batch&&batch->valuedouble==USB_BULK_RECEIVE_BYTES);
    if(!count)usb_binary_reply(b,true,wait_ms);
}
static void write_crash_diagnostic(void) {
    // Only the CDC task calls this; keep diagnostic output off its 8 KiB stack.
    static char trace[4096],flash[1280],out[5504];crash_record_json(trace,sizeof(trace));
    flash_benchmark_json(flash,sizeof(flash));
    int n=snprintf(out,sizeof(out),"@AIBOT {\"type\":\"tab5_crash_diagnostic\",\"trace\":%s,\"flashBenchmark\":%s}\n",trace,flash);
    if(n>0&&(size_t)n<sizeof(out))usb_write(out,n);
}
// Preserve the complete legacy response, including binding and nonce. Numeric
// OTA performance is optional; never trade an acknowledgement for telemetry.
static size_t append_ota_performance(char *frame,size_t capacity,size_t used,const char *suffix) {
    if(!frame||!suffix||!suffix[0]||used>=capacity||frame[used])return used;
    size_t extra=strlen(suffix);
    if(extra>=capacity-used)return used;
    // otaDiag is parsed by Stop acknowledgement and must stay four fields.
    // displayDiag already has tagged Flash/runtime diagnostic sections.
    static const char key[]="\"displayDiag\":\"";
    char *value=strstr(frame,key);
    if(!value)return used;
    value+=sizeof(key)-1;
    char *end=strchr(value,'"');
    if(!end||end>frame+used)return used;
    size_t value_size=(size_t)(end-value);
    if(value_size>1024||extra>1024-value_size)return used;
    memmove(end+extra,end,used-(size_t)(end-frame)+1);
    memcpy(end,suffix,extra);
    return used+extra;
}
static void write_reply(const char *type,uint64_t seq) {
    device_config cfg=device_get_config();static char out[4096];int64_t now=esp_timer_get_time()/1000;
    xSemaphoreTake(device_mutex,portMAX_DELAY);link_kind active=link_state_active(&device_links,now);
    unsigned ready=assets_ready(),cached=assets_cached();char asset_ids[27];assets_ids(asset_ids);long long ages[3];for(int i=0;i<3;i++)ages[i]=device_links.received_ms[i]<0?-1:now-device_links.received_ms[i];xSemaphoreGive(device_mutex);
    // Only this CDC task calls write_reply; keep the probe off its 8 KiB stack.
    static char photo_diag[512];
    char camera_diag[128],display_diag[1024],upload_diag[192];camera_preview_diagnostics(camera_diag,sizeof(camera_diag));
    codex_remote_upload_diagnostics(upload_diag,sizeof(upload_diag));
    display_diagnostics_format(display_diag,sizeof(display_diag));
    photo_diagnostics(photo_diag,sizeof(photo_diag));
    char ota_diag[64];ota_update_diagnostics(ota_diag,sizeof(ota_diag));
    char camera_log[320];camera_preview_driver_error_hex(camera_log,sizeof(camera_log));
    char orientation_diag[80];art_orientation_diagnostics(orientation_diag,sizeof(orientation_diag));
    char gallery_diag[160]="",transfer_diag[96]="";
    char wifi_isolation[96];wifi_probe_diagnostics(wifi_isolation,sizeof(wifi_isolation));
#if TAB5_LOCAL_ART
    gallery_diagnostics(gallery_diag,sizeof(gallery_diag));
    gallery_transfer_diagnostics(transfer_diag,sizeof(transfer_diag));
#endif
    int n=snprintf(out,sizeof(out),"@AIBOT {\"version\":1,\"rpcVersion\":1,\"rpcUsbChunk\":16384,\"rpcUsbBinary\":1,\"rpcUsbBinaryChunk\":65526,\"usbRxBatch\":4096,\"telemetryVersion\":1,\"usbPacked\":1,\"usbDelta\":1,\"type\":\"%s\",\"deviceId\":\"%s\",\"firmware\":\"%s\",\"sequence\":%llu,\"active\":\"%s\",\"wifiConnected\":%s,\"bleReady\":%s,\"ageMs\":[%lld,%lld,%lld],\"bleConnected\":%s,\"bleConnectCount\":%u,\"bleDisconnectReason\":%d,\"assetsReady\":%u,\"assetsCached\":%u,\"assetIds\":\"%s\",\"uiPage\":%d,\"uptimeMs\":%lld,\"resetReason\":%d,\"saverActive\":%d,\"pcWakeCount\":%u,\"uiAgeMs\":%u,\"uiDataAgeMs\":%d,\"taskSelected\":%d,\"inputSessionReady\":%d,\"codexNotice\":%u,\"voiceCheck\":%d,\"voiceAgeMs\":%d,\"voiceStage\":%d,\"voiceHttp\":%d,\"voiceRequestMs\":%d,\"voiceSeq\":%d,\"backlightChanges\":%u,\"backlightPercent\":%d,\"lcdUnderruns\":%u,\"rotateUs\":%u,\"rotatePeakUs\":%u,\"flushCount\":%u,\"flushErrors\":%u,\"cameraDiag\":\"%s\",\"displayDiag\":\"%s\",\"photoDiag\":\"%s\",\"cameraLogHex\":\"%s\",\"photoUploadDiag\":\"%s\",\"otaDiag\":\"%s\",\"artOrientation\":\"%s\",\"galleryDiag\":\"%s\",\"wifiIsolation\":\"%s\"}\n",type,cfg.id,esp_app_get_description()->version,(unsigned long long)seq,link_name(active),wifi_connected?"true":"false",ble_available?"true":"false",ages[0],ages[1],ages[2],ble_connected?"true":"false",ble_connect_count,ble_disconnect_reason,ready,cached,asset_ids,tab5_ui_page,now,esp_reset_reason(),tab5_saver_active,tab5_pc_wakes,tab5_ui_age_ms(),tab5_ui_data_age_ms,tab5_ui_task_selected,tab5_ui_session_ready,tab5_codex_notice_state,tab5_ui_voice_check,tab5_ui_voice_age_ms,tab5_voice_http_stage,tab5_voice_http_status,tab5_voice_request_ms,tab5_voice_last_seq,tab5_backlight_changes,tab5_backlight_percent,tab5_lcd_underruns,tab5_display_rotate_us,tab5_display_rotate_peak_us,tab5_display_flush_count,tab5_display_flush_errors,camera_diag,display_diag,photo_diag,camera_log,upload_diag,ota_diag,orientation_diag,gallery_diag,wifi_isolation);
    if(n<0||(size_t)n>=sizeof(out))goto essential;
    char rpc_diag[160];rpc_failure_diagnostics(rpc_diag,sizeof(rpc_diag));
    char wifi_diag[192];tab5_wifi_numeric_diagnostics(wifi_diag,sizeof(wifi_diag));
    char wifi_recovery[96];tab5_wifi_recovery_diagnostics(wifi_recovery,sizeof(wifi_recovery));
    char wifi_http[128];tab5_wifi_http_diagnostics(wifi_http,sizeof(wifi_http));
    char wifi_path[224];tab5_wifi_path_diagnostics(wifi_path,sizeof(wifi_path));
    char boot_diag[64];tab5_wireless_boot_diagnostics(boot_diag,sizeof(boot_diag));
    unsigned unlock_failed=0,unlock_failed_at=0;unlock_failure_snapshot(&unlock_failed,&unlock_failed_at);
    int extra=snprintf(out+n-2,sizeof(out)-(size_t)n+2,",\"usbHidState\":%u,\"rpcUsbWait\":%u,\"rpcDiag\":\"%s\",\"wifiDiag\":\"%s\",\"wifiRecoveryDiag\":\"%s\",\"wifiHttpDiag\":\"%s\",\"wifiPathDiag\":\"%s\",\"wirelessBootDiag\":\"%s\",\"galleryTransferDiag\":\"%s\",\"usbDiag\":\"%u,%u,%u,%u,%u,%u\"}\n",usb_keyboard_diagnostic(),rpc_usb_wait_supported()?20u:0u,rpc_diag,wifi_diag,wifi_recovery,wifi_http,wifi_path,boot_diag,transfer_diag,
        atomic_load(&cdc_epoch),atomic_load(&cdc_rx_bytes),atomic_load(&cdc_json_lines),atomic_load(&cdc_overflows),atomic_load(&cdc_binary_expired),atomic_load(&cdc_tx_incomplete));
    if(extra<0||(size_t)extra>=sizeof(out)-(size_t)n+2)goto essential;
    n=n-2+extra;
    char failure_diagnostic[80];
    extra=snprintf(failure_diagnostic,sizeof(failure_diagnostic),",\"f\":\"%08X\",\"unlockFailureUptimeMs\":%u}\n",unlock_failed,unlock_failed_at);
    if(extra>0&&(size_t)extra<sizeof(failure_diagnostic)&&(size_t)(n-2+extra)<sizeof(out)) {
        memcpy(out+n-2,failure_diagnostic,(size_t)extra+1);n=n-2+extra;
    }
    char nonce[33],host_id[33],host_name[TRUSTED_HOST_NAME],binding[200];
    device_host_challenge(nonce);device_host_identity(host_id,host_name);
    extra=snprintf(binding,sizeof(binding),",\"hostBindingVersion\":1,\"hostNonce\":\"%s\",\"currentHostId\":\"%s\",\"usbHostAuthenticated\":%s}\n",nonce,host_id,device_host_usb_owned()?"true":"false");
    if(extra>0&&(size_t)extra<sizeof(binding)&&(size_t)(n-2+extra)<sizeof(out)){memcpy(out+n-2,binding,(size_t)extra+1);n=n-2+extra;}
    else goto essential;
    static char ota_performance[OTA_PERFORMANCE_SUFFIX_CAPACITY];
    ota_trace_performance_format(ota_performance,sizeof(ota_performance));
    n=(int)append_ota_performance(out,sizeof(out),(size_t)n,ota_performance);
    usb_write(out,n);return;
essential:
    // Optional counters can fill the legacy 4 KiB line. Still acknowledge the
    // accepted sequence and preserve discovery/authentication capabilities.
    // Omit uptime so hosts retain their last complete diagnostic sample.
    device_host_challenge(nonce);device_host_identity(host_id,host_name);
    unlock_failure_snapshot(&unlock_failed,&unlock_failed_at);
    n=snprintf(out,sizeof(out),"@AIBOT {\"version\":1,\"rpcVersion\":1,\"rpcUsbChunk\":16384,\"rpcUsbBinary\":1,\"rpcUsbBinaryChunk\":65526,\"usbRxBatch\":4096,\"telemetryVersion\":1,\"usbPacked\":1,\"usbDelta\":1,\"rpcUsbWait\":%u,\"usbHidState\":%u,\"f\":\"%08X\",\"unlockFailureUptimeMs\":%u,\"type\":\"%s\",\"deviceId\":\"%s\",\"firmware\":\"%s\",\"sequence\":%llu,\"active\":\"%s\",\"wifiConnected\":%s,\"bleReady\":%s,\"ageMs\":[%lld,%lld,%lld],\"bleConnected\":%s,\"assetsReady\":%u,\"assetsCached\":%u,\"assetIds\":\"%s\",\"hostBindingVersion\":1,\"hostNonce\":\"%s\",\"currentHostId\":\"%s\",\"usbHostAuthenticated\":%s}\n",
        rpc_usb_wait_supported()?20u:0u,usb_keyboard_diagnostic(),unlock_failed,unlock_failed_at,type,cfg.id,esp_app_get_description()->version,(unsigned long long)seq,link_name(active),
        wifi_connected?"true":"false",ble_available?"true":"false",ages[0],ages[1],ages[2],ble_connected?"true":"false",ready,cached,asset_ids,nonce,host_id,device_host_usb_owned()?"true":"false");
    if(n>0&&(size_t)n<sizeof(out))usb_write(out,n);
}
// CDC task context only: TinyUSB callbacks must not acquire application locks.
static bool usb_link_watch(bool *was_ready) {
    bool ready=tab5_usb_connected();
    if(*was_ready&&!ready) {
        xSemaphoreTake(device_mutex,portMAX_DELAY);
        link_state_disconnect(&device_links,LINK_USB);
        xSemaphoreGive(device_mutex);
        rpc_link_reset(LINK_USB); // Cancel the old mailbox and wake its owner.
    }
    *was_ready=ready;
    return ready;
}
static void task(void *arg) {
    char *line=heap_caps_malloc(TAB5_MAX_FRAME+1,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);size_t used=0;bool overflow=false;
    const size_t input_size=CONFIG_TINYUSB_CDC_RX_BUFSIZE;
    uint8_t *input=heap_caps_malloc(input_size,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    if(!line||!input){free(line);free(input);cdc_task=NULL;vTaskDelete(NULL);return;}
    bool was_ready=false;unsigned epoch=atomic_load(&cdc_epoch);cdc_receive_epoch=epoch;usb_binary binary={0};
    while(true) {
        unsigned current_epoch=atomic_load(&cdc_epoch);
        if(epoch!=current_epoch) {
            epoch=current_epoch;cdc_receive_epoch=epoch;used=0;overflow=false;binary.used=binary.want=0;binary.failed=false;
            usb_rx_enable(false);usb_bulk_receive_select(false);
            // Close/open may complete while this task is blocked. Invalidate
            // the old heartbeat even if the readiness poll never saw DTR low;
            // retain session/sequence anti-replay state until a new full frame.
            xSemaphoreTake(device_mutex,portMAX_DELAY);
            link_state_disconnect(&device_links,LINK_USB);
            xSemaphoreGive(device_mutex);
            rpc_link_reset(LINK_USB);
        }
        if(!usb_link_watch(&was_ready)) {
            // A partial JSON frame must not span unplug/suspend and reconnect.
            used=0;overflow=false;binary.used=binary.want=0;binary.failed=false;size_t discarded=0;
            usb_rx_enable(false);usb_bulk_receive_select(false);
            tinyusb_cdcacm_read(TINYUSB_CDC_ACM_0,input,input_size,&discarded);
            vTaskDelay(pdMS_TO_TICKS(10));continue;
        }
        size_t received=0;
        if(usb_binary_expire(&binary,esp_timer_get_time()/1000)){atomic_fetch_add(&cdc_binary_expired,1);used=0;overflow=true;usb_rx_enable(false);usb_bulk_receive_select(false);rpc_link_reset(LINK_USB);}
        unsigned read_begin=(unsigned)esp_timer_get_time();
        tinyusb_cdcacm_read(TINYUSB_CDC_ACM_0,input,input_size,&received);
        unsigned read_us=(unsigned)esp_timer_get_time()-read_begin;
        int count=(int)received;
        atomic_fetch_add(&cdc_rx_bytes,(unsigned)received);
        if(count==0)ulTaskNotifyTake(pdTRUE,pdMS_TO_TICKS(10));
        for(int i=0;i<count;i++) {
            // Do not dispatch a buffer captured before the port was closed.
            // The next loop resets only parser/mailbox state, not new FIFO data.
            if(atomic_load(&cdc_epoch)!=epoch)break;
            if(binary.failed)continue; // framing is recovered only by a CDC reconnect
            if(binary.want){
                unsigned copy_begin=(unsigned)esp_timer_get_time();
                size_t n=usb_binary_feed_span(&binary,input+i,count-i);
                if(!n){binary.failed=true;continue;}
                usb_rx_span((unsigned)n,read_us,(unsigned)esp_timer_get_time()-copy_begin,(unsigned)esp_timer_get_time());
                i+=(int)n-1;
                // A close/open during the copy cannot commit an old frame.
                if(atomic_load(&cdc_epoch)!=epoch)break;
                if(binary.used==binary.want)usb_binary_reply(&binary,false,0);
                continue;
            }
            if(input[i]=='\n') {
                atomic_fetch_add(&cdc_json_lines,1);if(overflow)atomic_fetch_add(&cdc_overflows,1);
                line[used]=0;
                if(!overflow&&used>7&&!strncmp(line,"@AIBOT ",7)) {
                    cJSON *root=cJSON_Parse(line+7);cJSON *type=cJSON_GetObjectItemCaseSensitive(root,"type");
                    if(cJSON_IsString(type)&&!trusted_host_public_command(type->valuestring)&&!device_host_usb_owned()) {
                        static const char required[]="@AIBOT {\"version\":1,\"type\":\"tab5_host_required\"}\n";
                        usb_write(required,sizeof(required)-1);cJSON_Delete(root);used=0;continue;
                    }
                    if(cJSON_IsString(type)&&!strcmp(type->valuestring,"tab5_ping")) write_reply("tab5_hello",0);
                    else if(cJSON_IsString(type)&&!strcmp(type->valuestring,"tab5_host_claim")) {
                        cJSON *response=device_host_claim(root);
                        if(!response){response=cJSON_CreateObject();cJSON_AddStringToObject(response,"type","tab5_host_claimed");cJSON_AddStringToObject(response,"error","host_not_trusted");}
                        char *wire=cJSON_PrintUnformatted(response);if(wire){usb_write("@AIBOT ",7);usb_write(wire,strlen(wire));usb_write("\n",1);free(wire);}cJSON_Delete(response);
                    }
                    else if(cJSON_IsString(type)&&!strcmp(type->valuestring,"tab5_rpc")) usb_rpc(root);
                    else if(cJSON_IsString(type)&&!strcmp(type->valuestring,"tab5_rpc_binary")) usb_binary_start(&binary,root);
                    else if(cJSON_IsString(type)&&!strcmp(type->valuestring,"tab5_benchmark")) {
                        device_config cfg=device_get_config();const char *id=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(root,"deviceId"));
                        cJSON *v=cJSON_GetObjectItemCaseSensitive(root,"version");const char *action=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(root,"action"));
                        if(cfg.paired&&id&&!strcmp(cfg.id,id)&&cJSON_IsNumber(v)&&v->valuedouble==1&&action) {
                            if(!strcmp(action,"start"))transport_benchmark_start();else if(!strcmp(action,"ble_firmware_probe"))transport_benchmark_start_firmware_probe(false);else if(!strcmp(action,"ble_firmware_probe_isolated"))transport_benchmark_start_firmware_probe(true);else if(!strcmp(action,"ble_ota_probe"))transport_benchmark_start_ble_ota_probe();else if(!strcmp(action,"ble_ota_probe_isolated"))transport_benchmark_start_ble_isolated_probe();else if(!strcmp(action,"cancel"))transport_benchmark_cancel();
                            char *value=heap_caps_malloc(3600,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);if(!value){cJSON_Delete(root);used=0;continue;}
#if TAB5_LOCAL_ART
                            if(!strcmp(action,"gallery_start")){usb_rx_reset();gallery_speed_start();}
                            else if(!strcmp(action,"gallery_cancel"))gallery_speed_cancel();
                            if(!strncmp(action,"gallery_",8)){gallery_speed_diagnostics(value,3600);size_t used=strlen(value);if(used<3600)usb_rx_format(value+used,3600-used);}else
#endif
                            transport_benchmark_diagnostics(value,3600);
                            char power[48];wifi_performance_diagnostics(power,sizeof(power));
                            cJSON *reply=cJSON_CreateObject();cJSON_AddStringToObject(reply,"type","tab5_benchmark");cJSON_AddStringToObject(reply,"deviceId",cfg.id);cJSON_AddStringToObject(reply,"result",value);cJSON_AddStringToObject(reply,"wifiPower",power);free(value);
                            cJSON_AddBoolToObject(reply,"bleOtaProbe",true);
                            #if TAB5_LOCAL_ART
                            cJSON_AddBoolToObject(reply,"gallerySpeed",true);
#endif
                            cJSON_AddBoolToObject(reply,"bleFirmwareProbe",true);
                            char radio[256],isolation[96];ble_probe_diagnostics(radio,sizeof(radio));wifi_probe_diagnostics(isolation,sizeof(isolation));
                            cJSON_AddStringToObject(reply,"bleRadio",radio);cJSON_AddStringToObject(reply,"wifiIsolation",isolation);
                            cJSON_AddBoolToObject(reply,"bleOtaProbeIsolation",true);
                            char *wire=cJSON_PrintUnformatted(reply);if(wire){usb_write("@AIBOT ",7);usb_write(wire,strlen(wire));usb_write("\n",1);free(wire);}cJSON_Delete(reply);
                        }
                    }
                    else if(cJSON_IsString(type)&&!strcmp(type->valuestring,"tab5_ota_status")) write_ota_diagnostic();
                    else if(cJSON_IsString(type)&&!strcmp(type->valuestring,"tab5_orientation_status")) {
                        art_orientation_state s=art_orientation_get();char reply[384],diag[100];art_orientation_diagnostics(diag,sizeof(diag));
                        int n=snprintf(reply,sizeof(reply),"@AIBOT {\"version\":1,\"type\":\"tab5_orientation_diagnostic\",\"ready\":%s,\"orientation\":%d,\"accel\":[%d,%d,%d],\"error\":%d,\"sampleAgeMs\":%lld,\"diagnostic\":\"%s\"}\n",s.ready?"true":"false",s.orientation,s.x,s.y,s.z,s.error,s.sampled_ms?(esp_timer_get_time()/1000-s.sampled_ms):-1LL,diag);
                        if(n>0&&n<sizeof(reply))usb_write(reply,n);
                    }
                    else if(cJSON_IsString(type)&&(!strcmp(type->valuestring,"tab5_wifi_list")||!strcmp(type->valuestring,"tab5_wifi_forget"))) {
                        device_config cfg=device_get_config();
                        const char *id=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(root,"deviceId"));
                        cJSON *version=cJSON_GetObjectItemCaseSensitive(root,"version");
                        if(cfg.paired&&id&&!strcmp(id,cfg.id)&&cJSON_IsNumber(version)&&version->valueint==1) {
                            if(!strcmp(type->valuestring,"tab5_wifi_forget")) {
                                const char *ssid=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(root,"ssid"));
                                write_reply(ssid&&tab5_ui_wifi_forget(ssid)?"tab5_wifi_forgotten":"tab5_wifi_rejected",0);
                            } else {
                                cJSON *response=cJSON_CreateObject(),*networks=device_wifi_public_list();
                                if(response&&networks) {
                                    cJSON_AddNumberToObject(response,"version",1);
                                    cJSON_AddStringToObject(response,"type","tab5_wifi_list");
                                    cJSON_AddStringToObject(response,"deviceId",cfg.id);
                                    cJSON_AddItemToObject(response,"networks",networks);networks=NULL;
                                    char *json=cJSON_PrintUnformatted(response);
                                    if(json){usb_write("@AIBOT ",7);usb_write(json,strlen(json));usb_write("\n",1);free(json);}
                                }
                                cJSON_Delete(networks);cJSON_Delete(response);
                            }
                        }
                    }
                    else if(cJSON_IsString(type)&&!strcmp(type->valuestring,"tab5_crash_status")) write_crash_diagnostic();
                    else if(cJSON_IsString(type)&&(!strcmp(type->valuestring,"tab5_metrics")||!strcmp(type->valuestring,"tab5_status")||!strcmp(type->valuestring,"tab5_delta")||!strcmp(type->valuestring,"tab5_packed"))) {
                        bool metrics=!strcmp(type->valuestring,"tab5_metrics");
                        // Dispatch no longer retains a second full catalog tree.
                        // Packed full/delta status share the exact-sequence ACK; metrics stay raw.
                        cJSON_Delete(root);root=NULL;uint64_t seq=0;
                        device_reject_reason reason;bool ok=device_receive_checked(line+7,LINK_USB,&seq,&reason);
                        if(ok&&!metrics)write_reply("tab5_ack",seq);
                        else {char ack[160];int n=snprintf(ack,sizeof(ack),"@AIBOT {\"type\":\"%s%s\",\"sequence\":%llu,\"rejectReason\":%d}\n",metrics?"tab5_metrics_ack":"tab5_ack",ok?"":"_rejected",(unsigned long long)seq,(int)reason);if(n>0&&(size_t)n<sizeof(ack))usb_write(ack,n);}
                    }
#ifdef TAB5_USB_HID
                    else if(cJSON_IsString(type)&&!strcmp(type->valuestring,"tab5_hid_status")) {
                        extern unsigned tab5_hid_magic,tab5_hid_stage,tab5_hid_error;
                        extern unsigned tab5_hid_previous_stage(void);
                        device_config current=device_get_config();
                        char status[200];int n=snprintf(status,sizeof(status),"@AIBOT {\"type\":\"hid_status\",\"deviceId\":\"%s\",\"valid\":%s,\"stage\":%u,\"previousStage\":%u,\"error\":%u}\n",current.id,tab5_hid_magic==0x48494435?"true":"false",tab5_hid_stage,tab5_hid_previous_stage(),tab5_hid_error);
                        usb_write(status,n);
                    }
#endif
#ifdef TAB5_USB_HID
                    else if(cJSON_IsString(type)&&(!strcmp(type->valuestring,"tab5_hid_toggle")
#ifdef TAB5_HID_PROBE
                        ||!strcmp(type->valuestring,"tab5_hid_probe")||!strcmp(type->valuestring,"tab5_hid_enumerate")||!strcmp(type->valuestring,"tab5_hid_keycheck")
#endif
                        )) {
                        int key_mode=1;bool auto_escape=false;
#ifndef TAB5_HID_PROBE
                        const char *shortcut=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(root,"shortcut"));
                        if(shortcut&&!strcmp(shortcut,"RightAltSpace"))key_mode=3;
                        else if(shortcut&&!strcmp(shortcut,"CtrlAltSpace"))key_mode=4;
                        else if(shortcut&&strcmp(shortcut,"LeftAltSpace")){cJSON_Delete(root);used=0;continue;}
#endif
#ifdef TAB5_HID_PROBE
                        key_mode=!strcmp(type->valuestring,"tab5_hid_enumerate")?0:!strcmp(type->valuestring,"tab5_hid_keycheck")?2:1;
                        auto_escape=!strcmp(type->valuestring,"tab5_hid_probe");
#endif
                        if(!device_get_config().paired){cJSON_Delete(root);used=0;continue;}
                        write_reply("tab5_hid_probe_start",0);
                        // The normal composite device does not re-enumerate;
                        // its acknowledgement needs no old diagnostic pause.
                        if(auto_escape)vTaskDelay(pdMS_TO_TICKS(300));
                        extern void tab5_hid_probe(int,bool);tab5_hid_probe(key_mode,auto_escape);
                    }
#endif
                    else if(root) {const char *reply=NULL;if(device_command(root,&reply)&&reply)write_reply(reply,0);}
                    cJSON_Delete(root);
                }
                used=0;overflow=false;
            } else if(!overflow&&used<TAB5_MAX_FRAME) line[used++]=(char)input[i];else overflow=true;
        }
    }
}
void usb_start(void) {
    device_config cfg=device_get_config();
    snprintf(usb_serial,sizeof(usb_serial),"TAB5-%s",cfg.id);
    // USB-C is wired to PHY0. IDF 5.4.2 phy_select(0) falls through to
    // PHY1, so route this volatile mux explicitly before TinyUSB starts.
    LP_SYS.usb_ctrl.sw_hw_usb_phy_sel=1;
    LP_SYS.usb_ctrl.sw_usb_phy_sel=1;
    tinyusb_config_t usb=TINYUSB_DEFAULT_CONFIG();
    // Keep USB control transfers responsive while audio capture and CDC frames
    // are active on the other tasks. A missed descriptor request drops COM/HID.
    usb.task=TINYUSB_TASK_CUSTOM(8192,TAB5_USB_STACK_PRIORITY,TAB5_USB_IO_CORE);
    usb.port=TINYUSB_PORT_FULL_SPEED_0;usb.event_cb=usb_keyboard_bus_event;
    usb.descriptor.device=&usb_device;
    usb.descriptor.string=usb_strings;
    usb.descriptor.string_count=sizeof(usb_strings)/sizeof(usb_strings[0]);
    usb.descriptor.full_speed_config=usb_configuration;
    esp_err_t err=tinyusb_driver_install(&usb);
    if(err!=ESP_OK) {
        printf("TAB5_USB_COMPOSITE_INIT error=%d\n",err);
        return; // Wi-Fi, BLE, and the display still start for diagnosis.
    }
    tinyusb_config_cdcacm_t cdc={.cdc_port=TINYUSB_CDC_ACM_0,.callback_line_state_changed=usb_cdc_line_state,.callback_rx=usb_cdc_rx};
    err=tinyusb_cdcacm_init(&cdc);
    if(err!=ESP_OK) {
        printf("TAB5_USB_CDC_INIT error=%d\n",err);
        tinyusb_driver_uninstall();
        return;
    }
    usb_keyboard_init();
    esp_err_t shutdown_result=esp_register_shutdown_handler(usb_prepare_restart);
    if(shutdown_result!=ESP_OK)printf("TAB5_USB_SHUTDOWN_INIT error=%d\n",shutdown_result);
    if(xTaskCreatePinnedToCore(task,"tab5_usb",8192,NULL,TAB5_USB_RECEIVER_PRIORITY,&cdc_task,TAB5_USB_IO_CORE)!=pdPASS)
        printf("TAB5_USB_TASK_INIT error=out_of_memory\n");
}





