#include "gallery_store.h"
#include "sd_paths.h"
#include <errno.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#ifdef _WIN32
#include <direct.h>
#include <io.h>
#define store_mkdir(path) _mkdir(path)
#define store_sync(file) _commit(_fileno(file))
#define store_truncate(file,size) _chsize_s(_fileno(file),(size))
#else
#include <unistd.h>
#define store_mkdir(path) mkdir(path,0775)
#define store_sync(file) fsync(fileno(file))
#define store_truncate(file,size) ftruncate(fileno(file),(size))
#endif
#ifndef GALLERY_STORE_TEST
#include "mbedtls/sha256.h"
#endif

// Includes separately bounded complete-text pages and JSON escaping. This is
// persisted metadata, not a larger wire reply or LVGL original-text buffer.
#define META_LIMIT (256u*1024u)
#define JPEG_LIMIT 1048576u
// Header: magic, little-endian lengths, SHA-256(metadata), SHA-256(JPEG).
// Two alternating records preserve the last complete write on FAT, whose
// rename does not replace an existing destination. Never touch other files.
static const uint8_t magic[8]={'A','I','B','A','R','T','0','1'},magic_tail[8]={'A','I','B','A','R','T','0','2'};
static const uint8_t magic_ref[8]={'A','I','B','A','R','T','0','3'};
static bool media_error;
bool gallery_store_media_error(void){return media_error;}
static void io_error(void) {
    if(errno==EIO||errno==ENODEV||errno==ENXIO||errno==ETIMEDOUT)media_error=true;
}
#include "gallery_file_io.h"
static uint32_t u32(const uint8_t *b){return (uint32_t)b[0]|(uint32_t)b[1]<<8|(uint32_t)b[2]<<16|(uint32_t)b[3]<<24;}
static void set32(uint8_t *b,uint32_t v){for(int i=0;i<4;i++)b[i]=(uint8_t)(v>>(8*i));}
static const char *text(cJSON *o,const char *k){const char *v=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(o,k));return v?v:"";}
static bool revision_ok(const char *r){return r&&(!*r||(strlen(r)==64&&strspn(r,"0123456789abcdef")==64));}
static bool key_ok(const gallery_image *k) {
    if(!k||k->category<0||k->category>1||k->frame<0||k->frame>=64||strlen(k->date)!=10)return false;
    for(int i=0;i<10;i++)if(i==4||i==7){if(k->date[i]!='-')return false;}else if(k->date[i]<'0'||k->date[i]>'9')return false;
    int month=(k->date[5]-'0')*10+k->date[6]-'0',day=(k->date[8]-'0')*10+k->date[9]-'0';
    return month>=1&&month<=12&&day>=1&&day<=31;
}
static bool path_for(char *out,size_t size,const char *root,const gallery_image *key,unsigned slot) {
    if(!root||!key_ok(key)||slot>1)return false;
    int n=snprintf(out,size,"%s/%s/works/%s/%02d-%c.%u.aig",root,key->category?SD_STORAGE_CALLIGRAPHY_DIR:SD_STORAGE_PAINTING_DIR,key->date,key->frame,key->portrait?'p':'l',slot);
    return n>0&&(size_t)n<size;
}
static bool directories(const char *root,int category,const char *work) {
    if(!root||category<0||category>1)return false;
    const char *category_path=category?SD_STORAGE_CALLIGRAPHY_DIR:SD_STORAGE_PAINTING_DIR;
    const char *parts[]={SD_STORAGE_APP_DIR,SD_STORAGE_GALLERY_DIR,category_path};
    for(int i=0;i<3;i++) {
        char p[512];int n=snprintf(p,sizeof(p),"%s/%s",root,parts[i]);if(n<0||n>=sizeof(p))return false;
        if(store_mkdir(p)&&errno!=EEXIST){io_error();return false;}
    }
    if(work) {
        char p[512];int n=snprintf(p,sizeof(p),"%s/%s/works",root,category_path);if(n<0||n>=sizeof(p))return false;
        if(store_mkdir(p)&&errno!=EEXIST){io_error();return false;}
        n=snprintf(p,sizeof(p),"%s/%s/works/%s",root,category_path,work);if(n<0||n>=sizeof(p))return false;
        if(store_mkdir(p)&&errno!=EEXIST){io_error();return false;}
    }
    return true;
}
static cJSON *metadata(FILE *file,uint8_t header[80],const gallery_image *key,const char *revision) {
    if(store_fread(header,1,80,file)!=80){if(ferror(file))io_error();return NULL;}
    uint32_t m=u32(header+8),j=u32(header+12);
    bool tail=!memcmp(header,magic_tail,8);
    if((memcmp(header,magic,8)&&!tail&&memcmp(header,magic_ref,8))||!m||m>META_LIMIT||j<16||j>JPEG_LIMIT)return NULL;
    if(tail&&fseek(file,80+j,SEEK_SET)){io_error();return NULL;}
    char *bytes=malloc(m+1);if(!bytes){errno=ENOMEM;return NULL;}
    uint8_t hash[32];cJSON *meta=NULL;
    if(store_fread(bytes,1,m,file)==m&&mbedtls_sha256((uint8_t *)bytes,m,hash,0)==0&&!memcmp(hash,header+16,32)) {
        bytes[m]=0;meta=cJSON_ParseWithLength(bytes,m+1);
    }else if(ferror(file))io_error();
    free(bytes);if(!meta)return NULL;
    cJSON *category=cJSON_GetObjectItemCaseSensitive(meta,"category"),*frame=cJSON_GetObjectItemCaseSensitive(meta,"frame"),*frames=cJSON_GetObjectItemCaseSensitive(meta,"frames"),*serial=cJSON_GetObjectItemCaseSensitive(meta,"serial");
    bool ok=cJSON_IsObject(meta)&&cJSON_IsNumber(category)&&category->valuedouble==key->category&&
        cJSON_IsNumber(frame)&&frame->valuedouble==key->frame&&cJSON_IsNumber(frames)&&frames->valuedouble==frames->valueint&&frames->valueint>key->frame&&frames->valueint<=64&&
        cJSON_IsBool(cJSON_GetObjectItemCaseSensitive(meta,"portrait"))&&cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(meta,"portrait"))==key->portrait&&
        cJSON_IsNumber(serial)&&serial->valuedouble>=1&&serial->valuedouble<=UINT32_MAX&&serial->valuedouble==(uint32_t)serial->valuedouble&&
        !strcmp(text(meta,"date"),key->date)&&strlen(text(meta,"workId"))<sizeof(key->work_id)&&revision_ok(text(meta,"revision"))&&
        (!*revision||!strcmp(text(meta,"revision"),revision));
    if(tail&&fseek(file,80,SEEK_SET))ok=false;
    if(!ok){cJSON_Delete(meta);return NULL;}return meta;
}
static bool record_end(FILE *file,const uint8_t header[80]) {
    if(fseek(file,0,SEEK_END))return false;
    return ftell(file)==(long)(80u+u32(header+8)+(memcmp(header,magic_ref,8)?u32(header+12):0))&&!ferror(file);
}
static bool reference_path(char out[520],const char *root,const gallery_image *key,unsigned slot) {
    char base[512];if(!path_for(base,sizeof(base),root,key,slot))return false;
    int n=snprintf(out,520,"%s.meta",base);return n>0&&n<520;
}
// Validate both metadata records and their exact image-slot identity before
// considering a reference. JPEG bytes are still hashed on every actual load.
static cJSON *reference_meta(const char *root,const gallery_image *key,const char *revision,unsigned slot,uint8_t header[80],int *source) {
    char path[520];if(!reference_path(path,root,key,slot))return NULL;
    FILE *f=store_fopen(path,"rb");if(!f){io_error();return NULL;}
    cJSON *meta=metadata(f,header,key,revision);
    bool ok=meta&&!memcmp(header,magic_ref,8)&&record_end(f,header);
    if(fclose(f)){io_error();ok=false;}
    cJSON *at=cJSON_GetObjectItemCaseSensitive(meta,"sourceSlot"),*serial=cJSON_GetObjectItemCaseSensitive(meta,"sourceSerial");
    ok=ok&&cJSON_IsNumber(at)&&at->valuedouble==at->valueint&&at->valueint>=0&&at->valueint<2&&
        cJSON_IsNumber(serial)&&serial->valuedouble>=1&&serial->valuedouble<=UINT32_MAX&&serial->valuedouble==(uint32_t)serial->valuedouble;
    if(ok) {
        *source=at->valueint;char image_path[512];uint8_t base_header[80];
        ok=path_for(image_path,sizeof(image_path),root,key,*source);
        f=ok?store_fopen(image_path,"rb"):NULL;
        cJSON *base=f?metadata(f,base_header,key,""):NULL;
        ok=base&&memcmp(base_header,magic_ref,8)&&record_end(f,base_header)&&
            cJSON_GetObjectItemCaseSensitive(base,"serial")->valuedouble==serial->valuedouble&&
            !strcmp(text(base,"workId"),text(meta,"workId"))&&u32(base_header+12)==u32(header+12)&&!memcmp(base_header+48,header+48,32);
        cJSON_Delete(base);if(f&&fclose(f)){io_error();ok=false;}
    }
    if(!ok){cJSON_Delete(meta);return NULL;}return meta;
}
static bool resume_header(const gallery_image *,const char *,const char *,size_t,uint8_t [76]);
static bool direct_path(char out[512],const char *root,const gallery_image *key,const char *suffix) {
    if(!root||!key_ok(key))return false;
    int n=snprintf(out,512,"%s/%s/download.%s",root,key->category?SD_STORAGE_CALLIGRAPHY_DIR:SD_STORAGE_PAINTING_DIR,suffix);
    return n>0&&n<512;
}
static uint32_t probe(const char *path,const gallery_image *key,const char *revision,bool check_jpeg) {
    FILE *f=store_fopen(path,"rb");if(!f){io_error();return 0;}
    uint8_t header[80];errno=0;cJSON *m=metadata(f,header,key,revision);
    uint32_t serial=m?(uint32_t)cJSON_GetObjectItemCaseSensitive(m,"serial")->valuedouble:0;
    if(check_jpeg&&!m&&errno==ENOMEM)serial=UINT32_MAX;
    if(m&&serial&&check_jpeg) {
        size_t size=u32(header+12);uint8_t hash[32],*bytes=malloc(size);
        if(!bytes)serial=UINT32_MAX; // Cannot verify the survivor: do not replace it.
        else if(store_fread(bytes,1,size,f)!=size||!record_end(f,header)||ferror(f)||
           mbedtls_sha256(bytes,size,hash,0)||memcmp(hash,header+48,32))serial=0;
        if(ferror(f))io_error();
        free(bytes);
    }
    cJSON_Delete(m);if(fclose(f))io_error();return serial;
}
bool gallery_store_get(const char *root,const gallery_image *key,const char *revision,gallery_store_allocator allocate,gallery_jpeg *out,cJSON **guide) {
    media_error=false;if(!out||!guide||!allocate||!revision_ok(revision))return false;
    *out=(gallery_jpeg){0};*guide=NULL;
    char paths[2][512];uint32_t serials[4]={0};
    for(int i=0;i<2;i++) {
        if(!path_for(paths[i],sizeof(paths[i]),root,key,i))return false;
        serials[i]=probe(paths[i],key,revision,false);
        uint8_t header[80];int source=0;cJSON *ref=reference_meta(root,key,revision,i,header,&source);
        if(ref)serials[i+2]=(uint32_t)cJSON_GetObjectItemCaseSensitive(ref,"serial")->valuedouble;
        cJSON_Delete(ref);
    }
    for(int attempt=0;attempt<4;attempt++) {
        int candidate=0;for(int i=1;i<4;i++)if(serials[i]>serials[candidate])candidate=i;
        if(!serials[candidate])break;
        serials[candidate]=0;int slot=candidate;
        uint8_t ref_header[80];cJSON *ref=NULL;
        if(candidate>=2){ref=reference_meta(root,key,revision,candidate-2,ref_header,&slot);if(!ref)continue;}
        FILE *f=store_fopen(paths[slot],"rb");if(!f){io_error();cJSON_Delete(ref);continue;}
        uint8_t header[80],hash[32];cJSON *meta=metadata(f,header,key,ref?"":revision);size_t capacity=0;
        if(ref&&meta&&(memcmp(header+48,ref_header+48,32)||strcmp(text(meta,"workId"),text(ref,"workId")))){cJSON_Delete(meta);meta=NULL;}
        uint32_t size=meta?u32(header+12):0;uint8_t *jpeg=meta?allocate(size,&capacity):NULL;
        bool ok=jpeg&&capacity>=size&&store_fread(jpeg,1,size,f)==size&&record_end(f,header)&&!ferror(f)&&
            mbedtls_sha256(jpeg,size,hash,0)==0&&!memcmp(hash,header+48,32);
        if(ferror(f))io_error();
        if(fclose(f)){io_error();ok=false;}
        if(ref){cJSON_Delete(meta);meta=ref;ref=NULL;}
        if(ok) {
            out->key=*key;out->key.pixels=NULL;out->key.frames=cJSON_GetObjectItemCaseSensitive(meta,"frames")->valueint;
            snprintf(out->key.work_id,sizeof(out->key.work_id),"%s",text(meta,"workId"));
            out->data=jpeg;out->size=size;out->capacity=capacity;
            *guide=cJSON_DetachItemFromObjectCaseSensitive(meta,"guide");cJSON_Delete(meta);return true;
        }
        free(jpeg);cJSON_Delete(meta);
    }
    return false;
}
static cJSON *guide_json(const gallery_guide *g) {
    if(!g)return NULL;
    cJSON *o=cJSON_CreateObject();if(!o)return NULL;
#define FIELD(name,member) if(!cJSON_AddStringToObject(o,name,g->member)){cJSON_Delete(o);return NULL;}
    FIELD("workId",work_id);FIELD("title",title);FIELD("author",author);FIELD("period",period);FIELD("museum",museum);FIELD("brief",brief);
    FIELD("overview",overview);FIELD("looking",looking);FIELD("transcription",transcription);FIELD("explanation",explanation);
#undef FIELD
    if(g->transcription_page_count>1) {
        if(g->transcription_page_count>32||!cJSON_AddNumberToObject(o,"transcriptionPages",g->transcription_page_count)||
           !cJSON_AddStringToObject(o,"transcriptionSha256",g->transcription_sha256)){cJSON_Delete(o);return NULL;}
        cJSON *pages=cJSON_AddArrayToObject(o,"transcriptionPageTexts");if(!pages){cJSON_Delete(o);return NULL;}
        for(int i=0;i<g->transcription_page_count;i++) {
            if(!g->transcription_pages[i]||strlen(g->transcription_pages[i])>4095){cJSON_Delete(o);return NULL;}
            cJSON *page=cJSON_CreateString(g->transcription_pages[i]);
            if(!page||!cJSON_AddItemToArray(pages,page)){cJSON_Delete(page);cJSON_Delete(o);return NULL;}
        }
    }
    return o;
}
static bool store_put(const char *root,const gallery_jpeg *v,const char *revision,const gallery_guide *guide,const gallery_resume *progress) {
    media_error=false;
    if(!v||!v->data||v->size<16||v->size>JPEG_LIMIT||v->capacity<v->size||!key_ok(&v->key)||!revision_ok(revision)||
       v->key.frames<=v->key.frame||v->key.frames>64||
       (guide&&(strcmp(guide->work_id,v->key.work_id)||strcmp(guide->date,v->key.date))))return false;
    if(!directories(root,v->key.category,v->key.date))return false;
    char paths[2][512],temp[520];uint32_t serial[2];
    for(int i=0;i<2;i++){if(!path_for(paths[i],sizeof(paths[i]),root,&v->key,i))return false;serial[i]=probe(paths[i],&v->key,"",true);}
    uint32_t newest=serial[0]>serial[1]?serial[0]:serial[1];if(newest==UINT32_MAX)return false;
    unsigned slot=serial[0]<=serial[1]?0:1;
    for(int i=0;i<2;i++) {
        uint8_t ref_header[80];int source=0;errno=0;cJSON *ref=reference_meta(root,&v->key,"",i,ref_header,&source);
        if(!ref&&errno==ENOMEM)return false;
        uint32_t number=ref?(uint32_t)cJSON_GetObjectItemCaseSensitive(ref,"serial")->valuedouble:0;
        if(number>newest&&serial[source]){newest=number;slot=source^1;}
        cJSON_Delete(ref);
    }
    if(newest>=UINT32_MAX-1)return false;
    cJSON *meta=cJSON_CreateObject();if(!meta)return false;
    bool ok=cJSON_AddNumberToObject(meta,"serial",newest+1)&&cJSON_AddStringToObject(meta,"revision",revision)&&
        cJSON_AddNumberToObject(meta,"category",v->key.category)&&cJSON_AddNumberToObject(meta,"frame",v->key.frame)&&
        cJSON_AddNumberToObject(meta,"frames",v->key.frames)&&cJSON_AddBoolToObject(meta,"portrait",v->key.portrait)&&
        cJSON_AddStringToObject(meta,"date",v->key.date)&&cJSON_AddStringToObject(meta,"workId",v->key.work_id);
    if(guide){cJSON *g=guide_json(guide);if(!g)ok=false;else if(!cJSON_AddItemToObject(meta,"guide",g)){cJSON_Delete(g);ok=false;}}
    char *json=ok?cJSON_PrintUnformatted(meta):NULL;cJSON_Delete(meta);
    size_t m=json?strlen(json):0;uint8_t header[80]={0};memcpy(header,magic,8);set32(header+8,m);set32(header+12,v->size);
    ok=m&&m<=META_LIMIT&&mbedtls_sha256((uint8_t *)json,m,header+16,0)==0&&mbedtls_sha256(v->data,v->size,header+48,0)==0;
    bool promote=progress&&progress->direct&&progress->offset==v->size;
    if(promote) {
        char checkpoint[512],sha[65];uint8_t expected[76],found[76];
        for(int i=0;i<32;i++)sprintf(sha+i*2,"%02x",header[48+i]);
        ok=ok&&direct_path(temp,root,&v->key,"aig.part")&&direct_path(checkpoint,root,&v->key,"aic")&&resume_header(&v->key,revision,sha,v->size,expected);
        FILE *check=ok?store_fopen(checkpoint,"rb"):NULL;
        ok=check&&store_fread(found,1,76,check)==76&&!memcmp(found,expected,76);
        if(check&&fclose(check))ok=false;
        memcpy(header,magic_tail,8);
    }else snprintf(temp,sizeof(temp),"%s.part",paths[slot]);
    FILE *f=ok?store_fopen(temp,promote?"r+b":"wb"):NULL;
    if(f) {
        if(promote) {
            uint8_t existing[80];
            ok=store_fread(existing,1,80,f)==80&&!memcmp(existing,magic_tail,8)&&u32(existing+12)==v->size&&
                !fseek(f,0,SEEK_END)&&ftell(f)>=(long)(80+v->size)&&!store_truncate(f,80+v->size)&&
                !fseek(f,80+v->size,SEEK_SET)&&store_fwrite(json,1,m,f)==m&&!fseek(f,0,SEEK_SET)&&store_fwrite(header,1,80,f)==80;
        }else ok=store_fwrite(header,1,80,f)==80&&store_fwrite(json,1,m,f)==m&&store_fwrite(v->data,1,v->size,f)==v->size;
        ok=ok&&fflush(f)==0&&store_sync_profiled(f)==0;
        if(!ok)io_error();
        if(fclose(f)){io_error();ok=false;}
    }else {if(ok)io_error();ok=false;}
    free(json);
    if(ok) {
        // The other complete record survives every failure here.
        if(remove(paths[slot])&&errno!=ENOENT){io_error();ok=false;}
        if(ok&&rename(temp,paths[slot])){io_error();ok=false;}
    }
    if(!ok&&!promote)remove(temp);
    return ok;
}

