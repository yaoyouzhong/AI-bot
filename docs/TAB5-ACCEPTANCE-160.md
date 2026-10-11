# TAB5 .160 本地安装与实机验收 / Local installation and hardware acceptance

2026-10-10：用户批准精确镜像及桥接后，已本地安装 .160，启动核验通过。三轮 RAM 完整性测速通过，持续 USB 稳定性观察失败；屏保方向和底部布局也收到真机问题反馈。不得将本次安装称为全部验收通过。未提交、推送或公开发布。

.160 为 6,676,528 字节；独立构建镜像占应用分区约 93%，保留 532,432 字节。安装前固定路径准备、正式校验、归档与实际 sidecar 原生升级说明预览通过，145 个 main 文件与当时构建快照一致。固定 latest 后续可以指向新候选，复核 .160 须使用下列历史归档。

| 对象 | SHA-256 |
| --- | --- |
| .160 镜像 | `8b03de92a33e3195e5068f67ccc757321cc1ce656b19da1f113f142a48c77272` |
| 内嵌 ELF | `d7bd7e8e1549fc5e5edbd035b799db98649bff72e613d543481886705b86845a` |
| 升级说明 | `2fdf70bcf7560051b706049362c89dc1894e2371b3745367ea9866d5cd1cd0d9` |
| 配套桥接 DLL | `023ff6a289214619ddd81c476dcd24c906cdf8d22eb1eb6b2805a4d11bd92fca` |

归档为 `artifacts/firmware/tab5/versions/0.2.160-ui/8b03de92a33e-2fdf70bc/`。配套桥接为 `artifacts/development/saver-usb-20261010/bridge/`，已部署至固定 Release 路径并核对精确 DLL 哈希；原运行文件正常退出后保存在 `runtime-backup-20261010-124754/`。部署结果见同目录 `deployment-result.json`，正常桌面桥接 PID 60820。

## 行为与范围

- Windows 嵌入已审定的 2632 件导读，按图库稳定 `workId` 提供。画面「作品详情」含简介与欣赏提示；仅 14 件已核实正文的书法可出现释文。没有匹配导读时隐藏入口，保留原图。
- 设备端在成功图片之后发布对应文字，按分类、日期与作品身份检查。阅读暂停自动页码、日期与方向变化，返回保持原图原页并留出一个完整阅读间隔。横竖屏、纸色与深色沿用艺术屏保，普通画面触摸仍可退出。
- UTF-8 文本按完整字段检查，不截断；字库补足审定数据中的繁体、罕见字。原 JPEG 字节与资源包保持不变。
- USB/RPC 候选让解密正文显式使用 PSRAM，回复 body 转移所有权，避免再复制 JSON 树。连接在解码中改变代次时拒绝旧结果，不复活旧会话；无效结果清空有效数据计数。
- 新数值诊断区分传输、认证、信封、正文和连接代次失败。新增本地负错误码必须保持负值传给 OTA，不能误作已读字节数。未缩小块大小、降低串口速率或扩大超时掩盖失败。

## 本地验证

- Windows Release 构建：0 错误、0 警告；正常桌面启动的 `--status-once` 通过。候选在隔离目录构建，没有覆盖正在运行的 Release 程序。
- 导读检查：2632 件、14 件核实正文、UTF-8 边界、未知身份与小回复约束通过。
- 真实桥接路由、加密与回环 HTTP，加上模拟 USB/BLE：732 个认证请求、36 幅分页／方向图片，导读身份与原文、48 KiB/8 KiB 范围、SHA-256、JPEG 解码、旧固件请求兼容通过。此项不经过真实 USB 线缆或无线电。
- 原生 LVGL：阅读触摸不退出、阅读时暂停轮换、返回原页、横竖屏与四方向、完整图片和部分刷新一致性通过。扫描全部 1765 件绘画与 867 件书法的文本及已核实释文，字库覆盖通过；真实最长标题在两个布局中未遮挡返回操作。
- RPC/CDC 故障测试：100 次完成竞态、100 次重连竞态、旧回复隔离、完成信号分配失败、断连终止发送、写入堵塞期限、嵌入换行的二进制、认证与分配错误、回复所有权通过。
- 图库 worker、JPEG 缓存与 DMA 输出测试：失败保留原图、同链路有限重试、认证失败不重试、前台抢占、缓存日期／哈希／身份隔离、超长导读拒绝通过。
- C# 产生的二进制回复向量由固件 C 解码检查；OTA 范围、压缩、尾部损坏、取消、会话切换、旧端回退及本地错误不能推进写入通过。

原始证据保存在 `artifacts/development/saver-usb-20261010/` 与同级 `saver-usb-20261010-*.log/jsonl`。两次原生测试调用曾漏传所需的向量／固件参数，随后用正确参数重跑；初次调用记录保留，不把它们当代码通过。模拟与软件渲染不证明真实显示或链路稳定。

## 实机结果与剩余验收

