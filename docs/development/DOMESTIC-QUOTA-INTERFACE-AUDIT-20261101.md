# 国产模型额度接口核对（2026-11-01）

## 后续补齐：Windows 实现完成，真实套餐待验证

用户授权“补齐”后完成以下增量，下面的审计表已更新为当前状态。

- **MiMo：已完成实现／待验证真实账户。** `XiaomiQuota.cs` 支持 `data.usage.items`，选择唯一非 `compensation_total_token` 的主额度行，转换其 0–1 比例，并交叉验证已提供的 used/limit；不混合补偿额度、不使用外层旧百分比覆盖新行、不任取多行中的第一项。缺失、矛盾、非法比例或多主额度行继续失败保留缓存。周期只取被选中行的有效起止时间，旧平铺格式继续支持。
- **MiniMax：已完成实现／待验证真实账户。** 新增 `MiniMaxQuota.cs`，网页采集和 `DomesticQuotaService` 均使用同一绝对毫秒时间戳与扩容比例规则。`end_time/weekly_end_time` 优先于旧剩余倒计时，缺失才走旧格式；明确无效时间保持未知。新版 `*_used_percent / *_total_percent` 转换为实际利用率，例如 120 / 200 = 60%，耗尽封顶 100%，无效总量拒绝。未修改共享协议、布局或固件。
- 官网时间单位依据：公开 `7620-d6b6d15d5fe605da.js` 从 resetTime 减 `Date.now()` 得到 resetInMs；`2759-494e3ef8d7ed23ed.js` 将上述 end_time 字段作为 resetTime，并将新版 used/total 传给进度组件。只复核契约，没有迁入第三方实现。
- 新增公开自测入口 `Program.cs --self-test-domestic-quota`，先 `AppPaths.BeginPublicSelfTest`，再执行既有官方 API、附加厂商、MiMo 和周期自测。无需完整 public suite 的窗口测试，也无需反射调用。新增样例覆盖补偿在第一项、零/耗尽、外层旧值隔离、多主额度、比例/计数冲突、缓存保留、周期清理、扩容比率、非法总量、绝对时间/旧倒计时优先级和两条解析路径一致。
- Release build 0 警告/0 错误；源码与完整发布候选的 `--self-test-domestic-quota` 均通过，`--self-test-zhipu`、`--self-test-data` 回归通过。最终候选在 `C:\Users\yaououzhong\Work\AI-bot\artifacts\windows-quota-compat-final-20261101`。进一步统一 MiniMax 两条解析路径的通用模型选择，排除 video/hailuo 单独预算，避免视频字段的零总额阻挡通用额度；混合模型样例回归通过。前一 compat 目录仅作候选检查，未安装。
- **Mac：待同步。** 本轮 Windows 修复不改 Mac 源码。Mac 需要一并核对 GLM 新旧入口、MiMo 当前主额度结构、MiniMax 重置毫秒时间戳与扩容归一化；必须在 Mac 编译并以有效账户验证。Kimi/阿里以及其他未配置的真实账户验证仍待完成，没有使用模拟结果替代。

13:37 最终兼容候选已正常安装并启动，DLL SHA-256 `214c862269f0767471102e22117a7f3415f15de212d285b0dc081819232c895e`，PID 60116。107 个 payload 文件逐一核验，108 个旧文件可回滚；正常退出后、安装状态检查后的 7 项配对/草稿/设置文件哈希与备份一致。源码必需 status-once、候选及安装后 status-once 全通过；启动后只读 `/status` 验证展示模式、六页顺序及 15 秒间隔不变。13:38:17 GLM 再次真实 `balance-saved`。MiMo/MiniMax 无本次有效订阅响应，不宣称已完成真实账户验收。

额外的桌面 post 辅助进程启动被自动审批拒绝，原因只有 `blocked by policy`；没有重试绕过。改用只读运行 API 核对展示策略，并用原 `AppProfileGuard.PhysicalPath` 检查文件访问位置。工具环境中 6 个配置路径被重定向，未读取这些副本、更未据此判定生产文件发生变化；启动后全量配置哈希复核仍未完成。`readonly-post-result.json` 将未验证项记为 null，安装前后已通过的 7 项哈希验证保持有效。未推送、合并、打标签、发布或刷机。任务 worktree 保留，因 Windows 最终验收和 Mac 同步尚未完成。

本轮由 Windows GLM 网页余额持续超时触发，随后按用户要求核对其他国产模型。公开官网脚本只用于核对接口契约，没有复制其实现；没有导出账户 Cookie、令牌或响应正文，没有启动新的登录流程、切换前台或修改监控选择。下列“接口一致”不代表真实账户验收通过。

