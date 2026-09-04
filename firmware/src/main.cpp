#include <Arduino.h>
#include <ArduinoJson.h>
#include <TFT_eSPI.h>

namespace {

constexpr uint32_t kBaudRate = 460800;
constexpr uint32_t kOfflineAfterMs = 8000;
constexpr uint32_t kHelloIntervalMs = 2000;
constexpr char kPrefix[] = "@AIBOT ";

TFT_eSPI display;
String inputLine;
uint32_t lastStatusAt = 0;
uint32_t lastHelloAt = 0;
bool showingOffline = false;

uint16_t stateColor(const char* state) {
  if (strcmp(state, "working") == 0) return TFT_GREEN;
  if (strcmp(state, "idle") == 0) return TFT_YELLOW;
  return TFT_DARKGREY;
}

void drawCentered(const String& value, int y, int font, uint16_t color) {
  display.setTextColor(color, TFT_BLACK);
  int width = display.textWidth(value, font);
  display.drawString(value, max(0, (240 - width) / 2), y, font);
}

void drawTool(const char* label, const char* state, int y) {
  display.setTextColor(TFT_WHITE, TFT_BLACK);
  display.drawString(label, 22, y, 2);
  display.setTextColor(stateColor(state), TFT_BLACK);
  display.drawRightString(state, 218, y, 2);
}

void drawStatus(JsonObjectConst data) {
  const char* clock = data["time"] | "--:--:--";
  const char* codex = data["codex"]["state"] | "offline";
  const char* claude = data["claude"]["state"] | "offline";

  display.fillScreen(TFT_BLACK);
  drawCentered("AI-bot", 20, 4, TFT_CYAN);
  drawCentered(clock, 75, 4, TFT_WHITE);
  display.drawFastHLine(20, 125, 200, TFT_DARKGREY);
  drawTool("CODEX", codex, 148);
  drawTool("CLAUDE", claude, 182);
  showingOffline = false;
}

void drawOffline() {
  if (showingOffline) return;
  display.fillScreen(TFT_BLACK);
  drawCentered("AI-bot", 35, 4, TFT_CYAN);
  drawCentered("USB OFFLINE", 105, 2, TFT_RED);
  drawCentered("Waiting for bridge", 145, 2, TFT_LIGHTGREY);
  showingOffline = true;
}

void sendControl(const char* type) {
  Serial.print(kPrefix);
  Serial.print("{\"version\":1,\"type\":\"");
  Serial.print(type);
  Serial.println("\",\"device\":\"esp8266\"}");
}

void handleFrame(const String& line) {
  if (!line.startsWith(kPrefix)) return;

  JsonDocument document;
  DeserializationError error = deserializeJson(document, line.c_str() + strlen(kPrefix));
  if (error || document["version"].as<int>() != 1) return;

  const char* type = document["type"] | "";
  if (strcmp(type, "ping") == 0) {
    sendControl("pong");
    return;
  }

  if (strcmp(type, "status") == 0 && document["data"].is<JsonObject>()) {
    drawStatus(document["data"].as<JsonObjectConst>());
    lastStatusAt = millis();
  }
}

void readSerial() {
  while (Serial.available() > 0) {
    char value = static_cast<char>(Serial.read());
    if (value == '\n') {
      inputLine.trim();
      if (!inputLine.isEmpty()) handleFrame(inputLine);
      inputLine.clear();
    } else if (value != '\r') {
      if (inputLine.length() < 1024) inputLine += value;
      else inputLine.clear();
    }
  }
}

}  // namespace

void setup() {
  Serial.begin(kBaudRate);
  inputLine.reserve(1024);
  display.init();
  display.setRotation(0);
  display.setTextDatum(TL_DATUM);
  drawOffline();
  sendControl("hello");
}

void loop() {
  readSerial();
  const uint32_t now = millis();

  if (lastStatusAt == 0 || now - lastStatusAt > kOfflineAfterMs) {
    drawOffline();
    if (now - lastHelloAt >= kHelloIntervalMs) {
      lastHelloAt = now;
      sendControl("hello");
    }
  }

  delay(2);
}
