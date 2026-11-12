# Windows / Mac 开发、维护与发布流程

本文件是两端协作流程的统一入口。版本编号以 [COMPONENT-VERSIONS.md](COMPONENT-VERSIONS.md) 为准，正式发布收尾以 [RELEASE-CHECKLIST.md](RELEASE-CHECKLIST.md) 为准，不在交接文档复制另一套规则。

## 开发与交接

1. 开始时读取 AGENTS.md，检查当前分支、工作区和主干变化，保留已有工作。两端从已同步的 main 建立任务分支，建议使用 `codex/windows-*`、`codex/mac-*`；不维护长期分叉的操作系统主干。
2. 修改前定义共同用户行为和协议契约；输入法、音频、窗口及 UI 按平台实现。已有能力先核对，不机械移植另一端代码。
3. 涉及另一平台或固件时，使用[交接模板](development/CROSS-PLATFORM-HANDOFF-TEMPLATE.md)，记录实现位置、协议兼容性、测试证据及剩余事项；交接与代码一起提交。
4. 在对应系统完成必要构建、自测及受影响路径验证后再合并。Windows 验证不能替代 Mac 验证，构建通过不能替代安装或真机通过。未知项写明，不隐去失败。
5. 本地提交、合并、远端推送、创建标签、上传草稿及正式发布分别按已有授权执行。只有推送完成，另一台电脑才能从远端拉取；不得把本地合并描述为已经交付到 GitHub。

## 版本与发布负责人

- 桥接保持现有共用版本线；每次指定一个发布负责人，统一维护 VERSION、平台元数据、release-manifest.json 和中英文说明。发布负责人可在 Windows 或 Mac 上操作。
- 双平台发布使用同一桥接版本；单平台更新只交付该平台，另一平台保留实际已发布版本和下载入口，不重新打包凑号。
- `components.bridge.desktopPlatform` 必须明确为 `windows`、`macos` 或 `all`。TAB5、ESP8266 独立编号，按协议和最低版本判断兼容性，不要求版本号一致。
- 同一次发布只有一个执行方管理标签、草稿和公开发布，另一端提供代码、构建物与验证记录，避免覆盖同名附件或重复创建 Release。

## 候选与构建物

1. 两端改动合并后，选定一个完整源码提交 SHA 作为候选。双平台包从同一 SHA 构建；打包工作区必须干净，不能混入未提交代码、个人设置或凭据。
2. Windows 包在 Windows 构建，Mac 包在 macOS 构建。可使用现有 GitHub Actions 候选流程，也可使用仓库本地打包脚本。CI 构建不代替平台安装和硬件验收。
3. 每个包登记平台/架构、版本、源码 SHA、文件名、大小、SHA-256、构建方式、测试结果及验收人。安装包不进入 Git 源码历史；验收材料不得包含个人草稿、Cookie 或授权令牌。
4. 用 `release_pipeline.py assemble` 汇集并冻结原生包；Actions 候选流程会自动完成。`CANDIDATE.json` 固定文件清单和哈希，`record` 将实测或复用证据绑定到此候选。Actions artifacts 有保留期限，应连同验收记录下载归档，不将其作为长期记录的唯一存储。
5. 在对应系统使用这一份候选的实际安装包验证受影响的安装、启动、资料保留和关键变更。符合下文复用条件时直接登记已有证据，不要求每次重新从旧公开版升级、配对或刷机。上传后核对服务器摘要，不再下载同一个包重复验收。源目录运行不能替代实际安装包验证。
6. **验收哪个文件，就发布哪个文件。** 验收后不再次构建并沿用旧结论。源码或包变化时记录新 SHA/哈希，补做受影响验证。纯说明修改不要求重建已验收包，但必须注明其实际源码身份；发布标签应指向被验收源码提交。

## 发布门槛与验收分工

每项交接使用“已完成／已有无需修改／不适用／待验证”，同时记录实现位置和证据。“已完成”必须说明是代码、自测、安装还是具体设备验收，不能混用。

### 按变更复用证据

- 一个候选以源码 SHA 和包 SHA-256 固定身份。同一份已验收包直接晋级；不因换会话、整理文档、准备标签或上传附件而重做构建和功能验收。
- 只改变版本声明时，沿用未变更运行代码的功能实测，补实际发布包的身份、安装／启动检查；说明修改不触发重建。
- 小幅变化且原截图、宣传视频仍准确时，标记“已有无需修改”并复用原素材及适用的播放证据；只有旧素材会误导用户时才更新受影响镜头或截图。更新说明、版本和下载入口仍按实际发布同步。
- 对照最近已发布版本列出实际变化。安装器、运行时依赖、数据格式、加密和迁移代码未变时，可结合既有对应链路实测、本次实际包安装与资料保留结果复用证据；新增安装或数据迁移路径仍须定向验证。保留未覆盖的具体条件，不将其扩成每次全量重测。
- 安装升级检查允许使用本机临时目录、静默安装和只读启动，保存并恢复原安装登记，保护用户资料；没有新证据时不反复尝试同一沙盒路径。
- 唯一发布负责人汇集各端一次性验收结果与剩余限制后，按原包手动晋级或自动晋级。不通过的关键变更仍阻止发布；证据复用不等于把失败或未验证的新增路径标为成功。

