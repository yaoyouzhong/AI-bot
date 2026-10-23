# TAB5 0.2.211-ui 本地验收 / Local acceptance

2026-10-17：用户授权刷入后确认“实际效果可以，翻转和触控也正常”，并要求本地提交。未授权本次推送或公开发布。

The user authorized installation, confirmed the physical layout, rotation and touch behavior, and requested local commits. No push or public release is included.

## 内容 / Changes

- 普通页面新增横屏 180° 自动翻转及方向锁定；触控随显示转向，竖放时保持横屏。
- 屏幕声音设置使用等宽双列；声音控制左侧“试听”、右侧“提示静音”，后者整体右对齐；第三行左侧屏保等待、右侧屏保类型与预览；底部为任务状态提醒。
- Automatic 180° landscape rotation and direction lock; touch follows the display, while portrait poses retain landscape.
- Equal-width settings columns; preview sound on the left and right-aligned mute on the right; screensaver delay and type/preview below, followed by task notifications.

## 镜像与源码 / Image and source

- TAB5 source commit: `987dfc7ca8652a5233a0ed214237e410bfaf2680` (`codex/tab5-landscape-flip`).
- Source checkout: `artifacts/release-068-20261016/tab5-saver-source` in the primary AI-bot workspace.
- Version: `0.2.211-ui`; application: 7,073,440 bytes.
- Application SHA-256: `ecda8b9b373502bb69fb951cca886c68059fe698f62dca348b4bf29d4fa723d9`.
- ELF SHA-256: `ad348ad17e8b34a795e1c36447d31a6366bf08e0ccdc82c6b804118d59679f87`.
- Local OTA path: `artifacts/firmware/tab5/latest/aibot_tab5.bin`.

## 验证 / Validation

固件构建、原生方向及设置测试、完整原生 UI 和实际 sidecar 预览通过。提交前核对 15 个实现/测试文件与已刷候选的源码哈希一致；另两份文档只补充验收状态。设备精确版本、ELF 指纹、OTA 分区和 VALID 启动核验通过。用户确认实际布局、翻转与触控正常。保留 TAB5 展示设置。

Firmware build, native orientation/preferences tests, full native UI and actual sidecar preview passed. Before commit, all 15 implementation/test files matched the installed candidate's source hashes; the other two documentation files only gained acceptance updates. Exact version, ELF fingerprint, OTA partition and VALID boot verification passed. The user confirmed the physical layout, rotation and touch experience. TAB5 display preferences were preserved.

Evidence remains under `artifacts/tab5-orientation-20261017`: `candidate.json`, `final-build.log`, `sidecar-preview.log`, `ota-verify.json`, `installed-ota.json` and `preview/orientation-auto-settings.png`.

未单独记录所有页面/四角触控、按住滑动延后翻转、锁定后重启、书画切换、倒置相机性能和长期稳定性；不将本次用户确认扩展成这些专项全部通过。

Every page/corner, held-swipe deferral, lock across restart, art transitions, inverted camera performance and long-term stability lack separate hardware evidence; the user's confirmation is not claimed as exhaustive acceptance.