## 结论和状态

| 厂商 | 状态 | 接口与实现证据 | 验证及剩余问题 |
| --- | --- | --- | --- |
| 智谱 GLM 网页余额 | **已完成（Windows）／待验证（Mac）** | 新版 GET `/api/biz/account/finance-overview` 的 `data.availableTotalBalance`；Windows `ZhipuBalance.cs`、`MigratedDomestic/DomesticQuotaService.cs` 已兼容。旧接口继续支持。 | 更新本机候选后 12:15:00、12:17:08、12:19:20 均出现真实 `balance-response=200`、`balance-saved`，`stale=false`，无需重登。实体屏金额/观感未新增验收。Mac 仍需适配。 |
| DeepSeek | **已有无需修改** | 官方 `GET https://api.deepseek.com/user/balance`，`balance_infos[].total_balance/currency`，与 `DeepSeekBalanceApi.cs` 一致；网页取数仍成功。 | 12:16:01、12:18:13、12:20:25 网页 `usage-saved`；12:20:58 主机余额更新且 `stale=false`。部分被宽路径过滤器捕获的非余额响应出现 `wallet-fields-missing`，随后真实余额响应成功，不等于整轮失败。未发现本次需要迁移余额接口的证据。 |
| 阿里百炼 Token Plan | **待验证** | 当前官方 console `fetcher.js` 仍使用 `/data/api.json`，与 `DomesticQuotaService.cs` 的监听地址相符；官方说明仍区分 Token Plan Credits 与云账户余额。 | 当前只有 7 月历史缓存，非本次实时成功。没有有效订阅响应，不能确认当前具体 action/响应层级全部兼容，也不能因缓存陈旧断言授权过期。 |
| Kimi Code | **已有无需修改（官方本地 API 契约）／待验证（真实账户和网页）** | `KimiUsageApi.cs` 的 `/api/v1/oauth/usage` 与当前官方 Server API 一致，支持 `data.kind`、`quota.usages` 的 `limit5h/limit7d/monthTotal/monthCode`。 | 只有 8 月历史缓存。公网 `/code/console` 返回 Kimi 主站脚本，不能据此证明登录后的 Coding 网页 `BillingService/GetUsage` 未变化；不把主站订阅余额混当 Code 额度。需真实 Code 账户或本地服务复核。 |
| MiniMax | **已完成 Windows 实现／待验证账户** | 网页新旧路径均覆盖；MiniMaxQuota.cs 与共享模型解析实现绝对毫秒时间戳、扩容 used/total 归一化和视频预算排除。 | 新旧契约、混合模型、缓存/周期和两条入口回归通过。只有 9 月历史账户缓存；真实 API Key / 网页订阅刷新、实体显示仍待验证。 |
| 小米 MiMo | **已完成 Windows 实现／待验证账户** | XiaomiQuota.cs 支持新版唯一主 usage.items 行，兼容旧平铺字段，补偿单列不混算。 | 官网结构合成样例、歧义/非法值保护、周期和缓存回归通过。当前无真实订阅缓存；真实刷新及多主额度账户仍待验证，后者保守拒绝而非任取第一项。 |
| 阶跃星辰 | **已有无需修改（文档契约）／待验证（账户）** | `AdditionalQuotaApi.cs` 的 `GET /v1/accounts` 及 `object=account/type/balance` 与当前官方文档一致。 | 当前无账户结果；只是开放平台可用余额，不是 Step Plan 额度。 |
| 百度千帆 | **已有无需修改（文档契约）／待验证（账户）** | `AdditionalQuotaApi.cs` 的 `POST /v2/charge?Action=DescribePackageResource`、AK/SK 签名及 `result.instance` 与官方文档一致。 | 当前无指定量包结果。不是百度云钱包余额；必须核对指定 packageId、有效期和单位。 |
| 火山方舟、腾讯混元、华为盘古、讯飞星火、百川、零一万物 | **不适用（现有自动额度接口）** | `DomesticProviderCatalog.All` 将这六家的 `CaptureSupported` 标记为 false，当前仅有官网入口。 | 没有现有采集接口可做“是否失效”验证；这不代表厂商没有可用 API，也不算本项目已支持。 |

## GLM 修复及安全更新

