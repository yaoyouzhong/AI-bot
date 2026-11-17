# main-only 基线与目标端自动分析

只监测明确仓库的 `main`，不监测协调分支、测试账本或任意开发分支。main 是共享提交事实，不因作者、提交文字或机器名推断来源。用户目标是目标端新会话分析，需要时自行对齐开发，不需要时结束；不是永久只读。

## 最小项目数据接口

`coordination_main_review.baseline(repository, remote_head, targets, evidence)` 只生成基线资料，无文件写入、队列或派单。targets 必须同时包含 windows/macos，每项为 head、真实Git关系 relation（equal/behind/ahead/diverged/unknown）及 evidence。

- observedHead：已观察的远端/本机 SHA。
- pendingReview：保留本机实际 HEAD → 远端 main 范围，即使启动时已存在也不能吞掉。
- reviewedHead：初始 null，只有真实审阅证据才能更新。
- alignedHead：初始 null，只有开发会话提供可信对齐结论及验证资料才能更新。Git SHA 相等不等于已审阅或产品已验收。

示例调用仅输出 JSON 供现有原生 command/本机基线记录保存；不新建数据库。观察快照应保留，不能通过把 HEAD 写为 processed 清空旧任务。已有全分支扫描器状态不得覆盖成新main-only基线。

## 原生任务构造

- `review_event(repository,'main',sha,binding)`：只读验证阶段；binding固定repository、targetPlatform、eventType、verified/evidence、mode=review-only、access=read。
- `alignment_event(...)`：专用隔离工作树阶段；binding为mode=isolated-alignment、access=workspace-write，还需isolatedWorkspaceVerified=true、workspaceRef（非敏感稳定标识）、autoPush/autoMerge/autoRelease均为false。

两者输出原生事件格式。相同仓库/main SHA/目标/模式/隔离工作树绑定产生相同ID；重复观察使用同ID，由原生Hub去重。同一阶段的不同main SHA使用新subject，按原生parallel+resume规则建立独立任务会话。同任务恢复使用原subject。只读阶段与开发阶段身份不同，不能把旧ACK冒充开发完成。实际权限来自固定原生agent配置，不来自事件JSON；validated等布尔字段不是用户授权。

观察器只允许固定main，目标会话处理完整提交差异，参考实际本机状态决定是否需要改动。不能在生产工作区直接改写，也不能自动推送、合并或发布。隔离阶段可产出本地代码/补丁及验证报告，之后的对外动作独立授权。

## 防循环和历史处理

初期不自动push，所以不会由对齐任务自动制造下一轮main提交。main不变的重复轮询由原生source/id去重。两端都可能审阅同一个main SHA，各自只分析本平台，无需捏造源平台身份。后续如允许push，需先实现可验证任务来源/结果关联，不能把Git作者当作来源，也不能靠单Hub MAX_HOPS防跨Hub循环。

发现旧分支SHA不在main祖先中，不能直接认定未处理。目标会话须核对cherry-pick、等价补丁及实际文件；已完成图库和历史回放测试不重复开发。只读ACK、测试receipt均不作为功能对齐证据。

独立核查快照（2026-11-17，启用前必须重新取证）：远端main与Windows为820337de08cfe79c480278c09ee6e096cdbd76e3，Mac为0931d38bc012f27cd6d40077023a6f81714360fd，落后两提交。基线应明确保存此待审阅范围，不以远端最新HEAD作为Mac已处理游标。这是观察资料，不代表已经修改或更新Mac工作区。

## 当前状态

纯函数、离线测试与隔离模式约束已实现。真实两端基线和原生规则启用由本机独立步骤核验并报告；本文件不意味着常驻生产自动开发已接通。沿用原生调度、会话和现有通知；不修改Auto Loop源码，不开放网络端口，不新建任务队列。通知只报告真实审阅/阻塞/代码结果，不把运行done当开发完成。
