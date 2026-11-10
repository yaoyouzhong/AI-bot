#pragma once
#include <stdint.h>
// Original compact stroke glyphs for the lunar date; no bundled font.
namespace LunarText {
template<class Line> void glyph(uint32_t code, int x, int y, Line line) {
    auto stroke = [&](int a,int b,int c,int d) { line(x+a,y+b,x+c,y+d); };
    switch(code) {
    case 0x519C: // 农
        stroke(2,5,14,5);
        stroke(3,2,3,7);
        stroke(8,1,6,9);
        stroke(6,9,2,13);
        stroke(6,9,6,17);
        stroke(6,17,10,14);
        stroke(8,8,14,16);
        stroke(10,12,14,8);
        break;
    case 0x5386: // 历
        stroke(2,2,14,2);
        stroke(2,2,2,12);
        stroke(2,12,0,17);
        stroke(4,7,14,7);
        stroke(9,3,8,12);
        stroke(8,12,4,17);
        stroke(14,7,13,17);
        stroke(13,17,10,16);
        break;
    case 0x95F0: // 闰
        stroke(1,4,1,17);
        stroke(3,1,4,3);
        stroke(6,2,15,2);
        stroke(15,2,15,17);
        stroke(15,17,12,17);
        stroke(4,6,12,6);
        stroke(8,6,8,14);
        stroke(4,10,12,10);
        stroke(3,14,13,14);
        break;
    case 0x6B63: // 正
        stroke(2,2,14,2);
        stroke(8,2,8,16);
        stroke(8,8,13,8);
        stroke(3,7,3,16);
        stroke(1,16,15,16);
        break;
    case 0x6708: // 月
        stroke(4,1,14,1);
        stroke(4,1,4,12);
        stroke(4,12,1,17);
        stroke(14,1,14,17);
        stroke(14,17,11,16);
        stroke(4,6,14,6);
        stroke(4,11,14,11);
        break;
    case 0x521D: // 初
        stroke(3,1,4,3);
        stroke(1,5,7,5);
        stroke(7,5,2,11);
        stroke(4,9,4,17);
        stroke(5,10,8,13);
        stroke(7,8,6,10);
        stroke(9,3,15,3);
        stroke(15,3,14,17);
        stroke(14,17,11,16);
        stroke(12,3,11,11);
        stroke(11,11,8,17);
        break;
    case 0x5EFF: // 廿
        stroke(1,6,15,6);
        stroke(5,1,5,15);
        stroke(11,1,11,15);
        stroke(5,15,11,15);
        break;
    case 0x4E00: // 一
        stroke(1,9,15,9);
        break;
    case 0x4E8C: // 二
        stroke(3,4,13,4);
        stroke(1,14,15,14);
        break;
    case 0x4E09: // 三
        stroke(2,2,14,2);
        stroke(4,9,12,9);
        stroke(1,16,15,16);
        break;
    case 0x56DB: // 四
        stroke(1,3,15,3);
        stroke(1,3,1,16);
        stroke(15,3,15,16);
        stroke(1,16,15,16);
        stroke(6,3,6,9);
        stroke(6,9,3,12);
        stroke(10,3,10,12);
        stroke(10,12,13,12);
        break;
    case 0x4E94: // 五
        stroke(2,2,14,2);
        stroke(7,2,5,16);
        stroke(2,8,12,8);
        stroke(12,8,12,16);
        stroke(1,16,15,16);
        break;
    case 0x516D: // 六
        stroke(7,1,9,3);
        stroke(1,6,15,6);
        stroke(6,10,2,17);
        stroke(10,10,15,17);
        break;
    case 0x4E03: // 七
        stroke(1,7,15,5);
        stroke(7,1,7,16);
        stroke(7,16,14,16);
        stroke(14,16,15,13);
        break;
    case 0x516B: // 八
        stroke(5,3,4,11);
        stroke(4,11,1,17);
        stroke(10,2,11,10);
        stroke(11,10,15,17);
        break;
    case 0x4E5D: // 九
        stroke(2,6,12,6);
        stroke(6,1,6,10);
        stroke(6,10,2,17);
        stroke(12,6,11,16);
        stroke(11,16,15,16);
        stroke(15,16,15,13);
        break;
    case 0x5341: // 十
        stroke(1,7,15,7);
        stroke(8,1,8,17);
        break;
    case 0x51AC: // 冬
        stroke(6,1,2,6);
        stroke(4,4,12,4);
        stroke(12,4,7,9);
        stroke(4,5,14,10);
        stroke(7,9,1,11);
        stroke(6,11,10,13);
        stroke(5,15,10,17);
        break;
    case 0x814A: // 腊
        stroke(1,2,5,2);
        stroke(1,2,1,14);
        stroke(1,14,0,17);
        stroke(5,2,5,17);
        stroke(5,17,3,16);
        stroke(1,7,5,7);
        stroke(1,12,5,12);
        stroke(8,1,8,7);
        stroke(12,1,12,7);
        stroke(6,4,15,4);
        stroke(6,8,15,8);
        stroke(7,11,14,11);
        stroke(7,11,7,17);
        stroke(14,11,14,17);
        stroke(7,14,14,14);
        stroke(7,17,14,17);
        break;
    }
}
template<class Line> void draw(const char* text, int center, int y, Line line) {
    int count = 0;
    for (const unsigned char* p = (const unsigned char*)text; *p; p += 3) count++;
    int x = center - (count * 18 - 2) / 2;
    for (const unsigned char* p = (const unsigned char*)text; *p; p += 3, x += 18) {
        const uint32_t code = ((p[0] & 15) << 12) | ((p[1] & 63) << 6) | (p[2] & 63);
        glyph(code,x,y,line);
    }
}
}
