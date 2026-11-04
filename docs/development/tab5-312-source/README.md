# TAB5 0.2.312-ui 完整工程源码 / Complete project source

[下载源码 ZIP](AI-bot-tab5-0.2.312-ui-source.zip) · [SHA-256](SHA256SUMS.txt) · [源码身份清单](SOURCE-MANIFEST.json)

本归档遵循此前 [.283 源码归档](../tab5-283-source/README.md)的规则：保留完整项目、配置、锁文件、测试、必要资源、修改过的依赖及 SDK 补丁；未修改的 SDK、托管依赖和工具链按锁定版本获取，不重复入库。无需叠加旧版项目补丁。

- 完整项目 **993 个文件**；重建输入 **7,594 项**，含 **6 个依赖覆盖文件**及覆盖 **9 个 SDK 文件**的补丁。
- 对应已验收的 **0.2.312-ui** 应用：6,907,792 字节，SHA-256 `21d78bcce33bdbdb531588b225b59a9902e53ecff620c9b614863f56cdb34d8c`。
- ELF SHA-256：`aa12dd1673e155c53a15c22a0a9a940443a4da3aa8520b452b59a5080891a39b`。
- ESP-IDF **5.4.2**，提交 `f5c3654a1c2d2a01f7f67def7a0dc48e691f63c0`；NimBLE `0225775e49669cd4f070400908f73718bddce83b`。
- 6,601 项依赖输入及锁文件与已有冻结记录一致；13,251 个 SDK 文件对照固定上游 Git 对象，仅发现已保留的 9 项修改，其余文本差异为 Windows 换行。详细记录位于包内 `SDK-BASELINE-VERIFICATION.json`。

## 核验与重建

1. 解压到新目录，使用 Python 3.11+ 运行 `python verify.py`，核验文件清单与所有摘要。保留原归档，将 `source/` 复制到独立工作目录。
2. 准备上述固定 ESP-IDF 及其递归子模块。在 SDK 根应用 `sdk/esp-idf.patch`，在 `components/bt/host/nimble/nimble` 应用 `sdk/nimble.patch`。
3. 保留 `source/firmware/sdkconfig`，不要运行 `set-target` 重置配置。按 `dependencies.lock` 获取依赖，配置时指定 `TAB5_LOCAL_ART=ON` 和 `TAB5_LOCAL_VERSION=0.2.312-ui`。
4. 将 `managed-overrides/` 内容覆盖到工作副本的 `firmware/managed_components/`，运行 `python verify.py --resolved-project <工作目录> --sdk <SDK目录>` 核对实际输入。
5. Windows 使用 `scripts/build.ps1 -LocalArt -LocalVersion 0.2.312-ui`，保留 CPU 与优先级限制；SDK／工具位置按 `scripts/IdfEnvironment.ps1` 配置。其他平台使用相同配置和版本参数执行原生 ESP-IDF 构建。

工程包括 Codex／Clawd 实际生成数据与对应原始输入，独立许可见包内 `supplied-art/NOTICE.md` 和 `licenses/companion-art/NOTICE.md`。用户已确认随固件及对应来源材料公开再分发的授权；素材不归入项目 MIT 许可。工程内的历史 README 保留原件，以本页和根清单标明的 .312 身份为准。

此次为源码同步，不重新构建或刷机，不创建固件 Release。原构建来自独立快照，`sourceCommit` 保持 `null`，不伪造原生 Git 提交，也不宣称已独立逐位重建相同二进制。BIN／ELF、运行数据、凭据、未修改依赖树及主机工具环境不包含在本包中。

## English

The complete .312 project archive follows the existing .283 policy: 993 project files, exact configuration, tests, required resources, dependency locks, six managed overrides and SDK patches covering nine files. It requires no older project overlays. Unmodified SDK/dependency trees, host tools, firmware binaries and private runtime data are excluded. The 6,601 dependency inputs and lock match the frozen baseline; comparison of 13,251 SDK files against pinned upstream Git objects found exactly the nine retained changes, with other text differences limited to Windows line endings.

Extract into a new directory and run `python verify.py` with Python 3.11+. Prepare the pinned SDK/submodules, apply SDK/NimBLE patches, preserve sdkconfig, resolve locked components and apply managed overrides. Verify the resolved project and SDK, then build with local art and version 0.2.312-ui. Artwork retains its separate recorded redistribution authorization. The accepted BIN/ELF identities are listed above; this source-only synchronization neither flashes nor publishes firmware and makes no independent bit-identical rebuild claim.
