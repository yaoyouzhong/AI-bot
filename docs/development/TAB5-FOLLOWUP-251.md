# TAB5 .251 本地候选 / Local candidate

## 现场结论

- `.250` 已核验精确镜像启动；用户确认日程超长语音截断回填和提示布局正常。
- USB 拔出时，Windows 已取得 `.250` 的实时 Wi-Fi 诊断；仅蓝牙模式下，后台 Wi-Fi 轮询可能让桥接先选错诊断通道。
- 用户明确描述添加照片时“照片画面会蓝闪几下”，照片尚未退回会话页。显示欠载、背光变更、刷新错误等旧计数没有相应变化；这些零值不能替代可见现象，也不能排除带宽争用。

## 照片修复候选

官方 [ESP-FAQ](https://docs.espressif.com/projects/esp-faq/en/latest/software-framework/peripherals/lcd.html) 将 MIPI-DSI 蓝闪与 PSRAM 供数不足联系起来。现场代码已使用 200 MHz PSRAM、RGB565、XIP、256 KiB L2、128 字节缓存行和性能编译选项。JPEG DMA2D 与屏幕扫描 DW-GDMA 仍竞争系统内存通路；上一版仅排除了 LVGL 拷贝并发。

新代码在取得停止后的照片引用、排空显示拷贝之后，临时提高 DW-GDMA 内存 master 1 的系统仲裁及读 QoS 优先级，覆盖缩略图、JPEG 输出分配、编码和收缩。SDK 的 `DW_GDMA_LL_MASTER_PORT_MEMORY==1` 编译断言固定该映射。只改这两个字段，结束或任一失败路径恢复进入前的原值，不改其他主设备或写优先级。现有照片尺寸、quality 85、屏幕时钟、帧缓冲和三张附件能力不变。

这是针对新蓝闪证据的带宽修复候选；尚无真机结果证明因果或症状消失。`displayDiag` 追加版本化数字后缀，记录启用次数、恢复次数和实际读回优先级，不读取照片内容。

## Windows 额度及诊断

- 日统计接续前次有效周额度采样；空周额度样本只能在身份和套餐一致时跨过。
- 可核实的长跨日差额归入恢复采集当天，标明“含跨日补记”，不进入完整日均；未采集日期仍为未知。
- 到期或有明确窗口依据的手动重置分段，只计新段实际观察值；未知回落、身份变更或无法核实的窗口不臆造消耗。
- 现场边界 35→61→65 的同账户同窗口夹具得到 30 个百分点，其中 26 为跨日补记；安装后仍须核验实际历史的身份及窗口条件。原历史文件不改写、不迁移。
- 无线诊断首通道等候八秒超时后，最多再尝试同一绑定设备/电脑的另一在线无线通道，使用新请求 ID 和有效期；迟到旧回复拒绝。仅本机诊断 HTTP 响应预算为 20 秒。Mac 未改。

## 验证与交付边界

证据目录：`artifacts/development/tab5-followup-251-20261022/`。

- 固件构建成功，镜像 7,173,952 字节，应用槽剩余 35,008 字节；未改分区。已有两个无用符号警告保留。构建中既有 ROM ELF 环境提示不影响镜像链接和尺寸校验，未将其作为成功诊断证据。
- 实际寄存器结构的 128 次优先级租约测试通过，保留非零旧值和其他字段；真实照片函数的分配、编码、超时、结果异常、收缩失败与重试路径均释放保护。
- 相机 128 次模拟拍摄/重开、72 次视口会话、方向合成、退出清理、JPEG 缓存范围和 DMA 分配、上传分块、无线固件回归通过。
- Windows Release 构建零错误/警告；额度历史和无线诊断 57 项检查通过。
- `--status-once` 被原有桌面资料目录保护拒绝；未绕过，不能记作通过。已安装 `.250` 的正常桌面运行和本机诊断证据保留，新桥接现场运行待安装。
- 完整界面预览通过；跨日标记可见且不遮挡。实际 sidecar 预览及最终哈希见 `PREPARED-251.json`。
- 以下部署记录更新候选阶段的安装状态；真机蓝闪、真实额度重算、仅蓝牙诊断回退仍分别验收。保留设置、绑定、配对、草稿和附件。不提交、不推送、不公开发布。

## 2026-10-22 部署核验

- 用户明确授权安装对应桥接并刷入 `.251`，随后确认继续。批准镜像 SHA-256 为 `a216616b227faf8682ead9a192c889bf3812855bd293612eafeed8bca45e4b2c`，桥接 DLL 为 `9dc7b53df0026cf302306288fb4fee622d6a8fb7ffe13a9fb98d28b5b0024b1d`。
- 正常退出后替换固定运行目录的五个文件；旧文件已备份并逐个复核哈希。用户正常桌面启动成功，原资料目录、配对文件和 43,664 条以上额度历史均保留。
- 用户通过升级界面完成安装，实际传输为 Wi-Fi 分块压缩，7,173,952 字节镜像压缩传输 4,906,486 字节。首次启动核验返回 IOException；设备启动稳定后再次核验通过，精确 ELF `813446b721faa2819833436e1135cca7f2e95f63f9c6d91170b900b1da9212f9`、`ota_0`、`VALID` 和三次递增心跳均通过正式核验器。证据为 `live-251-boot-verified.txt`。
- 新桥接的实际历史仍将 10 月 21 日计为 4 个百分点、跨日补记为 0，尚未达到历史差额验收。工具可见的历史文件停在 10 月 5 日，不能作为当前桌面样本证据；未修改它，也未将夹具中的 30 当成现场结果。账户与窗口边界仍待取得可核验的实时明细。
- 用户验证后确认：仍蓝闪，附件增删正常；点开附件照片只在屏幕中央小幅显示。新鲜诊断中的优先级启用/恢复均为 3，显示错误仍为 0，不能推翻可见故障。证据为 `live-251-blue-flash-failed.txt`。仅蓝牙诊断回退仍未进行现场验收。

## English

The user accepted .250 agenda truncation/layout, and live Wi-Fi diagnostics worked with USB disconnected. Photo addition still produces full-screen blue flashes while the captured image remains visible. Existing zero counters do not override this observation. The official ESP-FAQ associates DSI blue flashes with PSRAM bandwidth pressure; this device already uses the recommended memory/cache options.

The candidate temporarily prioritizes the LCD's DW-GDMA memory-master reads across still preparation, encoding and output compaction, restoring exactly the prior arbitration/read-QoS fields on success or failure. It preserves panel clocks, image dimensions/quality, buffers and three attachments. Numeric readback counters expose lease activation/restoration; they are not physical display acceptance.

Windows reconnects valid cumulative quota samples across midnight, labels long-gap carry-over, segments verified resets and excludes partial days from complete-day averages. Missing dates remain unknown; uncertain identity/windows do not manufacture consumption. The 35→61→65 fixture yields 30 points including 26 carried, pending verification against installed actual history. Wireless diagnostics make at most two bounded probes on live paths to the same bound host/device with distinct challenges. Mac remains unchanged.

Build, native camera/lifecycle and UI regressions passed. Windows build and synthetic quota/57 wireless checks passed. The CLI status check was rejected by the existing profile guard and was not bypassed. Exact candidate hashes and preview evidence are recorded separately.

On October 22 the user authorized and completed deployment. The five fixed-path bridge files match the approved hashes and were backed up before replacement; normal desktop startup retained the original profile and history. The device received the approved image through compressed Wi-Fi OTA. After an initial verification IOException during startup, the official verifier confirmed the exact ELF, ota_0, VALID and three advancing heartbeats. The user subsequently confirmed blue flashes persist, attachment add/delete works, and the attachment viewer is too small. Fresh diagnostics show three priority acquisitions/restorations and zero display errors; these do not override the visible fault. Forced-BLE diagnostic fallback remains pending. Actual October 21 usage is still 4 points with zero carry-over; the synthetic 30-point case is not an actual-history result. The tool-visible history file is stale and was not modified. No commit, push or public release occurred.
