# TAB5 .121 安装核验 / Installation verification

用户授权“更新”，随后确认设备升级完成和 USB 启动核验通过。配套桥接已部署，五个程序文件与候选哈希逐项一致；原程序已备份。运行 DLL SHA-256：`5e21d0941c212abb67ad57e082a2c84f08eaaa123c37c757acc277aee0b0ab03`。

设备 `e8f60ae2ec56` 通过 Wi-Fi 分块压缩 OTA 安装 `0.2.121-ui`。镜像 SHA-256：`aa7cffafc5a7ed5684dc40ec53baf608668ac161400638401862e0cece7291eb`。USB 核验确认 ELF SHA-256 `f8b92605172ea6d76d08d27c9fda54a7c675818509c7dc9d1e48df13b7201d57`、`ota_1 VALID`、previousStage=9、previousError=0，完整写入 6,941,424 字节。

压缩传输 3,623,132 字节，电脑发送耗时 9.938 秒。设备准备 7.951 秒、写入 9.620 秒、校验 0.907 秒、安装至校验完成 19.419 秒，不含成功提示停留与重启；阶段重叠，不简单相加。升级/核验期间曾记录 USB 断线/超时，最终已重连并成功核验，不据此宣称链路长期稳定。

正常退出桥接后执行 `--restore-cycle-default`，再无参数启动。最终 `/status` 确认自动模式、启用循环展示、原六页顺序与 15 秒间隔；配对一致且文件时间未变。没有再次提供升级、公开发布或 Git 提交。

2026-10-06 用户实测后反馈“效果还是不错的，一切正常”，本轮日常语音优化按用户反馈验收通过，不再列为重复待验。此反馈不等于已完成每个通道、主动失焦、断线和长时间运行的专项覆盖；这些边界仍按需验证。单次 60 秒限制未改变。

证据：`artifacts/development/tab5-121-voice-continuity/deployment/` 下的 `file-hashes.json`、`boot-verified.json`、`status-final.json` 和 `diagnostics-final.json`。

The user authorized and confirmed installation and USB boot verification. The paired Windows bridge is deployed with matching hashes. Compressed Wi-Fi OTA and exact-image USB verification passed: ota_1 VALID, no upgrade error, complete image. Installation through verification took 19.419 seconds, excluding completion dwell/reboot. Original automatic cycling, six-page order, 15-second interval and pairing are preserved. On 2026-10-06 the user reported that the result was good and everything worked normally; everyday voice optimization is accepted based on this feedback. This does not establish separate coverage of every transport, deliberate focus loss, disconnections or long-running behavior. No public release or Git commit.
