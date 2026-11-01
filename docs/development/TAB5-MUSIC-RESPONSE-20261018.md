# TAB5 音乐控制状态回读延迟

用户报告：上一首、下一首、播放/暂停点击后电脑立即执行，TAB5 按钮状态约 2–3 秒后才更新。

## 定位与修改

当前已安装 `.225` 的 `ui_computer.h` 中，`computer_action()` 为所有控制操作设置 `computer_message_until=now+3500`。`computer_refresh()` 正常每 2000 ms 查询一次，且受上述等待阻挡。播放器接受命令后可能尚未更新其系统播放状态，此时命令回复携带旧状态，TAB5 后续查询又被延后。这是源码确认的延迟来源；未把用户现场具体每一毫秒归因于它。

修改仅作用于现有音乐页：媒体操作接受后不设置 3500 ms 的通用操作提示等待，随后 5 秒内以 500 ms 间隔读取现有 `status`。没有新增控制命令、RPC 字段或桥接组件。保留忙状态检查、每次一个在途请求；离开音乐页或断开连接时清除加速窗口，窗口结束恢复 2000 ms。命令回复仍立即显示，后续按钮依据实际 `playing` 回报，不做乐观翻转，也不自动重发上一首、下一首或播放/暂停。

500 ms 是查询间隔，不是端到端延迟承诺；播放器发布状态、桥接处理与 USB/BLE/Wi-Fi 传输耗时仍存在。曲名和封面继续沿用现有音乐遥测，不宣称本次改变了其刷新频率。

## 验证与交付边界

修改位于 `artifacts/development/music-control-response-20261018/source-snapshot/ui_computer.h`，基于此前未部署的 `.226` 无线启动核验候选，保留其功能。配套桥接复用 `artifacts/development/wireless-boot-verification-20261018/bridge/`，本次未改 Windows 代码。

实际 LVGL 源码模拟验证三个动作：499 ms 不补读、500 ms 补读；收到状态前不翻转图标；真实回报使图标变化；忙时不并发；后续只读不重发命令；5 秒后恢复普通周期；离页清理。原 USB/BLE 一键解锁、成功关闭与状态轮询回归通过。固件构建日志与源恢复记录同目录保存。

当前真机仍为已验收 `.225`。此修改尚未部署，实际点击到按钮更新耗时需刷入后验收。未安装 Wi-Fi 解锁组件。

## English

The installed firmware delays ordinary controller status polls for 3.5 seconds after every action. A media command can return before the player publishes its updated state, leaving the old icon until that hold expires. The candidate removes the generic hold for Music actions and polls read-only state every 500 ms for five seconds, with one request in flight, no command replay, no optimistic state change and normal polling afterward. Track metadata and artwork telemetry remain unchanged. Actual LVGL regression checks cover all three media actions and existing USB/BLE unlock behavior. Hardware remains on the accepted .225 release; end-to-end latency awaits deployment testing.
