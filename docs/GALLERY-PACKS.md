# 图库包 / Gallery packs

当前桥接 **0.6.7**、TAB5 **0.2.205-ui**。标准名画沿用 **2026.10.14.1**；标准书法更新为 **2026.10.15.1**；新增两套 **2026.10.16 高清完整包**。两类各 366 件独立作品，分页和方向不重复计数。[标准包下载](../DOWNLOADS.md) · [高清包分段续传、合并与安装](GALLERY-HD.md)

| 类别 | 标准版图片 | 完整包 | 用途 |
| --- | ---: | --- | --- |
| 每日名画 | 826（413 页，横竖各一张） | 标准版 2026.10.14.1；高清版 2026.10.16 | TAB5 使用适配展示图，电脑可选高清 |
| 每日书法 | 3,802（1,901 页，横竖各一张） | 标准版 2026.10.15.1；高清版 2026.10.16 | 本版修正全部横屏标题、作者及年代，补齐 32 件年代，竖屏字节保留 |

## 下载、浏览与导入

1. 只需设备展示图，下载标准 ZIP；希望电脑细看作品，下载高清完整包。两版均含作品和导读，高清包已带标准图，任选一版即可。高清原作不发送给 TAB5。
2. Windows **设备中心 → 屏保图库 → 安装图库包…**，直接选择完整 ZIP，无需解压。升级电脑软件保留图库；导入失败或取消保留旧内容，更新某类不改变另一类。
3. 点击 **浏览作品**，按标题、作者、日期或拼音查找。图片与阅读区独立；左右键循环翻页，I 展开阅读，F 全屏，Esc 返回。全屏可滚轮缩放、拖动、恢复适合屏幕。
4. TAB5 **设置 → 屏幕与声音**选择每日名画或每日书法并预览。横屏资料位于作品左侧，竖屏保留简化信息。

![当前电脑高清作品浏览](assets/screens/gallery-browser.png)

图片、简介、权威欣赏资料、已核实原文或明确标注的参考文本与来源一起更新。欣赏提示逐件保留权威出处，无证据留空，不将推测或参考文本写成馆方原文。原作按需有界解码；标准图放大不会增加原图细节。各作品原许可继续适用，项目 MIT 许可不替代图像许可。

## TF 卡整库保存

设备缓存为默认。主动在 **设置 → 存储管理**选择 FAT32 TF 后，Windows 桥接通过 USB／Wi-Fi 后台保存已安装图库的全部展示图与可用资料；插卡不会自动启用复制。首次复制保持桥接运行，中断后按校验进度续存，未变 JPEG 复用，文字更新不重传全部图片。BLE 用于单幅同步。

完成作品直接读卡，退出桥接或重启后可离线欣赏；未完成内容仍需同步。无卡、卡满、只读或介质异常时保留已有数据并回退，不格式化卡或删除其他文件。目录为 `/sdcard/aibot/gallery/`，见[目录约定](TF-STORAGE.md)。

本次完整高清包均用发布的 Windows 程序隔离导入，各 366 件，所有标准分页读取解码、高清浏览、全屏、缩放和阅读检查通过。新截图为实际安装内容的原生捕获；不能由此推定 205 已刷入实机。201／204 的既有硬件结果及满额腾位、BLE 整包 OTA、长期运行边界见[本版发布记录](RELEASE-0.6.7.md)。

## English

The current bridge is 0.6.7 and TAB5 is 0.2.205-ui. Standard painting remains 2026.10.14.1; standard calligraphy is 2026.10.15.1 with 3,802 orientation images. Each category contains 366 independent works. Optional complete HD editions 2026.10.16 include desktop originals, matching device previews, guides and attribution; no separate standard import is needed. Follow the [resumable download/merge guide](GALLERY-HD.md), then import the complete ZIP through Device Center → Artwork Collections. App upgrades preserve installed collections; failed or cancelled imports preserve existing data.

Desktop browsing supports title/artist/date/pinyin search, wrapped page navigation, independent reading, fullscreen, wheel zoom and pan. HD decoding is bounded and desktop-only; TAB5 receives standard previews. Explicit opt-in TF storage copies complete installed previews/guides over USB/Wi-Fi, resumes verified progress and reuses unchanged JPEGs. Device cache remains the default; no unrelated files are removed. Actual complete imports and full page reads passed in isolation. Native captures are separate from physical firmware acceptance; see the release record for retained evidence and open boundaries.
