# TAB5 firmware changelog

TAB5 versions are independent of the desktop bridge and ESP8266. `versions/TAB5` records the release target; the image's embedded version and matching notes must agree with it before publication.

## 0.2.292-ui - 2026-10-29

Local quota-entry candidate; installed locally, not publicly released.

### Model quotas
- Clicking the bottom Quotas module, including repeated clicks while already there, or swiping into it defaults to Codex details.
- Overview model cards still open their corresponding model; clicking Quotas afterwards returns to Codex.

### Validation
- The original .291 behavior fails the new regression assertion; all eleven model reentries, repeated clicks, overview deep links and swipe entry pass after the fix. Actual rendered screenshots were reviewed.
- P4 build passes with unchanged board configuration definitions. Production image validation and the actual fixed-path sidecar preview pass. User-authorized Wi-Fi installation passes exact ELF, VALID partition, three live samples and unchanged display settings in 71.405 seconds through boot verification. The user confirms that overview model details → Quotas returns to Codex; touch interaction passes.

## 0.2.291-ui - 2026-10-29

Local OTA contention and radio-policy candidate; not publicly released.

### Bluetooth upgrade
- Add bounded mailbox admission for OTA reads, OTA control and boot verification; recover from unpublished-busy errors only for immutable image reads.
- Allow the existing temporary Wi-Fi pause during actual BLE OTA in automatic mode when the authenticated Windows/Mac bridge opts in. Preserve saved mode and restore the radio on inactivity, failure, disconnect or completion.

### Validation
- P4 build uses unchanged board settings; actual RPC admission, radio ownership/restoration and OTA fault/recovery tests pass, including first-block busy recovery and exhaustion before flash writes.
- Initial Wi-Fi installation and exact ELF/VALID/three-live-sample boot verification pass with display settings preserved. Same-image BLE comparison is recorded in the October 29 Mac BLE report.
- Identical-image BLE baseline installs in 399.979 seconds; R21 radio isolation takes 338.938/340.052 seconds across two reinstalls (about 15% less), with zero replay/write failures and successful boot/settings/radio-restoration checks. BLE remains minutes-long; actual controller/PHY evidence and Windows runtime validation remain unavailable.

## 0.2.290-ui - 2026-10-29

Local compatibility and diagnostics candidate; not publicly released.

### Bluetooth upgrade
- Use a 30–45 ms final fallback range to satisfy Apple connection-parameter rules; 15 ms and final fallback profiles use a six-second timeout.
- Preserve the first block's actual failure code. Authentication, ownership and integrity failure still prevent backup-partition writes.

### Validation
- P4 build with unchanged board configuration, negotiation tests, OTA fault/recovery matrix and three new first-block failure cases pass.
- .289 full BLE reinstall takes 413.770 seconds to install and 468.350 seconds through healthy boot; its earlier pre-first-block failure remains unresolved.
- Wi-Fi installs in 22.673 seconds; identical-image BLE reinstall takes 418.138 seconds, or 474.011 seconds through healthy boot. Exact ELF/VALID/three live samples/settings pass, with zero replays/write failures. Installation is 50.01% shorter than .288/R19, but still takes minutes.

## 0.2.289-ui - 2026-10-29

Local BLE negotiation candidate; not publicly released.

### Bluetooth upgrade
- Preserve the Windows-compatible 7.5 ms request, then try 15 and 30 ms after rejection, with bounded collision retries.
- Await NimBLE procedure completion, restore the actual idle parameters and stop retries after disconnect.
- Preserve the wire protocol, authentication, integrity checks, inactive partition and user preferences.

### Validation
- P4 build, unchanged board configuration and negotiation simulation pass.
- Wi-Fi installs in 21.703 seconds; exact ELF/VALID/three live samples and settings preservation pass.
- BLE 256 KiB download improves from 41.257 to 20.511 seconds. Full installation takes 413.770 seconds, or 468.350 seconds through healthy boot, with preserved settings. The earlier pre-first-block failure remains unexplained; Windows upgrade speed is not claimed.

## 0.2.288-ui - 2026-10-28

Local candidate installed and identically reinstalled over Wi-Fi; exact ELF, VALID partition, three live samples and unchanged settings pass. Not publicly released.

### Pipelined installation
- Start with a 64 KiB erase, then erase subsequent blocks immediately before writing them while an independent worker continues receiving the image.
- Preserve inactive-partition bounds, authenticated reception, whole-image integrity, SDK validation, rollback health checks and preferences.
- Retain .287 HTTP range connection reuse and same-version reinstall.

### Validation
- Full P4 build with unchanged board configuration, 840 OTA boundary/fault cases and 34 recovery cases pass. Sixteen cases explicitly prove the first write precedes completion of reception and full-partition erasure.
- Actual same-image Wi-Fi range installation takes 25.921 and 28.248 seconds in two accepted runs. The initial erase stall is removed, but this is not a claim of a ten-second installation.
- The Mac continuous-stream experiment retransmits heavily and recovers via ranges; it is not accepted as a speed improvement. Healthy boot verification remains separate from installation time.
- October 29 Mac R18 follow-up: the unchanged .288 image installs over Wi-Fi in 21.666 seconds with 32 ms data wait after a native-socket sender fix; exact boot and preferences pass. See [TCP comparison](MAC-TAB5-OTA-TCP-20261029.md).

## 0.2.287-ui - 2026-10-28

Local candidate installed over USB and identically reinstalled over Wi-Fi, with automatic exact boot verification and unchanged settings. Not publicly released.

### Wireless upgrade
- Retain successful HTTP range connections to reduce repeated TCP setup.
- Failed reads still reconnect at the existing offset, retaining independent authentication, integrity, inactive-partition protection and preferences.

### Validation
- P4 build, unchanged board configuration, 320 OTA fault cases and 34 recovery cases pass.
- Actual USB installation takes 38.793 seconds; identical-image Wi-Fi installation takes 25.578 seconds, including reception, erase/write and image verification. Healthy boot verification is separate.

## 0.2.286-ui - 2026-10-28

Local candidate installed and identically reinstalled over Wi-Fi; partition transition and exact boot verification pass, with unchanged display settings. Not publicly released.

### Firmware reinstall
- Allow explicitly requested same-version and identical-image reinstall; receiving an offer never starts installation.
- Preserve draft saving, inactive-partition writes, image integrity, boot verification and display preferences.

### Validation
- Full P4 build, 320 original-version/identical-image fault cases and recovery checks pass.
- Mac and Windows distinguish Reinstall; unchanged version heartbeats cannot prematurely finish a reinstall request.

## 0.2.285-ui - 2026-10-28

Local candidate installed with user authorization; exact ELF, VALID partition and three live samples pass, with display settings preserved. Physical visual acceptance awaits the user at the device. Not publicly released.

