# Mac TAB5 port / 开发与集中联调

2026-10-19：用户授权完成五阶段剩余开发、每模块 Mac CI 与必要修复，持续推送 `codex/mac-tab5-phase1-ci`。不合并 main，不部署、不创建标签或发布。真机联调安排在次日白天，当前仅开发与 CI。

The user authorized remaining development and per-module Mac CI on the test branch. No merge, deployment, tags or release. Physical acceptance is deferred to the following daytime session.

| 阶段 / Stage | 模块 / Module | 状态 / State |
| --- | --- | --- |
| 1 可行性 | USB/BLE、公开 Codex App Server、Mac 本地 Speech | 路径与原生编译/夹具测试完成；目标 Mac 版本/权限待现场确认 |
| 2 日常可用 | USB/Wi-Fi、日常数据、显示设置 | 源码与 CI 通过；设备回执与协议互验完成，真机待联调 |
| 3 无线与资源 | CoreBluetooth、封面/桌宠/图库、OTA | 开发与自动测试完成；射频、吞吐、真实屏幕和新固件启动待现场确认 |
| 4 交互 | 电脑/音乐、Codex、照片、语音、快捷控制台 | 105 测试覆盖；真实播放、语言模型、精确桌面输入框版本兼容待现场验收 |
| 5 完整交付 | 首刷、备份恢复、候选安装包、权限引导 | 代码/模拟设备流程/候选 App 与原生视图渲染完成；不安装、不发布 |

UI plan: keep native AppKit controls, system semantic background/label/separator/accent colors and system font sizes 23/15/13/12. Left-aligned connection status remains above USB/Wi-Fi setup. Add explicit Bluetooth and feature opt-ins, then separate resource/firmware actions; use scroll views for long settings. Match the existing Mac menu-bar utility, without adding a web dashboard or decorative animation. CI UI snapshots and physical permission/interaction checks have separate evidence.

协议保持 v1；每种通道共用配对身份、认证、重放校验、事务和结果。CI 只能证明该提交的编译与自动化测试；不能证明真实蓝牙吞吐、屏幕显示、权限或 OTA 启动。缺失的供应商/平台能力不伪造、不通告。

## 模块证据 / Module evidence

