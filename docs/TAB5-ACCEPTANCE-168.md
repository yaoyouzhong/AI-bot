# TAB5 .168 / Windows bridge .6.4 本地部署验收

2026-10-11。用户授权部署电脑与 TAB5，验证电脑打开回复后右上角未读数字同步。本地部署和该主链路验证通过；未公开发布。后续新增的总览 Codex Logo 调整单独进入 .169。

## 运行版本

- 电脑桥接 `0.6.4`，从原固定 Release 目录正常启动；所有候选部署文件逐一校验 SHA-256，原运行文件已备份。运行资料仍为原 Roaming 目录，`test=False`、`pairMatchesDisk=True`。
- TAB5 `0.2.168-ui`，镜像 SHA-256 `b9a44f6c86329c23f6dca2466d681ca9ea52176bb80b7c1e9afef2923043cb00`；ELF `3ccaa4ec889af276a1a9626438e14b82769fbe8ddf4c9555d35b24ff5a235803`。
- 用户在设备点击 Wi-Fi 升级。压缩传输 4,256,653 字节，用时 10,547 ms；设备记录安装 27,603 ms，`previousError=0`。
- 正式桥接核验三次新鲜 USB 应答，完整 ELF 一致、`ota_0 VALID`、显示错误 0。最终轮播恢复随 .169 部署收尾记录。

## 电脑已读 → TAB5 数字

使用已有、已完成且桌面已加载的“安装 LiveCanvas skill”回复，先临时标为未读，再通过 Codex 正式导航入口在电脑打开该聊天；没有发送测试消息。最初选取的未加载聊天和已中断聊天不属于可同步的已完成回复，没有把其零角标当作通过。

- 09:16:36 电脑持久化状态为未读；09:16:41 TAB5 实际控件读回 `65793`，即队列 1、数字标签 1、角标可见。
- 在电脑打开该聊天后，09:16:51 持久化状态已读；09:16:58 实际控件读回 `0`，队列与数字均清零、角标隐藏。此次约 8 秒，包含防止旧确认误清新回复的稳定等待和状态传输。
- 每次同时记录设备确认时间、运行时间、界面心跳、数据年龄和显示错误。该证据为真机 LVGL 控件读回，不是模拟结果，也没有冒称拍摄到物理屏幕。测试后恢复原已读状态并返回当前聊天。

## 软件检查与限制

Release 构建、Codex 任务与已读状态自测、原生 LVGL 全回归及实际 sidecar 更新说明预览、镜像校验、ELF 显示/Flash 安全检查通过。覆盖新轮次保留、旧确认隔离、未知来源保留、重启恢复明确未读及本地已读不反复弹回。

工具终端的 `--status-once` 因资料目录保护拒绝启动，未绕过保护；正常资源管理器启动后的真实 `/status` 和设备诊断用于运行验证。独立鉴权 HTTP 探针返回 401，未作为验收证据；未读测试最终使用正式运行桥接、桌面持久化状态与真实 USB 控件读回。没有重跑完整三通道吞吐或长期稳定性验收。

证据：`artifacts/development/upgrade-168-20261011/` 下的构建、自测、`boot-verified-diagnostics.json`、`mark-unread-transition.json`、`actual-open-read-transition.json` 及 `ui/`。

## English

Bridge 0.6.4 and TAB5 0.2.168-ui were deployed locally with authorization. Exact ELF / ota_0 VALID verification passed. An existing completed reply was temporarily marked unread, then opened in the desktop app; real LVGL badge readback changed from visible 1 to hidden 0 in approximately eight seconds. No message was sent and no public release was made. The subsequent overview-logo change is tracked under .169.
