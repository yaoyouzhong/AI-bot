# Mac DeepSeek / GLM 账户消费交接（2026-11-02）

## 范围和源码身份

- 来源 / 接收：Windows 行为契约 / macOS。开发分支 `codex/mac-0615-release`，基础提交 `657f22e76f624b3969e5fc2ffc8b95c0c2111525`。实施时 Windows 未提交工作区和指定 DeepSeek 交接文件不可用，行为以用户提供的契约为准；随后从远端取得 Windows 已提交的 `DEEPSEEK-WEB-DEVICE-HANDOFF-20261102.md` 和 `GLM_BALANCE.md`，核对的账户隔离、设备投影及 GLM 同次配对规则与本交接一致。Windows 本地未提交文件仍不可读取。
- 不涉及版本、发布负责人、构建物或发布授权；未推送、打标签、发布或刷机。
- 复用现有 `domesticQuotas` 字段，不修改协议、Windows 或固件；无需重新刷机。平台实现使用 WebKit，不移植 Windows 代码。

## 对齐清单

| 内容 | 实现位置 | 状态和证据 | 剩余问题 |
| --- | --- | --- | --- |
| DeepSeek 网页与 API 隔离，设备仅投影网页观测，同次余额和消费、24 小时限制 | `DataSources.swift`、`AppDelegate.swift`、`MacDomesticSources.swift` | 代码及合成回归 | 真实页面对照、账户切换后失败及两设备待验收；无法从网页响应确认账户身份变化，旧缓存与登录切换的关联仍需验证 |
| GLM 同页获取并校验新旧余额，匹配时使用旧报表消费 | `MacDomesticBrowser.swift`、`MacDomesticQuota.swift` | 代码及合成回归 | 调用官方页面报表请求模块；真实授权账户刷新已取得配对消费；旧接口不可用时仅显示余额；升级后未经配对证明的旧消费不复用，新版配对缓存可恢复 |
| ESP8266 USB / HTTP、TAB5 投影 | `AppDelegate.swift` | 沿用字段及链路，合成回归 | 用户确认 TAB5 GLM 和 DeepSeek 均显示累计消费；用户确认 ESP8266 余额显示正常；累计消费栏待单独确认，未刷机 |
| Mac 账号窗口提示 | `MacDomesticQuotaWindow.swift` | 文案修正；窗口布局未改；已查看常规与最小尺寸离屏渲染，账户行可读、未截断 | 真实账户状态和设备待验收 |

## UI 设计记录

使用场景为 DeepSeek 的“分别显示”模式，账号窗口中 API 余额与网页消费分列；设备显示网页账户数据。沿用原窗口 1160×800、最小 940×620、原有上下排列、间距、字体和颜色，原按钮、点击区域及状态不变。网页账户行显示余额、消费、成功时间和陈旧状态，缺失金额以 `--` 表示；说明末句改为“设备显示网页登录账户余额和消费”，避免将 API 余额误认为设备来源。使用隔离样例渲染并查看常规及最小尺寸，修改的账户行和说明可读、未截断；预览不包含真实账户，真实设备验收另列。

## 验收

- macOS：`swift test --package-path mac-app` 通过（426 项）；`--self-test-domestic-capture` 97 项、`--self-test-domestic-contract` 61 项通过，均为隔离合成数据。`swift build -c release --package-path mac-app` 通过；合成回归不等于真实账户、安装包或设备验收。
- 未读取或记录真实金额、Cookie、令牌、请求头及完整官方响应。2026-11-02 本机后台候选真实刷新取得 DeepSeek 网页余额与消费，及 GLM 总览余额与配对报表消费；未记录金额。用户确认 TAB5 的 DeepSeek 和 GLM 均显示累计消费；用户确认 ESP8266 余额显示正常；尚未对照官方页面或单独确认 ESP8266 累计消费栏。
- 无发布候选、附件、签名、公证或设备刷机记录。发布前需重新检查账号切换、真实页面认证、设备和窗口显示，并按统一流程明确剩余风险。

2026-11-02 实机反馈：TAB5 GLM 已显示累计消费，DeepSeek 未显示。原因是启动快照使用网页账户，周期发布却使用 API 快照；修正 `AppDelegate.updateTitle` 的 TAB5 发布投影，完成 macOS 全量测试及 Release 构建，修正候选已后台启动，两个账户成功刷新；ESP8266 轮播及原页面顺序、间隔已核对。用户随后确认 TAB5 DeepSeek 已显示金额；GLM 此前已确认显示。用户确认 ESP8266 余额显示正常，其累计消费栏及与官方页面逐项对照仍待验证；未更改固件或设备页面。
