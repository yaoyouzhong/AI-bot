# Mac / Windows 模块对齐 · 2026-10-22

最新基准为 GitHub 开发分支 `codex/windows-tab5-multihost-20261021` 的提交 `79ffe7fb6b41819322283c2e151d52ca0b4f8204`（2026-10-22），已读取该分支的窗口、服务、额度算法、协议与 Mac 迁移说明。用户明确要求结构与功能一起补齐。下表记录未发布 Mac 候选的实现；已实现不代表所有账户或实体设备已验收。

| 模块 | 当前实现 |
| --- | --- |
| TAB5 | 显示设置、语音设置、日历生日、日程待办、电脑控制、屏保图库、常用任务、数据设置、连接升级，九项顺序与 Windows 一致。音乐权限归入电脑控制，Codex 权限归入常用任务，图库安装／更新归入屏保图库；固件／首刷归入连接升级。 |
| ESP8266 | 显示设置、外观设置、数据设置、连接设置；管理菜单含设备信息、诊断、升级和网络重置。 |
| 账号数据 | 模型账号、天气定位、自选股票、额度历史；天气与股票独立保存。 |
| 桥接设置 | 开机启动，以及服务状态、软件固件、配置迁移、提醒管理、运行监控、关于应用六项。 |
| 设备管理 | 每种型号最多一台，与 Windows 当前限制一致；添加、命名、启停、移除、六类数据和八个供应商筛选。停用限制 USB／HTTP／BLE 与发现响应。移除登记保留配对密钥及个人数据。 |
| 日历生日 | 32 项、公历／农历／闰月、提前 0–30 天；缺失日期按月末，闰月不存在时回退普通月份。复用 calendar.detail.birthdays。 |
| 日程待办 | 16 项、独立开始／结束、提醒、五种重复规则、持久存储、并发版本检查、离线编辑冲突保留、删除回执与幂等操作；未知假期年份暂停法定工作日提醒，满额只清理可清理的过期一次事项。 |
| 常用任务 | 32 个真实 Codex 任务 ID；持久化、近期列表优先显示，保留暂未加载的置顶 ID。 |
| 电脑与语音 | 四个白名单入口；既有音量、静音、锁屏、音乐独立权限，以及本地语音识别后预览和确认。 |
| 图库 | 独立图库管理（版本／更新／校验下载／续传／取消／导入）＋作品缩略目录／搜索／日期定位＋独立全屏窗口。缩放／拖动／循环翻页／适合窗口、简介／欣赏提示／原文图文对照和键盘操作已接通。高清校验失败回退已验证预览，保留旧包备份。 |
| 连接升级 | Wi-Fi／USB 配对／固件升级／语音／传输测速五页；已存 Wi-Fi 列表、备注、删除，固件说明／启动核验，通信与图库写卡测速；真实设备链路仍需实机验收。 |
| 股票 | 沪深北／港／美搜索，中文／拼音／代码、添加、删除、排序和批量输入；不执行搜索响应脚本。 |
| 额度历史 | 7／30 天已观察周额度增量、完整日均与明细；跨午夜沿用同账户、同套餐的有效累计基线，标记跨日补记及部分采样；明细列出首末及前后采样、重置、间断；五小时已用比例取每小时最后样本。旧历史没有五小时字段时保持未知，不混入本机 token 数。 |
| 软件固件 | 三组件独立版本，正式发布目录、组件附件身份和 SHA-256 校验，说明预览、打开安装包／升级工具。下载成功不代表安装或刷机成功。 |
| 配置迁移 | Windows 兼容非密钥字段与生日备份，导出、预览、逐项恢复、恢复前备份；不导出配对和 API 凭据。 |
| 提醒管理 | 完成／待输入／额度／监控提醒、声音、跨午夜勿扰、记录和重复抑制；明确保存才请求系统通知权限。 |
| 运行监控 | CPU／内存／磁盘实际采样、网络／PAC、启停与检测开关、代理预期、间隔、慢响应／占用／持续阈值；可选每五分钟 128 KiB 小流量测量。设备诊断页读取 USB 或固件 0.2.250-ui 起的 Wi-Fi／BLE 实际样本，显示采样时间、过期状态、显示／相机／照片／内存诊断。 |

## 本轮逐项复核与修正

本轮再次逐项读取同一 Windows 提交的窗口及调用服务，核对入口、分组、默认值、保存行为和数据来源。以下为本次新增修正，并非只改模块标题：