### Composer
- Refresh the empty composer before synchronous draft persistence after long-press clear, avoiding stale text while storage blocks the UI task.
- Preserve photos, IME cleanup, busy guards and failed-save retries, plus the .284 capture-device labels and quota captions.

### Validation
- Full build with unchanged board configuration and production image checks pass; the actual notes sidecar's headings, lists and scrolling preview were inspected.
- Sixteen real long-press, partial-flush and pre-storage pixel scenarios pass. The baseline retains 2273 stale pixels at storage entry; the intermittent persistent symptom still needs a hardware retest.
- Share helper functions to reduce duplicated machine code, retaining the original OTA partition and 32-byte headroom. Display settings are preserved.

## 0.2.284-ui - 2026-10-28

Local candidate based on the committed, verified .283 archive; not a public release.

### Voice capture device
- The Codex recording hint displays the actual TAB5/Mac/DJI input device and recognizer, with status/timer and device name on separate lines.
- Reuses source/sourceName with fallback for older bridge responses lacking a name.

### Codex quota captions
- The Overview Codex card uses “周额度已用”; the Codex model-quota page uses “周额度已使用”.
- Other providers and quota calculations are unchanged.

### Validation
- Full ESP-IDF build, unchanged board configuration, production image/notes validation and actual LVGL capture/quota/recording-interaction checks pass.
- Physical installation and acceptance are tracked in [the Mac voice record](MAC-VOICE-SOURCE-20261028.md).

## 0.2.282-ui - 2026-10-27

### System diagnostics
- Prevent the system-alert icon from overlapping Performance and Diagnostics tabs; place overall status and guidance first.

### Check details
- Separate findings, measured data and guidance; add a return-to-diagnostics button and improve long-text layout.
- The user confirmed the interface on a TAB5 updated with settings preserved; native UI regression and startup verification passed.

## 0.2.281-ui - 2026-10-27

Release prepared after user hardware acceptance.

- Retain the .280 Codex long-press clear behavior, photo preservation and draft persistence protections.
- Read only pairing state and public device identity in BLE receive/proof paths instead of copying the complete configuration. The actual input callback frame falls from 512 to 176 bytes under the unchanged 320-byte limit.
- Repair five standalone native test targets against .279 interfaces, including CPU-owned photo output, cache ownership failure, ISR extraction and RPC admission. All native targets compile; 27 relevant test executables and the complete LVGL preview pass.
- Preserve the existing OTA partitions. User hardware acceptance passed. The real NimBLE task minimum stack watermark was not measured separately; ELF frame limits and physical BLE operation are separate evidence.

## 0.2.280-ui - 2026-10-27

Local candidate; not publicly released.

- Add Long press to clear to the normal Codex draft editor, visible with the keyboard open. Short clicks, release and repeat events do not clear text.
- Clear only the selected task's text and pending spelling while retaining photos; block clearing during recording, sending and uncertain delivery.
- Persist through the existing draft checkpoint/sync flow, reject stale loads after clearing, and report checkpoint failures while retaining retries.
- Based on the complete .279 source. Resolve nested Windows tool-directory links for builds. Hardware acceptance is pending; no public release.
- Use more compact compilation only for widget construction to retain the existing OTA slots; rendering, audio, camera and transport compilation flags remain intact.

## 0.2.279-ui - 2026-10-26

- HD preview first borrows a detached gallery spare or an idle camera frame, reusing existing full RGB565 storage before general-heap fallback instead of requiring another1843200-byte contiguous allocation.
- Release/reclaim follows existing gallery ownership; visible artwork, retained photographs and active CSI frames are excluded, while foreground priority and original deadlines remain intact.
- Clean and invalidate CPU-written USERPTR frames before their initial camera queue submission. Cache-sync failures unwind before STREAMON and permit a subsequent reopen.

## 0.2.278-ui - 2026-10-26

- Allow up to one second of pre-publication admission for computer control on USB/BLE RPC mailboxes, without replaying uncertain operations or extending response deadlines.
- Pause unpublished preview chunks during computer status reads or explicit controls, then resume within the original ownership and preview deadline. Voice, camera and update priorities remain in place.
- Correct the USB/BLE status location hint to name the Network Connection page, matching the actual navigation hierarchy.

## 0.2.277-ui - 2026-10-26

- Reduce full-resolution sampling and SHA scheduling delays for real-device preview while preserving HD, complete verification and foreground voice/camera/OTA priority.
- Preview hashing keeps internal1KiB DMA staging and yields after at most16KiB or about1ms; photo uploads and ordinary RPC retain their pacing.
- The companion viewer may shorten next-frame waits; preview v2, partitions, pairing and display settings stay unchanged.

## 0.2.276-ui - 2026-10-26

### Fixed
- Reserve HD capture scratch before slow admission and reuse it during preview, avoiding repeated large allocations while pages change. Release after five idle seconds.
- Foreground voice, camera and OTA reclaim idle scratch without stealing an active worker buffer.
- Preserve pairing, network, sound and display preferences. Sustained HD and foreground interaction need separate physical acceptance.

## 0.2.275-ui - 2026-10-26

### Fixed
- Retry only unpublished preview admission while task reads share the mailbox, within the original owner and deadline. Preserve voice/photo/OTA priority and never replay an uncertain published request.
- Preserve pairing, network, sound, display settings and existing HD recovery. Physical page-switch and reopen acceptance is recorded separately.

## 0.2.274-ui - 2026-10-25

- Fix the screen_preview stack underflow captured on273: move voice state into the worker PSRAM job, use a16KiB stack and expose numeric remaining-stack/allocation-phase observations.
- Retain273 admission,30-second expiry, complete-frame checks, photo feedback and diagnostic layout without changing partitions or user settings.
- Local candidate; physical and automated acceptance are recorded separately. Not a public release.

## 0.2.273-ui - 2026-10-25

- Avoid periodic diagnostic heap walks; yield in the wireless worker and bound HTTP body-read duration. Sustained blue-flash recovery still requires physical verification.
- Reclaim unpublished preview cache, grow JPEG output only as needed and repeat the existing authenticated begin during encoding without extending the 30-second request bound.
- Combine photo adding/completion feedback and the accepted grouped connection-diagnostic layout.
- Preserve pairing, networks, drafts, photos and display preferences. Local candidate, not a public release.

## 0.2.272-ui - 2026-10-25

- Apply a changed recording transcript in one edit instead of rebuilding the text area character by character every100ms, avoiding repeated draft updates and layout work.
- Preserve UTF-8 insertion anchors, existing drafts, cancellation recovery and the manual input length limit. Actual LVGL regressions pass; single-tap stop still requires physical acceptance of this candidate.
- Local candidate, not a public release. Existing pairing, network and display settings are retained; device-preview optimization remains deferred at the user's request.

