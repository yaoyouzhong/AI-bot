# TAB5 Flash 写入测量 / Flash write measurement

## 目标和结论边界

0.2.108-ui 安装和精确启动核验通过，但点击存储测速触发蓝屏，测速真机验收失败。0.2.109-ui 修复候选将测速任务从外部 PSRAM 栈改为内部栈，与现有 OTA 写入任务一致，并在 Flash 调用前核对栈位于内部 DRAM。崩溃记录定位到 `tab5_flash_benc` / `panic_abort`，栈为 `0x49ad1104–0x49ad4100`；具体断言文字未保留，不能仅据此断言唯一根因已确定。后续 .109 结果见下文。

128 KiB 微测试比较分块开销，不能单独证明 Flash 的持续写入上限。结合真实约 6.86 MB OTA 的纯写入阶段和实际 JEDEC 芯片型号判断瓶颈；网络发送、擦除、写入、最终校验及重启分开解释，并行阶段不相加。

0.2.109-ui 已安装并通过启动核验；本轮测速返回 `state=3,error=259,modified=0`，四组计时全部为零，未进入擦写。该版把读取失败与非空白区域合并为同一错误，不能据此认定尾区一定有数据。0.2.110-ui 候选保留读取原始错误，并在已校验镜像及 4 KiB 保护间隔之后，按 64 KiB 对齐逐块寻找完整的 128 KiB 全空白范围；未找到则返回 `ESP_ERR_NOT_FOUND`，绝不清除既有内容来腾出测试区。新增数值 `phase` / `scanned` 帮助辨别退出阶段。2026-10-05 真机已完成本轮测试，具体范围与结果如下。

现有五组完整 Wi-Fi 升级已经过精确镜像启动核验：

| 安装版本 | 镜像字节 | 分区准备 ms | 纯写入 ms | 校验 ms | 安装 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| 0.2.106-ui | 6,852,384 | 7,905 | 9,621 | 879 | 18,993 |
| 0.2.107-ui | 6,852,688 | 7,802 | 9,900 | 891 | 19,167 |
| 0.2.108-ui | 6,859,136 | 7,900 | 9,984 | 904 | 19,409 |
| 0.2.109-ui | 6,859,264 | 7,810 | 9,914 | 899 | 19,181 |
| 0.2.110-ui | 6,859,616 | 7,892 | 9,980 | 922 | 19,397 |

五组纯写入约 0.66–0.68 MiB/s。这是当前 OTA 软件路径的表现，尚不是芯片上限。五次安装的镜像略有差异，是可比较的实际升级样本，不冒充相同镜像、完全受控条件下的重复实验。本轮有效微测试结果见下文；.109 已读回实际 JEDEC ID `0x464018`、驱动页大小 256 字节，具体型号及手册参数仍需核实。

## 2026-10-05 真机结果

0.2.110-ui 精确 ELF 指纹、ota_0 VALID 和用户测试完成均已核验。读回 `state=2,error=0,restored=1,modified=1,phase=3,scanned=1`：在备用 ota_1 的相对偏移 `0x6c0000` 找到第一块空白区；测试区已恢复、备用镜像重新校验通过，运行/启动分区未变。

| 写入块 | 三轮纯写入 ms | 中位速度 KiB/s | 擦除中位数 ms |
| --- | --- | ---: | ---: |
| 8 KiB | 175.832 / 173.507 / 173.257 | 737.72 | 158.855 |
| 16 KiB | 174.096 / 178.145 / 173.504 | 735.23 | 158.811 |
| 48 KiB | 内部缓冲可用性检查未通过，跳过 | — | — |
| 64 KiB | 内部缓冲可用性检查未通过，跳过 | — | — |

首轮擦除为 20.118 ms，明显低于后两轮 158.855 / 159.584 ms，故展示中位数，不把首次全空白区的擦除时间外推为持续速度。8/16 KiB 两档相差约 0.34%，未观察到增大到 16 KiB 的提速。大缓冲跳过不等于测速成功或失败，更不能为获得成绩降低内存保护预算。

