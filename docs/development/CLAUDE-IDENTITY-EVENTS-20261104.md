# Claude 身份事件接入候选（2026-11-04）

本次为当前工作区的开发候选，未发布、未部署到已安装桥接、未改用户 Claude 设置。Windows 仍须原生系统验收；TAB5 仍须真机验收。最终完成覆盖范围见下文，不能把索引、模拟测试或编译当作全量会话支持。

## 数据与状态

两端原生桥接新增 `--claude-hook <port>`。它从标准输入读取 Claude hook JSON，仅转发本机 `127.0.0.1:<port>/event` 所需的事件名、session ID、prompt ID、发送时间，以及空闲通知类型。主会话 UUID 不合法、缺少轮次或带 `agent_id` 的子代理事件被忽略；正文、会话名称、路径、工具输入输出和错误详情均不转发或记录。转发最长约一秒，保持标准输出为空，不产生授权决定，不自动重试任务。

本机身份事件格式为 `agent=claude`、`event`、`sessionId`、`promptId`、`emittedAt`（Unix 毫秒）。它只用于桥接内存匹配，不发送到 TAB5。非本机调用和带浏览器 Origin 的请求不能写入；损坏的身份事件不会降级为旧的无身份事件。仅接受最近两分钟内发送的事件；长任务的新事件按当时发送时间处理。

只有 `UserPromptSubmit` 可打开一轮。旧轮次不能修改新轮次；同一结果重复到达不重复产生完成序号；停止、空闲和会话退出不证明成功。StopFailure 保留失败；明确 TurnInterrupted 保留中断；后来的空闲事件不覆盖这些终态。普通工具失败仅表示当前仍在处理，不能据此判定整轮失败。缺少终态或十分钟未收到进展时保持未确认，不用超时制造完成。会话退出后的同轮受控最终结果可以补齐终态。

同时存在多个 Claude 会话时，优先待操作，其次工作中，再比较实际工作活动时间；空闲通知的到达时间不更新工作活动时间。过期等待不继续优先。已确认失败使用现有 `error`/受阻萌宠；明确中断清除完成并显示空闲。TAB5 的状态、名称和最近活动时间来自同一选中会话，名称仍按 UUID 查找已有标题，没有时显示 Claude Code。原始国内供应商活动可能属于另一会话，在精确绑定的 TAB5 投影中省略该活动汇总，避免抢占萌宠；账户额度不受影响。

## 完成和中断的覆盖范围

独立运行的普通交互 Claude Code 可以提供身份、工作、待操作及 StopFailure。官方 hook 缺少通用的用户中断通知；`Stop` 可要求继续，`idle_prompt` 会延迟且失败后也会发送。因此该模式仅凭现有 hooks 不标记成功，也不能声称能精确识别所有 Esc 中断。已知终态缺失时显示未确认。

可选的 `tools/claude_identity_runner.py run` 用 Python 3 启动自己的 `claude --print` 任务，临时增加身份 hooks，并保留原本的模型、工具和审批策略。它读取自己启动进程的最终 JSON，核对 session ID 和本轮 hook 的 prompt ID，仅把 `terminal_reason=completed`、`is_error=false`、正的 num_turns、成功退出投影为同轮 TurnResult；失败投影为失败。中断入口只在进程实际退出后发送 TurnInterrupted。没有身份、结果格式未知或无法确认时不发送成功。该入口不能旁听或接管另一个已经运行的独立会话，也不自动把用户日常会话改成受控模式。

示例（任务从调用终端的标准输入交给 Claude）：

```sh
python3 tools/claude_identity_runner.py run --port 8765 --claude /absolute/path/to/claude -- --model claude-sonnet-4-6
```

此运行入口是可选的开发候选，需要 Python 3；原生 hook 转发不需要 Python。不得把它描述成已安装产品的默认入口。

## 临时 hook 配置

在单个 `claude --settings <temporary-settings.json>` 会话中使用候选原生桥接，重复为 UserPromptSubmit、PreToolUse、PostToolUse、PostToolUseFailure、PostToolBatch、PermissionRequest、Elicitation、Stop、StopFailure、Notification、SessionEnd 配置下面的 exec 形式。`command` 填本平台候选可执行文件完整路径；Windows 填真实 `.exe`，Mac 填 `.app/Contents/MacOS/AIBotBridge` 或开发二进制。`args` 避免平台 shell 及带空格路径的转义问题。开发不自动合并或覆盖用户全局设置。

```json
{"hooks":{"UserPromptSubmit":[{"hooks":[{"type":"command","command":"/absolute/path/to/AIBotBridge","args":["--claude-hook","8765"]}]}]}}
```

官方定义：[common input fields](https://code.claude.com/docs/en/hooks#common-input-fields)、[Stop](https://code.claude.com/docs/en/hooks#stop)、[Notification](https://code.claude.com/docs/en/hooks#notification)、[exec form](https://code.claude.com/docs/en/hooks#exec-form-and-shell-form)。

## 验收边界

自动回归包含旧轮次迟到、重复完成、失败后空闲、中断后空闲、退出先于最终结果、多会话名称和状态绑定、标题缺失与撤销、隐私字段白名单。真实 Claude CLI 以临时 HOME、模拟本机 API、合成任务验证原生 hook、受控成功、接口失败及中断链路；不使用真实账号或会话内容。

LVGL 预览使用当前已核验 .312 工程及 .313 选择补丁，追加绑定名称下工作、受阻、中断空闲、成功四种生产控件场景。用户随后要求 Claude 各状态及 Codex 的大小与位置对齐，本轮新增总览缩放和锚点修正；须重新构建 .313 候选，旧镜像不包含该显示修正。预览检查不代表刷机通过。真机必须分别验收单选固定客户端、双选待操作/工作/最近活动、标题和萌宠对应、失败和中断不播放成功，以及断线/重新连接。
