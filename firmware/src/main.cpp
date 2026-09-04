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
constexpr size_t kMaxBinaryEncoded = 820;
constexpr size_t kMaxBinaryDecoded = 800;

enum class DisplayMode { Auto, Dual, Weather, Stocks, Quotas, Domestic, System, Music, Pet, ScreenSaver };
enum class RenderPage { Dashboard, Weather, Stocks, Quotas, Domestic, System, Music, Pet, ScreenSaver };

struct BridgeConfig {
  String host;
  uint16_t port = 8765;
  String token;
};

struct WeatherState {
  String city;
  String condition;
  float temperature = 0;
  float high = 0;
  float low = 0;
  float pm25 = -1;
  int humidity = 0;
  int airQuality = -1;
  bool stale = true;
  bool available = false;
};

struct StockRow {
  String code;
  String price;
  String percent;
  int trend = 0;
};

struct ProviderQuotaState {
  String plan;
  float primaryPercent = 0;
  float weeklyPercent = 0;
  String primaryReset;
  String weeklyReset;
  int resetCredits = -1;
  bool primaryAvailable = false;
  bool weeklyAvailable = false;
  bool stale = true;
  bool available = false;
};

struct DomesticQuotaState {
  String plan;
  String currency;
  float primaryPercent = 0;
  float weeklyPercent = 0;
  float balance = 0;
  float usedCost = 0;
  String primaryReset;
  String weeklyReset;
  bool primaryAvailable = false;
  bool weeklyAvailable = false;
  bool balanceAvailable = false;
  bool usedCostAvailable = false;
  bool stale = true;
  bool available = false;
};

struct SystemMetricsState {
  float cpuPercent = 0;
  float memoryPercent = 0;
  uint32_t uploadBytesPerSecond = 0;
  uint32_t downloadBytesPerSecond = 0;
  bool available = false;
};

struct MusicState {
  String title;
  String artist;
  bool playing = false;
  float elapsedSeconds = 0;
  float durationSeconds = 0;
  bool available = false;
};

constexpr int kMaxStocks = 20;
constexpr int kStocksPerPage = 4;
WeatherState weather;
StockRow stocks[kMaxStocks];
int stockCount = 0;
int stockPage = 0;
ProviderQuotaState claudeQuota;
ProviderQuotaState codexQuota;
DomesticQuotaState alibabaQuota;
DomesticQuotaState kimiQuota;
DomesticQuotaState miniMaxQuota;
DomesticQuotaState deepSeekQuota;
SystemMetricsState systemMetrics;
MusicState music;

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
int lastStockPageTick = -1;
int lastPetFrame = -1;
RenderPage lastRenderedPage = RenderPage::ScreenSaver;
bool showingOffline = false;
bool adminStarted = false;
bool portalStarted = false;
bool screenDirty = true;
uint8_t binaryEncoded[kMaxBinaryEncoded];
uint8_t binaryDecoded[kMaxBinaryDecoded];
size_t binaryLength = 0;
bool binaryCollecting = false;

struct ResourceTransfer {
  File file;
  uint32_t id = 0;
  uint32_t totalLength = 0;
  uint32_t received = 0;
  uint32_t wholeCrc = 0;
  uint32_t runningCrc = 0xFFFFFFFF;
  uint16_t nextSequence = 0;
  uint16_t totalChunks = 0;
  uint8_t kind = 0;
  bool active = false;
};

ResourceTransfer resourceTransfer;
uint32_t lastCompletedTransferId = 0;
uint16_t lastCompletedSequence = 0;
bool lastCompletedOk = false;

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

void updateQuota(JsonObjectConst value, ProviderQuotaState& target) {
  target.plan = value["plan"] | "";
  target.primaryAvailable = !value["primaryPercent"].isNull();
  target.weeklyAvailable = !value["weeklyPercent"].isNull();
  target.primaryPercent = value["primaryPercent"] | 0.0f;
  target.weeklyPercent = value["weeklyPercent"] | 0.0f;
  target.primaryReset = value["primaryResetsAt"] | "";
  target.weeklyReset = value["weeklyResetsAt"] | "";
  target.resetCredits = value["resetCreditsAvailable"] | -1;
  target.stale = value["stale"] | true;
  target.available = target.primaryAvailable || target.weeklyAvailable || target.resetCredits >= 0;
}

