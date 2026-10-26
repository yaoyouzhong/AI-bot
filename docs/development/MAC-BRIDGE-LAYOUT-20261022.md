# Mac 桥接设备中心布局 / Mac bridge device-center layout

## 修改范围

- 参照本仓库 `windows-app/AIBotBridge/DeviceCenterForm.cs` 和现有设备中心截图，使用 AppKit 独立实现“我的设备／账号数据／桥接设置”导航、左侧设备选择和双列功能卡片。
- 我的设备包含 TAB5 和 ESP8266 的既有功能入口。USB、Wi-Fi、蓝牙分别显示实际桥接状态；尚未配对或未知固件保持明确标识。连接表单归入 TAB5“连接设置”，页面切换保留未提交的输入。
- 天气和股票、国产模型额度归入账号数据；服务状态、LAN 地址、小屏配对令牌和使用说明归入桥接设置。入口继续调用既有操作，不新增设备注册、授权、协议或自动配对行为。
- 菜单栏左键和“窗口”菜单打开设备中心，设备镜像仍可从右键菜单和 ESP8266 卡片进入。设备操作回执固定在窗口底部，内容区域单独滚动。
- 界面使用系统语义颜色、原生按钮键盘与辅助功能行为，支持浅色／深色和窗口缩放。候选使用指南已同步更新中英文入口。

## 验证

- macOS Release 构建通过；存在既有 `SerialBridge` Sendable 捕获警告。
- `scripts/build_macos_app.sh` 构建和签名核验通过；本地候选为 `artifacts/mac-layout-20261022/AIBotBridge.app`。
- 该候选的 `--self-test-tab5-ui` 通过：900×700、800×500、1120×800 窗口的浅色／深色原生渲染，导航和卡片回调，卡片边界及重叠检查，以及最小窗口的固定回执检查。截图位于同目录 `preview/`，为固定测试数据的 AppKit 渲染，不是设备在线证明。
- `swift test --package-path mac-app` 受本机 Command Line Tools 缺少 `XCTest` 模块阻塞，未宣称 XCTest 测试通过。
- `git diff --check` 通过。原有未提交修改保留；未推送、建标签或发布。
- 未切换正在运行的桥接；未刷写或操作 ESP8266／TAB5，未改变实际展示设置。本轮没有实体设备验收结果。

## English

The Mac bridge now follows this repository's Windows device-center structure: three navigation pages, a device sidebar and two-column action cards. TAB5 connection setup is separate from its overview. Existing ESP8266 controls, shared data settings and bridge tools retain their original callbacks. The menu-bar primary click opens Device Center; the mirror remains accessible. Device receipts stay outside the scrolling content. No pairing, data collection or wire-protocol behavior was added.

Release compilation, local app packaging/signature verification and native fixture rendering passed. The UI check covers three window sizes, both appearances, navigation/callback routing, card bounds/overlap and the fixed receipt at minimum size. XCTest could not run because the installed Command Line Tools lack its module. Pre-existing working-tree edits were preserved. The currently running bridge was not replaced; no device was flashed or operated, and no hardware acceptance is claimed.

## 后续：用户授权切换运行版本 / Authorized local switch

用户随后明确要求换成新版并打开布局。已通过正常退出快捷键结束旧候选，确认旧进程退出后启动上述签名候选；进程核对仅一个新版实例。原生界面已显示并置前“AI-bot · 设备中心”，三个页签、设备列表及卡片在当前深色模式下可见。观察时设备连接仍等待重连，不将窗口启动视为设备通信验收；没有重新配对、刷机或修改展示设置。

After the user explicitly requested the switch, the old candidate was quit normally and its exit verified before starting this signed candidate. Exactly one new bridge instance was observed. The native Device Center is visible in the foreground with its navigation, sidebar and cards in dark appearance. Device reconnection remained pending when observed; this is UI launch verification, not hardware communication acceptance. Pairing, firmware and display preferences were not changed.

## 后续：统一设置子窗口 / Settings-window follow-up

