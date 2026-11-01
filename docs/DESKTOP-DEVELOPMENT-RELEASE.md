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
4. 经授权将两端附件集中到同一个草稿 Release。Actions 临时 artifacts 可用于中转，但有保留期限，不能作为长期发布记录的唯一存储。检查本地包与草稿下载包的哈希一致。
5. 下载草稿中的实际安装包，分别验证安装、启动、旧版升级、配对/草稿/设备展示设置保留，以及本次关键变更。源目录运行或候选桥接成功不能代替安装包验证。
6. **验收哪个文件，就发布哪个文件。** 验收后不再次构建并沿用旧结论。源码或包变化时记录新 SHA/哈希，补做受影响验证。纯说明修改不要求重建已验收包，但必须注明其实际源码身份；发布标签应指向被验收源码提交。

## 发布门槛与验收分工

每项交接使用“已完成／已有无需修改／不适用／待验证”，同时记录实现位置和证据。“已完成”必须说明是代码、自测、安装还是具体设备验收，不能混用。

- 自动化先完成：构建、已有回归、普通设置保存/取消与读回、协议状态、版本与包完整性；不把可自动完成的逐按钮检查交给用户。
- 人工只覆盖真实条件：录音与输入法、硬件通道、设备提交、升级体验及必要账户。一次只给一个简单步骤。
- 发送正确性、防重复、草稿/设置保留、安装升级等关键链路未通过时阻止相关平台发布。缺少硬件或账户的非关键项，必须列明影响与替代证据，由用户明确接受剩余限制后才发布；日期目标不豁免门槛。
- 核对平台适用的签名、公证和系统安装提示；如未具备，明确真实状态与安装说明，不声称已签名或已公证。
- 发布前检查授权、候选 SHA、标签、附件哈希、平台范围、许可证、第三方声明、中英文更新说明和未验收项，再由唯一负责人公开发布。

## 维护与回退

发布后执行现有发布收尾清单，核验公开下载、客户端更新检查及各平台实际版本；更新主页、安装说明和相关媒体。保留发布清单、附件摘要与验收记录。

补丁从最新适用主干开发，判断是否影响另一端和旧固件。已发布标签与附件不原地替换；修复使用新版本。保留旧包用于必要回退，但降级前确认配置格式兼容并备份用户数据，不承诺任何旧版都可直接覆盖安装。

## 自动化现状与后续工作

当前 `.github/workflows/candidate.yml` 已支持 Windows/macOS 分平台构建；`release.yml` 支持标签触发构建并汇总草稿。**当前流程可能重新构建，不等于已经实现“晋级原有已验收候选”。** 使用现有流程时，应在其最后一次构建并汇总草稿后验收实际附件，再公开该草稿。

后续单独授权、实现及验证的 CI/CD 改进：

- 生成并校验双平台构建物清单，关联源码 SHA、架构、哈希与验证记录。
- 增加候选晋级路径：按明确的构建批次取原包，验证完整性，禁止静默覆盖或重建。
- 检查发布平台附件齐全、版本一致、标签源码正确，缺项时停止。
- 保持正式发布人工批准；推送 main 只检查，不自动公开新版本。

这些改进尚未因本文落库而实施；本文不修改工作流权限、触发条件或发布授权。

## English summary

Use one repository and main branch, with short-lived platform branches and evidence-backed handoffs. Share behavior and protocol contracts while implementing native platform integrations. Keep the existing bridge version line and publish only changed platforms. One release owner coordinates each release from either OS.

Build both platform packages from the same selected source commit on their respective operating systems. Collect authorized uploads in one draft Release, verify hashes, then test the actual downloadable installers. Publish exactly the accepted bytes. Existing tag workflows may rebuild: acceptance must follow the final build, not precede it. Candidate promotion without rebuilding is a proposed automation improvement, not an implemented guarantee.

Record source identity, package hashes, platform tests and remaining limitations. Critical delivery, data-preservation and installation failures block the affected release. Publishing, tags and uploads require their applicable authorization. Follow the existing version specification and release closeout checklist; never replace published assets or tags in place.