void updateDomesticQuota(JsonObjectConst value, DomesticQuotaState& target) {
  target.plan = value["plan"] | "";
  target.currency = value["currency"] | "";
  target.primaryAvailable = !value["primaryPercent"].isNull();
  target.weeklyAvailable = !value["weeklyPercent"].isNull();
  target.balanceAvailable = !value["balance"].isNull();
  target.usedCostAvailable = !value["usedCost"].isNull();
  target.primaryPercent = value["primaryPercent"] | 0.0f;
  target.weeklyPercent = value["weeklyPercent"] | 0.0f;
  target.balance = value["balance"] | 0.0f;
  target.usedCost = value["usedCost"] | 0.0f;
  target.primaryReset = value["primaryResetsAt"] | "";
  target.weeklyReset = value["weeklyResetsAt"] | "";
  target.stale = value["stale"] | true;
  target.available = target.primaryAvailable || target.weeklyAvailable || target.balanceAvailable;
}

void updateStatus(JsonObjectConst data) {
  codexState = data["codex"]["state"] | "offline";
  claudeState = data["claude"]["state"] | "offline";
  uint32_t epoch = data["epochUtc"] | 0;
  if (epoch != 0) {
    clockEpochUtc = epoch;
    clockSyncedAt = millis();
    utcOffsetSeconds = data["utcOffsetSeconds"] | utcOffsetSeconds;
  }

  if (data["weather"].is<JsonObject>()) {
    JsonObjectConst value = data["weather"].as<JsonObjectConst>();
    weather.city = value["city"] | "";
    weather.condition = value["condition"] | "";
    weather.temperature = value["temperature"] | 0.0f;
    weather.high = value["high"] | 0.0f;
    weather.low = value["low"] | 0.0f;
    weather.humidity = value["humidity"] | 0;
    weather.pm25 = value["pm25"] | -1.0f;
    weather.airQuality = value["airQualityIndex"] | -1;
    weather.stale = value["stale"] | true;
    weather.available = true;
  }

  if (data["stocks"]["quotes"].is<JsonArray>()) {
    stockCount = 0;
    for (JsonObjectConst quote : data["stocks"]["quotes"].as<JsonArrayConst>()) {
      if (stockCount >= kMaxStocks) break;
      stocks[stockCount].code = quote["code"] | "";
      stocks[stockCount].price = quote["price"] | "";
      stocks[stockCount].percent = quote["changePercent"] | "";
      stocks[stockCount].trend = quote["trend"] | 0;
      stockCount++;
    }
    int pages = max(1, (stockCount + kStocksPerPage - 1) / kStocksPerPage);
    if (stockPage >= pages) stockPage = 0;
  }

  if (data["quotas"].is<JsonObject>()) {
    JsonObjectConst quotas = data["quotas"].as<JsonObjectConst>();
    if (quotas["claude"].is<JsonObject>())
      updateQuota(quotas["claude"].as<JsonObjectConst>(), claudeQuota);
    if (quotas["codex"].is<JsonObject>())
      updateQuota(quotas["codex"].as<JsonObjectConst>(), codexQuota);
  }

  if (data["domesticQuotas"].is<JsonObject>()) {
    JsonObjectConst quotas = data["domesticQuotas"].as<JsonObjectConst>();
    if (quotas["alibaba"].is<JsonObject>())
      updateDomesticQuota(quotas["alibaba"].as<JsonObjectConst>(), alibabaQuota);
    if (quotas["kimi"].is<JsonObject>())
      updateDomesticQuota(quotas["kimi"].as<JsonObjectConst>(), kimiQuota);
    if (quotas["miniMax"].is<JsonObject>())
      updateDomesticQuota(quotas["miniMax"].as<JsonObjectConst>(), miniMaxQuota);
    if (quotas["deepSeek"].is<JsonObject>())
      updateDomesticQuota(quotas["deepSeek"].as<JsonObjectConst>(), deepSeekQuota);
  }

  if (data["systemMetrics"].is<JsonObject>()) {
    JsonObjectConst metrics = data["systemMetrics"].as<JsonObjectConst>();
    systemMetrics.cpuPercent = metrics["cpuPercent"] | 0.0f;
    systemMetrics.memoryPercent = metrics["memoryPercent"] | 0.0f;
    systemMetrics.uploadBytesPerSecond = metrics["uploadBytesPerSecond"] | 0;
    systemMetrics.downloadBytesPerSecond = metrics["downloadBytesPerSecond"] | 0;
    systemMetrics.available = true;
  }

  if (data["music"].is<JsonObject>()) {
    JsonObjectConst value = data["music"].as<JsonObjectConst>();
    music.title = value["title"] | "";
    music.artist = value["artist"] | "";
    music.playing = value["playing"] | false;
    music.elapsedSeconds = value["elapsedSeconds"] | 0.0f;
    music.durationSeconds = value["durationSeconds"] | 0.0f;
    music.available = music.title.length() > 0;
  }
  screenDirty = true;
  showingOffline = false;
}

