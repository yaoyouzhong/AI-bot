# 名画与书法图库 / Artwork collections

适用于 Windows AI-bot 0.6.0 及 TAB5 0.2.145-ui 或更新版本。软件、固件功能统一；图库可选，ESP8266 和其他屏保无需下载。

| 内容 | 文件 | 独立作品 | 横竖屏图片 | 下载大小 |
| --- | --- | ---: | ---: | ---: |
| 每日名画 | `AI-bot-DailyPainting-2026.10.07.zip` | 402 | 804 | 199 MiB |
| 每日书法 | `AI-bot-DailyCalligraphy-2026.10.07.zip` | 411 | 3,000 | 726 MiB |

书法包较大主要因为分页：411 件作品共有 1,500 页，而名画 402 件共 402 页。每页各有横、竖两种排版，因此书法有 3,000 张图片，名画有 804 张；单图平均大小相近。册页、长卷保留为同一作品，不把分页计作额外作品。

1. 电脑端打开“软件与固件更新 → 屏保图库 → 下载图库”，在 TAB5 发布页选择喜欢的图库 ZIP。
2. 下载后点“导入图库 ZIP…”，直接选择 ZIP，无需手动解压或放进程序目录。程序验证每个文件、图片和目录；失败或取消时保留旧图库。
3. 在 TAB5“设置 → 屏幕与声音”选择“每日名画”或“每日书法”，点击预览。自动屏保也使用同一图库。首次图片同步需要保持电脑和桥接运行。

未安装图库时设备会提示在电脑端安装对应图库；其他功能仍可使用。图库保存在 `%LOCALAPPDATA%/AI-bot/DailyArt`，应用升级不重复下载、不删除图库。替换图库时旧目录保留为备份，可从“打开图库目录”查看。已有程序目录 `Assets/DailyArt` 仍作为兼容来源使用，独立导入的同类别包优先。

每件保留作品名、作者、博物馆、馆藏链接、图像来源、许可和加工记录。已核实的通用中文译名显示在画面中，其余保留原馆藏名称。横竖排版及分页不重复计入作品数量。`THIRD_PARTY_NOTICES.md` 列出逐件署名与许可；项目 MIT 许可不替代原图许可。外部 `SHA256SUMS.txt` 校验整个下载包，内部 `PACK.json` 校验包内文件。

图库更新独立于固件；下载新图库后再次导入即可。当前通过发布页手动获取新图库，软件自动更新检查负责电脑端和设备固件。

## English

Requires Windows AI-bot 0.6.0+ and TAB5 0.2.145-ui+. Open Software and Firmware Updates → Artwork Collections, download a painting or calligraphy ZIP from the TAB5 release, and import the ZIP without extracting it. Select the corresponding screensaver on TAB5. Keep the computer and bridge running for initial synchronization.

Both collections are optional; missing packs do not affect other features. Imports validate paths, hashes, catalogs and JPEG dimensions before replacing a collection. Failed/cancelled imports retain the old collection. Data lives in the Windows user-data directory and survives application upgrades; replaced collections are retained as backups. Existing app-folder galleries remain supported, with imported category packs taking priority.

Paintings: 402 works / 804 layout images / 199 MiB. Calligraphy: 411 works / 3,000 layout images / 726 MiB. The 411 calligraphy works contain 1,500 pages, versus 402 pages for painting. Both layouts are stored for every page, so page count is the main size difference; average image sizes are similar. Work counts exclude repeated pages and layouts. Each pack includes per-work attribution, museum, source, license and derivative records. Verified Chinese equivalents appear in captions; otherwise original collection titles remain. Project licensing does not replace image licensing. Download later collection versions manually and import them independently of firmware updates.
