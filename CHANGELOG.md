# Changelog

All notable changes to this project will be documented in this file.

## Unreleased

## 0.6.0 - 2026-10-07

- Add unified Windows software/firmware updates: independent component discovery, notes, reminders, matching downloads, hashes, embedded identity and compatibility checks, and explicit installation handoff.
- Improve TAB5 Codex Direct session selection/activation, draft target validation, voice focus recovery, text append and explicit sending without duplicate insertion or sends.
- Coordinate compressed Wi-Fi/USB/BLE OTA and transfers, retain legacy compatibility and verify the running image after installation.
- Public builds retain daily paintings and calligraphy. Optional collection ZIPs support validated import, hot reload and preservation on failure; collections live outside the app directory, with legacy resource support.
- Companion TAB5 0.2.145-ui adds Codex Direct, eight screensavers, a floral calendar, four-way artwork layouts and lossless font capacity optimization. ESP8266 remains 0.5.0 and need not be reflashed.
- macOS keeps existing platform functionality with a synchronized version; the new update UI, TAB5 Codex and gallery services require Windows.
- See [complete three-component notes and acceptance status](docs/RELEASE-0.6.0.md). This supersedes the unpublished 0.5.1 draft.

## 0.5.1 - 2026-10-07

- Complete Windows software and firmware updates: discover stable bridge, ESP8266 and TAB5 versions independently, notify, download and validate the matching package, then hand it to the installer or firmware tool. Local art editions are protected from replacement by public updates.
- Improve TAB5 Codex Direct draft targeting, voice recovery and explicit send behavior.
- Add compressed OTA transfer handling, exact-image boot verification and transport diagnostics.
- Keep daily painting/calligraphy local-only: public Windows builds exclude gallery classes, routes, catalogs and images. Existing local installations are not replaced by candidate preparation.
- Keep macOS application behavior unchanged while synchronizing the bridge version. The new unified update entry is Windows-only; device firmware versions remain independent.

## Local development history (not public release notes)

- TAB5 .131 synchronizes gallery output caches before JPEG DMA, preventing dirty cache writeback from overwriting decoded pixels. Add dirty-tail fault modeling and pixel-level page-replacement checks. Installed with exact-image boot verification; the user confirms calligraphy corruption is resolved and original automatic cycling is restored. See [acceptance](docs/TAB5-ACCEPTANCE-131.md).

- TAB5 .130 calibrates landscape direction from a fixed real-device pose and accepts inclined stands without the former Z-axis restriction. Adds a regression from the captured acceleration vector through to the displayed layout. Installed with exact-image boot verification; the user confirms all four directions and stable switching.

- TAB5 .129 adds inverted landscape to daily art, rotating the cached original frame and date together without another download. Existing portrait behavior and normal landscape layout are retained.

- TAB5 .128 corrects the hardware-observed portrait inversion, checks orientation without the whole-second UI delay, and prefetches the matching alternate layout; portrait direction and speed confirmed by the user.

- TAB5 .127 corrects minimum BMI270 reset delays, retries sensor initialization and recovers interrupted readings. Sensor communication and exact-image boot checks pass; subsequent portrait direction feedback is addressed in .128.

- TAB5 .126 adds automatic portrait layouts to daily art while preserving landscape views. Validate orientation stability, same-work/page switching, offline retention and a two-frame cache; see the [acceptance record](docs/TAB5-ACCEPTANCE-126.md).

- Add 36 Chinese paintings to the TAB5 daily gallery (402 paintings total) and curate 24 separate reserves. Complete the pending 63-work calligraphy batch, then add 45 complete works with at most three frames each, reaching 411 calligraphy works. Order daily calligraphy by ascending frame count and record the holding museum alongside each verified title and author attribution, preserving anonymous or museum-romanized attributions. Add Chinese holding-museum metadata to all paintings and document that 48 have Chinese titles/authors while 354 retain museum English. Add sourced Chinese reference notes for 16 titles and the authors of 200 English-label works; retain all originals and omit unverified translations. Render verified notes below the original captions in 200 frames. Sources, licenses, original hashes and crops are recorded; paired layouts preserve complete couplets and panel sets. Eight further calligraphy reserves and downloaded scroll sources remain local. Annual quantity validation and all-frame gallery self-tests pass; reserves and details do not inflate work counts. Redistribute monthly calendar rows into the former footer space, reserving month controls only in preview. Deployed and flashed with .125; exact-image boot verification passes and the user confirms all three screensavers display normally. See [daily art notes](docs/TAB5-DAILY-ART.md).

- Local TAB5 .124 removes repeated calendar-footer descriptions, replaces the cover term pair with a different monthly phrase, and uses twelve traditional floral illustrations. Lunar notes, terms and rest/workday marks remain in the grid. A new automatic annual style uses light colors from 07:00 to 19:00 local time and night colors otherwise. See the [acceptance record](docs/TAB5-ACCEPTANCE-124.md).

