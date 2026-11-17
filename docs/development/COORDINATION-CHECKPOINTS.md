# 项目侧持久检查点、恢复与双端防循环

## 职责与部署边界

Windows/Mac **现有开发会话**读取提交、分析差异、决定并实施对齐。Dot/Auto Loop 仅发现、通知、交接、跟踪和发布协调。本模块不分析代码、不判定修复需求、不运行命令、不发送消息、不发布，也不修改 Auto Loop 源码。

`coordination_checkpoint.py` 使用 Python 标准库 SQLite，作为项目侧适配器的持久状态组件。当前只有隔离实现和测试，未接入双端真实执行器或真实工作区。消费者必须明确接入调用后才有效，不能把单元测试说成生产恢复成功。

数据库应放在独立本地运行目录，**不放 Git 仓库、不跨机器共享 SQLite、不放网络文件系统**。WAL 和 FULL 同步提供本地事务持久性；恢复时必须保留 SQLite 数据库及其 WAL 文件，不能只拷贝主文件。数据库损坏、权限失败时应停机报告，不能删除重建并重发所有任务。

## 稳定身份与显式来源

一个通知 envelope 包含 repository、originChangeId、sourceReference、sourcePlatform、targetPlatform、kind 和 visitedPlatforms。originChangeId 必须由开发会话/事件来源稳定分配，不能在每次轮询或恢复时重新生成。sourceReference 是可核对的提交/会话引用，不存凭证、代码补丁或私人聊天全文。

- `source-change`：真正独立的新变更，visitedPlatforms 初始仅包含源端。
- `alignment-result`：同一对齐链的结果，保留原 originChangeId，带 parentDeliveryId 和完整已访问平台列表。
- `(repository, originChangeId, targetPlatform)` 唯一，同身份不同内容拒绝覆盖。
- alignment-result 的 parent 必须已确认交接、属于同仓库同 origin，且 parent 目标就是当前源端。返回已访问过的平台记为 `suppressed-loop`，不能再领取为开发通知。
- 对齐结果依然可由协调者汇总为结果回执；抑制的是重复开发派单，不是隐藏用户需要知道的完成消息。
- 独立的新需求/新修改必须使用新的 originChangeId。**本工具不能从 diff 猜测是否属于原任务**；开发会话负责明确来源。若来源丢失，停止自动转发并补充来源，不根据代码相似度擅自决定。

## 状态与外部副作用

`enqueue → queued → claim → inflight → confirm → confirmed`

领取成功后，适配器应先持久化 token，再向固定的目标开发会话发送交接。如果目标系统支持幂等键，使用稳定 delivery id；本模块不会假设目标具备该能力。confirm 必须记录目标端真实消息/任务 ID 和核对证据。这里的 confirmed **只表示交接已确认，不表示开发完成、原生测试通过或允许发布**。

超过领取租期后，recover 将 inflight 转为 **uncertain**，绝不自动重新排队：

1. 查询目标端是否已收到相同 delivery id。
2. 已收到：用真实目标引用调用 confirm，不重发。
3. 确实未收到：先确认旧执行进程已停止，再用核对证据调用 reconcile_absent，才能重新排队。
4. 无法查询/无法确认旧进程停止：保持 uncertain 并上报，宁可等待，也不制造双重副作用。

reconcile_absent 的两个布尔参数是适配器对实际核查结果的声明，不是权限或自动证明。旧进程未停止即重试可能与迟到发送竞争，调用方必须遵守前置条件。确认响应丢失后重复 confirm 是幂等的；同 token 对不同目标引用的确认会被拒绝。旧 token 不能确认新一轮领取。

## 本地调用示意

```python
from coordination_checkpoint import Checkpoints
store = Checkpoints('/独立本地运行目录/checkpoints.sqlite')
row = store.enqueue(envelope)  # 仅写本地状态，不派发
claim = store.claim(row['id'], worker='现有目标会话适配器唯一运行号')
# 这里由已获授权的适配器发通知，并取得真实目标消息引用。
# 不要在示例或测试中伪造生产确认。
store.confirm(row['id'], claim['token'], actual_target_reference, actual_receipt_evidence)
store.close()
```

## 已验证与未验证

运行 `python scripts/test_coordination_checkpoint.py`：重复发现去重、双连接同时领取只一方成功、真实子进程在发送前直接退出、模拟目标副作用后直接退出但未确认、恢复读取目标记录后不重发、旧 token 拒绝、双端回环抑制、独立新变更正常排队，以及伪造/缺失传播来源拒绝。

测试的“目标”是临时目录中的隔离 JSON 邮箱，不涉及真实开发会话。没有断开用户网络、没有更改 VPN、没有发送真实消息、没有创建 GitHub 任务或伪造实际开发回执。真实部署仍需选择明确接收会话、接入适配器、确认发送与查询接口、保存稳定来源标识，再做受控交接验收。框架源码保持不变。
