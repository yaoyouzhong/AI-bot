# Historical development notes before 0.6.11

These are dated intermediate observations, not current release status. Final scope and acceptance are in [0.6.11](../RELEASE-0.6.11.md). Retained pending statements describe the time of each entry.

## Unreleased ESP8266 reset-credit text - 2026-10-27

- Strengthen the existing 14px reset-credit strokes while preserving badge size, alignment and blank character gaps. Production Font2 raster tests pass; physical appearance awaits installation.


## Unreleased Windows voice focus fix - 2026-10-27

- Prevent background quota window positioning and transitions from interactive mode from stealing input focus. Background display stays non-activating while interactive windows retain their minimum resize constraints.
- Add bounded voice focus recovery and audio pausing, and avoid another stop command when Doubao has already stopped. Preserve interrupted text for review without sending it. Add background-window focus, audio drain and recognizer stop regressions.
- Follow-up empty-result fix: do not treat native idle as a final empty result when sound was received. Keep the full text-delivery wait and selected microphone; a timeout retains the desktop draft with a diagnostic error. Add content-free audio duration, text-update counts and recognition-state diagnostics. Intermittent takes with no text still require physical investigation.
- Detect when Doubao ends capture before the device requests stop; stop forwarding audio immediately and preserve the partial text with an incomplete-result warning instead of continuing to announce readiness. This protection does not resolve the underlying early-stop cause; continuous dictation still needs acceptance.
- The first candidate still failed on the second physical TAB5 recording. Builds and native-window regressions do not replace hardware acceptance of the new fix. Mac Doubao also depends on focus, but its background refresh has no matching topmost operation; Mac and firmware are unchanged. See the [investigation record](WINDOWS-VOICE-FOCUS-20261027.md).


## Unreleased Windows/Mac alignment - 2026-10-26

- Sum DeepSeek recharge and bonus wallets without double-counting authoritative totals; store and display API/web sources independently to prevent cross-account balance/spending combinations. Add per-provider web zoom.
- Speed up USB/Wi-Fi preview for compatible TAB5 firmware, bound OTA command expiry to the actual status frame, protect terminal voice replies, normalize only newly recognized speech, and bound Codex history fallback paging and responses.
- Omit desktop runtime diagnostics and TAB5 forecast details from ESP8266 LAN status, preserving current weather, stock labels and graph samples. The smaller payload restores Wi-Fi fallback on the tested ESP8266; two real fallback/USB recovery checks passed. Add local-only, content-free LAN request counters.
- The Windows candidate is installed and normal Explorer startup is verified; tool-context startup still triggers profile protection. Build and synthetic regressions pass. The installed CLI does not implement item pagination, but the existing local-history route passes a 25.2MB synthetic rollout check. Fix the stale device-center expectation for the existing Computer Control card. Real DeepSeek login/zoom and source-separated device data were checked; TAB5 history/voice and remaining transport acceptance are pending. No firmware flash or public release. See the [alignment record](WINDOWS-MAC-ALIGNMENT-20261026.md).


## Unreleased TAB5 source archive - 2026-10-27

- Archive the complete accepted 0.2.279-ui project, exact configurations, six managed-dependency overrides and SDK/NimBLE patches with licenses and byte hashes. Add a cross-platform archive verifier and CI check; preserve original source bytes and omit firmware images, private state and unmodified downloaded dependencies. No rebuild, flashing or release.


## Unreleased Mac model account parity - 2026-10-26

- Render each stock-name atlas row independently to prevent cross-row rasterization differences on macOS 14 while retaining the accepted font, color, size and alignment. Update paginated-history fixtures, isolate test registration defaults and bound asynchronous menu checks for CI.

