# Mac TAB5 蓝牙语音延迟 · 2026-10-24

**当前结论：用户接受原 r76 的约 4–5 秒等待，进一步提速降为后续优化；优先继续偶发中断／发送失败、额度异常、日历年度更新。r77 未通过提速验收，传输实验已撤回并恢复运行 r76。**

## 当前基线与范围

用户确认停止收音后，蓝牙约 4–5 秒返回文字，Wi-Fi 约 1–2 秒；此前“45 秒”已明确更正为 4–5 秒。运行中的 r76 语音设置为 **Mac 本地识别**。这里的计时是停止到设备显示结果，不能与含说话等待的整轮跨度混用。

本机保留的一次 TAB5 收音记录为 20 块／4.0 秒音频：Mac 停止至最终识别 26 ms，最终识别至响应生成 0 ms；BLE 音频交换累计 4435 ms、后台状态／资源 3360 ms，整轮跨度 10092 ms（含说话等待）。结束请求一次 792 ms，额外结果查询零次。该记录支持优先缩短传输与后台占用，但不能从累计时间直接推算停止后的延迟。

本轮仅调整 Mac 桥接。TAB5 实机版本 0.2.264-ui 保持原样；源代码核对使用已保留的本项目 0.2.234-ui 来源包与 0.2.262-ui 变更参考，不把它们当作完整的 0.2.264-ui 源码。Windows 发送节奏核对点为 `2fb1b14c09550bce27173eb349d78187d1661ce7`。没有协议字段、固件、识别供应商、展示配置或全局系统设置变更。

## r77 尝试（已撤回传输改动）

- 已协商的 `tab5_packed` 状态／增量帧使用 zlib 默认压缩强度，替代最快压缩；保留展开大小上限和原始字节，仍只在更小时采用压缩结果。
- 已认证后台输入在分片大于 244 字节时，每 8 片保留一个 ATT 确认，末片始终确认；与 Windows 同大小分片的窗口一致。小 MTU、归属认证和语音回复保留原 5 片窗口。CoreBluetooth 背压、错误处理及签名整帧 ACK 不变。
- 语音就绪探测跟随已确认的窗口边界，保留固件语音租约；不在未完成状态帧中执行完整语音交换，不改变更新频率或删减显示字段。
- 本机内存诊断补充后台状态与资源的次数、耗时和读／写／生成分段；没有音频、识别文字、认证材料或身份字段。

## 验证与待验收

- `swift build -c release --package-path mac-app` 通过。首次构建遇到其他源文件在编译期间变更，重试成功。
- 已执行要求的 `swift test --package-path mac-app`，因当前 Command Line Tools 缺少 `XCTest` 模块而未能运行完整套件，不能记为通过。
- 使用临时断言适配器，链接实际 Debug 对象并执行 70 个现有及新增测试方法，全部通过；这不是 XCTest 运行。覆盖加密分片重组、末片确认、背压策略、语音窗口顺序／丢失／重复、压缩还原、增量基线、资源保留、计时与停止请求。适配器及输出只保存在忽略的 `artifacts/development/ble-latency-20261023/checks/`。
- 回归中发现旧测试仍要求上一轮异常留在当前异常标题下；现有产品已将其移入“此前语音”。更新断言，同时核对异常码仍保留、当前异常已清除、识别文字不进入诊断。未改变语音状态机。
- 实际停止至文字耗时的复测结果见下节；不将压缩／分片测试通过记为蓝牙延迟实机修复完成。

## 实机结果与恢复

r77 已完成应用打包、严格签名验证和 490 项隔离自检；钥匙串 helper 与 r76 字节一致。应用启动后 Wi-Fi 认证、蓝牙整帧确认与通知订阅恢复。用户随后确认文字完整，但停止至文字肯定超过 5 秒，未达到提速目的。

该次实际为 23 块／4.6 秒音频：本地停止至最终识别 36 ms、最终至响应生成 0 ms；音频收发共 8216 ms（平均约 357 ms／200 ms 音频），读 6192 ms、写 1952 ms、本机处理 30 ms；两次状态帧共 1327 ms，零资源帧。整轮跨度 11319 ms，包括说话等待；一次 stop 210 ms，零额外 poll。音频传输速度不足以跟上实时采集，支持继续调查逐块往返；不同录音及链路条件不能证明 r77 的单项改动造成全部差异。

用户进一步明确原 4–5 秒可以接受，要求先解决其他问题。本轮已正常退出 r77，恢复 r76 并核对 Wi-Fi／BLE 数据确认。压缩强度、分片窗口和探测边界源码与本轮开始时的备份逐字节一致，新增窗口 helper／测试已撤回；实验代码、差异与哈希保存在忽略目录 `artifacts/development/ble-latency-20261023/r77/`。只保留无内容的后台诊断细分及旧断言修正。没有修改固件、设备展示设置、语音提供方或全局系统设置；不再安排蓝牙反复录音。

## English

The user clarified that BLE takes approximately **4–5 seconds**, not 45 seconds, from stopping capture to seeing text; Wi-Fi takes about **1–2 seconds**. The running r76 uses Mac local recognition. A retained four-second, 20-block capture shows 26 ms from Mac stop to final recognition and 0 ms from final recognition to response generation. BLE audio exchanges occupy 4435 ms and background traffic 3360 ms within a 10092 ms span that includes speaking. One stop exchange takes 792 ms with no additional result poll. Cumulative durations are not a measurement of post-stop latency.

This Mac-only candidate uses default zlib compression for negotiated packed status/delta frames and an eight-fragment ATT barrier window for authenticated background packets larger than 244 bytes, matching the reviewed Windows window. Small MTUs, ownership and voice replies retain five-fragment barriers. Final fragments, CoreBluetooth backpressure, signed whole-frame acknowledgments, content, update cadence and firmware voice-lease probes remain intact. Additional local in-memory diagnostics expose only bounded background counts and stage durations. Firmware 0.2.264-ui, recognition provider, display settings and global system settings are unchanged. The retained 0.2.234-ui source and 0.2.262-ui overlay are references, not a complete 0.2.264-ui source tree.

The Release build passed on retry after an unrelated source changed during the initial build. Required `swift test` was attempted but cannot run because this Command Line Tools installation lacks XCTest. A temporary assertion adapter linked against actual Debug objects passed all 70 selected existing/new test methods; this is not an XCTest run. One stale diagnostic assertion now verifies the existing previous-session history, retained error code, cleared current error and transcript exclusion.

The packaged r77 passed strict signing and 490 isolated checks, with a byte-identical Keychain helper. The user then reported complete text but more than five seconds of post-stop waiting, so the speedup was not accepted. Its 23 blocks/4.6 seconds of audio occupied 8216 ms of BLE exchanges (about 357 ms per 200 ms block), with reading/writing/local processing of 6192/1952/30 ms. Two status frames took 1327 ms, with no resource frames. Local stop-to-final was 36 ms; one stop exchange took 210 ms without an additional poll. The full 11319 ms span includes speaking. Different samples/link conditions do not establish that one r77 change caused the difference.

The user accepts the original r76's 4–5 seconds for now and prioritizes intermittent capture/send failures, quota history and annual calendar updates. r77 was exited normally and r76 restored, with authenticated Wi-Fi and acknowledged BLE verified. Experimental compression/window/probe changes exactly match their pre-turn backups after reversal; the new helper/test were removed and the experiment retained only in ignored artifacts. Only bounded background diagnostics and the stale assertion correction remain. Firmware, recognition provider, device display and global settings are unchanged. Further BLE timing optimization is deferred, not declared fixed.
