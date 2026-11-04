# Claude 会话与 TAB5 总览对齐（开发中）

## 范围和源码身份

- 来源平台 / 接收平台：Mac 开发验证，Windows 与 TAB5 共同对齐；发布负责人仍为原统一负责人。
- 主仓库 `https://github.com/yaoyouzhong/AI-bot.git`，本工作树基线 `0dc59134626e110f25e427bc6d7c4f61e64a8a85`；适配仍未提交、未推送。
- 用户要求验证完成前不发布；后续已明确授权临时切换 Mac 候选并升级 TAB5 做验收。现有展示设置必须保留，下文早期构建记录不代表后续部署状态。
- 用户确认这次设备行为属于 0.2.312 之后的新固件能力，拟使用 `0.2.313-ui` 候选。原始配置试编译在链接前停止并重配为 0.2.313，未生成或安装新的 0.2.312 镜像；候选版本、变更说明与最终哈希需在验证后登记。
- 用户最初授权发送现有名称，后续明确扩展为点击时只读查看对应 Claude 主会话的助手公开回复，并要求普通功能默认可用、无需额外应用内授权。不得发送推理、用户提示词、工具输入输出、原始会话 ID 或路径；不增加 Claude 发送和中断入口。
- 共同协议为 v1 可选 `claudeSession`：见 [协议](../PROTOCOL.md)。选择行为由设备实现，电脑两端提供真实活动时间与名称；缺失字段兼容回退。

## 对齐清单

| 项目 | 状态及证据 | 剩余工作 |
| --- | --- | --- |
| 会话名称 | 两端候选已接入后台索引、身份事件和配对 TAB5 状态帧；Mac 配对帧与 Windows 索引／降级／身份合成测试通过。 | 没有身份事件时仍隐藏无法确认的活跃标题；需 Windows 原生及真实设备验证。 |
| Claude 完成 | 两端的无绑定 Stop 保留待确认并标记未知，不生成完成。真实 CLI + 模拟 API 重现 Stop 被拦截后继续。 | 尚无适用于已有独立 Claude 会话的可信最终结果生产者，不得推断成功。 |
| TAB5 选择与标题 | 原归档的 0.2.312 实际工程已在隔离副本整合；源码补丁见 `TAB5-COMPANION-312-SESSION-20261104.patch`。选择函数 12 项、标题回退 3 项通过；真实 LVGL 测试覆盖单选、双选及同客户端标题，短名称和长中文名称的离屏图已检查，长标题两行省略、没有遮挡萌宠或按钮。 | 0.2.313 已完成目标固件构建，但真机验收未做；Windows 原生和三通道验收分别记录。 |

## 构建物与验收记录

