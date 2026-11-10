# TAB5 Claude 完成语音清晰度候选（2026-11-08）

## 范围和试听

- 原设备播报的文字为“Claude任务已完成”，但用户反馈听起来像其他词。多段拼接的试听存在音色或语气不一致，其他整句试听的英文发音不够清楚，均未采用。
- 采用本机 macOS 神经网络中文语音整句合成“Claude，任务已完成”，试听经用户确认可用。生成脚本为 `scripts/generate_tab5_claude_neural_notice.swift`，通过 `afconvert -f WAVE -d LEI16@16000 -c 1` 转换后提取单声道 16 位 PCM；与用户认可的试听音频逐字节一致。此前用于本地候选；Apple 许可限制和用户后续提交决定见[源码增量说明](tab5-318-refresh-source/README.md)。
- PCM 长度 62,802 字节，SHA-256 `1533c2d5e353d28726cc3fc8cc5108df72b8a42349415a55806c2f0aaee5f023`；重新生成与试听一致，且完整 PCM 在候选 BIN 中恰好出现一次。

## 构建与实机状态

- 本地候选 `0.2.318-ui` BIN SHA-256 `f52945c59b392070b33c72c113646cca33a1e21c31f6b68af6fa91a22b3120f6`。独立 TAB5 工程构建成功，经固件准备脚本校验并归档，实际 sidecar 的横屏离屏渲染已检查，文字和操作区清晰。未改变界面布局。
- 用户授权后通过 USB 安装，设备启动验证确认精确 ELF `5d3ceefb20dab6322297bd7bccd34664a6fe8281e4184de76c60aa4df3a92d0c`、VALID 运行分区和三个实时界面样本；升级前后展示设置一致，日常桥接重新接回 USB。安装报告保存在本地忽略目录 `artifacts/mac-native-alignment-20261107/voice-clarity/install-neural-report.json`。
- 用户确认电脑试听可用，安装后再次反馈已听过声音、效果可以；这是用户对当前声音效果的实听反馈。上一候选的 Terminal 三通道测试记录见 [长任务追加验收](TAB5-CLAUDE-LONG-APPEND-20261108.md)，不得把上一镜像的测试直接写成本候选完整实机验收。
- 用户另反馈豆包语音输入已可以使用；本次反馈未指定音源、传输通道或独立测试步骤，不推断其余组合均已验收。