## 0.2.271-ui - 2026-10-25

- Improve the task-page round recording target and press response; prevent late replies from overriding stop/cancel intent.
- Defer background read receipts during capture while retaining identity, session and latest-turn validation.
- Send an essential USB acknowledgement when optional diagnostics exceed capacity, preserving sequence/authentication/capabilities and existing line limits.
- Pause optional preview cache preheating and reclaim rebuildable hidden-page/compressed-image caches to reduce HD preview memory pressure.
- Native/parser/target compilation checks do not replace physical recording and HD-preview acceptance; not yet installed or publicly released.

## 0.2.270-ui - 2026-10-25

- Keep one cancellable recording-start intent during background reads, show stop intent immediately and bound voice mailbox admission before publishing any request.
- Negotiate HD JPEG landscape/portrait previews and unchanged-frame receipts while retaining the legacy preview protocol and read-only display sampling. Mac and Windows adapters are updated together.
- Local candidate; recording reliability, HD previews and remaining physical acceptance still require verification.

## 0.2.269-ui - 2026-10-24

### Camera and interaction

- Open the local camera during ordinary background conversation reads/draft saves, immediately report the three-photo limit, and retain attachments plus initial-draft restoration. Consume an existing request result before uploading a photo.
- Renew USB ownership on valid delta states to prevent false expiry. Retain at most one explicit media tap during a status read, bound to its device/host/session/track, executing or cancelling within three seconds without replaying side effects.

### Preview, layout and reminders

- Fix the invalid cache-flag combination that leaks a global mutex during screen preview. Keep the official SDK and use valid aligned-framebuffer synchronization.
- Focus connection diagnostics on Wi-Fi and host-bridge communication; remove unrelated local pairing/activity/voice-toggle indicators, correct the entry description and compact the controls.
- Add an original dedicated 0.68 s agenda dual-note cue. The user accepts the second WAV version; retain user volume/mute, the voice worker/capture priority and existing countdown/other sounds.

### Local candidate validation

- This unified local candidate inherits accepted .268 behavior; build, installation and physical results are recorded separately and do not constitute public release. Generated configuration matches .268. Six patches apply sequentially and match 13 files, with focused native/UI and target-object checks passed.
- The user defers photo sending until camera startup is fixed. Test the new camera first, then attachments/mirroring; digital WAV review does not prove speaker sound. Keep the existing long-duration connection observation and unavailable ESP8266-hardware boundaries.
- See the [current acceptance checkpoint](MAC-CURRENT-ACCEPTANCE-20261024.md). Preserve pairing/network/display/data; installation still needs this candidate's explicit authorization.
## 0.2.264-ui - 2026-10-24

- Add bounded OTA elapsed-time diagnostics and a retained RTC snapshot; preserve the accepted .262 photo fix and existing Stop/ownership checks.
- Archive the [.264 source overlay](tab5-264-source/README.md), including configuration and tests. Exact .264 Wi-Fi installation and VALID boot were verified; installation took 61.327 seconds and remains slow. The user accepted deferring this performance issue on 2026-10-24.
- See [hardware evidence and limits](TAB5-OTA-264-ACCEPTANCE.md). This development synchronization creates no release and excludes .265/.266 diagnostic candidates.

## 0.2.262-ui

- Reduce photo-window display interruption by skipping diagnostic heap walks; retain allocation admission, photo contents and OTA behavior.
- Archive the complete .259-.262 source overlay in [tab5-262-source](tab5-262-source/README.md), including required fixture sources. The user previously confirmed successful photo addition without flashing. This commit does not rerun tests, build or flash.

## 0.2.257-ui - 2026-10-23 (local candidate; not installed)

### OTA recovery

- Recover transient USB/Wi-Fi/BLE download interruptions in the same installer without re-erasing completed blocks. Wi-Fi streams resume through authenticated ranges when the selected Windows bridge advertises support.
- Bound retry time/count, preserve Stop and selected-computer ownership, and retain complete-image/boot validation. No persistent power-loss/reboot resume is claimed; Mac source is unchanged.
- Native task/range fault injection, real Windows HTTP/RPC and compression checks passed. Physical interruption acceptance is pending. Photo blue flashing remains unresolved.
- See [implementation and acceptance](TAB5-OTA-RESUME-257.md).

## 0.2.256-ui - 2026-10-23 (installed; blue flashes remain)

### Photo still-display hold

- Keep the current scanout while encoding/uploading, using a dedicated renderer pause instead of live-camera framebuffer selection. Release accumulated invalidations without forcing a full-screen redraw.
- Preserve DMA completion ownership, touch/business timers, four orientations, three attachments and .255 BLE fixes. The actual port/real LVGL tests, 21 related native tests and full UI regressions passed; physical blue-flash acceptance remains pending.
- See [candidate evidence](TAB5-FOLLOWUP-256.md).

## 0.2.255-ui - 2026-10-23 (installed; blue flashes remain)

- Fix P4 Hosted ATT TX resource errors causing a device restart; move CPU-only HCI staging out of internal DMA memory and free failed copies correctly.
### Photo frame hold, BLE recovery and Windows upgrade stop

- Hold the already rendered full-screen photo through encoding/upload without another full-frame allocation or copy; restore scanout before the composer or retained-photo retry page. Preserve quality, orientation and three attachments. The exact .255 was installed; the user still observes blue flashes.
- With authenticated `data.imageChunkRetry=1`, BLE photo chunks use a 15-second deadline and one recovery opportunity per image: wait at most 20 seconds for the same host/session/mode, then retry the immutable range with a fresh nonce. Missing capability, explicit errors and non-photo actions do not retry; numeric diagnostics retain the first failure.
- Companion Windows stopping withdraws an upgrade before its final range and confirms through a new post-stop challenge or exact authenticated USB status sequence; guard delayed ready commits and rejected requests. Mac is unchanged.
- Builds, 17 current native tests, full LVGL and targeted Windows regressions pass. status-once was blocked by the profile-data guard (exit 2); exact .255 boot and first BLE photo acceptance passed; blue flashing failed and active OTA stop/crash regression remains pending. See [candidate evidence](TAB5-FOLLOWUP-255.md).

## 0.2.254-ui - 2026-10-23 (installed; blue flashes persist)

### Photo encoding and quota heading

- Encode camera RGB565 photographs in software one row at a time, retaining full resolution, quality 85 and YUV420. Remove photo hardware-encoder DMA/cache maintenance and the long display lock; retain the captured frame, three attachments and failure retries. The exact .254 boot is verified; user testing still reports blue flashes.
- Restore the concise weekly-quota trend heading without the carry-over explanation and remove the asterisks beside daily values; preserve all carry-over data and partial-history semantics.
- .254 is installed with exact ELF, ota_1 and VALID state verified. Blue flashes persist; the first BLE upload waited and failed, its retry succeeded, and Wi-Fi is fast. Both .253 BLE unlock attempts remain accepted. Windows and Mac are unchanged by this firmware payload.

