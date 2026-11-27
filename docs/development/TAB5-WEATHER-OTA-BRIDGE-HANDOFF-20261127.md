# TAB5 天气实况与升级草稿交接

## 范围和源码身份

- Windows 发起，macOS 接收。两端已有对应实现。工作树从主干 `013750ac2baf124ac4b3ac3e52db1ceb0c0aa39a` 提取已经联调的相关改动，实际源码以包含本交接的双端 PR 提交为准。此次仅提交并合入源码。后续 Windows 和 Mac 从同一候选源码构建，在两端验收后统一发布。
- 适用于支持相应字段和草稿保护的 TAB5 固件，包括已公开的 0.2.324-ui。兼容字段和行为见[天气实况](../PROTOCOL.md#tab5-天气实况补充--additional-live-weather-fields)与[升级前保留草稿](../PROTOCOL.md#tab5-升级前保留草稿--draft-preservation-during-ota-preparation)。协议版本仍为 1。

## 对齐和验收

### PM2.5 单项分指数

- Windows 和 Mac 将同一提供商响应的浓度、单项分指数及标准一并传给 TAB5。总体 AQI 独立显示。和风按实际标准选用数据，Open-Meteo 读取 `us_aqi_pm2_5`。
- 使用和风样例 `19 µg/m³`、分指数 `28`、总体 AQI `42` 检查实况中浓度、等级和总体 AQI；再移除或换错单项标准，确认只省略对应等级。已有 Windows 本地联调验证真实和风等级显示。Mac 已有实现与离线夹具，接收端需复验实机。

- macOS 接收端：已对齐本地实现。`WeatherForecastTests.testPM25MissingConcentrationDoesNotReuseOldGrade` 验证缺少浓度时不沿用旧单项等级；总体 AQI 独立，保留可用读数。实体设备仍待验证。

待验收：macOS｜和风与 Open-Meteo 的等级标准、缺失分指数降级及 TAB5 实况显示

### 三小时气压变化

- 用同地点、同来源、同口径的 `1013.2 → 1012.0 hPa` 样本验证 TAB5 显示「近 3 小时　下降 1.2 hPa」。切换地点或气压口径，或丢失有效观测后仅显示可用当前读数。两端的地点标识和历史均留在内存。
- Windows 本地候选经配对 USB 测得当前站点真实气压与受控的下降 1.2 hPa 显示。历史自然积累和最终安装包另行验收，Mac 已有等价实现和样本匹配测试。

待验收：Windows｜最终候选连续采集满三小时后复核真实气压变化

- macOS 接收端：已对齐本地实现。`WeatherForecastTests.testPM25SubIndexAndPressureTrend`、`testPressureTrendNotExposedOnDisconnectOrCacheRestore` 验证同口径趋势及断线、重启后不显示旧趋势。 独立审核后补充 `testFreshPressureTrendSurvivesPartialAirQualityFailure`，空气质量数据局部失败时仍保留本次有效气压趋势，气压本身缺失才隐藏。

待验收：macOS｜确认真实气压变化及地点与口径切换后正确降级

### 消息待核对送达

- 在电脑和 TAB5 各保留隔离的 Codex、Claude 草稿和附件。使用同一原请求分别验证已确认、明确终态和未知回执。已核对且草稿安全保留时可通过升级准备，未知时保持「待核对送达」拦截。整个过程核对发送、追加、人工确认、清空动作均为零。
- Windows 和 Mac 保留各自加密日志，配对及请求身份沿用。Windows 日志回归覆盖原回执恢复和草稿保留。早期 TAB5 R2 曾在保留原配对、经只读核对空草稿后完成 OTA；该记录不代表本次合入源码的最终原包验收。

待验收：Windows｜使用最终候选核对 Codex、Claude 原回执及升级前保留草稿和附件

- macOS 接收端：已对齐明确拒绝的原请求终态，未知仍拦截。`MacTab5CodexTests.testPreserveDraftRejectedReceiptIsFinal`、`testPreserveDraftUnknownThenResolvedForBothProviders`、`testUpgradeReceiptPreservesTextAttachmentsAndPeerDrafts` 覆盖回执、草稿与附件。真实配对升级仍待验证。

待验收：macOS｜核对 Codex、Claude 原回执、未知回退及保留草稿和附件

### 草稿未同步或冲突

- 将同一会话撤出近期列表，检查原机仍可保守保存已有草稿；若电脑端已有不同草稿，收到 `409 draft_changed` 后保留两份内容并继续拦截。空内容只核对电脑端也为空且无未知送达记录。Windows 由本机现有记录确认目录外 Codex 会话身份，非近期草稿保存路径不授权读取或发送。
- 通过已配对连接测试旧桥接回退、两种桌面拒绝提示、重启后的草稿保留和通过准备后启动。保留既有配对和 TAB5 展示设置。

待验收：Windows｜真机核对无冲突与冲突两条升级准备路径及配对和启动状态

- macOS 接收端：已有等价草稿保护；以 `MacTab5CodexTests` 的近期列表外身份、重启、空请求和冲突回归补验。旧桥接和升级提示的本地回归亦纳入 `MacTab5Tests`。不把模拟视为真实 OTA。

待验收：macOS｜真机核对近期列表之外的保存、冲突、旧桥接和升级准备提示

## 发布交接

- Windows Release 构建、`--status-once`、`--self-test-weather-forecast`、`--self-test-weather-location`、`--self-test-codex-tasks`、`--self-test-claude-native-workspace` 和 `--self-test-tab5-remote-ota` 均通过。Mac 原生 CI 运行 `swift test` 与 `swift build -c release`。合入后由接收端补实际安装与 TAB5 结果。
- 过去的 Windows 驻留联调包与本次源码分开记录。原生构建和历史联调不能替代最终发布候选安装与真机验收。此次没有新发布安装包、版本标签或附件。

## Mac 原包安装回填 · 2026-11-27

- 已从双端唯一源码 `32fbf674704b993cc1ef9c0e69cf952247e47482` 构建 Mac 0.6.35 固定签名原包；同一证书及 Bundle ID 沿用公开 0.6.34。ZIP 为 20,006,333 字节，SHA-256 `622769fe7ba33cc55c52320f45a807cddc35548efb4efbe014ee8382a9e58e32`，来源由 `BUILD-SOURCE-macos.json` 绑定。Mac 原生测试 592 项、5 跳过、0 失败，Release 构建通过。
- 原生一键升级夹具使用公开 0.6.34 ZIP 升到该原包，自动重启、隔离配置及两版进程实际读取的辅助功能权限通过。办公室 Mac 的日常安装也已覆盖到同一 0.6.35，正常启动和再启动通过；43 个受检文件未改，53 项原偏好保留，运行缓存与版本检查标记按设计刷新。运行中的系统授权页确认辅助功能及适用的既有权限已授权，未重置系统权限。
- 原配对通过新版进程的本机回环认证；Codex 和 Claude 各一条现有非空草稿读回匹配安装前内容。该检查没有向实体 TAB5 发送、确认或清空草稿。设备中心仍显示 USB 未连接、Wi-Fi 等待、蓝牙扫描，没有新鲜实体设备证据；上面天气、OTA 准备、冲突与附件的真机事项继续待验。
- 原包未公证，Gatekeeper 执行评估拒绝；现有安装正常升级与新的浏览器隔离首装分开记录，后者未通过本轮验收。下一步由 Windows 负责人汇集同 SHA 的两端原包，并处理设备／订阅待验条件后决定晋级；本 Mac 任务不公开发布。
- 原 ZIP、校验文件、来源 stamp、平台验收回执及接手步骤已放入 [GitHub 内部传输草稿](https://github.com/yaoyouzhong/AI-bot/releases/tag/untagged-09fb51e0f5a3d26c830b)，草稿名 `handoff-mac-0635-32fbf67-20261127`。使用有仓库写权限的登录下载；`ACCEPTANCE-macos.json` 尚未绑定双端候选 ID，须由 Windows 装配同 SHA 原包后登记到统一验收，不直接作为 promote 输入。该内部草稿不得直接公开发布。
