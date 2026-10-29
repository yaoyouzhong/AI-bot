# Mac TAB5 TCP 升级对照 / TCP upgrade comparison

## 范围与证据

用户要求先定位 Mac 大量重传，参考 Windows 实现，修复后用完全相同的固件实测。固定镜像为 TAB5 `0.2.288-ui`，7207488 字节，SHA-256 `7b6a2e30956fddde7f774de2f3d45f060d95a32d6b1b07eefeb82e63c65176d0`，ELF `8fb43ec9296abb22845bf92477aa9c661f9068e9fde2632316a542ffe7b115a4`。不重建固件、不改变分区或展示设置。

本机证据：`artifacts/mac-voice-source-20261029/ota-root-cause/`。用户在本机输入管理员密码启动限定 TAB5 地址和 8765 端口的五分钟 TCP 摘要采集；只保存 tcpdump 文本中的时间、序号、确认号、窗口与长度，未保存 pcap、HTTP 认证头或负载。采集自动结束，未修改系统权限、网络设置或 VPN。

## 观察与对照

- Windows `LanStatusServer` 使用系统 TCP socket、TCP_NODELAY；`Tab5OtaFlow` 对已协商快速流按 16 KiB 等待写入。Mac 先前使用 Network.framework 的 NWListener/NWConnection。关闭 Nagle、MSS 参数和应用发送节奏的既有实验不能作为根因修复。
- 实际握手中 TAB5 声明 MSS=1440；主机抓包却记录连续流中 179 个 1460 字节负载，反复由较小重传推进确认号。该连接无零接收窗口。对照流在 540672 字节中止，原启动分区保留。
- 主机抓包可能处于网卡卸载之前，仅凭该现象不能断言物理链路丢包点或某个系统内部函数错误。为隔离发送路径，临时用 BSD socket 转发同一 HTTP 实现、认证和镜像；本机后端明确不信任被中转的 loopback 请求。
- BSD 对照记录协商 MSS=1440，约 6.7 秒完成主机提交，记录到约 40 KiB 重传。设备完整接收/安装和自动精确启动核验通过。不能把主机提交时间当成全部升级时间。

| 实测阶段 | 稳定分块 R14 | BSD socket 对照 |
| --- | ---: | ---: |
| 完整安装（设备记录） | 25.921 / 28.248 秒 | 21.602 秒 |
| 等待数据（设备记录） | 4.166 / 6.367 秒 | 0.020 秒 |
| 分段擦除 | 9.181 / 9.144 秒 | 9.209 秒 |
| 写入 | 11.137 / 11.316 秒 | 10.874 秒 |
| 镜像校验 | 0.934 / 0.940 秒 | 0.939 秒 |

同一镜像、同一设备的对照确认：更换 Mac TCP 发送路径消除了长时间等待数据，固件擦写逻辑不需再变更。内部 OS／卸载实现的具体缺陷位置不作未经验证的断言。对照后运行 `ota_0 / VALID`，精确 ELF、三个实时 UI 样本及原设置保留通过。

## 整合修复

候选 R18 以公开 Darwin BSD socket 替换 HTTP 传输适配层，保留现有解析、身份认证、重放拒绝、压缩、偏移恢复、旧设备节奏及设备启动核验。只有已认证、身份匹配的 .288 及之后 0.2.x 接收端恢复快速连续流；原有范围回退保留。不依赖诊断中转程序，不设置系统全局 TCP 参数。

适配器保留 IPv4/IPv6 与 loopback 识别，独占监听端口，最多 32 个活动连接；关闭先 shutdown 唤醒阻塞 I/O，最后一个系统调用退出后再 close，避免描述符复用造成旧请求访问新连接。新增检查针对真实阻塞读写取消、描述符复用、重复监听拒绝和启动前关闭。最终签名包的完整升级验收结果另附，构建通过不等于实机通过。

## English

The authorized comparison uses the exact unchanged .288 image and preserves device settings. A user-started, five-minute administrator capture records only TCP text summaries for the paired device and bridge port; no packet payloads, authentication headers, persistent permissions or network/VPN changes are saved or applied.

