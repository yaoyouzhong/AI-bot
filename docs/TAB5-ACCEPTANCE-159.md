# TAB5 .159 实机验收 / Hardware acceptance

2026-10-10，按用户授权安装最终 `0.2.159-ui` 与配套 Windows 桥接。精确启动核验与 Codex 额度显示通过；Wi-Fi 上传明显改善，USB 稳定性未通过，不能将本轮记为三通道全部验收通过。

## 安装与显示

| 对象 | 身份与结果 |
| --- | --- |
| 固件 | 6,633,088 bytes；SHA-256 `6d53d1f43c915612ace8ef8038e20f09067af7ea48839bb4b97efb0a77cecbea` |
| 内嵌 ELF | `83e845dbfeba6a68c0b889b915ec1bcd12971d18b7aca2be974cc659b9a8748c`；实机 USB 核验匹配，`ota_0 / VALID` |
| Windows 桥接 DLL | `ce2b96fdc16b96ae6b9a3d5ed96502fd1e8fd6faa8c381b92e931902311825bb`；正常退出、备份与固定 Release 路径部署完成 |
| 启动 | 写入 6,633,088 / 6,633,088 bytes，保留升级错误为 0，界面心跳正常 |
| Codex 额度 | 运行态取得实际 604,800 秒周周期；用户确认小表盘、竖线与条内等分“显示正常” |
| 国产套餐 | 周期采集、缓存与协议测试通过；当前保留的套餐记录已过期，尚无有效套餐的在线及实机验收 |

Wi-Fi 升级由用户在设备端启动。桥接发送压缩数据 4,144,548 bytes 耗时 16.468 秒；设备保留阶段为准备 7.709 秒、写入 10.861 秒、校验 0.940 秒、安装 20.162 秒。阶段存在重叠，不能相加；发送时间不含完整重启与用户可见恢复。接收此次升级的是旧 .157，不能据此测量新 .159 的发送缓冲提升。

## 三通道测速

每次为 256 KiB RAM RPC 有效数据，上传校验 SHA-256，下载另逐字节校验；不包含 Flash 写入或图片解码。四组共计划 72 次，实际执行 64 次，62 次通过、2 次失败；失败后跳过该组剩余 USB 轮次，继续测 Wi-Fi 与 BLE，跳过的 8 次不计作成功。

| 通道 | 上传通过 / 尝试 | 上传中位耗时 | 下载通过 / 尝试 | 下载中位耗时 |
| --- | ---: | ---: | ---: | ---: |
| USB | 8 / 8 | 0.731 s | 6 / 8 | 1.066 s，仅成功样本 |
| Wi-Fi | 12 / 12 | 0.965 s | 12 / 12 | 0.963 s |
| BLE | 12 / 12 | 6.454 s | 12 / 12 | 5.257 s |

Wi-Fi 上传中位有效速率约 265 KiB/s，12 次耗时范围 0.621–1.395 秒；此前 .157 的 3 次上传中位耗时为 18.032 秒，观察到的中位耗时比约 18.7 倍。这是非交错的小样本前后对照，不证明仅由 TCP 缓冲引起，也不代表 OTA、图片、长期速率或 P95 改善验收。

USB 两次下载分别在 98,304 和 196,608 bytes 后失败，错误为 `-1`，对应 3 / 5 次 RPC 调用。累计接收回复字节为 147,944 / 246,578，但没有取得有效 RPC 结果。代码中的该错误包含解密、数据解析、分配等多种可能；现有诊断无法确认具体原因。不能用已恢复连接或随后成功的一组测试覆盖失败记录。

## 稳定性与收尾

原计划观察 60 分钟，因已出现明确失败，于约 9 分 33 秒停止并保留判定。共 283 个样本，读取失败 0；单一桥接进程，设备运行时间连续，显示错误和 LCD 欠载均为 0。11:41:58 出现一次 USB 指标 ACK 超时（4.015 秒，收到 0 bytes），随后记录 RPC 邮箱断开。与两次下载失败合并判定 USB 稳定性未通过；本轮没有完成 60 分钟验收。观测期间未见新的 BLE 断开，不代表完成长期 BLE 稳定性验证。

