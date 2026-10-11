# TF 卡目录约定 / TF storage layout

目录格式为 v1。卡根目录只由本项目创建 `aibot/`，设备挂载点 `/sdcard` 不属于卡内目录。由 `firmware/main/sd_paths.h` 集中定义路径；新功能必须沿用下列用途，并在所属模块目录内读写，不能自行在卡根目录新增文件。

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

Future project-written photos use `photos/`, persistent module data uses `data/<module>/`, and reproducible caches use `cache/<module>/`. Reserved namespaces are created only when used and do not imply implemented features. Current photo browsing reads user-selected JPGs without relocating them or saving camera captures to TF. Writers own only their module files, preserve unrelated user content, and pause on card errors. A future layout change requires explicit compatibility/migration. Existing pairing and Wi-Fi credential storage remains unchanged.
