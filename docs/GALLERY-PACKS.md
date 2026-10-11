# 图库包 / Gallery packs

桥接 0.6.5 支持名画与书法图文包独立导入，标准程序不嵌入整理中的图库文字。TAB5 .176 带阅读能力与扩充字库。本次不发布新图库，现有正式 2026.10.09 图片包保持兼容；没有文字的旧包仍正常展示图片。新包导入后的完整设备验收与内容核实留待后续发布。图库包不安装字体，新正文需检查缺字。

Bridge 0.6.5 supports independent image/text collection imports without embedding in-progress guide content; TAB5 .176 includes readers and extended fonts. No new collection is published here. Formal 2026.10.09 image-only packs remain compatible. New text and full imported-pack hardware acceptance follow separately; font coverage must be checked because packs do not install fonts.

## TF 卡存储（本地开发，尚未部署） / TF storage (local, not deployed)

2026-10-11：TAB5 新增共享 TF 卡挂载与图库持久缓存。已通过桥接认证、SHA-256 和图片尺寸检查的作品按日期、类别、页码及横竖屏保存到 `/sdcard/aibot/gallery/painting` 或 `calligraphy`；每份 `.aig` 记录同时包含 JPEG、作品身份、简介、欣赏提示及原文，文字保持原有完整长度。再次查看优先使用内存或 TF 卡，缺少记录时沿用 USB／Wi-Fi／BLE 获取。已保存日期可跨重启复用；尚未同步的日期仍需桥接，整套年度 ZIP 的直接导入和一次性全量搬运不包含在此改动中。

认证状态提供新类别内容版本时，旧卡记录不再作为该版本的缓存；两个类别独立匹配。每项使用两份轮换记录，完整写入并刷盘后提交，损坏或未完成的记录不参与展示。卡不存在、容量不足、只读或读取失败时保留原内存／桥接路径。照片浏览与图库共用挂载和文件访问锁，发生介质错误后延时重新挂载；不格式化卡、不清理其他用户文件。图库包仍在电脑端独立导入，不改变已有包格式和传输协议。

验证覆盖文件往返、8191 字节原文、模拟重启／离线、横竖屏与类别／版本隔离、损坏记录、容量不足、提交失败、内存不足及并发挂载；完整 ESP-IDF 构建和设备 TF 卡实机验收分别记录。该逻辑尚未部署，已发布的 TAB5 .176 不包含此能力。

The local TF extension persists verified image/reading records under `/sdcard/aibot/gallery`, keyed by date, category, page and orientation. Memory/TF reads precede existing authenticated USB/Wi-Fi/BLE transfers. Cached dates survive restarts; uncached dates still require the bridge. Images and complete optional guides are one checksummed record, with independent category revision matching and alternating committed copies. Absent/full/read-only/failed cards preserve existing paths. Photo browsing and gallery access share one mount/lock, with delayed remount after media failures and no formatting or unrelated-file deletion. Whole annual-ZIP import/initial full-library copying is outside this change. Host tests and firmware builds are separate from TF hardware acceptance; published .176 remains unchanged.


Windows AI-bot 0.6.3 / TAB5 0.2.156-ui 与年度精选集 2026.10.09 已发布，由[完整下载中心](../DOWNLOADS.md)提供正式下载。图库兼容 0.6.0 / .145 或更新版本。软件、固件功能统一；图库可选，ESP8266 和其他屏保无需下载。

| 内容 | 文件 | 独立作品 | 横竖屏图片 | 下载大小 |
| --- | --- | ---: | ---: | ---: |
| 每日名画 | `AI-bot-DailyPainting-Curated-2026.10.09.zip` | 366 | 860 | 211.15 MiB |
| 每日书法 | `AI-bot-DailyCalligraphy-Curated-2026.10.09.zip` | 366 | 1,782 | 426.37 MiB |

本次只分发年度精选集，全量图库保留本地。作品清单与来源保存在正式 ZIP 的目录及许可材料中。书法包较大主要因为分页：366 件作品共有 891 页，名画 366 件共 430 页。每页各有横、竖两种排版，因此书法有 1,782 张图片，名画有 860 张；单图平均大小相近。册页、长卷、对联及成套屏幅保留为同一作品，不把分页计作额外作品。两类合计 732 件独立作品、2,642 张图片。

