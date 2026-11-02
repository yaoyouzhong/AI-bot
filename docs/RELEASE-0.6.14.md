# AI-bot 0.6.14 与 TAB5 0.2.302-ui

2026-11-02 发布 Windows x64、Apple Silicon Mac 桥接 0.6.14，以及独立版本 TAB5 0.2.302-ui。ESP8266 0.5.7 和图库沿用原正式版本；无需因桥接更新而刷固件。

[Windows 与 Mac 下载](../DOWNLOADS.md) · [TAB5 固件下载](../DOWNLOADS.md) · [安装说明](INSTALL.zh.md) · [中英文更新日志](../CHANGELOG.zh.md)

## 本次更新

- TAB5 Codex 直达支持提问题卡、多题切换和提交，录音绑定设备、会话及输入目标，目标变化时保留预览与草稿。
- 结束录音自动填入电脑；可以在电脑校对，再通过 TAB5 提交最新文字。可靠接收或持久入队即算成功，结果未知不自动重发。连续录音保留文字、换行、标点和句尾光标。
- 任务页可向运行中的任务发送或追加；电脑已有草稿、附件或引用时拦截并保留内容。送达未知时可核对并由用户确认恢复，不自动重发。
- 优化语音准备、语音设置、常用任务和保存／取消操作；Windows 可激活已安装的豆包输入法，并区分未安装、未运行等状态。
- 适配 GLM、MiMo、MiniMax 额度解析，失败时保留已有缓存；改善音乐封面处理，加快 Mac 协商后的 USB 固件传输。
- TAB5 改进按钮反馈、发送提示、收音设备显示、额度入口、音乐封面与字形、时钟和高清预览；支持确认后重装同版本，优化无线升级并等待匹配的安装信息。

## 使用

在 TAB5 的 Codex 直达选择会话，录音结束后文字自动填入电脑；需要时可在电脑校对，再点击 TAB5 的蓝色箭头提交。题卡按当前题目切换或提交；结果未知时先核对电脑，不重复发送。任务页使用“发送／追加”。

从旧版升级前正常退出桥接；安装包保留已有配对、草稿和设备展示设置。出厂 TAB5 使用首刷 ZIP，已有 AI-bot 的 TAB5 使用升级 ZIP；不要混用。保持供电并按设备提示确认。

## 验证与限制

Windows 最终安装包升级、启动、107 个文件与 7 份关键配置及展示策略保留通过。Mac 最终包的安装、正常重启、37 项设置及资料保留由用户确认；本轮 Mac USB 串口不可用，设备通道未计入本轮通过。Windows 与 Mac 均有此前 TAB5 功能实测，不能以一端代替另一端。

TAB5 首刷包和升级包包含同一份已在设备安装及启动验证的应用镜像（SHA-256 `475e0bf93790a6125555f7c863f2b25aa1fc959d7734681f32441849ad17f84e`）。源码包记录构建树和依赖补丁；独立逐位重建尚未完成，构建树缺少一项依赖元数据校验文件。

大疆麦克风、长时间闲置后的首录和未配置账户的额度接口仍待对应条件验证。Windows 安装器未签名；Mac 包为 Apple Silicon 临时签名、未公证，Intel 未验证。

## English

Windows x64 and Apple Silicon Mac bridge 0.6.14 and independent TAB5 firmware 0.2.302-ui are released. ESP8266 0.5.7 and existing artwork packages remain unchanged. See the [download center](../DOWNLOADS.en.md).

Codex Direct supports question cards, target-bound dictation, computer proofreading before TAB5 submission, and durable queue acknowledgement without automatically retransmitting unknown outcomes. Continuous dictation preserves punctuation and line breaks. Task delivery protects existing computer drafts and attachments; users can check and confirm recovery of an unknown result. Voice and settings, GLM/MiMo/MiniMax quota adapters, music covers and negotiated Mac USB firmware transfer have been improved. TAB5 also improves interaction feedback, recording-source and quota displays, clock, high-resolution previews and firmware reinstallation/update handling. See the [desktop changelog](../CHANGELOG.md) and [TAB5 changelog](development/TAB5-CHANGELOG.md) for the full categorized notes.

Windows installer upgrade, startup, 107 files and protected profile/display settings were verified. The user reported successful Mac installation, restart and preservation of 37 settings/data items; its USB device channel was unavailable in this round. The Windows installer is unsigned; the Apple Silicon package is ad-hoc signed, not notarized, and Intel is unverified. DJI, long-idle recording and unavailable provider-account checks remain pending. TAB5 source materials are provided, but independent byte-for-byte reproduction is not claimed.
