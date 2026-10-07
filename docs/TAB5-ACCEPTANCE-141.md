# 本地 .141 最小回归候选

2026-10-07。本地完整版；构建、原生回归、精确镜像启动与实屏验收通过。用户确认：“没有蓝闪，图片和自动转向都正常”。

## 可复现基线

用户确认原始 `.131` 普通界面、名画/书法和四方向不蓝闪，但 `.140` 恢复字体与显示路径后仍蓝闪，故字体压缩不能认定为根因。

使用当前 SDK、依赖和配置，将固件源文件暂时还原至 `87881e1` 并重建 `.131`。新旧镜像同为 7,206,336 字节，七个加载段地址与长度相同；全部代码和数据逐字节相同，只有应用描述中的构建时间和 ELF 摘要共 38 字节不同（外围校验值随之改变）。证明当前构建环境可以复现稳定版本，不能再将 SDK 漂移作为未经证实的归因。重建后工作源码已从逐文件备份恢复。

## 变更范围

`.141` 在 `.131` 运行逻辑上只增加自动屏保期间对后台翻页事件的状态保护，避免屏保层仍显示而 `saver` 被错误清零。原字体、显示参数、扫描驱动、图片接收及缓存生命周期全部保留。公开/本地编译开关保留，不改变公开版排除图库的约定。

撤回 `.135` 图片类别缓存回收与图片诊断、`.140` 进入屏保释放页面缓存，以及 `.136`–`.139` 显示实验。对应失败版本、源码备份与验证记录保留在本地 artifacts。图片等待问题及容量优化不宣称已经解决。

新增诊断中的 `heap_caps_get_largest_free_block(MALLOC_CAP_SPIRAM)` 会经 `heap_caps_get_info` 进入 `multi_heap_get_info_impl`，在 `portENTER_CRITICAL_SAFE` 内遍历整个 TLSF 池；它曾位于每次 USB 应答的路径。这是可能延误显示中断的具体代码风险，尚无本设备耗时测量及单因素实屏证明，不能据此认定为最终根因。

## 验证与身份

- 本地固件编译、分区尺寸、扫描 ISR 的 IRAM 和 XIP 链接检查通过。
- 与复现 `.131` 的 ELF 对比，所有函数大小一致，仅 `tab_changed` 从 194 字节增至 208 字节；不将“大小相同”扩大称为不同版本所有机器指令都相同。
- 原生完整 UI、实际名画/书法横竖屏图片、自动屏保的生产时钟与延迟翻页事件回归通过；JPEG DMA 完整图像和失败路径测试通过。
- 7,206,336 字节，现有 7,208,960 字节槽位剩余 2,624 字节；容量问题仍未解决。
- SHA-256：`b90b0bbd692a73d0c6633d2a80dd2555569a681a693fbf695a40184b7cecd140`。
- ELF SHA-256：`c682aa2655fed7901f45fc31a502be9b963e1b1528f40a90882197adf16bea89`。

固定 OTA 路径与历史归档由正式包校验器生成；实际 sidecar 两页预览已检查，分类和条目可读。12:36 已提供给 TAB5，原六页顺序及 15 秒自动轮播保持。实机为 `ota_1 VALID`，精确镜像/ELF、三次递增心跳（UI 年龄 11/7/48 ms）、方向传感器及零显示错误均通过。用户确认普通界面/输入、两类图片及自动转向正常。原自动模式、六页顺序与 15 秒间隔已恢复。安装、实际 sidecar 预览及屏幕观察见 `artifacts/development/gallery-preview-sync/`。原始 `.131` 保留回退。未推送、打标签或公开发布；公开 `.133` 保持暂停。

## English

The user accepted archived .131 but reported blue flashes again on .140; restoring fonts alone did not identify or resolve the cause. Rebuilding the original .131 source with today's SDK and configuration reproduces every code/data byte except 38 application-description bytes for build time and ELF identity. This provides a reproducible baseline rather than an assumed SDK explanation.

Local .141 retains that runtime behavior and only protects automatic-screensaver state from a late background tab event. Public/local build selection remains. Later gallery/page-cache changes, gallery diagnostics and unsuccessful display tuning are withdrawn. A full external-heap walk inside a critical section on the previous diagnostic reply path is a concrete risk, not a proven individual cause. Build, IRAM/XIP, native actual-image/four-direction/automatic-saver and JPEG DMA checks pass. Only tab_changed changes function size (194 to 208 bytes); that is not a claim that relocated instruction bytes are identical. The image leaves 2,624 bytes of headroom, so capacity remains unresolved. Exact-image/ELF ota_1 VALID boot, UI heartbeats and IMU passed. The user confirmed no blue flashes and normal images/automatic rotation. Original auto mode, six-page order and 15-second interval were restored. This accepts the combined minimal rollback, not an individually proven cause. Public release remains unapproved.
