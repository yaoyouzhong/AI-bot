#undef NDEBUG
#include <assert.h>
#include <windows.h>
#include <bcrypt.h>
#include <stdio.h>
#include <stdlib.h>
#include <errno.h>
#include <io.h>
#include "gallery_store.h"
static int mbedtls_sha256(const uint8_t *bytes,size_t size,uint8_t *out,int variant) {
    (void)variant;BCRYPT_ALG_HANDLE provider=NULL;
    assert(BCryptOpenAlgorithmProvider(&provider,BCRYPT_SHA256_ALGORITHM,NULL,0)==0);
    assert(BCryptHash(provider,NULL,0,(PUCHAR)bytes,(ULONG)size,out,32)==0);BCryptCloseAlgorithmProvider(provider,0);return 0;
}
static size_t disk_written;
static bool full_card,fail_rename,probe_no_memory,guide_no_memory;
static cJSON_bool test_add_item(cJSON *object,const char *name,cJSON *item){if(guide_no_memory&&!strcmp(name,"guide")){errno=ENOMEM;return false;}return cJSON_AddItemToObject(object,name,item);}
static void *test_malloc(size_t size){if(probe_no_memory&&size>40000){errno=ENOMEM;return NULL;}return malloc(size);}
static unsigned bulk_writes;
static int test_native_write(int fd,const void *p,unsigned count){
    if(full_card){errno=ENOSPC;return 0;}
    if(count>=512){assert(!((uintptr_t)p%512)&&!(_tell(fd)%512)&&!(count%512));bulk_writes++;}
    int n=_write(fd,p,count);if(n>0)disk_written+=n;return n;
}
static int test_rename(const char *from,const char *to){if(fail_rename){errno=EACCES;return -1;}return rename(from,to);}
#define GALLERY_STORE_TEST
#define STORE_NATIVE_WRITE test_native_write
#define rename test_rename
#define malloc test_malloc
#define cJSON_AddItemToObject test_add_item
#include "../../firmware/main/gallery_store.c"
#undef rename
#undef malloc
#undef cJSON_AddItemToObject
static uint8_t *allocate(size_t n,size_t *capacity){*capacity=n;return malloc(n);}
static void release(gallery_jpeg *v,cJSON *g){free(v->data);cJSON_Delete(g);}
static void check_bulk_io(const char *root){
    char path[512];snprintf(path,sizeof(path),"%s/io.tmp",root);
    const size_t n=100003;uint8_t *source=malloc(n+1),*target=malloc(n+3);assert(source&&target);
    for(size_t i=0;i<n;i++)source[i+1]=(uint8_t)(i*31+17);
    const unsigned offsets[]={0,80,537,8191};
    for(unsigned i=0;i<sizeof(offsets)/sizeof(*offsets);i++){
        FILE *f=store_fopen(path,"w+b");assert(f);assert(!fseek(f,offsets[i],SEEK_SET));
        gallery_store_profile_reset();unsigned before=bulk_writes;
        assert(store_fwrite(source+1,1,n,f)==n&&ftell(f)==offsets[i]+n);
        assert(!fseek(f,offsets[i],SEEK_SET));assert(store_fread(target+3,1,n,f)==n&&ftell(f)==offsets[i]+n);
        assert(!memcmp(source+1,target+3,n));gallery_io_profile p=gallery_store_profile_get();
        assert(p.read_bytes==n&&p.write_bytes==n&&p.bulk_bytes>2*n-2048&&p.edge_bytes<2048);
        assert(bulk_writes-before>=3&&bulk_writes-before<=4);assert(!fclose(f));
    }
    assert(!remove(path));free(source);free(target);
    puts("GALLERY_ALIGNED_IO_OK legacy offsets; unaligned source/destination; multi-sector syscalls; exact bytes/position");
}
static void check_incremental_metadata(const char *root) {
    size_t size=1048576;uint8_t *bytes=malloc(size);assert(bytes);memset(bytes,37,size);
    gallery_jpeg value={.key={.category=1,.frame=3,.frames=8,.portrait=true},.data=bytes,.size=size,.capacity=size};
    strcpy(value.key.date,"2026-02-02");strcpy(value.key.work_id,"museum:incremental");
    gallery_guide guide={0};strcpy(guide.date,value.key.date);strcpy(guide.work_id,value.key.work_id);strcpy(guide.overview,"old guide");
    char a[65],b[65],c[65],d[65];memset(a,'a',64);memset(b,'b',64);memset(c,'c',64);memset(d,'d',64);a[64]=b[64]=c[64]=d[64]=0;
    assert(gallery_store_put(root,&value,a,&guide));
    gallery_store_profile_reset();assert(gallery_store_put(root,&value,b,&guide));gallery_io_profile legacy=gallery_store_profile_get();
    strcpy(guide.overview,"updated guide, identical JPEG");
    gallery_store_profile_reset();assert(gallery_store_refresh(root,&value,c,&guide));gallery_io_profile delta=gallery_store_profile_get();
    assert(legacy.write_bytes>size&&delta.write_bytes<2048&&delta.read_bytes<size+32768);
    gallery_jpeg got;cJSON *raw=NULL;
    assert(gallery_store_get(root,&value.key,c,allocate,&got,&raw)&&!strcmp(text(raw,"overview"),guide.overview)&&!memcmp(got.data,bytes,size));release(&got,raw);
    probe_no_memory=true;assert(!gallery_store_refresh(root,&value,d,&guide));probe_no_memory=false;
    guide_no_memory=true;assert(!gallery_store_refresh(root,&value,d,&guide));guide_no_memory=false;
    assert(gallery_store_get(root,&value.key,c,allocate,&got,&raw));release(&got,raw);
    full_card=true;assert(!gallery_store_refresh(root,&value,d,&guide));full_card=false;
    fail_rename=true;assert(!gallery_store_refresh(root,&value,d,&guide));fail_rename=false;
    assert(gallery_store_get(root,&value.key,c,allocate,&got,&raw));release(&got,raw);
    char path[520];assert(reference_path(path,root,&value.key,0));FILE *f=fopen(path,"r+b");assert(f);assert(!fseek(f,-1,SEEK_END));fputc('X',f);fclose(f);
    assert(!gallery_store_get(root,&value.key,c,allocate,&got,&raw));
    assert(gallery_store_get(root,&value.key,b,allocate,&got,&raw));release(&got,raw);
    assert(gallery_store_refresh(root,&value,c,&guide));
    bytes[0]=38;fail_rename=true;assert(!gallery_store_put(root,&value,d,&guide));fail_rename=false;
    assert(gallery_store_get(root,&value.key,c,allocate,&got,&raw)&&got.data[0]==37);release(&got,raw);
    assert(gallery_store_put(root,&value,d,&guide));
    assert(gallery_store_get(root,&value.key,"",allocate,&got,&raw)&&got.data[0]==38);release(&got,raw);
    assert(path_for(path,sizeof(path),root,&value.key,0));f=fopen(path,"r+b");assert(f);assert(!fseek(f,-1,SEEK_END));fputc(0,f);fclose(f);
    assert(!gallery_store_get(root,&value.key,d,allocate,&got,&raw));
    assert(gallery_store_get(root,&value.key,"",allocate,&got,&raw)&&got.data[0]==37&&!strcmp(text(raw,"overview"),guide.overview));release(&got,raw);
    assert(!gallery_store_refresh(root,&value,d,&guide)); // Never bind to a corrupt source image.
    bytes[0]=37;strcpy(value.key.work_id,"different:museum-object");strcpy(guide.work_id,value.key.work_id);
    assert(!gallery_store_refresh(root,&value,d,&guide));
    printf("GALLERY_INCREMENTAL_METADATA_OK jpegBytes=%u legacyWriteBytes=%u deltaWriteBytes=%u deltaReadBytes=%u; complete text, reboot, ENOSPC, rename, corrupt metadata/JPEG, survivor-slot and work identity checked\n",(unsigned)size,legacy.write_bytes,delta.write_bytes,delta.read_bytes);
    free(bytes);
}
int main(void) {
    char tmp[MAX_PATH],root[MAX_PATH];GetTempPathA(sizeof(tmp),tmp);snprintf(root,sizeof(root),"%saibot-tf-%lu",tmp,GetCurrentProcessId());assert(!_mkdir(root));
    check_bulk_io(root);check_incremental_metadata(root);
    uint8_t bytes[50000];for(unsigned i=0;i<sizeof(bytes);i++)bytes[i]=(uint8_t)i;
    gallery_jpeg value={.key={.category=0,.frame=0,.frames=3,.portrait=false},.data=bytes,.size=sizeof(bytes),.capacity=sizeof(bytes)};
    strcpy(value.key.date,"2026-10-11");strcpy(value.key.work_id,"museum:reviewed-work");
    gallery_guide guide={0};strcpy(guide.date,value.key.date);strcpy(guide.work_id,value.key.work_id);
    strcpy(guide.overview,"馆方介绍");strcpy(guide.looking,"有依据的欣赏提示");
    for(int i=0;i<8190;i+=3)memcpy(guide.transcription+i,"字",3);guide.transcription[8190]='x';
    char rev[65];memset(rev,'a',64);rev[64]=0;
    assert(gallery_store_put(root,&value,rev,&guide));
    char expected[512],actual[512];
    snprintf(expected,sizeof(expected),"%s/aibot/gallery/painting/works/2026-10-11/00-l.0.aig",root);
    assert(path_for(actual,sizeof(actual),root,&value.key,0)&&!strcmp(actual,expected)&&!_access(expected,0));
    WIN32_FIND_DATAA entry;char pattern[512];snprintf(pattern,sizeof(pattern),"%s/*",root);
    HANDLE find=FindFirstFileA(pattern,&entry);assert(find!=INVALID_HANDLE_VALUE);
    do {if(strcmp(entry.cFileName,".")&&strcmp(entry.cFileName,".."))assert(!strcmp(entry.cFileName,SD_STORAGE_APP_DIR));}while(FindNextFileA(find,&entry));FindClose(find);
    gallery_jpeg got;cJSON *raw=NULL;
    assert(gallery_store_get(root,&value.key,rev,allocate,&got,&raw));
    assert(!memcmp(got.data,bytes,sizeof(bytes))&&got.key.frames==3&&!strcmp(got.key.work_id,value.key.work_id));
    assert(!strcmp(text(raw,"overview"),guide.overview)&&!strcmp(text(raw,"looking"),guide.looking)&&strlen(text(raw,"transcription"))==8191&&!strcmp(text(raw,"transcription"),guide.transcription));release(&got,raw);
    // A fresh client with no bridge digest can reopen the last persisted record.
    assert(gallery_store_get(root,&value.key,"",allocate,&got,&raw));release(&got,raw);
    char next[65];strcpy(next,rev);next[0]='b';assert(!gallery_store_get(root,&value.key,next,allocate,&got,&raw));
    gallery_image other=value.key;other.portrait=true;assert(!gallery_store_get(root,&other,rev,allocate,&got,&raw));
    other=value.key;other.category=1;assert(!gallery_store_get(root,&other,rev,allocate,&got,&raw));
    other=value.key;strcpy(other.date,"../../oops");assert(!gallery_store_get(root,&other,rev,allocate,&got,&raw));
    // Failed write/full/read-only card leaves the previous revision usable.
    full_card=true;assert(!gallery_store_put(root,&value,next,&guide));full_card=false;
    assert(gallery_store_get(root,&value.key,rev,allocate,&got,&raw));release(&got,raw);
    fail_rename=true;assert(!gallery_store_put(root,&value,next,&guide));fail_rename=false;
    assert(gallery_store_get(root,&value.key,rev,allocate,&got,&raw));release(&got,raw);
    probe_no_memory=true;assert(!gallery_store_put(root,&value,next,&guide));probe_no_memory=false;
    assert(gallery_store_get(root,&value.key,rev,allocate,&got,&raw));release(&got,raw);
    guide_no_memory=true;assert(!gallery_store_put(root,&value,next,&guide));guide_no_memory=false;
    assert(gallery_store_get(root,&value.key,rev,allocate,&got,&raw));
    assert(!strcmp(text(raw,"transcription"),guide.transcription));release(&got,raw);
    assert(gallery_store_put(root,&value,next,&guide));
    assert(gallery_store_get(root,&value.key,next,allocate,&got,&raw));release(&got,raw);
    // Corrupt the newest JPEG: old complete record still loads; an interrupted
    // repair must not delete that good record in favor of the corrupt one.
    char file[512];assert(path_for(file,sizeof(file),root,&value.key,1));FILE *f=fopen(file,"r+b");assert(f);assert(!fseek(f,-1,SEEK_END));assert(fputc(123,f)!=EOF);assert(!fclose(f));
    assert(!gallery_store_get(root,&value.key,next,allocate,&got,&raw));
    assert(gallery_store_get(root,&value.key,"",allocate,&got,&raw));release(&got,raw);
    fail_rename=true;assert(!gallery_store_put(root,&value,next,&guide));fail_rename=false;
    assert(gallery_store_get(root,&value.key,rev,allocate,&got,&raw));release(&got,raw);
    assert(gallery_store_put(root,&value,next,&guide));
    assert(gallery_store_get(root,&value.key,next,allocate,&got,&raw));release(&got,raw);
    // Uncommitted .part is never a display source; unrelated user files survive.
    char unrelated[512];snprintf(unrelated,sizeof(unrelated),"%s/my-photo.jpg",root);f=fopen(unrelated,"wb");assert(f);fputs("user-photo",f);fclose(f);
    for(int i=0;i<2;i++){path_for(file,sizeof(file),root,&value.key,i);assert(!remove(file));}
    path_for(file,sizeof(file),root,&value.key,0);strcat(file,".part");f=fopen(file,"wb");assert(f);fputs("incomplete",f);fclose(f);
    assert(!gallery_store_get(root,&value.key,"",allocate,&got,&raw));assert(!_access(unrelated,0));
    gallery_catalog plan={.works=366,.next=17},loaded={0};strcpy(plan.revision,rev);
    assert(gallery_catalog_save(root,0,&plan)&&gallery_catalog_load(root,0,rev,&loaded)&&loaded.next==17&&loaded.works==366);
    plan.next=18;full_card=true;assert(!gallery_catalog_save(root,0,&plan));full_card=false;
    assert(gallery_catalog_load(root,0,rev,&loaded)&&loaded.next==17);
    fail_rename=true;assert(!gallery_catalog_save(root,0,&plan));fail_rename=false;
    assert(gallery_catalog_load(root,0,rev,&loaded)&&loaded.next==17);
    assert(gallery_catalog_save(root,0,&plan)&&gallery_catalog_load(root,0,rev,&loaded)&&loaded.next==18);
    assert(!gallery_catalog_load(root,1,rev,&loaded)&&!gallery_catalog_load(root,0,next,&loaded));
    assert(catalog_path(file,root,0,1));f=fopen(file,"r+b");assert(f);fputc('X',f);fclose(f);
    assert(gallery_catalog_load(root,0,rev,&loaded)&&loaded.next==17);
    char canonical[11];
    assert(gallery_catalog_key("2028-02-29",366,canonical)&&!strcmp(canonical,"2026-02-27"));
    assert(gallery_catalog_key("2028-02-29",3,canonical)&&!strcmp(canonical,"2026-01-01"));
    assert(gallery_catalog_key("2025-12-31",366,canonical)&&!strcmp(canonical,"2027-01-01"));
    assert(!gallery_catalog_key("2026-02-29",366,canonical)&&!gallery_catalog_key("2028-02-30",366,canonical));
    assert(!gallery_catalog_key("2028-02-29",4097,canonical)&&gallery_catalog_date(4095,canonical));
    // Resume data is bounded and never visible as a completed artwork. Simulate
    // reboot, torn/corrupt tails, full media and mismatched new content.
    uint8_t hash[32];char image_sha[65];mbedtls_sha256(bytes,sizeof(bytes),hash,0);
    for(int i=0;i<32;i++)sprintf(image_sha+2*i,"%02x",hash[i]);
    gallery_resume progress={0},restarted={0};uint8_t resumed[50000]={0};
    assert(gallery_resume_append(root,&value.key,rev,image_sha,bytes,sizeof(bytes),20000,&progress));
    assert(gallery_resume_load(root,&value.key,rev,image_sha,resumed,sizeof(resumed),&restarted)&&restarted.offset==20000&&!memcmp(resumed,bytes,20000));
    assert(!gallery_store_get(root,&value.key,rev,allocate,&got,&raw));
    gallery_resume mismatch={0};
    assert(!gallery_resume_load(root,&value.key,next,image_sha,resumed,sizeof(resumed),&mismatch)&&!mismatch.offset);
    other=value.key;other.portrait=true;assert(!gallery_resume_load(root,&other,rev,image_sha,resumed,sizeof(resumed),&mismatch));
    char wrong_sha[65];strcpy(wrong_sha,image_sha);wrong_sha[0]=wrong_sha[0]=='a'?'b':'a';
    assert(!gallery_resume_load(root,&value.key,rev,wrong_sha,resumed,sizeof(resumed),&mismatch));
    assert(!gallery_resume_load(root,&value.key,rev,image_sha,resumed,sizeof(resumed)-1,&mismatch));
    char resume_file[512];assert(direct_path(resume_file,root,&value.key,"aic"));
    f=fopen(resume_file,"ab");assert(f);fputs("torn range",f);assert(!fclose(f));
    assert(gallery_resume_load(root,&value.key,rev,image_sha,resumed,sizeof(resumed),&restarted)&&restarted.offset==20000);
    full_card=true;assert(!gallery_resume_append(root,&value.key,rev,image_sha,bytes,sizeof(bytes),30000,&restarted));full_card=false;
    assert(gallery_resume_load(root,&value.key,rev,image_sha,resumed,sizeof(resumed),&restarted)&&restarted.offset==20000);
    assert(gallery_resume_append(root,&value.key,rev,image_sha,bytes,sizeof(bytes),40000,&restarted));
    char resume_data[512];assert(direct_path(resume_data,root,&value.key,"aig.part"));f=fopen(resume_data,"r+b");assert(f);assert(!fseek(f,80+39999,SEEK_SET));fputc(77,f);assert(!fclose(f));
    assert(gallery_resume_load(root,&value.key,rev,image_sha,resumed,sizeof(resumed),&restarted)&&restarted.offset==20000);
    assert(gallery_resume_append(root,&value.key,rev,image_sha,bytes,sizeof(bytes),sizeof(bytes),&restarted));
    assert(gallery_resume_load(root,&value.key,rev,image_sha,resumed,sizeof(resumed),&restarted)&&restarted.offset==sizeof(bytes)&&!memcmp(resumed,bytes,sizeof(bytes)));
    assert(!gallery_resume_clear(root,&value.key,next,image_sha,sizeof(bytes))&&!_access(resume_file,0));
    size_t before_finish=disk_written;
    assert(gallery_store_finish(root,&value,rev,&guide,&restarted)&&gallery_resume_clear(root,&value.key,rev,image_sha,sizeof(bytes))&&_access(resume_file,0)&&!_access(unrelated,0));
    puts("GALLERY_RESUME_STORE_OK cold checked-prefix reload; torn/corrupt tail repair; ENOSPC preserves prefix; layout/revision/size/hash binding; final-only display; bounded per-category file; unrelated files retained");
    assert(disk_written-before_finish<20000); // metadata/header only; no second JPEG write.
    assert(gallery_store_get(root,&value.key,rev,allocate,&got,&raw)&&!memcmp(got.data,bytes,sizeof(bytes)));release(&got,raw);
    other=value.key;other.category=1;gallery_resume legacy={0},migrated={0};
    assert(legacy_resume_append(root,&other,rev,image_sha,bytes,sizeof(bytes),20000,&legacy));
    assert(gallery_resume_load(root,&other,rev,image_sha,resumed,sizeof(resumed),&migrated)&&!migrated.direct&&migrated.offset==20000);
    assert(gallery_resume_append(root,&other,rev,image_sha,bytes,sizeof(bytes),40000,&migrated)&&migrated.direct);
    assert(gallery_resume_load(root,&other,rev,image_sha,resumed,sizeof(resumed),&migrated)&&migrated.direct&&migrated.offset==40000);
    puts("GALLERY_SINGLE_WRITE_OK authenticated durable prefix; AIG2 promotion writes metadata only; old AIG1 reading and old AIP resume migration preserved");
    puts("GALLERY_CATALOG_OK resumable whole-library cursor; checked alternate records; failed-write/rename/corruption retention; leap/year canonical reuse");
    printf("GALLERY_STORE_OK image/text roundtrip, 8191-byte original, restart/offline, layout/category/revision isolation, corruption, ENOSPC/rename rollback, incomplete exclusion, user-file retention\n");
    return 0;
}
