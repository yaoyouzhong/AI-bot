#include <Arduino.h>
#include <ArduinoJson.h>
#include <ESP8266HTTPClient.h>
#include <ESP8266WebServer.h>
#include <ESP8266WiFi.h>
#include <LittleFS.h>
#include <TFT_eSPI.h>
#include <WiFiManager.h>
#include <time.h>

namespace {

constexpr uint32_t kBaudRate = 460800;
constexpr uint32_t kUsbFreshMs = 8000;
constexpr uint32_t kBridgeFreshMs = 8000;
constexpr uint32_t kPollIntervalMs = 2000;
constexpr uint32_t kHelloIntervalMs = 2000;
constexpr uint32_t kPortalDelayMs = 15000;
constexpr char kPrefix[] = "@AIBOT ";
constexpr char kConfigPath[] = "/bridge.json";
constexpr char kBrightnessPath[] = "/brightness.txt";

enum class DisplayMode { Auto, Dual, ScreenSaver };

struct BridgeConfig {
  String host;
  uint16_t port = 8765;
  String token;
};

TFT_eSPI display;
ESP8266WebServer admin(80);
WiFiManager wifiManager;
BridgeConfig bridge;
String inputLine;
String codexState = "offline";
String claudeState = "offline";
DisplayMode displayMode = DisplayMode::Auto;
uint32_t lastUsbStatusAt = 0;
uint32_t lastBridgeStatusAt = 0;
uint32_t lastPollAt = 0;
uint32_t lastHelloAt = 0;
uint32_t startedAt = 0;
uint32_t clockEpochUtc = 0;
uint32_t clockSyncedAt = 0;
int32_t utcOffsetSeconds = 8 * 3600;
int brightness = 100;
int lastClockSecond = -1;
bool showingOffline = false;
bool adminStarted = false;
bool portalStarted = false;
bool screenDirty = true;

bool usbFresh() {
  return lastUsbStatusAt != 0 && millis() - lastUsbStatusAt < kUsbFreshMs;
}

bool bridgeFresh() {
  return lastBridgeStatusAt != 0 && millis() - lastBridgeStatusAt < kBridgeFreshMs;
}

bool validBridgeConfig(const BridgeConfig& value) {
  return value.host.length() > 0 && value.host.length() <= 64 && value.port > 0 &&
         value.token.length() >= 32 && value.token.length() <= 128;
}

uint16_t stateColor(const char* state) {
  if (strcmp(state, "working") == 0) return TFT_GREEN;
  if (strcmp(state, "idle") == 0) return TFT_YELLOW;
  return TFT_DARKGREY;
}

void applyBrightness() {
  analogWrite(TFT_BL, 100 - constrain(brightness, 0, 100));
}

void saveBrightness() {
  File file = LittleFS.open(kBrightnessPath, "w");
  if (!file) return;
  file.println(brightness);
  file.close();
}

void loadBrightness() {
  File file = LittleFS.open(kBrightnessPath, "r");
  if (!file) return;
  int stored = file.readStringUntil('\n').toInt();
  file.close();
  if (stored >= 0 && stored <= 100) brightness = stored;
}

void saveBridgeConfig() {
  if (!validBridgeConfig(bridge)) return;
  File file = LittleFS.open(kConfigPath, "w");
  if (!file) return;
  JsonDocument document;
  document["host"] = bridge.host;
  document["port"] = bridge.port;
  document["token"] = bridge.token;
  serializeJson(document, file);
  file.close();
}

void loadBridgeConfig() {
  File file = LittleFS.open(kConfigPath, "r");
  if (!file) return;
  JsonDocument document;
  DeserializationError error = deserializeJson(document, file);
  file.close();
  if (error) return;
  BridgeConfig loaded;
  loaded.host = document["host"] | "";
  loaded.port = document["port"] | 8765;
  loaded.token = document["token"] | "";
  if (validBridgeConfig(loaded)) bridge = loaded;
}

uint32_t currentEpochUtc() {
  if (clockEpochUtc != 0) return clockEpochUtc + (millis() - clockSyncedAt) / 1000;
  time_t networkTime = time(nullptr);
  return networkTime > 1704067200 ? static_cast<uint32_t>(networkTime) : 0;
}

String clockText(bool includeSeconds = true) {
  uint32_t utc = currentEpochUtc();
  if (utc == 0) return includeSeconds ? "--:--:--" : "--:--";
  time_t local = static_cast<time_t>(static_cast<int64_t>(utc) + utcOffsetSeconds);
  tm parts{};
  gmtime_r(&local, &parts);
  char value[12];
  snprintf(value, sizeof(value), includeSeconds ? "%02d:%02d:%02d" : "%02d:%02d",
           parts.tm_hour, parts.tm_min, parts.tm_sec);
  return String(value);
}

void drawCentered(const String& value, int y, int font, uint16_t color) {
  display.setTextDatum(TL_DATUM);
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
  codexState = data["codex"]["state"] | "offline";
  claudeState = data["claude"]["state"] | "offline";
  uint32_t epoch = data["epochUtc"] | 0;
  if (epoch != 0) {
    clockEpochUtc = epoch;
    clockSyncedAt = millis();
    utcOffsetSeconds = data["utcOffsetSeconds"] | utcOffsetSeconds;
  }

  display.fillScreen(TFT_BLACK);
  drawCentered("AI-bot", 20, 4, TFT_CYAN);
  drawCentered(clockText(), 75, 4, TFT_WHITE);
  display.drawFastHLine(20, 125, 200, TFT_DARKGREY);
  drawTool("CODEX", codexState.c_str(), 148);
  drawTool("CLAUDE", claudeState.c_str(), 182);
  showingOffline = false;
  screenDirty = false;
}

void drawOffline() {
  int second = static_cast<int>(currentEpochUtc() % 60);
  if (showingOffline && second == lastClockSecond) return;
  lastClockSecond = second;
  display.fillScreen(TFT_BLACK);
  drawCentered("AI-bot", 35, 4, TFT_CYAN);
  drawCentered(clockText(), 102, 4, TFT_WHITE);
  display.setTextDatum(TR_DATUM);
  display.setTextColor(TFT_ORANGE, TFT_BLACK);
  display.drawString("PC OFF", 224, 208, 2);
  display.setTextDatum(TL_DATUM);
  showingOffline = true;
}

void drawScreenSaver() {
  int second = static_cast<int>(currentEpochUtc() % 60);
  if (!screenDirty && second == lastClockSecond) return;
  lastClockSecond = second;
  display.fillScreen(TFT_BLACK);
  display.setTextDatum(TL_DATUM);
  display.setTextColor(TFT_GREEN, TFT_BLACK);
  display.drawString(clockText(false), 18 + (second * 3) % 70,
                     52 + (second * 5) % 85, 4);
  display.setTextDatum(TR_DATUM);
  display.setTextColor(TFT_DARKGREY, TFT_BLACK);
  display.drawString(bridgeFresh() ? "BRIDGE" : "PC OFF", 230, 222, 2);
  screenDirty = false;
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
    lastUsbStatusAt = millis();
    lastBridgeStatusAt = millis();
    return;
  }

