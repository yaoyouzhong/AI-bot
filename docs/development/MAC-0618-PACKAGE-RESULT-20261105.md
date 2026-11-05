# Mac 0.6.18 原包交接结果

来源平台为 macOS Apple Silicon，接收平台及唯一发布负责人为 Windows。本记录提交不改变已构建原包的源码身份；桥接双平台候选源码仍为 `778e4332ac99a609439690a5892db4f4f1881e98`。本次无协议、固件或功能实现修改。

| 项目 | 状态和证据 |
| --- | --- |
| Mac 原包 | 已完成：`AI-bot-bridge-v0.6.18-macos-arm64.zip`，18,946,336 字节，SHA-256 `07a8f31b11e3c08d31639d3b0e40a4e7e34d11e716c0816c1d560d5be7986203`。在 macOS 原生运行 `scripts/package_macos.sh`，并用 `release_pipeline.py stamp` 登记源码；ZIP、同名 `.sha256` 和 `BUILD-SOURCE-macos.json` 尚在 Mac 本地，尚未传送。安装包不进入 Git 历史。 |
| 自动化 | 已完成：469 项测试，1 项条件跳过、0 失败；29,068 项 parity 检查及 Release 构建通过。解包文件哈希、arm64、版本、包内 `SOURCE_SHA.txt` 和 flash-tool 自测通过；包内附 LICENSE 和第三方声明。 |
| 签名 | 已完成严格深度签名校验，实际为 `AI-bot Local Development` 本机开发签名、hardened runtime；未公证，不声称 Developer ID 公证。 |
| 实际包启动 | 已完成：校验同一 ZIP 后解压至临时安装目录，从解压的应用正常后台启动，进程存活 15 秒并有 TCP 监听；正常退出后恢复原桥接并确认其监听。此项不等同于完整升级体验验收。 |
| 资料保留 | 待验证，阻止发布：测试前后 `devices.json`、`birthdays.json`、`agenda.json`、`tab5-task-pins.json` 和应用偏好文件的合并摘要变化。四个数据文件保留测试之前的修改时间，偏好文件在测试期间被写入；未保存测试前各文件独立摘要或值，无法确定变化键或证明用户设置保留。不得把此项标为通过。 |
| 功能实测 | 已有无需修改：Mac 目录相对 `79670dac2cea6f0e12ac4a6744d45d0c61a99768` 仅 `Info.plist` 版本变化，打包及构建脚本未变。复用[原验收记录](MAC-WINDOWS-FINAL-ALIGNMENT-20261105.md)中正常入口真实大疆／豆包自动录音、停止与文字回填及音源指纹结果；诊断入口焦点保护拒绝不算成功。 |
| TAB5 | 已有无需修改：复用已验收 `0.2.318-ui`、`20261105-controls72` 原镜像，SHA-256 `fad7d04c5585f222951aa7ac13deb01e8664a2069679d32547fa9f058563e2be`；见[原验收记录](TAB5-CONTROLS72-ACCEPTANCE-20261105.json)。本次未刷机或更改设备展示设置。 |

Windows 发布负责人应取得上述原 ZIP、摘要及来源记录并校验哈希，先解决 Mac 资料保留的未验证项，再将结果绑定最终冻结候选；不得将本记录提交的 SHA 写成包内源码 SHA。最终候选 ID、草稿位置和正式发布尚未确定。本交接提交与推送不代表已上传原包、创建标签或正式发布。
