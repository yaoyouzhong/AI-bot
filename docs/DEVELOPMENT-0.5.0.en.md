# 0.5.0 development history / 开发历史

Historical intermediate results; use the release checklist for current status.
历史中间结果，以发布验收记录为当前状态依据。

## Unreleased — Windows Device Center

- Deploy and boot-verify TAB5 0.2.89-ui with its matching bridge. Complete settings layout and value feedback, camera resource recovery, voice cursor insertion and disconnected-result recovery, attachment receipt queries, selective configuration restore, pointer visibility and firmware-note formatting. Use a fixed local firmware path with versioned archives. Individual hardware acceptance and restored display defaults are recorded in [the current checklist](ACCEPTANCE-2026-10-02.md); USB upgrade interruption investigation is paused and untested environments remain open.

- Prepare TAB5 .083 after isolating the BLE timeout to the identity read. Bound the ATT identity value to 512 bytes: representative/maximal production replies shrink from 553/744 to 451/492 bytes with authentication fields and existing 8/32 window behavior preserved. Link icons now distinguish a physical connection from fresh authenticated data. Firmware build, full UI preview, protocol regression and ELF guards pass; the candidate is not installed yet. See [record](BLUETOOTH-083.md).

- Installed .081 captures three repeated `nimble_host` stack-protection faults: only 32–92 bytes remain before a 128-byte interrupt context. Installed .082 reduces the identity callback's actual stack frame from 2032 to 336 bytes and removes printf from nonce/proof hex conversion. Build, production wire-equivalence/maximum-JSON tests and actual ELF budgets pass; USB confirms the exact repair ELF and VALID slot. Active BLE connection and stability acceptance remain pending because service reads time out. The corrected installer safely resumes with the original backup and confirms all 26 bridge hashes. Normal desktop startup also restores the universal browser artwork path: authenticated HTTP 200 and a live 1280×720 source, with device visual acceptance pending. See [record](CRASH-080.md).

- Install the universal music-artwork .080 candidate: per-site browser Media Session integration, aspect-preserving JPEG covers, bounded resource-only radio transfers, shorter pending USB waits, and a transparent cover background. There is no per-video override. USB confirms the exact installed image and valid OTA slot; the user installed the extension, but clarity and stability acceptance failed. See [record](MUSIC-ARTWORK-080.md).

- Installed the birthday editor adjustment: direct Gregorian/lunar selection, explicit new/add/update actions and repeated-apply protection. Local build, isolated edit/cancel tests and layout checks pass; the user confirmed the interaction works. See [record](BIRTHDAY-UI.md).

- Installed the TAB5 .078 landscape-cover layout and paired bridge, retaining the existing asset size and square-cover behavior. The user confirms a larger image but still sees blur: the browser source remains 150×83. Clarity is unresolved pending the exact video page URL; USB boot verification remains pending. See [record](MUSIC-ARTWORK-078.md).

- Investigate an idle blue-screen reset on TAB5 .076: reset reason 7 confirms a watchdog reset, but the retained record has no panic address or stack. Root cause remains open. .077 checkpoints survive the .078 reboot; prepare .079 to prevent early boot Flash operations from overwriting the previous Flash checkpoint. The regression fails before and passes after the guard; the candidate is not installed and is not a crash fix. See [record](CRASH-077.md).

- Installed and boot-verified TAB5 0.2.76-ui: fit each home viewport in one partial buffer for contiguous snapshot copies, and keep valid cached snapshots at snap-animation endpoints. The user reports a substantial improvement. Ten initial cached home-frame samples have a 24.3 ms median versus 31.4 ms in .075; these are sparse rendering samples, not whole-gesture FPS. Full UI/image/ELF checks pass. Extended observation found one LCD underrun and later an idle watchdog reset, so stability remains under investigation. Original automatic cycling was restored. See [record](PERFORMANCE-076.md).

- TAB5 0.2.75-ui prevents frequent visible-page updates from starving the first cached image of other home pages. The production LVGL reproduction fails before the fix and passes after it; 500 swipe pixel cases and the full UI preview pass. Hardware boot verification passes; the user reports improvement with remaining unevenness, prompting .076. Bridge 074c is unchanged. See [record](PERFORMANCE-075.md).

