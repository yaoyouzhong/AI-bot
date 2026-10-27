# TAB5 0.2.281-ui 本地候选 / Local candidate

基于 GitHub .279 完整源码，保留 Codex 草稿“长按清空”，修复 5 个历史测试并解决 BLE 接收栈门槛失败。此前 .280 的测试与栈门槛限制已在本候选中解决，旧 .280 包及其原验收记录保留在 versions/。

## 修复与验证

| 项目 | 修复内容与证据 |
| --- | --- |
| photo-memory-test | 按 .279 软件编码的 CPU 所有权验证 PSRAM 分配；覆盖精确/非对齐长度、分配失败、不清零、双方向预览及单次转移。旧 DMA 输出假设不再适用，真实 CSI 缓存交接在相机测试中验证。 |
| display-flash-safe-test | 修正生产 ISR 函数的抽取终点，不再误包含屏幕预览函数；不提供 LVGL 桩，继续验证 10000 次刷新与两次刷新所有权屏障。 |
| camera-ownership-test | 检查每个 USERPTR 首次入队前已按正确地址、长度和标志完成缓存同步；注入同步失败，断言不入队、不启动采集且可清理重开；原 128 次重开及像素断言保留。 |
| rpc-exchange-test | 补齐授权状态及状态输出参数；保留 100 次完成与 100 次重连竞争测试，新增未发布忙碌不覆盖请求、未授权 BLE 拒绝、超时不误标忙碌。 |
| host-unlock-timeout-test | 抽取当前 admission 与 timeout 策略，覆盖 16 项断言，包括当前 computer-control、voice 和 screen-preview 等待预算。 |
| BLE 栈 | 用持锁的小范围读取替代接收及证明路径的整份配置复制，未增加共享静态工作区、未扩大任务栈、未调整门槛。实际 ELF 的 telemetry_input_access 为 176/320 字节（原 512），辅助读取函数仅 16 字节；授权、主机 epoch 与防重放路径保留。 |

- 全部原生目标编译成功；上述 5 项、BLE 身份证明/RPC admission 及 20 项相关回归，共 27 个测试程序运行通过。
- 完整 LVGL 界面回归通过，包含短按不删除、仅长按清空、未确认拼音、照片保留、忙碌保护、保存重试及继续输入。实际 .281 sidecar 的更新说明完成预览。
- ESP32-P4 构建成功；ELF 的 UI PSRAM、可执行地址保护、显示 ISR/XIP、BLE 栈四项检查全部通过。
- 另外三个受检栈帧：telemetry_info_access 256/512、receive_frame 96/256、telemetry_tree_bounded 128/160 字节；认证十六进制路径无直接 printf。
- 镜像 7,208,304 字节，原 OTA 槽 7,208,960 字节，余量 656 字节；分区、sdkconfig、SDK 和 managed dependencies 保持基线。
- 冻结输入 7,531 个，其中 7,515 个不变、16 个声明修改；另有 11 个恢复的测试文件，分别登记原始与当前摘要；9 个 SDK 修改文件摘要全部匹配。
- 完整补丁可应用到冻结 .279 source/，版本登记与中英文更新日志同步。

## 身份与交付

- 基线：`bc2c40ba851ba6a59a209a6de47c7dce97e6ed5b`，`docs/development/tab5-279-source/source`。
- BIN SHA-256：`684587e024c9a52e1e2e069abba066826a4e93c425c1ec87e574aa73a4f6ce00`。
- ELF SHA-256：`c73b154bbd2f3ac7a7afb70970ca0a67da4b0d0ea0d5e1cdaf5fd7213f7e755e`。
- 工作源码：`C:/Users/yaououzhong/Work/tab5-280`；构建：`scripts/build.ps1 -LocalArt -LocalVersion 0.2.281-ui`。
- 完整补丁：[tab5-codex-draft-clear.patch](../proposals/tab5-codex-draft-clear.patch)；摘要：[tab5-source-manifest.json](../proposals/tab5-source-manifest.json)。
- 日志与截图：`artifacts/development/tab5-281/validation/`，包括 repair-native-build、repair-*-test、build-281-verification、repair-candidate-checks、preview-281-with-notes 和 package-prepare-281。
- 固定候选：`C:/Users/yaououzhong/Work/AI-bot/artifacts/firmware/tab5/latest/aibot_tab5.bin`，使用正式桥接镜像/版本/摘要/sidecar 解析器准备并复核，附许可证和 SHA-256 清单。

## 尚待实机验收

未刷机、未发布。静态栈检查只证明被检查函数的局部栈帧及直接调用，不等于真实 NimBLE 任务最小剩余栈；还需设备持续 BLE 收发/重连验收。触摸长按、照片保留、任务切换与重启草稿、相机/高清预览及 USB/Wi-Fi/BLE 回归也待安装后验证。Windows 豆包录音稳定性不由本固件候选宣告解决。

All five formerly failing native targets now compile and execute successfully. The unchanged BLE gate passes with a 176-byte input callback frame, reduced from 512 bytes. All 27 relevant native tests, the complete LVGL preview, the P4 build and four ELF gates pass. OTA layout and frozen dependencies remain unchanged. This is a local candidate; physical stack margin, hardware acceptance and public release are not claimed.