| 对照窗口／服务 | Mac 修正及检查范围 |
| --- | --- |
| `DeviceCenterForm` / `DeviceCapabilities` | TAB5 管理菜单严格为修改名称、停用／启用、分隔线、移除；仅 ESP8266 增加固件升级、设备信息、连接诊断、重置网络。诊断接真实 Wi-Fi 回退检查，重置接已有确认流程；移除保留配置与配对。停用后设备卡片禁用，离线时禁用需要在线的菜单项。 |
| `DeviceDataForm` | 六类采集项采用两行三列，八个模型按中文／英文／内容对齐；关闭模型额度会禁用子选项并保留选择。ESP 轮播选中的国产模型参与采集筛选。 |
| `CycleSettingsForm` / `DisplayPolicy` | ESP 支持完整 16 个轮播页面、手动模式、顺序和自动保存；亮度先读取后调节；关闭轮播保留已选页面。 |
| `DeviceAppearanceForm` / `LegacyDeviceSettingsForm` | 天气动画选择后自动保存；保留导入／恢复桌宠。连接设置增加屏保等待入口，范围为 0–1440 分钟。 |
| `Tab5DisplaySettingsForm` | 启用轮播选择自动模式；选择固定页面关闭轮播；其余参数继续读取设备实际值并等待确认。 |
| `BirthdaySettingsForm` | 默认提前 3 天；公历禁用并清除闰月；应用后保留选中项，重复应用不重复添加；修改未保存时关闭会提醒。 |
| `AgendaForm` | 默认显示全部；未结束按发生时间、已结束按最近时间排序，无时间项置后；开始时间变化时修正结束时间；删除确认、关闭未保存提醒。 |
| `ComputerControlForm` / `Tab5TaskPins` | 四个入口统一间距，路径与操作分行；清除或重选目标更换操作 ID，防止旧命令误命中新目标；关闭未保存提醒。固定任务继续使用真实任务 ID，保留暂时未加载的项目。 |
| `WeatherSettingsForm` | 增加主动获取位置及测试天气；测试使用表单草稿且不改配置、缓存或钥匙串，保存才写入。失败保留旧数据。 |
| 股票 `SettingsForm` | 纵向紧凑布局、名称／市场／代码列、350 ms 搜索延迟、回车搜索、双击添加；保留重排、删除及批量输入；关闭未保存提醒。 |
| `DomesticQuotaAuthForm` / `QuotaTrendForm` | 八个既有供应商来源逐项核查，刷新同时更新本机 Codex／Claude 额度，套餐文字实时更新。历史延续 7／30 天与每小时五小时比例的观测边界，不改成 token 推算。 |
| `BridgeStatusForm` | 每 2 秒刷新设备状态、实际固件／通道、数据更新时间和缓存状态；重连选定设备通道并恢复 LAN 监听；导出仅含允许字段的诊断 ZIP。 |
| `BridgeNotifications` | 完成提醒优先设备或备用音；其他类别系统通知，仅监控提醒带声音；额度合并 300 秒，其他 10 秒；保留最多 100 条含关闭／勿扰／设备处理的记录。 |
| `ConfigurationBackup` | 恢复预览展示当前值、备份值、生日内容及备份时间；保留选择性恢复和恢复前备份，凭据排除。 |
| `RuntimeHealthForm` | 网络与电脑负载双栏、CPU／内存条和详情入口；保留原有实际采样、阈值及设备诊断，不虚构 Windows 特有指标。 |
| `Tab5VoiceHost` | 使用公共 CoreAudio 列出并选择 Mac 输入设备，支持本地识别测试、停止及取消；未连接的已选麦克风明确报错。TAB5 麦克风仍为单独来源，不冒充 Windows DJI 自动选择／回退链路。 |
| `UpdateCenterForm` / `AboutForm` | 增加自动检查开关、更新通知、下载进度、取消与校验续传；下载完成仍需打开安装／升级工具。关于窗口收窄并补作者信息。 |

高清完整包导入时另修正了系统解压工具对 UTF-8 和方括号文件名的处理；逐文件精确选择、路径约束和 SHA-256 校验保留，测试包含与字面文件名相似的干扰条目。

名称、顺序及共同功能按 Windows 校准；macOS 原生权限、音乐控制、登录项、Keychain 和本地语音保留各自平台实现。真实账户授权、设备在线诊断、开机登录、音频录入、更新安装和刷机不以 UI fixture 代替实测。

## 最新 Windows 开发分支增量

- 设备中心按用户反馈收窄为 920×620，侧栏宽 164；九卡片随可用宽度切换三列／两列，卡片宽度上限 240，账号页与桥接设置页不再横向拉伸按钮；短描述、边距与子窗口共用布局。
- 无线运行诊断：认证 RPC、请求合并、10 秒请求期限、8 秒等待；首次超时仅重试当前绑定电脑的另一个在线通道一次，使用新请求 ID；迟到、重复、错误身份／通道／会话与超限数据拒绝，失败保留上次样本。
- 多电脑归属：Mac 已具备独立配对密钥、USB challenge 认证、BLE 归属提示及短证明；GATT 按服务 UUID 发现。USB 正常状态每 2 秒（活动音乐 0.5 秒）发送完整帧，满足新固件 8 秒归属租约，不需照搬 Windows 增量帧补发策略。
- Windows 扩展密码解锁助手、DPAPI 和登录辅助程序属于 Windows 平台实现；Mac 不伪造对应能力。固件 .250–.252 的相机、照片队列、日历文字等修改位于设备工程，本轮仅适配已有桥接协议，不构建或刷写固件。
- 上游迁移说明中“昨日额度仍为 4”的真实数据案例尚未解决；本轮算法检查只验证已给出的观测规则，不代表那个真实账户问题已经修复。
- 2026-10-24 复核：本机样本未复现上述固定值，用户确认额度显示“目前已正常”，当前症状关闭。未更改历史或算法，历史根因仍未确认。

## 模型账户来源

Claude／Codex 复用本机官方客户端已有授权。国产模型按实际来源读取，钱包余额与模型订阅分开显示：

| 平台 | Mac 来源 |
| --- | --- |
| 百炼 | 官方订阅控制台，或 AK/SK 查询阿里云账户余额。 |
| Kimi | Kimi Code loopback 服务、官方订阅控制台、Moonshot API 余额。 |
| MiniMax | 官方 Token Plan API／控制台的短周期与周窗口。 |
| DeepSeek | 官方余额 API。 |
| GLM | 官方 Coding Plan API 或财务页；Token 额度排除 MCP 次数。 |
| StepFun | 官方账户余额 API。 |
| 千帆 | 模型资源包 AK/SK 查询，或独立标识的云账户余额。 |
| MiMo | 官方 Token Plan 控制台响应；不一致的用量字段拒绝覆盖。 |

