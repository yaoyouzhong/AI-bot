# 电脑图库高清原图与自动回退 — 2026-10-16

## 范围与实现

仅电脑图库浏览器读取可选 desktop-originals.json 本机索引；TAB5 的 BrowsePage/Handle 及图库 JPEG 不改变。电脑有匹配的原图时，独立显示作品、标题和资料；没有索引、原文件缺失/损坏、哈希或图库展示页不匹配、裁切越界、解码不支持时回退到原标准展示图。界面标记“高清原图 尺寸”或“标准预览”。目录缩略图继续使用轻量展示图。

scripts/build_desktop_art_index.py 对当前安装的 366 名画、366 书法建立全部 2314 横屏页映射，读取并核对 1755 个原文件 SHA-256。长卷、册页、成组画面、已审核裁切和页序保持一致；索引绑定 workId、每页展示图哈希及原图哈希。原素材不复制，不下载，索引中包含本机绝对路径，不属于公开发布包。后续高清素材包未制作、未发布。

GalleryDesktopImages 使用 Windows 图像解码器的缩放/区域读取；后台串行解码，取消过时请求，同一作品保留上一帧到新图就绪。输出位图上限约 1600 万像素、长边 8192，避免把超大卷的全尺寸像素长期驻留内存；这是受限高清显示，不能称为任意放大均保持原始像素。已验证约 1.2 GB、83933×4868 的长卷 TIFF 能读取。

## 验证

- Release：0 警告、0 错误。
- 高清专项：原尺寸与局部像素、成组顺序、180°方向、无索引、缺失文件、错误哈希、陈旧展示页、坏裁切、坏 JSON、损坏解码内容、取消均通过。
- 34 个真实代表页高清解码通过，覆盖巨幅长卷、局部、成组作品、册页；输出尺寸上限检查通过。
- 原生 WinForms 完整图库自测通过，包含标准预览路径、默认每日书法、首尾循环、I/拼音浏览控件、左右栏目、选字、滚动、普通/全屏/宽窄布局、无空帧翻页、失败保留和缩放。实际书法/名画窗口断言加载高清成功。
- 当前安装的全部 366+366 标准图页仍能只读解码；本次未修改图库文件或设备固件。
- 实际程序截图：artifacts/gallery-desktop-hd-20261016/checks/installed-fullscreen-calligraphy.png、installed-details-calligraphy.png、installed-fullscreen-painting.png、installed-details-painting.png。
- Python 语法检查、git diff --check 通过。

## 交付与待验

候选 DLL SHA-256：0bc61c2bc5e7af37436dd80e7b365f7e455ef216d4a4f411b956fb536171b57f。
artifacts/gallery-desktop-hd-20261016 提供 27 文件清单（原 26 运行文件＋本机索引）、desktop-originals.json 及 install-gallery-from-desktop.cmd/ps1。安装入口核对对应 DLL 和索引哈希，备份变更运行文件，允许创建本次新索引，复制失败按原存在状态回滚，然后检查状态、恢复原自动轮播并启动。脚本语法通过；尚未安装。

工具环境 --status-once 被原 AppProfileGuard 拒绝（资料目录重定向）；保留保护，需要用户在 Windows 桌面运行安装入口。安装后实际大屏浏览流畅度仍待用户验收，模拟通过不替代该验收。