- 0.2.312 工程来自该主干的 `docs/development/tab5-312-source/`，ZIP 26,126,525 字节，SHA-256 `8e47299f88d9cd2f1fed56ca2518112052912dcbe64a4b3599487d888000555b`；内外清单逐字节一致，归档 1,143 文件摘要及 1,142 项清单核验通过。
- `TAB5-COMPANION-312-SESSION-20261104.patch` 针对该归档 `source/`；原始 `ui.c` 与头文件为 CRLF，已对干净原始工程用 `git apply --ignore-space-change --check` 验证。仅复制修改到独立候选，不改原归档。
- 原验收应用 SHA-256 `21d78bcce33bdbdb531588b225b59a9902e53ecff620c9b614863f56cdb34d8c` 仅用于识别基线，不代表此次修改后固件已验收。其独立来源 `sourceCommit=null`。
- 隔离副本的 7,594 项已解析构建输入仅 3 项有意变化（`ui.c`、`ui_claude_companion.h`、`claude_companion_ui_test.h`）；SDK 基线 `f5c3654a1c2d2a01f7f67def7a0dc48e691f63c0`、NimBLE `0225775e49669cd4f070400908f73718bddce83b` 和 9 个 SDK 修改文件均核对通过。原 sdkconfig 未改，候选配置参数为 `TAB5_LOCAL_ART=ON` 与 `TAB5_LOCAL_VERSION=0.2.313-ui`。
- Codex 与 Clawd 素材原始来源、用户授权及非 MIT 归属见归档 `supplied-art/NOTICE.md` 和 `source/licenses/companion-art/NOTICE.md`；不得扩大授权。
- 离屏预览可按 `tools/tab5-preview-macos/README.md` 重现。短名称图 SHA-256 `41d25e4e98030b9ddf880902745b654fdf86a93e4005529b5fc5487ea5242bfc`；长名称图 SHA-256 `68c97afecb4d92d66a6881b05e38397ab4a9f0cc65041315cf28ce23d4d74fb3`。图像使用合成名称，非设备照片。
- 0°/180° 横向切换的名称控件宽高与选中客户端断言通过；两张 LVGL 离屏快照相同，不能因此声称物理屏幕翻转方向已经验收，仍需真机检查。
- 设备镜像编译后仅补充了预览测试的 180° 场景，运行时代码未再修改；`ui.c` SHA-256 `fb8133798f872494e5a1fdcb75975d15a0df9df6a16cf4530145f4a7f8a974a8`，`ui_claude_companion.h` SHA-256 `6dd64db84a6dd7ae39ed0c9b825c95b71adfd97f0074d50dc17c6bf3d0bbc179`。因尚未生成统一干净提交，不能将该工作区试验包当作正式候选身份。
- `0.2.313-ui` 在本机隔离工程及 SDK 上完成 ESP32-P4 编译，应用镜像 6,908,320 字节，SHA-256 `4033defb3c8377fb9f117013295849b6fcdc52697f1586a810819278aa75fc0f`，ELF SHA-256 `3b40d3116c4fa3b39dc138750755e399ba61e379fda3d009fb59865f7146c81b`。镜像格式、内部 checksum、版本和应用标识通过，应用分区剩余 `0x49660` 字节（约 4%）。构建包含基线源码的弃用及未使用符号警告。`prepare_tab5_firmware.ps1` 以生产加载器验证并生成本工作树独立 `artifacts/firmware/tab5/latest/`，未覆盖原工作区。最终 sidecar 的实际离屏预览通过并人工检查，截图 SHA-256 `4752b06b914b572afd8f254cb2a49b985e69721989f172ca6aae303dec045887`；尚未安装或刷写。
- `manifest.json.notesSha256` 是规范化后说明**正文**的 SHA-256：`16cd359e4ae9fdc2da435f6396e8868ecede4be19ea2b627655cd760c461696a`；整个 `aibot_tab5.bin.notes.json` 文件的 SHA-256 是 `8352a063247e2300d2416cc5dc1c4a0eb5d7f59e82d6d91f91fdb51afdd227fa`，二者是不同输入并非不一致。已从实际文件重新计算，且 sidecar、manifest、镜像的版本及镜像摘要一致。
- Windows 工程在 Mac 交叉编译通过，且索引、状态降级和 Stop 合成测试通过；不代表 Windows 原生构建、`--status-once` 或设备通过。
- Mac 最新基线的 445 项自动测试及 Release 构建、离线打包通过。试验包 `artifacts/claude-session-validation/mac-prototype/AI-bot-bridge-v0.6.17-macos-arm64.zip` 为 18,767,920 字节，SHA-256 `fc3d4ae3556e81d1f89d8c59cd1deab763c6a582e3679568db65c73deec0e890`；嵌入的基线为 `0dc59134626e110f25e427bc6d7c4f61e64a8a85` 并明确标记 `working-tree-changes=yes`。实际签名为本机 `AI-bot Local Development`，未验证公证；文件是未提交工作区的离线试验包，不是统一源码 SHA 的最终候选，不能发布。
- 真实设备和账户状态尚未验收，不能将历史其他候选或老版本验收套用到本次修改。

## 发布交接

- 共同候选源码 SHA、包哈希、真实设备与原生系统验收尚未确定；未推送、未建标签、未发布。
- 先完成可信事件源、Windows/Mac 原生验证与固件预览、目标设备验证，再由唯一发布负责人按既有流程处理。
# 本次增量：精确查找基础

Mac 与 Windows 候选会话索引支持按本地 session ID 精确查询；未知、删除或子会话 ID 不回退到其他会话。异步采样在目标变更时清空旧结果，并丢弃先前目标的在途扫描结果。ID 仅用于本地匹配，不进入设备帧。后续身份接入已补充调用方和受控结果来源，详见 [身份事件候选](CLAUDE-IDENTITY-EVENTS-20261104.md)；普通交互会话的通用最终完成及 Esc 中断仍不具备可靠来源，不得宣称全量会话或真机验收完成。

