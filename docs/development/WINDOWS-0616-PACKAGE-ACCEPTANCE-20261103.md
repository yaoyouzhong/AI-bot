# Windows 0.6.16 实际包安装升级验收

源码候选 `28ff9cf474ec74d93beb8f5041f4e02d8d7d751d`；安装程序 SHA-256 `99a31ae0be8e393c6b3087d08953fdd3723c294fb3fa22ce9e4d9eba3107855d`。使用已构建原包，不重新构建。

- 在本机独立临时目录静默安装已发布 0.6.14，再用指定 0.6.16 安装程序升级。依赖检测确认现有 .NET Desktop 和 WebView2，无需安装运行时；关闭快捷方式及安装后自动启动。
- 两次安装退出成功，安装登记分别记录 0.6.14 和 0.6.16；111 个安装文件与已核验的便携包字节一致。
- 20 个设置、配对、草稿相关文件在安装升级前后摘要相同。原安装登记恢复并在正常用户会话复核一致；临时任务已删除，常驻桥接未更换。
- Codex 启动环境中的 `--status-once` 被既有数据目录保护拒绝，未关闭保护。通过正常用户会话的临时隐藏任务运行已安装程序，产生有效状态 JSON、无错误输出，退出码 0。
- 这次没有新做托盘界面、系统权限弹窗、真实凭据解密或硬件全量验收；此前对应功能实测按变更范围沿用。文件摘要保留不单独证明加密资料可以解密。

此前沙盒中断停留在微软运行时或旧安装程序阶段，不作为候选包失败或通过的证据。原始日志及结果保存在本地 `artifacts/release-0616-esp058-tab303-20261103/native-upgrade/`。

English: The exact 0.6.16 setup upgrades the published 0.6.14 in a separate native Windows directory. All 111 installed files match the verified archive, 20 protected files retain their hashes, and the original installation registration is restored and independently checked in the desktop session. The installed executable returns valid status JSON with exit code zero through a hidden normal-user task. Existing profile protection remains enabled. This is installation, upgrade and CLI-startup evidence; it does not claim new tray, permission, credential-decryption or complete hardware acceptance.
