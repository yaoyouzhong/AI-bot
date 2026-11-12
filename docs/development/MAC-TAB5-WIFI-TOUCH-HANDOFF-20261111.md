# Mac 对齐 TAB5 Wi-Fi 触控与升级核验

## 范围与接续

Windows → macOS。交接分支为 `origin/codex/tab5-status-reliability`，以包含本文的提交为交接点，Windows 基线为 `d9150b18012bfaf734930c032b680a1c6843eeff`。Mac 拉取后先检查本地未合入改动，保留已完成的 Wi-Fi 状态回包优化，再对齐触控和自动核验。TAB5 协议版本仍为 `0.2.320-ui`，桥接发布版本待定。

```sh
git fetch origin codex/tab5-status-reliability
git log -1 --oneline FETCH_HEAD
```

现有自动验收入口增加 Wi-Fi 截图、点击和滑动，沿用设备报告的逻辑宽高、方向和触控区域。`wifi-test` 从认证 USB 临时切到 Wi-Fi，`restore`、`stop` 和输入结束都恢复原连接模式。连续 8 秒收不到新鲜 Wi-Fi 状态、绑定变更或达到 120 秒上限时，固件独立恢复。临时模式只写内存。协议和操作见 [TAB5 协议](../PROTOCOL.md) 与[自动验收设计](TAB5-AUTOMATION-DESIGN-20261104.md)。

## 对齐清单

| 项目 | Windows 实现 | Mac 接续 |
| --- | --- | --- |
| 认证与路由 | `Tab5Service` 和 `Tab5UIAutomation` 绑定状态轮询、配对归属、请求 ID 和通道。先验现有 GET，再验 `X-Tab5-UI-Automation-Proof`。双网卡时使用认证设备提供的地址选择可达本机接口。 | **待验证。** 在 `MacTab5Service` 的已认证状态回包路径接入同一报文，保留状态发布优化。确认 Mac 在有多个接口时向 TAB5 提供可达的 LAN 地址。 |
| 操作与恢复 | `Tab5AutomationRunner` 在已有 `--tab5-automation` 入口执行 `probe`、`tap`、`swipe`、`capture`、`wifi-test`、`restore` 和 `stop`。核对每次回执的实际通道，超时后不重发手势。 | **待验证。** 扩展 `MacTab5UIAutomation` 和 `MacTab5AutomationRunner`，在临时模式使用 Wi-Fi 截图与手势，恢复后用 USB 回执确认。保留旧固件的 USB 路径。 |
| 固件 | **320-R2 已完成 Windows 真机验收。** 固件负责临时模式、手势取消与超时恢复。源码见 [320-R2 补丁](TAB5-320-R2-WIFI-TOUCH-20261111.patch)。 | **已有无需修改。** 使用已核对指纹的 320-R2 或 320-R3 验收 Mac，记录实际使用的镜像。 |
| 自动启动核验 | **已完成 Windows 真机验收。** `Tab5UpgradeVerification` 对身份匹配、启动自检未完成的分区有界等待。镜像或分区不匹配立即拒绝，读取暂时失败可重试。 | **待验证。** 核对 `MacTab5Firmware.swift`、`MacTab5USB.swift` 已有实现；只补仍缺失的等待、回连或错误判断。 |
| 总览日期和天气 | **已完成固件实现及离屏回归。** 日期与星期、农历与节气分别同排。天气分隔线对齐左侧卡片上沿，温度和湿度同排，整组垂直居中。 | **无需修改 Mac 桥接。** 两端已有 `weather.humidity`。固件只在湿度为 0–100 的数值时显示。 |

## Windows 构建与实机证据

- 已安装桥接 DLL SHA-256 `af85644cbc3203323d2e499e4b0cd15270c74e663a0b6abec02ad8fb428e0f03`。Release 构建、`--status-once`、TAB5 自动化及通用自测通过。
- 320-R2 镜像 7,054,208 字节，SHA-256 `22dcabb6c4a4106fdaf87958635c702c9b3f0d6836ab82f895a7b3ada4c001c0`。从 TAB5 读回 ELF SHA-256 `0b59c4330dacfc4709b1559803e35e3bfd0c7b5f9bfcd5edc4247eb70e7e5bda`，`ota_1` 为 `VALID`。
- 2026-11-11 实机日志 `artifacts/t5-320-r2/physical-222548.log`。USB 保持供电及连接，临时模式确认生效后，无线点击进入「额度」，无线滑动进入「应用」，截图证实切页。总览显示 `项目 · AI-bot`，项目名旁没有页码。第二轮保持仅 Wi-Fi 至少 45 秒，仍可读取总览。
- 主动结束已收到 USB 恢复回执。第二轮直接结束测试桥接进程来中断 Wi-Fi 状态回包，等待 12 秒后重连，首个认证探测走 USB。测试结束检查设置与模式，日常桥接已恢复 USB 和 Wi-Fi。此轮未做物理拔线或长时间 Wi-Fi 稳定性验收。

## 总览断联补测

- `artifacts/t5-320-r2/overview-link-224000.log` 记录了分段仅 Wi-Fi 采样。首尾截图相隔 9 分 16 秒，34 张截图均在总览，窗口间恢复 USB 再续开。同期 252 次认证状态请求的最大间隔为 2.402 秒。标准触控诊断未启动系统监控采样，因此该轮只证明回包稳定，长测在诊断进程的总时限处结束。
- `artifacts/t5-320-r2/overview-outage-225439.log` 记录了从仅 Wi-Fi 主动停止测试桥接后的真实离线。设备仍连着热点，七个只读 USB 样本报告 `Offline`，总览仍可见。恢复后，USB 和 Wi-Fi 的真机截图均无断联提示；设备原模式、展示设置与日常桥接已恢复。
- 同源离屏回归通过，包括断联时显示、监控过期区分和重连清除。带系统监控采样的独立 Windows 诊断未建立认证 Wi-Fi 回包，内置截图也无法在桥接离线期间取图。系统监控开启的实屏结果见下方的日常桥接补测。

