# Mac 0.6.15 候选包验收

**`65620c7` 候选因辅助功能引导未显示而退回，不具备公开条件。** 用户授权范围为准备 Mac 0.6.15；Windows 保留正式版 0.6.14，ESP8266 0.5.7、TAB5 0.2.302-ui 与图库不变。未推送、创建标签、上传草稿或公开发布。正在加入应用自己的引导后重新生成候选；下列摘要仅对应退回包。

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

已正常退出原进程，将原应用备份成 ZIP，从上述 ZIP 安装 `/Applications/AIBotBridge.app`，全部安装文件与解压包相同，签名未修改。PID 64174 启动后，用户确认钥匙串一次、没有辅助功能引导；无凭据只读探针返回 401，收到新进程的实时 USB 心跳。辅助功能请求标记与实际 CDHash 相同，确认请求代码执行，不能据此认定系统显示了引导或授予权限。

前一候选 `0d9279b` 的真实结果：用户确认一次钥匙串提示并选“始终允许”，同包正常重启无再次提示；38 项固定设置、5 份配置文件、29 份加密资料与 3 份资源保持。TAB5 定位返回 409，当前进程诊断明确为辅助功能未生效。此失败阻止发布，未沿用旧本机证书包的定位通过结论。

该候选修复了全局“提示过”标志阻止新构建请求的问题，但系统未显示引导，故仍退回。后续改为应用拥有的原生说明及菜单入口，后台启动延后到用户打开应用后展示，不依赖系统提示一定出现。首次／旧记录、明暗四份实际渲染已查看，完整文案和按钮可读，窗口高度约 355–371 点；正常前台、后台延后、同包稍后及手动恢复的状态按设计验证。原钥匙串与加密资料保留；新包仍须重新登记摘要及实际安装验收。

## 公开前门槛

- 完成当前实际包的系统授权、正常重启与 TAB5 定位，确认原资料和设置保留。
- 从公开 0.6.14 分散钥匙串资料首次迁移、全新用户权限引导尚未实测；本机资料已迁入统一入口，不能代替这些路径。首次旧资料迁移可能仍需逐项系统批准。
- 本轮未重新验收真实音乐播放控制、ESP8266、DJI 或长闲置首录；按影响及已有证据明确适用范围，不记为通过。
- 标签须指向实际候选源码；验收哪个文件就发布哪个文件。现有标签工作流会重新构建，若采用该流程，必须在最终草稿构建完成后下载并重新核验实际附件，不能套用本包结论。
- 公开后按发布收尾清单更新中英文下载、安装说明与相关媒体；准备阶段下载索引继续保留原正式版本。

English: The Mac-only `65620c7` candidate is rejected for publication because no accessibility guidance appeared. Its packages and automated checks pass, and the user confirms one Keychain prompt; service readiness and fresh USB heartbeats are verified. The recorded guidance marker matches the actual CDHash, establishing that the system request ran but not that a dialog appeared. An app-owned guide and manual menu entry are being added, with background presentation deferred until user interaction. Fresh/upgrade and light/dark native previews have been reviewed. New package identity and physical acceptance must be recorded separately. Published Windows and firmware packages are unchanged; no remote release action has occurred.