Evidence follows the actual change: identify candidates by source and archive hashes, promote accepted bytes, reuse unchanged functional and storage-path evidence, and target installation or migration checks to changed paths. Documentation and version-only updates do not trigger a complete acceptance cycle. Minor changes may retain accurate screenshots, video and applicable playback evidence; update only misleading material while synchronizing release details and downloads. Preserve explicit limitations and continue to block failed critical changes.

- 自动化先完成：构建、已有回归、普通设置保存/取消与读回、协议状态、版本与包完整性；不把可自动完成的逐按钮检查交给用户。
- 人工只覆盖真实条件：录音与输入法、硬件通道、设备提交、升级体验及必要账户。一次只给一个简单步骤。
- 发送正确性、防重复、草稿/设置保留、安装升级等关键链路未通过时阻止相关平台发布。缺少硬件或账户的非关键项，必须列明影响与替代证据，由用户明确接受剩余限制后才发布；日期目标不豁免门槛。
- 核对平台适用的签名、公证和系统安装提示；如未具备，明确真实状态与安装说明，不声称已签名或已公证。
- 影响 Mac 授权流程、签名或 entitlement 时，用同一候选包分别验证独立 macOS 用户首次授权、重启复用，以及从公开旧包覆盖安装后的实际授权。以运行进程读到的权限与功能实测为准；系统设置里的开关状态不代替验证。
- 发布前检查授权、候选 SHA、标签、附件哈希、平台范围、许可证、第三方声明、中英文更新说明和未验收项，再由唯一负责人公开发布。

## 维护与回退

发布后执行现有发布收尾清单，核验公开下载、客户端更新检查及各平台实际版本；更新主页、安装说明和相关媒体。保留发布清单、附件摘要与验收记录。

补丁从最新适用主干开发，判断是否影响另一端和旧固件。已发布标签与附件不原地替换；修复使用新版本。保留旧包用于必要回退，但降级前确认配置格式兼容并备份用户数据，不承诺任何旧版都可直接覆盖安装。

## 发布只走四步

1. **定范围**：确定实际变化的组件、平台、版本和源码 SHA，整理最终中英文更新说明；唯一负责人使用当前任务中的发布授权，不为构建、标签、上传和收尾分别索要确认。
2. **构建候选一次**：从指定 SHA 在对应系统构建所需包；已有同 SHA、同哈希且构建验证完整的候选直接使用。Actions `candidate.yml` 自动登记原生来源并冻结候选。TAB5 用已核验的独立工程交付物封存，不为发布重新刷机。
3. **记录验收一次**：Agent 完成可自动检查，只将真实设备或账户条件留给用户。用 `record` 记录本次结果或有基线与变更分析的复用结果；失败和缺项继续阻止晋级。候选文件变化时旧记录失效，纯文档修订不重建原包。
4. **原包晋级与收尾**：`promote` 校验同一清单，创建正确标签和草稿，断点上传、核对服务端摘要；已有授权时一次选择公开发布，并自动刷新下载索引、中英文下载块和 Release 的统一下载链接。负责人一次复核受影响说明与媒体复用结论，保留记录。

具体命令及异常恢复见 [发布自动化](RELEASE-AUTOMATION.md)。`release.yml` 标签事件只检查声明，不再调用构建；`promote.yml` 显式取回成功候选批次。推送 main 不会公开版本。常规代码 CI 仍验证受影响产品；候选打包是唯一正式交付物构建，发布晋级不再编译。文档及发布工具改动只跑工具回归。

## English summary

Use one repository and main branch, with short-lived platform branches and evidence-backed handoffs. Share behavior and protocol contracts while implementing native platform integrations. Keep the existing bridge version line and publish only changed platforms. One release owner coordinates each release from either OS.

Select scope and source once, build the required native candidate packages once, record acceptance once, then promote exactly those bytes. Existing validated candidates and justified unchanged-path evidence are reusable. Acceptance is bound to the candidate identity and file hashes. Tag events only validate metadata; explicit promotion retrieves a successful candidate run without rebuilding, resumes verified uploads, and synchronizes download metadata. Ordinary code CI still checks changed products; documentation/tooling changes do not rebuild apps. See RELEASE-AUTOMATION.md for commands and recovery.

Record source identity, package hashes, platform tests and remaining limitations. Critical delivery, data-preservation and installation failures block the affected release. Publishing, tags and uploads require their applicable authorization. Follow the existing version specification and release closeout checklist; never replace published assets or tags in place.
