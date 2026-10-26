# TAB5 .249 照片编码缓存范围修正

## 故障与依据

用户在已核验启动的 .248 上反馈添加照片“还是闪了两下”。接回 USB 后采到同一启动 .248 运行 520741 ms 的记录：显示欠载、刷新错误、超过 50 ms 的扫描间隔均为 0；最大扫描间隔 30032 us，最大完整画面绘制 1021798 us；照片编码成功 108456 字节，Wi-Fi 上传 497 ms / HTTP 200。这些计数不代表用户没有看到闪烁。证据仅含运行数字，没有照片内容，见 `artifacts/development/tab5-photo-cache-249-20261022/live-248-flicker.txt`。

检查实际构建使用的 ESP-IDF 5.4.2 发现：JPEG encoder 的 `bytes_per_pixel` 保存的是 `color_hal_pixel_format_get_bit_depth()` 返回的位数，输入缓存同步却直接乘该值。1280×720 RGB565 缓冲区为 1843200 字节，实际调用缓存同步 14745600 字节，超出 12902400 字节。真实驱动片段回归在 .248 代码上失败，横屏和竖屏均受同一计算影响。C2M 会写回所覆盖范围内的脏缓存；不能把它描述成必然覆盖全部内存，也不能仅凭这一缺陷宣称两次闪烁的唯一根因已确认。

[Espressif release/v5.4 源码](https://github.com/espressif/esp-idf/blob/release/v5.4/components/esp_driver_jpeg/jpeg_encode.c) 已改为使用调用方输入字节数，并在 TX DMA 前检查图像尺寸。本次沿用该修正原则，通过项目现有 SDK 补丁脚本应用，不整体升级 SDK。

## 修改范围

- `apply-jpeg-dma-memory.py` 保留已有 DMA 内存池和描述符修复；输入缓存同步使用 `inbuf_size`。在设置 TX DMA 前验证图像大小、位深、字节对齐和输入容量，以先比较后乘法避免溢出。
- 固件构建和本地预览均应用同一幂等补丁；不匹配的 SDK 源码拒绝写入，避免部分修改。
- 新增真实 SDK 片段的原生测试，模拟缓存和 DMA 边界，覆盖两种方向、四种格式、缓冲区尾部余量、输入过短、零值和乘法溢出。
- 不改 .248 界面流程、显示驱动、照片质量、分辨率、三张附件内存管理、绑定、配对和设置。

## 无线诊断现状

Wi-Fi 状态请求已携带部分运行、内存和相机诊断，Windows 只在配置诊断日志时收集这部分；常规诊断中的完整设备健康快照仍由 USB ACK 更新。显示、照片编码、上传详情尚未进入该无线诊断链路，所以断开 USB 后显示的是旧 USB 快照。无线启动核验另有已实现的 RPC 通路。完整无线诊断可以在现有绑定鉴权通路补齐，但本次固件不扩展协议或修改 Windows/Mac 桥接。

## 验证与边界

- 旧代码回归复现 8 倍同步范围；修正后 39 项输入/缓存范围检查和 14 项已有编解码器 DMA 分配/失败清理检查通过。
- 固件构建、九项相机原生回归和完整界面回归通过，包含 128 次模拟拍摄/重开、三张附件增删、无线直达和电脑选择。实际更新说明的首屏及滚动内容已目视检查。
- 镜像 `0.2.249-ui`，7168064 字节，SHA-256 `473049610858cc9c570fb091b9cee8a3f65db59b84d27e3bbd05b7ef7b84f91c`；ELF SHA-256 `33ff80c0cae9ddb7e0755b2d794c833e34f5ec0eb592eba0f8970c7764946380`。源镜像、固定 latest、历史归档哈希一致。
- 增量补丁 [TAB5-CAMERA-249.patch](TAB5-CAMERA-249.patch) 可在 .248 基线上复放；构建使用的 SDK 源码哈希和既有相机源码哈希已复核。完整记录：`artifacts/development/tab5-photo-cache-249-20261022/PREPARED-249.json`。
- 实时心跳先于新的刷机问询显示已运行 .249；用户随后确认“已经升级完成，并且验证过了，还是有闪屏”。本轮 Agent 未发起刷入或重复升级；只读启动核验确认精确 ELF、`ota_0 VALID`，采样运行 64074 ms。证据：`live-249-boot.txt`。本地代码缺陷修正通过，真机闪屏验收未通过。
- 用户补充闪烁发生在照片仍显示时，要求下一次固件同时补齐无线诊断。该工作转入 `tab5-photo-wireless-250-20261022`。未提交、推送或发布。

## English

The user still observes two full-screen flashes on .248. Fresh USB diagnostics show successful encoding/upload and zero recorded scanout underruns or flush errors; this does not negate the visible symptom. Pinned ESP-IDF 5.4.2 uses a bit-depth field as bytes in JPEG input cache writeback: 14745600 bytes for a 1843200-byte RGB565 frame. A test executing the actual SDK fragment reproduces the oversized span. Apply the upstream release/v5.4 approach through the existing reproducible patch script: sync the caller-provided byte count and reject invalid input geometry/capacity before TX DMA. No SDK upgrade, UI/display-driver change, quality reduction or new full-frame allocation. Tests pass for 39 input/cache cases, 14 existing DMA allocation cases, nine camera targets and the complete UI regression. Actual release notes were visually inspected; source/latest/archive hashes and previous camera-source hashes match. See PREPARED-249.json for exact build evidence. The user independently installed .249; exact ELF and ota_0 VALID boot were verified read-only. The user confirms flashes persist while the photo remains visible: physical acceptance failed. Wi-Fi already carries partial diagnostic headers and supports boot verification, but full runtime display/photo diagnostics are still USB-only in the current bridge; this candidate does not change that protocol.
