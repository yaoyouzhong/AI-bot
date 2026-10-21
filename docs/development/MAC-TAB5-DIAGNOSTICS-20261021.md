# Mac 桥接诊断与 Codex 直达续修 / Diagnostics and desktop continuation

范围：仅 Mac 桥接、测试和文档；不合并 main、不部署、不发布、不刷机。保留 9415cee 已通过的锁屏、磁盘读数和日常只读 Codex；固件侧相机和列表布局问题暂缓。

## 变更与证据

- 网络诊断使用公开 CFNetwork 系统代理解析和 URLSession 的实际事务指标。每 30 秒对既有 Windows 两个目标执行无登录 HEAD，禁止缓存、Cookie、凭据、正文保存和重定向跟随，不改变系统网络配置。连接超时、HTTP 限制、慢响应分别描述；历史最多十轮，独立时间戳 75 秒失效。失败保留上次成功耗时及时间，不生成新的成功值。
- PAC 未配置明确标记 disabled；配置存在时，比较解析得到的代理／直连类型集合与实际连接指标。不验证具体代理节点，不把无显式代理当作没有 VPN，不把 HTTP 405/404 当成已验证模型服务。
- 初次原生探测未产出样本，定位为代理配置指纹的字典编码顺序不稳定；改用排序 JSON 后实际探测返回 ChatGPT HTTP 405、资源 HTTP 404，最大耗时约 993 ms，PAC 未配置。只保留固定目标标签、状态码、耗时和类型，未输出系统代理地址或 PAC URL。增加字典顺序稳定性回归。
- CPU 等待的 Windows 字段是队列长度。Mac 公开调度／负载接口未找到等价计数；继续省略 processorQueue，并明确显示平台限制，不用负载、线程数或 CPU 百分比估算。
- 9415cee 的直达失败已定位到主输入框标识。静态检查本机桌面编辑器发现其使用无固定 id 的富文本框；未找到既可绑定精确会话又可读写桌面主输入框的公开接口。官方 App Server 提供任务操作，但不构成桌面输入框身份凭据。新增同时判断输入框标识和精确地址的诊断，兼容 AXURL 的 URL／字符串表示，仍禁止通用文本框或标题猜测。此项不是直达验收通过。

自动验证：本机 XCTest 仍在执行前报缺少模块；Release 和完整 Mac CI 分开记录，不以静态检查代替测试。新候选 CI、完整附件核验和正常 App／TAB5 验收待补充。

