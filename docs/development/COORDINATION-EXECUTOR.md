# 固定 Auto Loop CLI 适配器

## 当前边界

`coordination_executor.py` 是 AI-bot 项目侧适配器，不改 Auto Loop 框架。只把稳定交接事件发给已验证的规则，并核对真实运行记录；目标端现有开发会话负责分析和修改。它不猜分支对应哪台机器/哪个会话，不把静态差异当修复要求，不自动发布。

现有只读观察器不会因本文件存在就获得派单能力。生产绑定必须逐项核对已有规则、Agent、开发会话、工作目录、权限与通知范围。临时受控规则通过不代表已绑定现有生产开发会话。

## 本地可信绑定

保存到仓库外的独立运行目录：

```json
{
  "commandPrefix": ["<Node绝对路径>", "<现有agent-autoloop.ts绝对路径>"],
  "expectedRule": "<实际核对过的规则名>",
  "expectedAgent": "<该规则绑定的Agent名>",
  "eventSource": "local",
  "eventType": "<该规则精确匹配的事件类型>",
  "targetPlatform": "macos",
  "bindingVerified": true,
  "bindingEvidence": "<规则/Agent/会话实际核对记录>"
}
```

Windows 使用 `windows`，Mac 使用 `macos`。不能把示例占位值当成已验证绑定。commandPrefix 由本地可信配置决定，不允许事件选择可执行命令。适配器调用已安装 CLI 的认证，不读取、复制、打印或写入 token。

绑定会生成指纹存入检查点；同一交接的规则/Agent/配置变化时拒绝重定向，不得删数据库绕过检查。需要变更绑定时先核对现有目标副作用，再进行明确移交。

## 交接与恢复

使用 `COORDINATION-CHECKPOINTS.md` 定义的 envelope；其 `sourceReference` 应指向确切提交/任务，originChangeId 必须保持稳定。目标规则把事件正文当资料，不能当成新授权。

```sh
python coordination_executor.py --binding binding.json --database checkpoint.sqlite \
  deliver envelope.json --worker <本次适配器运行标识>
python coordination_executor.py --binding binding.json --database checkpoint.sqlite \
  reconcile <deliveryId>
```

适配器调用已验证版本支持的：

- `emit <固定type> --id <deliveryId> --subject <deliveryId> --summary <固定说明> --detail <JSON资料>`。
- `runs <固定规则名> --json`，必要时使用 `--before` 翻页。

emit 的中文摘要或 exit 0 **不代表目标已收到**。只有 runs 中规则、Agent、事件来源、类型和 subject 全部精确匹配时，才记录实际 run ID，状态 confirmed 表示“交接在目标运行记录中可查”，不是开发完成或测试通过。固定版本的 `runs` CLI 只列已结束的轮次：任务已经派发且正在运行时，仍可能返回 uncertain。此状态仅表示“尚未取得终态运行记录”，**不表示没有派发**；重复 deliver 只查询，绝不再 emit。真实临时 Hub 验证已观察到运行中无记录、结束后 reconcile 成功的路径。超时/断线同样保留原 deliveryId，查询核对后再决定，不能自动生成新身份重发。

分页最多 100 页，遇到不完整/重复游标、多个匹配运行或格式异常时失败关闭，不能判定目标不存在。已有运行失败仍是“交接已到达”，执行失败需要单独跟踪，不自动另开开发任务。

## 验证与上线

`test_coordination_executor.py` 覆盖真实 CLI 响应结构的隔离夹具：正常确认、exit0无run、超时后核对、错误平台/未验证绑定拒绝、绑定变化拒绝、跨页重复run拒绝。`test_coordination_checkpoint.py` 另覆盖真实本地进程中断与SQLite持久恢复。

真实双端 Hub 验证只能使用临时精确事件类型、独立目录、只读Agent、tell:never；不与生产规则同名，不启用QQ，不创建开发或发布任务。验证完成后删除本次新增的临时规则/Agent并核对原规则保留。真实回执由执行测试的电脑报告，不能用夹具结果代替。

正式接入仍需明确的生产规则/Agent/现有开发会话映射，不能根据分支名推断。合并部署和发布不包含在适配器测试授权内。