- Installed TAB5 0.2.74-ui with bridge 074c. Keep BLE bulk priority through attachment handling and the final response; the 118509-byte photo completed in 2.748 s (user: 2.7 s), retaining 1280×720 / JPEG 85. All 18 USB/Wi-Fi/BLE hardware rounds passed; the earlier USB third-round interruption did not recur. Original automatic cycling and pairing are preserved. This is one accepted photo sample; live voice was not retested, and the prescribed dotnet run status check still has a host console-handle failure. See [acceptance record](TRANSPORT-PERFORMANCE-074.md).

- Verify the .071 OTA receiver with .072: device receive/write time fell from 38.381 s to 25.979 s; boot version, ELF hash and VALID partition matched. A 160364-byte BLE photo still took 4.545 s. Prepare .073 with complete notification buffers and negotiated 32-packet RPC windows, keeping voice at 8 and removing the Windows throughput-preference experiment. Local checks pass; .073 hardware acceptance is pending. See [record](TRANSPORT-PERFORMANCE-073.md).

- Install TAB5 0.2.71 with optional 64 KiB internal flash staging and a 96 KiB reserve, plus a negotiated 7.5 ms BLE bulk interval request. Withdraw early BLE window refill after hardware transfer failed; the corrected bridge restores the original window ordering, adds notification timing and bounds status deferral at three seconds. All 18 corrected hardware rounds passed, with BLE upload/download medians of 29.51/27.22 KiB/s. Prepare an opt-in Windows throughput comparison and a version-only 0.2.72 OTA image; the photo target and actual OTA savings remain unverified. See [record](TRANSPORT-PERFORMANCE-071.md).

- Prepare a second voice follow-up after the first patch failed hardware acceptance: verified Doubao 0.9.1.22 control, completion-based empty-result handling, preserved errors for late audio and a visible draft on focus failure. Synthetic audio through the real desktop recognizer passed; the bridge has been backed up and replaced in the original directory, normal startup and USB-only no-speech/spoken hardware acceptance passed. No firmware update is needed. See [voice record](VOICE-COMPLETION-070.md).

- Installed the paired 0.2.70 firmware and bridge; boot verification and 18 benchmark rounds passed. Negotiated 48 KiB bulk blocks, per-window BLE scheduling, bounded blocking USB confirmations and compressed state improved measured median throughput in all six directions. The USB voice entry and exclusive photo feedback are included; hardware photo and feedback checks passed, and USB-only no-speech plus short spoken follow-up checks also passed. See [comparison results](TRANSPORT-PERFORMANCE-070.md).

- Version 0.2.68 adds raw-byte RPC paths for photos/OTA, TCP_NODELAY on new HTTP TCP connections and completion-driven BLE voice waits. Installed bridge/firmware passed boot verification and all 18 equal-size hardware RAM rounds. USB and Wi-Fi throughput improved; BLE upload is close to an earlier .067 sample, so baseline-dependent gains are not guaranteed. BLE-only photo/short voice checks passed, without a higher effective photo rate this time. Transient USB reconnections and remaining real workloads still need acceptance. See [performance record](TRANSPORT-PERFORMANCE-068.md).

- Withdraw firmware 0.2.65 after a hardware stack-protection reset and rollback. The matching 0.2.66 repair removes recursive JSON printing from BLE delta validation and separates identity/data callbacks, reducing the receive callback's compiled local frame from 1680 to 224 bytes. Bridge files are unchanged; local regression and ELF checks pass, hardware acceptance remains pending.

- Prepare the matching TAB5 0.2.65-ui transport candidate: acknowledged BLE state deltas and bounded windows, USB binary RPC and voice, Wi-Fi binary image uploads and persistent RPC, BLE OTA, and RAM-only transport benchmarks. Local protocol, crypto, fault and camera-memory regressions pass; hardware throughput and deployment remain pending. See [integration record](TRANSPORT-INTEGRATED-2026-09-30.md).

- Remove the repeated transport name from both device headers. Show ESP8266 Wi-Fi as standby while USB is active and LAN polling is idle; standby does not assert wireless fallback readiness.

