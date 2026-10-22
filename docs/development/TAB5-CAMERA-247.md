# TAB5 .247 添加照片切换候选

用户确认 .246 反复拍照、删除已正常，剩余现象是在点击“添加照片”时横竖屏均花屏/闪屏。未读取照片内容。

代码确认旧路径在启动 JPEG 编码后立即隐藏拍后画面并展开整个附件页；竖屏还会在后续时钟回调才恢复普通页面方向。新回归断言在原 .246 上失败，证明提前切页。尚无对应故障时刻的 LCD 欠载记录，因此不将此视为唯一硬件根因已确认，也未据此修改显示驱动。

## 改动

- 编码期间保留当前拍后画面，原添加按钮显示“处理中…”，阻止重复操作及方向变更。
- 编码完成或失败后，等当前触摸结束，在同一 LVGL 回调内恢复普通页面方向后显示附件页。
- 编码失败重试留在原错误页，避免重新旋转相机后再次编码。
- 不增加整帧缓存，不改变相机预留三帧、JPEG、上传协议、电脑绑定或设置。

## 候选与验证

- 版本：`0.2.247-ui`；SHA-256：`20da13bb4ab1971565d5bd0b0b8415a009c249e5ef99d79dfbd1258d3ecb6d65`。
- 固件构建、四向切换专项和九项相机原生回归通过。专项包含保留画面/指针、阻止重复点击、错误重试和恢复页面尺寸；编码及网络由夹具模拟。
- 专项合成横竖屏预览已目视检查；完整界面回归通过（含三张增删、连接切换及无线直达），实际更新说明已目视核对。
- 增量源码补丁：[TAB5-CAMERA-247.patch](TAB5-CAMERA-247.patch)，基于 .246；回放后三个文件内容一致（CRLF/LF 归一化）。完整现场源码及证据见 `artifacts/development/tab5-photo-add-247-20261022/` 与固定开发源码目录。
- 用户完成已授权升级；Wi-Fi 启动核验通过，精确 ELF 一致、`ota_0 VALID`、三次实时界面采样通过。初次核验尚未确认，随后核验通过，不从版本号单独推断成功。压缩传输 4,903,435 字节/14,016 ms。
- 真机显示验收未通过：用户先称局部闪屏，复看后纠正为整个画面闪屏。等待新鲜 USB 显示诊断，旧 .243 通用缓存不用于判断。未提交、推送或发布。

验收只需横竖各拍一张并点“添加照片”，观察处理、回附件两段是否花屏；再保留三张附件，删一张补拍一次。不发送会话，不删除既有附件作为排障前提。

## English

The .246 user reports repeat capture/removal working, with corruption/flicker specifically when adding a captured photo in either orientation. Keep the captured image visible during encoding, block duplicate controls/rotation, and restore normal page geometry before revealing the attachment page in one LVGL callback. Retry remains on its error page. No extra full-frame storage or protocol/settings change. The new ordering regression fails on .246 and passes on this candidate; four-pose UI and nine native camera targets pass. Full UI regression passed, including three-photo editing, host selection and wireless Direct; matching actual notes were visually inspected. The user completed the authorized upgrade; subsequent exact live boot verification passed over Wi-Fi for ota_0 VALID. Hardware display acceptance failed: the user clarified that the entire screen flashes. Fresh USB diagnostics are pending; stale .243 samples are excluded. A unique LCD-level root cause has not been established.