凭据只写钥匙串；控制台使用临时 WebKit 会话，只观察额度响应，不读取密码、Cookie 或请求头；后台失败保留上次成功数据。

## 验证与边界

- 原生 fixture 生成 224 张窗口截图，覆盖默认／最小尺寸、浅色／深色、固定操作区、未保存草稿、管理菜单顺序、离线禁用和停用／启用行为；使用隔离测试数据。检查日志为 `artifacts/mac-parity-audit-ui-r12.log`。
- `--self-test-parity` 通过 146 项隔离检查，包含生日、日程冲突／回放、设备登记、置顶、股票、额度、供应商、配置、更新校验、提醒策略、勿扰、高清原图合成及 UTF-8／方括号 ZIP 精确读取。检查日志为 `artifacts/mac-parity-audit-selftest-r14.log`。
- Release 在本机编译；`swift test` 因 Command Line Tools 缺少 XCTest 模块无法运行，独立检查不等于完整 XCTest 通过。
- 本轮没有全供应商真实账户授权、更新安装、开机登录或实体设备端到端验收。完整高清图库的安装与浏览结果单独记录。解析 fixture 不代表这些操作已经实测。运行应用检查见工作记录。
- macOS 不提供 Windows PIN 一键解锁、等价处理器队列计数或 Windows 豆包输入法／虚拟声卡；采用本机权限与语音能力，未知指标不代填。
- 不改变协议版本，不自动刷机，不推送、建标签或公开发布。不宣称跨平台像素完全相同。

## 本机安装与运行结果

高清安装验收使用 `artifacts/mac-all-module-parity-20261022-r14/AIBotBridge.app`，签名校验通过，Mach-O UUID 与 Release 构建一致。后续已替换为下述 r16 图片裁剪修正版。仍为未发布候选，不更新公开下载或组件版本号。详细日志和安装回执见本机忽略目录中的 `artifacts/mac-parity-audit-receipt.json`。

官方高清完整包来自本项目 `tab5-v0.2.205-ui` 附件，分段及完整 ZIP 均按官方清单校验。安装器继续核对内置标准包、索引、导读和每个高清文件，全部通过后原子启用。资源位于 `~/Library/Application Support/AI-bot/Tab5/Galleries/`，名画旧包保留为备份。

| 高清图库 | 已安装版本 | 作品 | 原图 | 高清展示页 |
| --- | --- | ---: | ---: | ---: |
| 名画 | 2026.10.16 | 366 | 384 | 413 |
| 书法 | 2026.10.16 | 366 | 1371 | 1901 |

- 名画完整 ZIP SHA-256：`ad436d154821abbd9b3203e6ad73f0c25cb2463a50bc9d039f6a7c8e28698749`。
- 书法完整 ZIP SHA-256：`02efde0d682aa56495707697d5c0bc53e79a2655dfdd47cc9520f6a058f88064`。
- 实际窗口确认 TAB5 三项管理菜单、ESP8266 专属维护项及离线禁用，修改名称弹窗只编辑名称，取消不改变配置。
- 名画实际加载高清原图，实测全屏、1600% 放大、拖动、适合窗口和图文对照；含方括号标题的《Summer Mountains》也正确加载 1477×3400 高清原图。
- 服务状态已显示真实数据更新时间与旧缓存；运行监控显示实际 CPU／内存／网络采样；麦克风列表可读取本机输入设备，未发起录音测试。
- 书法安装后已核对版本、作品数、全部原图路径及高清索引。2026-10-23 用户解锁后完成真实窗口检查：《黄州寒食诗帖》加载 3058×2141 高清图，三页均可浏览，下一页／上一页按钮、方向键翻页、回首页、1600% 放大、拖动、⌘0 适合窗口、简介／欣赏提示／原文面板及退出／重新进入全屏均正常。最终停留在第一页的全屏原文对照窗口。

## 2026-10-23 滚轮缩放修正

- 原先将 `scrollingDeltaY` 直接用作指数，鼠标加速或合并后的大滚动事件会迅速达到 1600%；先前的倍率上限检查没有验证滚动手感。
- 现在区分触控板像素与鼠标行单位，触控板小量移动对应更细的缩放，单个滚动事件最多放大 12%（反向采用倒数倍率）。忽略惯性滚动，保留光标锚点、双指捏合及 ⌘0 复位。
- 新增 13 项回归检查覆盖大滚动量限幅、精细移动、反向缩小、连续滚动、光标锚点和上下限；Release 独立检查共 159 项通过。构建与签名校验通过，`swift test` 仍因本机缺少 XCTest 模块无法完成。
- 原生窗口实测：名画浏览器按相同滚动输入显示 `100% → 112% → 125% → 112% → 100%`；书法全屏显示 `100% → 112% → 125% → 112%`，⌘0 恢复 100%。此处确认的是界面滚动输入结果，实体鼠标／触控板手感仍由使用者体验确认。
- r15 验收应用为 `artifacts/mac-gallery-wheel-20261023-r15/AIBotBridge.app`，后续由 r16 替换。日志：`artifacts/mac-gallery-wheel-release-r15.log`、`artifacts/mac-gallery-wheel-selftest-r15.log`、`artifacts/mac-gallery-wheel-swift-test-r15.log`。

