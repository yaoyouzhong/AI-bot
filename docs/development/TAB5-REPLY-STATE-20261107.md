# 回复页加载状态与 Codex 对齐（未发布）

后续更新：本页记录的 `da8ed059…` 已完成安装验收；用户随后要求去掉底部刷新提示并缩短会话发送就绪等待。配套日常桥接和新固件 `3140a12c…` 已安装，并完成五次会话切换、四次查看回复的真机检查，见 [回复静默刷新与会话发送就绪](TAB5-SILENT-CONTROLS-20261107.md)。下文保留本阶段原始证据。

## 当前交付状态

Windows 日常桥接已安装 DLL `2342238d69f67ae212e783f3c7adfcb800acefb6da52d9cb9039a742764b3ba8`，替代此前 `7d23a00e…`。只替换 DLL/PDB，保留登录辅助程序；五个设置、设备和配对访问文件摘要不变，旧文件可回退。安装后的 `dotnet run --no-build --project windows-app/AIBotBridge/AIBotBridge.csproj -c Release -- --status-once`、唯一日常常驻、HTTP 状态及 TAB5 USB/Wi-Fi 检查通过。

用户已批准安装并验收此精确候选。BIN `da8ed0590fac6013f2a3a0a23e66c8a063b19573672538f7a60403777add4bcc`，ELF `52a5415f3c9380d17d022fec18c707aef30d8dade6baa4a3419f0451b0341319`，6,926,544 字节，版本保持 `0.2.318-ui`。固定交付入口是 `artifacts/firmware/tab5/latest/aibot_tab5.bin`，归档为 `versions/0.2.318-ui/da8ed0590fac-63563085/`。本轮仅发起一次本地 OTA，Wi-Fi 分块传输完成，USB 启动核验确认上述 ELF、`ota_1` 分区和 `VALID` 状态，替代此前 `93747165…`。安装前后五个电脑端配置摘要及设备实际展示设置一致。未推送、打标签或发布，未新增模型请求。

## Codex 对照与原因

用户观察 Claude 回复页依次出现连接提示、缓存更新提示、已完成，要求参考 Codex。

`Tab5CodexTasks.SnapshotLimited` 同时发布 `responses`、`receipts` 与历史 `startedLabel`。Claude 的直接 read 响应已有 receipt，但周期状态包没有 `receipts`，历史视图也缺少 `startedLabel`。已补齐现有协议字段，状态与当前可见回复一起更新并限制在当前目录中；读取复用已解析的原生会话实例，避免 `ObserveNative` 再次查找。此处不声称已经量化时延改善。

两种客户端共用设备端回复页。旧 `codex_reply_refresh` 在只读通道暂时未就绪时立即改写回复状态；`render_codex` 随后又用缓存刷新文案覆盖原有结果。缓存书签还从周期包取 receipt，会丢掉 Claude 直接响应中已有的完成状态。专用回归在旧代码打开已完成缓存时稳定失败，初始状态为连接不可用；因此不能只补桥接字段，或把问题归结为模型生成慢。

## 交互与实现

设计保持现有 1280×720 布局、24px 中文字体、标题/状态/正文层级及按钮尺寸和配色。有公开正文时立即显示原正文和其结果，后台刷新不改为连接或缓存警告。持续等待超过三秒时，仅单页回复的既有次级位置显示“稍后更新”；多页继续显示页码。首次无正文的读取使用中性加载文案，原十二秒导航超时与返回重新打开路径保留。历史页保留历史标识、日期、页码和阅读位置。

没有放宽通道新鲜度、认证、送达未知保护或跨客户端/会话修订校验。直接回复写入缓存时优先保存同一直接响应的 receipt。完整设备改动为 [增量补丁](TAB5-318-REPLY-STATE-20261107.patch)，基于前一切换优化候选源码。

## 验证与限制

- Windows Release 隔离构建零警告、零错误；history、native-workspace（59 项）、native-receipt、native-send 和 status-once 全部通过。新增回归检查周期包中的正文/完成状态、历史日期，以及停止后不能沿用旧完成状态。
- 设备端旧代码的专项回归失败，修复通过：Codex/Claude、缓存完成态、无周期 receipt 时保存直接状态、历史日期与页码、首次读取、延迟就绪、一次读取失败、恢复成功、超时后重新打开和零发送。
- 工作区一致性、切换同步及取消草稿刷新三个既有专项通过。当前源代码的完整预览套件在自动轮询断言 `preview_reads==before_poll+1` 失败；另建独立基线，使用保存的改动前 UI 和改动前 preview，同一断言也失败。保留两份日志，不修改或绕过该断言，不宣称完整套件通过。本次涉及的状态与历史分支由独立专项覆盖。
- 实际 LVGL 渲染已查看完成态、持续等待、首次加载和超时页面，文字可读、未增加布局跳动；实际 sidecar 首屏和滚动后中英文说明也已查看。预览与下述真机验证分别记录。
- 官方资源受限固件构建、栈预算、绘图后端及正式镜像校验通过，保留未改代码的既有警告。最初通用预览工程配置因旧 read-ack 提取标记不匹配而失败，随后使用本任务此前一直使用的 native preview 工程；未修改产品 read-ack 来绕过工具错误。
- 曾准备使用配对 HTTP read 做时延采样，工具自动批准审查在执行前拒绝该命令，只给出 `blocked by policy`；未执行或改写重试该命令。本轮使用已有只读诊断和本地回归，不报告新的端到端读取时延。

