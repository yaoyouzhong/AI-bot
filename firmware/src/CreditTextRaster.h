#pragma once
#include <stdint.h>

// Integrate the whole source footprint instead of skipping thin digit strokes
// when reducing Font2 from 16 to 14 pixels. Coordinates use integer subpixels.
template <typename ReadPixel>
uint16_t creditTextPixel(int x, int y, int sourceWidth, int sourceHeight,
                         int width, int height, uint16_t color, ReadPixel read) {
  const int left = x * sourceWidth, right = left + sourceWidth;
  const int top = y * sourceHeight, bottom = top + sourceHeight;
  int ink = 0;
  for (int sy = top / height; sy < (bottom + height - 1) / height; ++sy) {
    const int dy = (bottom < (sy + 1) * height ? bottom : (sy + 1) * height)
                 - (top > sy * height ? top : sy * height);
    for (int sx = left / width; sx < (right + width - 1) / width; ++sx) {
      const int dx = (right < (sx + 1) * width ? right : (sx + 1) * width)
                   - (left > sx * width ? left : sx * width);
      if (read(sx, sy)) ink += dx * dy;
    }
  }
  const int area = sourceWidth * sourceHeight;
  return (((((color >> 11) & 31) * ink + area / 2) / area) << 11)
       | (((((color >> 5) & 63) * ink + area / 2) / area) << 5)
       | (((color & 31) * ink + area / 2) / area);
}
