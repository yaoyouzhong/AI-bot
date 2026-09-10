# Firmware materials / 固件分发材料

具体刷写步骤见 [Windows 刷机图解](FLASH.zh.md)或 [Mac 刷机指南](FLASH_MAC.zh.md)，本文侧重分发材料与重建。

本包是本地构建候选，包含 `firmware.bin`、AI-bot 固件源码、实际构建所用的
Arduino core/库/PlatformIO 平台源码和随附 SDK 文件、许可、依赖版本及校验清单。
不含设备 Flash 备份、Wi-Fi 信息、私人图片或用户配置，也不包含主机编译器。
依赖材料排除不参与此固件构建的示例、测试及独立上传工具；保留 core、库、
构建脚本、SDK 与原始组件许可。原始框架未在本轮修改。

固件平台锁定 espressif8266 4.2.1，Arduino core 3.1.2（PlatformIO 包 3.30102.0），
TFT_eSPI 2.5.43、ArduinoJson 7.4.3、WiFiManager 2.0.17，与收尾前本机实际解析版本
一致。编译器为 toolchain-xtensa 2.100300.220621（GCC 10.3.0 / newlib 4.0.0）。
对应版本和每个文件的 SHA-256 记录在 `DEPENDENCIES.json` 和 `FILES.sha256`。

## 重建与修改

先安装 Python 和 PlatformIO 6.1.18，再在解压目录执行：

```powershell
cd source/firmware
python -m platformio run --project-conf rebuild.ini
```

PlatformIO 可能需要联网安装锁定的平台和编译工具。`rebuild.ini` 指向包内的
`source/vendor` core 和库；可在这些源码中修改后重新编译和链接。主机工具链
由 PlatformIO 官方包仓库安装，不以打包整个主机环境替代许可和源码材料。
`source/vendor/espressif8266` 保留平台构建脚本作为对应证据。
编译输出为 `source/firmware/.pio/build/nodemcuv2/firmware.bin`；路径、构建时间和
主机差异可能改变二进制，因此源码可重建不等于跨机器逐字节相同。

本包中的第三方源码和 SDK 保持其各自声明，未改写为 MIT。LGPL 库的源码随包
提供；AI-bot 应用源码也随包提供，以便修改后重新链接。Espressif NONOS SDK
仅用于 ESP8266。工具链不随包提供，但固件链接的 GCC runtime/newlib 的许可
已收集在 `licenses/compiler-runtime`。组件级声明详见 `THIRD_PARTY_NOTICES.md`。

构建和归档校验不会刷写设备，也不证明 USB/Wi-Fi、实体视觉或长期运行通过。
日后公开分发时应原样提供完整材料 ZIP，不能只提供 BIN 或依赖可变化的网页链接。

## English

This local candidate contains the firmware BIN, corresponding AI-bot/core/library
sources, platform scripts, bundled SDK files, notices, exact dependency versions
and SHA-256 manifest. It contains no device dump, credentials, private artwork or
host compiler. Install PlatformIO 6.1.18 and run the command above from the unpacked
directory. `rebuild.ini` uses the included core and libraries; modify them and
rebuild/relink as needed. Platform/compiler downloads may require network access.
Buildability is not a claim of byte-identical cross-host output or device acceptance.
Preserve this complete materials archive when distributing the firmware.
