# Mac 最终候选包安装验收（2026-11-02）

验收完成时间：2026-11-02T08:27:03+08:00（Asia/Shanghai）。用户指定下载 Actions 最终构建物，核对固定 SHA-256，验收安装、启动与设置保留；禁止重新打包或发布。本记录不扩大标签、上传、推送或公开发布授权。

## 最终包身份

| 项目 | 结果 |
| --- | --- |
| Actions | [运行 41318440450](https://github.com/yaoyouzhong/AI-bot/actions/runs/41318440450) |
| Artifact | `candidate-macos-arm64`，ID `13088228700` |
| 源码 | `fca200eea7a7dfce07009a0906706db2d0ed41be`，与内嵌 `SOURCE_SHA.txt` 一致 |
| 文件 | `AI-bot-bridge-v0.6.14-macos-arm64.zip` |
| 大小 | 18,817,827 字节 |
| SHA-256 | `3c8c8ae9754111e74c0e3969eff941dd06ea5a2d03a3c20c1982a83a031058f5`，与用户指定值及随包摘要一致 |
| 应用 | `0.6.14`，`arm64`，最低 macOS 13；本机 macOS 27.2 / arm64 |
| 主程序 SHA-256 | `ca186fa48827eb8f69a303d0efd2172b383f17bb33fe276722313e2a00978531` |
| helper SHA-256 | `1c9589a5376bb66ff5b040e0053649ed9b2065c6d7d570ff031539421fe05f94` |

Actions 的 Mac 与 Windows 作业成功；本次固件及 macos-source 作业跳过，不据此宣称固件重新构建。下载的是指定运行的原始 ZIP，不以本地开发候选代替。

## 原包与安装验证

- ZIP 固定摘要、包内 `FILES.sha256` 的 17 个文件均通过，包含许可、第三方声明、安装入口及应用。
- `codesign --verify --deep --strict` 通过。签名为 ad-hoc，无 Developer ID / Team ID，无公证票据；`spctl --assess` 拒绝，不能称为已通过 Gatekeeper 或已公证。GitHub CLI 下载未构成带浏览器隔离属性的首次安装测试；[安装说明](../INSTALL.zh.md#mac)中的系统授权路径仍适用。
- 正常退出旧桥接，在 `/Applications/AIBotBridge.app` 安装原包应用；14 个应用文件逐项与解压目录一致，安装后签名校验通过。旧应用保留用于回退。
- 最终包主程序自身通过 29,059 项隔离回归和内嵌刷机工具离线检查；未构建本地应用，未重新签名、替换 helper、修改权限数据库或改动包内容。
- 最终包自身的原生设置回归通过，生成 267 张离屏预览；实际查看语音设置默认亮色与最小暗色，文字、下拉框、按钮及状态均可读，取消在左、保存在右。用例使用隔离数据，未打开或置顶用户的设置窗口。

## 旧设置与资料保留

正常退出旧应用后保存基线与私有回退备份，安装后比较哈希，不记录设置值、密钥、Cookie、令牌或私人草稿正文。

| 范围 | 安装后结果 |
| --- | --- |
| UserDefaults 41 个原有键（37 项固定设置、4 项可变缓存） | 安装后、启动前全部相同；授权启动及正常重启后，37 项固定设置仍全部相同，没有新增配置键。4 项行情／额度缓存已正常刷新，均为有效非空 JSON，原顶层数据键保留；不将缓存刷新误报为设置丢失 |
| 5 个原有配置文件 | 全部相同：设备登记、常用任务、日程、生日、本机签名指纹；不存在的文件不计通过 |
| 29 个原有加密资料文件 | 全部相同；密文保留不等于已完成新进程的解密／发送验收 |
| 3 个本机桌宠资料文件 | 全部相同；不将此计作未安装的完整图库验收 |
| 6 个 Keychain 条目 | 授权启动及重启后，持久引用及创建／修改元数据仍全部相同；诊断只请求属性与引用，不导出凭据值。应用实际读取原配对令牌成功，其他凭据的完整功能分别验收 |

## 应用启动与正常重启（通过）

首次通过 `open -g -j` 后台启动时等待 macOS 钥匙串授权，用户随后明确确认已完成授权；保留这一步的实际安装体验，不称为无需权限确认。

- 授权后，已安装最终包的唯一进程监听服务，并对无凭据的本机只读 `GET /status` 返回 `401`。实现只有在原配对令牌成功读取且长度有效后才会进入此分支，因此可确认实际应用读取凭据成功；没有通过外部工具导出令牌，没有取消鉴权。`/tab5/v1/status` 也返回 `401`，未发送录音、草稿或消息。
- 对该进程执行正常退出，确认旧进程终止，再后台启动 `/Applications/AIBotBridge.app`。新进程在 30 秒观察窗口内到达服务就绪，间隔 10 秒的第二次探测仍为 `401`，唯一应用进程稳定，已完成启动且未成为前台应用。
- 授权启动后和正常重启后再次比较：37 项固定设置、5 个配置文件、29 个加密资料文件、3 个桌宠文件均保持；6 个 Keychain 条目元数据相同。改变的 4 项仅为 `deepseek_web_quota_cache`、`domestic_quota_cache`、`quota_cache`、`stock_cache` 后台数据缓存。
- 收尾再次核对原 ZIP 摘要、17 项文件清单和安装后全部 14 个应用文件，仍与下载原包逐字节相同，签名校验仍有效。旧应用继续保留，当前运行的是“应用程序”目录中的最终包。

**本次指定范围：原包安装、应用启动／正常重启、上述设置及资料保留，均通过。** 这不是整个平台发布验收全部完成或公开发布授权；系统注销／开机启动、浏览器隔离后的 Gatekeeper 首次打开未作专项测试。

## 设备状态（未计通过）

当前最终包的后台诊断已进入 TAB5 设备发现阶段，报告“串口不可用”，没有该新进程的有效 USB 心跳；不能仅据此确定是拔线、设备离线还是端口占用。因此本轮未将 USB 通道、设备认证、真实录音／发送及设备显示计作最终包实机通过，没有要求重刷当前 .302。已有加密资料的密文和钥匙串条目保留已核对，但没有额外读取私人草稿正文做解密演示。

## 证据与边界

本机详细证据位于忽略目录 `artifacts/mac-final-package-41318440450/`：`PACKAGE-VERIFICATION.json`、`INSTALLATION.json`、`PRESERVATION-BEFORE.json`、`PRESERVATION-INSTALLED.json`、`KEYCHAIN-BEFORE.json`、`KEYCHAIN-INSTALLED.json`、`KEYCHAIN-AUTHORIZED.json`、`KEYCHAIN-RESTARTED.json`、`STARTUP-FIRST.json`、`STARTUP-RESTART.json`、`PRESERVATION-AUTHORIZED.json`、`PRESERVATION-RESTARTED.json`、`CACHE-REFRESH-CHECK.json`、`DEVICE-OBSERVATION.json`、`FINAL-ACCEPTANCE.json`、`parity.log`、`flash-tool.log`、`ui.log`、`ui/`。私有回退备份留在本机，不进入 Git。

本轮没有重新打包、重新构建应用、重新签名、上传、创建标签、发布 Release 或刷写设备。新选择器的系统默认／指定 Mac 麦克风组合、DJI 实物、长闲置首录和缺少订阅账户的真实刷新，沿用[已有待验记录](MAC-VOICE-SELECTOR-20261101.md)；旧开发包的录音／提交成功不冒充最终包专项实机验收。

## English handoff

The exact Actions artifact and inner ZIP match the requested SHA-256. All 17 manifest entries, installed bundle bytes/signature, 29,059 isolated checks and native UI fixtures pass. All 37 fixed settings, 5 configuration files, 29 encrypted private files, 3 pet files and metadata for 6 Keychain items remain unchanged after startup and restart. Four valid nonempty data caches refresh normally and are distinguished from settings. The original archive and bundle are unchanged; no rebuild, repack, re-sign, upload or publication occurred. After the user completed macOS Keychain authorization, first startup and normal quit/restart passed: the installed app reads its existing pairing token, listens, returns the expected 401 to unauthenticated read-only probes and remains the only background instance. Installation, application startup and the scoped preservation checks are accepted. USB discovery currently reports an unavailable serial port with no fresh heartbeat, so physical device channels and broader release acceptance remain pending. The app is ad-hoc signed, not notarized, and Gatekeeper assessment rejects it; a browser-quarantined first launch has not been tested.
