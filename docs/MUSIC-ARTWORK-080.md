# Universal artwork and transfer candidate 0.2.80

The deployed 0.2.78 bridge receives a 150×83 Windows thumbnail for the browser
video. Scaling that thumbnail cannot restore source detail. This candidate does
not bundle or hardcode any video ID or image.

- An optional browser companion automatically reads the current Media Session
  artwork on websites the user enables. YouTube uses the current live video ID
  to obtain its available high-resolution thumbnail; other sites use the largest
  declared artwork. Title and artist must match the Windows media session.
- Without the companion or useful source artwork, Windows artwork remains the
  fallback. Network failures retain the last successful matching cover. Browser
  history, cookies and credentials are not read. Pairing configuration is local
  and the extension is not installed without approval.
- All TAB5 covers use JPEG quality 88 on firmware 0.2.80 or newer, at an aspect-
  preserving size bounded by 560×336. Older firmware keeps 336-square RLE565;
  the ESP8266 retains its 112-square cover. JPEG source bytes never enter JSON
  music metadata, only bounded resource fragments.
- USB keeps its 16-fragment burst and yields the serial gate. When resources are
  pending the next burst waits 40 ms instead of 2000 ms. Only matching CRC IDs
  prove delivery; an old cover's ready bit cannot suppress retransmission.
- Wi-Fi/BLE use bounded resource-only frames, at most eight/four 1 KB chunks,
  after an authenticated full-state session. Wi-Fi temporarily polls at 250 ms
  while partial resources exist. Resource frames do not renew task freshness.
  Existing voice, image-upload and RPC priority stays in effect.
- Native rectangular images need no letterbox canvas. The surrounding cover card
  is transparent; source content is retained in full. The 0.2.79 early-boot crash
  record guard is included. It improves diagnosis, not a claim of crash repair.

Validation: Release build; direct-DLL status-once; artwork lifecycle, automatic
source/cache/failure/authentication tests; JS next-video/generic-site capture;
transport/receiver and native UI regressions; ESP-IDF build and ELF flash safety.
The native receiver uses a fake JPEG DMA driver and does not prove hardware JPEG
decode. The required dotnet-run status entry has the known Windows console-handle
failure; direct DLL invocation succeeds. Real browser installation, boot
verification, picture quality and three-channel timing remain pending.

## 中文

本候选针对所有封面的传输链路，不设置某一视频的替换规则。可选扩展从用户
启用网站的当前播放信息自动获取封面；YouTube 换视频后自动更新，其他网站
使用自身的 Media Session 封面。不支持或获取失败时继续保留 Windows 封面。

传输使用 JPEG 原生比例、CRC 确认、有界分片和忙闲分离的等待间隔。去掉
播放器封面周围的大块黑色卡片，保持完整图片。原照片、滑动、生日设置和
自动轮播继续保留。候选本地检查通过不等于实机完成；扩展是否安装需要用户
单独决定，清晰度和同步耗时等待真实浏览器及 TAB5 验收。

## 2026-10-02 installed result

USB confirms the .080 ELF and a valid OTA slot. The user approved and installed
the companion and enabled its website, but the bridge still reports 150×83 and
an authenticated local request returns 404. Clarity acceptance failed. Repeated
Assist Debug panics also occur on both Music and Overview; stability acceptance
failed. .081 prepares complete fault capture and paired bridge collection;
it is not an accepted crash fix. See CRASH-080.md.

用户已批准并安装扩展，但高清接口尚未接通；不能以安装完成代替清晰度验收。
目前优先定位跨页面反复崩溃，按用户要求保留功能、不回退。

## Desktop integration recovered on 2026-10-02 at 12:37

Following normal desktop restart with the verified .081 bridge, existing local browser pairing matches without a key change and the authenticated endpoint returns HTTP 200. The same browser video's source is now 1280x720. The bridge reports an acknowledged 560x315 JPEG, 65066 bytes, CRC 34c74541, in 3031 ms. This proves the universal capture/download/matching/transfer chain, without a per-video override; visual quality and complete change-to-cover latency remain pending. Separate .081 fault records identify the recurring idle blue screens as NimBLE task stack exhaustion; see CRASH-080.md for the .082 repair.
