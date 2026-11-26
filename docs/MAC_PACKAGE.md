# Mac 应用安装：已合并到完整指南

**请阅读 [AI-bot 完整安装图文指南](https://github.com/yaoyouzhong/AI-bot/blob/main/docs/INSTALL.zh.md)。**

下载、Windows/Mac 安装、小屏刷写、首次连接、内容设置、升级和排错都在同一页，按顺序操作即可；本页仅保留旧链接入口，不再维护另一套安装步骤。

Installation instructions have moved to the [complete illustrated guide (Chinese)](https://github.com/yaoyouzhong/AI-bot/blob/main/docs/INSTALL.zh.md). All steps are on that page.

包内 `MAC_SIGNING.json` 标明这份 ZIP 使用的是临时签名还是证书签名。本打包流程不做 Apple 公证。若从临时签名切换到固定证书签名，macOS 可能在首次重启时要求重新确认部分权限；只处理实际缺失的权限即可。

`MAC_SIGNING.json` identifies whether this ZIP uses an ad-hoc signature or a certificate. This packaging workflow does not notarize the app. On the first upgrade to a certificate-signed build, macOS may ask you to reapprove some permissions. Reapprove only the permissions that are missing.
