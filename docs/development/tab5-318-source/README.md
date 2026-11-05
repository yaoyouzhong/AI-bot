# TAB5 0.2.318-ui 完整工程源码

[源码 ZIP](AI-bot-tab5-0.2.318-ui-source.zip) · [SHA-256](SHA256SUMS.txt) · [身份清单](SOURCE-MANIFEST.json)

在已归档 .313 完整工程上整合后续全部固件、配置及测试输入，包含 .314 草稿恢复、菜单关闭、持续显示读取保护、清空重绘、录音状态恢复和最终 72px 双圆；不需要另找旧补丁叠加。对应已安装并完成人工验收的 `20261105-controls72` 原镜像，BIN SHA-256 为 `fad7d04c5585f222951aa7ac13deb01e8664a2069679d32547fa9f058563e2be`。

沿用既有归档规则：保留完整工程、配置、锁文件、测试、授权素材、修改过的托管依赖和 SDK 补丁；不包含缓存、完整未修改的依赖／工具链、固件 BIN／ELF 和私有运行数据。先解压运行 `python verify.py`，按包内 README 准备锁定依赖和 SDK，再使用 `--resolved-project`／`--sdk` 校验；构建参数为 `-LocalArt -LocalVersion 0.2.318-ui`。本次归档没有重建或刷机，也不宣称独立逐位重建相同二进制。

运行改动绑定仓库提交 `6cc8528608c7e7ceb88fe59aadc171e07a24b33f`；独立完整工程本身以文件清单固定，不假称它有独立 Git HEAD。官方素材仍遵循已有授权和第三方归属，不属于项目 MIT 许可。

## English

This complete project integrates every post-.313 firmware change through the accepted controls72 image. No historical patch overlay is required. Preserve the project's configuration, dependency lock, tests, authorized art inputs, managed overrides and pinned SDK patches. Host caches/tools, unchanged dependency trees, BIN/ELF and private state are excluded. Verify the extracted archive and resolved inputs before building with the recorded version arguments. Packaging does not rebuild, flash or claim an independent bit-identical firmware reproduction.