根因：旧桥接网页加载成功，但只监听 `query-customer-account-report`；新页面请求 `finance-overview`，所以 60 秒后超时。没有授权拒绝证据。新旧接口严格按 HTTPS 官方域名、默认端口和精确路径放行，再按捕获路径选择各自字段；缺字段、非成功响应、NaN 或无效金额均拒绝并保留缓存。

新版页面采用 `Big.toFixed(2, 1)`，Windows 对应两位小数 half-up；旧版保留向零截断。新版不含累计消费，USED 留未知，不凭空计算或复用旧累计消费。详见 [GLM 取数说明](../GLM_BALANCE.md)。

- Release build：0 警告、0 错误。
- 必需源码 `dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -- --status-once`：在正常桌面上下文通过；候选和安装后的 status-once 也通过，未绕过 AppProfileGuard。
- `--self-test-zhipu`、`--self-test-migration`、`--self-test-data`：通过。data 自测首次使用隐藏 GUI exe 遇到 console encoding 句柄错误，改用 `dotnet AIBotBridge.dll --self-test-data` 后通过，未修改断言。
- 额外离线审计通过既有 `OfficialQuotaApiSelfTest`、`AdditionalQuotaSelfTest`、`XiaomiQuotaSelfTest`、`DomesticQuotaCycleSelfTest`。这些入口属于 `--self-test-public`，本次以反射单独调用，并先使用其既有 `AppPaths.BeginPublicSelfTest` 隔离账户数据，避免运行完整 public suite 中会显示窗口的测试；没有运行生产刷新、读取实际凭据或放宽 profile guard。模拟测试不能替代真实账号成功。
- 本机 107 个候选文件逐一 SHA-256 校验，108 个原文件回滚备份；7 个配对/草稿/设置文件在安装前后和启动后保持一致。展示模式、页面顺序和 15 秒间隔保留，图库未覆盖。
- 安装 DLL SHA-256：`5c9715eb3ec4d243cf4b11cb06d9fa8bfd9385b752408119cb653940f92f77a8`。证据与回滚：`C:\Users\yaououzhong\Work\AI-bot\artifacts\windows-glm-20261101`，公开脚本与合成样例不作为真实账户数据。

## Mac 与主干交接

只读 fetch 已看到最新主干 `f4a509d`（PR #20，Mac USB OTA）；相对本地候选基线 `0b39860`，该 PR 只改 Mac 和文档，没有 Windows 源码变化。本轮未合并、rebase、提交、推送、打标签、发布或刷机，既有未提交工作保留。

已直接检查最新 `origin/main` 的 `MacDomesticBrowser.swift`：Swift URL 过滤与注入 JavaScript 过滤仍只接受 GLM 旧接口。Mac 应同时补齐两处过滤、按端点绑定的新字段解析和上述金额规则，保留旧接口与异常缓存保护；增加新旧契约测试，再在 Mac 上执行 `swift test`、`swift build -c release` 和真实后台刷新验收。Windows 安装成功不能代替这一步。Mac R35 排队状态实机验收仍待完成。

优先待办：MiMo、MiniMax 真实套餐验收及 Mac 同步；Kimi 网页和阿里 Token Plan 真实响应核对。Windows 录音启动及发送前文字填入速度的后续优化仍待推进，本轮 GLM 修复没有把它们标记为完成。任务 worktree `C:\Users\yaououzhong\.codex\worktrees\windows-w29\AI-bot` 保留，用于尚未完成的对齐和最终验收；未获授权合并或打归档标签，不能删除。

## 公开依据

- [DeepSeek 余额 API](https://api-docs.deepseek.com/zh-cn/api/get-user-balance/)
- [Kimi Code Server API](https://www.kimi.com/code/docs/en/kimi-code-cli/reference/server-api.html)
- [阿里 Token Plan FAQ](https://help.aliyun.com/zh/model-studio/token-plan-personal-faq)；[当前 console fetcher](https://g.alicdn.com/bailian-fe/bailian-site-fe/0.0.180/assets/fetcher.js)
- [MiniMax 官网当前额度适配脚本](https://filecdn.minimax.chat/platform_web/prod-console-cn-0.1.7/_next/static/chunks/2759-494e3ef8d7ed23ed.js)
- [MiMo 当前接口声明](https://platform.xiaomimimo.com/static/main.3cb60685ae060faa.chunk.js)；[当前套餐显示脚本](https://platform.xiaomimimo.com/static/279.f8ccd0caddcde327.chunk.js)
- [阶跃账户信息](https://platform.stepfun.com/docs/zh/api-reference/accounts/get)
- [百度千帆指定量包查询](https://cloud.baidu.com/doc/qianfan/s/3mh4sv4ve)
