# Windows：ESP8266 刷机图解

[返回首页](../README.md) · [Windows 安装](INSTALL.zh.md) · [Mac 刷机指南](FLASH_MAC.zh.md) · [功能图鉴](FEATURES.zh.md)

本教程使用仓库实际配置的 **PlatformIO 上传流程**，适用于 ESP8266/ESP-12S、
240×240 ST7789、SD2 小电视引脚布局。不是 ESP32 教程，也不适用于任意外观相似的小屏。
下面是操作说明；文档生成过程没有刷写你的设备。

![刷机操作顺序示意，非成功日志截图](assets/guides/flash.svg)

## 1. 核对硬件，并留好回退材料

查看板卡型号、屏幕驱动及商家引脚说明，再与[实际配置](../firmware/platformio.ini)比对：

| 屏幕信号 | ESP8266 GPIO | 常见 NodeMCU 丝印 |
| --- | ---: | --- |
| MOSI / SDA | 13 | D7 |
| SCLK / SCL | 14 | D5 |
| CS | 15 | D8 |
| DC | 0 | D3 |
| RST | 2 | D4 |
| BL 背光 | 5，低电平点亮 | D1 |

这里是**信号对应表**，不是供电接线许可；供电电压和模块连线以自己的硬件说明为准。
成品小电视通常已经接好线。丝印和 GPIO 编号不是一回事，不能把 D7 理解成 GPIO7。

刷写会替换程序区域。先保留可用的旧固件/恢复方法、旧程序目录，以及自己的设置和资源备份；
只有源码并不等于已经备份设备里的原固件。不确定型号或恢复方法时先解决这一点再继续。
本流程不执行全片擦除，也不执行“重置设备 Wi-Fi”。

## 2. 准备工具

安装 Python 和 Git，先按[安装图解](INSTALL.zh.md)获取源码。
在仓库根目录打开 PowerShell，创建专用于刷机的 Python 环境：

```powershell
python -m venv .venv-flash
.\.venv-flash\Scripts\python.exe -m pip install platformio==6.1.18
.\.venv-flash\Scripts\python.exe -m platformio --version
```

