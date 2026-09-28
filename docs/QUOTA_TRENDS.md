# Codex 额度趋势

Windows 左键镜像窗口底部“额度趋势”打开独立、可缩放的历史窗口，保留原 Claude/Codex、双额度页的显示和设备模式。TAB5 接收近七日摘要，原 ESP8266 协议保持不变。

入口为镜像底部独立整行“Codex 额度趋势”，不与 USB 状态并排挤占空间；右键“模型额度 → Codex 额度趋势…”也打开同一个窗口。
Windows 正常启动时会检查 Codex MSIX 私有缓存中的既有 `.apet` 导入，只恢复当前配置目录缺失且通过解码验证的动画，不覆盖当前选择、不删除原文件，不复制凭据或其他缓存。公开自测不访问此私有来源。

- 驻留桥接启动即采集，随后每分钟采集一次，不依赖当前显示页面。额度响应成功后立即保存；独立的重置卡请求超时、失败或格式异常不会丢掉有效额度记录。
- 统计日为北京时间 00:00（含）到次日 00:00（不含）。单位为周额度百分点，不是 Token 或账单费用。按用户确认的口径，跨零点且不超过五分钟的正常采样增量计入后一次采样的日期，不拆分、不重复计数。
- 默认展示“每日已记录用量（周额度）”：累加同账户、同套餐及可比较窗口内的有效增量。不完整日期仍显示已记录用量；没有可比采样显示 `--`，没有记录显示“未采集”，今天显示“统计中”。同一天内超过五分钟的间隔，如果账户和重置窗口可核实，仍保留非负累计差值，同时标记断档；跨天长断档不归入恢复采集当天。未知账户身份的历史不作为完整日。
- 日均只包含所选范围内的完整统计日，分母明确显示。不显示顶部今日增长或“如何计算”按钮。启用前日期不绘入图表，中间缺失日仍保留为未知。
- 到期重置分段累计内部可观察增量，不加上未使用的剩余额度。由于缺少重置前最后消耗，这种日期暂不作为完整日。无法核实的回落或周期变化显示“额度变化无法核实，未计入”，另起基线，不断言用户手动重置；未来重置时间一分钟以内的舍入抖动允许视为同一窗口，但用量下降仍不累加。重置次数减少可能是过期，不能据此确认消耗。旧记录不回填。
- 完整日允许正常轮询覆盖零点，不要求请求恰好命中 00:00:00；跨零点增量采用上述归属约定。接口返回比例有自身精度，日统计是观测增量，不是供应商每日账单。桥接未运行、电脑休眠或网络/授权故障期间无法凭空恢复历史。
- 五小时额度独立展示每小时最后一次使用率快照，不连线、不与周额度相加。趋势图与数据明细分栏切换；点击柱/点会切换并定位对应明细。重复打开复用同一个窗口。
- 历史保存在 `%APPDATA%\AI-bot\codex-quota-history.json`，保留 90 天，最多 140000 条（容纳每分钟采样）。保存采样时间、套餐、使用率、重置时间及请求账号标识的 SHA-256 指纹；无原始账号标识、Key、Token、Cookie 或会话内容。旧记录缺少账号指纹时保留为不完整，不推断其身份。
- `--quota-history-once` 只读输出七日汇总、最新采样时间及保存错误，不输出账号指纹或凭据，便于核实驻留程序是否持续采集。
- 文件损坏或权限异常时保留原文件并提示，只在内存继续记录，不能让统计存储失败中断桥接。

开发自测采用合成记录和多尺寸窗口截图，不能替代驻留桥接界面验收。桌宠只读诊断 `GET http://127.0.0.1:8765/diagnostics/pets` 返回当前进程的缓存目录、角色加载状态、最近绘制结果和错误类型，不返回动画内容或凭据；它不是另外启动的 CLI 进程的状态。

## English

The mirror's quota-trend button opens a separate resizable Windows history window.
The minimal layout keeps filters, recording times, chart/details, and one short
scope note; no today headline or calculation-help button. Dates before
recording began are omitted, while missing dates inside the period remain unknown.
Statistics use Beijing dates and sum comparable observed increments. Partial days
also appear in daily bars; only closed, verified days with covered boundaries enter
averages. No comparable samples means `--`; missing dates are not zero usage.
Reset days with unobserved consumption, gaps, and unknown identity remain incomplete.
Scheduled resets split observed increments. Unverified drops or window changes are
excluded and labelled as unverified changes, not proof of a manual reset. Future
reset timestamps tolerate up to one minute of rounding jitter, but usage drops do not.
A credit count change alone never proves a quota reset. No unused balance is counted as consumption.
Polling runs every minute, independently of the selected page. Valid usage is saved
before requesting optional credit metadata. Under the user's attribution convention,
short intervals across midnight belong to the later sample's Beijing date. Exact
midnight samples are unnecessary. Same-day, verified account/window differences
survive gaps, flagged as partial; long cross-day gaps are never assigned to one day.
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
