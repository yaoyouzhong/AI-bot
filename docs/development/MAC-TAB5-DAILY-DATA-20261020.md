# Mac 日常数据补齐 / Daily data parity

接续音乐与 Codex 七日额度验收，用户另报告网络曲线、天气、首页农历下方信息、DeepSeek 和智谱额度缺失。本轮仍只修改 Mac 桥接，保持既有 v1 字段；不修改 Windows 或设备代码，不刷机、部署、发布或合并 main。

## 最新验收结果 / Latest acceptance

当前运行 `06dd164`，完整 [Mac CI 39400345443](https://github.com/yaoyouzhong/AI-bot/actions/runs/39400345443) 与附件核验通过：190 项测试、0 失败／跳过，本机 Release／签名包通过。实体已确认网络曲线、首页工作日、DeepSeek／智谱余额及微信二维码，Open-Meteo 当前／24 小时／7 天天气，以及和风来源／实时天气／AQI 等级／站点气压／24 小时／7 天预报均正常。用户已返回原日常总览，保留其选择的和风和自动定位；USB／Wi-Fi／BLE 正常。未记录位置、密钥、余额或天气具体数值，未修改或刷写固件。下文保留各阶段当时的待验与失败记录。

Running candidate `06dd164` passed CI 39400345443 and full artifacts (190 tests, zero failures/skips), plus local Release/signing. Physical network curves, workday text, DeepSeek/GLM balances and WeChat QR passed. Open-Meteo current/24-hour/seven-day weather passed; QWeather source/current conditions, AQI category, station pressure and both forecast tabs also passed. The user returned to the normal overview, retaining QWeather and automatic location; USB/Wi-Fi/BLE remain healthy. No location, credentials, balances or specific weather values were recorded; firmware was unchanged. Historical pending/failure checkpoints remain below.

## 已定位与修正

### 日历全年安排补齐（接续）

用户报告日历“放假安排待更新”。对照当前 `.234` 实际消费端，日历页读取 `calendar.detail.holidayYears`（兼容回退 `holidayYear`），与首页的 `holidayLabel` 分开。Mac 已有 2026 年公开事实表，但此前没有发送 `detail`，所以即使浏览已知年份也会显示待更新。现补发同一事实表的全年假期、补班日及 24 节气，保持已有协议；跨年浏览仍保留已知的 2026 表，不把它重标为未知年份，不伪造生日数据。未公布／尚未收录年份继续提示待更新。未修改固件。

新增 JSON 消费契约回归，验证春节范围、补班日、节气位置及跨年不冒充 2027 年。此记录时源码修复已完成，本机 XCTest 仍缺失，Release／CI 与新候选实体日历验收待完成；当前运行的 `06dd164` 尚不含修复。

The calendar page consumes annual `calendar.detail` tables independently of home labels. Mac already held published 2026 facts but omitted that object. The patch sends holiday ranges, compensating workdays and 24 solar terms using existing fields, retains the known 2026 table across year boundaries and does not invent future schedules or birthdays. Firmware is unchanged. A serialized-contract regression covers the actual consumer fields and unknown-year boundary. At this checkpoint local XCTest is unavailable; Release, CI and physical calendar acceptance are pending, and running candidate `06dd164` does not contain the change.

后续验证：程序 `29f1654e231a1f6e2f241a37093744fb9e164891` 的 [Mac CI 39403630848](https://github.com/yaoyouzhong/AI-bot/actions/runs/39403630848) 与完整附件核验通过，191 项测试、0 失败／跳过，Release／App／工具／原生界面与源码哈希均通过。本机 Release 和候选签名通过。已通过正常快捷键退出 `06dd164`，启动 `artifacts/mac-tab5-20261020/29f1654/AIBotBridge.app`；本次记录时等待用户处理系统钥匙串授权，连接恢复与实体日历尚未确认。

Subsequent validation: `29f1654` passed CI 39403630848 and full artifact verification (191 tests, zero failures/skips; Release/app/tools/native UI/source hashes), plus local Release/signing. The old candidate exited normally and the new temporary app started. User-handled Keychain authorization, connection recovery and physical calendar verification are pending at this checkpoint.

- 网络：Mac 只有瞬时上传／下载，没有 `sampleSession`、`sampleSequence` 和 `samples`。实际 `.234` 的 `render_system` 只有收到样本数组才调用 `render_chart`。新增单调时钟的 250 ms 采样、64 条有界历史、一秒不可变发布和末四样本均值；桥接重启更换会话，长采样间隔保留序号缺口，不重放虚构样本。TAB5 仍通过已有完整快照收到历史，本轮没有新增快速 metrics 传输。CPU／内存单独每两秒采样。
- 天气：本机没有城市／坐标或缓存，Mac 原先没有自动定位实现。新增用户主动选择的 Core Location 路径，默认关闭；位置仅按约一公里网格取整后查询既有 Open-Meteo 服务，不记录／持久化精确坐标。授权拒绝／超时有状态提示。手动城市继续可用，空气质量失败不再使成功天气丢失，主天气失败仍保留上次结果。
- 首页：实际页面农历下方读取 `calendar.holidayLabel`，Mac 原来只有日期、星期和农历。独立实现当前／次日的节气和工作日／补班／假期标签，采用公开的 2026 年事实表；未知年份明确显示待更新，不推算法定假日。本轮未实现 Windows 的联网节假日年度更新、生日管理或完整日历详情。
- 国产额度：Mac 原来没有余额采集，TAB5 还移除了 `domesticQuotas`。补齐显示缓存与固定的 11 个 provider 位置；DeepSeek 位于索引 5，智谱位于 6，保持 `billingMode=payg`、`balance`、`cost` 和币种契约，不以 token 数计算余额。未授权或未取得的值仍未知。
- DeepSeek：新授权输入框只写钥匙串，启用后每两分钟查询官方 `/user/balance`；拒绝 HTTP 跳转，错误只显示分类／状态码。更换密钥清除旧账户缓存；失败保留已有数据并标记旧数据。无模型调用或付费操作。
- 智谱：应用内打开官方财务页，使用独立临时 WebKit 会话。仅接收该页面已请求的精确财务接口成功响应，解析 `availableBalance` 和累计支出；不读取 Cookie、请求凭证、存储令牌或页面任意数字。校验主框架、HTTPS 来源、接口路径、响应大小和数据结构。关闭财务页释放会话并将余额标记为缓存；再次打开或点击刷新可更新。本轮不提供关闭网页后的后台自动查询。

## 验证与边界

新增回归涵盖网络发布／同一均值／有界历史／采样断档，日历调休／假期／跨年未知，空气质量失败与粗粒度坐标，余额真实字段／重复币种／非法值／缓存及固定数组位置，智谱接口来源约束。自动测试不等于定位、授权、真实余额和实体曲线验收。`2ce7c7d` 的 [Mac CI 39391053508](https://github.com/yaoyouzhong/AI-bot/actions/runs/39391053508) 及完整下载附件核验通过：180 项测试、0 失败／跳过、Release、候选包、随包工具、8 张原生界面 PNG 和哈希检查。用户在实体 TAB5 确认网络上传／下载曲线持续更新、首页农历下方工作日文字均正常。正常候选的音乐和 Codex 已通过结果继续保留。

用户明确允许自动定位天气后保存开关；第一次定位失败。系统设置显示 AI-bot 的定位权限关闭，按该次授权开启并重试，正常候选显示“天气：已更新”。用户确认实体当前天气已显示，但 24 小时／7 天预报仍为空；没有记录位置、温度或账户金额。

用户另报告智谱微信扫码没有二维码。官方登录页引用的 [微信 WxLogin 脚本](https://res.wx.qq.com/connect/zh_CN/htmledition/js/wxLogin.js) 创建 `open.weixin.qq.com/connect/qrconnect` 内嵌框架，旧导航策略只允许 bigmodel.cn，因此拦截了二维码。修正仅为子框架放行该 HTTPS 地址及初始 about:blank，拒绝主框架转往微信、其他微信路径、非标准端口和伪造域名；余额捕获仍只接受智谱主框架的精确财务接口。新增导航回归；新候选及扫码实机验证待执行，不能据代码修正写为通过。

本机 Release、候选 App 与严格签名核验通过；本机 XCTest 仍缺失，完整测试在 Mac CI 执行。单独链接实际 Debug 对象的原生采样检查读取了本机网卡计数，序号从 4 增长到 24、历史累积为 24 条；只记录样本个数和序号，未记录流量内容、接口地址或速率数值。该检查不替代 TAB5 图形验收。

事实与接口依据：

- [Apple CLLocationManager](https://developer.apple.com/documentation/corelocation/cllocationmanager)
- [Open-Meteo 天气接口](https://open-meteo.com/en/docs)
- [DeepSeek 余额接口](https://api-docs.deepseek.com/api/get-user-balance/)
- [2026 年国务院节假日安排（北京市政府转载）](https://www.beijing.gov.cn/fuwu/bmfw/sy/jrts/202511/t20251104_4258838.html)
- [香港天文台 2026 公农历及节气表](https://www.hko.gov.hk/tc/gts/time/calendar/text/files/T2026c.txt)

## English

The reported missing network chart, weather, home holiday line and DeepSeek/GLM balances are Mac producer gaps. Changes use existing v1 fields; Windows and firmware are unchanged. No deployment, release, main merge or flash is authorized.

Networking adds monotonic 250 ms samples, a bounded 64-sample tail, immutable one-second publications and a trailing four-sample mean. Suspensions leave sequence gaps, without fabricated samples; restart changes the sampling session. Existing full TAB5 frames carry the tail; a new fast metrics transport is not implemented. CPU/memory sample separately every two seconds.

Weather gains explicit, off-by-default Core Location selection, kilometer-scale rounded request coordinates, no precise-location persistence, and denial/timeout diagnostics. Manual locations remain available. Air-quality failure no longer discards a successful forecast; forecast failure retains last-good weather.

Calendar supplies today's/tomorrow's solar-term and holiday/workday labels using cited 2026 public facts, with explicit unknown-year fallback. Online yearly schedule updates, birthday editing and full calendar details are not part of this patch.

Domestic balances preserve the established eleven provider positions (DeepSeek 5, GLM 6), PAYG fields and unknown values. DeepSeek's key stays in Keychain; the official balance endpoint is polled every two minutes after authorization, redirects are denied and key changes clear the previous account cache. Errors preserve stale data, with no model calls or financial actions. GLM uses a temporary official-finance WebKit session, observing only the exact finance response requested by that page. Main-frame origin, path, size and schema are validated; credentials/cookies/storage and arbitrary DOM amounts are not read. Closing the finance window releases its session and marks the last balance cached; reopening or manual refresh updates it. Closed-window automatic GLM refresh is not implemented.

Regressions cover network publication/mean/bounds/gaps, calendar boundaries, optional air-quality failure, coarse coordinates, official balance fields/schema failures, cache preservation/provider positions and finance endpoint restrictions. Commit `2ce7c7d` passed Mac CI 39391053508 and complete downloaded artifact verification: 180 tests, zero failures/skips, Release, bundle/tools, eight native PNGs and hashes. The user confirmed live network curves and the home workday line on the actual TAB5. Earlier music and Codex acceptance stays valid.

The user explicitly authorized automatic-location weather. The initial request failed while the app’s system location permission was off. After enabling that permission within the authorized scope and retrying, the normal candidate reported weather updated. The user confirmed current weather on the device but found hourly/daily forecast tabs empty; actual domestic balances remain pending; no location, weather values or account amounts were recorded.

The user reported a blank WeChat QR login. The official WxLogin script embeds open.weixin.qq.com/connect/qrconnect, which the old BigModel-only navigation policy blocked. The fix permits only that HTTPS subframe and its initial about:blank, rejecting main-frame WeChat navigation, other paths, nonstandard ports and lookalike domains. Finance capture remains restricted to the exact BigModel main-frame endpoint. A navigation regression was added; candidate and QR-device acceptance are still pending.

Local Release, candidate app and strict signing verification passed. Local XCTest remains unavailable; full tests run on Mac CI. An isolated harness linked to the actual Debug objects verified native network counter sampling: sequence advanced from 4 to 24 with 24 retained samples. Only counts/sequences were recorded, not traffic contents, interface addresses or rate values. This does not replace physical chart acceptance.


## 预报与和风后续 / Forecast and QWeather follow-up

用户确认当前天气后发现两个预报页为空，并要求测试和风天气。原因是原 Mac 仅请求 `forecast_days=1` 和当前／最高／最低温，没有小时或每日数组。对照实际 `.234` 的 `ui_weather.h` 及 Windows 已有字段后，Mac 独立补齐既有 `hourly`（最多 24）和 `daily`（最多 7）、城市时区、降雨未知值及日出／日落信息；不修改协议版本或设备代码。缓存重载、查询失败和部分预报失败均保留已取得的序列并标记缓存，不制造零值。

天气设置新增 Open-Meteo／和风选项。和风按 Windows 当前使用的官方 v7 城市接口查询 now／24h／7d；自动位置以两位小数发送，手动城市通过和风 GeoAPI 解析。API Host 只允许控制台分配的合法 `qweatherapi.com` 子域名（包括区域多级域名）；API Key 按 Host 分开保存在钥匙串，以请求头发送，拒绝重定向。没有导入 Windows 密钥或执行真实和风请求；需要用户在正常 App 中授权并填写。当前和风路径未接入空气质量、JWT 或新版 v1 接口；官方已提示 v7 将弃用，后续须跟进，不能将该兼容实现表述为完整供应商功能。

新增预报测试覆盖城市时区（含非整小时偏移）、24／7 上限、缺失及越界降雨概率、日期与日出校验、部分失败、缓存重载、实际 TAB5 快照字段及请求目标／密钥头。`a2078fd` 二维码补丁 CI 39392319817 已通过 181 项测试；天气后续 `efb9620` 的 [CI 39393139531](https://github.com/yaoyouzhong/AI-bot/actions/runs/39393139531) 与完整附件核验通过：185 项测试，0 失败／跳过，Release、签名候选、工具、8 张原生 PNG 和源码／包哈希均通过。本机 Release／签名候选通过，本机 XCTest 仍缺失。候选正常启动，用户完成钥匙串授权；USB 数据确认、Wi-Fi 认证请求及 BLE USB 优先待命均恢复。用户已确认智谱二维码出现、登录后取回余额、TAB5 显示正常，扫码与额度链路实机通过。预报实机仍待核验。

The user confirmed current weather but reported empty 24-hour/seven-day tabs and requested QWeather testing. The old Mac request fetched only one day's extrema/current conditions. The independent Mac projection now supplies the existing hourly/daily arrays (24/7 maximum), city timezone, nullable precipitation and solar fields, following the actual `.234` consumer and established Windows schema. Cache reload and failures retain known series as cached, without fabricated zero values. Firmware and protocol versions are unchanged.

Weather settings add Open-Meteo/QWeather selection. QWeather uses the same official v7 now/24h/7d family currently used by Windows, rounded automatic coordinates or provider GeoAPI city lookup. Keys are stored per API Host in Keychain and sent only in request headers to validated valid `qweatherapi.com` subdomains, including regional multi-label hosts; redirects are denied. No Windows credential import or actual QWeather request has occurred. Air quality, JWT and the newer v1 API are not part of this implementation; the provider's announced v7 deprecation remains a follow-up. Explicit normal-app authorization and user-entered credentials are required for real acceptance.

New regressions cover timezone offsets, series bounds, null/invalid precipitation, dates/solar data, partial failure, cache reload, actual TAB5 wire fields and credential destination/header restrictions. QR fix `a2078fd` passed CI 39392319817 with 181 tests; Forecast commit `efb9620` passed [CI 39393139531](https://github.com/yaoyouzhong/AI-bot/actions/runs/39393139531) and complete artifact verification: 185 tests, zero failures/skips, Release, signed bundle/tools, eight native PNGs and source/package hashes. Local Release/bundle verification passed; local XCTest is still unavailable. The candidate started normally after user Keychain authorization, with USB data confirmed, authenticated Wi-Fi requests and authenticated BLE USB-priority standby. The user confirmed the WeChat QR appeared, login retrieved the balance and TAB5 displayed it correctly. QR/balance acceptance passed; forecast hardware acceptance is still pending.

接口依据 / API references:

- [和风认证 / Authentication](https://dev.qweather.com/en/docs/configuration/authentication/)
- [和风 v7 小时预报 / Hourly forecast](https://dev.qweather.com/en/docs/api/weather/weather-hourly-forecast-webapi-v7/)
- [和风 v7 每日预报 / Daily forecast](https://dev.qweather.com/en/docs/api/weather/weather-daily-forecast-webapi-v7/)

本机正常额度窗口已显示 DeepSeek 余额已更新，授权由用户在 App 内完成；没有读取或记录 API Key／余额数值，用户确认实体 DeepSeek 额度页已有数据，该链路实机通过。

The normal app now reports DeepSeek balance updated after the user configured access in-app. No API key or balance amount was read or recorded; the user confirmed DeepSeek data on the physical device, so that path passed acceptance.


`efb9620` 实际刷新曾显示天气查询失败并保留旧缓存。使用固定公开测试坐标（非用户位置），独立链接真实 Debug 对象的原生 URLSession／解析检查返回 HTTP 200、24 小时和 7 天；只记录状态和序列数量，不替代正常 App／设备预报验收。和风设置已交给用户在本机填写；未读取密钥。

A normal `efb9620` refresh reported a query failure and retained its cache. An isolated native URLSession/parser check linked to actual Debug objects, using fixed public test coordinates rather than the user's location, returned HTTP 200 with 24 hours and seven days. Only status/counts were recorded; this does not replace normal-app/device forecast acceptance. QWeather setup awaits user entry in the app; no key was read.


和风首次配置未切换成功：正常设置窗口的来源仍为 Open-Meteo；新增 Host 校验错误地只接受单级子域名，误拒绝控制台分配的区域多级域名。修正为逐段 DNS 校验后严格限定 `qweatherapi.com` 后缀，仍拒绝伪造后缀、空标签、userinfo、协议／端口／路径。设置仅在选择和风时展示凭据字段，切换来源保留未提交输入；状态提示增加实际来源名称。补充真实格式（合成域名）与 UI 切换回归；不保存用户 Host、密钥或金额到此记录。该补充候选仍待构建／CI／实机。

The first QWeather configuration did not switch the selected provider from Open-Meteo. The new host validator incorrectly accepted only single-label subdomains, rejecting a legitimate regional multi-label host. It now validates each DNS label and strictly requires the qweatherapi.com suffix, still rejecting lookalike suffixes, empty labels, userinfo, schemes, ports and paths. Credential fields appear only when QWeather is selected and preserve unsubmitted edits across source changes; status includes the actual source. Synthetic regional-host and UI-switch regressions were added. No user host, key or balance is recorded. This follow-up awaits build/CI/hardware checks.


## 空气质量与气压后续 / Air quality and pressure follow-up

用户已确认 Open-Meteo 的 24 小时预报（小时／温度／曲线）及未来 7 天（日期／天气／高低温）均正常。另报告 AQI 只有数字没有等级、大气压强为空；对照公开 `.234` 固件 `ui.c` 消费字段，确认 Mac 缺少 `airQualityLabel`／`airQualityStandard`／`pressureHpa`／`pressureKind`，无需修改或刷写固件。

Mac 补齐 Open-Meteo `pressure_msl`（海平面气压）及和风 now.pressure（站点气压），使用既有 hPa 字段。Open-Meteo AQI 明确使用 US EPA 标准与对应中文等级，不能将其冒称中国 AQI。和风增加可选的官方 v1 当前空气质量请求，按照实际返回的中国（cn-mee／cn-mee-1h）或美国（us-epa／us-epa-nc）标准映射等级并验证 PM2.5 单位；不会将小数 QAQI 四舍五入为另一标准。空气质量失败不丢弃成功的天气；同城市／来源／时区下缺失的已知空气质量和气压保留为缓存并标记 stale，来源切换不混用旧值。无效／缺失气压仍未知，不估算。

和风 Host/UI 补丁 `afa1f0d` 的 [CI 39399002879](https://github.com/yaoyouzhong/AI-bot/actions/runs/39399002879) 与完整附件核验通过：186 项测试，0 失败／跳过。为避免连续两次更换候选及钥匙串授权，未单独运行此包，将与本次字段修复一并上机。实际和风成功查询及新增字段显示仍待验收。

The user confirmed both Open-Meteo forecast tabs on the physical TAB5. Missing AQI categories and pressure are Mac producer omissions against existing `.234` fields, requiring no firmware change. The Mac now requests sea-level pressure from Open-Meteo and reads station pressure from QWeather, supplies the explicit US EPA scale/category for Open-Meteo, and optionally fetches QWeather v1 current air quality using its actual regional Chinese or US standard. A regional-index regression checks Chinese categories against the [official index table](https://dev.qweather.com/en/docs/api/air-quality/aqi-list/). Fractional QAQI is never relabeled or rounded into US AQI; PM2.5 units are checked. Partial failures retain compatible known facts as stale; provider changes do not inherit old facts, and missing pressure is not estimated.

Host/UI fix `afa1f0d` passed CI 39399002879 and complete artifact checks: 186 tests, zero failures/skips. Its standalone bundle was not launched, to combine this follow-up into one candidate and avoid another redundant Keychain authorization. Actual QWeather responses and the new device fields remain pending.

Sources: [Open-Meteo air quality](https://open-meteo.com/en/docs/air-quality-api), [Open-Meteo weather](https://open-meteo.com/en/docs), [QWeather current air](https://dev.qweather.com/en/docs/api/air-quality/air-current/), [QWeather current weather](https://dev.qweather.com/en/docs/api/weather/weather-now-webapi-v7/).


`fbebfa9` 的 [CI 39400001872](https://github.com/yaoyouzhong/AI-bot/actions/runs/39400001872) 及完整附件核验通过：189 项测试、0 失败／跳过，Release／签名候选／工具／原生 UI 与哈希通过。独立链接实际 Debug 对象，使用固定公开坐标 0,0 的原生 URLSession 检查，天气与空气质量均 HTTP 200，解析确认 24 小时／7 天、气压值和类型、AQI 数值／等级／标准均存在；只记录状态与字段存在性，没有使用或记录用户位置。后续 `06dd164` 补充中国地区返回标准的覆盖，最终候选仍待完整 CI 与上机。

`fbebfa9` passed CI 39400001872 and full artifact verification: 189 tests, no failures/skips, Release/bundle/tools/native UI/hashes. A native URLSession harness linked against actual Debug objects queried fixed public coordinates (0,0): both endpoints returned HTTP 200, with 24 hours/seven days and populated pressure/type and AQI/category/standard. Only status and field presence were recorded, with no user location. Follow-up `06dd164` adds Chinese regional-standard coverage; final CI and device acceptance remain pending.


最终程序 `06dd164` 的 [CI 39400345443](https://github.com/yaoyouzhong/AI-bot/actions/runs/39400345443) 及完整附件核验通过：190 项测试，0 失败／跳过。Mac 本机 Release 与临时签名候选包通过，XCTest 仍不可用。新候选路径 `artifacts/mac-tab5-20261020/06dd164/AIBotBridge.app`，尚未启动：尝试正常退出旧 `efb9620` 时，应用退出菜单禁用；工具仅看到 TAB5 主窗，截图不可用，Escape／取消快捷键未解除。已请用户处理尚未结束的设置／提示并正常退出，未同时启动第二个桥接。新增空气质量／气压与和风实机验收仍待完成。

Final code `06dd164` passed CI 39400345443 and complete artifacts: 190 tests, zero failures/skips. Local Release and signed bundle passed; local XCTest remains unavailable. The new candidate is built but not launched: normal exit of old `efb9620` was disabled, the UI tool exposed only its main TAB5 window with no screenshot, and cancel shortcuts did not resolve it. The user was asked to dismiss any outstanding settings/alert and exit normally. No second bridge was launched. Actual AQI/pressure and QWeather device acceptance are still pending.


用户确认已正常退出旧候选后，核对旧进程消失，启动 `06dd164`，用户完成钥匙串授权。正常 App 显示 USB 数据已确认、Wi-Fi 认证请求、BLE 已认证／USB 优先待命。新临时签名候选的原自动定位权限再次为关闭，在先前明确授权范围内恢复后，通过正常设置保存触发刷新，显示“天气：Open-Meteo 已更新 · 24 小时 / 7 天”。已请求实体确认 AQI 等级及气压；尚未切换和风。

After the user exited the old app, its process was confirmed gone and `06dd164` was launched. The user handled Keychain authorization; normal UI confirmed USB data, authenticated Wi-Fi and BLE USB-priority standby. The new ad-hoc candidate's previously authorized location toggle was off again; restoring that permission and saving normal settings refreshed Open-Meteo with 24 hours/seven days. Device confirmation of AQI categories/pressure is pending; QWeather has not yet been selected.


`06dd164` 正常 App 先完成 Open-Meteo 刷新；实体新增字段反馈“都 OK”。随后只读 UI 发现用户已自行完成和风选择和保存，正常 App 显示“和风天气 已更新 · 24 小时 / 7 天”；没有再次填写或读取其密钥。用户进一步明确确认设备和风来源、温度、空气质量等级和气压正常，并分别确认和风小时及七日页全部正常。和风 v7 `now.pressure` 官方定义为站点气压（hPa），标签准确；Open-Meteo 的 `pressure_msl` 为海平面气压，保留两者区别。用户最后确认返回日常总览，和风及自动定位保留。

The normal candidate first refreshed Open-Meteo and the user reported the new fields working. A subsequent UI read showed the user had selected/saved QWeather, reporting fresh 24-hour/seven-day data; no key was re-entered or read by the agent. The user then explicitly confirmed QWeather source/current conditions/AQI category/pressure, followed by both forecast tabs. QWeather v7 defines now.pressure as station pressure (hPa), while Open-Meteo pressure_msl is mean-sea-level pressure; labels preserve that distinction. The user confirmed returning to the daily overview with QWeather/automatic location retained.
