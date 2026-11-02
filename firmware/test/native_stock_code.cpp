#include "../src/StockCodeFont.h"
#include <cassert>
#include <fstream>
#include <string>
#include <vector>

using Bitmap = std::vector<unsigned char>;
Bitmap raster(const std::string& text) {
  Bitmap result(StockCodeFont::width * StockCodeFont::height);
  StockCodeFont::render(text.c_str(),[&](int x,int y,uint16_t color) {
    assert(x >= 0 && x < StockCodeFont::width && y >= 0 && y < StockCodeFont::height);
    result[y*StockCodeFont::width+x] = static_cast<unsigned char>(((color>>11)&31)*255/31);
  });
  return result;
}
int main(int argc,char** argv) {
  for (char c='0';c<='9';++c) {
    auto one=raster(std::string(1,c));
    int top=StockCodeFont::height, bottom=-1;
    for (int y=0;y<StockCodeFont::height;++y) for (int x=0;x<8;++x)
      if (one[y*StockCodeFont::width+x] >= 70) { if (y<top) top=y; if (y>bottom) bottom=y; }
    assert(top >= 2 && top <= 3 && bottom == 12);
    for(char prior='0';prior<c;++prior) assert(one != raster(std::string(1,prior)));
  }
  assert(raster("AAPL") != raster("aapl"));
  assert(raster("600519") != raster("600518"));
  assert(raster("123456789LONG") == raster("1234567ZZLONG"));
  assert(raster("123456789LONG") != raster("12345678"));
  assert(raster(std::string(1, char(1))) == raster("?"));
  if (argc==2) {
    const std::string cases[] = {"600519","000001","AAPL","TSLA","ABCDEFGHJKLM"};
    for(size_t i=0;i<5;++i) {
      auto bytes=raster(cases[i]); std::ofstream file(std::string(argv[1])+"/stock-code-"+std::to_string(i)+".pgm",std::ios::binary);
      file<<"P5\n"<<StockCodeFont::width<<" "<<StockCodeFont::height<<"\n255\n";
      file.write(reinterpret_cast<const char*>(bytes.data()),bytes.size());
    }
  }
}
