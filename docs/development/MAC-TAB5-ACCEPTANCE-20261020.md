# Mac TAB5 现场验收 / Local acceptance — 2026-10-20

接续 [交接文档](../MAC-TAB5-HANDOFF.md)，在测试分支 `codex/mac-tab5-phase1-ci` 执行。初始 HEAD 为 `bf16f1f9becd172407c51297fadb0b0bce57fb8f`，工作区干净，远端测试分支与本地一致。用户确认 TAB5 已连接 Mac，Windows 桥接已退出或暂停。

本轮只进行本地候选运行、测试、联调与修复；不合并或推送 main、不安装常驻服务、不部署、不发布、不写入 TAB5 固件。真实发送、追加或中断 Codex 任务仍须指定测试任务。TAB5 展示设置保持原值。

## 当前检查点

当前已验程序为 `7ad1c24`，具体修复、CI 与操作证据见下文。较早段落中的“待验”描述保留其当时上下文，不覆盖后续明确结果。

| 交接阶段 | 当前结论与剩余项 |
| --- | --- |
| 1 本机基础 | Release、候选签名、正常运行／窗口／退出与 CI 150 项通过；本机 XCTest 工具链仍受阻 |
| 2 USB | 配对、数据、设置读写、拔插与桥接重启恢复通过 |
| 3 Wi-Fi | 纯 Wi-Fi 认证与设置读写、USB 返回通过；配网、地址恢复、固定模式和八秒阈值精测待验 |
| 4 BLE | 实际认证／数据／设置、开关重连、睡眠恢复、USB 优先、系统撤权恢复通过；首次直接拒绝与精确延时确认待验 |
| 5 展示与资源 | 亮度持久化、设备声音、固定页／轮播／屏保、图库导入／失败保留／分页／方向通过；日常数据逐项、桌宠及音乐均匀计时仍未完成 |
| 6 系统与音乐 | Apple Music 标题／封面／单次控制、系统音量／静音／取消静音、文件夹入口通过；Spotify、程序入口、锁屏／辅助功能拒绝恢复待验 |
| 7 Codex | 已安装 CLI 已定位；公开代理默认 socket 缺失、握手前退出。实际任务链路受阻，未执行发送／追加／中断 |
| 8 照片与语音 | 语音授权、空录音停止提示通过；HTTP 长连接修复后纯 Wi-Fi 单次成功识别，收到 7.4 秒音频；静默提示去抖、USB／BLE 稳定性、准确最终文字及取消仍待复验，照片／任务附件未通过 |
| 9 固件工具 | 官方包离线检查、正常 App 版本／SHA／说明预览、当前镜像只读 USB 启动核验通过；实际 OTA／首刷／恢复均未执行 |

临时音乐页／开关／曲目、系统音量／静音和文件夹入口均已收尾。语音开关已暂停并恢复关闭；用户曾退出未保存日程；最新录音复验重新进入未保存编辑，尚未保存测试内容。当前为隔离语音问题暂时断开 USB、关闭桥接蓝牙，不将整体九阶段写成完成。

## 环境与基础检查

| 检查 | 实际结果 |
| --- | --- |
| 架构 | `arm64`（Apple Silicon） |
| macOS | `27.2`，build `26B5112a`（`sw_vers` 实测） |
| Swift | Apple Swift `6.2.3`，当前 developer directory 为 `/Library/Developer/CommandLineTools` |
| `swift test --package-path mac-app` | **受阻，未执行任何测试**：测试目标编译报 `no such module 'XCTest'`。当前工具链也没有 `xctest`，未找到已安装的 Xcode。此结果不能记为测试通过或应用测试失败。 |
| Release | **通过**：`scripts/build_macos_app.sh` 内执行 `swift build -c release --package-path mac-app` |
| 候选包 | **通过**：Info.plist、固定版本刷机工具包 SHA、ad-hoc 签名及严格签名验证 |
| 内置刷机工具 | **通过**：正常候选包的 `--self-test-flash-tool`，仅帮助/版本检查，无设备访问 |
| 原生界面夹具 | **通过**：`--self-test-tab5-ui` 生成 8 张 PNG；不是正常启动、权限或硬件验收 |
| 构建警告 | Swift 6.2.3 报 `SerialBridge` 跨 `@Sendable` 闭包捕获的既有警告；未靠添加未经证明的 Sendable 声明压制 |
| 正常 App 运行 | **启动、菜单栏、正常退出与重启通过，人工反馈**：用户从访达打开初始候选，确认菜单栏 `C:… A:…`；后正常退出，改开 `f3f9dd6` 候选。新旧进程路径、8765 监听和串口所有者已核对。界面控制工具选择候选 App 和访达均超时，不能据此认定 App 启动失败；其余窗口与权限仍单独验收 |
| 实际 HTTP 鉴权 | **通过**：已运行候选的 `/status` 与 `/tab5/v1/status` 对无认证本机请求均返回 HTTP 401；未读取状态正文或配对材料 |

本机候选与离线证据位于忽略目录 `artifacts/mac-tab5-20261020/`，未安装到 Applications。不得将该目录的本地运行数据加入 Git 或 CI 附件。

