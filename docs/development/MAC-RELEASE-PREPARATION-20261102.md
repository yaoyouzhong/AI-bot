# Mac 补丁发布准备

**后续状态：用户明确要求发布后，Mac 0.6.15 已于 2026-11-02 公开。** 以下保留准备阶段说明；最终公开文件及收尾见[正式发布记录](RELEASE-0615-CLOSEOUT-20261102.md)。Published after the user's subsequent explicit instruction; the preparation history below does not supersede the closeout record.

远端已核对：`v0.6.14` 于 2026-11-02 公开，本次同步的 main 为 `34119c362515199eaa0a493baced78f0a10ac42b`。Mac 修改原基于 `fca200eea7a7dfce07009a0906706db2d0ed41be`；现已同步主干并保存在本地功能提交 `6ee40c8`，随后单独提交 0.6.15 发布元数据。

用户确认准备 Mac 专用 `v0.6.15` 时，保留已发布的 Windows 与固件版本。已同步上述主干，将本次 Mac 修改保存为本地提交；根目录版本、Mac plist 和共享 Windows 项目声明对齐 0.6.15，`desktopPlatform=macos`。Windows 仅同步源码版本声明，不构建、上传或发布新包。准备阶段未改变远端发布状态。

公开前必须完成：核对主干差异并固定干净源码提交；设置桥接版本及 `desktopPlatform=macos`；构建附许可与 SHA-256 的正式包；从该实际包验收安装、公开 0.6.14 升级、资料保持及授权次数，保留验收原包；再按唯一发布负责人流程操作标签、草稿与公开发布，完成中英文下载、安装说明、相关媒体及实际链接核验。

正式候选显式使用 `AIBOT_MAC_SIGN_IDENTITY=-`，与现有公开包的临时签名方式一致。本机证书签名候选的一次授权结果不能替代该包从旧版本升级的结果。部署专用迁移容器不等于已交付给普通用户的安装迁移流程；发布说明必须保留旧项目首次迁移可能逐项询问的边界。先核验正式包与实际安装表现，再决定是否具备公开条件，不上传带工作区修改的开发包并沿用旧结论。

相关记录：[统一入口验收](MAC-KEYCHAIN-VAULT-20261102.md)、[发布流程](../DESKTOP-DEVELOPMENT-RELEASE.md)、[发布收尾](../RELEASE-CHECKLIST.md)。准备阶段未推送、建标签、上传或发布；后续已按明确发布指令执行，见页首记录。

当前已验收候选源码为 `8f4a5edf0b81af977706ae30cfd38035572fd5f8`；Mac ZIP 摘要 `f07ca7b0a31ebff6d42d64fd52725a603795d43882722875c148296b8c15036a`。424 项测试、Release、实际包隔离与原生界面回归通过，包及源码许可／摘要已备齐。同一 ZIP 的实际安装、应用引导、一次钥匙串授权、同包重启无新提示、重新关联系统授权后定位成功、资料保持及真实音乐播放／暂停已通过；用户确认音乐操作没有新的权限提示。公开旧版首次迁移与全新用户环境仍未实测。见[候选验收](MAC-0615-PACKAGE-ACCEPTANCE-20261102.md)。说明提交不改变本包源码身份；不得为了更新验收说明而重建原包并沿用旧结论。

English: The user first authorized preparation of Mac-only v0.6.15 while retaining published Windows and firmware packages. The exact ad-hoc package from clean commit `8f4a5ed` passes local installation, one Keychain prompt, app-owned guidance, normal restart, settings preservation and navigation after the user re-added its accessibility entry. Real TAB5 play/pause also passes with no new permission prompt. These accepted bytes were subsequently published after an explicit release instruction; public legacy-first migration and fresh-user environments remain untested.