bool gallery_store_put(const char *root,const gallery_jpeg *v,const char *revision,const gallery_guide *guide){return store_put(root,v,revision,guide,NULL);}
bool gallery_store_finish(const char *root,const gallery_jpeg *v,const char *revision,const gallery_guide *guide,const gallery_resume *progress){return store_put(root,v,revision,guide,progress);}

bool gallery_store_refresh(const char *root,const gallery_jpeg *v,const char *revision,const gallery_guide *guide) {
    media_error=false;
    if(!v||!v->data||v->size<16||v->size>JPEG_LIMIT||v->capacity<v->size||!key_ok(&v->key)||!revision_ok(revision)||
       v->key.frames<=v->key.frame||v->key.frames>64||
       (guide&&(strcmp(guide->work_id,v->key.work_id)||strcmp(guide->date,v->key.date))))return false;
    uint8_t jpeg_hash[32];if(mbedtls_sha256(v->data,v->size,jpeg_hash,0))return false;
    uint32_t newest=0,source_serial=0,ref_serials[2]={0},matches[2]={0};int source=-1;
    for(int i=0;i<2;i++) {
        char path[512];uint8_t header[80];if(!path_for(path,sizeof(path),root,&v->key,i))return false;
        FILE *f=store_fopen(path,"rb");cJSON *m=f?metadata(f,header,&v->key,""):NULL;
        uint32_t number=m?(uint32_t)cJSON_GetObjectItemCaseSensitive(m,"serial")->valuedouble:0;
        bool match=m&&strcmp(text(m,"workId"),v->key.work_id)==0&&u32(header+12)==v->size&&!memcmp(header+48,jpeg_hash,32);
        cJSON_Delete(m);if(f&&fclose(f))return false;
        if(number>newest)newest=number;
        // Do not promote a corrupt existing JPEG just because its header matches.
        if(match)matches[i]=number;
    }
    // Prefer the newest matching image and read its payload only once. Probe
    // the older slot only if the preferred image is corrupt or unavailable.
    for(int attempt=0;attempt<2;attempt++) {
        unsigned slot=matches[0]>=matches[1]?0:1;uint32_t number=matches[slot];
        if(!number)break;
        matches[slot]=0;char path[512];
        if(!path_for(path,sizeof(path),root,&v->key,slot))return false;
        if(probe(path,&v->key,"",true)==number){source=slot;source_serial=number;break;}
    }
    if(source<0)return false;
    for(int i=0;i<2;i++) {
        uint8_t header[80];int at=0;errno=0;cJSON *m=reference_meta(root,&v->key,"",i,header,&at);
        if(!m&&errno==ENOMEM)return false;
        if(m)ref_serials[i]=(uint32_t)cJSON_GetObjectItemCaseSensitive(m,"serial")->valuedouble;
        if(ref_serials[i]>newest)newest=ref_serials[i];
        cJSON_Delete(m);
    }
    if(newest>=UINT32_MAX-1)return false;
    unsigned slot=ref_serials[0]<=ref_serials[1]?0:1;
    cJSON *meta=cJSON_CreateObject();if(!meta)return false;
    bool ok=cJSON_AddNumberToObject(meta,"serial",newest+1)&&cJSON_AddStringToObject(meta,"revision",revision)&&
        cJSON_AddNumberToObject(meta,"category",v->key.category)&&cJSON_AddNumberToObject(meta,"frame",v->key.frame)&&
        cJSON_AddNumberToObject(meta,"frames",v->key.frames)&&cJSON_AddBoolToObject(meta,"portrait",v->key.portrait)&&
        cJSON_AddStringToObject(meta,"date",v->key.date)&&cJSON_AddStringToObject(meta,"workId",v->key.work_id)&&
        cJSON_AddNumberToObject(meta,"sourceSlot",source)&&cJSON_AddNumberToObject(meta,"sourceSerial",source_serial);
    if(guide){cJSON *g=guide_json(guide);if(!g)ok=false;else if(!cJSON_AddItemToObject(meta,"guide",g)){cJSON_Delete(g);ok=false;}}
    char *json=ok?cJSON_PrintUnformatted(meta):NULL;cJSON_Delete(meta);size_t length=json?strlen(json):0;
    uint8_t header[80]={0};memcpy(header,magic_ref,8);set32(header+8,length);set32(header+12,v->size);memcpy(header+48,jpeg_hash,32);
    char path[520],temp[528]={0};ok=length&&length<=META_LIMIT&&!mbedtls_sha256((uint8_t *)json,length,header+16,0)&&reference_path(path,root,&v->key,slot);
    if(ok)snprintf(temp,sizeof(temp),"%s.part",path);
    FILE *f=ok?store_fopen(temp,"wb"):NULL;
    if(f){ok=store_fwrite(header,1,80,f)==80&&store_fwrite(json,1,length,f)==length&&fflush(f)==0&&store_sync_profiled(f)==0;if(fclose(f))ok=false;}
    else ok=false;
    free(json);
    if(ok){if(remove(path)&&errno!=ENOENT)ok=false;if(ok&&rename(temp,path))ok=false;}
    if(!ok){io_error();if(*temp)remove(temp);}
    return ok;
}

