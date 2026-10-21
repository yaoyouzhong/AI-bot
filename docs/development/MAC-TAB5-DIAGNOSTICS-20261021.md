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

## English

Only the Mac bridge, tests and docs change. No main merge, deployment, publication or flashing. Firmware camera/layout issues remain deferred.

App-owned probes use public CFNetwork resolution and URLSession transaction metrics, with bounded unauthenticated HEAD requests, no stored body/cookies/credentials or redirects, and no proxy changes. Network timestamps expire independently after 75 seconds; ten actual rounds are retained, and failures preserve separately dated last-success evidence. Stable sorted-JSON configuration fingerprints repaired a locally observed discarded-sample issue. Native probing then returned HTTP 405/404 at approximately 993 ms, with PAC unconfigured. Responses establish HTTP reachability, not successful model service. Proxy/direct type matching does not identify nodes or VPN tunnels.

The Windows CPU queue counter has no established public Mac equivalent and stays absent. Desktop navigation remains unaccepted: the installed editor lacks the expected fixed identity. Diagnostics now distinguish absent identity from absent exact thread address, accept URL/string address representations, and still reject generic text areas or window-title matching. App Server task APIs do not prove desktop composer identity. Local XCTest remains unavailable; CI and physical results must be recorded separately.

Actual loopback PAC fixtures exposed normal CFNetwork output containing a primary script plus fallback DIRECT. The repair resolves the primary script, rejecting errors rather than accepting fallback as proof. Current production source compiled independently and passed DIRECT, PROXY, combined and malformed-script cases; the same regression is included in XCTest without changing system proxy settings. SDK ownership overloads handle both managed and Unmanaged run-loop sources. Two real network rounds produced samples and stop cleared the current sample. The first CI compile failure was recorded; that failed candidate was not launched.

Final program bdd84cd passed linked CI with 227 tests, zero failures/skips, and complete downloaded-artifact verification. Local Release, signed bundle, offline tools and native fixture rendering passed. The old app was normally quit after temporarily stopping read-only Codex; the exact new app path is running, awaiting user Keychain authorization. Restore the previously authorized daily read-only feature afterward; other temporary controls stay off. Normal-app and physical acceptance are pending, and neither desktop direct control nor CPU queue availability is claimed fixed.

After user Keychain approval, normal-app checks confirmed acknowledged USB, authenticated Wi-Fi, USB-priority BLE standby and restored daily read-only Codex with 30 chats; other temporary controls remain off. Both unauthenticated status routes return 401. Live network latency repeatedly updated around 957–1092 ms with PAC unconfigured. The user physically confirmed VPN latency values; CPU remains unavailable as documented. Device PAC wording/graph and desktop direct control are not accepted yet. Location was disabled for the candidate in System Settings; restoring the previously authorized scope and refreshing unchanged QWeather settings recovered current/24-hour/7-day weather. No credentials, device settings or Wi-Fi configuration were changed; unsent user text in the Wi-Fi name field was not provisioned.
