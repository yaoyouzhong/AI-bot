# Mac TAB5 额度显示补齐 / Quota display parity

用户报告近 7 日周额度使用缺失、重置卡信息不完整。已比对本仓库 Windows 调用方和正在运行的 `.234` 源码归档中的 `ui_dashboard.h`：设备读取 `quotaTrend.days` 和 `quotaDetails[].creditExpiries`，Mac 原来均未提供。此次只补已有协议字段，设备及 Windows 代码不变。

- `quotaDetails` 提供逐张到期时间（`yyyy-MM-dd HH:mm`，设备状态所用时区），保留同一到期时间的多张卡。
- 周期长度传递供应商实际秒数；Claude 的有名称窗口为 5 小时／7 天；未知、非整数、非法值不猜测。缓存和降级读回保留已知周期长度。
- 独立的重置卡请求超时或内容损坏不再丢弃已取得的周额度；原有同数量卡片日期缓存规则继续适用。
- Mac 在 `~/Library/Application Support/AI-bot/codex-quota-history.json` 保存可展示的额度采样和账户 SHA-256 判别值，不保存凭证、原始响应或会话内容。只记录成功的新采样，保留 90 日、最多 140000 条；损坏文件保留原样，本轮仅内存记录并通告保存警告。
- 七日图采用 Windows 的观察增量口径、上海时区、百分比点单位。当前日或覆盖不完整的日期标记部分数据；未知与已观察零用量区分。账户／套餐切换、重置、下降修正、跨午夜和长期未采样分别处理，不拿 token 数换算账户额度。历史不会从本次累计百分比反推，也不会自动取得 Windows 的本地历史。

回归包括真实可用字段投影、重复到期日期、缓存、可选请求失败、无历史／零增量、重置／修正、账户变化、午夜／长间隔和损坏文件保护。构建／CI 与当前设备验收结果分别记录，未通过实机前不宣称完成验收。

## English

The reported missing seven-day usage and incomplete credit details are omissions in the Mac bridge. The existing Windows producer and running `.234` dashboard consume `quotaTrend.days` and `quotaDetails[].creditExpiries`; Mac supplied neither. This patch fills existing fields without changing firmware or Windows.

Credit expiry dates use the status timezone and retain duplicate dates for distinct credits. Actual provider window durations survive cache/fallback; unknown or invalid durations remain unknown. Optional credit request failures no longer discard successful usage.

Mac keeps bounded, display-only observations in the application-support file above, with a hashed account discriminator and no credentials, response bodies or conversations. It retains 90 days/up to 140000 rows, preserves corrupt files, and reports persistence warnings. The seven-day chart follows Windows' observed-increment contract in Asia/Shanghai, measured in weekly percentage points. Unknown days are not zero; current/incomplete days are partial. Account/plan changes, resets, corrections, midnight and long gaps are handled separately. It does not infer historical usage from current totals/tokens or import another host's history.

Regression coverage includes projection, duplicate expiries, persistence, optional-request failure, unknown/zero history, resets/corrections, account boundaries, midnight/gaps and corrupt-file preservation. Build/CI evidence and physical acceptance are recorded separately.

## 实机验收 / Physical acceptance

`d93c318` 的 Mac CI／完整附件通过 171 项测试。用户确认重置卡信息完整；两次相隔 120 秒的真实采样后，当日零增量与前六日未知显示正常，后续当日数值也随新采样增加。卡片、未知／零值区分和动态累计显示均已实机确认。本记录不保存实际账户用量；过去未采集的历史仍保留未知。

Candidate `d93c318` passed Mac CI and full artifact verification with 171 tests. The user confirmed complete credit details, today's observed zero versus earlier unknown days after two real samples 120 seconds apart, and subsequent increase of today's accumulated value. Card details, unknown/zero distinction and live accumulation passed physical acceptance. Actual account usage values are not retained here; unobserved history remains unknown.
