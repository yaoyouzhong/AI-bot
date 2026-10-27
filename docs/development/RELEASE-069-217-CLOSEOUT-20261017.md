# 0.6.9 / 217 发布收尾（2026-10-17）

状态：正式发布与收尾验证完成。两套稳定 Release、全部中英文下载入口、安装说明、相关截图、影片与来源许可已同步；公开下载及主页原生播放器均有实际验证证据。

## 发布身份

- 远端 `https://github.com/yaoyouzhong/AI-bot.git`，目标 `main`。
- [Windows 0.6.9](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.6.9)：2026-10-17 12:44:46 UTC 发布；[TAB5 217](https://github.com/yaoyouzhong/AI-bot/releases/tag/tab5-v0.2.217-ui)：12:44:43 UTC 发布。
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
| 文档提交 CI | [39034819677](https://github.com/yaoyouzhong/AI-bot/actions/runs/39034819677) 三平台检查成功；附件关联由浏览器保存于 `c6311fc` |
| 公开媒体 | 6 个 Release 媒体附件 HEAD／首尾 Range 匹配；主页新附件 GET Range 206、video/mp4，首尾字节匹配本地。附件重定向使用 GET 签名，不能用重定向后的 HEAD 403 代替播放判断 |
| 公开页面与原生播放器 | 公开主页显示 Windows 0.6.9／Mac 0.6.7／TAB5 217；实际点击播放 0→27.25 秒，81.41 秒暂停，右方向键跳至 82.57 秒且保持暂停，继续至 103.51 秒，最终到达 116 秒 ended；无媒体错误 |
| 公开文档与截图 | 18 个主页／下载／安装／功能／媒体／来源及新截图文件，公开 raw 字节与已提交 Git 内容一致；本地相关文档 446 项相对文件链接存在 |
| 最终媒体同步 CI | [39035841840](https://github.com/yaoyouzhong/AI-bot/actions/runs/39035841840) 成功（提交 `09653d5`） |

视频附件新地址 `https://github.com/user-attachments/assets/f8aefacb-cf94-40c2-9b07-8be8b348a03b`，重新上传本次 9,098,411 字节导出，未沿用旧附件。媒体 MP4、工程与三种封面随 Windows Release 分发，另附 VIDEO-SHA256SUMS.txt；来源、署名及哈希见 PRODUCT_VIDEO、SCREENSHOTS、THIRD_PARTY_NOTICES 与 materials.json。主观试听未验；原生预览不是硬件摄影。

本机详细证据保留于 `artifacts/release-069-217-20261017`、`artifacts/publish-069-217-20261017` 和 `artifacts/product-intro-release-069-217-20261017`，不把私人状态 JSON、测试配对数据或配置纳入公开仓库。

## 实机边界

沿用用户已确认的音量、静音、启动图标及打开、锁屏／解锁、无闪动与拔掉 USB 后 Wi-Fi 音量控制；临时启动项已恢复为空。精确 217 镜像 `ota_0 VALID`。本轮发布不重新安装桥接、不刷机、不改变用户展示设置。十网络以合成输入／迁移回归验证，未写入十个真实测试网络。Wi-Fi 恢复未确定 NIC／驱动／AP 根因；独立环境字节重建、完整三通道 OTA、全页面四角触控和长期稳定性不宣称完成。

## English

Both stable releases are published under annotated tags pointing to the exact source commit above. Accepted binaries are preserved; Mac, ESP8266 and artwork retain their public versions. Build/status, final-package/native/source/license regressions passed. Fourteen base assets match local hashes through server digests and bounded public HEAD/range checks; this is not a second complete remote download. Public native playback advanced, paused, sought, resumed and reached the 116-second end without media errors. Eighteen public document/image files match committed Git bytes; 446 relative file links exist. Updated native media, source and checksums retain their licenses. Hardware acceptance and unverified installation, long-term/root-cause/reproduction/OTA boundaries remain explicit.

## 附件关联补验

首次上传形成的临时附件未完成关联，公开检查为 404；未将此状态计为成功。刷新旧编辑器解决 main 提交冲突后，从最新主页重新上传同一影片，并经 GitHub 正常保存于 `c6311fcabb055e9f638bed476cbe4e9080457686`。该新地址实际 GET／Range 返回 206、video/mp4，匹配本地首尾字节；之后完成公开主页播放验收。HEAD 重定向到仅为 GET 签名的存储地址会返回 403，因此使用真实 GET Range 与浏览器播放分别验证，没有取消任何认证或证书检查。

本机 `public-player-checks.json`、`public-native-attachment.json`、`public-payload-bounded-checks.json`、`public-media-ranges-only.json`、`public-docs-and-screenshots.json` 与 `public-homepage-video.png` 保留结果；不包含附件的临时签名 URL。最终中英文媒体地址、许可证摘要及发布元数据一致。

The initial unattached upload returned 404 and was not accepted. After refreshing the stale editor, the same video was uploaded and saved through normal GitHub UI. Public GET Range checks and real homepage playback then passed. A redirect signed for GET does not validate with HEAD; no authentication or TLS protections were bypassed.