- Local TAB5 .123 adds monthly-calendar and night-blue/warm-gray annual screensavers. Normal calendar mode hides month controls and follows the current month; preview supports month selection. Includes the approved brighter countdown. See the [acceptance record](docs/TAB5-ACCEPTANCE-123.md) for installation and hardware status.

- Prepare the approved countdown color refinement: use the reset-date light text color for time digits and d/h/m units, retaining the muted prefix, weight and original position. Installed with .123; final visual acceptance is tracked separately.

- .122 Wi-Fi installation and exact-image USB boot verification passed; original automatic cycling restored. Countdown color advice is pending after the user reported it too dark; no color change or final visual acceptance yet.

- Record user acceptance of everyday .121 voice behavior and prepare the .122 local countdown-weight candidate. Only time digits/units become heavier; color/layout are unchanged. Not installed.

- Prepare a typography-only quota countdown adjustment: medium-weight time digits and d/h/m units, preserving regular Chinese text, color, size, advances and position. Source/preview change; not installed.

- TAB5 .121 completed authorized compressed Wi-Fi installation and exact-image USB boot verification. The paired bridge is deployed and original automatic cycling restored; everyday voice behavior is accepted by the user; deliberate focus loss and link faults remain separate. See [installation evidence](docs/TAB5-ACCEPTANCE-121.md).

- Prepare same-take focus recovery only while the owned Doubao recorder remains active; preserve audio/text and never restart recognition automatically. TAB5 .121 auto mode prefers USB, and Wi-Fi/USB use existing 16 kHz IMA ADPCM to reduce audio payload by about 75%. Deployed and accepted for everyday voice use; seamless recovery after deliberate focus loss is not implied.

- Pace TAB5 audio reception against Windows playback capacity instead of writing network bursts directly into the two-second buffer. Keep audio intact, bound the playback queue to 400 ms for 200 ms packets, yield while waiting and fail visibly on a stalled output. A real NAudio buffer test reproduces the former overflow and verifies an exact six-second burst; the user reports normal everyday behavior; precise latency gains lack a correlated timing trace.

- Prepare TAB5 .121 with immediate stop acknowledgement and an animated progress ring throughout recognition and desktop staging. Ignore duplicate stop taps, stop hidden animations and retain audio-tail delivery and final-text checks. Installed and accepted for everyday voice use; no precise latency reduction claimed.

- After verified TAB5 dictation staging, place the Codex insertion caret at the complete draft end using an empty accessibility text selection. Recheck the target and text, verify the resulting selection, and never repeat an already successful append if caret positioning is unavailable. Bridge-only fix; TAB5 .120 remains unchanged.

- TAB5 .120 and its paired Windows bridge inspect the selected Codex composer when opening a conversation. A confirmed empty draft restores dictation and retires old send tickets; nonempty or unreadable drafts preserve existing controls. Observation never edits or submits a draft.

- Prepare TAB5 .120 with a one-line voice review hint that explains continued dictation. Rename the staged-draft action to “继续语音” and use “重试录音” before successful staging; preserve the existing explicit recording and send steps. Local candidate only.

- Continue TAB5 dictation at the desktop draft end instead of inserting a new line. Preserve existing punctuation and manual line breaks; insert a Chinese full stop only when the previous text lacks a separator. Keep target checks, exact readback and one-write safeguards.

- TAB5 .119 uses a medium-weight percent sign in the annual screensaver; preserve the approved digits, rounded labels and layout. Authorized USB installation and exact-image boot verification passed; the user confirmed typography and preview/touch exit. The user also confirmed automatic entry and reboot persistence, completing annual screensaver acceptance. No public release.

- Prepare TAB5 .118 annual-dot screensaver with device-side selection, full-screen preview, persisted style and leap-year rollover. Retain the classic saver and the quota countdown position; soften its color and separate time units. Hardware acceptance is pending.
- Guard bridge startup against launcher-virtualized profile files before loading pairing or quota history. Preserve full Codex titles for exact target checks and reject duplicate titles outside the device's shortened task list. No profile migration or credential replacement.

- Simplify the TAB5 bridge diagnostics page by removing the superseded short BLE OTA probe and completed window-comparison experiment from the normal UI. Retain transfer measurement, full-image preflight, boot verification and voice troubleshooting.

- Add TAB5 .117 three-transport OTA compression, an explicit BLE upgrade entry and separate decoded-image/transfer progress. Add the Codex weekly reset countdown without duplicate elapsed-cycle information. Keep legacy raw fallback and image/boot checks. BLE installation and exact-image USB boot verification pass; recorded installation through verification took 114.185 seconds, excluding completion delay/reboot. Wi-Fi/USB compressed upgrades remain pending on hardware.

