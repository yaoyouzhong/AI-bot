# TAB5 0.2.264-ui source overlay

在已重建的 .257 工程上，先应用 [tab5-262-source](../tab5-262-source/README.md)，再按相同相对路径覆盖本目录的 `firmware/` 与 `tests/`。本目录是增量完整文件覆盖包，不是独立工程。使用 `-LocalArt -LocalVersion 0.2.264-ui` 构建；项目及第三方许可继续适用。

此处保存 .263/.264 的 OTA 分段耗时、启动后保留的 RTC 诊断和测试。包含实际 .264 配置；未包含 .265/.266 I/O 探针及任务运行时间统计。保留 .262 照片修复。源码文件字节、应用与 ELF 身份见 `SOURCE.json`，实机结果见 [验收记录](../TAB5-OTA-264-ACCEPTANCE.md)。归档不重新刷机，也不宣称独立逐位重建。

Apply this incremental full-file overlay after `tab5-262-source` on the reconstructed .257 project. Preserve relative paths and build with local version 0.2.264-ui. It adds the .263/.264 elapsed-time diagnostics and retained RTC snapshot, preserving the .262 photo fix. The pinned configuration excludes .265/.266 probes and task runtime statistics. Existing licenses apply. See `SOURCE.json` for exact file/image identities and the linked acceptance record for hardware evidence. This archive is not a standalone project and does not claim independent bit-identical rebuilding.
