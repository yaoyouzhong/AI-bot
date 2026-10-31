# TAB5 大屏预览内存修复 / Preview memory fix

2026-10-29；Mac R34 / TAB5 0.2.292-ui 现场，修复候选 0.2.293-ui。候选已按用户授权安装，尚未公开发布。

## 现场与原因

- 桥接预览显示 `640×360 · USB · 标准画质`，打开后超过两分钟仍未恢复高清。
- 原始像素分配失败（阶段 1）的累计样本由 2 增至 5，最新作业 196；请求由 26 增至 203，无超时、拒绝、显示错误或 underrun。相机空闲，未进行 OTA。
- 图库保留两张已解码的大图，可能同时占用相机可借出的两块预留帧；第三块保留拍照图像。原预览只能取得已经分离的图库缓冲区或空闲相机帧，无法回收 UI 仍持有但未显示的缓存，继而申请连续的 `1280×720×2` 字节。分配失败触发桥接标清回退，定时重试不能解决持续持有。
- 这是可复现的缓冲区所有权缺口。现场没有可用的最大连续堆块测量，不将其描述为已证实的内存泄漏或碎片化。

## 修改

`screen_preview.c` 在分配新大块内存前有界获取显示锁，向 UI 借用未显示的图库缓存；在锁内清除对应 LVGL 缓存和描述符，转移一次所有权。排除当前图像、隐藏但仍被引用的图像、保留图像及过渡图像；过渡期间不借用。继续沿用相机缓冲区的归还路径，不直接释放预留池内存。

图库需要继续解码且没有可重用缓存时，等待活动预览结束，然后回收预览空闲缓冲区。现有协议、画面分辨率、字体、分区和用户展示设置不变。Windows 与 Mac 共用此固件修复，无需新的桥接版本。

补丁：[tab5-preview-gallery-cache.patch](../proposals/tab5-preview-gallery-cache.patch)。父来源：[tab5-quota-module-default.json](../proposals/tab5-quota-module-default.json)。精确源文件／镜像／ELF 哈希：[来源记录](../proposals/tab5-preview-gallery-cache.json)。基线 7,550 个文件逐项匹配已冻结 .292 清单；补丁只改四个生产文件及两个测试文件。

## 已完成验证

- P4 Release 构建；板级配置定义无变化。镜像 7,208,672 字节，应用分区 7,208,960 字节，余量 288 字节。没有为腾空间删字库或改变分区。
- 真实 LVGL 回归验证只借一次、显示／隐藏引用及过渡保护、当前像素不变、前台解码等待和重用。完整原生 UI 回归通过。
- 实际预览工作器、编解码器、相机池和图库缓冲区代码的 82 项低内存／取消／恢复检查通过；RTOS、捕获、传输和内存限制是测试夹具。受限堆加两个被占用的池帧时，原 .292 复现阶段 1 失败，修复版完成高清帧并归还借用，保留另一帧和照片。
- 正式 Windows 包校验器通过，准备脚本已生成固定路径 `artifacts/firmware/tab5/latest/aibot_tab5.bin`；实际 sidecar 的首屏和滚动预览已检查。
- 本地证据：`artifacts/mac-voice-source-20261029/preview-memory-recheck/VALIDATION.json`、`artifacts/mac-voice-source-20261029/tab5-293-preview/`。不将运行状态、设备凭据、个人画面或二进制提交到仓库。

## 实机状态与剩余验收

用户已明确授权安装 .293。桥接 USB 启动核验通过：精确 ELF、VALID 分区及三个实时界面样本一致；原 R34 桥接的预览首帧恢复为 `1280×720 · USB`。升级前后只读核对亮度、音量、提醒、固定页面、轮播开关／列表／顺序／间隔及屏保等待时间，完全一致；未改动展示设置。

用户随后要求图库切换验收留到 10 月 30 日，因此没有把首帧成功扩大为持续运行通过。仍需在图库缓存占用场景检查持续高清、关闭再开、图库继续解码、当前画面和照片保留，以及没有新增原始像素分配失败。这些仍为发布前验证门槛；R35 排队确认仍按用户要求留到 10 月 30 日。源码合并、模拟回归、实机验收及公开发布分别记录。

