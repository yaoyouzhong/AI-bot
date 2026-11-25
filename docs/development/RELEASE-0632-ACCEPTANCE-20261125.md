# 0.6.32 发布验收 / Release acceptance

2026-11-25。Windows x64 与 Apple Silicon Mac 桥接来自同一封存批次。TAB5 和 ESP8266 沿用已发布固件。 / Both desktop downloads come from the same frozen build. TAB5 and ESP8266 remain on their existing published firmware.

- 源码 / Source: 9e14d70399a46afe65f2c3fad99c6c1260366058
- 封存候选 / Candidate: cad7a5ded009303e6ad22b486780cec7d30ec6a1101708f07ddae5f45843858d
- [发布 / Release](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.6.32) · [构建 / Candidate run](https://github.com/yaoyouzhong/AI-bot/actions/runs/45592785988) · [发布 PR #52](https://github.com/yaoyouzhong/AI-bot/pull/52)

| 原包 / Asset | SHA-256 |
| --- | --- |
| Windows x64 setup | ea12f145b84e0a855eea82d3b323ccb3aace81dc67adee95b8e553f34c52814a |
| macOS arm64 ZIP | 5ed3a29ab622bd07328a21a1f6732e4d64441103b32bed3f7ca1ad250e59ac40 |

- Windows：原安装包在 Windows Sandbox 从 0.6.31 升级并启动，测试用配置、额度和本地解密校验通过。原生界面检查覆盖两列、三列和更新提示，目录检查保留实际项目会话并排除隔离验证会话。Windows 安装器无 Authenticode 签名。
- macOS：原生 Swift 测试 556 项、跳过 5 项、失败 0 项。发布构建、arm64、签名完整性、ZIP 内校验和许可文件通过。应用使用临时签名，未做 Apple 公证。带隔离标记的首次启动未单独验证。
- 真机：用户确认 Windows 和 Mac 都使用本次 0.6.32 封存候选接入实体 TAB5，实测无问题。Mac 从本批 ZIP 安装并启动。[双平台交接](WINDOWS-DEVICE-CENTER-HANDOFF-20261125.md)

Windows passed the sandbox upgrade from 0.6.31 and startup using the original installer. The Windows native layout and project filtering checks passed. The Windows installer has no Authenticode signature. Mac passed the native Release build and Swift tests (556 tests, 5 skipped, 0 failures), plus arm64, signature integrity, and archive checks. The Mac app is ad-hoc signed and its first launch with the quarantine flag was outside the verification scope. The maintainer confirmed that both frozen candidates worked with the physical TAB5 and installed and launched the Mac release from the exact ZIP.

中英文下载、安装和更新说明已同步。新增 Windows 更新角标截图，原有安装确认截图仍适用。项目介绍视频中的 TAB5 总览、会话提醒和其它场景仍适用，沿用原片和封面。 / Both languages point to the current desktop downloads. The update guide shows the new Windows badge. The existing product film, cover and installation confirmation picture remain applicable. [截图与来源 / Screenshot sources](../SCREENSHOTS.md) · [视频 / Film](../PRODUCT_VIDEO.md)
