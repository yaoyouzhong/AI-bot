# 发布与上线收尾 / Release closeout

每次上线都执行本清单。发布附件完成不等于上线收尾完成；记录各项结果及未完成原因，不把候选、模拟或构建通过写成真机验收。

开发、双平台交接和候选验收统一遵循 [Windows / Mac 开发发布流程](DESKTOP-DEVELOPMENT-RELEASE.md)。双平台构建锁定同一源码 SHA；每次仅一个发布负责人。[发布自动化](RELEASE-AUTOMATION.md)负责冻结候选、绑定验收、晋级原包与下载元数据收尾。标签事件不构建，同一原包不因上传或收尾重新验收；变化范围之外的有效证据直接复用。

1. 明确本次组件、平台、版本、远端与提交，核对已有授权。未变更的平台保留已发布版本；Windows 与 Mac 可独立交付。
   电脑端标签发布前，将 `release-manifest.json` 中的 `components.bridge.desktopPlatform` 设为本次范围：`windows`、`macos` 或 `all`。缺省为 `all`；此项应随每次发布重新核对。自动候选只构建指定平台，单平台发布不重建另一平台。
2. 核对源码身份、必要回归、安装及真实设备验收。保留已验收包的字节，不为文档或发布流程修改重复打包。
3. 校验包格式、首刷与升级区别、兼容别名、许可证和 SHA-256；用 `promote` 一次完成带注释的组件标签、草稿上传、服务器摘要检查和已授权的正式发布。重试同一命令只补未完成项，禁止覆盖已公开附件。
4. 发布后刷新 `download-catalog.json`，运行 `download_catalog.py sync` 和 `check`，同步中英文主页、完整下载中心及 Release 说明。Windows、Mac、ESP8266、TAB5 和图库按实际附件分别取版本。
5. 全面复核主页介绍、功能说明、安装手册、安装说明、更新步骤及组件版本页，更新本次变化。保留正确的兼容性下限和历史验收记录。
6. 按实际变化评估截图和宣传视频（含封面与可复现工程）。小幅更新且旧素材仍准确时，登记“已有无需修改”并复用，不要求每次重新截图或编码。界面、操作路径或功能表达发生实质变化、旧素材会误导用户时，仅更新受影响部分；以当前实际组件或隔离原生控件录制，检查文字、点击路径、版本及音画，标明演示数据与实拍边界，登记来源、许可及媒体 SHA-256。复用素材保留原来源与摘要，不写成新媒体已更新。
   - 主页宣传片介绍整个项目：共有功能对照两种硬件，TAB5 独有功能单独介绍；画面、文字和设备排列一致，封面不带具体版本号。中英文文案同步，历史更新放入更新日志。
   - 宣传视频的现有展示顺序已由用户优化确认，作为后续维护基线。只替换受影响的页面或镜头，或按需增加内容；新增内容保持既有段落的相对顺序。未经用户另行明确要求，不大幅改版、重排顺序或整片重做。仅为输出更新文件而重新编码，不视为获准重设计视频。
   - 新增或改动视频、播放器或观看入口时，验证受影响的主页内实际播放、独立观看入口与 MP4 下载，覆盖播放、暂停、拖动和重播；HTTP 200 不等同播放成功。媒体字节和播放集成都未变时，可复用既有播放验收，不因版本号变化重测整套播放流程。
7. 运行公开内容与许可检查、文档链接检查和 `git diff --check`；核验公开主页、安装说明、媒体及实际下载入口。大文件优先比对本地完整哈希、服务器 digest、HEAD 和小 Range；不把抽查称为远端完整下载校验。
8. 记录正式发布地址、提交、附件摘要、验证和剩余限制。以上项目均有完成证据后交付。

Every release follows this checklist: confirm scope and authorization; preserve accepted bytes; validate source, tests, installation and hardware separately; publish verified assets; synchronize affected bilingual descriptions and download/install instructions; and verify public pages and downloads. Minor changes may retain accurate screenshots, video, covers and media source projects. Replace only material made misleading by changed interfaces, workflows or feature claims, registering new provenance, licenses and hashes. Record reuse without claiming new media was produced. An unchanged platform retains its published version; upload alone does not complete closeout.

Homepage video covers the whole project: compare shared hardware features, present TAB5-only features separately, match captions and device order, and omit version numbers from covers. Changed video or playback integrations need actual playback checks; unchanged media and integrations may reuse prior acceptance. Synchronize affected bilingual copy.

Preserve the existing user-approved presentation order and overall video structure. Future updates replace only affected pages or shots, or add needed content while retaining the relative order of existing sections. Major redesign, reordering or a complete remake requires a separate explicit user instruction. Re-encoding an updated file does not authorize redesign.
