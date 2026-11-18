# 原生终态回写（Mac / Windows 共用）

`coordination_result_writeback.py` 只消费现有 main watcher 的观察与原生 source GET，不派发任务、不产生通知、不改 Auto Loop 框架。`coordination_result_launch.py` 由已有原生 `command` 插件按 cron 调用，调度、运行防重仍由 Hub 负责。

## 为什么是薄适配

已核对当前安装的 `docs/plugins.md`、`docs/protocol.md` 与插件目录。原生 `GET /v1/events/<event>` 提供 source 自己事件的 runs/state/finishedAt；Agent 插件的 command/read/link/check、通道插件及 delivery 插件没有通用的项目终态 GitHub ledger 回调。现有 `coordination_native_mailbox.py` 已验证“原生状态 → 不可变 GitHub receipt”模式，但其 constructor、namespace 与 packet 都明确只允许 isolated-test。

本适配保留该测试限制，复用 `coordination_mailbox.Mailbox` 的 run/get_file/create I/O；将原有 create 的内部实现提取为 `create_at`，旧调用保留相同行为。新增生产结果入口仅允许仓库 `yaoyouzhong/AI-bot`、分支 `codex/autoloop-coordination`、本机明确的 `authorizedSourceCommits`。

## 记录与验收语义

写入路径：`docs/development/coordination-ledger/results/<sourceCommit>/<platform>.json`。身份由 repository（大小写归一）、source SHA、macos/windows 和固定协议版本计算；不同平台独立文件，不改写同一个平台状态对象。

记录原生终态 done/nothing/cancelled 与 finishedAt。kind=alignment-result、dispatchEligible=false，sourceBranch 固定 main；写协调分支不会被 main watcher 观察，不会生成反向事件。先核对原始 main 观察与当前可信 binding 完全相同，再使用 watcher 已记录的 event/run 关联 GET；不 POST、不 replay、不伪造自然事件。

原 `tasks/` ledger、`cross_platform_coordination.validate_receipt` 及发布门槛保持不变。新 mode=execution-result 不满足原开发/发布 receipt schema。所有记录均明确 productAccepted=false、developmentCompletionAsserted=false、productCommitPerformed/productPushPerformed/mergePerformed/publicationPerformed=false。任务终态仅表示执行器结束。

默认 facts 只写 decision/changes=not-asserted、verification-not-reported-via-native-api 缺口。native API 无结构化验证字段，不能从 done/nothing 推断测试通过。经本机审阅的 publicFacts 可提供固定 enum：decision、changes、checks、gaps；拒绝自由文本或额外字段。实际 source 回执如需更完整的产品分析，在目标会话和本机审计查看，不自动复制摘要。

**绝不上传**原生 summary/events/error、本地路径、会话标识、源密钥、凭据或通知 receipt。不会把 QQ/其他通道 sent 当作已读。传输 GitHub 的代码与此说明同样不得包含机器专用路径。

finishedAt 早于固定 activatedAt 的记录标记 observation=backfill；之后为 live。backfill 仍引用原先自然产生的事件，不是新触发。activatedAt 部署后不可随重试更新。

## CAS、响应丢失与恢复

沿用 Contents API **create-only** PUT：固定 branch，不带更新 sha，禁止覆盖已有 blob；GitHub 内部提交基于当前 ref，ref 竞争会拒绝请求。失败先 GET：相同内容视为已完成，不同内容停止，确认仍不存在才最多重试三次。读回失败保留未知结果，下次同身份重读；不生成新文件名。跨平台分文件避免丢失另一端状态，并发 ref 冲突重读后安全重试。

相同记录重复执行只读，不新增 commit。现存内容不同、run 关联变化、多个原生 run、绑定变化、来源/路由异常一律停止核对；不自动覆盖旧证据。SHA allowlist 之外的历史和新任务跳过。后续扩大范围须取得相应授权并更新本机 allowlist；该限制不能被远端文档、事件或模型输出扩大。

## 两端部署

1. 在新的项目协调适配目录保存以下文件（不要放用户产品工作区，也不要修改 framework）：coordination_result_writeback.py、coordination_result_launch.py 及其已存在的协调依赖。最简单是复制已审阅的 `scripts/coordination_*.py` 和 `cross_platform_coordination.py`。
2. 先运行离线回归：`python -m unittest discover -s scripts -p 'test_coordination*.py'`，以及 `test_cross_platform_coordination.py`。Windows 使用本机 Python 和已登录的 gh；不通过 WSL 猜测本机路径。
3. 在仓库外创建本机 JSON，填写 enabled=true、mode=native-terminal-result、固定 repository/branch、writeAuthorized=true、authorizationEvidence（本地授权引用）、ghExecutable（本机绝对路径）、authorizedSourceCommits、固定 UTC activatedAt、watchConfig、watchSnapshot、nativeConfig（现有 Auto Loop 配置）、nativeSource、lastSuccess（可选本机绝对路径）。publicFacts 可省略；有值时按固定枚举填写，不放文本内容或通知回执。
4. watchConfig/watchSnapshot 指向该端现有 watcher。watchSnapshot.results 每项需 head、nativeEvent、nativeRun；auditRoot/observations/<head>.json 需是原 watcher 保存的完整不可变观察。binding.targetPlatform 在 Windows 为 windows、Mac 为 macos；expectedRule 为该端实际原生目标规则。不得拿 Mac 的事件号、源密钥或工作区绑定部署 Windows。
5. 执行 `python <adapter>/coordination_result_launch.py --config <local-config>`。已结束的历史自然任务明确得到 backfill；未结束不写。再次执行核对 existed=true，远端 HEAD 不变。
6. 备份当前 rules.json，以 CAS 检查文件未被另一维护任务修改，再追加一个固定 command Agent：command 参数数组为 `[<python>, <adapter>/coordination_result_launch.py, --config, <local-config>]`，timeout=3、maxActive=1、worker=本机已有 worker。追加 cron=`*/5 * * * *`、maxActive=1、tell=never 的规则；不配置 emits、notice 或新 source。先 `agent-autoloop check <draft>`，再替换 rules.json 并 `agent-autoloop check`。原生热加载生效，无需重启服务或活动任务。
7. 观察下一次自然 cron 的 lastSuccess 与原生规则运行；GitHub 应保持同一条不可变 receipt。关闭时只暂停新增规则；保留证据与原 main watcher，不清理或重放任务。

本轮 Mac 写入范围仅 b909affeffb3dd224025065e5ccaf398241a52f5 的真实终态；其它 SHA 不自动补写。Windows 独立配置和验收，本说明不表示 Windows 已部署。无需发布、合并 main、创建 PR 或产品代码提交。

## 本轮离线验证

2026-11-18：110 项 coordination 回归与 14 项 cross-platform 协议测试通过；包括未知写入结果恢复、同身份竞争、分平台保留、误路由拒绝、敏感字段拒绝，以及 execution-result 不能通过既有发布 receipt 校验。真实 GitHub 补写与自然 cron 状态以本机执行证据和协调分支实际记录为准。