## 2026-10-23 图片裁剪修正

- `NSView` 在 macOS 14 起默认不裁剪越界绘制。图片画布现显式启用 `clipsToBounds`，绘制时另外保存绘图状态、按自身 `bounds` 裁剪并恢复状态，放大图片不再画到标题、列表、导读或工具栏区域。
- 增加竖图、横图及拖到边缘的像素检查：1600% 下画布四周像素保持透明，内部图片仍可见，绘图状态恢复。独立检查共 162 项通过；Release 构建和签名校验通过。`swift test` 仍因本机缺少 XCTest 模块无法完成。
- 原生窗口实测名画普通窗口与书法全屏 1600%，图片被裁剪在浏览区域，标题、页码与操作按钮保持可见；普通窗口拖动及上下图文对照也已检查。全屏左右图文对照未单独完成操作验收，检查期间用户开始操作窗口，因此保留用户当前画面。
- 当前应用：`artifacts/mac-gallery-clip-20261023-r16/AIBotBridge.app`。日志：`artifacts/mac-gallery-clip-release-r16.log`、`artifacts/mac-gallery-clip-selftest-r16.log`、`artifacts/mac-gallery-clip-swift-test-r16.log`。

## 2026-10-23 解锁与豆包后续

后续候选为 `artifacts/mac-vnc-unlock-test-20261023-r20/AIBotBridge.app`。新增豆包 Mac 输入法调用候选（电脑收音、Mac 草稿确认）及已有本机 VNC 的连接验证窗口；均不能标记为 Windows 功能完整等价。r19 锁屏后认证与会话初始化实际通过，但只检查连接，不会解锁。r20 单独补上受控的一次登录输入测试；要求 Apple 登录安全字段焦点、当前控制台与 RFB 标记／删除回显均可确认。公共接口不开放时停止，不盲输。197 项隔离检查和 Release 构建通过；实际登录密码输入与解锁、TAB5 解锁、音频转送和真实识别仍未验收，`oneClickUnlock` 保持 false。详细状态见 [Mac 解锁与豆包候选](MAC-UNLOCK-DOUBAO-20261023.md)。

用户实际反馈 r20 未解锁且密码框未出现。最新 r21 `artifacts/mac-vnc-unlock-focus-20261023-r21/AIBotBridge.app` 补上密码页准备：先认证 VNC，再确认同一 Apple 登录窗口，查找／聚焦唯一安全字段；字段尚未出现时最多一个 Tab，随后仍需全部原有输入检查。206 项隔离检查、Release 构建和 230 张布局检查通过；已替换运行并打开测试窗口。真实密码框准备与解锁仍待验收，不因新增流程而通告一键解锁能力。

后续实际反馈 r21 密码框已出现但未解锁，重新打开窗口确认错误为 AX -25205（字符计数属性不支持），在密码输入前停止。当前 r23 `artifacts/mac-vnc-protected-field-20261023-r23/AIBotBridge.app` 为此增加受保护字段模式：仅对明确的不支持改用同一签名登录窗口内的焦点移出／移回证明，保留会话、原安全字段、取消及单次提交门控；不读取内容或假定零字符。Release、228 项隔离检查和 230 张布局检查通过，已签名替换运行。真实焦点往返与解锁仍未验收，一键解锁能力仍关闭。

用户实测 r23 的保留结果进一步确认焦点往返未通过，尚未发送登录密码。r24 改用锁屏前的自有安全测试框逐字符验证公开的大小写／数字／符号，成功后保持同一 VNC 连接才倒计时 15 秒；锁屏后仍核验控制台、Apple 登录窗口及密码框焦点，不再依赖 Tab 往返，并按最新 Windows 对齐 40 ms 按住时序。真实键盘回显和解锁尚待人工验收，设备能力仍关闭。

r24 的实际保留结果确认公开键盘样本未匹配，尚不能判定是按键未到还是字符映射问题。当前签名 r25 已替换运行：新增仅需 VNC 密码且无需锁屏的键盘检测，输入会话先进行有界单像素 Raw 初始化，并区分未收字符／大小写／其他映射错误，附自有测试框按下／抬起计数，不记录键值。243 项隔离检查与 230 张原生布局通过；真实键盘及解锁仍待验收，设备能力保持关闭。

r25 实测在图像响应精确匹配处停止，尚未进入键盘检测。签名 r26 随后替换运行：最小图像会话设置改为只发送，不等待／解码图像；公开键盘回显仍是必要门控。失败时只可选、非阻塞读取固定更新头摘要，不能据此授权输入。Debug／Release 各 244 项隔离检查通过；布局代码未变，230 张布局证据仍来自 r25。

r26 实测未收到第一个字符，按下／抬起计数均为 0。系统日志确认 VNCAuth 实际进入独立虚拟登录会话（session 265、`onconsole 0`、`vfb 1`），物理桌面是 session 257；认证通过不能证明当前桌面输入可用。r27 因此独立实现 Mac 账户认证（RFB type 30），新增“用 Mac 账户验证键盘（无需锁屏）”，只需在本机填写 Mac 登录密码，并明确说明该凭据用于本机 Apple 屏幕共享。发送前核验系统 launchd 任务和 Apple 进程签名；不保存密码、不启动或配置系统服务、不降级到其他认证。只读检查已通过本机 Apple 服务身份和 4096 位临时 DH 运算，不发送账户凭据或输入。当前桌面回显与实际解锁仍待人工验收，不能将编译或握手作为完成；设备 `oneClickUnlock` 保持 false。签名 r27 已替换运行（PID 4845），账户键盘验证窗口留在前台，密码框为空；257 项隔离检查与 230 张布局检查通过，XCTest 仍缺少本机模块。详见解锁候选记录。

