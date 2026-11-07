# Mac 0.6.18 对齐回传

## 范围和源码身份

- 来源：Windows 已推送的 `codex/windows-dynamic-microphone-20261105`，完整 SHA `23c5924371ae7e40e85b93d08d7999cbacd1837c`。接收分支：本地 `codex/mac-native-claude-alignment-20261107`，直接在该提交上开发；原工作区和 11 月 5 日 Mac 候选分支保留。
- 目标仍为 Apple Silicon Mac 0.6.18、Windows 0.6.18 和 TAB5 0.2.318-ui。现有协议 v1：复用已定义的可选 `tasks[].source` 与 `claudeTasks.controls`，没有改接收端字段，也不重刷 TAB5。最终共同源码仍待发布负责人冻结。本次做本地开发和准备，未推送、打标签、上传、替换常驻程序或发布。
- 原生环境：arm64 macOS 27.3 (26C5035r)，Apple Swift 6.4.2，Claude Code 2.1.318。只读探针确认一条 Terminal 原会话，操作权限未就绪；本机未发现正在使用的 VS Code/iTerm2 Claude 宿主。当前常驻是旧 0.6.17 开发版，源码 `79670dac2cea6f0e12ac4a6744d45d0c61a99768`，由它持有 TAB5 USB，不能作为新候选验收。

## 对齐清单

| 项目 | 结果与证据 | 剩余 / 发布影响 |
| --- | --- | --- |
| M01 原会话收发 | 已完成 Swift 派发与离线回归。原生会话能力按操作声明，默认拒绝；Windows 的会话 API 不冒充 Mac 普通扩展。终端验证 PID/启动身份、同用户、选中 TTY 和进程组；VS Code 验证选中会话、唯一原生面板及输入框。发送前复核原会话和草稿，不启动或恢复另一会话。 | 两宿主的真实端到端收发待验证。需要原会话位于前台及辅助功能，终端还需已授予自动化权限。终端实现文字发送、排队和受控停止，不承诺照片；普通 VS Code 实现发送、选图和受控停止，不承诺运行中追加。未验证能力阻断发布。 |
| M02 来源与 Harness | 已完成代码和真实 workspace 夹具。来源按注册身份解析，同 UUID 多端认领禁操作；来源不符或身份变化拒绝。来源缓存 512 项、读上限 256 KiB、使用已有钥匙串加密私有存储，断连或重启仅保留显示。 | 真实 Terminal 来源已只读确认；VS Code、第三方 Harness 和新 Mac 桥接驱动的设备标签待实机。Mac 暂无 Windows 专用后台会话适配器。 |
| M03 每会话控制 | 已完成协议投影与回归。两条未读会话可独立就绪，读 A 不清 B；按实际传输目录裁剪，过期、撤权、停止、断连和冲突清能力。派发另做实时检查。 | 前台输入限制使多个 Mac 窗口不能同时可写。新 Mac 候选真机按钮就绪待验证。 |
| M04 回复与切换 | 已完成离线验证。后台读取失败保留成功正文、完成状态及同轮回执，不把一次回复失败升级为目录故障。原生发送等待期间草稿可保存，完成只清与发送内容一致的草稿。TAB5 离屏四专项通过。 | Mac 与配对设备的真实切换、查看/返回待验证。 |
| M05 历史与空白页 | 已完成代码和合成回归。`system/api_error` 和 `informational` 只作为同会话合法主链连接；覆盖重试完成、异会话、伪造父节点、支线、空白页和历史中断。 | 普通 VS Code 空白页真实识别待验证。 |
| M06 送达与草稿 | 已完成自动回归。写前记录、精确用户记录和内容摘要确认、重复请求只查回执、未知不自动重发、原轮次停止证据和新草稿保护。 | 实际原客户端送达、停止与照片仍待各支持宿主验证。 |
| M07 项目与入口 | 已完成代码与回归。沿用规范化路径身份及八条分页，合并空白页按 UUID 去重，保留会话标题与失败前目录。 | 真实项目切换待随设备验证。 |
| M08 权限与焦点 | 已完成设计与离屏检查。Mac 设置页保留原字号、间距和主次操作；说明前台、辅助功能和终端自动化要求；审批在原客户端。只读观察不切换窗口、不请求权限。 | 真实权限已授予时的原生输入、宿主焦点及设备操作待验证。 |
| M09 语音与通道 | 已有无需修改。未改音源、语音、配对、显示设置或固件。USB 只读枚举见 TAB5，常驻仍使用原串口。 | 只复用已记录的 DJI 等原范围；未用新桥接独立重测 USB、Wi-Fi、BLE 或长闲置首录。 |
| M10 原包 | 待验证。可构建开发包，最终双端共同源码和候选 ID 待冻结。 | 最终原包的实际安装、升级、设置保留和真机验收仍阻断发布。 |

## 验证与证据

- 初始 Windows 交接源码在本机编译失败；修复 Swift 运算符和原生输入守卫后完成本机原生回归。开发验证 `swift test` 484 项（1 条件跳过，0 失败），模块合同 29,068 项，`swift build -c release` 通过；现有 Swift/SDK 编译警告保留。最终开发包结果另附。
- 已查看 Mac 原生设置离屏渲染，检查最小窗口浅色和常用窗口深色；Claude 说明可读，无截断或按钮重叠。复核 TAB5 1280×720 的会话来源、第四行、长标签、按钮层级及完成态正文。先核对归档与 1,156 个源文件摘要，再叠加 Windows 六份现有补丁，在 Mac 编译实际 LVGL；会话来源、会话控制、回复状态、切换同步四个专项通过。离屏结果不等同真机。
- 本地证据目录 `artifacts/mac-native-alignment-20261107/`。Windows 最新 TAB5 参考 BIN `fc6e8d3b680398175db8adf63e1827b3927b0c480b7c659c6e1eededa8e77fb7`，ELF `d8e7531e4b41bfb28ab7752728deb4ea9c65ba6e1a9d70d1a80b3dd4802f8c4d`；本次未读取或声明 Mac 当前连接设备的运行镜像身份。
- [Apple Automation 权限检查](https://developer.apple.com/documentation/coreservices/3025784-aedeterminepermissiontoautomatet)用于无提示预检；终端发送和追加使用 [Claude Code 的 queueSubmit](https://code.claude.com/docs/en/keybindings#chat-actions)，避免把运行中的追加改成强制立即发送。

## 下一步与发布边界

用专用原生测试会话先验证一条无工具文字往返（原会话收到、TAB5 读同轮回复）；Terminal 与 VS Code 分开记录，再按支持范围补选图、追加和停止。实际请求须有当次授权。此次模型请求为 **0**，未改用户权限、设备设置、配对及现有草稿。Mac 真实链路通过后由唯一发布负责人冻结两端共同源码；用该 SHA 生成最终 Mac 原包，再完成安装验收。

The local Mac branch follows Windows source 23c5924. Native tests and offscreen AppKit/LVGL checks verify the new contracts. Read-only inspection found one original Terminal session; input permission and real Mac/TAB5 message delivery remain unverified. The development package is distinct from the final dual-platform release candidate.
