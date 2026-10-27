# ESP8266 重置卡字重调整

用户反馈 Codex 重置信息字体偏细。保持已选定的 14px 字号、框尺寸、数量/日期对齐和动画避让范围，仅调整 `CreditTextRaster.h` 的面积覆盖强度：半覆盖笔画达到原前景色，完整覆盖不超出原色，空白像素保持黑色。没有横向重影或扩大字形占用范围。

使用实际 TFT_eSPI Font2 和生产栅格函数生成前后对照。3,459 项原生检查通过，包含细笔画不丢失、十个数字互异、空白不染色、颜色上限和半覆盖笔画对比度。对照字符行保持 175×14，892 个像素变亮，没有像素变暗或空白间隔被填充。

证据：`artifacts/esp-credit-weight/comparison.png`、`raster-results.json`、`firmware-build.log`。图片是实际字形的离线栅格结果，不是设备照片。此轮仅修改源码并构建验证，未刷写 ESP8266、未发布，也未改变用户展示设置；最终实屏字重仍需安装后确认。

English: Increase coverage strength for the existing 14px reset-credit text without changing its footprint, alignment or badge geometry. Production Font2 raster checks retain blank gaps and distinct digits. The before/after image is an offline render, not a device photograph. No ESP8266 installation or public release is claimed.

构建结果：python -m platformio run -d firmware 成功（119.18 秒）；git diff --check 通过。此为源码验证构建，未登记新发布版本、未提供安装或刷机。
## 0.5.7 本地安装（2026-10-27）

用户随后明确授权刷入小屏并保留轮播页面、顺序和间隔。已通过 COM7 给 ESP8266 8c:4f:00:52:33:3b 安装 0.5.7，镜像 540432 字节，SHA-256 为 `c5557ae42830b20075b573b3a7b2abc8e4d7afa208225d5231062427ede6e387`。写入前完整备份 4 MiB flash，写入后摘要验证通过；重启前整片读回比较，程序扇区末尾 540672 起的 3653632 字节完全相同，文件系统及设备配置未改变。完整备份留在忽略的本地 artifacts 目录，不纳入提交或公开发布。

重启后设备回报 firmware=0.5.7、brightness=80、usb_active=true、bridge_online=true。原轮播顺序为 codex、domestic_deepseek、domestic_zhipu、weather、stocks、system，间隔 15 秒；操作前暂时停在手动 Codex（cycleEnabled=false）。证据位于 `artifacts/esp-credit-weight/install-057/`。

收尾状态：桥接已正常退出，准备按项目规则执行 `--restore-cycle-default` 并正常启动。此时桌面控制返回拒绝访问，检测到 LockApp，HTTP 服务已退出；已请求用户解锁。自动轮播恢复及最终在线复核尚未完成，不能将安装启动证据视为收尾完成或用户对字重效果的验收。TAB5 未刷写。无公开发布。

English: ESP8266 0.5.7 was flashed and verified locally; all bytes outside the application sectors were preserved. The device booted and reported 0.5.7 over USB. Final bridge restart and restoration of the original six-page, 15-second automatic cycle are pending desktop unlock. Visual weight acceptance remains with the user; nothing has been published.
## 安装收尾与用户实屏验收完成

用户解锁后，通过资源管理器正常运行已安装桥接的 `--restore-cycle-default`，等待退出再正常启动。已将 `/status` 的 displayPolicy 与刷机前记录逐项比较：selectedMode=auto、cycleEnabled=true，原 6 页及顺序完全一致，intervalSeconds=15。设备回报 0.5.7、brightness=80、USB 与桥接在线；连续诊断 uptime_ms 从 1236863 增至 1263646、usb_status_count 从 43 增至 56，实际页面从 domestic_zhipu 切换到 weather，轮播运行正常。收尾证据：`install-057/after-display-policy.json`、`after-device.json`。

用户随后明确反馈“现在字体清晰不少”，本次字体清晰度实机验收通过。此前的锁屏收尾阻碍已解除。未创建标签、推送或公开发布。

English: Closeout completed after desktop unlock. The original six pages, order and 15-second interval were verified unchanged, with automatic cycling enabled. Firmware 0.5.7 is online over USB, and live counters and page transitions advance. The user confirmed improved text clarity. No public release was performed.
