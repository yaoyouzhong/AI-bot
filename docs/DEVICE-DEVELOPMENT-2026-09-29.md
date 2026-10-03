# 设备管理开发记录 / Device management development

当前部署及验收结论见 [2026-09-30 验收清单](ACCEPTANCE-2026-09-30.md)；下文保留本开发批次当时的状态。 / See the current acceptance checklist; the original batch status below is historical.

本轮为未发布开发代码。Windows 桥接使用隔离输出构建；未替换正在运行的程序、未刷机、未推送或发布。TAB5 对端位于独立 `m5stack TAB5` 工作区，已有的其他改动保留。

This is unreleased development work. The Windows build uses a separate output directory. The running bridge and device firmware have not been replaced; nothing has been pushed or released. The corresponding TAB5 changes are in the separate `m5stack TAB5` workspace, preserving its existing changes.

## 实现范围 / Implemented scope

| 入口 / Entry | 行为 / Behavior |
| --- | --- |
| 桥接设置 → 服务状态 / Bridge Settings → Service Status | 记录本次运行的最近通信、中断和恢复次数；只重建所选设备服务；录音、RPC、升级或安装期间拒绝冲突操作。ZIP 诊断只导出允许的状态字段，不含配对、地址、设备名称、账号或原始日志。 / Session communication, disconnect and recovery history, isolated service restart and a diagnostic ZIP with explicitly allowed fields. Conflicting operations are blocked while busy. |
| TAB5 → 显示设置 / TAB5 → Display Settings | 从设备读取亮度、音量、静音、页面、轮播、顺序、屏保及设备提醒；保存必须收到设备回执。修订号冲突要求重新读取，超时显示结果未确认。 / Device readback and confirmed save, with revision conflict checks and an explicit unknown result on timeout. Requires the matching 0.2.53-ui candidate firmware. |
| 桥接设置 → 软件固件 / Bridge Settings → Software and Firmware | 手动查询正式桥接版本，打开各设备已有升级流程；TAB5 镜像检查 P4 芯片、项目、段边界、校验和、SHA-256，提供前展示版本及摘要。USB 启动核验检查设备身份、递增运行时间、UI 心跳、版本、ELF 指纹、分区和 VALID 状态。 / Manual release lookup, per-device upgrade entry, image validation and a separate USB boot verification. |
| TAB5 → 常用任务 / TAB5 → Frequent Tasks | 最多 32 个任务在 TAB5 对应项目中优先排列；不修改 Codex 侧栏。设备保存最多 32 个阅读位置，只保存任务/轮次标识、页码、滚动位置和跟随状态；每 15 秒在非手势、非 OTA 时提交脏数据，突然断电可能丢失最近未提交位置。沿用现有未读入口和断线重试，不把历史阅读记成最新回复已读。 / Up to 32 locally pinned tasks and 32 device-persisted reading bookmarks; recent uncommitted movement can be lost on power interruption. Existing unread and reconnect behavior is retained. |
| 桥接设置 → 配置迁移 / Bridge Settings → Configuration Migration | 明确支持 ESP8266 显示/轮播/屏保/天气动画、自选股票及生日。先显示逐项差异，默认全部不选；选择性恢复前保存原配置，设置写入失败时回滚生日。文件不含凭据、网络、TAB5 NVS 设置或桌宠素材；生日含姓名。 / Allowlisted export, unchecked-by-default restore preview and recovery backup. Credentials, network settings, TAB5 NVS settings and pet asset files are excluded; birthday names are included. |
| 桥接设置 → 提醒管理 / Bridge Settings → Notifications | 完成、等待操作、额度异常独立开关；本地时间勿扰，完成音开关，重复/频繁事件合并；本次运行最多 100 条历史，记录“已请求通知”而不声称 Windows 已展示。勿扰结束不补发，设备提示音独立管理。 / Per-category switches, quiet hours, completion sound, deduplication and 100 in-memory history entries. System notification delivery is not asserted. Device sounds remain separately controlled. |

