# 摄像头启动失败与重启修复（2026-09-26）

## 真机反馈与证据

- 首版拍照附件固件 `7FE699F39B2A65062D311E7668843FB91745CC048100D673D627B314E7C8C507` 已刷写，用户随后报告“摄像头未就绪”及再次重启。拍照功能未通过验收。
- 17:45、17:46 的 USB 心跳中重启原因均为 `4`（panic），运行时间发生归零。Wi-Fi 数字诊断中，内部内存最大连续块曾降至 2,560 字节；这不是内部剩余总量，也不能单凭此认定泄漏。
- 原版只有笼统错误提示，应用控制串口不承载 UART 控制台日志，因此未获取到这两次 panic 的精确崩溃栈。以下是源码中已确认的缺陷及修复，是否覆盖此次全部原因仍需新固件实测。
- 首轮修正版复位后 COM9、Wi-Fi、界面心跳正常，连续运行超过 3 分钟未发现再次重启。但诊断显示内部空闲总量约 31–34 KiB，低于相机启动检查的 48 KiB。尚未让用户反复尝试相机，继续修复静态内存占用。

## 修复

1. 官方 BSP 的相机时钟和本项目背光共用 LEDC Timer 0。相机将其设为 24 MHz / 1 bit，背光需要 5 kHz / 12 bit。相机改用 Timer 1，配置结构清零，保留背光 Timer 0 / Channel 1。
2. `esp_video_open` 初始化失败时原先先置 `video=NULL`，随后访问 `video->mutex`。现在先回滚引用计数并释放锁，再返回 NULL。
3. ISP 保留摄像头引用，因此关闭预览不会清除视频队列。重新分配帧缓冲时先清空旧队列；正在采集时拒绝重分配；CSI 重开时清除旧备用帧指针。
4. AE 控制器启动分配失败改为返回错误并回收已创建资源，不通过 `ESP_ERROR_CHECK` 直接终止整机。传感器配置清零；探测失败释放 SCCB 句柄。
5. 三通道状态 JSON 节点在设备任务启动前统一分配到 PSRAM，避免大量小节点占用 USB、CSI、ISP 所需内部内存。相机开始前检查内部空闲总量和连续块，分配失败保留界面。
6. 新增私有 `VIDIOC_DQBUF_TIMEOUT`，每次最多等待 250 ms，连续 3 秒无帧返回明确错误。原 `VIDIOC_DQBUF` 行为保持不变。收到真实画面后才允许拍摄。
7. 分阶段错误码显示到相机提示，并在配对 Wi-Fi 数字诊断末尾增加七列：`cameraStage,cameraError,internalFree,internalLargest,cameraFrames,previousCameraStage,previousCameraError`。阶段 1 内存、2 时钟、3 初始化、4 打开、5 格式、6 缓冲、7 启流、8 等帧、9 正常收尾。上次阶段/错误只放在 RTC 保留内存，不写 NVS、不记录照片或凭据。
8. 根据首轮修正版真机数据，将 32 KiB 蓝牙帧重组缓冲及两个存储卡目录列表移到 PSRAM。蓝牙缓冲在服务启动前分配，失败不启动该服务；两个目录列表只在打开存储卡入口时分配。保留原帧容量、鉴权、分片和超时逻辑，相机内存保护阈值不降低。
9. 内存调整后首次预览返回“摄像头时钟启动失败”，数字诊断 `stage=2,error=-1`，无再次重启。P4 LEDC 所有定时器共用全局时钟；AUTO 为背光选 XTAL 40 MHz，为 24 MHz 相机选 PLL 80 MHz，SDK `ledc_set_timer_div` 拒绝冲突。背光与相机现在从首次初始化起均指定 `LEDC_USE_PLL_DIV_CLK`，保留各自定时器和 5 kHz/24 MHz 输出；相机通道采用适合该源的默认非睡眠保活模式。
10. 时钟版真实启动后通过时钟、传感器初始化及视频设备打开，失败推进至 `stage=5,error=22`（格式/EINVAL），无重启。`CAPTURE_VIDEO_SET_FORMAT` 只填写像素尺寸，缓存格式的 `type` 留为 0；`esp_video_get_format` 完整复制缓存使调用者的采集类型丢失，随后的 S_FMT 无法找到流。修复 G_FMT 保留已校验的请求类型，新增实际函数的读取→设置→再读取回归，以及无效类型拒绝检查。

`esp_video 0.7.0` 没有完整的部分初始化撤销接口。初始化整体失败后当前启动周期不再重复注册设备，显示重启后重试；时钟、内存预检查或已初始化后的预览失败仍可重试。

## 验证

