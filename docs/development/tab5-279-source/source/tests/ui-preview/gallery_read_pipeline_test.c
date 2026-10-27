#undef NDEBUG
#include <assert.h>
#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
#include "gallery_read_pipeline.h"

typedef struct {gallery_read_pipeline pipe;size_t bytes,fragment;atomic_size_t peak;} fixture;
static uint8_t pattern(size_t offset){return (uint8_t)(offset*31+17);}
static DWORD WINAPI produce(void *arg) {
    fixture *f=arg;size_t at=0;
    while(at<f->bytes&&!atomic_load(&f->pipe.stop)) {
        uint8_t *out;size_t n=gallery_read_write_span(&f->pipe,&out);
        if(!n){SwitchToThread();continue;}
        if(n>f->fragment)n=f->fragment;
        if(n>f->bytes-at)n=f->bytes-at;
        for(size_t i=0;i<n;i++)out[i]=pattern(at+i);
        gallery_read_publish(&f->pipe,n);at+=n;
        size_t lead=at-atomic_load(&f->pipe.consumed);
        assert(lead<=f->pipe.capacity);
        if(lead>atomic_load(&f->peak))atomic_store(&f->peak,lead);
    }
    atomic_store_explicit(&f->pipe.finished,true,memory_order_release);return 0;
}
int main(void) {
    uint8_t *ring=malloc(GALLERY_HTTP_AHEAD_BYTES),*out=malloc(32768);assert(ring&&out);
    size_t fragments[]={1,64,511,1440,16384},read_sizes[]={1,97,4096,32768};unsigned cases=0;
    for(unsigned i=0;i<5;i++)for(unsigned j=0;j<4;j++) {
        fixture f={.bytes=2*1048576+123,.fragment=fragments[i]};
        gallery_read_init(&f.pipe,ring,GALLERY_HTTP_AHEAD_BYTES);atomic_init(&f.peak,0);
        HANDLE task=CreateThread(NULL,0,produce,&f,0,NULL);assert(task);
        Sleep(2); // Consumer processing must not stop network receive-ahead.
        size_t at=0;for(;;) {
            int n=gallery_read_poll(&f.pipe,out,read_sizes[j]);
            if(n==-2){SwitchToThread();continue;}
            assert(n>=0);if(!n)break;
            for(int k=0;k<n;k++)assert(out[k]==pattern(at+(size_t)k));at+=n;
        }
        assert(at==f.bytes&&WaitForSingleObject(task,5000)==WAIT_OBJECT_0);CloseHandle(task);
        assert(atomic_load(&f.peak)>0&&atomic_load(&f.peak)<=GALLERY_HTTP_AHEAD_BYTES);cases++;
    }
    fixture blocked={.bytes=1048576,.fragment=16384};
    gallery_read_init(&blocked.pipe,ring,GALLERY_HTTP_AHEAD_BYTES);atomic_init(&blocked.peak,0);
    HANDLE task=CreateThread(NULL,0,produce,&blocked,0,NULL);assert(task);
    DWORD until=GetTickCount()+5000;
    while(atomic_load(&blocked.pipe.produced)<GALLERY_HTTP_AHEAD_BYTES&&GetTickCount()<until)Sleep(1);
    assert(atomic_load(&blocked.pipe.produced)==GALLERY_HTTP_AHEAD_BYTES);
    atomic_store(&blocked.pipe.stop,true);assert(WaitForSingleObject(task,5000)==WAIT_OBJECT_0);CloseHandle(task);
    // A failed transport still exposes only its already-received prefix; the
    // authenticated consumer decides which blocks may become checkpoints.
    gallery_read_init(&blocked.pipe,ring,GALLERY_HTTP_AHEAD_BYTES);
    ring[0]=42;gallery_read_publish(&blocked.pipe,1);atomic_store(&blocked.pipe.failed,true);
    atomic_store_explicit(&blocked.pipe.finished,true,memory_order_release);
    assert(gallery_read_poll(&blocked.pipe,out,32)==1&&out[0]==42);
    assert(gallery_read_poll(&blocked.pipe,out,32)==-1);
    free(out);free(ring);
    printf("GALLERY_READ_PIPELINE_OK %u concurrent fragmentation/wrap/ordering cases; bounded full ring, cancellation and failed prefix\n",cases);
    return 0;
}
