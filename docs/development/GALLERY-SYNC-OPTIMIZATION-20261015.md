# TAB5 图库增量同步优化（2026-10-15）

目标：内容未变不重传、不重写 JPEG；同一作品各分页和横竖屏避免重复发送完整导读。保留断点、离线完整原文、校验和失败回退。TAB5 设置独立于 ESP8266，本次不执行小屏的恢复自动循环操作。

## 已确认的原有行为与浪费

- 原有整库同步已比较新清单与旧 JPEG 的 SHA-256，未变图片不请求 `read` 分段；完成作品的类别游标可断点续传。
- 但复用图片仍调用整文件保存，导致 TF 重复写 JPEG；同一作品每个图片清单重复附带完整导读。
- 本次书法包实际修改了全部 1,901 张横屏图；1,901 张竖屏图保持原字节。横屏必须传输，竖屏应复用。

## 改动

- 设备通过 `gallery_store_refresh` 写两槽校验元数据，引用原 JPEG 槽及序号；不重写未变图片。加载仍验证实际 JPEG，引用失效不当成命中；完整新图片提交保护原可用槽。
- 完整导读按类别保留最近一个作品；仅日期、认证内容摘要、作品身份和完整原文一致时请求复用。桥接匹配摘要／作品后省略重复正文，设备深拷贝原文分页。旧桥接和旧设备继续原有完整导读路径。
- Windows 缓存包摘要，成功导入时失效；相同文件大小／时间戳的纯文字替换也正确更新。
- 不改变图片质量、分辨率、作品数、配对认证、请求优先级、前台保护和 PSRAM 安全余量。

## 本地验证

证据目录：`artifacts/development/gallery-sync-optimization-20261015/`。

| 场景 | 原路径 | 优化后 |
|---|---:|---:|
| 1 MiB JPEG 纯文字更新 TF 写入 | 1,049,016 字节 | 492 字节（减少 99.95%） |
| 18 张未变 JPEG 的设备 worker | 原路径反复写入图片 | 0 图片分段请求，TF 总写入 10,013 字节 |
| 3 件作品、每件 3 页 × 2 方向完整导读 | 18 次 | 3 次完整 + 15 次引用（减少 83.3%） |

存储回归覆盖完整文字、重启、卡满、rename 失败、损坏元数据／JPEG、源槽保护、错误作品；整库回归覆盖两类别、所有页／方向、冷启动续传、跨年、纯文字更新、三通道接口回归、前台优先和后台不解码 RGB。7 件真实长原文 fixture 经三种传输接口替身验证完整 SHA、导读引用、离线读取和独立内存生命周期。Windows Release 构建、图库包／导读兼容性自检及只读 `--status-once` 已通过。

以上为本地代码回归与写入字节测量，不代表真机倍速。现用设备仍运行 0.2.194-ui，现行同步未被中断；只有安装匹配的桥接与固件后才能实测端到端速度。旧桥接也可使用设备端元数据写入优化，但不会省略重复导读。

## 待授权安装的具体候选

ESP-IDF 完整构建与正式 `prepare_tab5_firmware.ps1` 镜像校验已通过。固件从 TAB5 `91888b3f986812f00207b562f2b2d78f066fee49` 的隔离快照构建，仅覆盖本次 3 个图库生产文件；桥接从 AI-bot `a2e0b577d991373c0877472016942804dd179873` 的隔离快照构建，仅覆盖本次图库服务与自检文件。未混入工作区的日程等未提交修改。

| 对象 | 路径 / SHA-256 |
|---|---|
| TAB5 0.2.195-ui | `artifacts/firmware/tab5/latest/aibot_tab5.bin` |
| 镜像 SHA-256 | `c502008a41e6bc111f1519acf4307b063eed3a22c08330617e971c37138743a5` |
| Windows 桥接候选 | `artifacts/development/gallery-sync-optimization-20261015/AIBotBridge-gallery-sync-20261015.zip` |
| ZIP SHA-256 | `905d2456b10f0fd00e879a0768070d34d9ac0f771261a8bd4ba7870902fcc384` |
| 桥接 DLL SHA-256 | `8193df360aef693e244ba348aecff4ffa469127018c4033fc2f58b881067fb63` |

镜像为 7,196,448 字节，比现用 .194 固件增加 5,120 字节；通过既有 OTA 分区边界校验，分区余量 12,512 字节，因此构建提示分区接近满。未更改分区、NVS 或资料目录。仅交付应用升级 BIN，不使用首次安装的全量写入命令。历史镜像／说明已归档保留。

尚未部署桥接或写入设备。后续验收需分别证明：安装版本与哈希、启动及回滚状态、未变化图像跳过、资料更新后离线显示、同步端到端耗时，以及 TAB5 现有展示设置保留。桥接更新需正常退出后替换并重启以加载新逻辑；不运行 ESP8266 的 `--restore-cycle-default`。

English: local candidates eliminate redundant unchanged-image writes and repeated per-frame guides while retaining SHA checks, complete originals and restart recovery. Native fixtures measured 492 bytes instead of 1,049,016 for a 1 MiB metadata-only update, and 3 full guides plus 15 references instead of 18 full guides. Complete ESP-IDF/Windows builds and compatibility regressions passed. These figures measure local I/O and message reduction, not physical-device speed. Device installation and end-to-end acceptance remain pending authorization.
