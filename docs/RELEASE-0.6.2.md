# 0.6.2 修复版 / Patch release

本轮分别交付电脑桥接 **0.6.2**、ESP8266 **0.5.1**、TAB5 **0.2.150-ui**。从[完整下载中心](../DOWNLOADS.md)取得 Windows / Mac、两种固件及 TAB5 首刷包。书法和名画继续使用原有独立图库包。

## 升级顺序与变化

1. 先升级 Windows 桥接至 0.6.2。更新窗口使用真实小屏版本判断升级；旧固件没有版本字段时提供明确的手动准备入口。下载校验后仍需用户确认刷写。
2. 小屏通过 USB 升级至 0.5.1：先完整备份、再写入与校验。刷机窗口自动释放桥接占用的串口并保留设备身份。此后 USB 和已认证 Wi-Fi 均能上报固件版本。
3. TAB5 使用 0.2.150-ui **升级 ZIP**，在设备端确认安装。修复 Codex 页“改用 TAB5”与相机入口遮挡，录音及识别期间隐藏相机入口。只有首次安装才使用首刷 ZIP。

macOS 保持现有功能并同步版本，不新增 Windows 专用的统一更新窗口。各组件独立更新，不要求版本号相同。图库无需重新下载，电脑程序升级保留用户图库。

## 验证与已知边界

- 小屏修复代码已通过真实 USB 备份、写入校验、版本上报、Wi-Fi 回退与 USB 恢复；该轮使用本地 0.5.0 候选，不冒充正式 0.5.1 包已安装。
- TAB5 .150 的 Wi-Fi OTA 与精确镜像指纹、ota_0 / VALID 启动核验通过；原生 LVGL 布局预览及语音交互回归通过，物理屏幕按钮效果尚未人工确认。
- 蓝牙新增失败时的连接状态和实际等待时间记录。**这不是蓝牙断连修复**：已抓到 GATT 确认超时后软件重连，底层原因仍待定位。
- 构建、包校验与自动化测试不代表另一台电脑或 macOS 的交互安装验收。Mac 包为 Apple Silicon、ad-hoc 签名，未公证。
- 宣传视频为原生界面的固定演示数据；画面中的版本属于拍摄时示例，下载以完整下载中心为准。
- TAB5 发布镜像沿用已验收字节；配套源码构建命令为 `powershell -NoProfile -File scripts/build.ps1 -LocalArt -LocalVersion 0.2.150-ui`，以该显式版本覆盖源码中的历史默认值。源码包记录提交和镜像哈希，不宣称已验证逐字节可复现构建。

## English

This release independently delivers bridge **0.6.2**, ESP8266 **0.5.1** and TAB5 **0.2.150-ui**. The [complete download center](../DOWNLOADS.en.md) includes both desktop platforms, both firmware families, TAB5 first-install/upgrade packages and the unchanged optional art collections.

Upgrade the Windows bridge first. Connected legacy ESP8266 firmware without identity can explicitly prepare a manual USB upgrade, with complete backup and write verification. The flasher releases the bridge's serial connection and retains device identity; ESP8266 0.5.1 reports its component version over USB and authenticated Wi-Fi. TAB5 .150 repairs the voice-source switch/camera overlap and hides the camera during voice preparation, recording and recognition. Existing installations use the upgrade ZIP and confirm on the device; the first-install ZIP is only for initial setup. macOS keeps existing functionality with synchronized version metadata.

The ESP8266 repair was hardware-tested using a local 0.5.0 candidate, separately from installing the final 0.5.1 package. TAB5 .150 passed Wi-Fi OTA and exact-image VALID boot verification plus native layout/voice regressions; physical-screen button appearance is not yet manually confirmed. BLE changes add diagnostics only: confirmation timeouts and software reconnects are observed, but the underlying cause remains open. Build/package tests do not replace another computer's installation acceptance; macOS arm64 is ad-hoc signed and not notarized. The film uses fixed native-interface demonstration data and historical example versions.

The TAB5 release preserves the tested image bytes. Build its source with `powershell -NoProfile -File scripts/build.ps1 -LocalArt -LocalVersion 0.2.150-ui`, explicitly overriding the historical source default. The source manifest records the commit and image hash; byte-for-byte build reproducibility has not been verified.
