# TAB5 firmware changelog

TAB5 versions are independent of the desktop bridge and ESP8266. `versions/TAB5` records the release target; the image's embedded version and matching notes must agree with it before publication.

## 0.2.190-ui - 2026-10-13

### Desktop-initiated upgrades

- Accept bounded requests from the paired bridge. Reuse local draft/busy guards on LVGL, require an authenticated commit, and atomically bind installation to the authorized offer ID/SHA-256. Expired, duplicate, withdrawn or mismatched intents cannot start an upgrade.
- Retain device-triggered installation, OTA slots/rollback, transport preferences and TF files. Windows 0.6.6 exposes Start Upgrade for compatible devices; older firmware needs one device-confirmed installation. Physical trigger acceptance is recorded separately.

## 0.2.189-ui - 2026-10-13

### Artwork reading

- Sample unscaled RGB565/ARGB8888 layers at 90/180/270 degrees in bounded tiles, covering the actual portrait reader's transparent layer and reducing repeated arithmetic and distant PSRAM row reads. Preserve LVGL's sampling coordinates, colors and edge alpha; other formats, scales and angles retain the original path. No additional full-page allocation.
- Add numeric preparation, layout and first-refresh timings for opening artwork details. Text, orientation, scrolling, return behavior and TF formats remain compatible. Actual responsiveness requires separate hardware acceptance.

### Gallery transfer

- Negotiate a bounded USB request-ready wait. Publishing a request or disconnecting wakes the reader immediately, reducing desktop delays after empty polls. Preserve framing, generation isolation, deadlines and legacy firmware behavior.
- Reuse an exclusive HTTP client and valid connection across successful background gallery saves to avoid per-image reconnection and TCP slow start. Changed endpoints, lifetime/request limits, cancellation and incomplete or failed transfers release the connection. Each request retains fresh authentication; verified checkpoints and TF files remain compatible. Measure actual gains separately using the same sample set.

## 0.2.188-ui - 2026-10-13

### Wireless startup

- Initialize the unmodified ESP-Hosted SDK after the Tab5 I/O-expander power rail is ready, before large USB/audio/UI allocations. The pinned automatic constructor is excluded by the application build; source-layout changes fail configuration rather than silently restoring the old order.
- On a software restart, power-cycle the C6 before creating radio transport tasks. Ordinary association failures never reset a live Wi-Fi/BLE module; credentials and installed TF collections remain intact. Add numeric startup diagnostics. Native ordering and failure tests are separate from OTA recovery acceptance.

## 0.2.187-ui - 2026-10-13

### USB receive batching

- The installed TinyUSB CDC driver defaults to one 64-byte receive submission even with an 8 KiB endpoint buffer. Negotiate up to 4 KiB submissions for binary bodies with a supporting bridge, retaining one-packet legacy control and default behavior.
- Bind pending DMA to its DTR generation before submission. Discard stale bulk completions during reopen and use a short discovery barrier; preserve existing same-core task ordering, framing, authentication and resume data.
- Reject a receive notification sampled after its caller's timestamp rather than reporting an unsigned multi-billion-microsecond wait. Hardware throughput acceptance remains separate.

## 0.2.186-ui - 2026-10-13

### Parallel gallery reception

- Read HTTP responses in batches up to 16 KiB through a bounded 64 KiB PSRAM receiver, separate from authentication and the existing concurrent card writer. Retain synchronous fallback, safe cancellation, authenticated resumable prefixes and complete-file verification.
- Match the TCP receive window and mailbox using the ESP-IDF documented sizing formula, preferring PSRAM for Wi-Fi/lwIP allocations. Existing saved galleries, credentials and cache choices remain compatible.
- Add optional network read/wait and passive USB FIFO/wakeup/hardware counters. No USB endpoint or scheduling change is included. Native tests and builds are distinct from physical throughput and sustained BLE acceptance; hardware gains remain pending.

## 0.2.185-ui - 2026-10-13

### USB reception and Wi-Fi recovery

- Use the installed official USB component's recommended 8 KiB CDC endpoint/RX buffers, batched binary reception and data-arrival wakeups. Retain same-core task priorities, disconnect-generation isolation and protocol compatibility.
- Run targeted no-AP scans with multiple saved networks too. Wait for association and IP assignment before retrying or changing profiles; scan all channels and prefer stronger matching access points, preserving network credentials.
- Keep saved galleries, complete guides and resumable prefixes without a full resync. Build/regression checks do not substitute for physical Wi-Fi recovery, USB throughput and sustained BLE acceptance.

## 0.2.184-ui - 2026-10-13

### Gallery storage and diagnostics

- Transfer aligned multi-sector spans through a bounded 32 KiB aligned buffer, separating partial-sector edges in existing cache files. Keep image/text formats, checksums, durable checkpoints, alternate slots and resume compatibility.
- Report card read/write/fsync and authenticated RPC stage timings for the same eight isolated samples. Existing installed galleries are preserved; actual speed and sustained BLE behavior require physical acceptance with the matching bridge.

## 0.2.183-ui - 2026-10-13

- Explicit sync measurement now uses the existing hidden-page cache trim even after both libraries are complete, retaining the 5 MiB safety reserve and avoiding a zero-byte preflight rejection.
- Report distinct preflight failures; exercise the production UI memory preparation with completed libraries and 4 MiB free memory.

## 0.2.182-ui - 2026-10-13

- Add an opt-in gallery sync test to TAB5 connection diagnostics: eight real artwork samples use authenticated receive and the production card writer in an isolated TF directory, preserving installed works and cursors. Report effective bytes, elapsed/receive/write time and overlap; cancellation supported. Requires 0.2.182-ui; physical throughput acceptance remains separate.

- Move image-text gallery navigation beside the bottom page counter in both orientations. Align visible glyph centers and enlarge touch targets to 80×72 without enlarging arrow glyphs. Preserve legacy image-pack caption pixels and paging compatibility.
- Show fresh, bridge-confirmed authenticated BLE standby under active USB in pale blue, separately from amber waiting. Stale reports and physical disconnection cannot retain standby; routing is unchanged.

## 0.2.181-ui - 2026-10-12

- Authenticate continuous Wi-Fi gallery blocks individually and receive concurrently with a bounded, backpressured TF writer. Sync payloads before committing checkpoints; promote AIG2 records without rewriting JPEGs, retaining AIG1 and old resume compatibility. USB state deltas preserve unread/settings/music freshness while avoiding repeated full catalogs.

- Prevent whole-library synchronization from stalling behind retained offscreen UI snapshots. Reclaim only rebuildable offscreen caches and avoid hidden preheating during synchronization; preserve the original 5 MiB worker reserve, active gestures, visible pixels and image/text verification.
- In automatic transport mode prefer a fresh authenticated Wi-Fi path for whole-library reads, with USB fallback. Keep explicit channel selections, foreground priority and per-request binding; validated checkpoints remain reusable across transports.
- Retain .180 side navigation/Wi-Fi diagnostics and .179 complete reading, storage management and range-resume fixes. Performance and wireless recovery still require the latest image's hardware acceptance.

## 0.2.180-ui - 2026-10-12

- Place multi-page painting/calligraphy arrows at the artwork sides for both pack formats. Rotate each small control independently, preserve legacy portrait caption pixels, and hide navigation for single-page works.
- Add numeric device Wi-Fi diagnostics for driver/configuration results. After a disconnected no-AP failure, scan only the saved SSID at most once per 30 seconds; retain network configuration and authentication.

## 0.2.179-ui - 2026-10-12

- Keep manual paging visible for multi-page legacy image-only packs independently of optional guide text. Retain original image captions, offer details only when text is available, and preserve matching complete guides when promoting cached JPEGs to TF.
- Rename the setting to Storage Management and place it below Content Rotation; retain the saved cache choice.
- Resume within unfinished TF gallery images across reconnect/reboot using checksummed prefix records bound to pack revision, work, layout, frame and expected JPEG SHA-256. Reject torn tails or changed content; only complete image/text commits become offline display sources. Completed works/pages remain reusable.
- Checkpoint each 192 KiB and flush smaller verified tails on graceful interruption, reducing per-range storage writes. Abrupt power loss may require retransmitting the uncommitted tail. Keep at most one staging file per category under `aibot/gallery/<category>/download.aip`.
- Reuse a checked complete original across frames/layouts only when date, work, revision and full-text hash match; revalidate after a pack update. Avoid retrying already-persisted background images because of an unrelated RGB reserve check; retain foreground priority.
- Include .178 long-filename fixes, complete readers and the latest 10,535-glyph font. Native resume/integrity/failure preservation, whole-library offline and seven long-original regressions passed; final image and hardware acceptance are recorded separately.

## 0.2.178-ui - 2026-10-12