r27 实测已认证到当前物理桌面（系统日志 session 257、onconsole 1），测试框收到 1 次按下／抬起后在组合焦点检查停止；完整字符与解锁仍未通过。r28 已替换运行（PID 5499）：公开样本改用独立 AppKit 文本编辑器，测试前滚动可见，仍严格检查应用／原窗口／编辑对象，状态错误分别提示；不自动抢回焦点或重发。Debug／Release 各 269 项独立检查和 230 张原生布局通过，完整 XCTest 仍缺少本机模块；真实 r28 键盘与解锁尚待验收。

r28 实测在应用活动标记变为 false 时停止；用户确认未切换窗口。r29-final 已替换运行（PID 6643），新增系统前台进程和自有公开测试框 AX 身份交叉核验；状态不一致仍停止，不抢焦点、不改变系统权限或 Dock 策略。无需密码、不连接 VNC 的实际本机自检已通过全部 8 字符，六项焦点状态均为 1，按下／抬起各 8 次。Debug／Release 各 272 项独立检查通过；界面沿用已通过 230 张布局检查的同布局 r29 首版，未重复渲染。实际 VNC 下活动标记为 false 的分支和锁屏解锁仍未验收，不能由本机自检代替。

r29-final 实测同时失去键盘窗口与系统前台（active=0、key=0、front=0），不能按活动标记误报处理。r30 已签名替换运行（PID 7253）：解锁测试窗口临时采用普通前台应用模式，关闭后恢复菜单栏；本机进程注册类型已核对 UIElement → Foreground → UIElement，重新打开也正常。参照 Windows 纯按键通道，删除图像初始化／更新请求，继续保留全部输入目标检查和不重试约束。本机无密码 8 字符、8 次按下／抬起及系统焦点自检通过；Debug／Release 各 267 项检查、重新生成的 230 张原生布局通过（移除 5 项已删除图像准备代码的夹具）。真实 VNC 全样本及锁屏解锁仍待用户验收，一键解锁能力仍关闭。

r30 实测仍在第一个公开按键后失去前台，停止时为 Codex，修饰键仅 Shift。两次系统日志均在首键后记录 Apple 屏幕共享连接提示，最新一次提示与前台切换相隔约 70 毫秒；这支持初始化提示干扰的判断，尚非内部因果证明。r31 因此在同一连接认证后等待用户点击固定页脚“继续键盘验证”，等待期间不发测试按键、不开始锁屏倒计时、不自动激活窗口；三秒后按钮可用，三十秒未继续即结束。过早、过期、旧连接、重复或取消的点击不能授权输入。无密码本机自检也经过同一门控；原焦点／不重试检查保留，一键解锁仍未验收。详见解锁候选记录。

签名 r31-final 已替换运行（PID 8632），Debug／最终 Release 各 275 项检查、最终版 230 张原生布局通过。实际无密码／无 VNC 检查通过等待、取消、新尝试继续、全部 8 字符及 8 次按下／抬起，六项焦点状态均为 1；真实 VNC 及锁屏解锁仍待验收，完整 XCTest 仍受本机缺少模块阻挡。

r31 真实 VNC 仍未收到首个字符（按下 0／抬起 1），未触发失焦错误；系统服务在输入源更新后明确记录 `ignore key event`，WindowServer 仅投递 keyUp，不能判为通过。r32 已签名替换运行（PID 9761）：公开测试框聚焦后等待输入源连续稳定一秒，最长四秒；未知／变化／取消／超时／失焦不放行，不补发首键。Debug／Release 各 284 项检查通过；实际无密码／无 VNC 自检验证新增准备阶段及全部 8 字符、8 次按下／抬起和六项焦点检查。布局沿用 r31-final 的 230 张证据，实际新增动态提示已查看。真实 r32 VNC／解锁仍待验收，XCTest 仍缺少模块。

r32 实测仍为 0 次按下／1 次抬起，焦点及输入源等待均通过。对本机系统服务的只读核对发现本地光标活动后约两秒抑制远程 keyDown，而该次首键仅距最后光标更新 1.931 秒。r33 已签名替换运行（PID 10558）：用公共经过时间接口等待键鼠／触控板及输入源稳定三秒，最长十秒；不改系统保护、不监听全局按键、不重放。Debug／Release 各 293 项检查及实际无需密码自检通过，8 字符、8 次按下／抬起、六项焦点标记全部匹配；布局证据沿用 r31-final，新提示已实际查看。真实 VNC／锁屏解锁仍待验收，XCTest 仍缺少模块。系统内部诊断只限定本机 macOS 构建，详见解锁候选记录。

用户已在签名 r33 本机通过真实 Mac 账户及当前桌面 VNC 键盘验证，代理从实际窗口核对成功结果，并复核 PID 10558 与包内 SHA-256 未变。完整公开样本 `Ab9!@_-+` 已精确匹配；此项升级为真实 VNC 键盘已验收，不再只有本机自检证据。该次未锁屏或提交登录输入，实际锁屏解锁仍待验收，设备 `oneClickUnlock` 保持关闭；本轮仅记录结果，未改代码或重新构建。

