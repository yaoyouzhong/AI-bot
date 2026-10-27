# 0.2.66：0.2.65 启动回滚修复候选

## 现场事实

2026-09-30 用户批准部署，桥接已安装原 Release 路径，DLL `d4be7a0686140c0dcc0c2c14b11760c8ab398a905285b38068a78a7080279c0b`。0.2.65 Wi-Fi 传输 6635024 字节 / 35578 ms，启动约 20 秒内异常回滚至 .064，不能计为升级成功或新版本性能通过。

USB 确认 .064、ota_1、state=2/VALID、resetReason=4。保留记录 crash=1、cause=27（P4 栈保护），previousHealthySeconds=16，未达到原有 30 秒确认。PC `0x40159162` 为 `_svfprintf_r`，RA `0x401516da` 为 `sprintf`。codeCandidates 不是完整展开的调用栈。

冻结 .065 ELF 的 BLE 接收 callback 局部帧为 1680 字节，NimBLE host 总栈配置为 4096。新增增量校验在同一接收链调用 `cJSON_PrintUnformatted` 重打印完整状态，小数进入 newlib 浮点格式化。现场崩溃位置、新增调用和小栈占用一致。

## 修复

- 仅修复 .065 接收栈路径，保留三通道功能；桥接无需再次替换。
- 移除整树打印，改为固定指针栈的迭代边界检查：最多 4096 个节点、16 层子树、32768 字节字符串及键（含终止符）。更新前后均检查，拒绝时保留状态与心跳。这约束保留内存，不再声称精确序列化长度上限。
- 身份读取与数据接收分离为独立 GATT 回调，使身份 JSON 缓冲离开解密/解析/增量链。最终 ELF 接收回调局部帧 224 字节，receive_frame 96 字节，边界检查 128 字节。
- 不增加 NimBLE host 栈，不关闭栈保护，不放宽 30 秒健康确认和自动回滚条件。

## 证据与边界

1500 个小数的生产代码测试禁止增量路径调用 JSON 打印，核对精确小数、节点/字符串/深度边界及失败原子性。接收器 null/remove/数组、重放、会话、断线及新鲜度测试，BLE 窗口回归和 LVGL 预览继续通过。

`scripts/check-ble-receive-stack.py` 检查最终 ELF 局部帧与直接格式化调用；不是全调用链峰值证明。既有 PSRAM、扫描 ISR/IRAM 和镜像校验通过。AI-bot 证据：`artifacts/acceptance-20260930/integrated-065-crash-usb.jsonl`、`integrated-065-crash-symbols.txt`、`integrated-066-receive-tests.log`、`integrated-066-ble-stack.json` 等。

仍需真机保持 .066 超过启动确认时间，核验精确 ELF/分区/VALID，再测速。不能用本地检查宣称硬件修复已通过。
