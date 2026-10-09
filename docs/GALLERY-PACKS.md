# 名画与书法图库 / Artwork collections

本次准备 Windows AI-bot 0.6.3 / TAB5 0.2.156-ui 与精选作品集 2026.10.09；尚未公开，新包发布后由[完整下载中心](../DOWNLOADS.md)提供。图库兼容 0.6.0 / .145 或更新版本。软件、固件功能统一；图库可选，ESP8266 和其他屏保无需下载。

| 内容 | 文件 | 独立作品 | 横竖屏图片 | 下载大小 |
| --- | --- | ---: | ---: | ---: |
| 每日名画 | `AI-bot-DailyPainting-Curated-2026.10.09.zip` | 366 | 860 | 211.15 MiB |
| 每日书法 | `AI-bot-DailyCalligraphy-Curated-2026.10.09.zip` | 366 | 1,782 | 426.37 MiB |

本次只分发年度精选集，全量图库保留本地。固定入选范围见[精选清单](art-guides/CURATED-ANNUAL-2026-10-09.md)。书法包较大主要因为分页：366 件作品共有 891 页，名画 366 件共 430 页。每页各有横、竖两种排版，因此书法有 1,782 张图片，名画有 860 张；单图平均大小相近。册页、长卷、对联及成套屏幅保留为同一作品，不把分页计作额外作品。两类合计 732 件独立作品、2,642 张图片。

1. 电脑端打开“设备中心 → 桥接设置 → 软件固件”，再点“屏保图库 → 下载图库”，从[完整下载中心](../DOWNLOADS.md)选择喜欢的类别。新精选作品集准备附于本次 TAB5 Release；尚未公开前，原 402 件名画／411 件书法正式包继续有效。
2. 下载后点“导入图库 ZIP…”，直接选择 ZIP，无需手动解压或放进程序目录。程序验证每个文件、图片和目录；失败或取消时保留旧图库。
3. 在 TAB5“设置 → 屏幕与声音”选择“每日名画”或“每日书法”，点击预览。自动屏保也使用同一图库。首次图片同步需要保持电脑和桥接运行。

未安装图库时设备会提示在电脑端安装对应图库；其他功能仍可使用。图库保存在 `%LOCALAPPDATA%/AI-bot/DailyArt`，应用升级不重复下载、不删除图库。替换图库时旧目录保留为备份，可从“打开图库目录”查看。已有程序目录 `Assets/DailyArt` 仍作为兼容来源使用，独立导入的同类别包优先。

每件保留作品名、作者、博物馆、馆藏链接、图像来源、许可和加工记录。已核实的通用中文译名显示在画面中，其余保留原馆藏名称。横竖排版及分页不重复计入作品数量。`THIRD_PARTY_NOTICES.md` 列出逐件署名与许可；项目 MIT 许可不替代原图许可。外部 `SHA256SUMS.txt` 校验整个下载包，内部 `PACK.json` 校验包内文件。

图库更新独立于固件；下载新图库后再次导入即可。当前通过发布页手动获取新图库，软件自动更新检查负责电脑端和设备固件。

## 首次同步与缓存

0.6.1 / .149 修复无线图片同步，并缓存最近查看的图片。首次蓝牙下载仍需数秒，竖屏文件较大可能更久；同图两轮实测为 7.26 / 8.54 秒，不保证每次低于 8 秒。缓存命中时无需重新下载，重启或缓存回收后仍需同步。“仅蓝牙”模式在下载期间临时暂停 Wi-Fi，完成后恢复；自动选择模式保持原回退功能。[实测记录](TAB5-BLE-GALLERY-148.md)

## English

This preparation covers Windows AI-bot 0.6.3, TAB5 0.2.156-ui and curated collection edition 2026.10.09, not yet public. Packs support 0.6.0+ / .145+. Open Device Center → Bridge Settings → Software/Firmware → Artwork Collections, select a ZIP from the [complete download center](../DOWNLOADS.en.md), and import it without extraction. Select the corresponding screensaver on TAB5. Keep the computer and bridge running for initial synchronization. Until publication, the original 402/411-work downloads remain available.

Both collections are optional; missing packs do not affect other features. Imports validate paths, hashes, catalogs and JPEG dimensions before replacing a collection. Failed/cancelled imports retain the old collection. Data lives in the Windows user-data directory and survives application upgrades; replaced collections are retained as backups. Existing app-folder galleries remain supported, with imported category packs taking priority.

Only the annual curated edition is distributed; the full library stays local. Paintings: 366 works / 860 layout images / 211.15 MiB. Calligraphy: 366 works / 1,782 layout images / 426.37 MiB. Calligraphy has 891 pages, versus 430 for painting, with landscape and portrait layouts for each page. Work counts exclude repeated pages, layouts and components of the same album or set. Each pack includes per-work attribution, museum, source, license and derivative records. Verified Chinese equivalents appear in captions; otherwise original collection titles remain. Project licensing does not replace image licensing. Import collection updates independently of firmware updates; browser guide/detail previews remain outside device integration.

Bridge 0.6.1 / TAB5 .149 repairs wireless image synchronization and caches recently viewed images. Cold BLE downloads still take seconds: two runs of the same portrait took 7.26 / 8.54 seconds, without an under-eight-second guarantee. Cache hits avoid downloading; restarts or reclamation require another synchronization. Explicit BLE-only mode temporarily pauses Wi-Fi during downloads and restores it afterward; automatic-mode fallback remains unchanged. See the [hardware record](TAB5-BLE-GALLERY-148.md).
