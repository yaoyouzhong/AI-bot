# Mac / Windows 模块对齐工作记录

用户已于 2026-10-22 明确要求模块结构和实际功能全部对齐，并补齐 Mac 缺失模块。此前仅统一布局的交付不满足本次要求。本文件区分代码实现、本机验证和实体设备／真实账户验收。功能细目见同目录 MAC-WINDOWS-FEATURE-PARITY-20261022.md。

## Windows 基准

- TAB5：显示设置、语音设置、日历生日、日程待办、电脑控制、屏保图库、常用任务、数据设置、连接升级。
- ESP8266：显示设置、外观设置、数据设置、连接设置；设备管理菜单包含固件升级、设备信息、连接诊断、重置网络。
- 账号数据：模型账号、天气定位、自选股票、额度历史。
- 桥接设置：开机启动、服务状态、软件固件、配置迁移、提醒管理、运行监控、关于应用。
- 设备管理：添加、命名、启停、移除；Windows 当前每种型号最多一台，并非任意多台。

## 实现及验收清单

- [x] 生日存储、编辑、农历／闰月与既有日历字段同步
- [x] 日程存储、重复／提醒、并发编辑／删除／离线同步协议
- [x] 常用任务持久化、选择界面和设备列表优先显示
- [x] 设备注册、命名、启停、移除与按设备数据源／供应商筛选
- [x] 主窗口模块、顺序、分组严格对齐；已有 Mac 功能归入对应模块
- [x] 独立电脑控制、语音、天气、股票设置；股票搜索／列表／顺序
- [x] 图库浏览、详情、原图、导入和设备资源保持
- [x] 模型账户及八个国产供应商实际授权／读取路径
- [x] 额度历史 7／30 天图表和明细
- [x] 登录项、软件固件、配置迁移、提醒、运行监控、关于应用
- [x] 离线逻辑检查、原生界面检查、Release 构建与 XCTest 尝试
- [x] 签名候选、正常退出旧版、启动新版本并检查真实窗口

不复制第三方未授权实现；只按本仓库 Windows 可观察行为及现有协议独立实现 Swift。优先复用已有服务，保留用户工作区未提交修改。既有协议的新增 Mac 支持不改变 Windows／固件字段；如确需协议变化则按项目规则同步对端。密钥继续留在钥匙串。设备不在线时明确记录实机未验收。Windows PIN 解锁等平台专属能力不做虚假实现。

## 本机验证

- `swift build -c release --package-path mac-app` 通过；保留原有 SerialBridge Sendable 警告。
- `--self-test-parity`：74 项通过，隔离存储，不使用凭据／网络／硬件。
- `swift test --package-path mac-app` 已执行，因本机 CLT 缺少 XCTest 失败；没有声称完整 XCTest 通过。
- 原生 UI fixture：`artifacts/mac-parity-ui-20261022-r3`，包括监控设置与全部新增模块；检查固定底部操作区、默认／最小尺寸、浅色／深色和未保存草稿。
- 未刷机、未改 TAB5 当前页面与轮播；实体设备同步、全部供应商账户和更新安装尚未实测。

## 本机候选交付

- 应用：`artifacts/mac-module-parity-20261022-r1/AIBotBridge.app`，桥接版本仍为根目录 `VERSION` 的 0.6.10，未创建公开版本。
- 固定本机证书签名及 `codesign --verify --deep --strict` 通过；包内含 LICENSE、第三方声明、候选说明、刷机工具和钥匙串辅助进程。包旁 `SHA256SUMS.txt` 校验 11 个文件。
- 包内执行 parity 74 项、钥匙串辅助进程身份／私有管道探针和刷机工具离线自检均通过；没有访问真实凭据或设备。
- 旧布局候选已正常退出；r1 当时等待钥匙串授权。后续已完成授权并检查真实窗口，见最新跟进。
- UI fixture 共生成 172 张图；本轮检查主窗口九项、日程、电脑控制四个入口、模型账号和监控设置等视图。


## GitHub 最新开发分支跟进

