# TAB5 页面一致性与通用总览 / UI consistency and adaptable overview

本地候选 `0.2.158-ui`。源码实现在相邻独立 TAB5 工程；采用现有 `quotaDetails` 与 `systemMetrics`，兼容追加供应商描述，桥接协议版本保持不变。构建、原生 LVGL 演示截图与模拟回归不代表设备已安装或实机验收。

## 页面审视

| 页面组 | 结论与处理 |
| --- | --- |
| 总览、模型额度、Codex 任务、应用 | 保留共用顶栏、底栏、卡片与按钮；集中维护背景、文字、状态色、圆角与内边距。总览模型卡片统一字号、数值与进度布局，以供应商 Logo 与独立标题色区分品牌。 |
| 天气实况、24 小时、未来 7 天 | 保留现有页签、同一位置/来源行、温度与预报层级；列表密度与主卡片不同，但沿用同一字体、表面与颜色含义。 |
| 行情、电脑状态、桌宠 | 修正主要列间距到 24 px；电脑状态、桌宠内容底边对齐 512 px 主卡片高度。行情各列卡片宽度一致。 |
| 音乐、五种时钟 | 保留大封面、计时/进度与专用表盘布局，沿用公共导航、正文和状态色。内容形态不同无需加同样的卡片框。 |
| 设置五个分类 | 保留 30 px 四字分类和标题、24 px 正文、固定外框与内层滚动；新增模型选择沿用现有复选框与自动保存反馈。 |
| 会话回复、语音、照片/摄像头、升级与诊断 | 保留现有公共控件和操作状态；回复阅读、录音、照片确认、升级进度保留各自信息层级。 |
| 屏保与艺术横竖屏 | 保留年度主题、月历书法、艺术作品的专用配色与构图。主工作台的样式统一不覆盖作品。 |

## 总览行为

- 新用户默认自动选择最多三个已有账户额度/余额数据的供应商。仅有本机 token 记录不算账户额度；零余额和缓存成功数据仍可显示。
- `设置 → 内容轮播 → 总览显示的模型` 可固定最多三个：Codex、Claude、百炼、Kimi、MiniMax、DeepSeek、智谱、阶跃、百度、小米、火山方舟。顺序固定，避免心跳导致卡片不断换位；一/二/三张卡片等宽铺满左下区域。
- 十一个供应商均显示 Logo，并各有独立区分色。标识来自 2026-10-10 核对的官方页面当前图案：阿里采用百炼当前标识，智谱采用当前 Z.ai 标识。保留原图比例和颜色，统一生成 36×36 像素、ARGB8888、144 字节行跨度的设备资源；官方原图分辨率不同，不把归一化资源误称为统一高分辨率原图。原始文件、网址及 SHA-256 记录在独立 TAB5 工程 `firmware/assets/brands/providers.json` 与同目录 README。
- 没有额度时显示一张授权提示卡。无 Codex 会话时，左上显示 CPU、内存及「查看电脑状态」；有会话时保留真实任务、未读完成内容及继续入口。其他模型的额度支持不表示已实现其会话控制。火山方舟已有电脑端控制台入口，本次加入 TAB5 选择、Logo 与兼容消息项；额度采集仍待接入，手动选择明确显示该状态。
- 原 `home_models` 的七种有效选择映射到新的十一供应商选择，保持原用户组合。新选择独立保存为 `home_providers`；写入或提交失败保留原选择并恢复复选状态。轮播、顺序、间隔、亮度、配对与草稿不因该迁移改变。

## 验证与边界

Windows Release 默认输出被正在运行的进程占用；改用独立输出目录构建通过，不中断现有桥接。Windows TAB5 模拟协议测试通过。Codex 进程内的 `--status-once` 被现有数据目录保护拦截；通过 Windows 桌面启动同一候选 DLL 后，读取状态通过，证据为 `desktop-status-check.json`，未绕过保护或替换常驻桥接。

原生预览覆盖主页面、天气页签、五类设置、五种表盘、月历/屏保、回复、语音、照片、升级与空数据。新增回归核验十一供应商目标路由（包括阿里与天气的索引区分）、最多三模型、失败回滚、自动选择、零/缓存余额及无会话降级；持久化回归核验七种旧选择、默认自动、重载及 open/set/commit 失败。重复心跳不使总览缓存失效，原滑动/缓存/完整与分块绘制断言继续执行。

证据目录：独立 TAB5 工程 `artifacts/ui-audit-20261010/`；可审阅预览复制到 AI-bot `artifacts/development/tab5-ui-audit-20261010/`。艺术图片需要独立图库素材才能显示；普通预览里的彩色块是图片加载测试素材，不是拟交付视觉资源。

## English

Local candidate `0.2.158-ui` centralizes existing workbench colors, type roles, radii and padding, aligns the primary market/system/pet gutters, and retains purpose-specific music, clock, forecast and art compositions. The TAB5 implementation uses the existing bridge payload with a backward-compatible appended provider entry.

Fresh devices automatically show up to three providers with account quota/balance data. Settings can pin any three of the eleven providers. Equal-width cards fill the available space. Each provider has an official logo checked against its current official page on 2026-10-10 and a distinct readable accent. Device resources share a 36×36 ARGB8888 canvas and retain the original proportions/colors; official source resolutions differ. Sources and hashes are recorded in the TAB5 brand asset directory. Local token counts never become account quota. Zero balances and successful cached data remain meaningful. An empty model list shows an authorization hint; without a Codex session the task area shows CPU/memory and a system shortcut. Quota support does not imply task-control support for other providers. Ark is selectable with its official logo, but collection remains pending; all unavailable quota fields remain null.

All seven valid legacy model masks are mapped without overwriting the old key. The new selection persists independently, with rollback after failed writes/commits. Native UI and persistence regressions cover routing, one/two/three cards, empty/stale/zero data, save failures, migration and unchanged-heartbeat cache reuse. Firmware builds and synthetic native previews do not establish device installation or hardware acceptance. Existing gallery/transport and other uncommitted changes remain part of the current checkout and are not re-certified by this UI audit.

The running bridge locks the default Release executable, so the candidate was built in a separate output directory. Synthetic TAB5 protocol tests passed. The normal profile guard blocked status capture under Codex; the same candidate DLL passed a read-only desktop-launched status check without bypassing the guard or replacing the resident bridge.
