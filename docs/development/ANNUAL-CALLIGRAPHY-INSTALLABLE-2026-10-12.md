# 书法年度精选集本地作品包验收

2026 年 10 月 12 日交付 366 件独立作品的本地书法精选集：横屏和竖屏各 1,901 页，简介、欣赏资料、正文或明确标注的参考文本、来源和许可随同一 ZIP 更新。作品身份及年度顺序保持既定 366 件清单。当前交付仅为本地作品包、阅读接入源码与隔离验证，不安装到用户图库或部署程序和设备。

## 本地安装

[最终作品包 2026.10.12.32](../../artifacts/development/annual-calligraphy-installable-20261012/delivery/AI-bot-DailyCalligraphy-2026.10.12.32.zip) · [SHA256 校验](../../artifacts/development/annual-calligraphy-installable-20261012/delivery/AI-bot-DailyCalligraphy-2026.10.12.32.zip.sha256)

文件大小 976,429,851 字节（931.20 MiB）；SHA256 为 `b977616f600a807638c77eb13f87b4f0a8c9b2b63dc073f32867a06737b004ab`。[本地安装说明](../../artifacts/development/annual-calligraphy-installable-20261012/delivery/INSTALL.zh.md)。

在支持导读随包更新的 Windows AI-bot 中，进入“检查更新 → 屏保图库 → 导入图库 ZIP…”，选择作品包，无需解压。作品包按书法类别原子导入，图片、资料和正文一起更新；导入失败或取消保留已有图库。包内包含 LICENSE、THIRD_PARTY_NOTICES.md 和逐件来源与权利记录。

包保持 schemaVersion=2。已支持随包导读的旧桥接可安装，7 篇超长正文显示完整升级提示；完整 Windows 阅读及设备长文分段服务需要本次候选桥接，设备阅读需要支持 guideVersion=3 的新固件。正式固件尚未构建或部署，不能将本地验证作为用户当前程序或设备已经升级的证据。

## 横竖屏排版

304 件既有作品保留横竖屏原生页。修正 6 处与审定资料不同的标题；金农漆书格言移除一张空白局部图，两方向各保留 4 页，其中整幅 JPEG 保持原字节。

62 件新增作品由 508 张已归档原生摄影图生成，两方向各 728 页。册页保留所属整册，必要时展开右页和左页，长卷按原生分页及连续重叠展示。摄影色卡按逐件审核的边界裁去，保留文字和印章；不把多个原图拼成伪造整卷。最后复核修正《农桑辑要》六屏右缘残留色卡。全部 3,802 张 JPEG 的存储尺寸为 1280×720，竖屏按 720×1280 排版后预旋转编码，符合现有设备及导入器格式；单图不超过 1 MiB。

[新增作品视觉记录](../../artifacts/development/annual-calligraphy-installable-20261012/new-layouts/visual-review.json) · [既有作品复核](../../artifacts/development/annual-calligraphy-installable-20261012/existing-layout-review/REVIEW.md)

![书谱横竖屏排版](../../artifacts/development/annual-calligraphy-installable-20261012/new-layouts/focus-expansion-npm-shupu-01.jpg)

![农桑辑要末页横竖屏排版](../../artifacts/development/annual-calligraphy-installable-20261012/new-layouts/focus-annual-npm-he-liuping-17.jpg)

## 七篇长文

| 作品 | 完整参考正文 UTF8 字节 | 阅读段数 |
| --- | ---: | ---: |
| 道因法师碑拓本 | 8,653 | 3 |
| 行书居庸赋卷 | 13,383 | 4 |
| 书谱 | 13,710 | 4 |
| 自书诗稿册 | 13,829 | 4 |
| 颜氏家廟碑拓本 | 10,471 | 3 |
| 佛顶尊胜陀罗尼经幢拓本 | 12,610 | 4 |
| 楷书离骚九歌卷 | 14,618 | 4 |

7 篇完整参考正文随包保留，共 26 段。分段按 Unicode 字符边界进行，每段最多 4,095 个 UTF8 字节；每段及全文均校验 SHA256。设备“原文”页可上一段、下一段，边界按钮禁用。TF 存储完整段集并校验后读回，支持离线阅读。打开阅读页时复制独立快照，使在途作品切换不能替换用户正在阅读的正文；快照和段缓冲显式使用 PSRAM，分配失败保留最近的有效资料。