## 0.2.253-ui - 2026-10-23 (installed; BLE accepted; blue flashes persist)

### Photos and credential admission

- Move the completed preview into the attachment cache instead of allocating/copying another full preview; pace preview generation every four rows and use 16-byte JPEG DMA bursts. Preserve quality, three-photo editing and accepted screensaver orientation. Blue-flash improvement still requires device acceptance.
- Remove the requested photo receipt/viewer notes. Preserve the current proportional viewer layout.
- Wait up to one second for credential mailbox admission before sending one USB/BLE request, within the original deadline and host generation. No credential or HID replay. Both user BLE attempts passed after installation; blue flashes still fail.
- The Windows quota candidate is independent of this firmware: banked-reset history, observed early-reset segments and uncertain cross-day growth are documented in the companion report.

## 0.2.252-ui - 2026-10-22 (installed; camera/gallery accepted; blue flashes persist)

### Photos and gallery screensavers

- Fit attachment previews proportionally to available space in all four orientations. Use the full portrait pixel budget and reuse removed-photo cache entries before evicting retained attachments.
- Split image cache synchronization into 16 KiB spans with yields and avoid clearing the entire JPEG output allocation. Preserve quality and panel timing; physical blue-flash improvement remains unverified.
- Integrate the painting/calligraphy portrait fix: borrow two idle reserved camera frames, return every loan before camera startup, and exclude the captured photograph. See [integration evidence](TAB5-FOLLOWUP-252.md).

## 0.2.251-ui - 2026-10-22 (installed; add/delete passed; blue flashes persist)

### Photo display and quota history

- Temporarily prioritize LCD memory reads during still-image preparation and restore the previous arbitration settings on completion/failure. Preserve resolution, quality, panel clocks and three attachments. Blue-flash improvement requires hardware confirmation.
- Mark quota bars containing verified cross-day carry-over with `*`; Windows computes reset segments and excludes partial dates from complete-day averages.
- Numeric wireless diagnostics include the priority lease readback. See [candidate evidence](TAB5-FOLLOWUP-251.md).
- The user confirmed attachment add/delete works, but adding still flashes blue and the attachment viewer is too small. Boot verification and zero underrun counters are not display acceptance.

## 0.2.250-ui - 2026-10-22 (installed; agenda passed; photos still flash blue)

### Photos, diagnostics and agenda

- Drain display copies before photo encoding and prevent concurrent UI copies until the encoder is released. Preserve the captured frame and three-photo editing; physical flicker acceptance remains pending.
- With the new Windows bridge, request Wi-Fi/BLE runtime diagnostics over existing authenticated channels, with source, time and stale markers. The Mac counterpart is unchanged.
- Insert a bounded UTF-8 prefix of oversized agenda speech while retaining existing text. Align ordinary feedback with the name field and separate recording actions; require explicit Save.
- Build and local validation evidence is in the [candidate record](TAB5-CAMERA-WIRELESS-250.md); this does not imply installation or physical acceptance.

## 0.2.249-ui - 2026-10-22 (installed, captured photo still flashes)

### Photo encoding

- Fix an eightfold oversized JPEG input cache sync caused by treating bit depth as bytes; synchronize within the caller buffer and reject invalid geometry/capacity before DMA.
- Preserve photo quality, three attachments, camera orientation, add-photo flow and binding settings without upgrading the SDK. The actual old driver fragment reproduces the defect; 39 input-range and 14 DMA allocation checks pass. Physical flicker causality remains unconfirmed; see [candidate evidence](TAB5-CAMERA-249.md).
- Exact ELF and ota_0 VALID boot verification passed. The user confirms flashes persist while the captured photo remains visible, so physical display acceptance failed.

## 0.2.248-ui - 2026-10-22 (installed, two flashes remain when adding photos)

### Add photo

- Keep the captured image through encoding and upload, then return directly to the composer on success without a transient full-screen attachment page. Failures retain the photo and manual retry; never repeat uploads automatically.
- Reuse the image and controls without another full-frame allocation; retain three-photo edits, orientation, bindings and settings. Four-way pending-ack/error/retry checks and nine native camera tests pass; see [candidate evidence](TAB5-CAMERA-248.md). The user reports two flashes remain, so physical display acceptance failed.
- Authorized image installed and verified against the exact ELF, ota_1 VALID and three live healthy samples; no duplicate installation issued.

## 0.2.247-ui - 2026-10-22 (installed, entire screen still flashes when adding)

### Add-photo transition

- Keep the captured image during encoding and restore normal page geometry before revealing attachments. Block duplicate actions and rotation during processing; retain error retry.
- No extra full-frame storage; retain three-photo editing, orientation and bindings. Ordering regression, four-pose checks, nine native camera targets and full UI pass; see [candidate evidence](TAB5-CAMERA-247.md).
- User completed installation; exact live boot verification passed for ota_0 VALID. Physical display acceptance failed: the entire screen still flashes when adding.

## 0.2.246-ui - 2026-10-22 (installed, repeated capture/removal passed; flicker when adding)

### Four-way camera and first open

- Place the shutter opposite the lens in all four holds, with the caption below the circle and matching touch coordinates. Correct both portrait viewfinder directions and save normalized portrait/landscape photos using the existing three frames.
- Wait up to four seconds for transient cold-start memory pressure to recover; keep cancellation responsive and retain the original safety thresholds and explicit failure path.
- Firmware build, nine camera targets, four-way UI/pixel checks and full UI regressions passed locally. Reboot-first-open, physical orientation and three-photo hardware acceptance remain pending. See [candidate evidence](TAB5-CAMERA-246.md).
- User completed the authorized upgrade through the normal bridge UI. Scoped live boot verification passed for the exact image, healthy UI samples and VALID OTA state; no duplicate installation was issued.
- Subsequent user feedback confirms repeated capture/removal works; the remaining reported issue is corruption/flicker when tapping Add Photo in both orientations. First-open and individual direction substeps were not separately reported, so acceptance is not broadened.

## 0.2.245-ui - 2026-10-22 (installed, camera direction acceptance failed)

### Camera direction and controls

- Correct the extra half-turn on the viewfinder and captured review when using inverted landscape or holding the device vertically. Keep the sensor-to-panel mapping fixed while control snapshots follow the UI and touch direction.
- Apply live direction changes after releasing display ownership; preserve capture, gesture handling and the reserved three frames. Match fallback/review pixel boundaries using small edge strips. Native direction, touch and memory regressions pass; real orientation and camera acceptance remain pending.
- Boot was verified, but hardware feedback rejects direction acceptance: both portrait holds remain upside down and the inverted-landscape shutter stays on the wrong side. The user also reports first-open 1/12 after reboot followed by a successful second open. These remain open for .246 verification; local fixtures did not prove physical orientation.

