# 双端协调任务、回执和发布预检

## 当前交付边界

本工具属于 AI-bot 项目，不改 Auto Loop 框架源码。它提供离线、无第三方依赖的机器可读契约与发布前预检。本分支已加入真实任务生命周期工具，给 `promote.yml` 及支持的本地组件/产品发布入口增加协调预检；尚未合并部署。当前未接入真实 GitHub 任务派发和两端开发调度，因此**不是已经上线的全自动协调系统，也不是覆盖全部发布入口的不可绕过门禁**。测试只验证契约及 Git 提交归属，不冒充 Windows/Mac 原生验证。

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
4. 为候选包生成发布计划，运行本预检，再运行现有不可变候选验收。本分支已在 `promote.yml` 的正式操作之前执行预检；合并部署后该工作流会要求 `coordination_plan_json`。本地组件和产品 promote/promote-set 执行也必须提供协调计划，旧独立上传 CLI 已拒绝执行；分支保护和用户手工 GitHub 操作不受项目脚本控制，不能宣称全局不可绕过。
5. 全部完成后由 Windows 发布负责人按既有授权准备发布。发布仍需用户要求与既有发布约束，缺 Mac 资料时保持暂停。

不要把本地 Auto Loop 运行状态、凭证或私人路径写入仓库。当前实施只增加项目工具、说明和 CI 测试，没有开启以上外部写入/调度步骤。


## 真实任务生命周期（本分支新增）

`coordination_ledger.py` 是项目使用侧工具，只输出新 JSON，不直接写 GitHub、不运行开发命令。每个真实任务一个文件，建议路径 `docs/development/coordination-ledger/tasks/<taskId>.json`。原 `transport-probe-v1.json` 保留为传输证据，不迁移、不伪装为真实任务；其中 simulation 标记禁止用于发布验收。

```sh
python scripts/coordination_ledger.py create task.json --authorization '<用户授权记录>' > offered.json
python scripts/coordination_ledger.py transition offered.json --revision 0 \
  --platform macos --action claim --claim-id '<唯一运行标识>' --evidence '<领取记录>' > claimed.json
```

依次 `claim → acknowledge → start → finish`，可由 claimed/acknowledged/running 进入 `block`，阻塞解除使用 `resume` 回到 acknowledged。`finish` 需要 `--receipt <终态回执文件>`；已有实现或不适用可以在 acknowledge 后直接 finish。工具不会自动生成通过证据。

### 并发与重试约束

每次消费者先读取远端文件与 **blob SHA**，使用读取时的 revision 生成新内容，然后通过 GitHub contents API 的 `sha` 条件更新同一路径。只有更新成功才允许进入下一阶段；收到 409 或连接结果不明时重新读取远端核对，不得覆盖重试。CLI 的 revision/claimId 是逻辑校验，**单独使用不能替代服务端 blob-SHA CAS**。两端同时更新同一任务时，一端成功，另一端必须重新读取，再更新自己的平台字段。禁止 force push 协调账本。

领取 claimId 应使用工作节点与唯一运行号，只有同一 claim 能改变该端状态。blocked 不自动过期或被另一个执行器抢占，防止旧执行器恢复后双重开发。暂未提供换主命令；需要协调者先停止旧执行器并形成明确移交，不能擅自删除 claim。

消费者只处理 `mode=development-task`、明确的目标平台、经过验证且确有用户授权的任务；不得把账本中的任意命令直接执行。authorization 字段用于追溯，不能由工具或第三方文本自行授予权限。

### 汇总发布计划

```sh
python scripts/coordination_ledger.py plan task-1.json task-2.json \
  --source <候选完整SHA> --scope-evidence '<完整发布范围审阅记录>' > release-plan.json
python scripts/cross_platform_coordination.py gate release-plan.json --source <候选完整SHA> --repo .
```

任何任务只完成一端都会阻断生成计划。`promote.yml` 会校验计划仓库、候选源提交、双端回执及提交祖先关系，然后才运行既有 promote。协调计划随工作流产物保存。计划任务覆盖的真实性、原生证据真实性仍需协调者审阅，不能从 JSON 自述推断。

本次没有创建真实开发任务、启动原生构建、修改 Auto Loop、合并或发布。开启消费者、形成真实任务授权范围、统一其他发布入口与合并上线仍需按用户授权逐步执行。


## 本地发布入口一致性

所有支持的 `promote` / `promote-set` 组件与产品执行入口，在创建标签、Release 草稿或上传之前调用 `require_coordination`。指定环境变量 `AIBOT_COORDINATION_PLAN` 为计划文件绝对路径；多组件源 SHA 不同时，指定目录，目录内使用 `<完整sourceCommit>.json` 命名。工作流会自动保存并传入已核对的计划。

没有计划、仓库不符、源提交不符或任何任务未闭环时，执行失败；不带 `--execute` 的只读预览仍可使用。旧 `upload_release_resumable.py upload` 独立 CLI 拒绝操作，须从接受过验收的候选经统一入口上传；底层 upload 函数仅供发布入口内部调用。`split` 纯本地分片不受影响。

