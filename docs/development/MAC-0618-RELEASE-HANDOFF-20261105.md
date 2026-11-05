# Mac 0.6.18 最小发布交接

目标：为本轮已对齐的音源修复生成正式 0.6.18 Mac 原包。Windows 统一负责合并、推送、标签与发布。当前为本地发布准备，尚未声称本文件或新候选提交已到 GitHub；待 Windows 提供远端可达的冻结 SHA 后执行。

1. 检查仓库／分支和已有修改，不覆盖用户工作。检出 Windows 给出的完整冻结源码 SHA；根 `VERSION`、Windows csproj、Mac plist 必须均为 0.6.18。两端同 SHA，不自行追加实现或升号。
2. 运行 `bash scripts/package_macos.sh artifacts/mac-0618-<SHA前12位>`，再运行 `python3 scripts/release_pipeline.py stamp <输出目录> --source <完整SHA> --platform macos`。保留现有签名身份，不修改钥匙串、系统权限、音源、配对或固件。
3. 复用 `79670dac2cea6f0e12ac4a6744d45d0c61a99768` 的 469 项测试（1 条件跳过）、29,068 parity 及正常入口真实大疆／豆包自动录音→停止→回填、音源指纹未变的原证据。只有版本声明与包身份变化，不要求重新配对、刷机或全套物理复测；打包脚本原生检查照常执行。
4. 对最终 0.6.18 ZIP 完成严格签名、arm64、包内 `Contents/Resources/SOURCE_SHA.txt` 与版本读回，以及正常后台安装／启动和资料保留检查。新包运行代码与上述已验收代码一致时记录复用依据；不要把源目录运行当成安装包验收，也不把诊断入口焦点拒绝写成成功。
5. 回传原始 `AI-bot-bridge-v0.6.18-macos-arm64.zip`、同名 `.sha256`、`BUILD-SOURCE-macos.json` 和简短验收 JSON，包含完整源码 SHA、大小、SHA-256、实际签名状态及检查范围。不得改动验收后的 ZIP；不要发送个人语音文本、凭据或真实会话正文。

没有要求 Mac 自行 push、创建标签、公开发布、刷机、重新安装桥接或向其他人发送消息。Windows 提供具体上传交接授权后，才使用指定草稿保存附件；不要覆盖旧附件。若无法取得冻结源码或后台启动条件，保留输出并写明阻碍，不自行用旧开发包替代。

The Mac host packages the exact Windows-provided frozen source at 0.6.18, reuses unchanged functional evidence from 79670da and checks the actual new archive identity, signature and background startup separately. Do not relabel the accepted 0.6.17 development ZIP, alter system/profile settings, flash TAB5, publish, or overwrite existing assets. Windows remains the single release owner.
