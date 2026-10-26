# Data sources and privacy

Applies to the current Windows 0.6.1 implementation unless a section names macOS. Configure shared sources under **Device Center → Account Data** and select each device's requirements under **My Devices → Data Settings**. Feature-specific flows such as [Codex Direct](TAB5-QUICK-CONSOLE.md) and [artwork imports](GALLERY-PACKS.md) have separate guides.

## Weather

Windows lets users select Open-Meteo or QWeather independently of automatic/manual location. Open-Meteo needs no key and offers free personal non-commercial numerical forecasts with US-standard AQI. QWeather needs its assigned API host and key, offers Chinese district names and local AQI, and follows the account's allowance and billing terms. Both provide hourly and seven-day forecasts; values and AQI standards can differ. Existing configurations retain their previous provider until explicitly changed. QWeather failures fall back to Open-Meteo; if both fail, the last successful data remain available. The bridge sends the configured city or coordinate; optional automatic location uses Windows location services. QWeather keys stay in Windows Credential Manager and are sent to the configured `qweatherapi.com` host. Open-Meteo data retain attribution to [Open-Meteo](https://open-meteo.com/).

Windows 可独立选择 Open-Meteo 或和风天气，自动／手动位置不会切换数据源。免费源免密钥，提供个人非商业数值预报与美标 AQI；和风需要 Host / Key，支持中文区县与当地 AQI，按账户额度和计费规则使用。两者均提供逐小时及七天预报，天气数值与 AQI 口径可能不同。旧配置保持原数据源，和风失败时回退免费源；两者均失败时保留上次成功的数据。

Current Windows weather includes conditions, temperature/range, humidity, pressure, available air-quality fields and hourly/daily forecasts. Fields and AQI standards depend on the provider; absent values remain unavailable. A successful response is cached in `%APPDATA%\AI-bot\weather-provider-cache.json`; failed refreshes preserve the last successful snapshot and mark it stale.

macOS has its own Open-Meteo implementation and stores non-secret preferences and last-successful display data in `UserDefaults`; do not assume it implements every Windows weather field or setting. CI tests/builds do not establish live-location or provider acceptance on a real Mac.

## Calendar / 日历

