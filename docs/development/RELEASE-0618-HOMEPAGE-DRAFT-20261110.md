# 0.6.18 / TAB5 0.2.318-ui 主页与安装说明草稿

此文件用于发布时审核，不是当前公开版本。文本要在 Mac 对齐、最终固件与验收完成后复核；链接仅在附件实际发布可访问后替换。

## 中文主页更新提示

**本次更新：** Windows／Mac 桥接 **0.6.18** 和 TAB5 **0.2.318-ui** 改善 Claude Code 原生会话识别、对话和未读回复体验，并优化语音授权后的恢复。TAB5 增加项目内会话滚动和未读优先显示，改进总览显示与固件升级提示。ESP8266 保持 **0.5.8**。[版本与验收说明](../RELEASE-0.6.18.md) · [安装指南](../INSTALL.zh.md)

## English homepage update

**This update:** Windows/Mac bridge **0.6.18** and TAB5 **0.2.318-ui** improve native Claude Code session discovery, dialogue and unread replies, as well as recovery after voice authorization. TAB5 adds scrolling within project conversations and unread-first visibility, with refinements to overview rendering and firmware-update feedback. ESP8266 remains **0.5.8**. [Release scope and acceptance](../RELEASE-0.6.18.md) · [Installation guide](../INSTALL.zh.md)

## 发布时需核对的替换

- 功能图鉴与中英文主页中有关“外部运行的 Claude 会话一律只读”的旧描述需按最终两端验收修订：已确认可控的受支持原生会话可按对应平台能力交互；无法确认目标或权限的会话保留只读／发送保护。不要把 Mac VS Code 支持写成已验收功能。
- 更新对话页图示时使用 `docs/assets/screens/tab5-dialogue-0318-r13-preview.png`：这是合成会话和未读数据的 LVGL 离屏渲染，与设备实拍和 R13 真机验收区分。该图的来源及摘要已登记在 `licenses/materials.json`。
- 桥接两个附件及校验文件指向 `v0.6.18`，TAB5 首刷／升级包和校验文件指向 `tab5-v0.2.318-ui`；具体文件名以实际发行附件和 `scripts/artifact_names.py` 为准。ESP8266 和独立图库维持各自已发布入口。
- 更新 `download-catalog.json` 后，通过仓库脚本同步并核对中英文下载页及安装指南；主页顶栏、中英文概述、当前发布说明、设备展示截图分别核对。首次安装与后续升级使用不同包，不以版本字符串代替镜像身份。
- 首页视频的 GitHub user-attachments 播放链接、1080p 下载链接、视频来源页和工程包必须指向实际上传且可播放的同一新版本。可复用未受影响的封面，不更改视频原有章节顺序或配乐；新镜头应明确合成演示数据和来源。
- 安装指南 Mac 权限描述应以最终包实际行为为准，不把旧版引导当作 0.6.18 的承诺。记录 Windows 未签名及 Mac 最终签名／公证状态。
- 公布后再核对中英文页面、每个附件的下载、SHA-256 清单和播放器，按 [发布收尾清单](../RELEASE-CHECKLIST.md)留证。
