# Codex 额度趋势

Windows 小屏镜像窗口底部“额度趋势”打开独立、可缩放的历史窗口，保留原 Claude/Codex、双额度页的显示和设备模式。TAB5 接收近七日摘要，原 ESP8266 协议保持不变。

入口为镜像底部独立整行“Codex 额度趋势”，不与 USB 状态并排挤占空间；设备中心“账号数据 → 额度历史”也打开同一个窗口。
Windows 正常启动时会检查 Codex MSIX 私有缓存中的既有 `.apet` 导入，只恢复当前配置目录缺失且通过解码验证的动画，不覆盖当前选择、不删除原文件，不复制凭据或其他缓存。公开自测不访问此私有来源。

- 驻留桥接启动即采集，随后每分钟采集一次，不依赖当前显示页面。额度响应成功后立即保存；独立的重置卡请求超时、失败或格式异常不会丢掉有效额度记录。
- 统计日为北京时间 00:00（含）到次日 00:00（不含）。单位为周额度百分点，不是 Token 或账单费用。按用户确认的口径，跨零点且不超过五分钟的正常采样增量计入后一次采样的日期，不拆分、不重复计数。
- 默认展示“每日已记录用量（周额度）”：累加同账户、同套餐及可比较窗口内的有效增量。不完整日期仍显示已记录用量；没有可比采样显示 `--`，没有记录显示“未采集”，今天显示“统计中”。同一天内超过五分钟的间隔，如果账户和重置窗口可核实，仍保留非负累计差值，同时标记断档；跨天长断档接续前一次有效记录，将可核实的差值补记到恢复采集当天，并标明“含跨日补记”；不据此推定消耗实际发生在哪一天。没有记录的日期仍保持未知。未知账户身份的历史不作为完整日。
- 日均只包含所选范围内的完整统计日，分母明确显示。不显示顶部今日增长或“如何计算”按钮。启用前日期不绘入图表，中间缺失日仍保留为未知。
- 到期重置、可观察的提前重置及有明确兑换记录的重置均分段累计，不加上未使用的剩余额度；不会仅凭观察到重置就称用户用了卡。重置日期变化但用量未下降时，同一已知账户/套餐且两个日期仍在未来，保留差额作为观测下限，标为周期变化待核实；跨日长断档仍标明补记。无法核实的回落不填补，另起基线。未来重置时间一分钟以内的抖动可视为同一窗口；重置后 0% 状态的日期持续滚动，不重复计次。
- Windows 通过只读 `rate-limit-reset-credits/history` 采集明确的 `redeemed` 事件，保留事件时间和不可逆 ID 指纹，仅在首次成功接收的同账户额度记录上保存一次。最多四页、合计四秒；可用卡列表或历史接口失败不丢有效额度和已取得的事件页。`granted`、过期、卡数减少不算已使用；只用明确事件核对相应采样区间，未知事件不猜测。官方自动/付费重置不在这份卡片历史内；没有账号级事件时只能根据已用比例回落及窗口改变识别重置边界，触发来源保持未知。官方公告不是账户重置证明。
- 完整日允许正常轮询覆盖零点，不要求请求恰好命中 00:00:00；跨零点增量采用上述归属约定。接口返回比例有自身精度，日统计是观测增量，不是供应商每日账单。桥接未运行、电脑休眠或网络/授权故障期间无法凭空恢复历史。
- 五小时额度独立展示每小时最后一次使用率快照，不连线、不与周额度相加。趋势图与数据明细分栏切换；点击柱/点会切换并定位对应明细。重复打开复用同一个窗口。
- 历史保存在 `%APPDATA%\AI-bot\codex-quota-history.json`，保留 90 天，最多 140000 条（容纳每分钟采样）。保存采样时间、套餐、使用率、重置时间及请求账号标识的 SHA-256 指纹；可选 `ResetCredits` 仅保存明确使用事件的时间和 ID 的 SHA-256，不保存原始事件 ID。无原始账号标识、Key、Token、Cookie 或会话内容。兼容既有记录，不重写或补造过去的事件。
- `--quota-history-once` 只读输出七日汇总、最新采样时间及保存错误，不输出账号指纹或凭据，便于核实驻留程序是否持续采集。
- `--quota-history-from-file <codex-quota-history.json 副本>` 在隔离自测环境中只读重算指定副本，不访问桌面资料目录。驻留诊断 `resetCreditHistory` 仅给兑换时间、首次采集时间和来源；空列表不证明未发生官方自动重置。
- `resetCreditHistoryStatus` 明确给出本次检查时间、分页是否完整及失败类型；接口不可用、超时或只取到部分页，与成功取得空历史区分显示。它不表示自动重置事件的覆盖情况。
- 文件损坏或权限异常时保留原文件并提示，只在内存继续记录，不能让统计存储失败中断桥接。