r34 针对“锁屏后没有自动解锁”重新读取实际窗口，仍是仅键盘检查的成功结果；该流程结束即关闭连接，没有实际解锁／倒计时已启动的证据。新版已签名替换运行（PID 11483），顶部直接显示 Mac 密码与“开始实际解锁测试”，其余入口明确“不会解锁”；运行及最终结果标记测试类型，避免误把键盘检查当作解锁。认证和实际输入逻辑未改，r33 真实 VNC 键盘验收保留，锁屏解锁仍待验收，设备能力仍关闭。Debug／Release 各 293 项、重新渲染 230 张原生布局及实际窗口检查通过；XCTest 仍缺少模块。详见解锁候选记录。

2026-10-23，用户确认 r34 按“开始实际解锁测试”进入倒计时并手动锁屏后，已自动解锁成功。代理读取到实际流程的单次登录提交结果，且 PID 11483、包内 SHA-256／UUID 与签名候选一致。本机当前登录会话的完整测试流程正式记录为一次实机通过，成功依据为用户反馈，程序只确认提交。TAB5 设备触发／持续后台解锁／重启登录未验收，设备 `oneClickUnlock=false`；没有保存凭据或修改代码。本轮只同步中英文验收记录。

2026-10-23 r35 接入候选：一键解锁新增独立开关、验证后钥匙串保存／删除及现有纯 Wi-Fi 设备请求；USB／BLE 密码导出仍关闭。豆包新增 VB-CABLE Mac 音频转送、Mac／DJI 选择与恢复、公开实际输入 IO 核验和最终文字自动返回预览。用户已安装官方 VB-CABLE，组件及端点识别通过；这不代表设备解锁或真实豆包识别已验收。335 项 Debug 检查通过，Release／布局检查进行中；详见 [新增接入记录](MAC-UNLOCK-DOUBAO-INTEGRATION-20261023.md)。

## English

The r35 integration candidate adds separate saved-unlock enablement, verified Keychain storage/deletion and the existing Wi-Fi-only device request route; USB/BLE password export remains disabled. Doubao gains VB-CABLE Mac audio routing, physical Mac/DJI selection/restoration, public actual-input-IO verification and automatic committed-text review. The user installed the official driver and endpoints enumerate, without establishing real device unlock or Doubao recognition acceptance. Debug passes 335 checks; Release/layout checks are in progress. See the linked integration record.

This unpublished candidate implements the current Windows structure: nine TAB5 cards, four ESP8266 cards, four account/data cards, and six bridge cards plus login startup. Music, Codex and resource tools live inside their corresponding modules. Persisted implementations cover birthdays, conflict-safe agenda sync, task pins, device registration/filtering, stock search/reordering, HD artwork browsing, 7/30-day weekly quota increments and hourly five-hour snapshots, selective configuration migration, notifications and configurable runtime monitoring. Update downloads validate component identity and checksums before opening the installer/tool.

Qwen, Kimi, MiniMax, DeepSeek, GLM, StepFun, Qianfan and MiMo have official API/console or loopback adapters. Credentials remain in Keychain and failures retain prior successful display data. Cloud wallets, subscriptions and local token counts remain distinct. Network settings do not alter system proxies; optional throughput observes a bounded 128 KiB transfer, not maximum bandwidth.

Native fixtures and deterministic checks exercise local storage, parsing and UI. Release compilation succeeds on macOS; XCTest is unavailable in this host's Command Line Tools. Real supplier accounts, updates/login startup and physical devices need separate acceptance. Full HD archive installation and browsing results are recorded separately. Windows PIN unlock and processor queue counters have no equivalent Mac implementation. No wire version, release tag or public download changes.

The latest reference is `codex/windows-tab5-multihost-20261021` at `79ffe7fb6b41819322283c2e151d52ca0b4f8204`. This follow-up adds adaptive two/three-column cards, explicitly partial cross-midnight quota carry-over with observation bounds, and authenticated USB/Wi-Fi/BLE device runtime diagnostics. Wireless refreshes coalesce, expire after ten seconds, retry the other live channel of the same host once after eight seconds, reject late/mismatched replies, and preserve stale samples. Existing Mac host ownership and full USB cadence already match the new contract. Windows login helpers remain platform-specific; device camera/attachment changes require firmware acceptance. The upstream real-account “yesterday = 4” case remains unverified.

The detailed follow-up compares the Windows forms and their services, including separate device-management menus, disabled/offline action gating, full ESP rotation pages, draft-safe birthday/agenda/computer/stock editors, source/provider controls, draft weather testing, live bridge status, notification suppression, configuration previews, runtime cards, microphone selection/testing and cancellable verified update downloads. TAB5 management has only Rename, Enable/Disable and Remove; the four maintenance actions belong only to ESP8266. Mac microphone choice does not claim the Windows DJI automatic selection/fallback behavior. Hardware, audio, supplier-account and installer acceptance remain separate from native fixture checks.

Native validation now includes 224 rendered settings views and 146 deterministic checks, including device-menu ordering, offline/disabled actions and exact UTF-8/bracket ZIP entry reading. The full painting archive is installed locally and the real HD viewer has been exercised at 1600% zoom, with pan, fit-to-window and image/text comparison. This evidence does not substitute for physical-device or microphone acceptance.

Both official complete HD archives are now installed and activated in the local gallery directory: edition 2026.10.16, 366 paintings (384 originals / 413 HD pages) and 366 calligraphy works (1,371 originals / 1,901 HD pages). Every archive part, full ZIP and imported file is checksum validated. The signed r14 app was used for initial acceptance. Real painting browsing, 1600% zoom, pan, fit, reading mode, device menus and input-device enumeration were checked. After the user unlocked the Mac on 2026-10-23, the final calligraphy UI check also passed: Huangzhou Hanshi Poems loaded a 3058×2141 HD image, all three pages, next/previous buttons, arrow-key paging, return to the first page, 1600% zoom, pan, Command-0 fit, introduction/viewing tips/original-text panels, and fullscreen exit/reentry.

