# TAB5 firmware changelog

TAB5 versions are independent of the desktop bridge and ESP8266. `versions/TAB5` records the release target; the image's embedded version and matching notes must agree with it before publication.

## 0.2.143-ui - 2026-10-07

- Prepare a new public candidate from the accepted display baseline, retaining lossless Chinese fonts and six public screensavers including the floral monthly calendar.
- Keep automatic-screensaver state intact when a delayed background tab event arrives. Later gallery/heap diagnostic and cache experiments remain withdrawn.
- Keep daily painting/calligraphy local-only. Public application headroom is 696,096 bytes without a partition change. Supersedes .133; public-image physical acceptance remains pending. See [candidate record](../RELEASE-CANDIDATE-0.5.1.md).

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
