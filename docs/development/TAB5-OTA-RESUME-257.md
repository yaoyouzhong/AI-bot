# TAB5 OTA retry and resume candidate

Status: installed and physically tested on 2026-10-23. Approved Windows DLL f5206a7a4a44 and exact .257/.258 images were deployed. USB, BLE and Wi-Fi each recovered from a physical disconnection without another start command, then passed full-image and exact boot checks. Final device is .258 / ota_1 VALID and the user restored automatic mode. Same-boot limits remain. Wi-Fi performance regression and photo blue flashing are unresolved.

## Behavior / 行为

USB, Wi-Fi and BLE retain the current installer, flash handle, completed blocks and SHA state while retrying a transient read failure. USB/BLE repeat the same immutable range under a new nonce after reconnect. Wi-Fi normally retains its fast compressed stream; after I/O loss it discards only the incomplete block and resumes through authenticated range RPC. No full-image restart or second partition erase occurs within that recovery.

三通道的短暂断线均在同一次升级内自动恢复；明确选择的通道保持不变，自动模式也不在一份镜像传输中跳到另一电脑。部分压缩块不是断点，只重读当前未完成块。已完成块和写入进度保持；校验失败不切启动分区。

This is same-boot recovery. Power loss, reboot, a different offer/image or an exhausted recovery budget ends the installer; those cases start a new installation. There is no persistent NVS/download journal and no database migration.

## Bounds / 边界

- Each gap permits at most eight recovery attempts, with 250/500/1000/2000/4000 ms backoff (then capped).
- No new retry begins once the gap has lasted 90 seconds or the installer has lasted 15 minutes; an in-flight RPC retains its own bounded deadline. At most 32 recovery attempts per installer, including the Wi-Fi stream-to-range transition.
- A physically absent channel waits within the same time budget, without spending all range attempts before reconnection. The production task test covers a 30-second absence on every route.
- A successful verified range resets only the consecutive gap counter, never the total recovery count.
- Authentication, invalid range/encoding, corrupt/decryption failures, offer withdrawal, mode change and selected-host epoch change are terminal.
- A new signed offer or withdrawal is compared with the latest accepted offer publication time, so an older cross-channel status cannot cancel a newer installation.
- Stop withdraws the offer. No new range is issued after the firmware observes withdrawal; final-byte exclusion and a fresh post-stop idle confirmation remain.
- The current boot partition remains selected on failure. Complete SHA-256, ESP image checks, project/version identity and startup verification remain mandatory.
- 配对、电脑绑定、凭据、照片及草稿内容不写入续传日志。

## Protocol / 协议

Windows advertises optional exact numeric data.rpcOtaResume=1 inside authenticated state. New firmware uses Wi-Fi /tab5/v1/rpc kind=ota fallback only when that selected host capability is present. Requests preserve offerId, sha256, offset, count, session and issuedAt, using a fresh nonce plus existing HMAC/AES-GCM rules. No nonce or ciphertext is replayed.

Windows accepts OTA ranges over the same authenticated Wi-Fi RPC endpoint and applies the same offset/offer and Stop rules used for USB/BLE. Wi-Fi supports the existing independent zlib range envelope. A different hash/offer is rejected. Legacy HTTP receivers are unchanged; an older Mac bridge omitting the capability retains its old HTTP behavior. No Mac source was changed. Mac Wi-Fi resume needs an explicit capability and endpoint adoption before claiming support there.

## Evidence / 证据

Source baseline: tab5-source/artifacts/development/ota-resume-257/BASELINE.json and baseline/.
Native production range and full installer: native-tests-6.log, including four routes (USB, BLE, raw Wi-Fi, compressed Wi-Fi), actual failed-block offsets, transient/reconnect-generation recovery, bounded exhaustion, explicit rejection, corrupt ranges, Stop and selected-host change. The same assertions were retained; cancellation injection was moved after the first range request, and exact one-call/no-further-call assertions were added. The first proposed expectation change was rejected by automatic review and never executed.

Windows candidate build: artifacts/development/tab5-ota-resume-257-build.log, zero warnings/errors. Remote/Stop test: tab5-ota-resume-257-remote-test.log, including real LAN HTTP requests, repeated immutable ranges, wrong SHA and stopped-offer rejection, fresh idle confirmation and final-byte protection. Compression test: tab5-ota-resume-257-compression-test.log, source .256 image, 159 independent roundtrip vectors plus complete USB/BLE/Wi-Fi authenticated range comparisons. These are local tests and not real-radio interruption results.

## Hardware acceptance / 真机待验收

