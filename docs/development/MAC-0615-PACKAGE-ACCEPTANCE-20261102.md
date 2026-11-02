# Mac 0.6.15 最终候选验收

**当前为本地候选，系统授权与设备定位仍待完成，不具备公开条件。** 用户授权范围为准备 Mac 0.6.15；Windows 保留正式版 0.6.14，ESP8266 0.5.7、TAB5 0.2.302-ui 与图库不变。未推送、创建标签、上传草稿或公开发布。

## 文件与源码身份

- 候选源码：`65620c7a8d5a7c1c11b6744f7d392b187fddd329`，从干净工作区构建；后续验收说明提交不改变本包源码身份。
- Mac ZIP：`AI-bot-bridge-v0.6.15-macos-arm64.zip`，18,662,583 字节，SHA-256 `1143fb31cb9e74f6754287d6448b0ff0aa64a1902ec281aee0dacbc3f73dae46`。
- 源码 ZIP：`AI-bot-bridge-v0.6.15-source.zip`，81,090,183 字节，SHA-256 `f2acef510d3da80006f02068ea3b4f6caa19fc9e35ca0adeefe091f2a81f09bb`。
- 本机材料：`artifacts/mac-0615-release-20261102/` 下的 `candidate/`、`public-payload/`、`PACKAGE-VERIFICATION.json`、`AUTOMATED-CHECKS.json`、`INSTALLATION.json` 与后续验收记录。旧候选位于独立历史目录，不混入当前发布目录。
- 包含 LICENSE、第三方声明和摘要；核对全部 17 项包内文件及 2,689 项源码文件。`desktopPlatform=macos`，没有构建 Windows 安装器或固件。
- Apple Silicon，macOS 13+；显式 `AIBOT_MAC_SIGN_IDENTITY=-`，临时签名、未公证。Intel 与浏览器隔离属性下的首次打开未验收。

## 自动验证与界面

最终源码通过 424 项 XCTest、Release 构建；实际解压包通过 29,059 项隔离检查、任务追加回归、离线刷机工具检查、架构及严格签名完整性校验。证书专用 helper 探针不适用于临时签名路径：前一临时签名候选按设计拒绝该探针，未将其记为通过；本包实际凭据访问仍须独立验收。

设计覆盖默认开启文案和换包后权限失效的引导，沿用现有原生布局、字体、语义色及系统辅助功能请求。最终包原生 UI 回归覆盖默认／最小尺寸、明暗外观及授权状态，已查看最终未授权功能页，文字、对齐及按钮可读。迁移弹窗明暗渲染与已查看版本的 SHA-256 相同。系统提示是否实际出现仍以用户反馈为准。

## 实际安装与当前状态

已正常退出原进程，将原应用备份成 ZIP，从上述最终 ZIP 安装 `/Applications/AIBotBridge.app`，全部安装文件与解压包相同，签名未修改。当前启动进程 PID 64174；正在等待系统钥匙串响应，服务尚未就绪，辅助功能构建标记尚未写入。不能据自动测试认定本包已完成授权或定位。

前一候选 `0d9279b` 的真实结果：用户确认一次钥匙串提示并选“始终允许”，同包正常重启无再次提示；38 项固定设置、5 份配置文件、29 份加密资料与 3 份资源保持。TAB5 定位返回 409，当前进程诊断明确为辅助功能未生效。此失败阻止发布，未沿用旧本机证书包的定位通过结论。

最终候选已修复全局“提示过”标志阻止新构建引导的问题：按公开代码签名摘要识别构建，权限未生效且功能开启时请求一次；同一构建不自动重复，明确关闭时不请求。原钥匙串与加密资料保留。最终包的提示次数、系统引导、授权后服务／USB 心跳、正常重启、定位及资料保持待本机继续确认。

## 公开前门槛

- 完成当前实际包的系统授权、正常重启与 TAB5 定位，确认原资料和设置保留。
- 从公开 0.6.14 分散钥匙串资料首次迁移、全新用户权限引导尚未实测；本机资料已迁入统一入口，不能代替这些路径。首次旧资料迁移可能仍需逐项系统批准。
- 本轮未重新验收真实音乐播放控制、ESP8266、DJI 或长闲置首录；按影响及已有证据明确适用范围，不记为通过。
- 标签须指向实际候选源码；验收哪个文件就发布哪个文件。现有标签工作流会重新构建，若采用该流程，必须在最终草稿构建完成后下载并重新核验实际附件，不能套用本包结论。
- 公开后按发布收尾清单更新中英文下载、安装说明与相关媒体；准备阶段下载索引继续保留原正式版本。

English: This Mac-only local candidate was built from clean commit `65620c7a8d5a7c1c11b6744f7d392b187fddd329`. Its exact packages, licenses and manifests pass integrity checks; 424 tests, the Release build, 29,059 isolated checks and native UI fixtures pass. The installed bundle matches the archive. System authorization and final navigation remain pending, so publication is blocked. The preceding candidate passed one consolidated Keychain prompt and no prompt after restart but failed navigation because accessibility trust was inactive; the final candidate renews guidance per untrusted build. Published Windows and firmware packages are unchanged. Legacy public upgrades, fresh-user environments and other physical workflows are not inferred from this machine's already migrated vault. No remote release action has occurred.