本次整包纯写入 9.980 秒，约 671.2 KiB/s；小样本约 0.72 MiB/s。将小样本速度机械外推至整包约需 9.08–9.11 秒，仅为量级比较，不是可兑现的优化收益或芯片上限。128 KiB 样本、两档缓冲和一次启动内的测试尚不能证明绝对硬件上限。没有触发新的刷机或重复测速。

原始证据保存在本地 `artifacts/development/tab5-flash-benchmark/hardware-110-test-result.json`。电脑策略仍为 auto、循环开启、15 秒及原页面顺序；验收后退出设备测试页继续日常使用。

## .118 后复核：瓶颈不等于硬件极限

2026-10-05 只读复核已有证据和 ESP-IDF 5.4.2 实现，未重新擦写测试。当前 OTA 已使用内部内存暂存、SDK 8 KiB 写入分块、允许对齐块擦除（未启用 `CONFIG_SPI_FLASH_BYPASS_BLOCK_ERASE`），且接收与写入并行；不能将这些已有优化列为新收益。

.118 的完整镜像为 6,940,192 字节，擦除/准备 8.037 秒、写入 9.893 秒、等待接收数据 0.025 秒、安装至校验完成 19.800 秒。结合上面的多轮整包与 8/16 KiB 微测试，只能判断常规缓冲调整已接近本实现的性能平台，不能证明芯片绝对极限。48/64 KiB 测试此前跳过，芯片 ID `0x464018` 对应确切型号与厂商时序尚未证实；通用 SPI 时钟或读取带宽不能代替页编程/擦除时序。

官方文档说明 `esp_ota_begin` 按给定镜像大小擦除所需范围，并提供顺序增量擦除选项；增量擦除本身并不消除实际擦除工作。当前网络等待接近零，因此单纯改变擦除与接收的重叠，不能承诺明显降低总耗时。后续若追求较大收益，应评估缩小实际安装镜像；压缩传输包解压后仍写入完整镜像，不能等同于减少 Flash 工作量。此次仅分析，不修改擦写参数或恢复/校验保护。

