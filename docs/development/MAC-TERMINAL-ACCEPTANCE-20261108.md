# Mac Terminal 与 TAB5 验收交接（2026-11-08）

## 范围和源码身份

- 来源／接收：macOS 与 TAB5；Windows 无代码变更。分支 `codex/mac-native-claude-alignment-20261107`，Mac 构建源码 `38055cc86be812c27259b95bc1d8d889e0419baf`，未推送、未发布。
- 使用固定签名的 `/Applications/AIBotBridge.app`，安装前备份旧包，逐文件哈希和签名校验后后台启动，确认 USB 串口由新进程持有。用户配置未重置。
- Terminal 已打开的 Claude Code 使用 macOS 窗口输入和本机公开会话记录；没有改变跨平台协议。iTerm2、VS Code 按用户要求暂缓，不以 Terminal 结果代替它们的验收。

## 验收与剩余项

| 行为 | 结果与条件 | 剩余事项 |
| --- | --- | --- |
| Terminal 收发文字、照片、追加、中断 | 真实 TAB5 分别经 USB、Wi-Fi、蓝牙对隔离 Terminal 会话及本机模拟模型操作，发送／追加／停止获得成功回执；三种通道照片与模拟服务接收的摘要一致。无线测试排除了其他数据链路。 | 这是隔离模型实测，不代表对每个真实模型状态均已验收。蓝牙添加照片被用户观察到较慢，缺少同条件计时，尚未量化。 |
| 追加后回执状态 | Wi-Fi 和蓝牙都曾先返回未知，任务结束后原回执确认成功，未重发。 | 页面提示反复变化；另一次页面卡在“发送中”，切总览再返回恢复。根因尚未确认，不能记为修复。 |
| Codex 误显示未运行 | 发现长任务的开始时间过旧但同会话 token 活动仍持续，状态计算现使用同会话最近活动；针对长任务和已完成任务增加回归。 | 新签名包已安装、USB 已重连；设备页面实际状态仍需核对。 |
| 两个对话页签 | 隔离双客户端数据下，两页签均可显示并切换；此前单页签的测试源只提供 Claude。 | 日常桥接的实际页面状态需单独核对。 |
| Claude 完成语音与回执提示 | TAB5 独立候选实现按客户端区分语音及稳定自动核对文案；横屏离屏预览通过，用户授权后已安装，精确 ELF、VALID 分区、实时界面与原展示设置核验通过。 | 声音、纵屏和实际追加状态尚未验收；不声称解决“发送中”卡住。Tingting 本地合成声音的公开再分发权限尚未确认。 |

## 构建与证据

- macOS：`swift test --package-path mac-app` 共 495 项，4 项跳过、0 失败；`bash scripts/build_macos_app.sh` Release 包构建及签名校验通过。
- TAB5：独立工程编译通过；本地候选 `0.2.318-ui`，BIN SHA-256 `858fb5c5f3fab2e51494aad99c9b1bc270159b43a9ccfa677b5770566154ca7d`。使用 `scripts/prepare_tab5_firmware.ps1` 校验并归档，实际 sidecar 的横屏升级说明已渲染检查。用户授权后通过 USB 安装；启动核验确认精确 ELF、VALID 分区及三个实时界面样本，设置前后相同。
- TAB5 变更在 [补丁](TAB5-CLAUDE-REPLY-VOICE-20261108.patch)，相对本次已验收的独立工程开发副本。原文件混有 CRLF，应用时使用 `patch -l -p1`；[生成脚本](../../scripts/generate_tab5_claude_notice.py) 生成本地语音 PCM。候选不是正式发行物。
- 隔离验收记录位于本地忽略目录 `artifacts/mac-native-alignment-20261107/window-acceptance/day-followup/` 和 `day-interrupt/`；屏幕预览在 `artifacts/mac-native-alignment-20261107/claude-audio/ui-render/`。这些目录不进入源码或发布包。
- 后续优先定位页面卡住的状态机和网络结果；避免在无法判定送达时重发或清除草稿。继续核验设备实际语音和页面状态。没有公开发布、推送或标签授权。