- Keep DeepSeek API total balance (already including gifts) and console spending independently stored, timestamped and labeled. In separate-source mode devices receive API balance only; unverified web spending is never merged. Pure-console mode can display its own balance and spending together. Sum same-currency recharge and bonus wallets with decimal arithmetic, without double-counting an authoritative total.
- Read console spending from `total_costs`; same-console balance-only updates retain the original spending timestamp for at most 24 hours. Source/credential/currency transitions prevent old API data being relabeled as web data. Failed reads retain the last successful source-specific data.
- Match Windows stock-name rendering parameters: 21-pixel regular type, 20-pixel rows, right alignment, gray text and end ellipsis for long names. Remove extra vertical padding; stock codes already share the firmware's 16-pixel Font2. The local ESP8266 0.5.6 candidate preserves reset-credit digit strokes and aligns columns inside a smaller badge.
- Replace model account dialogs with one window containing the 14 Windows provider entries, an embedded official login page and quota-source settings. Retain API/local-service sources and add DeepSeek official web balance parsing.
- Let system WebKit storage manage web sessions; selected console sources retry in the background after the window closes and after bridge restart. Expired login, rate limiting and network failures preserve the last successful data with freshness shown. Never export cookies or combine account balances with local token counts.
- Fit ESP8266 weather place names to actual glyph bounds and enlarge Chinese stock names within existing row heights. A separate local ESP8266 0.5.4 candidate fixes Codex reset-credit borders and pet-animation exclusion. TAB5 firmware is unchanged.
- Follow-up feedback: match Windows’ colored air-quality badge and separate condition text, and add per-provider web zoom for oversized official dialogs. The local ESP8266 0.5.5 candidate reduces reset-credit text from 16 to 14 pixels at the user’s request.


## Unreleased Mac ESP8266 discovery - 2026-10-26

- Add authenticated ESP8266 LAN rediscovery using the existing Windows/device protocol and retained Keychain pairing. Share the TAB5 UDP listener, select the current matching subnet, and bind the reply source to the advertised address only when HTTP is ready. Preserve USB-first routing, pairing and display settings. Native policy/loopback checks are recorded separately from controlled stale-port ESP8266 rediscovery, Wi-Fi fallback and USB recovery hardware evidence; actual DHCP changes remain untested. No new firmware or public release.

- Match Windows’ 12-second completion display timeout so ESP cycling can resume. Correct imported pet and localized-text bitmap row orientation, including stock names in the 20-row atlas. Import the project’s six-frame Codex mascot at the user’s request; visual and text hardware acceptance remain separate.
- Fix the API quota providers’ console button opening no page and losing its feedback: open DeepSeek, StepFun and Baidu Qianfan official sites in the default browser while preserving quota sources and credentials.


## Unreleased Mac TAB5 candidate - 2026-10-20

- Complete the five-stage Mac candidate scope: shared USB/Wi-Fi/BLE authentication and receipts, daily display/settings, covers/pets/gallery, exact-image OTA/boot checks, computer/music controls, opt-in Codex replies/encrypted drafts/one-time delivery, bound photos, local Speech capture/review, and verified 16 MiB P4 first install/backup/restore.
- Add module Mac tests/Release, ad-hoc candidate-app/tool checks, fixture-only native UI rendering, checksums and an offline bilingual guide. Fix native XCTest report collection, Swift compatibility, Keychain-helper visibility and Codex pipe buffering from actual CI failures. Hardware, Automation/playback, Accessibility composer compatibility and on-device speech availability await physical acceptance. No merge to main, deployment, tag or publication. See the [candidate guide](../MAC-TAB5-CANDIDATE.md) and [evidence](MAC-TAB5-PORTING.md).

## Unreleased · Windows sign-in method

- Windows source candidate adds safe stop during TAB5 upgrade preparation/transfer. Last-range release disables stop; a fresh same-host idle report confirms stopped transfers. Existing firmware cleanup and pairing are retained. Not installed; hardware cancellation is pending.

- TAB5 .254 local candidate replaces camera hardware JPEG with bounded scanline software encoding, retaining full resolution, quality 85, YUV420 and three-photo editing. The quota heading drops the carry-over explanation and the daily values lose their asterisks, without changing calculations. .253 is installed: both BLE attempts passed, while blue flashes persist; .254 is now installed with exact boot verification, but the user still reports blue flashes and a slow failed first BLE upload; its retry and Wi-Fi succeed. Windows and Mac binaries are unchanged in this follow-up.

