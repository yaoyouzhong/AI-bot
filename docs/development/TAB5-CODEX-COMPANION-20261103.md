# TAB5 Codex 萌宠本地实现与验证

本文保留 0.2.308 初版的设计、源码补丁与验证记录。后续 Claude 支持、布局、屏保背景与字体及 0.2.312 真机验收见[客户端萌宠调整](TAB5-CLIENT-COMPANIONS-20261104.md)；后续记录替代本文中的当前状态与待办描述。

This document retains the original 0.2.308 implementation evidence. The linked client-companion record contains the subsequent 0.2.312 behavior and acceptance; its current status supersedes the initial pending items below.

## 范围与设计

沿用用户确认的官方蓝色 Codex 萌宠及其双手动作。只放在总览任务卡右侧，并新增可选 Codex 屏保；Codex 任务页面不增加萌宠。总览不增加长状态备注，只保留一个短状态名称；项目名、任务标题和原任务按钮继续承担各自用途。

1280×720 逻辑画布沿用现有界面的色板、原中文字体及控件样式。总览任务卡仍为 760×328；萌宠图片原点为屏幕 `(570,138)`，占位约 176×191，与下方按钮留出间隔，任务标题宽度调整为 480，保留两行省略。项目名单行省略规则不变。短状态沿用工作绿、待处理琥珀和次要灰，受阻使用柔和红色。

屏保使用黑底，萌宠为 384×416、图片原点 `(448,92)`，下方仅一行 30px 状态。左下保留 Codex，右下显示 24px 时分；画面随分钟小幅移动。总览和屏保共用状态、时间线和一个解码缓冲区，切换不重播完成动作。动画只在相关界面可见且没有手势操作时更新；待命缓慢眨眼、工作持续操作电脑、等待低频挥手、完成短暂庆祝后停留。

| 可见状态 | 依据与造型 |
| --- | --- |
| 待命 | 新鲜、明确的 idle；自然站立和眨眼。 |
| 工作中 | 明确生命周期或 owner 运行状态；操作电脑，日志静默不使其停止。 |
| 等你回答 | 答题/选项卡请求；挥手提示。 |
| 等你确认 | 操作授权请求；挥手提示。 |
| 等你处理 | 混合或无法细分的待处理请求。 |
| 已完成 | 既有精确 task/turn 的未读完成内容；短暂庆祝。 |
| 任务受阻 | 明确 blocked/systemError，或 idle 且最新轮次 failed。 |
| Codex 未运行 | 新鲜根状态明确 offline；闭眼歪头。 |
| 状态暂不可用 | owner 失败/观测过期或设备心跳失效；睁眼托腮。保留已有任务/回复。 |

等待处理和受阻任务优先于普通工作任务；没有这些待办时保留既有未读完成内容入口，继续打开对应的精确轮次。旧未读回复不会把同一任务正在执行的新轮次画成完成。不同任务的全局 `needsInput` 不套用到当前萌宠。

## 实现与源码归属

Windows/macOS 增加固定的 `companionState`、`waitingKind`、`outcome`、`turnId` 投影。等待类型只按已识别的请求 method 分类，不包含问题、选项、命令、回答或私有请求正文。未知及混合类型保留通用提示；现有发送、授权、答题和回执流程不变。协议说明见 [PROTOCOL.md](../PROTOCOL.md)。

设备增加 `codex_companion.c/.h` 纯状态解析、动画时间线和有界解码，`ui_codex_companion.h` 集成 LVGL。解码前验证整帧，不用损坏的半帧覆盖可见图片。27 个精选帧的编码为 214,028 字节，调色板 768 字节、帧表 216 字节，单个 RGB565A8 解码缓冲区 119,808 字节。没有每帧分配释放；未提供图片素材时仍可构建并显示短状态。

Codex 屏保使用设备本地 `saver_style=9`，追加到选择器末尾。旧编号、设置 blob 和电脑显示设置协议保持兼容；支持含图库和不含图库的选项映射。

