# TAB5 .166 修复纳入核对 / Fix inclusion audit

2026-10-10。精确 `.166` 候选和配套 Windows 桥接已获批准并安装，完整 ELF / `ota_0 VALID` 启动核验通过。当天缓存、正常交互复测、离线书画操作和三次 USB 拔插通过；切页时仍有短暂省略号提示。设备端采用独立 `.tools/upgrade166/firmware` 快照，保留未安装的 `.165` 草稿及历史证据。

`.166` 已构建并通过正式镜像／sidecar 校验，固定交付路径为 `artifacts/firmware/tab5/latest/aibot_tab5.bin`。镜像 6,681,568 字节，OTA 分区剩余 527,392 字节。已安装，未公开发布。

| 产物 | SHA-256 |
| --- | --- |
| `0.2.166-ui` 镜像 | `7fb461cfa889c1474888e22aea861134585c64beae3c36e2137c899ef3de19a2` |
| ELF（与镜像内嵌值一致） | `ec9fb4d4d107ed74ee707b0e72764771e9bbd372c05fd8ce7096c00944c64be7` |
| 更新说明文本 | `b6ffad09a03bffadba2586a3b1eb03329e691ee107e8990902b6554eeaac6ada` |
| 配套 Windows 桥接 DLL | `6a9476363f7e7064aadc0000ab519f8dc6aaab909d31f37b51702e69cf1f88f6` |

固件归档为 `artifacts/firmware/tab5/versions/0.2.166-ui/7fb461cfa889-b6ffad09/`；配套 Windows 完整候选目录为 `artifacts/development/upgrade-166-20261010/bridge/`，已部署至固定 Release 运行目录。电脑端修复属于配套桥接。

## 安装与真机验证 / Installation and hardware validation

2026-10-10 用户已明确批准安装上述 `.166` 和配套 Windows 桥接。Windows 已从固定 Release 路径正常退出，完整备份至 `artifacts/development/install-166-20261010/bridge-before/` 后部署；26 个候选文件逐一哈希一致，DLL 为上述 `6a9476…f88f6`。正常用户资料目录、`test=False`、`pairMatchesDisk=True` 已确认，配对文件时间未变。

升级窗口自动化的控件缓存／焦点不一致，用户从规范 latest 路径选包并启动安装。Wi-Fi 分块压缩实际传输 4,181,266 字节，用时 16,297 ms；设备回报完整镜像 6,681,568 字节、`previousError=0`，安装阶段 19,911 ms。USB 核验通过三次身份／版本／递增运行时间检查，完整 ELF 为上述 `ec9fb4…c64be7`，实际分区 `ota_0`、`state=VALID`。

当天两类作品共 5 页、10 个横竖布局 JPEG，2,225,576 字节全部缓存；两块 RGB 输出。首次补齐后最后图片 RPC 为 20:40:42，之后的旋转／翻页持续增加本地命中数，解码样本 19–22 ms，无新取图。用户首次操作报告页码“…”及暂时无法翻页；当时与内存传输测速重叠，生产代码 `foreground_busy()` 在测速期间暂停书画解码。测速结束后用户复测正常，仍有短暂“…”恢复为正确页码。该显示提示没有在本轮删除；解码耗时不是手势到面板完成时间。

USB、Wi-Fi、BLE 各三次上传及三次下载、每次 262,144 字节，18 次加密内存传输全部通过。USB 三次实际拔下约 10 秒再插回，用户确认重连正常；桥接重新枚举后的连接阶段分别 110 / 203 / 125 ms。这些数字不代表用户可见的全程耗时。断开阶段 Wi-Fi 继续提供数据，USB 溢出、二进制过期及不完整 TX 均为 0。设备从最早启动样本保留一次 `USB RPC stage=1,error=0,bytes=0` 无回复记录，不能声称设备失败计数为零；后续计数不增加，未观察到 GCM 解码失败。桥接 20:38:22 的指标超时发生于 `.164`，不是 `.166` 新失败。

正常退出桥接后，确认桥接进程不存在、HTTP 请求超时；HTTP.sys 仍报告 PID 0 的内核监听条目，因此不把它写成“端口已关闭”。应用层 USB／BLE／HTTP 图片服务均不存在。用户保持设备不重启，确认名画、书法旋转、可用翻页、详情与返回离线正常。缓存位于 RAM，无 SD 卡时重启仍需补齐。

