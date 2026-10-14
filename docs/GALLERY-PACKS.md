# 图库包 / Gallery packs

2026-10-14 正式版本：Windows 桥接 **0.6.6**、TAB5 **0.2.194-ui**。名画布局包 **2026.10.14.1** 已同步横屏标题、作者和年代；书法沿用 **2026.10.09** 正式精选集。两类各 366 件独立作品，分开下载和导入。整理中的新书法与文字集不属于本次发布。

The current releases are Windows bridge 0.6.6 and TAB5 0.2.194-ui. Painting layout edition 2026.10.14.1 updates landscape title, author and period; published calligraphy edition 2026.10.09 is retained. Each optional category contains 366 independent works. Unpublished editorial collections are excluded.

| 类别 / Category | 文件 / File | 作品 / Works | 横竖屏图片 / Images | 大小 / Size |
| --- | --- | ---: | ---: | ---: |
| 每日名画 / Painting | `AI-bot-DailyPainting-Curated-2026.10.14.1.zip` | 366 | 826 | 191.26 MiB |
| 每日书法 / Calligraphy | `AI-bot-DailyCalligraphy-Curated-2026.10.09.zip` | 366 | 1,782 | 426.37 MiB |

名画包共有 413 页，横竖各一张；本轮只更新横屏，413 张竖屏字节保持。分页、册页和方向不增加独立作品数。来源、许可和完整校验清单保留在 ZIP 中；项目 MIT 许可不替代作品图像许可。

Paintings contain 413 pages in two orientations. All 413 portrait files retain their reviewed bytes. Pages, albums and orientations are not counted as additional works. Attribution, image licenses and checksums remain in each pack.

## 下载、浏览与导入 / Download, browse and import

1. 从[完整下载中心](../DOWNLOADS.md)选择需要的图库。在 Windows「设备中心 → 桥接设置 → 软件固件 → 屏保图库」检查或导入 ZIP，无需解压。升级软件保留已安装图库，更新某一类别不改另一类别。
2. 点击「浏览作品」，按作品名、作者或日期查找；支持全屏、滚轮缩放、拖动及阅读资料。单页作品不显示上一页／下一页。
3. 在 TAB5「设置 → 屏幕与声音」选择每日名画或每日书法，并打开预览。横屏将作品名、作者和年代置于图画左侧；竖屏显示简化信息。书法保持相同布局原则。

Import ZIPs directly in Windows Artwork Collections. App upgrades preserve installed data; replacing one category leaves the other intact. Browse Works supports title/artist/date search, fullscreen, zoom, pan and available reading material. Select the matching TAB5 screensaver. Landscape metadata sits beside the artwork; portrait information is compact. Failed or cancelled imports preserve the previous pack and its backup.

## TF 卡整库保存 / Complete TF storage

默认使用设备缓存。主动在「设置 → 存储管理」选择 FAT32 TF 卡后，电脑桥接通过 USB／Wi-Fi 保存已安装的整套图片与可用资料；插卡不会自动启用整库复制。首次保存需保持电脑和桥接运行，页面显示名画／书法保存进度，中断后按校验进度续存。

保存完成后直接读卡，退出桥接或重启后仍可预览已保存作品。未保存作品仍需同步；无卡、卡满或介质异常时保留已有内容并回退，不格式化卡或删除其他文件。数据位于 `/sdcard/aibot/gallery/`；[目录约定](TF-STORAGE.md)。BLE 用于单幅同步，全库保存使用 USB／Wi-Fi。

Device cache remains the default. Explicitly choose FAT32 TF storage to copy installed collections over USB/Wi-Fi. Keep the bridge running for the initial copy; verified progress resumes after interruption. Completed works load directly from TF across bridge shutdowns and restarts. Unsaved content still requires synchronization. No automatic card formatting or deletion of unrelated files is performed; BLE retains individual transfers.

本轮真实设备已确认两类预览不再停在“等待电脑同步图片”、横竖布局正确及原轮播恢复。多页作品实机翻页由用户安排后续核验；长期 BLE 稳定性、提速和整包 BLE OTA 仍不声明通过。[发布与验收](RELEASE-0.6.6.md)。截图采用原生隔离数据，不替代真机结论。