The 2026-10-23 r15 follow-up fixes excessive wheel zoom: raw scroll deltas previously drove an unbounded exponent before the final 16× clamp. Pixel-based trackpad input and line-based wheel input are now normalized separately, each event can enlarge by at most 12% (with the reciprocal for zoom-out), and momentum is ignored. Pointer anchoring, pinch and Command-0 remain available. Thirteen additional regression checks pass, for 159 total; Release compilation and signing pass, while XCTest remains unavailable on this host. Native scrolling shows 100% → 112% → 125% → 112% → 100% in the painting browser and equivalent small steps in calligraphy fullscreen. Physical mouse/trackpad feel remains for user confirmation. This r15 candidate has since been replaced by r16.

The r16 follow-up explicitly clips the image canvas and saves/restores a bounds clip during drawing, addressing NSView's unclipped default on macOS 14 and later. Three additional pixel checks cover portrait, landscape and edge-panned images at 1600%, including unchanged pixels outside the viewport and restored graphics state; 162 deterministic checks, Release build and signing pass. XCTest remains unavailable. Native painting-window and calligraphy-fullscreen checks at 1600% keep titles and controls visible, including panning and vertically stacked reading mode in the normal window. Fullscreen side-by-side reading was not separately accepted because the user resumed interacting with the window. The running app is `artifacts/mac-gallery-clip-20261023-r16/AIBotBridge.app`; the user's current view is retained.

The follow-up candidate is now signed r20. Doubao Mac IME draft confirmation and existing-loopback-VNC checks do not establish full Windows parity. The actual r19 locked-session connection check authenticated and initialized successfully but never attempted input. r20 adds a separate one-use login-input test gated by Apple login-field focus, current-console ownership and an RFB marker/erase echo; unknown or unavailable public AX access stops it without blind input. Release and 197 deterministic checks pass. Actual account-password input, unlock, device unlock, real voice and TAB5-to-Doubao audio remain unaccepted; one-click unlock stays disabled. See the linked 2026-10-23 candidate record for exact boundaries.

After the user reported that r20 did not reveal a password field or unlock, the current signed r21 candidate adds bounded password-page preparation after VNC authentication: confirm the same Apple loginwindow, locate/focus a unique secure field, or send at most one Tab if no field is exposed. Existing input gates still apply. Release, 206 deterministic checks and 230 native-layout checks pass; the app is running with its test window open. Physical preparation and unlock are still unaccepted, and one-click capability remains disabled.

The r21 field subsequently appeared but unlock still failed. The actual window identified AX -25205 at the initial character-count read, before account-password input. The current signed r23 candidate handles that explicit unsupported attribute with a nonprinting focus round trip within the same signed loginwindow, while retaining console, original secure-field focus, cancellation and one-use submission gates. It neither reads protected text nor substitutes zero. Release, 228 deterministic checks and 230 native layouts pass, and the revised app is running. Actual round-trip behavior and unlocking remain unaccepted; device one-click capability is disabled.

The user's retained r23 result confirmed that the focus round trip failed before account-password input. r24 replaces that navigation prerequisite with a public case/digit/symbol echo in an app-owned secure editor before locking. Only successful echo starts a 15-second countdown, with the same VNC connection retained. Console, Apple loginwindow and original secure-field focus remain mandatory after locking, and key holds now match the latest Windows 40 ms timing. Real keyboard echo and unlock require human acceptance; device unlock remains disabled.

The actual r24 result confirms a public keyboard-sample mismatch without account-password input; absent keys versus mapping errors were not distinguished. The signed r25 app is running with a VNC-password-only keyboard check requiring no locking. Input sessions initialize a bounded single Raw pixel, and report absent characters, wrong case or other mismatches plus app-owned probe down/up counts, without recording keys. Its 243 deterministic checks and 230 native layouts pass. Real keyboard receipt and unlocking remain unaccepted, and device unlock stays disabled.

The actual r25 result stopped at exact graphics matching before the keyboard probe. Signed r26 then used send-only minimal viewer setup without a graphics-decoding prerequisite; exact local public echo remained mandatory. Failure-only optional nonblocking header metadata never authorized input. Debug/Release 244 deterministic checks passed; its unchanged layout retained r25's 230-view evidence.

The real r26 result received neither the first public character nor any down/up event. Apple system logs show that VNCAuth selected a separate virtual login session (265, onconsole 0, vfb 1), while the physical desktop remained session 257. r27 therefore independently implements Mac account authentication (RFB type 30), with an explicit no-lock keyboard button using a locally entered Mac login password for the existing loopback Apple Screen Sharing service. System launchd identity and Apple daemon signature are checked before credential transmission; nothing is persisted, no service or authorization setting is changed, and no alternate authentication is selected. The read-only Apple-service and 4096-bit ephemeral-DH probe passes without sending account credentials or input. Current-desktop keyboard echo and actual unlock still need human acceptance, and device oneClickUnlock remains false. Signed r27 is now running (PID 4845), with the account keyboard-test window in front and empty credential fields. Its 257 deterministic checks and 230 native layouts pass; XCTest remains unavailable. See the detailed unlock candidate record.

