# TAB5 发送反馈与恢复设计（2026-10-31）

场景：用户已点击追加但回执暂未确认，或发送前被电脑草稿／附件保护拒绝。已拒绝必须说“未发送”及具体原因；未知结果不能说失败，也不能自动重发。

设备保持现有 1280×720 横屏任务页、24 点反馈字体、蓝色主操作及既有间距，状态栏两行 64 像素内展示。明确拒绝后保留可编辑草稿。未知结果保留文字与附件、禁止发送重试，先做至多三次纯查询（约 2/4/8 秒间隔），不阻塞界面、不挤占录音/照片/现有网络请求。查询获确认就自动清空该请求草稿并恢复按钮。

仍未知时原发送按钮显示“核对送达”；桥接明确声明该请求可人工确认后，点击打开设备本地确认层。复用固件升级确认风格：黑色 60% 遮罩，居中 700×300 卡片，20 像素内边距；30 点标题“核对这条消息”，24 点说明“请先在电脑会话确认这条消息已出现。确认只清理待确认状态，不会再次发送。”，最多两行原文预览。底部 64 高操作区，从左至右为取消（144）、继续查询（176）、确认已送达（232），间距 16；取消与查询为次操作，确认沿用主色，无自动确认。消息内容、会话、原请求和连接主机必须与打开弹窗时一致才提交；切题／切主机后旧弹窗失效。未知或旧桥接不提供该能力时仍使用纯查询，不发送新操作。

桥接采用原 requestId 的 receipt-confirm 操作，校验设备绑定、权限、原操作与轮次、文字及图片指纹、当前草稿。仅支持当前实现已可安全核验的纯文字追加，成功返回原有 202 幂等回执；不调用桌面发送、不删除其他记录。重复确认只返回相同成功。Mac 内存中短期保留发送前无正文的回执基线，以确认新出现的已接受或排队消息；重启失去基线时保持未知，不猜测。

定位兼容：在用户发起定位/追加时，请求 Electron AXManualAccessibility，随后仍用现有可信页面与精确会话校验；失败保留错误而不假定发送目标。官方依据：https://github.com/electron/electron/blob/main/docs/tutorial/accessibility.md 。此次历史故障是否由辅助功能树未激活导致仍未证实。

实现后须用生产 LVGL 渲染检查空/长文字、明确拒绝、等待查询、恢复弹窗，核对字体、按钮边界、换行和完整状态；再真机验收。Windows 同步新增协议操作的受限实现，平台原生追加能力继续按交接清单独立验证。

English: Explicit rejection preserves editable drafts and identifies the cause. Unknown delivery triggers at most three read-only receipt queries; proven delivery clears only the bound pending draft. Otherwise a device-local, explicitly confirmed recovery dialog replaces the Mac-menu-plus-device-query workflow. Recovery is capability-gated, request/device/content-bound, idempotent and never resends. Mac short-lived in-memory receipt baselines may prove delayed acceptance, never after restart without evidence. Native accessibility initialization preserves exact target checks. Production LVGL rendering and physical acceptance are separate requirements.

## 实现与检查记录

R45/R46 已包含 Mac 回执基线和受限 `receipt-confirm`；Windows 同步记录设备绑定、精确草稿校验及幂等人工确认，跨平台构建通过，Windows 原生运行仍待该端验收。Mac Release、独立追加回归通过；本机 XCTest 缺模块。

.298 使用真实生产 LVGL 检查确认层、具体拒绝提示及实际升级说明；全套界面回归通过。核对卡片实际主按钮宽 232，高 64，取消 144／继续查询 176，间隔 16；说明两行，原文预览截断避免压住按钮。取消、连接主机变化、内容变化、三次纯查询上限、503 不解锁和 202 清理原草稿检查通过。

设备程序使用固定分区，镜像 7,208,848 字节；为容纳新增恢复流程，对低频请求／草稿函数限制内联并采用体积优化，未裁字库或降低分辨率。实际设备确认与 UI 体验另记，不以构建替代。

真机：.298 精确启动、VALID 分区、三次实时界面核验通过，展示设置保留。R47 上正常追加成功；电脑已有草稿时明确拒绝及两端内容保留均由用户确认。设备端人工确认弹窗的真实待确认记录验收仍待发生可核验场景；不得制造重复提交或把离屏渲染记作真机通过。
