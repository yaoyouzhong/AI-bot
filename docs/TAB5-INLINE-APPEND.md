# TAB5 草稿末尾追加 / Inline draft append

2026-10-05，用户要求后续录音接在上一条草稿末尾，前文末尾需有分隔符，避免文字黏连。这是 Windows 桥接的文本合并规则变更，TAB5 继续运行 .119，协议不变。

- 已有句号、逗号、问号、叹号、冒号、分号、省略号或破折号等分隔符时，直接续写，不自动换行。
- 前文没有分隔符时，在末尾空白之前插入中文句号；保留原字符与空白。
- 检查闭引号/括号之前的标点，避免在已有句号外再补一个；手动段落换行照常保留。
- 空草稿不补句号；目标身份确认、32000 UTF-8 字节限制、并发修改检查、单次写入和精确回读保持。

内部证据位于 `artifacts/development/tab5-inline-append/`：Release 构建零警告/错误，quick-console 和 voice 回归通过。覆盖连续补录、中英文标点、无标点补句号、引号、空白、手动换行及写入回执异常不重复写入。语音测试使用合成输入，不能代替本次规则的真机验收。

规定的 `dotnet run --status-once` 仍出现 GUI 宿主控制台句柄异常，记录于 `required-status.log`；正常桌面环境直接运行相同 Release DLL 的 `desktop-status.json` 成功。未通过的入口不计为通过。

内部检查后仅更新五个桥接文件，记录于 `deployment.json`，旧文件保留在 `bridge-backup/`；退出、恢复自动轮播后正常启动。原配对、页面顺序及 15 秒间隔按 `policy-before.json` 和 `final-status.json` 核对。未刷固件、未提交/推送/公开发布。

此前 .119 的会话、语音及发送保护已由用户确认通过。用户随后回复“测试ok”，确认无标点补句号、已有标点不重复、手动换行保留的追加规则真机验收通过。

The bridge now appends dictation inline, adding a Chinese full stop only when the preceding draft lacks a separator. Existing punctuation, closing quotes/brackets and manual paragraph breaks are respected. Target identity, length, concurrent-edit and one-write/readback protections remain. Build and synthetic regressions pass; the normal-desktop Release DLL status check passes while the existing GUI-hosted dotnet-run console-handle limitation remains. Only bridge files were deployed, with backups and original cycling preserved. Firmware stays at .119. The user subsequently reported the separator test passed, completing hardware acceptance for this change. Previously accepted functionality is not reopened.
