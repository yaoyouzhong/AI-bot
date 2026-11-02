# Mac 0.6.15 发布与收尾

[AI-bot v0.6.15](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.6.15) 于 2026-11-02 13:21:41（Asia/Shanghai）公开。用户明确要求“发布”，本轮只交付 Mac；Windows 0.6.14、ESP8266 0.5.7、TAB5 0.2.302-ui、图库与旧发布附件保持。

## 源码、标签与附件

- 带注释标签 `v0.6.15` 指向已验收源码 `8f4a5edf0b81af977706ae30cfd38035572fd5f8`；远端注释标签对象为 `2e30c695cd892d99d96542f73bd6fc405d9e16b1`。文档与媒体收尾单独提交，不改变程序来源。
- Mac ZIP 18,665,971 字节，SHA-256 `f07ca7b0a31ebff6d42d64fd52725a603795d43882722875c148296b8c15036a`。实际安装、本机验收与草稿完整下载相同；包内 17 项文件及签名保持。未重建、重签或替换已发布附件。
- 源码 ZIP 81,096,623 字节，SHA-256 `31e15a9da7a5aba136dbf626ace404d245ea8903e4cb52fe43eb8a179afeeff3`。发行附 LICENSE、THIRD_PARTY_NOTICES、BUILD、脱敏 ACCEPTANCE 与 SHA-256。
- 共 12 项附件的服务器 digest 与本地完整摘要一致。Mac 草稿原包做了完整下载；大源码包以服务器 digest 与本地完整摘要核对，不声称另做了完整远端下载。
- 标签自动触发的 [Release 任务 41392707526](https://github.com/yaoyouzhong/AI-bot/actions/runs/41392707526) 已主动取消，原因是本轮晋级已验收本地原包，防止自动重建产生未验收字节。没有修改工作流权限或触发器；主干 CI 独立验证，不替代安装验收。

## 代码、界面与真实验收

424 项 macOS 测试、Release 构建、实际包 29,059 项隔离检查、任务追加、包完整性和离线工具检查通过。四份授权引导明暗／首次／升级原生预览已查看并与实际包渲染摘要核对。

用户确认一次统一钥匙串提示、应用引导可见且能打开设置；旧条目虽已勾选仍未生效，用户重新添加当前应用后，同包正常重启无新提示、TAB5 定位成功。后台新进程记录定位 200 和实时 USB 心跳。38 项固定设置、5 份配置、29 份原加密资料、3 份资源保持；仅预期构建引导标记更新，未导出凭据。用户确认真实播放／暂停正常，没有新的自动化提示，播放器名称未登记。[详细验收](MAC-0615-PACKAGE-ACCEPTANCE-20261102.md)

## 文档、截图与媒体

下载目录按实际附件仅提升 Mac 为 0.6.15，其他条目逐一与操作前比较保持一致；公开资产、校验文件、HEAD 及高清图库清单核验通过。中英文主页／下载中心、功能页、安装与升级说明、组件版本页、快捷会话说明同步，并明确临时签名授权限制。

新增两张实际包的原生授权引导图；保留旧版管理窗口截图的历史来源，不冒称新捕获。新增 24 秒、1280×720、24 fps、H.264 静音 Mac 说明短片；三幅实际静帧已查看，完整解码通过。媒体与来源工程摘要分别为 `5ee0de101504b1377de6b18667428ad07feda9bf87c67acfa80395608463d4db`、`78ba61eaa99f4cf4ade245534a94f478bf1b117a85bf871abac0b2b92e6a7c70`，仅提供下载，不声称主页内嵌播放。来源、许可、封面及 SHA-256 已登记于 [媒体说明](../MAC-AUTHORIZATION-VIDEO.md)、[截图说明](../SCREENSHOTS.md)、PROVENANCE 和 `licenses/materials.json`。

主项目影片保留双硬件介绍与原音轨。公开主页后台浏览器实测播放 `0.21152s`、暂停 `0.498127s`、时间轴调整至 `1.70896s` 及 `121.083333s`、从片尾重播至 `0.155635s`；独立观看页实测播放 `0.091457s` 后暂停 `0.10022s`。源均为已登记公开附件。测试静音，不声称自然播完或新的主观听感验收。360 个相关本地文档路径已检查存在；公开页面及下载按收尾材料另作回读。

## 已知边界

Mac 为 Apple Silicon、临时签名、未公证；Intel、浏览器隔离属性下首次打开、公开 0.6.14 分散资料首次迁移、全新用户与首次自动化弹窗未实测。原旧项目首迁可能仍需逐项批准，升级不保证系统授权永久保持。ESP8266、DJI、长闲置首录本轮未重新验收。发布使用用户在这些范围已明确披露后要求发布的原包，不将未测项记为通过。

English: Mac-only 0.6.15 was published under an annotated tag at accepted source `8f4a5ed`; Windows, firmware, galleries and old assets remain unchanged. All 12 server digests match local files, and the complete draft Mac download equals the installed accepted ZIP. The automatic tag rebuild was intentionally cancelled; no workflow settings were changed. Local tests, native previews, one Keychain prompt, app guidance, restart, settings preservation, physical navigation and play/pause pass within the recorded scope. Bilingual downloads and instructions, native screenshots and a silent reproducible video supplement are updated with provenance and hashes. The unchanged whole-project film's public embedded controls and separate watch page were tested muted. Legacy-first/fresh-user and other stated hardware/signing limits remain explicit.