- Fix .177's disabled FAT long filenames, which prevented creating calligraphy, date and record paths even after mounting. Use heap buffers and preserve the `aibot/` layout, checked records and nonformatting behavior.
- Reject firmware configurations lacking long filenames. The actual SDK FatFs regression covers 128GB FAT32, 32KiB clusters, both categories, temporary-file commits and remount/readback; hardware acceptance is recorded separately.
- Add an independent Storage & Cache settings category with background TF detection, regardless of screensaver or bridge state. Gallery caching defaults to the existing device cache; explicitly select TF to enable complete-library storage. Preserve the choice, roll back failed saves, fall back when TF is unavailable and retain card files when disabled.
- Hide Codex Direct's empty bottom result panel on initial entry. Show compact operation feedback or expand for recognized text, preserving voice, draft and send flows.
- Native host regressions cover storage status, cache choice, save rollback, card removal/reinsertion and Codex interactions. Build and installed-device acceptance are recorded separately. Validate long filename flags during ESP-IDF configuration without falsely rejecting unloaded flags during dependency discovery.
- Include the latest font, preserving existing glyph metrics and pixels and checking coverage against the final annual reading material. Artwork images and reading data remain separate optional packs, with 366 independent works in each category.
- Read complete long originals through bounded, checked reading pages, preserving full text and clearly labelled reference scope. Coordinate with Windows bridge 0.6.6 for the new reading capability; old packs continue to work. Source, packages, build and hardware acceptance are separately verified in the [release record](../RELEASE-0.6.6.md).

## 0.2.177-ui - 2026-10-11

- Automatically synchronize both complete installed collections from Windows over USB/Wi-Fi to TF, including every work/page/layout and complete optional guide. Settings show progress; finished collections remain available offline across reboots/year changes. Resume interrupted work, prioritize foreground operations and reuse identical JPEGs on revision changes; text-only changes transfer no image ranges. BLE retains individual-image synchronization.
- Alternate complete records, excluding corrupt/incomplete/stale revisions. Absent/full/read-only/failed cards fall back to the bridge. Photos and gallery access share one nonformatting mount/lock without deleting unrelated files. Import ZIPs on the computer; unsaved works still require the bridge.
- Keep project-written TF files under `aibot/`, split artwork by category/work, and reserve module directories for photos, persistent data and reproducible caches; create directories only when used.
- Freeze 9,027 mapped font glyphs, adding 11 to public .176 while preserving all 9,016 existing metrics/bitmaps. All 5,557 required characters in the frozen guide snapshot are covered; future content still requires coverage checks.
- New collections remain separate; in-progress artwork/text is excluded. Build, host tests, package validation and TF hardware acceptance are separate records. See the [release record](../RELEASE-0.6.6.md).

## 0.2.176-ui - 2026-10-11

- Consolidate .157–.175 improvements while preserving automatic mode, cycle pages/order/interval and preferences. Extended fonts retain existing glyph metrics/pixels. New collection images and in-progress guide content are excluded.
- Provide nine clock faces and a grid picker, including classic green seven-segment LCD. The day/night face retains a normal 12-hour inner dial and maps its 24-hour outer ring to local daylight/night/current time, with sun/moon sunrise/sunset markers and 06:00/18:00 defaults.
- Use Clock/Timer segmented navigation, a large-number timer view and touch controls, retaining background timing. Keep the date/day countdown as a separate screensaver, hide unselected settings and refine layout/copy.
- Unify top-left back navigation and left-aligned titles, retaining return hierarchy. Make cycle/fixed/automatic display choices mutually exclusive, display only applicable settings, roll back failed saves and protect the last cycle page.
- Balance provider-icon visible areas across overview, selection and rotation; Codex uses 30×29, with additional provider selection/display interactions.
- Synchronize desktop Codex read state and the top-right unread count. Support independent image/text collection updates; pause page/orientation changes while reading and restore the original image/page on return.
- Consolidate actual quota periods, gallery orientation/footer/cache refresh, USB receive isolation/diagnostics and Wi-Fi/BLE recovery. Outstanding transport and sustained stability checks remain explicit.
- Retain .175 boot/Soda evidence as version-specific records; validate this build, previews and packages separately. See the [release record](../RELEASE-0.6.5.md).

## 0.2.175-ui - 2026-10-11

- Give Codex Direct and Settings the same upper-left back arrow. Returning through Direct retains the original Settings/detail entry and its parent destination.
- Place the 30 px, left-aligned Codex Direct title beside the back button, matching Settings.
- Present Carousel / Fixed page / Automatic as three exclusive choices with only their relevant parameters. Mode and page selections stay in Settings; Back opens the chosen page.
- Preserve carousel pages, order and interval. Browsing and changing clock faces retain the automatic/carousel mode; failed saves restore the previous choice, and at least one carousel page remains enabled.
- Full native interaction regressions, ESP-IDF build, display/flash safety and production image validation passed. Device installation and physical acceptance are recorded separately in the [acceptance record](../TAB5-ACCEPTANCE-175.md). No public release.

## 0.2.174-ui - 2026-10-11

- Refine the clock dial, time/date hierarchy and selection layout. Provide nine faces and a grid picker while preserving existing IDs and saved choices.
- Keep ordinary twelve-hour hands inside the day/night face. Its outer ring maps local twenty-four-hour time, daylight, night and the current point; use a sun for sunrise and a moon for sunset, show event times on the right and remove the metadata row beneath the dial.
- Accept dated sunrise/sunset and city timezone data, use 06:00/18:00 defaults when unavailable, and handle valid same-day cache and polar states. Live collection of the new desktop weather fields remains pending.
- Use a classic olive-green LCD face with seven-segment time and separate seconds. Retain Clock / Countdown segments and the independent date screensaver.
- The user installed the update, and the live desktop window reports this version on the device. Complete boot verification and physical appearance acceptance remain pending; see [acceptance record](../TAB5-ACCEPTANCE-174.md). No public release.

## 0.2.173-ui - 2026-10-11

- Replace the clock countdown shortcut with matching Clock / Countdown segments in both headers. The selected page uses a pale blue background.
- Switching to Clock keeps the timer running; returning preserves its remaining time. The running screen retains only large digits, with controls revealed by touch.
- Remove the timer touch hint and the date-settings persistence footnote.

## 0.2.172-ui - 2026-10-11

- Show only large centered digits while the clock timer runs. Tap to reveal controls, which hide after five seconds; paused and completed timers retain their status and controls.
- Refine duration fields, presets and date-countdown name/date/save layout. Hide the other form controls while entering a Chinese event name.
- Show date configuration only for the date-countdown screensaver and move the sound controls up when it is hidden.

## 0.2.171-ui - 2026-10-11

- Add an independent hours/minutes/seconds timer under Apps → Clock, with 5/15/25-minute presets, pause, resume, reset and completion notification. It continues after closing the page and stops on device reboot.
- Add a date countdown screensaver with a locally configured Chinese event name and target date. It counts local calendar days, distinguishes today from past dates and retains settings across restarts.
- Keep both countdowns independent. Event-name Pinyin input is isolated from Codex drafts; the completion chime respects device volume and mute.
- Balance provider logo sizes across overview and model selection, retaining the confirmed 30×29 Codex mark.
- Gallery content remains a separate collection and release; this package imports no new gallery.

## 0.2.170-ui - 2026-10-11

- Use the same 30×29 visible Codex mark in overview and model selection, retaining the 36×36 canvas and other provider marks.
- Expand the reading font for the current reviewed gallery introductions, viewing tips and verified originals; content editions continue to update independently through gallery packs.
- Retain desktop read acknowledgement and TAB5 unread badge synchronization.

## 0.2.169-ui - 2026-10-11

- Normalize the visible Codex logo in overview cards to the same cropped official asset used in the model picker. Keep the 36×36 canvas, alignment and other provider marks unchanged.
- Retain .168 desktop read synchronization; hardware verification recorded a badge change from 1 to hidden after opening the reply on the PC.

## 0.2.168-ui - 2026-10-11

- Installed locally: synchronize desktop read acknowledgements and unread badge; expose actual widget state for hardware verification. Exact ELF / ota_0 VALID boot verification and actual PC-open → badge 1-to-0 verification passed. No public release.

## 0.2.167-ui - 2026-10-10

Installed with user authorization. Exact full ELF / ota_1 VALID boot verification and physical visual acceptance pass; the original six-page, 15-second automatic carousel is restored. No public release. See the [.167 acceptance record](../TAB5-ACCEPTANCE-167.md).

- Use cool/warm low and high temperature panels with directional marks and a static current-temperature range indicator. Missing or invalid data cannot imply a position.
- Add existing official provider logos to all eleven overview model choices, align their centers with the checkboxes and separate them by at least 16 pixels. Normalize Codex's visible mark from the original high-resolution OpenAI asset.
- Place calligraphy original text inside Artwork Details alongside Introduction and Viewing Tips, with a separately labeled explanation. Only fourteen reviewed transcriptions are currently available; unverified works show a clear message.
- Retain the installed .166 daily art cache, orientation and paging behavior, and USB authentication/reconnect fixes. Keep the same partition layout and companion Windows bridge.

