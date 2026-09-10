#include <Arduino.h>
#include <ArduinoJson.h>
#include <ESP8266HTTPClient.h>
#include <ESP8266WebServer.h>
#include <ESP8266WiFi.h>
#include <LittleFS.h>
#include <TFT_eSPI.h>
#include <WiFiManager.h>
#include <time.h>
#include <vector>
#include "ScreenSaverGeometry.h"
#include "WeatherAnimations.h"
#include "ResetCountdown.h"

namespace {

constexpr uint32_t kBaudRate = 460800;
// One maximum JSON line (6144 bytes), a resource chunk and control traffic must
// survive a synchronous screen/flash operation without overrunning the UART.
constexpr size_t kSerialRxBufferBytes = 8192;
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

enum class DisplayMode { Auto, Dual, Weather, Stocks, Quotas, Domestic, System, Music, Pet, ScreenSaver, Claude, Codex, Activity, DomesticAlibaba, DomesticKimi, DomesticMinimax, DomesticDeepseek, DomesticZhipu };
enum class RenderPage { Dashboard, Weather, Stocks, Quotas, Domestic, System, Music, Pet, ScreenSaver, Claude, Codex };
struct ModeName { const char* name; DisplayMode mode; };
const ModeName modeNames[] = {
  {"auto",DisplayMode::Auto},{"dual",DisplayMode::Dual},{"weather",DisplayMode::Weather},{"stocks",DisplayMode::Stocks},
  {"quotas",DisplayMode::Quotas},{"domestic",DisplayMode::Domestic},{"system",DisplayMode::System},{"music",DisplayMode::Music},
  {"pet",DisplayMode::Pet},{"screensaver",DisplayMode::ScreenSaver},{"claude",DisplayMode::Claude},{"codex",DisplayMode::Codex},
  {"activity",DisplayMode::Activity},{"domestic_alibaba",DisplayMode::DomesticAlibaba},{"domestic_kimi",DisplayMode::DomesticKimi},
  {"domestic_minimax",DisplayMode::DomesticMinimax},{"domestic_deepseek",DisplayMode::DomesticDeepseek},{"domestic_zhipu",DisplayMode::DomesticZhipu}
};
DisplayMode parseDisplayMode(const String& name) {
  for (const auto& entry : modeNames) if (name == entry.name) return entry.mode;
  return DisplayMode::Auto;
}
DisplayMode effectiveDisplayMode = DisplayMode::Auto;
DisplayMode cyclePages[16] = {DisplayMode::Codex,DisplayMode::Claude,DisplayMode::Weather,DisplayMode::Stocks};
int cycleCount = 4, cycleInterval = 15;
bool cycleEnabled = true;
int64_t cycleStartedAt = 0;

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
  int icon = 0;
  int animation = 0;
  int headerCenterX = 61;
  int rangeY = 34;
  int32_t utcOffset = 0;
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
  std::vector<uint32_t> resetExpirations;
  bool primaryAvailable = false;
  bool weeklyAvailable = false;
  bool stale = true;
  bool available = false;
};

struct DomesticQuotaState {
  float planPercent = 0;
  bool planAvailable = false;
  String planReset;
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
  bool hasArtwork = false;
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
DomesticQuotaState zhipuQuota;
SystemMetricsState systemMetrics;
uint32_t netUp[224] = {}, netDown[224] = {};
String netSampleStamp;
String netSession;
uint32_t netSequence=0, netQueueUp[32]={},netQueueDown[32]={};
int netQueueHead=0,netQueueCount=0;
uint32_t systemChartFrames=0,systemChromeDraws=0,systemNumberDraws=0,systemSamplesConsumed=0;
void queueNetworkSample(uint32_t up,uint32_t down) {
  if(netQueueCount==32){netQueueHead=(netQueueHead+1)%32;--netQueueCount;}
  int tail=(netQueueHead+netQueueCount)%32;netQueueUp[tail]=up;netQueueDown[tail]=down;++netQueueCount;
}
MusicState music;

TFT_eSPI display;
ESP8266WebServer admin(80);
WiFiManager wifiManager;
BridgeConfig bridge;
String inputLine;
String codexState = "offline";
String bridgeFollowApp;
String claudeState = "offline";
bool codexNeedsInput = false, claudeNeedsInput = false, domesticNeedsInput = false, completionActive = false;
uint32_t completionSequence = 0, completionStarted = 0;
String domesticActivityProvider, domesticActivityState;
uint32_t codexTokensToday = 0, claudeTokensToday = 0, domesticTokensToday = 0;
DisplayMode displayMode = DisplayMode::Auto;
uint32_t lastUsbStatusAt = 0;
uint32_t lastBridgeStatusAt = 0;
uint32_t usbStatusCount = 0;
uint32_t lanStatusCount = 0;
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
uint32_t visualGeneration = 0;
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

bool drawRgb565File(const char* path, int x, int y, int width, int height);
bool drawRgb565FileRegion(const char* path, int sourceWidth, int sourceHeight,
                          int sourceY, int x, int y, int width, int height);

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
  target.plan.trim();target.plan.toUpperCase();target.plan.replace("_"," ");target.plan.replace("-"," ");
  if(target.plan.startsWith("CLAUDE "))target.plan.remove(0,7);
  if(target.plan=="PROLITE")target.plan="PRO LITE";
  if(target.plan=="MAX5X")target.plan="MAX 5X";
  if(target.plan=="MAX20X")target.plan="MAX 20X";
  target.primaryAvailable = !value["primaryPercent"].isNull();
  target.weeklyAvailable = !value["weeklyPercent"].isNull();
  target.primaryPercent = value["primaryPercent"] | 0.0f;
  target.weeklyPercent = value["weeklyPercent"] | 0.0f;
  target.primaryReset = value["primaryResetsAt"] | "";
  target.weeklyReset = value["weeklyResetsAt"] | "";
  target.resetCredits = value["resetCreditsAvailable"] | -1;
  target.resetExpirations.clear();
  for (JsonVariantConst item : value["resetCreditExpiresAt"].as<JsonArrayConst>()) {
    if (item.is<uint32_t>() && item.as<uint32_t>() > 0)
      target.resetExpirations.push_back(item.as<uint32_t>());
  }
  target.stale = value["stale"] | true;
  target.available = target.primaryAvailable || target.weeklyAvailable || target.resetCredits >= 0 ||
      !target.resetExpirations.empty();
}

void updateDomesticQuota(JsonObjectConst value, DomesticQuotaState& target) {
  target.planAvailable = !value["planPercent"].isNull();
  target.planPercent = value["planPercent"] | 0.0f;
  target.planReset = value["planResetsAt"] | "";
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
  target.available = target.primaryAvailable || target.weeklyAvailable || target.planAvailable || target.balanceAvailable;
}

void updateMetrics(JsonObjectConst metrics) {
  String stamp = metrics["updatedAt"] | "";
  // Both heartbeat and fast metrics contain headers; an older heartbeat must
  // neither move the graph backwards nor replace newer displayed numbers.
  if(stamp.length() && netSampleStamp.length() && stamp < netSampleStamp)return;
  systemMetrics.cpuPercent = metrics["cpuPercent"] | 0.0f;
  systemMetrics.memoryPercent = metrics["memoryPercent"] | 0.0f;
  systemMetrics.uploadBytesPerSecond = metrics["uploadBytesPerSecond"] | 0U;
  systemMetrics.downloadBytesPerSecond = metrics["downloadBytesPerSecond"] | 0U;
  systemMetrics.available = true;
  String session=metrics["sampleSession"]|"";
  if(session.length()) {
    if(session!=netSession){netSession=session;netSequence=0;netQueueHead=netQueueCount=0;}
    if(metrics["samples"].is<JsonArrayConst>()) {
      auto samples=metrics["samples"].as<JsonArrayConst>();
      uint32_t sequence=metrics["sampleSequence"]|0U;
      if(sequence>=samples.size() && sequence>netSequence) {
        uint32_t first=sequence-samples.size()+1,index=0;
        for(JsonObjectConst sample:samples){if(first+index>netSequence)queueNetworkSample(sample["upload"]|0U,sample["download"]|0U);++index;}
        netSequence=sequence;
      }
    }
  } else if(stamp.length()==0 || stamp!=netSampleStamp) {
    queueNetworkSample(systemMetrics.uploadBytesPerSecond,systemMetrics.downloadBytesPerSecond);
  }
  netSampleStamp = stamp;
  if (effectiveDisplayMode == DisplayMode::System) screenDirty = true;
}

