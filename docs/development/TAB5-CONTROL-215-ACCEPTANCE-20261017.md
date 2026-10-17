# TAB5 电脑控制部署验收 2026-10-17

最终结果：Windows 桥接已更新至包含电脑控制的 0.6.8 本地修订，TAB5 已安装 **0.2.215-ui**。启动核验通过，用户确认音量、静音、快捷启动图标/打开、锁屏/解锁状态及不再闪动。临时启动项已恢复为空。

## 实际部署

- Windows 固定运行路径：C:\Users\yaououzhong\Work\AI-bot/windows-app/AIBotBridge/bin/Release/net8.0-windows10.0.19041.0/AIBotBridge.exe。
- 五个已部署应用文件与 bridge-215-candidate 发布候选哈希逐一一致，见 bridge-215-hash-verification.json。原文件备份 bridge-before；锁屏修订前备份 bridge-before-lock-state。
- TAB5 固定路径：C:\Users\yaououzhong\Work\AI-bot/artifacts/firmware/tab5/latest/aibot_tab5.bin。
- 版本 0.2.215-ui，7,085,488 字节，SHA-256 `3440dca247e5a4176ac1781bc32930f40d244affde129a57a40f0441caa925b3`。
- 设备 e8f60ae2ec56，经 USB OTA；完整写入回读偏移 7,085,488 字节，previousStage=9，previousError=0。
- 精确启动核验：ota_0 / VALID，ELF `cc1634b3a91c22d029a607266d61b093cd23612d30c2dc18b3d67510b1d7110c`。
- 最后观察 uptime=182.801 秒，内部空闲 75,331 字节，最大连续块 32,768 字节，显示错误 0，RPC 失败计数均 0。

## 故障及修复

- 首次 213 镜像写入完成，但启动约 6 秒 abort 蓝屏，自动回退到 211；该次升级失败，不能称为升级成功。
- 新图标相关静态缓冲区占内部 RAM 50,768 字节。214 改为 PSRAM 分配，仅留下 12 字节内部指针；真实启动通过，内存恢复至约 65–75 KB。失败包、ELF、map 和崩溃记录均保留。
- 214 用户反馈锁屏后状态不变化、连接测试时闪动。215 增加 Windows 当前会话真实锁屏状态，显示已锁屏/未锁屏，锁定期间禁用重复锁屏，解锁恢复。
- 状态轮询不再使控件灰亮交替；启动项和音乐布局仅在实际变化时重设。回归检查 8 次稳定轮询产生的新增 LVGL 无效区域事件为 0。
- 运行目录采用 win-x64 发布布局，普通构建的串口依赖不匹配时被替换前校验拦住，随后采用匹配的发布产物。第二次替换等候路径规范化问题修正后完成，最终部署哈希全部通过。

## 验证

- dotnet build Release：0 警告、0 错误；win-x64 publish 成功。
- ComputerControlSelfTest：实际系统图标提取、缓存、目标变化、WTS 会话状态读取、锁定/解锁/未知状态、认证 RPC 与既有控制检查通过。OS 动作自测使用 fake 后端，不混称真机验证。
- 必需 status-once：通过正常桌面上下文执行 dotnet run，exit=0，输出为有效 JSON；私有业务状态不写入本报告。
- TAB5 scripts/build.ps1、LVGL 原生检查及真实 sidecar 预览通过。构建保留 8 物理核心/BelowNormal 约束；既有其他 UI 警告未作为零警告报告。
- 用户现场确认：音量跟随、静音/取消静音、两个图标及启动均正常；随后确认 215 的锁屏/解锁状态和无闪动两项均正常。
- 临时记事本和验证文件夹入口已清空，restore-launchers.result 为 LAUNCHERS_RESTORED_EMPTY。
- displayPolicy 的模式、轮播开关、间隔和页面列表与更新前一致。TAB5 未执行自动轮播重置。

## 限制和保留项

- Wi-Fi 当前为已联网/桥接未通，黄色含义准确；Wi-Fi 状态轮询并非 USB 待命。当前控制链路为 USB，未宣称 Wi-Fi 链路已通过，也未为此修改系统网络或防火墙。
- 后台音量采样启动、最终桌面配置文件字节哈希复核被自动审批拦截，工具未给具体原因；没有绕过。采样没有启动，桌面哈希复核未完成。
- 从 Codex 进程直接读取配置文件得到的 hash 对比与正常桌面资料视图可能不同，不能用来宣称正常桌面配置已改变或字节一致。最终应用诊断显示正常 Roaming profile、pairMatchesDisk=True、原配对时间 2026-09-30；显示设置运行态已逐项对照通过。
- 原运行文件和配置备份均保留。不含长期稳定性或三通道全量验收结论。未提交、推送、打标签或公开发布。

## 证据

- final-215-tab5.json、ota-verify-215.json、final-crashes.json
- bridge-215-hash-verification.json、bridge-215-build.log、bridge-215-publish.log、bridge-215-selftest/result.txt
- build-215-lock.log、preview-215-lock.log、notes-215.log、notes-215.png
- icon-internal-bss-comparison.json、failed-213-aibot_tab5.elf、failed-213-aibot_tab5.map、postboot-crashes.json
- source-214/、source-215/、source-hashes-214.json、source-hashes-215.json
