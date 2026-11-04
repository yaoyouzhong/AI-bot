# Mac 0.6.17 最终原包交接

**已完成：** Mac 已提交源码 `854f679` 的新原包 `fe6a9bdd…` 及匹配构建／验收清单，三项取消修复与 465 项原生测试（1 跳过、0 失败）已核验，Windows 已统一合并发布。以下为本次执行记录，不是新的待办。


目标是一次补齐最后一项修复和发布包，沿用已通过的 Mac 本机与 .313 固件验收，不等待 GitHub hosted Mac runner，也不重刷 TAB5。Windows 统一负责最终合并与发布。

## 已有结果

- .313 原固件及 95 项摘要已收到，精确应用 `e0c0981b…` 不变，完整源码也已登记。
- Mac 已有 462 项测试、29,068 项 parity、签名及设备验收继续保留。但上传的 `c00d01eb…` 桥接包对应 `9973f9b`，尚未包含 `a934d6a` 的取消草稿修复。
- 取消修复已在远端 `codex/windows-claude-dialogue`，包括 `MacTab5Codex.swift` 与 `MacClaudeCancelledDraftTests.swift`。不要仅拉旧的 `codex/tab5-claude-dialogue` 分支。

## Mac 执行范围

保护原有工作区，使用干净的上述远端分支最新源码。先核对 HEAD 与远端一致，并确认没有未提交的发布输入；不覆盖用户已有修改。无需改动钥匙串、配对、模型、权限或设备设置。

从该源码根运行一次现有正式打包脚本：

```bash
source_sha="$(git rev-parse HEAD)"
output="artifacts/mac-0617-final-${source_sha:0:12}"
bash scripts/package_macos.sh "$output"
python3 scripts/release_pipeline.py stamp "$output" --source "$source_sha" --platform macos
```

脚本包含原生 `swift test`（覆盖新增三项取消回归）、Release app 构建、原有本机签名身份、精确 ZIP 解压哈希、arm64、codesign strict 及包内刷机工具自检，不会刷设备。保持同一最终 ZIP，不在验收后重建。

只补受影响的确认后排队取消：文字与照片恢复；不覆盖后来输入的新草稿；查询回执也能撤销旧 202。其余已通过场景复用。核对最终 app 中 `Contents/Resources/SOURCE_SHA.txt` 等于上述完整 SHA，且不含 `working-tree-changes=yes`。实际最终包的后台启动/安装身份检查及复用依据与原包一并回传，不输出真实状态正文、凭据或会话。

## 回传

提供输出目录内的原始 `AI-bot-bridge-v0.6.17-macos-arm64.zip`、`.sha256` 和 `BUILD-SOURCE-macos.json`；附源码 SHA、包大小/哈希及简短原生测试与受影响检查结果。现有草稿 `handoff-tab5-0.2.313-ui-e0c0981b` 可保存这些附件，保留旧包供审计，不覆盖或公开已有正式附件。不自行创建正式标签、发布或刷机。

Windows 接收后核对同一源码、原包摘要与验收范围，完成 Windows 对应包，统一冻结候选、合并主干和发布；传递文件、说明修改及会话切换不使未变更的验收失效。