- Prepare the local TAB5 .116 candidate and full-image RAM preflight. Exact-image compressed payload is 47.59% smaller; full-image hash and failure-path tests pass. The paired bridge binds results to the selected image, preflight never writes Flash, and Wi-Fi is restored on exit. Builds, regressions and actual notes previews pass; deployment and hardware acceptance remain pending.

- Add internally validated TAB5 BLE OTA range compression: the exact .115 image payload is 47.57% smaller, all 140 authenticated encrypted ranges roundtrip, and native decoder bounds plus fault regressions pass. Preserve window64/native7, voice scheduling, raw fallback and full-image checks. No deployment or reflash; the 120-second hardware goal remains unverified. See `docs/TAB5-BLE-COMPRESSION.md`.

- Withdraw the idle voice-poll deferral candidate and restore the verified window64/native7 bridge. Three RAM rounds took12.376s, but voice stop trouble also reproduced after rollback before recovering; causality remains unconfirmed. No full BLE OTA is scheduled.

- TAB5 .114 passes both 32/64/32 BLE RAM comparisons (18 rounds). The repeat takes 15.979/12.982/14.865s; 64 reduces elapsed time by 12.7% against the final32 baseline, with six complete64 rounds across both runs. The bridge now defaults to min(peerOffer,64) for large replies, keeps native7, and restores64 after comparisons; older peers, voice and small replies retain their bounds. Bridge-only deployment is verified against all six file hashes and the listening process, with BLE data acknowledged and the original display policy restored; no reflash was needed. The conservative repeat estimate is172s, still above120s; no full BLE OTA acceptance or hardware-ceiling claim.

- Same-process BLE queue comparison passes all nine RAM rounds: native 7/31/7 totals 12.658/14.547/13.459s. Queue31 is 11.4% slower than the two standard groups' mean; retain seven. TAB5 .114 advertises a 64-fragment response window. The bridge preserves final ACK, integrity, bounded concurrency and restoration; window acceptance is recorded above.

- Restored BLE cohort sender passes in 17.555s (whole-image estimate 224s), versus its earlier 13.997s and rolling's 19.213s; variability prevents attributing all differences to code. Add a bridge-only, one-click 7/31/7 pending-write comparison with separate results and automatic restoration of seven on completion/failure/cancellation. Local checks pass; temporary queue-depth gains await hardware testing. Keep .113 and the 120s OTA gate; no firmware reflash or full BLE OTA.

- Same-.113 BLE RAM comparison passed: temporarily stopping Wi-Fi took13.997s versus18.953s with Wi-Fi retained, 35.4% higher throughput in this one short pair. Wi-Fi recovered; estimates202s/280s exceed120s. The subsequent rolling-write trial also passed integrity but took19.213s with isolation (estimate236s), showing no speed gain. Withdraw that scheduling change and restore the seven-command cohort sender for a same-condition recheck; retain Wi-Fi isolation and .113 without reflashing. Radio variability still limits causal conclusions.

- TAB5 .113 and its paired bridge add an optional BLE RAM preflight that temporarily stops Wi-Fi and restores it after completion, cancellation or failure. Preserve saved networks; bound the stop lease and report failed restoration. USB-only numeric diagnostics expose negotiated data lengths, connection settings and receive-handler timing without growing the BLE identity. Local checks pass; coexistence speed gains and the 120-second full-OTA target still require hardware evidence.

- Bridge-only BLE scheduling preserves bulk priority for one second between successful large RPC replies and rechecks status deferral after acquiring the shared send gate. Required status refresh and active voice polling remain enabled. Add a bounded whole-preflight trace. Three hardware RAM rounds passed in 16.630 seconds versus 17.024 previously, a 2.3% shorter sample with the third round slower. The conservative whole-image estimate remains 213 seconds; sustained speedup and full BLE OTA are unverified. TAB5 .112 is unchanged.

- TAB5 .112 negotiates a separate 32-packet large-response write window with the paired bridge, reducing a 48 KiB reply from 13 ATT barriers to four while retaining at most seven native writes in flight. Older firmware, small replies, voice and device notification pacing retain their previous bounds. .112 now passes all three BLE RAM downloads at 40.89–50.20 KiB/s; the conservative whole-image estimate is 222 seconds, still above the two-minute goal. Full BLE OTA remains unaccepted.

- BLE large RPC replies now use a bounded queue of up to seven native write commands within the existing eight-packet window, drain submitted operations before the final ACK and preserve legacy/voice paths. Add actual connection and write-wait diagnostics plus explicit preflight budget-failure text. Bridge-only candidate for installed TAB5 .111; hardware speedup and the 120-second OTA target remain unverified.