- New local candidate: preserve observed positive quota differences across changed future reset dates, label uncertain/cross-day attribution, and collect explicit banked-reset history read-only. Scheduled and observed early resets keep prior increments and start a new segment; zero-usage deadline drift is not another reset. Replay of the untouched actual history yields October 21 = 30 points, including 26 carried across a gap, and one observed October 23 01:10 reset. Automatic-reset origin is not established by sampling alone. Installed; resident totals match the replay. Live history reports complete with no normalized redemptions, which does not establish absence of card use; raw event kinds remain unverified. Mac is unchanged.

- TAB5 .253 candidate removes the two redundant photo hints, transfers completed preview ownership to the attachment cache, and shortens JPEG DMA bursts. It also permits bounded credential-mailbox admission without replay. Installed; both user BLE attempts passed and blue flashes still fail.

- Installed candidate: daily quota totals continue the previous valid reading across midnight, segment verified resets, and label long-gap carry-over separately. Actual October 21 still reports 4 points and remains under investigation. Runtime diagnostics retry the other live wireless path to the same bound device after an initial timeout; forced-BLE hardware acceptance is pending.

- Add on-demand Wi-Fi/BLE runtime diagnostics for TAB5 .250 using the bound computer's encrypted channels, retaining stale samples with explicit age. Add daily quota sample bounds to diagnose partial history separately from complete totals. Installed; live Wi-Fi diagnostics passed with USB unplugged. BLE-only fallback is a follow-up; Mac is unchanged.

- Query the TAB5 GATT service directly and report separate discovery/ownership/identity stages. A firmware candidate follows the .237 trial to notify existing bonds of service-layout changes; Bluetooth hardware acceptance is pending.

- TAB5 0.2.237-ui candidate: pin USB, Wi-Fi and BLE to one selected trusted computer. Only authenticated USB from an already trusted computer may take over automatically; unavailable links and other computers' wireless reconnects never change the target. Verify and filter BLE peer ownership, preserve pairings, and keep unrelated physical links from blocking the selected computer's Wi-Fi unlock. Windows proves its BLE ownership before USB standby. Hardware acceptance is pending.

- Store an explicit PIN/password type for one-click unlock and invoke a protected, on-demand Windows helper to select the matching input page. Stop without replay when the method, focus, session or request freshness cannot be confirmed. Install prerequisites by default and save/check USB/Bluetooth and Wi-Fi from one page. Preserve legacy credentials; macOS and firmware are unchanged. Five real method switches and USB unlocking from both the wallpaper and fingerprint page passed. Transport acceptance is recorded in the development report.


## Unreleased

- Add a macOS TAB5 USB/Wi-Fi, daily-display and acknowledged display-settings candidate, isolated from ESP8266 settings. Independent protocol/reference checks passed on Windows; 55 Swift tests and Release compilation passed in Mac CI. Preserve command failures through log pipelines and collect the XCTest XML using an absolute path. Native UI and hardware acceptance remain pending. No deployment or release; see the [phase-one record](MAC-TAB5-PHASE1-20261019.md).
- Widen the default device center and adapt its action grid between three and two columns, preserving device names and shortening descriptions. Recompute stale scroll extents after resizing so the connection/upgrade entry remains fully reachable. Windows UI only; firmware and device settings are unchanged.

### TAB5 local candidate

- Add authenticated selection among independently trusted Windows/Mac computers with TAB5 .236. Preserve existing pairing and credential bindings, and stop stale input during takeover.
- Extend Windows unlock credentials to 1–64 printable ASCII characters and retain the default numeric keypad. Pair with .235/.236 camera memory handoff fixes. Local validation passed; Mac build and hardware acceptance remain pending. See [integration record](TAB5-MULTI-HOST-20261021.md).

