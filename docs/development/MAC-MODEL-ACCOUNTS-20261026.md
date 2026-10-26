# Mac 模型账号与小屏显示修复 / Model accounts and ESP display

2026-10-26，本地 R31 候选；不是公开发布。Windows 源行为参考为本仓库 `windows-app/AIBotBridge/MigratedDomestic/DomesticQuotaService.cs`，未引入其他项目源码。

## 使用入口 / Entry

Mac 菜单栏 AI-bot → 设备中心 → 账号数据 → 模型账号。左侧选择厂商，右侧为官方网页。下拉选择额度来源；API 或本地服务凭据通过“接口设置 → 保存并测试”写入现有钥匙串服务。“在浏览器打开”提供外部浏览器查看入口，但外部浏览器登录状态不会自动变成内嵌网页授权。

Open AI-bot in the Mac menu bar → Device Center → Account data → Model accounts. Select a provider on the left and use its official page on the right. Choose the quota source; API/local-service credentials are saved through API settings using the existing Keychain service. Opening the external browser does not import that browser's login into the embedded page.

## 对齐范围 / Scope

- 14 个厂商入口：百炼、Kimi、MiMo、智谱、火山方舟、MiniMax、DeepSeek、百度、腾讯云、华为云、讯飞、阶跃、百川、零一万物。
- 额度仍仅来自已经接入的 8 家来源；其余厂商明确标注仅官方网页，不推算或伪造余额。
- 已配置的 API 来源和旧 DeepSeek API 授权保留。新增 DeepSeek 网页余额解析，必须有明确货币及有效金额；拒绝失败响应、冲突金额和不明币种。
- 内嵌网页使用系统 WebKit 持久存储，不读取或导出 Cookie、密码、请求头。旧临时会话需要重新登录一次；之后是否保持登录由厂商会话期限决定。
- 关闭窗口后保留已授权网页并每 120 秒尝试刷新，桥接重启后恢复已选择网页来源的会话。正在前台操作的网页不被计时器刷新打断。60 秒没有额度响应或网络失败保留旧缓存；失败／限流退避 5 分钟，显示登录和更新时间信息。
- 网页和 API 来源切换后，旧来源的在途结果不会覆盖新来源；Claude/Codex 仍沿用本机官方客户端授权。余额、套餐窗口和本机 token 不混算。

The window contains the same 14 provider entries as Windows, with the existing eight quota integrations. Other entries are explicitly web-only. Existing API sources, including legacy DeepSeek authorization, are retained. Web balance parsing requires explicit currency and a valid amount. System WebKit owns persistent sessions; no cookies, passwords or request headers are exported. Old temporary sessions require a fresh sign-in. Selected web sessions retry every 120 seconds while hidden and resume on app restart. Active foreground interaction is not interrupted. Responses time out after 60 seconds; failure/rate limiting backs off for five minutes while preserving the last successful sample. Switching sources invalidates older in-flight results. Account quotas remain separate from local token counts.

## 智谱原因 / Zhipu diagnosis

旧窗口使用临时 WebKit 数据存储，关闭时停止页面并将余额标旧；常规额度定时器跳过网页来源，因此关闭或重启后没有新的财务响应。真实界面确认显示旧缓存。修复通过恢复官方网页会话及后台刷新解决这条链路；真实余额验收须在右侧完成官方登录后观察成功时间推进，不能把编译或合成 JSON 测试当作已取得实时余额。

The old temporary web session stopped on close, marked its balance stale, and was excluded from regular API refreshes. This explains the observed stale balance after close/restart. Persistent official web sessions and background refresh repair that path. Live acceptance requires a successful official sign-in and an advancing success timestamp; builds and synthetic JSON checks do not establish live account access.

## 显示修复 / Display fixes

- 天气地名按实际字形边界缩放至 122×26，不再按固定字号截断；股票名称以 21 像素目标字号适配现有 156×20 行。协议和资源尺寸不变。
- 独立 ESP8266 0.5.4 候选按实际 Font2 字宽扩大 Codex 重置卡，左右各至少 6 像素、上下各 4 像素留白；套餐标签避让，桌宠动画共用同一排除边界。TAB5 固件不改动。

Weather place names fit their actual glyph bounds within the existing 122×26 bitmap; Chinese stock names target 21 pixels within each existing 156×20 row. The separate ESP8266 0.5.4 candidate measures actual font widths, adds badge padding and shares its bounds with pet-animation exclusion. No protocol/resource-size or TAB5 firmware change.

