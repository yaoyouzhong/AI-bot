# TAB5 .253 与 Windows 额度证据候选

本轮先完成 [.252 验收](TAB5-252-ACCEPTANCE-20261023.md)，再处理已确认的照片蓝闪、BLE 首次解锁失败、额度少算和两处照片提示。用户进一步要求核对重置卡使用记录，并把官方不定期重置纳入累计。Mac 不动；现有 .252 的蓝闪失败记录保留。本候选已按用户授权安装并刷入，尚未提交或推送。后续实测结果见文末。

## 额度累计与真实边界

- 正常桌面复制了原始历史副本，44493 条；副本 SHA-256 `b36ece70591829fc40591204c49cbb462023d9e91188c8f9c24f799dcf336b90`。只读重算前后哈希相同，未改原历史。私有副本在忽略目录，不进入 Git。
- 10 月 20 日 09:42 已用 35%，10 月 21 日 17:57 已用 61%，当晚末次 65%；账户指纹与套餐一致，但两个仍在未来的重置日期改变。旧算法因此丢掉 26 个百分点，只显示 4。用户明确确认该断档未使用重置卡。
- 新算法保留该区间的正向差额，21 日为 **30 个周额度百分点，其中 26 为跨日补记**，仍标部分记录和一次周期变化；这是观测下限，不能断言全部发生在 21 日，也不能排除缺采期间的其他事件。缺失日期不填零，不把部分日纳入完整日均。
- 用户报告 23 日凌晨约 2 点官方重置。实际样本最早在北京时间 **01:09:35→01:10:34** 从已用 **55%→0%**，重置时间由 10 月 29 日 17:46:11 改到 10 月 30 日 01:10:34。同一已知账户/套餐，足以识别一次提前重置边界；触发来源仅凭样本仍未知。
- 零用量持续期间下一重置时间曾随采样滚动。新算法不把这些滚动重复计作重置或待核实增长；副本中 23 日结果为 1 个已记录百分点、一次重置、零次额外周期变化。原有消耗不被清空，恢复的余额不算消耗。断档内未知消耗仍不编造。

## 重置卡与官方自动重置

核对官方说明与本机 Codex 26.1016.8622 的静态源码，确认桌面历史使用只读 `GET /wham/rate-limit-reset-credits/history`，返回 `events[{id,kind,occurred_at}],next_cursor`。桥接此前只保留可用卡数量/到期时间，未保存兑换历史。

