# TAB5 封面同步循环／100%：全链路内存预留

## 现场证据

用户在 .299 时钟验收通过后报告音乐封面停在“封面同步中 100%”，已暂停播放；保持原曲切到总览再返回音乐，仍未恢复。桥接 R47 后台一直为同一资源代数、334×336 JPEG、45,998 字节、第一轮确认、35 个 USB 分块，未触发重试上限或 RGB565 回退。

现场显示诊断中的 PSRAM 可用量从 331,756 下降到 320,244 字节；显示缓存约 5 MB，并存在原生旋转缓存。用户随后报告封面恢复，此时资源代数／格式／分块数未变，但界面缓存和原生缓存均为 0，PSRAM 可用量回升到 4,203,876 字节。原始本机数值样本留在忽略目录，不提交运行资料。

这与此前“JPEG 三轮未确认”不同，不能用接收确认宣称已经显示。数据强烈支持显示副本内存不足；现场没有直接导出旧版 `malloc` 返回值或最大连续块，因此不把相关性单独冒充直接测量。

## 可复现路径与修复

生产 `assets_copy` 需要复制解码后的 RGB565 图片到 UI 自有缓冲（本例 224,448 字节）。旧实现分配失败直接返回 false；`assets_progress` 仍因接收端 ready 返回 100%，接收端 ACK 也保持成功，UI 没有主动释放可重建缓存。

使用提取的生产函数、受控分配失败与 C11/UBSan 回归复现 `ready=1 / progress=100 / copy_failed=1 / no_reclaim=1`。修复只在首次分配失败后，由 UI 线程在 LVGL 渲染间隙回收不可见的可重建页面缓存，再尝试一次分配。沿用 `tab5_draw_cache_trim_hidden` 的可见页面、手势与捕获保护；不释放用户照片、正在显示的图片、LCD 缓冲或接收端 ready 图，不修改分辨率、字库或封面像素。

正常分配不触发回收；没有可回收缓存或重试仍失败时，输出对象与接收端图片保持原样，下一轮 UI 可以再次尝试。新增固定数值 `displayDiag` 后缀区分副本失败、回收量与重试恢复，详见 [协议](../PROTOCOL.md)。该共享设备修复适用于 Windows/Mac，桥接不需改变发送协议；两端兼容自检保留新后缀。

## 设计、构建与验收

这是资源生命周期修复，不新增按钮、提示或布局；成功后恢复现有封面显示。实际 .300 更新说明按分类／列表渲染检查，沿用 .299 已验收时钟设计。

- 旧路径故障复现、修复后精确像素／revision、仅一次有界重试、正常路径不回收及失败后双侧图片保留，通过 C11/UBSan 检查。本机 ASan 启动未完成，不计为通过。
- 完整生产 LVGL 回归通过，包括不可见缓存回收和可见页面／手势保护；原时钟 90 组渲染仍通过。
- P4 构建通过，板级配置完全一致；镜像 7,208,880 字节，原分区余量 80 字节。分区余量很小，后续每次必须执行真实镜像尺寸检查。
- [补丁](../proposals/tab5-cover-display-memory.patch)基于 .299，[逐文件与镜像来源](../proposals/tab5-cover-display-memory.json)已实际应用并核对 after 哈希。正式准备脚本生成固定 latest，实际 sidecar 已检查。
- .300 安装与真机结果接续记录；不得把离线注入故障或旧版自动恢复写成新固件的实机恢复验收。

此修复针对**接收已确认后的显示分配**。此前 JPEG 未确认的底层原因、真实 RGB565 回退触发仍保留各自边界，不能一并宣称关闭。

## .300 真机失败与 .301 后续修复

.300 已安装，精确 ELF、VALID 分区、三个实时界面样本及设置保留核验通过，但用户跨总览／Codex／额度／应用后换歌，封面再次循环。此时资源已经进入 336×336、153,388 字节 RGB565 备用传输，尚未接收确认；PSRAM 可用量仅 174,200 字节，四页缓存占 5,036,544 字节，新增显示副本失败／回收／恢复计数全部为 0。这证明本次尚未走到 .300 修复的复制阶段，不能将 .300 记为封面压力验收通过。后续缓存释放后可用量回升，但桥接同一资源已耗尽三轮备用预算；没有因采样或状态刷新自动重置预算。

检查生产调用链发现，6 MiB 上限仅约束已发布普通快照；暂存副本和保留的旋转副本未共享接收空间预算。旋转缓存只在最初分配时保留空间，后续普通快照仍可把空间吃完。接收线程不能直接调用 LVGL 回收函数。

