# 媒体播放器显示 / Media playback display

本次媒体接入纳入桥接 0.6.3 的发布准备，尚未公开。复用现有音乐页面和协议字段，ESP8266 / TAB5 无需为媒体来源扩展刷机。2026-10-08 完成候选代码、合成数据与隔离构建检查；2026-10-09 使用 QQ 音乐 `22.71.10.11.55` 实际采样确认系统／原生曲目、播放状态、推进的进度和公开封面。暂停、拖动、切歌及设备显示仍待交互验收。

## 接入范围

| 来源 | 接入方式 | 数据与限制 |
| --- | --- | --- |
| QQ 音乐 Windows 客户端 | 原生只读采集；有 SMTC 时匹配同曲数据 | 曲名、歌手、专辑、当前／总时长、播放／暂停及公开封面；无需注入插件。原生匹配参考的 x86 `QQMusic.dll` 结构，未知版本或架构回退系统接口。`22.71.10.11.55` 已确认实际播放采样；暂停／拖动／切歌及设备显示待验。 |
| 网易云音乐 | 保留原生采集与系统接口 | 保留真实时间、中文别名与专辑；本次回归不替代既有版本的实际兼容性记录。 |
| Spotify、Apple Music、Windows 媒体播放器及其他桌面客户端 | Windows SMTC | 自动发现所有上报的媒体会话。具体字段、封面和时间线由客户端提供；没有上报的内容保持未知。应用名称识别不等于该应用所有版本均已验收。 |
| 酷狗、酷我、VLC、PotPlayer、foobar2000 | Windows SMTC（客户端有上报时） | 同一通用接入路径；需要客户端本身或其已启用组件提供媒体会话。本次没有为这些应用实现内存扫描，也不宣称无条件兼容。 |
| Chrome / Edge 的网页音乐、视频 | SMTC；可选媒体助手补充 HTML Media 状态 | 用户确认 Chrome 网页播放此前一直正常，保留该已验基线。助手只处理用户逐个启用的网站；0.2.0 新增进度、暂停、倍速和标签页失效处理另行验收，不把既有正常播放重新列为未知。直播保持进度未知。Edge 及新增网站／能力未逐项实测。 |
| Firefox / Brave / Opera 等浏览器 | Windows SMTC（有上报时） | 通用系统会话接入；本项目现有媒体助手面向 Chrome / Edge。 |

多个来源同时打开时，先选正在播放的来源；同为播放中时优先 Windows 当前媒体会话，否则保留此前来源。都暂停时保留可用的当前／原来源。切换时整组替换曲名、歌手、专辑、时间和封面，不把上一首或另一个播放器的数据混入当前歌曲。

QQ 封面下载在后台进行；曲目变化会放弃旧封面，下载失败不阻塞曲名和进度。只读取当前用户会话中的 QQMusic 进程及有限的当前歌曲字段，架构、签名、指针、长度或歌曲身份不匹配时停止原生采集。封面仅允许腾讯公开图像域名，使用 HTTPS、大小限制与独立取消；不读取账号文件或注入 DLL。

网页助手沿用既有本机鉴权及逐网站授权，无需扩大权限。当前播放状态只保存在桥接内存中；标签页结束或三秒没有新数据后失效。封面按网站／标签页与曲目隔离。同页同时播放多个媒体元素时，不猜测哪个进度对应标题。不会读取浏览历史、Cookie 或账号密码，也不控制播放。

## 使用与验收

桌面播放器播放后，桥接按现有设置采集。若某客户端不向 Windows 上报媒体会话，应先检查其媒体集成设置；通用接口不能凭空取得缺失数据。Chrome／Edge 的可选助手按 [browser-companion 说明](../browser-companion/README.md)加载并启用目标网站；已经加载旧助手时需重新加载扩展和网页才能使用 0.2.0 的播放状态接口。

本地开发检查：

```powershell
dotnet build windows-app/AIBotBridge/AIBotBridge.csproj -c Release
dotnet run --project windows-app/AIBotBridge/AIBotBridge.csproj -- --self-test-media-players
node browser-companion/capture.test.cjs
```

实际验收逐项检查：播放／暂停、切歌、拖动进度、不同播放器切换、封面延迟到达、关闭播放器以及两台设备显示；网页另检查倍速、直播、标签页关闭和多个标签页。这份媒体采样记录本身不包含其他播放器安装、系统媒体设置修改或设备部署；后续桥接／TAB5 部署和发布状态见[本次发布记录](RELEASE-0.6.3.md)。

安装播放器后，可使用候选 DLL 的 `--test-media-live <本地输出.json>` 保存 12 次当前来源和歌曲采样。该命令只读客户端，不启动桥接、不占用设备串口、不修改设置；记录只保存在指定本地文件，含当前曲目，不能冒作设备验收。独立诊断进程不接收网页助手心跳，网页验收需在候选桥接中完成。常规 `--status-once` 在当前 Codex 启动环境被既有数据目录重定向保护阻止；本次没有修改或绕过该保护。

## English

The media integration is included in bridge 0.6.3 release preparation and is not yet public. The existing music UI and protocol remain compatible; extending media sources alone does not require a firmware update.

On 2026-10-09, QQ Music `22.71.10.11.55` x86 was sampled live. Both system
metadata/timeline and bounded native current-song reads worked; twelve native
samples advanced with playback, and public artwork reached 500×500. The locator's
interleaved string-initializer checks were corrected. Interactive pause, seek,
track switching and real ESP8266/TAB5 rendering remain pending.

Local Windows development only; publication remains paused. QQ Music adds a bounded read-only native adapter for the referenced x86 layout, with exact-song SMTC merging and asynchronous public artwork. Unknown layouts retain system-session fallback. NetEase support remains available. The bridge now enumerates all public Windows media sessions and chooses actual playing sources, retaining the system-current or previous source when multiple candidates remain. Spotify, Apple Music, Windows Media Player and other clients can use this route only when they publish SMTC data; recognition of a client name is not live compatibility acceptance. KuGou, Kuwo, VLC, PotPlayer and foobar2000 receive the same conditional system-interface support, without new native memory scanners.

The user confirms that existing Chrome website playback has consistently worked; retain that accepted baseline. New companion 0.2.0 behavior and Edge remain separate acceptance items. The optional Chrome/Edge companion supplies current Media Session metadata and real HTML media playback, position, rate and artwork for explicitly enabled websites. Ended or stale tabs expire; ambiguous simultaneous media are not assigned a guessed timeline. Sources/artwork remain separated and memory-only. Existing authenticated loopback/site permissions are retained. Live streams without a finite duration stay unknown. Other browsers use SMTC when available.

Synthetic checks cover selection, native structure bounds, pause/seek/transition behavior, stale data, artwork ownership, browser timestamps/rate/end and unchanged device frames. QQ Music has real playback sampling as recorded above; its interactive behavior and dual-device rendering, and other uninstalled clients, still require acceptance. The user installed QQ Music and started the candidate bridge manually; no agent installation, automatic bridge replacement/restart, flash, commit, tag or publication was performed.

`--test-media-live <local-output.json>` captures 12 read-only client observations
without starting a bridge, claiming serial ports or changing settings. The local
file includes the current track and is diagnostic evidence only; the standalone
process cannot receive companion heartbeats. Website acceptance needs the
candidate bridge. The existing profile-redirection guard blocks `--status-once`
under this Codex launch environment; that protection remains intact.
