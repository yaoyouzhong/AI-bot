# TAB5 318-R5：统一完成语音

用户试听并选择保留 Codex 原提示，只重做 Claude。原 Codex 与新 Claude 都使用 Microsoft Huihui Desktop。Claude 文案为「Claude，任务已完成」，SAPI 语速 1、音量 100；格式为 16 kHz、单声道、16 位有符号小端 PCM。两句在同一份试听中播放，前一句为原 Codex，后一句为新 Claude。

| 固件输入 | SHA-256 | 来源 |
| --- | --- | --- |
| `firmware/main/assets/reply_notice.pcm` | `51543d528f906319cca99982197ebb5708a696175a3570498ddd11cfddc0c513` | 既有 .318 源码归档，未改动 |
| `firmware/main/assets/claude_notice.pcm` | `0b2aa1bc16d5a8c76c3e79b6c3deec80447b6ddcae6d91a4dfca8044297b75d6` | 本机 Huihui Desktop 合成，用户已试听 |

从已校验的 .318 完整工程依次重放 [R4 源码增量](tab5-318-r4-source/README.md)、[LIST1 累计补丁](TAB5-PROJECT-SCROLL-LIST1-20261110.patch)及 [R5 补丁](TAB5-318-R5-RELEASE-NOTICE.patch)。R5 保持协议和对外版本 `0.2.318-ui`，将内部版本更新为 `R5`，并隔离离屏预览的 SHA-256 配置头以避免覆盖拼音内存桩。放入上述准确 PCM 后构建；[生成脚本](../../scripts/generate_tab5_claude_huihui_notice.ps1)用于本机重建 WAV，语音包版本不同时字节可能不同，仍须核对 PCM 摘要。

新提示不含 Apple 系统语音、不需要捆绑 Microsoft 引擎或语音包。当前新 PCM 只作为本地候选输入；未找到明确覆盖该文件公开再分发的 Huihui 授权依据，不把该本地构建记作可公开晋级的固件或源码包。Windows 和 Mac 桥接发布独立。实际设备播放、升级与设置保留应绑定同一镜像另行验收。
