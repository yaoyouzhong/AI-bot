# 发布自动化 / Release automation

发布负责人执行本页命令，用户无需手工搬运文件或逐阶段确认。同一次明确的发布指令覆盖其范围内的提交、推送、标签、上传、公开发布与收尾；命令的 `--execute --publish` 表示执行这个已有授权，不另设第二轮审批。没有发布授权时只准备候选和运行预检。

四项交付是 Windows 桥接、Mac 桥接、ESP8266 和 TAB5；使用桥接、ESP8266、TAB5 三条版本线。未变化的组件不重新编号、不构建、不刷机。

## 1. 固定范围，取得候选

先完成版本声明和最终中英文更新日志，再选定源码 SHA。优先查找已有候选：同源码、同文件哈希、原生构建与检查记录完整时直接使用。普通代码 CI 的编译输出不是完整发布包，不冒充可晋级候选。

需要新候选时，在干净的指定提交上运行 `candidate.yml`；选择 `bridge`、`esp8266` 或一次构建两者的 `bundle`，桥接平台选 `all`、`windows` 或 `macos`。双平台包从同一 SHA 在各自操作系统构建。候选流程在原生打包成功后登记文件来源，汇集时拒绝混合源码或变化的文件，输出：

- `release-candidate-bridge`：所选 Windows/Mac 包、审计材料及冻结清单。
- `release-candidate-esp8266`：ESP8266 包、审计材料及冻结清单。

`CANDIDATE.json` 的 `candidateId` 绑定源码、组件、平台、文件大小、SHA-256、说明及必要验收项；候选目录不再修改。下载并归档 Actions artifacts，记录批次 ID。重复装配同一候选可直接复用，不重建产品。

首次启用本流程后，新的候选源码应包含新版标签工作流。GitHub 的标签事件读取被打标签提交中的工作流；仅更新 main 不能改变旧提交的行为。脚本拒绝给仍带旧构建流程的历史源码创建新标签，避免意外重建；已存在且来源正确的正式标签仍可核对，不追溯重发历史版本。

本地已有原生候选可用 `stamp`、`assemble --require-stamps` 汇集；仅在原生构建和回归确实成功后登记来源，不给未提交工作区补写假的源码 SHA。已有 `prepare_release_assets.py` 验证目录可用 `seal` 接入，不重新打包。TAB5 仍由独立工程构建，先用 `prepare_release_assets.py --component tab5 --tab5-source ...` 核对首刷、升级、实际镜像和源码，再用 `seal --tab5-source ...` 封存对应源码 ZIP。封存不刷机。

## 2. 一次登记实际验收

Agent 先读已有证据，只补本次受影响路径。在 Windows PowerShell 中可这样登记一项，实际路径与证据由发布负责人填写：

```powershell
$candidate = 'artifacts/release-next/bridge'
$acceptance = 'artifacts/release-next/bridge-acceptance.json'
python scripts/release_pipeline.py record $candidate $acceptance --check windows-install-startup --result passed --evidence '实际候选安装、启动与资料保留的结果及证据编号'
python scripts/release_pipeline.py status $candidate $acceptance
```

必要项为 `release-review`，另按范围要求 `windows-install-startup`、`macos-install-startup`、`esp8266-device` 或 `tab5-device`。`release-review` 一次记录范围、关键变更、最终双语说明、剩余限制及媒体采用“更新”还是“准确可复用”。记录可以是 `passed`、`reused`、`failed`；`reused` 必须同时提供完整基线 SHA（`--baseline`）、原证据（`--evidence`）及未受影响的理由（`--reason`）。记录将公开，不能写入个人资料或凭据。

同一原包和场景已经通过，就不因换会话、换机器传递、准备标签、上传重试或纯文档变动再验收。不要求每次重做旧公开版升级、配对、草稿解密、设备刷机或媒体编码。安装器、迁移、依赖或具体功能变化才补对应检查；不能复用覆盖不了该变化的结论。失败、缺项、错候选或文件变化会阻止晋级；工具不自动宣称真机通过。

只需真实物理操作或必要账户时才让用户参与。自动化不得置顶或切换前台；安装验证可采用受控临时目录和后台启动，保存恢复安装登记及用户资料。同一路径受阻且没有新证据时换方法，不机械重试沙盒。

## 3. 一次执行原包发布

本地单组件预检不访问 GitHub、不建标签、不上传、不构建：

```powershell
python scripts/release_pipeline.py promote $candidate $acceptance --repo yaoyouzhong/AI-bot
```

用户已要求正式发布时，同一命令加 `--execute --publish`；只准备草稿时加 `--execute`。脚本创建或核对指向候选源码的带注释标签，生成兼容别名与公开证据，断点上传并校验服务端摘要，最后公开。它不会调用产品构建、安装或刷机命令。

多组件一起交付时，用一份计划一次执行，路径相对计划文件所在目录：

```json
{
  "schemaVersion": 1,
  "repository": "yaoyouzhong/AI-bot",
  "candidates": [
    {"directory": "bridge", "acceptance": "bridge-acceptance.json"},
    {"directory": "esp8266", "acceptance": "esp8266-acceptance.json"},
    {"directory": "tab5", "acceptance": "tab5-acceptance.json"}
  ]
}
```

