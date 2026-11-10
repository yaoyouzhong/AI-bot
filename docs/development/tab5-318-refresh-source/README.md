# TAB5 最新源码增量与 Windows 重建（2026-11-08）

这里保留 Mac 本地历史镜像 `090d42a61077ea448f268a60e59ff6c0891d90c18a4a22211b33c9fff2d7505e` 的复核记录。其历史 `source-overlay.zip` 包含 20 个源码或测试文件及 1 个系统语音素材；该 ZIP 已移出待发布源码树，只保留在本地验收证据中。[manifest.json](manifest.json) 记录原始身份，ZIP SHA-256：`9360e68c281143ea5622a879ed7ba5981a02a3fa67d83481ba93c25f37e3aef3`。后续源码对齐使用[不含语音素材的 R4 增量](../tab5-318-r4-source/README.md)和[项目列表累计补丁](../TAB5-PROJECT-PICKER-20261110.md)。

本机对照结果：基线的 1006 个 `source/` 文件中 14 个发生变化、另新增 6 个源码或测试文件及 1 个语音素材，全部包含在增量中；对应字节与本次 Mac 本地构建工程一致。本地构建工程没有 Windows `scripts/build.ps1`，重建时保留归档中的 Windows 构建脚本。`firmware/sdkconfig` 未改，SHA-256 `ee9abd2fabb90d59c2f2e9f4432dcbf80e2ae782d546fef37a704ed6d3522b37`。此对照不包含工具链、构建产物及本地私有数据。

## Windows 操作

以下仅用于本地复核历史候选，需要已有历史 ZIP，并按原文件名在本目录临时提供该输入。正式构建使用上面的 R4/LIST1 源码及有再分发依据的音频。路径仅为示例，必须使用新目录，勿覆盖既有工程或未提交修改。需要 Python 3.11 或更新版本。

```powershell
$repo = (Get-Location).Path
$archive = Join-Path $repo 'docs\development\tab5-318-source\AI-bot-tab5-0.2.318-ui-source.zip'
$work = Join-Path $repo 'artifacts\tab5-windows-handoff'
if (Test-Path $work) { throw 'Choose a new empty handoff directory.' }
New-Item -ItemType Directory -Path $work | Out-Null
Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $work 'archive')
python (Join-Path $work 'archive\verify.py')
Copy-Item -Recurse (Join-Path $work 'archive\source') (Join-Path $work 'project')
$overlay = Join-Path $repo 'docs\development\tab5-318-refresh-source\apply_overlay.py'
python $overlay (Join-Path $work 'project') --check
python $overlay (Join-Path $work 'project') --apply
python $overlay (Join-Path $work 'project') --verify
```

每一步成功后再继续；脚本先核对基线包与增量包摘要、所有 1006 个原始文件和增量目标，发现原文件被改动或新增文件已存在会拒绝写入。`--verify` 核对合成工程的全部原始文件及 21 个增量文件。现成 `.318` 归档的 `verify.py` 必须在原始解压目录执行；它的 `--resolved-project` 检查针对**未叠加增量的基线**，不要将其报出的旧文件摘要当作更新后工程验证。依照归档 README 准备锁定 ESP-IDF、组件及授权素材，并用该基线脚本验证已解析输入；同时使用本目录的 `--verify` 验证源码增量。Windows 从归档保留的 `scripts/build.ps1` 构建；构建和设备验收须记录新的文件摘要，不承诺逐位复现 Mac 镜像。

## 语音与来源边界

固件 `CMakeLists.txt` 需要 `firmware/main/assets/claude_notice.pcm`。增量中包含 Mac 当前安装镜像使用的原文件，SHA-256 `1533c2d5e353d28726cc3fc8cc5108df72b8a42349415a55806c2f0aaee5f023`；使用 macOS 系统语音 Linfei 生成，具体方法见[语音记录](../TAB5-CLAUDE-VOICE-CLARITY-20261108.md)。[Apple macOS 许可第 2.F 条](https://www.apple.com/legal/sla/docs/macOSGoldenGate.pdf)未授权系统语音用于公开分享或再分发，包括非营利场景；用户知悉该限制后于 2026-11-08 指示继续提交。用户决定不改变 Apple 许可内容，本文件不声称获得了 Apple 单独授权；该素材不归入项目 MIT 许可。电脑桥接和 TAB5 可独立发布，新构建不等于已验收原包。

增量中的 `firmware/main/tab5_source_20.c` 是 Noto Sans CJK SC Regular 的 20 px、2 bpp 子集；原字体及 Adobe 版权按 SIL OFL 1.1 保留，来源与源字体 SHA-256 `2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b` 见完整工程 `THIRD_PARTY_NOTICES.md`，许可全文在 `firmware/main/NotoSans-OFL.txt`。该子集不是项目 MIT 许可素材。保留完整工程的其他第三方许可和素材声明。

## 实际校验

在 macOS 从仓库归档重新解压：原归档 `verify.py` 校验 1156 个文件通过；包含语音的增量 `--check`、`--apply`、`--verify` 均通过，逐项对照本地构建工程；改动原文件以及预先存在新增文件两种情况均被拒绝，未覆盖测试文件。此次未在 Windows 执行构建，也未重新刷机；设备此前的有限验收见[刷新优化记录](../TAB5-CLAUDE-REPLY-REFRESH-20261108.md)。