void updateStatus(JsonObjectConst data) {
  if (data["displayPolicy"].is<JsonObjectConst>()) {
    auto policy = data["displayPolicy"].as<JsonObjectConst>();
    displayMode = parseDisplayMode(policy["selectedMode"] | "auto");
    cycleEnabled = policy["cycleEnabled"] | true;
    cycleStartedAt = policy["cycleStartedAt"] | (int64_t)0;
    int interval = policy["intervalSeconds"] | 15;
    cycleInterval = interval == 10 || interval == 15 || interval == 30 || interval == 60 ? interval : 15;
    if (policy["pages"].is<JsonArrayConst>()) {
      int count = 0;
      for (auto page : policy["pages"].as<JsonArrayConst>()) {
        auto mode = parseDisplayMode(page.as<String>());
        if (mode != DisplayMode::Auto && mode != DisplayMode::ScreenSaver && count < 16) cyclePages[count++] = mode;
      }
      if (count > 0) cycleCount = count;
    }
  }
  // Read-only variants require Const type checks; mutable checks always fail.
  codexState = data["codex"]["state"] | "offline";
  bridgeFollowApp = data["followApp"] | "";
  if (bridgeFollowApp != "claude" && bridgeFollowApp != "codex") bridgeFollowApp = "";
  claudeState = data["claude"]["state"] | "offline";
  codexNeedsInput = data["codex"]["needsInput"] | false;
  claudeNeedsInput = data["claude"]["needsInput"] | false;
  domesticNeedsInput = data["domesticActivity"]["needsInput"] | false;
  domesticActivityProvider = data["domesticActivity"]["activeProvider"] | "";
  domesticActivityState = data["domesticActivity"]["state"] | "offline";
  codexTokensToday = data["codex"]["tokensToday"] | 0U;
  claudeTokensToday = data["claude"]["tokensToday"] | 0U;
  domesticTokensToday = data["domesticActivity"]["tokensToday"] | 0U;
  uint32_t sequence = data["codex"]["completionSequence"] | 0U;
  completionActive = data["codex"]["completionActive"] | false;
  if (sequence != completionSequence) { completionSequence = sequence; completionStarted = millis(); }
  uint32_t epoch = data["epochUtc"] | 0;
  if (epoch != 0) {
    clockEpochUtc = epoch;
    clockSyncedAt = millis();
    utcOffsetSeconds = data["utcOffsetSeconds"] | utcOffsetSeconds;
  }

  if (data["weather"].is<JsonObjectConst>()) {
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
    int wmo = value["weatherCode"] | -1;
    int fallbackIcon = wmo == 0 ? 0 : wmo == 1 || wmo == 2 ? 1 : wmo == 3 ? 2 :
      wmo == 45 || wmo == 48 ? 3 : (wmo >= 51 && wmo <= 67) || (wmo >= 80 && wmo <= 82) ? 4 :
      (wmo >= 71 && wmo <= 77) || wmo == 85 || wmo == 86 ? 5 : wmo >= 95 && wmo <= 99 ? 6 : 2;
    weather.icon = constrain(value["animationIcon"] | fallbackIcon, 0, 6);
    String animation = value["animation"] | "robot";
    weather.animation = animation == "house" ? 1 : animation == "plant" ? 2 : animation == "off" ? 3 : animation == "pet" ? 4 : 0;
    weather.headerCenterX = constrain(value["headerCenterX"] | 61, 0, 121);
    weather.rangeY = constrain(value["rangeY"] | 34, 32, 35);
    weather.utcOffset = constrain(value["utcOffsetSeconds"] | utcOffsetSeconds, -50400, 50400);
  }

  if (data["stocks"]["quotes"].is<JsonArrayConst>()) {
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

  if (data["quotas"].is<JsonObjectConst>()) {
    JsonObjectConst quotas = data["quotas"].as<JsonObjectConst>();
    if (quotas["claude"].is<JsonObjectConst>())
      updateQuota(quotas["claude"].as<JsonObjectConst>(), claudeQuota);
    if (quotas["codex"].is<JsonObjectConst>())
      updateQuota(quotas["codex"].as<JsonObjectConst>(), codexQuota);
  }

  if (data["domesticQuotas"].is<JsonObjectConst>()) {
    JsonObjectConst quotas = data["domesticQuotas"].as<JsonObjectConst>();
    if (quotas["alibaba"].is<JsonObjectConst>())
      updateDomesticQuota(quotas["alibaba"].as<JsonObjectConst>(), alibabaQuota);
    if (quotas["kimi"].is<JsonObjectConst>())
      updateDomesticQuota(quotas["kimi"].as<JsonObjectConst>(), kimiQuota);
    if (quotas["miniMax"].is<JsonObjectConst>())
      updateDomesticQuota(quotas["miniMax"].as<JsonObjectConst>(), miniMaxQuota);
    if (quotas["deepSeek"].is<JsonObjectConst>())
      updateDomesticQuota(quotas["deepSeek"].as<JsonObjectConst>(), deepSeekQuota);
    if (quotas["zhipu"].is<JsonObjectConst>())
      updateDomesticQuota(quotas["zhipu"].as<JsonObjectConst>(), zhipuQuota);
  }

  if (data["systemMetrics"].is<JsonObjectConst>()) {
    updateMetrics(data["systemMetrics"].as<JsonObjectConst>());
  }

  if (data["music"].is<JsonObjectConst>()) {
    JsonObjectConst value = data["music"].as<JsonObjectConst>();
    music.title = value["title"] | "";
    music.artist = value["artist"] | "";
    music.playing = value["playing"] | false;
    music.elapsedSeconds = value["elapsedSeconds"] | 0.0f;
    music.durationSeconds = value["durationSeconds"] | 0.0f;
    music.available = music.title.length() > 0;
    music.hasArtwork = value["hasArtwork"] | true;
  }
  else { music = MusicState(); }
  screenDirty = true;
  showingOffline = false;
}

uint32_t activityChromeDraws=0,activityClockDraws=0,activityDataDraws=0;
void drawDashboard() {
  static uint32_t generation=UINT32_MAX;
  static String prior[6];
  bool chrome=generation!=visualGeneration;
  if(chrome) {
    generation=visualGeneration; ++activityChromeDraws;
    for(auto& key:prior)key="";
    display.fillScreen(TFT_BLACK);
    drawCentered("AI-bot",20,4,TFT_CYAN);
    display.drawFastHLine(20,110,200,TFT_DARKGREY);
    display.setTextDatum(TL_DATUM);display.setTextColor(TFT_WHITE,TFT_BLACK);
    display.drawString("CODEX",22,120,2);display.drawString("CLAUDE",22,146,2);
    drawCentered("Local tokens today",169,2,0x9492);
    display.setTextColor(TFT_LIGHTGREY,TFT_BLACK);
    display.drawString("Codex",22,189,2);display.drawString("Claude",22,209,2);
  }
  auto region=[&](int index,const String& key,const String& text,int x,int y,int w,int h,int font,uint16_t color,int align) {
    if(prior[index]==key)return;
    TFT_eSprite cell(&display);cell.setColorDepth(16);
    if(!cell.createSprite(w,h))return;
    cell.fillSprite(TFT_BLACK);cell.setTextColor(color,TFT_BLACK);cell.setTextDatum(TL_DATUM);
    int left=align==1?(w-cell.textWidth(text,font))/2:align==2?w-cell.textWidth(text,font):0;
    cell.drawString(text,max(0,left),0,font);cell.pushSprite(x,y);cell.deleteSprite();
    prior[index]=key;
    if(index==0)++activityClockDraws;else ++activityDataDraws;
  };
  String time=clockText();
  region(0,time,time,0,75,240,28,4,TFT_WHITE,1);
  region(1,codexState,codexState,138,120,80,18,2,stateColor(codexState.c_str()),2);
  region(2,claudeState,claudeState,138,146,80,18,2,stateColor(claudeState.c_str()),2);
  String credits=codexQuota.resetCredits>0?"R*"+String(codexQuota.resetCredits):"";
  region(3,credits+String(codexQuota.stale),credits,86,120,50,18,2,codexQuota.stale?TFT_ORANGE:TFT_GREEN,0);
  region(4,String(codexTokensToday),String(codexTokensToday),92,189,126,18,2,TFT_GREEN,2);
  region(5,String(claudeTokensToday),String(claudeTokensToday),92,209,126,18,2,TFT_GREEN,2);
  showingOffline = false;
  screenDirty = false;
}

// Heartbeats and unrelated provider updates must not repaint static page chrome.
bool pageContentChanged(int slot, const String& key) {
  static String keys[6];
  static uint32_t generations[6] = {UINT32_MAX,UINT32_MAX,UINT32_MAX,UINT32_MAX,UINT32_MAX,UINT32_MAX};
  bool changed = generations[slot] != visualGeneration || keys[slot] != key;
  generations[slot] = visualGeneration; keys[slot] = key;
  return changed;
}

void drawWeather() {
  static int lastSecond = -1;
  static int lastWeatherMinute = -1, lastWeatherHour = -1;
  static uint32_t lastAnimation = 0;
  static WeatherAnimations animations(display);
  if (!weather.available) {
    if (screenDirty) {
      display.fillScreen(TFT_BLACK);
      drawCentered("Set weather city in tray", 102, 2, TFT_LIGHTGREY);
      screenDirty = false;
    }
    return;
  }
  String key = weather.city + "|" + weather.condition + "|" + String(weather.temperature) + "|" + String(weather.high)
    + "|" + String(weather.low) + "|" + String(weather.humidity) + "|" + String(weather.airQuality)
    + "|" + String(weather.icon) + "|" + String(weather.animation) + "|" + String(weather.utcOffset);
  const bool redraw = pageContentChanged(0,key);
  if (redraw) {
    display.fillScreen(TFT_BLACK);
    if (!drawRgb565File("/weather-header.rgb565", 14, 1, 122, 26))
      drawRgb565File("/weather-text.rgb565", 4, 1, 232, 24);
    drawRgb565File("/weather-air.rgb565", 136, 12, 100, 30);
    drawRgb565File("/weather-date.rgb565", 14, 117, 190, 30);
    String low = "L " + String(static_cast<int>(roundf(weather.low))) + "C";
    String high = "H " + String(static_cast<int>(roundf(weather.high))) + "C";
    int left = max(2, 14 + weather.headerCenterX - (display.textWidth(low, 2) + 10 + display.textWidth(high, 2)) / 2);
    display.setTextDatum(TL_DATUM);
    display.setTextColor(TFT_CYAN, TFT_BLACK);
    display.drawString(low, left, weather.rangeY, 2);
    display.drawString(low, left + 1, weather.rangeY, 2);
    left += display.textWidth(low, 2) + 10;
    display.setTextColor(TFT_ORANGE, TFT_BLACK);
    display.drawString(high, left, weather.rangeY, 2);
    display.drawString(high, left + 1, weather.rangeY, 2);
    for (int row = 0; row < 2; row++) {
      int y = row == 0 ? 162 : 199;
      int value = row == 0 ? static_cast<int>(roundf(weather.temperature)) : weather.humidity;
      uint16_t color = row == 0 ? TFT_RED : TFT_GREEN;
      display.fillCircle(row==0?18:19, y + 13, row == 0 ? 4 : 5, color);
      if (row == 0) display.fillRoundRect(16, y, 5, 14, 2, color);
      else display.fillTriangle(14, y + 13, 24, y + 13, 19, y, color);
      display.setTextDatum(TL_DATUM);
      display.setTextColor(TFT_LIGHTGREY, TFT_BLACK);
      display.drawString(row == 0 ? "TEMP" : "HUMID", 30, row==0?y:y-1, 1);
      display.fillRoundRect(30, y + 15, 60, 5, 3, TFT_DARKGREY);
      int filled = row == 0 ? constrain((value + 10) * 3 / 2, 0, 60) : constrain(value * 60 / 100, 0, 60);
      if (filled > 0) display.fillRoundRect(30, y + 15, filled, 5, 3, color);
      int numberRight = 144 - max(display.textWidth("C", 4), display.textWidth("%", 4)) - 2;
      display.setTextColor(TFT_WHITE, TFT_BLACK);
      display.drawRightString(String(value), numberRight, y, 4);
      display.drawString(row == 0 ? "C" : "%", numberRight + 2, y, 4);
    }
    lastSecond = -1;
    lastWeatherMinute = lastWeatherHour = -1;
  }
  time_t local = static_cast<time_t>(static_cast<int64_t>(currentEpochUtc()) + weather.utcOffset);
  struct tm parts;
  gmtime_r(&local, &parts);
  if (parts.tm_sec != lastSecond) {
    char hour[3], minute[3], second[3];
    strftime(hour, sizeof(hour), "%H", &parts);
    strftime(minute, sizeof(minute), "%M", &parts);
    strftime(second, sizeof(second), "%S", &parts);
    int width = display.textWidth(hour, 7) + display.textWidth(minute, 7) + 54;
    int x = (240 - width) / 2 - 4;
    display.setTextDatum(TL_DATUM);
    if(parts.tm_min!=lastWeatherMinute||parts.tm_hour!=lastWeatherHour) {
    display.fillRect(0,52,240,63,TFT_BLACK);
    display.setTextColor(TFT_WHITE, TFT_BLACK);
    display.drawString(hour, x, 57, 7);
    display.setTextColor(TFT_ORANGE, TFT_BLACK);
    display.drawString(minute, x+display.textWidth(hour,7)+10, 57, 7);
    lastWeatherMinute=parts.tm_min;lastWeatherHour=parts.tm_hour;
    }
    x += display.textWidth(hour,7)+display.textWidth(minute,7)+18;
    display.fillRect(x-2,70,40,39,TFT_BLACK);
    display.setTextColor(TFT_LIGHTGREY, TFT_BLACK);
    // Legacy device geometry: 16x30 cells, 3px strokes, 20px step.
    const char* segments[] = {"02356789","2345689","0235689","045689","01234789","0268","013456789"};
    const int rects[7][4] = {{3,0,10,3},{3,14,10,3},{3,27,10,3},{0,3,3,12},{13,3,3,12},{0,15,3,12},{13,15,3,12}};
    for (int digit=0;digit<2;++digit) for (int segment=0;segment<7;++segment)
      if (strchr(segments[segment],second[digit])) display.fillRoundRect(x+digit*20+rects[segment][0],75+rects[segment][1],rects[segment][2],rects[segment][3],1,TFT_LIGHTGREY);
    lastSecond = parts.tm_sec;
  }
  uint32_t frame = millis() / 350;
  if (redraw || frame != lastAnimation) {
    animations.draw(weather.icon, weather.animation, frame % 12);
    lastAnimation = frame;
  }
  screenDirty = false;
}

void drawStocks() {
  static uint32_t pageStarted = 0, pageGeneration = UINT32_MAX;
  int pages = max(1, (stockCount + kStocksPerPage - 1) / kStocksPerPage);
  if (pageGeneration != visualGeneration) {pageStarted=millis();pageGeneration=visualGeneration;}
  int tick = static_cast<int>(((millis()-pageStarted) / 5000) % pages);
  if (tick != lastStockPageTick) {
    stockPage = tick;
    lastStockPageTick = tick;
    screenDirty = true;
  }
  String key = String(stockPage) + "|" + String(stockCount);
  for(int i=0;i<stockCount;i++) key += "|" + stocks[i].code + "|" + stocks[i].price + "|" + stocks[i].percent + "|" + String(stocks[i].trend);
  if (!pageContentChanged(1,key)) {screenDirty=false;return;}

  display.fillScreen(TFT_BLACK);
  drawCentered(pages > 1 ? "STOCKS " + String(stockPage + 1) + "/" + String(pages) : "STOCKS",
               228, 1, TFT_DARKGREY);
  if (stockCount == 0) {
    drawCentered("Waiting for data", 110, 2, TFT_DARKGREY);
    screenDirty = false;
    return;
  }

  int start = stockPage * kStocksPerPage;
  for (int row = 0; row < kStocksPerPage && start + row < stockCount; row++) {
    StockRow& quote = stocks[start + row];
    int y = 6 + row * 54;
    bool hasName = drawRgb565FileRegion("/stock-names.rgb565", 156, 400,
                                        (start + row) * 20, 70, y, 156, 20);
    if (!hasName) hasName = drawRgb565FileRegion("/stock-names.rgb565",120,400,
                                        (start + row)*20,106,y,120,20);
    display.setTextDatum(TL_DATUM);
    display.setTextColor(TFT_DARKGREY, TFT_BLACK);
    display.drawString(quote.code, 14, y, 2);
    display.setTextDatum(TL_DATUM);
    if (!hasName) {
      display.setTextColor(TFT_LIGHTGREY, TFT_BLACK);
      display.drawRightString(quote.code, 226, y, 2);
    }
    display.setTextColor(TFT_WHITE, TFT_BLACK);
    display.drawString(quote.price, 14, y + 22, 4);
    display.setTextDatum(TR_DATUM);
    uint16_t color = quote.trend > 0 ? TFT_RED : quote.trend < 0 ? TFT_GREEN : TFT_LIGHTGREY;
    display.setTextColor(color, TFT_BLACK);
    display.drawString(quote.percent, 226, y + 22, 4);
  }
  screenDirty = false;
}

String resetClock(const String& value) {
  int64_t reset = quotaResetEpoch(value.c_str());
  if (reset < 0) return "";
  int64_t remaining = reset - static_cast<int64_t>(currentEpochUtc());
  uint32_t minutes = remaining > 0 ? static_cast<uint32_t>((remaining + 59) / 60) : 0;
  if (minutes >= 1440) return String(minutes / 1440) + "d " + String(minutes % 1440 / 60) + "h";
  if (minutes >= 60) return String(minutes / 60) + "h " + String(minutes % 60) + "m";
  return String(minutes) + "m";
}


// Reserve a footer outside the pet image/animation bounds. No sprite can erase it.
void drawResetCredits(int y) {
  const size_t known = codexQuota.resetExpirations.size();
  const int missing = max(0, codexQuota.resetCredits - static_cast<int>(known));
  const size_t rows = known + ((missing > 0 || (known == 0 && codexQuota.resetCredits >= 0)) ? 1 : 0);
  if (rows == 0) return;
  const size_t pages = (rows + 1) / 2;
  const size_t page = (currentEpochUtc() / 4) % pages;
  display.setTextDatum(TL_DATUM);
  for (size_t index = page * 2; index < rows && index < page * 2 + 2; ++index) {
    const int top = y + (index % 2) * 18;
    display.setTextColor(codexQuota.stale ? TFT_ORANGE : TFT_GREEN, TFT_BLACK);
    display.drawString("R*" + String(index < known ? 1 : missing), 16, top, 2);
    String date = "--";
    if (index < known) {
      time_t local = static_cast<time_t>(static_cast<int64_t>(codexQuota.resetExpirations[index]) + utcOffsetSeconds);
      struct tm parts;
      gmtime_r(&local, &parts);
      date = String(parts.tm_mon + 1) + "/" + String(parts.tm_mday);
    }
    display.drawRightString(date, 180, top, 2);
  }
  if (pages > 1) {
    display.setTextColor(TFT_LIGHTGREY, TFT_BLACK);
    display.drawRightString(String(page + 1) + "/" + String(pages), 228, y + 5, 1);
  }
}

String quotaVisualKey(const ProviderQuotaState& q) {
  String key=q.plan+"|"+String(q.primaryAvailable)+"|"+String(q.weeklyAvailable)+"|"+String(q.primaryPercent,3)
    +"|"+String(q.weeklyPercent,3)+"|"+resetClock(q.primaryReset)+"|"+resetClock(q.weeklyReset)+"|"+String(q.resetCredits);
  for(auto expiry:q.resetExpirations)key+="|"+String(expiry);
  return key;
}

void drawQuotas() {
  if(!pageContentChanged(2,quotaVisualKey(claudeQuota)+"|"+quotaVisualKey(codexQuota)+"|"+claudeState+"|"+codexState)){screenDirty=false;return;}
  display.fillScreen(TFT_BLACK);
  display.setTextDatum(TC_DATUM);display.setTextColor(TFT_WHITE);
  display.drawString("USAGE OVERVIEW",120,8,2);display.drawString("USAGE OVERVIEW",121,8,2);
  display.drawFastHLine(18, 121, 204, 0x2945);
  for (int index = 0; index < 2; index++) {
    const auto& q = index == 0 ? claudeQuota : codexQuota;
    const String& state = index == 0 ? claudeState : codexState;
    int top = index == 0 ? 29 : 126;
    display.fillCircle(18, top + 9, 4, state == "working" ? TFT_GREEN : state == "idle" ? TFT_YELLOW : 0x39E7);
    display.setTextDatum(TL_DATUM);
    display.setTextColor(index == 0 ? TFT_ORANGE : TFT_CYAN);
    display.drawString(index == 0 ? "CLAUDE" : "CODEX", 31, top, 2);
    display.drawString(index == 0 ? "CLAUDE" : "CODEX", 32, top, 2);
    if (index == 1 && q.resetCredits > 0) {
      display.setTextColor(TFT_GREEN);display.setTextDatum(MC_DATUM);
      display.drawString("R*" + String(q.resetCredits), 98, top+8, 2);
      display.drawString("R*" + String(q.resetCredits), 99, top+8, 2);
    }
    if(q.plan.length()) {
      uint16_t color=q.plan=="PLUS"?TFT_CYAN:q.plan=="PRO"||q.plan=="PRO LITE"||q.plan=="MAX"||q.plan=="MAX 5X"||q.plan=="MAX 20X"?TFT_ORANGE:q.plan=="TEAM"||q.plan=="BUSINESS"||q.plan=="ENTERPRISE"?TFT_MAGENTA:TFT_LIGHTGREY;
      int width=constrain(display.textWidth(q.plan,2)+16,40,112),left=220-width;
      display.fillRoundRect(left,top,width,17,4,TFT_BLACK);
      display.setTextDatum(MC_DATUM);display.setTextColor(color);
      display.drawString(q.plan,left+width/2,top+8,2);
      display.drawRoundRect(left,top,width,17,4,color);
    }
    bool weeklyOnly = index == 1 && !q.primaryAvailable;
    for (int row = weeklyOnly ? 1 : 0; row < 2; row++) {
      int y = weeklyOnly ? top + 34 : top + 21 + row * 33;
      bool known = row == 0 ? q.primaryAvailable : q.weeklyAvailable;
      float pct = row == 0 ? q.primaryPercent : q.weeklyPercent;
      display.setTextDatum(TL_DATUM);
      display.setTextColor(0x7BEF, TFT_BLACK);
      display.drawString(row == 0 ? "5H" : "WK", 20, y + 4, 2);
      display.drawString(resetClock(row == 0 ? q.primaryReset : q.weeklyReset), 54, y + 7, 1);
      display.setTextColor(TFT_WHITE, TFT_BLACK);
      display.drawRightString(known ? String(static_cast<int>(pct)) + "%" : "--", 220, y, 4);
      display.fillRoundRect(20, y + 24, 200, 6, 3, 0x2104);
      int width = known ? constrain(static_cast<int>(pct * 2), 0, 200) : 0;
      if (width > 0) display.fillRoundRect(20, y + 24, width, 6, 3, pct >= 99.5 ? TFT_RED : pct >= 80 ? TFT_YELLOW : TFT_GREEN);
    }
  }
  screenDirty = false;
}

bool readPetHeader(File& file, uint8_t& count, uint16_t* delays, uint16_t& width, uint16_t& height) {
  uint8_t header[12];
  if (!file || file.read(header, 12) != 12 || memcmp(header, "APET", 4) != 0 || (header[4] != 1 && header[4] != 2) ||
      header[5] < 1 || header[5] > 8 || header[10] != 0 || header[11] != 0) return false;
  width = header[6] | (uint16_t(header[7]) << 8); height = header[8] | (uint16_t(header[9]) << 8);
  if (width < 1 || width > 120 || height < 1 || height > 120 || (header[4] == 1 && (width != 112 || height != 112))) return false;
  count = header[5];
  if (file.size() != 12U + 2U * count + uint32_t(width) * height * 2U * count) return false;
  for (int i = 0; i < count; ++i) {
    uint8_t bytes[2]; if (file.read(bytes, 2) != 2) return false;
    delays[i] = bytes[0] | (static_cast<uint16_t>(bytes[1]) << 8);
    if (delays[i] < 20 || delays[i] > 60000) return false;
  }
  return true;
}

bool drawPetAnimation(int x, int y, bool force = false, bool animate = true, bool claude = false, bool protectCredits = false) {
  const char* slotPath = claude ? "/claude.apet" : "/codex.apet";
  const char* path = LittleFS.exists(slotPath) ? slotPath : "/pet.apet";
  File file = LittleFS.open(path, "r");
  uint8_t count = 0; uint16_t delays[8], width, height;
  if (!readPetHeader(file, count, delays, width, height)) return false;
  x += 56 - width / 2; y += 56 - height / 2;
  uint32_t duration = 0; for (int i = 0; i < count; ++i) duration += delays[i];
  static uint32_t previousTicks[2] = {}, elapsedTimes[2] = {};
  uint32_t& previousTick = previousTicks[claude ? 0 : 1];
  uint32_t& elapsed = elapsedTimes[claude ? 0 : 1];
  uint32_t tick = millis();
  if (animate) elapsed += min(uint32_t(250), tick - previousTick);
  previousTick = tick;
  uint32_t phase = elapsed % duration; int frame = 0;
  while (frame + 1 < count && phase >= delays[frame]) { phase -= delays[frame]; ++frame; }
  static int previousFrame = -1, previousY = -1;
  static String previousPath;
  if (!force && frame == previousFrame && y == previousY && previousPath == path) return true;
  if (!file.seek(12U + 2U * count + frame * uint32_t(width) * height * 2U, SeekSet)) return false;
  uint16_t row[120];
  for (int line = 0; line < height; ++line) {
    if (file.read(reinterpret_cast<uint8_t*>(row), width * 2U) != static_cast<int>(width * 2U)) return false;
    int rows=codexQuota.resetExpirations.empty()?(codexQuota.resetCredits>0?1:0):codexQuota.resetExpirations.size();
    int badgeTop=max(15,29-(rows-1)*19/2),badgeBottom=badgeTop+rows*19-1;
    if(protectCredits && rows>0 && y+line>=badgeTop && y+line<badgeBottom) {
      int left=constrain(153-x,0,(int)width),right=constrain(221-x,0,(int)width);
      if(left>0)display.pushImage(x,y+line,left,1,row);
      if(right<width)display.pushImage(x+right,y+line,width-right,1,row+right);
    } else display.pushImage(x, y + line, width, 1, row);
  }
  previousFrame = frame; previousY = y; previousPath = path;
  return true;
}

float lastRingPercent = 0;
void drawPercentageRing(float pct) {
  lastRingPercent = pct;
  uint16_t track=0x2104;
  display.fillRect(4,4,232,10,track);display.fillRect(226,4,10,232,track);
  display.fillRect(4,226,232,10,track);display.fillRect(4,4,10,232,track);
  int remaining = constrain(static_cast<int>(pct * 928 / 100), 0, 928);
  int segment = min(remaining, 232);
  if (segment > 0) display.fillRect(4, 4, segment, 10, TFT_GREEN);
  remaining -= 232; segment = constrain(remaining, 0, 232);
  if (segment > 0) display.fillRect(226, 4, 10, segment, TFT_GREEN);
  remaining -= 232; segment = constrain(remaining, 0, 232);
  if (segment > 0) display.fillRect(236 - segment, 226, segment, 10, TFT_GREEN);
  remaining -= 232; segment = constrain(remaining, 0, 232);
  if (segment > 0) display.fillRect(4, 236 - segment, 10, segment, TFT_GREEN);
}

void drawSingleCreditBadge() {
  const auto& q=codexQuota;
  int details=q.resetExpirations.size(),rows=details>0?details:q.resetCredits>0?1:0;
  if(rows==0)return;
  int top=max(15,29-(rows-1)*19/2),height=rows*19-1;
  display.fillRoundRect(153,top,68,height,5,TFT_BLACK);
  display.drawRoundRect(153,top,68,height,5,TFT_GREEN);
  display.setTextDatum(ML_DATUM);display.setTextColor(TFT_GREEN,TFT_BLACK);
  for(int i=0;i<rows;i++) {
    String count=details>0?"R*1":"R*"+String(q.resetCredits),date;
    if(details>0) {time_t local=static_cast<time_t>(static_cast<int64_t>(q.resetExpirations[i])+utcOffsetSeconds);struct tm parts;gmtime_r(&local,&parts);date=String(parts.tm_mon+1)+"/"+String(parts.tm_mday);}
    int countWidth=display.textWidth(count,2),dateWidth=display.textWidth(date,2),gap=date.length()?3:0,left=187-(countWidth+gap+dateWidth)/2;
    display.drawString(count,left,top+i*19+9,2);if(date.length())display.drawString(date,left+countWidth+gap,top+i*19+9,2);
  }
  display.setTextDatum(TL_DATUM);
}

void drawPixelPetBody(int x, int y, bool step, bool working);

void drawDefaultQuotaPet(bool animate, bool force) {
  static uint32_t lastTick = 0;
  static bool lastAnimate = false;
  uint32_t tick = millis() / 240;
  if (!force && lastAnimate == animate && (!animate || tick == lastTick)) return;
  lastTick = tick; lastAnimate = animate;
  // Stay below the plan header, above quota rows and left of credit badges.
  display.fillRect(91,70,62,98,TFT_BLACK);
  drawPixelPetBody(91,70,animate && (tick % 2),animate);
}

void drawSingleQuota(bool claude) {
  static String previousKey;
  static bool staticPetAvailable = false;
  static uint32_t previousGeneration = UINT32_MAX;
  bool animate = (claude ? claudeState : codexState) == "working" || (!claude && completionActive && millis()-completionStarted<3500);
  const auto& q = claude ? claudeQuota : codexQuota;
  float pct = claude ? (q.primaryAvailable?q.primaryPercent:0) : q.weeklyAvailable?q.weeklyPercent:q.primaryAvailable?q.primaryPercent:0;
  String key = String(claude) + "|" + q.plan + "|" + String(pct,3) + "|" + String(q.primaryAvailable) + "|" + String(q.weeklyAvailable)
    + "|" + String(q.primaryPercent,3) + "|" + String(q.weeklyPercent,3) + "|" + resetClock(q.primaryReset) + "|" + resetClock(q.weeklyReset) + "|" + String(q.resetCredits);
  for (auto expiry : q.resetExpirations) key += "|" + String((long long)expiry);
  if (previousGeneration == visualGeneration && key == previousKey) {
    if (!drawPetAnimation(64,64,false,animate,claude,!claude) && !staticPetAvailable)
      drawDefaultQuotaPet(animate,false);
    screenDirty=false;
    return;
  }
  previousKey=key;previousGeneration=visualGeneration;
  display.fillScreen(TFT_BLACK);
  drawPercentageRing(pct);
  display.setTextDatum(TL_DATUM);
  display.setTextColor(claude ? TFT_ORANGE : TFT_CYAN, TFT_BLACK);
  if(!drawRgb565File(claude?"/claude-logo.rgb565":"/codex-logo.rgb565",14,18,40,40))
    display.drawString(claude ? "CLAUDE" : "CODEX", 14, 31, 1);
  if (q.plan.length()) {
    uint16_t color=q.plan=="PLUS" ? TFT_CYAN : q.plan=="MAX"||q.plan=="MAX 5X"||q.plan=="MAX 20X"||q.plan=="PRO"||q.plan=="PRO LITE" ? TFT_ORANGE :
      q.plan=="TEAM" || q.plan=="BUSINESS" || q.plan=="ENTERPRISE" ? TFT_MAGENTA : TFT_LIGHTGREY;
    int width=constrain(display.textWidth(q.plan,2)+12,34,!claude && (q.resetCredits>0||q.resetExpirations.size()>0)?88:98);
    display.drawRoundRect(61,29,width,18,5,color);
    display.setTextDatum(MC_DATUM);display.setTextColor(color,TFT_BLACK);display.drawString(q.plan,61+width/2,38,2);
    display.setTextDatum(TL_DATUM);
  }
  staticPetAvailable = false;
  if (!drawPetAnimation(64, 64, true, animate, claude)) {
    staticPetAvailable = drawRgb565File("/pet.asset", 64, 64, 112, 112);
    if (!staticPetAvailable) drawDefaultQuotaPet(animate,true);
  }
  if (!claude)drawSingleCreditBadge();
  bool weeklyOnly = !q.primaryAvailable && (!claude || q.weeklyAvailable);
  for (int row = weeklyOnly ? 1 : 0; row < 2; row++) {
    int y = weeklyOnly ? 191 : 178 + row * 23;
    bool known = row == 0 ? q.primaryAvailable : q.weeklyAvailable;
    display.fillRoundRect(20, y, 200, weeklyOnly?24:21, weeklyOnly?7:6, 0x1082);
    display.drawRoundRect(20, y, 200, weeklyOnly?24:21, weeklyOnly?7:6, 0x29A5);
    int centerY=y+(weeklyOnly?12:11);
    display.setTextDatum(MC_DATUM);
    auto bold=[&](const String& text,int centerX,uint16_t color) {
      display.setTextColor(color);
      display.drawString(text,centerX,centerY,2);display.drawString(text,centerX+1,centerY,2);
    };
    bold(row == 0 ? "5H" : "WK",53,0x9492);
    bold(known ? String(constrain(static_cast<int>(row == 0 ? q.primaryPercent : q.weeklyPercent),0,100)) + "%" : "--",120,TFT_WHITE);
    bold(resetClock(row == 0 ? q.primaryReset : q.weeklyReset),187,TFT_CYAN);
  }
  display.setTextDatum(TL_DATUM);
  screenDirty = false;
}


String domesticMembership(const String& input,bool balance) {
  if(balance)return "API PAYG";
  if(input.length()==0)return "";
  String lower=input;lower.toLowerCase();
  if(effectiveDisplayMode==DisplayMode::DomesticMinimax){String upper=input;upper.toUpperCase();return upper;}
  if(effectiveDisplayMode!=DisplayMode::DomesticAlibaba)return input;
  const char* names[]={"CODING PLAN","TEAM","ENTERPRISE","PERSONAL","PRO","STANDARD","BASIC"};
  const char* english[]={"coding","team","enterprise","personal","professional","standard","basic"};
  const char* chinese[]={"coding","团队","企业","个人","专业","标准","基础"};
  for(int i=0;i<7;i++)if(lower.indexOf(english[i])>=0||input.indexOf(chinese[i])>=0)return names[i];
  return lower.indexOf("individual")>=0?"PERSONAL":"TOKEN PLAN";
}

bool domesticScaledText(const String& text,int font,int sourceW,int sourceH,int x,int y,int w,int h,uint16_t color,bool bold) {
  if(sourceW<1||sourceW>1024||w<1||w>200)return false;
  TFT_eSprite mask(&display);mask.setColorDepth(1);
  if(!mask.createSprite(sourceW,sourceH))return false;
  mask.fillSprite(TFT_BLACK);mask.setTextColor(TFT_WHITE);mask.setTextDatum(TL_DATUM);
  mask.drawString(text,0,0,font);if(bold)mask.drawString(text,1,0,font);
  uint16_t row[200];
  for(int dy=0;dy<h;dy++) {
    for(int dx=0;dx<w;dx++)row[dx]=mask.readPixel(dx*sourceW/w,dy*sourceH/h)?color:TFT_BLACK;
    display.pushImage(x,y+dy,w,1,row);
  }
  mask.deleteSprite();return true;
}

void drawDomesticBalance(const String& amount,const String& currency) {
  int sourceW=display.textWidth(amount,7)+1,numberW=(sourceW*5+3)/6;
  int sourceUnit=display.textWidth(currency,4),unitW=(sourceUnit*10+6)/13,gap=currency.length()?6:0;
  int font=7,height=40,captionFont=2;bool scaled=true;
  if(numberW+gap+unitW>184){font=4;scaled=false;numberW=display.textWidth(amount,font)+1;height=display.fontHeight(font);}
  if(numberW+gap+unitW>184){font=2;captionFont=1;scaled=false;numberW=display.textWidth(amount,font)+1;height=display.fontHeight(font);}
  int captionH=display.fontHeight(captionFont),top=62+(106-captionH-8-height)/2,valueY=top+captionH+8,left=120-(numberW+gap+unitW)/2;
  display.setTextDatum(TC_DATUM);display.setTextColor(0x7BEF);display.drawString("AVAILABLE BALANCE",120,top,captionFont);
  if(!scaled||!domesticScaledText(amount,7,sourceW,48,left,valueY,numberW,40,0xFFDF,true)) {
    display.setTextDatum(TL_DATUM);display.setTextColor(0xFFDF);display.drawString(amount,left,valueY,font);display.drawString(amount,left+1,valueY,font);
  }
  if(currency.length()) {
    int baseline=pgm_read_byte(&fontdata[font].baseline),unitBaseline=pgm_read_byte(&fontdata[4].baseline);
    if(scaled)baseline=(baseline*40+display.fontHeight(7)/2)/display.fontHeight(7);
    unitBaseline=(unitBaseline*20+display.fontHeight(4)/2)/display.fontHeight(4);
    if(!domesticScaledText(currency,4,sourceUnit,26,left+numberW+gap,valueY+baseline-unitBaseline,unitW,20,TFT_GREEN,false)) {
      display.setTextDatum(TL_DATUM);display.setTextColor(TFT_GREEN);display.drawString(currency,left+numberW+gap,valueY,2);
    }
  }
  display.setTextDatum(TL_DATUM);
}

void drawDomestic() {
  const DomesticQuotaState* value = &alibabaQuota;
  const char* name = "QWEN";
  if (effectiveDisplayMode == DisplayMode::DomesticKimi) { value = &kimiQuota; name = "KIMI"; }
  if (effectiveDisplayMode == DisplayMode::DomesticMinimax) { value = &miniMaxQuota; name = "MINIMAX"; }
  if (effectiveDisplayMode == DisplayMode::DomesticDeepseek) { value = &deepSeekQuota; name = "DEEPSEEK"; }
  const bool isZhipu = effectiveDisplayMode == DisplayMode::DomesticZhipu;
  if (isZhipu) { value = &zhipuQuota; name = "GLM"; }
  const auto& q = *value;
  String key=String((int)effectiveDisplayMode)+"|"+q.plan+"|"+q.currency+"|"+String(q.planAvailable)+"|"+String(q.planPercent,3)
    +"|"+q.planReset+"|"+String(q.primaryAvailable)+"|"+String(q.weeklyAvailable)+"|"+String(q.balanceAvailable)+"|"+String(q.usedCostAvailable)
    +"|"+String(q.primaryPercent,3)+"|"+String(q.weeklyPercent,3)+"|"+String(q.balance,2)+"|"+String(q.usedCost,2)
    +"|"+resetClock(q.primaryReset)+"|"+resetClock(q.weeklyReset)+"|"+String(domesticTokensToday)+"|"+String(utcOffsetSeconds);
  if(!pageContentChanged(3,key)){screenDirty=false;return;}
  display.fillScreen(TFT_BLACK);
  bool windowed=effectiveDisplayMode==DisplayMode::DomesticKimi||q.primaryAvailable||q.weeklyAvailable;
  bool percentAvailable=q.weeklyAvailable||q.planAvailable;
  float percent=q.weeklyAvailable?q.weeklyPercent:q.planPercent;
  drawPercentageRing(q.balanceAvailable ? 0 : percentAvailable ? percent : 0);
  display.setTextDatum(TL_DATUM);
  uint16_t headingColor=isZhipu?0x8CFF:TFT_GREEN;
  display.fillCircle(25,30,4,headingColor);
  display.setTextColor(headingColor);display.drawString(name,36,24,2);display.drawString(name,37,24,2);
  String membership=domesticMembership(q.plan,q.balanceAvailable);
  if(membership.length()) {
    while(display.textWidth(membership,2)>100&&membership.length()>1)membership.remove(membership.length()-1);
    int badgeW=constrain(display.textWidth(membership,2)+12,34,112),x=218-badgeW;
    display.setTextDatum(MC_DATUM);display.setTextColor(TFT_ORANGE);display.drawString(membership,x+badgeW/2,31,2);display.drawRoundRect(x,22,badgeW,18,5,TFT_ORANGE);
  }
  display.drawFastHLine(20,53,200,0x7BEF);
  if(isZhipu&&!q.balanceAvailable) {
    drawCentered("AVAILABLE BALANCE",73,1,0x7BEF);
    drawCentered("--",100,4,0xFFDF);
    drawCentered("Authorize in bridge",188,2,headingColor);
    screenDirty=false;return;
  }
  if(!q.balanceAvailable)drawCentered(windowed?"WEEKLY":"PLAN",73,1,0x7BEF);
  String number = q.balanceAvailable ? String(q.balance, 2) : percentAvailable ? String(static_cast<int>(constrain(percent,0,100))) : "--";
  String suffix = q.balanceAvailable ? q.currency : percentAvailable ? "%" : "";
  if(q.balanceAvailable)drawDomesticBalance(number,suffix);
  else {
  int numberFont=number.length()<=3?7:number.length()<=6?4:2,unitFont=numberFont==7?4:2;
  int width = display.textWidth(number, numberFont) + display.textWidth(suffix, unitFont) + (suffix.length()?4:0);
  int left = (240 - width) / 2;
  display.setTextDatum(TL_DATUM);
  int y=numberFont==7?90:numberFont==4?101:108,unitY=numberFont==7?105:108;
  display.setTextColor(0xFFDF);display.drawString(number,left,y,numberFont);display.drawString(number,left+1,y,numberFont);
  display.setTextColor(TFT_GREEN);display.drawString(suffix,left+display.textWidth(number,numberFont)+4,unitY,unitFont);
  }
  display.setTextDatum(TL_DATUM);
  if(!q.balanceAvailable) {
    display.setTextColor(0x7BEF,TFT_BLACK);display.drawString(windowed?"RESET":"REMAINING",37,153,1);
    display.setTextColor(TFT_GREEN,TFT_BLACK);
    display.drawRightString(windowed?resetClock(q.weeklyReset):q.planAvailable?String(floorf(max(0.0f,100-q.planPercent)*100)/100,2)+"% LEFT":"QUOTA UNKNOWN",203,151,2);
  }
  display.fillRoundRect(20,177,200,38,8,0x1082);display.drawRoundRect(20,177,200,38,8,0x29A5);
  display.setTextDatum(MC_DATUM);display.setTextColor(TFT_GREEN);
  display.drawString(q.balanceAvailable?"USED":windowed?"5H":"RESET",53,196,2);
  if(q.balanceAvailable) {
    if(q.usedCostAvailable) {
      String amount=String(q.usedCost,2);int w=display.textWidth(amount,2),cw=display.textWidth(q.currency,2),gap=q.currency.length()?5:0,x=153-(w+gap+cw)/2;
      display.setTextDatum(ML_DATUM);display.setTextColor(0xFFDF);display.drawString(amount,x,196,2);display.drawString(amount,x+1,196,2);
      display.setTextColor(TFT_GREEN);display.drawString(q.currency,x+w+gap,196,2);display.drawString(q.currency,x+w+gap+1,196,2);
    } else {display.setTextColor(0xFFDF);display.drawString("--",153,196,2);}
  }
  else if(windowed) {
    display.setTextColor(TFT_WHITE);display.drawString(q.primaryAvailable?String(static_cast<int>(constrain(q.primaryPercent,0,100)))+"%":"--",120,196,2);
    display.setTextColor(TFT_CYAN);display.drawString(resetClock(q.primaryReset),187,196,2);
  } else {
    String reset;int64_t epoch=quotaResetEpoch(q.planReset.c_str());
    if(epoch>0) {time_t local=static_cast<time_t>(epoch+utcOffsetSeconds);struct tm parts;gmtime_r(&local,&parts);char text[20];strftime(text,sizeof(text),"%m-%d %H:%M",&parts);reset=text;}
    display.setTextColor(TFT_CYAN);display.drawString(reset,154,196,2);
  }
  display.setTextDatum(TL_DATUM);
  display.setTextColor(TFT_DARKGREY,TFT_BLACK);
  String provider = effectiveDisplayMode == DisplayMode::DomesticKimi ? "kimi" : effectiveDisplayMode == DisplayMode::DomesticMinimax ? "minimax" : effectiveDisplayMode == DisplayMode::DomesticDeepseek ? "deepseek" : "alibaba";
  screenDirty = false;
}


void drawSystem() {
  static uint32_t generation=UINT32_MAX,lastTick=0;
  static String lastDown,lastUp,lastCpu,lastMem,lastScale;
  bool chrome=generation!=visualGeneration;
  if(!chrome && millis()-lastTick<250)return;
  lastTick=millis();
  if(chrome){++systemChromeDraws;generation=visualGeneration;display.fillScreen(TFT_BLACK);lastDown=lastUp=lastCpu=lastMem=lastScale="";}
  if (!systemMetrics.available) {
    drawCentered("Waiting for data", 110, 2, TFT_DARKGREY);
    screenDirty = false;
    return;
  }
  auto compactRate = [](double value) -> String {
    return value >= 1000000 ? String(value / 1000000, 1) + "M" : value >= 1000 ? String(value / 1000, 0) + "K" : String(value, 0) + "B";
  };
  display.setTextDatum(TL_DATUM);
  display.setTextColor(TFT_DARKGREY, TFT_BLACK);
  if(chrome){
    display.drawString("DOWN",14,10,1);display.drawString("UP",134,10,1);
    display.drawString("CPU",28,198,2);display.drawString("MEM",130,198,2);
    drawCentered("SYSTEM MONITOR",226,1,TFT_DARKGREY);
  }
  auto number=[&](String value,String& prior,int x,int y,int w,uint16_t color){
    if(value==prior)return;prior=value;
    // Render the complete replacement offscreen, then push once: no blank phase
    // and no leftover digit when 100% becomes 9%.
    TFT_eSprite cell(&display);cell.setColorDepth(16);
    if(!cell.createSprite(w,28)){prior="";return;}
    cell.fillSprite(TFT_BLACK);cell.setTextColor(color,TFT_BLACK);cell.setTextDatum(TL_DATUM);
    cell.drawString(value,0,0,4);cell.pushSprite(x,y);cell.deleteSprite();++systemNumberDraws;
  };
  number(compactRate(systemMetrics.downloadBytesPerSecond)+"/s",lastDown,12,20,116,TFT_GREEN);
  number(compactRate(systemMetrics.uploadBytesPerSecond)+"/s",lastUp,132,20,108,TFT_YELLOW);
  number(String(systemMetrics.cpuPercent,0)+"%",lastCpu,62,192,64,TFT_WHITE);
  number(String(systemMetrics.memoryPercent,0)+"%",lastMem,164,192,64,TFT_WHITE);
  bool chartChanged=chrome||netQueueCount>0;
  int steps=netQueueCount>16?3:1;
  while(steps-->0&&netQueueCount>0){
    for(int i=0;i<223;i++){netUp[i]=netUp[i+1];netDown[i]=netDown[i+1];}
    netUp[223]=netQueueUp[netQueueHead];netDown[223]=netQueueDown[netQueueHead];
    netQueueHead=(netQueueHead+1)%32;--netQueueCount;
    ++systemSamplesConsumed;
  }
  if(!chartChanged){screenDirty=false;return;}
  ++systemChartFrames;
  double peak = 1000;
  for (int i = 0; i < 224; ++i) peak = max(peak, static_cast<double>(max(netUp[i], netDown[i])));
  double scale = max(10240.0, peak + floor(peak / 7));
  display.setTextColor(TFT_DARKGREY, TFT_BLACK);
  String scaleText=compactRate(scale);
  if(lastScale!=scaleText){lastScale=scaleText;display.fillRect(120,48,112,10,TFT_BLACK);display.drawRightString(scaleText,232,48,1);}
  static uint8_t downTop[224],downBottom[224],upTop[224],upBottom[224];
  int previousDown = 187, previousUp = 187;
  for (int i = 0; i < 224; ++i) {
    int lo = max(0, i - 1), hi = min(223, i + 1);
    double down = (static_cast<double>(netDown[lo]) + netDown[i] + netDown[hi]) / 3;
    double up = (static_cast<double>(netUp[lo]) + netUp[i] + netUp[hi]) / 3;
    int dy = 187 - static_cast<int>(min(1.0, down / scale) * 126);
    int uy = 187 - static_cast<int>(min(1.0, up / scale) * 126);
    int dlTop=min(dy,i==0?dy:previousDown),dlBottom=min(187,max(dy,i==0?dy:previousDown)+4);
    int ulTop=min(uy,i==0?uy:previousUp),ulBottom=min(187,max(uy,i==0?uy:previousUp)+4);
    downTop[i]=dlTop;downBottom[i]=dlBottom;upTop[i]=ulTop;upBottom[i]=ulBottom;
    previousDown = dy; previousUp = uy;
  }
  uint16_t pixels[224];
  for(int y=60;y<188;y++) {
    for(int x=0;x<224;x++) {
      uint16_t color=(y==92||y==124||y==156)?0x2945:TFT_BLACK;
      if(y>downBottom[x])color=0x02A0;
      else if(y>=downTop[x])color=TFT_GREEN;
      if(upTop[x]<187&&y>=upTop[x]&&y<=upBottom[x])color=TFT_YELLOW;
      pixels[x]=color;
    }
    display.pushImage(8,y,224,1,pixels);
    if((y&31)==31)yield();
  }
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

bool drawRgb565FileRegion(const char* path, int sourceWidth, int sourceHeight,
                          int sourceY, int x, int y, int width, int height) {
  File file = LittleFS.open(path, "r");
  bool dimensionsValid = sourceWidth > 0 && sourceWidth <= 232 && sourceHeight > 0 &&
      sourceY >= 0 && sourceY + height <= sourceHeight && width > 0 && width <= sourceWidth &&
      file && file.size() == static_cast<size_t>(sourceWidth * sourceHeight * 2);
  if (!dimensionsValid) {
    if (file) file.close();
    return false;
  }
  uint16_t row[232];
  for (int line = 0; line < height; line++) {
    uint32_t offset = static_cast<uint32_t>((sourceY + line) * sourceWidth * 2);
    if (!file.seek(offset, SeekSet) ||
        file.read(reinterpret_cast<uint8_t*>(row), width * 2) != width * 2) {
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
  bool chromeChanged=pageContentChanged(4,music.title+"|"+music.artist+"|"+String(music.available)+"|"+String(music.hasArtwork));
  bool progressChanged=pageContentChanged(5,String(music.elapsedSeconds,0)+"|"+String(music.durationSeconds,0)+"|"+String(music.playing));
  if(!chromeChanged&&!progressChanged){screenDirty=false;return;}
  if(chromeChanged) {
  display.fillScreen(TFT_BLACK);
  bool hasCover = false;
  File cover = LittleFS.open("/cover.rgb565", "r");
  if (music.available && music.hasArtwork && cover && cover.size() == 112 * 112 * 2) {
    uint16_t source[112], row[128]; hasCover = true;
    for (int y=0; y<128; ++y) {
      if (!cover.seek((y*112/128)*112*2, SeekSet) || cover.read(reinterpret_cast<uint8_t*>(source),224)!=224) {hasCover=false;break;}
      for (int x=0;x<128;++x) row[x]=source[x*112/128];
      display.pushImage(56,14+y,128,1,row);
    }
  }
  cover.close();
  if (!hasCover) {
    display.fillRect(56, 14, 128, 128, TFT_DARKGREY);
    display.setTextDatum(MC_DATUM);
    display.setTextColor(TFT_LIGHTGREY,TFT_DARKGREY);
    display.drawString("No Art",120,78,2);
  }
  bool hasText = music.available && drawRgb565File("/text.rgb565", 4, 150, 232, 44);
  if (!hasText) {
    drawCentered(music.title.length() ? music.title.substring(0, 25) : "No Music", 154, 2, TFT_WHITE);
    drawCentered(music.artist.substring(0, 28), 174, 2, TFT_LIGHTGREY);
  }
  }
  float ratio = music.durationSeconds > 0
      ? constrain(music.elapsedSeconds / music.durationSeconds, 0.0f, 1.0f) : 0;
  display.fillRect(20, 204, 200, 8, TFT_DARKGREY);
  display.fillRect(20, 204, static_cast<int>(200 * ratio), 8, music.playing ? TFT_GREEN : TFT_LIGHTGREY);
  display.setTextDatum(TC_DATUM);
  display.setTextColor(TFT_LIGHTGREY, TFT_BLACK);
  display.fillRect(0,216,240,14,TFT_BLACK);
  display.drawString(durationText(music.elapsedSeconds) + " / " + durationText(music.durationSeconds), 120, 220, 1);
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
  static uint32_t petGeneration=UINT32_MAX;
  static String previousState;
  static uint32_t lastCreditCycle = 0;
  static String lastCreditKey;
  static bool hadCreditFooter = false;
  const uint32_t creditCycle = currentEpochUtc() / 4;
  const bool hasCreditFooter = codexQuota.resetCredits >= 0 || !codexQuota.resetExpirations.empty();
  String creditKey = String(codexQuota.resetCredits) + ":" + String(codexQuota.stale) + ":" + String(utcOffsetSeconds);
  for (uint32_t epoch : codexQuota.resetExpirations) creditKey += ":" + String(epoch);
  if (codexQuota.resetExpirations.size() +
      (codexQuota.resetCredits > static_cast<int>(codexQuota.resetExpirations.size()) ? 1 : 0) > 2)
    creditKey += ":" + String(creditCycle);
  bool working = codexState == "working" || claudeState == "working";
  String state=codexState+"|"+claudeState+"|"+String(hasCreditFooter);
  bool chrome=petGeneration!=visualGeneration || previousState!=state;
  bool repaintFooter=chrome||creditKey!=lastCreditKey;
  petGeneration=visualGeneration;previousState=state;
  int frame=working?static_cast<int>(millis()/120):0;
  bool fallbackChanged=chrome||lastPetFrame!=frame;
  lastCreditCycle = creditCycle;
  lastPetFrame = frame;
  bool step = (frame & 1) != 0;
  int x = 91;
  if (working) {
    int travel = frame % 56;
    if (travel > 28) travel = 56 - travel;
    x = 16 + travel * 5;
  }

  if(chrome){display.fillScreen(TFT_BLACK);drawCentered("BYTE SPROUT",12,2,working?TFT_GREEN:TFT_CYAN);}
  bool externalPet = drawPetAnimation(64,49,chrome,working,claudeState=="working"&&codexState!="working");
  if(!externalPet&&fallbackChanged){display.fillRect(0,49,240,120,TFT_BLACK);if(!drawRgb565File("/pet.asset",64,49,112,112))drawPixelPetBody(x,54,step,working);}
  if(chrome)drawCentered(working?"WORKING":"IDLE",180,2,working?TFT_GREEN:TFT_YELLOW);
  String owner = codexState == "working" && claudeState == "working" ? "CODEX + CLAUDE"
      : codexState == "working" ? "CODEX" : claudeState == "working" ? "CLAUDE" : "READY";
  if (hasCreditFooter) {
    if(chrome)drawCentered(owner, 32, 1, TFT_LIGHTGREY);
    if (repaintFooter) {
      display.fillRect(0, 198, 240, 42, TFT_BLACK);
      drawResetCredits(200);
    }
  } else if(chrome)drawCentered(owner, 207, 2, TFT_LIGHTGREY);
  hadCreditFooter = hasCreditFooter;
  lastCreditKey = creditKey;
  screenDirty = false;
}

void drawOffline() {
  static uint32_t generation=UINT32_MAX;
  static String previousMinute;
  lastPetFrame = -1; // A full offline redraw invalidates the preserved pet footer.
  int second = static_cast<int>(currentEpochUtc() % 60);
  bool chrome=!showingOffline||generation!=visualGeneration;
  if (!chrome && second == lastClockSecond) return;
  generation=visualGeneration;
  lastClockSecond = second;
  if(chrome) {
    previousMinute="";display.fillScreen(TFT_BLACK);
    drawCentered("AI-bot",40,4,TFT_WHITE);
    drawCentered("Waiting for bridge",176,2,0x9492);
    display.setTextDatum(TL_DATUM);display.setTextColor(TFT_ORANGE,TFT_BLACK);
    display.drawRightString("PC OFF",224,208,2);
  }
  String time=clockText(),minute=time.substring(0,5);
  display.setTextDatum(TL_DATUM);
  if(previousMinute!=minute) {
    String hour=time.substring(0,2),minutes=time.substring(3,5);
    int hourW=display.textWidth(hour,7),colonW=display.textWidth(":",7),minuteW=display.textWidth(minutes,7);
    int left=(240-hourW-colonW-minuteW)/2;
    // Small independent cells keep peak heap use below a full-screen sprite.
    auto digitCell=[&](const String& value,int x,int width,uint16_t color){
      TFT_eSprite cell(&display);cell.setColorDepth(16);
      if(!cell.createSprite(width,display.fontHeight(7)))return false;
      cell.fillSprite(TFT_BLACK);cell.setTextColor(color,TFT_BLACK);cell.drawString(value,0,0,7);cell.pushSprite(x,92);cell.deleteSprite();return true;
    };
    bool ok=digitCell(hour,left,hourW,TFT_CYAN);
    ok=digitCell(":",left+hourW,colonW,TFT_YELLOW)&&ok;
    ok=digitCell(minutes,left+hourW+colonW,minuteW,TFT_CYAN)&&ok;
    if(ok)previousMinute=minute;
  }
  showingOffline = true;
}

void drawWeekdayStrokeGlyph(int day, int x, int y, uint16_t color) {
  // Original geometric strokes on a 24px grid: 周, 日, 一, 二, 三, 四, 五, 六.
  auto horizontal = [&](int left, int top, int width) { display.fillRect(x + left, y + top, width, 2, color); };
  auto vertical = [&](int left, int top, int height) { display.fillRect(x + left, y + top, 2, height, color); };
  auto box = [&](int left, int top, int width, int height) {
    horizontal(left, top, width); horizontal(left, top + height - 2, width);
    vertical(left, top, height); vertical(left + width - 2, top, height);
  };
  if (day == -1) {
    horizontal(2, 1, 20); vertical(2, 1, 22); vertical(20, 1, 22); horizontal(17, 21, 5);
    horizontal(6, 6, 12); vertical(11, 4, 8); horizontal(5, 11, 14); box(7, 15, 10, 6);
  } else if (day == 0) { box(4, 1, 16, 22); horizontal(4, 11, 16); }
  else if (day == 1) horizontal(2, 12, 20);
  else if (day == 2) { horizontal(4, 6, 16); horizontal(2, 19, 20); }
  else if (day == 3) { horizontal(3, 3, 18); horizontal(5, 11, 14); horizontal(2, 20, 20); }
  else if (day == 4) {
    box(2, 4, 20, 18); vertical(8, 4, 9); vertical(14, 4, 11); horizontal(14, 13, 5);
    display.drawLine(x + 8, y + 12, x + 5, y + 16, color);
  } else if (day == 5) {
    horizontal(3, 2, 18); vertical(9, 2, 20); horizontal(4, 10, 14);
    vertical(16, 10, 12); horizontal(1, 21, 22);
  } else {
    horizontal(10, 2, 4); horizontal(12, 4, 4); horizontal(2, 8, 20);
    display.fillTriangle(x + 8, y + 12, x + 11, y + 14, x + 3, y + 22, color);
    display.fillTriangle(x + 14, y + 12, x + 12, y + 15, x + 21, y + 22, color);
  }
}

void drawScreenSaver() {
  static uint32_t lastTick = 0;
  static bool lastOnline = false;
  const uint32_t utc = currentEpochUtc();
  const uint32_t tick = utc / 5;
  const bool online = bridgeFresh();
  if (!screenDirty && tick == lastTick && online == lastOnline) return;
  lastTick = tick;
  lastOnline = online;
  time_t local = static_cast<time_t>(static_cast<int64_t>(utc) + utcOffsetSeconds);
  struct tm parts;
  gmtime_r(&local, &parts);
  const int x = 6 + ScreenSaverGeometry::bounce(tick, 2, 24);
  const int y = 12 + ScreenSaverGeometry::bounce(tick, 1, 90);
  display.fillScreen(TFT_BLACK);
  display.setTextDatum(TL_DATUM);
  const int digits[] = {parts.tm_hour / 10, parts.tm_hour % 10, parts.tm_min / 10, parts.tm_min % 10};
  const int offsets[] = {0, 47, 115, 162};
  for (int cell = 0; cell < 4; ++cell)
    for (const auto& segment : ScreenSaverGeometry::Segments)
      if (strchr(segment.digits, '0' + digits[cell]))
        display.fillRoundRect(x + offsets[cell] + segment.x, y + segment.y,
                              segment.width, segment.height, 3, TFT_CYAN);
  display.fillCircle(x + 102, y + 26, 5, TFT_YELLOW);
  display.fillCircle(x + 102, y + 50, 5, TFT_YELLOW);
  char date[6];
  snprintf(date, sizeof(date), "%02d-%02d", parts.tm_mon + 1, parts.tm_mday);
  const int dateWidth = display.textWidth(date, 4);
  const int visibleCenter = x + (ScreenSaverGeometry::Width + (digits[0] == 1 ? 33 : 0)) / 2;
  const int dateX = visibleCenter - (dateWidth + 10 + 50) / 2;
  display.setTextColor(0xC618, TFT_BLACK);
  display.drawString(date, dateX, y + ScreenSaverGeometry::CalendarY, 4);
  drawWeekdayStrokeGlyph(-1, dateX + dateWidth + 10, y + ScreenSaverGeometry::CalendarY, 0xC618);
  drawWeekdayStrokeGlyph(parts.tm_wday, dateX + dateWidth + 36, y + ScreenSaverGeometry::CalendarY, TFT_YELLOW);
  if (!online) {
    display.drawRoundRect(181, 219, 56, 18, 3, TFT_ORANGE);
    display.setTextDatum(MC_DATUM);
    display.setTextColor(TFT_ORANGE, TFT_BLACK);
    display.drawString("PC OFF", 209, 228, 1);
  }
  display.setTextDatum(TL_DATUM);
  screenDirty = false;
}

void sendControl(const char* type) {
  String ip = WiFi.status() == WL_CONNECTED ? WiFi.localIP().toString() : "";
  Serial.print(kPrefix);
  Serial.print("{\"version\":1,\"type\":\"");
  Serial.print(type);
  Serial.print("\",\"device\":\"esp8266\",\"ip\":\"");
  Serial.print(ip);
  Serial.println("\"}");
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
  if (kind == 4) return "/weather-text.rgb565";
  if (kind == 5) return "/stock-names.rgb565";
  if (kind == 6) return "/weather-header.rgb565";
  if (kind == 7) return "/weather-date.rgb565";
  if (kind == 8) return "/weather-air.rgb565";
  if (kind == 9) return "/pet.apet";
  if (kind == 10) return "/claude.apet";
  if (kind == 11) return "/codex.apet";
  if (kind == 12) return "/claude-logo.rgb565";
  if (kind == 13) return "/codex-logo.rgb565";
  return nullptr;
}

void abandonResourceTransfer() {
  if (resourceTransfer.file) resourceTransfer.file.close();
  LittleFS.remove("/resource.new");
  resourceTransfer = ResourceTransfer{};
}

bool commitResourceTransfer() {
  if(resourceTransfer.kind==12||resourceTransfer.kind==13) {
    File candidate=LittleFS.open("/resource.new","r");if(!candidate||candidate.size()!=3200)return false;
  }
  if (resourceTransfer.kind >= 9 && resourceTransfer.kind <= 11) {
    File candidate = LittleFS.open("/resource.new", "r");
    uint8_t count; uint16_t delays[8], width, height;
    if (!readPetHeader(candidate, count, delays, width, height)) return false;
  }
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
  if (valid) { screenDirty = true; visualGeneration++; }
  resourceTransfer.active = false;
  lastCompletedTransferId = transferId;
  lastCompletedSequence = sequence;
  lastCompletedOk = valid;
  sendResourceAck(transferId, sequence, valid);
}

String displayModeName();

void fillDeviceInfo(JsonObject response) {
  response["device"] = "AI-bot";
  response["version"] = 1;
  response["ip"] = WiFi.status() == WL_CONNECTED ? WiFi.localIP().toString() : "";
  response["usb_active"] = usbFresh();
  response["bridge_online"] = bridgeFresh();
  response["mode"] = displayModeName();
  response["brightness"] = brightness;
  response["uptime_ms"] = millis();
  response["usb_status_count"] = usbStatusCount;
  response["lan_status_count"] = lanStatusCount;
  JsonObject pages = response["page_data"].to<JsonObject>();
  pages["offline_clock_width"]=display.textWidth("88:88",7);
  pages["offline_clock_height"]=display.fontHeight(7);
  pages["activity_chrome_draws"]=activityChromeDraws;
  pages["activity_clock_draws"]=activityClockDraws;
  pages["activity_data_draws"]=activityDataDraws;
  pages["system_chart_frames"]=systemChartFrames;
  pages["system_chrome_draws"]=systemChromeDraws;
  pages["system_number_draws"]=systemNumberDraws;
  pages["system_samples_consumed"]=systemSamplesConsumed;
  pages["system_queue_depth"]=netQueueCount;
  pages["weather"] = weather.available;
  pages["temperature"] = weather.temperature;
  pages["stock_count"] = stockCount;
  pages["claude"] = claudeQuota.available;
  pages["codex"] = codexQuota.available;
  pages["alibaba"] = alibabaQuota.available;
  pages["kimi"] = kimiQuota.available;
  pages["minimax"] = miniMaxQuota.available;
  pages["deepseek"] = deepSeekQuota.available;
  pages["zhipu"] = zhipuQuota.available;
  if(zhipuQuota.balanceAvailable) {
    pages["zhipu_balance"] = zhipuQuota.balance;
    pages["zhipu_currency"] = zhipuQuota.currency;
  }
  pages["system"] = systemMetrics.available;
  pages["cpu_percent"] = systemMetrics.cpuPercent;
  pages["music"] = music.available;
}

void handleFrame(const String& line) {
  if (!line.startsWith(kPrefix)) return;

  JsonDocument document;
  DeserializationError error = deserializeJson(document, line.c_str() + strlen(kPrefix));
  if (error || document["version"].as<int>() != 1) return;

  const char* type = document["type"] | "";
  if (strcmp(type, "device_info_request") == 0 || strcmp(type, "reset_wifi") == 0) {
    if (!document["request_id"].is<uint32_t>() || document["request_id"].as<uint32_t>() == 0) return;
    bool reset = strcmp(type, "reset_wifi") == 0;
    bool confirmed = document["confirm"].is<bool>() && document["confirm"].as<bool>();
    JsonDocument response;
    response["version"] = 1;
    response["type"] = reset ? "reset_wifi_ack" : "device_info";
    response["request_id"] = document["request_id"].as<uint32_t>();
    response["ok"] = !reset || confirmed;
    if (!reset) fillDeviceInfo(response["data"].to<JsonObject>());
    Serial.print(kPrefix);
    serializeJson(response, Serial);
    Serial.println();
    if (reset && confirmed) {
      Serial.flush();
      delay(100);
      wifiManager.resetSettings();
      ESP.restart();
    }
    return;  // Diagnostic requests never refresh USB status freshness.
  }
  if (strcmp(type, "ping") == 0) {
    sendControl("pong");
    return;
  }

  if (strcmp(type, "status") == 0 && document["data"].is<JsonObject>()) {
    updateStatus(document["data"].as<JsonObjectConst>());
    lastUsbStatusAt = millis();
    lastBridgeStatusAt = millis();
    usbStatusCount++;
    return;
  }

  if (strcmp(type, "metrics") == 0 && document["data"].is<JsonObject>() && usbFresh()) {
    updateMetrics(document["data"].as<JsonObjectConst>());
    return; // Metrics alone never extend USB freshness or suppress Wi-Fi fallback.
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
    displayMode = parseDisplayMode(mode);
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

void pollLanResources() {
  if (usbFresh() || resourceTransfer.active) return;
  WiFiClient client; HTTPClient http;
  String base = "http://" + bridge.host + ":" + String(bridge.port);
  if (!http.begin(client, base + "/resources")) return;
  http.setTimeout(1200); http.addHeader("X-AIBot-Token", bridge.token);
  if (http.GET() != HTTP_CODE_OK) { http.end(); return; }
  JsonDocument catalog;
  if (deserializeJson(catalog, http.getStream()) || catalog["version"].as<int>() != 1) { http.end(); return; }
  http.end();
  for (JsonObjectConst entry : catalog["resources"].as<JsonArrayConst>()) {
    int kind = entry["kind"] | 0; uint32_t crc = entry["crc"] | 0U, length = entry["length"] | 0U;
    if (kind < 1 || kind > 13 || !resourcePath(kind) || length == 0 || length > 1048576U) continue;
    // Verify already persisted pixels after restart or USB transfer before downloading.
    File existing = LittleFS.open(resourcePath(kind), "r");
    if (existing && existing.size() == length) {
      uint8_t bytes[512]; uint32_t check = 0xFFFFFFFF; int count;
      while ((count = existing.read(bytes, sizeof(bytes))) > 0) { check = updateCrc32(check, bytes, count); yield(); }
      if (~check == crc) { existing.close(); continue; }
    }
    existing.close();
    if (!http.begin(client, base + "/resources/" + String(kind) + "/" + String(crc))) return;
    http.setTimeout(1200); http.addHeader("X-AIBot-Token", bridge.token);
    if (http.GET() != HTTP_CODE_OK || http.getSize() != static_cast<int>(length)) { http.end(); return; }
    File candidate = LittleFS.open("/lan-resource.new", "w");
    if (!candidate) { http.end(); return; }
    uint32_t received = 0, check = 0xFFFFFFFF, started = millis(); uint8_t bytes[512];
    auto* stream = http.getStreamPtr();
    while (received < length && millis() - started < 6000 && !usbFresh()) {
      readSerial();
      if (usbFresh()) break;
      int available = stream->available();
      if (available <= 0) { if (!http.connected()) break; delay(1); continue; }
      int count = stream->read(bytes, min(size_t(available), min(sizeof(bytes), size_t(length - received))));
      if (count <= 0 || candidate.write(bytes, count) != static_cast<size_t>(count)) break;
      check = updateCrc32(check, bytes, count); received += count; yield();
    }
    candidate.close(); http.end();
    bool valid = received == length && ~check == crc && !usbFresh() && !resourceTransfer.active;
    if (valid) {
      LittleFS.remove("/resource.new");
      valid = LittleFS.rename("/lan-resource.new", "/resource.new");
      if (valid) { resourceTransfer.kind = kind; valid = commitResourceTransfer(); }
    }
    if (valid) { screenDirty = true; visualGeneration++; }
    LittleFS.remove("/lan-resource.new");
    // One changed resource per successful status poll; old content remains on failure.
    return;
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
  int status = http.GET(); bool updated = false;
  if (status == HTTP_CODE_OK) {
    JsonDocument document;
    if (!deserializeJson(document, http.getStream()) && document["version"].as<int>() == 1) {
      updateStatus(document.as<JsonObjectConst>());
      lastBridgeStatusAt = millis();
      lanStatusCount++;
      updated = true;
    }
  }
  http.end();
  if (updated) pollLanResources();
}

bool authorizeAdmin() {
  if (validBridgeConfig(bridge) && admin.header("X-AIBot-Token") == bridge.token) return true;
  admin.send(401, "application/json", "{\"error\":\"unauthorized\"}");
  return false;
}

String displayModeName() {
  for (const auto& entry : modeNames) if (displayMode == entry.mode) return entry.name;
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
    fillDeviceInfo(response.to<JsonObject>());
    String body;
    serializeJson(response, body);
    admin.send(200, "application/json", body);
  });
  admin.on("/api/display", HTTP_POST, [] {
    if (!authorizeAdmin()) return;
    String mode = admin.arg("mode");
    displayMode = parseDisplayMode(mode);
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
  DisplayMode mode = displayMode;
  if (mode != DisplayMode::ScreenSaver) {
    if (domesticNeedsInput) mode = parseDisplayMode("domestic_" + domesticActivityProvider);
    else if (codexNeedsInput && claudeNeedsInput && bridgeFollowApp.length()) mode = parseDisplayMode(bridgeFollowApp);
    else if (codexNeedsInput) mode = DisplayMode::Codex;
    else if (claudeNeedsInput) mode = DisplayMode::Claude;
    else if (completionActive) mode = DisplayMode::Codex;
  }
  if (mode == DisplayMode::Auto) {
    if (cycleEnabled && cycleCount > 0) mode = cyclePages[(max((int64_t)0, (int64_t)currentEpochUtc() - cycleStartedAt) / cycleInterval) % cycleCount];
    else if (music.available && music.playing) mode = DisplayMode::Music;
    else if (domesticActivityState == "working") mode = parseDisplayMode("domestic_" + domesticActivityProvider);
    else if (bridgeFollowApp.length()) mode = parseDisplayMode(bridgeFollowApp);
    else {
      bool cw = codexState == "working", aw = claudeState == "working";
      if (cw != aw) mode = cw ? DisplayMode::Codex : DisplayMode::Claude;
      else mode = (max((int64_t)0, (int64_t)currentEpochUtc() - cycleStartedAt) / (cw ? 2 : 6)) % 2 == 0
        ? DisplayMode::Claude : DisplayMode::Codex;
    }
  }
  if (mode != effectiveDisplayMode) { effectiveDisplayMode = mode; screenDirty = true; lastPetFrame = -1; }
  if (mode == DisplayMode::ScreenSaver) return RenderPage::ScreenSaver;
  if (mode == DisplayMode::Weather) return RenderPage::Weather;
  if (mode == DisplayMode::Stocks) return RenderPage::Stocks;
  if (mode == DisplayMode::Quotas || mode == DisplayMode::Dual) return RenderPage::Quotas;
  if (mode == DisplayMode::Claude) return RenderPage::Claude;
  if (mode == DisplayMode::Codex) return RenderPage::Codex;
  if (mode == DisplayMode::Domestic || mode == DisplayMode::DomesticAlibaba ||
      mode == DisplayMode::DomesticKimi || mode == DisplayMode::DomesticMinimax || mode == DisplayMode::DomesticDeepseek || mode == DisplayMode::DomesticZhipu) return RenderPage::Domestic;
  if (mode == DisplayMode::System) return RenderPage::System;
  if (mode == DisplayMode::Music) return RenderPage::Music;
  if (mode == DisplayMode::Pet) return RenderPage::Pet;
  return RenderPage::Dashboard;
}

void drawSignalRing(RenderPage page) {
  static bool previouslyShown = false;
  static RenderPage previousPage = RenderPage::Dashboard;
  if (previousPage != page) { previouslyShown = false; previousPage = page; }
  bool waiting = page == RenderPage::Claude ? claudeNeedsInput : page == RenderPage::Codex ? codexNeedsInput : page == RenderPage::Domestic ? domesticNeedsInput : false;
  uint16_t color = TFT_BLACK;
  bool show = waiting && millis()%800<400;
  if (show) color = TFT_RED;
  uint32_t phase = (millis()-completionStarted)/70;
  if (!waiting && page == RenderPage::Codex && completionActive && phase < 50) {
    static const uint8_t levels[10] = {40,88,144,208,255,255,208,144,88,0};
    color = display.color565(0,levels[phase%10],0); show = true;
  }
  if (show) {
    display.fillRect(4,4,232,10,color); display.fillRect(226,4,10,232,color);
    display.fillRect(4,226,232,10,color); display.fillRect(4,4,10,232,color);
  }
  else if (previouslyShown) drawPercentageRing(lastRingPercent);
  previouslyShown = show;
}

void renderCurrentPage() {
  static uint32_t lastQuotaSecond = 0;
  static bool wasFresh = false;
  bool fresh = bridgeFresh();
  if(fresh != wasFresh){visualGeneration++;wasFresh=fresh;}
  RenderPage page = desiredPage();
  if (page != lastRenderedPage) {
    visualGeneration++;
    lastRenderedPage = page;
    lastPetFrame = -1;
    screenDirty = true;
  }
  const bool quotaPage = page == RenderPage::Quotas || page == RenderPage::Claude || page == RenderPage::Codex || page == RenderPage::Domestic;
  if (quotaPage && currentEpochUtc() != lastQuotaSecond) { lastQuotaSecond = currentEpochUtc(); screenDirty = true; }
  if (bridgeFresh() && (page == RenderPage::Quotas || page == RenderPage::Domestic || page == RenderPage::Music) && !screenDirty) { drawSignalRing(page); return; }
  if (page == RenderPage::ScreenSaver) drawScreenSaver();
  else if (!bridgeFresh()) drawOffline();
  else if (page == RenderPage::Weather) drawWeather();
  else if (page == RenderPage::Stocks) drawStocks();
  else if (page == RenderPage::Quotas) drawQuotas();
  else if (page == RenderPage::Claude) drawSingleQuota(true);
  else if (page == RenderPage::Codex) drawSingleQuota(false);
  else if (page == RenderPage::Domestic) drawDomestic();
  else if (page == RenderPage::System) drawSystem();
  else if (page == RenderPage::Music) drawMusic();
  else if (page == RenderPage::Pet) drawPet();
  else drawDashboard();
  if (bridgeFresh()) drawSignalRing(page);
}

}  // namespace

void setup() {
  Serial.setRxBufferSize(kSerialRxBufferBytes);
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
  // All AI-bot RGB565/APET resources use natural little-endian uint16 values.
  display.setSwapBytes(true);
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
