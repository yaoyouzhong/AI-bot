# 0.6.9 / 217 发布收尾（2026-10-17）

状态：两套稳定 Release 已发布，包、下载入口、文档与媒体已更新；公开主页与播放器正在本轮补验。最终结果在本页继续登记。

## 发布身份

- 远端 `https://github.com/yaoyouzhong/AI-bot.git`，目标 `main`。
- [Windows 0.6.9](https://github.com/yaoyouzhong/AI-bot/releases/tag/bridge-v0.6.9)：2026-10-17 12:44:46 UTC 发布；[TAB5 217](https://github.com/yaoyouzhong/AI-bot/releases/tag/tab5-v0.2.217-ui)：12:44:43 UTC 发布。
- 两枚带注释标签指向 `9a570d4b443b1354306d753afcca9fe8f0fbd387`。桥接编译提交 `a81096f`；后续提交只补齐包内已有日志，907 项归档源码与发布源码一致。
- TAB5 独立源码 `d31a601289dbfc4edf8da82d0f91a7f926dbd090`。实际应用／ELF／源码与六项交付包 SHA-256 见[本版说明](../RELEASE-0.6.9.md)。已验收包字节保持不变。
- Mac 0.6.7、ESP8266 0.5.3 与全部图库保留；未发布 CI 生成的 Mac 0.6.9。

## 验证记录

| 项目 | 结果 |
| --- | --- |
| 发布代码 CI | [39032960978](https://github.com/yaoyouzhong/AI-bot/actions/runs/39032960978) 成功 |
| 组件标签工作流 | [bridge](https://github.com/yaoyouzhong/AI-bot/actions/runs/39032974033)、[TAB5](https://github.com/yaoyouzhong/AI-bot/actions/runs/39032974122) 均成功 |
| 本地完整准备 | Windows 构建、正常用户 status、最终 ZIP 自检／安装器自检、包与源码、原生 UI／网络／旋转／OTA 编解码均通过；细节见本版说明 |
| 发布资产 | 两套各 7 个基础附件，正式公开前核对名称、大小与 GitHub SHA-256 digest；实际兼容别名同字节 |
| 公开下载 | 14 个基础附件 HEAD 与首尾最多各 4 KiB Range 均匹配本地；不是远端整包重新下载校验 |
| 索引与许可 | 完整下载索引 refresh／sync／check；13 项索引、6 项组件测试；分发许可证据通过 |
| 截图与视频 | 8 张相关原生截图；116 秒／3,480 帧／15 镜头／35 音效、232 联系帧、14 切点及三封面已审阅；解码、字体图像及定位／音频结构通过 |
| 公开页面与原生播放器 | 本轮补验中，待登记 |

视频附件新地址 `https://github.com/user-attachments/assets/d6130fef-1c86-4e4c-b976-43d941d5f403`，重新上传本次 9,098,411 字节导出，未沿用旧附件。媒体 MP4、工程与三种封面随 Windows Release 分发，另附 VIDEO-SHA256SUMS.txt；来源、署名及哈希见 PRODUCT_VIDEO、SCREENSHOTS、THIRD_PARTY_NOTICES 与 materials.json。主观试听未验；原生预览不是硬件摄影。

本机详细证据保留于 `artifacts/release-069-217-20261017`、`artifacts/publish-069-217-20261017` 和 `artifacts/product-intro-release-069-217-20261017`，不把私人状态 JSON、测试配对数据或配置纳入公开仓库。

## 实机边界

沿用用户已确认的音量、静音、启动图标及打开、锁屏／解锁、无闪动与拔掉 USB 后 Wi-Fi 音量控制；临时启动项已恢复为空。精确 217 镜像 `ota_0 VALID`。本轮发布不重新安装桥接、不刷机、不改变用户展示设置。十网络以合成输入／迁移回归验证，未写入十个真实测试网络。Wi-Fi 恢复未确定 NIC／驱动／AP 根因；独立环境字节重建、完整三通道 OTA、全页面四角触控和长期稳定性不宣称完成。

## English

Both stable releases are published under annotated tags pointing to the exact source commit above. Accepted binaries are preserved; Mac, ESP8266 and artwork retain their public versions. Build/status, final-package/native/source/license regressions passed. Fourteen base assets match local hashes through server digests and bounded public HEAD/range checks; this is not a second complete remote download. Public page/player checks are being recorded in this closeout. Updated native media, source and checksums retain their licenses. Hardware acceptance and unverified installation, long-term/root-cause/reproduction/OTA boundaries remain explicit.
