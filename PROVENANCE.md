# Provenance

AI-bot is a new implementation maintained by 姚有忠.

The Windows NetEase playback adapter uses two instruction signatures and playback
field layout facts from Kyle's MIT-licensed reference at
`3a97a4b5c8b673ce2f69b0205adbc6a3ac861ba5`. The locator, checked process reads,
metadata matching and bridge integration were independently written. The original
MIT notice and exact origin are retained in `licenses/netease-clock-reference/`.

The QQ Music reader independently implements bounded reads and PE relocation using
the x86 initialization signature and current-song layout documented in the same
MIT reference. Its exact origin and original notice are retained separately in
`licenses/qqmusic-clock-reference/`. System-session selection and browser playback
capture are original project code using public Windows/HTML media interfaces.

The product direction and compatibility contract were informed by the observable behavior and documented requirements of an earlier personal ESP8266 status-clock project. AI-bot does not import that project's Git history or any third-party-authored source, documentation, screenshot, logo, sprite, or binary asset. Protocol names, source layout, user-interface text, and the clean-room implementation are written for AI-bot.

Some later functionality in the earlier fork has commits authored by the AI-bot maintainer. Such code may only be reused after file- and dependency-level provenance review confirms that the reused expression is owned by that maintainer; otherwise only behavior is treated as a specification and the implementation is rewritten.

## Maintainer-authored migration, 2026-09-08

`windows-app/AIBotBridge/CompletionChime.cs` is the maintainer-authored procedural
WAV synthesizer introduced at `c8b32d86b1da30c058dbd2e3590e479660d9d1b5`.
Full-file blame attributes it to 姚有忠; no recorded audio was imported.
The surrounding legacy `StatusService.cs` has mixed upstream authorship and was
not copied. AI-bot's bounded incremental lifecycle reader is independently written.

`windows-app/AIBotBridge/Assets/app-icon.ico` is the maintainer's robot icon,
generated on 2026-08-24 for the explicit request “小机器人样式”. The original
generation and ICO conversion record was reviewed in local task
`01a03295-faf1-7f02-87aa-c3942ca274c7`; the imported ICO hash matches the old
application's asset. This is not the upstream `happy-mac.png` or any vendor logo.

The macOS bridge reuses this same reviewed robot through
`scripts/build_macos_app_icon.py`. Matching PNG frames are retained; larger
macOS icon representations only resize the largest source frame. `AppIcon.icns`
is generated during packaging and is not a new artwork source.

macOS 桥接通过上述脚本复用同一已核验机器人图标；匹配尺寸沿用原 PNG，
大尺寸仅缩放原有最大帧。`AppIcon.icns` 在打包时生成，不另行引入图像来源。

The following narrowly scoped source was reviewed against the local legacy checkout at
`2efab1d0661e24b926c0fef808522d427a0cd5d0`. File/line blame attributes the migrated
code to 姚有忠; this does not authorize copying the surrounding third-party framework.

- `windows-app/AIBotBridge/MigratedWeather/WeatherMonitor.cs` and `CredentialStore.cs`:
  legacy files under `windows-app/AIClockBridge/`, introduced in
  `2dafc6b64d8c230be0320093d331c912f59b989a`.
- `MigratedWeather/WindowsLocation.cs` and `WeatherSettingsForm.cs`: same legacy directory,
  introduced in `512707360a40e5029f5ec7f6d5d408b1f95f7e14`.
- `firmware/src/WeatherAnimations.h`: maintainer-authored geometric weather animations
  from legacy `firmware/src/main.cpp`, lines 2274–2455 at the reviewed checkout.
  `WeatherAnimations.cs` ports that geometry to the independent GDI drawing adapter.
  No bitmap, vendor logo, main pet sprite, or framework source was included.
- `windows-app/AIBotBridge/MigratedDomestic/DomesticQuotaService.cs`: reviewed in full
  (1866 legacy lines), introduced at `2dafc6b64d8c230be0320093d331c912f59b989a`.
  Git blame contains only the maintainer's 姚有忠 / yaoyouzhong aliases with the
  same maintainer email. Reuses the provider authorization UI, response and DOM
  parsing, rate-limit backoff and browser recovery. Settings and CredentialStore
  resolve to the audited adapters, not the legacy application framework.
  The cache is `AI-bot/domestic-provider-cache.json`; WebView2 uses the independent
  `AI-bot/quota-auth-profile`. Existing legacy caches and named credentials are
  read-only fallbacks. Raw browser/exception diagnostic text is not logged.