Both saved screensavers, layouts and restored cycling passed the current hardware checks. Physical multi-page navigation is deferred; prolonged BLE stability, the speed objective and whole-image BLE OTA remain open. Native fixture screenshots are separate from hardware evidence.

## 内容与来源 / Content and sources

作品资料按各包实际内容显示，不将缺失的欣赏提示或释文补写为事实。欣赏提示须有对应作品的权威出处；没有证据时留空。程序支持图片与资料一起校验、按类别独立更新以及完整长文分段；这不代表所有本地整理资料已公开发布。

Reading material follows the actual installed pack. Unverified appreciation or transcription is not invented. Per-work authoritative evidence is required for appreciation hints. The software can verify and update images and text together, including paged long references; unpublished local editorial material remains excluded.

<details>
<summary>历史开发记录 / Historical development notes</summary>

下列版本和“待构建／未发布”描述是当时记录；当前下载、交付范围和验收以本页上方为准。


年度精选名画 2026.10.11.23、书法 2026.10.12.32 各 366 件，以独立图文 ZIP 提供。图片、简介、权威欣赏资料、原文或明确标注的参考及来源一起更新。Windows 0.6.6 与 TAB5 .178 支持完整长文分段、最新字库及 TF 整库保存；先升级程序／固件，再导入内容。旧图片包继续兼容，字库由固件安装，图库包不安装字体。

Two independent annual packs contain 366 paintings and 366 calligraphy works, with images and reviewed reading/source material updated together. Windows 0.6.6 and TAB5 .178 support complete long-text paging, the latest font and opt-in complete-library TF storage. Upgrade the app/firmware before importing new content; old image-only packs remain compatible. Collection imports do not install fonts.

## 电脑浏览作品 / Desktop browsing

Windows 桥接在「设备中心 → 桥接设置 → 软件／固件 → 屏保图库」增加「浏览作品」。左侧滚动选择已安装名画或书法，支持作品名／作者搜索及按日期查找；日期映射与每日屏保一致，跨年份连续循环。右侧以图片为主，单页作品隐藏分页按钮，简介、欣赏提示与完整原文通过「阅读资料」按需展开。

点击「全屏观看」或双击图片放大观看；鼠标滚轮围绕指针缩放，拖动查看细节，「适合窗口／屏幕」恢复完整画面，Esc 退出全屏。图片使用已安装包里的横屏展示页，放大不增加原图分辨率。旧图片包仍可浏览，没有核实的文字显示缺失状态。导入后可刷新目录，不修改图库文件、设备设置或每日作品日期；暂不提供「在 TAB5 查看」。

追加源程序已通过 Windows 原生入口、查找与导航、全屏／滚轮／拖动／退出、取消与失败状态，以及 100%／150%／200% 正常和最小窗口检查；只读逐页解码本机两类各 366 件作品。证据见 `artifacts/development/gallery-browser-20261011/`。尚未部署或对外发布，不需要构建 TAB5 固件。

Windows source adds Device Center → Bridge Settings → Software/Firmware → Artwork Collections → Browse Works. Scroll installed paintings/calligraphy, search titles/artists, or find by date using the same continuous mapping as the daily screensaver. The image dominates; single-page works hide paging controls and reading tabs expand on demand. Fullscreen via button/double-click, pointer-anchored wheel zoom, drag to pan, fit reset and Esc are supported. It uses packaged landscape presentation pages, so zoom does not add source resolution. Legacy image-only packs remain browsable; missing verified text is not fabricated. Refresh after imports; no pack files, device settings or daily dates are changed, and no View on TAB5 action is provided. Native interactions, failure/cancellation states, normal/minimum layouts at 100/150/200% and read-only full-page decoding of both installed 366-work collections passed. This source addition is not deployed or published and requires no TAB5 firmware build.

## TF 卡整库保存（待构建 .178） / Complete TF collection storage (pending .178)

TAB5 .178 待构建源程序配合 Windows 桥接 0.6.6，在「设置 → 存储与缓存」主动选择 TF 卡后，通过 USB／Wi-Fi 在后台同步电脑已安装的整套名画、书法，保存每件作品的所有分页、横竖屏图片和配套阅读文字；本页独立显示卡状态及完成作品数。默认仍为设备内存缓存，插卡不会自动启用整库保存。全部保存后直接读卡，跨重启和年份离线展示；尚未保存的作品仍需桥接。首次复制不是每天只缓存一幅。图库 ZIP 仍在电脑端导入，整理中的新内容不随程序包发送到卡。已安装 .177 缺少 FAT 长文件名配置，修复及新设置界面均等待字库稳定后构建和实卡验收。