void drawDashboard() {
  display.fillScreen(TFT_BLACK);
  drawCentered("AI-bot", 20, 4, TFT_CYAN);
  drawCentered(clockText(), 75, 4, TFT_WHITE);
  display.drawFastHLine(20, 125, 200, TFT_DARKGREY);
  drawTool("CODEX", codexState.c_str(), 148);
  drawTool("CLAUDE", claudeState.c_str(), 182);
  showingOffline = false;
  screenDirty = false;
}

void drawWeather() {
  display.fillScreen(TFT_BLACK);
  drawCentered("WEATHER", 10, 2, TFT_CYAN);
  drawCentered(clockText(false), 34, 4, TFT_WHITE);
  if (!weather.available) {
    drawCentered("Waiting for data", 116, 2, TFT_DARKGREY);
    screenDirty = false;
    return;
  }

  String temperature = String(static_cast<int>(roundf(weather.temperature))) + " C";
  drawCentered(temperature, 82, 4, TFT_ORANGE);
  drawCentered(String(static_cast<int>(roundf(weather.low))) + " / " +
               String(static_cast<int>(roundf(weather.high))), 126, 2, TFT_LIGHTGREY);
  display.setTextDatum(TL_DATUM);
  display.setTextColor(TFT_GREEN, TFT_BLACK);
  display.drawString("HUMID", 24, 164, 2);
  display.setTextDatum(TR_DATUM);
  display.setTextColor(TFT_WHITE, TFT_BLACK);
  display.drawString(String(weather.humidity) + "%", 216, 164, 2);
  display.setTextDatum(TL_DATUM);
  display.setTextColor(TFT_YELLOW, TFT_BLACK);
  display.drawString("PM2.5", 24, 194, 2);
  display.setTextDatum(TR_DATUM);
  display.setTextColor(TFT_WHITE, TFT_BLACK);
  display.drawString(weather.pm25 >= 0 ? String(weather.pm25, 1) : "--", 216, 194, 2);
  if (weather.stale) {
    display.setTextColor(TFT_ORANGE, TFT_BLACK);
    display.drawString("STALE", 216, 220, 1);
  }
  display.setTextDatum(TL_DATUM);
  display.setTextColor(TFT_DARKGREY, TFT_BLACK);
  display.drawString("OPEN-METEO", 6, 226, 1);
  screenDirty = false;
}

void drawStocks() {
  int pages = max(1, (stockCount + kStocksPerPage - 1) / kStocksPerPage);
  int tick = static_cast<int>((millis() / 5000) % pages);
  if (tick != lastStockPageTick) {
    stockPage = tick;
    lastStockPageTick = tick;
    screenDirty = true;
  }
  if (!screenDirty) return;

  display.fillScreen(TFT_BLACK);
  drawCentered(pages > 1 ? "STOCKS " + String(stockPage + 1) + "/" + String(pages) : "STOCKS",
               6, 2, TFT_CYAN);
  if (stockCount == 0) {
    drawCentered("Waiting for data", 110, 2, TFT_DARKGREY);
    screenDirty = false;
    return;
  }

  int start = stockPage * kStocksPerPage;
  for (int row = 0; row < kStocksPerPage && start + row < stockCount; row++) {
    StockRow& quote = stocks[start + row];
    int y = 38 + row * 49;
    display.setTextDatum(TL_DATUM);
    display.setTextColor(TFT_LIGHTGREY, TFT_BLACK);
    display.drawString(quote.code, 12, y, 2);
    display.setTextColor(TFT_WHITE, TFT_BLACK);
    display.drawString(quote.price, 12, y + 21, 2);
    display.setTextDatum(TR_DATUM);
    uint16_t color = quote.trend > 0 ? TFT_RED : quote.trend < 0 ? TFT_GREEN : TFT_LIGHTGREY;
    display.setTextColor(color, TFT_BLACK);
    display.drawString(quote.percent, 228, y + 21, 2);
  }
  screenDirty = false;
}

String resetClock(const String& value) {
  int separator = value.indexOf('T');
  return separator >= 0 && static_cast<int>(value.length()) >= separator + 6
      ? value.substring(separator + 1, separator + 6) : "--:--";
}