Integration changes: independent settings adapter and lifecycle; AI-bot cache and
credential targets; read-only legacy display-cache/credential fallback; RGB565 encoding
through AI-bot's existing little-endian encoder; QWeather host validation with redirects
disabled. No legacy settings or credentials are written by migration tools. The legacy
settings framework, RGB565 implementation, and firmware globals were not imported.

Third-party libraries are consumed through their normal package managers and retain their own licenses. See `THIRD_PARTY_NOTICES.md`.

Solar-term network updates now read public date facts from the Chinese Academy of Sciences, Purple Mountain Observatory official annual-calendar index. The Swift/C# index parsers, year/month/term validation and cache conversion are independent AI-bot implementations; no source documents, layouts, logos or publisher code are bundled. Mac uses system PDFKit, while Windows consumes unmodified PdfPig 0.1.16 through NuGet with its full original license and embedded component notices retained. See `licenses/pmo-calendar/NOTICE.md`, `licenses/pdfpig/` and `docs/DATA_SOURCES.md`. An initially evaluated NAOJ implementation was withdrawn before packaging or deployment after the user's domestic-source preference; it is not a product dependency or network fallback.

节气联网更新改读中国科学院紫金山天文台官方年度日历的公开日期事实，索引读取、年月／节气校验和缓存转换均由本项目独立编写。不捆绑来源文档、排版、标志或发布方代码。Mac 使用系统 PDFKit；Windows 通过 NuGet 使用未修改的 PdfPig 0.1.16 并保留完整原许可。先前试验的日本源在打包及部署前已撤回，不作为产品依赖或联网回退。

Claude, Codex, OpenAI, Anthropic, and other product names are referenced to describe interoperability. AI-bot is not affiliated with or endorsed by those vendors. Application and firmware packages do not bundle vendor logos. The media-only mascot preview described below carries a separately attributed Codex mark.

The built-in `BYTE SPROUT` pixel pet is drawn from geometric primitives in AI-bot firmware. It does not contain or derive from an imported sprite, screenshot, character, or logo.

The Mac preview's existing original geometry is shared by `MacByteSprout` and
encoded in memory as a two-frame TAB5 fallback when no user animation exists.
No external artwork, vendor mark or generated bitmap is added to the app bundle.

## Source closeout review, 2026-09-10

Rechecked full-file blame at the legacy commit above: CompletionChime 84 lines,
WeatherMonitor 705, CredentialStore 79, WindowsLocation 26, WeatherSettingsForm 207,
and the weather-animation range 2274-2455 (182 lines) identify 姚有忠. The domestic
service identifies 姚有忠 for 1689 lines and yaoyouzhong for 177 lines.

A mechanical comparison of 10 consecutive nonblank normalized source lines
(blocks longer than 220 characters) found shared blocks only in the listed
maintainer migrations and the C# weather-animation port. It did not identify an
additional copied upstream implementation. This is a bounded similarity check,
not proof of copyright ownership or a complete derivative-work analysis.

The bundled application artwork is the reviewed robot ICO and its generated macOS ICNS representation; the source ICO SHA-256 is pinned in
`licenses/materials.json`. The similarly named `TestFixtures/claude_sprite.h` is
three synthetic red/green/blue pixels, not a legacy sprite; its LF-normalized hash
is also checked. Runtime importers, backup extraction and comparison tools contain
code only; they do not authorize public redistribution of user-provided assets.

Third-party license documents under `licenses/` are deliberately retained from
their named upstream sources with exact hashes. Firmware source materials collect
package-manager dependencies separately from AI-bot's own source archive; they
retain their original licensing and are not imported legacy project history.

## Homepage illustrations, 2026-09-10

`docs/assets/hero.zh.svg`, `hero.en.svg` and `scenes.svg` are original vector
illustrations generated by `tools/build_readme_art.py` for this repository.
The device is a conceptual enclosure; the geometric BYTE SPROUT pet follows
AI-bot's original primitive-based design. All displayed figures are fictional
examples, not screenshots or measured results. No third-party logo, photograph,
sprite, external image or font file is embedded. These illustrations are
covered by the repository MIT license; their hashes are pinned in
`licenses/materials.json`.

## Documentation captures and built-in fallback, 2026-09-10

`docs/assets/screens/*.png` are reviewed offline renders of AI-bot's own Windows
pages and controls, generated by `tools/doc-capture` against the current source.
All displayed account/market/activity values are synthetic. An isolated profile
prevents private pets, page logos, credentials and user settings from loading;
the authorization browser and online gallery are disabled. No third-party art or
vendor webpage is embedded. Each approved filename and SHA-256 is checked by the
public-content guard and `licenses/materials.json`. See `docs/SCREENSHOTS.md`.

