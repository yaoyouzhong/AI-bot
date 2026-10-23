# TAB5 computer control local candidate — 2026-10-17

## Result

Continued the unfinished control-app work in chat `重写产品介绍`. Applied the user's final correction: playback controls belong in Music; the Windows configuration description is exactly `快捷启动项（最多4项）`.

- TAB5 Control: PC master volume, mute, lock and up to four locally configured application/folder shortcuts. Music: previous, play/pause and next, enabled only for supported operations.
- Existing 212 clock/header/countdown navigation and music idle changes retained.
- Windows configuration is reachable from the TAB5 device card/menu. Paths stay local; the device sends only opaque IDs. Unsaved edits are protected on close.
- Paired encrypted RPC, timestamp/session validation, 120-second/256-entry duplicate receipts and no automatic side-effect retries. Music commands recheck that the current GSMTC source/title/artist match the displayed music source. Old bridges and Mac do not advertise support and remain disabled.

## Source locations

- Windows: `C:/Users/yaououzhong/.codex/worktrees/release-067-205/AI-bot`.
- TAB5: `C:/Users/yaououzhong/Work/AI-bot/artifacts/release-068-20261016/tab5-saver-source`.
- Both retain uncommitted candidate changes. No commit, push, tag, public release or deployment performed. The primary checkout's other pending work was not overwritten.

## Validation

- Windows Release build: success, zero warnings/errors.
- `--self-test-computer-control`: passed with fake OS actions through the actual encrypted RPC entry point; invalid authentication/session/time, parameter rejection, duplicate/concurrent requests, conflicting requests, capacity, missing capabilities, persistence and unsaved edits covered.
- Existing `--self-test-media-players`: passed after music source tracking changed.
- Required `dotnet run --project windows-app/AIBotBridge/AIBotBridge.csproj -- --status-once`: initially exited 2 under the Codex packaged launch context because AppProfileGuard detected redirected data folders. Running the same command through an existing normal Explorer window exited 0 and produced a valid status snapshot. The guard was preserved; no application data was moved or reset. Evidence: `status-once-explorer.exit` and private local `status-once-explorer.log` in this artifact directory.
- WinForms previews at 100%, 150%, 200% simulated font/geometry scaling were generated. The final description was visually checked at 100%. This is not an actual system-DPI-switch acceptance test.
- TAB5 official `scripts/build.ps1`: passed, eight physical-core budget and BelowNormal priority retained. Existing SDK/BSP/unused-helper warnings remain.
- Full native UI suite: `COMPUTER_CONTROL_UI_OK`, `SWIPE_PREWARM_FAIR_OK`, `MUSIC_IDLE_OK`, `LVGL_PREVIEW_OK`. Volume sends on release, playback capability gating, offline/legacy disablement and return navigation covered. Existing long music metadata assertions retained.
- Actual firmware notes: `OTA_ACTUAL_SIDECAR_PREVIEW_OK`; top and scrolled previews generated. `git diff --check` passed in both working trees.

## Local firmware

- Version: **0.2.213-ui**, 7,082,240 bytes.
- Canonical image: `C:/Users/yaououzhong/Work/AI-bot/artifacts/firmware/tab5/latest/aibot_tab5.bin`.
- SHA-256: `3c9f181e397a861b9610d4515fb62a5667812f4f3cda78815dd1811138dbd23e`.
- ELF SHA-256: `479b7303a4da00ad5b600af7e3a685863b3787680d79ba33999aa41c639870a0`.
- Prepared using `scripts/prepare_tab5_firmware.ps1`; previous candidates retained in versions.

## Remaining physical acceptance

The new Windows build and firmware are not installed. Actual TAB5 touch-to-PC volume/mute, lock, application/folder launch, media control and USB/Wi-Fi/BLE behavior have not been tested on hardware. Fake backend assertions and native previews do not establish physical acceptance. Existing bridge/device display settings remain unchanged.


## 后续 UI 修订

2026-10-17 控制页继续修订，加入启动图标和静音显示；当前 .213 本地候选 SHA-256 为 `ca0f9413492938989770f9b9f7f9cba68456e8e8bc018cdbacd30abcb9ffc284`。本文件上方哈希描述较早草稿。最新证据见 `C:\Users\yaououzhong\Work\AI-bot\artifacts\tab5-control-layout-20261017/REPORT.md`，仍未部署或真机验收。

后续音量数字和静音图标放大，用户选择扬声器加斜杠图标。当前同版本本地候选 SHA-256：`6c014c9b8100a9c24830e7423e4edd6ebbd09793a63c7b948a408aa975677460`。最新报告仍为 `C:\Users\yaououzhong\Work\AI-bot\artifacts\tab5-control-layout-20261017/REPORT.md`；未部署。


后续部署与现场验收已完成，最终版本 0.2.215-ui；213 曾蓝屏回退，214 修复内存，215 完成锁屏状态与闪动修复。最新结果见 `C:\Users\yaououzhong\Work\AI-bot\artifacts\acceptance-068-213-20261017\ACCEPTANCE.md`，上文较早构建/未部署状态为历史记录。
