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
static bool full_card,fail_rename,probe_no_memory,guide_no_memory,small_no_memory;
static cJSON_bool test_add_item(cJSON *object,const char *name,cJSON *item){if(guide_no_memory&&!strcmp(name,"guide")){errno=ENOMEM;return false;}return cJSON_AddItemToObject(object,name,item);}
static size_t workspace_size;static unsigned workspace_attempts,workspace_fail_at,workspace_live;static void *workspace_slots[128];
static void *test_malloc(size_t size){
    if((probe_no_memory&&size>40000)||(small_no_memory&&size<4096)){errno=ENOMEM;return NULL;}
    if(size==workspace_size&&++workspace_attempts==workspace_fail_at){errno=ENOMEM;return NULL;}
    void *p=malloc(size);if(p&&size==workspace_size){unsigned i=0;while(i<128&&workspace_slots[i])i++;assert(i<128);workspace_slots[i]=p;workspace_live++;}return p;
}
static void test_free(void *p){if(p)for(unsigned i=0;i<128;i++)if(workspace_slots[i]==p){workspace_slots[i]=NULL;assert(workspace_live);workspace_live--;break;}free(p);}

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
#define free test_free
#define cJSON_AddItemToObject test_add_item
#include "../../firmware/main/gallery_store.c"
#undef rename
#undef malloc
#undef free
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
static void check_workspace_failures(const char *root){
    uint8_t bytes[50000];memset(bytes,23,sizeof(bytes));
    char a[65],b[65];memset(a,'3',64);memset(b,'4',64);a[64]=b[64]=0;
    gallery_jpeg value={.key={.category=1,.frame=0,.frames=1},.data=bytes,.size=sizeof(bytes),.capacity=sizeof(bytes)};
    strcpy(value.key.work_id,"fixture:workspace");
    for(unsigned mode=0;mode<2;mode++)for(unsigned failure=1;failure<=6;failure++){
        snprintf(value.key.date,sizeof(value.key.date),"2034-%02u-%02u",mode+1,failure);
        gallery_guide guide={0};strcpy(guide.date,value.key.date);strcpy(guide.work_id,value.key.work_id);strcpy(guide.overview,"workspace OOM");
        assert(gallery_store_put(root,&value,a,&guide)&&!workspace_live);
        workspace_attempts=0;workspace_fail_at=failure;
        bool ok=mode?gallery_store_refresh(root,&value,b,&guide):gallery_store_put(root,&value,b,&guide);
        workspace_fail_at=0;assert(!workspace_live);
        gallery_jpeg got={0};cJSON *raw=NULL;assert(gallery_store_get(root,&value.key,ok?b:a,allocate,&got,&raw));release(&got,raw);assert(!workspace_live);
    }
    puts("GALLERY_PATH_WORKSPACE_OK nested allocation failures preserve completed records; every private path workspace released");
}
int main(void) {
    workspace_size=sizeof(gallery_paths);
    char tmp[MAX_PATH],root[MAX_PATH];GetTempPathA(sizeof(tmp),tmp);snprintf(root,sizeof(root),"%saibot-tf-%lu",tmp,GetCurrentProcessId());assert(!_mkdir(root));
    check_bulk_io(root);check_incremental_metadata(root);check_workspace_failures(root);
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
    // A path-workspace allocation failure must not publish or remove a record.
    small_no_memory=true;assert(!gallery_store_put(root,&value,rev,&guide));
    gallery_jpeg missing={0};cJSON *missing_guide=NULL;
    assert(!gallery_store_get(root,&value.key,next,allocate,&missing,&missing_guide)&&!missing.data&&!missing_guide);
    small_no_memory=false;assert(gallery_store_get(root,&value.key,next,allocate,&got,&raw));release(&got,raw);
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
    // Legacy firmware used just two shared slots. Preserve both revisions
    // before a new save overwrites them, including after repeated switches.
    gallery_catalog legacy_a={.works=366,.next=11},legacy_b={.works=1765,.next=27},legacy_loaded={0};
    strcpy(legacy_a.revision,rev);strcpy(legacy_b.revision,next);
    assert(catalog_pair_save(root,1,&legacy_a,false)&&catalog_pair_save(root,1,&legacy_b,false));
    legacy_b.next=28;assert(gallery_catalog_save(root,1,&legacy_b));
    legacy_b.next=29;assert(gallery_catalog_save(root,1,&legacy_b));
    assert(gallery_catalog_load(root,1,rev,&legacy_loaded)&&legacy_loaded.next==11);
    assert(gallery_catalog_load(root,1,next,&legacy_loaded)&&legacy_loaded.next==29);
    puts("GALLERY_LEGACY_SCOPE_MIGRATION_OK");
    gallery_catalog plan={.works=366,.next=17},loaded={0};strcpy(plan.revision,rev);
    assert(gallery_catalog_save(root,0,&plan)&&gallery_catalog_load(root,0,rev,&loaded)&&loaded.next==17&&loaded.works==366);
    plan.next=18;full_card=true;assert(!gallery_catalog_save(root,0,&plan));full_card=false;
    assert(gallery_catalog_load(root,0,rev,&loaded)&&loaded.next==17);
    fail_rename=true;assert(!gallery_catalog_save(root,0,&plan));fail_rename=false;
    assert(gallery_catalog_load(root,0,rev,&loaded)&&loaded.next==17);
    assert(gallery_catalog_save(root,0,&plan)&&gallery_catalog_load(root,0,rev,&loaded)&&loaded.next==18);
    assert(!gallery_catalog_load(root,0,next,&loaded));
    assert(catalog_revision_path(file,root,0,rev,1));f=fopen(file,"r+b");assert(f);fputc('X',f);fclose(f);
    assert(gallery_catalog_load(root,0,rev,&loaded)&&loaded.next==17);
    // Switch scopes repeatedly after partial writes; each keeps its own cursor.
    gallery_catalog full={.works=1765,.next=35};strcpy(full.revision,next);
    assert(gallery_catalog_save(root,0,&full));full.next=38;assert(gallery_catalog_save(root,0,&full));
    plan.next=24;assert(gallery_catalog_save(root,0,&plan));plan.next=25;assert(gallery_catalog_save(root,0,&plan));
    assert(gallery_catalog_load(root,0,next,&loaded)&&loaded.next==38&&loaded.works==1765);
    assert(gallery_catalog_load(root,0,"",&loaded)&&loaded.next==38&&!strcmp(loaded.revision,next));
    assert(gallery_catalog_load(root,0,rev,&loaded)&&loaded.next==25&&loaded.works==366);
    assert(gallery_catalog_load(root,0,"",&loaded)&&loaded.next==25&&!strcmp(loaded.revision,rev));
    full.next=39;assert(gallery_catalog_save(root,0,&full));
    assert(gallery_catalog_load(root,0,"",&loaded)&&loaded.next==39&&!strcmp(loaded.revision,next));
    assert(gallery_catalog_load(root,0,rev,&loaded)&&loaded.next==25);
    puts("GALLERY_SCOPE_PROGRESS_OK partial switch, resume, offline last active");
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