离线验收后运行正式 `--restore-cycle-default`，再从固定 Release 正常启动。`/status` 确认自动轮播开启，顺序 `codex, domestic_deepseek, domestic_zhipu, weather, stocks, system`、间隔 15 秒，与操作前逐项一致。桥接重启后 USB 109 ms 完成连接阶段，资料目录和配对读回一致，桥接 RPC／控制失败记录均为空，设备原有一次无回复计数不增加。安装取证位于 `artifacts/development/install-166-20261010/`。下方描述未部署的原始构建记录属于构建阶段历史状态。

本次观察覆盖约 12 分钟，含主动拔插、桥接离线与正常恢复；离线时诊断读取失败是测试阶段预期，不能描述成连续在线稳定性通过。在线样本的设备 RPC 计数保持一致，未复现 GCM 认证失败或新的指标确认／重连超时，USB 接收溢出、二进制过期和不完整发送计数均为 0。完整原始样本、阶段和最终结果见 `install-monitor.jsonl`、`hardware-observations.jsonl`、`phases.jsonl`、`observation-result.json`。最长日期真实内存、Windows 睡眠恢复及更长时间稳定性仍未在本轮验收；此前 `.164` 失败保留。

The exact .166 and companion bridge are installed with authorization. All 26 copied runtime files match; the real profile and pairing remain intact. Three boot checks verify the complete ELF hash and ota_0 VALID. All 10 current-day JPEG layouts (2,225,576 bytes) are cached, with two RGB outputs and subsequent local decode samples of 19–22 ms. The initial gallery stall overlapped memory benchmarking, which deliberately suspends decoding; the user confirms normal interaction after benchmarking ends, with a brief pending ellipsis still present. All 18 encrypted transfers and three physical USB reconnects pass. One initial USB RPC no-reply counter remains recorded without increasing; no GCM failure is observed. Offline operation passes with the bridge absent. The normal restore command and restart return the exact six-page automatic carousel at 15 seconds. Bridge phase timings are not physical end-to-end latency. Statements below describing no deployment belong to the earlier build-only stage; peak-day memory and sleep/resume remain untested on hardware.

## 修复矩阵

| 范围 | 当前实现和构建来源 | 本轮验证 |
| --- | --- | --- |
| 旋转和触摸 | `art_orientation.c` / `art_orientation_filter.h`；50 ms 采样、150 ms 稳定确认；两分类四方向触摸变换 | 真实驱动恢复、平放／抖动／过期拒绝；八组缺失方向及八组预热换图 |
| 翻页与详情 | `ui_gallery_reader.h` / `ui_gallery_saver.h` / `ui.c`；待执行目标累加，匹配结果到达前保留；详情保持作品、日期、页码 | 32 次快速点击、后台及前台等待、详情返回；实际 LVGL 物理坐标点击 |
| 刷新 | 目标方向未就绪时保留完整旧图，相同图片和布局不重复设源 | 八次相同画面零 LCD flush；4 MiB 绘图预算下分块和完整结果逐像素一致 |
| 当天缓存 | `gallery_jpeg_cache.c/.h` / `gallery_remote.c`；10 MiB、64 项，双分类全部页面和横竖屏 JPEG，后台不解码，只有两块 RGB 输出 | 30 布局／9.6 MB、72 次离线重开零 RPC、低内存暂停、跨日失败保留及迟到旧日拒绝 |
| 前台优先 | 已缓存前台图片可在后台下载期间独立解码；网络请求继续串行 | 实际生产 worker 的阻塞后台、缓存命中、三通道和缓冲归属测试 |
| 设备 USB | `usb_task_policy.h` / `usb_link.c`；栈与接收任务同核心、栈优先级较高；快速重开使旧心跳失效 | 63 种 FIFO 占用、实际 TinyUSB 重挂／FIFO、CDC 代次同步、Wi-Fi 回退及防重放 |
| RPC 与认证 | 保留实际 `rpc_link.c`、`rpc_reply.h`、`device_receive_reason.h`、接收端认证和帧边界 | 100 次完成竞争、100 次重连竞争，认证失败不返回明文，跨会话／旧序号拒绝 |
| Windows 配套 | 已验证候选包含首包压缩协商、RPC 连接代次、文本半帧清理、原生串口错误恢复、断线状态失效 | 318 项 Portable PDB 源码哈希一致；传输、认证、重放、取消和连接代次回归通过 |
| 升级图标 | 沿用 `.164` 的绿色圆底、白色箭头 | UI 回放及实际候选 sidecar 预览 |
| 转场边界 | `ui_gallery_transition.h` 的实验仅在 `TAB5_UI_PREVIEW`；设备分支关闭，不创建实验图层 | 构建参数、生产分支及 ELF 纳入核对；实验回放不代表设备启用 |