The peer advertises MSS 1440. Host-side Network.framework stream observations include 179 payloads of 1460 bytes followed by smaller retransmissions and advancing acknowledgements, without a zero receive window. Host capture alone cannot identify the physical drop point or an internal OS defect. A temporary BSD relay isolates the physical sender while retaining the existing parser, authentication and image; relayed loopback connections are explicitly untrusted. The comparison negotiates MSS 1440, submits the compressed image in approximately 6.7 seconds and records about 40 KiB of retransmission. Device installation takes 21.602 seconds, including 9.209 erase, 10.874 write, 0.020 data wait and 0.939 image verification. Exact ELF, VALID partition, three live samples and unchanged settings pass. Host submission, device installation and healthy-boot completion are distinct measurements.

R18 integrates public BSD sockets into the existing HTTP transport instead of shipping the diagnostic relay. Parsing, authentication, anti-replay, compression, offset recovery, legacy pacing and boot verification remain. It preserves IPv4/IPv6 and loopback recognition, exclusive listener ownership and bounded live connections. Cancellation shuts down pending I/O before closing its descriptor. Targeted lifecycle and whole-image checks accompany the integration; final signed-package hardware acceptance is recorded separately.

BLE scope: authenticated ranges/compression and device OTA logic are compatible, but Mac's fixed four-write/one-barrier schedule differs from Windows bulk-window/connection-parameter options. No complete Mac BLE reinstall timing is claimed in this Wi-Fi diagnosis.


## 最终签名包实机验收 / Final signed-package acceptance

R18 最终包 SHA-256 `c5d856c3d491347bda206b7077f6962920e907f0fd852ec079cfaf0638b57f27`，固定安装路径已更新，R14 完整旧包已备份。原有四个未提交的用户窗口改动仍不进入部署候选。独立候选和完整工作区 Release 构建通过；3212 项隔离、81 项 HTTP／真实 socket 生命周期、16 项端口恢复及原生界面检查通过。端口恢复测试发现 BSD bind 同步报告占用，已补齐原有有界重试语义，复验通过。`swift test` 仍因当前 CLT 缺少 XCTest 模块失败，未记通过。

相同 .288 镜像再次通过 Wi-Fi 完整重装：主机提交约 11.3 秒，设备安装 **21666 ms**，擦除 9209、写入 10956、等待数据 **32**、镜像校验 933 ms。采样中的升级 TCP 连接发送 4954005 字节、重传 34560 字节，约 0.7%；不能宣称零重传。从发起到健康启动核验通过约 74.0 秒，包含重启和设备健康观察。最终 ota_1／VALID、精确 ELF、三个实时 UI 样本及设置回读一致。与稳定分块 25.921／28.248 秒相比，完整安装减少约 16%–23%，网络等待基本消除；剩余主要是设备 Flash 擦写，不宣称十几秒完整安装。

已恢复唯一正常桥接实例，固定二进制摘要与验收包一致。实际设备中心确认 .288、USB 数据、Wi-Fi 认证请求及蓝牙认证待命；检查窗口关闭，保持后台运行。未执行推送、标签或公开发布。完整蓝牙升级、Windows 原生同镜像对比及 ESP8266 实机不在本次通过范围；语音闪现／恢复和清空残影的既有现场体验验收仍独立待验。

The final signed R18 binary above is installed at the fixed path with a complete R14 backup. Both isolated and workspace Release builds pass, alongside 3212 isolated checks, 81 HTTP/socket checks, 16 listener-recovery checks and native UI fixtures. The synchronous BSD bind collision path now uses the existing bounded recovery policy. XCTest remains unavailable in the installed CLT.

The exact unchanged .288 image is reinstalled over Wi-Fi: approximately 11.3 s host submission, **21.666 s device installation**, including 9.209 erase, 10.956 write, **0.032 data wait** and 0.933 image verification. The sampled OTA connection records 4,954,005 transmitted bytes and 34,560 retransmitted bytes (~0.7%), not zero retransmission. Request-to-healthy-boot verification is approximately 74.0 s. Exact ELF, ota_1 VALID, three advancing UI samples and unchanged settings pass. Installation improves approximately 16–23% versus the stable range baseline; flash erase/write now dominates. No ten-second total-installation claim is made.

The sole normal bridge is restored in the background with USB data, authenticated Wi-Fi and authenticated BLE standby; the inspection window is closed. No push/tag/public release. Full BLE upgrades, same-image Windows-native comparison, ESP8266 hardware, and the previously pending physical voice/composer experience checks remain separate.
