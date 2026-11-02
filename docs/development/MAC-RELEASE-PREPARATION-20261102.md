# Mac 补丁发布准备

远端已核对：`v0.6.14` 于 2026-11-02 公开，当前远端 main 为 `34119c362515199eaa0a493baced78f0a10ac42b`。本地源码基于 `fca200eea7a7dfce07009a0906706db2d0ed41be`，有尚未提交的 Mac 修改与验收记录。

建议新增 Mac 专用 `v0.6.15`，保留已发布的 Windows 与固件版本。当前只补充中英文未发布说明，未变更版本号、平台发布清单或远端状态。

公开前必须完成：核对主干差异并固定干净源码提交；设置桥接版本及 `desktopPlatform=macos`；构建附许可与 SHA-256 的正式包；从该实际包验收安装、公开 0.6.14 升级、资料保持及授权次数，保留验收原包；再按唯一发布负责人流程操作标签、草稿与公开发布，完成中英文下载、安装说明、相关媒体及实际链接核验。

本机签名候选的一次授权结果不能替代公开 ad-hoc 包从旧版本升级的结果。部署专用迁移容器不等于已交付给普通用户的安装迁移流程。发布前需确认正式包的签名和旧资料迁移路径，不能直接上传带工作区修改的本机应用并沿用旧结论。

相关记录：[统一入口验收](MAC-KEYCHAIN-VAULT-20261102.md)、[发布流程](../DESKTOP-DEVELOPMENT-RELEASE.md)、[发布收尾](../RELEASE-CHECKLIST.md)。未推送、建标签、上传或发布。

English: Prepare a new Mac-only patch release, proposed v0.6.15, while retaining published Windows and firmware versions. Current changes are uncommitted local work. Freeze a clean source commit, build the distributable, and validate the actual public-package upgrade and authorization path before publication. Local certificate-signed migration acceptance does not establish acceptance for the public ad-hoc installer. No remote release action has been taken.
