> 最新状态见 [单一电脑归属与 Mac 迁移](TAB5-SINGLE-HOST-20261022.md)：.237 已实际启动并核验，.238 为后续蓝牙兼容候选；下文的 .236 与 Windows 摘要是历史快照。

> 2026-10-22 实际安装/验收更新：Windows 已安装最终心跳兼容版本（DLL 32f7d2a...），.236 已在 ota_0 VALID 启动并核验精确 ELF，USB 90 秒稳定性通过。拍照仍复现 6/12；现有 PIN 在已选输入页上成功，人脸/指纹页需要先手动选择 PIN/密码，自动登录方式切换未实现。一位密码按用户要求推迟。以下 2026-10-21 状态为历史快照，以 TAB5-236-INSTALL-ACCEPTANCE-20261022.md 及任务目录 ACCEPTANCE-20261022.md 为当前验收结论。

# TAB5 多台可信电脑与分支集成记录（2026-10-21）

## 范围与隔离

- Windows / TAB5：`codex/windows-tab5-multihost-20261021`，工作目录 `C:\Users\yaououzhong\Work\AI-bot-windows-multihost-20261021`，基线 `c2240d96b3cc2bf17f8ab16352d4626f4d05e2b3`。
- Mac 适配：`codex/mac-tab5-multihost-adapter-20261021`，工作目录 `C:\Users\yaououzhong\Work\AI-bot-mac-adapter-20261021`，基线 `bf16f1f9becd172407c51297fadb0b0bce57fb8f`。保留该基线已有的五阶段功能，包括 BLE、OTA、资源和 Codex 能力。
- 本次改动位于独立 Git worktree，尚未提交、推送、打标签、合并主干或发布。原始混合 checkout 中任务开始前的工作保留。
- 固件工程不在本仓库生产源码目录中；从公开 .234 对应源码应用 [.235 修复](TAB5-CAMERA-PASSWORD-235.patch)，再应用 [.236 增量](TAB5-MULTI-HOST-236.patch)。原始源码、构建与原生测试日志保存在 `C:\Users\yaououzhong\Work\AI-bot\artifacts\development\multi-host-20261021`。
- Mac 的独立适配补丁见 [MAC-HOST-BINDING.patch](MAC-HOST-BINDING.patch)，适用于上述 Mac 基线，不能直接应用到旧版 Windows 发布基线。

## 行为

- 设备最多保存 8 台可信电脑；每台首次经 USB 明确配对，保留独立密钥和已有配对，不共享密码。
- 同时只选中一台电脑。认证成功的 USB 主机优先接管；拔线后保留该电脑的 Wi-Fi / BLE 目标。其他电脑待命，不因广播抢占。设备网络诊断页支持选择已信任电脑，控制页显示当前电脑。
- 接管绑定设备身份、一次性 nonce、端点和主机身份；切换前阻止新按键、取消并排空旧输入。状态、RPC 与 HID 授权绑定主机代次，升级中拒绝另一台电脑接管。损坏信任记录、容量满和保存失败不静默覆盖旧记录。
- 保留 .235 的拍照内存交接修复及 Windows 1–64 个可打印 ASCII 字符密码。数字键盘默认，支持大小写、符号及空格。Mac 密码扩展仍按用户约定留待 Mac 开发处理。
- 详细字段、兼容与认证约定见 [协议](../PROTOCOL.md)。新版 USB 自动接管需要配套桥接；旧无线协议只在设备明确选中其密钥时继续使用。

## 已完成的本地验证

- Windows Release：0 警告、0 错误；`--status-once` 在普通 Explorer 桌面环境通过，未绕过 AppProfileGuard。
- Windows：可信电脑 19 项、PIN 248 项、Wi-Fi 解锁 298 项，以及加密 RPC、OTA 鉴权失败提示/恢复、64 KiB 状态预算和连续/差量传输回归通过。
- ESP-IDF 5.4.2 最终交叉编译及离线镜像校验通过。原生可信电脑测试覆盖迁移、独立密钥、nonce/端点/会话、旧输入失效、USB 优先、NVS 保存失败、首次写入断电恢复、容量和损坏记录。
- 相机、图库、照片附件、键盘、遥测接收等相关原生回归通过；完整原生界面及主机选择、USB 占用提示、实际 sidecar 顶部和滚动截图已查看。截图是隔离夹具，不是真机证明。
- 固件 19 文件增量、Mac 7 文件适配补丁在干净基线上重放通过。固件比较仅归一化 LF/CRLF；编译源码的原始 SHA-256 保留于 CANDIDATE.json，未声称字节相同。
- 中英文候选更新日志同步；许可与全部交付文件 SHA-256 单独复核。`git diff --check` 通过。