## 验证边界 / Validation

- Mac Release 编译通过；隔离逻辑 2,974 项、窗口生命周期 27 项、HTTP 回环 16 项、发现回环 38 项通过。
- 真实 AppKit 固定数据渲染包含最小／默认窗口及深浅色；14 厂商选择、来源隔离、失效登录保留缓存、成功恢复检查通过，无网络／真实凭据。
- `swift test` 已执行，当前 Command Line Tools 缺少 XCTest 模块，不能记作通过。
- ESP8266 PlatformIO 编译通过；从生产函数抽取并使用实际 TFT Font2 字宽的 1,350 组日期／数量边界检查通过。
- 完整 Flash 私有备份、写入读回、真实启动及页面视觉验收分别记录在本地证据目录，尚未完成的步骤不得由编译结果替代。

Release and the listed isolated/native checks pass. XCTest remains unavailable in the installed Command Line Tools. PlatformIO and 1,350 actual-font geometry fixtures pass. Full private Flash backup, read-back verification, hardware boot and visual acceptance are separate evidence. No push, tag or public release.

本地证据：`artifacts/development/mac-model-accounts-r31-20261026/`；Flash 原机备份保存在用户 Application Support 的 `AI-bot/device-backups`，不进入仓库或发布包。

## 部署记录 / Deployment

R31 最终签名应用已运行，两块设备串口均打开。ESP8266 已完成私有 4 MiB 原机备份、0.5.4 写入及读回摘要校验，随后在 460800 波特率取得三个递增运行时间样本。大块读取存在串口噪声，备份改为 16 段，每段经过工具校验与必要重试，全部通过后才写入。原六页、顺序及 15 秒间隔保持一致，已恢复自动与轮播；Codex／Claude 桌宠资源摘要一致。TAB5 未刷机。

The final signed R31 app is running with both serial ports open. ESP8266 has a private complete 4 MiB backup, verified 0.5.4 write/read-back and three increasing-uptime samples at 460800 baud. Serial noise required 16 individually validated backup segments with retries before writing. Auto/cycling, all six pages, order, 15-second interval and pet-resource identities are preserved. TAB5 was not flashed.

真实界面已显示左侧 14 家厂商及右侧百炼／智谱官方页面；智谱进入官方登录页。已请用户自行登录并观察成功时间推进，以及检查小屏的重置卡、地名与股票字号。以上真实余额和视觉验收尚待用户反馈，不记作已通过。

The live UI shows the provider sidebar and official Bailian/Zhipu pages, with Zhipu at its official sign-in screen. Live balance freshness and the three physical display fixes await the user's sign-in/observation and are not yet marked passed.

## R31 用户验收与 R32 后续调整 / Acceptance and follow-up

用户已确认 R31 三项显示正常：重置卡能装下日期、天气地名完整、股票中文字号合适；智谱已登录并成功更新。实际关闭账号窗口后，官方成功时间从 2026-10-26 08:02:27Z 推进至 08:04:29Z，证明后台刷新继续工作，不仅是保留旧缓存。

用户随后指出智谱推广弹窗无法关闭。实测页面提供的 Close 控件可关闭，但弹窗超出当前视口，使可见关闭按钮被裁切。R32 增加每厂商 60/75/90/100/125% 网页缩放，保留官方关闭行为，不拦截或自动同意页面对话框。

天气右上角经 Windows 调用方与 ESP8266 协议对端核对，Mac 原实现将 `AQI 数字＋天气` 放在一个 12px 文本行，与 Windows 不同。R32 改为 42px 彩色空气质量徽标、6px 间距及 52px 独立天气文字；沿用原 100×30 资源、官方来源 AQI 值及标准。未知空气质量不显示健康徽标。天气文本仅在小屏图像中压缩为短标签，不改完整预报。

按用户后续偏好，ESP8266 0.5.5 将重置卡 Font2 由 16px 缩为 14px，保留已验收的边框和避让范围，使用原有缩放器及分配失败后的原字号回退。1,350 组实际字体尺寸测试通过。写入前核验设备仍匹配精确 0.5.4 回退镜像；保留上一轮完整私有 Flash 备份，只更新程序区，不擦除文件系统。

