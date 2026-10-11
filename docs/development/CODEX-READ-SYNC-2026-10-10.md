# Codex 电脑已读同步到 TAB5

后续部署：2026-10-11 已安装桥接 0.6.4 与 TAB5 .168，精确镜像启动核验和电脑打开回复后角标 1→0 的真机控件读回通过，见[部署验收](../TAB5-ACCEPTANCE-168.md)。以下保留首轮源码检查的历史边界。

Deployment update: bridge 0.6.4 and TAB5 .168 were installed locally and passed exact-image boot verification and real desktop-open-to-badge-clear verification. The following source-only record is historical.

范围：程序逻辑、共享状态协议及本地验证。未提交、推送、发布或安装程序/固件；图库内容发布仍独立处理。

## 修复

- Windows 只读 Codex `electron-thread-read-state-v1`，按当前 ChatGPT 凭据的账户与用户标识及标准本地主机键选择未读集合，不修改 Codex 状态。核对本机安装包 `26.1007.2314.0` 的存储约定，代码独立实现，不迁入安装包源码。
- `codexTasks.repliesRead` 使用 `{taskId: turnId}`。完成轮次和未读状态稳定至少 5 秒后发送确认，新轮次重新观察；无法读取、过期身份、未知 schema 或账户/主机不匹配不生成确认。
- TAB5 仅清除任务和轮次都匹配的提醒，统一刷新入口右上角未读数字、回复按钮及概览。读掉部分任务仍保留剩余计数，全部读完隐藏数字；9 条以上仍显示 `9+`。
- 已读确认独立于被裁剪的任务列表；旧桥接缺少字段时兼容原行为。设备本地读过的回复不会被电脑仍未读状态重新点亮，当前不向桌面写回。

## 验证

- Windows Release 编译通过，0 警告、0 错误；使用独立输出目录，未覆盖正在使用的 Release 程序。
- `--self-test-codex-tasks`：账户、主机、过期身份、稳定等待、已读、新轮次、断线重连与账户变化通过；原有 IPC、历史读取及发送失败回归通过。验证确认保留在裁剪快照及共享协议序列化中，未发送真实 Codex 消息。
- `--self-test-codex-read-state-local`：读取实际本机当前账户成功，仅输出总数，不输出身份、线程内容或凭据。
- `--self-test-tab5-telemetry`：压缩、差分 ACK、交互期间状态刷新及重连基线回归通过。
- LVGL 原生模拟执行生产 `ui.c`，断言 3→2→0、旧轮次不清除新轮次、目录裁剪及状态缺失行为；完整模拟 `LVGL_PREVIEW_OK` 通过，截图见下方。
- ESP-IDF 完整构建 1,698 项通过，镜像大小 `0x66c360`（6,734,688 字节），应用分区剩余 7%。仅编译验证，未刷新正式 `latest` 包、未安装设备。
- `--status-once` 被既有用户目录重定向保护拒绝（exit 2），未绕过保护。因此常规启动和真机 USB/Wi-Fi/BLE 验收仍未完成。

证据目录：`artifacts/development/codex-read-sync-20261010/`，含 `codex-tests.log`、`telemetry-tests.log`、`firmware-build.log`、`ui/preview.log` 和独立桥接构建。

临时安装包解包目录的清理被自动审批策略拒绝（未提供具体原因），已保留在本机上述 `artifacts` 目录的 `codex-source/`，经 `git check-ignore` 确认不受版本控制，不作为交付/发布内容。

![电脑已读一条后仍有两条](../../artifacts/development/codex-read-sync-20261010/ui/codex-desktop-read-count-two.png)

![电脑全部已读后隐藏数字](../../artifacts/development/codex-read-sync-20261010/ui/codex-desktop-read-count-zero.png)
