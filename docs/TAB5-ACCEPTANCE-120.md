# TAB5 .120 安装核验 / Installation verification

2026-10-05 用户授权更新新固件，并确认设备升级完成。配套 Windows 桥接已更新，文件哈希逐项一致；设备 e8f60ae2ec56 通过 Wi-Fi 压缩 OTA 安装。镜像 SHA-256：`a6b2722092ef1999f54ba4683e5173048235c57aa1bbefdefbaf4acdbc62f394`。

USB 启动核验确认 `0.2.120-ui`、ELF SHA-256 `11042734fb7424b8541bb2c6fbc7d47215d118f71e226aa32e60e19543ac7c97`、`ota_0 VALID`，完整镜像 6,940,608 字节，previousStage=9、previousError=0。实际压缩传输 3,622,293 字节，电脑发送 9.641 秒；设备准备 8.027 秒、写入 9.857 秒、校验 0.927 秒，安装至校验完成 19.449 秒，不含成功提示停留与重启。不能将这些重叠阶段简单相加。

本次新增的单行提示、“继续语音”按钮及点击 Codex 后空草稿恢复录音已通过内部测试；安装成功不等于交互真机验收。待确认：电脑发送或清空草稿后点击 Codex，豆包恢复录音；草稿仍有文字时保留原发送状态。

证据：`artifacts/development/tab5-120-draft-sync/boot-verified.json`、`deployment.json` 与 `final-status.json`。恢复自动模式、启用轮播，保持原六页顺序和 15 秒间隔；配对未修改。未公开发布。

The user authorized and confirmed installation on 2026-10-05. The paired Windows bridge was updated with exact file hashes. Wi-Fi compressed OTA and USB exact-image boot verification passed with ota_0 VALID and no upgrade error. Installation through verification took 19.449 seconds, excluding completion dwell and reboot. Internal UI/state regressions pass; the new hints and empty-composer voice reset still require user hardware acceptance. Original automatic cycling, six-page order and 15-second interval are preserved. Pairing was not changed; no public release.
