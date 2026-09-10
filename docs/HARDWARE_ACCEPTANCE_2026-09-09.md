# 新版切换验收：2026-09-09

## 当日第二次候选验收（A45B02B，失败后已回滚）

- 用户明确批准切换候选；固件 SHA-256 为 `A45B02B65D89D4C964636B791C81C04AE1547678A017B1A9082E4642B5360319`，桥接 DLL 为 `EDFBA9B28C6EC6E0EAFA34CB80CF14F22965D4E6EC683CB859324D7B08F305E5`。刷前逐一核对，回滚整片备份哈希一致。
- CH340 COM7 / ESP8266EX，115200 写入 515104 字节，写入哈希验证通过。
- `--test-device-pages` 退出 0：九模式、四类资源 ACK、亮度、USB 超时与恢复、原模式/亮度恢复回读均通过。股票标签资源现为 124800 字节。
- `--test-live-device-data` 退出 0：天气 26°C、6 只股票、Claude/Codex/四家国产额度及系统字段可用，音乐 false。该版本未单独断言默认桌宠/页面图标同步完成，不构成宠物形态验收。
- Wi-Fi 回退再次失败（退出 1）。前/回退/恢复的 USB 状态计数为 69/69/70，LAN 计数全部为 0；设备均有非空 IP。回退中 USB=false、bridge_online=false，恢复后 USB/online=true。证明 USB 超时/恢复正常，但不能定位 LAN 无数据是地址、鉴权、防火墙、网络隔离还是设备请求问题。
- 用户现场反馈：“字体大小、颜色、宠物形态和原来不太一样，需要对齐”。视觉验收失败，不能将测试阶段画面或数据回读当成最终展示一致。
- 已按约定写回 4194304 字节旧整片备份，`Hash of data verified`；旧桥接重新启动为 PID 55568，旧 `/status` HTTP 200。未启动新版常驻，未重置 Wi-Fi、未发布。
- 回滚后增加只读 `--audit-local-legacy-pets`：Claude 111×120 / Codex 120×120，各 6 帧的本机选中动画与旧源码默认逐像素/帧延时一致。仅证明本机缓存，不证明设备收到了这些资源，也不覆盖旧设备自选动画。
- 补强下一轮真实数据脚本：缺任一角色/图标即失败，逐项要求传输 ACK。Windows 构建通过；补强后的脚本尚未重新刷机执行。DLL 已重建，不能再将上述旧哈希视为当前 DLL。

以下是当日首次候选的历史记录。

用户确认设备在身边并连接电脑，随后明确批准暂停旧桥接、刷入新版并验收。

## 候选与回滚

- CH340 COM7，刷写握手确认 ESP8266EX。
- 固件：`firmware/.pio/build/nodemcuv2/firmware.bin`，497040 字节。
- SHA-256：`215783445A13ACBA34A464ED03A0EB1FC2CD1003FCDD6EE2FAEF34D6BCBB6A4B`。
- 桥接：昨晚 Release 目录的 AIBotBridge.exe，2026-09-08 22:32 构建。
- 仓库外原整片备份 4194304 字节，现场重算 SHA-256 与
  `7EBDD0F8F9EEBB6805111A96805D6564E66BB049E0096422D9B88682FF5C1ACD` 一致。
- 旧桥接 PID 19160 已停止；无主窗口可正常关闭，使用定向进程停止释放串口。
- 使用已安装的 PlatformIO esptool.py，以 115200 写入偏移 0；`Hash of data verified`。
  未整片擦除、未重置 Wi-Fi、未推送或发布。

## 自动验收结果

1. `--test-device-pages` 退出 0：COM7 握手、weather/stocks/quotas/domestic/system/music
   合成字段解析回读通过；四类资源 ACK 通过（WeatherText 11136、StockLabels 96000、
   TextBitmap 20416、MusicCover 25088 字节）。
2. dual/weather/stocks/quotas/domestic/system/music/pet/screensaver 九模式回读通过。
3. 亮度回读、USB 超时、USB 恢复通过；模式和亮度恢复回读通过。
4. `--test-wifi-fallback` 退出 1：Wi-Fi 回退未通过，USB 恢复通过，已解除发送暂停。
   根因尚未确认，不直接归因网络隔离或固件。该测试未验证 Wi-Fi 大资源下载。
5. `--test-live-device-data` 退出 0：实际运行数据经设备回读，天气 24°C、股票 6 只、
   Claude/Codex 与四家国产额度字段、系统数据可用。音乐 false；额度可含缓存，
   不能据此认定新授权流程、实时账号额度或音乐会话验收通过。

## 当前运行与待用户确认

新版 Release 桥接 PID 43812 已启动；本机 /status 返回 version=1、auto、
南京雨花台 24°C、6 只股票。旧桥接未同时运行。此为单次现场快照。

已请用户检查实体天气、股票、Claude/Codex 页面，以及橙色 Claude/黄色 Codex
桌宠、布局和可读性。用户明确回复：“我要的是和旧版一模一样的布局，现在不一样”。
因此本轮视觉验收失败，不能把上述通信与数据回读通过扩大为功能完整或可替换稳定版。
用户随后批准恢复旧版固件和桥接，再在隔离环境继续修正新版布局。
等待输入/完成提醒、屏保自动唤醒、图库更换、Wi-Fi 资源下载、真实额度授权和
macOS 仍须分别验证；不能用九模式 ACK 覆盖这些验收项。

## 按用户要求恢复旧版

用户批准后，重新校验上述备份 SHA-256，停止新版 PID 43812，在 COM7 以
115200 写回 4194304 字节旧整片镜像；esptool 退出 0，`Hash of data verified`。
启动旧仓库原 Release 桥接 PID 56108；/status 返回旧版结构，/weather 返回
南京雨花台、qweather。当前只有 AIClockBridge 运行，新版 AIBotBridge 未运行。
备份、新版代码和候选固件全部保留，未删除或发布。实体屏恢复情况仍以用户确认
为准。后续在新仓库隔离修正布局，不自动再次切换日常使用版本。
