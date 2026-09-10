#pragma once
#include <TFT_eSPI.h>
// Maintainer-authored legacy weather animations; see PROVENANCE.md.
class WeatherAnimations {
  TFT_eSPI& tft;
  struct { int icon = 0; int animation = 0; } weatherStatus;
  int weatherAnimFrame = 0;
  int weatherLastAnimation = -1;
  static constexpr int WEATHER_ANIM_BOTTOM = 224;
public:
  explicit WeatherAnimations(TFT_eSPI& canvas) : tft(canvas) {}
  void draw(int icon, int animation, int frame) {
    weatherStatus.icon = icon; weatherStatus.animation = animation;
    weatherAnimFrame = frame; drawWeatherAnimation();
  }
private:
void drawWeatherRobotAnimation() {
  // A tiny weather buddy: its colour follows the condition, it floats on a
  // cloud, blinks, and sends a moving antenna pulse. It is deliberately more
  // characterful than a second copy of the weather icon in the upper corner.
  const int x = 184, y = 188 + ((weatherAnimFrame % 6) == 0 ? -2 : (weatherAnimFrame % 6) == 3 ? 2 : 0);
  const int phase = weatherAnimFrame % 12;
  const uint16_t body = weatherStatus.icon == 4 ? TFT_CYAN
                      : weatherStatus.icon == 5 ? TFT_WHITE
                      : weatherStatus.icon == 6 ? TFT_ORANGE
                      : weatherStatus.icon == 3 ? TFT_LIGHTGREY : TFT_YELLOW;
  // drifting cloud
  int cloud = (phase % 4) * 2;
  tft.fillCircle(156 + cloud, WEATHER_ANIM_BOTTOM - 8, 8, TFT_DARKGREY);
  tft.fillCircle(168 + cloud, WEATHER_ANIM_BOTTOM - 13, 11, TFT_DARKGREY);
  tft.fillCircle(181 + cloud, WEATHER_ANIM_BOTTOM - 8, 8, TFT_DARKGREY);
  tft.fillRoundRect(149 + cloud, WEATHER_ANIM_BOTTOM - 8, 40, 8, 4, TFT_DARKGREY);
  // rounded robot head and a live antenna
  tft.fillRoundRect(x - 22, y - 22, 44, 36, 10, body);
  tft.drawLine(x, y - 22, x + (phase < 6 ? 5 : -5), y - 31, body);
  tft.fillCircle(x + (phase < 6 ? 5 : -5), y - 33, 3, phase % 3 == 0 ? TFT_MAGENTA : body);
  bool blink = phase == 0 || phase == 1;
  tft.setTextColor(TFT_BLACK, body);
  if (blink) {
    tft.drawFastHLine(x - 13, y - 6, 8, TFT_BLACK);
    tft.drawFastHLine(x + 5, y - 6, 8, TFT_BLACK);
  } else {
    tft.fillCircle(x - 9, y - 6, 4, TFT_BLACK);
    tft.fillCircle(x + 9, y - 6, 4, TFT_BLACK);
    tft.fillCircle(x - 8, y - 7, 1, TFT_WHITE);
    tft.fillCircle(x + 10, y - 7, 1, TFT_WHITE);
  }
  tft.drawArc(x, y + 3, 9, 6, 25, 155, TFT_BLACK, body);
  if (weatherStatus.icon == 4 || weatherStatus.icon == 6) {
    for (int i = 0; i < 3; i++) {
      int dropY = 158 + ((phase * 5 + i * 17) % 28);
      tft.drawLine(151 + i * 13, dropY, 148 + i * 13, dropY + 5, TFT_CYAN);
    }
  } else if (weatherStatus.icon == 5) {
    for (int i = 0; i < 4; i++) {
      int sx = 150 + ((i * 19 + phase * 3) % 78);
      tft.drawFastHLine(sx - 2, 159 + i * 9, 5, TFT_WHITE);
      tft.drawFastVLine(sx, 156 + i * 9, 7, TFT_WHITE);
    }
  } else {
    tft.fillCircle(218, 162, 5 + (phase % 3), body);
    for (int i = 0; i < 4; i++) {
      float a = i * 1.57f + phase * 0.12f;
      tft.drawLine(218 + (int)(cos(a) * 9), 162 + (int)(sin(a) * 9),
                   218 + (int)(cos(a) * 13), 162 + (int)(sin(a) * 13), body);
    }
  }
}

void drawWeatherHouseAnimation() {
  const int phase = weatherAnimFrame % 12;
  // ground and a compact house
  tft.drawFastHLine(153, WEATHER_ANIM_BOTTOM, 78, TFT_DARKGREY);
  tft.fillRect(166, 190, 48, 33, TFT_ORANGE);
  tft.fillTriangle(158, 192, 190, 167, 222, 192, TFT_RED);
  tft.fillRect(173, 199, 12, 24, TFT_BROWN);
  tft.fillRect(194, 198, 13, 12, (phase < 6) ? TFT_YELLOW : TFT_ORANGE);
  tft.drawRect(194, 198, 13, 12, TFT_WHITE);
  tft.drawFastVLine(200, 198, 12, TFT_WHITE);
  tft.drawFastHLine(194, 204, 13, TFT_WHITE);
  // chimney smoke drifts instead of leaving static pixels behind.
  tft.fillRect(205, 170, 7, 15, TFT_DARKGREY);
  int drift = phase / 3;
  tft.fillCircle(211 + drift, 164, 3, TFT_LIGHTGREY);
  tft.fillCircle(215 + drift, 158, 2, TFT_DARKGREY);
  if (weatherStatus.icon == 4 || weatherStatus.icon == 6) {
    for (int i = 0; i < 5; i++) {
      int dropY = 158 + ((phase * 4 + i * 13) % 30);
      tft.drawLine(153 + i * 16, dropY, 150 + i * 16, dropY + 5, TFT_CYAN);
    }
  } else if (weatherStatus.icon == 5) {
    for (int i = 0; i < 5; i++) {
      int sx = 153 + ((i * 17 + phase * 3) % 74);
      int sy = 158 + ((i * 11 + phase * 2) % 29);
      tft.drawPixel(sx, sy, TFT_WHITE); tft.drawPixel(sx + 1, sy, TFT_WHITE);
    }
  } else {
    tft.fillCircle(224, 163, 5 + (phase % 2), TFT_YELLOW);
  }
}

void drawWeatherPlantAnimation() {
  const int phase = weatherAnimFrame % 12;
  const int sway = phase < 6 ? phase / 2 : (11 - phase) / 2;
  const int stemX = 188 + sway - 1;
  // pot
  tft.fillRoundRect(171, 204, 36, 8, 3, TFT_ORANGE);
  tft.fillTriangle(175, 211, 203, 211, 199, WEATHER_ANIM_BOTTOM, TFT_BROWN);
  tft.fillTriangle(175, 211, 199, WEATHER_ANIM_BOTTOM, 179, WEATHER_ANIM_BOTTOM, TFT_BROWN);
  // swaying stem and leaves
  tft.drawLine(189, 204, stemX, 171, TFT_GREEN);
  tft.fillEllipse(stemX - 10, 177, 11, 6, TFT_GREEN);
  tft.fillEllipse(stemX + 1, 185, 12, 6, TFT_GREEN);
  tft.fillEllipse(stemX - 9, 193, 10, 5, TFT_DARKGREEN);
  if (weatherStatus.icon == 4 || weatherStatus.icon == 6) {
    for (int i = 0; i < 4; i++) {
      int dropY = 154 + ((phase * 5 + i * 15) % 38);
      tft.drawLine(154 + i * 21, dropY, 152 + i * 21, dropY + 5, TFT_CYAN);
    }
  } else if (weatherStatus.icon == 5) {
    for (int i = 0; i < 5; i++) {
      int sx = 153 + ((i * 18 + phase * 2) % 75);
      tft.fillCircle(sx, 157 + i * 7, 1, TFT_WHITE);
    }
  } else {
    tft.fillCircle(219, 162, 6 + (phase % 2), TFT_YELLOW);
    for (int i = 0; i < 4; i++) {
      float a = i * 1.57f + phase * 0.1f;
      tft.drawLine(219 + (int)(cos(a) * 9), 162 + (int)(sin(a) * 9),
                   219 + (int)(cos(a) * 13), 162 + (int)(sin(a) * 13), TFT_YELLOW);
    }
  }
}

void drawWeatherPetAnimation() {
  const int phase = weatherAnimFrame % 12;
  const int petY = -6; // align the paws with the humidity row's lower edge
  const bool blink = phase == 0 || phase == 1;
  const uint16_t fur = weatherStatus.icon == 5 ? TFT_LIGHTGREY : TFT_ORANGE;
  // Only erase pixels that can move. Clearing the full 88x76 area before
  // repainting the large pet made the black intermediate frame visible.
  tft.fillRect(148, 152, 88, 28, TFT_BLACK);
  tft.fillRect(207, 193 + petY, 22, 24, TFT_BLACK);
  // Curled tail swishes behind the body.
  int tailLift = phase < 6 ? phase / 2 : (11 - phase) / 2;
  tft.drawLine(207, 210 + petY, 219, 207 + petY - tailLift, fur);
  tft.drawLine(219, 207 + petY - tailLift, 224, 198 + petY + tailLift, fur);
  tft.fillEllipse(190, 208 + petY, 20, 16, fur);
  // Head, ears and paws.
  tft.fillTriangle(173, 181 + petY, 178, 166 + petY, 184, 181 + petY, fur);
  tft.fillTriangle(196, 181 + petY, 203, 166 + petY, 207, 183 + petY, fur);
  tft.fillRoundRect(174, 176 + petY, 34, 29, 10, fur);
  tft.fillEllipse(180, WEATHER_ANIM_BOTTOM - 4 + petY, 9, 4, TFT_LIGHTGREY);
  tft.fillEllipse(201, WEATHER_ANIM_BOTTOM - 4 + petY, 9, 4, TFT_LIGHTGREY);
  // Face alternates between open eyes and a blink.
  if (blink) {
    tft.drawFastHLine(180, 187 + petY, 7, TFT_BLACK);
    tft.drawFastHLine(196, 187 + petY, 7, TFT_BLACK);
  } else {
    tft.fillCircle(183, 187 + petY, 3, TFT_BLACK);
    tft.fillCircle(199, 187 + petY, 3, TFT_BLACK);
    tft.drawPixel(184, 186 + petY, TFT_WHITE); tft.drawPixel(200, 186 + petY, TFT_WHITE);
  }
  tft.fillTriangle(188, 193 + petY, 194, 193 + petY, 191, 197 + petY, TFT_MAGENTA);
  tft.drawLine(191, 197 + petY, 188, 200 + petY, TFT_BLACK);
  tft.drawLine(191, 197 + petY, 194, 200 + petY, TFT_BLACK);
  // Weather-reactive detail around the pet.
  if (weatherStatus.icon == 4) {
    for (int i = 0; i < 4; i++) {
      int dropY = 154 + ((phase * 5 + i * 17) % 24);
      tft.drawLine(153 + i * 22, dropY, 150 + i * 22, dropY + 5, TFT_CYAN);
    }
  } else if (weatherStatus.icon == 5) {
    for (int i = 0; i < 5; i++) {
      int sx = 153 + ((i * 18 + phase * 3) % 75);
      tft.fillCircle(sx, 155 + (i % 3) * 8, 1, TFT_WHITE);
    }
  } else if (weatherStatus.icon == 6) {
    uint16_t flash = phase == 0 || phase == 6 ? TFT_WHITE : TFT_YELLOW;
    tft.drawLine(222, 154, 216, 166, flash);
    tft.drawLine(216, 166, 222, 164, flash);
    tft.drawLine(222, 164, 217, 176, flash);
  } else {
    tft.fillCircle(220, 159, 5 + (phase % 2), TFT_YELLOW);
  }
}

void drawWeatherAnimation() {
  bool animationChanged = weatherStatus.animation != weatherLastAnimation;
  if (weatherStatus.animation != 4 || animationChanged) {
    tft.fillRect(148, 152, 88, 76, TFT_BLACK);
  }
  weatherLastAnimation = weatherStatus.animation;
  if (weatherStatus.animation == 1) drawWeatherHouseAnimation();
  else if (weatherStatus.animation == 2) drawWeatherPlantAnimation();
  else if (weatherStatus.animation == 4) drawWeatherPetAnimation();
  else if (weatherStatus.animation != 3) drawWeatherRobotAnimation();
}
};