## 0.2.164-ui - 2026-10-10

Installed with exact-image authorization and full ELF/ota_1 VALID verification. Current-day cache and physical offline interaction pass; first-fill latency and USB authentication/reconnect timeouts prevent full acceptance. Unpublished; see the [.164 daily-cache and rotation record](../TAB5-ACCEPTANCE-164.md).

- Prefetch every page and both layouts of today's painting and calligraphy into a single-day JPEG cache capped at 10 MiB, retaining exactly two RGB output buffers and a 4 MiB rendering reserve. Reboot or foreground memory reclamation requires refilling; insufficient memory pauses background fill.
- Remove the 30-second manual-page/reader-return block on orientation loading and warm the reverse layout during the hold. Sample posture every 50 ms and confirm 150 ms stability while retaining shake/flat rejection.
- Background synchronization yields to foreground requests; page taps made while busy are retained for execution when idle.
- Display the firmware upgrade badge with a dark green circular background and white arrow. Retain all .163 fixes and the installed painting correction.

## 0.2.163-ui - 2026-10-10

Installed locally with exact-candidate authorization; full ELF/ota_0 VALID verification passes. The user reports usable landscape interaction and no blue flashes, but rotation is too slow. USB/BLE benchmark failures and a USB authentication failure prevent a stability pass; no public release. See the [.163 hardware record](../TAB5-ACCEPTANCE-163.md).

- Release hidden main-page caches when entering painting or calligraphy screensavers, retaining memory for rotated rendering. Existing cache rebuild resumes after exit; ordinary screensavers are unchanged.
- Follow device orientation while reading Details without changing the work, date or frame. Return restores that frame in the current orientation.
- Retain .162 USB reconnect and all earlier optimizations. Rebuild the painting layout from the actual old collection to correct two work differences between the earlier local baseline and the user's gallery. Installation requires approval for the exact candidates.

## 0.2.162-ui - 2026-10-10

Installed locally with exact-hash authorization; embedded ELF and VALID partition verification pass. Hardware screensaver acceptance fails with blue flashes and unresponsive interaction/exit in portrait and landscape. Formal benchmark and sustained observation have not started; no public release occurred. See the [.162 hardware record](../TAB5-ACCEPTANCE-162.md).

- Clear old CDC FIFOs at serial close, retain new connection input during delayed receiver recovery, and block replies from the old receive generation. Require both a ready bus and an open serial session. Authentication and channel fallback remain intact; sustained stability still requires hardware validation.
- Retain all .161 gallery direction, footer and Details behavior. The separate painting pack displays full landscape title, artist and period beside the artwork, matching calligraphy's hierarchy. Production import matches the approved ZIP and calligraphy is unchanged, but two works differ from the actual old collection. Complete annual/portrait preservation fails; a correction candidate is being prepared. Installing firmware alone does not install this pack.

## 0.2.161-ui - 2026-10-10

Installed locally with exact-hash authorization; Wi-Fi OTA and boot checks pass, without public release. The user confirms the landscape footer and portrait Details orientation/Return. USB fails the first two RAM suites and all channels pass the third; sustained USB observation still fails. See the [gallery layout and USB recovery hardware record](../TAB5-ACCEPTANCE-161.md).

- Align portrait Details with the pre-rotated bitmap in both portrait directions and reverse landscape; Return preserves the displayed frame.
- Separate museum credit, centered paging and Details. Hide duplicate landscape titles, dates and the ambiguous first-page control. Rotate only the footer strip and preserve original JPEG bytes.
- Add numeric USB rejection reasons and one bounded raw-state confirmation fallback in the matching bridge. Identity/sequence rejection and transport failures remain failures. Raw recovery has triggered and confirmed on hardware, while other USB failures remain; root cause and sustained stability have not passed acceptance.

## 0.2.160-ui - 2026-10-10

Installed locally with exact-hash authorization. BLE OTA, embedded ELF and VALID boot partition checks passed. All 54 RAM integrity cases across three hardware suites passed; sustained USB observation recorded rejection, disconnect and write timeouts and failed acceptance. The user also reported real gallery orientation/footer defects. See the [.160 hardware record](../TAB5-ACCEPTANCE-160.md).

- Integrate 2632 reviewed artwork guides, binding overview, viewing hints and verified transcriptions to the displayed work. Reading pauses rotation; Return preserves the frame; unknown guides hide the entry.
- Put decrypted replies in PSRAM, transfer body ownership and reject stale connection generations; add numeric RPC/CDC failure diagnostics. Local fault tests passed; sustained hardware stability did not.

## 0.2.159-ui - 2026-10-10

Installed locally with authorization and verified against the exact embedded ELF; not publicly released. The user confirmed the Codex quota screen. USB stability failed acceptance. See the [.159 hardware acceptance record](../TAB5-ACCEPTANCE-159.md) and [transport validation report](../TAB5-TRANSPORT-157.md). Includes the .158 overview, official logos and page spacing.

- Replace the upgrade dot with a rounded 14×14 pixel arrow; show the number of tasks with unread replies on Codex, capped at `9+`. Both use deep green `#21854A` and retain task-specific read clearing.
- Increase TCP send capacity from four to sixteen MSS (23,040 bytes), keeping SDIO and radio queue bounds. All twelve hardware 256-KiB RAM RPC uploads passed, with a 0.965-second median versus 18.032 seconds in three earlier .157 samples. These small, non-interleaved samples do not establish causation for one parameter or acceptance of OTA, gallery transfers or sustained stability.
- Add a refined clock face and vertical time marker to quota bars for Codex and Claude, using each actual quota window. Five-hour limits support minute comparisons and weekly limits support hour comparisons, with second precision internally. No extra labels or external ruler; hide unknown or expired windows. A matching bridge build supplies the cycle metadata.
- Divide quota bars internally for all providers, including domestic Token Plans: five equal parts for standard five-hour limits and package percentages, seven for weekly limits. A known integral one-to-seven-hour short window uses one segment per hour and its actual label. Percentage divisions do not imply an unknown package duration; PAYG balances have no quota bar.
- Use the matching bridge's domestic cycle metadata for time markers, retaining actual short/week durations and package start/end bounds through cache conversion. Short markers support minute comparisons, package/week markers support hour comparisons. Unknown monthly periods remain hidden.

## 0.2.158-ui - 2026-10-10

Local UI candidate; not installed or publicly released. See the [UI consistency and adaptable overview audit](../TAB5-UI-AUDIT-2026-10-10.md).

- Support the eleven quota provider entries in the overview, automatically selecting available accounts or pinning up to three equal-width cards. Preserve legacy selections.
- Display each provider's current official logo in a uniform 36×36 pixel resource, with a distinct readable accent color.
- Add the selectable Ark provider with its official mark and an explicit pending-collection state; do not substitute cloud-wallet values.
- Show CPU/memory and a system shortcut without a Codex session; show an authorization prompt without quota data, preserving zero balances and cached results.
- Centralize workbench visual tokens and align primary market/system/pet spacing. Retain dedicated music, clock and art compositions.

## 0.2.157-ui - 2026-10-09

Installed locally on October 10; exact ELF fingerprint, VALID partition and continuous UI heartbeats verified. Not publicly released; transport speed and sustained stability remain incomplete. See the [transport validation report](../TAB5-TRANSPORT-157.md).

- Retry a gallery manifest/range at most twice after a missing valid response, retaining completed ranges on the same transport. Stop on explicit errors, foreground work, or a different fixed transport; verify the full SHA-256 before caching.
- Bound USB/BLE gallery reply waits to eight seconds and Wi-Fi I/O waits to eight seconds. Preserve explicit HTTP errors and remove the fixed ten-millisecond pause between completed gallery ranges.
- Rename the quota trend to “近7日 · 周额度使用（百分比）” and remove the partial-record annotation above the current day's bar, preserving values and incomplete-data semantics.

## 0.2.156-ui - 2026-10-09

Prepared release, installed locally with exact image identity, VALID partition state and advancing UI heartbeats. The user confirmed synchronized one-second network display on hardware. Not yet public; see the [deployment record](../LOCAL-063-156.md). This version includes .151–.155 changes since the last public .150 release.

- Narrow the category buttons from 250 to 220 pixels and enlarge their labels from 24 to 30 pixels, retaining icons, four-character names and touch height.
- Move and widen the right content frame while centering its existing control columns. Keep 30-pixel headings and 24-pixel body text, content and actions.
- Include .155 network number/curve alignment. Bridge 0.6.3 publishes complete snapshots once per second, refreshing numbers and curves together; CPU/memory sampling remains every two seconds.
- Show a large music cover with complete title, localized alias, artist and album hierarchy; position elapsed/total times at opposite progress-bar ends and retain unknown-clock fallback.
- Divide the Codex weekly budget into seven daily planned sections while retaining continuous actual usage. Initially select the most recently active task session, preserving manual choices and drafts.
- Add device information and consistent four-character settings categories; center Codex Direct titles with previous-page navigation, and distinguish installed firmware from the latest public release in empty upgrade states.
- Retain all three calendar faces with more readable calligraphy coverage, incorporating .151–.154 weather, music and settings layout changes.