- Use concise functional descriptions for TAB5 and ESP8266 cards, including the missing Favorite Tasks subtitle, and check that subtitles fit at minimum window sizes. Keep the Add Device limit dialog compact with a fixed Close action and check its default/minimum bounds at 100/125/150/200 percent scaling.

- Bound full-state deferral during continuous BLE reply reads to four seconds so metrics cannot indefinitely starve task/session freshness. Report paired-device authentication failures explicitly. Regression tests cover sustained read traffic and reconnect ordering; live acceptance is tracked in [the current checklist](ACCEPTANCE-2026-09-30.md).

- Distinguish sampling pauses from incomplete daily quota totals. Same-account cumulative readings recover same-day pauses and flat midnight intervals; verified scheduled/manual resets start new segments whose usage is added, including the first post-reset reading, without making the day partial. Keep ambiguous overnight growth, account changes and unexplained decreases partial. Original history is preserved.

- Send a full TAB5 state first after every authenticated BLE reconnect, including during reply paging, so a rebooted device never receives metrics before its session baseline. Reconnect ordering tests pass. With firmware 0.2.55-ui, device-reboot reconnection and BLE-only overview/reply reading passed user hardware acceptance on 2026-09-30; long-duration stress testing was not performed.

- Add per-device reconnect, session connection history and allowlisted diagnostic export; authenticated TAB5 display readback/save; an update center with P4 image and USB boot validation; frequent tasks and companion-firmware reading bookmarks; selective configuration backup/restore; and notification switches, quiet hours and session history. Matching TAB5 firmware candidate: 0.2.53-ui. These changes are not deployed; see [scope and validation](DEVICE-DEVELOPMENT-2026-09-29.md).

- Withdraw TAB5 firmware candidates 0.2.40/0.2.41 after hardware rollback exposed insufficient receiver stack capacity for compressed-state decoding. Candidate 0.2.42 uses a heap inflater workspace; the bridge stops sending compressed state to affected versions and adds BLE capability version 2. Full hardware acceptance remains required, and the intermittent USB transfer failure remains unresolved.

- Serialize complete TAB5 BLE status, voice and conversation RPC exchanges and retain the latest failure stage. Limit firmware downloads to USB / Wi-Fi while keeping BLE data and conversation features. Format release notes with a heading, bullets, aligned wrapping and scrolling. The 0.2.41 candidate shows received KB from the first download fragments; hardware acceptance is recorded separately.

- Recover from an IOException while disposing an unplugged TAB5 serial port instead of losing the reconnect worker or failing app shutdown. Add 250 ms system-metrics frames, compressed full state and legacy fallback for the 0.2.40 candidate; reduce idle BLE mailbox contention. Actual BLE cadence and firmware behavior still require hardware acceptance.

- Add authenticated USB / BLE RPC for the TAB5 0.2.39 candidate: firmware ranges, history, drafts, send receipts and chunked image uploads, preserving send deduplication and image validation. Older firmware needs an initial USB or Wi-Fi upgrade. Builds and simulations pass; hardware acceptance remains pending.

- Remove device-name hover tooltips from the tray menu so they cannot cover open submenus.
- Fix Codex completion notices holding the task page after the visible notice ends. Limit the interruption to 12 seconds; explicitly selecting Automatic Cycling in the preview or Display Settings dismisses the existing completion notice while preserving pages, order and interval. Pending input requests still show their interruption reason.
- Dismiss the preview when focus moves outside it, while keeping its page menu usable. Retain explicit close, Escape and tray toggling. Add press feedback, a blue checked running state and started/restarted confirmation to Automatic Cycling, without reporting failed actions as successful.
- Normalize escaped line breaks in TAB5 firmware update notes.

- Redesign the preview popup with a unified light surface, single-row page navigation, an on-demand page menu, cycle progress and a slim brightness slider. Replace the radio button that ignored clicks when already selected with an explicit Automatic Cycling action that saves and restarts cycling on every click. Show the actual cycle position, countdown or task-alert interruption. Regression coverage includes repeated clicks, menu selection, previous/next navigation and keyboard brightness adjustment.