“完整参考正文”不表示已经逐字精校本件墨迹。道因阙五字、居庸赋组字及题识后跋、书谱和诗稿的馆方主文范围、颜家庙刊刻尾记、佛顶经的刻录起止与咒音题款、离骚和九歌的篇序与待校款跋均保留各自说明。两处未编码组字仍用组合描述，不伪造 Unicode 字。设备显示的馆方介绍、欣赏资料与原文说明保持区分，详见包内 guides.zh.json。

## 验收证据与边界

- [包文件验证](../../artifacts/development/annual-calligraphy-installable-20261012/package-validation-final.json)：366 个独立作品身份及年度顺序、3,802 张 JPEG 的解码/尺寸/大小/哈希、所有包成员及 ZIP 校验、7 篇完整文本与冻结资料一致。
- [正式消费者隔离导入](../../artifacts/development/annual-calligraphy-installable-20261012/annual-consumer-final/annual-pack-verification.json)：通过生产 GalleryPack.Install 导入最终 ZIP，验证 366 件 Windows 全文读取、全部横竖屏帧的服务响应、7 篇 v1/v2 升级提示和 v3 段拼接哈希、非法及过期请求拒绝。
- Windows 候选 Release 编译 0 警告、0 错误；导读/图库原子安装回归通过；传输回归 750 次认证请求通过。WiFi 使用本机 HTTP，USB/BLE 使用协议模拟，不替代硬件验收。
- Windows 浏览界面导航、全文、全屏、缩放、拖动、错误恢复，以及正常/最小窗口的 100%、150%、200% 缩放回归通过；该界面交互用测试图库，最终包的全文及所有页另由正式消费者验证。
- [最终包长文 native 与 LVGL 验证](../../artifacts/development/annual-calligraphy-installable-20261012/long-reader-final/summary.json)：7 篇完整分页、三个通道 facade 共 21 个用例、TF 离线、坏段/取消、在途缓存替换及阅读快照生命周期；首末段两方向共 28 张截图和四朝向实际触摸回归。测试为本机 native/LVGL 模拟，未连接真实设备。
- [受保护资料与字库复核](../../artifacts/development/annual-calligraphy-installable-20261012/frozen-input-verification.json)：原主库元数据、年度冻结清单/资料、字库及已有固件镜像 SHA256 保持不变；本包所需 6,978 个显示字符均有字形，缺字 0。

默认 Release 输出目录被正在运行的用户桥接占用，改用独立输出完成编译，未停止用户程序。正常 `--status-once` 被 AppProfileGuard 在工具启动环境中拒绝，未绕过保护，因此正常用户启动检查未通过。没有正式固件构建、硬件运行/TF 实物验收、用户图库安装、桥接或设备部署、Git 推送、标签和公开发布。历史 .31 预检包保存在 preflight-31，仅 .32 为交付版本。

![书谱竖屏阅读模拟预览](../../artifacts/development/annual-calligraphy-installable-20261012/long-reader-final/annual-long-2-portrait-start-logical.png)

![离骚九歌末段横屏阅读模拟预览](../../artifacts/development/annual-calligraphy-installable-20261012/long-reader-final/annual-long-6-landscape-end.png)

## English

The local calligraphy edition contains 366 independent works, with 1,901 landscape and 1,901 portrait pages. Images, source-qualified reading material, complete references, provenance and rights records are installed atomically from the schema 2 ZIP. Import it through Check for updates → Artwork collections → Import ZIP; extraction is unnecessary.

Seven complete references are retained as 26 bounded Unicode-safe segments. Each segment and the reassembled text are checked by SHA256. Windows full reading and device v3 service require the local candidate bridge; device paging requires pending guideVersion=3 firmware. Older guide-capable bridges can import the package and show an upgrade notice for these long texts. Reference completeness does not imply character-level collation against the individual manuscript, and per-work limitations remain in guides.zh.json.

The final package is validated through isolated production import, all image decoding and hashes, native worker/storage tests and LVGL UI touch tests. Windows compilation is clean; 750 authenticated transport regression requests and browser layout checks pass. HTTP runs on loopback and USB/BLE/native facades are simulations. Normal startup remains unverified because AppProfileGuard rejected the tool launch environment. No user-gallery installation, formal firmware build, hardware acceptance, deployment, push, tag or public release occurred.
