# TAB5 318‑R4 源码增量

[源码增量 ZIP](source-overlay.zip) · [身份清单](manifest.json) · [R4 验收](../TAB5-REPLY-RETURN-R4-20261109.md)

27 个源码、测试及脚本文件，基于已归档的 [TAB5 0.2.318-ui 工程](../tab5-318-source/README.md)，整合原生会话来源与读取变化、未读提示、内部修订及 R4 回复导航。这个完整增量替代逐次应用旧补丁。不包含 BIN、ELF、主机工具链、用户数据或录音素材。

1. 在独立目录解压原 `.318` 归档，运行包内 `python verify.py`；将已验证的 `source/` 复制为新的 `<project>`。不要覆盖另一个工作区。
2. 从 AI-bot 根目录运行 `python docs/development/tab5-318-r4-source/apply_overlay.py <project> --check`、`--apply`、`--verify`。脚本对照 1006 个原工程文件、两个归档摘要及每个修改后的文件摘要。
3. 按原归档说明准备工具链、锁定依赖和授权素材。该次安装镜像需要 `firmware/main/assets/claude_notice.pcm`，准确 SHA-256 见 `manifest.json` 的 `localBuildInput`。该素材由有权使用它的本地构建端提供，不包含在源码 ZIP，也不视作 MIT 授权；使用替代提示音的构建要单独记录身份与验收。
4. 继续使用镜像/协议版本 `0.2.318-ui`，独立内部版本 `318-R4`。Windows 按原工程脚本构建；Mac 使用其原生工程流程。重新构建、打包及部署分别记录，不复用已验收镜像的哈希。

字体子集使用原工程附带的 SIL OFL 许可，保留原字体归属；其他第三方声明沿用完整基线归档。