void drawQuotaWindow(const char* label, bool available, float percent,
                     const String& reset, int y) {
  display.setTextDatum(TL_DATUM);
  display.setTextColor(TFT_LIGHTGREY, TFT_BLACK);
  display.drawString(label, 18, y, 2);
  display.setTextDatum(TR_DATUM);
  display.setTextColor(available ? TFT_WHITE : TFT_DARKGREY, TFT_BLACK);
  String value = available ? String(percent, 1) + "%" : "--";
  display.drawString(value + "  R " + resetClock(reset), 224, y, 2);
}

void drawQuotaProvider(const char* label, const ProviderQuotaState& quota, int y) {
  display.setTextDatum(TL_DATUM);
  display.setTextColor(TFT_CYAN, TFT_BLACK);
  display.drawString(label, 12, y, 2);
  display.setTextDatum(TR_DATUM);
  display.setTextColor(quota.stale ? TFT_ORANGE : TFT_DARKGREY, TFT_BLACK);
  display.drawString(quota.stale ? "STALE" : quota.plan, 228, y, 2);
  drawQuotaWindow("5H", quota.primaryAvailable, quota.primaryPercent, quota.primaryReset, y + 23);
  drawQuotaWindow("7D", quota.weeklyAvailable, quota.weeklyPercent, quota.weeklyReset, y + 46);
}

void drawQuotas() {
  display.fillScreen(TFT_BLACK);
  drawCentered("ACCOUNT QUOTAS", 5, 2, TFT_WHITE);
  if (!claudeQuota.available && !codexQuota.available) {
    drawCentered("Waiting for data", 110, 2, TFT_DARKGREY);
    screenDirty = false;
    return;
  }
  drawQuotaProvider("CLAUDE", claudeQuota, 32);
  drawQuotaProvider("CODEX", codexQuota, 112);
  if (codexQuota.resetCredits >= 0) {
    display.setTextDatum(TL_DATUM);
    display.setTextColor(TFT_LIGHTGREY, TFT_BLACK);
    display.drawString("RESET CREDITS", 12, 195, 2);
    display.setTextDatum(TR_DATUM);
    display.setTextColor(TFT_GREEN, TFT_BLACK);
    display.drawString(String(codexQuota.resetCredits), 228, 195, 2);
  }
  screenDirty = false;
}

void drawDomesticRow(const char* label, const DomesticQuotaState& quota, int y) {
  display.setTextDatum(TL_DATUM);
  display.setTextColor(quota.stale ? TFT_ORANGE : TFT_CYAN, TFT_BLACK);
  display.drawString(label, 10, y, 2);
  display.setTextDatum(TR_DATUM);
  display.setTextColor(quota.available ? TFT_WHITE : TFT_DARKGREY, TFT_BLACK);
  String value = "--";
  if (quota.balanceAvailable) value = quota.currency + " " + String(quota.balance, 2);
  else if (quota.weeklyAvailable) value = String(quota.weeklyPercent, 1) + "% WK";
  else if (quota.primaryAvailable) value = String(quota.primaryPercent, 1) + "% 5H";
  display.drawString(value, 230, y, 2);
  display.setTextDatum(TL_DATUM);
  display.setTextColor(TFT_DARKGREY, TFT_BLACK);
  String detail = quota.plan;
  if (quota.usedCostAvailable) detail += " USED " + String(quota.usedCost, 2);
  else if (quota.weeklyAvailable && quota.weeklyReset.length() > 0)
    detail += " R " + resetClock(quota.weeklyReset);
  display.drawString(detail.substring(0, 31), 10, y + 20, 1);
}

void drawDomestic() {
  display.fillScreen(TFT_BLACK);
  drawCentered("DOMESTIC QUOTAS", 5, 2, TFT_WHITE);
  drawDomesticRow("QWEN", alibabaQuota, 34);
  drawDomesticRow("KIMI", kimiQuota, 80);
  drawDomesticRow("MINIMAX", miniMaxQuota, 126);
  drawDomesticRow("DEEPSEEK", deepSeekQuota, 172);
  screenDirty = false;
}

String rateText(uint32_t bytesPerSecond) {
  if (bytesPerSecond >= 1024UL * 1024UL)
    return String(bytesPerSecond / (1024.0f * 1024.0f), 1) + " MB/s";
  if (bytesPerSecond >= 1024UL)
    return String(bytesPerSecond / 1024.0f, 1) + " KB/s";
  return String(bytesPerSecond) + " B/s";
}

