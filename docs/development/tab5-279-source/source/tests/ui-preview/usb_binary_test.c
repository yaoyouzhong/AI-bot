#undef NDEBUG
#include <assert.h>
#include <stdio.h>
#include "usb_binary.h"
int main(void) {
    unsigned wait;const char *wait_cases[]={"{}","{\"waitMs\":0}","{\"waitMs\":20}"};
    for(unsigned i=0;i<3;i++){cJSON *r=cJSON_Parse(wait_cases[i]);assert(usb_binary_wait(r,0,&wait)&&wait==(i==2?20u:0u));cJSON_Delete(r);}
    const char *wait_bad[]={"{\"waitMs\":21}","{\"waitMs\":-1}","{\"waitMs\":20.5}","{\"waitMs\":\"20\"}","{\"waitMs\":null}","{\"waitMs\":true}"};
    for(unsigned i=0;i<6;i++){cJSON *r=cJSON_Parse(wait_bad[i]);assert(!usb_binary_wait(r,0,&wait));cJSON_Delete(r);}
    cJSON *body_wait=cJSON_Parse("{\"waitMs\":20}");assert(!usb_binary_wait(body_wait,7,&wait));cJSON_Delete(body_wait);
    uint8_t bytes[USB_BINARY_MAX],header[16];usb_binary state={.data=bytes,.want=USB_BINARY_MAX,.deadline=100};
    for(unsigned i=0;i<USB_BINARY_MAX;i++)assert(usb_binary_feed(&state,(uint8_t)i)==(i==USB_BINARY_MAX-1));
    for(unsigned i=0;i<USB_BINARY_MAX;i++)assert(bytes[i]==(uint8_t)i);
    assert(!usb_binary_feed(&state,0));
    uint8_t input[8192];for(unsigned i=0;i<sizeof(input);i++)input[i]=(uint8_t)i;
    const unsigned lengths[]={1,64,512,8192,USB_BINARY_MAX};
    for(unsigned k=0;k<sizeof(lengths)/sizeof(*lengths);k++) {
        state=(usb_binary){.data=bytes,.want=lengths[k]};unsigned consumed=0;
        while(consumed<state.want){size_t n=usb_binary_feed_span(&state,input,sizeof(input));assert(n&&n<=sizeof(input));consumed+=n;}
        assert(consumed==lengths[k]&&!usb_binary_feed_span(&state,input,sizeof(input)));
        for(unsigned i=0;i<consumed;i++)assert(bytes[i]==(uint8_t)i);
    }
    state=(usb_binary){.data=bytes,.want=20,.used=3,.deadline=100};assert(!usb_binary_expire(&state,99));assert(usb_binary_expire(&state,100));
    assert(state.failed&&!state.want&&!state.used&&!usb_binary_feed(&state,'\n'));
    usb_binary_header(header,0x12345678,16393,true);
    assert(!memcmp(header,"AIB2",4)&&header[4]==0x78&&header[7]==0x12&&header[8]==9&&header[9]==64&&header[12]==1);
    cJSON *root=cJSON_Parse("{\"version\":1,\"deviceId\":\"device\",\"tag\":4294967295,\"count\":16393}");
    unsigned count,chunk;uint32_t tag;assert(usb_binary_command(root,"device",&tag,&count,&chunk)&&tag==UINT32_MAX&&count==16393&&chunk==16384);
    assert(!usb_binary_command(root,"other",&tag,&count,&chunk));
    cJSON_ReplaceItemInObject(root,"count",cJSON_CreateNumber(65535));
    assert(!usb_binary_command(root,"device",&tag,&count,&chunk)); // no large fragments without negotiation
    cJSON_AddNumberToObject(root,"chunk",65526);
    assert(usb_binary_command(root,"device",&tag,&count,&chunk)&&count==USB_BINARY_MAX&&chunk==65526);
    double bad[]={-1,65536,1.5};for(unsigned i=0;i<3;i++){cJSON_ReplaceItemInObject(root,"count",cJSON_CreateNumber(bad[i]));assert(!usb_binary_command(root,"device",&tag,&count,&chunk));}
    cJSON_ReplaceItemInObject(root,"count",cJSON_CreateNumber(0));
    double invalid_chunks[]={-1,65527,1.5};for(unsigned i=0;i<3;i++){cJSON_ReplaceItemInObject(root,"chunk",cJSON_CreateNumber(invalid_chunks[i]));assert(!usb_binary_command(root,"device",&tag,&count,&chunk));}
    cJSON_Delete(root);puts("USB_BINARY_OK production framing, embedded newlines, exact capacity, timeout requires reconnect, identity/length rejected");
}
