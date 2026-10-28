# 0.6.13 双平台发布验证

2026-10-28，接续首次设备接入任务。正式发布：[AI-bot v0.6.13](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.6.13)。北京时间 13:06 发布，Windows x64 与 Apple Silicon Mac 同步更新；TAB5 0.2.282-ui、ESP8266 0.5.7 和图库沿用现有附件。

## 源码与安装包

- 带注释的标签 `v0.6.13` 指向 `031b4f06197be876098aa2d55142d6c83f23bd53`，对应已合并 PR #10。
- [CI 40632246118](https://github.com/yaoyouzhong/AI-bot/actions/runs/40632246118)：Windows、macOS（含完整 XCTest）和固件全部成功。
- [Release 40632266014](https://github.com/yaoyouzhong/AI-bot/actions/runs/40632266014)：验证、Windows 包、Mac 包、草稿上传成功；固件打包按本轮范围跳过。
- Windows 正式安装器 31,118,489 bytes，SHA-256 `62f2dac05ea43c27123e06eed1180a7c6ef4a382bfdd5c40c5565e6c884dd8d4`。完整下载与附件 digest 一致。
- Mac ZIP 18,084,263 bytes，SHA-256 `8a2e84d9494ca97d2effaf8210aeb4de9dea86643895734d697046b2c7f6c661`。从完整 CI artifact 提取后与 Release digest 一致；ZIP CRC、Info.plist 0.6.13/macOS 13.0 下限和两份许可文件通过。没有把上传响应或仅比大小写成完整下载验收。
- 已验证的安装包字节保持不变；后续仅同步文档、截图、下载索引和媒体证据。

## Windows Sandbox

隔离环境已准备 Microsoft .NET Desktop Runtime 8.0.31；正式安装器退出 0，安装后 `--status-once` 与 `--self-test-onboarding` 退出 0。安装产品版本为 `0.6.13+031b4f06197be876098aa2d55142d6c83f23bd53`，DLL SHA-256 `d20f4484c1056ab33b38a2735ae37356cb92a2e3daf035c09c40f750ed7d1e1a`。

实际启动安装后的程序，看到空设备中心；点击添加首台设备，打开 TAB5 首装窗口，再关闭回到添加窗口。无 USB 时验证禁用，首装入口、帮助、状态与关闭入口可见。本轮未连接或改写任何实体设备。

安装日志同时报告 `Computer unlock setup incomplete`。解锁配置、无运行时的自动下载补装链路不计通过；本次未运行 Mac 安装包，Mac 原生界面和源码检查沿用已有记录。真实首刷、新配对和实体屏首次显示未重验，理由及此前结果见[首次接入记录](FIRST-RUN-20261028.md)。

后续只读预检确认：Sandbox 进程会话为 `1`、`locked=False`，`WTSGetActiveConsoleSessionId()` 为 `4294967295`（`0xFFFFFFFF`，没有活动物理控制台）；登录辅助任务返回 `FileNotFoundException / 0x80070002`，辅助目录、manifest 和 `tvnserver` 服务均不存在。`HelperInstaller.Run` 要求调用方与活动物理控制台为同一会话，因此这个 Sandbox 不满足安装条件；不是解除会话保护后可计通过的测试。预检退出 `0` 只代表诊断完成。真实 Windows 控制台中的组件安装及实际解锁仍须独立验收，本次未改宿主机登录组件或服务。

本地证据原位于发布 worktree 的 `artifacts/release-0613/`；收尾时须完整复制并核验到主工作区 `artifacts/release-0613-continuation-20261028/` 后才删除 worktree。`sandbox-results/` 含安装日志、真实退出码、安装版本、DLL 摘要和隔离控件图；`downloads/` 与 `verified-downloads/` 保存已校验包。Sandbox 已停止，环境列表和窗口列表均确认无残留。

## 公开内容与媒体

下载索引 refresh/sync/check 通过；全部七类包的公开地址、SHA 清单和大小，以及高清图库 manifest 一致。中英文主页、下载中心、安装指南、组件版本、功能图鉴、更新步骤与截图来源同步为实际发布范围。两张 Windows 接入图保留原生控件像素，Mac 旧图保留原始来源标注。

20 秒接入补充短片及可复现工程已作为 Release 附件发布；它使用 Windows 原生隔离控件和明确标注的 Mac 路径示意。原项目介绍片保留在主页和独立观看入口。

初次检查时，Chrome 主页和独立页播放器持续缓冲，时间为 `0`、`readyState=0`，备用浏览器也复现。自动附件播放器使用 `private-user-images.githubusercontent.com` 短时签名地址；刷新取得有效签名后仍未加载元数据，因此不能把过期签名认定为唯一原因。两条公开媒体路径的首段、开放末端和尾段 Range 均返回正确 `206`，这与浏览器播放结果分开记录。

改为显式 `<video controls playsinline preload="metadata" width="100%" src="公开附件地址">`，保持原 MP4 字节、单个原生播放器、独立观看与下载入口。GitHub Markdown 预览和更新后的独立观看页均保留 `github.com/user-attachments/assets/...` 视频源。公开独立页实测：播放到 `23.580399s`、暂停于 `23.637333s`、时间轴跳至 `121s` 时 `ended=true`、重播到 `5.637123s` 且 `paused=false`；原生控件显示完整 `121s` 时长。中英文主页及本仓库视频说明同步采用该写法，合并后需再核对默认分支公开主页。

PR #12 合并后的主页仍停在 `0s / readyState=0`，用户截图也复现黑屏，因此上述写法调整不能计为主页问题已解决。随后保留 1080p 原版下载，从同一文件转码 720p H.264 Main / yuv420p、AAC-LC、24 fps 和 faststart 的网页播放版，并上传为独立附件。大小从 `9,217,409` 降至 `4,968,870` bytes；本地完整解码和关键画面检查通过。原版前 256 KiB 的一次读取为 8.68 秒，新版为 4.59 秒；这是当次网络样本，不据此认定唯一根因或保证所有网络速度。

新版公开独立页连续播放到 `105.723663s / readyState=4`，随后自然结束于 `121.083333s / ended=true`；重播到 `0.048702s`、暂停于 `3.195217s`，原生时间轴可定位至 `119.872166s`。中英文主页、媒体说明和许可摘要同步为新版附件，原版下载保留。主分支最终实播结果写入收尾证据，不用独立页或 HTTP 响应替代。转码命令和摘要见 [产品视频说明](../PRODUCT_VIDEO.md)。

20 秒接入补充短片仍作为 Release 下载附件：完整公开下载摘要已核对；浏览器将直接地址按下载处理，不把它写成内嵌播放通过。所有媒体验证均独立于安装包、Mac 实机及设备验收。

## English

Windows and Apple Silicon Mac 0.6.13 were published from commit `031b4f0`; CI and both desktop package jobs passed. Exact package hashes, Mac archive metadata/licenses, Windows Sandbox installation and installed status/onboarding checks passed. Real Windows navigation through Add TAB5 and First Install was exercised without hardware writes. Sandbox has no active physical console, so it fails the login helper's installation session guard; no host components or services were changed. Automatic runtime download, this Mac package's execution and new physical first-flash/pair/display acceptance are not claimed. Public downloads and bilingual documentation were reconciled. Explicit native video markup retains the public attachment source: playback, pause, end seek, ended state and replay passed on the public separate watch page. Both homepages use the same markup and require a final default-branch browser check after merging. The supplemental clip remains a verified download, with no embedded playback claim.

The markup-only change in PR #12 did not resolve homepage loading, as confirmed by the user's screenshot. A new 720p web encode reduces the same film from 9.22 MB to 4.97 MB while retaining the original 1080p download and provenance. The new public watch-page player ran continuously to its natural end and passed replay, pause and timeline positioning. Final homepage playback remains a separate recorded check. Sandbox was stopped after preserving its evidence.