void drawMetric(const char* label, const String& value, int y, uint16_t color) {
  display.setTextDatum(TL_DATUM);
  display.setTextColor(TFT_LIGHTGREY, TFT_BLACK);
  display.drawString(label, 18, y, 2);
  display.setTextDatum(TR_DATUM);
  display.setTextColor(color, TFT_BLACK);
  display.drawString(value, 222, y, 2);
}

void drawSystem() {
  display.fillScreen(TFT_BLACK);
  drawCentered("SYSTEM", 8, 2, TFT_CYAN);
  if (!systemMetrics.available) {
    drawCentered("Waiting for data", 110, 2, TFT_DARKGREY);
    screenDirty = false;
    return;
  }
  drawMetric("CPU", String(systemMetrics.cpuPercent, 1) + "%", 48, TFT_GREEN);
  drawMetric("MEMORY", String(systemMetrics.memoryPercent, 1) + "%", 88, TFT_ORANGE);
  display.drawFastHLine(18, 125, 204, TFT_DARKGREY);
  drawMetric("UPLOAD", rateText(systemMetrics.uploadBytesPerSecond), 146, TFT_YELLOW);
  drawMetric("DOWNLOAD", rateText(systemMetrics.downloadBytesPerSecond), 186, TFT_CYAN);
  screenDirty = false;
}

bool drawRgb565File(const char* path, int x, int y, int width, int height) {
  File file = LittleFS.open(path, "r");
  if (!file || file.size() != static_cast<size_t>(width * height * 2) || width > 232) {
    if (file) file.close();
    return false;
  }
  uint16_t row[232];
  for (int line = 0; line < height; line++) {
    if (file.read(reinterpret_cast<uint8_t*>(row), width * 2) != width * 2) {
      file.close();
      return false;
    }
    display.pushImage(x, y + line, width, 1, row);
  }
  file.close();
  return true;
}

String durationText(float seconds) {
  int value = constrain(static_cast<int>(seconds), 0, 359999);
  int remainder = value % 60;
  return String(value / 60) + ":" + (remainder < 10 ? "0" : "") + String(remainder);
}

void drawMusic() {
  display.fillScreen(TFT_BLACK);
  drawCentered(music.playing ? "NOW PLAYING" : "MUSIC", 4, 2,
               music.playing ? TFT_GREEN : TFT_DARKGREY);
  if (!music.available) {
    drawCentered("No active session", 108, 2, TFT_DARKGREY);
    screenDirty = false;
    return;
  }
  bool hasCover = drawRgb565File("/cover.rgb565", 64, 25, 112, 112);
  if (!hasCover) {
    display.drawRoundRect(64, 25, 112, 112, 8, TFT_DARKGREY);
    drawCentered("NO COVER", 75, 2, TFT_DARKGREY);
  }
  bool hasText = drawRgb565File("/text.rgb565", 4, 142, 232, 44);
  if (!hasText) {
    drawCentered(music.title.substring(0, 25), 146, 2, TFT_WHITE);
    drawCentered(music.artist.substring(0, 28), 168, 1, TFT_LIGHTGREY);
  }
  float ratio = music.durationSeconds > 0
      ? constrain(music.elapsedSeconds / music.durationSeconds, 0.0f, 1.0f) : 0;
  display.drawRect(18, 192, 204, 8, TFT_DARKGREY);
  display.fillRect(20, 194, static_cast<int>(200 * ratio), 4, TFT_CYAN);
  display.setTextDatum(TL_DATUM);
  display.setTextColor(TFT_LIGHTGREY, TFT_BLACK);
  display.drawString(durationText(music.elapsedSeconds), 18, 207, 1);
  display.setTextDatum(TR_DATUM);
  display.drawString(durationText(music.durationSeconds), 222, 207, 1);
  drawCentered(music.playing ? "PLAY" : "PAUSE", 222, 1,
               music.playing ? TFT_GREEN : TFT_YELLOW);
  screenDirty = false;
}