应输出 PlatformIO Core 6.1.18。使用独立环境不需要改 PowerShell 执行策略。
首次构建会下载编译器和库，需要网络及足够磁盘空间。[PlatformIO 官方安装说明](https://docs.platformio.org/en/latest/core/installation/methods/installer-script.html)。

## 3. 找到实际串口

1. **退出桥接程序**及其他串口工具，再插入 USB 数据线。
2. Windows 开始菜单搜索“设备管理器”并打开，展开 **端口（COM 和 LPT）**。
3. 对比插拔前后出现的 CH340/USB-SERIAL 条目，记下括号里的 `COM` 号。
4. 没有串口时，先换数据线/USB 插口；若设备提示缺少 CH340 驱动，从[芯片厂商 WCH](https://www.wch.cn/downloads/CH341SER_EXE.html)获取。

也可使用只读命令查看：

```powershell
Get-CimInstance Win32_SerialPort | Select-Object DeviceID, Name
.\.venv-flash\Scripts\python.exe -m platformio device list
```

输出形式示例（**不是你的实际设备结果**）：

```text
DeviceID  Name
COM5      USB-SERIAL CH340 (COM5)
```

后文 `COM5` 只是示例。请替换成上一步发现的真实端口，**不要照抄 COM5 或 COM7**。

## 4. 先构建，确认通过后再上传

以下命令在包含 `firmware/` 的**仓库根目录**执行：

```powershell
.\.venv-flash\Scripts\python.exe -m platformio run -d firmware
```

等进程结束，输出应有 `SUCCESS`。这一步只编译，不写入设备。
生成的文件位于 `firmware/.pio/build/nodemcuv2/firmware.bin`。
若失败，先处理第一条实际错误；不要上传旧目录里遗留的 BIN。

确认你准备好替换设备程序后，指定真实端口上传：

```powershell
.\.venv-flash\Scripts\python.exe -m platformio run -d firmware -t upload --upload-port COM5
```

上传期间保持供电和数据线连接。PlatformIO 根据本仓库 `nodemcuv2` 环境处理芯片及上传参数，
无需手工输入其他项目的 Flash 地址。等待写入、校验和重启步骤完成，退出码应为 0，最终看到 `SUCCESS`。
**编译 SUCCESS 与上传 SUCCESS 是两件事**；最终还要完成下面的设备确认。

### 如果使用的是完整固件材料 ZIP

材料包不是仓库目录。完整解压后进入 `source/firmware`，使用包内的 `rebuild.ini`：

```powershell
# 在完整材料包解压目录执行
python -m venv .venv-flash
.\.venv-flash\Scripts\python.exe -m pip install platformio==6.1.18
$flashPython = (Resolve-Path '.\.venv-flash\Scripts\python.exe').Path
cd source/firmware
& $flashPython -m platformio run --project-conf rebuild.ini
& $flashPython -m platformio run --project-conf rebuild.ini -t upload --upload-port COM5
```

此路径会重建包内源码后上传重建结果，不是直接写入 ZIP 顶层的预编译 BIN。
保留 `source/vendor` 和其他材料；不要只复制 `source/firmware`。详见[固件材料说明](FIRMWARE_PACKAGE.md)。

## 5. 刷完怎样才算连上

1. 上传结束，关闭串口监视器，再启动 Windows `AIBotBridge.exe`。
2. 等待自动串口探测。右键托盘 → **设备连接 → USB 管理与诊断 → 设备信息…**，核实能读到设备响应。
3. **显示模式 → 系统监控**：检查实体屏出现 CPU/内存等信息，且会更新。
4. **显示模式 → 天气时钟 / 桌宠**：检查实体屏确实切页。默认桌宠不要求导入文件。
5. **显示模式 → 智能跟随**，确认 **循环展示 → 启用循环展示**；保留原有勾选页面、顺序与间隔。

![实际设备控制窗口的离线截图；连接结果以你的设备为准](assets/screens/device-control.png)

“能编译”“能上传”“电脑镜像能显示”都不能替代真实设备握手、切页和稳定运行确认。
新默认桌宠的设备显示仍需刷入包含该修改的固件；只更新电脑程序不会自动升级设备。

## 6. Wi-Fi 是可选回退，不是 USB 前置条件

仅使用 USB 时可以先不配 Wi-Fi。需要回退时：

1. 退出桥接但保持设备 USB 供电，设备没有可用 Wi-Fi 且没有新鲜 USB 心跳时，启动约 15 秒后可开启 `AI-bot-Setup` 配网热点。
2. 连接该热点，按 WiFiManager 门户配置自己的网络；没有自动弹出门户时查看该连接的网关地址，在浏览器打开该地址。
3. 电脑恢复正常网络，重新启动桥接，先完成 USB 握手；桥接会通过 USB 下发认证回退配置。
4. 设备和电脑须在可互通的局域网。运行 **设备连接 → USB 管理与诊断 → 测试 Wi-Fi 回退（保持 USB 供电）…**。
5. 测试会短暂暂停 USB 状态发送并恢复；观察测试结果。单位客户端隔离或防火墙会影响 LAN 回退，不能用关闭防火墙代替正确配置。

这项测试保持 USB 供电，**不要拔掉唯一电源线**。完整回退仍有待验收，失败不等于 USB 不能用。
“重置设备 Wi-Fi…”是另一个会改变设备配置的动作，不属于常规刷机或测试步骤。

## 7. 常见故障

| 现象 | 建议检查 |
| --- | --- |
| 找不到串口 | 先检查数据线、USB 插口、设备管理器与 CH340 驱动 |
| Access denied / could not open port | 退出桥接、串口监视器及其他占用程序，重新核对 COM 号 |
| Failed to connect / 一直 Connecting | 确认是 ESP8266 和当前端口；核对板卡自动下载电路与厂商 BOOT/RESET 步骤，不猜其他板卡的按键组合 |
| 库下载或编译失败 | 检查网络、PlatformIO 版本和首条报错；构建通过前不继续上传 |
| 上传成功但黑屏/颜色异常 | 先核对 ST7789 型号、引脚、背光极性与供电；不要反复全片擦除 |
| 更新程序后小屏仍旧样式 | 电脑程序与固件分别更新；核对刚上传的是同一份源码构建的固件 |
| 刷完显示 PC OFF | 先启动桥接并确认握手；这表示当前未收到有效电脑状态，不能只凭此判断刷机失败 |

原始诊断入口及测试通过标准见 [USB 与回退验收](USB_VALIDATION.md)。
