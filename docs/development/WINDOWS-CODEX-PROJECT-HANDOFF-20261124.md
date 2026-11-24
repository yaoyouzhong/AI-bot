# Windows TAB5 Codex 项目归属

## 范围

- Windows 桥接读取 Codex 项目归属。现有 TAB5 选择器、分页、交互状态和传输协议沿用原实现，固件无需重刷。
- 同一会话保持原会话 ID 和实际工作目录。桥接先查本地会话库和索引中的项目 ID，再读取桌面保留的旧归属与本机迁移映射，最后沿用目录和 Git 工作树匹配。只有已保存的本地项目可进入设备目录，远程与归档会话继续按原边界过滤。
- macOS 的 [MacTab5Codex.swift](../../mac-app/Sources/AIBotBridge/MacTab5Codex.swift) 在本次对齐前从工作目录和 Git 信息推导 Codex 项目。Windows 实现可以独立部署。
- macOS 回填：待验证。Mac 对齐分支已按桌面保存的项目归属列出会话，项目归属优先级、旧归属迁移及远程和归档会话排除的本地回归通过。本机 Codex 数据库的相关字段已只读核对。仍待配对 TAB5 验证真实会话列表及独立 Git 工作树归属。Windows 已在 #42 完成对应实现及 USB 真机验证。

## 复现与验证

- 当前 Windows Codex 把「排查 AutoLoop 自动处理失败」分到 `AutoLoop`。该会话位于 `autoloop-alignment-tools` 的独立 Git 工作树。新项目字段还未写入，旧桌面项目映射有明确、可匹配的本机记录。旧桥接能查到会话索引，但在项目匹配时排除了这条会话。
- 候选桥接的实际设备目录在 16、32、48、64 和 80 条容量下均包含该会话，归属 `AutoLoop` 的第 1 组。真实回复读取返回 200，来源为 App Server。
- Windows Release 构建、`--status-once`、`--self-test-codex-tasks` 全部通过。专项回归覆盖旧项目映射、新项目 ID、迁移前后的冲突、项目归属清除、远程和归档隔离。
- 本机已安装并运行 `0.6.30-projects.20261124`，DLL SHA-256 `B703B540BB15BE8AFDD63ACF6ED5A0DCAED196AD16CA58BAF5A14110DD265769` 与候选一致。TAB5 `0.2.322-ui` USB 已认证，展示配置保持一致。
- 用户在物理 TAB5 的 `AutoLoop` 项目中找到「排查 AutoLoop 自动处理失败」并确认回复正常。设备打开测试时桥接记录了 App Server 正文应答，随后也记录到上一轮的 USB 应答。状态帧完整下发 80 条会话，未因容量降级。
