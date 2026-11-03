# 0.6.16 桥接与两款固件发布 / Desktop and device release

本次四项交付：Windows 0.6.16、Apple Silicon Mac 0.6.16、ESP8266 0.5.8、TAB5 0.2.303-ui。桥接合并为一个 Release，两种固件独立编号；图库沿用既有版本。构建源码候选为 `28ff9cf474ec74d93beb8f5041f4e02d8d7d751d`，后续文档提交不改变原包身份。

## 变化

- Windows 增加微软本机语音识别选项，保留既有输入方式；实际识别准确率取决于系统识别器。DeepSeek 和 GLM 已使用金额采用对应授权账户来源，失败时保留最近可用数据。
- 两端桥接改善 ESP8266 USB 资源同步；Mac 避免资源传输阻塞菜单，股票镜像使用对应字形和布局。小屏固件修复股票绘制栈溢出，采用用户验收的较细 I、页面轻微变暗恢复和行情局部刷新。
- TAB5 近期生日限定为今天至未来 30 天，告警详情关联实际原因，恢复后移除对应告警。Windows 的 Codex 恢复以真实通道成功为依据；Mac 继续保留缺少业务证据时的未确认状态。

## 实际包和验证范围

[小屏股票字形预览](assets/screens/esp8266-stock-058-preview.png) 使用合成代码和价格，来自固件字形的离屏渲染，不是设备摄影或账户数据。字形及数字清晰度已查看；本次小屏实机结论见下表。Preview: synthetic symbols/prices rendered with the device glyphs, not hardware photography or account data.

| 交付 | SHA-256 | 证据 |
| --- | --- | --- |
| Windows 安装程序 | `99a31ae0be8e393c6b3087d08953fdd3723c294fb3fa22ce9e4d9eba3107855d` | [原包安装升级](development/WINDOWS-0616-PACKAGE-ACCEPTANCE-20261103.md)：公开 0.6.14 → 0.6.16 静默升级、111 文件一致、20 资料文件摘要保留、已安装程序正常用户会话只读启动退出码 0；源代码及便携包公共回归已通过。 |
| Mac ZIP | `8a0d7b72a6a1d6228bd67a0138f931c4e91686f0db27b854203dc2b14b43afb0` | [原包验收及沿用范围](development/MAC-RELEASE-0616-HANDOFF-20261103.md)：本机安装、启动、重启、资料摘要和签名检查通过；433 项 CI 测试与 29,068 项隔离检查通过。未变更的小屏功能及存储链路沿用既有验收。 |
| ESP8266 应用镜像（ZIP 内） | `ce5a66cf9cce9a06ae2dda2c3decd79287ea29b1f8ca7ee78b6785f635841ed3` | 最终 0.5.8 授权刷入、独立回读、版本心跳、亮屏通过；原轮播配置已恢复。 |
| TAB5 应用镜像（两种 ZIP 内） | `c79abf03174dd35ca29922ee41b66c2826416afa99bdaf4f3a68232e03543732` | 首刷与升级包应用一致；已授权升级并核对 VALID 启动。独立重建完成，除构建元数据和校验外应用字节一致。 |

Windows 安装包未签名；Mac 为 ad-hoc 签名、未公证，替换应用后仍可能需要系统授权。Mac 此次安装前为开发版，公开旧版直接升级及当前候选真实配对／草稿解密未单独复测；钥匙串、配对、加密存储和打包脚本相对公开 0.6.15 无变化，沿用相应既有证据，不宣称新增实测。Windows 安装检查不代表重新验证所有托盘、权限和硬件路径。

TAB5 的真实生日 30/31 天边界及真实 Codex 故障详情仍待条件，离线测试已覆盖逻辑；设备已确认旧 Codex 告警消失。TAB5 源码快照来自已发布源码及记录的补丁，`sourceCommit` 为 null；重建文件的时间、SDK 标识、ELF 和尾部校验不同，因此不称逐字节可复现。分区仅余 1,312 字节。发布保留已验收原镜像，不要求用户重复刷写已运行的相同镜像。

## English

Four deliverables: Windows and Apple Silicon Mac desktop 0.6.16 share one Release, while ESP8266 0.5.8 and TAB5 0.2.303-ui have independent Releases. Promote the accepted packages from source `28ff9cf474ec74d93beb8f5041f4e02d8d7d751d` without rebuilding.

Windows adds native Microsoft speech recognition and account spending support. Desktop bridges improve ESP USB synchronization and Mac menu responsiveness; the ESP update refines stock typography and transitions and avoids unnecessary whole-screen redraws and the observed stack overflow. TAB5 limits upcoming birthdays to today through 30 days and improves alert details and recovery.

The exact Windows setup passes published-old-version native upgrade, installed-file identity, protected-file hashes and installed CLI startup. The exact Mac ZIP passes user-confirmed installation/start/restart and file-preservation checks, 433 CI tests and 29,068 isolated checks; unchanged storage paths reuse existing evidence. Direct public-old-version Mac upgrade and current real pairing/draft decryption were not separately retested. Windows is unsigned; Mac is ad-hoc signed and not notarized. Real TAB5 birthday boundaries and failure details remain unverified. Its reconstructed source rebuild differs only in build metadata and integrity fields, and the application partition has only 1,312 bytes free. These limitations are retained rather than marked as new acceptance.
