# 0.6.11 / .281 / .57 发布收尾 — 2026-10-27

发布提交：`58a88ba4250ffd404e68aa8392b9847f5a6e9300`，来自 [PR #7](https://github.com/yaoyouzhong/AI-bot/pull/7)。三个组件均使用带注释标签；桌面版为 GitHub Latest。

## 正式附件

| 组件 | 版本 | 发布页 |
| --- | --- | --- |
| Windows / Apple Silicon Mac | 0.6.11 | [v0.6.11](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.6.11) |
| ESP8266 | 0.5.7 | [esp8266-v0.5.7](https://github.com/yaoyouzhong/AI-bot/releases/tag/esp8266-v0.5.7) |
| TAB5 | 0.2.281-ui | [tab5-v0.2.281-ui](https://github.com/yaoyouzhong/AI-bot/releases/tag/tab5-v0.2.281-ui) |

25 个附件在公开前核对服务器大小与完整 SHA-256；Windows/Mac 安装附件另与 CI 生成的清单一致。首刷／升级、旧客户端兼容别名、对应源码、许可证和媒体源码分别保留。小屏公开包使用已经实机验收的应用字节；不以 CI 另行构建的镜像替代已验收镜像。

## 验证

- [最终 PR CI](https://github.com/yaoyouzhong/AI-bot/actions/runs/40458315546)：Windows、macOS、ESP8266 全部通过。
- [候选打包](https://github.com/yaoyouzhong/AI-bot/actions/runs/40456736444)：Windows/Mac 均通过。
- [桌面正式流水线](https://github.com/yaoyouzhong/AI-bot/actions/runs/40458828652)、[小屏流水线](https://github.com/yaoyouzhong/AI-bot/actions/runs/40458828569)、[TAB5 标签检查](https://github.com/yaoyouzhong/AI-bot/actions/runs/40458828552)全部通过。
- 公开内容与许可摘要检查、14 项打包边界测试、下载目录测试、正式日志提取和修改文档的相对文件链接检查通过。
- 本地长路径 PowerShell 源码复制曾失败；正式包使用已通过完整校验的干净 CI 工作目录构建，没有跳过校验。
- TAB5 首刷和升级应用均为 `684587e024c9a52e1e2e069abba066826a4e93c425c1ec87e574aa73a4f6ce00`；小屏应用为 `c5557ae42830b20075b573b3a7b2abc8e4d7afa208225d5231062427ede6e387`，与已刷实机一致。
- [TAB5 人工与运行证据](TAB5-281-ACCEPTANCE-20261027.md)、[小屏字体与设置保留](ESP8266-CREDIT-WEIGHT-20261027.md)及 [Mac 阶段证据](MAC-MODEL-ACCOUNTS-20261026.md)分别记录；不把 Windows 构建写成 Mac 实机复验。

## 主页与媒体

中英文主页、下载目录、组件版本页、安装和更新说明均按实际公开附件更新；明确 TAB5 支持 Windows / Apple Silicon Mac。新增 .281 草稿、长按清空、额度和模型账户截图，以及 .57 字库加粗对照，均标明隔离原生像素与实机照片的区别。

64 秒、1920×1080 / 30 fps 视频完成 1,920 帧解码、128 联系帧、6 处转场、3 种封面和字体／图片／定位检查。419 个源码文件打包核对，独立解压重建 HTML 摘要一致。媒体源码保留 AGPL/OFL 及原生素材来源。GitHub CLI 以隐藏窗口运行；用户明确将浏览器稳定性和网页播放器交互验收留待后续，本轮使用封面链接与正式 MP4 下载。主观试听未验。

公开核验：七类标准下载的公开状态、SHA-256 清单、实际下载 URL 和大小全部通过；两套高清图库的整包大小、摘要和分段数核对通过。新版 MP4 完整公开下载后的 SHA-256 与本地一致。此结果不代替网页播放器交互验收。

## 主要附件 SHA-256

| 文件 | SHA-256 |
| --- | --- |
| `AI-bot-tab5-0.2.281-ui-first-install.zip` | `aa72f27a601998a933bb55bbd3768c513328c1ad13a9d876d60455fceae084e6` |
| `AI-bot-tab5-0.2.281-ui-source.zip` | `51c745bfde5c089c92ca9565014fb068c39d4d9a9a965853343193ca07c6a2a2` |
| `AI-bot-tab5-0.2.281-ui-upgrade.zip` | `794f698b544d4740da1ed3bc8c7b936b1d9cd0c9ec12891998ec4541a2d374fd` |
| `AI-bot-esp8266-0.5.7-firmware.zip` | `47b5cce4d4797ff844e49f65d265b0c292410386d312e26863ff729d1c601020` |
| `AI-bot-bridge-v0.6.11-macos-arm64.zip` | `ead2f8602190372c08fc7cbcbd063c75881068efe38fabaa3e7fbf9dd213f858` |
| `AI-bot-bridge-v0.6.11-windows-x64-setup.exe` | `6c1b7891b343e6bd7f82250b8689c7cedb6c9892d3b05b6cf9fb20e857a6e728` |
| `AI-bot-product-intro-20261027-0611-281.mp4` | `94ec158b1ac95c58bdb34b7062149db7ca06457cb46acc8c5025d659f7611985` |
| `AI-bot-product-video-source-20261027-0611-281.zip` | `887334f8eb9a911fe55feae70abd41b2cf65ff4d98c5c31d93decadc68d21371` |

## English

All three component releases are public under annotated tags. Final Windows/macOS/firmware CI and component release workflows passed; all 25 remote asset sizes and digests were verified before publication. Accepted device application bytes were preserved. Bilingual home/download/install pages, native fixture screenshots and the reproducible 64-second film were updated. CLI/API checks are distinct from deferred browser playback; subjective audio listening and the runtime NimBLE minimum watermark are not claimed. See linked records for hardware acceptance boundaries.