开发自测采用合成记录和多尺寸窗口截图，不能替代驻留桥接界面验收。桌宠只读诊断 `GET http://127.0.0.1:8765/diagnostics/pets` 返回当前进程的缓存目录、角色加载状态、最近绘制结果和错误类型，不返回动画内容或凭据；它不是另外启动的 CLI 进程的状态。

## English

Device Center → Account Data → Quota History, or the mirror's quota-trend button, opens a separate resizable Windows history window.
The minimal layout keeps filters, recording times, chart/details, and one short
scope note; no today headline or calculation-help button. Dates before
recording began are omitted, while missing dates inside the period remain unknown.
Statistics use Beijing dates and sum comparable observed increments. Partial days
also appear in daily bars; only closed, verified days with covered boundaries enter
averages. No comparable samples means `--`; missing dates are not zero usage.
Reset days with unobserved consumption, gaps, and unknown identity remain incomplete.
Scheduled and observed early resets split increments without asserting who triggered them.
For known matching accounts/plans, nondecreasing usage across changed future deadlines
retains its observed difference as a lower bound, marked partial with a window-change
warning. Unverified drops remain excluded. Future reset timestamps tolerate one minute
of rounding jitter; idle zero-usage deadline drift does not create repeated resets.
A credit count change alone never proves a quota reset. No unused balance is counted as consumption.
Polling runs every minute, independently of the selected page. Valid usage is saved
before requesting optional credit metadata. Under the user's attribution convention,
short intervals across midnight belong to the later sample's Beijing date. Exact
midnight samples are unnecessary. Same-day, verified account/window differences
survive gaps. Long cross-day gaps carry a verified difference into the resumed
collection date, labelled as carry-over and excluded from complete-day averages.
This attribution does not claim the actual calendar date of consumption; missing
dates remain unknown. Null weekly observations can be skipped only within a
verified account/plan. Scheduled or evidenced manual resets split the increments;
no unobserved pre-reset consumption is invented.
Windows also reads the installed Codex desktop history contract, GET
`rate-limit-reset-credits/history`: at most four pages within four seconds. Only
`redeemed` events with a valid timestamp/ID are retained once for the account,
as optional `ResetCredits` on the first successful usage observation. IDs are hashed;
raw IDs and credentials are never stored. Grants, expiry and count drops are not
redemptions. History/credit failures preserve valid usage and prior event pages.
Explicit events resolve a changed-window interval even if resumed usage exceeds its
old reading. Automatic/global and paid resets are absent from banked-reset history;
without account-specific events, observed usage drops plus changed deadlines establish
a reset boundary whose origin remains unknown. Public announcements are not account proof.
Local diagnostics expose event time, first observation time and source only.
`resetCreditHistoryStatus` distinguishes complete empty history from unavailable,
timed-out or partial pagination; it never establishes coverage of automatic resets.
`--quota-history-from-file` recalculates an explicitly selected history copy read-only
inside the public self-test profile, without accessing the desktop profile.
These are observed percentage-point increments at the API's precision, not daily
billing records. Five-hour observations remain hourly-last snapshots. History retains a
SHA-256 account fingerprint, never raw identifiers or credentials; older records
without identity remain incomplete. Data stays local for 90 days/140000 rows.
No historic data is fabricated and damaged files are preserved. Local tests do not
replace deployed UI or real-account acceptance.
Chart and detail tabs share the selected record; repeated opens reuse one window.
The local-only `/diagnostics/pets` route reports resident pet-load/draw metadata,
never sprite contents or credentials.
The full-width mirror footer and the Model quotas tray menu both open the same
trend window. Normal Windows startup recovers missing validated APET imports from
the Codex MSIX cache without overwriting selections or deleting originals; public
self-tests never access that private source.
