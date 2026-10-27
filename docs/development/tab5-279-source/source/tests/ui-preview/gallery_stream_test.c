#undef NDEBUG
#include <assert.h>
#include <windows.h>
#include <bcrypt.h>
#include <stdio.h>
#include "gallery_stream.h"
#include "gallery_write_pipeline.h"
typedef struct {uint8_t *wire;size_t size,at,fragment,progress;bool cancel; } reader;
static int read_wire(void *context,uint8_t *out,size_t count) {
    reader *r=context;if(count>r->fragment)count=r->fragment;if(count>r->size-r->at)count=r->size-r->at;
    memcpy(out,r->wire+r->at,count);r->at+=count;return (int)count;
}
static char *decrypt_wire(void *context,const uint8_t *packet,size_t size,const char *nonce,int *error) {
    (void)context;*error=-1003;if(size<28||size>65535)return NULL;
    BCRYPT_ALG_HANDLE alg=NULL;BCRYPT_KEY_HANDLE key=NULL;uint8_t bytes[32];for(int i=0;i<32;i++)bytes[i]=i;
    assert(!BCryptOpenAlgorithmProvider(&alg,BCRYPT_AES_ALGORITHM,NULL,0));
    assert(!BCryptSetProperty(alg,BCRYPT_CHAINING_MODE,(PUCHAR)BCRYPT_CHAIN_MODE_GCM,sizeof(BCRYPT_CHAIN_MODE_GCM),0));
    assert(!BCryptGenerateSymmetricKey(alg,&key,NULL,0,bytes,32,0));
    BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO auth;BCRYPT_INIT_AUTH_MODE_INFO(auth);
    auth.pbNonce=(PUCHAR)packet;auth.cbNonce=12;auth.pbTag=(PUCHAR)packet+12;auth.cbTag=16;auth.pbAuthData=(PUCHAR)nonce;auth.cbAuthData=32;
    char *clear=malloc(size-28+1);assert(clear);ULONG n=0;
    NTSTATUS code=BCryptDecrypt(key,(PUCHAR)packet+28,(ULONG)size-28,&auth,NULL,0,(PUCHAR)clear,(ULONG)size-28,&n,0);
    BCryptDestroyKey(key);BCryptCloseAlgorithmProvider(alg,0);
    if(code||n!=size-28){free(clear);return NULL;}clear[n]=0;*error=0;return clear;
}
int mbedtls_base64_encode(unsigned char *o,size_t c,size_t *n,const unsigned char *i,size_t s){assert(false);return -1;}
int mbedtls_base64_decode(unsigned char *o,size_t c,size_t *n,const unsigned char *i,size_t s){assert(false);return -1;}
static bool progress_wire(void *context,size_t verified){reader *r=context;assert(verified>r->progress&&verified-r->progress<=49152);r->progress=verified;return !r->cancel;}
static cJSON *receive(reader *r,size_t declared,size_t offset,size_t size,int *status,size_t *got,uint8_t *out,const char *nonce) {
    r->at=r->progress=0;return gallery_stream_receive(read_wire,decrypt_wire,progress_wire,r,declared,nonce,out,size,offset,size,status,got);
}
int main(int argc,char **argv) {
    assert(argc==2);char nonce[33];memset(nonce,'a',32);nonce[32]=0;
    int ranges[][2]={{0,1},{0,49152},{0,1048576},{196608,700000}};unsigned cases=0;
    for(unsigned i=0;i<4;i++) {
        char path[1024];snprintf(path,sizeof(path),"%s/gallery-stream-%d-%d.bin",argv[1],ranges[i][0],ranges[i][1]);
        FILE *f=fopen(path,"rb");assert(f&&!fseek(f,0,SEEK_END));size_t bytes=ftell(f);rewind(f);
        uint8_t *wire=malloc(bytes),*out=malloc(ranges[i][1]);assert(wire&&out&&fread(wire,1,bytes,f)==bytes);fclose(f);
        for(size_t fragment=1;fragment<=4096;fragment*=16) {
            reader r={wire,bytes,0,fragment};int status;size_t got;cJSON *body=receive(&r,bytes,ranges[i][0],ranges[i][1],&status,&got,out,nonce);
            assert(body&&status==200&&got==ranges[i][1]&&r.progress==got&&r.at==bytes);
            for(size_t j=0;j<got;j++)assert(out[j]==(uint8_t)((j+ranges[i][0])*31+17));cJSON_Delete(body);cases++;
        }
        // Every cut retains only fully authenticated preceding blocks.
        for(size_t cut=0;cut<bytes;cut+=8191) {
            reader r={wire,cut,0,4096};int status;size_t got;
            cJSON *body=receive(&r,bytes,ranges[i][0],ranges[i][1],&status,&got,out,nonce);
            assert(!body&&got<(size_t)ranges[i][1]&&got%49152==0&&r.progress==got);cases++;
        }
        for(size_t flip=16;flip<bytes;flip+=17003) {
            wire[flip]^=1;reader r={wire,bytes,0,4096};int status;size_t got;
            cJSON *body=receive(&r,bytes,ranges[i][0],ranges[i][1],&status,&got,out,nonce);assert(!body&&got<(size_t)ranges[i][1]);wire[flip]^=1;cases++;
        }
        if(ranges[i][1]>49152) {
            size_t first=4+gallery_stream_u32(wire+16),second=4+gallery_stream_u32(wire+16+first);
            uint8_t *reordered=malloc(bytes);assert(reordered);memcpy(reordered,wire,16);
            memcpy(reordered+16,wire+16+first,second);memcpy(reordered+16+second,wire+16,first);memcpy(reordered+16+second+first,wire+16+first+second,bytes-16-first-second);
            reader shuffled={reordered,bytes,0,4096};int status;size_t got;
            assert(!receive(&shuffled,bytes,ranges[i][0],ranges[i][1],&status,&got,out,nonce)&&!got);free(reordered);
            size_t duplicated_size=bytes-second+first;uint8_t *duplicate=malloc(duplicated_size);assert(duplicate);
            memcpy(duplicate,wire,16+first);memcpy(duplicate+16+first,wire+16,first);memcpy(duplicate+16+first*2,wire+16+first+second,bytes-16-first-second);
            reader repeated={duplicate,duplicated_size,0,4096};
            assert(!receive(&repeated,duplicated_size,ranges[i][0],ranges[i][1],&status,&got,out,nonce)&&got==49152);free(duplicate);cases+=2;
        }
        reader r={wire,bytes,0,4096};int status;size_t got;nonce[0]='b';assert(!receive(&r,bytes,ranges[i][0],ranges[i][1],&status,&got,out,nonce)&&!got);nonce[0]='a';
        r.cancel=true;assert(!receive(&r,bytes,ranges[i][0],ranges[i][1],&status,&got,out,nonce)&&got<=49152);
        assert(!receive(&r,bytes,ranges[i][0]+1,ranges[i][1],&status,&got,out,nonce)&&!got);
        free(wire);free(out);
    }
    printf("GALLERY_STREAM_RECEIVE_OK %u cross-language cases; exact 1MiB/partial ranges; fragmentation; truncation durable prefix; corrupt length/AEAD; nonce/order/bounds/cancellation\n",cases);
    return 0;
}