1. 电脑端打开“设备中心 → 桥接设置 → 软件固件”，再点“屏保图库 → 下载图库”，从[完整下载中心](../DOWNLOADS.md)选择喜欢的类别。本次年度精选集附于 TAB5 0.2.156-ui Release，各 366 件作品。
2. 下载后点“导入图库 ZIP…”，直接选择 ZIP，无需手动解压或放进程序目录。程序验证每个文件、图片和目录；失败或取消时保留旧图库。
3. 在 TAB5“设置 → 屏幕与声音”选择“每日名画”或“每日书法”，点击预览。自动屏保也使用同一图库。首次图片同步需要保持电脑和桥接运行。

未安装图库时设备会提示在电脑端安装对应图库；其他功能仍可使用。图库保存在 `%LOCALAPPDATA%/AI-bot/DailyArt`，应用升级不重复下载、不删除图库。替换图库时旧目录保留为备份，可从“打开图库目录”查看。已有程序目录 `Assets/DailyArt` 仍作为兼容来源使用，独立导入的同类别包优先。

每件保留作品名、作者、博物馆、馆藏链接、图像来源、许可和加工记录。已核实的通用中文译名显示在画面中，其余保留原馆藏名称。横竖排版及分页不重复计入作品数量。`THIRD_PARTY_NOTICES.md` 列出逐件署名与许可；项目 MIT 许可不替代原图许可。外部 `SHA256SUMS.txt` 校验整个下载包，内部 `PACK.json` 校验包内文件。

图库更新独立于固件。当前正式版仍通过下载页手动获取 ZIP 后导入。

## 简介、欣赏提示与原文随包更新（开发中）

欣赏提示为可选字段：必须有对应作品的权威赏析出处、具体来源字段或页码及证据哈希；没有依据时 `looking` 为空，不填通用提示或缺失占位文。新打包器阻止没有这类证据的非空提示。旧导入器曾将该字段列为必填，本轮已修正源码并在隔离目录验证，尚未替换正在运行的桥接程序，因此新包当前不向用户图库导入。

Viewing notes are optional and require work-specific authoritative analysis, a source field or page locator and an evidence hash. Empty notes are accepted; unsourced templates and filler are rejected. The old importer required nonempty notes; this source fix is validated in isolation and has not replaced the running bridge. New packs remain local pending compatible deployment.


新书法集和名画集使用 `PACK.json` schemaVersion=2，包内的 `guides.zh.json` 与图片一起校验和安装，按稳定作品身份对应简介、欣赏提示、已核实原文及来源。包内文字优先于桥接的旧版内置导读。后续只更新内容时，重新导入对应类别 ZIP 即可，无需同时重新编译或升级桥接；名画与书法独立替换，旧包保留为备份。文字更新同样触发内容校验值变化，支持此能力的设备会重新同步受影响类别的当天缓存，并保持旧图片显示直到新图片就绪。

该能力已随桥接 0.6.5 与 TAB5 .168 本地部署，尚未公开发布；新版内容包导入后的完整图文刷新仍需真机验收。完成首次能力升级后，后续内容更新走图库包。旧包继续兼容。原文未核实时不打包推测正文；设备仅显示“这幅作品的原文尚未核实。”。包版本、来源链接和校验值随包保存，工作区研究快照路径不写入包内。单篇原文目前最多 8,191 UTF-8 字节，超限正文在打包前报错，不截断。新增生僻字需核对设备字库覆盖；图库包本身不安装设备字体。

**桥接 0.6.5**：打开“屏保图库”自动检查正式精选集，分别显示“已安装”和“最新精选”日期版本与更新状态。点击对应类别的“下载并安装／升级”，程序直接下载、验证发布包 SHA-256、检查包内类别和版本，再校验全部目录与图片并安装，无需浏览器下载或手动导入。保留“检查更新”和离线 ZIP 导入；相同或更高的本机版本不会提供在线降级。

升级使用新精选集的作品范围，不合并旧版多出的作品。旧类别目录保留为 `painting.backup-*` 或 `calligraphy.backup-*`，可从“打开图库目录”查看；名画和书法独立替换。下载、校验失败，或取消、关闭窗口时，仍使用原图库。成功后立即重新读取安装版本，图库服务无需重启便可使用新包。

