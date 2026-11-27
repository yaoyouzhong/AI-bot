# Windows 国产编程套餐交接（2026-11-27）

## 范围和源码身份

- 发起端 Windows；用户要求先完成代码与模拟测试，真实套餐在后续开通后实测。
- 本地分支 `codex/windows-domestic-coding-plans`，起点 `0139da5ad693eebb79e556b3a6670fd399e720e4`。功能提交为 28816e6e35f0ea3841bbfdf937366067233fe4dd。PR 以 013750ac2baf124ac4b3ac3e52db1ceb0c0aa39a 为主干基线，保留该基线的发布日志条目。
- 开发阶段在共享工作区执行了构建、模拟和渲染验证。提交到 PR 的差异只包含套餐相关代码和文档。
- 不改变 VERSION、固件或额度协议字段。不新增火山、腾讯、华为设备显示模式；这三家本轮只在 Windows 模型账号窗口查看与缓存。
- 设计见 [设计记录](WINDOWS-CODING-PLAN-DESIGN-20261127.md)，当前设置与支持级别见 [使用说明](../DOMESTIC_QUOTA_SETUP.md#2026-11-27-windows-开发版编程套餐)。

## 对齐清单

### 1. 套餐／钱包／资源包分开选择和缓存

- 适用范围：两端共同的数据语义；本次实施只授权 Windows。
- Windows 新增“统计内容”。已有智谱余额、百炼 Token Plan、阶跃余额、千帆指定资源包保留旧选择；新账号优先打开订阅。切换后异步旧响应不得覆盖新统计内容，失败保留各自最后成功结果。
- 用户确认显示与下拉选项都只写 Coding Plan / Token Plan，不重复厂商或模型名前缀。厂商卡片副标题、额度徽标及既有缓存的输出一并规范化；厂商标题独立保留。Step Plan / MiniMax M Plan 在界面分别归为 Coding Plan / Token Plan，技术来源记录保留正式产品名。
- 验收：分别造钱包与套餐数据，来回切换；套餐不能带钱包金额，窗口不能串行合并；重启后恢复选择和两份缓存。`CodingPlanSelfTest` 已覆盖本地集成。Mac 需核对原生账号 UI 和持久化，不能直接移植 WinForms/WebView2。
- 发起端：代码与模拟已完成；真实账户待验证。接收端：待核对，未在 Windows 声称 Mac 已支持。

待对齐：macOS｜国产模型统计内容选择与独立缓存

### 2. 智谱个人版 GLM Coding Plan

- Windows 将现有解析器接到账号 Key 配置、刷新健康、后台轮询、缓存与既有状态字段，使用 `AI-bot/ZhipuCodingPlanKey` 的 Windows 凭据目标。保留 raw Authorization 头，不加 Bearer；排除 MCP 的 TIME_LIMIT。
- 验收：模拟成功、401、403、429、重定向、5xx；检查五小时／周窗口、重置时间、错误不泄漏原始响应、切换钱包期间迟到响应被拒绝。团队版登录页结构尚未核实，不能宣称个人 Key 接口支持团队额度。
- 发起端：模拟 HTTP、缓存、状态、Windows 镜像检查已完成；Mac 原生查询及真实账号待验证。

待对齐：macOS｜智谱个人版 Coding Plan 额度查询

### 3. 百炼 Coding Plan 三窗口

- 以当前官方控制台 `codingPlanInstanceInfos` 中唯一 `VALID` 项的 `codingPlanQuotaInfo` 为契约，分别处理 `per5Hour*`、`perWeek*`、`perBillMonth*`，保留零用量。失效、无有效订阅、多有效订阅、无效计数不更新缓存。
- 与 Token Plan 选择及缓存分开；没有订阅起止边界时只保存月重置时刻，不补造 30 天周期。无时区的控制台日期按 UTC+8 解析。
- 验收：三个不同的比例、零用量、28/31 天订阅月、无效数据、缓存重载；Mac 需核对本端百炼采集契约。
- 发起端：基于官方契约构造的响应与缓存测试已完成；实际订阅登录响应未采到。

待对齐：macOS｜百炼 Coding Plan 三窗口与 Token Plan 区分

### 4. MiniMax 与 MiMo 官方入口

- MiniMax 订阅查询改用 `https://www.minimax.cn/v1/token_plan/remains`；保留已保存 Key 和三个兼容环境变量。网页登录按精确主机、HTTPS 默认端口和三条已核对的套餐路径过滤，兼容旧中国站主机。非零 `base_resp.status_code` 不更新。
- MiMo 打开 `https://platform.xiaomimimo.com/#/console/plan-manage`；现有 `/api/v1/tokenPlan/usage` 解析、失败保留与周期规则沿用，不臆造 Key 用量 API。
- 验收：旧凭据读取、域名与路径拒绝、MiniMax 扩容额度、MiMo Credits 和周期；两个真实套餐均待验证。

待对齐：macOS｜MiniMax 当前中国站域名与 MiMo 套餐管理入口

### 5. 其他官方套餐的保守网页进度适配

- 火山 Coding Plan、腾讯 Coding/Token Plan、千帆 Token Plan、Step Plan、华为 MaaS Token Plan 均有产品入口、选项、独立显示缓存及额度摘要。后五家使用本应用自己的归一化 DOM 行契约，不声称掌握其私有接口。
- 精确主机／套餐路由内，仅读取可见进度块中的周期标签和明确已用／剩余百分比。钱包、隐藏／过期内容、含多周期的块和冲突行拒绝。未知页面结构保持旧值并提示未识别。腾讯两种套餐路径与缓存分离。
- 验收：真实 WebView2 加载本地合成 HTML 后执行生产采集脚本，覆盖 0%、剩余转已用、隐藏内容、钱包、过期和冲突进度；各厂商归一化解析及缓存重载也有模拟测试。**这不是厂商登录后页面的端到端验收，实际 markup、团队／席位、多套餐及重置时间仍待实测。**
- 发起端：模拟适配完成，UI 标“待验证”。另一端可选跟进，不能据此自动新增设备显示模式。

待对齐：macOS｜其他国产套餐选项及可见进度采集能力核对

### 6. GLM 设备显示边界

- Windows 镜像修正 GLM 只认余额的分支，复用已有百分比／周期布局。状态结构没有新字段。
- ESP8266 `esp8266/src/main.cpp` 的 GLM 无余额分支仍显示余额占位，未修改；TAB5 当前固件需分别核对套餐百分比、周／短周期、重置及缓存提示。
- 验收：接收端先检查上述分支，再按另行授权修改并编译；用户有真实套餐后进行设备检查。本轮不刷固件、不变更设备展示设置。Windows 镜像不是实机证据。

待对齐：ESP8266｜GLM Coding Plan 百分比显示

待验收：TAB5｜GLM Coding Plan 短周期、周额度和缓存显示

## 自动化与渲染结果

- `dotnet build windows-app/AIBotBridge/AIBotBridge.csproj -c Release`：通过，0 warning / 0 error。
- `--self-test-domestic-quota`：官方接口、额外厂商、MiMo、周期、Coding Plan 模拟测试通过。
- `--self-test-coding-plan-dom`：实际 WebView2 执行离线 HTML 的生产采集脚本，通过；未访问厂商、未使用真实凭据。
- `--self-test-coding-plan-ui artifacts/coding-plans-20261127/ui`：六家账号页的三组相对尺寸（含 900×620 最小窗口）、GLM 镜像离屏预览通过；查看智谱、百炼、小米和 GLM 图片，选择器／摘要／凭据按钮可读，最新智谱截图及徽标均为 Coding Plan。测试窗透明、屏外、不激活、无浏览器导航。不是三档真实系统 DPI 验收。
- `dotnet run --project windows-app/AIBotBridge/AIBotBridge.csproj -c Release --no-build -- --status-once`：成功输出协议 v1 状态。
- `git diff --check`：通过。
- 未升级驻留 App、未安装新包、未做真实账户请求、未做设备验收、未运行 Mac 原生测试。本次没有正式发布候选或包身份。

待验收：Windows｜开通真实套餐后的登录、额度、恢复与账户切换

## 2026-11-27 官方资料与契约核对

只从官方资料独立实现，未迁入第三方项目代码或素材。

- [智谱用量插件说明](https://docs.bigmodel.cn/cn/coding-plan/extension/usage-query-plugin)及其链接的 [官方插件脚本](https://github.com/zai-org/zai-coding-plugins/blob/main/plugins/glm-plan-usage/skills/usage-query-skill/scripts/query-usage.mjs)：个人版、GET quota/limit、raw Authorization。
- [百炼 Coding Plan](https://help.aliyun.com/zh/model-studio/coding-plan)：三窗口和专属套餐；官方控制台发布资源 `bailian-fe/bailian-tokenplan/0.0.145/app-tokenplan/js/subscription-B8kkXAMu.js`、`dist-web-DF9vjJok.js` 确认响应字段及 `queryCodingPlanInstanceInfoV2`。这里只观察取数契约，未复制页面实现。
- [MiniMax 官方套餐页](https://platform.minimax.cn/subscribe/coding-plan?source=link)：当前文案 M Plan，公开的 remains 查询 URL。官方 `platform_web/prod-console-cn-0.1.39` 控制台 2759 chunk 确认另外两条网页套餐路径。
- [MiMo 快速接入](https://mimo.mi.com/docs/zh-CN/tokenplan/Token%20Plan/quick-access)：套餐 Key 与按量 Key 独立，官方链接指向 hash 套餐管理路由。
- [火山 Coding Plan FAQ](https://docs.volcengine.com/docs/ark/coding-plan-personal-faq)、[腾讯 Coding Plan FAQ](https://cloud.tencent.com/document/product/1823/130103)、[百度 Token Plan](https://cloud.baidu.com/product/codingplan.html)、[Step Plan](https://platform.stepfun.com/step-plan)、[华为 MaaS Token Plan](https://support.huaweicloud.com/Token-Plan-maas/tokenplan-maas-0001.html)：确认产品与用量入口，不代表已确认登录后 DOM。
- [DeepSeek 官方定价](https://api-docs.deepseek.com/quick_start/pricing/)：按量从钱包扣款。未发现自营编程套餐的官方依据，保持原钱包入口。

## 发布与收尾

本轮授权范围为推送 Windows 开发分支并创建 PR。真实套餐开通后补测，在本交接中回填结果。macOS 和设备侧按各条待办跟进。
