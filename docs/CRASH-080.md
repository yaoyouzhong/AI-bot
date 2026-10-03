# TAB5 .080 repeated panic, .081 capture and .082 stack repair

## Root cause captured on .081

At 12:39:06, 12:42:42 and 12:45:40 on 2026-10-02, the new bridge
automatically saved three .081 panic records. All identify `nimble_host`,
Assist Debug cause 27 and the original guard PC `0x4ff00268`: the first
instruction of `_interrupt_handler`, `addi sp,sp,-128`. Saved task SPs are
84, 92 and 32 bytes above their respective task-stack lower bounds. The
4096-byte NimBLE task does not have room for the 128-byte interrupt context.
The first two saved task PCs are in newlib `_svfprintf_r`; the third is in
ROM with return address `__ssprint_r`. The earlier PC `0x4ff002c2` is where
the already-pending guard interrupt is delivered after switching to the ISR
stack. It was not the original fault location.

The frozen .081 ELF shows a 2032-byte local frame in `telemetry_info_access`
and 1152 bytes in `_svfprintf_r`, in addition to the NimBLE callers. The
identity read callback has large JSON/proof/diagnostic buffers on the stack.
Hexadecimal nonce and HMAC formatting also used repeated `sprintf` calls.
These background reads explain why the failure occurs on both Overview and
Music while the user is idle. This evidence identifies the recurring stack
protection panics; it does not retroactively prove the separate .076 watchdog
reset had the same cause.

Raw records are in `artifacts/development/crash-081-20261002/live-crash-*.json`;
the .082 bundle retains the first record as `root-cause-081.json`. The exact
.081 ELF SHA-256 is
`dfe791e3ab7d7d53ee8e1b9d0bcc8f244b81c7b4c69d5976b929d09a20ace6fb`.

## .082 repair and validation

The identity callback uses a static workspace owned exclusively by the
serialized NimBLE host queue, reducing its actual compiled frame from 2032
to 336 bytes. No heap allocation or extra mutex is added to GATT reads.
Nonce and proof hexadecimal conversion uses fixed byte lookup rather than
printf, preserving the authenticated wire bytes. Identity replies reject a
formatting failure or truncation instead of appending partial JSON.
Hardware stack protection, watchdogs and the 4096-byte task stack remain
enabled and unchanged. The display, swipe, cover, transport and birthday
behavior is retained; no protocol or companion bridge change is needed.

ESP-IDF build and image checksum/hash validation pass. The actual ELF stack
budget rejects the old 2032-byte callback and accepts the new 336-byte callback;
the receiver frames remain 224/96/128 bytes within their existing budgets.
The production callback host regression compares old and new replies byte
for byte with maximum numeric values and a maximum firmware identifier. The
744-byte JSON parses; repeated reads, all 256 byte hex values, output boundaries,
nonce/proof sizes, unauthenticated/non-read requests and mbuf failures pass.
Display ISR IRAM inspection also passes. The candidate is frozen in
`artifacts/development/crash-082-20261002/candidate-082` with its manifest.

The user completed installation on 2026-10-02. USB boot verification confirms
`0.2.82-ui`, `ota_0`, `VALID`, and the exact candidate ELF SHA-256
`d8bdcf1716dd0535eab949b1ec2c0eb69ee3feb93bdd48a307d6e2f21371bf67`.
The completed image has 6641968 bytes and no previous OTA error. A retry during
the .081 crash left a stale busy message while the original upload completed;
the verified .082 image did not require another upload or a rollback.

The original automatic cycle remains enabled with the same six pages and
15-second interval. A 603.8-second observation beginning at 12:57:16 has no new
.082 panic, uptime regression or diagnostic read failure, but Windows repeatedly
times out while reading the BLE services and never confirms an authenticated
BLE data frame during that window.
An active authenticated standby BLE connection and bounded observation of that
path remain pending. USB startup and absence of a new panic while BLE is
unavailable do not establish full crash-repair acceptance. Evidence is retained
in the .082 bundle's boot summary and hardware-observation report.

Subsequent service reconnect and full bridge restart did not recover BLE.
An isolated probe narrowed the timeout to the identity characteristic read;
the identity exceeded ATT's 512-byte limit. The .083 bounded-identity and
link-indicator candidate is documented in [BLUETOOTH-083.md](BLUETOOTH-083.md).