## 0.2.244-ui - 2026-10-22 (installed, three-photo memory test passed; direction issue open)

### Camera reopening

- Apply the cold initialization memory headroom check only before registering the sensor/video/ISP pipeline. A previously initialized camera uses its reserved frames and checks actual driver allocation results, avoiding false stage 1 / ENOMEM errors after successful photo uploads.
- Preserve first-initialization protection, driver failure cleanup, three full-resolution frames, JPEG quality and existing attachments. Eight native camera targets pass, including 128 captures/reopens at the observed internal heap values. The user confirmed real three-photo deletion/re-add, repeated reopen and gallery-return tests passed; upside-down viewfinder/control behavior is tracked separately.
- User-authorized installation and exact startup verification passed over paired Wi-Fi: matching ELF, ota_1 VALID and three healthy live UI reports. Cached .243 USB health is excluded from .244 acceptance.

## 0.2.243-ui - 2026-10-22 (installed, host/Direct tests passed; camera regression failed)

### Computer selection and names

- Group current-computer, Wi-Fi and service status clearly. Show a monitor/state icon for connecting, authenticated connection, offline and blocked switching; a saved selection alone cannot show success. Keep the selected target when unavailable, and follow authenticated USB takeover.
- Edit Chinese/English computer aliases on TAB5. Retain aliases through USB reconnection, preserve the original trusted-host/key/bond records, and restore the machine name by saving an empty alias. Save failure retains the old name and editor text.

### Wireless Codex Direct recovery

- Use the desktop-confirmed conversation without waiting for a delayed wireless catalog or the hidden task editor's background synchronization. Prevent old catalogs from clearing that target and pause new hidden read/draft jobs during the visit; retain in-flight results, local drafts, authentication and pending-send protection.
- Native ownership/persistence tests and a four-switch delayed-catalog/synchronization UI fixture pass locally. The exact candidate was installed with user authorization; startup verification confirmed the matching ELF, ota_0 VALID and fresh UI heartbeats. The user confirmed real connection/alias and Mac/Wi-Fi Direct tests passed. Three-photo regression reported camera stage 1 / ENOMEM 12, tracked separately from the earlier stage 6 frame-allocation failure.

## 0.2.242-ui - 2026-10-22 (installed, three-photo hardware test passed)

### Three-photo attachment lifecycle

- Reserve all three full-resolution camera frames at startup and retain them between sessions. Avoid allocating large frames among retained attachment thumbnails after reopening; preserve three-frame preview and JPEG quality.
- Cover three acknowledged uploads, middle/last/first deletion and re-addition, repeated camera reopening, a full draft rejecting a fourth attachment, and removal of all three. Preserve remaining photos and draft text. Local codec/network interfaces are simulated. The user confirmed the real three-photo deletion/re-add, repeated reopening and gallery-return run passed on the exact .242 image; boot verification confirmed ota_1 VALID.

## 0.2.241-ui - 2026-10-22 (installed, repeated-use acceptance failed)

### Repeated camera use

- Use the retained still as one of three preview frames, based on the pre-cleanup allocation failure captured on .240. Reduce simultaneous full-size frames from four to three while preserving resolution and JPEG quality.
- Stop capture before transferring the photographed frame to attachment encoding and freeing the other two. Three photos initially succeeded, but deleting one and reopening reproduced 6/12. A pre-cleanup trace found the second frame needed 1,843,200 bytes while the largest block was 1,835,008; .242 avoids repeated large allocations.

## 0.2.240-ui - 2026-10-22 (installed, repeated-use acceptance failed)

### Camera cache reclamation

- Detach and recycle hidden decoded artwork on the UI thread before opening the camera, then reclaim pending decode results after gallery workers stop. Share this handoff across application, composer and retake entries.
- Preserve drafts and photo attachments, and reload the same artwork/page when returning to the gallery. Keep resolution, quality and .238 computer channel ownership unchanged. Two photo attachments succeeded, but closing and reopening still reproduced 6/12; .241 addresses the measured contiguous-memory failure.

## 0.2.239-ui - 2026-10-22 (local diagnostic candidate, never flashed)

### Camera buffer diagnostics

- Record failed frame allocation parameters, free bytes and the largest contiguous block before pool cleanup to investigate camera 6/12. A fix is not yet confirmed. Preserve the .238 computer binding and channel logic.

## 0.2.238-ui - 2026-10-22 (local candidate)

### Bluetooth upgrade compatibility

- Send the standard Service Changed indication after startup so bonded computers can refresh an older GATT layout without deleting pairings. This candidate investigates the upgrade/cache failure; Bluetooth hardware acceptance is pending.

## 0.2.237-ui - 2026-10-22 (local candidate)

### One selected computer

- Keep USB, Wi-Fi and BLE assigned to the selected trusted computer. An authenticated USB connection from an already bound computer may take over; an unbound USB computer or another computer's wireless reconnect cannot change the target. Unplugging retains the selected computer.
- Authenticate BLE ownership before enabling control, remember peer identities separately, and filter connections to the selected computer without deleting pairings.
- Allow the selected computer's Wi-Fi unlock when an unrelated physical connection is present. Preserve the existing display preferences. Hardware acceptance is pending.

## 0.2.236-ui - 2026-10-21 (local candidate)

### Trusted computers

- Retain up to eight independent trusted computers after explicit first USB enrollment. Authenticated USB selects one owner; unplugging retains its wireless target. Other Windows/Mac bridges wait. Select a saved target in network diagnostics and see it on computer control.
- Bind takeover to a one-use device challenge and existing key; cancel/drain old input and invalidate old state/RPC/HID generations. Preserve pairing, credentials and BLE bonds; refuse another owner during an upgrade.

### Validation boundary

- Includes .235 camera and Windows password fixes. Windows, firmware and native regressions passed locally; Mac build and real device acceptance remain pending. The last Windows read-only preflight found no COM9; no firmware was written. See [integration record](TAB5-MULTI-HOST-20261021.md).

## 0.2.235-ui - 2026-10-21 (local candidate)

### Camera memory and Windows credentials