本地全量资源与实际播放的精选集分别保留：全量源资源可留在项目的 `windows-app/AIBotBridge/Assets/DailyArt`，不纳入公开发布包；导入的线上精选集放在用户图库目录并优先用于播放。精选集更新只替换用户目录中的对应类别，不修改项目全量源资源。窗口把更新状态、已安装版本和最新精选版本分开显示，避免状态文字挤占版本信息。

大包按 2 MiB 分段下载，每段最多尝试三次。失败、取消和关闭窗口保留断点，再次点击或重启桥接后从已保存的字节位置继续；下载缓存位于 `%LOCALAPPDATA%/AI-bot/updates/gallery-downloads`，按完整包的 SHA-256 隔离，并核对类别、日期版本、文件名和大小。包校验值变化时使用新缓存，不混用分段。完整 SHA-256 不符会清除损坏的断点；安装成功才清理该包的下载缓存，旧图库备份保留。

## 图片与导读一起更新（本版程序支持，内容稍后发布）

新的名画和书法包均使用 `PACK.json schemaVersion=2`，必须包含 `guides.zh.json`。每个类别只携带本包作品的简介、欣赏提示、已核实原文、原文说明和来源记录，以 `id/category/workId` 对应图片目录；同一作品的分页共用文字。未核实或待复核原文不会发送到阅读页，通行原文与原作释文保留各自说明。

导读文件与图片一起纳入包内 SHA-256 清单。导入先验证哈希、作品身份、覆盖范围和设备字段上限，全部通过后才整体替换对应类别；导入失败或取消时保留原图片和原文字，旧类别仍保留为备份。桥接导入后立即重新读取，不需重启桥接。支持内容版本通知的设备会自动重新同步受影响类别的当天缓存，另一类别继续可用；旧设备重启后获取新内容，程序不会自动重启设备。

新桥接安装一次后，后续名画、书法内容可以各自按日期版本发布和导入，不因文字增补再次升级桥接。旧 `schemaVersion=1` 图片包继续兼容并使用桥接内置导读作为回退；新格式的导读缺失时报告图库无效，不用内置旧文字替代。旧桥接不支持 `schemaVersion=2`，需要先升级到包含本次改动的版本。设备沿用既有 `guideVersion:1/2` 协商，长原文在 v1 读页显示升级提示，在 v2 读页完整提供；认证状态增加可选类别内容校验值，不改变图片传输及认证规则，正式版本号保持不变。

`scripts/package_daily_art.py` 默认同时生成两个独立 ZIP，可用 `--guides` 指定审定资料，或自动使用源目录内的 `guides.zh.json`；否则使用项目审定目录。`--category painting|calligraphy` 可只打包一个类别。年度精选构建器 `scripts/build_curated_art.py` 同样支持 `--category`，在已锁定的作品范围及年度顺序内加入导读，不增加作品或改动 JPEG；同一次构建的包和审阅副本使用同一文字快照。这里只实现程序和打包流程，内容采集与正式图库发布继续独立进行。

## 首次同步与缓存

0.6.1 / .149 修复无线图片同步，并缓存最近查看的图片。首次蓝牙下载仍需数秒，竖屏文件较大可能更久；同图两轮实测为 7.26 / 8.54 秒，不保证每次低于 8 秒。缓存命中时无需重新下载，重启或缓存回收后仍需同步。“仅蓝牙”模式在下载期间临时暂停 Wi-Fi，完成后恢复；自动选择模式保持原回退功能。[实测记录](TAB5-BLE-GALLERY-148.md)

## English

**Pack-bundled reading material (bridge 0.6.5):** schemaVersion=2 packs include `guides.zh.json`, with introductions, viewing suggestions, reviewed originals and portable source records bound to stable artwork identities. Imports validate and atomically replace images and text together. Pack text takes priority over optional local embedded guides. After the initial capability update, later content editions need only the corresponding category ZIP; rebuilding or upgrading the bridge alongside each collection is unnecessary. Authenticated content digests also refresh the affected device cache, retaining the previous bitmap until new data arrives. This capability is included in bridge 0.6.5 and TAB5 .176; full hardware acceptance of newly imported image/text packs is pending. Older packs remain compatible, unverified originals stay empty, and originals above 8,191 UTF-8 bytes are rejected rather than truncated. New rare characters must be checked against the device font; gallery packs do not install fonts.

