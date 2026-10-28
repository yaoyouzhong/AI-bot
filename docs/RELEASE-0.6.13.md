# Windows / Mac 0.6.13

本次同时更新 Windows x64 与 Apple Silicon Mac 桥接程序，改善首次添加设备和安装流程。TAB5 保持 0.2.282-ui，ESP8266 保持 0.5.7；本次桥接更新不要求刷机。正常退出旧桥接后安装，保留现有设置、配对和图库。

- Windows 添加设备窗口采用紧凑对齐布局，刷新紧邻 USB 选择；已有 AI-bot 的“验证并添加”和出厂设备的“首次安装”并排显示。
- USB 刷新保留设备身份，扫描失败清除失效选择；TAB5 首装完成后根据入口返回添加设备或 USB 配对。
- Mac 新增 TAB5 后直接进入 USB 配对，包括复用已有连接窗口；普通重开保留页签，升级仍进入固件页。
- 两端安装备注和提示精简，保留备份与替换确认。按[安装指南](INSTALL.zh.md)选择已有固件、出厂首装或日常升级路线。

## 验证边界

Windows 接入选择、失败处理、窗口尺寸和缩放已通过隔离检查。Mac 导航修复通过 Release 构建、3034 项独立检查及原生窗口核验；发布提交 `031b4f0` 的 [Windows、macOS、固件 CI](https://github.com/yaoyouzhong/AI-bot/actions/runs/40632246118) 全部成功，完整 XCTest 已由 macOS CI 补验。[双平台打包](https://github.com/yaoyouzhong/AI-bot/actions/runs/40632266014) 也全部成功。本机 Mac Command Line Tools 缺少 XCTest 的失败记录及三类既有编译警告保留在 [Mac 检查记录](development/MAC-ONBOARDING-REVIEW-20261028.md)。

本次 0.6.13 安装器经完整下载及 SHA-256 核验，在已准备 .NET Desktop Runtime 的 Windows Sandbox 中安装退出 0，安装后 `--status-once` 与 `--self-test-onboarding` 均退出 0。实际窗口走通空设备中心、添加 TAB5、打开首装页和关闭返回。安装日志同时报告电脑解锁组件配置未完成，因此不计解锁功能通过，也不计无运行时自动下载链路通过。早期证据见[首次接入记录](development/FIRST-RUN-20261028.md)。

Mac ZIP 完整性、版本元数据、许可和完整哈希均通过；本次未在此 Windows 主机上运行 Mac 安装包。没有备用出厂设备，本轮不重做真实首刷、重新配对或实体屏首次显示验收。Windows 安装器未签名；Mac 仅支持 Apple Silicon、临时签名且未公证。

## English

This release updates both Windows x64 and Apple Silicon Mac bridges. Windows gets a compact Add Device form, persistent USB identity selection, stale-port clearing and caller-aware first-install completion. Mac opens USB Pairing immediately after adding TAB5, including reused windows; ordinary reopening keeps the selected tab and firmware upgrades open Firmware. Both platforms shorten setup hints while preserving backup and replacement confirmations.

TAB5 remains at 0.2.282-ui and ESP8266 at 0.5.7. No firmware update is required for these bridge changes. Exit the old bridge normally before installing and retain settings, pairing and galleries.

Windows isolated onboarding/layout checks and Mac native navigation/UI checks passed. The Mac review also passed its Release build and 3,034 parity checks. Windows, macOS (including full XCTest), firmware CI and both desktop package jobs passed for release commit `031b4f0`. Existing local Mac toolchain limitations and warnings remain recorded separately.

The exact 0.6.13 Windows installer matches its SHA-256 and installs with exit 0 in Sandbox with the desktop runtime already prepared. Installed status/onboarding checks exit 0, and the empty Device Center → Add TAB5 → First Install → close/return flow was exercised in real windows. Unlock-component setup remained incomplete; neither unlock nor automatic runtime download is counted as passed. The Mac ZIP passes full hash, archive, version and license checks, but was not executed on this Windows host. No spare factory device is available, so physical first flashing, new pairing and first display are not revalidated. Windows remains unsigned and Mac ad-hoc signed, not notarized; Intel Mac is not supported by this package.
