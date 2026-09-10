# 发布前检查 / Release readiness

本页保留 2026-09-09 本地准备证据，并补充 2026-09-10 源码与分发材料收尾，不宣称正式版已发布。
带日期的迁移/验收文档保留为历史证据，不能将其中的 PID、COM 号、候选哈希或等待操作当作当前指令。

## 仓库与公开内容

- GitHub `yaoyouzhong/AI-bot` 已经是公开仓库，仓库许可证标识为 MIT；当前没有 GitHub Release。
- 本地 HEAD：`ee964a34a6ab7f056286694198d8c3390d6f13b0`。
- 本次读取的远端 main：`acff6435d705e97afcb1fbe75ddae6bc82375e27`。
- `rev-list --left-right --count HEAD...<remote-sha>` 为 `32 0`：远端是本地祖先，不需要覆盖或重写远端历史。
- 当前还有大量未提交迁移文件；本次没有暂存、提交、推送、打标签或改变版本。最终提交必须包括必要的新增源码、资源、测试与文档，不能只提交已跟踪文件。
- 私有 APET、页面标志、账号缓存、浏览器 profile、配对数据及旧设备备份不属于公开内容。公开默认桌宠为 BYTE SPROUT，不承诺包含维护者个人导入的旧机器人素材。
- `scripts/check_public_content.py` 检查当前已跟踪和未忽略文件的敏感路径及少数高置信度密钥模式；`--history` 检查本地可达历史 blob。只报告路径/行号/规则，不输出匹配值。零命中不等于完整安全或许可证审计。
- 本次检查：当前 177 个候选文件、410 个本地可达历史 blob 均为 0 命中；不覆盖未拉取的远端引用或 GitHub 外部附件。
- 国产授权模块旧版完整 blame 复核为维护者两种署名：姚有忠 1689 行、yaoyouzhong 177 行，与既有来源记录的 1866 行相符。
- 来源记录仍以 `PROVENANCE.md` 为准。本次复核 CompletionChime 的旧版 blame（84 行均为维护者），并确认新旧 app-icon.ico SHA-256 相同：`09EF535337CDD1295D2337F3A3213732C55D1584F8DF5485343C9AE4B038F904`。作者记录不是对全部外部素材的授权证明；第三方依赖仍遵循自己的许可证。

## 可重复的本地验证

在仓库根目录执行：

```powershell
powershell -NoProfile -File scripts/verify_release_local.ps1 -Firmware
python scripts/check_public_content.py --self-test
python scripts/check_public_content.py --history
```

脚本将当前可提交源码复制到新的 `artifacts/release-check-*`，不带原有 bin/obj/.pio、Git 历史或私有缓存。Windows Release 从副本还原依赖并编译；`--self-test-public` 在初始化前选择空白测试用户目录，屏蔽 Windows 凭据和厂商环境密钥，再执行空状态页面及合成数据回归。仅合成 LAN 测试使用随机 loopback 端口；不启动托盘、串口、真实授权窗口，也不调用运行实例的退出信号。构建仍使用本机 SDK/包管理器，不是完全封闭的容器环境。

本次证据：

- Windows 最终源码副本 `artifacts/release-check-a62f2f6dfb6e4a059cd55fdfff9ce815`：Release 0 警告/0 错误，`PUBLIC_SELF_TEST_OK`、`LOCAL_RELEASE_CHECK_OK`。
- 覆盖：空账号/无私有资源页面、天气/股票/额度解析、Codex 生命周期、循环策略、桌宠帧时序/资源校验/独立选择、合成 LAN 认证与资源、GLM、系统数字 2 秒间隔、布局缩放和镜像。图片是 Windows 渲染证据，不是设备像素验收。
- 增强空账号断言时曾误要求外层 QuotaSnapshot 必须为 null；代码实际返回两个厂商字段均为空的容器。已按无账号数据的契约检查两个字段，并重新通过完整脚本，不跳过测试。
- 固件源码副本 `artifacts/release-check-461f2e9ce8384848a96baf4a4ce0b7d4`：PlatformIO SUCCESS，RAM 44228 / 81920，Flash 521775 / 1044464。本轮没有修改固件，也没有上传。仍有触控引脚、未使用变量/函数、缩进和日期缓冲区静态分析警告；不是零警告构建。
- CI 新增公开内容检查、Windows 隔离回归和 macOS `swift test` / Release build；仍保留固件构建。工作流仅本地修改，尚未推送运行，不宣称云端或 Mac 已通过。

## 正式发布前剩余门槛