**Pack guides (bridge 0.6.5):** both painting and calligraphy packs now use `PACK.json schemaVersion=2` and require `guides.zh.json`. Each category bundles introductions, viewing suggestions, reviewed originals, explanations and source records only for its own works, bound by `id/category/workId`. Hashes, coverage, identity and device field limits are validated before atomically replacing images and text. Failed/cancelled imports retain both; old collections remain as backups. The running bridge reloads successful imports immediately. Supporting devices refresh the affected category cache from authenticated content digests; legacy devices fetch cached content again after a restart. Imports never restart devices automatically.

After installing the bridge containing this change once, later painting or calligraphy editions update independently through their ZIPs, without another bridge upgrade for text changes. Legacy schema-1 image-only packs remain compatible; optional local builds may retain embedded-guide fallback; schema-2 packs never substitute embedded text for missing packaged guides. Earlier bridges reject schema 2 and must be upgraded first. Existing guide v1/v2 negotiation remains unchanged, including the whole-text upgrade notice for long originals on v1 readers. `package_daily_art.py` accepts `--guides` and optional `--category painting|calligraphy`; the annual builder also accepts `--category` and freezes the same guide snapshot for packages and review artifacts. No collection research or public release is performed by this change.

**Bridge 0.6.5:** opening Artwork Collections checks formal curated editions and compares their date-based versions with installed packs. Each category can be downloaded, checksum-verified and installed directly in the window. Large packs use 2 MiB chunks with up to three attempts each; partial downloads persist across failures, cancellation, window closure and bridge restarts. Caches are keyed by SHA-256 and validate category, edition, filename and size, so different packages cannot mix bytes. A bad final checksum clears the corrupt checkpoint. ZIP import remains available offline. Online updates cannot downgrade; failures or cancellations preserve installed artwork, and old category folders remain as backups. Updated selections replace rather than merge old works. Successful installation reads back the edition, hot-reloads gallery data and removes that package's download cache.

The full source library may remain in the project's `windows-app/AIBotBridge/Assets/DailyArt`, outside public packages. Imported online curated editions live in the user gallery directory and take playback priority. Updating a curated category does not modify the project's full source library. The window separates update status from installed and available edition details to keep version information readable.

Windows AI-bot 0.6.3, TAB5 0.2.156-ui and annual curated collection edition 2026.10.09 are published. Packs support 0.6.0+ / .145+. Open Device Center → Bridge Settings → Software/Firmware → Artwork Collections, select a ZIP from the [complete download center](../DOWNLOADS.en.md), and import it without extraction. Select the corresponding screensaver on TAB5. Keep the computer and bridge running for initial synchronization. The current TAB5 release provides 366 works per curated category.

Both collections are optional; missing packs do not affect other features. Imports validate paths, hashes, catalogs and JPEG dimensions before replacing a collection. Failed/cancelled imports retain the old collection. Data lives in the Windows user-data directory and survives application upgrades; replaced collections are retained as backups. Existing app-folder galleries remain supported, with imported category packs taking priority.

Only the annual curated edition is distributed; the full library stays local. Paintings: 366 works / 860 layout images / 211.15 MiB. Calligraphy: 366 works / 1,782 layout images / 426.37 MiB. Calligraphy has 891 pages, versus 430 for painting, with landscape and portrait layouts for each page. Work counts exclude repeated pages, layouts and components of the same album or set. Each pack includes per-work attribution, museum, source, license and derivative records. Verified Chinese equivalents appear in captions; otherwise original collection titles remain. Project licensing does not replace image licensing. Import collection updates independently of firmware updates; browser guide/detail previews remain outside device integration.

Bridge 0.6.1 / TAB5 .149 repairs wireless image synchronization and caches recently viewed images. Cold BLE downloads still take seconds: two runs of the same portrait took 7.26 / 8.54 seconds, without an under-eight-second guarantee. Cache hits avoid downloading; restarts or reclamation require another synchronization. Explicit BLE-only mode temporarily pauses Wi-Fi during downloads and restores it afterward; automatic-mode fallback remains unchanged. See the [hardware record](TAB5-BLE-GALLERY-148.md).
