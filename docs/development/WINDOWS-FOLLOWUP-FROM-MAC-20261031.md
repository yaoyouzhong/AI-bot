# Windows 对齐清单：Mac R47 / TAB5 .298

最新共享设备候选补充：.299 已构建，尚未安装；精简普通日期标签、放大秒数，Windows 无需修改桥接。应用 .298 后继续应用 [时钟可读性补丁](../proposals/tab5-clock-readability.patch)，核对[来源哈希](../proposals/tab5-clock-readability.json)；设计和验证见[时钟记录](TAB5-CLOCK-DAY-DESIGN-20261031.md)。

本清单是 2026-10-31 的最新开发入口，优先于 [10-29 清单](WINDOWS-FOLLOWUP-FROM-MAC-20261029.md) 中的历史状态。先拉取包含本文件的最新主干；不要重复实现已同步的代码，也不要把 Mac 实机通过当成 Windows 已验收。

## 本次合并范围

- Mac 任务页纯文字追加使用已核验会话的原生发送／排队／引导动作；保护电脑已有草稿、附件与引用，不再误把工具栏装饰图像当附件，也不跳过附件控件的子树。
- 长会话定位跳过已识别的历史正文区域，保留目标唯一性和输入区检查；明确操作补充会话活跃时间，后台轮询不影响排序。
- 已接收或可靠入队即算交付成功。未知结果不重发；纯查询可用短期回执基线核实；用户可在 TAB5 核对并确认已送达。旧 Mac 菜单恢复入口继续兼容。
- 本机后台诊断只保存固定状态和结构／数值信息，不打开前台窗口，不保存消息正文、附件、真实会话／请求标识或凭据。
- 封面使用跨通道共享的有限重试和确认等待；JPEG 未确认时可尝试同尺寸 RGB565 兼容传输，兼容路径也有上限，不缩小宽图。
- TAB5 同版本显示“当前版本，可重新安装”；安装／重装有确认弹窗；新增明确拦截提示、自动查询及核对送达弹窗。共享设备补丁以 .298 为最新。

## Windows 执行清单