void drawPixelPetBody(int x, int y, bool step, bool working) {
  uint16_t shell = working ? TFT_CYAN : TFT_DARKGREY;
  display.fillRect(x + 27, y, 4, 10, shell);
  display.fillRect(x + 23, y, 12, 4, shell);
  display.fillRoundRect(x + 8, y + 10, 42, 35, 5, shell);
  display.fillRect(x + 14, y + 17, 30, 19, TFT_BLACK);
  display.fillRect(x + 20, y + 23, 5, 6, working ? TFT_GREEN : TFT_LIGHTGREY);
  display.fillRect(x + 34, y + 23, 5, 6, working ? TFT_GREEN : TFT_LIGHTGREY);
  display.fillRoundRect(x + 12, y + 48, 34, 42, 5, shell);
  display.fillRect(x + 20, y + 59, 18, 5, TFT_BLACK);
  display.fillRect(x + 5, y + 54, 7, 27, shell);
  display.fillRect(x + 46, y + 54, 7, 27, shell);
  int leftFoot = step ? 6 : 15;
  int rightFoot = step ? 32 : 41;
  display.fillRect(x + leftFoot, y + 90, 17, 8, shell);
  display.fillRect(x + rightFoot, y + 90, 17, 8, shell);
}

void drawPet() {
  bool working = codexState == "working" || claudeState == "working";
  int frame = working ? static_cast<int>(millis() / 180) : 0;
  if (!screenDirty && frame == lastPetFrame) return;
  lastPetFrame = frame;
  bool step = (frame & 1) != 0;
  int x = 91;
  if (working) {
    int travel = frame % 56;
    if (travel > 28) travel = 56 - travel;
    x = 16 + travel * 5;
  }

  display.fillScreen(TFT_BLACK);
  drawCentered("BYTE SPROUT", 12, 2, working ? TFT_GREEN : TFT_CYAN);
  drawPixelPetBody(x, 54, step, working);
  drawCentered(working ? "WORKING" : "IDLE", 180, 2,
               working ? TFT_GREEN : TFT_YELLOW);
  String owner = codexState == "working" && claudeState == "working" ? "CODEX + CLAUDE"
      : codexState == "working" ? "CODEX" : claudeState == "working" ? "CLAUDE" : "READY";
  drawCentered(owner, 207, 2, TFT_LIGHTGREY);
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

uint16_t readLe16(const uint8_t* value) {
  return static_cast<uint16_t>(value[0]) | static_cast<uint16_t>(value[1]) << 8;
}

uint32_t readLe32(const uint8_t* value) {
  return static_cast<uint32_t>(value[0]) | static_cast<uint32_t>(value[1]) << 8 |
         static_cast<uint32_t>(value[2]) << 16 | static_cast<uint32_t>(value[3]) << 24;
}

uint32_t updateCrc32(uint32_t crc, const uint8_t* data, size_t length) {
  for (size_t index = 0; index < length; index++) {
    crc ^= data[index];
    for (int bit = 0; bit < 8; bit++)
      crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320UL : crc >> 1;
  }
  return crc;
}

size_t cobsDecode(const uint8_t* input, size_t length, uint8_t* output, size_t capacity) {
  size_t read = 0;
  size_t write = 0;
  while (read < length) {
    uint8_t code = input[read++];
    if (code == 0 || read + code - 1 > length) return 0;
    for (uint8_t index = 1; index < code; index++) {
      if (write >= capacity) return 0;
      output[write++] = input[read++];
    }
    if (code != 0xFF && read < length) {
      if (write >= capacity) return 0;
      output[write++] = 0;
    }
  }
  return write;
}

void sendResourceAck(uint32_t transferId, uint16_t sequence, bool ok) {
  Serial.print(kPrefix);
  Serial.print("{\"version\":1,\"type\":\"resource_ack\",\"transferId\":");
  Serial.print(transferId);
  Serial.print(",\"sequence\":");
  Serial.print(sequence);
  Serial.print(",\"ok\":");
  Serial.print(ok ? "true" : "false");
  Serial.println("}");
}

const char* resourcePath(uint8_t kind) {
  if (kind == 1) return "/text.rgb565";
  if (kind == 2) return "/cover.rgb565";
  if (kind == 3) return "/pet.asset";
  return nullptr;
}

void abandonResourceTransfer() {
  if (resourceTransfer.file) resourceTransfer.file.close();
  LittleFS.remove("/resource.new");
  resourceTransfer = ResourceTransfer{};
}

bool commitResourceTransfer() {
  const char* target = resourcePath(resourceTransfer.kind);
  if (target == nullptr) return false;
  String backup = String(target) + ".bak";
  LittleFS.remove(backup);
  bool hadTarget = LittleFS.exists(target);
  if (hadTarget && !LittleFS.rename(target, backup)) return false;
  if (!LittleFS.rename("/resource.new", target)) {
    if (hadTarget) LittleFS.rename(backup, target);
    return false;
  }
  if (hadTarget) LittleFS.remove(backup);
  return true;
}

void handleBinaryFrame(const uint8_t* encoded, size_t encodedLength) {
  size_t length = cobsDecode(encoded, encodedLength, binaryDecoded, sizeof(binaryDecoded));
  if (length < 28 || memcmp(binaryDecoded, "AIB1", 4) != 0 || binaryDecoded[4] != 1) return;
  uint8_t kind = binaryDecoded[5];
  uint32_t transferId = readLe32(binaryDecoded + 6);
  uint16_t sequence = readLe16(binaryDecoded + 10);
  uint16_t totalChunks = readLe16(binaryDecoded + 12);
  uint32_t totalLength = readLe32(binaryDecoded + 14);
  uint16_t payloadLength = readLe16(binaryDecoded + 18);
  uint32_t wholeCrc = readLe32(binaryDecoded + 20);
  bool headerValid = resourcePath(kind) != nullptr && totalChunks > 0 && totalLength > 0 &&
      totalLength <= 1024UL * 1024UL && payloadLength <= 768 &&
      length == static_cast<size_t>(24 + payloadLength + 4);
  if (!headerValid) {
    sendResourceAck(transferId, sequence, false);
    return;
  }
  uint32_t expectedChunkCrc = readLe32(binaryDecoded + 24 + payloadLength);
  uint32_t actualChunkCrc = ~updateCrc32(0xFFFFFFFF, binaryDecoded, 24 + payloadLength);
  if (actualChunkCrc != expectedChunkCrc) {
    sendResourceAck(transferId, sequence, false);
    return;
  }

  if (!resourceTransfer.active && transferId == lastCompletedTransferId &&
      sequence == lastCompletedSequence) {
    sendResourceAck(transferId, sequence, lastCompletedOk);
    return;
  }

  if (resourceTransfer.active && resourceTransfer.id == transferId &&
      sequence + 1 == resourceTransfer.nextSequence) {
    sendResourceAck(transferId, sequence, true);
    return;
  }
  if (sequence == 0 && (!resourceTransfer.active || resourceTransfer.id != transferId)) {
    abandonResourceTransfer();
    resourceTransfer.file = LittleFS.open("/resource.new", "w");
    if (!resourceTransfer.file) {
      sendResourceAck(transferId, sequence, false);
      return;
    }
    resourceTransfer.id = transferId;
    resourceTransfer.totalLength = totalLength;
    resourceTransfer.wholeCrc = wholeCrc;
    resourceTransfer.totalChunks = totalChunks;
    resourceTransfer.kind = kind;
    resourceTransfer.active = true;
  }
  if (!resourceTransfer.active || resourceTransfer.id != transferId ||
      resourceTransfer.kind != kind || resourceTransfer.totalLength != totalLength ||
      resourceTransfer.totalChunks != totalChunks || resourceTransfer.wholeCrc != wholeCrc ||
      resourceTransfer.nextSequence != sequence ||
      resourceTransfer.received + payloadLength > resourceTransfer.totalLength) {
    sendResourceAck(transferId, sequence, false);
    return;
  }

  size_t written = resourceTransfer.file.write(binaryDecoded + 24, payloadLength);
  if (written != payloadLength) {
    abandonResourceTransfer();
    sendResourceAck(transferId, sequence, false);
    return;
  }
  resourceTransfer.runningCrc = updateCrc32(resourceTransfer.runningCrc,
                                             binaryDecoded + 24, payloadLength);
  resourceTransfer.received += payloadLength;
  resourceTransfer.nextSequence++;

  bool complete = resourceTransfer.nextSequence == resourceTransfer.totalChunks;
  if (!complete) {
    sendResourceAck(transferId, sequence, true);
    return;
  }
  resourceTransfer.file.close();
  bool valid = resourceTransfer.received == resourceTransfer.totalLength &&
               ~resourceTransfer.runningCrc == resourceTransfer.wholeCrc;
  if (valid) valid = commitResourceTransfer();
  if (!valid) LittleFS.remove("/resource.new");
  if (valid) screenDirty = true;
  resourceTransfer.active = false;
  lastCompletedTransferId = transferId;
  lastCompletedSequence = sequence;
  lastCompletedOk = valid;
  sendResourceAck(transferId, sequence, valid);
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
    updateStatus(document["data"].as<JsonObjectConst>());
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
                : mode == "weather" ? DisplayMode::Weather
                : mode == "stocks" ? DisplayMode::Stocks
                : mode == "quotas" ? DisplayMode::Quotas
                : mode == "domestic" ? DisplayMode::Domestic
                : mode == "system" ? DisplayMode::System
                : mode == "music" ? DisplayMode::Music
                : mode == "pet" ? DisplayMode::Pet
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
    uint8_t raw = static_cast<uint8_t>(Serial.read());
    if (raw == 0) {
      if (!binaryCollecting) {
        binaryCollecting = true;
        binaryLength = 0;
      } else {
        if (binaryLength > 0) handleBinaryFrame(binaryEncoded, binaryLength);
        binaryCollecting = false;
        binaryLength = 0;
      }
      continue;
    }
    if (binaryCollecting) {
      if (binaryLength < sizeof(binaryEncoded)) binaryEncoded[binaryLength++] = raw;
      else {
        binaryCollecting = false;
        binaryLength = 0;
      }
      continue;
    }
    char value = static_cast<char>(raw);
    if (value == '\n') {
      inputLine.trim();
      if (!inputLine.isEmpty()) handleFrame(inputLine);
      inputLine.clear();
    } else if (value != '\r') {
      if (inputLine.length() < 6144) inputLine += value;
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
      updateStatus(document.as<JsonObjectConst>());
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
  if (displayMode == DisplayMode::Weather) return "weather";
  if (displayMode == DisplayMode::Stocks) return "stocks";
  if (displayMode == DisplayMode::Quotas) return "quotas";
  if (displayMode == DisplayMode::Domestic) return "domestic";
  if (displayMode == DisplayMode::System) return "system";
  if (displayMode == DisplayMode::Music) return "music";
  if (displayMode == DisplayMode::Pet) return "pet";
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
                : mode == "weather" ? DisplayMode::Weather
                : mode == "stocks" ? DisplayMode::Stocks
                : mode == "quotas" ? DisplayMode::Quotas
                : mode == "domestic" ? DisplayMode::Domestic
                : mode == "system" ? DisplayMode::System
                : mode == "music" ? DisplayMode::Music
                : mode == "pet" ? DisplayMode::Pet
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

RenderPage desiredPage() {
  if (displayMode == DisplayMode::ScreenSaver) return RenderPage::ScreenSaver;
  if (displayMode == DisplayMode::Weather) return RenderPage::Weather;
  if (displayMode == DisplayMode::Stocks) return RenderPage::Stocks;
  if (displayMode == DisplayMode::Quotas) return RenderPage::Quotas;
  if (displayMode == DisplayMode::Domestic) return RenderPage::Domestic;
  if (displayMode == DisplayMode::System) return RenderPage::System;
  if (displayMode == DisplayMode::Music) return RenderPage::Music;
  if (displayMode == DisplayMode::Pet) return RenderPage::Pet;
  if (displayMode == DisplayMode::Dual) return RenderPage::Dashboard;

  if (music.playing) return RenderPage::Music;

  RenderPage pages[8];
  int count = 0;
  pages[count++] = RenderPage::Dashboard;
  pages[count++] = RenderPage::Pet;
  if (weather.available) pages[count++] = RenderPage::Weather;
  if (stockCount > 0) pages[count++] = RenderPage::Stocks;
  if (claudeQuota.available || codexQuota.available) pages[count++] = RenderPage::Quotas;
  if (alibabaQuota.available || kimiQuota.available || miniMaxQuota.available || deepSeekQuota.available)
    pages[count++] = RenderPage::Domestic;
  if (systemMetrics.available) pages[count++] = RenderPage::System;
  if (music.available) pages[count++] = RenderPage::Music;
  return pages[(millis() / 15000) % count];
}

void renderCurrentPage() {
  RenderPage page = desiredPage();
  if (page != lastRenderedPage) {
    lastRenderedPage = page;
    screenDirty = true;
  }
  if (page == RenderPage::ScreenSaver) drawScreenSaver();
  else if (!bridgeFresh()) drawOffline();
  else if (page == RenderPage::Weather) drawWeather();
  else if (page == RenderPage::Stocks) drawStocks();
  else if (page == RenderPage::Quotas) drawQuotas();
  else if (page == RenderPage::Domestic) drawDomestic();
  else if (page == RenderPage::System) drawSystem();
  else if (page == RenderPage::Music) drawMusic();
  else if (page == RenderPage::Pet) drawPet();
  else if (screenDirty) drawDashboard();
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

  renderCurrentPage();

  if (!usbFresh() && millis() - lastHelloAt >= kHelloIntervalMs) {
    lastHelloAt = millis();
    sendControl("hello");
  }
  delay(2);
}