// Append only new ranges, not the entire growing prefix. A torn last record is
// ignored; verified earlier records survive a lost link, reboot or card removal.
// The header binds date, work, layout, frame, pack revision and expected JPEG.
static bool resume_header(const gallery_image *key,const char *revision,const char *sha,size_t total,uint8_t header[76]) {
    if(!key_ok(key)||!revision_ok(revision)||!*revision||!sha||strlen(sha)!=64||strspn(sha,"0123456789abcdef")!=64||
       total<16||total>JPEG_LIMIT||key->frames<=key->frame||key->frames>64||!memchr(key->work_id,0,sizeof(key->work_id)))return false;
    uint8_t identity[236]={0};set32(identity,key->category);set32(identity+4,key->frame);set32(identity+8,key->frames);
    identity[12]=key->portrait;memcpy(identity+13,key->date,11);memcpy(identity+24,key->work_id,strlen(key->work_id));
    memcpy(identity+104,revision,64);memcpy(identity+168,sha,64);set32(identity+232,total);
    memset(header,0,76);memcpy(header,"AIBRES01",8);set32(header+8,total);
    return !mbedtls_sha256(identity,sizeof(identity),header+12,0)&&!mbedtls_sha256(header,44,header+44,0);
}
static bool resume_path(char path[512],const char *root,const gallery_image *key) {
    if(!root||!key_ok(key))return false;
    int n=snprintf(path,512,"%s/%s/download.aip",root,key->category?SD_STORAGE_CALLIGRAPHY_DIR:SD_STORAGE_PAINTING_DIR);
    return n>0&&n<512;
}
static bool legacy_resume_load(const char *root,const gallery_image *key,const char *revision,const char *sha,uint8_t *jpeg,size_t total,gallery_resume *state) {
    media_error=false;if(!state)return false;*state=(gallery_resume){0};
    uint8_t expected[76],header[76];char path[512];
    if(!jpeg||!resume_header(key,revision,sha,total,expected)||!resume_path(path,root,key))return false;
    FILE *f=store_fopen(path,"rb");if(!f){io_error();return false;}
    bool ok=store_fread(header,1,76,f)==76&&!memcmp(header,expected,76);
    if(ok) {
        state->file_bytes=76;
        while(state->offset<total) {
            uint8_t range[40],hash[32];
            if(store_fread(range,1,40,f)!=40)break;
            size_t count=u32(range+4);
            if(u32(range)!=state->offset||!count||count>GALLERY_RESUME_BLOCK||count>total-state->offset||
               store_fread(jpeg+state->offset,1,count,f)!=count||mbedtls_sha256(jpeg+state->offset,count,hash,0)||memcmp(hash,range+8,32))break;
            state->offset+=count;state->file_bytes+=40+count;
        }
    }
    if(ferror(f))io_error();
    if(fclose(f))io_error();
    return ok;
}
#ifdef GALLERY_STORE_TEST
static bool legacy_resume_append(const char *root,const gallery_image *key,const char *revision,const char *sha,const uint8_t *jpeg,size_t total,size_t end,gallery_resume *state) {
    media_error=false;uint8_t header[76],found[76],range[40]={0};char path[512];
    if(!state||!jpeg||!resume_header(key,revision,sha,total,header)||!resume_path(path,root,key)||end>total||end<=state->offset||
       end-state->offset>GALLERY_RESUME_BLOCK||(state->offset&&state->file_bytes<76)||!directories(root,key->category,NULL))return false;
    size_t count=end-state->offset;set32(range,state->offset);set32(range+4,count);
    if(mbedtls_sha256(jpeg+state->offset,count,range+8,0))return false;
    FILE *f=store_fopen(path,state->offset?"r+b":"wb");if(!f){io_error();return false;}
    bool ok;
    if(state->offset)ok=store_fread(found,1,76,f)==76&&!memcmp(found,header,76)&&!fseek(f,0,SEEK_END)&&
        ftell(f)>=(long)state->file_bytes&&!store_truncate(f,state->file_bytes)&&!fseek(f,(long)state->file_bytes,SEEK_SET);
    else ok=store_fwrite(header,1,76,f)==76;
    ok=ok&&store_fwrite(range,1,40,f)==40&&store_fwrite(jpeg+state->offset,1,count,f)==count&&!fflush(f)&&!store_sync_profiled(f);
    if(!ok)io_error();
    if(fclose(f)){io_error();ok=false;}
    if(ok){state->file_bytes=(state->offset?state->file_bytes:76)+40+count;state->offset=end;}return ok;
}
#endif
static bool legacy_resume_clear(const char *root,const gallery_image *key,const char *revision,const char *sha,size_t total) {
    media_error=false;uint8_t expected[76],header[76];char path[512];
    if(!resume_header(key,revision,sha,total,expected)||!resume_path(path,root,key))return false;
    FILE *f=store_fopen(path,"rb");if(!f){io_error();return errno==ENOENT;}
    bool same=store_fread(header,1,76,f)==76&&!memcmp(header,expected,76);if(ferror(f))io_error();if(fclose(f))io_error();
    if(!same)return false;
    bool ok=!remove(path);if(!ok)io_error();return ok;
}