初始 HEAD 的 [Mac CI 39353897262](https://github.com/yaoyouzhong/AI-bot/actions/runs/39353897262) 已成功；交接程序基线仍为 `f41a429`。CI 结果不替代本机尚未执行的测试。

## USB 与设备

| 检查 | 实际结果 |
| --- | --- |
| 端口 | `/dev/cu.usbmodem101` |
| USB 描述符 | `TAB5 Control`，vendor `TAB5 development`，VID `0x303a`，PID `0x4005` |
| 只读身份检查 | **通过**：以 `460800` 发送一条 `@AIBOT ` v1 `tab5_ping`，收到目标 `tab5_hello`；未配对、未改 Wi-Fi、未写固件 |
| 实际固件 | `0.2.234-ui` |
| 能力 | `rpcVersion=1`、`rpcUsbBinary=1`、`usbPacked=1`、`usbDelta=1` |
| 初次 UI 诊断样本 | `uptimeMs=56152410`、`uiAgeMs=13`、`flushCount=34381`、`flushErrors=0`；单份样本不构成启动健康验收 |
| 正常 App 配对、状态 ACK | **通过，人工反馈**：用户明确选择端口并配对，窗口显示 USB“数据已确认”；真实屏幕数据仍待观察 |
| 实际数据与屏幕 | **基本画面正常，人工反馈**；具体 Mac 数据、页面与触摸仍待逐项观察 |
| USB 拔插 | **通过，人工反馈**：断开约 12 秒后重新连接，未点击重新配对，USB 自动恢复“数据已确认” |
| 桥接正常退出与重启恢复 | **通过，人工反馈**：从初始候选切换 `f3f9dd6`，未重新配对，自动恢复 USB“数据已确认” |

显示设置的真机读取已通过。首次保持值不变保存时，设备仍处于本机设置页，收到“TAB5 正在编辑本机设置，请退出设备设置页后重试。”，记录为设备忙碌拒绝，未记为保存成功。用户退出设备设置页后，重新读取并原值保存，收到“TAB5 已确认保存显示设置。”，确认匹配成功回执；未改变用户配置。亮度、声音及重启保持的后续实机结果见下方；其他显示设置仍逐项验收。

不将真实设备配对材料、网络配置或原始串口流写入记录。ESP8266 未连接或执行硬件验收，不宣称通过。

## 显示设置与设备声音实机验收

在 `791d898` 正常候选中，先读取设备实际设置，并仅将恢复基线保存在忽略的本机证据目录，不提交用户完整配置。

- **亮度效果、正常开关机持久化及恢复通过，人工反馈**：只降低亮度，收到匹配保存回执，实体屏幕明显变暗。用户通过正常电源操作关机再开机后，USB 自动恢复“数据已确认”，画面正常；重新读取确认临时亮度值仍被保留。随后恢复原亮度并收到保存成功，用户确认屏幕亮度恢复。未进入下载模式、未写入固件。
- **设备声音开启试听通过，人工反馈**：仅降低 TAB5 设备提示音量，保持非静音，收到保存回执；在实体“设置 → 屏幕声音”点试听，用户明确听到提示音。此项不涉及 Mac 系统输出音量，也没有将“听到”推断为音量精确比例正确。
- **设备静音设置与试听禁用通过，人工反馈**：Mac 勾选静音保存成功后，用户看到实体试听按钮变灰。公开 `.234` 设备端 `sound_refresh` 在静音或音量为零时禁用试听，与观察一致；不将禁用按钮写成已执行的无声试听。随后恢复原设备音量并取消静音，用户确认保存成功。其他显示与资源结果分别记录如下。
- **固定页切换通过，人工反馈**：仅将固定页临时改为时钟，收到保存成功，实体 TAB5 切到时钟页。
- **轮播页面与切换通过，人工反馈**：临时启用两个页面、调整顺序并选择十秒间隔，用户确认保存成功，随后观察反馈“正常在切换”。加入第三个页面后，针对指定三页顺序的观察请求，用户再次反馈正常切换。没有仪器测量精确间隔，也没有单独取得第三次保存回执的原文，不扩大为精确计时验收。
- **屏保进入与触摸退出通过，人工反馈**：恢复原固定页、页面勾选、顺序、间隔及轮播开关后，仅临时缩短屏保等待，收到保存成功。无触摸／键鼠操作约七十秒的观察请求后，用户确认自动进入屏保；轻触后反馈正常退出，具体返回页和亮度没有另行详述。随后恢复原屏保等待并收到保存成功，本轮临时显示设置均已按基线恢复。

## 图库导入、分页与方向

`791d898` 正常候选导入已校验的官方名画 ZIP，用户确认成功导入 366 件作品；只读核对启用的清单与官方包逐字节一致（826 张图）。这与此前仅在隔离目录做的离线验证分开记录。

当天作品只有单页，不能据此宣称图片翻页通过。因此从同一官方包复制一件明确标为 CC0 的多页作品，保留原始图片、来源、许可和导读，生成仅在忽略目录使用的临时验收子集（1 件作品，横竖屏各 10 页，共 20 张图）；它不是正式交付包，不改系统或设备日期。用户在正常 App 成功导入，实体选择名画预览后确认图片、翻页箭头和页码正常，前后翻页也正常。随后竖屏翻页再转回横屏，用户确认两种方向的图片、文字与触摸均正常，无黑屏或异常裁切。

仅改动一字节图片、保持清单 SHA 不变的本地错误包，在隔离加载器与正常 App 中都被拒绝。正常 App 提示“图库文件校验失败”；前后 28 个已安装资源文件的 SHA 全部一致，用户随后明确确认实体仍可正常翻页。失败保留旧图库与可用画面通过。临时子集、错误包和来源文件均未加入 Git。用户随后重新导入完整官方包并确认成功；只读核对清单恢复为 366 件作品、826 张图且与官方包一致。最后用户恢复原屏保类型和自动旋转，退出设备设置页回到总览，临时资源与显示设置收尾通过。

## 本轮修复：音乐读取失败保留成功缓存

源码复核发现，自动化权限拒绝、超时或无效歌曲结果都返回空读取；原轮询将其计作无播放，累计三次后清空最近成功的歌曲和封面。使用真实 `MacMusicService` 编译对象与合成读取后端，已复现四次读取失败清除预置成功缓存。此检查没有启动音乐 App 或发送 Apple Events。

轮询现在区分读取失败与已确认停止／程序退出：若没有成功选中的歌曲且任一运行中播放器读取失败，保留最近成功数据和封面，并重置连续空样本计数；连续三次已确认无播放仍清理，用户明确关闭音乐功能仍立即清理。播放器选择顺序、Apple Events、权限、单次控制与读回逻辑保持。四个回归覆盖连续失败、另一播放器停止时仍有失败、失败打断空样本计数，以及明确关闭不探测播放器。

独立本机检查已确认修复后缓存保留、已确认退出仍清理、明确关闭仍清理且不探测；Release 构建通过。它不代替 XCTest 或真实音乐验收。本机 `swift test` 仍在编译测试目标时报缺少 XCTest，没有测试执行。程序提交 `50240016ba810f1f302a6e77e598f7b8c7ffbcf8` 的 [Mac CI 39362895479](https://github.com/yaoyouzhong/AI-bot/actions/runs/39362895479) 与完整下载附件核验通过：122 项测试，0 失败、0 跳过，Release、候选包、工具、8 张原生 PNG、源码与内外层包哈希一致。本机候选包及签名通过，位于 `artifacts/mac-tab5-20261020/5024001/AIBotBridge.app`。新正常候选的自动化授权、播放／暂停实机回归待继续。

## 本轮修复：音乐播放控制遵循独立授权

进入音乐实机验收前，用户确认电脑控制与音乐两个开关原本均关闭。新候选 `5024001` 启动后，USB“数据已确认”、蓝牙“已认证，USB 优先待命”；只读进程核对仅该候选运行，无认证的两个状态路由仍返回 HTTP 401。用户启用音乐后看到自动化授权弹窗，但因没有订阅，尚未播放曲目；没有将弹窗出现写成播放通过。已准备带原创几何图形封面的原创本地测试音频，不需要订阅，不读取用户现有媒体；导入、播放与后续从资料库移除分别跟踪。

另一个独立复现显示：仅打开音乐开关时，`computer-control` 的全局电脑开关检查返回 403，播放动作没有执行，且状态未通告共有控制通道，与界面“允许读取并控制音乐”不一致。现在音乐开关单独授权媒体动作，并在它或电脑开关启用时通告已有控制版本。音量、静音、锁屏和常用入口仍只由电脑控制开关授权；音乐单独启用时隐藏这些能力和数值，电脑单独启用时隐藏媒体控制。请求去重、执行前权限复核、实际播放器绑定和结果读回保持。现有 v1 消息和字段未改变，已对照 TAB5 的能力标志消费逻辑。

四个回归覆盖音乐独立控制及电脑动作拒绝、反向授权隔离、撤销后拒绝旧请求，以及共有通道能力随开关变化。使用实际编译对象的合成后端检查先复现 403，再确认媒体动作成功且音量写入仍拒绝；未发送真实 Apple Events。Release 通过；本机全量 XCTest 仍缺少模块、未执行。程序提交 `c51a1bb8aa59afcaea9cd2735071da96b0fb8b72` 的 [Mac CI 39364551773](https://github.com/yaoyouzhong/AI-bot/actions/runs/39364551773) 及完整下载附件核验通过：126 项测试，0 失败、0 跳过，Release、候选 App、工具、8 张原生 PNG、源码 SHA 与包哈希一致。本机候选 `artifacts/mac-tab5-20261020/c51a1bb/AIBotBridge.app` 构建、随包工具与签名检查通过。正常 App 的后续自动操作见下节；实体音乐显示、播放／暂停及拒绝恢复仍待继续，没有将 CI 写成实机控制通过。

## 恢复 Mac 界面自动化后的音乐实测

用户提出应尽可能自动操作 Mac，避免将常规导入和窗口操作全部交给人工。重新检查后，Computer Use 已可读取和操作音乐、桥接设置与系统活动监视器窗口；此前的超时不再作为所有 Mac 操作均需人工的理由。系统授权和实体 TAB5 的屏幕／声音／触摸仍与软件自动检查分开记录。

已通过正常音乐 App 的“文件 → 导入”自动导入只含原创测试 M4A 的文件夹，歌曲列表确认只有该测试曲目，时长十分钟。自动播放后，实际窗口显示测试封面和暂停按钮，展开播放中视图后确认经过时间已推进。部分无障碍滑块标识仍为静态“未在播放”，未单独把这些标识当作播放器真值。测试曲目无需订阅，未读取其他用户媒体；资料库临时条目仍待收尾移除。

正常 `5024001` 候选中，自动重新读取显示设置，仅临时切换到音乐页，已从固定底部读到“TAB5 已确认保存显示设置。”。实体曲名、封面和触摸控制尚无反馈，不将 Mac 封面或保存回执算成设备显示通过。本阶段 fresh read 的显示模式与较早人工基线不同，已在忽略目录另存本阶段恢复值；后续只恢复本阶段改动，不凭旧记录覆盖当前设置。

随后通过活动监视器对精确匹配的旧候选选择“退出”，未选择强制退出；确认旧桥接进程消失后，用 Computer Use 启动精确 `c51a1bb` 候选。该方式按 [Apple 说明](https://support.apple.com/en-ie/guide/activity-monitor/actmntr1002/10.14/mac/15.0) 走正常退出流程。新进程存在，但系统安全授权尚未完成、状态监听尚未启动，不能记为新候选启动或通道恢复通过。Computer Use 明确禁止访问 SecurityAgent，已要求用户仅在 Mac 上处理匹配的钥匙串提示并打开一次 TAB5 窗口；没有读取、输入或记录密码。常规窗口操作将在此后继续由助手完成。只读进程采样进一步确认主线程仍在 `SecItemCopyMatching` 等待系统钥匙串服务；只检查调用栈，不读取钥匙串内容。测试曲目已自然播放结束一次，未打开循环播放。

## 本轮修复：音乐进度回退与控制延迟

用户完成 `c51a1bb` 系统授权并打开 TAB5 窗口后，自动读取确认 USB 数据、Wi-Fi 认证请求和蓝牙 USB 优先待命均恢复；唯一桥接进程路径正确，未认证的两个状态路由均返回 401。音乐开关保持开启，电脑控制关闭。原创测试曲目自动重新播放后，用户确认实体曲名与蓝橙封面正常；实体各点击一次暂停、播放，用户反馈状态切换，Mac 正常音乐窗口也分别显示已暂停／播放中与相反动作按钮。因此封面显示和单次控制效果通过。

用户随后明确报告控制延迟、进度跳秒及往回跳，音乐计时验收记为未通过，不能由前述控制效果覆盖。源码确认原音乐轮询与总状态发布均为两秒，轮询异步启动但总状态立即发布旧缓存；USB 还会重复发送该缓存，且 Apple Events 将进度取整。公开 `.234` 的界面在每次收到状态后，以该进度重新开始本地计时，故重复旧采样会造成回退。控制后实际播放器读回原来仅用于判断成功，没有立即回填状态。

修复保留小数进度，独立每半秒轮询（同一轮未完成时不重入），取包时使用最新音乐采样，并按采样时间投影到实际发送时刻。仅最近三秒的有效时钟允许推进，过期／未来采样沿用既有 `timelineAvailable=false` 隐藏未知进度，保留最近成功曲目与封面。暂停不推进，真实回退跳转仍按播放器值生效。确认的控制读回立即更新音乐缓存；USB 对曲目、播放状态和真实跳转优先发送，普通进度保留原状态传输频率。旧轮询和延迟封面不能覆盖较新控制读回，也不能将旧曲封面挂到新曲。没有新消息类型、协议版本或设备固件变更。

新增八项回归覆盖重复帧的设备计时契约、暂停／真实跳转、过期／未来采样、小数及曲末边界、USB 优先条件、迟到元数据／封面，音乐绕过旧总状态缓存，以及曲名与封面同步取自同一最新采样。使用实际编译对象的独立检查在修复前复现重复位置，在修复后确认重复发送按采样年龄前进且不修改原缓存；没有访问设备或发送 Apple Events。本机 Release 通过，全量 XCTest 仍因缺少模块未执行。新候选的正常 App 播放／暂停、计时与延迟实机复验待继续；此时仍运行 `c51a1bb`。

`754fd02` 正常候选已自动构建、签名、正常退出旧候选并启动，用户完成匹配系统授权；自动检查再次确认 USB、Wi-Fi 认证与 BLE 待命恢复、唯一精确进程和两个未认证状态路由 401。其 [Mac CI 39367861614](https://github.com/yaoyouzhong/AI-bot/actions/runs/39367861614) 与完整下载附件核验通过：134 项测试，0 失败、0 跳过，Release／App／工具、8 张原生 PNG 和源码／包哈希一致。

该候选的三十秒观察首先得到“仍有回退或明显跳秒”，进一步询问后用户明确为“只向前跳秒，没有往回走”。因此本次观察未再报告回退，但向前跳秒仍未通过，不能写成整体计时通过。继续对照设备将整秒基值与收包后的整秒增量相加的逻辑，发现 USB 原来在 ACK 完成后再等两秒，传输／回调耗时会累积到发送相位。修复为按原截止时刻推进发送计划；控制变更另起相位，过长阻塞后跳过错过的时隙，避免补发突发。新增两项回归覆盖多轮 ACK／回调延迟不累积、控制及长阻塞。合成相位模型可出现短时间相邻秒变化，固定相位消除该模型现象；它不是真机计时证据，实机复验仍待继续。

固定节奏程序提交 `3c82bcce4df49af2b3e1c64445664500eabf7a43` 的 [Mac CI 39368358000](https://github.com/yaoyouzhong/AI-bot/actions/runs/39368358000) 与完整附件核验通过：136 项测试，0 失败、0 跳过，Release／App／工具、8 张原生 PNG、源码与包哈希一致。实际编译对象的独立节奏检查也通过二十轮不同 ACK 耗时、控制另起相位及长阻塞不突发；没有访问设备。本机 Release、候选与签名通过，XCTest 仍缺少模块、未执行。

助手通过正常退出切换到精确 `3c82bcc` 候选，用户完成系统授权并确认打开 TAB5 窗口；唯一精确进程和两个状态路由的未认证 401 已自动核对。该轮桥接窗口的界面工具选取连续超时，重建工具连接也未恢复，不能擅自写入 USB／BLE 状态文字已复核。只读进程调用栈包含正常 AppKit 事件循环、音乐轮询与 USB 工作，未证明持续卡死，也没有读取钥匙串内容。音乐 App 的自动操作仍可用，测试曲目已重新播放。

实体四十五秒观察后，用户反馈“不跳秒了，但是时间前进的不均匀”。因此跳秒现象改善，但音乐均匀计时仍未通过。随后实体分别点击一次暂停、播放，用户均反馈一秒内生效；Mac 音乐窗口也分别确认已暂停／播放中，以及相反动作按钮。这是人工估计的响应时间，不是仪器测量。对应 `.234` 固件音乐计时的整数截断／每包重设相位逻辑、合成回放证据及后续修正要求，见[音乐计时固件后续项](MAC-TAB5-MUSIC-TIMING-FOLLOWUP.md)；未修改或写入固件。

用户将 TAB5 窗口置前后，界面工具恢复选取，自动核对 USB 数据确认、Wi-Fi 已认证请求和蓝牙 USB 优先待命。随后重新读取设备设置，只把本阶段临时音乐页恢复为进入本阶段时的页面模式，收到固定底部匹配保存回执；轮播及其他值未改。音乐开关恢复关闭，电脑／Codex／语音开关仍关闭。测试曲目先暂停，再通过 Music 的正常资料库删除流程移入可恢复的废纸篓，歌曲列表不再含该条目；原创源文件留在忽略目录，未清空废纸篓。Music 正常退出已用进程检查核对。本阶段音乐临时设置和媒体收尾完成；系统权限授权保持原系统管理方式，未放宽钥匙串 ACL。

## 正常 App 的固件预览与只读启动核验

在同一 `3c82bcc` 正常候选中，自动从“资源与固件”选择官方 `.234` 升级 ZIP。窗口确认版本 `0.2.234-ui`、精确应用 SHA-256 `1c4fa6261243d3d1d9ac5c5fa54ac6e005f3988ebab9b125ddc5358c37a4f4c8` 和含分类标题／列表的实际 sidecar 说明，并明确显示尚未开始升级。随后只点击“核验所选固件的启动状态”，收到“USB 启动核验通过：精确 ELF、VALID 分区和三个实时界面样本一致。”。这是已运行镜像的只读核验，不是本轮写入／OTA 验收，也没有新的屏幕触摸观察。最后点击“停止提供所选固件”，确认“尚未选择固件 / 已停止提供固件。”。没有点击请求开始升级、重启或 Flash 写入。

## 本轮修复：重新打开与窗口快捷键

窗口置前可恢复界面工具，但关闭子窗口后工具再次无法选取桥接，`Command-W` 也没有关闭窗口。源码中的 accessory App 没有重新打开处理或主菜单。补充正常的 App reopen 委托：再次打开已运行的 App 时显示 TAB5 主窗口；补充标准窗口菜单、`Command-W` 关闭和 `Command-Q` 退出。关闭仍由窗口既有忙碌保护决定，退出仍经过现有 App 委托保护；没有自动批准权限、外部控制接口或部署变更。

本机 Release 编译通过，既有 SerialBridge Sendable 警告未变；本机 `swift test` 仍因缺少 XCTest 模块在测试执行前受阻。此前文档提交 `61e86ac` 的 [Mac CI 39374024338](https://github.com/yaoyouzhong/AI-bot/actions/runs/39374024338) 和完整附件核验通过 136 项测试、0 失败／跳过，Release／App／工具、源码和包哈希一致；窗口修复的 CI 与正常 App 检查另行记录。

程序提交 `3db70a3dece89c39acd71743007b8213ae366600` 的 [Mac CI 39374705006](https://github.com/yaoyouzhong/AI-bot/actions/runs/39374705006) 和完整附件核验通过：136 项测试，0 失败／跳过，Release／App／工具、8 张原生 PNG、源码与包哈希一致。本机正常候选构建／严格签名检查通过；用户处理匹配的钥匙串授权后，助手自动打开 TAB5，核对实际三通道状态与未认证 401。`Command-W` 关闭交互子窗口并回到主窗口通过；标准窗口菜单包含关闭与 TAB5 入口；`Command-Q` 退出后旧 PID 消失。随后自动启动同一候选，首次选取因尚无窗口超时，再次打开已运行 App 即自动显示 TAB5；无需用户手动打开窗口，USB 随后自动恢复数据确认。保留菜单栏 App 首次启动不主动弹窗的行为，不能据此宣称所有工具超时均已消除。

## Mac 系统音量与静音实机验收

在正常 `3db70a3` 中自动启用电脑控制，音乐仍关闭。使用实际产品的 CoreAudio 只读路径取得操作前音量／静音恢复基线，不通过测试后端发送控制。用户在实体“应用 → 控制”拖动后反馈 17%；Mac CoreAudio 读回与正常系统声效面板均为 17%。实体分别点击一次静音、取消静音后，用户确认按钮切换，CoreAudio 分别读回 true／false，原音量保持。因此真实 TAB5 → Mac 的音量、静音及取消静音控制通过；没有用模拟请求代替实体触摸。

助手随后通过正常系统设置恢复操作前音量，CoreAudio 再次确认音量和非静音均已恢复；正常退出临时打开的系统设置，关闭电脑控制开关并核对其他开关保持关闭。重新读取设备显示设置与阶段恢复值一致，只取消表单，不再保存。此项与 TAB5 自身提示声音、锁屏／辅助功能、常用入口、Spotify 分别记录。

**常用文件夹入口通过**：原入口列表为空，助手在正常 App 临时添加本轮新建的空目录 `TAB5-LaunchTest` 并启用电脑控制。用户在实体控制页点一次该入口后，访达从原候选目录窗口切为该测试目录，显示零个项目；未直接在 Mac 打开该目录来替代设备操作。随后关闭测试访达窗口、从正常 App 移除唯一临时入口并关闭电脑控制，确认列表再次为空，最后移除助手创建的空目录。应用程序入口、锁屏／辅助功能和权限拒绝恢复仍单独待验，不以文件夹打开代替。

## 语音授权与提前中断排查

正常 `3db70a3` 中由助手触发语音授权，用户完成系统确认，窗口与人工反馈均为“语音权限已授权；开始时仍会检查本地语言模型是否可用。”。电脑、音乐和 Codex 开关保持关闭。日程名称使用设备已有的独立语音 owner，不需要启用或发送 Codex 任务；测试只进入未保存的名称编辑。

用户报告能识别，但多次在尚未来得及说话时自动结束，提示连接中断，因此稳定性未通过，未取得准确最终文字，不写成完整识别验收。对照实际 `.234` 源码，`voice_remote.c` 在请求没有可接受回复时进入连接中断／恢复；Mac 识别器回调错误则使用另一条“识别中断”提示，不能将两者混为静音结束。

助手先在正常 App 关闭语音、正常退出桥接，确认无进程／串口所有者后，以 460800 只发送一次 `tab5_ping`，读取并仅输出白名单数字／阶段码：最近语音请求 2 ms、seq 24，USB RPC 历史计数 83、最近阶段 1、状态 0／零回复字节。计数覆盖此前运行，不足以对应每次失败或锁定竞争／重置的根因。未记录原始串口流、音频、识别文字或配对材料，未重置／写入设备。随后正常恢复同一候选，等待 USB 数据确认后才启用语音。

一次三秒静默、手动停止的定点检查得到“识别中断，文字可能不完整，请检查后重新录入”；用户明确它在点击停止后才出现，此轮没有复现提前结束。两个 Mac 窗口采样均显示 USB 数据确认，不代表覆盖所有瞬时变化。原实现丢弃 NSError 的领域／编号，将所有识别错误合为同一提示；[Apple 错误说明](https://developer.apple.com/documentation/speech/sfspeechrecognitiontask/error) 将无语音、资源、授权和服务故障分开列出。

修复保留受控错误分类：只有用户明确停止、没有部分文字且得到无语音结果时，进入不能插入的空预览；提前结束、连接超时或未确认的部分文字仍失败。资源／权限／识别器忙碌／本地服务连接与未知数字错误码分别提示，不显示原始错误描述。交互窗口新增收音／本地识别／待检查／错误状态，不显示转录、任务或设备标识，便于助手直接核对。协议字段、强制本地识别及停止／取消界限保持。

新增五项回归覆盖空录音停止、提前无语音、部分文字不确认、按领域分类、连接超时不伪装为手动停止，以及窗口摘要不泄露转录／错误原文。本机 Release 通过，XCTest 仍缺少模块、未执行；新正常 App／CI 复验另记。当前语音开关已恢复关闭；原始连接中断未被此错误提示修复宣称解决。

### `b012fe6` 复验

程序提交 `b012fe63861d1f524607bcbec045472252ad2810` 的 [Mac CI 39376374679](https://github.com/yaoyouzhong/AI-bot/actions/runs/39376374679) 与完整下载附件核验通过：141 项测试，0 失败／跳过，Release、候选 App、工具、8 张原生 PNG 和源码／包哈希一致。新增语音状态行在完整原生截图及正常 App 中可见，没有遮挡后续控件。本机 XCTest 工具链限制不变。

正常退出旧候选后自动启动该精确路径，用户处理同类钥匙串授权，助手自动打开 TAB5 并核对 USB 数据确认、Wi-Fi 已认证请求及蓝牙 USB 优先待命。随后自动启用已授权的本地语音，其他控制开关保持关闭。一次三秒静默、手动停止后，用户反馈“没有识别到文字，请准备好后重录。”；Mac 状态也为“没有识别到文字，请重新录入”。本次空录音提示通过，没有把它记作原提前连接中断已修复。随后正常说话复验中，用户反馈“要么提示未检测到声音，要么提示已取回识别文字，本段可能不完整，请检查后补录”，并明确无法从头到尾录完。因此正常录音仍失败；Mac 曾显示识别结束，不能据此覆盖设备不完整结果。语音开关再次关闭，未保存日程。

### 晚到音频覆盖识别终态的竞争

继续对照调用方与协议对端：设备语音工作线程只接受成功状态的回包，拒绝回包会进入断流恢复；Windows 的既有 `audio` 分支在非收音状态返回原状态。Mac 原实现却将非收音状态合并进 `invalid_voice_audio`，因此识别器在两块音频之间结束后，仍在途的下一块会被返回 400，设备只能恢复取字并提示可能不完整。链接本机实际 debug 对象的独立合成检查已复现 `status=400`；它没有访问设备、麦克风或 Speech 服务，不当作实机根因的唯一证据。

最小修复在既有启用、设备／voiceId／session 校验之后，对非收音状态的晚到 `audio` 返回当前状态，不再投喂音频、不重开采集、不替换结果或错误。新增三项回归覆盖最终／错误状态、停止／取消和权限／原会话隔离；实际录音中的其他提前结束原因仍需新候选复验。没有协议、Windows 或固件变更。修复后同一实际对象合成检查通过最终／错误回包及不重复投喂。本机 Release 通过，测试源码语法检查通过；`swift test` 仍因缺少 XCTest 在执行前受阻，完整回归交由 Mac CI。

暂停语音、正常退出并确认串口独占后，又只读 ping 一次：最近请求 83 ms、seq 27，USB RPC 历史失败计数由 83 增至 91，最近仍为阶段 1／状态 0／零回复字节。这显示另有未收到回包的历史事件，不能以终态回包修复概括所有连接中断；它也不能精确关联用户每次试录。未输出原始流、音频、转录或配对材料。

### `4466022` 的实际结果与接收诊断

程序提交 `4466022f40cf379a6cf0c3dc0c5aa10f04f89086` 的 [Mac CI 39377149298](https://github.com/yaoyouzhong/AI-bot/actions/runs/39377149298) 与完整下载附件核验通过：144 项测试，0 失败／跳过，Release／候选／工具、8 张 PNG 及源码／包哈希一致。正常退出旧候选并启动该精确路径后，用户完成匹配钥匙串授权，助手自动打开窗口核对 USB、Wi-Fi 和 BLE 待命；两个无认证状态路由均返回 401。

用户在完整句子复验中仍反馈连接中断或录音中未检测到声音，完整录音未通过。为隔离通道，助手关闭语音和桥接蓝牙，用户断开 USB；端口确实消失，正常 App 显示 USB 未确认／Wi-Fi 已认证／蓝牙关闭。纯 Wi-Fi 读回设备实际设置成功，值仍为自动页面、轮播关闭、亮度 79、设备音量 70、非静音、15 秒／5 分钟、原页面顺序和选择；表单只取消，不保存。随后用户的纯 Wi-Fi 单次录音直接提示“连接已中断，收音已停止”。因此不能将全部故障归因于 USB。语音再次关闭，用户已退出未保存日程编辑回到日常页面。

链接实际 debug 对象、注入合成配对和识别后端，自动验证真实 HTTP 解析／路由／证明校验／解密／RPC：开始、连续 50 块 IMA 音频、停止和最终回包均通过。没有监听端口、真实设备、麦克风、Speech 服务或私人配对访问；此结果不能代替现场连接验收。

下一步增加仅本机窗口的接收诊断：已接收块数／时长、最近及峰值电平、固定操作名与受限数字状态码；新录音清空旧峰值／失败原因，关闭语音隐藏诊断。不展示或保存音频、转录、设备／任务／会话标识或认证材料，不增加协议字段或未认证诊断接口。新增回归覆盖去重计数／峰值、新录音重置、固定标签与关闭隐藏，以及 HTTP 鉴权失败可见性；本机 Release、测试语法检查及实际对象的 50 块完整 HTTP／诊断检查通过；XCTest 仍在执行前受阻。正常候选和新 CI 结果另记。

### 零音频现场与语音 HTTP 连接修复

程序 `888086b22fca492cc7f81ce0518873499ed6b901` 的 [Mac CI 39377742095](https://github.com/yaoyouzhong/AI-bot/actions/runs/39377742095) 与完整附件核验通过：147 项测试，0 失败／跳过，Release／App／工具、8 张原生 PNG、源码／包哈希一致。正常退出旧候选后启动精确新路径，用户处理匹配钥匙串提示，助手自动接管窗口；纯 Wi-Fi 已认证、USB 不在、BLE 关闭，两个未认证状态路由仍为 401。

新的一次实体日程录音仍显示“连接中断，收音已停止；重连后取回文字”。Mac 正常窗口实际捕获：已接收 **0 块／0.0 秒**，最近／峰值电平均为 0，恢复停止与后续查询为 Wi-Fi 200，没有记录到被拒请求。此处零电平只证明没有音频到达，不能据此判断实体麦克风损坏。动态诊断布局正常、没有遮挡后续控件。语音随后关闭，未保存日程，当前再次处于未保存编辑供后续复验。

进一步只读对照：设备使用同一 HTTP client 连续执行开始／音频／停止，原 Mac 每次回包均为 `Connection: close` 并取消 socket。普通设置读写只需单次请求，不能覆盖这条连续流。使用实际服务器与合成配对／后端、不会自动重连的 loopback TCP 客户端，旧实现复现连续交换失败；修复后同一个 socket 成功完成开始、50 块音频和停止。此证据支持纯 Wi-Fi 的连接生命周期兼容问题，不将其外推为所有 USB 中断已解决。ESP HTTP 客户端的持久连接及显式关闭规则可参见 [官方说明](https://docs.espressif.com/projects/esp-idf/en/v5.3/esp32/api-reference/protocols/esp_http_client.html)。

Mac 仅对 HTTP/1.1、成功且逐次鉴权的语音 POST 保持顺序连接；每次重新解析及检查 nonce／证明，失败关闭。显式 `Connection: close`（含逗号列表）、HTTP/1.0、其他路由保持关闭。每连接最多 640 请求／120 秒，读取空闲上限仍为 10 秒；既有请求体／队列上限及组合报文拒绝规则保持。新增真实 loopback socket 回归覆盖同连接多块／停止、后续错误证明关闭、显式关闭／HTTP1.0和普通状态路由关闭。没有自动重发音频或改动固件／协议字段。新正常候选与 CI 另记。

### 长连接实机成功与静默提示去抖

程序提交 `7ad1c240e27ca82e9b633f2aacf82596f32ada6a` 的 [Mac CI 39378349053](https://github.com/yaoyouzhong/AI-bot/actions/runs/39378349053) 及完整附件核验通过：150 项测试，0 失败／跳过，含真实 TCP 回归；Release／候选／工具、8 张原生 PNG 与源码／包哈希一致。本机 Release、候选严格签名及随包工具检查通过，本机 XCTest 仍在执行前受阻。正常退出旧候选、启动该精确路径并由用户处理同类钥匙串提示后，助手自动接管，核对纯 Wi-Fi、其他开关关闭以及两个无认证路由 401。

本次用户反馈识别成功；正常 Mac 窗口同时显示 **37 块／7.4 秒**音频、最近电平 4%／峰值 100%、最终待检查状态和 Wi-Fi 查询 200，没有记录到拒绝。相较旧候选的零音频，此次持续收音与识别实际通过一次；用户未给出逐字最终文字，因此不宣称准确率或长期／USB 稳定性全部通过。未保存日程。

用户同时反馈收音中频繁“检测不到语音”，影响使用。原静默标志在总收音一秒后，只要当前约 200 ms 块的峰值取整为零就立即触发，正常短停顿也会切换提示。修复按去重后接受的 PCM 时长累计连续低电平，满三秒才设置既有 `silent` 标志；一有信号立即清除，新录音归零，轮询与重复块不推进计时。提示不自动停止录音，音频和本地识别策略保持。新增两项回归覆盖连续静默／短停顿／信号恢复／新会话，以及重复与轮询不累计。实际对象合成 PCM 检查、本机 Release 和测试语法检查通过；本机 XCTest 仍在执行前受阻。下一候选 CI 与实机结果另记。

## 本轮修复：正式固件包的许可证目录

使用当前 Mac 代码的固件加载器读取 SHA 已核对的官方 `.234` 升级 ZIP，实际复现“ZIP 含重复或不安全的文件名”。官方包的七个许可证路径有九段，例如 `licenses/idf/bt/host/nimble/nimble/ext/tinycrypt/LICENSE`；原 `safePath` 最多接受八段，导致合法升级包与同类首刷包无法进入预览。

将嵌套 ASCII 路径上限调整为 16 段，并增加 720 字节总长度上限。继续逐段验证文件名，拒绝绝对路径、空段、点段、穿越、反斜线和解压工具通配符；重复项、符号链接、条目数和文件大小校验保持。依然只通过 `unzip -p` 读取明确条目，不按包内路径解压。没有协议变更。

新增包含同类九段许可证路径的合成升级包回归及边界/危险路径检查，首刷回归也加入该目录。测试资源为独立合成内容，不收录第三方许可证或二进制。

修复后的本机加载器已成功读取真实升级 ZIP，核对版本 `0.2.234-ui`、应用 SHA `1c4fa6261243d3d1d9ac5c5fa54ac6e005f3988ebab9b125ddc5358c37a4f4c8`、ELF SHA `9e6d0e4523381385b721a46389c87ec02dd6ea492225260771f38b3d7bbc29fb` 以及真实 sidecar 的分类标题和列表。真实完整首刷 ZIP（SHA `aec51f9d21f8f9ce2f7ad3ec7cf98604969710b7aee3b80615bb4889549a17e3`）也通过清单、布局、空白区、全部分段 SHA 与精确应用提取校验。这些离线检查未创建桥接服务、未访问设备、未提供升级。

修复后 Release、候选包与签名再次通过。首个修复提交的候选为 `artifacts/mac-tab5-20261020/3cf5fa8/AIBotBridge.app`。初次启动、配对及早期 USB/Wi-Fi 验收使用 `bf16f1f` 候选；随后正常退出并切换包含全部当前修复的 `artifacts/mac-tab5-20261020/f3f9dd6/AIBotBridge.app`，随包 `SOURCE_SHA.txt` 为 `f3f9dd6ba4d7aad391ab2292555a1f5e6a8cd7e1`，已确认身份自动恢复。

`3cf5fa8` 的 [Mac CI 39356044460](https://github.com/yaoyouzhong/AI-bot/actions/runs/39356044460) **通过**。下载完整 CI 附件后，`scripts/verify_mac_tab5_ci_artifacts.py` 核对 112 项测试、0 失败、0 跳过，Release arm64 可执行文件、候选 App、内外层哈希、源码 SHA、许可证、工具检查与 8 张原生 PNG 均通过。本机 XCTest 仍因缺少 Xcode 受阻，不用 CI 结果改写该本机结论。

## 本轮修复：操作回执可见性

用户反馈保存后表单关闭但没有可见成功或失败提示。滚动主窗口到底后，才看到设备忙碌拒绝。原结果标签在长表单末尾；现移到滚动区域之外的固定底部，保存等待、成功和错误均持续可见。最小窗口尺寸的原生夹具渲染已验证标签位于窗口内，实际显示文字为合成回执，不是真机保存截图。用户在 `f3f9dd6` 正常候选再次原值保存，确认无需滚到底即可直接看见固定底部成功回执，真机复验通过。

`f3f9dd6` 的 [Mac CI 39357054680](https://github.com/yaoyouzhong/AI-bot/actions/runs/39357054680) 已通过，完整附件下载复核为 **114 项测试、0 失败、0 跳过**；Release、候选 App、源码 SHA、内外层哈希、许可文件、离线工具与 8 张原生 PNG 均通过。该候选本机 Release、签名和原生渲染也通过；本机 XCTest 仍因缺少完整 Xcode 未执行。

## Codex 本机探测与限制

本机 CLI 为 `0.163.0-alpha.10.2`，实际位于 `ChatGPT.app/Contents/Resources/codex-cli/CodexCLI.app/Contents/MacOS/codex`，原自动查找的 `Codex.app`、Homebrew 和 `/usr/local/bin` 均不存在。现按 `com.openai.codex` 的运行中或已注册应用目录查找新旧两种随包 CLI 布局，并保留用户显式路径优先及终端安装回退。独立本机探测已确认新解析器找到实际安装的 CLI；新增当前布局、应用重命名、用户覆盖、旧布局和无效路径回归。

CLI 帮助确认提供 `app-server proxy`，但只读 `initialize` 探测在握手前结束，默认 socket 返回 `ENOENT`；`daemon version` 同样不可达。当前桌面子进程使用 stdio。**因此这台 Mac 的 Codex 会话能力当前受阻**，不能把“找到 CLI”记为连接成功。未启动或部署另一套 daemon，未修改模型、权限、审批和服务参数，也没有列出、读取或向真实任务发送消息。交互窗口补充持续更新的 Codex 连接状态，避免静默失败。

初始化接口依据 [官方 App Server 说明](https://learn.chatgpt.com/docs/app-server)，本机安装布局、命令帮助和失败结果以实际 CLI 探测为准。没有从文档推断当前桌面已开放代理。

## BLE 首次权限与重新启用问题

用户实际首次选择了允许蓝牙，随后关闭再开启。窗口无论开启或关闭都显示“蓝牙已关闭”。因此当时已发生首次授权，但拒绝/恢复路径没有执行，连接与收发尚未确认。

源码定位到重新启用复用已有 `CBCentralManager`，状态不变时系统不再触发状态回调，导致旧的关闭提示一直保留。现重新启用时显式处理管理器的当前状态，同时忽略关闭后迟到的状态回调，避免反向覆盖“已关闭”或维护暂停提示。扫描、配对认证、USB 优先及传输协议保持不变；已对照 `.234` 固件确认广告的服务 UUID 与 Mac 相符。

用户正常退出旧候选并打开精确的 `ffdf43b` 候选，关闭再开启蓝牙后，状态正确变为“正在寻找已配对 TAB5”，随后反馈“已认证 · USB 优先待命”。重新启用提示、真实 BLE 配对认证和 USB 活跃时待命已通过。曾提出排查 Windows 系统蓝牙连接占用，但用户没有确认是否关闭了 Windows 蓝牙，不能据此归因。

用户确认设备原连接模式为“自动”，随后临时设为“仅蓝牙”并退出实体设置页，断开 Mac USB、保持独立供电。约 20 秒后 Mac 蓝牙行显示“数据已确认”；保持该隔离条件读取并原值保存显示设置，收到“TAB5 已确认保存显示设置。”。因此真实 BLE 状态帧及显示设置读写通过，无 Wi-Fi 路由混入。用户随后在相同隔离条件关闭蓝牙约 5 秒再开启，约 20 秒后自动恢复“数据已确认”，未重新配对，断线重连通过。本轮尚未改变亮度、声音、页面或轮播。隔离复验结束后，用户已明确将连接方式恢复到原先“自动选择”并退出设置页。

修复提交 `ffdf43b` 的 [Mac CI 39357782391](https://github.com/yaoyouzhong/AI-bot/actions/runs/39357782391) 与完整附件复核通过：114 项测试、0 失败、0 跳过，Release、候选包、工具、原生夹具、源码 SHA 和包哈希一致。本机 Release 与签名通过，精确候选为 `artifacts/mac-tab5-20261020/ffdf43b/AIBotBridge.app`；本机 XCTest 仍受缺少完整 Xcode 阻塞。

## 本轮修复：睡眠后恢复系统已有的 BLE 连接

在仅蓝牙、USB 断开条件下，用户让 Mac 睡眠约 15 秒并唤醒，等待约 20 秒后仍显示“正在寻找已配对 TAB5”；继续等待后再次确认提示不变。初次结果记录为未通过，后续 `791d898` 已复验通过。只读 `system_profiler SPBluetoothDataType -json` 检查确认 TAB5 位于系统的 `device_connected` 列表；诊断只输出是否匹配该设备及所在分类，不打印其他设备、蓝牙地址或配对材料。

Mac 原来只扫描广播以发现设备，而 `.234` 固件在已有 BLE 连接时不继续广播。现每次发现周期先通过 `retrieveConnectedPeripherals(withServices:)` 查找提供 TAB5 专有服务的系统已连接设备，再调用正常连接、服务发现和设备 ID/随机数/HMAC 认证路径；无匹配结果时沿用广播扫描。该公开 API 的本机 SDK 说明明确要求应用对系统已有连接再发起本地连接。未按设备名称信任连接，未检索其他 HID 设备，也没有协议或固件变更。本机 Release 与候选包签名通过；全量 XCTest 仍在执行前受阻。实际睡眠恢复结果见下文新候选复验，不能将修复代码或 CI 本身记为硬件通过。

## 本轮修复：重复钥匙串授权

用户补充：同一轮连续要求输入本机密码 3～4 次。随后提供的截图确认：AI-bot Bridge 请求访问“登录”钥匙串中服务名为 `io.github.yaoyouzhong.AI-bot` 的机密信息，按钮为“始终允许／拒绝／允许”；用户每次选择“允许”。这属于钥匙串授权，不能当作蓝牙权限弹窗；截图没有区分同一服务下的具体 account。切换 `5e91c29` 后用户仍报告输入 4 次，因此尚不能宣称重复弹窗消除。未复制截图或读取任何真实密码。

源码确认旧 LAN 令牌在启动、HTTP 鉴权、USB LAN 配置与镜像窗口中重复调用 `SecItemCopyMatching`；拒绝或读取失败还会被当作不存在并尝试创建/更新令牌。现将该项目的首次读取结果（包括失败）串行缓存在当前进程内，正常后台访问不再重复请求授权。只有明确不存在才创建；拒绝、解码错误和短记录都不会替换已有记录。显式保存成功才更新内存值，失败保留旧值。密钥仍只持久化到原钥匙串项目，不改变 ACL、不读出真实秘密、不写入偏好或诊断。

新增四项回归覆盖多调用方复用、拒绝后不重复授权/不轮换、仅缺失时创建、显式轮换与写入失败保留。程序提交 `5e91c293586b439ddd16e8bb32635179e4295563` 的 [Mac CI 39359379573](https://github.com/yaoyouzhong/AI-bot/actions/runs/39359379573) 与完整附件核验通过：**118 项测试，0 失败、0 跳过**，Release、候选包、工具、原生渲染、源码 SHA 和内外层包哈希一致。精确本地候选位于 `artifacts/mac-tab5-20261020/5e91c29/AIBotBridge.app`，已只读确认新进程运行且旧候选退出；正常界面与授权结果仍以人工反馈为准。TAB5 配对项目本来只在服务启动及用户明确重新配对时访问；它和旧 LAN 项目仍互相独立，临时签名新候选也可能各自需要一次正常授权。因此不能宣称所有系统授权弹窗已消除。按 [Apple 官方说明](https://support.apple.com/zh-cn/guide/keychain-access/kyca1243/mac)，“允许”仅授权一次，“始终允许”记录该 App 对相应项目的后续许可；正在使用同一候选验证该正常授权流程，未放宽为允许所有 App 访问。

本机使用实际缓存源码配合合成存储后端执行独立检查，上述四组行为（含 32 次并发读取）均通过；未访问真实钥匙串。全量 `swift test` 仍因缺少 XCTest 在执行前受阻。缓存仅在当前进程有效，外部直接修改钥匙串需要重启 App 才会重新读取；App 内显式保存会立即更新缓存。

用户按同一个 `5e91c29` 候选重新启动并选择“始终允许”，反馈需要确认两次。随后关闭蓝牙、正常退出并重新打开同一候选，确认没有再出现钥匙串弹窗；同一候选的正常授权和重启记忆路径通过。启动会读取旧 LAN 令牌和 TAB5 配对两项，但截图未显示具体 account，不对每张弹窗逐次强行归因。程序没有修改钥匙串 ACL；用户通过系统弹窗为当前候选授权属于正常流程。更换临时签名构建后的首次授权、拒绝和恢复仍不能据此宣称全部通过。

## 本轮修复：系统蓝牙配对确认被提前取消

用户另提供系统蓝牙数字比较截图，并确认配对弹窗约十几秒后再次出现。它与钥匙串弹窗不同，配对码没有写入记录。Mac 连接和服务认证原共用 15 秒期限，超时会取消连接，三秒后自动重连；`.234` 设备端数字比较允许 30 秒，并且需要在实体 TAB5 上确认。因此在用户处理两端弹窗时，桥接可能先取消正常配对并触发第二次请求。

将连接/初始认证等待调整为 60 秒，并在连接状态明确提示核对两端数字。服务发现只请求 TAB5 专有服务，不再请求全部服务（包括未使用的 HID）；仍保留同一认证路径。数据帧和 RPC 的已有期限、USB 优先、HMAC 认证、固件的配对确认与持久化规则都不改变；不自动批准数字比较、不删除配对记录。程序提交 `791d898c306b7232dc69c0432a51a6d55b7cfc2c` 的 [Mac CI 39360266984](https://github.com/yaoyouzhong/AI-bot/actions/runs/39360266984) 与完整附件核验通过：118 项测试，0 失败、0 跳过，Release、候选包、工具、8 张原生 PNG、源码 SHA 和包哈希一致。本机 Release 与签名也通过，XCTest 仍因缺少完整 Xcode 受阻。精确候选为 `artifacts/mac-tab5-20261020/791d898/AIBotBridge.app`；系统配对弹窗与睡眠恢复分别跟踪，复验前用户曾暂停 Mac 蓝牙。隔离测试后用户已恢复 TAB5 原先“自动选择”并退出设置页，用户接回 USB 后明确反馈 USB“数据已确认”、蓝牙“已认证，USB 优先待命”，优先级回归通过。

用户已正常打开 `791d898`，初始保持蓝牙关闭；只读进程核对确认旧候选退出且仅新候选运行，未认证的两个状态 HTTP 路由仍返回 401。启用蓝牙后，用户反馈出现 macOS“键盘设置助理”；固件同时广播 HID 服务，Mac 的 TAB5 状态与设置功能不需要配置键盘布局。用户退出助理后确认“数据已确认”，新候选真实 BLE 状态传输通过；首次配对弹窗保持时间没有明确反馈，不将精确延迟确认复验写为通过。用户随后明确确认本次睡眠唤醒与 USB 接回期间没有再次出现数字配对弹窗，正常重连不重复配对通过。随后保持 USB 断开与仅蓝牙，让 Mac 睡眠约 15 秒并唤醒，约 20 秒后用户回复“显示数据已确认”，此前失败的睡眠恢复项真机复验通过。候选使用说明已双语补充两端数字确认、键盘设置助理和钥匙串授权的区别。

用户在系统设置中关闭当前候选的蓝牙权限，App 显示权限被拒绝并提示到系统设置允许；重新开启同一权限后，用户确认“已认证，USB 优先待命”，撤销授权后的拒绝提示与恢复通过。首次权限弹窗当时选择的是允许，首次直接拒绝的分支仍没有执行，不将两者混为同一项。

## 后续项目

- Wi-Fi：保持 TAB5 独立供电、断开 Mac USB 后，用户约 15 秒内观察到“收到已认证请求”；纯 Wi-Fi 的显示设置读取和原值保存也收到明确成功回执，插回 USB 后自动恢复且画面正常。未改变原网络配置；尚未精确测量八秒阈值。USB 配网、发现、地址恢复和固定模式待验收。
- BLE：首次选择允许、重新启用提示、实际认证、USB 活跃时待命，以及仅蓝牙下状态帧和显示设置读取/原值保存通过，关闭再启用后自动重连也通过；`791d898` 的睡眠唤醒复验也通过。撤销授权后的拒绝提示与恢复也通过。首次权限弹窗直接选择拒绝仍未执行；隔离结束已恢复原先“自动选择”模式。
- 显示与资源：USB/Wi-Fi 原值读取与保存回执已通过；降低亮度、正常开关机后读回和恢复原亮度通过；设备非静音试听及静音后禁用试听通过，原设备音量和非静音已恢复并收到保存成功。固定页切换、轮播与屏保进入/触摸退出已有人工实测，精确轮播间隔未测量；原显示设置均收到恢复保存成功。真实名画 ZIP `2026.10.14.1`（SHA `a774f3717f83b685c8d9b9c1dc4f8ecd22b0a95a3abb80f882eb21726b29fc39`）的隔离校验与正常 App 完整导入均通过。临时 CC0 多页子集的实体图片、前后翻页、横竖屏及错误包不替换旧资源也通过；完整官方图库及原屏保类型、自动旋转均已恢复，用户退出设置页回到总览。封面、桌宠与高清完整包仍单独验收。
- 系统控制与音乐：CoreAudio 只读预检已取得真实可用音量/静音状态，未修改输出或播放；正常 App 的控制回执、常用入口、锁屏、自动化权限和播放控制待验收。
- Codex：本机版本与 CLI 布局已核对，默认本机代理不可达；精确任务、公开回复、草稿、送达状态和快捷控制台受此阻塞。未对真实任务发送、追加或中断。
- 照片与语音：真实采集、校验、预览、中文本地识别、停止/取消/断流与手动确认待验收。
- 固件工具：已核对公开 `.234` 源码 ZIP SHA `c107e5f929e757a6342bea659f84e726a40d5f16b692441865f348fa26290890` 和升级 ZIP SHA `900612d0338c2350a3eda19a991536ce28475cce9667a4752613fccde211d509`；App 选包/说明预览待验收。OTA、首刷和恢复保持未执行。

## English

Current verified program is `7ad1c24`; older pending statements retain their historical context. USB and actual BLE settings/reconnection/sleep recovery passed, as did pure-Wi-Fi settings and USB return. Provisioning/address recovery/fixed modes/exact fallback timing remain pending. Display controls/persistence, gallery import/failure retention/pagination/orientation passed; individual daily data, pets and even music timing remain unfinished. Apple Music metadata/artwork/control, system volume/mute/unmute and folder launch passed; Spotify, application launch and lock/Accessibility denial/recovery remain pending. Codex proxy initialization is blocked before handshake, and no real task send/append/interrupt was executed. Voice permissions and one 7.4-second Wi-Fi take passed after the HTTP persistence repair; no-sound hint debouncing, exact final text, USB/BLE and sustained capture stability remain pending, as do photo/task attachments. Normal-app firmware preview and read-only current-image USB boot verification passed; no firmware write occurred. Temporary music/media, system audio and folder-launcher cleanup completed; voice was disabled again; the user previously left the unsaved editor, then reopened it for the latest retest without saving. USB is temporarily disconnected and bridge BLE disabled for voice isolation. The full nine-stage acceptance is not complete.

Fixed-cadence program commit `3c82bcce4df49af2b3e1c64445664500eabf7a43` passed [Mac CI 39368358000](https://github.com/yaoyouzhong/AI-bot/actions/runs/39368358000) and full artifact verification: 136 tests, zero failures/skips, Release/app/tools, eight native PNGs and matching source/package hashes. An actual-object synthetic cadence check passed twenty varying ACKs, control rephasing and blocked-write burst suppression without device access. Local Release/bundle/signature passed; local XCTest remains unavailable. The previous candidate was normally quit before exact `3c82bcc` launch. The user handled system authorization and confirmed opening TAB5; the unique process and both unauthenticated HTTP 401 routes were verified. Bridge-window selection repeatedly timed out, including after reconnecting the UI tool, so USB/BLE row text is not independently reconfirmed for this run. Read-only stack symbols showed AppKit event processing, music polling and USB work; no persistent freeze was established and no Keychain contents were inspected. Music UI automation remained available and restarted the fixture.

After the forty-five-second observation, the user reported no skipped seconds but uneven progress. This is an improvement, not full timeline acceptance. Subsequent single physical pause and play each reportedly took under one second; normal Music UI independently confirmed both real states and opposite action buttons. These durations are user estimates, not instrumented measurements. See the [firmware timing follow-up](MAC-TAB5-MUSIC-TIMING-FOLLOWUP.md) for the running `.234` renderer's integer truncation/per-packet phase reset, synthetic replay evidence and required lifecycle checks. No firmware was changed or written.

Bringing TAB5 to the foreground restored UI-tool selection, allowing acknowledged USB, authenticated Wi-Fi and USB-priority BLE standby to be reconfirmed. Fresh settings were read and only the temporary music-page mode was restored to this phase's entry mode, with a matching fixed-footer save receipt; cycling and all other values stayed unchanged. Music opt-in was restored off; computer/Codex/voice remained off. After pausing, normal Music library removal moved only the fixture copy to recoverable Trash, and the library no longer contained its entry. The original source stays ignored; Trash was not emptied. Normal Music quit was independently confirmed by process inspection. Temporary music-page/opt-in/media cleanup is complete. No Keychain ACL was weakened.

In the same normal `3c82bcc` app, selecting the official `.234` upgrade ZIP showed version `0.2.234-ui`, the exact application SHA above, structured actual sidecar notes and an explicit not-started state. Only Verify Boot was invoked; it reported USB verification of exact ELF, VALID partition and three live UI samples. This checks the already running image, not an OTA/write performed in this session or a new physical screen/touch observation. Stop Providing Firmware then returned to no selected firmware with the stop receipt. No upgrade-start, reset or flash-write action was invoked.

Foregrounding restored bridge selection, but closing a child window again left it unselectable and Command-W did not close the window. The accessory app lacked a reopen delegate and main menu. Reopening the running app now opens TAB5; a standard Window menu adds Command-W and Command-Q follows the existing quit path. Existing window-busy and application termination guards remain. No automatic permission approval, external control interface or deployment was added.

Local Release passed with the unchanged SerialBridge Sendable warning; local XCTest remained blocked before execution. Earlier documentation commit `61e86ac` passed [Mac CI 39374024338](https://github.com/yaoyouzhong/AI-bot/actions/runs/39374024338), and complete artifacts verified 136 tests with zero failures/skips, Release/app/tools and matching source/package hashes. The window change's CI and normal-app checks are tracked separately.

Program `3db70a3dece89c39acd71743007b8213ae366600` passed [Mac CI 39374705006](https://github.com/yaoyouzhong/AI-bot/actions/runs/39374705006); complete artifacts verified 136 tests, zero failures/skips, Release/app/tools, eight native PNGs and matching source/package hashes. The normal local candidate passed bundle/strict-signature checks. After the user handled expected Keychain authorization, automated window selection confirmed actual transport status and unauthenticated 401. Command-W closed the feature child and returned to TAB5; the Window menu exposed Close and TAB5. Command-Q exited with the old PID absent. Automated relaunch initially timed out with no window; reopening that running app then showed TAB5 without manual window assistance, and USB automatically recovered. Initial menu-only launch remains intentional, so not every UI-tool timeout is claimed eliminated.

With computer control temporarily enabled and music off, a read-only production CoreAudio path captured the fresh restoration baseline. The user dragged physical TAB5 volume to 17%; CoreAudio and normal Sound settings both read 17%. Single physical mute/unmute clicks changed the device buttons and CoreAudio to true/false with volume retained. Real TAB5-to-Mac volume/mute/unmute passed without synthetic control requests. Normal Sound settings then restored the original volume, independently confirmed with the unmuted baseline. Temporary System Settings was quit and computer opt-in restored off. Fresh device display settings matched this phase's restored values; the form was cancelled without another save. Device speaker audio, lock/Accessibility, launchers and Spotify remain separate checks.

Folder launcher passed separately. Starting with an empty launcher list, normal app selection temporarily added the newly created empty `TAB5-LaunchTest` directory and enabled computer control. After one physical device click, Finder switched from the old candidate folder to that exact empty test directory; the assistant did not open it directly on the Mac to substitute for device input. The temporary Finder window was closed, the entry removed through the app and computer control disabled; the empty list was verified and the assistant-created empty directory removed. Application launchers, lock/Accessibility and permission denial/recovery remain separate pending checks.

Normal `3db70a3` voice authorization passed via the user's system confirmation and matching app receipt, with computer/music/Codex off. An unsaved agenda-name editor uses the existing independent voice owner. The user could recognize speech but reported ending before speaking with a connection-interruption message; accurate final text was not supplied, so full recognition/stability is not accepted. Published `.234` firmware distinguishes missing/unacceptable transport replies from the Mac recognizer-error message. After disabling voice and normally quitting, the assistant verified exclusive serial ownership and sent only a 460800-baud ping. Whitelisted diagnostics retained 2 ms/seq 24 for the recent voice request and historical USB RPC count 83, stage 1/status 0/zero reply bytes. Historical counters do not correlate every failure or prove contention/reset. No raw stream, audio, transcript or pairing material was saved; no reset/write occurred. The same app was restarted and USB confirmation awaited before another voice check.

A single silent three-second take reported the generic recognition error only after the user pressed Stop; premature completion was not reproduced in that check. Both UI samples showed acknowledged USB, without claiming continuous coverage. The old backend discarded NSError classification. The repair distinguishes manual empty no-speech review from premature/timeout/partial failures, and reports controlled resource/permission/busy/service/unknown-code errors without raw error descriptions. A live Mac status summarizes capture, local recognition, review and errors without transcripts or identities. Existing wire fields and local-only recognition remain. Five regressions cover these behaviors and summary privacy. Local Release passed; XCTest remains unavailable and CI/normal-app checks follow separately. Voice was disabled again. The original transport interruption is not claimed fixed by this error-reporting repair.

Program `b012fe63861d1f524607bcbec045472252ad2810` passed [Mac CI 39376374679](https://github.com/yaoyouzhong/AI-bot/actions/runs/39376374679) and full downloaded-artifact verification: 141 tests, zero failures/skips, Release/app/tools, eight native PNGs and matching source/package hashes. The new speech status is visible in full native rendering and the normal app without covering following controls. Local XCTest remains unavailable. The old candidate was normally quit before exact-path launch; after the user handled expected Keychain authorization, automated TAB5 selection reconfirmed acknowledged USB, authenticated Wi-Fi and USB-priority BLE standby. Local voice was enabled with existing authorization while other controls stayed off. A three-second silent take stopped manually now reports no recognized text on both device and Mac, passing this empty-take message check. It does not establish that the original premature transport interruption is fixed. The subsequent spoken retest still failed: the user reported either no detected sound or recovered text explicitly marked possibly incomplete, and confirmed a complete take could not finish. A Mac review status cannot override that device result. Voice was disabled again and no agenda was saved.

A further caller/peer review found a terminal-state race. Firmware treats a non-success voice reply as transport failure/recovery; Windows already returns the original state for in-flight audio outside recording. Mac instead returned `invalid_voice_audio`/400 if its recognizer had ended between audio requests, causing a possibly-incomplete recovery despite a stored final result. An isolated check linked to actual local debug objects reproduced status 400 without device, microphone or Speech-service access. This establishes a code defect, not every hardware failure's cause. The minimal repair returns the current state for late audio only after the existing opt-in and exact device/voice/session checks; it neither feeds audio nor reopens capture or changes results/errors. Three regressions cover final/error, stop/cancel and owner/consent isolation. Protocol, Windows and firmware are unchanged. The same actual-object check passed after the repair; local Release and test-source parsing passed, while local swift test still stops before execution because XCTest is missing. Full regressions require Mac CI. After disabling voice and normally quitting with exclusive serial ownership verified, another read-only ping reported a recent 83 ms/seq 27 request and historical USB failure count increasing from 83 to 91, still at stage 1/status 0/zero reply bytes. These uncorrelated counters indicate other missing-reply history, so terminal handling is not claimed to resolve all connection failures. No raw stream/audio/transcript/pairing data was output. Physical retest remains required.

Program `4466022f40cf379a6cf0c3dc0c5aa10f04f89086` passed [Mac CI 39377149298](https://github.com/yaoyouzhong/AI-bot/actions/runs/39377149298) and full artifact verification: 144 tests, zero failures/skips, Release/app/tools, eight PNGs and matching source/package hashes. Normal exact-path launch after old-app quit, expected user Keychain approval, automatic window selection, USB/Wi-Fi/BLE recovery and both unauthenticated 401 routes passed. Full spoken capture still failed with connection interruption/no-signal reports. Disabling voice/BLE and physically disconnecting USB confirmed an absent port and Wi-Fi-only authenticated status. Actual device settings were read successfully, matched the restored baseline and were cancelled without saving. The single Wi-Fi voice check immediately reported connection interruption/capture stopped, so USB alone cannot explain the fault. Voice was disabled again and the user left the unsaved agenda editor without saving.

A separate actual-object check with synthetic pairing/backend passed the real HTTP parser/route/proof/decryption/RPC path for start, fifty IMA audio blocks, stop and final reply. It opened no listener and accessed no device, microphone, Speech service or private pairing. It is not physical transport acceptance. Additional local-window diagnostics show accepted block count/duration, recent/peak level and fixed operation labels/bounded status numbers, resetting old peak/failure on a new take and hiding when opted out. They expose no audio/transcript/device/task/session/authentication data, add no wire fields or unauthenticated endpoint, and have regressions for deduplication/peaks/reset, fixed labels/opt-out and HTTP authentication failures. Local Release, test-source parsing and the actual-object fifty-block HTTP/diagnostic check passed; local XCTest remains blocked before execution. New candidate/CI checks follow separately.

Program `888086b22fca492cc7f81ce0518873499ed6b901` passed [Mac CI 39377742095](https://github.com/yaoyouzhong/AI-bot/actions/runs/39377742095) and full artifact verification: 147 tests, zero failures/skips, Release/app/tools, eight PNGs and matching hashes. Exact-path normal launch, expected user Keychain handling, automated selection, authenticated Wi-Fi with USB absent/BLE off and both unauthenticated 401 routes were verified. Another physical take still reported connection interruption/recovery. The normal Mac window showed zero accepted audio blocks/seconds and zero recent/peak level, with recovery Stop/later Poll returning Wi-Fi 200 and no recorded rejection. Zero level here means no audio arrived; it does not establish microphone hardware failure. Dynamic diagnostic layout remained readable. Voice was disabled again; no agenda was saved and the editor remains open for retest.

The device streams start/audio/stop through one HTTP client, but Mac previously returned Connection: close and cancelled every socket; single-call settings acceptance does not cover this stream. An actual-server synthetic-pairing/backend loopback client with automatic reconnection disabled reproduced failed sequential exchanges before the repair, then completed start, fifty audio blocks and stop on one socket afterward. This supports a Wi-Fi connection-lifecycle incompatibility, not resolution of all USB failures; see the [official ESP HTTP client guidance](https://docs.espressif.com/projects/esp-idf/en/v5.3/esp32/api-reference/protocols/esp_http_client.html). Only successful individually authenticated HTTP/1.1 voice POSTs now persist. Each request is freshly parsed and nonce/proof checked; failures, explicit Close tokens, HTTP/1.0 and other routes close. Connections are limited to 640 requests/120 seconds with the existing ten-second read idle timeout, body/queue bounds and combined-message rejection intact. Three live loopback regressions cover same-socket audio/stop and subsequent invalid proof, explicit-close/HTTP1.0 and ordinary status closure. No audio replay, wire-field or firmware change was introduced. New normal candidate/CI checks follow separately.

Program `7ad1c240e27ca82e9b633f2aacf82596f32ada6a` passed [Mac CI 39378349053](https://github.com/yaoyouzhong/AI-bot/actions/runs/39378349053) and full artifact verification: 150 tests, zero failures/skips including live TCP regressions, Release/app/tools, eight PNGs and matching hashes. Local Release/bundle/signature/tool checks passed while local XCTest remains unavailable. The previous candidate was normally quit, exact new path launched, expected Keychain permission handled by the user, and automated selection reconfirmed Wi-Fi-only transport, other opt-ins off and both unauthenticated 401 routes. The user reported successful recognition; the normal Mac window independently showed 37 blocks/7.4 seconds, recent level 4%/peak 100%, final review and Wi-Fi Poll 200 with no rejection recorded. This is one actual successful capture/recognition after the earlier zero-audio failure. Exact final text was not supplied, so accuracy, long-term or USB stability is not claimed passed. No agenda was saved.

The user still found repeated no-speech hints disruptive. Previously, after one second of audio, any current approximately 200 ms block with rounded-zero peak immediately set silent, including normal pauses. The repair requires three continuous quiet seconds of accepted, deduplicated PCM; any signal clears it, new takes reset it, and polls/duplicate chunks do not advance it. Existing silent wire semantics, capture and local recognition stay intact; the hint does not stop recording. Two regressions cover silence/pauses/signal/new take and duplicates/polls. Actual-object synthetic PCM checks, local Release and test-source parsing passed; local XCTest remains blocked before execution. New CI and physical results follow separately.

Normal candidate `754fd02` passed local bundle/signature/tool checks and was automatically launched after normal quit of the previous app; the user handled its expected system authorization. USB, authenticated Wi-Fi, BLE standby, the unique exact process and both unauthenticated HTTP 401 checks recovered. Its [Mac CI 39367861614](https://github.com/yaoyouzhong/AI-bot/actions/runs/39367861614) and complete downloaded artifacts verified 134 tests with zero failures/skips, Release/app/tools, eight native PNGs and source/package hashes. The initial thirty-second report selected backwards-or-skipped seconds; clarification specifically reported forward skips with no backwards movement. No rewind was reported in this observation, but forward stepping remains unpassed. Firmware adds a whole-second base to whole seconds since receipt. The old USB cadence waited two seconds after each ACK, accumulating transport/callback time into phase drift. The follow-up keeps original deadlines, restarts phase for control changes and skips missed slots after a blocked write instead of bursting. Two regressions cover repeated ACK/callback delays, control and blocked writes. A synthetic phase model can produce rapid adjacent second changes with ACK-based drift and avoids them with fixed phase; this is not hardware timing evidence and physical revalidation is pending.

After the user authorized normal candidate `c51a1bb` and opened TAB5, UI automation confirmed acknowledged USB data, authenticated Wi-Fi activity and authenticated USB-priority BLE standby. Only the expected candidate process was present; both unauthenticated status routes returned 401. With music enabled and computer control disabled, the user confirmed the physical test title/artwork, then clicked pause and play once each. Normal Music UI independently showed paused/playing state and the corresponding opposite action. Physical cover display and single control effects passed. The user subsequently reported latency, skipped seconds and backwards progress; timeline acceptance remains failed, separate from those effects.

The old two-second music poll was launched asynchronously while general status immediately published its older cache. USB could retransmit that cache, and player positions were rounded to integers. Published `.234` UI resets its local playback anchor on every status, so old positions cause rewinds. Confirmed player control readback was also discarded. The repair keeps fractional positions, polls independently every half-second without overlapping refreshes, samples current music at frame creation and projects only a recent sample to send time. Samples older than three seconds or from the future use existing `timelineAvailable=false` while retaining metadata/artwork. Pause stays fixed and genuine seeks are honored. Confirmed control readback immediately updates the cache; USB prioritizes track/state/seek changes without raising ordinary full-status frequency. Late polls/artwork cannot undo newer control observations or attach an old cover to a new song. No new message, wire version or firmware change is introduced. Eight regressions cover the actual device-clock contract, pause/seek, stale/future samples, fractions/end bounds, USB urgency, late results, current music overriding the general cache and artwork bound to that same sample. An actual-object synthetic check reproduced cached-position reset before the repair and passed afterward without accessing a device or Apple Events. Local Release passed; XCTest remains unavailable. A new normal-App timing/latency/play-pause retest is pending; `c51a1bb` is still running at this checkpoint.

After the user asked for less manual routine work, Computer Use was retried and could operate Music, bridge windows and Activity Monitor. Normal Music File Import added only the original test M4A; its ten-minute library entry, blue/orange artwork, pause control and advancing elapsed-time text were observed automatically. Some slider accessibility identifiers remained stale, so they were not used alone as playback truth. This local track needs no subscription and no other user media was read. Library cleanup remains pending. On normal `5024001`, the assistant read fresh device settings, temporarily selected Music and observed the matching save receipt. Physical title/artwork and touch control remain unconfirmed. This fresh display-mode read differed from the earlier manual baseline; its restoration value stays ignored and later cleanup must preserve current settings.

The exact old candidate was normally quit through Activity Monitor, using Quit rather than Force Quit as described by [Apple](https://support.apple.com/en-ie/guide/activity-monitor/actmntr1002/10.14/mac/15.0). Its absence was verified before Computer Use launched exact candidate `c51a1bb`. The new process exists, but system security authorization is pending and the status listener has not started. Startup and transport recovery are not marked passed. Computer Use explicitly forbids SecurityAgent access; the user was asked only to handle the matching expected prompt on the Mac and open TAB5 once. No password was read, entered or recorded. Routine Mac-window work will continue automatically afterward. A read-only process sample confirms the main thread is still waiting in `SecItemCopyMatching`; only stack symbols were inspected, with no Keychain contents read. The fixture completed once naturally and repeat playback was not enabled.

Before music acceptance, the user confirmed both computer-control and music opt-ins were off. Normal candidate `5024001` recovered acknowledged USB data and authenticated BLE standby; only that candidate was running and both unauthenticated status routes returned 401. Enabling music showed an Automation prompt, but no song played because the user lacks a subscription. Prompt appearance is not playback acceptance. Original local audio with a synthetic cover was prepared without accessing existing media; import/playback and later library removal are tracked separately.

A separate synthetic-backend reproduction found that the music-only opt-in returned 403 before media execution and omitted the shared control transport. Media commands now follow the music opt-in independently. The existing control-version capability is advertised when either feature is enabled. Volume, mute, lock and launchers remain gated by computer permission and are masked from music-only status; computer-only status masks media controls. Deduplication, pre-effect permission recheck, exact-player binding and readback remain. Existing v1 messages/fields are unchanged and firmware capability consumers were checked. Four regressions cover both opt-in directions, revocation of a recorded request and transport advertisement. The actual-object synthetic check failed before the repair and passed afterward, with volume writes still denied and no real Apple Events. Release passed; local XCTest remains blocked before execution. Program commit `c51a1bb8aa59afcaea9cd2735071da96b0fb8b72` passed [Mac CI 39364551773](https://github.com/yaoyouzhong/AI-bot/actions/runs/39364551773); complete downloaded artifacts verified 126 tests with zero failures/skips, Release/app/tools, eight native PNGs and matching source/package hashes. Local candidate `artifacts/mac-tab5-20261020/c51a1bb/AIBotBridge.app` passed build, bundled-tool and signature checks. Subsequent normal-app automation is recorded above. Physical music display, play/pause and denial/recovery checks remain pending; CI is not physical control acceptance.

On normal candidate `791d898`, the user imported the official painting archive and confirmed 366 works. Its active manifest independently matched the official package, including 826 images. Today's work has one page, so physical pagination used a temporary one-work CC0 subset from that same archive, preserving original images, source/license records and guides: ten pages per orientation, twenty images total. No clock/date was changed and this local-only subset is not a release artifact. The user confirmed normal preview, next/previous images and page numbers, then normal images/text/paging in portrait and landscape without black frames or abnormal clipping. An otherwise identical local archive with one changed image byte and unchanged manifest SHA was rejected by both the isolated loader and normal app. All 28 installed resource-file hashes stayed unchanged and the user confirmed physical paging still worked after rejection. Failure retention passed. These archives/source assets remain ignored. The user subsequently confirmed successful full official-pack reimport; its manifest independently matched 366 works/826 images and the original archive. The user then restored the original saver type and automatic orientation, left device settings and returned to overview. Temporary resource/display restoration passed. Artwork covers, pets and the HD full-bundle path remain separate checks.

The fixed-page save was acknowledged and physically switched TAB5 to its clock page. A temporary two-page cycle was saved and the user reported normal switching; after adding a third page, the user again reported normal switching in response to the requested three-page order. Exact intervals were not instrumented and no separate third-save receipt text was supplied. Original fixed page, cycle toggle, page selection/order and interval were then restored with a successful save, temporarily shortening only the saver wait. The user confirmed automatic saver entry after the approximately seventy-second idle request and normal physical touch exit; return page and brightness were not separately detailed. Restoring the original saver wait was acknowledged. All temporary display settings from this phase have been restored to the local baseline.

Source review and a synthetic backend using the actual compiled `MacMusicService` reproduced a separate cache defect: four failed player reads erased previously successful metadata and artwork. The poller now distinguishes failure from confirmed stop or application exit. With no selected successful track, any running-player read failure preserves cached data and resets the empty-poll count. Three consecutive confirmed empty polls still clear playback; explicit feature disable clears immediately without probing players. Four regressions cover failures, a stopped second player, interrupted empty-poll debounce and opt-out. A local independent check passed cache preservation, confirmed exit and explicit disable after failing before the repair. It started no music app and sent no Apple Events. Release passed. Local XCTest is still blocked before execution. Program commit `50240016ba810f1f302a6e77e598f7b8c7ffbcf8` passed [Mac CI 39362895479](https://github.com/yaoyouzhong/AI-bot/actions/runs/39362895479); complete downloaded artifacts verified 122 tests with zero failures/skips, Release/app/tools, eight native PNGs and matching source/package hashes. Local app/signature checks also passed; the exact candidate is `artifacts/mac-tab5-20261020/5024001/AIBotBridge.app`. Normal-candidate Automation/play-pause acceptance remains pending.

On the normal `791d898` candidate, reducing only TAB5 brightness produced a matching save receipt and a visibly dimmer screen. After a normal power-off/on, USB recovered acknowledged data and the picture was normal; fresh settings readback retained the temporary brightness. Restoring the original brightness was acknowledged and visibly confirmed, passing effect, persistence and restoration without firmware writes. Lowering only device notification volume while unmuted was acknowledged and the physical preview tone was heard. Muting was then saved and the user observed a disabled preview button, matching the published `.234` UI logic. This verifies the muted control state, not a performed silent preview or exact volume ratio. The user subsequently acknowledged successful restoration of the original device volume and unmuted state. The private restoration baseline stays only in ignored local evidence. Mac system audio, artwork covers and pets remain separate checks; the later page/cycle/saver results are recorded above.

The user revoked Bluetooth permission in System Settings and observed the app reporting denied permission with recovery guidance. Restoring that permission returned to authenticated USB-priority standby. Revocation handling and recovery passed; rejecting the original first-use prompt remains untested because the user originally chose Allow.

The user opened the exact `791d898` candidate with Bluetooth initially off. Only that candidate process remained; unauthenticated HTTP status routes still returned 401. After enabling Bluetooth, macOS opened Keyboard Setup Assistant, consistent with the firmware advertising HID. The user quit that assistant and reported acknowledged BLE data, passing actual status transfer on the new candidate. The initial pairing-dialog hold time was not explicitly reported, so the exact delayed-confirmation check is not marked passed. The user subsequently confirmed no numeric-comparison dialog reappeared during sleep/wake or USB return, passing normal reconnection without repeated pairing. The user then slept the Mac for about fifteen seconds under the same Bluetooth-only isolation and observed acknowledged data about twenty seconds after wake. Sleep recovery passed physical revalidation. The bilingual candidate guide now distinguishes two-ended pairing approval, Keyboard Setup Assistant and Keychain authorization.

On the same `5e91c29` candidate, choosing Always Allow required two confirmations. The user then disabled Bluetooth, normally quit and reopened that same candidate and confirmed there were no Keychain prompts. Normal authorization and persistence across relaunch passed for that candidate; new signatures and denial/recovery remain separate checks. Startup reads the legacy LAN token and TAB5 pairing record, but the screenshot does not identify each prompt's account. The separate Bluetooth numeric-comparison dialog reappeared after roughly fifteen seconds. The host's original fifteen-second connect/authentication deadline could cancel the link and retry after three seconds, while the firmware permits thirty seconds for explicit approval on TAB5. The initial handshake window is now sixty seconds, with a status message directing the user to compare and approve both ends; service discovery requests only the dedicated TAB5 service, leaving unused HID services undiscovered by this app. Data/RPC deadlines, USB priority, HMAC authentication and device pairing policy are unchanged. No pairing code is recorded, no approval is automated and no bond is deleted. Local Release and app signing passed, and the linked `791d898` CI plus full downloaded artifacts verified 118 tests with zero failures/skips, matching source/package hashes, tools and eight native PNGs. The exact candidate is in `artifacts/mac-tab5-20261020/791d898/AIBotBridge.app`; local XCTest is still unavailable. System-pairing timing remains separately tracked. Bluetooth was paused before the retest; after isolation the user restored the original Auto connection mode and left device settings. After reconnecting USB, the user confirmed acknowledged USB data and authenticated USB-priority BLE standby, passing return priority.

The user reported three to four consecutive local-password prompts. A later screenshot confirmed AI-bot Bridge requesting confidential information for service `io.github.yaoyouzhong.AI-bot` in the login Keychain, with Always Allow, Deny and Allow buttons. The user selected Allow each time and still reported four prompts on `5e91c29`; that one-time-access run did not establish elimination of repeated prompts; the later same-candidate Always Allow/relaunch check passed. The screenshot does not identify the account within that shared service. No screenshot or password was copied into the repository. Source review found repeated legacy LAN-token Keychain reads across startup, HTTP authentication, USB provisioning and the mirror window. A denied read also previously looked like a missing record and could trigger a replacement attempt. One serialized process-local cache now retains the initial read result, including failures, so background consumers do not re-prompt. Only a missing item is created; denial, invalid encoding and short stored credentials do not replace existing records. Explicit successful saves update memory; failed writes retain the previous value. Persistence remains in the existing Keychain item, with no ACL change or secret diagnostics. Four regressions cover these flows; an independent local check using the actual cache source and synthetic storage passed all four groups, including 32 concurrent reads, without accessing Keychain. Full local XCTest is still blocked before execution. External edits to the Keychain item require restarting the process; explicit in-app saves update the cache immediately. The separate TAB5 pairing item and authorization for a newly signed candidate can still require normal access approval. Apple documents Allow as one-time access and Always Allow as persistent permission for that app/item; the same candidate is being retested with this normal system authorization. No access for all applications is enabled.

Under Bluetooth-only isolation with USB disconnected, sleeping the Mac for about 15 seconds and waking it left the app searching after the requested wait and a later confirmation. Read-only system profiling still classified TAB5 as connected; no addresses or pairing data were printed. The Mac previously relied solely on advertisements, while the firmware stops advertising during a live connection. Discovery now first retrieves system-connected peripherals implementing the dedicated TAB5 service and uses the existing local connect, service discovery and device-ID/nonce/HMAC authentication path. It falls back to scanning when none are found. No name-based trust, unrelated HID retrieval, protocol change or firmware change is introduced. Local Release and app signing passed. The linked `5e91c29` CI and complete downloaded artifacts verified **118 tests with zero failures/skips**, matching source/package hashes, tools and native fixture renders. Only the new candidate process remains running; The later physical sleep-recovery result is recorded above; firmware-package CI alone is not hardware acceptance. Local XCTest remains blocked before execution.

For the first Bluetooth prompt, the user actually chose Allow, then disabled and re-enabled the feature. The row continued to say Bluetooth was off. Reusing a central manager does not produce a new state callback when its state is unchanged; re-enabling now explicitly processes the current state, and late state callbacks after disable are ignored. This does not change scanning, pairing, USB priority or wire formats. The `.234` advertised service UUID matches the Mac. The linked `ffdf43b` CI and full artifacts passed verification with 114 tests and zero failures/skips; local Release/signature checks passed, while local XCTest remains unavailable. After opening the exact `ffdf43b` candidate and toggling Bluetooth, the user observed searching followed by authenticated USB-priority standby. The toggle repair, actual BLE authentication and standby passed. Permission revocation/recovery and the later `791d898` sleep/wake retest passed; denying the original first-use prompt remains untested. Windows Bluetooth contention was suggested for diagnosis, but no user confirmation establishes whether Windows Bluetooth was disabled or caused the initial delay.

The device's original connection mode was Auto. The user temporarily selected Bluetooth only, left its settings page, disconnected Mac USB and retained independent power. The Mac then reported acknowledged BLE data; display settings readback and saving unchanged values also received the matching success receipt under the same isolated conditions. Actual BLE status/settings transfer passed without Wi-Fi routing. Disabling Bluetooth for about five seconds and re-enabling it restored acknowledged data without re-pairing, passing reconnection under the same isolation. Brightness, sound, pages and cycling were unchanged; the user restored the original Auto connection mode after the isolation checks.

Two additional repairs follow the first firmware-package CI: operation receipts now stay in a fixed footer outside the scrolling setup form, verified by minimum-size native fixture rendering; Codex CLI discovery now handles the actual nested ChatGPT desktop layout and renamed/moved registered applications while preserving explicit overrides and legacy fallbacks. The interaction window now displays live Codex connection status. On this host, CLI `0.163.0-alpha.10.2` exposes the proxy command but its default socket is missing (`ENOENT`), initialization exits before a handshake and the desktop child uses stdio. Codex conversation acceptance is therefore blocked even after executable discovery succeeds. No alternate daemon was deployed, no task methods were called and no model/approval configuration changed. Release/app/signature/native rendering and the linked `f3f9dd6` CI passed; full downloaded artifacts verified 114 tests with zero failures/skips and matching source/package hashes. The user normally quit the initial candidate, opened the exact committed `f3f9dd6` app and confirmed USB recovery without re-pairing. The process, listener and serial owner were independently verified. The user repeated an unchanged-settings save in the new candidate and confirmed the success receipt was directly visible in the fixed footer without scrolling; physical revalidation passed.

With TAB5 independently powered and USB disconnected, the user observed an authenticated Wi-Fi request within approximately 15 seconds. Readback and an unchanged-settings save also succeeded over Wi-Fi; reconnecting USB restored acknowledged data and a normal device picture. The exact eight-second threshold was not measured. Existing Wi-Fi configuration was retained; discovery, provisioning, address recovery and fixed modes remain pending. The actual curated ZIP first passed isolated validation for 366 works and 826 frames; later normal-app import and physical subset results are recorded above.

Acceptance resumed on the test branch from clean HEAD `bf16f1f`, matching the remote. The user confirmed the TAB5 USB connection and the Windows bridge was stopped or paused. This session permits temporary local candidate execution and related repairs, with no main merge/push, persistent installation, deployment, publication or firmware writes. Preserve TAB5 display settings; real Codex sends/appends/interrupts require a designated test task.

The host reports Apple Silicon, macOS 27.2 (`26B5112a`) and Swift 6.2.3. Local tests are **blocked before execution**: the selected Command Line Tools lack both the XCTest module and xctest, and no Xcode installation was found. Release, pinned bundled-tool hashes, ad-hoc signing, strict signature verification, offline tool-help checks and eight native fixture PNGs passed. The existing SerialBridge Sendable capture warning remains recorded. Native UI automation timed out for both the candidate and Finder. The user subsequently launched the initial candidate normally and confirmed its menu bar; its process, listener and HTTP 401 responses for both unauthenticated status routes were independently checked. The user also confirmed explicit USB pairing and the window's acknowledged-data status. Detailed screen data, remaining windows and permissions are tracked separately; the normal quit and restart result is recorded above.

USB descriptor and a read-only 460800-baud v1 ping confirmed a TAB5 running `0.2.234-ui`, with RPC v1/binary and packed/delta capabilities. The single UI sample had age 13 ms and zero flush errors; it does not establish boot health. The user confirmed automatic USB recovery after a 12-second cable disconnect without re-pairing. Display settings readback passed; saving unchanged settings was first correctly rejected while the device settings page was open, then acknowledged after leaving that page. The later brightness/persistence and device-audio results are recorded above; remaining display settings are tracked separately. No ESP8266 was tested.

An actual official `.234` upgrade archive exposed a loader defect: seven dependency-license paths contain nine components, exceeding the previous eight-component ASCII limit. Bounded support now allows 16 components and at most 720 bytes while retaining all component, traversal, duplicate, symlink and size checks. Entries are still streamed by exact name, never extracted to archive-provided paths. Synthetic upgrade/first-install ZIP regressions cover the published layout and reject unsafe paths. The repaired loader successfully validated the real upgrade image and its bound sidecar version, application/ELF SHA and structured notes. The real first-install ZIP also passed its manifest, layout, erased-gap, segment-hash and exact-application checks. These offline checks created no bridge service and accessed no hardware. Updated Release/app/signature checks passed; the committed candidate embeds source SHA `3cf5fa8`. Its linked Mac CI and downloaded full artifacts passed verification: 112 tests, zero failures/skips, executable/app/source hashes, licenses, tools and eight fixture PNGs. Local XCTest remains blocked. Startup and first pairing used the initial `bf16f1f` candidate; recovery on the repaired candidate has since passed, while normal-app firmware preview and all firmware writes remain untested. Evidence stays in the ignored local artifact directory; no credentials, private network configuration or raw runtime streams belong in Git.