## 候选与真实运行状态

- TAB5 `0.2.236-ui`，7,150,592 字节，SHA-256 `0ef426380b135e3eaa0622b3094e77c605237e262ea0315dd816eaf5dc628a6e`。
- ELF `91693b6155750a957f384d9addc13540ba904226cf1ebe77353da4cf3be1cb3b`。固定入口 `C:\Users\yaououzhong\Work\AI-bot\artifacts\firmware\tab5\latest\aibot_tab5.bin`；归档 `versions/0.2.236-ui/0ef426380b13-07619396`。
- 新 Windows 多主机候选 DLL `70f1aea8efd3faf714f647bb16be14d31429e2ae4f20f0c8de1288328c0bcb09`，目录 `C:\Users\yaououzhong\Work\AI-bot\artifacts\development\multi-host-20261021\windows-branch-build`，尚未安装。
- 17:57 已安装的是此前拍照/密码候选 Windows DLL `966be7eb8a374c95f388ab92e4c1e710790a84a8397a96d97a979151b1e9189d`。配对、设置及已保存凭据文件核对一致，原桥接已恢复运行。
- Windows 最后看到设备为 .234，实际 USB RPC 返回 `401 / HMAC rejected`，固件传输为 0 字节。18:44 只读预检时 COM9 已不在 Windows，未读到分区，也未写入任何固件。不能将该故障写成 Flash 损坏或固件传输完成。

## 合并前必须完成

1. 审查两个分支各自的最终 diff 和共享协议；对当前主干重新执行合并预检。历史基线能合并不代表最新工作自动通过；具体结果见任务目录 MERGE-PREVIEW.json。
2. 在 Mac 执行 `cd mac-app && swift test && swift build -c release`，并确认已有五阶段能力保留。此 Windows 环境未执行 Mac 编译，不得填为通过。
3. 在设备接回后，使用保留当前 Mac 信任的升级路径解决旧固件配对鉴权边界，核验精确 ELF、运行分区和实时心跳；不得用工厂全量刷写覆盖配对/分区来掩盖故障。
4. 真机依次验证两台可信电脑首次配对、USB 来回切换、非当前 BLE 待命、拔 USB 后 Wi-Fi / BLE、重启保留、旧按键/回复不串到新电脑，以及升级期间接管被拒绝。
5. 完成连续拍照/重拍、图库后拍照、附件上传，以及 USB、BLE、Wi-Fi 一位密码真实解锁。真实 Windows 密码须已满足测试条件；不在聊天、日志或仓库记录密码内容。
6. 通过评审和上述检查后，按用户的具体授权合并或推送；CI / 发布工作流本次不改。当前未满足真机和 Mac 验收，不能标记可发布。

新 .236 包增加多主机功能，区别于此前明确指定的 .235 刷写对象。当前只准备可审查候选，没有将旧授权扩大为 .236 实际刷写。

## 本次合并预检结果

当前未提交改动已在一次性 bare 仓库内构造审查快照，真实分支、索引和主干均未合并。业务源码无文本冲突；共享 `docs/PROTOCOL.md`、`CHANGELOG.md` 和 `CHANGELOG.zh.md` 共 3 处文档冲突已逐项查看，临时集成方案保留双方内容。解决文件和补丁保存在任务目录 `integration-docs/`、`INTEGRATION-DOCS-RESOLUTION.patch`；这不代表真实分支冲突已消失。

在该文档解决方案下，与当前主干 `a2e0b577d991373c0877472016942804dd179873` 的再合并预检退出码为 `0`。该结果仅是 Git 文本集成检查，不替代 Mac 编译和真机验收；后续分支变化必须重跑。