// Raw JPEG bytes are written once into their eventual AIG2 record. A tiny
// append-only journal is fsynced after the payload, so a torn checkpoint never
// endorses bytes that have not reached the card. Older AIP resumes are imported.
static bool direct_resume_load(const char *root,const gallery_image *key,const char *revision,const char *sha,uint8_t *jpeg,size_t total,gallery_resume *state) {
    uint8_t expected[76],found[76],draft[80];char journal[512],data[512];
    *state=(gallery_resume){0};
    if(!resume_header(key,revision,sha,total,expected)||!direct_path(journal,root,key,"aic")||!direct_path(data,root,key,"aig.part"))return false;
    FILE *index=store_fopen(journal,"rb"),*image=store_fopen(data,"rb");bool ok=index&&image&&store_fread(found,1,76,index)==76&&!memcmp(found,expected,76)&&
        store_fread(draft,1,80,image)==80&&!memcmp(draft,magic_tail,8)&&u32(draft+12)==total;
    if(ok) {
        state->file_bytes=76;state->direct=true;
        while(state->offset<total) {
            uint8_t range[40],hash[32];if(store_fread(range,1,40,index)!=40)break;
            size_t count=u32(range+4);
            if(u32(range)!=state->offset||!count||count>GALLERY_RESUME_BLOCK||count>total-state->offset||
               store_fread(jpeg+state->offset,1,count,image)!=count||mbedtls_sha256(jpeg+state->offset,count,hash,0)||memcmp(hash,range+8,32))break;
            state->offset+=count;state->file_bytes+=40;
        }
    }
    if(index){if(ferror(index))io_error();if(fclose(index))io_error();}
    if(image){if(ferror(image))io_error();if(fclose(image))io_error();}
    return ok;
}
bool gallery_resume_load(const char *root,const gallery_image *key,const char *revision,const char *sha,uint8_t *jpeg,size_t total,gallery_resume *state) {
    media_error=false;if(!state||!jpeg)return false;
    gallery_resume old={0},direct={0};bool legacy=legacy_resume_load(root,key,revision,sha,jpeg,total,&old);
    bool current=direct_resume_load(root,key,revision,sha,jpeg,total,&direct);
    if(current&&direct.offset>=old.offset){*state=direct;return true;}
    if(legacy){legacy_resume_load(root,key,revision,sha,jpeg,total,&old);*state=old;return true;}
    *state=(gallery_resume){0};return false;
}
static bool direct_checkpoint(FILE *index,const uint8_t *jpeg,size_t begin,size_t end) {
    uint8_t range[40]={0};set32(range,begin);set32(range+4,end-begin);
    return !mbedtls_sha256(jpeg+begin,end-begin,range+8,0)&&store_fwrite(range,1,40,index)==40;
}
bool gallery_resume_append(const char *root,const gallery_image *key,const char *revision,const char *sha,const uint8_t *jpeg,size_t total,size_t end,gallery_resume *state) {
    media_error=false;uint8_t header[76],found[76];char journal[512],data[512];
    if(!state||!jpeg||!resume_header(key,revision,sha,total,header)||!direct_path(journal,root,key,"aic")||!direct_path(data,root,key,"aig.part")||
       end>total||end<=state->offset||end-state->offset>GALLERY_RESUME_BLOCK||!directories(root,key->category,NULL))return false;
    bool fresh=!state->direct;
    FILE *image=store_fopen(data,fresh?"w+b":"r+b");if(!image){io_error();return false;}
    uint8_t draft[80]={0};memcpy(draft,magic_tail,8);set32(draft+12,total);
    bool ok=fresh?store_fwrite(draft,1,80,image)==80&&(!state->offset||store_fwrite(jpeg,1,state->offset,image)==state->offset):
        store_fread(draft,1,80,image)==80&&!memcmp(draft,magic_tail,8)&&u32(draft+12)==total;
    ok=ok&&!fseek(image,80+state->offset,SEEK_SET)&&store_fwrite(jpeg+state->offset,1,end-state->offset,image)==end-state->offset&&!fflush(image)&&!store_sync_profiled(image);
    if(!ok)io_error();
    if(fclose(image)){io_error();ok=false;}if(!ok)return false;
    FILE *index=store_fopen(journal,fresh?"w+b":"r+b");if(!index){io_error();return false;}
    size_t file_bytes=state->file_bytes;
    if(fresh) {
        ok=store_fwrite(header,1,76,index)==76;file_bytes=76;
        for(size_t at=0;ok&&at<state->offset;){size_t stop=state->offset-at>GALLERY_RESUME_BLOCK?at+GALLERY_RESUME_BLOCK:state->offset;ok=direct_checkpoint(index,jpeg,at,stop);file_bytes+=40;at=stop;}
    }else ok=store_fread(found,1,76,index)==76&&!memcmp(found,header,76)&&file_bytes>=76&&!fseek(index,0,SEEK_END)&&ftell(index)>=(long)file_bytes&&
        !store_truncate(index,file_bytes)&&!fseek(index,file_bytes,SEEK_SET);
    ok=ok&&direct_checkpoint(index,jpeg,state->offset,end)&&!fflush(index)&&!store_sync_profiled(index);
    if(!ok)io_error();
    if(fclose(index)){io_error();ok=false;}
    if(ok){state->offset=end;state->file_bytes=file_bytes+40;state->direct=true;}return ok;
}
bool gallery_resume_clear(const char *root,const gallery_image *key,const char *revision,const char *sha,size_t total) {
    media_error=false;uint8_t header[76],found[76];char journal[512],data[512];
    bool legacy=legacy_resume_clear(root,key,revision,sha,total);
    if(!resume_header(key,revision,sha,total,header)||!direct_path(journal,root,key,"aic")||!direct_path(data,root,key,"aig.part"))return false;
    FILE *f=store_fopen(journal,"rb");if(!f){io_error();return legacy;}
    bool same=store_fread(found,1,76,f)==76&&!memcmp(found,header,76);if(fclose(f))io_error();if(!same)return false;
    bool ok=(!remove(data)||errno==ENOENT)&&!remove(journal);if(!ok)io_error();return ok;
}