- TAB5 BLE OTA candidate: temporary Windows 11 throughput preference, first-response bulk priority and a bounded RAM-only download preflight for the user's 120-second upgrade target. Windows 10 and legacy benchmark behavior remain compatible. Real BLE performance remains unverified; see [preflight limits](docs/TAB5-BLE-OTA-PREFLIGHT.md).

- TAB5 0.2.110-ui searches verified spare-image free space for an all-FF 128 KiB sample, preserves read errors and reports scan phase/count without erasing existing contents. .109 stopped before any writes; actual JEDEC ID is now available, and .110 completed three rounds at 8/16 KiB with restoration verified; 48/64 KiB were skipped by memory checks. Absolute hardware limits remain unproven. See [method and evidence](docs/TAB5-FLASH-BENCHMARK.md).

- TAB5 0.2.109-ui repair candidate moves storage testing to an internal task stack and checks its memory location before Flash calls. The .108 image installed successfully, but its test panicked with a PSRAM stack; exact assertion text was unavailable. Native checks do not establish the repair's hardware acceptance, and no Flash hardware-limit result is claimed.

- TAB5 0.2.108-ui adds an explicit local storage microbenchmark: three rounds of internal 8/16/48/64 KiB writes over a verified unused 128 KiB spare-partition tail, separate erase/write timings and restoration checks on completion, cancellation or failure. The paired bridge reads bounded numeric results and actual JEDEC identity through startup verification. Compare full-image OTA timing before discussing sustained limits; candidate checks do not establish hardware acceptance. See [method](docs/TAB5-FLASH-BENCHMARK.md).

- TAB5 0.2.107-ui replaces the clear control's eraser with an original broom, preserving equal widths and long-press behavior. Upgrade progress follows successfully written bytes; distinguish remaining writes after download from final verification, showing elapsed verification time without a stuck 99% label. Preserve integrity checks and rollback; this fixes feedback rather than claiming shorter installation. The user confirmed the broom and this upgrade's feedback, and .107 passed exact startup verification with the original cycle preserved. Installing .107 used the previous receiver; the new write-progress path still needs independent observation on the next upgrade.

- TAB5 0.2.106-ui candidate makes Previous/Next switch the desktop conversation and makes the Codex key open the current selection. Commit selection only after matching foreground confirmation and keep list order stable until reopening. Preserve unsubmitted staging on same-target reopening; failed navigation never authorizes voice in an unconfirmed target. Existing bridge support is reused; hardware acceptance is pending.

- TAB5 0.2.105-ui local candidate restores “长按清空”, matches its width and icon/text alignment to Re-record, and hides it until a voice draft is confirmed. The deployed desktop bridge reconciles transient write-provider exceptions by reading the same composer without repeating input; mismatched or unreadable results remain unconfirmed. The user confirmed real first dictation and re-record append remain unsubmitted; new-layout installation and hardware acceptance are pending.

- TAB5 0.2.103-ui USB recovery and .104 complete Wi-Fi upgrade passed exact-image boot verification. The user confirmed no blue screen; final verification took 0.881s, sender transfer 12.171s and device installation 18.845s excluding reboot. Existing network parameters and original display cycle are preserved; one upgrade does not establish extended stability.

- TAB5 0.2.103-ui repair candidate recognizes the linked PSRAM instruction interval during SDK executable-address checks, preventing valid HTTP/pthread cleanup callbacks from being rejected on task deletion. It also adds retained OTA phase timing. The .101 Wi-Fi baseline crashed before verification and did not install .102; recover through USB first, then validate Wi-Fi on hardware. No speed gain is claimed.

- TAB5 0.2.102-ui aligns the four connection-status columns: headings and state text share a left edge, checks follow the text with vertical row alignment, and subtle separators clarify grouping. Green/amber/gray meanings and Wi-Fi OTA transport remain unchanged.

- Fix TAB5 bridge submission confirmation ending prematurely when Codex replaces its composer after Enter. Reacquire the editor and retry bounded observation while preserving a single Enter attempt and no replay after an uncertain result; add separate quick-submit diagnostics.

- TAB5 0.2.101-ui candidate adds USB to Connection & Network and highlights positive states with green checks; USB without fresh desktop data shows an amber waiting state. Close and conversation arrows retain their appearance while locked during Codex key operations, fixing disabled-state flicker.

- TAB5 0.2.101-ui distinguishes first staging, actual repeated append and confirmed submission hints. A compact outlined 清空 control with an original two-tone eraser beside Re-record clears the current desktop target composer only on long press and restores Doubao. The paired bridge checks the current target and readback and deduplicates requests without navigation or submission; the TAB5 draft remains intact. Physical acceptance is pending.

