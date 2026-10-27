// Included by ble_link.c: one bounded mailbox, owned by the voice worker.
// HTTP voice authentication/encryption is preserved byte-for-byte over GATT.
#define VOICE_CAPACITY 12288
static SemaphoreHandle_t voice_lock;
static SemaphoreHandle_t voice_done;
static uint8_t *voice_out,*voice_in;
static uint32_t voice_serial,voice_active;
static uint16_t voice_size,voice_offset,voice_offered,voice_received,voice_expected;
static bool voice_complete;
static int64_t voice_peer_seen;
static uint16_t read16(const uint8_t *p){return p[0]|((uint16_t)p[1]<<8);}
static uint32_t read32(const uint8_t *p){return read16(p)|((uint32_t)read16(p+2)<<16);}
static void write16(uint8_t *p,unsigned n){p[0]=n;p[1]=n>>8;}
static void write32(uint8_t *p,uint32_t n){write16(p,n);write16(p+2,n>>16);}
bool ble_voice_ready(void) {
    if(!voice_lock||!ble_connected)return false;
    xSemaphoreTake(voice_lock,portMAX_DELAY);
    bool ready=voice_peer_seen&&esp_timer_get_time()/1000-voice_peer_seen<5000;
    xSemaphoreGive(voice_lock);return ready;
}
static void voice_reset(void) {
    if(!voice_lock)return;
    xSemaphoreTake(voice_lock,portMAX_DELAY);voice_active=0;voice_peer_seen=0;voice_complete=false;
    if(voice_done)xSemaphoreGive(voice_done);
    xSemaphoreGive(voice_lock);
}
static size_t voice_packet_read(uint8_t *bytes,size_t capacity,void *ctx) {
    (void)ctx;if(capacity<8)return 0;
    xSemaphoreTake(voice_lock,portMAX_DELAY);
    voice_peer_seen=esp_timer_get_time()/1000;
    write32(bytes,voice_active);write16(bytes+4,voice_offset);write16(bytes+6,voice_active?voice_size:0);
    unsigned n=voice_active?voice_size-voice_offset:0;if(n>capacity-8)n=capacity-8;
    if(n)memcpy(bytes+8,voice_out+voice_offset,n);
    voice_offered=voice_offset+n;
    xSemaphoreGive(voice_lock);return n+8;
}
static bool voice_packet_write(const uint8_t *bytes,size_t n,void *ctx) {
    (void)ctx;bool ok=false;
    xSemaphoreTake(voice_lock,portMAX_DELAY);
    voice_peer_seen=esp_timer_get_time()/1000;
    if(n>=7&&voice_active&&read32(bytes+1)==voice_active) {
        if(bytes[0]==1&&n==7) {
            unsigned next=read16(bytes+5);
            if(next==voice_offered&&next>=voice_offset){voice_offset=next;ok=true;}
        } else if(bytes[0]==2&&n>9&&!voice_complete) {
            unsigned offset=read16(bytes+5),total=read16(bytes+7),count=n-9;
            if(voice_offered==voice_size&&total>=28&&total<=VOICE_CAPACITY&&offset==voice_received&&offset+count<=total&&
               (!voice_received||total==voice_expected)) {
                memcpy(voice_in+offset,bytes+9,count);voice_expected=total;voice_received+=count;voice_complete=voice_received==total;ok=true;
                if(voice_complete&&voice_done)xSemaphoreGive(voice_done);
            }
        }
    }
    xSemaphoreGive(voice_lock);return ok;
}
static int voice_access(uint16_t conn,uint16_t attr,struct ble_gatt_access_ctxt *ctx,void *arg) {
    (void)arg;
    if(!device_get_config().paired||!voice_lock||!ble_host_authorized())return BLE_ATT_ERR_INSUFFICIENT_AUTHEN;
    uint8_t bytes[488];
    if(ctx->op==BLE_GATT_ACCESS_OP_READ_CHR) {
        size_t n=voice_packet_read(bytes,sizeof(bytes),NULL);
        return os_mbuf_append(ctx->om,bytes,n)==0?0:BLE_ATT_ERR_INSUFFICIENT_RES;
    }
    unsigned n=OS_MBUF_PKTLEN(ctx->om);
    if(n>sizeof(bytes)||ble_hs_mbuf_to_flat(ctx->om,bytes,n,NULL))return BLE_ATT_ERR_INVALID_ATTR_VALUE_LEN;
    if(n==8&&bytes[0]==3) {
        if(bytes[7]>8)return BLE_ATT_ERR_INVALID_ATTR_VALUE_LEN;
        mailbox_notify_target target={conn,attr};size_t capacity=ble_att_mtu(conn)-3;if(capacity>488)capacity=488;
        return ble_mailbox_window(bytes,n,capacity,voice_packet_read,voice_packet_write,mailbox_notify,&target)?0:BLE_ATT_ERR_UNLIKELY;
    }
    return voice_packet_write(bytes,n,NULL)?0:BLE_ATT_ERR_INVALID_ATTR_VALUE_LEN;
}
uint8_t *ble_voice_exchange(const char *nonce,const char *proof,const uint8_t *data,size_t size,size_t *reply_size) {
    *reply_size=0;
    if(size+96>VOICE_CAPACITY||!ble_voice_ready())return NULL;
    xSemaphoreTake(voice_lock,portMAX_DELAY);
    if(voice_active){xSemaphoreGive(voice_lock);return NULL;}
    SemaphoreHandle_t done=xSemaphoreCreateBinary();voice_done=done;
    memcpy(voice_out,nonce,32);memcpy(voice_out+32,proof,64);memcpy(voice_out+96,data,size);
    voice_size=size+96;voice_offset=voice_offered=voice_received=voice_expected=0;voice_complete=false;
    if(!++voice_serial)++voice_serial;
    uint32_t serial=voice_active=voice_serial;
    xSemaphoreGive(voice_lock);
    uint8_t *reply=NULL;int64_t deadline=esp_timer_get_time()/1000+12000;
    while(ble_connected&&esp_timer_get_time()/1000<deadline) {
        xSemaphoreTake(voice_lock,portMAX_DELAY);
        if(voice_active!=serial){xSemaphoreGive(voice_lock);break;}
        if(voice_complete) {
            reply=malloc(voice_expected);
            if(reply){memcpy(reply,voice_in,voice_expected);*reply_size=voice_expected;}
            xSemaphoreGive(voice_lock);break;
        }
        xSemaphoreGive(voice_lock);
        int64_t remaining=deadline-esp_timer_get_time()/1000;if(remaining<=0)break;
        if(done){if(xSemaphoreTake(done,pdMS_TO_TICKS(remaining))!=pdTRUE)break;}
        else vTaskDelay(pdMS_TO_TICKS(10));
    }
    xSemaphoreTake(voice_lock,portMAX_DELAY);if(voice_active==serial)voice_active=0;
    if(voice_done==done)voice_done=NULL;
    xSemaphoreGive(voice_lock);if(done)vSemaphoreDelete(done);
    return reply;
}
