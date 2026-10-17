# 发布与上线收尾 / Release closeout

每次上线都执行本清单。发布附件完成不等于上线收尾完成；记录各项结果及未完成原因，不把候选、模拟或构建通过写成真机验收。

1. 明确本次组件、平台、版本、远端与提交，核对已有授权。未变更的平台保留已发布版本；Windows 与 Mac 可独立交付。
2. 核对源码身份、必要回归、安装及真实设备验收。保留已验收包的字节，不为文档或发布流程修改重复打包。
3. 校验包格式、首刷与升级区别、兼容别名、许可证和 SHA-256；推送带注释的组件标签，核验草稿附件后正式发布。
4. 发布后刷新 `download-catalog.json`，运行 `download_catalog.py sync` 和 `check`，同步中英文主页、完整下载中心及 Release 说明。Windows、Mac、ESP8266、TAB5 和图库按实际附件分别取版本。
5. 全面复核主页介绍、功能说明、安装手册、安装说明、更新步骤及组件版本页，更新本次变化。保留正确的兼容性下限和历史验收记录。
6. 刷新相关截图和宣传视频（含封面与可复现工程），以当前实际组件或隔离原生控件录制。检查文字、点击路径、版本及音画；必须在公开主页实际点击播放器，确认时间推进、暂停／继续与跳转有效，不能以封面下载链接、HTTP 200 或离线解码代替播放验收；标明演示数据与实拍边界，登记来源、许可及媒体 SHA-256。
7. 运行公开内容与许可检查、文档链接检查和 `git diff --check`；核验公开主页、安装说明、媒体及实际下载入口。大文件优先比对本地完整哈希、服务器 digest、HEAD 和小 Range；不把抽查称为远端完整下载校验。
8. 记录正式发布地址、提交、附件摘要、验证和剩余限制。以上项目均有完成证据后交付。

Every release follows this checklist: confirm component/platform scope and authorization; preserve accepted package bytes; validate source, tests, installation and hardware separately; publish verified assets under annotated component tags; refresh bilingual home/download/release pages; reconcile all introductions and installation/update instructions; update relevant screenshots, the product video, covers and reproducible media sources; register licensing and hashes; verify public pages and downloads; then record completion evidence. An unchanged platform retains its existing published version. A successful upload alone does not complete release closeout.
