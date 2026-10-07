# Third-party notices

AI-bot's own source code, including its Windows/macOS bridges and ESP8266/TAB5 firmware, is licensed under the MIT License. Dependencies remain under their respective licenses. See [TAB5 scope](docs/TAB5-LICENSE-SCOPE.md); TAB5 packages retain their component notices separately. Third-party service marks are not covered by the project's MIT grant.

| Component | Use | License/source |
| --- | --- | --- |
| .NET and `System.IO.Ports` | Windows bridge runtime and serial transport | MIT, https://github.com/dotnet/runtime |
| Microsoft.Web.WebView2 1.0.4078.44 | Authorization browser control/loader | Package's Microsoft BSD-style LICENSE.txt; browser Runtime installed separately under its Microsoft terms, https://developer.microsoft.com/microsoft-edge/webview2/ |
| Microsoft.Windows.SDK.NET.Ref 10.0.19041.56 | Windows SDK .NET projection and WinRT runtime DLLs | Windows SDK terms, https://aka.ms/WinSDKLicenseURL; both net8.0 DLLs explicitly listed at https://learn.microsoft.com/en-us/legal/windows-sdk/redist#microsoftwindowssdknetref |
| ESP8266 Arduino core | Firmware framework | LGPL-2.1 and component-specific notices, https://github.com/esp8266/Arduino |
| TFT_eSPI | ST7789 display driver | FreeBSD/MIT/BSD component notices, https://github.com/Bodmer/TFT_eSPI |
| ArduinoJson | Firmware JSON parser | MIT, https://github.com/bblanchon/ArduinoJson |
| WiFiManager | ESP8266 captive Wi-Fi configuration portal | MIT, https://github.com/tzapu/WiFiManager |
| Open-Meteo API data | Weather, air quality, and geocoding | CC BY 4.0, https://open-meteo.com/en/license |
| Espressif NONOS SDK | Wi-Fi and ESP8266 runtime in Arduino core | Espressif MIT License with ESP8266-only condition; core tools/sdk/License |
| LittleFS | Device filesystem | BSD-3-Clause; core libraries/LittleFS/lib/littlefs/LICENSE.md |
| lwIP | Device IP stack | BSD notice in core tools/sdk/lwip2/include/lwip/init.h |
| umm_malloc, libb64, eboot, uzlib, BearSSL and other bundled core components | Core utilities; archive includes component sources/notices | Original per-component notices retained; inclusion in source materials does not imply every component is linked |
| GCC 10.3.0 runtime | Linked compiler support code | GPL-3.0 with GCC Runtime Library Exception 3.1; licenses/firmware-toolchain/ |
| newlib 4.0.0 | Embedded C runtime | Collection of permissive notices in licenses/firmware-toolchain/COPYING.NEWLIB |

Release archives must preserve the license texts delivered by package managers when their terms require redistribution with binary forms.

Windows packaging collects notices from actual restored NuGet packages, plus the
targeting pack excluded from ordinary NuGet library lists. The two SDK DLLs must
match the reviewed originals by SHA-256. Complete SDK terms and REDIST evidence
are bundled, not replaced by this table or MIT. See [distribution terms](docs/DISTRIBUTION_TERMS.md).

Firmware packages include application and actual core/library sources for modifying
and rebuilding the LGPL-linked work, component notices, build configuration and
hashes. Original component notices take precedence over this summary. Toolchain
executables are installed separately, not redistributed in the materials archive.
See [firmware materials](docs/FIRMWARE_PACKAGE.md).

## Windows installer

The setup executable is generated with Inno Setup 6.4.3 (Jordan Russell and
Martijn Laan). Its original license is included as `licenses/inno-setup/license.txt`.
The compiler is restored from the hash-pinned, unofficial Tools.InnoSetup NuGet
redistribution; it is a build tool, not an application runtime dependency.
Microsoft .NET 8 Desktop Runtime and the Evergreen WebView2 bootstrapper retain
Microsoft's respective license terms. The .NET installer is downloaded only when missing and checked against a build-pinned hash; the unmodified Microsoft-signed WebView2 bootstrapper is bundled. Neither is licensed under this project's MIT license.
`INSTALLER_DEPENDENCIES.json` records their sources and checksums. Internet access is required when either runtime is missing. Uninstalling AI-bot retains these shared
runtimes and the user's AppData.