R32 Release、3,002 项隔离检查、原生窗口深浅色／最小尺寸、缩放选择保存及账号失败恢复检查通过；XCTest 因本机模块缺失仍未通过。真实重启后登录保持、网页缩放和新天气／14px 小字另行记录。

The user accepted R31's badge fit, complete place names, stock font size and successful Zhipu sign-in/balance refresh. While its window was closed, the official success timestamp advanced from 08:02:27Z to 08:04:29Z. The official advertisement's Close control worked, but was clipped outside the viewport. R32 adds per-provider web zoom without auto-accepting or suppressing dialogs. The weather bitmap now matches Windows' separate colored air badge and condition zones while preserving source AQI and protocol dimensions. ESP8266 0.5.5 reduces reset-credit text from 16px to 14px with the accepted frame retained. Release, 3,002 isolated checks, native UI/zoom checks and 1,350 font-layout fixtures pass; XCTest remains unavailable. Hardware restart/session retention, live zoom and final physical appearance are recorded separately.

R32 已完成部署：签名应用为 `artifacts/mac-model-weather-r32-complete-20261026/AIBotBridge.app`，单实例运行，两个串口均打开。0.5.5 写入／摘要读回及三个递增运行时间样本通过；六页自动轮播、顺序、15 秒间隔与桌宠摘要保持一致。智谱重启后无需再次登录，实际官方成功时间推进，75% 网页缩放已通过 UI 操作生效并保存；原广告已通过官方 Close 控件关闭。窗口已收起，后台刷新继续。R32 天气徽标布局及 14px 重置卡观感尚待用户最后反馈。

R32 is deployed as the single signed bridge with both serial ports open. ESP8266 0.5.5 passes write/read-back and three increasing-uptime samples; cycling and pet identities are unchanged. Zhipu's official success time advances after restart without a new login, and live UI selection confirms saved 75% web zoom. Its advertisement was closed through the official Close control. Windows are closed and background refresh continues. Final physical weather/badge appearance awaits user feedback. Evidence: `artifacts/development/mac-model-weather-r32-20261026/`.


## R33 数字完整性与 Windows 股票字号 / Digits and stock type size

用户反馈 R32 缩小后的重置卡数字缺笔画、对齐不齐，并要求方框相应缩小、股票代码及中文字号对齐 Windows。实际 TFT Font2 位图复现最近邻缩小会跳过细笔画；0.5.6 改为整数面积覆盖采样，保持 14px 高度。数量固定左侧，MM/DD 日期补零并靠右；常规宽度由 80px 缩至 73px，两行高度由 44px 缩至 40px，动画避让使用相同边界。临时字形缓冲分配失败时整行回退内置小字，避免部分放大或重叠。

Windows 股票中文使用 21px 常规字重、156×20 行、右对齐、RGB(210,210,210) 及末尾省略号。Mac 去掉额外上下留白并对齐这些参数，长名称不再整体缩小；系统字体字形仍因平台而异。股票代码由同一固件 Font2 绘制，原本就是 16px，无需按主机分支调整。

验证：Release 和 PlatformIO 编译通过；签名程序 3,002 项隔离、27 项窗口、16 项 HTTP、38 项发现检查通过；3,450 项细笔画检查及 1,354 项日期／数量／分配失败布局检查通过。swift test 已执行，因本机缺少 XCTest 失败，未计作通过。既有 0.5.5 精确回退镜像在设备上验证后，仅写程序区并读回校验；0.5.6 三个递增运行时间样本、460800 串口握手及双设备连接通过。自动模式、原六页顺序、15 秒间隔和桌宠摘要均一致，未改 TAB5 固件及展示设置。

用户最终反馈「都正常，大小合适」，本轮重置卡完整性／对齐／方框尺寸与股票字号实机验收通过。R32 天气徽标观感没有新增明确反馈，不据此扩展验收范围。证据：`artifacts/development/esp-credit-alignment-r33-20261026/`；运行应用：`artifacts/mac-credit-stock-r33-complete-20261026/AIBotBridge.app`。未推送、打标签或公开发布。

