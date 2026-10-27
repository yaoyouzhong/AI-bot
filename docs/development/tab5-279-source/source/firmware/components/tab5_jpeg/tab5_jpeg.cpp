#include "tab5_jpeg.h"
#include "jpge.h"
#include "esp_heap_caps.h"
#include <cstring>
#include <cstdlib>
#include <new>

namespace {
class bounded_stream final : public jpge::output_stream {
    uint8_t *output;
    size_t capacity, used=0;
public:
    bool full=false;
    bounded_stream(uint8_t *p,size_t n):output(p),capacity(n){}
    bool put_buf(const void *p,int n) override {
        if(n<0||size_t(n)>capacity-used){full=true;return false;}
        if(n){if(!p)return false;std::memcpy(output+used,p,size_t(n));used+=size_t(n);}
        return true;
    }
    jpge::uint get_size() const override{return static_cast<jpge::uint>(used);}
};
}

extern "C" tab5_jpeg_result tab5_jpeg_encode(const uint16_t *pixels,size_t input_bytes,
    unsigned width,unsigned height,uint8_t *output,size_t capacity,
    uint32_t *written,void (*yield)(void *),void *context) {
    if(written)*written=0;
    if(!written||!pixels||!output||!width||!height||width>4096||height>4096||
       size_t(width)*height>input_bytes/sizeof(uint16_t)||capacity>UINT32_MAX)
        return TAB5_JPEG_INVALID;
    // The instance and single RGB scanline stay off the worker's small stack.
    void *storage=heap_caps_malloc(sizeof(jpge::jpeg_encoder),MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    auto *row=static_cast<uint8_t *>(heap_caps_malloc(size_t(width)*3,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT));
    if(!storage||!row){std::free(row);std::free(storage);return TAB5_JPEG_MEMORY;}
    auto *codec=new(storage) jpge::jpeg_encoder;
    bounded_stream stream(output,capacity);
    jpge::params options;options.m_quality=85;options.m_subsampling=jpge::H2V2;
    tab5_jpeg_result result=TAB5_JPEG_MEMORY;
    if(codec->init(&stream,int(width),int(height),3,options)) {
        result=TAB5_JPEG_OK;
        for(unsigned y=0;y<height;y++) {
            for(unsigned x=0;x<width;x++) {
                uint16_t v=pixels[size_t(y)*width+x];
                // Expand all bits, keeping red/blue order identical to RGB565.
                unsigned r=v>>11,g=(v>>5)&63,b=v&31;
                row[3*x]=uint8_t((r<<3)|(r>>2));
                row[3*x+1]=uint8_t((g<<2)|(g>>4));
                row[3*x+2]=uint8_t((b<<3)|(b>>2));
            }
            if(!codec->process_scanline(row)){result=TAB5_JPEG_FAILED;break;}
            // Yield once per MCU row, bounding CPU/cache traffic while the
            // ordinary LCD refresh and input tasks continue normally.
            if(yield&&(y&15)==15)yield(context);
        }
        if(result==TAB5_JPEG_OK&&!codec->process_scanline(nullptr))result=TAB5_JPEG_FAILED;
        if(result==TAB5_JPEG_OK)*written=stream.get_size();
    }
    if(stream.full)result=TAB5_JPEG_FULL;
    codec->~jpeg_encoder();std::free(row);std::free(storage);
    return result;
}
