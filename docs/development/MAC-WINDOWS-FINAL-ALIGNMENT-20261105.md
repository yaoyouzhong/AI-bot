# Mac 接收 Windows 最新修复：最终对齐记录

## 范围与身份

来源 Windows，接收 macOS；已快进同步 `codex/windows-dynamic-microphone-20261105` 的 `3b781c4301bb35ad30381bbf6a619c3580fa3bb4`，本分支为 `codex/tab5-claude-dialogue`。没有另写一套 Mac 实现；接收两项 Mac 音源源文件及共同 TAB5 项目菜单补丁，Windows 独有的 WinForms 布局和诊断等待不移植到 AppKit。发布负责人继续为 Windows。本次是对齐、验收与交接，未操作正式标签或公开发布。

电脑版本号保持 0.6.17，但这次 Mac 源码有变化，新编译文件是独立开发候选，不能冒充此前已发布的原包。TAB5 固定交付路径仍保留已验收 .314 `f3f98f20…`；没有用新菜单源码重编/覆盖 .314。Windows 说明中的 .315 尚无本次已接收 BIN，不填写其真机通过。

## 自动检查

| 项目 | 状态 | 本机证据与范围 |
| --- | --- | --- |
| Mac 自动音源同步 | 已完成 | `MacDoubaoMicrophone.swift` 与 Windows 提交字节一致。新增 4 组决策测试通过，覆盖明确匹配、自动默认物理端点匹配、不同/未知默认及虚拟音源。 |
| Mac 原生测试 | 已完成 | 469 项，1 项条件跳过，0 失败；29,068 parity 检查通过。 |
| TAB5 项目菜单关闭 | 已完成 | 接收补丁在独立验证副本逐字节匹配清单。Mac 原生真实 LVGL：菜单离页立即关闭、返回保持关闭且草稿/选择保留；Codex、Claude 均通过。 |
| 受影响回归 | 已完成 | `TAB5_CANCELLED_DRAFT_ONLY`、`TAB5_WORKSPACE_PARITY_ONLY` 与新 `TAB5_PROJECT_DROPDOWN_ONLY` 均通过。已查看真实渲染：总览无菜单残留、返回 Claude 后草稿保留，可读性/对齐/布局正常。 |
| 原 .314 与既有设备验收 | 已有无需修改 | 固定 BIN 哈希不变，源文件验证后恢复原 .314 摘要。原取消恢复与新草稿保护实测按原范围复用。 |
| Mac 受影响真实录音 | 待验证 | 本机只读设备清点未检测到 DJI，原桥接当前无 TAB5 USB 句柄。尚未进行新候选实际录音、停止/回填，以及不同默认音源与手动改选的实测。已请求用户接回设备后配合。 |
| 新菜单固件真机 | 待验证 | Windows 尚未在此次交接提供 .315 BIN；.314 不包含新菜单修复，不用旧 BIN 验收替代新固件。 |

真实 LVGL 用 Windows 同一源码，不改布局和交互设计；Mac 此次只改音源复用决策，无 UI 改动。没有打开、切换或置顶电脑窗口，没有录制环境声音、修改音源/配对或刷设备。原常驻程序保留。

本机证据目录：`artifacts/windows-final-alignment-20261105/`；正式打包脚本日志：`artifacts/mac-final-alignment-package.log`。候选构建结果与摘要在完成后补在下文。

## 后续接收

同一硬件条件就绪后，只补 Mac 的新音源真实录音链路，以及 Windows 提供的新菜单固件原包身份和菜单离页回归。没有扩大到无关的语音、图库、发送或安装全量重测。Windows 已报告的本机录音、照片隔离和通道接续保留其原始范围，不替代 Mac 实测。

The Mac branch fast-forwards to Windows commit 3b781c4. Native tests and the three targeted real-LVGL suites pass. The new Mac binary is a development candidate at version 0.6.17, distinct from the unchanged released archive. The accepted .314 image remains intact. Physical Mac microphone testing awaits DJI/TAB5 availability; the new dropdown firmware image has not been handed over here. No final physical acceptance or public release is claimed.

## Mac 候选结果

- 正式脚本 `scripts/package_macos.sh` 完成原生测试和 Release 构建；arm64、原包解压文件摘要、codesign strict、包内刷机工具与 helper 检查通过。
- 候选源码 SHA：`3b781c4301bb35ad30381bbf6a619c3580fa3bb4`，包内 SOURCE_SHA 完全匹配且无 dirty 标记。
- 独立交接文件：`AIBotBridge-0.6.17-development-3b781c4-macos-arm64.zip`，18946154 字节；SHA-256 `5c2023b6dc537d59a48dd8df0076b0dbab2877d95185e8de7c0778d0e0a197f7`。文件仅改为独立交接名称，内容与脚本原 ZIP 字节一致。
- 原有本机开发签名，未公证。新候选完成 3 次只读语音元数据检查，无录音、无设置写入、无前台窗口。原常驻未替换。
- 此包含新音源逻辑，是开发候选；现有正式 0.6.17 原包继续保留。不能将其写为已通过全部新录音实测。


## 真实录音补验中发现的诊断入口问题

用户已将 TAB5 与 DJI 接回 Mac，读取确认系统默认输入为 DJI、豆包为自动模式。首次按设备录音时桥接诊断进程退出，设备提示语音连接中断；该次录音失败，不记为通过。崩溃栈为 `MacDoubaoSpeech.cancel()` 访问未初始化的 `NSApp`，属于 `--tab5-automation` 绕过正常 AppKit 启动路径。麦克风选择指纹前后相同，未留下待恢复记录。

修复仅在自动验收入口初始化 `NSApplication.shared` 并设置 accessory 策略，不打开或激活窗口，不改音源、录音布局、协议或固件。用户从 TAB5 主动发起录音仍走既有豆包收音流程。新候选另行构建并绑定新摘要，旧 3b781c4 开发包保留为本次失败历史；不能套用新候选实测。
