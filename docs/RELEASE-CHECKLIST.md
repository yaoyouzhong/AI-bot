# 发布与上线收尾 / Release closeout

每次上线都执行本清单。发布附件完成不等于上线收尾完成；记录各项结果及未完成原因，不把候选、模拟或构建通过写成真机验收。

1. 明确本次组件、平台、版本、远端与提交，核对已有授权。未变更的平台保留已发布版本；Windows 与 Mac 可独立交付。
   电脑端标签发布前，将 `release-manifest.json` 中的 `components.bridge.desktopPlatform` 设为本次范围：`windows`、`macos` 或 `all`。缺省为 `all`；此项应随每次发布重新核对。自动候选只构建指定平台，单平台发布不重建另一平台。
2. 核对源码身份、必要回归、安装及真实设备验收。保留已验收包的字节，不为文档或发布流程修改重复打包。
3. 校验包格式、首刷与升级区别、兼容别名、许可证和 SHA-256；推送带注释的组件标签，核验草稿附件后正式发布。
4. 发布后刷新 `download-catalog.json`，运行 `download_catalog.py sync` 和 `check`，同步中英文主页、完整下载中心及 Release 说明。Windows、Mac、ESP8266、TAB5 和图库按实际附件分别取版本。
5. 全面复核主页介绍、功能说明、安装手册、安装说明、更新步骤及组件版本页，更新本次变化。保留正确的兼容性下限和历史验收记录。
6. 刷新相关截图和宣传视频（含封面与可复现工程），以当前实际组件或隔离原生控件录制。检查文字、点击路径、版本及音画；标明演示数据与实拍边界，登记来源、许可及媒体 SHA-256。
   - 主页宣传片介绍整个项目：共有功能对照两种硬件，TAB5 独有功能单独介绍；画面、文字和设备排列一致，封面不带具体版本号。中英文文案同步，历史更新放入更新日志。
   - 分别验证主页内实际播放、独立观看入口与 MP4 下载；覆盖播放、暂停、拖动和重播，HTTP 200 不等同播放成功。
7. 运行公开内容与许可检查、文档链接检查和 `git diff --check`；核验公开主页、安装说明、媒体及实际下载入口。大文件优先比对本地完整哈希、服务器 digest、HEAD 和小 Range；不把抽查称为远端完整下载校验。
8. 记录正式发布地址、提交、附件摘要、验证和剩余限制。以上项目均有完成证据后交付。

Every release follows this checklist: confirm component/platform scope and authorization; preserve accepted package bytes; validate source, tests, installation and hardware separately; publish verified assets under annotated component tags; refresh bilingual home/download/release pages; reconcile all introductions and installation/update instructions; update relevant screenshots, the product video, covers and reproducible media sources; register licensing and hashes; verify public pages and downloads; then record completion evidence. An unchanged platform retains its existing published version. A successful upload alone does not complete release closeout.

Homepage video covers the whole project: compare shared hardware features, present TAB5-only features separately, match captions and device order, and omit version numbers from covers. Verify actual inline playback, a separate viewing entry and MP4 download; synchronize bilingual copy.