1. Under BLE-only selection, interrupt the selected Windows BLE link briefly during mid-file delivery; reconnect the same host and confirm that the next accepted range starts at the failed offset, then complete a valid boot.
2. Repeat under Wi-Fi and USB selection. Maintain device power during a USB unplug test. Wi-Fi must switch only its read protocol, retaining the same computer and channel.
3. Stop during a retry wait. Confirm a fresh same-host idle state on the original firmware, no automatic retry/rearm, and no new bytes after Stop.
4. Reject a changed hash/offer and verify that no boot partition changes.
5. Record original mode and restore the user's chosen mode after testing. Do not restore ESP8266 display/cycle defaults on TAB5.

## Photo display investigation / 蓝闪

Exact installed .256: 8b63889b63502eb50e655ea757a20fee2000ff7e2a43f696bbb0486bdc732b25, ELF 25540b571cd687a3ba8320502c66ba63ce9d18b3eeedd573e10c99b40b5ea688, ota_1 VALID. User reports blue flash persists; photo delete/add passed. Snapshot confirms Wi-Fi image upload status 200, bytes 103448, 442 ms and lcdUnderruns=0. A zero counter is not display acceptance.

The still-hold caller starts encoding only after a successful still_begin return. The .256 entry/release no longer directly selects/copies/cache-syncs LCD buffers. That change did not eliminate the visible issue.

External primary reproducer https://github.com/jtenniswood/espcontrol/issues/2118 describes similar static-page blue flashes with clean underrun counters; different board/panel/chip revision. It is a lead, not a TAB5 root-cause finding. Its changes are not copied into this project. No new SDK/cache/clock setting is changed based on that report.

## 2026-10-23 physical acceptance / 真机验收

- 新桥接 33 个运行文件经哈希核验后部署，旧运行文件保留备份；正常桌面启动沿用原 Roaming 资料，pairMatchesDisk=True。
- 初装 .257：SHA-256 dc6aa81326afaad0f9731c6a5da4a225f281427ffb89f70339ba288b1034a60e，ELF cb8a08500288102e9f2ae68a52d765628e50daf7bdfe390d860399183fe7745d，ota_0 / VALID。
- USB：仅 USB 下 .257 -> .258，用户实际拔插并保持电池供电；传输停在 3,047,424 字节，随后完成 7,179,456 字节，未重复开始。精确 .258 ELF 145b0dd9006a82f5dff587f694d6283fcd217129930ee9fd48c437a5a0a764ad、ota_1 / VALID。
- BLE：仅蓝牙下 .258 -> 同源码 .257，用户关闭再开启 Windows 蓝牙。约 933,888 字节后出现不可达和服务发现失败，随后继续至整包；设备运行时间连续，精确 .257 启动通过。重连成功，但服务发现等待仍偏长；USB 仅诊断，固件明确经 BLE 提供。
- Wi-Fi：仅 Wi-Fi 下 .257 -> .258，用户关闭再开启 Windows Wi-Fi。约 2 MB 压缩流中断后，经同一镜像的鉴权 Wi-Fi 分块继续，后续已提供字节 4,374,528 -> 5,505,024 -> 6,586,368 -> 7,179,456；未再次开始、未换电脑或固件通道。精确 .258 / ota_1 / VALID。
- 最终用户确认恢复自动选择，保留绑定、配对、设置和草稿；未读取照片内容。没有断电/跨重启续传结论。本轮未在恢复等待中触发 Stop；不能把已完成后禁用的按钮当作停止通过。

All three physical interruption cases passed with one scoped start per installer. The two approved same-source versions alternated to respect the existing same-version guard; it was never bypassed. Final canonical and running image remain the approved .258 (08ebec5599c43d0e5e810c942850a46dee328403c3330a4435abf9b8fcd3937a). No Mac edit, commit, push, tag or publication occurred.

### Performance remains open / 性能仍待修复

The user's observation is confirmed in all three Wi-Fi stages. A historical .254 receiver installed .255 with transfer/write/verify 10,391/10,197/1,329 ms. The .256 receiver installed .257 without a network fault in 18,844/44,051/6,407 ms. The final interrupted Wi-Fi test on .257 reports actual write 47,973 ms and verify 6,725 ms; its 86,520 ms installation includes a deliberate disconnection and must not be used as a no-fault transfer baseline. By contrast, current USB and BLE actual writes are 9,945 and 9,967 ms, with verify 936 and 948 ms. The historical and current figures come from physical device phase records, not UI guesses. This narrows the regression to the newer receiver's Wi-Fi path; it does not prove a specific source change. Retry success does not establish performance acceptance. Photo blue flashes also remain unresolved.

Evidence (ignored local artifacts): artifacts/development/tab5-ota-resume-257/PHYSICAL-ACCEPTANCE.md, physical-acceptance-state.json, resume258-usb.jsonl, resume257-ble.jsonl, resume258-wifi.jsonl, live258-wifi-verified.json, ota-performance-comparison.json. The old candidate-stage records remain historical snapshots.