The user reported incomplete digits after R32's nearest-neighbor reduction, uneven alignment and an oversized badge. ESP8266 0.5.6 integrates source-pixel coverage, keeps 14px text, aligns counts and zero-padded MM/DD dates, and reduces the typical two-row badge from 80×44 to 73×40. Allocation failure falls back to the built-in small font for the whole row. Mac stock labels now match Windows' 21px regular type, 156×20 rows, right alignment, gray color and end ellipsis; platform glyph outlines can differ. Stock codes already share 16px firmware Font2. Release, PlatformIO, 3,002 parity, 27 lifecycle, 16 HTTP, 38 discovery, 3,450 raster and 1,354 geometry checks pass; XCTest remains unavailable. Exact firmware read-back, increasing uptime, USB handshake and both device connections are verified, with cycle settings/pets preserved. The user accepted all four R33 physical checks. No new weather acceptance, TAB5 update or public release is claimed.


## R33 提交边界历史记录（DeepSeek 后续见 R34） / Historical commit boundary

2026-10-26 提交前复核：本轮 R33 重置卡与股票字号已获用户实机确认；完整 XCTest 仍需具备 Xcode 的环境／CI 验证。DeepSeek 官方 `GET /user/balance` 仅返回可用余额及其组成，不含已消费金额。Windows 的控制台解析另读取 `total_costs`；当前 Mac 控制台仅解析余额，因此“已用金额”仍是独立的功能对齐待办，不能宣称模型账号全部字段已完全对齐。参考：https://api-docs.deepseek.com/api/get-user-balance/ 。

Pre-commit review: R33 reset-credit and stock type-size hardware checks are accepted; full XCTest still requires an Xcode-capable environment/CI. DeepSeek's official balance API has no spending field. Windows separately parses console `total_costs`, while Mac console parsing currently handles balance only. Spent-amount parity remains a separate follow-up; complete field parity is not claimed.


## R34 DeepSeek 已用金额 / DeepSeek spending parity

按用户要求补齐 Windows 已有消费字段。官方控制台 `total_costs` 按选定余额币种读取 `amount`；缺币种沿用 Windows 同一摘要默认币种，显式其他币种不混算。真实零值有效，布尔、非有限值、负值和互相冲突的同币种结果不覆盖已有消费记录。余额 API 本身不提供消费金额，保持 `usedCost=nil` 的解析语义。

Mac 数据缓存对 DeepSeek 单独保存消费采集时间（本机偏好，不新增协议字段）。同币种仅余额刷新保留消费金额及原采集时间；凭据变更调用既有清理入口，币种变更也清除旧消费。超过 24 小时停止投影消费字段，避免新余额时间掩盖旧消费；重启后仍保留原采集时间。模型账号顶部增加已用金额和网页采集时间；ESP8266 `usedCost`、TAB5 `cost` 沿用原字段与显示，无固件修改。

Release、严格签名、3,019 项隔离检查（较 R33 增加 17 项）、27 项窗口生命周期、16 项 HTTP、38 项发现及原生 UI 固定数据检查通过。swift test 已执行，仍因本机缺少 XCTest 失败。双语更新日志、版本检查与 git diff --check 通过。

真实运行已切换为 `artifacts/mac-deepseek-cost-r34-complete-20261026/AIBotBridge.app`。用户既有 DeepSeek 网页会话有效，无需再次登录；通过正常模型账号 UI 将来源从余额 API 切换为官方控制台，真实累计消费字段已成功读取，模型账号顶部已显示已用及采集时间。证据仅保存来源、字段存在性、更新时间及 stale，不记录具体余额、消费、账号或凭据。小屏 0.5.6 与 TAB5 0.2.279-ui 未刷写；双串口保持打开，原六页、顺序、15 秒自动轮播与桌宠一致。用户确认「两块屏幕都正常」，ESP8266 USED 与 TAB5 消费金额实机验收通过。未 commit、push、创建标签或公开发布。

R34 implements Windows' existing console spending field: currency-matched `total_costs.amount`, valid zero, and no overwrite by malformed, non-finite, negative or ambiguous samples. The balance API still supplies no spending. Local-only metadata preserves the original usage timestamp across balance-only updates and restart; credential/currency changes clear it, and projection expires after 24 hours. Model accounts show spending and its web observation time; existing ESP8266/TAB5 fields require no protocol or firmware change. Release/signing, 3,019 isolated, 27 lifecycle, 16 HTTP, 38 discovery and native UI checks pass; XCTest remains unavailable. The existing official login session successfully supplied live spending after selecting console mode through the UI. Sanitized evidence records only presence/freshness/source, with original cycling/pets and both serial connections preserved. The user confirmed both physical displays are normal; ESP8266 USED and TAB5 spending display acceptance passed. Evidence: `artifacts/development/mac-deepseek-cost-r34-20261026/`.

