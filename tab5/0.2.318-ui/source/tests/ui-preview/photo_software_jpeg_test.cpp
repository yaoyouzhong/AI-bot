#ifdef NDEBUG
#undef NDEBUG
#endif
#include "tab5_jpeg.h"
#include <cassert>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <vector>
#include <chrono>

static unsigned allocation,fail_at,yields,mcus;
static size_t largest;
extern "C" void *software_test_alloc(size_t n){
    allocation++;if(n>largest)largest=n;
    if(allocation==fail_at)return nullptr;
    void *p=std::malloc(n);if(p&&allocation==3)mcus++;return p;
}
extern "C" void software_test_free(void *p){if(p){assert(mcus);mcus--;}std::free(p);}
extern "C" void *heap_caps_malloc(size_t n,int caps){assert(caps==3);return software_test_alloc(n);}
// Upstream's single MCU-row allocation passes through the heap fallback stub.
// Count its lifetime separately; the wrapper's two malloc/free pairs are plain.
static void give_time(void *){yields++;}
static void reset(){assert(!mcus);allocation=0;largest=0;yields=0;}
static void guards(const std::vector<uint8_t>&out,size_t cap){
    for(size_t i=cap;i<out.size();i++)assert(out[i]==0xa5);
}
int main(){
    for(unsigned pose=0;pose<4;pose++){
        unsigned w=pose%2?720:1280,h=pose%2?1280:720;
        std::vector<uint16_t>pixels(size_t(w)*h);
        const uint16_t colors[]={0xf800,0x07e0,0x001f,0xffff};
        for(unsigned y=0;y<h;y++)for(unsigned x=0;x<w;x++)
            pixels[size_t(y)*w+x]=colors[(x/(w/2)+2*(y/(h/2))+pose)%4];
        auto original=pixels;
        size_t cap=2*1024*1024;
        std::vector<uint8_t>out(cap+64,0xa5);uint32_t written=0;
        reset();auto start=std::chrono::steady_clock::now();
        auto result=tab5_jpeg_encode(pixels.data(),pixels.size()*2,w,h,out.data(),cap,&written,give_time,nullptr);
        auto elapsed=std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::steady_clock::now()-start).count();
        assert(result==TAB5_JPEG_OK&&written>600&&written<cap&&pixels==original);
        assert(yields==h/16&&allocation==3&&largest<=61440);
        assert(out[0]==0xff&&out[1]==0xd8&&out[written-2]==0xff&&out[written-1]==0xd9);
        guards(out,cap);char file[80];std::snprintf(file,sizeof file,"software-photo-pose-%u.jpg",pose);
        FILE *f=std::fopen(file,"wb");assert(f);assert(std::fwrite(out.data(),1,written,f)==written);std::fclose(f);
        std::printf("pose=%u %ux%u bytes=%u extraLargest=%zu nativeMs=%lld\n",pose,w,h,written,largest,(long long)elapsed);
        const size_t limits[]={0,1,64,512,1024,size_t(written-1)};
        for(size_t limit:limits){
            reset();std::vector<uint8_t>short_out(limit+64,0xa5);uint32_t n=9;
            assert(tab5_jpeg_encode(pixels.data(),pixels.size()*2,w,h,short_out.data(),limit,&n,give_time,nullptr)==TAB5_JPEG_FULL);
            assert(!n&&pixels==original);guards(short_out,limit);
        }
        for(fail_at=1;fail_at<=3;fail_at++){
            reset();written=9;
            assert(tab5_jpeg_encode(pixels.data(),pixels.size()*2,w,h,out.data(),cap,&written,give_time,nullptr)==TAB5_JPEG_MEMORY);
            assert(!written&&pixels==original);guards(out,cap);
        }
        fail_at=0;reset();
        assert(tab5_jpeg_encode(pixels.data(),pixels.size()*2,w,h,out.data(),cap,&written,give_time,nullptr)==TAB5_JPEG_OK);
        assert(tab5_jpeg_encode(pixels.data(),pixels.size()*2-1,w,h,out.data(),cap,&written,give_time,nullptr)==TAB5_JPEG_INVALID&&!written);
    }
    // Detailed synthetic content, not only a compressible color chart.
    std::vector<uint16_t>noise(1280*720);uint32_t random=0x12345678;
    for(auto &v:noise){random^=random<<13;random^=random>>17;random^=random<<5;v=uint16_t(random);}
    auto original=noise;std::vector<uint8_t>out(2*1024*1024+64,0xa5);uint32_t written=0;
    reset();assert(tab5_jpeg_encode(noise.data(),noise.size()*2,1280,720,out.data(),out.size()-64,&written,give_time,nullptr)==TAB5_JPEG_OK);
    assert(noise==original&&written>100000&&written<out.size()-64&&largest<=61440);guards(out,out.size()-64);
    FILE *f=std::fopen("software-photo-noise.jpg","wb");assert(f);assert(std::fwrite(out.data(),1,written,f)==written);std::fclose(f);
    std::puts("PHOTO_SOFTWARE_JPEG_OK real codec; four poses, immutable pixels, bounded writes, all allocation failures and retry, row yields, detailed content");
}
