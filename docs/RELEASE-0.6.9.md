# AI-bot v0.6.9 / TAB5 0.2.217-ui

2026-10-17 正式发布 Windows **0.6.9** 与 TAB5 **0.2.217-ui**。Mac 保留 **0.6.7**，ESP8266 保留 **0.5.3**，现有标准／高清图库保持原版。

[Windows 发布](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.6.9) · [TAB5 发布](https://github.com/yaoyouzhong/AI-bot/releases/tag/tab5-v0.2.217-ui) · [完整下载](../DOWNLOADS.md) · [安装指南](INSTALL.zh.md) · [本次收尾](development/RELEASE-069-217-CLOSEOUT-20261017.md)

## 本轮变化

- TAB5「应用 → 控制」调节电脑音量、静音、锁屏与最多四个快捷启动项。音量数字居中位于静音按钮上方；静音显示放大的扬声器加斜杠。程序／文件夹显示图标，在电脑设备中心配置。
- 锁屏和解锁后同步实际状态，稳定轮询避免控件每两秒灰亮闪动。新图标缓冲移入 PSRAM，修复早期 213 候选启动蓝屏回退。
- 音乐页提供当前匹配 Windows 媒体会话支持的播放操作；执行前核对来源、歌名与歌手。控制复用配对认证、加密 RPC 与去重；设备不能提交路径、命令或 URL。
- 最多保存 **10 个 Wi-Fi**；迁移旧记录、备注与优先项，空列表保持为空。连接失败时做有界网关／电脑诊断，不改网络设置。路由器已连接与电脑桥接鉴权通过分别显示。
- 包含 210 之后的横屏 180° 自动翻转、方向锁定、触控跟随、屏幕声音设置布局及倒计时单位／导航修订。

![电脑控制原生预览，固定演示数据](assets/screens/tab5-computer-control.png)

## 下载、兼容与身份

先更新 Windows 桥接，再按设备选择 TAB5 首刷或升级包。已有 AI-bot 解压升级 ZIP，选择 `aibot_tab5.bin`，保留同目录实际 notes sidecar；出厂系统使用完整首刷 ZIP，两种格式不能互换。完整电脑控制与十网络管理需要 Windows 0.6.9；Mac 不提供本轮控制。升级保留配对、网络、图库及展示设置，TAB5 不执行 ESP8266 轮播恢复。

两枚带注释组件标签指向 `9a570d4b443b1354306d753afcca9fe8f0fbd387`。桥接编译提交 `a81096fa1a09b8e6b3d6d46572e8199df3dd95d5`；最终提交只补齐已打入包的 TAB5 双语日志，907 个归档源码文件与最终源码逐字节一致。保留已验收包字节，不为文档或 CI 重新打包。

TAB5 独立源码 `d31a601289dbfc4edf8da82d0f91a7f926dbd090`；首刷／升级含同一 7,089,088 字节应用。应用 SHA-256 `bb3a94f9e1862c200a81d5176852966663b0d456559f8f59e8b1eed0714d3e96`，ELF SHA-256 `a6f756c7270fbdd702333d0f9bd74f50f88f6de6b4227cb5085324115df5f4df`。[开发者源码与构建](development/TAB5-SOURCE.md)

| 文件 | SHA-256 |
| --- | --- |
| Windows 安装器 | `8d08b50b9a5b023f100ae9469ac45cba51d7e49a7e55386aae35a445803ba871` |
| Windows 便携 ZIP | `02d70a2c24c756d7e40d5256ca3ba4d4c3c3bd0a49ed7e04a14ff98cf6f59641` |
| Windows 源码 ZIP | `0993e0ec65023131785025012fe30fc24fa806a1cc70f613f4167f98d0ddfdb5` |
| TAB5 首刷 ZIP | `911608f6c450d2d04a6500a84424216bee930338d335361ed6b36edf2d44c280` |
| TAB5 升级 ZIP | `8d748679ccd6aaca6c009eefcd711ce344ee7056c80b2bcdf1ab7b27a48e85f3` |
| TAB5 源码 ZIP | `0efbc045cdb5b35e69a64ee1b4b13f756639be7fd7fec5c0f3746cefe1b73176` |

两套 Release 均附许可证、第三方声明、完整 SHA-256 清单；旧客户端兼容别名与标准包同字节。

## 验证与限制

Windows Release 构建零警告／零错误，源码构建与最终 ZIP 解压后完整公共自测均通过；覆盖真实加密 RPC 的隔离电脑控制测试。安装器编译、自检和本机运行库检测通过，便携包 82 项文件哈希及离线刷机工具检查通过。正常资源管理器桌面上下文 `dotnet run -- --status-once` exit 0、返回有效 JSON，保留资料目录保护。**本轮未执行全新安装或替换正在运行的桥接**。

包边界／组件版本／更新说明共 22 项，分发许可 11 项，设置布局及 24 个关闭保存场景通过。最终桥接接受 6–10 个网络、拒绝第 11 个。TAB5 首刷格式 5 项、完整原生 LVGL、网络迁移／诊断／连接／RPC／OTA／旋转等定向回归通过；实际 217 应用经过最终 .NET 认证压缩编码及原生解码、碎片与损坏拒绝检查。首刷的 NVS、PHY、备用 OTA 与艺术分区为空，不混入设备凭据。932 项源码清单与原始 722 项固件构建输入核对通过。

[211](development/TAB5-211-ACCEPTANCE-20261017.md)记录横屏翻转、触控与设置布局；[215](development/TAB5-CONTROL-215-ACCEPTANCE-20261017.md)记录音量、静音、启动图标及打开、锁屏状态与无闪动。临时启动项已恢复为空。精确 217 镜像启动为 `ota_0 VALID`；重连电脑当前 Wi-Fi 后鉴权 HTTP 恢复，用户拔掉 USB 确认桥接正常且音量可控制。本轮发布未再次刷机。

十网络容量为合成输入与迁移测试，未向真机写入十个临时网络。Wi-Fi 连接恢复未确定网卡、驱动或接入点根因。独立环境逐字节重建、完整三通道整包 OTA、全部页面四角触控及长期稳定性均不扩大为已验收。截图／视频为隔离原生界面，不是硬件摄影；主观音频试听未验。

## English

Windows 0.6.9 and TAB5 0.2.217-ui were published on October 17. Mac remains 0.6.7, ESP8266 0.5.3, and artwork archives are unchanged. New PC volume/mute/lock, real lock state, four local launchers with icons, matching-session media commands and ten saved networks use the authenticated protocol. TAB5 also includes post-210 rotation/touch/settings/navigation changes and PSRAM icon buffers.

Update Windows first. Factory installations use the full ZIP; existing AI-bot uses the extracted application BIN with its matching notes. Preserve pairing, Wi-Fi, collections and display preferences. Annotated tags, source commits and exact package/application hashes are recorded above. Accepted bytes were preserved.

Release builds, normal-profile status, final ZIP self-tests, installer self-test/runtime detection, package/source/license checks, network-capacity/migration and native/OTA/rotation regressions passed. This release did not install a new Windows runtime or flash the device again. Earlier physical acceptance covers rotation, controls, launcher icons/opening, lock state, steady rendering and unplugged-USB Wi-Fi volume control; temporary launchers were cleared. The exact 217 image booted ota_0 VALID. Ten synthetic networks are not ten real saved test networks. The NIC/AP root cause, independent byte-identical reproduction, all-channel full OTA and long-running reliability remain unverified. Media uses isolated native fixtures; subjective audio listening remains unverified. Public download and media verification are recorded separately in the linked closeout.
