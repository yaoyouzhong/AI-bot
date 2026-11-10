# Windows 主导发布：双平台与 TAB5 交接（2026-11-10）

> 更新：Windows 最新开发基线为 `a13769074383aabb15c84597ee6de3b54c465072`，设备候选已推进到 R12；后续 Mac 本轮对齐结果和最新发布事项见 [R12 对齐记录](MAC-TAB5-R12-ALIGNMENT-20261110.md)。下文保留此前 R6 和 Mac 候选的历史身份，不能将其作为最终 R12 或最终 Mac 包。

Windows 是后续发布的唯一负责人。本记录准备源码、说明及验收材料，不是正式发布指令或已冻结的发布候选。用户本次授权 Mac 提交并推送交接；不在本步骤创建标签、上传附件或公开发布。统一步骤见[开发发布流程](../DESKTOP-DEVELOPMENT-RELEASE.md)与[发布自动化](../RELEASE-AUTOMATION.md)。

## 来源、范围与版本

- Mac 交接分支为 `codex/mac-native-claude-alignment-20261107`，已包含 Windows `020d248978d011a3a9169a46dca09d11fe47e168`，以及 Mac 未读同步源码 `2365e94d84a4496abd95f1e8d32abda8b411c86b`；后续提交为交接资料。Windows 先获取该分支，检查实际远端状态并整合，不能用旧分支覆盖任一端的工作。最终候选需在日志、版本和范围确定后重新指定完整 SHA。
- 2026-11-10 只读核对：公开桌面版为 `v0.6.17`，TAB5 为 `tab5-v0.2.313-ui`；远端未找到 `v0.6.18` 或 `tab5-v0.2.318-ui` 标签。本仓库目标登记为桥接 `VERSION=0.6.18`、`desktopPlatform=all`、TAB5 `versions/TAB5=0.2.318-ui`。发布时再确认远端；ESP8266 和图库没有本轮重编、重编号或发布计划。
- 双平台发布必须从同一个最终候选源码 SHA 各自在原生系统构建并验收。Windows 接收后先确认与最终说明有关的代码是否再变；仅说明/交接修改不使未变原包的具体功能证据失效，但旧包的源码身份不能改写。完整候选、安装包、固件及私有验收截图不随 Git 传输。
- 待审校的[双语用户说明草稿](WINDOWS-RELEASE-DRAFT-20261110.md)同时覆盖现有目标版本条目和 `Unreleased / 未发布`。发布负责人确定范围与实际日期后，把相关内容合入四份中英文组件 CHANGELOG 的目标版本段，并清空对应未发布段，再运行 `extract_release_notes.py ... --require-final`。不要把 2026-11-05 的旧日期当成实际发布日期。最终说明、sidecar 和镜像必须匹配。

## 已有证据与未验收项

| 对象 | 已有证据 | 不能替代的工作 |
| --- | --- | --- |
| Windows 桥接 | Windows 来源 `c9e817c55c1b16d6984b5af93e1e88d8f2e991e0` 的本地安装 DLL SHA-256 `ead4787f2194ada2ebcbfc18da6bae3a7df3c0025829541e7c3c299842f5fd07`；Release、`--status-once`、语音准备和 Claude 回归、安装文件和资料保留检查通过；首次语音允许后继续本次录音由用户确认。详见 [Windows 交接](MAC-HANDOFF-FROM-WINDOWS-20261110.md)。Windows 已将同项目上限提高到 64、总目录上限 80，旧固件继续分页，不再是每项目只发 8 条。 | 该 DLL 不是本次共同最终源码构建的完整发布候选。最终 Windows 原包的安装与启动、受影响功能和单次授权升级 B、常驻桥接 OTA、Wi-Fi 自动回连及实际已读耗时按受影响范围补验。电脑自动发起升级此前未进入传输，不能记为通过。 |
| Mac 桥接 | macOS 27.3 arm64 的 `2365e94d84a4496abd95f1e8d32abda8b411c86b` 本地开发候选：主程序 SHA-256 `3c3ad6b952ccb1bb81e406bd87de77eb54abcfa5509f3cd599a91a51c313d561`；ZIP 19,638,533 字节，SHA-256 `d18992eaba58ada2fc3fe959ad25e54968381e507124f76ab2c65cc835b53075`。512 项测试中 4 跳过、其余通过；额外隔离校验通过；开发签名、安装和后台 USB 检查通过。详见 [Mac 结果](MAC-WINDOWS-R6-ALIGNMENT-20261110.md)。 | ZIP 是本机开发签名、未公证的本地候选，不随 Git 推送且不自动构成可公开晋级包。真实终端窗口投递及已有草稿保护、独立身份首次语音授权与识别、设备接收隔离新回复及已读消退耗时未完成。无窗口环回测试、历史包或截图不能冒充这些验收。 |
| TAB5 R6 | `0.2.318-ui · R6`：BIN 7,027,136 字节，SHA-256 `de7cfc8515fec52e8990af04c86ca8d767dbe9208dc5e5cdb08606bb28549303`，ELF SHA-256 `40cc39cbe8ba92e2dd763d2fa58cc1409a238fdc28bf320556ef13d70c2af153`。Windows 用户确认未读优先显示；原包经设备发起 Wi-Fi 升级，核验分区 VALID、ELF、Wi-Fi 和 USB 恢复。Mac 连同一 R6 时确认 USB 工作，R6 UI 离屏回归覆盖 64 条、手势与未读排序。详见 [R6 记录](TAB5-318-R6-UNREAD-PRIORITY.md)。 | Mac 下新回复置顶实测未完成；实际 sidecar、完整源码、首刷包和升级包须与所选应用字节一一绑定。R6 使用 Huihui Claude 提示音，当前**没有确认该 PCM 的公开再分发许可**；R6 本地实测不等于可公开发布，须先查明许可或更换具备授权的素材并对新镜像重新记录受影响验收。 |