`docs/assets/guides/*.svg` are original procedural workflow diagrams generated by
`tools/build_guide_art.py`. They are labeled as illustrations, not screenshots.

The Windows and Mac BYTE SPROUT fallback uses the same original primitive-based
geometry already present in AI-bot firmware. No external sprite was imported.
These project-authored diagrams and captures are covered by the repository MIT
license; third-party components retain their own licenses.

## TAB5 developer snapshot, 2026-10-03

`docs/development/TAB5-0.2.89-ui-source.zip` extracts the previously published
TAB5 source/materials package. All 608 tracked project files match that package
and the clean independently authored TAB5 source commit
`2c45bbbc9e5aef1c4e57576096bd15fd4a304d92` byte-for-byte. Own code is MIT licensed;
original component/font licenses, dependency locks, actual sdkconfig and SDK patch
are retained. No Git history is imported. Downloaded managed dependency trees,
compilers and caches are omitted; see `docs/development/TAB5-SOURCE.md` for scope.
The exact archive SHA-256 is pinned in `licenses/materials.json`.

## Selected mascot media, 2026-10-03

`docs/assets/pet/AI-bot-mascot.png` is the 1254×1254 transparent master selected
by the maintainer in this task. Its SHA-256 is
`d5b150a9d5382dedb4b6464efc9119b36cc94757f8193d0f3a026d2e62217533`.
The pointed periwinkle character was generated using the built-in image generator;
the six animation poses and the moving mint sunglass reflections were generated
from that selected artwork. No Petdex or legacy character frames were copied.
The reference laptop Codex mark was taken from the user's existing local Codex
page-logo preview. It remains a vendor identifier, separate from AI-bot's generated
character contributions and outside the project MIT grant.

`tools/media-design` converts the generated 3×2 sheet into an aligned six-frame GIF.
The existing product importer verifies six distinct 112×112 RGB565 frames with a
2.4-second cycle. `tools/doc-capture --design-pet <gif>` selects this artwork only
inside its isolated documentation profile, and emits complete native UI frames
for the film so quotas and reset-credit badges retain their original draw order.
Static screenshots and the film label the mascot as a design preview. Production
application/firmware sources, defaults, private selections and devices are unchanged.

The master, GIF and optional ZIP have exact path/hash records in
`licenses/materials.json`. Runtime APET files remain excluded. The isolated video
project retains the animation source sheet, generation prompts and conversion
records for reproduction. Original vendor marks are not claimed as project artwork.

## Artwork collection expansion / 图库扩充 — 2026-10-08

The first expansion batch reached 2,262 independent works (1,765 paintings / 497 calligraphy works), including 180 confirmed Chinese paintings and 16 newly complete Chinese painting albums. The batch adds 1,449 works using 1,668 originals. Per-work official records and per-image original hashes are retained; 1,583 new CMA image URLs were matched to official metadata. All 3,804 previous native image hashes remain unchanged. Complete albums, six-scroll calligraphy and four-season panel groups count once. Local source integration and annual/native-image checks passed; hardware installation is not asserted.

第一批扩充后的本地博物馆图库为2262件独立作品（名画1765／书法497），中国画180件，本批含16册完整中国画册页。新增1449件作品使用1668张原图，1583条克利夫兰图像URL与官方记录逐一匹配；原有3804张原生图片哈希保持一致。册页、六屏书法及四季竹图各计一件。来源、审查与验证见[扩充说明](docs/art-guides/EXPANSION-2026-10-08.md)，不冒称已安装到设备。

## Calligraphy supplement, 2026.10.08.1 / 书法追加批次

A further 370 independent calligraphy works use 675 qualified official photographs: Tokyo National Museum / ColBase 98, Metropolitan Museum of Art 55, Princeton University Art Museum 1, Cleveland Museum of Art 3, and Minneapolis Institute of Art 213. Museum culture/place fields identify 108 as Chinese, 261 as Japanese and one as Korean; artist nationality is not substituted for object attribution. All selected image URLs match official records, original hashes are unique, and all 7,754 preceding native images remain unchanged. The resulting local catalogue has 2,632 works (1,765 paintings / 867 calligraphy works) and 9,744 native JPEGs.

书法追加370件独立作品，使用675张馆方原图，来源及分布如上。按馆方文化／制作地字段统计，新增中国书法108件、日本书法261件、韩国书法1件。对联、三联幅、四屏、十二屏和整册各计一件；保留实际左右顺序、拓本、摹写、前半及传称等限定，不把碑刻时代冒称拓印时代。源图URL、许可、分辨率、校验值与组件身份分别核对；本地目录与隔离导入的验证不替代硬件安装。详见[扩充报告](docs/art-guides/EXPANSION-2026-10-08.md)。
