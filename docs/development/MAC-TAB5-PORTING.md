# Mac TAB5 port / 开发与集中联调

2026-10-19：用户授权完成五阶段剩余开发、每模块 Mac CI 与必要修复，持续推送 `codex/mac-tab5-phase1-ci`。不合并 main，不部署、不创建标签或发布。真机联调安排在次日白天，当前仅开发与 CI。

The user authorized remaining development and per-module Mac CI on the test branch. No merge, deployment, tags or release. Physical acceptance is deferred to the following daytime session.

| 阶段 / Stage | 模块 / Module | 状态 / State |
| --- | --- | --- |
| 1 可行性 | USB、协议互验、Mac 构建基础 | 已通过首轮 55 测试及 Release；Codex/语音路径随交互模块继续验证 |
| 2 日常可用 | USB/Wi-Fi、日常数据、显示设置 | 源码与 CI 通过；真机待联调 |
| 3 无线与资源 | CoreBluetooth、封面/桌宠/图库、OTA | 开发中，逐模块 CI |
| 4 交互 | 电脑/音乐控制、Codex、照片、语音 | 待开发与 CI |
| 5 完整交付 | 首刷、备份恢复、候选安装包、权限引导 | 待开发与 CI；不发布 |

UI plan: keep native AppKit controls, system semantic background/label/separator/accent colors and system font sizes 23/15/13/12. Left-aligned connection status remains above USB/Wi-Fi setup. Add explicit Bluetooth and feature opt-ins, then separate resource/firmware actions; use scroll views for long settings. Match the existing Mac menu-bar utility, without adding a web dashboard or decorative animation. CI UI snapshots and physical permission/interaction checks have separate evidence.

协议保持 v1；每种通道共用配对身份、认证、重放校验、事务和结果。CI 只能证明该提交的编译与自动化测试；不能证明真实蓝牙吞吐、屏幕显示、权限或 OTA 启动。缺失的供应商/平台能力不伪造、不通告。
