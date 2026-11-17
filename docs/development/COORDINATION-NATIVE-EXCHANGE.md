# 原生 Hub + 既有 GitHub 中转（隔离验证）

本轮用户选择 GitHub 中转，不开两机网络端口，不修改 Auto Loop。原生定时/command 插件负责轮询；codex worker 负责只读执行；Hub 自身负责事件持久化、去重、运行状态。不是重新建设项目队列。

## 可运行接口

复用 `coordination_mailbox.Mailbox.publish(event, route, provenance)` 发布既有完整 packet，不改变哈希格式。新的独立 namespace 为 `isolated-test/native-shared-20261117`，避免覆盖上一轮 executor 账本的 receipt。

目标侧：

```python
from coordination_native_mailbox import NativeMailbox
mailbox = NativeMailbox(mailbox_config)
result = mailbox.poll_native(route, binding, hub)
```

- mailbox_config 与旧 Mailbox 完全一致，仅允许显式授权的 isolated-test 模式；禁止 main/master。
- route 沿用已明确的仓库/分支/双端来源，不根据名称猜测。
- binding 沿用 build_event 的 projectKey/workItemId/eventType/verified/evidence，另需 expectedPacketId 与 expectedRule。本实现仅接收预先指定的一个隔离 packet，不扫描任意产品任务。
- hub 是可信本机函数 `hub(method, path, payload=None)`，用既有 source 鉴权访问原生 loopback API；凭据只在本机原有配置和进程内存中使用，不由此模块加载、不写共享记录。
- 不从 packet 接受 executable、路径、token、rule 或执行权限；固定本地规则负责这些配置。

目标按固定 Git snapshot 读取并验证 packet 的内容哈希、来源、平台，再构造稳定原生事件。每次重复轮询都使用相同 source/id 原生去重，POST 返回整数 event 后使用 GET 查询原生运行（包括进行中）。GET.event 和预期规则必须一致，多个 run 或异常规则停止核对。终态写入不可变 GitHub receipt，明确只代表执行器状态，不代表开发完成或发布。

## 恢复与保护

没有新增 SQLite、cursor、任务队列或通知系统。GitHub 不可读时不派单；POST 响应丢失时下次原样重 POST，通过 Hub 原生幂等获取原 event；GitHub receipt 写入响应丢失由已验证 Mailbox.create 读回确认。同路径不同 receipt 停止，禁止覆盖证据。

Hub 数据丢失或更换 source 并不属于上述去重保证，不能清空 Hub 后重放旧 packet。切换规则/agent/任务绑定也不是透明重试，必须先核对既有副作用。并发原生 command 轮询须限制同一任务 lane，且不同时启用旧自研 Executor 消费同一来源。

保留旧模块与历史状态不等于继续启用它们。此类仅复用旧 Mailbox 的 GitHub I/O；不调用旧 poll、Checkpoints 或 Executor，不建立自研运行数据库。

## 验证范围与当前限制

离线测试覆盖：运行中不写终态、终态重复、POST 接收后响应丢失、packet 损坏、packet 未到、多个运行拒绝、receipt 冲突不覆盖、GitHub 读取中断不派发。实际双端临时定时测试由两端独立执行并保存审计，最终以真实回执报告，文档本身不宣称已接通生产。

只有单个受控 packet；生产分支来源登记、任意新任务发现、跨机派生结果和权限边界尚未启用。不存在自动发布，也不新增 QQ 测试消息；通知优先复用现有扩展，但其本轮端到端发送不是此测试范围。

## 暂停后的原生恢复

原生事件的 paused/routed 是历史字段。恢复规则后，仅重复 POST 相同 ID 不会重新派发；应使用 Auto Loop 自带 `replay <固定规则> --since <实际暂停起点带时区时间>`，由本机受信操作者明确执行。该操作需要 owner 权限，不读取或复制 owner 凭据给项目脚本，也不由 packet 正文授权。不要改事件 ID 或规则名制造新任务。

已核对现版本原生 replay 只取 paused 包含目标规则、尚未与该规则 run 关联的事件；规则仍暂停则拒绝，相同范围重复不会再次产生运行。since 范围可能包含多条事件，执行前核对具体规则与隔离范围，不能将单个 packet 授权扩展为任意历史批量补派。

本项目薄适配不调用 replay，只对账其结果：同一个原生 event 的 POST/GET 都显示 `routed=[]`、`paused=[唯一预期规则]`，但 GET 已出现唯一且规则匹配的真实 run，才将 `recoveredAfterPause=true`。无 run、混入其他规则、多个 run、event ID 不符均继续阻断。receipt 仍只说明该 run 的终态，不声称开发完成。

原生 replay 后响应丢失应先 GET 原 event；如果需要重试 replay，只能原规则与原时间范围，依赖原生已有 run 关联去重。不额外建立自研恢复队列。生产切换前仍需验证安装版本契约，不把上述源码行为当跨版本永恒保证。
