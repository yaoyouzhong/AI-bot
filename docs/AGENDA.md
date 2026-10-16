# 日程与待办 / Agenda and reminders

## 204 当前候选规则 / Current candidate behavior

2026-10-16 用户进一步明确：过期事项先保留，只有已有 16 条且需要新增时，才清理最早结束的一条过期单次事项，腾出一个位置。第 16 条新增、普通编辑、读取、到期和重启均不触发清理。结束相同时按 ID 排序；旧记录无结束字段时按开始时间判断过期。没有可清理项时拒绝新增，保留输入；重复规则、无时间待办及未结束事项不参与自动清理。不另存日程历史或备份。203 已按此前批准的到期清理规则移除了两条原记录，本候选不自动恢复或重建它们。

TAB5 本地优先，离线增删改和已校准 RTC 提醒不依赖电脑；电脑提供辅助编辑及后台同步。未校时不自动清理；满额清理与新建作为一次原子写入，失败时保留原记录和输入。新建过去事项会提示调整时间，已保留的过期事项可以编辑。“今日”只显示待处理事项；“全部”显示保留的已结束事项，并标明状态。名称语音识别仍需电脑，离线可用触屏键盘；工作日仍依赖已缓存的官方年度安排。

手动删除与满额清理共用最多 16 条待同步删除记录；同步取得精确回执后清除，防止电脑旧副本恢复已删除项。队列满时提示先连接电脑同步，保留原列表和新增输入。同步协议增加 `retentionSupport=2`；204 设备拒绝采用缺少该能力的旧桥接响应。电脑合并离线新增超过容量时，只清理补足空位所需的旧过期项，保护本批离线编辑和并发修改副本；丢失回执重试不额外清理。过期的离线编辑同样需要精确确认，不能因过期而直接丢弃。

202 已安装并通过精确 ELF／OTA VALID／心跳，但实际日程删除、屏保设置保存及 Wi-Fi 连接验收失败。真实备份中复现 5,872 字节封套超出 NVS 可用空间；203 在同一个 `agenda_v3` 键按实际内容无损编码，最多 3,574 字节，能读旧 4,272 字节文档和 202 封套，启动时先整理旧格式释放设置空间。分区和电脑 JSON 不变；旧固件不能读新格式。既有升级前 NVS 备份仍保留，程序不新增日程历史备份。

203 已安装并通过启动、自动 Wi-Fi 连接、名画设置保存、日程新建、桥接关闭后重启保留及离线删除的实物验证；删除补同步和最终轮播收尾待完成。204 的保留规则尚未部署，仍需对应候选批准。存储修复证据见 [203 修复记录](development/TAB5-AGENDA-203-20261016.md)。下列版本记录是历史行为，以上规则以 204 为准。

204 retains elapsed entries until an insertion into a full 16-item list needs
one slot. It evicts the oldest-ended one-off, with ID as a stable tie-breaker;
recurring, undated and future entries are protected. Reads, expiry, edits and
reboots never prune. Eviction and insertion commit atomically, including a
durable deletion receipt. A full receipt queue or no eligible victim rejects the
insertion without losing records. All shows retained ended entries; Today shows
pending entries. Both peers advertise retentionSupport=2, and 204 refuses older
host snapshots. Batch sync protects incoming edits and reclaims only required
slots; lost receipts are idempotent. The compact NVS format remains unchanged,
with no new history backup. 203 is installed and passed the listed physical
checks; 204 deployment and capacity-policy hardware acceptance remain pending.
## 既有版本记录 / Earlier version history


2026-10-15 已在本机安装桥接 0.6.7 与 TAB5 0.2.197-ui，镜像指纹、OTA VALID 状态和连续显示心跳核验通过；这不代表日程触屏、声音及三通道全部验收。收到大疆音源干扰反馈后，先停用并隔离，随后已在本机桥接安装语音热修复并重新启用：日程与电脑共用当前大疆麦克风，内置麦克风才临时切换音源并自动恢复；不再按豆包版本白名单阻止升级。用户已在拔掉 USB 后用大疆录音并结束，确认“回填正常，电脑仍用大疆”；内置麦克风切换及恢复仍待真机验收。编辑页左侧及键盘语音区另行优化，应用入口缩为“日程”，仍未刷入设备。详见 [197 验收与音源隔离记录](development/TAB5-197-ACCEPTANCE-20261015.md) 和 [语音热修复记录](development/TAB5-VOICE-MICROPHONE-RECOVERY-20261015.md)。

