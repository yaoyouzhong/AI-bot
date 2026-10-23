# TAB5 0.2.222-ui 部署与集中验收（2026-10-18）

本精确候选已获用户批准刷入；现有桥接复用，Wi-Fi组件本轮不安装。固件位于统一latest路径，历史.221包及本次latest备份保留。

## 实机证据

- 设备e8f60ae2ec56，USB COM9；正式启动核验完成三个递增UI心跳。
- BIN SHA-256：`61e1a7d7a9541ad7d36ca2d86062731bbe2169edba9e3194d3ff31d97c1b952c`；大小7115904 bytes。
- 实际ELF：`f973546da8d6f15078ed6e6ec1678e88bcbe46d94fb09726af934e21022ec2d8`；ota_0，VALID。
- previousStage=9，previousOffset=previousTotal=7115904，previousError=0；previousStackFree=3848。
- 设备保留的阶段耗时：prepare8335ms、write9833ms、verify1384ms、install20410ms；transport=1。观察到Wi-Fi分块压缩传输4868660 bytes、8109ms，不能称为BLE传输。
- 所有五个桥接运行文件与操作前哈希一致。未安装tvnserver，未修改TAB5展示设置，未收集PIN。

## 保留的中间状态

现有桥接运行时拒绝通过新进程替换固件选择，首次offer未生效且没有开始安装；随后通过桌面Explorer正常退出并重新启动同一桥接、载入批准镜像。没有强制终止、替换二进制或绕过资料目录保护。

首次精确核验未通过；固件要求连续30秒健康运行后确认镜像，等待后再次只读核验通过。首次失败证据保留，未重复发起刷机。首次失败未保存原始分区状态，因此不把其具体失败字段写成已确认事实。

## 集中验收待用户反馈

1. 拔出USB并保持仅蓝牙，正常解锁后TAB5面板是否自动关闭。
2. 平时听得清的音量听几次“Codex任务已完成”，是否仍有啪声或破音；无需强行区分声音位置。

启动验证通过不等于上述功能听感验收通过。用户后续选择尝试TightVNC Server，本轮仍先完成以上集中验收；后续采用仅本机访问及独立组件密码，Windows PIN仍每次输入、不保存。

[部署证据](../../artifacts/development/tab5-unlock-audio-20261018/deployment/deployment-result.json)；[不可变批准清单](../../artifacts/development/tab5-unlock-audio-20261018/candidate-review.json)。

## 用户回报：基础解锁回归

用户报告解锁持续“电脑连接未就绪”，控制页显示电脑静音。.222基础解锁验收未通过，自动关闭及音频效果尚未完成真机验收。已核对主源码与运行桥接；根因尚未确定，未再次刷机。原.221回退包已校验隔离准备，等待本次回退授权。[诊断记录](../../artifacts/development/ble-unlock-not-ready-20261018/DIAGNOSIS.md)。
