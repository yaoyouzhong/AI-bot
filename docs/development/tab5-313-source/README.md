# TAB5 0.2.313-ui 完整工程源码 / Complete project source

[下载源码 ZIP](AI-bot-tab5-0.2.313-ui-source.zip) · [SHA-256](SHA256SUMS.txt) · [源码身份清单](SOURCE-MANIFEST.json)

本包在已核验的 [.312 完整工程](../tab5-312-source/README.md)上整合 Mac 分支 `9973f9b25083c0d35a696b947f55dd2327587820` 的全部 25 项固件修改，并逐项匹配[交接摘要](../CLAUDE-WORKSPACE-SOURCE-MANIFEST-20261104.json)。无需叠加旧版本补丁；沿用 .283 的归档规则，保留项目、配置、测试、资源、锁文件、修改过的依赖及 SDK 补丁，排除未修改的依赖树、工具链、缓存、BIN/ELF 和私有运行数据。

- 完整项目 **999 个文件**、**7,600 项**重建输入摘要、**6 个**依赖覆盖文件，以及覆盖 **9 个 SDK 文件**的补丁。
- 对应 Mac 原生构建并经 USB 真机核验的 .313 应用：**6,923,744 字节**，SHA-256 `e0c0981beae8525e74fc1aeb3880904aeb95df909d63f5b9a39162ec72deb587`。
- ELF SHA-256：`e5f52c3aff72315dec130342b4a16705b3205ba60da3cb41bc367ba4ffeb8ef7`。
- ESP-IDF **5.4.2**，提交 `f5c3654a1c2d2a01f7f67def7a0dc48e691f63c0`；NimBLE `0225775e49669cd4f070400908f73718bddce83b`。依赖和 SDK 修改沿用已核验的 .312 基线。

解压到新目录后运行 `python verify.py`。准备固定 SDK 及子模块，应用 `sdk/esp-idf.patch` 和 `sdk/nimble.patch`，保留实际 `sdkconfig`，按锁文件获取托管组件并应用 `managed-overrides/`。然后运行 `python verify.py --resolved-project <工作目录> --sdk <SDK目录>`。构建使用 `scripts/build.ps1 -LocalArt -LocalVersion 0.2.313-ui`，其他平台采用同配置的原生 ESP-IDF 构建，详见包内 README。

原始 Codex／Clawd 输入和实际生成数据均保留，公开再分发授权及第三方归属见包内 `supplied-art/NOTICE.md` 和 `licenses/companion-art/NOTICE.md`，不归入项目 MIT 许可。独立工程快照的 `sourceCommit` 保持 `null`；Mac 增量的仓库提交另行登记。本次仅同步对应源码，未重新构建、刷机或发布固件，也不宣称独立逐位重建相同二进制。

## English

The complete .313 project integrates all 25 firmware changes from Mac commit `9973f9b25083c0d35a696b947f55dd2327587820`, verified against its exact file hashes. It contains 999 project files, 7,600 resolved-input hashes, six managed overrides, SDK patches, licenses and authorized artwork inputs. No older project overlay is required. Unmodified dependencies, host tools, build binaries and private runtime data are excluded under the existing archive policy.

Extract and run `python verify.py`, prepare the pinned SDK/submodules and patches, resolve locked dependencies and apply managed overrides, then verify the resolved project/SDK before building with local art and version 0.2.313-ui. The application and ELF identities above come from the Mac native/USB handoff. This source synchronization does not rebuild, flash or publish firmware, and makes no independent bit-identical rebuild claim.