设备源码位于隔离目录 `C:\Users\yaououzhong\Work\outputs\AI-bot-codex-companion-20261103\source`，不修改并行维护的 TAB5 源码树。萌宠增量在 [配套 patch](TAB5-CODEX-COMPANION-20261103.patch)；基线是冻结的 0.2.307 拼音/图库修复源码，来源 ZIP SHA-256：`080899269958a634562babdf8a6640fedc84e9def58438f1a2523bb568d98126`。原基线与该冻结覆盖包的相关生产文件相同，仅需合并最新预览测试；合并无冲突。任务目录保留原始和更新后的基线 ZIP、逐文件摘要及合并记录。

本次没有修改根 VERSION、versions/TAB5 或正式发布清单。初版工程镜像 `0.2.307-codex-ui` 只做离线验证，没有刷入：部署检查发现其额外后缀不符合现有桥接的数字版本能力识别规则。因此仅改本地构建版本为 `0.2.308-ui` 后重新构建，保留原工程镜像证据，并按生产流程准备固定 latest 候选。没有修改版本解析器或降低校验要求。

## 图片来源

用户选择参考 Codex 官方原萌宠。本地素材取自已安装 Codex `26.1027.3384.0` 的内置 `codex` 图集，原图 1536×2288、单格 192×208，SHA-256：`ac2990f24d55372411cebdc66a726e1fcbf5e80ec95378e2d25a6241abbedf88`。未复制旧 esp8266-ai 的第三方动画或源码。

原图归属 OpenAI，不将图片归入 AI-bot MIT，也不从 Codex 开源代码的许可证推定其图片再发布权。原图与生成的 `codex_pet_data.inc/.json` 只留在本地候选；源码 patch 不包含图片和图集数据，Git 忽略这两个生成文件。`scripts/package_codex_companion.py --input <明确提供的图集> --output firmware/main/codex_pet_data.inc --expected-sha256 <摘要>` 生成本地资源与来源清单。正式公开分发素材的权利仍需另行核实；本次没有公开发布。

## 验证与交接

已完成：

- Windows Release 候选构建：0 warnings、0 errors；输出在任务目录 `windows/`。后续部署仅替换固定运行目录中内容不同的 DLL/PDB；其他依赖逐文件核对一致。
- Windows `--self-test-codex-lifecycle` 与 `--self-test-codex-tasks` 通过。覆盖等待类型、日志长时间静默、owner 失败、任务选择、精确轮次、现有发送/历史/回执保护。
- 原生状态/解码回归和屏保 NVS 回归通过：兼容旧编号、新 Codex 编号保存重读、失败保留、坏数据边界。
- 实际生产 LVGL 离屏渲染：9 个状态分别覆盖总览与屏保，另核对“屏幕声音”中的 Codex 选择器，已查看位置、字号、动作、未运行/不可用区别；没有用浏览器草图代替设备渲染。测试检查共用缓冲区/时间线、等待优先级、缓存任务保留、屏保中实时心跳更新和失效，以及选择器保存。
- `git apply --check`：萌宠增量可应用于冻结的 0.2.307 基线；不包含并行图库/拼音修改。

完整 ESP-IDF 固件构建通过，图库栈预算检查通过，原 8 KiB 栈和保护保持启用。实际部署镜像 `0.2.308-ui` 为 **6,837,792 字节**，较 0.2.307 基线增加 219,680 字节；应用分区剩余 **371,168 字节**。SHA-256：`a4b537e8c503711d5bed2baea24befa244bbc69fd839506837f15f615a2e08c2`；ELF 指纹：`4db85d7fe6ef5799a1e954789b0106304e3ab16f7490a8c81fa5bdfca60f2e7f`。构建经 `BuildResources.ps1` 限制为 8 个物理核心、BelowNormal；没有新增萌宠代码编译错误。SDK/BSP 和既有预览代码仍有弃用/未使用警告，不宣称整套原生构建零警告。

生产 `prepare_tab5_firmware.ps1` 镜像与 sidecar 校验通过。初次工程验证输出保留在任务目录 `firmware-check/`；实际部署改用项目固定交付路径 `artifacts/firmware/tab5/latest/aibot_tab5.bin`，历史 0.2.307 镜像仍保留在 versions。新候选的正式远程升级、无线启动核验和运行诊断版本能力均通过。0.2.308 实际 sidecar 已重新渲染并检查，文字与分类可读，截图位于任务目录 `notes-renders-308/`。

