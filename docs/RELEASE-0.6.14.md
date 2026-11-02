# AI-bot 0.6.14 与 TAB5 0.2.302-ui

2026-11-02 发布 Windows x64、Apple Silicon Mac 桥接 0.6.14，以及独立版本 TAB5 0.2.302-ui。ESP8266 0.5.7 和图库沿用原正式版本；无需因桥接更新而刷固件。

[Windows 与 Mac 下载](../DOWNLOADS.md) · [TAB5 固件下载](../DOWNLOADS.md) · [安装说明](INSTALL.zh.md) · [中英文更新日志](../CHANGELOG.zh.md)

## 本次更新

- TAB5 Codex 直达支持提问题卡、多题切换和提交，录音绑定设备、会话及输入目标，目标变化时保留预览与草稿。
- 结束录音自动填入电脑；可以在电脑校对，再通过 TAB5 提交最新文字。可靠接收或持久入队即算成功，结果未知不自动重发。连续录音保留文字、换行、标点和句尾光标。
- 优化 Windows 和 Mac 的语音设置、常用任务、发送状态及 GLM、MiMo、MiniMax 额度解析；失败时保留已有缓存。
- TAB5 改进按压反馈、发送提示、音乐文字、封面内存与时钟显示；固件升级等待匹配的安装信息。

## 使用

在 TAB5 的 Codex 直达选择会话，录音结束后文字自动填入电脑；需要时可在电脑校对，再点击 TAB5 的蓝色箭头提交。题卡按当前题目切换或提交；结果未知时先核对电脑，不重复发送。任务页使用“发送／追加”。

从旧版升级前正常退出桥接；安装包保留已有配对、草稿和设备展示设置。出厂 TAB5 使用首刷 ZIP，已有 AI-bot 的 TAB5 使用升级 ZIP；不要混用。保持供电并按设备提示确认。

## 验证与限制

Windows 最终安装包升级、启动、107 个文件与 7 份关键配置及展示策略保留通过。Mac 最终包的安装、正常重启、37 项设置及资料保留由用户确认；本轮 Mac USB 串口不可用，设备通道未计入本轮通过。Windows 与 Mac 均有此前 TAB5 功能实测，不能以一端代替另一端。

TAB5 首刷包和升级包包含同一份已在设备安装及启动验证的应用镜像（SHA-256 `475e0bf93790a6125555f7c863f2b25aa1fc959d7734681f32441849ad17f84e`）。源码包记录构建树和依赖补丁；独立逐位重建尚未完成，构建树缺少一项依赖元数据校验文件。

大疆麦克风、长时间闲置后的首录和未配置账户的额度接口仍待对应条件验证。Windows 安装器未签名；Mac 包为 Apple Silicon 临时签名、未公证，Intel 未验证。

## English

Windows x64 and Apple Silicon Mac bridge 0.6.14 and independent TAB5 firmware 0.2.302-ui are released. ESP8266 0.5.7 and existing artwork packages remain unchanged. See the [download center](../DOWNLOADS.en.md).

Codex Direct supports question cards, target-bound dictation, computer proofreading before TAB5 submission, and durable queue acknowledgement without automatic retransmission of unknown outcomes. Continuous dictation preserves existing punctuation and line breaks. Desktop settings, quota adapters, device feedback and TAB5 display/upgrade behavior have been refined.

Windows installer upgrade, startup, 107 files and protected profile/display settings were verified. The user reported successful Mac installation, restart and preservation of 37 settings/data items; its USB device channel was unavailable in this round. The Windows installer is unsigned; the Apple Silicon package is ad-hoc signed, not notarized, and Intel is unverified. DJI, long-idle recording and unavailable provider-account checks remain pending. TAB5 source materials are provided, but independent byte-for-byte reproduction is not claimed.
