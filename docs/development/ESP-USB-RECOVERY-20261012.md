# ESP8266 USB 恢复与设备中心布局

## 问题与修改

- 现场固定 COM7 已失效，CH340 在另一插口枚举为 COM8；老程序不能按固定端口找回设备。CH340 的 USB instance path 也随插口变化，不能作为设备本体的唯一身份。
- ESP8266 0.5.3 通过 USB hello／pong 和设备信息上报固定 `device_id`；ID 来自出厂 station MAC，读取不依赖 Wi-Fi 连接。Windows 在已确认连接登记 ID，以后探测同类 USB 适配器，先匹配固定 ID 再发送任何配置、状态或资源，并保存新 USB 路径。
- 不同设备 ID、缺少 ID、TAB5 和已预留串口不能替代已登记小屏。普通设置保存不会用旧窗口快照覆盖新绑定。
- 连接状态由等宽分栏改为按单行文本宽度分配。极窄空间使用省略号和完整提示；正常与最小窗口中的原“COM7 · 连接中断”均完整显示。最小宽度调整为 820。

## 验证

- Windows Release 构建通过；桌面环境 `--status-once` 正常，使用现有 profile guard，不读取启动环境中的重定向旧配置作为现场状态。
- `--self-test-esp-usb-recovery`、`--self-test-device-recovery`、`--self-test-usb-management` 通过。覆盖离线空 IP 回执、多个适配器、错 ID、旧固件、绑定持久化、过期保存和停用状态。
- 5 个设备状态 × 100%／150%／200% × 正常／最小窗口，共 30 张真实 WinForms 渲染，布局问题为 0。
- PlatformIO 构建通过：RAM 46,160 字节，Flash 534,775 字节，镜像 538,928 字节。保留原有非本次变更引入的工具链／TFT／日期格式编译警告。
- 经单独授权，完整备份 4 MiB Flash 到用户私有目录，备份附 SHA-256；写入和 `verify_flash` digest 校验通过。实机启动报告固件 0.5.3，固定 ID 与刷机前读出的硬件 MAC 一致，USB 计数连续增长。
- 用户实际移动 USB 插口：COM8 → COM7，无手动重绑，ID 不变，新路径已保存，USB 同步恢复。随后实际 UART 测试模拟登记路径缺失，未启动 LAN 工作线程、未发送 LAN 配置，仅凭 USB ID 恢复通信，计数 78 → 80；常驻桥接恢复后计数 81 → 86，设备 LAN 计数为 0。
- 自动模式、六个原循环页面的顺序和 15 秒间隔保持。没有刷 TAB5，没有推送、标签或公开发布。

## 验证边界与材料

设备 Wi-Fi 未实际关闭；空 IP 协议测试及无 LAN 配置的真实 UART 测试证明恢复路径不依赖 LAN 验证，不应描述为“关闭 Wi-Fi 后物理换口”验收。macOS 仅同步可选字段解析，因当前主机为 Windows，未执行 macOS 编译／测试。

本地证据位于 `artifacts/usb-layout-recovery-20261012/`：`ui/report.json`、`flash-result.json`、`usb-only-hardware-test.log`、`final-runtime.json`。这些运行材料不进入发布包；完整 Flash 备份不放入仓库。

固件 SHA-256：`170DB55EC7E4501BB8B11FDD8C403D3C9B27BC5C811D33F0D00BA8DD1AA15BDB`。本地固件目录附 `LICENSE`、`THIRD_PARTY_NOTICES.md` 和 `SHA256SUMS`。