源码增量归档为任务目录 `codex-companion-source-delta.zip`，包含 15 个 TAB5 文件、10 个 Windows/Mac 文件、patch、说明及逐文件 SHA-256；不含原图、生成图集或 SDK。ZIP 完整性及内容摘要已核对，patch 实际应用后的文件与当前萌宠源码逐一一致。桥接是在当前含其他未提交工作的 checkout 上构建，包中仅保留本功能增量，不冒充完整发布源码提交。`SOURCE-MANIFEST.json` 记录基准 HEAD、镜像/ELF/桥接 DLL 指纹；后续正式候选须冻结完整源码提交。

## 本地部署与启动核验

2026-11-03 用户提出刷机后，核对绑定设备 `e8f60ae2ec56`，原运行版本为 `0.2.307-ui`。本次只向该设备发起一次升级，开始时间为北京时间 20:13:10。Wi-Fi 分块压缩发送 5,048,980 字节，传输耗时 8,734 ms；完整镜像 SHA 与上述候选一致。

桥接更新前正常退出并备份旧 DLL/PDB，备份位于任务目录 `bridge-runtime-backup/`。仅两文件内容有变化，替换后摘要逐一核对。新 DLL SHA-256 为 `a47bb79dcac12d5d70a2be1e67e2ff39c81b5426ef69e186b42517d8d41be741`。从现有 Explorer 桌面对象启动后台进程，沿用固定 Release 可执行路径和 18765 服务端口；没有修改 AppProfileGuard、配对资料或系统配置。运行诊断确认真实 Roaming 目录、`pairMatchesDisk=True`，配对文件时间与部署前一致，USB COM9 和 Wi-Fi 均恢复。

首次启动版本和 ELF 均匹配，分区一度为正常的 `PENDING_VERIFY`；等待固件连续健康检查后再次核验通过：`partition=ota_0; state=VALID; previousError=0`。核验证据保留在任务目录 `boot-check-308-second.json`、`device-verified-308.json`，初次未通过记录也保留，没有将其改写为成功。没有再次发起安装。桥接的显示模式、轮播开关、间隔和页面顺序逐项与操作前一致；轮播起点随桥接重启重新计时，未执行 ESP8266 恢复命令，也未替用户选择 Codex 屏保。

随后四次运行采样中设备 uptime 从 139,920 ms 持续增至 169,469 ms，界面心跳年龄为 10–30 ms、显示错误为 0，新驻留进程没有收到崩溃记录；采样见 `runtime-samples-308.jsonl`。最后一次精确启动核验仍通过，见 `boot-check-308-final.json`。这是短时间启动稳定性证据，不替代长期运行、TF 同步或完整交互验收。

限制与待办：

- 当前工具直接启动的 `--status-once` 会触发 `AppProfileGuard`。后续通过现有 Explorer 桌面对象启动同一候选，`--status-once` 返回 0，保护正常通过；正式驻留进程的 `/status` 也确认 Codex 工作状态和桥接心跳正常。没有使用跳过保护的自测参数。
- Mac 已补投影与 XCTest 用例，但当前为 Windows，未运行 `swift test` / `swift build -c release`，也未进行 Mac 实机验证。
- 已完成本地桥接部署、TAB5 升级与精确启动核验；没有推送、打标签或公开发布。生产 LVGL 离屏画面已检查，尚未取得此次升级后设备实拍或屏幕回传，不能宣称各萌宠状态、触控、屏保进入/退出均已完成真机视觉验收。Codex 屏保由用户在“设置 → 屏幕声音”主动选择。图库/输入专项验收仍由原任务继续。
- 任务隔离目录为交付源码、来源和构建证据保留；它不是 Git worktree，没有新增任务分支。

Mac 交接：在同一冻结桥接源码上运行 `swift test`、`swift build -c release`，核对答题/授权/混合类型与断开后的 unknown，再按既有桌面统一流程记录包摘要和实际验收。设备交接：已安装候选供本地体验；后续补真实屏幕上的各状态区分、屏保进入/退出与持续运行验收，保留用户当前展示设置。
