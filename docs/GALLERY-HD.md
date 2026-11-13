# 高清完整图库 / Complete HD collections

名画与书法各 366 件，内容版次 **2026.2**，内部包版本为 2026.11.12.1。[版次说明](GALLERY-VERSIONS.md)。完整包包含高清素材、TAB5 标准展示图、简介、已核实欣赏资料、原文或明确标注的参考文本，以及来源和许可；无需先安装标准版。电脑优先使用高清，TAB5 使用适配设备的展示图。

<a id="download"></a>

## 推荐下载与安装

从[图库下载表](GALLERY-PACKS.md)选择同类的标准版或高清完整版即可；高清名画 **2.93 GiB／2 段**，高清书法 **2.89 GiB／2 段**。以下下载工具同时支持两套，按提示选择一套。

1. 安装 [AI-bot 桥接 0.6.20](../DOWNLOADS.md)。
2. Windows 或 Mac 下载[高清图库下载工具 ZIP](https://github.com/yaoyouzhong/AI-bot/releases/download/gallery-2026.11.12.1/AI-bot-HD-Gallery-Downloader-2026.3.zip)，解压到空文件夹。Mac 如提示无法打开，可在 Finder 中右键选择“打开”并按系统提示操作。
3. Windows 双击 **Download-HD-Gallery.cmd**，Mac 双击 **Download-HD-Gallery.command**，输入 **1（名画）**或 **2（书法）**。工具逐段下载并检查 SHA-256，再合并、核验完整 ZIP。中断后再次运行即可续传；已完成段不重复下载，未完成段用 HTTP Range 接着下载（服务端不支持 Range 时只重下该段）。不改变系统执行策略。
4. 合并时预留至少 **6 GiB**，导入还需额外安装空间。原分段保留，不覆盖不同的已有 ZIP。
5. 打开 **设备中心 → 屏保图库 → 安装图库包…**，选择合并后的完整 ZIP，无需手动解压，不能直接选 `.partNNN`。

手动下载时可从下方获取相应的两个分段。建议使用下载工具自动校验和合并，再在桥接中导入生成的 ZIP。

只需 TAB5 标准展示图的用户，可从[图库下载表](GALLERY-PACKS.md)选标准名画或标准书法。高清分段及校验清单见[图库发布页](https://github.com/yaoyouzhong/AI-bot/releases/tag/gallery-2026.11.12.1)和 [SHA256SUMS.txt](https://github.com/yaoyouzhong/AI-bot/releases/download/gallery-2026.11.12.1/SHA256SUMS.txt)。

## 手动分段与完整哈希

分段下载完成后需要合并并核对完整 ZIP 的 SHA-256。Windows 与 Mac 可使用下载工具自动处理。

<a id="painting"></a>

<details>
<summary>高清名画，2 段，完整 ZIP 3,141,293,222 字节</summary>

完整 ZIP SHA-256 为 `5173ef666cac71a2b13cfa8a85c952b2cc391d03afcf96342a17caf36326228b`。

- [名画第 1 段](https://github.com/yaoyouzhong/AI-bot/releases/download/gallery-2026.11.12.1/AI-bot-gallery-painting-hd-2026.11.12.1.zip.part201)
- [名画第 2 段](https://github.com/yaoyouzhong/AI-bot/releases/download/gallery-2026.11.12.1/AI-bot-gallery-painting-hd-2026.11.12.1.zip.part202)

</details>

<a id="calligraphy"></a>

<details>
<summary>高清书法，2 段，完整 ZIP 3,104,737,018 字节</summary>

完整 ZIP SHA-256 为 `5fc40849c20fb033faf9222d3a48a2f75377ccb6b64d8283530a9cef45a7176a`。

- [书法第 1 段](https://github.com/yaoyouzhong/AI-bot/releases/download/gallery-2026.11.12.1/AI-bot-gallery-calligraphy-hd-2026.11.12.1.zip.part201)
- [书法第 2 段](https://github.com/yaoyouzhong/AI-bot/releases/download/gallery-2026.11.12.1/AI-bot-gallery-calligraphy-hd-2026.11.12.1.zip.part202)

</details>

## 画质与内容边界

取消或导入失败保留旧图库；图片、资料和对应预览一起更新。高清原作采用有界解码，超大作品并非无限放大仍保持全部原始像素。分发版《汉宫春晓图》使用处理后的 JPEG，不含 1.2 GB TIFF；处理和来源记录随包保留。作品图像的原许可独立适用。

## English

Both complete HD editions contain 366 works, desktop originals, matching TAB5 previews, guides and source/license records. Standard installation is unnecessary. Install bridge 0.6.20, download and extract the HD downloader ZIP linked above to an empty folder, run Download-HD-Gallery.cmd on Windows or Download-HD-Gallery.command on Mac and choose 1 (painting) or 2 (calligraphy). It downloads two parts, resumes interrupted HTTP transfers, verifies each part and the final archive, and retains all parts. Run again after interruption. Reserve at least 6 GiB for download plus merging, with additional import space. Import the complete ZIP through Device Center → Artwork Collections → Install Gallery Package; never import an individual part.

Manual part links are listed above. Completed upload/download parts are reused; GitHub upload recovery occurs at part boundaries because its release API does not resume inside one asset. Edition 2026.2 is available as standard and complete HD (package version 2026.11.12.1). Use the corresponding standard package for device previews and the complete HD package for desktop detail. Bounded image decoding, distribution derivatives and individual licenses retain their stated limits.
