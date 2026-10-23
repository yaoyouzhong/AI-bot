# TAB5 .254：照片软件编码与额度标题

## 本轮反馈与范围

用户确认 `.253` 两次蓝牙解锁均成功，记录为本轮通过；添加照片仍蓝闪，未通过。用户要求去掉近七日标题中“含跨日补记”的括号说明，随后追加去掉数字旁星号；累计口径和数据不变。本轮不改 Windows 或 Mac 程序，不重复解锁验收，不提交、推送或刷写新镜像。

## 改动依据

`.253` 的已存采样没有显示欠载或刷新错误；本轮无线读取超时，只取得标为过期的旧 USB 样本，因此不能作为本次失败的新鲜证据。用户看到的蓝闪继续有效。先前的预览所有权、缓存分段、LCD 优先级及 DMA 短突发候选都没有消除可见故障，不能继续把它们称为已修复。

[Espressif LCD FAQ](https://docs.espressif.com/projects/esp-faq/en/latest/software-framework/peripherals/lcd.html)说明 P4 MIPI-DSI 蓝屏可由外部存储带宽不足引起；这只支持调查方向，不能证明本机这次就是同一根因。当前捕获后的照片已静止，硬件 JPEG 仍需通过 DMA2D 读取整帧并写回输出，与 LCD 同时访问 PSRAM。本候选将相机 JPEG 改为逐行软件编码，移除该操作的硬件编码 DMA、整帧缓存维护以及对界面刷新的长锁；不改变整个显示驱动、时钟、分区或其他图片解码路径。

- 固定 RGB565 全分辨率输入，quality 85、YUV420；逐行转换为 RGB，不另分配全帧 RGB888。
- 原始静态帧持有不可变租约直到编码完成；预览仍在上传成功后转交附件缓存，保留三张增删与重开路径。
- 每 16 行让出执行，软件只占用编码对象、一个 RGB 行和一个 MCU 行组；输出保留 2 MiB→256 KiB 的有界分配退让、失败清理与原照片重试。
- 编码失败不返回半张 JPEG，输出越界被拒绝。编码完成仍按实际字节收缩输出。
- 标题统一回“近7日 · 周额度使用（百分比）”；保留 carryOver 数据、缺采与部分记录语义，数字旁星号也按用户追加要求移除。

## 来源

只引入 Espressif 官方 `esp32-camera` 的 jpge 两个源文件，commit `f37e65608f709bd94846dee01d947f02ae07c0b0`。原文件不改动，保留 Public domain 作者声明和上游 Apache-2.0 根许可；固定 URL 与 SHA-256 在组件 `vendor/SOURCE.json`，本地封装和测试独立。没有迁入其他相机驱动、旧项目资源或 Git 历史。

## 验证状态

- 最终固件构建成功，镜像 7174416 字节，既有槽剩余 34544 字节；没有改分区。SHA-256 `394a1320405e337ded969657aaf7cbdfd386ac80f0a8c63c7abce11db0b0ffa7`，ELF `1a795f325b5ebf814abcbc4be5375334836ed103138697bda5d31205ce06bc74`。
- 九个相关 native 可执行测试通过，包含真实软件编码器四向图片、逐次分配失败与重试、有界输出、不可变源图、详细合成内容及生产准备函数的 128 对重开/重试；另由独立 Pillow 解码核对尺寸、颜色和 YUV420。单次最大额外申请 61440 字节，没有全帧 RGB888。没有读取用户照片，PC 耗时不作为 P4 性能证明。
- 最终去星号后的完整 LVGL 回归通过；真实 carryOver 夹具仍保留数值，预览不再有括号说明或星号。三张附件六次上传、删除首/中/末再添加、反复重开及图库交接保持通过。
- 最终 ELF 符号表存在 `tab5_jpeg_encode`，不存在 `jpeg_new_encoder_engine` 或 `jpeg_encoder_process`，确认相机不再链接硬件编码路径。硬件解码和原显示驱动保持不变。
- 17 个源文件补丁重放与当前源码一致；上游三个空白行原样保留，沿用仓库现有 archived-patch 属性。见 [源码补丁](TAB5-FOLLOWUP-254.patch)。固定交付目录已校验；实际 sidecar 首屏及滚动预览已检查通过。
- 已刷入精确 `.254` 并核验 ELF、`ota_1`、`VALID` 与递增心跳。蓝闪和真实处理耗时仍待用户实测，不宣称已解决可见故障。

## English

The user accepted both .253 BLE unlock attempts, while photo-add blue flashes remain a failure. This follow-up removes the carry-over explanation from the quota heading and the asterisk beside daily values, retaining the statistics and metadata. Windows and Mac programs are unchanged. The current diagnostic request timed out wirelessly and returned stale USB data; no fresh zero-error counter is claimed.

The camera candidate replaces hardware JPEG encoding with bounded scanline software encoding: unchanged dimensions, quality 85, YUV420, immutable captured frame and three-attachment ownership. It avoids a full RGB888 copy, codec DMA/cache sweeps and a long LVGL lock. Each MCU row yields; allocation/output failures retain the original photograph for retry. The unchanged jpge files come from the pinned official Espressif source above, with notices, root license, source URLs and hashes retained. The final build is 7174416 bytes with 34544 bytes free in the unchanged slot. SHA-256 is `394a1320405e337ded969657aaf7cbdfd386ac80f0a8c63c7abce11db0b0ffa7`. Nine relevant native executables, independent decoding of the real software output, final full LVGL regressions and a 17-file exact source-patch replay passed. The final ELF defines the software entry point without either hardware-encoder entry point. Maximum extra allocation in the real-code tests is 61440 bytes; PC timing is not a P4 latency result. The actual sidecar first/scrolled previews were inspected and passed. The exact .254 image is now installed with ELF, ota_1, VALID state and advancing heartbeat verified. Physical blue-flash and latency acceptance remains pending.


## 刷入与启动核验

2026-10-23 09:52–09:57，用户从正常桌面入口选定并启动已授权的精确 `.254`，实际使用 BLE；没有重复发起。完整接收 7,174,416 字节后重启，启动核验返回通过：运行 ELF 与候选完全一致，分区 `ota_1`、状态 `VALID`，设备心跳递增。第一次核验发生在启动状态尚未就绪时失败，随后重新读取已通过。保留三张资源和原有设置；物理照片蓝闪及处理速度尚待用户反馈。

The user selected and started the authorized exact .254 through the normal desktop UI over BLE. All 7,174,416 bytes were transferred; the later exact boot check passed with matching ELF, ota_1, VALID state and advancing heartbeat. The first early check failed before startup was ready; no second installation was started. Physical photo acceptance is still pending.

## 用户实测反馈 / Physical feedback

用户反馈：`.254` 添加照片仍蓝闪；蓝牙首次添加等待很久后失败，第二次成功，Wi-Fi 添加很快。此次软件编码候选未通过蓝闪验收，不能标记为已修复。新 USB 诊断确认 `.254`，相机/照片阶段无错误、显示欠载与刷新错误均为 0；末次约 92 KB 蓝牙上传成功约 3.4 秒，但这不是首次失败的完整记录。首次失败时桥接记录蓝牙特征发现错误 `0x80650008`，尚不能确定就是该次上传失败的唯一原因。未读取用户照片。

The user reports continued blue flashes. The first BLE photo upload waited a long time and failed, its retry succeeded, and Wi-Fi uploads are fast. This candidate fails physical blue-flash acceptance. Fresh .254 USB diagnostics show no camera/codec error, LCD underrun or flush error; the latest successful ~92 KB BLE upload took ~3.4 seconds, not the full failed-attempt duration. A nearby BLE characteristic-discovery error is recorded, but causation is unproven. No photo content was read.
