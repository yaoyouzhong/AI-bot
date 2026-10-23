# 当前开发基线 / Current development baseline: 0.2.264-ui

开发分支已归档 [.264 增量源码与配置](tab5-264-source/README.md)，按其说明在 .262 覆盖包后应用。实机 Wi-Fi 安装与精确镜像启动核验通过；OTA 速度问题留待后续处理，详见[验收记录](TAB5-OTA-264-ACCEPTANCE.md)。以下 .234 链接仍是既有正式发布的源码记录。

The development baseline is .264; apply its overlay after .262. Exact Wi-Fi installation and boot verification passed. OTA performance remains a deferred issue. The .234 archive below remains the existing published source record.

# 当前 TAB5 .234 对应源码 / Current corresponding source

[下载 .234 源码包](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.234-ui/AI-bot-tab5-0.2.234-ui-source.zip)。包内 SOURCE-MANIFEST.json 登记实际应用 SHA-256、223 项主目录输入及全部 962 项源码／验证文件，附 SDK 基线、重建步骤和许可。不宣称已独立逐位重建相同二进制。首刷与升级包使用同一应用字节，首刷私有区域保持擦除。实机 ELF／VALID／三实时样本与电量观察见 [验收](TAB5-BLE-UPTIME-20261019.md)。

The linked .234 archive supplies all 962 corresponding-source/validation entries, exact application/main-input hashes, SDK baseline, build instructions and licenses. Independent bit-identical rebuilding is not claimed. First-install and OTA share application bytes; physical acceptance is documented separately. Earlier source records below are historical.

# TAB5 开发者源码 / Developer source

普通用户按[图文指南](../INSTALL.zh.md)选择首刷或升级包，无需源码。

## 当前版本：0.2.217-ui

下载 [TAB5 0.2.217-ui 源码与构建快照](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.217-ui/AI-bot-tab5-0.2.217-ui-source.zip)，校验同一发布页的 [SHA256SUMS.txt](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.217-ui/SHA256SUMS.txt)。

| 身份 | 值 |
| --- | --- |
| 源码提交 | `d31a601289dbfc4edf8da82d0f91a7f926dbd090` |
| 对应应用 SHA-256 | `bb3a94f9e1862c200a81d5176852966663b0d456559f8f59e8b1eed0714d3e96` |
| 源码 ZIP SHA-256 | `0efbc045cdb5b35e69a64ee1b4b13f756639be7fd7fec5c0f3746cefe1b73176` |
| ESP-IDF | 5.4.2，commit `f5c3654a1c2d2a01f7f67def7a0dc48e691f63c0` |

以包内 `SOURCE-MANIFEST.json`、`source/README.md`、`dependencies.lock`、实际 `sdkconfig` 和 SDK 差异记录为准。准备相应官方 ESP-IDF 环境后按工程说明构建 `source/firmware/`。源码快照不是刷机文件；依赖锁定不等于已在独立环境复现逐字节相同的 BIN，清单明确记录 `reproducibleBinaryVerified: false`。

自有源码 MIT，组件、字体与词库保留原许可。图库支持包含在固件中，艺术图像另行下载；许可和镜像验收分别见[许可范围](../TAB5-LICENSE-SCOPE.md)与[本版验证边界](../RELEASE-0.6.9.md)。

The current developer archive is 0.2.217-ui and matches the source/application identities above. Check the release checksum, then follow the included manifest, README, dependency lock, sdkconfig and SDK patch with ESP-IDF 5.4.2. It is not a flashing package; byte-identical independent reproduction remains unverified. Own code is MIT, third-party terms remain intact, and artwork collections are separate.

<details>
<summary>历史 0.2.89-ui 快照 / Historical snapshot</summary>

### 0.2.89-ui 历史资料

普通用户按[图文指南](../INSTALL.zh.md)下载首刷或升级包，无需本页源码。

开发者下载：[TAB5 0.2.89-ui 源码与构建快照](TAB5-0.2.89-ui-source.zip)。精确哈希登记在 [`licenses/materials.json`](../../licenses/materials.json)，ZIP 内另有各文件 SHA-256。

该快照从已公开的 TAB5 材料包提取，608 个受 Git 管理的文件逐字节一致，未迁入其他项目 Git 历史。应用基线 `dbef9dc0bddb1b28ad444ca22ad11f5c998e5b1a`，MIT 许可补充后源码 `2c45bbbc9e5aef1c4e57576096bd15fd4a304d92`；镜像未改变。来源与身份见包内 `SOURCE-MANIFEST.json`。

## 构建资料

- 自有源码、工程脚本及 `source/README.md`；MIT 许可与第三方声明。
- `source/firmware/dependencies.lock` 和实际 `sdkconfig`；锁定组件由官方 ESP-IDF Component Manager 从原来源获取。
- ESP-IDF **5.4.2**，commit `f5c3654a1c2d2a01f7f67def7a0dc48e691f63c0`；`ESP-IDF-local-changes.patch` 保留实际 SDK 差异。
- `licenses/` 保留实际组件、字体与其他依赖的许可正文。

解压后阅读 `source/README.md`，准备对应官方 ESP-IDF 环境，核对 SDK commit 与补丁，再按工程说明构建 `source/firmware/`。本包省略下载的组件树、编译器和缓存，不包含应用 BIN；依赖锁定与配置不等于已经在独立环境复现相同二进制。

自有代码 MIT，第三方组件、字体、词库与标识保留原条款，见[许可范围](../TAB5-LICENSE-SCOPE.md)。开发快照不用于图形刷机窗口。

## English

The developer archive preserves 608 tracked TAB5 files byte-for-byte from the previously published materials package, original licenses, dependency locks, actual sdkconfig and SDK patch; no other project's Git history is imported. Own code is MIT licensed and third-party terms remain intact. Source/application commits are recorded above and in `SOURCE-MANIFEST.json`. Install the matching official ESP-IDF 5.4.2 environment, check its recorded commit and patch, and follow `source/README.md`. Locked dependencies are fetched from their original official sources. Downloaded dependency trees, compilers, caches and binaries are omitted; independently reproducible binaries are not claimed. End users should use factory-install or upgrade packages instead.

</details>
