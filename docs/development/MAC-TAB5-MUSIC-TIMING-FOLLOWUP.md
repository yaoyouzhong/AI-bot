# TAB5 音乐计时的固件后续项 / Firmware follow-up for music timing

本项来自 2026-10-20 Mac 联调，不代表已修改或刷入固件。本轮授权修改范围为 Mac 桥接，实际 OTA、首刷、恢复 Flash 均需另行明确授权。

## 现场与已完成的 Mac 修复

- `c51a1bb`：实体曲名／封面、暂停／播放动作生效，但用户报告延迟、跳秒及回退。
- `754fd02`：保留小数采样、按发送时刻投影、更新控制读回并绑定当前封面；用户进一步明确只向前跳秒，没有往回走。
- `3c82bcc`：USB 发送计划不再累计 ACK 耗时。用户确认不跳秒了，但时间前进仍不均匀。因此只记录改善，音乐计时整体仍未通过。

`3c82bcc` 的 Mac CI 与完整附件已核验 136 项测试、0 失败／跳过。这些测试不构成固件计时或均匀度实机验收。

## 已核对的设备逻辑

参考为实际运行的 `0.2.234-ui` 对应官方源码 ZIP，SHA-256：

`c107e5f929e757a6342bea659f84e726a40d5f16b692441865f348fa26290890`

归档中的 `source/firmware/main/ui.c`，`clock_music` 第 2155–2158 行，先将 `music.elapsedSeconds` 转为整数，再加上收包后经过的整数秒。每次采用新状态都会更换 `last_received`。小数相位被丢弃，传输／调度波动因此会改变相邻秒的停留时间。

同一归档的 `source/firmware/main/ui_time.h` 已有普通时钟使用的 `ui_time_value`：在容许的心跳抖动内保持原本地单调时钟相位，而不是每包重设。它目前没有用于音乐进度，且不能不加生命周期处理就直接套用：音乐可能从零开始、暂停、切歌和跳转。

## 代码回放证据与边界

在忽略的本机目录使用原创合成采样，分别回放实际音乐整秒表达式和归档中的实际 `ui_time_value`。固定两秒发送、50 ms 渲染周期，结果如下：

| 合成条件 | 当前音乐表达式的每秒停留 | 连续时钟辅助函数的每秒停留 |
| --- | --- | --- |
| 固定传输耗时 | 1000 ms | 1000 ms |
| 60–180 ms 的有界传输耗时变化 | 900–1100 ms | 1000 ms |
| 同时加入有界采样波动 | 900–1100 ms | 1000 ms |

上述回放均没有回退或跳过整数秒，仍可出现不均匀停留，与本轮剩余现象相符。数字来自合成回放，不能称为实体 TAB5 的实测间隔，也不能直接证明未来固件修复通过。没有连接设备、创建固件镜像或写入 Flash。

另对本机地址查询做了 12 次只读汇总预检，原路径最大约 39 ms；没有据此将它认定为剩余计时问题的主因，也没有改变地址选择策略。未输出地址或网络配置。

## 对应固件工程的修正要求

1. 音乐锚点保留毫秒与本地单调时间，仅在最终显示时取整数秒。
2. 同一首歌持续播放时，容忍有界心跳抖动并保持连续相位；不能每次收包重新开始一秒。
3. 明确处理零进度、新曲、暂停、恢复和真实跳转；不能用只增不减掩盖真实回退跳转。
4. 尊重 `timelineAvailable=false`、失联与过期样本，未知时钟不继续伪造；曲名与有效封面按既有缓存约定保留。
5. 保持现有 `music` 字段、v1 协议以及 Windows／Mac 兼容性，不要求所有桥接一起升级。

验证必须包括有界／变化的传输延迟、重复状态、暂停／恢复、零位置、前后跳转、切歌、失联／恢复及曲末边界。随后分别做 Windows／Mac 与 USB／Wi-Fi／BLE 的实际画面观察。真实写入前仍需独立授权，写入后另核对精确版本、ELF、VALID 分区、实时 UI 样本及屏幕触摸；本文件不授权写入。

## English

On `3c82bcc`, the user reported that skipped seconds were gone but second dwell remained uneven. Music timing is improved, not fully accepted. Mac CI and complete artifacts verified 136 tests with zero failures/skips; these are not firmware timing measurements.

The official running `.234` source archive is bound by the SHA above. Its `ui.c` music renderer truncates the sampled position and time since receipt separately, replacing the receipt anchor for every new status. This loses fractional phase. The existing `ui_time.h` wall-clock helper preserves a monotonic local phase within heartbeat jitter, but music does not use it. Music-specific zero-position, pause/resume, track and seek lifecycle handling is required before reusing that approach.

An isolated replay used original synthetic samples, the published music expression and the actual wall-clock helper. With a two-second sender and 50 ms renderer, constant transport produced 1000 ms dwell in both. Bounded transport/sample jitter produced 900–1100 ms dwell in current music and 1000 ms with the steady helper, without skipped/backwards seconds. These are synthetic code-replay results, not device measurements or acceptance of a future patch. No device was accessed and no firmware was built or written. A separate twelve-probe address-resolution timing aggregate did not establish that lookup as the main cause; address selection was unchanged and no addresses were printed.

The firmware follow-up must retain millisecond anchors and monotonic time, keep phase during continuous playback, and correctly reset for zero/new track/pause/resume/real seeks. Unknown or stale timelines must not invent progress. Keep existing v1 music fields and Windows/Mac compatibility. Regress delay variation, duplicates, lifecycle, genuine forward/backward seeks, disconnect and end-of-track, then physically verify both hosts and all three transports. Firmware changes/writes remain outside this Mac-only implementation scope and require separate authorization; this document grants none.