## 使用范围

### 0.2.200-ui 已安装历史行为 / Previously installed behavior

200 已按具体镜像授权在本机安装，精确 ELF、OTA VALID 及连续心跳通过，原轮播和日程文件保持不变；新版触屏与耳听仍待现场确认。详见 [200 部署记录](development/TAB5-AGENDA-200-20261015.md)。The approved 200 image is now installed locally with exact ELF/VALID/heartbeat verification; touch and listening remain separate acceptance items.

新版 TAB5 编辑页取消“日程时间”开关，新建默认有开始时间，已有无时间事项保持原语义。名称右侧固定语音按钮；右侧仅显示开始时间和持续时长，不再显示预计结束。30 分钟／1 小时／2 小时可直接选择，点击“自定义”按需打开时间设置，后台继续保存结束时间并自动结束。新增提前 1／3 分钟；“工作日”按中国官方假期及调休补班计算，与“周一至周五”分开。后文旧版本行为仅作历史说明。

桥接复用既有官方年度假期数据，设备单独持久保存最多三个年度的紧凑日历，2026 年具备内置已公布安排。年度未知时显示“工作日安排待更新，提醒暂停”，不猜测为周一至周五；同步取得年度安排后恢复计算。原事项 `agenda_v3` 的 4272 字节布局不变，年度缓存使用 `agenda_days_v1`，无需迁移用户事项。

The 200 candidate removes the device time toggle, defaults new entries to timed, preserves existing undated items, places dictation beside the name, and shows start/duration without an end row. Custom timing opens on demand; the stored end still controls expiry. It adds 1/3-minute reminders and Chinese official workdays separately from Monday-Friday. Up to three annual calendars persist under a separate key, with built-in published 2026 data. Unknown years explicitly pause reminders until updated; the existing item blob is unchanged. Earlier behavior below is historical. Local previews and scheduler tests do not establish device installation or physical acceptance.

- 电脑端入口：设备中心 → TAB5 → 日程待办。添加、编辑、删除事项；设置开始和结束时间，对已到提醒时间且距离结束超过 10 分钟的事项，可手动延后提醒。窗口每秒刷新到期状态。
- TAB5 入口：应用 → 日程（已安装 197 仍显示“日程待办”，短名称随下次固件更新）。顶部只显示开始时间、名称和距离开始的时间／进行中状态；结束时间在编辑页设置，用于自动结束；今日／全部时间轴可滚动，以圆点显示状态。选中事项后在底部处理；正常同步提示、重复状态和计数已精简。到点显示独立提醒，只有“10 分钟后再提醒”和“知道了”；没有手工完成入口。到结束时间自动收起该提醒并移出未结束列表。
- 每个电脑配置最多 16 项，名称最多 32 个字符。可创建无时间待办，或带日期时间的日程；支持不重复、每天、周一至周五和每周。周一至周五不额外排除法定假日或包含调休补班。
- 可选择准时或提前 5／10／15／30／60 分钟提醒。提前提醒相对开始时间计算，默认准时。到结束时间自动结束本次，重复日程转到下一次；结束后不补响过期提醒。单次支持跨午夜，最长 24 小时，结束须晚于开始。无时间待办不响铃、不自动结束，保留到用户删除或补充时间。
- TAB5 可直接新建和编辑名称、开始／结束时间、重复及提醒，中文拼音键盘可离线使用；保存先写本机 NVS，再同步电脑。名称支持语音输入：优先与电脑豆包共用已选择的大疆；开始时大疆不可用则使用 TAB5 内置麦克风。识别文字保留在独立草稿，点击结束录音后回填名称，再手工保存。取消、失败、跨会话结果或超过 32 字时保留原名称。语音识别复用已启用的 Windows 语音桥接，离线仍可使用触屏键盘。
- TAB5 编辑页左侧填写名称及语音输入，右侧显示开始／结束两行时间。点击时间行展开月历和时／分滚轮，可点今天／明天；取消保留原时间。修改开始时间自动平移结束时间并保持原时长；可直接选择 30 分钟／1 小时／2 小时。结束早于开始或超过 24 小时时留在选择器中提示修正。
- 后续本地候选复用 Codex 的“改用 TAB5”入口：仅大疆录音时显示，先保留前段已提交文字再切内置麦克风，最后一起回填；内置段用完归还豆包原音源。接收器在线但发射器关闭／静音不等于端点不可用，需要此手动入口。配套桥接和新固件均尚未部署，详见 [手动切换记录](development/TAB5-VOICE-SOURCE-SWITCH-20261015.md)。
- TAB5 提醒，电脑和设备均可管理事项；没有接入 Outlook／飞书／云日历，也不额外发电脑端日程气泡。

