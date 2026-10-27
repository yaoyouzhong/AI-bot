# Mac ESP8266 自动发现与双设备验收 · 2026-10-26

R33 当前：用户确认重置卡数字完整、数量与日期对齐、缩小后的方框合适，以及股票代码／中文字号与 Windows 一致。R33 签名桥接与 ESP8266 0.5.6 已部署；写入读回、三个递增运行时间样本、USB 握手和双串口连接通过。保持原六页、顺序、15 秒自动轮播及桌宠，TAB5 固件未变。

R33 current: the user accepted complete reset-credit digits, aligned count/date columns, the smaller badge and stock code/name sizes matching Windows. The signed R33 bridge and ESP8266 0.5.6 are deployed with verified write/read-back, three increasing-uptime samples, USB handshake and both serial ports open. The original six-page/15-second automatic cycle and pets are preserved; TAB5 firmware is unchanged.

R32 历史记录（后续重置卡反馈见 R33）：R31 的重置卡容量、天气地名、股票字号及智谱更新已获用户确认。R32／ESP8266 0.5.5 已部署，空气质量徽标与天气分区对齐 Windows，重置卡按用户偏好略缩至 14px。写入读回、启动和原六页轮播保持通过；智谱登录跨桥接重启及关闭窗口后刷新均实测通过，网页缩放已设 75%。本轮新天气布局与 14px 字体仍待人工观感反馈。

R32 historical checkpoint (superseded by R33 for reset-credit text): the user accepted R31 display fit/place names/stock font and live Zhipu update. R32/ESP8266 0.5.5 is deployed with Windows-style air/condition zones and 14px reset-credit text. Read-back, boot, original cycling, persistent sign-in, hidden-window refresh and live 75% zoom are verified; final physical appearance remains pending.

R31 跟进：用户确认地名截断、股票字号小、智谱缓存未实时更新，并补充 ESP8266 Codex 重置卡边框拥挤。已实现统一模型账号窗口和对应显示修复；构建、实际字体边界与账号失效恢复合成检查通过，部署及人工观察单独记录。详见 [R31 记录](MAC-MODEL-ACCOUNTS-20261026.md)。

R31 follow-up covers the user-reported place-name clipping, small stock text, stale Zhipu data and crowded ESP reset-credit badge. Implementation and native/geometry checks pass; deployment and visual acceptance remain separate. See the linked record.


## 续接 R29／R30：轮播、桌宠与中文资源

R28 最终包已完成部署。旧完整状态包 6783 字节超过小屏 6144 字节上限，投影后为 2066 字节；真实 USB 保持连接。前一会话已通过保留供电、使旧端口不可达的认证发现测试，并确认 Wi-Fi 回退及 USB 恢复；它不是实际修改 DHCP 地址的证据。早期失败候选及检查记录仍保留。

R29 已通过 Release、2900 项隔离、27 项窗口生命周期、16 项 HTTP 回环和 38 项发现回环检查，已签名并正常替换 R28。完整 XCTest 再次执行，因本机 Command Line Tools 缺少模块失败，未计作通过。小屏收尾执行 `--restore-cycle-default`，正常启动后 `/status` 核对自动模式、轮播开启、Codex → Claude → 天气 → 股票及 15 秒间隔一致。TAB5 未改展示设置。

轮播修复与 Windows `DisplayModes.ExpireCompletionNotice` 一致：完成提示 12 秒后不再占用 Codex 页，不修改完成序号或待处理权限。另将 PNG/GIF 导入改为正向像素行；合成非对称上下色块验证 PNG、GIF、帧顺序和时长，以及 ESP/TAB5 下发内容。

按用户要求，通过正常 Codex 导入入口选择项目已有 `docs/assets/pet/AI-bot-mascot.gif`；与 GitHub 当前文件的 blob `2ef11330a3f72ec40dffe08935db1de88c1ba0b4` 相同，沿用同目录来源说明，不复制第三方素材。六帧各 400 ms，输出 112×112。TAB5 目标／设备确认均为 `bee75b22`，小屏资源 CRC 为 `1200038283`；Claude 资源和原轮播配置未变化。传输确认不等于外观验收。

用户后续指出天气部分字倒置、股票仍缺中文。真实文字渲染函数重现：`NSBitmapImageRep` 已按顶部行读取，旧编码再次反转，导致单行字倒置、股票前几行移到 400 像素图集底部。R30 修正文字行序；已查看同函数生成的修复前后天气／股票 PNG，并加入非对称字形及全部 20 行名称位置检查。它同时覆盖共用此函数的音乐标题资源；音乐控制、Apple Events、封面处理不变。R30 最终 Release 已完成，实际签名包通过 2927 项隔离、27 项窗口生命周期、16 项 HTTP 回环及 38 项发现回环；XCTest 仍因缺模块未通过。已正常退出 R29，执行小屏 `--restore-cycle-default` 后启动唯一 R30（PID 70266），两条设备串口已打开。小屏实体复验待反馈。

同轮用户反馈“模型账号→打开官方控制台”一闪而过且没有页面。已在选中 DeepSeek 的实际 UI 复现：API-only 平台被 `console` 模式检查拦截，提示又被每秒刷新覆盖。R30 将 DeepSeek、阶跃星辰、百度千帆转到默认浏览器的官方入口，保留当前凭据及 API 额度来源，且持续显示打开结果；其余网页授权平台保持既有窗口。R30 实际账号窗口已保持“已在默认浏览器打开官方控制台”提示，Chrome 中已核对 `https://platform.deepseek.com/sign_in` 的“DeepSeek 开放平台”登录页。三个 API-only 来源模式前后相同；没有代填登录资料，另外两个平台的真实浏览器打开未在本轮逐个验收。

