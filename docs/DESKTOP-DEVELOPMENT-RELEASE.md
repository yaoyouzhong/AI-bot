# Windows / Mac 开发、维护与发布流程

本文件是两端协作流程的统一入口。版本编号以 [COMPONENT-VERSIONS.md](COMPONENT-VERSIONS.md) 为准，正式发布收尾以 [RELEASE-CHECKLIST.md](RELEASE-CHECKLIST.md) 为准，不在交接文档复制另一套规则。

## 开发与交接

1. 开始时读取 [AGENTS.md](../AGENTS.md)，检查当前分支、工作区和主干变化，保留已有工作。两端共用 `main`，同一交付范围复用开发分支。需独立交付或隔离并行工作时，从已同步的 `main` 建立 `codex/` 分支。分支和 `.worktrees/` 按项目规则收尾。
2. 修改前定义共同用户行为和协议契约；输入法、音频、窗口及 UI 按平台实现。已有能力先核对，不机械移植另一端代码。
3. Windows 或 Mac 任一端有功能改动时，必须在同一个 PR 中使用[交接模板](development/CROSS-PLATFORM-HANDOFF-TEMPLATE.md)，新增或更新 `docs/development/` 下的 `*-HANDOFF-*.md`。涉及固件的改动沿用同一模板。发起端在提交时明确是否需要交接、接收端是谁、场景属于两端共同要求还是平台差异，并给出对齐初判和依据。逐项写明触发条件、两端预期、验收步骤和通过标准，区分发起端已完成的检查与各端待验项。交接与改动一起合入 `main`。
4. 发起端完成适用的本机测试、PR 的 CI 检查和审核后，当前 PR 可以合入，即使另一端仍待对齐。把接收端的下一步记入交接，主干合入后再跟进。分别记录代码检查、安装与真机验证的结果和待验项。
5. 合入后，由接收端核对本端实现和已有验收证据。已有等价行为就记录依据，只有验收缺口就安排复验，确认缺少共同要求对应的实现时，由 AutoLoop 通过 QQ 发开发审批，说明接收端、待补行为和验收要点。用户选择「对齐开发」后按审批范围实施，选择「暂不处理」则本次保留现状。范围确认只用于确有未决的共同要求或实施范围。
6. 对齐端得出可核对的结论后，在原交接文档中逐项更新明确交给本端的待对齐、待验收事项，写明状态和实际验证依据。已有对齐 PR 时随该 PR 更新。结论为已有实现或无需修改时，也回填原事项的核对依据。有待验项就继续标为待验证，完成实际验收再更新。
7. 本地提交、合并、远端推送、创建标签、上传草稿及正式发布分别按已有授权执行。只有推送完成，另一台电脑才能从远端拉取；不得把本地合并描述为已经交付到 GitHub。

## 版本与发布负责人

- 产品总发布号与桥接组件号独立；桥接保持现有 Windows／Mac 共用组件版本线。每次指定一个发布负责人，统一维护 VERSION、平台元数据、release-manifest.json 和中英文说明。发布负责人可在 Windows 或 Mac 上操作。
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

## Mac 首次授权验收

- 首次打开包时检查引导是否依次请求定位、语音识别、麦克风、通知及适用的自动化权限，已允许的项目不重复弹出；通知首次没有返回授权决定时会再请求一次，仍未决定可在同一引导重试。辅助功能在最后直接打开系统设置，引导仍可操作并刷新应用读到的授权；若系统开关已开而权限未生效，核对当前 App 后按提示处理。使用「稍后完成」后不再继续发起系统请求。
- 首次授权时，可在后台启动已安装的 Terminal、音乐和 Spotify，逐个确认自动化；未安装的应用不请求。未配对 TAB5 时也应请求 Terminal 自动化，实际会话操作仍须通过配对与功能检查。核对蓝牙及 macOS 15 以上可用网络中的本地网络提示。允许权限后验证音乐读取、播放／暂停及语音功能；拒绝某项仍能继续完成其他授权。
- 覆盖升级后，新版本首次打开时重新检查启用功能的权限；只列出系统可确认的缺失项，已授权项不再请求。启用电脑控制时，在后台启动系统事件以预检自动化权限。辅助功能待授权时，由当前进程向 macOS 请求，再打开系统设置，便于给本次运行的 App 授权。无法无提示预检的本地网络和未运行的其他自动化目标仍可在完整清单中处理。同版本重新启动不重复打开引导。
- 关闭引导后，从「设备中心 → 桥接设置 → 系统授权」补办；保留手动关闭的功能。正式发布验收使用独立账户。也可在当前账户用全新应用标识、独立配置和钥匙串记录的完整包复测流程；从一开始固定安装路径，勿清除日常账户权限。

### 固定签名候选 / Fixed-signing candidates

准备未公证的固定自签名候选时，在 Mac 构建机的钥匙串使用**同一张**代码签名证书和私钥。用下列命令对新目录打包；证书指纹可用于识别证书，私钥、密码和钥匙串不得进入仓库或安装包。`AIBOT_MAC_SIGN_KEYCHAIN` 可指向已经解锁的独立构建钥匙串。`fixed` 要求明确的证书指纹，失败即停止，不自动生成/更换证书，也不退回临时签名。正式长期使用前需确认发布人、私钥备份和证书有效期；不要复用一次性测试证书。这不改变默认 CI 的签名方式或发布授权。