- 新版独立请求卡片历史，最多四页、合计四秒。只接纳明确的 `redeemed` 事件及有效时间/ID；把不可逆 ID 指纹和兑换时间保存一次，绑定成功额度响应的账户。领取、过期、余额减少不作为卡片使用证据；不调用兑换接口。
- 已保存的事件能回查相应额度区间。即使断档后的比例高于先前比例，只要存在匹配账户的明确卡片事件与改变的窗口，也按新段第一读数累计，避免只算差值。不同账户、套餐或无效窗口不借用证据。
- `resetCreditHistory` 诊断只提供兑换时间、首次关联采样时间与来源。`resetCreditHistoryStatus` 提供检查时间、完整性及失败类型；成功空列表与接口失败/部分页分开。额度响应先落盘，卡片或历史失败不丢有效额度，已经取得的事件页也保留。
- [官方说明](https://help.openai.com/en/articles/20001498-how-resets-work-in-codex)明确：自动及付费重置不在这份卡片历史中，日期变化本身不等于使用卡。未找到可确认账户自动重置来源的单独接口；本次以账户采样的重置边界分段，来源保持未知。公告或用户提供的大致时间不直接制造账户事件。
- 历史原有字段兼容，仅新观察增加可选事件元数据；不迁移或补造旧事件。安装后已取得真实查询状态和驻留摘要：历史检查完成、无错误，但明确兑换事件为空，原始事件类型未核对；合成响应不作为服务端事件证据。

## TAB5 照片与 BLE

- 照片处理期间原先还要分配并复制一个 460800 字节预览。现在完成上传后将预览所有权交给附件缓存，保留仍在草稿内的另外两张照片；真实生产函数测试验证指针转移、禁止工作中转移、一次性所有权和清理。
- 预览每四行让出执行时间；限定 JPEG 编码器 TX/RX 为 16 字节 DMA 突发，保留原宏块、照片尺寸、quality 85、屏幕时钟和三张附件上限。SDK 补丁校验原状态、幂等及未知/混合状态拒绝。蓝闪根因尚未由真机证实，这些改动只是待验候选。
- 去掉“可与文字一起发送”“返回后可继续编辑或发送”，沿用当前等比例附件查看布局。已查看合成界面图，未读取用户照片。
- BLE 失败记录为 `credential_transport_failed`、尚未 HID 输入。代码存在信箱被普通 RPC 占用就立即拒绝取凭据的路径；新候选仅在首次发布前等待至多一秒，计入原总时限，检查电脑代次、切换及断开。没有重发凭据或 HID，也没有延长六秒凭据有效期；这条缺陷已由生产交换函数测试复现并覆盖，但不能据此声称它是现场唯一根因。

## 构建、交付与边界

- Windows Release 构建通过，零警告/错误；额度历史、采集合成回归和数据源回归通过。额外明确数据自测进入独立自测资料目录，不改正常启动保护。工具直接 `--status-once` 被已有桌面资料目录保护拒绝，未绕过；随后通过正常桌面启动核实实际资料目录、配对一致及新驻留结果，未修改该保护。
- 固件经 `scripts/build.ps1 -LocalArt -LocalVersion 0.2.253-ui` 构建通过，沿用限定核心/BelowNormal 策略。镜像 7175664 字节，既有槽内剩余 33296 字节，未改分区。
- 十个相关 native 可执行测试通过：RPC admission/exchange/mailbox、照片内存/编码/上传分块、JPEG burst/DMA/cache、显示带宽。包括原生产交换函数的 100 次完成竞争与 100 次重连竞争。
- 完整 LVGL 界面、四向照片交接、三张增删生命周期、相机/图库交接回归通过；实际 sidecar 说明已通过校验并预览。11 个源文件的 .252→.253 补丁重放与当前源码一致，见 [补丁](TAB5-FOLLOWUP-253.patch)。
- 固件 SHA-256：`a66bd7cee671b2a306e5e86e216ddd33e3ce02f0e6c2b9412686295a5ee344e3`。
- Windows DLL SHA-256：`769965b1837a744c697476d5dd58fa89e713b94b3b8d3b4bbc7ab07302a9a621`。固定候选副本与 PDB 保存在本任务的 `bridge-candidate/`，不包含用户配置或历史副本。
- 固定固件路径已准备为 `AI-bot/artifacts/firmware/tab5/latest/aibot_tab5.bin`，旧 .252 已保留归档。完整机器证据在忽略目录 `artifacts/development/tab5-followup-253-20261023/PREPARED-253.json`。
- **已安装/刷入并核验**：固定运行 DLL 与上述哈希一致；真实启动核验匹配 `.253` 的版本及 ELF、`ota_0` / `VALID`。用户本次两次 BLE 解锁均成功，设备 attempt 1/2 也记录 success；该项本轮通过。照片添加仍蓝闪，继续列为未通过。驻留 21 日为 30（跨日补记 26），23 日为一次重置；真实历史查询状态 complete=true、error=null，但明确兑换事件为空，不能据此断言没有用卡，原始事件类型尚待核对。一位真实密码继续按用户决定延期。不改变绑定、系统密码、Wi-Fi 或显示偏好；Mac 源码及主干未改动。

## English

The user completed .252 acceptance before requesting consolidated fixes. The installed .253 build keeps the accepted gallery orientation, first camera opening and three-photo behavior; both BLE attempts passed after installation, while blue flashes remain unaccepted. Mac is unchanged; nothing new is committed or pushed.

An untouched 44493-row actual history copy now recalculates October 21 as 30 observed weekly percentage points, with 26 explicitly carried across a long gap and a partial/window-change marker. This is a lower bound with uncertain date attribution, not a full-day bill. Same account/plan and still-future moved deadlines establish comparability of positive differences, not a reset source. The user confirmed no card use in that gap. October 23 samples show 55% to zero between 01:09:35 and 01:10:34 Beijing, with an advanced deadline. One reset boundary is counted; subsequent zero-usage deadline drift is not repeatedly counted. Sampling alone does not prove an official actor.

The installed Codex desktop static contract supplies read-only banked-reset history, at most four pages in four seconds. Explicit redeemed events are saved once under the account with hashed IDs and real event times; grants/expiry/count drops are not redemptions. Successful usage and prior event pages survive optional-source errors. Diagnostics distinguish a complete empty history from failed/partial retrieval. Official help excludes automatic/paid resets from this history; no account-specific automatic-reset event endpoint was established. Those resets use observed boundaries with unknown origin; public announcements never create account events. The installed authenticated history check completed without error but returned no normalized redemptions; raw event kinds are unverified, separate from synthetic contract tests.

TAB5 transfers completed preview ownership rather than copying another full image, paces preview rows and limits JPEG DMA bursts. Requested photo hints are removed without changing viewer layout. Credential reads gain bounded mailbox admission before one publication, preserving total/credential deadlines and host generations without replay. This fixes a reproduced collision path; neither its causal exclusivity nor blue-flash removal is established on hardware.

Release, quota/data-source tests, ten relevant native executables, full LVGL/three-photo/orientation/gallery transitions, eleven-file patch replay and actual sidecar preview pass locally. The tool-launched real-profile status check remains blocked by the existing profile guard and is not claimed passed. Exact payload hashes are above. The authorized .253 candidate is installed and exact boot verification confirms its ELF, ota_0 and VALID state. Both user BLE attempts succeeded and device attempt 1/2 agree. Photo blue flashes still fail. Resident October 21 is 30 points (26 carried), and October 23 has one reset. Live history retrieval reports complete without error but no normalized redemption events; the raw event kinds remain unverified, so this does not prove no card use.