中断后按持久进度续存，并复用已经校验的分页；前台浏览、阅读、语音、照片及 OTA 优先，不在后台分配 RGB 显示帧。内容更新按类别校验版本，重新获取元数据，相同 SHA-256 的图片复用卡上字节；仅修改简介、欣赏提示或原文不重传图片。BLE 保留当前作品传输能力，全库复制使用 USB／Wi-Fi。

作品图文以完整校验记录保存在 `/sdcard/aibot/gallery/painting` 和 `calligraphy`，每件作品单独放入 `works/<作品索引键>/`，两份轮换记录排除损坏或未完成内容。项目所有卡内数据统一使用 `aibot/`，预留照片、其他数据及缓存目录，详见 [TF 卡目录约定](TF-STORAGE.md)。无卡、卡满、只读或介质错误时暂停保存并保留内存／桥接路径；不格式化卡、不删除其他用户文件。全库主链路、断点、跨年离线和文字更新已做主机回归，实际 TF 卡验收另行记录。已发布 .176 不含此功能。

Pending TAB5 .178 source with Windows bridge 0.6.6 synchronizes both complete installed collections over USB/Wi-Fi only after explicitly choosing TF in Settings → Storage & Cache: every work/page/orientation and optional full guide. Independent settings show card status and completed works; the default remains device memory cache and inserting a card does not opt in. Finished collections read directly from TF across reboots and years; unsaved works still require the bridge. Persistent cursors and checked pages resume interrupted copies. Foreground use has priority and background jobs allocate no RGB frame. Category revisions refresh metadata while identical JPEGs are reused; text-only updates transfer no image ranges. BLE retains individual transfers. ZIPs remain imported on the computer and new in-progress content is excluded. Alternating records, card-failure fallback and shared nonformatting photo/gallery locking preserve unrelated files. Installed .177 lacked FAT long-filename support; its fix and the new settings await finalized fonts, firmware construction and separate hardware acceptance. Public .176 is unchanged.


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

该能力已随桥接 0.6.5 与 TAB5 .168 本地部署，尚未公开发布；新版内容包导入后的完整图文刷新仍需真机验收。完成首次能力升级后，后续内容更新走图库包。旧包继续兼容。原文未核实时不打包推测正文；设备仅显示“这幅作品的原文尚未核实。”。包版本、来源链接和校验值随包保存，工作区研究快照路径不写入包内。原有单字段上限仍为 8,191 UTF-8 字节；新增完整原文按下述 v3 分段阅读，上限 32,760 UTF-8 字节，不截断。新增生僻字需核对设备字库覆盖；图库包本身不安装设备字体。

**桥接 0.6.5**：打开“屏保图库”自动检查正式精选集，分别显示“已安装”和“最新精选”日期版本与更新状态。点击对应类别的“下载并安装／升级”，程序直接下载、验证发布包 SHA-256、检查包内类别和版本，再校验全部目录与图片并安装，无需浏览器下载或手动导入。保留“检查更新”和离线 ZIP 导入；相同或更高的本机版本不会提供在线降级。

升级使用新精选集的作品范围，不合并旧版多出的作品。旧类别目录保留为 `painting.backup-*` 或 `calligraphy.backup-*`，可从“打开图库目录”查看；名画和书法独立替换。下载、校验失败，或取消、关闭窗口时，仍使用原图库。成功后立即重新读取安装版本，图库服务无需重启便可使用新包。

本地全量资源与实际播放的精选集分别保留：全量源资源可留在项目的 `windows-app/AIBotBridge/Assets/DailyArt`，不纳入公开发布包；导入的线上精选集放在用户图库目录并优先用于播放。精选集更新只替换用户目录中的对应类别，不修改项目全量源资源。窗口把更新状态、已安装版本和最新精选版本分开显示，避免状态文字挤占版本信息。