- Drain cancelled gallery workers, close their idle HTTP connection and reclaim detached spare decode storage before camera allocations. Preserve full resolution, three frame buffers, JPEG quality and existing attachments. A bounded, cancellable wait keeps the UI responsive.
- Extend paired Windows PIN/password input to 1–64 printable ASCII characters, preserving case and edge spaces. USB/BLE use per-character Shift and host Caps Lock state; Wi-Fi accepts printable keysyms. Existing encryption, one-shot submission, wiping and lock-state confirmation remain.
- Always open the manual keypad in numeric mode; switch to lowercase, uppercase or symbols as needed. Retain masking, space, backspace and clear. Remove the keyboard-layout remark from device and Windows settings. Login-method detection/switching and macOS changes are outside this candidate.

### Local validation

- ESP-IDF 5.4.2 build and image/checksum verification passed. Windows Release build, isolated credentials/RFB/encrypted-RPC tests, native UI and camera/photo/gallery regressions passed; actual notes are bound to the candidate image.
- This candidate has not been installed or flashed. `--status-once` was stopped by the existing profile-redirection guard. Real camera operation and USB/BLE/Wi-Fi Windows unlock acceptance remain pending.

## 0.2.234-ui - 2026-10-19

### BLE verification and display continuity

- Repair integer uptime in refreshed BLE boot reports. The original bridge now accepts three authenticated live samples for the exact ELF and VALID partition; health thresholds are unchanged. Retain bounded busy-report retries and correct cross-core heartbeat ordering.
- Retain recent trusted battery percentages across one transient conversion failure, retry brief read failures sooner and sample immediately at startup. Confirmed absence/staleness still clears the estimate; cold charging calibration is preserved. User observation confirmed quick display after reboot and normal USB-change display.
- Includes .229/.230 computer unlock, volume-drag recovery and bidirectional Codex read synchronization. Opening an actually displayed matching reply on TAB5 clears desktop unread state; prefetch does not.

### Validation and compatibility

- Original Windows 0.6.10 / c608 passed one isolated BLE check in 9.34 seconds with exact .234 ELF, ota_1 VALID and three live samples, with no isolation violations. Earlier passed physical cases were retained without repetition. PC WLAN was restored.
- Native worker/actual c608 wire replay, full native UI, all 962 corresponding-source entries, factory erase regions and licensed package hashes passed. Captures use isolated fixtures, with separate hardware evidence. Preserve pairing, PIN and display settings; keep first-install and upgrade formats distinct.
- Full read synchronization and unlock use Windows 0.6.10. Mac, ESP8266 and collections retain existing releases. See [release notes](../RELEASE-0.6.10.md), [BLE root cause](TAB5-BLE-UPTIME-20261019.md) and [battery acceptance](TAB5-BATTERY-DISPLAY-20261019.md).

## 0.2.230-ui - 2026-10-19

### Control recovery and read synchronization

- Continue read-only status polling during a volume drag and cancel abandoned gestures. An expired/disabled slider can recover without rebooting or replaying a command; stale status no longer claims the PC is ready.
- Acknowledge matching public replies after actual display on TAB5. The paired bridge rechecks device, account, host and the latest terminal turn, updates Codex through versioned desktop IPC and confirms its saved state. Prefetch, empty replies and old turns do not mark newer content read.
- Retain the exact .229 unlock implementation. This is a local candidate; physical acceptance and remaining release work are recorded in [the repair record](TAB5-CONTROL-READ-SYNC-20261019.md).

## 0.2.229-ui - 2026-10-19

### Computer unlock

- Replace fixed USB/Bluetooth preparation with consistent, fresh samples across requests. Unknown/changed states and disconnects reset observation; bounded wake settling, single-use authorization and no PIN replay remain.
- Retain transport labels and the latest eight diagnostic entries. Prevent ordinary control-state expiry from cancelling an in-progress unlock. Cancellation/disconnect reports incomplete without automatic retry.
- Existing hardware acceptance covers repeated USB/BLE, USB black-screen wake, cancellation and exact Wi-Fi boot verification. Regression with the new 0.6.10 bridge is tracked separately in the [closeout record](RELEASE-0610-229-CLOSEOUT-20261019.md).

## 0.2.217-ui - 2026-10-17

### Computer control and music

- Add PC volume, mute, lock and up to four launchers with application/folder icons. The enlarged volume value is centered above the mute button and becomes a slashed-speaker icon when muted.
- Show actual locked/unlocked state and prevent polling flicker. Music offers supported previous/play-pause/next actions while retaining idle-state and long-title behavior.
- Move large icon buffers to PSRAM, resolving the early 213 candidate's boot abort/rollback while preserving internal OTA staging memory.

### Networks and display

- Expand saved networks from five to ten, migrating existing credentials, labels and preferred selection while preserving an intentionally empty list. Updating existing entries remains possible at capacity.
- Add bounded gateway/PC TCP checks and transport errors after bridge connection failures, without changing settings or credentials and while pausing during OTA. Real wireless control recovered after a PC Wi-Fi reconnect; diagnostics alone are not a connectivity fix.
- Include post-210 automatic 180-degree landscape rotation, orientation lock and matching touch, refined two-column display/audio settings, countdown units and navigation changes.

### Validation and compatibility

- Installed image passed exact ELF and ota_0 VALID checks. User acceptance covers volume, mute, launchers, lock state, steady controls and Wi-Fi volume control with USB unplugged. Ten-slot storage/migration passed regression tests; existing real networks were retained without adding ten test credentials to the device.
- Full computer control and ten-network management require Windows 0.6.9. Mac retains 0.6.7 without these controls. Preserve TAB5 display preferences; do not apply the ESP8266 carousel reset. First-install and upgrade packages share application bytes but have different formats.
- Formal publication, package/installation checks and long-term stability have separate evidence. See the [release record](../RELEASE-0.6.9.md).

## 0.2.210-ui - 2026-10-17

### Agenda home

- Combine Today/All into a segmented filter, lower the toolbar and align it with the left timeline. Place New beside the list and emphasize the selected appointment's time and title on a separate detail surface.
- Preserve five complete rows, long titles, scrolling, selection and centered empty states. Add no instructional text; editing, reminders, synchronization and deletion are unchanged.
- Includes the changes since public 205: automatic day/night classic-clock and annual-dot screensavers, stable hour/minute positions while seconds update, simpler screensaver choices, centered agenda empty states and concise editing hints, restored internal-memory OTA staging, and automatic battery estimates after a connected-USB reboot.
- Native regressions and local hardware acceptance passed: agenda layout, 9.609-second OTA writing, retained internal memory and connected-USB reboot battery recovery. See the [210 record](TAB5-210-ACCEPTANCE-20261017.md).
- Preserve TAB5 display preferences during acceptance; automatic-mode/carousel restoration applies only to the small-screen device.

### Windows builds

- Generate Windows compiler response files from tracked CMake configuration, preserving long argument lists across fresh builds and regeneration without hand-editing Ninja files.

- Use up to eight physical build cores, with at most two performance cores, BelowNormal priority and a matching compiler/linker job pool. Preserve incremental outputs and restore the caller after failures.

