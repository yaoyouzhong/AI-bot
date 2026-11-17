# 扫描交接、运行状态与独立通知账本

## 隔离接通的层次

`coordination_flow.py` 接收扫描事件的稳定 id、repo、branch、before、after，必须另外提供可信 route 与显式 provenance，才能调用固定执行器。不会根据分支名猜电脑或会话，不从 diff 决定是否修改，不给事件内容增加权限。目标端现有开发会话负责技术分析与对齐。

route 字段：`repository/branch/sourcePlatform/targetPlatform/mappingVerified/mappingEvidence`。
provenance 字段：`sourceEventId/sourceCommit/originChangeId/kind/visitedPlatforms/verified/evidence`；对齐结果另带 `parentDeliveryId`。缺少明确来源时失败等待，不能自动把所有派生提交都当新原始任务。

```sh
python coordination_flow.py --binding binding.json --database checkpoint.sqlite ingest \
  --route route.json --provenance provenance.json --event event.json --worker <唯一运行标识>
python coordination_flow.py --binding binding.json --database checkpoint.sqlite track <deliveryId>
```

同一观察重复 ingest 不重复派发。旧适配器已经发送的任务，应使用同一绑定、同一数据库直接 track 原 deliveryId，不能换新 envelope 或清空数据库来重复运行。模块会新增独立 run_observations 表，补齐确认交接后、保存运行状态前中断的情形。

返回的 handoffState 与 execution.state 分开：confirmed 说明交接有记录；queued/claimed/unknown/done/nothing/cancelled 是 Auto Loop 原始运行状态。done 不能替代代码提交、原生验证或开发会话的技术回执；输出始终 `developmentCompletionAsserted=false`、`publicationPerformed=false`。cancelled 不当成功，终态变化失败并要求核对。

## 来源链与扫描质量边界

originChangeId 与父交接来自开发会话或受控来源登记，不由 Dot 根据代码猜测。返回已访问平台会抑制重复开发派单；合法独立修改必须明确新 origin。SQLite 不跨机器共享；若父交接在另一台机器而本地没有可核对记录，当前实现会拒绝派生链，不能伪造父记录通过。生产跨端来源登记需要由共同协调账本提供并验证，尚未自动接入。

flow 接口只保证观察身份和路由对应，不负责证明 GitHub diff 完整、判断 force-push 语义或解析所有 scanner.analysis 字段。当前受控回放必须由测试调用方真实查询 compare 并保存证据。未来无人值守接入需固定完整扫描 schema，明确 pending/分叉/不完整记录如何通知目标会话审阅；不能以模块接受了 SHA 就声称代码分析已完成。只读测试规则不能升级为开发授权。

历史事件回放不是自然新提交监测，也不证明跨机器自动传输已上线。已在扫描器落盘的历史事件可以通过受控交接验证接收链，报告时必须说明是历史回放及人工/协调者传递。

## 独立通知账本与受控QQ包装器

`coordination_notifications.py` 保存通知状态，与开发交接数据库分开使用。它本身不发送QQ、不读取QQ凭据；`coordination_qq.py` 可在明确授权和固定目的地核验后调用既有qq-only传输。已支持独立受控实发验证，但不等于生产自动通知规则启用。

- 稳定通知 key 对应具体任务/状态，保存渠道、收件人和消息内容的摘要。
- begin 将 queued 原子转为 sending；再次 begin 对 sending/unknown/sent 一律拒绝。
- 进程中断后 recover 只转 unknown，不重发。
- confirm 需要本次尝试 token、真实传输回执引用、实际收件人/内容、实际回执观察时间及证据。旧时间、错误内容/收件人或已用于其他通知的回执都拒绝。
- 不保存消息明文或凭据；回执引用应含渠道前缀，不能裸用可能跨平台重复的数字。

QQ自身没有任务级幂等键。若发送成功但回执丢失且无法查询，必须保持 unknown 并核对，不能宣称失败、自动重发或借用旧 Accepted 回执。账本核查只检查传入证据一致性，不能代替传输接口证据获取。没有提供自动清空/重置unknown的接口。历史成功回执不代表生产通知集成完成。

## 验证

- flow：扫描到运行终态、重启后去重、来源/路由拒绝、响应丢失恢复、运行中暂不可查、cancelled跟踪、确认与状态写入之间中断恢复、回环抑制、终态冲突。
- notification：重复key、真实本地进程中断后unknown、旧回执不能用于新key、错误收件人/内容、旧回执时间拒绝。全部使用隔离夹具，无QQ实发。

生产监听、真实分支/会话映射、跨机来源登记与QQ包装器接入仍须分别验证。临时执行器规则结束后必须恢复；不得修改Auto Loop框架源码、合并或发布来“完成测试”。


隔离GitHub共享中转及QQ包装器运行说明见 `COORDINATION-TRANSPORT.md`。