最终程序 `bdd84cdc884c752c108c75b495d84ec09a89fdc4` 通过 [Mac CI 39538612677](https://github.com/yaoyouzhong/AI-bot/actions/runs/39538612677)：227 项测试、0 失败、0 跳过；完整附件下载后通过源码／内外层 SHA、arm64 可执行文件、App、许可、工具及八张原生界面 PNG 核验。本机 Release、临时签名包、离线工具检查和原生界面渲染也通过。正常退出旧 9415cee 前暂时关闭 Codex 只读以结束子进程，新 bdd84cd 已按精确路径启动，目前等待用户处理系统钥匙串授权。授权后必须恢复此前已获持续授权的日常只读；其余临时操作开关仍关闭。新候选正常 App／实体 TAB5 尚未验收，直达和 CPU 等待不得标为已修复。

实际 PAC 回环夹具进一步发现：CFNetwork 返回主脚本加备用 DIRECT，原单条限制误拒绝正常配置。修复后，直接编译当前生产源文件，对回环 HTTP 临时脚本的 DIRECT、PROXY、PROXY+DIRECT 和无效脚本四项均通过；无效脚本仍拒绝，不以备用直连冒充成功。相同测试加入 XCTest，测试不改系统代理、不开外部代理连接、不写 PAC 配置。SDK 兼容修复通过重载处理旧 SDK 的 Unmanaged 与新 SDK 的托管 CFRunLoopSource。真实网络连续两轮返回有效样本，停止后清除当前样本；首次 CI 的编译失败已明确记录，未启动该失败候选。

### bdd84cd 正常 App 与实体续验

用户完成钥匙串授权后，正常 App 确认 USB 数据已确认、Wi-Fi 收到已认证请求、蓝牙已认证且 USB 优先待命。自动恢复 Codex 日常只读，实际连接 30 个近期会话；发送／桌面输入框／电脑控制／音乐／语音开关保持关闭。两条无认证状态请求仍返回 401。窗口连续显示真实网络耗时约 957–1092 ms，PAC 未配置。用户确认实体 VPN 响应出现数值；CPU 仍无数值，符合尚不支持队列计数的限制。实体 PAC 文字和曲线尚未明确确认，Codex 直达仍未通过。

新候选自动定位初次超时，保留上次天气。系统定位服务中桥接条目实际为关闭；恢复此前已授权的自动定位范围，并保持原和风／自动定位配置不变触发刷新后，窗口确认“和风天气 已更新 · 24 小时 / 7 天”。未变更密钥、城市、设备展示设置或 Wi-Fi 配置。窗口 Wi-Fi 名称框出现用户输入的未提交文字，未点击保存到设备，并已提醒在聊天回复验收结果。

随后用户补充实体 PAC 显示“未开启”，与系统未配置状态一致，实体 PAC 状态验收通过。网络曲线尚未明确反馈，不推定通过。为获取新版直达诊断，临时恢复此前授权的桌面输入框开关，发送开关仍关闭；只允许指定“TAB5 操作验收”点击打开一次，不录音、不输入、不发送。正在等待这次设备点击，结束后关闭临时桌面开关。

用户随后澄清：直达顶部左右箭头会切换 Mac 会话，但 TAB5 标题／内容不更新，因此无法按原步骤直接选中测试会话。只读核对 `.234` 的 `ui_quick_console.h` 和 `desktop_remote.c`：箭头发起 open-recent，只有 status=200、foreground=true 且 taskId 匹配才提交新的选择；失败保留旧选择。当前 App 实际诊断为辅助功能权限未生效；系统条目“在访达中显示”仍指向 9415cee，而非运行中的 bdd84cd。已关闭临时桌面开关，不再要求重复点击。Mac 修复在打开前执行无弹窗权限预检，失败直接返回原有 409，不先导航；补充回归保证零打开／写入／发送、授权后的重复旧请求不重试、新请求才执行。此修复防止已知权限缺失造成的两端位置差异，不把主输入框兼容或实体直达标为通过，固件未修改。

前置权限检查程序 `a6fadc52250fc207a377e8221637981a5d2365ed` 通过 [Mac CI 39540421901](https://github.com/yaoyouzhong/AI-bot/actions/runs/39540421901)，完整附件核验确认 228 项测试、0 失败／跳过、Release、App、许可、内外层 SHA、工具和八张原生 PNG；本机 Release／签名包／离线工具／原生渲染也通过，XCTest 仍缺失。依据用户此前明确授权，仅再次重置桥接 bundle ID 的辅助功能记录；系统设置加入精确 a6fadc5 路径后开关为 on，“在访达中显示”确认 a6fadc5。正常退出 bdd84cd 前暂时关闭只读子进程；a6fadc5 已启动，目前等待用户钥匙串授权。下一步恢复日常只读，使用新候选的前置检查核验实际辅助功能，再决定是否需要一次设备诊断；当前尚无新候选的正常 App／实体直达成功证据。

用户完成 a6fadc5 钥匙串授权后，正常 App 恢复日常只读并确认连接本机 Codex 桌面、30 个近期会话。临时恢复已授权的直达验收开关，发送、电脑控制、音乐和语音仍关闭；实时前置检查显示“等待设备发起直达”，不再报辅助功能未生效。已请求在实体直达顶部仅切换一次左右箭头，不要求先选中测试任务，不输入或发送，等待两端切换结果及桥接诊断；尚不能据此认定输入框兼容或直达成功。

本次实体箭头操作后，用户确认 Mac 切换成功但 TAB5 保留旧选择；桥接精确诊断为“未取得主输入框标识及精确会话地址”。权限前置检查已通过，当前阻碍是桌面输入框与地址的兼容性，不能把系统接受导航或仅标题相同当作成功。已关闭临时直达，保留 30 个会话的日常只读。

用户随后要求采集默认开启、减少反复授权。新增正常启动的默认偏好注册，保留显式关闭和既有手动天气；音乐采集与控制拆分并迁移旧组合许可，其他控制不默认开启。私有草稿／回执密钥的成功与失败均在进程内串行缓存，避免每次操作重新询问同一钥匙串项目。构建支持已有稳定签名身份，本机只读检查为 0 个可用签名身份，仍不能保证换临时签名后免除系统授权。待自动验证，不声称此变更已上机。

采集偏好／钥匙串修复最终候选 `3c9b90d84f3c7aca5146c8c1b5ea3a6951544c51` 通过 [Mac CI 39542234325](https://github.com/yaoyouzhong/AI-bot/actions/runs/39542234325)：234 项测试、0 失败／跳过，下载完整附件并核验 arm64 Release、App、许可、源码／内外层 SHA、工具及八张原生界面图。本机 Release、临时签名包、工具检查及界面渲染通过；本机 XCTest 仍缺失。首次 CI 39542034211 因新增并发测试异常处理写法不兼容而编译失败，已改为在非抛出 worker 内捕获异常；失败候选未运行。旧 a6fadc5 已正常退出，未关闭日常只读；新候选已按精确路径启动，当前等待用户处理系统钥匙串。正常 App 的偏好保持、音乐采集与控制拆分及重复提示改善仍待授权后验收，不能把自动测试当作实机通过。

### 3c9b90d 正常启动与同版本重启复验

用户完成钥匙串授权后，USB 数据已确认、Wi-Fi 已认证、蓝牙 USB 优先待命；Codex 日常只读未重新勾选即连接 30 个会话。音乐沿用先前验收关闭值，因此按用户新的持续采集要求仅开启音乐采集，独立音乐控制仍关闭，窗口正确显示“无活动曲目”。系统定位中的桥接项因换包为关闭，恢复此前已授权的自动定位范围，并保持和风 Host／密钥／城市等原值刷新，成功取得当前、24 小时与 7 天预报。

随后未关闭任何采集开关，正常退出并重启同一个 3c9b90d。进程退出与新进程启动均已核实；本次未观察到 SecurityAgent 密码阻塞，也未要求用户再次操作。正常 App 核实音乐采集开启、音乐控制关闭、Codex 只读开启且 30 会话自动恢复；USB／Wi-Fi／蓝牙、和风当前及预报自动恢复。电脑控制、任务发送、桌面输入框与语音输入仍关闭。此结果只证明同版本设置保持及本次正常重启；未以之承诺更换临时签名免授权，也未声称真实草稿操作或本轮音乐播放重新验收。Codex 直达身份／地址兼容、CPU 等价队列计数仍未解决。

## English

Only the Mac bridge, tests and docs change. No main merge, deployment, publication or flashing. Firmware camera/layout issues remain deferred.

App-owned probes use public CFNetwork resolution and URLSession transaction metrics, with bounded unauthenticated HEAD requests, no stored body/cookies/credentials or redirects, and no proxy changes. Network timestamps expire independently after 75 seconds; ten actual rounds are retained, and failures preserve separately dated last-success evidence. Stable sorted-JSON configuration fingerprints repaired a locally observed discarded-sample issue. Native probing then returned HTTP 405/404 at approximately 993 ms, with PAC unconfigured. Responses establish HTTP reachability, not successful model service. Proxy/direct type matching does not identify nodes or VPN tunnels.

The Windows CPU queue counter has no established public Mac equivalent and stays absent. Desktop navigation remains unaccepted: the installed editor lacks the expected fixed identity. Diagnostics now distinguish absent identity from absent exact thread address, accept URL/string address representations, and still reject generic text areas or window-title matching. App Server task APIs do not prove desktop composer identity. Local XCTest remains unavailable; CI and physical results must be recorded separately.

Actual loopback PAC fixtures exposed normal CFNetwork output containing a primary script plus fallback DIRECT. The repair resolves the primary script, rejecting errors rather than accepting fallback as proof. Current production source compiled independently and passed DIRECT, PROXY, combined and malformed-script cases; the same regression is included in XCTest without changing system proxy settings. SDK ownership overloads handle both managed and Unmanaged run-loop sources. Two real network rounds produced samples and stop cleared the current sample. The first CI compile failure was recorded; that failed candidate was not launched.

Final program bdd84cd passed linked CI with 227 tests, zero failures/skips, and complete downloaded-artifact verification. Local Release, signed bundle, offline tools and native fixture rendering passed. The old app was normally quit after temporarily stopping read-only Codex; the exact new app path is running, awaiting user Keychain authorization. Restore the previously authorized daily read-only feature afterward; other temporary controls stay off. Normal-app and physical acceptance are pending, and neither desktop direct control nor CPU queue availability is claimed fixed.

After user Keychain approval, normal-app checks confirmed acknowledged USB, authenticated Wi-Fi, USB-priority BLE standby and restored daily read-only Codex with 30 chats; other temporary controls remain off. Both unauthenticated status routes return 401. Live network latency repeatedly updated around 957–1092 ms with PAC unconfigured. The user physically confirmed VPN latency values; CPU remains unavailable as documented. Device PAC wording/graph and desktop direct control are not accepted yet. Location was disabled for the candidate in System Settings; restoring the previously authorized scope and refreshing unchanged QWeather settings recovered current/24-hour/7-day weather. No credentials, device settings or Wi-Fi configuration were changed; unsent user text in the Wi-Fi name field was not provisioned.

The subsequent device report confirms PAC displays disabled, matching the host and passing that state check; no graph result was supplied. The previously authorized temporary desktop-composer switch was restored for one open-only diagnostic on the designated acceptance chat, with sending still disabled. Awaiting that device click; close the temporary desktop switch after the diagnostic.

The user clarified that top arrows navigate the Mac but leave the device title/content unchanged. Read-only consumer inspection confirms selection commits only after a matching successful foreground receipt. The live diagnostic reports missing Accessibility permission; System Settings' Reveal in Finder still targets 9415cee, not running bdd84cd. Temporary desktop control was closed again. The bridge repair checks permission before navigation, preserving the existing 409 response, with regression coverage for zero side effects, no replay after permission recovery, and execution only for a fresh request. This prevents known permission failures from first moving the Mac; composer compatibility and physical direct navigation remain unaccepted. Firmware is unchanged.

Program a6fadc5 passed the linked CI and full artifact verification with 228 tests and zero failures/skips; local Release, signed bundle, offline tools and native rendering also passed. Under the prior explicit scoped-reset authorization, only the bridge Accessibility record was reset. System Settings added the exact new path, showed it enabled, and Reveal in Finder confirmed a6fadc5. The old app was normally quit after temporarily stopping read-only Codex; the new app awaits user Keychain authorization. Restore daily read-only and verify actual Accessibility via the new preflight before another device diagnostic. Normal-app/physical direct-navigation success is not yet established.

After user Keychain approval for a6fadc5, the normal app restored daily read-only access and connected to the local Codex desktop with 30 recent chats. The previously authorized temporary direct-control test switch was restored; sending, computer control, music and voice remain off. Live preflight now reports waiting for a device request rather than missing Accessibility. One arrow-only device navigation was requested, without requiring prior test-chat selection or entering/sending text. Device behavior and the resulting diagnostic remain pending; composer compatibility and successful direct navigation are not inferred.

The physical arrow request navigated the Mac but retained the device selection. The exact bridge diagnostic was missing both composer identity and the exact chat address, after Accessibility preflight passed. Temporary desktop control was disabled; daily read-only remains enabled with 30 chats. Navigation acceptance or matching titles are not treated as proof.

The user then requested default collection and fewer authorization prompts. Normal startup now registers collection defaults while preserving explicit opt-outs and manual weather configuration. Playback collection and control are separate, retaining an existing combined grant on upgrade. Other actions do not default on. The private draft/receipt key now serializes and caches both success and denial for the process lifetime. The bundle script supports an existing stable signing identity; this host reports zero valid signing identities, so changed ad-hoc builds may still need OS approval. Automated validation is pending; no physical acceptance of these changes is claimed.

Final collection/key-cache candidate 3c9b90d passed [CI 39542234325](https://github.com/yaoyouzhong/AI-bot/actions/runs/39542234325) with 234 tests and zero failures/skips. The complete artifact was downloaded and verified for source/inner/outer hashes, arm64 Release/app, licenses, tools and eight native images. Local Release, ad-hoc bundle, tool checks and UI rendering passed; local XCTest remains unavailable. Initial CI 39542034211 failed to compile a throwing XCTest expression inside a nonthrowing concurrent worker; explicit worker-local error handling repaired it, and that failed candidate was never run. The old app quit normally without turning off daily read-only. The exact new candidate is running, awaiting user Keychain handling. Normal-app preference retention, separate music collection/control and reduced repeated prompts remain pending; automated results are not physical acceptance.

### 3c9b90d normal app and same-build restart

After user Keychain handling, USB was acknowledged, Wi-Fi authenticated and BLE in USB-priority standby. Daily read-only Codex reconnected to 30 chats without re-enabling its switch. Music retained the previous test opt-out, so collection alone was enabled under the new daily-collection instruction, leaving separate playback control off; no active track was reported. The candidate’s Location switch was off after the build change. Restoring the previously authorized scope and refreshing unchanged QWeather configuration recovered current, hourly and daily forecasts.

The same candidate then quit normally with all collection preferences preserved and restarted. Process exit/start was checked; no SecurityAgent password blockage was observed and no further user action was requested. The normal UI confirmed music collection on/control off, read-only Codex on with 30 chats, restored USB/Wi-Fi/BLE and automatically refreshed current/24-hour/7-day QWeather. Computer control, task mutations, desktop composer and voice input remain off. This verifies the observed same-build restart and preference persistence, not exemption from changed ad-hoc-build approval, a new live draft acceptance or a new playback acceptance. Desktop identity/address compatibility and the equivalent CPU queue metric remain unresolved.


## 锁屏显示与直达导航分离修复（待真机）

用户要求优先处理锁屏状态显示和 Codex 直达左右切换不同步；后续解锁需支持字母、数字和符号密码，本轮不采集密码、不启用解锁。

Mac 普通电脑状态改为使用本进程观察到的系统锁屏／解锁通知显示 `locked`，不再始终写死未知。启动未观察到事件、用户会话切换和睡眠／唤醒后仍为未知；不把发出锁屏快捷键当成已锁定。使用公开 Foundation／AppKit 框架订阅兼容性通知；通知名不是正式公开的锁定状态 API，也不是可认证的凭据输入依据。单独的 `lock-screen` 准备查询仍返回未知，`oneClickUnlock` 仍为 false，避免显示观测误授权现有设备 HID。系统事件顺序和实体显示尚待实际锁屏／解锁验证。

Codex `open-recent` 的导航确认不再必须读取主输入框。仅在 Codex 为前台且前台窗口文档携带精确会话地址，或其可信 App 资源 WebArea 内有 `AXARIACurrent=page` 的精确会话链接时返回成功；匹配标题、未选中的历史链接、外部嵌入网页和系统接受 deep link 均不算成功。不读取输入框时不回传 `draftEmpty=true`。填写、清空和发送保持原有主输入框与精确任务检查。已读取 `.234` 对端确认缺少 `draftEmpty` 不会阻止已确认导航更新标题／项目／索引，也不会宣称输入框为空；不修改固件。

新增测试覆盖导航不读取／不授权编辑器、导航失败不能借用可读编辑器确认、精确当前链接与可信来源、锁屏显示状态失效和不能开放凭据准备。本机 XCTest 仍缺少模块；Release 已编译通过，首次并行编辑导致的构建输入变更错误已重跑排除。完整 CI 与新候选实体结果待补充。

### English

The current priority is lock-state display and synchronized Codex previous/next navigation. Future unlock must accept letters, digits and symbols; no credential is collected and unlock stays disabled.

Ordinary computer status now displays observed lock/unlock events instead of always returning unknown. Startup without observations, session changes and sleep/wake invalidate the observation. These compatibility notification names are consumed through public Foundation/AppKit APIs but are not a documented lock-state API or an authenticated credential-entry gate. The separate device `lock-screen` preparation query stays unknown and `oneClickUnlock` stays false. Actual OS event ordering and hardware display remain unverified.

Navigation confirmation is separated from composer access. Success requires a foreground Codex window with an exact thread document URL or a current-page link carrying that exact thread ID inside the trusted app resource WebArea. Titles, unselected links, embedded external pages and accepted deep links alone do not qualify. No `draftEmpty=true` is asserted without reading a composer; editing/submission retain their prior independent identity checks. The archived `.234` consumer was checked for compatibility; firmware is unchanged. New tests cover these boundaries and display-only lock observations. Local XCTest is unavailable; Release compilation passed after rerunning an initial build whose source changed during compilation. Full CI and live acceptance are pending.


继续只读检查本机已安装 Codex 的入口定义，确认打包主 renderer 使用 `app://-`，文件浏览来源另为 `app://fs`。原 Mac 精确地址检查没有允许前者，构成可复现的解析兼容缺口。新增仅该主来源的校验，支持前台 WebArea 的精确实时路径和当前链接；`app://fs`、其他 host、带用户信息／端口的伪装地址、不同任务以及仅 query 中的 `initialRoute` 启动提示均不用于确认。实现独立编写，未复制供应商代码；应用文件只读取，不修改。

Further read-only inspection of the installed desktop entry definition established that its packaged main renderer uses `app://-`, while file browsing uses the separate `app://fs` origin. The former was missing from the bridge's exact-route parser. The compatibility fix accepts only that main origin and exact live route/current link, rejecting the file origin, other hosts, credentials/ports, different tasks and startup-only `initialRoute` hints. Implementation is independent; no vendor source was copied or installed app modified.


### 1e68fda 自动验证与真实锁屏观测

候选程序 `1e68fdadf9ec6311e0537ee0a0882ce1dc7ab1f1` 已推送测试分支，通过 [Mac CI 39548667116](https://github.com/yaoyouzhong/AI-bot/actions/runs/39548667116)：240 项测试、0 失败、0 跳过。完整附件下载后经 `verify_mac_tab5_ci_artifacts.py` 核验源码 SHA、arm64 Release、App、许可、工具及原生界面附件。本机 Release、临时签名包、离线工具检查及原生界面渲染通过。本机 XCTest 仍因缺少模块未执行，不计为测试通过。

新 App 准备于 `artifacts/mac-tab5-20261021/1e68fda/AIBotBridge.app`，尚未替换运行中的 3c9b90d。按一次手动锁屏／解锁请求启动的只读探针实际捕获 `com.apple.screenIsLocked`；同时 CoreGraphics 当前会话与 IORegistry 当前用户记录均出现明确 `CGSSessionScreenIsLocked=true`，当前用户仍在 console，前台类别为 loginwindow。未读取窗口内容、截图或密码。后续只读确认仍为锁定；尚无解锁反馈和候选实体显示证据。生产观察类的独立临时只读进程在锁屏期间启动，其初始值按设计保持 unknown，等待实际事件；启动在锁屏之后不能冒称已收到先前通知。

当前停点是用户手动解锁。解锁后再正常退出旧候选、启动精确 1e68fda；必要系统密码授权仍由本人处理。之后临时恢复此前已授权的电脑控制与直达测试开关，先验证设备普通锁定显示，再验证一次直达左右切换；不要把 CI、系统探针或 deep link 接受当作实体通过。日常只读、音乐采集、和风自动定位和展示设置继续保留。

English: candidate `1e68fdadf9ec6311e0537ee0a0882ce1dc7ab1f1` passed CI 39548667116 with 240 tests and zero failures/skips. Full downloaded artifacts passed source/hash, arm64 executable/app, license, tool and native-render verification. Local Release, ad-hoc packaging, offline tool and UI fixture checks passed; local XCTest remains unavailable. The new app is prepared but the running 3c9b90d has not been replaced. A user-triggered lock produced the actual screen-lock notification and explicit true session/registry lock observations without reading window contents or credentials. The machine remains locked; no unlock or candidate hardware acceptance is recorded. A separate production-observer probe started after locking correctly begins unknown until an event. Resume normal app replacement and the already authorized narrow hardware checks after the user unlocks. No deployment, release, main merge or firmware change/flash occurred.


### 1e68fda 正常 App 续验

用户回复已解锁后，使用生产 `MacScreenLockObservation` 类编译的临时观察进程实际记录 unlocked → locked → unlocked，说明本机事件顺序下状态可往返更新。该探针已停止；仍不等于 TAB5 显示已通过。

已正常退出运行中的 3c9b90d，核对旧进程退出后通过精确 App 路径启动 1e68fda。用户完成系统钥匙串授权，正常 App 确认 USB 数据已确认、Wi-Fi 已认证、蓝牙 USB 优先待命、音乐采集开启及 Codex 日常只读 30 会话。临时恢复此前已授权的电脑控制、直达开关；任务发送和语音保持关闭。定位条目因换包为关闭，按已有持续自动定位授权恢复。辅助功能原条目仍失效，本次通过系统设置直接移除并重新添加精确 1e68fda 路径，开关为 on；当前进程前置检查已变为“等待设备发起直达”，无需 tccutil 或新系统密码操作。已请求实体直达顶部箭头只切换一次，等待标题／项目／序号及诊断；尚无实体直达通过结论。

English: after the user unlocked, a temporary process compiled with the production observation class recorded unlocked → locked → unlocked and was stopped. The old app exited normally; exact candidate 1e68fda launched, the user completed Keychain authorization, and USB/Wi-Fi/BLE, music collection and 30-thread daily read-only Codex recovered. Previously authorized temporary computer/desktop controls were restored with task sending and voice still off. Existing automatic-location access was restored. System Settings removed/re-added the exact candidate to Accessibility; the running process now passes permission preflight without another reset command. One hardware arrow-switch check is pending; no device-display/navigation acceptance is claimed yet.


### 固定本机签名与直达页面路由续查

1e68fda 实体箭头仍失败：用户报告先有密码授权，然后“未确认 Codex 前台”；正常 App 诊断为“未取得匹配目标任务的公开页面地址”。权限前置检查通过，不把本次当成直达通过。启动四次提示及首次直达额外提示分别涉及独立钥匙串项目与延迟读取的草稿／回执密钥；现有进程缓存不能解决换临时签名后的身份变化。

用户明确授权创建并固定使用 `AI-bot Local Development` 本机代码签名身份。通过钥匙串访问证书助理创建 RSA 2048、SHA-256、仅代码签名用途、365 天自签名证书，私钥留在登录钥匙串；未导出私钥、未添加网络或全局信任。用户完成 codesign 的首次系统授权后，两个内容不同的临时二进制均签名校验通过且 designated requirement 完全相同，绑定证书指纹和桥接 bundle ID。构建脚本自动读取仓库外的本机指纹选择；环境变量仍可显式覆盖，无配置的 CI 仍使用临时签名，固定身份失败不会回退。普通 App 首次身份迁移和跨构建权限保持尚待实测。

只读检查已安装 Codex 的路由定义发现本机会话实际使用 `/local/:conversationId`；外部 deep link 才使用 `codex://threads/:id`。桥接新增可信打包来源内精确 local 路由匹配，拒绝其他来源、remote、错误任务、额外路径与 initialRoute 启动参数；不放宽主输入框确认。导航失败新增无主区域、无可验证地址、遍历到界等不含正文／地址的诊断，避免再次盲试。固件未修改。自动测试与新候选结果待续。

English: the 1e68fda hardware arrow still failed after a Keychain prompt; the app reported no matching exact page address despite permission preflight passing. Separate Keychain items plus the lazily accessed private draft/receipt key explain additional prompts; process caching does not stabilize ad-hoc build identity. With explicit user approval, Certificate Assistant created a local 2048-bit RSA/SHA-256 code-signing-only identity, valid for 365 days. Its private key remains in the login Keychain, with no key export or global/network trust changes. After user approval for codesign, two different temporary binaries verified successfully with identical certificate-bound designated requirements. Build selection now persists outside the repository, retains an explicit environment override, and never silently falls back from a configured signer. First migration and cross-build OS grants remain unverified. Static installed-app inspection also established `/local/:conversationId` as the internal local route; exact trusted-origin parsing and bounded non-content failure diagnostics were added without loosening composer checks or modifying firmware. Further automatic and physical validation is pending.


### 5f3c819 固定签名候选验证

程序 `5f3c819389b4e25cbb9fd2b58ecaf79a5dbd768f` 通过 [Mac CI 39550403341](https://github.com/yaoyouzhong/AI-bot/actions/runs/39550403341)：241 项测试、0 失败／跳过。完整附件下载并核验源码、内外层 SHA、arm64 Release、App、许可、工具和原生界面。当前本机仍缺少 XCTest，本机测试尝试如实失败；本机 Release、固定证书签名包、工具和原生界面检查通过。两个内容不同的临时二进制与真实新 App 使用实际 bundle ID `io.github.yaoyouzhong.aibot.bridge` 签署后，三者的证书绑定 designated requirement 完全相同且签名有效。

候选位于 `artifacts/mac-tab5-20261021/5f3c819/AIBotBridge.app`，尚未启动；当前 1e68fda 保持运行，已请求用户在其上验证普通锁屏／解锁状态显示，等待反馈，避免在测试中途退出。新签名首次迁移、跨构建权限保持和 local 路由实体直达均未宣称通过。不合并 main、不部署、不发布、不修改或刷写固件。

English: program 5f3c819 passed CI 39550403341 with 241 tests and zero failures/skips, followed by complete downloaded-artifact verification. The local XCTest attempt still fails because the module is unavailable; local Release, fixed-certificate bundle, tool and native-UI checks passed. Two different fixture binaries and the actual app, all signed with the real bridge bundle ID, have identical certificate-bound designated requirements and valid signatures. The new candidate is prepared but not launched. Running 1e68fda remains undisturbed while awaiting a requested physical lock/unlock-display check. First signer migration, permission retention across builds, and physical direct-navigation acceptance remain pending. No main merge, deployment, release or firmware change/flash occurred.


用户在正常运行的 1e68fda 上完成实体锁屏／解锁复验，明确反馈两个状态“都 OK”：TAB5 发起锁屏后显示已锁定，Mac 本机解锁后恢复未锁定。普通锁定状态显示实机验收通过；不表示一键解锁已支持。已关闭本轮临时电脑控制开关，保留日常采集和只读；正常退出旧候选，准备启动固定签名 5f3c819 继续直达验收。

English: the user physically confirmed both lock-state transitions in normally running 1e68fda: locked after the TAB5 action, then unlocked after local Mac authentication. Ordinary lock-state display passes hardware acceptance; one-click unlock remains unsupported. Temporary computer control was disabled, daily collection/read-only preserved, and the old candidate quit normally before the fixed-signature candidate transition.

固定签名 5f3c819 已按精确路径启动，首次签名迁移仍由用户完成钥匙串授权。正常 App 自动恢复 USB 数据确认、蓝牙 USB 优先待命、30 个 Codex 会话及音乐采集；电脑控制／发送／语音关闭，桌面直达临时开启。系统设置移除旧临时签名条目并加入精确新路径后，当前 App 辅助功能预检变为“等待设备发起直达”；本次无需 tccutil 或额外系统密码操作。已请求一次实体箭头，尚待结果。

English: exact fixed-signature candidate 5f3c819 launched; the user handled the initial signer-transition Keychain prompts. USB acknowledgment, BLE standby, 30 read-only chats and music collection recovered. Computer control, sending and voice remain off; temporary desktop navigation remains on. Replacing the old Accessibility entry with the exact new path made the running app pass preflight, without tccutil or another system-password action. A single device arrow check is pending.

5f3c819 实体直达仍未通过：首次使用固定签名的独立回执密钥时用户再次处理密码提示，随后仍未确认前台；正常 App 诊断显示主区域和链接存在，但未匹配目标任务。静态核对当前桌面的交互会话行发现 `aria-current=page` 在外层 group，导航链接是其子节点，原检查遗漏该结构。新增有边界的当前行检查：最多 64 个节点、6 层、共用导航截止时间，不读取文字，拒绝嵌入页面／输入框／弹窗／嵌套当前节点；必须完成遍历且所有链接均指向同一精确任务才确认。新增回归覆盖未选中、空链接、遍历不完整、其他会话、外部地址与启动提示，等待新 CI 和实体结果。

English: 5f3c819 direct navigation still failed after the first fixed-signer access to the separate receipt key. The normal app found the main area and links but no matching current target. Static inspection showed that current-page state belongs to an outer interactive row group rather than its child link. A bounded current-row check now requires complete traversal and every link to identify the same exact task, rejecting embedded pages, editors, dialogs and nested current nodes without reading text. It shares the navigation deadline and caps traversal at 64 nodes/six levels. Regression coverage includes unselected, empty, incomplete, different-task, external and startup-hint cases; CI and hardware results remain pending.


### 81c913f 验证与钥匙串重复授权根因

程序 `81c913f8e80dea750ea3a571a8f6e3c66708a446` 通过 [Mac CI 39551451497](https://github.com/yaoyouzhong/AI-bot/actions/runs/39551451497)：242 项测试、0 失败／跳过，完整附件核验通过。本机 Release、固定签名 App、随包工具与原生界面检查通过；XCTest 本机仍缺失。新旧实际 App 二进制不同但 designated requirement 相同。正常退出 5f3c819 后启动 81c913f，用户报告仍需四次钥匙串授权；跨版本免钥匙串授权明确未通过。

仅对桥接服务执行无认证 UI 的访问控制元数据检查（不请求 `kSecReturnData`、不读取秘密、不写 ACL），确认五个相关项目的 partition 列表仅有 `cdhash`。四个启动项的普通允许列表已包括 5f3c819，且授权后 partition 同时包含两个固定签名构建的不同 CDHash；私有回执项尚无这两个构建的持久 partition 记录。Apple 当前公开 [securityd 实现](https://github.com/apple-oss-distributions/Security/blob/main/securityd/src/clientid.cpp) 同样说明，符合 Apple 开发／分发证书链的代码使用 team ID，其余已签名代码使用 CDHash。固定自签名无法单独消除此层换构建询问，先前预期过于乐观，已向用户更正。未移除 partition、未授权所有 App、未读取或导出秘密。

固定签名仍有实际收益：81c913f 无需重新登记辅助功能即通过前置检查，自动定位沿用并自动刷新和风当前／24 小时／7 天天气；USB、Wi-Fi、蓝牙与 30 个只读会话正常。电脑控制、发送、语音关闭，临时直达开启。当前已请求一次实体箭头核验外层当前行修复，结果待补充。

English: 81c913f passed CI 39551451497 with 242 tests and zero failures/skips, full artifact verification, local Release/fixed-signature packaging, tool checks and native UI rendering. Local XCTest remains unavailable. After normal replacement, the user still reported four Keychain prompts: cross-build password-free access failed. Read-only, no-auth-UI metadata inspection of this bridge service alone confirmed that its five relevant items use only CDHash partitions. Four startup items already allowed the previous signed candidate and now contain both builds' differing hashes; the private receipt item lacks persistent partitions for either. Apple's current public securityd source corroborates per-build hashes for signed code outside the recognized Apple development/distribution chains. The earlier expectation was corrected; no partition/ACL was weakened and no secret data was requested or exported. Fixed signing did retain Accessibility and Location across builds, automatically recovering QWeather and transports plus 30 read-only chats. Temporary direct navigation awaits a single hardware check; other temporary actions remain off.

### 81c913f 直达未通过与额外密钥访问移除

用户确认实体直达仍是“老样子”，并补充又输入一次授权密码；当前 App 诊断仍未取得匹配目标任务的公开地址。继续静态追查已安装桌面的实际会话行调用方，发现该调用明确关闭 navigation link、使用 button；上一轮只读通用组件后推断 group 是当前实际会话行，证据不足。Chromium 当前公开实现确认属性名仍为 `AXARIACurrent`，无需猜测改名。新增只报告“发现当前按钮、无可验证目标地址”的诊断，不凭标题或深链已打开就宣称目标确认，不修改桌面或固件。

额外密码来源已在桥接调用链确认：`open-recent` 原先与草稿／发送共用加密持久回执，打开会话也会访问私有密钥。现在纯导航回执只保存在有上限的进程内存中，保留同请求去重、设备／任务绑定与冲突拒绝；重启后允许再次打开同一页面。编辑和发送仍须先加密持久保存回执，拒绝密钥访问时不会修改输入框或发送。旧加密文件不迁移、不删除、不覆盖，密钥存储和权限不变。新增测试用主动拒绝密钥的存储验证正常导航零密钥访问、去重、冲突、重启及写操作拒绝，并验证旧文件保留。启动四次授权仍未解决；本改动尚待 CI，不冒称实机免授权通过。

English: the user confirmed unchanged direct-navigation failure plus another password prompt. Static inspection of the actual installed sidebar caller, rather than its generic component alone, shows button-only rows with navigation links disabled. Chromium's public implementation confirms the existing `AXARIACurrent` spelling. A bounded diagnostic distinguishes a selected button without an exact verifiable address; title matching and accepted deep links remain insufficient. Pure navigation previously accessed encrypted persistent draft/delivery receipts unnecessarily. It now keeps bounded process-local deduplication receipts with device/task binding and conflict checks; reopening after restart is safe. Editing/submission still persist encrypted receipts before actions and fail closed when the key is unavailable. Existing encrypted files and Keychain permissions are unchanged. New regression coverage denies the key and checks zero-access navigation, duplicates, conflicts, restart, mutation rejection and preservation. CI and physical verification are pending; four startup prompts remain unresolved. No firmware or installed desktop app was changed.

程序 `1ab5d2e33fedbd1bfab8318d8b914c940d95a04a` 已推送测试分支，通过 [Mac CI 39553409389](https://github.com/yaoyouzhong/AI-bot/actions/runs/39553409389)：243 项测试、0 失败／跳过，完整附件源码 SHA、包校验、arm64 Release、App 和界面附件核验通过。本机 XCTest 仍不可用；源码冻结后重新运行的 Release／固定签名打包、离线工具和界面检查均通过。候选准备于 `artifacts/mac-tab5-20261021/1ab5d2e/AIBotBridge.app`，尚未启动，避免为未解决的直达确认再触发四次换包授权。运行中的 81c913f 保留日常只读，临时直达已在正常 App 界面关闭并读回确认；其他临时操作关闭。纯导航零密钥访问是自动测试结论，尚非实机免弹窗通过。直达确认和跨构建四次启动授权仍待解决。

English: program 1ab5d2e passed CI 39553409389 with 243 tests and zero failures/skips; complete downloaded artifacts passed SHA, package, arm64 Release, app and UI verification. Local XCTest remains unavailable; after freezing sources, local Release/fixed-signature packaging and offline tool/UI checks passed. The candidate is prepared but unlaunched to avoid another four startup prompts while exact navigation confirmation remains unresolved. Running 81c913f retains daily read-only access; its temporary desktop control was disabled and verified through the normal app UI. Zero-key navigation is automatically tested, not yet a physical no-prompt acceptance. Exact direct-navigation confirmation and repeated cross-build startup authorization remain open.
