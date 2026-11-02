#pragma once
#include <stddef.h>
#include <stdint.h>
#include "StockCodeGlyphs.h"

struct StockCodeFont {
  static constexpr int width = 56, height = 16, maxChars = 8;
  static constexpr uint16_t color = 0xCE79;
  static uint8_t sourceCoverage(unsigned char c, int x, int y) {
    const int offset = y*8+x;
    const uint8_t packed = STOCK_FONT_READ(&stockCodeGlyphs[c-32][offset/2]);
    return offset % 2 ? packed & 15 : packed >> 4;
  }
  template <typename Pixel>
  static void render(const char* text, Pixel pixel) {
    size_t length = 0; while (text[length]) ++length;
    const bool truncated = length > maxChars;
    const size_t shown = truncated ? maxChars : length;
    // The usual six-digit code gets a full 8px cell; eight-character codes
    // use seven-pixel cells and stay within the existing 56px code column.
    const int advance = shown <= 7 ? 8 : 7;
    for (size_t i = 0; i < shown; ++i) {
      unsigned char c = static_cast<unsigned char>(text[i]);
      if (c < 32 || c > 126) c = '?';
      for (int y = 0; y < height; ++y) for (int x = 0; x < advance; ++x) {
        uint8_t coverage;
        if (truncated && i == maxChars-1) coverage = y == 12 && (x == 1 || x == 3 || x == 5) ? 15 : 0;
        else if (advance == 8) coverage = sourceCoverage(c,x,y);
        else {
          // Integrate both source pixels where the 8-to-7 reduction overlaps.
          const int start=x*8, end=start+8, first=start/7, last=(end-1)/7;
          const int firstWeight=((first+1)*7 < end ? (first+1)*7 : end)-start;
          const int sum=sourceCoverage(c,first,y)*firstWeight +
              (last==first ? 0 : sourceCoverage(c,last,y)*(8-firstWeight));
          coverage=static_cast<uint8_t>((sum+4)/8);
        }
        if (coverage) {
          const uint16_t shade = (((((color>>11)&31)*coverage+7)/15)<<11)
              | (((((color>>5)&63)*coverage+7)/15)<<5) | (((color&31)*coverage+7)/15);
          pixel(static_cast<int>(i)*advance+x,y,shade);
        }
      }
    }
  }
};
