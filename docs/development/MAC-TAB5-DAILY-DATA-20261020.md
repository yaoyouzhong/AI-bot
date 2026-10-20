# Mac 日常数据补齐 / Daily data parity

接续音乐与 Codex 七日额度验收，用户另报告网络曲线、天气、首页农历下方信息、DeepSeek 和智谱额度缺失。本轮仍只修改 Mac 桥接，保持既有 v1 字段；不修改 Windows 或设备代码，不刷机、部署、发布或合并 main。

## 已定位与修正

- 网络：Mac 只有瞬时上传／下载，没有 `sampleSession`、`sampleSequence` 和 `samples`。实际 `.234` 的 `render_system` 只有收到样本数组才调用 `render_chart`。新增单调时钟的 250 ms 采样、64 条有界历史、一秒不可变发布和末四样本均值；桥接重启更换会话，长采样间隔保留序号缺口，不重放虚构样本。TAB5 仍通过已有完整快照收到历史，本轮没有新增快速 metrics 传输。CPU／内存单独每两秒采样。
- 天气：本机没有城市／坐标或缓存，Mac 原先没有自动定位实现。新增用户主动选择的 Core Location 路径，默认关闭；位置仅按约一公里网格取整后查询既有 Open-Meteo 服务，不记录／持久化精确坐标。授权拒绝／超时有状态提示。手动城市继续可用，空气质量失败不再使成功天气丢失，主天气失败仍保留上次结果。
- 首页：实际页面农历下方读取 `calendar.holidayLabel`，Mac 原来只有日期、星期和农历。独立实现当前／次日的节气和工作日／补班／假期标签，采用公开的 2026 年事实表；未知年份明确显示待更新，不推算法定假日。本轮未实现 Windows 的联网节假日年度更新、生日管理或完整日历详情。
- 国产额度：Mac 原来没有余额采集，TAB5 还移除了 `domesticQuotas`。补齐显示缓存与固定的 11 个 provider 位置；DeepSeek 位于索引 5，智谱位于 6，保持 `billingMode=payg`、`balance`、`cost` 和币种契约，不以 token 数计算余额。未授权或未取得的值仍未知。
- DeepSeek：新授权输入框只写钥匙串，启用后每两分钟查询官方 `/user/balance`；拒绝 HTTP 跳转，错误只显示分类／状态码。更换密钥清除旧账户缓存；失败保留已有数据并标记旧数据。无模型调用或付费操作。
- 智谱：应用内打开官方财务页，使用独立临时 WebKit 会话。仅接收该页面已请求的精确财务接口成功响应，解析 `availableBalance` 和累计支出；不读取 Cookie、请求凭证、存储令牌或页面任意数字。校验主框架、HTTPS 来源、接口路径、响应大小和数据结构。关闭财务页释放会话并将余额标记为缓存；再次打开或点击刷新可更新。本轮不提供关闭网页后的后台自动查询。

## 验证与边界

新增回归涵盖网络发布／同一均值／有界历史／采样断档，日历调休／假期／跨年未知，空气质量失败与粗粒度坐标，余额真实字段／重复币种／非法值／缓存及固定数组位置，智谱接口来源约束。自动测试不等于定位、授权、真实余额和实体曲线验收；新候选这些实机项目仍待执行。正常候选的音乐和 Codex 已通过结果继续保留。

事实与接口依据：

- [Apple CLLocationManager](https://developer.apple.com/documentation/corelocation/cllocationmanager)
- [Open-Meteo 天气接口](https://open-meteo.com/en/docs)
- [DeepSeek 余额接口](https://api-docs.deepseek.com/api/get-user-balance/)
- [2026 年国务院节假日安排（北京市政府转载）](https://www.beijing.gov.cn/fuwu/bmfw/sy/jrts/202511/t20251104_4258838.html)
- [香港天文台 2026 公农历及节气表](https://www.hko.gov.hk/tc/gts/time/calendar/text/files/T2026c.txt)

## English

The reported missing network chart, weather, home holiday line and DeepSeek/GLM balances are Mac producer gaps. Changes use existing v1 fields; Windows and firmware are unchanged. No deployment, release, main merge or flash is authorized.

Networking adds monotonic 250 ms samples, a bounded 64-sample tail, immutable one-second publications and a trailing four-sample mean. Suspensions leave sequence gaps, without fabricated samples; restart changes the sampling session. Existing full TAB5 frames carry the tail; a new fast metrics transport is not implemented. CPU/memory sample separately every two seconds.

Weather gains explicit, off-by-default Core Location selection, kilometer-scale rounded request coordinates, no precise-location persistence, and denial/timeout diagnostics. Manual locations remain available. Air-quality failure no longer discards a successful forecast; forecast failure retains last-good weather.

Calendar supplies today's/tomorrow's solar-term and holiday/workday labels using cited 2026 public facts, with explicit unknown-year fallback. Online yearly schedule updates, birthday editing and full calendar details are not part of this patch.

Domestic balances preserve the established eleven provider positions (DeepSeek 5, GLM 6), PAYG fields and unknown values. DeepSeek's key stays in Keychain; the official balance endpoint is polled every two minutes after authorization, redirects are denied and key changes clear the previous account cache. Errors preserve stale data, with no model calls or financial actions. GLM uses a temporary official-finance WebKit session, observing only the exact finance response requested by that page. Main-frame origin, path, size and schema are validated; credentials/cookies/storage and arbitrary DOM amounts are not read. Closing the finance window releases its session and marks the last balance cached; reopening or manual refresh updates it. Closed-window automatic GLM refresh is not implemented.

Regressions cover network publication/mean/bounds/gaps, calendar boundaries, optional air-quality failure, coarse coordinates, official balance fields/schema failures, cache preservation/provider positions and finance endpoint restrictions. Physical location, authorization, actual balances and device charts remain pending; earlier music and Codex acceptance stays valid.