References: [ESP-IDF 5.4.2 OTA](https://docs.espressif.com/projects/esp-idf/en/v5.4.2/esp32p4/api-reference/system/ota.html), [SPI Flash API](https://docs.espressif.com/projects/esp-idf/en/v5.4.2/esp32p4/api-reference/peripherals/spi_flash/index.html). See also `TAB5-CLOSEOUT-118.md` for the exact installed image and retained timing.

Read-only review after .118: existing internal staging, SDK write chunks, block erase and overlapping reception are already in place. Measured Flash preparation and writing dominate; similar small-buffer results indicate a plateau in the current software path, not a proven silicon limit. Exact chip timing remains unverified, larger-buffer samples were skipped, and this review changes no flash settings or boot protections.

## 小样本测试

- 在 TAB5 的固件升级页进入“存储测速”，阅读范围后按“开始测试”；不提供远程自动写入命令。
- 仅使用备用 OTA 分区镜像之外的 128 KiB 空白区。先要求当前启动镜像为 VALID、启动分区未待切换、备用镜像校验通过；从分区尾部按 64 KiB 对齐向前查找，完整镜像末端至少距测试区 4 KiB。通过内部 4 KiB 缓冲读取，备份并要求整个测试区为 `0xFF`，不清除非空白内容。任何检查失败都不擦除或写入。
- 从内部内存取 8、16、48、64 KiB 缓冲，各测三轮；分配后保留至少 96 KiB 内部空闲预算，不足的规格明确跳过。实际分配失败也跳过。
- 每轮擦除及 `esp_partition_write` 调用分别计时。确定性数据生成、读取比对、轮次间等待不算入纯写入时间；芯片驱动锁和 API 内部等待仍包含在内。
- 成功、失败、取消都先恢复测试区并回读比较，重新校验备用镜像的长度及摘要，确认运行/启动分区未变。恢复最多尝试三次；未确认恢复不能显示成功。突然断电最多留下这块空白尾区的测试内容，不覆盖已校验镜像。
- 不写配对、NVS、assets 或启动选择。只读 JEDEC ID 与实际驱动 page size；不从启用的芯片驱动推断型号。

每行显示三轮写入、擦除的中位数。完整数值由电脑“核验启动（USB）”读取，结果只在本次启动内保存；测试完成后先读结果再重启。停止按钮表示请求停止，返回按钮在恢复完成前禁用。

## 真机步骤和完成证据

1. 从资源管理器双击既有“TAB5 固件升级”，安装配套桥接并提供固定 latest 候选。
2. 在设备确认升级修复候选 0.2.110-ui，电脑核验精确版本、ELF 指纹、OTA 分区及 VALID 状态。不要再运行 .108 的存储测试。
3. 在设备手动开始存储测速；结束后再次点击电脑“核验启动（USB）”，采集 `flashBenchmark` 的三轮原始计时、实际 JEDEC ID、跳过项及恢复确认。
4. 对比微测试中位数与整包纯写入速度，并核对该芯片官方数据手册。结果相近只说明当前软件路径接近所测性能，不能证明绝对硬件上限。
5. 完成后退出测试页，恢复原自动模式、轮播页面顺序及间隔；记录崩溃/显示诊断及用户观察。构建与故障模拟不能代替上述真机证据。

## English

The .109 image now passed exact startup verification. Its test stopped before mutation (`state=3,error=259,modified=0`, all timings zero); that version conflated a read error with a nonblank tail. The .110 candidate preserves read errors and searches 64 KiB-aligned windows beyond the verified image plus a 4 KiB guard for a complete all-FF 128 KiB sample. No suitable range returns `ESP_ERR_NOT_FOUND`; existing bytes are never erased to make space. Reads use an internal 4 KiB buffer. Optional numeric `phase` and `scanned` identify the failure stage. On 2026-10-05, .110 passed exact startup verification and completed the manual test with state=2, error=0 and restored=1. The first scanned window was blank; the spare image and unchanged boot selection were reverified. Three write rounds with 8 KiB buffers were 175.832/173.507/173.257 ms (median 737.72 KiB/s); 16 KiB results were 174.096/178.145/173.504 ms (735.23 KiB/s). The 48/64 KiB variants were skipped by internal-buffer availability checks, not measured. Erase medians were 158.855/158.811 ms; the initial 20.118 ms erase is not representative of subsequent rounds. The fifth full-image sample measured preparation 7.892s, pure writing 9.980s, verification 0.922s and installation 19.397s. Small-sample extrapolation gives 9.08–9.11s pure writing for this image, a scale comparison only, not a promised gain. Actual JEDEC ID is now `0x464018`, driver page size 256 bytes; exact part/datasheet parameters remain to be verified. The fourth full-image sample (.109) measured preparation 7.810s, pure writing 9.914s, verification 0.899s and installation 19.181s; phases overlap and must not be added.

The .108 image installed and passed exact startup verification, but its manual storage test panicked on hardware. The crash names `tab5_flash_benc`, `panic_abort` and an external PSRAM stack; assertion text was unavailable, so the unique root cause is not proven. The .109 repair candidate uses an internal task stack as the existing OTA writer does and rejects a non-DRAM stack before Flash calls; its subsequent pre-write failure is described above. Five verified full-image upgrades measured roughly 0.66–0.68 MiB/s in the pure-write phase; this is current OTA-path performance, not a proven silicon maximum. Images differ slightly. The completed microbenchmark covers two buffer sizes only and does not establish the absolute hardware limit; actual JEDEC readback is listed above.

Only the all-FF tail outside a verified spare image is measured, with an additional 4 KiB gap. Require a VALID current image, unchanged boot selection and valid partition geometry before modification. Back up the region; measure three rounds each with internal 8/16/48/64 KiB buffers, retaining a 96 KiB free-memory budget and explicitly skipping unavailable sizes. Separate erase and write API time from pattern generation/readback/yields. Restore and compare the tail, reverify the spare image and confirm running/boot partitions before reporting completion, including on cancellation or partial failure. Restore attempts are bounded at three. No pairing/configuration/assets/boot-selection writes or remote start command are added.

Results are numeric, current-boot-only and collected through the existing USB startup verification after an explicit on-device test. Use medians and full-image timing together; consult the actual chip's official datasheet before discussing limits. Build/mock tests are not hardware acceptance. Restore the original automatic display cycle at deployment closure.
