// Core is shared by CDC/GATT and compiled directly by the native test.
#define RPC_REQUEST_MAX 65535
#define RPC_RESPONSE_MAX 65535
typedef struct {
    uint8_t *out,*in;
    uint32_t serial,active,generation;
    unsigned size,offset,offered,received,expected;
    bool complete;
    int64_t peer_seen;
    int64_t confirmed_ms;
    char confirmed_session[40];
} rpc_mailbox;
static unsigned rpc_u16(const uint8_t *p){return p[0]|((unsigned)p[1]<<8);}
static uint32_t rpc_u32(const uint8_t *p){return rpc_u16(p)|((uint32_t)rpc_u16(p+2)<<16);}
static void rpc_put16(uint8_t *p,unsigned n){p[0]=n;p[1]=n>>8;}
static void rpc_put32(uint8_t *p,uint32_t n){rpc_put16(p,n);rpc_put16(p+2,n>>16);}
static size_t rpc_read(rpc_mailbox *m,uint8_t *out,size_t capacity) {
    if(capacity<8)return 0;
    rpc_put32(out,m->active);rpc_put16(out+4,m->offset);rpc_put16(out+6,m->active?m->size:0);
    size_t n=m->active?m->size-m->offset:0;if(n>capacity-8)n=capacity-8;
    if(n)memcpy(out+8,m->out+m->offset,n);
    m->offered=m->offset+n;return n+8;
}
static bool rpc_write_with_replay(rpc_mailbox *m,const uint8_t *data,size_t size,bool replay) {
    if(size<7||!m->active||rpc_u32(data+1)!=m->active)return false;
    if(data[0]==1&&size==7) {
        unsigned next=rpc_u16(data+5);
        if(next!=m->offered||next<m->offset)return false;
        m->offset=next;return true;
    }
    if(data[0]!=2||size<=9)return false;
    unsigned offset=rpc_u16(data+5),total=rpc_u16(data+7),count=size-9;
    if(m->offered!=m->size||total<28||total>RPC_RESPONSE_MAX||
       offset+count>total||(m->received&&total!=m->expected))return false;
    // BLE may replay an interrupted response window. Accept only bytes that
    // already belong to this active response and match the accepted prefix.
    // Gaps, overlapping new data and changed duplicates remain rejected.
    if(replay&&offset<m->received&&offset+count<=m->received)
        return !memcmp(m->in+offset,data+9,count);
    if(m->complete||offset!=m->received)return false;
    memcpy(m->in+offset,data+9,count);m->expected=total;m->received+=count;
    m->complete=m->received==total;return true;
}
static bool rpc_write(rpc_mailbox *m,const uint8_t *data,size_t size) {
    return rpc_write_with_replay(m,data,size,false);
}
