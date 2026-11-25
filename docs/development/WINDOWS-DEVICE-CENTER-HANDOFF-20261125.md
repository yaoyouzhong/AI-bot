# Windows 升级提示与 TAB5 会话目录

发起端为 Windows。两个场景均使用现有桥接接口，固件版本要求不变。Mac 有对应实现，待核对和对齐。

| 场景 | Windows 预期 | Mac 核对项 |
| --- | --- | --- |
| 设备中心有可用更新 | 将绿底白箭头从卡片右侧移到更新数量前。图标按 DPI 缩放，96 DPI 下直径 14 px，间距 4 px。标题与其他卡片对齐，整张卡片可点击。更新数清零后移除提示。 | `MacBridgeActionCard` 仍在卡片右侧绘制 24 pt 图标。按本端字号对齐到更新数量行，并检查有更新和无更新状态。 |
| TAB5 同步 Codex 项目 | 只有 App Server 项目 ID 的会话还需能匹配已登记工作区或 Codex 工作树目录。桌面索引和旧项目分配可证明外部工作目录的真实归属。独立验证副本及其残留记录不进入项目列表。 | `MacCodexLocalMetadata.projects` 同样读取 App Server 与桌面索引。用真实项目会话、已分配的外部目录和继承项目 ID 的验证副本复验。 |

Windows Release 构建、`--status-once`、`--self-test-device-center` 和 `--self-test-codex-tasks` 均通过。已检查两列、三列原生布局以及有无更新的状态。用真实本机目录只读验证，保留 AutoLoop 的侧栏维护和 AI-bot 的两条实际对齐开发，共排除五条隔离验证及重放会话。

待验收：Windows｜将修正后的桥接接给实体 TAB5，确认项目列表渲染。

待验收：macOS｜原生更新卡片与会话目录对齐后的检查。
