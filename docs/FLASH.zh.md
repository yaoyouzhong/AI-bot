# Windows 给小屏刷固件：先备份，再写入

[下载选择指南](DOWNLOAD.zh.md) · [Windows 应用安装](INSTALL.zh.md) · [Mac 刷机](FLASH_MAC.zh.md)

**v0.2.0 新增小屏农历和模型页面，需要更新固件；仅使用电脑端新功能可暂不刷机。** 以下用于首次安装或更新小屏固件。

本页使用发布包内的现成 `firmware.bin`，不用 Git、.NET SDK 或编译器。工具固定为 **esptool 4.8.1**，请不要混用其他教程的参数。

![刷机顺序示意，非设备操作记录](assets/guides/flash.svg)

## 1. 核对硬件，并留好回退材料

本固件只适用于 **ESP8266 / ESP-12S + 240×240 ST7789 屏幕 + SD2 小电视引脚方案**。不是 ESP32 固件，也不适用于所有外观相似的小电视。型号不清楚时先问卖家。

<details>
<summary>展开核对引脚（成品通常不用重新接线）</summary>

| 信号 | GPIO | 常见 NodeMCU 丝印 |
| --- | ---: | --- |
| MOSI / SDA | 13 | D7 |
| SCLK / SCL | 14 | D5 |
| CS | 15 | D8 |
| DC | 0 | D3 |
| RST | 2 | D4 |
| BL | 5，低电平点亮 | D1 |

以[仓库配置](../firmware/platformio.ini)及设备厂商说明为准。D7 不是 GPIO7，供电不能按这张信号表猜测。

</details>

准备 USB **数据线**和可恢复的旧固件；没有旧固件时，在第 5 步读取设备备份后再写入。刷写会替换设备程序，本页不执行全片擦除或主动清除 Wi-Fi。

**完成标志：** 型号与引脚已确认匹配；不匹配就不要继续。

## 2. 下载并解压固件包

从 [v0.2.0 发布页](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.2.0)下载：

- `AI-bot-0.2.0-firmware-materials.zip`
- 同名 `.zip.sha256` 校验文件

在下载文件夹地址栏输入 `powershell`，回车，运行：

```powershell
Get-FileHash -Algorithm SHA256 .\AI-bot-0.2.0-firmware-materials.zip
Get-Content .\AI-bot-0.2.0-firmware-materials.zip.sha256
```

两串哈希一致后，右键 ZIP → **全部解压**。进入解压目录，顶层应有 `firmware.bin`、`source`、`licenses` 等。保留整个材料包。

**完成标志：** 能找到顶层 `firmware.bin`。不要选择 Windows EXE 或把 ZIP 当固件。

## 3. 准备刷机工具（第一次做一次）

