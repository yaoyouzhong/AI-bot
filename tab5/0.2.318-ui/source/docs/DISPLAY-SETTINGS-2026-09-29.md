# 显示设置与阅读位置候选

配套 Windows 桥接在相邻 AI-bot 仓库；本次固件 `PROJECT_VER=0.2.53-ui`，未刷机或发布。

- 接收加密快照 `connectionHealth.displayCommand`，LVGL 线程读取/保存亮度、声音、轮播、屏保与提醒；内容修订号校验防止覆盖其他位置刚修改的值。
- 同一请求只保存一次；回执通过 USB/BLE RPC 或 Wi-Fi 加密 `POST /tab5/v1/rpc` 返回。错误、过期和保存失败不显示成功。
- 阅读书签沿用已有恢复逻辑，增加 `reading` NVS blob；最多 32 个任务，保存轮次、页码、滚动位置和跟随状态，不保存正文。每 15 秒在无手势、无 OTA 时提交更改，掉电可能丢失最近未提交位置。
- 常用任务由桥接排序，设备沿用对应项目的列表顺序；未读与断线恢复逻辑保留。

协议细节位于 AI-bot `docs/TAB5.md`，开发与真机待验收列表位于 `docs/DEVICE-DEVELOPMENT-2026-09-29.md`。

已进行固件构建、完整 LVGL preview 回归、`display-settings-test` 和使用实际持久化代码的 `reading-test`。电脑侧合成 HTTP 回执测试通过。上述证据不能代替 USB/Wi-Fi/BLE 真机设置、断电书签恢复和升级验收。本轮保留该工作区之前已有的其他改动。