- Codex：通过官方本地 App Server 协议及已安装 CLI 的 `app-server proxy` 连接桌面服务。独立开关控制公开回复读取与主动发送；不启动独立会话服务器，不改模型或审批策略。加密草稿与持久化不确定回执先于发送，重复 ID 不重发；历史分页、终态、运行中追加/中断和设备绑定阅读回执分别处理。Mac 原生前台/输入框确认仍需后续快捷控制台适配和真机验收。协议依据：[官方 App Server 文档](https://learn.chatgpt.com/docs/app-server)及本机 CLI 生成的公开 schema；测试只使用夹具，不读取或发送真实用户对话。

- CoreBluetooth：`17f3922` 在 [Mac CI 39274063784](https://github.com/yaoyouzhong/AI-bot/actions/runs/39274063784) 通过 60 项测试和 Release 编译。覆盖身份/nonce/ACK、MTU 边界、USB 优先和纯 BLE 显示事务；真实权限、射频和唤醒留待集中联调。
- 资源与二进制 RPC：实现现有 Mac 桌宠和 112×112 音乐封面的 RLE565/CRC/1024 字节分块，按设备 ACK 停止发送。USB 专用资源命令、BLE 独立资源帧与状态/RPC 交替调度。T5R1/T5R2、49152 字节原始数据和 65535 字节邮箱有独立边界校验；Windows AES/HMAC 和实际固件邮箱七种长度互验通过。
- 资源模块 `86f242d` 在 [Mac CI 39275017824](https://github.com/yaoyouzhong/AI-bot/actions/runs/39275017824) 通过 66 项测试和 Release 编译。
- 图库与 OTA：ZIP 按安全的单层文件名逐项读取，不向归档提供的路径解压；验证全部 SHA、JPEG 尺寸、分页和已核实导读。切换前保留旧图库备份。OTA 对齐已发布 .234 源码，先校验 P4 镜像结构、项目、SHA 与说明；只显式提供和开始升级，HTTP 分块等候 TCP 背压，USB/BLE 使用现有 RPC。精确 ELF、VALID 分区和三个递增实时样本才构成启动通过。

对应固件参考为公开 `AI-bot-tab5-0.2.234-ui-source.zip`，SHA-256 `c107e5f929e757a6342bea659f84e726a40d5f16b692441865f348fa26290890`；仅在忽略的 artifacts 中只读对照，未修改或重建固件。

- 图库/OTA `d75f7f9` 在 [Mac CI 39277128869](https://github.com/yaoyouzhong/AI-bot/actions/runs/39277128869) 通过测试及 Release。修复 Swift 5.10 测试夹具的 Digest 集合类型兼容性，没有弱化校验。
- 电脑/音乐：新增默认关闭的授权入口、CoreAudio 实际音量/静音读写、白名单快捷启动、Music/Spotify 单次操作和只读状态确认。请求 ID 的结果缓存覆盖失败与不确定结果，冲突 ID 不执行。未知锁定/屏幕状态保持 null，Mac 不通告一键解锁。Apple Events 在主线程执行；同时修复原音乐服务默认 UserDefaults 参数引起的跨 actor 警告。
- 音乐改动的 CI 同时生成临时签名候选 App、校验签名及内置刷机工具，不安装或部署。实际自动化提示、播放/暂停和锁屏权限验证按用户安排留待次日真机联调。

- 电脑/音乐 `5c06b75` 的 [Mac CI 39278118225](https://github.com/yaoyouzhong/AI-bot/actions/runs/39278118225) 附件确认 78 测试，0 失败、0 跳过；Release、签名 App、内置工具帮助检查通过。
- 首轮后续模块 CI 揭示私有随机数辅助方法不可跨文件访问，已用系统安全随机数 API 修复。综合 `f3dba8c` 的 [Mac CI 39281930425](https://github.com/yaoyouzhong/AI-bot/actions/runs/39281930425) 附件确认 99 测试及 Release/App 通过，0 失败、0 跳过。包括照片、语音、16 MiB 首刷、原机备份/恢复及失败不写入流程；无真实设备写入。
- 增加真实子进程 stdio 握手测试后，发现 Mac `read(upToCount:)` 等待缓冲区导致短 JSON-RPC 超时，改用当前可用数据读取。`e53eea3` 的 [Mac CI 39282804628](https://github.com/yaoyouzhong/AI-bot/actions/runs/39282804628) 确认 105 测试及 Release/App/工具/原生视图渲染通过，0 失败、0 跳过。
- 原生渲染另发现 AppKit `document` 属性命名冲突和透明背景影响检查，均已修复；`47a2648` 的 [Mac CI 39283265532](https://github.com/yaoyouzhong/AI-bot/actions/runs/39283265532) 已生成可读 PNG，逐张查看连接、权限/交互、资源、首刷与显示设置表单，控件未重叠或裁切。最后收紧表单行高，避免固定页行伸展。PNG 使用固定数据，不是真机画面或权限验收。
- 最后补充：Codex 目录失联超过 30 秒保留可见缓存，但禁止新发送/桌面写入；阅读回执重新核对当前终态轮次。HTTP 设备操作放到有界后台队列，慢 Codex 请求不阻塞日常状态连接；新增真实回环 HTTP 并发回归。最终提交结果以 CI 附件和本地 `verify_mac_tab5_ci_artifacts.py` 复核为准。

候选使用、权限、版本差异与集中真机清单见 [双语指南](../MAC-TAB5-CANDIDATE.md)。代码保存在独立测试 worktree/测试分支，不覆盖原始 checkout 的 Windows/固件未提交改动。远端 main 未更新。

English: the linked CoreBluetooth module run passed 60 tests and Release compilation. Resource transfer reuses existing Mac selections/artwork, device CRC acknowledgements and the unchanged binary RPC formats; the actual firmware mailbox passes seven size scenarios up to 65535 bytes. Hardware acceptance remains pending.

All five stages now have candidate implementations and module CI evidence. Integrated runs reached 99 then 105 tests with no failures/skips, Release and ad-hoc app/tool checks. Actual child-process testing exposed and fixed short-pipe buffering. Native AppKit fixture PNGs were inspected after correcting transparent backgrounds; they are not physical acceptance. The final regression adds stale-action blocking, current-turn read receipts and live status during slow Codex requests. No main update, installation, hardware write, deployment or publication occurred.
