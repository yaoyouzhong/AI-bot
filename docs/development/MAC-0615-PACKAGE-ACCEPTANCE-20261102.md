# Mac 0.6.15 候选包验收

**当前候选已通过本机安装、授权引导、同包重启与 TAB5 定位验收，尚未公开。** 用户授权范围为准备 Mac 0.6.15；Windows 保留正式版 0.6.14，ESP8266 0.5.7、TAB5 0.2.302-ui 与图库不变。未推送、创建标签、上传草稿或公开发布。其他验收范围与限制见下文。

## 文件与源码身份

- 候选源码：`8f4a5edf0b81af977706ae30cfd38035572fd5f8`，从干净工作区构建；后续验收说明提交不改变本包源码身份，发布标签应指向此提交。
- Mac ZIP：`AI-bot-bridge-v0.6.15-macos-arm64.zip`，18,665,971 字节，SHA-256 `f07ca7b0a31ebff6d42d64fd52725a603795d43882722875c148296b8c15036a`。
- 源码 ZIP：`AI-bot-bridge-v0.6.15-source.zip`，81,096,623 字节，SHA-256 `31e15a9da7a5aba136dbf626ace404d245ea8903e4cb52fe43eb8a179afeeff3`。
- 本机材料：`artifacts/mac-0615-release-20261102/` 下的 `candidate/`、`public-payload/`、`PACKAGE-VERIFICATION.json`、`AUTOMATED-CHECKS.json`、`INSTALLATION.json`、`FINAL-ACCEPTANCE.json` 及资料保持记录。旧候选位于独立历史目录，不混入当前发布目录。
- 包含 LICENSE、第三方声明和摘要；核对全部 17 项包内文件及 2,691 项源码文件。`desktopPlatform=macos`，没有构建 Windows 安装器或固件。
- Apple Silicon，macOS 13+；显式 `AIBOT_MAC_SIGN_IDENTITY=-`，临时签名、未公证。Intel 与浏览器隔离属性下的首次打开未验收。

## 自动验证与界面

最终源码通过 424 项 XCTest、Release 构建；实际解压包通过 29,059 项隔离检查、任务追加回归、离线刷机工具检查、架构及严格签名完整性校验。证书专用 helper 探针不作为临时签名路径的通过项；本包统一凭据访问以本机实际启动及正常重启验证。

设计覆盖默认开启文案、应用自己的授权说明、后台延后展示及固定菜单入口。沿用系统字体、语义色、原生按钮和间距，首次／旧记录的明暗四份实际预览均已查看，窗口约 355–371 点高，文案、对齐和按钮完整；最终包渲染与已查看图片逐一摘要相同。完整原生 UI 回归还覆盖默认／最小设置尺寸及授权状态。用户已确认点击菜单栏图标后出现应用引导，点击主操作能打开系统设置；全新账户前台首次启动尚未实测。

## 实际安装与当前状态

已正常退出原进程，将原应用备份成 ZIP，从上述 ZIP 安装 `/Applications/AIBotBridge.app`。安装后及最终验收后再次核对，全部应用文件与解压包相同，签名未修改。备份不以改名 `.app` 留在“应用程序”目录。

用户确认首次启动只出现一次统一钥匙串提示并完成授权。后台启动不消耗已展示标记；用户点击菜单栏图标后看到了引导并打开系统设置。当时旧条目已有勾选，但同包重启定位仍为 409，当前进程明确记录辅助功能未生效。用户移除旧条目、重新添加当前应用并开启后，再正常重启同一个包，确认无新提示且定位成功。新 PID 65999 记录定位 200、目标会话确认及实时 USB 心跳；未发送测试消息或读取录音正文。

38 项用户固定设置、5 份配置文件、29 份原加密资料与 3 份资源均与安装前一致；数据缓存更新另列。仅预期的非用户设置“当前构建引导标记”更新，且与当前 CDHash 匹配。原钥匙串资料保留，未导出凭据。

该结果包含一次必要的用户系统授权重新关联，不能描述为升级免授权。应用说明不能代替 macOS 授权；已勾选的旧条目不代表当前构建已获得有效权限。

## 前一候选退回依据

`0d9279b` 的定位 409 与 `65620c7` 的系统请求没有显示引导均保留在本机历史材料；后者虽然请求标记与 CDHash 匹配，用户明确未看到提示，因此退回。当前包改用应用自己的引导，未把接口调用成功当作界面显示或授权通过。

## 公开前门槛

- 当前实际包的授权、引导、正常重启、TAB5 定位及原资料保持已通过；真实音乐控制正在补验。
- 从公开 0.6.14 分散钥匙串资料首次迁移、全新用户权限引导尚未实测；本机资料已迁入统一入口，不能代替这些路径。首次旧资料迁移可能仍需逐项系统批准。
- ESP8266、DJI 或长闲置首录本轮未重新验收；按影响及已有证据明确适用范围，不记为通过。
- 标签须指向实际候选源码；验收哪个文件就发布哪个文件。现有标签工作流会重新构建，若采用该流程，必须在最终草稿构建完成后下载并重新核验实际附件，不能套用本包结论。
- 公开后按发布收尾清单更新中英文下载、安装说明与相关媒体；准备阶段下载索引继续保留原正式版本。

English: The exact Mac-only `8f4a5ed` ZIP passes scoped local installation, app-owned guidance, one initial Keychain prompt, normal restart without new prompts and TAB5 navigation. Re-adding the current app's old accessibility entry was necessary before navigation succeeded; this is not an authorization-free upgrade. The final process confirms navigation 200 and fresh USB heartbeats. All 38 user settings, 5 configuration files, 29 encrypted files and 3 resources remain unchanged; the expected onboarding marker is tracked separately. The exact archives pass integrity checks, 424 tests, the Release build, 29,059 isolated checks and native UI previews. Music acceptance is being completed. Public legacy-first migration, fresh-user environments and other hardware workflows remain outside this acceptance. Windows and firmware packages are unchanged; no remote release action has occurred.