## 0.2.209-ui - 2026-10-17

### Automatic battery estimate recovery

- When starting on charge without a trusted percentage, briefly pause charging to obtain a settled voltage estimate, then restore charging automatically. Keep established estimates across normal plug/unplug and transient read failures; do not substitute charging voltage for a known percentage.
- Verify charger-control writes, retry monitor initialization, restore charging after failed sampling, and bound each calibration attempt to 90 seconds before a five-minute retry interval. Native tests cover connected USB, zero battery current, read/write faults, restoration retries, unstable samples and timeouts. Physical acceptance is tracked separately.
- Retain 208's agenda empty state and internal OTA staging. The separate version-only 209 validation image was superseded before deployment; this candidate includes the battery fix.

## 0.2.208-ui - 2026-10-17

### Internal memory and OTA staging

- Move independent UI voice snapshots and text buffers to PSRAM; the linked candidate recovers 30,270 bytes of internal static memory after reserving a 4 KiB internal OTA staging buffer.
- Keep OTA writes in internal memory when larger allocations are unavailable. Add a 4 KiB storage benchmark using the existing internal readback buffer so low-memory devices can measure this path. Native low-memory and failure-path regressions pass; hardware results are recorded separately during acceptance.
- Acceptance limitation: the production benchmark entry remains hidden and the bridge formatter still expects the original 8 KiB first row. The benchmark has not run on hardware; validate the new writer with a full OTA instead.

### Agenda empty state

- Group the empty-state message and New button in the center. Hide irrelevant top filters when there are no items; retain a centered All action when other dates contain items.
- Restore the existing timeline toolbar when items become visible. Keep the concise wording and the verified 207 classic clock fix.

## 0.2.207-ui - 2026-10-17

### Classic clock stability

- Reserve a fixed seconds width from the widest value in the actual font so seconds updates no longer move the hours/minutes. Preserve the existing colors, grouping, day/night switching and minute-by-minute drift.
- Add all-60-second position and clipping regressions. Native, firmware and physical acceptance results are recorded separately in the local acceptance record; this is an unpublished candidate.

### Agenda home

- Place the timeline beside the selected item's details, show five complete rows, and separate New from the contextual Edit/Delete controls.
- Remove persistent capacity-cleanup and empty-list instruction text. Keep actionable save, sync and failure states; scheduling, retention and deletion behavior are unchanged.

## 0.2.206-ui - 2026-10-17

### Screensaver clock

- Center the classic clock's large hours/minutes and smaller seconds as a group, with the date and weekday beneath. Both classic and annual clocks use light colors from 07:00 to 19:00 device-local time and a black background otherwise; an unavailable clock defaults to night.
- Simplify annual clock choices, retain warm gray, and map legacy day/night styles to automatic switching without rewriting stored preferences. Preserve brightness, touch/PC wake and minute-by-minute drift.
- Native layout, transition and preference regressions passed. Firmware build and physical-device acceptance are tracked separately in the local acceptance record; this is an unpublished candidate.

## 0.2.205-ui - 2026-10-16

### Reminder layout

- Use a centered 640×300 agenda reminder, retaining the dark background, light-blue border and existing typography. Allow two title lines; size snooze and acknowledge controls by role without stretching a sole action.
- Preserve 204 expiry retention, full-list insertion eviction and offline deletion sync, 203 compact persistence/Wi-Fi recovery, and 201 bounded rendering, complete reading texts and existing font coverage. Companion bridge 0.6.7 is recommended.
- Native reminder previews, storage/interaction regressions and firmware builds are verified. 205 has not been flashed to the current device; 201/204 physical evidence is not complete 205 hardware acceptance. Publication does not flash devices.

## 0.2.204-ui - 2026-10-16

### Agenda retention

- Retain ended one-offs in All. Only an insertion into a full 16-item list reclaims the oldest-ended one-off; edits, reads and time passing do not prune. Keep recurring, future and undated entries.
- Commit replacement and deletion receipt atomically. Preserve all records on failure, require updated companion retention support, and acknowledge elapsed offline edits without resurrecting evicted entries. Keep 203 compact storage and Wi-Fi fixes. Local candidate; hardware acceptance pending.

## 0.2.203-ui - 2026-10-16

- Compact agenda storage to at most 3,574 bytes and repair pinned NVS SDK blob cleanup; add Wi-Fi station recovery. Boot, Wi-Fi connection, setting persistence, offline agenda reboot and deletion passed physical checks. The original automatic-expiry rule is superseded by 204.

## 0.2.202-ui - 2026-10-16

### Agenda synchronization and visibility

- Accept a complete six-range annual schedule when a combined holiday names all seven holidays. A calendar refresh failure retains the last-good calendar while independently confirming a valid item snapshot; unknown workday years still pause their reminders.
- Save locally first: create/edit/delete and reminders work offline; durable deletion metadata survives reboot and synchronizes after reconnect. Pending synchronization is separate from local save success; concurrent changes are preserved as copies. Add an Ended list with editing access and automatically select the retained item after saving a past event.
- Replace the agenda recording cancel cross with the selected trash icon, retaining its hit area and original-name preservation.
- Add a confirmed Delete action to TAB5, and place the companion Windows Delete Item action beside its list; preserve confirmation and edition checks. Shorten the Windows agenda label to “日程待办” and expand/align the birthday form. Keep existing user records and the accepted 201 rendering fixes. Local checks and installation acceptance are recorded separately.

## 0.2.201-ui - 2026-10-15

### Rendering stability

- Split eligible unscaled 90/180/270-degree LVGL layers into bounded strips. Preserve edge sampling with overlap and disjoint output clips, avoiding the reproduced large contiguous allocation without changing pixels.
- Both galleries pass eight rounds of four-direction artwork/detail/chapter/return checks with an explicit 700 KiB drawing budget. Local comparisons preserve 307 normal captures and 41 constrained captures exactly. This is a local candidate; the earlier physical portrait freeze is not a confirmed diagnosis or completed device acceptance.
- Preserve the 200 agenda, audio, status-capacity and recovery features. Companion USB cleanup retains disconnected-port recovery and exposes unrelated invariant errors.

## 0.2.200-ui - 2026-10-15

### Agenda editor

- Keep the microphone beside the name, with stationary stop controls during capture and keyboard input. Remove the device time switch: new items are timed, existing undated items are preserved.
- Show start and duration without an end row. Open custom timing on demand while retaining automatic expiry. Add one- and three-minute advance reminders.
- Add Chinese official workdays, including holidays and weekend makeup days, independently from Monday-Friday. Persist annual calendars separately; unknown years visibly pause reminders.

### Notifications and acceptance