English: R28's bounded heartbeat and controlled stale-port discovery/Wi-Fi/USB recovery have hardware evidence; actual DHCP changes remain untested. R29 is signed and deployed after Release, 2900 isolated, 27 window, 16 HTTP and 38 discovery checks; XCTest still lacks its local module. It matches Windows' 12-second completion display expiry and fixes PNG/GIF row orientation. The user-requested project mascot matches GitHub's blob, is imported only for Codex, and TAB5 acknowledges `bee75b22`; Claude and ESP cycle configuration stay unchanged. Subsequent user feedback identifies inverted weather text and missing stock names. Production-rendered before/after images reproduce the shared bitmap-row reversal, and R30 adds upright-glyph and 20-row atlas checks. R30 also fixes the reproduced DeepSeek console button rejection/fleeting feedback, opening API-only providers' official sites without changing quota sources. R30 now passes Release and its signed executable passes 2927 isolated, 27 window, 16 HTTP and 38 discovery checks; XCTest remains unavailable. The sole R30 bridge runs with both serial ports open. The real Chrome DeepSeek official sign-in page and persistent success feedback are verified, with all three API source preferences unchanged. Other provider browsers and R30 physical display acceptance remain pending. No firmware flash or public release.

R30 部署读回显示用户当前轮播列表已变为 Codex → DeepSeek → 智谱 → 天气 → 股票 → 系统，15 秒，自动模式且轮播开启。没有把它覆盖回早期四页基线；桌宠与 R29 导入结果相同。证据位于 `artifacts/development/mac-esp-discovery-20261026/r29/` 与 `r30/`。

> 以下保留首次实现时的记录；当前进展以以上续接记录为准。

## 范围与现场

用户要求补齐 Mac 小屏自动发现，电脑地址变化后无需再插 USB，并保持 Windows 既有协议一致。没有修改 ESP8266 或 TAB5 固件。当前硬件为 ESP8266 0.5.3、TAB5 0.2.279-ui；两台已同时枚举 USB。TAB5 已确认 USB 数据、Wi-Fi 认证请求和蓝牙认证待命。

原 C-to-C 接法下小屏未亮；更换连接后已亮屏，用户反馈固定股票页且缺少中文。独占串口探测使用 460800，Python 与从生产源码提取的 Swift 配置函数均收到 version=1、device=esp8266 的 pong。未重置网络或配对。

## 修改

- Mac 与 TAB5 共用单个 UDP 18766 监听，识别既有 ESP HMAC 协议，保留 Keychain 原令牌。按实际请求来源的网段选当前有效本机地址，从该地址回复，仅公布已就绪 HTTP 端口。设备仍在取得认证状态后保存候选地址。
- 排查真实小屏握手后立即失联，发现 Mac 原 USB 心跳直接序列化共享完整快照，超过 6144 字节会退出串口发送；资源发送也依赖同一心跳，可能连带导致股票中文图片没有下发。Windows 的 DeviceStatusFrame 已排除大屏天气预报序列等非小屏显示数据。Mac 补齐独立小屏投影，保留数字、额度、显示策略和系统采样时间；完整快照及中文资源原文保留。现场修复后字节数与持续回执待记录。
- 加入连接阶段说明及受控自动发现测试：保留 USB 供电，暂时让已保存的电脑端口不可达，停止常规 USB 状态，核对认证发现、Wi-Fi 计数及 USB 恢复。结束重新下发正常 LAN 配置。此测试不同于单纯 Wi-Fi 回退，也不等同于实际变更 DHCP 地址。
- 补齐 Mac Release 的 --restore-cycle-default，仅恢复小屏自动模式和启用轮播，保留原页面、顺序、间隔，不操作 TAB5。

## 轮播对齐边界

Mac 与 Windows 的轮播目录均有 16 项：Claude、Codex、双模型额度、八个国产模型、系统、音乐、股票、天气及桌宠。支持选择页面、顺序、10/15/30/60 秒间隔。目录及设置代码对齐不等于各供应商账户、图片和真实小屏逐页验收通过。

现场 Mac 原配置为固定股票、轮播关闭，选择 codex → claude → weather → stocks，间隔 15 秒。股票停留符合该配置。收尾按仓库规则恢复自动轮播，保持这四页和原顺序、间隔；中文、实际轮播和无线重新发现仍须实测后更新。

## 软件证据与当前边界

R27 已通过 Release、2872 项隔离检查、14 项真实 loopback UDP/HTTP、27 项窗口生命周期和 16 项 HTTP 恢复检查；Windows 实际发现协议生产类的便携执行向量通过。当前 Mac Command Line Tools 缺少 XCTest，swift test 已执行但未通过，不能记作完整测试通过。证据位于 artifacts/development/mac-esp-discovery-20261026/。

R28 首次候选的验证在“原快照未修改”处中止，原因是比较了未排序 JSON 的字节顺序；该候选未运行并已保留失败材料。比较已改为排序编码，正在重新构建与验证。真实硬件结果在完成后补记，不以软件测试替代。

English: This local, unpublished change implements the existing authenticated ESP discovery protocol on the shared Mac listener, retaining pairing and firmware. Both devices now enumerate; TAB5 transport checks pass and the ESP 0.5.3 responds to isolated production-configured USB probes. The Mac heartbeat lacked Windows' bounded ESP projection, potentially ending the USB loop and preventing Chinese resource delivery. A bounded projection, a controlled stale-endpoint discovery test and ESP-only cycle restoration are being verified. The 16-page catalog matches Windows; account coverage and physical page rendering are separate acceptance boundaries. Actual DHCP change and long-term reliability are not claimed. No push, tags or public release.
