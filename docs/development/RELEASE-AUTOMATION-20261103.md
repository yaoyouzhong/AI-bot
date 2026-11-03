# 发布流程精简记录 / Release automation changes

日期：2026-11-03。目标是减少重复构建、重复验收、文件搬运和阶段确认；不修改或重新发布已验收的 0.6.16 / ESP8266 0.5.8 / TAB5 0.2.303-ui 附件。使用入口见 [发布自动化](../RELEASE-AUTOMATION.md)。

## 已处理的问题

| 原因 | 现在的行为 |
| --- | --- |
| 打标签再次调用候选构建，导致之前的包验收不能直接沿用 | 标签只校验元数据；显式晋级冻结原包，禁止调用产品构建 |
| 包身份和验收散落在会话与目录中 | `CANDIDATE.json` 绑定源码、平台、说明与全部文件哈希；验收必须匹配候选 ID |
| 每轮发布机械重做安装、设备与媒体全套检查 | 记录已通过或有基线及理由的复用证据；只补变更路径，未知和失败仍阻止发布 |
| 上传或收尾失败后从头再来 | 核对服务器摘要跳过成功附件，保留分组件进度；已公开附件不覆盖 |
| 三个组件分别更新下载表，容易重复或漏项 | `promote-set` 预检全部候选、依次晋级，最后只做一次下载元数据收尾 |
| 文档推送也编译所有产品，Windows 重复跑公共检查 | CI 按路径选择产品，公共工具检查集中一次；文档/工具变动不编译产品 |
| 阶段变换被误当成授权失效 | 明确发布指令覆盖范围内的必要动作，负责人连续执行；只保留真实物理条件与新增范围的必要询问 |

四项交付使用三条组件版本线。TAB5 保留独立工程、实际镜像和对应源码核验；Windows/Mac 双平台候选继续来自同一源码提交。普通开发 CI 编译与正式交付物构建是不同目的，不将本改动宣称为整个开发过程只编译一次。

## 验证结果

- 发布流水线 25 项、断点上传 3 项、打包边界 16 项、下载索引 15 项、说明提取 4 项、组件版本 6 项、命名 2 项，共 71 项通过。
- 新回归覆盖错来源/错哈希/缺验收/失败验收、明确复用、原始字节与兼容别名、TAB5 对应源码、真实 CLI 预检、幂等重试、跨组件预检与部分失败记录、旧标签构建流程拦截及防止夹带其他本地提交。
- 四个工作流通过 actionlint 1.7.12（关闭非必需的外部 ShellCheck/Pyflakes），两个内嵌 Python 块通过语法解析；实际 GitHub 权限与运行未由此冒充通过。
- 修改路径单独进行公开内容及 diff 检查；原有七个未提交文件按既有清单比对大小与 SHA-256，保持原样，未纳入本次交付。
- 本次没有产品构建、安装、刷机、新建正式标签或公开 Release。测试使用小型候选夹具和模拟 GitHub；actionlint 工具下载后核对官方校验清单，仅放在本次 artifacts 目录，未全局安装。

## 启用与剩余边界

工作流须实际推送后才会在 GitHub 生效。本地与语法回归不能替代首次托管执行；不为试运行而额外发布产品。下次有真实发布时，由唯一负责人使用该入口完成首次托管晋级并记录结果。

标签事件使用被打标签提交中的工作流。新源码必须包含元数据专用标签流程；旧源码仍会触发历史构建时，脚本在创建新标签前停止，不能假定更新 main 就覆盖旧工作流。

公开下载元数据可自动生成；功能说明准确性、真实硬件/账户条件及媒体是否仍准确由负责人依据实际证据集中判断。分支保护或网络中断造成收尾失败时，报告保留“已公开／收尾待完成”的区别，按既有合并方式处理文档，不重新构建或刷机。

## English

Release tooling now freezes hash-bound candidates, records actual or justified reused acceptance, promotes original bytes, resumes verified uploads and closes out selected components once. Tag events no longer build, and scoped CI avoids product compilation for documentation/tooling changes. Four deliverables retain three component version lines and existing platform/source requirements.

All 71 targeted tests and lint for four workflows passed locally. Existing private workspace files remain unchanged. No product build, installation, flash or real release was performed for this tooling change. Hosted execution requires deployment of these workflow changes and will be recorded on the next real authorized release; local tests do not prove GitHub permissions or physical acceptance. Historical source without the metadata-only tag workflow is prevented from silently triggering a rebuild. Editorial accuracy and actual hardware conditions still require evidence-backed review by the release owner.
