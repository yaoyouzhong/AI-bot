# Windows 0.6.9 / TAB5 0.2.217-ui 发布准备

2026-10-17 本地候选准备，尚未公开发布。当前正式版仍为 Windows 0.6.8、TAB5 0.2.210-ui；Mac 保留 0.6.7、ESP8266 保留 0.5.3，标准与高清图库不重新打包。

## 本轮范围

- Windows：TAB5 电脑音量、静音、锁屏与最多四个快捷启动项；程序／文件夹显示系统图标。设置由设备中心或托盘进入，保存配置不执行启动。
- 音乐控制核对当前系统媒体会话和展示歌曲；不支持的控制禁用。操作复用配对认证、加密 RPC 和去重，不接受设备提交路径、命令或 URL。
- TAB5：音量数字与静音图标放大居中，实际锁屏状态与稳定轮询；新图标缓冲移入 PSRAM，修复早期 213 候选的启动蓝屏回退。
- 保存网络由五个扩展到十个，迁移旧记录、备注与优先项；空列表保持为空。新增失败时的网关／电脑路径诊断，不修改网络设置。
- 包含已验收的横屏 180° 自动翻转、方向锁定、触控跟随与屏幕声音设置布局，以及倒计时单位与导航修订。

## 已有实机证据

[211](development/TAB5-211-ACCEPTANCE-20261017.md)记录显示翻转、触控及设置布局；[215](development/TAB5-CONTROL-215-ACCEPTANCE-20261017.md)记录电脑控制、启动图标与打开、锁屏状态和闪动修复。临时启动项验证后恢复为空。

217 镜像 7,089,088 字节，应用 SHA-256 `bb3a94f9e1862c200a81d5176852966663b0d456559f8f59e8b1eed0714d3e96`，ELF SHA-256 `a6f756c7270fbdd702333d0f9bd74f50f88f6de6b4227cb5085324115df5f4df`，精确启动核验为 `ota_0 VALID`。重连电脑当前 Wi-Fi 后鉴权 HTTP 恢复为 200，用户拔掉 USB 后确认桥接正常、音量可控制。

十网络容量与迁移经过测试，实际已有网络保留；未向真机写入十个临时网络。连接恢复证明本次故障解除，尚未确定异常发生在网卡、驱动或接入点，不能宣称已永久修复。

## 候选包与验证

桥接候选使用独立源码快照构建，输出 Windows 安装器、便携 ZIP、源码 ZIP、许可证与 SHA-256。TAB5 保留上述已验收镜像字节，生成首刷、升级和源码快照；两种刷机包内应用必须一致，首刷不混入设备备份或 NVS 凭据。

完整电脑控制与十网络管理需要 Windows 0.6.9。Mac 不提供本轮控制。升级保留配对、网络、图库与展示设置；TAB5 不执行 ESP8266 的轮播恢复。

打包完成后在本节登记检查、精确交付哈希与安装验证结果。未完成的验证不得写为通过。独立环境逐字节重建、全部三通道完整 OTA、所有页面四角触控与长期稳定性不在已有实机结论中。

公开发布前确认具体源码提交、标签和文件；公开后再更新下载索引、主页、截图、视频与安装入口，并执行[发布收尾清单](RELEASE-CHECKLIST.md)。

## English

Local preparation on October 17 targets Windows 0.6.9 and TAB5 0.2.217-ui. Published versions remain Windows 0.6.8 and TAB5 0.2.210-ui; Mac stays on 0.6.7, ESP8266 on 0.5.3, and artwork packages remain unchanged.

This candidate adds authenticated PC volume/mute/lock and four local launchers with icons, matching-session music controls, real lock state, steady polling, PSRAM icon buffers, ten saved networks with migration, and bounded failure-path diagnostics. It includes the post-210 landscape rotation, touch/settings and countdown/navigation changes.

Existing hardware acceptance covers rotation, controls, launcher icons/opening, lock state and steady rendering. The exact 217 application and ELF hashes above booted as ota_0 VALID. After reconnecting the existing PC Wi-Fi association, authenticated traffic recovered and the user confirmed volume control with USB unplugged. This does not establish the underlying NIC/AP fault or long-term reliability. Ten-slot storage/migration tests do not mean ten test networks were written to the device.

Keep the accepted firmware bytes. Validate Windows installer/portable/source packages, matching TAB5 first-install/upgrade applications, clean source identity, licenses and checksums separately. Full new controls and ten-network management require Windows 0.6.9. Preserve user settings and do not apply ESP8266 carousel reset to TAB5. Public release, download/media refresh and long-running acceptance remain separate stages.