- Combine small-screen brightness, display mode and cycling in Display Settings without Apply buttons. Coalesce rapid slider changes and send the final pending value when closing. Read actual brightness without writing settings on opening; report send failures.
- Keep the preview page menu synchronized with checked cycle pages and their order, with Automatic Cycling resuming cycling and a fixed popup height regardless of page count. Remove the duplicate preview card. Order small-screen cards as display, appearance, data and connection; move upgrades into Manage and the gallery into Appearance. Order TAB5 cards as voice, calendar and birthdays, data, and connection and updates.

- Add per-screen Automatic, USB-only and Wi-Fi-only settings for ESP8266, defaulting to Automatic and applying without restarting device services or affecting TAB5. Wi-Fi only suppresses USB status, metrics and resources while retaining pairing/diagnostics. USB only blocks legacy LAN data and discovery. Connection status follows actual data activity; invalid HTTP routes no longer count as Wi-Fi heartbeats.

- Use the same two-column action cards in My Devices, Accounts and Data, and Bridge Settings; shorten the Chinese Weather and Location label.
- Rename Display Content / Data Selection to Data Settings. Explain the relationship between data categories and domestic model providers, show Chinese and English names with quota types, and disable provider choices without losing selections when model quotas are off.
- Keep Add responsive at the two-device limit, showing registered models, the one-per-model limit and replacement steps.

- Show device features after registration. A tray left-click opens the preview when an ESP8266 is enabled, otherwise Device Center. The first version supports one TAB5 and one ESP8266.
- Add a versioned registry, existing-pairing migration, explicit legacy-screen import, independent disable/remove operations and shared data-demand merging. Removal preserves settings, pairing recovery files and user content.
- Separate TAB5 data demand from ESP8266 display pages. Unregistered hardware does not start its USB/BLE/voice services; disabling one device does not restart the other.
- Bind device windows to a fixed target and block removal during recording, updates or installation. Legacy flashing/pairing entry points honor registration. Replacing retained TAB5 pairing requires confirmation in the add wizard and preserves an encrypted backup.

- Offer Device Center, screen preview, common settings grouped by registered device, and Exit in the tray menu. Keep shared accounts and data settings in Device Center. Preserve custom names, gray out disabled devices and bind each shortcut to its device. Right-click preview always opens it; left-click still toggles it. Keep full management and diagnostics in Device Center. Use the default name M5Stack TAB5 and remove duplicate model text from the device list.
- Use four-character Chinese labels for Accounts and Data and common Device Center actions.
- Put the small-screen brightness slider first in Display Settings and read the actual device brightness on opening; keep brightness controls in the preview popup.
- Fix a collapsed brightness-slider container under scaling. Connection settings show actual USB status without a COM input; align appearance controls by weather, shared pet, Claude and Codex.
- Arrange common Device Center actions in two columns, display full default device names and compact connection status; move small-screen information, diagnostics and network reset into Manage.
- Center settings windows in the current screen work area and avoid using a closed Device Center as the owner of birthday, voice or update dialogs, preventing disposed-object errors.
- Remove manual refresh and the quota-collection toggle. Collect quotas and record Codex history while the bridge runs; show service status as connection/data tables and move About into Bridge Settings.
- Show only the enable toggle and preferred microphone in voice settings by default. Move legacy shortcuts, recognition tests and audio checks into Troubleshooting while preserving microphone restoration.
- Recover changed TAB5 USB instance identifiers only after probing and matching the paired device ID. Preserve the pairing key and reject unrelated devices.
- Fix ESP8266 USB disconnections when shared weather included TAB5 hourly/daily forecasts and exceeded the 6144-byte heartbeat limit. Preserve current weather on the small screen and full TAB5 forecasts; expose the failed connection stage.

Windows builds, isolated migration, simulated service/protocol tests, installer fault tests and layout scaling checks passed. The September 29 revision restored TAB5 USB/BLE acknowledgements and confirmed ESP8266 bridge-online state, increasing USB counters and rendered cycle pages. No hardware flashing performed; full three-transport switching, three hardware combinations and actual cross-monitor DPI acceptance remain pending. See the [validation notes](DEVICE-CENTER-VALIDATION.zh.md) (Chinese).