文中先前记录的 Mac 离线试验包构建于本次增量之前，不包含这一索引改动，不可作为当前工作区源码的验收包。

Claude Code 交互模式隔离试验使用临时 HOME、临时配置和模拟本地 API，未接触真实账号或会话。正常回复后观察到同会话、同 prompt ID 的 `Notification: idle_prompt`；首次 Stop 被另一 hook 拦截并继续时先出现两次 Stop，再出现空闲通知；模拟 API 失败时先出现 StopFailure，约一分钟后仍出现空闲通知。因此空闲通知只能表示可观察的空闲，不能单独证明成功。它还受无输入、后台任务和使用额度等条件限制，可能不出现。两端身份事件已在后续候选接入，但空闲通知仍不得作为成功依据；覆盖边界见身份事件候选文档。官方依据：https://code.claude.com/docs/en/hooks#notification 和 https://code.claude.com/docs/en/hooks#stop 。

补充反例：模拟同一轮首次 Stop 被拦截后接口失败，依次观察到 Stop、StopFailure 和 idle_prompt；它们对应同 session ID、同 prompt ID，先前的 Stop 和后续空闲通知均不能覆盖失败。中断、事件丢失和并发乱序仍需验证。


## 续接：Claude 只读会话与默认功能（2026-11-04）

- 继续原「对齐 Windows 功能并准备新版本」会话的工作树，未合并或覆盖主工作区。设计沿用 [阅读页设计](CLAUDE-REPLY-READER-DESIGN-20261104.md)，普通功能默认可用，不添加 Claude 授权开关；现有设备与数据源停用选择仍生效。
- Mac / Windows 白名单解析、固定会话绑定、UTF-8 分页和加密防重放 RPC 已接入。新增修正：未知回复格式返回不可用，不能变成空成功；设备数据停用后清空回复并禁用分页与重试。身份事件的普通交互会话终态覆盖限制仍见 [身份事件文档](CLAUDE-IDENTITY-EVENTS-20261104.md)。
- Mac 全量 454 项测试及 Release 打包通过；包内摘要、固定本地证书签名、离线刷机工具检查通过。实际包再通过真实 Claude CLI＋本机模拟 API 的原生 hook、成功、失败和中断链路；未读取真实账户或对话内容。Mac 功能设置原生离屏预览、深浅色和最小尺寸通过，已查看实际图像，无截断问题；夹具中的停用状态不代表首次默认值。
- Windows 全工程在 Mac 上交叉编译通过（0 警告、0 错误），可移植 C# 身份／索引／公开回复回归通过；不替代 Windows 原生构建和 `--status-once`。
- TAB5 实际 LVGL 预览覆盖加载、公开回复、滚动、分页、固定会话标题、数据停用及 0°/180°；已查看正文和实际 sidecar 渲染。常见缺字表情以明确文字标记显示，传输内容保持 UTF-8。物理触控、屏幕方向和读取体验仍须用户真机观察。
- 固件编译先因缺少 `ESP_IDF_VERSION=5.4` 丢失 C6 配置，恢复原始 sdkconfig 后重新配置，所有配置值与归档一致；随后修正阅读头文件四处目标编译器禁止的误导缩进，ESP32-P4 构建通过。镜像 6,915,024 字节，分区剩余约 4%，保留现存未使用符号警告。未修改 ESP8266 固件或执行轮播恢复。
- 当前 Mac 包：`artifacts/claude-session-validation/mac-reader-acceptance/AI-bot-bridge-v0.6.17-macos-arm64.zip`，18,811,296 字节，SHA-256 `9e7e756d688e183c634e1b465f66ee5f700ef52852796a1fad1b38a0e6a3718c`。签名为 `AI-bot Local Development`，未验证公证。它明确来自未提交工作区，不能视作统一干净源码的正式候选。
- 当前 TAB5 交付路径仍为 `artifacts/firmware/tab5/latest/aibot_tab5.bin`，版本 `0.2.313-ui`，SHA-256 `f9b5f701053238ee23275ac35e3569a2128fa2c4bc522daaa453c63c39828e63`，ELF SHA-256 `8e9775644770b513582e8bd4daea270c40822b1978677597b3ddf3bbaf744b1c`。通过生产校验器归档为 `versions/0.2.313-ui/f9b5f7010532-b0eb291a`；该路径内 manifest 的 scope 是准备时描述，实际部署结果以升级报告为准。
- `TAB5-COMPANION-312-SESSION-20261104.patch` 已更新为完整 12 文件增量（含阅读实现与测试）；对原始 .312 归档无额外忽略参数应用成功，逐字节重建一致。身份清单在 `artifacts/claude-session-validation/tab5-reader-source-delta.json`。
- 本轮已正常退出上一临时桥接，使用实际包解压出的程序执行 USB 升级；进度及展示设置保留结果记录于 `tab5-reader-upgrade-report.json`。新阅读页的真机触控观察尚待完成，不把既有萌宠大小／位置确认当作阅读页通过。完整候选身份及验证状态见 `artifacts/claude-session-validation/READER-ACCEPTANCE.json`。
- 未提交、推送、打标签或发布。后续完成真机阅读、普通 Claude 实际事件联动和 Windows 原生验收，再确定同一完整源码的最终候选；不得将本轮本机试验包直接发布。

