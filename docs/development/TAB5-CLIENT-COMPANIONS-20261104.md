# TAB5 客户端萌宠调整 / Client companions

本文保留本地开发与验收过程；2026-11-04 素材再分发授权和本轮正式发布范围见 [0.6.17 发布记录](../RELEASE-0.6.17.md)。下文的本地候选限制是对应阶段记录，不替代后续授权。

后续完整源码同步：[0.2.312-ui 工程归档](tab5-312-source/README.md)已按既有规则收齐项目源码、配置、测试、锁文件及必要依赖／SDK 修改；不再依赖下文的历史增量包。This complete project archive supersedes the historical patch-only handoff; it does not publish firmware.

本次基于已经验收的 TAB5 0.2.310-ui，保留其绘图后端精简、图库任务栈修复与总览缓存修复。仅在本地候选中增加客户端萌宠与相关界面调整，不是正式发布。

## 已确认的交互 / Agreed behavior

- 总览只显示一个萌宠：Codex 客户端使用 Codex，Claude Code 使用 Clawd；使用国内模型时仍保持客户端萌宠。未增加其他 Harness、OpenCode 或国产模型萌宠适配。
- 实际活跃客户端优先；空闲时复用已有的三个模型选择作偏好，不另设选择器。两个客户端同时活跃时优先需要操作的一方，其次比较最近活动时间；相同时保留当前显示。
- Codex 左侧保留项目和当前会话，右侧短状态相对于萌宠居中。Claude 当前协议没有项目和会话元数据，因此显示客户端名称，并沿用额度入口，不借用 Codex 会话。
- 屏保名称为“Codex萌宠”“Claude萌宠”。正常画面只显示动画和底部居中小时间，无状态文字和品牌角标；触摸退出恢复原页面。
- 工作中持续循环。总览的完成动画播放后停在结束姿势；屏保的完成动画继续循环。二者共享真实状态和开始时间，切换屏保不重新产生任务完成事件。
- 短状态“状态未知”表示观测不可靠或缺失，不等同于已确认客户端退出。

## 动画来源与对应关系 / Source and mapping

Clawd 使用 Anthropic 官方开发者站点 `https://claude.dev` 实际使用的原始动画帧，来源链为 Claude Code 官方产品页的开发者博客链接。原始资源包 SHA-256 为 `b45ebc4241bf8f98efd6bd3637e71930eb2d4d65ef5a16d68684f4d1e1ff20ba`。仅抽取图像数据；不把上游站点程序复制到项目中。

| 本项目状态 | 原始动画 | 播放规则 |
| --- | --- | --- |
| 工作中 | walk | 循环 |
| 待命 | pool | 循环 |
| 等待操作 | kite | 循环 |
| 已完成 | skate | 总览播完停住，屏保循环 |
| 暂无活动 | kite 的原始站姿帧 | 静止 |
| 状态未知 | astronaut | 循环 |

动画内容来自官方，任务含义和播放策略由 AI-bot 定义，不宣称这是官方任务状态表。拒绝的自制键盘、举手和跳跃动作没有进入本次交付。Codex 保留已确认的官方图集、尺寸与动作。

官方素材属于各自权利人，不纳入项目 MIT。初始验证仅为本地候选；2026-11-04 项目维护者明确确认两套素材均有随固件公开再分发授权，见[授权及来源记录](../../licenses/companion-art/NOTICE.md)。原始来源、摘要、生成帧及声明随发布材料保留。未使用旧 ESP8266 项目中仅获宣传视频使用授权的动画。

实现使用有界解码与共享帧缓冲；Clawd 共 378 帧，压缩图像流 98,371 字节，共享解码缓冲 3,960 字节。仅补透明列以对齐 RGB565 步长；保留原始像素、调色板与顺序，颜色转换为设备 RGB565。官方像素风不经 AI 重画或伪造高清细节。

## 验证记录 / Validation

候选构建、原始帧逐像素比对、实际 LVGL 渲染和设备验收分别记录。构建与模拟预览不能替代真机验收，最终版本与摘要以候选清单及验收记录为准。

本地候选 `0.2.311-ui`：镜像 6,906,896 字节，SHA-256 `475e74dcffbe00a0d29541433884f549b450113a886a41dd1533db22117dd31e`，ELF SHA-256 `285b92e3ca7e42b266c32e203e393901049bb786fc97c2679b84909e0e94e850`。固件构建、图库栈门槛、绘图后端链接检查、378 帧逐像素比对、414 个完整界面场景、45 张健康诊断预览及萌宠专项检查通过。实际更新说明 sidecar 已离屏渲染并检查。

2026-11-04 已通过一次 Wi-Fi OTA 安装到设备 `e8f60ae2ec56`，USB 回报精确 ELF 匹配、`ota_0 state=VALID`，桥接启动核验通过。首次健康观察期曾处于 pending 状态，等待后核验成功，没有重新发送安装。启动后显示错误、欠载和已回传崩溃记录为 0，TF 图库读取回传成功。全局展示策略读回一致；TAB5 本身的屏保偏好兼容性由原 ID 保留、持久化回归和后续用户设备操作分别核对。

