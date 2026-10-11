# 汽水音乐与 Apple Music 接入

2026-10-11。用户要求音乐页增加汽水音乐，并在安装汽水音乐期间追加 Apple Music。范围为 Windows 桥接的音乐来源与兼容处理；复用既有 `MusicSnapshot`、封面和音乐页，ESP8266／TAB5 不新增协议或固件。用户随后授权将候选部署到电脑并验证 TAB5 汽水音乐；本机部署与验收已完成，Apple Music 实播延期，没有公开发布。

## 实现与来源

- 保留枚举全部 Windows 媒体会话、优先播放中来源、当前会话／前次来源稳定选择的路径。新增汽水音乐中文应用 ID 与 `SodaMusic` 可执行来源名称；Apple Music 已有系统媒体接入，补充包／进程和 `com.apple.Music` 名称识别及专项回归。
- 系统音乐样本统一校验时长、相对位置、更新时间和倍速。只有有效的播放时间线才计算当前位置；暂停保持位置。缺失或异常时间线将当前位置置零并标记未知，仍保留有效的总时长，不用播放器启动时间或历史记录推算进度。
- 保留曲目／播放器独立资源键和整组替换，避免将汽水音乐封面带入 Apple Music。只读 `--test-media-live` 同时记录选中样本及各 Windows 来源的原始元数据、状态、时间线、封面是否上报，便于区分元数据、总时长和实时进度。
- 通过[汽水音乐官方下载页](https://www.qishui.com/download)的资源脚本及其公开配置接口，取得 Windows x64 `3.7.0` 应用资源包。使用 `baoyu-electron-extract` 检查官方包内 Source Map：主程序应用 ID 为 `汽水音乐`，播放器通过 Media Session 上报曲名、歌手、专辑及封面。此静态事实不等于当前安装版本实际提供有效时间线。
- 下载包 SHA-256：`30fe7762ccdb3c978cd51f8ca4696aed5e8cad876d7bfc20b25168d4cd2d40ac`。下载包、解包资源与源图映射只保留在被 Git 忽略的 `artifacts/development/soda-music-20261011/`，未执行该包、修改客户端或将第三方代码复制到产品实现。
- 本机已安装汽水音乐 `3.7.0` 和 Apple Music `1.1540.23042.0`。Apple Music 显示“没有有效订阅”，点击已有歌单仍没有启动播放或创建系统媒体会话；用户确认暂时没有订阅，要求先完成开发，真实播放验收延期。

## 验证与边界

证据目录：`artifacts/development/soda-music-20261011/`。

- 隔离输出的 Release 构建通过，0 警告／0 错误；候选 DLL SHA-256 `9fe606b8fc07a14e6816225a2e13d19b05a12c251f1b37bf50e650f2b0dcd793`。后续本机部署核对了相同文件身份。
- `SODA_APPLE_MEDIA_SELF_TEST_OK`、`MEDIA_PLAYERS_SELF_TEST_OK`、`MUSIC_LIFECYCLE_SELF_TEST_OK`、`NETEASE_MUSIC_SELF_TEST_OK`、`NETEASE_FAST_FRAME_OK` 通过。源码及候选身份记录于 `verification.json`。
- 新增未知进度设备帧断言首次因 JSON 节点实际为整数、测试直接按 double 读取失败；日志保留于 `media-self-test-number-fixture-failed.log`。改为按 JSON 数值反序列化，零进度及未知状态断言未放宽。
- 按隔离候选执行要求的 `dotnet run … --status-once`，退出码 2，仍被既有桌面数据目录重定向保护阻止。没有绕过该保护；此项不能标记通过。
- `git diff --check` 通过。安装前的基线和初次采样没有活动系统会话；安装后取得汽水音乐的真实播放、暂停和跳转数据，具体证据如下。

## 汽水音乐真实客户端验证

2026-10-11 15:15–15:22（Asia/Shanghai），使用隔离候选的只读媒体诊断与客户端可见界面对照，没有替换运行桥接。

| 场景 | 观察与采集结果 | 证据文件 |
| --- | --- | --- |
| 用户开始播放 | `Soda Pop`／不赶时间，播放中，150×150 封面，总时长 `150.389` 秒；12 次采样进度从 `19.1505` 连续增至 `24.8532` 秒 | `soda-playing-live.json` |
| 切歌与暂停 | 后续客户端显示 `Stay a Little Longer`／Eddie Dalton，`00:30 / 03:48`；新曲总时长 `228.2020` 秒、封面仍可采集；暂停后 12 次采样均为 `30.331066` 秒、`playing=false` | `soda-paused-live.json` |
| 跳转播放 | 点击进度条后客户端显示约 `01:30 / 03:48` 并开始播放；系统位置重置到 `90.290892` 秒，12 次候选采样从 `92.2189` 增至 `98.0794` 秒 | `soda-seek-live.json` |
| Apple Music 尝试 | 已有歌单无法播放，界面再次提示“没有有效订阅”；12 次采样没有 Apple Music 会话，选中来源仍为暂停的汽水音乐 | `apple-start-live.json` |

汽水音乐此版本通过 Windows Media Session／SMTC 提供有效时间线，浏览器播放器的原始位置可能只在状态变化时更新，候选根据其更新时间和播放倍率计算实时位置。暂停与跳转数据已验证；专辑字段在这些曲目中为空，按客户端上报保留。首次验证后将 `Stay a Little Longer` 恢复到约 30 秒并继续播放。后续 TAB5 真机验收见下一节；实际两款播放器之间切换、Apple Music 播放及 ESP8266 显示尚未验收。

## 本机部署与 TAB5 实机验收

- 用户明确授权部署后，通过资源管理器中的应用入口正常退出旧桥接，确认监听关闭及 DLL 文件锁释放。保留旧 DLL／PDB 于 `runtime-before-soda/`，核对原文件与候选身份后只替换这两个变化文件。正式运行目录为 `windows-app/AIBotBridge/bin/Release/net8.0-windows10.0.19041.0/`，版本源仍为 `0.6.4`，运行 DLL 与候选 SHA-256 一致。
- 从普通桌面入口启动成功；既有数据目录保护没有修改或绕过。`/status` 及 `/diagnostics/music` 回报真实汽水音乐元数据与有效进度，`/diagnostics/tab5` 回报原设备、`0.2.175-ui`、USB／Wi-Fi／蓝牙连接正常和封面接收确认。此前由受重定向的命令环境启动 `--status-once` 的失败记录仍保留，不将它改记为通过。
- 通过公开 Windows 媒体控制接口，仅对应用 ID `汽水音乐` 的唯一会话执行暂停、恢复与跳转；一次性控制工具保存在被忽略的任务证据目录，没有改写播放器、桥接配置、配对或设备协议。
- 实播曲目为 `Where the Rhythm Found Us`／Casset，总时长 `124.16` 秒。暂停回执位置 `29.480635` 秒；桥接首个采样仍处于轮询过渡，后续七次均固定该位置并显示暂停。用户在 TAB5 目视确认曲名、歌手、封面、“已暂停”、`00:29 / 02:04` 及停止的进度，回复“都OK”。
- 恢复播放后桥接进度从 `29.9452` 增至 `33.7972` 秒。跳转至 60 秒被客户端接受，原始位置 `60.012856` 秒；桥接随后从 `60.4156` 增至 `63.0891` 秒。用户在 TAB5 确认“播放中”、时间跳转及数字／进度条继续前进，回复“也是ok的”。这是实际设备反馈，不以桥接采样或传输 ACK 替代屏幕验收。
- 验证后恢复原曲约 29.48 秒的位置并继续播放。按项目规则再次正常退出，执行 Release 的 `--restore-cycle-default`，再正常启动。读回 `selectedMode=auto`、`cycleEnabled=true`，原六个页面及其顺序、15 秒间隔均与部署前一致。

部署和实机证据：`deployment-files.json`、`deployment.json`、`after-start.json`、`deployed-paused.json`、`deployed-playing.json`、`deployed-seek.json`、`tab5-user-acceptance.json`、`playback-restored.json`、`cycle-before-deployment.json` 和 `cycle-final.json`。无需更新 TAB5 固件；Apple Music 实播、真实跨播放器切换及 ESP8266 目视验收仍未完成。没有提交、推送、标签或公开发布。

新增回归覆盖：汽水／Apple 应用身份、暂停播放器不遮蔽另一播放来源、两者同时播放时的稳定选择、时长偏移／倍速／暂停、缺失／未来／过期／越界时间线、保留已知总时长、播放器切换后不保留旧封面，以及两个设备帧的未知进度状态。

本地回归不代替真实客户端与设备显示。汽水音乐的实际采集及 TAB5 显示已验证；Apple Music 按用户要求完成开发、暂缓订阅后的播放验收。本机运行桥接已部署，原轮播设置已复核保留，没有修改组件版本号、提交、推送、标签或公开发布。

## English

The user requested Soda Music support and then added Apple Music. Both use public Windows media sessions, the shared music snapshot, artwork and unchanged device frames. Soda's Chinese application ID and executable identity are recognized explicitly. Existing Apple Music system-session support receives additional identity and timeline coverage.

Elapsed playback is exposed only for a valid timeline; pause freezes it and unknown/invalid timelines remain unavailable while a valid total duration is retained. Independent source/track keys and whole-sample replacement prevent old-player artwork from leaking. Read-only live diagnostics now include raw fields for every system session.

Inspection of the official Soda Music 3.7.0 resource package confirms its Media Session metadata and application ID; no vendor code was copied into the product or executed. The isolated Release build and media/NetEase/artwork regressions passed. The required status-once check exited 2 at the existing desktop-profile redirection guard, which was retained.

Live Soda Music 3.7.0 sampling confirmed title, artist, 150×150 artwork, total duration, continuously advancing elapsed playback, pause and seek. Twelve playing samples of “Soda Pop” advanced from 19.1505 to 24.8532 seconds with a 150.389-second duration. A subsequent track, “Stay a Little Longer”, matched the visible client at 00:30 / 03:48; twelve paused samples all remained at 30.331066 seconds. Seeking reset the raw position to 90.290892 seconds, followed by normal elapsed advance. The client was restored near 30 seconds and left playing.

Apple Music 1.1540.23042.0 displayed an inactive-subscription banner and did not start playback or expose a media session after clicking the existing playlist. The user explicitly requested completing development while deferring live Apple playback until a subscription is available. Real cross-player switching and ESP8266 display remain unverified.

After explicit deployment authorization, the old bridge exited normally from a desktop Explorer entry. The original DLL/PDB were backed up and only these changed files were replaced; the installed DLL matches the validated candidate hash. Normal desktop startup succeeded with the profile guard retained, and live diagnostics confirmed the original TAB5 running .175 with USB/Wi-Fi/BLE connected and artwork acknowledged. The earlier status-once failure from the redirected command environment remains recorded separately.

On the physical TAB5, the user confirmed “Where the Rhythm Found Us” / Casset, artwork, pause at 00:29 / 02:04 and frozen progress. Public Windows media controls then resumed the exact Soda session and sought to 60 seconds. The running bridge advanced from 29.9452 to 33.7972 seconds before the seek, then from 60.4156 to 63.0891 seconds; the user confirmed playing state, the visible seek and continuing digits/progress. The original song position was restored near 29.48 seconds and left playing. Final normal exit, Release --restore-cycle-default and normal restart were completed; /status confirms auto mode, enabled cycling and the original six-page order at 15 seconds. No TAB5 firmware change, version bump, commit, push, tag or public release.