```powershell
python scripts/release_pipeline.py promote-set artifacts/release-next/plan.json
# 在已有正式发布授权内执行；三个组件发布后只做一次下载元数据收尾
python scripts/release_pipeline.py promote-set artifacts/release-next/plan.json --execute --publish
```

只列本次变化的组件。执行前先预检全部验收；网络失败可能让部分组件先公开，记录会明确完成到哪里，重试同一命令继续，不能声称跨三个 Release 是原子操作。

也可显式调度 `promote.yml`，提供成功的 `candidate.yml` 批次 ID、组件、`record` 生成的完整验收 JSON 和 `publish`。它取回该批次的原候选并核对 SHA，不调用构建；默认 `publish=false`。输入只通过环境变量交给 JSON 解析器，不拼接进 shell。多组件发布在本地用 `promote-set` 可统一收尾；独立组件适合 Actions 晋级入口。TAB5 用本地晋级入口，不伪造原生 Actions 批次。

## 4. 自动收尾，集中复核

公开后自动刷新并验证 `download-catalog.json`、中英文下载中心、README 下载块和本次 Release 的完整下载表；自动核验实际公开下载大小、摘要及所需抽查，不重复下载完整大包。公开的 `CANDIDATE.json`、`ACCEPTANCE.json`、许可证、第三方声明与 `SHA256SUMS.txt` 留存原包身份；便携包和完整桥接源码继续保留在候选归档。

默认只更新本地生成文档。在已有推送授权内加 `--commit-closeout`，只暂存并提交这些指定生成文件，拒绝已有相关改动、非空暂存区、落后主干或会夹带其他本地提交的情况；不自动 rebase。Actions 的公开晋级使用此选项，串行执行以减少冲突。如果分支保护不允许机器人直接推送，公开状态和收尾失败会被分别记录；负责人按仓库现有合并方式处理生成文档，不重新构建或验收包。

负责人只做一轮语义复核：受影响主页亮点、安装说明、兼容性和媒体是否准确。下载表可自动生成，功能文案不能由哈希推断。小更新复用仍准确的截图、视频和播放结果，仅更新会误导用户的部分；完成 [收尾清单](RELEASE-CHECKLIST.md) 并保留限制后交付。该复核由 Agent 完成，不要求用户再批准已授权的普通收尾。

## 重试与边界

| 情况 | 继续方式 |
| --- | --- |
| 上传断线或回包丢失 | 重跑同一晋级命令；按服务器摘要跳过已成功附件 |
| 部分组件已公开 | 重跑同一计划；只校验已公开原包，继续其余组件 |
| 已公开但下载文档收尾失败 | 原包保持公开；重试晋级或 `closeout --execute --repo ... --tag ...`，不重建 |
| 候选、说明或验收范围变化 | 新候选身份，只补受影响检查；不能让旧记录静默通过 |
| 标签或已公开附件与候选不符 | 停止，不移动标签、不替换正式附件；查明原因或另发版本 |
| 只有文档/工具改动 | 跑相应工具回归；不编译 Windows/Mac/ESP8266，不重验设备 |

`release.yml` 标签事件只校验版本与最终说明；`ci.yml` 通过 `ci_scope.py` 选择受影响产品和工具检查。常规代码 CI 编译仍用于开发质量，正式交付物在候选阶段构建一次；不能把“无重复发布构建”表述为整个开发过程只有一次编译。原生候选批次构建、真实验收与公共下载是三类不同证据。

## English

Use four steps: select changed scope and source, reuse or build native candidate packages once, record actual or justified reused acceptance once, then promote those exact bytes and close out. Four deliverables share three component version lines. Tag events validate metadata only. Candidate identities bind source, scope, notes and file hashes; altered packages or missing/failed acceptance block promotion. Record explicit baseline evidence and reasons when reusing unchanged-path results.

`release_pipeline.py promote` defaults to a local-only preflight. Within an existing release authorization, `--execute` creates/verifies tags and a draft, and `--publish` publishes verified assets and refreshes download metadata. `promote-set` preflights all selected components, resumes partial publication and closes out once; GitHub does not provide a cross-Release transaction. Neither command rebuilds, installs or flashes a product. Actions promotion retrieves an explicitly selected successful native candidate run; TAB5 retains independently validated local artifacts and corresponding source.

Verified assets are skipped on retry, published mismatches are never replaced, and failures distinguish publication from pending closeout. Optional `--commit-closeout` stages only generated download files and refuses unrelated work; protected branches may require the existing merge route. Ordinary code CI still validates changed products, while documentation/tooling changes avoid product compilation. The release owner reviews affected bilingual prose and accurate media reuse once, preserves limitations, and does not request separate approval for stages already covered by the user's release instruction.

New candidate source commits must contain the metadata-only tag workflow. GitHub reads tag workflows from the tagged commit, so promotion refuses to create a new tag on historical source that would trigger the old rebuild flow. Existing correctly sourced tags remain verifiable; historical releases are not republished during rollout.
