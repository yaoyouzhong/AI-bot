# 图库包同步更新图片与导读 / Bundled gallery guides

2026-10-11 本轮内容包更新：新增 58 件原文，累计 270/867 件，余 597 件待补；年度书法可读 141/366 件。已纳入 `2026.10.11.1` 年度书画及全量书法 ZIP。此轮仅处理图库内容，没有构建或部署桥接/固件；共享工作区同期的其他部署另记。见[本轮来源、包与验证](CALLIGRAPHY-ORIGINALS-2026-10-11.md)。下方先前数字为历史阶段记录。

Content-only update: 58 additions, 270/867 readable originals, 597 remaining; annual coverage is 141/366. Resource edition `2026.10.11.1` includes annual packs and the full calligraphy pack. This round did not build or deploy a program. Earlier counts below are historical.

后续部署状态（2026-10-11）：本文所述程序能力已随桥接 0.6.4 与 TAB5 .168 本地部署，见[安装记录](../TAB5-ACCEPTANCE-168.md)。本次部署验证了 Codex 已读同步；未把内容包正式发布或图库导读真机验收列为通过。下文原有“未部署”描述保留为内容包制作阶段的历史边界。

Deployment update (2026-10-11): the program capabilities were installed locally with bridge 0.6.4 and TAB5 .168. This deployment verified Codex read synchronization; it does not establish gallery-content publication or hardware acceptance of the new guide packs. Earlier undeployed statements below describe the package-preparation phase.

## 2026-10-11续轮结果 / Current continuation

已生成两个本地年度内容包：名画366件/860张图片，书法366件/1782张图片。各自携带简介、欣赏提示、原文核实状态、已核实正文、说明和可携带来源记录。全库原文212件，年度书法99件可读。后续内容更新只需导入对应类别ZIP，无须每次同时更新桥接；首次包内导读与设备内容版本能力仍未部署，当前安装的桥接和.167固件保持不变。

验证覆盖实际生产导入器读取两包全部732件作品、两种方向逐字导读与图片服务；11类打包拒绝、坏包/取消保持原版、相同长度/时间文字即时重读、744次认证请求、最长7390字节原文和原生缓存刷新。认证状态携带类别内容校验值，纯文字更新也触发受影响类别刷新；活动传输结束后清缓存，另一类别仍离线可用，旧位图保留到新图片就绪。主机预览检查全部212件原文字形与滚动结尾，设备真机尚未验收。

交付：`artifacts/development/gallery-pack-guides-20261010/curated-2026.10.11/packs/`，含两类ZIP与SHA-256；完整当前验证记录为`verification-current.json`。单篇原文最多8191 UTF-8字节，完整回复16 KiB；后续内容还须在设备已有字库范围内，任意新增生僻字不能直接当作已支持。

Both local 366-work annual ZIPs bundle images, guides, reviewed originals/status and portable sources. All 732 works pass production import and exact guide/image readback in both layouts. The full catalogue has 212 readable originals (99 annual selections); 655 gaps remain. Import/error/cancellation checks, 744 authenticated loopback/simulated requests, long text, font coverage and deferred category-cache refresh pass. Later content editions need only their ZIP after the initial capability update. No installed bridge replacement, firmware build/install, public release or hardware acceptance occurred. Existing glyph and field limits apply. `verification-current.json` contains current evidence; the following initial-phase record is historical.

## 首轮记录（历史） / Historical initial phase

本轮仅完成 Windows 桥接与名画、书法打包逻辑。未增加内容采集，未修改审定文字目录和原图库，未部署桥接、刷机、提交、推送、打标签或公开发布。验证样本包保存在 `artifacts/development/gallery-pack-guides-20261010/`，不是新的正式作品集。

## 实现

- 两类新包使用 `PACK.json schemaVersion=2`，必含 `guides.zh.json`。简介、欣赏提示、核实状态、原文、原文说明和来源记录随对应作品图片分发；只收录本类别、本包作品。
- 打包器和实际导入器检查 `id/category/workId`、覆盖范围、重复身份、来源、已核实原文非空、UTF-8 字段和完整回复大小。文字与图片均有 SHA-256；全部通过后才整体替换类别目录。
- 运行中的桥接按目录读取导读，导入提交后使目录、图像和文字缓存失效。即使文字文件大小和时间相同，更新仍生效；读取与类别目录切换互斥，避免读到混合版本。
- 损坏、身份错配、超限、缺失、格式错误或取消均在替换前失败，旧图文继续保留；旧目录作为备份保留。Schema-1 包沿用内置审定导读回退；schema-2 缺失导读报错，不替换为内置旧文字。
- 原作释文与明确标注的文学参考文本继续分别登记。未核实或待复核原文不进入读页。既有 guide v1/v2 协商不变，v1 的长文显示升级提示，v2 提供完整正文。
- 打包器与年度精选构建器支持 `--category painting|calligraphy`，可只构建一种内容包。年度构建冻结同一导读快照用于包、审阅副本和预览；作品范围、年度顺序和 JPEG 保持原样。
- 设备已有当天图片和文字缓存时，需要重启设备重新获取；导入后桥接立即更新，但不会推送替换设备缓存或自动重启设备。界面成功提示与图库说明已明确该行为。

