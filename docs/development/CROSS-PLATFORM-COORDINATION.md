# 双端协调任务、回执和发布预检

## 当前交付边界

本工具属于 AI-bot 项目，不改 Auto Loop 框架源码。它提供离线、无第三方依赖的机器可读契约与发布前预检。当前未接入 GitHub Issue 写入、两端任务调度或现有 promote 流水线，因此**不是已经上线的全自动协调系统，也不是不可绕过的发布门禁**。测试只验证契约及 Git 提交归属，不冒充 Windows/Mac 原生验证。

只读扫描继续独立运行。扫描到新提交后，需要协调者审阅实际 diff、判断适用平台，再生成任务。扫描器不能直接将“有新提交”当成“双方需要改代码”，也不能自动发布。

## 任务身份与回执

```sh
python scripts/cross_platform_coordination.py task \
  --repository yaoyouzhong/AI-bot --branch <分支名> \
  --base <完整旧SHA> --head <完整新SHA> --summary '<变更范围>' > task.json
python scripts/cross_platform_coordination.py receipt task.json macos-receipt.json
```

重复执行相同输入得到同一 taskId；任务内容变更会改变 taskId，旧回执不能套用。任务应作为 GitHub Issue 的结构化内容或附件，Issue 只创建一次；重试前先按 taskId 搜索。收到任务可以报告 acknowledged/running/blocked，但这些状态都不能通过发布预检。

终态回执格式（SHA 均为完整的 40 位小写 SHA）：

```json
{
  "schemaVersion": 1,
  "taskId": "<task.json中的taskId>",
  "sourceCommit": "<源端新提交SHA>",
  "platform": "macos",
  "status": "completed",
  "implementationCommit": "<该端实现提交SHA>",
  "reason": "本次适配范围及差异说明",
  "evidence": "实现PR或可追溯日志链接",
  "verification": {
    "platform": "macos",
    "result": "passed",
    "commit": "<该端实现提交SHA>",
    "evidence": "原生构建与受影响功能验证记录"
  }
}
```

Windows 使用 `windows`，Mac 使用 `macos`。`status` 可为 `completed`、`already-present`、`not-applicable`。两端都必须明确回执，包括源端，不能靠沉默代表完成。

- `completed` / `already-present` 必须附原生验证结果 `passed` 或 `reused`。
- 已测试通过且相关实现未变，允许 `reused`：`verification.commit` 填原验证提交，另加 `reuseReason` 说明为何仍适用；不要求用户重做全量测试。
- `not-applicable` 必须说明原因及证据，仍填写审阅的实现基准提交；可不填写 verification。
- 本工具检查字段与提交归属，**不验证链接真实性或日志内容**；协调者必须审阅证据，不得伪造回执。

## 发布计划与预检

由唯一发布负责人汇总一个 JSON：`schemaVersion: 1`、`repository`、`releaseSource`、`scopeEvidence`、`requiredTaskIds`、`tasks`、`receipts`。`tasks` 存完整任务对象，`receipts` 存两端回执。`scopeEvidence` 指向本次发布范围审阅记录，`requiredTaskIds` 是该记录中需闭环的任务清单，不能仅挑已完成任务。

```sh
python scripts/cross_platform_coordination.py gate release-plan.json \
  --source <候选CANDIDATE.json中的sourceCommit> --repo .
```

失败返回非零：旧发布计划、缺任务/回执、重复回执、待验证、失败验证、未合入发布提交的源变更或适配实现、无法读取提交历史，都会阻断预检。应先 fetch 所需历史，不能用浅克隆缺历史作为跳过门禁的理由。当前要求源与实现提交是 releaseSource 的祖先；squash/cherry-pick 必须重新审阅并建立准确的集成任务，不能假称原 SHA 已合入。

通过只输出 `ready` 和 `publishPerformed: false`，不会打标签、上传、修改 Release。既有候选包验收及哈希检查继续执行，本工具不能替代它们。

## 下一阶段接入顺序

1. 在允许的 GitHub 写入范围内按 taskId 建 Issue，记录原分支、旧/新 SHA、适用平台及验收要求。
2. 由现有 Auto Loop 配置分别投递给两端现有开发会话。保存任务领取回执；连接/授权失败如实标 blocked，不宣称送达。
3. 两端提交实现及结构化回执，协调者审阅证据和整批变更覆盖范围。
4. 为候选包生成发布计划，运行本预检，再运行现有不可变候选验收。将此预检放进唯一发布入口，才形成强制门禁；须同步更新该入口测试后部署。
5. 全部完成后由 Windows 发布负责人按既有授权准备发布。发布仍需用户要求与既有发布约束，缺 Mac 资料时保持暂停。

不要把本地 Auto Loop 运行状态、凭证或私人路径写入仓库。当前实施只增加项目工具、说明和 CI 测试，没有开启以上外部写入/调度步骤。