- 现有设置子窗口共用 `MacBridgeSettingsStyle`、`MacBridgeSettingsPage`、`MacBridgeSettingsShell` 和 `MacBridgeSettingsForm`：24 pt 外边距、16 pt 内容间距、12 pt 同行操作间距；可缩放、内容独立滚动、回执和保存／取消固定在底部。
- 范围包含 TAB5 显示设置、电脑控制／音乐／Codex／语音、图库与固件、首次安装与备份恢复、国产额度、天气／股票、ESP8266 刷机、桌宠图库，以及轮播、自动屏保和配对令牌表单。系统文件选择器和操作确认继续使用原生系统对话框。
- 设备中心为电脑、音乐、Codex、语音提供单独入口；交互窗口拆成四页签。账号数据增加单独股票入口，与天气共用同一草稿。保存校验出错时保留输入并在底部提示；取消和关闭不写入设置。ESP8266 轮播编辑保留原有已选页面顺序。
- 固件窗口去除初始化时固化的背景色，避免系统主题切换后出现黑底黑字；原生语义颜色随窗口外观显示。现有设备请求、刷写、音乐控制、语音授权等处理函数保持既有行为。
- 功能核对结果见 [Mac / Windows 功能差异](MAC-WINDOWS-FEATURE-PARITY-20261022.md)。本轮没有实现表中缺失的业务模块，也没有用占位按钮表示已实现。

Existing settings now use shared AppKit window, page, tab, footer and modal-editor components: 24 pt outer margins, 16 pt content spacing and 12 pt inline action spacing. The change covers display, interaction, resources, installation/recovery, quota, weather/stocks, ESP8266 firmware, pet gallery, cycle, saver and pairing editors. Native file pickers and confirmation dialogs remain standard system UI. Dedicated entry points expose existing interaction modules; weather and stock tabs retain one draft, validation errors keep the editor open, and cancel/close discards the draft. Existing selected ESP8266 cycle order is retained. Device operations and permission behavior are preserved. The linked audit separately records missing functional modules; this UI work does not claim feature parity.

### 本次验证与本机运行 / Validation and local launch

- 最终候选：`artifacts/mac-settings-layout-20261022-r2/AIBotBridge.app`。Release 构建、打包、内置刷机工具校验和严格签名核验通过；同目录有整个 App 的 `SHA256SUMS.txt`。原运行版本保留在原目录。
- 最终签名二进制的 `--self-test-tab5-ui` 通过，生成 95 张原生测试图（同目录 `preview/`）。覆盖主窗口三种尺寸及两个主题、子窗口各页签默认／最小尺寸及两个主题、内容宽度、固定底部区、导航回调、天气／股票草稿保留、输入拒绝与取消／确认。检查后修正了刷机按钮伸展和固件设备行的主题背景问题。
- `swift test --package-path mac-app` 已执行，但本机工具链缺少 `XCTest` 模块，测试目标未能编译。Release 构建保留既有 `SerialBridge` Sendable 警告；没有将构建或 UI 检查冒充 XCTest 通过。
- 正常退出此前候选并确认退出后，启动最终候选，进程核对仅一个新版实例。实际打开语音、电脑控制、股票、ESP8266 轮播、屏保和配对令牌窗口；取消按钮、Escape 和关闭窗口均返回主窗口。最后将“交互与权限 → 电脑控制”留在前台。
- 以上实际打开仅用于检查界面，没有保存业务设置、触发权限授权、配对或刷机。观察时 TAB5 等待连接、ESP8266 未连接；显示设置使用固定数据进行原生渲染，本轮不声称设备通信或实机显示验收。
- `git diff --check` 通过。未推送、建标签或发布；原有未提交工作保留。

The final signed candidate is `artifacts/mac-settings-layout-20261022-r2/AIBotBridge.app`, with an App checksum manifest and 95 native fixture images alongside it. Release compilation, packaging, bundled flash-tool verification, strict signature verification and the expanded UI self-check passed. XCTest remains blocked by the local toolchain's missing module; the existing SerialBridge Sendable warning remains. After a normal exit of the previous candidate, one final candidate process was confirmed. Voice, computer control, stocks, cycle, saver and pairing editors were opened locally; cancel, Escape and window-close paths returned correctly. The computer-control settings tab was left visible. No business settings, permissions, pairing or firmware were changed. Devices were not connected during inspection, so no hardware acceptance is claimed.
