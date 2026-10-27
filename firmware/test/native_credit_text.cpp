// Compile after PlatformIO resolves the existing TFT_eSPI dependency:
// clang++ -std=c++17 -Ifirmware/src -Ifirmware/.pio/libdeps/nodemcuv2/TFT_eSPI firmware/test/native_credit_text.cpp -o /tmp/credit-text-test
#include <cassert>
#include <cstdint>
#include <fstream>
#include <iostream>
#include <string>
#include <vector>
#include "CreditTextRaster.h"
#define PROGMEM
#include "Fonts/Font16.c"

struct Bitmap { int width, height; std::vector<uint8_t> pixels; };
Bitmap font(const std::string& text) {
  int width = 0; for (unsigned char c : text) width += widtbl_f16[c-32];
  Bitmap image{width,16,std::vector<uint8_t>(width*16)};
  int left = 0;
  for (unsigned char c : text) {
    int advance = widtbl_f16[c-32], stride = (advance+6)/8;
    for (int y = 0; y < 16; ++y) for (int x = 0; x < stride*8 && left+x < width; ++x)
      if (chrtbl_f16[c-32][y*stride+x/8] & (0x80>>(x%8))) image.pixels[y*width+left+x]=255;
    left += advance;
  }
  return image;
}
Bitmap shrink(const Bitmap& source, bool coverage) {
  int width=(source.width*7+4)/8;
  Bitmap result{width,14,std::vector<uint8_t>(width*14)};
  for(int y=0;y<14;++y) for(int x=0;x<width;++x) {
    if (!coverage) result.pixels[y*width+x]=source.pixels[(y*16/14)*source.width+x*source.width/width];
    else {
      auto pixel=creditTextPixel(x,y,source.width,16,width,14,0x07e0,[&](int sx,int sy){
        assert(sx>=0 && sx<source.width && sy>=0 && sy<16);
        return source.pixels[sy*source.width+sx]!=0;
      });
      result.pixels[y*width+x]=((pixel>>5)&63)*255/63;
    }
  }
  return result;
}
void save(const Bitmap& image,const std::string& path) {
  std::ofstream out(path,std::ios::binary); out<<"P5\n"<<image.width<<" "<<image.height<<"\n255\n";
  out.write(reinterpret_cast<const char*>(image.pixels.data()),image.pixels.size());
}
int main(int argc,char**argv) {
  int checks=0, oldDropped=0;
  // Actual LCD color contract: no background bleed, no overbright channels,
  // and a half-covered thin stroke reaches the intended foreground color.
  for(uint16_t color : {uint16_t(0x07e0),uint16_t(0xffff),uint16_t(0xfd20)}) {
    assert(creditTextPixel(0,0,2,2,1,1,color,[](int,int){return false;})==0);
    assert(creditTextPixel(0,0,2,2,1,1,color,[](int,int){return true;})==color);
    assert(creditTextPixel(0,0,2,2,1,1,color,[](int x,int){return x==0;})==color);
    checks+=3;
  }
  // Single-pixel strokes at every source position: the former nearest sample
  // can lose them entirely; coverage must retain every one after reduction.
  for(int width : {8,24,39,64,80}) for(int y=0;y<16;++y) for(int x=0;x<width;++x) {
    Bitmap source{width,16,std::vector<uint8_t>(width*16)};source.pixels[y*width+x]=255;
    auto next=shrink(source,true),old=shrink(source,false);
    bool seen=false,before=false;for(auto value:next.pixels)seen|=value!=0;for(auto value:old.pixels)before|=value!=0;
    assert(seen);oldDropped+=!before;++checks;
  }
  assert(oldDropped>0);
  // Every numeral/date remains distinct at 14px, including the first and last digit.
  std::vector<std::vector<uint8_t>> digits;
  for(char c='0';c<='9';++c) {
    auto image=shrink(font(std::string(1,c)),true);
    for(const auto& previous:digits)assert(previous!=image.pixels);
    digits.push_back(image.pixels);++checks;
  }
  if(argc==2) {
    auto source=font("0123456789 R*1 09/30 10/31");
    save(source,std::string(argv[1])+"/native.pgm");
    save(shrink(source,false),std::string(argv[1])+"/nearest.pgm");
    save(shrink(source,true),std::string(argv[1])+"/coverage.pgm");
  }
  std::cout<<"CREDIT_TEXT_RASTER_OK "<<checks<<" checks; previous sampler erased "<<oldDropped<<" isolated strokes; coverage preserves all\n";
}