## 离线、保存与同步

事项保存到电脑的独立 `agenda.json`。同步副本和离线操作保存到 TAB5 原有 NVS 的独立 `agenda_v3` 键，完整记录为 4272 字节（未部署的旧预览键保留，不覆盖），不要求 TF 卡。新增数据不改写原生日、轮播、图库或 Codex 草稿。

现有“配置迁移”的白名单不包含日程；迁移电脑时需另行保留本机 `AI-bot/agenda.json`，不能把现有设置导出文件当作日程备份。

设备需保持供电，并至少从已配对电脑校准一次 RTC。校准后，已同步事项在断网、电脑退出桥接或关机时仍可计时提醒；未校时／RTC 无效时明确提示，不猜测日期。设备完全关机不响铃。本机更换时区后需重新连接校时。

结束状态直接由已保存的结束时间和当前时钟计算，不需要手工标记、网络或额外 Flash 写入。延后先在设备持久化，提醒弹窗等对应操作成功回执后收起；保存失败保留弹窗和错误信息，可明确重试；到结束时间仍自动收起。电脑恢复连接后，通过原有加密 RPC 回传；重复请求不会再次完成或取消较新延后。电脑和设备修改定义均生成新编辑版本。设备连续离线编辑保留原始 baseEdit。若两端同时编辑或电脑已删除，电脑保留原版本，并用设备编辑版本号作为确定性的副本 ID 保存设备版本；重复同步不重复建副本。电脑列表已满、旧桥接不支持设备编辑或确认保存失败时保留设备原数据和待同步状态。旧版离线操作不覆盖改期后的事项。删除或改期须在设备连接同步后生效；离线期间设备继续使用已保存副本。损坏文件、NVS 读写失败和未确认同步均明确报错，保留最近成功数据。

提醒使用现有声音通道，尊重音量和静音，录音期间延后呈现。知道了仅收起本次提示，不改变结束时间。“10 分钟后再提醒”从点击时刻起计时，不会自动循环；若延后时间等于或晚于结束时间则不允许延后。早期未部署预览中缺少 `end` 的记录按开始时间结束，编辑保存后补齐结束时间。

## 本地验证与待验范围

证据目录：`artifacts/agenda-20261015/`。

- Windows Release 构建和候选 `--status-once`；业务测试覆盖原子保存、重新加载、重复完成、改期冲突、工作日／每周、跨午夜提前与延后、损坏文件保留。
- 实际 HMAC／AES-GCM RPC 调度入口分别使用 USB、Wi-Fi、BLE 参数验证；包含非法认证和 nonce 重放拒绝。此项不等于真实三通道传输。
- 90 组相同 JSON 输入交由实际 C 固件规则和 C# 规则核对。生产设备 worker 使用 NVS、RTC 和传输替身验证重启去重、离线新建／连续编辑及延后、旧桥接保护、缺少确认保护、同步重试和保存失败保留；替身不等于真实 Flash／RTC 验收。
- Windows 当前显示器 175% 下检查原生编辑控件字高；标准／最小窗口的 100%／150%／200% 字体及几何使用独立测试进程模拟，不修改系统 DPI。真实多显示器 DPI 切换仍待验。
- TAB5 原生 LVGL 预览覆盖时间轴、选择、进行中、延后边界、自动结束收起弹窗、丢弃过期提醒、空状态及未校时；另跑现有页面回归。ESP-IDF 构建通过不等于真机通过。
- 新建／编辑预览验证日期网格和分钟滚轮的实际 LVGL 指针输入、今天／明天、取消不改草稿、开始平移保持时长、跨日结束校验及标签字库覆盖。语音识别使用替身验证回填、取消、失败、超长与 owner 隔离；用户已验收无线大疆录音回填及电脑音源保持，内置麦克风路径仍待验。