- TAB5 0.2.100-ui local candidate centers the main keys, compacts the transcript panel and uses the official Doubao IME avatar. Green recording status shows the actual full microphone name; cancel and re-record icons share consistent sizing. Fix busy-state background flashing and desktop dictation overwriting the TAB5 local draft. The paired bridge appends further takes to the desktop Codex draft without submitting; only a separate click on the thick blue send arrow submits. Candidate builds and simulations do not establish hardware acceptance.

- TAB5 0.2.99-ui development candidate groups Codex/豆包 on a mechanical deck with brushed-metal collars, graphite/red lacquer faces, press/rebound motion and centered official application icons. Short captions have clear space below the collars. Reuse the voice key for start, stop/staging and separate manual submission. Cancel/Re-record retains desktop drafts; submission binds staging and never repeats after uncertain input. A stopped Codex is launched through its registered URI with a bounded cold-start wait. New controls and motion await hardware acceptance.

- On October 4, 2026 TAB5 0.2.97-ui USB installation and exact-image ota_0 VALID verification passed, retaining pairing and the original automatic cycle. The older receiver took 40.745 seconds for preparation/receiving/writing. The user confirmed .098 installation with smooth initial progress and no blue flash; reception continued during preparation with zero LCD underrun delta. Exact .098 boot verification and deployment closure remain unfinished.

- Local TAB5 0.2.97-ui authenticates the first received block and waits for the upgrade overlay before backup erasure. ESP32-P4 PSRAM execution, bounded prefetch and preparation/LCD diagnostics target the initial pause and blue flash; hardware acceptance remains pending.

- TAB5 0.2.96-ui uses the bottom-right Codex icon. The matching bridge pauses high-rate USB metrics and artwork during upgrades. A real USB installation reduced preparation/receive/write time from 59.570 to 38.526 seconds and passed exact-image boot verification; initial stalls and a blue flash remain unresolved. Codex Direct adds a scoped foreground activation fallback with actual-target verification, awaiting hardware confirmation.

- TAB5 0.2.94-ui aligns receive blocks with one 48 KiB firmware reply and prepares the required backup-partition extent before reception; preparation/receive/write phase timing includes preparation erasure. Its USB installation and exact-image boot verification passed, but the older receiver still took 61.668 seconds. The functionally unchanged 0.2.95-ui measured 59.570 seconds for that phase with the new receiver; the speed target was not reached.

- TAB5 0.2.92-ui negotiates larger USB binary fragments for single-frame 48 KiB firmware replies, keeps legacy receivers compatible, and retries only immutable OTA ranges with bounded timeouts. Partial USB sends poison the stream until reconnect. Fragmented internal memory falls back to 16/8 KiB flash staging before the slower external-memory path. Simulated fault tests pass; real upgrade speed and reliability remain pending.

- Add TAB5 desktop quick controls for five recent Codex conversations and Doubao start/stop, staging complete recognition in the desktop composer without automatic submission. Existing text and uncertain results retain the draft. See [candidate scope](docs/TAB5-QUICK-CONSOLE.md); hardware acceptance is pending.
- Fix draft insertion rejection for focused Codex composers and empty-field placeholder decorations. Reuse an open target window, wait for navigation readiness, and report insertion diagnostics without draft contents.
- TAB5 0.2.91-ui renames the entry to “Codex 直达” and moves it to a dedicated bottom-right footer slot. Each return opens the latest conversation anew; dictation binds to the actual opened conversation and remains an unsubmitted draft. Hardware acceptance is pending.

- Show the native ESP8266 Codex page beside TAB5 Codex quotas in the product cover and opening, using matching isolated demonstration values.
- Separate bridge, ESP8266 and TAB5 version sources, changelogs, tag validation and package names. Keep the published v0.5.0 payload unchanged.
- Query stable bridge and firmware releases independently in the Windows update center; retain legacy-bundle support.
- Add isolated ESP8266-only packaging and minimal per-component download staging; check TAB5 image/source identity rather than filename alone.
- Route authorized component-tag workflows separately: bridge and ESP8266 build and stage minimal draft downloads; TAB5 retains independent local image validation.

## 0.5.0 - 2026-10-03

### Release
- Windows Device Center and TAB5 integration are the main changes since 0.4.0. TAB5 firmware has its own version: the accepted baseline is 0.2.89-ui. v0.5.0 is the current stable release following maintainer-confirmed hardware acceptance on 2026-10-03.

### Features and fixes
- Add device registration, capability-based menus and independent TAB5 / ESP8266 connection services, with shared accounts and data sources.
- Consolidate display, connection, voice, calendar and backup/restore controls; improve narrow-window layouts and pointer visibility.
- Improve TAB5 USB / Wi-Fi / BLE transport, USB identity recovery after COM changes, firmware selection checks and attachment receipt queries.
- Recover camera resources and unsent drafts; insert voice results at the original cursor and recover results after the tested short and 45-second USB disconnections without duplicate insertion or automatic sending.
- Preserve quota history and recover cumulative daily usage across verified sampling gaps and reset segments; retain uncertainty for ambiguous overnight growth, account changes and unexplained decreases.

