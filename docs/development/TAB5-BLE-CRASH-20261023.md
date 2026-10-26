# TAB5 蓝牙 ATT 崩溃修复候选（2026-10-23）

## 现场与原因

点击新版 Windows 桥接升级后，TAB5 整屏蓝并重启，Windows 正常。USB 崩溃记录来自 .254：nimble_host，assert failed: ble_att_tx_with_conn ble_att_cmd.c:91 (rc == 0)。OTA 位于首块 READ、offset 0，尚无成功写入进度。记录未保留底层具体返回码，因此不能确定是哪次分配或传输失败。

普通控制器忙（EAGAIN）已由 L2CAP 排队并转成功；可达非零分支包括头部内存不足及 Hosted 传输失败。原代码把这些资源/传输错误当作断言条件，导致整个设备复位。“请求未确认”是稍后的超时提示，不能据此否认已经进入首块读取。

## 修改

- P4 + PSRAM + Hosted SDIO 下，HCI ACL 和命令的 CPU 临时复制改用 8-bit PSRAM；NO_ZEROCOPY 后仍复制到 SDIO 自己的 DMA 池，实际 DMA 缓冲与同步保持原样。其他目标沿用原分配策略。
- P4 Hosted 的 ATT 失败返回原始错误并记录数值，恢复本条失败请求刚设置的 busy 标志，不清除另一条在途请求。L2CAP 已消费的 mbuf 不再次释放或重发，上层原有错误处理继续运行。
- HCI flatten 在转交 Hosted 前失败，补上临时缓冲释放；Hosted 已接管后，无论接收或拒绝均由其释放，调用方不二次释放。
- 没有全局关闭 assert，没有把错误改成成功，包长度等不变量检查保留；不重放 HID、凭据或用户消息。
- scripts/apply-ble-tx-safety.py 在固件/预览构建应用补丁。先核对两个固定依赖布局再写入；未知版本拒绝，重复应用不重复修改。生成依赖不作为交付源文件。

## 验证

- 原 .254 实际 ATT 函数故障注入重现 rc == 0 断言，exit 3。仅测试进程设置 stderr/abort 行为以避免系统崩溃对话框。
- 实际函数测试通过精确错误码、失败/排队请求恢复、通知/回复失败不误清在途请求。
- HCI 两个目标分配分支、最大 ACL/命令内容、分配/flatten/transport 拒绝、唯一释放及 512 次循环均通过。
- 六个源码文件补丁 check/apply 与重放内容核对通过，patch SHA-256：a5ab74e993eae0f795652f2c975c1ce5963e3d51eb5439661587dc09395543b6。
- 最终固件重新编译实际 ble_att_cmd.c 与 vhci_drv.c；ELF 含新错误处理字符串。
- 最终 12 项相关 native 回归及完整 LVGL 回归通过，含 BLE mailbox/owner、RPC admission/exchange、照片分块、OTA fault matrix、运行诊断和相机 viewport。实际 sidecar 预览生成，图片核验单独记录。
- 实际安装及反馈见末节：首次 BLE 图片上传通过，蓝闪仍失败；原 BLE OTA 崩溃及传输中停止待补测。未读取用户照片。

## 候选与安装

版本沿用尚未刷入的 0.2.255-ui，新镜像替代早先 84754d25afca 候选，旧包保留在历史归档。

- 固件 SHA-256：f7e32ff3da799338017b039ca0565ab5249b0bd8d3e649e2add324ce4081f48d
- ELF SHA-256：f9851a2e9b9098f479a58cd6ed04c288e6033e4a805087c8e3d31257ecabc816
- 大小 7,176,384 字节，原槽剩余 32,576 字节，分区不变。
- 固定入口：artifacts/firmware/tab5/latest/aibot_tab5.bin。
- 配套 Windows DLL 51c2e3641716f22ff626d701e873e97d5a6e465a566257775411b0a9dae61a1d 已安装，包含停止升级；本轮无需重装 Windows。
- 已安装并核验精确 .255：ota_0、VALID、ELF 完全匹配，心跳递增。此次实际走 Wi-Fi 压缩传输，USB 保持连接；并非 USB 固件传输。
- 保留绑定、配对、Wi-Fi、显示设置及三张照片附件。不提交、不推送、不发布。

官方知识检索未找到该断言的针对说明；向 M5Stack 的反馈被自动审批拒绝，未发送且没有 feedback_id。修复依据是本地依赖原码、现场记录及实际函数测试。

## English

The .254 device crashed in nimble_host on an unconditional ATT TX success assertion during the first OTA read. The precise lower-layer return code was not captured. EAGAIN is already queued by L2CAP; resource and Hosted transport failures are legitimate nonzero results.

The P4 Hosted patch returns the original error and restores only the failed request's busy flag, preserving consumed-mbuf ownership. CPU-only HCI staging moves to PSRAM while SDIO retains its independent DMA buffers; a pre-handoff flatten failure now frees staging exactly once. Other targets retain their allocation and assertion policy. Assertions and authentication were not globally disabled. User commands and credentials are not replayed.

The original production function reproduces the assertion (exit 3). Real-function fault tests cover exact errors, queue recovery, outstanding request isolation, maximum packets, allocation/copy/transport failure, ownership and 512 cycles for both target branches. Source replay, firmware build, 12 final related native regressions and full LVGL preview passed. The exact updated .255 image is f7e32ff3da799338017b039ca0565ab5249b0bd8d3e649e2add324ce4081f48d, ELF f9851a2e9b9098f479a58cd6ed04c288e6033e4a805087c8e3d31257ecabc816, with 7,176,384 bytes and 32,576 bytes free. It supersedes the earlier unflashed .255. Exact installation is verified on ota_0/VALID with advancing heartbeat. Actual firmware transfer used Wi-Fi zlib, with USB still connected. First BLE photo acceptance passed; blue flashing failed. Active-transfer cancellation and the original BLE OTA crash scenario still require physical regression. The installed Windows bridge is unchanged. No commit, push or release occurred. External feedback was rejected and not sent.


## 安装及用户反馈（本轮最终结果）

- 精确 .255 启动通过：ota_0 / VALID；ELF f9851a2e9b9098f479a58cd6ed04c288e6033e4a805087c8e3d31257ecabc816。运行毫秒 8,845 → 63,729 → 166,911 → 236,149，重启原因 3（升级后软件重启）。三张资源 ready/persisted 均为 3。
- 实际固件传输为 Wi-Fi 分块压缩，4,910,296 字节、10.391 秒；USB 用于状态和核验，不能称 USB 固件传输通过。
- 用户在仅蓝牙模式下报告首次拍照添加成功、耗时正常；设备明确记录 BLE 104,532 字节、3.463 秒、HTTP 200，首次失败及恢复次数均为 0；桥接接收 1.484 秒。USB 仍在线，因此仅将图片通道验收为 BLE，不把诊断称为无线采样。
- 用户确认照片仍蓝闪：此项未修复，不因显示欠载计数为零更改判定。
- 实际停止按钮测试到达时已经传完，按钮禁用并提示最终校验不可停止；没有中断本次升级。仅最终阶段保护通过，传输中停止/确认/重试仍待验收。
- 当前版本与提供版本相同，正式开始升级防护禁用；不为补测绕过该防护。原来 BLE OTA 首块崩溃场景尚未再次真机覆盖，不能用首次 BLE 图片成功替代它。
