# Windows 发布准备：TAB5 对话入口与跨端对齐

Windows 是本次后续发布的唯一负责人。本文件是开发交接和候选准备，不是发布指令、已冻结候选或正式发布记录。首先按 [跨平台交接](WINDOWS-HANDOFF-TAB5-PROVIDER-STABILITY-20261110.md) 完成 Windows 对齐；统一流程见 [开发发布流程](../DESKTOP-DEVELOPMENT-RELEASE.md)和[发布自动化](../RELEASE-AUTOMATION.md)。

## 源码、范围与候选身份

- Windows 最近已核对的开发基线是 `codex/windows-dynamic-microphone-20261105` 的 `6456c169c3f92222c1a233489da36af347b8ed28`。Mac 交接分支含独立后续工作；以实际远端和待合并变更为准，选择性整合，避免以旧 Windows 文件覆盖 Windows 开发。两端桥接如同次交付，必须从同一个最终候选源码 SHA 各自原生构建。
- 当前仓库的待发布版本源为桥接 `VERSION=0.6.18`、TAB5 `versions/TAB5=0.2.318-ui`；这只是当前登记，**不是对最终发布版本或范围的决定**。Windows 完成对齐后核对已发布标签与版本，按最终范围维护 `release-manifest.json` 的平台、组件版本及中英文更新日志。ESP8266 和图库只在实际变化时参与。
- Mac 已运行的本地桥接候选源码 `6f3d27317d397b3305b454450ca61eda89d2c1fb`，主程序 SHA-256 `0c215c09271a5caed627a41ce93dcc63f42c3467ec1d18f9c30b5b0d3f99306b`；安装前后加密草稿摘要一致。它不是同最终整合源码构建的正式 Mac ZIP，也没有记录公证或正式安装包验收。最终候选发生代码变化时，只复用确有依据的未变路径证据。
- Mac 真机已验收的 TAB5 应用 BIN：`0.2.318-ui · R4-ICON2`，7,002,608 字节，SHA-256 `fcee93bffeffba6c8fa01e4ab04ff7bbc890647349c0a9bae422b7c2e114a2c6`，ELF SHA-256 `1f82688bfde3c11b66717e5a1b9cdfbf5326e0d62372182f164c43840a2c0c82`。实际 sidecar 文件 SHA-256 `884ee9bcf9930d1d7e93d2f76d73de8e1c540592d34359db34dfa411767d78f6`；说明文本 SHA-256 `b6216ec55473dbe03962cfa38935bc91db4ae2206ace43a08f0dd6cedf67e86e`。USB 启动、VALID 分区与展示设置保留已验，不能把其他构建认作同一原包。
- 上述本地 BIN、sidecar 与 Mac 应用位于 Mac 的忽略目录，不随 Git 分支传输，也不是已经冻结的发布包。Windows 如要晋级这份原包，需取得并核对原字节，补齐同应用的首刷包和与镜像匹配的完整源码包后按工具封存；重新构建会产生新身份，须对新包完成受影响验收。不能用旧 .318 原包的 SHA 或旧验收冒充 ICON2。

## 当前实际 TAB5 说明

已验收应用的实际 sidecar 为：

```text
## 对话页
- Codex 和 Claude 改用图标切换。
- 优化厂商入口稳定性，放大图标和未读数字。
```

本次中英文 `CHANGELOG` 与 TAB5 中英文日志新增 `Unreleased / 未发布` 草稿；Windows 负责人合并两端最终范围后，按实际版本和同一日期整理正式中英文条目。对用户尚未验收的内容保持原有证据边界，不把待验证写成已完成。实际升级说明须与最终 BIN 的版本、SHA-256 相符，使用实际 sidecar 做离屏预览。

## 后续执行清单

1. Windows 完成两家目录稳定性、原生 Terminal 发送状态及设备联动的实现与回归，按 `AGENTS.md` 在 Windows 执行 Release 构建与 `--status-once`，记录安装、升级、设置和草稿保留；不将 Mac 结果当作 Windows 通过。
2. 在独立 TAB5 工程应用经核对的 R4 基线与 ICON2 累计补丁。完整首刷 ZIP、升级 ZIP、实际 sidecar 和对应源码包必须绑定同一应用 SHA；保存旧候选并使用 [本地固件流程](../TAB5-FIRMWARE-WORKFLOW.md)。电脑端、ESP8266、TAB5 分别按其版本线决定是否发布。
3. 收集 Windows 和 Mac 各自原生包、源码身份、实际安装/启动记录及受影响真机结果；完成许可证、第三方声明、校验文件与 [发布收尾清单](../RELEASE-CHECKLIST.md)。此前 Mac 的 511 项 Swift 测试 0 失败、4 跳过；TAB5 六帧跨约 46 秒均显示两个厂商且发送可用，非连续观察；真实未读角标视觉和长期稳定性待确认。
4. 本次源码公开内容预检报告现有 `docs/development/tab5-318-refresh-source/source-overlay.zip` 为 `unreviewed-binary-or-artwork`（3090 文件、1 项）。素材的来源、许可及公开打包边界须按现有来源记录处理并通过实际预检，不能绕过或把本地构建通过写成公开包合规。
5. 版本与中英文说明就绪后，以最终干净源码 SHA 运行 `candidate.yml` 或原生流程，冻结对应候选；TAB5 按 `prepare_release_assets.py --component tab5 --tab5-source ...` 和 `release_pipeline.py seal --tab5-source ...` 核验源码。为每个候选用 `release_pipeline.py record` 记录安装、设备与 `release-review`，候选 SHA、包 SHA 和记录一致后再晋级。
6. 用户后续明确正式发布范围时，Windows 发布负责人按 [发布自动化](../RELEASE-AUTOMATION.md)统一完成候选预检、标签、原包晋级、服务器摘要、公开及中英文下载和媒体收尾；不为纯文档变化重建已冻结原包。保留既有宣传视频结构，仅更新受到界面变化影响的镜头和素材。

## English release handoff

Windows owns the subsequent coordinated release. Finish Windows-specific catalog stability, native-terminal and device validation first; freeze the final shared desktop source and independently version TAB5. The Mac bridge and TAB5 application identities above describe locally validated candidates, not transferable release archives or acceptance of rebuilt bytes. Final first-install, OTA and source packages must bind the same TAB5 application SHA. Prepare matching bilingual release notes, licenses and checksums, resolve the existing public-content guard finding, and record actual package-specific Windows, Mac and device evidence before promotion.