Mac and Windows obtain solar-term date facts from the Chinese Academy of Sciences [Purple Mountain Observatory (PMO) official calendar index](https://pmo.cas.cn/xwdt2019/kpdt2019/202203/t20220309_6386774.html). The reader follows only HTTPS annual-calendar PDF links on `pmo.cas.cn` / `www.pmo.cas.cn`, validates the cover year and attribution, all twelve monthly sections and exactly 24 ordered solar terms, then retains only normalized date facts. This replaces Hong Kong Observatory network requests. It is a public-document reader, not an official API. On 2026-10-24 the index lists annual documents through 2026; missing future years are not invented or treated as downloaded. Existing verified 2026–2028 offline dates and last-successful caches remain available. Neither source PDF documents nor website text/layout are bundled. Attribution: `licenses/pmo-calendar/NOTICE.md`. Mac uses system PDFKit; Windows uses PdfPig 0.1.16 with its original Apache-2.0 and component notices retained in `licenses/pdfpig/`.

Mac 与 Windows 节气源改为中国科学院紫金山天文台官方历书索引，停止向香港天文台发起更新请求。只跟随上述官方域名的 HTTPS 年度日历 PDF，核对封面年份、编制机构、十二个月及完整有序的 24 节气，再转换为原缓存格式；这是公开文档读取，不是官方 API。2026-10-24 索引最新列到 2026 年，未列出的后续年份保留原离线数据，不虚记下载成功。原 2026–2028 已核对日期及成功缓存继续可用。不捆绑来源 PDF、网页文字或布局，只使用公开日期事实；归属见对应 NOTICE。Mac 使用系统 PDFKit，Windows 使用 PdfPig 0.1.16 并保留完整原许可及组件声明。

Holiday schedules still use the MIT [holiday-cn dataset](https://github.com/NateScarlet/holiday-cn), which cites State Council notices and is not an official government API. No device, account, birthday or agenda data are uploaded. Mac validated public date tables remain under `~/Library/Application Support/AI-bot/calendar`; Windows keeps its existing local calendar directory. The existing `terms-YEAR.txt` grammar remains readable by older versions. Startup restores caches before fetching; successful checks repeat daily and failures retry hourly. Failed or unpublished updates preserve prior data. Unknown holiday years remain pending; lunar festivals do not imply statutory time off.

放假／调休仍使用引用国务院公告的 MIT holiday-cn 数据集，它不是政府 API。请求不上传设备、账户、生日或日程。Mac 校验后的公开日期缓存于用户目录，Windows 沿用现有日历目录，`terms-YEAR.txt` 保留旧版本读取兼容。启动先恢复缓存；成功后每天检查，失败一小时后重试。失败或未公布空表不覆盖旧数据，未知年度放假安排继续待更新，不由农历节日推断。

## Stocks

AI-bot currently reads quote responses from `qt.gtimg.cn` for configured `sh`, `sz`, `bj`, `hk`, and `us` symbols. This endpoint has no public stability or redistribution commitment documented by this project. Quote data are fetched at runtime and are not bundled in the repository or release archives.

The configured symbol list is sent to that quote endpoint. A successful response is cached under `%APPDATA%\AI-bot`; a failed refresh keeps the prior snapshot and marks it stale.

The macOS source uses the same quote endpoint and stores the configured symbols and last-successful display snapshot in `UserDefaults`. Current CI tests/builds do not establish live refresh or physical-display acceptance.

## Claude and Codex account quotas

The bridge reads the existing local CLI sign-in files only when requesting account quota data:

- Claude: `%USERPROFILE%\.claude\.credentials.json`, sent only to `api.anthropic.com`.
- Codex: `%USERPROFILE%\.codex\auth.json`, sent only to `chatgpt.com`.

Access tokens are held in memory for the request. They are not copied to AI-bot settings, status JSON, serial frames, logs, or caches. `%APPDATA%\AI-bot\usage-cache.json` contains only display data such as plan, utilization percentages, reset times, reset-credit counts, update time, and stale state. A failed request preserves the last successful snapshot. The current implementation does not refresh expired CLI credentials; the corresponding CLI must refresh its own sign-in first.

The macOS source follows the same credential-file, destination-host, no-log, and display-only-cache boundaries. It refreshes every two minutes while running and stores its display-only cache in `UserDefaults`. CI builds do not establish real-account acceptance.

## Music on Windows

Windows music enumerates public GSMTC sessions and prefers a playing source over
a paused default application. The selected session supplies metadata and cover
art. NetEase Cloud Music may publish no valid playback timeline even with SMTC
enabled. Its read-only adapter locates two unique instruction
signatures in the x64 `cloudmusic.dll` code section and reads the current song ID,
position, duration and playing/paused state. It uses checked reads and releases
process handles on exit/cancellation; access denial, invalid values, ambiguous
signatures and unsupported architecture leave the clock unavailable.

The current song ID is matched to the bounded local `WebData/file/playingList`
record, then checked against the selected SMTC title, artist and available album.
Translations/aliases and the full album are added only for that exact song. The
shared `title` can contain an original title followed by its translated alias on
the next line. TAB5 displays both; the 232×44 small-screen text bitmap prefers the
alias. Artwork remains sourced from SMTC. No account files, cookies, audio, full
memory dumps, remote song searches or injected plugins are used.

Playback is sampled every 500 ms. Recently observed playing positions compensate
for the time since sampling using a monotonic clock; paused, unknown and samples
older than three seconds are not advanced. TAB5 refreshes cached music state at
500 ms without waiting for the tray's two-second tick; USB uses the same shorter
cadence while a valid music timeline is present. Other cached state still expires
if its regular publisher stops. TAB5 shows the original album name without a
field prefix, consistent with its title and artist labels.

While artwork is pending, Wi-Fi/BLE telemetry alternates state delivery with
bounded artwork bursts even if music state keeps changing. Song changes, pause
and seeking retain priority. Automatic display transport preference remains
USB, then fresh Wi-Fi, then BLE; a connected BLE link can run concurrently and
does not by itself identify the active display transport.

The existing `elapsedSeconds`, `durationSeconds`, `playing` and
`timelineAvailable` fields are reused for TAB5 and ESP8266. A valid system clock
has priority. Without a native/system clock, the existing exact-song SQLite
fallback can provide total duration only: TAB5 shows unknown elapsed time, while
the legacy ESP8266 USB frame keeps its existing zero-time compatibility fallback.
Diagnostics are loopback-only at `/diagnostics/music`. Current local compatibility
is NetEase `3.1.40.205461` x64. QQ Music now has a separate bounded adapter for
the recognized x86 `QQMusic.dll` layout, collecting the current title, artist,
album, cover URL, clock and playing/paused state. It can work without SMTC; exact
matching prevents stale QQ system metadata from replacing the native song. A
valid matching system timeline remains preferred. Only public Tencent artwork
URLs are downloaded asynchronously; results belong to the exact song and URL.
Unsupported layouts, ambiguous signatures and buffering states are not guessed.
Live sampling of QQ Music `22.71.10.11.55` x86 on 2026-10-09 confirmed native
current-song data, advancing progress and public artwork. Interactive controls
and device rendering remain pending; see [validation limits](MEDIA-PLAYERS.md).

Spotify, Apple Music, Windows Media Player and other applications are handled
when they expose public system sessions. This is conditional interface support,
not a promise that every version of KuGou, Kuwo, VLC, PotPlayer or foobar2000
publishes complete SMTC data. The optional Chrome/Edge companion also supplies
current HTML media state, metadata, real position, duration and playback rate
from explicitly enabled sites. Multiple simultaneously active elements are
ambiguous; live streams retain unknown progress. Closed/stale sources expire
after three seconds, and artwork is scoped by tab/site and track. No playback
history or credentials are persisted. See [support and validation](MEDIA-PLAYERS.md).

Windows 音乐优先采用系统媒体会话。网易云未提供时间线时，只读当前播放进程，
核对歌曲身份后补充真实位置、总时长、播放状态、中文译名及专辑。TAB5 和小屏共用
采集结果；TAB5 可显示原名和中文别名，小屏文字位图优先中文别名。读取失败保持
时间未知，不以历史播放记录推算实时进度。QQ 音乐另有当前歌曲的有限只读适配，
可在没有 SMTC 时提供信息，精确匹配歌曲后优先采用有效系统时间线；未知结构不猜测。
其他客户端接入其公开系统媒体会话，是否提供完整信息需按版本实测。Chrome／Edge
媒体助手补充已授权网站的曲目、进度、暂停和倍速，过期标签页失效，封面按来源隔离。
具体支持范围与尚未实测项见[媒体支持说明](MEDIA-PLAYERS.md)。
播放采样和 TAB5 音乐帧刷新采用 0.5 秒间隔，仅在近期有效播放样本上补偿等待时间；
暂停、未知或过期时间不递增。TAB5 歌名、歌手和专辑均直接显示原始文字，不添加字段标识。
封面待同步时，Wi-Fi/蓝牙在状态发送间穿插批量封面分片，持续刷新状态不会挤掉封面；
切歌、暂停和拖动进度仍优先。自动模式仍按 USB、有效 Wi-Fi、蓝牙选择显示通道，
蓝牙连接可与 Wi-Fi 并存，不能单凭蓝牙连接状态判断当前显示通道。

## Music on macOS / macOS 音乐

Music access is opt-in. The Mac bridge checks whether Apple Music (`com.apple.Music`) or Spotify (`com.spotify.client`) is running, then uses public Apple Events for title, artist, album, playback state, elapsed time, duration and artwork. Apple Music artwork candidates are decoded before use: an Apple Event `missing value` can contain nonempty bytes and is not an image. A bounded, read-only lookup of the same database ID in the current playlist or library can confirm a candidate; no playlist or library contents are exported. The streaming current-track interface has been observed to return an old valid picture with current metadata, so that response alone is insufficient confirmation.

When that lookup cannot confirm artwork, the bridge queries Apple's public [iTunes Search API](https://developer.apple.com/library/archive/documentation/AudioVideo/Conceptual/iTuneSearchAPI/) in HK/US storefronts using the current title, artist and album. Results must uniquely match all three fields and duration within two seconds; traditional/simplified Chinese variants and title-only 你/妳 storefront spelling are normalized, while artist/album checks and live/edition qualifiers are preserved. This catalog does not cover every Apple Music subscription recording. Missing or ambiguous results stay unavailable rather than selecting a different recording. If that catalog has no exact match, the bridge reads metadata embedded in Apple Music’s public regional search and album pages. It requires one matching album and checks its structured song title, artist, album and recording duration before using the Apple CDN image. Regional home-page redirects are rejected and the regional search is retried once. This is a public-document fallback, not a guaranteed anonymous Apple Music API; page changes or ambiguous metadata remain unavailable. Search is limited to18 requests per minute. Network catalog/page attempts are limited to once per current track per minute; playback sampling continues while artwork is pending. Artwork downloads accept only HTTPS Apple CDN hosts and request a600×600 variant where the returned size path is recognized, falling back to the original URL if needed. Spotify artwork downloads accept only its `scdn.co` / `spotifycdn.com` HTTPS hosts. Sessions are ephemeral, without cookies or account credentials, and use5/8-second request/resource timeouts.

Images are limited to10MiB and4096×4096 source dimensions. ImageIO normalizes EXIF orientation. ESP8266 keeps112×112 RGB565; TAB5 firmware0.2.80+ receives aspect-preserving JPEG up to560×336 (square covers336×336), with JPEG quality0.88 and an encoded limit of512KiB. Older firmware receives the112×112 fallback. These are separate in-memory resources. Track changes clear the old cover immediately, wait for a stable observation and recheck track identity after asynchronous work. Failed refreshes preserve the same track's last successful picture. Music metadata and artwork are not persisted; the runtime image preview stays in RAM, and exported diagnostics contain only status, dimensions and resource identifiers.

音乐采集须用户开启。Mac 通过公开 Apple Events 读取当前播放信息和封面；只在当前播放列表／资料库中按同一 database ID 只读定位，不导出列表或资料库内容。接口已观察到“新歌资料配旧图”，因此单凭 current track 返回有效图片不能认定匹配。无法确认时向 Apple 公开 iTunes 目录发送当前歌曲、歌手和专辑，在香港／美国目录中同时严格核对三项及两秒内时长；规范繁简体并兼容标题的“你／妳”地区写法；歌手、专辑及现场版等版本限定继续严格核对。目录不覆盖全部订阅曲目，缺失或歧义保持未知，不取其他版本顶替。旧目录无精确匹配时，再只读 Apple Music 对应地区公开搜索页和专辑页的内嵌资料，要求唯一专辑，并核对录音标题、歌手、专辑和时长后才下载 Apple CDN 图片。首页重定向不作封面来源，可在重定向地区重试一次搜索。这是公开文档后备读取，并非保证稳定的匿名 Music API；页面结构变化或资料歧义时保持未知。同曲目网络目录／页面尝试每分钟最多一次，等待封面时播放状态继续采样。每分钟最多18次搜索；Apple CDN 在已知尺寸路径上先请求600×600，失败才回退原地址。Spotify 仅下载其官方封面域名；临时会话不含 Cookie、账户或凭据。

图片经方向归一化后分别生成小屏112×112 RGB565与 TAB5 高清 JPEG。TAB5 .080起支持最高560×336，方图336×336，质量0.88，最大512KiB；旧固件回退原尺寸。源图限制10MiB／4096×4096。切歌立即清旧图，稳定观察并在异步完成后复核；同曲目刷新失败保留最近成功封面。不持久化歌曲资料或封面；运行监控的实际采样预览只在内存显示，导出诊断仅含状态、尺寸与资源标识。生成的应用包保留 Automation 用途说明和 Apple Events entitlement，权限由用户在 macOS 本机确认；具体实体覆盖以当前验收记录为准。

## Domestic-provider quotas

Windows **Device Center → Account Data → Model Accounts** opens the provider configuration. DeepSeek balance, MiniMax Token Plan and Kimi's local usage service have credential-backed API paths. Ali Token Plan, Zhipu balance and other supported browser adapters use an isolated WebView2 profile at `%APPDATA%\AI-bot\quota-auth-profile`. Browser capture filters responses according to the selected provider and its endpoint rules before parsing display fields; it is not a general network-history export. Provider-specific support and unverified adapters are listed in [the setup guide](DOMESTIC_QUOTA_SETUP.md).

Keys saved through the Windows form reside in Windows Credential Manager. MiniMax checks its saved credential first, then the compatibility environment variables `MINIMAX_SUBSCRIPTION_KEY`, `MINIMAX_TOKEN_PLAN_KEY`, and `MINIMAX_API_KEY`; its endpoint is `https://www.minimaxi.com/v1/token_plan/remains`. Kimi's local-service token goes to the configured loopback service. The configured credentials are not stored in ordinary settings, device status frames or display caches.

The current Windows display cache is `domestic-provider-cache.json`; older `domestic-quota-cache.json` data may be read for compatibility. These caches hold normalized provider/plan names, percentages, reset times, balance/cost/currency and timestamps. Failed requests retain prior successful values. The WebView2 profile persists provider cookies and browser storage, so it must not be committed, copied into releases or treated as a shareable cache. Parser/build tests are not evidence that a real account still matches a provider's current response schema.

## Legacy settings compatibility

When `%APPDATA%\AI-bot\settings.json` does not yet exist, the bridge may read a strict allow-list of non-secret values from `%APPDATA%\AIClockBridge\settings.json`. It does not modify the legacy file and does not migrate API keys, cookies, OAuth credentials, passwords, or Windows Credential Manager entries.

## Museum library expansion / 博物馆图库扩充

The first 2026-10-08 expansion batch reached 1,765 paintings and 497 calligraphy works. The batch uses official CMA open-access metadata/images, ColBase records/photos under its attributed-use terms, and Mia public-domain records/photos. Complete multi-image objects are grouped under one work identity. Metadata evidence and original-image fingerprints remain separate; artist attribution and copy qualifications are preserved. The optional packs contain native derivatives and provenance, rather than research-cache originals. See [the complete expansion report](art-guides/EXPANSION-2026-10-08.md).

2026-10-08第一批扩充后的本地资源为名画1765件、书法497件。来源采用馆方资料与图像服务，保留逐件证据、原图校验值及归属限定。成套、多页对象共用一个作品身份；可选图库包包含原生排版图片和来源记录，研究缓存原图单独保存。中文导读与浏览器交互仍为独立预览，不能视为桥接或固件详情页已接入。

### Calligraphy supplement / 书法追加批次

Local resource edition 2026.10.08.1 adds 370 calligraphy works from ColBase / Tokyo National Museum (98), Met (55), Princeton (1), Cleveland (3) and Mia (213), using 675 official photographs. The catalogue now has 1,765 paintings and 867 calligraphy works. Source culture/place fields identify 108 new Chinese, 261 Japanese and one Korean work. Album pages and component scrolls remain inside one independent work. Original resolution, image/record identity, reproduction qualifications, public-use rights and hashes are checked separately.

书法第二批新增370件，使用675张馆方原图；现有名画1765件、书法867件。新增作品的馆方文化／制作地归属为中国108、日本261、韩国1。对联、三联幅、四屏、十二屏和整册不拆分计数；正文、款印、拓本形态及历史残损保留，不用通行诗文猜补释文。来源与导入证据见[扩充报告](art-guides/EXPANSION-2026-10-08.md)。