测速完成后正常退出桥接，运行 Release `--restore-cycle-default` 并正常启动。`/status` 读回 `selectedMode=auto`、`cycleEnabled=true`；页面顺序为 Codex、DeepSeek、智谱、天气、股票、系统，间隔 15 秒，与操作前一致。固定 Release DLL 哈希再次匹配。重启后清空的进程诊断计数不能用作修复证明。

仍需定位 USB 失败、复核修复后的持续运行及图片 / 会话 / 重连主链路，并以有效国产套餐验证在线周期。已安装镜像、配套桥接及用户配置没有因排查而替换；未提交、推送或公开发布。

证据位于 `artifacts/development/quota-cycles-20261010/`：`deployment-result.json`、`boot-verified-159.txt`、`physical-quota-ui-confirmation.json`、`runtime-quota-after-deployment.json`、`hardware-benchmark-1.json` 至 `hardware-benchmark-4.json`、`hardware-benchmark-summary.json`、`link-observation-final.json`、`link-observation-stop.json`、`policy-before.json` 和 `policy-after-verification.json`。本地构建与模拟测试边界见[三通道报告](TAB5-TRANSPORT-157.md)和[国产额度配置](DOMESTIC_QUOTA_SETUP.md#额度周期与-tab5-159)。

## English record

The authorized final .159 image and matching bridge were installed locally. The identities above match the image, embedded ELF and fixed Release bridge DLL. USB boot verification confirmed ota_0 / VALID, all application bytes written, no retained upgrade error and live UI heartbeats. The user confirmed the Codex clock face, vertical marker and internal divisions; the running bridge supplied the actual 604,800-second weekly period. Domestic API/cache/protocol fixtures passed, but retained plan records are expired, so current domestic-plan account and hardware acceptance remain pending.

The Wi-Fi installer sent 4,144,548 compressed bytes in 16.468 seconds. Retained prepare/write/verify/install times are 7.709/10.861/0.940/20.162 seconds; these phases overlap. Sending excludes the complete restart and visible recovery. The old .157 received this upgrade, so its installation time does not measure the new send-buffer change.

Four device RAM RPC suites planned 72 rounds and executed 64: 62 passed and two USB downloads failed. Each transfers 256 KiB with SHA-256 and download byte checks; Flash writing and image decoding are excluded. A failed channel skips its remaining rounds, leaving eight USB rounds unexecuted. The table lists actual attempts; the USB download median excludes failures. Wi-Fi and BLE each passed all 24 direction checks. Wi-Fi upload median was 0.965 seconds (about 265 KiB/s), ranging from 0.621 to 1.395 seconds across twelve samples, versus 18.032 seconds across three earlier .157 samples. The observed median ratio is 18.7; non-interleaved small samples do not establish single-parameter causation, long-run or P95 improvement, or OTA/gallery acceptance.

USB downloads failed after 98,304 and 196,608 bytes with error -1. Full-sized reply byte counts were recorded, but no usable RPC result was produced; existing diagnostics cannot distinguish decryption, parsing or allocation failures. A 4.015-second metrics ACK timeout also occurred. The planned 60-minute observation stopped after this explicit failure at about 9 minutes 33 seconds: 283 samples, no sample-read failure, one bridge process, monotonic device uptime and zero display errors/underruns. No new BLE disconnect was observed, but full-duration stability was not completed. USB stability failed and successful later samples do not erase it.

Normal exit, Release carousel restoration and normal startup restored the original six pages in order with auto mode, cycling enabled and a 15-second interval. The fixed runtime DLL hash was rechecked. Remaining work includes USB root-cause diagnosis, sustained operation and gallery/session/reconnect checks after a verified fix, and current domestic-plan account validation. No commit, push or public release was performed. Exact evidence files are listed above.
