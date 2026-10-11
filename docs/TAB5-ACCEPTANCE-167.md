# TAB5 .167 天气、模型标识与书法原文 / UI installation record

2026-10-10。用户已明确授权“构建并安装新固件”。`.167` 已安装，完整 ELF／ota_1 VALID 启动核验通过，用户确认三个修改项“显示正常”。已恢复原自动轮播，未公开发布。

| 产物 | SHA-256 |
| --- | --- |
| `0.2.167-ui` 镜像 | `7a2d004f1fff60feee9fce1bac454e4770431a2efd1a7c419ae2b6d69a9b5fc1` |
| ELF，与镜像内嵌值一致 | `e80133021f525e2a3dd791e5173d55027e3d80f188f6b8b089243d4dbc6e610b` |
| 更新说明文本 | `00f5a7f7cbba8339b39c7ec30ae941e708057807c31dbffb98f8c2d0f073493f` |
| 沿用的 Windows 桥接 DLL | `6a9476363f7e7064aadc0000ab519f8dc6aaab909d31f37b51702e69cf1f88f6` |

镜像 6,689,424 字节，OTA 分区剩余 519,536 字节。固定交付路径为 `artifacts/firmware/tab5/latest/aibot_tab5.bin`，独立历史归档为 `versions/0.2.167-ui/7a2d004f1fff-00f5a7f7/`。

## 纳入与构建 / Inclusion and build

- 相对已安装 `.166`，仅 `main/ui.c`、`main/ui_weather.h`、`main/ui_gallery_reader.h` 与 `main/brand_logos.h` 四个设备端文件变化。组件、依赖、分区、sdkconfig 和缓存／USB 源码一致。
- 最低、最高温度采用冷暖配色、方向标记及当前值范围条；全部 11 个总览模型选项增加官方厂商 Logo，与勾选框间距至少 16 像素，名称间距约 12 像素；Codex 可见图案归一化。
- 原文放在作品详情，释义单独标记。867 件书法目前有 14 件已核实原文；其余作品明确提示未核实，未新增推测转录。
- 独立快照 `.tools/upgrade167/firmware` 的 7,103 个输入文件逐项 SHA-256 一致；构建后 153 个主程序／配置文件及 7 个关键编译单元核对通过，启用正式书画及 USB 配置，无 `TAB5_UI_PREVIEW`。
- Ninja 的实际 `ui.c` 依赖包含三个已改头文件。分区检查及显示／Flash 的 ELF 安全检查通过。原 14 份厂商像素资源不变，仅增加列表专用 OpenAI 变体。
- ESP-IDF 构建退出码 0。触摸 API 弃用、既有未使用变量／函数警告保留在完整日志，未通过关闭警告或测试断言制造通过。

## 安装与验证 / Installation and validation

安装前后均核对自动模式、轮播开启、原六页顺序 `codex / domestic_deepseek / domestic_zhipu / weather / stocks / system` 和 15 秒间隔。

- 用户在 TAB5 实际点击安装；Wi-Fi 压缩流传输 4,183,323 字节、13,188 ms，设备记录完整镜像 6,689,424 字节、previousStage=9、previousError=0、installMs=27,122。
- 正式桥接“核验启动（USB）”通过三次新鲜设备应答，完整 ELF 与上表一致，运行分区 `ota_1`、状态 `VALID`。显示错误为 0。
- 用户分别检查天气冷暖配色、11 个模型 Logo 和“作品详情 → 原文 → 返回”，回复“显示正常”。后续提出的原文覆盖扩充和页签底色属于新的改进，不冒称已包含在本镜像。
- 使用资源管理器启动原 Release 程序的官方 `--exit`、`--restore-cycle-default`、正常启动入口；旧进程 20692 正常退出，新进程 37068。`/status` 读回原六页与 15 秒间隔完全一致。
- 数据目录仍为原 Roaming 用户目录，`test=False`、`pairMatchesDisk=True`，配对文件时间未变化。命令行启动曾被数据目录保护拒绝；没有修改或绕过保护。
- 重启、桥接正常退出期间存在预期断链与 RPC 中断。最终设备累计 RPC 失败诊断为 `2,5,-1005,49236`，不是零失败；本次没有重跑传输测速，也不把桥接内部 110 ms 连接阶段当作物理重连全程。

## 证据 / Evidence

`artifacts/development/upgrade-167-20261010/`：`source-snapshot.json`、`changed-from166.json`、三个界面文本差异、`built-inclusion-audit.json`、`display-flash-safe.json`、`candidate.json`、构建日志、实际 sidecar 原生断言预览、`boot-verified-diagnostics.json`、`restored-display-policy.json`、`restored-diagnostics.json`。

构建前的源码预览、温度边界、11 个实际 Logo 点击、全目录字形及四方向原文操作记录见 [UI refinement record](development/TAB5-UI-REFINEMENTS-2026-10-10.md)。

## English

The user authorized building and installing the new firmware. The .167 image is installed and exact USB boot verification confirms the matching full ELF and ota_1 VALID. The user confirms all three visual changes render correctly. There is no public release.

Four UI files change from the installed .166 snapshot. Weather temperature styling, all eleven provider logos and Original Text inside calligraphy Details are included. Only fourteen of the 867 calligraphy works currently have reviewed transcriptions; unavailable originals are clearly identified. Cache, USB fixes, component/configuration inputs and partition layout remain identical.

All 7,103 snapshot inputs match. Post-build checks match 153 main/configuration files and seven production translation units, including real dependencies on the edited UI headers. OTA layout and display/Flash ELF safety pass. Existing build warnings are retained in the log. Normal exit, the official restore-cycle-default command and normal Explorer launch restore the original six-page, 15-second auto carousel. The original profile and pairing are verified unchanged. Expected disconnect/RPC interruptions remain recorded; this does not claim zero USB failures. Later requests to expand original texts and style chapter backgrounds are separate uninstalled changes.
