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

### 启动授权稳定身份与 Windows 直达对照修复

用户要求继续修复，并明确参考 Windows。已阅读 `Tab5QuickConsole.OpenAsync`、`Tab5CodexComposer.UniqueTitle/WindowMatches` 和对应回归：Windows 使用完整桌面目录中的原始唯一标题，核对前台主文档，导航与输入框动作分开。Mac 原实现额外要求 UUID 页面地址，当前桌面不提供该地址时，Mac 已切换但桥接仍返回 409，设备按协议保留旧选中项。现在增加同类完整标题确认：SQLite 仅只读打开；对目标本机 ID 绑定查询，按完整目录检查重复标题（保守地包含其他 host 的同名项），不截断、不只看设备近期／项目列表。只接受可信打包来源的前台主 WebArea 精确标题，重读唯一性、主页面标题和窗口／前台身份。普通窗口标题、外部／内嵌页面、重复／空／通用／含 NUL／过长标题、缺表／忙／读取失败均不确认。输入框检查不放宽。曾准备的剪贴板核对方案已从最终改动移除；无剪贴板访问。只读核对指定验收标题在真实目录中恰有一项，未打印其内容或其他会话。

启动四次询问的根因仍是变化的主程序 CDHash。新增包内、按需退出的 `AIBotKeychainHelper`，只处理本桥接既有项目；双方校验同一叶证书，工具另外核对真实父进程签名、精确主程序路径以及继承的匿名管道。无 socket、launchd、网络、命令行凭据或日志，凭据仍只持久存于原钥匙串项目。工具的签名字节按其源码／构建脚本／包配置／工具链／架构／证书保留在私有构建缓存，主程序更新不重签工具；损坏／异签工具拒绝，不静默改用主程序读钥匙串。无证书的原开发／临时签名路径保留既有直接访问行为。首次迁移仍可能由系统逐项询问，尚未宣称旧项目已免授权。

本机正常签名包预检中，在匿名管道下使用真实 App 调用无凭据 probe 通过；改变 App 的 Info.plist 并重签后主 CDHash 确实不同，工具字节／CDHash 完全相同，probe 仍通过；其他父程序直接调用及替换异签工具均拒绝。测试发现并修复 `/var` 与 `/private/var` 路径别名导致合法父程序拒绝的问题。该预检不读取真实凭据，不代替普通启动的钥匙串迁移验收。当前本机 Release 编译通过；XCTest 仍缺失，新增完整标题和密钥工具边界测试等待 Mac CI。正常运行候选仍为 81c913f，临时操作关闭，日常只读保持；新修复尚未上机，不合并 main、不部署、不发布、不修改或刷写固件。

English: Windows comparison established that open-only navigation confirms a globally unique full catalog title against the foreground primary document, separately from composer actions. Mac's extra UUID-route requirement caused 409 responses after the Mac had already switched, leaving TAB5's prior selection unchanged. The independent Mac implementation now performs a bounded read-only SQLite query for the exact local task, rejects duplicate titles even on other hosts, and confirms only the trusted primary WebArea's exact full title with repeated catalog/window/frontmost checks. Device-truncated/project/recent subsets, generic chrome, embedded/external pages, invalid/duplicate titles and database errors cannot confirm navigation. Composer checks remain independent. The provisional clipboard implementation was removed entirely; no clipboard access is used.

The bundled on-demand Keychain helper verifies its real parent path, certificate-bound app identity and anonymous pipes; the app verifies the helper. It serves only existing bridge accounts, exits after each request and uses no service, socket, network, credential arguments or logs. Signed helper bytes are privately cached by source/build inputs, preserving its CDHash while the main app changes. Tampered/differently signed helpers fail closed; certificate-free development retains prior direct behavior. Credentials stay in the original Keychain items. A real signed app's no-credential probe passed before and after changing/re-signing the main CDHash, with identical helper bytes/hash. Wrong-parent and wrong-helper cases were rejected. The fixture caught and fixed canonical-path aliases. This is identity/protocol evidence, not physical acceptance of legacy-item grants. Local Release passed; local XCTest remains unavailable and new regressions await CI. Running 81c913f remains with temporary controls off and daily read-only on; no main merge, service deployment, release or firmware change/flash occurred.

