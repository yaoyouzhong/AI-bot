# TAB5 0.2.283-ui 完整源码归档 / Complete source archive

[下载完整源码包](AI-bot-tab5-0.2.283-ui-source.zip) · [SHA-256](SHA256SUMS.txt)

本包对应当前交付的 `.283` 应用固件，包含全部 941 个项目文件、实际配置、测试、6 个修改后的依赖文件、9 个 SDK/NimBLE 修改文件的补丁和原许可。无需下载或叠加旧版本源码包。

| 身份 | SHA-256 / 值 |
| --- | --- |
| 应用版本 | `0.2.283-ui` |
| 应用 BIN | `729fb34ea484ba8025399cb736e737d229fa524aeb08a963578bc14361139569` |
| ELF | `af91fa99171abbc94409dbfe2e775ef85101f82ea20a0cb7f7698feb4ddbe7bf` |
| 应用大小 | 7208800 bytes |
| ESP-IDF 基线 | `5.4.2` / `f5c3654a1c2d2a01f7f67def7a0dc48e691f63c0` |
| NimBLE 基线 | `0225775e49669cd4f070400908f73718bddce83b` |

## 核验

- 941 个归档项目文件与 `.283` 构建工程逐字节一致；与已公开 `.282` 完整源码比较，只有 `firmware/main/ui_dashboard.h` 和 `tests/ui-preview/preview.c` 改变，分别移除重置卡数量旁的上滑提示、验证数量文本及滚动仍可用。原文件字节和换行格式保留。
- `RESOLVED-INPUTS.json` 冻结本次核对的 7,542 项项目与依赖输入。全部未修改依赖与已有 .279 冻结记录一致；6 个依赖覆盖文件和 9 个 SDK 修改文件逐项一致。
- 原构建 BIN 与当前 `latest` 交付 BIN 完全相同；保留 ELF 的摘要也与交付清单一致。
- 使用原编译器、参数、路径、配置和 SDK 重新编译包含改动的 `ui.c`，输出对象与原构建对象逐字节相同，摘要 `8a330d4ba9062700c8896f2bf3085ac9b48af8966f188c6c1e1861cd99ce0879`。这是同一环境的 UI 对象核验，未执行独立环境整固件逐位重建。
- 本次只归档源码；没有重新刷机、发布新的固件 Release 或改变已有下载版本。原项目文档和 `README-281.md`、`README-282.md` 含历史版本，以本页和 `SOURCE-MANIFEST.json` 为本包说明。

## 校验和重建

解压后先运行 `python verify.py`。`FILES.sha256` 覆盖全部其他文件；`SOURCE-MANIFEST.json` 登记版本、固件摘要、来源与文件清单。

1. 将 `source/` 复制到新的工作目录，保留原归档用于核验。准备上述固定 ESP-IDF 及 NimBLE 子模块、对应 ESP32-P4 工具链（GCC esp-14.2.0_20241119）。
2. 在 SDK 根应用 `sdk/esp-idf.patch`，在 `components/bt/host/nimble/nimble` 应用 `sdk/nimble.patch`。这些补丁保留原始 SDK 改动，勿覆盖其他版本。
3. 保留 `source/firmware/sdkconfig`，不执行 `set-target`。激活 SDK 环境，在工作副本的 `firmware/` 执行 `idf.py -D TAB5_LOCAL_ART=ON -D TAB5_LOCAL_VERSION=0.2.283-ui reconfigure`，按 `dependencies.lock` 解析官方未修改依赖。
4. 将 `managed-overrides/` 下的内容按同一路径覆盖到工作副本 `firmware/managed_components/`。运行 `python verify.py --resolved-project <工作副本> --sdk <SDK目录>` 核对全部 7,542 项输入和 9 个 SDK 修改文件。
5. Windows 按 `source/scripts/IdfEnvironment.ps1` 配置工作副本 `.tools/`，运行 `./scripts/build.ps1 -LocalArt -LocalVersion 0.2.283-ui`，保留脚本的 CPU/优先级限制。其他平台使用同样的配置和版本参数运行 `idf.py build`。不要使用 `-Legacy`、`-NoHid` 或 `-CrashTrace` 改变本候选配置。

可用 `python verify.py --firmware <已交付BIN> --elf <原构建ELF>` 核对应用身份。跨目录、构建时间、主机及工具差异可能影响输出；完整源码归档和输入一致性不等于已证明可复现二进制。

## 来源与许可

已公开 `.282` 源码包 SHA-256：`4d2ab52dcbf6d9ce2ccd6febcc22a25b179fd23af03ae0d677f822c5dce50849`。本包保留该完整基线及其许可证，并使用本次 `.283` 工程的全部实际项目文件。`CHANGES-283.json` 和 `no-swipe-283.patch` 提供两处修改的摘要与审查差异。自有源码 MIT，第三方组件、字体、词库和标识保留原条款，见 `LICENSE`、`THIRD_PARTY_NOTICES.md`、`licenses/` 及组件内许可证。

包含依赖锁、修改后的依赖文件及 SDK 补丁；未修改的官方 SDK、依赖树和工具链按固定版本获取。包内没有应用 BIN/ELF、设备备份、账号状态、密码、配对密钥或艺术图库集合。

## English

This self-contained archive contains all 941 project files from the retained .283 build, exact configuration and tests, six modified managed dependency files, SDK/NimBLE patches covering nine files, and original licenses. No older source archive or overlay is required. All 7,542 resolved project/dependency inputs were checked and are recorded in `RESOLVED-INPUTS.json`.

The retained build BIN is byte-identical to the current delivered BIN; the ELF hash also matches its delivery manifest. Only the reset-card hint and its scrolling/text regression test differ from the published .282 project. Recompiling the affected UI translation unit with the original compiler, command, configuration, SDK and source path produced a byte-identical object. Independent whole-firmware binary reproduction is not claimed.

Run `python verify.py`, copy `source/` into a separate working directory, prepare the pinned SDK/submodule, apply the two SDK patches, resolve locked dependencies without resetting sdkconfig, and apply `managed-overrides/`. Run the resolved-project/SDK verification above. Build with `TAB5_LOCAL_ART=ON` and `TAB5_LOCAL_VERSION=0.2.283-ui`; Windows uses the supplied resource-bounded build script. Historical README files remain for provenance; this README and `SOURCE-MANIFEST.json` describe .283. This source-only synchronization neither flashes hardware nor publishes a firmware Release.

源码 ZIP SHA-256 / Archive SHA-256: `0750f5f1dd9d77165dc45f88b70070a434c44b582ecc42e4db9967186e092898`
