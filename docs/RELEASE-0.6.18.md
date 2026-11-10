# 0.6.18 / TAB5 0.2.318-ui 发布准备

本页是发布准备记录，当前公开下载仍为 Windows／Mac 0.6.17、TAB5 0.2.313-ui。用户于 2026-11-07 选择 Windows、Mac 和 TAB5 一起准备，等待 Mac 原包验收。目标 Windows x64、Apple Silicon Mac 0.6.18 与 TAB5 0.2.318-ui；`desktopPlatform=all`，ESP8266 0.5.8 和图库保持不变。当前先不发布，不创建正式标签或替换公开附件。

Claude Code 原会话接入、来源标签与后续交互优化已在 Windows/TAB5 本地候选实现，见[原会话接入记录](development/CLAUDE-CODE-NATIVE-ALIGNMENT-20261105.md)及[最新来源标签验收](development/TAB5-SESSION-SOURCE-20261107.md)。Mac 需按[功能对齐交接](development/MAC-0618-FUNCTION-ALIGNMENT-20261107.md)补差异并原生验证。`778e433` 桥接包不再作为本次最终候选；旧包和原范围证据保留，但不通过改名复用。

双平台共同源码 SHA 尚未冻结。交接检查时的基线 HEAD `9894a66750c601df687c3a6898c05aa1de309c04` 不包含当时全部未提交改动，不能据此标记最终包。用户随后授权提交并推送，开发交接通过 `codex/windows-dynamic-microphone-20261105` 分支进行；接收端应取得包含交接文档的提交。此前 ZIP 仍是带摘要的选定文件快照，本次开发交接提交也不是已完成 Mac 对齐后的双平台最终候选。

## 本次变化

- Claude 原会话协作：区分普通 VS Code 与原生终端，保留原客户端审批和送达未知保护；Mac 的真实输入能力及独立验收仍待补齐。
- 会话来源与 Harness：列表、输入区和回复标题显示承载端来源，提供编译期 Claude 兼容适配接口；来源与模型、标题、发送权限分离，不声称已接通任意第三方 Harness。
- Mac 的 Claude 列表保留原会话名；菜单栏右键不再放 Codex 桌面授权入口，桥接不主动弹桌面授权引导。普通功能仍默认启用，桌面控制仍取决于 macOS 的实际权限。
- 切换与回复：后台草稿同步不反复阻塞切换，完成回复稳定显示、后台静默刷新，去除完成态“稍后更新”；通过每会话控制状态改善发送按钮就绪，Mac 按共同协议对齐。
- 两端豆包自动检测已正确路由到本次大疆物理端点时直接复用；需要临时选择时继续读回、恢复并保护用户手动改选。Windows 语音小窗改善提示换行。
- TAB5 项目菜单切页即关闭，长项目名与箭头分开；底部双圆与发送等高，采用 72px 双圆、统一底色与细边框，以及胶囊发送／追加／取消按钮。
- 修复取消后文字／照片恢复、长按清空后旧文字残影、录音停止后的按下态及色差；保护相机之外的持续 LCD 读取。

## 原包与验收

本次准备复用最新已验收的 TAB5 来源标签镜像：BIN SHA-256 `fc6e8d3b680398175db8adf63e1827b3927b0c480b7c659c6e1eededa8e77fb7`，6,933,920 bytes；ELF identity `d8e7531e4b41bfb28ab7752728deb4ea9c65ba6e1a9d70d1a80b3dd4802f8c4d`，配套 sidecar SHA-256 `e42c3772a5fe6cf7a0dbc3dece8a49086655129d6af63c6326231982f641fb01`。已完成一次本地 OTA、精确启动核对和实体界面验收，不为发布重建或再刷机。首刷/升级容器及完整源码归档尚需按最新镜像封存，首刷结构检查不代表恢复出厂实测。