R4、ICON2 和 LIST1 的验收分别保留原身份；不可将其哈希、版本或验收直接套到 R6 或重建包。R6 源码重放顺序为 [.318/R4 增量](tab5-318-r4-source/README.md) → [LIST1](TAB5-PROJECT-SCROLL-LIST1-20261110.patch) → [R5](TAB5-318-R5-RELEASE-NOTICE.patch) → [R6](TAB5-318-R6-UNREAD-PRIORITY.patch)，本地授权素材边界见 [R5 记录](TAB5-318-R5-RELEASE-NOTICE.md)。旧 `tab5-318-refresh-source` ZIP 已被 R4 增量取代；不要将历史文档里该旧文件的预检发现误写成当前结果。

## 当前预检与发布负责人步骤

1. 此次在 Mac 对当前工作树运行 `python3 scripts/check_public_content.py`：3,127 个文件、0 项发现；这是有限模式扫描，不替代素材来源和授权核验。`check_version.py --component all` 与 `download_catalog.py check` 通过。说明提取、版本、下载索引、发布流水线、打包及续传工具回归通过，详见 Mac 交接提交。发布前对最终源码再次执行。
2. 核对两端未验收项和 TAB5 提示音再分发依据，明确实际交付范围。关键发送正确性、草稿与安装升级路径不通过时阻止对应平台晋级；未知项不得记作通过。沿用用户确认的宣传片顺序；只替换会误导的页面或镜头，记录仍适用素材的复用依据。
3. 确定版本、实际日期、完整中英文说明及共同源码 SHA；从干净源码原生构建和冻结所需桌面候选，或在同 SHA、同包哈希及证据条件确实成立时复用。TAB5 使用原生材料核对首刷、升级、sidecar、应用和对应完整源码并封存；替换包要登记新身份与受影响验收。
4. 按 `release_pipeline.py record` 记录每份实际候选及 `release-review`，包含证据、签名/公证状态、许可证、第三方声明、SHA-256 和剩余限制。完成原包校验后，由 Windows 负责人在适用的正式发布授权内统一创建标签、晋级和收尾；核对公开页面、中英文下载入口、安装说明与真实下载链接。

## English handoff

Windows owns the subsequent coordinated release. The Mac handoff branch contains the Windows `020d248` baseline plus the Mac read-state alignment. Desktop and TAB5 candidate versions remain independently tracked. The Windows DLL, Mac development ZIP, and TAB5 R6 image above have different source and acceptance identities and are not frozen public release packages. Finalize the bilingual draft and actual release date, select a common desktop source, validate and freeze native packages, and record exact-file evidence. The R6 Claude audio currently lacks established public redistribution rights. Outstanding real-device, first-consent, native-window and installation checks must not be marked passed without evidence. Preserve the approved video structure and update only misleading material.