- Use the selected synthesized cuckoo for monitoring; keep the Codex completion chime and add offline “Codex任务已完成” speech. Preserve mute, notification settings and capture priority. Windows does not duplicate completion popups; it supplies a fallback chime only without fresh TAB5 traffic, including silent small-display-only setups.
- Group the companion desktop upgrade channel with transfer results and keep status height bounded so actions and notes remain visible. This is a local candidate; flashing, listening and touch acceptance remain separate.

## 0.2.199-ui - 2026-10-15

### App restoration and capacity

- Build from the agenda-capable 197 working baseline in `tab5-178`, restoring the agenda entry, offline reminders and editing while retaining subsequent agenda/voice UI changes and existing apps.
- Apply the 64 KiB status ceiling, 48 KiB bridge normal target and automatic overflow recovery. Preserve pairing, agenda NVS, gallery and carousel settings.
- Local build, native UI and capacity-boundary checks pass. Record flashing and real touch/voice acceptance separately.

## 0.2.198-ui - 2026-10-15

### Connectivity and status capacity

- Installed and boot-verified locally, then retired because it was built from the wrong source directory and omitted agenda. Corrected by 199. Raise encrypted status packets to 64 KiB with a companion bridge normal target of 48 KiB; older bridges retain their 32 KiB packets.
- Update bounded USB, Wi-Fi, BLE, decryption, decompression and delta-tree limits together. Place larger receive buffers in PSRAM; keep independent RPC and voice limits.
- The companion bridge budgets task/read-state maps together, retains valid state during overflow and automatically retries full content without restarting or renewing cached sample timestamps.

## 0.2.197-ui - 2026-10-15

### Firmware size

- Local candidate, not flashed or published. Retain 196 agenda, gallery synchronization and runtime alerts, with the existing partitions and configuration storage.
- Keep every glyph, metric and advance; compact only the 24px character map. Compress small brand icons with the existing LVGL LZ4 decoder, preserving colors and alpha.
- Retain every offline Pinyin entry, weight, syllable boundary and candidate order. Use compact offsets and shared string suffixes without whole-dictionary decompression or a runtime dictionary cache.
- Compare font pixels, decoded icons and dictionary lookup against a frozen pre-compaction snapshot. Record builds/native regressions separately from physical acceptance.

## 0.2.196-ui - 2026-10-15

### Agenda and reminders

- Local acceptance candidate, not flashed or published. Add a start-first timeline, automatic ending at the configured end and no manual completion control. The list displays only start times.
- Create/edit names, start/end times, repeats and advance reminders locally. Open the month grid and hour/minute rollers on demand, use Today/Tomorrow and duration shortcuts, preserve duration when moving the start, and leave drafts unchanged on cancel.
- Keep an independent Chinese touch keyboard. Name dictation uses the TAB5 microphone and enabled Windows recognition bridge, returning text to an unsaved draft after explicit stop. Offline keyboard editing remains available; Windows 0.6.7 supplies bidirectional definition synchronization and conflict protection.

### Gallery and acceptance

- Include the 195 local gallery candidate's unchanged-JPEG metadata updates and complete-guide reuse within a work, retaining resume, full-text checks and failure recovery. Preserve the standalone 195 archive.
- Retain existing OTA partitions, glyph coverage and user data. Build/native preview results do not establish physical RTC, audio, voice or three-transport acceptance. Capacity, provenance and afternoon checks are recorded separately.

## 0.2.194-ui - 2026-10-14

- Give BLE-only bulk RPCs a separate 35-second inactivity lease, renewed only after a complete authenticated success. Failure, 600 ms idle or no progress restores Wi-Fi; late replies cannot revive expired leases. Original gallery and diagnostic deadlines remain unchanged.
- Installed and boot-verified in `ota_1`, `VALID`, with Overview as the first observed usable page. Pairing, both 366-work galleries and the original six-page order/15-second cycle were preserved. The user confirmed rotation resumed after leaving Settings.
- All six 256 KiB transfers completed with error 0: aggregate upload 19.50 KiB/s and download 18.86 KiB/s. The observed gains over one 192 run were about 8%/22%, with varying workloads. The 80.7-second pause restored without expiry; the last response recorded one successful exact confirmed replay.
- Native checks cover 64 successful ranges over two minutes and idle/failure/expiry/mode/recovery behavior. Whole-image BLE OTA, longer stability and the speed objective remain unaccepted. Physical multi-page artwork navigation is deferred by the user. See the [closeout record](TAB5-194-ACCEPTANCE-20261014.md) for identities and limits.

## 0.2.193-ui - 2026-10-14

- Start on Overview, hold the saved display interval, then resume saved fixed-page or cycle behavior. Manual navigation wins; page order and interval remain intact. Installed; the user confirmed the actual first screen.
- Introduce temporary Wi-Fi isolation for BLE-only bulk RPCs. The second physical download stopped at 96/256 KiB while the 35-second lease expired. This policy mismatch is repaired in 194; it does not establish overall acceleration or the sole cause of the earlier 192 OTA interruption.

## 0.2.192-ui - 2026-10-14

- Negotiate exact-prefix response replay: accept only byte-identical ciphertext for the same request/total; reject stale, altered, gapped or partially overlapping fragments. The bridge repairs an explicit ATT 0E once using confirmed writes; other errors propagate.
- Retain eight-packet notification bounds and backpressure. Installed and boot-verified; all six 256 KiB transfers completed at aggregate upload 18.115/download 15.433 KiB/s. Completeness passed; the speed objective remains open.

## 0.2.191-ui - 2026-10-14

- Render the final disconnected monitor state directly, fixing repeated left-card repainting after bridge exit while retaining details, alert deduplication and recovery. The user confirmed no flicker and automatic recovery.
- Preserve accepted calligraphy layouts; landscape paintings show title, author and period left of the artwork, with compact portrait information. Painting pack 2026.10.14.1 was imported and its layout/reading accepted. Multi-page navigation was deferred because both daily works had one page.
- Fix offline TF scheduling without a PC snapshot and JPEG decoder DMA descriptor allocation. Installed and boot-verified; both previews display correctly without synchronization prompts, and the original rotation works. Calligraphy re-import was unnecessary.
- BLE's second download still failed at this stage and was subsequently addressed by 192/194. Historical build/simulation evidence is distinct from physical results.

## 0.2.190-ui - 2026-10-13

### Runtime monitoring

- Add a monitor entry in System for desktop CPU/memory, commit and paging pressure, VPN/PAC routing, actual Codex connections and bridge watchdog health. Confirmed anomalies show brief causes and actions; absent data from older bridges remains unknown.
- Preserve the primary system statistics, hierarchical return and stale-data presentation. Monitoring coexists with desktop upgrade requests. Desktop/native regressions and TAB5 hardware acceptance are recorded separately.

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