- 已 fetch `codex/windows-tab5-multihost-20261021`，参考精确提交 `79ffe7fb6b41819322283c2e151d52ca0b4f8204`；未切换或覆盖当前工作区。
- 新增自适应两／三列、跨日额度补记及采样边界、USB/Wi-Fi/BLE 设备运行诊断、USB 认证失败升级保护。多电脑归属、BLE 短证明和完整 USB 帧已逐项对照；设备相机等改动留在固件边界。
- Release 构建通过；独立自检扩大到 110 项并通过，含加密诊断 RPC、错误证明／会话／通道、迟到回包、一次备用通道、过期缓存、跨账户／套餐／重置及旧历史解码。
- `swift test --package-path mac-app` 再次执行，仍因缺少 XCTest 失败。没有把独立检查描述为 XCTest 全量通过。
- 原生布局检查通过：`artifacts/mac-parity-latest-ui-20261022-r1`，177 张 fixture；检查默认／最小窗口、浅色／深色、模块点击路由、草稿、固定底部区域与三／两列切换。
- 候选 `artifacts/mac-module-parity-20261022-r2/AIBotBridge.app` 固定本机证书签名校验通过，包内 110 项、Keychain helper 身份／私有管道与离线刷机工具检查通过。包旁 SHA256SUMS 覆盖 11 个文件，包含许可和第三方声明。仍为桥接 0.6.10 本地候选。
- r1 当前窗口已检查，随后正常退出；r2 已启动。用户本人完成系统钥匙串授权后，实际检查三列九模块、运行监控设备诊断页及离线反馈、额度表格选择后的采样明细，均正常。最后关闭子窗口，把“我的设备”留在前台。未绕过系统授权。
- 当前 TAB5 未连接；本轮没有固件刷写、显示设置变更或 USB 接管／仅蓝牙诊断实机验收。未 push、打标签或发布。

Latest follow-up: the signed r2 bundle is running after the user completed the system Keychain prompt. Live UI checks confirmed the nine-card three-column device center, the device-diagnostics page with an honest offline result, and quota observation details. Release build, 110 isolated checks, native layout validation and bundle/checksum verification passed. XCTest remains unavailable; the disconnected TAB5 prevented physical transport acceptance.


## 紧凑布局调整

用户反馈窗口和按钮太宽，Mac 默认设备中心改为 920×620，侧栏 164。九模块默认三列，每卡约 220；窗口收窄自动两列，账号／桥接／ESP8266 页共用卡片宽度上限 240。卡片位置与窗口横向最小尺寸解耦，缩放不再被旧行宽反向限制，卡片对象和点击路由保留。

- 原生布局 fixture 检查通过：`artifacts/mac-compact-layout-ui-20261022-r4`，覆盖默认／最小／放大尺寸、全部设置页、浅色／深色、按钮宽度上限及点击路由。
- `swift test` 已执行，仍因本机缺少 XCTest 无法运行；调试构建通过。
- 本轮仅调整电脑端窗口布局，不改变设备协议、数据、展示或轮播设置。

Compact layout follow-up: the Mac device center now defaults to 920×620 with a 164-point sidebar. Cards stay around 220 points at the default size and are capped at 240 on every page. Native fixtures verify compact/default, minimum and enlarged layouts, both appearances and action routing. XCTest remains unavailable in this host's toolchain; no device protocol or display preference changes are involved.

- 最终紧凑候选：`artifacts/mac-module-parity-20261022-r4/AIBotBridge.app`。Release、固定证书签名校验、110 项独立自检均通过；SHA256SUMS 记录包内 11 个文件。原生 fixture 完成 177 张图。
- 已正常退出 r2，启动 r4，并确认设备中心九模块正常出现；用户随后开始查看语音子窗口。本次没有操作语音权限或改变个人设置。

The final compact r4 bundle passed Release compilation, signing verification and all 110 isolated checks. It replaced the running r2 process successfully; the device center was verified before the user began viewing a settings subwindow.

## 连接升级与图库全屏对齐（r7）

参照 Windows `79ffe7f` 的 `Tab5ConnectionForm`、`GalleryPackForm`、`GalleryBrowserForm`、`GalleryImageView` 和 `GalleryReaderTabs`，修正之前仅有入口／整窗放大的实现：