- Rewrite installation and flashing instructions for both hardware models, separating TAB5 factory-install ZIP from application-upgrade BIN. Refresh native screenshots and rebuild the full 54-second product video.
- Refresh the public regression suite for same-account DeepSeek cost retention and account/currency isolation; complete distribution-document fixtures.

### Validation and known limitations
- The maintainer confirmed hardware acceptance on 2026-10-03. Windows build/public regressions, macOS tests/build and ESP8266 build passed; see [release records](docs/RELEASE-0.5.0.md).
- Historical individual checks remain in [the acceptance checklist](docs/ACCEPTANCE-2026-10-02.md). Formal release does not establish new speed measurements, long-term stability or a fix for previously recorded intermittent USB interruptions.
- Mac remains Apple Silicon, ad-hoc signed and not notarized. Its platform-specific acceptance limits remain documented in the installation guide.
- Release downloads now contain five user packages and one checksum list. TAB5 factory installation and application upgrade both use 0.2.89-ui; ESP8266 materials apply only to ESP8266. TAB5 [source/build snapshots](docs/development/TAB5-SOURCE.md) and the product video are available from the repository.
- Own code, including TAB5, is uniformly MIT licensed; [third-party terms are preserved](docs/TAB5-LICENSE-SCOPE.md).

Development-stage records, including superseded candidates, are preserved in [the development history](docs/DEVELOPMENT-0.5.0.en.md).

## 0.4.0 - 2026-09-23

- Keep the last valid device page visible for up to 30 seconds during brief host stalls while the eight-second USB-to-Wi-Fi fallback and offline diagnostics remain intact. Explicit host shutdown still shows offline immediately.
- Reduce Windows background activity scans under load so serial heartbeats and status updates have more room to run.
- Rebind the Windows LAN listener when the laptop's network address changes, choosing an adapter on the device's subnet and updating the device over USB when available.
- Add paired, HMAC-authenticated local discovery for Windows and ESP8266. When either or both IP addresses change without USB, the device can find the bridge on the current local subnet; it saves a new address only after an authenticated status request succeeds.

Windows Release and ESP8266 firmware builds passed. On the real device, a five-minute Wi-Fi-only status run, discovery from an obsolete bridge port, ordinary Wi-Fi fallback and USB recovery passed. A loopback test changed both simulated endpoint addresses. Simultaneous real DHCP address changes with the device on independent power remain unverified. Discovery requires reachable local broadcast and is not yet supported by the macOS bridge. Install both the new Windows bridge and firmware for address discovery; the Windows installer remains unsigned and the Apple Silicon app remains ad-hoc signed and not notarized.

## 0.3.0 - 2026-09-21

- Pause only USB during flashing/backup and resume on completion or flasher disconnect; keep the resident bridge and network listeners running without restarting any executable. Refine Windows typography, spacing, button states and collapsed details.
- Limit flashing progress display/log updates to once per second and stop stalled read-only backups after 30 seconds without output before compatibility retry.
- Stream backspace-delimited backup progress immediately; use 460800 baud with read-only compatibility fallback before writing.
- The flasher selects a single connected USB device without replugging, retains device identity across scan errors, uses a dedicated icon and connection panel, and bundles verified tools for offline flashing.
- Automatically select a single connected USB device and request unplug/replug only when multiple devices are ambiguous; hide technical port names in collapsed details, exclude Bluetooth on Windows, and stop if the selected USB identity changes.
- Add native Windows and Apple Silicon Mac firmware-flashing windows: automatically identify the device, select firmware ZIP/BIN, and use the bundled hash-pinned official tool to back up the full flash, write and read back for verification. Release USB ownership during flashing and restore the USB connection afterward.
- Consolidate download, installation, firmware and first-connection instructions into one illustrated guide.

Earlier Windows flashing and backup throughput were verified on hardware. The new USB-only handoff passed local regressions; hardware acceptance is pending. The new Mac window passed macOS CI tests and build, while Mac hardware acceptance remains pending. Published as a release; the acceptance limitations above still apply. Windows setup is unsigned; the Apple Silicon app is ad-hoc signed and not notarized. Devices already running v0.2.2 firmware do not need reflashing for this update.

## 0.2.2 - 2026-09-21

- Fix completion/input alerts overriding manually selected pages on the device and mirrors; automatic mode retains alert navigation. Device diagnostics now distinguish selected, effective and rendered pages.

- Windows: request real-time Hong Kong quotes, preserve three-decimal prices and index precision, and recover missing symbols individually without erasing cached quotes.
- Windows: queue display selections without blocking the UI, prioritize the latest selection between USB resource chunks, and prevent queued heartbeats from restoring an older selection.