  if (strcmp(type, "lan_config") == 0 && document["data"].is<JsonObject>()) {
    JsonObjectConst data = document["data"].as<JsonObjectConst>();
    BridgeConfig proposed;
    proposed.host = data["host"] | "";
    proposed.port = data["port"] | 8765;
    proposed.token = data["token"] | "";
    if (validBridgeConfig(proposed)) {
      bridge = proposed;
      saveBridgeConfig();
    }
    return;
  }

  if (strcmp(type, "display") == 0) {
    String mode = document["mode"] | "auto";
    displayMode = mode == "screensaver" ? DisplayMode::ScreenSaver
                : mode == "dual" ? DisplayMode::Dual : DisplayMode::Auto;
    screenDirty = true;
    return;
  }

  if (strcmp(type, "brightness") == 0) {
    brightness = constrain(document["level"] | brightness, 0, 100);
    applyBrightness();
    saveBrightness();
    return;
  }

  if (strcmp(type, "host_going_away") == 0) {
    lastUsbStatusAt = 0;
    lastBridgeStatusAt = 0;
    screenDirty = true;
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

void pollBridge() {
  if (usbFresh() || WiFi.status() != WL_CONNECTED || !validBridgeConfig(bridge)) return;
  if (millis() - lastPollAt < kPollIntervalMs) return;
  lastPollAt = millis();

  WiFiClient client;
  HTTPClient http;
  String url = "http://" + bridge.host + ":" + String(bridge.port) + "/status";
  if (!http.begin(client, url)) return;
  http.setTimeout(1200);
  http.addHeader("X-AIBot-Token", bridge.token);
  int status = http.GET();
  if (status == HTTP_CODE_OK) {
    JsonDocument document;
    if (!deserializeJson(document, http.getStream()) && document["version"].as<int>() == 1) {
      drawStatus(document.as<JsonObjectConst>());
      lastBridgeStatusAt = millis();
    }
  }
  http.end();
}

bool authorizeAdmin() {
  if (validBridgeConfig(bridge) && admin.header("X-AIBot-Token") == bridge.token) return true;
  admin.send(401, "application/json", "{\"error\":\"unauthorized\"}");
  return false;
}

String displayModeName() {
  if (displayMode == DisplayMode::ScreenSaver) return "screensaver";
  if (displayMode == DisplayMode::Dual) return "dual";
  return "auto";
}

void startAdminServer() {
  if (adminStarted || WiFi.status() != WL_CONNECTED) return;
  admin.collectHeaders("X-AIBot-Token");
  admin.on("/api/info", HTTP_GET, [] {
    if (!authorizeAdmin()) return;
    JsonDocument response;
    response["device"] = "AI-bot";
    response["version"] = 1;
    response["ip"] = WiFi.localIP().toString();
    response["usb_active"] = usbFresh();
    response["bridge_online"] = bridgeFresh();
    response["mode"] = displayModeName();
    response["brightness"] = brightness;
    String body;
    serializeJson(response, body);
    admin.send(200, "application/json", body);
  });
  admin.on("/api/display", HTTP_POST, [] {
    if (!authorizeAdmin()) return;
    String mode = admin.arg("mode");
    displayMode = mode == "screensaver" ? DisplayMode::ScreenSaver
                : mode == "dual" ? DisplayMode::Dual : DisplayMode::Auto;
    screenDirty = true;
    admin.send(200, "application/json", "{\"ok\":true}");
  });
  admin.on("/api/brightness", HTTP_POST, [] {
    if (!authorizeAdmin()) return;
    int level = admin.arg("level").toInt();
    if (level < 0 || level > 100) {
      admin.send(400, "application/json", "{\"error\":\"level must be 0..100\"}");
      return;
    }
    brightness = level;
    applyBrightness();
    saveBrightness();
    admin.send(200, "application/json", "{\"ok\":true}");
  });
  admin.on("/reset-wifi", HTTP_POST, [] {
    if (!authorizeAdmin()) return;
    admin.send(200, "application/json", "{\"ok\":true,\"restarting\":true}");
    delay(100);
    wifiManager.resetSettings();
    ESP.restart();
  });
  admin.begin();
  adminStarted = true;
  configTime(0, 0, "ntp.aliyun.com", "ntp.tencent.com", "pool.ntp.org");
}

void serviceWiFi() {
  if (WiFi.status() == WL_CONNECTED) {
    if (portalStarted) {
      wifiManager.stopConfigPortal();
      portalStarted = false;
    }
    startAdminServer();
    admin.handleClient();
    return;
  }

  if (usbFresh() && portalStarted) {
    wifiManager.stopConfigPortal();
    portalStarted = false;
    WiFi.mode(WIFI_STA);
    WiFi.begin();
  }
  if (!usbFresh() && !portalStarted && millis() - startedAt >= kPortalDelayMs) {
    wifiManager.setConfigPortalBlocking(false);
    wifiManager.startConfigPortal("AI-bot-Setup");
    portalStarted = true;
  }
  if (portalStarted) wifiManager.process();
}

}  // namespace

void setup() {
  Serial.begin(kBaudRate);
  startedAt = millis();
  inputLine.reserve(2048);
  LittleFS.begin();
  loadBridgeConfig();
  loadBrightness();

  pinMode(TFT_BL, OUTPUT);
  analogWriteRange(100);
  applyBrightness();
  display.init();
  display.setRotation(0);
  display.setTextDatum(TL_DATUM);
  drawOffline();

  WiFi.mode(WIFI_STA);
  WiFi.setAutoReconnect(true);
  WiFi.begin();
  sendControl("hello");
}

void loop() {
  readSerial();
  serviceWiFi();
  pollBridge();

  if (displayMode == DisplayMode::ScreenSaver) {
    drawScreenSaver();
  } else if (!bridgeFresh()) {
    drawOffline();
  } else if (screenDirty) {
    JsonDocument snapshot;
    snapshot["epochUtc"] = currentEpochUtc();
    snapshot["utcOffsetSeconds"] = utcOffsetSeconds;
    snapshot["codex"]["state"] = codexState;
    snapshot["claude"]["state"] = claudeState;
    drawStatus(snapshot.as<JsonObjectConst>());
  }

  if (!usbFresh() && millis() - lastHelloAt >= kHelloIntervalMs) {
    lastHelloAt = millis();
    sendControl("hello");
  }
  delay(2);
}