- `scripts/test-camera-recovery.ps1` 编译实际 BSP / esp_video 函数体，以硬件/RTOS 模拟接口注入错误：连续 20 次打开时背光参数不变；连续 20 次初始化失败不解引用 NULL、不遗留锁或引用；连续 20 次重分配不保留释放后队列指针；采集中禁止替换帧缓冲。输出 `CAMERA_DRIVER_TEST_OK`。
- `scripts/preview-ui.ps1 -Clang`：`LVGL_PREVIEW_OK`，现有界面、草稿及附件检查通过。
- 内存调整后的 `scripts/test-ble-reassembly.ps1` 使用实际蓝牙接收函数，验证 PSRAM 分配失败、连续 20 轮最大帧分片接收、超限拒绝、超时后新帧及未配对拒绝；`BLE_REASSEMBLY_TEST_OK`。再次运行 LVGL 预览通过。
- 内存调整版编译通过，镜像 6,433,280 字节，SHA-256 为 `BC4A2DB52B28B5E4A81C42E311FFF5A8D211E0AFC762A8AFD127CDE70FA2ED4A`，冻结在 `.tools/camera-headroom-20260926/`。ELF 符号证实三个对象由 32,772 / 5,764 / 5,764 字节改为各 4 字节指针，内部静态占用减少 44,288 字节；运行时空闲增加量需刷入后读取，不能直接等同。
- 共享时钟修正后，测试补充 P4 全局时钟限制，先复现 AUTO 背光与相机的冲突，再调用实际背光初始化函数与相机函数验证统一源及连续 20 次启动。`CAMERA_DRIVER_TEST_OK`，固件构建通过；`.tools/camera-clock-20260926/` 镜像 6,433,280 字节，SHA-256 `D1E244CA061C06915AC7436CB3D0FA4F0746982DCD659A93A39D30913A9F6009`，等待刷写验收。
- 时钟修正版已仅写应用区 `0x10000`，写入数据哈希验证通过；等待复位及实际相机测试。
- legacy 固件构建通过，镜像 6,432,720 字节，适用当前 8 MiB 应用区。仅刷写 `0x10000`，保留分区、NVS 和资源。
- 新版已通过 COM8 仅写入 `0x10000`，6,432,720 字节写入完成，esptool 返回 `Hash of data verified`；SHA-256 为 `5D546355933C72FC8B8C5272286561D42BA7D3ECFAF77E31DE4DAFB2C4780C4D`。刷写日志保留在 `.tools/camera-recovery-20260926/flash.log`。
- 内存调整版随后已仅刷 `0x10000`，6,433,280 字节写入哈希校验通过（`.tools/camera-headroom-20260926/flash.log`）。用户确认正常启动，COM9 和 Wi-Fi 恢复；18:07:54 诊断内部空闲 81,355 字节、最大连续块 51,200 字节，超过相机检查阈值。真实相机画面、拍摄/重拍/关闭再打开、照片上传、实际 Codex 收图及内存稳定性仍待验收。

## 格式修正版刷写记录

- `.tools/camera-format-20260926/` 冻结镜像为 6,433,280 字节，SHA-256 `742FCE548B348B7307E6125F960348E03BFCE1FE2F6FF5D15686B48154E8799B`。构建和实际驱动函数的格式往返测试通过。
- 已通过 COM8 仅写入应用区 `0x10000`，esptool 返回 `Hash of data verified`，退出码 0。日志为同目录 `flash.log`。
- 用户短按复位后确认屏幕正常，COM9 和 Wi-Fi 恢复。18:21:05 诊断内部空闲 80,411 字节、最大连续块 45,056 字节，界面心跳正常、显示欠载 0。已请求一次相机实时画面验收；拍摄与照片发送尚未通过验收。
- 用户随后确认实时画面可见。18:24:07 诊断 `stage=8,error=0,frames=683`，运行时间 216,195 ms、内部空闲 64,579 字节、最大连续块 30,720 字节，未出现重启或显示欠载。预览恢复已通过真机确认；拍摄、附件及实际发送仍待验收。
- 用户所说“拍摄看不到画面”已澄清为镜头与屏幕在同一面，朝外拍摄时无法看屏幕构图，不是预览缺失。未据此修改正常预览代码；后续需选择异屏取景或其他拍摄入口，倒计时只能缓解按快门困难。

## 上游来源

- [M5Stack 官方示例 BSP](https://github.com/m5stack/M5Tab5-UserDemo/blob/main/platforms/tab5/components/m5stack_tab5/m5stack_tab5.c)
- [M5Stack 官方相机示例](https://github.com/m5stack/M5Tab5-UserDemo/blob/main/platforms/tab5/main/hal/components/hal_camera.cpp)
- [ESP-IDF 5.4.2 P4 LEDC：全局时钟共享及可用时钟源](https://docs.espressif.com/projects/esp-idf/en/v5.4.2/esp32p4/api-reference/peripherals/ledc.html)

保留组件许可与来源。此文记录本地补丁，不代表已向上游提交反馈或修复。