Update both the bridge and ESP8266 firmware to receive the manual page-selection fix. Windows/device behavior has been verified on hardware and confirmed by the user. macOS shares the display-policy fix; interactive Mac acceptance remains pending. Windows setup is unsigned; the Apple Silicon app is ad-hoc signed and not notarized.

## 0.2.1 - 2026-09-17

### Windows fixes

- Retry transient DeepSeek balance failures once, renew idle connections between refresh rounds, and distinguish authentication, access, rate-limit and network errors while retaining the last successful balance.
- Wait for automatic recovery before showing quota warnings; cancel pending warnings after success and preserve specific failure reasons.
- Add bounded, credential-free API and browser-stage diagnostics to distinguish login redirects, capture timeouts and successful balance saves.
- Retry local and LAN listener startup when a port is temporarily unavailable. Persistent conflicts can use the existing AIBOT_HTTP_PORT override; USB pairing passes the selected port to the device.

### Validation and limitations

- Windows Release build, isolated public self-tests and status capture passed. Live DeepSeek refresh, Zhipu login followed by background balance refresh, USB device connectivity and preserved automatic cycling were verified.
- Zhipu still requires a valid embedded-browser login; login retention across another restart has not been verified. A port held by another process is not forcibly reclaimed.
- macOS and firmware behavior are unchanged from v0.2.0; this release does not require reflashing an already working v0.2.0 device. Their packages are rebuilt with this version.
- Pre-release. Windows setup remains unsigned; macOS is Apple Silicon/macOS 13+, ad-hoc signed and not notarized. Previously documented provider and platform acceptance limits remain in effect.

## 0.2.0 - 2026-09-16

- Complete MiMo console-session quota routing, strict parsing, persistence, mirror and optional firmware display. Restrict domestic background checks and warnings to selected pages with prior authorization or configured credentials; live MiMo acceptance remains pending.

- Add documented StepFun API-wallet and Baidu Qianfan model-package adapters, credential inputs, failure retention and optional device fields. Cloud wallets are excluded; live-account, hardware and macOS runtime acceptance remain pending. Other catalog providers remain unconnected.

- Prefer official DeepSeek/MiniMax quota APIs and the documented Kimi Code local usage API; add in-app credentials, browser-failure reminders, stale labels and domestic manual refresh. Ali Token Plan and Zhipu wallet keep browser fallback where no matching documented public API is confirmed.

- Add a lunar date line to Windows/macOS screensavers and the ESP8266 screensaver, with leap-month labels, offline device calculation and movement bounds that preserve the PC OFF area. Hardware and macOS runtime acceptance remain pending.

- Fix macOS 13 lunar-calendar compilation while preserving leap-month support.
- Improve domestic settings layout and long provider labels at high DPI; synchronize the nine-image homepage gallery and setup guides.

Pre-release: Windows adapters are included; new provider live-account acceptance and physical-device acceptance remain pending. macOS includes lunar dates and protocol compatibility, not the new Windows-only credential adapters. Install updated firmware for lunar dates and additional device pages. Windows setup remains unsigned; macOS is ad-hoc signed and not notarized.

## 0.1.3 - 2026-09-16

### Windows installation

- Add a Chinese online setup wizard, about 8 MB. Detect .NET 8 Desktop Runtime and WebView2; skip installed prerequisites and download missing components automatically.
- Show .NET download progress, support cancellation and retries, and verify the pinned SHA-256 before execution. Create shortcuts and preserve user settings and shared runtimes on uninstall.
- Include the setup EXE and checksums in release packaging. Compile-time download tests cover valid/corrupted/404 responses and detection without downloads.

### Guides

- Put the recommended Windows download first and add illustrated download selection, installation, firmware backup and flashing guides. Keep ZIP/manual and source-build instructions separate.
- Synchronize Chinese/English homepages, show six feature screenshots in a two-by-three grid, and update distribution notices and acceptance criteria.

This is a pre-release. The setup is unsigned; clean-machine prerequisite installation, visual acceptance, upgrade and uninstall remain unverified. Missing runtimes require internet access. Existing working firmware does not need reflashing for this installer update.

## 0.1.2 - 2026-09-16

Windows maintenance pre-release. Upgrade by exiting the bridge, extracting the entire new archive and retaining existing user AppData.

### Quota trends and window fixes

- Show verified daily quota increments even for incomplete days, with explicit coverage notes; missing intervals remain unknown and incomplete days do not enter the full-day average.
- Tolerate small reset-deadline jitter without hiding valid usage or falsely reporting resets. Retain checks for actual usage drops, changed windows and offline gaps.
- Retry transient history-save failures on the next sample while preserving unreadable original history files.
- Fix clipped mirror connection information at high DPI. Compact and center the trend window, improve table columns and chart labels, and clarify quota units.
- Fix the first trend-window opening being suppressed by hidden process startup; verify native window visibility before activation.

