# Windows 交接：TAB5 厂商入口稳定性与图标

## 范围和源码身份

- 来源为 Mac 对齐分支；接收端以 Windows 分支 `codex/windows-dynamic-microphone-20261105` 的 `6456c169c3f92222c1a233489da36af347b8ed28` 为基线，开始前核对远端新提交与本地改动。两条分支各有独立开发，**不要直接用 Mac 分支覆盖 Windows 桥接**。
- 本交接提交所在的 Mac 分支包含 `ddc9b64`、`cd7636d`、`479d2ad`、`c31be1e` 和 `6f3d273`；其中 `479d2ad` 是 macOS 的 Terminal 权限复查修复，不能直接移植到 Windows。共同目标是会话目录短暂缺项时避免入口闪动、保留当前会话及草稿，发送时依然校验真实会话和权限。
- 没有协议或最低版本变化。电脑端版本、TAB5 发布版本及发布负责人尚未确定；本交接只授权源码提交和推送，不包含标签或发布。

## Windows 端待完成

| 项目 | 状态 | 依据与验证要求 |
| --- | --- | --- |
| Codex 目录 | 待开发、待 Windows 验证 | Windows `Tab5CodexTasks.SnapshotLimited` 使用 `Tab5CodexCatalog.Recent`；数据库或项目读取未就绪等路径可能返回空列表。区分短暂读取异常与持续、有效的空目录，避免一次异常立即抹去已显示入口；不要让缓存放宽真实发送的目标、权限或草稿校验。 |
| Claude 目录 | 待开发、待 Windows 验证 | Windows `ClaudeCatalog.Refresh` 发布扫描结果，`ClaudeWorkspace.Snapshot` 使用该目录。复核短暂空扫描、恢复、长期真实空目录和禁用路径；与 Codex 独立处理。不要借用另一会话的控制或回执。 |
| 原生 Terminal 发送状态 | 待 Windows 验证 | Mac 的 TCC 权限复查是平台专用实现；Windows 依据自己的终端权限、会话身份与输入目标判断，验证稳定性并保持发送前检查。 |
| TAB5 UI 与设备保护 | Mac 构建及 USB 有限真机通过；Windows 构建和 Windows 桥接真机待验证 | 在已核验的 R4 工程上应用下述累计补丁；验证一个、两个、更多厂商、短暂缺项、持续空目录、切换与未读角标。设备端防抖适用于两种桥接。 |
| 项目列表辨识 | Mac 与新 TAB5 固件开发中；Windows 待开发 | [项目显示设计](TAB5-PROJECT-PICKER-20261110.md)要求经 Git 元数据确认的同仓库 worktree 合并，同名不同仓库区分；新固件将验证通过的项目分页合并并滚动，旧固件保持分页。Windows 自行实现路径处理，并调整每项目 8 条的桥接侧上限，保留会话真实目标和草稿。 |

## TAB5 源码与重建

1. 按 Windows 分支 `docs/development/tab5-318-r4-source/README.md` 解压已归档 .318 工程，执行 `verify.py`；在独立项目目录应用 R4 增量的 `--check`、`--apply`、`--verify`。
2. 使用本交接分支的 [ICON2 累计补丁](TAB5-PROVIDER-ICON2-20261110.patch)，其 SHA-256 为 `d6788f5621c7679bdb84b2b85c801aefb80272833d4d129118e2abf7a0b460e6`。补丁已包含前一次 ICON1 的变化，**只应用 ICON2**。在独立 TAB5 项目根目录先执行 `git apply --check <ICON2补丁绝对路径>`，通过后执行 `git apply <ICON2补丁绝对路径>`。
3. 按 R4 清单准备锁定工具链、依赖和本地授权素材，Windows 原生构建并记录新镜像身份；不得把不同构建的结果当成 Mac 实测原包。Mac 构建使用的 `claude_notice.pcm` SHA-256 为 `1533c2d5e353d28726cc3fc8cc5108df72b8a42349415a55806c2f0aaee5f023`，素材不包含在 R4 或 ICON2 源码增量中。详见 R4 来源说明。

## 已有证据和剩余验证

- Mac：`swift test` 511 项、0 失败、4 项跳过；`swift build -c release` 通过。空目录保持、恢复、持续为空、失败及禁用有离线回归。Mac 签名候选源码 `6f3d27317d397b3305b454450ca61eda89d2c1fb`，主程序 SHA-256 `0c215c09271a5caed627a41ce93dcc63f42c3467ec1d18f9c30b5b0d3f99306b`；安装前后加密草稿摘要一致。
- TAB5：Mac 使用 ESP-IDF 5.4.2 完整构建并通过离屏 LVGL、升级说明预览、生产镜像校验；版本 `0.2.318-ui · R4-ICON2`，BIN SHA-256 `fcee93bffeffba6c8fa01e4ab04ff7bbc890647349c0a9bae422b7c2e114a2c6`、ELF SHA-256 `1f82688bfde3c11b66717e5a1b9cdfbf5326e0d62372182f164c43840a2c0c82`。USB 安装确认精确 ELF、VALID 分区和展示设置保留。
- 真机对话页两个 Logo 可见，Codex/Claude 切换后原生终端会话保留；六次画面样本跨约 46 秒均显示两个入口且 Claude 发送按钮可用，**不是连续观测**。真实未读角标没有出现；14 px 数字仅经离屏渲染及 `9+` 边界断言，待真实未读时按视距检查。短暂缺项的实际来源未捕获，需继续观察。
- Windows 原生构建、自动测试和对应桥接下的真实设备验证均待完成；本次没有运行 Windows 测试。共享协议保持不变，不以 Mac 真机或源码检查代替 Windows 验收。