## 0.2.155-ui - 2026-10-09

Local repair candidate; not installed on hardware or publicly released.

- Use the same trailing four measured 250 ms samples for upload/download numbers and the graph endpoint.
- Apply each received batch through its latest sample, removing the extra one-second playback reserve and future-point preview that made numbers advance ahead of the curve.
- Preserve measured traffic, missing-sample gaps, duplicate suppression, reconnect resets and slowly shrinking vertical scale. Transport fields and carousel settings remain unchanged.

## 0.2.154-ui - 2026-10-08

Installed locally, not publicly released. Exact device ELF identity, ota_1 / VALID state and three advancing UI heartbeats passed. The user confirmed normal four-character settings categories and calendar calligraphy; see the [local validation record](../LOCAL-063-154.md).

- Center the Codex Direct title separately from the taller left-side Back control. Previous-page navigation and busy-operation locks remain intact.
- When no upgrade is offered, center the status and next-step guidance, hide the empty notes card and upgrade control, and restore notes/actions when an offer returns.
- When the running image matches the computer's offer, state that the current firmware is installed rather than implying it is the latest online release.
- Use consistent four-character Chinese settings categories for network, screen/audio, content rotation, firmware upgrades and device information while retaining their functions.
- Enter the task page at the project and session with the latest activity timestamp; retain manual selection and session drafts throughout the visit.
- Retain all three calendar faces: keep WenKai and Ma Shan Zheng at 24 pixels and enlarge the harder-to-read Zhi Mang Xing to 28 pixels. Use 4-bit coverage and darker ink while preserving column spacing, baseline rhythm, poems, flowers and calendar layout. The user approved the comparison preview.

## 0.2.153-ui - 2026-10-08

Installed locally, not publicly released. Wi-Fi OTA, the exact running ELF fingerprint and ota_0 / VALID boot state passed; the user confirmed normal Device Information and Back control rendering.

- Move the fixed model label from the overview header to Settings → Device Information, with the model, running firmware, device ID, live data transport and scrollable official hardware specifications.
- Distinguish working, idle, waiting, offline and unknown pet states. The bridge's live session activity fixes incorrect idle indications.
- Use a top-left Back control in Codex Direct, increasing its height from 48 to 64 pixels and returning to the previous page. Recording/submission locks remain intact.

## 0.2.152-ui - 2026-10-08

Installed locally including the .151 UI changes; not publicly released. Wi-Fi upgrade and exact device fingerprint / ota_1 / VALID boot verification pass. Physical appearance remains subject to user observation; see the [local validation record](../LOCAL-063-152.md).

- Give music a large cover on the left and distinct title, localized alias, artist and album rows on the right. Place elapsed and total time at opposite ends of the progress bar without field prefixes; retain complete landscape artwork and unknown-clock fallback.
- Divide the Codex weekly-used bar into seven equal sections using six separators. Each section represents a daily planned budget (1/7 of the weekly allowance); continuous fill still represents actual usage.
- Retain the .151 Settings/Codex entry and weather-detail changes; preserve pairing, Wi-Fi, partitions and the small-screen protocol.

## 0.2.151-ui - 2026-10-08

Local UI candidate; not installed on a device or publicly released.

- Give Settings and Codex Direct equal 56×56 touch areas, right margins and corner radii; center each in its header/footer row while retaining independent background styles.
- Pre-render the smaller Codex brand asset to avoid runtime scaling differences between partial and full-frame rendering; retain all other brand assets unchanged.
- Put the weather location and source on one line, using larger pale text and smaller muted blue-gray text for hierarchy; retain cached-data indicators.
- Remove the local-time forecast caption and the duplicate source/current-weather caption at the temperature card's lower left.
- Actual LVGL previews and existing rendering, swipe, page-entry and voice regressions pass; device appearance and touch acceptance await installation.

## 0.2.150-ui - 2026-10-08

Wi-Fi transfer and exact image fingerprint / ota_0 / VALID boot verification passed. This release does not fix intermittent Bluetooth disconnections.

- Fix the camera entry covering the "Use TAB5" microphone switch during DJI recording on the Codex page. Give the switch its own space and match the microphone control height.
- Hide the camera entry during voice preparation, recording and recognition; restore it after completion or cancellation.
- LVGL previews and existing voice interaction regressions pass. The repaired firmware is running; the recording controls on the physical display still await user confirmation.

## 0.2.149-ui - 2026-10-07

