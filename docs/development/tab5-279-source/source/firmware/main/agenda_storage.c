#include "agenda_storage.h"
#include <string.h>
typedef struct {uint8_t *bytes;size_t capacity,at;bool valid;} writer;
typedef struct {const uint8_t *bytes;size_t size,at;bool valid;} reader;
static bool identity(const char value[33]){return value[32]==0&&strlen(value)==32&&strspn(value,"0123456789abcdef")==32;}
bool agenda_deletions_valid(const agenda_document *document,const agenda_deletions *deleted) {
    if(!deleted||deleted->count>AGENDA_LIMIT)return false;
    for(unsigned n=0;n<deleted->count;n++) {
        const agenda_deletion *d=&deleted->items[n];
        if(!identity(d->id)||!identity(d->edit)||d->base_edit[32]||(d->base_edit[0]&&!identity(d->base_edit)))return false;
        for(unsigned j=0;j<n;j++)if(!strcmp(d->id,deleted->items[j].id))return false;
        for(unsigned j=0;j<document->count;j++)if(!strcmp(d->id,document->items[j].id))return false;
    }
    return true;
}
static void put(writer *w,const void *bytes,size_t size){if(!w->valid||size>w->capacity-w->at){w->valid=false;return;}memcpy(w->bytes+w->at,bytes,size);w->at+=size;}
static void byte(writer *w,unsigned value){uint8_t b=(uint8_t)value;put(w,&b,1);}
static void number(writer *w,int64_t value){if(value<0||value>4105123200LL){w->valid=false;return;}for(unsigned n=0;n<4;n++)byte(w,(uint32_t)value>>(n*8));}
static void guid(writer *w,const char *value){const char *hex="0123456789abcdef";for(unsigned n=0;n<16;n++)byte(w,((unsigned)(strchr(hex,value[n*2])-hex)<<4)|(unsigned)(strchr(hex,value[n*2+1])-hex));}
static void base(writer *w,const char *value){byte(w,value[0]!=0);if(value[0])guid(w,value);else{static const uint8_t zero[16];put(w,zero,sizeof(zero));}}
size_t agenda_storage_encode(const agenda_document *document,const agenda_deletions *deleted,uint8_t *out,size_t capacity) {
    if(!out||!agenda_document_valid(document)||!agenda_deletions_valid(document,deleted))return 0;
    writer w={out,capacity,0,true};const uint8_t magic[]={'A','G','L',1};put(&w,magic,sizeof(magic));byte(&w,document->count);byte(&w,deleted->count);guid(&w,document->revision);
    for(unsigned n=0;n<document->count;n++) {
        const agenda_item *i=&document->items[n];guid(&w,i->id);guid(&w,i->edit);base(&w,i->base_edit);
        size_t length=strlen(i->title);byte(&w,length);put(&w,i->title,length);
        number(&w,i->due);number(&w,i->done);number(&w,i->snooze_occurrence);number(&w,i->snooze_until);number(&w,i->alerted);number(&w,i->end);
        byte(&w,i->repeat);byte(&w,i->lead);byte(&w,i->dirty);
    }
    for(unsigned n=0;n<deleted->count;n++){const agenda_deletion *d=&deleted->items[n];guid(&w,d->id);guid(&w,d->edit);base(&w,d->base_edit);}
    return w.valid?w.at:0;
}
static void get(reader *r,void *out,size_t size){if(!r->valid||size>r->size-r->at){r->valid=false;memset(out,0,size);return;}memcpy(out,r->bytes+r->at,size);r->at+=size;}
static unsigned read_byte(reader *r){uint8_t b=0;get(r,&b,1);return b;}
static int64_t read_number(reader *r){uint32_t value=0;for(unsigned n=0;n<4;n++)value|=(uint32_t)read_byte(r)<<(n*8);return value;}
static void read_guid(reader *r,char value[33]){const char *hex="0123456789abcdef";for(unsigned n=0;n<16;n++){unsigned b=read_byte(r);value[n*2]=hex[b>>4];value[n*2+1]=hex[b&15];}value[32]=0;}
static void read_base(reader *r,char value[33]){unsigned present=read_byte(r);read_guid(r,value);if(present>1)r->valid=false;if(!present){if(strspn(value,"0")!=32)r->valid=false;memset(value,0,33);}}
bool agenda_storage_decode(const uint8_t *bytes,size_t size,agenda_document *document,agenda_deletions *deleted) {
    const uint8_t magic[]={'A','G','L',1};if(!bytes||!document||!deleted||size<22||size>AGENDA_STORAGE_MAX||memcmp(bytes,magic,sizeof(magic)))return false;
    reader r={bytes,size,4,true};memset(document,0,sizeof(*document));memset(deleted,0,sizeof(*deleted));document->version=1;
    document->count=read_byte(&r);deleted->count=read_byte(&r);if(document->count>AGENDA_LIMIT||deleted->count>AGENDA_LIMIT)return false;read_guid(&r,document->revision);
    for(unsigned n=0;r.valid&&n<document->count;n++) {
        agenda_item *i=&document->items[n];read_guid(&r,i->id);read_guid(&r,i->edit);read_base(&r,i->base_edit);unsigned length=read_byte(&r);
        if(!length||length>=sizeof(i->title)){r.valid=false;break;}get(&r,i->title,length);if(memchr(i->title,0,length)){r.valid=false;break;}
        i->due=read_number(&r);i->done=read_number(&r);i->snooze_occurrence=read_number(&r);i->snooze_until=read_number(&r);i->alerted=read_number(&r);i->end=read_number(&r);
        i->repeat=read_byte(&r);i->lead=read_byte(&r);i->dirty=read_byte(&r);if(i->alerted>4105123200LL)return false;
    }
    for(unsigned n=0;r.valid&&n<deleted->count;n++){agenda_deletion *d=&deleted->items[n];read_guid(&r,d->id);read_guid(&r,d->edit);read_base(&r,d->base_edit);}
    return r.valid&&r.at==size&&agenda_document_valid(document)&&agenda_deletions_valid(document,deleted);
}
