# TAB5 .122 安装核验 / Installation verification

2026-10-06，用户同意收尾安排并在设备上完成安装，随后确认升级完成。此次仅加粗倒计时时间数字及 d/h/m 单位，保持灰蓝色、字号、字宽与位置；桥接二进制未更换。

Wi-Fi 压缩 OTA 安装完成：镜像 SHA-256 `a310c727df71ef3753ed3f0098227dfae0fc3d516fb7a3dc578c3cc0941f59db`，6,942,496 字节；实际传输 3,623,525 字节，电脑发送 7.750 秒。USB 精确启动核验：ELF `99b321aabf77f4c9149677228aade1c06e409da15cd1c55b26fdfd60f3f6fb49`，`ota_0 VALID`，previousStage=9，previousError=0，完整镜像字节一致。设备 prepareMs=8021、writeMs=9597、verifyMs=909、installMs=19154，不含完成提示停留及重启。

正常退出桥接、执行 --restore-cycle-default 后正常启动。最终 /status 确认 auto、cycleEnabled=true、原六页顺序和 15 秒间隔；配对一致。证据位于 artifacts/development/tab5-122-countdown/deployment/，包括 boot-verified.json 和 status-final.json。无 Git 提交或公开发布。

视觉反馈：用户确认升级完成，但认为“距重置 4d 8h”仍偏暗，询问是否应与“本周已使用”同色。随后用户同意保留灰蓝色提示、将时间提亮为右侧重置日期同款浅白色。源码及预览已调整、构建通过，设备仍为原 .122 配色；新颜色尚未安装，视觉验收尚未最终通过。此前 .121 日常语音验收继续有效。

The user confirmed .122 installation. Compressed Wi-Fi OTA and exact-image USB boot verification passed (ota_0 VALID, no upgrade error). Original automatic cycling, six-page order, 15-second interval and pairing are preserved. Bridge binaries are unchanged. The user finds the countdown too dark and requested color advice; the user subsequently approved a muted prefix with light time text. Source/preview and build checks pass; the revised color is not installed and final visual acceptance remains pending. Everyday .121 voice acceptance remains valid.

后续：此前确认的浅白色时间配色已随 .123 安装并通过精确镜像启动核验，见 TAB5-ACCEPTANCE-123.md；视觉反馈继续跟踪。

Follow-up: The approved light countdown time was installed with .123 and passed exact-image boot verification; see TAB5-ACCEPTANCE-123.md. Visual feedback remains separately tracked.
