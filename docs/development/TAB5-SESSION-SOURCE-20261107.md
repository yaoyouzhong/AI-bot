# TAB5 session source badges — local candidate

用户批准按实际 LVGL 预览实现来源标签、预留第三方 Harness 接口，并更新日常桥接和 TAB5 完成真机验证；先不发布。此次授权已覆盖本候选的本地部署，不新增模型测试请求。

## Design and implementation

沿用 1280×720 界面和已有卡片。Claude 会话行高 80px、间距 8px，标题下面增加被动来源标签；输入区与回复页标题旁重复显示。VS Code 为蓝色，原生终端为紫色，Harness 为琥珀色，未知来源为中性色。列表内可视高度增加到 344px，第四行标签完整可见；运行标识和操作按钮位置保留。长标签和标题单行截断，中文字库回退使用现有 24px 字体。

来源来自精确承载端解析，与标题、项目和实际模型提供商分离。已确认来源存入有界加密缓存，断连不清空标签；操作能力仍随实时控制状态撤销。协议新增可选 `tasks[].source`，旧端可省略。Windows 原生会话路由移至编译期适配器注册表；多个承载端同时认领同一会话时不选择第一个发送。接入约定见 [CLAUDE-HARNESS-ADAPTERS.md](CLAUDE-HARNESS-ADAPTERS.md)。未安装或连接实际第三方 Harness。

重复刷新仅更新变化的尺寸、位置和样式。由于 LVGL 的截断标签会改写显示缓冲，保留这些固定标签的完整输入以避免反复赋值；同时去除随后立即被覆盖的回复状态中间颜色。最终视觉颜色不变，长标题和运行中状态的页面缓存回归通过。

字体为已有授权 Noto Sans CJK SC 的 20px 子集，源 OTF SHA-256 `2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b`，生成命令和校验在本地证据目录 `prepare.py`，保留既有字体许可。ASCII 和固定来源词使用子集，其他标签使用原 24px 字体支持的字形。

## Frozen payload

证据目录：`artifacts/tab5-session-source-20261107/`。原日常桥接 DLL 和 TAB5 镜像保留回滚副本/历史归档。未提交、推送、打标签或发布。

- Bridge DLL: `4b6bac4f0df20a52e3650ea3ad7e23ae1017a1d578bd2a4b148ca3f7a69f8327`.
- TAB5 version: `0.2.318-ui`, 6,933,920 bytes.
- BIN: `fc6e8d3b680398175db8adf63e1827b3927b0c480b7c659c6e1eededa8e77fb7`.
- ELF identity: `d8e7531e4b41bfb28ab7752728deb4ea9c65ba6e1a9d70d1a80b3dd4802f8c4d`.
- Fixed local image: `artifacts/firmware/tab5/latest/aibot_tab5.bin`.
- Archive: `artifacts/firmware/tab5/versions/0.2.318-ui/fc6e8d3b6803-e42c3772/`.
- Incremental source patch: [TAB5-318-SESSION-SOURCE-20261107.patch](TAB5-318-SESSION-SOURCE-20261107.patch), based on the preceding [silent-controls candidate](TAB5-SILENT-CONTROLS-20261107.md). The artifact source is outside Git; this patch preserves actual changes and new font/test files.

## Validation

Windows Release build succeeded without warnings/errors. Native Harness (10), workspace (66), receipt (23) and dispatch (35) checks passed, together with history regression and `--status-once` from the ordinary Windows profile. Real read-only discovery found both existing sessions with correct source IDs and `canSend=true`; cold catalog readiness was about 4.25 seconds, not a claim about warm device switching latency. No input was dispatched.

Official TAB5 resource-scoped build passed, including gallery stack and draw-backend checks. Targeted native LVGL tests passed for source metadata/capability independence, legacy unknown source, long CJK labels, running header bounds, fourth row, cached completed reply, unchanged Codex layout, reply states, workspace controls, switching, cancelled drafts, project navigation and draw cache repair. Actual rendered PNGs and matching sidecar notes were visually reviewed. The first extended cache test exposed redundant style writes; after correction, 25 full/partial cache frames matched the direct renderer. These local checks alone do not imply device acceptance.

## Physical acceptance

TAB5 `e8f60ae2ec56` 完成一次本地 OTA 传输，精确 ELF 核验为上列 `d8e7531e…`，启动分区 `ota_1`、状态 `VALID`。日常桥接保留固定 Release 路径，原 DLL/PDB 已备份，登录辅助程序未替换。

真实设备经配对 USB 诊断采集 10 段、51 帧画面，包含 5 次会话/客户端切换、两条已有回复的打开与返回。列表、输入区及回复标题均显示对应来源，第四行未知来源标签完整可见；Codex 返回后维持原有布局和运行中操作。VS Code 的 `FOCUS_READY_1107` 与原生终端的 `PC_TERMINAL_READY_1107` 正文一致，采集到的回复画面稳定显示“已完成”，底部无刷新提示；目标会话画面中的发送按钮可用。没有发送、追加或停止模型任务。

采样帧间隔为 2,016–6,079ms，包含 USB 镜像采集和动作确认开销；这是采集画面的检查结果，不据此声称毫秒级切换速度或排除采样间隙内的瞬态。第三方 Harness 使用本地只读适配器夹具验证，不冒充真实第三方接入验收。

部署、真机视觉复核和日常进程收尾分别记录于证据目录的 `installation.json`、`hardware-review.json`、`hardware-final-verification.json`。原有 7 个配对/设置/草稿文件摘要、两条 Claude 原会话记录摘要及 TAB5 展示设置均保留。macOS 与 ESP8266 未修改、未测试；未公开发布。

## English

This local candidate implements the approved source badge layout and a compiled Claude-compatible Harness adapter seam. Display provenance is independent of live capabilities and model/provider identity. Third-party tools still require explicit adapters and acceptance; no arbitrary plugin loader is provided. The Windows bridge and TAB5 binary are independently frozen by the hashes above. Visual/local checks and physical acceptance are separate evidence; no public release, new model message, macOS or ESP8266 validation is included.