- 实际 OTA 通道为 BLE；用户最初使用仅蓝牙模式，不能记作 Wi-Fi OTA。12:50:01 完成升级，12:50:10 新固件启动。USB 核验确认精确 ELF、`ota_1`/`VALID`、前次总字节 6,676,528、保留 OTA 错误 0、栈余量 3852。见 `after-boot-verification.txt`。
- 三轮 256 KiB RAM RPC 测速共 54 项（USB/Wi-Fi/BLE 各 18 项，上下行各 9 项），全部错误码 0 且完成 262,144 字节；设备生产测速同时检查字节和 SHA-256。USB 上传包含 3845 ms 慢样本，其余 423–665 ms。证据为 `hardware-benchmark-1/2/3.json`；第三轮仅记录完成后捕获时间，不编造精确开始／结束时间。此项不等于 OTA 或持续稳定性通过。
- 自动模式连续观察从 12:52:56 至 13:52:57，共 3601.391 秒、1781 个样本，诊断读取失败 0 次，稳定性未通过。首个失败为 12:55:01：`status_ack` IOException，伴随 RPC disconnected。随后多次 `status_write`/`0x80070079`。记录 115 条不同的控制失败报告、283 个 USB 不可用样本、13 段不可用区间，最长约 133.5 秒；这些是每 2 秒采样的主机诊断，不是精确物理断连次数。报告运行时间中未发现意外重启。首失败永久保存在 `hardware-observation-first-failure.json/txt`，最终统计为 `hardware-acceptance.json`，不以成功重连冲销失败。
- 13:19、13:23、13:27 的运行时异常捕获进一步确认设备 `tab5_ack_rejected` 在随后写入超时之前发生。仅固定协议事件代码、异常类型、数值 HRESULT／线程号落盘，未记录状态正文、私有内容或凭据。尚未知道拒收原因，不能断言是内存、线缆或压缩错误。
- 用户报告横屏馆藏与页码重叠、竖屏详情在顶部反向。原先软件测试未发现图片预旋转与叠加层的相反关系；.161 新增真实图片四方向与转换后触摸验证，修正源代码并提供原生预览，尚未安装。见 [.161 候选记录](TAB5-ACCEPTANCE-161.md)。

以下仍是完整硬件验收要求；上面的局部成功不替代尚未完成的项：

1. 核对镜像、ELF 和桥接 SHA-256，升级后确认正确版本、启动分区 VALID、UI 心跳和无保留升级错误。
2. 真机预览绘画、书法详情：读简介、切欣赏提示、核实释文入口、翻页、转向与返回；确认当前作品身份和完整画面。
3. USB 固定通道连续上下行 256 KiB：每轮验证字节和 SHA-256，记录实际完成、失败与跳过数量，同时保存失败阶段、CDC 计数及内部堆余量。
4. 恢复自动模式，观察至少 60 分钟。首个 ACK 超时、异常断连、哈希错误、设备重启或显示故障即保留失败证据；后续成功不能冲销失败。
5. 检查断连时停止旧请求、USB 心跳失效后的 Wi-Fi 回退、同会话重连和旧回复拒绝；物理拔插另需实际操作记录，模拟测试不能代替。
6. 正常退出、恢复原轮播配置并从固定 Release 路径启动，确认 auto、循环开启，原页面、顺序和 15 秒间隔保持。

USB .159 的两次下载失败与 4.015 秒 ACK 超时仍见 [.159 记录](TAB5-ACCEPTANCE-159.md)。.160 的内存峰值调整及诊断已安装，但实机仍有失败，不能宣布 USB 稳定性修复完成。完整观察结束后，按新的明确批准正常退出并备份旧桥接，再部署 .161 配套桥接；13:53:35 的 `saver-layout-20261010/deployment-result.json` 与 `policy-after-deploy.json` 确认原自动模式、六个页面、原顺序和 15 秒间隔保持。USB 测试所需的设备网络自动模式暂时保留，测试结束再恢复用户原仅蓝牙选择。

## English

The .160 candidate integrates reviewed artwork Details into the production Windows service and device UI. It pins the displayed work while reading, shows only verified transcriptions, preserves original image bytes and checks stable identity and UTF-8 bounds. Local Release, authenticated simulated transports, native LVGL/glyph coverage and failure-injection tests pass. Bulk replies avoid a second JSON-tree allocation, reject stale connection generations and retain negative local OTA errors. New content-free counters distinguish failure stages.

The exact approved .160 image and bridge were installed. Actual OTA used BLE; USB boot verification matched the embedded ELF and VALID partition. All 54 RAM integrity cases across three hardware suites passed, but sustained observation failed after 125 seconds. The complete 3601.391-second observation retained 1781 samples, 115 distinct control-failure reports and 13 approximate unavailable periods, the longest about 133.5 seconds. No diagnostic reads failed. Runtime exception capture confirmed device status rejection preceding write stalls, without establishing its underlying cause. After the full hour, the separately approved .161 bridge was deployed with a normal exit and backup, and the original six-page automatic carousel and 15-second interval were verified. Physical gallery/recovery acceptance and restoration of the user's original BLE-only network preference remain separate requirements. No public release or Git submission occurred.