相对 `.164` 快照，设备主目录仅 7 个文件变化：`gallery_remote.c`、`ui_gallery_reader.h`、`ui_gallery_saver.h`、`ui_gallery_transition.h`、`ui.c`、`usb_link.c`、`usb_task_policy.h`。已有缓存、方向、箭头等改进没有被回退。分区表及实际 sdkconfig 与 `.164` 一致。

构建前 7,103 个主程序、组件、依赖及配置文件与隔离快照逐项 SHA-256 一致。CMake 正式入口启用本地书画，将 JPEG 缓存、后台 worker 和方向模块列入源文件；Windows 修复属于配套桥接，不能靠单独刷固件获得。

## 验证记录与限制

完整 LVGL 回放、书画五项专项、USB 六项专项、真实 telemetry 接收、Windows telemetry 和传输集成回归均通过。旧 UI 预览构建目录缺少 CMake cache，首次调用未开始编译；保留 `native-cache-missing.log` 后新建独立 native 构建目录，同一测试和断言通过。

构建后再次确认 153 个主程序／配置文件与检查前及现工作区一致；7 个关键编译单元均为正式设备配置，启用书画和 USB、没有 `TAB5_UI_PREVIEW`。Ninja 实际依赖包含新增头文件，ELF 包含书画及 USB 入口且没有预览转场对象／回调。首次全量构建记录了 SDK ROM 调试符号目录缺失警告；补齐本次进程环境后重新配置和构建成功，未修改系统环境。另保留第三方触摸库弃用 API 警告，未把警告数描述为零。

固定 latest 的实际 sidecar 已通过完整 LVGL 回放，`OTA_ACTUAL_SIDECAR_PREVIEW_OK` 与 `LVGL_PREVIEW_OK` 均通过；人工图像检查首屏及滚动末屏，版本、分类、换行、末尾验证说明及绿色圆底白色箭头正常。图片为 `notes-preview/ota-release-notes-actual.png` 和 `notes-preview/ota-release-notes-actual-scrolled.png`；它们是电脑预览，画面中的当前版本是测试夹具值，并非设备回报。收尾设备回报仍为 `.164`，自动模式、原六页顺序和 15 秒间隔保持，未发送升级 offer。

本轮不更改设备或日常桥接。Windows 完整修复待配套候选部署，设备端修复待 `.166` 安装。书画物理旋转到面板完成耗时、最复杂日期的真实内存占用、USB 拔插／睡眠恢复及持续并发稳定性仍需真机验收；此前 `.164` 的 GCM 和超时失败不因本地回归通过而清除。

证据目录：`artifacts/development/upgrade-166-20261010/`。核心记录为 `inclusion-audit.json`、`changed-from164.json`、`source-snapshot.json`、`bridge-source-audit.json`、`bridge-resource-audit.json`、`native-day.log`、各专项日志、`bridge-telemetry.log` 和 `bridge-transfer.log`。

## English

The user authorized a new firmware build after a complete inclusion audit. The isolated .166 snapshot preserves all installed .164 features and contains seven changed main-source files covering gallery interaction, device-disabled transition experiments and USB fixes. All 7,103 firmware inputs match the snapshot; partition layout and sdkconfig match .164. The companion Windows candidate matches 318 Portable PDB source hashes, including packed bootstrap, RPC generation fencing, text-stream cleanup and native serial recovery. Firmware alone does not contain these Windows fixes.

Full LVGL replay, five gallery suites, six USB/RPC suites, production telemetry reception and Windows integration tests pass. The missing old native CMake cache was recorded and resolved by configuring a separate native build, without weakening tests. No device installation or daily bridge deployment is performed. Physical interaction latency, peak-day hardware memory behavior, unplug/sleep recovery and sustained stability remain pending; prior .164 failures remain recorded.

The .166 image is built and validated at the standard latest path: 6,681,568 bytes with 527,392 bytes of OTA space remaining. The exact firmware, ELF, notes and companion DLL hashes are listed above. Post-build checks match 153 source/configuration files and seven production compilation units; actual header dependencies and ELF symbols confirm inclusion and exclude preview transitions. Missing ROM debug-symbol environment configuration was repaired locally and reconfiguration/build passed; an upstream touch API deprecation warning remains recorded. No OTA offer, device write or public release was performed.

The actual latest sidecar passes native preview, including its initial and scrolled screens, verified visually. The screenshot's current-version label is a desktop fixture, not hardware evidence. The real device remains on .164 with the original six-page auto carousel and 15-second interval.
