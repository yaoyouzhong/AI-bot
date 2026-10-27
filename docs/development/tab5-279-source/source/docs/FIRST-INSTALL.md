# TAB5 首次安装（Windows 候选功能）

新买的 TAB5 通过 Windows AI-bot 桥接执行 USB 首次安装。桥接完整发行包携带刷写工具，用户不需要 Python、ESP-IDF 或自行编译。此流程替换 **P4 的全部闪存内容及设置**，保留本机原固件备份，不修改 C6 无线芯片固件。

当前为本地候选功能，尚未公开发布。构建、模拟刷写及界面检查不能替代出厂 TAB5 真机验收；不同硬件批次及出厂 C6 固件兼容性仍需记录。

## 普通用户安装

1. 准备包含首次安装入口的 Windows 桥接完整发行包，以及配套的 `TAB5-first-install-<version>.zip` 和 SHA-256 校验文件。只从可信发布渠道获取；校验值用于发现损坏，不等于发布者签名。私有源码仓库不提供匿名下载，公开二进制渠道另行安排。
2. 打开 **TAB5 连接 → USB 配对 → 新设备首次安装**。用能传输数据的 USB 线连接 TAB5 的 **USB-C 接口**。
3. 长按 RESET 约 2 秒，直到内部绿灯快速闪烁后松开，进入下载模式。点击“刷新设备”，主动选择 TAB5 的 USB 下载端口。参见 [M5Stack 官方说明](https://docs.m5stack.com/zh_CN/guide/tab5/restore_factory)。不要选择其他 ESP32 开发板。
4. 选择首次安装 **ZIP 包**，勾选设备及替换设置确认，点击“备份并安装”。确认框会显示目标 USB 设备及固件版本。
5. 桥接核验包、芯片修订、16 MiB 容量和安全状态，读取并核验完整原固件备份，然后写入并核验安装镜像。准备/备份阶段可取消，写入后不可取消。保持供电与 USB 连接。
6. 提示“写入校验通过”后，**短按 RESET**，等待屏幕启动。刷新并选择重启后的 TAB5 USB 端口，再点“检查启动”。端口号可能变化。
7. 检查通过后点“进入 USB 配对”，选择这台 TAB5 并配对。再到 Wi-Fi 页保存 2.4 GHz 网络，确认设备与桥接的实际连接状态。Wi-Fi 已关联不等于桥接可达。

启动检查核对设备 MAC、版本、运行分区、ELF 指纹及连续界面心跳，不代替屏幕、触摸和无线功能验收。日常自动通道优先级仍为 USB > Wi-Fi > BLE。以后使用 OTA `.bin` 升级，不重复首次安装。

## 备份与恢复

- 默认位置：`%LOCALAPPDATA%\AI-bot\device-backups\tab5-<时间>-<随机标识>.bin`，配套 `.bin.json` 保存设备身份、大小及 SHA-256。备份可能含设备设置，**不上传仓库、日志或 Release**。
- 只有完整读取且与设备核验一致后才生成可恢复的 `.bin.json`。备份失败则停止写入。
- 写入或启动失败时，重新进入下载模式，选择同一设备，点“恢复原固件”，选择对应 `.bin.json`。工具检查 MAC 和备份哈希，再备份当前闪存，然后恢复原备份并核验。
- 恢复校验通过后短按 RESET，人工确认原系统启动。仅恢复 P4 闪存，不恢复 SD 卡、C6 固件或 eFuse。
- 工具不执行 `erase_flash`、`--force` 或 eFuse 写入。安全启动或闪存加密设备不受支持。已有 AI-bot 固件使用 OTA；旧分区使用单独的迁移流程。

## 构建与验证

先按 README 构建固件，再运行。输出文件必须不存在；版本从应用描述读取。

```powershell
python scripts/package-first-install.py --build firmware/build --output artifacts/TAB5-first-install-<version>.zip
python -m unittest discover -s tests -p test_first_install_package.py
```

生成器核验 ESP32-P4 checksum/SHA-256、项目名及分区 MD5，仅复制下列构建文件，其余位置初始化为 `0xFF`。不读取设备或设备备份。

| 内容 | 地址 | 约束 |
|---|---:|---|
| bootloader | `0x2000` | ESP32-P4 镜像 |
| partition table | `0x8000` | 固定 OTA 双分区布局 |
| otadata | `0x10000` | 空白 8 KiB |
| application | `0x20000` | `aibot_tab5`，最大 `0x6e0000` |

ZIP 含 `manifest.json`、完整 16 MiB `factory.bin`、说明及第三方许可；同时生成 `.zip.sha256`。NVS、PHY、备用 OTA 槽和 assets 分区为空。桥接只提取固定文件，不按 ZIP 中任意路径解压。

桥接继续在相邻 AI-bot 项目维护，沿用固定 esptool 4.9.1 及对应源码/许可打包流程。首次安装入口与 ESP8266 刷机窗口分离。

在 AI-bot 目录验证：

```powershell
dotnet build windows-app/AIBotBridge/AIBotBridge.csproj -c Release
dotnet run --project windows-app/AIBotBridge/AIBotBridge.csproj -c Release -- --self-test-tab5-install <首次安装ZIP绝对路径>
dotnet run --project windows-app/AIBotBridge/AIBotBridge.csproj -c Release -- --capture-tab5-install <截图目录>
```

发布前，用一台允许替换出厂固件的 TAB5 完成“下载模式 → 原固件备份 → 首次安装 → 启动检查 → USB 配对 → Wi-Fi 桥接 → BLE → 备份恢复”，记录硬件修订、P4/C6 组合、安装包哈希及实际交互。当前软件检查尚不构成这份真机记录。源码推送、标签及公开发布分别授权。

## English summary

Windows first installation is under **TAB5 connection → USB pairing → New device installation**. Connect USB-C with a data cable, hold RESET for about two seconds until the green LED flashes rapidly, release, refresh and explicitly select TAB5. Select the full ZIP, confirm replacement, and keep power connected during backup, write and verification. Briefly press RESET afterward, select the application port and check boot before pairing and configuring Wi-Fi.

The installer checks chip/revision, 16 MiB capacity and disabled security, preserves a verified full backup, then writes a clean image. Restore only accepts a verified backup for the same MAC. C6 firmware is untouched. Existing AI-bot installations use OTA or legacy migration. This local candidate still requires stock-device installation, pairing, wireless operation and recovery acceptance before release.
