# TF 卡目录约定 / TF storage layout

目录格式为 v1。卡根目录只由本项目创建 `aibot/`，设备挂载点 `/sdcard` 不属于卡内目录。由 `firmware/main/sd_paths.h` 集中定义路径；新功能必须沿用下列用途，并在所属模块目录内读写，不能自行在卡根目录新增文件。

当前固件需要 FAT／FAT32 卡，不支持 exFAT。128GB 实卡已在电脑按整卡容量格式化为 FAT32、32KB 分配单元，并通过读写校验；不需要缩成 32GB 分区。新卡先在电脑准备好文件系统，设备不会自动格式化。

目录及记录名称需要 FAT 长文件名支持：默认配置启用 `CONFIG_FATFS_LFN_HEAP=y`，缺少支持时构建直接报错。已安装 .177 缺少此配置，可能挂载成功但无法保存；修复已登记到待构建 .178。实际卡挂载与保存分别验收。

待构建 .178 提供独立的「设置 → 存储与缓存」：卡状态由后台检测，不依赖屏保选择或桥接在线；本页同时显示图库保存进度。缓存位置默认「设备缓存」，即原来的按需下载与 PSRAM 内存缓存，重启后需重新同步图片，不是把整套图片写入内置闪存。插卡本身不会开启图库保存；主动选择「TF 卡」并保存成功后才启用整库同步与读卡。设置保存在设备上，失败恢复原选择。卡缺失或不可用时保留该选择并回退设备缓存；切回设备缓存不删除卡上作品。当前选择只控制图库，未来其他模块在本页增加各自的存储设置。

整库保存与浏览入口是两项能力：目前每日屏保按当天日期选作品，左右翻页只切换同一作品的分页，尚未提供往期作品选择页。

```text
aibot/
├── gallery/
│   ├── painting/
│   │   ├── catalog.0 / catalog.1
│   │   └── works/<作品索引键>/
│   │       └── <页码>-<l或p>.<0或1>.aig
│   └── calligraphy/                 # 相同结构
├── photos/                         # 项目保存的照片，预留
├── data/<模块名>/                  # 其他需要长期保留的数据，预留
└── cache/<模块名>/                 # 可重新生成的数据，预留
```

- **图库**：保存已导入整套图库的图片、简介、欣赏提示和原文（包中存在时），并保留完整性校验和同步进度。作品索引键是从 `2026-01-01` 起枚举的稳定键，不是每日下载日期；每件作品的全部分页及横竖屏分别保存。`l` 表示横屏，`p` 表示竖屏，页码从 `00` 开始。图片与文字在同一 `.aig` 记录中，两份记录轮换提交；未完成的 `.part` 只写在同一作品目录，不作为有效内容读取。
- **照片**：后续由项目保存到卡的照片使用 `photos/`。当前照片功能读取用户选择的 JPG，不向卡保存拍摄照片；仍可浏览卡上原有目录，不移动旧照片。
- **其他数据**：例如未来的笔记或历史记录放入 `data/<模块名>/`；缩略图、下载缓存等可以重建的内容放入 `cache/<模块名>/`。这些是目录约定，不表示相关功能已经实现。目录按需创建，预留目录不提前占位。
- **归属与升级**：各模块只替换自己的已校验记录，不格式化卡，不移动、删除其他目录的用户文件。卡满、只读或不可用时暂停保存并保留原有内容。后续若调整结构，需要明确版本兼容或迁移逻辑，不能静默换路径丢弃旧资料。
- **配置**：配对密钥和 Wi-Fi 凭据继续使用现有设备安全配置路径，本次不迁移到 TF 卡。

The card layout is v1. All project-written files live under `aibot/`; `/sdcard` is a device mount point. `sd_paths.h` centralizes names. Gallery category directories contain checked alternating cursors and per-work records under `works/<canonical key>/`. These keys enumerate works from 2026-01-01 and are independent of the viewing/download date. Each record binds its page/orientation, verified JPEG and optional full guide; temporary `.part` files stay beside their destination.

2026-10-15 增量优化候选额外使用 `<页码>-<l或p>.<0或1>.aig.meta` 两槽元数据文件。`AIBART03` 引用已有完整图片槽及序号，在 JPEG 未变时只写新资料，不复制 JPEG；读取仍验证源槽、作品身份及图片 SHA-256。普通图片更新保护上一有效槽，失败可回退。既有 AIG1/AIG2 不迁移；照片和用户其他文件不变。旧固件不识别新元数据引用，回退旧版后可能重新获取对应版本的资料。

The 2026-10-15 incremental candidate adds alternating `.aig.meta` sidecars. AIG3 references an existing complete image slot and serial, updating text without rewriting unchanged JPEGs. Reads still verify source identity, work and JPEG SHA-256; image replacement retains a complete survivor. AIG1/AIG2 need no migration. Older firmware ignores sidecars and may refetch the required revision after downgrade.

The current firmware requires FAT/FAT32 and does not enable exFAT. A real 128GB card was formatted at full capacity on the computer with FAT32 and 32KiB allocation units and passed read/write verification; a 32GB partition is unnecessary. Prepare new cards on the computer; the device does not format automatically.

Directory/record names require FAT long filenames. Defaults enable `CONFIG_FATFS_LFN_HEAP=y`; builds reject missing support. Installed .177 lacked it and could mount without saving; the fix is registered for pending .178. Mounting and saving are separate hardware checks.

Pending .178 adds Settings → Storage & Cache. Background card detection and the gallery progress row are independent of the selected screensaver and bridge connection. The default Device cache retains on-demand downloads and the existing PSRAM image cache; images require synchronization after reboot, rather than persisting the complete collection to internal flash. Inserting a card does not opt in. Only an explicitly saved TF choice enables gallery card reads/writes and complete-library synchronization. The device persists the choice and restores it after failed saves. An unavailable card falls back to device caching without clearing the choice; disabling TF leaves saved works intact. This choice controls galleries only; future modules can add their own storage settings here.

Complete-library storage and browsing are separate capabilities. The current daily screensaver selects by today's date, and arrows only change pages within that work; no past-work selection page is implemented yet.

Future project-written photos use `photos/`, persistent module data uses `data/<module>/`, and reproducible caches use `cache/<module>/`. Reserved namespaces are created only when used and do not imply implemented features. Current photo browsing reads user-selected JPGs without relocating them or saving camera captures to TF. Writers own only their module files, preserve unrelated user content, and pause on card errors. A future layout change requires explicit compatibility/migration. Existing pairing and Wi-Fi credential storage remains unchanged.