后续刷入新候选仍需对应授权、独立版本、实际更新说明与可回滚包。剩余日程真机验收包括电脑离线／关机、RTC 校准、声音耳听、USB／Wi-Fi／BLE 同步、操作断线恢复；原有轮播已恢复。当前 `.194` 书法竖屏偶发卡死另案保留，不因本功能测试通过而认定修复。语音与电脑轮流共用豆包输入法，不支持两个识别会话并行；电脑正在录音／识别时拒绝接管。大疆在录音中断开会结束本轮、保留草稿，重新开始时可使用内置麦克风，避免在同一豆包录音期间切换音源。

## English

Bridge 0.6.7 and TAB5 0.2.197-ui were installed locally on 2026-10-15; exact image identity, OTA VALID state and advancing UI heartbeats passed. Complete touch, audio and transport acceptance remains pending. After initial isolation of DJI interference, a local bridge hotfix was installed and voice re-enabled: agenda and desktop now share the already-selected DJI endpoint directly, and only built-in TAB5 capture borrows/restores the microphone channel. There is no Doubao version allowlist. With USB unplugged, the user confirmed successful DJI dictation, final name insertion and an unchanged desktop DJI selection. Built-in microphone borrowing/restoration still needs physical acceptance. The separate left-column/keyboard voice UI and shortened app label “日程” remain unflashed. See the microphone-recovery development record for the exact hotfix hash and validation scope.

Windows and TAB5 both manage up to 16 dated or undated items. The device can create/edit locally with an independent Chinese IME. Optional name dictation prefers the same DJI endpoint selected in desktop Doubao, falling back at start to the built-in TAB5 microphone through the enabled Windows bridge; final text stays in the name draft until explicit save. Cancellation, errors, overlong text and other voice owners preserve the original name. The desktop IME supports alternating sessions, not concurrent dictation: busy desktop capture is refused before focus or audio-channel changes. A mid-take DJI disconnect ends that take and preserves text; restarting can choose the built-in microphone. Offline keyboard editing remains available. TAB5 shows only start times (the configured end remains internal to automatic expiry) and provides a time-first agenda, a scrollable Today/All timeline, automatic ending at the configured end time and explicit ten-minute snooze, and local reminders. Repeats are none, daily, Monday–Friday or weekly; statutory holiday adjustments are not applied.

The redesigned editor separates name entry from two compact time rows. Tapping a row opens a month grid and hour/minute rollers with Today/Tomorrow shortcuts. Cancel preserves the draft range. Moving the start preserves the duration and shifts the end; 30-minute, one-hour and two-hour duration shortcuts are available. Invalid end times remain in the picker for correction. Native pointer tests exercise date selection and roller dragging; preview label glyphs are checked. Physical wireless DJI dictation passed; built-in microphone restoration remains pending.

An uninstalled local candidate reuses the Codex “改用 TAB5” source switch during DJI capture. The paired bridge commits the first segment before borrowing the built-in channel and returns the combined draft after explicit stop, then restores the prior desktop microphone. An online receiver with a muted/off transmitter still needs manual switching. The new bridge/firmware pair remains undeployed; see the source-switch development record.

Reminders are relative to the start; dated items end automatically at the end, including overnight events (up to 24 hours). Repeats then advance; expired reminders do not ring. Undated items remain until deleted or assigned a time. Snooze is a single user action and must remain before the end. No manual completion button is shown.

The paired host saves an independent JSON file; the device persists an NVS copy under agenda_v3 and pending actions without requiring microSD. A powered device with a previously calibrated, valid RTC can remind while the host is offline. Powered-off alarms and external calendar integrations are outside this version. Concurrent definition edits are preserved as two copies using a deterministic conflict-copy ID, with idempotent retries. Unsupported or full hosts retain pending local definitions. Edited definitions receive a new edition; stale offline actions cannot overwrite them. Corrupt data and failed persistence preserve the last successful state.

Evidence distinguishes encrypted RPC dispatch, cross-language scheduler fixtures, mocked production-worker persistence tests, native previews and ESP-IDF builds from physical hardware acceptance. Windows monitor-DPI font checks and isolated 100/150/200% font/geometry simulations do not establish real multi-monitor DPI transitions. Installation requires separate authorization and versioned rollback materials; the unrelated portrait-freeze issue remains unresolved.
