# TAB5 项目约定

- 本项目是 AI-bot 的独立 TAB5 客户端；公共电脑桥接继续维护在相邻 AI-bot 项目。
- USB、Wi-Fi、BLE 均为日常数据通道；自动模式优先 USB > Wi-Fi > BLE，以有效数据心跳判断可用性。
- 设备设置独立于原 ESP8266 小屏。保持原小屏协议和用户配置兼容。
- 官方 BSP 放在 firmware/components，保留许可和来源；.tools、.reference、build 不进入交付。
- Windows 固件构建统一使用 `scripts/build.ps1`；固件和本地预览构建默认通过 `scripts/BuildResources.ps1` 选择最多8个独立物理核心（混合架构最多2个性能核心，其余优先效率核心），并行编译数与核心预算一致，优先级为 BelowNormal，结束后恢复调用终端。遵守继承的 CPU 掩码；同构 CPU 尽量保留一个物理核心。临时构建脚本需复用同一 Enter/Exit 资源作用域，不能裸跑 idf.py/Ninja 绕过默认限制；不要扩大到系统级配置或其他任务。
- 设备配对凭据及 Wi-Fi 密码不进入源码、日志或发布材料。
- 构建、模拟协议测试和真机验收分别记录；不以编译通过代替三通道真机通过。
- 发布沿用公共桥接与独立 TAB5 固件方案；推送、标签和公开 Release 需单独授权。
- 本地固件交付遵循相邻 `AI-bot/docs/TAB5-FIRMWARE-WORKFLOW.md`：用准备脚本生成固定 `AI-bot/artifacts/firmware/tab5/latest/aibot_tab5.bin`，保留历史包，并预览实际更新说明。