## 用户追加：后台画面与自动操作（2026-11-04）

用户原计划次日人工验收，随后明确要求后台读取 TAB5 画面，并开通自动操作以便自动验收。因此新增本地诊断入口，复用原配对，不打开电脑前台窗口，不增加应用内授权开关。设计、运行方法及范围见 [自动验收设计](TAB5-AUTOMATION-DESIGN-20261104.md)和 [工具说明](TAB5-AUTOMATION.md)。普通桥接不主动发布触摸命令。

- 已取得真实 1280×720 USB 截图，最初为年度进度屏保；并非离屏模拟。仅截图的工具不注入触摸，截图只保存到本次指定的本地证据目录。
- Windows/Mac 接入共同的短时命令和精确应答引擎。首版自动操作限已认证 USB，Mac 提供逐行 JSON 的 `--tab5-automation` 入口。真实 LVGL 指针测试覆盖四种方向、实际滚动、范围、重复、过期/通道拒绝和取消释放。
- Mac 最终 456 项测试及 Release 构建、固定本地证书签名检查通过。Windows 可移植回归及全工程交叉编译通过（0 警告、0 错误）；Windows 原生运行与其命令行封装仍未验收，不将 Mac 入口称为 Windows 已可用。
- 新 TAB5 镜像仍为未发布的 `0.2.313-ui`，6,918,224 字节，SHA-256 `1684f7aae7104b421625f3bc55226ef6e79f52a00aa39c0103ae678619aca8c6`；此前 reader 镜像保留归档，不能只凭同版本字符串混用结果。生产校验器生成固定路径与实际 sidecar，sidecar 的真实 LVGL 预览已查看。USB 升级报告 `tab5-automation-upgrade-report.json` 确认精确 ELF、VALID 分区、三个实时 UI 样本及 `settingsPreserved=true`。
- 真机已自动点击总览长按钮，打开“自动验收会话 A”的合成公开回复，标题/萌宠/内容归属一致；上一页从 3/3 到 2/3，滑动后同页可见段落从 17 移至 26，下一页恢复 3/3，返回后恢复总览。截图位于 `artifacts/claude-session-validation/tab5-automation-live-probe/`，已经逐张查看。它证明设备上实际 LVGL 与读取链路，不代表物理触摸面板手感、所有方向或 Wi-Fi/BLE 阅读已验收。
- 原始 .312 工程的完整增量现覆盖 15 个文件，对干净归档应用并逐字节重建一致；保留原文件换行，清单见 `tab5-automation-source-delta.json`。最新自动操作工具路径为 `artifacts/claude-session-validation/mac-automation-final/AIBotBridge.app`，是本机未提交工作区的诊断构建，不是跨平台统一源码的发布包；此前 reader ZIP 身份不变。
- 后续人工只补物理触控手感和未覆盖的实际客户端/通道场景；Windows 原生及普通独立 Claude 可信完成来源的既有限制仍保留。未推送、创建标签或发布，不因自动点击链路通过而越过发布门槛。