Windows 当前日常 DLL SHA-256 为 `4b6bac4f0df20a52e3650ea3ad7e23ae1017a1d578bd2a4b148ca3f7a69f8327`，构建、专项回归与正常用户运行检查已通过。这是本地功能候选，不是同一冻结 SHA 的双平台最终发行包。最新实体检查为十段 51 帧、五次会话/客户端切换和两条已有回复打开；标签正确，所采回复稳定且发送可用，七份设置/配对/草稿摘要及设备展示设置保留。采样间隔 2,016–6,079ms，不能宣称毫秒切换或排除帧间瞬态；此轮没有新增模型发送、追加或停止。

旧 controls72 的 `fad7d04c…`、静默控制 `3140a12c…` 等包保留历史验收意义，但不再是最终 TAB5 交付镜像。原[controls72 验收](development/TAB5-CONTROLS72-ACCEPTANCE-20261105.json)不失效，也不能代替最新镜像身份；旧源码 ZIP、sidecar 不可标成最新候选。

Windows 自动模式录音、停止回填、客户端草稿／照片隔离、USB→Wi-Fi→USB 接续已有对应开发候选实测；Mac 自动模式大疆录音与停止回填也已有其独立原包实测。正式 0.6.18 的元数据和包身份变化需要各自补实际原包安装／启动核验，功能代码不变的实测按原范围复用。Mac 诊断入口曾有焦点保护拒绝，不写成该入口录音成功。

桥接双平台须采用同一冻结源码提交。旧 Mac 已验收 ZIP 为 `0.6.17-development-79670da`，不能改名为 0.6.18；本次最终 Mac 包及 M01–M10 回执仍待补齐，阻止双平台候选晋级。Windows 未签名；Mac 旧包为本机开发签名、未经公证，最终包状态按 Mac 回执核实，不提前声称 Developer ID 签名或公证。安装包须附 LICENSE、THIRD_PARTY_NOTICES 和 SHA-256。

## 图示与兼容

![TAB5 .318 原生界面演示](assets/screens/tab5-dialogue-0318-preview.png)

图示来自 controls72 阶段生产 LVGL 的实际离屏渲染，使用合成项目、草稿和附件数据，不是实体屏实拍，不展示后续来源标签；素材摘要登记于 `licenses/materials.json`。最终发布前核对受影响截图和中英文 CHANGELOG、主页/下载/安装说明，只更新会误导用户的内容。原宣传片复用仍准确的场景，不声称媒体本体已更新，保留片段顺序和整体结构。

TAB5 保持桥接 0.6.17 兼容下限；自动音源优化需对应的新桥接。现有用户选升级 ZIP，首次安装用首刷 ZIP，不要求重配对或恢复出厂。完整源码采用既有归档规范，保留工程、配置、锁文件、必要依赖覆盖与 SDK 补丁，排除缓存、主机工具链、BIN／ELF 和私有运行数据。

## English

This is preparation, not publication. The user selected Windows x64 and Apple Silicon Mac 0.6.18 together with independent TAB5 0.2.318-ui, pending Mac feature alignment and exact-package acceptance. ESP8266 and galleries stay unchanged. Scope includes original Claude sessions, source badges and a compiled Harness seam, stable replies, per-session controls, switching, audio routing and dialogue interactions. Mac production support remains subject to its own implementation and native evidence.

On Mac, Claude rows keep their original titles. The menu bar's context menu no longer offers Codex desktop authorization, and the bridge does not show its own desktop authorization prompt. Ordinary features remain enabled by default; desktop control still depends on macOS permissions.

Reuse the latest accepted TAB5 BIN `fc6e8d3b…` and matching sidecar, not the earlier controls72 image. Both desktop packages must come from one frozen commit, which is still pending; the dirty checkout HEAD is not a complete candidate identity. Old Mac archives cannot be relabeled. Final containers and source archives must match the accepted bytes. Reuse unchanged evidence with its original scope, preserve settings, and separately verify final installation/startup and actual signing/notarization. The illustration is an earlier controls72 native LVGL render, not physical footage or a preview of the new badges. Update misleading release material while retaining accurate media and the approved video structure. See the Mac handoff for the actionable gaps and return requirements.
