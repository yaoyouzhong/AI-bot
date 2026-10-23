# TAB5 .264 development acceptance / 开发验收

## Identity / 身份

- Version: `0.2.264-ui`; application: 7,186,768 bytes.
- Application SHA-256: `0c9f5d7ae3228652d29c69bd75a2937134582a8ad87da981215ff3f992022561`.
- ELF SHA-256: `2f23aa91cea2957c7ff0b2d85b217859f2adf221d30b7a5bd895c4e816643585`.
- Source: [incremental overlay and manifest](tab5-264-source/README.md); apply after .262.

## Verified behavior / 已验证行为

实际 Wi-Fi 升级成功，精确 ELF、`ota_1`、`VALID`、错误码 0 已核验，用户确认 .264 升级成功但较慢。启动约 12 秒时的首次确认未满足全部条件，后续原条件核验通过；未放宽检查。USB 仅用于采样，保留用户展示设置。

Exact .264 Wi-Fi installation and boot verification passed: matching ELF, `ota_1`, `VALID`, error 0. The user confirmed success and slow installation. The first check at about 12 seconds uptime was incomplete; later verification passed the original conditions. USB was used for sampling; display settings were preserved.

| Stage / 阶段 | Time / 耗时 |
| --- | ---: |
| Wi-Fi transfer / 传输 | 16,219 ms |
| Prepare / 准备 | 8,424 ms |
| Write / 写入 | 44,103 ms |
| Verify / 验证 | 6,429 ms |
| Installer total / 安装总计 | 61,327 ms |

传输与安装是分别记录的阶段，不将两者合计冒充单一计时。运行安装器为 .263。`esp_ota_write` 共 1,755 次、7,186,768 字节，累计经过时间 44,001,688 微秒；复制仅 73,572 微秒。API 经过时间包含调度、锁等待，不能解释成纯 CPU 时间。两种目标分区均出现慢写，尚未定位根因。

Transfer and installer durations are measured separately. The installer ran on .263. `esp_ota_write` handled 7,186,768 bytes in 1,755 calls over 44,001,688 microseconds; copy time was 73,572 microseconds. These elapsed times include scheduling/lock waits and are not CPU time. Slow writing occurred on both target partitions; the root cause remains unresolved.

## Validation scope / 验证范围

此前 .263/.264 完整构建和镜像校验通过；两者可执行段逐字节相同，仅版本及元数据不同。13 项原生回归覆盖 OTA 性能格式、RTC 留存、USB 诊断边界、OTA 恢复/流/压缩/范围/工作线程、无线带宽策略、照片显示诊断、相机视口、显示冻结和照片编码。既有 RTC 字段地址及 .262 照片修复源码保持一致。以上是候选与实机阶段证据，不表示本次归档重新刷机或独立逐位重建。

Previous complete .263/.264 builds and image checks passed. Executable sections were byte-identical, with different embedded versions/metadata. Thirteen native regressions covered OTA diagnostics/retention/framing/recovery/stream/compression/ranges/workers, radio policy and photo/display/camera behavior. Existing RTC field addresses and .262 photo-fix sources were preserved. These are prior candidate/hardware results, not a claim of a new flash or independent bit-identical rebuilding during archival.

## Deferred issue / 后续事项

用户于 2026-10-24 明确同意 OTA 变慢后续解决，因此该性能问题不作为本次合并阻断项。此次只同步 .264；.265/.266 仍为本地未刷入诊断候选。多主机、解锁、停止升级等整个开发分支的其他功能，应按各自验收记录及合并检查判断，不能用本次 .264 启动成功代替。

On 2026-10-24 the user accepted deferring OTA performance, so it is not a merge blocker for this synchronization. .265/.266 remain local unflashed diagnostic candidates. Other branch features (multi-host, unlock and stopping upgrades) retain their own acceptance requirements; this .264 boot result does not establish those outcomes.

## 2026-10-24 archive checks / 本次归档检查

本次从归档覆盖文件重建独立测试目录，重新编译并运行 `ota-performance-test`、`ota-performance-trace-test`、`ota-usb-diagnostic-test`、`ota-update-test`，四项通过。15 个源码/配置文件及 Git 暂存字节逐项匹配清单，原 .262 归档未改，固定 latest 仍为上述精确 .264。未重新构建或刷入固件。

An isolated test tree was reconstructed with the archived overlays. All four named native tests were rebuilt and passed. All 15 source/configuration files and staged Git blobs matched the manifest; the .262 archive was unchanged, and fixed latest still matched the exact .264 image above. No firmware rebuild or hardware write occurred.
