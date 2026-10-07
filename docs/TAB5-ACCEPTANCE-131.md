# TAB5 0.2.131-ui 书法花屏排查

## 现象与判断

- .130 的四方向朝向、切换稳定性已由用户确认。随后用户报告书法持续花屏，名画正常；横屏下方与竖屏左侧是同一物理区域。
- 当日书法 `colbase-tnm-TB-6` 有 3 页，横竖屏共 6 张 JPEG 均为 1280×720，运行目录与源码资源的 SHA-256 完全一致。检查过的本地图片未出现花屏。
- 当前 ESP-IDF 5.4.2 的 JPEG 输出分配器使用 PSRAM calloc，解码器只在 DMA 完成后使输出缓存失效。缺少 DMA 前的脏缓存清理，存在清零缓存回写覆盖新像素的风险。本次修正该缺口后，用户确认花屏问题已解决；没有采集硬件 DMA 轨迹，不将模拟故障模型表述为硬件内部过程的直接观测。

## 修改和验证

- 在画廊 JPEG 解码前对已对齐的完整输出缓冲区执行 C2M 写回并失效；同步失败时不提交新图，保留已成功显示的图片。继续使用驱动自带的解码后 M2C。
- 保持 .130 的传感器判向、防抖、排版、图片资源、双帧缓存和分页行为。
- `gallery-dma-test` 直接编译生产解码片段：模拟脏尾部缓存覆盖的负对照、12 次完整帧检查，以及分配/同步/解码/长度异常拒绝。该测试是缓存故障模型，不是硬件复现。
- UI 回归逐像素检查两类画廊各 12 次横竖布局替换、3 页循环，并比较完整帧与设备同尺寸分段刷新（包含最后一条短条带）。方向与驱动恢复测试独立执行。

## 交付状态

- 本地证据目录：`artifacts/development/tab5-gallery-131/`。
- 编译、缓存故障模型、两类画廊各 12 次逐像素替换、完整 UI 回归、方向与驱动恢复测试均通过；实际更新说明首屏和滚动预览已检查。
- 固定候选 `artifacts/firmware/tab5/latest/aibot_tab5.bin`：7,206,336 字节，SHA-256 `3a1b3655f4becc544575df4871a965f4a578dd4552a077444728ec90a03d0125`，ELF `ee715c1822172327acff1ff2afdf3ab30d6739ea9153ad1b4ffbe356e4b0bbea`。
- 2026-10-07 已安装。COM9 三次采样确认设备 `e8f60ae2ec56`、版本和 ELF 与交付物一致，运行时间递增；`ota_0`（`0x20000`）为 VALID，显示错误和 LCD 欠载均为 0。方向传感器 ready、error=0，采样年龄 67 ms。
- 用户反馈“这次ok了”，书法花屏视觉验收通过；此前 .130 的四方向朝向及稳定性已确认。保留升级前照片，不将此反馈扩大为对全部 813 件作品逐件实机检查。
- 核验后正常退出桥接，执行 Release `--restore-cycle-default` 并重启；auto/循环启用，原六页顺序与 15 秒间隔一致。证据为 `boot-verified.json`、`display-after.json`、`user-acceptance.json` 和 `closeout.json`。
- 应用分区剩余 `0xA40`（2,624）字节；本版适配现有分区，后续增加功能前需先评估瘦身。两个仓库仍有未提交改动；本次未提交、推送或公开发布。

## 本轮收尾复核

- 年度完整性重检通过：402 件名画、411 件书法，无错误。逐文件 SHA-256 核对源码与实际桥接运行目录：3,804 张横竖屏 JPEG 及 catalog/provenance，共 3,806 个文件全部一致。
- 书法 223 件单页、188 件多页，301 件不超过 3 页、41 件超过 8 页；升序展示顺序核对通过。用户要求的长卷保留，没有为缩短页数删减作品。
- 已同步中英文首页、日志、中文组件 .131 条目及每日艺术说明，过期的“未部署”叙述明确标为历史。
- 当前个人设备功能验收收口；未完成的工程事项为两仓库按范围提交归档、后续功能前的固件容量评估。若启动正式分发，另行准备发布包及发布验收；本次没有发布授权。
- 没有已核实中文译名的作品保留原馆藏名称，是既定交付规则，不作为缺陷。未逐件实机观看全库，长期运行也不扩大宣称已覆盖。

## English

The user accepted .130 orientation, then reported persistent calligraphy corruption at the same physical display edge. All six current calligraphy JPEGs match the deployed copies. Add the missing cache writeback/invalidation before JPEG DMA writes its calloc-initialized PSRAM output, retaining driver post-DMA invalidation and .130 orientation behavior. Production-code fault modeling and UI replacement/partial-strip tests pass. On 2026-10-07, exact-image USB boot verification confirms ota_0 VALID, healthy orientation sampling and zero display errors/underruns. The user confirms the corruption is resolved. Original automatic cycling, page order and 15-second interval are restored. This is not a hardware DMA trace or visual review of every artwork. Partition headroom is 2,624 bytes; source changes remain uncommitted and unpublished.