两种屏保的物理显示、触摸退出以及真实 Claude Code 活跃时的总览切换单独验收；不把当前 Claude 未运行时的安静站姿或原生测试称为这些场景已经实机通过。任务验收账本位于 `C:/Users/yaououzhong/Work/outputs/AI-bot-codex-companion-20261103/acceptance-20261104/`。

The local candidate extends the accepted 0.2.310-ui baseline while retaining its renderer, gallery stack and overview-cache fixes. Only one mascot is shown, bound to the supported client rather than its model provider. Saver IDs remain compatible: Codex 9 and appended Claude 10. Savers display animation and a small bottom-centered clock. Work loops continuously; completion holds in Overview and loops in the saver without changing task state. Original Anthropic frames are preserved, with project-defined task meanings. Artwork remains outside MIT; the owner confirmed public firmware redistribution rights on 2026-11-04, recorded in the linked notice. Native previews and device acceptance are recorded separately.

## 0.2.312 布局与背景细化 / Framing and background refinement

用户确认 .311 两种屏保的显示与退出正常，随后指出 Clawd 偏下、黑色背景单调，并要求改善时间字体。保留 Codex 的尺寸和位置，Clawd 屏保改为以动画本体的固定视觉中心定位，保持各段官方动作中的跳跃、漂浮。工作造型缩小 25%；同一分钟的原生渲染中，Codex 可见范围约 330×341，Clawd 约 343×262，中心分别约 (632,329.5)、(631.5,327)，含相同的防烧屏微移。不同形象保留原始比例，总览几何不变。

背景采用静态深蓝灰与暖灰棕柔光。直接 RGB565 渐变预览存在色带，因此改用只在选择萌宠变化时生成的共享 640×360 RGB565 背景，稳定空间抖动消除明显色带，PSRAM 占用 460,800 字节。背景固定，萌宠与时间仍按原策略微移，不露黑边；没有动态粒子或新增素材二进制。分配失败时保留纯品牌色背景。时钟使用已内置的 Montserrat 32 数字字体、2 px 字距及固定居中区域，仍只显示 HH:mm。

The refinement keeps Codex unchanged, aligns Clawd by a fixed visual actor anchor while preserving original motion, adds quiet static brand backdrops, and uses the existing Montserrat 32 font for a centered HH:mm clock. One shared dithered RGB565 backdrop avoids dark color bands without adding image assets to flash. Physical acceptance is tracked per exact candidate.

0.2.312-ui 镜像为 6,907,792 字节，SHA-256 `21d78bcce33bdbdb531588b225b59a9902e53ecff620c9b614863f56cdb34d8c`；ELF SHA-256 `aa12dd1673e155c53a15c22a0a9a940443a4da3aa8520b452b59a5080891a39b`。背景与字体完成实际渲染检查后，414 个完整界面场景再次通过，固件构建、图库栈和显示后端检查通过。经一次 OTA 安装，USB 精确启动核验为 `ota_1 state=VALID`。用户在 TAB5 上确认“位置、背景和字体都合适”；此前已确认两种萌宠屏保的显示和触摸退出正常。真实 Claude Code 活跃任务切换及各特殊任务状态仍只通过原生测试，未声称全部实机验收。

## 主干集成 / Main integration

本轮提交 Windows/Mac 状态投影、协议说明及两份独立 TAB5 工程源码增量；不迁移仓库布局，不包含官方素材、生成图集、固件二进制或本地运行资料。两个增量分别对应冻结 0.2.307、0.2.310 基线，后者 SHA-256 为 `82c921c786a40e7aba31be4f4095ff13d96542c3d61868a819baca8b61a8e6e9`，补丁需按各自基线使用；不是在旧补丁后直接叠加即可重建的完整公开发行包。

提交前从仅含本轮暂存改动的源码树构建 Windows Release，0 警告、0 错误；生命周期和 TAB5 任务回归通过。补充检查覆盖已确认的 owner `systemError`：其 `blocked` 状态必须保留为新鲜观测，不能因兼容字段 `state=unknown` 而被误过滤。该桥接修复未替换当前驻留桥接，已验收的 TAB5 0.2.312 镜像字节不变。Mac 构建与测试交由 macOS CI，未将 Windows 检查称为 Mac 验收。

The main integration contains scoped bridge code, protocol documentation and baseline-specific TAB5 source patches. It excludes artwork, firmware binaries and local runtime data, and does not migrate repository layout or constitute a standalone public release package. A clean staged-source Windows build and targeted regressions passed; confirmed blocked observations now survive the live cache. The resident bridge and accepted firmware bytes were not replaced by this commit preparation. macOS build/test evidence remains distinct from Windows validation and hardware acceptance.