R34 后台实测：关闭模型账号窗口后，消费采集时间由 2026-10-26T08:55:35Z 推进至 08:57:37Z，字段仍存在且 stale=false；确认消费金额后台刷新生效，具体金额未记录。

R34 background check: after closing the account window, the spending observation advanced from 08:55:35Z to 08:57:37Z with the field present and stale=false. No amount was logged.


## R36 总余额与来源隔离 / Total balance and independent sources

用户最终选择「默认分开：API 总余额、网页已用金额各自标明来源，设备不合并未验证数据」。本节取代 R34 中可能将 API 余额与网页消费共同投影的方案；当前用户已确认两份授权属于同账号，也不作为自动合并的特例。

- 官方余额 API 直接使用 `total_balance`，已含赠送，不再二次加赠送。网页摘要的 `normal_wallets` 与 `bonus_wallets` 按同币种以 Decimal 合计；若有权威 `balance_infos.total_balance` 则优先采用，避免重复累计。缺失分组、冲突或非法值不覆盖最近成功缓存。
- 分开模式分别保存 API 余额和网页消费、成功时间与 stale；网页失败不使 API 余额变旧。设备统一 DeepSeek 卡仅投影 API 总余额，`usedCost/cost` 留空。电脑模型账号明确标注两份来源与未核实同账号提示。未配置 API Key 时回退纯网页；纯网页来源可显示该来源自己的总余额与消费。
- 余额 API 不提供可与网页登录核对的身份；币种相同、余额接近或 API Key 指纹不变均不作为账号一致证明。使用虚构的 API A 余额 90、网页 B 余额 12／消费 5 验证了事件顺序、重启、过期与网页失败隔离。无协议或固件变更。
- R35 为总余额中间构建，未部署；最终 R36 签名应用为 `artifacts/mac-deepseek-separated-r36-complete-20261026/AIBotBridge.app`。Release、3,034 项隔离检查、27 项窗口生命周期、16 项 HTTP、38 项发现、原生 UI 检查通过。`swift test` 已执行，仍因本机缺少 XCTest 模块失败，不能记为通过。
- R36 已实际部署并通过正常 UI 选用分开模式。两来源均取得真实成功数据，设备数据包含余额且不包含已用金额；记录仅保留来源、字段存在性、时间与 stale。用户确认「两块屏幕都正常」。ESP8266 0.5.6 与 TAB5 0.2.279-ui 未刷写；原六页、顺序、15 秒自动轮播、桌宠与双串口连接保留。窗口已收起。
- Windows 后续开发与验收见 [本次集中对照](WINDOWS-FOLLOWUP-FROM-MAC-20261026.md)。本次提交／推送为用户授权，不创建标签或发布版本。

R36 implements the user's final decision: API total balance and console spending are separate observations with independent freshness. The balance API already includes gifts; console recharge and bonus wallets are summed per currency with Decimal, without double-counting authoritative totals. Devices in separate mode receive API balance only, never unverified web spending; pure-console mode can show its own balance and spending. Synthetic different-account cases cover isolation, restart and expiry. Release, 3,034 isolated checks, lifecycle/HTTP/discovery and native UI checks pass; XCTest remains unavailable locally. The signed R36 app is deployed, both live sources succeeded, and the user accepted both physical displays. Firmware, original six-page cycling, pets and both serial connections are preserved. Evidence: `artifacts/development/mac-deepseek-separated-r36-20261026/`. Commit/push is authorized; no tag or public release is requested.

R36 关闭窗口后的后台复核：API 成功时间由 09:39:07Z 推进到 09:39:45Z，网页消费由 09:39:08Z 推进到 09:41:09Z；两份数据均新鲜，设备消费字段仍缺省。Windows 累计源码使用本地 .NET 8 交叉构建 Release 成功（0 警告、0 错误）；`--status-once` 已尝试但 macOS 缺少 Microsoft.WindowsDesktop.App，不能作为 Windows 运行通过。Windows 原生运行／实机待 Windows 环境完成。

After the window closed, API and web success times independently advanced; device spending remained absent. The accumulated Windows source cross-build succeeds with zero warnings/errors. The requested status-once run was attempted but requires Microsoft.WindowsDesktop.App, unavailable on macOS; Windows native runtime/hardware acceptance remains pending.