| 编号 | 优先级 | 当前状态 | 开发及验收要求 |
| --- | --- | --- | --- |
| W31-01 | P1 | 需核对并补齐原生追加行为 | 检查 `Tab5CodexTasks` / `Tab5QuickConsole` / `Tab5CodexComposer`。追加面向所选会话最新轮次，服从 Codex 当前排队／引导偏好；一次动作只提交一次。参考 `MacTab5Desktop.appendTask`。验证空闲发送、运行中引导、排队成功，不以“模型开始执行”作为送达标准。 |
| W31-02 | P1 | 需按 Windows UIA 控件验证 | 电脑已有文字、真实附件或引用时明确拒绝并保留两端草稿。工具栏装饰图像允许，实际附件及移除按钮必须检查全部后代，不能整棵子树免检。参考 `attachmentFreeLayout` 和 `MacTab5TaskAppendValidation`。 |
| W31-03 | P1 | 回执确认能力需继续补齐 | 未确认不重发，不回退第二种提交接口。用动作前完整基线与动作后新消息／排队回执比对，拒绝历史同文和旧队列迁移。Mac 复用原基线，最多 64 项、60 秒，只在内存保存必要摘要；Windows 从 `RecoverReceiptAsync` 和既有队列接口接入。保留 [W29-04 的完整判定条件](WINDOWS-FOLLOWUP-FROM-MAC-20261029.md#排队确认的实现要求w29-04)。 |
| W31-04 | P1 | **协议和 journal 已同步**，原生实测待做 | `Tab5CodexJournal`、`Tab5CodexTasks`、`Tab5Service` 已实现 `receipt-confirm`、设备绑定、内容指纹、幂等和 `canConfirmDelivered`，并补 journal 自检。确认操作只登记原请求送达，不调用桌面提交；重复确认不得清掉新草稿。验证错误设备、变更文字／图片／轮次、旧记录无设备绑定、权限关闭、重复确认及旧设备兼容。 |
| W31-05 | P1 | Windows 错误映射需对齐 | 返回 `desktop_draft_present`、`desktop_attachments_present`、`desktop_foreground_unconfirmed` 等具体原因；未知保持 `delivery_unknown`。不要用“请求已处理”替代失败原因。TAB5 .298 已有对应显示，桥接按实际能力返回，不伪造成功。 |
| W31-06 | P2 | 定位、排序及性能需核对 | 检查长会话是否遍历历史全文而耗尽定位预算。只跳过有结构证据的历史区域；保留不明确目标的拒绝。精确会话 ID 优先，标题回退必须唯一，时间排序不能替代身份确认。明确定位／提交可更新有界本机活跃时间，轮询不能提升排序。Mac AX 类名及 `AXManualAccessibility` 不适用于 Windows。 |
| W31-07 | P2 | 后台诊断需核对 | 提供用户专属、有界的固定状态／结构计数／设备数值诊断；不能自动打开、切换或置顶窗口，也不要求用户抄日志。记录前后时间，不能把旧错误当成当前失败。参考 `MacTab5LocalDiagnostics`。 |
| W31-08 | P1 | **Windows 封面逻辑尚未同步** | `Tab5Assets.NextChunk` 仍可无限循环。补同一资源跨通道三轮预算、轮次间两秒确认等待；相同资源刷新不重置预算，后续新封面独立计数。保留现有随机分块策略。参考 `MacTab5Assets`。 |
| W31-09 | P2 | 封面兼容恢复待补齐／实测 | JPEG 三轮未确认后，协议允许宽度 ≤336 的图像可在主机解码为同宽高 RGB565，复用 `rle565`；转换最多一次，备用传输仍有限。宽图不能偷偷缩小，失败不得清空最近成功的缓存。验证像素方向、尺寸、迟到确认、新曲目及 USB/Wi-Fi/BLE 混合调度。Mac 真实备用路径尚未触发验收，不将其当作已证明的底层 JPEG 根因修复。 |
| W31-10 | P1 | **共享 TAB5 补丁已提供** | 按下述补丁链更新独立设备工程；Windows 桥接无需重做设备同版本标签和弹窗。保持候选编号、sidecar、说明和 SHA-256 一致。不能仅凭版本号相同认定重装已完成。 |
| W31-11 | P2 | 原清单仍需逐项核对 | W29-01/02/05：题卡回答、多题切换、录音目标绑定与回填内容绑定；W29-07：电脑音源；W29-08：连续录音、原文和末尾光标；W29-09/10：历史读取与语音延迟；W29-11/12：持久回执、重装／传输能力。详见旧清单，已正常的 Windows 功能不要重复改造。 |

优先级表示开发顺序，不代表已在 Windows 复现对应错误。设备照片追加仍是 Mac 原生适配器未支持的能力；不得把“保护电脑已有附件”误读成全面禁止用户主动发送照片。

## 协议与兼容边界

[协议正文](../PROTOCOL.md) 已登记：`receipt-confirm` 是写操作，必须使用既有认证通道、当前 session 和原 requestId。核对设备、会话、纯文字、expectedTurn、图片为空及当前草稿指纹；成功返回原有 202 回执。`canConfirmDelivered` 只表示该未知记录允许用户确认，不表示已送达。

设备最多执行三次纯查询；仅在桥接声明可确认时显示人工确认流程。旧桥接没有能力字段时仍只查询。结果未知不能自动清空、删除 journal 或再次发送。恢复会丢失的内存基线不能在桥接重启后凭空重建。

## TAB5 补丁应用顺序

基于主干已保存的 .293 高清预览补丁链，在独立 TAB5 工程按顺序应用并逐项核对 JSON 内 before/after 和 patchSHA256：

1. [同版本显示 .294](../proposals/tab5-ota-same-version-label.patch) / [来源](../proposals/tab5-ota-same-version-label.json)。
2. [安装确认 .295](../proposals/tab5-ota-install-confirm.patch) / [来源](../proposals/tab5-ota-install-confirm.json)。
3. [确认弹窗设计 .296](../proposals/tab5-ota-confirm-design.patch) / [来源](../proposals/tab5-ota-confirm-design.json)。
4. [发送恢复 .298](../proposals/tab5-send-recovery.patch) / [来源](../proposals/tab5-send-recovery.json)。
5. [时钟可读性 .299](../proposals/tab5-clock-readability.patch) / [来源](../proposals/tab5-clock-readability.json)，构建及离屏检查通过，真机待验收。

**不要叠加未安装的 .297 rejection-feedback 补丁**：.298 直接基于 .296，已包含并替代其提示修改。若工程已含某一步，先核对哈希，不重复套用。不要直接编辑 279 历史源码快照，保持来源校验可重复。

.298 使用原分区，镜像 7,208,848 字节，余量 112 字节；后续变更必须继续通过实际分区尺寸检查。未删除字库或降低分辨率。本次不提交本机二进制、运行日志、用户截图或配对资料；本地已安装镜像的 SHA-256 为 `6828186749ba70348e97f3661c08500dcdc5bffeb988cf1b26f8245b1d14f007`。跨机器重建须重新生成实际 sidecar，不沿用旧镜像校验和。

本地交付仍用 `scripts/prepare_tab5_firmware.ps1` 生成 `artifacts/firmware/tab5/latest/aibot_tab5.bin`。桥接、ESP8266 与 TAB5 独立编号；本次源码合并不发布标签或 Release。.299 是新增的本地构建候选，镜像 7,208,720 字节、余量 240 字节，尚未安装；不要把 .298 的已验收状态移植给 .299。

## 已验收与未验收

| 项目 | Mac / TAB5 本轮结论 |
| --- | --- |
| 任务页文字引导、排队、会话排序 | 已真机通过；当前偏好恢复引导 |
| 真实电脑附件拦截、两端内容保留 | R42 真机通过 |
| 正常追加、已有电脑草稿的具体拦截提示 | R47 + .298 真机通过；两端内容完整保留 |
| 按钮防连点与完成清理 | 真机通过，一次有效点击；请求级幂等另有合成回归 |
| 高清图库预览、关闭重开、照片草稿保留 | 真机通过 |
| 同版本标签、重装确认、取消和布局 | .294–.296 真机通过 |
| .298 安装 | 精确 ELF、VALID 分区、三个实时界面样本通过，展示设置保留 |
| 故障歌曲复播、切歌、播放／暂停 | 已真机通过；复播走 JPEG 第一轮确认，没有触发备用路径 |
| 真实未知发送后的自动核对／TAB5 人工确认 | 离线回归及实际渲染通过，真实异常场景未验收；旧 Mac 菜单恢复已实测 |
| JPEG 失败后的 RGB565 备用传输 | 离线回归通过，真实触发未验收；历史偶发 JPEG 未确认根因仍未完全确定 |
| DJI 麦克风切换 | 按用户要求暂缓 |

Windows 必须独立验收。连接中断／长期压力仍按用户决定留作日常观察，不新增为已知故障或本次发布门槛。

## Windows 开发验证

```powershell
dotnet build windows-app\AIBotBridge\AIBotBridge.csproj -c Release
dotnet run --no-build -c Release --project windows-app\AIBotBridge\AIBotBridge.csproj -- --status-once
dotnet run --no-build -c Release --project windows-app\AIBotBridge\AIBotBridge.csproj -- --self-test-public
python scripts/check_version.py
python scripts/check_public_content.py
```

随后按本表做真实 Windows + TAB5 验收。Mac 本地 Release、签名、28,814 项隔离检查、追加与音乐采样回归通过；本机 CLT 缺 XCTest，完整测试交由本 PR 的 macOS CI 验证。此前 Windows win-x64 交叉构建通过，不能替代 Windows CI 或实机。CI 结果以当前提交为准。

## English summary

This is the current Windows handoff for Mac R47 / TAB5 .298. Device-bound manual delivery confirmation and journal guards are already ported; Windows native behavior and hardware acceptance remain separate. Priorities are native queue/steer semantics, precise draft/attachment rejection, reliable receipt reconciliation, bounded background diagnostics and cover retries, then same-dimension RGB565 fallback and remaining W29 parity items. Apply shared firmware patches .294 → .295 → .296 → .298 → .299; skip superseded .297. Clock candidate .299 is built and visually checked but not installed; Windows requires no clock-specific bridge change. Never resend unknown outcomes, shrink artwork silently, foreground diagnostic windows or copy Mac-specific selectors into Windows. Two exceptional paths remain physically unverified: device recovery of a real unknown send, and fallback after a real JPEG failure. DJI testing remains deferred. This merge publishes source/documentation, not release artifacts.