安装 [Python 3.12 Windows x64](https://www.python.org/downloads/release/python-31210/)，选择页面 Files 表中的 **Windows installer (64-bit)**，在安装界面勾选 **Add python.exe to PATH**。已有可正常运行的兼容 Python 时可复用。

在刚才**包含 firmware.bin 的解压目录**，点资源管理器地址栏，输入 `powershell`，回车。下面命令逐行执行，上一行成功后再继续：

```powershell
python --version
python -m venv .venv-flash
.\.venv-flash\Scripts\python.exe -m pip install esptool==4.8.1
.\.venv-flash\Scripts\python.exe -m esptool version
```

**完成标志：** 最后一条显示 `4.8.1`。这些步骤不会写入小屏，也不需要改变 PowerShell 执行策略。

如果 `python` 找不到或打开商店，重新打开终端；仍不行时修复 Python 安装。安装工具失败时不要继续写入。

## 4. 找出小屏的 COM 号

1. 先从托盘菜单正常**退出 AI-bot**，关闭其他串口工具。
2. 打开 Windows **设备管理器 → 端口（COM 和 LPT）**。
3. 插拔一次小屏的数据线，找出随它出现和消失的那一行。
4. 记下括号里的 COM 号。下图 `COM5` 只是示例。

![通过插拔识别串口示意，非真实设备截图](assets/guides/serial.svg)

在同一个 PowerShell 中运行下面一行，按提示输入**你自己的实际端口**：

```powershell
$flashPort = Read-Host '输入设备管理器中小屏的 COM 号，例如 COM5'
```

**完成标志：** 已明确目标端口。没有端口时先换数据线或 USB 插口；确认缺 CH340 驱动后再从 [WCH 官方页面](https://www.wch.cn/downloads/CH341SER_EXE.html)安装。

## 5. 识别芯片、保存旧固件

仍在同一目录和终端，先识别设备：

```powershell
.\.venv-flash\Scripts\python.exe -m esptool --chip esp8266 --port $flashPort flash_id
```

应识别到 ESP8266 及 Flash 容量。报错、识别不符或一直连接时，先处理，不继续写入。

读取整片备份（会暂时重启设备进入下载模式，但不改写 Flash）：

```powershell
$backupFile = 'backup-before-ai-bot-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.bin'
.\.venv-flash\Scripts\python.exe -m esptool --chip esp8266 --port $flashPort --baud 115200 read_flash 0 ALL $backupFile
```

**完成标志：** 读取成功，并产生与检测容量相符的备份文件。把备份另存到安全目录；里面可能含私人网络配置，不要公开上传。备份失败就先停止，不直接跳到写入。

## 6. 写入现成固件

确认文件是本版顶层 `firmware.bin`，端口正确且桥接已经退出，再执行这一条：

```powershell
.\.venv-flash\Scripts\python.exe -m esptool --chip esp8266 --port $flashPort --baud 115200 write_flash 0x0 .\firmware.bin
```

`0x0` 是本项目 ESP8266 Arduino 固件的写入地址，不是通用 ESP32 地址。模式和大小沿用固件头，不额外覆盖。115200 是此处的刷写速度，和桥接通信速度不是一回事。

保持供电，不拔线。**完成标志：** 写入到 100%，出现 `Hash of data verified`，随后重启并返回命令提示符，没有报错。

![成功输出关键位置示意，不代表已执行刷机](assets/guides/flash-result.svg)

如果是失败、超时或 `Connecting...` 不结束，不算刷好。先按下面故障表处理，不反复擦除芯片。

## 7. 启动应用，检查实体屏

1. 保持 USB 连接，启动 `AIBotBridge.exe`。
2. 右键托盘 → **设备连接 → USB 管理与诊断 → 设备信息…**，确认有设备响应。
3. 切到 **系统监控**，看实体屏数据是否更新；再切一次 **天气时钟**确认能换页。
4. 恢复 **智能跟随**并启用循环展示，保留原页面顺序与间隔。

<img src="assets/screens/device-control.png" width="680" alt="设备控制真实离线示例截图，操作时需看到自己的设备响应">

**最终成功标志：** 写入校验通过、设备信息有响应、实体小屏可切页。电脑镜像有图，不等于小屏已连接。

基础 USB 不需要配 Wi-Fi。要无线回退时，再看[进阶配网说明](FLASH_BUILD.zh.md#6-wi-fi-是可选回退不是-usb-前置条件)。

## 遇到问题看这里

| 现象 | 下一步 |
| --- | --- |
| 找不到 firmware.bin | 确认下载的是固件材料包，并在解压目录顶层执行 |
| Access denied / could not open port | 正常退出 AI-bot 和串口工具，检查 COM 号 |
| 一直 Connecting | 检查数据线、供电和自动下载电路；按板卡厂商说明进入下载模式，不猜引脚短接 |
| 校验失败 / 写入中断 | 保存报错，检查连接及供电；不要继续宣称刷写成功 |
| 写入成功但黑屏、花屏 | 重新核对 ESP8266、ST7789 和引脚方案，不反复全片擦除 |
| 小屏显示 PC OFF | 启动电脑桥接并核对设备信息响应，再判断连接 |
| 想恢复旧固件 | 使用自己同一设备的备份和已确认的恢复方案；不要下载别人的整片备份 |

需要修改固件或从源码重建，另看[PlatformIO 进阶刷机](FLASH_BUILD.zh.md)。

命令依据 [Espressif esptool v4 文档](https://docs.espressif.com/projects/esptool/en/release-v4/esp32/esptool/basic-commands.html)和本仓库 ESP8266 配置。此处只核对命令接口与发布材料，不宣称本轮已对你的设备完成刷写。
