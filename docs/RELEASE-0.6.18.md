# Windows / Mac 桥接 0.6.18 与 TAB5 固件 0.2.318-ui

[English](#english) · [下载](../DOWNLOADS.md) · [安装说明](INSTALL.zh.md)

这次更新主要有两部分：电脑桥接让 TAB5 能继续部分已在电脑上打开的 Claude Code 会话；TAB5 固件让找项目、看未读回复和操作对话更方便。

## 电脑桥接

- **Windows 和 Mac**：在 TAB5 的「对话 → Claude」选择受支持的电脑会话，可以在原会话里看回复，并按屏上的可用操作发送、追加或停止。同一仓库的 Codex 会话归在一个项目下。在电脑上读过的回复，TAB5 也会标为已读。
- **Windows**：首次允许麦克风后，会接着录同一次语音。用安装器升级桥接只需确认一次管理员授权，日常运行的桥接也能直接提供 TAB5 固件。
- **Mac**：可连接已打开且可确认的 Apple「终端」Claude Code 会话，按系统提示授权即可。额度历史图会直接标出每天记录到的用量占周额度多少；配合新版 TAB5 固件，键盘和触控板也可以退出萌宠屏保预览。

[完整桥接更新日志](../CHANGELOG.zh.md#0618---2026-11-10) · [桥接发布页](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.6.18)

## TAB5 固件

- **会话更好找**：用图标切换 Codex 和 Claude，同一项目的会话在一张列表里滚动查看。未读排在前面，收到新回复时会等你滑完再调整列表。
- **操作更顺手**：相机和麦克风按钮更大。取消排队的追加消息后，如果输入框仍为空，原来的文字和照片会恢复。Claude 回复完成时有专属语音提示。
- **升级等什么，看得见**：设备还在读取数据或保存草稿时，会提示正在等什么，准备好后再开始。你也可以取消准备。

[完整 TAB5 更新日志](development/TAB5-CHANGELOG.zh.md#02318-ui---2026-11-10) · [TAB5 发布页](https://github.com/yaoyouzhong/AI-bot/releases/tag/tab5-v0.2.318-ui)

要使用完整的对话改进，建议先更新电脑桥接，再更新 TAB5。已安装 AI-bot 的 TAB5 选升级包，仍在出厂系统的 TAB5 选首刷包。ESP8266 保持 0.5.8，可选图库也无需更新。

## English

[Downloads](../DOWNLOADS.en.md) · [Installation](INSTALL.zh.md)

This release has two parts: the desktop bridge lets TAB5 continue supported Claude Code conversations already open on your computer, and the TAB5 firmware makes Dialogue easier to browse and use.

### Desktop bridge

- **Windows and Mac:** Open Dialogue → Claude on TAB5 and select a supported conversation to read replies in the original session. Use the available on-screen controls to send, follow up, or stop. Codex conversations from the same repository appear under one project. Replies you read on your computer are marked read on TAB5 too.
- **Windows:** Granting microphone permission continues the same recording. The bridge installer needs one administrator confirmation, and the running bridge can provide TAB5 firmware directly.
- **Mac:** Connect to supported Claude Code conversations already open in Apple Terminal by following the macOS permission prompts. The quota history chart labels each day’s recorded usage as a percentage of the weekly allowance. With the new TAB5 firmware, the Mac keyboard or trackpad can dismiss the companion screensaver preview.

[Full bridge changelog](../CHANGELOG.md#0618---2026-11-10) · [Bridge release](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.6.18)

### TAB5 firmware

- **Find conversations more easily:** Switch between Codex and Claude using their icons and scroll through a single conversation list per project. Unread conversations appear first, and new replies come into view after you finish scrolling.
- **Use Dialogue more comfortably:** Larger camera and microphone buttons are easier to tap. Cancelling a queued follow-up restores the original text and photos if the input box is still empty. Claude has its own spoken completion alert.
- **See what is holding up an update:** TAB5 shows when it is waiting for a background read or draft save, then continues when ready. You can cancel preparation.

[Full TAB5 changelog](development/TAB5-CHANGELOG.md#02318-ui---2026-11-10) · [TAB5 release](https://github.com/yaoyouzhong/AI-bot/releases/tag/tab5-v0.2.318-ui)

For all the Dialogue improvements, update the desktop bridge first, then TAB5. Use the upgrade package if TAB5 is already running AI-bot and the first-install package if it is still on factory firmware. ESP8266 stays at 0.5.8 and optional gallery packs do not need updating.
