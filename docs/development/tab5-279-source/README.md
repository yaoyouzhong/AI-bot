# TAB5 0.2.279-ui 完整项目源码 / Full project source

这是实际构建并安装的 **.279 r2** 完整项目快照，补齐此前只有版本登记、部分补丁和说明的缺口。`source/` 包含 930 个项目文件，无需依次拼接 .234～.278 的覆盖包。下载的未修改依赖仍由锁定文件获取，不复制 SDK、工具链和编译目录；保留全部 7,531 个冻结输入摘要，以及依赖中实际修改的 6 个文件。

| 记录 | 身份 |
| --- | --- |
| 固件版本 | `0.2.279-ui` |
| 已验收应用 SHA-256 | `f6e540c750beb9b0ca908a221d4c80fd1caf170c112beaef2d299621387f8165` |
| 已验收 ELF SHA-256 | `a99a142b78ab18d325ed9bdfa7f8d3b3d96f8983d7f0b13d35c97e13dea63d04` |
| 应用大小 | 7,208,736 字节 |
| ESP-IDF | `5.4.2` / `f5c3654a1c2d2a01f7f67def7a0dc48e691f63c0` |
| NimBLE 子模块 | `0225775e49669cd4f070400908f73718bddce83b` |

`SOURCE.json` 记录归档文件摘要、镜像身份、来源和边界；`FROZEN-INPUTS.json` 对应安装候选构建时的全部输入。此次核验 7,531 个本地文件均与冻结摘要相同，归档保持原字节，包括 CRLF。没有重新构建或刷机，也不宣称已在独立环境逐位重建相同 BIN。

## 内容与许可

- `source/`：固件、自有脚本、配置、测试、文档、BSP、字体、词库及其许可。`source/README.md` 保持原始历史记录；本页和根 `SOURCE.json` 才是本次归档版本说明。源 CMake 的公开版本默认值也保持原件，构建 .279 必须显式指定本地版本。
- `managed-overrides/`：ESP-Hosted 4 个、LVGL 2 个修改后的完整文件。原始官方校验与候选校验见 `MANAGED-CHANGES.json`；对应许可及注册表摘要见 `dependency-metadata/`。
- `sdk/`：原工具链 SDK 的 8 个文件改动及 NimBLE 的 1 个文件改动，分别以补丁保存，附原许可。补丁已对实际本地 SDK 反向检查通过；SDK 不是此次重新下载构建的产物。
- 所有 44 个二进制输入与此前公开的 .234 源码包相同，包括项目生成花卉、服务标识、提示音、官方 BSP 图片及 44,194 字节的 Espressif `libesp_ipa.a`。后者是已有官方依赖，并非本次编译生成物；适用组件内 Espressif MIT 条款。服务标识不受本项目 MIT 商标授权。
- 自有源码沿用 [MIT](source/LICENSE)，第三方条款见 [原始来源声明](source/THIRD_PARTY_NOTICES.md)。基线来自此前公开 .234 源码以及维护者已提交的后续改动；未迁入其他项目 Git 历史或无许可证代码。缺失的 `photo_upload_retry.h` 曾按已提交协议独立重写，该文件是实际 .279 构建输入，不能视为恢复了旧工程原件。
- 不包含应用 BIN/ELF、私有 Flash 备份、Wi-Fi 密码、配对密钥、设备状态、账号信息或艺术图库集合。

## 校验与重建

先保留此目录作为只读归档，将 `source/` 复制到单独开发目录。不要在归档目录内构建，否则生成文件会影响后续归档检查。

```sh
python verify.py
```

1. 准备官方 ESP-IDF 5.4.2 固定提交并初始化其子模块，确认 NimBLE 提交与表格一致。安装对应工具链、Python 环境及 ESP32-P4 工具。将 `sdk/esp-idf.patch` 应用到 SDK 根目录，`sdk/nimble.patch` 应用到 `components/bt/host/nimble/nimble`。不要覆盖其他版本的 SDK。
2. 激活 SDK 环境，对工作副本的 `firmware/` 执行 `idf.py -D TAB5_LOCAL_ART=ON -D TAB5_LOCAL_VERSION=0.2.279-ui reconfigure`，让 Component Manager 按 `dependencies.lock` 取得依赖。保留实际 `sdkconfig`，不执行 `set-target` 重置配置。初次依赖解析与最后构建都必须指定版本。
3. 将 `managed-overrides/` 的内容按相同相对路径覆盖到工作副本的 `firmware/managed_components/`。这是锁定的候选最终文件；不必再依次应用旧版本补丁。官方未修改依赖不在本归档重复存放。
4. 运行 `python verify.py --resolved-project <工作副本> --sdk <SDK根目录>`。它应确认 7,531 个项目／依赖输入及 9 个 SDK 修改文件。额外的依赖缓存文件不参与比对。若配置或依赖解析改变了已记录输入，先核查原因，不把不一致当成已复现。
5. macOS/Linux 在相同配置和版本参数下执行 `idf.py build`。Windows 使用原 `source/scripts/build.ps1 -LocalArt -LocalVersion 0.2.279-ui`，并按 `IdfEnvironment.ps1` 将固定 SDK 与工具配置到工作副本 `.tools/`；该脚本保留 CPU 预算和进程优先级约定。不要使用 `-Legacy`、`-NoHid` 或 `-CrashTrace` 改变这份候选配置。

实际安装验收见 [.279 验收记录](../MAC-TO-WINDOWS-FIX-REFERENCE-20261025.md)。本次只补源码归档，不改变设备版本、设置或已发布下载入口。不同路径、主机、工具及构建时间可能改变 ELF/BIN；哈希清单证明来源字节，不等于可复现构建证明。

## English

This is the complete 930-file project snapshot used for the accepted .279 r2 build, not another incremental overlay. All 7,531 frozen project/dependency inputs match the retained local build. Unmodified managed dependencies are resolved from the lockfile; six modified ESP-Hosted/LVGL files are included separately with upstream and candidate hashes. Nine SDK/NimBLE modifications, exact commits and original licenses are retained. All 44 binary inputs are unchanged from the previously published .234 source package; the Espressif IPA library is an existing licensed vendor dependency.

Copy `source/` to a working directory, prepare the pinned SDK/submodule and apply the two SDK patches. Resolve locked dependencies with the explicit .279 local version and existing sdkconfig, apply `managed-overrides/`, then run `verify.py --resolved-project <project> --sdk <sdk>`. Build with the same local-art/version configuration; Windows uses the original resource-bounded build script and its `.tools` layout. Original source documentation contains historical versions and is preserved byte-for-byte. No private state, application images, toolchain or downloaded unmodified dependency tree is included. This archive does not perform a new build/flash or claim an independent bit-identical binary reproduction.