## English

Installed .292 repeatedly falls back to 640×360 over USB with raw-pixel allocation failures. Two UI gallery caches can hold both loanable reserved camera frames while the third preserves a photo. Preview cannot access an unused UI-owned cache and attempts another contiguous 1.76 MiB allocation. This reproducible ownership gap is not evidence of a measured leak or fragmentation.

Candidate .293 transfers only an unused cache under the display lock, protects displayed/hidden references and transitions, and lets foreground decoding reclaim idle preview scratch. Protocol, fonts, partitions and display preferences remain unchanged. The P4 build, real LVGL/full native UI regression, 82 actual-worker cases with synthetic runtime fixtures, production package validator and actual sidecar previews pass. The image fits with 288 bytes remaining. Authorized installation now passes exact ELF, VALID partition and three live samples over USB, and the unchanged R34 bridge displays a 1280×720 first frame. Display preferences match before/after. The user deferred gallery switching and sustained HD acceptance to October 30; source merge does not constitute device acceptance or a public release. Windows uses the same firmware fix without a bridge protocol change.


## 2026-10-30 实机续验 / Hardware follow-up

.293 经用户切换两张书法作品，预览保持 1280×720；连续一分钟采样仍为高清，关闭重新打开恢复高清，用户再次切换作品并确认正常。USB 实时诊断：55 次请求、22 个完整帧、0 超时、0 拒绝，显示错误及欠载均为 0；预览工作器累计完成 250 个任务，最近错误阶段为 0。未采集历史全部失败次数，不将最近错误为 0 扩大为历史无失败。照片保留与 R35 排队确认仍待验收。

本次发现同版本升级提示误报：TAB5 当前 .293、电脑提供 .293 时仍写“有新固件可用”。同版本重装能力保留后，界面没有区分同版本提供。新增 [修复补丁](../proposals/tab5-ota-same-version-label.patch) 与 [来源](../proposals/tab5-ota-same-version-label.json)，.294 本地候选已构建，待授权安装。

The user switched calligraphy works, confirmed another switch after reopening preview, and reported normal rendering. Samples over one minute remained 1280×720; reopening also returned HD. Live diagnostics show 55 requests, 22 complete frames, zero timeouts/rejections/display errors/underruns, and last preview-worker error 0 after 250 completed jobs. This does not establish a historical zero-failure count. Photo retention and R35 queue acceptance remain pending. A same-version update-label defect was found and fixed in the .294 candidate, awaiting authorized installation.


.294 后续实机：已授权安装，USB 精确 ELF／VALID 分区／三个实时样本通过。用户确认当前版本提示、重新安装按钮及隐藏绿色升级标记三项均正确。照片保留与 R35 排队验收继续待测。

.294 hardware follow-up: authorized installation passes exact ELF, VALID partition and three live samples over USB. The user confirms all three same-version UI corrections. Photo retention and R35 queue acceptance remain pending.


.296 已安装并通过精确启动核验；用户确认优化后的确认弹窗布局和取消正常。

.296 installation passes exact boot verification; user confirms the revised dialog layout and cancel behavior.

## 2026-10-30 照片保留补验 / Photo-retention acceptance

R40 + .296，用户拍摄一张测试照片并确认已在 Codex 草稿中显示缩略图；保留照片后打开设备预览、切换两张图库作品。电脑窗口确认 `1280 × 720 · USB`。关闭预览后重新打开，用户返回原草稿并打开照片，确认“照片完整正常，预览已重开”；电脑再次核对高清状态。照片保留及本次图库／预览关闭重开组合通过，随后关闭电脑预览窗口。没有发送照片、刷机或改动展示设置，不据此推断未采样的历史错误计数。

On R40/.296, the user retained a test-photo thumbnail in the Codex draft, opened device preview and switched two gallery works. The desktop viewer confirmed 1280×720 over USB. After closing and reopening preview, the user reopened the original draft photo and confirmed it remained complete and normal; HD status was independently checked again. This passes photo retention through the exercised gallery/preview sequence. The viewer was closed afterward. No photo submission, flash or display-setting change occurred, and unobserved historical error counters are not inferred.
