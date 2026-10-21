# Mac 桥接诊断与 Codex 直达续修 / Diagnostics and desktop continuation

范围：仅 Mac 桥接、测试和文档；不合并 main、不部署、不发布、不刷机。保留 9415cee 已通过的锁屏、磁盘读数和日常只读 Codex；固件侧相机和列表布局问题暂缓。

## 变更与证据

- 网络诊断使用公开 CFNetwork 系统代理解析和 URLSession 的实际事务指标。每 30 秒对既有 Windows 两个目标执行无登录 HEAD，禁止缓存、Cookie、凭据、正文保存和重定向跟随，不改变系统网络配置。连接超时、HTTP 限制、慢响应分别描述；历史最多十轮，独立时间戳 75 秒失效。失败保留上次成功耗时及时间，不生成新的成功值。
- PAC 未配置明确标记 disabled；配置存在时，比较解析得到的代理／直连类型集合与实际连接指标。不验证具体代理节点，不把无显式代理当作没有 VPN，不把 HTTP 405/404 当成已验证模型服务。
- 初次原生探测未产出样本，定位为代理配置指纹的字典编码顺序不稳定；改用排序 JSON 后实际探测返回 ChatGPT HTTP 405、资源 HTTP 404，最大耗时约 993 ms，PAC 未配置。只保留固定目标标签、状态码、耗时和类型，未输出系统代理地址或 PAC URL。增加字典顺序稳定性回归。
- CPU 等待的 Windows 字段是队列长度。Mac 公开调度／负载接口未找到等价计数；继续省略 processorQueue，并明确显示平台限制，不用负载、线程数或 CPU 百分比估算。
- 9415cee 的直达失败已定位到主输入框标识。静态检查本机桌面编辑器发现其使用无固定 id 的富文本框；未找到既可绑定精确会话又可读写桌面主输入框的公开接口。官方 App Server 提供任务操作，但不构成桌面输入框身份凭据。新增同时判断输入框标识和精确地址的诊断，兼容 AXURL 的 URL／字符串表示，仍禁止通用文本框或标题猜测。此项不是直达验收通过。

自动验证：本机 XCTest 仍在执行前报缺少模块；Release 和完整 Mac CI 分开记录，不以静态检查代替测试。新候选 CI、完整附件核验和正常 App／TAB5 验收待补充。

## English

Only the Mac bridge, tests and docs change. No main merge, deployment, publication or flashing. Firmware camera/layout issues remain deferred.

App-owned probes use public CFNetwork resolution and URLSession transaction metrics, with bounded unauthenticated HEAD requests, no stored body/cookies/credentials or redirects, and no proxy changes. Network timestamps expire independently after 75 seconds; ten actual rounds are retained, and failures preserve separately dated last-success evidence. Stable sorted-JSON configuration fingerprints repaired a locally observed discarded-sample issue. Native probing then returned HTTP 405/404 at approximately 993 ms, with PAC unconfigured. Responses establish HTTP reachability, not successful model service. Proxy/direct type matching does not identify nodes or VPN tunnels.

The Windows CPU queue counter has no established public Mac equivalent and stays absent. Desktop navigation remains unaccepted: the installed editor lacks the expected fixed identity. Diagnostics now distinguish absent identity from absent exact thread address, accept URL/string address representations, and still reject generic text areas or window-title matching. App Server task APIs do not prove desktop composer identity. Local XCTest remains unavailable; CI and physical results must be recorded separately.
