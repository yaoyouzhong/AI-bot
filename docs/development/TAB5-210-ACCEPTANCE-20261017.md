> 本地收尾完成：210 已刷入，精确启动核验通过（ota_1 / VALID）；写入 9.609 秒，日程操作后内部空闲 75,087 字节、最大连续块 32,768 字节。日程布局和插电重启电量均获用户确认正常。未推送、未打标签、未正式发布。

# TAB5 210 local acceptance — 2026-10-17

User authorized local build, OTA, agenda/memory/write-speed/reboot-battery acceptance and delivery preparation. No push, tag or public release. Windows remains the previously installed and accepted 0.6.8; its original packages are retained byte-for-byte.

## Candidate identity

- Firmware: 0.2.210-ui, 7,071,728 bytes.
- Application SHA-256: `21e733f4c9f5e0940c4d3a7d5debac29e60051ad7c4bdb6466b48d32c47f4335`.
- ELF SHA-256: `2e5b54d1b4d7748629915a91531e380797f48b4aab3719a2ecb29af691bdca97`.
- Source commit: `dc61c081d409f18978e8496f8915b747901e71e9` (isolated TAB5 source, clean at packaging).
- Canonical OTA path: `artifacts/firmware/tab5/latest/aibot_tab5.bin`.
- Archive: `versions/0.2.210-ui/21e733f4c9f5-9ad0c182`; 209 remains archived.

## Build and native validation

- Full firmware build passed through `scripts/build.ps1`, with CMake-generated response files; no generated Ninja repair after reconfiguration.
- Long command regression compiled real C/C++ commands of 59,366/59,372 characters, both with a fresh configuration and after regeneration. Macros, flags and headers in spaced paths remain intact. The prior failing firmware command measured 32,996 characters.
- Eight physical cores (two performance, six efficiency on this host), shared eight-job compiler/linker pool, BelowNormal priority. Ten resource-policy tests passed, including inherited masks, child/grandchild inheritance, IDF failure propagation and restoration. All 1,567 firmware C/C++ targets use the generated pool.
- Subsequent unchanged-source build passed in 11.322 seconds without C/C++ recompilation. This is a warm incremental measurement, not a guaranteed time for changed sources.
- Known SDK/BSP warnings remain, including optional ROM GDB metadata, deprecated touch APIs and unused BSP declarations; no build error was bypassed.
- Approved agenda UI and pointer-test hashes still match the accepted preview. Native agenda tests cover five rows, long titles, scrolling, selection, empty states, filters, New/cancel and retained editor/reminder flows.
- Actual sidecar passed `OTA_ACTUAL_SIDECAR_PREVIEW_OK`; full UI suite passed `LVGL_PREVIEW_OK`. Both top/scrolled notes PNGs were visually inspected. Simulated version/battery in those images are fixture data.
- Internal static B/D bytes: 209 = 171,306; 210 = 171,310; delta +4 bytes. The earlier 30,270-byte memory recovery remains. Runtime heap is recorded separately.

## Local delivery

- First-install, upgrade and source archives passed package validation, ZIP integrity and SHA-256 checks. First-install application bytes equal the OTA image; erased factory regions contain no device NVS backup.
- Source snapshot includes the permanent Windows response-file policy, regression and eight-core resource policy. A separate clean byte-reproducible rebuild was not performed.
- Windows setup, portable and frozen source packages are unchanged. Each component includes LICENSE, THIRD_PARTY_NOTICES and checksums.
- Local delivery directory: `artifacts/delivery-068-210-20261017`.

## Hardware

- Exact startup verification passed: 0.2.210-ui, matching ELF, `ota_1`, `VALID`, previous stage 9, full 7,071,728-byte offset, error 0; OTA stack free 3,856 bytes.
- Actual 209 writer phases: prepare 8.233 s; write **9.609 s**; verify 0.919 s; install 19.654 s. Compressed host transfer: 4,840,808 bytes in 8.734 s. These are separate, partially overlapping counters.
- Previous write into 209 was 9.619 s; the new write remains within the user's historical 10-second range. The slow 207 writer had taken 103.948 s into 208.
- At uptime 61.620 s, page 1: internal free **75,175 bytes**, largest block **32,768 bytes**. Camera diagnostic positions 2/3. The preflash 209 sample was 69,835/28,672 bytes on another page with the saver active; page/cache allocations vary, so runtime deltas are not treated as static savings.
- Display errors 0, LCD underruns 0, crash endpoint `[]`. Exact records: ota-verify.json, verified-diagnostics.json and hardware-metrics.json.
- The local CLI start was rejected because the offered-device update was already in progress; no duplicate write was sent. Exact postboot identity and retained OTA phases establish the delivered image.
- Physical agenda acceptance: user replied “正常” to the requested layout, Today/All, selection/scroll where available and New/cancel check. No data creation/deletion was requested. Native coverage establishes the five-row and long-title cases; no physical row count was separately recorded.
- Connected-USB reboot battery acceptance: user replied “正常” to the explicit check that the number recovered after the 210 reboot while USB stayed connected, without unplug recovery. No numeric percentage was supplied; charging current/CHG_EN are not exported, so separate physical charging-current verification is not claimed.

TAB5 display preferences are preserved. Automatic-mode/carousel restoration applies only to ESP8266 and was not run for TAB5.

- After the user confirmed agenda operation, uptime 260.353 s: internal free 75,087 bytes, largest block 32,768 bytes; display errors and underruns 0. See post-agenda-confirmed-diagnostics.json and post-agenda-metrics.json.
- Final result: all requested 210 local build/deployment/agenda/memory/write-speed/reboot-battery checks passed. Delivery hashes and archive integrity passed. No push, tag or public release.