1. **长期运行验证**：已查明工具启动的桥接属于 Codex 持有、设置了 kill-on-close 的 Job，历史消失时间与 Codex 重启吻合；未对历史退出逐次取得终止审计。改由资源管理器启动后，不再匹配 Codex 持有的 Job。启动闪窗另已通过 WinExe 修正，用户反馈“现在 ok 了”。这些即时验收不替代长期稳定性验证；后续准备不停止当前实例。
2. **Wi-Fi 与 macOS**：在可达网络中完成 USB 失联 → LAN 数据/资源 → USB 恢复的端到端验证；Mac 构建、真实权限、菜单与设备行为另验。
3. **最终候选验收**：无私有缓存的离线自测不等于全新安装、真实账号授权、实际屏幕视觉和稳定性通过；需针对确定的候选产物记录结果。
4. **分发流程**：本地候选脚本已补齐源码归档、Windows 许可与 SDK 原样 DLL 校验、固件对应源码/许可/重建材料；详见下方 2026-09-10 更新。正式 Release 工作流尚未接入这些本地检查，仍须另行接入并验证；不能直接以旧标签流程发布。
5. **提交与远端验证**：整理精确提交内容和 SHA，经授权推送 main，观察三平台 CI；最后单独确认版本、标签及 Release。不得用旧远端 CI 成功替代当前源码验证。

### 2026-09-09 Windows 候选包历史阻塞

当时普通 NuGet 依赖的许可文本已自动收集，但 `Microsoft.Windows.SDK.NET.Ref/10.0.19041.56` 提供的两个 DLL 不在普通 libraries 列表。旧候选仅有原始 nuspec，并通过 `RELEASE_BLOCKERS.txt` 标记不可作为公开成品。该历史阻塞已通过下述原始条款与官方 REDIST 核对处理；旧 ZIP 本身没有被改写，仍不作为新材料包使用。

当时带阻塞说明的候选位于 `artifacts/release-check-1bcfafa0374b44d4878798628f00c6fb/AIBotBridge-0.1.0-local-candidate-win-x64.zip`，包含 36 个被校验的文件。旧候选及更早无补充材料的候选均保留为历史证据，不用于本次交付。

### 2026-09-10 源码与分发材料

- 复核维护者迁移来源、原创 ICO 和三像素合成测试数据；已审核图片/测试数据的哈希纳入材料检查。加强源码内容检查，拦截误跟踪的构建/私有目录和未审核图片/二进制；`.gitignore` 补充运行数据与凭据路径。检查仍不等于完整安全审计。
- 保存 Windows SDK 包指向的原始 RTF、便于阅读的文本，以及[官方 REDIST 清单](https://learn.microsoft.com/en-us/legal/windows-sdk/redist#microsoftwindowssdknetref)。清单明确列出两个 net8.0 DLL。打包核对实际 DLL 与已审核 targeting pack 的 SHA-256，随包携带原始条款、清单、元数据、版本清单及分发说明；版本或证据变化时失败，不再用“只有 nuspec”替代许可。
- 固件依赖锁定为本机既有解析版本：平台 4.2.1、core 3.1.2、TFT_eSPI 2.5.43、ArduinoJson 7.4.3、WiFiManager 2.0.17。材料包保留实际 core/库/平台源码与组件声明、应用固件源码、GCC runtime/newlib 声明、`rebuild.ini` 和文件校验；不含 Flash 备份或私人资源。
- Windows 候选、公开源码 ZIP、固件材料 ZIP 均从隔离源码副本生成。公开源码严格按 `SOURCE_FILES.sha256` 收集并重新检查，不递归打包整个工作目录。Windows 解压回归和材料 ZIP 的逐文件校验是打包门槛。
- 本轮不改变版本、标签、远端或 CI/Release 工作流，不部署桥接或刷写设备。长期运行、Wi-Fi 回退、Mac、最终安装/账号/实体视觉验收及正式发布流程接入仍是独立剩余项。

## English summary

The repository is already public; local main is 32 commits ahead of the inspected
remote, with additional uncommitted migration work. No push, tag or release was
performed. Isolated Windows build and fixture tests passed; a separate firmware
build passed with warnings and was not flashed. CI changes are local and macOS
has not been validated. Private imported artwork and account data are excluded.
Tool-owned kill-on-close jobs explained the bridge lifetime risk; Explorer launch
and the WinExe console fix have received immediate acceptance. The 2026-09-10
source/materials closeout adds SDK terms and explicit REDIST evidence, original-DLL
hash checks, pinned firmware dependencies, corresponding sources/rebuild materials
and source archives. Remaining gates include sustained runtime, reachable-network
Wi-Fi fallback, Mac/hardware and final-install acceptance, integration of these
local packaging checks into the official release workflow, and explicitly approved
push/release payloads.