// Small, independently checksummed cursor. Only a completely saved work moves
// next forward; after an interrupted work, its verified pages are reused.
static bool catalog_path(char out[512],const char *root,int category,unsigned slot) {
    if(!root||category<0||category>1||slot>1)return false;
    int n=snprintf(out,512,"%s/%s/catalog.%u",root,category?SD_STORAGE_CALLIGRAPHY_DIR:SD_STORAGE_PAINTING_DIR,slot);
    return n>0&&n<512;
}
static uint32_t catalog_read(const char *path,const char *revision,gallery_catalog *out) {
    uint8_t bytes[116],hash[32];FILE *f=store_fopen(path,"rb");if(!f){io_error();return 0;}
    bool ok=store_fread(bytes,1,sizeof(bytes),f)==sizeof(bytes)&&fgetc(f)==EOF&&!ferror(f);
    if(ferror(f))io_error();
    if(fclose(f)){io_error();ok=false;}
    if(!ok||memcmp(bytes,"AIBCAT01",8)||mbedtls_sha256(bytes,84,hash,0)||memcmp(hash,bytes+84,32))return 0;
    gallery_catalog value={.works=u32(bytes+12),.next=u32(bytes+16)};memcpy(value.revision,bytes+20,64);
    uint32_t serial=u32(bytes+8);
    if(!serial||serial==UINT32_MAX||value.works<1||value.works>4096||value.next>value.works||
       strlen(value.revision)!=64||!revision_ok(value.revision)||(*revision&&strcmp(revision,value.revision)))return 0;
    if(out)*out=value;
    return serial;
}
bool gallery_catalog_load(const char *root,int category,const char *revision,gallery_catalog *out) {
    media_error=false;if(!out||!revision_ok(revision))return false;
    uint32_t newest=0;char path[512];gallery_catalog candidate;
    for(unsigned i=0;i<2;i++) {
        if(!catalog_path(path,root,category,i))return false;
        uint32_t serial=catalog_read(path,revision,&candidate);
        if(serial>newest){newest=serial;*out=candidate;}
    }
    return newest!=0;
}
bool gallery_catalog_save(const char *root,int category,const gallery_catalog *value) {
    media_error=false;
    if(!value||strlen(value->revision)!=64||!revision_ok(value->revision)||value->works<1||value->works>4096||
       value->next>value->works||category<0||category>1||!directories(root,category,NULL))return false;
    char paths[2][512],temp[520];uint32_t serial[2];
    for(unsigned i=0;i<2;i++){if(!catalog_path(paths[i],root,category,i))return false;serial[i]=catalog_read(paths[i],"",NULL);}
    uint32_t newest=serial[0]>serial[1]?serial[0]:serial[1];if(newest>=UINT32_MAX-1)return false;
    unsigned slot=serial[0]<=serial[1]?0:1;
    uint8_t bytes[116]={0};memcpy(bytes,"AIBCAT01",8);set32(bytes+8,newest+1);set32(bytes+12,value->works);set32(bytes+16,value->next);
    memcpy(bytes+20,value->revision,64);if(mbedtls_sha256(bytes,84,bytes+84,0))return false;
    snprintf(temp,sizeof(temp),"%s.part",paths[slot]);FILE *f=store_fopen(temp,"wb");bool ok=false;
    if(f){ok=store_fwrite(bytes,1,sizeof(bytes),f)==sizeof(bytes)&&fflush(f)==0&&store_sync_profiled(f)==0;if(!ok)io_error();if(fclose(f)){io_error();ok=false;}}
    else io_error();
    if(ok&&remove(paths[slot])&&errno!=ENOENT){io_error();ok=false;}
    if(ok&&rename(temp,paths[slot])){io_error();ok=false;}
    if(!ok)remove(temp);
    return ok;
}
static bool leap(int year){return year%4==0&&(year%100!=0||year%400==0);}
static int month_days(int year,int month){static const int days[]={31,28,31,30,31,30,31,31,30,31,30,31};return days[month-1]+(month==2&&leap(year));}
bool gallery_catalog_date(unsigned index,char out[11]) {
    if(!out||index>=4096)return false;
    int year=2026,month=1,day=1;
    while(index){unsigned remaining=month_days(year,month)-day+1;if(index<remaining){day+=index;break;}index-=remaining;day=1;if(++month==13){month=1;year++;}}
    char date[40];
    if(snprintf(date,sizeof(date),"%04d-%02d-%02d",year,month,day)!=10)return false;
    memcpy(out,date,11);return true;
}
bool gallery_catalog_key(const char *date,unsigned works,char out[11]) {
    if(!date||strlen(date)!=10||!out||!works||works>4096)return false;
    for(int i=0;i<10;i++)if(i==4||i==7){if(date[i]!='-')return false;}else if(date[i]<'0'||date[i]>'9')return false;
    int year=0,month=0,day=0;if(sscanf(date,"%d-%d-%d",&year,&month,&day)!=3||year<1||year>9999||month<1||month>12||day<1||day>month_days(year,month))return false;
    int y=year-1,number=y*365+y/4-y/100+y/400+day-1;
    for(int m=1;m<month;m++)number+=month_days(year,m);
    int base=2025*365+2025/4-2025/100+2025/400;
    int index=(number-base)%(int)works;if(index<0)index+=works;
    return gallery_catalog_date(index,out);
}

bool gallery_speed_prepare(const char *mount) {
    media_error=false;if(!mount)return false;
    const char *parts[]={SD_STORAGE_APP_DIR,SD_STORAGE_CACHE_DIR,SD_STORAGE_CACHE_DIR "/gallery-speed"};
    for(unsigned i=0;i<3;i++) {
        char path[512];int n=snprintf(path,sizeof(path),"%s/%s",mount,parts[i]);
        if(n<0||n>=sizeof(path))return false;
        if(store_mkdir(path)&&errno!=EEXIST){io_error();return false;}
    }
    return true;
}
