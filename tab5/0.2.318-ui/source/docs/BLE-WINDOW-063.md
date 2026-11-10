# 0.2.63：蓝牙公共邮箱窗口传输

## 基线与目标

0.2.62 真机已恢复照片添加：用户确认成功；桥接读取 `photoDiag=7,0,...,97559`、`photoUploadDiag=BLE,97559,104244,200`，有效端到端速度约 0.91 KiB/s。桥接首片至收齐 100109 ms；设备端 104244 ms 覆盖更多前后处理，以设备端为本轮基线。BLE 实际 MTU 517，不能再用增大 MTU 作为已完成提速的证明。记录位于相邻 AI-bot `artifacts/acceptance-20260930/photo-062-upload-success.txt`。

此次优化设备→电脑图片/RPC/语音公共邮箱及电脑→设备回复与状态流。Wi-Fi/USB 和其 OTA/语音性能矩阵继续待验；仅蓝牙不跨通道，照片分辨率/画质不变。照片添加成功不等于含照片消息已发送。

## 向后兼容扩展

两个现有 GATT 特征（语音 0004、RPC 0005）新增 Notify 属性。新桥接仅在 Notify 属性存在且 CCCD 订阅成功时启用通知；否则使用原 read + cursor ACK。旧桥接继续读取相同特征，无需改请求加密、HMAC、会话、nonce、期限或幂等判断。

新 grant 命令为 8 字节，小端：`type=3:u8 | requestId:u32 | nextOffset:u16 | credits:u8`，credits 仅 1–4。首片仍通过 read 获取；grant 的 ID/游标必须满足原邮箱校验，然后设备发送最多 credits 个通知。每个通知保留 `id:u32 | offset:u16 | total:u16 | bytes`，总长不超过 `min(MTU-3,488)`。每片成功入队后才推进下一片，窗口末尾由下一次 grant 确认。读取完成后直接返回原加密响应。

该操作有界，不在 NimBLE 回调等待或循环重试。通知排队失败、读到另一请求或断线即失败；主机逐片检查相同 ID、总长、连续偏移，队列仅四包，通知等待有 4 秒上限及原外层截止时间。不能把掉片、缺片或超时转成成功，也不在不确定失败后自动重发用户操作。

电脑→设备回复使用 `min(MTU-3,488)-9` 字节数据片段。仅支持 WriteWithoutResponse 的特征使用窗口：每四片及最后一片必须 WriteWithResponse；设备连续偏移校验使缺片在确认点失败。旧特征仍逐片确认。大 MTU 状态帧从 244 扩展至 488 字节，常规窗口从 16 调整为 8，保持约 3.9 KiB 队列上限；照片传输期间仍四片窗口。

图片上传或近期语音流期间，将后台状态发送节奏从 250 ms 调至 1000 ms；原四秒完整状态刷新约束和样本历史保留。未在传输中改变用户通道选择，也未延长认证有效期。

## 验证与待验

- Windows Release 构建通过。实际公共泵 + 窗口代码覆盖 MTU 23/247/517、通知/旧式读取、支持/不支持无响应写、原字节一致、最终确认，以及旧 ID、偏移错误、丢片、整窗丢失取消、通知队列溢出、回复首片丢失在确认点被拒绝。
- 项目要求的 `dotnet run --project ... -- --status-once` 再次遇到宿主 Console.OutputEncoding 控制台句柄异常；不记为该命令通过。同一 Release 候选 DLL 直接执行 `--status-once` 输出有效 JSON。Release 输出到隔离候选目录以避免覆盖运行中的原目录桥接；尚未部署。
- 设备实际 RPC 邮箱和通知窗口代码覆盖三个 MTU、四包有界、旧 ID/跳游标/非法 credits、通知入队失败。实际语音邮箱覆盖旧式读取、新通知、超时和断连。
- RPC 100 次完成竞争及 100 次重连竞争、图片分片与旧桥接回退、0.2.62 照片内存与相机所有权、完整 LVGL 预览回归通过。固件构建及最终镜像、内存和 IRAM 检查通过。
- 新增 C 测试初次构建缺少基础类型头文件，调整 include 顺序后通过；未弱化断言。曾使用不存在的桥接测试参数，程序明确拒绝且没有启动桥接；改用已有 `--self-test-tab5-ble` 后通过。
- 12 KiB/MTU 517 的模拟请求由 26 次读 + 25 次游标写，变为 1 次读 + 7 次窗口请求；这是协议往返数，不是真机耗时或无线峰值。

候选安装前冻结 BIN/ELF/源码及哈希。真机需确认诊断 `flow=notify4/write4`，记录同场景照片字节数、端到端毫秒，随后验证连续照片、历史翻轮与语音。仅在本地测试完成，不能宣称达到硬件上限。安装完成后保持原自动轮播、页面/顺序和 15 秒间隔；固定蓝牙测速期间按用户验收页面例外处理。

参考官方 API：[NimBLE 通知及 mbuf 所有权](https://mynewt.apache.org/latest/network/ble_hs/ble_gatts.html)、[Windows GATT ValueChanged 与订阅](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.genericattributeprofile.gattcharacteristic.valuechanged)。具体实现依据本仓库固定 SDK 源码核对。