## 验证

| 检查 | 结果与边界 |
| --- | --- |
| Windows Release 隔离构建 | 0 警告、0 错误；输出到本轮 artifacts 目录，没有覆盖运行中的 Release |
| 审定目录 | 2,632 件作品身份和字段边界；保留 87 件可读原文 |
| 实际 Python 打包 | 使用已有真实来源、4 件作品和 22 张原生 JPEG，生成两个类别包及另一个书法独立版本；逐文件哈希和类别范围通过 |
| 打包失败检查 | 11 类不完整、重复、错配、字段类型、来源、原文和回复边界案例被拒绝 |
| 实际 C# 导入与读取 | 直接导入打包器产物，两类全部样本作品横竖屏逐字读回导读，并通过实际图片分片服务；书法后续独立日期版同样通过 |
| 类别更新与回退 | 图文同步安装、只改文字即时重读、相同大小/时间缓存失效、另一类别保留、旧包兼容、v1/v2 阅读、坏包和取消不替换、备份保留通过 |
| 鉴权传输 | 744 次请求通过；真实本机 HTTP、USB/BLE 邮箱模拟；运行服务导入后各通道读到新文字 |
| 在线更新流程 | 模拟正式发布接口的下载、SHA-256、导入、图文读取、身份和版本拒绝、断点续传及取消检查通过 |
| 更新界面 | 生产按钮下载/安装/读回与取消、重复点击检查通过；100%/150%/200% 默认/最小窗口 12 张截图，检查实际文字排版 |
| `--status-once` | `dotnet run --project windows-app/AIBotBridge/AIBotBridge.csproj -- --status-once` 返回 1，现有数据目录保护阻止启动；未绕过保护 |
| `git diff --check` | 通过 |
| 真机验收 | 未执行，不把本机 HTTP 或模拟 USB/BLE 称为真机通过 |

复核入口：

```powershell
python scripts/test_gallery_pack_guides.py --output <new-verification-directory>
dotnet <isolated-bridge>/AIBotBridge.dll --self-test-gallery-pack-guides
dotnet <isolated-bridge>/AIBotBridge.dll --self-test-gallery-guide-packs <painting.zip> <calligraphy.zip>
dotnet <isolated-bridge>/AIBotBridge.dll --self-test-art-guides
dotnet <isolated-bridge>/AIBotBridge.dll --self-test-tab5-gallery-transports
dotnet <isolated-bridge>/AIBotBridge.dll --self-test-gallery-updates
dotnet <isolated-bridge>/AIBotBridge.dll --self-test-gallery-update-ui <capture-directory>
```

SHA-256 复核确认运行中桥接 DLL、固定 .167 固件、导读目录和全量图库目录与本轮前已有的原文收集验证记录一致。具体值、测试日志和样本包见本轮 artifacts 中的 `verification.json`。

## Codex 已读状态的额外只读核对

用户同时询问电脑上读过的消息是否应同步 TAB5。正确行为是同一条回复在电脑标记已读后清除 TAB5 未读，后续新回复重新产生未读。当前源码没有实现该同步：`Tab5CodexTasks.SnapshotLimited` 通过 `repliesReady` 发布完成回复身份，`ui.c` 的 `codex_notice_tick/codex_notice_read` 在设备本地维护未读与已查看，没有消费电脑端已读状态。本轮仅定位缺口，没有修改这条链路。

## English

Program and packaging support now bundles guides with images for both categories. Schema-2 imports validate hashes, stable identity, coverage and device limits before one category swap; successful imports invalidate the running bridge's caches. Schema-1 fallback remains, and later category editions do not require another bridge build for text changes. A device retaining today's cached content needs a restart to fetch the new edition.

Release build, real-image producer/consumer samples, atomic import/error handling, 744 authenticated loopback/simulated transport requests and the production update UI tests pass. Standard status startup remains blocked by the existing desktop-profile guard. No content research, deployment, firmware installation, public release or hardware acceptance occurred. A separate read-only audit confirms that desktop Codex read-state synchronization is currently missing and was not changed in this task.