这些约束是项目程序入口约束，不是 GitHub 服务端权限控制。仓库所有者手工执行 gh/API、导入底层函数、修改脚本或运行旧版本仍可绕过。只更新既有 Release 文案的 closeout 不创建/发布新版本，保留原行为。若需要不可绕过的平台级保护，应单独授权配置环境审批/分支保护/发布凭证隔离，本次不更改账户权限。

隔离测试 `test_coordination_safety.py` 覆盖并发领取、响应丢失、不同平台冲突、缺计划的组件/产品发布阻断、目录计划和旧上传入口阻断。没有伪造真实任务回执。双端只读端到端验收方案见 `COORDINATION-E2E-READONLY.md`，尚未派发真实任务。

## 门禁审查补充

- 空 `requiredTaskIds`、空 tasks、缺任一必需任务或任一平台回执都不能通过。不能以“本次没任务”为理由省略协调；只涉及已有实现的发布应形成明确范围任务，引用已通过的真实验证并说明复用理由，不要求重新全量测试。
- 只读巡检（`kind` 以 `read-only` 开头或 `constraints.inspectionOnly=true`）不能推进开发 start/finish，也不能用作发布完成回执。接收确认保留原含义；本工具不会把历史图库巡检变成图库修复任务。
- task 的 base 必须是 source 的祖先；force-push 造成分叉时先重新审阅，不假称原差异已覆盖。复用原生验证时，验证 baseline 必须在实现提交历史中，仍需解释为何相关行为没有改变。
- 门禁现在要求独立固定到 Git 提交的范围记录，并重新计算实际提交与文件差异，能够检测仅从发布计划中整项删除任务。范围记录中的基线选择和语义映射仍需人工/协调者审阅；不能将JSON中的 reviewed=true 自述当成用户授权。


## 独立发布范围记录与完整差异覆盖

只读生成待审清单（不会猜任务或自动标记审阅通过）：

```sh
python scripts/coordination_scope.py --repo . --repository yaoyouzhong/AI-bot \
  --base <上个已接受发布基线SHA> --source <本次候选源SHA> > scope-draft.json
```

记录包含 `releaseBase/releaseSource`、完整 `commits/files`、`commitTasks/fileTasks` 和 `requiredTaskIds`。生成结果 `reviewed=false`，所有映射为空，不能发布。协调者按真实 diff 审阅，填写每个提交和每个变更文件所对应的任务 ID；删除和重命名按旧/新文件路径纳入，合并提交、撤销后净文件差异为空的提交仍列在 commits 中，不能静默跳过。范围记录的任务 ID 集合必须等于映射引用集合，且发布计划中的任务集合必须与之完全一致。

审阅后将记录保存到约定分支的：

`docs/development/coordination-ledger/release-scopes/<完整候选sourceCommit>.json`

该文件提交后的 **范围记录所在提交 SHA** 与候选 sourceCommit 不同。执行时独立指定：

```sh
export AIBOT_COORDINATION_SCOPE_COMMIT=<已审阅范围记录的完整提交SHA>
export AIBOT_COORDINATION_PLAN=<发布计划文件绝对路径或按源SHA命名的目录>
python scripts/cross_platform_coordination.py gate release-plan.json --source <候选源SHA> --repo .
```

Windows PowerShell 使用 `$env:AIBOT_COORDINATION_SCOPE_COMMIT='...'` 设置环境变量。`promote.yml` 新增必填 `coordination_scope_commit`，从该独立输入取得 SHA，**不信任发布计划自行指定的 scopeCommit**。门禁通过 git show 读取固定提交中的规范路径，不读取可能被本地修改的工作区记录；再重新计算 Git 实际变化，逐项匹配提交、文件和任务映射，检查全部任务回执。范围提交或完整历史缺失时失败，须先 fetch；没有免检开关。预检结果保存范围提交、路径和实际变更数量，工作流上传为 `coordination-preflight.json`。

多组件不同 sourceCommit 可在同一范围记录提交中保存多份 `<source>.json`，单独的发布计划也按 source 命名，兼容原有分批发布和已有验收复用。没有实际新产品变更但需要重发已验收候选时，仍使用该候选原来审定的范围记录，不改变候选或要求重复验收。

### 能与不能自动证明的事

能验证：基线是源提交祖先、实际提交/文件没有少列、每个提交/文件都关联任务、独立记录与发布计划任务集一致、双端回执与实现归属有效。整项任务从计划和 receipts 同时删除也会失败。

不能自动证明：选择的上次发布基线是否正确、把某文件映射到某任务是否在产品语义上恰当、原生日志内容是否真实、审阅人是否有批准权。若攻击者同时有权更改范围记录、另选范围提交与发布输入，或直接操作 GitHub，则项目脚本不能提供服务端权限隔离。`reviewed` 和证据字段仅是可追溯声明，不新增任何发布授权。需要平台级强制时另行授权配置环境审批/凭证隔离。

本次只开发和隔离测试范围机制，没有创建真实发布范围记录、没有更改既有任务、没有合并或发布。真实范围审定必须以明确候选版本和既有验收证据为依据。