## Graphical firmware tool

Windows and Apple Silicon Mac packages include unmodified Espressif esptool 4.9.1
standalone archives fetched from the official GitHub release at build time.
Each platform pins the archive SHA-256 reported by that release and verifies it
before extraction/execution. The build also includes the matching esptool source
distribution (with its GPL license) and an ORIGIN.json hash/source record.
Original bundled dependency notices remain inside the unmodified tool archive.
Runtime flashing requires no tool download. Generated archives are not committed.
The flasher icon is original geometric artwork from tools/build_flash_icon.py.
Source, licenses and releases:
https://github.com/espressif/esptool/tree/v4.9.1
https://github.com/espressif/esptool/releases/tag/v4.9.1

Device backups are private runtime data, never distribution materials.

## TAB5 voice audio library

The Windows TAB5 voice bridge uses NAudio.Wasapi and NAudio.Core 2.2.1 (MIT), by Mark Heath and contributors. Source: https://github.com/naudio/NAudio. The original MIT notice is retained in `licenses/naudio/license.txt`. VB-CABLE is a separately installed third-party driver and is not bundled or licensed by this repository.

## Mainland China holiday dataset

TAB5 checks the publicly maintained holiday-cn JSON dataset by NateScarlet (MIT): https://github.com/NateScarlet/holiday-cn. Each accepted year must cite a State Council notice; this is a third-party dataset, not an official government API. The 2026 test fixture comes from that project. Its original license is retained in `licenses/holiday-cn/LICENSE`. Cached schedules survive network failures; unpublished years are never generated from predictions.

## TAB5 monthly calendar artwork and font

The .124 monthly-calendar flower illustrations were generated for this project
without external image references. Original transparent PNGs, subject prompts,
source hashes and the LVGL encoding script are retained in the separate TAB5
source under `firmware/assets/calendar-flowers/` and
`scripts/generate-calendar-flowers.py`. They are project-generated artwork,
not images copied from another repository. The calendar labels use the existing
Noto Sans CJK SC Regular source (SIL OFL 1.1), SHA-256
`2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b`;
the TAB5 source retains its notice and subset generator.

The classical left cover uses LXGW WenKai Regular v1.522 under SIL OFL 1.1.
Copyright 2021–2026 LXGW and 2020 The Klee Project Authors. Upstream:
https://github.com/lxgw/LxgwWenKai/releases/tag/v1.522 (`LXGWWenKai-Regular.ttf`).
Source SHA-256: `39ad71264b588165b469e35e6afb162a378dacd1f95348160240ba9038ac3009`.
The TAB5 source retains the complete license at `firmware/main/CalendarWenKai-OFL.txt`
and the subset generator at `scripts/generate-calendar-font.py`.
The custom subsets are named `tab5_calendar_month_48`, `tab5_calendar_verse_24`
and `tab5_calendar_meta_18` (48/24/18 px, 2 bpp, losslessly compressed bitmaps).
Calendar glyphs and the 72/96/144 px numeral subsets use lossless bitmap compression.
Numeral outlines, grayscale and metrics are unchanged; general Chinese UI fonts remain uncompressed.
The poem body selects among three distinct OFL calligraphic faces by solar term:
LXGW WenKai, Ma Shan Zheng and Zhi Mang Xing. Each poem uses one face throughout;
only its required glyphs are included. Font/source coverage is checked before generation.
Ma Shan Zheng: Copyright 2018 The Ma Shan Zheng Project Authors;
Google Fonts commit `406197b91ff39a93061c2c2eeaee67ddf2ae1f0d`, font SHA-256
`6d2546bb189c732a8ca29af9e22457b152387d158aa459e4ac2ce1e51788b7fb`.
Zhi Mang Xing: Copyright 2018 The Zhi Mang Xing Project Authors;
Google Fonts commit `b12c22f97f4769802373d8de6a0f4115eabb9a24`, font SHA-256
`644e0cae9b40f0b10ab729a01bd32032e3973bac22be3dccae01bf6ae7fde969`.
The TAB5 source retains exact download URLs and hashes in
`firmware/assets/calendar-calligraphy-sources.json`, with complete upstream licenses
in `firmware/main/mashanzheng-OFL.txt` and `firmware/main/zhimangxing-OFL.txt`.
The 24 classical poem texts are public-domain originals; source links and variant notes
are recorded in the TAB5 source at `firmware/assets/calendar-poems.json`.
No modern translations or commentaries are embedded. Seasonal pairings are editorial
selections and do not imply that all poems were written for the named solar term.