大包按 2 MiB 分段下载，每段最多尝试三次。失败、取消和关闭窗口保留断点，再次点击或重启桥接后从已保存的字节位置继续；下载缓存位于 `%LOCALAPPDATA%/AI-bot/updates/gallery-downloads`，按完整包的 SHA-256 隔离，并核对类别、日期版本、文件名和大小。包校验值变化时使用新缓存，不混用分段。完整 SHA-256 不符会清除损坏的断点；安装成功才清理该包的下载缓存，旧图库备份保留。

## 图片与导读一起更新（本版程序支持，内容稍后发布）

新的名画和书法包均使用 `PACK.json schemaVersion=2`，必须包含 `guides.zh.json`。每个类别只携带本包作品的简介、欣赏提示、已核实原文、原文说明和来源记录，以 `id/category/workId` 对应图片目录；同一作品的分页共用文字。未核实或待复核原文不会发送到阅读页，通行原文与原作释文保留各自说明。

导读文件与图片一起纳入包内 SHA-256 清单。导入先验证哈希、作品身份、覆盖范围和设备字段上限，全部通过后才整体替换对应类别；导入失败或取消时保留原图片和原文字，旧类别仍保留为备份。桥接导入后立即重新读取，不需重启桥接。支持内容版本通知的设备会自动重新同步受影响类别的当天缓存，另一类别继续可用；旧设备重启后获取新内容，程序不会自动重启设备。

新桥接安装一次后，后续名画、书法内容可以各自按日期版本发布和导入，不因文字增补再次升级桥接。旧 `schemaVersion=1` 图片包继续兼容并使用桥接内置导读作为回退；新格式的导读缺失时报告图库无效，不用内置旧文字替代。旧桥接不支持 `schemaVersion=2`，需要先升级到包含本次改动的版本。认证状态增加可选类别内容校验值，不改变图片传输及认证规则，正式版本号保持不变。

年度书法包保留已核实或已标明参考范围的 `completeReferenceText`，以独立的 `completeReferenceTextSha256` 校验内嵌正文；原有 `completeReferenceSha256` 仍标识参考文件，不混作正文哈希。Windows 目录阅读使用完整正文。v1 在正文达到 512 字节时、v2 在达到 8,192 字节时返回整篇升级提示，不发送节录冒充全文。v3 对超出原单字段上限的正文返回第 1 段、`transcriptionPages` 和全文 `transcriptionSha256`；认证的 `gallery/guidePage` 请求携带 `category/date/workId/sha256/page`，返回 `workId/page/pages/sha256/pageSha256/text`。段号从 0 开始；段边界保持 Unicode 字符完整，每段最多 4,095 UTF-8 字节，实际 JSON 回复不超过 8 KiB；manifest 不超过 16 KiB。正文最多 32 段、32,760 字节，身份或正文哈希变化报 409，越界段号报 400。

TAB5 候选源码在同一认证后台 worker 中获取并校验全部段，再提供“上一段／下一段”；不扩张原 `transcription[8192]` 字段。阅读页使用打开时冻结的已校验段快照，关闭即释放；既有图库修订通知仍可关闭阅读页。保存到 TF 时将已校验段一起保存，读回重新核对完整正文哈希，缺段或损坏不当作完整原文。图库包不携带固件；v3 需要本次候选桥接及支持该能力的待构建固件。本地打包和模拟／native 测试不等于真机验收；本次不部署设备。

这 7 篇接入的是已标明范围的完整参考文本。分页重组和 SHA-256 只证明正文传输完整，不把参考本升级为本件逐字释文；包内继续保留逐件来源、异文、缺文和待精校说明。

| 作品 | 阅读与精校界限 |
| --- | --- |
| 道因法师碑（拓本） | 完整来源版本参考，保留原录阙五字；未据参考本补造碑面缺文。 |
| 行书居庸赋卷 | 同篇完整参考，保留组字描述和原注；本件异文、题识和后跋待精校。 |
| 书谱 | 馆方主文完整参考；未冒称独立逐字对照墨迹的释文。 |
| 自书诗稿册 | 馆方诗稿记录完整参考；本件逐字异文与图像对照待精校。 |
| 颜氏家庙碑（拓本） | 固定古籍参考正文含刊刻尾记；本件世系、人名、异体及碑面拼接范围待精校。 |
| 佛顶尊胜陀罗尼经（拓本） | 已识别的汉译经文完整参考；刻录起止、咒音字形和题款未逐一对照。 |
| 楷书《离骚》《九歌》卷 | 《离骚》及《九歌》十一篇按已核篇序提供参考；本件异文、卷末题记、题跋、款印仍待精校。 |