### Other Windows improvements

- Keep Windows USB heartbeats, LAN status and the tray responsive during slow session-log scans by publishing cached activity from a single background scan. Preserve the last successful sample on failure, skip unchanged lifecycle files, and expose loopback activity/device diagnostics.

- Add an About AI-bot tray menu with the running application's version, author yaoyouzhong & Codex, and a clickable GitHub repository link.

### Validation and limitations

- Windows build, status capture, quota regressions, 175% layout checks and hidden-start first-opening reproduction were verified locally. Live quota samples were saved after restart.
- Missing historical samples cannot be reconstructed. Recorded daily use may be lower than the full-day total.
- macOS and firmware behavior are unchanged; packages are rebuilt with the release. macOS 13+ Apple Silicon only, ad-hoc signed and not notarized; real-Mac acceptance remains pending. No new full physical-device acceptance is claimed.
- Windows requires .NET 8 Desktop Runtime x64 and Microsoft Edge WebView2 Runtime. Packages include license notices and SHA-256 checksums.

## 0.1.1 - 2026-09-11

First public GitHub pre-release. Version 0.1.0 was a development baseline; this release includes accumulated desktop, firmware and packaging work as well as the Windows startup fixes.

### Windows fixes

- Use a current-user interactive logon task with a five-second delay, avoiding the ordinary Run startup queue. No password or elevation; battery operation and unlimited runtime are supported. Toggle an older startup entry off and on once to migrate it, and register again after moving the app.
- Prevent duplicate bridge instances before initializing providers or opening USB/HTTP connections.
- Simplify tray hover text to the app name and click instructions, preserving display-page activity states. Normal startup does not flash a console.

### Included capabilities

- Windows tray and mirror with Claude/Codex activity, account quotas, local token accounting, completion/attention signals and Codex quota trends; domestic authorization for Alibaba, Kimi, MiniMax, DeepSeek and Zhipu.
- Weather, stocks, system metrics, music, automatic cycling and screen saving; retain last-successful provider data when requests fail.
- ESP8266 USB-first transport at 460800 baud, authenticated Wi-Fi fallback, standalone clock, device controls and acknowledged binary-resource transfer. Improve UART buffering, page decoding and RGB565 rendering.
- Original BYTE SPROUT pet, per-role animation selection/import and persistent resources. Private account data and personal artwork are excluded.
- Apple Silicon macOS test app with menu bar, mirror, activity/quotas, weather, stocks, metrics, music, pets and device controls, subject to platform limits.
- Bilingual installation/flashing guides and Windows, Mac, source and firmware-materials archives with license notices and SHA-256 checksums.

### Validation and limitations

- Locally verified Windows Release build, console-host status diagnostics, startup-task launch and three duplicate launches. Cycle pages/order/interval were preserved. A fresh reboot/logon and actual mouse-hover appearance were not observed in this acceptance run.
- Cloud build/tests and archive checks must pass before publication; automated tests do not establish real-account or physical-device acceptance.
- Mac requires macOS 13+ and Apple Silicon. It is ad-hoc signed, not Developer ID signed or notarized. Real-Mac first launch, permissions and device behavior remain unverified; this package does not support Intel.
- No ESP8266 USB port was available during the latest startup-fix acceptance. Complete page-by-page and Wi-Fi fallback acceptance remains pending. The protocol version is unchanged.
- Windows requires .NET 8 Desktop Runtime x64 and Microsoft Edge WebView2 Runtime. Extract the entire ZIP, stop the bridge before upgrading and preserve user AppData.

## 0.1.0 - 2026-09-04

### Features

- Add an independently implemented Windows tray bridge.
- Add an ESP8266 USB status-display firmware.
- Define the `@AIBOT` version 1 line protocol.
- Add CI and tag-driven release scaffolding.

- TAB5 0.2.73 hardware acceptance: the 32-packet BLE RPC window failed; candidate bridge 073b restores the eight-packet limit pending hardware retest.

- TAB5 0.2.74 candidate separates paced notification grants (32) from eight-packet PC writes. Hardware photo acceptance remains 4.6 seconds on 0.2.73/073b; the three-second target is unmet.

- TAB5 0.2.74 photo acceptance: 111532 bytes uploaded in 3845 ms. Candidate bridge 074b reduces competing status and idle voice polling during photo upload; hardware validation pending.

- Bridge 074b hardware photo: 115186 bytes in 3396 ms. Candidate 074c retains photo priority through attachment handling and final reply, including small final chunks; hardware validation pending.

- Bridge 074c / TAB5 0.2.74 real BLE photo accepted: 118509 bytes in 2748 ms (user: 2.7 s), with unchanged 1280×720 JPEG quality 85 and no notification failures. Full transport closeout retest remains pending.