## TAB5 annual screensaver font

TAB5 .118 annual screensaver digits use a subset of Nunito by The Nunito Project Authors (2014), SIL Open Font License 1.1. Source is pinned to Google Fonts commit `8b0a1d0f5983c89bc2b93f1b5fb55f9e252744b5`, `ofl/nunito/Nunito[wght].ttf`, SHA-256 `bb55a5ca5c2042335b3991af27c4d0705d0ef41cac6164ac737fd8f2a1e85207`. The separate TAB5 source includes the full license in `firmware/main/Nunito-OFL.txt` and its reproducible subset generator. This font is not embedded in the Windows bridge.

Its rounded Chinese labels use Resource Han Rounded CN Bold v0.990 by Cyano Hao (2018–2019), with portions by Adobe (2014, 2015, 2018), SIL OFL 1.1. Source: https://github.com/CyanoHao/Resource-Han-Rounded/releases/tag/v0.990 (`RHR-CN-0.990.7z`); font SHA-256 `f713907a21a10701cd68a7ce3e345ccdce46c789e1809d65ace54e095d7107c3`. The TAB5 source retains the license in `firmware/main/ResourceHanRounded-OFL.txt` and generates only the annual UI glyph subset.

## TAB5 daily art gallery

`windows-app/AIBotBridge/Assets/DailyArt/` contains native-display layouts made
from documented public-domain, CC0 or CC BY 4.0 reproductions of
paintings and calligraphy.
These underlying artworks are not covered by AI-bot's MIT ownership claim.
Individual museum records, image-reproduction sources, license designations,
source dimensions/checksums and explicitly recorded display crops are retained
in `catalog.json`, `provenance.json`, `selection.json` and `NOTICE.md` in that folder.
The Met and Cleveland Museum of Art assets use their Open Access CC0 images
(https://www.clevelandart.org/open-access); other image reproductions retain
their documented Commons Public Domain / CC0 status. The Princeton Zheng Xie
image follows https://artmuseum.princeton.edu/art/our-collections/image-use-and-access
(public-domain image, with the credit: Image courtesy of the Princeton University
Art Museum). Its photographed background and color chart are removed using the
explicit crop in selection.json; the complete written sheet and seals remain.
The NPM Su Shi and Wu Ju additions use CC BY 4.0 images extracted from
documented Commons PDF mirrors without re-encoding. Attribution: 國立故宮博物院，
台北，CC BY 4.0 @ www.npm.gov.tw. License: https://creativecommons.org/licenses/by/4.0/.
The display derivatives are resized, captioned and JPEG-encoded; explicit crops,
full work attribution and PDF SHA-256 are retained in selection.json.
The Minneapolis Institute of Art additions use images individually marked Public
Domain, under https://github.com/artsmia/collection-info/blob/gh-pages/open-access.md.
Credit: Minneapolis Institute of Art. Complete multi-leaf works are grouped;
each additional source image retains its source URL, dimensions, crop and SHA-256.
Paris Musées / Musée Cernuschi additions use official download-package images
individually marked CC0 in the museum's IIIF manifests; selection.json retains
the manifest URL, archive download URL and original archive member name.
License: https://creativecommons.org/publicdomain/zero/1.0/.
The Rijksmuseum Zhang Jian couplet is marked Public domain at
https://id.rijksmuseum.nl/20075101. Its two official IIIF images are downsampled
within the service's image-area limit. Credit: Rijksmuseum / Royal Asian Art Society.
Each complete pair is one artwork; both image sources remain individually documented.
Modern commentary is not
embedded. Resampling, display crops, captions and JPEG encoding do not repaint
or restore the artworks. Captions are rasterized with the previously documented
OFL-licensed LXGW WenKai; no additional font binary is shipped with this gallery.

## Selected mascot design media

The optional PNG/GIF under `docs/assets/pet/` and selected documentation/video
previews contain a Codex product mark on the laptop. That identifier remains the
property of its respective owner and is outside AI-bot's MIT grant. AI-bot is not
affiliated with or endorsed by OpenAI. The character and animation contributions
were generated for this project; no legacy or Petdex animation was copied.
See `PROVENANCE.md` and the same-name notice accompanying the GIF. These media
assets are optional and do not replace installed application/firmware defaults.

### ColBase / Tokyo National Museum calligraphy

The 72 ColBase images added to the daily calligraphy gallery use CC BY 4.0 under [ColBase terms](https://colbase.nich.go.jp/documents/term?locale=zh). Credit: ColBase / Tokyo National Museum. Each frame states that AI-bot resized and laid out the source image; the result is not presented as museum-produced. The complete source URLs, Chinese titles, qualified attribution where applicable, hashes and sizes are in Assets/DailyArt/selection.json, provenance.json and NOTICE.md.

新增72件ColBase／东京国立博物馆书法依CC BY 4.0署名使用；画面标注经排版加工，逐件来源、归属、尺寸和校验值见上述文件。

The next ColBase batch adds 47 works / 118 original images under the same terms, for 119 ColBase works in total. Complete albums, multi-panel sets and couplets are counted once; all individual image sources and modification credit remain recorded.

后续新增47件／118张原图，ColBase来源累计119件；8册册页、3组多屏与1副对联各计一件，逐图来源与加工署名完整保留。

The album/handscroll batch adds 45 works / 328 museum originals under the same ColBase terms, bringing ColBase to 164 works. Nineteen albums and twenty-six scroll/section-based works each count once. Original authorship, sequence, attribution qualifiers and five colophon-separating crops are recorded per work and per image.

册页与手卷批次再增45件／328张原图，ColBase累计164件。19册册页与26件手卷／分段作品各计一件；逐件核对原作者、顺序、归属限定并记录5处排除后人题跋的裁切。

The final annual batch adds 63 ColBase works / 582 originals, followed by two short works / two originals, bringing ColBase to 229 works. Thirty-four complete rubbing albums in the annual batch preserve historical losses and distinguish inscription dates from impression dates. All use the same documented CC BY 4.0 terms and modification credit.

年度补齐批次新增63件／582张ColBase原图，短幅批次再增2件／2张原图，ColBase累计229件。年度批次含34件完整拓本，保留残损并区分碑刻、拓印年代；继续按CC BY 4.0署名并说明排版加工。

The short batch also adds 26 Mia works (37 public-domain originals) and 17 Met works (17 CC0 originals). Eight complete couplets and one four-panel set are grouped and laid out two panels per page, preserving all text and seals. Original image URLs and hashes remain individually recorded. Museum romanized author names are retained where the Chinese characters have not been verified. Eight additional Mia reserves remain local and outside the distribution catalog.

短幅批次另含明尼阿波利斯26件（37张Public Domain原图）和大都会17件（17张CC0原图）。8副完整对联与1组四屏各算一件，每页并排两屏，保留全部文字、落款及逐图来源和校验值。未核实汉字的作者沿用馆方罗马字姓名。另8件Mia候选仅留本地，不在分发catalog内。

## Bosch BMI270 orientation sensor

TAB5 portrait art mode uses Bosch Sensortec BMI270 SensorAPI under BSD-3-Clause. Copyright (c) 2023 Bosch Sensortec GmbH. The unmodified driver subset and official maximum-FIFO configuration come from M5Stack M5Tab5-UserDemo commit b4e356bc491ca070d54004718dad789c07d5fc93, platforms/tab5/components/sensor_bmi270. The TAB5 firmware retains full license text and source provenance in firmware/components/bosch_bmi270/LICENSE and SOURCE.md. Only accelerometer measurements are enabled; no copied third-party application UI is included.

TAB5艺术屏保的方向检测使用Bosch官方BMI270驱动（BSD-3-Clause），完整许可与固定来源保留于独立TAB5工程上述路径。