The real r27 connection authenticated to physical session 257 (console flag 1) and delivered one down/up before the combined focus guard stopped it; full character mapping and unlock remain unaccepted. Signed r28 is running (PID 5499) with a dedicated AppKit public-sample editor, automatic probe visibility, separate activity/key-window/editor errors and unchanged strict input gates, without reactivation or replay. Debug/Release 269 checks and 230 native layouts pass; XCTest remains unavailable. Real r28 keyboard/unlock acceptance is still pending.

Real r28 stopped when AppKit activity became false, with no user window switch. Signed r29-final is running (PID 6643), cross-checking system frontmost ownership and the exact app-owned public AX probe identity; mismatches stop without reactivation, refocusing or permission/Dock-policy changes. The actual no-password/no-VNC local check passes all eight public characters with six focus flags=1 and eight down/up events. Debug/Release 272 deterministic checks pass; unchanged layout retains initial r29 230-view evidence without a fresh render. The inactive-AppKit branch under real VNC and locked-session unlock still need separate human acceptance.

Actual r29-final lost both key-window and system-front ownership (active/key/front=0), so this is not merely an activity-flag false positive. Signed r30 is running (PID 7253) with temporary ordinary-app presentation for the unlock-test window; process metadata verifies UIElement → Foreground → UIElement across open/close, and reopening works. Graphics initialization/update requests are removed to match Windows input-only post-init behavior, while focus and no-replay gates remain. The real no-password local check passes eight characters, eight down/up and system-focus checks. Debug/Release 267 checks and 230 rerendered native views pass; five removed fixtures correspond to deleted graphics code. Real full VNC echo and locked-session unlock remain pending, and device oneClickUnlock stays disabled.

Actual r30 still loses foreground after its first public key, with Codex in front and only Shift recorded. Both attempts log an Apple Screen Sharing connection popover after the first key; the latest front change follows it by about 70 ms. This supports initialization-prompt interference without proving the internal cause. r31 therefore keeps the same authenticated connection waiting for a fresh fixed-footer Continue Keyboard Check click, without test keys, a lock countdown or app reactivation. The button enables after three seconds; waiting expires at thirty. Early, expired, stale, repeated and cancelled clicks cannot authorize input. The no-password local check exercises the same gate, and strict target/no-replay checks remain. Actual unlock is still unaccepted; see the detailed candidate record.

Signed r31-final is running (PID 8632), with 275 checks in Debug/final Release and 230 final native renders passing. Actual no-password/no-VNC checks pass waiting, cancellation, fresh continuation, eight exact characters, eight down/up and six focus flags=1. Real VNC/locked-session unlock remain pending; full XCTest remains blocked by the host's missing module.

Real r31 VNC still received no first character (zero down/one up), without a focus error. The server explicitly logged ignore key event after input-source updates; WindowServer delivered only keyUp. Signed r32 is running (PID 9761), requiring a one-second stable probe input source within four seconds before any sample key, with unknown/change/cancel/expiry/focus checks and no key replay. Debug/Release each pass 284 checks. Actual no-password/no-VNC validation passes the new preparation stage, eight characters, eight down/up and six focus flags. The unchanged layout retains r31-final's 230-view evidence and the new dynamic live status was inspected. Real r32 VNC/unlock remain pending and XCTest is unavailable.

Real r32 still reports zero down/one up despite valid focus/source preparation. Read-only inspection of this host's Apple service identifies remote keyDown suppression for about two seconds after local cursor activity; the failed key arrived only 1.931 seconds after the last cursor update. Signed r33 is running (PID 10558), using public elapsed-input metadata to require three seconds of quiet keyboard/mouse/tablet and source state within ten seconds, without changing protection, global key listening or replay. Debug/Release 293 checks and actual no-password validation pass eight characters, eight down/up and all six focus flags. Layout evidence remains r31-final, with new live text inspected. Real VNC/locked-session unlock remain pending and XCTest is unavailable. Internal diagnosis is limited to this host's system build; see the detailed candidate record.

The user has now passed real Mac-account/current-desktop VNC keyboard verification on signed r33. The agent confirmed the successful live-window result, unchanged PID 10558 and matching bundle SHA-256. Exact full Ab9!@_-+ echo is accepted over real VNC, beyond local-only evidence. This attempt did not lock or submit login input; locked-session unlock remains pending and device oneClickUnlock remains disabled. This turn records acceptance without source changes or a rebuild.

For the subsequent no-unlock report, the real window still showed keyboard-only success, whose connection closes on completion; there is no evidence of an actual-unlock flow/countdown. Signed r34 is running (PID 11483), with the Mac credential and Start Actual Unlock Test at the top. Other entries explicitly say they do not unlock, and progress/results identify the flow. Authentication and actual input logic are unchanged. Real r33 VNC keyboard acceptance is retained; locked-session unlock remains pending and the device capability stays disabled. Debug/Release 293 checks, 230 fresh native renders and the live layout pass. XCTest remains unavailable. See the detailed unlock candidate record.

On 2026-10-23 the user confirmed successful automatic unlock after starting r34's Actual Unlock Test and manually locking during its countdown. The retained window confirms a single login submission; PID 11483 and bundle SHA-256/UUID still match the signed candidate. One complete test in the current login session is now physically accepted based on the user's observation, while the program itself confirms submission only. TAB5-triggered/persistent background unlock and login after restarting remain unaccepted, with device oneClickUnlock=false. No credentials were saved or source changed; this turn updates bilingual acceptance records only.