### e2ca73b 自动与上机结果、同名回退及排序续修

程序 `e2ca73bc87bf97c641ea767d774c4ce6db01e279` 通过 [Mac CI 39556680202](https://github.com/yaoyouzhong/AI-bot/actions/runs/39556680202)：250 项测试、0 失败／跳过，完整下载附件的源码、SHA、arm64 Release、App、许可、工具和界面核验通过。本机固定签名包、身份边界 probe、随包工具和界面检查均通过。正常退出旧 App 后启动精确 e2ca73b，用户处理首次钥匙串迁移；USB／Wi-Fi／BLE、和风预报、30 个日常只读会话及辅助功能恢复。随后无认证 UI 的只读 ACL 元数据检查确认四个启动项目都已持久登记新工具的路径与 CDHash；私有草稿项目未登记且纯导航未读取它。用户明确反馈本次左右切换没有再要求密码，但 TAB5 标题／项目／序号仍未变，Mac 已切换，设备仍未确认前台。桥接记录遍历到界，本轮导航未通过。

继续只读检查实际会话行调用方，确认原始完整标题放在 `aria-label`，外层是当前页面 button；增加可信会话行完整唯一名称检查，不靠普通静态文字。树搜索改为广度优先，跳过文字／图片／输入框正文，避免深处长回复先耗尽预算；整体导航重试有三秒边界。诊断只显示节点数、区域数、唯一性和匹配布尔／耗时，不显示标题、ID、页面地址或正文。

用户补充目标名为 OpenClaw 前缀。仅查询计数和标题长度确认本机该前缀有八项、最大 88 UTF-8 字节，完整标题分组数量为 1／3／4，没有输出其内容。Windows 实现对 `UniqueTitle=null` 的纯打开仅确认应用前台，不要求页面标题；此前对照未覆盖该分支。按用户明确参考 Windows 的要求，Mac 增加仅对数据库明确判定重复标题的打开回执：已请求所选精确 ID 的深链、Codex 实际前台且无阻挡 sheet，返回现有 foreground 回执并在 Mac 明示“同名会话未作精确确认”。此结果不进入输入框读取／填入／清空／发送确认，不设置 draftEmpty，也不将目录损坏当同名回退。无新协议字段、固件或 Windows 改动。曾研究的额外桌面 IPC 无已建立的当前页接口；没有调用猜测方法，也不使用剪贴板。

用户进一步要求按时间从近到远排序。Mac 在合并全部目录页后明确按更新秒数降序，时间相同保留来源顺序，未知时间排后；同名或显示截断后重名的标题加简短日期前缀。只有显示文字变化，所有操作仍绑定原 ID，原始完整标题确认仍从独立目录读取。新增回归覆盖排序、同名显示、未知／布尔时间、同名打开不能借用输入框权限，以及侧栏不被长回复饿死。新自动验证和跨真实主程序改动的免密码启动尚待续验。

English: e2ca73b passed CI 39556680202 with 250 tests and zero failures/skips, full artifact verification and local signed/helper/tool/UI checks. The user completed the first helper migration; transports, QWeather, 30 read-only chats and Accessibility recovered. Read-only, no-auth-UI ACL metadata confirmed persistent helper identity for all four startup items, while the private draft key remained untouched. The device arrow no longer prompted for a password, but navigation still failed with a traversal-limit diagnostic. The selected thread row's raw full aria-label is now checked with its current-page/thread-class state, and bounded breadth-first traversal avoids long-reply starvation. Diagnostics contain only counts, booleans and timings.

Metadata counts for the user-specified OpenClaw prefix revealed eight local entries with full-title group sizes 1/3/4 (maximum 88 UTF-8 bytes), without printing content. Windows permits an app-foreground open-only result when full titles are ambiguous. Mac now follows that boundary only for explicitly duplicate titles after dispatching the exact selected deeplink, with a foreground Codex window and no sheet. The UI explicitly distinguishes this from exact page confirmation; it never authorizes composer actions or asserts draft emptiness. Missing/corrupt catalog data cannot use the fallback. No guessed desktop IPC, clipboard, wire-field, firmware or Windows change was used. Per the user's additional direction, merged task pages are explicitly sorted newest first, stable on ties, with unknown times last and dates on duplicate display names. IDs remain unchanged. New regressions and cross-main-build password-free startup acceptance are pending.

### 09a15ca 验证与跨主程序更新启动

程序 `09a15cacc13536f3a61e2dfb1219d4a639b738ca` 通过 [Mac CI 39559369652](https://github.com/yaoyouzhong/AI-bot/actions/runs/39559369652)：254 项测试、0 失败／跳过；完整下载附件的源码 SHA、内外层哈希、arm64 Release、App、许可、随包工具和原生界面核验通过。本机 Release、固定签名打包、钥匙串工具身份边界 probe、离线工具与原生界面检查通过；本机 `swift test` 仍因 XCTest 缺失而不能执行，未把编译作为完整测试证据。

正常退出 e2ca73b 后启动精确 09a15ca 路径，主程序字节／CDHash 已变化，包内工具字节和 CDHash 保持相同。这次启动没有新的密码授权交接，也没有观察到 SecurityAgent 或阻塞的工具进程；正常 App 自动恢复 USB 数据确认、Wi-Fi 已认证、蓝牙 USB 优先待命、和风当前及预报、30 个日常只读会话和音乐采集。发送、电脑控制和语音关闭；此前授权的桌面直达临时开启，待本次检查后关闭。此结果完成一轮真实跨主程序构建的免重复密码启动验证，不保证工具源码／工具链／签名身份变化后仍免询问。

已请求实体退出并重新进入 Codex 直达，查看同名时间并只切换一次左右箭头；TAB5 排序显示和新导航回执的实机结果仍待用户反馈。未修改固件、部署服务、合并 main 或发布。

English: program 09a15ca passed [CI 39559369652](https://github.com/yaoyouzhong/AI-bot/actions/runs/39559369652) with 254 tests and zero failures/skips, plus complete artifact verification. Local Release, fixed-signature packaging, helper identity probes, offline tools and native UI checks passed; local XCTest is still unavailable. After normal e2ca73b exit, the exact new candidate started with a changed main CDHash and identical helper bytes/hash, without another password handoff or observed SecurityAgent blockage. The normal app recovered transports, QWeather, 30 daily read-only chats and music collection. Sending, computer control and voice remain off; the previously authorized temporary direct-navigation switch awaits this check and subsequent closure. This verifies one actual cross-main-build password-free startup, not immunity when helper inputs or signer change. Device timestamp/order and arrow synchronization feedback remains pending. No firmware change, service deployment, main merge or publication occurred.

### 09a15ca 实体未通过，行布局和等待时间续修

用户反馈仍然一样，本次实体直达不记为通过，也未取得单独的排序显示确认。正常 App 精确诊断为：完整遍历 1051 个节点、1 个可信主区域、目标完整标题唯一、1 个当前会话行且有名称；主页面和行名称均未确认匹配，单轮 72 毫秒。这次不是遍历超时。已在正常 App 暂停临时直达，日常只读与采集保持。

对照已安装桌面的实际行组件及其调用方，当前会话标记同时用于侧栏、默认与表格布局，`sidebar-item` 只是侧栏样式。删除将该样式当作身份必要条件的假设，仍必须同时满足可信 App 区域、会话行专用标记、当前页面状态、完整唯一名称及二次前台／窗口／名称核验；普通侧栏按钮和不同名称仍拒绝。另修复六次轮询提前耗尽的问题，使用原有三秒截止时间继续等待异步导航，不重复打开、不读输入框、不发送。回归覆盖非侧栏行及第九次检查才完成的导航；诊断增加长度、分别匹配布尔与样式存在性，不记录标题、ID、地址或正文。自动验证和上机结果待补充，不把这两处源码问题当作本次实机失败的既定根因。

English: the user reported unchanged device behavior, so neither direct navigation nor timestamp display is accepted. The normal app completed 1,051 nodes with one trusted main area, a unique target title and one named current row; neither page nor row matched, in 72 ms. This was not a traversal timeout. Temporary direct control was disabled while daily read-only/collection stayed on. Static caller/component inspection shows that the thread-row marker also covers default/table layouts; requiring the sidebar-only style was unjustified. Confirmation still requires trusted origin, thread marker, current-page state, full global uniqueness and repeated foreground/window/name checks. The retry loop now uses its existing three-second deadline instead of ending after six fast polls. New coverage includes non-sidebar rows and delayed readiness after the old poll limit, with no reopen, editor read or send. New diagnostics contain lengths/booleans only. Automatic and hardware results are pending; these code defects are not asserted as the proven cause of this particular failure.

程序 `6c170fec25475165cedc31401d71b1b062c125a2` 通过 [Mac CI 39562121839](https://github.com/yaoyouzhong/AI-bot/actions/runs/39562121839)：255 项测试、0 失败／跳过，完整附件核验通过。本机 Release、固定签名包、密钥工具身份边界、离线工具及原生界面检查通过，XCTest 本机仍缺失。正常退出 09a15ca 后启动精确 6c170fe，工具 SHA-256 仍完全相同，再次没有新的密码授权交接或观察到 SecurityAgent 阻塞；USB／Wi-Fi／蓝牙和 30 个日常只读会话自动恢复。此前授权的临时直达仅为本次打开验收开启，发送／电脑控制／语音仍关闭。已请求一次实体箭头，等待实际显示及本版更详细的匹配证据；不把 CI 成功写成导航实机通过。

English: 6c170fe passed [CI 39562121839](https://github.com/yaoyouzhong/AI-bot/actions/runs/39562121839) with 255 tests and zero failures/skips, complete artifact verification and local Release/signing/helper/offline/native-UI checks. Local XCTest remains unavailable. After normal 09a15ca exit, the exact new app started with the identical helper SHA-256, again without a new password handoff or observed SecurityAgent blockage; transports and 30 daily read-only chats recovered. The prior temporary direct-navigation grant is enabled only for this open-only check; sending, computer control and voice remain off. One device arrow and the new matching evidence are pending. CI success is not physical navigation acceptance.

### 6c170fe 名称证据与加载后缩略名兼容

用户再次反馈仍一样；实体直达仍未通过。新诊断完整遍历 1053 节点，目标原名 81 UTF-8 字节，当前行的 AXTitle／AXDescription 均为 62 字节且均不匹配；侧栏样式确实存在，因此布局和等待修正没有解决本次实际问题。已关闭临时直达，不再盲目要求点击。

继续静态核对当前桌面的目录与加载后标题调用方：未命名会话的目录预览长度与加载后 60 UTF-16 单元的省略名称不同，正好可以形成 81／62 字节差异。Mac 新增独立实现的名称转换，仅对完整目录中的转换结果唯一时接受精确行／主页面名称，不用目标前缀直接确认；原完整名称路径保留。所有 host、近期列表之外、已有短名与长名转换后的碰撞都纳入检查；缺数据、忙、超限、非法 UTF-8／NUL、不能安全转换的代理对边界及通用应用名不建立该身份。显示名称碰撞仍只用于既有纯打开前台回执，不借给输入框、草稿或发送。没有读取 rollout、剪贴板或通过其他工具获取桌面界面内容。

直接编译当前生产名称模块，执行边界断言和实际只读目录的元数据检查：30 项中缩略名称唯一 17 项、歧义 13 项、不可读 0 项，其中 2 个唯一名称为 62 字节；没有打印标题、任务 ID 或正文。新增回归覆盖完整名不同但缩略名重复、512 项之外及其他 host 碰撞、空白／通用名称、UTF-16 与 emoji 截断边界。此前布局假设已据实更正；本轮缩略名称自动和实机验收待补充。

English: the user reported unchanged behavior. The new complete 1,053-node diagnostic found an 81-byte expected full title versus two equal 62-byte current-row names, neither matching; the sidebar style was present. The previous layout/deadline corrections did not resolve this instance. Temporary direct control was closed. Static catalog/loaded-title caller inspection identified different preview and 60-UTF-16-unit loaded unnamed-chat labels, consistent with the observed lengths. The independent compatibility implementation accepts a compact label only after checking all catalog entries/hosts for collisions, retaining the full-name path. Target-only prefixes, missing/busy/oversized/invalid data, unsafe surrogate boundaries and generic app names cannot establish identity. Ambiguous displayed names permit only the existing open-only acknowledgment, never composer/draft/send permission. No rollout, clipboard or alternate desktop-UI probe was used. Compiling the current production name module directly passed boundary assertions and read-only metadata checks: 30 entries, 17 unique compact names, 13 ambiguous, zero unavailable, including two unique 62-byte names, without printing titles/IDs/content. New regressions cover remote/outside-512 collisions, generic/whitespace names and Unicode boundaries. Automated and physical acceptance remains pending.

程序 `f1696f1f91ee32cd4dee2977d563e7b81dfa7b73` 通过 [Mac CI 39563308958](https://github.com/yaoyouzhong/AI-bot/actions/runs/39563308958)：257 项测试、0 失败／跳过，完整附件核验通过。本机 Release、固定签名包、工具身份边界 probe、离线工具、实际元数据名称检查与原生界面检查通过，XCTest 本机仍不可用。正常退出 6c170fe 后启动精确 f1696f1，再次无新的密码交接或观察到的 SecurityAgent 阻塞；包内密钥工具 SHA-256 连续相同，USB／Wi-Fi／蓝牙、30 个日常只读会话与音乐采集自动恢复。临时直达为本次打开检查开启，发送／电脑控制／语音关闭；已请求一次实体箭头，结果待反馈。未刷机、部署、合并 main 或发布。

English: f1696f1 passed [CI 39563308958](https://github.com/yaoyouzhong/AI-bot/actions/runs/39563308958) with 257 tests and zero failures/skips and full artifact verification. Local Release/fixed-signature packaging, helper identity probes, offline tools, actual catalog-metadata checks and native UI rendering passed; local XCTest remains unavailable. After normal 6c170fe exit, the exact new candidate again started without a new password handoff or observed SecurityAgent blockage, using the identical bundled-helper SHA-256. Transports, 30 daily read-only chats and music collection recovered. Temporary direct control is enabled only for this requested device-arrow check; task mutation, computer control and voice stay off. Physical feedback is pending. No firmware flash, service deployment, main merge or publication occurred.

### f1696f1 切换开始生效，中途授权和名称差异待核对

用户明确反馈已经能够切换，同时指出中途有一次密码授权，以及第二个会话名称与 Mac 左侧列表不同。因此记录切换已经实际生效，不把名称一致性或全部免授权标为通过。正常 App 读回“已确认目标会话”，本次可信主页面名称匹配、14 节点、5 毫秒；此次成功不是只返回同名纯打开回执。随后已关闭临时直达，发送／电脑控制／语音继续关闭，日常只读和采集保留。

仅对桥接服务再次运行禁止认证 UI、只返回 ACL 元数据的检查，四个启动项的工具许可保持；第五个 `tab5-private-v1` 由之前未持久登记工具身份变为已登记路径和当前工具 CDHash。纯 `open-recent` 不读私有存储，但设备后续 `draft-load` 会加载加密 journal，存在首次草稿密钥迁移的独立许可路径；尚未收到弹窗文字，不能将本次用户弹窗直接归因于它。没有读取秘密字节或修改 ACL。已请求用户补充提示来源及两端有差异的名称，区分日期／缩略显示、目录旧名称或实际不同任务；不要求再次切换。

English: the user confirms switching now works, but reports one intervening password authorization and a different second-chat title from the Mac sidebar. Switching has real success evidence; name parity and fully prompt-free navigation are not accepted. The normal app reports exact target confirmation through the trusted primary page, 14 nodes/5 ms, rather than only ambiguous-name activation. Temporary direct control was then closed; daily read-only/collection stays on and mutation/computer/voice remains off. A no-auth-UI, metadata-only ACL check confirms all four startup grants remain and the fifth private-storage key now persistently includes the stable helper path/CDHash. Pure open does not read that key; a following device draft-load can access the encrypted journal. This is a possible first-use migration path, not a proven attribution without the dialog text. No secrets or ACL writes were requested. User clarification of the dialog and differing names is pending; no repeat switch requested.

用户补充弹窗记不清、与之前相似；名称为 Mac 左侧 OpenClaw 前缀，而 TAB5 显示“26-10-21 12:43 Codex 会话”。已定位显示端在公开列表未返回 `name` 时直接填入泛称，日期前缀则来自该泛称重复后的显示消歧。新增按已加载本机任务 ID 批量读取桌面目录预览名，只补未命名项，保留显式名称优先、原任务 ID 与更新时间排序；失败保留本进程已取得的目录名，空／非法／缺项不猜测标题。不以显示读取结果建立导航身份或增加任务；查询只读、100 毫秒、8192 行／8192 字节边界，不读取正文或凭据。本机独立编译生产模块的只读实际元数据检查为请求 30、匹配 30，不打印名称或 ID；Release 通过。新增测试覆盖未命名项回填、显式名不覆盖、失败保留与成功缺项回退，以及重复标题允许显示但不得作为唯一身份、其他 host／已删除／非法名称不导出。完整 CI 与新候选实机名称待验收；中途密码准确来源仍未证实，不承诺零弹窗。

English: the user recalls only a similar password dialog and specifies that the Mac sidebar has an OpenClaw title while TAB5 shows a timestamp plus the generic chat label. The public list's absent name was directly replaced by that generic label, whose duplication then added the date. A bounded read-only batch lookup now supplements only unnamed loaded local IDs from desktop preview metadata, keeping explicit names, task IDs and newest-first order. Failures retain process-local successful names; malformed/missing metadata does not invent one. Display lookup never establishes navigation identity or adds tasks. The current production module independently passed real metadata checks for all 30 requested IDs without printing labels/IDs; local Release passed. New regressions cover label precedence, failure retention, missing metadata, duplicate display versus identity, remote/removed/invalid rows. Full CI and hardware label parity remain pending; the intervening dialog's exact source is still unconfirmed.

用户再次询问 Codex 任务列表位置，已明确说明仍未修复、未通过实体布局验收。只读重新核验已登记 SHA 的 `.234` 源码：`ui.c:1302–1324` 将所选项目的有效任务紧凑放到 `visible_tasks[0…]`，然后顺序更新行并隐藏剩余行；`ui.c:3601–3614` 在设备上设置列表容器位置和每行 `i*88` 坐标；切换项目路径 `1727–1740` 没有收到桥接坐标或空白行控制。Mac 分页项目由实际选中任务生成，协议没有列表坐标字段。因此不能把全局任务序号或缺少空白任务条目作为本次上方空白的修复点；列表容器／滚动状态的具体原因仍待设备布局验证。遵守用户此前“固件本身先不修”的范围，不修改、编译或刷写固件，不把待办说成桥接已修好。

English: the user asks again about the misplaced task list; it remains unfixed and physically unaccepted. Read-only verification of the recorded `.234` source hash confirms that the device compacts valid selected-project tasks into rows starting at zero, hides unused rows, and owns the list container/row coordinates (`ui.c:1302–1324`, `3601–3614`). The project-change path has no bridge-coordinate/blank-row input. Mac project pages are generated from actual selected tasks; the protocol carries no list coordinates. Global task indices or missing padding entries therefore are not a justified repair for the blank space. The exact container/scroll-state cause remains unverified. Under the standing firmware-deferral instruction, no firmware was modified, built or flashed, and this is not reported as a bridge fix.

程序 `441dc654e1d3c7aec37e42fd9076d195748bdbe3` 通过 [Mac CI 39565250748](https://github.com/yaoyouzhong/AI-bot/actions/runs/39565250748)：259 项测试、0 失败／跳过，完整附件核验通过。本机 Release、固定签名包、工具身份边界／离线／原生界面检查及实际名称元数据检查通过；本机 XCTest 仍缺失。正常退出 f1696f1 后启动精确 441dc65，无新的启动密码交接或观察到的 SecurityAgent 阻塞，密钥工具 SHA-256 仍相同。USB／Wi-Fi／蓝牙、30 个日常只读会话、音乐采集自动恢复。此前许可的临时直达已为名称和中途弹窗检查开启，发送／电脑控制／语音关闭。已请求重新进入后核对第二项名称及密码提示，尚待反馈，不推定通过。

English: 441dc65 passed [CI 39565250748](https://github.com/yaoyouzhong/AI-bot/actions/runs/39565250748) with 259 tests and zero failures/skips and full artifact verification. Local Release/signing, helper identity/offline/native UI checks and actual title-metadata checks passed; local XCTest is still missing. The exact new candidate replaced f1696f1 normally, without a new startup password handoff or observed SecurityAgent blockage; helper SHA-256 is unchanged. Transports, 30 daily read-only chats and music collection recovered. The previously authorized temporary direct switch is enabled only for the name/intervening-dialog check; mutation/computer/voice stays off. One reentry/check of the second chat and any password dialog is requested, with physical feedback pending.

### 441dc65 无弹窗实机结果与最近活动排序续修

用户明确反馈这次没有密码弹窗，名称基本正确，但纠正排序为 Mac 左侧 1／2／3／4，TAB5 对应 1／4／2／3。本次换主程序后的启动与切换无密码均有实际观察，不保证将来工具或签名变化后免询问；显示名称获得改善，排序仍未通过。正常 App 再次读回可信主页面精确匹配，随后关闭临时直达，日常只读保留。

只读目录元数据对比直接复现：按 `source_updated_at` 排序得到 `source_recency_at` 顺序中的 1／4／2／3。已安装桌面的原生最近列表使用 `recency_at`／`recencyAt`。临时、只读标准 App Server 子进程验证该排序键接受，30 项均提供有效活动时间；旧更新时间顺序同样为 1／4／2／3，最近活动顺序前四项与桌面目录一致。不输出标题、ID、正文或凭据，子进程检查后退出，不安装服务。

修正列表请求与合并后排序采用原生活动时间，缺失或非法活动时间才回退有效更新时间。只在首列表请求被拒绝时，为老接口按实例回退一次排序键；超时不重试且保留旧列表。只读复核 `.234` 直达消费者会再次按既有任务 `updatedAt` 排最近五项，因此将相同活动时间映射到该既有排序值，日期消歧和概览也保持一致；不增加字段、变更 ID 或修改 Windows／固件。新增回归覆盖精确 1／4／2／3 反例、多页合并、设备二次排序、兼容回退和超时保留、非法及零时间。自动完整验证与新实机顺序待补充。

English: the user confirms no password dialog and broadly correct names, correcting the order comparison to sidebar 1/2/3/4 versus device 1/4/2/3. This verifies the observed changed-build startup/navigation without prompts, not immunity to future helper/signer changes; sorting is still unaccepted. The normal app again confirmed the trusted primary target; temporary direct control was closed, preserving daily read-only. Read-only metadata reproduced 1/4/2/3 exactly when sorting record updates instead of native recency. The installed desktop uses recency_at/recencyAt. A temporary standard read-only App Server child accepted that key, supplied activity times for all 30 entries, reproduced the old mismatch and matched catalog recency for the new top four. No titles/IDs/content/credentials were printed and the child exited without a service. The fix requests/merges by activity time, falling back to valid update time, and performs one instance-local old-key fallback only after a rejected first list request, never a timeout. Existing firmware re-sorts by task updatedAt, so the same effective activity time must be carried in that established sort field. IDs, authentication, Windows and firmware are unchanged. New coverage includes the exact counterexample, cursor merging/device re-sorting, rejection/timeout behavior and malformed/zero times; full automated and hardware order checks are pending.

程序 `b204f0de7692563fbd4e442406cae9bfda79fc2f` 通过 [Mac CI 39567265920](https://github.com/yaoyouzhong/AI-bot/actions/runs/39567265920)：262 项测试、0 失败／跳过，完整下载附件核验通过。本机 Release、固定签名 App、随包工具和原生界面检查通过；密钥工具字节保持相同，本机 XCTest 仍缺失。已正常退出 441dc65，并按精确路径启动 b204f0d，确认运行进程。随后界面工具返回 Mac 已锁屏，自动解锁因物理输入暂停，要求用户本人手动解锁；未尝试绕过或获取密码。已请求本机解锁，正常 App 的连接／偏好恢复和实体排序仍待继续，临时直达沿用之前关闭值。未合并 main、部署、发布或修改／刷写固件。

English: b204f0d passed [CI 39567265920](https://github.com/yaoyouzhong/AI-bot/actions/runs/39567265920) with 262 tests and zero failures/skips and complete downloaded-artifact verification. Local Release/fixed-signature packaging, offline tools and native UI checks passed; helper bytes are unchanged and local XCTest remains unavailable. 441dc65 quit normally; the exact new app process started. Computer Use then reported a locked Mac with automatic unlock paused after physical input, requiring manual user unlock. No bypass/password access was attempted. Manual unlock was requested; normal-app recovery and physical recency ordering remain pending, with temporary direct control retaining its prior off preference. No main merge, deployment, publication or firmware change/flash occurred.

用户已在本机解锁。正常 b204f0d 界面确认 USB 数据已确认、Wi-Fi 已认证、蓝牙 USB 优先待命、和风当前／24 小时／7 天数据恢复；权限页为 30 个日常只读会话，音乐采集开启，发送／电脑控制／音乐控制／语音关闭。此前授权的临时直达仅为本次排序核对开启；已请求退出并重新进入实体直达页，再核对前四项顺序及有无密码弹窗，等待实际反馈，未将自动测试视作实机通过。

English: after the user manually unlocked the Mac, the normal b204f0d UI confirmed USB data, authenticated Wi-Fi, Bluetooth USB-priority standby and current/24-hour/7-day QWeather recovery. Its permissions show 30 daily read-only chats and music collection on, with task mutation, computer/music control and voice off. Previously authorized temporary direct control is enabled only for this order check. Reentry into the physical direct panel and verification of its first four entries and any password prompt were requested; physical feedback is pending, separately from automatic test results.

### b204f0d 实体排序通过与日常功能开启约定

用户重新进入直达并核对前四项后回复“这下 OK 了”，本次最近活动顺序实机通过；未单独补充密码弹窗次数，不据此新增具体次数结论。正常 App 同时读回“已确认目标会话”，可信主区域匹配、14 节点、12 毫秒，输入框操作继续独立校验。随后曾按旧的临时验收约定关闭直达，用户明确纠正“所有这些功能都应该打开”。**这一最新指令替代此前本轮临时开关的收尾约定：保留日常开启，不能在验收后再次自动关闭。**

已通过正常 b204f0d 界面开启并读回全部七项：电脑控制、音乐采集、音乐控制、Codex 公开回复／加密草稿、Codex 发送／中断、桌面直达及本地语音。Codex 显示连接本机桌面、30 个近期会话、操作已启用；语音完成既有权限核对，显示“已启用，等待 TAB5 发起收音”。开关写入现有持久偏好，不修改新安装的通用默认策略，也没有实际发送／中断、开始收音或锁屏。发送等动作仍要求用户明确点击，原模型／审批设置保持；追加和未通过的输入框操作不因开关开启而声称可用。任务列表上方空白仍未修复，按用户范围继续暂缓固件改动。未合并 main、部署、发布或刷机。

English: after reentering the physical direct panel and comparing the first four entries, the user replied that it is now correct, accepting recency order. No separate password-dialog count was supplied. The normal app also confirmed the exact trusted primary target in 14 nodes/12 ms; composer actions remain independently checked. Direct control was briefly closed under the former temporary-test agreement, then the user explicitly directed that all these functions remain enabled. **That latest instruction supersedes the earlier cleanup convention: keep these daily preferences on rather than automatically disabling them after acceptance.** All seven normal-app switches were enabled and read back: computer control, music collection/control, Codex public replies/encrypted drafts, task send/interrupt, desktop direct control and local speech. The app reports 30 connected local chats with operations enabled and speech enabled awaiting a device request after existing permission verification. Existing persistent preferences are saved; generic fresh-install defaults are unchanged. No task mutation, recording or locking was triggered. Actions still require explicit user clicks and retain model/approval settings; unsupported append/composer capabilities are not implied. Device task-list blank-space layout remains deferred under the firmware boundary. No main merge, service deployment, publication or flashing occurred.