## 本次已授权的安装与真机查看回复

安装使用日常桥接的同用户 IPC，只对已配对设备发起一次 OTA，没有重建或更换获批镜像。固件传输 5,044,210 字节，完整镜像 6,926,544 字节；启动核验确认精确 ELF、分区及有效状态。设备展示设置在安装前、安装后和交互验收后逐项比较，包括亮度 79、音量 70、当前模式、轮播、屏保、页面启用和顺序。

交互验收使用当前 Windows 源码构建的专用只读 USB 驱动，沿用实际原生会话和配对，禁止 send、steer、interrupt 和 receipt-confirm。它临时接管 USB，结束后恢复已安装的日常桥接。两条 Claude 原会话共完成六次有效打开：VS Code 首次查看、两次返回重开、原生终端首次查看和重开、从原生终端切回 VS Code 再查看。六组连续采集共 27 帧，包含点击前输入页；已逐组检查全部状态标题、正文和次级提示。捕获到的回复页均保持“已完成”，对应正文分别为既有 `FOCUS_READY_1107` 和 `PC_TERMINAL_READY_1107`，未观察到连接/缓存/完成三段切换。两次 VS Code 重开中，底部短暂出现“稍后更新”，完成状态和正文保持不变。

采样间隔为 1.828–5.828 秒，不是高帧率录像，不能仅凭这些帧排除帧间瞬时闪烁，也不把自动化回执耗时当作用户点击延迟。初始、等待和失败刷新分支另有上述旧代码失败/新代码通过的确定性回归。第一版连续预览下的 80 ms 模拟按压虽然收到动作回执，却没有进入回复页，因此未计入通过；验收驱动改用低于长按阈值的 320 ms 原地手势后，真实页面切换得到画面确认，没有修改设备镜像。第二次驱动启动后的第一组画面属于 Codex 活动会话，虽然目录名仍为 `vscode-reopen-1`，也未计入六组 Claude 完成态验收。

最终已恢复日常桥接 DLL `2342238d…`，唯一正常常驻、`--status-once`、HTTP 200、COM9 USB 及 Wi-Fi 桥接健康均通过；五个配置文件、设备展示设置及两条原生会话的完整 JSONL 摘要不变。实际 Claude 加密草稿文件摘要不变，全部诊断流量只有 read、read-ack、draft-load，零 draft-save、send、steer、interrupt 或 receipt-confirm。

草稿审计保留一项限制：首次校验脚本从 Codex 打包进程读取 `tab5-codex.dat` 时，被 Windows 重定向到 `Packages/OpenAI.Codex_…/LocalCache/Roaming` 中的旧副本。文件句柄物理路径和摘要均已证实这不是日常桥接所用的桌面文件；早先清理会话时保存的该文件备份也来自这份旧副本。因此原先两份 Codex 摘要不可比较，首次校验失败记录予以保留，不能报告实际 Codex 草稿通过前后字节校验。实际桌面文件最后写入时间早于本次安装，且验收没有草稿写入操作；这些是辅助证据，不把 mtime 单独当作字节一致证明。该限制不影响六次实际回复查看、Claude 草稿、设备设置与运行健康的已验证结论。详情为 `draft-baseline-verification.json`、`hardware-final-verification-first.json` 和 `hardware-final-verification-shadow-baseline.json`。

证据位于 `artifacts/tab5-reply-state-20261107/`：构建与离线部分为 `bridge-validation.json`、`daily-install/result.json`、`daily-final-verification.json`（安装设备前的历史检查）、`baseline-case.log`、`extended-case.log`、三个 `TAB5_*_ONLY.log`、`full-preview.log`、`baseline-full-case.log`、`firmware-build.log`、`prepare-firmware.log`、`fixed-preview/`、`notes-preview/`；本次部署与真机部分为 `installation.json`、`device-nav-before-da8/`、`device-nav-reply-da8/`、`device-nav-reply-da8-v2/`、`hardware-visual-review.json` 和 `hardware-final-verification.json`。此次未修改或验证 macOS，不将 Windows 字段对齐写成 Mac 已交付；Mac 端如缺少相同字段，需在原生测试后对齐。

English: The daily Windows bridge publishes reply receipts and history labels alongside content, matching the existing Codex snapshot contract, and reuses the resolved native client during reads. The authorized exact TAB5 image was installed once and verified by its ELF hash and valid boot partition. Six actual openings across the existing VS Code and native terminal Claude sessions were reviewed in 27 USB frames, including pre-click composer frames. Every captured reply frame retained its correct completed status and content. A secondary delayed-update hint appeared in two cases without replacing the result. Capture gaps were 1.828–5.828 seconds, so the images alone do not exclude sub-frame flashes. Dedicated deterministic regressions cover initial and refresh transitions; the old implementation fails and the fix passes. The daily bridge is restored and its status, USB and Wi-Fi checks pass. Device settings, five configuration files, the Claude draft journal and both original transcripts are unchanged. The Codex draft hash baseline was a different packaged shadow file, so that byte comparison is explicitly inconclusive; its old mtime and the absence of draft-write RPCs are supplementary evidence only. The broader preview suite still has the same polling assertion failure on saved pre-change and current sources, and is not reported as passing. No new model message, publication or macOS validation is claimed.
