# Mac 补丁发布准备

远端已核对：`v0.6.14` 于 2026-11-02 公开，本次同步的 main 为 `34119c362515199eaa0a493baced78f0a10ac42b`。Mac 修改原基于 `fca200eea7a7dfce07009a0906706db2d0ed41be`；现已同步主干并保存在本地功能提交 `6ee40c8`，随后单独提交 0.6.15 发布元数据。

用户已确认准备 Mac 专用 `v0.6.15`，保留已发布的 Windows 与固件版本。已同步上述主干，将本次 Mac 修改保存为本地提交；根目录版本、Mac plist 和共享 Windows 项目声明对齐 0.6.15，`desktopPlatform=macos`。Windows 仅同步源码版本声明，不构建、上传或发布新包。当前远端状态未改变。

公开前必须完成：核对主干差异并固定干净源码提交；设置桥接版本及 `desktopPlatform=macos`；构建附许可与 SHA-256 的正式包；从该实际包验收安装、公开 0.6.14 升级、资料保持及授权次数，保留验收原包；再按唯一发布负责人流程操作标签、草稿与公开发布，完成中英文下载、安装说明、相关媒体及实际链接核验。

正式候选显式使用 `AIBOT_MAC_SIGN_IDENTITY=-`，与现有公开包的临时签名方式一致。本机证书签名候选的一次授权结果不能替代该包从旧版本升级的结果。部署专用迁移容器不等于已交付给普通用户的安装迁移流程；发布说明必须保留旧项目首次迁移可能逐项询问的边界。先核验正式包与实际安装表现，再决定是否具备公开条件，不上传带工作区修改的开发包并沿用旧结论。

相关记录：[统一入口验收](MAC-KEYCHAIN-VAULT-20261102.md)、[发布流程](../DESKTOP-DEVELOPMENT-RELEASE.md)、[发布收尾](../RELEASE-CHECKLIST.md)。未推送、建标签、上传或发布。

最终本地候选源码为 `65620c7a8d5a7c1c11b6744f7d392b187fddd329`；Mac ZIP 摘要 `1143fb31cb9e74f6754287d6448b0ff0aa64a1902ec281aee0dacbc3f73dae46`。424 项测试、Release、实际包隔离与原生界面回归通过，包及源码许可／摘要已备齐。已按同一 ZIP 安装，系统授权、引导与定位仍待完成，尚不具备公开条件。前一包定位失败已用于修复按构建更新授权引导，未掩去失败或混用旧签名包结论；见[最终候选验收](MAC-0615-PACKAGE-ACCEPTANCE-20261102.md)。说明提交不改变待验收包的源码身份。

English: The user authorized preparation of Mac-only v0.6.15 while retaining published Windows and firmware packages. Main has been reconciled, source changes are committed locally, shared version metadata is aligned and the manifest selects macOS only. Build from a clean commit with explicit ad-hoc signing and validate the actual installer and upgrade path. Local certificate-signed migration acceptance does not establish acceptance for the public package; legacy items may still need first-time approval. No remote release action has been taken.