本次仅交付本地作品包与桥接候选、阅读客户端源码及本机验证证据；不构建正式 TAB5 固件，不安装到用户图库、不部署或刷写设备，不推送或公开发布。

`scripts/package_daily_art.py` 默认同时生成两个独立 ZIP，可用 `--guides` 指定审定资料，或自动使用源目录内的 `guides.zh.json`；否则使用项目审定目录。`--category painting|calligraphy` 可只打包一个类别。年度精选构建器 `scripts/build_curated_art.py` 同样支持 `--category`，在已锁定的作品范围及年度顺序内加入导读，不增加作品或改动 JPEG；同一次构建的包和审阅副本使用同一文字快照。这里只实现程序和打包流程，内容采集与正式图库发布继续独立进行。

## 首次同步与缓存

0.6.1 / .149 修复无线图片同步，并缓存最近查看的图片。首次蓝牙下载仍需数秒，竖屏文件较大可能更久；同图两轮实测为 7.26 / 8.54 秒，不保证每次低于 8 秒。缓存命中时无需重新下载，重启或缓存回收后仍需同步。“仅蓝牙”模式在下载期间临时暂停 Wi-Fi，完成后恢复；自动选择模式保持原回退功能。[实测记录](TAB5-BLE-GALLERY-148.md)

## English

**Pack-bundled reading material (bridge 0.6.5):** schemaVersion=2 packs include `guides.zh.json`, with introductions, viewing suggestions, reviewed originals and portable source records bound to stable artwork identities. Imports validate and atomically replace images and text together. Pack text takes priority over optional local embedded guides. After the initial capability update, later content editions need only the corresponding category ZIP; rebuilding or upgrading the bridge alongside each collection is unnecessary. Authenticated content digests also refresh the affected device cache, retaining the previous bitmap until new data arrives. This capability is included in bridge 0.6.5 and TAB5 .176; full hardware acceptance of newly imported image/text packs is pending. Older packs remain compatible and unverified originals stay empty. The existing 8,191-byte field is unchanged; v3 segmented reading supports complete originals up to 32,760 UTF-8 bytes without truncation. New rare characters must be checked against the device font; gallery packs do not install fonts.

**Pack guides (bridge 0.6.5):** both painting and calligraphy packs now use `PACK.json schemaVersion=2` and require `guides.zh.json`. Each category bundles introductions, viewing suggestions, reviewed originals, explanations and source records only for its own works, bound by `id/category/workId`. Hashes, coverage, identity and device field limits are validated before atomically replacing images and text. Failed/cancelled imports retain both; old collections remain as backups. The running bridge reloads successful imports immediately. Supporting devices refresh the affected category cache from authenticated content digests; legacy devices fetch cached content again after a restart. Imports never restart devices automatically.

After installing the bridge containing this change once, later painting or calligraphy editions update independently through their ZIPs, without another bridge upgrade for text changes. Legacy schema-1 image-only packs remain compatible; optional local builds may retain embedded-guide fallback; schema-2 packs never substitute embedded text for missing packaged guides. Earlier bridges reject schema 2 and must be upgraded first. `package_daily_art.py` accepts `--guides` and optional `--category painting|calligraphy`; the annual builder also accepts `--category` and freezes the same guide snapshot for packages and review artifacts. No public release is performed by this change.

**Annual complete-text reader v3:** reviewed `completeReferenceText` retains its stated reference/collation scope and gets a distinct `completeReferenceTextSha256` over the embedded UTF-8 text; legacy `completeReferenceSha256` remains the reference-file evidence hash. Windows browsing reads complete text. v1 returns a whole-text upgrade notice at 512 bytes, v2 at 8,192 bytes. For longer originals, v3 manifests include page 0, `transcriptionPages` and complete `transcriptionSha256`. Authenticated `gallery/guidePage` reads use `category/date/workId/sha256/page` and return `workId/page/pages/sha256/pageSha256/text`. Page numbering starts at 0; Unicode characters are never split. Each page contains at most 4,095 UTF-8 bytes and its actual JSON reply is bounded to 8 KiB; manifests remain below 16 KiB. Originals are bounded to 32 pages and 32,760 bytes. Stale identity/hash returns 409 and invalid page indexes return 400. The candidate TAB5 worker retrieves and verifies all pages on its existing authenticated link, then the reader offers previous/next-page controls without enlarging `transcription[8192]`. The open reader owns a frozen verified page snapshot and releases it on close; existing pack-revision handling may still close the reader. TF metadata persists and revalidates the complete page set for offline reading. This requires the candidate bridge and pending v3 firmware; ZIPs include no firmware. Local/native verification is separate from hardware acceptance; no device deployment is performed.

