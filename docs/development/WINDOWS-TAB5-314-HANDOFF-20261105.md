# Windows 接收 TAB5 .314 草稿恢复修复

## 可直接转发

请拉取远端 `origin/codex/tab5-claude-dialogue`，确认包含功能修复提交 `22294c587a9926ac829a76895e83fd696c5d9a07`。本次修复 TAB5 取消已确认追加消息后，文字和照片未自动恢复到输入框的问题。固件请使用 [GitHub 交接草稿](https://github.com/yaoyouzhong/AI-bot/releases/tag/untagged-b2a5657846854e706504) 中的 **`AI-bot-TAB5-0.2.314-ui-f3f98f20-accepted-ota.zip`** 原包及同名 `.sha256`；应用版本为 **0.2.314-ui**，应用 SHA-256 以 **`f3f98f20`** 开头。**桥接继续使用现有 0.6.17 原包，不需要重编、重装或升号。** Mac 已通过实际渲染检查、精确镜像/VALID/三个实时样本启动核验，以及“取消后恢复文字和照片”“保留后来输入的新草稿”两项合成会话真机自动验收，展示设置已保留。发布交付直接使用这份已验收 .314 BIN，不重新构建后套用旧验收；.313 原包和旧失败记录仅保留作历史。不受影响的既有验收继续复用。Windows 仍统一负责后续合并与发布。

## 接收核对

- 来源 / 接收：macOS / Windows；发布负责人沿用 Windows。
- 分支：`codex/tab5-claude-dialogue`；功能修复提交：`22294c587a9926ac829a76895e83fd696c5d9a07`，已在远端。
- 本次授权：修复、自动验收、交接附件与文档、提交和推送；本交接不新增正式标签或公开发布授权。
- 版本范围：只更新 TAB5 至 `0.2.314-ui`；Windows / Mac 桥接保持 `0.6.17`，ESP8266 无变化，RPC 字段无变化。
- OTA ZIP：5,270,970 字节；SHA-256 `0cd09f5ea3999903e387b3e7b333bf8c94c653e9d7ea5e384e0d0e82921f35cd`。
- 包内 `aibot_tab5.bin`：6,924,000 字节；SHA-256 `f3f98f2019c46ac9c49be3b60fe7f550b1b03ee0c5732e044796c53bbc21dd99`。
- ELF SHA-256：`eace7cc46df3bef73f9dc27d49f4c3df04a5d4ceb169293c66984d4abe27b3a1`。
- 使用范围：已有 AI-bot 的 TAB5 升级包，非首刷包。包含匹配 sidecar、manifest、源码补丁、许可及全部文件摘要；附件服务端摘要已核对。
- 源码与设计、两端实现位置、详细证据：[修复与验收交接](TAB5-CANCELLED-DRAFT-REFRESH-20261105.md)、[对应源码清单](TAB5-CANCELLED-DRAFT-314-SOURCE.json)。补丁基于仓库中的完整 .313 工程，三文件已逐字节应用验证；生产运行代码只改 `ui.c`。

| 项目 | 状态 | 证据与边界 |
| --- | --- | --- |
| TAB5 草稿恢复与新草稿保护 | 已完成 | 两客户端真实 LVGL 回归；真实 TAB5 两项 Claude 合成会话验收通过。 |
| 固件原包与展示设置 | 已完成 | 精确 ELF、VALID、三个实时 UI 样本及 settingsPreserved=true；测试草稿清理、正常桥接恢复。 |
| 桥接 0.6.17 / ESP8266 | 已有无需修改 | 无运行代码、协议或安装包变化，复用已有对应原包及验收。 |

测试使用正式加密 USB、设备自动触控/截图及未变更的桥接生产处理逻辑，注入确定性合成 CLI 验证 202 确认后取消时序。没有新增真实供应商网络或 Windows USB 的实测声明；不把诊断桥接程序称为正式 Mac ZIP 的二进制验收。此次仅交接 .314 原包，原有记录按实际范围保留。

This handoff transfers the exact accepted .314 TAB5 OTA image from fix commit `22294c5`; desktop bridges remain at 0.6.17 with unchanged packages. Use the original ZIP and checksum from the linked draft. Targeted native rendering, exact boot identity and two real-device synthetic cancellation scenarios passed on Mac. No new Windows USB or provider-network test is claimed. Windows remains the release coordinator.