```bash
AIBOT_MAC_SIGN_MODE=fixed AIBOT_MAC_SIGN_IDENTITY='<40 位证书指纹>' \
  bash scripts/package_macos.sh artifacts/<新候选目录>
```

- 迁移：使用已公开的临时签名旧包，保持原安装位置，通过内置更新装入固定签名候选。旧版本可能没有迁移说明，应在对应发布说明同时解释：重启后仅补办确实失效的系统权限，钥匙串确认单独核对，不承诺全部无提示。
- 连续升级：使用同一张证书和 Bundle ID 构建一个不同版本，通过内置更新复验自动重启和当前进程的辅助功能状态。不以系统开关外观代替实测；下载包的 Gatekeeper 放行与权限继承单列。固定签名版拒绝通过内置更新退回临时签名或换证书；确需轮换需单独设计迁移与验收，不把手动回退当作保留权限。
- Use the same code-signing certificate and bundle identifier across builds. `fixed` requires the certificate fingerprint and never falls back to ad-hoc. First adoption can require renewed system permissions; subsequent in-app updates reject signer changes or ad-hoc packages. This package is not notarized. Verify Gatekeeper, Accessibility and Keychain separately, and do not publish with the isolated test certificate.

## 维护与回退

发布后执行现有发布收尾清单，核验公开下载、客户端更新检查及各平台实际版本；更新主页、安装说明和相关媒体。保留发布清单、附件摘要与验收记录。

补丁从最新适用主干开发，判断是否影响另一端和旧固件。已发布标签与附件不原地替换；修复使用新版本。需要回退时优先核对对应公开原包，需离线回退时按 [本地工作区保留与清理](WORKSPACE-RETENTION.md) 指定本地保留包。降级前确认配置格式兼容并备份用户数据，不承诺任何旧版都可直接覆盖安装。

## 发布只走四步

1. **定范围**：确定实际变化的组件、平台、版本和源码 SHA，整理最终中英文更新说明；唯一负责人使用当前任务中的发布授权，不为构建、标签、上传和收尾分别索要确认。
2. **构建候选一次**：从指定 SHA 在对应系统构建所需包；已有同 SHA、同哈希且构建验证完整的候选直接使用。Actions `candidate.yml` 自动登记原生来源并冻结候选。TAB5 用已核验的独立工程交付物封存，不为发布重新刷机。
3. **记录验收一次**：Agent 完成可自动检查，只将真实设备或账户条件留给用户。用 `record` 记录本次结果或有基线与变更分析的复用结果；失败和缺项继续阻止晋级。候选文件变化时旧记录失效，纯文档修订不重建原包。
4. **原包晋级与收尾**：`promote` 校验同一清单，创建正确标签和草稿，断点上传、核对服务端摘要；已有授权时一次选择公开发布，并自动刷新下载索引、中英文下载块和 Release 的统一下载链接。负责人一次复核受影响说明与媒体复用结论，保留记录。

具体命令及异常恢复见 [发布自动化](RELEASE-AUTOMATION.md)。`release.yml` 标签事件只检查声明，不再调用构建；`promote.yml` 显式取回成功候选批次。推送 main 不会公开版本。常规代码 CI 仍验证受影响产品；候选打包是唯一正式交付物构建，发布晋级不再编译。文档及发布工具改动只跑工具回归。

## English summary

Use one repository and one main branch. Related changes for the same delivery share a development branch. Create a separate `codex/` branch from current `main` for independent deliveries or isolated parallel work. Follow [AGENTS.md](../AGENTS.md) for branch and worktree cleanup. Share behavior and protocol contracts while implementing native platform integrations. Keep the existing bridge version line and publish only changed platforms. One release owner coordinates each release from either OS.

Select scope and source once, build the required native candidate packages once, record acceptance once, then promote exactly those bytes. Existing validated candidates and justified unchanged-path evidence are reusable. Acceptance is bound to the candidate identity and file hashes. Tag events only validate metadata; explicit promotion retrieves a successful candidate run without rebuilding, resumes verified uploads, and synchronizes download metadata. Ordinary code CI still checks changed products; documentation/tooling changes do not rebuild apps. See RELEASE-AUTOMATION.md for commands and recovery.

Record source identity, package hashes, platform tests and remaining limitations. Critical delivery, data-preservation and installation failures block the affected release. Publishing, tags and uploads require their applicable authorization. Follow the existing version specification and release closeout checklist; never replace published assets or tags in place.

## 审查要点

- 核对改动的调用方和原有行为。设备连接、配对、授权、串口与网络回退不能因平台对齐退化。
- 本机只把实际执行的检查记为通过。分开核对合入、安装、真机验收和发布，不能用模拟、设备在线或另一个提交的记录代替。
- 桌面功能改动在同一 PR 写交接，明确说明另一端需要做什么、哪些实机事项尚待验证。
- 审查 GitHub 工作流时，检查触发范围、权限、提交身份和必需检查如何处理失败、取消及跳过。合入主干不应绕过发布或设备验收授权。
- 自动对齐只处理已通过本机验收的产品改动和配套交接，不自动合入改动交付策略、主干工作流或审查规则的 PR。GitHub 对主干强制要求来自 GitHub Actions 的 `alignment-check`，并要求 PR 分支跟上主干。