[Published](https://github.com/yaoyouzhong/AI-bot/releases/tag/tab5-v0.2.149-ui). Locally installed; exact boot identity and two cold-image BLE transfers verified. Includes the .146–.148 fixes below.

- Repair wireless gallery RPC by explicitly using authenticated HTTP POST. Cache up to 2 MiB of JPEGs, prefetch the other layout and reclaim cache for camera, voice, attachments and OTA. Preserve the two decode buffers, image quality, pairing and partitions.

- Fix the portrait prefetch missing Wi-Fi isolation while the preceding landscape download is restoring the radio.
- Consecutive layouts reuse the paused radio across a 600 ms idle window, without extending the original 35-second deadline. Preserve the bounded preparation fallback and mode-change restoration.
- Regression tests exercise consecutive layouts, restoration contention, timeout and lease ownership; two uncached portrait fetches took 7,260 / 8,540 ms versus 9,484 ms in the .147 record (approximately 23% / 10% shorter, not a sustained guarantee). Wi-Fi restored successfully and no disconnect was observed during either transfer; recorded interruptions coincided with upgrade or deliberate restarts. The desktop continues at its existing fixed Release path.

## 0.2.148-ui - 2026-10-07

Locally installed and boot-verified, unpublished. Portrait prefetch missed isolation during restoration; the .149 candidate corrects this omission.

- In explicit BLE-only mode, first gallery downloads request a temporary Wi-Fi pause of at most 35 seconds. Restore after completion, failure or a mode change. Fall back to coexistence after a 500 ms preparation budget; cache hits do not pause Wi-Fi.
- Preserve automatic USB, Wi-Fi, BLE priority, credentials, pairing, partitions, image quality and window64/native7.
- Include existing seven-field Wi-Fi isolation diagnostics in USB health replies and display restoration state in the bridge. See [candidate evidence and acceptance boundaries](../TAB5-BLE-GALLERY-148.md).

## 0.2.147-ui - 2026-10-07

Locally installed with hardware cache and three-transport gallery evidence; first BLE downloads remain slow. Unpublished.

- Retain the .146 HTTP POST fix and daily USB, Wi-Fi, BLE priority.
- Prefetch the other layout after displaying a page and retain at most 2 MiB of JPEGs. Cache hits skip gallery RPC; preserve the two output buffers and original image quality.
- Camera, attachments, voice and OTA reclaim the cache first. Reboot, reclaim or eviction requires a new download. No SD card, pairing or partition change is required.
- Temporarily disable Wi-Fi power saving during downloads; pair with desktop image snapshot reuse and BLE throughput scheduling. Add fetch/decode timing; actual speed gains remain unmeasured. See [candidate evidence](../TAB5-GALLERY-CACHE-147.md).

## 0.2.146-ui - 2026-10-07

Locally functionally verified, unpublished; performance issues remain tracked.

- Use POST for authenticated HTTP RPC, fixing gallery clients that inherited GET and failed to synchronize wirelessly.
- Exact boot image and all three gallery transports passed display, orientation and pagination acceptance. Slow BLE cold downloads, the USB screen indicator taking over ten seconds and the historical intermittent Codex message remain separately tracked in the [record](../TAB5-GALLERY-TRANSPORTS-146.md).

## 0.2.145-ui - 2026-10-07

- Fix landscape fallback on later calligraphy pages in portrait: retain retired-frame and failed-job output buffers instead of allocating another 1.8 MB image.
- Transfer spare-buffer ownership atomically between the UI and decoder; never lend the currently displayed bitmap to a worker.
- Passed 24 page cycles, 96 orientation checks, both categories and failed-request retries with only two output allocations. Exact-image boot and user pagination acceptance passed; see [evidence](../TAB5-ACCEPTANCE-145.md).
- Append frame, reuse and allocation-attempt diagnostics. Preserve the display baseline, orientation mapping, fonts and driver.
- Supersede unpublished .143/.144; see [complete 0.6.0 notes](../RELEASE-0.6.0.md).

## 0.2.144-ui - 2026-10-07

- Retain .143 Codex Direct, eight screensavers, optional collection support, four-way art layouts, lossless fonts and three-transport OTA improvements.
- Continued .143 observation captured decode-output allocation failure. Reuse an unused decoder buffer instead of allocating a third full image alongside two cached frames; bound cached/worker output storage to two images and protect the actual displayed fallback.
- Retain 2–15 second retry backoff and lightweight receive stages, without restoring heap scans, driver or page-cache experiments.
- Later-page portrait hardware acceptance failed; superseded by .145. See [complete 0.6.0 notes](../RELEASE-0.6.0.md). The unpublished .143 is superseded.

## 0.2.143-ui - 2026-10-07

- Standard firmware retains all eight screensavers, including the floral monthly calendar, paintings and calligraphy. Import optional collections using Windows AI-bot 0.6.0; missing packs have explicit hints.
- Codex Direct provides a permanent entry, five recent sessions, voice drafts and explicit sending. Improve quota/countdown styling, connection status and compressed three-transport OTA.
- Art previews and automatic screensavers support four orientations; retain the accepted stable display path, JPEG cache synchronization and screensaver state fix. Lossless compressed fonts retain complete glyph coverage and caching.
- Completed image failures retry with 2–15 second backoff instead of retaining a 60-second reservation; preserve the last successful image and record lightweight receive/decode stages. Exact-image boot and user acceptance of both art categories, all orientations, automatic screensavers and normal typing pass without blue flashes or persistent waits.
- See [complete 0.6.0 notes and acceptance status](../RELEASE-0.6.0.md). The unpublished six-screensaver candidate is superseded.

## 0.2.133-ui - 2026-10-07

- Losslessly compress both full Chinese fonts, preserving all glyph coverage, metrics and pixels. Reuse decoded glyph caching to limit repeated decompression.
- Public firmware exposes six screensaver styles; daily painting and calligraphy and their downloader/orientation task are excluded. Local installations and artwork files remain intact.
- Keep the existing OTA partitions, NVS and asset layout. Local full-build experiment .132 is separate; public .133 hardware acceptance remains pending.

## 0.2.131-ui - Gallery decoder cache ownership

- Write back and invalidate the aligned gallery output buffer before JPEG DMA; reject failed synchronization without replacing the visible frame.
- Add dirty-tail fault modeling and repeated page/layout pixel checks, retaining .130 orientation. Installed with exact-image boot verification and user-confirmed corruption resolution; original automatic cycling restored. See [acceptance](../TAB5-ACCEPTANCE-131.md).

## 0.2.130-ui - Calibrated art orientation

- Use the fixed device measurement: positive X is normal landscape, negative X inverted landscape. Preserve the previously verified portrait mapping.
- Accept ordinary inclined stands using in-plane gravity while retaining near-flat, diagonal, movement and stale-data protections. Add sensor-to-layout calibration regression. See [acceptance](../TAB5-ACCEPTANCE-130.md).

## 0.2.129-ui - Inverted landscape art

- Rotate both the original landscape bitmap and date overlay when the device is turned upside down; reuse the same page cache without a new download.
- Complete four-direction detection while retaining portrait layouts, 350 ms debounce, prefetching and the two-frame cache limit. See [acceptance](../TAB5-ACCEPTANCE-129.md).

## 0.2.128-ui - Upright and faster portrait art

- Correct both portrait directions after .127 hardware feedback. Reduce the stable window to 350 ms and check the UI every 50 ms.
- Prefetch the other orientation of the current page within the two-frame cache limit; landscape images are unchanged. See [acceptance](../TAB5-ACCEPTANCE-128.md).

## 0.2.127-ui - Orientation sensor recovery

- Honor the minimum reset/configuration delay, initialize after display startup, retry startup communication failures and reinitialize after ten failed samples.
- Report chip identity, initialization stage and I2C error; retain the existing landscape and portrait layouts. Hardware status: [acceptance record](../TAB5-ACCEPTANCE-127.md).

## 0.2.126-ui - Automatic portrait art screensavers

- Daily painting and calligraphy follow device orientation in either portrait direction; existing landscape images and all other pages remain unchanged.
- Require one stable second, hold orientation while flat or moving, preserve artwork/frame identity and cached content on failure, and retain a two-frame decoded cache limit.
- Add 1902 portrait layouts for 813 works. Both-orientation gallery and native UI checks pass; see the [acceptance record](../TAB5-ACCEPTANCE-126.md) for hardware status.

## 0.2.125-ui - Seasonal flowers and daily art

- Include the latest floral calendar: 24 complete seasonal poems, three poem typefaces, adjusted flower placement/opacity, and calendar row spacing. Retain automatic annual day/night colors.
- Add daily painting and calligraphy screensavers with a matching PC gallery of 402 paintings and 411 calligraphy works / 1902 frames; order calligraphy by frame count.
- Render sourced Chinese notes in 200 painting frames (16 titles and 200 author names), retaining original captions and museum credits. Keep original names where no translation is verified.
- See the [acceptance record](../TAB5-ACCEPTANCE-125.md) for installation and device checks.

## 0.2.124-ui - Floral calendar and automatic annual colors

- Replace the calendar cover geometry with twelve traditional floral illustrations. Each month has a distinct phrase in place of the cover term pair; October retains the original phrase.
- Remove repeated holiday/date footer descriptions. Lunar notes, solar terms and rest/workday marks remain in the grid; a short pending notice appears only when official data is missing.
- Add “年度点阵 · 自动昼夜”: light colors from 07:00 to 19:00 device local time, night colors otherwise. Preview and normal idle display update in place without changing the stored selection, brightness or annual counts.
- See the [acceptance record](../TAB5-ACCEPTANCE-124.md) for installation and actual-device status.

## 0.2.123-ui - Monthly calendar and annual night screensavers

- Adds twelve seasonal monthly calendar pages with lunar dates, solar terms, official holidays and compensatory workdays. Missing official year data is explicitly marked as awaiting an update.
- The normal screensaver follows the current month and year without month buttons. Device preview offers all twelve months; changing months keeps it open, while touching elsewhere exits.
- Adds black-background night-blue and warm-gray annual styles while retaining the light style, fonts, hollow today marker and counting rules. Existing dimming, touch/PC wake and brightness restoration remain in effect.
- Applies the previously approved light countdown time text while retaining the muted prefix.
- Local validation and hardware installation status are recorded in the [acceptance record](../TAB5-ACCEPTANCE-123.md); no public release.

## 0.2.122-ui - countdown font weight

- Use medium-weight time digits and d/h/m units after the reset countdown label. Preserve regular Chinese text, muted color, size, character advances and position.
- Installed with successful exact-image USB boot verification. The user finds the countdown too dark; color advice and visual acceptance remain pending. Bridge binaries and everyday .121 voice acceptance are unchanged. See [installation evidence](../TAB5-ACCEPTANCE-122.md).

## 0.2.121-ui - voice stop feedback

- Authorized Wi-Fi installation and USB exact-image boot verification passed (ota_1 VALID, no upgrade error). The paired bridge is deployed and original automatic cycling restored; everyday voice behavior was accepted by the user; exceptional scenarios are not implied. See [installation evidence](../TAB5-ACCEPTANCE-121.md).

- Auto mode selects USB before Wi-Fi/BLE; fixed modes remain fixed. Wi-Fi/USB send existing 16 kHz IMA ADPCM blocks (1606 bytes per 200 ms instead of 6400 PCM bytes). Lossy encoding preserves sample count, order and tail delivery; the user reported normal everyday behavior; individual transports and exceptional scenarios remain separate.

- Replace the stop icon immediately with an animated progress ring after the stop tap, keeping it through recognition and desktop draft staging. Restore the send arrow only after staging succeeds. A temporarily unready recording response no longer drops a stop tap; repeated taps do not resend stop.
- Stop progress animations when hidden so unrelated page caching remains idle. Preserve cancellation, tail audio and final text checks. Installed and accepted for everyday voice use; no precise latency reduction is claimed without a correlated timing trace.

## 0.2.120-ui - continued dictation hints

- Authorized Wi-Fi installation and USB exact-image boot verification passed on 2026-10-05 (ota_0 VALID, no upgrade error). Paired Windows bridge deployed; original auto cycle restored. New interaction acceptance remains pending; see [installation evidence](../TAB5-ACCEPTANCE-120.md).

- With the paired Windows bridge, reopening Codex checks the target composer. Confirmed empty restores the voice key; nonempty, unavailable or older replies preserve existing state. No draft mutation or automatic send.

- Keep review and append hints on one line and explain that more speech can supplement the draft. Replace ambiguous re-record wording with “继续语音” after confirmed staging and “重试录音” when no take has been confirmed.
- Retain explicit recording, cancellation, clear and send behavior. Local candidate; device verification pending.

## 0.2.119-ui - annual percentage weight

- Increase only the annual screensaver percent sign from Nunito 400 to 500, between the installed thin form and the earlier 600 preview. Retain the 900-weight digits, rounded labels, hollow today marker and layout. User-authorized USB installation and exact-image ota_1 VALID verification passed; installation through verification took 19.207 seconds. The user confirmed typography and preview/touch exit; the user subsequently confirmed automatic entry and reboot persistence, completing annual screensaver acceptance. No public release.

## 0.2.118-ui - annual-dot screensaver

- Add a device-only annual-dot screensaver choice and full-screen preview under Screen and sound. Preserve the classic saver, timeout, brightness restoration and normal touch/PC wake behavior.
- Draw one dot per calendar day with distinct elapsed/today/future states; support leap years, century rules and rollover. Save the style separately without changing the existing preference blob or bridge settings protocol.
- Keep the quota countdown at its original location; soften its blue-gray color and separate day/hour units. Internal tests pass; hardware installation and acceptance remain pending.

## 0.2.117-ui - compression on every OTA transport and BLE upgrade entry

- Extend negotiated OTA compression to Wi-Fi and USB, preserving legacy raw transfers and full-image integrity/boot checks. Show decoded image progress separately from transferred data.
- Expose the BLE upgrade button and remove obsolete USB/Wi-Fi-only hints. Automatic OTA prefers Wi-Fi, USB, then BLE; fixed modes remain pinned.
- Remove the redundant scrolling hint and storage benchmark entry from the upgrade page; notes remain scrollable and internal diagnostics remain available.
- Rename the Codex weekly quota label to “本周已使用” and add one “距重置” countdown using the provider reset instant.
- BLE hardware installation and exact ELF/ota_1 VALID boot verification pass. Retained installation-through-verification time is 114.185 seconds, excluding completion delay and reboot. Wi-Fi/USB compressed OTA and voice regression remain pending on hardware; this is a local installation, not a public release. See [acceptance evidence](../TAB5-OTA-COMPRESSION-117.md).

## 0.2.116-ui - compressed BLE firmware and full-image preflight

- Independently compress existing 48 KiB BLE firmware ranges before authenticated encryption and decode within bounds; retain raw fallback and legacy, USB and Wi-Fi compatibility.
- Add a full-image RAM preflight using the real range reader/decoder and streaming SHA-256 without Flash writes. Pin BLE without transport fallback; reject cancellation, session changes, hash failure and timeouts.
- Optional Wi-Fi isolation uses a 125-second restoration lease and 110-second transfer budget. Completion/failure restores Wi-Fi and unconfirmed restoration cannot pass. The paired bridge checks the selected image hash and retains the 120-second full-upgrade target.
- Local candidate only. BLE throughput, device ROM decoding, voice regression and complete OTA still require hardware acceptance.

## 0.2.115-ui - recording stop status correction

- Preserve local stop/cancel intent against delayed recording replies; do not restore the ready-to-speak hint or recording icon.
- Match the local draining phase with a stopping message while remaining audio is sent; recognition, draft staging and explicit submission remain unchanged.
- UI regression covers delayed recording replies, stop, recognition and cancel. Firmware builds pass; device acceptance is pending. The intermittent inability to stop later recovered, with its cause still unconfirmed.

## 0.2.114-ui - optional larger BLE response window

- Hardware follow-up verifies exact .114 boot after Wi-Fi installation and two complete32/64/32 RAM comparisons. First totals14.852/12.663/13.106s; repeat15.979/12.982/14.865s. Window64 wins both controls in both runs; six64 rounds pass, with a12.7% shorter repeat than final32. Adopt64 as the bridge default, still native7. Conservative64 estimates165/172s exceed120s; sustained performance and full BLE OTA remain unaccepted.
- Advertise `rpcWriteWindow=64` within the unchanged 512-byte identity. Receive buffer sizes, packet validation, notification grants, radio settings and Flash paths remain unchanged.
- The paired bridge now defaults to min(peerOffer,64), preserving older32/8 peers and voice/small replies. Explicit32/64/32 comparison uses at most seven native writes; every exit restores production64. It requires USB and isolated BLE RAM preflight, records actual per-response windows, and never automatically selects a setting or starts OTA. Bridge-only deployment is verified through six matching files, the listening process, acknowledged BLE data and restored display policy; no new firmware was needed.
- Queue7/31/7 hardware comparison passed all nine rounds in12.658/14.547/13.459s with no gain from larger native concurrency. This diagnostic continues to fix the wire window at32 and restores production64/native7 afterwards. The120s full-OTA gate remains unchanged.

## 0.2.113-ui - isolated BLE preflight and receive diagnostics

- Later bridge-only cohort recheck passes in 17.555s (estimate 224s), slower than the same sender's first 13.997s. Add an explicit desktop 7/31/7 queue-capacity comparison to assess variability and temporary larger-queue benefit; it restores seven and retains separate results on exit. Local checks pass; hardware comparison pending. The .113 image remains unchanged and full BLE OTA remains unaccepted.
- Add an optional RAM-only BLE preflight with Wi-Fi temporarily stopped by its existing worker. Preserve network settings and restore on completion, cancellation, USB/mode loss or a 35-second lease expiry; retry failed restarts and never report preflight success while restoration remains pending.
- Keep the 32-fragment write window, seven pending native writes and three 256 KiB rounds unchanged. The desktop checkbox enables a same-firmware coexistence comparison; normal mode retains existing behavior. Neither mode starts OTA.
- Export bounded numeric radio/receive and Wi-Fi-stop diagnostics over USB without expanding the 512-byte BLE identity. Local fault/build checks pass; actual coexistence benefit, restored Wi-Fi association and complete BLE OTA remain pending.
- Subsequent hardware readback verifies exact .113 healthy boot and recovered Wi-Fi association. Three-round RAM tests pass in both modes:13.997s isolated versus18.953s with Wi-Fi retained (35.4% higher sample throughput); estimates202s/280s still exceed120s. A later bridge-only rolling sender passed integrity but took19.213s isolated (estimate236s), so restore the cohort sender for a same-condition recheck. Firmware remains unchanged; full BLE OTA is unaccepted.

## 0.2.112-ui - separately negotiated BLE response write window

- Advertise optional `rpcWriteWindow=32` for large computer-to-device RPC replies; the bridge drains cohorts of at most seven native write commands and retains the final ATT barrier. Old bridges/firmware, small replies and voice keep previous limits; outgoing notification pacing is unchanged.
- Preserve the existing bounded receive buffer, cursor/ID/length checks, authenticated payloads and the 512-byte identity limit. No Flash, partition or memory-pool parameter change.
- .111 queued eight-packet preflight completed two rounds in 8.726/7.025 seconds and hit the aggregate budget. .112 exact healthy boot is now verified after Wi-Fi installation; all three BLE RAM download rounds completed in 6.260/5.100/5.664 seconds, with error zero. The conservative whole-image estimate is 222 seconds, exceeding the 120-second goal; full BLE OTA remains unaccepted.

## 0.2.111-ui - BLE upgrade optimization and bounded preflight candidate

- Add three BLE-only 256 KiB downloads without Flash writes, with a 20-second transfer budget plus at most five seconds for an active request; preserve the existing benchmark.
- The paired bridge temporarily requests Windows 11 throughput preference during actual BLE bulk traffic, restores Balanced afterwards and prioritizes the first large response. Windows 10 keeps its existing behavior.
- Screen the selected image against the user's 120-second target using the slowest round, 20% transfer margin and a 25-second Flash/verify/reboot reserve. Missing rounds, timeout or an excessive estimate do not qualify for full-image testing; never start OTA automatically. Hardware throughput remains pending; see [method](../TAB5-BLE-OTA-PREFLIGHT.md).

## 0.2.110-ui - blank-range search and explicit storage-test failures

- Search 64 KiB-aligned windows beyond the verified spare image plus a 4 KiB guard for an entirely blank 128 KiB sample. Read through an internal buffer; preserve original read errors and never erase existing bytes to make room.
- Show specific failure reasons and export optional numeric phase/scanned diagnostics. Native checks cover dirty tails, earlier blank windows, read failures and restoration; 2026-10-05 hardware readback confirms completion and restoration with no error: 8/16 KiB medians 737.72/735.23 KiB/s; 48/64 KiB skipped by memory availability checks. No absolute hardware limit is claimed.
- .109 installed and passed startup verification, but its test stopped before erase/write with error 259 and zero completed rounds. Actual JEDEC ID is 0x464018, page size 256 bytes. This is not a completed microbenchmark or a hardware-limit result.

## 0.2.109-ui - storage-test task stack repair candidate

- Use an internal 12 KiB task stack, matching the existing OTA writer's memory domain; reject a non-DRAM stack before image verification or any Flash access. Keep the sample boundary and restore checks.
- .108 installed and passed exact-image startup verification, with 7.900s preparation, 9.984s pure writing, 0.904s verification and 19.409s installation. Its manual storage test then panicked (`tab5_flash_benc`, external stack, `panic_abort`); the exact assertion was not retained. Hardware repair acceptance is pending, and the microbenchmark has not produced valid hardware results.

## 0.2.108-ui - explicit storage block-size measurement

- Add a manual storage test on the firmware page; verify a spare image and its all-FF unused 128 KiB tail before modification. Compare three rounds of internal 8/16/48/64 KiB buffers with separate erase and write API timing; skip unavailable sizes with a 96 KiB reserve.
- Restore the tail and reverify the spare image and unchanged boot selection after completion, cancellation or partial failure. Export numeric current-boot results and actual JEDEC ID through the existing USB startup verification. Never select a new boot image or modify configuration.
- Pair the microbenchmark with the third comparable full-image OTA measurement. A 128 KiB result alone cannot establish sustained hardware limits. Firmware, fault and UI checks remain distinct from pending hardware results; see [method](../TAB5-FLASH-BENCHMARK.md).

## 0.2.107-ui - broom icon and truthful upgrade progress

- Replace the clear control with an original wooden-handle, golden-bristle broom; retain its 184×48 width, caption, initial hiding and long-press trigger.
- Advance upgrade progress only after successful flash writes. Distinguish pending writes after completed reception from final verification, which displays its stage and elapsed time rather than a stuck 99% label. Preserve SHA, image, identity and boot-partition checks; no installation speedup is claimed.
- The .106 Wi-Fi upgrade passed exact startup verification: 7.905s preparation, 9.621s pure writing, 0.879s final verification and 18.993s installation. Reception and writing overlap and must not be summed. The user subsequently confirmed the broom and this upgrade's feedback. Installed .107 passed exact startup verification (ota_1 VALID, error 0) with the original automatic cycle preserved: 7.802s preparation, 9.900s pure writing, 0.891s verification and 19.167s installation. Installing .107 used the previous receiver; the new progress path applies on the next upgrade and has not yet been independently observed on hardware.

## 0.2.106-ui - desktop conversation selection follows the panel

- Previous/Next immediately requests the corresponding desktop Codex conversation and updates panel selection after matching confirmation. Keep the five-entry order stable until reopening; entering the panel still defaults to the latest entry.
- The Codex key opens or raises the current selection rather than resolving latest again. Same-target reopening retains unsubmitted stage/send/clear controls; switching targets resets the old send entry without deleting drafts.
- Failed or wrong-target replies retain the previous selection and block dictation until navigation is confirmed. Reuse the existing bridge explicit-target operation without a wire-format change. Native event and existing bridge selection tests pass; firmware installation and physical acceptance remain pending.

## 0.2.105-ui - conditional, aligned long-press clear control

- Restore the explicit 长按清空 caption and match Re-record at 184×48. Align icon/text offsets and use a 24px gap between the controls.
- Initially hide clear until a voice draft is confirmed in this flow. Hide during recording and after confirmed submission or clearing; Re-record preserves the existing desktop draft and its clear entry. Visibility does not poll all manual desktop edits.
- Preserve long-press-only clearing, plain-text append without submission, existing transport parameters and the PSRAM executable-address repair. The independent desktop bridge write-exception reconciliation has passed one real first-take/re-record/append acceptance; .105 layout still awaits installation and hardware acceptance.

## 0.2.104-ui - Wi-Fi upgrade acceptance

- Keep .103 behavior and network parameters unchanged. Use a new image identity to exercise the installed fixed receiver and its retained phase timing during a complete Wi-Fi upgrade; preparing the candidate does not install it.
- .103 USB installation passed exact ELF/partition/VALID verification with upgrade error 0 and original automatic cycling restored. The user then confirmed Wi-Fi data and page switching without crashes over approximately two minutes.
- 2026-10-04 complete Wi-Fi upgrade passed: .104 exact ELF `473a39419662c243c21b4abec21c4c3bd360ee33f05601b18f7ef39fea421df3`, ota_0 VALID, all 6,852,304 bytes written, previousStage=9 and previousError=0. The user reported no blue screen, a brief final verification pause and faster perceived operation than USB. Sender=12.171s; receiver preparation/receive/write=17.882s; installation=18.845s excluding completion dwell/reboot. New phase timing: prepare=7.892s, pure write=9.502s, free-buffer wait=0, ready-data wait=0.105s, verify/boot selection=0.881s. Receive and write overlap; these times must not be summed. Original auto/15-second/six-page cycle is verified. This accepts one complete Wi-Fi upgrade, not extended stability or a tuned network configuration.

## 0.2.103-ui - PSRAM executable-address repair and OTA phase-timing candidate

- Wrap the pinned SDK executable-address check to recognize only the actual linked P4 PSRAM instruction interval, retaining SDK fallback and the FreeRTOS TLS corruption guard. This addresses the HTTP/pthread task-deletion path described in [Espressif issue 15997](https://github.com/espressif/esp-idf/issues/15997). Firmware .101's recorded abort function and classifier match the pre-fix ELF byte-for-byte.

- Record backup preparation, pure flash writing, receiver free-buffer wait, writer ready-buffer wait and final verification separately. Retain these diagnostics after reboot; installation time excludes completion dwell, reboot and healthy boot.
- Keep existing three-part timing, authentication, integrity and rollback semantics. The first installation uses the old receiver; a subsequent upgrade is needed to exercise new timing. Concurrent receive/write phases must not be summed.
- The companion bridge formats only allowlisted numeric fields and remains compatible with old device diagnostics. Retain .102 connection alignment.
- The .101 Wi-Fi attempt sent the .102 image in about 15 seconds, but the device aborted at write offset 4,866,048 of 6,851,120 bytes and repeatedly aborted on task deletion after reboot. The device retains .101 on ota_0 VALID; .102 was not installed. The user stopped the loop with Only USB, and the bridge was normally exited. Install the repair through USB first; Wi-Fi recovery and full upgrade timing remain unverified. Receive parameters have not changed and no speed gain is claimed.
- 2026-10-04 recovery acceptance: .103 installed through USB, ELF `7e0a7266a75d6162f94356288a2f4e69de15582eb7df7e0fca70d3420a97f7c6`, ota_1 VALID, previousStage=9, previousError=0. The old receiver recorded 19.589 seconds for preparation/receive/write. This is a USB sample, not a Wi-Fi speed result. Genuine bridge file hashes/pairing and original auto/15-second/six-page cycle match. User confirmed ordinary Wi-Fi display and page switching without blue screen or reboot; full Wi-Fi upgrade remains pending.

## 0.2.102-ui - aligned connection-status layout candidate

- Use four left-aligned columns for pairing, USB, Wi-Fi and Bluetooth. Headings and state text share a left edge; move checks after the text and center them through a row layout, with subtle separators.
- Retain green, amber and gray state semantics; hiding checks never shifts state text.
- Keep .101 dictation hints, explicit submission and long-press clearing. This candidate does not change Wi-Fi OTA transport. Native LVGL previews passed; physical layout acceptance is pending.

## 0.2.101-ui - installed; connection status and quick dictation candidate

- Add USB to Connection & Network alongside pairing, Wi-Fi and Bluetooth; positive states use green text and checks.
- Distinguish USB readiness, waiting for desktop data and disconnection using physical connection and recent authenticated data. Waiting is amber.
- Lock Close and conversation navigation during main-key operations without changing their appearance. Preserve normal boundary availability styling.
- Shorten the successful-send message to “已成功发送Codex执行。”.
- First staging shows “检查并修改完善后可直接点击蓝色箭头发送。”; show the append hint only for subsequent confirmed takes appended to an existing desktop draft. Cancellation, failure and an empty composer do not falsely report an append.
- Add a compact outlined 清空 control with an original two-tone solid eraser beside Re-record, activated by long press. Clear only the current desktop target composer, restore Doubao after confirmation and retain the TAB5 draft. The paired bridge guards target identity and request replay without navigation or submission.
- Native previews check connection-state transitions and pixel equality for Close/Next before and during locking. Physical acceptance remains pending.
- On October 4, 2026, the bridge observed .101 and the user supplied a connection-page photo with an alignment complaint. The original six-page automatic cycle and 15-second interval remain. Subsequent live diagnostics confirmed exact ELF aa97bbaff21bcd4e94d71af9d51500e2d92aba522c14be32902545d0632ed2dc, ota_0 VALID and zero prior upgrade errors. Preparation/receive/write took 19.744 seconds in this USB sample, not a Wi-Fi baseline. Clearing and navigation appearance still await acceptance.

## 0.2.100-ui - desktop dictation append and lower-panel layout

- Keep key backgrounds transparent while busy; preserve physical press motion. Center the two keys symmetrically, remove repeated captions and the shared deck, and use a compact 172px lower panel with wrapping and scrolling.
- Use the original avatar with voice bubble from the official Doubao IME site. Align auxiliary icons and text consistently; use a circled cross for cancellation. Display the actual full microphone endpoint name, updating to the TAB5 built-in microphone on fallback.
- Show green recording-ready text and an active microphone indicator; Re-record has a green microphone. Confirmed staging uses reference-blue #3A83F7 and a centered thick white send arrow.
- Separate desktop dictation from the TAB5 composer, including partial results, failures and cancellation. The paired Windows bridge appends subsequent takes to the current desktop draft, excludes question-card editors and confirms readback without sending.
- Simulated faults and live two-take desktop insertion were checked. On October 4, 2026, physical installation and exact-image boot verification passed; the user accepted button motion, the full microphone name and consecutive dictation appending to the desktop draft without submitting.

## 0.2.99-ui - Codex physical keycap styling and explicit submission candidate

- Group both controls on a mechanical deck with brushed-metal collars, graphite/red lacquer faces, side walls and shadows. Compact the conversation area and leave at least 20 pixels between collars and short Codex/豆包 captions. Press depresses/shrinks the face; release or lost touch rebounds. Larger centered Codex and original frameless official Doubao site icons replace the microphone; stop/send icons indicate action states.
- Reuse the voice key for start, explicit stop/staging, and a separate third-click submission. Recognition remains an unsubmitted draft until the user chooses to press Enter in the original conversation's current desktop composer.
- Provide Cancel/Re-record recovery while retaining desktop drafts and user edits. A successful staging receipt binds the target and is consumed once; uncertain replies and connection failures never automatically repeat submission.
- Local builds, actual LVGL previews and fault-state tests validate the candidate. New submission/re-record controls still require hardware acceptance.

## 0.2.98-ui - installed; this run passed visual receiver acceptance

- Functional code is unchanged from 0.2.97-ui. A distinct version enables hardware measurement of initial progress, blue flashes and complete USB upgrade time with the new receiver.
- On October 4, 2026 the user confirmed successful USB installation, smooth initial progress and no blue flash throughout. Pre-reboot telemetry recorded 7.601-second preparation, 136 buffer slots and zero LCD underrun delta, with USB reception advancing during preparation. This single run does not establish long-term stability; exact-image boot verification and complete upgrade timing remain unfinished.

## 0.2.97-ui - installed; new-receiver initial-pause/blue-flash validation pending

- Authenticate and receive the first block, then wait for the preparation overlay to render before the SDK prepares the backup partition. The bridge can pause high-rate sampling before erasure, while reception continues during preparation.
- Enable the ESP32-P4 SDK PSRAM instruction/rodata execution path to reduce flash-related display/USB blocking. Expand a bounded receive pool with a 4 MiB reserve; retain the two-block fallback under memory pressure.
- Display preparation seconds and received bytes; add per-attempt preparation time, buffer count and LCD underrun delta diagnostics. Initial authentication/read, UI preparation or receiver-start failure causes no partition erase/write.
- Compilation and fault simulations do not establish hardware acceptance. Installation still uses the 0.2.96 receiver; the following distinct-image upgrade must validate the new initial-pause/blue-flash behavior.

- On October 4, 2026 the user confirmed USB installation. Production verification confirmed exact ELF 3ab2b02d694751e8eccd2344b80752223c7705e103cc44cbdbc1cbb89ee3efef, increasing uptime and ota_0 VALID. The older receiver took 40.745 seconds for preparation/receiving/writing, with cumulative receiving 32.289 and erasing/writing 17.064 seconds; all 6,667,168 bytes completed with zero error. Pairing, brightness 75 and the original six-page/15-second automatic cycle remain. This bootstrap does not prove elimination of pauses or blue flashes.

## 0.2.96-ui - installed Codex icon entry and USB background-transfer changes

- Replace the permanent entry text with the Codex icon, retaining a 60×60 bottom-right touch target and widening the four navigation buttons. Record the icon source with the firmware.
- The matching bridge pauses high-rate USB metrics and artwork during upgrades, retaining status heartbeats, session freshness, authentication and boot checks. Deferred resources resume afterward. Add cumulative and peak USB gate-wait diagnostics.
- On October 4, 2026 the user confirmed USB installation; exact-image and ota_1 VALID boot verification passed. Preparation, receiving and writing took 38.526 seconds, with cumulative receiving 30.125 seconds and erasing/writing 16.839 seconds. The same phase was about 35% shorter than the preceding 59.570-second run. Final verification, completion display and reboot are excluded. The user still observed an initial pause and blue flash; full speed/stability acceptance remains incomplete.
- The user then reported that Codex Direct failed to bring Codex forward. The desktop bridge adds one scoped foreground input-queue activation fallback and content-free diagnostics, retaining actual foreground/target verification. Hardware confirmation is pending; TAB5 does not require reflashing.

## 0.2.95-ui - installed USB complete-image validation with the new receiver

- Functional code is unchanged from 0.2.94-ui. A distinct image version allows measurement of complete USB upgrade time with the new receiver.
- On October 4, 2026 complete USB installation and exact-image ota_0 VALID verification passed: 59.570 seconds for preparation, receiving and writing, 51.627 seconds cumulative receiving and 16.922 seconds cumulative erasing/writing. Writing improved, but overall speed missed the target and the user still observed intermittent stalls.

## 0.2.94-ui - installed local upgrade performance candidate

- Pass the validated image size to the SDK and prepare the backup partition before receiving data, decoupling erase block size from 16/8 KiB staging. Running-partition, rollback, SHA, image and boot checks remain active.
- Match owned receive buffers to one authenticated 48 KiB range, avoiding the extra short request from each 64 KiB block. Prefer 48 KiB internal staging, retaining 16/8 KiB fallback and 96 KiB free memory.
- Preparation/receive/write phase timing and cumulative flash time include preparation erasure, so pre-download waiting is not hidden. Full-image and failure simulations passed; throughput changes require hardware acceptance.
- On October 4, 2026 the user confirmed USB installation. Production verification confirmed exact image identity, increasing uptime and ota_1 VALID. The older receiver completed preparation, receiving and writing in 61.668 seconds, with cumulative receiving 60.774 seconds and writing 33.740 seconds; this does not measure the new receiver.

## 0.2.93-ui - installed complete USB upgrade validation candidate

- Functional code is unchanged from 0.2.92-ui. A distinct image version allows a complete USB upgrade test using the newly installed receiver.
- On October 4, 2026 a complete USB installation and exact-image ota_0 VALID boot verification passed. Device timing was 58.328 seconds for preparation, receiving and writing, 57.401 seconds cumulative receiving and 34.371 seconds cumulative writing; the speed target was not reached.
- Three USB RAM runs in each direction succeeded: upload 561–579 KiB/s, download 262–336 KiB/s. These use 256 KiB runs and do not replace complete-image or long-term stability acceptance. No public release.

## 0.2.92-ui - installed local candidate

- On October 4, 2026 the user confirmed Wi-Fi installation. Production USB verification confirmed matching image identity, increasing uptime and ota_1 VALID.
- The older receiver performed preparation, receiving and writing in 37.002 seconds, with cumulative receive time 26.206 seconds and write time 35.008 seconds. They overlap and must not be added. This does not measure the new USB receiver.

### Upgrade transfer
- Negotiate larger USB binary fragments so a 48 KiB firmware reply fits one transaction; retain 16 KiB compatibility with older firmware and bridges.
- Retry only the same immutable firmware range, at most twice after a missing response, with a ten-second request bound. Reject permanent errors and malformed offsets without retrying; retain the old boot partition on failure.
- Finish USB binary frames within a bounded wait; require reconnect after incomplete sends.

### Flash writing
- Fall back to 16/8 KiB internal staging when a fragmented heap cannot allocate 64 KiB, retaining at least 96 KiB free. SHA, image identity and boot checks remain mandatory. Real transfer/write timing is pending.
- Include the 0.2.91-ui Codex entry and voice draft changes below.

## 0.2.91-ui - candidate, USB installation interrupted

### Entry and navigation
- Rename the entry to “Codex 直达” and place it in a permanent bottom-right footer slot without covering page content or navigation.
- Resolve the latest desktop conversation on every return, including one updated after the panel opened; update the panel and bind dictation to the actual target.

### Voice drafts
- Keep recognition preview at the panel bottom, clear it only after successful desktop staging, and retain manual submission. Hardware acceptance is pending.

## 0.2.90-ui - installed local candidate

- Add the header quick-control entry, five recent Codex conversations, desktop navigation and Doubao voice start/stop.
- Stage completed speech in the selected desktop composer for manual submission; retain uncertain recovery drafts without automatic insertion or sending.
- Improve the conversation card hierarchy and remove the static instruction beneath the buttons. Requires the matching Windows development bridge; physical-device acceptance is pending.

## 0.2.89-ui - 2026-10-03

- Independently versioned TAB5 baseline distributed with AI-bot v0.5.0.
- Separate full factory-install and application-upgrade packages; own code is MIT licensed.
- See [source/build snapshot](TAB5-SOURCE.md) and [release validation](../RELEASE-0.5.0.md).

Future releases use `tab5-vX.Y.Z-ui` tags and preserve their own image/source identity. Bridge releases do not renumber TAB5 images.