## Earlier .080 evidence

On 2026-10-02 the user reports repeated idle blue screens and automatic resets,
first on Music and then on Overview. Direct USB reads at 11:36:59, 11:45:22 and
12:01:31 confirm firmware 0.2.80-ui, ota_0 / VALID / stateError=0, and the exact
frozen ELF fingerprint c45a67f5de979d9667c89cc7293a4090298db252c785114b17bb239bbc0e0318.
The latest retained UI/cache checkpoints identify page 0; the earlier ones page 5.
This is not limited to the Music page.

Each retained panic has core=0, exception=4, cause=27, MEPC=0x4ff002c2,
RA=0x4ff002b4, SP=0x4ff1ad00, and no code candidates. In the exact ESP-IDF
5.4.2 build, interrupt 27 is Assist Debug; cache error is interrupt 25.
The PC maps to vectors.S after nested interrupts are re-enabled. SP equals
core 0's interrupt-stack top (base 0x4ff1a700 + 1536 bytes). This does not
establish an ISR stack overflow or a bad JPEG. The original triggering guard
PC/status and the interrupted task were not retained in .080.

The previous .076 watchdog reset (reason 7, no panic frame) remains a separate
unresolved observation; the new panic cannot retroactively explain it.
Raw reads are in artifacts/development/crash-080-20261002/retained-current.jsonl.
Earlier bridge health text was stale (10:56:52); direct USB evidence is used here.

## .081 capture candidate

Retain the existing display, swipe, transport, photo and music behavior. Add:

- Both cores' Assist Debug raw status, original trigger PC and guard bounds.
- Current fixed task identifier and its stack bounds, interrupt depth and ISR bounds.
- Saved interrupted task PC/RA/SP and saved s1 as diagnostic candidates.
- Automatic bridge collection after a panic boot, retaining the last eight
  records in RAM at the loopback-only /diagnostics/tab5-crashes endpoint. Failed
  reads are attempted at most three times per boot; browser origins cannot read it.

Panic capture reads only bounded internal SRAM and MMIO, calls no RTOS API,
allocates no heap and performs no Flash/USB work. The regular IDF panic path
remains unchanged. IDF asserts the StaticTask_t stack-field offsets against the
RISC-V assembly offsets. External or unaligned task/frame pointers are skipped.
There is no watchdog/stack-guard disabling or speculative stack enlargement.

The diagnostic record version is advanced; earlier .080 evidence has already
been exported before any upgrade. Diagnostic USB buffers remain static and grow
to 3072/3200 bytes. Maximum-width production formatter output is 1769 bytes and
parses as JSON in the host regression.

Local ESP-IDF build, actual ELF IRAM inspection, complete native UI preview,
production panic/RTC regressions with mocked registers, Windows Release build,
and actual local HTTP isolation/multi-boot retention tests pass. The user installed
.081; its automatic hardware records above provided the original fault location.
.081 was a diagnostic image rather than a repair.

The user declined a rollback. The prepared .074 recovery script is disabled;
no downgrade was offered or installed.

## Browser cover status

Earlier .080 integration failed: the installed companion's loopback request
returned 404 and the source remained 150x83. Following the normal desktop
restart at 12:37, the installer confirms the existing desktop key matches the
installed extension without modifying it; authenticated HTTP returns 200.
The same live browser video's source becomes 1280x720. The bridge reports a
560x315 JPEG (65066 bytes), CRC `34c74541`, initially acknowledged in 3031 ms;
the later .082 live sample reports 953 ms for the same cover. The generic
capture/download/matching chain now works; visual clarity and end-to-end change
latency still require the user's device observation. No per-video override or
browser credential access is introduced.

The failed diagnostic installer had treated the HTTP listener closing as proof
the whole process had exited. The corrected installer waits for tracked process
exit and exclusive EXE/DLL/PDB handles, reuses a verified original .080 backup,
and rolls back only modified bytes while preserving the original error. Real
lock/process/partial-write/retry regressions pass. All 26 installed bridge hashes
match the frozen .081 candidate; the original cycle is confirmed restored.

No per-video source override is introduced. The current desktop birthday settings,
TAB5 pairing and original automatic-cycle page order/interval are preserved.