2026-11-12 已用日常 Windows 桥接完成总览专项真机验收。TAB5 设置为「仅 Wi-Fi」，USB 继续供电并提供只读诊断。桥接停止后，设备仍连接热点，9 个样本均报告 `Offline`，界面仍在总览。用户确认「桥接连接已中断」出现，恢复后自动消失。恢复后约 5 分钟未观察到误报，同期 133 次认证状态请求最长间隔 4.456 秒。一次电脑端详细诊断超时后，使用设备认证请求日志和末尾监控读回补齐观察。证据为 `artifacts/t5-320-r2/resident-overview-retest-20261112.log` 与 `resident-overview-link-metrics-20261112.json`。

## 升级后自动核验

2026-11-12 Windows 在原连接模式「自动选择」下，通过 Wi-Fi 分块压缩重装同一份 320-R2 镜像。日常桥接 DLL SHA-256 为 `ebed8678a706edeb3b5fccef11635a02ec199aabf7ffe9dab753cd29cdaf85dd`。从原 `ota_0` 启动到 `ota_1`，自动核验确认同一 ELF 指纹且分区为 `VALID`。测试只请求重装并轮询状态，未调用手动核验。USB 与 Wi-Fi 已读回在线。证据为 `artifacts/t5-320-r2/automatic-upgrade-proof-20261112.json`。

首次自动核验在 `PENDING_VERIFY` 时耗尽重试。独立 USB 读回随后证实固件完成 30 秒健康自检并进入 `VALID`。修复后，当前镜像、设备和分区身份匹配且仍处于启动自检时继续等待，直到 `VALID`、确定不匹配或达到等待上限。最终重装已通过实机自动核验。验收入口的详细诊断请求曾超时，后续只读查询确认了自动核验结果。该次全量显示设置比较未执行到末尾，连接模式已在重装前恢复，末尾读回总览与亮度 79%。

## 320-R3 总览固件

从 0.2.319-ui R8 源码依次应用 [320-R1](TAB5-320-R1-OVERVIEW-LINK-20261111.patch)、[320-R2](TAB5-320-R2-WIFI-TOUCH-20261111.patch) 和 [320-R3](TAB5-320-R3-OVERVIEW-CALENDAR-WEATHER-20261112.patch) 的增量。R3 补丁已在 R2 源码上检查并应用，应用结果与构建目录中的变更逐文件一致。公共版本保持 `0.2.320-ui`，内部标记为 `R3`。Codex 额度卡按用户确认保留原样。

1280×720 离屏预览验证了垂直留白、低温与 100% 湿度同排、无有效湿度的显示和节假日配色。天气内容到分隔线约 40 px，到卡片底边约 42 px。本地预览位于 `artifacts/t5-320-r3-centered/render/`。

Windows 已完成 ESP-IDF 构建与生产升级包校验。镜像 `aibot_tab5.bin` 为 7,054,480 字节，SHA-256 `744ac758c27a5963eac2ea031736531e2f10975f20c9ec55fe8061948fd958b4`，ELF SHA-256 `2a893d70af823105f7e374a43447e928c5b17c749ff1edfc8d952eae03a742eb`。原包归档于 Windows 的 `artifacts/firmware/tab5/versions/0.2.320-ui-r3/744ac758c27a-f80fbe60/`。Git 交接包含源码增量，补丁 SHA-256 `15cce88246c465dbd29d2f5b8f7d3303763b6a8ef3ab2e8885933f721ade857b`。Mac 若需要独立验收布局，请取得这份原包并核对指纹。

2026-11-12 10:22（北京时间）已通过 Windows 常驻桥接将本机 TAB5 从 R2 升到 R3。Wi-Fi 分块压缩传输 5,122,448 字节，重启后自动核验 R3 ELF 和 `ota_0` 的 `VALID` 状态。USB、Wi-Fi 均恢复在线，升级前后的设备展示设置读回一致。证据在 Windows 的 `artifacts/r3fw/local-install.json`。实屏观感由用户在 TAB5 总览确认，Mac 端原生验收待完成。

## Mac 验收

1. 补认证、错误归属、错误通道、重复回执、旧固件和正常结束用例。在 Mac 运行 `cd mac-app && swift test && swift build -c release`。
2. 配对已核验的 320-R2 或 320-R3，保持 USB 供电。从 USB 进入临时仅 Wi-Fi，使用真实截图确认点击和滑动改变页面。分别验证主动退出和中止状态回包后的超时恢复，读回 USB 和原设置。Mac 自己记录实际候选摘要及实机证据。
3. 复用 `MacTab5Firmware.swift` 与 `MacTab5USB.swift` 中现有的自动启动核验。覆盖同版本重装后的暂时断链、`PENDING_VERIFY` 到 `VALID` 的转换，以及不同镜像指纹的拒绝。以原生构建和 Mac 配对真机确认一次后台自动完成，验收过程仅观察状态，不点手动核验。
4. 在仅 Wi-Fi 的总览验证桥接中断提示、恢复后自动消失和正常运行无误报。结束后恢复原连接模式并读回设置；保留 USB 供电即可。