**Scope of the seven complete references:** paged reassembly and SHA-256 establish complete transmission, not independent character-by-character transcription of the specific object. The Daoyin stele retains five explicitly missing source characters; the Juyong rhapsody retains character descriptions/annotations and pending object variants/colophons. Shupu and the self-written poem album retain their complete museum-reference scope without claiming independent image collation. The Yan family stele includes the fixed literary reference's inscription tail while names, variants and the specific rubbing's coverage remain pending. The Usnisa Vijaya scripture retains the identified complete translated reference, with carved extent, dharani forms and inscriptions still to check. The Lisao/Jiuge scroll preserves Lisao and all eleven Jiuge songs in their checked order, while object variants, final notes, colophons and seals remain pending. Per-work source and collation disclosures remain in the pack. This work delivers local collection ZIPs, the bridge candidate, reader source and local evidence only: no formal TAB5 firmware build, user-library installation, device deployment/flashing, push or public release.

**Bridge 0.6.5:** opening Artwork Collections checks formal curated editions and compares their date-based versions with installed packs. Each category can be downloaded, checksum-verified and installed directly in the window. Large packs use 2 MiB chunks with up to three attempts each; partial downloads persist across failures, cancellation, window closure and bridge restarts. Caches are keyed by SHA-256 and validate category, edition, filename and size, so different packages cannot mix bytes. A bad final checksum clears the corrupt checkpoint. ZIP import remains available offline. Online updates cannot downgrade; failures or cancellations preserve installed artwork, and old category folders remain as backups. Updated selections replace rather than merge old works. Successful installation reads back the edition, hot-reloads gallery data and removes that package's download cache.

The full source library may remain in the project's `windows-app/AIBotBridge/Assets/DailyArt`, outside public packages. Imported online curated editions live in the user gallery directory and take playback priority. Updating a curated category does not modify the project's full source library. The window separates update status from installed and available edition details to keep version information readable.

Windows AI-bot 0.6.3, TAB5 0.2.156-ui and annual curated collection edition 2026.10.09 are published. Packs support 0.6.0+ / .145+. Open Device Center → Bridge Settings → Software/Firmware → Artwork Collections, select a ZIP from the [complete download center](../DOWNLOADS.en.md), and import it without extraction. Select the corresponding screensaver on TAB5. Keep the computer and bridge running for initial synchronization. The current TAB5 release provides 366 works per curated category.

Both collections are optional; missing packs do not affect other features. Imports validate paths, hashes, catalogs and JPEG dimensions before replacing a collection. Failed/cancelled imports retain the old collection. Data lives in the Windows user-data directory and survives application upgrades; replaced collections are retained as backups. Existing app-folder galleries remain supported, with imported category packs taking priority.

Only the annual curated edition is distributed; the full library stays local. Paintings: 366 works / 860 layout images / 211.15 MiB. Calligraphy: 366 works / 1,782 layout images / 426.37 MiB. Calligraphy has 891 pages, versus 430 for painting, with landscape and portrait layouts for each page. Work counts exclude repeated pages, layouts and components of the same album or set. Each pack includes per-work attribution, museum, source, license and derivative records. Verified Chinese equivalents appear in captions; otherwise original collection titles remain. Project licensing does not replace image licensing. Import collection updates independently of firmware updates; browser guide/detail previews remain outside device integration.

Bridge 0.6.1 / TAB5 .149 repairs wireless image synchronization and caches recently viewed images. Cold BLE downloads still take seconds: two runs of the same portrait took 7.26 / 8.54 seconds, without an under-eight-second guarantee. Cache hits avoid downloading; restarts or reclamation require another synchronization. Explicit BLE-only mode temporarily pauses Wi-Fi during downloads and restores it afterward; automatic-mode fallback remains unchanged. See the [hardware record](TAB5-BLE-GALLERY-148.md).

</details>