ESP8266 USB 的最近通信只证明电脑成功写入，不证明设备已绘制。连接历史和提醒历史均在退出桥接后清空；常用任务与提醒设置保存在用户本机配置目录。

ESP8266 USB activity proves a host write, not rendering. Connection and notification histories are session-only; task pins and notification preferences persist in the local user profile.

## 电脑与模拟验证 / Host and simulated validation

按用户反馈收紧新增窗口：删除默认长备注，TAB5 字段对齐，单位放进下拉选项，排序按钮放在列表旁，保存操作固定底部；升级说明只在需要时展开。显示设置额外检查最小窗口下控件是否被祖先容器裁切。

Following UI feedback, the new windows omit long default notes, align TAB5 fields, include units in choices, put ordering controls beside the list and keep save actions at the bottom. Release details appear on demand. Display settings also checks control clipping at the minimum window size.

Windows:

```powershell
dotnet build windows-app/AIBotBridge/AIBotBridge.csproj -c Release -o artifacts/development/bridge
dotnet artifacts/development/bridge/AIBotBridge.dll --status-once
dotnet artifacts/development/bridge/AIBotBridge.dll --self-test-device-recovery
dotnet artifacts/development/bridge/AIBotBridge.dll --self-test-tab5-display-settings
dotnet artifacts/development/bridge/AIBotBridge.dll --self-test-upgrade
dotnet artifacts/development/bridge/AIBotBridge.dll --self-test-user-preferences
dotnet artifacts/development/bridge/AIBotBridge.dll --self-test-tab5
dotnet artifacts/development/bridge/AIBotBridge.dll --self-test-development-ui artifacts/development/ui
# 使用新的空目录，避免旧测试注册数据产生重复设备。
# Use a fresh directory to avoid duplicate registrations from an earlier test run.
dotnet artifacts/development/bridge/AIBotBridge.dll --self-test-device-center artifacts/development/device-center-new
```

TAB5 工作区 / TAB5 workspace:

```powershell
./scripts/build.ps1
./scripts/preview-ui.ps1 -Clang
./.tools/ui-preview-clang64/display-settings-test.exe
./.tools/ui-preview-clang64/reading-test.exe
```

这些检查覆盖独立重连及忙碌保护、诊断字段排除、加密 HTTP 设置回执、固件设置冲突/去重/保存失败、镜像拒绝条件、选择性配置恢复、提醒边界、书签持久化及原有 LVGL 交互回归。窗口截图覆盖 100% / 150% / 200% 控件缩放；这不替代真实多显示器 DPI 验收。

The checks cover isolated restart, busy guards, diagnostic field exclusion, encrypted HTTP settings reports, device conflicts/deduplication/write failure, firmware validation, selective restore, notification boundaries, bookmark persistence and existing LVGL interactions. Window captures cover 100/150/200 percent control scaling, not real mixed-DPI monitor acceptance.

## 真机待验收 / Hardware acceptance pending

- TAB5 在 USB、Wi-Fi、BLE 下读取和保存设置，断线/超时/本机同时编辑后的恢复；保存后重启设备读取同值。
- TAB5 长回复定位后断电重启，重新进入同任务；新回复仍保留未读提示；检查常用任务顺序。
- 分设备重连时另一台仍刷新；诊断时间与实际断线一致。
- 升级前错误镜像拒绝、正常 OTA 安装、USB 启动核验与真实页面操作；不能用版本号或校验通过替代功能验收。
- Windows 实际通知、勿扰、声音、系统通知关闭后的记录，以及真实屏幕 DPI 与窗口滚动。
- End-to-end settings over all three transports, interrupted operations, simultaneous local editing, reboot persistence, long-reply bookmarks/unread behavior, isolated reconnect, OTA plus boot verification, real display interaction and Windows notification/DPI behavior remain to be accepted on hardware.

部署验收结束后按项目规则恢复用户原轮播页面、顺序和间隔，并确认自动模式与循环展示；本轮没有部署，未改变运行中的展示配置。

After deployment acceptance, restore the user's original cycle pages, order and interval and confirm automatic cycling. This development pass does not alter the running display configuration.