.301 在普通快照、暂存副本和旋转副本的准入处统一保护接收余量：普通／暂存至少保留 3 MiB，旋转缓存沿用更严格的 6 MiB。首次快照一并计算尚需的暂存副本，拒绝时继续正常绘制。UI 每轮在渲染间隙维护余量，包括不预热页面缓存的音乐页；不足时先回收未发布暂存，再回收隐藏快照。可见缓存、活动手势、用户照片和正在使用的封面仍有各自所有权保护。余量不是全局堆配额：其他子系统、碎片或硬件解码失败仍需通过新增计数和真机观察判断。

新增 `asset,2` 接收分配失败、CRC 失败、解码失败及接收成功数，保留前三项副本计数。JPEG 解码失败项包含硬件解码工作区／引擎失败；接收分配项只表示 packed／RGB565 缓冲分配。该诊断不含图像内容，Windows/Mac 兼容夹具同步更新。

### .301 设计与验证

资源策略沿用现有音乐布局、颜色、字号、触控区域和进度／完成状态，不增加交互控件。内存紧张时允许放弃隐藏页缓存加速，维持封面原始像素尺寸和前台可操作性；更新说明继续使用现有分类标题与可滚动列表。实际生产 LVGL 音乐画面与真实 sidecar 渲染已查看。

- 提取生产接收／CRC／RLE／显示复制代码：170 KB 可用预算复现失败，最大 336×336 RLE 的像素逐字节正确，接收分配／CRC／解码失败保留旧 ready 图，后续重传恢复；C11/UBSan 通过。
- JPEG 硬件引擎使用工作区分配替身，最大 560×336、524,288 字节输入，保留旧接收／显示图时峰值 2,553,856 字节，小于 3 MiB。此项只验证预算与所有权，不代替硬件 JPEG 解码验收。
- 真实 LVGL 回归注入总内存预算，覆盖普通／暂存／旋转准入、外部压力后回收、手势期间不回收、可见像素不变、恢复内存后四页缓存恢复；完整 LVGL 回归通过。
- P4 构建通过；镜像 7,208,880 字节，原分区余量 80 字节。仅接收／复制控制函数使用局部 `Os`，像素循环、JPEG 引擎、布局、字库和分区不变。
- .301 [补丁](../proposals/tab5-cover-cache-reserve.patch)与[逐文件来源](../proposals/tab5-cover-cache-reserve.json)接续 .300；两份补丁实际应用及哈希检查通过。
- .301 已安装，精确 ELF、VALID 分区、三个实时界面样本及设置保留通过，发起至健康核验为 96.96 秒。用户完成总览／Codex／额度／应用、返回音乐及两首歌各停留约 15 秒，确认封面都正常，没有循环。后台最后一张 336×336 JPEG 第一轮接收确认，接收成功计数 5，接收分配／CRC／解码／显示副本失败均为 0；抽样 PSRAM 最低 4,578,292 字节，最后 4,703,712 字节。本轮没有触发 RGB565 备用传输成功，不能宣称该异常路径或无限期稳定性已验证。

### English follow-up

The .300 image booted and preserved settings, but music stress acceptance failed before UI copy: 174,200 bytes of free PSRAM, 5,036,544 bytes of page snapshots, unconfirmed RGB565 fallback and zero copy-failure counters. This is an incomplete fix, not a hardware pass. The optional cache budget previously excluded staging and retained native buffers.

Firmware .301 reserves 3 MiB for receive/decode/display across ordinary snapshots and staging; native acceleration retains its stricter 6 MiB admission threshold. UI-thread maintenance reclaims unpublished staging and hidden snapshots between render passes while preserving visible images and gestures. A low-memory receiver reproduction, exact maximum RLE pixels, failure ownership, a maximum JPEG workspace budget double (2,553,856 bytes), and real LVGL pressure/visibility regression pass. JPEG hardware remains a separate physical check. Optional asset diagnostic version 2 distinguishes receiver allocation, CRC and decode failures from display-copy failure. Host fixtures preserve the numeric suffix. The original partition, resolution and fonts are unchanged; the 7,208,880-byte image has 80 bytes of headroom. This source update is not a release.

Hardware follow-up: .301 exact boot/settings pass in 96.96 s including health observation. The user confirms normal artwork after all four primary pages and two track changes with approximately 15-second dwell. The final 336×336 JPEG is confirmed on its first pass; five assets received and all allocation/CRC/decode/copy failure counts remain zero. Sampled free PSRAM stays at or above 4,578,292 bytes. This verifies the reported scenario, not indefinite stability or successful real RGB565 fallback.
