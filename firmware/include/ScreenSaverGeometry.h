#pragma once

// Independently defined seven-segment geometry, matching the legacy visible size.
namespace ScreenSaverGeometry {
constexpr int Width = 204;
constexpr int DigitHeight = 76;
constexpr int CalendarY = 86;
constexpr int GroupHeight = 112;
struct Segment { int x, y, width, height; const char* digits; };
constexpr Segment Segments[] = {
    {9, 0, 24, 9, "02356789"}, {9, 67, 24, 9, "0235689"},
    {9, 34, 24, 9, "2345689"}, {0, 9, 9, 29, "045689"},
    {0, 38, 9, 29, "0268"}, {33, 9, 9, 29, "01234789"},
    {33, 38, 9, 29, "013456789"}
};
inline int bounce(unsigned long tick, int speed, int range) {
    int phase = (tick * speed) % (2 * range);
    return phase > range ? 2 * range - phase : phase;
}
}