- 连接升级改为 640 点宽独立窗口，Wi-Fi、USB 配对、固件升级、语音、传输测速五页。USB 配对页保留 Mac 蓝牙开关与首次安装入口。网络列表最多 10 项，支持备注、增删、保存连接，失败保留已有列表。升级状态、更新说明和启动核验放在同一页。
- 接入已有 USB 测速控制和已认证 RAM 模式字节校验；图库写卡允许真实图库 RPC，互斥升级／照片／语音。开始不重发，只读回报有限重试，取消／超时清理。真实 USB、Wi-Fi、BLE 速度和设备写卡尚未实测，绝不使用模拟数值作为设备结果。
- 屏保图库管理独立显示版本与作品数；正式版本检查、带 SHA-256 的流式下载、断点续传、取消、标准／高清导入及旧包保留。浏览器提供缩略目录、即时搜索、日期定位、循环翻页和独立全屏窗口。
- 全屏使用完整可变尺寸画布；1–16 倍相对适配缩放、滚轮／双指锚点缩放、受限拖动、⌘0 复位、双击进入／退出。I 开关图文对照，宽屏并排、窄屏上下；简介、欣赏提示、原文可选择复制，左右键切栏目，Esc 先退导读再退全屏。
- 修正导读按 `workId` 读取；原文不再混在无序字典值中。加入原生编辑菜单支持剪切／复制／粘贴／全选；目录定位到选中作品。更新高清 ZIP 测试为有效原图索引，符合现有导入校验。
- 133 项独立检查通过，覆盖真实加密 RPC 分派、测速数据校验与互斥、Wi-Fi 响应校验、导读映射、取消保留旧图库等。206 张原生 UI fixture 图覆盖五页连接、图库管理、全屏／普通、图文对照、最小尺寸和深浅色；r6 桌面实测确认正式图库版本查询、当前 366 件名画、全屏缩放、导读快捷键和十页长卷首尾循环。
- `swift test` 已执行，受本机 Command Line Tools 缺少 XCTest 限制，不能声称 XCTest 通过。TAB5 当前断开；本轮未改设备 Wi-Fi、未测速、未刷机、未改展示设置。桌面验证不能代替设备实测。

Connection/gallery follow-up: the Mac implementation now uses the five Windows connection tabs, saved-network management, existing benchmark protocols, a separate gallery manager and a genuine fullscreen image/reading viewer. The browser supports thumbnails, search/date lookup, wrapping pages, zoom/pan, fit, comparison chapters and keyboard controls. It also fixes guide lookup by work ID and adds the standard native Edit menu. All 133 isolated checks and 206 native layout renders passed; live r6 UI verified release lookup, the installed 366-work collection, fullscreen reading/zoom and ten-page wraparound. XCTest is unavailable on this host, and physical transport/upgrade acceptance remains pending with TAB5 disconnected. No device settings or firmware were changed.

最终包：`artifacts/mac-gallery-connection-20261022-r7/AIBotBridge.app`；Release 编译、固定证书签名校验与 133 项独立自检再次通过，`SHA256SUMS.txt` 覆盖 11 个包内文件。已正常退出中间 r6 并启动 r7，进程路径已核对。未执行提交、推送、标签或发布。

Final package: `artifacts/mac-gallery-connection-20261022-r7/AIBotBridge.app`. Release compilation, signing verification and all 133 isolated checks passed. The 11-file checksum manifest accompanies the bundle; r7 replaced the intermediate r6 process. No commit, push, tag or release was performed.

r7 最终桌面复验：编辑菜单出现，⌘V 搜索即时筛选《漢宮春曉圖》成功；初始目录自动滚到当日选中作品；再次进入独立全屏并打开简介对照，保留在第 1/10 页供用户查看。标准名画与书法正式版本查询成功，本次没有下载或替换用户图库。

Final r7 live verification confirmed the native Edit menu and Command-V search, automatic selected-row scrolling, and fullscreen image/introduction comparison. The ten-page scroll was left at page 1 for the user. Official collection metadata was fetched successfully; the user's installed gallery was not replaced.
